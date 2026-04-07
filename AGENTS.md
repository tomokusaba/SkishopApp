# AGENTS.md — SkiShop .NET 10 マイクロサービス構築エージェント指示書

本ファイルはエージェントが SkiShop プラットフォームの構築・開発作業を実施する際に**必ず最初に読み込むドキュメント**である。全セクションを熟読した上で作業を開始すること。

---

## 1. 本プロジェクトの概要

| 項目 | 内容 |
|------|------|
| プロジェクト名 | SkiShop（スキー用品 EC サイト） |
| アーキテクチャ | マイクロサービス + イベント駆動 |
| 言語 | C# 14 |
| ランタイム | .NET 10 (LTS) |
| フレームワーク | ASP.NET Core 10 (Minimal API) |
| オーケストレーション | .NET Aspire 13.1 |
| ORM | Entity Framework Core 10 |
| DB | PostgreSQL（EF Core Migrations で管理） |
| メッセージング | Apache Kafka (Confluent.Kafka) |
| キャッシュ | Redis (StackExchange.Redis) |
| 認証 | ASP.NET Core Identity + Microsoft.Identity.Web (JWT / OAuth2 / OIDC) |
| AI 機能 | Semantic Kernel 1.x |
| ビルドツール | dotnet CLI |
| 設計書 | `design-docs/` ディレクトリ配下 |

**作業前に必ず参照するファイル一覧**:
1. `design-docs/spec.md` — システム全体設計
2. `.github/instructions/dotnet-coding-standards.instructions.md` — C# コーディング規約
3. `.github/instructions/security-coding.instructions.md` — セキュリティ規約
4. `.github/instructions/api-design.instructions.md` — Endpoints / API 設計規約
5. `.github/instructions/dotnet-config.instructions.md` — ASP.NET Core 設定規約
6. `.github/instructions/nuget-dependency.instructions.md` — NuGet 依存関係管理規約
7. `.github/instructions/test-standards.instructions.md` — テスト規約
8. `.github/instructions/dockerfile-infra.instructions.md` — Dockerfile / コンテナ設定規約
9. `.github/instructions/sql-schema-review.instructions.md` — SQL スキーマ・EF Core Migrations 規約

---

## 2. アーキテクチャの基本理解

### 2.1 レイヤー依存方向（絶対に逆転させない）

```
Endpoints（Controllers）→ Services → Repositories
                                         ↓
                                 EF Core Entity (Models/)
```

- **Endpoints / Controllers は Repository を直接呼び出さない**
- **Services は Controllers に依存しない**
- **Repositories は Services/Controllers に依存しない**

### 2.2 プロジェクト構成（各マイクロサービス共通）

```
<ServiceName>/
├── <ServiceName>.csproj
├── Program.cs                    # エントリポイント・DI 登録・ミドルウェア設定
├── Endpoints/                    # Minimal API エンドポイント定義
├── Services/                     # ビジネスロジック
│   └── Interfaces/               # Service インターフェース
├── Repositories/                 # データアクセス層
│   └── Interfaces/               # Repository インターフェース
├── Models/                       # EF Core エンティティ
├── DTOs/
│   ├── Requests/                 # リクエスト DTO (record)
│   └── Responses/                # レスポンス DTO (record)
├── Configurations/               # 設定クラス (IOptions<T>)
├── Infrastructure/
│   └── Persistence/
│       └── AppDbContext.cs       # EF Core DbContext
├── Migrations/                   # EF Core 自動生成マイグレーション
├── appsettings.json              # 共通設定（秘密情報禁止）
├── appsettings.Development.json  # 開発用設定
└── appsettings.Production.json   # 本番用（環境変数参照のみ）
```

### 2.3 マイクロサービス一覧

| サービス名 | 役割 | ポート |
|-----------|------|--------|
| `ApiGateway` | YARP ゲートウェイ、認証フィルタ、レート制限 | 8080 |
| `AuthService` | ユーザー認証・認可、JWT 発行 | 5001 |
| `UserManagementService` | ユーザープロファイル管理 | 5002 |
| `InventoryManagementService` | 商品・在庫管理 | 5003 |
| `SalesManagementService` | 注文・販売管理 | 5004 |
| `PaymentCartService` | カート・決済処理 | 5005 |
| `CouponService` | クーポン管理 | 5006 |
| `PointService` | ポイント管理 | 5007 |
| `MailSendService` | メール送信 | 5008 |
| `AiSupportService` | AI チャットボット（Semantic Kernel） | 5009 |
| `AppHost` | .NET Aspire オーケストレーション | — |

---

## 3. DDD（ドメイン駆動設計）原則

本プロジェクトは DDD の戦術パターンを採用する。全マイクロサービスで以下を遵守すること。

### 3.1 Aggregate Root

各マイクロサービスは 1 つ以上の Aggregate Root を持つ。Aggregate Root はトランザクション整合性の境界であり、外部からは Aggregate Root 経由でのみ子エンティティを操作する。

```csharp
// ✅ Order が Aggregate Root。OrderItem は Order 経由でのみ操作
public class Order  // Aggregate Root
{
    public ICollection<OrderItem> Items { get; private set; } = [];

    public void AddItem(Product product, int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        Items.Add(new OrderItem(product.Id, product.Name, product.Price, quantity));
    }
}

// ❌ 禁止: OrderItem を直接 Repository で操作
// _orderItemRepository.Add(item);  // Aggregate 境界違反
```

### 3.2 Value Object

不変の値（金額、メールアドレス、住所等）は `record` / `readonly record struct` で定義:

```csharp
// ✅ Value Object（不変、等価比較は値ベース）
public readonly record struct Money(decimal Amount, string Currency)
{
    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new BusinessException("通貨単位が異なります");
        return this with { Amount = Amount + other.Amount };
    }
}

public record EmailAddress
{
    public string Value { get; }
    public EmailAddress(string value)
    {
        if (!value.Contains('@'))
            throw new BusinessException("無効なメールアドレスです");
        Value = value;
    }
}
```

### 3.3 Domain Event

Aggregate 間の結合は Domain Event で疎結合化。直接の DB 参照・サービス呼び出しは禁止:

```csharp
// ✅ Domain Event 定義（不変 record）
public record OrderPlacedEvent(string OrderId, string UserId, decimal TotalAmount, DateTime OccurredAt);

// ✅ イベント発行は Outbox パターンで DB トランザクションと整合性を保証（§10.4 参照）
```

### 3.4 Repository パターン

Repository は Aggregate Root 単位で定義。異なる Aggregate のクエリを 1 つの Repository に混在させない:

```csharp
// ✅ 正しい: Order Aggregate 専用
public interface IOrderRepository
{
    Task<Order?> FindByIdAsync(string id, CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

// ❌ 禁止: OrderRepository に Product のクエリを含める
public interface IOrderRepository
{
    Task<Product?> FindProductByIdAsync(string id, CancellationToken ct = default);  // 境界違反
}
```

---

## 4. コーディング規約（必須遵守）

以下は `.github/instructions/dotnet-coding-standards.instructions.md` の要点。
**詳細はインストラクションファイル本体を参照すること。**

### 4.1 命名規則

| 対象 | 規約 | 正しい例 | 誤った例 |
|------|------|---------|---------|
| クラス名 | PascalCase | `UserService`, `OrderController` | `user_service` |
| インターフェース | `I` プレフィックス + PascalCase | `IUserRepository`, `IOrderService` | `UserRepository`（インターフェース） |
| メソッド | PascalCase + 動詞始まり + `Async` サフィックス | `FindByEmailAsync`, `CreateOrderAsync` | `userSearch`, `data` |
| プロパティ | PascalCase | `UserName`, `OrderItems` | `user_name` |
| ローカル変数 | camelCase | `userName`, `orderItems` | `x`, `tmp`, `Data` |
| プライベートフィールド | `_camelCase` | `_userRepository`, `_logger` | `userRepository` |
| 定数（const） | PascalCase | `MaxRetryCount`, `DefaultPageSize` | `MAX_RETRY_COUNT` |
| enum 値 | PascalCase | `OrderStatus.Pending` | `ORDER_STATUS_PENDING` |
| bool プロパティ | `Is/Has/Can/Should` プレフィックス | `IsActive`, `HasPermission` | `active`, `checkPerm` |
| コレクション変数 | 複数形 | `users`, `orderItems` | `userList`, `itemsArr` |

### 4.2 禁止事項チェックリスト（Critical / High）

| 優先度 | 禁止事項 | 代替手段 |
|--------|---------|---------|
| **Critical** | `Console.WriteLine` / `Console.Error.WriteLine` | `ILogger<T>` + `_logger.LogInformation()` |
| **Critical** | `catch (Exception) { }` 例外の握りつぶし | 必ずログ出力または再スロー |
| **Critical** | 秘密情報のハードコード（`var password = "..."` 等） | 環境変数または `dotnet user-secrets` |
| **Critical** | `FromSqlRaw` での文字列結合 SQL | EF Core LINQ / `FromSqlInterpolated` |
| **Critical** | `new UserService()` による直接インスタンス化 | DI コンテナ経由（コンストラクタインジェクション） |
| **High** | プロパティインジェクション（`[Inject]` 等） | コンストラクタインジェクション |
| **High** | null 参照の無検証使用 | null 条件演算子 `?.`、`??`、`ArgumentNullException.ThrowIfNull()` |
| **High** | ログへの個人情報出力 | マスキングまたは出力しない |
| **High** | Endpoints / Controllers が Repository を直接参照 | Service 経由 |
| **High** | Endpoints / Controllers に複雑なビジネスロジックを記述 | Service 層に移動 |
| **High** | `Thread.Sleep()` の使用 | `await Task.Delay()` |
| **High** | 非同期メソッドで `.Result` / `.Wait()` を使用 | `await` を使用 |

### 4.3 DI（依存性注入）規約

```csharp
// ✅ 正しい: primary constructor によるコンストラクタインジェクション（C# 12+、推奨）
public class AuthService(
    IUserRepository userRepository,
    ISecurityLogRepository securityLogRepository,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<UserDto> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        // ...
    }
}

// ✅ Program.cs での DI 登録（Scoped が基本）
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

// ❌ 禁止: プロパティインジェクション
public class AuthService
{
    [Inject]
    public IUserRepository UserRepository { get; set; }  // 禁止
}
```

### 4.4 C# 14 の積極的活用

| 機能 | 適用場面 | コード例 |
|------|---------|---------|
| **record 型** | DTO（リクエスト/レスポンス）の不変型定義 | `public record LoginRequest(string Email, string Password);` |
| **sealed record** | 決済結果などの限定型階層 | `public sealed record PaymentSuccess(string TransactionId) : PaymentResult;` |
| **switch 式** | ステータス変換・ルーティング | `var label = status switch { OrderStatus.Pending => "処理中", _ => "不明" };` |
| **パターンマッチング** | 型チェック・条件分岐 | `if (result is PaymentSuccess { Amount: > 0 } s) { ... }` |
| **null 条件代入** | Nullable プロパティの安全な更新 | `user.LastLoginAt ??= DateTime.UtcNow;` |
| **primary constructor** | Service / Repository の DI | `public class UserService(IUserRepository repo, ILogger<UserService> logger) { }` |
| **extension blocks** | 既存型への拡張機能追加 | `extension(string s) { public bool IsValidEmail => ... }` |

### 4.5 CancellationToken の必須化

全ての `async` メソッドのシグネチャに `CancellationToken ct = default` を含め、下位呼び出しに伝搬する:

```csharp
// ✅ 正しい: CancellationToken をシグネチャに含め、すべての下位呼び出しに伝搬
public async Task<UserDto?> FindByEmailAsync(string email, CancellationToken ct = default)
    => await _context.Users
        .AsNoTracking()
        .FirstOrDefaultAsync(u => u.Email == email, ct);

// ❌ 禁止: CancellationToken を省略
public async Task<UserDto?> FindByEmailAsync(string email)
    => await _context.Users.FirstOrDefaultAsync(u => u.Email == email);  // ct 未伝搬
```

**ルール**:
- Endpoint（Minimal API）では自動バインドされた `CancellationToken ct` を受け取り、Service → Repository へ伝搬
- `IHttpClientFactory` 経由の HTTP 呼び出しにも必ず `ct` を渡す
- `BackgroundService.ExecuteAsync` では `stoppingToken` を全下位呼び出しに伝搬

### 4.6 トランザクション管理

```csharp
// ✅ EF Core トランザクション（SaveChangesAsync が基本単位）
public async Task<Order> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct)
{
    await using var transaction = await _context.Database.BeginTransactionAsync(ct);
    try
    {
        // 複数テーブル更新...
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return order;
    }
    catch
    {
        await transaction.RollbackAsync(ct);
        throw;
    }
}

// ✅ 読み取り専用クエリ（AsNoTracking で高速化）
public async Task<List<ProductDto>> GetProductsAsync(CancellationToken ct)
    => await _context.Products
        .AsNoTracking()
        .Select(p => new ProductDto(p.Id, p.Name, p.Price))
        .ToListAsync(ct);
```

### 4.7 例外処理規約

```csharp
// ✅ 正しい例外クラス階層
// NotFoundException       → HTTP 404
// BusinessException       → HTTP 422（ビジネスルール違反）
// UnauthorizedException   → HTTP 401
// ForbiddenException      → HTTP 403
// ConcurrencyException    → HTTP 409（楽観的ロック競合）

// ✅ グローバル例外ハンドラー（Program.cs）— ILogger によるログ出力必須
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var error = feature?.Error;

        // ✅ 必須: スタックトレースを含めてログ出力（500 系のみ Error、その他は Warning）
        if (error is not (NotFoundException or BusinessException or UnauthorizedException or ForbiddenException))
            logger.LogError(error, "Unhandled exception: {Message}", error?.Message);
        else
            logger.LogWarning("Handled exception: {ExceptionType} - {Message}", error.GetType().Name, error.Message);

        var problem = error switch
        {
            NotFoundException e       => TypedResults.Problem(e.Message, statusCode: 404),
            BusinessException e       => TypedResults.Problem(e.Message, statusCode: 422),
            UnauthorizedException     => TypedResults.Problem(statusCode: 401),
            ForbiddenException        => TypedResults.Problem(statusCode: 403),
            ConcurrencyException e    => TypedResults.Problem(e.Message, statusCode: 409),
            _                         => TypedResults.Problem("内部エラーが発生しました", statusCode: 500)
        };
        await problem.ExecuteAsync(context);
    });
});
```

- `catch (Exception)` で全例外をまとめてキャッチしない
- `catch` ブロック内は必ず `_logger.LogError(ex, "message: {Message}", ex.Message)` でスタックトレースを含めてログ出力する

### 4.8 Null Safety

```csharp
// ❌ 禁止
return null;           // コレクション型の場合
list!.FirstOrDefault(); // null 強制許容演算子の乱用

// ✅ 正しい
return [];             // コレクションが空の場合（C# 12+）
return Enumerable.Empty<Product>();

var user = await _repository.FindByIdAsync(id)
    ?? throw new NotFoundException($"User {id} not found");
```

---

## 5. セキュリティ規約（最優先）

以下は `.github/instructions/security-coding.instructions.md` の要点。
セキュリティ規約の違反は他の全規約より優先的に修正する。

### 5.1 入力バリデーション

```csharp
// ✅ すべてのリクエスト DTO に Data Annotations または FluentValidation
public record LoginRequest(
    [Required, EmailAddress, StringLength(255)]
    string Email,
    [Required, StringLength(100, MinimumLength = 8)]
    string Password);

// ✅ Minimal API でのバリデーション
app.MapPost("/auth/login", async (
    [FromBody] LoginRequest request,
    IValidator<LoginRequest> validator,
    IAuthService authService) =>
{
    var validationResult = await validator.ValidateAsync(request);
    if (!validationResult.IsValid)
        return Results.ValidationProblem(validationResult.ToDictionary());
    var token = await authService.LoginAsync(request);
    return Results.Ok(new { Token = token });
}).AllowAnonymous();
```

**NG パターン一覧**:
- バリデーションなしで Service を直接呼ぶ
- ブラックリスト方式の検証（ホワイトリスト方式に変更）
- コレクションパラメータに上限なし

### 5.2 SQL インジェクション防止（絶対禁止）

```csharp
// ❌ Critical 違反: 絶対禁止
var users = await _context.Users
    .FromSqlRaw($"SELECT * FROM users WHERE email = '{email}'")  // 禁止
    .ToListAsync();

// ✅ 安全: EF Core LINQ（推奨）
var user = await _context.Users
    .FirstOrDefaultAsync(u => u.Email == email);

// ✅ 安全: FromSqlInterpolated（パラメータ化）
var user = await _context.Users
    .FromSqlInterpolated($"SELECT * FROM users WHERE email = {email}")
    .FirstOrDefaultAsync();
```

### 5.3 ASP.NET Core Security 設定の必須項目

`Program.cs` に以下を全て含めること:

```csharp
// 1. 認証・認可サービス登録
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
    // Fallback: 全エンドポイントに認証必須（AllowAnonymous で明示的に除外）
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

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

// 3. CSRF 保護（Blazor / Razor Pages 利用時）
builder.Services.AddAntiforgery();

// 4. レート制限
builder.Services.AddRateLimiter(options => { /* 設定 */ });

// 5. スタックトレースをクライアントに返さない
// appsettings.json: "DetailedErrors": false
```

### 5.4 パスワードハッシュ

```csharp
// ✅ ASP.NET Core Identity のデフォルト（PBKDF2）を使用
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ✅ カスタムハッシュが必要な場合（Argon2 等）
builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, Argon2PasswordHasher>();
```

### 5.5 管理者操作への多層防御

URL レベル認可（`RequireAuthorization`）に加え、管理者専用エンドポイントにはポリシー制限を付与:

```csharp
// ✅ Minimal API でのロールベース認可
app.MapPut("/admin/products/{id}", async (
    string id,
    AdminProductRequest request,
    IProductService svc) =>
    Results.Ok(await svc.UpdateProductAsync(id, request)))
    .RequireAuthorization("AdminOnly");
```

### 5.6 IDOR（オブジェクトレベル認可）防止

注文・住所等のユーザー固有リソースは、ログイン済みユーザーの ID と照合してオーナーシップを検証する:

```csharp
// ✅ ログインユーザーの注文のみ参照可能
app.MapGet("/orders/{id}", async (
    string id,
    ClaimsPrincipal user,
    IOrderService orderService) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedException();
    return await orderService.GetByIdAndUserIdAsync(id, userId) is { } order
        ? Results.Ok(order)
        : Results.NotFound();
}).RequireAuthorization();
```

### 5.7 PII ログ禁止

ログに出力してはいけない情報:
- `Email`（メールアドレス全文）
- `PasswordHash`（パスワード関連）
- `Address`（住所情報）
- クレジットカード番号、決済情報

`SecurityLog` には IP アドレスとイベント種別のみ記録する。

---

## 6. Endpoints / API 設計規約

以下は `.github/instructions/api-design.instructions.md` の要点。

### 6.0 OpenAPI ドキュメント生成（.NET 10 方式）

**.NET 10 では `AddOpenApi()` + `MapOpenApi()` によりサービスレベルで OpenAPI ドキュメントを自動生成するため、個別エンドポイントへの `.WithOpenApi()` は不要。**

```csharp
// ✅ .NET 10 推奨: Program.cs でのサービスレベル OpenAPI 設定
builder.Services.AddOpenApi();  // OpenAPI ドキュメント生成を有効化

var app = builder.Build();
app.MapOpenApi();  // /openapi/v1.json エンドポイントを公開

// ❌ .NET 10 では不要（禁止）: 個別エンドポイントへの .WithOpenApi()
group.MapGet("/", GetAllProducts).WithOpenApi();  // 不要
```

**ルール**:
- `Program.cs` に `builder.Services.AddOpenApi()` と `app.MapOpenApi()` を設定する
- 個別エンドポイントへの `.WithOpenApi()` チェーンは**使用しない**
- `.WithTags()` と `.WithName()` は引き続き使用可能（OpenAPI メタデータの強化に有用）
- `.Produces<T>()` / `.ProducesValidationProblem()` によるレスポンス型明示も推奨

### 6.1 URL 設計

| リソース | HTTP メソッド | エンドポイント |
|---------|-------------|-------------|
| ログイン | GET / POST | `/auth/login` |
| 商品一覧 | GET | `/products` |
| 商品詳細 | GET | `/products/{id}` |
| カート追加 | POST | `/cart/items` |
| 注文キャンセル | POST | `/orders/{id}/cancel` |
| 管理: 商品一覧 | GET | `/admin/products` |
| 管理: 商品更新 | PUT | `/admin/products/{id}` |
| 管理: 商品削除 | DELETE | `/admin/products/{id}` |

- URI は名詞（複数形）で構成し、動詞を含めない
- REST 原則に従う（GET は副作用なし、POST/PUT/DELETE は適切に区別）

### 6.2 Minimal API エンドポイント実装パターン

```csharp
// ✅ 推奨: エンドポイントを専用クラスに分離（IEndpointRouteBuilder 拡張メソッド）
public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products")
            .WithTags("Products");

        group.MapGet("/", GetAllProducts).WithName("GetProducts");
        group.MapGet("/{id}", GetProductById).WithName("GetProductById");
        group.MapPost("/", CreateProduct)
            .RequireAuthorization("AdminOnly")
            .WithName("CreateProduct");
    }

    private static async Task<IResult> GetAllProducts(
        [AsParameters] ProductQueryParams query,
        IProductService service,
        CancellationToken ct)
        => Results.Ok(await service.GetAllAsync(query, ct));

    private static async Task<IResult> GetProductById(
        string id,
        IProductService service,
        CancellationToken ct)
        => await service.GetByIdAsync(id, ct) is { } product
            ? Results.Ok(product)
            : Results.NotFound();
}

// Program.cs
app.MapProductEndpoints();
```

### 6.3 エラーレスポンス

RFC 9457（Problem Details）準拠のエラーレスポンスを返す:

```csharp
// ✅ TypedResults.Problem を使用
return TypedResults.Problem(
    detail: "指定された商品が存在しません",
    statusCode: 404,
    title: "Not Found");
```

- スタックトレースをクライアントに返さない（`appsettings.json` で `"DetailedErrors": false`）

---

## 7. ASP.NET Core 設定ファイル規約

以下は `.github/instructions/dotnet-config.instructions.md` の要点。

### 7.1 プロファイル別ファイル構成

```
<ServiceName>/
├── appsettings.json                 # 共通（安全なデフォルト値・スキーマ定義のみ）
├── appsettings.Development.json     # 開発（ローカル PostgreSQL / Debug ログ）
├── appsettings.Staging.json         # ステージング（PostgreSQL / Information ログ）
└── appsettings.Production.json      # 本番（環境変数参照 / Warning ログ）
```

### 7.2 必須設定項目（appsettings.json 共通）

```json
{
  "AllowedHosts": "*",
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "SkiShop": "Information"
    }
  },
  "DetailedErrors": false,
  "Kestrel": {
    "AddServerHeader": false
  }
}
```

### 7.3 秘密情報管理

```jsonc
// ❌ Critical 違反: appsettings.json に直接記述
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Password=mysecret"  // 禁止
  }
}

// ✅ 開発時: dotnet user-secrets を使用
// dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;..."

// ✅ 本番: 環境変数で参照
// 環境変数名: ConnectionStrings__DefaultConnection
```

---

## 8. NuGet 依存関係管理規約

以下は `.github/instructions/nuget-dependency.instructions.md` の要点。

### 8.1 必須パッケージ（各 .csproj）

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
    <PackageReference Include="Microsoft.Identity.Web" Version="3.*" />

    <!-- バリデーション（FluentValidation.AspNetCore は 11.x で非推奨） -->
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

    <!-- ヘルスチェック -->
    <PackageReference Include="AspNetCore.HealthChecks.NpgSql" Version="9.*" />
    <PackageReference Include="AspNetCore.HealthChecks.Redis" Version="9.*" />
  </ItemGroup>

  <!-- テスト（テストプロジェクトのみ） -->
  <ItemGroup Condition="'$(IsTestProject)' == 'true'">
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="NSubstitute" Version="5.*" />
    <PackageReference Include="Shouldly" Version="4.*" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.*" />
    <PackageReference Include="Testcontainers.PostgreSql" Version="4.*" />
    <PackageReference Include="coverlet.collector" Version="6.*" />
  </ItemGroup>
</Project>
```

### 8.2 禁止パッケージ・避けるべき慣行

| 禁止・非推奨 | 理由 | 代替 |
|------------|------|------|
| `System.Web` | .NET Framework 専用 | ASP.NET Core |
| `log4net` / `NLog`（直接使用） | `ILogger<T>` 抽象を破る | Serilog + `ILogger<T>` |
| `EntityFramework`（EF6） | 旧世代 ORM | Entity Framework Core 10 |
| `FromSqlRaw` での文字列結合 | SQL インジェクションリスク | EF Core LINQ / `FromSqlInterpolated` |
| `WebClient` / `HttpWebRequest` | 非推奨 | `HttpClient` / `IHttpClientFactory` |
| `-preview` / `-beta` / `-rc` パッケージ | 不安定 | GA（正式リリース）を使用 |
| `Newtonsoft.Json`（新規実装） | 旧世代 | `System.Text.Json`（SDK 同梱） |

---

## 9. テスト規約

以下は `.github/instructions/test-standards.instructions.md` の要点。

### 9.1 テスト種別と対応アプローチ

| テスト種別 | フレームワーク | 対象 |
|---------|-------------|------|
| Unit Test | xUnit + NSubstitute + Shouldly | Service, Utility クラス |
| Integration Test（API） | `WebApplicationFactory<Program>` | Minimal API エンドポイント |
| DB スライステスト | Testcontainers.PostgreSql | Repository（実 PostgreSQL） |
| セキュリティテスト | `WebApplicationFactory` + カスタム `AuthenticationHandler` | 認証/認可 |
| E2E テスト | Microsoft.Playwright | ブラウザ操作 |

### 9.2 テストメソッド命名（必須）

```csharp
// ✅ Should_期待結果_When_条件 パターン
[Fact]
public async Task Should_ReturnUser_When_ValidEmailProvided()
{
    // Arrange / Act / Assert
}

[Fact]
public async Task Should_ThrowNotFoundException_When_UserDoesNotExist()
{
    // ...
}
```

### 9.3 AAA パターン（Arrange-Act-Assert）

```csharp
[Fact]
public async Task Should_FindUser_When_EmailExists()
{
    // Arrange
    var expectedUser = new User { Email = "test@example.com" };
    _userRepository.FindByEmailAsync("test@example.com", default)
        .Returns(expectedUser);

    // Act
    var result = await _authService.FindByEmailAsync("test@example.com");

    // Assert
    result.ShouldNotBeNull();
    result.Email.ShouldBe("test@example.com");
}
```

### 9.4 カバレッジ目標

必須基準: **分岐カバレッジ 80% 以上**

| レイヤー | 目標 | 備考 |
|---------|------|------|
| Service | 80% 以上 | 必須（ビジネスロジック中心） |
| Endpoints | 80% 以上 | `WebApplicationFactory` で統合テスト |
| Repository | 70% 以上 | Testcontainers で実 DB テスト推奨 |
| 全体 | 80% 以上 | `dotnet test --collect:"XPlat Code Coverage"` で確認 |

---

## 10. 実装上の重要知識

### 10.1 マイクロサービス間通信パターン

#### Service・Repository 実装パターン

```csharp
// ✅ Service インターフェース定義
public interface IAuthService
{
    Task<string> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<UserDto?> FindByEmailAsync(string email, CancellationToken ct = default);
}

// ✅ Service 実装（primary constructor）
public class AuthService(
    IUserRepository userRepository,
    IPasswordHasher<User> passwordHasher,
    IJwtTokenService jwtTokenService,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<string> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await userRepository.FindByEmailAsync(request.Email, ct)
            ?? throw new NotFoundException("ユーザーが見つかりません");

        if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
            == PasswordVerificationResult.Failed)
            throw new UnauthorizedException("認証情報が無効です");

        return jwtTokenService.GenerateToken(user);
    }
}

// ✅ Repository インターフェース（1 Aggregate Root の原則）
public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> FindByIdAsync(string id, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

// ✅ Repository 実装（EF Core）
public class UserRepository(AppDbContext context) : IUserRepository
{
    public async Task<User?> FindByEmailAsync(string email, CancellationToken ct = default)
        => await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, ct);
}
```

#### DTO (record) パターン

```csharp
// ✅ リクエスト DTO（不変 record + バリデーション）
public record LoginRequest(
    [Required, EmailAddress, StringLength(255)]
    string Email,
    [Required, StringLength(100, MinimumLength = 8)]
    string Password);

// ✅ レスポンス DTO（不変 record）
public record UserDto(string Id, string Email, string Role, DateTime CreatedAt);
```

#### 設定クラスパターン（`IOptions<T>`）

```csharp
// ✅ 設定クラス（appsettings.json と対応）
public record MailSettings(string Host, int Port, string FromAddress);

// Program.cs
builder.Services.Configure<MailSettings>(
    builder.Configuration.GetSection("Mail"));

// Service での使用
public class MailService(IOptions<MailSettings> mailOptions, ILogger<MailService> logger)
{
    private readonly MailSettings _settings = mailOptions.Value;
}
```

### 10.2 カート・セッション管理パターン

```csharp
// ✅ 未ログイン時: Cookie にカート ID を格納（Cart エンティティは DB に保存）
app.MapPost("/cart/items", async (
    HttpContext httpContext,
    [FromBody] AddCartItemRequest request,
    ICartService cartService) =>
{
    var cartId = httpContext.Request.Cookies["CartId"]
        ?? Guid.NewGuid().ToString();
    await cartService.AddItemAsync(cartId, request);
    httpContext.Response.Cookies.Append("CartId", cartId,
        new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict });
    return Results.Ok();
}).AllowAnonymous();

// ✅ ログイン成功時: カートをユーザーにマージ
// IClaimsTransformation または AuthenticationSuccessHandler で実装
```

### 10.3 EF Core エンティティ作成の必須ルール

```csharp
[Table("users")]
public class User
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("email")]
    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Column("password_hash")]
    [Required]
    [MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // コレクションナビゲーションは = [] で初期化（C# 12+）
    public ICollection<Order> Orders { get; set; } = [];
}
```

**必須チェック項目**:
- `DateTime.Now`（ローカル時刻）は使用禁止 → `DateTime.UtcNow` または `DateTimeOffset.UtcNow` を使用
- 全プロパティに `[Column("snake_case_name")]` でカラム名を明示
- コレクションナビゲーションは `= []` で初期化（null 防止）
- `LazyLoading` は無効のまま使用し、`Include()` / `ThenInclude()` で明示的 Eager Loading
- 楽観的ロックが必要なエンティティには `[Timestamp]` プロパティを追加:

```csharp
// ✅ 楽観的ロック（Optimistic Concurrency）
[Table("products")]
public class Product
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // ... 他のプロパティ ...

    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];
}

// ✅ DbUpdateConcurrencyException のハンドリング
try
{
    await _context.SaveChangesAsync(ct);
}
catch (DbUpdateConcurrencyException ex)
{
    _logger.LogWarning(ex, "楽観的ロック競合: {EntityType}", ex.Entries.FirstOrDefault()?.Entity.GetType().Name);
    throw new ConcurrencyException("データが他のユーザーによって更新されました。再度お試しください。");
}
```

### 10.4 CheckoutService の注文確定フロー

> **⚠️ SSOT（Single Source of Truth）**: 注文確定フローの設計詳細は **`design-docs/spec.md`（ADR-0009: 注文確定フローのアーキテクチャ方針）** を唯一の正とする。本セクションでは実装時に参照すべきポイントのみを記載する。設計の変更は spec.md に対して行い、本セクションとの二重管理を禁止する。

**アーキテクチャ方針**: ADR-0006（サービス別独立 DB）に基づき、注文確定は **Saga オーケストレーション（gRPC + Outbox パターン）** で実装する。単一の EF Core トランザクションで複数サービスの DB を操作することは技術的に不可能である。

**実装上の必須ルール**（spec.md の設計に従って実装する際の規約）:

| ルール | 内容 |
|--------|------|
| **Saga 設計の参照先** | `design-docs/spec.md` の ADR-0009 セクション（Saga ステップ定義・補償設計・Deadline） |
| **OutboxEvent エンティティ** | `[Table("outbox_events")]` で定義。カラム名は snake_case。ステータス値は UPPER_CASE（`PENDING`, `PUBLISHED`, `FAILED`） |
| **補償トランザクション** | 各補償ステップはべき等であること。専用の `CancellationToken`（30 秒タイムアウト）を使用 |
| **BackgroundService** | `OutboxPublisher` は `IServiceScopeFactory` で Scoped サービスを取得。`stoppingToken` を全下位呼び出しに伝搬 |
| **Outbox Polling** | 動的バックオフ（100ms〜5s）。固定間隔 1 秒は禁止 |
| **コード規約** | primary constructor、`TimeProvider` DI、`CancellationToken ct = default`、`ILogger<T>` メッセージテンプレート |

### 10.5 .NET Aspire オーケストレーション

```csharp
// AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin()
    .AddDatabase("skishopdb");

var redis = builder.AddRedis("redis").WithRedisInsight();
var kafka = builder.AddKafka("kafka");

var authService = builder.AddProject<Projects.AuthService>("auth-service")
    .WithReference(postgres)
    .WithReference(redis);

var inventoryService = builder
    .AddProject<Projects.InventoryManagementService>("inventory-service")
    .WithReference(postgres)
    .WithReference(kafka);

builder.AddProject<Projects.ApiGateway>("api-gateway")
    .WithReference(authService)
    .WithReference(inventoryService);

builder.Build().Run();
```

### 10.6 Kafka イベント発行・購読パターン

```csharp
// ✅ イベント発行（Producer）
public class OrderEventPublisher(
    IProducer<string, string> producer,
    ILogger<OrderEventPublisher> logger)
{
    public async Task PublishOrderCreatedAsync(OrderCreatedEvent @event)
    {
        var message = new Message<string, string>
        {
            Key = @event.OrderId,
            Value = JsonSerializer.Serialize(@event)
        };
        await producer.ProduceAsync("order.created", message);
        logger.LogInformation("OrderCreated イベント発行: {OrderId}", @event.OrderId);
    }
}

// ✅ イベント購読（Consumer）は BackgroundService で実装
// IServiceScopeFactory でスコープを生成し、Scoped サービスを安全に利用
public class OrderCreatedConsumer(
    IConsumer<string, string> consumer,
    IServiceScopeFactory scopeFactory,
    ILogger<OrderCreatedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        consumer.Subscribe("order.created");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var @event = JsonSerializer.Deserialize<OrderCreatedEvent>(result.Message.Value);
                if (@event is not null)
                {
                    using var scope = scopeFactory.CreateScope();
                    var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
                    await orderService.ProcessOrderCreatedAsync(@event, stoppingToken);
                }
                consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume エラー: {Topic}", ex.ConsumerRecord?.Topic);
                // Dead Letter Topic への転送を検討
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "イベント処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);  // バックオフ
            }
        }
    }
}
```

---

## 11. 耐障害性・可観測性・ミドルウェア設計

### 11.1 耐障害性（Resilience）パターン

全ての外部 HTTP 通信に `IHttpClientFactory` + `Microsoft.Extensions.Http.Resilience` を適用する:

```csharp
// ✅ Program.cs での HttpClient 登録（Polly v8 統合）
builder.Services.AddHttpClient<IInventoryClient, InventoryClient>(client =>
{
    client.BaseAddress = new Uri("https://inventory-service");
})
.AddStandardResilienceHandler(options =>
{
    // リトライ: 指数バックオフ（最大 3 回）
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.Delay = TimeSpan.FromMilliseconds(500);

    // サーキットブレーカー: 10 秒の遮断期間
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.MinimumThroughput = 10;

    // タイムアウト: 10 秒
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
});

// ❌ 禁止: new HttpClient() を直接使用
var client = new HttpClient();  // DI 管理外・リトライなし
```

**必須ルール**:
- `IHttpClientFactory` 経由のみ使用（`new HttpClient()` 禁止）
- 全外部 HTTP 呼び出しに `AddStandardResilienceHandler` を適用
- 高負荷サービスには Bulkhead（同時実行制限）を追加検討
- フォールバック戦略（キャッシュ応答、デフォルト値等）をサービスごとに定義

### 11.2 可観測性（Observability）

#### OpenTelemetry 統合

```csharp
// ✅ Program.cs での OpenTelemetry 設定
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource("SkiShop.*"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation());
```

#### ヘルスチェック

全サービスに `/health`（Liveness）と `/health/ready`（Readiness）を実装:

```csharp
// ✅ Program.cs
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql", tags: ["ready"])
    .AddRedis(redisConnectionString, name: "redis", tags: ["ready"]);

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false  // Liveness: 常に 200
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

#### 構造化ログ（Serilog）

```csharp
// ✅ Program.cs — Serilog 設定
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "AuthService")
        .WriteTo.Console(new CompactJsonFormatter()));

// ✅ ログ出力はメッセージテンプレート形式（文字列補間禁止）
_logger.LogInformation("Order created: {OrderId}, User: {UserId}", orderId, userId);

// ❌ 禁止: 文字列補間
_logger.LogInformation($"Order created: {orderId}");  // 構造化ログが壊れる
```

#### Correlation ID

全リクエストに相関 ID を付与し、マイクロサービス間で伝搬:

```csharp
// ✅ ミドルウェアで Correlation ID を付与
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? Guid.NewGuid().ToString();
    context.Response.Headers.Append("X-Correlation-Id", correlationId);
    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next();
    }
});
```

### 11.3 ミドルウェアパイプライン順序（厳守）

ASP.NET Core のミドルウェアは**登録順序が動作に直結**する。以下の順序を厳守すること:

```csharp
// ✅ Program.cs — ミドルウェア登録順序（この順序を変更しない）
var app = builder.Build();

// 1. 例外ハンドラー（最も外側で全例外をキャッチ）
app.UseExceptionHandler();

// 2. セキュリティヘッダー
app.UseHsts();
app.UseHttpsRedirection();

// 3. Correlation ID ミドルウェア（ログに相関 ID を付与）
app.UseCorrelationId();  // カスタムミドルウェア

// 4. Serilog リクエストログ
app.UseSerilogRequestLogging();

// 5. CORS（認証より前に配置）
app.UseCors();

// 6. 認証・認可（この順序は絶対）
app.UseAuthentication();
app.UseAuthorization();

// 7. レート制限（認証後に配置し、ユーザー単位の制限を可能に）
app.UseRateLimiter();

// 8. エンドポイントマッピング
app.MapProductEndpoints();
app.MapAuthEndpoints();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");

app.Run();
```

**禁止パターン**:
- `UseAuthentication()` を `UseAuthorization()` の後に配置する
- `UseExceptionHandler()` をパイプラインの途中に配置する
- `UseCors()` を `UseAuthentication()` の後に配置する

---

## 12. フェーズ実行の注意事項

### 12.1 各フェーズの完了条件

各フェーズ終了時に **必ず** 以下を確認すること:

| フェーズ | 最低限の確認コマンド |
|---------|-------------------|
| Phase 1（基盤構築） | `dotnet build` |
| Phase 2（エンティティ） | `dotnet build` + `dotnet ef migrations add Initial` 成功確認 |
| Phase 3（Repository） | `dotnet test --filter Category=Repository` |
| Phase 4（Service） | `dotnet test --filter Category=Service` |
| Phase 5（Endpoints） | `dotnet test --filter Category=Endpoints` |
| Phase 6（統合テスト） | `dotnet test --filter Category=Integration` |
| Phase 7（Security） | セキュリティテスト全件通過 |
| Phase 8（Test） | `dotnet test --collect:"XPlat Code Coverage"` + カバレッジ 80% 確認 |
| Phase 9（最終） | `dotnet publish` + Docker イメージビルド成功 |

### 12.2 自動生成・ツール使用時の確認事項

コード自動生成や Scaffold を使用した場合は必ず以下を確認する:
1. プロパティインジェクション（`[Inject]` 等）が生成されていないか
2. `Console.WriteLine` が残っていないか
3. `async` メソッドで `.Result` / `.Wait()` が使用されていないか
4. ハードコードされた接続文字列や秘密情報がないか
5. `FromSqlRaw` での文字列結合 SQL がないか

### 12.3 DB 操作上の注意

- **EF Core Migrations を使用**。`dotnet ef migrations add <Name>` でマイグレーション生成
- マイグレーションファイルは `/Migrations/` に自動生成。必ずコミット対象とすること
- テスト環境では `Testcontainers.PostgreSql` を使用（InMemory Provider は PostgreSQL 方言非対応）
- カラム名は `snake_case`、テーブル名は `snake_case` 複数形（`[Table]` / `[Column]` 属性で明示）
- SQL 内の命名規則: テーブル名は `snake_case` 複数形、カラム名は `snake_case`（`sql-schema-review.instructions.md` 準拠）

### 12.4 セキュリティ上の絶対禁止事項

事前確認コマンド（コードレビュー前に実施）:
```bash
# 秘密情報のハードコードチェック
grep -r "Password\s*=\s*\"" --include="*.cs" src/

# Console.WriteLine チェック
grep -r "Console\.Write" --include="*.cs" src/

# SQL 文字列結合チェック
grep -r "FromSqlRaw.*\+" --include="*.cs" src/

# .Result / .Wait() チェック（デッドロックリスク）
grep -rP "\.(Result|Wait)\(\)" --include="*.cs" src/

# プロパティインジェクションチェック
grep -r "\[Inject\]" --include="*.cs" src/
```

### 12.5 Dockerfile 規約

以下は `.github/instructions/dockerfile-infra.instructions.md` の要点。Phase 9（最終）の Docker イメージビルド時に遵守すること。

```dockerfile
# ✅ 正しい: マルチステージビルド + 非 root + バージョン固定
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ServiceName/ServiceName.csproj", "ServiceName/"]
RUN dotnet restore "ServiceName/ServiceName.csproj"
COPY . .
WORKDIR "/src/ServiceName"
RUN dotnet publish "ServiceName.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# 非 root ユーザーの作成（必須）
RUN groupadd -r skishop && useradd -r -g skishop -d /app skishop
COPY --from=build --chown=skishop:skishop /app/publish .
USER skishop

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "ServiceName.dll"]
```

**必須チェック項目**:
- ベースイメージのタグは `latest` 禁止 → `10.0` 等の固定バージョンを指定
- ランタイムイメージは `aspnet`（ASP.NET Core ランタイムのみ）を使用し `sdk` は含めない
- `USER` 命令で非 root ユーザーに切り替え（root 実行禁止）
- `HEALTHCHECK` を必ず設定（`/health` エンドポイントを利用）
- `.dockerignore` で `bin/`, `obj/`, `.git/`, `*.md` を除外し、イメージサイズを最小化

---

## 13. よくあるミスと対策

| ミス | 原因 | 対策 |
|------|------|------|
| `ObjectDisposedException` | DbContext が Dispose された後にナビゲーションプロパティをアクセス | `Include()` / `ThenInclude()` で明示的 Eager Loading |
| デッドロック | `.Result` / `.Wait()` による同期コンテキストのブロッキング | 全て `await` を使用 |
| CORS エラー | CORS ポリシー未設定 | `builder.Services.AddCors()` + `app.UseCors()` を適切に設定 |
| JWT 検証失敗 | 時刻のずれ / クロック・スキュー | `ClockSkew = TimeSpan.FromMinutes(5)` を設定 |
| N+1 クエリ | コレクションナビゲーション未 Include | `Include()` / `AsSplitQuery()` / `AsNoTracking()` で対策 |
| Testcontainers が CI で失敗 | Docker が利用できない CI 環境 | CI 環境に Docker インストール済みか確認 |
| 循環依存（DI） | Service が互いに注入し合っている | 依存関係を見直す。`Lazy<T>` は一時回避のみ |
| テスト用設定に本番値 | `appsettings.Development.json` の混用 | テスト設定はすべてダミー値 / `dotnet user-secrets` を使用 |
| EF Core Migration が適用されない | DbContext が正しく登録されていない | `dotnet ef database update` の出力を確認 |

---

## 14. ファイル探索ガイド

| 知りたいこと | 参照先 |
|------------|--------|
| システム全体設計・マイクロサービス一覧 | `design-docs/spec.md` |
| API ゲートウェイ設計 | `design-docs/api-gateway-design.md` |
| 認証サービス設計 | `design-docs/authentication-service-design.md` |
| 在庫管理設計 | `design-docs/inventory-management-design.md` |
| 販売管理設計 | `design-docs/sales-management-design.md` |
| 決済・カート設計 | `design-docs/payment-cart-service-design.md` |
| クーポン設計 | `design-docs/coupon-service-design.md` |
| ポイント設計 | `design-docs/point-service-design.md` |
| AI サポート設計 | `design-docs/ai-support-service-design.md` |
| メール送信設計 | `design-docs/mailsend-service-design.md` |
| ユーザー管理設計 | `design-docs/user-management-design.md` |
| フロントエンド要件 | `design-docs/front-end-need.md` |
| 追加実装計画 | `design-docs/additional*.md` |
