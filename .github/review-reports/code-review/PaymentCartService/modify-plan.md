# PaymentCartService 修正計画書

## 概要

本ドキュメントは `check-report-1.md` で報告された全ての課題（High 2件、Medium 21件、Low 23件）に対する詳細な修正計画を記載する。

---

## 修正優先度マトリクス

| 優先度 | 件数 | 対応タイミング |
|--------|------|---------------|
| **High** | 2 | 即時対応（リリース前必須） |
| **Medium** | 21 | 次回リファクタリング時 |
| **Low** | 23 | 時間がある時 |

---

## High 指摘の修正（必須対応）

### H-1: テストプロジェクトが存在しない

**出典**: test-quality-reviewer  
**重要度**: High  
**概要**: テストプロジェクトが存在せず、分岐カバレッジ 80% の基準を達成できていない

#### 修正方針

新規テストプロジェクト `PaymentCartService.Tests` を作成し、主要なコンポーネントの単体テストを実装する。

#### 作成ファイル一覧

```
Services/PaymentCartService.Tests/
├── PaymentCartService.Tests.csproj
├── GlobalUsings.cs
├── Services/
│   ├── CartServiceTests.cs
│   ├── PaymentServiceTests.cs
│   ├── RefundServiceTests.cs
│   ├── PriceServiceTests.cs
│   ├── CartCacheServiceTests.cs
│   └── StripeGatewayTests.cs
├── Endpoints/
│   ├── CartEndpointsTests.cs
│   ├── PaymentEndpointsTests.cs
│   └── GuestCheckoutEndpointsTests.cs
├── Repositories/
│   ├── CartRepositoryTests.cs
│   └── PaymentRepositoryTests.cs
├── GrpcServices/
│   ├── CartGrpcServiceImplTests.cs
│   └── PaymentGrpcServiceImplTests.cs
├── Validators/
│   └── ValidatorTests.cs
└── Fixtures/
    ├── WebApplicationFactoryFixture.cs
    └── TestDbContextFixture.cs
```

#### PaymentCartService.Tests.csproj

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
    <ProjectReference Include="..\PaymentCartService\PaymentCartService.csproj" />
  </ItemGroup>
</Project>
```

#### CartServiceTests.cs（サンプル）

```csharp
using NSubstitute;
using Shouldly;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using PaymentCartService.Services;
using PaymentCartService.Services.Interfaces;
using PaymentCartService.Repositories.Interfaces;
using PaymentCartService.Configurations;
using PaymentCartService.Models;
using PaymentCartService.Models.Enums;
using PaymentCartService.DTOs.Requests;
using PaymentCartService.Exceptions;

namespace PaymentCartService.Tests.Services;

public class CartServiceTests
{
    private readonly ICartRepository _cartRepository;
    private readonly ICartCacheService _cacheService;
    private readonly IOptions<CartSettings> _cartOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CartService> _logger;
    private readonly CartService _sut;

    public CartServiceTests()
    {
        _cartRepository = Substitute.For<ICartRepository>();
        _cacheService = Substitute.For<ICartCacheService>();
        _cartOptions = Options.Create(new CartSettings { ExpiryDays = 7, MaxItemsPerCart = 50 });
        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        _logger = Substitute.For<ILogger<CartService>>();
        _sut = new CartService(_cartRepository, _cacheService, _cartOptions, _timeProvider, _logger);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnCart_When_CartExistsAndActive()
    {
        // Arrange
        var cartId = Guid.NewGuid().ToString();
        var cart = new Cart { Id = cartId, Status = CartStatus.Active, SessionId = "session-1" };
        _cartRepository.FindByIdWithItemsAsync(cartId, default).Returns(cart);
        _cacheService.GetCartAsync(cartId, default).Returns((Cart?)null);

        // Act
        var result = await _sut.GetCartAsync(cartId);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(cartId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowNotFoundException_When_CartDoesNotExist()
    {
        // Arrange
        var cartId = Guid.NewGuid().ToString();
        _cartRepository.FindByIdWithItemsAsync(cartId, default).Returns((Cart?)null);
        _cacheService.GetCartAsync(cartId, default).Returns((Cart?)null);

        // Act & Assert
        var act = async () => await _sut.GetCartAsync(cartId);
        var ex = await Should.ThrowAsync<NotFoundException>(act);
        ex.Message.ShouldContain("カートが見つかりません");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_CreateNewCart_When_CartIdIsNull()
    {
        // Arrange
        var sessionId = "session-123";
        _cartRepository.FindBySessionIdAsync(sessionId, default).Returns((Cart?)null);

        // Act
        var result = await _sut.GetOrCreateCartAsync(null, sessionId);

        // Assert
        result.ShouldNotBeNull();
        result.SessionId.ShouldBe(sessionId);
        await _cartRepository.Received(1).AddAsync(Arg.Any<Cart>(), default);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_MaxItemsReached()
    {
        // Arrange
        var cartId = Guid.NewGuid().ToString();
        var cart = new Cart { Id = cartId, Status = CartStatus.Active, SessionId = "session-1" };
        // 50 items already added (max)
        for (int i = 0; i < 50; i++)
        {
            cart.AddItem($"product-{i}", $"Product {i}", $"SKU-{i}", 100m, 1);
        }
        _cartRepository.FindByIdWithItemsAsync(cartId, default).Returns(cart);

        var request = new AddCartItemRequest("product-51", "Product 51", "SKU-51", 100m, 1);

        // Act & Assert
        var act = async () => await _sut.AddItemAsync(cartId, request);
        var ex = await Should.ThrowAsync<BusinessException>(act);
        ex.Message.ShouldContain("上限");
    }
}
```

---

### H-2: Stripe Webhook タイムスタンプ検証が不十分

**出典**: security-reviewer  
**重要度**: High  
**対象ファイル**: `Services/PaymentService.cs` (HandleWebhookAsync メソッド)

#### 問題点

`EventUtility.ConstructEvent` は内部でタイムスタンプ検証を行うが、tolerance（許容時間差）の設定が明示的に行われていない。デフォルトは 300 秒（5分）だが、設定ファイルの `WebhookToleranceSeconds` が反映されていない。

#### 修正方針

**案 1**: EventUtility.ConstructEvent に tolerance パラメータを明示的に渡す（推奨）

> **技術補足**: Stripe SDK の `EventUtility.ConstructEvent` メソッドの `tolerance` パラメータは `long` 型（秒数を直接渡す）であり、`TimeSpan` や文字列ではない点に注意。

```csharp
// PaymentService.cs の HandleWebhookAsync メソッド

public async Task HandleWebhookAsync(string json, string signature, CancellationToken ct = default)
{
    // tolerance は long 型で秒数を直接渡す
    var stripeEvent = EventUtility.ConstructEvent(
        json, 
        signature, 
        _stripeSettings.WebhookSecret,
        tolerance: _paymentSettings.WebhookToleranceSeconds,  // long 型（秒）
        throwOnApiVersionMismatch: false);

    logger.LogInformation("Stripe Webhook 受信: EventType={EventType}, EventId={EventId}",
        stripeEvent.Type, stripeEvent.Id);

    // ... 以降の処理
}
```

**案 2**: カスタムタイムスタンプ検証を追加（より厳密な制御が必要な場合）

```csharp
public async Task HandleWebhookAsync(string json, string signature, CancellationToken ct = default)
{
    var stripeEvent = EventUtility.ConstructEvent(
        json, signature, _stripeSettings.WebhookSecret);

    // カスタムタイムスタンプ検証
    var eventTimestamp = DateTimeOffset.FromUnixTimeSeconds(stripeEvent.Created);
    var now = timeProvider.GetUtcNow();
    var tolerance = TimeSpan.FromSeconds(_paymentSettings.WebhookToleranceSeconds);
    
    if (now - eventTimestamp > tolerance)
    {
        logger.LogWarning(
            "Stripe Webhook タイムスタンプ超過: EventId={EventId}, EventTime={EventTime}, Now={Now}",
            stripeEvent.Id, eventTimestamp, now);
        throw new BusinessException("Webhook イベントが古すぎます", "PAY-4100");
    }

    // ... 以降の処理
}
```

**案 3**: throwOnApiVersionMismatch パラメータと合わせて詳細設定（最も堅牢）

```csharp
public async Task HandleWebhookAsync(string json, string signature, CancellationToken ct = default)
{
    try
    {
        var stripeEvent = EventUtility.ConstructEvent(
            json, 
            signature, 
            _stripeSettings.WebhookSecret,
            tolerance: _paymentSettings.WebhookToleranceSeconds,
            throwOnApiVersionMismatch: true);

        // イベント処理...
    }
    catch (StripeException ex) when (ex.Message.Contains("timestamp"))
    {
        logger.LogWarning(ex, "Stripe Webhook タイムスタンプ検証失敗: {Message}", ex.Message);
        throw new BusinessException("Webhook 署名が無効または期限切れです", ex, "PAY-4100");
    }
}
```

#### 採用案: 案 1（推奨）

**理由**: 
- Stripe SDK の標準機能を活用し、既存の実装への影響を最小限に抑える
- 設定ファイルの `WebhookToleranceSeconds` を明示的に使用することで設定の意図が明確
- 例外処理は既存の `try-catch (StripeException)` で対応済み

#### 修正内容

**ファイル**: `Services/PaymentService.cs`  
**行番号**: 161-181

```csharp
// 修正前
public async Task HandleWebhookAsync(string json, string signature, CancellationToken ct = default)
{
    var stripeEvent = EventUtility.ConstructEvent(
        json, signature, _stripeSettings.WebhookSecret);
    // ...
}

// 修正後
public async Task HandleWebhookAsync(string json, string signature, CancellationToken ct = default)
{
    var stripeEvent = EventUtility.ConstructEvent(
        json, 
        signature, 
        _stripeSettings.WebhookSecret,
        tolerance: _paymentSettings.WebhookToleranceSeconds,
        throwOnApiVersionMismatch: false);

    logger.LogInformation("Stripe Webhook 受信: EventType={EventType}, EventId={EventId}, Tolerance={ToleranceSeconds}s",
        stripeEvent.Type, stripeEvent.Id, _paymentSettings.WebhookToleranceSeconds);
    // ...
}
```

---

## Medium 指摘の修正（推奨対応）

### M-1: CartGrpcServiceImpl が Repository を直接参照

**出典**: architecture-reviewer  
**対象ファイル**: `GrpcServices/CartGrpcServiceImpl.cs`

#### 修正方針

`ICartRepository` を `ICartService` 経由に変更し、レイヤードアーキテクチャの依存方向を遵守する。

#### 修正内容

```csharp
// 修正前
public class CartGrpcServiceImpl(
    ICartRepository cartRepository,
    ILogger<CartGrpcServiceImpl> logger) : CartGrpcService.CartGrpcServiceBase

// 修正後
public class CartGrpcServiceImpl(
    ICartService cartService,
    ILogger<CartGrpcServiceImpl> logger) : CartGrpcService.CartGrpcServiceBase
{
    public override async Task<GetCartSnapshotResponse> GetCartSnapshot(
        GetCartSnapshotRequest request,
        ServerCallContext callContext)
    {
        var ct = callContext.CancellationToken;
        logger.LogInformation("gRPC GetCartSnapshot: CartId={CartId}", request.CartId);

        var cart = await cartService.GetCartAsync(request.CartId, ct);
        // cart は CartResponse 型で返されるため、適切にマッピング

        var response = new GetCartSnapshotResponse
        {
            CartId = cart.Id,
            CustomerId = cart.CustomerId ?? string.Empty,
            TotalAmountMinorUnits = (long)(cart.TotalAmount * 100),
            CurrencyCode = "JPY"
        };

        foreach (var item in cart.Items)
        {
            response.Items.Add(new CartItemSnapshot
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Sku = item.Sku ?? string.Empty,
                UnitPriceMinorUnits = (long)(item.UnitPrice * 100),
                Quantity = item.Quantity,
                SubtotalMinorUnits = (long)(item.Subtotal * 100)
            });
        }

        return response;
    }

    public override async Task<ClearCartResponse> ClearCart(
        ClearCartRequest request,
        ServerCallContext callContext)
    {
        var ct = callContext.CancellationToken;
        logger.LogInformation("gRPC ClearCart: CartId={CartId}", request.CartId);

        try
        {
            await cartService.ClearCartAsync(request.CartId, ct);
            return new ClearCartResponse
            {
                Success = true,
                Message = "カートをクリアしました"
            };
        }
        catch (NotFoundException)
        {
            // べき等応答
            return new ClearCartResponse
            {
                Success = true,
                Message = "カートは既にクリア済みです"
            };
        }
    }
}
```

---

### M-2: Cart.Items の EF Core ナビゲーション連動

**出典**: ddd-domain-reviewer  
**対象ファイル**: `Models/Cart.cs`

#### 問題点

`_items` フィールドが `IReadOnlyCollection<CartItem>` として公開されているが、EF Core の `HasField("_items")` 設定との連動が確実でない可能性がある。

#### 修正方針

`_items` フィールドの初期化を EF Core のマテリアライゼーションに対応させる。

```csharp
// 修正後
[Table("carts")]
public class Cart : IHasTimestamps
{
    // ... 既存プロパティ ...

    // EF Core がナビゲーションをロードできるよう、バッキングフィールドを調整
    private List<CartItem> _items = [];
    
    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();

    // EF Core マテリアライゼーション用（必要な場合）
    // protected Cart() { }
}
```

**AppDbContext.cs の確認**: 既に `entity.Navigation(c => c.Items).HasField("_items");` が設定されているため、追加修正は不要。ただし、Cart エンティティのコンストラクタが EF Core の要件を満たしているか確認。

---

### M-3: Payment.Transactions の EF Core ナビゲーション連動

**出典**: ddd-domain-reviewer  
**対象ファイル**: `Models/Payment.cs`

M-2 と同様の対応。`_transactions` フィールドの初期化方法を確認。

---

### M-4: カートエンドポイントの IDOR リスク

**出典**: api-endpoint-reviewer / security-reviewer  
**対象ファイル**: `Endpoints/CartEndpoints.cs`

#### 問題点

カートエンドポイントが `AllowAnonymous()` で公開されており、セッション ID のみでカートを特定している。他ユーザーのカートにアクセス可能なリスクがある。

#### 修正方針の検討

**案 1**: セッション ID を Cookie で管理（現状維持 + 検証強化）

- カート ID は Cookie（HttpOnly, Secure, SameSite=Strict）で管理
- カート操作時に Cookie のカート ID とリクエストのカート ID を照合
- **利点**: 未ログインユーザーでもカート機能を利用可能（ECサイトの標準的な要件）
- **欠点**: Cookie を持っていれば誰でもアクセス可能（ただし推測は困難）

**案 2**: セッション ID + 追加検証（推奨）

- 現状の Cookie ベースの仕組みを維持
- カート操作時に `sessionId`（接続 ID ではなくユーザーセッション）を検証
- **`httpContext.Connection.Id` の使用を廃止**し、代わりに Cookie ベースのセッショントークンを使用

**案 3**: 認証必須化

- カート機能を認証ユーザーのみに限定
- **欠点**: ゲストユーザーのカート機能が使えなくなる（ECサイトとしては致命的）

#### 採用案: 案 2（推奨）

**修正内容**:

1. **`httpContext.Connection.Id` の使用を廃止**: 接続ごとに変わるため不適切
2. **セッショントークン Cookie を導入**: 永続的なセッション識別子として使用

```csharp
// CartEndpoints.cs
private static async Task<IResult> GetCart(
    HttpContext httpContext,
    ICartService cartService,
    CancellationToken ct)
{
    var cartId = httpContext.Request.Cookies["CartId"];
    
    // Connection.Id ではなく、既存の CartId Cookie または新規生成した UUID を使用
    var sessionToken = httpContext.Request.Cookies["SessionToken"];
    if (string.IsNullOrEmpty(sessionToken))
    {
        sessionToken = Guid.NewGuid().ToString();
        httpContext.Response.Cookies.Append("SessionToken", sessionToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromDays(365)
        });
    }

    var cart = await cartService.GetOrCreateCartAsync(cartId, sessionToken, ct);
    SetCartCookie(httpContext, cart.Id);
    return Results.Ok(cart);
}
```

---

### M-5: OpenAPI 設定の追加

**出典**: api-endpoint-reviewer  
**対象ファイル**: `Program.cs`

#### 修正内容

```csharp
// Program.cs に追加（builder.Services の設定部分）

// ── OpenAPI ──
builder.Services.AddOpenApi();

// ... 既存の設定 ...

var app = builder.Build();

// ミドルウェアパイプライン
app.UseExceptionHandler();
// ...

// OpenAPI エンドポイント（ヘルスチェックの前に追加）
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
```

---

### M-6: CartCacheService の TTL ハードコード

**出典**: csharp-standards-reviewer  
**対象ファイル**: `Services/CartCacheService.cs`

#### 修正内容

```csharp
// 修正前
private static readonly TimeSpan CartTtl = TimeSpan.FromDays(7);
private static readonly DistributedCacheEntryOptions CartCacheOptions = new()
{
    AbsoluteExpirationRelativeToNow = CartTtl
};

// 修正後
public class CartCacheService(
    IDistributedCache cache,
    IOptions<CartSettings> cartOptions,
    ILogger<CartCacheService> logger) : ICartCacheService
{
    private readonly CartSettings _settings = cartOptions.Value;
    
    private DistributedCacheEntryOptions CreateCacheOptions()
        => new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(_settings.ExpiryDays) };

    public async Task SetCartAsync(Cart cart, CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(cart);
            var options = CreateCacheOptions();
            await cache.SetStringAsync($"cart:{cart.Id}", json, options, ct);
            // ...
        }
        // ...
    }
}
```

---

### M-7: StripeGateway の SessionService インスタンス化

**出典**: async-concurrency-reviewer  
**対象ファイル**: `Services/StripeGateway.cs`

#### 修正方針の検討

**案 1**: IStripeClient を DI でインジェクション（推奨）

```csharp
// Program.cs に追加
builder.Services.AddSingleton<Stripe.IStripeClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<StripeSettings>>().Value;
    return new Stripe.StripeClient(settings.SecretKey);
});

// StripeGateway.cs
public class StripeGateway(
    Stripe.IStripeClient stripeClient,
    ResiliencePipelineProvider<string> resilienceProvider,
    ILogger<StripeGateway> logger) : IStripeGateway
{
    public async Task<Session> CreateCheckoutSessionAsync(
        SessionCreateOptions options, CancellationToken ct = default)
    {
        var pipeline = resilienceProvider.GetPipeline("stripe");
        return await pipeline.ExecuteAsync(async token =>
        {
            var service = new SessionService(stripeClient);
            return await service.CreateAsync(options, cancellationToken: token);
        }, ct);
    }
}
```

**案 2**: Stripe API Key を設定で初期化（現状の方式を維持）

```csharp
// Program.cs に追加（既存の Stripe 設定後）
Stripe.StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];
```

#### 採用案: 案 1（推奨）

**理由**: DI によりテスタビリティが向上し、IStripeClient のモックが可能になる。

---

### M-8: PaymentService のトランザクションロールバックログ

**出典**: error-logging-reviewer  
**対象ファイル**: `Services/PaymentService.cs`

#### 修正内容

```csharp
// 修正前（CheckoutAsync メソッド内）
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

// 修正後
catch (StripeException ex)
{
    logger.LogWarning("トランザクションロールバック実行: PaymentId={PaymentId}", payment?.Id);
    await transaction.RollbackAsync(ct);
    logger.LogError(ex, "Stripe API エラー: {Message}", ex.Message);
    throw new BusinessException("決済処理に失敗しました", ex, "PAY-4223");
}
catch (Exception ex)
{
    logger.LogWarning(ex, "トランザクションロールバック実行（予期しないエラー）: PaymentId={PaymentId}", payment?.Id);
    await transaction.RollbackAsync(ct);
    throw;
}
```

---

### M-9: PaymentRepository の不要な Include

**出典**: data-access-reviewer  
**対象ファイル**: `Repositories/PaymentRepository.cs`

#### 修正方針

Transactions が不要なケースでもロードされるため、メソッドを分離する。

```csharp
// 修正後
public class PaymentRepository(AppDbContext context) : IPaymentRepository
{
    public async Task<Payment?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.Payments
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Payment?> FindByIdWithTransactionsAsync(string id, CancellationToken ct = default)
        => await context.Payments
            .Include(p => p.Transactions)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    // ... 他のメソッド
}
```

**IPaymentRepository インターフェースにも追加**:
```csharp
Task<Payment?> FindByIdWithTransactionsAsync(string id, CancellationToken ct = default);
```

---

### M-10: マイグレーションファイルの生成

**出典**: data-access-reviewer

#### 実行コマンド

```bash
cd Services/PaymentCartService
dotnet ef migrations add Initial --context AppDbContext
```

---

### M-11: appsettings.json の秘密情報プレースホルダコメント

**出典**: config-di-reviewer  
**対象ファイル**: `appsettings.json`

#### 修正内容

```json
{
  "AllowedHosts": "*",
  "DetailedErrors": false,
  "Kestrel": {
    "AddServerHeader": false
  },
  "ConnectionStrings": {
    "DefaultConnection": "",
    "Redis": ""
  },
  "_comment_secrets": "秘密情報は dotnet user-secrets または環境変数で管理してください。Jwt:Key, Stripe:SecretKey, Stripe:WebhookSecret は appsettings に直接記述しないでください。",
  "Jwt": {
    "Issuer": "SkiShop",
    "Audience": "SkiShop.Client",
    "Key": ""
  },
  "Stripe": {
    "SecretKey": "",
    "WebhookSecret": "",
    "SuccessUrl": "https://localhost:5173/checkout/success",
    "CancelUrl": "https://localhost:5173/checkout/cancel"
  },
  // ... 既存の設定
}
```

---

### M-12: GuestCheckoutEndpoints の cartId 優先順位

**出典**: security-reviewer  
**対象ファイル**: `Endpoints/GuestCheckoutEndpoints.cs`

#### 修正内容

```csharp
// 修正前
var cartId = httpContext.Request.Cookies["CartId"]
    ?? request.CartId;

// 修正後（Cookie を優先し、明示的にログ出力）
private static async Task<IResult> GuestCheckout(
    HttpContext httpContext,
    GuestCheckoutRequest request,
    IValidator<GuestCheckoutRequest> validator,
    IPaymentService paymentService,
    ILogger<Program> logger,
    CancellationToken ct)
{
    var validationResult = await validator.ValidateAsync(request, ct);
    if (!validationResult.IsValid)
        return Results.ValidationProblem(validationResult.ToDictionary());

    var cookieCartId = httpContext.Request.Cookies["CartId"];
    var requestCartId = request.CartId;
    
    // Cookie を優先（セキュリティ上、Cookie は HttpOnly で保護されているため）
    var cartId = cookieCartId ?? requestCartId;
    
    if (string.IsNullOrWhiteSpace(cartId))
        return Results.BadRequest("カート ID が特定できません");

    if (cookieCartId is not null && requestCartId is not null && cookieCartId != requestCartId)
    {
        logger.LogWarning(
            "カート ID 不一致: Cookie={CookieCartId}, Request={RequestCartId}. Cookie を優先します",
            cookieCartId, requestCartId);
    }

    var result = await paymentService.GuestCheckoutAsync(cartId, request, ct);
    return Results.Ok(result);
}
```

---

### M-13: CartEndpoints の sessionId 問題

**出典**: security-reviewer  
**対象ファイル**: `Endpoints/CartEndpoints.cs`

M-4 で対応済み。`httpContext.Connection.Id` の使用を廃止し、Cookie ベースのセッショントークンに変更。

---

### M-14: CartService.MapToResponse の LINQ 実行

**出典**: performance-reviewer  
**対象ファイル**: `Services/CartService.cs`

#### 修正方針

頻繁に呼ばれるため、ToList() の呼び出しを最小化する。現状の実装は問題ないが、パフォーマンスが問題になる場合は以下を検討:

```csharp
// 現状維持（LINQ は遅延評価されるため、ToList() は一度だけ実行される）
// パフォーマンス問題が発生した場合、キャッシュされた CartResponse を使用する
```

**備考**: 現状の実装は十分効率的。パフォーマンス問題が計測された場合のみ対応。

---

### M-15: OutboxPublisher のインデックス確認

**出典**: performance-reviewer  
**対象ファイル**: `Infrastructure/Persistence/AppDbContext.cs`

#### 確認結果

既に以下のインデックスが設定されている:
```csharp
entity.HasIndex(e => new { e.Status, e.CreatedAt })
    .HasDatabaseName("idx_outbox_events_pending")
    .HasFilter("status = 'Pending'");
```

**対応**: 不要（既に適切なインデックスが存在）

---

### M-16: Polly パイプラインにサーキットブレーカー追加

**出典**: resilience-reviewer  
**対象ファイル**: `Program.cs`

#### 修正内容

```csharp
// 修正前
builder.Services.AddResiliencePipeline("stripe", pipelineBuilder =>
{
    pipelineBuilder
        .AddRetry(new Polly.Retry.RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromMilliseconds(500),
            ShouldHandle = new PredicateBuilder().Handle<Stripe.StripeException>()
        })
        .AddTimeout(TimeSpan.FromSeconds(30));
});

// 修正後
builder.Services.AddResiliencePipeline("stripe", pipelineBuilder =>
{
    pipelineBuilder
        .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 10,
            BreakDuration = TimeSpan.FromSeconds(30),
            ShouldHandle = new PredicateBuilder().Handle<Stripe.StripeException>()
        })
        .AddRetry(new Polly.Retry.RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromMilliseconds(500),
            ShouldHandle = new PredicateBuilder().Handle<Stripe.StripeException>()
        })
        .AddTimeout(TimeSpan.FromSeconds(30));
});
```

---

### M-17〜M-18: Service 層・gRPC サービスの単体テスト

H-1 のテストプロジェクト作成で対応。

---

### M-19〜M-21: エンティティの IHasTimestamps 実装

**出典**: tech-lead  
**対象ファイル**: 
- `Models/PaymentMethod.cs`
- `Models/Transaction.cs`  
- `Models/CartItem.cs`

#### 修正内容

```csharp
// PaymentMethod.cs
[Table("payment_methods")]
public class PaymentMethod : IHasTimestamps
{
    // ... 既存のプロパティ（CreatedAt, UpdatedAt は既に存在）
}

// Transaction.cs
[Table("transactions")]
public class Transaction : IHasTimestamps
{
    // ... 既存のプロパティ（CreatedAt, UpdatedAt は既に存在）
}

// CartItem.cs
[Table("cart_items")]
public class CartItem : IHasTimestamps
{
    // ... 既存のプロパティ
    
    // 名前変更: AddedAt → CreatedAt（IHasTimestamps との整合性）
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

**注意**: CartItem の `AddedAt` を `CreatedAt` に変更する場合、以下のファイルも修正が必要:
- `DTOs/Responses/CartItemResponse.cs`
- `Services/CartService.cs` (MapToResponse メソッド)

#### DB マイグレーション対応

`CartItem.AddedAt` → `CartItem.CreatedAt` への変更に伴い、DB カラム名も `added_at` → `created_at` に変更する必要がある。

**方法 1**: EF Core マイグレーションで自動生成（推奨）

```bash
cd Services/PaymentCartService
dotnet ef migrations add RenameCartItemAddedAtToCreatedAt --context AppDbContext
```

生成されたマイグレーションファイルを確認し、以下のような `RenameColumn` が含まれていることを確認:

```csharp
migrationBuilder.RenameColumn(
    name: "added_at",
    table: "cart_items",
    newName: "created_at");
```

**方法 2**: 手動 SQL（既存データがある本番環境向け）

```sql
-- PostgreSQL
ALTER TABLE cart_items RENAME COLUMN added_at TO created_at;
```

**API レスポンス互換性**: `CartItemResponse` の `AddedAt` プロパティ名を `CreatedAt` に変更すると、API レスポンスの JSON フィールド名も変わる（`addedAt` → `createdAt`）。フロントエンドとの互換性を確認すること。

---

## Low 指摘の修正（時間がある時）

### L-1〜L-23 の対応一覧

| # | 指摘 | 対象ファイル | 対応内容 |
|---|------|------------|----------|
| 1 | Events/ ディレクトリの確認 | Infrastructure/Kafka/ | ファイル拡張子の確認（.cs が付いているか） |
| 2 | 未使用インターフェース | Services/Interfaces/ | IShippingFeeCalculator, ITaxCalculator を将来実装するか削除するか判断 |
| 3 | Money Value Object の活用 | Models/Payment.cs | `decimal Amount` を `Money` 型に変更 |
| 4 | ShippingAddress の永続化 | Models/ValueObjects/ | 設計書との整合性を確認 |
| 5 | Proto パッケージ名の整合性 | Protos/*.proto | C# 名前空間との一貫性確認 |
| 6 | Response DTO のドキュメントコメント | DTOs/Responses/ | XML ドキュメントコメント追加 |
| 7 | Settings クラスの Data Annotations | Configurations/ | `[Required]` 等を追加 |
| 8 | BackgroundService のバックオフ設定 | Infrastructure/Kafka/ | 定数を設定ファイルから読み込む |
| 9 | Kafka Consumer のブロッキング | Infrastructure/Kafka/ | 非同期対応は Confluent.Kafka の制限のため現状維持 |
| 10 | GlobalExceptionHandler の 503 ログ | Infrastructure/ | ExternalServiceException の詳細ログ追加 |
| 11 | PaymentMethod 用 Repository | Repositories/ | 必要に応じて作成 |
| 12 | OutboxEvent.AggregateId インデックス | AppDbContext.cs | インデックス追加 |
| 13 | appsettings.Production.json 設定追加 | appsettings.Production.json | 本番用設定の充実 |
| 14 | Dockerfile curl vs wget | Dockerfile | Alpine 移行時に対応 |
| 15 | Microsoft.Identity.Web 追加 | .csproj | 設計書との整合性確認後に追加 |
| 16 | Testcontainers 統合テスト | Tests/ | H-1 で対応 |
| 17 | FindByIdWithItemsAsync の AsNoTracking | Repositories/ | 更新が必要なため現状維持 |
| 18 | Stripe API キャッシュ | Services/ | 将来検討 |
| 19 | DLT 転送 | Infrastructure/Kafka/ | 将来検討 |
| 20 | Redis フォールバック | Services/CartCacheService.cs | DB フォールバック追加 |
| 21 | Validator の ErrorCode | Validators/ | `WithErrorCode()` 追加 |
| 22 | OpenTelemetry HttpClient | Program.cs | `AddHttpClientInstrumentation()` 追加 |
| 23 | Dockerfile --no-restore | Dockerfile | フラグ追加 |

---

## 修正実行順序

### Phase 1: 必須対応（High）

1. H-2: PaymentService の Webhook タイムスタンプ検証修正
2. H-1: テストプロジェクト作成（基本構造のみ）

### Phase 2: アーキテクチャ・セキュリティ（Medium 高優先）

3. M-1: CartGrpcServiceImpl の Service 層経由化
4. M-4/M-13: CartEndpoints のセッション ID 修正
5. M-12: GuestCheckoutEndpoints の cartId 優先順位明確化
6. M-16: Polly サーキットブレーカー追加
7. M-5: OpenAPI 設定追加

### Phase 3: コード品質（Medium 中優先）

8. M-6: CartCacheService の TTL ハードコード修正
9. M-7: StripeGateway の DI 化
10. M-8: PaymentService のロールバックログ追加
11. M-9: PaymentRepository の Include 分離
12. M-19〜M-21: エンティティの IHasTimestamps 実装

### Phase 4: 設定・インフラ（Medium 低優先）

13. M-10: マイグレーションファイル生成
14. M-11: appsettings.json コメント追加

### Phase 5: テスト拡充（継続）

15. H-1: テストプロジェクトの単体テスト実装
16. M-17〜M-18: Service / gRPC テスト追加

### Phase 6: Low 指摘対応（時間がある時）

17. L-7: Settings クラスの Data Annotations
18. L-12: OutboxEvent.AggregateId インデックス
19. L-21: Validator の ErrorCode
20. L-22: OpenTelemetry HttpClient Instrumentation
21. L-23: Dockerfile --no-restore
22. その他の Low 指摘

---

## 修正完了後の確認事項

### 必須確認項目

1. `dotnet build` が成功すること
2. `dotnet ef migrations add Initial` が成功すること（または `RenameCartItemAddedAtToCreatedAt` 等の追加マイグレーション）
3. `dotnet test` が成功すること（テストプロジェクト作成後）
4. 分岐カバレッジが 80% 以上であること

### 機能確認項目

5. gRPC サービス（`CartGrpcServiceImpl`）が `ICartService` 経由で正常に動作すること
6. セッション Cookie（`SessionToken`）が正しく発行・維持されること
7. Stripe Webhook のタイムスタンプ検証が設定値（`WebhookToleranceSeconds = 300`）で動作すること
8. Redis キャッシュが `CartSettings.ExpiryDays` に基づいて TTL 設定されること
9. Polly サーキットブレーカーが Stripe 障害時に正常にオープンすること

### API 互換性確認項目

10. `CartItemResponse` の `AddedAt` → `CreatedAt` 変更がフロントエンドに影響しないこと（または調整済みであること）

---

*本修正計画書は check-report-1.md の全課題に対応するものです。*

---

## 更新履歴

| 日付 | 更新内容 |
|------|---------|
| 2026-04-06 | 初版作成 |
| 2026-04-06 | H-2 の技術補足追加（tolerance パラメータは long 型）、M-19〜M-21 の DB マイグレーション対応追記、修正完了後の確認事項拡充 |
