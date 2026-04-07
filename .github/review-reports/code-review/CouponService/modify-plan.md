# CouponService 修正計画書 — modify-plan.md

**対象レポート**: [check-report-2.md](check-report-2.md)  
**作成日**: 2026-04-06  
**修正対象**: `Services/CouponService/` 配下全ソースコード  
**指摘総数（重複排除後）**: Critical 3 / High 21 / Medium 27 / Low 12

---

## 修正方針

1. **Critical → High → Medium → Low** の優先度順に修正
2. 各修正は独立したコミット単位で実施可能
3. 修正後は `dotnet build` + 既存テスト通過を確認
4. Medium/Low の一部はリファクタリング影響が大きいため、修正範囲を限定して記載

---

## 目次

- [Critical 修正（3 件）](#critical-修正3-件)
  - [C-1: ReleaseCouponAsync 冪等性修正](#c-1-releasecouponasync-冪等性修正)
  - [C-2: Validator DiscountType ロジック反転修正](#c-2-validator-discounttype-ロジック反転修正)
  - [C-3: JWT ValidAlgorithms 追加](#c-3-jwt-validalgorithms-追加)
- [High 修正（21 件）](#high-修正21-件)
- [Medium 修正（27 件）](#medium-修正27-件)
- [Low 修正（12 件）](#low-修正12-件)

---

## Critical 修正（3 件）

### C-1: ReleaseCouponAsync 冪等性修正

| 項目 | 内容 |
|------|------|
| **指摘 ID** | C-1 |
| **出典 Agent** | tech-lead, ddd-domain |
| **カテゴリ** | データ整合性 / Saga 補償トランザクション |
| **対象ファイル** | `Services/CouponService/Services/CouponAppService.cs` (L195-218) |
| **関連ファイル** | `Models/CouponUsage.cs`, `Repositories/Repositories.cs`, `Repositories/Interfaces/IRepositories.cs` |

#### 問題分析

現在の `ReleaseCouponAsync` は以下のフローで処理している:

1. `FindByOrderIdAsync` で CouponUsage レコードを検索
2. usage が `null` の場合は early return（冪等）
3. usage が存在する場合、`CurrentUsageCount` を減算
4. **CouponUsage レコードは削除も無効化もせず放置**

再呼び出し時、ステップ 2 で同一の usage レコードが再検出され、`CurrentUsageCount` が二重減算される。Saga 補償トランザクションとして致命的。

#### 解決策検討

**案 A: CouponUsage レコードを物理削除**
- メリット: シンプル、DB 肥大化防止
- デメリット: 利用履歴が消失し分析・監査に影響

**案 B: CouponUsage に `IsReleased` フラグを追加（採用）**
- メリット: 利用履歴を保持しつつ冪等性を保証。監査証跡が残る
- デメリット: スキーマ変更（マイグレーション必要）
- 理由: EC サイトの監査・分析要件に適合。Saga 補償の冪等性は `IsReleased == true` なら early return で保証

**案 C: CouponUsage のステータスカラムを追加し状態遷移管理**
- メリット: 詳細な状態遷移（Active → Released → etc.）を表現可能
- デメリット: 過剰設計。Currently CouponUsage にステータス管理は不要

#### 修正内容（案 B 採用）

**1. `Models/CouponUsage.cs` — `IsReleased` プロパティ追加**

```csharp
// 既存プロパティの後に追加
[Column("is_released")]
public bool IsReleased { get; set; }

[Column("released_at")]
public DateTimeOffset? ReleasedAt { get; set; }
```

**2. `Services/CouponAppService.cs` — `ReleaseCouponAsync` 修正**

```csharp
public async Task ReleaseCouponAsync(
    ReleaseCouponRequest request, CancellationToken ct = default)
{
    var coupon = await couponRepository.FindByIdAsync(request.CouponId, ct)
        ?? throw new CouponNotFoundException($"クーポン {request.CouponId} が見つかりません");

    var usage = await couponUsageRepository.FindByOrderIdAsync(coupon.Id, request.OrderId, ct);
    if (usage is null || usage.IsReleased)
    {
        logger.LogInformation(
            "リリース済みまたは対象なし（冪等）: CouponId={CouponId}, OrderId={OrderId}",
            request.CouponId, request.OrderId);
        return; // Idempotent: already released or not found
    }

    usage.IsReleased = true;
    usage.ReleasedAt = timeProvider.GetUtcNow();
    coupon.CurrentUsageCount = Math.Max(0, coupon.CurrentUsageCount - 1);

    // Outbox event
    var outboxEvent = new OutboxEvent
    {
        AggregateType = "Coupon",
        AggregateId = coupon.Id,
        EventType = "coupon.released",
        Payload = JsonSerializer.Serialize(new CouponReleasedEvent(
            coupon.Id, coupon.Code, usage.UserId, request.OrderId,
            usage.DiscountAmount, timeProvider.GetUtcNow()))
    };
    await outboxEventRepository.AddAsync(outboxEvent, ct);

    try
    {
        await couponRepository.SaveChangesAsync(ct);
    }
    catch (DbUpdateConcurrencyException ex)
    {
        logger.LogWarning(ex, "楽観的ロック競合: Coupon {CouponId}", coupon.Id);
        throw new ConcurrencyException("データが他のユーザーによって更新されました。再度お試しください。");
    }

    await cacheService.InvalidateAsync(coupon.Code, ct);
    logger.LogInformation("クーポンリリース: {CouponCode}, OrderId={OrderId}", coupon.Code, request.OrderId);
}
```

**3. EF Core マイグレーション追加**

```bash
cd Services/CouponService
dotnet ef migrations add AddCouponUsageReleasedFields
```

**4. `Consumers/CouponEvents.cs` に `CouponReleasedEvent` の import を使用**

既に定義済み（`CouponReleasedEvent` record が存在）。Outbox の Payload を匿名型ではなく `CouponReleasedEvent` に変更。※`CouponReleasedEvent.ReleasedAmount` フィールドに `usage.DiscountAmount` をマッピング（現在の匿名型には `DiscountAmount` が含まれていないため、イベントペイロードへの **新規フィールド追加** となる。Consumer 側の後方互換性を確認すること）。

---

### C-2: Validator DiscountType ロジック反転修正

| 項目 | 内容 |
|------|------|
| **指摘 ID** | C-2 |
| **出典 Agent** | tech-lead, csharp, api-endpoint, architecture |
| **カテゴリ** | バリデーション |
| **対象ファイル** | `Validators/CouponValidators.cs` (L27-30, L125) |

#### 問題分析

`DiscountType == 0` は `FixedAmount`（固定額割引）だが、「100% 以下」制約を適用している。  
`DiscountType == 1`（`Percentage`）には上限チェックが無い。  
結果: 固定額が 100 円以下に不当制限され、パーセント割引が 100% 超の不正値を許容する。

#### 修正内容

**`Validators/CouponValidators.cs` — 2 箇所修正**

**CreateCouponRequestValidator (L27-30):**

```csharp
// Before:
When(x => x.DiscountType == 0, () =>
{
    RuleFor(x => x.DiscountValue)
        .LessThanOrEqualTo(100).WithMessage("パーセント割引は100%以下を指定してください");
});

// After:
When(x => x.DiscountType == 1, () =>
{
    RuleFor(x => x.DiscountValue)
        .LessThanOrEqualTo(100).WithMessage("パーセント割引は100%以下を指定してください");
});
```

**UpdateCouponRequestValidator (L125):**

```csharp
// Before:
When(x => x.DiscountType == 0 && x.DiscountValue.HasValue, () =>

// After:
When(x => x.DiscountType == 1 && x.DiscountValue.HasValue, () =>
```

---

### C-3: JWT ValidAlgorithms 追加

| 項目 | 内容 |
|------|------|
| **指摘 ID** | C-3 |
| **出典 Agent** | security |
| **カテゴリ** | 認証セキュリティ |
| **対象ファイル** | `Program.cs` (L60-68) |

#### 問題分析

JWT `TokenValidationParameters` に `ValidAlgorithms` が未指定。攻撃者は `alg:none` トークンや RS256→HS256 鍵混同攻撃を試行可能。

#### 修正内容

**`Program.cs` — TokenValidationParameters への追加**

```csharp
options.TokenValidationParameters = new()
{
    ValidateIssuer = true,
    ValidateAudience = true,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    ClockSkew = TimeSpan.FromMinutes(5),
    // 追加: アルゴリズム固定・署名必須
    RequireSignedTokens = true,
    ValidAlgorithms = ["RS256"]
};
```

---

## High 修正（21 件）

### H-1: レート制限のエンドポイント適用

| 項目 | 内容 |
|------|------|
| **対象ファイル** | `Endpoints/CouponEndpoints.cs`, `Endpoints/AdminCouponEndpoints.cs`, `Endpoints/InternalCouponEndpoints.cs`, `Endpoints/CampaignEndpoints.cs` |
| **修正内容** | 各 `MapGroup` に `.RequireRateLimiting()` を追加 |

```csharp
// CouponEndpoints.cs
var group = app.MapGroup("/api/v1/coupons")
    .WithTags("Coupons")
    .RequireRateLimiting("coupon-api");  // 追加

// AdminCouponEndpoints.cs
var group = app.MapGroup("/api/v1/admin/coupons")
    .WithTags("Admin Coupons")
    .RequireAuthorization("AdminOnly")
    .RequireRateLimiting("coupon-api");  // 追加

// InternalCouponEndpoints.cs
var group = app.MapGroup("/api/v1/internal/coupons")
    .WithTags("Internal Coupons")
    .RequireAuthorization("InternalServiceOnly")
    .RequireRateLimiting("redeem-api");  // 追加

// CampaignEndpoints.cs
var group = app.MapGroup("/api/v1/admin/campaigns")
    .WithTags("Admin Campaigns")
    .RequireAuthorization("AdminOnly")
    .RequireRateLimiting("coupon-api");  // 追加
```

---

### H-2: RedeemCouponAsync のクーポン有効性再検証

| 項目 | 内容 |
|------|------|
| **対象ファイル** | `Services/CouponAppService.cs` (L134-190) |
| **修正内容** | idempotency チェック後・usage 作成前にクーポンの有効性を再検証する |

```csharp
// L139 の後（existingUsage チェック後）に追加:

// TOCTOU 防止: Redeem 前にクーポンの有効性を再検証
if (!coupon.IsActive)
    throw new InvalidCouponException("クーポンが無効です");

var now = timeProvider.GetUtcNow();
if (now < coupon.ValidFrom || now > coupon.ValidUntil)
    throw new CouponExpiredException("クーポンの有効期間外です");

if (coupon.CurrentUsageCount >= coupon.MaxUsageCount)
    throw new CouponUsageLimitExceededException("クーポンの利用上限に達しました");
```

**注意**: `timeProvider` は既に DI 注入済み。

---

### H-3: RedeemCouponRequest.DiscountAmount のサーバー側検証

| 項目 | 内容 |
|------|------|
| **対象ファイル** | `Services/CouponAppService.cs` (L143-148) |
| **修正内容** | リクエストの DiscountAmount とサーバー側計算結果の整合性チェックを追加 |

#### 解決策検討

**案 A: DiscountAmount をサーバー側で完全再計算（採用）**
- メリット: 内部サービスクライアントの改ざん/バグも防止
- デメリット: RuleEngine 呼出のオーバーヘッド
- 理由: Redeem は高頻度ではなくオーバーヘッドは許容範囲。安全性優先

**案 B: DiscountAmount の差異が閾値以内か検証**
- メリット: 計算誤差を許容
- デメリット: 閾値の管理が必要

**案 C: DiscountAmount をリクエストから除去し完全再計算のみ**
- メリット: 最もシンプル
- デメリット: RedeemCouponRequest の互換性破壊

```csharp
// L143 以降の修正:
// サーバー側で割引額を再計算し整合性検証
// 注: request.DiscountAmount は注文金額（orderAmount）として使用される。
//     命名上の混乱があるため、将来的に RedeemCouponRequest に OrderAmount フィールドを追加し分離を検討
var serverDiscount = CouponRuleEngine.CalculateDiscount(coupon, request.DiscountAmount);
// DiscountAmount の不一致を検出（1%以上の乖離は拒否）
if (Math.Abs(request.DiscountAmount - serverDiscount) > serverDiscount * 0.01m && serverDiscount > 0)
{
    logger.LogWarning(
        "割引額不一致: Request={RequestAmount}, Server={ServerAmount}, CouponId={CouponId}",
        request.DiscountAmount, serverDiscount, coupon.Id);
    throw new BusinessException($"割引額が不正です。期待値: {serverDiscount}");
}
```

**注**: `CalculateDiscount` メソッドを `CouponRuleEngine` から `internal static` として公開するか、`CouponAppService` 内に計算ロジックを複製する。実装上は `CouponRuleEngine.CalculateDiscount` を `internal static` に変更し再利用が最適。

**`Services/CouponRuleEngine.cs` — CalculateDiscount のアクセス修飾子変更:**

```csharp
// Before:
private static decimal CalculateDiscount(Coupon coupon, decimal orderAmount)

// After:
internal static decimal CalculateDiscount(Coupon coupon, decimal orderAmount)
```

**`Services/CouponAppService.cs` — RedeemCouponAsync 内に検証を追加:**

`RedeemCouponRequest` の `DiscountAmount` フィールドは残すが、サーバー側再計算値を実際の usage に記録するよう変更:

```csharp
// usage 作成部分を修正
var serverDiscount = CouponRuleEngine.CalculateDiscount(coupon, request.DiscountAmount);

var usage = new CouponUsage
{
    CouponId = coupon.Id,
    UserId = userId,
    OrderId = request.OrderId,
    DiscountAmount = serverDiscount  // サーバー側再計算値を使用
};
```

**補足**: `request.DiscountAmount` は本来 `orderAmount` であるべき。CalculateDiscountRequest との整合性を考慮すると、RedeemCouponRequest に `OrderAmount` フィールドを追加する設計変更も検討すべき。ただし本修正では互換性を維持し、既存の DiscountAmount をサーバー側検証の入力として使用する。

---

### H-4: RedeemCoupon の userId 取得方法修正

| 項目 | 内容 |
|------|------|
| **対象ファイル** | `Endpoints/InternalCouponEndpoints.cs` (L43-46), `DTOs/Requests/CouponRequests.cs` |
| **修正内容** | RedeemCouponRequest に `UserId` フィールドを追加し、リクエストボディから取得 |

```csharp
// DTOs/Requests/CouponRequests.cs — RedeemCouponRequest 修正:
public record RedeemCouponRequest(
    [Required, StringLength(36)] string CouponId,
    [Required, StringLength(36)] string OrderId,
    [Required, Range(0.01, 100000000)] decimal DiscountAmount,
    [Required, StringLength(36)] string UserId);  // 追加

// Endpoints/InternalCouponEndpoints.cs — RedeemCoupon 修正:
private static async Task<IResult> RedeemCoupon(
    [FromBody] RedeemCouponRequest request,
    IValidator<RedeemCouponRequest> validator,
    ICouponService couponService, CancellationToken ct)
{
    var validationResult = await validator.ValidateAsync(request, ct);
    if (!validationResult.IsValid)
        return Results.ValidationProblem(validationResult.ToDictionary());

    // リクエストボディの UserId を使用（internal-service の sub ではなく実ユーザーID）
    var result = await couponService.RedeemCouponAsync(request.UserId, request, ct);
    return Results.Created($"/api/v1/internal/coupons/redeem", result);
}
```

**Validator 追加（`Validators/CouponValidators.cs` — RedeemCouponRequestValidator）:**

```csharp
RuleFor(x => x.UserId)
    .NotEmpty().WithMessage("ユーザーIDは必須です");
```

---

### H-5: InternalServiceHandler 認証強化

| 項目 | 内容 |
|------|------|
| **対象ファイル** | `Infrastructure/Authorization/InternalServiceHandler.cs` |
| **修正内容** | 複数クレーム（sub + scope + iss）による検証 |

#### 解決策検討

**案 A: sub + scope + iss の 3 クレーム検証（採用）**
- メリット: JWT 脆弱性との複合攻撃を実質排除
- デメリット: 全内部サービスの JWT に scope / iss を追加する必要

**案 B: sub + aud の 2 クレーム検証**
- メリット: シンプル
- デメリット: scope の粒度制御ができない

**案 C: mTLS による内部サービス間認証**
- メリット: 最も安全
- デメリット: 証明書管理のインフラ追加が大規模

```csharp
// Infrastructure/Authorization/InternalServiceHandler.cs
public class InternalServiceHandler : AuthorizationHandler<InternalServiceRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        InternalServiceRequirement requirement)
    {
        var hasSub = context.User.HasClaim(c =>
            c.Type == "sub" && c.Value == "internal-service");
        var hasScope = context.User.HasClaim(c =>
            c.Type == "scope" && c.Value.Contains("coupon:manage"));
        var hasIssuer = context.User.HasClaim(c =>
            c.Type == "iss" && !string.IsNullOrEmpty(c.Value));

        if (hasSub && hasScope && hasIssuer)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
```

**注**: AuthService が発行する内部サービストークンに `scope: "coupon:manage"` を含める必要あり。AuthService 側の対応が前提。

---

### H-6: OpenAPI 設定追加

| 項目 | 内容 |
|------|------|
| **対象ファイル** | `CouponService.csproj`, `Program.cs` |
| **修正内容** | パッケージ追加 + `AddOpenApi()` / `MapOpenApi()` |

**`CouponService.csproj` — パッケージ追加:**

```xml
<!-- OpenAPI -->
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.*" />
```

**`Program.cs` — サービス登録 + エンドポイント:**

```csharp
// サービス登録セクション（FluentValidation の後あたり）に追加:
builder.Services.AddOpenApi();

// エンドポイントマッピングセクション（ヘルスチェックの前）に追加:
app.MapOpenApi();
```

---

### H-7: OpenTelemetry に EF Core インスツルメンテーション追加

| 項目 | 内容 |
|------|------|
| **対象ファイル** | `CouponService.csproj`, `Program.cs` (L186-193) |
| **修正内容** | `OpenTelemetry.Instrumentation.EntityFrameworkCore` パッケージ追加 + 設定 |

**`CouponService.csproj` — パッケージ追加:**

```xml
<PackageReference Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.*" />
```

**`Program.cs` — OpenTelemetry セクション修正:**

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()  // 追加
        .AddSource("SkiShop.CouponService"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("SkiShop.CouponService"));
```

---

### H-8: OutboxPublisher の Repository パターン適用

| 項目 | 内容 |
|------|------|
| **対象ファイル** | `BackgroundServices/OutboxPublisher.cs`, `Repositories/Interfaces/IRepositories.cs`, `Repositories/Repositories.cs` |
| **修正内容** | OutboxPublisher が AppDbContext を直接操作する部分を IOutboxEventRepository に移譲 |

#### 解決策検討

**案 A: IOutboxEventRepository にバッチ処理メソッドを追加（採用）**
- メリット: レイヤー依存方向を維持、既存インターフェースの自然な拡張
- デメリット: Advisory Lock のロジックが Repository に入る

**案 B: 新規 IOutboxProcessingService を作成し Service 層に移譲**
- メリット: Repository に Advisory Lock を含めない
- デメリット: 不必要なレイヤー追加

**案 C: Advisory Lock 専用の Repository を作成**
- メリット: 責務分離が明確
- デメリット: 過剰設計

**`Repositories/Interfaces/IRepositories.cs` — IOutboxEventRepository に追加:**

```csharp
Task<bool> TryAcquireAdvisoryLockAsync(long lockId, CancellationToken ct = default);
Task ReleaseAdvisoryLockAsync(long lockId, CancellationToken ct = default);
Task<List<OutboxEvent>> GetPendingAndMarkProcessingAsync(int batchSize, CancellationToken ct = default);
Task UpdateBatchAsync(CancellationToken ct = default);
```

**`Repositories/Repositories.cs` — OutboxEventRepository に実装追加:**

```csharp
public async Task<bool> TryAcquireAdvisoryLockAsync(long lockId, CancellationToken ct = default)
    => await context.Database
        .SqlQuery<bool>($"SELECT pg_try_advisory_lock({lockId})")
        .FirstOrDefaultAsync(ct);

public async Task ReleaseAdvisoryLockAsync(long lockId, CancellationToken ct = default)
    => await context.Database
        .ExecuteSqlAsync($"SELECT pg_advisory_unlock({lockId})", ct);

public async Task<List<OutboxEvent>> GetPendingAndMarkProcessingAsync(int batchSize, CancellationToken ct = default)
{
    var pendingEvents = await context.OutboxEvents
        .Where(e => e.Status == OutboxEventStatus.Pending && e.RetryCount < e.MaxRetries)
        .OrderBy(e => e.CreatedAt)
        .Take(batchSize)
        .ToListAsync(ct);

    foreach (var evt in pendingEvents)
        evt.Status = OutboxEventStatus.Processing;
    await context.SaveChangesAsync(ct);
    return pendingEvents;
}

public async Task UpdateBatchAsync(CancellationToken ct = default)
    => await context.SaveChangesAsync(ct);
```

**`BackgroundServices/OutboxPublisher.cs` — AppDbContext を IOutboxEventRepository に置換:**

コンストラクタの `AppDbContext` への依存を `IOutboxEventRepository` に変更し、全 DB 操作を Repository 経由に修正。

---

### H-9: ReleaseCouponAsync の DbUpdateConcurrencyException ハンドリング

**→ C-1 の修正に含めて対応済み（上記 C-1 の修正コード参照）**

`try/catch (DbUpdateConcurrencyException)` を `ReleaseCouponAsync` の `SaveChangesAsync` に追加。

---

### H-10: CouponRestriction に updated_at 追加

| 項目 | 内容 |
|------|------|
| **対象ファイル** | `Models/CouponRestriction.cs` |
| **修正内容** | `UpdatedAt` プロパティ追加 |

```csharp
// CreatedAt の後に追加:
[Column("updated_at")]
public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
```

**EF Core マイグレーション:**

```bash
dotnet ef migrations add AddCouponRestrictionUpdatedAt
```

**注**: AppDbContext の `SaveChangesAsync` オーバーライドが `UpdatedAt` プロパティの自動更新を行うため、追加後は自動的に機能する。

---

### H-11: Microsoft.Identity.Web パッケージ追加

| 項目 | 内容 |
|------|------|
| **対象ファイル** | `CouponService.csproj` |
| **修正内容** | パッケージ追加 |

```xml
<!-- 認証 — 既存の JwtBearer の後に追加 -->
<PackageReference Include="Microsoft.Identity.Web" Version="3.*" />
```

---

### H-12: Kafka Producer 設定強化

| 項目 | 内容 |
|------|------|
| **対象ファイル** | `Program.cs` (L112-116) |
| **修正内容** | `ProducerConfig` に信頼性パラメータ追加 |

```csharp
var config = new ProducerConfig
{
    BootstrapServers = kafkaSettings.BootstrapServers,
    Acks = Acks.All,                      // 追加: 全 ISR レプリカへの書き込み確認
    EnableIdempotence = true,              // 追加: 冪等プロデューサー
    MessageSendMaxRetries = 3,             // 追加: 最大リトライ
    RetryBackoffMs = 100                   // 追加: リトライ間隔
};
```

---

### H-13: Kafka ヘルスチェック追加

| 項目 | 内容 |
|------|------|
| **対象ファイル** | `CouponService.csproj`, `Program.cs` (L181-184) |
| **修正内容** | Kafka ヘルスチェックパッケージ追加 + Readiness に登録 |

**`CouponService.csproj`:**

```xml
<PackageReference Include="AspNetCore.HealthChecks.Kafka" Version="9.*" />
```

**`Program.cs` — ヘルスチェック登録セクション修正:**

```csharp
var healthChecksBuilder = builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql", tags: ["ready"]);

// Kafka ヘルスチェック追加
var kafkaBootstrapServers = builder.Configuration["Kafka:BootstrapServers"];
if (!string.IsNullOrEmpty(kafkaBootstrapServers))
{
    healthChecksBuilder.AddKafka(
        new Confluent.Kafka.ProducerConfig { BootstrapServers = kafkaBootstrapServers },
        name: "kafka", tags: ["ready"]);
}

if (!string.IsNullOrEmpty(redisConnectionString))
{
    healthChecksBuilder.AddRedis(redisConnectionString, name: "redis", tags: ["ready"]);
}
```

---

### H-14: Redis 例外ハンドリング拡大

| 項目 | 内容 |
|------|------|
| **対象ファイル** | `Services/CouponCacheServiceRedis.cs` |
| **修正内容** | `catch (RedisConnectionException)` → `catch (RedisException)` に変更（全 5 箇所） |

```csharp
// 全メソッド（GetCouponAsync, SetCouponAsync, InvalidateAsync, GetUserUsageCountAsync, SetUserUsageCountAsync）:
// Before:
catch (RedisConnectionException ex)

// After:
catch (RedisException ex)
```

`RedisException` は `RedisConnectionException`, `RedisTimeoutException`, `RedisServerException` の共通基底クラス。

---

### H-15: Redis 動的フォールバック

| 項目 | 内容 |
|------|------|
| **対象ファイル** | 新規ファイル作成不要（`CouponCacheServiceRedis.cs` 内の修正で対応） |
| **修正内容** | Redis 障害時のフォールスルー実装 |

#### 解決策検討

**案 A: catch ブロック内で InMemory キャッシュにフォールバック（採用）**
- メリット: 既存コード変更最小限。Redis 障害時に自動でメモリキャッシュを利用
- デメリット: メモリキャッシュとの一貫性は保証されない

**案 B: サーキットブレーカーデコレータパターン**
- メリット: Redis 復旧時の自動切り替え
- デメリット: 新規クラス追加、DI 構成変更

**案 C: Polly を使用した Resilience Pipeline**
- メリット: 標準的なアプローチ
- デメリット: ICouponCacheService が HttpClient ベースではないため `AddStandardResilienceHandler` が直接使えない

現状は案 A を採用。Redis の catch ブロックは既に null / 無操作を返しており、呼び出し元（CouponRuleEngine）は DB フォールバックを実装済み。current implementation は事実上のフォールバック実装になっている。追加として `IMemoryCache` を CouponCacheService に DI 注入し、Redis 障害時にインメモリキャッシュを使用する方式に変更する:

**`Services/CouponCacheServiceRedis.cs` — コンストラクタ修正 + フォールバック実装:**

```csharp
public class CouponCacheService(
    IConnectionMultiplexer redis,
    IOptions<CouponSettings> settings,
    IMemoryCache memoryCache,  // 追加: フォールバック用
    ILogger<CouponCacheService> logger) : ICouponCacheService
{
    // ...
    public async Task<Coupon?> GetCouponAsync(string code, CancellationToken ct = default)
    {
        try
        {
            var cached = await _db.StringGetAsync($"coupon:{code}");
            if (!cached.HasValue) return null;
            logger.LogDebug("キャッシュヒット: coupon:{CouponCode}", code);
            return JsonSerializer.Deserialize<Coupon>(cached.ToString());
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Redis エラー — InMemory フォールバック: coupon:{CouponCode}", code);
            // InMemory フォールバック（短い TTL でステールデータを防止）
            return memoryCache.TryGetValue($"coupon:{code}", out Coupon? fallback) ? fallback : null;
        }
    }
    // 同様に他のメソッドにもフォールバック追加
}
```

**`Program.cs` — Redis 使用時にも MemoryCache を追加登録:**

```csharp
// Redis セクション修正:
if (!string.IsNullOrEmpty(redisConnectionString))
{
    builder.Services.AddMemoryCache();  // 追加: フォールバック用
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp => { /* 既存 */ });
    builder.Services.AddScoped<ICouponCacheService, CouponCacheService>();
}
```

---

### H-16: Kafka Consumer 指数バックオフ

| 項目 | 内容 |
|------|------|
| **対象ファイル** | `Consumers/OrderEventConsumer.cs` (L49) |
| **修正内容** | 固定 5 秒 → 動的指数バックオフ |

```csharp
public class OrderEventConsumer(
    IConsumer<string, string> consumer,
    IServiceScopeFactory scopeFactory,
    ILogger<OrderEventConsumer> logger) : BackgroundService
{
    private static readonly TimeSpan MinBackoff = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);
    private TimeSpan _currentBackoff = MinBackoff;  // 追加

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // ...
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // ... existing consume logic ...
                consumer.Commit(result);
                _currentBackoff = MinBackoff;  // 成功時リセット
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume エラー: {Topic}",
                    ex.ConsumerRecord?.Topic);
                await Task.Delay(_currentBackoff, stoppingToken);  // 追加
                _currentBackoff = TimeSpan.FromTicks(
                    Math.Min(_currentBackoff.Ticks * 2, MaxBackoff.Ticks));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "イベント処理エラー: {Message}", ex.Message);
                await Task.Delay(_currentBackoff, stoppingToken);  // 修正
                _currentBackoff = TimeSpan.FromTicks(
                    Math.Min(_currentBackoff.Ticks * 2, MaxBackoff.Ticks));
            }
        }
    }
}
```

---

### H-17: Redis CancellationToken 伝搬

| 項目 | 内容 |
|------|------|
| **対象ファイル** | `Services/CouponCacheServiceRedis.cs` |
| **修正内容** | 各 Redis 呼び出し前に `ct.ThrowIfCancellationRequested()` を追加 |

```csharp
public async Task<Coupon?> GetCouponAsync(string code, CancellationToken ct = default)
{
    ct.ThrowIfCancellationRequested();  // 追加
    try
    {
        var cached = await _db.StringGetAsync($"coupon:{code}");
        // ...
    }
    // ...
}

// 同様に SetCouponAsync, InvalidateAsync, GetUserUsageCountAsync, SetUserUsageCountAsync にも追加
```

---

### H-18: GenerateBatchAsync の N+1 クエリ改善

| 項目 | 内容 |
|------|------|
| **対象ファイル** | `Services/CouponCodeGenerator.cs` (L30-35), `Repositories/Interfaces/IRepositories.cs`, `Repositories/Repositories.cs` |
| **修正内容** | 一括生成 + 一括存在チェック |

**`Repositories/Interfaces/IRepositories.cs` — ICouponRepository に追加:**

```csharp
Task<HashSet<string>> FindExistingCodesAsync(IEnumerable<string> codes, CancellationToken ct = default);
```

**`Repositories/Repositories.cs` — CouponRepository に実装追加:**

```csharp
public async Task<HashSet<string>> FindExistingCodesAsync(
    IEnumerable<string> codes, CancellationToken ct = default)
{
    var codeList = codes.ToList();
    var existing = await context.Coupons
        .AsNoTracking()
        .Where(c => codeList.Contains(c.Code))
        .Select(c => c.Code)
        .ToListAsync(ct);
    return existing.ToHashSet();
}
```

**`Services/CouponCodeGenerator.cs` — GenerateBatchAsync 修正:**

```csharp
public async Task<List<string>> GenerateBatchAsync(int count, CancellationToken ct = default)
{
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

    var codes = new HashSet<string>(count);
    var maxAttempts = count * MaxRetries;

    for (var attempt = 0; codes.Count < count && attempt < maxAttempts; attempt++)
        codes.Add(GenerateRandomCode());

    // 一括存在チェック
    var existingCodes = await couponRepository.FindExistingCodesAsync(codes, ct);
    codes.ExceptWith(existingCodes);

    // 不足分を追加生成
    var remaining = count - codes.Count;
    for (var i = 0; i < remaining * MaxRetries && codes.Count < count; i++)
    {
        var code = GenerateRandomCode();
        if (!existingCodes.Contains(code))
            codes.Add(code);
    }

    if (codes.Count < count)
        throw new InvalidOperationException(
            $"一意なクーポンコードの生成に失敗しました（要求: {count}, 生成: {codes.Count}）");

    return codes.ToList();
}
```

---

### H-19: CalculateDiscountAsync の二重クエリ解消

| 項目 | 内容 |
|------|------|
| **対象ファイル** | `Services/CouponAppService.cs` (L162-170), `DTOs/Responses/CouponResponses.cs` |
| **修正内容** | `ValidationResult` に CouponId を含めるか、ValidateAsync からクーポン情報を返す |

**`DTOs/Responses/CouponResponses.cs` — ValidationResult 拡張:**

```csharp
// Before:
public record ValidationResult(
    bool IsValid,
    string? ErrorCode,
    string? ErrorMessage,
    decimal DiscountAmount);

// After:
public record ValidationResult(
    bool IsValid,
    string? ErrorCode,
    string? ErrorMessage,
    decimal DiscountAmount,
    string? CouponId = null,
    int? DiscountType = null,
    decimal? DiscountValue = null);
```

**`Services/CouponRuleEngine.cs` — ValidateAsync の return 修正:**

```csharp
// L117（成功時の return）:
return new ValidationResult(true, null, null, discount,
    coupon.Id, (int)coupon.DiscountType, coupon.DiscountValue);
```

**`Services/CouponAppService.cs` — CalculateDiscountAsync 修正:**

```csharp
public async Task<CalculateDiscountResponse> CalculateDiscountAsync(
    CalculateDiscountRequest request, CancellationToken ct = default)
{
    var result = await ruleEngine.ValidateAsync(
        request.CouponCode, request.UserId, request.OrderAmount, request.CategoryId, ct);

    if (!result.IsValid)
        return new CalculateDiscountResponse(false, result.ErrorCode, result.ErrorMessage,
            null, 0m, 0, 0m);

    // 二重クエリ解消: ValidationResult から直接取得
    return new CalculateDiscountResponse(true, null, null,
        result.CouponId, result.DiscountAmount,
        result.DiscountType ?? 0, result.DiscountValue ?? 0m);
}
```

---

### H-20: ページネーション未実装メソッドへの追加

| 項目 | 内容 |
|------|------|
| **対象ファイル** | `Repositories/Interfaces/IRepositories.cs`, `Repositories/Repositories.cs` |
| **修正対象メソッド** | `GetByUserIdAsync`, `GetByStatusAsync`, `GetByDateRangeAsync` |

**1. `IUserCouponRepository.GetByUserIdAsync` — ページネーション追加:**

```csharp
// Interface:
Task<List<UserCoupon>> GetByUserIdAsync(string userId, int page = 1, int pageSize = 50, CancellationToken ct = default);

// Implementation:
public async Task<List<UserCoupon>> GetByUserIdAsync(
    string userId, int page = 1, int pageSize = 50, CancellationToken ct = default)
    => await context.UserCoupons
        .AsNoTracking()
        .Include(uc => uc.Coupon)
        .Where(uc => uc.UserId == userId)
        .OrderByDescending(uc => uc.AcquiredAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(ct);
```

**2. `ICampaignRepository.GetByStatusAsync` — ページネーション追加:**

```csharp
// Interface:
Task<List<Campaign>> GetByStatusAsync(CampaignStatus status, int page = 1, int pageSize = 50, CancellationToken ct = default);

// Implementation:
public async Task<List<Campaign>> GetByStatusAsync(
    CampaignStatus status, int page = 1, int pageSize = 50, CancellationToken ct = default)
    => await context.Campaigns
        .AsNoTracking()
        .Where(c => c.Status == status)
        .OrderByDescending(c => c.CreatedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(ct);
```

**3. `ICouponUsageRepository.GetByDateRangeAsync` — ページネーション追加:**

```csharp
// Interface:
Task<List<CouponUsage>> GetByDateRangeAsync(DateTimeOffset from, DateTimeOffset to, int page = 1, int pageSize = 100, CancellationToken ct = default);

// Implementation:
public async Task<List<CouponUsage>> GetByDateRangeAsync(
    DateTimeOffset from, DateTimeOffset to, int page = 1, int pageSize = 100, CancellationToken ct = default)
    => await context.CouponUsages
        .AsNoTracking()
        .Where(u => u.UsedAt >= from && u.UsedAt <= to)
        .OrderByDescending(u => u.UsedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(ct);
```

**注**: 呼び出し元（`CouponAppService.GetUserCouponsAsync`, `CouponAnalyticsService.GetUsagesByDateRangeAsync`）もページネーションパラメータを受け取るよう修正が必要。

---

### H-21: テスト不足の解消

| 項目 | 内容 |
|------|------|
| **対象ファイル** | `Services/CouponService.Tests/` 以下に新規テストファイル追加 |
| **修正内容** | テストカバレッジ 80% 達成のため以下を追加 |

追加すべきテストファイル一覧:

| テストファイル名 | テスト種別 | 対象 |
|----------------|---------|------|
| `CouponAnalyticsServiceTests.cs` | Unit | CouponAnalyticsService 全メソッド |
| `CouponCodeGeneratorTests.cs` | Unit | CouponCodeGenerator.GenerateAsync / GenerateBatchAsync |
| `CouponCacheServiceInMemoryTests.cs` | Unit | CouponCacheServiceInMemory 全メソッド |
| `CouponEndpointsTests.cs` | Integration | `WebApplicationFactory` による全 Endpoint テスト |
| `AdminCouponEndpointsTests.cs` | Integration | Admin Endpoint 統合テスト |
| `InternalCouponEndpointsTests.cs` | Integration | Internal Endpoint 統合テスト |
| `CampaignEndpointsTests.cs` | Integration | Campaign Endpoint 統合テスト |
| `SecurityTests.cs` | Security | 認証/認可テスト（未認証アクセス拒否、ロール制限、IDOR） |
| `CouponValidatorsTests.cs` | Unit | 全 8 Validator の正常系/異常系 |

最低追加テスト数: 60 〜 80 件（既存 38 件 → 合計 100 件以上）

**テスト追加の優先度**:
1. `CouponEndpointsTests.cs`（Endpoint 統合テスト）— 最重要
2. `SecurityTests.cs`（セキュリティテスト）— 重要
3. 残りの Unit テスト

---

## Medium 修正（27 件）

以下、Medium 指摘は対象ファイルと修正概要を表形式でまとめる。

| # | 出典 Agent | 対象ファイル | 修正概要 |
|---|-----------|------------|---------|
| M-1 | architecture | `BackgroundServices/CouponExpirationService.cs` | Repository 呼出を Service 層経由に変更 |
| M-2 | architecture | `BackgroundServices/CampaignStatusService.cs` | Repository 呼出を Service 層経由に変更 |
| M-3 | architecture | `Services/CouponAppService.cs` L253 | `GetAllCouponsAsync` が `GetAvailableCouponsAsync` を呼出 → `GetAllCouponsAsync` を呼出に修正 |
| M-4 | architecture | `Repositories/Repositories.cs` | 7 Repository クラスを個別ファイルに分割（`CouponRepository.cs`, `CampaignRepository.cs`, `CouponTypeRepository.cs`, `UserCouponRepository.cs`, `CouponUsageRepository.cs`, `OutboxEventRepository.cs`, `CouponRestrictionRepository.cs`） |
| M-5 | ddd | `Services/CouponAppService.cs` Outbox Events | 匿名型 → 型付き Event record（`CouponAppliedEvent` 等）に変更 |
| M-6 | ddd | `Services/CampaignAppService.cs` | ActivateCampaign/PauseCampaign にステータス遷移ガード追加（Draft→Active→Paused→Ended） |
| M-7 | csharp | `Services/CouponCacheServiceInMemory.cs` | 陳腐化コメント「Phase 4. Will be replaced」を削除 |
| M-8 | csharp | `Services/CouponCacheServiceRedis.cs` L26 | `cached!` の `!` 演算子使用 → `cached.ToString()` に変更 |
| M-9 | config-di | `Configurations/CouponSettings.cs` | `FraudDetectionSettings`/`CacheSettings` に `[Range]` Data Annotations 追加 |
| M-10 | config-di | `appsettings.Production.json` | `"DetailedErrors": false` を明示追加 |
| M-11 | api-endpoint | `Endpoints/CampaignEndpoints.cs` L77, L84 | `ActivateCampaign`/`PauseCampaign` の `Results.Ok()` → `Results.NoContent()` に変更 |
| M-12 | api-endpoint | `Endpoints/AdminCouponEndpoints.cs` L54 | `Results.NotFound()` → `Results.Problem(statusCode: 404, detail: "...")` に変更（RFC 9457 準拠） |
| M-13 | api-endpoint | 全 Endpoints | `.Produces<T>()` / `.ProducesValidationProblem()` / `.ProducesProblem(404)` 追加 |
| M-14 | data-access | `Repositories/Repositories.cs` L47-50 | `GetExpiredActiveCouponsAsync` に `.AsNoTracking()` 追加 |
| M-15 | data-access | `Repositories/Repositories.cs` L109, L113, L117 | Campaign メソッド (`GetCampaignsToEndAsync`, `GetCampaignsToStartAsync`) — 変更操作用なので AsNoTracking 不要（現状正しい） |
| M-16 | data-access | `Repositories/Repositories.cs` MarkAsPublishedAsync L263 | `DateTimeOffset.UtcNow` → `timeProvider.GetUtcNow()` に変更。**`OutboxEventRepository` のコンストラクタに `TimeProvider` を DI 注入する必要あり**: `public class OutboxEventRepository(AppDbContext context, TimeProvider timeProvider)` |
| M-17 | error-logging | `Exceptions/CouponExceptions.cs` | 共通基底クラス `CouponServiceException` を追加 |
| M-18 | error-logging | `Infrastructure/Observability/CouponMetrics.cs` | 静的 `Meter` → `IMeterFactory` DI に変更 |
| M-19 | security | `Services/CouponCodeGenerator.cs` L48 | `Random.Shared.Next()` → `RandomNumberGenerator.GetInt32()` に変更（暗号論的セキュアな PRNG） |
| M-20 | security | `Consumers/OrderEventConsumer.cs` L59-65 | Kafka メッセージの入力検証追加（payload が null/空文字の場合のスキップ） |
| M-21 | security | `Consumers/OrderEventConsumer.cs` HandleUserDeleted | 仮名化処理の実装（CouponUsage/UserCoupon の userId を匿名化） |
| M-22 | performance | `Repositories/Repositories.cs` 複数箇所 | Select プロジェクション追加（全エンティティ取得 → 必要フィールドのみ） |
| M-23 | performance | `Services/CouponAppService.cs` L29-30 | `GetAvailableCouponsAsync` の Count + List 二重クエリを最適化 |
| M-24 | performance | `Services/CouponAnalyticsService.cs` L60 | `GetTopCouponsAsync` の topN パラメータに上限追加（`Math.Min(topN, 100)`） |
| M-25 | resilience | `BackgroundServices/OutboxPublisher.cs` | PROCESSING ステータスのリカバリ機能追加（起動時に PROCESSING → PENDING に戻す） |
| M-26 | resilience | `BackgroundServices/CouponExpirationService.cs`, `CampaignStatusService.cs` | エラー後の動的バックオフ追加 |
| M-27 | resilience | `Consumers/OrderEventConsumer.cs` | Dead Letter Topic 転送の基盤実装 |

### M-3: GetAllCouponsAsync の修正詳細

```csharp
// Before (L253-260):
public async Task<PagedResponse<CouponResponse>> GetAllCouponsAsync(
    CouponQueryParams query, CancellationToken ct = default)
{
    var now = timeProvider.GetUtcNow();
    var coupons = await couponRepository.GetAvailableCouponsAsync(now, query.Page, query.PageSize, ct);
    var totalCount = await couponRepository.CountAvailableCouponsAsync(now, ct);

// After:
public async Task<PagedResponse<CouponResponse>> GetAllCouponsAsync(
    CouponQueryParams query, CancellationToken ct = default)
{
    var coupons = await couponRepository.GetAllCouponsAsync(query, ct);
    var totalCount = await couponRepository.CountAllCouponsAsync(ct);
```

### M-6: Campaign ステータス遷移ガード

```csharp
// CampaignAppService.ActivateCampaignAsync:
if (campaign.Status is not (CampaignStatus.Draft or CampaignStatus.Paused))
    throw new BusinessException($"キャンペーンのステータス {campaign.Status} から Active への遷移はできません");

// CampaignAppService.PauseCampaignAsync:
if (campaign.Status != CampaignStatus.Active)
    throw new BusinessException($"キャンペーンのステータス {campaign.Status} から Paused への遷移はできません");
```

### M-19: CouponCodeGenerator の PRNG セキュリティ強化

```csharp
using System.Security.Cryptography;

// Before:
code[i] = Alphabet[Random.Shared.Next(Alphabet.Length)];

// After:
code[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
```

### M-21: HandleUserDeleted 仮名化処理の実装

```csharp
private async Task HandleUserDeletedAsync(string payload, IServiceScope scope, CancellationToken ct)
{
    var deleteEvent = JsonSerializer.Deserialize<UserDeletedEvent>(payload);
    if (deleteEvent is null) return;

    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // CouponUsage のユーザーID を仮名化
    var anonymizedId = $"deleted-{Guid.NewGuid():N}";
    await context.CouponUsages
        .Where(u => u.UserId == deleteEvent.UserId)
        .ExecuteUpdateAsync(s => s.SetProperty(u => u.UserId, anonymizedId), ct);

    // UserCoupon のユーザーID を仮名化
    await context.UserCoupons
        .Where(uc => uc.UserId == deleteEvent.UserId)
        .ExecuteUpdateAsync(s => s.SetProperty(uc => uc.UserId, anonymizedId), ct);

    logger.LogInformation("ユーザー仮名化完了: 元UserId のデータを匿名化しました");
}
```

**注**: `HandleUserDeleted` を `HandleUserDeletedAsync` に変更し非同期化。`ExecuteAsync` 内の呼び出しも以下のように修正:

```csharp
// Before:
case "user.deleted":
    HandleUserDeleted(result.Message.Value);
    break;

// After:
case "user.deleted":
    await HandleUserDeletedAsync(result.Message.Value, scope, stoppingToken);
    break;
```

**補足**: `HandleUserDeletedAsync` は `AppDbContext` を直接使用（`ExecuteUpdateAsync` バルク操作のため Repository パターンをバイパス）。パフォーマンスとのトレードオフで許容とする。

---

## Low 修正（12 件）

| # | 出典 Agent | 対象ファイル | 修正概要 |
|---|-----------|------------|---------|
| L-1 | architecture | `Services/CouponCacheServiceRedis.cs`, `Program.cs` | クラス名 `CouponCacheService` → `CouponCacheServiceRedis` に変更（ファイル名と一致させる）。**Program.cs の DI 登録** `AddScoped<ICouponCacheService, CouponCacheService>()` も `CouponCacheServiceRedis` に更新必須 |
| L-2 | csharp | `GrpcServices/CouponGrpcService.cs` | future-plan コメントの整理（実装計画を TODO に変更） |
| L-3 | api-endpoint | `Endpoints/CouponEndpoints.cs` L44 | ページネーションパラメータのサイレント補正をログ出力付きに変更 |
| L-4 | api-endpoint | `Endpoints/InternalCouponEndpoints.cs` L52 | `Results.Created` の Location URI を正しいリソース URI に修正 |
| L-5 | data-access | `Models/Campaign.cs` | `[Timestamp] [Column("row_version")] public byte[] RowVersion` 追加 |
| L-6 | data-access | `Models/CouponType.cs` | `[Timestamp] [Column("row_version")] public byte[] RowVersion` 追加 |
| L-7 | error-logging | `Program.cs` L212 | `CouponFraudDetectedException` → 403 は 422 (`BusinessException` 扱い) の方が適切か検討 |
| L-8 | error-logging | OpenTelemetry | OTel exporter の設定追加（Aspire 統合 or OTLP エンドポイント） |
| L-9 | dependency | `CouponService.Tests.csproj` | `Testcontainers.PostgreSql` パッケージ追加 |
| L-10 | test-quality | `FraudDetectionServiceTests.cs` | テスト名と Assert の矛盾修正（`Should_ReturnClear_When_ExactlyAtThreshold`） |
| L-11 | performance | `Services/CouponAppService.cs` RedeemCouponAsync | Outbox ProduceAsync を非同期バッチ化（現状は 1 件ずつだが影響軽微） |
| L-12 | resilience | Aspire 統合 | `.NET Aspire` の `WithReference` で CouponService を AppHost に統合 |

---

## 修正の実行順序

修正は以下の順序で実施することを推奨する。各行は独立したコミット単位。

| 順序 | 指摘 ID | コミットメッセージ例 |
|------|---------|-------------------|
| 1 | C-2 | `fix(coupon): Validator DiscountType ロジック反転を修正` |
| 2 | C-3 | `fix(coupon): JWT ValidAlgorithms を RS256 に固定` |
| 3 | C-1 + H-9 | `fix(coupon): ReleaseCouponAsync を冪等化（IsReleased フラグ追加）` |
| 4 | H-2 + H-3 | `fix(coupon): RedeemCouponAsync に有効性再検証・割引額検証を追加` |
| 5 | H-4 | `fix(coupon): RedeemCoupon の userId をリクエストボディから取得` |
| 6 | H-5 | `fix(coupon): InternalServiceHandler を複数クレーム検証に強化` |
| 7 | H-1 | `fix(coupon): レート制限を全エンドポイントに適用` |
| 8 | H-6 + H-7 + H-11 | `feat(coupon): OpenAPI / EF Core OTel / Identity.Web パッケージ追加` |
| 9 | H-12 + H-13 | `fix(coupon): Kafka Producer 設定強化・ヘルスチェック追加` |
| 10 | H-14 + H-15 + H-17 | `fix(coupon): Redis 例外ハンドリング拡大・フォールバック・ct 伝搬` |
| 11 | H-16 | `fix(coupon): Kafka Consumer 指数バックオフ実装` |
| 12 | H-8 | `refactor(coupon): OutboxPublisher を Repository 経由に変更` |
| 13 | H-18 | `perf(coupon): GenerateBatchAsync 一括存在チェック実装` |
| 14 | H-19 | `perf(coupon): CalculateDiscountAsync 二重クエリ解消` |
| 15 | H-20 | `feat(coupon): ページネーション未実装メソッドに追加` |
| 16 | H-10 | `fix(coupon): CouponRestriction に updated_at 追加` |
| 17 | M-1〜M-27 | Medium 修正（個別コミット） |
| 18 | L-1〜L-12 | Low 修正（個別コミット） |
| 19 | H-21 | `test(coupon): 統合テスト・セキュリティテスト・Unit テスト追加` |

---

## マイグレーション一覧

修正に伴い、以下の EF Core マイグレーションが必要:

| # | マイグレーション名 | 対象エンティティ | 変更内容 |
|---|------------------|----------------|---------|
| 1 | `AddCouponUsageReleasedFields` | CouponUsage | `is_released` (bool), `released_at` (DateTimeOffset?) 追加 |
| 2 | `AddCouponRestrictionUpdatedAt` | CouponRestriction | `updated_at` (DateTimeOffset) 追加 |
| 3 | `AddCampaignRowVersion` | Campaign | `row_version` (byte[]) 追加 |
| 4 | `AddCouponTypeRowVersion` | CouponType | `row_version` (byte[]) 追加 |

---

## 影響範囲サマリー

| ファイル | 修正種別 | 関連指摘 ID |
|---------|---------|-----------|
| `Program.cs` | 変更 | C-3, H-1, H-6, H-7, H-12, H-13, H-15 |
| `CouponService.csproj` | 変更 | H-6, H-7, H-11, H-13 |
| `Services/CouponAppService.cs` | 変更 | C-1, H-2, H-3, H-19, M-3, M-23 |
| `Validators/CouponValidators.cs` | 変更 | C-2, H-4 |
| `Models/CouponUsage.cs` | 変更 | C-1 |
| `Models/CouponRestriction.cs` | 変更 | H-10 |
| `Endpoints/CouponEndpoints.cs` | 変更 | H-1, M-13 |
| `Endpoints/AdminCouponEndpoints.cs` | 変更 | H-1, M-12, M-13 |
| `Endpoints/InternalCouponEndpoints.cs` | 変更 | H-1, H-4, M-13 |
| `Endpoints/CampaignEndpoints.cs` | 変更 | H-1, M-11, M-13 |
| `Infrastructure/Authorization/InternalServiceHandler.cs` | 変更 | H-5 |
| `Services/CouponCacheServiceRedis.cs` | 変更 | H-14, H-15, H-17, L-1 |
| `Services/CouponCodeGenerator.cs` | 変更 | H-18, M-19 |
| `Consumers/OrderEventConsumer.cs` | 変更 | H-16, M-20, M-21 |
| `BackgroundServices/OutboxPublisher.cs` | 変更 | H-8, M-25 |
| `BackgroundServices/CouponExpirationService.cs` | 変更 | M-1, M-26 |
| `BackgroundServices/CampaignStatusService.cs` | 変更 | M-2, M-26 |
| `Repositories/Repositories.cs` | 変更 | H-8, H-18, H-20, M-14, M-16 |
| `Repositories/Interfaces/IRepositories.cs` | 変更 | H-8, H-18, H-20 |
| `Services/Interfaces/IServiceInterfaces.cs` | 変更 | H-19 |
| `DTOs/Responses/CouponResponses.cs` | 変更 | H-19 |
| `DTOs/Requests/CouponRequests.cs` | 変更 | H-4 |
| `Services/CouponRuleEngine.cs` | 変更 | H-3, H-19 |
| `Configurations/CouponSettings.cs` | 変更 | M-9 |
| `appsettings.Production.json` | 変更 | M-10 |
| `Exceptions/CouponExceptions.cs` | 変更 | M-17 |
| `Infrastructure/Observability/CouponMetrics.cs` | 変更 | M-18 |
| `Services/CampaignAppService.cs` | 変更 | M-6 |
| `Services/CouponCacheServiceInMemory.cs` | 変更 | M-7 |
| `Services/CouponAnalyticsService.cs` | 変更 | M-24 |
| `Models/Campaign.cs` | 変更 | L-5 |
| `Models/CouponType.cs` | 変更 | L-6 |
| `GrpcServices/CouponGrpcService.cs` | 変更 | L-2 |
| `CouponService.Tests.csproj` | 変更 | L-9 |
| **テストプロジェクト（新規追加）** | 新規 | H-21 |

---

## 追加検出事項（計画書レビュー時に発見）

### 追加修正: ReleaseCoupon の所有権検証

| 項目 | 内容 |
|------|------|
| **出典** | security-reviewer (High) — check-report "ReleaseCoupon の所有権未検証" |
| **対象ファイル** | `Services/CouponAppService.cs` `ReleaseCouponAsync` |
| **問題** | `ReleaseCouponAsync` は `CouponId` + `OrderId` のみで操作を受け入れるが、リクエスト元サービスが当該注文の所有者であるかを検証していない。内部サービス認証（H-5）で保護されているが、内部サービス間でも最小権限原則に基づき操作対象の妥当性を検証すべき |
| **対応方針** | 現時点では InternalServiceOnly 認証ポリシー（H-5 で強化済み）による保護で十分と判断し、**追加のビジネスレベル所有権チェックは保留**とする。ただし、今後サービス間の信頼境界を細分化する場合は、`ReleaseCouponRequest` に呼び出し元サービス識別子を追加し、Outbox イベントとの照合による所有権検証を検討する |

### H-15 補足: InMemory フォールバックの TTL 設定

Redis 障害時の InMemory フォールバックには TTL を設定し、長期間のステール（古い）データを防止する:

```csharp
// CouponCacheServiceRedis.cs — フォールバック時のキャッシュ書き込みに TTL 付与
catch (RedisException ex)
{
    logger.LogWarning(ex, "Redis エラー — InMemory フォールバック: coupon:{CouponCode}", code);
    return memoryCache.TryGetValue($"coupon:{code}", out Coupon? fallback) ? fallback : null;
}

// SetCouponAsync のフォールバック（上記に追加して Redis SetCouponAsync の catch ブロック内）:
catch (RedisException ex)
{
    logger.LogWarning(ex, "Redis 書き込みエラー — InMemory フォールバック: coupon:{CouponCode}", code);
    // フォールバック: InMemory に短い TTL で保存（Redis 復旧時の整合性を確保）
    var cacheOptions = new MemoryCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)  // Redis 正常時の TTL より短く設定
    };
    memoryCache.Set($"coupon:{code}", coupon, cacheOptions);
}
```

### M-22 補足: Select プロジェクション対象の具体的箇所

performance-reviewer が指摘した全エンティティ取得箇所（Select プロジェクション未使用）:

| # | Repository メソッド | 行番号（概算） | 返却型 | 改善案 |
|---|-------------------|---------------|--------|--------|
| 1 | `CouponRepository.GetAvailableCouponsAsync` | L26-33 | `List<Coupon>` | `Select(c => new CouponSummaryResponse(...))` に変更 |
| 2 | `CouponRepository.GetTopByUsageAsync` | L65-70 | `List<Coupon>` | `Select` で必要フィールドのみ取得 |
| 3 | `CouponRepository.GetAllCouponsAsync` | L72-78 | `List<Coupon>` | `Select(c => new CouponResponse(...))` に変更 |
| 4 | `UserCouponRepository.GetByUserIdAsync` | L152-158 | `List<UserCoupon>` | `Select` でユーザークーポン応答に必要なフィールドのみ |
| 5 | `CampaignRepository.GetByStatusAsync` | L120-125 | `List<Campaign>` | `Select` でキャンペーン応答に必要なフィールドのみ |
| 6 | `CouponUsageRepository.GetByDateRangeAsync` | L210-215 | `List<CouponUsage>` | `Select` で分析に必要なフィールドのみ |

**注**: Select プロジェクションを Repository 層に追加する場合、返却型が DTO になりレイヤー依存方向（Repository → DTO）が発生する。代替案として Service 層で Select を行うか、匿名型で返す方法もある。Repository は Entity を返し、Service 層で DTO に変換する現行パターンを維持しつつ、必要カラムのみを `Select` する方式を推奨。

---

## 除外・保留事項（check-report エスカレーション項目）

check-report-2.md のエスカレーション事項のうち、本計画書で **対応済み** および **保留** の判定:

| # | エスカレーション内容 | 判定 | 備考 |
|---|-------------------|------|------|
| 1 | ReleaseCouponAsync 冪等性修正方針 | ✅ 対応済み | C-1 で IsReleased フラグ方式を採用 |
| 2 | InternalServiceHandler 認証強化方針 | ✅ 対応済み | H-5 で sub+scope+iss 3 クレーム検証を採用 |
| 3 | HandleUserDeleted 仮名化処理 | ✅ 対応済み | M-21 で ExecuteUpdateAsync による仮名化を実装 |
| 4 | Redis 動的フォールバック戦略 | ✅ 対応済み | H-15 で InMemory フォールバック方式を採用 |
| 5 | Aggregate Root 境界の再編成 | ⏸️ **保留** | 大規模リファクタリングのため本計画書では対象外。テックリード判断後に別計画書で対応 |
| 6 | xUnit v3 移行方針 | ⏸️ **保留** | プロジェクト全体の方針決定が必要。AGENTS.md §8.1 では `xunit 2.*` を記載。現行 CouponService.Tests は xUnit v3 (`xunit.v3`) を使用中。プロジェクト統一方針の決定後に対応 |
| 7 | `UseCors()` の要否 | ⏸️ **保留** | CouponService は内部サービスのためブラウザ直接アクセスなし。ApiGateway 経由のみであれば CORS 不要と判断。アーキテクト確認後に最終決定 |
