# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/front-end-need.md`
- **判定**: ⚠️ **Conditional Approval** — High 指摘あり、人間の判断を介在
- **レビュー日時**: 2026-04-03
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)
- **参照ドキュメント**: `spec.md`, `AGENTS.md`, ADR-0007〜0009, `.github/instructions/` 各ファイル

## 技術スタック検証結果

| カテゴリ | front-end-need.md 記載 | spec.md 定義 | AGENTS.md 定義 | 整合性 |
|---------|----------------------|-------------|---------------|--------|
| フレームワーク | Next.js 15 (App Router) | Next.js 14（React） | — | ❌ バージョン不一致 |
| 言語 | TypeScript 5.x | TypeScript 5 | — | ✅ |
| 状態管理 | Zustand / Jotai | Redux Toolkit / Zustand | — | ⚠️ Jotai vs Redux Toolkit 不一致 |
| API 通信 | TanStack Query (React Query) v5 | Axios, React Query | — | ⚠️ Axios の廃止判断不明 |
| フォーム管理 | React Hook Form + Zod | — | — | ✅（spec.md 未定義のため） |
| UI コンポーネント | Tailwind CSS + shadcn/ui | TailwindCSS, Headless UI | — | ⚠️ shadcn/ui vs Headless UI 不一致 |
| テスト | Vitest + Testing Library + Playwright | Jest, React Testing Library, Cypress | — | ⚠️ テストフレームワーク不一致 |
| i18n | 未明記 | next-intl | — | ❌ 未記載 |
| 管理画面 | Next.js 共通 | ASP.NET Core + Razor Pages | — | ❌ 管理画面技術の不一致 |

## 指摘サマリー

| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ⚠️ Conditional | 0 | 2 | 3 | 1 |
| architect | ⚠️ Conditional | 0 | 3 | 2 | 0 |
| programing-reviewer | ⚠️ Conditional | 0 | 1 | 2 | 1 |
| dba-reviewer | Pass | 0 | 0 | 1 | 0 |
| security-reviewer | ⚠️ Conditional | 0 | 2 | 2 | 0 |
| compliance-reviewer | ⚠️ Conditional | 0 | 2 | 1 | 0 |
| audit-reviewer | Pass | 0 | 0 | 2 | 0 |
| qa-manager | ⚠️ Conditional | 0 | 1 | 1 | 0 |
| performance-reviewer | ⚠️ Conditional | 0 | 2 | 1 | 0 |
| infra-ops-reviewer | Pass | 0 | 0 | 1 | 1 |
| release-manager | Pass | 0 | 0 | 1 | 0 |
| oss-reviewer | ⚠️ Conditional | 0 | 1 | 0 | 0 |
| ux-accessibility-reviewer | ⚠️ Conditional | 0 | 1 | 2 | 1 |
| tech-lead | ⚠️ Conditional | 0 | 3 | 2 | 0 |
| **合計** | | **0** | **18** | **21** | **4** |

## 判定根拠
- Critical 指摘: 0 件 → 自動 Rejected にはならない
- High 指摘: 18 件 → Conditional Approval。実装着手前に是正または人間の受容/是正判断が必要
- 最も重大な指摘: Next.js バージョン不一致（15 vs 14）、Core Web Vitals 指標の陳腐化（FID → INP）、ゲスト購入フロー・越境データ移転同意 UI の未記載、管理画面技術スタックの矛盾

---

## Critical/High 指摘一覧（修正必須）

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|--------------|----------|----------|
| H-01 | **High** | tech-lead, architect | 技術スタック整合性 | §1.2 技術スタック参考 | **Next.js バージョン不一致**: front-end-need.md は「Next.js 15 (App Router)」、spec.md §技術スタック は「Next.js 14（React）」。どちらが正か不明。App Router は Next.js 13+ で利用可能だが、バージョン差異は破壊的変更を含む可能性がある | spec.md と front-end-need.md のどちらかに統一する。Next.js 15 採用の場合、spec.md を更新。14 のままの場合、front-end-need.md を修正 |
| H-02 | **High** | tech-lead, architect | 技術スタック整合性 | §1.2 / §6.1 | **状態管理ライブラリの矛盾**: front-end-need.md は「Zustand / Jotai」、spec.md は「Redux Toolkit / Zustand」。Jotai と Redux Toolkit は設計思想が大きく異なり、コンポーネント構成図（Mermaid）では `Redux Store` への依存が描画されている | 状態管理ライブラリを 1 つに確定する。Redux Toolkit を廃止して Zustand に統一するなら spec.md のコンポーネント構成図も更新 |
| H-03 | **High** | tech-lead, architect | 技術スタック整合性 | §3 管理画面 | **管理画面の技術スタック矛盾**: spec.md §技術スタック → 管理画面 は「ASP.NET Core + Razor Pages + Bootstrap 5 + htmx + Alpine.js」。front-end-need.md は管理画面も Next.js ベースの SPA として記述（§3.1 で `/admin/*` ルーティング）。根本的に異なるアーキテクチャ | どちらのアプローチを採用するか決定し、両文書を統一する。管理画面を Razor Pages とするなら front-end-need.md §3 を「管理画面仕様は `spec.md` の Razor Pages セクションを参照」に変更 |
| H-04 | **High** | performance-reviewer | パフォーマンス | §8.1 パフォーマンス | **Core Web Vitals 指標の陳腐化**: front-end-need.md は「FID (First Input Delay) < 100 ms」を記載しているが、Google は 2024年3月に FID を **INP (Interaction to Next Paint)** に置換済み。spec.md §Core Web Vitals 目標値 は正しく INP ≤ 200ms を記載している | §8.1 の FID を INP に置換し、目標値を spec.md と整合させる（INP ≤ 200ms） |
| H-05 | **High** | compliance-reviewer, security-reviewer | 法規制・セキュリティ | 文書全体 | **ゲスト購入フロー UI の未記載**: spec.md §ゲスト購入フロー（L1600〜）で詳細に定義された「ゲスト購入」画面フロー（`/checkout/guest` パス、越境データ移転同意チェックボックス、メールアドレス 2 回入力一致検証、プライバシーポリシー同意チェックボックス）が front-end-need.md に一切記載されていない。§2.1 画面一覧にも `/checkout/guest` が存在しない | ゲスト購入フローに対応する画面定義を §2.2 または専用セクションとして追加。spec.md の「ゲスト用配送先・メールアドレス入力フォーム」要件を反映 |
| H-06 | **High** | compliance-reviewer | 法規制 | 文書全体 | **越境データ移転同意 UI 要件の未記載**: spec.md で定義された SendGrid 越境移転同意チェックボックス（オプトイン方式）、プライバシーポリシーリンク、個人情報利用目的通知テキスト（個人情報保護法 第21条準拠）がフロントエンド要件として記載されていない | チェックアウト画面の UI 要素に越境データ移転同意チェックボックスとプライバシーポリシーリンクを追加 |
| H-07 | **High** | compliance-reviewer, security-reviewer | 法規制 | 文書全体 | **特定商取引法表示ページの未記載**: spec.md §特定商取引法対応 で `/legal/tokushoho` ページの実装が必須とされ、フッターからのリンクと注文確定前チェックボックスが定義されているが、front-end-need.md の §2.1 画面一覧に含まれていない | 画面一覧に `/legal/tokushoho` を追加し、フッターコンポーネントにリンクを含める要件を記載 |
| H-08 | **High** | business-analyst | ビジネス要件 | §2.2 チェックアウト | **チェックアウトフローのステップ不一致**: spec.md §チェックアウト・ステップインジケーター設計 では「5ステップ」（カート確認→配送先入力→お支払い→注文確認→注文完了）を定義し、URL パスも `/checkout/cart`, `/checkout/shipping`, `/checkout/payment`, `/checkout/confirm`, `/checkout/complete` と明記。一方 front-end-need.md §画面5 では単一の `/checkout` パスのみで、5ステップの分離が記述されていない | spec.md の5ステップフローに合わせ、front-end-need.md のチェックアウト画面を5つのサブ画面に分割して記述する |
| H-09 | **High** | business-analyst | ビジネス要件 | §2.2 チェックアウト | **Stripe Hosted Payment Page へのリダイレクトフロー未記載**: ADR-0008（PCI DSS 非保持化方針）で採用された Stripe Checkout（Hosted Payment Page）により、決済は外部ページにリダイレクトされる。spec.md のチェックアウト受入基準にも「Stripe Checkout にリダイレクトされ」と明記されているが、front-end-need.md では `POST /payments` を直接呼び出すフローのみ記載。Hosted Payment Page へのリダイレクトとコールバック（成功 URL / キャンセル URL）の画面遷移が未定義 | チェックアウト画面の API フロー に Stripe Checkout Session 作成→リダイレクト→コールバック URL への復帰フローを追加 |
| H-10 | **High** | architect | アーキテクチャ | §9 API エンドポイント | **Wishlist / お気に入り API の不一致**: front-end-need.md は `/users/me/favorites` パスでお気に入り機能を定義しているが、spec.md §ウィッシュリスト機能 では `/wishlists` パスで Wishlist API を定義（UserManagementService が提供）。パス・リソース名・機能範囲（在庫復活通知等）が異なる | spec.md の Wishlist 設計に合わせ、API パスを `/wishlists` に修正し、在庫復活通知機能の UI（`notifyOnRestock` トグル）を追加 |
| H-11 | **High** | security-reviewer | セキュリティ | §4.4 API 通信設定 | **JWT トークン取得方法の矛盾**: §4.4 の API クライアント設定コード例で `const token = getAccessToken()` として Authorization ヘッダーに Bearer トークンを付与しているが、§5.3 JWT トークン仕様では「保存場所: httpOnly Cookie（SameSite=Strict, Secure）」と明記。httpOnly Cookie に保存されたトークンは JavaScript から `getAccessToken()` でアクセス不可能。AGENTS.md §10.2 でも httpOnly Cookie を指定 | httpOnly Cookie 方式の場合、Authorization ヘッダーではなく Cookie による自動送信を使用する。API クライアントのコード例を `withCredentials: true` 方式に修正し、`getAccessToken()` の記述を削除 |
| H-12 | **High** | performance-reviewer | パフォーマンス | §8.1 パフォーマンス | **TTFB 目標値の記載があるが spec.md に未定義**: front-end-need.md は TTFB < 600ms を目標としているが、spec.md の Core Web Vitals 目標値には TTFB が含まれていない。SSOT（Single Source of Truth）の観点から、パフォーマンス目標の不整合が発生している | spec.md の Core Web Vitals 目標値に TTFB を追加するか、front-end-need.md の TTFB 目標を spec.md からの派生として位置づけを明記 |
| H-13 | **High** | qa-manager | テスト | 文書全体 | **フロントエンドテスト戦略の未定義**: テストフレームワーク（Vitest vs Jest、Playwright vs Cypress）が spec.md と不一致であるだけでなく、front-end-need.md にテスト戦略セクション自体が存在しない。カバレッジ目標、テスト種別（Unit/Integration/E2E）、アクセシビリティ自動テスト（axe-core 等）の記載が欠如 | テスト戦略セクションを追加し、テストフレームワーク・カバレッジ目標・WCAG 自動テスト（CI での axe-core / Lighthouse CI）を定義 |
| H-14 | **High** | oss-reviewer | 依存関係 | §1.2 / §4.4 | **API クライアントライブラリの不一致**: §1.2 では「TanStack Query v5」を推奨し、§4.4 のコード例では `axios.create()` を使用。spec.md は「Axios, React Query」。TanStack Query v5 は内部的に `fetch` API を使用し、axios と併用する公式パターンは非推奨。Axios 使用の要否を明確化すべき | TanStack Query + fetch API に統一するか、TanStack Query + axios アダプタに統一するか決定し、コード例を修正 |
| H-15 | **High** | ux-accessibility-reviewer | アクセシビリティ | §8.2 / 文書全体 | **WCAG 2.1 AA 要件が §8.2 で簡略化されすぎている**: spec.md では WCAG 2.1 AA の各基準（4.1.3 ステータスメッセージ、1.4.13 ホバー/フォーカスコンテンツ、SC 2.4.1 スキップリンク、SC 2.3.1 アニメーション制御等）が詳細に定義されているが、front-end-need.md §8.2 は 4 行の箇条書きのみ。spec.md のアクセシビリティ設計を front-end-need.md に反映すべき | spec.md §アクセシビリティ（WCAG 2.1 AA 準拠）の全項目を front-end-need.md に反映するか、「spec.md §アクセシビリティを参照」と明記してクロスリファレンスを設定 |
| H-16 | **High** | security-reviewer | セキュリティ | §4.4 API 通信設定 | **Correlation ID 生成のセキュリティリスク**: §4.4 のコード例で `crypto.randomUUID()` をフロントエンドで生成し `X-Correlation-Id` ヘッダーに付与しているが、spec.md §Correlation ID ではバックエンド（ミドルウェア）が Correlation ID を生成する設計。クライアント生成の Correlation ID をそのまま信頼すると、ログ汚染やトレース追跡の困難を招く | Correlation ID の生成をバックエンド（API Gateway ミドルウェア）に委譲し、フロントエンドからは送信しない設計に修正。レスポンスの `X-Correlation-Id` をログ表示用に使用する |
| H-17 | **High** | architect | 通信設計 | §2.2 画面5, §9.5 | **注文確定 API フローと Saga パターンの未反映**: ADR-0009 で注文確定は Saga オーケストレーションパターン（5ステップ: 在庫引当→決済→注文登録→ポイント付与→配送手配）が採用されているが、front-end-need.md のチェックアウト API フローは `POST /orders` → `POST /payments` の単純な逐次呼出しのみで、Saga の非同期性（処理中ステータス、ポーリング/WebSocket による完了通知）を考慮していない | 注文確定後の非同期処理フロー（`PENDING` → `CONFIRMED` へのステータス遷移待ち）の画面 UX を定義する。ローディング画面 or ポーリング or Server-Sent Events での完了通知 UI を追加 |
| H-18 | **High** | business-analyst | ビジネス要件 | §7.1 | **i18n ライブラリが未指定**: spec.md では `next-intl` が明記され、翻訳キー命名規則（`{domain}.{context}.{key}` 形式）も定義されているが、front-end-need.md §7 は対応言語とタイムゾーン・通貨のみで、i18n ライブラリ名・翻訳キー管理方式・フォールバック戦略が未記載 | §7 に i18n ライブラリ（`next-intl`）、翻訳キー命名規則、フォールバック言語（`ja` → `en`）を追加 |

---

## Medium/Low 指摘一覧（改善推奨）

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|--------------|----------|----------|
| M-01 | Medium | architect | 整合性 | §1.2 | UI コンポーネントが「shadcn/ui」、spec.md は「Headless UI」。shadcn/ui は Radix Primitives ベース、Headless UI は Tailwind Labs 製。設計方針が異なる | どちらかに統一 |
| M-02 | Medium | architect | 整合性 | §1.2 | テストフレームワークが「Vitest + Playwright」、spec.md は「Jest + Cypress」。CI 設定・テスト資産に影響 | テスト戦略セクション追加時に統一 |
| M-03 | Medium | programing-reviewer | コード品質 | §4.4 | API クライアントのコード例で `console.error()` を使用（L410）。本番環境では構造化ロギング（Application Insights SDK 等）を使用すべき | `console.error` を Application Insights / 集約ログに置換する旨を記載 |
| M-04 | Medium | programing-reviewer | コード品質 | §6.3 | 楽観的更新パターンのコード例で型定義が不完全（`Cart` 型の定義なし）。実装時の参考として不十分 | 主要な型定義（`Cart`, `CartItem`, `Product` 等）のインターフェースを補足 |
| M-05 | Medium | dba-reviewer | データアクセス | §6.2 | サーバー状態のキャッシュ戦略で「カート内容: staleTime = 0（必ず最新）」としているが、spec.md のカートサービス設計では Redis Write-Through キャッシュが実装されている。フロントエンドの staleTime=0 だと毎回 API 呼出しが発生し、Redis キャッシュの効果が薄れる | カートの staleTime を 10〜30 秒程度に緩和し、楽観的更新で整合性を担保する設計に変更 |
| M-06 | Medium | security-reviewer | セキュリティ | §2.2 画面9 | ログイン画面のセキュリティ要件でアカウントロック（5回失敗で15分ロック）は記載あるが、ロックされたアカウントを示す UI（残り試行回数の表示はセキュリティリスク）と、ロック後の回復フロー（パスワードリセット誘導）の画面フローが未定義 | ロック時のエラーメッセージ（「一定時間お待ちください」、パスワードリセットリンク提示）を画面要件に追加 |
| M-07 | Medium | security-reviewer | セキュリティ | §2.2 画面10 | ユーザー登録のパスワードバリデーション「大文字/小文字/数字/記号を含む」は記載あるが、一般的な漏洩パスワードリスト（HIBP API 等）との照合要件が未記載 | パスワード強度チェックの拡張（漏洩パスワード照合 or 表示警告）を検討 |
| M-08 | Medium | audit-reviewer | トレーサビリティ | §4.1 | RFC 9457 エラーレスポンスの `traceId` フィールドをフロントエンドでログ出力する方針は記載されているが、ユーザー操作ログ（ボタンクリック、ページ遷移等）の Application Insights 連携が未定義 | ユーザー操作テレメトリ（Application Insights SDK / web-vitals）の設計セクションを追加 |
| M-09 | Medium | audit-reviewer | ドキュメント管理 | 文書全体 | 改訂履歴が存在しない。spec.md には改訂履歴テーブルと運用ルールが定義されているが、front-end-need.md はバージョン管理されていない | spec.md と同様の改訂履歴セクションを追加 |
| M-10 | Medium | performance-reviewer | パフォーマンス | §8.1 | バンドルサイズ目標「< 200 KB (初期ロード)」は gzip 圧縮時の値だが、Next.js 15 の App Router + React Server Components 使用時のバンドル構成が考慮されていない | App Router の RSC (React Server Components) によるクライアントバンドル削減効果を考慮した目標値の再検討 |
| M-11 | Medium | business-analyst | ビジネス要件 | §2.2 | 商品一覧のページネーション方式が「無限スクロール or ページ番号」と未確定。SEO（SSR ではページ番号が必要）とモバイル UX（無限スクロールが好まれる）のトレードオフが検討されていない | デスクトップ: ページ番号、モバイル: 無限スクロール のハイブリッド方式を検討 |
| M-12 | Medium | business-analyst | ビジネス要件 | §2.1 | 画面一覧に「パスワードリセット」画面が存在しない。ログイン画面に「パスワードを忘れた場合」リンクは記載あるが、遷移先画面（`/auth/password/reset-request`, `/auth/password/reset`）が §2.1 に未定義 | パスワードリセット要求画面と新パスワード入力画面を §2.1 に追加 |
| M-13 | Medium | ux-accessibility-reviewer | アクセシビリティ | §2.2 画面5 | チェックアウト画面に spec.md で定義された「ステップインジケーター」の `aria-current="step"` および `aria-label` 仕様が front-end-need.md に反映されていない | spec.md のステップインジケーター設計（アクセシビリティ属性含む）をチェックアウト画面仕様に統合 |
| M-14 | Medium | compliance-reviewer | 法規制 | 文書全体 | GDPR DSR（データ主体の権利行使）フォームの UI 要件が未記載。spec.md §ゲスト購入者 DSR ワークフロー で `/dsr/guest` フォームが定義されているが、画面一覧に含まれていない | §2.1 に DSR フォーム画面を追加するか、「法的ページ」セクションとして分離して定義 |
| M-15 | Medium | infra-ops-reviewer | 運用 | §4.3 | グレースフルデグラデーション戦略は網羅的だが、SalesManagementService 障害時のフォールバック（注文履歴がキャッシュなしで表示不可）が未記載 | SalesManagementService 障害時のフォールバック（「注文履歴を現在取得できません」メッセージ）を追加 |
| M-16 | Medium | release-manager | リリース | §11 MoSCoW | MoSCoW 分類で「SEO 最適化」が Should Have だが、spec.md では商品一覧・詳細の SSR/SSG が技術選定の根幹（Next.js 採用理由）。SEO を Phase 1 に含めないと構造化データ・メタタグの実装が後回しになるリスク | SEO の基本実装（メタタグ、OGP、sitemap.xml）を Must Have に昇格、高度な SEO（構造化データ JSON-LD）を Should に維持 |
| M-17 | Medium | ux-accessibility-reviewer | UX | §2.2 画面1 | トップページの構成要素に「検索バー」が明記されていない。spec.md §検索サジェスト UX（H9-48）でオートコンプリート機能が定義されているが、front-end-need.md のトップページ/ヘッダーコンポーネントに検索バーの配置要件が未記載 | ヘッダーコンポーネントに検索バー（オートコンプリート付き）の配置要件を追加 |
| L-01 | Low | programing-reviewer | コード品質 | §4.4 | API クライアント設定で `process.env.NEXT_PUBLIC_API_BASE_URL` を使用しているが、App Router 環境では Server Actions や Route Handlers も考慮すべき。サーバーサイドでの API 呼出しでは `NEXT_PUBLIC_` プレフィックスは不要 | Server/Client コンポーネントでの API 呼出しパターンの違いを補足 |
| L-02 | Low | ux-accessibility-reviewer | UX | §2.2 画面3 | 在庫表示ルールで「緑」「オレンジ」「赤」のカラーコードが定義されているが、色覚多様性への対応（テキストラベル・アイコン併用）には §2.2 画面3 で言及なし。spec.md のエラーメッセージ UX ガイドラインでは「色非依存の情報伝達」が原則 | 在庫表示にテキストラベル（在庫あり/残りわずか/在庫切れ）を必須とする記載を追加 |
| L-03 | Low | business-analyst | ビジネス要件 | §2.1 | 画面一覧に NPS アンケートページ（`/nps?token={one-time-token}`）が含まれていない。spec.md §NPS 測定実装設計 で定義済み。Phase 2 スコープだが、画面一覧には含めるべき | Phase 2 画面として画面一覧の補足に追加 |
| L-04 | Low | infra-ops-reviewer | 運用 | §12 | OpenAPI Swagger UI の URL 一覧は有用だが、API Gateway の統合 Swagger（YARP 経由）の具体的な設定方法が不明 | YARP の Swagger 統合設定への参照リンクを追加 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-01 | 最優先 | tech-lead | **Next.js バージョンの確定**: 14 と 15 のどちらを正とするか。App Router の安定性、RSC サポート、パフォーマンスへの影響を考慮して決定が必要 | テックリード / アーキテクト |
| E-02 | 最優先 | tech-lead | **管理画面の技術スタック確定**: Next.js SPA か ASP.NET Core Razor Pages か。運用・保守体制（フロントエンドチーム vs バックエンドチーム）の分担に影響 | テックリード / PO |
| E-03 | 高優先 | compliance-reviewer | **ゲスト購入フローの越境データ移転同意 UI**: spec.md で詳細に定義済みだが、front-end-need.md への反映方針（直接記載 vs クロスリファレンス）を決定 | PO / 法務 |
| E-04 | 高優先 | security-reviewer | **JWT 保存・送信方式の確定**: httpOnly Cookie 方式を最終確定し、API クライアントの通信設計（CORS + withCredentials）を front-end-need.md に反映 | セキュリティリード |
| E-05 | 通常 | business-analyst | **ページネーション方式の確定**: 無限スクロール vs ページ番号 vs ハイブリッド。SEO 要件とモバイル UX のトレードオフ | フロントエンドリード / UX デザイナー |
| E-06 | 通常 | release-manager | **SEO 基本実装の Phase 1 昇格判断**: 現在 Should Have の SEO を Must Have に変更するか | PO |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| C-01 | performance-reviewer（staleTime=0 は過剰） | security-reviewer（カート整合性を重視） | カートデータの staleTime: パフォーマンス vs 整合性 | staleTime=10s + 楽観的更新を推奨 | 楽観的更新パターン（§6.3 で既に定義済み）を活用すれば、staleTime を緩和しても UX 上の整合性は維持可能。サーバーサイドの Redis Write-Through キャッシュとの二重取得を回避 |

---

## ドキュメント横断分析

### サービス間 API 整合性

| front-end-need.md のパス | spec.md / 設計書のパス | 整合性 | 備考 |
|-------------------------|---------------------|--------|------|
| `/users/me/favorites` | `/wishlists/{id}/items` | ❌ | パス・リソース名が異なる |
| `/checkout` (単一) | `/checkout/cart`, `/checkout/shipping`, etc. (5分割) | ❌ | チェックアウトステップ構成の不一致 |
| `POST /payments` (直接呼出) | Stripe Checkout Session → リダイレクト | ❌ | ADR-0008 の Hosted Payment Page 方式と不一致 |
| `POST /orders` → `POST /payments` (逐次) | Saga オーケストレーション (非同期) | ❌ | ADR-0009 の Saga パターンと不一致 |
| `/coupons/validate` | `/coupons/validate` | ✅ | |
| `/points/balance` | `/points/balance` | ✅ | |
| `/auth/login` | `/auth/login` | ✅ | |
| `/cart/items` | `/cart/items` | ✅ | |
| `/products`, `/products/{id}` | `/products`, `/products/{id}` | ✅ | |

### 未定義・曖昧な領域

| # | 未定義事項 | 影響度 | ブロッカー可能性 |
|---|----------|--------|---------------|
| 1 | ゲスト購入フローの全画面定義 | 高 | ⚠️ 実装ブロッカー |
| 2 | Stripe Checkout リダイレクトの画面遷移 | 高 | ⚠️ 実装ブロッカー |
| 3 | Saga 処理中のローディング UI | 中 | 設計判断が必要 |
| 4 | 管理画面の技術スタック（Razor vs Next.js） | 高 | ⚠️ 実装ブロッカー |
| 5 | i18n ライブラリと翻訳キー管理方式 | 中 | 設計判断が必要 |
| 6 | パスワードリセットフロー画面 | 低 | Phase 遅延リスク |
| 7 | DSR（データ主体の権利行使）フォーム画面 | 中 | 法規制コンプライアンスリスク |
| 8 | 特定商取引法表示ページ | 中 | 法規制コンプライアンスリスク |

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### 評価: ⚠️ Conditional — High 2件, Medium 3件, Low 1件

**レビュー観点**: ビジネス要件の完全性、ユーザーストーリー整合性、EC サイト機能要件カバレッジ

**肯定的評価**:
- 画面一覧マトリクス（§2.1）は MoSCoW 優先度付きで網羅的
- カート仕様（ゲスト/ログイン時の挙動）が明確
- 在庫表示ルール（10以上/1〜9/0）が具体的
- 管理画面の機能一覧が充実（15画面）
- リスクと対策セクション（§13）が実用的

**指摘**: H-08, H-09, H-18, M-11, M-12, L-03 参照
</details>

<details>
<summary>architect レビューレポート</summary>

### 評価: ⚠️ Conditional — High 3件, Medium 2件

**レビュー観点**: アーキテクチャ整合性、Bounded Context 反映、サービス間通信

**肯定的評価**:
- バックエンドサービス一覧（§1.3）がAGENTS.md と整合
- API Gateway 経由パスの設計が妥当
- RFC 9457 エラーハンドリングの TypeScript 型定義が実装可能

**指摘**: H-01, H-02, H-03, H-10, H-17, M-01, M-02 参照

**追加所見**: コンポーネント構成図（Mermaid）内の `Redux Store` への依存は Zustand 採用時に修正が必要。`STATE[状態管理] --> ZUSTAND[Zustand Store]` に置換すべき。
</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional — High 1件, Medium 2件, Low 1件

**レビュー観点**: コード例の正確性、TypeScript パターンの適切性

**肯定的評価**:
- TypeScript のエラーハンドリングコード例が具体的で実装可能
- 楽観的更新パターン（TanStack Query）のコード例が正確
- ページネーションの TanStack Query 実装例が `keepPreviousData` を適切に使用

**指摘**: H-14, M-03, M-04, L-01 参照
</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 評価: Pass — Medium 1件

**レビュー観点**: データアクセスパターン、キャッシュ戦略の妥当性

**肯定的評価**:
- ページネーション仕様が ASP.NET Core の規約に準拠
- サーバー状態のキャッシュ戦略テーブルが実用的

**指摘**: M-05 参照
</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional — High 2件, Medium 2件

**レビュー観点**: OWASP Top 10、認証/認可設計、秘密情報管理

**肯定的評価**:
- ログイン画面のセキュリティ要件（アカウントロック、レート制限、CSRF）が具体的
- JWT トークン仕様が RS256 + httpOnly Cookie で安全
- Cookie 設定（HttpOnly, Secure, SameSite=Strict）が適切
- エラーメッセージで「メールアドレスまたはパスワードが正しくありません」（ユーザー列挙攻撃防止）が適切

**指摘**: H-11, H-16, M-06, M-07 参照

**追加所見**: §4.4 のレスポンスインターセプターでトークンリフレッシュのリトライロジックが実装されているが、同時に複数リクエストが 401 を受けた場合のトークンリフレッシュの多重実行防止（mutex / queue）が未考慮。
</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional — High 2件, Medium 1件

**レビュー観点**: GDPR、個人情報保護法、PCI DSS、データガバナンス

**肯定的評価**:
- JWT の httpOnly Cookie 保存が PII 保護に有効
- エラーメッセージに内部情報を含めない方針が明確

**指摘**: H-06, H-07, M-14 参照

**追加所見**: Cookie 同意バナー（GDPR ePrivacy Directive 準拠）の UI 要件が front-end-need.md に一切記載されていない。EU ユーザーへのサービス提供を想定する場合、Cookie 同意管理の画面設計が必要。
</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 評価: Pass — Medium 2件

**レビュー観点**: トレーサビリティ、ドキュメント整合性

**肯定的評価**:
- RFC 9457 の Problem Details に `traceId` を含める設計でトレーサビリティが確保される
- Correlation ID の伝搬設計が記載されている

**指摘**: M-08, M-09 参照
</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 評価: ⚠️ Conditional — High 1件, Medium 1件

**レビュー観点**: テスト戦略、カバレッジ目標、E2E テスト設計

**肯定的評価**:
- §1.2 にテストフレームワークの候補が記載されている

**指摘**: H-13 参照

**追加所見**: spec.md §テスト規約 の「分岐カバレッジ 80% 以上」がフロントエンドにも適用されるかが不明。フロントエンド固有のテスト目標（コンポーネントテストカバレッジ、E2E シナリオカバレッジ、アクセシビリティ自動テスト通過率）を定義すべき。
</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional — High 2件, Medium 1件

**レビュー観点**: パフォーマンス SLA、Core Web Vitals、キャッシュ戦略

**肯定的評価**:
- LCP ≤ 2.5s, CLS < 0.1 の目標値が spec.md と整合
- ヒーロー画像の `next/image` 最適化、遅延読み込みの方針が適切
- バンドルサイズ目標（< 200 KB gzip）が設定されている

**指摘**: H-04, H-12, M-10 参照

**追加所見**: ISR (Incremental Static Regeneration) の `revalidate` 間隔が未定義。商品詳細ページのISR 間隔は在庫変動頻度に依存し、パフォーマンスと鮮度のトレードオフが存在する。
</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 評価: Pass — Medium 1件, Low 1件

**レビュー観点**: コンテナ設計、可観測性、ヘルスチェック、グレースフルデグラデーション

**肯定的評価**:
- §4.3 のグレースフルデグラデーション戦略が全サービスを網羅
- API Gateway 経由での統一アクセスポイントが適切

**指摘**: M-15, L-04 参照
</details>

<details>
<summary>release-manager レビューレポート</summary>

### 評価: Pass — Medium 1件

**レビュー観点**: リリース戦略、MoSCoW 優先度、Phase 計画

**肯定的評価**:
- MoSCoW 分類（§11）が整理されている
- Won't Have（スコープ外）が明確

**指摘**: M-16 参照
</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional — High 1件

**レビュー観点**: 依存ライブラリの整合性、ライセンス

**肯定的評価**:
- 推奨ライブラリが全て OSS で主要な問題なし

**指摘**: H-14 参照

**追加所見**: shadcn/ui は MIT ライセンスで問題なし。Radix Primitives（MIT）との組み合わせもライセンス上の懸念なし。
</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional — High 1件, Medium 2件, Low 1件

**レビュー観点**: UX 設計品質、WCAG 2.1 AA 準拠、レスポンシブ設計

**肯定的評価**:
- レスポンシブブレークポイント（§4.5）が Tailwind CSS 標準と整合
- 商品詳細の在庫表示ルールがユーザーフレンドリー
- 画面遷移図（Mermaid）が分かりやすい

**指摘**: H-15, M-13, M-17, L-02 参照

**追加所見**: spec.md で定義されている「カート放棄メール」のディープリンク（カートに戻る URL）がフロントエンドのルーティング設計に影響するが、front-end-need.md に未反映。
</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 評価: ⚠️ Conditional — High 3件, Medium 2件

**レビュー観点**: 技術標準の横断適合性、実装実現可能性、規約遵守

**肯定的評価**:
- ドキュメント全体の構成が論理的（画面一覧→詳細→共通コンポーネント→認証→状態管理→API 一覧→画面遷移）
- API エンドポイント一覧（§9）が全サービスを網羅
- エスカレーション項目（§14）で未確定事項が明示されている
- エラーハンドリングの Problem Details パースロジックが実装品質

**指摘**: H-01, H-02, H-03 参照

**総合所見**: front-end-need.md は「バックエンド API との連携仕様」に重心を置いた良質なドキュメントだが、spec.md との**技術スタック・チェックアウトフロー・法規制対応の同期**が不十分。特に Next.js バージョン、管理画面の技術スタック、チェックアウト5ステップフロー、Stripe Hosted Payment Page リダイレクト、ゲスト購入フローの5点は実装着手前に必ず解決すべき。これらは実装アーキテクチャに直結するため、曖昧なまま実装を開始するとフル手戻りリスクがある。
</details>

---

## 次のアクション

1. **即座に対応**: E-01〜E-04 のエスカレーション事項を人間が判断
2. **H-01〜H-03 解決後**: 技術スタックの統一（spec.md と front-end-need.md の同期更新）
3. **H-05〜H-09 解決後**: ゲスト購入フロー・チェックアウト5ステップ・Stripe リダイレクトの画面定義追加
4. **H-11, H-16 解決後**: API 通信設計のセキュリティ修正（httpOnly Cookie 方式への統一）
5. **修正完了後**: 再レビューを実施
