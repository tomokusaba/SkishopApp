# ApiGateway — SkiShop API ゲートウェイ

SkiShop EC プラットフォームの **YARP ベース API ゲートウェイ**。  
全マイクロサービスへのリクエストルーティング、認証・認可、レート制限、セキュリティヘッダー付与、可観測性を一元的に提供する。

---

## 目次

1. [前提条件](#前提条件)
2. [ローカル環境セットアップ](#ローカル環境セットアップ)
3. [ビルド](#ビルド)
4. [テスト実行](#テスト実行)
5. [ローカル起動と動作確認](#ローカル起動と動作確認)
6. [Docker による起動](#docker-による起動)
7. [プロジェクト構成](#プロジェクト構成)
8. [設定ファイル](#設定ファイル)
9. [アーキテクチャ概要](#アーキテクチャ概要)
10. [トラブルシューティング](#トラブルシューティング)

---

## 前提条件

| ツール | バージョン | 確認コマンド |
|--------|-----------|-------------|
| .NET SDK | **10.0 以上** | `dotnet --version` |
| Git | 2.x 以上 | `git --version` |
| Docker（任意） | 24.x 以上 | `docker --version` |

> **注意**: .NET 10 SDK がインストールされていない場合は、[公式ダウンロードページ](https://dotnet.microsoft.com/download/dotnet/10.0) から取得してください。

---

## ローカル環境セットアップ

### 1. リポジトリのクローン

```bash
git clone https://github.com/<your-org>/DotNet-Skishop-App.git
cd DotNet-Skishop-App
```

### 2. JWT 秘密情報の設定（⚠️ 必須）

本サービスでは **`ValidateOnStart()`** により、起動時に JWT 設定の存在が検証されます。  
`Jwt:Issuer`、`Jwt:Audience`、および `Jwt:SigningKey` または `Jwt:Authority` のいずれかが設定されていない場合、アプリケーションは **即座に例外で停止** します。

#### 方法 A: `dotnet user-secrets` を使用する（推奨）

`user-secrets` はプロジェクトのソースコードとは分離されたローカルストレージに秘密情報を保存します。  
リポジトリにコミットされるリスクがないため、**推奨される方法** です。

```bash
cd Services/ApiGateway

# user-secrets を初期化（初回のみ）
dotnet user-secrets init

# JWT 設定を登録
dotnet user-secrets set "Jwt:Issuer" "https://dev.skieshop.com"
dotnet user-secrets set "Jwt:Audience" "skieshop-dev-api"
dotnet user-secrets set "Jwt:SigningKey" "dev-only-signing-key-do-not-use-in-production-minimum-32-chars"

# 登録済みの秘密情報を確認
dotnet user-secrets list
```

#### 方法 B: `appsettings.Development.json` を使用する

本リポジトリには **開発用のデフォルト値** が `appsettings.Development.json` に含まれています。  
`ASPNETCORE_ENVIRONMENT=Development` で起動する場合は、追加の設定なしでそのまま動作します。

```jsonc
// Services/ApiGateway/appsettings.Development.json（同梱済み）
{
  "Jwt": {
    "Issuer": "https://dev.skieshop.com",
    "Audience": "skieshop-dev-api",
    "SigningKey": "dev-only-signing-key-do-not-use-in-production-minimum-32-chars"
  }
}
```

> **⚠️ セキュリティ注意**: `appsettings.Development.json` に記載されている JWT 鍵は **開発専用のダミー値** です。本番環境では環境変数または Azure Key Vault を使用してください。

#### 方法 C: 環境変数を使用する

ASP.NET Core は `__`（ダブルアンダースコア）区切りの環境変数を自動的に設定にマッピングします。

```bash
export Jwt__Issuer="https://dev.skieshop.com"
export Jwt__Audience="skieshop-dev-api"
export Jwt__SigningKey="dev-only-signing-key-do-not-use-in-production-minimum-32-chars"
```

### 3. CORS 設定の確認

`Cors:AllowedOrigins` も `ValidateOnStart()` により起動時に検証されます。  
開発環境では `appsettings.Development.json` に以下のデフォルト値が設定されています。

```jsonc
{
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000", "http://localhost:5173"]
  }
}
```

フロントエンドが別ポートで動作する場合は、オリジンを追加してください。

### 4. NuGet パッケージの復元

```bash
cd Services/ApiGateway
dotnet restore
```

---

## ビルド

### プロダクションコードのビルド

```bash
cd Services/ApiGateway
dotnet build
```

**期待される結果**:

```
ビルドに成功しました。
    0 個の警告
    0 エラー
```

> `TreatWarningsAsErrors=true` が有効なため、警告もエラーとして扱われます。

### テストプロジェクトを含む一括ビルド

```bash
cd Services
dotnet build ApiGateway/ApiGateway.csproj
dotnet build ApiGateway.Tests/ApiGateway.Tests.csproj
```

---

## テスト実行

### 全テスト実行

```bash
cd Services/ApiGateway.Tests
dotnet test
```

**期待される結果**:

```
成功!   -失敗:     0、合格:    26、スキップ:     0、合計:    26
```

### カテゴリ別テスト実行

テストは `[Trait("Category", "...")]` でカテゴリ分けされています。

```bash
# セキュリティテストのみ
dotnet test --filter "Category=Security"

# ミドルウェアテストのみ
dotnet test --filter "Category=Middleware"

# ヘルスチェックテストのみ
dotnet test --filter "Category=HealthCheck"
```

### テストカバレッジの取得

```bash
cd Services/ApiGateway.Tests
dotnet test --collect:"XPlat Code Coverage"
```

カバレッジレポートは `TestResults/` ディレクトリ配下に生成されます。

### テストスイート一覧

| テストクラス | カテゴリ | テスト数 | 検証内容 |
|------------|---------|---------|---------|
| `AuthenticationFilterTests` | Security | 5 | JWT 認証・認可フィルタ |
| `CorrelationIdMiddlewareTests` | Middleware | 5 | 相関 ID 付与・検証 |
| `SecurityHeadersMiddlewareTests` | Middleware | 4 | セキュリティヘッダー |
| `RateLimitingTests` | RateLimiting | 3 | レート制限ポリシー |
| `ErrorHandlingTests` | ErrorHandling | 3 | グローバル例外ハンドリング |
| `HealthCheckTests` | HealthCheck | 2 | Liveness/Readiness |
| `CircuitBreakerTests` | Resilience | 4 | サーキットブレーカー |

---

## ローカル起動と動作確認

### 1. アプリケーションの起動

```bash
cd Services/ApiGateway

# Development 環境で起動（appsettings.Development.json が自動適用）
dotnet run
```

デフォルトでは `http://localhost:5000` と `https://localhost:5001` でリッスンします。

> **注意**: バックエンドサービス（AuthService, InventoryManagementService 等）が起動していない場合、YARP のプロキシ転送は失敗しますが、ゲートウェイ自体は正常に起動します。

### 2. ヘルスチェックの確認

```bash
# Liveness チェック（常に 200 OK）
curl -s http://localhost:5000/health
# 期待レスポンス: {"status":"Healthy"}

# Readiness チェック（バックエンドサービスの状態を含む）
curl -s http://localhost:5000/health/ready | python3 -m json.tool
```

**Readiness レスポンス例**（バックエンドサービス未起動時）:

```json
{
  "status": "Degraded",
  "duration": 0.12,
  "checks": [
    {
      "name": "backend-services",
      "status": "Degraded",
      "duration": 0.05,
      "description": "1/8 clusters healthy, 4/8 critical clusters available",
      "data": {}
    }
  ]
}
```

### 3. セキュリティヘッダーの確認

```bash
curl -s -I http://localhost:5000/health
```

以下のセキュリティヘッダーが含まれていることを確認:

```
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Content-Security-Policy: default-src 'self'
Referrer-Policy: strict-origin-when-cross-origin
Permissions-Policy: camera=(), microphone=(), geolocation=()
X-Correlation-Id: <UUID>
```

### 4. レート制限の確認

認証エンドポイント（5 req/min/IP）にリクエストを送り、6 回目で 429 が返ることを確認:

```bash
for i in $(seq 1 6); do
  echo "Request $i:"
  curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/api/auth/login
  echo ""
done
# 1〜5 回目: 502（バックエンド未起動のため）
# 6 回目: 429（レート制限超過）
```

---

## Docker による起動

### イメージビルド

```bash
cd Services
docker build -f ApiGateway/Dockerfile -t skieshop/api-gateway:latest .
```

### コンテナ起動

```bash
docker run -d \
  --name api-gateway \
  -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e Jwt__Issuer="https://skieshop.com" \
  -e Jwt__Audience="skieshop-api" \
  -e Jwt__SigningKey="<本番用の32文字以上の秘密鍵>" \
  -e Cors__AllowedOrigins__0="https://www.skieshop.com" \
  skieshop/api-gateway:latest
```

> **⚠️ 本番環境**: 秘密鍵はコマンドライン引数に含めず、Docker Secrets や Azure Key Vault を使用してください。

### ヘルスチェック確認

```bash
curl -s http://localhost:8080/health
```

---

## プロジェクト構成

```
Services/ApiGateway/
├── ApiGateway.csproj               # プロジェクト定義（NuGet パッケージ）
├── Program.cs                      # エントリポイント・DI・ミドルウェアパイプライン
├── Configurations/                 # 設定クラス（IOptions<T> + ValidateOnStart）
│   ├── JwtSettings.cs              #   JWT 認証設定（Issuer/Audience/SigningKey/Authority）
│   ├── JwtSettingsValidator.cs     #   JWT カスタムバリデーター（SigningKey or Authority 必須）
│   └── CorsSettings.cs            #   CORS 設定（AllowedOrigins 必須）
├── Infrastructure/
│   ├── HealthChecks/
│   │   └── BackendServicesHealthCheck.cs  # YARP IProxyStateLookup ベースヘルスチェック
│   ├── Metrics/
│   │   └── GatewayMetrics.cs              # OpenTelemetry カスタムメトリクス
│   └── Middleware/
│       ├── CorrelationIdMiddleware.cs     # 分散トレーシング用相関 ID
│       ├── ResponseTimeMiddleware.cs      # 処理時間計測 + メトリクス
│       ├── SecurityHeadersMiddleware.cs   # OWASP 推奨セキュリティヘッダー
│       └── *Extensions.cs                # ミドルウェア登録用拡張メソッド
├── appsettings.json                # 共通設定（YARP ルート/クラスター定義）
├── appsettings.Development.json    # 開発用設定（JWT ダミー値含む）
├── appsettings.Production.json     # 本番用設定（環境変数参照）
└── Dockerfile                      # マルチステージビルド + 非 root 実行

Services/ApiGateway.Tests/
├── ApiGateway.Tests.csproj
├── Fixtures/
│   └── GatewayWebApplicationFactory.cs   # テスト用 WebApplicationFactory
├── Security/                             # 認証・認可テスト
├── Middleware/                           # ミドルウェアテスト
├── RateLimiting/                         # レート制限テスト
├── ErrorHandling/                        # エラーハンドリングテスト
├── HealthCheck/                          # ヘルスチェックテスト
└── Resilience/                           # サーキットブレーカーテスト
```

---

## 設定ファイル

### 環境別設定の優先順位

ASP.NET Core は以下の優先順位で設定を読み込みます（後のものが上書き）:

```
appsettings.json                ← 共通デフォルト
appsettings.{Environment}.json  ← 環境別オーバーライド
dotnet user-secrets             ← ローカル開発用秘密情報
環境変数                         ← デプロイ環境での上書き
```

### ValidateOnStart() による起動時バリデーション

以下の設定は起動時に存在チェックされます。未設定の場合はアプリケーションが起動しません。

| 設定キー | 必須 | 説明 | バリデーション |
|---------|------|------|-------------|
| `Jwt:Issuer` | ✅ | トークン発行者（iss クレーム） | 空文字不可 |
| `Jwt:Audience` | ✅ | トークン受信者（aud クレーム） | 空文字不可 |
| `Jwt:SigningKey` | ⚡ | HS256 対称鍵（Authority 未設定時は必須） | 32 文字以上 |
| `Jwt:Authority` | ⚡ | OIDC Discovery エンドポイント（SigningKey 未設定時は必須） | — |
| `Cors:AllowedOrigins` | ✅ | 許可オリジン配列 | 1 件以上 |

> ⚡ `SigningKey` と `Authority` はいずれか一方が必須です。両方未設定の場合は起動時にエラーになります。

### YARP ルーティング設定

`appsettings.json` の `ReverseProxy` セクションに 18 ルート / 8 クラスターが定義されています。

| ルート | パス | 認証 | レート制限 |
|--------|------|------|----------|
| auth-route | `/api/auth/{**catch-all}` | anonymous | login (5/min/IP) |
| products-route | `/api/products/{**catch-all}` | anonymous | products (300/min/IP) |
| cart-items-route | `/api/cart/items` | anonymous | anonymous-api (30/min/IP) |
| ai-recommendations-route | `/api/recommendations/{**catch-all}` | anonymous | ai-api (10/min/IP) |
| ai-search-route | `/api/search/{**catch-all}` | anonymous | ai-api (10/min/IP) |
| coupons-public-route | `/api/coupons` (GET) | anonymous | anonymous-api (30/min/IP) |
| cart-checkout-route | `/api/cart/checkout` | 認証必須 | checkout (10/min/user) |
| その他 11 ルート | `/api/{service}/{**catch-all}` | 認証必須 | user-based (120/min/user) |

### レート制限ポリシー一覧

| ポリシー名 | 種類 | 制限値 | パーティションキー | 用途 |
|-----------|------|--------|-----------------|------|
| `login` | Fixed Window | 5 req/min | IP アドレス | ブルートフォース防止 |
| `checkout` | Fixed Window | 10 req/min | ユーザー ID | 決済の連打防止 |
| `products` | Token Bucket | 300 req/min | IP アドレス | 商品一覧の高トラフィック対応 |
| `anonymous-api` | Token Bucket | 30 req/min | IP アドレス | 匿名エンドポイントの DoS 防止 |
| `ai-api` | Token Bucket | 10 req/min | IP アドレス | LLM API コスト暴走防止 |
| `user-based` | Token Bucket | 120 req/min | ユーザー ID | 認証済みユーザーの汎用制限 |

---

## アーキテクチャ概要

### ミドルウェアパイプライン（登録順序）

```
Request
  │
  ├─ 1. UseExceptionHandler()      ← 全例外をキャッチ（RFC 9457 Problem Details）
  ├─ 2. UseForwardedHeaders()      ← リバースプロキシ背後の実 IP 復元
  ├─ 3. UseHsts()                  ← HSTS ヘッダー
  ├─ 4. UseHttpsRedirection()      ← HTTPS リダイレクト
  ├─ 5. UseSecurityHeaders()       ← OWASP セキュリティヘッダー
  ├─ 6. ResponseTimeMiddleware     ← 処理時間計測 + メトリクス
  ├─ 7. UseCorrelationId()         ← 相関 ID 付与・伝搬
  ├─ 8. UseSerilogRequestLogging() ← 構造化リクエストログ
  ├─ 9. UseCors()                  ← CORS 制御
  ├─ 10. UseAuthentication()       ← JWT Bearer 認証
  ├─ 11. UseAuthorization()        ← ロールベース認可
  ├─ 12. UseRateLimiter()          ← レート制限
  └─ 13. MapReverseProxy()         ← YARP プロキシ転送
  │
Response
```

### バックエンドサービス接続先

| クラスター | アドレス | タイムアウト |
|-----------|---------|------------|
| auth-cluster | `http://auth-service:5001` | 2s |
| user-cluster | `http://user-management-service:5002` | 3s |
| inventory-cluster | `http://inventory-management-service:5003` | 5s |
| sales-cluster | `http://sales-management-service:5004` | 4s |
| payment-cart-cluster | `http://payment-cart-service:5005` | 10s |
| coupons-cluster | `http://coupon-service:5006` | 3s |
| points-cluster | `http://point-service:5007` | 3s |
| ai-cluster | `http://ai-support-service:5009` | 5s |

---

## トラブルシューティング

### 起動時に `OptionsValidationException` が発生する

```
Microsoft.Extensions.Options.OptionsValidationException:
  Jwt:Issuer は必須です
```

**原因**: JWT 設定が未設定の状態で起動しています。

**対処法**:

```bash
cd Services/ApiGateway

# 方法 1: user-secrets を設定
dotnet user-secrets set "Jwt:Issuer" "https://dev.skieshop.com"
dotnet user-secrets set "Jwt:Audience" "skieshop-dev-api"
dotnet user-secrets set "Jwt:SigningKey" "dev-only-signing-key-do-not-use-in-production-minimum-32-chars"

# 方法 2: Development 環境で起動（appsettings.Development.json のデフォルト値を使用）
ASPNETCORE_ENVIRONMENT=Development dotnet run
```

### `Jwt:SigningKey は最低 32 文字以上` エラー

**原因**: SigningKey が短すぎます（HS256 は 256 bit = 32 文字以上が必要）。

**対処法**: 32 文字以上の鍵を設定してください。

```bash
dotnet user-secrets set "Jwt:SigningKey" "your-very-long-secret-key-at-least-32-characters"
```

### `Jwt:SigningKey または Jwt:Authority のいずれかを設定してください` エラー

**原因**: `Jwt:SigningKey` と `Jwt:Authority` の両方が空または未設定です。

**対処法**: いずれか一方を設定してください。

- **HS256（対称鍵）**: `Jwt:SigningKey` に 32 文字以上の秘密鍵を設定
- **RS256/ES256（JWKS）**: `Jwt:Authority` に OpenID Connect Discovery エンドポイントを設定

### `Cors:AllowedOrigins は必須です` エラー

**原因**: CORS の許可オリジンが未設定です。

**対処法**: `appsettings.Development.json` または環境変数でオリジンを設定してください。

```bash
export Cors__AllowedOrigins__0="http://localhost:3000"
```

### バックエンドサービスへの転送が 502 エラーになる

**原因**: バックエンドサービス（AuthService 等）が起動していません。

**対処法**: ゲートウェイ単体では YARP のプロキシ転送テストはできません。バックエンドサービスを起動するか、.NET Aspire の `AppHost` で全サービスを一括起動してください。

### テストが `OptionsValidationException` で失敗する

**原因**: テスト用 `GatewayWebApplicationFactory` に JWT / CORS のテスト設定が注入されていません。

**対処法**: `GatewayWebApplicationFactory.ConfigureWebHost` 内で `AddInMemoryCollection` によりテスト値が注入されていることを確認してください（通常は実装済みです）。

---

## ライセンス

本プロジェクトは SkiShop EC プラットフォームの一部です。
