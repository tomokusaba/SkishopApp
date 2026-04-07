# PointService フェーズ別実装計画書

> **対象サービス**: PointService（ポイント管理マイクロサービス）
> **ポート**: 5007
> **DB**: PostgreSQL (pointdb) — ADR-0006: Database per Service
> **メッセージング**: Apache Kafka（Outbox パターン — ADR-0005）
> **キャッシュ**: Redis
> **gRPC**: Saga 連携（ReservePoints / ReleasePoints / AwardPoints / ConfirmPoints）
> **設計書**: `design-docs/point-service-design.md`
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

PointService プロジェクトの骨格を構築する。ビルド可能な最小構成を作成し、以降のフェーズの土台とする。

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `PointService/PointService.csproj` | プロジェクト定義（EF Core, Kafka, Redis, Serilog, OpenTelemetry 等） |
| 2 | `PointService/Program.cs` | エントリポイントスケルトン（最小構成） |
| 3 | `PointService/appsettings.json` | 共通設定（安全なデフォルト値のみ、秘密情報禁止） |
| 4 | `PointService/appsettings.Development.json` | 開発環境設定 |
| 5 | `PointService/appsettings.Production.json` | 本番環境設定（環境変数参照のみ） |
| 6 | `PointService/Dockerfile` | マルチステージビルド + 非 root 実行 |
| 7 | `PointService/.dockerignore` | ビルド不要ファイルの除外 |

### 1.1 PointService.csproj

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

    <!-- gRPC（Saga 連携） -->
    <PackageReference Include="Grpc.AspNetCore" Version="2.*" />

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
  </ItemGroup>
</Project>
```

### 1.2 ディレクトリ構造

```
PointService/
├── PointService.csproj
├── Program.cs
├── Endpoints/
│   ├── PointEndpoints.cs
│   ├── TierEndpoints.cs
│   └── InternalPointEndpoints.cs
├── GrpcServices/
│   └── PointGrpcService.cs
├── Services/
│   ├── Interfaces/
│   │   ├── IPointService.cs
│   │   ├── IPointRuleService.cs
│   │   ├── IPointCampaignService.cs
│   │   ├── IPointCalculator.cs
│   │   ├── IExpiryService.cs
│   │   ├── IPointAnalyticsService.cs
│   │   └── IPointCacheService.cs
│   ├── PointService.cs
│   ├── PointRuleService.cs
│   ├── PointCampaignService.cs
│   ├── PointCalculator.cs
│   ├── ExpiryService.cs
│   ├── PointAnalyticsService.cs
│   └── PointCacheService.cs
├── Consumers/
│   ├── OrderEventConsumer.cs
│   ├── MemberRankEventConsumer.cs
│   ├── UserRegisteredEventConsumer.cs
│   ├── UserDeletedEventConsumer.cs
│   └── PaymentRefundedEventConsumer.cs
├── BackgroundServices/
│   ├── PointExpirationChecker.cs
│   └── OutboxPublisher.cs
├── Models/
│   ├── PointAccount.cs
│   ├── PointTransaction.cs
│   ├── PointExpiry.cs
│   ├── PointRule.cs
│   ├── PointCampaign.cs
│   ├── PointConversionRate.cs
│   ├── TierDefinition.cs
│   ├── OutboxEvent.cs
│   └── PointAuditLog.cs
├── DTOs/
│   ├── Requests/
│   │   ├── ConfirmPointsRequest.cs
│   │   ├── ReleasePointsRequest.cs
│   │   ├── AwardPointsRequest.cs
│   │   └── AdjustPointsRequest.cs
│   └── Responses/
│       ├── PointBalanceResponse.cs
│       ├── PointTransactionResponse.cs
│       ├── TierInfoResponse.cs
│       ├── ExpiringPointsResponse.cs
│       └── PointAnalyticsResponse.cs
├── Repositories/
│   ├── Interfaces/
│   │   ├── IPointAccountRepository.cs
│   │   ├── IPointTransactionRepository.cs
│   │   ├── IPointExpiryRepository.cs
│   │   ├── IPointRuleRepository.cs
│   │   ├── IPointCampaignRepository.cs
│   │   ├── ITierDefinitionRepository.cs
│   │   ├── IOutboxEventRepository.cs
│   │   └── IPointAuditLogRepository.cs
│   ├── PointAccountRepository.cs
│   ├── PointTransactionRepository.cs
│   ├── PointExpiryRepository.cs
│   ├── PointRuleRepository.cs
│   ├── PointCampaignRepository.cs
│   ├── TierDefinitionRepository.cs
│   ├── OutboxEventRepository.cs
│   └── PointAuditLogRepository.cs
├── Validators/
│   ├── AdjustPointsRequestValidator.cs
│   ├── ReservePointsRequestValidator.cs
│   └── AwardPointsRequestValidator.cs
├── Events/
│   ├── PointsEarnedEvent.cs
│   ├── PointsRedeemedEvent.cs
│   ├── PointsReservedEvent.cs
│   ├── PointsReleasedEvent.cs
│   ├── PointsExpiredEvent.cs
│   ├── MemberRankUpdatedEvent.cs
│   └── UserRankCache.cs
├── Exceptions/
│   ├── PointException.cs
│   ├── PointAccountNotFoundException.cs
│   ├── InsufficientPointsException.cs
│   ├── PointExpiredException.cs
│   ├── DuplicateTransactionException.cs
│   ├── ConcurrencyException.cs
│   ├── UnauthorizedException.cs
│   └── ForbiddenException.cs
├── Infrastructure/
│   ├── Persistence/
│   │   └── AppDbContext.cs
│   └── Middleware/
│       ├── CorrelationIdMiddleware.cs
│       ├── CorrelationIdMiddlewareExtensions.cs
│       ├── SecurityHeadersMiddleware.cs
│       └── SecurityHeadersMiddlewareExtensions.cs
├── Configurations/
│   └── PointSettings.cs
├── Migrations/
├── appsettings.json
├── appsettings.Development.json
└── appsettings.Production.json
```

### 1.3 Program.cs スケルトン

```csharp
var builder = WebApplication.CreateBuilder(args);

// TimeProvider DI（テスタビリティ）
builder.Services.AddSingleton(TimeProvider.System);

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
    "GroupId": "point-service"
  },
  "Points": {
    "BaseRate": 1,
    "BaseUnit": 100,
    "ExpirationMonths": 12,
    "ExpiryBatchSize": 1000,
    "ExpiryCheckIntervalHours": 24
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

### 1.7 PointSettings.cs

```csharp
/// <summary>ポイントサービス設定（IOptions パターン）。</summary>
public record PointSettings
{
    public int BaseRate { get; init; } = 1;
    public int BaseUnit { get; init; } = 100;
    public int ExpirationMonths { get; init; } = 12;
    public int ExpiryBatchSize { get; init; } = 1000;
    public int ExpiryCheckIntervalHours { get; init; } = 24;
}
```

### 1.8 例外クラス

```csharp
public class NotFoundException(string message) : Exception(message);
public class BusinessException(string message) : Exception(message);
public class BusinessException(string message, Exception inner) : Exception(message, inner);
public class UnauthorizedException() : Exception("認証が必要です");
public class ForbiddenException() : Exception("アクセスが拒否されました");
public class ConcurrencyException(string message) : Exception(message);
```

### 1.9 Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["PointService/PointService.csproj", "PointService/"]
RUN dotnet restore "PointService/PointService.csproj"
COPY . .
WORKDIR "/src/PointService"
RUN dotnet publish "PointService.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN groupadd -r skishop && useradd -r -g skishop -d /app -s /sbin/nologin skishop
COPY --from=build --chown=skishop:skishop /app/publish .
USER skishop

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 5007
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:5007/health || exit 1
ENTRYPOINT ["dotnet", "PointService.dll"]
```

### 1.10 .dockerignore

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

- [ ] `dotnet build PointService/PointService.csproj` — 警告なし成功
- [ ] `dotnet run --project PointService` — 起動確認（`/health` で 200 応答）
- [ ] .csproj: `TreatWarningsAsErrors=true`, `Nullable=enable`, `TargetFramework=net10.0`
- [ ] .csproj: プレリリース版パッケージなし（`-preview`, `-beta`, `-rc` なし）
- [ ] appsettings.json: 秘密情報なし（パスワード、API キー、接続文字列なし）
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

設計書 §5（データモデル）および §A（EF Core エンティティ C# クラス定義）に基づき、全エンティティ・DbContext・初回マイグレーションを作成する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `PointService/Models/PointAccount.cs` | 作成 | Aggregate Root — ポイントアカウント |
| 2 | `PointService/Models/PointTransaction.cs` | 作成 | ポイント取引履歴 |
| 3 | `PointService/Models/PointExpiry.cs` | 作成 | ポイント有効期限管理 |
| 4 | `PointService/Models/PointRule.cs` | 作成 | ポイント付与ルール |
| 5 | `PointService/Models/PointCampaign.cs` | 作成 | ポイントキャンペーン |
| 6 | `PointService/Models/PointConversionRate.cs` | 作成 | ポイント換算レート |
| 7 | `PointService/Models/TierDefinition.cs` | 作成 | ティア定義マスター（ローカル参照用） |
| 8 | `PointService/Models/OutboxEvent.cs` | 作成 | Outbox パターン用イベントテーブル |
| 9 | `PointService/Models/PointAuditLog.cs` | 作成 | 管理者ポイント操作の監査ログ（設計書 §19.2 H-09 対応） |
| 10 | `PointService/Infrastructure/Persistence/AppDbContext.cs` | 作成 | EF Core DbContext（全エンティティ設定） |

### 2.1 PointAccount（Aggregate Root）

```csharp
[Table("point_accounts")]
public class PointAccount
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    [Column("available_points")]
    [Required]
    public int AvailablePoints { get; set; }

    [Column("pending_points")]
    [Required]
    public int PendingPoints { get; set; }

    [Column("total_earned")]
    [Required]
    public int TotalEarned { get; set; }

    [Column("total_spent")]
    [Required]
    public int TotalSpent { get; set; }

    [Column("total_expired")]
    [Required]
    public int TotalExpired { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    public ICollection<PointTransaction> Transactions { get; set; } = [];
    public ICollection<PointExpiry> Expiries { get; set; } = [];
}
```

### 2.2 PointTransaction

```csharp
/// <summary>
/// ポイント取引履歴。
/// Type: EARN / REDEEM / EXPIRE / ADJUST / RESERVE / RELEASE / REFUND / CANCEL
/// </summary>
[Table("point_transactions")]
public class PointTransaction
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("account_id")]
    [Required]
    [MaxLength(36)]
    public string AccountId { get; set; } = string.Empty;

    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    [Column("type")]
    [Required]
    [MaxLength(20)]
    public string Type { get; set; } = string.Empty;

    [Column("points")]
    [Required]
    public int Points { get; set; }

    [Column("balance_after")]
    [Required]
    public int BalanceAfter { get; set; }

    [Column("reference_id")]
    [MaxLength(36)]
    public string? ReferenceId { get; set; }

    [Column("reference_type")]
    [MaxLength(50)]
    public string? ReferenceType { get; set; }

    [Column("description")]
    [MaxLength(500)]
    public string? Description { get; set; }

    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(AccountId))]
    public PointAccount? Account { get; set; }
}
```

### 2.3 PointExpiry

```csharp
/// <summary>
/// ポイント有効期限管理。Status: ACTIVE / EXPIRED / CONSUMED
/// </summary>
[Table("point_expiries")]
public class PointExpiry
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("account_id")]
    [Required]
    [MaxLength(36)]
    public string AccountId { get; set; } = string.Empty;

    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    [Column("points")]
    [Required]
    public int Points { get; set; }

    [Column("expires_at")]
    [Required]
    public DateTime ExpiresAt { get; set; }

    [Column("status")]
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "ACTIVE";

    [Column("source_transaction_id")]
    [MaxLength(36)]
    public string? SourceTransactionId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(AccountId))]
    public PointAccount? Account { get; set; }

    [ForeignKey(nameof(SourceTransactionId))]
    public PointTransaction? SourceTransaction { get; set; }
}
```

### 2.4 PointRule / PointCampaign / PointConversionRate / TierDefinition / OutboxEvent

設計書 §A のエンティティ定義に完全準拠。各エンティティは `[Table("snake_case")]` + `[Column("snake_case")]` で DB 側の命名規則と整合。`DateTime.Now` 禁止、`DateTime.UtcNow` を使用。

### 2.4.1 PointAuditLog（設計書 §19.2 H-09 対応）

管理者によるポイント手動操作（付与・減算・調整）の監査証跡を記録するエンティティ。

```csharp
[Table("point_audit_logs")]
public class PointAuditLog
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("admin_user_id")]
    [Required]
    [MaxLength(36)]
    public string AdminUserId { get; set; } = string.Empty;

    [Column("target_user_id")]
    [Required]
    [MaxLength(36)]
    public string TargetUserId { get; set; } = string.Empty;

    [Column("action")]
    [Required]
    [MaxLength(20)]
    public string Action { get; set; } = string.Empty;

    [Column("points_before")]
    public int PointsBefore { get; set; }

    [Column("points_after")]
    public int PointsAfter { get; set; }

    [Column("points_changed")]
    public int PointsChanged { get; set; }

    [Column("reason")]
    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    [Column("ip_address")]
    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

**重要**: `AdjustPoints` エンドポイントから `HttpContext` 経由で IP アドレス・UserAgent を取得し、監査ログに記録する。

### 2.5 AppDbContext

```csharp
public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    TimeProvider timeProvider) : DbContext(options)
{
    public DbSet<PointAccount> PointAccounts => Set<PointAccount>();
    public DbSet<PointTransaction> PointTransactions => Set<PointTransaction>();
    public DbSet<PointExpiry> PointExpiries => Set<PointExpiry>();
    public DbSet<PointRule> PointRules => Set<PointRule>();
    public DbSet<PointCampaign> PointCampaigns => Set<PointCampaign>();
    public DbSet<PointConversionRate> PointConversionRates => Set<PointConversionRate>();
    public DbSet<TierDefinition> TierDefinitions => Set<TierDefinition>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    public DbSet<PointAuditLog> PointAuditLogs => Set<PointAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // PointAccount
        modelBuilder.Entity<PointAccount>(entity =>
        {
            entity.HasIndex(a => a.UserId).IsUnique();
            entity.Property(a => a.AvailablePoints).HasDefaultValue(0);
            entity.Property(a => a.PendingPoints).HasDefaultValue(0);
            entity.Property(a => a.TotalEarned).HasDefaultValue(0);
            entity.Property(a => a.TotalSpent).HasDefaultValue(0);
            entity.Property(a => a.TotalExpired).HasDefaultValue(0);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_point_accounts_available_points", "available_points >= 0"));
            entity.HasMany(a => a.Transactions)
                .WithOne(t => t.Account).HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(a => a.Expiries)
                .WithOne(e => e.Account).HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PointTransaction
        modelBuilder.Entity<PointTransaction>(entity =>
        {
            entity.HasIndex(t => t.UserId).HasDatabaseName("idx_point_tx_user_id");
            entity.HasIndex(t => t.AccountId).HasDatabaseName("idx_point_tx_account_id");
            entity.HasIndex(t => new { t.ReferenceId, t.ReferenceType })
                .HasDatabaseName("idx_point_tx_reference");
            entity.HasIndex(t => t.CreatedAt).HasDatabaseName("idx_point_tx_created_at");
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_point_transactions_type",
                "type IN ('EARN','REDEEM','EXPIRE','ADJUST','RESERVE','RELEASE','REFUND','CANCEL')"));
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_point_transactions_points", "points != 0"));
        });

        // PointExpiry
        modelBuilder.Entity<PointExpiry>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.ExpiresAt })
                .HasDatabaseName("idx_point_expiry_user_expires")
                .HasFilter("status = 'ACTIVE'");
            entity.HasIndex(e => e.AccountId)
                .HasDatabaseName("idx_point_expiry_account_id");
            entity.Property(e => e.Status).HasDefaultValue("ACTIVE");
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_point_expiries_status",
                "status IN ('ACTIVE','EXPIRED','CONSUMED')"));
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_point_expiries_points", "points > 0"));
        });

        // PointRule
        modelBuilder.Entity<PointRule>(entity =>
        {
            entity.HasIndex(r => r.Name).IsUnique();
            entity.Property(r => r.MinimumAmount).HasDefaultValue(0m);
            entity.Property(r => r.IsActive).HasDefaultValue(true);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_point_rules_minimum_amount", "minimum_amount >= 0"));
        });

        // PointCampaign
        modelBuilder.Entity<PointCampaign>(entity =>
        {
            entity.HasIndex(c => new { c.StartDate, c.EndDate })
                .HasDatabaseName("idx_campaign_active_dates")
                .HasFilter("is_active = true");
            entity.Property(c => c.Multiplier).HasDefaultValue(1.0m);
            entity.Property(c => c.IsActive).HasDefaultValue(true);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_point_campaigns_multiplier", "multiplier > 0"));
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_point_campaigns_dates", "end_date > start_date"));
        });

        // OutboxEvent
        modelBuilder.Entity<OutboxEvent>(entity =>
        {
            entity.HasIndex(e => new { e.Status, e.CreatedAt })
                .HasDatabaseName("idx_outbox_pending")
                .HasFilter("status = 'PENDING'");
            entity.Property(e => e.Status).HasDefaultValue("PENDING");
            entity.Property(e => e.RetryCount).HasDefaultValue(0);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_outbox_events_status",
                "status IN ('PENDING','PUBLISHED','FAILED')"));
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_outbox_events_retry_count", "retry_count >= 0"));
        });

        // TierDefinition（設計書 §5.2 準拠）
        modelBuilder.Entity<TierDefinition>(entity =>
        {
            entity.HasIndex(t => t.Name).IsUnique();
            entity.HasIndex(t => t.SortOrder);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_tier_definitions_name",
                "name IN ('STANDARD','SILVER','GOLD','PLATINUM')"));
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_tier_definitions_point_rate",
                "point_rate > 0"));
        });

        // PointConversionRate（設計書 §A 準拠）
        modelBuilder.Entity<PointConversionRate>(entity =>
        {
            entity.HasIndex(r => r.CurrencyCode).IsUnique();
            entity.Property(r => r.IsActive).HasDefaultValue(true);
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_point_conversion_rates_rate",
                "rate_per_point > 0"));
        });

        // PointAuditLog（設計書 §19.2 H-09 準拠）
        modelBuilder.Entity<PointAuditLog>(entity =>
        {
            entity.HasIndex(a => a.AdminUserId)
                .HasDatabaseName("idx_audit_admin_user_id");
            entity.HasIndex(a => a.TargetUserId)
                .HasDatabaseName("idx_audit_target_user_id");
            entity.HasIndex(a => a.CreatedAt)
                .HasDatabaseName("idx_audit_created_at");
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_point_audit_logs_action",
                "action IN ('ADD','SUBTRACT','ADJUST')"));
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var entry in ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            if (entry.Entity is PointAccount account)
            {
                if (entry.State == EntityState.Added) account.CreatedAt = now;
                account.UpdatedAt = now;
            }
            else if (entry.Entity is PointExpiry expiry)
            {
                if (entry.State == EntityState.Added) expiry.CreatedAt = now;
                expiry.UpdatedAt = now;
            }
            else if (entry.Entity is PointRule rule)
            {
                if (entry.State == EntityState.Added) rule.CreatedAt = now;
                rule.UpdatedAt = now;
            }
            else if (entry.Entity is PointCampaign campaign)
            {
                if (entry.State == EntityState.Added) campaign.CreatedAt = now;
                campaign.UpdatedAt = now;
            }
            else if (entry.Entity is PointTransaction tx && entry.State == EntityState.Added)
            {
                tx.CreatedAt = now;
            }
            else if (entry.Entity is OutboxEvent outbox && entry.State == EntityState.Added)
            {
                outbox.CreatedAt = now;
            }
            else if (entry.Entity is PointAuditLog auditLog && entry.State == EntityState.Added)
            {
                auditLog.CreatedAt = now;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
```

### 2.6 DTO 定義（Requests / Responses）

```csharp
// === リクエスト DTO ===
public record ReservePointsRequest(
    [Required] string UserId,
    [Required] string OrderId,
    [Required, Range(1, int.MaxValue)] int Points);

public record ConfirmPointsRequest(
    [Required] string UserId,
    [Required] string OrderId);

public record ReleasePointsRequest(
    [Required] string UserId,
    [Required] string OrderId);

public record AwardPointsRequest(
    [Required] string UserId,
    [Required] string OrderId,
    [Required, Range(0.01, double.MaxValue)] decimal OrderAmount);

public record AdjustPointsRequest(
    [Required, Range(1, int.MaxValue)] int Points,
    [Required] string Reason,
    string? ReferenceId = null);

// === レスポンス DTO ===
public record PointBalanceResponse(
    string UserId, int AvailablePoints, int PendingPoints,
    int TotalEarned, int TotalSpent, int TotalExpired);

public record PointTransactionResponse(
    string Id, string Type, int Points, int BalanceAfter,
    string? ReferenceId, string? ReferenceType,
    string? Description, DateTime? ExpiresAt, DateTime CreatedAt);

public record TierInfoResponse(
    string TierName, decimal EarnRateMultiplier,
    int TotalEarnedPoints, int CurrentYearPoints,
    string? NextTierName, int? PointsToNextTier,
    List<string> Benefits);

public record ExpiringPointsResponse(
    List<ExpiringPointItem> Items, int TotalExpiringPoints);

public record ExpiringPointItem(
    int Points, DateTime ExpiresAt, string SourceDescription);

public record PointAnalyticsResponse(
    long TotalPointsIssued, long TotalPointsRedeemed, long TotalPointsExpired,
    double RedemptionRate, Dictionary<string, long> PointsByTier,
    Dictionary<string, long> TierDistribution);

// === 共通 ===
public record ReservePointsResult(
    bool Success, int RemainingBalance, string? ErrorMessage = null);

public record PagedResult<T>(
    List<T> Items, int TotalCount, int Page, int PageSize);

public record PaginationQuery(
    [property: Range(1, int.MaxValue)] int Page = 1,
    [property: Range(1, 100)] int PageSize = 20);
```

### 2.7 イベント record 定義（設計書 §9 準拠）

```csharp
public record PointsEarnedEvent(
    string UserId, int Points, string OrderId,
    int NewBalance, string CorrelationId, DateTime OccurredAt);

public record PointsRedeemedEvent(
    string UserId, int Points, string OrderId,
    int NewBalance, string CorrelationId, DateTime OccurredAt);

public record PointsReservedEvent(
    string UserId, int Points, string OrderId,
    string CorrelationId, DateTime OccurredAt);

public record PointsReleasedEvent(
    string UserId, int Points, string OrderId,
    string CorrelationId, DateTime OccurredAt);

public record PointsExpiredEvent(
    string UserId, int Points, int ExpiredCount,
    string CorrelationId, DateTime OccurredAt);

public record MemberRankUpdatedEvent(
    string UserId, string CurrentRank, decimal PointRate, DateTime OccurredAt);

public record UserRankCache(string Rank, decimal PointRate);
```

### Phase 2 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] `dotnet ef migrations add InitialCreate --project PointService` — マイグレーション生成成功
- [ ] 全エンティティに `[Table("snake_case")]`, `[Column("snake_case")]` 属性あり
- [ ] PointAccount に `[Timestamp]` 楽観的ロックあり
- [ ] コレクションナビゲーション: `= []` で初期化
- [ ] `DateTime.Now` 使用なし → `DateTime.UtcNow` のみ
- [ ] AppDbContext: `SaveChangesAsync` で `CreatedAt` / `UpdatedAt` 自動管理
- [ ] AppDbContext: `TimeProvider` DI
- [ ] 全 CHECK 制約が `OnModelCreating` で定義されていること
- [ ] インデックス定義が設計書 §5 と一致すること
- [ ] PointAuditLog エンティティ: 設計書 §19.2 の全カラム（admin_user_id, target_user_id, action, points_before/after/changed, reason, ip_address, user_agent）が定義されていること
- [ ] TierDefinition: `OnModelCreating` で名前ユニーク制約・CHECK 制約が定義されていること
- [ ] PointConversionRate: `OnModelCreating` で通貨コードユニーク制約・CHECK 制約が定義されていること
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 3: Repository 層実装

### 目的

設計書 §D（Repository インターフェース完全定義）に基づき、Aggregate Root 単位の Repository を実装する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `PointService/Repositories/Interfaces/IPointAccountRepository.cs` | 作成 | Aggregate Root Repository インターフェース |
| 2 | `PointService/Repositories/Interfaces/IPointTransactionRepository.cs` | 作成 | 取引履歴 Repository インターフェース |
| 3 | `PointService/Repositories/Interfaces/IPointExpiryRepository.cs` | 作成 | 有効期限 Repository インターフェース |
| 4 | `PointService/Repositories/Interfaces/IPointRuleRepository.cs` | 作成 | ルール Repository インターフェース |
| 5 | `PointService/Repositories/Interfaces/IPointCampaignRepository.cs` | 作成 | キャンペーン Repository インターフェース |
| 6 | `PointService/Repositories/Interfaces/ITierDefinitionRepository.cs` | 作成 | ティア定義 Repository インターフェース |
| 7 | `PointService/Repositories/Interfaces/IOutboxEventRepository.cs` | 作成 | Outbox イベント Repository インターフェース（設計書 §D 準拠） |
| 8 | `PointService/Repositories/Interfaces/IPointAuditLogRepository.cs` | 作成 | 監査ログ Repository インターフェース（設計書 §19.2 対応） |
| 9-16 | `PointService/Repositories/*.cs` | 作成 | 各 Repository 実装クラス |

### 3.1 IPointAccountRepository

```csharp
public interface IPointAccountRepository
{
    Task<PointAccount?> FindByUserIdAsync(string userId, CancellationToken ct = default);
    Task<PointAccount?> FindByIdAsync(string id, CancellationToken ct = default);
    Task AddAsync(PointAccount account, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

### 3.2 PointAccountRepository 実装

```csharp
public class PointAccountRepository(AppDbContext context) : IPointAccountRepository
{
    public async Task<PointAccount?> FindByUserIdAsync(
        string userId, CancellationToken ct = default)
        => await context.PointAccounts
            .FirstOrDefaultAsync(a => a.UserId == userId, ct);

    public async Task<PointAccount?> FindByIdAsync(
        string id, CancellationToken ct = default)
        => await context.PointAccounts.FindAsync([id], ct);

    public async Task AddAsync(PointAccount account, CancellationToken ct = default)
        => await context.PointAccounts.AddAsync(account, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
```

### 3.3 IPointTransactionRepository

```csharp
public interface IPointTransactionRepository
{
    Task<PointTransaction?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<List<PointTransaction>> FindByAccountIdAsync(
        string accountId, CancellationToken ct = default);
    Task<List<PointTransaction>> FindByUserIdAsync(
        string userId, CancellationToken ct = default);
    Task<(List<PointTransaction> Items, int TotalCount)> GetPagedAsync(
        string userId, int page, int pageSize, CancellationToken ct = default);
    Task<PointTransaction?> FindByReferenceAsync(
        string referenceId, string type, CancellationToken ct = default);
    Task AddAsync(PointTransaction transaction, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

### 3.4 IPointExpiryRepository / IPointRuleRepository / IPointCampaignRepository / ITierDefinitionRepository

設計書 §D に完全準拠。全メソッドに `CancellationToken ct = default` を含む。読み取り専用クエリは `AsNoTracking()` を使用。

**IPointExpiryRepository の必須メソッド**（設計書 §D 準拠）:

```csharp
public interface IPointExpiryRepository
{
    Task<List<PointExpiry>> FindExpiredAsync(
        DateTime asOf, int batchSize, CancellationToken ct = default);
    Task<List<PointExpiry>> FindActiveByAccountIdAsync(
        string accountId, CancellationToken ct = default);
    Task<List<PointExpiry>> FindExpiringWithinAsync(
        string userId, int days, CancellationToken ct = default);
    Task AddAsync(PointExpiry expiry, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

### 3.5 IOutboxEventRepository（設計書 §D 準拠）

```csharp
public interface IOutboxEventRepository
{
    Task<List<OutboxEvent>> FindPendingAsync(
        int batchSize, CancellationToken ct = default);
    Task AddAsync(OutboxEvent outboxEvent, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

### 3.6 IPointAuditLogRepository（設計書 §19.2 対応）

```csharp
public interface IPointAuditLogRepository
{
    Task AddAsync(PointAuditLog log, CancellationToken ct = default);
    Task<List<PointAuditLog>> FindByTargetUserIdAsync(
        string targetUserId, int page, int pageSize,
        CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

### Phase 3 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] 全 Repository にインターフェースと実装クラスが存在すること
- [ ] 全メソッドに `CancellationToken ct = default` が含まれること
- [ ] 読み取り専用クエリに `AsNoTracking()` が使用されていること
- [ ] Repository は Aggregate Root 単位（PointAccount の Repository に Product クエリ混在なし）
- [ ] IOutboxEventRepository: `FindPendingAsync`, `AddAsync`, `SaveChangesAsync` が定義されていること
- [ ] IPointAuditLogRepository: `AddAsync`, `FindByTargetUserIdAsync`, `SaveChangesAsync` が定義されていること
- [ ] IPointExpiryRepository: `FindExpiredAsync`, `FindActiveByAccountIdAsync`, `FindExpiringWithinAsync` が定義されていること
- [ ] `new UserService()` のような直接インスタンス化なし
- [ ] Repository が Service / Endpoints に依存していないこと（依存方向厳守）
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 4: Service 層実装

### 目的

設計書 §E（Service インターフェース完全定義）および §7（ポイントライフサイクル）、§8（ティアシステム）に基づき、ビジネスロジックを実装する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `PointService/Services/Interfaces/IPointService.cs` | 作成 | ポイントコアサービス |
| 2 | `PointService/Services/Interfaces/IPointRuleService.cs` | 作成 | ルール管理 |
| 3 | `PointService/Services/Interfaces/IPointCampaignService.cs` | 作成 | キャンペーン管理 |
| 4 | `PointService/Services/Interfaces/IPointCalculator.cs` | 作成 | ポイント計算エンジン |
| 5 | `PointService/Services/Interfaces/IExpiryService.cs` | 作成 | 有効期限管理 |
| 6 | `PointService/Services/Interfaces/IPointAnalyticsService.cs` | 作成 | ポイント分析レポート |
| 7 | `PointService/Services/Interfaces/IPointCacheService.cs` | 作成 | Redis キャッシュ |
| 8-14 | `PointService/Services/*.cs` | 作成 | 各 Service 実装クラス |
| 15 | `PointService/Validators/AdjustPointsRequestValidator.cs` | 作成 | FluentValidation バリデーター |
| 16 | `PointService/Validators/ReservePointsRequestValidator.cs` | 作成 | FluentValidation バリデーター |
| 17 | `PointService/Validators/AwardPointsRequestValidator.cs` | 作成 | FluentValidation バリデーター |

### 4.1 IPointService

```csharp
public interface IPointService
{
    Task<int> EarnPointsAsync(
        string userId, string orderId, decimal orderAmount,
        CancellationToken ct = default);
    Task<ReservePointsResult> ReservePointsAsync(
        ReservePointsRequest request, CancellationToken ct = default);
    Task<int> ConfirmPointsAsync(
        string userId, string orderId, CancellationToken ct = default);
    Task<int> ReleasePointsAsync(
        string userId, string orderId, CancellationToken ct = default);
    Task<PointBalanceResponse> GetBalanceAsync(
        string userId, CancellationToken ct = default);
    Task<PagedResult<PointTransactionResponse>> GetTransactionHistoryAsync(
        string userId, int page, int pageSize, CancellationToken ct = default);
    Task AdjustPointsAsync(
        string userId, AdjustPointsRequest request,
        string adminUserId, string? ipAddress, string? userAgent,
        CancellationToken ct = default);
    Task<ExpiringPointsResponse> GetExpiringPointsAsync(
        string userId, CancellationToken ct = default);
    Task CreateAccountAsync(
        string userId, CancellationToken ct = default);
    /// <summary>DSR（データ主体要求）: ユーザーデータ匿名化（設計書 §19.1）</summary>
    Task AnonymizeUserDataAsync(
        string userId, CancellationToken ct = default);
}
```

### 4.2 PointService 実装（Saga 冪等性設計含む）

```csharp
public class PointService(
    IPointAccountRepository accountRepository,
    IPointTransactionRepository transactionRepository,
    IPointExpiryRepository expiryRepository,
    IPointAuditLogRepository auditLogRepository,
    IPointCalculator calculator,
    IPointCacheService cacheService,
    AppDbContext context,
    IOptions<PointSettings> options,
    TimeProvider timeProvider,
    ILogger<PointService> logger) : IPointService
{
    private readonly PointSettings _settings = options.Value;

    public async Task<ReservePointsResult> ReservePointsAsync(
        ReservePointsRequest request, CancellationToken ct = default)
    {
        // 冪等性チェック: 同一 orderId + RESERVE
        var existingTx = await transactionRepository.FindByReferenceAsync(
            request.OrderId, "RESERVE", ct);
        if (existingTx is not null)
        {
            logger.LogInformation(
                "冪等性: Reserve 既に処理済み。OrderId={OrderId}", request.OrderId);
            var currentAccount = await accountRepository.FindByUserIdAsync(
                request.UserId, ct);
            return new ReservePointsResult(true, currentAccount?.AvailablePoints ?? 0);
        }

        var account = await accountRepository.FindByUserIdAsync(request.UserId, ct)
            ?? throw new NotFoundException($"PointAccount not found: {request.UserId}");

        if (account.AvailablePoints < request.Points)
            return new ReservePointsResult(false, account.AvailablePoints, "ポイント残高不足");

        account.AvailablePoints -= request.Points;
        account.PendingPoints += request.Points;

        await transactionRepository.AddAsync(new PointTransaction
        {
            AccountId = account.Id,
            UserId = request.UserId,
            Type = "RESERVE",
            Points = -request.Points,
            BalanceAfter = account.AvailablePoints,
            ReferenceId = request.OrderId,
            ReferenceType = "ORDER",
            Description = $"注文 {request.OrderId} のポイント仮消費"
        }, ct);

        // Outbox にイベント INSERT
        context.OutboxEvents.Add(new OutboxEvent
        {
            AggregateType = "PointAccount",
            AggregateId = account.Id,
            EventType = "point.reserved",
            Payload = JsonSerializer.Serialize(new PointsReservedEvent(
                request.UserId, request.Points, request.OrderId,
                "", timeProvider.GetUtcNow().UtcDateTime))
        });

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex,
                "ポイント残高の楽観的ロック競合: UserId={UserId}", request.UserId);
            throw new ConcurrencyException(
                "ポイント残高が他のリクエストにより更新されました。再度お試しください。");
        }

        await cacheService.InvalidateBalanceCacheAsync(request.UserId, ct);
        return new ReservePointsResult(true, account.AvailablePoints);
    }

    // EarnPointsAsync, ConfirmPointsAsync, ReleasePointsAsync 等も同様の冪等性パターンで実装

    // ── FIFO ポイント消費（設計書 §7.2.1 準拠）──
    // ReservePointsAsync 内でポイントを消費する際、PointExpiry テーブルから
    // expires_at の古い順（FIFO）に ACTIVE な有効期限レコードを取得し、
    // 要求ポイント数に達するまで順次 consumed_points を加算する。
    // 全量消費されたレコードは status = 'CONSUMED' に更新する。
    // private async Task ConsumePointsFifoAsync(
    //     PointAccount account, int pointsToConsume, CancellationToken ct)

    // ── 監査ログ記録（設計書 §19.2 H-09）──
    // AdjustPointsAsync 内で PointAuditLog レコードを作成。
    // ipAddress と userAgent は Endpoint から HttpContext 経由で取得して引数で受け取る。
    // public async Task AdjustPointsAsync(
    //     string userId, AdjustPointsRequest request,
    //     string adminUserId, string? ipAddress, string? userAgent, CancellationToken ct)

    // ── DSR ユーザーデータ匿名化（設計書 §19.1）──
    // user.deleted イベント受信時に呼び出し。
    // PointAccount.UserId を匿名化ハッシュに置換、PointTransaction の個人情報をマスク。
    // データ保持ポリシー: 取引履歴は 7 年間保持後に物理削除。
    // public async Task AnonymizeUserDataAsync(string userId, CancellationToken ct)
}
```

### 4.3 PointCalculator（計算エンジン）

```csharp
public class PointCalculator(
    IPointRuleRepository ruleRepository,
    IPointCampaignRepository campaignRepository,
    ILogger<PointCalculator> logger) : IPointCalculator
{
    public async Task<int> CalculateEarnedPointsAsync(
        decimal orderAmount, decimal pointRate,
        string? productCategory = null,
        CancellationToken ct = default)
    {
        var basePoints = (int)Math.Floor(orderAmount * pointRate);

        var rules = await ruleRepository.FindActiveRulesAsync(ct);
        foreach (var rule in rules.Where(r => orderAmount >= r.MinimumAmount))
        {
            basePoints = (int)Math.Floor(basePoints * (1 + rule.ConversionRate));
        }

        var campaigns = await campaignRepository
            .FindActiveCampaignsAsync(DateTime.UtcNow, ct);
        var maxMultiplier = campaigns
            .Where(c => IsApplicable(c, productCategory))
            .Select(c => c.Multiplier)
            .DefaultIfEmpty(1.0m)
            .Max();

        var finalPoints = (int)Math.Floor(basePoints * maxMultiplier);

        logger.LogInformation(
            "ポイント計算: OrderAmount={OrderAmount}, Rate={Rate}, " +
            "Base={BasePoints}, Campaign={Multiplier}, Final={FinalPoints}",
            orderAmount, pointRate, basePoints, maxMultiplier, finalPoints);

        return finalPoints;
    }

    private static bool IsApplicable(PointCampaign campaign, string? category)
        => campaign.TargetProducts is null || category is null
           || campaign.TargetProducts.Contains(category);
}
```

### 4.4 FluentValidation バリデーター

```csharp
public class AdjustPointsRequestValidator : AbstractValidator<AdjustPointsRequest>
{
    private const int MaxAdjustmentPoints = 100_000;

    public AdjustPointsRequestValidator()
    {
        RuleFor(x => x.Points)
            .GreaterThan(0).WithMessage("ポイント数は 1 以上である必要があります")
            .LessThanOrEqualTo(MaxAdjustmentPoints)
            .WithMessage($"1 回の調整は {MaxAdjustmentPoints:N0} ポイントが上限です");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("調整理由は必須です")
            .MaximumLength(500);

        RuleFor(x => x.ReferenceId)
            .MaximumLength(36).When(x => x.ReferenceId is not null);
    }
}

public class ReservePointsRequestValidator : AbstractValidator<ReservePointsRequest>
{
    private const int MaxRedeemPoints = 500_000;

    public ReservePointsRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().MaximumLength(36);
        RuleFor(x => x.OrderId).NotEmpty().MaximumLength(36);
        RuleFor(x => x.Points)
            .GreaterThan(0).LessThanOrEqualTo(MaxRedeemPoints);
    }
}

public class AwardPointsRequestValidator : AbstractValidator<AwardPointsRequest>
{
    public AwardPointsRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().MaximumLength(36);
        RuleFor(x => x.OrderId).NotEmpty().MaximumLength(36);
        RuleFor(x => x.OrderAmount).GreaterThan(0);
    }
}
```

### Phase 4 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] 全 Service にインターフェースと実装クラスが存在すること
- [ ] 全 async メソッドに `CancellationToken ct = default` が含まれること
- [ ] primary constructor による DI を使用していること
- [ ] Saga 操作（Reserve / Release / Award）に冪等性チェックが実装されていること
- [ ] 楽観的ロック: `DbUpdateConcurrencyException` → `ConcurrencyException` 変換
- [ ] PointCalculator: `Math.Floor` で切り捨て計算
- [ ] FluentValidation バリデーター: ポイント上限・必須フィールド・文字列長チェック
- [ ] FIFO ポイント消費: ReservePointsAsync 内で PointExpiry を expires_at 昇順で消費（設計書 §7.2.1）
- [ ] AdjustPointsAsync: PointAuditLog に監査ログを記録、HttpContext から IP/UserAgent を取得（設計書 §19.2）
- [ ] AnonymizeUserDataAsync: DSR（データ主体要求）対応のユーザーデータ匿名化を実装（設計書 §19.1）
- [ ] `Console.WriteLine` なし、`ILogger<T>` メッセージテンプレート使用
- [ ] Endpoints / Controllers が Repository を直接参照していないこと
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 5: Endpoints 実装

### 目的

設計書 §6（API 設計）および §F（Endpoint 実装パターン）に基づき、一般ユーザー向け・管理者向け・内部 API・gRPC エンドポイントを実装する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `PointService/Endpoints/PointEndpoints.cs` | 作成 | 一般ユーザー向け + 管理者向け + 内部 API |
| 2 | `PointService/Endpoints/TierEndpoints.cs` | 作成 | ティア管理エンドポイント |
| 3 | `PointService/GrpcServices/PointGrpcService.cs` | 作成 | gRPC Saga 連携サービス |
| 4 | `PointService/Program.cs` | 更新 | エンドポイント・gRPC マッピング登録 |

### 5.1 エンドポイント一覧

#### 一般ユーザー向け API（設計書 §6.1）

| HTTP メソッド | パス | ハンドラー | 認可 | 説明 |
|---|---|---|---|---|
| GET | `/api/v1/points/balance` | `GetBalance` | `RequireAuthorization()` | 自分のポイント残高取得 |
| GET | `/api/v1/points/history` | `GetHistory` | `RequireAuthorization()` | ポイント取引履歴（ページネーション） |
| GET | `/api/v1/points/tier` | `GetTierInfo` | `RequireAuthorization()` | ティア情報・次ティア進捗 |
| GET | `/api/v1/points/expiring` | `GetExpiringPoints` | `RequireAuthorization()` | 今月失効予定ポイント |

#### 管理者向け API（設計書 §6.2）

| HTTP メソッド | パス | ハンドラー | 認可 | 説明 |
|---|---|---|---|---|
| GET | `/api/v1/admin/points/users/{userId}/balance` | `GetUserBalance` | `AdminOnly` | 指定ユーザー残高参照 |
| POST | `/api/v1/admin/points/users/{userId}/adjust` | `AdjustPoints` | `AdminOnly` | ポイント手動調整 |
| GET | `/api/v1/admin/points/analytics` | `GetAnalytics` | `AdminOnly` | ポイント分析レポート |
| GET | `/api/v1/admin/tiers` | `GetTiers` | `AdminOnly` | ティア定義一覧 |
| PUT | `/api/v1/admin/tiers/{id}` | `UpdateTier` | `AdminOnly` | ティア定義更新 |

#### 内部 API（設計書 §6.3 — サービス間通信用）

| HTTP メソッド | パス | ハンドラー | 認可 | 説明 |
|---|---|---|---|---|
| GET | `/api/v1/internal/points/users/{userId}/balance` | `GetInternalBalance` | `InternalServiceOnly` | ポイント残高取得 |
| POST | `/api/v1/internal/points/reserve` | `ReservePoints` | `InternalServiceOnly` | ポイント仮消費 |
| POST | `/api/v1/internal/points/confirm` | `ConfirmPoints` | `InternalServiceOnly` | ポイント消費確定 |
| POST | `/api/v1/internal/points/release` | `ReleasePoints` | `InternalServiceOnly` | ポイント仮消費解放 |
| POST | `/api/v1/internal/points/award` | `AwardPoints` | `InternalServiceOnly` | ポイント付与 |

#### gRPC サービス（設計書 §6.5 — Saga 連携用）

| gRPC メソッド | 説明 |
|---|---|
| `ReservePoints` | Saga ステップ 4: ポイント仮消費 |
| `ReleasePoints` | Saga 補償: ポイント仮消費解放 |
| `AwardPoints` | Saga ステップ 7: ポイント確定付与 |
| `ConfirmPoints` | ポイント消費確定 |

### 5.2 PointEndpoints 実装

```csharp
public static class PointEndpoints
{
    public static void MapPointEndpoints(this IEndpointRouteBuilder app)
    {
        // ── 一般ユーザー向け ──
        var userGroup = app.MapGroup("/api/v1/points")
            .WithTags("Points")
            .RequireAuthorization()
            .WithOpenApi();

        userGroup.MapGet("/balance", GetBalance).WithName("GetPointBalance");
        userGroup.MapGet("/history", GetHistory).WithName("GetPointHistory");
        userGroup.MapGet("/tier", GetTierInfo).WithName("GetTierInfo");
        userGroup.MapGet("/expiring", GetExpiringPoints).WithName("GetExpiringPoints");

        // ── 管理者向け ──
        var adminGroup = app.MapGroup("/api/v1/admin/points")
            .WithTags("PointsAdmin")
            .RequireAuthorization("AdminOnly")
            .WithOpenApi();

        adminGroup.MapGet("/users/{userId}/balance", GetUserBalance)
            .WithName("AdminGetUserBalance");
        adminGroup.MapPost("/users/{userId}/adjust", AdjustPoints)
            .WithName("AdminAdjustPoints");
        adminGroup.MapGet("/analytics", GetAnalytics)
            .WithName("GetPointAnalytics");

        // ── 内部 API ──
        var internalGroup = app.MapGroup("/api/v1/internal/points")
            .WithTags("PointsInternal")
            .RequireAuthorization("InternalServiceOnly")
            .WithOpenApi();

        internalGroup.MapGet("/users/{userId}/balance", GetInternalBalance)
            .WithName("InternalGetBalance");
        internalGroup.MapPost("/reserve", ReservePoints)
            .WithName("InternalReservePoints");
        internalGroup.MapPost("/confirm", ConfirmPoints)
            .WithName("InternalConfirmPoints");
        internalGroup.MapPost("/release", ReleasePoints)
            .WithName("InternalReleasePoints");
        internalGroup.MapPost("/award", AwardPoints)
            .WithName("InternalAwardPoints");
    }

    // IDOR 防止: ログインユーザー ID を ClaimsPrincipal から取得
    private static async Task<IResult> GetBalance(
        ClaimsPrincipal user,
        IPointService pointService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        return Results.Ok(await pointService.GetBalanceAsync(userId, ct));
    }

    private static async Task<IResult> GetHistory(
        ClaimsPrincipal user,
        [AsParameters] PaginationQuery query,
        IPointService pointService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        return Results.Ok(await pointService.GetTransactionHistoryAsync(
            userId, query.Page, query.PageSize, ct));
    }

    private static async Task<IResult> GetExpiringPoints(
        ClaimsPrincipal user,
        IPointService pointService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        return Results.Ok(await pointService.GetExpiringPointsAsync(userId, ct));
    }

    private static async Task<IResult> AdjustPoints(
        string userId,
        [FromBody] AdjustPointsRequest request,
        IValidator<AdjustPointsRequest> validator,
        ClaimsPrincipal adminUser,
        HttpContext httpContext,
        IPointService pointService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var adminUserId = adminUser.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        await pointService.AdjustPointsAsync(
            userId, request, adminUserId, ipAddress, userAgent, ct);
        return Results.Ok();
    }

    private static async Task<IResult> ReservePoints(
        [FromBody] ReservePointsRequest request,
        IValidator<ReservePointsRequest> validator,
        IPointService pointService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var result = await pointService.ReservePointsAsync(request, ct);
        return result.Success ? Results.Ok(result) : Results.UnprocessableEntity(result);
    }

    // GetUserBalance, GetAnalytics, GetInternalBalance,
    // ConfirmPoints, ReleasePoints, AwardPoints も同様に実装

    // ── GetTierInfo（設計書 §6.1 / §F 準拠）──
    // ClaimsPrincipal から userId を取得し、
    // IPointService.GetBalanceAsync() のレスポンスに含まれるティア情報を返す。
    // private static async Task<IResult> GetTierInfo(
    //     ClaimsPrincipal user, IPointService pointService, CancellationToken ct)
}
```

### 5.2.1 TierEndpoints 実装（設計書 §6.2 準拠）

```csharp
public static class TierEndpoints
{
    public static void MapTierEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/tiers")
            .WithTags("Tiers")
            .RequireAuthorization("AdminOnly")
            .WithOpenApi();

        group.MapGet("/", GetTiers).WithName("GetTiers");
        group.MapPut("/{id}", UpdateTier).WithName("UpdateTier");
    }

    private static async Task<IResult> GetTiers(
        ITierDefinitionRepository tierRepository,
        CancellationToken ct)
        => Results.Ok(await tierRepository.FindAllAsync(ct));

    private static async Task<IResult> UpdateTier(
        string id,
        [FromBody] UpdateTierRequest request,
        ITierDefinitionRepository tierRepository,
        CancellationToken ct)
    {
        var tier = await tierRepository.FindByIdAsync(id, ct)
            ?? throw new NotFoundException($"TierDefinition not found: {id}");
        // ティア定義の更新ロジック
        tier.PointRate = request.PointRate;
        tier.MinAnnualPurchase = request.MinAnnualPurchase;
        tier.BenefitsJson = request.BenefitsJson;
        await tierRepository.SaveChangesAsync(ct);
        return Results.Ok(tier);
    }
}
```

### 5.3 PointGrpcService 実装

```csharp
public class PointGrpcService(
    IPointService pointService,
    ILogger<PointGrpcService> logger) : SkiShop.Point.V1.PointService.PointServiceBase
{
    public override async Task<ReservePointsResponse> ReservePoints(
        ReservePointsRequest request, ServerCallContext context)
    {
        var result = await pointService.ReservePointsAsync(
            new DTOs.Requests.ReservePointsRequest(
                request.UserId, request.OrderId, request.Points),
            context.CancellationToken);

        return new ReservePointsResponse
        {
            Success = result.Success,
            RemainingBalance = result.RemainingBalance,
            ErrorMessage = result.ErrorMessage ?? string.Empty
        };
    }

    // ReleasePoints, AwardPoints, ConfirmPoints も同様に実装
}
```

### Phase 5 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] 一般ユーザー向け: 4 エンドポイント（balance, history, tier, expiring）
- [ ] 管理者向け: 5 エンドポイント（balance, adjust, analytics, tiers, tier update）
- [ ] 内部 API: 5 エンドポイント（balance, reserve, confirm, release, award）
- [ ] gRPC: 4 メソッド（ReservePoints, ReleasePoints, AwardPoints, ConfirmPoints）
- [ ] 全エンドポイントに `.WithOpenApi()` が付与されていること
- [ ] IDOR 防止: 一般ユーザー向けは `ClaimsPrincipal` からユーザー ID を取得
- [ ] AdjustPoints: HttpContext から IP アドレス・UserAgent を取得し、AdjustPointsAsync に渡すこと（設計書 §19.2）
- [ ] TierEndpoints: `GET /api/v1/admin/tiers` と `PUT /api/v1/admin/tiers/{id}` が実装されていること
- [ ] GetTierInfo: `GET /api/v1/points/tier` が一般ユーザー向けに実装されていること
- [ ] バリデーション: `IValidator<T>` による入力検証が全 POST エンドポイントに実装
- [ ] 認可: 一般 → `RequireAuthorization()`、管理者 → `AdminOnly`、内部 → `InternalServiceOnly`
- [ ] Endpoints にビジネスロジックなし（Service 層に委譲）
- [ ] `CancellationToken ct` が全ハンドラーに含まれること
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 6: Kafka イベント連携

### 目的

設計書 §9（イベント設計）に基づき、Outbox パターンによるイベント発行と、外部イベントの購読を実装する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `PointService/BackgroundServices/OutboxPublisher.cs` | 作成 | Outbox → Kafka 発行 BackgroundService |
| 2 | `PointService/BackgroundServices/PointExpirationChecker.cs` | 作成 | ポイント失効バッチ処理 |
| 3 | `PointService/Consumers/OrderEventConsumer.cs` | 作成 | `order.created` / `order.cancelled` イベント購読 |
| 4 | `PointService/Consumers/UserRegisteredEventConsumer.cs` | 作成 | `user.registered` イベント購読 |
| 5 | `PointService/Consumers/MemberRankEventConsumer.cs` | 作成 | `member_rank.updated` イベント購読 |
| 6 | `PointService/Consumers/UserDeletedEventConsumer.cs` | 作成 | `user.deleted` イベント購読 |
| 7 | `PointService/Consumers/PaymentRefundedEventConsumer.cs` | 作成 | `payment.refunded` イベント購読 |

### 6.1 発行イベント一覧（Outbox パターン — ADR-0005 準拠）

| Kafka トピック | イベント record | トリガー |
|---|---|---|
| `point.earned` | `PointsEarnedEvent` | ポイント付与完了 |
| `point.redeemed` | `PointsRedeemedEvent` | ポイント消費確定 |
| `point.reserved` | `PointsReservedEvent` | ポイント仮消費 |
| `point.released` | `PointsReleasedEvent` | ポイント仮消費解放 |
| `point.expired` | `PointsExpiredEvent` | ポイント失効 |

### 6.2 購読イベント一覧

| Kafka トピック | 発行元 | 処理内容 |
|---|---|---|
| `order.created` | SalesManagementService | 注文情報のログ記録・分析用データ蓄積（**ポイント付与は行わない**。付与は gRPC AwardPoints のみ — 設計書 §9.2） |
| `order.cancelled` | SalesManagementService | ポイント返却（消費ポイントの返却） |
| `user.registered` | AuthService | 初期 PointAccount 作成 |
| `user.deleted` | UserManagementService | ポイントアカウント無効化・ユーザーデータ匿名化（設計書 §19.1 DSR 対応） |
| `payment.refunded` | PaymentCartService | 返金時のポイント返却処理 |
| `member_rank.updated` | UserManagementService | ランク情報の Redis キャッシュ同期 |

### 6.3 OutboxPublisher 実装

```csharp
public class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private static readonly TimeSpan MinDelay = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var currentDelay = MinDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // pg_try_advisory_lock でインスタンス排他制御
            var lockAcquired = await context.Database
                .SqlQueryRaw<bool>(
                    "SELECT pg_try_advisory_lock(hashtext('outbox_publisher'))")
                .FirstOrDefaultAsync(stoppingToken);
            if (!lockAcquired)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                continue;
            }

            try
            {
                var pendingEvents = await context.OutboxEvents
                    .Where(e => e.Status == "PENDING")
                    .OrderBy(e => e.CreatedAt)
                    .Take(100)
                    .ToListAsync(stoppingToken);

                if (pendingEvents.Count == 0)
                {
                    currentDelay = TimeSpan.Min(
                        TimeSpan.FromTicks(currentDelay.Ticks * 2), MaxDelay);
                    await Task.Delay(currentDelay, stoppingToken);
                    continue;
                }

                currentDelay = MinDelay;

                foreach (var outboxEvent in pendingEvents)
                {
                    try
                    {
                        await producer.ProduceAsync(
                            outboxEvent.EventType,
                            new Message<string, string>
                            {
                                Key = outboxEvent.AggregateId,
                                Value = outboxEvent.Payload
                            }, stoppingToken);

                        outboxEvent.Status = "PUBLISHED";
                        outboxEvent.ProcessedAt = DateTime.UtcNow;
                    }
                    catch (ProduceException<string, string> ex)
                    {
                        outboxEvent.RetryCount++;
                        if (outboxEvent.RetryCount >= 5)
                            outboxEvent.Status = "FAILED";
                        logger.LogError(ex,
                            "Outbox イベント発行失敗: EventId={EventId}", outboxEvent.Id);
                    }
                }

                await context.SaveChangesAsync(stoppingToken);
            }
            finally
            {
                await context.Database
                    .SqlQueryRaw<bool>(
                        "SELECT pg_advisory_unlock(hashtext('outbox_publisher'))")
                    .FirstOrDefaultAsync(stoppingToken);
            }
        }
    }
}
```

### 6.4 MemberRankEventConsumer 実装

```csharp
public class MemberRankEventConsumer(
    IConsumer<string, string> consumer,
    IConnectionMultiplexer redis,
    ILogger<MemberRankEventConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        consumer.Subscribe("member_rank.updated");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var @event = JsonSerializer.Deserialize<MemberRankUpdatedEvent>(
                    result.Message.Value);
                if (@event is not null)
                {
                    var db = redis.GetDatabase();
                    await db.StringSetAsync(
                        $"points:user_rank:{@event.UserId}",
                        JsonSerializer.Serialize(
                            new UserRankCache(@event.CurrentRank, @event.PointRate)),
                        TimeSpan.FromHours(24));

                    logger.LogInformation(
                        "ランク情報同期: UserId={UserId}, Rank={Rank}",
                        @event.UserId, @event.CurrentRank);
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
                logger.LogError(ex, "ランクイベント処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
```

### 6.5 PointExpirationChecker 実装

設計書 §7.3 準拠。`pg_try_advisory_lock` によるインスタンス排他制御 + バッチサイズ分割処理。

### 6.6 OrderEventConsumer 実装要件（設計書 §9.2）

- **`order.created`**: ログ記録・分析用データ蓄積のみ。**ポイント付与は行わない**（ポイント付与は gRPC `AwardPoints` でのみ実施）。`_logger.LogInformation` で注文情報を記録し、将来的な分析基盤連携に備える。
- **`order.cancelled`**: 注文に関連する `RESERVE` 状態のポイントを返却（`ReleasePointsAsync` を呼び出し）。

### 6.7 UserDeletedEventConsumer 実装要件（設計書 §9.2 + §19.1）

- `user.deleted` イベント受信時に `IPointService.AnonymizeUserDataAsync(userId)` を呼び出し、ユーザーデータの匿名化（DSR 対応）を実施。
- PointAccount の `UserId` を匿名化ハッシュに置換、残高を 0 にリセット。
- PointTransaction の個人情報フィールドをマスク処理。

### 6.8 PaymentRefundedEventConsumer 実装要件（設計書 §9.2）

- `payment.refunded` イベント受信時、返金対象注文で付与されたポイントを減算。
- 冪等性: 同一 `orderId` + `REFUND` の重複チェックを実施。

### Phase 6 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] OutboxPublisher: 動的バックオフ（100ms〜5s）で実装されていること
- [ ] OutboxPublisher: `pg_try_advisory_lock` でインスタンス排他制御
- [ ] OutboxPublisher: リトライ 5 回超過で `FAILED` ステータスに変更
- [ ] PointExpirationChecker: バッチサイズ分割処理（`ExpiryBatchSize` 件ずつ独立トランザクション）
- [ ] PointExpirationChecker: `pg_try_advisory_lock` で排他制御
- [ ] 全 Consumer: `IServiceScopeFactory` で Scoped サービスを取得
- [ ] 全 Consumer: `stoppingToken` を全下位呼び出しに伝搬
- [ ] 全 Consumer: `ConsumeException` を個別にキャッチし、バックオフ後に再試行
- [ ] 全イベント record に `CorrelationId` フィールドが含まれること
- [ ] 購読イベント 6 種（order.created, order.cancelled, user.registered, user.deleted, payment.refunded, member_rank.updated）
- [ ] OrderEventConsumer: `order.created` はログ記録のみ（ポイント付与なし）
- [ ] UserDeletedEventConsumer: `AnonymizeUserDataAsync` を呼び出し DSR 対応
- [ ] PaymentRefundedEventConsumer: 返金時のポイント減算 + 冪等性チェック
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 7: Redis キャッシュ連携

### 目的

設計書 §10（キャッシュ戦略）に基づき、ポイント残高・ランク情報・ティア定義の Redis キャッシュを実装する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `PointService/Services/PointCacheService.cs` | 作成 | Redis キャッシュサービス実装 |
| 2 | `PointService/Program.cs` | 更新 | Redis 接続登録 |

### 7.1 キャッシュキー設計（設計書 §10.1）

| キー | 値 | TTL | 用途 |
|---|---|---|---|
| `points:balance:{userId}` | PointBalanceResponse JSON | 5 分 | 残高の高速読み取り |
| `points:user_rank:{userId}` | UserRankCache JSON | 24 時間 | ランク・還元率キャッシュ |
| `tier:definitions` | 全ティア定義 JSON | 1 時間 | ティア定義マスター |

### 7.2 PointCacheService 実装

```csharp
public class PointCacheService(
    IConnectionMultiplexer redis,
    ILogger<PointCacheService> logger) : IPointCacheService
{
    private static readonly TimeSpan BalanceTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RankTtl = TimeSpan.FromHours(24);

    public async Task<PointBalanceResponse?> GetBalanceCacheAsync(
        string userId, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var cached = await db.StringGetAsync($"points:balance:{userId}");
        if (cached.IsNullOrEmpty) return null;

        return JsonSerializer.Deserialize<PointBalanceResponse>(cached!);
    }

    public async Task SetBalanceCacheAsync(
        string userId, PointBalanceResponse balance,
        CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync(
            $"points:balance:{userId}",
            JsonSerializer.Serialize(balance),
            BalanceTtl);
    }

    public async Task InvalidateBalanceCacheAsync(
        string userId, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync($"points:balance:{userId}");
        logger.LogInformation(
            "残高キャッシュ無効化: UserId={UserId}", userId);
    }

    public async Task<UserRankCache?> GetUserRankAsync(
        string userId, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var cached = await db.StringGetAsync($"points:user_rank:{userId}");
        if (cached.IsNullOrEmpty) return null;

        return JsonSerializer.Deserialize<UserRankCache>(cached!);
    }

    public async Task SetUserRankAsync(
        string userId, UserRankCache rank,
        CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync(
            $"points:user_rank:{userId}",
            JsonSerializer.Serialize(rank),
            RankTtl);
    }
}
```

### 7.3 キャッシュ無効化ルール（設計書 §10.2）

| トリガー | 無効化キー |
|---|---|
| ポイント残高変動（付与・消費・失効） | `points:balance:{userId}` |
| ランク変更イベント受信 | `points:user_rank:{userId}`（更新） |
| ティア定義更新 | `tier:definitions` |

### Phase 7 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] Redis 接続: `IConnectionMultiplexer` の DI 登録
- [ ] キャッシュ TTL: balance=5 分、rank=24 時間、tier=1 時間
- [ ] キャッシュ無効化: ポイント変動時に `InvalidateBalanceCacheAsync` 呼び出し
- [ ] Redis 障害時: DB フォールバックで動作継続（キャッシュミス = DB 直接読み取り）
- [ ] `new HttpClient()` 使用なし
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 8: 認証・認可・セキュリティ

### 目的

設計書 §12（セキュリティ設計）に基づき、JWT 認証・認可ポリシー・IDOR 防止・セキュリティヘッダーを構成する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `PointService/Program.cs` | 更新 | 認証・認可・セキュリティ設定 |
| 2 | `PointService/Infrastructure/Middleware/SecurityHeadersMiddleware.cs` | 作成 | セキュリティヘッダー |
| 3 | `PointService/Infrastructure/Middleware/SecurityHeadersMiddlewareExtensions.cs` | 作成 | 拡張メソッド |
| 4 | `PointService/Infrastructure/Middleware/CorrelationIdMiddleware.cs` | 作成 | Correlation ID |
| 5 | `PointService/Infrastructure/Middleware/CorrelationIdMiddlewareExtensions.cs` | 作成 | 拡張メソッド |

### 8.1 認証設定

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

### 8.2 認可ポリシー

| ポリシー | 条件 | 適用対象 |
|---|---|---|
| `FallbackPolicy` | `RequireAuthenticatedUser()` | 全エンドポイント（デフォルト） |
| `AdminOnly` | `RequireRole("Admin")` | 管理者向け API |
| `InternalServiceOnly` | `RequireClaim("client_id") + RequireClaim("scope", "point:internal")` | 内部 API |

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("InternalServiceOnly", policy =>
        policy.RequireClaim("client_id")
              .RequireClaim("scope", "point:internal"));
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

### 8.3 セキュリティヘッダー

| ヘッダー | 値 |
|---|---|
| `X-Content-Type-Options` | `nosniff` |
| `X-Frame-Options` | `DENY` |
| `Content-Security-Policy` | `default-src 'self'` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` |

### 8.4 IDOR 防止

一般ユーザー向けエンドポイントは `ClaimsPrincipal` からユーザー ID を取得し、他ユーザーのポイント情報にアクセスできないよう制御（Phase 5 で実装済み）。

### 8.5 不正防止ルール（設計書 §12.3）

- ポイント付与は内部 API / gRPC 経由のみ
- 管理者のポイント手動調整は監査ログに記録
- 楽観的ロック（`RowVersion`）で同時更新競合を防止

### 8.6 エラーコード定義（設計書 §11）

| コード | HTTP ステータス | 説明 |
|---|---|---|
| `PNT-4001` | 404 | ユーザーが見つからない |
| `PNT-4002` | 422 | ポイント残高不足 |
| `PNT-4003` | 422 | 無効なポイント数（0 以下） |
| `PNT-4004` | 422 | 仮消費済みの注文 ID で重複リクエスト |
| `PNT-4005` | 409 | 楽観的ロック競合 |
| `PNT-4006` | 422 | 仮消費が存在しない（確定/解放時） |
| `PNT-5001` | 500 | データベースエラー |
| `PNT-5002` | 503 | Redis 接続エラー |
| `PNT-5003` | 503 | Kafka 接続エラー |

### 8.7 例外クラス階層（設計書 §H 準拠）

PointService 専用の例外階層を定義する。`ErrorCode` プロパティで §11 のエラーコードと対応する。

```csharp
// PointService 基底例外
public class PointException : Exception
{
    public string ErrorCode { get; }
    public PointException(string message, string errorCode)
        : base(message) => ErrorCode = errorCode;
    public PointException(string message, string errorCode, Exception innerException)
        : base(message, innerException) => ErrorCode = errorCode;
}

public class PointAccountNotFoundException : PointException
{
    public PointAccountNotFoundException(string userId)
        : base($"ポイントアカウントが見つかりません: UserId={userId}", "PNT-4001") { }
}

public class InsufficientPointsException : PointException
{
    public int RequestedPoints { get; }
    public int AvailablePoints { get; }
    public InsufficientPointsException(int requested, int available)
        : base($"ポイント残高不足です。要求: {requested}, 利用可能: {available}", "PNT-4002")
    { RequestedPoints = requested; AvailablePoints = available; }
}

public class PointExpiredException : PointException
{
    public PointExpiredException(string message = "対象ポイントは有効期限切れです")
        : base(message, "PNT-4003") { }
}

public class DuplicateTransactionException : PointException
{
    public DuplicateTransactionException(string orderId, string type)
        : base($"重複リクエストを検出しました: OrderId={orderId}, Type={type}", "PNT-4004") { }
}

public class ConcurrencyException : PointException
{
    public ConcurrencyException(string message)
        : base(message, "PNT-4005") { }
}

public class UnauthorizedException : PointException
{
    public UnauthorizedException()
        : base("認証が必要です", "PNT-4010") { }
}
```

### 8.8 グローバル例外ハンドラー

```csharp
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// GlobalExceptionHandler — PointException 階層を使用
public class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var (statusCode, message) = exception switch
        {
            PointAccountNotFoundException e    => (404, e.Message),
            InsufficientPointsException e      => (422, e.Message),
            PointExpiredException e             => (422, e.Message),
            DuplicateTransactionException e    => (409, e.Message),
            ConcurrencyException e             => (409, e.Message),
            UnauthorizedException              => (401, "認証が必要です"),
            _                                  => (500, "内部エラーが発生しました")
        };

        if (statusCode >= 500)
            logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        else
            logger.LogWarning("Handled exception: {ExceptionType} - {Message}",
                exception.GetType().Name, exception.Message);

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Detail = message,
            Title = ReasonPhrases.GetReasonPhrase(statusCode)
        }, ct);
        return true;
    }
}
```

### Phase 8 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] JWT 認証: `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey` = `true`
- [ ] JWT: `ClockSkew = TimeSpan.FromMinutes(5)`
- [ ] FallbackPolicy: `RequireAuthenticatedUser()`
- [ ] AdminOnly / InternalServiceOnly ポリシーが定義されていること
- [ ] セキュリティヘッダー: 5 種全て設定
- [ ] IDOR 防止: 一般ユーザーは `ClaimsPrincipal` からユーザー ID 取得
- [ ] グローバル例外ハンドラー: RFC 9457 Problem Details 形式
- [ ] 例外クラス階層: PointException 基底クラスに ErrorCode プロパティ（設計書 §H）
- [ ] 例外クラス: PointAccountNotFoundException, InsufficientPointsException, PointExpiredException, DuplicateTransactionException, ConcurrencyException が定義されていること
- [ ] GlobalExceptionHandler: PointException 派生クラスによる switch 式マッピング
- [ ] InternalServiceOnly ポリシー: `client_id` + `scope: point:internal` 両方のクレーム要求（設計書 §6.6）
- [ ] gRPC エンドポイント: `.RequireAuthorization("InternalServiceOnly")` が設定されていること
- [ ] スタックトレースがクライアントに返却されないこと
- [ ] `DetailedErrors: false` が設定されていること
- [ ] PII ログ禁止: パスワード、トークン、メールアドレス全文がログに出力されないこと
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 9: テスト実装

### 目的

設計書 §A（テスト戦略）に基づき、Unit Test・Integration Test・Security Test を実装し、分岐カバレッジ 80% 以上を達成する。

### テストプロジェクト構成

```
PointService.Tests/
├── PointService.Tests.csproj
├── Unit/
│   ├── Services/
│   │   ├── PointServiceTests.cs
│   │   ├── PointCalculatorTests.cs
│   │   ├── PointExpirationServiceTests.cs
│   │   └── TierServiceTests.cs
│   ├── Validators/
│   │   ├── AdjustPointsRequestValidatorTests.cs
│   │   ├── ReservePointsRequestValidatorTests.cs
│   │   └── AwardPointsRequestValidatorTests.cs
│   ├── Consumers/
│   │   ├── MemberRankEventConsumerTests.cs
│   │   ├── UserRegisteredEventConsumerTests.cs
│   │   ├── UserDeletedEventConsumerTests.cs
│   │   ├── OrderEventConsumerTests.cs
│   │   └── PaymentRefundedEventConsumerTests.cs
│   └── BackgroundServices/
│       └── OutboxPublisherTests.cs
├── Integration/
│   ├── Endpoints/
│   │   ├── PointEndpointsTests.cs
│   │   └── TierEndpointsTests.cs
│   ├── Repositories/
│   │   ├── PointAccountRepositoryTests.cs
│   │   └── PointTransactionRepositoryTests.cs
│   └── GrpcServices/
│       └── PointGrpcServiceTests.cs
├── Security/
│   └── AuthorizationTests.cs
├── Fixtures/
│   ├── PostgresFixture.cs
│   └── TestAuthHandler.cs
└── Helpers/
    └── FakeTimeProviderHelper.cs
```

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `PointService.Tests/PointService.Tests.csproj` | 作成 | テスト依存パッケージ |
| 2 | `PointService.Tests/Unit/Services/PointServiceTests.cs` | 作成 | PointService 単体テスト |
| 3 | `PointService.Tests/Unit/Services/PointCalculatorTests.cs` | 作成 | ポイント計算ロジックテスト |
| 4 | `PointService.Tests/Unit/Services/PointExpirationServiceTests.cs` | 作成 | 失効処理テスト |
| 5 | `PointService.Tests/Unit/Services/TierServiceTests.cs` | 作成 | ティアサービステスト |
| 6 | `PointService.Tests/Unit/Validators/AdjustPointsRequestValidatorTests.cs` | 作成 | バリデーターテスト |
| 7 | `PointService.Tests/Unit/Validators/ReservePointsRequestValidatorTests.cs` | 作成 | バリデーターテスト |
| 8 | `PointService.Tests/Unit/Validators/AwardPointsRequestValidatorTests.cs` | 作成 | バリデーターテスト |
| 9 | `PointService.Tests/Unit/Consumers/MemberRankEventConsumerTests.cs` | 作成 | Consumer テスト |
| 10 | `PointService.Tests/Unit/BackgroundServices/OutboxPublisherTests.cs` | 作成 | OutboxPublisher テスト |
| 11 | `PointService.Tests/Integration/Endpoints/PointEndpointsTests.cs` | 作成 | エンドポイント統合テスト |
| 12 | `PointService.Tests/Integration/Repositories/PointAccountRepositoryTests.cs` | 作成 | Repository DB テスト |
| 13 | `PointService.Tests/Integration/GrpcServices/PointGrpcServiceTests.cs` | 作成 | gRPC 統合テスト |
| 14 | `PointService.Tests/Security/AuthorizationTests.cs` | 作成 | セキュリティテスト |
| 15 | `PointService.Tests/Fixtures/PostgresFixture.cs` | 作成 | Testcontainers PostgreSQL |
| 16 | `PointService.Tests/Fixtures/TestAuthHandler.cs` | 作成 | テスト用認証ハンドラー |
| 17 | `PointService.Tests/Unit/Consumers/UserDeletedEventConsumerTests.cs` | 作成 | DSR 匿名化 Consumer テスト |
| 18 | `PointService.Tests/Unit/Consumers/OrderEventConsumerTests.cs` | 作成 | 注文イベント Consumer テスト |
| 19 | `PointService.Tests/Unit/Consumers/PaymentRefundedEventConsumerTests.cs` | 作成 | 返金イベント Consumer テスト |

### 9.1 テストプロジェクト .csproj

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
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" PrivateAssets="all" />
    <PackageReference Include="NSubstitute" Version="5.*" />
    <PackageReference Include="Shouldly" Version="4.*" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.*" />
    <PackageReference Include="Testcontainers.PostgreSql" Version="4.*" />
    <PackageReference Include="coverlet.collector" Version="6.*" PrivateAssets="all" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="Grpc.Net.ClientFactory" Version="2.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\PointService\PointService.csproj" />
  </ItemGroup>
</Project>
```

### 9.2 PointService 単体テスト（代表例）

```csharp
public class PointServiceTests
{
    private readonly IPointAccountRepository _pointAccountRepo;
    private readonly IPointTransactionRepository _transactionRepo;
    private readonly IPointCacheService _cacheService;
    private readonly ILogger<Services.PointService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly Services.PointService _sut;

    public PointServiceTests()
    {
        _pointAccountRepo = Substitute.For<IPointAccountRepository>();
        _transactionRepo = Substitute.For<IPointTransactionRepository>();
        _cacheService = Substitute.For<IPointCacheService>();
        _logger = Substitute.For<ILogger<Services.PointService>>();
        _timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 3, 18, 12, 0, 0, TimeSpan.Zero));
        _sut = new Services.PointService(
            _pointAccountRepo, _transactionRepo, _cacheService,
            _logger, _timeProvider);
    }

    // ── 正常系テスト ──

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnBalance_When_ValidUserIdProvided()
    {
        // Arrange
        var userId = "user-001";
        var account = new PointAccount
        {
            UserId = userId,
            CurrentBalance = 5000,
            LifetimeEarned = 10000
        };
        _cacheService.GetBalanceCacheAsync(userId, default)
            .Returns((PointBalanceResponse?)null);
        _pointAccountRepo.FindByUserIdAsync(userId, default)
            .Returns(account);

        // Act
        var result = await _sut.GetBalanceAsync(userId);

        // Assert
        result.ShouldNotBeNull();
        result.CurrentBalance.ShouldBe(5000);
        result.LifetimeEarned.ShouldBe(10000);
        await _cacheService.Received(1).SetBalanceCacheAsync(
            userId, Arg.Any<PointBalanceResponse>(), default);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnCachedBalance_When_CacheExists()
    {
        // Arrange
        var userId = "user-001";
        var cached = new PointBalanceResponse(5000, 10000, 0, "SILVER");
        _cacheService.GetBalanceCacheAsync(userId, default).Returns(cached);

        // Act
        var result = await _sut.GetBalanceAsync(userId);

        // Assert
        result.ShouldBe(cached);
        await _pointAccountRepo.DidNotReceive()
            .FindByUserIdAsync(userId, Arg.Any<CancellationToken>());
    }

    // ── 異常系テスト ──

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowNotFoundException_When_UserDoesNotExist()
    {
        // Arrange
        _cacheService.GetBalanceCacheAsync("no-user", default)
            .Returns((PointBalanceResponse?)null);
        _pointAccountRepo.FindByUserIdAsync("no-user", default)
            .Returns((PointAccount?)null);

        // Act & Assert
        var act = async () => await _sut.GetBalanceAsync("no-user");
        var ex = await Should.ThrowAsync<NotFoundException>(act);
        ex.Message.ShouldContain("ポイントアカウント");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_InsufficientBalance()
    {
        // Arrange
        var account = new PointAccount { UserId = "user-001", CurrentBalance = 100 };
        _pointAccountRepo.FindByUserIdAsync("user-001", default).Returns(account);

        var request = new ReservePointsRequest("user-001", "order-001", 500);

        // Act & Assert
        var act = async () => await _sut.ReservePointsAsync(request);
        var ex = await Should.ThrowAsync<BusinessException>(act);
        ex.Message.ShouldContain("残高不足");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReserveSamePoints_IdempotentlyForSameOrderId()
    {
        // Arrange: 同一 orderId で既に RESERVED のトランザクションが存在する
        var account = new PointAccount { UserId = "user-001", CurrentBalance = 1000 };
        _pointAccountRepo.FindByUserIdAsync("user-001", default).Returns(account);
        _transactionRepo.FindByOrderIdAndTypeAsync("order-001", "RESERVE", default)
            .Returns(new PointTransaction { Points = 500 });

        var request = new ReservePointsRequest("user-001", "order-001", 500);

        // Act
        var result = await _sut.ReservePointsAsync(request);

        // Assert
        result.Success.ShouldBeTrue();
        await _pointAccountRepo.DidNotReceive()
            .SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── キャンセルトークンテスト ──

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowOperationCanceled_When_TokenIsCanceled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = async () => await _sut.GetBalanceAsync("user-001", cts.Token);
        await Should.ThrowAsync<OperationCanceledException>(act);
    }
}
```

### 9.3 PointCalculator テスト

```csharp
public class PointCalculatorTests
{
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(10000, 0.01, 1.0, 100)]    // BRONZE: 10000 × 1% × 1.0 = 100
    [InlineData(10000, 0.03, 1.0, 300)]    // SILVER: 10000 × 3% × 1.0 = 300
    [InlineData(10000, 0.05, 1.0, 500)]    // GOLD:   10000 × 5% × 1.0 = 500
    [InlineData(10000, 0.07, 1.0, 700)]    // PLATINUM: 10000 × 7% × 1.0 = 700
    [InlineData(10000, 0.01, 2.0, 200)]    // BRONZE + キャンペーン2倍 = 200
    [InlineData(15999, 0.01, 1.0, 159)]    // 端数切り捨て: 15999 × 1% = 159.99 → 159
    public void Should_CalculateCorrectPoints(
        decimal orderAmount, decimal pointRate,
        decimal campaignMultiplier, int expectedPoints)
    {
        // Arrange
        var calculator = new PointCalculator();

        // Act
        var result = calculator.Calculate(orderAmount, pointRate, campaignMultiplier);

        // Assert
        result.ShouldBe(expectedPoints);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(0)]
    [InlineData(-100)]
    public void Should_ThrowException_When_OrderAmountIsZeroOrNegative(
        decimal invalidAmount)
    {
        // Arrange
        var calculator = new PointCalculator();

        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(
            () => calculator.Calculate(invalidAmount, 0.01m, 1.0m));
    }
}
```

### 9.4 統合テスト（WebApplicationFactory + Testcontainers）

```csharp
public class PointEndpointsTests : IClassFixture<PostgresFixture>
{
    private readonly HttpClient _client;
    private readonly PostgresFixture _fixture;

    public PointEndpointsTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        "Test", _ => { });
            });
        }).CreateClient();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_Return200_When_GetBalance()
    {
        // Arrange: テストデータ投入は PostgresFixture で実施

        // Act
        var response = await _client.GetAsync("/api/v1/points/balance");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<PointBalanceResponse>();
        content.ShouldNotBeNull();
        content.CurrentBalance.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_Return422_When_InsufficientBalance()
    {
        // Arrange
        var request = new ReservePointsRequest("user-001", "order-999", 9999999);

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/v1/internal/points/reserve", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }
}
```

### 9.5 セキュリティテスト

```csharp
public class AuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthorizationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task Should_Return401_When_NoAuthToken()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/points/balance");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task Should_Return403_When_UserAccessAdminEndpoint()
    {
        // Arrange: User ロールのトークンでアクセス
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        "Test", _ => { });
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/admin/points/analytics");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
```

### 9.6 PostgresFixture

```csharp
public class PostgresFixture : IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        await Postgres.StartAsync();
        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor is not null)
                        services.Remove(descriptor);

                    services.AddDbContext<AppDbContext>(options =>
                        options.UseNpgsql(Postgres.GetConnectionString()));
                });
            });

        // Migration 適用
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await Postgres.DisposeAsync();
}
```

### 9.7 テストカバレッジ目標

| レイヤー | 目標 | テスト種別 |
|---|---|---|
| Service（PointService, TierService 等） | 80%+ | Unit Test |
| Validators | 90%+ | Unit Test |
| Endpoints | 80%+ | Integration Test |
| Repository | 70%+ | Testcontainers |
| gRPC | 80%+ | Integration Test |
| BackgroundService | 70%+ | Unit Test（ロジック部分） |
| 全体 | 80%+ | `dotnet test --collect:"XPlat Code Coverage"` |

### Phase 9 完了チェックリスト

- [ ] `dotnet test` — 全テスト合格
- [ ] テスト命名: `Should_XXX_When_YYY` パターン
- [ ] AAA パターン: 全テストメソッドで Arrange / Act / Assert が分離
- [ ] Shouldly アサーション: `ShouldBe()`, `ShouldNotBeNull()`, `ShouldContain()`
- [ ] `[Trait("Category", "Unit|Integration|Security")]` が全テストに付与
- [ ] 異常系テスト: PointAccountNotFoundException, InsufficientPointsException, DuplicateTransactionException, PointExpiredException, ConcurrencyException
- [ ] Saga べき等性テスト: 同一 orderId + type の重複リクエストが正しく処理されること
- [ ] ポイント計算テスト: ティア別還元率 × キャンペーン倍率の全組合せ
- [ ] 端数切り捨て: `Math.Floor` による切り捨て結果の検証
- [ ] FIFO ポイント消費テスト: expires_at 昇順で消費されること、全量消費時に status=CONSUMED になること
- [ ] 監査ログテスト: AdjustPointsAsync が PointAuditLog レコードを正しく作成すること
- [ ] DSR 匿名化テスト: AnonymizeUserDataAsync が UserId を匿名化ハッシュに置換、残高 0 にリセットすること
- [ ] UserDeletedEventConsumer テスト: user.deleted イベント受信で AnonymizeUserDataAsync が呼ばれること
- [ ] OrderEventConsumer テスト: order.created はログ記録のみ（ポイント付与を行わないこと）、order.cancelled でポイント返却
- [ ] PaymentRefundedEventConsumer テスト: 返金時のポイント減算 + 冪等性チェック
- [ ] CancellationToken テスト: キャンセル時に `OperationCanceledException` が発生
- [ ] セキュリティテスト: 401/403 応答の検証
- [ ] Testcontainers.PostgreSql: Repository テストで実 PostgreSQL を使用
- [ ] モック数: 1 テストクラスにつき 5 個以下（SRP 遵守の指標）
- [ ] `dotnet test --collect:"XPlat Code Coverage"` — 分岐カバレッジ 80% 以上
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 10: 可観測性（Observability）

### 目的

AGENTS.md §11.2 準拠の可観測性（分散トレーシング・メトリクス・構造化ログ・ヘルスチェック）を統合する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `PointService/Program.cs` | 更新 | OpenTelemetry・Serilog・ヘルスチェック設定 |
| 2 | `PointService/Infrastructure/Telemetry/PointServiceMetrics.cs` | 作成 | カスタムメトリクス |
| 3 | `PointService/Infrastructure/Middleware/CorrelationIdMiddleware.cs` | 更新 | Correlation ID 付与 |

### 10.1 Serilog 構造化ログ

```csharp
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "PointService")
        .WriteTo.Console(new CompactJsonFormatter()));
```

### 10.2 OpenTelemetry 設定

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddGrpcClientInstrumentation()
        .AddSource("PointService.*"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("PointService.Metrics"));
```

### 10.3 カスタムメトリクス

```csharp
public class PointServiceMetrics
{
    private readonly Counter<long> _pointsAwarded;
    private readonly Counter<long> _pointsRedeemed;
    private readonly Counter<long> _pointsExpired;
    private readonly Histogram<double> _reserveLatency;
    private readonly Counter<long> _tierUpgraded;
    private readonly Counter<long> _tierDowngraded;

    public PointServiceMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("PointService.Metrics");

        _pointsAwarded = meter.CreateCounter<long>(
            "points.awarded.total",
            unit: "points",
            description: "Total points awarded");

        _pointsRedeemed = meter.CreateCounter<long>(
            "points.redeemed.total",
            unit: "points",
            description: "Total points redeemed");

        _pointsExpired = meter.CreateCounter<long>(
            "points.expired.total",
            unit: "points",
            description: "Total points expired");

        _reserveLatency = meter.CreateHistogram<double>(
            "points.reserve.duration",
            unit: "ms",
            description: "Point reservation latency");

        // 設計書 §14.1 追加メトリクス
        _tierUpgraded = meter.CreateCounter<long>(
            "tier.upgrade.total",
            description: "Total tier upgrades");

        _tierDowngraded = meter.CreateCounter<long>(
            "tier.downgrade.total",
            description: "Total tier downgrades");

        // ObservableGauge: points.balance.average — 全ユーザー平均残高
        // ObservableGauge: tier.distribution — ティア別ユーザー分布
        // これらは IPointAnalyticsService から定期取得して記録する
    }

    public void RecordPointsAwarded(long points) => _pointsAwarded.Add(points);
    public void RecordPointsRedeemed(long points) => _pointsRedeemed.Add(points);
    public void RecordPointsExpired(long points) => _pointsExpired.Add(points);
    public void RecordReserveLatency(double latencyMs) => _reserveLatency.Record(latencyMs);
    public void RecordTierUpgrade() => _tierUpgraded.Add(1);
    public void RecordTierDowngrade() => _tierDowngraded.Add(1);
}
```

### 10.3.1 アラート条件（設計書 §14.3）

| 条件 | 閾値 | アクション |
|---|---|---|
| ポイント付与失敗率 | 5 分間で 10% 超過 | アラート通知 |
| 残高マイナス検出 | `available_points < 0` | 即時調査 |
| 大量ポイント付与 | 1 回で 100,000 pt 以上 | 管理者確認 |
| Outbox 未処理蓄積 | PENDING が 1,000 件超過 | キュー調査 |

### 10.4 ヘルスチェック

```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql", tags: ["ready"])
    .AddRedis(redisConnectionString, name: "redis", tags: ["ready"]);

// Liveness: アプリケーション生存確認（依存チェックなし = 常に 200）
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

// Readiness: 依存サービス（PostgreSQL, Redis）の準備完了確認
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();
```

### 10.5 Correlation ID ミドルウェア

```csharp
public class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-Id"]
            .FirstOrDefault() ?? Guid.NewGuid().ToString();
        context.Response.Headers.Append("X-Correlation-Id", correlationId);
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(
        this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
```

### Phase 10 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] Serilog: `CompactJsonFormatter` で JSON 出力
- [ ] Serilog: `ServiceName = "PointService"` プロパティ付与
- [ ] OpenTelemetry: Tracing + Metrics 設定
- [ ] OpenTelemetry: EF Core Instrumentation が含まれること
- [ ] カスタムメトリクス: `points.awarded.total`, `points.redeemed.total`, `points.expired.total`, `points.reserve.duration`, `tier.upgrade.total`, `tier.downgrade.total`
- [ ] ObservableGauge: `points.balance.average`（全ユーザー平均残高）、`tier.distribution`（ティア別ユーザー分布）が定義されていること
- [ ] アラート条件: ポイント付与失敗率・残高マイナス検出・大量ポイント付与・Outbox 未処理蓄積の 4 条件がログ/メトリクスで検知可能であること
- [ ] CorrelationId: 全リクエストに `X-Correlation-Id` ヘッダー付与
- [ ] CorrelationId: `LogContext.PushProperty` でログに含まれること
- [ ] ヘルスチェック: `/health`（Liveness）+ `/health/ready`（Readiness）
- [ ] ヘルスチェック: PostgreSQL + Redis の疎通確認が `ready` タグ
- [ ] ヘルスチェック: `.AllowAnonymous()` で認証不要
- [ ] 構造化ログ: 文字列補間ではなくメッセージテンプレート形式（`{Placeholder}`）
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 11: Docker / デプロイ

### 目的

コンテナ化・.NET Aspire オーケストレーション・ミドルウェアパイプライン最終構成を完了する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `PointService/Dockerfile` | 更新 | 最終 Dockerfile |
| 2 | `PointService/.dockerignore` | 作成 | ビルド除外ファイル |
| 3 | `AppHost/Program.cs` | 更新 | PointService の Aspire 登録 |
| 4 | `PointService/Program.cs` | 更新 | 最終ミドルウェアパイプライン |

### 11.1 最終 Dockerfile

```dockerfile
# ── Build Stage ──
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["PointService/PointService.csproj", "PointService/"]
RUN dotnet restore "PointService/PointService.csproj"

COPY . .
WORKDIR "/src/PointService"
RUN dotnet publish "PointService.csproj" -c Release -o /app/publish --no-restore

# ── Runtime Stage ──
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN groupadd -r skishop && useradd -r -g skishop -d /app -s /sbin/nologin skishop
COPY --from=build --chown=skishop:skishop /app/publish .

USER skishop

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_gcServer=1

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "PointService.dll"]
```

### 11.2 .dockerignore

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
**/*Tests/
```

### 11.3 .NET Aspire AppHost 登録

```csharp
// AppHost/Program.cs
var pointService = builder.AddProject<Projects.PointService>("point-service")
    .WithReference(postgres)
    .WithReference(redis)
    .WithReference(kafka);
```

### 11.4 最終ミドルウェアパイプライン（AGENTS.md §11.3 厳守）

```csharp
var app = builder.Build();

// 1. 例外ハンドラー（最も外側で全例外をキャッチ）
app.UseExceptionHandler();

// 2. セキュリティヘッダー
app.UseHsts();
app.UseHttpsRedirection();
app.UseSecurityHeaders();

// 3. Correlation ID ミドルウェア
app.UseCorrelationId();

// 4. Serilog リクエストログ
app.UseSerilogRequestLogging();

// 5. CORS（認証より前に配置）
app.UseCors();

// 6. 認証・認可（この順序は絶対）
app.UseAuthentication();
app.UseAuthorization();

// 7. レート制限
app.UseRateLimiter();

// 8. エンドポイントマッピング
app.MapPointEndpoints();
app.MapTierEndpoints();
app.MapGrpcService<PointGrpcService>()
    .RequireAuthorization("InternalServiceOnly");
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

- [ ] `docker build` — イメージビルド成功
- [ ] Dockerfile: マルチステージビルド（SDK → aspnet）
- [ ] Dockerfile: `USER skishop`（非 root 実行）
- [ ] Dockerfile: `HEALTHCHECK` 定義
- [ ] Dockerfile: ベースイメージに固定バージョン（`:10.0`、`latest` 禁止）
- [ ] .dockerignore: `bin/`, `obj/`, `.git/`, `*.md`, テストプロジェクト除外
- [ ] AppHost: `WithReference(postgres, redis, kafka)` で PointService 登録
- [ ] ミドルウェア順序: `UseExceptionHandler` → `UseHsts` → `UseHttpsRedirection` → `UseCorrelationId` → `UseSerilogRequestLogging` → `UseCors` → `UseAuthentication` → `UseAuthorization` → `UseRateLimiter` → エンドポイント
- [ ] `UseAuthentication()` が `UseAuthorization()` の前に配置されていること
- [ ] DI 登録: `IPointAuditLogRepository` / `PointAuditLogRepository` が Scoped 登録されていること
- [ ] DI 登録: `IOutboxEventRepository` / `OutboxEventRepository` が Scoped 登録されていること
- [ ] DI 登録: 全 BackgroundService（`OutboxPublisher`, `PointExpirationChecker`, `MemberRankUpdatedEventConsumer`, `OrderCancelledEventConsumer`, `OrderEventConsumer`, `UserDeletedEventConsumer`, `PaymentRefundedEventConsumer`）が `AddHostedService` で登録されていること
- [ ] gRPC: `MapGrpcService<PointGrpcService>()` に `.RequireAuthorization("InternalServiceOnly")` が付与されていること
- [ ] `dotnet publish -c Release` — 成功
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## フェーズ依存関係図

```
Phase 1: 基盤構築
    ↓
Phase 2: エンティティ定義
    ↓
Phase 3: Repository 実装
    ↓
Phase 4: Service 実装
    ↓
Phase 5: Endpoints 実装 ←─── Phase 8: 認証・認可（並行実装可能）
    ↓
Phase 6: Kafka イベント連携
    ↓
Phase 7: Redis キャッシュ連携
    ↓
Phase 9: テスト実装
    ↓
Phase 10: 可観測性
    ↓
Phase 11: Docker / デプロイ
```

---

## 横断的コンプライアンスチェックリスト

### 禁止パターン検出コマンド

```bash
# 1. Console.WriteLine の使用チェック
grep -r "Console\.Write" --include="*.cs" PointService/

# 2. 秘密情報のハードコードチェック
grep -r "Password\s*=\s*\"" --include="*.cs" PointService/
grep -r "ApiKey\s*=\s*\"" --include="*.cs" PointService/
grep -r "Token\s*=\s*\"" --include="*.cs" PointService/

# 3. SQL 文字列結合チェック
grep -r "FromSqlRaw.*\+" --include="*.cs" PointService/
grep -r "FromSqlRaw.*\$\"" --include="*.cs" PointService/

# 4. .Result / .Wait() チェック（デッドロックリスク）
grep -rP "\.(Result|Wait)\(\)" --include="*.cs" PointService/

# 5. Thread.Sleep() チェック
grep -r "Thread\.Sleep" --include="*.cs" PointService/

# 6. new HttpClient() チェック
grep -r "new HttpClient()" --include="*.cs" PointService/

# 7. DateTime.Now チェック（DateTime.UtcNow / TimeProvider を使用）
grep -r "DateTime\.Now[^U]" --include="*.cs" PointService/

# 8. プロパティインジェクションチェック
grep -r "\[Inject\]" --include="*.cs" PointService/

# 9. catch (Exception) { } チェック（例外握りつぶし）
grep -rn "catch.*Exception.*{.*}" --include="*.cs" PointService/

# 10. プレリリースパッケージチェック
grep -r "\-preview\|\-beta\|\-rc" --include="*.csproj" PointService/
```

### コーディング規約遵守チェック

| # | チェック項目 | 確認方法 |
|---|---|---|
| 1 | `CancellationToken ct = default` が全 async メソッドに含まれるか | grep + コードレビュー |
| 2 | `AsNoTracking()` が読み取り専用クエリに使用されているか | grep |
| 3 | record 型で DTO が定義されているか | grep `public record` |
| 4 | primary constructor が Service / Repository で使用されているか | grep |
| 5 | `ILogger<T>` + メッセージテンプレートが使用されているか | grep |
| 6 | `[Column("snake_case")]` が全プロパティに付与されているか | コードレビュー |
| 7 | コレクションナビゲーションが `= []` で初期化されているか | grep |
| 8 | **TODO/FIXME/HACK コメント残存チェック** | `grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 9 | **Mock/Stub/仮実装の残存チェック** | `grep -rn "NotImplementedException\|\"dummy\"\|\"test\"\|\"xxx\"" --include="*.cs" src/` で本番コードに 0 件 |
| 10 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディや未実装例外スローが本番コードに残存していないこと |

### テスト規約遵守チェック

| # | チェック項目 | 確認方法 |
|---|---|---|
| 1 | テスト命名: `Should_XXX_When_YYY` | grep |
| 2 | AAA パターン: `// Arrange`, `// Act`, `// Assert` コメント | grep |
| 3 | `[Trait("Category", "...")]` が全テストに付与 | grep |
| 4 | Shouldly アサーション使用 | grep `ShouldBe\|ShouldNotBeNull` |
| 5 | アサーションなしテストが存在しないこと | コードレビュー |
| 6 | **TODO/FIXME/HACK コメント残存チェック** | `grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 7 | **Mock/Stub/仮実装の残存チェック** | `grep -rn "NotImplementedException\|\"dummy\"\|\"test\"\|\"xxx\"" --include="*.cs" src/` で本番コードに 0 件 |
| 8 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディや未実装例外スローが本番コードに残存していないこと |

### セキュリティ規約遵守チェック

| # | チェック項目 | 確認方法 |
|---|---|---|
| 1 | 全エンドポイントに認可設定（`RequireAuthorization` / `AllowAnonymous`） | コードレビュー |
| 2 | 一般ユーザー向け API の IDOR 防止 | コードレビュー |
| 3 | FluentValidation による入力検証 | grep `IValidator<T>` |
| 4 | セキュリティヘッダー 5 種設定 | コードレビュー |
| 5 | `DetailedErrors: false` | grep appsettings |
| 6 | PII ログ禁止 | コードレビュー |
| 7 | **TODO/FIXME/HACK コメント残存チェック** | `grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 8 | **Mock/Stub/仮実装の残存チェック** | `grep -rn "NotImplementedException\|\"dummy\"\|\"test\"\|\"xxx\"" --include="*.cs" src/` で本番コードに 0 件 |
| 9 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディや未実装例外スローが本番コードに残存していないこと |

---

## 付録: PointService ディレクトリ最終構成

```
PointService/
├── PointService.csproj
├── Program.cs
├── Dockerfile
├── .dockerignore
├── appsettings.json
├── appsettings.Development.json
├── appsettings.Production.json
├── Endpoints/
│   ├── PointEndpoints.cs
│   └── TierEndpoints.cs
├── GrpcServices/
│   └── PointGrpcService.cs
├── Services/
│   ├── Interfaces/
│   │   ├── IPointService.cs
│   │   ├── ITierService.cs
│   │   ├── IPointCalculator.cs
│   │   ├── IPointExpirationService.cs
│   │   ├── IPointAnalyticsService.cs
│   │   ├── IPointCacheService.cs
│   │   └── IOutboxEventService.cs
│   ├── PointService.cs
│   ├── TierService.cs
│   ├── PointCalculator.cs
│   ├── PointExpirationService.cs
│   ├── PointAnalyticsService.cs
│   ├── PointCacheService.cs
│   └── OutboxEventService.cs
├── Repositories/
│   ├── Interfaces/
│   │   ├── IPointAccountRepository.cs
│   │   ├── IPointTransactionRepository.cs
│   │   ├── IPointExpiryRepository.cs
│   │   ├── IPointRuleRepository.cs
│   │   ├── ITierDefinitionRepository.cs
│   │   ├── IOutboxEventRepository.cs
│   │   └── IPointAuditLogRepository.cs
│   ├── PointAccountRepository.cs
│   ├── PointTransactionRepository.cs
│   ├── PointExpiryRepository.cs
│   ├── PointRuleRepository.cs
│   ├── TierDefinitionRepository.cs
│   ├── OutboxEventRepository.cs
│   └── PointAuditLogRepository.cs
├── Models/
│   ├── PointAccount.cs
│   ├── PointTransaction.cs
│   ├── PointExpiry.cs
│   ├── PointRule.cs
│   ├── PointCampaign.cs
│   ├── PointConversionRate.cs
│   ├── TierDefinition.cs
│   ├── OutboxEvent.cs
│   └── PointAuditLog.cs
├── DTOs/
│   ├── Requests/
│   │   ├── AdjustPointsRequest.cs
│   │   ├── ReservePointsRequest.cs
│   │   ├── ConfirmPointsRequest.cs
│   │   ├── ReleasePointsRequest.cs
│   │   ├── AwardPointsRequest.cs
│   │   ├── UpdateTierRequest.cs
│   │   └── PaginationQuery.cs
│   └── Responses/
│       ├── PointBalanceResponse.cs
│       ├── PointTransactionResponse.cs
│       ├── ExpiringPointsResponse.cs
│       ├── TierInfoResponse.cs
│       ├── PointAnalyticsResponse.cs
│       ├── ReservePointsResponse.cs
│       └── PagedResponse.cs
├── Configurations/
│   ├── PointSettings.cs
│   ├── KafkaSettings.cs
│   └── RedisSettings.cs
├── Validators/
│   ├── AdjustPointsRequestValidator.cs
│   ├── ReservePointsRequestValidator.cs
│   └── AwardPointsRequestValidator.cs
├── Exceptions/
│   ├── PointException.cs
│   ├── PointAccountNotFoundException.cs
│   ├── InsufficientPointsException.cs
│   ├── PointExpiredException.cs
│   ├── DuplicateTransactionException.cs
│   ├── ConcurrencyException.cs
│   ├── UnauthorizedException.cs
│   └── ForbiddenException.cs
├── Events/
│   ├── PointsEarnedEvent.cs
│   ├── PointsRedeemedEvent.cs
│   ├── PointsReservedEvent.cs
│   ├── PointsReleasedEvent.cs
│   ├── PointsExpiredEvent.cs
│   ├── OrderCancelledEvent.cs
│   ├── UserRegisteredEvent.cs
│   ├── UserDeletedEvent.cs
│   ├── PaymentRefundedEvent.cs
│   └── MemberRankUpdatedEvent.cs
├── BackgroundServices/
│   ├── OutboxPublisher.cs
│   └── PointExpirationChecker.cs
├── Consumers/
│   ├── OrderEventConsumer.cs
│   ├── UserRegisteredEventConsumer.cs
│   ├── UserDeletedEventConsumer.cs
│   ├── PaymentRefundedEventConsumer.cs
│   └── MemberRankEventConsumer.cs
├── Infrastructure/
│   ├── Persistence/
│   │   └── AppDbContext.cs
│   ├── Middleware/
│   │   ├── SecurityHeadersMiddleware.cs
│   │   ├── SecurityHeadersMiddlewareExtensions.cs
│   │   ├── CorrelationIdMiddleware.cs
│   │   └── CorrelationIdMiddlewareExtensions.cs
│   ├── Telemetry/
│   │   └── PointServiceMetrics.cs
│   └── ExceptionHandlers/
│       └── GlobalExceptionHandler.cs
└── Migrations/
    └── (EF Core 自動生成)

PointService.Tests/
├── PointService.Tests.csproj
├── Unit/
│   ├── Services/
│   │   ├── PointServiceTests.cs
│   │   ├── PointCalculatorTests.cs
│   │   ├── PointExpirationServiceTests.cs
│   │   └── TierServiceTests.cs
│   ├── Validators/
│   │   ├── AdjustPointsRequestValidatorTests.cs
│   │   ├── ReservePointsRequestValidatorTests.cs
│   │   └── AwardPointsRequestValidatorTests.cs
│   ├── Consumers/
│   │   ├── MemberRankEventConsumerTests.cs
│   │   └── UserRegisteredEventConsumerTests.cs
│   └── BackgroundServices/
│       └── OutboxPublisherTests.cs
├── Integration/
│   ├── Endpoints/
│   │   ├── PointEndpointsTests.cs
│   │   └── TierEndpointsTests.cs
│   ├── Repositories/
│   │   ├── PointAccountRepositoryTests.cs
│   │   └── PointTransactionRepositoryTests.cs
│   └── GrpcServices/
│       └── PointGrpcServiceTests.cs
├── Security/
│   └── AuthorizationTests.cs
├── Fixtures/
│   ├── PostgresFixture.cs
│   └── TestAuthHandler.cs
└── Helpers/
    └── FakeTimeProviderHelper.cs
```

---

> **本ドキュメントは設計書 (`design-docs/point-service-design.md`) の実装ガイドであり、設計判断の SSOT（Single Source of Truth）は設計書本体に存在する。設計変更が必要な場合は設計書を先に更新し、本実装計画に反映すること。**
