---
applyTo: "**/Endpoints/**/*.cs"
---

# API 設計 Instructions

本 Instructions は `**/Endpoints/**/*.cs` に自動適用される。Minimal API エンドポイントの作成・編集時に以下のチェック観点を遵守すること。

---

## 1. REST 設計原則

### URI 設計
- リソース指向の URI 設計（**名詞を使用、動詞を避ける**）
- コレクションリソースには**複数形**を使用（`/users`, `/orders`）
- ネストは 2 階層までに制限（`/users/{userId}/orders` は可、`/users/{userId}/orders/{orderId}/items/{itemId}` は避ける）
- URI にファイル拡張子を含めない（`.json`, `.xml`）

```csharp
// ✅ 良い例: Minimal API のルート定義
var group = app.MapGroup("/products")
    .WithTags("Products");

group.MapGet("/", GetAllProducts);
group.MapGet("/{id}", GetProductById);

// ❌ 悪い例
app.MapGet("/getProducts", ...);       // 動詞を使用
app.MapGet("/product", ...);           // 単数形
app.MapGet("/product_list", ...);      // スネークケース
```

### HTTP メソッド
- 適切な HTTP メソッドを使用し、意味論を厳守する

| メソッド | 用途 | べき等性 | 安全性 | 成功時ステータス |
|---------|------|---------|--------|----------------|
| **GET** | リソースの取得 | ✅ | ✅ | 200（データあり）/ 204（データなし） |
| **POST** | リソースの作成 | ❌ | ❌ | 201 + `Location` ヘッダー |
| **PUT** | リソースの全置換 | ✅ | ❌ | 200 または 204 |
| **PATCH** | リソースの部分更新 | ❌ | ❌ | 200 |
| **DELETE** | リソースの削除 | ✅ | ❌ | 204（ボディなし） |

- **GET リクエストでリソースの状態を変更しない**（安全性の保証）
- **PUT / DELETE はべき等でなければならない**（同一リクエストの複数回実行で結果が同一）

### HTTP ステータスコード
- 意味的に正しいステータスコードを返す。全て 200 で返すことは禁止

| コード | 用途 | 使用場面 |
|--------|------|---------|
| **200** | 成功（ボディあり） | GET / PUT / PATCH の正常応答 |
| **201** | 作成成功 | POST でリソース作成成功時。`Location` ヘッダーに新リソースの URI を含める |
| **204** | 成功（ボディなし） | DELETE 成功時、PUT でボディ不要時 |
| **400** | 不正なリクエスト | バリデーションエラー、不正なリクエストボディ |
| **401** | 未認証 | 認証情報なし、または認証失敗 |
| **403** | 権限不足 | 認証済みだがリソースへのアクセス権限がない |
| **404** | リソース未検出 | 指定された ID のリソースが存在しない |
| **409** | 競合 | 楽観的ロックの競合、一意制約違反 |
| **422** | 処理不能 | ビジネスルール違反（バリデーションは通過するが業務的に処理不能） |
| **429** | レート制限超過 | 一定期間内のリクエスト数が上限を超過 |
| **500** | サーバーエラー | 予期しない例外。**スタックトレースをクライアントに返さない** |

---

## 2. エラーレスポンス設計

### RFC 9457 Problem Details 形式の必須化
- 全てのエラーレスポンスは **RFC 9457 Problem Details** 形式で統一する
- ASP.NET Core の `TypedResults.Problem()` / `Results.Problem()` を活用する

```json
// ✅ RFC 9457 準拠のエラーレスポンス
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "指定された商品が存在しません",
  "instance": "/products/123"
}
```

### バリデーションエラーのレスポンス
- フィールド単位のエラー詳細を含める
- 全フィールドのエラーを**一括で返す**（1 つずつ返す逐次方式は禁止）
- `Results.ValidationProblem()` を使用する

```csharp
// ✅ バリデーションエラーレスポンス
var validationResult = await validator.ValidateAsync(request, ct);
if (!validationResult.IsValid)
    return Results.ValidationProblem(validationResult.ToDictionary());
```

### エラーレスポンスの禁止事項
- **スタックトレースをクライアントに返さない**（`"DetailedErrors": false`）
- **内部実装の詳細を漏洩しない**（SQL エラーメッセージ、クラス名、内部パス等）
- **開発用エラーページを本番で表示しない**。`UseExceptionHandler` で統一的に処理する

```csharp
// ✅ UseExceptionHandler による統一例外処理（Program.cs）
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var error = feature?.Error;

        if (error is not (NotFoundException or BusinessException))
            logger.LogError(error, "Unhandled exception: {Message}", error?.Message);

        var problem = error switch
        {
            NotFoundException e    => TypedResults.Problem(e.Message, statusCode: 404),
            BusinessException e    => TypedResults.Problem(e.Message, statusCode: 422),
            UnauthorizedException  => TypedResults.Problem(statusCode: 401),
            ForbiddenException     => TypedResults.Problem(statusCode: 403),
            ConcurrencyException e => TypedResults.Problem(e.Message, statusCode: 409),
            _                      => TypedResults.Problem("内部エラーが発生しました", statusCode: 500)
        };
        await problem.ExecuteAsync(context);
    });
});
```

---

## 3. 入力検証

### バリデーション必須化
- **全ての外部入力に対してバリデーションを実施する**
- FluentValidation の `IValidator<T>` を DI で注入し、エンドポイント内で明示的に実行する
- リクエスト DTO には Data Annotations（`[Required]`, `[StringLength]`, `[EmailAddress]` 等）を付与する

```csharp
// ✅ 入力検証の実装例（Minimal API）
app.MapPost("/users", async (
    [FromBody] CreateUserRequest request,
    IValidator<CreateUserRequest> validator,
    IUserService userService,
    CancellationToken ct) =>
{
    var validationResult = await validator.ValidateAsync(request, ct);
    if (!validationResult.IsValid)
        return Results.ValidationProblem(validationResult.ToDictionary());

    var user = await userService.CreateAsync(request, ct);
    return Results.Created($"/users/{user.Id}", user);
}).AllowAnonymous();

// ✅ リクエスト DTO（record + Data Annotations）
public record CreateUserRequest(
    [Required(ErrorMessage = "名前は必須です")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "名前は1〜50文字で入力してください")]
    string Name,

    [Required(ErrorMessage = "メールアドレスは必須です")]
    [EmailAddress(ErrorMessage = "有効なメールアドレスを入力してください")]
    string Email);
```

### サイズ制限
- リクエストボディの上限を Kestrel で設定する
- コレクションパラメータに `[MaxLength]` で上限を設定する

---

## 4. レスポンス設計

### レスポンス DTO の分離
- EF Core エンティティを直接レスポンスとして返さない。**レスポンス専用の DTO（record 推奨）を使用する**

```csharp
// ❌ エンティティを直接返却
app.MapGet("/users/{id}", async (string id, AppDbContext context) =>
    await context.Users.FindAsync(id));

// ✅ レスポンス DTO を使用
app.MapGet("/users/{id}", async (
    string id,
    IUserService userService,
    CancellationToken ct) =>
    await userService.FindByIdAsync(id, ct) is { } user
        ? Results.Ok(user)
        : Results.NotFound())
    .RequireAuthorization();
```

### ページネーション
- コレクションリソースには**ページネーションを必須化**する
- 上限なしの全件取得エンドポイントは禁止
- `[AsParameters]` でクエリパラメータをバインドする

```csharp
// ✅ ページネーション対応
public record ProductQueryParams(
    int Page = 1,
    [Range(1, 100)] int PageSize = 20,
    string? SortBy = "CreatedAt",
    bool Descending = true);

app.MapGet("/products", async (
    [AsParameters] ProductQueryParams query,
    IProductService service,
    CancellationToken ct) =>
    Results.Ok(await service.GetAllAsync(query, ct)));
```

### 日時フォーマット
- 日時は **ISO 8601 形式**（`2026-03-18T14:30:00Z`）で統一する
- タイムゾーン情報を必ず含める（UTC 推奨）
- `DateTime.UtcNow` または `DateTimeOffset.UtcNow` を使用する

---

## 5. Minimal API エンドポイント実装パターン

### エンドポイントを専用クラスに分離
- `IEndpointRouteBuilder` の拡張メソッドでエンドポイントを定義する
- `MapGroup` でグループ化し、共通設定（認可、タグ、OpenAPI）を適用する

```csharp
// ✅ 推奨: エンドポイントを専用クラスに分離
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

    private static async Task<IResult> CreateProduct(
        [FromBody] CreateProductRequest request,
        IValidator<CreateProductRequest> validator,
        IProductService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var product = await service.CreateAsync(request, ct);
        return Results.Created($"/products/{product.Id}", product);
    }
}

// Program.cs
app.MapProductEndpoints();
```

---

## 6. べき等性とリトライ安全性

- **PUT / DELETE はべき等に設計する**（同一リクエストの複数回実行で同一結果）
- POST のリトライ安全性が必要な場合、**べき等性キー**（`Idempotency-Key` ヘッダー）の設計を検討する

---

## 7. Endpoint の責務

### Endpoint にビジネスロジックを書かない
- Endpoint の役割は**リクエストの受付・バリデーション・レスポンスの構築のみ**
- ビジネスロジックは Service 層に委譲する

```csharp
// ❌ Endpoint にビジネスロジック
app.MapPost("/orders", async ([FromBody] CreateOrderRequest request, AppDbContext context) =>
{
    var totalPrice = request.Items.Sum(item => item.Price * item.Quantity);
    if (totalPrice > 1000000)
        throw new BusinessException("上限金額を超過しています");
    // ...
});

// ✅ Service に委譲
app.MapPost("/orders", async (
    [FromBody] CreateOrderRequest request,
    IValidator<CreateOrderRequest> validator,
    IOrderService orderService,
    CancellationToken ct) =>
{
    var validationResult = await validator.ValidateAsync(request, ct);
    if (!validationResult.IsValid)
        return Results.ValidationProblem(validationResult.ToDictionary());

    var order = await orderService.CreateAsync(request, ct);
    return Results.Created($"/orders/{order.Id}", order);
}).RequireAuthorization();
```

---

## 8. セキュリティ関連（Endpoint 層のみ）

- 全エンドポイントに**認可設定**（`.RequireAuthorization()` / `.AllowAnonymous()`）を明示する
- **CORS 設定**で `AllowAnyOrigin()` を使用しない。許可オリジンを明示する
- **レート制限**が必要なエンドポイント（認証、検索等）を識別し、`UseRateLimiter()` で対策する

```csharp
// ✅ 認可設定の明示
app.MapDelete("/products/{id}", async (
    string id,
    IProductService service,
    CancellationToken ct) =>
{
    await service.DeleteAsync(id, ct);
    return Results.NoContent();
}).RequireAuthorization("AdminOnly");

// ✅ 公開エンドポイントの明示
app.MapGet("/health", () => Results.Ok(new { Status = "UP" }))
    .AllowAnonymous();
```

---

## 9. API ドキュメント（.NET 10 OpenAPI）

### .NET 10 のサービスレベル OpenAPI

**.NET 10 では `AddOpenApi()` + `MapOpenApi()` によりサービスレベルで OpenAPI ドキュメントを自動生成するため、個別エンドポイントへの `.WithOpenApi()` は不要。**

```csharp
// ✅ .NET 10 推奨: Program.cs でのサービスレベル OpenAPI 設定
builder.Services.AddOpenApi();  // OpenAPI ドキュメント生成を有効化

var app = builder.Build();
app.MapOpenApi();  // /openapi/v1.json エンドポイントを公開

// ❌ .NET 10 では不要（使用禁止）: 個別エンドポイントへの .WithOpenApi()
group.MapGet("/", GetAllProducts).WithOpenApi();  // 不要
```

### 使用可能なメタデータ拡張

以下のメソッドは引き続き使用可能（推奨）:
- `.WithName()` で操作 ID を設定する
- `.WithTags()` でグループ化する
- `.Produces<T>()` / `.ProducesValidationProblem()` でレスポンス型を明示する

```csharp
// ✅ メタデータの設定（.WithOpenApi() なし）
group.MapGet("/{id}", GetProductById)
    .WithName("GetProductById")
    .Produces<ProductResponse>(200)
    .Produces(404);
```
