# SalesManagementService フェーズ別実装計画書

> **対象サービス**: SalesManagementService（注文・販売管理サービス）
> **ポート**: 5004
> **DB**: PostgreSQL（salesdb）— サービス別独立 DB（ADR-0006）
> **メッセージング**: Apache Kafka（Confluent.Kafka）+ Outbox パターン（ADR-0005）
> **キャッシュ**: Redis（StackExchange.Redis）
> **gRPC**: InventoryService, CouponService, PointService, CartService, PaymentService との通信
> **設計書**: `design-docs/sales-management-design.md`
> **規約**: AGENTS.md / `.github/instructions/` 配下のインストラクションファイル群

---

## 目次

1. [Phase 1: プロジェクト基盤構築](#phase-1-プロジェクト基盤構築)
2. [Phase 2: エンティティ & DbContext](#phase-2-エンティティ--dbcontext)
3. [Phase 3: Repository 層](#phase-3-repository-層)
4. [Phase 4: Service 層](#phase-4-service-層)
5. [Phase 5: Endpoints（Minimal API）](#phase-5-endpointsminimal-api)
6. [Phase 6: Kafka + Outbox パターン](#phase-6-kafka--outbox-パターン)
7. [Phase 7: Saga オーケストレーション](#phase-7-saga-オーケストレーション)
8. [Phase 8: 可観測性 & 耐障害性](#phase-8-可観測性--耐障害性)
9. [Phase 9: セキュリティ](#phase-9-セキュリティ)
10. [Phase 10: テスト](#phase-10-テスト)
11. [Phase 11: 最終統合 & デプロイ準備](#phase-11-最終統合--デプロイ準備)

---

## Phase 1: プロジェクト基盤構築

### 目的

SalesManagementService プロジェクトの骨格を構築する。ビルド可能な最小構成を作成し、以降のフェーズの土台とする。

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `SalesManagementService/SalesManagementService.csproj` | プロジェクト定義（EF Core, gRPC, Kafka, Redis, Serilog, OpenTelemetry 等） |
| 2 | `SalesManagementService/Program.cs` | エントリポイントスケルトン（最小構成） |
| 3 | `SalesManagementService/appsettings.json` | 共通設定（安全なデフォルト値のみ、秘密情報禁止） |
| 4 | `SalesManagementService/appsettings.Development.json` | 開発環境設定 |
| 5 | `SalesManagementService/appsettings.Production.json` | 本番環境設定（環境変数参照のみ） |
| 6 | `SalesManagementService/Dockerfile` | マルチステージビルド + 非 root 実行 |
| 7 | `SalesManagementService/.dockerignore` | ビルド不要ファイルの除外 |

### ディレクトリ構成

```
SalesManagementService/
├── SalesManagementService.csproj
├── Program.cs
├── Endpoints/
├── Services/
│   └── Interfaces/
├── Repositories/
│   └── Interfaces/
├── Models/
│   └── Enums/
├── DTOs/
│   ├── Requests/
│   └── Responses/
├── Configurations/
├── Infrastructure/
│   ├── Persistence/
│   ├── Saga/
│   ├── Outbox/
│   └── Exceptions/
├── Migrations/
├── appsettings.json
├── appsettings.Development.json
├── appsettings.Production.json
├── Dockerfile
└── .dockerignore
```

### 1.1 SalesManagementService.csproj

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

    <!-- gRPC クライアント -->
    <PackageReference Include="Grpc.Net.Client" Version="2.*" />
    <PackageReference Include="Google.Protobuf" Version="3.*" />
    <PackageReference Include="Grpc.Tools" Version="2.*" PrivateAssets="all" />

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
    <PackageReference Include="OpenTelemetry.Instrumentation.GrpcNetClient" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.*" />

    <!-- ヘルスチェック -->
    <PackageReference Include="AspNetCore.HealthChecks.NpgSql" Version="9.*" />
    <PackageReference Include="AspNetCore.HealthChecks.Redis" Version="9.*" />
    <PackageReference Include="AspNetCore.HealthChecks.Kafka" Version="9.*" />

    <!-- レポート生成 -->
    <PackageReference Include="QuestPDF" Version="2024.*" />
  </ItemGroup>
</Project>
```

### 1.2 Program.cs（最小構成スケルトン）

```csharp
var builder = WebApplication.CreateBuilder(args);

// ── Serilog ──
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "SalesManagementService")
        .WriteTo.Console(new CompactJsonFormatter()));

// ── TimeProvider ──
builder.Services.AddSingleton(TimeProvider.System);

// Phase 2 以降で追加: EF Core, Repository, Service, gRPC, Kafka, 認証, ヘルスチェック

var app = builder.Build();

// Phase 5 以降で追加: ミドルウェアパイプライン, エンドポイント

app.Run();

// ✅ WebApplicationFactory テスト用にクラスを公開
public partial class Program;
```

### 1.3 appsettings.json

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

### 1.4 appsettings.Development.json

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

### 1.5 appsettings.Production.json

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

### 1.6 Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["SalesManagementService/SalesManagementService.csproj", "SalesManagementService/"]
RUN dotnet restore "SalesManagementService/SalesManagementService.csproj"
COPY . .
WORKDIR "/src/SalesManagementService"
RUN dotnet publish "SalesManagementService.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN groupadd -r skishop && useradd -r -g skishop -d /app -s /sbin/nologin skishop
COPY --from=build --chown=skishop:skishop /app/publish .
USER skishop

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 5004
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:5004/health || exit 1
ENTRYPOINT ["dotnet", "SalesManagementService.dll"]
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

- [ ] `dotnet build SalesManagementService/` が成功すること
- [ ] `appsettings.json` に秘密情報（パスワード、API キー等）が含まれていないこと
- [ ] `Dockerfile` がマルチステージビルドで非 root ユーザー実行になっていること
- [ ] `.csproj` に `-preview` / `-beta` / `-rc` パッケージが含まれていないこと
- [ ] `Program.cs` に `public partial class Program;` が含まれていること
- [ ] ディレクトリ構成が AGENTS.md §2.2 に準拠していること
- [ ] **TODO/FIXME/HACK コメント残存チェック**: ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" SalesManagementService/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック**: テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック**: 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 2: エンティティ & DbContext

### 目的

ドメインモデル（8 エンティティ + 6 列挙型）と SalesDbContext を定義する。EF Core マイグレーションで初期スキーマを生成する。

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `SalesManagementService/Models/Enums/OrderStatus.cs` | 注文ステータス列挙型（11 値） |
| 2 | `SalesManagementService/Models/Enums/PaymentStatus.cs` | 決済ステータス列挙型（6 値） |
| 3 | `SalesManagementService/Models/Enums/ShipmentStatus.cs` | 配送ステータス列挙型（5 値） |
| 4 | `SalesManagementService/Models/Enums/ReturnReason.cs` | 返品理由列挙型（6 値） |
| 5 | `SalesManagementService/Models/Enums/ReturnStatus.cs` | 返品ステータス列挙型（6 値） |
| 6 | `SalesManagementService/Models/Enums/InvoiceStatus.cs` | 請求書ステータス列挙型（5 値） |
| 7 | `SalesManagementService/Models/Order.cs` | Order エンティティ（Aggregate Root） |
| 8 | `SalesManagementService/Models/OrderItem.cs` | OrderItem エンティティ |
| 9 | `SalesManagementService/Models/Shipment.cs` | Shipment エンティティ |
| 10 | `SalesManagementService/Models/Return.cs` | Return エンティティ |
| 11 | `SalesManagementService/Models/Invoice.cs` | Invoice エンティティ |
| 12 | `SalesManagementService/Models/SagaLog.cs` | SagaLog エンティティ |
| 13 | `SalesManagementService/Models/OutboxEvent.cs` | OutboxEvent エンティティ |
| 14 | `SalesManagementService/Models/IdempotencyKey.cs` | IdempotencyKey エンティティ |
| 15 | `SalesManagementService/Infrastructure/Persistence/SalesDbContext.cs` | DbContext 完全定義 |

### 2.1 列挙型（6 ファイル）

設計書 §A の列挙型定義に準拠。DB カラムは文字列型（`UPPER_CASE`）で保存するため、列挙型は参照用。

```csharp
// Models/Enums/OrderStatus.cs
public enum OrderStatus
{
    Pending,
    Confirmed,
    Processing,
    Shipped,
    Delivered,
    Returned,
    Refunded,
    Cancelled,
    InventoryShortage,
    PaymentFailed,
    PendingPayment
}
```

> 他の 5 列挙型（PaymentStatus, ShipmentStatus, ReturnReason, ReturnStatus, InvoiceStatus）も同様に設計書 §A に準拠して作成する。

### 2.2 Order エンティティ（Aggregate Root）

設計書 §A `Order エンティティ` に完全準拠。

```csharp
[Table("orders")]
public class Order
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("order_number")]
    [Required]
    [MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty;

    [Column("customer_id")]
    [Required]
    [MaxLength(100)]
    public string CustomerId { get; set; } = string.Empty;

    [Column("order_date")]
    public DateTimeOffset OrderDate { get; set; } = DateTimeOffset.UtcNow;

    [Column("status")]
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "PENDING";

    [Column("payment_status")]
    [Required]
    [MaxLength(20)]
    public string PaymentStatus { get; set; } = "PENDING";

    [Column("payment_method")]
    [Required]
    [MaxLength(50)]
    public string PaymentMethod { get; set; } = string.Empty;

    [Column("subtotal_amount")]
    [Precision(12, 2)]
    public decimal SubtotalAmount { get; set; }

    [Column("tax_amount")]
    [Precision(12, 2)]
    public decimal TaxAmount { get; set; }

    [Column("shipping_fee")]
    [Precision(12, 2)]
    public decimal ShippingFee { get; set; }

    [Column("discount_amount")]
    [Precision(12, 2)]
    public decimal DiscountAmount { get; set; }

    [Column("total_amount")]
    [Precision(12, 2)]
    public decimal TotalAmount { get; set; }

    [Column("coupon_code")]
    [MaxLength(50)]
    public string? CouponCode { get; set; }

    [Column("used_points")]
    public int UsedPoints { get; set; }

    [Column("point_discount_amount")]
    [Precision(12, 2)]
    public decimal PointDiscountAmount { get; set; }

    [Column("shipping_postal_code")]
    [MaxLength(10)]
    public string? ShippingPostalCode { get; set; }

    [Column("shipping_prefecture")]
    [MaxLength(50)]
    public string? ShippingPrefecture { get; set; }

    [Column("shipping_city")]
    [MaxLength(100)]
    public string? ShippingCity { get; set; }

    [Column("shipping_address_line1")]
    [MaxLength(200)]
    public string? ShippingAddressLine1 { get; set; }

    [Column("shipping_address_line2")]
    [MaxLength(200)]
    public string? ShippingAddressLine2 { get; set; }

    [Column("shipping_recipient_name")]
    [MaxLength(100)]
    public string? ShippingRecipientName { get; set; }

    [Column("shipping_phone_number")]
    [MaxLength(20)]
    public string? ShippingPhoneNumber { get; set; }

    [Column("currency_code")]
    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "JPY";

    [Column("is_guest")]
    public bool IsGuest { get; set; }

    [Column("guest_email")]
    [MaxLength(255)]
    public string? GuestEmail { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_by")]
    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    [Column("updated_by")]
    [MaxLength(100)]
    public string? UpdatedBy { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    // ── ナビゲーションプロパティ ──
    public ICollection<OrderItem> Items { get; set; } = [];
    public ICollection<Shipment> Shipments { get; set; } = [];
    public ICollection<Return> Returns { get; set; } = [];
    public Invoice? Invoice { get; set; }
    public SagaLog? SagaLog { get; set; }

    // ── ドメインメソッド（Aggregate Root） ──
    public void AddItem(string productId, string productName, string sku,
        decimal unitPrice, int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        Items.Add(new OrderItem
        {
            OrderId = Id,
            ProductId = productId,
            ProductName = productName,
            Sku = sku,
            UnitPrice = unitPrice,
            Quantity = quantity,
            Subtotal = unitPrice * quantity
        });
    }
}
```

> 残りの 7 エンティティ（OrderItem, Shipment, Return, Invoice, SagaLog, OutboxEvent, IdempotencyKey）は設計書 §A に完全準拠して作成する。各エンティティの全プロパティ・属性は設計書のコード例をそのまま実装すること。

### 2.3 SalesDbContext

設計書 §B `AppDbContext 完全定義` に完全準拠。

```csharp
public class SalesDbContext(
    DbContextOptions<SalesDbContext> options,
    TimeProvider timeProvider) : DbContext(options)
{
    // ── DbSet プロパティ ──
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<Return> Returns => Set<Return>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<SagaLog> SagaLogs => Set<SagaLog>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Order ──
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(o => o.OrderNumber).IsUnique();
            entity.HasIndex(o => o.CustomerId);
            entity.HasIndex(o => o.Status);
            entity.HasIndex(o => o.OrderDate);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_orders_status",
                    "status IN ('PENDING','CONFIRMED','PROCESSING','SHIPPED','DELIVERED','RETURNED','REFUNDED','CANCELLED','INVENTORY_SHORTAGE','PAYMENT_FAILED','PENDING_PAYMENT')");
                t.HasCheckConstraint(
                    "ck_orders_payment_status",
                    "payment_status IN ('PENDING','AUTHORIZED','CAPTURED','FAILED','REFUNDED','PARTIALLY_REFUNDED')");
                t.HasCheckConstraint("ck_orders_subtotal_amount", "subtotal_amount >= 0");
                t.HasCheckConstraint("ck_orders_tax_amount", "tax_amount >= 0");
                t.HasCheckConstraint("ck_orders_shipping_fee", "shipping_fee >= 0");
                t.HasCheckConstraint("ck_orders_discount_amount", "discount_amount >= 0");
                t.HasCheckConstraint("ck_orders_total_amount", "total_amount >= 0");
                t.HasCheckConstraint("ck_orders_used_points", "used_points >= 0");
                t.HasCheckConstraint("ck_orders_point_discount_amount", "point_discount_amount >= 0");
            });

            entity.HasMany(o => o.Items)
                .WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(o => o.Shipments)
                .WithOne(s => s.Order)
                .HasForeignKey(s => s.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(o => o.Returns)
                .WithOne(r => r.Order)
                .HasForeignKey(r => r.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.Invoice)
                .WithOne(i => i.Order)
                .HasForeignKey<Invoice>(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(o => o.SagaLog)
                .WithOne(s => s.Order)
                .HasForeignKey<SagaLog>(s => s.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── OrderItem ──
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasIndex(i => i.ProductId);
            entity.Property(i => i.ProductSnapshot).HasColumnType("jsonb");
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_order_items_unit_price", "unit_price >= 0");
                t.HasCheckConstraint("ck_order_items_quantity", "quantity > 0");
                t.HasCheckConstraint("ck_order_items_subtotal", "subtotal >= 0");
                t.HasCheckConstraint("ck_order_items_coupon_discount_amount", "coupon_discount_amount >= 0");
                t.HasCheckConstraint("ck_order_items_used_points", "used_points >= 0");
                t.HasCheckConstraint("ck_order_items_point_discount_amount", "point_discount_amount >= 0");
            });
        });

        // ── Shipment ──
        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.HasIndex(s => s.OrderId);
            entity.HasIndex(s => s.TrackingNumber);
            entity.HasIndex(s => s.Status);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_shipments_status",
                    "status IN ('PREPARING','SHIPPED','IN_TRANSIT','DELIVERED','FAILED')");
            });
        });

        // ── Return ──
        modelBuilder.Entity<Return>(entity =>
        {
            entity.HasIndex(r => r.ReturnNumber).IsUnique();
            entity.HasIndex(r => r.OrderId);
            entity.HasIndex(r => r.Status);
            entity.HasOne(r => r.OrderItem)
                .WithMany()
                .HasForeignKey(r => r.OrderItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_returns_reason",
                    "reason IN ('DEFECTIVE','WRONG_ITEM','SIZE_MISMATCH','NOT_AS_DESCRIBED','CHANGED_MIND','OTHER')");
                t.HasCheckConstraint(
                    "ck_returns_status",
                    "status IN ('REQUESTED','APPROVED','REJECTED','RECEIVED','REFUNDED','CLOSED')");
                t.HasCheckConstraint("ck_returns_quantity", "quantity > 0");
                t.HasCheckConstraint("ck_returns_refund_amount", "refund_amount >= 0");
            });
        });

        // ── Invoice ──
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasIndex(i => i.InvoiceNumber).IsUnique();
            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_invoices_status",
                    "status IN ('DRAFT','ISSUED','PAID','OVERDUE','CANCELLED')");
                t.HasCheckConstraint("ck_invoices_amount", "amount >= 0");
            });
        });

        // ── SagaLog ──
        modelBuilder.Entity<SagaLog>(entity =>
        {
            entity.HasIndex(s => s.OrderId);
            entity.HasIndex(s => s.UpdatedAt)
                .HasFilter("status = 'PROCESSING'");
            entity.HasIndex(s => s.UpdatedAt)
                .HasDatabaseName("idx_saga_logs_pending_payment")
                .HasFilter("status = 'PENDING_PAYMENT'");
            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_saga_logs_saga_type",
                    "saga_type IN ('ORDER_CHECKOUT','ORDER_CANCEL','ORDER_RETURN')");
                t.HasCheckConstraint(
                    "ck_saga_logs_status",
                    "status IN ('CREATED','PROCESSING','COMPLETED','COMPENSATING','COMPENSATED','FAILED','PENDING_PAYMENT')");
                t.HasCheckConstraint("ck_saga_logs_current_step", "current_step >= 0");
                t.HasCheckConstraint("ck_saga_logs_retry_count", "retry_count >= 0");
            });
        });

        // ── OutboxEvent ──
        modelBuilder.Entity<OutboxEvent>(entity =>
        {
            entity.HasIndex(e => e.CreatedAt)
                .HasFilter("status = 'PENDING'");
            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_outbox_events_status",
                    "status IN ('PENDING','PROCESSING','PUBLISHED','FAILED','DEAD_LETTER')");
                t.HasCheckConstraint("ck_outbox_events_retry_count", "retry_count >= 0");
            });
        });

        // ── IdempotencyKey ──
        modelBuilder.Entity<IdempotencyKey>(entity =>
        {
            entity.HasIndex(k => new { k.Key, k.UserId }).IsUnique();
            entity.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "ck_idempotency_keys_request_status",
                    "request_status IN ('PENDING','PROCESSING','COMPLETED')");
            });
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        foreach (var entry in ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            switch (entry.Entity)
            {
                case Order order:
                    if (entry.State == EntityState.Added) order.CreatedAt = now;
                    order.UpdatedAt = now;
                    break;
                case Shipment shipment:
                    if (entry.State == EntityState.Added) shipment.CreatedAt = now;
                    shipment.UpdatedAt = now;
                    break;
                case Return ret:
                    if (entry.State == EntityState.Added) ret.CreatedAt = now;
                    ret.UpdatedAt = now;
                    break;
                case Invoice invoice:
                    if (entry.State == EntityState.Added) invoice.CreatedAt = now;
                    invoice.UpdatedAt = now;
                    break;
                case SagaLog saga:
                    if (entry.State == EntityState.Added) saga.CreatedAt = now;
                    saga.UpdatedAt = now;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
```

### 2.4 Program.cs への追加（DbContext 登録）

```csharp
// Phase 2 で追加
builder.Services.AddDbContext<SalesDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("salesdb")));
```

### Phase 2 完了チェックリスト

- [ ] `dotnet build` が成功すること
- [ ] `dotnet ef migrations add Initial` が成功すること
- [ ] 全エンティティに `[Table("snake_case")]` + `[Column("snake_case")]` 属性が付与されていること
- [ ] コレクションナビゲーションが `= []` で初期化されていること
- [ ] 楽観的ロックが必要なエンティティ（Order, Shipment, Return, SagaLog）に `[Timestamp]` が付与されていること
- [ ] Order エンティティに `IsGuest`（bool）と `GuestEmail`（AES-256-GCM 暗号化、MaxLength 255）プロパティが存在すること（設計書 §A 準拠）
- [ ] `DateTime.Now` が使用されていないこと（`DateTimeOffset.UtcNow` を使用）
- [ ] `SaveChangesAsync` で `CreatedAt` / `UpdatedAt` が `TimeProvider` 経由で設定されていること
- [ ] 全 CHECK 制約が設計書 §B に準拠していること
- [ ] 部分インデックス（OutboxEvent PENDING, SagaLog PROCESSING / PENDING_PAYMENT）が設定されていること
- [ ] `OnDelete` の動作（Cascade / Restrict）が設計書 §B に準拠していること
- [ ] **TODO/FIXME/HACK コメント残存チェック**: ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" SalesManagementService/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック**: テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック**: 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 3: Repository 層

### 目的

6 つの Repository インターフェースと実装を作成する。Aggregate Root 単位の原則を遵守する。

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `SalesManagementService/Repositories/Interfaces/IOrderRepository.cs` | 注文 Repository インターフェース |
| 2 | `SalesManagementService/Repositories/Interfaces/IShipmentRepository.cs` | 配送 Repository インターフェース |
| 3 | `SalesManagementService/Repositories/Interfaces/IReturnRepository.cs` | 返品 Repository インターフェース |
| 4 | `SalesManagementService/Repositories/Interfaces/ISagaLogRepository.cs` | Saga ログ Repository インターフェース |
| 5 | `SalesManagementService/Repositories/Interfaces/IOutboxEventRepository.cs` | Outbox イベント Repository インターフェース |
| 6 | `SalesManagementService/Repositories/Interfaces/IIdempotencyKeyRepository.cs` | べき等キー Repository インターフェース |
| 7 | `SalesManagementService/Repositories/OrderRepository.cs` | 注文 Repository 実装 |
| 8 | `SalesManagementService/Repositories/ShipmentRepository.cs` | 配送 Repository 実装 |
| 9 | `SalesManagementService/Repositories/ReturnRepository.cs` | 返品 Repository 実装 |
| 10 | `SalesManagementService/Repositories/SagaLogRepository.cs` | Saga ログ Repository 実装 |
| 11 | `SalesManagementService/Repositories/OutboxEventRepository.cs` | Outbox イベント Repository 実装 |
| 12 | `SalesManagementService/Repositories/IdempotencyKeyRepository.cs` | べき等キー Repository 実装 |

### 3.1 Repository インターフェース

設計書 §E に完全準拠。全メソッドに `CancellationToken ct = default` を含める。

```csharp
// Repositories/Interfaces/IOrderRepository.cs
public interface IOrderRepository
{
    Task<Order?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<Order?> FindByIdWithDetailsAsync(string id, CancellationToken ct = default);
    Task<Order?> FindByOrderNumberAsync(string orderNumber, CancellationToken ct = default);
    Task<PaginatedResult<Order>> FindByCustomerIdAsync(
        string customerId, int page, int pageSize, CancellationToken ct = default);
    Task<PaginatedResult<Order>> SearchAsync(
        string? customerId, string? status, string? paymentStatus,
        int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

> 残りの 5 インターフェース（IShipmentRepository, IReturnRepository, ISagaLogRepository, IOutboxEventRepository, IIdempotencyKeyRepository）は設計書 §E に完全準拠して作成する。

### 3.2 Repository 実装例（OrderRepository）

```csharp
public class OrderRepository(SalesDbContext context) : IOrderRepository
{
    public async Task<Order?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<Order?> FindByIdWithDetailsAsync(string id, CancellationToken ct = default)
        => await context.Orders
            .Include(o => o.Items)
            .Include(o => o.Shipments)
            .Include(o => o.Returns)
            .Include(o => o.Invoice)
            .Include(o => o.SagaLog)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<Order?> FindByOrderNumberAsync(string orderNumber, CancellationToken ct = default)
        => await context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, ct);

    public async Task<PaginatedResult<Order>> FindByCustomerIdAsync(
        string customerId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.Orders
            .AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.OrderDate);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PaginatedResult<Order>(items, totalCount, page, pageSize);
    }

    public async Task<PaginatedResult<Order>> SearchAsync(
        string? customerId, string? status, string? paymentStatus,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.Orders.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(customerId))
            query = query.Where(o => o.CustomerId == customerId);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(o => o.Status == status);
        if (!string.IsNullOrWhiteSpace(paymentStatus))
            query = query.Where(o => o.PaymentStatus == paymentStatus);

        query = query.OrderByDescending(o => o.OrderDate);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PaginatedResult<Order>(items, totalCount, page, pageSize);
    }

    public async Task AddAsync(Order order, CancellationToken ct = default)
        => await context.Orders.AddAsync(order, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
```

### 3.3 Program.cs への追加（Repository DI 登録）

```csharp
// Phase 3 で追加
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IShipmentRepository, ShipmentRepository>();
builder.Services.AddScoped<IReturnRepository, ReturnRepository>();
builder.Services.AddScoped<ISagaLogRepository, SagaLogRepository>();
builder.Services.AddScoped<IOutboxEventRepository, OutboxEventRepository>();
builder.Services.AddScoped<IIdempotencyKeyRepository, IdempotencyKeyRepository>();
```

### Phase 3 完了チェックリスト

- [ ] `dotnet build` が成功すること
- [ ] 全 Repository が Aggregate Root 単位であること（異なる Aggregate のクエリが混在していない）
- [ ] 全メソッドに `CancellationToken ct = default` が含まれていること
- [ ] 読み取りクエリに `AsNoTracking()` が使用されていること
- [ ] N+1 対策として `Include()` / `AsSplitQuery()` が適切に使用されていること
- [ ] ページネーションが `Skip/Take` で実装されていること
- [ ] primary constructor による DI が使用されていること
- [ ] `FromSqlRaw` での文字列結合が使用されていないこと（`FromSqlInterpolated` のみ許可）
- [ ] **TODO/FIXME/HACK コメント残存チェック**: ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" SalesManagementService/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック**: テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック**: 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 4: Service 層

### 目的

ビジネスロジック層を構築する。OrderService, ShipmentService, ReturnService, ReportService、および OrderStateMachine, TaxCalculator, ShippingFeeCalculator, OrderNumberGenerator を実装する。

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `SalesManagementService/Services/Interfaces/IOrderService.cs` | 注文 Service インターフェース |
| 2 | `SalesManagementService/Services/Interfaces/IShipmentService.cs` | 配送 Service インターフェース |
| 3 | `SalesManagementService/Services/Interfaces/IReturnService.cs` | 返品 Service インターフェース |
| 4 | `SalesManagementService/Services/Interfaces/IReportService.cs` | レポート Service インターフェース |
| 5 | `SalesManagementService/Services/OrderService.cs` | 注文 Service 実装 |
| 6 | `SalesManagementService/Services/ShipmentService.cs` | 配送 Service 実装 |
| 7 | `SalesManagementService/Services/ReturnService.cs` | 返品 Service 実装 |
| 8 | `SalesManagementService/Services/ReportService.cs` | レポート Service 実装 |
| 9 | `SalesManagementService/Services/OrderStateMachine.cs` | 注文ステータス遷移バリデーション |
| 10 | `SalesManagementService/Services/TaxCalculator.cs` | 消費税計算（10% 標準、8% 軽減、切り捨て） |
| 11 | `SalesManagementService/Services/ShippingFeeCalculator.cs` | 配送料計算（会員ランク別閾値、地域別料金、お急ぎ便・大型商品加算） |
| 12 | `SalesManagementService/Services/OrderNumberGenerator.cs` | 注文番号生成（`ORD-yyyyMMdd-NNNNN` 形式） |
| 13 | `SalesManagementService/DTOs/Requests/*.cs` | リクエスト DTO（record 型） |
| 14 | `SalesManagementService/DTOs/Responses/*.cs` | レスポンス DTO（record 型） |
| 15 | `SalesManagementService/Infrastructure/Exceptions/*.cs` | カスタム例外クラス群 |

### 4.1 DTO 定義

設計書 §C に完全準拠。全 DTO を record 型で定義する。

```csharp
// DTOs/Responses/PaginatedResult.cs
public record PaginatedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

// DTOs/Requests/OrderCreateRequest.cs
public record OrderCreateRequest(
    [Required] string CustomerId,
    [Required, MinLength(1)] IReadOnlyList<OrderItemRequest> Items,
    [Required] ShippingAddressRequest ShippingAddress,
    [Required, StringLength(50)] string PaymentMethod,
    [StringLength(50)] string? CouponCode,
    [Range(0, int.MaxValue)] int UsedPoints = 0,
    [StringLength(500)] string? Notes = null);

// DTOs/Requests/OrderItemRequest.cs
public record OrderItemRequest(
    [Required, StringLength(100)] string ProductId,
    [Required, StringLength(200)] string ProductName,
    [Required, StringLength(100)] string Sku,
    [Range(0.01, double.MaxValue)] decimal UnitPrice,
    [Range(1, 99)] int Quantity);

// DTOs/Requests/ShippingAddressRequest.cs
public record ShippingAddressRequest(
    [Required, StringLength(100)] string RecipientName,
    [Required, StringLength(10)] string PostalCode,
    [Required, StringLength(50)] string Prefecture,
    [Required, StringLength(100)] string City,
    [Required, StringLength(200)] string AddressLine1,
    [StringLength(200)] string? AddressLine2,
    [Required, StringLength(20)] string PhoneNumber);

// DTOs/Requests/OrderCancelRequest.cs
public record OrderCancelRequest(
    [Required, StringLength(500)] string Reason);

// DTOs/Requests/OrderStatusUpdateRequest.cs
public record OrderStatusUpdateRequest(
    [Required, StringLength(20)] string Status);
```

> 残りの DTO（ShipmentCreateRequest, ShipmentUpdateRequest, ReturnCreateRequest, ReturnProcessRequest, OrderDto, OrderDetailDto, OrderItemDto, ShipmentDto, ReturnDto, InvoiceDto, SalesReportDto, DailySalesDto）は設計書 §C に完全準拠して作成する。

### 4.2 FluentValidation バリデーター

設計書 §D に完全準拠。

```csharp
// Infrastructure/Validators/OrderCreateRequestValidator.cs
public class OrderCreateRequestValidator : AbstractValidator<OrderCreateRequest>
{
    public OrderCreateRequestValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("顧客IDは必須です")
            .MaximumLength(100);

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("注文明細は1件以上必要です")
            .Must(items => items.Count <= 50).WithMessage("注文明細は50件以下にしてください");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty().WithMessage("商品IDは必須です");
            item.RuleFor(i => i.ProductName).NotEmpty().MaximumLength(200);
            item.RuleFor(i => i.Sku).NotEmpty().MaximumLength(100);
            item.RuleFor(i => i.UnitPrice).GreaterThan(0);
            item.RuleFor(i => i.Quantity).InclusiveBetween(1, 99);
        });

        RuleFor(x => x.ShippingAddress).NotNull().WithMessage("配送先住所は必須です");

        When(x => x.ShippingAddress is not null, () =>
        {
            RuleFor(x => x.ShippingAddress.RecipientName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.ShippingAddress.PostalCode)
                .NotEmpty()
                .Matches(@"^\d{3}-?\d{4}$").WithMessage("郵便番号の形式が不正です（例: 100-0001）");
            RuleFor(x => x.ShippingAddress.Prefecture).NotEmpty();
            RuleFor(x => x.ShippingAddress.City).NotEmpty();
            RuleFor(x => x.ShippingAddress.AddressLine1).NotEmpty();
            RuleFor(x => x.ShippingAddress.PhoneNumber)
                .NotEmpty()
                .Matches(@"^[\d\-]+$").WithMessage("電話番号の形式が不正です");
        });

        RuleFor(x => x.PaymentMethod).NotEmpty().MaximumLength(50);
        RuleFor(x => x.UsedPoints).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CouponCode).MaximumLength(50).When(x => x.CouponCode is not null);
    }
}
```

> 残りのバリデーター（ReturnCreateRequestValidator, ShipmentCreateRequestValidator, ShipmentUpdateRequestValidator）は設計書 §D に完全準拠して作成する。

### 4.3 カスタム例外クラス

```csharp
// Infrastructure/Exceptions/NotFoundException.cs
public class NotFoundException(string message) : Exception(message);

// Infrastructure/Exceptions/BusinessException.cs
public class BusinessException(string message) : Exception(message);

// Infrastructure/Exceptions/UnauthorizedException.cs
public class UnauthorizedException() : Exception("認証が必要です");

// Infrastructure/Exceptions/ForbiddenException.cs
public class ForbiddenException() : Exception("アクセスが拒否されました");

// Infrastructure/Exceptions/ConcurrencyException.cs
public class ConcurrencyException(string message) : Exception(message);

// Infrastructure/Exceptions/InsufficientStockException.cs
public class InsufficientStockException(string message) : BusinessException(message);

// Infrastructure/Exceptions/PaymentProcessingException.cs
public class PaymentProcessingException(string message) : BusinessException(message);

// Infrastructure/Exceptions/PaymentPendingException.cs
public class PaymentPendingException(string message) : BusinessException(message);

// Infrastructure/Exceptions/InvalidOrderStateException.cs
public class InvalidOrderStateException(string message) : BusinessException(message);

// Infrastructure/Exceptions/IdempotencyConflictException.cs
public class IdempotencyConflictException(string message) : BusinessException(message);

// Infrastructure/Exceptions/ExternalServiceException.cs
public class ExternalServiceException(string message, Exception? inner = null)
    : Exception(message, inner);
```

### 4.4 Service インターフェース

設計書 §F に完全準拠。

```csharp
// Services/Interfaces/IOrderService.cs
public interface IOrderService
{
    Task<OrderDetailDto> CreateOrderAsync(
        OrderCreateRequest request, SagaContext context, CancellationToken ct = default);
    Task<OrderDetailDto?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<OrderDetailDto?> GetByIdAndUserIdAsync(
        string id, string userId, CancellationToken ct = default);
    Task<OrderDetailDto?> GetByOrderNumberAsync(
        string orderNumber, CancellationToken ct = default);
    Task<PaginatedResult<OrderDto>> GetByCustomerIdAsync(
        string customerId, int page, int pageSize, CancellationToken ct = default);
    Task<PaginatedResult<OrderDto>> SearchAsync(
        string? customerId, string? status, string? paymentStatus,
        int page, int pageSize, CancellationToken ct = default);
    Task CancelOrderAsync(string orderId, string reason, CancellationToken ct = default);
    Task UpdateStatusAsync(string orderId, string newStatus, CancellationToken ct = default);
}
```

> 残りの Service インターフェース（IShipmentService, IReturnService, IReportService, ISagaCoordinator, IOutboxWriter）は設計書 §F に完全準拠して作成する。

### 4.5 OrderStateMachine

設計書 §I の状態遷移図に基づく遷移バリデーション。

```csharp
// Services/OrderStateMachine.cs
public static class OrderStateMachine
{
    private static readonly Dictionary<string, HashSet<string>> ValidTransitions = new()
    {
        ["PENDING"] = ["CONFIRMED", "INVENTORY_SHORTAGE", "PAYMENT_FAILED", "PENDING_PAYMENT", "CANCELLED"],
        ["PENDING_PAYMENT"] = ["CONFIRMED", "CANCELLED"],
        ["CONFIRMED"] = ["PROCESSING", "CANCELLED"],
        ["PROCESSING"] = ["SHIPPED", "CANCELLED"],
        ["SHIPPED"] = ["DELIVERED", "RETURNED"],
        ["DELIVERED"] = ["RETURNED"],
        ["RETURNED"] = ["REFUNDED"],
        ["CANCELLED"] = ["REFUNDED"],
    };

    public static bool CanTransition(string currentStatus, string newStatus)
        => ValidTransitions.TryGetValue(currentStatus, out var validNext)
           && validNext.Contains(newStatus);

    public static void ValidateTransition(string currentStatus, string newStatus)
    {
        if (!CanTransition(currentStatus, newStatus))
            throw new InvalidOrderStateException(
                $"注文ステータスを '{currentStatus}' から '{newStatus}' に変更できません");
    }
}
```

### 4.6 TaxCalculator

```csharp
// Services/TaxCalculator.cs
public static class TaxCalculator
{
    private const decimal StandardTaxRate = 0.10m;
    private const decimal ReducedTaxRate = 0.08m;

    public static decimal CalculateStandardTax(decimal amount)
        => Math.Floor(amount * StandardTaxRate);

    public static decimal CalculateReducedTax(decimal amount)
        => Math.Floor(amount * ReducedTaxRate);
}
```

### 4.7 ShippingFeeCalculator

設計書 §5 配送料計算ルールに完全準拠。会員ランク別送料無料閾値、地域別料金、お急ぎ便・大型商品加算を実装する。

```csharp
// Services/ShippingFeeCalculator.cs
public static class ShippingFeeCalculator
{
    private const decimal DefaultShippingFee = 550m;
    private const decimal HokkaidoOkinawaFee = 1100m;
    private const decimal ExpressShippingSurcharge = 330m;
    private const decimal LargeItemSurcharge = 1650m;

    private static readonly Dictionary<string, decimal> FreeShippingThresholds = new()
    {
        ["Standard"] = 10000m,
        ["Silver"] = 8000m,
        ["Gold"] = 5000m,
        ["Platinum"] = 0m
    };

    private static readonly HashSet<string> RemotePrefectures = ["北海道", "沖縄県"];

    public static decimal Calculate(
        decimal orderAmount,
        string memberRank,
        string prefecture,
        bool isExpress = false,
        bool isLargeItem = false)
    {
        // Platinum 会員は常時送料無料
        if (memberRank == "Platinum")
            return 0m;

        // 会員ランク別の送料無料閾値チェック
        var threshold = FreeShippingThresholds.GetValueOrDefault(memberRank, 10000m);
        if (orderAmount >= threshold)
            return 0m;

        // 基本送料（地域別）
        var baseFee = RemotePrefectures.Contains(prefecture)
            ? HokkaidoOkinawaFee
            : DefaultShippingFee;

        // お急ぎ便加算
        if (isExpress) baseFee += ExpressShippingSurcharge;

        // 大型商品加算（重量 10kg 超 or 長さ 170cm 超）
        if (isLargeItem) baseFee += LargeItemSurcharge;

        return baseFee;
    }
}
```

### 4.8 OrderNumberGenerator

注文番号生成ロジック。`ORD-yyyyMMdd-NNNNN` 形式で一意な注文番号を生成する。

```csharp
// Services/OrderNumberGenerator.cs
public class OrderNumberGenerator(
    SalesDbContext context,
    TimeProvider timeProvider,
    ILogger<OrderNumberGenerator> logger)
{
    public async Task<string> GenerateAsync(CancellationToken ct = default)
    {
        var today = timeProvider.GetUtcNow().ToString("yyyyMMdd");
        var prefix = $"ORD-{today}-";

        var lastOrder = await context.Orders
            .Where(o => o.OrderNumber.StartsWith(prefix))
            .OrderByDescending(o => o.OrderNumber)
            .Select(o => o.OrderNumber)
            .FirstOrDefaultAsync(ct);

        var sequence = 1;
        if (lastOrder is not null)
        {
            var lastSequence = lastOrder[(prefix.Length)..];
            if (int.TryParse(lastSequence, out var parsed))
                sequence = parsed + 1;
        }

        var orderNumber = $"{prefix}{sequence:D5}";
        logger.LogInformation("注文番号生成: {OrderNumber}", orderNumber);
        return orderNumber;
    }
}
```

### 4.9 Program.cs への追加（Service DI 登録）

```csharp
// Phase 4 で追加
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IShipmentService, ShipmentService>();
builder.Services.AddScoped<IReturnService, ReturnService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<OrderNumberGenerator>();

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<OrderCreateRequestValidator>();
```

### Phase 4 完了チェックリスト

- [ ] `dotnet build` が成功すること
- [ ] 全 Service が primary constructor で DI を受けていること
- [ ] 全 async メソッドに `CancellationToken ct = default` が含まれていること
- [ ] `Console.WriteLine` が使用されていないこと（`ILogger<T>` を使用）
- [ ] Service が Repository を直接 `new` していないこと（DI 経由）
- [ ] ログ出力がメッセージテンプレート形式であること（文字列補間禁止）
- [ ] OrderStateMachine が設計書 §I の全遷移をカバーしていること
- [ ] ShippingFeeCalculator が設計書 §5 配送料計算ルール（会員ランク別閾値: Silver ≥8000/Gold ≥5000/Platinum 常時無料、地域別: 北海道・沖縄 1100 円、お急ぎ便 +330 円、大型商品 +1650 円）を網羅していること
- [ ] OrderNumberGenerator が `ORD-yyyyMMdd-NNNNN` 形式で注文番号を生成できること
- [ ] PII（配送先住所、電話番号、宛名）がログに出力されていないこと
- [ ] 例外処理で `catch (Exception) { }` の握りつぶしがないこと
- [ ] `DbUpdateConcurrencyException` が `ConcurrencyException` に変換されていること
- [ ] **TODO/FIXME/HACK コメント残存チェック**: ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" SalesManagementService/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック**: テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック**: 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 5: Endpoints（Minimal API）

### 目的

REST API エンドポイントを Minimal API で実装する。設計書の全 23 エンドポイントを IEndpointRouteBuilder 拡張メソッドで分離する。

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `SalesManagementService/Endpoints/OrderEndpoints.cs` | 注文管理 API（7 エンドポイント） |
| 2 | `SalesManagementService/Endpoints/ShipmentEndpoints.cs` | 配送管理 API（6 エンドポイント） |
| 3 | `SalesManagementService/Endpoints/ReturnEndpoints.cs` | 返品管理 API（5 エンドポイント） |
| 4 | `SalesManagementService/Endpoints/ReportEndpoints.cs` | レポート API（5 エンドポイント） |
| 5 | `SalesManagementService/DTOs/Requests/PaginationParams.cs` | ページネーションパラメータ |
| 6 | `SalesManagementService/DTOs/Requests/OrderSearchParams.cs` | 注文検索パラメータ |

### 5.1 OrderEndpoints

設計書 §G に完全準拠。IDOR 防止（ClaimsPrincipal）、Idempotency-Key ヘッダー必須化を含む。

```csharp
public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/orders")
            .WithTags("Orders")
            .RequireAuthorization()
            .WithOpenApi();

        group.MapGet("/{orderId}", GetOrderById).WithName("GetOrderById");
        group.MapGet("/number/{orderNumber}", GetOrderByNumber).WithName("GetOrderByNumber");
        group.MapGet("/customer/{customerId}", GetCustomerOrders).WithName("GetCustomerOrders");
        group.MapGet("/search", SearchOrders)
            .RequireAuthorization("AdminOnly")
            .WithName("SearchOrders");
        group.MapPost("/", CreateOrder).WithName("CreateOrder");
        group.MapPut("/{orderId}/status", UpdateOrderStatus)
            .RequireAuthorization("AdminOnly")
            .WithName("UpdateOrderStatus");
        group.MapPost("/{orderId}/cancel", CancelOrder).WithName("CancelOrder");
    }

    private static async Task<IResult> GetOrderById(
        string orderId,
        ClaimsPrincipal user,
        IOrderService orderService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        return await orderService.GetByIdAndUserIdAsync(orderId, userId, ct) is { } order
            ? Results.Ok(order)
            : Results.NotFound();
    }

    private static async Task<IResult> GetCustomerOrders(
        string customerId,
        [AsParameters] PaginationParams pagination,
        ClaimsPrincipal user,
        IOrderService orderService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        if (customerId != userId)
            throw new ForbiddenException();
        var result = await orderService.GetByCustomerIdAsync(
            customerId, pagination.Page, pagination.PageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateOrder(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] OrderCreateRequest request,
        IValidator<OrderCreateRequest> validator,
        ClaimsPrincipal user,
        ISagaCoordinator sagaCoordinator,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest(new { Error = "Idempotency-Key ヘッダーは必須です" });

        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        var order = await sagaCoordinator.ExecuteCheckoutSagaAsync(
            request, userId, idempotencyKey, ct);
        return Results.Created($"/api/v1/orders/{order.Id}", order);
    }

    private static async Task<IResult> CancelOrder(
        string orderId,
        [FromBody] OrderCancelRequest request,
        ClaimsPrincipal user,
        IOrderService orderService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        await orderService.CancelOrderAsync(orderId, request.Reason, ct);
        return Results.NoContent();
    }
}

// ── クエリパラメータ用 record ──
public record PaginationParams(int Page = 1, int PageSize = 20);

public record OrderSearchParams(
    string? CustomerId,
    string? Status,
    string? PaymentStatus,
    int Page = 1,
    int PageSize = 20);
```

> 残りの Endpoints（ShipmentEndpoints, ReturnEndpoints, ReportEndpoints）は設計書 §G の API テーブルに準拠して作成する。配送・返品 Endpoints には管理者認可（`RequireAuthorization("AdminOnly")`）を適用する。

### 5.2 Program.cs への追加（エンドポイントマッピング）

```csharp
// Phase 5 で追加（ミドルウェアパイプライン後に配置）
app.MapOrderEndpoints();
app.MapShipmentEndpoints();
app.MapReturnEndpoints();
app.MapReportEndpoints();
```

### Phase 5 完了チェックリスト

- [ ] `dotnet build` が成功すること
- [ ] 全エンドポイントに認可設定（`.RequireAuthorization()` / `.AllowAnonymous()`）が明示されていること
- [ ] POST `/api/v1/orders` に `Idempotency-Key` ヘッダーチェックがあること
- [ ] IDOR 防止: `GetOrderById`, `GetCustomerOrders`, `CancelOrder` で `ClaimsPrincipal` によるオーナーシップ検証があること
- [ ] 管理者専用エンドポイント（ステータス更新、検索）に `RequireAuthorization("AdminOnly")` があること
- [ ] `CreateOrder` で FluentValidation による入力検証があること
- [ ] エンドポイントにビジネスロジックが含まれていないこと（Service 層に委譲）
- [ ] 全エンドポイントに `.WithOpenApi()` が付与されていること
- [ ] EF Core エンティティを直接 `[FromBody]` で受け取っていないこと（DTO 経由）
- [ ] 全エンドポイントに `CancellationToken ct` が含まれていること
- [ ] **TODO/FIXME/HACK コメント残存チェック**: ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" SalesManagementService/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック**: テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック**: 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 6: Kafka + Outbox パターン

### 目的

Outbox パターンによるイベント発行保証と Kafka Consumer（イベント購読）を実装する。OutboxPublisher BackgroundService（Advisory Lock + 動的バックオフ）、OutboxWriter、および 5 つの Kafka Consumer を実装する。

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `SalesManagementService/Infrastructure/Outbox/OutboxWriter.cs` | Outbox テーブルへのイベント書き込み |
| 2 | `SalesManagementService/Infrastructure/Outbox/OutboxPublisher.cs` | Outbox → Kafka 発行 BackgroundService |
| 3 | `SalesManagementService/Infrastructure/Outbox/OutboxCleanupService.cs` | PUBLISHED イベントの 7 日後クリーンアップ（定期ジョブ） |
| 4 | `SalesManagementService/Services/Interfaces/IOutboxWriter.cs` | Outbox Writer インターフェース |
| 5 | `SalesManagementService/Infrastructure/Kafka/PaymentCompletedConsumer.cs` | `payment.completed` 購読 — 決済ステータス更新、注文ステータスを CONFIRMED に設定 |
| 6 | `SalesManagementService/Infrastructure/Kafka/PaymentFailedConsumer.cs` | `payment.failed` 購読 — 決済ステータスを FAILED、注文ステータスを PAYMENT_FAILED に更新 |
| 7 | `SalesManagementService/Infrastructure/Kafka/InventoryReservedConsumer.cs` | `inventory.reserved` 購読 — Saga ステップ 2 の成功通知（gRPC 応答処理済みの場合は No-op） |
| 8 | `SalesManagementService/Infrastructure/Kafka/InventoryReleasedConsumer.cs` | `inventory.released` 購読 — 補償トランザクション完了の確認記録 |
| 9 | `SalesManagementService/Infrastructure/Kafka/UserDeletedConsumer.cs` | `user.deleted` 購読 — 注文データの PII 匿名化処理（GDPR 第 17 条対応） |
| 10 | `SalesManagementService/Infrastructure/Maintenance/IdempotencyKeyCleanupService.cs` | 期限切れ IdempotencyKey の日次クリーンアップ |
| 11 | `SalesManagementService/Infrastructure/Maintenance/SagaLogArchivalService.cs` | COMPLETED/COMPENSATED の SagaLog を 30 日後にアーカイブ |

### 6.1 OutboxWriter

```csharp
public class OutboxWriter(SalesDbContext context) : IOutboxWriter
{
    public async Task WriteAsync<T>(string eventType, string aggregateId,
        T payload, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var outboxEvent = new OutboxEvent
        {
            EventType = eventType,
            AggregateId = aggregateId,
            Payload = JsonSerializer.Serialize(payload),
            Status = "PENDING"
        };
        await context.OutboxEvents.AddAsync(outboxEvent, ct);
        await context.SaveChangesAsync(ct);
    }
}
```

### 6.2 OutboxPublisher

設計書 §7.1 に完全準拠。Advisory Lock + 動的バックオフ（100ms〜5s）。

```csharp
public class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    TimeProvider timeProvider,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private const string AdvisoryLockQuery =
        "SELECT pg_try_advisory_lock(hashtext('outbox_publisher'))";
    private const string AdvisoryUnlockQuery =
        "SELECT pg_advisory_unlock(hashtext('outbox_publisher'))";

    private static readonly TimeSpan MinPollingInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MaxPollingInterval = TimeSpan.FromSeconds(5);
    private TimeSpan _currentInterval = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SalesDbContext>();

            var lockAcquired = await context.Database
                .ExecuteSqlRawAsync(AdvisoryLockQuery, stoppingToken) > 0;
            if (!lockAcquired)
            {
                await Task.Delay(MaxPollingInterval, stoppingToken);
                continue;
            }

            try
            {
                var pendingEvents = await context.OutboxEvents
                    .Where(e => e.Status == "PENDING")
                    .OrderBy(e => e.CreatedAt)
                    .Take(100)
                    .ToListAsync(stoppingToken);

                foreach (var evt in pendingEvents)
                {
                    try
                    {
                        await producer.ProduceAsync(evt.EventType, new Message<string, string>
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
                        if (evt.RetryCount >= evt.MaxRetries)
                            evt.Status = "FAILED";
                        logger.LogError(ex, "Outbox publish failed: {EventId}, retry: {RetryCount}",
                            evt.Id, evt.RetryCount);
                    }
                }
                await context.SaveChangesAsync(stoppingToken);

                _currentInterval = pendingEvents.Count > 0
                    ? MinPollingInterval
                    : TimeSpan.FromTicks(Math.Min(
                        _currentInterval.Ticks * 2,
                        MaxPollingInterval.Ticks));
            }
            finally
            {
                await context.Database.ExecuteSqlRawAsync(AdvisoryUnlockQuery, stoppingToken);
            }

            await Task.Delay(_currentInterval, stoppingToken);
        }
    }
}
```

### 6.3 Kafka Consumer 実装

設計書 §6 購読イベントに完全準拠。全 Consumer は `BackgroundService` で実装し、`IServiceScopeFactory` で Scoped サービスを取得する。

#### UserDeletedConsumer（PII 匿名化）

設計書 §6 の `user.deleted` 購読時アクションに準拠。GDPR 第 17 条対応の匿名化処理。

```csharp
// Infrastructure/Kafka/UserDeletedConsumer.cs
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
                var @event = JsonSerializer.Deserialize<UserDeletedEvent>(result.Message.Value);
                if (@event is not null)
                {
                    using var scope = scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
                    await AnonymizeUserDataAsync(context, @event.UserId, stoppingToken);
                }
                consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume エラー: {Topic}", ex.ConsumerRecord?.Topic);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "user.deleted イベント処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private static async Task AnonymizeUserDataAsync(
        SalesDbContext context, string userId, CancellationToken ct)
    {
        var hashedCustomerId = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(userId))).ToLowerInvariant();

        // 注文データの匿名化（注文自体は税法 7 年保持義務のため削除しない）
        var orders = await context.Orders
            .Include(o => o.Shipments)
            .Where(o => o.CustomerId == userId)
            .ToListAsync(ct);

        foreach (var order in orders)
        {
            order.CustomerId = hashedCustomerId;
            order.ShippingRecipientName = "[DELETED]";
            order.ShippingPostalCode = "[DELETED]";
            order.ShippingPrefecture = "[DELETED]";
            order.ShippingCity = "[DELETED]";
            order.ShippingAddressLine1 = "[DELETED]";
            order.ShippingAddressLine2 = "[DELETED]";
            order.ShippingPhoneNumber = "[DELETED]";
            order.GuestEmail = null;
            order.Notes = "[DELETED]";

            foreach (var shipment in order.Shipments)
            {
                shipment.ShippingRecipientName = "[DELETED]";
                shipment.ShippingPostalCode = "[DELETED]";
                shipment.ShippingPrefecture = "[DELETED]";
                shipment.ShippingCity = "[DELETED]";
                shipment.ShippingAddressLine1 = "[DELETED]";
                shipment.ShippingAddressLine2 = "[DELETED]";
                shipment.ShippingPhoneNumber = "[DELETED]";
            }
        }

        await context.SaveChangesAsync(ct);
    }
}

public record UserDeletedEvent(string UserId, DateTimeOffset DeletedAt);
```

#### PaymentCompletedConsumer / PaymentFailedConsumer

```csharp
// Infrastructure/Kafka/PaymentCompletedConsumer.cs
public class PaymentCompletedConsumer(
    IConsumer<string, string> consumer,
    IServiceScopeFactory scopeFactory,
    ILogger<PaymentCompletedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        consumer.Subscribe("payment.completed");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var @event = JsonSerializer.Deserialize<PaymentCompletedEvent>(result.Message.Value);
                if (@event is not null)
                {
                    using var scope = scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
                    var order = await context.Orders
                        .FirstOrDefaultAsync(o => o.Id == @event.OrderId, stoppingToken);
                    if (order is not null)
                    {
                        order.PaymentStatus = "CAPTURED";
                        OrderStateMachine.TransitionTo(order, OrderStatus.CONFIRMED);
                        await context.SaveChangesAsync(stoppingToken);
                    }
                }
                consumer.Commit(result);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "payment.completed 処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
```

#### メンテナンス BackgroundService

設計書 §14 定期メンテナンスに準拠。

```csharp
// Infrastructure/Maintenance/IdempotencyKeyCleanupService.cs
// 期限切れ IdempotencyKey を日次クリーンアップ（expires_at を過ぎたレコードを削除）
public class IdempotencyKeyCleanupService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<IdempotencyKeyCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IIdempotencyKeyRepository>();
            await repo.CleanupExpiredAsync(stoppingToken);
            logger.LogInformation("IdempotencyKey クリーンアップ完了");
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}

// Infrastructure/Maintenance/SagaLogArchivalService.cs
// COMPLETED/COMPENSATED の SagaLog を 30 日後にアーカイブ
public class SagaLogArchivalService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<SagaLogArchivalService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
            var threshold = timeProvider.GetUtcNow().AddDays(-30);
            var archived = await context.SagaLogs
                .Where(s => (s.Status == "COMPLETED" || s.Status == "COMPENSATED")
                    && s.UpdatedAt < threshold)
                .ExecuteDeleteAsync(stoppingToken);
            logger.LogInformation("SagaLog アーカイブ完了: {Count} 件", archived);
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
```

### 6.4 Program.cs への追加（Kafka + Outbox 登録）

```csharp
// Phase 6 で追加

// ── Kafka Producer ──
builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var config = new ProducerConfig
    {
        BootstrapServers = builder.Configuration["Kafka:BootstrapServers"],
        Acks = Acks.All,
        EnableIdempotence = true
    };
    return new ProducerBuilder<string, string>(config).Build();
});

// ── Outbox ──
builder.Services.AddScoped<IOutboxWriter, OutboxWriter>();
builder.Services.AddHostedService<OutboxPublisher>();

// ── Kafka Consumers ──
builder.Services.AddHostedService<PaymentCompletedConsumer>();
builder.Services.AddHostedService<PaymentFailedConsumer>();
builder.Services.AddHostedService<InventoryReservedConsumer>();
builder.Services.AddHostedService<InventoryReleasedConsumer>();
builder.Services.AddHostedService<UserDeletedConsumer>();

// ── メンテナンス BackgroundService ──
builder.Services.AddHostedService<IdempotencyKeyCleanupService>();
builder.Services.AddHostedService<SagaLogArchivalService>();
```

### Phase 6 完了チェックリスト

- [ ] `dotnet build` が成功すること
- [ ] OutboxPublisher が `IServiceScopeFactory` で Scoped サービスを取得していること
- [ ] Advisory Lock（`pg_try_advisory_lock(hashtext('outbox_publisher'))`）が実装されていること
- [ ] 動的バックオフ: イベントあり→100ms、なし→指数増加（上限 5s）が実装されていること
- [ ] バッチサイズが 100 であること
- [ ] 最大リトライ 5 回で `FAILED` ステータスに遷移すること
- [ ] `stoppingToken` が全下位呼び出しに伝搬されていること
- [ ] `finally` ブロックで Advisory Lock が解放されていること
- [ ] Kafka Producer に `Acks = Acks.All` + `EnableIdempotence = true` が設定されていること
- [ ] 固定間隔 1 秒のポーリングが使用されていないこと（動的バックオフ必須）
- [ ] Kafka Consumer が 5 つ実装されていること（payment.completed, payment.failed, inventory.reserved, inventory.released, user.deleted）
- [ ] UserDeletedConsumer が PII 匿名化を実装していること（customer_id → SHA-256 ハッシュ、配送先住所 → `[DELETED]`、guest_email → NULL）
- [ ] UserDeletedConsumer が注文データを削除せずに匿名化のみ行うこと（税法 7 年保持義務）
- [ ] PaymentCompletedConsumer が OrderStateMachine で CONFIRMED に遷移すること
- [ ] IdempotencyKeyCleanupService が `expires_at` を過ぎたレコードを日次クリーンアップすること
- [ ] SagaLogArchivalService が COMPLETED/COMPENSATED の SagaLog を 30 日後にアーカイブすること
- [ ] OutboxCleanupService が PUBLISHED イベントを 7 日後にクリーンアップすること
- [ ] **TODO/FIXME/HACK コメント残存チェック**: ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" SalesManagementService/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック**: テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック**: 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 7: Saga オーケストレーション

### 目的

注文確定フローの Saga オーケストレーション（9 ステップ）、キャンセルフロー（ORDER_CANCEL Saga: 6 ステップ）、返品フロー（ORDER_RETURN Saga: 5 ステップ）を実装する。SagaCoordinator（実行・補償）と SagaRecoveryService（滞留検出・PENDING_PAYMENT 処理）を実装する。

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `SalesManagementService/Infrastructure/Saga/SagaContext.cs` | Saga 実行コンテキスト（中間結果保持） |
| 2 | `SalesManagementService/Infrastructure/Saga/SagaCoordinator.cs` | ORDER_CHECKOUT Saga 9 ステップ実行 + 補償 |
| 3 | `SalesManagementService/Infrastructure/Saga/CancelSagaCoordinator.cs` | ORDER_CANCEL Saga 6 ステップ実行（キャンセルフロー） |
| 4 | `SalesManagementService/Infrastructure/Saga/ReturnSagaCoordinator.cs` | ORDER_RETURN Saga 5 ステップ実行（返品フロー） |
| 5 | `SalesManagementService/Infrastructure/Saga/SagaRecoveryService.cs` | 滞留 Saga 検出 + PENDING_PAYMENT 処理 |
| 6 | `SalesManagementService/Services/Interfaces/ISagaCoordinator.cs` | Saga Coordinator インターフェース |

### 7.1 SagaContext

```csharp
// Infrastructure/Saga/SagaContext.cs
public class SagaContext
{
    public IReadOnlyList<CartItemDto>? CartItems { get; set; }
    public string? ReservationId { get; set; }
    public decimal CouponDiscount { get; set; }
    public string? PointReservationId { get; set; }
    public string? OrderId { get; set; }
    public string? PaymentId { get; set; }
}
```

### 7.2 SagaCoordinator

設計書 §7 `SagaCoordinator 実装例` に完全準拠。9 ステップ + 補償 + PENDING_PAYMENT 処理。

```csharp
public class SagaCoordinator(
    ISagaLogRepository sagaLogRepository,
    InventoryService.InventoryServiceClient inventoryClient,
    CouponService.CouponServiceClient couponClient,
    PointService.PointServiceClient pointClient,
    CartService.CartServiceClient cartClient,
    IOrderService orderService,
    IPaymentClient paymentClient,
    IOutboxWriter outboxWriter,
    TimeProvider timeProvider,
    ILogger<SagaCoordinator> logger) : ISagaCoordinator
{
    private static readonly TimeSpan SloDeadline = TimeSpan.FromMilliseconds(1000);

    public async Task<OrderDetailDto> ExecuteCheckoutSagaAsync(
        OrderCreateRequest request, string userId,
        string idempotencyKey, CancellationToken ct = default)
    {
        using var sloCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        sloCts.CancelAfter(SloDeadline);

        var sagaLog = await sagaLogRepository.CreateAsync(new SagaLog
        {
            SagaType = "ORDER_CHECKOUT",
            UserId = userId,
            Status = "PROCESSING",
            CurrentStep = 1,
            StartedAt = timeProvider.GetUtcNow(),
            TimeoutAt = timeProvider.GetUtcNow().AddMinutes(5)
        }, ct);

        var context = new SagaContext();

        try
        {
            // ステップ 1: カート取得（gRPC, 200ms Deadline）
            sagaLog.CurrentStep = 1;
            var cartResponse = await cartClient.GetCartAsync(
                new GetCartRequest { UserId = userId },
                CreateCallOptions(200, sloCts.Token));
            context.CartItems = cartResponse.Items;

            // ステップ 2: 在庫確認・引当（gRPC, 500ms Deadline）
            sagaLog.CurrentStep = 2;
            var inventoryResponse = await inventoryClient.ReserveInventoryAsync(
                new ReserveInventoryRequest { Items = { context.CartItems } },
                CreateCallOptions(500, sloCts.Token));
            context.ReservationId = inventoryResponse.ReservationId;

            // ステップ 3: クーポン検証・適用（gRPC, 300ms Deadline）
            sagaLog.CurrentStep = 3;
            if (!string.IsNullOrEmpty(request.CouponCode))
            {
                var couponResponse = await couponClient.ValidateCouponAsync(
                    new ValidateCouponRequest { CouponCode = request.CouponCode },
                    CreateCallOptions(300, sloCts.Token));
                context.CouponDiscount = couponResponse.DiscountAmount;
            }

            // ステップ 4: ポイント仮消費（gRPC, 300ms Deadline）
            sagaLog.CurrentStep = 4;
            if (request.UsedPoints > 0)
            {
                var pointResponse = await pointClient.ReservePointsAsync(
                    new ReservePointsRequest { UserId = userId, Points = request.UsedPoints },
                    CreateCallOptions(300, sloCts.Token));
                context.PointReservationId = pointResponse.ReservationId;
            }

            // ステップ 5: 注文作成（ローカル TX）
            sagaLog.CurrentStep = 5;
            var order = await orderService.CreateOrderAsync(request, context, ct);
            context.OrderId = order.Id;
            sagaLog.OrderId = order.Id;

            // ステップ 6: 決済認証（HTTPS）
            sagaLog.CurrentStep = 6;
            var paymentResult = await paymentClient.ProcessPaymentAsync(
                order.TotalAmount, request.PaymentMethod, ct);
            context.PaymentId = paymentResult.PaymentId;

            // ステップ 7: ポイント確定付与（gRPC, 300ms）— 後処理
            sagaLog.CurrentStep = 7;
            await pointClient.AwardPointsAsync(
                new AwardPointsRequest { UserId = userId, Amount = order.TotalAmount },
                CreateCallOptions(300, ct));

            // ステップ 8: カートクリア（gRPC, 200ms）— 後処理
            sagaLog.CurrentStep = 8;
            await cartClient.ClearCartAsync(
                new ClearCartRequest { UserId = userId },
                CreateCallOptions(200, ct));

            // ステップ 9: Outbox 書込み（ローカル TX）— 後処理
            sagaLog.CurrentStep = 9;
            await outboxWriter.WriteAsync("order.created", order.Id,
                new OrderCreatedEvent(order.Id, userId, order.TotalAmount), ct);

            sagaLog.Status = "COMPLETED";
            sagaLog.CompletedAt = timeProvider.GetUtcNow();
            await sagaLogRepository.UpdateAsync(sagaLog, ct);

            return order;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded
            && sagaLog.CurrentStep == 6)
        {
            sagaLog.Status = "PENDING_PAYMENT";
            sagaLog.LastError = "決済タイムアウト: SagaRecoveryService で結果確認予定";
            await sagaLogRepository.UpdateAsync(sagaLog, ct);
            logger.LogWarning("決済タイムアウト: SagaId={SagaId}, OrderId={OrderId}",
                sagaLog.Id, sagaLog.OrderId);
            throw new PaymentPendingException(
                "お支払い処理中です。確定次第メールでお知らせします。");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Saga 失敗: SagaId={SagaId}, Step={Step}",
                sagaLog.Id, sagaLog.CurrentStep);
            await CompensateAsync(sagaLog, context, ct);
            throw;
        }
    }

    public async Task CompensateAsync(SagaLog sagaLog, CancellationToken ct = default)
        => await CompensateAsync(sagaLog, new SagaContext(), ct);

    private async Task CompensateAsync(
        SagaLog sagaLog, SagaContext context, CancellationToken ct)
    {
        sagaLog.Status = "COMPENSATING";
        await sagaLogRepository.UpdateAsync(sagaLog, ct);

        using var compensationCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cToken = compensationCts.Token;

        for (var step = sagaLog.CurrentStep - 1; step >= 1; step--)
        {
            try
            {
                switch (step)
                {
                    case 5 when context.OrderId is not null:
                        await orderService.CancelOrderAsync(
                            context.OrderId, "Saga 補償", cToken);
                        break;
                    case 4 when context.PointReservationId is not null:
                        await pointClient.ReleasePointsAsync(
                            new ReleasePointsRequest
                                { ReservationId = context.PointReservationId },
                            CreateCallOptions(300, cToken));
                        break;
                    case 3 when context.CouponDiscount > 0:
                        await couponClient.ReleaseCouponAsync(
                            new ReleaseCouponRequest { OrderId = context.OrderId },
                            CreateCallOptions(300, cToken));
                        break;
                    case 2 when context.ReservationId is not null:
                        await inventoryClient.ReleaseReservationAsync(
                            new ReleaseReservationRequest
                                { ReservationId = context.ReservationId },
                            CreateCallOptions(500, cToken));
                        break;
                    case 1: // 読取り専用 → No-op
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "補償ステップ {Step} 失敗: SagaId={SagaId}",
                    step, sagaLog.Id);
            }
        }

        sagaLog.Status = "COMPENSATED";
        sagaLog.CompletedAt = timeProvider.GetUtcNow();
        await sagaLogRepository.UpdateAsync(sagaLog, ct);
    }

    private CallOptions CreateCallOptions(int deadlineMs, CancellationToken ct)
    {
        var deadline = timeProvider.GetUtcNow().AddMilliseconds(deadlineMs).UtcDateTime;
        return new CallOptions(deadline: deadline, cancellationToken: ct);
    }
}
```

### 7.3 ORDER_CANCEL Saga（キャンセルフロー）

設計書 §7 ORDER_CANCEL Saga に完全準拠。6 ステップ + リトライ（最大 3 回、指数バックオフ）。

```csharp
// Infrastructure/Saga/CancelSagaCoordinator.cs
// ステップ構成:
// 1. 注文状態検証（ローカル）— PENDING/CONFIRMED/PROCESSING のみキャンセル可
// 2. 在庫予約解放（gRPC, 500ms）— 在庫引当済みの場合のみ
// 3. クーポン使用取消（gRPC, 300ms）— クーポン適用済みの場合のみ
// 4. ポイント仮消費取消（gRPC, 300ms）— ポイント使用済みの場合のみ
// 5. 決済返金（gRPC/HTTPS）— CAPTURED の場合は返金、AUTHORIZED の場合はキャンセルのみ
// 6. 注文ステータス更新 + Outbox（ローカル TX）— CANCELLED or REFUNDED + order.cancelled イベント
//
// 補償設計:
// - ステップ 2〜4（リソース解放）失敗: リトライ（最大 3 回、指数バックオフ）。全失敗時は FAILED + 管理者通知
// - ステップ 5（返金）失敗: CANCELLED に更新するが payment_status は REFUND_PENDING。手動返金を管理者に委譲
// - 全体タイムアウト: 30 秒（CancellationTokenSource(TimeSpan.FromSeconds(30))）
```

### 7.4 ORDER_RETURN Saga（返品フロー）

設計書 §7 ORDER_RETURN Saga に完全準拠。5 ステップ。返品承認後の返金・在庫戻し処理を管理。

```csharp
// Infrastructure/Saga/ReturnSagaCoordinator.cs
// ステップ構成:
// 1. 返品状態検証（ローカル）— Return.Status が RECEIVED（商品受領済み）であることを検証
// 2. 返金処理（gRPC/HTTPS）— PartialRefund(paymentId, refundAmount) — 部分返金
// 3. 在庫戻し（gRPC, 500ms）— RestoreInventory(productId, quantity) — 検品合格品のみ
// 4. ポイント調整（gRPC, 300ms）— 購入ポイント付与取消 + 使用ポイント返還
// 5. 返品・注文ステータス更新 + Outbox（ローカル TX）— Return.Status → REFUNDED
//    Order.Status → REFUNDED（全明細返品時）or 据え置き（部分返品時）+ order.status-changed イベント
//
// 補償設計:
// - ステップ 2（返金）失敗: ORDER_CANCEL と同様、手動返金処理に委譲
// - ステップ 3〜4（在庫戻し・ポイント調整）失敗: リトライ（最大 3 回）。失敗時は FAILED + 管理者通知
// - 全体タイムアウト: 60 秒（外部 API 返金処理の余裕を確保）
```

### 7.5 SagaRecoveryService

設計書 §7.2 に完全準拠。`SELECT FOR UPDATE SKIP LOCKED` + 30 秒ポーリング。

```csharp
public class SagaRecoveryService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<SagaRecoveryService> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StallThreshold = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PaymentPollingTimeout = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SalesDbContext>();

            var stalledSagas = await context.SagaLogs
                .FromSqlInterpolated($"""
                    SELECT * FROM saga_logs
                    WHERE status = 'PROCESSING'
                      AND updated_at < {timeProvider.GetUtcNow().AddMinutes(-5)}
                    FOR UPDATE SKIP LOCKED
                    LIMIT 10
                """)
                .ToListAsync(stoppingToken);

            foreach (var saga in stalledSagas)
            {
                logger.LogWarning(
                    "Stalled saga detected: SagaId={SagaId}, OrderId={OrderId}, Step={Step}",
                    saga.Id, saga.OrderId, saga.CurrentStep);
                saga.Status = "COMPENSATING";
                saga.LastError = "SagaRecoveryService: 5 分以上 PROCESSING のためタイムアウト";
            }

            var pendingPaymentSagas = await context.SagaLogs
                .Where(s => s.Status == "PENDING_PAYMENT")
                .ToListAsync(stoppingToken);

            foreach (var saga in pendingPaymentSagas)
            {
                var elapsed = timeProvider.GetUtcNow() - saga.UpdatedAt;
                if (elapsed > PaymentPollingTimeout)
                {
                    saga.Status = "COMPENSATING";
                    saga.LastError = "決済結果確認タイムアウト（30 分）";
                    logger.LogWarning(
                        "Payment polling timeout: SagaId={SagaId}, OrderId={OrderId}",
                        saga.Id, saga.OrderId);
                }
            }

            await context.SaveChangesAsync(stoppingToken);
            await Task.Delay(PollingInterval, stoppingToken);
        }
    }
}
```

### 7.6 Program.cs への追加（Saga + gRPC 登録）

```csharp
// Phase 7 で追加

// ── Saga ──
builder.Services.AddScoped<ISagaCoordinator, SagaCoordinator>();
builder.Services.AddScoped<CancelSagaCoordinator>();
builder.Services.AddScoped<ReturnSagaCoordinator>();
builder.Services.AddHostedService<SagaRecoveryService>();

// ── gRPC クライアント（Polly リトライ付き） ──
builder.Services.AddGrpcClient<InventoryService.InventoryServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["GrpcClients:InventoryService"]!))
    .AddStandardResilienceHandler();

builder.Services.AddGrpcClient<CouponService.CouponServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["GrpcClients:CouponService"]!))
    .AddStandardResilienceHandler();

builder.Services.AddGrpcClient<PointService.PointServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["GrpcClients:PointService"]!))
    .AddStandardResilienceHandler();

builder.Services.AddGrpcClient<CartService.CartServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["GrpcClients:CartService"]!))
    .AddStandardResilienceHandler();

builder.Services.AddGrpcClient<PaymentService.PaymentServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["GrpcClients:PaymentService"]!))
    .AddStandardResilienceHandler();
```

### Phase 7 完了チェックリスト

- [ ] `dotnet build` が成功すること
- [ ] SagaCoordinator が 9 ステップを順次実行すること
- [ ] 各 gRPC 呼び出しに設計書の Deadline が設定されていること（ステップ 1:200ms, 2:500ms, 3:300ms, 4:300ms, 7:300ms, 8:200ms）
- [ ] 補償が逆順（失敗ステップ-1 → 1）で実行されること
- [ ] 各補償ステップがべき等であること（`saga_step_id` による重複検知）
- [ ] 補償に専用 `CancellationTokenSource(30 秒)` が使用されていること
- [ ] ステップ 7〜9（後処理）が補償対象外であること
- [ ] 決済タイムアウト時に Saga が `PENDING_PAYMENT` に遷移すること
- [ ] `PENDING_PAYMENT` 時に HTTP 202 が返されること
- [ ] SagaRecoveryService が `SELECT FOR UPDATE SKIP LOCKED` を使用していること
- [ ] SagaRecoveryService のポーリング間隔が 30 秒であること
- [ ] SagaRecoveryService の滞留閾値が 5 分であること
- [ ] PENDING_PAYMENT の 30 分タイムアウトで補償が開始されること
- [ ] SLO 1,000ms の Deadline が `CancellationTokenSource.CreateLinkedTokenSource` で設定されていること
- [ ] ORDER_CANCEL Saga が 6 ステップで実装されていること（状態検証→在庫解放→クーポン取消→ポイント取消→返金→ステータス更新+Outbox）
- [ ] ORDER_CANCEL Saga がキャンセル可能な状態（PENDING/CONFIRMED/PROCESSING）のみ受け付けること
- [ ] ORDER_CANCEL Saga のステップ 2〜4 が最大 3 回リトライ（指数バックオフ）を行うこと
- [ ] ORDER_CANCEL Saga のステップ 5（返金）失敗時に CANCELLED + payment_status=REFUND_PENDING で処理し、手動返金を管理者に委譲すること
- [ ] ORDER_CANCEL Saga の全体タイムアウトが 30 秒であること
- [ ] ORDER_RETURN Saga が 5 ステップで実装されていること（返品状態検証→返金→在庫戻し→ポイント調整→ステータス更新+Outbox）
- [ ] ORDER_RETURN Saga のステップ 1 が Return.Status=RECEIVED を検証すること
- [ ] ORDER_RETURN Saga のステップ 2 が部分返金（返品対象商品のみの金額）を実行すること
- [ ] ORDER_RETURN Saga のステップ 3 が検品合格品のみの在庫戻しを実行すること
- [ ] ORDER_RETURN Saga の全体タイムアウトが 60 秒であること
- [ ] CancelSagaCoordinator と ReturnSagaCoordinator が DI 登録（Scoped）されていること
- [ ] **TODO/FIXME/HACK コメント残存チェック**: ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" SalesManagementService/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック**: テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック**: 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 8: 可観測性 & 耐障害性

### 目的

OpenTelemetry（トレーシング・メトリクス）、Serilog（構造化ログ）、ヘルスチェック、Correlation ID、Polly レジリエンス、カスタム監視メトリクス（10 項目）、Redis キャッシュ戦略を統合する。

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `SalesManagementService/Infrastructure/Middleware/CorrelationIdMiddleware.cs` | Correlation ID 付与・伝搬 |
| 2 | `SalesManagementService/Infrastructure/Middleware/SecurityHeadersMiddleware.cs` | セキュリティヘッダー |
| 3 | `SalesManagementService/Infrastructure/Caching/OrderCacheService.cs` | Redis キャッシュ（注文・レポート） |

### 8.1 カスタム監視メトリクス

設計書 §11 に完全準拠。以下 10 メトリクスの計装と閾値ベースアラートを実装する。

| # | メトリクス名 | 種別 | 警告閾値 | アラート閾値 |
|---|------------|------|---------|------------|
| 1 | `order_creation_rate` | Counter（/分） | > 100/min | > 200/min |
| 2 | `order_processing_time_ms` | Histogram | > 1,000ms（95th） | > 3,000ms（95th） |
| 3 | `payment_success_rate` | Gauge（%） | < 95% | < 90% |
| 4 | `saga_completion_rate` | Gauge（%） | < 98% | < 95% |
| 5 | `saga_pending_payment_count` | Gauge | > 5 | > 10 |
| 6 | `outbox_pending_count` | Gauge | > 50 | > 100 |
| 7 | `outbox_publish_latency_ms` | Histogram | > 5,000ms | > 10,000ms |
| 8 | `grpc_deadline_exceeded_rate` | Gauge（%） | > 1% | > 5% |
| 9 | `compensation_execution_rate` | Gauge（%） | > 5% | > 10% |
| 10 | `api_error_rate` | Gauge（%） | > 1% | > 5% |

```csharp
// Program.cs に追加 — OpenTelemetry カスタムメトリクス
using System.Diagnostics.Metrics;

var meter = new Meter("SkiShop.SalesManagement", "1.0.0");
var orderCreationCounter = meter.CreateCounter<long>("order_creation_rate", "orders", "注文作成レート");
var orderProcessingHistogram = meter.CreateHistogram<double>("order_processing_time_ms", "ms", "注文処理時間");
var sagaPendingPaymentGauge = meter.CreateObservableGauge("saga_pending_payment_count", () =>
{
    // SagaLog から PENDING_PAYMENT の件数を取得
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
    return context.SagaLogs.Count(s => s.Status == "PENDING_PAYMENT");
});
var outboxPendingGauge = meter.CreateObservableGauge("outbox_pending_count", () =>
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
    return context.OutboxEvents.Count(e => e.Status == "PENDING");
});
```

### 8.2 Redis キャッシュ戦略

設計書 §9 に完全準拠。キーパターンと TTL を定義。

| キーパターン | TTL | 用途 | 無効化タイミング |
|-------------|-----|------|----------------|
| `order:{orderId}` | 1 時間 | 注文詳細キャッシュ | 注文ステータス変更時 |
| `orders:user:{userId}` | 1 時間 | ユーザー注文一覧キャッシュ | 新規注文作成・ステータス変更時 |
| `report:sales:daily:{date}` | 1 日 | 日次売上レポートキャッシュ | バッチ更新時 |
| `shipping:tracking:{shipmentId}` | 30 分 | 配送追跡情報キャッシュ | 配送ステータス変更時 |

```csharp
// Infrastructure/Caching/OrderCacheService.cs
public class OrderCacheService(
    IConnectionMultiplexer redis,
    ILogger<OrderCacheService> logger)
{
    private static readonly TimeSpan OrderCacheTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan ReportCacheTtl = TimeSpan.FromDays(1);
    private static readonly TimeSpan TrackingCacheTtl = TimeSpan.FromMinutes(30);

    private IDatabase Db => redis.GetDatabase();

    public async Task<OrderDetailDto?> GetOrderAsync(string orderId, CancellationToken ct = default)
    {
        var cached = await Db.StringGetAsync($"order:{orderId}");
        if (cached.IsNullOrEmpty) return null;
        return JsonSerializer.Deserialize<OrderDetailDto>(cached!);
    }

    public async Task SetOrderAsync(string orderId, OrderDetailDto order, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(order);
        await Db.StringSetAsync($"order:{orderId}", json, OrderCacheTtl);
    }

    public async Task InvalidateOrderAsync(string orderId, string userId, CancellationToken ct = default)
    {
        await Db.KeyDeleteAsync($"order:{orderId}");
        await Db.KeyDeleteAsync($"orders:user:{userId}");
        logger.LogDebug("キャッシュ無効化: order:{OrderId}, orders:user:{UserId}", orderId, userId);
    }
}
```

### 8.3 OpenTelemetry 設定

```csharp
// Program.cs に追加
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddGrpcClientInstrumentation()
        .AddSource("SkiShop.SalesManagement.*"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation());
```

### 8.4 ヘルスチェック

設計書 §11 に完全準拠。PostgreSQL + Redis + Kafka。

```csharp
// Program.cs に追加
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("salesdb")!,
        name: "salesdb-postgresql",
        tags: ["ready"])
    .AddRedis(
        builder.Configuration.GetConnectionString("redis")!,
        name: "redis",
        tags: ["ready"])
    .AddKafka(
        new ProducerConfig
        {
            BootstrapServers = builder.Configuration["Kafka:BootstrapServers"]
        },
        name: "kafka",
        tags: ["ready"]);

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();
```

### 8.5 Correlation ID ミドルウェア

```csharp
public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                ?? Guid.NewGuid().ToString();
            context.Response.Headers.Append("X-Correlation-Id", correlationId);
            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await next();
            }
        });
    }
}
```

### Phase 8 完了チェックリスト

- [ ] `/health` が常に 200 を返すこと（Liveness）
- [ ] `/health/ready` が PostgreSQL + Redis + Kafka の疎通を確認すること（Readiness）
- [ ] ヘルスチェックエンドポイントに `.AllowAnonymous()` があること
- [ ] OpenTelemetry に ASP.NET Core, HTTP Client, EF Core, gRPC のインストルメンテーションがあること
- [ ] Correlation ID がリクエスト/レスポンスヘッダーに付与されていること
- [ ] Serilog のログに `CorrelationId` プロパティが含まれていること
- [ ] gRPC クライアントに `AddStandardResilienceHandler()` が適用されていること
- [ ] カスタム監視メトリクス（10 項目）が OpenTelemetry Meter で計装されていること
- [ ] `order_creation_rate`, `order_processing_time_ms` が Saga 実行内で記録されていること
- [ ] `saga_pending_payment_count`, `outbox_pending_count` が ObservableGauge で定期取得されていること
- [ ] Redis キャッシュが `order:{orderId}`（TTL 1h）、`orders:user:{userId}`（TTL 1h）で実装されていること
- [ ] 注文ステータス変更時にキャッシュ無効化が実行されること
- [ ] `report:sales:daily:{date}`（TTL 1 日）のキャッシュが実装されていること
- [ ] OrderCacheService が DI 登録されていること
- [ ] **TODO/FIXME/HACK コメント残存チェック**: ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" SalesManagementService/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック**: テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック**: 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 9: セキュリティ

### 目的

JWT 認証・認可、IDOR 防止、入力バリデーション、セキュリティヘッダー、CORS、レート制限、エラーコード体系（ORD-4001〜ORD-5002）を統合する。

### 9.1 エラーコード体系

設計書 §8 に完全準拠。19 エラーコードを定義し、グローバル例外ハンドラーで統一マッピングする。

| エラーコード | HTTP ステータス | 説明 |
|-------------|---------------|------|
| `ORD-4001` | 400 | 不正なリクエストパラメータ（バリデーションエラー） |
| `ORD-4002` | 400 | 不正な日付範囲指定 |
| `ORD-4003` | 400 | 不正な注文ステータス遷移 |
| `ORD-4041` | 404 | 注文が見つかりません |
| `ORD-4042` | 404 | 注文明細が見つかりません |
| `ORD-4043` | 404 | 出荷情報が見つかりません |
| `ORD-4044` | 404 | 返品情報が見つかりません |
| `ORD-4045` | 404 | 請求書が見つかりません |
| `ORD-4091` | 409 | 注文の楽観的ロック競合（同時更新） |
| `ORD-4092` | 409 | べき等キー重複（同一リクエストの再送検出） |
| `ORD-4221` | 422 | 在庫不足（inventory.reserved 失敗） |
| `ORD-4222` | 422 | 決済処理失敗 |
| `ORD-4223` | 422 | クーポン適用不可（無効・期限切れ） |
| `ORD-4224` | 422 | ポイント不足 |
| `ORD-4225` | 422 | 返品期限超過 |
| `ORD-2021` | 202 | 決済処理中（PENDING_PAYMENT — 確定次第メール通知） |
| `ORD-5001` | 500 | 内部サーバーエラー（予期しない例外） |
| `ORD-5002` | 503 | 外部サービス障害（gRPC Deadline 超過） |
| `ORD-5003` | 503 | Saga 補償処理中（一時的に利用不可） |

### 9.2 レート制限

設計書 §11 に準拠。注文作成エンドポイントにはユーザー単位のレート制限を適用する。

```csharp
// Program.cs に追加
builder.Services.AddRateLimiter(options =>
{
    // 注文作成: ユーザーあたり 1 分間に 5 回まで
    options.AddFixedWindowLimiter("order-create", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });

    // 管理 API: IP あたり 1 分間に 30 回まで
    options.AddFixedWindowLimiter("admin-api", limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

// エンドポイントへの適用
group.MapPost("/", CreateOrder)
    .RequireAuthorization()
    .RequireRateLimiting("order-create")
    .WithName("CreateOrder");
```

### 9.3 認証・認可設定

```csharp
// Program.cs に追加
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
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

### 9.4 グローバル例外ハンドラー

設計書 §8 に完全準拠。Saga 関連例外を含む。

```csharp
// Program.cs に追加
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var error = feature?.Error;

        if (error is not (NotFoundException or BusinessException or UnauthorizedException
            or ForbiddenException or ConcurrencyException or PaymentPendingException))
            logger.LogError(error, "未処理の例外: {Message}", error?.Message);
        else
            logger.LogWarning("処理済み例外: {ExceptionType} - {Message}",
                error.GetType().Name, error.Message);

        var problem = error switch
        {
            NotFoundException e          => TypedResults.Problem(e.Message, statusCode: 404),
            InsufficientStockException e => TypedResults.Problem(e.Message, statusCode: 422),
            PaymentProcessingException e => TypedResults.Problem(e.Message, statusCode: 422),
            PaymentPendingException e    => TypedResults.Problem(e.Message, statusCode: 202),
            InvalidOrderStateException e => TypedResults.Problem(e.Message, statusCode: 422),
            ConcurrencyException e       => TypedResults.Problem(e.Message, statusCode: 409),
            IdempotencyConflictException e => TypedResults.Problem(e.Message, statusCode: 409),
            UnauthorizedException        => TypedResults.Problem(statusCode: 401),
            ForbiddenException           => TypedResults.Problem(statusCode: 403),
            BusinessException e          => TypedResults.Problem(e.Message, statusCode: 422),
            ExternalServiceException e   => TypedResults.Problem(e.Message, statusCode: 503),
            _                            => TypedResults.Problem("内部エラーが発生しました", statusCode: 500)
        };
        await problem.ExecuteAsync(context);
    });
});
```

### 9.5 セキュリティヘッダー

```csharp
// Program.cs に追加
app.UseHsts();
app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    await next();
});
```

### Phase 9 完了チェックリスト

- [ ] Fallback Policy でデフォルト認証必須になっていること
- [ ] 公開エンドポイント（`/health`）に `.AllowAnonymous()` があること
- [ ] 管理者専用エンドポイントに `RequireAuthorization("AdminOnly")` があること
- [ ] グローバル例外ハンドラーがスタックトレースをクライアントに返さないこと
- [ ] `DetailedErrors: false` が appsettings.json に設定されていること
- [ ] `Kestrel.AddServerHeader: false` が appsettings.json に設定されていること
- [ ] セキュリティヘッダー（X-Content-Type-Options, X-Frame-Options, CSP）が付与されていること
- [ ] CORS に `AllowAnyOrigin()` が使用されていないこと
- [ ] エラーコード体系（ORD-4001〜ORD-5003, 19 コード）が例外クラスまたは定数として定義されていること
- [ ] グローバル例外ハンドラーが Problem Details レスポンスにエラーコードを含めていること
- [ ] 注文作成エンドポイントにレート制限（`order-create`: 5 回/分/ユーザー）が適用されていること
- [ ] 管理 API にレート制限（`admin-api`: 30 回/分/IP）が適用されていること
- [ ] PII（配送先住所、電話番号）がログに出力されていないこと
- [ ] 秘密情報が appsettings.json にハードコードされていないこと
- [ ] **TODO/FIXME/HACK コメント残存チェック**: ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" SalesManagementService/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック**: テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック**: 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 10: テスト

### 目的

単体テスト、統合テスト、Saga テスト（ORDER_CHECKOUT/ORDER_CANCEL/ORDER_RETURN）、Outbox テスト、Kafka Consumer テスト、API テスト、負荷テストを作成する。分岐カバレッジ 80% 以上を目標とする。

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `SalesManagementService.Tests/SalesManagementService.Tests.csproj` | テストプロジェクト定義 |
| 2 | `SalesManagementService.Tests/Unit/Services/OrderServiceTests.cs` | OrderService 単体テスト |
| 3 | `SalesManagementService.Tests/Unit/Services/ShipmentServiceTests.cs` | ShipmentService 単体テスト |
| 4 | `SalesManagementService.Tests/Unit/Services/ReturnServiceTests.cs` | ReturnService 単体テスト |
| 5 | `SalesManagementService.Tests/Unit/Services/OrderStateMachineTests.cs` | ステータス遷移テスト |
| 6 | `SalesManagementService.Tests/Unit/Services/TaxCalculatorTests.cs` | 消費税計算テスト |
| 7 | `SalesManagementService.Tests/Unit/Services/ShippingFeeCalculatorTests.cs` | 送料計算テスト（会員ランク・地域別） |
| 8 | `SalesManagementService.Tests/Unit/Services/OrderNumberGeneratorTests.cs` | 注文番号生成テスト |
| 9 | `SalesManagementService.Tests/Unit/Validators/OrderCreateRequestValidatorTests.cs` | バリデーターテスト |
| 10 | `SalesManagementService.Tests/Integration/Repositories/OrderRepositoryTests.cs` | DB スライステスト（Testcontainers） |
| 11 | `SalesManagementService.Tests/Integration/Saga/SagaCoordinatorTests.cs` | ORDER_CHECKOUT Saga 完走・補償テスト |
| 12 | `SalesManagementService.Tests/Integration/Saga/CancelSagaCoordinatorTests.cs` | ORDER_CANCEL Saga テスト |
| 13 | `SalesManagementService.Tests/Integration/Saga/ReturnSagaCoordinatorTests.cs` | ORDER_RETURN Saga テスト |
| 14 | `SalesManagementService.Tests/Integration/Outbox/OutboxPublisherTests.cs` | Outbox 発行テスト |
| 15 | `SalesManagementService.Tests/Integration/Kafka/UserDeletedConsumerTests.cs` | UserDeletedConsumer PII 匿名化テスト |
| 16 | `SalesManagementService.Tests/Integration/Kafka/PaymentCompletedConsumerTests.cs` | PaymentCompletedConsumer テスト |
| 17 | `SalesManagementService.Tests/Integration/Endpoints/OrderEndpointsTests.cs` | API 統合テスト |
| 18 | `SalesManagementService.Tests/Integration/Endpoints/SecurityTests.cs` | 認証・認可テスト |
| 19 | `SalesManagementService.Tests/Load/SagaSloLoadTest.cs` | Saga SLO 負荷テスト（NBomber） |

### 10.1 テストプロジェクト定義

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
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\SalesManagementService\SalesManagementService.csproj" />
  </ItemGroup>
</Project>
```

### 10.2 単体テスト例（OrderServiceTests）

```csharp
[Trait("Category", "Unit")]
public class OrderServiceTests
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<OrderService> _logger;
    private readonly OrderService _orderService;

    public OrderServiceTests()
    {
        _orderRepository = Substitute.For<IOrderRepository>();
        _logger = Substitute.For<ILogger<OrderService>>();
        _orderService = new OrderService(_orderRepository, _logger);
    }

    [Fact]
    public async Task Should_ReturnOrderDetail_When_ValidIdAndUserProvided()
    {
        // Arrange
        var order = new Order
        {
            Id = "order-1",
            CustomerId = "user-1",
            OrderNumber = "ORD-001",
            Status = "CONFIRMED"
        };
        _orderRepository.FindByIdWithDetailsAsync("order-1", default)
            .Returns(order);

        // Act
        var result = await _orderService.GetByIdAndUserIdAsync("order-1", "user-1");

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("order-1");
        result.OrderNumber.ShouldBe("ORD-001");
    }

    [Fact]
    public async Task Should_ReturnNull_When_OrderBelongsToOtherUser()
    {
        // Arrange
        var order = new Order
        {
            Id = "order-1",
            CustomerId = "user-2",
            OrderNumber = "ORD-001"
        };
        _orderRepository.FindByIdWithDetailsAsync("order-1", default)
            .Returns(order);

        // Act
        var result = await _orderService.GetByIdAndUserIdAsync("order-1", "user-1");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Should_ThrowNotFoundException_When_OrderDoesNotExist()
    {
        // Arrange
        _orderRepository.FindByIdWithDetailsAsync("nonexistent", default)
            .Returns((Order?)null);

        // Act & Assert
        var act = async () => await _orderService.GetByIdAsync("nonexistent");
        var ex = await Should.ThrowAsync<NotFoundException>(act);
        ex.Message.ShouldContain("nonexistent");
    }

    [Fact]
    public async Task Should_ThrowInvalidOrderStateException_When_InvalidTransition()
    {
        // Arrange
        var order = new Order
        {
            Id = "order-1",
            Status = "DELIVERED"
        };
        _orderRepository.FindByIdAsync("order-1", default).Returns(order);

        // Act & Assert
        var act = async () =>
            await _orderService.UpdateStatusAsync("order-1", "PENDING");
        await Should.ThrowAsync<InvalidOrderStateException>(act);
    }
}
```

### 10.3 OrderStateMachine テスト

```csharp
[Trait("Category", "Unit")]
public class OrderStateMachineTests
{
    [Theory]
    [InlineData("PENDING", "CONFIRMED", true)]
    [InlineData("PENDING", "CANCELLED", true)]
    [InlineData("PENDING", "PENDING_PAYMENT", true)]
    [InlineData("PENDING", "INVENTORY_SHORTAGE", true)]
    [InlineData("PENDING", "PAYMENT_FAILED", true)]
    [InlineData("CONFIRMED", "PROCESSING", true)]
    [InlineData("PROCESSING", "SHIPPED", true)]
    [InlineData("SHIPPED", "DELIVERED", true)]
    [InlineData("DELIVERED", "RETURNED", true)]
    [InlineData("DELIVERED", "PENDING", false)]
    [InlineData("CANCELLED", "CONFIRMED", false)]
    [InlineData("REFUNDED", "PENDING", false)]
    public void Should_ValidateTransition_Correctly(
        string current, string next, bool expected)
    {
        // Act
        var result = OrderStateMachine.CanTransition(current, next);

        // Assert
        result.ShouldBe(expected);
    }
}
```

### 10.4 Saga テスト（統合テスト）

```csharp
[Trait("Category", "Integration")]
public class SagaCoordinatorTests
{
    [Fact]
    public async Task Should_CompleteAllSteps_When_AllServicesSucceed()
    {
        // Arrange: 全 gRPC クライアントをモック化し成功レスポンスを返す
        // Act: ExecuteCheckoutSagaAsync を実行
        // Assert:
        //   - SagaLog.Status == "COMPLETED"
        //   - SagaLog.CurrentStep == 9
        //   - OutboxEvent が書き込まれていること
        //   - 注文が作成されていること
    }

    [Fact]
    public async Task Should_CompensateInReverseOrder_When_PaymentFails()
    {
        // Arrange: ステップ 6（決済）で例外を発生
        // Act: ExecuteCheckoutSagaAsync を実行
        // Assert:
        //   - 補償がステップ 5→4→3→2→1 の逆順で実行されること
        //   - SagaLog.Status == "COMPENSATED"
        //   - InventoryService.ReleaseReservation が呼ばれること
        //   - CouponService.ReleaseCoupon が呼ばれること
        //   - PointService.ReleasePoints が呼ばれること
    }

    [Fact]
    public async Task Should_SetPendingPayment_When_PaymentTimesOut()
    {
        // Arrange: ステップ 6 で RpcException(DeadlineExceeded) を発生
        // Act: ExecuteCheckoutSagaAsync を実行
        // Assert:
        //   - SagaLog.Status == "PENDING_PAYMENT"
        //   - PaymentPendingException がスローされること
        //   - 補償が実行されないこと
    }

    [Fact]
    public async Task Should_CompensateOnlyCompletedSteps_When_Step3Fails()
    {
        // Arrange: ステップ 3（クーポン検証）で例外を発生
        // Act: ExecuteCheckoutSagaAsync を実行
        // Assert:
        //   - 補償がステップ 2→1 の逆順で実行されること
        //   - InventoryService.ReleaseReservation が呼ばれること
        //   - PointService.ReleasePoints が呼ばれないこと（ステップ 4 未到達）
    }

    [Fact]
    public async Task Should_NotCompensatePostProcessingSteps_When_Step7Fails()
    {
        // Arrange: ステップ 7（ポイント確定）で例外を発生
        // Act & Assert:
        //   - ステップ 7 はポスト処理のため、補償対象外
        //   - SagaLog.Status は "COMPLETED"（ステップ 6 まで成功済み）
    }
}
```

### 10.5 Outbox テスト

```csharp
[Trait("Category", "Integration")]
public class OutboxPublisherTests
{
    [Fact]
    public async Task Should_PublishPendingEvents_When_AdvisoryLockAcquired()
    {
        // Arrange: PENDING イベントを DB に挿入
        // Act: OutboxPublisher の 1 サイクルを実行
        // Assert:
        //   - イベントのステータスが PUBLISHED に更新されること
        //   - Kafka Producer に ProduceAsync が呼ばれること
        //   - PublishedAt が設定されていること
    }

    [Fact]
    public async Task Should_UseExponentialBackoff_When_NoEventsFound()
    {
        // Arrange: PENDING イベントなし
        // Act: 複数サイクルを実行
        // Assert:
        //   - ポーリング間隔が 100ms→200ms→400ms→...→5s に増加すること
    }

    [Fact]
    public async Task Should_MarkAsFailed_When_MaxRetriesExceeded()
    {
        // Arrange: Kafka 発行で常に例外を発生
        // Act: MaxRetries 回のサイクルを実行
        // Assert:
        //   - RetryCount が MaxRetries に達していること
        //   - ステータスが FAILED であること
    }
}
```

### 10.6 API 統合テスト例

```csharp
[Trait("Category", "Integration")]
public class OrderEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public OrderEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // テスト用 DB / モック設定
            });
        }).CreateClient();
    }

    [Fact]
    public async Task Should_Return401_When_NoAuthToken()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/orders/test-id");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_Return400_When_IdempotencyKeyMissing()
    {
        // Arrange: 認証ヘッダーあり、Idempotency-Key なし
        // Act: POST /api/v1/orders
        // Assert: 400 Bad Request
    }
}
```

### 10.7 セキュリティテスト

```csharp
[Trait("Category", "Security")]
public class SecurityTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Should_Return401_When_NoToken()
    {
        // 全認証必須エンドポイントに対して 401 を確認
    }

    [Fact]
    public async Task Should_Return403_When_UserAccessesOtherUserOrders()
    {
        // IDOR 防止: 他ユーザーの注文にアクセスした場合 403 を確認
    }

    [Fact]
    public async Task Should_Return403_When_NonAdminAccessesAdminEndpoint()
    {
        // 管理者専用エンドポイントに一般ユーザーがアクセスした場合 403 を確認
    }
}
```

### 10.8 ORDER_CANCEL Saga テスト

```csharp
[Trait("Category", "Integration")]
public class CancelSagaCoordinatorTests
{
    [Fact]
    public async Task Should_CancelOrder_When_StatusIsPending()
    {
        // Arrange: PENDING 状態の注文を作成
        // Act: ExecuteCancelSagaAsync を実行
        // Assert:
        //   - Order.Status == CANCELLED
        //   - OutboxEvent（order.cancelled）が書き込まれていること
    }

    [Fact]
    public async Task Should_RefundAndCancel_When_StatusIsConfirmedAndPaymentCaptured()
    {
        // Arrange: CONFIRMED + PaymentStatus.CAPTURED の注文を作成
        // Act: ExecuteCancelSagaAsync を実行
        // Assert:
        //   - PaymentService.Refund が呼ばれること
        //   - Order.Status == REFUNDED
        //   - InventoryService.ReleaseReservation が呼ばれること
    }

    [Fact]
    public async Task Should_RejectCancel_When_StatusIsShipped()
    {
        // Arrange: SHIPPED 状態の注文を作成
        // Act: ExecuteCancelSagaAsync を実行
        // Assert:
        //   - InvalidOrderStateException がスローされること
        //   - 注文ステータスが変更されないこと
    }

    [Fact]
    public async Task Should_RetryAndFail_When_InventoryReleaseFailsThreeTimes()
    {
        // Arrange: InventoryService.ReleaseReservation が 3 回連続で失敗
        // Act: ExecuteCancelSagaAsync を実行
        // Assert:
        //   - SagaLog.Status == "FAILED"
        //   - 管理者通知が発行されること
    }

    [Fact]
    public async Task Should_SetRefundPending_When_PaymentRefundFails()
    {
        // Arrange: PaymentService.Refund が失敗
        // Act: ExecuteCancelSagaAsync を実行
        // Assert:
        //   - Order.Status == CANCELLED
        //   - Order.PaymentStatus == REFUND_PENDING
    }
}
```

### 10.9 ORDER_RETURN Saga テスト

```csharp
[Trait("Category", "Integration")]
public class ReturnSagaCoordinatorTests
{
    [Fact]
    public async Task Should_ProcessFullReturn_When_AllItemsReturned()
    {
        // Arrange: 全明細が返品対象の Return（Status=RECEIVED）を作成
        // Act: ExecuteReturnSagaAsync を実行
        // Assert:
        //   - PaymentService.PartialRefund が呼ばれること
        //   - InventoryService.RestoreInventory が呼ばれること
        //   - Order.Status == REFUNDED
    }

    [Fact]
    public async Task Should_ProcessPartialReturn_When_SomeItemsReturned()
    {
        // Arrange: 一部明細のみ返品の Return（Status=RECEIVED）を作成
        // Act: ExecuteReturnSagaAsync を実行
        // Assert:
        //   - 返品対象商品のみ返金されること
        //   - Order.Status は据え置き（DELIVERED のまま）
    }

    [Fact]
    public async Task Should_RejectReturn_When_StatusIsNotReceived()
    {
        // Arrange: Return.Status が PENDING（商品未受領）
        // Act: ExecuteReturnSagaAsync を実行
        // Assert: InvalidOrderStateException がスローされること
    }
}
```

### 10.10 Kafka Consumer テスト

```csharp
[Trait("Category", "Integration")]
public class UserDeletedConsumerTests
{
    [Fact]
    public async Task Should_AnonymizePii_When_UserDeletedEventReceived()
    {
        // Arrange: 注文・配送先付きのユーザーデータを DB に作成
        // Act: user.deleted イベントを処理
        // Assert:
        //   - Order.CustomerId が SHA-256 ハッシュに置換されていること
        //   - Shipment.RecipientName, PostalCode, Prefecture, City, AddressLine1/2, PhoneNumber が "[DELETED]" であること
        //   - Order.GuestEmail が NULL であること
        //   - Order.Notes が "[DELETED]" であること
        //   - 注文データ自体は削除されていないこと（税法 7 年保持義務）
    }
}

[Trait("Category", "Integration")]
public class PaymentCompletedConsumerTests
{
    [Fact]
    public async Task Should_TransitionToConfirmed_When_PaymentCompletedReceived()
    {
        // Arrange: PENDING 状態の注文を作成
        // Act: payment.completed イベントを処理
        // Assert:
        //   - Order.Status == CONFIRMED
        //   - Order.PaymentStatus == CAPTURED
    }
}
```

### 10.11 負荷テスト（Saga SLO 検証）

設計書 §12 に準拠。k6 または NBomber で Checkout Saga の SLO（95th percentile ≤ 1,000ms）を検証する。

```csharp
// Load/SagaSloLoadTest.cs（NBomber）
// - シナリオ: POST /api/v1/orders（注文作成）を 50 並列で 60 秒間実行
// - SLO 検証: 95th percentile レイテンシ ≤ 1,000ms
// - 補足: gRPC 外部サービスはモックまたはスタブを使用
// - 参考: k6 でも同等のテストを実行可能
```

### Phase 10 完了チェックリスト

- [ ] `dotnet test` が全件通過すること
- [ ] テストメソッド名が `Should_期待結果_When_条件` パターンであること
- [ ] AAA パターン（`// Arrange` / `// Act` / `// Assert`）のコメントがあること
- [ ] Shouldly の流暢なアサーション（`ShouldBe()`, `ShouldNotBeNull()` 等）が使用されていること
- [ ] `[Trait("Category", "Unit/Integration/Security")]` が付与されていること
- [ ] Saga 完走テスト（9 ステップ正常フロー）があること
- [ ] Saga 補償テスト（ステップ 2〜6 での障害注入 + 逆順補償）があること
- [ ] PENDING_PAYMENT テスト（決済タイムアウト → 202 レスポンス）があること
- [ ] ORDER_CANCEL Saga テスト（PENDING/CONFIRMED/PROCESSING → CANCELLED/REFUNDED）があること
- [ ] ORDER_CANCEL Saga リトライ失敗テスト（3 回失敗 → FAILED + 管理者通知）があること
- [ ] ORDER_CANCEL Saga 返金失敗テスト（CANCELLED + REFUND_PENDING）があること
- [ ] ORDER_RETURN Saga テスト（全品返品 → REFUNDED、部分返品 → 据え置き）があること
- [ ] ORDER_RETURN Saga 状態検証テスト（RECEIVED 以外 → 拒否）があること
- [ ] UserDeletedConsumer PII 匿名化テスト（SHA-256 ハッシュ化、`[DELETED]` 置換、NULL 設定）があること
- [ ] PaymentCompletedConsumer ステータス遷移テスト（PENDING → CONFIRMED）があること
- [ ] ShippingFeeCalculator テスト（会員ランク別送料、地域別追加料金、速達・大型商品料金）があること
- [ ] OrderNumberGenerator テスト（`ORD-yyyyMMdd-NNNNN` 形式、一意性）があること
- [ ] Saga SLO 負荷テスト（95th percentile ≤ 1,000ms）のシナリオが定義されていること
- [ ] OutboxPublisher テスト（発行・バックオフ・リトライ）があること
- [ ] API 統合テスト（認証・バリデーション・Idempotency-Key）があること
- [ ] テストコードに本番個人情報が含まれていないこと
- [ ] テスト間の共有状態がないこと（各テストが独立して実行可能）
- [ ] `dotnet test --collect:"XPlat Code Coverage"` でカバレッジ 80% 以上であること
- [ ] **TODO/FIXME/HACK コメント残存チェック**: ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" SalesManagementService/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック**: テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック**: 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 11: 最終統合 & デプロイ準備

### 目的

全フェーズの成果物を統合し、ミドルウェアパイプラインの最終順序を確定し、Dockerfile と AppHost を完成させる。

### 作成・更新ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `SalesManagementService/Program.cs` | 全 DI 登録 + ミドルウェアパイプライン統合 |
| 2 | `SalesManagementService/Dockerfile` | 最終版（Phase 1 から更新） |
| 3 | `AppHost/Program.cs` | .NET Aspire オーケストレーションに SalesManagementService を追加 |

### 11.1 Program.cs 統合ビュー

設計書 §H に完全準拠。全 DI 登録 + ミドルウェアパイプライン順序。

```csharp
var builder = WebApplication.CreateBuilder(args);

// ── Serilog ──
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "SalesManagementService")
        .WriteTo.Console(new CompactJsonFormatter()));

// ── EF Core (PostgreSQL) ──
builder.Services.AddDbContext<SalesDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("salesdb")));
builder.Services.AddSingleton(TimeProvider.System);

// ── Repository 登録 ──
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IShipmentRepository, ShipmentRepository>();
builder.Services.AddScoped<IReturnRepository, ReturnRepository>();
builder.Services.AddScoped<ISagaLogRepository, SagaLogRepository>();
builder.Services.AddScoped<IOutboxEventRepository, OutboxEventRepository>();
builder.Services.AddScoped<IIdempotencyKeyRepository, IdempotencyKeyRepository>();

// ── Service 登録 ──
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IShipmentService, ShipmentService>();
builder.Services.AddScoped<IReturnService, ReturnService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<ISagaCoordinator, SagaCoordinator>();
builder.Services.AddScoped<CancelSagaCoordinator>();
builder.Services.AddScoped<ReturnSagaCoordinator>();
builder.Services.AddScoped<IOutboxWriter, OutboxWriter>();
builder.Services.AddScoped<ShippingFeeCalculator>();
builder.Services.AddScoped<OrderNumberGenerator>();
builder.Services.AddSingleton<OrderCacheService>();

// ── FluentValidation ──
builder.Services.AddValidatorsFromAssemblyContaining<OrderCreateRequestValidator>();

// ── gRPC クライアント（Polly リトライ付き） ──
builder.Services.AddGrpcClient<InventoryService.InventoryServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["GrpcClients:InventoryService"]!))
    .AddStandardResilienceHandler();

builder.Services.AddGrpcClient<CouponService.CouponServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["GrpcClients:CouponService"]!))
    .AddStandardResilienceHandler();

builder.Services.AddGrpcClient<PointService.PointServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["GrpcClients:PointService"]!))
    .AddStandardResilienceHandler();

builder.Services.AddGrpcClient<CartService.CartServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["GrpcClients:CartService"]!))
    .AddStandardResilienceHandler();

builder.Services.AddGrpcClient<PaymentService.PaymentServiceClient>(o =>
    o.Address = new Uri(builder.Configuration["GrpcClients:PaymentService"]!))
    .AddStandardResilienceHandler();

// ── Kafka Producer ──
builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var config = new ProducerConfig
    {
        BootstrapServers = builder.Configuration["Kafka:BootstrapServers"],
        Acks = Acks.All,
        EnableIdempotence = true
    };
    return new ProducerBuilder<string, string>(config).Build();
});

// ── BackgroundService ──
builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddHostedService<SagaRecoveryService>();
builder.Services.AddHostedService<PaymentCompletedConsumer>();
builder.Services.AddHostedService<PaymentFailedConsumer>();
builder.Services.AddHostedService<InventoryReservedConsumer>();
builder.Services.AddHostedService<InventoryReleasedConsumer>();
builder.Services.AddHostedService<UserDeletedConsumer>();
builder.Services.AddHostedService<IdempotencyKeyCleanupService>();
builder.Services.AddHostedService<SagaLogArchivalService>();

// ── レート制限 ──
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("order-create", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("admin-api", limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

// ── 認証・認可 ──
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
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ── ヘルスチェック ──
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("salesdb")!,
        name: "salesdb-postgresql",
        tags: ["ready"])
    .AddRedis(
        builder.Configuration.GetConnectionString("redis")!,
        name: "redis",
        tags: ["ready"])
    .AddKafka(
        new ProducerConfig
        {
            BootstrapServers = builder.Configuration["Kafka:BootstrapServers"]
        },
        name: "kafka",
        tags: ["ready"]);

// ── OpenTelemetry ──
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddGrpcClientInstrumentation()
        .AddSource("SkiShop.SalesManagement.*"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation());

var app = builder.Build();

// ── ミドルウェアパイプライン（順序厳守） ──

// 1. 例外ハンドラー（最も外側で全例外をキャッチ）
app.UseExceptionHandler();

// 2. セキュリティヘッダー
app.UseHsts();
app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'");
    await next();
});

// 3. Correlation ID
app.UseCorrelationId();

// 4. Serilog リクエストログ
app.UseSerilogRequestLogging();

// 5. CORS
app.UseCors();

// 6. 認証・認可
app.UseAuthentication();
app.UseAuthorization();

// 7. レート制限
app.UseRateLimiter();

// 8. エンドポイントマッピング
app.MapOrderEndpoints();
app.MapShipmentEndpoints();
app.MapReturnEndpoints();
app.MapReportEndpoints();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();

app.Run();

// ✅ WebApplicationFactory テスト用にクラスを公開
public partial class Program;
```

### 11.2 AppHost への SalesManagementService 追加

```csharp
// AppHost/Program.cs に追加
var salesService = builder
    .AddProject<Projects.SalesManagementService>("sales-management-service")
    .WithReference(postgres)
    .WithReference(redis)
    .WithReference(kafka);
```

### 11.3 appsettings.json — App セクション

設計書 §15 に完全準拠。注文・送料・返品・Saga・Outbox の業務パラメータを型安全な設定クラスで管理する。

```jsonc
// appsettings.json に追加
{
  "App": {
    "Order": {
      "ExpiryHours": 24,
      "AutoCancelEnabled": true
    },
    "Shipping": {
      "FreeShippingThreshold": 10000,
      "DefaultShippingFee": 550,
      "HokkaidoOkinawaFee": 1100,
      "ExpressSurcharge": 330,
      "LargeItemSurcharge": 1650,
      "MemberRankDiscounts": {
        "Silver": 8000,
        "Gold": 5000,
        "Platinum": 0
      }
    },
    "Return": {
      "AllowedDays": 30,
      "AutoApprovalThreshold": 5000
    },
    "Saga": {
      "SloDeadlineMs": 1000,
      "CompensationTimeoutSeconds": 30,
      "RecoveryPollingIntervalSeconds": 30,
      "StallThresholdMinutes": 5,
      "PaymentPollingTimeoutMinutes": 30
    },
    "Outbox": {
      "MinPollingIntervalMs": 100,
      "MaxPollingIntervalMs": 5000,
      "BatchSize": 50,
      "MaxRetries": 5
    }
  }
}
```

```csharp
// Configurations/AppSettings.cs — IOptions<T> 用設定クラス（record 推奨）
public record OrderSettings(int ExpiryHours = 24, bool AutoCancelEnabled = true);
public record ShippingSettings(
    decimal FreeShippingThreshold = 10000,
    decimal DefaultShippingFee = 550,
    decimal HokkaidoOkinawaFee = 1100,
    decimal ExpressSurcharge = 330,
    decimal LargeItemSurcharge = 1650,
    Dictionary<string, decimal>? MemberRankDiscounts = null);
public record ReturnSettings(int AllowedDays = 30, decimal AutoApprovalThreshold = 5000);
public record SagaSettings(
    int SloDeadlineMs = 1000,
    int CompensationTimeoutSeconds = 30,
    int RecoveryPollingIntervalSeconds = 30,
    int StallThresholdMinutes = 5,
    int PaymentPollingTimeoutMinutes = 30);
public record OutboxSettings(
    int MinPollingIntervalMs = 100,
    int MaxPollingIntervalMs = 5000,
    int BatchSize = 50,
    int MaxRetries = 5);

// Program.cs での登録（ValidateOnStart 必須）
builder.Services.AddOptions<OrderSettings>()
    .Bind(builder.Configuration.GetSection("App:Order"))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<ShippingSettings>()
    .Bind(builder.Configuration.GetSection("App:Shipping"))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<ReturnSettings>()
    .Bind(builder.Configuration.GetSection("App:Return"))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<SagaSettings>()
    .Bind(builder.Configuration.GetSection("App:Saga"))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<OutboxSettings>()
    .Bind(builder.Configuration.GetSection("App:Outbox"))
    .ValidateDataAnnotations().ValidateOnStart();
```

### 11.4 最終 Dockerfile

Phase 1 で作成した Dockerfile を確認し、以下が満たされていることを保証する:
- マルチステージビルド（sdk → aspnet）
- 非 root ユーザー（skishop）
- HEALTHCHECK（`/health`）
- `ASPNETCORE_ENVIRONMENT=Production`
- ベースイメージに `latest` タグなし

### Phase 11 完了チェックリスト

- [ ] `dotnet build` が成功すること
- [ ] `dotnet publish -c Release` が成功すること
- [ ] ミドルウェアパイプライン順序が AGENTS.md §11.3 に準拠していること（ExceptionHandler → HSTS → CORS → Auth → RateLimiter → Endpoints）
- [ ] `UseAuthentication()` が `UseAuthorization()` より前に配置されていること
- [ ] `UseExceptionHandler()` が最も外側に配置されていること
- [ ] `UseCors()` が `UseAuthentication()` より前に配置されていること
- [ ] Dockerfile: ベースイメージに `latest` タグなし
- [ ] Dockerfile: 非 root ユーザーで実行
- [ ] Dockerfile: HEALTHCHECK 設定あり
- [ ] AppHost: `WithReference` で PostgreSQL, Redis, Kafka が参照されていること
- [ ] appsettings.json に `App` セクション（Order, Shipping, Return, Saga, Outbox）が定義されていること
- [ ] `IOptions<T>` 用設定クラスが record で定義され、`ValidateOnStart()` が設定されていること
- [ ] SagaCoordinator / OutboxPublisher / SagaRecoveryService が `IOptions<SagaSettings>` / `IOptions<OutboxSettings>` を使用し、マジックナンバーが排除されていること
- [ ] ShippingFeeCalculator が `IOptions<ShippingSettings>` を使用していること
- [ ] 全 Kafka Consumer（5 件）が `AddHostedService<>()` で登録されていること
- [ ] メンテナンス BackgroundService（IdempotencyKeyCleanupService, SagaLogArchivalService）が登録されていること
- [ ] レート制限（order-create, admin-api）が `AddRateLimiter()` で登録されていること
- [ ] OrderCacheService が DI 登録されていること
- [ ] **TODO/FIXME/HACK コメント残存チェック**: ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" SalesManagementService/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック**: テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック**: 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## 横断的な規約遵守チェックリスト

全フェーズ完了後に以下の最終確認を実施する。

### AGENTS.md 禁止事項チェック

```bash
# 秘密情報のハードコードチェック
grep -r "Password\s*=\s*\"" --include="*.cs" SalesManagementService/

# Console.WriteLine チェック
grep -r "Console\.Write" --include="*.cs" SalesManagementService/

# SQL 文字列結合チェック
grep -r "FromSqlRaw.*\+" --include="*.cs" SalesManagementService/

# .Result / .Wait() チェック（デッドロックリスク）
grep -rP "\.(Result|Wait)\(\)" --include="*.cs" SalesManagementService/

# プロパティインジェクションチェック
grep -r "\[Inject\]" --include="*.cs" SalesManagementService/

# DateTime.Now チェック（ローカル時刻禁止 → DateTime.UtcNow / TimeProvider を使用）
grep -r "DateTime\.Now[^U]" --include="*.cs" SalesManagementService/

# new HttpClient() チェック（IHttpClientFactory 経由のみ）
grep -r "new HttpClient()" --include="*.cs" SalesManagementService/

# 文字列補間ログチェック
grep -rP '_logger\.Log\w+\(\$"' --include="*.cs" SalesManagementService/

# Thread.Sleep() チェック
grep -r "Thread\.Sleep" --include="*.cs" SalesManagementService/
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
| 8 | TimeProvider | `DateTime.UtcNow` 直接使用禁止、`TimeProvider` DI 経由 |
| 9 | PII ログ禁止 | 配送先住所、電話番号、メールアドレスをログに出力しない |
| 10 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 11 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 12 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

### テスト規約チェック

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | 命名 | `Should_期待結果_When_条件` パターン |
| 2 | AAA パターン | `// Arrange` / `// Act` / `// Assert` のコメント付き |
| 3 | アサーション | Shouldly 使用（`ShouldBe()`, `ShouldNotBeNull()` 等） |
| 4 | カテゴリ | `[Trait("Category", "...")]` 付与 |
| 5 | 独立性 | テスト間の共有状態なし |
| 6 | PII | テストコードに本番個人情報なし |
| 7 | カバレッジ | 分岐カバレッジ 80% 以上 |
| 8 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 9 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 10 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

### Saga 固有チェック

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | 9 ステップ順次実行 | カート→在庫→クーポン→ポイント→注文→決済→ポイント確定→カートクリア→Outbox |
| 2 | gRPC Deadline | ステップ 1:200ms, 2:500ms, 3:300ms, 4:300ms, 7:300ms, 8:200ms |
| 3 | 補償逆順 | 失敗ステップ-1 → 1 の逆順 |
| 4 | べき等性 | 各補償ステップが `saga_step_id` で重複検知 |
| 5 | 補償タイムアウト | 専用 `CancellationTokenSource(30 秒)` |
| 6 | 後処理非補償 | ステップ 7〜9 が補償対象外 |
| 7 | PENDING_PAYMENT | 決済タイムアウト → SagaLog.Status = "PENDING_PAYMENT" |
| 8 | SagaRecoveryService | 30 秒ポーリング、5 分滞留閾値、30 分 PENDING_PAYMENT タイムアウト |
| 9 | Advisory Lock | OutboxPublisher が `pg_try_advisory_lock` を使用 |
| 10 | 動的バックオフ | 100ms（イベントあり）→ 指数増加 → 5s（イベントなし） |
| 11 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 12 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 13 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## フェーズ間依存関係

```
Phase 1（基盤）
    ↓
Phase 2（エンティティ & DbContext）── IsGuest/GuestEmail フィールド含む
    ↓
Phase 3（Repository 層）
    ↓
Phase 4（Service 層）── DTO, Validator, 例外クラス, OrderStateMachine, TaxCalculator, ShippingFeeCalculator, OrderNumberGenerator
    ↓
Phase 5（Endpoints）── Minimal API, IDOR 防止, Idempotency-Key
    ↓
Phase 6（Kafka + Outbox）── OutboxWriter, OutboxPublisher, 5 Kafka Consumers, メンテナンス BackgroundService
    ↓
Phase 7（Saga オーケストレーション）── ORDER_CHECKOUT 9 ステップ, ORDER_CANCEL 6 ステップ, ORDER_RETURN 5 ステップ, SagaRecoveryService
    ↓
Phase 8（可観測性 & 耐障害性）── OpenTelemetry, Serilog, HealthCheck, Polly, Correlation ID, カスタムメトリクス 10 項目, Redis キャッシュ
    ↓
Phase 9（セキュリティ）── JWT 認証, Fallback Policy, グローバル例外ハンドラー, セキュリティヘッダー, エラーコード体系 (ORD-4001〜5003), レート制限
    ↓
Phase 10（テスト）── Unit, Integration, Saga (CHECKOUT/CANCEL/RETURN), Kafka Consumer, Outbox, API, Security, 負荷テスト
    ↓
Phase 11（最終統合）── 全ミドルウェア統合, App 設定セクション, Dockerfile 最終版, AppHost 追加
```

> **注記**: Phase 4 で DTO・バリデーター・例外クラスを作成し、Phase 5 以降で利用する。Phase 6（Outbox）と Phase 7（Saga）は密接に関連するが、Outbox は独立して動作可能なため先に実装する。Phase 8〜9 は並行可能だが、セキュリティ（Phase 9）をミドルウェアパイプラインに統合する際に Phase 8 の Correlation ID が必要となるため順序通りに実施すること。

---

## 参照ドキュメント

| ドキュメント | 参照セクション |
|------------|-------------|
| `design-docs/sales-management-design.md` | 全セクション（§1-§13、付録 §A-§J） |
| `design-docs/spec.md` | ADR-0005（Outbox パターン）、ADR-0006（サービス別独立 DB）、ADR-0009（Saga オーケストレーション） |
| `AGENTS.md` | §2（アーキテクチャ）、§3（DDD）、§4（コーディング規約）、§5（セキュリティ）、§6（API 設計）、§7（設定ファイル）、§8（NuGet）、§9（テスト）、§10.4（CheckoutService Saga）、§10.6（Kafka）、§11（耐障害性・可観測性・ミドルウェア） |
| `.github/instructions/` | `dotnet-coding-standards.instructions.md`, `security-coding.instructions.md`, `api-design.instructions.md`, `dotnet-config.instructions.md`, `nuget-dependency.instructions.md`, `test-standards.instructions.md`, `dockerfile-infra.instructions.md`, `sql-schema-review.instructions.md` |
