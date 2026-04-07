# ソースコードレビュー統合レポート — Frontend + AdminPortal + Frontend.Tests

## 判定結果

- **対象**: Services/Frontend/, Services/AdminPortal/, Services/Frontend.Tests/
- **判定**: ❌ **Rejected** — Critical 指摘 12 件検出
- **レビュー日時**: 2026-04-07 21:13
- **プロジェクト**: SkiShop (.NET 10 マイクロサービス EC サイト)

---

## 指摘サマリー

| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|:--------:|:----:|:------:|:---:|
| tech-lead | ⚠️ | 2 | 8 | 10 | 5 |
| architecture-reviewer (Frontend) | ⚠️ | 0 | 5 | 7 | 3 |
| architecture-reviewer (AdminPortal) | ❌ | 2 | 6 | 7 | 3 |
| ddd-domain-reviewer | ⚠️ | 0 | 7 | 7 | 3 |
| api-endpoint-reviewer | ⚠️ | 3 | 9 | 10 | 5 |
| csharp-standards-reviewer | ⚠️ | 0 | 2 | 7 | 3 |
| async-concurrency-reviewer | ⚠️ | 2 | 5 | 3 | 2 |
| error-logging-reviewer | ⚠️ | 2 | 8 | 6 | 3 |
| data-access-reviewer | ⚠️ | 1 | 5 | 6 | 2 |
| config-di-reviewer | ❌ | 0 | 9 | 8 | 3 |
| security-reviewer | ⚠️ | 1 | 7 | 8 | 2 |
| dependency-reviewer | ⚠️ | 0 | 1 | 4 | 2 |
| test-quality-reviewer | ❌ | 12 | 16 | 11 | 3 |
| performance-reviewer | ⚠️ | 2 | 5 | 5 | 3 |
| resilience-reviewer | ⚠️ | 1 | 7 | 6 | 2 |
| **統合後（重複排除済）** | ❌ | **12** | **32** | **28** | **12** |

## 判定根拠

- **判定ルール適用結果**: Critical 指摘 12 件 → 自動的に ❌ Rejected
- **最も重大な指摘**: AdminPortal → API Gateway への HTTP リクエストに認証トークンが付与されておらず、管理操作が API Gateway 側で認証・認可されない可能性（security/architecture/tech-lead 共通検出）

---

## Critical 指摘一覧（修正必須）— 12 件

| # | 出典 Agent | カテゴリ | 対象ファイル | 指摘内容 |
|---|-----------|---------|------------|----------|
| C-01 | security, arch-admin, tech-lead | 認証/BFF | `AdminPortal/Login.cshtml.cs` | **AdminPortal → API Gateway JWT 未転送**: ログインで取得した JWT が保存されず、後続 API 呼出しに Bearer ヘッダーが付与されない。管理操作が認可バイパスされるリスク |
| C-02 | arch-admin, security | CDN/SRI | `AdminPortal/_Layout.cshtml` L109 | **htmx が SRI なしで unpkg CDN から読み込み**: 管理画面への供給チェーン攻撃リスク。Bootstrap Icons (L10-12) も SRI 欠落 |
| C-03 | data-access, performance | キャッシュ | `Frontend/Services/ProductApiClient.cs` L27 | **キャッシュキーにフィルタ条件不足**: `category`, `sort`, `minPrice`, `maxPrice` がキーに含まれず、異なる検索条件で誤ったキャッシュデータが返される |
| C-04 | error-logging, tech-lead | 例外処理 | `Frontend/Components/Pages/Auth/Login.razor` L183-185, L218-220 | **`catch (Exception)` でログ出力なし**: 認証障害の痕跡がサーバーに残らず、インシデント原因特定が不可能 |
| C-05 | error-logging | 例外処理 | `Frontend/Components/Pages/Auth/Register.razor` L139-143 | **ユーザー登録エラーのログ未出力**: `ex.Message` の文字列比較のみでスタックトレースが記録されない |
| C-06 | api-endpoint | バリデーション | `AdminPortal/Pages/Admin/Products/Create.cshtml.cs` L49-101 | **AdminPortal 全 BindProperty に Data Annotations なし**: `[Required]`, `[StringLength]`, `[Range]` 等が一切なく、手動の null チェックのみ。全 PageModel 共通の問題 |
| C-07 | api-endpoint | ログ | `AdminPortal/Products/Create.cshtml.cs` L88-90 | **API エラーレスポンスの生ボディをログ出力**: スタックトレースや SQL エラー等の内部情報漏洩リスク |
| C-08 | async-concurrency | CancellationToken | Blazor コンポーネント 7 ファイル | **OnInitializedAsync で CancellationToken 未使用**: サーキット切断後も最大 5 API 呼出しが完走。`IAsyncDisposable` + CTS パターンが必要 |
| C-09 | performance | N+1 | `Orders/Index.razor` L140-152, `Orders/Detail.razor` L405-416 | **ReorderAsync で N+1 API 呼出し**: ループ内で `GetProductByIdAsync` × N + `AddItemAsync` × N。同一コードが 2 箇所に重複 |
| C-10 | tech-lead | 禁止事項 | `AiChatWidget.razor` L283-287 | **空 catch ブロック**: `catch { // スクロール失敗は無視 }` は AGENTS.md §4.2 禁止事項違反 |
| C-11 | api-endpoint | バリデーション | `AdminPortal/Login.cshtml.cs` L29-31 | **ログインフォームに Data Annotations なし**: Email 形式検証なしで不正入力がバックエンドに送信される |
| C-12 | performance | 実装不備 | `Checkout/Payment.razor` L135 | **ポイント残高 `_pointBalance = 5000` ハードコード**: 設計書ではPointAPI取得を規定。全ユーザーに誤った残高表示 |

---

## High 指摘一覧（修正強く推奨）— 主要 32 件（上位抜粋）

| # | 出典 Agent | カテゴリ | 対象ファイル | 指摘内容 |
|---|-----------|---------|------------|----------|
| H-01 | config-di, security | セキュリティ | `AdminPortal/Program.cs` L79-86 | CSP ヘッダー未設定。Frontend は SecurityHeadersMiddleware で CSP 設定済み |
| H-02 | config-di | ミドルウェア | 両 Program.cs | `UseExceptionHandler()` が Development 環境で無効。受信側 Correlation ID ミドルウェア未実装 |
| H-03 | config-di | ミドルウェア | 両 Program.cs | `UseRateLimiter()` 未実装。ログイン・AI チャットへのブルートフォース防止なし |
| H-04 | config-di | IOptions | 両 Program.cs | `builder.Configuration["ApiGateway:BaseUrl"]` の直接アクセス。IOptions&lt;T&gt; パターン未使用 |
| H-05 | security | トークン管理 | `TokenRefreshHandler.cs` L34-39 | リフレッシュ成功後に新トークンが Cookie に保存されない |
| H-06 | security | 並行制御 | `TokenRefreshHandler.cs` L14 | `SemaphoreSlim` が Transient スコープで無効化。`static` または Singleton 必要 |
| H-07 | async-concurrency | Fire-and-Forget | `SearchSuggest.razor` L163 | `Task.Run` + Fire-and-Forget で例外消失・レンダリング不整合 |
| H-08 | async-concurrency | リソースリーク | `SearchSuggest.razor` L86-87 | `CancellationTokenSource` 未 Dispose。`IDisposable` 未実装 |
| H-09 | async-concurrency | CancellationToken | SignalR 3 コンポーネント | `HubConnection.StartAsync` / `InvokeAsync` に CT 未伝搬 |
| H-10 | error-logging | 例外処理 | AdminPortal 5 ページ | POST 系ハンドラーに try-catch なし（Users, Coupons, Points, Campaigns, Mails） |
| H-11 | error-logging | OpenTelemetry | `AdminPortal/Program.cs` | OpenTelemetry 設定が一切ない。分散トレーシング途切れ |
| H-12 | arch-frontend | レイヤー | `CartEffects.cs` | `IApiGatewayClient` を直接注入し API パスも不整合 (`/cart/items` vs `/api/v1/cart`) |
| H-13 | arch-frontend | DTO | 全 ApiClient | DTO が各 ApiClient ファイル末尾に同居。`DTOs/` 未分離 |
| H-14 | arch-frontend | 関心事分離 | `ApiErrorHandler.cs` | Service 層が `MudBlazor.ISnackbar`（UI）に依存 |
| H-15 | csharp-standards | DI | 全 Services | 14 サービスクラスにインターフェース未定義。テスタビリティ低下 |
| H-16 | data-access | キャッシュ | `CacheService.cs` L55-60 | `InvalidateByPrefix` が No-Op 実装。Stampede 問題もあり |
| H-17 | data-access | トークン | `TokenRefreshHandler.cs` L34-39 | リフレッシュ後の新トークン保存欠落（security #5 と重複） |
| H-18 | resilience | ヘルスチェック | `AdminPortal/Program.cs` L97 | `/health/ready` (Readiness) 未実装 |
| H-19 | resilience | サーキットブレーカー | 両 Program.cs | `AddStandardResilienceHandler` でサーキットブレーカーの明示パラメータ未設定 |
| H-20 | arch-admin | 設計書逸脱 | `_Layout.cshtml` | htmx ロードされているが全ページで未使用。Alpine.js 未導入。設計書規定技術スタック未活用 |
| H-21 | arch-admin | NuGet | `AdminPortal.csproj` | JwtBearer / FluentValidation / OpenTelemetry パッケージ参照あるが未使用 |
| H-22 | arch-admin | エラーページ | `Program.cs` L72 | `UseExceptionHandler("/Admin/Error")` 参照先の Error.cshtml 不在 |
| H-23 | api-endpoint | RFC 9457 | AdminPortal 全ページ | ProblemDetails パース基盤なし。エラー詳細がユーザーに表示されない |
| H-24 | api-endpoint | REST 規約 | Orders/Returns/Shipments | ステータス変更に PUT 使用（POST が適切） |
| H-25 | arch-admin | API パス | 複数ページ | `/admin/` と `/api/v1/` プレフィックスの混在 |
| H-26 | dependency | プロジェクト設定 | `Frontend.Tests.csproj` | `TreatWarningsAsErrors` 未設定 |
| H-27 | ddd | ドメインロジック | `CartState.cs` L16 | `TotalAmount` 金額再計算がフロントエンドに漏洩 |
| H-28 | ddd | レイヤー責務 | `Orders/Detail.cshtml.cs` L47-55 | Staff ロール制限のビジネスルールが BFF に漏洩 |
| H-29 | resilience | SignalR | `AiChatWidget.razor` | Reconnecting/Closed ハンドラ未設定 |
| H-30 | resilience | フォールバック | `AiApiClient.cs` L122-127 | `EscalateChatAsync` のみ try-catch なし |
| H-31 | performance | キャッシュ | `ProductApiClient.cs` L46-65 | NewArrivals/Popular/Categories がキャッシュバイパス |
| H-32 | api-endpoint | エラーハンドリング | `ApiGatewayClient.cs` L64-70 | `SendAsync` が `EnsureSuccessAsync` を呼び出さない |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| 1 | 🔴 最優先 | security, arch | AdminPortal → API Gateway JWT 転送アーキテクチャの方式決定（Cookie保存+DelegatingHandler / セッション / API Gateway 側認証） | アーキテクト + セキュリティ |
| 2 | 🔴 最優先 | security, async | TokenRefreshHandler の再設計（static SemaphoreSlim + トークン永続化） | テックリード |
| 3 | 🟡 高 | arch, tech-lead | レンダリングモード戦略の確定（全ページ InteractiveServer → 画面別使い分け時期） | プロダクトオーナー + テックリード |
| 4 | 🟡 高 | api-endpoint | AdminPortal API クライアント層新設の優先度判断 | テックリード |
| 5 | 🟡 高 | security | MFA ロックアウトのサーバーサイド実装（Auth Service 側確認） | セキュリティ + バックエンド |
| 6 | 🟡 高 | arch | DTO 共有戦略の決定（Shared プロジェクト導入時期） | テックリード |
| 7 | 🟡 高 | async | Fluxor Effects の CancellationToken 戦略（IDisposable / Middleware / コンポーネント側） | テックリード |
| 8 | 🟡 高 | performance | キャッシュ基盤の Redis 移行判断 | インフラ + テックリード |
| 9 | 🟢 通常 | arch, tech-lead | Fluxor Store の適用範囲（AuthStore/CartStore 以外も Store 化すべきか） | テックリード |
| 10 | 🟢 通常 | tech-lead | StockBadge 在庫表示段階数（設計書3段階 vs 実装4段階） | プロダクトオーナー |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| 1 | test-quality (Critical ×12) | tech-lead (条件付きPass) | Skip 付きテストの `true.ShouldBeTrue()` 偽アサーション | **Medium に降格** | Skip 注釈付きで CI 実行されない。ただし偽アサーションは削除すべき（テンプレート悪用リスク） |
| 2 | ddd (High: record化必須) | csharp-standards (Medium: 互換性懸念) | AdminPortal `[BindProperty]` の mutable class | **Medium** | Razor Pages の `[BindProperty]` は mutable プロパティを前提。`sealed class` + `init` 折衷案を推奨。完全な record 化は技術検証後に判断 |
| 3 | async (Critical: CT未伝搬) | tech-lead (条件付き) | Fluxor Effects の CancellationToken 未サポート | **High に降格** | Fluxor フレームワーク制約として `[EffectMethod]` は CT パラメータ非対応。IDisposable パターンでの CTS 管理を推奨するが、Critical ではなく High とする |
| 4 | ddd (High: Aggregate分離) | api-endpoint (許容) | AdminPortal PageModel の HttpClient 直接使用 | **許容（条件付き）** | BFF 的 Razor Pages にはビジネスロジックなく DDD 層構造の厳格適用は不要。ただし共通 API クライアントの導入は High で推奨 |

---

## 設計書との照合結果

### 設計書からの逸脱

| # | 設計書セクション | 設計書の規定 | 実装の実態 | 影響度 |
|---|---------------|-----------|----------|--------|
| 1 | §1.2 レンダリングモード | Static SSR / Interactive Server / WASM / Auto の画面別使い分け | 全ページ `InteractiveServer` | Medium — Phase制約 |
| 2 | §1.2 技術スタック | AdminPortal: htmx + Alpine.js | htmx ロードのみ未活用、Alpine.js 未導入 | High |
| 3 | §2.2 在庫表示 | 3段階（>10/1-9/0） | 4段階（LowStockThreshold=5） | Low — UX改善 |
| 4 | §1.2 DTO 共有 | 「DTO（record型）をプロジェクト参照で共有」 | Frontend独自定義、ApiClient内に同居 | Medium |
| 5 | §3.2 RBAC | 「全管理画面は Admin ロールが必要」 | Admin/Manager/Staff 3段階差別化 | Low — 実装が優位 |
| 6 | §1.3 API パス | 各サービスの API パス定義 | CartEffects: `/cart/items` vs CartApiClient: `/api/v1/cart/items` | High — 実行時404リスク |

### 未実装の設計要素

| # | 設計要素 | 状態 |
|---|---------|------|
| 1 | ThemeStore（ダーク/ライトモード切替） | ディレクトリ作成済み、ファイル空 |
| 2 | Interactive WebAssembly レンダリング | 未実装（Phase制約） |
| 3 | Interactive Auto レンダリング | 未実装（Phase制約） |
| 4 | 動的サイトマップ（商品URL生成） | 静的URLのみ |
| 5 | AdminPortal OpenTelemetry 構成 | パッケージ参照のみ、初期化コード未実装 |

---

## サービス品質比較

| 評価項目 | Frontend | AdminPortal | 差分 |
|---------|:--------:|:-----------:|------|
| BFF パターン | ⭐⭐⭐⭐ | ⭐⭐⭐ | AdminPortal に API クライアント抽象化なし |
| セキュリティヘッダー | ⭐⭐⭐⭐ | ⭐⭐ | AdminPortal で CSP 欠落 |
| エラーハンドリング | ⭐⭐⭐⭐ | ⭐⭐ | AdminPortal で ProblemDetails 未対応、try-catch 不統一 |
| 可観測性 | ⭐⭐⭐⭐ | ⭐ | AdminPortal で OpenTelemetry/Correlation ID 未実装 |
| DI 設計 | ⭐⭐⭐ | ⭐⭐ | 両方インターフェース不足だが Frontend は基盤あり |
| 認証フロー | ⭐⭐⭐ | ⭐ | AdminPortal で JWT 転送欠落 |
| C# 14 活用 | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | 両方とも模範的 |
| 禁止パターン | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | 両方とも完全遵守 |

**所見**: Frontend は高品質な BFF 実装基盤を持つが、AdminPortal は「機能的には動作するが品質基盤が不足」している状態。特に認証トークン転送・エラーハンドリング・可観測性の 3 領域で顕著な品質差がある。

---

## 優れた実装（評価すべきパターン）

1. **C# 14 全面活用**: primary constructor, record, collection expression `[]`, file-scoped namespace が全ファイルで統一。禁止パターン（`Console.Write`, `new HttpClient()`, `.Result/.Wait()`, `DateTime.Now`）ゼロ
2. **BFF パターン徹底**: DbContext/EF Core 依存なし。全データ取得が API Gateway 経由。JWT がクライアントに非露出
3. **`Task.WhenAll` 並列化**: Home.razor（5API）、Detail.razor（3API）、MyPage/Index.razor（2段階WhenAll）で模範的な並列化
4. **AiApiClient グレースフルデグラデーション**: 全7メソッドで try-catch + 空リスト返却。AI サービス障害でもフロントエンド継続
5. **構造化ログ完全遵守**: 全ファイルで Serilog テンプレート形式。文字列補間ゼロ。PII ログ出力ゼロ
6. **Cookie セキュリティ**: HttpOnly=true, Secure=Always, SameSite=Strict が両サービスで徹底
7. **AdminPortal CancellationToken**: 全 PageModel ハンドラで CT を受け取り完全伝搬（模範的）
8. **テスト命名**: 全テストが `Should_X_When_Y` パターン準拠。`[Trait]` 分類も統一

---

## 各 Agent 詳細レポート

<details>
<summary>tech-lead レビューレポート</summary>

- **判定**: ⚠️ Warning — Critical: 2 / High: 8 / Medium: 10 / Low: 5
- **総合スコア**: 16/20
- **主要指摘**: AiChatWidget 空 catch（禁止事項§4.2）、Login.razor 例外握りつぶし（§4.7）、AdminPortal JWT 未転送、AdminPortal サービス抽象化欠如、CartEffects API パス不整合、OpenTelemetry 未設定、CSP 欠落
- **Agent間競合裁定**: AdminPortal PageModel 直接 HttpClient 使用は BFF として許容（共通クライアント導入推奨）、TokenRefreshHandler SemaphoreSlim は static 化推奨、E2E Skip テストは条件付き Pass

</details>

<details>
<summary>architecture-reviewer レビューレポート（Frontend）</summary>

- **判定**: ⚠️ Warning — Critical: 0 / High: 5 / Medium: 7 / Low: 3
- **総合スコア**: 16/25
- **主要指摘**: CartEffects の IApiGatewayClient 直接注入+パス不整合、Login.razor 認証ロジック重複、DTO 配置規約違反、Payment.razor ポイント残高ハードコード、ApiErrorHandler の UI 層依存
- **BFF パターン評価**: トークン非露出✅、IHttpClientFactory✅、API Gateway 経由✅、Polly 統合✅

</details>

<details>
<summary>architecture-reviewer レビューレポート（AdminPortal）</summary>

- **判定**: ❌ Fail — Critical: 2 / High: 6 / Medium: 7 / Low: 3
- **総合スコア**: 24/35
- **Critical**: JWT トークン未転送（BFF パターン根幹が機能していない）、htmx SRI なし CDN 読み込み
- **主要指摘**: htmx/Alpine.js 未活用、未使用 NuGet パッケージ多数、API パスプレフィックス不整合、Error.cshtml 不在

</details>

<details>
<summary>ddd-domain-reviewer レビューレポート</summary>

- **判定**: ⚠️ Warning — Critical: 0 / High: 7 / Medium: 7 / Low: 3
- **総合スコア**: 24/30
- **主要指摘**: CartState.TotalAmount 金額再計算漏洩、AdminPortal mutable class DTO、CartEffects パス不整合、Orders/Detail Staff ロール制限の BFF 漏洩、DTO 重複定義
- **良い点**: BFF として Aggregate Root を正しく持たない、Fluxor Reducer の純粋関数設計

</details>

<details>
<summary>csharp-standards-reviewer レビューレポート</summary>

- **判定**: ⚠️ Warning — Critical: 0 / High: 2 / Medium: 7 / Low: 3
- **総合スコア**: 21/25
- **主要指摘**: 14 サービスクラスにインターフェース未定義、AuthEffects 具象クラス直接依存
- **優れた点**: C# 14 機能全面活用(5/5)、禁止パターン完全遵守(5/5)、Null Safety(5/5)

</details>

<details>
<summary>config-di-reviewer レビューレポート</summary>

- **判定**: ❌ Fail — Critical: 0 / High: 9 / Medium: 8 / Low: 3
- **総合スコア**: 14/25
- **主要指摘**: UseExceptionHandler 条件付き配置、Correlation ID 受信側未実装、UseRateLimiter 欠落、IOptions パターン未使用、FluentValidation DI 未登録、AdminPortal CSP/Readiness/ShutdownTimeout 欠落
- **良い点**: appsettings 秘密情報ゼロ(5/5)

</details>

<details>
<summary>api-endpoint-reviewer レビューレポート</summary>

- **判定**: ⚠️ Warning — Critical: 3 / High: 9 / Medium: 10 / Low: 5
- **総合スコア**: 16/25
- **Critical**: AdminPortal BindProperty Data Annotations 全欠落、ログインフォーム検証なし、APIエラーボディ生ログ出力
- **Frontend API クライアント評価**: 3 層構造・ApiErrorHandler・RFC 9457 対応は高品質。AdminPortal との品質差が顕著

</details>

<details>
<summary>data-access-reviewer レビューレポート</summary>

- **判定**: ⚠️ Warning — Critical: 1 / High: 5 / Medium: 6 / Low: 2
- **総合スコア**: 29/40
- **Critical**: キャッシュキーにフィルタ条件不足
- **主要指摘**: InvalidateByPrefix No-Op、Cache Stampede 問題、TokenRefreshHandler 新トークン保存欠落、Inventory ページネーション未対応
- **良い点**: BFF アーキテクチャ準拠(5/5)、CancellationToken 伝搬(5/5)

</details>

<details>
<summary>async-concurrency-reviewer レビューレポート</summary>

- **判定**: ⚠️ Warning — Critical: 2 / High: 5 / Medium: 3 / Low: 2
- **総合スコア**: 16/20
- **Critical**: Fluxor Effects CT 未伝搬（→競合裁定で High に降格）、Blazor OnInitializedAsync CT 未使用
- **良い点**: 同期ブロッキング完全ゼロ(5/5)、サービス層・AdminPortal PageModel の CT 伝搬は模範的

</details>

<details>
<summary>error-logging-reviewer レビューレポート</summary>

- **判定**: ⚠️ Warning — Critical: 2 / High: 8 / Medium: 6 / Low: 3
- **総合スコア**: 18/25
- **Critical**: Login.razor 例外ログ未出力、Register.razor 例外ログ未出力
- **良い点**: 構造化ログ品質(5/5)、PII ログ遵守(5/5)

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

- **判定**: ⚠️ Warning — Critical: 1 / High: 7 / Medium: 8 / Low: 2
- **総合スコア**: 41/60
- **Critical**: AdminPortal → API Gateway 無認証通信
- **良い点**: インジェクション防止(5/5)、秘密情報管理(5/5)、HtmlSanitizationService✅、Cookie 設定✅
- **STRIDE 分析**: Elevation of Privilege で Critical、DoS で High リスク検出

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

- **判定**: ⚠️ Warning — Critical: 2 / High: 5 / Medium: 5 / Low: 3
- **総合スコア**: 16/25
- **Critical**: ReorderAsync N+1（2箇所）、キャッシュキーバグ
- **良い点**: Home.razor/Detail.razor の Task.WhenAll 並列化、readonly record struct 活用

</details>

<details>
<summary>resilience-reviewer レビューレポート</summary>

- **判定**: ⚠️ Warning — Critical: 1 / High: 7 / Medium: 6 / Low: 2
- **Critical**: AdminPortal Readiness ヘルスチェック未実装
- **良い点**: IHttpClientFactory + Polly 統合✅、OrderStatusMonitor のポーリングフォールバック✅、AiApiClient 全メソッドのグレースフルデグラデーション✅

</details>

<details>
<summary>dependency-reviewer レビューレポート</summary>

- **判定**: ⚠️ Warning — Critical: 0 / High: 1 / Medium: 4 / Low: 2
- **総合スコア**: 28/30
- **良い点**: 禁止パッケージゼロ(5/5)、プレリリース排除(5/5)、バージョン一貫性(5/5)、ライセンス適合(5/5)
- **主要指摘**: Frontend.Tests TreatWarningsAsErrors 未設定、StackExchange.Redis 未使用参照、AdminPortal OpenTelemetry 未構成

</details>

<details>
<summary>test-quality-reviewer レビューレポート</summary>

- **判定**: ❌ Fail — Critical: 12（→競合裁定で Medium に降格） / High: 16 / Medium: 11 / Low: 3
- **全体カバレッジ見込み**: ~25%（目標 80%）
- **テスト命名**: ✅ 全テスト Should_X_When_Y 準拠
- **主要指摘**: 11 サービスクラス完全未テスト（62 メソッド 0%）、Store Effects 未テスト、9 Shared コンポーネント未テスト、E2E/A11y 全件スタブ、異常系テスト不足
- **良い点**: 命名規約完全遵守、FakeInnerHandler パターンによる DelegatingHandler テスト

</details>

---

## 修正優先度ロードマップ

### Phase 1（即時 — マージ前必須）
1. ❌ C-01: AdminPortal JWT トークン転送ハンドラー実装
2. ❌ C-04/C-05: Login.razor / Register.razor の例外ログ出力追加
3. ❌ C-03: ProductApiClient キャッシュキーにフィルタ条件追加
4. ❌ C-10: AiChatWidget 空 catch ブロックにログ出力追加
5. ❌ C-12: Payment.razor ポイント残高を PointApiClient から取得
6. ❌ C-02: htmx / Bootstrap Icons に SRI 属性追加

### Phase 2（短期 — 1-2 週間以内）
7. H-01: AdminPortal CSP ヘッダー追加
8. H-02/H-03: UseExceptionHandler 全環境化 + UseRateLimiter 追加
9. H-10: AdminPortal POST 系メソッド try-catch 追加（5 ページ）
10. H-22: AdminPortal Error.cshtml 作成
11. C-06/C-11: AdminPortal Data Annotations 追加
12. H-05/H-06: TokenRefreshHandler 再設計

### Phase 3（中期 — 次スプリント）
13. H-04: IOptions&lt;T&gt; パターン導入
14. H-11: AdminPortal OpenTelemetry 構成
15. H-15: Frontend サービスインターフェース追加
16. H-23: AdminPortal ProblemDetails パース基盤
17. C-09: ReorderAsync N+1 解消 + DRY 共通化
18. テストカバレッジ拡充（目標: 50% → 80%）
