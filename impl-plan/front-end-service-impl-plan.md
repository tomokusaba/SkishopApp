# フロントエンドサービス フェーズ別実装計画書

> **対象**: SkiShop フロントエンド（EC サイト: Blazor Web App / 管理画面: Razor Pages）
> **技術スタック**: Blazor Web App (.NET 10) + MudBlazor 8.x + Fluxor 6.x + SignalR
> **管理画面**: ASP.NET Core Razor Pages + Bootstrap 5 + htmx + Alpine.js
> **オーケストレーション**: .NET Aspire 13.1
> **設計書**: `design-docs/front-end-design.md`（~3200 行）
> **規約**: AGENTS.md / `.github/instructions/` 配下のインストラクションファイル群

---

## 目次

1. [Phase 1: プロジェクト基盤構築（EC サイト + 管理画面）](#phase-1-プロジェクト基盤構築ec-サイト--管理画面)
2. [Phase 2: 共通基盤・共有ライブラリ構築](#phase-2-共通基盤共有ライブラリ構築)
3. [Phase 3: 認証・認可フロー実装](#phase-3-認証認可フロー実装)
4. [Phase 4: 商品閲覧系画面（Static SSR）](#phase-4-商品閲覧系画面static-ssr)
5. [Phase 5: カート・チェックアウトフロー](#phase-5-カートチェックアウトフロー)
6. [Phase 6: 注文管理・マイページ系画面](#phase-6-注文管理マイページ系画面)
7. [Phase 7: ポイント・クーポン・ウィッシュリスト](#phase-7-ポイントクーポンウィッシュリスト)
8. [Phase 8: AI 機能・リアルタイム通信](#phase-8-ai-機能リアルタイム通信)
9. [Phase 9: 管理画面（Razor Pages）実装](#phase-9-管理画面razor-pages実装)
10. [Phase 10: 国際化（i18n）・アクセシビリティ・SEO](#phase-10-国際化i18nアクセシビリティseo)
11. [Phase 11: テスト（bUnit / Playwright / axe-core）](#phase-11-テストbunit--playwright--axe-core)
12. [Phase 12: パフォーマンス最適化・可観測性](#phase-12-パフォーマンス最適化可観測性)
13. [Phase 13: Docker / デプロイ準備・最終検証](#phase-13-docker--デプロイ準備最終検証)

---

## Phase 1: プロジェクト基盤構築（EC サイト + 管理画面）

### 目的

Blazor Web App（EC サイト）および Razor Pages（管理画面）のプロジェクト骨格を構築する。ビルド可能な最小構成を作成し、.NET Aspire `AppHost` に統合する。

### 対象セクション

- §1.2 技術スタック
- §1.3 バックエンドサービス一覧
- §7 設定ファイル規約（AGENTS.md §7 / dotnet-config.instructions.md）
- §8 NuGet 依存関係管理規約（nuget-dependency.instructions.md）

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `Services/Frontend/Frontend.csproj` | Blazor Web App プロジェクト定義 |
| 2 | `Services/Frontend/Program.cs` | エントリポイント（最小構成 — MudBlazor, Fluxor, IHttpClientFactory, Serilog, 認証, ヘルスチェック） |
| 3 | `Services/Frontend/appsettings.json` | 共通設定（安全なデフォルト値、秘密情報禁止） |
| 4 | `Services/Frontend/appsettings.Development.json` | 開発環境設定 |
| 5 | `Services/Frontend/appsettings.Production.json` | 本番環境設定（環境変数参照のみ） |
| 6 | `Services/Frontend/App.razor` | ルートコンポーネント（`<Router>`） |
| 7 | `Services/Frontend/Routes.razor` | ルーティング設定 |
| 8 | `Services/Frontend/_Imports.razor` | グローバル `@using` 宣言 |
| 9 | `Services/Frontend/Components/Layout/MainLayout.razor` | メインレイアウト（MudBlazor の `MudLayout`） |
| 10 | `Services/Frontend/Components/Layout/MainLayout.razor.css` | CSS 分離ファイル |
| 11 | `Services/Frontend/Components/Layout/NavMenu.razor` | ナビゲーションメニュー |
| 12 | `Services/Frontend/Components/Pages/Home.razor` | トップページスタブ |
| 13 | `Services/Frontend/Components/Pages/Error.razor` | エラーページ |
| 14 | `Services/Frontend/wwwroot/` | 静的アセット（favicon, css） |
| 15 | `Services/Frontend/Dockerfile` | マルチステージビルド + 非 root |
| 16 | `Services/Frontend/.dockerignore` | ビルド不要ファイル除外 |
| 17 | `Services/AdminPortal/AdminPortal.csproj` | Razor Pages 管理画面プロジェクト定義 |
| 18 | `Services/AdminPortal/Program.cs` | Razor Pages エントリポイント（最小構成） |
| 19 | `Services/AdminPortal/Pages/Index.cshtml` / `.cshtml.cs` | 管理ダッシュボードスタブ |
| 20 | `Services/AdminPortal/appsettings.json` | 管理画面共通設定 |
| 21 | `Services/Frontend.Tests/Frontend.Tests.csproj` | bUnit テストプロジェクト |
| 22 | `Services/AdminPortal.Tests/AdminPortal.Tests.csproj` | 管理画面テストプロジェクト |

### 1.1 Frontend.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <!-- UI コンポーネント -->
    <PackageReference Include="MudBlazor" Version="8.*" />

    <!-- 状態管理 -->
    <PackageReference Include="Fluxor.Blazor.Web" Version="6.*" />

    <!-- 認証 -->
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.*" />

    <!-- バリデーション -->
    <PackageReference Include="FluentValidation" Version="11.*" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.*" />

    <!-- 耐障害性 -->
    <PackageReference Include="Polly" Version="8.*" />
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="9.*" />

    <!-- ロギング -->
    <PackageReference Include="Serilog.AspNetCore" Version="8.*" />
    <PackageReference Include="Serilog.Sinks.Console" Version="6.*" />
    <PackageReference Include="Serilog.Formatting.Compact" Version="3.*" />

    <!-- OpenTelemetry -->
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.*" />

    <!-- ヘルスチェック -->
    <PackageReference Include="AspNetCore.HealthChecks.Uris" Version="9.*" />

    <!-- HTML サニタイズ（AI チャットレスポンス用 — §16.9） -->
    <PackageReference Include="HtmlSanitizer" Version="8.*" />

    <!-- キャッシュ -->
    <PackageReference Include="StackExchange.Redis" Version="2.*" />
  </ItemGroup>
</Project>
```

### 1.2 ディレクトリ構造（EC サイト）

```
Services/Frontend/
├── Frontend.csproj
├── Program.cs
├── App.razor
├── Routes.razor
├── _Imports.razor
├── Components/
│   ├── Layout/
│   │   ├── MainLayout.razor          ← MudLayout ベース
│   │   ├── MainLayout.razor.css
│   │   ├── NavMenu.razor             ← ヘッダーナビ + 検索
│   │   ├── Footer.razor              ← フッター（法的ページリンク）
│   │   └── CookieConsentBanner.razor  ← §15.1 同意管理バナー
│   ├── Pages/
│   │   ├── Home.razor                ← 画面 1: トップページ
│   │   ├── Products/                 ← 画面 2, 3: 商品一覧・詳細
│   │   ├── Cart/                     ← 画面 4: カート
│   │   ├── Checkout/                 ← 画面 5a-5e, 5g: チェックアウト
│   │   ├── Orders/                   ← 画面 6, 7, 8, 29, 28: 注文系
│   │   ├── Auth/                     ← 画面 9, 10, 10a, 10b, 23, 24: 認証系
│   │   ├── MyPage/                   ← 画面 11-14, 25-27, 30, 31, 32: マイページ系
│   │   ├── Points/                   ← 画面 18: ポイント履歴
│   │   ├── Coupons/                  ← 画面 17: クーポン一覧
│   │   ├── Wishlists/                ← 画面 15: ウィッシュリスト
│   │   ├── Legal/                    ← 画面 20, 21, 22: 法的ページ
│   │   └── Error.razor               ← エラーページ（404, 403, 500）
│   └── Shared/
│       ├── ProductCard.razor          ← 商品カードコンポーネント
│       ├── SearchSuggest.razor        ← §4.6 検索サジェスト
│       ├── StepIndicator.razor        ← チェックアウトステップ
│       ├── PaginationComponent.razor  ← §4.2 / §16.4 ページネーション
│       ├── AiChatWidget.razor         ← 画面 19: AI チャット
│       ├── StockBadge.razor           ← 在庫ステータスバッジ
│       ├── OrderStatusBadge.razor     ← §15.6 注文ステータスバッジ
│       └── PointExpiryAlert.razor     ← §15.11 ポイント失効アラート
├── Services/
│   ├── Interfaces/
│   │   ├── IApiGatewayClient.cs
│   │   ├── IProductApiClient.cs
│   │   ├── ICartApiClient.cs
│   │   ├── IOrderApiClient.cs
│   │   ├── IAuthApiClient.cs
│   │   ├── IUserApiClient.cs
│   │   ├── ICouponApiClient.cs
│   │   ├── IPointApiClient.cs
│   │   ├── IAiApiClient.cs
│   │   └── ITokenStorageService.cs
│   ├── ApiGatewayClient.cs
│   ├── ProductApiClient.cs
│   ├── CartApiClient.cs
│   ├── OrderApiClient.cs
│   ├── AuthApiClient.cs
│   ├── UserApiClient.cs
│   ├── CouponApiClient.cs
│   ├── PointApiClient.cs
│   ├── AiApiClient.cs
│   ├── TokenStorageService.cs
│   ├── ApiErrorHandler.cs             ← §4.1 / §16.1 エラーハンドリング
│   └── HtmlSanitizer.cs              ← §16.9 XSS 対策
├── Store/                             ← Fluxor ストア
│   ├── CartStore/
│   │   ├── CartState.cs
│   │   ├── CartActions.cs
│   │   ├── CartReducers.cs
│   │   └── CartEffects.cs
│   ├── AuthStore/
│   │   ├── AuthState.cs
│   │   ├── AuthActions.cs
│   │   ├── AuthReducers.cs
│   │   └── AuthEffects.cs
│   └── ThemeStore/
├── Models/                            ← フロントエンド共有 DTO
│   ├── PaginatedResult.cs             ← §16.4 汎用ページネーション型
│   ├── ProblemDetailsResponse.cs      ← §16.1 RFC 9457 エラー型
│   └── ApiValidationException.cs      ← §16.1 バリデーション例外
├── Handlers/
│   ├── TokenRefreshHandler.cs         ← §16.2 自動トークンリフレッシュ
│   └── CorrelationIdHandler.cs        ← §16.3 Correlation ID
├── Infrastructure/
│   └── Middleware/
│       ├── CorrelationIdMiddleware.cs
│       └── SecurityHeadersMiddleware.cs
├── Resources/
│   ├── SharedResources.ja.resx        ← §16.7 日本語リソース
│   └── SharedResources.en.resx        ← §16.7 英語リソース
├── wwwroot/
│   ├── css/
│   ├── images/
│   └── favicon.ico
├── appsettings.json
├── appsettings.Development.json
├── appsettings.Production.json
├── Dockerfile
└── .dockerignore
```

### 1.3 .NET Aspire AppHost 統合

`AppHost/Program.cs` に以下を追加:

```csharp
var frontend = builder.AddProject<Projects.Frontend>("frontend")
    .WithReference(apiGateway)
    .WithReference(redis);

var adminPortal = builder.AddProject<Projects.AdminPortal>("admin-portal")
    .WithReference(apiGateway)
    .WithReference(redis);
```

### Phase 1 完了チェックリスト

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | `dotnet build Services/Frontend/Frontend.csproj` | 警告なし成功 |
| 2 | `dotnet build Services/AdminPortal/AdminPortal.csproj` | 警告なし成功 |
| 3 | `dotnet build Services/Frontend.Tests/Frontend.Tests.csproj` | 警告なし成功 |
| 4 | `dotnet run --project Services/Frontend` | 起動確認（`/health` で 200 応答） |
| 5 | `dotnet run --project Services/AdminPortal` | 起動確認（`/health` で 200 応答） |
| 6 | .csproj: `TreatWarningsAsErrors=true` | 両プロジェクトで設定済み |
| 7 | .csproj: `Nullable=enable` | 両プロジェクトで設定済み |
| 8 | .csproj: `TargetFramework=net10.0` | 両プロジェクトで設定済み |
| 9 | .csproj: プレリリース版パッケージなし | `-preview`, `-beta`, `-rc` なし |
| 10 | appsettings.json: 秘密情報なし | パスワード、API キー、接続文字列なし |
| 11 | appsettings.json: `DetailedErrors: false` | 設定済み |
| 12 | appsettings.json: `AddServerHeader: false` | 設定済み |
| 13 | MudBlazor テーマ適用確認 | ブラウザで MudBlazor コンポーネントが表示されること |
| 14 | AppHost で Frontend / AdminPortal が起動すること | `dotnet run --project AppHost` で両プロジェクト起動 |
| 15 | Dockerfile: マルチステージビルド + 非 root + HEALTHCHECK | 両プロジェクト |
| 16 | ディレクトリ構造が §1.2 の設計通り | Pages/, Shared/, Services/, Store/, Models/ 配置 |
| 17 | TODO/FIXME/HACK コメント残存なし | `grep -rn "TODO\|FIXME\|HACK" --include="*.razor" --include="*.cs" Services/Frontend/` で 0 件 |

---

## Phase 2: 共通基盤・共有ライブラリ構築

### 目的

全画面で再利用する共通コンポーネント、API クライアント基盤、エラーハンドリング、状態管理の土台を構築する。

### 対象セクション

- §4.1 エラーハンドリング（RFC 9457 — Problem Details）
- §4.2 ページネーション
- §4.3 グレースフルデグラデーション
- §4.4 API 通信共通設定（BFF パターン）
- §4.5 レスポンシブデザインブレークポイント
- §6 状態管理設計（Fluxor / CascadingParameters）
- §15.6 注文ステータス表示マッピング
- §15.7 タッチターゲットサイズ要件
- §15.8 配送料・消費税表示仕様
- §16.1 API エラーハンドリング詳細
- §16.2 認証・トークン管理詳細
- §16.3 Correlation ID ハンドリング
- §16.4 ページネーション共通コンポーネント
- §16.9 フロントエンドセキュリティ対策

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `Services/Frontend/Models/PaginatedResult.cs` | §16.4 汎用ページネーション型 |
| 2 | `Services/Frontend/Models/ProblemDetailsResponse.cs` | §16.1 RFC 9457 エラー型 |
| 3 | `Services/Frontend/Models/ApiValidationException.cs` | §16.1 バリデーション例外 |
| 4 | `Services/Frontend/Services/ApiErrorHandler.cs` | §4.1 / §16.1 エラーハンドリングサービス |
| 5 | `Services/Frontend/Handlers/TokenRefreshHandler.cs` | §16.2 トークンリフレッシュ DelegatingHandler |
| 6 | `Services/Frontend/Handlers/CorrelationIdHandler.cs` | §16.3 Correlation ID DelegatingHandler |
| 7 | `Services/Frontend/Services/Interfaces/IApiGatewayClient.cs` | API Gateway クライアントインターフェース |
| 8 | `Services/Frontend/Services/ApiGatewayClient.cs` | §4.4 BFF パターン API クライアント基盤 |
| 9 | `Services/Frontend/Components/Shared/PaginationComponent.razor` | §4.2 / §16.4 ページネーション UI |
| 10 | `Services/Frontend/Components/Shared/OrderStatusBadge.razor` | §15.6 注文ステータスバッジ |
| 11 | `Services/Frontend/Components/Shared/StockBadge.razor` | 画面 3 在庫ステータスバッジ |
| 12 | `Services/Frontend/Store/CartStore/*` | §6.1 / §6.3 Fluxor カートストア |
| 13 | `Services/Frontend/Store/AuthStore/*` | §6.1 Fluxor 認証ストア |
| 14 | `Services/Frontend/Services/HtmlSanitizer.cs` | §16.9 XSS 対策 HTML サニタイザー |
| 15 | `Services/Frontend/Components/Layout/MainLayout.razor` | MudLayout ベースレイアウト（スキップリンク、ランドマーク構造 §16.6） |
| 16 | `Services/Frontend/Components/Layout/NavMenu.razor` | ヘッダーナビ + 検索ボックス（§4.6 連動） |
| 17 | `Services/Frontend/Components/Layout/Footer.razor` | フッター（法的ページリンク） |
| 18 | `Services/Frontend/Program.cs` | 更新: IHttpClientFactory + Polly + DI 登録 + ミドルウェアパイプライン |

### 2.1 Program.cs — ミドルウェアパイプライン（AGENTS.md §11.3 準拠）

```csharp
// ミドルウェア登録順序（厳守）
app.UseExceptionHandler();
app.UseHsts();
app.UseHttpsRedirection();
// Correlation ID
// Serilog リクエストログ
app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseStaticFiles();
app.UseAntiforgery();
// エンドポイントマッピング
app.MapRazorComponents<App>().AddInteractiveServerRenderMode().AddInteractiveWebAssemblyRenderMode();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");
```

### 2.2 BFF パターン API クライアント DI 登録（§4.4）

```csharp
builder.Services.AddHttpClient<IApiGatewayClient, ApiGatewayClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:8080");
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddHttpMessageHandler<TokenRefreshHandler>()
.AddHttpMessageHandler<CorrelationIdHandler>()
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.Delay = TimeSpan.FromMilliseconds(500);
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
});
```

### Phase 2 完了チェックリスト

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | `dotnet build Services/Frontend/Frontend.csproj` | 警告なし成功 |
| 2 | `ProblemDetailsResponse` record が §16.1 の 7 フィールドを持つ | Type, Title, Status, Detail, Instance, Errors, TraceId |
| 3 | `ApiErrorHandler` が §16.1 の全ステータスコード（400/401/403/404/409/422/429/500/503）を処理 | switch 式で全分岐網羅 |
| 4 | `TokenRefreshHandler` が 401 受信時に 1 回のみリフレッシュ試行 | SemaphoreSlim 実装 |
| 5 | `CorrelationIdHandler` がレスポンスヘッダーから `X-Correlation-Id` を取得・ログ出力 | ILogger 出力確認 |
| 6 | `PaginatedResult<T>` が §16.4 の全プロパティを持つ | Items, TotalElements, Page, Size, TotalPages, HasNext, HasPrevious |
| 7 | IHttpClientFactory + Polly（リトライ 3 回 + サーキットブレーカー）が設定済み | Program.cs で `AddStandardResilienceHandler` 確認 |
| 8 | Fluxor ストア（CartStore, AuthStore）の State/Action/Reducer/Effect が定義済み | ファイル存在確認 |
| 9 | MainLayout に `<a href="#main-content" class="skip-link">` あり | §16.6 スキップリンク |
| 10 | MainLayout のランドマーク構造（`<header>`, `<nav>`, `<main>`, `<footer>`）が §16.6 準拠 | HTML 構造確認 |
| 11 | `OrderStatusBadge` が §15.6 の全 8 ステータスを表示 | PENDING～REFUNDED |
| 12 | `StockBadge` が画面 3 在庫表示ルール 3 段階を実装 | 10 以上/1-9/0 |
| 13 | `HtmlSanitizer` がホワイトリスト方式で許可タグを制限 | §16.9 AllowedTags |
| 14 | `new HttpClient()` 直接使用なし | DI コンテナ経由のみ |
| 15 | `Console.WriteLine` 使用なし | `ILogger<T>` のみ |
| 16 | ミドルウェアパイプライン順序が AGENTS.md §11.3 準拠 | ExceptionHandler → HSTS → Auth → RateLimiter |
| 17 | TODO/FIXME/HACK コメント残存なし | grep 0 件 |
| 18 | Mock/Stub/仮実装残存なし | `NotImplementedException` が本番コードに 0 件 |

---

## Phase 3: 認証・認可フロー実装

### 目的

ログイン、ユーザー登録、メール認証、MFA、パスワードリセット、ログアウト、OAuth の全認証画面を実装する。BFF パターンで JWT をサーバーサイド管理する。

### 対象セクション

- §2.2 画面 9: ログイン（MFA 検証ステップ含む）
- §2.2 画面 10: ユーザー登録
- §2.2 画面 10a: メール認証待ち
- §2.2 画面 10b: メール認証結果
- §2.2 画面 23: パスワードリセット要求
- §2.2 画面 24: パスワードリセット
- §5 認証・認可フロー（§5.1 JWT, §5.2 リフレッシュ, §5.3 トークン仕様）
- §9.1 認証 API
- §15.4b MFA セットアップ画面
- §16.2 認証・トークン管理詳細（OAuth フロー含む）

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `Pages/Auth/Login.razor` | 画面 9: ログイン（MFA 検証ステップ含む） |
| 2 | `Pages/Auth/Register.razor` | 画面 10: ユーザー登録 |
| 3 | `Pages/Auth/EmailPending.razor` | 画面 10a: メール認証待ち |
| 4 | `Pages/Auth/EmailVerify.razor` | 画面 10b: メール認証結果 |
| 5 | `Pages/Auth/PasswordResetRequest.razor` | 画面 23: パスワードリセット要求 |
| 6 | `Pages/Auth/PasswordReset.razor` | 画面 24: パスワードリセット |
| 7 | `Pages/MyPage/MfaSetup.razor` | 画面 30: MFA セットアップ（§15.4b） |
| 8 | `Services/AuthApiClient.cs` | 認証 API クライアント（§9.1 全エンドポイント） |
| 9 | `Services/TokenStorageService.cs` | サーバーサイドトークン管理 |
| 10 | `Store/AuthStore/*` | 更新: 認証状態管理の完全実装 |

### 各画面の実装要件

#### 画面 9: ログイン

- メール/パスワード入力 + `EditForm` バリデーション
- ログイン成功 → httpOnly Cookie 設定（`SameSite=Strict`, `Secure`）
- MFA 検証ステップ: `mfaRequired: true` 判定 → 6 桁コード入力 → `POST /api/v1/auth/mfa/verify`
- バックアップコード切替リンク
- 5 回失敗で 15 分ロック表示
- レート制限: 10req/分
- ログイン後: ゲストカートマージ（`POST /api/v1/cart/merge`）
- OAuth ボタン（§16.2 Microsoft Entra ID）

#### 画面 10: ユーザー登録

- 5 フィールドバリデーション（メール重複チェック、パスワード強度メーター）
- 登録成功 → 画面 10a に自動遷移

#### 画面 10a/10b: メール認証

- 再送信レート制限（60 秒カウントダウン）
- トークン検証結果の 3 パターン表示（成功/期限切れ/無効）

### Phase 3 完了チェックリスト

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | `dotnet build` 警告なし | 全画面コンパイル成功 |
| 2 | ログイン画面が `POST /api/v1/auth/login` を呼び出し | API 連携確認 |
| 3 | MFA 検証ステップが `mfaRequired: true` で表示される | 条件分岐確認 |
| 4 | MFA 最大試行回数 5 回 + 15 分ロック | 回数制限実装 |
| 5 | JWT を httpOnly Cookie で保存（`SameSite=Strict`, `Secure`） | `localStorage` 使用なし |
| 6 | ログイン後にゲストカートマージ API 呼び出し | `POST /api/v1/cart/merge` |
| 7 | ユーザー登録のバリデーション 5 項目 | メール/パスワード/パスワード確認/氏名/規約 |
| 8 | 登録成功 → メール認証待ち画面（10a）に自動遷移 | NavigateTo 確認 |
| 9 | メール再送信のレート制限（60 秒） | カウントダウン UI |
| 10 | メール認証結果の 3 パターン表示 | 成功/期限切れ/無効 |
| 11 | パスワードリセット要求 → リセット完了のフロー | 全ステップ遷移確認 |
| 12 | MFA セットアップ: QR コード + 手動入力シークレット表示 | §15.4b アクセシビリティ |
| 13 | MFA バックアップコード表示 + コピー/ダウンロードボタン | 一度だけ表示 |
| 14 | OAuth ログインボタン表示 + リダイレクト | §16.2 OAuth フロー |
| 15 | ログアウト: トークン無効化 API + Cookie 削除 + キャッシュクリア + トップページ `forceLoad` リダイレクト | §16.2 ログアウトフロー |
| 16 | エラーメッセージが「メールアドレスまたはパスワードが正しくありません」（情報漏洩防止） | 具体的なフィールド名を返さない |
| 17 | CancellationToken を全 async メソッドに伝搬 | シグネチャ確認 |
| 18 | TODO/FIXME/HACK コメント残存なし | grep 0 件 |

---

## Phase 4: 商品閲覧系画面（Static SSR）

### 目的

トップページ、商品一覧、商品詳細の 3 画面を Static SSR モードで実装する。SEO 最適化と Core Web Vitals 達成を目指す。

### 対象セクション

- §2.2 画面 1: トップページ
- §2.2 画面 2: 商品一覧
- §2.2 画面 3: 商品詳細（サイズガイド含む）
- §2.2 画面 16: レビュー投稿
- §4.6 検索サジェストコンポーネント
- §8.4 SEO
- §9.3 商品 API
- §16.8 パフォーマンス詳細

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `Pages/Home.razor` | 画面 1: トップページ（AI レコメンデーション 3 セクション含む） |
| 2 | `Pages/Products/List.razor` | 画面 2: 商品一覧 |
| 3 | `Pages/Products/Detail.razor` | 画面 3: 商品詳細 |
| 4 | `Pages/Products/Review.razor` | 画面 16: レビュー投稿 |
| 5 | `Shared/SearchSuggest.razor` | §4.6 検索サジェスト（aria-combobox） |
| 6 | `Shared/ProductCard.razor` | 商品カード（一覧/カルーセル共用） |
| 7 | `Shared/SizeGuideModal.razor` | 画面 3 サイズガイドモーダル |
| 8 | `Services/ProductApiClient.cs` | §9.3 商品 API クライアント |
| 9 | `Services/AiApiClient.cs` | §9.8 AI API クライアント（レコメンデーション） |
| 10 | `Pages/Legal/Tokushoho.razor` | 画面 20: 特定商取引法に基づく表記（静的ページ） |
| 11 | `Pages/Legal/Privacy.razor` | 画面 21: プライバシーポリシー（静的ページ） |
| 12 | `Pages/Legal/Terms.razor` | 画面 22: 利用規約（静的ページ） |

### 実装要件

#### トップページ（画面 1）

- Static SSR モード
- ヒーローバナー + 新着 8 件 + 人気 8 件
- AI レコメンデーション 3 セクション（trending / seasonal / personalized）
- パーソナライズは認証時のみ。未認証時は trending + seasonal のみ
- AI API 障害時: セクション非表示（§4.3 グレースフルデグラデーション）
- LCP ≤ 2.5 秒: ヒーロー画像 `loading="eager"`, カルーセル `loading="lazy"`

#### 商品一覧（画面 2）

- カテゴリ/価格帯/ソート/ページネーション/キーワード検索
- 検索サジェスト連動（§4.6）
- `page=0` 始まり、`size=20` デフォルト

#### 商品詳細（画面 3）

- 画像ギャラリー + サイズ/カラー選択 + 在庫表示（3 段階）
- サイズガイドモーダル（`GET /api/size-guides/{categoryId}`）
- AI レコメンデーション 2 セクション（similar / frequently-bought）+ フィードバックボタン
- AI API 障害時: 同カテゴリ商品にフォールバック

#### 検索サジェスト（§4.6）

- 2 文字以上 + デバウンス 300ms
- `GET /api/v1/ai/search/suggest?query={input}`
- ドロップダウン最大 10 件、カテゴリ別グルーピング
- キーボード操作（↑↓ Enter Escape）
- `aria-combobox` パターン準拠（WCAG SC 4.1.2）
- AI 障害時: サジェスト非表示、通常検索のみ

### Phase 4 完了チェックリスト

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | トップページが Static SSR でレンダリング | `@rendermode` 指定なし |
| 2 | 新着/人気商品の API 呼び出し（`sort=createdAt,desc` / `sort=salesCount,desc`） | API パラメータ確認 |
| 3 | AI レコメンデーション 3 セクション表示（trending / seasonal / personalized） | ログイン時のみ personalized |
| 4 | AI API 障害時にセクション非表示 | §4.3 グレースフルデグラデーション |
| 5 | 商品一覧: 5 種フィルター動作（カテゴリ/価格帯/ソート/ページ/キーワード） | クエリパラメータ |
| 6 | 商品詳細: 在庫表示 3 段階（10 以上/1-9/0） | `StockBadge` 使用 |
| 7 | サイズガイドモーダル表示 + `role="table"` + `aria-label` | §2.2 画面 3 |
| 8 | 検索サジェスト: 2 文字以上 + デバウンス 300ms | §4.6 |
| 9 | 検索サジェスト: `aria-combobox` + キーボード操作 | WCAG SC 4.1.2 |
| 10 | SEO メタタグ（title, description, OGP, JSON-LD） | §8.4 `<HeadContent>` |
| 11 | ヒーロー画像 `loading="eager"` + カルーセル `loading="lazy"` | §16.8 画像最適化 |
| 12 | ページネーションに `<nav aria-label="ページネーション">` + `aria-current="page"` | §16.4 アクセシビリティ |
| 13 | AI レコメンデーションフィードバック（`POST /api/v1/ai/recommendations/feedback`） | 認証必要 |
| 14 | 商品詳細: AI レコメンデーション 2 セクション（similar / frequently-bought）表示 | §2.2 画面 3 |
| 15 | 商品詳細: AI API 障害時に同カテゴリ商品にフォールバック | §4.3 グレースフルデグラデーション |
| 16 | 法的ページ 3 件（画面 20 特定商取引法 / 画面 21 プライバシーポリシー / 画面 22 利用規約）が作成済み | 静的ページ |
| 17 | 法的ページがフッターからリンクされていること | Footer.razor 確認 |
| 18 | CancellationToken 全 async メソッドに伝搬 | シグネチャ確認 |
| 19 | TODO/FIXME/HACK コメント残存なし | grep 0 件 |

---

## Phase 5: カート・チェックアウトフロー

### 目的

カート画面、5 ステップチェックアウト、ゲストチェックアウトの購入フロー全体を実装する。Stripe Hosted Payment Page 連携と Saga パターン対応を含む。

### 対象セクション

- §2.2 画面 4: カート
- §2.2 画面 5a-5e: チェックアウト 5 ステップ
- §2.2 ゲスト購入フロー（画面 5g）
- §6.3 楽観的更新パターン
- §9.4 カート API
- §9.5 注文 API
- §9.9 決済 API
- §15.8 配送料・消費税表示仕様

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `Pages/Cart/Index.razor` | 画面 4: カート（Interactive Server） |
| 2 | `Pages/Checkout/CartReview.razor` | ステップ 1: カート確認 |
| 3 | `Pages/Checkout/Shipping.razor` | ステップ 2: 配送先入力 |
| 4 | `Pages/Checkout/Payment.razor` | ステップ 3: お支払い（ポイント使用含む） |
| 5 | `Pages/Checkout/Confirm.razor` | ステップ 4: 注文確認（WCAG SC 3.3.4） |
| 6 | `Pages/Checkout/Complete.razor` | ステップ 5: 注文完了（Saga ポーリング） |
| 7 | `Pages/Checkout/Guest.razor` | ゲストチェックアウト |
| 8 | `Shared/StepIndicator.razor` | チェックアウトステップインジケーター |
| 9 | `Services/CartApiClient.cs` | §9.4 カート API クライアント |
| 10 | `Services/OrderApiClient.cs` | §9.5 注文 API クライアント |
| 11 | `Services/CouponApiClient.cs` | §9.6 クーポン API クライアント（検証 — `POST /api/v1/coupons/validate`） |
| 12 | `Services/PaymentApiClient.cs` | §9.9 決済 API クライアント（返金は Phase 9 で使用） |

### 実装要件

#### カート（画面 4 — Interactive Server）

- カートアイテム CRUD（数量変更 ± ボタン、削除）
- 楽観的更新パターン（§6.3）: UI 即時反映 → API 呼び出し → 失敗時ロールバック
- クーポンコード入力 + 検証（`POST /api/v1/coupons/validate`）
- ポイント残高表示（ログイン時）
- ゲストカート: Cookie（`CartId`）管理（`HttpOnly`, `Secure`, `SameSite=Strict`）

#### チェックアウト（5 ステップ — Interactive WebAssembly）

- ステップインジケーター: `aria-current="step"` + スクリーンリーダー対応
- ステップ 3: ポイント使用入力（§M-05 準拠 — 1pt=1円, 50%上限, スライダー+数値入力+「全額使う」ボタン, インラインバリデーション）
- ステップ 4: 注文確認画面 WCAG SC 3.3.4 準拠（「¥XX,XXX で注文を確定する」金額入りボタン）
- ステップ 4 → Stripe リダイレクト（`POST /api/v1/orders` → `checkoutSessionUrl`）
- ステップ 5: Saga パターン対応ポーリング（3 秒間隔、30 秒タイムアウト）
- 金額サマリー表示（§15.8 表示順: 小計→税→送料→クーポン→ポイント→合計→獲得予定ポイント）

#### ゲストチェックアウト

- メールアドレス 2 回入力一致検証
- 越境データ移転同意 + プライバシーポリシー同意
- ポイント/クーポン適用なし

### Phase 5 完了チェックリスト

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | カート: CRUD 操作（追加/数量変更/削除/全クリア） | §9.4 全 API 呼び出し |
| 2 | カート: 楽観的更新 + エラー時ロールバック | §6.3 パターン |
| 3 | カート: ゲストカート Cookie（`HttpOnly`, `Secure`, `SameSite=Strict`） | Cookie 設定確認 |
| 4 | ステップインジケーター: `aria-current="step"` + `aria-label="ステップ N/5"` | WCAG |
| 5 | ステップ 3: ポイント使用（1pt=1円, 50%上限, スライダー+入力+全額ボタン） | M-05 準拠 |
| 6 | ステップ 3: ポイントバリデーション（残高超過/上限超過/負数） | インラインエラー |
| 7 | ステップ 4: 金額入りボタン「¥XX,XXX で注文を確定する」 | WCAG SC 3.3.4 |
| 8 | ステップ 4: 「変更」リンク（配送先/支払い方法/クーポン・ポイント） | 遷移確認 |
| 9 | ステップ 4 → Stripe リダイレクト | `checkoutSessionUrl` 使用 |
| 10 | ステップ 5: Saga ポーリング（3 秒間隔/30 秒タイムアウト） | PENDING → CONFIRMED |
| 11 | ステップ 5: FAILED/CANCELLED 表示 + 再注文リンク | エラーケース |
| 12 | ゲスト: メール 2 回入力一致 + 同意チェックボックス 2 つ | バリデーション |
| 13 | 金額サマリー表示順が §15.8 準拠 | 7 行表示 |
| 14 | 送料無料プロモーション「あと ¥{差額} で送料無料！」 | §15.8 |
| 15 | CancellationToken 全 async メソッドに伝搬 | シグネチャ確認 |
| 16 | TODO/FIXME/HACK コメント残存なし | grep 0 件 |

---

## Phase 6: 注文管理・マイページ系画面

### 目的

注文履歴、注文詳細（配送追跡・返品申請含む）、マイページとそのサブページ（プロフィール、パスワード変更、住所管理、同意管理、データエクスポート、アカウント削除、アクティビティ履歴、ユーザー設定）を実装する。

### 対象セクション

- §2.2 画面 7: 注文履歴一覧
- §2.2 画面 8: 注文詳細（配送追跡 §15.4 / 返品ステータス §15.4a / キャンセル §15.4）
- §2.2 画面 11: マイページ（ポイント失効アラート §15.11 含む）
- §2.2 画面 12: プロフィール編集
- §2.2 画面 13: パスワード変更
- §2.2 画面 14: 住所管理
- §15.2 画面 25: 同意管理 / 画面 26: データエクスポート / 画面 27: アカウント削除
- §15.3 再注文機能
- §15.4 注文キャンセル確認画面
- §15.4a 画面 29: 返品申請
- §15.4c 画面 31: アクティビティ履歴
- §15.4d 画面 32: ユーザー設定
- §2.2 画面 6: 注文確認
- §15.5 画面 28: ゲスト注文追跡

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `Pages/Orders/Index.razor` | 画面 7: 注文履歴一覧 + 再注文ボタン |
| 2 | `Pages/Orders/Detail.razor` | 画面 8: 注文詳細（配送追跡+キャンセル+返品ボタン）+ 画面 6: 注文確認（同一コンポーネントで表示モード切替） |
| 3 | `Pages/Orders/Return.razor` | 画面 29: 返品申請 |
| 4 | `Pages/Orders/Track.razor` | 画面 28: ゲスト注文追跡 |
| 5 | `Pages/MyPage/Index.razor` | 画面 11: マイページ（ポイント失効アラート含む） |
| 6 | `Pages/MyPage/Profile.razor` | 画面 12: プロフィール編集 |
| 7 | `Pages/MyPage/Password.razor` | 画面 13: パスワード変更 |
| 8 | `Pages/MyPage/Addresses.razor` | 画面 14: 住所管理 |
| 9 | `Pages/MyPage/Privacy.razor` | 画面 25: 同意管理 |
| 10 | `Pages/MyPage/DataExport.razor` | 画面 26: データエクスポート |
| 11 | `Pages/MyPage/DeleteAccount.razor` | 画面 27: アカウント削除 |
| 12 | `Pages/MyPage/Activities.razor` | 画面 31: アクティビティ履歴 |
| 13 | `Pages/MyPage/Settings.razor` | 画面 32: ユーザー設定 |
| 14 | `Shared/PointExpiryAlert.razor` | ポイント失効アラートバナー |
| 15 | `Services/UserApiClient.cs` | §9.2 ユーザー API クライアント |

### Phase 6 完了チェックリスト

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | 注文履歴一覧: ページネーション + 各行に「再注文」ボタン | §15.3 |
| 2 | 注文詳細: 配送追跡タイムライン表示（5 段階ステータス） | §15.4 H-07 |
| 3 | 注文詳細: 追跡番号 + 配送業者外部リンク（`target="_blank" rel="noopener noreferrer"`） | セキュリティ |
| 4 | 注文詳細: キャンセルボタン表示条件（PENDING/CONFIRMED/PROCESSING のみ） | §15.4 |
| 5 | 注文詳細: キャンセル確認モーダル（`role="alertdialog"` + フォーカストラップ） | WCAG |
| 6 | 注文詳細: 返品申請ボタン（DELIVERED + 14 日以内のみ） | §15.4a |
| 7 | 返品申請: 商品チェックボックス + 数量 + 理由ドロップダウン + 確認モーダル | §15.4a |
| 8 | 返品ステータスバッジ 5 段階（REQUESTED/APPROVED/REJECTED/RETURNED/REFUNDED） | §15.4a |
| 9 | ゲスト注文追跡: 注文番号+メール検索 + レート制限 5req/分表示 | §15.5 |
| 10 | マイページ: ポイント失効アラート（30 日以内失効分） | §15.11 H-09 |
| 11 | マイページ: クイックリンク 7 項目 | プロフィール/注文履歴/住所/パスワード/MFA/アクティビティ/設定 |
| 12 | 住所管理: CRUD 操作 | §9.2 addresses API |
| 13 | 同意管理: 4 カテゴリトグル + 保存 | §15.2 |
| 14 | データエクスポート: リクエスト + ポーリング + ダウンロード | §15.2 |
| 15 | アカウント削除: パスワード再確認 + 確認チェック + 赤ボタン + 確認モーダル | §15.2 |
| 16 | アクティビティ履歴: テーブル + ページネーション + フィルター | §15.4c |
| 17 | ユーザー設定: メール通知トグル + 言語選択 + テーマ選択 | §15.4d |
| 18 | CancellationToken 全 async メソッドに伝搬 | シグネチャ確認 |
| 19 | TODO/FIXME/HACK コメント残存なし | grep 0 件 |

---

## Phase 7: ポイント・クーポン・ウィッシュリスト

### 目的

ポイント履歴（ティア情報含む）、クーポン一覧、ウィッシュリストの画面を実装する。

### 対象セクション

- §2.2 画面 17: クーポン一覧
- §2.2 画面 18: ポイント履歴 + §15.11 ティア情報表示
- §2.2 画面 15: ウィッシュリスト
- §9.6 クーポン API
- §9.7 ポイント API
- §9.2a ウィッシュリスト API

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `Pages/Points/Index.razor` | 画面 18: ポイント履歴 + ティア情報 |
| 2 | `Pages/Coupons/Index.razor` | 画面 17: クーポン一覧 |
| 3 | `Pages/Wishlists/Index.razor` | 画面 15: ウィッシュリスト |
| 4 | `Services/PointApiClient.cs` | §9.7 ポイント API クライアント |
| 5 | `Services/CouponApiClient.cs` | §9.6 クーポン API クライアント |

### Phase 7 完了チェックリスト

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | ポイント履歴: 履歴一覧テーブル + ページネーション | §9.7 history API |
| 2 | ポイント履歴: ティア情報セクション（ティア名+特典+プログレスバー） | §15.11 |
| 3 | ポイント履歴: ティア API 障害時にティアセクション非表示 | §4.3 グレースフルデグラデーション |
| 4 | クーポン一覧: 利用可能クーポン表示 + 取得ボタン | §9.6 |
| 5 | ウィッシュリスト: CRUD 操作 + 商品追加/削除 | §9.2a |
| 6 | CancellationToken 全 async メソッドに伝搬 | シグネチャ確認 |
| 7 | TODO/FIXME/HACK コメント残存なし | grep 0 件 |

---

## Phase 8: AI 機能・リアルタイム通信

### 目的

AI チャットボット（SignalR ストリーミング）、在庫リアルタイム通知、注文ステータスリアルタイム通知を実装する。

### 対象セクション

- §2.2 画面 19: AI チャット（エスカレーション §15.12 含む）
- §9.8 AI チャット API
- §16.3 リアルタイム通知仕様（SignalR）
- §15.1 Cookie 同意管理バナー

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `Shared/AiChatWidget.razor` | 画面 19: AI チャット（Interactive Server — SignalR） |
| 2 | `Shared/StockRealtimeMonitor.razor` | §16.3 在庫変動 SignalR 監視 |
| 3 | `Shared/OrderStatusMonitor.razor` | §16.3 注文ステータス SignalR 監視 |
| 4 | `Layout/CookieConsentBanner.razor` | §15.1 Cookie 同意管理バナー |
| 5 | `Services/AiApiClient.cs` | 更新: チャット API + エスカレーション |

### 実装要件

#### AI チャット（Interactive Server）

- SignalR `/hubs/ai-chat` でストリーミング応答
- エスカレーション: 「オペレーターに問い合わせ」ボタン + 確認モーダル（§15.12）
- エスカレーション後: チャット入力欄無効化 + メール連絡メッセージ

#### 在庫リアルタイム（§16.3）

- SignalR `/hubs/stock` で在庫変動を購読
- `JoinProductGroup` / `LeaveProductGroup` でグループ管理
- `IAsyncDisposable` で接続解放

#### 注文ステータスリアルタイム（§16.3）

- SignalR `/hubs/orders` で Saga 進行状況をリアルタイム表示
- SignalR 接続失敗時: 3 秒ポーリングにフォールバック

#### Cookie 同意管理バナー（§15.1）

- 4 カテゴリトグル（MARKETING/ANALYTICS/PERSONALIZATION/THIRD_PARTY）
- 「すべて同意」「必要最低限のみ」「設定を保存」ボタン
- `role="dialog"` + フォーカストラップ + `Escape` キー
- 同意がない場合のトラッキングスクリプト非読込

### Phase 8 完了チェックリスト

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | AI チャット: SignalR ストリーミング応答表示 | `/hubs/ai-chat` |
| 2 | AI チャット: エスカレーションボタン + 確認モーダル | §15.12 |
| 3 | AI チャット: エスカレーション後にチャット入力無効化 | UI 状態確認 |
| 4 | AI チャット: `IAsyncDisposable` で SignalR 接続解放 | メモリリーク防止 |
| 5 | AI チャットレスポンス: `HtmlSanitizer` でサニタイズ | §16.9 XSS 対策 |
| 6 | 在庫リアルタイム: SignalR `/hubs/stock` + グループ参加/離脱 | §16.3 |
| 7 | 注文ステータス: SignalR `/hubs/orders` + ポーリングフォールバック | §16.3 |
| 8 | Cookie 同意バナー: 4 カテゴリトグル + 3 ボタン | §15.1 |
| 9 | Cookie 同意バナー: `role="dialog"` + フォーカストラップ | WCAG |
| 10 | ANALYTICS 同意なし → トラッキングスクリプト非読込 | §15.1 |
| 11 | CancellationToken 全 async メソッドに伝搬 | シグネチャ確認 |
| 12 | TODO/FIXME/HACK コメント残存なし | grep 0 件 |

---

## Phase 9: 管理画面（Razor Pages）実装

### 目的

管理画面全 20 画面を Razor Pages（Bootstrap 5 + htmx + Alpine.js）で実装する。RBAC 3 段階ロール（ADMIN/MANAGER/STAFF）に基づく認可制御を含む。

### 対象セクション

- §3.1 管理画面一覧マトリクス（#1-#20）
- §3.2 管理画面共通仕様
- §3.3 管理画面詳細仕様（A16-A20, #2/3 価格タブ, #9 クーポン分析タブ, #11 ポイント分析タブ, #15 メール管理拡張）
- §9 全 API エンドポイント（Admin 系）
- §10 画面遷移図（管理画面 Mermaid）
- §16.5 管理画面ロール別権限仕様

### 作成ファイル一覧

Phase 9 は管理画面が多いため、サブフェーズに分割する:

#### Phase 9-1: 管理画面基盤 + Must Have 画面（#1-#7, #19, #20）

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `AdminPortal/Program.cs` | 更新: 認証/認可ポリシー設定 |
| 2 | `AdminPortal/Pages/Admin/Index.cshtml` | #1 ダッシュボード |
| 3 | `AdminPortal/Pages/Admin/Products/*` | #2 商品管理 + #3 商品登録/編集 + 価格管理タブ |
| 4 | `AdminPortal/Pages/Admin/Categories/*` | #4 カテゴリー管理 |
| 5 | `AdminPortal/Pages/Admin/Inventory/*` | #5 在庫管理 |
| 6 | `AdminPortal/Pages/Admin/Orders/*` | #6 注文管理 + #7 注文詳細 |
| 7 | `AdminPortal/Pages/Admin/Shipments/*` | #19 出荷管理（A19） |
| 8 | `AdminPortal/Pages/Admin/Returns/*` | #20 返品管理（A20） |

#### Phase 9-2: Should/Could Have 画面（#8-#18）

| # | ファイルパス | 内容 |
|---|------------|------|
| 9 | `AdminPortal/Pages/Admin/Users/*` | #8 ユーザー管理 |
| 10 | `AdminPortal/Pages/Admin/Coupons/*` | #9 クーポン管理 + 分析タブ |
| 11 | `AdminPortal/Pages/Admin/Campaigns/*` | #10 キャンペーン管理 |
| 12 | `AdminPortal/Pages/Admin/Points/*` | #11 ポイント管理 + 分析タブ |
| 13 | `AdminPortal/Pages/Admin/Reports/*` | #12 売上レポート + #13 在庫レポート |
| 14 | `AdminPortal/Pages/Admin/SecurityLogs/*` | #14 セキュリティログ |
| 15 | `AdminPortal/Pages/Admin/Mails/*` | #15 メール管理（4 タブ） |
| 16 | `AdminPortal/Pages/Admin/Ai/Forecast/*` | #16 AI 需要予測（A16） |
| 17 | `AdminPortal/Pages/Admin/Ai/Analytics/*` | #17 AI 分析ダッシュボード（A17） |
| 18 | `AdminPortal/Pages/Admin/Ai/Models/*` | #18 AI モデル管理（A18） |

### Phase 9 完了チェックリスト

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | 管理画面全 20 ページが作成済み | §3.1 マトリクス全行 |
| 2 | RBAC 3 段階（ADMIN/MANAGER/STAFF）が §16.5 マトリクス通り | `[Authorize]` ポリシー |
| 3 | 商品管理: 価格管理タブ（通常価格/セール価格/履歴） | §3.3 #2/3 |
| 4 | クーポン管理: 分析タブ（KPI+テーブル+グラフ） | §3.3 #9 |
| 5 | ポイント管理: 分析タブ（月別KPI+グラフ） | §3.3 #11 |
| 6 | メール管理: 4 タブ（テンプレート/テスト送信/送信履歴/統計） | §3.3 #15 |
| 7 | AI 管理 3 画面（需要予測/分析/モデル管理） | §3.3 A16-A18 |
| 8 | 出荷管理: 一覧+作成+ステータス更新+追跡番号更新 | §3.3 A19 |
| 9 | 返品管理: 一覧+詳細+承認/却下+返金連携 | §3.3 A20 |
| 10 | データテーブル: ソート/フィルター/ページネーション | §3.2 共通 UI |
| 11 | 管理画面セッション有効期限 30 分 | §3.2 |
| 12 | 全管理画面で Admin ロール未満はアクセス拒否 | `[Authorize(Roles)]` |
| 13 | CancellationToken 全 async メソッドに伝搬 | シグネチャ確認 |
| 14 | TODO/FIXME/HACK コメント残存なし | grep 0 件 |

---

## Phase 10: 国際化（i18n）・アクセシビリティ・SEO

### 目的

国際化対応（日本語/英語）、WCAG 2.1 Level AA 準拠、SEO 最適化を全画面に適用する。

### 対象セクション

- §7 国際化（i18n）対応（§7.0-§7.3）
- §8.2 アクセシビリティ（WCAG 2.1 Level AA）
- §8.4 SEO
- §16.6 アクセシビリティ要件詳細
- §16.7 国際化詳細仕様

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `Resources/SharedResources.ja.resx` | 日本語リソース（全翻訳キー） |
| 2 | `Resources/SharedResources.en.resx` | 英語リソース（全翻訳キー） |
| 3 | 各 `.razor` ファイル | `@inject IStringLocalizer<SharedResources> L` + 全テキストリテラル → 翻訳キー置換 |
| 4 | `Program.cs` | 更新: `RequestLocalizationMiddleware` 設定 |

### 実装要件

#### i18n（§7 / §16.7）

- `IStringLocalizer<T>` + `.resx` リソースファイル
- キー命名: `{Domain}_{Context}_{Key}`（例: `Cart_Error_OutOfStock`）
- エラーメッセージ: Problem Details `type` URI → 翻訳キー変換
- 通貨: `¥12,800`（JPY, カンマ区切り）
- 日付: `ja-JP` → `YYYY年MM月DD日` / `en-US` → `Apr 03, 2026`
- タイムゾーン: UTC → JST 変換表示

#### アクセシビリティ（§8.2 / §16.6）

- 全ページにランドマーク構造（`<header>`, `<nav>`, `<main>`, `<footer>`）
- スキップリンク
- フォームエラー: `aria-errormessage` + `aria-invalid` + エラー集約
- キーボードナビゲーション: §16.6 全コンポーネント対応
- コントラスト比: テキスト 4.5:1, UI 境界線 3:1
- モーダル: フォーカストラップ + `Escape` キー
- `:focus-visible` スタイル全要素
- `prefers-reduced-motion: reduce` 対応
- カラー非依存情報伝達（テキスト+アイコン併用）

#### SEO（§8.4）

- 商品ページ: `<HeadContent>` で title/description/OGP メタタグ
- JSON-LD 構造化データ（Product, BreadcrumbList, Organization）
- `sitemap.xml` 自動生成

### Phase 10 完了チェックリスト

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | `.resx` ファイルに全翻訳キーが定義（ja/en） | 全テキストリテラル網羅 |
| 2 | 全 `.razor` ファイルでハードコード日本語テキストなし | `IStringLocalizer` 使用 |
| 3 | URL パス方式（`/ja/products`, `/en/products`）動作 | `RequestLocalizationMiddleware` |
| 4 | 通貨表示: `¥12,800` | フォーマット確認 |
| 5 | 日付表示: ja-JP `2026年04月07日` / en-US `Apr 07, 2026` | ロケール別 |
| 6 | タイムゾーン: UTC → JST 変換 | 注文日時等で確認 |
| 7 | 全ページにスキップリンク | `<a href="#main-content">` |
| 8 | 全ページにランドマーク構造 | header/nav/main/footer |
| 9 | フォームエラー: `aria-errormessage` + `aria-invalid` | 全フォーム |
| 10 | モーダル: フォーカストラップ + `Escape` キー | 全モーダル |
| 11 | `:focus-visible` スタイル適用 | 全インタラクティブ要素 |
| 12 | コントラスト比 4.5:1（通常テキスト）/ 3:1（UI 境界線） | 計測 |
| 13 | 商品ページ: `<HeadContent>` メタタグ + JSON-LD | §8.4 |
| 14 | `sitemap.xml` 自動生成 | エンドポイント確認 |
| 15 | `prefers-reduced-motion: reduce` CSS 対応 | メディアクエリ |
| 16 | TODO/FIXME/HACK コメント残存なし | grep 0 件 |

---

## Phase 11: テスト（bUnit / Playwright / axe-core）

### 目的

全コンポーネント・全画面のテストを実装し、分岐カバレッジ 80% 以上を達成する。E2E テスト + アクセシビリティ自動テストを含む。

### 対象セクション

- §8.5 フロントエンドテスト戦略
- §9 テスト規約（test-standards.instructions.md）

### テスト種別と対象

| テスト種別 | フレームワーク | 対象 | カバレッジ目標 |
|---------|-------------|------|-------------|
| 単体テスト | bUnit + xUnit + NSubstitute | 全共有コンポーネント、API クライアント、Store | 分岐カバレッジ 80% |
| 統合テスト | bUnit + `WebApplicationFactory<Program>` | 全ページコンポーネント | 主要フロー 100% |
| E2E テスト | Playwright（`Microsoft.Playwright`） | 購入フロー、ログイン、管理画面 | Must Have 画面の全主要パス |
| アクセシビリティ | axe-core（Playwright 統合） | 全ページ | WCAG 2.1 AA 違反 0 件 |
| パフォーマンス | Lighthouse CI | Core Web Vitals | LCP ≤ 2.5s, INP ≤ 200ms, CLS ≤ 0.1 |

### テスト命名規約（AGENTS.md §9.2 準拠）

```csharp
[Fact]
public async Task Should_DisplayLoginForm_When_PageLoaded()
[Fact]
public async Task Should_ShowMfaInput_When_MfaRequired()
[Fact]
public async Task Should_ShowValidationErrors_When_InvalidInput()
```

### テストファイル一覧（主要）

| # | ファイルパス | 対象 |
|---|------------|------|
| 1 | `Frontend.Tests/Components/Shared/SearchSuggestTests.cs` | 検索サジェスト |
| 2 | `Frontend.Tests/Components/Shared/PaginationTests.cs` | ページネーション |
| 3 | `Frontend.Tests/Components/Shared/OrderStatusBadgeTests.cs` | 注文ステータスバッジ |
| 4 | `Frontend.Tests/Components/Shared/StockBadgeTests.cs` | 在庫バッジ |
| 5 | `Frontend.Tests/Components/Pages/Auth/LoginTests.cs` | ログイン画面 |
| 6 | `Frontend.Tests/Components/Pages/Auth/RegisterTests.cs` | ユーザー登録画面 |
| 7 | `Frontend.Tests/Components/Pages/Products/ListTests.cs` | 商品一覧画面 |
| 8 | `Frontend.Tests/Components/Pages/Products/DetailTests.cs` | 商品詳細画面 |
| 9 | `Frontend.Tests/Components/Pages/Checkout/PaymentTests.cs` | チェックアウト（ポイント使用） |
| 10 | `Frontend.Tests/Services/ApiErrorHandlerTests.cs` | エラーハンドリング |
| 11 | `Frontend.Tests/Services/TokenRefreshHandlerTests.cs` | トークンリフレッシュ |
| 12 | `Frontend.Tests/Store/CartStoreTests.cs` | Fluxor カートストア |
| 13 | `Frontend.Tests/E2E/PurchaseFlowTests.cs` | E2E: 購入フロー |
| 14 | `Frontend.Tests/E2E/AuthFlowTests.cs` | E2E: 認証フロー |
| 15 | `Frontend.Tests/Accessibility/AllPagesA11yTests.cs` | axe-core 全ページスキャン |

### Phase 11 完了チェックリスト

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | `dotnet test Services/Frontend.Tests` | 全テスト成功 |
| 2 | 分岐カバレッジ 80% 以上 | `dotnet test --collect:"XPlat Code Coverage"` |
| 3 | 全共有コンポーネントに単体テスト | SearchSuggest, Pagination, OrderStatusBadge, StockBadge 等 |
| 4 | 全 API クライアントに単体テスト | NSubstitute で HttpClient モック |
| 5 | Fluxor ストアに単体テスト（State/Action/Reducer/Effect） | CartStore, AuthStore |
| 6 | E2E テスト: 購入フロー（カート→チェックアウト→完了） | Playwright |
| 7 | E2E テスト: 認証フロー（登録→メール認証→ログイン→ログアウト） | Playwright |
| 8 | アクセシビリティテスト: 全ページ WCAG 2.1 AA 違反 0 件 | axe-core |
| 9 | テストメソッド命名: `Should_X_When_Y` パターン | 全テスト |
| 10 | AAA パターン（Arrange-Act-Assert） | 全テスト |
| 11 | CI 統合: PR に対して単体テスト + a11y テスト実行 | CI パイプライン |
| 12 | TODO/FIXME/HACK コメント残存なし | grep 0 件 |

---

## Phase 12: パフォーマンス最適化・可観測性

### 目的

Core Web Vitals 目標達成、サーバーサイドキャッシュ戦略実装、OpenTelemetry 統合、ヘルスチェック設定を行う。

### 対象セクション

- §6.2 サーバー状態（キャッシュ戦略）
- §8.1 パフォーマンス
- §16.8 パフォーマンス詳細要件

### 実装項目

| # | 項目 | 内容 |
|---|------|------|
| 1 | `IMemoryCache` キャッシュ戦略 | §6.2 / §16.8 の全データ種別（商品 5 分、カート 0 秒、注文 1 分、ポイント 30 秒等） |
| 2 | 画像最適化 | WebP/AVIF 変換、`loading="lazy"` / `loading="eager"`、BlurHash プレースホルダー |
| 3 | レンダリングモード最適化 | Static SSR（商品）、Interactive Server（カート/AI）、Interactive WASM（チェックアウト）、Auto（マイページ） |
| 4 | OpenTelemetry トレーシング | `AddAspNetCoreInstrumentation` + `AddHttpClientInstrumentation` |
| 5 | ヘルスチェック | `/health`（Liveness）+ `/health/ready`（Readiness — API Gateway 疎通） |
| 6 | Serilog 構造化ログ | `CompactJsonFormatter` + メッセージテンプレート |

### Phase 12 完了チェックリスト

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | LCP ≤ 2.5 秒（トップページ） | Lighthouse CI |
| 2 | INP ≤ 200ms | Lighthouse CI |
| 3 | CLS ≤ 0.1 | Lighthouse CI |
| 4 | `IMemoryCache` キャッシュが §6.2 の全データ種別に適用 | コード確認 |
| 5 | ヒーロー画像 `loading="eager"` + カルーセル `loading="lazy"` | HTML 確認 |
| 6 | レンダリングモードが §1.2 の戦略通り | `@rendermode` 確認 |
| 7 | OpenTelemetry トレーシング設定済み | Program.cs |
| 8 | `/health` + `/health/ready` 200 応答 | curl 確認 |
| 9 | Serilog 構造化ログ: メッセージテンプレート使用（文字列補間禁止） | コード確認 |
| 10 | `Console.WriteLine` 使用なし | grep 0 件 |
| 11 | TODO/FIXME/HACK コメント残存なし | grep 0 件 |

---

## Phase 13: Docker / デプロイ準備・最終検証

### 目的

Docker イメージビルド、最終セキュリティチェック、全画面の統合確認を行い、デプロイ可能な状態にする。

### 対象セクション

- AGENTS.md §12.1 フェーズ完了条件
- AGENTS.md §15 / dockerfile-infra.instructions.md
- §13 リスクと対策
- §16.9 フロントエンドセキュリティ対策

### 実装項目

| # | 項目 | 内容 |
|---|------|------|
| 1 | Dockerfile 最終化 | マルチステージビルド + 非 root + HEALTHCHECK + バージョン固定 |
| 2 | CSP ヘッダー確認 | §16.9 Content Security Policy |
| 3 | SRI ハッシュ | 外部スクリプト（Stripe.js）に `integrity` 属性 |
| 4 | セキュリティスキャン | OWASP ZAP / 依存関係脆弱性チェック |
| 5 | 全画面遷移確認 | §10 Mermaid 図の全遷移パスを検証 |

### 最終セキュリティチェック（AGENTS.md §12.4 準拠）

```bash
# 秘密情報のハードコードチェック
grep -r "Password\s*=\s*\"" --include="*.cs" --include="*.razor" Services/Frontend/ Services/AdminPortal/

# Console.WriteLine チェック
grep -r "Console\.Write" --include="*.cs" --include="*.razor" Services/Frontend/ Services/AdminPortal/

# localStorage 使用チェック（JWT 保存禁止）
grep -r "localStorage" --include="*.cs" --include="*.razor" --include="*.js" Services/Frontend/ Services/AdminPortal/

# MarkupString 無検証使用チェック
grep -r "MarkupString" --include="*.cs" --include="*.razor" Services/Frontend/

# target="_blank" に rel="noopener noreferrer" なしチェック
grep -r "target=\"_blank\"" --include="*.razor" Services/Frontend/ | grep -v "noopener"
```

### Phase 13 完了チェックリスト

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | `docker build` 成功 | Frontend / AdminPortal 両方 |
| 2 | Docker イメージ: 非 root ユーザー実行 | `USER skishop` |
| 3 | Docker イメージ: HEALTHCHECK 設定 | `/health` エンドポイント |
| 4 | Docker イメージ: `latest` タグなし | `10.0` 固定バージョン |
| 5 | `dotnet publish -c Release` 成功 | 警告なし |
| 6 | CSP ヘッダーが §16.9 準拠 | レスポンスヘッダー確認 |
| 7 | Stripe.js に SRI ハッシュ付き | `integrity` 属性 |
| 8 | 秘密情報ハードコードなし | grep 0 件 |
| 9 | `Console.WriteLine` 使用なし | grep 0 件 |
| 10 | `localStorage` での JWT 保存なし | grep 0 件 |
| 11 | `MarkupString` 全箇所で `HtmlSanitizer` 経由 | コード確認 |
| 12 | `target="_blank"` に `rel="noopener noreferrer"` 付与 | grep 確認 |
| 13 | 全画面遷移が §10 Mermaid 図通り | 手動確認 |
| 14 | .NET Aspire AppHost で全サービス起動 | `dotnet run --project AppHost` |
| 15 | 全テスト成功 + カバレッジ 80% 以上 | `dotnet test` |
| 16 | WCAG 2.1 AA 違反 0 件 | axe-core |
| 17 | Lighthouse スコア: LCP ≤ 2.5s, INP ≤ 200ms, CLS ≤ 0.1 | Lighthouse CI |
| 18 | TODO/FIXME/HACK コメント残存なし | grep 0 件 |
| 19 | Mock/Stub/仮実装残存なし | `NotImplementedException` が本番コードに 0 件 |
| 20 | プレリリース版パッケージなし | `.csproj` 確認 |

---

## 付録 A: 画面 → フェーズ対応マトリクス

全画面が漏れなくどのフェーズで実装されるかを整理する。

| # | 画面名 | 優先度 | 実装フェーズ |
|---|--------|--------|-----------|
| 1 | トップページ | Must | Phase 4 |
| 2 | 商品一覧 | Must | Phase 4 |
| 3 | 商品詳細 | Must | Phase 4 |
| 4 | カート | Must | Phase 5 |
| 5a | チェックアウト: カート確認 | Must | Phase 5 |
| 5b | チェックアウト: 配送先入力 | Must | Phase 5 |
| 5c | チェックアウト: お支払い | Must | Phase 5 |
| 5d | チェックアウト: 注文確認 | Must | Phase 5 |
| 5e | チェックアウト: 注文完了 | Must | Phase 5 |
| 5g | ゲストチェックアウト | Must | Phase 5 |
| 6 | 注文確認 | Must | Phase 6 |
| 7 | 注文履歴一覧 | Must | Phase 6 |
| 8 | 注文詳細 | Must | Phase 6 |
| 9 | ログイン | Must | Phase 3 |
| 10 | ユーザー登録 | Must | Phase 3 |
| 10a | メール認証待ち | Must | Phase 3 |
| 10b | メール認証結果 | Must | Phase 3 |
| 11 | マイページ | Must | Phase 6 |
| 12 | プロフィール編集 | Should | Phase 6 |
| 13 | パスワード変更 | Should | Phase 6 |
| 14 | 住所管理 | Should | Phase 6 |
| 15 | ウィッシュリスト | Could | Phase 7 |
| 16 | レビュー投稿 | Could | Phase 4 |
| 17 | クーポン一覧 | Should | Phase 7 |
| 18 | ポイント履歴（+ティア） | Should | Phase 7 |
| 19 | AI チャット | Could | Phase 8 |
| 20 | 特定商取引法 | Must | Phase 4（静的ページ） |
| 21 | プライバシーポリシー | Must | Phase 4（静的ページ） |
| 22 | 利用規約 | Must | Phase 4（静的ページ） |
| 23 | パスワードリセット要求 | Should | Phase 3 |
| 24 | パスワードリセット | Should | Phase 3 |
| 25 | 同意管理 | Should | Phase 6 |
| 26 | データエクスポート | Should | Phase 6 |
| 27 | アカウント削除 | Should | Phase 6 |
| 28 | ゲスト注文追跡 | Must | Phase 6 |
| 29 | 返品申請 | Must | Phase 6 |
| 30 | MFA セットアップ | Should | Phase 3 |
| 31 | アクティビティ履歴 | Should | Phase 6 |
| 32 | ユーザー設定 | Should | Phase 6 |

### 管理画面（Razor Pages）

| # | 画面名 | 優先度 | 実装フェーズ |
|---|--------|--------|-----------|
| A1 | ダッシュボード | Must | Phase 9-1 |
| A2 | 商品管理（+価格タブ） | Must | Phase 9-1 |
| A3 | 商品登録/編集 | Must | Phase 9-1 |
| A4 | カテゴリー管理 | Must | Phase 9-1 |
| A5 | 在庫管理 | Must | Phase 9-1 |
| A6 | 注文管理 | Must | Phase 9-1 |
| A7 | 注文詳細 | Must | Phase 9-1 |
| A8 | ユーザー管理 | Should | Phase 9-2 |
| A9 | クーポン管理（+分析タブ） | Should | Phase 9-2 |
| A10 | キャンペーン管理 | Should | Phase 9-2 |
| A11 | ポイント管理（+分析タブ） | Should | Phase 9-2 |
| A12 | 売上レポート | Should | Phase 9-2 |
| A13 | 在庫レポート | Could | Phase 9-2 |
| A14 | セキュリティログ | Should | Phase 9-2 |
| A15 | メール管理（4 タブ） | Should | Phase 9-2 |
| A16 | AI 需要予測 | Should | Phase 9-2 |
| A17 | AI 分析ダッシュボード | Should | Phase 9-2 |
| A18 | AI モデル管理 | Could | Phase 9-2 |
| A19 | 出荷管理 | Must | Phase 9-1 |
| A20 | 返品管理 | Must | Phase 9-1 |

---

## 付録 B: 共通コンポーネント → フェーズ対応マトリクス

| # | コンポーネント | 設計書セクション | 実装フェーズ |
|---|-------------|---------------|-----------|
| 1 | MainLayout（MudLayout ベース） | §16.6 ランドマーク構造 | Phase 2 |
| 2 | NavMenu（ヘッダー + 検索） | §4.6 連動 | Phase 2 |
| 3 | Footer（法的ページリンク） | — | Phase 2 |
| 4 | PaginationComponent | §4.2 / §16.4 | Phase 2 |
| 5 | OrderStatusBadge | §15.6 | Phase 2 |
| 6 | StockBadge | 画面 3 在庫表示ルール | Phase 2 |
| 7 | SearchSuggest | §4.6 | Phase 4 |
| 8 | ProductCard | 画面 1/2 共用 | Phase 4 |
| 9 | SizeGuideModal | 画面 3 | Phase 4 |
| 10 | StepIndicator | 画面 5 チェックアウト | Phase 5 |
| 11 | PointExpiryAlert | §15.11 | Phase 6 |
| 12 | AiChatWidget | 画面 19 + §15.12 | Phase 8 |
| 13 | CookieConsentBanner | §15.1 | Phase 8 |
| 14 | StockRealtimeMonitor | §16.3 | Phase 8 |
| 15 | OrderStatusMonitor | §16.3 | Phase 8 |

---

## 付録 C: API クライアント → フェーズ対応マトリクス

| # | API クライアント | 対象 API セクション | 実装フェーズ |
|---|---------------|-----------------|-----------|
| 1 | ApiGatewayClient（基盤） | §4.4 BFF パターン | Phase 2 |
| 2 | AuthApiClient | §9.1 認証 API | Phase 3 |
| 3 | ProductApiClient | §9.3 商品 API | Phase 4 |
| 4 | AiApiClient | §9.8 AI API | Phase 4（レコメンデーション）/ Phase 8（チャット） |
| 5 | CartApiClient | §9.4 カート API | Phase 5 |
| 6 | OrderApiClient | §9.5 注文 API | Phase 5（注文作成）/ Phase 6（注文一覧・詳細） |
| 7 | UserApiClient | §9.2 ユーザー API | Phase 6 |
| 8 | PointApiClient | §9.7 ポイント API | Phase 7 |
| 9 | CouponApiClient | §9.6 クーポン API | Phase 5（検証）/ Phase 7（一覧） |
| 10 | PaymentApiClient | §9.9 決済 API | Phase 5（定義）/ Phase 9（返金処理） |

---

## 付録 D: 設計書セクション → フェーズ対応マトリクス

設計書 `front-end-design.md` の全セクションがどのフェーズでカバーされるかを整理する。

| セクション | 内容 | 実装フェーズ |
|-----------|------|-----------|
| §1 プロジェクト概要 | 技術スタック・サービス一覧 | Phase 1 |
| §2.1 画面一覧マトリクス | 全画面定義 | 付録 A で対応 |
| §2.2 各画面詳細（画面 1-3） | トップ・商品一覧・詳細 | Phase 4 |
| §2.2 各画面詳細（画面 4） | カート | Phase 5 |
| §2.2 各画面詳細（画面 5） | チェックアウト 5 ステップ | Phase 5 |
| §2.2 各画面詳細（画面 9-10） | ログイン・登録 | Phase 3 |
| §2.2 各画面詳細（画面 10a-10b） | メール認証 | Phase 3 |
| §2.2 各画面詳細（ゲスト購入） | ゲストチェックアウト | Phase 5 |
| §2.2 各画面詳細（法的ページ） | 特商法・プラポリ・規約 | Phase 4 |
| §2.2 各画面詳細（画面 11） | マイページ | Phase 6 |
| §3 管理画面一覧 | #1-#20 | Phase 9 |
| §3.3 管理画面詳細仕様 | A16-A20 + タブ追加 | Phase 9 |
| §4.1 エラーハンドリング | RFC 9457 | Phase 2 |
| §4.2 ページネーション | 共通コンポーネント | Phase 2 |
| §4.3 グレースフルデグラデーション | フォールバック戦略 | Phase 2（定義）/ 各フェーズ（実装） |
| §4.4 API 通信共通設定 | BFF パターン | Phase 2 |
| §4.5 レスポンシブデザイン | ブレークポイント | Phase 2 |
| §4.6 検索サジェスト | AI 検索サジェスト | Phase 4 |
| §5 認証・認可フロー | JWT / リフレッシュ / OAuth | Phase 3 |
| §6 状態管理設計 | Fluxor / キャッシュ | Phase 2（定義）/ Phase 12（キャッシュ最適化） |
| §7 国際化（i18n） | 日本語・英語 | Phase 10 |
| §8.1 パフォーマンス | Core Web Vitals | Phase 12 |
| §8.2 アクセシビリティ | WCAG 2.1 AA | Phase 10 |
| §8.3 ブラウザ対応 | Chrome/Firefox/Safari/Edge | Phase 13（最終確認） |
| §8.4 SEO | メタタグ・構造化データ | Phase 10 |
| §8.5 テスト戦略 | bUnit / Playwright / axe-core | Phase 11 |
| §9 API エンドポイント一覧 | 全 API | 各フェーズで API クライアント実装 |
| §10 画面遷移図 | Mermaid | Phase 13（最終確認） |
| §11 MoSCoW 優先順位 | Must/Should/Could/Won't | フェーズ順序の根拠 |
| §12 OpenAPI 参照先 | Swagger | Phase 2（開発中参照） |
| §13 リスクと対策 | セキュリティ・パフォーマンス | Phase 13（最終確認） |
| §14 エスカレーション項目 | バックエンド調整 | 各フェーズで確認 |
| §15.1 Cookie 同意管理バナー | GDPR 準拠 | Phase 8 |
| §15.2 同意管理・データエクスポート・アカウント削除 | 画面 25-27 | Phase 6 |
| §15.3 再注文機能 | 画面 7/8 | Phase 6 |
| §15.4 注文キャンセル確認 + 配送追跡 | 画面 8 拡張 | Phase 6 |
| §15.4a 返品申請画面 | 画面 29 | Phase 6 |
| §15.4b MFA セットアップ | 画面 30 | Phase 3 |
| §15.4c アクティビティ履歴 | 画面 31 | Phase 6 |
| §15.4d ユーザー設定 | 画面 32 | Phase 6 |
| §15.5 ゲスト注文追跡 | 画面 28 | Phase 6 |
| §15.6 注文ステータス表示マッピング | 共通コンポーネント | Phase 2 |
| §15.7 タッチターゲットサイズ | CSS | Phase 2 |
| §15.8 配送料・消費税表示 | チェックアウト | Phase 5 |
| §15.9 画面一覧マトリクス（追記） | 画面 25-32 | 付録 A で対応 |
| §15.10 API エンドポイント一覧（追記） | §9.10-9.13 | 各フェーズ |
| §15.11 ティア情報表示 | 画面 18 補完 | Phase 7 |
| §15.12 AI チャットエスカレーション | 画面 19 補完 | Phase 8 |
| §16.1 エラーハンドリング詳細 | §4.1 補完 | Phase 2 |
| §16.2 認証・トークン管理詳細 | §5 補完（OAuth 含む） | Phase 3 |
| §16.3 リアルタイム通知 | SignalR | Phase 8 |
| §16.4 ページネーション共通コンポーネント | §4.2 補完 | Phase 2 |
| §16.5 管理画面ロール別権限 | §3 補完 | Phase 9 |
| §16.6 アクセシビリティ詳細 | §8.2 補完 | Phase 10 |
| §16.7 i18n 詳細 | §7 補完 | Phase 10 |
| §16.8 パフォーマンス詳細 | §8.1 補完 | Phase 12 |
| §16.9 セキュリティ対策 | XSS/CSRF/CSP/SRI | Phase 2（基盤）/ Phase 13（最終確認） |
