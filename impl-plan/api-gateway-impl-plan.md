# ApiGateway フェーズ別実装計画書

> **対象サービス**: ApiGateway（ステートレス YARP リバースプロキシ）
> **ポート**: 8080
> **DB / Kafka / EF Core**: 使用しない
> **設計書**: `design-docs/api-gateway-design.md`
> **規約**: AGENTS.md / `.github/instructions/` 配下のインストラクションファイル群

---

## 目次

1. [Phase 1: プロジェクト基盤構築](#phase-1-プロジェクト基盤構築)
2. [Phase 2: YARP リバースプロキシ設定](#phase-2-yarp-リバースプロキシ設定)
3. [Phase 3: 認証・認可フィルタ](#phase-3-認証認可フィルタ)
4. [Phase 4: セキュリティミドルウェア](#phase-4-セキュリティミドルウェア)
5. [Phase 5: レート制限](#phase-5-レート制限)
6. [Phase 6: 可観測性](#phase-6-可観測性)
7. [Phase 7: 耐障害性](#phase-7-耐障害性)
8. [Phase 8: エラーハンドリング & ログ](#phase-8-エラーハンドリング--ログ)
9. [Phase 9: 最終統合 & デプロイ準備](#phase-9-最終統合--デプロイ準備)

---

## Phase 1: プロジェクト基盤構築

### 目的

ApiGateway プロジェクトの骨格を構築する。ビルド可能な最小構成を作成し、以降のフェーズの土台とする。

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `ApiGateway/ApiGateway.csproj` | プロジェクト定義（YARP, Serilog, OpenTelemetry 等） |
| 2 | `ApiGateway/Program.cs` | エントリポイントスケルトン（最小構成） |
| 3 | `ApiGateway/appsettings.json` | 共通設定（安全なデフォルト値のみ、秘密情報禁止） |
| 4 | `ApiGateway/appsettings.Development.json` | 開発環境設定 |
| 5 | `ApiGateway/appsettings.Production.json` | 本番環境設定（環境変数参照のみ） |
| 6 | `ApiGateway/Dockerfile` | マルチステージビルド + 非 root 実行 |
| 7 | `ApiGateway/.dockerignore` | ビルド不要ファイルの除外 |

### 1.1 ApiGateway.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <!-- YARP リバースプロキシ -->
    <PackageReference Include="Yarp.ReverseProxy" Version="2.*" />

    <!-- 認証 -->
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.*" />

    <!-- 耐障害性 -->
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="9.*" />

    <!-- キャッシュ（レート制限カウンター用・認証キャッシュ） -->
    <PackageReference Include="StackExchange.Redis" Version="2.*" />

    <!-- メッセージング（認証キャッシュ無効化イベント受信: §12 準拠） -->
    <PackageReference Include="Confluent.Kafka" Version="2.*" />

    <!-- Azure 認証・Key Vault 連携（§2 技術スタック準拠） -->
    <PackageReference Include="Azure.Identity" Version="1.*" />
    <PackageReference Include="Azure.Security.KeyVault.Secrets" Version="4.*" />

    <!-- ロギング -->
    <PackageReference Include="Serilog.AspNetCore" Version="8.*" />
    <PackageReference Include="Serilog.Sinks.Console" Version="6.*" />
    <PackageReference Include="Serilog.Formatting.Compact" Version="3.*" />

    <!-- OpenTelemetry -->
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.*" />

    <!-- ヘルスチェック -->
    <PackageReference Include="AspNetCore.HealthChecks.Redis" Version="9.*" />
  </ItemGroup>
</Project>
```

> **注記**: FluentValidation は不要（設計書 §22 — API Gateway はリクエストボディのバリデーションを下流サービスに委譲）。
> EF Core / Npgsql は不要（DB 不使用）。Confluent.Kafka は認証キャッシュ無効化イベント受信用（設計書 §12 準拠）。
> Azure.Identity / Azure.Security.KeyVault.Secrets は秘密情報管理用（設計書 §2 技術スタック準拠）。

### 1.2 ディレクトリ構造

```
ApiGateway/
├── ApiGateway.csproj
├── Program.cs
├── Infrastructure/
│   ├── Middleware/
│   │   ├── CorrelationIdMiddleware.cs
│   │   ├── CorrelationIdMiddlewareExtensions.cs
│   │   ├── SecurityHeadersMiddleware.cs
│   │   ├── SecurityHeadersMiddlewareExtensions.cs
│   │   └── ResponseTimeMiddleware.cs
│   ├── HealthChecks/
│   │   └── BackendServicesHealthCheck.cs
│   └── Caching/
│       └── AuthCacheInvalidationConsumer.cs
├── appsettings.json
├── appsettings.Development.json
├── appsettings.Production.json
├── Dockerfile
└── .dockerignore
```

### 1.3 Program.cs スケルトン

```csharp
var builder = WebApplication.CreateBuilder(args);

// YARP リバースプロキシ（最小構成）
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapReverseProxy();
app.MapGet("/health", () => Results.Ok(new { Status = "UP" }));

app.Run();
```

### 1.4 appsettings.json（安全なデフォルト値）

```json
{
  "AllowedHosts": "*",
  "DetailedErrors": false,
  "Kestrel": {
    "AddServerHeader": false
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "SkiShop": "Information",
      "Yarp.ReverseProxy": "Information"
    }
  },
  "ReverseProxy": {
    "Routes": {},
    "Clusters": {}
  }
}
```

### 1.5 appsettings.Development.json

```json
{
  "DetailedErrors": true,
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Debug",
      "SkiShop": "Debug",
      "Yarp.ReverseProxy": "Debug"
    }
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000", "http://localhost:5173"]
  }
}
```

### 1.6 Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ApiGateway/ApiGateway.csproj", "ApiGateway/"]
RUN dotnet restore "ApiGateway/ApiGateway.csproj"
COPY . .
WORKDIR "/src/ApiGateway"
RUN dotnet publish "ApiGateway.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN groupadd -r skishop && useradd -r -g skishop -d /app -s /sbin/nologin skishop
COPY --from=build --chown=skishop:skishop /app/publish .
USER skishop

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "ApiGateway.dll"]
```

### 1.7 .dockerignore

```
**/bin/
**/obj/
.git/
.github/
*.md
*.sln.DotSettings
.idea/
.vs/
design-docs/
impl-plan/
```

### Phase 1 完了チェックリスト

- [ ] `dotnet build ApiGateway/ApiGateway.csproj` — 警告なし成功
- [ ] `dotnet run --project ApiGateway` — 起動確認（`/health` で 200 応答）
- [ ] .csproj: `TreatWarningsAsErrors=true`, `Nullable=enable`, `TargetFramework=net10.0`
- [ ] .csproj: プレリリース版パッケージなし（`-preview`, `-beta`, `-rc` なし）
- [ ] .csproj: EF Core / Npgsql / FluentValidation パッケージが含まれていないこと
- [ ] .csproj: `Confluent.Kafka` が含まれていること（認証キャッシュ無効化イベント受信用: 設計書 §12）
- [ ] .csproj: `Azure.Identity`, `Azure.Security.KeyVault.Secrets` が含まれていること（設計書 §2）
- [ ] appsettings.json: 秘密情報なし（パスワード、API キー、接続文字列なし）
- [ ] appsettings.json: `DetailedErrors: false`, `AddServerHeader: false`
- [ ] Dockerfile: マルチステージビルド、非 root ユーザー、HEALTHCHECK あり
- [ ] Dockerfile: ベースイメージに `latest` タグなし（`10.0` 固定）
- [ ] .dockerignore: `bin/`, `obj/`, `.git/`, `*.md` 除外
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" ApiGateway/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 2: YARP リバースプロキシ設定

### 目的

設計書 §4（API ルートテーブル）および §13（YARP ReverseProxy 設定例）に基づき、全バックエンドサービスへのルーティングを構成する。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `ApiGateway/appsettings.json` | 更新 | `ReverseProxy` セクションに全ルート・クラスターを定義 |
| 2 | `ApiGateway/Program.cs` | 更新 | YARP + Correlation ID Transform 登録 |

### 2.1 YARP ルート定義（全 18 ルート）

設計書 §4 のルートテーブルおよび §13 の JSON 設定に基づく。

| ルート名 | パスパターン | クラスター | 認可ポリシー | メソッド制限 |
|---------|------------|----------|------------|-----------|
| `auth-route` | `/api/auth/{**catch-all}` | `auth-cluster` | `anonymous` | — |
| `users-route` | `/api/users/{**catch-all}` | `user-cluster` | `default` | — |
| `products-route` | `/api/products/{**catch-all}` | `inventory-cluster` | `anonymous` | — |
| `inventory-route` | `/api/inventory/{**catch-all}` | `inventory-cluster` | `AdminOrManager` | — |
| `orders-route` | `/api/orders/{**catch-all}` | `sales-cluster` | `default` | — |
| `reports-route` | `/api/reports/{**catch-all}` | `sales-cluster` | `AdminOrManager` | — |
| `cart-items-route` | `/api/cart/items` | `payment-cart-cluster` | `anonymous` | `POST` |
| `cart-checkout-route` | `/api/cart/checkout` | `payment-cart-cluster` | `default` | — |
| `cart-route` | `/api/cart/{**catch-all}` | `payment-cart-cluster` | `default` | — |
| `payments-route` | `/api/payments/{**catch-all}` | `payment-cart-cluster` | `default` | — |
| `points-route` | `/api/points/{**catch-all}` | `points-cluster` | `default` | — |
| `coupons-public-route` | `/api/coupons` | `coupons-cluster` | `anonymous` | `GET` |
| `coupons-apply-route` | `/api/coupons/apply` | `coupons-cluster` | `default` | `POST` |
| `coupons-route` | `/api/coupons/{**catch-all}` | `coupons-cluster` | `default` | — |
| `ai-recommendations-route` | `/api/recommendations/{**catch-all}` | `ai-cluster` | `anonymous` | — |
| `ai-search-route` | `/api/search/{**catch-all}` | `ai-cluster` | `anonymous` | — |
| `ai-chat-route` | `/api/chat/{**catch-all}` | `ai-cluster` | `default` | — |
| `ai-analytics-route` | `/api/analytics/{**catch-all}` | `ai-cluster` | `AdminOrManager` | — |

### 2.2 YARP クラスター定義（全 8 クラスター）

| クラスター名 | サービス | アドレス | ロードバランシング | ヘルスチェック |
|------------|---------|---------|----------------|------------|
| `auth-cluster` | AuthService | `http://auth-service:5001` | RoundRobin | Active, 30s, `/health` |
| `user-cluster` | UserManagementService | `http://user-management-service:5002` | RoundRobin | Active, 30s, `/health` |
| `inventory-cluster` | InventoryManagementService | `http://inventory-management-service:5003` | RoundRobin | Active, 30s, `/health` |
| `sales-cluster` | SalesManagementService | `http://sales-management-service:5004` | RoundRobin | Active, 30s, `/health` |
| `payment-cart-cluster` | PaymentCartService | `http://payment-cart-service:5005` | RoundRobin | Active, 30s, `/health` |
| `coupons-cluster` | CouponService | `http://coupon-service:5006` | RoundRobin | Active, 30s, `/health` |
| `points-cluster` | PointService | `http://point-service:5007` | RoundRobin | Active, 30s, `/health` |
| `ai-cluster` | AiSupportService | `http://ai-support-service:5009` | RoundRobin | Active, 30s, `/health` |

> **注記**: MailSendService（5008）は内部専用サービス（Kafka 駆動のみ）。API Gateway からのルーティングは不要。

### 2.3 パス変換ルール

全ルートに `PathRemovePrefix: /api` を適用し、`/api/auth/login` → `/auth/login` に変換する。

### 2.4 YARP Transform — Correlation ID 転送

```csharp
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(builderContext =>
    {
        builderContext.AddRequestTransform(transformContext =>
        {
            if (transformContext.HttpContext.Items.TryGetValue("CorrelationId", out var correlationId)
                && correlationId is string id)
            {
                transformContext.ProxyRequest.Headers.Remove("X-Correlation-Id");
                transformContext.ProxyRequest.Headers.Add("X-Correlation-Id", id);
            }
            return ValueTask.CompletedTask;
        });
    });
```

### Phase 2 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] appsettings.json: 全 18 ルートが定義されていること
- [ ] appsettings.json: 全 8 クラスターが定義されていること
- [ ] 各クラスター: `LoadBalancingPolicy: RoundRobin` が設定されていること
- [ ] 各クラスター: `HealthCheck.Active.Enabled: true` が設定されていること
- [ ] 全ルート: `Transforms: [{ "PathRemovePrefix": "/api" }]` が設定されていること
- [ ] `auth-route`, `products-route`, `ai-recommendations-route`, `ai-search-route`: `AuthorizationPolicy: anonymous`
- [ ] `inventory-route`, `reports-route`, `ai-analytics-route`: `AuthorizationPolicy: AdminOrManager`
- [ ] `cart-items-route`: `Methods: ["POST"]` + `anonymous`
- [ ] `coupons-public-route`: `Methods: ["GET"]` + `anonymous`
- [ ] `coupons-apply-route`: `Methods: ["POST"]` + `default`
- [ ] MailSendService（5008）のルート・クラスターが存在しないこと
- [ ] クラスターアドレスがハードコードではなく設定ファイルベースであること（.NET Aspire 連携準備）
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" ApiGateway/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 3: 認証・認可フィルタ

### 目的

設計書 §6（セキュリティ設定）および §14（Program.cs 構成設計）に基づき、JWT Bearer 認証と認可ポリシーを構成する。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `ApiGateway/Program.cs` | 更新 | 認証・認可の DI 登録 |
| 2 | `ApiGateway.Tests/ApiGateway.Tests.csproj` | 作成 | テストプロジェクト定義 |
| 3 | `ApiGateway.Tests/Fixtures/GatewayWebApplicationFactory.cs` | 作成 | テスト用 WebApplicationFactory + TestAuthHandler |
| 4 | `ApiGateway.Tests/Security/AuthenticationFilterTests.cs` | 作成 | 認証・認可テスト |

### 3.1 認証設定

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });
```

### 3.2 認可ポリシー

| ポリシー名 | 適用対象 | 条件 |
|-----------|---------|------|
| `AdminOnly` | 管理者専用エンドポイント | `RequireRole("Admin")` |
| `AdminOrManager` | 管理者・マネージャ用 | `RequireRole("Admin", "Manager")` |
| `FallbackPolicy` | 全エンドポイント（デフォルト） | `RequireAuthenticatedUser()` |
| `anonymous` | YARP ルート定義 | `AuthorizationPolicy: "anonymous"` で Fallback 除外 |

### 3.3 エンドポイント別認可マッピング

| カテゴリ | ルート | ポリシー |
|---------|-------|---------|
| **公開（anonymous）** | `/api/auth/**`, `/api/products/**`, `/api/recommendations/**`, `/api/search/**`, `GET /api/coupons`, `POST /api/cart/items`, `/health`, `/health/ready` | 認証不要 |
| **認証必須（default）** | `/api/users/**`, `/api/orders/**`, `/api/cart/checkout`, `/api/cart/**`, `/api/payments/**`, `/api/points/**`, `/api/chat/**`, `POST /api/coupons/apply`, `/api/coupons/**` | `FallbackPolicy`（認証済みユーザー） |
| **管理者（AdminOrManager）** | `/api/inventory/**`, `/api/reports/**`, `/api/analytics/**` | Admin または Manager ロール |

### 3.4 テスト仕様

| テストメソッド | カテゴリ | 検証内容 |
|-------------|---------|---------|
| `Should_AllowAccess_When_AnonymousEndpointRequested` | Security | `/api/auth/login`, `/api/products`, `/api/recommendations/popular`, `/health`, `/health/ready` に未認証でアクセスし 401 でないこと |
| `Should_Return401_When_AuthenticatedEndpointAccessedWithoutToken` | Security | `/api/orders`, `/api/users/profile`, `/api/points/balance`, `/api/cart/checkout` に未認証でアクセスし 401 であること |
| `Should_Return403_When_NonAdminAccessesAdminEndpoint` | Security | `/api/inventory/stock` に User ロールでアクセスし 403 であること |
| `Should_AllowAccess_When_AdminAccessesAdminEndpoint` | Security | `/api/inventory/stock` に Admin ロールでアクセスし 401/403 でないこと |

### Phase 3 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] `dotnet test --filter "Category=Security"` — 全テスト通過
- [ ] JWT 認証: `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey` がすべて `true`
- [ ] JWT 認証: `ClockSkew = TimeSpan.FromMinutes(5)`
- [ ] 認可: `FallbackPolicy` に `RequireAuthenticatedUser()` が設定されていること
- [ ] 認可: `AdminOnly`, `AdminOrManager` ポリシーが定義されていること
- [ ] YARP ルート: `AuthorizationPolicy: "anonymous"` が設定されたルートと設計書 §4 が一致すること
- [ ] テスト: Should_X_When_Y 命名規約準拠
- [ ] テスト: AAA パターン（Arrange / Act / Assert）遵守
- [ ] テスト: Shouldly アサーション使用
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" ApiGateway/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 4: セキュリティミドルウェア

### 目的

設計書 §6（セキュリティレスポンスヘッダー）および §20（SecurityHeadersMiddleware 完全実装）に基づき、セキュリティヘッダーと CORS を構成する。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `ApiGateway/Infrastructure/Middleware/SecurityHeadersMiddleware.cs` | 作成 | セキュリティヘッダーミドルウェア |
| 2 | `ApiGateway/Infrastructure/Middleware/SecurityHeadersMiddlewareExtensions.cs` | 作成 | `UseSecurityHeaders()` 拡張メソッド |
| 3 | `ApiGateway/Program.cs` | 更新 | CORS 設定 + Kestrel 設定 + ForwardedHeaders + ミドルウェア登録 |
| 4 | `ApiGateway/Infrastructure/Middleware/ResponseTimeMiddleware.cs` | 作成 | `X-Response-Time` レスポンスヘッダー付与（設計書 §5） |
| 5 | `ApiGateway.Tests/Middleware/SecurityHeadersMiddlewareTests.cs` | 作成 | セキュリティヘッダーテスト |

### 4.1 セキュリティレスポンスヘッダー（設計書 §6 / §20 準拠）

| ヘッダー | 値 | 目的 |
|---------|---|------|
| `X-Content-Type-Options` | `nosniff` | MIME タイプスニッフィング防止 |
| `X-Frame-Options` | `DENY` | クリックジャッキング防止 |
| `Content-Security-Policy` | `default-src 'self'` | XSS 防止 |
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains` | HTTPS 強制 |
| `X-XSS-Protection` | `0` | ブラウザ組み込み XSS フィルタ無効化（CSP で代替） |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | リファラー情報の制限 |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` | ブラウザ機能の制限 |
| `Cache-Control` | `no-store` | API レスポンスのキャッシュ防止 |
| `Pragma` | `no-cache` | HTTP/1.0 互換キャッシュ防止 |
| `Server` | 削除 | サーバー情報の非公開 |

### 4.2 CORS 設定（設計書 §6 準拠）

| 設定項目 | 本番値 | 開発値 |
|---------|-------|-------|
| `allowedOrigins` | `["https://www.skieshop.com"]` | `["http://localhost:3000", "http://localhost:5173"]` |
| `allowedMethods` | `["GET", "POST", "PUT", "DELETE", "OPTIONS"]` | 同左 |
| `allowedHeaders` | `["Content-Type", "Authorization", "X-Correlation-Id", "Accept-Language", "X-Request-Id"]` | 同左 |
| `maxAge` | `3600` 秒 | 同左 |

> **禁止**: `AllowAnyOrigin()` のワイルドカード CORS は使用禁止。

### 4.3 Kestrel セキュリティ設定

```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 5 * 1024 * 1024; // 5MB（設計書 §5）
    options.AddServerHeader = false;
});
```

### 4.4 ForwardedHeaders 設定（設計書 §5 準拠）

ロードバランサー / リバースプロキシ経由のリクエストで正しいクライアント IP・プロトコルを取得するために `ForwardedHeaders` を構成する:

```csharp
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedPrefix;
});

// ミドルウェアパイプライン: UseExceptionHandler の直後に配置
app.UseForwardedHeaders();
```

### 4.5 X-Response-Time ヘッダー（設計書 §5 準拠）

パフォーマンスメトリクス用に `X-Response-Time` ヘッダーをレスポンスに付与する:

```csharp
// Infrastructure/Middleware/ResponseTimeMiddleware.cs
public sealed class ResponseTimeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var startTime = Stopwatch.GetTimestamp();
        context.Response.OnStarting(() =>
        {
            var elapsed = Stopwatch.GetElapsedTime(startTime);
            context.Response.Headers.Append("X-Response-Time", $"{elapsed.TotalMilliseconds:F1}ms");
            return Task.CompletedTask;
        });
        await next(context);
    }
}
```

### 4.6 テスト仕様

| テストメソッド | カテゴリ | 検証内容 |
|-------------|---------|---------|
| `Should_IncludeAllSecurityHeaders_When_AnyResponseReturned` | Security | 全 7 セキュリティヘッダーの存在と値を検証 |
| `Should_IncludeCacheControlNoStore_When_ApiResponseReturned` | Security | `Cache-Control: no-store` の検証 |
| `Should_NotIncludeServerHeader_When_ResponseReturned` | Security | `Server` ヘッダーが存在しないこと |
| `Should_IncludePermissionsPolicy_When_AnyResponseReturned` | Security | `Permissions-Policy` ヘッダーの存在と値を検証 |
| `Should_IncludeResponseTime_When_AnyResponseReturned` | Security | `X-Response-Time` ヘッダーの存在を検証（設計書 §5） |

### Phase 4 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] `dotnet test --filter "Category=Security"` — 全テスト通過
- [ ] SecurityHeadersMiddleware: 全 7 ヘッダー + Cache-Control + Pragma が設定されていること
- [ ] SecurityHeadersMiddleware: ヘッダー値が定数化されていること（リクエストごとのアロケーション回避）
- [ ] CORS: `AllowAnyOrigin()` が使用されていないこと
- [ ] CORS: `WithOrigins()` で許可オリジンが明示されていること
- [ ] CORS: 開発環境用オリジンは `appsettings.Development.json` の `Cors:AllowedOrigins` で管理
- [ ] Kestrel: `AddServerHeader = false`
- [ ] Kestrel: `MaxRequestBodySize = 5MB`
- [ ] ForwardedHeaders: `XForwardedFor`, `XForwardedProto`, `XForwardedPrefix` が設定されていること（設計書 §5）
- [ ] ForwardedHeaders: `UseForwardedHeaders()` が `UseExceptionHandler()` の直後に配置されていること
- [ ] X-Response-Time: レスポンスヘッダーにリクエスト処理時間が付与されること（設計書 §5）
- [ ] `UseHsts()` と `UseHttpsRedirection()` が配置されていること
- [ ] 禁止事項: `Console.WriteLine` なし、秘密情報のハードコードなし
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" ApiGateway/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 5: レート制限

### 目的

設計書 §8（レート制限ポリシー）に基づき、ASP.NET Core Rate Limiter を構成する。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `ApiGateway/Program.cs` | 更新 | `AddRateLimiter` 設定 |
| 2 | `ApiGateway.Tests/RateLimiting/RateLimitingTests.cs` | 作成 | レート制限テスト |

### 5.1 レート制限ポリシー一覧（設計書 §8 準拠）

| カテゴリ | 対象 | アルゴリズム | 閾値 | キーリゾルバ | 備考 |
|---------|------|-----------|------|----------|------|
| IP ベース | 未認証リクエスト | Token Bucket | 60 req/min | IP アドレス（`X-Forwarded-For` 検証） | DDoS 軽減 |
| ユーザーベース | 認証済みリクエスト | Token Bucket | 120 req/min | ユーザー ID（JWT `sub` クレーム） | 一般ユーザー制限 |
| エンドポイント別 | `POST /auth/login` | Fixed Window | 5 req/min/IP | IP アドレス | ブルートフォース防止 |
| エンドポイント別 | `POST /checkout/*` | Fixed Window | 10 req/min/user | ユーザー ID | 決済 API の過負荷防止 |
| エンドポイント別 | `GET /products` | Token Bucket | 300 req/min/IP | IP アドレス | スクレイピング防止 |

### 5.2 429 レスポンス設計

- HTTP ステータス: `429 Too Many Requests`
- `Retry-After` ヘッダー付与（秒数）
- レスポンスボディ: RFC 9457 Problem Details 形式

```json
{
  "type": "https://tools.ietf.org/html/rfc6585#section-4",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "レート制限を超過しました。しばらく待ってからリトライしてください。"
}
```

### 5.3 Redis 分散カウンター（設計書 §8 / §12 準拠）

レート制限カウンターは Redis に保存し、API Gateway の全レプリカで共有する。

| キーパターン | 用途 | TTL |
|------------|------|-----|
| `rate:{ip}:{endpoint}` | IP ベースレート制限 | 1 分 |
| `rate:{userId}:{endpoint}` | ユーザーベースレート制限 | 1 分 |

- `X-Forwarded-For` ヘッダーからクライアント IP を取得する際、プロキシチェーンの最初の IP（最左端）を使用する
- 信頼できるプロキシの `KnownProxies` / `KnownNetworks` を設定し、IP スプーフィングを防止する

### 5.3 テスト仕様

| テストメソッド | カテゴリ | 検証内容 |
|-------------|---------|---------|
| `Should_Return429WithRetryAfter_When_RateLimitExceeded` | RateLimit | ログインエンドポイントで 5 req/min 超過時に 429 + Retry-After |
| `Should_AllowRequest_When_WithinRateLimit` | RateLimit | 制限内のリクエストが 429 にならないこと |
| `Should_IncludeRetryAfterHeader_When_RateLimited` | RateLimit | 429 レスポンスに `Retry-After` ヘッダーが含まれること |

### Phase 5 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] `dotnet test --filter "Category=RateLimit"` — 全テスト通過
- [ ] レート制限: `RejectionStatusCode = 429` が設定されていること
- [ ] レート制限: `OnRejected` で `Retry-After` ヘッダーが付与されること
- [ ] レート制限: `OnRejected` で RFC 9457 形式の JSON レスポンスが返却されること
- [ ] レート制限: ログインエンドポイント — Fixed Window 5 req/min/IP
- [ ] レート制限: 決済エンドポイント — Fixed Window 10 req/min/user
- [ ] レート制限: 商品一覧 — Token Bucket 300 req/min/IP
- [ ] レート制限: Redis 分散カウンターが有効であること（設計書 §8 / §12）
- [ ] レート制限: `X-Forwarded-For` からのクライアント IP 取得が正しく動作すること
- [ ] ミドルウェア順序: `UseRateLimiter()` が `UseAuthorization()` の後に配置されていること
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" ApiGateway/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 6: 可観測性

### 目的

設計書 §9（監視と可観測性）および §19（Correlation ID ミドルウェア完全実装）に基づき、分散トレーシング・構造化ログ・ヘルスチェックを構成する。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `ApiGateway/Infrastructure/Middleware/CorrelationIdMiddleware.cs` | 作成 | Correlation ID ミドルウェア |
| 2 | `ApiGateway/Infrastructure/Middleware/CorrelationIdMiddlewareExtensions.cs` | 作成 | `UseCorrelationId()` 拡張メソッド |
| 3 | `ApiGateway/Infrastructure/HealthChecks/BackendServicesHealthCheck.cs` | 作成 | 下流サービスヘルスチェック |
| 4 | `ApiGateway/Program.cs` | 更新 | Serilog + OpenTelemetry + ヘルスチェック登録 |
| 5 | `ApiGateway.Tests/Middleware/CorrelationIdMiddlewareTests.cs` | 作成 | Correlation ID テスト |

### 6.1 Correlation ID ミドルウェア（設計書 §19 準拠）

| 処理 | 詳細 |
|------|------|
| 受信チェック | リクエストヘッダー `X-Correlation-Id` が存在すればその値を採用 |
| 新規生成 | 存在しなければ `Guid.NewGuid().ToString()` で生成 |
| レスポンス付与 | `context.Response.OnStarting()` で `X-Correlation-Id` をレスポンスヘッダーに追加 |
| ログコンテキスト | `LogContext.PushProperty("CorrelationId", correlationId)` で Serilog に付与 |
| HttpContext 格納 | `context.Items["CorrelationId"]` に格納（YARP Transform から参照） |
| バックエンド転送 | YARP `AddRequestTransform` で下流サービスへ `X-Correlation-Id` を転送 |

### 6.2 ヘルスチェック設計（設計書 §21 準拠）

| エンドポイント | 種別 | チェック対象 | AllowAnonymous |
|------------|------|-----------|---------------|
| `/health` | Liveness | なし（常に 200） | ✅ |
| `/health/ready` | Readiness | Redis + バックエンドサービス | ✅ |

#### BackendServicesHealthCheck — サービス分類

| 分類 | サービス | Unhealthy 時の影響 |
|------|---------|-----------------|
| **必須（Critical）** | `auth-service`, `inventory-service`, `sales-service`, `payment-cart-service` | Readiness = Unhealthy |
| **非必須（Optional）** | `user-service`, `coupon-service`, `point-service`, `ai-service` | Readiness = Degraded |

### 6.3 Serilog 構造化ログ設定（設計書 §9 準拠）

```csharp
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "ApiGateway")
        .WriteTo.Console(new CompactJsonFormatter()));
```

### 6.4 OpenTelemetry 設定（設計書 §14 準拠）

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("SkiShop.ApiGateway"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation());
```

### 6.5 カスタムゲートウェイメトリクス（設計書 §9 準拠）

以下のカスタムメトリクスを `System.Diagnostics.Metrics` で計装する:

| メトリクス名 | 種別 | タグ | 用途 |
|------------|------|-----|------|
| `gateway.requests.total` | Counter | service, status | サービス・ステータス別リクエスト総数 |
| `gateway.requests.duration` | Histogram | service | パーセンタイル付きリクエスト所要時間 |
| `gateway.circuit_breaker.state` | ObservableGauge | service | サーキットブレーカー状態（Open/Closed/HalfOpen） |
| `gateway.circuit_breaker.calls` | Counter | service, result | サーキットブレーカー呼び出し結果（成功/失敗） |
| `gateway.rate_limiter.limited` | Counter | policy | レート制限発動イベント |
| `gateway.rate_limiter.capacity_used` | ObservableGauge | policy | レート制限容量使用率 |

### 6.6 ヘルスチェック JSON レスポンスライター（設計書 §21.3 準拠）

ヘルスチェックエンドポイントにカスタム `ResponseWriter` を設定し、個別サービスのステータス・応答時間を JSON 形式で返却する:

```csharp
// WriteHealthResponse — JSON 形式のヘルスチェックレスポンス
static Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    var result = new
    {
        status = report.Status.ToString(),
        duration = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            duration = e.Value.Duration.TotalMilliseconds,
            description = e.Value.Description,
            data = e.Value.Data
        })
    };
    return context.Response.WriteAsJsonAsync(result);
}
```

### 6.7 ログパターン仕様（設計書 §9 準拠）

Serilog CompactJsonFormatter で出力される各ログエントリには以下のフィールドを含める:

| フィールド | ソース | 用途 |
|-----------|-------|------|
| `@t` (timestamp) | Serilog 自動 | ISO-8601 形式のタイムスタンプ |
| `@l` (level) | Serilog 自動 | ログレベル |
| `TraceId` | OpenTelemetry 自動 | 分散トレーシング用 |
| `SpanId` | OpenTelemetry 自動 | 分散トレーシング用 |
| `CorrelationId` | CorrelationIdMiddleware | X-Correlation-Id ヘッダーから伝搬 |
| `ServiceName` | Serilog Enrich | `"ApiGateway"` |

### 6.8 テスト仕様

| テストメソッド | カテゴリ | 検証内容 |
|-------------|---------|---------|
| `Should_GenerateCorrelationId_When_HeaderNotProvided` | Middleware | ヘッダー未送信時に UUID 形式の Correlation ID がレスポンスに含まれること |
| `Should_PreserveCorrelationId_When_HeaderProvided` | Middleware | 送信した Correlation ID がそのままレスポンスに返却されること |
| `Should_Return200_When_LivenessEndpointRequested` | HealthCheck | `/health` が常に 200 を返すこと |
| `Should_ReturnJsonResponse_When_ReadinessEndpointRequested` | HealthCheck | `/health/ready` が JSON 形式で応答すること（`status`, `duration`, `checks` フィールド含む） |
| `Should_ReturnDegraded_When_OptionalServiceUnhealthy` | HealthCheck | 非必須サービスが Unhealthy の場合、Readiness が Degraded を返すこと |
| `Should_ReturnUnhealthy_When_CriticalServiceUnhealthy` | HealthCheck | 必須サービスが Unhealthy の場合、Readiness が Unhealthy を返すこと |

### Phase 6 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] `dotnet test --filter "Category=Middleware|Category=HealthCheck"` — 全テスト通過
- [ ] CorrelationIdMiddleware: 専用クラスとして実装（インラインミドルウェアではない）
- [ ] CorrelationIdMiddleware: `LogContext.PushProperty` で Serilog コンテキストに付与
- [ ] CorrelationIdMiddleware: `context.Items["CorrelationId"]` に格納
- [ ] CorrelationIdMiddleware: `context.Response.OnStarting()` でレスポンスヘッダーに付与
- [ ] YARP Transform: `X-Correlation-Id` を下流サービスに転送
- [ ] ヘルスチェック: `/health` — Liveness（常に 200）
- [ ] ヘルスチェック: `/health/ready` — Readiness（Redis + バックエンドサービス）
- [ ] ヘルスチェック: 必須サービス Unhealthy → 全体 Unhealthy
- [ ] ヘルスチェック: 非必須サービス Unhealthy → 全体 Degraded
- [ ] ヘルスチェック: 個別サービスのタイムアウト 3 秒
- [ ] ヘルスチェック: `/health`, `/health/ready` に `.AllowAnonymous()` が設定されていること
- [ ] Serilog: `CompactJsonFormatter` で JSON 形式出力
- [ ] Serilog: `ServiceName = "ApiGateway"` がエンリッチされていること
- [ ] OpenTelemetry: ASP.NET Core + HTTP Client + Runtime インストルメンテーション
- [ ] カスタムメトリクス: `gateway.requests.total`, `gateway.requests.duration` が計装されていること（設計書 §9）
- [ ] カスタムメトリクス: `gateway.circuit_breaker.state`, `gateway.rate_limiter.limited` が計装されていること
- [ ] ヘルスチェック: `/health/ready` に `WriteHealthResponse` JSON レスポンスライターが設定されていること（設計書 §21.3）
- [ ] ヘルスチェック: レスポンスに `status`, `duration`, `checks` フィールドが含まれること
- [ ] ログ: `Console.WriteLine` なし（`ILogger<T>` のみ使用）
- [ ] ログ: 文字列補間禁止（メッセージテンプレート形式のみ使用）
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" ApiGateway/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 7: 耐障害性

### 目的

設計書 §7（サーキットブレーカー設定）に基づき、YARP + Polly によるサーキットブレーカー・リトライ・タイムアウトを構成する。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `ApiGateway/Program.cs` | 更新 | YARP HttpClient に Resilience ハンドラー適用 |
| 2 | `ApiGateway/Infrastructure/Caching/AuthCacheInvalidationConsumer.cs` | 作成 | Kafka イベント受信による認証キャッシュ無効化（設計書 §12） |
| 3 | `ApiGateway.Tests/Resilience/CircuitBreakerTests.cs` | 作成 | サーキットブレーカー状態遷移テスト（WireMock） |

### 7.1 サービス別サーキットブレーカー設定（設計書 §7 準拠）

| サービス | 失敗率しきい値 | タイムアウト | ハーフオープンリクエスト数 | リセットタイムアウト |
|---------|-------------|---------|-------------------|------------|
| AuthService | 50% | 2s | 10 | 30s |
| UserManagementService | 50% | 3s | 5 | 60s |
| InventoryManagementService | 60% | 5s | 10 | 60s |
| SalesManagementService | 50% | 4s | 10 | 60s |
| PaymentCartService | 30% | 10s | 5 | 120s |
| PointService | 50% | 3s | 5 | 60s |
| CouponService | 60% | 3s | 10 | 45s |
| AiSupportService | 70% | 5s | 10 | 30s |

### 7.2 フォールバック戦略（設計書 §7 準拠）

| サービス | フォールバック戦略 | レスポンス |
|---------|---------------|----------|
| AuthService | 503 即時返却 | 認証は代替不可 |
| UserManagementService | 503 即時返却 | ユーザーデータは代替不可 |
| InventoryManagementService | Redis キャッシュ応答 | 最新の商品一覧キャッシュを返却 |
| SalesManagementService | 503 即時返却 | 注文データは代替不可 |
| PaymentCartService | 503 即時返却 | 決済は安全性優先で即座にエラー返却 |
| CouponService | デフォルト値（クーポンなし） | クーポン適用なしで処理継続 |
| PointService | デフォルト値（0 ポイント） | ポイント残高 0 で表示 |
| AiSupportService | 人気商品の静的リスト | AI レコメンドの代わりに人気商品リストを返却 |

### 7.3 実装方針

YARP は `IHttpClientFactory` ベースで下流サービスへの HTTP リクエストを送信するため、`Microsoft.Extensions.Http.Resilience` の `AddStandardResilienceHandler` をクラスター単位の `HttpClient` に適用する。

```csharp
// 各クラスターの HttpClient に Resilience ポリシーを適用
builder.Services.AddHttpClient("auth-cluster")
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.Delay = TimeSpan.FromMilliseconds(500);
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.FailureRatio = 0.5;
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
    });
```

### 7.4 Redis キャッシュ戦略（設計書 §12 準拠）

API Gateway はパフォーマンス最適化のため、以下のキャッシュを Redis に保存する:

| キャッシュ種別 | キーパターン | TTL | 用途 |
|-------------|------------|-----|------|
| レート制限カウンター | `rate:{userId/ip}:{endpoint}` | 1 分 | レート制限の分散カウンター |
| ルート定義キャッシュ | `route:{path}` | 60 分 | YARP ルート定義のキャッシュ |
| 認証検証結果キャッシュ | `auth:{tokenHash}` | 5 分 | JWT 検証結果のキャッシュ |

### 7.5 認証キャッシュ無効化 — Kafka イベント受信（設計書 §12 準拠）

セキュリティリスクを最小化するため、以下の Kafka イベント受信時に認証キャッシュを即時無効化する:

| Kafka トピック | イベント | アクション |
|--------------|--------|----------|
| `user.permission_changed` | ユーザー権限変更 | 該当ユーザーの `auth:{tokenHash}` キャッシュを削除 |
| `user.logged_out` | ユーザーログアウト | トークンハッシュを無効化リストに追加 |

実装は `BackgroundService` として Kafka コンシューマーを起動し、`IServiceScopeFactory` で Scoped サービスを安全に取得する（AGENTS.md §10.6 準拠）。

### 7.6 テスト仕様（設計書 §16 準拠）

| テストメソッド | カテゴリ | 検証内容 |
|-------------|---------|---------|
| `Should_ReturnFallbackResponse_When_CircuitBreakerOpen` | Resilience | サーキットブレーカー Open 時にフォールバックレスポンスが返却されること |
| `Should_TransitionToHalfOpen_When_ResetTimeoutExpired` | Resilience | リセットタイムアウト経過後に HalfOpen 状態に遷移すること |
| `Should_ReturnCachedProducts_When_InventoryServiceUnavailable` | Resilience | 在庫管理サービス障害時に Redis キャッシュ応答が返却されること |
| `Should_Return503_When_AuthServiceCircuitBreakerOpen` | Resilience | 認証サービス障害時に即座に 503 が返却されること |

### Phase 7 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] 全 8 サービスにサーキットブレーカー設定が適用されていること
- [ ] 各サービスのタイムアウト値が設計書 §7 と一致すること
- [ ] 各サービスの失敗率しきい値が設計書 §7 と一致すること
- [ ] フォールバック戦略が設計書 §7 に基づき定義されていること
- [ ] Redis キャッシュ: 認証検証結果キャッシュ（TTL 5 分）が実装されていること（設計書 §12）
- [ ] Redis キャッシュ: キーパターン `auth:{tokenHash}` が正しいこと
- [ ] Kafka: `AuthCacheInvalidationConsumer` が `BackgroundService` として実装されていること
- [ ] Kafka: `user.permission_changed` イベントで該当ユーザーの認証キャッシュが削除されること
- [ ] Kafka: `user.logged_out` イベントでトークンが無効化リストに追加されること
- [ ] リトライ: 指数バックオフ（最大 3 回）
- [ ] `new HttpClient()` の直接使用がないこと（`IHttpClientFactory` 経由のみ）
- [ ] 禁止事項: `.Result` / `.Wait()` の使用なし
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" ApiGateway/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 8: エラーハンドリング & ログ

### 目的

設計書 §11（エラーハンドリング）および §9（センシティブデータマスキング）に基づき、グローバル例外ハンドラーと PII マスキング付きログを構成する。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `ApiGateway/Program.cs` | 更新 | グローバル例外ハンドラー（`UseExceptionHandler`） |

### 8.1 エラーコード定義（設計書 §11 準拠）

| エラーコード | HTTP ステータス | 説明 |
|------------|---------------|------|
| `GW-4001` | 401 Unauthorized | 無効な認証トークン |
| `GW-4002` | 403 Forbidden | 認可が必要 |
| `GW-4003` | 401 Unauthorized | トークン期限切れ |
| `GW-4004` | 404 Not Found | ルートが見つからない |
| `GW-4005` | 405 Method Not Allowed | メソッド不許可 |
| `GW-4006` | 415 Unsupported Media Type | サポートされていないメディアタイプ |
| `GW-4007` | 400 Bad Request | 必須ヘッダーの欠如 |
| `GW-4008` | 400 Bad Request | 無効なリクエスト形式 |
| `GW-4291` | 429 Too Many Requests | レート制限超過 |
| `GW-5001` | 504 Gateway Timeout | ゲートウェイタイムアウト |
| `GW-5002` | 503 Service Unavailable | サーキットブレーカーオープン |
| `GW-5003` | 502 Bad Gateway | バックエンドサービスエラー |
| `GW-5004` | 500 Internal Server Error | ゲートウェイ内部エラー |

### 8.2 グローバル例外ハンドラー（RFC 9457 準拠 + GW-xxxx コード）

設計書 §11 で定義されたエラーコード（GW-4001〜GW-5004）をレスポンスの `extensions` フィールドに含め、`traceId` も付与する:

```csharp
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var error = feature?.Error;

        logger.LogError(error, "Unhandled exception: {Message}", error?.Message);

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        var (statusCode, errorCode, detail) = error switch
        {
            TimeoutException => (504, "GW-5001", "ゲートウェイタイムアウト"),
            HttpRequestException => (502, "GW-5003", "バックエンドサービスエラー"),
            _ => (500, "GW-5004", "内部エラーが発生しました")
        };

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new
        {
            type = $"https://tools.ietf.org/html/rfc9110#section-15.6.{statusCode - 499}",
            title = ReasonPhrases.GetReasonPhrase(statusCode),
            status = statusCode,
            detail,
            instance = context.Request.Path.Value,
            extensions = new { code = errorCode, traceId }
        });
    });
});
```

> **注記**: サーキットブレーカー Open 時（`GW-5002`）のレスポンスはフォールバック戦略（Phase 7）で処理する。
> 認証・認可エラー（`GW-4001`〜`GW-4003`）は ASP.NET Core の認証ミドルウェアが自動的に 401/403 を返すため、
> `JwtBearerEvents.OnChallenge` / `OnForbidden` でカスタムレスポンスを設定する。

### 8.3 バックエンドエラーのマスキング

- バックエンドサービスが返すエラー詳細（スタックトレース、SQL エラー、内部パス等）をクライアントに露出しない
- `appsettings.json`: `"DetailedErrors": false`
- 500 系エラーには一般的なメッセージのみ返却: `"内部エラーが発生しました"`

### 8.4 PII マスキング（設計書 §9 準拠）

| マスキング対象 | 方法 |
|-------------|------|
| `Authorization` ヘッダー（JWT トークン） | Serilog Destructuring Policy で `Bearer ***` に置換 |
| `Cookie` ヘッダー | ログ出力から除外 |
| リクエストボディ（`/auth/login`） | パスワードフィールドをマスク |
| メールアドレス | 部分マスク（`u***@example.com`） |
| IP アドレス | レート制限ログのみ記録、一般ログには出力しない |

### 8.5 テスト仕様

| テストメソッド | カテゴリ | 検証内容 |
|-------------|---------|---------|
| `Should_ReturnProblemDetailsWithErrorCode_When_UnhandledExceptionOccurs` | ErrorHandling | 500 エラーレスポンスに `GW-5004` コードと `traceId` が含まれること |
| `Should_NotExposeStackTrace_When_ErrorOccurs` | ErrorHandling | エラーレスポンスにスタックトレースが含まれないこと |
| `Should_Return504WithGW5001_When_GatewayTimeout` | ErrorHandling | タイムアウト時に 504 + `GW-5001` が返却されること |
| `Should_Return502WithGW5003_When_BackendServiceError` | ErrorHandling | バックエンドエラー時に 502 + `GW-5003` が返却されること |
| `Should_MaskAuthorizationHeader_When_LoggingRequest` | ErrorHandling | ログに JWT トークンが `Bearer ***` としてマスクされること |

### Phase 8 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] グローバル例外ハンドラー: `UseExceptionHandler()` がパイプライン最上位に配置されていること
- [ ] エラーレスポンス: RFC 9457 Problem Details 形式であること
- [ ] エラーレスポンス: `extensions` に `code`（GW-xxxx）と `traceId` が含まれること（設計書 §11）
- [ ] エラーレスポンス: GW-5001（504 タイムアウト）、GW-5003（502 バックエンドエラー）、GW-5004（500 内部エラー）が正しくマッピングされること
- [ ] エラーレスポンス: 認証エラー（GW-4001/GW-4003）が `JwtBearerEvents` でカスタマイズされていること
- [ ] エラーレスポンス: スタックトレースがクライアントに返却されないこと
- [ ] エラーレスポンス: `"DetailedErrors": false` が設定されていること
- [ ] ログ: スタックトレースを含めて `ILogger.LogError(ex, ...)` で記録すること
- [ ] ログ: PII（パスワード、トークン、メールアドレス全文）がログに出力されないこと
- [ ] ログ: `catch (Exception) { }` の例外握りつぶしがないこと
- [ ] 禁止事項: `Console.WriteLine` なし
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" ApiGateway/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 9: 最終統合 & デプロイ準備

### 目的

設計書 §24（Program.cs 最終統合版）に基づき、全ミドルウェアを正しいパイプライン順序で統合し、Dockerfile の最終化と .NET Aspire AppHost 統合を行う。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `ApiGateway/Program.cs` | 最終統合 | 全ミドルウェアの統合（§5 パイプライン順序厳守） |
| 2 | `ApiGateway/Dockerfile` | 最終化 | 本番用 Dockerfile |
| 3 | `AppHost/Program.cs` | 更新 | ApiGateway の WithReference 統合 |

### 9.1 ミドルウェアパイプライン最終順序（設計書 §5 / §24 / AGENTS.md §11.3 厳守）

```
┌─────────────────────────────────────────────────┐
│ 1. UseExceptionHandler()                         │ ← 最外層: 全例外をキャッチ
│ 1.5 UseForwardedHeaders()                        │ ← プロキシヘッダー処理（§5）
│ 2. UseHsts() + UseHttpsRedirection()             │ ← セキュリティ
│ 2.5 UseSecurityHeaders()                         │ ← セキュリティヘッダー（§20）
│ 2.6 UseResponseTime()                            │ ← X-Response-Time（§5）
│ 3. UseCorrelationId()                            │ ← Correlation ID（§19）
│ 4. UseSerilogRequestLogging()                    │ ← リクエストログ
│ 5. UseCors()                                     │ ← CORS（認証より前）
│ 6. UseAuthentication()                           │ ← 認証
│    UseAuthorization()                            │ ← 認可（認証の直後）
│ 7. UseRateLimiter()                              │ ← レート制限（認証後）
│ 8. MapReverseProxy()                             │ ← YARP ルーティング
│    MapHealthChecks("/health")                    │ ← Liveness
│    MapHealthChecks("/health/ready")              │ ← Readiness
└─────────────────────────────────────────────────┘
```

> **禁止パターン**:
> - `UseAuthentication()` を `UseAuthorization()` の後に配置する
> - `UseExceptionHandler()` をパイプライン途中に配置する
> - `UseCors()` を `UseAuthentication()` の後に配置する

### 9.2 .NET Aspire AppHost 統合（設計書 §15 準拠）

```csharp
// AppHost/Program.cs — ApiGateway 登録
builder.AddProject<Projects.ApiGateway>("api-gateway")
    .WithReference(redis)
    .WithReference(authService)
    .WithReference(userService)
    .WithReference(inventoryService)
    .WithReference(salesService)
    .WithReference(paymentCartService)
    .WithReference(couponService)
    .WithReference(pointService)
    .WithReference(aiService);
```

> **注記**: MailSendService は Kafka イベント駆動の内部サービスのため、API Gateway からの `WithReference` は不要。

### 9.3 パフォーマンス目標（設計書 §12 準拠）

最終統合後、以下のパフォーマンス目標を検証する。パフォーマンステストには k6 または NBomber を使用する（設計書 §16 準拠）。

| メトリクス | 目標 | 検証方法 |
|---------|------|---------|
| リクエストレイテンシ（平均） | < 30ms | OpenTelemetry メトリクス |
| リクエストレイテンシ（p95） | < 50ms | OpenTelemetry メトリクス |
| リクエストレイテンシ（p99） | < 100ms | OpenTelemetry メトリクス |
| スループット | > 1000 req/s | k6 / NBomber 負荷テスト |
| エラー率 | < 0.1% | OpenTelemetry メトリクス |
| サーキットブレーカーオープン時間 | < 1% | カスタムメトリクス |

> **spec.md との整合性**: API Gateway 通過レイテンシは 30ms（処理 20ms + オーバーヘッド 10ms）以内。全体 API p95 は 300ms 以内。

### 9.3 環境変数チェックリスト（設計書 §10 準拠）

本番デプロイ前に以下の環境変数が全て設定されていることを確認する。

| 変数名 | 必須 | 用途 |
|--------|------|------|
| `ASPNETCORE_ENVIRONMENT` | ✅ | 環境名（Production） |
| `ConnectionStrings__Redis` | ✅ | Redis 接続文字列（レート制限、認証キャッシュ） |
| `Jwt__SecretKey` | ✅ | JWT 署名キー |
| `Jwt__Issuer` | ✅ | JWT 発行者 |
| `Jwt__Audience` | ✅ | JWT 対象者 |
| `Kafka__BootstrapServers` | ✅ | Kafka ブローカーアドレス（認証キャッシュ無効化） |
| `RateLimiting__Anonymous__PermitLimit` | — | 未認証ユーザーレート制限数 |
| `RateLimiting__Authenticated__PermitLimit` | — | 認証ユーザーレート制限数 |

### 9.4 テストプロジェクト最終構成

```
ApiGateway.Tests/
├── ApiGateway.Tests.csproj
├── Fixtures/
│   └── GatewayWebApplicationFactory.cs       # テスト用 WebApplicationFactory + TestAuthHandler
├── Middleware/
│   ├── CorrelationIdMiddlewareTests.cs        # Correlation ID テスト
│   └── SecurityHeadersMiddlewareTests.cs      # セキュリティヘッダーテスト
├── Routing/
│   └── YarpRoutingTests.cs                    # YARP ルーティングテスト
├── Security/
│   └── AuthenticationFilterTests.cs           # 認証・認可テスト
├── RateLimiting/
│   └── RateLimitingTests.cs                   # レート制限テスト
├── Resilience/
│   └── CircuitBreakerTests.cs                 # サーキットブレーカーテスト
└── ErrorHandling/
    └── ErrorResponseTests.cs                  # エラーレスポンステスト
```

### 9.4 テスト一覧（全フェーズ統合）

| # | テストクラス | テストメソッド | Trait |
|---|-----------|-------------|-------|
| 1 | `CorrelationIdMiddlewareTests` | `Should_GenerateCorrelationId_When_HeaderNotProvided` | Middleware |
| 2 | `CorrelationIdMiddlewareTests` | `Should_PreserveCorrelationId_When_HeaderProvided` | Middleware |
| 3 | `SecurityHeadersMiddlewareTests` | `Should_IncludeAllSecurityHeaders_When_AnyResponseReturned` | Security |
| 4 | `SecurityHeadersMiddlewareTests` | `Should_IncludeCacheControlNoStore_When_ApiResponseReturned` | Security |
| 5 | `SecurityHeadersMiddlewareTests` | `Should_NotIncludeServerHeader_When_ResponseReturned` | Security |
| 6 | `SecurityHeadersMiddlewareTests` | `Should_IncludePermissionsPolicy_When_AnyResponseReturned` | Security |
| 7 | `SecurityHeadersMiddlewareTests` | `Should_IncludeResponseTime_When_AnyResponseReturned` | Security |
| 8 | `AuthenticationFilterTests` | `Should_AllowAccess_When_AnonymousEndpointRequested` | Security |
| 9 | `AuthenticationFilterTests` | `Should_Return401_When_AuthenticatedEndpointAccessedWithoutToken` | Security |
| 10 | `AuthenticationFilterTests` | `Should_Return403_When_NonAdminAccessesAdminEndpoint` | Security |
| 11 | `AuthenticationFilterTests` | `Should_AllowAccess_When_AdminAccessesAdminEndpoint` | Security |
| 12 | `YarpRoutingTests` | `Should_Return404_When_UndefinedRouteRequested` | Routing |
| 13 | `YarpRoutingTests` | `Should_NotReturn404_When_DefinedRouteRequested` | Routing |
| 14 | `YarpRoutingTests` | `Should_StripApiPrefix_When_RoutingToBackendService` | Routing |
| 15 | `RateLimitingTests` | `Should_Return429WithRetryAfter_When_RateLimitExceeded` | RateLimit |
| 16 | `RateLimitingTests` | `Should_AllowRequest_When_WithinRateLimit` | RateLimit |
| 17 | `RateLimitingTests` | `Should_IncludeRetryAfterHeader_When_RateLimited` | RateLimit |
| 18 | `CircuitBreakerTests` | `Should_ReturnFallbackResponse_When_CircuitBreakerOpen` | Resilience |
| 19 | `CircuitBreakerTests` | `Should_TransitionToHalfOpen_When_ResetTimeoutExpired` | Resilience |
| 20 | `CircuitBreakerTests` | `Should_ReturnCachedProducts_When_InventoryServiceUnavailable` | Resilience |
| 21 | `CircuitBreakerTests` | `Should_Return503_When_AuthServiceCircuitBreakerOpen` | Resilience |
| 22 | `ErrorResponseTests` | `Should_ReturnProblemDetailsWithErrorCode_When_UnhandledExceptionOccurs` | ErrorHandling |
| 23 | `ErrorResponseTests` | `Should_NotExposeStackTrace_When_ErrorOccurs` | ErrorHandling |
| 24 | `ErrorResponseTests` | `Should_Return504WithGW5001_When_GatewayTimeout` | ErrorHandling |
| 25 | `ErrorResponseTests` | `Should_Return502WithGW5003_When_BackendServiceError` | ErrorHandling |
| 26 | `ErrorResponseTests` | `Should_MaskAuthorizationHeader_When_LoggingRequest` | ErrorHandling |

### Phase 9 完了チェックリスト

- [ ] `dotnet build ApiGateway/` — 警告なし成功
- [ ] `dotnet build ApiGateway.Tests/` — 警告なし成功
- [ ] `dotnet test ApiGateway.Tests/` — 全テスト通過
- [ ] ミドルウェアパイプライン順序が §9.1 の順序と完全に一致すること
- [ ] `UseForwardedHeaders()` が `UseExceptionHandler()` の直後に配置されていること
- [ ] `UseAuthentication()` が `UseAuthorization()` の直前に配置されていること
- [ ] `UseExceptionHandler()` がパイプライン最上位に配置されていること
- [ ] `UseCors()` が `UseAuthentication()` の前に配置されていること
- [ ] `UseRateLimiter()` が `UseAuthorization()` の後に配置されていること
- [ ] Dockerfile: マルチステージビルド（`sdk:10.0` → `aspnet:10.0`）
- [ ] Dockerfile: `USER skishop`（非 root 実行）
- [ ] Dockerfile: `HEALTHCHECK` あり（`/health` エンドポイント使用）
- [ ] Dockerfile: ベースイメージに `latest` タグなし
- [ ] AppHost: `WithReference` で全 8 バックエンドサービスが参照されていること
- [ ] AppHost: MailSendService の WithReference がないこと
- [ ] パフォーマンス: レイテンシ目標（p95 < 50ms, p99 < 100ms）が検証可能であること（設計書 §12）
- [ ] テスト: 全 26 テストケースが実装されていること（Middleware: 2, Security: 6, Routing: 3, RateLimit: 3, Resilience: 4, ErrorHandling: 5, HealthCheck: 3）
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" ApiGateway/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## 横断的な規約遵守チェックリスト

全フェーズ完了後に以下の最終確認を実施する。

### AGENTS.md 禁止事項チェック

```bash
# 秘密情報のハードコードチェック
grep -r "Password\s*=\s*\"" --include="*.cs" ApiGateway/

# Console.WriteLine チェック
grep -r "Console\.Write" --include="*.cs" ApiGateway/

# .Result / .Wait() チェック（デッドロックリスク）
grep -rP "\.(Result|Wait)\(\)" --include="*.cs" ApiGateway/

# プロパティインジェクションチェック
grep -r "\[Inject\]" --include="*.cs" ApiGateway/

# DateTime.Now チェック（ローカル時刻禁止 → DateTime.UtcNow を使用）
grep -r "DateTime\.Now[^U]" --include="*.cs" ApiGateway/

# new HttpClient() チェック（IHttpClientFactory 経由のみ）
grep -r "new HttpClient()" --include="*.cs" ApiGateway/

# 文字列補間ログチェック
grep -rP '_logger\.Log\w+\(\$"' --include="*.cs" ApiGateway/
```

### コーディング規約チェック

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | 命名規則 | PascalCase（クラス・メソッド）、camelCase（変数）、`_camelCase`（プライベートフィールド） |
| 2 | CancellationToken | 全 async メソッドに `CancellationToken ct = default` |
| 3 | ログ出力 | `ILogger<T>` + メッセージテンプレート形式 |
| 4 | DI | primary constructor によるコンストラクタインジェクション |
| 5 | Null Safety | `?.`, `??`, `ArgumentNullException.ThrowIfNull()` の使用 |
| 6 | 例外処理 | `catch` ブロックで必ずログ出力または再スロー |
| 7 | 秘密情報 | `dotnet user-secrets` / 環境変数（ハードコード禁止） |
| 8 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 9 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 10 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

### テスト規約チェック

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | 命名 | `Should_期待結果_When_条件` パターン |
| 2 | AAA パターン | `// Arrange` / `// Act` / `// Assert` のコメント付き |
| 3 | アサーション | Shouldly 使用（`ShouldBe()`, `ShouldNotBeNull()` 等） |
| 4 | カテゴリ | `[Trait("Category", "...")]` 付与 |
| 5 | 独立性 | テスト間の共有状態なし |
| 6 | PII | テストコードに本番個人情報なし |
| 7 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 8 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 9 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## フェーズ間依存関係

```
Phase 1（基盤）
    ↓
Phase 2（YARP ルーティング）
    ↓
Phase 3（認証・認可）── テストプロジェクト作成
    ↓
Phase 4（セキュリティ）── SecurityHeadersMiddleware, ForwardedHeaders, ResponseTimeMiddleware
    ↓
Phase 5（レート制限）── Redis 分散カウンター
    ↓
Phase 6（可観測性）── CorrelationIdMiddleware, HealthCheck（JSON レスポンスライター含む）, Serilog, OpenTelemetry, カスタムメトリクス
    ↓
Phase 7（耐障害性）── サーキットブレーカー, フォールバック, Redis キャッシュ戦略, Kafka 認証キャッシュ無効化
    ↓
Phase 8（エラーハンドリング）── グローバル例外ハンドラー（GW-xxxx コード）, PII マスキング
    ↓
Phase 9（最終統合）── 全ミドルウェア統合, Dockerfile, AppHost, パフォーマンス目標検証
```

> **注記**: Phase 3 でテストプロジェクトを作成し、以降のフェーズでテストを追加していく構成。各フェーズは前フェーズの成果物に依存するため、順序通りに実行すること。

---

## 参照ドキュメント

| ドキュメント | 参照セクション |
|------------|-------------|
| `design-docs/api-gateway-design.md` | 全セクション（§1-§24） |
| `design-docs/spec.md` | §1（API Gateway 責務）、§レート制限設計、§Saga レイテンシバジェット |
| `AGENTS.md` | §4（コーディング規約）、§5（セキュリティ）、§6（API 設計）、§7（設定ファイル）、§8（NuGet）、§9（テスト）、§11.1（耐障害性）、§11.2（可観測性）、§11.3（ミドルウェアパイプライン順序） |
| `doc-improve-plan.md` | §3.10（ApiGateway 不足項目分析） |
| `.github/instructions/` | `dotnet-coding-standards.instructions.md`, `security-coding.instructions.md`, `api-design.instructions.md`, `dotnet-config.instructions.md`, `nuget-dependency.instructions.md`, `test-standards.instructions.md`, `dockerfile-infra.instructions.md` |
