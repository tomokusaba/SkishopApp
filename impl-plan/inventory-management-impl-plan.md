# InventoryManagementService フェーズ別実装計画書

> **対象サービス**: InventoryManagementService（在庫管理サービス）
> **ポート**: 5003
> **DB**: PostgreSQL（inventorydb）
> **キャッシュ**: Redis（StackExchange.Redis）
> **メッセージング**: Apache Kafka（Confluent.Kafka + Outbox パターン）
> **gRPC**: 在庫予約/解放（Saga ステップ 2）
> **設計書**: `design-docs/inventory-management-design.md`
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

InventoryManagementService プロジェクトの骨格を構築する。ビルド可能な最小構成（.csproj, Program.cs, appsettings, Dockerfile）を作成し、以降のフェーズの土台とする。

### 作成ファイル一覧

| # | ファイルパス | 役割 |
|---|------------|------|
| 1 | `InventoryManagementService/InventoryManagementService.csproj` | プロジェクト定義（EF Core, Redis, Kafka, Serilog, OpenTelemetry, gRPC 等） |
| 2 | `InventoryManagementService/Program.cs` | エントリポイントスケルトン（最小構成） |
| 3 | `InventoryManagementService/appsettings.json` | 共通設定（安全なデフォルト値のみ、秘密情報禁止） |
| 4 | `InventoryManagementService/appsettings.Development.json` | 開発環境設定 |
| 5 | `InventoryManagementService/appsettings.Production.json` | 本番環境設定（環境変数参照のみ） |
| 6 | `InventoryManagementService/Dockerfile` | マルチステージビルド + 非 root 実行 |
| 7 | `InventoryManagementService/.dockerignore` | ビルド不要ファイルの除外 |

### 1.1 InventoryManagementService.csproj

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
    <PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="10.*" />

    <!-- gRPC -->
    <PackageReference Include="Grpc.AspNetCore" Version="2.*" />

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
    <PackageReference Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.*" />

    <!-- ヘルスチェック -->
    <PackageReference Include="AspNetCore.HealthChecks.NpgSql" Version="9.*" />
    <PackageReference Include="AspNetCore.HealthChecks.Redis" Version="9.*" />
    <PackageReference Include="AspNetCore.HealthChecks.Kafka" Version="9.*" />

    <!-- Azure Blob Storage（画像管理） -->
    <PackageReference Include="Azure.Storage.Blobs" Version="12.*" />
    <PackageReference Include="Azure.Identity" Version="1.*" />
  </ItemGroup>
</Project>
```

### 1.2 ディレクトリ構造

```
InventoryManagementService/
├── InventoryManagementService.csproj
├── Program.cs
├── Endpoints/
│   ├── ProductEndpoints.cs
│   ├── CategoryEndpoints.cs
│   ├── InventoryEndpoints.cs
│   ├── PriceEndpoints.cs
│   ├── ReviewEndpoints.cs
│   └── SizeGuideEndpoints.cs
├── Services/
│   ├── Interfaces/
│   │   ├── IProductService.cs
│   │   ├── ICategoryService.cs
│   │   ├── IInventoryService.cs
│   │   ├── IPriceService.cs
│   │   ├── IReviewService.cs
│   │   ├── ISizeGuideService.cs
│   │   └── IEventPublisherService.cs
│   ├── ProductService.cs
│   ├── CategoryService.cs
│   ├── InventoryService.cs
│   ├── PriceService.cs
│   ├── ReviewService.cs
│   ├── SizeGuideService.cs
│   └── EventPublisherService.cs
├── Repositories/
│   ├── Interfaces/
│   │   ├── IProductRepository.cs
│   │   ├── ICategoryRepository.cs
│   │   ├── IInventoryRepository.cs
│   │   ├── IPriceRepository.cs
│   │   ├── IReviewRepository.cs
│   │   ├── ISizeGuideRepository.cs
│   │   └── IImageRepository.cs
│   ├── ProductRepository.cs
│   ├── CategoryRepository.cs
│   ├── InventoryRepository.cs
│   ├── PriceRepository.cs
│   ├── ReviewRepository.cs
│   ├── SizeGuideRepository.cs
│   └── ImageRepository.cs
├── Models/
│   ├── Product.cs
│   ├── Category.cs
│   ├── Inventory.cs
│   ├── Price.cs
│   ├── PriceHistory.cs
│   ├── ProductImage.cs
│   ├── Review.cs
│   ├── ReviewResponse.cs
│   ├── Supplier.cs
│   ├── ProductSupplier.cs
│   ├── SizeGuide.cs
│   └── OutboxEvent.cs
├── DTOs/
│   ├── Requests/
│   │   ├── ProductCreateRequest.cs
│   │   ├── ProductUpdateRequest.cs
│   │   ├── CategoryCreateRequest.cs
│   │   ├── CategoryUpdateRequest.cs
│   │   ├── StockInRequest.cs
│   │   ├── StockOutRequest.cs
│   │   ├── InventoryUpdateRequest.cs
│   │   ├── PriceCreateRequest.cs
│   │   ├── PriceUpdateRequest.cs
│   │   ├── ReviewCreateRequest.cs
│   │   ├── ReviewStatusUpdateRequest.cs
│   │   ├── ReviewResponseCreateRequest.cs
│   │   ├── SizeGuideCreateRequest.cs
│   │   ├── SizeGuideUpdateRequest.cs
│   │   ├── ReserveItemDto.cs
│   │   ├── ProductSearchCriteria.cs
│   │   ├── PaginationParams.cs
│   │   └── ProductSearchParams.cs
│   └── Responses/
│       ├── ProductDto.cs
│       ├── ProductDetailDto.cs
│       ├── CategoryDto.cs
│       ├── InventoryDto.cs
│       ├── InventoryStatusDto.cs
│       ├── PriceDto.cs
│       ├── PriceHistoryDto.cs
│       ├── ReviewDto.cs
│       ├── ReviewResponseDto.cs
│       ├── ProductImageDto.cs
│       ├── SizeGuideDto.cs
│       └── PaginatedResult.cs
├── Configurations/
│   ├── CacheConfig.cs
│   └── KafkaConfig.cs
├── Validators/
│   ├── ProductCreateRequestValidator.cs
│   ├── PriceCreateRequestValidator.cs
│   ├── ReviewCreateRequestValidator.cs
│   ├── CategoryCreateRequestValidator.cs
│   ├── StockInRequestValidator.cs
│   ├── SizeGuideCreateRequestValidator.cs
│   └── ImageUploadRequestValidator.cs
├── Events/
│   ├── ProductCreatedEvent.cs
│   ├── ProductUpdatedEvent.cs
│   ├── ProductDeletedEvent.cs
│   ├── InventoryReservedEvent.cs
│   ├── InventoryReleasedEvent.cs
│   ├── InventoryUpdatedEvent.cs
│   ├── StockDepletedEvent.cs
│   ├── LowStockAlertEvent.cs
│   ├── PriceUpdatedEvent.cs
│   ├── ReviewCreatedEvent.cs
│   ├── ReviewApprovedEvent.cs
│   ├── OrderCreatedEvent.cs
│   ├── OrderCompletedEvent.cs
│   └── OrderCancelledEvent.cs
├── Exceptions/
│   ├── InventoryException.cs
│   ├── ResourceNotFoundException.cs
│   ├── InsufficientStockException.cs
│   └── DuplicateResourceException.cs
├── GrpcServices/
│   └── InventoryGrpcService.cs
├── BackgroundServices/
│   ├── OutboxPublisher.cs
│   ├── InventoryReservationCleanupService.cs
│   ├── OrderCreatedConsumer.cs
│   ├── OrderCompletedConsumer.cs
│   ├── OrderCancelledConsumer.cs
│   ├── UserDeletedConsumer.cs
│   └── CacheWarmupService.cs
├── Infrastructure/
│   ├── Persistence/
│   │   └── AppDbContext.cs
│   └── Middleware/
│       ├── CorrelationIdMiddleware.cs
│       ├── CorrelationIdMiddlewareExtensions.cs
│       ├── SecurityHeadersMiddleware.cs
│       └── SecurityHeadersMiddlewareExtensions.cs
├── Migrations/
├── Protos/
│   └── inventory.proto
├── appsettings.json
├── appsettings.Development.json
├── appsettings.Production.json
├── Dockerfile
└── .dockerignore
```

### 1.3 Program.cs スケルトン

```csharp
var builder = WebApplication.CreateBuilder(args);

// TimeProvider（テスト時の時刻制御用）
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

// 最小ヘルスチェック
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
      "Microsoft.EntityFrameworkCore": "Debug",
      "SkiShop": "Debug"
    }
  },
  "Inventory": {
    "Cache": {
      "Enabled": true,
      "DefaultTtlSeconds": 600,
      "ProductTtlSeconds": 1800,
      "CategoryTtlSeconds": 3600
    },
    "LowStockThreshold": 10,
    "ReservationTimeoutSeconds": 900
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
COPY ["InventoryManagementService/InventoryManagementService.csproj", "InventoryManagementService/"]
RUN dotnet restore "InventoryManagementService/InventoryManagementService.csproj"
COPY . .
WORKDIR "/src/InventoryManagementService"
RUN dotnet publish "InventoryManagementService.csproj" -c Release -o /app/publish --no-restore

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
ENTRYPOINT ["dotnet", "InventoryManagementService.dll"]
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

| # | チェック項目 | 確認コマンド/方法 | 期待結果 |
|---|-----------|----------------|---------|
| 1 | ビルド成功 | `dotnet build InventoryManagementService/` | 警告なし成功 |
| 2 | 起動確認 | `dotnet run --project InventoryManagementService` → `curl localhost:5003/health` | `{"Status":"UP"}` |
| 3 | .csproj 設定 | 目視確認 | `TreatWarningsAsErrors=true`, `Nullable=enable`, `TargetFramework=net10.0` |
| 4 | プレリリース版なし | `grep -i "preview\|beta\|-rc" InventoryManagementService/*.csproj` | 一致なし |
| 5 | 秘密情報なし | `grep -r "Password\|ApiKey\|Secret" InventoryManagementService/appsettings*.json` | 一致なし |
| 6 | セキュリティ設定 | 目視確認 | `DetailedErrors: false`, `AddServerHeader: false` |
| 7 | Dockerfile | 目視確認 | マルチステージビルド、非 root ユーザー、HEALTHCHECK あり、`latest` タグなし |
| 8 | .dockerignore | 目視確認 | `bin/`, `obj/`, `.git/`, `*.md` 除外 |
| 9 | **TODO/FIXME/HACK コメント残存チェック** | `grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと（0 件） |
| 10 | **Mock/Stub/仮実装の残存チェック** | `grep -rn "NotImplementedException\|\"dummy\"\|\"test\"\|\"xxx\"" --include="*.cs" src/` | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, 仮のハードコード値が本番コードに残存していないこと（0 件） |
| 11 | **未実装メソッド・空メソッドチェック** | 目視確認 | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## Phase 2: エンティティ・Value Object・Enum 定義

### 目的

設計書 §4（データモデル）および §5.3（EF Core エンティティ定義）、追記セクション（未定義エンティティ定義）に基づき、全 12 エンティティ、例外クラス階層、DTO（record 型）、AppDbContext を定義する。EF Core マイグレーションでスキーマを生成可能な状態にする。

### 作成ファイル一覧

| # | ファイルパス | 役割 |
|---|------------|------|
| 1 | `InventoryManagementService/Models/Product.cs` | 商品エンティティ（Aggregate Root） |
| 2 | `InventoryManagementService/Models/Category.cs` | カテゴリエンティティ |
| 3 | `InventoryManagementService/Models/Inventory.cs` | 在庫エンティティ |
| 4 | `InventoryManagementService/Models/Price.cs` | 価格エンティティ |
| 5 | `InventoryManagementService/Models/PriceHistory.cs` | 価格履歴エンティティ |
| 6 | `InventoryManagementService/Models/ProductImage.cs` | 商品画像エンティティ |
| 7 | `InventoryManagementService/Models/Review.cs` | レビューエンティティ（Aggregate Root） |
| 8 | `InventoryManagementService/Models/ReviewResponse.cs` | レビュー返信エンティティ |
| 9 | `InventoryManagementService/Models/Supplier.cs` | サプライヤーエンティティ |
| 10 | `InventoryManagementService/Models/ProductSupplier.cs` | 商品・サプライヤー中間テーブル（複合主キー） |
| 11 | `InventoryManagementService/Models/SizeGuide.cs` | サイズガイドエンティティ |
| 12 | `InventoryManagementService/Models/OutboxEvent.cs` | Outbox イベントエンティティ（ADR-0005 準拠） |
| 13 | `InventoryManagementService/Infrastructure/Persistence/AppDbContext.cs` | DbContext（全 12 DbSet + Fluent API + SaveChangesAsync オーバーライド） |
| 14 | `InventoryManagementService/DTOs/Responses/*.cs` | 全レスポンス DTO（record 型） |
| 15 | `InventoryManagementService/DTOs/Requests/*.cs` | 全リクエスト DTO（record 型 + Data Annotations） |
| 16 | `InventoryManagementService/Exceptions/InventoryException.cs` | 基底例外クラス |
| 17 | `InventoryManagementService/Exceptions/ResourceNotFoundException.cs` | リソース未検出例外（HTTP 404） |
| 18 | `InventoryManagementService/Exceptions/InsufficientStockException.cs` | 在庫不足例外（HTTP 422） |
| 19 | `InventoryManagementService/Exceptions/DuplicateResourceException.cs` | リソース重複例外（HTTP 409） |

### 2.1 Product エンティティ（Aggregate Root）

```csharp
[Table("products")]
public class Product
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("sku")]
    [Required]
    [MaxLength(100)]
    public string Sku { get; set; } = string.Empty;

    [Column("name")]
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("brand")]
    [MaxLength(100)]
    public string? Brand { get; set; }

    [Column("attributes", TypeName = "jsonb")]
    public string? Attributes { get; set; }

    [Column("tags")]
    public string[]? Tags { get; set; }

    [Column("category_id")]
    [MaxLength(36)]
    public string? CategoryId { get; set; }

    [Column("weight")]
    public decimal? Weight { get; set; }

    [Column("active")]
    public bool Active { get; set; } = true;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    // ナビゲーションプロパティ（= [] で初期化）
    public Category? Category { get; set; }
    public ICollection<Inventory> Inventories { get; set; } = [];
    public ICollection<Price> Prices { get; set; } = [];
    public ICollection<ProductImage> Images { get; set; } = [];
    public ICollection<Review> Reviews { get; set; } = [];
}
```

### 2.2 Inventory エンティティ

```csharp
[Table("inventory")]
public class Inventory
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("product_id")]
    [Required]
    [MaxLength(36)]
    public string ProductId { get; set; } = string.Empty;

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("reserved_quantity")]
    public int ReservedQuantity { get; set; }

    [Column("location_code")]
    [Required]
    [MaxLength(20)]
    public string LocationCode { get; set; } = string.Empty;

    [Column("status")]
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "IN_STOCK";

    [Column("reorder_point")]
    public int ReorderPoint { get; set; }

    [Column("reserved_at")]
    public DateTimeOffset? ReservedAt { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("created_by")]
    [MaxLength(255)]
    public string? CreatedBy { get; set; }

    [Column("updated_by")]
    [MaxLength(255)]
    public string? UpdatedBy { get; set; }

    // ナビゲーション
    public Product Product { get; set; } = null!;
}
```

### 2.3 OutboxEvent エンティティ（ADR-0005 準拠）

```csharp
[Table("outbox_events")]
public class OutboxEvent
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("event_type")]
    [Required]
    [MaxLength(255)]
    public string EventType { get; set; } = string.Empty;

    [Column("aggregate_id")]
    [Required]
    [MaxLength(36)]
    public string AggregateId { get; set; } = string.Empty;

    [Column("aggregate_type")]
    [Required]
    [MaxLength(100)]
    public string AggregateType { get; set; } = string.Empty;

    [Column("topic")]
    [Required]
    [MaxLength(255)]
    public string Topic { get; set; } = string.Empty;

    [Column("payload")]
    [Required]
    public string Payload { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("published_at")]
    public DateTimeOffset? PublishedAt { get; set; }

    [Column("retry_count")]
    public int RetryCount { get; set; }

    [Column("max_retries")]
    public int MaxRetries { get; set; } = 5;

    [Column("last_error")]
    [MaxLength(2000)]
    public string? LastError { get; set; }

    [Column("status")]
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "PENDING";
}
```

> **注記**: Category, Price, PriceHistory, ProductImage, Review, ReviewResponse, Supplier, ProductSupplier, SizeGuide の各エンティティは設計書の追記セクションに完全な定義があるため、そのまま実装する。全エンティティに `[Table("snake_case")]` と `[Column("snake_case")]` を付与し、コレクションナビゲーションは `= []` で初期化する。

### 2.4 AppDbContext

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider timeProvider) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<Price> Prices => Set<Price>();
    public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ReviewResponse> ReviewResponses => Set<ReviewResponse>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<ProductSupplier> ProductSuppliers => Set<ProductSupplier>();
    public DbSet<SizeGuide> SizeGuides => Set<SizeGuide>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // --- Product ---
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(e => e.Sku).IsUnique();
            entity.HasIndex(e => e.CategoryId);
            entity.HasIndex(e => e.Brand);
            entity.HasIndex(e => e.Active);
            entity.HasIndex(e => e.CreatedAt).IsDescending();
            entity.HasOne(e => e.Category).WithMany(c => c.Products)
                .HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(e => e.Weight).HasPrecision(8, 2);
        });

        // --- Category ---
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasOne(e => e.Parent).WithMany(c => c.Children)
                .HasForeignKey(e => e.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        // --- Inventory ---
        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.LocationCode);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.ProductId, e.LocationCode });
            entity.HasOne(e => e.Product).WithMany(p => p.Inventories)
                .HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_inventory_quantity", "quantity >= 0");
                t.HasCheckConstraint("ck_inventory_reserved_quantity", "reserved_quantity >= 0");
                t.HasCheckConstraint("ck_inventory_quantity_reserved", "quantity >= reserved_quantity");
                t.HasCheckConstraint("ck_inventory_reorder_point", "reorder_point >= 0");
            });
        });

        // --- Price ---
        modelBuilder.Entity<Price>(entity =>
        {
            entity.HasIndex(e => new { e.ProductId, e.IsActive });
            entity.HasIndex(e => new { e.SaleStartDate, e.SaleEndDate });
            entity.HasOne(e => e.Product).WithMany(p => p.Prices)
                .HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.RegularPrice).HasPrecision(12, 2);
            entity.Property(e => e.SalePrice).HasPrecision(12, 2);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_prices_regular_price", "regular_price >= 0");
                t.HasCheckConstraint("ck_prices_sale_price", "sale_price >= 0");
            });
        });

        // --- PriceHistory ---
        modelBuilder.Entity<PriceHistory>(entity =>
        {
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.EffectiveDate);
            entity.HasOne(e => e.Product).WithMany()
                .HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.Price).HasPrecision(12, 2);
            entity.ToTable(t => t.HasCheckConstraint("ck_price_histories_price", "price >= 0"));
        });

        // --- ProductImage ---
        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasIndex(e => e.ProductId);
            entity.HasOne(e => e.Product).WithMany(p => p.Images)
                .HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        // --- Review ---
        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasIndex(e => new { e.ProductId, e.UserId }).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasOne(e => e.Product).WithMany(p => p.Reviews)
                .HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(t => t.HasCheckConstraint("ck_reviews_rating", "rating >= 1 AND rating <= 5"));
        });

        // --- ReviewResponse ---
        modelBuilder.Entity<ReviewResponse>(entity =>
        {
            entity.HasOne(e => e.Review).WithMany(r => r.Responses)
                .HasForeignKey(e => e.ReviewId).OnDelete(DeleteBehavior.Cascade);
        });

        // --- SizeGuide ---
        modelBuilder.Entity<SizeGuide>(entity =>
        {
            entity.HasIndex(e => e.CategoryId);
            entity.HasOne(e => e.Category).WithMany(c => c.SizeGuides)
                .HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        // --- Supplier ---
        modelBuilder.Entity<Supplier>(entity => entity.HasIndex(e => e.Name));

        // --- ProductSupplier（複合主キー） ---
        modelBuilder.Entity<ProductSupplier>(entity =>
        {
            entity.HasKey(e => new { e.ProductId, e.SupplierId });
            entity.HasOne(e => e.Product).WithMany()
                .HasForeignKey(e => e.ProductId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Supplier).WithMany(s => s.ProductSuppliers)
                .HasForeignKey(e => e.SupplierId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.SupplierPrice).HasPrecision(12, 2);
        });

        // --- OutboxEvent ---
        modelBuilder.Entity<OutboxEvent>(entity =>
        {
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            // 部分インデックス: OutboxPublisher が PENDING/FAILED のみポーリング
            entity.HasIndex(e => new { e.Status, e.CreatedAt })
                .HasFilter("status = 'PENDING'")
                .HasDatabaseName("ix_outbox_events_pending");
            entity.HasIndex(e => new { e.Status, e.RetryCount })
                .HasFilter("status = 'FAILED'")
                .HasDatabaseName("ix_outbox_events_failed");
        });

        // --- Inventory: 予約タイムアウト用部分インデックス ---
        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.HasIndex(e => e.ReservedAt)
                .HasFilter("reserved_at IS NOT NULL")
                .HasDatabaseName("ix_inventory_reserved_at_active");
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var entry in ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            if (entry.Metadata.FindProperty("UpdatedAt") is not null)
                entry.Property("UpdatedAt").CurrentValue = now;
            if (entry.State == EntityState.Added && entry.Metadata.FindProperty("CreatedAt") is not null)
                entry.Property("CreatedAt").CurrentValue = now;
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
```

### 2.5 例外クラス階層

```csharp
// 基底例外
public class InventoryException(string errorCode, string message, Dictionary<string, object>? details = null)
    : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
    public Dictionary<string, object> Details { get; } = details ?? [];
}

// 個別例外（primary constructor）
public class ResourceNotFoundException(string resourceType, string resourceId)
    : InventoryException("RESOURCE_NOT_FOUND",
        $"{resourceType} が見つかりません (ID: {resourceId})",
        new Dictionary<string, object> { ["resourceType"] = resourceType, ["resourceId"] = resourceId });

public class InsufficientStockException(string productId, int requested, int available)
    : InventoryException("INV_001", "在庫不足",
        new Dictionary<string, object> { ["productId"] = productId, ["requestedQuantity"] = requested, ["availableQuantity"] = available });

public class DuplicateResourceException(string resourceType, string key, string value)
    : InventoryException("DUPLICATE_RESOURCE",
        $"{resourceType} は既に存在します ({key}: {value})",
        new Dictionary<string, object> { ["resourceType"] = resourceType, [key] = value });
```

### 2.5.1 エラーコード定数（設計書 §10 準拠）

```csharp
public static class ErrorCodes
{
    // 商品系
    public const string ProductNotFound = "PROD_001";
    public const string ProductDuplicate = "PROD_002";
    public const string ProductInactive = "PROD_003";
    public const string ProductValidation = "PROD_004";
    public const string ProductDeleteConflict = "PROD_005";

    // 在庫系
    public const string InsufficientStock = "INV_001";
    public const string ReservationNotFound = "INV_002";
    public const string InventoryConflict = "INV_003";

    // 価格系
    public const string PriceNotFound = "PRICE_001";
    public const string PriceValidation = "PRICE_002";

    // メディア系
    public const string MediaUploadFailed = "MEDIA_001";
    public const string MediaInvalidFormat = "MEDIA_002";
    public const string MediaSizeExceeded = "MEDIA_003";
}
```

### 2.6 レスポンス DTO（record 型）

```csharp
// 代表例 — 全 DTO は設計書の追記セクションに完全定義あり

public record ProductDto(
    string Id, string Sku, string Name, string? Description, string? Brand,
    string? CategoryName, decimal? Weight, bool Active, DateTimeOffset CreatedAt);

public record InventoryDto(
    string Id, string ProductId, int Quantity, int ReservedQuantity,
    int AvailableQuantity, string LocationCode, string Status, int ReorderPoint);

public record PaginatedResult<T>(List<T> Items, long TotalElements, int Page, int Size)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalElements / Size);
    public bool HasNext => Page < TotalPages - 1;
    public bool HasPrevious => Page > 0;
}
```

### 2.7 リクエスト DTO（record 型 + Data Annotations）

```csharp
public record ProductCreateRequest(
    [Required, StringLength(100), RegularExpression(@"^[A-Z]{2,5}-[A-Z0-9]+-\d{3,}$",
        ErrorMessage = "SKU は 'XX-YYYY-NNN' 形式で入力してください")]
    string Sku,
    [Required, StringLength(255)] string Name,
    [StringLength(5000)] string? Description,
    [StringLength(100)] string? Brand,
    [Required] string CategoryId,
    string? Attributes = null, string[]? Tags = null, decimal? Weight = null);

public record StockInRequest(
    [Required] string ProductId,
    [Range(1, int.MaxValue, ErrorMessage = "入庫数量は 1 以上を指定してください")] int Quantity,
    string? ReferenceId = null);

public record StockOutRequest(
    [Required] string ProductId,
    [Range(1, int.MaxValue, ErrorMessage = "出庫数量は 1 以上を指定してください")] int Quantity,
    [Required] string Reason = "ADJUSTMENT");
```

### 2.8 Program.cs 更新（EF Core 登録）

```csharp
// Phase 2 で追加
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});
```

### Phase 2 完了チェックリスト

| # | チェック項目 | 確認コマンド/方法 | 期待結果 |
|---|-----------|----------------|---------|
| 1 | ビルド成功 | `dotnet build` | 警告なし成功 |
| 2 | マイグレーション生成 | `dotnet ef migrations add Initial --project InventoryManagementService` | 成功 |
| 3 | 全 12 エンティティ定義 | 目視確認 | Product, Category, Inventory, Price, PriceHistory, ProductImage, Review, ReviewResponse, Supplier, ProductSupplier, SizeGuide, OutboxEvent |
| 4 | `[Table("snake_case")]` | `grep -r '\[Table(' Models/` | 全エンティティに snake_case テーブル名 |
| 5 | `[Column("snake_case")]` | `grep -r '\[Column(' Models/` | 全プロパティに snake_case カラム名 |
| 6 | コレクション初期化 | `grep -r '= \[\]' Models/` | コレクションナビゲーションが `= []` で初期化 |
| 7 | `DateTimeOffset` 使用 | `grep -r 'DateTime ' Models/` | `DateTime`（非 Offset）が使用されていないこと |
| 8 | CHECK 制約 | AppDbContext.cs 目視確認 | Inventory（4 件）、Price（2 件）、PriceHistory（1 件）、Review（1 件） |
| 9 | 例外クラス | 目視確認 | InventoryException, ResourceNotFoundException, InsufficientStockException, DuplicateResourceException |
| 10 | DTO は record 型 | `grep -r '^public record' DTOs/` | 全 DTO が record 型 |
| 11 | **TODO/FIXME/HACK コメント残存チェック** | `grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと（0 件） |
| 12 | **Mock/Stub/仮実装の残存チェック** | `grep -rn "NotImplementedException\|\"dummy\"\|\"test\"\|\"xxx\"" --include="*.cs" src/` | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, 仮のハードコード値が本番コードに残存していないこと（0 件） |
| 13 | **未実装メソッド・空メソッドチェック** | 目視確認 | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## Phase 3: Repository 層実装

### 目的

設計書 §5.2（Repository インターフェース）および追記セクション（Repository 完全定義）に基づき、全 7 Repository のインターフェースと EF Core 実装を作成する。1 Aggregate Root = 1 Repository の原則を遵守する。

### 作成ファイル一覧

| # | ファイルパス | 役割 |
|---|------------|------|
| 1 | `InventoryManagementService/Repositories/Interfaces/IProductRepository.cs` | 商品リポジトリインターフェース |
| 2 | `InventoryManagementService/Repositories/Interfaces/ICategoryRepository.cs` | カテゴリリポジトリインターフェース |
| 3 | `InventoryManagementService/Repositories/Interfaces/IInventoryRepository.cs` | 在庫リポジトリインターフェース |
| 4 | `InventoryManagementService/Repositories/Interfaces/IPriceRepository.cs` | 価格リポジトリインターフェース |
| 5 | `InventoryManagementService/Repositories/Interfaces/IReviewRepository.cs` | レビューリポジトリインターフェース |
| 6 | `InventoryManagementService/Repositories/Interfaces/ISizeGuideRepository.cs` | サイズガイドリポジトリインターフェース |
| 7 | `InventoryManagementService/Repositories/Interfaces/IImageRepository.cs` | 画像リポジトリインターフェース（Azure Blob Storage） |
| 8-14 | `InventoryManagementService/Repositories/*.cs` | 各リポジトリの EF Core 実装 |

### 3.1 IProductRepository

```csharp
public interface IProductRepository
{
    Task<Product?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<Product?> FindBySkuAsync(string sku, CancellationToken ct = default);
    Task<List<Product>> SearchAsync(ProductSearchCriteria criteria, int page, int size, CancellationToken ct = default);
    Task<long> CountAsync(ProductSearchCriteria criteria, CancellationToken ct = default);
    Task<List<Product>> FindByCategoryIdAsync(string categoryId, int page, int size, CancellationToken ct = default);
    Task<long> CountByCategoryIdAsync(string categoryId, CancellationToken ct = default);
    Task<List<Product>> FindByIdsAsync(List<string> ids, CancellationToken ct = default);
    Task AddAsync(Product product, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

### 3.2 ProductRepository 実装

```csharp
public class ProductRepository(AppDbContext context) : IProductRepository
{
    public async Task<Product?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.Products
            .Include(p => p.Category)
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .Include(p => p.Prices.Where(pr => pr.IsActive))
            .Include(p => p.Inventories)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Product?> FindBySkuAsync(string sku, CancellationToken ct = default)
        => await context.Products.AsNoTracking()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Sku == sku, ct);

    public async Task<List<Product>> SearchAsync(
        ProductSearchCriteria criteria, int page, int size, CancellationToken ct = default)
    {
        var query = BuildSearchQuery(criteria);
        return await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip(page * size).Take(size)
            .Include(p => p.Category)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<long> CountAsync(ProductSearchCriteria criteria, CancellationToken ct = default)
        => await BuildSearchQuery(criteria).LongCountAsync(ct);

    public async Task<List<Product>> FindByIdsAsync(List<string> ids, CancellationToken ct = default)
        => await context.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.Active)
            .Include(p => p.Category)
            .ToListAsync(ct);

    public async Task<List<Product>> FindByCategoryIdAsync(
        string categoryId, int page, int size, CancellationToken ct = default)
        => await context.Products.AsNoTracking()
            .Where(p => p.CategoryId == categoryId && p.Active)
            .OrderByDescending(p => p.CreatedAt)
            .Skip(page * size).Take(size)
            .Include(p => p.Category)
            .ToListAsync(ct);

    public async Task<long> CountByCategoryIdAsync(string categoryId, CancellationToken ct = default)
        => await context.Products.LongCountAsync(p => p.CategoryId == categoryId && p.Active, ct);

    public async Task AddAsync(Product product, CancellationToken ct = default)
        => await context.Products.AddAsync(product, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);

    private IQueryable<Product> BuildSearchQuery(ProductSearchCriteria criteria)
    {
        var query = context.Products.AsNoTracking().Where(p => p.Active);
        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
            query = query.Where(p => p.Name.Contains(criteria.Keyword)
                || (p.Description != null && p.Description.Contains(criteria.Keyword)));
        if (!string.IsNullOrWhiteSpace(criteria.CategoryId))
            query = query.Where(p => p.CategoryId == criteria.CategoryId);
        if (!string.IsNullOrWhiteSpace(criteria.Brand))
            query = query.Where(p => p.Brand == criteria.Brand);
        return query;
    }
}
```

### 3.3 IInventoryRepository

```csharp
public interface IInventoryRepository
{
    Task<Inventory?> FindByProductIdAsync(string productId, CancellationToken ct = default);
    Task<Inventory?> FindByProductIdForUpdateAsync(string productId, CancellationToken ct = default);
    Task<List<Inventory>> FindLowStockAsync(int threshold, int page, int size, CancellationToken ct = default);
    Task<long> CountLowStockAsync(int threshold, CancellationToken ct = default);
    Task<List<Inventory>> FindExpiredReservationsAsync(TimeSpan timeout, CancellationToken ct = default);
    Task<List<Inventory>> FindByProductIdsAsync(List<string> productIds, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

### 3.4 InventoryRepository 実装（SELECT FOR UPDATE 対応）

```csharp
public class InventoryRepository(AppDbContext context) : IInventoryRepository
{
    public async Task<Inventory?> FindByProductIdAsync(string productId, CancellationToken ct = default)
        => await context.Inventories.AsNoTracking()
            .FirstOrDefaultAsync(i => i.ProductId == productId, ct);

    // 悲観的ロック: SELECT FOR UPDATE
    public async Task<Inventory?> FindByProductIdForUpdateAsync(string productId, CancellationToken ct = default)
        => await context.Inventories
            .FromSqlInterpolated($"SELECT * FROM inventory WHERE product_id = {productId} FOR UPDATE")
            .FirstOrDefaultAsync(ct);

    public async Task<List<Inventory>> FindLowStockAsync(
        int threshold, int page, int size, CancellationToken ct = default)
        => await context.Inventories.AsNoTracking()
            .Where(i => i.Quantity <= threshold && i.Quantity > 0)
            .OrderBy(i => i.Quantity)
            .Skip(page * size).Take(size)
            .ToListAsync(ct);

    public async Task<long> CountLowStockAsync(int threshold, CancellationToken ct = default)
        => await context.Inventories.LongCountAsync(i => i.Quantity <= threshold && i.Quantity > 0, ct);

    public async Task<List<Inventory>> FindExpiredReservationsAsync(
        TimeSpan timeout, CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow.Add(-timeout);
        return await context.Inventories
            .Where(i => i.ReservedAt != null && i.ReservedAt < cutoff && i.ReservedQuantity > 0)
            .ToListAsync(ct);
    }

    public async Task<List<Inventory>> FindByProductIdsAsync(
        List<string> productIds, CancellationToken ct = default)
        => await context.Inventories.AsNoTracking()
            .Where(i => productIds.Contains(i.ProductId))
            .ToListAsync(ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
```

### 3.5 Program.cs 更新（Repository DI 登録）

```csharp
// Phase 3 で追加
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<IPriceRepository, PriceRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<ISizeGuideRepository, SizeGuideRepository>();
builder.Services.AddScoped<IImageRepository, ImageRepository>();
```

### Phase 3 完了チェックリスト

| # | チェック項目 | 確認コマンド/方法 | 期待結果 |
|---|-----------|----------------|---------|
| 1 | ビルド成功 | `dotnet build` | 警告なし成功 |
| 2 | 全 7 インターフェース | 目視確認 | IProductRepository, ICategoryRepository, IInventoryRepository, IPriceRepository, IReviewRepository, ISizeGuideRepository, IImageRepository |
| 3 | 全 7 実装クラス | 目視確認 | 対応する実装クラスが存在 |
| 4 | primary constructor | `grep -r 'public class.*Repository(' Repositories/` | 全実装クラスが primary constructor |
| 5 | `CancellationToken ct = default` | `grep -r 'CancellationToken' Repositories/Interfaces/` | 全メソッドに CancellationToken |
| 6 | `AsNoTracking()` | `grep -r 'AsNoTracking' Repositories/` | 読み取り専用クエリで使用 |
| 7 | DI 登録 | Program.cs 目視確認 | 全 Repository が `AddScoped` で登録 |
| 8 | `FromSqlInterpolated` | InventoryRepository.cs 目視確認 | SELECT FOR UPDATE に `FromSqlInterpolated` を使用（`FromSqlRaw` 禁止） |
| 9 | **TODO/FIXME/HACK コメント残存チェック** | `grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと（0 件） |
| 10 | **Mock/Stub/仮実装の残存チェック** | `grep -rn "NotImplementedException\|\"dummy\"\|\"test\"\|\"xxx\"" --include="*.cs" src/` | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, 仮のハードコード値が本番コードに残存していないこと（0 件） |
| 11 | **未実装メソッド・空メソッドチェック** | 目視確認 | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## Phase 4: Service 層実装

### 目的

設計書 §5.2（Service インターフェース）および追記セクション（Service 完全定義）に基づき、全 7 Service のインターフェースとビジネスロジック実装を作成する。

### 作成ファイル一覧

| # | ファイルパス | 役割 |
|---|------------|------|
| 1 | `InventoryManagementService/Services/Interfaces/IProductService.cs` | 商品サービスインターフェース |
| 2 | `InventoryManagementService/Services/Interfaces/ICategoryService.cs` | カテゴリサービスインターフェース |
| 3 | `InventoryManagementService/Services/Interfaces/IInventoryService.cs` | 在庫サービスインターフェース |
| 4 | `InventoryManagementService/Services/Interfaces/IPriceService.cs` | 価格サービスインターフェース |
| 5 | `InventoryManagementService/Services/Interfaces/IReviewService.cs` | レビューサービスインターフェース |
| 6 | `InventoryManagementService/Services/Interfaces/ISizeGuideService.cs` | サイズガイドサービスインターフェース |
| 7 | `InventoryManagementService/Services/Interfaces/IEventPublisherService.cs` | イベント発行サービスインターフェース |
| 8-14 | `InventoryManagementService/Services/*.cs` | 各サービス実装 |
| 15 | `InventoryManagementService/Validators/*.cs` | FluentValidation バリデーター（6 件） |

### 4.1 IProductService

```csharp
public interface IProductService
{
    Task<ProductDto> CreateProductAsync(ProductCreateRequest request, CancellationToken ct = default);
    Task<ProductDto> UpdateAsync(string id, ProductUpdateRequest request, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task<ProductDto?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<ProductDto?> GetBySkuAsync(string sku, CancellationToken ct = default);
    Task<PaginatedResult<ProductDto>> SearchAsync(
        ProductSearchCriteria criteria, int page, int size, CancellationToken ct = default);
    Task<PaginatedResult<ProductDto>> GetByCategoryAsync(
        string categoryId, int page, int size, CancellationToken ct = default);
    Task<List<ProductDto>> GetByIdsAsync(List<string> ids, CancellationToken ct = default);
    Task<ProductImageDto> UploadImageAsync(string productId, IFormFile file, CancellationToken ct = default);
}
```

### 4.2 ProductService 実装

```csharp
public class ProductService(
    IProductRepository productRepository,
    ICategoryRepository categoryRepository,
    IImageRepository imageRepository,
    IEventPublisherService eventPublisher,
    ILogger<ProductService> logger) : IProductService
{
    public async Task<ProductDto> CreateProductAsync(
        ProductCreateRequest request, CancellationToken ct = default)
    {
        // SKU 重複チェック
        var existing = await productRepository.FindBySkuAsync(request.Sku, ct);
        if (existing is not null)
            throw new DuplicateResourceException("Product", "sku", request.Sku);

        // カテゴリ存在チェック
        if (!await categoryRepository.ExistsByIdAsync(request.CategoryId, ct))
            throw new ResourceNotFoundException("Category", request.CategoryId);

        var product = new Product
        {
            Sku = request.Sku,
            Name = request.Name,
            Description = request.Description,
            Brand = request.Brand,
            CategoryId = request.CategoryId,
            Attributes = request.Attributes,
            Tags = request.Tags,
            Weight = request.Weight
        };

        await productRepository.AddAsync(product, ct);

        // Outbox パターンでイベント書き込み
        await eventPublisher.PublishProductEventAsync("ProductCreated", product.Id,
            new ProductCreatedEvent(product.Id, product.Sku, product.Name, product.Brand,
                product.CategoryId, DateTimeOffset.UtcNow), ct);

        await productRepository.SaveChangesAsync(ct);

        logger.LogInformation("商品作成完了: ProductId={ProductId}, SKU={Sku}", product.Id, product.Sku);
        return MapToDto(product);
    }

    public async Task<ProductDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var product = await productRepository.FindByIdAsync(id, ct);
        return product is null ? null : MapToDto(product);
    }

    public async Task<ProductDto?> GetBySkuAsync(string sku, CancellationToken ct = default)
    {
        var product = await productRepository.FindBySkuAsync(sku, ct);
        return product is null ? null : MapToDto(product);
    }

    public async Task<PaginatedResult<ProductDto>> SearchAsync(
        ProductSearchCriteria criteria, int page, int size, CancellationToken ct = default)
    {
        var products = await productRepository.SearchAsync(criteria, page, size, ct);
        var total = await productRepository.CountAsync(criteria, ct);
        return new PaginatedResult<ProductDto>(products.Select(MapToDto).ToList(), total, page, size);
    }

    public async Task<PaginatedResult<ProductDto>> GetByCategoryAsync(
        string categoryId, int page, int size, CancellationToken ct = default)
    {
        var products = await productRepository.FindByCategoryIdAsync(categoryId, page, size, ct);
        var total = await productRepository.CountByCategoryIdAsync(categoryId, ct);
        return new PaginatedResult<ProductDto>(products.Select(MapToDto).ToList(), total, page, size);
    }

    public async Task<List<ProductDto>> GetByIdsAsync(List<string> ids, CancellationToken ct = default)
    {
        var products = await productRepository.FindByIdsAsync(ids, ct);
        return products.Select(MapToDto).ToList();
    }

    public async Task<ProductDto> UpdateAsync(
        string id, ProductUpdateRequest request, CancellationToken ct = default)
    {
        var product = await productRepository.FindByIdAsync(id, ct)
            ?? throw new ResourceNotFoundException("Product", id);

        if (request.Name is not null) product.Name = request.Name;
        if (request.Description is not null) product.Description = request.Description;
        if (request.Brand is not null) product.Brand = request.Brand;
        if (request.CategoryId is not null)
        {
            if (!await categoryRepository.ExistsByIdAsync(request.CategoryId, ct))
                throw new ResourceNotFoundException("Category", request.CategoryId);
            product.CategoryId = request.CategoryId;
        }
        if (request.Attributes is not null) product.Attributes = request.Attributes;
        if (request.Tags is not null) product.Tags = request.Tags;
        if (request.Weight.HasValue) product.Weight = request.Weight;
        if (request.Active.HasValue) product.Active = request.Active.Value;

        await eventPublisher.PublishProductEventAsync("ProductUpdated", product.Id,
            new ProductUpdatedEvent(product.Id, product.Sku, product.Name, product.Brand,
                product.CategoryId, product.Active, DateTimeOffset.UtcNow), ct);

        await productRepository.SaveChangesAsync(ct);
        logger.LogInformation("商品更新完了: ProductId={ProductId}", product.Id);
        return MapToDto(product);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var product = await productRepository.FindByIdAsync(id, ct)
            ?? throw new ResourceNotFoundException("Product", id);

        product.Active = false; // 論理削除

        await eventPublisher.PublishProductEventAsync("ProductDeleted", product.Id,
            new ProductDeletedEvent(product.Id, product.Sku, DateTimeOffset.UtcNow), ct);

        await productRepository.SaveChangesAsync(ct);
        logger.LogInformation("商品論理削除完了: ProductId={ProductId}, SKU={Sku}", product.Id, product.Sku);
    }

    public async Task<ProductImageDto> UploadImageAsync(
        string productId, IFormFile file, CancellationToken ct = default)
    {
        var product = await productRepository.FindByIdAsync(productId, ct)
            ?? throw new ResourceNotFoundException("Product", productId);

        await using var stream = file.OpenReadStream();
        var imageUrl = await imageRepository.UploadAsync(stream, file.FileName, file.ContentType, ct);

        var image = new ProductImage
        {
            ProductId = productId,
            Url = imageUrl,
            Type = "MAIN",
            SortOrder = product.Images.Count
        };
        product.Images.Add(image);
        await productRepository.SaveChangesAsync(ct);

        logger.LogInformation("商品画像アップロード完了: ProductId={ProductId}, ImageId={ImageId}",
            productId, image.Id);
        return new ProductImageDto(image.Id, image.Url, image.ThumbnailUrl, image.Type,
            image.SortOrder, image.AltText);
    }

    private static ProductDto MapToDto(Product p) => new(
        p.Id, p.Sku, p.Name, p.Description, p.Brand,
        p.Category?.Name, p.Weight, p.Active, p.CreatedAt);
}
```

### 4.3 IInventoryService

```csharp
public interface IInventoryService
{
    Task<string> ReserveAsync(string orderId, List<ReserveItemDto> items, CancellationToken ct = default);
    Task ReleaseAsync(string orderId, string reservationId, CancellationToken ct = default);
    Task<InventoryDto> StockInAsync(StockInRequest request, CancellationToken ct = default);
    Task<InventoryDto> StockOutAsync(StockOutRequest request, CancellationToken ct = default);
    Task<InventoryDto?> GetByProductIdAsync(string productId, CancellationToken ct = default);
    Task<List<InventoryDto>> GetByProductIdsAsync(List<string> productIds, CancellationToken ct = default);
    Task<PaginatedResult<InventoryDto>> GetLowStockAsync(
        int threshold, int page, int size, CancellationToken ct = default);
}
```

### 4.4 InventoryService 実装（SELECT FOR UPDATE による在庫引当）

```csharp
public class InventoryService(
    AppDbContext context,
    IInventoryRepository inventoryRepository,
    IEventPublisherService eventPublisher,
    ILogger<InventoryService> logger) : IInventoryService
{
    public async Task<string> ReserveAsync(
        string orderId, List<ReserveItemDto> items, CancellationToken ct = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var item in items)
            {
                // SELECT FOR UPDATE で行ロック取得
                var inventory = await context.Inventories
                    .FromSqlInterpolated(
                        $"SELECT * FROM inventory WHERE product_id = {item.ProductId} FOR UPDATE")
                    .FirstOrDefaultAsync(ct)
                    ?? throw new ResourceNotFoundException("Inventory", item.ProductId);

                var available = inventory.Quantity - inventory.ReservedQuantity;
                if (available < item.Quantity)
                    throw new InsufficientStockException(item.ProductId, item.Quantity, available);

                inventory.ReservedQuantity += item.Quantity;
                inventory.ReservedAt = DateTimeOffset.UtcNow;

                await eventPublisher.PublishInventoryEventAsync("InventoryReserved", item.ProductId,
                    new InventoryReservedEvent(orderId, item.ProductId, item.Quantity,
                        Guid.NewGuid().ToString(), DateTimeOffset.UtcNow), ct);
            }

            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            logger.LogInformation("在庫引当完了: OrderId={OrderId}, Items={ItemCount}", orderId, items.Count);
            return Guid.NewGuid().ToString();
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<InventoryDto> StockInAsync(StockInRequest request, CancellationToken ct = default)
    {
        var inventory = await inventoryRepository.FindByProductIdAsync(request.ProductId, ct)
            ?? throw new ResourceNotFoundException("Inventory", request.ProductId);

        var previousQuantity = inventory.Quantity;
        inventory.Quantity += request.Quantity;
        inventory.Status = inventory.Quantity > inventory.ReorderPoint ? "IN_STOCK" : "LOW_STOCK";

        await eventPublisher.PublishInventoryEventAsync("InventoryUpdated", request.ProductId,
            new InventoryUpdatedEvent(request.ProductId, string.Empty, previousQuantity,
                inventory.Quantity, "STOCK_IN", inventory.LocationCode,
                DateTimeOffset.UtcNow, request.ReferenceId), ct);

        await inventoryRepository.SaveChangesAsync(ct);

        logger.LogInformation("入庫処理完了: ProductId={ProductId}, Quantity={Qty}",
            request.ProductId, request.Quantity);
        return MapToDto(inventory);
    }

    // ReleaseAsync, StockOutAsync, GetByProductIdAsync, GetLowStockAsync は同様のパターンで実装

    /// <summary>在庫ステータスの自動判定メソッド（設計書 追記: ビジネスルール — 在庫ステータス遷移図）</summary>
    public static string DetermineStatus(int quantity, int reservedQuantity, int reorderPoint, string currentStatus)
    {
        if (currentStatus == "DISCONTINUED")
            return "DISCONTINUED"; // 販売終了は管理者のみ変更可能

        return (quantity, reservedQuantity) switch
        {
            (0, _) => "OUT_OF_STOCK",
            (_, > 0) => "RESERVED",
            _ when quantity <= reorderPoint => "LOW_STOCK",
            _ => "IN_STOCK"
        };
    }

    private static InventoryDto MapToDto(Inventory i) => new(
        i.Id, i.ProductId, i.Quantity, i.ReservedQuantity,
        i.Quantity - i.ReservedQuantity, i.LocationCode, i.Status, i.ReorderPoint);
}
```

### 4.5 EventPublisherService（Outbox パターン）

```csharp
public class EventPublisherService(
    AppDbContext context,
    ILogger<EventPublisherService> logger) : IEventPublisherService
{
    public async Task PublishAsync<TEvent>(
        string eventType, string topic, string aggregateId, string aggregateType,
        TEvent payload, CancellationToken ct = default) where TEvent : class
    {
        var outboxEvent = new OutboxEvent
        {
            EventType = eventType,
            Topic = topic,
            AggregateId = aggregateId,
            AggregateType = aggregateType,
            Payload = JsonSerializer.Serialize(payload)
        };
        await context.OutboxEvents.AddAsync(outboxEvent, ct);
        logger.LogInformation("Outbox イベント登録: {EventType}, AggregateId={AggregateId}", eventType, aggregateId);
    }

    public Task PublishProductEventAsync(string eventType, string productId, object payload, CancellationToken ct = default)
        => PublishAsync(eventType, "inventory.products", productId, "Product", payload, ct);

    public Task PublishInventoryEventAsync(string eventType, string productId, object payload, CancellationToken ct = default)
        => PublishAsync(eventType, "inventory.levels", productId, "Inventory", payload, ct);

    public Task PublishPriceEventAsync(string eventType, string productId, object payload, CancellationToken ct = default)
        => PublishAsync(eventType, "inventory.pricing", productId, "Price", payload, ct);
}
```

### 4.6 FluentValidation バリデーター（代表例）

```csharp
public class ProductCreateRequestValidator : AbstractValidator<ProductCreateRequest>
{
    public ProductCreateRequestValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(100)
            .Matches(@"^[A-Z]{2,5}-[A-Z0-9]+-\d{3,}$")
            .WithMessage("SKU は 'XX-YYYY-NNN' 形式で入力してください");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Weight).GreaterThan(0).When(x => x.Weight.HasValue);
    }
}

public class PriceCreateRequestValidator : AbstractValidator<PriceCreateRequest>
{
    public PriceCreateRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.RegularPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SalePrice).LessThan(x => x.RegularPrice)
            .When(x => x.SalePrice.HasValue)
            .WithMessage("セール価格は通常価格より低く設定してください");
        RuleFor(x => x.SaleEndDate).GreaterThan(x => x.SaleStartDate)
            .When(x => x.SaleStartDate.HasValue && x.SaleEndDate.HasValue);
        RuleFor(x => x.CurrencyCode).NotEmpty().MaximumLength(3)
            .Matches("^(JPY|USD|EUR)$");
    }
}
```

### 4.7 Program.cs 更新（Service / Validator DI 登録）

```csharp
// Phase 4 で追加
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IPriceService, PriceService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<ISizeGuideService, SizeGuideService>();
builder.Services.AddScoped<IEventPublisherService, EventPublisherService>();

builder.Services.AddValidatorsFromAssemblyContaining<ProductCreateRequestValidator>();
```

### Phase 4 完了チェックリスト

| # | チェック項目 | 確認コマンド/方法 | 期待結果 |
|---|-----------|----------------|---------|
| 1 | ビルド成功 | `dotnet build` | 警告なし成功 |
| 2 | 全 7 Service インターフェース | 目視確認 | IProductService, ICategoryService, IInventoryService, IPriceService, IReviewService, ISizeGuideService, IEventPublisherService |
| 3 | 全 7 Service 実装 | 目視確認 | 対応する実装クラスが存在 |
| 4 | primary constructor | `grep -r 'public class.*Service(' Services/` | 全実装クラスが primary constructor |
| 5 | `CancellationToken ct = default` | `grep -r 'CancellationToken' Services/Interfaces/` | 全メソッドに CancellationToken |
| 6 | `ILogger<T>` | `grep -r 'ILogger' Services/` | 全 Service で構造化ログ使用 |
| 7 | メッセージテンプレート | `grep -rP '_logger\.Log\w+\(\$"' Services/` | 文字列補間ログなし |
| 8 | FluentValidation | `grep -r 'AbstractValidator' Validators/` | 6 件のバリデーター |
| 9 | DI 登録 | Program.cs 目視確認 | 全 Service が `AddScoped` で登録 |
| 10 | Outbox パターン | EventPublisherService.cs 目視確認 | DB トランザクション内で OutboxEvent に書き込み |
| 11 | **TODO/FIXME/HACK コメント残存チェック** | `grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと（0 件） |
| 12 | **Mock/Stub/仮実装の残存チェック** | `grep -rn "NotImplementedException\|\"dummy\"\|\"test\"\|\"xxx\"" --include="*.cs" src/` | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, 仮のハードコード値が本番コードに残存していないこと（0 件） |
| 13 | **未実装メソッド・空メソッドチェック** | 目視確認 | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## Phase 5: Endpoints 実装

### 目的

設計書 §5.1（API エンドポイント設計）および追記セクション（Endpoint 実装パターン）に基づき、全 6 エンドポイントグループ（Products, Categories, Inventory, Prices, Reviews, SizeGuides）と gRPC サービスを実装する。各エンドポイントは Minimal API パターンで専用クラスに分離し、FluentValidation による入力検証を必須化する。

### 作成ファイル一覧

| # | ファイルパス | 役割 |
|---|------------|------|
| 1 | `InventoryManagementService/Endpoints/ProductEndpoints.cs` | 商品 API（10 エンドポイント: GET×5, POST×3, PUT×1, DELETE×1） |
| 2 | `InventoryManagementService/Endpoints/CategoryEndpoints.cs` | カテゴリ API（6 エンドポイント） |
| 3 | `InventoryManagementService/Endpoints/InventoryEndpoints.cs` | 在庫 API（6 エンドポイント） |
| 4 | `InventoryManagementService/Endpoints/PriceEndpoints.cs` | 価格 API（4 エンドポイント） |
| 5 | `InventoryManagementService/Endpoints/ReviewEndpoints.cs` | レビュー API（6 エンドポイント） |
| 6 | `InventoryManagementService/Endpoints/SizeGuideEndpoints.cs` | サイズガイド API（3 エンドポイント） |
| 7 | `InventoryManagementService/GrpcServices/InventoryGrpcService.cs` | gRPC 在庫予約/解放サービス |
| 8 | `InventoryManagementService/Protos/inventory.proto` | gRPC プロトコル定義 |

### 5.1 ProductEndpoints

```csharp
public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products")
            .WithTags("Products")
            .WithOpenApi();

        // --- Public（AllowAnonymous）---
        group.MapGet("/", GetAllProducts).WithName("GetProducts").AllowAnonymous();
        group.MapGet("/{id}", GetProductById).WithName("GetProductById").AllowAnonymous();
        group.MapGet("/sku/{sku}", GetProductBySku).WithName("GetProductBySku").AllowAnonymous();
        group.MapGet("/search", SearchProducts).WithName("SearchProducts").AllowAnonymous();
        group.MapGet("/category/{categoryId}", GetProductsByCategory)
            .WithName("GetProductsByCategory").AllowAnonymous();
        group.MapPost("/batch", GetProductsBatch).WithName("GetProductsBatch").AllowAnonymous();

        // --- Admin（RequireAuthorization）---
        group.MapPost("/", CreateProduct)
            .RequireAuthorization("AdminOnly")
            .WithName("CreateProduct");
        group.MapPut("/{id}", UpdateProduct)
            .RequireAuthorization("AdminOnly")
            .WithName("UpdateProduct");
        group.MapDelete("/{id}", DeleteProduct)
            .RequireAuthorization("AdminOnly")
            .WithName("DeleteProduct");
        group.MapPost("/{id}/images", UploadProductImage)
            .RequireAuthorization("AdminOnly")
            .WithName("UploadProductImage")
            .DisableAntiforgery();  // multipart/form-data
    }

    private static async Task<IResult> GetAllProducts(
        [AsParameters] PaginationParams query,
        IProductService service,
        CancellationToken ct)
    {
        var criteria = new ProductSearchCriteria();
        return Results.Ok(await service.SearchAsync(criteria, query.Page, query.Size, ct));
    }

    private static async Task<IResult> GetProductById(
        string id,
        IProductService service,
        CancellationToken ct)
        => await service.GetByIdAsync(id, ct) is { } product
            ? Results.Ok(product)
            : Results.NotFound();

    private static async Task<IResult> GetProductBySku(
        string sku,
        IProductService service,
        CancellationToken ct)
        => await service.GetBySkuAsync(sku, ct) is { } product
            ? Results.Ok(product)
            : Results.NotFound();

    private static async Task<IResult> SearchProducts(
        [AsParameters] ProductSearchParams search,
        IProductService service,
        CancellationToken ct)
    {
        var criteria = new ProductSearchCriteria
        {
            Keyword = search.Keyword,
            CategoryId = search.CategoryId,
            Brand = search.Brand
        };
        return Results.Ok(await service.SearchAsync(criteria, search.Page, search.Size, ct));
    }

    private static async Task<IResult> GetProductsByCategory(
        string categoryId,
        [AsParameters] PaginationParams query,
        IProductService service,
        CancellationToken ct)
        => Results.Ok(await service.GetByCategoryAsync(categoryId, query.Page, query.Size, ct));

    private static async Task<IResult> GetProductsBatch(
        [FromBody] List<string> ids,
        IProductService service,
        CancellationToken ct)
    {
        if (ids.Count > 50)
            return Results.BadRequest("一括取得の上限は 50 件です");
        return Results.Ok(await service.GetByIdsAsync(ids, ct));
    }

    private static async Task<IResult> CreateProduct(
        [FromBody] ProductCreateRequest request,
        IValidator<ProductCreateRequest> validator,
        IProductService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var product = await service.CreateProductAsync(request, ct);
        return Results.Created($"/api/products/{product.Id}", product);
    }

    private static async Task<IResult> UpdateProduct(
        string id,
        [FromBody] ProductUpdateRequest request,
        IProductService service,
        CancellationToken ct)
    {
        var product = await service.UpdateAsync(id, request, ct);
        return Results.Ok(product);
    }

    private static async Task<IResult> DeleteProduct(
        string id,
        IProductService service,
        CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> UploadProductImage(
        string id,
        IFormFile file,
        IValidator<IFormFile> validator,
        IProductService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(file, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var image = await service.UploadImageAsync(id, file, ct);
        return Results.Created($"/api/products/{id}/images/{image.Id}", image);
    }
}
```

### 5.2 InventoryEndpoints

```csharp
public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory")
            .WithTags("Inventory")
            .WithOpenApi();

        group.MapGet("/{productId}", GetByProductId).WithName("GetInventoryByProductId");
        group.MapGet("/status", GetByStatus).WithName("GetInventoryByStatus");
        group.MapPost("/batch", GetBatch).WithName("GetInventoryBatch");
        group.MapGet("/low-stock", GetLowStock).WithName("GetLowStock");
        group.MapPost("/stock-in", StockIn)
            .RequireAuthorization("AdminOnly")
            .WithName("StockIn");
        group.MapPost("/stock-out", StockOut)
            .RequireAuthorization("AdminOnly")
            .WithName("StockOut");
    }

    private static async Task<IResult> GetByProductId(
        string productId,
        IInventoryService service,
        CancellationToken ct)
        => await service.GetByProductIdAsync(productId, ct) is { } inventory
            ? Results.Ok(inventory)
            : Results.NotFound();

    private static async Task<IResult> StockIn(
        [FromBody] StockInRequest request,
        IValidator<StockInRequest> validator,
        IInventoryService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var inventory = await service.StockInAsync(request, ct);
        return Results.Ok(inventory);
    }

    private static async Task<IResult> StockOut(
        [FromBody] StockOutRequest request,
        IValidator<StockOutRequest> validator,
        IInventoryService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var inventory = await service.StockOutAsync(request, ct);
        return Results.Ok(inventory);
    }

    private static async Task<IResult> GetLowStock(
        [AsParameters] PaginationParams query,
        IInventoryService service,
        CancellationToken ct)
        => Results.Ok(await service.GetLowStockAsync(10, query.Page, query.Size, ct));

    // GetByStatus, GetBatch は同様のパターンで実装
}
```

### 5.3 CategoryEndpoints

```csharp
public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/categories")
            .WithTags("Categories")
            .WithOpenApi();

        group.MapGet("/", GetAllCategories).WithName("GetCategories");
        group.MapGet("/{id}", GetCategoryById).WithName("GetCategoryById");
        group.MapGet("/{id}/products", GetCategoryProducts).WithName("GetCategoryProducts");
        group.MapPost("/", CreateCategory)
            .RequireAuthorization("AdminOnly").WithName("CreateCategory");
        group.MapPut("/{id}", UpdateCategory)
            .RequireAuthorization("AdminOnly").WithName("UpdateCategory");
        group.MapDelete("/{id}", DeleteCategory)
            .RequireAuthorization("AdminOnly").WithName("DeleteCategory");
    }

    private static async Task<IResult> GetAllCategories(
        ICategoryService service,
        CancellationToken ct)
        => Results.Ok(await service.GetAllAsync(ct));

    private static async Task<IResult> GetCategoryById(
        string id,
        ICategoryService service,
        CancellationToken ct)
        => await service.GetByIdAsync(id, ct) is { } category
            ? Results.Ok(category)
            : Results.NotFound();

    private static async Task<IResult> CreateCategory(
        [FromBody] CategoryCreateRequest request,
        IValidator<CategoryCreateRequest> validator,
        ICategoryService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var category = await service.CreateAsync(request, ct);
        return Results.Created($"/categories/{category.Id}", category);
    }

    private static async Task<IResult> UpdateCategory(
        string id,
        [FromBody] CategoryUpdateRequest request,
        IValidator<CategoryUpdateRequest> validator,
        ICategoryService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var category = await service.UpdateAsync(id, request, ct);
        return Results.Ok(category);
    }

    private static async Task<IResult> DeleteCategory(
        string id,
        ICategoryService service,
        CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> GetCategoryProducts(
        string id,
        [AsParameters] PaginationParams query,
        IProductService productService,
        CancellationToken ct)
        => Results.Ok(await productService.GetByCategoryAsync(id, query.Page, query.Size, ct));
}
```

### 5.4 ReviewEndpoints

```csharp
public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reviews")
            .WithTags("Reviews")
            .WithOpenApi();

        group.MapGet("/product/{productId}", GetByProductId).WithName("GetReviewsByProductId");
        group.MapGet("/{id}", GetById).WithName("GetReviewById");
        group.MapPost("/", CreateReview)
            .RequireAuthorization().WithName("CreateReview");
        group.MapPost("/{id}/response", CreateResponse)
            .RequireAuthorization("AdminOnly").WithName("CreateReviewResponse");
        group.MapPost("/{id}/helpful", MarkHelpful)
            .RequireAuthorization().WithName("MarkReviewHelpful");
        group.MapPut("/{id}/status", UpdateStatus)
            .RequireAuthorization("AdminOnly").WithName("UpdateReviewStatus");
    }

    private static async Task<IResult> CreateReview(
        [FromBody] ReviewCreateRequest request,
        IValidator<ReviewCreateRequest> validator,
        ClaimsPrincipal user,
        IReviewService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException();
        var review = await service.CreateAsync(request, userId, ct);
        return Results.Created($"/reviews/{review.Id}", review);
    }

    // 他のハンドラも同様のパターンで実装
}
```

### 5.5 PriceEndpoints

```csharp
public static class PriceEndpoints
{
    public static void MapPriceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/prices")
            .WithTags("Prices")
            .WithOpenApi();

        group.MapGet("/{productId}", GetByProductId).WithName("GetPriceByProductId");
        group.MapGet("/{productId}/history", GetPriceHistory).WithName("GetPriceHistory");
        group.MapPost("/", CreatePrice)
            .RequireAuthorization("AdminOnly").WithName("CreatePrice");
        group.MapPut("/{id}", UpdatePrice)
            .RequireAuthorization("AdminOnly").WithName("UpdatePrice");
    }

    private static async Task<IResult> GetByProductId(
        string productId,
        IPriceService service,
        CancellationToken ct)
        => await service.GetByProductIdAsync(productId, ct) is { } price
            ? Results.Ok(price)
            : Results.NotFound();

    private static async Task<IResult> CreatePrice(
        [FromBody] PriceCreateRequest request,
        IValidator<PriceCreateRequest> validator,
        IPriceService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var price = await service.CreateAsync(request, ct);
        return Results.Created($"/prices/{price.Id}", price);
    }

    // GetPriceHistory, UpdatePrice は同様のパターンで実装
}
```

### 5.6 SizeGuideEndpoints

```csharp
public static class SizeGuideEndpoints
{
    public static void MapSizeGuideEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/size-guides")
            .WithTags("SizeGuides")
            .WithOpenApi();

        group.MapGet("/category/{categoryId}", GetByCategoryId).WithName("GetSizeGuideByCategory");
        group.MapPost("/", CreateSizeGuide)
            .RequireAuthorization("AdminOnly").WithName("CreateSizeGuide");
        group.MapPut("/{id}", UpdateSizeGuide)
            .RequireAuthorization("AdminOnly").WithName("UpdateSizeGuide");
    }

    private static async Task<IResult> GetByCategoryId(
        string categoryId,
        ISizeGuideService service,
        CancellationToken ct)
        => await service.GetByCategoryIdAsync(categoryId, ct) is { } guide
            ? Results.Ok(guide)
            : Results.NotFound();

    // CreateSizeGuide, UpdateSizeGuide は同様のパターンで実装
}
```

### 5.7 gRPC プロトコル定義（inventory.proto）

```protobuf
syntax = "proto3";

option csharp_namespace = "InventoryManagementService.Protos";

package inventory;

service InventoryGrpc {
    rpc ReserveInventory (ReserveInventoryRequest) returns (ReserveInventoryResponse);
    rpc ReleaseReservation (ReleaseReservationRequest) returns (ReleaseReservationResponse);
}

message ReserveInventoryRequest {
    string order_id = 1;
    repeated ReserveItem items = 2;
}

message ReserveItem {
    string product_id = 1;
    int32 quantity = 2;
}

message ReserveInventoryResponse {
    bool success = 1;
    string reservation_id = 2;
    string error_message = 3;
}

message ReleaseReservationRequest {
    string order_id = 1;
    string reservation_id = 2;
}

message ReleaseReservationResponse {
    bool success = 1;
    string error_message = 2;
}
```

### 5.8 InventoryGrpcService

```csharp
public class InventoryGrpcService(
    IInventoryService inventoryService,
    ILogger<InventoryGrpcService> logger) : InventoryGrpc.InventoryGrpcBase
{
    public override async Task<ReserveInventoryResponse> ReserveInventory(
        ReserveInventoryRequest request, ServerCallContext context)
    {
        try
        {
            var items = request.Items.Select(i => new ReserveItemDto(i.ProductId, i.Quantity)).ToList();
            var reservationId = await inventoryService.ReserveAsync(request.OrderId, items, context.CancellationToken);
            return new ReserveInventoryResponse { Success = true, ReservationId = reservationId };
        }
        catch (InsufficientStockException ex)
        {
            logger.LogWarning("在庫不足 (gRPC): OrderId={OrderId}, Message={Message}", request.OrderId, ex.Message);
            return new ReserveInventoryResponse { Success = false, ErrorMessage = ex.Message };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "在庫引当エラー (gRPC): OrderId={OrderId}", request.OrderId);
            throw new RpcException(new Status(StatusCode.Internal, "在庫引当処理でエラーが発生しました"));
        }
    }

    public override async Task<ReleaseReservationResponse> ReleaseReservation(
        ReleaseReservationRequest request, ServerCallContext context)
    {
        try
        {
            await inventoryService.ReleaseAsync(request.OrderId, request.ReservationId, context.CancellationToken);
            return new ReleaseReservationResponse { Success = true };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "在庫解放エラー (gRPC): OrderId={OrderId}", request.OrderId);
            throw new RpcException(new Status(StatusCode.Internal, "在庫解放処理でエラーが発生しました"));
        }
    }
}
```

### 5.9 PaginationParams / ProductSearchParams

```csharp
public record PaginationParams(
    [Range(0, int.MaxValue)] int Page = 0,
    [Range(1, 100)] int Size = 20);

public record ProductSearchParams(
    string? Keyword = null,
    string? CategoryId = null,
    string? Brand = null,
    [Range(0, int.MaxValue)] int Page = 0,
    [Range(1, 100)] int Size = 20);

public record ProductSearchCriteria
{
    public string? Keyword { get; init; }
    public string? CategoryId { get; init; }
    public string? Brand { get; init; }
}
```

### 5.10 Program.cs 更新（Endpoint / gRPC マッピング）

```csharp
// Phase 5 で追加 — Endpoint マッピング
app.MapProductEndpoints();
app.MapCategoryEndpoints();
app.MapInventoryEndpoints();
app.MapPriceEndpoints();
app.MapReviewEndpoints();
app.MapSizeGuideEndpoints();

// gRPC サービスマッピング
app.MapGrpcService<InventoryGrpcService>();
```

### Phase 5 完了チェックリスト

| # | チェック項目 | 確認コマンド/方法 | 期待結果 |
|---|-----------|----------------|---------|
| 1 | ビルド成功 | `dotnet build` | 警告なし成功 |
| 2 | 全 6 Endpoint クラス | 目視確認 | ProductEndpoints, CategoryEndpoints, InventoryEndpoints, PriceEndpoints, ReviewEndpoints, SizeGuideEndpoints |
| 3 | `MapGroup` + `WithTags` + `WithOpenApi` | `grep -r 'MapGroup\|WithTags\|WithOpenApi' Endpoints/` | 全エンドポイントグループで使用 |
| 4 | `RequireAuthorization` | `grep -r 'RequireAuthorization' Endpoints/` | 管理者エンドポイントに AdminOnly ポリシー |
| 5 | `IValidator<T>` | `grep -r 'IValidator' Endpoints/` | POST/PUT エンドポイントでバリデーション実施 |
| 6 | `CancellationToken ct` | `grep -r 'CancellationToken ct' Endpoints/` | 全ハンドラメソッドに CancellationToken |
| 7 | Service 経由 | `grep -r 'Repository' Endpoints/` | Repository を直接参照していないこと |
| 8 | gRPC サービス | 目視確認 | InventoryGrpcService が InventoryGrpc.InventoryGrpcBase を継承 |
| 9 | gRPC proto | 目視確認 | inventory.proto に ReserveInventory / ReleaseReservation 定義 |
| 10 | **TODO/FIXME/HACK コメント残存チェック** | `grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと（0 件） |
| 11 | **Mock/Stub/仮実装の残存チェック** | `grep -rn "NotImplementedException\|\"dummy\"\|\"test\"\|\"xxx\"" --include="*.cs" src/` | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, 仮のハードコード値が本番コードに残存していないこと（0 件） |
| 12 | **未実装メソッド・空メソッドチェック** | 目視確認 | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## Phase 6: Kafka イベント連携

### 目的

設計書 §5.5（Kafka イベント設計）および §7（BackgroundService 設計）に基づき、Outbox パターンによるイベント発行と Kafka Consumer による受信処理を実装する。

### 作成ファイル一覧

| # | ファイルパス | 役割 |
|---|------------|------|
| 1 | `InventoryManagementService/Events/ProductCreatedEvent.cs` | 商品作成イベント |
| 2 | `InventoryManagementService/Events/ProductUpdatedEvent.cs` | 商品更新イベント |
| 3 | `InventoryManagementService/Events/ProductDeletedEvent.cs` | 商品削除イベント |
| 4 | `InventoryManagementService/Events/InventoryReservedEvent.cs` | 在庫引当イベント |
| 5 | `InventoryManagementService/Events/InventoryReleasedEvent.cs` | 在庫解放イベント |
| 6 | `InventoryManagementService/Events/InventoryUpdatedEvent.cs` | 在庫更新イベント |
| 7 | `InventoryManagementService/Events/StockDepletedEvent.cs` | 在庫枯渇イベント |
| 8 | `InventoryManagementService/Events/LowStockAlertEvent.cs` | 低在庫アラートイベント |
| 9 | `InventoryManagementService/Events/PriceUpdatedEvent.cs` | 価格更新イベント |
| 10 | `InventoryManagementService/Events/ReviewCreatedEvent.cs` | レビュー作成イベント |
| 11 | `InventoryManagementService/Events/ReviewApprovedEvent.cs` | レビュー承認イベント |
| 12 | `InventoryManagementService/Events/OrderCreatedEvent.cs` | 注文作成イベント（Subscribe） |
| 13 | `InventoryManagementService/Events/OrderCompletedEvent.cs` | 注文完了イベント（Subscribe） |
| 14 | `InventoryManagementService/Events/OrderCancelledEvent.cs` | 注文キャンセルイベント（Subscribe） |
| 15 | `InventoryManagementService/BackgroundServices/OutboxPublisher.cs` | Outbox → Kafka 発行サービス |
| 16 | `InventoryManagementService/BackgroundServices/OrderCreatedConsumer.cs` | 注文作成イベント Consumer |
| 17 | `InventoryManagementService/BackgroundServices/OrderCompletedConsumer.cs` | 注文完了イベント Consumer |
| 18 | `InventoryManagementService/BackgroundServices/OrderCancelledConsumer.cs` | 注文キャンセルイベント Consumer |
| 19 | `InventoryManagementService/BackgroundServices/InventoryReservationCleanupService.cs` | 期限切れ在庫予約の自動解放 |
| 20 | `InventoryManagementService/BackgroundServices/CacheWarmupService.cs` | 起動時キャッシュウォームアップ |
| 21 | `InventoryManagementService/BackgroundServices/UserDeletedConsumer.cs` | GDPR DSR ユーザー削除イベント Consumer（レビュー匿名化） |
| 22 | `InventoryManagementService/Configurations/KafkaConfig.cs` | Kafka 設定クラス |

### 6.1 イベント record 定義（Publish 側）

```csharp
// 商品イベント（inventory.products トピック）
public record ProductCreatedEvent(
    string ProductId, string Sku, string Name, string? Brand,
    string? CategoryId, DateTimeOffset CreatedAt);

public record ProductUpdatedEvent(
    string ProductId, string Sku, string Name, string? Brand,
    string? CategoryId, bool Active, DateTimeOffset UpdatedAt);

public record ProductDeletedEvent(
    string ProductId, string Sku, DateTimeOffset DeletedAt);

// 在庫イベント（inventory.levels トピック）
public record InventoryReservedEvent(
    string OrderId, string ProductId, int Quantity,
    string ReservationId, DateTimeOffset ReservedAt);

public record InventoryReleasedEvent(
    string OrderId, string ProductId, int Quantity,
    string ReservationId, string Reason, DateTimeOffset ReleasedAt);

public record InventoryUpdatedEvent(
    string ProductId, string Sku, int PreviousQuantity, int NewQuantity,
    string Reason, string LocationCode, DateTimeOffset UpdatedAt,
    string? ReferenceId = null);

public record StockDepletedEvent(
    string ProductId, string Sku, string LocationCode, DateTimeOffset DepletedAt);

public record LowStockAlertEvent(
    string ProductId, string Sku, int CurrentQuantity, int ReorderPoint,
    string LocationCode, DateTimeOffset AlertedAt);

// 価格イベント（inventory.pricing トピック）
public record PriceUpdatedEvent(
    string ProductId, decimal OldRegularPrice, decimal NewRegularPrice,
    decimal? OldSalePrice, decimal? NewSalePrice,
    string CurrencyCode, DateTimeOffset UpdatedAt);

// レビューイベント（inventory.reviews トピック）
public record ReviewCreatedEvent(
    string ReviewId, string ProductId, string UserId,
    int Rating, DateTimeOffset CreatedAt);

public record ReviewApprovedEvent(
    string ReviewId, string ProductId,
    int Rating, DateTimeOffset ApprovedAt);
```

### 6.2 イベント record 定義（Subscribe 側）

```csharp
// Subscribe イベント（他サービスから受信）
public record OrderCreatedEvent(
    string OrderId, string UserId, List<OrderItem> Items, DateTimeOffset OccurredAt)
{
    public record OrderItem(string ProductId, int Quantity, decimal UnitPrice);
}

public record OrderCompletedEvent(
    string OrderId, List<OrderCompletedItem> Items, DateTimeOffset OccurredAt)
{
    public record OrderCompletedItem(string ProductId, int Quantity);
}

public record OrderCancelledEvent(
    string OrderId, string ReservationId, string Reason, DateTimeOffset CancelledAt);

// ユーザー削除イベント（user.deleted トピック — AuthService から受信）
public record UserDeletedEvent(string UserId, DateTimeOffset DeletedAt);
```

### 6.3 OutboxPublisher（動的バックオフ + Advisory Lock）

```csharp
public class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private const int MinDelayMs = 100;
    private const int MaxDelayMs = 5000;
    private int _currentDelayMs = MinDelayMs;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxPublisher started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Advisory Lock で排他制御（マルチインスタンス対応）
                await using var connection = context.Database.GetDbConnection();
                await connection.OpenAsync(stoppingToken);
                await using var lockCmd = connection.CreateCommand();
                lockCmd.CommandText = "SELECT pg_try_advisory_lock(12345)";
                var acquired = (bool)(await lockCmd.ExecuteScalarAsync(stoppingToken) ?? false);

                if (!acquired)
                {
                    await Task.Delay(_currentDelayMs, stoppingToken);
                    continue;
                }

                try
                {
                    var events = await context.OutboxEvents
                        .Where(e => e.Status == "PENDING" || (e.Status == "FAILED" && e.RetryCount < e.MaxRetries))
                        .OrderBy(e => e.CreatedAt)
                        .Take(50)
                        .ToListAsync(stoppingToken);

                    if (events.Count > 0)
                    {
                        foreach (var evt in events)
                        {
                            try
                            {
                                evt.Status = "PROCESSING";
                                await context.SaveChangesAsync(stoppingToken);

                                var message = new Message<string, string>
                                {
                                    Key = evt.AggregateId,
                                    Value = evt.Payload,
                                    Headers = new Headers
                                    {
                                        { "event-type", System.Text.Encoding.UTF8.GetBytes(evt.EventType) }
                                    }
                                };
                                await producer.ProduceAsync(evt.Topic, message, stoppingToken);

                                evt.Status = "PUBLISHED";
                                evt.PublishedAt = DateTimeOffset.UtcNow;
                                await context.SaveChangesAsync(stoppingToken);

                                logger.LogInformation("Outbox イベント発行: {EventType}, AggregateId={AggregateId}",
                                    evt.EventType, evt.AggregateId);
                            }
                            catch (Exception ex)
                            {
                                evt.Status = "FAILED";
                                evt.RetryCount++;
                                evt.LastError = ex.Message[..Math.Min(ex.Message.Length, 2000)];

                                if (evt.RetryCount >= evt.MaxRetries)
                                    evt.Status = "DEAD_LETTER";

                                await context.SaveChangesAsync(stoppingToken);
                                logger.LogError(ex, "Outbox イベント発行失敗: {EventId}", evt.Id);
                            }
                        }
                        _currentDelayMs = MinDelayMs; // リセット
                    }
                    else
                    {
                        _currentDelayMs = Math.Min(_currentDelayMs * 2, MaxDelayMs); // バックオフ
                    }
                }
                finally
                {
                    await using var unlockCmd = connection.CreateCommand();
                    unlockCmd.CommandText = "SELECT pg_advisory_unlock(12345)";
                    await unlockCmd.ExecuteScalarAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "OutboxPublisher エラー: {Message}", ex.Message);
                _currentDelayMs = MaxDelayMs;
            }

            await Task.Delay(_currentDelayMs, stoppingToken);
        }
    }
}
```

### 6.4 OrderCreatedConsumer

```csharp
public class OrderCreatedConsumer(
    IConsumer<string, string> consumer,
    IServiceScopeFactory scopeFactory,
    ILogger<OrderCreatedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        consumer.Subscribe("order.created");
        logger.LogInformation("OrderCreatedConsumer started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var @event = JsonSerializer.Deserialize<OrderCreatedEvent>(result.Message.Value);
                if (@event is not null)
                {
                    using var scope = scopeFactory.CreateScope();
                    var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();

                    var items = @event.Items
                        .Select(i => new ReserveItemDto(i.ProductId, i.Quantity))
                        .ToList();

                    await inventoryService.ReserveAsync(@event.OrderId, items, stoppingToken);

                    logger.LogInformation("注文作成による在庫引当完了: OrderId={OrderId}, Items={ItemCount}",
                        @event.OrderId, @event.Items.Count);
                }
                consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume エラー: {Topic}", ex.ConsumerRecord?.Topic);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "OrderCreated イベント処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
```

### 6.5 InventoryReservationCleanupService

```csharp
public class InventoryReservationCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<InventoryReservationCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan ReservationTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("InventoryReservationCleanupService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var inventoryRepository = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
                var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisherService>();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Advisory Lock で排他制御
                await using var connection = context.Database.GetDbConnection();
                await connection.OpenAsync(stoppingToken);
                await using var lockCmd = connection.CreateCommand();
                lockCmd.CommandText = "SELECT pg_try_advisory_lock(12346)";
                var acquired = (bool)(await lockCmd.ExecuteScalarAsync(stoppingToken) ?? false);

                if (acquired)
                {
                    try
                    {
                        var expired = await inventoryRepository.FindExpiredReservationsAsync(
                            ReservationTimeout, stoppingToken);

                        foreach (var inventory in expired)
                        {
                            var releasedQty = inventory.ReservedQuantity;
                            inventory.ReservedQuantity = 0;
                            inventory.ReservedAt = null;
                            inventory.Status = inventory.Quantity > inventory.ReorderPoint
                                ? "IN_STOCK" : inventory.Quantity > 0 ? "LOW_STOCK" : "OUT_OF_STOCK";

                            await eventPublisher.PublishInventoryEventAsync("InventoryReleased", inventory.ProductId,
                                new InventoryReleasedEvent("TIMEOUT", inventory.ProductId, releasedQty,
                                    string.Empty, DateTimeOffset.UtcNow), stoppingToken);

                            logger.LogWarning("在庫予約タイムアウト解放: ProductId={ProductId}, Quantity={Qty}",
                                inventory.ProductId, releasedQty);
                        }

                        if (expired.Count > 0)
                            await context.SaveChangesAsync(stoppingToken);
                    }
                    finally
                    {
                        await using var unlockCmd = connection.CreateCommand();
                        unlockCmd.CommandText = "SELECT pg_advisory_unlock(12346)";
                        await unlockCmd.ExecuteScalarAsync(stoppingToken);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "予約クリーンアップエラー: {Message}", ex.Message);
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }
}
```

### 6.6 CacheWarmupService

```csharp
public class CacheWarmupService(
    IServiceScopeFactory scopeFactory,
    ILogger<CacheWarmupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CacheWarmupService started — 起動時キャッシュウォームアップ開始");

        try
        {
            using var scope = scopeFactory.CreateScope();
            var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
            var categoryService = scope.ServiceProvider.GetRequiredService<ICategoryService>();

            // 上位 100 商品をキャッシュに読み込み
            await productService.SearchAsync(new ProductSearchCriteria(), 0, 100, stoppingToken);

            // 全カテゴリをキャッシュに読み込み
            await categoryService.GetAllAsync(stoppingToken);

            logger.LogInformation("CacheWarmupService 完了: キャッシュウォームアップ成功");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "CacheWarmupService: ウォームアップ失敗（起動を継続）: {Message}", ex.Message);
        }
    }
}
```

### 6.7 UserDeletedConsumer（GDPR DSR 対応）

設計書 §4.7 に基づき、`user.deleted` Kafka イベントを購読し、該当ユーザーのレビューを匿名化する。AuthService から GDPR DSR（データ主体の権利）リクエスト時に発行されるイベントを処理する。

```csharp
public class UserDeletedConsumer(
    IConsumer<string, string> consumer,
    IServiceScopeFactory scopeFactory,
    ILogger<UserDeletedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        consumer.Subscribe("user.deleted");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var userId = result.Message.Key;

                if (string.IsNullOrEmpty(userId))
                {
                    logger.LogWarning("UserDeletedConsumer: userId が空のメッセージを受信しました");
                    consumer.Commit(result);
                    continue;
                }

                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // レビューの匿名化（UserId を "DELETED_USER" に置換）
                var reviews = await context.Reviews
                    .Where(r => r.UserId == userId)
                    .ToListAsync(stoppingToken);

                foreach (var review in reviews)
                {
                    review.UserId = "DELETED_USER";
                }

                await context.SaveChangesAsync(stoppingToken);
                consumer.Commit(result);

                logger.LogInformation("GDPR DSR 処理完了: UserId={UserId}, 匿名化レビュー数={Count}",
                    userId, reviews.Count);
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume エラー: {Topic}", ex.ConsumerRecord?.Topic);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "ユーザー削除イベント処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
```

### 6.8 Program.cs 更新（Kafka / BackgroundService 登録）

```csharp
// Phase 6 で追加 — Kafka Producer
builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var config = new ProducerConfig
    {
        BootstrapServers = builder.Configuration["Kafka:BootstrapServers"],
        EnableIdempotence = true,
        Acks = Acks.All
    };
    return new ProducerBuilder<string, string>(config).Build();
});

// Kafka Consumer（各 Consumer 用に個別 IConsumer を登録）
builder.Services.AddSingleton<IConsumer<string, string>>(sp =>
{
    var config = new ConsumerConfig
    {
        BootstrapServers = builder.Configuration["Kafka:BootstrapServers"],
        GroupId = "inventory-service",
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = false
    };
    return new ConsumerBuilder<string, string>(config).Build();
});

// BackgroundService 登録
builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddHostedService<OrderCreatedConsumer>();
builder.Services.AddHostedService<OrderCompletedConsumer>();
builder.Services.AddHostedService<OrderCancelledConsumer>();
builder.Services.AddHostedService<InventoryReservationCleanupService>();
builder.Services.AddHostedService<CacheWarmupService>();
builder.Services.AddHostedService<UserDeletedConsumer>();
```

### Phase 6 完了チェックリスト

| # | チェック項目 | 確認コマンド/方法 | 期待結果 |
|---|-----------|----------------|---------|
| 1 | ビルド成功 | `dotnet build` | 警告なし成功 |
| 2 | Publish イベント定義 | `ls Events/*.cs` | 11 件の Publish イベント record |
| 3 | Subscribe イベント定義 | `ls Events/Order*.cs` | OrderCreatedEvent, OrderCompletedEvent, OrderCancelledEvent |
| 4 | Outbox パターン | OutboxPublisher.cs 目視確認 | Advisory Lock + 動的バックオフ (100ms-5s) |
| 5 | Consumer 実装 | `ls BackgroundServices/` | 6 件の BackgroundService |
| 6 | `stoppingToken` 伝搬 | `grep -r 'stoppingToken' BackgroundServices/` | 全 Consumer で stoppingToken を下位に伝搬 |
| 7 | `IServiceScopeFactory` | `grep -r 'IServiceScopeFactory' BackgroundServices/` | 全 BackgroundService で Scoped サービス取得 |
| 8 | Consumer の例外処理 | 目視確認 | `ConsumeException` と汎用 `Exception` を個別キャッチ、バックオフ付き |
| 9 | 予約タイムアウト | InventoryReservationCleanupService.cs 目視確認 | 15 分タイムアウト + Advisory Lock |
| 10 | キャッシュウォームアップ | CacheWarmupService.cs 目視確認 | 失敗時もアプリ起動を継続（Warning ログ） |
| 11 | **TODO/FIXME/HACK コメント残存チェック** | `grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと（0 件） |
| 12 | **Mock/Stub/仮実装の残存チェック** | `grep -rn "NotImplementedException\|\"dummy\"\|\"test\"\|\"xxx\"" --include="*.cs" src/` | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, 仮のハードコード値が本番コードに残存していないこと（0 件） |
| 13 | **未実装メソッド・空メソッドチェック** | 目視確認 | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## Phase 7: Redis キャッシュ連携

### 目的

設計書 §9（キャッシュ戦略）に基づき、商品・カテゴリ・在庫・価格データの Redis キャッシュを実装する。`IDistributedCache` を使用し、Cache-Aside パターンで読み取りを高速化する。書き込み操作時にはキャッシュを無効化する。

### 作成ファイル一覧

| # | ファイルパス | 役割 |
|---|------------|------|
| 1 | `InventoryManagementService/Configurations/CacheConfig.cs` | キャッシュ設定クラス（TTL 等） |
| 2 | `InventoryManagementService/Services/ProductService.cs` | キャッシュ統合（更新） |
| 3 | `InventoryManagementService/Services/CategoryService.cs` | キャッシュ統合（更新） |
| 4 | `InventoryManagementService/Services/InventoryService.cs` | キャッシュ統合（更新） |
| 5 | `InventoryManagementService/Services/PriceService.cs` | キャッシュ統合（更新） |

### 7.1 CacheConfig

```csharp
public record CacheConfig
{
    public bool Enabled { get; init; } = true;
    public int DefaultTtlSeconds { get; init; } = 600;
    public int ProductTtlSeconds { get; init; } = 1800;       // 30 分
    public int CategoryTtlSeconds { get; init; } = 3600;      // 1 時間
    public int InventoryTtlSeconds { get; init; } = 300;       // 5 分
    public int PriceTtlSeconds { get; init; } = 900;           // 15 分
    public int SearchTtlSeconds { get; init; } = 600;          // 10 分
}
```

### 7.2 キャッシュキー設計

| キー | TTL | 用途 |
|-----|-----|------|
| `product:{id}` | 30 分 | 商品詳細 |
| `product:sku:{sku}` | 30 分 | SKU 検索 |
| `product:search:{hash}` | 10 分 | 検索結果 |
| `category:all` | 1 時間 | 全カテゴリ一覧 |
| `category:{id}` | 1 時間 | カテゴリ詳細 |
| `inventory:{productId}` | 5 分 | 在庫情報 |
| `price:{productId}` | 15 分 | 価格情報 |

### 7.3 ProductService へのキャッシュ統合例

```csharp
public class ProductService(
    IProductRepository productRepository,
    ICategoryRepository categoryRepository,
    IEventPublisherService eventPublisher,
    IDistributedCache cache,
    IOptions<CacheConfig> cacheOptions,
    ILogger<ProductService> logger) : IProductService
{
    private readonly CacheConfig _cacheConfig = cacheOptions.Value;

    public async Task<ProductDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (!_cacheConfig.Enabled)
            return await GetByIdFromDbAsync(id, ct);

        var cacheKey = $"product:{id}";
        var cached = await cache.GetStringAsync(cacheKey, ct);

        if (cached is not null)
            return JsonSerializer.Deserialize<ProductDto>(cached);

        var dto = await GetByIdFromDbAsync(id, ct);
        if (dto is not null)
        {
            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(dto),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_cacheConfig.ProductTtlSeconds)
                }, ct);
        }
        return dto;
    }

    public async Task<ProductDto> CreateProductAsync(
        ProductCreateRequest request, CancellationToken ct = default)
    {
        // ... 作成処理 ...

        // キャッシュ無効化
        await InvalidateProductCacheAsync(ct);

        return MapToDto(product);
    }

    private async Task InvalidateProductCacheAsync(CancellationToken ct)
    {
        // 関連キャッシュを無効化
        // category:all も無効化（商品数が変わるため）
        await cache.RemoveAsync("category:all", ct);
    }

    private async Task<ProductDto?> GetByIdFromDbAsync(string id, CancellationToken ct)
    {
        var product = await productRepository.FindByIdAsync(id, ct);
        return product is null ? null : MapToDto(product);
    }
}
```

### 7.4 Program.cs 更新（Redis 登録）

```csharp
// Phase 7 で追加
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "InventoryService:";
});

builder.Services.AddOptions<CacheConfig>()
    .Bind(builder.Configuration.GetSection("Inventory:Cache"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

### Phase 7 完了チェックリスト

| # | チェック項目 | 確認コマンド/方法 | 期待結果 |
|---|-----------|----------------|---------|
| 1 | ビルド成功 | `dotnet build` | 警告なし成功 |
| 2 | CacheConfig | 目視確認 | TTL 設定が設計書 §9 に一致（Product:30m, Category:1h, Inventory:5m, Price:15m） |
| 3 | `IDistributedCache` 注入 | `grep -r 'IDistributedCache' Services/` | 4 Service に DI 注入 |
| 4 | Cache-Aside パターン | 目視確認 | 読み取り: キャッシュ確認 → DB フォールバック → キャッシュ書き込み |
| 5 | キャッシュ無効化 | 目視確認 | 書き込み操作（Create/Update/Delete）で関連キャッシュを Remove |
| 6 | Redis 接続文字列 | appsettings.json 確認 | 接続文字列は環境変数参照（ハードコード禁止） |
| 7 | `IOptions<CacheConfig>` | Program.cs 確認 | `ValidateOnStart()` 付きで登録 |
| 8 | 起動時キャッシュウォームアップ | CacheWarmupService.cs 確認 | Phase 6 で作成済み |
| 9 | **TODO/FIXME/HACK コメント残存チェック** | `grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと（0 件） |
| 10 | **Mock/Stub/仮実装の残存チェック** | `grep -rn "NotImplementedException\|\"dummy\"\|\"test\"\|\"xxx\"" --include="*.cs" src/` | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, 仮のハードコード値が本番コードに残存していないこと（0 件） |
| 11 | **未実装メソッド・空メソッドチェック** | 目視確認 | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## Phase 8: 認証・認可・セキュリティ

### 目的

設計書 §6（セキュリティ設計）およびセキュリティコーディング規約に基づき、JWT Bearer 認証、ロールベース認可、IDOR 防止、セキュリティヘッダー、CORS、レート制限を実装する。

### 作成ファイル一覧

| # | ファイルパス | 役割 |
|---|------------|------|
| 1 | `InventoryManagementService/Infrastructure/Middleware/SecurityHeadersMiddleware.cs` | セキュリティヘッダー付与 |
| 2 | `InventoryManagementService/Infrastructure/Middleware/SecurityHeadersMiddlewareExtensions.cs` | 拡張メソッド |
| 3 | `InventoryManagementService/Infrastructure/Middleware/CorrelationIdMiddleware.cs` | Correlation ID 付与 |
| 4 | `InventoryManagementService/Infrastructure/Middleware/CorrelationIdMiddlewareExtensions.cs` | 拡張メソッド |

### 8.1 認証・認可設定（Program.cs）

```csharp
// Phase 8 で追加 — JWT Bearer 認証
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

// 認可ポリシー
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("UserOrAdmin", p => p.RequireRole("User", "Admin"));
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(builder.Configuration["AllowedOrigins"]
            ?? "https://skishop.example.com")
            .AllowAnyMethod()
            .AllowAnyHeader());
});

// レート制限
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("default", limiter =>
    {
        limiter.PermitLimit = 100;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("search", limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});
```

### 8.2 SecurityHeadersMiddleware

```csharp
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
        await next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
        => builder.UseMiddleware<SecurityHeadersMiddleware>();
}
```

### 8.3 CorrelationIdMiddleware

```csharp
public class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();
        context.Response.Headers.Append("X-Correlation-Id", correlationId);
        using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
        => builder.UseMiddleware<CorrelationIdMiddleware>();
}
```

### 8.4 グローバル例外ハンドラー（IExceptionHandler）

```csharp
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var (statusCode, message) = exception switch
        {
            ResourceNotFoundException e     => (404, e.Message),
            InsufficientStockException e    => (422, e.Message),
            DuplicateResourceException e    => (409, e.Message),
            InventoryException e            => (422, e.Message),
            UnauthorizedAccessException     => (401, "認証が必要です"),
            DbUpdateConcurrencyException    => (409, "データが他のユーザーによって更新されました。再度お試しください。"),
            _                               => (500, "内部エラーが発生しました")
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
            Title = Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(statusCode)
        }, ct);
        return true;
    }
}
```

### 8.5 ImageUploadRequestValidator（画像アップロードセキュリティ）

設計書 §7 に基づき、画像アップロードのセキュリティバリデーションを実装する。マジックバイト検証、EXIF 除去、ファイルサイズ・形式制限を含む。

```csharp
public class ImageUploadRequestValidator : AbstractValidator<IFormFile>
{
    private static readonly Dictionary<string, byte[]> AllowedMagicBytes = new()
    {
        { "image/jpeg", [0xFF, 0xD8, 0xFF] },
        { "image/png", [0x89, 0x50, 0x4E, 0x47] },
        { "image/webp", [0x52, 0x49, 0x46, 0x46] }
    };

    public ImageUploadRequestValidator()
    {
        RuleFor(x => x.Length)
            .GreaterThan(0).WithMessage("ファイルが空です")
            .LessThanOrEqualTo(10 * 1024 * 1024).WithMessage("ファイルサイズは 10MB 以下にしてください");

        RuleFor(x => x.ContentType)
            .Must(ct => AllowedMagicBytes.ContainsKey(ct))
            .WithMessage("許可されているファイル形式は JPEG, PNG, WebP のみです");

        RuleFor(x => x)
            .Must(ValidateMagicBytes)
            .WithMessage("ファイルの内容が宣言された形式と一致しません");
    }

    private static bool ValidateMagicBytes(IFormFile file)
    {
        if (!AllowedMagicBytes.TryGetValue(file.ContentType, out var expectedBytes))
            return false;

        using var stream = file.OpenReadStream();
        var headerBytes = new byte[expectedBytes.Length];
        if (stream.Read(headerBytes, 0, expectedBytes.Length) < expectedBytes.Length)
            return false;

        return headerBytes.AsSpan().StartsWith(expectedBytes);
    }
}
```

### 8.6 gRPC InternalServiceOnly 認可ポリシー

設計書 §5.1 に基づき、gRPC エンドポイント（Saga Step 2 の在庫引当/解放）はマイクロサービス間通信専用とし、外部リクエストからのアクセスを禁止する。

```csharp
// Program.cs での InternalServiceOnly ポリシー登録
builder.Services.AddAuthorization(options =>
{
    // 既存ポリシー
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    // gRPC 用: 内部サービス間通信のみ許可
    options.AddPolicy("InternalServiceOnly", p =>
        p.RequireClaim("service_role", "internal"));
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// gRPC サービスへの認可適用
app.MapGrpcService<InventoryGrpcService>()
    .RequireAuthorization("InternalServiceOnly");
```

### 8.7 ミドルウェアパイプライン完成版（Program.cs）

```csharp
// Phase 8 で Program.cs のミドルウェアを完成

// 例外ハンドラー DI 登録
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// 1. 例外ハンドラー
app.UseExceptionHandler();

// 2. セキュリティヘッダー
app.UseHsts();
app.UseHttpsRedirection();
app.UseSecurityHeaders();

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
app.MapProductEndpoints();
app.MapCategoryEndpoints();
app.MapInventoryEndpoints();
app.MapPriceEndpoints();
app.MapReviewEndpoints();
app.MapSizeGuideEndpoints();
app.MapGrpcService<InventoryGrpcService>();

// 9. ヘルスチェック
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false })
    .AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();

app.Run();
```

### 8.8 Endpoint の認可設定一覧

| エンドポイント | 認可設定 | 備考 |
|-------------|---------|------|
| `GET /api/products/**` | `.AllowAnonymous()` | 商品参照は未認証でもアクセス可 |
| `POST /api/products` | `RequireAuthorization("AdminOnly")` | 管理者のみ作成 |
| `PUT /api/products/{id}` | `RequireAuthorization("AdminOnly")` | 管理者のみ更新 |
| `DELETE /api/products/{id}` | `RequireAuthorization("AdminOnly")` | 管理者のみ論理削除 |
| `POST /api/products/{id}/images` | `RequireAuthorization("AdminOnly")` | 管理者のみ画像アップロード |
| `GET /api/categories/**` | FallbackPolicy | 全認証ユーザー |
| `POST/PUT/DELETE /api/categories/**` | `RequireAuthorization("AdminOnly")` | 管理者のみ変更 |
| `GET /api/inventory/**` | FallbackPolicy | 全認証ユーザー |
| `POST /api/inventory/stock-in/stock-out` | `RequireAuthorization("AdminOnly")` | 管理者のみ在庫操作 |
| `POST /api/reviews` | `RequireAuthorization()` | ログインユーザーのみ |
| `POST /api/reviews/{id}/response, PUT /api/reviews/{id}/status` | `RequireAuthorization("AdminOnly")` | 管理者のみ |
| `GET/POST/PUT /api/prices/**` | `RequireAuthorization("AdminOnly")` | 管理者のみ（GET 含む） |
| gRPC `InventoryGrpcService` | `RequireAuthorization("InternalServiceOnly")` | マイクロサービス間通信のみ |
| `GET /health, /health/ready` | `.AllowAnonymous()` | ヘルスチェックは公開 |

### Phase 8 完了チェックリスト

| # | チェック項目 | 確認コマンド/方法 | 期待結果 |
|---|-----------|----------------|---------|
| 1 | ビルド成功 | `dotnet build` | 警告なし成功 |
| 2 | JWT Bearer 認証 | Program.cs 目視確認 | `AddAuthentication` + `AddJwtBearer` |
| 3 | FallbackPolicy | Program.cs 目視確認 | `RequireAuthenticatedUser()` |
| 4 | AdminOnly ポリシー | Program.cs 目視確認 | `RequireRole("Admin")` |
| 5 | セキュリティヘッダー | SecurityHeadersMiddleware.cs 目視確認 | X-Content-Type-Options, X-Frame-Options, CSP, Referrer-Policy, Permissions-Policy |
| 6 | Correlation ID | CorrelationIdMiddleware.cs 目視確認 | リクエスト/レスポンスヘッダーに X-Correlation-Id |
| 7 | CORS | Program.cs 目視確認 | `AllowAnyOrigin()` 禁止、明示的オリジン指定 |
| 8 | レート制限 | Program.cs 目視確認 | search エンドポイントに制限あり |
| 9 | ミドルウェア順序 | Program.cs 目視確認 | ExceptionHandler → HSTS → SecurityHeaders → CorrelationId → Serilog → CORS → Auth → RateLimiter → Endpoints |
| 10 | グローバル例外ハンドラー | GlobalExceptionHandler.cs 目視確認 | `IExceptionHandler` 実装、スタックトレース非公開 |
| 11 | `AllowAnonymous` | `grep -r 'AllowAnonymous' Endpoints/ Program.cs` | `/health` と商品 GET エンドポイントのみ |
| 12 | `DetailedErrors: false` | appsettings.json 確認 | 本番でスタックトレース非公開 |
| 13 | ImageUploadRequestValidator | Validators/ 目視確認 | マジックバイト検証、ファイルサイズ制限（10MB）、許可形式（JPEG/PNG/WebP） |
| 14 | InternalServiceOnly ポリシー | Program.cs 目視確認 | gRPC サービスに `RequireAuthorization("InternalServiceOnly")` |
| 15 | **TODO/FIXME/HACK コメント残存チェック** | `grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと（0 件） |
| 16 | **Mock/Stub/仮実装の残存チェック** | `grep -rn "NotImplementedException\|\"dummy\"\|\"test\"\|\"xxx\"" --include="*.cs" src/` | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, 仮のハードコード値が本番コードに残存していないこと（0 件） |
| 17 | **未実装メソッド・空メソッドチェック** | 目視確認 | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## Phase 9: 単体テスト・統合テスト

### 目的

テスト規約（`.github/instructions/test-standards.instructions.md`）に基づき、全 Service の単体テスト（xUnit + NSubstitute + Shouldly）と Endpoint の統合テスト（WebApplicationFactory + Testcontainers.PostgreSql）を作成する。分岐カバレッジ 80% 以上を目指す。

### 作成ファイル一覧

| # | ファイルパス | 役割 |
|---|------------|------|
| 1 | `InventoryManagementService.Tests/InventoryManagementService.Tests.csproj` | テストプロジェクト |
| 2 | `InventoryManagementService.Tests/Services/ProductServiceTests.cs` | 商品サービス単体テスト |
| 3 | `InventoryManagementService.Tests/Services/InventoryServiceTests.cs` | 在庫サービス単体テスト |
| 4 | `InventoryManagementService.Tests/Services/CategoryServiceTests.cs` | カテゴリサービス単体テスト |
| 5 | `InventoryManagementService.Tests/Services/PriceServiceTests.cs` | 価格サービス単体テスト |
| 6 | `InventoryManagementService.Tests/Services/ReviewServiceTests.cs` | レビューサービス単体テスト |
| 7 | `InventoryManagementService.Tests/Services/SizeGuideServiceTests.cs` | サイズガイドサービス単体テスト |
| 8 | `InventoryManagementService.Tests/Services/EventPublisherServiceTests.cs` | イベント発行サービス単体テスト |
| 9 | `InventoryManagementService.Tests/Validators/ProductCreateRequestValidatorTests.cs` | バリデーター単体テスト |
| 10 | `InventoryManagementService.Tests/Validators/PriceCreateRequestValidatorTests.cs` | バリデーター単体テスト |
| 11 | `InventoryManagementService.Tests/Endpoints/ProductEndpointsTests.cs` | 商品 API 統合テスト |
| 12 | `InventoryManagementService.Tests/Endpoints/CategoryEndpointsTests.cs` | カテゴリ API 統合テスト |
| 13 | `InventoryManagementService.Tests/Endpoints/InventoryEndpointsTests.cs` | 在庫 API 統合テスト |
| 14 | `InventoryManagementService.Tests/Repositories/ProductRepositoryTests.cs` | DB スライステスト |
| 15 | `InventoryManagementService.Tests/Repositories/InventoryRepositoryTests.cs` | DB スライステスト |
| 16 | `InventoryManagementService.Tests/Validators/ImageUploadRequestValidatorTests.cs` | 画像アップロードバリデーター単体テスト |
| 17 | `InventoryManagementService.Tests/BackgroundServices/UserDeletedConsumerTests.cs` | GDPR ユーザー削除コンシューマー単体テスト |

### 9.1 テストプロジェクト .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="NSubstitute" Version="5.*" />
    <PackageReference Include="Shouldly" Version="4.*" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.*" />
    <PackageReference Include="Testcontainers.PostgreSql" Version="4.*" />
    <PackageReference Include="coverlet.collector" Version="6.*" />
    <PackageReference Include="FluentValidation" Version="11.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\InventoryManagementService\InventoryManagementService.csproj" />
  </ItemGroup>
</Project>
```

### 9.2 ProductServiceTests（代表例 — Should_X_When_Y パターン）

```csharp
[Trait("Category", "Unit")]
public class ProductServiceTests
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IEventPublisherService _eventPublisher;
    private readonly ILogger<ProductService> _logger;
    private readonly ProductService _sut;

    public ProductServiceTests()
    {
        _productRepository = Substitute.For<IProductRepository>();
        _categoryRepository = Substitute.For<ICategoryRepository>();
        _eventPublisher = Substitute.For<IEventPublisherService>();
        _logger = Substitute.For<ILogger<ProductService>>();
        _sut = new ProductService(_productRepository, _categoryRepository, _eventPublisher, _logger);
    }

    [Fact]
    public async Task Should_ReturnProduct_When_ValidIdProvided()
    {
        // Arrange
        var product = new Product { Id = "p1", Sku = "SKI-001-001", Name = "テストスキー", Active = true };
        _productRepository.FindByIdAsync("p1", default).Returns(product);

        // Act
        var result = await _sut.GetByIdAsync("p1");

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("p1");
        result.Sku.ShouldBe("SKI-001-001");
        result.Name.ShouldBe("テストスキー");
    }

    [Fact]
    public async Task Should_ReturnNull_When_ProductDoesNotExist()
    {
        // Arrange
        _productRepository.FindByIdAsync("nonexistent", default).Returns((Product?)null);

        // Act
        var result = await _sut.GetByIdAsync("nonexistent");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Should_CreateProduct_When_ValidRequest()
    {
        // Arrange
        var request = new ProductCreateRequest("SKI-NEW-001", "新商品", null, null, "cat1");
        _productRepository.FindBySkuAsync("SKI-NEW-001", default).Returns((Product?)null);
        _categoryRepository.ExistsByIdAsync("cat1", default).Returns(true);

        // Act
        var result = await _sut.CreateProductAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.Sku.ShouldBe("SKI-NEW-001");
        result.Name.ShouldBe("新商品");
        await _productRepository.Received(1).AddAsync(Arg.Any<Product>(), default);
        await _productRepository.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Should_ThrowDuplicateResourceException_When_SkuAlreadyExists()
    {
        // Arrange
        var request = new ProductCreateRequest("SKI-DUP-001", "重複商品", null, null, "cat1");
        _productRepository.FindBySkuAsync("SKI-DUP-001", default)
            .Returns(new Product { Sku = "SKI-DUP-001" });

        // Act & Assert
        var act = async () => await _sut.CreateProductAsync(request);
        var ex = await Should.ThrowAsync<DuplicateResourceException>(act);
        ex.Message.ShouldContain("Product");
        ex.Message.ShouldContain("SKI-DUP-001");
    }

    [Fact]
    public async Task Should_ThrowResourceNotFoundException_When_CategoryDoesNotExist()
    {
        // Arrange
        var request = new ProductCreateRequest("SKI-NEW-002", "新商品2", null, null, "invalid-cat");
        _productRepository.FindBySkuAsync("SKI-NEW-002", default).Returns((Product?)null);
        _categoryRepository.ExistsByIdAsync("invalid-cat", default).Returns(false);

        // Act & Assert
        var act = async () => await _sut.CreateProductAsync(request);
        var ex = await Should.ThrowAsync<ResourceNotFoundException>(act);
        ex.Message.ShouldContain("Category");
    }

    [Fact]
    public async Task Should_ThrowOperationCanceled_When_TokenIsCanceled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var act = async () => await _sut.GetByIdAsync("p1", cts.Token);
        await Should.ThrowAsync<OperationCanceledException>(act);
    }

    // --- UpdateAsync テスト ---
    [Fact]
    public async Task Should_UpdateProduct_When_ValidRequestProvided()
    {
        // Arrange
        var existing = new Product { Id = "p1", Name = "Old Name", Sku = "SKU-001" };
        _productRepository.GetByIdAsync("p1", default).Returns(existing);
        var request = new ProductUpdateRequest("New Name", null, null, null, null, null, null);

        // Act
        var result = await _sut.UpdateAsync("p1", request);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("New Name");
        await _eventPublisher.Received(1).PublishProductUpdatedAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ThrowNotFoundException_When_UpdateNonExistentProduct()
    {
        // Arrange
        _productRepository.GetByIdAsync("p999", default).Returns((Product?)null);

        // Act & Assert
        var act = async () => await _sut.UpdateAsync("p999", new ProductUpdateRequest("X", null, null, null, null, null, null));
        await Should.ThrowAsync<ResourceNotFoundException>(act);
    }

    // --- DeleteAsync テスト ---
    [Fact]
    public async Task Should_SoftDeleteProduct_When_ValidId()
    {
        // Arrange
        var existing = new Product { Id = "p1", Active = true };
        _productRepository.GetByIdAsync("p1", default).Returns(existing);

        // Act
        await _sut.DeleteAsync("p1");

        // Assert
        existing.Active.ShouldBeFalse();
        await _productRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _eventPublisher.Received(1).PublishProductDeletedAsync("p1", Arg.Any<CancellationToken>());
    }

    // --- UploadImageAsync テスト ---
    [Fact]
    public async Task Should_UploadImage_When_ValidFileProvided()
    {
        // Arrange
        var file = Substitute.For<IFormFile>();
        file.Length.Returns(1024);
        file.ContentType.Returns("image/jpeg");
        _productRepository.GetByIdAsync("p1", default).Returns(new Product { Id = "p1" });
        _imageRepository.UploadAsync(Arg.Any<IFormFile>(), "p1", default).Returns("https://blob.example.com/img.jpg");

        // Act
        var result = await _sut.UploadImageAsync("p1", file);

        // Assert
        result.ShouldNotBeNull();
        result.Url.ShouldStartWith("https://");
    }
}
```

### 9.3 InventoryServiceTests

```csharp
[Trait("Category", "Unit")]
public class InventoryServiceTests
{
    private readonly AppDbContext _context;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IEventPublisherService _eventPublisher;
    private readonly ILogger<InventoryService> _logger;
    private readonly InventoryService _sut;

    public InventoryServiceTests()
    {
        _context = Substitute.For<AppDbContext>();
        _inventoryRepository = Substitute.For<IInventoryRepository>();
        _eventPublisher = Substitute.For<IEventPublisherService>();
        _logger = Substitute.For<ILogger<InventoryService>>();
        _sut = new InventoryService(_context, _inventoryRepository, _eventPublisher, _logger);
    }

    [Fact]
    public async Task Should_ReturnInventory_When_ProductIdExists()
    {
        // Arrange
        var inventory = new Inventory
        {
            Id = "inv1", ProductId = "p1", Quantity = 100,
            ReservedQuantity = 10, LocationCode = "WH-01", Status = "IN_STOCK"
        };
        _inventoryRepository.FindByProductIdAsync("p1", default).Returns(inventory);

        // Act
        var result = await _sut.GetByProductIdAsync("p1");

        // Assert
        result.ShouldNotBeNull();
        result.Quantity.ShouldBe(100);
        result.AvailableQuantity.ShouldBe(90);
        result.Status.ShouldBe("IN_STOCK");
    }

    [Fact]
    public async Task Should_IncreaseQuantity_When_StockIn()
    {
        // Arrange
        var inventory = new Inventory
        {
            Id = "inv1", ProductId = "p1", Quantity = 50,
            ReservedQuantity = 0, LocationCode = "WH-01",
            Status = "LOW_STOCK", ReorderPoint = 100
        };
        _inventoryRepository.FindByProductIdAsync("p1", default).Returns(inventory);

        var request = new StockInRequest("p1", 200);

        // Act
        var result = await _sut.StockInAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.Quantity.ShouldBe(250);
        result.Status.ShouldBe("IN_STOCK");
        await _eventPublisher.Received(1).PublishInventoryEventAsync(
            "InventoryUpdated", "p1", Arg.Any<InventoryUpdatedEvent>(), default);
    }

    [Fact]
    public async Task Should_ThrowResourceNotFoundException_When_InventoryNotFound()
    {
        // Arrange
        var request = new StockInRequest("nonexistent", 10);
        _inventoryRepository.FindByProductIdAsync("nonexistent", default).Returns((Inventory?)null);

        // Act & Assert
        var act = async () => await _sut.StockInAsync(request);
        var ex = await Should.ThrowAsync<ResourceNotFoundException>(act);
        ex.Message.ShouldContain("Inventory");
    }

    // --- DetermineStatus テスト（在庫ステータス状態遷移） ---
    [Theory]
    [InlineData(0, 0, 10, false, "OUT_OF_STOCK")]
    [InlineData(5, 0, 10, false, "LOW_STOCK")]
    [InlineData(100, 0, 10, false, "IN_STOCK")]
    [InlineData(100, 5, 10, false, "RESERVED")]
    [InlineData(0, 0, 10, true, "DISCONTINUED")]
    [InlineData(100, 0, 10, true, "DISCONTINUED")]
    public void Should_ReturnCorrectStatus_When_DetermineStatusCalled(
        int quantity, int reserved, int reorderPoint, bool discontinued, string expectedStatus)
    {
        // Act
        var status = InventoryService.DetermineStatus(quantity, reserved, reorderPoint, discontinued);

        // Assert
        status.ShouldBe(expectedStatus);
    }
}
```

### 9.4 ProductCreateRequestValidatorTests

```csharp
[Trait("Category", "Unit")]
public class ProductCreateRequestValidatorTests
{
    private readonly ProductCreateRequestValidator _validator = new();

    [Fact]
    public async Task Should_PassValidation_When_AllFieldsValid()
    {
        // Arrange
        var request = new ProductCreateRequest("SKI-TEST-001", "テスト商品", "説明", "TestBrand", "cat1");

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_FailValidation_When_SkuIsBlank(string invalidSku)
    {
        // Arrange
        var request = new ProductCreateRequest(invalidSku, "テスト商品", null, null, "cat1");

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Sku");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("123-456")]
    [InlineData("ski-test-001")]
    public async Task Should_FailValidation_When_SkuFormatInvalid(string invalidSku)
    {
        // Arrange
        var request = new ProductCreateRequest(invalidSku, "テスト商品", null, null, "cat1");

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_FailValidation_When_NameIsNull()
    {
        // Arrange
        var request = new ProductCreateRequest("SKI-TEST-001", null!, null, null, "cat1");

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task Should_FailValidation_When_CategoryIdIsEmpty()
    {
        // Arrange
        var request = new ProductCreateRequest("SKI-TEST-001", "テスト商品", null, null, "");

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "CategoryId");
    }
}
```

### 9.5 統合テスト（WebApplicationFactory）

```csharp
[Trait("Category", "Integration")]
public class ProductEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ProductEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // テスト用 DB / 認証モック設定
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        "Test", _ => { });
            });
        }).CreateClient();
    }

    [Fact]
    public async Task Should_Return200_When_GetProducts()
    {
        // Act
        var response = await _client.GetAsync("/products?page=0&size=10");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_Return404_When_ProductNotFound()
    {
        // Act
        var response = await _client.GetAsync("/products/nonexistent-id");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_Return401_When_NoAuthToken()
    {
        // Arrange — 認証なしのクライアント
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/products");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
```

### 9.6 DB スライステスト（Testcontainers.PostgreSql）

```csharp
[Trait("Category", "Integration")]
public class ProductRepositoryTests : IAsyncLifetime
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
        await _context.Database.EnsureCreatedAsync();

        // テストデータ投入
        _context.Categories.Add(new Category { Id = "cat1", Name = "スキー板" });
        _context.Products.Add(new Product
        {
            Id = "p1", Sku = "SKI-TEST-001", Name = "テストスキー",
            CategoryId = "cat1", Active = true
        });
        await _context.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Should_FindProduct_When_IdExists()
    {
        // Arrange
        var repository = new ProductRepository(_context);

        // Act
        var result = await repository.FindByIdAsync("p1");

        // Assert
        result.ShouldNotBeNull();
        result.Sku.ShouldBe("SKI-TEST-001");
        result.Category.ShouldNotBeNull();
        result.Category!.Name.ShouldBe("スキー板");
    }

    [Fact]
    public async Task Should_ReturnNull_When_IdDoesNotExist()
    {
        // Arrange
        var repository = new ProductRepository(_context);

        // Act
        var result = await repository.FindByIdAsync("nonexistent");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Should_FindProduct_When_SkuExists()
    {
        // Arrange
        var repository = new ProductRepository(_context);

        // Act
        var result = await repository.FindBySkuAsync("SKI-TEST-001");

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("テストスキー");
    }
}
```

### Phase 9 完了チェックリスト

| # | チェック項目 | 確認コマンド/方法 | 期待結果 |
|---|-----------|----------------|---------|
| 1 | テストビルド成功 | `dotnet build InventoryManagementService.Tests/` | 警告なし成功 |
| 2 | 全テスト通過 | `dotnet test` | 全件 Pass |
| 3 | テスト命名 | `grep -r 'public async Task Should_' Tests/` | `Should_X_When_Y` パターン |
| 4 | AAA パターン | 目視確認 | Arrange / Act / Assert が分離 |
| 5 | `[Trait("Category")]` | `grep -r '\[Trait\(' Tests/` | 全テストに `Unit` / `Integration` タグ |
| 6 | Shouldly アサーション | `grep -r 'ShouldBe\|ShouldNotBeNull\|ShouldThrow' Tests/` | xUnit Assert ではなく Shouldly 使用 |
| 7 | NSubstitute モック | `grep -r 'Substitute.For' Tests/` | 外部依存のモック化 |
| 8 | `Received()` 検証 | `grep -r 'Received\|DidNotReceive' Tests/` | メソッド呼び出しの検証 |
| 9 | 異常系テスト | `grep -r 'ThrowAsync\|ShouldThrow' Tests/` | 例外テストが正常系と同等以上 |
| 10 | CancellationToken テスト | `grep -r 'OperationCanceledException' Tests/` | キャンセル時の動作テスト |
| 11 | Testcontainers | `grep -r 'PostgreSqlContainer' Tests/` | DB スライステストで使用 |
| 12 | カバレッジ | `dotnet test --collect:"XPlat Code Coverage"` | 分岐カバレッジ 80% 以上 |
| 13 | **TODO/FIXME/HACK コメント残存チェック** | `grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと（0 件） |
| 14 | **Mock/Stub/仮実装の残存チェック** | `grep -rn "NotImplementedException\|\"dummy\"\|\"test\"\|\"xxx\"" --include="*.cs" src/` | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, 仮のハードコード値が本番コードに残存していないこと（0 件） |
| 15 | **未実装メソッド・空メソッドチェック** | 目視確認 | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## Phase 10: 可観測性

### 目的

設計書 §10（可観測性設計）に基づき、OpenTelemetry（トレーシング + メトリクス）、Serilog 構造化ログ、ヘルスチェック（PostgreSQL / Redis / Kafka）を統合する。

### 作成ファイル一覧

| # | ファイルパス | 役割 |
|---|------------|------|
| 1 | `InventoryManagementService/Program.cs` | OpenTelemetry / Serilog / HealthCheck 登録（更新） |

### 10.1 OpenTelemetry 設定

```csharp
// Phase 10 で追加
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource("SkiShop.InventoryManagement"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation());
```

### 10.2 Serilog 設定

```csharp
// Phase 10 で追加
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "InventoryManagementService")
        .WriteTo.Console(new CompactJsonFormatter()));
```

### 10.3 ヘルスチェック設定

```csharp
// Phase 10 で追加
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("接続文字列が設定されていません");

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql", tags: ["ready"])
    .AddRedis(builder.Configuration.GetConnectionString("Redis") ?? "",
        name: "redis", tags: ["ready"])
    .AddKafka(new Confluent.Kafka.ProducerConfig
    {
        BootstrapServers = builder.Configuration.GetConnectionString("Kafka") ?? ""
    }, name: "kafka", tags: ["ready"]);
```

### Phase 10 完了チェックリスト

| # | チェック項目 | 確認コマンド/方法 | 期待結果 |
|---|-----------|----------------|---------|
| 1 | ビルド成功 | `dotnet build` | 警告なし成功 |
| 2 | OpenTelemetry | Program.cs 目視確認 | Tracing + Metrics + ASP.NET Core / HttpClient / EF Core Instrumentation |
| 3 | Serilog | Program.cs 目視確認 | `UseSerilog` + `CompactJsonFormatter` + `Enrich.WithProperty("ServiceName")` |
| 4 | ヘルスチェック | Program.cs 目視確認 | PostgreSQL + Redis + Kafka の Readiness チェック |
| 5 | `/health` | `curl localhost:5003/health` | `200 OK` |
| 6 | `/health/ready` | `curl localhost:5003/health/ready` | PostgreSQL / Redis / Kafka 接続状態を返す |
| 7 | Correlation ID | ログ出力確認 | 全リクエストに CorrelationId プロパティが含まれる |
| 8 | メッセージテンプレート | `grep -rP '\.Log\w+\(\$"' InventoryManagementService/` | 文字列補間ログが 0 件 |
| 9 | **TODO/FIXME/HACK コメント残存チェック** | `grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと（0 件） |
| 10 | **Mock/Stub/仮実装の残存チェック** | `grep -rn "NotImplementedException\|\"dummy\"\|\"test\"\|\"xxx\"" --include="*.cs" src/` | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, 仮のハードコード値が本番コードに残存していないこと（0 件） |
| 11 | **未実装メソッド・空メソッドチェック** | 目視確認 | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## Phase 11: Docker / デプロイ準備

### 目的

Phase 1 で作成した Dockerfile を最終確認し、.NET Aspire AppHost への統合、CI/CD パイプラインで必要な設定を整える。

### 11.1 Dockerfile 最終版

Phase 1 で作成済み。以下のチェック項目を満たすことを確認する：

- マルチステージビルド（sdk → aspnet）
- バージョン固定（`10.0`、`latest` 禁止）
- 非 root ユーザー（`USER skishop`）
- `HEALTHCHECK` あり
- `.dockerignore` あり

### 11.2 AppHost 統合

```csharp
// AppHost/Program.cs に追加
var inventoryService = builder.AddProject<Projects.InventoryManagementService>("inventory-service")
    .WithReference(postgres)
    .WithReference(redis)
    .WithReference(kafka);
```

### 11.3 Docker イメージビルド確認

```bash
# Dockerfile のビルド確認
docker build -t skishop/inventory-service:latest -f InventoryManagementService/Dockerfile .

# イメージサイズ確認（SDK が含まれていないことを確認）
docker images skishop/inventory-service

# 起動テスト
docker run -d --name inventory-test -p 5003:8080 skishop/inventory-service:latest
curl http://localhost:5003/health
docker stop inventory-test && docker rm inventory-test
```

### Phase 11 完了チェックリスト

| # | チェック項目 | 確認コマンド/方法 | 期待結果 |
|---|-----------|----------------|---------|
| 1 | Docker ビルド成功 | `docker build -f InventoryManagementService/Dockerfile .` | ビルド成功 |
| 2 | マルチステージ | Dockerfile 目視確認 | `sdk` → `aspnet` の 2 ステージ |
| 3 | 非 root | Dockerfile 目視確認 | `USER skishop` |
| 4 | HEALTHCHECK | Dockerfile 目視確認 | `/health` への curl チェック |
| 5 | バージョン固定 | `grep 'FROM' Dockerfile` | `10.0`（`latest` なし） |
| 6 | .dockerignore | 目視確認 | `bin/`, `obj/`, `.git/` 除外 |
| 7 | AppHost 統合 | AppHost/Program.cs 目視確認 | `WithReference(postgres/redis/kafka)` |
| 8 | `dotnet publish` | `dotnet publish -c Release InventoryManagementService/` | 発行成功 |
| 9 | **TODO/FIXME/HACK コメント残存チェック** | `grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと（0 件） |
| 10 | **Mock/Stub/仮実装の残存チェック** | `grep -rn "NotImplementedException\|\"dummy\"\|\"test\"\|\"xxx\"" --include="*.cs" src/` | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, 仮のハードコード値が本番コードに残存していないこと（0 件） |
| 11 | **未実装メソッド・空メソッドチェック** | 目視確認 | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## 最終チェックリスト

### AGENTS.md 禁止事項チェック

実装完了後、以下のコマンドで禁止事項がないことを確認する：

```bash
# 1. 秘密情報のハードコードチェック
grep -r "Password\s*=\s*\"" --include="*.cs" InventoryManagementService/

# 2. Console.WriteLine チェック
grep -r "Console\.Write" --include="*.cs" InventoryManagementService/

# 3. SQL 文字列結合チェック
grep -r "FromSqlRaw.*\+" --include="*.cs" InventoryManagementService/

# 4. .Result / .Wait() チェック（デッドロックリスク）
grep -rP "\.(Result|Wait)\(\)" --include="*.cs" InventoryManagementService/

# 5. プロパティインジェクションチェック
grep -r "\[Inject\]" --include="*.cs" InventoryManagementService/

# 6. DateTime.Now チェック（ローカル時刻禁止）
grep -r "DateTime\.Now" --include="*.cs" InventoryManagementService/

# 7. new HttpClient() チェック
grep -r "new HttpClient()" --include="*.cs" InventoryManagementService/

# 8. catch (Exception) { } チェック（例外の握りつぶし）
grep -rP "catch\s*\(Exception\)\s*\{[\s]*\}" --include="*.cs" InventoryManagementService/

# 9. Thread.Sleep() チェック
grep -r "Thread\.Sleep" --include="*.cs" InventoryManagementService/

# 10. プレリリースパッケージチェック
grep -i "preview\|beta\|-rc" InventoryManagementService/*.csproj
```

全コマンドの出力が**空（0 件一致）**であることを確認する。

### コーディング規約チェックリスト

| # | チェック項目 | 期待結果 |
|---|-----------|---------|
| 1 | 命名規則（PascalCase / camelCase / `_camelCase`） | 全ファイルで遵守 |
| 2 | primary constructor | 全 Service / Repository で使用 |
| 3 | `CancellationToken ct = default` | 全 async メソッドに含まれる |
| 4 | `ILogger<T>` メッセージテンプレート | 文字列補間ログなし |
| 5 | record 型 DTO | 全 DTO が record |
| 6 | `AsNoTracking()` | 読み取り専用クエリで使用 |
| 7 | `FromSqlInterpolated` | 生 SQL が必要な場合のみ使用 |
| 8 | `= []` コレクション初期化 | 全ナビゲーションプロパティ |
| 9 | `[Table("snake_case")]` / `[Column("snake_case")]` | 全エンティティ |
| 10 | FallbackPolicy | 認証必須（AllowAnonymous は明示的のみ） |
| 11 | `ValidateOnStart()` | IOptions 登録に付与 |
| 12 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 13 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 14 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

### テスト規約チェックリスト

| # | チェック項目 | 期待結果 |
|---|-----------|---------|
| 1 | `Should_X_When_Y` 命名 | 全テストメソッド |
| 2 | AAA パターン | Arrange / Act / Assert が分離 |
| 3 | `[Trait("Category")]` | 全テストに Unit / Integration / Security タグ |
| 4 | Shouldly アサーション | `Assert.Equal` ではなく `ShouldBe` |
| 5 | NSubstitute `Received()` | モック呼び出しの検証 |
| 6 | 異常系テスト | 正常系と同等以上 |
| 7 | CancellationToken テスト | キャンセル時の動作テスト |
| 8 | 分岐カバレッジ 80% 以上 | `dotnet test --collect:"XPlat Code Coverage"` |
| 9 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 10 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 11 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

### フェーズ依存関係図

```
Phase 1 ── プロジェクト基盤構築
  │
Phase 2 ── エンティティ・AppDbContext・DTO・例外クラス
  │
Phase 3 ── Repository 層
  │
Phase 4 ── Service 層 + FluentValidation
  │
  ├── Phase 5 ── Endpoints + gRPC
  │     │
  │     ├── Phase 6 ── Kafka イベント連携 + BackgroundServices
  │     │
  │     ├── Phase 7 ── Redis キャッシュ連携
  │     │
  │     └── Phase 8 ── 認証・認可・セキュリティ
  │
  └── Phase 9 ── 単体テスト・統合テスト（Phase 3-8 を並行テスト可能）
        │
Phase 10 ── 可観測性
  │
Phase 11 ── Docker / デプロイ準備
```

### 参照ドキュメント一覧

| ドキュメント | パス |
|-----------|------|
| システム全体設計 | `design-docs/spec.md` |
| 在庫管理サービス設計 | `design-docs/inventory-management-design.md` |
| C# コーディング規約 | `.github/instructions/dotnet-coding-standards.instructions.md` |
| セキュリティコーディング規約 | `.github/instructions/security-coding.instructions.md` |
| API 設計規約 | `.github/instructions/api-design.instructions.md` |
| ASP.NET Core 設定規約 | `.github/instructions/dotnet-config.instructions.md` |
| NuGet 依存関係管理規約 | `.github/instructions/nuget-dependency.instructions.md` |
| テスト規約 | `.github/instructions/test-standards.instructions.md` |
| Dockerfile / インフラ規約 | `.github/instructions/dockerfile-infra.instructions.md` |
| SQL スキーマ規約 | `.github/instructions/sql-schema-review.instructions.md` |
| エージェント指示書 | `AGENTS.md` |
