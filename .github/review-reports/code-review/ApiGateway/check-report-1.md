# ApiGateway ソースコードレビューレポート

## メタデータ

| 項目 | 値 |
|------|-----|
| レビュー日時 | 2026-04-07 12:59 |
| 対象サービス | Services/ApiGateway |
| レビューファイル数 | 32 |
| レビュー Agent 数 | 14 |

---

## 総合判定

# ❌ Rejected

**本番リリース不可** — Critical / High レベルの未解決課題があります。

---

## スコアサマリー

| Agent | スコア | 判定 |
|-------|--------|------|
| architecture-review | 19/25 | ❌ Fail |
| ddd-domain-review | 26/30 | ⚠️ Warning |
| csharp-standards-review | 22/25 | ⚠️ Warning |
| config-di-review | 20/25 | ⚠️ Warning |
| api-endpoint-review | 30/40 | ❌ Fail |
| data-access-review | 13/20 | ⚠️ Warning |
| async-concurrency-review | 22/25 | ⚠️ Warning |
| error-logging-review | 20/25 | ⚠️ Warning |
| security-review | 53/60 | ⚠️ Warning |
| performance-review | 16/20 | ⚠️ Warning |
| resilience-review | 13/25 | ❌ Fail |
| dependency-review | 28/30 | ⚠️ Warning |
| test-quality-review | 12/25 | ❌ Fail |
| tech-lead-review | 17/20 | ❌ Fail |

---

## 指摘件数（重複排除後）

| 重要度 | 件数 | 説明 |
|--------|------|------|
| **Critical** | 5 | 本番障害・セキュリティ脆弱性の直接原因 |
| **High** | 12 | リリース前に必ず修正すべき問題 |
| **Medium** | 18 | 計画的に対応すべき技術的負債 |
| **Low** | 8 | 改善推奨だが緊急性なし |

---

## Critical 指摘（5 件）

### C-1: DI 未登録によるランタイムクラッシュリスク

**対象ファイル**: `Program.cs`

**問題**: 以下のコンポーネントが実装済みだが DI コンテナに登録されていない

| コンポーネント | 型 | 影響 |
|---------------|-----|------|
| `GatewayMetrics` | Singleton | 依存する機能が動作しない |
| `TimeProvider` | Singleton | `CircuitBreakerService` がインスタンス化不可 |
| `ICircuitBreakerService` | Scoped | サーキットブレーカー機能が無効 |
| `IFallbackService` | Scoped | フォールバック機能が無効 |
| `IDistributedCache` | — | Redis キャッシュ未接続 |

**修正案**:
```csharp
// Program.cs に追加
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<GatewayMetrics>();
builder.Services.AddScoped<ICircuitBreakerService, CircuitBreakerService>();
builder.Services.AddScoped<IFallbackService, FallbackService>();
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});
```

---

### C-2: BackgroundService 未登録

**対象ファイル**: `Infrastructure/Messaging/AuthCacheInvalidationConsumer.cs`

**問題**: Kafka キャッシュ無効化コンシューマーが `BackgroundService` として実装されているが、`AddHostedService<T>()` で登録されていない。サービス起動後も Kafka メッセージを受信しない。

**修正案**:
```csharp
// Program.cs に追加
builder.Services.AddHostedService<AuthCacheInvalidationConsumer>();
```

---

### C-3: PII マスキング未有効化

**対象ファイル**: `Infrastructure/Logging/PiiMaskingEnricher.cs`, `Program.cs`

**問題**: `PiiMaskingEnricher` は実装済みだが、Serilog 設定で `.Enrich.With<PiiMaskingEnricher>()` が呼び出されていない。本番環境でメールアドレス・IP アドレス等の PII がログに平文出力される。

**修正案**:
```csharp
// Program.cs の Serilog 設定に追加
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.With<PiiMaskingEnricher>()  // 追加
        .Enrich.WithProperty("ServiceName", "ApiGateway")
        .WriteTo.Console(new CompactJsonFormatter()));
```

---

### C-4: ミドルウェア未登録（Dead Code）

**対象ファイル**: `Infrastructure/Middleware/CircuitBreakerMiddleware.cs`, `Infrastructure/Middleware/StatusCodeMiddleware.cs`

**問題**: 以下のミドルウェアが実装済みだがパイプラインに登録されていない

| ミドルウェア | 機能 | 影響 |
|-------------|------|------|
| `CircuitBreakerMiddleware` | サーキットブレーカー | 障害伝播を防げない |
| `StatusCodeMiddleware` | 404/405 の RFC 9457 化 | エラーレスポンスが非標準 |

**修正案**:
```csharp
// Program.cs のミドルウェアパイプラインに追加
app.UseMiddleware<StatusCodeMiddleware>();
app.UseMiddleware<CircuitBreakerMiddleware>();
app.MapReverseProxy();
```

---

### C-5: JWT SigningKey の appsettings 直接記述

**対象ファイル**: `appsettings.Development.json`

**問題**: JWT 署名キーが設定ファイルに直接記述されている。Git リポジトリにコミットされており、秘密情報漏洩リスクがある。

**修正案**:
1. `appsettings.Development.json` から `Jwt.SecretKey` を削除
2. `dotnet user-secrets set "Jwt:SecretKey" "<your-secret-key>"` で管理

---

## High 指摘（12 件）

### H-1: Kafka Consumer の同期ブロッキング

**対象ファイル**: `AuthCacheInvalidationConsumer.cs` (L38-40)

**問題**: `ExecuteAsync` 内で `consumer.Consume()` が同期呼び出しされており、`CancellationToken` が正しく伝搬されない。

**修正案**: `Task.Run` でラップし、`stoppingToken` を適切に監視

---

### H-2: YARP ヘルスチェック URL ハードコード

**対象ファイル**: `appsettings.json`

**問題**: 全 8 クラスターのヘルスチェック URL が `http://localhost:XXXX/health` でハードコードされている。コンテナ環境では DNS 名を使用すべき。

**修正案**: 環境変数または .NET Aspire のサービス参照に変更

---

### H-3: RequiredHeaderMiddleware 無効化

**対象ファイル**: `RequiredHeaderMiddleware.cs` (L26)

**問題**: `Content-Type` ヘッダー検証のパス条件が POST/PUT 以外も通過させる設計だが、実質的に全リクエストがスキップされている。

---

### H-4: カスタムレート制限の代替キー不足

**対象ファイル**: `Program.cs` (L281-305)

**問題**: `fixed-by-user` ポリシーで `ClaimTypes.NameIdentifier` が取得できない場合の代替キー（IP アドレス等）が設定されていない。

---

### H-5: サーキットブレーカー状態永続化なし

**対象ファイル**: `CircuitBreakerService.cs`

**問題**: サーキットブレーカー状態がインメモリで管理されており、Pod 再起動時にリセットされる。分散環境では Redis 等での状態共有が必要。

---

### H-6: OpenTelemetry Exporter 未設定

**対象ファイル**: `Program.cs` (L189-213)

**問題**: `AddOtlpExporter()` が呼び出されているが、OTLP エンドポイント設定がない。トレースデータが送信されない。

---

### H-7: ValidateOnStart 不足

**対象ファイル**: `Program.cs`

**問題**: `RateLimitSettings`, `CorsSettings` は `IOptions<T>` パターンで登録されているが、`ValidateOnStart()` が欠落している。

---

### H-8 〜 H-12: テストカバレッジ不足

**対象ファイル**: `Services/ApiGateway.Tests/`

| 問題 | 詳細 |
|------|------|
| 推定カバレッジ | 30-35%（目標: 80%） |
| 未テストクラス | 7/13 クラス |
| ユニットテストなし | 全て統合テストのみ |
| AAA パターン違反 | 5 箇所 |
| NSubstitute/Shouldly 未使用 | テストプロジェクトに未追加 |

---

## Medium 指摘（18 件）

| # | 対象 | 指摘内容 |
|---|------|----------|
| M-1 | `CircuitBreakerService.cs` | primary constructor 未使用 |
| M-2 | `GatewayMetrics.cs` | primary constructor 未使用 |
| M-3 | `CircuitBreakerService.cs` | フィールド名 `_tp` が略語（`_timeProvider` 推奨） |
| M-4 | 複数箇所 | Problem Details の anonymous type を共有 record に抽出すべき |
| M-5 | `JwtSettingsValidator.cs` | `sealed` 修飾子の欠落 |
| M-6 | `Program.cs` | Antiforgery が Add されているが Use されていない |
| M-7 | `BackendServicesHealthCheck.cs` | パラレル HTTP 呼び出しに `Task.WhenAll` 未使用 |
| M-8 | `appsettings.json` | RateLimitSettings の PermitLimit が 100（高すぎる可能性） |
| M-9 | `Dockerfile` | ビルドステージで restore 後に全ソースをコピー（キャッシュ効率低下） |
| M-10 | `appsettings.Production.json` | 環境変数プレースホルダーのみで値なし |
| M-11 | `CorrelationIdMiddleware.cs` | 外部 Correlation-ID の長さ制限なし |
| M-12 | `RequestTimingMiddleware.cs` | Stopwatch よりも `TimeProvider` 使用推奨 |
| M-13 | `RateLimitMiddleware.cs` | カスタム 429 レスポンスが RFC 9457 非準拠 |
| M-14 | 各テストファイル | `Should_X_When_Y` 命名パターン未使用 |
| M-15 | テストプロジェクト | `[Trait("Category", "...")]` 未設定 |
| M-16 | `FallbackService.cs` | フォールバックレスポンスがサービス固有でない |
| M-17 | 全体 | Extension types / extension blocks 未採用（C# 14 機能） |
| M-18 | `Program.cs` | ミドルウェアパイプラインのコメント不足 |

---

## Low 指摘（8 件）

| # | 対象 | 指摘内容 |
|---|------|----------|
| L-1 | `Dockerfile` | `HEALTHCHECK` の `--start-period` が 5s（短い） |
| L-2 | 各ファイル | XML ドキュメントコメントの不足 |
| L-3 | `appsettings.json` | デフォルトログレベルが Information（Warning 推奨） |
| L-4 | `CorsSettings.cs` | record struct の検討 |
| L-5 | `SecurityHeadersMiddleware.cs` | CSP の `script-src 'self'` に `'unsafe-inline'` なし |
| L-6 | テストプロジェクト | テストデータの重複 |
| L-7 | `ApiGateway.csproj` | パッケージバージョンが `*` 指定（一部） |
| L-8 | `Program.cs` | HSTS の MaxAge が短い（31536000 推奨） |

---

## 肯定的所見

### 遵守されている規約

| カテゴリ | 状態 |
|---------|------|
| 禁止パターン（Console.WriteLine, catch{}, DateTime.Now 等） | ✅ 違反なし |
| OWASP セキュリティヘッダー | ✅ 完全実装 |
| RFC 9457 Problem Details | ✅ 例外ハンドラーで統一 |
| JWT ValidateOnStart | ✅ 正しく実装 |
| YARP 設定 | ✅ 設計書通り（18 ルート/8 クラスター） |
| Correlation ID | ✅ 注入防止付きで実装 |
| CancellationToken 伝搬 | ✅ 全 async メソッドで実装 |
| Null Safety | ✅ nullable enable + 適切なガード |

### 設計の優れた点

1. **ミドルウェア分離**: 各横断的関心事が独立したミドルウェアに分離されている
2. **設定の型安全化**: `IOptions<T>` パターンで全設定が型安全
3. **FluentValidation 採用**: 設定バリデーションが宣言的で保守しやすい
4. **メトリクス実装**: OpenTelemetry 準拠の `GatewayMetrics` が設計済み

---

## 修正優先度マトリクス

```
            影響度
              ↑
        High │ C-1, C-2 │ C-3, C-4
             │ H-5, H-6 │ C-5
             ├──────────┼──────────
             │ H-1, H-7 │ H-8〜H-12
        Low  │ M-1〜M-8 │ M-9〜M-18
             └──────────┴──────────→ 緊急度
               Low          High
```

---

## 推奨アクションプラン

### Phase 1: 即時対応（リリースブロッカー解消）

1. **DI 登録の追加** (C-1, C-2)
2. **PiiMaskingEnricher の有効化** (C-3)
3. **ミドルウェアパイプラインへの登録** (C-4)
4. **JWT SecretKey の user-secrets 移行** (C-5)

### Phase 2: リリース前対応

1. Kafka Consumer の非同期化 (H-1)
2. YARP ヘルスチェック URL の外部化 (H-2)
3. OpenTelemetry Exporter 設定 (H-6)
4. ValidateOnStart 追加 (H-7)

### Phase 3: テスト拡充

1. NSubstitute / Shouldly のテストプロジェクト追加
2. 未テストクラスのユニットテスト作成
3. カバレッジ 80% 達成

### Phase 4: 技術的負債解消

1. C# 14 機能の積極採用（primary constructor 等）
2. コード品質の向上（命名、コメント）

---

## 付録: レビュー対象ファイル一覧

### 本体コード（22 ファイル）

```
Services/ApiGateway/
├── ApiGateway.csproj
├── Program.cs
├── Dockerfile
├── appsettings.json
├── appsettings.Development.json
├── appsettings.Staging.json
├── appsettings.Production.json
├── Configurations/
│   ├── CorsSettings.cs
│   ├── JwtSettings.cs
│   ├── JwtSettingsValidator.cs
│   └── RateLimitSettings.cs
└── Infrastructure/
    ├── HealthChecks/
    │   └── BackendServicesHealthCheck.cs
    ├── Logging/
    │   └── PiiMaskingEnricher.cs
    ├── Messaging/
    │   └── AuthCacheInvalidationConsumer.cs
    ├── Metrics/
    │   └── GatewayMetrics.cs
    ├── Middleware/
    │   ├── CircuitBreakerMiddleware.cs
    │   ├── CorrelationIdMiddleware.cs
    │   ├── RateLimitMiddleware.cs
    │   ├── RequestTimingMiddleware.cs
    │   ├── RequiredHeaderMiddleware.cs
    │   ├── SecurityHeadersMiddleware.cs
    │   └── StatusCodeMiddleware.cs
    └── Resilience/
        ├── CircuitBreakerService.cs
        ├── CircuitBreakerSettings.cs
        ├── CircuitState.cs
        ├── FallbackService.cs
        └── ICircuitBreakerService.cs
```

### テストコード（10 ファイル）

```
Services/ApiGateway.Tests/
├── ApiGateway.Tests.csproj
├── Fixtures/
│   └── GatewayWebApplicationFactory.cs
├── HealthChecks/
│   └── BackendServicesHealthCheckTests.cs
├── Integration/
│   ├── AuthRoutingTests.cs
│   ├── CorsTests.cs
│   ├── HealthEndpointTests.cs
│   ├── RateLimitTests.cs
│   └── SecurityHeadersTests.cs
└── Middleware/
    ├── CorrelationIdMiddlewareTests.cs
    └── RequestTimingMiddlewareTests.cs
```

---

## 署名

| 役割 | Agent |
|------|-------|
| アーキテクチャ | architecture-review |
| DDD ドメイン | ddd-domain-review |
| C# 規約 | csharp-standards-review |
| 設定・DI | config-di-review |
| API エンドポイント | api-endpoint-review |
| データアクセス | data-access-review |
| 非同期処理 | async-concurrency-review |
| エラー・ログ | error-logging-review |
| セキュリティ | security-review |
| パフォーマンス | performance-review |
| 耐障害性 | resilience-review |
| 依存関係 | dependency-review |
| テスト品質 | test-quality-review |
| 技術リード | tech-lead-review |

---

## 追加検証: バックエンドサービスルーティング整合性レビュー

### 検証概要

| 項目 | 値 |
|------|-----|
| 検証日時 | 2026-04-07 13:09 |
| 検証対象 | 9 バックエンドサービス |
| 参照ドキュメント | Services-Verification-Report/*.md |
| YARP 設定 | appsettings.json (18 ルート / 8 クラスター) |

---

### 総合判定

# ❌ ルーティング設定に Critical な不整合あり

**API Gateway 経由でのバックエンドサービス呼び出しが正常に動作しない可能性が高い**

---

### 検出された問題（重要度別）

#### Critical（3 件）

##### R-C1: API バージョンプレフィックスの不整合

**影響範囲**: 全 9 サービス

**問題**: 全バックエンドサービスは `/api/v1/...` のバージョン付きパスを使用しているが、API Gateway の YARP 設定は `/api/...` から `/...` への変換のみ行っており、`/v1` が欠落する。

| サービス | バックエンドパス | Gateway 変換後 | 期待されるパス |
|---------|----------------|--------------|--------------|
| AuthService | `/api/v1/auth/login` | `/auth/login` | `/api/v1/auth/login` |
| UserManagement | `/api/v1/users/me` | `/users/me` | `/api/v1/users/me` |
| Sales | `/api/v1/orders` | `/orders` | `/api/v1/orders` |
| PaymentCart | `/api/v1/cart` | `/cart` | `/api/v1/cart` |
| Coupon | `/api/v1/coupons/available` | `/coupons/available` | `/api/v1/coupons/available` |
| Point | `/api/v1/points/balance` | `/points/balance` | `/api/v1/points/balance` |
| AiSupport | `/api/v1/ai/search` | `/search` | `/api/v1/ai/search` |

**修正案**: YARP の `Transforms` を以下のように変更:

```json
"Transforms": [
  { "PathPattern": "/api/{**remainder}" },
  { "PathSet": "/api/v1/{remainder}" }
]
```

または、バックエンドサービス側で `/api/v1` プレフィックスを削除する設計変更。

---

##### R-C2: MailSendService のルーティング未定義

**影響範囲**: MailSendService（ポート 5008）

**問題**: MailSendService は API Gateway の YARP 設定に一切含まれていない。クラスター定義もルート定義も存在しない。

| エンドポイント | 認可 | 状態 |
|--------------|------|------|
| `POST /admin/mail/templates` | AdminOrManager | ❌ ルートなし |
| `GET /admin/mail/templates` | AdminOrManager | ❌ ルートなし |
| `POST /admin/mail/test` | AdminOrManager | ❌ ルートなし |
| `GET /admin/mail/logs` | AdminOrManager | ❌ ルートなし |
| `POST /admin/mail/logs/{id}/retry` | AdminOnly | ❌ ルートなし |
| `GET /admin/mail/stats` | AdminOrManager | ❌ ルートなし |

**修正案**:

```json
// Clusters に追加
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

// Routes に追加
"mail-route": {
  "ClusterId": "mail-cluster",
  "AuthorizationPolicy": "AdminOrManager",
  "RateLimiterPolicy": "user-based",
  "Match": {
    "Path": "/api/admin/mail/{**catch-all}"
  },
  "Transforms": [{ "PathRemovePrefix": "/api" }]
}
```

---

##### R-C3: InventoryManagementService の主要エンドポイント未ルーティング

**影響範囲**: カテゴリ、価格、レビュー、サイズガイド機能

**問題**: InventoryManagementService の 28 エンドポイントのうち、`/api/products` と `/api/inventory` のみがルーティングされており、以下が欠落:

| 欠落エンドポイント | HTTP メソッド | 認可 |
|------------------|-------------|------|
| `/api/categories` | GET | anonymous |
| `/api/categories/{id}` | GET | anonymous |
| `/api/categories/{id}/products` | GET | anonymous |
| `/api/categories` | POST | Admin |
| `/api/prices/{productId}` | GET | anonymous |
| `/api/prices/{productId}/history` | GET | Admin |
| `/api/prices` | POST | Admin |
| `/api/reviews/{id}` | GET | authenticated |
| `/api/reviews` | POST | authenticated |
| `/api/size-guides/{categoryId}` | GET | anonymous |
| `/api/size-guides` | POST | Admin |

**修正案**:

```json
"categories-route": {
  "ClusterId": "inventory-cluster",
  "AuthorizationPolicy": "anonymous",
  "Match": { "Path": "/api/categories/{**catch-all}" },
  "Transforms": [{ "PathRemovePrefix": "/api" }]
},
"prices-route": {
  "ClusterId": "inventory-cluster",
  "AuthorizationPolicy": "anonymous",
  "Match": { "Path": "/api/prices/{**catch-all}" },
  "Transforms": [{ "PathRemovePrefix": "/api" }]
},
"reviews-route": {
  "ClusterId": "inventory-cluster",
  "AuthorizationPolicy": "default",
  "Match": { "Path": "/api/reviews/{**catch-all}" },
  "Transforms": [{ "PathRemovePrefix": "/api" }]
},
"size-guides-route": {
  "ClusterId": "inventory-cluster",
  "AuthorizationPolicy": "anonymous",
  "Match": { "Path": "/api/size-guides/{**catch-all}" },
  "Transforms": [{ "PathRemovePrefix": "/api" }]
}
```

---

#### High（5 件）

##### R-H1: SalesManagementService の配送・返品エンドポイント未ルーティング

| 欠落エンドポイント | 認可 | 影響 |
|------------------|------|------|
| `/api/v1/shipments/*` | Admin | 配送管理機能が利用不可 |
| `/api/v1/returns/*` | User/Admin | 返品管理機能が利用不可 |

---

##### R-H2: PaymentCartService のゲストチェックアウト未ルーティング

| 欠落エンドポイント | 認可 | 影響 |
|------------------|------|------|
| `/api/v1/checkout/guest` | anonymous | ゲストユーザーの決済不可 |

---

##### R-H3: Admin エンドポイントの体系的な未ルーティング

以下の管理機能エンドポイントが API Gateway 経由でアクセス不可:

| サービス | 欠落パス | 影響 |
|---------|---------|------|
| UserManagement | `/api/v1/admin/users/*` | ユーザー管理（一覧・ステータス変更）不可 |
| Coupon | `/api/v1/admin/campaigns/*` | キャンペーン管理不可 |
| Coupon | `/api/v1/admin/coupons/*` | クーポン管理不可 |
| Point | `/api/v1/admin/points/*` | ポイント管理不可 |
| Point | `/api/v1/admin/tiers/*` | ティア設定不可 |
| AiSupport | `/api/v1/admin/ai/forecast/*` | 需要予測不可 |
| AiSupport | `/api/v1/admin/ai/analytics/*` | AI 分析不可 |
| AiSupport | `/api/v1/admin/ai/models/*` | モデル管理不可 |

---

##### R-H4: PointService のティア公開エンドポイント未ルーティング

| 欠落エンドポイント | 認可 | 影響 |
|------------------|------|------|
| `/api/v1/tiers` | anonymous | 会員ランク一覧表示不可 |
| `/api/v1/tiers/{name}` | anonymous | 会員ランク詳細表示不可 |

---

##### R-H5: AiSupportService のパス変換不整合

**問題**: AiSupportService は `/api/v1/ai/search`, `/api/v1/ai/recommendations` 等を使用するが、Gateway は `/api/search`, `/api/recommendations` にマッチし、変換後は `/search`, `/recommendations` になる。

| Gateway パス | 変換後 | バックエンド期待 | 一致 |
|-------------|-------|----------------|------|
| `/api/search/*` | `/search/*` | `/api/v1/ai/search/*` | ❌ |
| `/api/recommendations/*` | `/recommendations/*` | `/api/v1/ai/recommendations/*` | ❌ |
| `/api/chat/*` | `/chat/*` | `/api/v1/ai/chat/*` | ❌ |
| `/api/analytics/*` | `/analytics/*` | `/api/v1/admin/ai/analytics/*` | ❌ |

---

#### Medium（2 件）

##### R-M1: Internal エンドポイントの意図的除外の明文化不足

以下のサービス間通信用エンドポイントは Gateway 経由でルーティングされていないが、これが意図的な設計かどうかがドキュメント化されていない:

| サービス | Internal エンドポイント |
|---------|----------------------|
| Coupon | `/api/v1/internal/coupons/*` |
| Point | `/api/v1/internal/points/*` |

**推奨**: 設計書または `appsettings.json` にコメントで意図を明記。

---

##### R-M2: YARP 認可ポリシー名と実際のポリシー定義の不一致可能性

YARP 設定で参照されているポリシー名:
- `anonymous`
- `default`
- `AdminOrManager`
- `login`
- `user-based`
- `products`
- `anonymous-api`
- `checkout`
- `ai-api`

**確認必要**: `Program.cs` でこれらすべてのポリシーが正しく定義されているか。

---

### サービス別ルーティング対応表

| サービス | ポート | クラスター | ルート数 | カバー率 | 判定 |
|---------|-------|-----------|---------|---------|------|
| AuthService | 5001 | auth-cluster | 1 | 低（バージョン不一致） | ⚠️ |
| UserManagement | 5002 | user-cluster | 1 | 低（admin 欠落） | ⚠️ |
| InventoryManagement | 5003 | inventory-cluster | 2 | 低（4 カテゴリ欠落） | ❌ |
| SalesManagement | 5004 | sales-cluster | 2 | 中（shipments/returns 欠落） | ⚠️ |
| PaymentCart | 5005 | payment-cart-cluster | 4 | 高（checkout/guest 欠落） | ⚠️ |
| Coupon | 5006 | coupons-cluster | 3 | 中（admin 欠落） | ⚠️ |
| Point | 5007 | points-cluster | 1 | 低（admin/tiers 欠落） | ⚠️ |
| MailSend | 5008 | **未定義** | 0 | **0%** | ❌ |
| AiSupport | 5009 | ai-cluster | 4 | 低（パス不一致） | ❌ |

---

### 推奨アクションプラン

#### Phase 1: Critical 修正（即時対応）

1. **R-C1 対応**: YARP Transform のパス変換ロジックを修正し、`/api/v1` プレフィックスを保持
2. **R-C2 対応**: MailSendService のクラスター・ルート定義を追加
3. **R-C3 対応**: InventoryManagementService の欠落ルートを追加

#### Phase 2: High 修正（リリース前対応）

1. **R-H1 対応**: shipments / returns ルートを追加
2. **R-H2 対応**: checkout/guest ルートを追加
3. **R-H3 対応**: 各サービスの admin ルートを追加
4. **R-H4 対応**: tiers ルートを追加
5. **R-H5 対応**: AiSupport のパス変換を修正

#### Phase 3: Medium 対応

1. **R-M1 対応**: Internal エンドポイントの設計意図をドキュメント化
2. **R-M2 対応**: YARP ポリシー名と Program.cs の定義を照合

---

### 検証に使用したバックエンドサービスドキュメント

| ファイル | エンドポイント数 |
|---------|---------------|
| AuthService-verification-report.md | 20 |
| UserManagementService-verification-report.md | 22 |
| InventoryManagementService-verification-report.md | 28 |
| SalesManagementService-verification-report.md | 19 |
| PaymentCartService-verification-report.md | 14 |
| CouponService-verification-report.md | 21 |
| PointService-verification-report.md | 18 |
| MailSendService-verification-report.md | 11 |
| AiSupportService-verification-report.md | 28 |
| **合計** | **181** |

---

*追加検証: バックエンドサービスルーティング整合性レビュー (9 Service Parallel Analysis)*
