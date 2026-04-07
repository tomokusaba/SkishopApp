# 品質チェックパターン集

フェーズ完了検証で使用する手抜き検出パターン・コード品質基準の詳細を定義する。

---

## 1. コード品質 — grep 検索パターン

検証時に以下のパターンを `grep_search` で実行し、該当箇所を記録する。

### 1.1 未完了作業の痕跡

| パターン | 対象ファイル | 判定 |
|---------|------------|------|
| `TODO` | `frontend/src/**/*.{ts,tsx}` | ❌ Phase 完了時は全て解消済みであること |
| `FIXME` | 同上 | ❌ 同上 |
| `HACK` | 同上 | ❌ 同上 |
| `XXX` | 同上 | ⚠️ コメント内容を確認し、一時的な回避策なら ❌ |
| `TEMP` | 同上 | ❌ 仮実装は許可しない |
| `placeholder` | 同上 | ⚠️ UI プレースホルダテキスト以外は ❌ |

### 1.2 デバッグコードの残存

| パターン | 対象ファイル | 判定 |
|---------|------------|------|
| `console.log(` | `frontend/src/**/*.{ts,tsx}`（テストファイル除外） | ❌ 本番コードに残してはならない |
| `console.warn(` | 同上 | ⚠️ 意図的な警告出力か確認 |
| `console.error(` | 同上 | ⚠️ エラーハンドリングの一部なら ✅ |
| `debugger` | 同上 | ❌ |
| `alert(` | 同上 | ❌ ユーザー通知は toast を使用すること |

### 1.3 TypeScript 型安全性

| パターン | 対象ファイル | 判定 |
|---------|------------|------|
| `: any` | `frontend/src/**/*.{ts,tsx}`（型定義ファイル除外） | ❌ 型を明示すること |
| `as any` | 同上 | ❌ 型の強制キャストは禁止 |
| `// @ts-ignore` | 同上 | ❌ 型エラーの握りつぶし禁止 |
| `// @ts-expect-error` | 同上 | ⚠️ テストコードなら許可、本番コードは ❌ |
| `// eslint-disable` | 同上 | ⚠️ 正当な理由のコメントがあるか確認 |

### 1.4 ハードコード検出

| パターン | 対象ファイル | 判定 |
|---------|------------|------|
| `localhost` | `frontend/src/**/*.{ts,tsx}`（`.env*` / テスト除外） | ❌ 環境変数を使用すること（§1.4） |
| `8080` / `8081` / `8082` / `8083` / `8084` / `8085` / `8087` / `8088` / `8090` | 同上 | ❌ ポートのハードコード禁止 |
| `http://` / `https://` | `frontend/src/**/*.{ts,tsx}`（テスト・コメント除外） | ⚠️ URL のハードコード。環境変数化されているか確認 |
| `api/v1` | `frontend/src/app/(ec)/**`, `frontend/src/app/(admin)/**` | ❌ ブラウザコンポーネントから直接 API パスを参照しない（BFF 経由） |

### 1.5 秘密情報のハードコード

| パターン | 対象ファイル | 判定 |
|---------|------------|------|
| `password` = (文字列リテラル代入) | `frontend/src/**/*.{ts,tsx}`（テスト・型定義除外） | ❌ |
| `secret` = (文字列リテラル代入) | 同上 | ❌ |
| `token` = (文字列リテラル代入) | 同上 | ⚠️ テストのモックデータか確認 |
| `apiKey` = (文字列リテラル代入) | 同上 | ❌ |

---

## 2. アーキテクチャ準拠チェック

### 2.1 BFF パターン準拠

**ルール**: ブラウザで実行されるコンポーネント（`src/app/(ec)/`, `src/app/(admin)/`）は、バックエンド API（`/api/v1/`）を **直接呼ばない**。必ず BFF Route Handler（`src/app/api/`）経由で呼び出す。

**検証方法**:

1. `src/app/(ec)/` と `src/app/(admin)/` 配下のファイルで以下を検索:
   - `api/v1/` — 直接 API パスの参照
   - `API_BASE_URL` — サーバー側環境変数の参照（ブラウザコンポーネントでは使用不可）
   - `fetch('/api/v1/` — 直接 fetch 呼び出し

2. ブラウザコンポーネントからの API 呼び出しは以下のパターンのみ許可:
   - `/api/auth/...` — BFF 認証エンドポイント
   - `/api/products/...` — BFF プロキシエンドポイント
   - `/api/cart/...` — BFF カートエンドポイント
   - その他 `/api/` 配下の BFF Route Handler

### 2.2 環境変数管理

**ルール**: `front-end-need.md` §1.4 に基づき、API ベース URL は環境変数で一元管理する。

**検証方法**:

1. `src/lib/env.ts` が存在し、zod でバリデーションされていること
2. `API_BASE_URL` が `src/bff/` 配下でのみ参照され、`src/app/(ec)/` や `src/app/(admin)/` からは参照されていないこと
3. `.env.example` に全必要変数が記載されていること

### 2.3 i18n-ready 準拠

**ルール**: `front-end-need.md` §4.6 に基づき、UI テキストはハードコードせず翻訳キー方式を使用する。

**検証方法**:

1. JSX 内の日本語テキストリテラルを検索（`grep_search` でかな/カナ/漢字を検索）
2. 以下は許容:
   - `locales/ja.json` 内のテキスト
   - テストファイル内のテキスト
   - コメント
3. 以下は **不許可**:
   - コンポーネントの JSX 内に直接記述された日本語文字列
   - ボタンテキスト、ラベル、プレースホルダの直書き

### 2.4 RFC 7807 エラーハンドリング準拠

**ルール**: `front-end-need.md` §4.2.1 に基づき、全 HTTP ステータスに対応したエラーハンドリングが実装されていること。

**検証方法**:

`src/lib/error-handler.ts` で以下の全ステータスの分岐が存在すること:

| HTTP ステータス | 必須処理 |
|---------------|---------|
| 400 | `errors` 配列 → フォームインラインエラー、なければトースト |
| 401 | トークンリフレッシュ試行 → 失敗時ログインリダイレクト |
| 403 | 「権限がありません」トースト |
| 404 | 「見つかりません」+ 一覧ナビゲーション |
| 422 | `detail` メッセージをトースト表示 |
| 429 | 指数バックオフリトライ + トースト |
| 500 | 「予期しないエラー」トースト |
| 503 | サービス一時停止 + 自動リトライ |
| ネットワークエラー | 「通信エラー」+ リトライボタン |

### 2.5 縮退 UI 準拠

**ルール**: `front-end-need.md` §4.2.3 に基づき、画面ごとに必須/任意 API を分離し、任意 API 障害時はセクション非表示。

**検証方法**:

対象フェーズの画面で、§4.2.3 の縮退マトリクスに記載された全行が実装されていること:

1. 任意 API: `Promise.allSettled` で呼び出し、`rejected` 時にセクション非表示
2. 必須 API: 失敗時にエラープレースホルダ + リトライボタン表示
3. フォールバックコンテンツ（AI チャット障害時の問い合わせリンク等）

### 2.6 ページネーション準拠

**ルール**: §4.2.2 の `PagedModel` 形式に準拠。

**検証方法**:

1. レスポンスの `content[]` + `page { size, number, totalElements, totalPages }` 構造を処理していること
2. EC: 無限スクロール（Intersection Observer）で `page.number + 1 < page.totalPages` の間ロード
3. Admin: テーブルページネーション（件数表示、ページサイズ切替 20/50/100）
4. `page` パラメータが **0 始まり** であること（1 始まりになっていないか確認）

---

## 3. セキュリティチェック

### 3.1 トークン保存方式

**ルール**: `front-end-need.md` §4.3.1 に基づき、トークンの保存先を厳格に管理する。

| 検索パターン | 対象 | 判定 |
|------------|------|------|
| `localStorage.setItem` | `frontend/src/**/*.{ts,tsx}` | ⚠️ トークン保存に使用していたら ❌。ダークモード設定等は ✅ |
| `sessionStorage.setItem` | 同上 | ⚠️ チェックアウト状態以外でトークンを保存していたら ❌ |
| `document.cookie` | 同上 | ⚠️ Cookie を直接操作していないか確認（BFF / next-auth 経由であること） |

### 3.2 入力バリデーション

**ルール**: 全フォームに zod スキーマバリデーションが実装されていること。

**検証方法**:

対象フェーズのフォームコンポーネントで:

1. `react-hook-form` の `useForm` が使用されているか
2. `@hookform/resolvers/zod` で zod スキーマが接続されているか
3. zod スキーマが `front-end-need.md` §6.1 のバリデーションルール（文字数制限、メール形式、パスワード強度等）に準拠しているか

### 3.3 認可チェック

**ルール**: 管理画面のコンポーネントにロールチェックが実装されていること。

**検証方法**:

1. `src/middleware.ts` で `/admin/*` パスに ADMIN/MANAGER ロールチェックがあるか
2. Admin レイアウト/コンポーネントでロール判定（`isAdmin`, `isManager`）が行われているか
3. MANAGER ロールで ADMIN 専用機能が非表示になるか（§3.1 画面一覧の「必要ロール」列を参照）

---

## 4. テスト品質チェック

### 4.1 テストファイルの存在

対象フェーズで作成された以下のファイルカテゴリに対し、テストファイルが存在すること:

| カテゴリ | テストファイルの命名規則 | 必須度 |
|---------|----------------------|--------|
| ページコンポーネント | `page.test.tsx` | 必須 |
| 共通コンポーネント | `{component}.test.tsx` | 必須 |
| hooks | `{hook}.test.ts` | 必須 |
| lib ユーティリティ | `{util}.test.ts` | 必須 |
| BFF Route Handler | `route.test.ts` | 推奨 |
| zustand ストア | `{store}.test.ts` | 推奨 |

### 4.2 テストの充実度

以下のパターンは「スタブテスト」として ❌ とする:

```typescript
// ❌ 空のテスト
it('should work', () => {
  expect(true).toBe(true);
});

// ❌ render のみでアサーションなし
it('should render', () => {
  render(<Component />);
});

// ❌ スナップショットテストのみ
it('matches snapshot', () => {
  const { container } = render(<Component />);
  expect(container).toMatchSnapshot();
});
```

✅ 適切なテストの例:

```typescript
// ✅ レンダリング + コンテンツ確認
it('should display product name', () => {
  render(<ProductCard product={mockProduct} />);
  expect(screen.getByText('スキーブーツ Pro')).toBeInTheDocument();
});

// ✅ ユーザー操作 + 結果確認
it('should add item to cart on button click', async () => {
  render(<AddToCartButton product={mockProduct} />);
  await userEvent.click(screen.getByRole('button', { name: /カートに追加/ }));
  expect(mockAddToCart).toHaveBeenCalledWith(mockProduct.id, 1);
});

// ✅ エラーハンドリングのテスト
it('should display error message on validation failure', async () => {
  server.use(
    http.post('/api/auth/login', () => HttpResponse.json(problemDetail400, { status: 400 }))
  );
  render(<LoginForm />);
  await userEvent.click(screen.getByRole('button', { name: /ログイン/ }));
  expect(screen.getByText(/有効なメールアドレスを入力してください/)).toBeInTheDocument();
});
```

### 4.3 テストカバレッジ基準

| カテゴリ | 目標カバレッジ |
|---------|-------------|
| 共通コンポーネント（`src/components/`） | ≥ 80% |
| lib ユーティリティ（`src/lib/`） | ≥ 90% |
| hooks（`src/hooks/`） | ≥ 80% |
| ページコンポーネント | ≥ 60%（MSW でモック） |

---

## 5. スタブ実装検出パターン

以下のパターンが本番コードに存在する場合、「手抜き実装」として ❌ とする:

| パターン | 説明 | 例 |
|---------|------|-----|
| 空の関数ボディ | イベントハンドラやコールバックが空 | `onClick={() => {}}` |
| 固定値の返却 | API 呼び出しなしにダミーデータを返す | `return { products: [] }` |
| コメントのみの関数 | ロジック未実装 | `// TODO: implement validation` |
| 条件分岐の省略 | 全パスの処理が実装されていない | `if (status === 200) { ... }` で 400/500 が未処理 |
| ハードコードされたテストデータ | 本番コードにモックデータが混在 | `const user = { name: 'テスト太郎' }` |
| `throw new Error('Not implemented')` | 実装が完了していないマーカー | — |
| `return null` の多用 | エラー時に何も表示しない | コンポーネントが条件不一致で `return null` |
