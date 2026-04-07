# ApiGateway 修正計画書

## メタデータ

| 項目 | 値 |
|------|-----|
| 作成日時 | 2026-04-07 13:19 |
| 最終更新 | 2026-04-07 13:30 |
| 対象サービス | Services/ApiGateway |
| 参照レポート | .github/review-reports/code-review/ApiGateway/check-report-1.md |
| 総指摘件数（コードレビュー） | Critical: 5, High: 12, Medium: 18, Low: 8 |
| 総指摘件数（ルーティング検証） | Critical: 3, High: 5, Medium: 2 |
| 合計 | **Critical: 8, High: 17, Medium: 20, Low: 8 = 53 件** |

---

## 目次

1. [Phase 1: Critical 課題の修正（即時対応）](#phase-1-critical-課題の修正即時対応)
2. [Phase 2: High 課題の修正（リリース前対応）](#phase-2-high-課題の修正リリース前対応)
3. [Phase 3: Medium 課題の修正（品質向上）](#phase-3-medium-課題の修正品質向上)
4. [Phase 4: Low 課題の修正（改善推奨）](#phase-4-low-課題の修正改善推奨)
5. [Phase 5: テストカバレッジ拡充](#phase-5-テストカバレッジ拡充)

---

## Phase 1: Critical 課題の修正（即時対応）

### 1.1 C-1: DI 未登録によるランタイムクラッシュリスク

**対象ファイル**: `Services/ApiGateway/Program.cs`

**現状の問題**:
以下のコンポーネントが実装済みだが DI コンテナに登録されていない:
- `TimeProvider` — `CircuitBreakerService` の依存
- `GatewayMetrics` — メトリクス機能
- `ICircuitBreakerService` / `CircuitBreakerService` — サーキットブレーカー機能
- `IFallbackService` / `FallbackService` — フォールバック機能
- `IDistributedCache` (Redis) — FallbackService / AuthCacheInvalidationConsumer の依存

**修正内容**:

`Program.cs` の L200 付近（`AddHealthChecks()` の前）に以下を追加:

```csharp
// ─────────────────────────────────────────────────────────────
// C-1 対応: 実装済みコンポーネントの DI 登録
// ─────────────────────────────────────────────────────────────

// TimeProvider（テスタビリティ確保）
builder.Services.AddSingleton(TimeProvider.System);

// メトリクス
builder.Services.AddSingleton<GatewayMetrics>();

// サーキットブレーカー / フォールバック
builder.Services.AddSingleton<ICircuitBreakerService, CircuitBreakerService>();
builder.Services.AddSingleton<IFallbackService, FallbackService>();

// Redis 分散キャッシュ（FallbackService / AuthCacheInvalidationConsumer の依存）
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "ApiGateway:";
    });
}
else
{
    // 開発環境用: Redis 未設定時はインメモリキャッシュを使用
    builder.Services.AddDistributedMemoryCache();
}
```

**追加必要な using**:
```csharp
using ApiGateway.Infrastructure.Resilience;
using ApiGateway.Infrastructure.Logging;
```

**appsettings.json に追加**:
```json
"ConnectionStrings": {
  "Redis": ""
}
```

**appsettings.Development.json に追加**:
```json
"ConnectionStrings": {
  "Redis": "localhost:6379"
}
```

---

### 1.2 C-2: BackgroundService 未登録

**対象ファイル**: `Services/ApiGateway/Program.cs`

**現状の問題**:
`AuthCacheInvalidationConsumer` が `BackgroundService` として実装されているが、`AddHostedService<T>()` で登録されていない。

**修正内容**:

`Program.cs` の DI 登録セクション（C-1 の追加箇所の後）に以下を追加:

```csharp
// Kafka 認証キャッシュ無効化コンシューマー（BackgroundService）
builder.Services.AddHostedService<AuthCacheInvalidationConsumer>();
```

**追加必要な using**:
```csharp
using ApiGateway.Infrastructure.Messaging;
```

**KafkaSettings の追加** (`Configurations/KafkaSettings.cs` 新規作成):

```csharp
namespace ApiGateway.Configurations;

/// <summary>
/// Kafka 接続設定。
/// </summary>
public record KafkaSettings
{
    /// <summary>Kafka ブローカーのアドレス（カンマ区切り）。</summary>
    public string BootstrapServers { get; init; } = string.Empty;

    /// <summary>コンシューマーグループ ID。</summary>
    public string GroupId { get; init; } = "api-gateway-group";
}
```

**Program.cs に追加**:
```csharp
// Kafka 設定（C-2 対応）
builder.Services.Configure<KafkaSettings>(
    builder.Configuration.GetSection("Kafka"));
```

**appsettings.json に追加**:
```json
"Kafka": {
  "BootstrapServers": "",
  "GroupId": "api-gateway-group"
}
```

---

### 1.3 C-3: PII マスキング未有効化

**対象ファイル**: `Services/ApiGateway/Program.cs` (L34-39)

**現状の問題**:
`PiiMaskingEnricher` は実装済みだが、Serilog 設定で `.Enrich.With<PiiMaskingEnricher>()` が呼び出されていない。

**修正内容**:

```csharp
// 修正前
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "ApiGateway")
        .WriteTo.Console(new CompactJsonFormatter()));

// 修正後
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.With<PiiMaskingEnricher>()  // C-3 対応: PII マスキング有効化
        .Enrich.WithProperty("ServiceName", "ApiGateway")
        .WriteTo.Console(new CompactJsonFormatter()));
```

---

### 1.4 C-4: ミドルウェア未登録（Dead Code）

**対象ファイル**: `Services/ApiGateway/Program.cs` (L399-411)

**現状の問題**:
`CircuitBreakerMiddleware` と `StatusCodeMiddleware` が実装済みだがパイプラインに登録されていない。

**修正内容**:

```csharp
// 修正前 (L399-411)
app.UseForwardedHeaders();
app.UseHsts();
app.UseHttpsRedirection();
app.UseSecurityHeaders();
app.UseMiddleware<ResponseTimeMiddleware>();
app.UseCorrelationId();
app.UseSerilogRequestLogging();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapReverseProxy();

// 修正後
app.UseForwardedHeaders();
app.UseHsts();
app.UseHttpsRedirection();
app.UseSecurityHeaders();
app.UseMiddleware<ResponseTimeMiddleware>();
app.UseCorrelationId();
app.UseSerilogRequestLogging();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// C-4 対応: 404/405 等の RFC 9457 Problem Details 化
app.UseMiddleware<StatusCodeMiddleware>();

// C-4 対応: サーキットブレーカー（YARP 転送前のクラスター状態チェック）
app.UseMiddleware<CircuitBreakerMiddleware>();

app.MapReverseProxy();
```

---

### 1.5 C-5: JWT SigningKey の appsettings 直接記述

**対象ファイル**: `Services/ApiGateway/appsettings.Development.json`

**現状の問題**:
JWT 署名キーが設定ファイルに直接記述されている（L14）。

**エスカレーション事項**: 秘密情報管理方式の選択

| 案 | 方式 | メリット | デメリット |
|----|------|---------|----------|
| **案 A（推奨）** | `dotnet user-secrets` | Git 管理外、開発者ごとに独立 | ローカル開発専用 |
| 案 B | 環境変数 | CI/CD 連携容易 | 開発時に毎回設定必要 |
| 案 C | そのまま（開発用のみ許容） | 変更不要 | セキュリティベストプラクティス違反 |

**採用案**: **案 A（dotnet user-secrets）**

**修正内容**:

1. `appsettings.Development.json` から `SigningKey` を削除:

```json
// 修正前
{
  "Jwt": {
    "Issuer": "https://dev.skieshop.com",
    "Audience": "skieshop-dev-api",
    "SigningKey": "dev-only-signing-key-do-not-use-in-production-minimum-32-chars"
  }
}

// 修正後
{
  "Jwt": {
    "Issuer": "https://dev.skieshop.com",
    "Audience": "skieshop-dev-api"
  }
}
```

2. README または CONTRIBUTING.md に以下を追記:

```markdown
### 開発環境のセットアップ

#### JWT 署名キーの設定

```bash
cd Services/ApiGateway
dotnet user-secrets init
dotnet user-secrets set "Jwt:SigningKey" "dev-only-signing-key-do-not-use-in-production-minimum-32-chars"
```
```

---

### 1.6 R-C1: API バージョンプレフィックスの不整合

**対象ファイル**: `Services/ApiGateway/appsettings.json`

**現状の問題**:
全バックエンドサービスは `/api/v1/...` のバージョン付きパスを使用しているが、API Gateway の YARP 設定は `/api/...` から `/...` への変換のみ行っており、`/v1` が欠落する。

**エスカレーション事項**: パス変換戦略の選択

| 案 | 方式 | メリット | デメリット |
|----|------|---------|----------|
| **案 A（推奨）** | Gateway 側で `/api/v1/` プレフィックスを使用 | クライアントが明示的にバージョンを指定 | 全ルート設定の変更が必要 |
| 案 B | Transform でバージョン付与 | 既存クライアント互換 | Transform 設定が複雑化 |
| 案 C | バックエンドのパスを変更 | Gateway 側変更不要 | 9 サービス全ての変更が必要 |

**採用案**: **案 A（Gateway 側で `/api/v1/` プレフィックスを使用）**

**修正内容**:

`appsettings.json` の全 YARP ルートを以下のように変更:

```json
// 修正前（例: auth-route）
"auth-route": {
  "ClusterId": "auth-cluster",
  "AuthorizationPolicy": "anonymous",
  "RateLimiterPolicy": "login",
  "Match": {
    "Path": "/api/auth/{**catch-all}"
  },
  "Transforms": [{ "PathRemovePrefix": "/api" }]
}

// 修正後
"auth-route": {
  "ClusterId": "auth-cluster",
  "AuthorizationPolicy": "anonymous",
  "RateLimiterPolicy": "login",
  "Match": {
    "Path": "/api/v1/auth/{**catch-all}"
  },
  "Transforms": [{ "PathRemovePrefix": "/api" }]
}
```

**全ルートの Match.Path 修正一覧**:

| ルート名 | 修正前 | 修正後 |
|---------|-------|-------|
| auth-route | `/api/auth/{**catch-all}` | `/api/v1/auth/{**catch-all}` |
| users-route | `/api/users/{**catch-all}` | `/api/v1/users/{**catch-all}` |
| products-route | `/api/products/{**catch-all}` | `/api/products/{**catch-all}` ※InventoryService は `/api/products` |
| inventory-route | `/api/inventory/{**catch-all}` | `/api/inventory/{**catch-all}` ※同上 |
| orders-route | `/api/orders/{**catch-all}` | `/api/v1/orders/{**catch-all}` |
| reports-route | `/api/reports/{**catch-all}` | `/api/v1/reports/{**catch-all}` |
| cart-items-route | `/api/cart/items` | `/api/v1/cart/items` |
| cart-checkout-route | `/api/cart/checkout` | `/api/v1/cart/checkout` |
| cart-route | `/api/cart/{**catch-all}` | `/api/v1/cart/{**catch-all}` |
| payments-route | `/api/payments/{**catch-all}` | `/api/v1/payments/{**catch-all}` |
| points-route | `/api/points/{**catch-all}` | `/api/v1/points/{**catch-all}` |
| coupons-public-route | `/api/coupons` | `/api/v1/coupons/available` |
| coupons-apply-route | `/api/coupons/apply` | `/api/v1/coupons/apply` |
| coupons-route | `/api/coupons/{**catch-all}` | `/api/v1/coupons/{**catch-all}` |
| ai-recommendations-route | `/api/recommendations/{**catch-all}` | `/api/v1/ai/recommendations/{**catch-all}` |
| ai-search-route | `/api/search/{**catch-all}` | `/api/v1/ai/search/{**catch-all}` |
| ai-chat-route | `/api/chat/{**catch-all}` | `/api/v1/ai/chat/{**catch-all}` |
| ai-analytics-route | `/api/analytics/{**catch-all}` | `/api/v1/admin/ai/analytics/{**catch-all}` |

**注意**: InventoryManagementService は `/api/products` （`/v1` なし）を使用しているため、products-route と inventory-route は変更不要。

---

### 1.7 R-C2: MailSendService のルーティング未定義

**対象ファイル**: `Services/ApiGateway/appsettings.json`

**現状の問題**:
MailSendService（ポート 5008）の YARP クラスター・ルート定義が存在しない。

**修正内容**:

1. **Clusters セクションに追加** (`appsettings.json` L310 付近):

```json
"mail-cluster": {
  "LoadBalancingPolicy": "RoundRobin",
  "HealthCheck": {
    "Active": {
      "Enabled": true,
      "Interval": "00:00:30",
      "Timeout": "00:00:03",
      "Path": "/health"
    }
  },
  "Destinations": {
    "destination1": {
      "Address": "http://mail-send-service:5008"
    }
  }
}
```

2. **Routes セクションに追加** (`appsettings.json` L195 付近):

```json
"mail-admin-route": {
  "ClusterId": "mail-cluster",
  "AuthorizationPolicy": "AdminOrManager",
  "RateLimiterPolicy": "user-based",
  "Match": {
    "Path": "/api/admin/mail/{**catch-all}"
  },
  "Transforms": [{ "PathRemovePrefix": "/api" }]
}
```

3. **Program.cs の ConfigureHttpClient に追加** (L324 付近):

```csharp
"mail-cluster" => TimeSpan.FromSeconds(5),
```

4. **CircuitBreakerService.cs の ClusterSettings に追加** (L55 付近):

```csharp
["mail-cluster"] = new(0.50, TimeSpan.FromSeconds(60), 5, TimeSpan.FromSeconds(30)),
```

5. **CircuitBreakerMiddleware.cs の PathToClusterMappings に追加** (L30 付近):

```csharp
("/api/admin/mail/", "mail-cluster"),
```

---

### 1.8 R-C3: InventoryManagementService の主要エンドポイント未ルーティング

**対象ファイル**: `Services/ApiGateway/appsettings.json`

**現状の問題**:
InventoryManagementService の categories, prices, reviews, size-guides エンドポイントがルーティングされていない。

**修正内容**:

**Routes セクションに追加** (`appsettings.json` L60 付近、inventory-route の後):

```json
"categories-route": {
  "ClusterId": "inventory-cluster",
  "AuthorizationPolicy": "anonymous",
  "RateLimiterPolicy": "products",
  "Match": {
    "Path": "/api/categories/{**catch-all}"
  },
  "Transforms": [{ "PathRemovePrefix": "/api" }]
},
"categories-admin-route": {
  "ClusterId": "inventory-cluster",
  "AuthorizationPolicy": "AdminOrManager",
  "RateLimiterPolicy": "user-based",
  "Match": {
    "Path": "/api/categories",
    "Methods": ["POST", "PATCH"]
  },
  "Transforms": [{ "PathRemovePrefix": "/api" }],
  "Order": 1
},
"prices-route": {
  "ClusterId": "inventory-cluster",
  "AuthorizationPolicy": "anonymous",
  "RateLimiterPolicy": "products",
  "Match": {
    "Path": "/api/prices/{**catch-all}"
  },
  "Transforms": [{ "PathRemovePrefix": "/api" }]
},
"prices-admin-route": {
  "ClusterId": "inventory-cluster",
  "AuthorizationPolicy": "AdminOrManager",
  "RateLimiterPolicy": "user-based",
  "Match": {
    "Path": "/api/prices",
    "Methods": ["POST", "PATCH"]
  },
  "Transforms": [{ "PathRemovePrefix": "/api" }],
  "Order": 1
},
"reviews-route": {
  "ClusterId": "inventory-cluster",
  "AuthorizationPolicy": "default",
  "RateLimiterPolicy": "user-based",
  "Match": {
    "Path": "/api/reviews/{**catch-all}"
  },
  "Transforms": [{ "PathRemovePrefix": "/api" }]
},
"size-guides-route": {
  "ClusterId": "inventory-cluster",
  "AuthorizationPolicy": "anonymous",
  "RateLimiterPolicy": "products",
  "Match": {
    "Path": "/api/size-guides/{**catch-all}"
  },
  "Transforms": [{ "PathRemovePrefix": "/api" }]
}
```

**CircuitBreakerMiddleware.cs の PathToClusterMappings に追加**:

```csharp
("/api/categories/", "inventory-cluster"),
("/api/prices/", "inventory-cluster"),
("/api/reviews/", "inventory-cluster"),
("/api/size-guides/", "inventory-cluster"),
```

---

## Phase 2: High 課題の修正（リリース前対応）

### 2.1 H-1: Kafka Consumer の同期ブロッキング

**対象ファイル**: `Services/ApiGateway/Infrastructure/Messaging/AuthCacheInvalidationConsumer.cs` (L66)

**現状の問題**:
`consumer.Consume(stoppingToken)` が同期呼び出しされており、スレッドをブロックする。

**エスカレーション事項**: 非同期化の方式選択

| 案 | 方式 | メリット | デメリット |
|----|------|---------|----------|
| **案 A（推奨）** | `Task.Run` でラップ + `stoppingToken` 監視 | 実装シンプル | 追加スレッド消費 |
| 案 B | `Confluent.Kafka` の `ConsumeAsync`（ライブラリ未サポート） | 理想的 | 現時点で API 未提供 |
| 案 C | Channel&lt;T&gt; パターン | 高度な非同期制御 | 実装複雑 |

**採用案**: **案 A**

**修正内容**:

```csharp
// 修正前 (L66)
var result = consumer.Consume(stoppingToken);

// 修正後
// H-1 対応: Kafka の同期 Consume を非ブロッキング化
var result = await Task.Run(() => consumer.Consume(stoppingToken), stoppingToken);
```

---

### 2.2 H-2: YARP ヘルスチェック URL ハードコード

**対象ファイル**: `Services/ApiGateway/appsettings.json`

**現状の問題**:
全クラスターのヘルスチェック URL が `http://localhost:XXXX/health` でハードコードされている。

**確認結果**: 
現在の設定を確認したところ、すでに DNS 名（`http://auth-service:5001` 等）を使用しており、この問題は実際には存在しない。ただし、`appsettings.Development.json` でローカル開発用のオーバーライドが必要。

**修正内容**: なし（確認のみ）

---

### 2.3 H-3: RequiredHeaderMiddleware 無効化

**対象ファイル**: `Services/ApiGateway/Infrastructure/Middleware/RequiredHeaderMiddleware.cs`

**現状の問題**:
現在の実装は正しく機能している。`ContentLength > 0` の条件で空ボディのリクエストをスキップしており、意図した動作。

**修正内容**: なし（確認のみ）

---

### 2.4 H-4: カスタムレート制限の代替キー不足

**対象ファイル**: `Services/ApiGateway/Program.cs` (L243-251, L289-299)

**現状の問題**:
`checkout` / `user-based` ポリシーで `ClaimTypes.NameIdentifier` が取得できない場合、`"anonymous"` が使用されるが、これでは全匿名ユーザーで共有されてしまう。

**修正内容**:

```csharp
// 修正前 (L243-251)
options.AddPolicy("checkout", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

// 修正後
options.AddPolicy("checkout", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown",  // H-4 対応: IP アドレスをフォールバックキーに
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
```

同様に `user-based` ポリシー (L289-299) も修正。

---

### 2.5 H-5: サーキットブレーカー状態永続化なし

**対象ファイル**: `Services/ApiGateway/Infrastructure/Resilience/CircuitBreakerService.cs`

**現状の問題**:
サーキットブレーカー状態がインメモリで管理されており、Pod 再起動時にリセットされる。

**エスカレーション事項**: 状態永続化の方式選択

| 案 | 方式 | メリット | デメリット |
|----|------|---------|----------|
| 案 A | Redis で状態共有 | 分散環境対応 | 実装複雑、Redis 依存増加 |
| 案 B | Polly の分散サーキットブレーカー（`Polly.Extensions.Http`） | 標準ライブラリ | 学習コスト |
| **案 C（推奨）** | 現状維持 + ドキュメント化 | 実装変更なし | 分散環境では各 Pod が独立動作 |

**採用案**: **案 C（現状維持 + ドキュメント化）**

**理由**: API Gateway は通常 1〜2 レプリカで運用され、各 Pod が独立してサーキット状態を管理しても実用上問題ない。Redis 永続化は将来の拡張として検討する。

**修正内容**:

`CircuitBreakerService.cs` のクラスコメントに以下を追記:

```csharp
/// <para>
/// 【注意】サーキット状態はインメモリで管理されるため、Pod 再起動時にリセットされます。
/// 分散環境で状態を共有する必要がある場合は、Redis ベースの実装に切り替えてください。
/// </para>
```

---

### 2.6 H-6: OpenTelemetry Exporter 未設定

**対象ファイル**: `Services/ApiGateway/Program.cs` (L189-199), `appsettings.json`

**現状の問題**:
`AddOtlpExporter()` が呼び出されていないため、トレースデータが外部に送信されない。

**修正内容**:

1. **Program.cs (L189-199) を修正**:

```csharp
// 修正前
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("SkiShop.ApiGateway"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(GatewayMetrics.MeterName));

// 修正後
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("SkiShop.ApiGateway");
        
        // H-6 対応: OTLP Exporter 設定
        if (!string.IsNullOrEmpty(otlpEndpoint))
        {
            tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter(GatewayMetrics.MeterName);
        
        if (!string.IsNullOrEmpty(otlpEndpoint))
        {
            metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    });
```

2. **ApiGateway.csproj にパッケージ追加**:

```xml
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.*" />
```

3. **appsettings.json に追加**:

```json
"OpenTelemetry": {
  "OtlpEndpoint": ""
}
```

4. **appsettings.Production.json に追加**:

```json
"OpenTelemetry": {
  "OtlpEndpoint": "http://otel-collector:4317"
}
```

---

### 2.7 H-7: ValidateOnStart 不足

**対象ファイル**: `Services/ApiGateway/Program.cs`

**現状の問題**:
`RateLimitSettings` は `IOptions<T>` パターンで登録されていない。

**確認結果**: 
`RateLimitSettings` は現在 `appsettings.json` から直接読み込まれており、IOptions パターンを使用していない。`CorsSettings` は ValidateOnStart が設定済み。

**修正内容**: 

`RateLimitSettings.cs` にバリデーションを追加し、`Program.cs` で ValidateOnStart を追加:

```csharp
// Configurations/RateLimitSettings.cs
using System.ComponentModel.DataAnnotations;

namespace ApiGateway.Configurations;

public record RateLimitSettings
{
    [Range(1, 10000)]
    public int DefaultPermitLimit { get; init; } = 100;

    [Range(1, 10000)]
    public int LoginPermitLimit { get; init; } = 5;

    [Range(1, 10000)]
    public int CheckoutPermitLimit { get; init; } = 10;
}
```

```csharp
// Program.cs に追加
builder.Services.AddOptions<RateLimitSettings>()
    .Bind(builder.Configuration.GetSection("RateLimiting"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

---

### 2.8 R-H1: SalesManagementService の配送・返品エンドポイント未ルーティング

**対象ファイル**: `Services/ApiGateway/appsettings.json`

**修正内容**:

**Routes セクションに追加**:

```json
"shipments-route": {
  "ClusterId": "sales-cluster",
  "AuthorizationPolicy": "AdminOrManager",
  "RateLimiterPolicy": "user-based",
  "Match": {
    "Path": "/api/v1/shipments/{**catch-all}"
  },
  "Transforms": [{ "PathRemovePrefix": "/api" }]
},
"returns-route": {
  "ClusterId": "sales-cluster",
  "AuthorizationPolicy": "default",
  "RateLimiterPolicy": "user-based",
  "Match": {
    "Path": "/api/v1/returns/{**catch-all}"
  },
  "Transforms": [{ "PathRemovePrefix": "/api" }]
}
```

**CircuitBreakerMiddleware.cs の PathToClusterMappings に追加**:

```csharp
("/api/v1/shipments/", "sales-cluster"),
("/api/v1/returns/", "sales-cluster"),
```

---

### 2.9 R-H2: PaymentCartService のゲストチェックアウト未ルーティング

**対象ファイル**: `Services/ApiGateway/appsettings.json`

**修正内容**:

**Routes セクションに追加** (cart-checkout-route の前に Order: 0 で):

```json
"checkout-guest-route": {
  "ClusterId": "payment-cart-cluster",
  "AuthorizationPolicy": "anonymous",
  "RateLimiterPolicy": "checkout",
  "Match": {
    "Path": "/api/v1/checkout/guest",
    "Methods": ["POST"]
  },
  "Transforms": [{ "PathRemovePrefix": "/api" }],
  "Order": 0
}
```

---

### 2.10 R-H3: Admin エンドポイントの体系的な未ルーティング

**対象ファイル**: `Services/ApiGateway/appsettings.json`

**修正内容**:

**Routes セクションに追加**:

```json
"admin-users-route": {
  "ClusterId": "user-cluster",
  "AuthorizationPolicy": "AdminOrManager",
  "RateLimiterPolicy": "user-based",
  "Match": {
    "Path": "/api/v1/admin/users/{**catch-all}"
  },
  "Transforms": [{ "PathRemovePrefix": "/api" }]
},
"admin-campaigns-route": {
  "ClusterId": "coupons-cluster",
  "AuthorizationPolicy": "AdminOrManager",
  "RateLimiterPolicy": "user-based",
  "Match": {
    "Path": "/api/v1/admin/campaigns/{**catch-all}"
  },
  "Transforms": [{ "PathRemovePrefix": "/api" }]
},
"admin-coupons-route": {
  "ClusterId": "coupons-cluster",
  "AuthorizationPolicy": "AdminOrManager",
  "RateLimiterPolicy": "user-based",
  "Match": {
    "Path": "/api/v1/admin/coupons/{**catch-all}"
  },
  "Transforms": [{ "PathRemovePrefix": "/api" }]
},
"admin-points-route": {
  "ClusterId": "points-cluster",
  "AuthorizationPolicy": "AdminOrManager",
  "RateLimiterPolicy": "user-based",
  "Match": {
    "Path": "/api/v1/admin/points/{**catch-all}"
  },
  "Transforms": [{ "PathRemovePrefix": "/api" }]
},
"admin-tiers-route": {
  "ClusterId": "points-cluster",
  "AuthorizationPolicy": "AdminOrManager",
  "RateLimiterPolicy": "user-based",
  "Match": {
    "Path": "/api/v1/admin/tiers/{**catch-all}"
  },
  "Transforms": [{ "PathRemovePrefix": "/api" }]
},
"admin-ai-forecast-route": {
  "ClusterId": "ai-cluster",
  "AuthorizationPolicy": "AdminOrManager",
  "RateLimiterPolicy": "user-based",
  "Match": {
    "Path": "/api/v1/admin/ai/forecast/{**catch-all}"
  },
  "Transforms": [{ "PathRemovePrefix": "/api" }]
},
"admin-ai-models-route": {
  "ClusterId": "ai-cluster",
  "AuthorizationPolicy": "AdminOrManager",
  "RateLimiterPolicy": "user-based",
  "Match": {
    "Path": "/api/v1/admin/ai/models/{**catch-all}"
  },
  "Transforms": [{ "PathRemovePrefix": "/api" }]
}
```

**CircuitBreakerMiddleware.cs の PathToClusterMappings に追加**:

```csharp
("/api/v1/admin/users/", "user-cluster"),
("/api/v1/admin/campaigns/", "coupons-cluster"),
("/api/v1/admin/coupons/", "coupons-cluster"),
("/api/v1/admin/points/", "points-cluster"),
("/api/v1/admin/tiers/", "points-cluster"),
("/api/v1/admin/ai/", "ai-cluster"),
```

---

### 2.11 R-H4: PointService のティア公開エンドポイント未ルーティング

**対象ファイル**: `Services/ApiGateway/appsettings.json`

**修正内容**:

**Routes セクションに追加**:

```json
"tiers-route": {
  "ClusterId": "points-cluster",
  "AuthorizationPolicy": "anonymous",
  "RateLimiterPolicy": "anonymous-api",
  "Match": {
    "Path": "/api/v1/tiers/{**catch-all}"
  },
  "Transforms": [{ "PathRemovePrefix": "/api" }]
}
```

---

### 2.12 R-H5: AiSupportService のパス変換不整合

**対象ファイル**: `Services/ApiGateway/appsettings.json`

**修正内容**: R-C1 で対応済み（パスを `/api/v1/ai/*` に変更）

---

### 2.13 H-8 〜 H-12: テストカバレッジ不足

**対象ファイル**: `Services/ApiGateway.Tests/`

**修正内容**: Phase 5 で対応

---

## Phase 3: Medium 課題の修正（品質向上）

### 3.1 M-1, M-2: primary constructor 未使用

**対象ファイル**: 
- `Services/ApiGateway/Infrastructure/Resilience/CircuitBreakerService.cs`
- `Services/ApiGateway/Infrastructure/Metrics/GatewayMetrics.cs`

**修正内容**:

`CircuitBreakerService.cs` を primary constructor に変更:

```csharp
// 修正前
public sealed class CircuitBreakerService : ICircuitBreakerService
{
    private readonly ConcurrentDictionary<string, ClusterCircuitState> _states = new();
    private readonly ILogger<CircuitBreakerService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly GatewayMetrics _metrics;

    public CircuitBreakerService(
        ILogger<CircuitBreakerService> logger,
        TimeProvider timeProvider,
        GatewayMetrics metrics)
    {
        _logger = logger;
        _timeProvider = timeProvider;
        _metrics = metrics;
        // ...
    }
}

// 修正後
public sealed class CircuitBreakerService(
    ILogger<CircuitBreakerService> logger,
    TimeProvider timeProvider,
    GatewayMetrics metrics) : ICircuitBreakerService
{
    private readonly ConcurrentDictionary<string, ClusterCircuitState> _states = InitializeStates(timeProvider);

    // ...（logger, timeProvider, metrics は primary constructor パラメータを直接使用）
}
```

---

### 3.2 M-3: フィールド名 `_tp` が略語

**対象ファイル**: `Services/ApiGateway/Infrastructure/Resilience/CircuitBreakerService.cs` (L238)

**修正内容**:

```csharp
// 修正前
private readonly TimeProvider _tp;

// 修正後
private readonly TimeProvider _timeProvider;
```

---

### 3.3 M-4: Problem Details の anonymous type を共有 record に抽出

**対象ファイル**: 複数のミドルウェア

**修正内容**:

`DTOs/ProblemDetailsResponse.cs` を新規作成:

```csharp
namespace ApiGateway.DTOs;

/// <summary>
/// RFC 9457 Problem Details 形式のレスポンス。
/// </summary>
public record ProblemDetailsResponse(
    string Type,
    string Title,
    int Status,
    string Detail,
    string? Instance,
    string Code,
    string TraceId);
```

各ミドルウェアで使用。

---

### 3.4 M-5: JwtSettingsValidator に `sealed` 修飾子追加

**対象ファイル**: `Services/ApiGateway/Configurations/JwtSettingsValidator.cs`

**修正内容**:

```csharp
// 修正前
public class JwtSettingsValidator : IValidateOptions<JwtSettings>

// 修正後
public sealed class JwtSettingsValidator : IValidateOptions<JwtSettings>
```

---

### 3.5 M-6: Antiforgery が Add されているが Use されていない

**確認結果**: 現在の `Program.cs` に `AddAntiforgery()` の呼び出しは存在しない。誤検出。

**修正内容**: なし

---

### 3.6 M-7: BackendServicesHealthCheck で `Task.WhenAll` 未使用

**対象ファイル**: `Services/ApiGateway/Infrastructure/HealthChecks/BackendServicesHealthCheck.cs`

**現状の問題**:
複数のバックエンドサービスへのヘルスチェックが順次実行されており、レスポンス時間が遅い。

**修正内容**:

```csharp
// 修正前（順次実行）
foreach (var service in _services)
{
    var response = await httpClient.GetAsync($"{service.Url}/health", ct);
    // ...
}

// 修正後（並列実行）
var tasks = _services.Select(async service =>
{
    try
    {
        var response = await httpClient.GetAsync($"{service.Url}/health", ct);
        return (service.Name, IsHealthy: response.IsSuccessStatusCode);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "ヘルスチェック失敗: {Service}", service.Name);
        return (service.Name, IsHealthy: false);
    }
}).ToArray();

var results = await Task.WhenAll(tasks);
// 結果を集計
```

---

### 3.7 M-8: RateLimitSettings の PermitLimit が 100

**確認結果**: 各ポリシーで個別に設定されており、適切な値が使用されている。

**修正内容**: なし

---

### 3.8.1 M-10: appsettings.Production.json の環境変数プレースホルダー

**対象ファイル**: `Services/ApiGateway/appsettings.Production.json`

**現状の問題**:
環境変数プレースホルダーのみで実際の値がない。`${VARIABLE}` 形式のプレースホルダーは .NET の標準設定プロバイダーではサポートされていない。

**修正内容**:

本番環境の設定は以下のいずれかで管理:
1. **環境変数を直接使用**: Docker Compose / Kubernetes で設定
2. **Azure App Configuration / AWS Parameter Store**: クラウドネイティブ環境

```json
// appsettings.Production.json（最小限の設定のみ記載、秘密情報は環境変数で上書き）
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "DetailedErrors": false
}
```

**Program.cs で環境変数読み込みを明示**:
```csharp
builder.Configuration.AddEnvironmentVariables(prefix: "SKIESHOP_");
```

---

### 3.8.2 M-17: Extension types / extension blocks 未採用

**対象ファイル**: 複数ファイル

**現状の問題**:
C# 14 の新機能である `extension types` / `extension blocks` が採用されていない。

**修正内容**:

現時点では採用を見送り、将来の改善項目として記録。理由:
- 既存コードが拡張メソッドで十分機能している
- チームの学習コストを考慮
- C# 14 の機能が安定した後に段階的に導入

**TODO**: Phase 6（技術的負債解消）で検討

---

### 3.8 M-9: Dockerfile のレイヤーキャッシュ効率改善

**対象ファイル**: `Services/ApiGateway/Dockerfile`

**修正内容**:

```dockerfile
# 修正前
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ApiGateway/ApiGateway.csproj", "ApiGateway/"]
RUN dotnet restore "ApiGateway/ApiGateway.csproj"
COPY . .
WORKDIR "/src/ApiGateway"
RUN dotnet publish "ApiGateway.csproj" -c Release -o /app/publish

# 修正後（レイヤーキャッシュ最適化）
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 1. プロジェクトファイルのみコピーして restore（依存関係が変わらない限りキャッシュ有効）
COPY ["ApiGateway/ApiGateway.csproj", "ApiGateway/"]
RUN dotnet restore "ApiGateway/ApiGateway.csproj"

# 2. ソースコードをコピーしてビルド
COPY ["ApiGateway/", "ApiGateway/"]
WORKDIR "/src/ApiGateway"
RUN dotnet publish "ApiGateway.csproj" -c Release -o /app/publish --no-restore
```

---

### 3.9 M-11: CorrelationIdMiddleware で外部 Correlation-ID の長さ制限

**対象ファイル**: `Services/ApiGateway/Infrastructure/Middleware/CorrelationIdMiddleware.cs`

**修正内容**:

```csharp
// 長さ制限を追加（例: 128 文字）
private const int MaxCorrelationIdLength = 128;

if (correlationId.Length > MaxCorrelationIdLength)
{
    correlationId = Guid.NewGuid().ToString();
}
```

---

### 3.10 M-12: RequestTimingMiddleware で `TimeProvider` 使用推奨

**対象ファイル**: `Services/ApiGateway/Infrastructure/Middleware/ResponseTimeMiddleware.cs`

**現状**: `Stopwatch` を使用している。

**修正内容**: `TimeProvider` を DI でインジェクションして使用（テスタビリティ向上）

---

### 3.11 M-13: RateLimitMiddleware の 429 レスポンスが RFC 9457 非準拠

**確認結果**: 現在の `OnRejected` ハンドラー (Program.cs L209-229) は RFC 9457 準拠の Problem Details 形式を返している。

**修正内容**: なし

---

### 3.12 M-14, M-15: テスト命名パターン・Trait 未設定

**修正内容**: Phase 5 で対応

---

### 3.13 M-16: FallbackService のフォールバックレスポンス改善

**対象ファイル**: `Services/ApiGateway/Infrastructure/Resilience/FallbackService.cs`

**修正内容**: 各サービス固有のフォールバックメッセージを追加

---

### 3.14 M-18: ミドルウェアパイプラインのコメント追加

**対象ファイル**: `Services/ApiGateway/Program.cs` (L347-359)

**現状**: すでに詳細なコメントが存在する。

**修正内容**: なし

---

### 3.15 R-M1: Internal エンドポイントの設計意図ドキュメント化

**対象ファイル**: `Services/ApiGateway/appsettings.json`

**修正内容**:

`appsettings.json` の Routes セクションの先頭にコメントを追加:

```json
"Routes": {
  "_comment": "Internal エンドポイント（/api/v1/internal/*）はサービス間通信専用のため、意図的に API Gateway 経由でルーティングしない。これらのエンドポイントは Kubernetes の ClusterIP サービス経由で直接呼び出される。",
  // ...
}
```

---

### 3.16 R-M2: YARP 認可ポリシー名と定義の照合

**対象ファイル**: `Services/ApiGateway/Program.cs`

**確認結果**: 

YARP で使用されているポリシー名と Program.cs の定義:
- `anonymous` → `AllowAnonymous()` で処理（YARP の AuthorizationPolicy 設定）
- `default` → FallbackPolicy（認証済みユーザー必須）
- `AdminOnly` → L182 で定義済み
- `AdminOrManager` → L183 で定義済み

YARP の `AuthorizationPolicy: "anonymous"` は YARP の組み込み機能で認証をスキップする。

**修正内容**: なし（全ポリシーが正しく定義されている）

---

## Phase 4: Low 課題の修正（改善推奨）

### 4.1 L-1: Dockerfile の HEALTHCHECK start-period

**対象ファイル**: `Services/ApiGateway/Dockerfile` (L19)

**現状**: `--start-period=30s`（現在の設定は適切）

**修正内容**: なし

---

### 4.2 L-3: デフォルトログレベルが Information

**対象ファイル**: `Services/ApiGateway/appsettings.json`

**現状**: `"Default": "Warning"` で設定済み。

**修正内容**: なし

---

### 4.3 L-7: パッケージバージョンが `*` 指定

**対象ファイル**: `Services/ApiGateway/ApiGateway.csproj`

**現状の問題**: 一部パッケージでワイルドカード指定。

**修正内容**: 本番環境では固定バージョンを推奨するが、メジャーバージョン固定（`10.*`）は許容範囲内。

---

### 4.4 L-8: HSTS の MaxAge

**対象ファイル**: `Services/ApiGateway/Program.cs`

**現状**: `app.UseHsts()` はデフォルトで 30 日（2592000 秒）。

**修正内容**:

```csharp
// 修正前
app.UseHsts();

// 修正後
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
```

`appsettings.Production.json` に追加:
```json
"Hsts": {
  "MaxAge": "365:00:00:00"
}
```

---

### 4.5 L-2: XML ドキュメントコメントの不足

**対象ファイル**: 全パブリック API を持つクラス

**現状の問題**:
パブリックメソッド・クラスに XML ドキュメントコメントが不足している。

**修正内容**:

以下のクラスに XML ドキュメントを追加:
- `ICircuitBreakerService` インターフェース
- `IFallbackService` インターフェース
- `GatewayMetrics` クラス
- 各設定クラス（`JwtSettings`, `CorsSettings`, `RateLimitSettings`）

```csharp
/// <summary>
/// サーキットブレーカーサービスのインターフェース。
/// クラスター単位でサーキット状態を管理し、障害伝播を防止する。
/// </summary>
public interface ICircuitBreakerService
{
    /// <summary>
    /// 指定されたクラスターがリクエストを許可するかを判定する。
    /// </summary>
    /// <param name="clusterId">クラスター ID</param>
    /// <returns>リクエストを許可する場合は true</returns>
    bool AllowRequest(string clusterId);
    
    // ...
}
```

**優先度**: Low（機能に影響なし）

---

### 4.6 L-4: CorsSettings の record struct 検討

**対象ファイル**: `Services/ApiGateway/Configurations/CorsSettings.cs`

**現状の問題**:
`CorsSettings` は小さな値型として `record struct` が適切な可能性がある。

**修正内容**:

```csharp
// 現状（record class）
public record CorsSettings
{
    public string[] AllowedOrigins { get; init; } = [];
}

// 変更なし — AllowedOrigins が参照型配列のため record class を維持
// record struct に変更するとデフォルトコンストラクタで null 問題が発生する可能性
```

**結論**: 修正不要（現状維持）

---

### 4.7 L-5: CSP の script-src 設定

**対象ファイル**: `Services/ApiGateway/Infrastructure/Middleware/SecurityHeadersMiddleware.cs`

**現状の問題**:
Content-Security-Policy の `script-src` に `'unsafe-inline'` がなく、インラインスクリプトがブロックされる。

**修正内容**:

```csharp
// 現状
context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'");

// 修正案（必要に応じて）
context.Response.Headers.Append("Content-Security-Policy", 
    "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'");
```

**注意**: API Gateway は主に JSON API を提供するため、HTML レスポンスが限定的。現状維持でも問題なし。
フロントエンドが Blazor の場合は `'wasm-unsafe-eval'` も検討。

---

### 4.8 L-6: テストデータの重複

**対象ファイル**: `Services/ApiGateway.Tests/` 配下の各テストファイル

**現状の問題**:
テストデータ（JWT トークン、ユーザー情報等）が各テストファイルで重複定義されている。

**修正内容**:

共有テストデータクラスを作成:

```csharp
// Tests/Fixtures/TestData.cs
namespace ApiGateway.Tests.Fixtures;

public static class TestData
{
    public const string ValidUserId = "test-user-123";
    public const string ValidEmail = "test@example.com";
    public const string AdminRole = "Admin";
    
    public static class Tokens
    {
        public const string ValidUserToken = "eyJhbGciOiJIUzI1NiIs...";
        public const string ExpiredToken = "eyJhbGciOiJIUzI1NiIs...";
        public const string InvalidToken = "invalid-token";
    }
    
    public static class ClusterIds
    {
        public const string Auth = "auth-cluster";
        public const string User = "user-cluster";
        // ...
    }
}
```

**優先度**: Low（リファクタリング）

---

## Phase 5: テストカバレッジ拡充

### 5.1 テストプロジェクトへの NSubstitute / Shouldly 追加

**対象ファイル**: `Services/ApiGateway.Tests/ApiGateway.Tests.csproj`

**修正内容**:

```xml
<ItemGroup>
  <!-- 追加 -->
  <PackageReference Include="NSubstitute" Version="5.*" />
  <PackageReference Include="Shouldly" Version="4.*" />
</ItemGroup>
```

---

### 5.2 未テストクラスのユニットテスト作成

以下のクラスに対するユニットテストを作成:

| クラス | テストファイル | 優先度 |
|-------|--------------|--------|
| `CircuitBreakerService` | `CircuitBreakerServiceTests.cs` | High |
| `FallbackService` | `FallbackServiceTests.cs` | High |
| `PiiMaskingEnricher` | `PiiMaskingEnricherTests.cs` | Medium |
| `StatusCodeMiddleware` | `StatusCodeMiddlewareTests.cs` | Medium |
| `RequiredHeaderMiddleware` | `RequiredHeaderMiddlewareTests.cs` | Medium |
| `SecurityHeadersMiddleware` | `SecurityHeadersMiddlewareTests.cs` | Low |
| `GatewayMetrics` | `GatewayMetricsTests.cs` | Low |

---

### 5.3 テスト命名パターンの適用

全テストメソッドを `Should_期待結果_When_条件` パターンに変更:

```csharp
// 修正前
[Fact]
public async Task HealthEndpoint_ReturnsOk()

// 修正後
[Fact]
[Trait("Category", "Integration")]
public async Task Should_Return200_When_HealthEndpointCalled()
```

---

## 修正ファイル一覧

| ファイル | 修正内容 | Phase |
|---------|---------|-------|
| `Program.cs` | DI 登録、ミドルウェア登録、PII マスキング、OpenTelemetry、環境変数読み込み | 1, 2, 3 |
| `appsettings.json` | YARP ルート追加、クラスター追加、パス修正 | 1, 2 |
| `appsettings.Development.json` | JWT SigningKey 削除、Redis 接続文字列 | 1 |
| `appsettings.Production.json` | OTLP エンドポイント、HSTS、最小限の設定 | 2, 3, 4 |
| `Configurations/KafkaSettings.cs` | 新規作成 | 1 |
| `Configurations/RateLimitSettings.cs` | バリデーション追加 | 2 |
| `Configurations/JwtSettingsValidator.cs` | sealed 追加 | 3 |
| `Infrastructure/Messaging/AuthCacheInvalidationConsumer.cs` | Task.Run ラップ | 2 |
| `Infrastructure/Resilience/CircuitBreakerService.cs` | primary constructor、コメント | 2, 3 |
| `Infrastructure/Resilience/CircuitBreakerMiddleware.cs` | PathToClusterMappings 追加 | 1, 2 |
| `Infrastructure/Middleware/CorrelationIdMiddleware.cs` | 長さ制限 | 3 |
| `Infrastructure/HealthChecks/BackendServicesHealthCheck.cs` | Task.WhenAll 並列化 | 3 |
| `DTOs/ProblemDetailsResponse.cs` | 新規作成 | 3 |
| `Dockerfile` | レイヤーキャッシュ最適化 | 3 |
| `ApiGateway.csproj` | OTLP Exporter パッケージ追加 | 2 |
| `ApiGateway.Tests.csproj` | NSubstitute / Shouldly 追加 | 5 |
| `Tests/Fixtures/TestData.cs` | 新規作成（共有テストデータ） | 4, 5 |
| 各テストファイル | 命名パターン、Trait 追加 | 5 |
| 各パブリック API クラス | XML ドキュメントコメント追加 | 4 |

---

## 実行順序

```
Phase 1 (Critical)
├── 1.1 C-1: DI 登録追加
├── 1.2 C-2: BackgroundService 登録
├── 1.3 C-3: PII マスキング有効化
├── 1.4 C-4: ミドルウェア登録
├── 1.5 C-5: JWT SigningKey 移行
├── 1.6 R-C1: API バージョンプレフィックス修正
├── 1.7 R-C2: MailSendService ルート追加
└── 1.8 R-C3: Inventory ルート追加

Phase 2 (High)
├── 2.1 H-1: Kafka Consumer 非同期化
├── 2.4 H-4: レート制限フォールバックキー
├── 2.6 H-6: OpenTelemetry Exporter
├── 2.7 H-7: ValidateOnStart
├── 2.8 R-H1: shipments/returns ルート
├── 2.9 R-H2: checkout/guest ルート
├── 2.10 R-H3: Admin ルート
├── 2.11 R-H4: tiers ルート
└── dotnet build で検証

Phase 3 (Medium)
├── 3.1 M-1, M-2: primary constructor
├── 3.2 M-3: フィールド名修正
├── 3.3 M-4: ProblemDetailsResponse
├── 3.4 M-5: sealed 修飾子
├── 3.6 M-7: BackendServicesHealthCheck 並列化
├── 3.8 M-9: Dockerfile 最適化
├── 3.8.1 M-10: appsettings.Production.json 整理
├── 3.9 M-11: Correlation-ID 長さ制限
└── dotnet build で検証

Phase 4 (Low)
├── 4.4 L-8: HSTS MaxAge
├── 4.5 L-2: XML ドキュメントコメント追加
├── 4.8 L-6: テストデータ共有クラス作成
└── コードレビューで確認

Phase 5 (Tests)
├── 5.1 テストパッケージ追加
├── 5.2 ユニットテスト作成
├── 5.3 テスト命名パターン
└── dotnet test で検証
```

---

## 指摘対応マトリクス

| # | 課題 ID | 対応セクション | 対応状況 |
|---|---------|---------------|---------|
| 1 | C-1 | 1.1 | ✅ 対応計画あり |
| 2 | C-2 | 1.2 | ✅ 対応計画あり |
| 3 | C-3 | 1.3 | ✅ 対応計画あり |
| 4 | C-4 | 1.4 | ✅ 対応計画あり |
| 5 | C-5 | 1.5 | ✅ 対応計画あり |
| 6 | R-C1 | 1.6 | ✅ 対応計画あり |
| 7 | R-C2 | 1.7 | ✅ 対応計画あり |
| 8 | R-C3 | 1.8 | ✅ 対応計画あり |
| 9 | H-1 | 2.1 | ✅ 対応計画あり |
| 10 | H-2 | 2.2 | ⚪ 確認済み（修正不要） |
| 11 | H-3 | 2.3 | ⚪ 確認済み（修正不要） |
| 12 | H-4 | 2.4 | ✅ 対応計画あり |
| 13 | H-5 | 2.5 | ⚪ 現状維持（ドキュメント化） |
| 14 | H-6 | 2.6 | ✅ 対応計画あり |
| 15 | H-7 | 2.7 | ✅ 対応計画あり |
| 16 | H-8〜H-12 | 5.x | ✅ テスト拡充で対応 |
| 17 | R-H1 | 2.8 | ✅ 対応計画あり |
| 18 | R-H2 | 2.9 | ✅ 対応計画あり |
| 19 | R-H3 | 2.10 | ✅ 対応計画あり |
| 20 | R-H4 | 2.11 | ✅ 対応計画あり |
| 21 | R-H5 | 2.12 | ✅ R-C1 で対応済み |
| 22 | M-1 | 3.1 | ✅ 対応計画あり |
| 23 | M-2 | 3.1 | ✅ 対応計画あり |
| 24 | M-3 | 3.2 | ✅ 対応計画あり |
| 25 | M-4 | 3.3 | ✅ 対応計画あり |
| 26 | M-5 | 3.4 | ✅ 対応計画あり |
| 27 | M-6 | 3.5 | ⚪ 確認済み（誤検出） |
| 28 | M-7 | 3.6 | ✅ 対応計画あり |
| 29 | M-8 | 3.7 | ⚪ 確認済み（修正不要） |
| 30 | M-9 | 3.8 | ✅ 対応計画あり |
| 31 | M-10 | 3.8.1 | ✅ 対応計画あり |
| 32 | M-11 | 3.9 | ✅ 対応計画あり |
| 33 | M-12 | 3.10 | ✅ 対応計画あり |
| 34 | M-13 | 3.11 | ⚪ 確認済み（修正不要） |
| 35 | M-14 | 5.3 | ✅ テスト拡充で対応 |
| 36 | M-15 | 5.3 | ✅ テスト拡充で対応 |
| 37 | M-16 | 3.13 | ✅ 対応計画あり |
| 38 | M-17 | 3.8.2 | ⚪ 将来検討 |
| 39 | M-18 | 3.14 | ⚪ 確認済み（修正不要） |
| 40 | R-M1 | 3.15 | ✅ 対応計画あり |
| 41 | R-M2 | 3.16 | ⚪ 確認済み（修正不要） |
| 42 | L-1 | 4.1 | ⚪ 確認済み（修正不要） |
| 43 | L-2 | 4.5 | ✅ 対応計画あり |
| 44 | L-3 | 4.2 | ⚪ 確認済み（修正不要） |
| 45 | L-4 | 4.6 | ⚪ 確認済み（修正不要） |
| 46 | L-5 | 4.7 | ⚪ 現状維持（API Gateway は JSON のみ） |
| 47 | L-6 | 4.8 | ✅ 対応計画あり |
| 48 | L-7 | 4.3 | ⚪ 許容範囲（メジャーバージョン固定） |
| 49 | L-8 | 4.4 | ✅ 対応計画あり |

**凡例**:
- ✅ 対応計画あり（修正実施）
- ⚪ 確認済み（修正不要 or 将来検討）

---

*修正計画書生成: 2026-04-07 13:19*
*最終更新: 2026-04-07 13:30（全課題対応確認、漏れ項目追記）*
