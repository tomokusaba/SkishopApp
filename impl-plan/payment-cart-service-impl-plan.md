# PaymentCartService 実装計画

> **対象サービス**: PaymentCartService（決済・カート管理サービス）
> **ポート**: 5005
> **DB**: PostgreSQL（サービス専用独立 DB）/ Redis（カートキャッシュ）/ Kafka（イベント駆動）
> **決済ゲートウェイ**: Stripe（Hosted Payment Page — PCI DSS SAQ A 準拠）
> **設計書**: `design-docs/payment-cart-service-design.md`
> **規約**: `.github/instructions/dotnet-coding-standards.instructions.md`, `AGENTS.md`

---

## 目次

1. [Phase 1: プロジェクト基盤構築](#phase-1-プロジェクト基盤構築)
2. [Phase 2: エンティティ・Value Object・Enum 定義](#phase-2-エンティティvalue-objectenum-定義)
3. [Phase 3: Repository 層実装](#phase-3-repository-層実装)
4. [Phase 4: Service 層実装](#phase-4-service-層実装)
5. [Phase 5: Endpoints 実装](#phase-5-endpoints-実装)
6. [Phase 6: Kafka イベント連携](#phase-6-kafka-イベント連携)
7. [Phase 7: Redis キャッシュ連携](#phase-7-redis-キャッシュ連携)
8. [Phase 8: 認証・認可・セキュリティ実装](#phase-8-認証認可セキュリティ実装)
9. [Phase 9: 単体テスト・統合テスト](#phase-9-単体テスト統合テスト)
10. [Phase 10: 可観測性（OpenTelemetry, HealthCheck, 構造化ログ）](#phase-10-可観測性opentelemetry-healthcheck-構造化ログ)
11. [Phase 11: Docker / デプロイ準備](#phase-11-docker--デプロイ準備)

---

## Phase 1: プロジェクト基盤構築

### 目的

PaymentCartService のプロジェクト構造を作成し、`.csproj`、`Program.cs` スケルトン、`appsettings.json`、ディレクトリ構成を整備する。ビルドが通る最小構成を確立する。

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `PaymentCartService/PaymentCartService.csproj` | プロジェクト定義（NuGet パッケージ参照） |
| 2 | `PaymentCartService/Program.cs` | エントリポイント・DI 登録・ミドルウェアスケルトン |
| 3 | `PaymentCartService/appsettings.json` | 共通設定（安全なデフォルト値） |
| 4 | `PaymentCartService/appsettings.Development.json` | 開発用設定 |
| 5 | `PaymentCartService/appsettings.Production.json` | 本番用設定（環境変数参照のみ） |
| 6 | `PaymentCartService/Properties/launchSettings.json` | 起動設定（ポート 5005） |
| 7 | `PaymentCartService/Configurations/CartSettings.cs` | カート設定クラス |
| 8 | `PaymentCartService/Configurations/PaymentSettings.cs` | 決済設定クラス |
| 9 | `PaymentCartService/Configurations/StripeSettings.cs` | Stripe 設定クラス |

### 1.1 ディレクトリ構造

```
PaymentCartService/
├── PaymentCartService.csproj
├── Program.cs
├── Properties/
│   └── launchSettings.json
├── appsettings.json
├── appsettings.Development.json
├── appsettings.Production.json
├── Configurations/
│   ├── CartSettings.cs
│   ├── PaymentSettings.cs
│   └── StripeSettings.cs
├── Endpoints/
├── Services/
│   └── Interfaces/
├── Repositories/
│   └── Interfaces/
├── Models/
│   ├── Enums/
│   └── ValueObjects/
├── DTOs/
│   ├── Requests/
│   └── Responses/
├── Infrastructure/
│   ├── Persistence/
│   └── Kafka/
├── Exceptions/
├── Validators/
└── Migrations/
```

### 1.2 PaymentCartService.csproj

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

    <!-- 決済ゲートウェイ -->
    <PackageReference Include="Stripe.net" Version="46.*" />

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
    <PackageReference Include="AspNetCore.HealthChecks.NpgSql" Version="9.*" />
    <PackageReference Include="AspNetCore.HealthChecks.Redis" Version="9.*" />
  </ItemGroup>
</Project>
```

### 1.3 Configurations（設定クラス）

```csharp
// Configurations/CartSettings.cs
namespace PaymentCartService.Configurations;

public record CartSettings
{
    public int ExpiryDays { get; init; } = 7;
    public int MaxItemsPerCart { get; init; } = 50;
    public int MaxQuantityPerItem { get; init; } = 10;
    public int CleanupIntervalMinutes { get; init; } = 60;
    public int CleanupBatchSize { get; init; } = 500;
    public int ExpirationCheckIntervalMinutes { get; init; } = 10;
    public int ExpirationBatchSize { get; init; } = 200;
}
```

```csharp
// Configurations/PaymentSettings.cs
namespace PaymentCartService.Configurations;

public record PaymentSettings
{
    public string DefaultCurrency { get; init; } = "JPY";
    public int MaxRetries { get; init; } = 3;
    public int WebhookToleranceSeconds { get; init; } = 300;
}
```

```csharp
// Configurations/StripeSettings.cs
namespace PaymentCartService.Configurations;

public record StripeSettings
{
    public string SecretKey { get; init; } = string.Empty;
    public string WebhookSecret { get; init; } = string.Empty;
    public string SuccessUrl { get; init; } = string.Empty;
    public string CancelUrl { get; init; } = string.Empty;
}
```

### 1.4 appsettings.json

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
  "App": {
    "Cart": {
      "ExpiryDays": 7,
      "MaxItemsPerCart": 50,
      "MaxQuantityPerItem": 10,
      "CleanupIntervalMinutes": 60,
      "CleanupBatchSize": 500,
      "ExpirationCheckIntervalMinutes": 10,
      "ExpirationBatchSize": 200
    },
    "Payment": {
      "DefaultCurrency": "JPY",
      "MaxRetries": 3,
      "WebhookToleranceSeconds": 300
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

### 1.7 Program.cs スケルトン

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PaymentCartService.Configurations;
using PaymentCartService.Infrastructure.Persistence;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ──
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "PaymentCartService")
        .WriteTo.Console(new CompactJsonFormatter()));

// ── 設定バインド（IOptions<T> + ValidateOnStart） ──
builder.Services.AddOptions<CartSettings>()
    .Bind(builder.Configuration.GetSection("App:Cart"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<PaymentSettings>()
    .Bind(builder.Configuration.GetSection("App:Payment"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<StripeSettings>()
    .Bind(builder.Configuration.GetSection("Stripe"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ── TimeProvider（テスタビリティ） ──
builder.Services.AddSingleton(TimeProvider.System);

// ── EF Core（PostgreSQL） ──
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("接続文字列が設定されていません");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// ── FluentValidation ──
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ── DI 登録（Phase 3–4 で追加） ──
// builder.Services.AddScoped<ICartRepository, CartRepository>();
// builder.Services.AddScoped<ICartService, CartService>();

// ── 認証・認可（Phase 8 で詳細実装） ──
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

var app = builder.Build();

// ── ミドルウェアパイプライン ──
app.UseExceptionHandler(handler =>
{
    handler.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var error = feature?.Error;

        if (error is not null)
            logger.LogError(error, "予期しないエラー: {Message}", error.Message);

        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new { detail = "内部エラーが発生しました" });
    });
});

app.UseAuthentication();
app.UseAuthorization();

// ── エンドポイント（Phase 5 で追加） ──
app.MapGet("/health", () => Results.Ok(new { Status = "UP" })).AllowAnonymous();

app.Run();
```

### Phase 1 完了チェックリスト

- [ ] `dotnet build PaymentCartService/` — ビルドが成功する
- [ ] `PaymentCartService.csproj` に `TreatWarningsAsErrors=true` が設定されている
- [ ] `appsettings.json` に秘密情報が含まれていない
- [ ] ディレクトリ構造が AGENTS.md 準拠である（Endpoints, Services, Repositories, Models, DTOs 等）
- [ ] `Program.cs` に `ILogger<T>` + Serilog が設定されている
- [ ] `IOptions<T>` + `ValidateOnStart()` で設定クラスがバインドされている
- [ ] `TimeProvider.System` が DI 登録されている
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと（`NotImplementedException`, 仮のハードコード値が残存していないこと）
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディや `=> throw new NotImplementedException()` が本番コードに残存していないこと

---

## Phase 2: エンティティ・Value Object・Enum 定義

### 目的

PaymentCartService の全エンティティ（Cart, CartItem, Payment, Transaction, PaymentMethod, OutboxEvent）、Value Object（ShippingAddress, Money）、Enum（CartStatus, PaymentStatus, TransactionType, OutboxEventStatus, PaymentMethodType）を定義し、EF Core DbContext と Initial Migration を作成する。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `PaymentCartService/Models/Enums/CartStatus.cs` | 作成 | カートステータス Enum |
| 2 | `PaymentCartService/Models/Enums/PaymentStatus.cs` | 作成 | 決済ステータス Enum |
| 3 | `PaymentCartService/Models/Enums/TransactionType.cs` | 作成 | トランザクション種別 Enum |
| 4 | `PaymentCartService/Models/Enums/OutboxEventStatus.cs` | 作成 | Outbox イベントステータス Enum |
| 5 | `PaymentCartService/Models/Enums/PaymentMethodType.cs` | 作成 | 決済方法種別 Enum |
| 6 | `PaymentCartService/Models/ValueObjects/ShippingAddress.cs` | 作成 | 配送先住所 Value Object |
| 7 | `PaymentCartService/Models/ValueObjects/Money.cs` | 作成 | 金額 Value Object |
| 8 | `PaymentCartService/Models/Cart.cs` | 作成 | カートエンティティ（Aggregate Root） |
| 9 | `PaymentCartService/Models/CartItem.cs` | 作成 | カートアイテムエンティティ |
| 10 | `PaymentCartService/Models/Payment.cs` | 作成 | 決済エンティティ（Aggregate Root） |
| 11 | `PaymentCartService/Models/Transaction.cs` | 作成 | トランザクションエンティティ |
| 12 | `PaymentCartService/Models/PaymentMethod.cs` | 作成 | 決済方法エンティティ |
| 13 | `PaymentCartService/Models/OutboxEvent.cs` | 作成 | Outbox イベントエンティティ |
| 14 | `PaymentCartService/Infrastructure/Persistence/AppDbContext.cs` | 作成 | EF Core DbContext |
| 15 | `PaymentCartService/Program.cs` | 更新 | DbContext DI 登録確認 |

### 2.1 Enum 定義

```csharp
// Models/Enums/CartStatus.cs
namespace PaymentCartService.Models.Enums;

public enum CartStatus
{
    Active,
    Expired,
    CheckedOut,
    Abandoned
}
```

```csharp
// Models/Enums/PaymentStatus.cs
namespace PaymentCartService.Models.Enums;

public enum PaymentStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Refunded,
    Cancelled
}
```

```csharp
// Models/Enums/TransactionType.cs
namespace PaymentCartService.Models.Enums;

public enum TransactionType
{
    Charge,
    Refund,
    Capture
}
```

```csharp
// Models/Enums/OutboxEventStatus.cs
namespace PaymentCartService.Models.Enums;

public enum OutboxEventStatus
{
    Pending,
    Processing,
    Published,
    Failed,
    DeadLetter
}
```

```csharp
// Models/Enums/PaymentMethodType.cs
namespace PaymentCartService.Models.Enums;

public enum PaymentMethodType
{
    CreditCard,
    ConvenienceStore,
    BankTransfer
}
```

### 2.2 Value Object 定義

```csharp
// Models/ValueObjects/Money.cs
namespace PaymentCartService.Models.ValueObjects;

public readonly record struct Money(decimal Amount, string CurrencyCode = "JPY")
{
    public Money Add(Money other)
    {
        if (CurrencyCode != other.CurrencyCode)
            throw new InvalidOperationException("通貨単位が異なります");
        return this with { Amount = Amount + other.Amount };
    }

    public Money Subtract(Money other)
    {
        if (CurrencyCode != other.CurrencyCode)
            throw new InvalidOperationException("通貨単位が異なります");
        return this with { Amount = Amount - other.Amount };
    }
}
```

```csharp
// Models/ValueObjects/ShippingAddress.cs
namespace PaymentCartService.Models.ValueObjects;

public record ShippingAddress(
    string RecipientName,
    string PostalCode,
    string Prefecture,
    string City,
    string AddressLine1,
    string? AddressLine2,
    string PhoneNumber);
```

### 2.3 エンティティ定義

```csharp
// Models/Cart.cs — Aggregate Root
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PaymentCartService.Models.Enums;

namespace PaymentCartService.Models;

[Table("carts")]
public class Cart
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("customer_id")]
    [MaxLength(100)]
    public string? CustomerId { get; set; }

    [Column("session_id")]
    [Required]
    [MaxLength(100)]
    public string SessionId { get; set; } = string.Empty;

    [Column("status")]
    [Required]
    [MaxLength(20)]
    public CartStatus Status { get; set; } = CartStatus.Active;

    [Column("expires_at")]
    [Required]
    public DateTime ExpiresAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    public ICollection<CartItem> Items { get; set; } = [];

    // ── Aggregate Root ビジネスメソッド ──

    public void AddItem(string productId, string productName, string sku, decimal unitPrice, int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        var existing = Items.FirstOrDefault(i => i.ProductId == productId);
        if (existing is not null)
        {
            existing.UpdateQuantity(existing.Quantity + quantity);
            return;
        }

        Items.Add(new CartItem
        {
            CartId = Id,
            ProductId = productId,
            ProductName = productName,
            Sku = sku,
            UnitPrice = unitPrice,
            Quantity = quantity,
            Subtotal = unitPrice * quantity
        });
    }

    public void UpdateItemQuantity(string itemId, int quantity)
    {
        var item = Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"CartItem {itemId} not found");
        item.UpdateQuantity(quantity);
    }

    public void RemoveItem(string itemId)
    {
        var item = Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException($"CartItem {itemId} not found");
        Items.Remove(item);
    }

    public void ClearItems() => Items.Clear();

    public decimal CalculateTotal() => Items.Sum(i => i.Subtotal);

    public void MarkAsCheckedOut() => Status = CartStatus.CheckedOut;

    public void MarkAsExpired() => Status = CartStatus.Expired;

    public void MarkAsAbandoned() => Status = CartStatus.Abandoned;
}
```

```csharp
// Models/CartItem.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PaymentCartService.Models;

[Table("cart_items")]
public class CartItem
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("cart_id")]
    [Required]
    [MaxLength(36)]
    public string CartId { get; set; } = string.Empty;

    [Column("product_id")]
    [Required]
    [MaxLength(100)]
    public string ProductId { get; set; } = string.Empty;

    [Column("product_name")]
    [Required]
    [MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    [Column("sku")]
    [Required]
    [MaxLength(100)]
    public string Sku { get; set; } = string.Empty;

    [Column("unit_price")]
    [Required]
    [Column(TypeName = "decimal(12,2)")]
    public decimal UnitPrice { get; set; }

    [Column("quantity")]
    [Required]
    public int Quantity { get; set; }

    [Column("subtotal")]
    [Required]
    [Column(TypeName = "decimal(12,2)")]
    public decimal Subtotal { get; set; }

    [Column("added_at")]
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(CartId))]
    public Cart Cart { get; set; } = null!;

    public void UpdateQuantity(int newQuantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newQuantity);
        Quantity = newQuantity;
        Subtotal = UnitPrice * newQuantity;
    }
}
```

```csharp
// Models/Payment.cs — Aggregate Root
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PaymentCartService.Models.Enums;

namespace PaymentCartService.Models;

[Table("payments")]
public class Payment
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("order_id")]
    [Required]
    [MaxLength(36)]
    public string OrderId { get; set; } = string.Empty;

    [Column("customer_id")]
    [Required]
    [MaxLength(100)]
    public string CustomerId { get; set; } = string.Empty;

    [Column("stripe_checkout_session_id")]
    [MaxLength(100)]
    public string? StripeCheckoutSessionId { get; set; }

    [Column("stripe_payment_intent_id")]
    [MaxLength(100)]
    public string? StripePaymentIntentId { get; set; }

    [Column("stripe_charge_id")]
    [MaxLength(100)]
    public string? StripeChargeId { get; set; }

    [Column("amount", TypeName = "decimal(12,2)")]
    [Required]
    public decimal Amount { get; set; }

    [Column("currency_code")]
    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "JPY";

    [Column("status")]
    [Required]
    [MaxLength(20)]
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    [Column("payment_method")]
    [Required]
    [MaxLength(50)]
    public string PaymentMethod { get; set; } = string.Empty;

    [Column("failure_code")]
    [MaxLength(100)]
    public string? FailureCode { get; set; }

    [Column("failure_message")]
    [MaxLength(500)]
    public string? FailureMessage { get; set; }

    [Column("paid_at")]
    public DateTime? PaidAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("created_by")]
    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    [Column("updated_by")]
    [MaxLength(100)]
    public string? UpdatedBy { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = [];

    // ── Aggregate Root ビジネスメソッド ──

    public void MarkAsCompleted(string stripePaymentIntentId, string? stripeChargeId, DateTime paidAt)
    {
        Status = PaymentStatus.Completed;
        StripePaymentIntentId = stripePaymentIntentId;
        StripeChargeId = stripeChargeId;
        PaidAt = paidAt;
    }

    public void MarkAsFailed(string failureCode, string failureMessage)
    {
        Status = PaymentStatus.Failed;
        FailureCode = failureCode;
        FailureMessage = failureMessage;
    }

    public void MarkAsRefunded() => Status = PaymentStatus.Refunded;

    public void MarkAsCancelled() => Status = PaymentStatus.Cancelled;
}
```

```csharp
// Models/Transaction.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PaymentCartService.Models.Enums;

namespace PaymentCartService.Models;

[Table("transactions")]
public class Transaction
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("payment_id")]
    [Required]
    [MaxLength(36)]
    public string PaymentId { get; set; } = string.Empty;

    [Column("type")]
    [Required]
    [MaxLength(30)]
    public TransactionType Type { get; set; }

    [Column("amount", TypeName = "decimal(12,2)")]
    [Required]
    public decimal Amount { get; set; }

    [Column("status")]
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = string.Empty;

    [Column("gateway_response")]
    public string? GatewayResponse { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(PaymentId))]
    public Payment Payment { get; set; } = null!;
}
```

```csharp
// Models/PaymentMethod.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PaymentCartService.Models.Enums;

namespace PaymentCartService.Models;

[Table("payment_methods")]
public class PaymentMethod
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("user_id")]
    [Required]
    [MaxLength(100)]
    public string UserId { get; set; } = string.Empty;

    [Column("type")]
    [Required]
    [MaxLength(30)]
    public PaymentMethodType Type { get; set; }

    [Column("provider")]
    [Required]
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;

    [Column("account_reference")]
    [Required]
    [MaxLength(255)]
    public string AccountReference { get; set; } = string.Empty;

    [Column("is_default")]
    public bool IsDefault { get; set; }

    [Column("expiry_date")]
    public DateTime? ExpiryDate { get; set; }

    [Column("billing_address_id")]
    [MaxLength(36)]
    public string? BillingAddressId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

```csharp
// Models/OutboxEvent.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PaymentCartService.Models.Enums;

namespace PaymentCartService.Models;

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

    [Column("payload")]
    [Required]
    public string Payload { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("published_at")]
    public DateTime? PublishedAt { get; set; }

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
    public OutboxEventStatus Status { get; set; } = OutboxEventStatus.Pending;
}
```

### 2.4 IHasTimestamps マーカーインターフェース

設計書 §30 準拠。`SaveChangesAsync` オーバーライドで `CreatedAt` / `UpdatedAt` を自動管理するためのマーカーインターフェース。

```csharp
// Models/IHasTimestamps.cs
namespace PaymentCartService.Models;

public interface IHasTimestamps
{
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}
```

> **適用対象**: Cart, Payment, OutboxEvent エンティティに `IHasTimestamps` を実装する。  
> 例: `public class Cart : IHasTimestamps`

### 2.5 AppDbContext

```csharp
// Infrastructure/Persistence/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using PaymentCartService.Models;
using PaymentCartService.Models.Enums;

namespace PaymentCartService.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider timeProvider)
    : DbContext(options)
{
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Cart ──
        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasMany(c => c.Items)
                .WithOne(i => i.Cart)
                .HasForeignKey(i => i.CartId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Status)
                .HasConversion<string>();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.SessionId).HasDatabaseName("idx_carts_session_id");
            entity.HasIndex(e => e.CustomerId).HasDatabaseName("idx_carts_customer_id");
            entity.HasIndex(e => e.Status).HasDatabaseName("idx_carts_status");
            entity.HasIndex(e => new { e.CustomerId, e.Status })
                .HasDatabaseName("idx_carts_customer_status");

            // 部分インデックス: ACTIVE カートの期限切れ検出用（設計書 §30）
            entity.HasIndex(e => e.ExpiresAt)
                .HasDatabaseName("idx_carts_expired")
                .HasFilter("status = 'ACTIVE' AND expires_at IS NOT NULL");

            // CHECK 制約（設計書 §30）
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_carts_status",
                "status IN ('ACTIVE', 'EXPIRED', 'ABANDONED', 'CHECKED_OUT')"));
        });

        // ── CartItem ──
        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasIndex(e => new { e.CartId, e.ProductId })
                .IsUnique()
                .HasDatabaseName("idx_cart_items_unique");

            entity.HasIndex(e => e.CartId).HasDatabaseName("idx_cart_items_cart_id");
            entity.HasIndex(e => e.ProductId).HasDatabaseName("idx_cart_items_product_id");

            entity.Property(e => e.UnitPrice).HasColumnType("decimal(12,2)");
            entity.Property(e => e.Subtotal).HasColumnType("decimal(12,2)");

            // CHECK 制約（設計書 §30）
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_cart_items_quantity", "quantity > 0"));
        });

        // ── Payment ──
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasMany(p => p.Transactions)
                .WithOne(t => t.Payment)
                .HasForeignKey(t => t.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.Status)
                .HasConversion<string>();

            entity.Property(e => e.Amount).HasColumnType("decimal(12,2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.OrderId).IsUnique().HasDatabaseName("idx_payments_order_id");
            entity.HasIndex(e => e.CustomerId).HasDatabaseName("idx_payments_customer_id");
            entity.HasIndex(e => e.Status).HasDatabaseName("idx_payments_status");
            entity.HasIndex(e => e.StripePaymentIntentId)
                .HasDatabaseName("idx_payments_stripe_intent_id");
            entity.HasIndex(e => e.StripeCheckoutSessionId)
                .HasDatabaseName("idx_payments_stripe_session_id");

            // CHECK 制約（設計書 §30）
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_payments_amount", "amount >= 0");
                t.HasCheckConstraint("ck_payments_status",
                    "status IN ('PENDING', 'PROCESSING', 'COMPLETED', 'FAILED', 'REFUNDED', 'CANCELLED')");
            });
        });

        // ── Transaction ──
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.Property(e => e.Type).HasConversion<string>();
            entity.Property(e => e.Amount).HasColumnType("decimal(12,2)");

            entity.HasIndex(e => e.PaymentId)
                .HasDatabaseName("idx_transactions_payment_id");

            // CHECK 制約（設計書 §30）
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_transactions_amount", "amount >= 0"));
        });

        // ── PaymentMethod ──
        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            entity.Property(e => e.Type).HasConversion<string>();

            // インデックス（設計書 §30）
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("idx_payment_methods_user_id");
        });

        // ── OutboxEvent ──
        modelBuilder.Entity<OutboxEvent>(entity =>
        {
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // 部分インデックス: 失敗イベントのリトライ検索用（設計書 §30）
            entity.HasIndex(e => new { e.Status, e.RetryCount })
                .HasDatabaseName("idx_outbox_events_failed")
                .HasFilter("status = 'FAILED'");

            // CHECK 制約（設計書 §30）
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_outbox_status",
                    "status IN ('PENDING', 'PROCESSING', 'PUBLISHED', 'FAILED', 'DEAD_LETTER')");
                t.HasCheckConstraint("ck_outbox_retry_count", "retry_count >= 0");
            });
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var entries = ChangeTracker.Entries<IHasTimestamps>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }

            entry.Entity.UpdatedAt = now;
        }

        return await base.SaveChangesAsync(ct);
    }
}
```

### Phase 2 完了チェックリスト

- [ ] `dotnet build PaymentCartService/` — ビルドが成功する
- [ ] 全エンティティに `[Table("snake_case")]` と `[Column("snake_case")]` が設定されている
- [ ] `DateTime.Now` が使用されていない（`DateTime.UtcNow` / `TimeProvider` を使用）
- [ ] コレクションナビゲーションが `= []` で初期化されている
- [ ] Cart に `[Timestamp]` RowVersion が定義されている
- [ ] `dotnet ef migrations add InitialCreate -p PaymentCartService/` — マイグレーション生成が成功する
- [ ] Enum が `HasConversion<string>()` で文字列として DB 保存される
- [ ] 全インデックスが `OnModelCreating` で定義されている
- [ ] `IHasTimestamps` マーカーインターフェースが定義され、Cart, Payment, OutboxEvent に実装されている
- [ ] CHECK 制約（`ck_carts_status`, `ck_cart_items_quantity`, `ck_payments_amount`, `ck_payments_status`, `ck_transactions_amount`, `ck_outbox_status`, `ck_outbox_retry_count`）が全て定義されている
- [ ] 部分インデックス `idx_carts_expired`（WHERE status='ACTIVE'）と `idx_outbox_events_failed`（WHERE status='FAILED'）が定義されている
- [ ] `idx_payment_methods_user_id` インデックスが PaymentMethod に定義されている
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと（`NotImplementedException`, 仮のハードコード値が残存していないこと）
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディや `=> throw new NotImplementedException()` が本番コードに残存していないこと

---

## Phase 3: Repository 層実装

### 目的

Cart Aggregate Root と Payment Aggregate Root の Repository インターフェースおよび EF Core 実装を作成する。DDD の Repository パターンに従い、Aggregate Root 単位でデータアクセスを定義する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `PaymentCartService/Repositories/Interfaces/ICartRepository.cs` | 作成 | カート Repository インターフェース |
| 2 | `PaymentCartService/Repositories/Interfaces/IPaymentRepository.cs` | 作成 | 決済 Repository インターフェース |
| 3 | `PaymentCartService/Repositories/Interfaces/IOutboxEventRepository.cs` | 作成 | Outbox イベント Repository インターフェース |
| 4 | `PaymentCartService/Repositories/CartRepository.cs` | 作成 | カート Repository 実装 |
| 5 | `PaymentCartService/Repositories/PaymentRepository.cs` | 作成 | 決済 Repository 実装 |
| 6 | `PaymentCartService/Repositories/OutboxEventRepository.cs` | 作成 | Outbox イベント Repository 実装 |
| 7 | `PaymentCartService/Program.cs` | 更新 | Repository DI 登録 |

### 3.1 Repository インターフェース

```csharp
// Repositories/Interfaces/ICartRepository.cs
using PaymentCartService.Models;

namespace PaymentCartService.Repositories.Interfaces;

public interface ICartRepository
{
    Task<Cart?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<Cart?> FindByIdWithItemsAsync(string id, CancellationToken ct = default);
    Task<Cart?> FindBySessionIdAsync(string sessionId, CancellationToken ct = default);
    Task<Cart?> FindActiveByCustomerIdAsync(string customerId, CancellationToken ct = default);
    Task AddAsync(Cart cart, CancellationToken ct = default);
    Task<int> ExpireCartsAsync(DateTime cutoffDate, int batchSize, CancellationToken ct = default);
    Task<int> CleanupExpiredCartsAsync(DateTime cutoffDate, int batchSize, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

```csharp
// Repositories/Interfaces/IPaymentRepository.cs
using PaymentCartService.Models;

namespace PaymentCartService.Repositories.Interfaces;

public interface IPaymentRepository
{
    Task<Payment?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<Payment?> FindByOrderIdAsync(string orderId, CancellationToken ct = default);
    Task<Payment?> FindByStripeCheckoutSessionIdAsync(string sessionId, CancellationToken ct = default);
    Task<Payment?> FindByStripePaymentIntentIdAsync(string intentId, CancellationToken ct = default);
    Task<(List<Payment> Items, int TotalCount)> FindByCustomerIdAsync(
        string customerId, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Payment payment, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

```csharp
// Repositories/Interfaces/IOutboxEventRepository.cs
using PaymentCartService.Models;

namespace PaymentCartService.Repositories.Interfaces;

public interface IOutboxEventRepository
{
    Task<List<OutboxEvent>> FindPendingEventsAsync(int batchSize, CancellationToken ct = default);
    Task AddAsync(OutboxEvent outboxEvent, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

### 3.2 Repository 実装

```csharp
// Repositories/CartRepository.cs
using Microsoft.EntityFrameworkCore;
using PaymentCartService.Infrastructure.Persistence;
using PaymentCartService.Models;
using PaymentCartService.Models.Enums;
using PaymentCartService.Repositories.Interfaces;

namespace PaymentCartService.Repositories;

public class CartRepository(AppDbContext context) : ICartRepository
{
    public async Task<Cart?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.Carts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Cart?> FindByIdWithItemsAsync(string id, CancellationToken ct = default)
        => await context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Cart?> FindBySessionIdAsync(string sessionId, CancellationToken ct = default)
        => await context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.SessionId == sessionId
                && c.Status == CartStatus.Active, ct);

    public async Task<Cart?> FindActiveByCustomerIdAsync(string customerId, CancellationToken ct = default)
        => await context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId
                && c.Status == CartStatus.Active, ct);

    public async Task AddAsync(Cart cart, CancellationToken ct = default)
        => await context.Carts.AddAsync(cart, ct);

    public async Task<int> ExpireCartsAsync(DateTime cutoffDate, int batchSize, CancellationToken ct = default)
        => await context.Carts
            .Where(c => c.Status == CartStatus.Active && c.ExpiresAt < cutoffDate)
            .Take(batchSize)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, CartStatus.Expired), ct);

    public async Task<int> CleanupExpiredCartsAsync(DateTime cutoffDate, int batchSize, CancellationToken ct = default)
        => await context.Carts
            .Where(c => (c.Status == CartStatus.Expired || c.Status == CartStatus.Abandoned)
                && c.UpdatedAt < cutoffDate)
            .Take(batchSize)
            .ExecuteDeleteAsync(ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
```

```csharp
// Repositories/PaymentRepository.cs
using Microsoft.EntityFrameworkCore;
using PaymentCartService.Infrastructure.Persistence;
using PaymentCartService.Models;
using PaymentCartService.Repositories.Interfaces;

namespace PaymentCartService.Repositories;

public class PaymentRepository(AppDbContext context) : IPaymentRepository
{
    public async Task<Payment?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.Payments
            .Include(p => p.Transactions)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Payment?> FindByOrderIdAsync(string orderId, CancellationToken ct = default)
        => await context.Payments
            .Include(p => p.Transactions)
            .FirstOrDefaultAsync(p => p.OrderId == orderId, ct);

    public async Task<Payment?> FindByStripeCheckoutSessionIdAsync(string sessionId, CancellationToken ct = default)
        => await context.Payments
            .FirstOrDefaultAsync(p => p.StripeCheckoutSessionId == sessionId, ct);

    public async Task<Payment?> FindByStripePaymentIntentIdAsync(string intentId, CancellationToken ct = default)
        => await context.Payments
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == intentId, ct);

    public async Task<(List<Payment> Items, int TotalCount)> FindByCustomerIdAsync(
        string customerId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.Payments
            .AsNoTracking()
            .Where(p => p.CustomerId == customerId)
            .OrderByDescending(p => p.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(Payment payment, CancellationToken ct = default)
        => await context.Payments.AddAsync(payment, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
```

```csharp
// Repositories/OutboxEventRepository.cs
using Microsoft.EntityFrameworkCore;
using PaymentCartService.Infrastructure.Persistence;
using PaymentCartService.Models;
using PaymentCartService.Models.Enums;
using PaymentCartService.Repositories.Interfaces;

namespace PaymentCartService.Repositories;

public class OutboxEventRepository(AppDbContext context) : IOutboxEventRepository
{
    public async Task<List<OutboxEvent>> FindPendingEventsAsync(int batchSize, CancellationToken ct = default)
        => await context.OutboxEvents
            .Where(e => e.Status == OutboxEventStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);

    public async Task AddAsync(OutboxEvent outboxEvent, CancellationToken ct = default)
        => await context.OutboxEvents.AddAsync(outboxEvent, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
```

### 3.3 Program.cs 更新（DI 登録）

```csharp
// Program.cs に追加
builder.Services.AddScoped<ICartRepository, CartRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IOutboxEventRepository, OutboxEventRepository>();
```

### Phase 3 完了チェックリスト

- [ ] `dotnet build PaymentCartService/` — ビルドが成功する
- [ ] Repository インターフェースが Aggregate Root 単位で分離されている
- [ ] 読み取りクエリに `AsNoTracking()` が適用されている
- [ ] 全 async メソッドに `CancellationToken ct = default` が含まれている
- [ ] primary constructor による DI が使用されている
- [ ] `FindByIdWithItemsAsync` で `Include(c => c.Items)` による Eager Loading が実装されている
- [ ] ページネーション対応の `FindByCustomerIdAsync` が実装されている
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと（`NotImplementedException`, 仮のハードコード値が残存していないこと）
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディや `=> throw new NotImplementedException()` が本番コードに残存していないこと

---

## Phase 4: Service 層実装

### 目的

カート管理（追加・更新・削除・マージ）と決済処理（チェックアウト・Webhook 処理・返金）のビジネスロジックを Service 層に実装する。FluentValidation によるバリデーション、カスタム例外クラス、DTO 定義を含む。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `PaymentCartService/Exceptions/NotFoundException.cs` | 作成 | 404 例外 |
| 2 | `PaymentCartService/Exceptions/BusinessException.cs` | 作成 | 422 例外 |
| 3 | `PaymentCartService/Exceptions/ConcurrencyException.cs` | 作成 | 409 例外 |
| 4 | `PaymentCartService/DTOs/Requests/AddCartItemRequest.cs` | 作成 | カートアイテム追加リクエスト |
| 5 | `PaymentCartService/DTOs/Requests/UpdateCartItemRequest.cs` | 作成 | カートアイテム更新リクエスト |
| 6 | `PaymentCartService/DTOs/Requests/MergeCartRequest.cs` | 作成 | カートマージリクエスト |
| 7 | `PaymentCartService/DTOs/Requests/CheckoutRequest.cs` | 作成 | チェックアウトリクエスト |
| 8 | `PaymentCartService/DTOs/Requests/RefundRequest.cs` | 作成 | 返金リクエスト |
| 9 | `PaymentCartService/DTOs/Responses/CartResponse.cs` | 作成 | カートレスポンス |
| 10 | `PaymentCartService/DTOs/Responses/CartItemResponse.cs` | 作成 | カートアイテムレスポンス |
| 11 | `PaymentCartService/DTOs/Responses/PaymentResponse.cs` | 作成 | 決済レスポンス |
| 12 | `PaymentCartService/DTOs/Responses/PaymentDetailResponse.cs` | 作成 | 決済詳細レスポンス |
| 13 | `PaymentCartService/DTOs/Responses/RefundResponse.cs` | 作成 | 返金レスポンス |
| 14 | `PaymentCartService/DTOs/Responses/PaginatedResponse.cs` | 作成 | ページネーションレスポンス |
| 15 | `PaymentCartService/Validators/AddCartItemRequestValidator.cs` | 作成 | バリデーター |
| 16 | `PaymentCartService/Validators/UpdateCartItemRequestValidator.cs` | 作成 | バリデーター |
| 17 | `PaymentCartService/Validators/CheckoutRequestValidator.cs` | 作成 | バリデーター |
| 18 | `PaymentCartService/Validators/MergeCartRequestValidator.cs` | 作成 | バリデーター |
| 19 | `PaymentCartService/Validators/RefundRequestValidator.cs` | 作成 | バリデーター |
| 20 | `PaymentCartService/Services/Interfaces/ICartService.cs` | 作成 | カート Service インターフェース |
| 21 | `PaymentCartService/Services/Interfaces/IPaymentService.cs` | 作成 | 決済 Service インターフェース |
| 22 | `PaymentCartService/Services/Interfaces/IRefundService.cs` | 作成 | 返金 Service インターフェース |
| 23 | `PaymentCartService/Services/CartService.cs` | 作成 | カート Service 実装 |
| 24 | `PaymentCartService/Services/PaymentService.cs` | 作成 | 決済 Service 実装 |
| 25 | `PaymentCartService/Services/RefundService.cs` | 作成 | 返金 Service 実装 |
| 26 | `PaymentCartService/Exceptions/CartExpiredException.cs` | 作成 | カート期限切れ例外（→ HTTP 409） |
| 27 | `PaymentCartService/Exceptions/PaymentProcessingException.cs` | 作成 | 決済処理例外（→ HTTP 422） |
| 28 | `PaymentCartService/Exceptions/RefundProcessingException.cs` | 作成 | 返金処理例外（→ HTTP 422） |
| 29 | `PaymentCartService/Exceptions/StripeApiException.cs` | 作成 | Stripe API 例外（→ HTTP 422） |
| 30 | `PaymentCartService/Exceptions/ExternalServiceException.cs` | 作成 | 外部サービス例外（→ HTTP 503） |
| 31 | `PaymentCartService/Services/Interfaces/IPriceService.cs` | 作成 | 価格計算 Service インターフェース |
| 32 | `PaymentCartService/Services/Interfaces/ITaxCalculator.cs` | 作成 | 税計算インターフェース |
| 33 | `PaymentCartService/Services/Interfaces/IShippingFeeCalculator.cs` | 作成 | 送料計算インターフェース |
| 34 | `PaymentCartService/Services/PriceService.cs` | 作成 | 価格計算 Service 実装 |
| 35 | `PaymentCartService/Program.cs` | 更新 | Service DI 登録 |

### 4.1 例外クラス

```csharp
// Exceptions/NotFoundException.cs
namespace PaymentCartService.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
    public NotFoundException(string message, Exception innerException) : base(message, innerException) { }
}
```

```csharp
// Exceptions/BusinessException.cs
namespace PaymentCartService.Exceptions;

public class BusinessException : Exception
{
    public string? ErrorCode { get; }
    public BusinessException(string message, string? errorCode = null) : base(message)
        => ErrorCode = errorCode;
    public BusinessException(string message, Exception innerException, string? errorCode = null)
        : base(message, innerException) => ErrorCode = errorCode;
}
```

```csharp
// Exceptions/ConcurrencyException.cs
namespace PaymentCartService.Exceptions;

public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message) { }
    public ConcurrencyException(string message, Exception innerException) : base(message, innerException) { }
}
```

```csharp
// Exceptions/CartExpiredException.cs — 設計書 §7（→ HTTP 409）
namespace PaymentCartService.Exceptions;

public class CartExpiredException : Exception
{
    public string? ErrorCode { get; }
    public CartExpiredException(string message, string? errorCode = "CART-4091") : base(message)
        => ErrorCode = errorCode;
}
```

```csharp
// Exceptions/PaymentProcessingException.cs — 設計書 §7（→ HTTP 422）
namespace PaymentCartService.Exceptions;

public class PaymentProcessingException : Exception
{
    public string? ErrorCode { get; }
    public PaymentProcessingException(string message, string? errorCode = "PAY-4223") : base(message)
        => ErrorCode = errorCode;
    public PaymentProcessingException(string message, Exception innerException, string? errorCode = "PAY-4223")
        : base(message, innerException) => ErrorCode = errorCode;
}
```

```csharp
// Exceptions/RefundProcessingException.cs — 設計書 §7（→ HTTP 422）
namespace PaymentCartService.Exceptions;

public class RefundProcessingException : Exception
{
    public string? ErrorCode { get; }
    public RefundProcessingException(string message, string? errorCode = "PAY-4222") : base(message)
        => ErrorCode = errorCode;
    public RefundProcessingException(string message, Exception innerException, string? errorCode = "PAY-4222")
        : base(message, innerException) => ErrorCode = errorCode;
}
```

```csharp
// Exceptions/StripeApiException.cs — 設計書 §7（→ HTTP 422）
namespace PaymentCartService.Exceptions;

public class StripeApiException : Exception
{
    public string? ErrorCode { get; }
    public StripeApiException(string message, string? errorCode = "PAY-5002") : base(message)
        => ErrorCode = errorCode;
    public StripeApiException(string message, Exception innerException, string? errorCode = "PAY-5002")
        : base(message, innerException) => ErrorCode = errorCode;
}
```

```csharp
// Exceptions/ExternalServiceException.cs — 設計書 §7（→ HTTP 503）
namespace PaymentCartService.Exceptions;

public class ExternalServiceException : Exception
{
    public string? ErrorCode { get; }
    public ExternalServiceException(string message, string? errorCode = "PAY-5001") : base(message)
        => ErrorCode = errorCode;
    public ExternalServiceException(string message, Exception innerException, string? errorCode = "PAY-5001")
        : base(message, innerException) => ErrorCode = errorCode;
}
```

### 4.2 価格計算 Service インターフェース（設計書 §25）

```csharp
// Services/Interfaces/IPriceService.cs
namespace PaymentCartService.Services.Interfaces;

public interface IPriceService
{
    Task<decimal> CalculateSubtotalAsync(string productId, int quantity, CancellationToken ct = default);
    Task<decimal> CalculateTotalAsync(string cartId, CancellationToken ct = default);
}
```

```csharp
// Services/Interfaces/ITaxCalculator.cs
namespace PaymentCartService.Services.Interfaces;

public interface ITaxCalculator
{
    Task<decimal> CalculateTaxAsync(decimal subtotal, string? region = null, CancellationToken ct = default);
}
```

```csharp
// Services/Interfaces/IShippingFeeCalculator.cs
namespace PaymentCartService.Services.Interfaces;

public interface IShippingFeeCalculator
{
    Task<decimal> CalculateShippingFeeAsync(
        decimal subtotal, string? shippingAddress = null, CancellationToken ct = default);
}
```

> **注記**: `IPriceService` は商品価格を InventoryManagementService から取得する。カートアイテム追加時にクライアントから価格を受け取らず、サーバーサイドで価格を確定する（設計書 §23 準拠）。`ITaxCalculator` と `IShippingFeeCalculator` はチェックアウト時の合計金額計算に使用する。

### 4.3 リクエスト DTO

```csharp
// DTOs/Requests/AddCartItemRequest.cs
using System.ComponentModel.DataAnnotations;

namespace PaymentCartService.DTOs.Requests;

public record AddCartItemRequest(
    [Required(ErrorMessage = "商品 ID は必須です")]
    [StringLength(100)]
    string ProductId,

    [Required]
    [Range(1, 10, ErrorMessage = "数量は 1〜10 の範囲で指定してください")]
    int Quantity);
```

```csharp
// DTOs/Requests/UpdateCartItemRequest.cs
using System.ComponentModel.DataAnnotations;

namespace PaymentCartService.DTOs.Requests;

public record UpdateCartItemRequest(
    [Required]
    [Range(1, 10, ErrorMessage = "数量は 1〜10 の範囲で指定してください")]
    int Quantity);
```

```csharp
// DTOs/Requests/MergeCartRequest.cs
using System.ComponentModel.DataAnnotations;

namespace PaymentCartService.DTOs.Requests;

public record MergeCartRequest(
    [Required(ErrorMessage = "ゲストカート ID は必須です")]
    [StringLength(36)]
    string GuestCartId);
```

```csharp
// DTOs/Requests/CheckoutRequest.cs
using System.ComponentModel.DataAnnotations;
using PaymentCartService.Models.ValueObjects;

namespace PaymentCartService.DTOs.Requests;

public record CheckoutRequest(
    [Required(ErrorMessage = "カート ID は必須です")]
    [StringLength(36)]
    string CartId,

    [Required(ErrorMessage = "決済方法は必須です")]
    [StringLength(50)]
    string PaymentMethod,

    ShippingAddress? ShippingAddress,

    [StringLength(50)]
    string? CouponCode,

    [Range(0, int.MaxValue)]
    int? UsedPoints);
```

```csharp
// DTOs/Requests/RefundRequest.cs
using System.ComponentModel.DataAnnotations;

namespace PaymentCartService.DTOs.Requests;

public record RefundRequest(
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "返金金額は 0 より大きい値を指定してください")]
    decimal Amount,

    [Required(ErrorMessage = "返金理由は必須です")]
    [StringLength(500)]
    string Reason);
```

### 4.4 レスポンス DTO

```csharp
// DTOs/Responses/CartResponse.cs
namespace PaymentCartService.DTOs.Responses;

public record CartResponse(
    string Id,
    string? CustomerId,
    string SessionId,
    string Status,
    List<CartItemResponse> Items,
    int TotalItems,
    decimal TotalAmount,
    DateTime ExpiresAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
```

```csharp
// DTOs/Responses/CartItemResponse.cs
namespace PaymentCartService.DTOs.Responses;

public record CartItemResponse(
    string Id,
    string ProductId,
    string ProductName,
    string Sku,
    decimal UnitPrice,
    int Quantity,
    decimal Subtotal,
    DateTime AddedAt);
```

```csharp
// DTOs/Responses/PaymentResponse.cs
namespace PaymentCartService.DTOs.Responses;

public record PaymentResponse(
    string Id,
    string OrderId,
    string CustomerId,
    decimal Amount,
    string CurrencyCode,
    string Status,
    string PaymentMethod,
    string? CheckoutUrl,
    DateTime CreatedAt,
    DateTime UpdatedAt);
```

```csharp
// DTOs/Responses/PaymentDetailResponse.cs
namespace PaymentCartService.DTOs.Responses;

public record PaymentDetailResponse(
    string Id,
    string OrderId,
    string CustomerId,
    decimal Amount,
    string CurrencyCode,
    string Status,
    string PaymentMethod,
    string? StripeCheckoutSessionId,
    string? StripePaymentIntentId,
    DateTime? PaidAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
```

```csharp
// DTOs/Responses/RefundResponse.cs
namespace PaymentCartService.DTOs.Responses;

public record RefundResponse(
    string Id,
    string PaymentId,
    decimal RefundAmount,
    string Status,
    DateTime CreatedAt);
```

```csharp
// DTOs/Responses/PaginatedResponse.cs
namespace PaymentCartService.DTOs.Responses;

public record PaginatedResponse<T>(
    List<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
```

### 4.5 FluentValidation バリデーター

```csharp
// Validators/AddCartItemRequestValidator.cs
using FluentValidation;
using PaymentCartService.DTOs.Requests;

namespace PaymentCartService.Validators;

public class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("商品 ID は必須です")
            .MaximumLength(100);

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("数量は 1 以上を指定してください")
            .LessThanOrEqualTo(10).WithMessage("数量は 10 以下を指定してください");
    }
}
```

```csharp
// Validators/CheckoutRequestValidator.cs
using FluentValidation;
using PaymentCartService.DTOs.Requests;

namespace PaymentCartService.Validators;

public class CheckoutRequestValidator : AbstractValidator<CheckoutRequest>
{
    private static readonly string[] ValidPaymentMethods =
        ["CREDIT_CARD", "CONVENIENCE_STORE", "BANK_TRANSFER"];

    public CheckoutRequestValidator()
    {
        RuleFor(x => x.CartId)
            .NotEmpty().WithMessage("カート ID は必須です")
            .MaximumLength(36);

        RuleFor(x => x.PaymentMethod)
            .NotEmpty().WithMessage("決済方法は必須です")
            .Must(m => ValidPaymentMethods.Contains(m))
            .WithMessage("サポートされていない決済方法です");

        RuleFor(x => x.UsedPoints)
            .GreaterThanOrEqualTo(0)
            .When(x => x.UsedPoints.HasValue)
            .WithMessage("使用ポイントは 0 以上を指定してください");

        When(x => x.ShippingAddress is not null, () =>
        {
            RuleFor(x => x.ShippingAddress!.RecipientName)
                .NotEmpty().WithMessage("受取人名は必須です")
                .MaximumLength(100);

            RuleFor(x => x.ShippingAddress!.PostalCode)
                .NotEmpty().WithMessage("郵便番号は必須です")
                .Matches(@"^\d{3}-?\d{4}$").WithMessage("郵便番号の形式が不正です");

            RuleFor(x => x.ShippingAddress!.PhoneNumber)
                .NotEmpty().WithMessage("電話番号は必須です")
                .Matches(@"^[\d\-]+$").WithMessage("電話番号の形式が不正です");
        });
    }
}
```

```csharp
// Validators/RefundRequestValidator.cs
using FluentValidation;
using PaymentCartService.DTOs.Requests;

namespace PaymentCartService.Validators;

public class RefundRequestValidator : AbstractValidator<RefundRequest>
{
    public RefundRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("返金金額は 0 より大きい値を指定してください");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("返金理由は必須です")
            .MaximumLength(500);
    }
}
```

### 4.6 Service インターフェース

```csharp
// Services/Interfaces/ICartService.cs
using PaymentCartService.DTOs.Requests;
using PaymentCartService.DTOs.Responses;

namespace PaymentCartService.Services.Interfaces;

public interface ICartService
{
    Task<CartResponse> GetCartAsync(string cartId, CancellationToken ct = default);
    Task<CartResponse> GetOrCreateCartAsync(string? cartId, string sessionId, CancellationToken ct = default);
    Task<CartResponse> AddItemAsync(string cartId, AddCartItemRequest request, CancellationToken ct = default);
    Task<CartResponse> UpdateItemQuantityAsync(
        string cartId, string itemId, UpdateCartItemRequest request, CancellationToken ct = default);
    Task<CartResponse> RemoveItemAsync(string cartId, string itemId, CancellationToken ct = default);
    Task ClearCartAsync(string cartId, CancellationToken ct = default);
    Task<CartResponse> MergeCartAsync(string guestCartId, string userId, CancellationToken ct = default);
}
```

```csharp
// Services/Interfaces/IPaymentService.cs
using PaymentCartService.DTOs.Requests;
using PaymentCartService.DTOs.Responses;

namespace PaymentCartService.Services.Interfaces;

public interface IPaymentService
{
    Task<PaymentResponse> CheckoutAsync(
        CheckoutRequest request, string userId, CancellationToken ct = default);
    Task<PaymentDetailResponse?> GetByIdAsync(
        string paymentId, string userId, CancellationToken ct = default);
    Task<PaymentDetailResponse?> GetByOrderIdAsync(
        string orderId, string userId, CancellationToken ct = default);
    Task<PaginatedResponse<PaymentResponse>> GetByCustomerIdAsync(
        string customerId, string userId, int page, int pageSize, CancellationToken ct = default);
    Task HandleWebhookAsync(string json, string signature, CancellationToken ct = default);
    Task CompensatePaymentAsync(string paymentId, CancellationToken ct = default);
}
```

```csharp
// Services/Interfaces/IRefundService.cs
using PaymentCartService.DTOs.Requests;
using PaymentCartService.DTOs.Responses;

namespace PaymentCartService.Services.Interfaces;

public interface IRefundService
{
    Task<RefundResponse> RefundAsync(
        string paymentId, RefundRequest request, string adminUserId, CancellationToken ct = default);
}
```

### 4.7 CartService 実装

```csharp
// Services/CartService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentCartService.Configurations;
using PaymentCartService.DTOs.Requests;
using PaymentCartService.DTOs.Responses;
using PaymentCartService.Exceptions;
using PaymentCartService.Models;
using PaymentCartService.Models.Enums;
using PaymentCartService.Repositories.Interfaces;
using PaymentCartService.Services.Interfaces;

namespace PaymentCartService.Services;

public class CartService(
    ICartRepository cartRepository,
    IOptions<CartSettings> cartOptions,
    TimeProvider timeProvider,
    ILogger<CartService> logger) : ICartService
{
    private readonly CartSettings _settings = cartOptions.Value;

    public async Task<CartResponse> GetCartAsync(string cartId, CancellationToken ct = default)
    {
        var cart = await cartRepository.FindByIdWithItemsAsync(cartId, ct)
            ?? throw new NotFoundException($"カートが見つかりません: {cartId}");

        if (cart.Status != CartStatus.Active)
            throw new BusinessException("カートは有効ではありません", "CART-4091");

        return MapToResponse(cart);
    }

    public async Task<CartResponse> GetOrCreateCartAsync(
        string? cartId, string sessionId, CancellationToken ct = default)
    {
        if (cartId is not null)
        {
            var existing = await cartRepository.FindByIdWithItemsAsync(cartId, ct);
            if (existing is not null && existing.Status == CartStatus.Active)
                return MapToResponse(existing);
        }

        var sessionCart = await cartRepository.FindBySessionIdAsync(sessionId, ct);
        if (sessionCart is not null)
            return MapToResponse(sessionCart);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var newCart = new Cart
        {
            SessionId = sessionId,
            ExpiresAt = now.AddDays(_settings.ExpiryDays),
            CreatedAt = now,
            UpdatedAt = now
        };

        await cartRepository.AddAsync(newCart, ct);
        await cartRepository.SaveChangesAsync(ct);

        logger.LogInformation("カート作成: CartId={CartId}, SessionId={SessionId}", newCart.Id, sessionId);
        return MapToResponse(newCart);
    }

    public async Task<CartResponse> AddItemAsync(
        string cartId, AddCartItemRequest request, CancellationToken ct = default)
    {
        var cart = await cartRepository.FindByIdWithItemsAsync(cartId, ct)
            ?? throw new NotFoundException($"カートが見つかりません: {cartId}");

        if (cart.Status != CartStatus.Active)
            throw new BusinessException("カートは有効ではありません", "CART-4091");

        if (cart.Items.Count >= _settings.MaxItemsPerCart)
            throw new BusinessException(
                $"カートのアイテム数上限（{_settings.MaxItemsPerCart}）に達しています", "CART-4002");

        // TODO: InventoryManagementService から商品情報を取得（価格操作リスク防止）
        // var product = await inventoryClient.GetProductAsync(request.ProductId, ct);
        // cart.AddItem(product.Id, product.Name, product.Sku, product.Price, request.Quantity);

        // 暫定実装: 商品情報はプレースホルダー
        cart.AddItem(request.ProductId, "商品名（要 API 連携）", "SKU-TEMP", 0m, request.Quantity);

        try
        {
            await cartRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "楽観的ロック競合: CartId={CartId}", cartId);
            throw new ConcurrencyException("カートが他のユーザーによって更新されました。再度お試しください。");
        }

        logger.LogInformation("カートアイテム追加: CartId={CartId}, ProductId={ProductId}, Quantity={Quantity}",
            cartId, request.ProductId, request.Quantity);

        return MapToResponse(cart);
    }

    public async Task<CartResponse> UpdateItemQuantityAsync(
        string cartId, string itemId, UpdateCartItemRequest request, CancellationToken ct = default)
    {
        var cart = await cartRepository.FindByIdWithItemsAsync(cartId, ct)
            ?? throw new NotFoundException($"カートが見つかりません: {cartId}");

        if (cart.Status != CartStatus.Active)
            throw new BusinessException("カートは有効ではありません", "CART-4091");

        cart.UpdateItemQuantity(itemId, request.Quantity);

        try
        {
            await cartRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "楽観的ロック競合: CartId={CartId}", cartId);
            throw new ConcurrencyException("カートが他のユーザーによって更新されました。再度お試しください。");
        }

        logger.LogInformation("カートアイテム更新: CartId={CartId}, ItemId={ItemId}, Quantity={Quantity}",
            cartId, itemId, request.Quantity);

        return MapToResponse(cart);
    }

    public async Task<CartResponse> RemoveItemAsync(
        string cartId, string itemId, CancellationToken ct = default)
    {
        var cart = await cartRepository.FindByIdWithItemsAsync(cartId, ct)
            ?? throw new NotFoundException($"カートが見つかりません: {cartId}");

        cart.RemoveItem(itemId);
        await cartRepository.SaveChangesAsync(ct);

        logger.LogInformation("カートアイテム削除: CartId={CartId}, ItemId={ItemId}", cartId, itemId);
        return MapToResponse(cart);
    }

    public async Task ClearCartAsync(string cartId, CancellationToken ct = default)
    {
        var cart = await cartRepository.FindByIdWithItemsAsync(cartId, ct)
            ?? throw new NotFoundException($"カートが見つかりません: {cartId}");

        cart.ClearItems();
        await cartRepository.SaveChangesAsync(ct);

        logger.LogInformation("カート全削除: CartId={CartId}", cartId);
    }

    public async Task<CartResponse> MergeCartAsync(
        string guestCartId, string userId, CancellationToken ct = default)
    {
        var guestCart = await cartRepository.FindByIdWithItemsAsync(guestCartId, ct)
            ?? throw new NotFoundException($"ゲストカートが見つかりません: {guestCartId}");

        var userCart = await cartRepository.FindActiveByCustomerIdAsync(userId, ct);

        if (userCart is null)
        {
            guestCart.CustomerId = userId;
            await cartRepository.SaveChangesAsync(ct);
            logger.LogInformation("ゲストカートをユーザーに紐付け: CartId={CartId}, UserId={UserId}",
                guestCartId, userId);
            return MapToResponse(guestCart);
        }

        foreach (var guestItem in guestCart.Items)
        {
            var existingItem = userCart.Items.FirstOrDefault(i => i.ProductId == guestItem.ProductId);
            if (existingItem is not null)
            {
                existingItem.UpdateQuantity(existingItem.Quantity + guestItem.Quantity);
            }
            else
            {
                userCart.AddItem(
                    guestItem.ProductId, guestItem.ProductName,
                    guestItem.Sku, guestItem.UnitPrice, guestItem.Quantity);
            }
        }

        guestCart.MarkAsAbandoned();
        await cartRepository.SaveChangesAsync(ct);

        logger.LogInformation("カートマージ完了: GuestCartId={GuestCartId}, UserCartId={UserCartId}, UserId={UserId}",
            guestCartId, userCart.Id, userId);

        return MapToResponse(userCart);
    }

    private static CartResponse MapToResponse(Cart cart) => new(
        Id: cart.Id,
        CustomerId: cart.CustomerId,
        SessionId: cart.SessionId,
        Status: cart.Status.ToString().ToUpperInvariant(),
        Items: cart.Items.Select(i => new CartItemResponse(
            Id: i.Id,
            ProductId: i.ProductId,
            ProductName: i.ProductName,
            Sku: i.Sku,
            UnitPrice: i.UnitPrice,
            Quantity: i.Quantity,
            Subtotal: i.Subtotal,
            AddedAt: i.AddedAt
        )).ToList(),
        TotalItems: cart.Items.Sum(i => i.Quantity),
        TotalAmount: cart.CalculateTotal(),
        ExpiresAt: cart.ExpiresAt,
        CreatedAt: cart.CreatedAt,
        UpdatedAt: cart.UpdatedAt
    );
}
```

### 4.8 PaymentService 実装

```csharp
// Services/PaymentService.cs
using System.Text.Json;
using Microsoft.Extensions.Options;
using PaymentCartService.Configurations;
using PaymentCartService.DTOs.Requests;
using PaymentCartService.DTOs.Responses;
using PaymentCartService.Exceptions;
using PaymentCartService.Infrastructure.Persistence;
using PaymentCartService.Models;
using PaymentCartService.Models.Enums;
using PaymentCartService.Repositories.Interfaces;
using PaymentCartService.Services.Interfaces;
using Stripe;
using Stripe.Checkout;

namespace PaymentCartService.Services;

public class PaymentService(
    IPaymentRepository paymentRepository,
    ICartRepository cartRepository,
    IOutboxEventRepository outboxEventRepository,
    AppDbContext dbContext,
    IOptions<StripeSettings> stripeOptions,
    IOptions<PaymentSettings> paymentOptions,
    TimeProvider timeProvider,
    ILogger<PaymentService> logger) : IPaymentService
{
    private readonly StripeSettings _stripeSettings = stripeOptions.Value;
    private readonly PaymentSettings _paymentSettings = paymentOptions.Value;

    public async Task<PaymentResponse> CheckoutAsync(
        CheckoutRequest request, string userId, CancellationToken ct = default)
    {
        var cart = await cartRepository.FindByIdWithItemsAsync(request.CartId, ct)
            ?? throw new NotFoundException("カートが見つかりません");

        if (cart.Status != CartStatus.Active || !cart.Items.Any())
            throw new BusinessException("カートが空または無効です", "PAY-4001");

        var totalAmount = cart.CalculateTotal();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            var payment = new Payment
            {
                OrderId = Guid.NewGuid().ToString(),
                CustomerId = userId,
                Amount = totalAmount,
                CurrencyCode = _paymentSettings.DefaultCurrency,
                Status = PaymentStatus.Pending,
                PaymentMethod = request.PaymentMethod,
                CreatedBy = userId
            };

            await paymentRepository.AddAsync(payment, ct);

            // Stripe Checkout Session 作成
            var sessionOptions = new SessionCreateOptions
            {
                PaymentMethodTypes = ["card"],
                LineItems = cart.Items.Select(item => new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = _paymentSettings.DefaultCurrency.ToLowerInvariant(),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.ProductName,
                        },
                        UnitAmount = (long)(item.UnitPrice * 100),
                    },
                    Quantity = item.Quantity,
                }).ToList(),
                Mode = "payment",
                SuccessUrl = $"{_stripeSettings.SuccessUrl}?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{_stripeSettings.CancelUrl}?session_id={{CHECKOUT_SESSION_ID}}",
                ClientReferenceId = payment.Id,
            };

            var service = new SessionService();
            var session = await service.CreateAsync(sessionOptions, cancellationToken: ct);

            payment.StripeCheckoutSessionId = session.Id;
            await paymentRepository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            logger.LogInformation(
                "チェックアウトセッション作成: PaymentId={PaymentId}, StripeSessionId={StripeSessionId}",
                payment.Id, session.Id);

            return new PaymentResponse(
                Id: payment.Id,
                OrderId: payment.OrderId,
                CustomerId: payment.CustomerId,
                Amount: payment.Amount,
                CurrencyCode: payment.CurrencyCode,
                Status: payment.Status.ToString().ToUpperInvariant(),
                PaymentMethod: payment.PaymentMethod,
                CheckoutUrl: session.Url,
                CreatedAt: payment.CreatedAt,
                UpdatedAt: payment.UpdatedAt);
        }
        catch (StripeException ex)
        {
            await transaction.RollbackAsync(ct);
            logger.LogError(ex, "Stripe API エラー: {Message}", ex.Message);
            throw new BusinessException("決済処理に失敗しました", ex, "PAY-4223");
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<PaymentDetailResponse?> GetByIdAsync(
        string paymentId, string userId, CancellationToken ct = default)
    {
        var payment = await paymentRepository.FindByIdAsync(paymentId, ct);
        if (payment is null) return null;

        if (payment.CustomerId != userId)
            throw new BusinessException("アクセス権限がありません", "PAY-4041");

        return MapToDetailResponse(payment);
    }

    public async Task<PaymentDetailResponse?> GetByOrderIdAsync(
        string orderId, string userId, CancellationToken ct = default)
    {
        var payment = await paymentRepository.FindByOrderIdAsync(orderId, ct);
        if (payment is null) return null;

        if (payment.CustomerId != userId)
            throw new BusinessException("アクセス権限がありません", "PAY-4041");

        return MapToDetailResponse(payment);
    }

    public async Task<PaginatedResponse<PaymentResponse>> GetByCustomerIdAsync(
        string customerId, string userId, int page, int pageSize, CancellationToken ct = default)
    {
        if (customerId != userId)
            throw new BusinessException("アクセス権限がありません", "PAY-4041");

        var (items, totalCount) = await paymentRepository.FindByCustomerIdAsync(
            customerId, page, pageSize, ct);

        var responses = items.Select(p => new PaymentResponse(
            Id: p.Id, OrderId: p.OrderId, CustomerId: p.CustomerId,
            Amount: p.Amount, CurrencyCode: p.CurrencyCode,
            Status: p.Status.ToString().ToUpperInvariant(),
            PaymentMethod: p.PaymentMethod, CheckoutUrl: null,
            CreatedAt: p.CreatedAt, UpdatedAt: p.UpdatedAt
        )).ToList();

        return new PaginatedResponse<PaymentResponse>(
            Items: responses, Page: page, PageSize: pageSize,
            TotalCount: totalCount, TotalPages: (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task HandleWebhookAsync(string json, string signature, CancellationToken ct = default)
    {
        var stripeEvent = EventUtility.ConstructEvent(
            json, signature, _stripeSettings.WebhookSecret,
            toleranceInSeconds: _paymentSettings.WebhookToleranceSeconds);

        logger.LogInformation("Stripe Webhook 受信: EventType={EventType}, EventId={EventId}",
            stripeEvent.Type, stripeEvent.Id);

        switch (stripeEvent.Type)
        {
            case EventTypes.CheckoutSessionCompleted:
                await HandleCheckoutSessionCompletedAsync(stripeEvent, ct);
                break;
            case EventTypes.PaymentIntentPaymentFailed:
                await HandlePaymentFailedAsync(stripeEvent, ct);
                break;
            default:
                logger.LogInformation("未処理の Webhook イベント: {EventType}", stripeEvent.Type);
                break;
        }
    }

    public async Task CompensatePaymentAsync(string paymentId, CancellationToken ct = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

        var payment = await paymentRepository.FindByIdAsync(paymentId, timeoutCts.Token)
            ?? throw new NotFoundException($"決済が見つかりません: {paymentId}");

        if (payment.Status is PaymentStatus.Refunded or PaymentStatus.Cancelled)
        {
            logger.LogInformation("Saga 補償スキップ（既に返金済み）: PaymentId={PaymentId}", paymentId);
            return;
        }

        if (payment.StripePaymentIntentId is not null)
        {
            var refundService = new Stripe.RefundService();
            await refundService.CreateAsync(new RefundCreateOptions
            {
                PaymentIntent = payment.StripePaymentIntentId,
                Reason = "requested_by_customer",
            }, new RequestOptions
            {
                IdempotencyKey = $"saga-refund-{paymentId}"
            }, timeoutCts.Token);
        }

        payment.MarkAsRefunded();
        await paymentRepository.SaveChangesAsync(timeoutCts.Token);

        logger.LogInformation("Saga 補償完了（返金）: PaymentId={PaymentId}", paymentId);
    }

    private async Task HandleCheckoutSessionCompletedAsync(Event stripeEvent, CancellationToken ct)
    {
        var session = stripeEvent.Data.Object as Session
            ?? throw new BusinessException("Stripe Session の解析に失敗しました");

        var payment = await paymentRepository.FindByStripeCheckoutSessionIdAsync(session.Id, ct)
            ?? throw new NotFoundException($"決済が見つかりません: StripeSessionId={session.Id}");

        var now = timeProvider.GetUtcNow().UtcDateTime;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            payment.MarkAsCompleted(session.PaymentIntentId, null, now);

            payment.Transactions.Add(new Transaction
            {
                PaymentId = payment.Id,
                Type = TransactionType.Charge,
                Amount = payment.Amount,
                Status = "COMPLETED",
                GatewayResponse = JsonSerializer.Serialize(new { session.Id, session.PaymentIntentId }),
                CreatedAt = now,
                UpdatedAt = now
            });

            await outboxEventRepository.AddAsync(new OutboxEvent
            {
                EventType = "payment.completed",
                AggregateId = payment.Id,
                Payload = JsonSerializer.Serialize(new
                {
                    PaymentId = payment.Id,
                    OrderId = payment.OrderId,
                    CustomerId = payment.CustomerId,
                    Amount = payment.Amount,
                    CurrencyCode = payment.CurrencyCode,
                    PaidAt = now
                }),
                CreatedAt = now
            }, ct);

            await paymentRepository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            logger.LogInformation("決済完了: PaymentId={PaymentId}, OrderId={OrderId}",
                payment.Id, payment.OrderId);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private async Task HandlePaymentFailedAsync(Event stripeEvent, CancellationToken ct)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent
            ?? throw new BusinessException("Stripe PaymentIntent の解析に失敗しました");

        var payment = await paymentRepository.FindByStripePaymentIntentIdAsync(paymentIntent.Id, ct);
        if (payment is null)
        {
            logger.LogWarning("対応する決済が見つかりません: StripePaymentIntentId={IntentId}", paymentIntent.Id);
            return;
        }

        payment.MarkAsFailed(
            paymentIntent.LastPaymentError?.Code ?? "UNKNOWN",
            paymentIntent.LastPaymentError?.Message ?? "決済に失敗しました");

        await outboxEventRepository.AddAsync(new OutboxEvent
        {
            EventType = "payment.failed",
            AggregateId = payment.Id,
            Payload = JsonSerializer.Serialize(new
            {
                PaymentId = payment.Id,
                OrderId = payment.OrderId,
                FailureCode = payment.FailureCode,
                FailureMessage = payment.FailureMessage
            })
        }, ct);

        await paymentRepository.SaveChangesAsync(ct);

        logger.LogWarning("決済失敗: PaymentId={PaymentId}, FailureCode={FailureCode}",
            payment.Id, payment.FailureCode);
    }

    private static PaymentDetailResponse MapToDetailResponse(Payment payment) => new(
        Id: payment.Id, OrderId: payment.OrderId, CustomerId: payment.CustomerId,
        Amount: payment.Amount, CurrencyCode: payment.CurrencyCode,
        Status: payment.Status.ToString().ToUpperInvariant(),
        PaymentMethod: payment.PaymentMethod,
        StripeCheckoutSessionId: payment.StripeCheckoutSessionId,
        StripePaymentIntentId: payment.StripePaymentIntentId,
        PaidAt: payment.PaidAt, CreatedAt: payment.CreatedAt, UpdatedAt: payment.UpdatedAt);
}
```

### 4.9 RefundService 実装

```csharp
// Services/RefundService.cs
using System.Text.Json;
using PaymentCartService.DTOs.Requests;
using PaymentCartService.DTOs.Responses;
using PaymentCartService.Exceptions;
using PaymentCartService.Infrastructure.Persistence;
using PaymentCartService.Models;
using PaymentCartService.Models.Enums;
using PaymentCartService.Repositories.Interfaces;
using PaymentCartService.Services.Interfaces;
using Stripe;

namespace PaymentCartService.Services;

public class RefundService(
    IPaymentRepository paymentRepository,
    IOutboxEventRepository outboxEventRepository,
    AppDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<RefundService> logger) : IRefundService
{
    public async Task<RefundResponse> RefundAsync(
        string paymentId, RefundRequest request, string adminUserId, CancellationToken ct = default)
    {
        var payment = await paymentRepository.FindByIdAsync(paymentId, ct)
            ?? throw new NotFoundException($"決済が見つかりません: {paymentId}");

        if (payment.Status != PaymentStatus.Completed)
            throw new BusinessException("完了済みの決済のみ返金可能です", "PAY-4222");

        if (request.Amount > payment.Amount)
            throw new BusinessException("返金金額が決済金額を超過しています", "PAY-4222");

        var now = timeProvider.GetUtcNow().UtcDateTime;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            Stripe.Refund? stripeRefund = null;
            if (payment.StripePaymentIntentId is not null)
            {
                var refundService = new Stripe.RefundService();
                stripeRefund = await refundService.CreateAsync(new RefundCreateOptions
                {
                    PaymentIntent = payment.StripePaymentIntentId,
                    Amount = (long)(request.Amount * 100),
                    Reason = "requested_by_customer",
                }, new RequestOptions
                {
                    IdempotencyKey = $"refund-{paymentId}-{now.Ticks}"
                }, ct);
            }

            payment.MarkAsRefunded();
            payment.UpdatedBy = adminUserId;

            var refundTransaction = new Transaction
            {
                PaymentId = payment.Id,
                Type = TransactionType.Refund,
                Amount = request.Amount,
                Status = "COMPLETED",
                GatewayResponse = stripeRefund is not null
                    ? JsonSerializer.Serialize(new { stripeRefund.Id, stripeRefund.Status })
                    : null,
                CreatedAt = now,
                UpdatedAt = now
            };

            payment.Transactions.Add(refundTransaction);

            await outboxEventRepository.AddAsync(new OutboxEvent
            {
                EventType = "payment.refunded",
                AggregateId = payment.Id,
                Payload = JsonSerializer.Serialize(new
                {
                    PaymentId = payment.Id,
                    OrderId = payment.OrderId,
                    RefundAmount = request.Amount,
                    Reason = request.Reason,
                    RefundedAt = now
                }),
                CreatedAt = now
            }, ct);

            await paymentRepository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            logger.LogInformation(
                "返金完了: PaymentId={PaymentId}, RefundAmount={RefundAmount}, AdminUserId={AdminUserId}",
                paymentId, request.Amount, adminUserId);

            return new RefundResponse(
                Id: refundTransaction.Id,
                PaymentId: payment.Id,
                RefundAmount: request.Amount,
                Status: "COMPLETED",
                CreatedAt: now);
        }
        catch (StripeException ex)
        {
            await transaction.RollbackAsync(ct);
            logger.LogError(ex, "Stripe 返金エラー: PaymentId={PaymentId}", paymentId);
            throw new BusinessException("返金処理に失敗しました", ex, "PAY-4222");
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
```

### Phase 4 完了チェックリスト

- [ ] `dotnet build PaymentCartService/` — ビルドが成功する
- [ ] 全 Service に primary constructor が使用されている
- [ ] 全 async メソッドに `CancellationToken ct = default` が含まれている
- [ ] DTO が record 型で定義されている
- [ ] FluentValidation バリデーターが全リクエスト DTO に対して作成されている
- [ ] カスタム例外クラスが NotFoundException, BusinessException, ConcurrencyException, CartExpiredException, PaymentProcessingException, RefundProcessingException, StripeApiException, ExternalServiceException で定義されている
- [ ] IPriceService / ITaxCalculator / IShippingFeeCalculator インターフェースが定義されている（設計書 §25）
- [ ] Saga 補償トランザクション（`CompensatePaymentAsync`）がべき等に実装されている
- [ ] Outbox パターンで DB トランザクションとイベント発行が整合している
- [ ] `ILogger<T>` でメッセージテンプレート形式のログ出力が使用されている
- [ ] PII（個人情報）がログに出力されていない
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと（`NotImplementedException`, 仮のハードコード値が残存していないこと）
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディや `=> throw new NotImplementedException()` が本番コードに残存していないこと

---

## Phase 5: Endpoints 実装

### 目的

Minimal API エンドポイントを実装する。カート管理 API（Cookie ベース、AllowAnonymous）、決済 API（認証必須、IDOR 防止）、ゲストチェックアウト API（設計書 §27）をエンドポイント専用クラスに分離して定義する。各エンドポイントに設計書 §24 準拠のレート制限ポリシーを適用する。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `PaymentCartService/Endpoints/CartEndpoints.cs` | 作成 | カート管理エンドポイント |
| 2 | `PaymentCartService/Endpoints/PaymentEndpoints.cs` | 作成 | 決済エンドポイント |
| 3 | `PaymentCartService/Endpoints/GuestCheckoutEndpoints.cs` | 作成 | ゲストチェックアウトエンドポイント（設計書 §27） |
| 4 | `PaymentCartService/Program.cs` | 更新 | エンドポイントマッピング追加 |

### 5.1 CartEndpoints

```csharp
// Endpoints/CartEndpoints.cs
using System.Security.Claims;
using FluentValidation;
using PaymentCartService.DTOs.Requests;
using PaymentCartService.Services.Interfaces;

namespace PaymentCartService.Endpoints;

public static class CartEndpoints
{
    public static void MapCartEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/cart")
            .WithTags("Cart")
            .RequireRateLimiting("cart")
            .WithOpenApi();

        group.MapGet("/", GetCart)
            .AllowAnonymous()
            .WithName("GetCart");

        group.MapPost("/items", AddItem)
            .AllowAnonymous()
            .WithName("AddCartItem");

        group.MapPut("/items/{itemId}", UpdateItemQuantity)
            .AllowAnonymous()
            .WithName("UpdateCartItem");

        group.MapDelete("/items/{itemId}", RemoveItem)
            .AllowAnonymous()
            .WithName("RemoveCartItem");

        group.MapDelete("/", ClearCart)
            .AllowAnonymous()
            .WithName("ClearCart");

        group.MapPost("/merge", MergeCart)
            .RequireAuthorization()
            .WithName("MergeCart");
    }

    private static async Task<IResult> GetCart(
        HttpContext httpContext,
        ICartService cartService,
        CancellationToken ct)
    {
        var cartId = httpContext.Request.Cookies["CartId"];
        var sessionId = httpContext.Connection.Id;

        var cart = await cartService.GetOrCreateCartAsync(cartId, sessionId, ct);

        SetCartCookie(httpContext, cart.Id);
        return Results.Ok(cart);
    }

    private static async Task<IResult> AddItem(
        HttpContext httpContext,
        AddCartItemRequest request,
        IValidator<AddCartItemRequest> validator,
        ICartService cartService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var cartId = httpContext.Request.Cookies["CartId"];
        var sessionId = httpContext.Connection.Id;

        // カートが存在しない場合は作成
        var existingCart = await cartService.GetOrCreateCartAsync(cartId, sessionId, ct);
        var cart = await cartService.AddItemAsync(existingCart.Id, request, ct);

        SetCartCookie(httpContext, cart.Id);
        return Results.Ok(cart);
    }

    private static async Task<IResult> UpdateItemQuantity(
        string itemId,
        HttpContext httpContext,
        UpdateCartItemRequest request,
        IValidator<UpdateCartItemRequest> validator,
        ICartService cartService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var cartId = httpContext.Request.Cookies["CartId"]
            ?? throw new PaymentCartService.Exceptions.NotFoundException("カートが見つかりません");

        var cart = await cartService.UpdateItemQuantityAsync(cartId, itemId, request, ct);
        return Results.Ok(cart);
    }

    private static async Task<IResult> RemoveItem(
        string itemId,
        HttpContext httpContext,
        ICartService cartService,
        CancellationToken ct)
    {
        var cartId = httpContext.Request.Cookies["CartId"]
            ?? throw new PaymentCartService.Exceptions.NotFoundException("カートが見つかりません");

        var cart = await cartService.RemoveItemAsync(cartId, itemId, ct);
        return Results.Ok(cart);
    }

    private static async Task<IResult> ClearCart(
        HttpContext httpContext,
        ICartService cartService,
        CancellationToken ct)
    {
        var cartId = httpContext.Request.Cookies["CartId"];
        if (cartId is null)
            return Results.NoContent();

        await cartService.ClearCartAsync(cartId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> MergeCart(
        MergeCartRequest request,
        IValidator<MergeCartRequest> validator,
        ClaimsPrincipal user,
        ICartService cartService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new PaymentCartService.Exceptions.BusinessException("認証情報が無効です");

        var cart = await cartService.MergeCartAsync(request.GuestCartId, userId, ct);
        return Results.Ok(cart);
    }

    private static void SetCartCookie(HttpContext httpContext, string cartId)
    {
        httpContext.Response.Cookies.Append("CartId", cartId, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromDays(7)
        });
    }
}
```

### 5.2 PaymentEndpoints

```csharp
// Endpoints/PaymentEndpoints.cs
using System.Security.Claims;
using FluentValidation;
using PaymentCartService.DTOs.Requests;
using PaymentCartService.Exceptions;
using PaymentCartService.Services.Interfaces;

namespace PaymentCartService.Endpoints;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/payments")
            .WithTags("Payments")
            .WithOpenApi();

        group.MapPost("/checkout", Checkout)
            .RequireAuthorization()
            .RequireRateLimiting("checkout")
            .WithName("Checkout");

        group.MapGet("/{paymentId}", GetPaymentById)
            .RequireAuthorization()
            .WithName("GetPaymentById");

        group.MapGet("/order/{orderId}", GetPaymentByOrderId)
            .RequireAuthorization()
            .WithName("GetPaymentByOrderId");

        group.MapGet("/customer/{customerId}", GetCustomerPayments)
            .RequireAuthorization()
            .WithName("GetCustomerPayments");

        group.MapPost("/{paymentId}/refund", Refund)
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("refund")
            .WithName("RefundPayment");

        group.MapPost("/webhook", HandleWebhook)
            .AllowAnonymous()
            .RequireRateLimiting("webhook")
            .WithName("StripeWebhook");
    }

    private static async Task<IResult> Checkout(
        CheckoutRequest request,
        IValidator<CheckoutRequest> validator,
        ClaimsPrincipal user,
        IPaymentService paymentService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new BusinessException("認証情報が無効です");

        var payment = await paymentService.CheckoutAsync(request, userId, ct);
        return Results.Created($"/api/v1/payments/{payment.Id}", payment);
    }

    private static async Task<IResult> GetPaymentById(
        string paymentId,
        ClaimsPrincipal user,
        IPaymentService paymentService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new BusinessException("認証情報が無効です");

        return await paymentService.GetByIdAsync(paymentId, userId, ct) is { } payment
            ? Results.Ok(payment)
            : Results.NotFound();
    }

    private static async Task<IResult> GetPaymentByOrderId(
        string orderId,
        ClaimsPrincipal user,
        IPaymentService paymentService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new BusinessException("認証情報が無効です");

        return await paymentService.GetByOrderIdAsync(orderId, userId, ct) is { } payment
            ? Results.Ok(payment)
            : Results.NotFound();
    }

    private static async Task<IResult> GetCustomerPayments(
        string customerId,
        int page,
        int pageSize,
        ClaimsPrincipal user,
        IPaymentService paymentService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new BusinessException("認証情報が無効です");

        var result = await paymentService.GetByCustomerIdAsync(
            customerId, userId, page > 0 ? page : 1, pageSize > 0 ? Math.Min(pageSize, 100) : 20, ct);

        return Results.Ok(result);
    }

    private static async Task<IResult> Refund(
        string paymentId,
        RefundRequest request,
        IValidator<RefundRequest> validator,
        ClaimsPrincipal user,
        IRefundService refundService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var adminUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new BusinessException("認証情報が無効です");

        var result = await refundService.RefundAsync(paymentId, request, adminUserId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleWebhook(
        HttpContext httpContext,
        IPaymentService paymentService,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var json = await new StreamReader(httpContext.Request.Body).ReadToEndAsync(ct);
        var signature = httpContext.Request.Headers["Stripe-Signature"].FirstOrDefault();

        if (string.IsNullOrEmpty(signature))
        {
            logger.LogWarning("Stripe Webhook: Stripe-Signature ヘッダーがありません");
            return Results.BadRequest();
        }

        try
        {
            await paymentService.HandleWebhookAsync(json, signature, ct);
            return Results.Ok();
        }
        catch (Stripe.StripeException ex)
        {
            logger.LogWarning(ex, "Stripe Webhook 署名検証失敗");
            return Results.BadRequest();
        }
    }
}
```

### 5.3 GuestCheckoutEndpoints（設計書 §27）

ゲストユーザー向けのチェックアウトエンドポイント。認証不要（AllowAnonymous）で、Saga フローではクーポン・ポイント検証ステップをスキップする。

```csharp
// Endpoints/GuestCheckoutEndpoints.cs
using FluentValidation;
using PaymentCartService.DTOs.Requests;
using PaymentCartService.Services.Interfaces;

namespace PaymentCartService.Endpoints;

public static class GuestCheckoutEndpoints
{
    public static void MapGuestCheckoutEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/checkout")
            .WithTags("Checkout")
            .WithOpenApi();

        group.MapPost("/guest", GuestCheckout)
            .AllowAnonymous()
            .RequireRateLimiting("checkout")
            .WithName("GuestCheckout");
    }

    private static async Task<IResult> GuestCheckout(
        HttpContext httpContext,
        GuestCheckoutRequest request,
        IValidator<GuestCheckoutRequest> validator,
        IPaymentService paymentService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var cartId = httpContext.Request.Cookies["CartId"]
            ?? request.CartId;

        if (string.IsNullOrWhiteSpace(cartId))
            return Results.BadRequest("カート ID が特定できません");

        var result = await paymentService.GuestCheckoutAsync(cartId, request, ct);
        return Results.Ok(result);
    }
}
```

> **注記**: `GuestCheckoutRequest` は `CheckoutRequest` の簡略版で、`CustomerId` を含まない。ゲストチェックアウト時の Saga フローでは、CouponService（Step 3）と PointService（Step 4）の検証ステップをスキップする（設計書 §27 準拠）。`GuestCheckoutRequest` DTO と `GuestCheckoutRequestValidator` バリデーターを Phase 4 に追加すること。

### 5.4 Program.cs 更新

```csharp
// Program.cs にエンドポイントマッピングを追加
app.MapCartEndpoints();
app.MapPaymentEndpoints();
app.MapGuestCheckoutEndpoints();
```

### Phase 5 完了チェックリスト

- [ ] `dotnet build PaymentCartService/` — ビルドが成功する
- [ ] カート API（GET/POST/PUT/DELETE）が `AllowAnonymous()` に設定されている
- [ ] カートマージ API が `RequireAuthorization()` に設定されている
- [ ] 決済 API が `RequireAuthorization()` に設定されている
- [ ] 返金 API が `RequireAuthorization("AdminOnly")` に設定されている
- [ ] Webhook API が `AllowAnonymous()` に設定されている（Stripe-Signature による署名検証で保護）
- [ ] ゲストチェックアウト API（`/api/v1/checkout/guest`）が `AllowAnonymous()` に設定されている（設計書 §27）
- [ ] Cookie に `HttpOnly=true, Secure=true, SameSite=Strict` が設定されている
- [ ] 全エンドポイントで `CancellationToken ct` がバインドされている
- [ ] FluentValidation による入力バリデーションが全 POST/PUT エンドポイントに適用されている
- [ ] IDOR 防止: 決済情報取得時に `ClaimsPrincipal` からユーザー ID を取得して検証している
- [ ] レート制限: カート API に `RequireRateLimiting("cart")`、チェックアウトに `RequireRateLimiting("checkout")`、返金に `RequireRateLimiting("refund")`、Webhook に `RequireRateLimiting("webhook")` が適用されている（設計書 §24）
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと（`NotImplementedException`, 仮のハードコード値が残存していないこと）
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディや `=> throw new NotImplementedException()` が本番コードに残存していないこと

---

## Phase 6: Kafka イベント連携

### 目的

Outbox パターンによるイベント発行（OutboxPublisher BackgroundService）、およびイベント購読（OrderCreated, OrderCancelled, UserDeleted Consumer）を実装する。動的バックオフポーリング、Dead Letter 対応を含む。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `PaymentCartService/Infrastructure/Kafka/OutboxPublisher.cs` | 作成 | Outbox イベント Publisher（BackgroundService） |
| 2 | `PaymentCartService/Infrastructure/Kafka/OrderCreatedConsumer.cs` | 作成 | 注文作成イベント Consumer |
| 3 | `PaymentCartService/Infrastructure/Kafka/OrderCancelledConsumer.cs` | 作成 | 注文キャンセルイベント Consumer |
| 4 | `PaymentCartService/Infrastructure/Kafka/UserDeletedConsumer.cs` | 作成 | ユーザー削除イベント Consumer（GDPR 対応） |
| 5 | `PaymentCartService/Infrastructure/Kafka/Events/PaymentCompletedEvent.cs` | 作成 | 発行イベント定義 |
| 6 | `PaymentCartService/Infrastructure/Kafka/Events/PaymentFailedEvent.cs` | 作成 | 発行イベント定義 |
| 7 | `PaymentCartService/Infrastructure/Kafka/Events/PaymentRefundedEvent.cs` | 作成 | 発行イベント定義 |
| 8 | `PaymentCartService/Infrastructure/Kafka/Events/OrderCreatedEvent.cs` | 作成 | 購読イベント定義 |
| 9 | `PaymentCartService/Infrastructure/Kafka/Events/OrderCancelledEvent.cs` | 作成 | 購読イベント定義 |
| 10 | `PaymentCartService/Infrastructure/Kafka/Events/UserDeletedEvent.cs` | 作成 | 購読イベント定義 |
| 11 | `PaymentCartService/Services/ExpiredCartCleanupService.cs` | 作成 | 期限切れカート削除 BackgroundService |
| 12 | `PaymentCartService/Services/CartExpirationService.cs` | 作成 | カートステータス期限切れ更新 BackgroundService |
| 13 | `PaymentCartService/Program.cs` | 更新 | Kafka Producer/Consumer DI 登録 |

### 6.1 イベント定義

```csharp
// Infrastructure/Kafka/Events/PaymentCompletedEvent.cs
namespace PaymentCartService.Infrastructure.Kafka.Events;

public record PaymentCompletedEvent(
    string PaymentId,
    string OrderId,
    string CustomerId,
    decimal Amount,
    string CurrencyCode,
    DateTime PaidAt);
```

```csharp
// Infrastructure/Kafka/Events/PaymentFailedEvent.cs
namespace PaymentCartService.Infrastructure.Kafka.Events;

public record PaymentFailedEvent(
    string PaymentId,
    string OrderId,
    string? FailureCode,
    string? FailureMessage);
```

```csharp
// Infrastructure/Kafka/Events/PaymentRefundedEvent.cs
namespace PaymentCartService.Infrastructure.Kafka.Events;

public record PaymentRefundedEvent(
    string PaymentId,
    string OrderId,
    decimal RefundAmount,
    string Reason,
    DateTime RefundedAt);
```

```csharp
// Infrastructure/Kafka/Events/OrderCreatedEvent.cs
namespace PaymentCartService.Infrastructure.Kafka.Events;

public record OrderCreatedEvent(
    string OrderId,
    string CustomerId,
    decimal TotalAmount,
    string CurrencyCode,
    DateTime OccurredAt);
```

```csharp
// Infrastructure/Kafka/Events/OrderCancelledEvent.cs
namespace PaymentCartService.Infrastructure.Kafka.Events;

public record OrderCancelledEvent(
    string OrderId,
    string CustomerId,
    string Reason,
    DateTime OccurredAt);
```

```csharp
// Infrastructure/Kafka/Events/UserDeletedEvent.cs
namespace PaymentCartService.Infrastructure.Kafka.Events;

public record UserDeletedEvent(
    string UserId,
    DateTime OccurredAt);
```

### 6.2 OutboxPublisher（動的バックオフ）

```csharp
// Infrastructure/Kafka/OutboxPublisher.cs
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using PaymentCartService.Infrastructure.Persistence;
using PaymentCartService.Models.Enums;

namespace PaymentCartService.Infrastructure.Kafka;

public class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    TimeProvider timeProvider,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private static readonly TimeSpan MinPollingInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MaxPollingInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 100;

    private TimeSpan _currentInterval = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxPublisher 開始");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var publishedCount = await PublishPendingEventsAsync(stoppingToken);

                _currentInterval = publishedCount > 0
                    ? MinPollingInterval
                    : TimeSpan.FromTicks(Math.Min(
                        _currentInterval.Ticks * 2,
                        MaxPollingInterval.Ticks));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "OutboxPublisher エラー: {Message}", ex.Message);
                _currentInterval = MaxPollingInterval;
            }

            await Task.Delay(_currentInterval, stoppingToken);
        }
    }

    private async Task<int> PublishPendingEventsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var pendingEvents = await context.OutboxEvents
            .Where(e => e.Status == OutboxEventStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (pendingEvents.Count == 0)
            return 0;

        foreach (var evt in pendingEvents)
        {
            try
            {
                evt.Status = OutboxEventStatus.Processing;
                await context.SaveChangesAsync(ct);

                await producer.ProduceAsync(evt.EventType, new Message<string, string>
                {
                    Key = evt.AggregateId,
                    Value = evt.Payload
                }, ct);

                evt.Status = OutboxEventStatus.Published;
                evt.PublishedAt = now;

                logger.LogInformation(
                    "Outbox イベント発行: EventType={EventType}, AggregateId={AggregateId}",
                    evt.EventType, evt.AggregateId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                evt.RetryCount++;
                evt.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;

                if (evt.RetryCount >= evt.MaxRetries)
                {
                    evt.Status = OutboxEventStatus.DeadLetter;
                    logger.LogError(ex,
                        "Outbox イベント Dead Letter: EventId={EventId}, RetryCount={RetryCount}",
                        evt.Id, evt.RetryCount);
                }
                else
                {
                    evt.Status = OutboxEventStatus.Pending;
                    logger.LogWarning(ex,
                        "Outbox イベントリトライ: EventId={EventId}, RetryCount={RetryCount}",
                        evt.Id, evt.RetryCount);
                }
            }
        }

        await context.SaveChangesAsync(ct);
        return pendingEvents.Count;
    }
}
```

### 6.3 OrderCreatedConsumer

```csharp
// Infrastructure/Kafka/OrderCreatedConsumer.cs
using System.Text.Json;
using Confluent.Kafka;
using PaymentCartService.Infrastructure.Kafka.Events;
using PaymentCartService.Models;
using PaymentCartService.Models.Enums;
using PaymentCartService.Repositories.Interfaces;

namespace PaymentCartService.Infrastructure.Kafka;

public class OrderCreatedConsumer(
    IConsumer<string, string> consumer,
    IServiceScopeFactory scopeFactory,
    ILogger<OrderCreatedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        consumer.Subscribe("order.created");
        logger.LogInformation("OrderCreatedConsumer 開始: Topic=order.created");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var @event = JsonSerializer.Deserialize<OrderCreatedEvent>(result.Message.Value);

                if (@event is not null)
                {
                    using var scope = scopeFactory.CreateScope();
                    var paymentRepository = scope.ServiceProvider
                        .GetRequiredService<IPaymentRepository>();

                    var existingPayment = await paymentRepository
                        .FindByOrderIdAsync(@event.OrderId, stoppingToken);

                    if (existingPayment is null)
                    {
                        var payment = new Payment
                        {
                            OrderId = @event.OrderId,
                            CustomerId = @event.CustomerId,
                            Amount = @event.TotalAmount,
                            CurrencyCode = @event.CurrencyCode,
                            Status = PaymentStatus.Pending,
                            PaymentMethod = "PENDING"
                        };

                        await paymentRepository.AddAsync(payment, stoppingToken);
                        await paymentRepository.SaveChangesAsync(stoppingToken);

                        logger.LogInformation(
                            "注文受信・決済レコード作成: OrderId={OrderId}, PaymentId={PaymentId}",
                            @event.OrderId, payment.Id);
                    }
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

### 6.4 UserDeletedConsumer（GDPR 対応）

```csharp
// Infrastructure/Kafka/UserDeletedConsumer.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using PaymentCartService.Infrastructure.Kafka.Events;
using PaymentCartService.Infrastructure.Persistence;
using PaymentCartService.Models.Enums;

namespace PaymentCartService.Infrastructure.Kafka;

public class UserDeletedConsumer(
    IConsumer<string, string> consumer,
    IServiceScopeFactory scopeFactory,
    ILogger<UserDeletedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        consumer.Subscribe("user.deleted");
        logger.LogInformation("UserDeletedConsumer 開始: Topic=user.deleted");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var @event = JsonSerializer.Deserialize<UserDeletedEvent>(result.Message.Value);

                if (@event is not null)
                {
                    using var scope = scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var hashedUserId = HashUserId(@event.UserId);

                    // カートデータ: customer_id を NULL に更新
                    await context.Carts
                        .Where(c => c.CustomerId == @event.UserId)
                        .ExecuteUpdateAsync(s => s.SetProperty(c => c.CustomerId, (string?)null),
                            stoppingToken);

                    // 決済データ: customer_id をハッシュ値に置換（匿名化）
                    await context.Payments
                        .Where(p => p.CustomerId == @event.UserId)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.CustomerId, hashedUserId),
                            stoppingToken);

                    logger.LogInformation(
                        "ユーザーデータ匿名化完了: HashedUserId={HashedUserId}", hashedUserId);
                }

                consumer.Commit(result);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "UserDeleted イベント処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private static string HashUserId(string userId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(userId));
        return $"DELETED_{Convert.ToHexStringLower(hash)[..16]}";
    }
}
```

### 6.5 BackgroundService（カート管理）

```csharp
// Services/ExpiredCartCleanupService.cs
using Microsoft.Extensions.Options;
using PaymentCartService.Configurations;
using PaymentCartService.Repositories.Interfaces;

namespace PaymentCartService.Services;

public class ExpiredCartCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<CartSettings> cartOptions,
    TimeProvider timeProvider,
    ILogger<ExpiredCartCleanupService> logger) : BackgroundService
{
    private readonly CartSettings _settings = cartOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ExpiredCartCleanupService 開始");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var cartRepository = scope.ServiceProvider.GetRequiredService<ICartRepository>();

                var cutoffDate = timeProvider.GetUtcNow().UtcDateTime.AddDays(-30);
                var deletedCount = await cartRepository.CleanupExpiredCartsAsync(
                    cutoffDate, _settings.CleanupBatchSize, stoppingToken);

                if (deletedCount > 0)
                    logger.LogInformation("期限切れカート削除: DeletedCount={DeletedCount}", deletedCount);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "ExpiredCartCleanupService エラー: {Message}", ex.Message);
            }

            await Task.Delay(
                TimeSpan.FromMinutes(_settings.CleanupIntervalMinutes), stoppingToken);
        }
    }
}
```

```csharp
// Services/CartExpirationService.cs
using Microsoft.Extensions.Options;
using PaymentCartService.Configurations;
using PaymentCartService.Repositories.Interfaces;

namespace PaymentCartService.Services;

public class CartExpirationService(
    IServiceScopeFactory scopeFactory,
    IOptions<CartSettings> cartOptions,
    TimeProvider timeProvider,
    ILogger<CartExpirationService> logger) : BackgroundService
{
    private readonly CartSettings _settings = cartOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CartExpirationService 開始");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var cartRepository = scope.ServiceProvider.GetRequiredService<ICartRepository>();

                var now = timeProvider.GetUtcNow().UtcDateTime;
                var expiredCount = await cartRepository.ExpireCartsAsync(
                    now, _settings.ExpirationBatchSize, stoppingToken);

                if (expiredCount > 0)
                    logger.LogInformation("カート期限切れ更新: ExpiredCount={ExpiredCount}", expiredCount);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "CartExpirationService エラー: {Message}", ex.Message);
            }

            await Task.Delay(
                TimeSpan.FromMinutes(_settings.ExpirationCheckIntervalMinutes), stoppingToken);
        }
    }
}
```

### 6.6 Program.cs Kafka 関連 DI 登録

```csharp
// Program.cs に Kafka 関連を追加
using Confluent.Kafka;

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

// ── Kafka Consumers ──
builder.Services.AddSingleton<IConsumer<string, string>>(sp =>
{
    var config = new ConsumerConfig
    {
        BootstrapServers = builder.Configuration["Kafka:BootstrapServers"],
        GroupId = "payment-cart-service",
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = false
    };
    return new ConsumerBuilder<string, string>(config).Build();
});

// ── BackgroundService 登録 ──
builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddHostedService<OrderCreatedConsumer>();
builder.Services.AddHostedService<UserDeletedConsumer>();
builder.Services.AddHostedService<ExpiredCartCleanupService>();
builder.Services.AddHostedService<CartExpirationService>();
```

### Phase 6 完了チェックリスト

- [ ] `dotnet build PaymentCartService/` — ビルドが成功する
- [ ] OutboxPublisher に動的バックオフ（100ms〜5s）が実装されている
- [ ] OutboxPublisher で `IServiceScopeFactory` を使用して Scoped サービスを取得している
- [ ] OutboxPublisher のリトライが最大 5 回に設定されている
- [ ] Dead Letter 状態への遷移が実装されている
- [ ] OrderCreatedConsumer で注文に対する決済レコードが作成される
- [ ] UserDeletedConsumer で GDPR 対応の匿名化処理が実装されている
- [ ] ExpiredCartCleanupService で 30 日経過カートが削除される
- [ ] CartExpirationService で 24 時間経過 ACTIVE カートが EXPIRED に更新される
- [ ] 全 BackgroundService で `stoppingToken` が下位呼び出しに伝搬されている
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと（`NotImplementedException`, 仮のハードコード値が残存していないこと）
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディや `=> throw new NotImplementedException()` が本番コードに残存していないこと

---

## Phase 7: Redis キャッシュ連携

### 目的

Write-Through パターンによるカートデータの Redis キャッシュを実装する。キャッシュキー設計、TTL 管理、Redis 障害時のフォールバック、決済ステータスキャッシュを含む。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `PaymentCartService/Services/Interfaces/ICartCacheService.cs` | 作成 | カートキャッシュ Service インターフェース |
| 2 | `PaymentCartService/Services/CartCacheService.cs` | 作成 | Redis キャッシュ Service 実装 |
| 3 | `PaymentCartService/Services/CartService.cs` | 更新 | キャッシュ統合 |
| 4 | `PaymentCartService/Program.cs` | 更新 | Redis DI 登録 |

### 7.1 キャッシュキー設計

| キー | パターン | TTL | 用途 |
|------|---------|-----|------|
| `cart:{cartId}` | カート ID | 7 日 | カートデータキャッシュ |
| `cart:session:{sessionId}` | セッション ID | 7 日 | セッション別カート ID マッピング |
| `cart:user:{customerId}` | ユーザー ID | 7 日 | ユーザー別カート ID マッピング |
| `payment:{paymentId}:status` | 決済 ID | 30 分 | 決済ステータスキャッシュ |

### 7.2 CartCacheService

```csharp
// Services/Interfaces/ICartCacheService.cs
using PaymentCartService.Models;

namespace PaymentCartService.Services.Interfaces;

public interface ICartCacheService
{
    Task<Cart?> GetCartAsync(string cartId, CancellationToken ct = default);
    Task SetCartAsync(Cart cart, CancellationToken ct = default);
    Task RemoveCartAsync(string cartId, CancellationToken ct = default);
    Task<string?> GetCartIdBySessionAsync(string sessionId, CancellationToken ct = default);
    Task SetCartIdBySessionAsync(string sessionId, string cartId, CancellationToken ct = default);
    Task<string?> GetCartIdByUserAsync(string customerId, CancellationToken ct = default);
    Task SetCartIdByUserAsync(string customerId, string cartId, CancellationToken ct = default);
}
```

```csharp
// Services/CartCacheService.cs
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using PaymentCartService.Models;
using PaymentCartService.Services.Interfaces;

namespace PaymentCartService.Services;

public class CartCacheService(
    IDistributedCache cache,
    ILogger<CartCacheService> logger) : ICartCacheService
{
    private static readonly TimeSpan CartTtl = TimeSpan.FromDays(7);
    private static readonly DistributedCacheEntryOptions CartCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = CartTtl
    };

    public async Task<Cart?> GetCartAsync(string cartId, CancellationToken ct = default)
    {
        try
        {
            var cached = await cache.GetStringAsync($"cart:{cartId}", ct);
            if (cached is null) return null;

            return JsonSerializer.Deserialize<Cart>(cached);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis キャッシュ読み取りエラー: CartId={CartId}", cartId);
            return null;
        }
    }

    public async Task SetCartAsync(Cart cart, CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(cart);
            await cache.SetStringAsync($"cart:{cart.Id}", json, CartCacheOptions, ct);

            if (cart.CustomerId is not null)
                await cache.SetStringAsync(
                    $"cart:user:{cart.CustomerId}", cart.Id, CartCacheOptions, ct);

            await cache.SetStringAsync(
                $"cart:session:{cart.SessionId}", cart.Id, CartCacheOptions, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis キャッシュ書き込みエラー: CartId={CartId}", cart.Id);
        }
    }

    public async Task RemoveCartAsync(string cartId, CancellationToken ct = default)
    {
        try
        {
            await cache.RemoveAsync($"cart:{cartId}", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis キャッシュ削除エラー: CartId={CartId}", cartId);
        }
    }

    public async Task<string?> GetCartIdBySessionAsync(string sessionId, CancellationToken ct = default)
    {
        try
        {
            return await cache.GetStringAsync($"cart:session:{sessionId}", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis セッションキャッシュ読み取りエラー: SessionId={SessionId}", sessionId);
            return null;
        }
    }

    public async Task SetCartIdBySessionAsync(
        string sessionId, string cartId, CancellationToken ct = default)
    {
        try
        {
            await cache.SetStringAsync($"cart:session:{sessionId}", cartId, CartCacheOptions, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis セッションキャッシュ書き込みエラー: SessionId={SessionId}", sessionId);
        }
    }

    public async Task<string?> GetCartIdByUserAsync(string customerId, CancellationToken ct = default)
    {
        try
        {
            return await cache.GetStringAsync($"cart:user:{customerId}", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis ユーザーキャッシュ読み取りエラー: CustomerId={CustomerId}", customerId);
            return null;
        }
    }

    public async Task SetCartIdByUserAsync(
        string customerId, string cartId, CancellationToken ct = default)
    {
        try
        {
            await cache.SetStringAsync($"cart:user:{customerId}", cartId, CartCacheOptions, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis ユーザーキャッシュ書き込みエラー: CustomerId={CustomerId}", customerId);
        }
    }
}
```

### 7.3 Program.cs Redis DI 登録

```csharp
// Program.cs に Redis 設定を追加
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException("Redis 接続文字列が設定されていません");
    options.InstanceName = "PaymentCartService:";
});

builder.Services.AddScoped<ICartCacheService, CartCacheService>();
```

> **注記**: Redis 障害時はキャッシュ操作でログ出力のみ行い、PostgreSQL からの直接読み取りにフォールバックする。全キャッシュ操作は try-catch でラップし、Redis 障害がサービス全体の障害に波及しないようにする。

### Phase 7 完了チェックリスト

- [ ] `dotnet build PaymentCartService/` — ビルドが成功する
- [ ] Redis キャッシュの TTL が 7 日に設定されている
- [ ] Redis 障害時にフォールバック（PostgreSQL 直接読み取り）が動作する
- [ ] 全キャッシュ操作が try-catch でラップされている
- [ ] キャッシュキーが `cart:{id}`, `cart:session:{sessionId}`, `cart:user:{userId}` で設計されている
- [ ] Write-Through パターン: DB 書き込み後に Redis キャッシュを更新している
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと（`NotImplementedException`, 仮のハードコード値が残存していないこと）
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディや `=> throw new NotImplementedException()` が本番コードに残存していないこと

---

## Phase 8: 認証・認可・セキュリティ実装

### 目的

JWT Bearer 認証、認可ポリシー（Fallback Policy, AdminOnly）、セキュリティヘッダー、CORS、レート制限、グローバル例外ハンドラー、InventoryService 連携用 HttpClient（Polly リトライ + サーキットブレーカー）を実装する。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `PaymentCartService/Program.cs` | 更新 | 認証・認可・セキュリティ設定 |

### 8.1 InventoryService 連携用 HttpClient（設計書 §33）

```csharp
// Program.cs — HttpClient + Resilience（InventoryManagementService 連携用）
builder.Services.AddHttpClient("InventoryService", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Services:InventoryManagement:BaseUrl"]
        ?? "https://inventory-service");
})
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.Delay = TimeSpan.FromMilliseconds(500);
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.MinimumThroughput = 10;
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
});
```

> **用途**: カートアイテム追加時に InventoryManagementService から商品情報（名前、SKU、単価）を取得する。クライアントから価格を受け取らず、サーバーサイドで価格を確定する（設計書 §23/§25 準拠）。

### 8.2 認証・認可設定

```csharp
// Program.cs — 認証・認可の完全実装
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

// ── 認証 ──
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

// ── 認可（Fallback Policy: デフォルト認証必須） ──
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("UserOrAdmin", p => p.RequireRole("User", "Admin"));
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

### 8.3 セキュリティヘッダー・CORS・レート制限

```csharp
// Program.cs — セキュリティヘッダー
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

// ── CORS ──
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(builder.Configuration["Cors:AllowedOrigins"] ?? "https://skishop.example.com")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
});

// ── レート制限 ──
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("checkout", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });

    options.AddSlidingWindowLimiter("cart", limiter =>
    {
        limiter.PermitLimit = 60;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.SegmentsPerWindow = 6;
        limiter.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("refund", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromHours(1);
        limiter.QueueLimit = 0;
    });

    // Webhook: 100 回/分 — DDoS 緩和（設計書 §15/§24 多層防御 L2）
    options.AddFixedWindowLimiter("webhook", limiter =>
    {
        limiter.PermitLimit = 100;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
```

### 8.4 グローバル例外ハンドラー

```csharp
// Program.cs — グローバル例外ハンドラー（IExceptionHandler）
using Microsoft.AspNetCore.Diagnostics;
using PaymentCartService.Exceptions;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var (statusCode, message, errorCode) = exception switch
        {
            NotFoundException e => (404, e.Message, (string?)null),
            CartExpiredException e => (409, e.Message, e.ErrorCode),
            ConcurrencyException e => (409, e.Message, (string?)null),
            PaymentProcessingException e => (422, e.Message, e.ErrorCode),
            RefundProcessingException e => (422, e.Message, e.ErrorCode),
            StripeApiException e => (422, e.Message, e.ErrorCode),
            BusinessException e => (422, e.Message, e.ErrorCode),
            ExternalServiceException e => (503, e.Message, e.ErrorCode),
            Stripe.StripeException => (502, "決済ゲートウェイエラー", "PAY-5002"),
            _ => (500, "内部エラーが発生しました", "PAY-5001")
        };

        if (statusCode >= 500)
            logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        else
            logger.LogWarning("Handled exception: {ExceptionType} - {Message}",
                exception.GetType().Name, exception.Message);

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new
        {
            status = statusCode,
            detail = message,
            errorCode
        }, ct);

        return true;
    }
}

// Program.cs での登録
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
```

### 8.5 ミドルウェアパイプライン順序（最終版）

```csharp
// Program.cs — ミドルウェアパイプライン順序（厳守）
var app = builder.Build();

// 1. 例外ハンドラー
app.UseExceptionHandler();

// 2. セキュリティヘッダー
app.UseHsts();
app.UseHttpsRedirection();

// 3. セキュリティヘッダーミドルウェア（カスタム）
// 上記 app.Use(...) のセキュリティヘッダー

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
app.MapCartEndpoints();
app.MapPaymentEndpoints();
app.MapGuestCheckoutEndpoints();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapHealthChecks("/health/ready").AllowAnonymous();

app.Run();
```

### Phase 8 完了チェックリスト

- [ ] `dotnet build PaymentCartService/` — ビルドが成功する
- [ ] JWT Bearer 認証が設定されている
- [ ] Fallback Policy でデフォルト認証必須が設定されている
- [ ] AdminOnly ポリシーが定義されている
- [ ] セキュリティヘッダー（X-Content-Type-Options, X-Frame-Options, CSP）が設定されている
- [ ] CORS で `AllowAnyOrigin()` が使用されていない
- [ ] レート制限: checkout=10/分, cart=60/分, refund=5/時, webhook=100/分 が設定されている（設計書 §24）
- [ ] グローバル例外ハンドラーで CartExpiredException→409, PaymentProcessingException→422, RefundProcessingException→422, StripeApiException→422, ExternalServiceException→503 が正しくマッピングされている（設計書 §33）
- [ ] グローバル例外ハンドラーでスタックトレースがクライアントに返されていない
- [ ] InventoryService 連携用 HttpClient が `AddStandardResilienceHandler` 付きで登録されている（設計書 §33）
- [ ] ミドルウェアパイプライン順序が正しい（ExceptionHandler → Security → CORS → Auth → RateLimiter → Endpoints）
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと（`NotImplementedException`, 仮のハードコード値が残存していないこと）
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディや `=> throw new NotImplementedException()` が本番コードに残存していないこと

---

## Phase 9: 単体テスト・統合テスト

### 目的

xUnit + NSubstitute + Shouldly による単体テスト、WebApplicationFactory による統合テスト、Testcontainers.PostgreSql による DB スライステストを実装する。分岐カバレッジ 80% 以上を目標とする。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `PaymentCartService.Tests/PaymentCartService.Tests.csproj` | 作成 | テストプロジェクト |
| 2 | `PaymentCartService.Tests/Services/CartServiceTests.cs` | 作成 | CartService 単体テスト |
| 3 | `PaymentCartService.Tests/Services/PaymentServiceTests.cs` | 作成 | PaymentService 単体テスト |
| 4 | `PaymentCartService.Tests/Services/RefundServiceTests.cs` | 作成 | RefundService 単体テスト |
| 5 | `PaymentCartService.Tests/Validators/AddCartItemRequestValidatorTests.cs` | 作成 | バリデーターテスト |
| 6 | `PaymentCartService.Tests/Validators/CheckoutRequestValidatorTests.cs` | 作成 | バリデーターテスト |
| 7 | `PaymentCartService.Tests/Endpoints/CartEndpointsTests.cs` | 作成 | カートエンドポイント統合テスト |
| 8 | `PaymentCartService.Tests/Endpoints/PaymentEndpointsTests.cs` | 作成 | 決済エンドポイント統合テスト |
| 9 | `PaymentCartService.Tests/Repositories/CartRepositoryTests.cs` | 作成 | DB スライステスト |

### 9.1 テストプロジェクト

```xml
<!-- PaymentCartService.Tests/PaymentCartService.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="NSubstitute" Version="5.*" />
    <PackageReference Include="Shouldly" Version="4.*" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.*" />
    <PackageReference Include="Testcontainers.PostgreSql" Version="4.*" />
    <PackageReference Include="coverlet.collector" Version="6.*" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../PaymentCartService/PaymentCartService.csproj" />
  </ItemGroup>
</Project>
```

### 9.2 CartServiceTests

```csharp
// PaymentCartService.Tests/Services/CartServiceTests.cs
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using PaymentCartService.Configurations;
using PaymentCartService.DTOs.Requests;
using PaymentCartService.Exceptions;
using PaymentCartService.Models;
using PaymentCartService.Models.Enums;
using PaymentCartService.Repositories.Interfaces;
using PaymentCartService.Services;
using Shouldly;

namespace PaymentCartService.Tests.Services;

public class CartServiceTests
{
    private readonly ICartRepository _cartRepository;
    private readonly IOptions<CartSettings> _cartOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CartService> _logger;
    private readonly CartService _cartService;

    public CartServiceTests()
    {
        _cartRepository = Substitute.For<ICartRepository>();
        _cartOptions = Options.Create(new CartSettings
        {
            ExpiryDays = 7,
            MaxItemsPerCart = 50,
            MaxQuantityPerItem = 10
        });
        _timeProvider = Substitute.For<TimeProvider>();
        _timeProvider.GetUtcNow().Returns(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
        _logger = Substitute.For<ILogger<CartService>>();
        _cartService = new CartService(_cartRepository, _cartOptions, _timeProvider, _logger);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnCart_When_CartExists()
    {
        // Arrange
        var cart = new Cart
        {
            Id = "cart-1",
            SessionId = "session-1",
            Status = CartStatus.Active,
            Items = [new CartItem
            {
                Id = "item-1",
                ProductId = "prod-1",
                ProductName = "スキーブーツ",
                Sku = "SKI-001",
                UnitPrice = 25000m,
                Quantity = 1,
                Subtotal = 25000m
            }]
        };
        _cartRepository.FindByIdWithItemsAsync("cart-1", default).Returns(cart);

        // Act
        var result = await _cartService.GetCartAsync("cart-1");

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("cart-1");
        result.Items.Count.ShouldBe(1);
        result.TotalAmount.ShouldBe(25000m);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowNotFoundException_When_CartDoesNotExist()
    {
        // Arrange
        _cartRepository.FindByIdWithItemsAsync("nonexistent", default).Returns((Cart?)null);

        // Act & Assert
        var act = async () => await _cartService.GetCartAsync("nonexistent");
        var ex = await Should.ThrowAsync<NotFoundException>(act);
        ex.Message.ShouldContain("カートが見つかりません");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_CartIsExpired()
    {
        // Arrange
        var cart = new Cart { Id = "cart-1", Status = CartStatus.Expired };
        _cartRepository.FindByIdWithItemsAsync("cart-1", default).Returns(cart);

        // Act & Assert
        var act = async () => await _cartService.GetCartAsync("cart-1");
        var ex = await Should.ThrowAsync<BusinessException>(act);
        ex.Message.ShouldContain("有効ではありません");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_CartItemLimitExceeded()
    {
        // Arrange
        var items = Enumerable.Range(1, 50)
            .Select(i => new CartItem
            {
                Id = $"item-{i}",
                ProductId = $"prod-{i}",
                ProductName = $"商品{i}",
                Sku = $"SKU-{i}",
                UnitPrice = 1000m,
                Quantity = 1,
                Subtotal = 1000m
            })
            .ToList();

        var cart = new Cart
        {
            Id = "cart-1",
            Status = CartStatus.Active,
            Items = items
        };
        _cartRepository.FindByIdWithItemsAsync("cart-1", default).Returns(cart);

        // Act & Assert
        var act = async () => await _cartService.AddItemAsync(
            "cart-1", new AddCartItemRequest("prod-new", 1));
        var ex = await Should.ThrowAsync<BusinessException>(act);
        ex.Message.ShouldContain("上限");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_MergeGuestCartIntoUserCart_When_UserCartExists()
    {
        // Arrange
        var guestCart = new Cart
        {
            Id = "guest-cart",
            Status = CartStatus.Active,
            Items = [new CartItem
            {
                Id = "g-item-1",
                ProductId = "prod-A",
                ProductName = "商品A",
                Sku = "SKU-A",
                UnitPrice = 1000m,
                Quantity = 1,
                Subtotal = 1000m
            }]
        };

        var userCart = new Cart
        {
            Id = "user-cart",
            CustomerId = "user-1",
            Status = CartStatus.Active,
            Items = [new CartItem
            {
                Id = "u-item-1",
                ProductId = "prod-B",
                ProductName = "商品B",
                Sku = "SKU-B",
                UnitPrice = 2000m,
                Quantity = 1,
                Subtotal = 2000m
            }]
        };

        _cartRepository.FindByIdWithItemsAsync("guest-cart", default).Returns(guestCart);
        _cartRepository.FindActiveByCustomerIdAsync("user-1", default).Returns(userCart);

        // Act
        var result = await _cartService.MergeCartAsync("guest-cart", "user-1");

        // Assert
        result.Items.Count.ShouldBe(2);
        guestCart.Status.ShouldBe(CartStatus.Abandoned);
        await _cartRepository.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowOperationCanceled_When_TokenIsCanceled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _cartRepository.FindByIdWithItemsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        var act = async () => await _cartService.GetCartAsync("cart-1", cts.Token);
        await Should.ThrowAsync<OperationCanceledException>(act);
    }
}
```

### 9.3 バリデーターテスト例

```csharp
// PaymentCartService.Tests/Validators/AddCartItemRequestValidatorTests.cs
using PaymentCartService.DTOs.Requests;
using PaymentCartService.Validators;
using Shouldly;

namespace PaymentCartService.Tests.Validators;

public class AddCartItemRequestValidatorTests
{
    private readonly AddCartItemRequestValidator _validator = new();

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_ValidRequest()
    {
        // Arrange
        var request = new AddCartItemRequest("prod-123", 2);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_ProductIdIsEmpty(string productId)
    {
        // Arrange
        var request = new AddCartItemRequest(productId, 1);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_QuantityIsOutOfRange(int quantity)
    {
        // Arrange
        var request = new AddCartItemRequest("prod-123", quantity);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }
}
```

### 9.4 テストケース一覧

| カテゴリ | テストケース | 種別 | 優先度 |
|---------|------------|------|--------|
| カート取得 | Should_ReturnCart_When_CartExists | Unit | 必須 |
| カート取得 | Should_ThrowNotFoundException_When_CartDoesNotExist | Unit | 必須 |
| カート取得 | Should_ThrowBusinessException_When_CartIsExpired | Unit | 必須 |
| カート追加 | Should_AddItem_When_ValidRequest | Unit | 必須 |
| カート追加 | Should_IncrementQuantity_When_SameProductAdded | Unit | 必須 |
| カート追加 | Should_ThrowBusinessException_When_CartItemLimitExceeded | Unit | 必須 |
| カート更新 | Should_UpdateQuantity_When_ValidRequest | Unit | 必須 |
| カート削除 | Should_RemoveItem_When_ItemExists | Unit | 必須 |
| カートマージ | Should_MergeGuestCartIntoUserCart_When_UserCartExists | Unit | 必須 |
| カートマージ | Should_AssignGuestCartToUser_When_NoUserCart | Unit | 必須 |
| 楽観的ロック | Should_ThrowConcurrencyException_When_VersionConflict | Unit | 必須 |
| 決済チェックアウト | Should_CreateStripeSession_When_ValidRequest | Unit | 必須 |
| 決済チェックアウト | Should_ThrowBusinessException_When_CartIsEmpty | Unit | 必須 |
| Webhook | Should_CompletePayment_When_CheckoutSessionCompleted | Unit | 必須 |
| Webhook | Should_MarkPaymentFailed_When_PaymentFailed | Unit | 必須 |
| 返金 | Should_RefundPayment_When_PaymentIsCompleted | Unit | 必須 |
| 返金 | Should_ThrowBusinessException_When_PaymentNotCompleted | Unit | 必須 |
| 返金 | Should_ThrowBusinessException_When_AmountExceedsPayment | Unit | 必須 |
| Saga 補償 | Should_SkipCompensation_When_AlreadyRefunded | Unit | 必須 |
| IDOR | Should_ThrowBusinessException_When_UserNotOwner | Unit | 必須 |
| キャンセル | Should_ThrowOperationCanceled_When_TokenIsCanceled | Unit | 必須 |
| バリデーション | Should_FailValidation_When_ProductIdIsEmpty | Unit | 必須 |
| バリデーション | Should_FailValidation_When_QuantityIsOutOfRange | Unit | 必須 |
| エンドポイント | Should_Return200_When_GetCart | Integration | 必須 |
| エンドポイント | Should_Return401_When_CheckoutWithoutAuth | Integration | 必須 |
| エンドポイント | Should_Return403_When_RefundWithoutAdminRole | Integration | 必須 |
| DB Repository | Should_FindCart_When_CartExists | DB Slice | 推奨 |
| ゲストチェックアウト | Should_CreateGuestCheckout_When_ValidCartExists | Unit | 必須 |
| ゲストチェックアウト | Should_SkipCouponAndPoints_When_GuestCheckout | Unit | 必須 |
| Webhook 署名検証 | Should_RejectWebhook_When_InvalidSignature | Unit | 必須 |
| 価格計算 | Should_CalculateSubtotal_When_ProductAndQuantityProvided | Unit | 必須 |
| gRPC | Should_ReturnCartSnapshot_When_CartExists | Unit | 推奨 |
| gRPC | Should_ClearCart_When_CartIdValid | Unit | 推奨 |
| gRPC | Should_ProcessPayment_When_ValidRequest | Unit | 推奨 |
| gRPC 補償 | Should_RefundPayment_When_SagaCompensation | Unit | 推奨 |

### Phase 9 完了チェックリスト

- [ ] `dotnet test PaymentCartService.Tests/` — 全テストが Pass する
- [ ] テスト命名が `Should_期待結果_When_条件` パターンに準拠している
- [ ] AAA パターン（Arrange/Act/Assert）が全テストで使用されている
- [ ] Shouldly アサーション（`ShouldBe`, `ShouldNotBeNull`, `ShouldThrowAsync`）が使用されている
- [ ] `[Trait("Category", "Unit")]` / `[Trait("Category", "Integration")]` でカテゴリが付与されている
- [ ] モック（NSubstitute）が外部依存（Repository, Logger）に適用されている
- [ ] 異常系テストが正常系と同等以上のケース数がある
- [ ] PII（個人情報）がテストコードに含まれていない
- [ ] `dotnet test --collect:"XPlat Code Coverage"` — 分岐カバレッジ 80% 以上
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと（`NotImplementedException`, 仮のハードコード値が残存していないこと）
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディや `=> throw new NotImplementedException()` が本番コードに残存していないこと

---

## Phase 10: 可観測性（OpenTelemetry, HealthCheck, 構造化ログ）

### 目的

OpenTelemetry（分散トレーシング・メトリクス）、ヘルスチェック（Liveness / Readiness）、構造化ログ（Serilog + CompactJsonFormatter）、Correlation ID を実装する。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `PaymentCartService/Program.cs` | 更新 | OpenTelemetry, HealthCheck, Correlation ID 設定 |

### 10.1 OpenTelemetry 設定

```csharp
// Program.cs に追加
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("PaymentCartService"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource("PaymentCartService.*"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation());
```

### 10.2 ヘルスチェック

```csharp
// Program.cs に追加
using Microsoft.Extensions.Diagnostics.HealthChecks;

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql", tags: ["ready"])
    .AddRedis(
        builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379",
        name: "redis",
        tags: ["ready"]);

// Liveness（常に 200）
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

// Readiness（PostgreSQL + Redis の疎通確認）
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();
```

### 10.3 Correlation ID

```csharp
// Program.cs に追加（ミドルウェアパイプライン内）
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? Guid.NewGuid().ToString();
    context.Response.Headers.Append("X-Correlation-Id", correlationId);
    using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next();
    }
});
```

### 10.4 カスタムメトリクス

```csharp
// Program.cs または専用クラスに定義
using System.Diagnostics.Metrics;

var meter = new Meter("PaymentCartService");
var cartCreationCounter = meter.CreateCounter<long>("cart_creation_total", description: "カート作成数");
var checkoutCounter = meter.CreateCounter<long>("checkout_total", description: "チェックアウト数");
var paymentSuccessCounter = meter.CreateCounter<long>("payment_success_total", description: "決済成功数");
var paymentFailureCounter = meter.CreateCounter<long>("payment_failure_total", description: "決済失敗数");
var refundCounter = meter.CreateCounter<long>("refund_total", description: "返金数");
var stripeApiLatency = meter.CreateHistogram<double>(
    "stripe_api_latency_ms", description: "Stripe API 応答時間（ミリ秒）");
```

### 10.5 監視メトリクス一覧

| メトリクス名 | 種別 | 説明 | 警告閾値 | アラート閾値 |
|------------|------|------|---------|-----------|
| `cart_creation_total` | Counter | カート作成数/分 | > 500 | — |
| `checkout_total` | Counter | チェックアウト数/分 | > 100 | — |
| `payment_success_total` | Counter | 決済成功数 | — | — |
| `payment_failure_total` | Counter | 決済失敗数 | — | — |
| `payment_success_rate` | Gauge | 決済成功率 | < 95% | < 90% |
| `stripe_api_latency_ms` | Histogram | Stripe API 応答時間 | > 3,000 ms | > 5,000 ms |
| `refund_total` | Counter | 返金数 | — | — |
| `api_error_rate` | Gauge | API エラー率 | > 1% | > 5% |

### Phase 10 完了チェックリスト

- [ ] `dotnet build PaymentCartService/` — ビルドが成功する
- [ ] OpenTelemetry トレーシングが AspNetCore, HttpClient, EF Core に設定されている
- [ ] `/health` — Liveness エンドポイントが 200 を返す
- [ ] `/health/ready` — PostgreSQL と Redis の疎通確認を含む Readiness エンドポイント
- [ ] Correlation ID がリクエスト/レスポンスヘッダーに含まれている
- [ ] Serilog で構造化ログ（CompactJsonFormatter）が設定されている
- [ ] カスタムメトリクス（cart_creation, checkout, payment_success 等）が定義されている
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと（`NotImplementedException`, 仮のハードコード値が残存していないこと）
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディや `=> throw new NotImplementedException()` が本番コードに残存していないこと

---

## Phase 11: Docker / デプロイ準備

### 目的

マルチステージビルド Dockerfile、.dockerignore、非 root ユーザー実行、HEALTHCHECK を設定する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `PaymentCartService/Dockerfile` | 作成 | マルチステージビルド Dockerfile |
| 2 | `PaymentCartService/.dockerignore` | 作成 | Docker ビルド除外ファイル |

### 11.1 Dockerfile

```dockerfile
# Stage 1: ビルド
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["PaymentCartService/PaymentCartService.csproj", "PaymentCartService/"]
RUN dotnet restore "PaymentCartService/PaymentCartService.csproj"

COPY . .
WORKDIR "/src/PaymentCartService"
RUN dotnet publish "PaymentCartService.csproj" -c Release -o /app/publish --no-restore

# Stage 2: ランタイム
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# 非 root ユーザーの作成（必須）
RUN groupadd -r skishop && useradd -r -g skishop -d /app -s /sbin/nologin skishop
COPY --from=build --chown=skishop:skishop /app/publish .

USER skishop

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_gcServer=1

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "PaymentCartService.dll"]
```

### 11.2 .dockerignore

```
**/bin/
**/obj/
.git/
.github/
.vs/
.idea/
*.md
*.sln.DotSettings
design-docs/
impl-plan/
**/*.Tests/
```

### Phase 11 完了チェックリスト

- [ ] `docker build -t payment-cart-service -f PaymentCartService/Dockerfile .` — Docker イメージビルドが成功する
- [ ] Dockerfile がマルチステージビルドである（sdk → aspnet）
- [ ] ベースイメージタグが `latest` ではなく `10.0` に固定されている
- [ ] `USER skishop` で非 root ユーザーに切り替えている
- [ ] `HEALTHCHECK` が `/health` エンドポイントを使用している
- [ ] `.dockerignore` で `bin/`, `obj/`, `.git/`, `*.md`, `design-docs/` が除外されている
- [ ] `ASPNETCORE_ENVIRONMENT=Production` が設定されている
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと（`NotImplementedException`, 仮のハードコード値が残存していないこと）
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディや `=> throw new NotImplementedException()` が本番コードに残存していないこと

---

## Phase 12: gRPC / Saga オーケストレーション連携

### 目的

設計書 §36 準拠。SalesManagementService の SagaCoordinator から呼び出される gRPC サービス（Saga Step 1: カート情報取得、Step 6: 決済認証、Step 8: カートクリア）を実装する。Proto ファイル定義、gRPC サービス実装、DI 登録を含む。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `PaymentCartService/Protos/payment.proto` | 作成 | 決済 gRPC サービス定義（Saga Step 6） |
| 2 | `PaymentCartService/Protos/cart.proto` | 作成 | カート gRPC サービス定義（Saga Step 1/8） |
| 3 | `PaymentCartService/GrpcServices/PaymentGrpcServiceImpl.cs` | 作成 | 決済 gRPC サービス実装 |
| 4 | `PaymentCartService/GrpcServices/CartGrpcServiceImpl.cs` | 作成 | カート gRPC サービス実装 |
| 5 | `PaymentCartService/PaymentCartService.csproj` | 更新 | gRPC パッケージ追加 |
| 6 | `PaymentCartService/Program.cs` | 更新 | gRPC サービスマッピング追加 |

### 12.1 payment.proto（Saga Step 6: 決済認証 / 補償: 返金）

```protobuf
syntax = "proto3";

package skishop.payment.v1;

option csharp_namespace = "SkiShop.Contracts.Payment.V1";

service PaymentGrpcService {
  rpc ProcessPayment(ProcessPaymentRequest) returns (ProcessPaymentResponse);
  rpc RefundPayment(RefundPaymentRequest) returns (RefundPaymentResponse);
}

message ProcessPaymentRequest {
  string order_id = 1;
  string customer_id = 2;
  string payment_method = 3;
  int64 amount_minor_units = 4;
  string currency_code = 5;
  string idempotency_key = 6;
}

message ProcessPaymentResponse {
  string payment_id = 1;
  string stripe_checkout_session_id = 2;
  string checkout_url = 3;
  PaymentStatus status = 4;
}

message RefundPaymentRequest {
  string payment_id = 1;
  string reason = 2;
  string idempotency_key = 3;
}

message RefundPaymentResponse {
  string refund_id = 1;
  PaymentStatus status = 2;
  string message = 3;
}

enum PaymentStatus {
  PAYMENT_STATUS_UNSPECIFIED = 0;
  PAYMENT_STATUS_PENDING = 1;
  PAYMENT_STATUS_PROCESSING = 2;
  PAYMENT_STATUS_COMPLETED = 3;
  PAYMENT_STATUS_FAILED = 4;
  PAYMENT_STATUS_REFUNDED = 5;
  PAYMENT_STATUS_CANCELLED = 6;
}
```

### 12.2 cart.proto（Saga Step 1: カート取得 / Step 8: カートクリア）

```protobuf
syntax = "proto3";

package skishop.cart.v1;

option csharp_namespace = "SkiShop.Contracts.Cart.V1";

service CartGrpcService {
  rpc GetCartSnapshot(GetCartSnapshotRequest) returns (GetCartSnapshotResponse);
  rpc ClearCart(ClearCartRequest) returns (ClearCartResponse);
}

message GetCartSnapshotRequest {
  string cart_id = 1;
}

message GetCartSnapshotResponse {
  string cart_id = 1;
  string customer_id = 2;
  repeated CartItemSnapshot items = 3;
  int64 total_amount_minor_units = 4;
  string currency_code = 5;
}

message CartItemSnapshot {
  string product_id = 1;
  string product_name = 2;
  string sku = 3;
  int64 unit_price_minor_units = 4;
  int32 quantity = 5;
  int64 subtotal_minor_units = 6;
}

message ClearCartRequest {
  string cart_id = 1;
}

message ClearCartResponse {
  bool success = 1;
  string message = 2;
}
```

### 12.3 Program.cs 更新（gRPC 登録）

```csharp
// Program.cs に追加
builder.Services.AddGrpc();

// ── gRPC サービスマッピング ──
app.MapGrpcService<PaymentGrpcServiceImpl>();
app.MapGrpcService<CartGrpcServiceImpl>();
```

### 12.4 .csproj 更新（gRPC パッケージ追加）

```xml
<!-- PaymentCartService.csproj に追加 -->
<ItemGroup>
  <PackageReference Include="Grpc.AspNetCore" Version="2.*" />
</ItemGroup>

<ItemGroup>
  <Protobuf Include="Protos/payment.proto" GrpcServices="Server" />
  <Protobuf Include="Protos/cart.proto" GrpcServices="Server" />
</ItemGroup>
```

### Phase 12 完了チェックリスト

- [ ] `dotnet build PaymentCartService/` — ビルドが成功する
- [ ] `payment.proto` に `ProcessPayment` と `RefundPayment` RPC が定義されている
- [ ] `cart.proto` に `GetCartSnapshot` と `ClearCart` RPC が定義されている
- [ ] gRPC サービス実装で `CancellationToken` が伝搬されている
- [ ] `RefundPayment` 補償トランザクションがべき等に実装されている（既に返金済みの場合はスキップ）
- [ ] `ProcessPayment` で `idempotency_key` による重複決済防止が実装されている
- [ ] Proto ファイルの `csharp_namespace` が `SkiShop.Contracts.*.V1` に設定されている
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと（`NotImplementedException`, 仮のハードコード値が残存していないこと）
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディや `=> throw new NotImplementedException()` が本番コードに残存していないこと

---

## 横断的な規約遵守チェックリスト

### AGENTS.md 禁止事項チェック

実装完了後、以下のコマンドで禁止事項を検出する:

```bash
# 秘密情報のハードコードチェック
grep -r "Password\s*=\s*\"" --include="*.cs" PaymentCartService/
grep -r "SecretKey\s*=\s*\"" --include="*.cs" PaymentCartService/
grep -r "ApiKey\s*=\s*\"" --include="*.cs" PaymentCartService/

# Console.WriteLine チェック
grep -r "Console\.Write" --include="*.cs" PaymentCartService/

# SQL 文字列結合チェック
grep -r "FromSqlRaw.*\+" --include="*.cs" PaymentCartService/
grep -rP "FromSqlRaw\(.*\\\$" --include="*.cs" PaymentCartService/

# .Result / .Wait() チェック（デッドロックリスク）
grep -rP "\.(Result|Wait)\(\)" --include="*.cs" PaymentCartService/

# プロパティインジェクションチェック
grep -r "\[Inject\]" --include="*.cs" PaymentCartService/

# DateTime.Now チェック
grep -r "DateTime\.Now[^U]" --include="*.cs" PaymentCartService/

# new HttpClient() チェック
grep -r "new HttpClient()" --include="*.cs" PaymentCartService/

# 文字列補間ログチェック
grep -rP '_logger\.Log\w+\(\$"' --include="*.cs" PaymentCartService/

# Thread.Sleep チェック
grep -r "Thread\.Sleep" --include="*.cs" PaymentCartService/
```

### コーディング規約チェック

| # | チェック項目 | 基準 |
|---|------------|------|
| 1 | クラス名: PascalCase | `CartService`, `PaymentEndpoints` 等 |
| 2 | インターフェース: `I` プレフィックス | `ICartService`, `IPaymentRepository` 等 |
| 3 | プライベートフィールド: `_camelCase` | `_cartRepository`, `_logger` 等 |
| 4 | 非同期メソッド: `Async` サフィックス | `FindByIdAsync`, `CheckoutAsync` 等 |
| 5 | CancellationToken: 全 async メソッドに含む | `CancellationToken ct = default` |
| 6 | ログ: メッセージテンプレート形式 | `_logger.LogInformation("msg: {Param}", param)` |
| 7 | DI: primary constructor | `public class CartService(...) : ICartService` |
| 8 | DTO: record 型 | `public record CartResponse(...)` |
| 9 | Null Safety: `??`, `?.`, `ThrowIfNull` | コレクションは `= []` で初期化 |
| 10 | エンティティ: `[Table("snake_case")]`, `[Column("snake_case")]` | 全エンティティに設定 |
| 11 | 読み取りクエリ: `AsNoTracking()` | Repository の読み取りメソッド |
| 12 | 例外: 握りつぶし禁止 | `catch` 内で必ずログ出力または再スロー |
| 13 | 秘密情報: ハードコード禁止 | 環境変数 / `dotnet user-secrets` を使用 |
| 14 | TimeProvider: DI 経由 | `DateTime.UtcNow` 直接使用を避ける |
| 15 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 16 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 17 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

### テスト規約チェック

| # | チェック項目 | 基準 |
|---|------------|------|
| 1 | テスト命名 | `Should_期待結果_When_条件` パターン |
| 2 | AAA パターン | Arrange / Act / Assert のセクション分離 |
| 3 | アサーション | Shouldly（`ShouldBe`, `ShouldNotBeNull`）を使用 |
| 4 | テストカテゴリ | `[Trait("Category", "Unit")]` / `[Trait("Category", "Integration")]` |
| 5 | テスト独立性 | 各テストが単独で実行可能 |
| 6 | PII 禁止 | テストコードに本番個人情報を含まない |
| 7 | 異常系テスト | 正常系と同等以上のケース数 |
| 8 | 例外テスト | 型 + メッセージ内容を検証 |
| 9 | モック | NSubstitute による外部依存の分離 |
| 10 | カバレッジ | 分岐カバレッジ 80% 以上 |
| 11 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 12 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 13 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## フェーズ間依存関係

```
Phase 1 ─── プロジェクト基盤
    │
Phase 2 ─── エンティティ・DbContext・Migration
    │
    ├── Phase 3 ─── Repository 層
    │       │
    │       └── Phase 4 ─── Service 層
    │               │
    │               ├── Phase 5 ─── Endpoints
    │               │
    │               ├── Phase 6 ─── Kafka イベント連携
    │               │
    │               └── Phase 7 ─── Redis キャッシュ連携
    │
    ├── Phase 8 ─── 認証・認可・セキュリティ（Phase 5 と同時進行可）
    │
    ├── Phase 9 ─── テスト（Phase 4–5 完了後）
    │
    ├── Phase 10 ─── 可観測性（任意のタイミングで追加可）
    │
    └── Phase 11 ─── Docker / デプロイ準備（最終）
```

> **注記**: Phase 9（テスト）は Phase 4（Service 層）完了後から段階的に開始できる。テスト作成は各 Phase の完了後に逐次追加する運用を推奨する。

---

## 参照ドキュメント

| ドキュメント | 参照セクション |
|------------|-------------|
| `design-docs/payment-cart-service-design.md` | 全セクション（エンティティ設計、API 設計、Kafka イベント、Redis 設計、Saga 統合） |
| `design-docs/spec.md` | ADR-0005（Outbox パターン）、ADR-0006（サービス別独立 DB）、ADR-0008（PCI DSS SAQ A）、ADR-0009（Saga オーケストレーション） |
| `AGENTS.md` | §3（DDD 原則）、§4（コーディング規約）、§5（セキュリティ規約）、§10.4（CheckoutService 注文確定フロー）、§11（耐障害性・可観測性） |
| `.github/instructions/dotnet-coding-standards.instructions.md` | C# 14 コーディング規約全般 |
| `.github/instructions/security-coding.instructions.md` | OWASP Top 10 対応、入力検証、IDOR 防止 |
| `.github/instructions/api-design.instructions.md` | REST 設計、Minimal API パターン、エラーレスポンス |
| `.github/instructions/dotnet-config.instructions.md` | appsettings 設定、IOptions パターン、ミドルウェア順序 |
| `.github/instructions/nuget-dependency.instructions.md` | NuGet パッケージ管理、禁止パッケージ |
| `.github/instructions/test-standards.instructions.md` | テスト命名、AAA パターン、カバレッジ基準 |
| `.github/instructions/dockerfile-infra.instructions.md` | マルチステージビルド、非 root 実行 |
| `.github/instructions/sql-schema-review.instructions.md` | テーブル設計、インデックス設計、マイグレーション安全性 |
