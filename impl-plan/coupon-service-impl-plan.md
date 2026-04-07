# CouponService フェーズ別実装計画書

> **対象サービス**: CouponService（クーポン・キャンペーン管理）
> **ポート**: 5006
> **DB**: PostgreSQL (coupondb) + EF Core 10
> **メッセージング**: Apache Kafka（Outbox パターン）
> **キャッシュ**: Redis
> **設計書**: `design-docs/coupon-service-design.md`
> **規約**: AGENTS.md / `.github/instructions/` 配下のインストラクションファイル群

---

## 目次

1. [Phase 1: プロジェクト基盤構築](#phase-1-プロジェクト基盤構築)
2. [Phase 2: エンティティ・Value Object・Enum 定義](#phase-2-エンティティvalue-objectenum-定義)
3. [Phase 3: Repository 層実装](#phase-3-repository-層実装)
4. [Phase 4: Service 層実装](#phase-4-service-層実装)
5. [Phase 5: Endpoints 実装](#phase-5-endpoints-実装)
6. [Phase 6: Kafka イベント連携](#phase-6-kafka-イベント連携)
7. [Phase 7: Redis キャッシュ連携](#phase-7-redis-キャッシュ連携)
8. [Phase 8: 認証・認可・セキュリティ](#phase-8-認証認可セキュリティ)
9. [Phase 9: 単体テスト・統合テスト](#phase-9-単体テスト統合テスト)
10. [Phase 10: 可観測性](#phase-10-可観測性)
11. [Phase 11: Docker / デプロイ準備](#phase-11-docker--デプロイ準備)

---

## Phase 1: プロジェクト基盤構築

### 目的

CouponService プロジェクトの骨格を構築する。ビルド可能な最小構成を作成し、以降のフェーズの土台とする。ポート 5006 で起動し `/health` エンドポイントで応答を確認できる状態にする。

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `CouponService/CouponService.csproj` | プロジェクト定義（EF Core, Kafka, Redis, Serilog, OpenTelemetry 等） |
| 2 | `CouponService/Program.cs` | エントリポイントスケルトン（最小構成） |
| 3 | `CouponService/appsettings.json` | 共通設定（安全なデフォルト値のみ、秘密情報禁止） |
| 4 | `CouponService/appsettings.Development.json` | 開発環境設定 |
| 5 | `CouponService/appsettings.Production.json` | 本番環境設定（環境変数参照のみ） |
| 6 | `CouponService/Dockerfile` | マルチステージビルド + 非 root 実行 |
| 7 | `CouponService/.dockerignore` | ビルド不要ファイルの除外 |

### 1.1 CouponService.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <!-- ORM -->
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.*" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.*" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.*" PrivateAssets="all" />

    <!-- 認証 -->
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.*" />

    <!-- バリデーション -->
    <PackageReference Include="FluentValidation" Version="11.*" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.*" />

    <!-- メッセージング -->
    <PackageReference Include="Confluent.Kafka" Version="2.*" />

    <!-- キャッシュ -->
    <PackageReference Include="StackExchange.Redis" Version="2.*" />

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
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.*" />

    <!-- ヘルスチェック -->
    <PackageReference Include="AspNetCore.HealthChecks.NpgSql" Version="9.*" />
    <PackageReference Include="AspNetCore.HealthChecks.Redis" Version="9.*" />

    <!-- gRPC -->
    <PackageReference Include="Grpc.AspNetCore" Version="2.*" />
  </ItemGroup>
</Project>
```

### 1.2 ディレクトリ構造

```
CouponService/
├── CouponService.csproj
├── Program.cs
├── Endpoints/
│   ├── CouponEndpoints.cs
│   ├── AdminCouponEndpoints.cs
│   ├── CampaignEndpoints.cs
│   └── InternalCouponEndpoints.cs
├── GrpcServices/
│   └── CouponGrpcService.cs
├── Services/
│   ├── Interfaces/
│   │   ├── ICouponService.cs
│   │   ├── ICampaignService.cs
│   │   ├── ICouponRuleEngine.cs
│   │   ├── IFraudDetectionService.cs
│   │   ├── ICouponAnalyticsService.cs
│   │   ├── ICouponCacheService.cs
│   │   └── ICouponCodeGenerator.cs
│   ├── CouponService.cs
│   ├── CampaignService.cs
│   ├── CouponRuleEngine.cs
│   ├── FraudDetectionService.cs
│   ├── CouponAnalyticsService.cs
│   ├── CouponCacheService.cs
│   └── CouponCodeGenerator.cs
├── Repositories/
│   ├── Interfaces/
│   │   ├── ICouponRepository.cs
│   │   ├── ICampaignRepository.cs
│   │   ├── ICouponUsageRepository.cs
│   │   ├── ICouponRestrictionRepository.cs
│   │   ├── IUserCouponRepository.cs
│   │   └── IOutboxEventRepository.cs
│   ├── CouponRepository.cs
│   ├── CampaignRepository.cs
│   ├── CouponUsageRepository.cs
│   ├── CouponRestrictionRepository.cs
│   ├── UserCouponRepository.cs
│   └── OutboxEventRepository.cs
├── Models/
│   ├── Campaign.cs
│   ├── Coupon.cs
│   ├── CouponType.cs
│   ├── CouponRestriction.cs
│   ├── Promotion.cs
│   ├── UserCoupon.cs
│   ├── CouponUsage.cs
│   └── OutboxEvent.cs
├── DTOs/
│   ├── Requests/
│   │   ├── CreateCampaignRequest.cs
│   │   ├── UpdateCampaignRequest.cs
│   │   ├── CreateCouponRequest.cs
│   │   ├── UpdateCouponRequest.cs
│   │   ├── ValidateCouponRequest.cs
│   │   ├── CalculateDiscountRequest.cs
│   │   ├── RedeemCouponRequest.cs
│   │   └── OrderItemDto.cs
│   └── Responses/
│       ├── CouponResponse.cs
│       ├── CampaignResponse.cs
│       ├── ValidationResponse.cs
│       ├── DiscountCalculationResponse.cs
│       ├── UserCouponResponse.cs
│       └── CouponAnalyticsResponse.cs
├── Validators/
│   ├── CreateCouponRequestValidator.cs
│   ├── CreateCampaignRequestValidator.cs
│   └── ValidateCouponRequestValidator.cs
├── Exceptions/
│   ├── CouponException.cs
│   ├── CouponNotFoundException.cs
│   ├── CouponExpiredException.cs
│   ├── CouponUsageLimitExceededException.cs
│   ├── InvalidCouponException.cs
│   ├── CouponFraudDetectedException.cs
│   ├── ConcurrencyException.cs
│   ├── UnauthorizedException.cs
│   └── ForbiddenException.cs
├── Consumers/
│   └── OrderEventConsumer.cs
├── BackgroundServices/
│   ├── OutboxPublisher.cs
│   ├── CouponExpirationService.cs
│   └── CampaignStatusService.cs
├── Infrastructure/
│   ├── Persistence/
│   │   └── AppDbContext.cs
│   └── Middleware/
│       ├── CorrelationIdMiddleware.cs
│       └── CorrelationIdMiddlewareExtensions.cs
├── Configurations/
│   └── CouponSettings.cs
├── Migrations/
├── appsettings.json
├── appsettings.Development.json
└── appsettings.Production.json
```

### 1.3 Program.cs スケルトン

```csharp
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

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
      "SkiShop": "Information"
    }
  },
  "Kafka": {
    "BootstrapServers": "",
    "GroupId": "coupon-service"
  },
  "Coupon": {
    "FraudDetection": {
      "WindowMinutes": 10,
      "MaxUsagesInWindow": 3
    },
    "Cache": {
      "CouponTtlMinutes": 10,
      "UsageTtlMinutes": 5
    }
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
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

### 1.6 appsettings.Production.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "SkiShop": "Information"
    }
  }
}
```

### 1.7 Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["CouponService/CouponService.csproj", "CouponService/"]
RUN dotnet restore "CouponService/CouponService.csproj"
COPY . .
WORKDIR "/src/CouponService"
RUN dotnet publish "CouponService.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN groupadd -r skishop && useradd -r -g skishop -d /app -s /sbin/nologin skishop
COPY --from=build --chown=skishop:skishop /app/publish .
USER skishop

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 5006
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:5006/health || exit 1
ENTRYPOINT ["dotnet", "CouponService.dll"]
```

### 1.8 .dockerignore

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

- [ ] `dotnet build CouponService/CouponService.csproj` — 警告なし成功
- [ ] `dotnet run --project CouponService` — 起動確認（`/health` で 200 応答）
- [ ] .csproj: `TreatWarningsAsErrors=true`, `Nullable=enable`, `TargetFramework=net10.0`
- [ ] .csproj: プレリリース版パッケージなし（`-preview`, `-beta`, `-rc` なし）
- [ ] appsettings.json: 秘密情報なし（パスワード、API キー、接続文字列のパスワードなし）
- [ ] appsettings.json: `DetailedErrors: false`, `AddServerHeader: false`
- [ ] Dockerfile: マルチステージビルド、非 root ユーザー、HEALTHCHECK あり
- [ ] Dockerfile: ベースイメージに `latest` タグなし（`10.0` 固定）
- [ ] .dockerignore: `bin/`, `obj/`, `.git/`, `*.md` 除外
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 2: エンティティ・Value Object・Enum 定義

### 目的

設計書 §5（データモデル）および追記セクション §A（EF Core エンティティ C# クラス定義）に基づき、全エンティティ、DbContext、例外クラス、DTO を定義する。マイグレーションの生成・適用が可能な状態にする。

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `CouponService/Models/Campaign.cs` | Campaign エンティティ（設計書 §A.1） |
| 2 | `CouponService/Models/CouponType.cs` | CouponType エンティティ（設計書 §A.2） |
| 3 | `CouponService/Models/Coupon.cs` | Coupon エンティティ（設計書 §A.3）— Aggregate Root |
| 4 | `CouponService/Models/CouponRestriction.cs` | CouponRestriction エンティティ（設計書 §A.4） |
| 5 | `CouponService/Models/UserCoupon.cs` | UserCoupon エンティティ（設計書 §A.5） |
| 6 | `CouponService/Models/CouponUsage.cs` | CouponUsage エンティティ（設計書 §A.6） |
| 7 | `CouponService/Models/Promotion.cs` | Promotion エンティティ（設計書 §A.7） |
| 8 | `CouponService/Models/OutboxEvent.cs` | OutboxEvent エンティティ（設計書 §A.8） |
| 9 | `CouponService/Infrastructure/Persistence/AppDbContext.cs` | DbContext（設計書 §B） |
| 10 | `CouponService/Exceptions/CouponException.cs` | 基底例外クラス |
| 11 | `CouponService/Exceptions/CouponNotFoundException.cs` | HTTP 404 例外 |
| 12 | `CouponService/Exceptions/CouponExpiredException.cs` | HTTP 422 例外（有効期間外） |
| 13 | `CouponService/Exceptions/CouponUsageLimitExceededException.cs` | HTTP 422 例外（利用上限） |
| 14 | `CouponService/Exceptions/InvalidCouponException.cs` | HTTP 422 例外（無効クーポン） |
| 15 | `CouponService/Exceptions/CouponFraudDetectedException.cs` | HTTP 403 例外（不正検知） |
| 16 | `CouponService/Exceptions/ConcurrencyException.cs` | HTTP 409 例外（楽観的ロック） |
| 17 | `CouponService/Exceptions/UnauthorizedException.cs` | HTTP 401 例外 |
| 18 | `CouponService/Exceptions/ForbiddenException.cs` | HTTP 403 例外 |
| 19 | `CouponService/DTOs/Requests/CreateCampaignRequest.cs` | キャンペーン作成 DTO |
| 20 | `CouponService/DTOs/Requests/CreateCouponRequest.cs` | クーポン作成 DTO |
| 21 | `CouponService/DTOs/Requests/ValidateCouponRequest.cs` | クーポン検証 DTO |
| 22 | `CouponService/DTOs/Requests/CalculateDiscountRequest.cs` | 割引計算 DTO |
| 23 | `CouponService/DTOs/Requests/RedeemCouponRequest.cs` | クーポン利用確定 DTO |
| 24 | `CouponService/DTOs/Requests/OrderItemDto.cs` | 注文明細 DTO |
| 24a | `CouponService/DTOs/Requests/UpdateCampaignRequest.cs` | キャンペーン更新 DTO（設計書 §6.4） |
| 24b | `CouponService/DTOs/Requests/UpdateCouponRequest.cs` | クーポン更新 DTO（設計書 §6.4） |
| 24c | `CouponService/Models/DateRange.cs` | DateRange Value Object（設計書 §D.8） |
| 25 | `CouponService/DTOs/Responses/CouponResponse.cs` | クーポンレスポンス DTO |
| 26 | `CouponService/DTOs/Responses/CampaignResponse.cs` | キャンペーンレスポンス DTO |
| 27 | `CouponService/DTOs/Responses/ValidationResponse.cs` | 検証結果 DTO |
| 28 | `CouponService/DTOs/Responses/DiscountCalculationResponse.cs` | 割引計算結果 DTO |
| 29 | `CouponService/DTOs/Responses/UserCouponResponse.cs` | ユーザークーポン DTO |
| 30 | `CouponService/DTOs/Responses/CouponAnalyticsResponse.cs` | 分析レポート DTO |
| 31 | `CouponService/Configurations/CouponSettings.cs` | 設定クラス |

### 2.1 エンティティ一覧（設計書 §5 / §A 準拠）

| エンティティ | テーブル名 | Aggregate Root | 楽観的ロック | 主要カラム数 |
|------------|----------|---------------|------------|------------|
| `Campaign` | `campaigns` | ✅ | ❌ | 10 |
| `CouponType` | `coupon_types` | ✅ | ❌ | 6 |
| `Coupon` | `coupons` | ✅ | ✅（`RowVersion`） | 18 |
| `CouponRestriction` | `coupon_restrictions` | ❌（Coupon 配下） | ❌ | 8 |
| `UserCoupon` | `user_coupons` | ❌（Coupon 配下） | ❌ | 8 |
| `CouponUsage` | `coupon_usages` | ❌（Coupon 配下） | ❌ | 8 |
| `Promotion` | `promotions` | ✅ | ❌ | 10 |
| `OutboxEvent` | `outbox_events` | ✅ | ❌ | 10 |

### 2.2 Coupon エンティティ（Aggregate Root — 設計書 §A.3）

```csharp
[Table("coupons")]
public class Coupon
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("campaign_id")]
    [MaxLength(36)]
    public string? CampaignId { get; set; }

    [Column("coupon_type_id")]
    [MaxLength(36)]
    public string? CouponTypeId { get; set; }

    [Column("code")]
    [Required]
    [MaxLength(30)]
    public string Code { get; set; } = string.Empty;

    [Column("discount_type")]
    [Required]
    [MaxLength(20)]
    public string DiscountType { get; set; } = "PERCENTAGE";

    [Column("discount_value")]
    [Required]
    [Precision(10, 2)]
    public decimal DiscountValue { get; set; }

    [Column("min_order_amount")]
    [Required]
    [Precision(10, 2)]
    public decimal MinOrderAmount { get; set; }

    [Column("max_discount_amount")]
    [Precision(10, 2)]
    public decimal? MaxDiscountAmount { get; set; }

    [Column("max_usage_count")]
    [Required]
    public int MaxUsageCount { get; set; } = 1;

    [Column("current_usage_count")]
    [Required]
    public int CurrentUsageCount { get; set; }

    [Column("max_usage_per_user")]
    [Required]
    public int MaxUsagePerUser { get; set; } = 1;

    [Column("target_category")]
    [MaxLength(100)]
    public string? TargetCategory { get; set; }

    [Column("target_product_id")]
    [MaxLength(36)]
    public string? TargetProductId { get; set; }

    [Column("is_active")]
    [Required]
    public bool IsActive { get; set; } = true;

    [Column("valid_from")]
    [Required]
    public DateTimeOffset ValidFrom { get; set; }

    [Column("valid_until")]
    [Required]
    public DateTimeOffset ValidUntil { get; set; }

    [Column("created_at")]
    [Required]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    [Required]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    // ── ナビゲーション ──
    public Campaign? Campaign { get; set; }
    public CouponType? CouponType { get; set; }
    public ICollection<CouponRestriction> Restrictions { get; set; } = [];
    public ICollection<UserCoupon> UserCoupons { get; set; } = [];
    public ICollection<CouponUsage> Usages { get; set; } = [];
}
```

### 2.3 AppDbContext（設計書 §B 準拠）

```csharp
public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    TimeProvider timeProvider) : DbContext(options)
{
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponType> CouponTypes => Set<CouponType>();
    public DbSet<CouponRestriction> CouponRestrictions => Set<CouponRestriction>();
    public DbSet<UserCoupon> UserCoupons => Set<UserCoupon>();
    public DbSet<CouponUsage> CouponUsages => Set<CouponUsage>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Campaign
        modelBuilder.Entity<Campaign>(entity =>
        {
            entity.HasIndex(c => c.Status);
            entity.HasIndex(c => c.StartDate);
            entity.HasIndex(c => c.EndDate);
            entity.HasMany(c => c.Coupons)
                .WithOne(cp => cp.Campaign)
                .HasForeignKey(cp => cp.CampaignId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_campaigns_status",
                "status IN ('DRAFT','ACTIVE','PAUSED','ENDED')"));
        });

        // CouponType
        modelBuilder.Entity<CouponType>(entity =>
        {
            entity.HasIndex(ct => ct.Name).IsUnique();
            entity.HasMany(ct => ct.Coupons)
                .WithOne(c => c.CouponType)
                .HasForeignKey(c => c.CouponTypeId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_coupon_types_usage_limitation",
                "usage_limitation_type IN ('SINGLE_USE','MULTI_USE','TIME_LIMITED')"));
        });

        // Coupon
        modelBuilder.Entity<Coupon>(entity =>
        {
            entity.HasIndex(c => c.Code).IsUnique();
            entity.HasIndex(c => c.IsActive);
            entity.HasIndex(c => c.ValidFrom);
            entity.HasIndex(c => c.ValidUntil);
            entity.HasIndex(c => c.CampaignId);
            entity.Property(c => c.RowVersion).IsRowVersion();
            entity.HasMany(c => c.Restrictions).WithOne(r => r.Coupon)
                .HasForeignKey(r => r.CouponId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(c => c.UserCoupons).WithOne(uc => uc.Coupon)
                .HasForeignKey(uc => uc.CouponId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(c => c.Usages).WithOne(u => u.Coupon)
                .HasForeignKey(u => u.CouponId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_coupons_discount_type",
                    "discount_type IN ('PERCENTAGE','FIXED_AMOUNT','FREE_SHIPPING')");
                t.HasCheckConstraint("ck_coupons_discount_value", "discount_value > 0");
                t.HasCheckConstraint("ck_coupons_min_order_amount", "min_order_amount >= 0");
                t.HasCheckConstraint("ck_coupons_max_usage_count", "max_usage_count > 0");
            });
        });

        // CouponRestriction
        modelBuilder.Entity<CouponRestriction>(entity =>
        {
            entity.HasIndex(cr => cr.CouponId);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_coupon_restrictions_type",
                    "restriction_type IN ('PRODUCT','CATEGORY','USER')");
                t.HasCheckConstraint("ck_coupon_restrictions_percentage_max",
                    "percentage_max IS NULL OR percentage_max > 0");
            });
        });

        // UserCoupon
        modelBuilder.Entity<UserCoupon>(entity =>
        {
            entity.HasIndex(uc => new { uc.CouponId, uc.UserId }).IsUnique();
            entity.HasIndex(uc => uc.UserId);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_user_coupons_status",
                "status IN ('AVAILABLE','USED','EXPIRED')"));
        });

        // CouponUsage
        modelBuilder.Entity<CouponUsage>(entity =>
        {
            entity.HasIndex(cu => new { cu.CouponId, cu.UserId })
                .HasDatabaseName("idx_coupon_usages_coupon_user");
            entity.HasIndex(cu => cu.OrderId)
                .HasDatabaseName("idx_coupon_usages_order");
            entity.HasIndex(cu => cu.UsedAt);
        });

        // Promotion
        modelBuilder.Entity<Promotion>(entity =>
        {
            entity.HasIndex(p => p.IsActive);
            entity.HasIndex(p => p.StartDate);
            entity.HasIndex(p => p.EndDate);
        });

        // OutboxEvent（部分インデックス）
        modelBuilder.Entity<OutboxEvent>(entity =>
        {
            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("idx_outbox_events_pending")
                .HasFilter("status = 'PENDING'");
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_outbox_events_status",
                    "status IN ('PENDING','PROCESSING','PUBLISHED','FAILED','DEAD_LETTER')");
                t.HasCheckConstraint("ck_outbox_events_retry_count", "retry_count >= 0");
            });
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var entry in ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            var updatedAtProp = entry.Properties
                .FirstOrDefault(p => p.Metadata.Name == "UpdatedAt");
            if (updatedAtProp is not null)
                updatedAtProp.CurrentValue = now;

            if (entry.State == EntityState.Added)
            {
                var createdAtProp = entry.Properties
                    .FirstOrDefault(p => p.Metadata.Name == "CreatedAt");
                if (createdAtProp is not null)
                    createdAtProp.CurrentValue = now;
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
```

### 2.4 例外クラス階層（設計書 §G 準拠）

```csharp
// 基底例外
public class CouponException(string message, Exception? innerException = null)
    : Exception(message, innerException);

// HTTP 404
public class CouponNotFoundException(string message) : CouponException(message);

// HTTP 422（ビジネスルール違反）
public class CouponExpiredException(string message) : CouponException(message);
public class CouponUsageLimitExceededException(string message) : CouponException(message);
public class InvalidCouponException(string message) : CouponException(message);

// HTTP 403（不正検知）
public class CouponFraudDetectedException(string message) : CouponException(message);

// HTTP 409（楽観的ロック）
public class ConcurrencyException(string message) : CouponException(message);

// 汎用
public class UnauthorizedException() : Exception("認証が必要です");
public class ForbiddenException() : Exception("アクセスが拒否されました");
```

### 2.5 リクエスト DTO（設計書 §6.4 準拠）

```csharp
public record CreateCampaignRequest(
    [Required, StringLength(200)] string Name,
    string? Description,
    [Required] DateTimeOffset StartDate,
    [Required] DateTimeOffset EndDate,
    [Range(1, int.MaxValue)] int MaxCoupons);

public record CreateCouponRequest(
    string? CampaignId,
    [Required, StringLength(30)] string Code,
    [Required] string DiscountType,
    [Required, Range(0.01, double.MaxValue)] decimal DiscountValue,
    decimal MinOrderAmount = 0,
    decimal? MaxDiscountAmount = null,
    [Range(1, int.MaxValue)] int MaxUsageCount = 1,
    [Range(1, int.MaxValue)] int MaxUsagePerUser = 1,
    string? TargetCategory = null,
    string? TargetProductId = null,
    [Required] DateTimeOffset ValidFrom = default,
    [Required] DateTimeOffset ValidUntil = default);

public record ValidateCouponRequest(
    [Required] string CouponCode,
    [Required] string UserId,
    [Required, Range(0.01, double.MaxValue)] decimal OrderAmount,
    List<OrderItemDto>? Items = null);

public record CalculateDiscountRequest(
    [Required] string CouponCode,
    [Required] string UserId,
    [Required, Range(0.01, double.MaxValue)] decimal OrderAmount,
    List<OrderItemDto>? Items = null);

public record RedeemCouponRequest(
    [Required] string CouponCode,
    [Required] string UserId,
    [Required] string OrderId,
    [Required, Range(0.01, double.MaxValue)] decimal DiscountApplied);

public record OrderItemDto(string ProductId, string Category, decimal Price, int Quantity);
```

### 2.5a 更新リクエスト DTO（設計書 §6.4 / ディレクトリ構成に記載あり — 追加）

```csharp
public record UpdateCouponRequest(
    string? DiscountType,
    [Range(0.01, double.MaxValue)] decimal? DiscountValue,
    decimal? MinOrderAmount,
    decimal? MaxDiscountAmount,
    [Range(1, int.MaxValue)] int? MaxUsageCount,
    [Range(1, int.MaxValue)] int? MaxUsagePerUser,
    string? TargetCategory,
    string? TargetProductId,
    bool? IsActive,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil);

public record UpdateCampaignRequest(
    [StringLength(200)] string? Name,
    string? Description,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    [Range(1, int.MaxValue)] int? MaxCoupons);
```

### 2.5b DateRange Value Object（設計書 §D.8 準拠 — DDD 戦術パターン）

Coupon エンティティの `ValidFrom`/`ValidUntil` を意味的に束ねる Value Object。
EF Core Owned Entity（`OwnsOne`）で DB マッピングする。Phase 1 では既存カラムを維持しつつ並行導入し、DB スキーマ変更は不要。

```csharp
/// <summary>
/// 開始日〜終了日の不変範囲を表す Value Object（spec.md DDD 戦術パターン準拠）。
/// </summary>
public readonly record struct DateRange
{
    public DateTimeOffset Start { get; }
    public DateTimeOffset End { get; }

    public DateRange(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
            throw new ArgumentException("終了日は開始日より後でなければなりません");
        Start = start;
        End = end;
    }

    public bool Contains(DateTimeOffset dateTime) => dateTime >= Start && dateTime <= End;
    public bool IsExpired(DateTimeOffset now) => now > End;
    public bool IsNotStarted(DateTimeOffset now) => now < Start;
}
```

**AppDbContext での Owned Entity 設定**（§2.3 の OnModelCreating に追加）:
```csharp
// OnModelCreating 内に追加
modelBuilder.Entity<Coupon>(entity =>
{
    entity.OwnsOne(c => c.ValidPeriod, vp =>
    {
        vp.Property(d => d.Start).HasColumnName("valid_from").IsRequired();
        vp.Property(d => d.End).HasColumnName("valid_until").IsRequired();
    });
});
```

### 2.6 レスポンス DTO

```csharp
public record CouponResponse(
    string Id, string Code, string DiscountType, decimal DiscountValue,
    decimal MinOrderAmount, decimal? MaxDiscountAmount,
    int MaxUsageCount, int CurrentUsageCount,
    string? TargetCategory, string? TargetProductId,
    bool IsActive, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil);

public record CampaignResponse(
    string Id, string Name, string? Description, string Status,
    DateTimeOffset StartDate, DateTimeOffset EndDate,
    int MaxCoupons, int IssuedCount);

public record ValidationResponse(
    bool IsValid, string? Message, decimal? DiscountAmount);

public record DiscountCalculationResponse(
    decimal OriginalAmount, decimal DiscountAmount, decimal FinalAmount);

public record UserCouponResponse(
    string Id, string CouponCode, string DiscountType, decimal DiscountValue,
    string Status, DateTimeOffset AcquiredAt, DateTimeOffset? UsedAt,
    DateTimeOffset ValidUntil);

public record CouponAnalyticsResponse(
    long TotalCouponsIssued, long TotalCouponsUsed, decimal TotalDiscountAmount,
    double RedemptionRate, Dictionary<string, long> UsageByCategory);
```

### 2.7 設定クラス

```csharp
public record CouponSettings
{
    public FraudDetectionSettings FraudDetection { get; init; } = new();
    public CacheSettings Cache { get; init; } = new();
}

public record FraudDetectionSettings
{
    public int WindowMinutes { get; init; } = 10;
    public int MaxUsagesInWindow { get; init; } = 3;
}

public record CacheSettings
{
    public int CouponTtlMinutes { get; init; } = 10;
    public int UsageTtlMinutes { get; init; } = 5;
}
```

### Phase 2 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] `dotnet ef migrations add Initial --project CouponService` — マイグレーション生成成功
- [ ] 全エンティティ: `[Table("snake_case")]` と `[Column("snake_case")]` が設定されていること
- [ ] 全エンティティ: `created_at` / `updated_at` カラムが含まれていること
- [ ] Coupon: `[Timestamp]` による楽観的ロック（`row_version`）が設定されていること
- [ ] Coupon: `Code` に UNIQUE インデックスが設定されていること
- [ ] UserCoupon: `(coupon_id, user_id)` の複合ユニーク制約が設定されていること
- [ ] 全 CHECK 制約が設計書 §5.2 と一致すること
- [ ] 金額カラム: `DECIMAL(10,2)` / `[Precision(10, 2)]` を使用（`FLOAT` / `DOUBLE` 禁止）
- [ ] コレクションナビゲーション: `= []` で初期化（null 防止）
- [ ] SaveChangesAsync: `TimeProvider` 経由で `CreatedAt` / `UpdatedAt` を自動管理
- [ ] DTO: 全て record 型で定義
- [ ] 例外クラス: 設計書 §G の階層に準拠
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 3: Repository 層実装

### 目的

設計書 §C（Repository インターフェース完全定義）に基づき、6 つの Repository インターフェースとその EF Core 実装を作成する。Aggregate Root 単位で Repository を分離し、`AsNoTracking()` による読み取りクエリ最適化を適用する。

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `CouponService/Repositories/Interfaces/ICouponRepository.cs` | クーポン Repository インターフェース |
| 2 | `CouponService/Repositories/Interfaces/ICampaignRepository.cs` | キャンペーン Repository インターフェース |
| 3 | `CouponService/Repositories/Interfaces/ICouponUsageRepository.cs` | 利用履歴 Repository インターフェース |
| 4 | `CouponService/Repositories/Interfaces/ICouponRestrictionRepository.cs` | 制限 Repository インターフェース |
| 5 | `CouponService/Repositories/Interfaces/IUserCouponRepository.cs` | ユーザークーポン Repository インターフェース |
| 6 | `CouponService/Repositories/Interfaces/IOutboxEventRepository.cs` | Outbox Repository インターフェース |
| 7 | `CouponService/Repositories/CouponRepository.cs` | クーポン Repository 実装 |
| 8 | `CouponService/Repositories/CampaignRepository.cs` | キャンペーン Repository 実装 |
| 9 | `CouponService/Repositories/CouponUsageRepository.cs` | 利用履歴 Repository 実装 |
| 10 | `CouponService/Repositories/CouponRestrictionRepository.cs` | 制限 Repository 実装 |
| 11 | `CouponService/Repositories/UserCouponRepository.cs` | ユーザークーポン Repository 実装 |
| 12 | `CouponService/Repositories/OutboxEventRepository.cs` | Outbox Repository 実装 |

### 3.1 ICouponRepository（設計書 §C.1）

```csharp
public interface ICouponRepository
{
    Task<Coupon?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<Coupon?> FindByCodeAsync(string code, CancellationToken ct = default);
    Task<List<Coupon>> GetActiveCouponsAsync(CancellationToken ct = default);
    Task<List<Coupon>> GetByCampaignIdAsync(string campaignId, CancellationToken ct = default);
    Task AddAsync(Coupon coupon, CancellationToken ct = default);
    Task<int> DeactivateExpiredCouponsAsync(CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

### 3.2 CouponRepository 実装例

```csharp
public class CouponRepository(AppDbContext context) : ICouponRepository
{
    public async Task<Coupon?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.Coupons
            .Include(c => c.Restrictions)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Coupon?> FindByCodeAsync(string code, CancellationToken ct = default)
        => await context.Coupons
            .Include(c => c.Restrictions)
            .FirstOrDefaultAsync(c => c.Code == code, ct);

    public async Task<List<Coupon>> GetActiveCouponsAsync(CancellationToken ct = default)
        => await context.Coupons
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task<List<Coupon>> GetByCampaignIdAsync(string campaignId,
        CancellationToken ct = default)
        => await context.Coupons
            .AsNoTracking()
            .Where(c => c.CampaignId == campaignId)
            .ToListAsync(ct);

    public async Task AddAsync(Coupon coupon, CancellationToken ct = default)
        => await context.Coupons.AddAsync(coupon, ct);

    public async Task<int> DeactivateExpiredCouponsAsync(CancellationToken ct = default)
        => await context.Coupons
            .Where(c => c.IsActive && c.ValidUntil < DateTimeOffset.UtcNow)
            .ExecuteUpdateAsync(s =>
                s.SetProperty(c => c.IsActive, false), ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
```

### 3.3 ICouponUsageRepository（設計書 §C.3 — 不正検知の基盤）

```csharp
public interface ICouponUsageRepository
{
    Task<int> CountByUserAndCouponAsync(string userId, string couponId,
        CancellationToken ct = default);
    Task<int> CountRecentUsagesAsync(string userId, DateTime since,
        CancellationToken ct = default);
    Task<List<CouponUsage>> GetByOrderIdAsync(string orderId,
        CancellationToken ct = default);
    Task<List<CouponUsage>> GetByCouponIdAsync(string couponId,
        CancellationToken ct = default);
    Task AddAsync(CouponUsage usage, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

### 3.4 IUserCouponRepository（設計書 §C.5）

```csharp
public interface IUserCouponRepository
{
    Task<UserCoupon?> FindByUserAndCouponAsync(string userId, string couponId,
        CancellationToken ct = default);
    Task<List<UserCoupon>> FindByUserIdAsync(string userId,
        CancellationToken ct = default);
    Task<List<UserCoupon>> FindAvailableByUserIdAsync(string userId,
        CancellationToken ct = default);
    Task AssignAsync(UserCoupon userCoupon, CancellationToken ct = default);
    Task<int> ExpireByDateAsync(DateTimeOffset expiresBefore,
        CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

### 3.5 ICampaignRepository（設計書 §C.2 — 追加）

```csharp
public interface ICampaignRepository
{
    Task<Campaign?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<List<Campaign>> GetActiveCampaignsAsync(CancellationToken ct = default);
    Task<List<Campaign>> GetByStatusAsync(string status,
        CancellationToken ct = default);
    Task AddAsync(Campaign campaign, CancellationToken ct = default);
    Task<int> CompleteExpiredCampaignsAsync(CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

### 3.6 ICouponRestrictionRepository（設計書 §C.4 — 追加）

```csharp
public interface ICouponRestrictionRepository
{
    Task<List<CouponRestriction>> FindByCouponIdAsync(string couponId,
        CancellationToken ct = default);
    Task AddAsync(CouponRestriction restriction, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<CouponRestriction> restrictions,
        CancellationToken ct = default);
    Task DeleteByCouponIdAsync(string couponId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

### 3.7 IOutboxEventRepository（設計書 §C.6 — 追加）

```csharp
public interface IOutboxEventRepository
{
    Task<List<OutboxEvent>> GetPendingEventsAsync(int batchSize,
        CancellationToken ct = default);
    Task AddAsync(OutboxEvent outboxEvent, CancellationToken ct = default);
    Task MarkAsPublishedAsync(string eventId, CancellationToken ct = default);
    Task MarkAsFailedAsync(string eventId, CancellationToken ct = default);
    Task<int> MoveToDeadLetterAsync(int maxRetryCount,
        CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

### 3.8 ICouponUsageRepository 追加メソッド（設計書 §C.3 — FindByUserAndCouponAsync）

> 設計書 §C.3 で定義されている `FindByUserAndCouponAsync` メソッドを §3.3 の `ICouponUsageRepository` に追加すること:

```csharp
// §3.3 の ICouponUsageRepository に以下のメソッドを追加
Task<CouponUsage?> FindByUserAndCouponAsync(string userId, string couponId,
    CancellationToken ct = default);
```

- [ ] `dotnet build` — 警告なし成功
- [ ] 全 Repository: Aggregate Root 単位で分離されていること
- [ ] 全 Repository: `CancellationToken ct = default` が全 async メソッドに含まれること
- [ ] 読み取りクエリ: `AsNoTracking()` が適用されていること
- [ ] `ICouponUsageRepository.CountRecentUsagesAsync`: 不正検知の基盤クエリが実装されていること
- [ ] `ICouponUsageRepository.FindByUserAndCouponAsync`: 設計書 §C.3 準拠のメソッドが実装されていること
- [ ] `ICouponRepository.DeactivateExpiredCouponsAsync`: `ExecuteUpdateAsync` によるバルク更新
- [ ] `IUserCouponRepository.ExpireByDateAsync`: 期限切れユーザークーポンのバルク更新
- [ ] `ICampaignRepository.CompleteExpiredCampaignsAsync`: 終了日超過キャンペーンのバルク更新（設計書 §C.2）
- [ ] `ICampaignRepository.GetByStatusAsync`: ステータス別取得（設計書 §C.2）
- [ ] `ICouponRestrictionRepository`: 設計書 §C.4 のインターフェース全メソッドが実装されていること
- [ ] `IOutboxEventRepository`: 設計書 §C.6 のインターフェース全メソッド実装（GetPendingEventsAsync, MarkAsPublishedAsync, MarkAsFailedAsync, MoveToDeadLetterAsync）
- [ ] DI 登録: `Program.cs` に全 Repository の Scoped 登録が追加されていること
- [ ] 禁止事項: `FromSqlRaw` での文字列結合なし
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 4: Service 層実装

### 目的

設計書 §D（Service インターフェース）、§7（ルールエンジン）、§8（不正検知）に基づき、6 つの Service を実装する。クーポン適用ロジック、有効期限管理、不正利用検知、分析レポートの全ビジネスロジックをこの層に集約する。

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `CouponService/Services/Interfaces/ICouponService.cs` | クーポンサービスインターフェース（設計書 §D.1） |
| 2 | `CouponService/Services/Interfaces/ICampaignService.cs` | キャンペーンサービスインターフェース（設計書 §D.2） |
| 3 | `CouponService/Services/Interfaces/ICouponRuleEngine.cs` | ルールエンジンインターフェース（設計書 §D.3） |
| 4 | `CouponService/Services/Interfaces/IFraudDetectionService.cs` | 不正検知インターフェース（設計書 §D.4） |
| 5 | `CouponService/Services/Interfaces/ICouponAnalyticsService.cs` | 分析サービスインターフェース（設計書 §D.5） |
| 6 | `CouponService/Services/Interfaces/ICouponCacheService.cs` | キャッシュサービスインターフェース（設計書 §D.6） |
| 7 | `CouponService/Services/CouponService.cs` | クーポンサービス実装 |
| 8 | `CouponService/Services/CampaignService.cs` | キャンペーンサービス実装 |
| 9 | `CouponService/Services/CouponRuleEngine.cs` | ルールエンジン実装（設計書 §7） |
| 10 | `CouponService/Services/FraudDetectionService.cs` | 不正検知実装（設計書 §8） |
| 11 | `CouponService/Services/CouponAnalyticsService.cs` | 分析サービス実装 |
| 12 | `CouponService/Validators/CreateCouponRequestValidator.cs` | クーポン作成バリデーター（設計書 §13.3） |
| 13 | `CouponService/Validators/CreateCampaignRequestValidator.cs` | キャンペーン作成バリデーター |
| 14 | `CouponService/Validators/ValidateCouponRequestValidator.cs` | クーポン検証バリデーター |
| 15 | `CouponService/Services/Interfaces/ICouponCodeGenerator.cs` | クーポンコード生成インターフェース（設計書 §D.7） |
| 16 | `CouponService/Services/CouponCodeGenerator.cs` | クーポンコード生成実装（設計書 §D.7 — 一意性保証 + リトライ） |
| 17 | `CouponService/Services/CouponExpirationService.cs` | BackgroundService: 有効期限切れクーポン自動無効化（設計書 §I.1） |
| 18 | `CouponService/Services/CampaignStatusService.cs` | BackgroundService: キャンペーン終了日チェック（設計書 §I.2） |

### 4.1 CouponRuleEngine（設計書 §7 準拠 — 検証フロー全 7 ステップ）

```csharp
public class CouponRuleEngine(
    TimeProvider timeProvider,
    ILogger<CouponRuleEngine> logger) : ICouponRuleEngine
{
    public ValidationResponse Validate(Coupon coupon, ValidateCouponRequest request,
        int userUsageCount)
    {
        // Step 1: 有効チェック
        if (!coupon.IsActive)
            return new ValidationResponse(false, "クーポンが無効です", null);

        // Step 2: 有効期間チェック
        var now = timeProvider.GetUtcNow();
        if (now < coupon.ValidFrom || now > coupon.ValidUntil)
            return new ValidationResponse(false, "クーポンの有効期間外です", null);

        // Step 3: 全体利用上限チェック
        if (coupon.CurrentUsageCount >= coupon.MaxUsageCount)
            return new ValidationResponse(false, "クーポンの利用上限に達しました", null);

        // Step 4: ユーザー利用回数チェック
        if (userUsageCount >= coupon.MaxUsagePerUser)
            return new ValidationResponse(false, "このクーポンは既に利用済みです", null);

        // Step 5: 最低注文金額チェック
        if (request.OrderAmount < coupon.MinOrderAmount)
            return new ValidationResponse(false,
                $"最低注文金額 {coupon.MinOrderAmount:N0} 円以上から適用可能です", null);

        // Step 6: カテゴリチェック
        if (coupon.TargetCategory is not null && request.Items is { Count: > 0 })
        {
            var hasMatchingItem = request.Items.Any(
                i => i.Category == coupon.TargetCategory);
            if (!hasMatchingItem)
                return new ValidationResponse(false,
                    $"対象カテゴリ '{coupon.TargetCategory}' の商品が含まれていません", null);
        }

        // Step 7: 割引額計算
        var discount = CalculateDiscount(coupon, request.OrderAmount);

        logger.LogInformation(
            "クーポン検証成功: {CouponCode}, 割引額: {Discount}",
            coupon.Code, discount);

        return new ValidationResponse(true, null, discount);
    }

    private static decimal CalculateDiscount(Coupon coupon, decimal orderAmount)
    {
        var discount = coupon.DiscountType switch
        {
            "PERCENTAGE" => orderAmount * coupon.DiscountValue / 100m,
            "FIXED_AMOUNT" => coupon.DiscountValue,
            "FREE_SHIPPING" => 0m,
            _ => 0m
        };

        if (coupon.MaxDiscountAmount.HasValue &&
            discount > coupon.MaxDiscountAmount.Value)
            discount = coupon.MaxDiscountAmount.Value;

        return Math.Min(discount, orderAmount);
    }
}
```

### 4.2 FraudDetectionService（設計書 §8 準拠）

```csharp
public class FraudDetectionService(
    ICouponUsageRepository usageRepository,
    TimeProvider timeProvider,
    IOptions<CouponSettings> settings,
    ILogger<FraudDetectionService> logger) : IFraudDetectionService
{
    public async Task<bool> IsSuspiciousAsync(string userId,
        CancellationToken ct = default)
    {
        var config = settings.Value.FraudDetection;
        var since = timeProvider.GetUtcNow()
            .AddMinutes(-config.WindowMinutes).UtcDateTime;
        var recentUsages = await usageRepository
            .CountRecentUsagesAsync(userId, since, ct);

        if (recentUsages >= config.MaxUsagesInWindow)
        {
            logger.LogWarning(
                "不正利用の疑い: UserId={UserId}, 直近{Window}分の利用回数={Count}",
                userId, config.WindowMinutes, recentUsages);
            return true;
        }
        return false;
    }
}
```

### 4.3 CreateCouponRequestValidator（設計書 §13.3 準拠）

```csharp
public class CreateCouponRequestValidator : AbstractValidator<CreateCouponRequest>
{
    public CreateCouponRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().MaximumLength(30)
            .Matches("^[A-Z0-9-]+$")
            .WithMessage("クーポンコードは大文字英数字とハイフンのみ使用可能です");

        RuleFor(x => x.DiscountType)
            .Must(dt => dt is "PERCENTAGE" or "FIXED_AMOUNT" or "FREE_SHIPPING")
            .WithMessage("割引タイプは PERCENTAGE, FIXED_AMOUNT, FREE_SHIPPING のいずれかです");

        RuleFor(x => x.DiscountValue).GreaterThan(0);

        When(x => x.DiscountType == "PERCENTAGE", () =>
        {
            RuleFor(x => x.DiscountValue).LessThanOrEqualTo(100)
                .WithMessage("割引率は 100% 以下を指定してください");
        });

        RuleFor(x => x.ValidUntil).GreaterThan(x => x.ValidFrom)
            .WithMessage("有効終了日は有効開始日より後の日時を指定してください");
    }
}
```

### 4.4 ICouponCodeGenerator（設計書 §D.7 — クーポンコード一意性保証）

```csharp
/// <summary>
/// クーポンコード生成インターフェース。
/// 一意性保証: UNIQUE 制約 + リトライによる衝突回避。
/// </summary>
public interface ICouponCodeGenerator
{
    Task<string> GenerateAsync(CancellationToken ct = default);
    Task<List<string>> GenerateBatchAsync(int count, CancellationToken ct = default);
}
```

**実装クラス**: `CouponCodeGenerator`

```csharp
public class CouponCodeGenerator(
    ICouponRepository couponRepository,
    ILogger<CouponCodeGenerator> logger) : ICouponCodeGenerator
{
    private const int CodeLength = 12;
    private const int MaxRetries = 10;
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<string> GenerateAsync(CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            var code = GenerateRandomCode();
            var existing = await couponRepository.FindByCodeAsync(code, ct);
            if (existing is null)
                return code;

            logger.LogWarning(
                "クーポンコード衝突: {Code}, リトライ: {Attempt}/{MaxRetries}",
                code, attempt + 1, MaxRetries);
        }
        throw new InvalidOperationException(
            $"クーポンコードの一意生成に {MaxRetries} 回失敗しました");
    }

    public async Task<List<string>> GenerateBatchAsync(int count,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var codes = new List<string>(count);
        for (var i = 0; i < count; i++)
            codes.Add(await GenerateAsync(ct));
        return codes;
    }

    private static string GenerateRandomCode()
    {
        Span<char> code = stackalloc char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
            code[i] = Alphabet[Random.Shared.Next(Alphabet.Length)];
        return $"{code[..4]}-{code[4..8]}-{code[8..12]}";
    }
}
```

### 4.5 ICouponAnalyticsService 全インターフェース（設計書 §D.5 — 4 メソッド）

```csharp
public interface ICouponAnalyticsService
{
    Task<CouponAnalyticsResponse> GetOverallAnalyticsAsync(
        CancellationToken ct = default);
    Task<double> GetRedemptionRateAsync(string couponId,
        CancellationToken ct = default);
    Task<List<CouponResponse>> GetTopCouponsAsync(int count,
        CancellationToken ct = default);
    Task<List<CouponUsage>> GetUsagesByDateRangeAsync(DateTimeOffset from,
        DateTimeOffset to, CancellationToken ct = default);
}
```

### 4.6 ICouponRuleEngine 追加メソッド（設計書 §D.3 — EvaluateRulesAsync）

> §4.1 の `ICouponRuleEngine` インターフェースに以下の非同期メソッドを追加すること:

```csharp
// ICouponRuleEngine に追加
Task<ValidationResponse> EvaluateRulesAsync(Coupon coupon,
    ValidateCouponRequest request, int userUsageCount,
    List<CouponRestriction> restrictions, CancellationToken ct = default);
```

### 4.7 IFraudDetectionService 追加メソッド（設計書 §D.4 — CheckFraudAsync）

> §4.2 の `IFraudDetectionService` インターフェースに以下のメソッドを追加すること:

```csharp
// IFraudDetectionService に追加
Task<FraudCheckResult> CheckFraudAsync(string userId, string couponCode,
    string? ipAddress, CancellationToken ct = default);
```

### 4.8 ICouponCacheService インターフェース（設計書 §D.6 — 全メソッド）

```csharp
public interface ICouponCacheService
{
    Task<Coupon?> GetCouponAsync(string code, CancellationToken ct = default);
    Task SetCouponAsync(string code, Coupon coupon,
        CancellationToken ct = default);
    Task InvalidateAsync(string code, CancellationToken ct = default);
    Task<int?> GetUserUsageCountAsync(string couponId, string userId,
        CancellationToken ct = default);
    Task SetUserUsageCountAsync(string couponId, string userId, int count,
        CancellationToken ct = default);
}
```

### 4.9 CouponExpirationService BackgroundService（設計書 §I.1 — 日次有効期限チェック）

```csharp
public class CouponExpirationService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<CouponExpirationService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CouponExpirationService 開始");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var couponRepository = scope.ServiceProvider
                    .GetRequiredService<ICouponRepository>();
                var userCouponRepository = scope.ServiceProvider
                    .GetRequiredService<IUserCouponRepository>();

                var deactivatedCount = await couponRepository
                    .DeactivateExpiredCouponsAsync(stoppingToken);
                if (deactivatedCount > 0)
                    logger.LogInformation(
                        "有効期限切れクーポンを {Count} 件無効化しました",
                        deactivatedCount);

                var now = timeProvider.GetUtcNow();
                var expiredUserCouponCount = await userCouponRepository
                    .ExpireByDateAsync(now, stoppingToken);
                if (expiredUserCouponCount > 0)
                    logger.LogInformation(
                        "ユーザークーポンを {Count} 件期限切れに更新しました",
                        expiredUserCouponCount);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "CouponExpirationService でエラーが発生しました: {Message}",
                    ex.Message);
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }
}
```

### 4.10 CampaignStatusService BackgroundService（設計書 §I.2 — キャンペーン終了日チェック）

```csharp
public class CampaignStatusService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<CampaignStatusService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CampaignStatusService 開始");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var campaignRepository = scope.ServiceProvider
                    .GetRequiredService<ICampaignRepository>();

                var completedCount = await campaignRepository
                    .CompleteExpiredCampaignsAsync(stoppingToken);
                if (completedCount > 0)
                    logger.LogInformation(
                        "終了日を過ぎたキャンペーンを {Count} 件完了に更新しました",
                        completedCount);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "CampaignStatusService でエラーが発生しました: {Message}",
                    ex.Message);
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }
}
```

- [ ] `dotnet build` — 警告なし成功
- [ ] CouponRuleEngine: 設計書 §7 のフローチャートの全 7 ステップが実装されていること
- [ ] CouponRuleEngine: `TimeProvider` を DI でインジェクション（`DateTime.UtcNow` 直接使用禁止）
- [ ] FraudDetectionService: `IOptions<CouponSettings>` で不正検知パラメータを外部化
- [ ] CouponService: `ValidateAndApplyAsync` / `ReleaseCouponAsync`（Saga 用）が実装されていること
- [ ] CouponService: 楽観的ロック競合時に `ConcurrencyException` をスローすること
- [ ] 全 Service: primary constructor によるコンストラクタインジェクション
- [ ] 全 Service: `CancellationToken ct = default` が全 async メソッドに含まれること
- [ ] 全 Service: `ILogger<T>` + メッセージテンプレート形式のログ出力
- [ ] FluentValidation: `CreateCouponRequestValidator` — コードパターン・割引タイプ・期間整合性を検証
- [ ] DI 登録: `Program.cs` に全 Service の Scoped 登録が追加されていること
- [ ] ICouponCodeGenerator: クーポンコード生成サービスが実装されていること（設計書 §D.7 — 一意性保証 + リトライ + バッチ生成）
- [ ] ICouponAnalyticsService: 設計書 §D.5 の 4 メソッド全てが実装されていること（GetOverallAnalyticsAsync, GetRedemptionRateAsync, GetTopCouponsAsync, GetUsagesByDateRangeAsync）
- [ ] ICouponRuleEngine.EvaluateRulesAsync: 設計書 §D.3 の非同期メソッド（CouponRestriction 対応）が実装されていること
- [ ] IFraudDetectionService.CheckFraudAsync: 設計書 §D.4 の詳細不正チェックメソッドが実装されていること
- [ ] ICouponCacheService: 設計書 §D.6 の 5 メソッド全てが実装されていること
- [ ] CouponExpirationService: BackgroundService として有効期限切れクーポンの自動無効化が実装されていること（設計書 §I.1）
- [ ] CampaignStatusService: BackgroundService としてキャンペーン終了日チェックが実装されていること（設計書 §I.2）
- [ ] BackgroundService: `IServiceScopeFactory` で Scoped サービスを取得、`stoppingToken` を全下位呼び出しに伝搬
- [ ] DI 登録: `Program.cs` に `AddHostedService<CouponExpirationService>()` と `AddHostedService<CampaignStatusService>()` が追加されていること
- [ ] 禁止事項: `Console.WriteLine` なし、例外の握りつぶしなし
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること


---

## Phase 5: Endpoints 実装

### 目的

設計書 §6（API 設計）および §E（Endpoint 実装パターン）に基づき、一般ユーザー向け API（4 エンドポイント）、管理者向け API（13 エンドポイント）、内部 API（3 エンドポイント）、gRPC サービス（2 RPC）を実装する。全エンドポイントで FluentValidation によるバリデーション、IDOR 防止、適切な認可設定を行う。

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `CouponService/Endpoints/CouponEndpoints.cs` | 一般ユーザー向けエンドポイント（設計書 §E.1） |
| 2 | `CouponService/Endpoints/AdminCouponEndpoints.cs` | 管理者向けクーポンエンドポイント（設計書 §E.2） |
| 3 | `CouponService/Endpoints/CampaignEndpoints.cs` | 管理者向けキャンペーンエンドポイント（設計書 §E.3） |
| 4 | `CouponService/Endpoints/InternalCouponEndpoints.cs` | 内部 API エンドポイント（設計書 §E.4） |
| 5 | `CouponService/GrpcServices/CouponGrpcService.cs` | gRPC サービス実装（設計書 §10） |
| 6 | `CouponService/Program.cs` | 更新: エンドポイントマッピング + gRPC 登録 |

### 5.1 エンドポイント一覧（全 20 エンドポイント + 2 gRPC RPC）

#### 一般ユーザー向け API（設計書 §6.1）

| # | メソッド | パス | 認可 | 説明 |
|---|--------|------|------|------|
| 1 | GET | `/api/v1/coupons/available` | UserOrAdmin | 利用可能なクーポン一覧 |
| 2 | POST | `/api/v1/coupons/{code}/acquire` | 認証必須 | クーポン取得 |
| 3 | GET | `/api/v1/coupons/mine` | 認証必須 | 自分のクーポン一覧 |
| 4 | POST | `/api/v1/coupons/validate` | 認証必須 | クーポン適用可否チェック |

#### 管理者向けキャンペーン API（設計書 §6.2）

| # | メソッド | パス | 認可 | 説明 |
|---|--------|------|------|------|
| 5 | GET | `/api/v1/admin/campaigns` | AdminOnly | キャンペーン一覧 |
| 6 | POST | `/api/v1/admin/campaigns` | AdminOnly | キャンペーン作成 |
| 7 | GET | `/api/v1/admin/campaigns/{id}` | AdminOnly | キャンペーン詳細 |
| 8 | PUT | `/api/v1/admin/campaigns/{id}` | AdminOnly | キャンペーン更新 |
| 9 | POST | `/api/v1/admin/campaigns/{id}/activate` | AdminOnly | キャンペーン有効化 |
| 10 | POST | `/api/v1/admin/campaigns/{id}/pause` | AdminOnly | キャンペーン一時停止 |

#### 管理者向けクーポン API（設計書 §6.2）

| # | メソッド | パス | 認可 | 説明 |
|---|--------|------|------|------|
| 11 | GET | `/api/v1/admin/coupons` | AdminOnly | クーポン一覧 |
| 12 | POST | `/api/v1/admin/coupons` | AdminOnly | クーポン作成 |
| 13 | GET | `/api/v1/admin/coupons/{id}` | AdminOnly | クーポン詳細 |
| 14 | PUT | `/api/v1/admin/coupons/{id}` | AdminOnly | クーポン更新 |
| 15 | DELETE | `/api/v1/admin/coupons/{id}` | AdminOnly | クーポン無効化（論理削除） |
| 16 | GET | `/api/v1/admin/coupons/{id}/usages` | AdminOnly | クーポン利用履歴 |
| 17 | GET | `/api/v1/admin/coupons/analytics` | AdminOnly | クーポン分析レポート |

#### 内部 API（設計書 §6.3 — PaymentCartService 向け）

| # | メソッド | パス | 認可 | 説明 |
|---|--------|------|------|------|
| 18 | POST | `/api/v1/internal/coupons/calculate` | 内部認証 | 注文に対する割引額計算 |
| 19 | POST | `/api/v1/internal/coupons/redeem` | 内部認証 | クーポン利用確定 |
| 20 | POST | `/api/v1/internal/coupons/release` | 内部認証 | クーポン利用取消 |

#### gRPC RPC（設計書 §10 — Saga ステップ 3）

| # | RPC 名 | Deadline | 説明 |
|---|--------|---------|------|
| 21 | `ApplyCoupon` | 300ms | クーポン検証・適用 |
| 22 | `ReleaseCoupon` | 500ms | クーポン利用取消（補償トランザクション） |

### 5.2 CouponEndpoints（一般ユーザー向け — 設計書 §E.1）

```csharp
public static class CouponEndpoints
{
    public static void MapCouponEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/coupons")
            .WithTags("Coupons")
            .WithOpenApi();

        group.MapGet("/available", GetAvailableCoupons)
            .WithName("GetAvailableCoupons")
            .RequireAuthorization("UserOrAdmin");

        group.MapGet("/mine", GetMyCoupons)
            .WithName("GetMyCoupons")
            .RequireAuthorization();

        group.MapPost("/{code}/acquire", AcquireCoupon)
            .WithName("AcquireCoupon")
            .RequireAuthorization();

        group.MapPost("/validate", ValidateCoupon)
            .WithName("ValidateCoupon")
            .RequireAuthorization();
    }

    private static async Task<IResult> GetAvailableCoupons(
        ICouponService couponService, CancellationToken ct)
        => Results.Ok(await couponService.GetActiveCouponsAsync(ct));

    private static async Task<IResult> GetMyCoupons(
        ClaimsPrincipal user, ICouponService couponService, CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        return Results.Ok(await couponService.GetUserCouponsAsync(userId, ct));
    }

    private static async Task<IResult> AcquireCoupon(
        string code, ClaimsPrincipal user,
        ICouponService couponService, CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        return Results.Ok(await couponService.AcquireCouponAsync(code, userId, ct));
    }

    private static async Task<IResult> ValidateCoupon(
        [FromBody] ValidateCouponRequest request,
        IValidator<ValidateCouponRequest> validator,
        ClaimsPrincipal user,
        ICouponService couponService, CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        var safeRequest = request with { UserId = userId };
        return Results.Ok(await couponService.ValidateCouponAsync(safeRequest, ct));
    }
}
```

### 5.3 CouponGrpcService（設計書 §10.2 — Saga ステップ 3）

```csharp
public class CouponGrpcService(
    ICouponService couponService,
    ILogger<CouponGrpcService> logger)
    : SkiShop.Contracts.Coupon.V1.CouponGrpcService.CouponGrpcServiceBase
{
    public override async Task<ApplyCouponResponse> ApplyCoupon(
        ApplyCouponRequest request, ServerCallContext context)
    {
        var result = await couponService.ValidateAndApplyAsync(
            request.CouponCode, request.UserId, request.OrderId,
            decimal.Parse(request.OrderAmount),
            context.CancellationToken);

        return new ApplyCouponResponse
        {
            Success = result.IsValid,
            Message = result.Message ?? string.Empty,
            DiscountAmount = result.DiscountAmount?.ToString("F2") ?? "0",
            CouponId = string.Empty
        };
    }

    public override async Task<ReleaseCouponResponse> ReleaseCoupon(
        ReleaseCouponRequest request, ServerCallContext context)
    {
        await couponService.ReleaseCouponAsync(
            request.CouponCode, request.UserId, request.OrderId,
            context.CancellationToken);
        return new ReleaseCouponResponse { Success = true };
    }
}
```

### 5.4 Program.cs エンドポイントマッピング

```csharp
// エンドポイントマッピング
app.MapCouponEndpoints();
app.MapAdminCouponEndpoints();
app.MapCampaignEndpoints();
app.MapInternalCouponEndpoints();
app.MapGrpcService<CouponGrpcService>();
```

### Phase 5 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] 一般ユーザー API: 全 4 エンドポイントが実装されていること
- [ ] 管理者 API: キャンペーン 6 + クーポン 7 = 全 13 エンドポイントが実装されていること
- [ ] 内部 API: 全 3 エンドポイントが実装されていること
- [ ] gRPC: `ApplyCoupon` / `ReleaseCoupon` の 2 RPC が実装されていること
- [ ] IDOR 防止: `AcquireCoupon` / `ValidateCoupon` / `GetMyCoupons` で `ClaimsPrincipal` からユーザー ID を取得
- [ ] 認可: 一般ユーザー API — `RequireAuthorization()` / `RequireAuthorization("UserOrAdmin")`
- [ ] 認可: 管理者 API — `RequireAuthorization("AdminOnly")`
- [ ] バリデーション: `IValidator<T>` によるリクエスト検証が全 POST/PUT エンドポイントに適用
- [ ] レスポンス: POST → `Results.Created()`、DELETE → `Results.NoContent()`、GET → `Results.Ok()` / `Results.NotFound()`
- [ ] Endpoints にビジネスロジックが含まれていないこと（Service 層に委譲）
- [ ] `WithOpenApi()` が全グループに設定されていること
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 6: Kafka イベント連携

### 目的

設計書 §9（イベント設計）および §11（Outbox パターン）に基づき、イベント発行（Outbox パターン）とイベント購読（Consumer）を実装する。直接 Kafka に Produce することは禁止し、`outbox_events` テーブル経由で発行する。

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `CouponService/BackgroundServices/OutboxPublisher.cs` | Outbox イベント発行（設計書 §11.1） |
| 2 | `CouponService/Consumers/OrderEventConsumer.cs` | 注文キャンセル・ユーザー削除イベント購読（設計書 §9.2） |
| 3 | `CouponService/Program.cs` | 更新: Kafka Producer / Consumer DI + BackgroundService 登録 |

### 6.1 発行するイベント（設計書 §9.1）

| イベントタイプ | Kafka トピック | トリガー | ペイロード |
|-------------|--------------|---------|----------|
| `CouponApplied` | `coupon.applied` | クーポン利用確定（`RedeemCouponAsync`） | `{ couponId, couponCode, userId, orderId, discountApplied, occurredAt }` |
| `CouponReleased` | `coupon.released` | クーポン解放（Saga 補償 / 注文キャンセル） | `{ couponId, couponCode, userId, orderId, occurredAt }` |
| `CouponExpired` | `coupon.expired` | クーポン期限切れ（`CouponExpirationService`） | `{ couponId, code, occurredAt }` |

### 6.2 購読するイベント（設計書 §9.2）

| Kafka トピック | 発行元 | 処理内容 |
|--------------|-------|---------|
| `order.cancelled` | SalesManagementService | クーポン利用取消・`current_usage_count` のデクリメント・ユーザークーポン AVAILABLE 復帰 |
| `user.deleted` | UserManagementService | ユーザー関連クーポンデータの仮名化（GDPR 対応） |

### 6.3 イベントペイロード定義

```csharp
public record CouponAppliedEvent(
    string CouponId, string CouponCode, string UserId, string OrderId,
    decimal DiscountApplied, DateTimeOffset OccurredAt);

public record CouponReleasedEvent(
    string CouponId, string CouponCode, string UserId, string OrderId,
    DateTimeOffset OccurredAt);

public record CouponExpiredEvent(
    string CouponId, string Code, DateTimeOffset OccurredAt);
```

### 6.4 OutboxPublisher（設計書 §11.1 — 動的バックオフ + Advisory Lock）

> **設計書 §11.1 の要件**: 複数インスタンスでの二重発行を防止するため、PostgreSQL Advisory Lock（`pg_try_advisory_lock`）によるリーダー選出を行い、`PENDING → PROCESSING → PUBLISHED` のステータス遷移と `SELECT ... FOR UPDATE` を使用する。

```csharp
public class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    TimeProvider timeProvider,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private static readonly TimeSpan MinPollingInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MaxPollingInterval = TimeSpan.FromSeconds(5);
    private const long AdvisoryLockId = 100001; // CouponService 用固定 Lock ID
    private TimeSpan _currentInterval = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Advisory Lock でリーダー選出（複数インスタンスの二重発行防止）
            var acquired = await context.Database
                .ExecuteSqlRawAsync(
                    $"SELECT pg_try_advisory_lock({AdvisoryLockId})",
                    stoppingToken) > 0;
            if (!acquired)
            {
                await Task.Delay(MaxPollingInterval, stoppingToken);
                continue;
            }

            try
            {
                // PENDING イベントを PROCESSING に遷移（SELECT ... FOR UPDATE 相当）
                var pendingEvents = await context.OutboxEvents
                    .Where(e => e.Status == "PENDING")
                    .OrderBy(e => e.CreatedAt)
                    .Take(100)
                    .ToListAsync(stoppingToken);

                foreach (var evt in pendingEvents)
                    evt.Status = "PROCESSING";
                await context.SaveChangesAsync(stoppingToken);

                foreach (var evt in pendingEvents)
                {
                    try
                    {
                        await producer.ProduceAsync(evt.EventType,
                            new Message<string, string>
                            {
                                Key = evt.AggregateId,
                                Value = evt.Payload
                            }, stoppingToken);
                        evt.Status = "PUBLISHED";
                        evt.PublishedAt = timeProvider.GetUtcNow();
                    }
                    catch (Exception ex)
                    {
                        evt.RetryCount++;
                        evt.LastError = ex.Message[..Math.Min(ex.Message.Length, 2000)];
                        evt.Status = evt.RetryCount >= evt.MaxRetries
                            ? "FAILED" : "PENDING";
                        logger.LogError(ex,
                            "Outbox publish failed: {EventId}, retry: {RetryCount}",
                            evt.Id, evt.RetryCount);
                    }
                }
                await context.SaveChangesAsync(stoppingToken);

                _currentInterval = pendingEvents.Count > 0
                    ? MinPollingInterval
                    : TimeSpan.FromTicks(Math.Min(
                        _currentInterval.Ticks * 2, MaxPollingInterval.Ticks));
            }
            finally
            {
                // Advisory Lock 解放
                await context.Database
                    .ExecuteSqlRawAsync(
                        $"SELECT pg_advisory_unlock({AdvisoryLockId})",
                        stoppingToken);
            }

            await Task.Delay(_currentInterval, stoppingToken);
        }
    }
}
```

### 6.5 OrderEventConsumer（設計書 §9.2）

```csharp
public class OrderEventConsumer(
    IConsumer<string, string> consumer,
    IServiceScopeFactory scopeFactory,
    ILogger<OrderEventConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        consumer.Subscribe(["order.cancelled", "user.deleted"]);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                using var scope = scopeFactory.CreateScope();

                switch (result.Topic)
                {
                    case "order.cancelled":
                        var couponService = scope.ServiceProvider
                            .GetRequiredService<ICouponService>();
                        var cancelEvent = JsonSerializer
                            .Deserialize<OrderCancelledEvent>(result.Message.Value);
                        if (cancelEvent is not null)
                            await couponService.ReleaseCouponAsync(
                                cancelEvent.CouponCode, cancelEvent.UserId,
                                cancelEvent.OrderId, stoppingToken);
                        break;

                    case "user.deleted":
                        logger.LogInformation(
                            "ユーザー削除イベント受信: 仮名化処理を実行");
                        break;
                }
                consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume エラー: {Topic}",
                    ex.ConsumerRecord?.Topic);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "イベント処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}

public record OrderCancelledEvent(
    string OrderId, string? CouponCode, string UserId);
```

### Phase 6 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] OutboxPublisher: `BackgroundService` として実装
- [ ] OutboxPublisher: `IServiceScopeFactory` で Scoped サービスを取得
- [ ] OutboxPublisher: 動的バックオフ（100ms〜5s）— 固定間隔 1 秒は禁止
- [ ] OutboxPublisher: `stoppingToken` を全下位呼び出しに伝搬
- [ ] OutboxPublisher: 発行失敗時に `RetryCount` をインクリメント、`MaxRetries` 超過で `FAILED`
- [ ] OutboxPublisher: PostgreSQL Advisory Lock（`pg_try_advisory_lock`）によるリーダー選出が実装されていること（設計書 §11.1）
- [ ] OutboxPublisher: `PENDING → PROCESSING → PUBLISHED` のステータス遷移が実装されていること（設計書 §11.1）
- [ ] イベント発行: Service 層で DB トランザクション内に `outbox_events` を書き込み（直接 Kafka Produce 禁止）
- [ ] イベントペイロード: `CouponAppliedEvent`, `CouponReleasedEvent`, `CouponExpiredEvent` の 3 種類が全て定義されていること（設計書 §9.1 / §9.3）
- [ ] OrderEventConsumer: `order.cancelled` — クーポン利用取消が実装されていること
- [ ] OrderEventConsumer: `user.deleted` — 仮名化処理の枠組みが実装されていること
- [ ] OrderEventConsumer: `ConsumeException` と一般例外を区別して処理
- [ ] OrderEventConsumer: バックオフ（5 秒）による障害時リトライ
- [ ] Kafka Producer / Consumer: `Program.cs` に DI 登録が追加されていること
- [ ] BackgroundService 登録: `AddHostedService<OutboxPublisher>()` / `AddHostedService<OrderEventConsumer>()`
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 7: Redis キャッシュ連携

### 目的

設計書 §12（キャッシュ戦略）に基づき、クーポンコードの高速ルックアップとユーザー利用回数の高速チェックを Redis キャッシュで実装する。Redis 障害時は DB フォールバックで動作を継続する。

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `CouponService/Services/CouponCacheService.cs` | Redis キャッシュ実装（設計書 §12） |
| 2 | `CouponService/Program.cs` | 更新: Redis DI 登録 |

### 7.1 キャッシュキー設計（設計書 §12.1）

| キー | 値 | TTL | 用途 |
|-----|-----|-----|------|
| `coupon:{code}` | Coupon エンティティ JSON | 10 分 | クーポンコード → 詳細の高速ルックアップ |
| `coupon:usage:{couponId}:{userId}` | 利用回数 (int) | 5 分 | ユーザー利用回数の高速チェック |

### 7.2 キャッシュ無効化トリガー（設計書 §12.2）

| トリガー | 無効化キー |
|---------|----------|
| クーポン利用確定 | `coupon:{code}` + `coupon:usage:{couponId}:{userId}` |
| クーポン更新 | `coupon:{code}` |
| クーポン無効化 | `coupon:{code}` |

### 7.3 CouponCacheService（設計書 §12 準拠 + Redis 障害フォールバック）

```csharp
public class CouponCacheService(
    IConnectionMultiplexer redis,
    IOptions<CouponSettings> settings,
    ILogger<CouponCacheService> logger) : ICouponCacheService
{
    private readonly IDatabase _db = redis.GetDatabase();
    private readonly CacheSettings _cacheSettings = settings.Value.Cache;

    public async Task<Coupon?> GetCouponAsync(string code, CancellationToken ct = default)
    {
        try
        {
            var cached = await _db.StringGetAsync($"coupon:{code}");
            return cached.HasValue
                ? JsonSerializer.Deserialize<Coupon>(cached!)
                : null;
        }
        catch (RedisConnectionException ex)
        {
            logger.LogWarning(ex, "Redis 接続エラー — DB フォールバック: coupon:{Code}", code);
            return null;
        }
    }

    public async Task SetCouponAsync(string code, Coupon coupon,
        CancellationToken ct = default)
    {
        try
        {
            await _db.StringSetAsync($"coupon:{code}",
                JsonSerializer.Serialize(coupon),
                TimeSpan.FromMinutes(_cacheSettings.CouponTtlMinutes));
        }
        catch (RedisConnectionException ex)
        {
            logger.LogWarning(ex, "Redis 書き込みエラー: coupon:{Code}", code);
        }
    }

    public async Task InvalidateAsync(string code, CancellationToken ct = default)
    {
        try
        {
            await _db.KeyDeleteAsync($"coupon:{code}");
            logger.LogInformation("キャッシュ無効化: coupon:{Code}", code);
        }
        catch (RedisConnectionException ex)
        {
            logger.LogWarning(ex, "Redis 削除エラー: coupon:{Code}", code);
        }
    }

    public async Task<int?> GetUserUsageCountAsync(string couponId, string userId,
        CancellationToken ct = default)
    {
        try
        {
            var cached = await _db.StringGetAsync($"coupon:usage:{couponId}:{userId}");
            return cached.HasValue ? (int?)int.Parse(cached!) : null;
        }
        catch (RedisConnectionException ex)
        {
            logger.LogWarning(ex, "Redis 接続エラー — DB フォールバック: usage");
            return null;
        }
    }

    public async Task SetUserUsageCountAsync(string couponId, string userId, int count,
        CancellationToken ct = default)
    {
        try
        {
            await _db.StringSetAsync($"coupon:usage:{couponId}:{userId}",
                count.ToString(),
                TimeSpan.FromMinutes(_cacheSettings.UsageTtlMinutes));
        }
        catch (RedisConnectionException ex)
        {
            logger.LogWarning(ex, "Redis 書き込みエラー: usage");
        }
    }
}
```

### Phase 7 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] CouponCacheService: `GetCouponAsync` — `coupon:{code}` キーでの読み取り
- [ ] CouponCacheService: `SetCouponAsync` — TTL 10 分で書き込み
- [ ] CouponCacheService: `InvalidateAsync` — キャッシュ削除
- [ ] CouponCacheService: `GetUserUsageCountAsync` / `SetUserUsageCountAsync` — ユーザー利用回数キャッシュ
- [ ] Redis 障害フォールバック: `RedisConnectionException` をキャッチし、`null` を返却（DB フォールバック）
- [ ] Redis 障害: 例外をログ出力（`LogWarning`）し、処理を継続
- [ ] TTL: `IOptions<CouponSettings>` から外部化された値を使用
- [ ] キャッシュ無効化: クーポン利用・更新・無効化時に Service 層から呼び出されること
- [ ] DI 登録: `Program.cs` に `IConnectionMultiplexer` と `ICouponCacheService` が登録されていること
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 8: 認証・認可・セキュリティ

### 目的

設計書 §13（セキュリティ設計）および AGENTS.md §5（セキュリティ規約）に基づき、JWT Bearer 認証、ロールベース認可、セキュリティヘッダー、CORS、グローバル例外ハンドラー、レート制限を構成する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `CouponService/Infrastructure/Middleware/CorrelationIdMiddleware.cs` | 作成 | Correlation ID ミドルウェア |
| 2 | `CouponService/Infrastructure/Middleware/CorrelationIdMiddlewareExtensions.cs` | 作成 | `UseCorrelationId()` 拡張メソッド |
| 3 | `CouponService/Program.cs` | 更新 | 認証・認可・ミドルウェアパイプライン統合 |

### 8.1 認証・認可設定（設計書 §13.1）

| ポリシー名 | 適用対象 | 条件 |
|-----------|---------|------|
| `AdminOnly` | 管理者専用エンドポイント（`/api/v1/admin/**`） | `RequireRole("Admin")` |
| `UserOrAdmin` | 一般ユーザー + 管理者（`/api/v1/coupons/available`） | `RequireRole("User", "Admin")` |
| `FallbackPolicy` | 全エンドポイント（デフォルト） | `RequireAuthenticatedUser()` |

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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("UserOrAdmin", p => p.RequireRole("User", "Admin"));
    // 内部サービス間通信用ポリシー（設計書 §13.1 — gRPC / Internal API で使用）
    options.AddPolicy("InternalServiceOnly", p =>
        p.AddRequirements(new InternalServiceRequirement()));
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// InternalServiceOnly 用の認可ハンドラー登録
builder.Services.AddSingleton<IAuthorizationHandler, InternalServiceHandler>();
```

### 8.1a InternalServiceOnly 認可ハンドラー（設計書 §13.1 — サービス間認証）

```csharp
public class InternalServiceRequirement : IAuthorizationRequirement { }

public class InternalServiceHandler : AuthorizationHandler<InternalServiceRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        InternalServiceRequirement requirement)
    {
        // サービス間通信: sub クレームが "internal-service" であることを検証
        if (context.User.HasClaim(c => c.Type == "sub" && c.Value == "internal-service"))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
```

> **補足**: 内部 API エンドポイント（`/api/v1/internal/coupons/*`）と gRPC サービス（`CouponGrpcService`）は `.RequireAuthorization("InternalServiceOnly")` を使用する。

### 8.2 セキュリティヘッダー

| ヘッダー | 値 |
|---------|---|
| `X-Content-Type-Options` | `nosniff` |
| `X-Frame-Options` | `DENY` |
| `Content-Security-Policy` | `default-src 'self'` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` |

### 8.3 グローバル例外ハンドラー（設計書 §F / §G 準拠）

```csharp
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var error = feature?.Error;

        if (error is not (CouponNotFoundException or CouponExpiredException
            or CouponUsageLimitExceededException or InvalidCouponException
            or CouponFraudDetectedException or UnauthorizedException
            or ForbiddenException))
            logger.LogError(error, "Unhandled exception: {Message}", error?.Message);
        else
            logger.LogWarning("Handled exception: {ExceptionType} - {Message}",
                error.GetType().Name, error.Message);

        var problem = error switch
        {
            CouponNotFoundException e              => TypedResults.Problem(e.Message, statusCode: 404),
            CouponExpiredException e                => TypedResults.Problem(e.Message, statusCode: 422),
            CouponUsageLimitExceededException e     => TypedResults.Problem(e.Message, statusCode: 422),
            InvalidCouponException e                => TypedResults.Problem(e.Message, statusCode: 422),
            CouponFraudDetectedException e          => TypedResults.Problem(e.Message, statusCode: 403),
            UnauthorizedException                   => TypedResults.Problem(statusCode: 401),
            ForbiddenException                      => TypedResults.Problem(statusCode: 403),
            ConcurrencyException e                  => TypedResults.Problem(e.Message, statusCode: 409),
            _                                       => TypedResults.Problem("内部エラーが発生しました", statusCode: 500)
        };
        await problem.ExecuteAsync(context);
    });
});
```

### 8.4 エラーコード一覧（設計書 §14 準拠）

| コード | HTTP ステータス | 説明 |
|--------|-------------|------|
| CPN-4001 | 404 | クーポンが見つからない |
| CPN-4002 | 422 | クーポンの有効期間外 |
| CPN-4003 | 422 | クーポンの利用上限到達 |
| CPN-4004 | 422 | ユーザーの利用上限到達 |
| CPN-4005 | 422 | 最低注文金額未満 |
| CPN-4006 | 422 | 対象カテゴリ外 |
| CPN-4007 | 422 | 不正利用の疑い |
| CPN-4008 | 409 | クーポン取得済み（重複） |
| CPN-4009 | 422 | キャンペーン発行上限到達 |
| CPN-4010 | 422 | クーポンが無効 |
| CPN-5001 | 500 | データベースエラー |
| CPN-5002 | 503 | 外部サービス通信エラー |

### 8.5 ミドルウェアパイプライン順序（AGENTS.md §11.3 厳守）

```
┌──────────────────────────────────────────────────┐
│ 1. UseExceptionHandler()                          │ ← 最外層: 全例外をキャッチ
│ 2. UseHsts() + UseHttpsRedirection()              │ ← セキュリティ
│ 3. セキュリティヘッダーミドルウェア                    │ ← X-Content-Type-Options 等
│ 4. UseCorrelationId()                             │ ← Correlation ID
│ 5. UseSerilogRequestLogging()                     │ ← リクエストログ
│ 6. UseCors()                                      │ ← CORS（認証より前）
│ 7. UseAuthentication()                            │ ← 認証
│    UseAuthorization()                             │ ← 認可（認証の直後）
│ 8. UseRateLimiter()                               │ ← レート制限（認証後）
│ 9. エンドポイントマッピング                          │ ← MapXxxEndpoints + MapGrpcService
│    MapHealthChecks("/health")                     │ ← Liveness
│    MapHealthChecks("/health/ready")               │ ← Readiness
└──────────────────────────────────────────────────┘
```

### Phase 8 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] JWT 認証: `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey` すべて `true`
- [ ] JWT 認証: `ClockSkew = TimeSpan.FromMinutes(5)`
- [ ] 認可: `FallbackPolicy` に `RequireAuthenticatedUser()` が設定されていること
- [ ] 認可: `AdminOnly`, `UserOrAdmin` ポリシーが定義されていること
- [ ] 認可: `InternalServiceOnly` ポリシーが定義されていること（設計書 §13.1 — サービス間認証）
- [ ] 認可: `InternalServiceHandler` + `InternalServiceRequirement` が実装・DI 登録されていること（設計書 §13.1）
- [ ] 認可: 内部 API エンドポイント（`/api/v1/internal/coupons/*`）に `.RequireAuthorization("InternalServiceOnly")` が設定されていること
- [ ] セキュリティヘッダー: 全 5 ヘッダーが設定されていること
- [ ] グローバル例外ハンドラー: RFC 9457 Problem Details 形式
- [ ] グローバル例外ハンドラー: スタックトレースがクライアントに返却されないこと
- [ ] グローバル例外ハンドラー: 設計書 §G の全例外型がマッピングされていること
- [ ] CorrelationIdMiddleware: Serilog `LogContext.PushProperty` に付与
- [ ] ミドルウェアパイプライン: §8.5 の順序に完全一致
- [ ] `UseAuthentication()` が `UseAuthorization()` の直前
- [ ] `UseExceptionHandler()` がパイプライン最上位
- [ ] `appsettings.json`: `DetailedErrors: false`
- [ ] CORS: `AllowAnyOrigin()` 不使用
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること


---

## Phase 9: テスト

### 目的

AGENTS.md §9（テスト規約）およびプロジェクト共通ルール「テストカバレッジ目標」に基づき、分岐カバレッジ 80% 以上を達成する。テスト対象は Service 層（CouponRuleEngine / FraudDetectionService / CouponService）、Repository 層（Testcontainers）、Endpoints 層（WebApplicationFactory）を網羅する。

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `CouponService.Tests/CouponService.Tests.csproj` | テストプロジェクト |
| 2 | `CouponService.Tests/Unit/Services/CouponRuleEngineTests.cs` | ルールエンジンの全 7 ステップテスト |
| 3 | `CouponService.Tests/Unit/Services/FraudDetectionServiceTests.cs` | 不正検知テスト（4 パターン） |
| 4 | `CouponService.Tests/Unit/Services/CouponServiceTests.cs` | CouponService テスト |
| 5 | `CouponService.Tests/Unit/Services/CampaignServiceTests.cs` | CampaignService テスト |
| 6 | `CouponService.Tests/Unit/Validators/ValidateCouponRequestValidatorTests.cs` | バリデーションテスト |
| 7 | `CouponService.Tests/Integration/Endpoints/CouponEndpointsTests.cs` | ユーザー向け API 統合テスト |
| 8 | `CouponService.Tests/Integration/Endpoints/AdminCouponEndpointsTests.cs` | 管理者 API 統合テスト |
| 9 | `CouponService.Tests/Integration/Endpoints/InternalCouponEndpointsTests.cs` | 内部 API 統合テスト |
| 10 | `CouponService.Tests/Integration/Repositories/CouponRepositoryTests.cs` | DB スライステスト（Testcontainers） |
| 11 | `CouponService.Tests/Integration/Repositories/CampaignRepositoryTests.cs` | DB スライステスト（Testcontainers） |
| 12 | `CouponService.Tests/Security/AuthorizationTests.cs` | 認証・認可テスト |
| 13 | `CouponService.Tests/Fixtures/CouponTestFixture.cs` | テスト共通フィクスチャ |

### 9.1 テストプロジェクト構成

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.*">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.*" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="NSubstitute" Version="5.*" />
    <PackageReference Include="Shouldly" Version="4.*" />
    <PackageReference Include="Testcontainers.PostgreSql" Version="4.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\CouponService\CouponService.csproj" />
  </ItemGroup>
</Project>
```

### 9.2 CouponRuleEngineTests（設計書 §7 — 7 ステップバリデーション）

```csharp
public class CouponRuleEngineTests
{
    private readonly ICouponRuleEngine _ruleEngine;
    private readonly ICouponRepository _couponRepository;
    private readonly IUserCouponRepository _userCouponRepository;
    private readonly ICouponUsageRepository _couponUsageRepository;
    private readonly IFraudDetectionService _fraudDetectionService;
    private readonly ICouponCacheService _cacheService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CouponRuleEngine> _logger;

    public CouponRuleEngineTests()
    {
        _couponRepository = Substitute.For<ICouponRepository>();
        _userCouponRepository = Substitute.For<IUserCouponRepository>();
        _couponUsageRepository = Substitute.For<ICouponUsageRepository>();
        _fraudDetectionService = Substitute.For<IFraudDetectionService>();
        _cacheService = Substitute.For<ICouponCacheService>();
        _timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
        _logger = Substitute.For<ILogger<CouponRuleEngine>>();

        _ruleEngine = new CouponRuleEngine(
            _couponRepository, _userCouponRepository,
            _couponUsageRepository, _fraudDetectionService,
            _cacheService, _timeProvider, _logger);
    }

    // --- Step 1: アクティブチェック ---

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnInvalid_When_CouponIsInactive()
    {
        // Arrange
        var coupon = CreateTestCoupon(isActive: false);
        _couponRepository.FindByCodeAsync("TEST10", default)
            .Returns(coupon);

        // Act
        var result = await _ruleEngine.ValidateAsync("TEST10", "user-1",
            1000m, null, default);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorCode.ShouldBe("CPN-4010");
    }

    // --- Step 2: 有効期間チェック ---

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnInvalid_When_CouponIsExpired()
    {
        // Arrange
        var coupon = CreateTestCoupon(
            validFrom: DateTime.UtcNow.AddDays(-30),
            validTo: DateTime.UtcNow.AddDays(-1));
        _couponRepository.FindByCodeAsync("EXPIRED", default)
            .Returns(coupon);

        // Act
        var result = await _ruleEngine.ValidateAsync("EXPIRED", "user-1",
            1000m, null, default);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorCode.ShouldBe("CPN-4002");
    }

    // --- Step 3: 全体利用回数チェック ---

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnInvalid_When_GlobalUsageLimitReached()
    {
        // Arrange
        var coupon = CreateTestCoupon(maxUsageCount: 100, currentUsageCount: 100);
        _couponRepository.FindByCodeAsync("MAXED", default)
            .Returns(coupon);

        // Act
        var result = await _ruleEngine.ValidateAsync("MAXED", "user-1",
            1000m, null, default);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorCode.ShouldBe("CPN-4003");
    }

    // --- Step 4: ユーザー利用回数チェック ---

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnInvalid_When_UserUsageLimitReached()
    {
        // Arrange
        var coupon = CreateTestCoupon(maxUsagePerUser: 1);
        _couponRepository.FindByCodeAsync("ONCE", default)
            .Returns(coupon);
        _couponUsageRepository.CountByUserAndCouponAsync(
                coupon.Id, "user-1", default)
            .Returns(1);

        // Act
        var result = await _ruleEngine.ValidateAsync("ONCE", "user-1",
            1000m, null, default);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorCode.ShouldBe("CPN-4004");
    }

    // --- Step 5: 最低注文金額チェック ---

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnInvalid_When_OrderAmountBelowMinimum()
    {
        // Arrange
        var coupon = CreateTestCoupon(minOrderAmount: 5000m);
        _couponRepository.FindByCodeAsync("BIGORDER", default)
            .Returns(coupon);

        // Act
        var result = await _ruleEngine.ValidateAsync("BIGORDER", "user-1",
            3000m, null, default);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorCode.ShouldBe("CPN-4005");
    }

    // --- Step 7: 正常パス（全ステップ通過 → 割引額計算） ---

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnValid_When_AllRulesPass()
    {
        // Arrange
        var coupon = CreateTestCoupon(
            discountType: DiscountType.Percentage, discountValue: 10m);
        _couponRepository.FindByCodeAsync("VALID10", default)
            .Returns(coupon);
        _couponUsageRepository.CountByUserAndCouponAsync(
                coupon.Id, "user-1", default)
            .Returns(0);
        _fraudDetectionService.CheckAsync("user-1", coupon, default)
            .Returns(FraudCheckResult.Clear);

        // Act
        var result = await _ruleEngine.ValidateAsync("VALID10", "user-1",
            10000m, null, default);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.DiscountAmount.ShouldBe(1000m);
    }

    private static Coupon CreateTestCoupon(
        bool isActive = true,
        DateTime? validFrom = null,
        DateTime? validTo = null,
        int maxUsageCount = 1000,
        int currentUsageCount = 0,
        int maxUsagePerUser = 10,
        decimal minOrderAmount = 0m,
        DiscountType discountType = DiscountType.FixedAmount,
        decimal discountValue = 500m)
    {
        return new Coupon
        {
            Id = Guid.NewGuid().ToString(),
            Code = "TEST",
            IsActive = isActive,
            ValidFrom = validFrom ?? DateTime.UtcNow.AddDays(-7),
            ValidTo = validTo ?? DateTime.UtcNow.AddDays(30),
            MaxUsageCount = maxUsageCount,
            CurrentUsageCount = currentUsageCount,
            MaxUsagePerUser = maxUsagePerUser,
            MinOrderAmount = minOrderAmount,
            DiscountType = discountType,
            DiscountValue = discountValue
        };
    }
}
```

### 9.3 FraudDetectionServiceTests（設計書 §8 — 4 パターン）

```csharp
public class FraudDetectionServiceTests
{
    // 各テストケースの命名パターン: Should_期待結果_When_条件

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnClear_When_NormalUsage() { /* ... */ }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnSuspicious_When_HighFrequencyUsage() { /* ... */ }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnSuspicious_When_MultipleDevices() { /* ... */ }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnSuspicious_When_UnusualPattern() { /* ... */ }
}
```

### 9.4 統合テスト（WebApplicationFactory）

```csharp
[Trait("Category", "Integration")]
public class CouponEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public CouponEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // テスト用 DB / モック / カスタム AuthenticationHandler
            });
        }).CreateClient();
    }

    [Fact]
    public async Task Should_Return200_When_GetAvailableCoupons()
    {
        // Arrange — JWT トークンセットアップ

        // Act
        var response = await _client.GetAsync("/api/v1/coupons/available");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_Return401_When_NoToken()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/coupons/mine");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
```

### 9.5 DB スライステスト（Testcontainers）

```csharp
[Trait("Category", "Integration")]
public class CouponRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .Build();
    private AppDbContext _context = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        _context = new AppDbContext(options, TimeProvider.System);
        await _context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Should_FindCoupon_When_CodeExists()
    {
        // Arrange
        var coupon = new Coupon { Code = "TEST10", IsActive = true };
        _context.Coupons.Add(coupon);
        await _context.SaveChangesAsync();

        // Act
        var repo = new CouponRepository(_context);
        var result = await repo.FindByCodeAsync("TEST10");

        // Assert
        result.ShouldNotBeNull();
        result.Code.ShouldBe("TEST10");
    }

    [Fact]
    public async Task Should_ReturnNull_When_CodeDoesNotExist()
    {
        // Arrange — 空の DB

        // Act
        var repo = new CouponRepository(_context);
        var result = await repo.FindByCodeAsync("NONEXISTENT");

        // Assert
        result.ShouldBeNull();
    }
}
```

### 9.6 セキュリティテスト

```csharp
[Trait("Category", "Security")]
public class AuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Should_Return401_When_AdminEndpointWithoutAuth()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/admin/coupons");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_Return403_When_UserAccessesAdminEndpoint()
    {
        // Arrange — User ロールの JWT

        // Act
        var response = await _client.GetAsync("/api/v1/admin/coupons");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
```

### 9.7 テストカバレッジ確認コマンド

```bash
dotnet test --collect:"XPlat Code Coverage"
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
dotnet test --filter "Category=Security"
```

### Phase 9 完了チェックリスト

- [ ] `dotnet test` — 全テスト成功
- [ ] CouponRuleEngineTests: 全 7 ステップ（6 異常系 + 1 正常系）のテストが実装されていること
- [ ] FraudDetectionServiceTests: 4 パターン（Clear / HighFrequency / MultipleDevices / UnusualPattern）
- [ ] CouponServiceTests: `AcquireCouponAsync` / `RedeemCouponAsync` / `ReleaseCouponAsync` の正常系 + 異常系
- [ ] CampaignServiceTests: CRUD + ステータス遷移テスト
- [ ] ValidatorTests: 各バリデーションルールのテスト
- [ ] CouponEndpointsTests: GET/POST エンドポイントの統合テスト
- [ ] AdminCouponEndpointsTests: 管理者 API の統合テスト
- [ ] AuthorizationTests: 認証なし → 401、権限不足 → 403
- [ ] CouponRepositoryTests: Testcontainers.PostgreSql を使用した DB スライステスト
- [ ] テスト命名: `Should_期待結果_When_条件` パターン
- [ ] AAA パターン: `// Arrange` / `// Act` / `// Assert` コメントあり
- [ ] `[Trait("Category", "...")]` が全テストに付与されていること
- [ ] Shouldly を使用した具体的なアサーション
- [ ] NSubstitute で `Received()` / `DidNotReceive()` による呼び出し検証
- [ ] 例外テストで型 + メッセージを検証
- [ ] `dotnet test --collect:"XPlat Code Coverage"` で分岐カバレッジ 80% 以上を確認
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 10: 可観測性（Observability）

### 目的

AGENTS.md §11.2（可観測性）および設計書 §F（Program.cs 統合）に基づき、Serilog 構造化ログ、OpenTelemetry 分散トレーシング / メトリクス、Correlation ID 伝搬、ヘルスチェック（Liveness / Readiness）を構成する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `CouponService/Program.cs` | 更新 | Serilog / OpenTelemetry / ヘルスチェック統合 |

### 10.1 Serilog 構造化ログ

```csharp
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "CouponService")
        .WriteTo.Console(new CompactJsonFormatter()));
```

### 10.2 OpenTelemetry 分散トレーシング / メトリクス

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddGrpcClientInstrumentation()
        .AddSource("SkiShop.CouponService"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation());
```

### 10.3 カスタムメトリクス（設計書 §7 / §12 対応）

```csharp
// CouponService 固有のメトリクス
private static readonly Meter CouponMeter = new("SkiShop.CouponService", "1.0");
private static readonly Counter<long> CouponValidationCounter =
    CouponMeter.CreateCounter<long>("coupon.validation.count",
        description: "クーポンバリデーション実行回数");
private static readonly Counter<long> CouponAppliedCounter =
    CouponMeter.CreateCounter<long>("coupon.applied.count",
        description: "クーポン適用回数");
private static readonly Counter<long> FraudDetectionCounter =
    CouponMeter.CreateCounter<long>("coupon.fraud_detection.count",
        description: "不正検知回数");
private static readonly Histogram<double> RuleEngineLatency =
    CouponMeter.CreateHistogram<double>("coupon.rule_engine.duration_ms",
        description: "ルールエンジン実行時間（ミリ秒）");
private static readonly Counter<long> CacheHitCounter =
    CouponMeter.CreateCounter<long>("coupon.cache.hit_count",
        description: "キャッシュヒット回数");
private static readonly Counter<long> CacheMissCounter =
    CouponMeter.CreateCounter<long>("coupon.cache.miss_count",
        description: "キャッシュミス回数");
// 設計書 §16.1 追加メトリクス
private static readonly Histogram<double> DiscountAmountHistogram =
    CouponMeter.CreateHistogram<double>("coupon.discount.amount",
        description: "適用された割引額の分布");
private static readonly Counter<long> CouponRedeemedCounter =
    CouponMeter.CreateCounter<long>("coupon.redeemed.total",
        description: "クーポン利用確定回数");
// Gauge は ObservableGauge で実装（コールバックでアクティブキャンペーン数を返す）
// CouponMeter.CreateObservableGauge("campaign.active.count",
//     () => /* ICampaignRepository.GetActiveCampaignsAsync().Count */,
//     description: "アクティブキャンペーン数");
```

### 10.4 ヘルスチェック

```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql", tags: ["ready"])
    .AddRedis(redisConnectionString, name: "redis", tags: ["ready"]);

// Liveness
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

// Readiness
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();
```

### 10.5 ログ設定（環境別 — appsettings 対応）

| 環境 | Default | Microsoft.AspNetCore | SkiShop | EF Core |
|------|---------|---------------------|---------|---------|
| Production | Warning | Warning | Information | Warning |
| Development | Information | Debug | Debug | Information |

### Phase 10 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] Serilog: `CompactJsonFormatter` + `Enrich.WithProperty("ServiceName", "CouponService")`
- [ ] Serilog: ログメッセージテンプレート形式（文字列補間禁止）
- [ ] OpenTelemetry: `AddAspNetCoreInstrumentation` / `AddHttpClientInstrumentation` / `AddEntityFrameworkCoreInstrumentation`
- [ ] OpenTelemetry: `AddSource("SkiShop.CouponService")` — カスタムトレース
- [ ] カスタムメトリクス: `coupon.validation.count` / `coupon.applied.count` / `coupon.fraud_detection.count`
- [ ] カスタムメトリクス: `coupon.discount.amount`（Histogram — 割引額分布、設計書 §16.1）
- [ ] カスタムメトリクス: `coupon.redeemed.total`（Counter — クーポン利用確定回数、設計書 §16.1）
- [ ] カスタムメトリクス: `campaign.active.count`（ObservableGauge — アクティブキャンペーン数、設計書 §16.1）
- [ ] ヘルスチェック: `/health`（Liveness — 常に 200）
- [ ] ヘルスチェック: `/health/ready`（Readiness — PostgreSQL + Redis 疎通確認）
- [ ] ヘルスチェック: `AllowAnonymous()` が設定されていること
- [ ] CorrelationIdMiddleware: リクエスト/レスポンスヘッダー `X-Correlation-Id`
- [ ] PII ログ禁止: Email / PasswordHash / 住所 / クレジットカード番号がログに出力されないこと
- [ ] 環境別ログレベル: Production → Warning（Default）、Development → Information
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 11: Docker / デプロイ準備

### 目的

AGENTS.md §12.5（Dockerfile 規約）および設計書 §15（Dockerfile）に基づき、マルチステージビルド、非 root 実行、ヘルスチェック付きの本番用 Docker イメージを構成する。.NET Aspire AppHost への統合も完了する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `CouponService/Dockerfile` | 作成（Phase 1 の雛形を完成版に更新） | マルチステージ + 非 root + HEALTHCHECK |
| 2 | `CouponService/.dockerignore` | 確認 | Phase 1 で作成済み |
| 3 | `AppHost/Program.cs` | 更新 | CouponService 統合 |

### 11.1 Dockerfile（設計書 §15 準拠 — 完成版）

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["CouponService/CouponService.csproj", "CouponService/"]
RUN dotnet restore "CouponService/CouponService.csproj"

COPY . .
WORKDIR "/src/CouponService"
RUN dotnet publish "CouponService.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN groupadd -r skishop && useradd -r -g skishop -d /app -s /sbin/nologin skishop
COPY --from=build --chown=skishop:skishop /app/publish .

USER skishop

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "CouponService.dll"]
```

### 11.2 AppHost 統合

```csharp
// AppHost/Program.cs に追加
var couponService = builder.AddProject<Projects.CouponService>("coupon-service")
    .WithReference(postgres)
    .WithReference(redis)
    .WithReference(kafka);

builder.AddProject<Projects.ApiGateway>("api-gateway")
    .WithReference(couponService);
```

### 11.3 Program.cs 最終構成（全レイヤー統合 — 完全版）

```csharp
// ===== Program.cs 最終構成 =====

var builder = WebApplication.CreateBuilder(args);

// --- 1. Serilog ---
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "CouponService")
        .WriteTo.Console(new CompactJsonFormatter()));

// --- 2. 設定（IOptions<T>） ---
builder.Services.AddOptions<CouponSettings>()
    .Bind(builder.Configuration.GetSection("Coupon"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// --- 3. 認証・認可 ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("UserOrAdmin", p => p.RequireRole("User", "Admin"));
    options.AddPolicy("InternalServiceOnly", p =>
        p.AddRequirements(new InternalServiceRequirement()));
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// --- 4. DbContext ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("接続文字列が設定されていません");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// --- 5. Redis ---
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(
        builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));

// --- 6. DI 登録 ---
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICouponRepository, CouponRepository>();
builder.Services.AddScoped<ICampaignRepository, CampaignRepository>();
builder.Services.AddScoped<ICouponTypeRepository, CouponTypeRepository>();
builder.Services.AddScoped<IUserCouponRepository, UserCouponRepository>();
builder.Services.AddScoped<ICouponUsageRepository, CouponUsageRepository>();
builder.Services.AddScoped<IOutboxEventRepository, OutboxEventRepository>();
builder.Services.AddScoped<ICouponService, CouponService>();
builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<ICouponRuleEngine, CouponRuleEngine>();
builder.Services.AddScoped<IFraudDetectionService, FraudDetectionService>();
builder.Services.AddScoped<ICouponCacheService, CouponCacheService>();
builder.Services.AddScoped<ICouponAnalyticsService, CouponAnalyticsService>();
builder.Services.AddScoped<ICouponCodeGenerator, CouponCodeGenerator>();
builder.Services.AddScoped<ICouponRestrictionRepository, CouponRestrictionRepository>();
builder.Services.AddSingleton<IAuthorizationHandler, InternalServiceHandler>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// --- 7. Kafka ---
// Producer / Consumer DI 登録

// --- 8. BackgroundServices ---
builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddHostedService<OrderEventConsumer>();
builder.Services.AddHostedService<CouponExpirationService>();
builder.Services.AddHostedService<CampaignStatusService>();

// --- 9. OpenTelemetry ---
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource("SkiShop.CouponService"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation());

// --- 10. ヘルスチェック ---
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql", tags: ["ready"])
    .AddRedis(
        builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379",
        name: "redis", tags: ["ready"]);

// --- 11. CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? [])
            .AllowAnyMethod()
            .AllowAnyHeader());
});

// --- 12. レート制限 ---
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("coupon-acquire", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

// --- 13. gRPC ---
builder.Services.AddGrpc();

// ===== ミドルウェアパイプライン =====
var app = builder.Build();

// 1. 例外ハンドラー
app.UseExceptionHandler();

// 2. セキュリティヘッダー
app.UseHsts();
app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy",
        "camera=(), microphone=(), geolocation=()");
    await next();
});

// 3. Correlation ID
app.UseCorrelationId();

// 4. Serilog リクエストログ
app.UseSerilogRequestLogging();

// 5. CORS
app.UseCors("AllowFrontend");

// 6. 認証・認可
app.UseAuthentication();
app.UseAuthorization();

// 7. レート制限
app.UseRateLimiter();

// 8. エンドポイントマッピング
app.MapCouponEndpoints();
app.MapAdminCouponEndpoints();
app.MapCampaignEndpoints();
app.MapInternalCouponEndpoints();
app.MapGrpcService<CouponGrpcService>();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();

app.Run();
```

### Phase 11 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] `dotnet publish -c Release` — 成功
- [ ] Dockerfile: マルチステージビルド（sdk:10.0 → aspnet:10.0）
- [ ] Dockerfile: `USER skishop`（非 root 実行）
- [ ] Dockerfile: `HEALTHCHECK` 設定済み（`/health` エンドポイント使用）
- [ ] Dockerfile: ベースイメージタグが `latest` でないこと
- [ ] Dockerfile: `.dockerignore` で `bin/`, `obj/`, `.git/`, `*.md` を除外
- [ ] AppHost: `coupon-service` プロジェクト参照（postgres / redis / kafka の WithReference）
- [ ] Program.cs: §11.3 の構成に完全一致
- [ ] ミドルウェアパイプライン: Phase 8 §8.5 の順序に完全一致
- [ ] DI 登録: 全 Repository / Service / BackgroundService が登録されていること
- [ ] DI 登録: `ICouponAnalyticsService`, `ICouponCodeGenerator`, `ICouponRestrictionRepository` の Scoped 登録が含まれていること
- [ ] DI 登録: `InternalServiceHandler` の Singleton 登録が含まれていること
- [ ] 認可ポリシー: `InternalServiceOnly` が定義されていること
- [ ] `ValidateOnStart()`: `CouponSettings` の起動時バリデーション
- [ ] CORS: `AllowAnyOrigin()` 不使用
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## ビジネス制約・横断的ルール（設計書 §21 準拠 — 追加セクション）

> 設計書 §21 で定義されているビジネス上の重要制約。全フェーズの実装で以下を遵守すること。

### 制約一覧

| # | 制約 | 適用先 | 検証方法 |
|---|------|--------|---------|
| 1 | **1 注文に 1 クーポン** | CouponService / gRPC | クーポン適用時に orderId ごとに既存利用チェック |
| 2 | **クーポンコード形式**: `^[A-Z0-9-]+$`（大文字英数字 + ハイフン） | CreateCouponRequestValidator | FluentValidation の `Matches` ルール |
| 3 | **割引上限キャップ**: PERCENTAGE クーポンに `MaxDiscountAmount` で上限設定 | CouponRuleEngine Step 7 | ルールエンジンの割引額算出ロジック |
| 4 | **楽観的ロック**: Coupon エンティティの `RowVersion` で同時更新を防止 | CouponRepository / CouponService | `DbUpdateConcurrencyException` → `ConcurrencyException` 変換 |
| 5 | **Redis フォールバック**: Redis 障害時は DB に直接問い合わせ | CouponCacheService | try-catch でキャッシュ失敗を握りつぶさず、DB フォールバック |
| 6 | **クーポン → ポイント適用順序**: Saga Step 3（クーポン）→ Step 4（ポイント）の順で適用（設計書 §21.1） | CheckoutService（SalesManagementService 側） | Saga オーケストレーター |

### gRPC Proto ファイル（設計書 §H 準拠）

gRPC サービスの Proto 定義は `SkiShop.Contracts/Protos/coupon_v1.proto` に配置する（設計書 §H 準拠）。
Phase 5 §5.4 の gRPC 実装時に以下の Proto を作成すること:

- `service CouponGrpcService` — `ApplyCoupon` RPC（Saga Step 3 正常パス、Deadline: 300ms）
- `service CouponGrpcService` — `ReleaseCoupon` RPC（Saga 補償トランザクション、Deadline: 500ms、べき等）
- メッセージ型: `ApplyCouponRequest`, `ApplyCouponResponse`, `ReleaseCouponRequest`, `ReleaseCouponResponse`, `OrderItemProto`
- `decimal` 値は `string` 型で送受信（Proto3 に `decimal` 型が存在しないため）
- `package skishop.coupon.v1;` / `option csharp_namespace = "SkiShop.Contracts.Coupon.V1";`

---

## 横断的コンプライアンスチェックリスト

全フェーズ完了後に以下のコマンドを実行し、禁止パターンがプロジェクトに含まれていないことを確認する。

### 1. Critical 禁止パターン

```bash
# 秘密情報のハードコードチェック
grep -r "Password\s*=\s*\"" --include="*.cs" CouponService/
grep -r "ApiKey\s*=\s*\"" --include="*.cs" CouponService/
grep -r "Token\s*=\s*\"" --include="*.cs" CouponService/
grep -r "Secret\s*=\s*\"" --include="*.cs" CouponService/

# Console.WriteLine チェック
grep -r "Console\.Write" --include="*.cs" CouponService/

# SQL 文字列結合チェック
grep -r "FromSqlRaw.*\+" --include="*.cs" CouponService/
grep -r 'FromSqlRaw.*\$' --include="*.cs" CouponService/

# 例外の握りつぶしチェック
grep -rn "catch.*Exception.*{" --include="*.cs" -A 2 CouponService/ | grep -B 1 "^--$"

# 直接インスタンス化チェック
grep -r "new HttpClient()" --include="*.cs" CouponService/
```

### 2. High 禁止パターン

```bash
# .Result / .Wait() チェック（デッドロックリスク）
grep -rP "\.(Result|Wait)\(\)" --include="*.cs" CouponService/

# Thread.Sleep() チェック
grep -r "Thread\.Sleep" --include="*.cs" CouponService/

# DateTime.Now チェック（DateTime.UtcNow / TimeProvider を使用）
grep -r "DateTime\.Now[^U]" --include="*.cs" CouponService/

# プロパティインジェクションチェック
grep -r "\[Inject\]" --include="*.cs" CouponService/

# 文字列補間ログチェック
grep -rP '_logger\.Log.*\$"' --include="*.cs" CouponService/
```

### 3. テスト規約チェック

```bash
# アサーションなしテスト検出（[Fact] の後に Assert/Should がないメソッド）
grep -rn "\[Fact\]" --include="*.cs" CouponService.Tests/

# テスト命名パターンチェック（Should_ で始まること）
grep -rn "public async Task " --include="*.cs" CouponService.Tests/ | grep -v "Should_"

# AAA コメントチェック
grep -rn "// Arrange\|// Act\|// Assert" --include="*.cs" CouponService.Tests/

# Trait カテゴリチェック
grep -rn "\[Trait(" --include="*.cs" CouponService.Tests/
```

### 4. コーディング規約チェック

```bash
# CancellationToken 伝搬チェック（async メソッドに ct がないもの）
grep -rn "async Task<\|async Task " --include="*.cs" CouponService/ | grep -v "CancellationToken" | grep -v "Test" | grep -v "BackgroundService"

# appsettings.json の秘密情報チェック
grep -r "Password" --include="*.json" CouponService/
grep -r "Secret" --include="*.json" CouponService/

# 全テスト実行
dotnet test CouponService.Tests/ --collect:"XPlat Code Coverage"

# カバレッジ確認
dotnet test CouponService.Tests/ --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
```

### 5. Docker / インフラチェック

```bash
# Dockerfile の latest チェック
grep "latest" CouponService/Dockerfile

# USER 命令の存在チェック
grep "^USER" CouponService/Dockerfile

# HEALTHCHECK の存在チェック
grep "HEALTHCHECK" CouponService/Dockerfile

# .dockerignore の存在チェック
test -f CouponService/.dockerignore && echo "OK" || echo "MISSING"
```

### 6. 最終ビルド確認

```bash
# ソリューションビルド
dotnet build --no-restore

# テスト実行（全カテゴリ）
dotnet test CouponService.Tests/ --filter "Category=Unit"
dotnet test CouponService.Tests/ --filter "Category=Integration"
dotnet test CouponService.Tests/ --filter "Category=Security"

# 公開ビルド
dotnet publish CouponService/CouponService.csproj -c Release -o ./publish

# Docker イメージビルド
docker build -t skishop/coupon-service:latest -f CouponService/Dockerfile .
```
