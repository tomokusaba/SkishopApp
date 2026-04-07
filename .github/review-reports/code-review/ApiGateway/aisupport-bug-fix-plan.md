# AiSupportService バグ修正計画

## 概要

API Gateway 経由で `GET /api/v1/ai/recommendations/trending` を呼び出すと HTTP 500 が返される問題を調査した結果、**AiSupportService 内部に 4 つの連鎖的バグ**が存在することが判明した。本ドキュメントは、各バグの根本原因分析と、具体的な修正内容を詳述する。

> **最終検証日**: 2026-04-07  
> **検証方法**: ソースコード精読 + Docker コンテナ内からの直接 HTTP 呼び出し

### バグの連鎖構造

```
[Bug 1] ProductClient の URL パス不一致
  └─→ InventoryManagementService が 401 Unauthorized を返す（FallbackPolicy）
      └─→ HttpRequestException → ProductClient が空リストを返す
          └─→ [Bug 2] ProductClient のレスポンス形式不一致（URL 修正後に顕在化）
              └─→ デシリアライズ失敗 → HttpRequestException → 空リスト
                  └─→ [Bug 3] RecommendationService がエラーハンドリングなしで DB 保存を試行
                      └─→ [Bug 4] DB に匿名ユーザープロファイルが存在しない
                          └─→ FK 制約違反 → DbUpdateException → HTTP 500
```

> **注意**: Bug 1 を修正するだけでは、Bug 2 が顕在化する（現在は Bug 1 の 401 で先に失敗するため Bug 2 に到達しない）。全 4 件を同時に修正する必要がある。

---

## Bug 1: ProductClient の URL パス不一致（Critical）

### 根本原因

ProductClient が呼び出す URL パスと、InventoryManagementService の実際のエンドポイントパスが一致していない。

| メソッド | ProductClient 呼び出し URL | 実際の InventoryManagement エンドポイント |
|---------|--------------------------|---------------------------------------|
| `SearchProductsAsync` | `GET /api/v1/products?query={query}` | `GET /api/products/search?keyword={keyword}` |
| `GetProductByIdAsync` | `GET /api/v1/products/{productId}` | `GET /api/products/{id}` |
| `GetCategoriesAsync` | `GET /api/v1/products/categories` | `GET /api/categories`（別エンドポイントグループ） |

**検証結果**（Docker コンテナ内からの直接呼び出し）:
```
GET /api/v1/products?query=ski       → 401 Unauthorized（FallbackPolicy に到達）
GET /api/products/search?keyword=ski → 200 OK（AllowAnonymous）
GET /api/products?query=ski          → 200 OK（AllowAnonymous、ただし query パラメータ無視）
```

### 不一致の詳細

#### 1-A: URL パスプレフィックスの不一致

- **ProductClient**: `/api/v1/products/...`（`v1` プレフィックス付き）
- **InventoryManagement**: `/api/products/...`（`v1` プレフィックスなし）

`/api/v1/products` パスは InventoryManagementService に定義されていないため、認証の FallbackPolicy（`RequireAuthenticatedUser`）に到達し、JWT トークンなしのリクエストは 401 Unauthorized となる。

#### 1-B: 検索エンドポイントのパスの不一致

- **ProductClient**: `GET /api/v1/products?query={query}`（ルートパス + query パラメータ）
- **InventoryManagement**: `GET /api/products/search?keyword={keyword}`（`/search` サブパス + `keyword` パラメータ）

#### 1-C: 検索パラメータ名の不一致

- **ProductClient**: `query`（クエリパラメータ名）
- **InventoryManagement**: `Keyword`（`ProductSearchParams` record のプロパティ名。ASP.NET Core のバインディングは case-insensitive だが、パラメータ名自体が異なる）

#### 1-D: カテゴリ一覧エンドポイントのパス不一致

- **ProductClient**: `GET /api/v1/products/categories` を呼び出す
- **InventoryManagement**: `GET /api/categories`（`CategoryEndpoints.cs` で定義。`/api/products` 配下ではなく独立したグループ）
- **レスポンス形式の不一致**: ProductClient は `List<string>` を期待するが、InventoryManagement は `List<CategoryDto>` を返す
  - `CategoryDto`: `Id`, `Name`, `Description`, `ParentId`, `Level`, `Path`, `IsActive`

### 影響範囲

ProductClient の全 3 メソッドが影響を受ける:

| ファイル | 行番号 | メソッド | 影響 |
|---------|--------|---------|------|
| `ProductClient.cs` | L50 | `SearchProductsAsync` | URL パス + パラメータ名が不正 |
| `ProductClient.cs` | L82 | `GetProductByIdAsync` | URL パスが不正 |
| `ProductClient.cs` | L105 | `GetCategoriesAsync` | URL パスが不正 + レスポンス形式不一致（`List<string>` vs `List<CategoryDto>`） |

### 修正内容

#### 修正対象ファイル: `Services/AiSupportService/Services/ProductClient.cs`

**修正 1-1: `SearchProductsAsync` の URL 修正（L50）**

```csharp
// 修正前（L50）
var url = $"/api/v1/products?query={Uri.EscapeDataString(query)}";
if (category is not null) url += $"&category={Uri.EscapeDataString(category)}";
if (minPrice.HasValue) url += $"&minPrice={minPrice}";
if (maxPrice.HasValue) url += $"&maxPrice={maxPrice}";

// 修正後
var url = $"/api/products/search?keyword={Uri.EscapeDataString(query)}";
if (category is not null) url += $"&categoryId={Uri.EscapeDataString(category)}";
// Note: InventoryManagement の SearchProducts は minPrice/maxPrice パラメータを
//       サポートしていない（ProductSearchParams: Keyword, CategoryId, Brand, Page, Size）
//       将来的に価格フィルタが必要な場合は InventoryManagement 側に追加が必要
```

**修正 1-2: `GetProductByIdAsync` の URL 修正（L82）**

```csharp
// 修正前（L82）
return await httpClient.GetFromJsonAsync<ProductDetail>($"/api/v1/products/{productId}", ct);

// 修正後
return await httpClient.GetFromJsonAsync<ProductDetail>($"/api/products/{productId}", ct);
```

**修正 1-3: `GetCategoriesAsync` の URL + レスポンス形式修正（L105）**

```csharp
// 修正前（L105）
var result = await httpClient.GetFromJsonAsync<List<string>>("/api/v1/products/categories", ct);

// 修正後
// InventoryManagementService のカテゴリエンドポイントは /api/categories
// レスポンスは List<CategoryDto> のため、カテゴリ名のみを抽出する
var categories = await httpClient.GetFromJsonAsync<List<InventoryCategoryDto>>("/api/categories", ct);
return categories?.Select(c => c.Name).ToList() ?? [];
```

> **確認済み**: InventoryManagementService の `GET /api/categories` は `List<CategoryDto>` を返す（AllowAnonymous）。
> `CategoryDto` のフィールド: `Id`, `Name`, `Description`, `ParentId`, `Level`, `Path`, `IsActive`。

#### 修正対象ファイル: `Services/AiSupportService/Services/Interfaces/IProductClient.cs`

- XML ドキュメントコメント内の API パス記載を修正（L45, L69, L88）

---

## Bug 2: レスポンス形式の不一致（Critical）

### 根本原因

ProductClient は InventoryManagementService のレスポンスを `List<ProductSearchResult>` として直接デシリアライズしようとしているが、InventoryManagementService は `PaginatedResult<ProductDto>` 形式でレスポンスを返す。

#### InventoryManagementService の実際のレスポンス形式

```json
{
  "items": [
    {
      "id": "...",
      "sku": "...",
      "name": "...",
      "description": "...",
      "brand": "...",
      "categoryName": "...",
      "weight": 0.0,
      "isActive": true,
      "createdAt": "..."
    }
  ],
  "totalElements": 0,
  "page": 0,
  "size": 20,
  "totalPages": 0,
  "hasNext": false,
  "hasPrevious": false
}
```

#### ProductClient が期待する形式

```json
[
  {
    "id": "...",
    "name": "...",
    "price": 0,
    "category": "...",
    "imageUrl": "..."
  }
]
```

#### 不一致のポイント

| 項目 | ProductClient (AiSupportService) | InventoryManagementService |
|------|--------------------------------|---------------------------|
| **ルートオブジェクト** | `List<T>` (JSON 配列) | `PaginatedResult<T>` (JSON オブジェクト) |
| **商品レコード型** | `ProductSearchResult(Id, Name, Price, Category, ImageUrl)` | `ProductDto(Id, Sku, Name, Description, Brand, CategoryName, Weight, IsActive, CreatedAt)` |
| **価格フィールド** | `Price` (decimal) | なし（`ProductDto` には Price がない。価格は `GET /api/prices/{productId}` で別途取得が必要） |
| **カテゴリフィールド** | `Category` (string?) | `CategoryName` (string?) |
| **画像フィールド** | `ImageUrl` (string?) | なし（`ProductDto` には画像情報がない。画像は `ProductDetailDto` のみに存在するが、現在未使用） |

### 影響範囲

| ファイル | 行番号 | メソッド | 問題 |
|---------|--------|---------|------|
| `ProductClient.cs` | L57 | `SearchProductsAsync` | `GetFromJsonAsync<List<ProductSearchResult>>` — ページネーション形式をリスト形式でデシリアライズ |
| `ProductClient.cs` | L82 | `GetProductByIdAsync` | `GetFromJsonAsync<ProductDetail>` — `ProductDto` とフィールド不一致（※ `GET /api/products/{id}` は `ProductDto` を返す。`ProductDetailDto` は定義のみで未使用） |
| `ProductClient.cs` | L105 | `GetCategoriesAsync` | `GetFromJsonAsync<List<string>>` — 実際のレスポンスは `List<CategoryDto>` |

### 修正内容

#### 修正対象ファイル: `Services/AiSupportService/Services/Interfaces/IProductClient.cs`

**修正 2-1: ページネーション対応の DTO を追加**

ファイル末尾に以下を追加:

```csharp
/// <summary>
/// InventoryManagementService のページネーション付きレスポンスを表す DTO。
/// </summary>
/// <typeparam name="T">リスト要素の型。</typeparam>
/// <param name="Items">現在ページのデータリスト。</param>
/// <param name="TotalElements">検索結果の合計件数。</param>
/// <param name="Page">現在のページ番号（0 始まり）。</param>
/// <param name="Size">1 ページあたりの件数。</param>
public record PaginatedResponse<T>(
    List<T> Items,
    long TotalElements,
    int Page,
    int Size);

/// <summary>
/// InventoryManagementService から返される商品一覧用 DTO。
/// </summary>
/// <remarks>
/// <para>
/// InventoryManagementService の <c>ProductDto</c> に対応。
/// Price, ImageUrl は商品一覧では含まれないため、詳細取得時に取得する。
/// </para>
/// </remarks>
/// <param name="Id">商品の一意識別子。</param>
/// <param name="Sku">SKU コード。</param>
/// <param name="Name">商品の表示名。</param>
/// <param name="Description">商品説明。</param>
/// <param name="Brand">ブランド名。</param>
/// <param name="CategoryName">所属カテゴリ名。</param>
/// <param name="Weight">重量（kg 単位）。</param>
/// <param name="IsActive">有効フラグ。</param>
/// <param name="CreatedAt">作成日時。</param>
public record InventoryProductDto(
    string Id,
    string? Sku,
    string Name,
    string? Description,
    string? Brand,
    string? CategoryName,
    decimal? Weight,
    bool IsActive,
    DateTimeOffset CreatedAt);
```

#### 修正対象ファイル: `Services/AiSupportService/Services/ProductClient.cs`

**修正 2-2: `SearchProductsAsync` のデシリアライズ修正（L55-L64）**

```csharp
// 修正前（L57）
var result = await httpClient.GetFromJsonAsync<List<ProductSearchResult>>(url, ct);
return result ?? [];

// 修正後
var response = await httpClient.GetFromJsonAsync<PaginatedResponse<InventoryProductDto>>(url, ct);
if (response?.Items is null or { Count: 0 })
    return [];

return response.Items.Select(p => new ProductSearchResult(
    p.Id,
    p.Name,
    0m,                // ProductDto には Price がない（一覧レスポンスに含まれない）
    p.CategoryName,
    null               // ProductDto には ImageUrl がない（一覧レスポンスに含まれない）
)).ToList();
```

**修正 2-3: `GetProductByIdAsync` のデシリアライズ修正（L80-L88）**

InventoryManagementService の `GET /api/products/{id}` は **`ProductDto` を返す（`ProductDetailDto` ではない）**。
`ProductDetailDto` は定義されているが現在の実装では使用されていない。

`ProductDto` のフィールド: `Id`, `Sku`, `Name`, `Description`, `Brand`, `CategoryName`, `Weight`, `IsActive`, `CreatedAt`

```csharp
// 修正前（L82）
return await httpClient.GetFromJsonAsync<ProductDetail>($"/api/v1/products/{productId}", ct);

// 修正後
var dto = await httpClient.GetFromJsonAsync<InventoryProductDto>($"/api/products/{productId}", ct);
if (dto is null) return null;

// ProductDto には Price, ImageUrl, StockQuantity が含まれないため、
// 必要に応じて /api/prices/{productId} や /api/inventory/{productId} を別途呼び出す。
// 現時点では AI レコメンデーション用途では商品名とカテゴリが主要なので 0/null で代替する。
return new ProductDetail(
    dto.Id,
    dto.Name,
    dto.Description,
    0m,                  // ProductDto には Price がない
    dto.CategoryName,
    null,                // ProductDto には ImageUrl がない
    0                    // ProductDto には StockQuantity がない
);
```

> **重要な発見**: 当初の計画では `GET /api/products/{id}` が `ProductDetailDto`（Price, Category, Inventory, Images を含むネスト構造）を返すと想定していたが、**実際のエンドポイントは `ProductDto` を返す**（ネストなし、フラット構造）。`ProductDetailDto` は `ProductDto.cs` に定義されているが、どのエンドポイントハンドラーからも使用されていない。
>
> **将来の改善候補**: AI レコメンデーションで価格情報が必要になった場合は、`GET /api/prices/{productId}` を別途呼び出す `GetProductPriceAsync` メソッドを ProductClient に追加する。

**修正 2-4: 不要になった DTO の整理**

当初計画していた以下のネスト DTO は**不要**（`ProductDetailDto` が未使用のため削除）:

```csharp
// ❌ 以下は不要（削除）
// public record InventoryProductDetailDto(...)  // GET /api/products/{id} は ProductDto を返す
// public record InventoryPriceDto(...)           // ネスト構造が不要
// public record InventoryDto(int Quantity, ...)  // ネスト構造が不要
// public record InventoryImageDto(...)           // ネスト構造が不要
```

代わりに、カテゴリ用の DTO のみ追加する:

```csharp
/// <summary>
/// InventoryManagementService のカテゴリ DTO（ローカル定義）。
/// GET /api/categories のレスポンス要素に対応。
/// </summary>
/// <param name="Id">カテゴリ ID。</param>
/// <param name="Name">カテゴリ名。</param>
/// <param name="Description">カテゴリ説明。</param>
/// <param name="ParentId">親カテゴリ ID。</param>
/// <param name="Level">階層レベル。</param>
/// <param name="Path">カテゴリパス。</param>
/// <param name="IsActive">有効フラグ。</param>
public record InventoryCategoryDto(
    string Id,
    string Name,
    string? Description,
    string? ParentId,
    int Level,
    string? Path,
    bool IsActive);
```

---

## Bug 3: RecommendationService のエラーハンドリング不足（High）

### 根本原因

RecommendationService の **6 つのメソッド** で、ProductClient 呼び出し後のエラーハンドリングが不足している。具体的には:

1. **商品検索結果が空の場合でも DB 保存を試行する**（GetTrendingAsync, GetSeasonalAsync）
2. **SaveChangesAsync の例外がキャッチされない**（DbUpdateException が上位に伝播 → HTTP 500）
3. **ProductClient の HttpRequestException 以外の例外がキャッチされない**（例: JsonException, TaskCanceledException）

### 影響を受けるメソッド

| メソッド | 行番号 | 問題点 |
|---------|--------|--------|
| `GetPersonalizedAsync` | L86-148 | L105: **空商品リストチェックなし**（foreach は空なら何もしないが、警告ログが出ない）、L139: SaveChangesAsync 未保護 |
| `GetSimilarProductsAsync` | L287-314 | L305 で件数チェックあり ✅、L308: SaveChangesAsync 未保護 |
| `GetTrendingAsync` | L330-347 | L337-342: **空リストチェックなし + SaveChangesAsync 未保護**（最も深刻） |
| `GetSeasonalAsync` | L363-387 | L377-382: **空リストチェックなし + SaveChangesAsync 未保護** |
| `GetFrequentlyBoughtTogetherAsync` | L404-425 | L418 で件数チェックあり ✅、L421: SaveChangesAsync 未保護 |
| `RecordFeedbackAsync` | L444-461 | L456: SaveChangesAsync 未保護（DB エラー時に HTTP 500） |

### 修正内容

#### 修正対象ファイル: `Services/AiSupportService/Services/RecommendationService.cs`

**修正 3-1: `GetTrendingAsync` の修正（L330-347）**

```csharp
// 修正前（L330-347）
public async Task<List<RecommendationResponse>> GetTrendingAsync(CancellationToken ct = default)
{
    var cacheKey = "recommendations:trending";
    var cached = await cacheService.GetAsync<List<RecommendationResponse>>(cacheKey, ct);
    if (cached is not null)
        return cached;

    var products = await productClient.SearchProductsAsync("人気 トレンド", ct: ct);
    var productIds = products.Take(10).Select(p => p.Id).ToList();

    var recommendation = Recommendation.CreateTrending(AnonymousUserId, productIds, 0.9m, "現在人気のトレンド商品");
    await recommendationRepository.AddAsync(recommendation, ct);
    await recommendationRepository.SaveChangesAsync(ct);

    var result = new List<RecommendationResponse> { ToResponse(recommendation) };
    await cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(15), ct);
    return result;
}

// 修正後
public async Task<List<RecommendationResponse>> GetTrendingAsync(CancellationToken ct = default)
{
    var cacheKey = "recommendations:trending";
    var cached = await cacheService.GetAsync<List<RecommendationResponse>>(cacheKey, ct);
    if (cached is not null)
        return cached;

    var products = await productClient.SearchProductsAsync("人気 トレンド", ct: ct);
    var productIds = products.Take(10).Select(p => p.Id).ToList();

    // 商品が見つからない場合は空リストを返す（DB 保存をスキップ）
    if (productIds.Count == 0)
    {
        logger.LogWarning("トレンド商品が見つかりませんでした");
        return [];
    }

    try
    {
        var recommendation = Recommendation.CreateTrending(AnonymousUserId, productIds, 0.9m, "現在人気のトレンド商品");
        await recommendationRepository.AddAsync(recommendation, ct);
        await recommendationRepository.SaveChangesAsync(ct);

        var result = new List<RecommendationResponse> { ToResponse(recommendation) };
        await cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(15), ct);
        return result;
    }
    catch (DbUpdateException ex)
    {
        logger.LogError(ex, "トレンドレコメンデーションの保存に失敗しました");
        return [];
    }
}
```

**修正 3-2: `GetSeasonalAsync` の修正（L363-387）**

`GetTrendingAsync` と同様のパターンを適用:

```csharp
// 修正後
public async Task<List<RecommendationResponse>> GetSeasonalAsync(CancellationToken ct = default)
{
    var cacheKey = "recommendations:seasonal";
    var cached = await cacheService.GetAsync<List<RecommendationResponse>>(cacheKey, ct);
    if (cached is not null)
        return cached;

    var season = DateTime.UtcNow.Month switch
    {
        >= 10 or <= 3 => "冬 スキー",
        >= 4 and <= 6 => "春 アウトドア",
        _ => "夏 トレーニング"
    };

    var products = await productClient.SearchProductsAsync(season, ct: ct);
    var productIds = products.Take(10).Select(p => p.Id).ToList();

    // 商品が見つからない場合は空リストを返す
    if (productIds.Count == 0)
    {
        logger.LogWarning("季節商品が見つかりませんでした: Season={Season}", season);
        return [];
    }

    try
    {
        var recommendation = Recommendation.CreateSeasonal(AnonymousUserId, productIds, 0.8m, "今シーズンのおすすめ商品");
        await recommendationRepository.AddAsync(recommendation, ct);
        await recommendationRepository.SaveChangesAsync(ct);

        var result = new List<RecommendationResponse> { ToResponse(recommendation) };
        await cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(60), ct);
        return result;
    }
    catch (DbUpdateException ex)
    {
        logger.LogError(ex, "季節レコメンデーションの保存に失敗しました");
        return [];
    }
}
```

**修正 3-3: `GetSimilarProductsAsync` の修正（L287-314）**

`SaveChangesAsync` に try-catch を追加:

```csharp
// 修正前（L305-309）
if (recommendations.Count > 0)
{
    await recommendationRepository.AddRangeAsync(recommendations, ct);
    await recommendationRepository.SaveChangesAsync(ct);
}

// 修正後
if (recommendations.Count > 0)
{
    try
    {
        await recommendationRepository.AddRangeAsync(recommendations, ct);
        await recommendationRepository.SaveChangesAsync(ct);
    }
    catch (DbUpdateException ex)
    {
        logger.LogError(ex, "類似商品レコメンデーションの保存に失敗しました: ProductId={ProductId}", productId);
        // DB 保存失敗でも検索結果をレスポンスとして返す（キャッシュはしない）
    }
}
```

**修正 3-4: `GetFrequentlyBoughtTogetherAsync` の修正（L404-425）**

同様に `SaveChangesAsync` に try-catch を追加:

```csharp
// 修正前（L418-422）
if (recommendations.Count > 0)
{
    await recommendationRepository.AddRangeAsync(recommendations, ct);
    await recommendationRepository.SaveChangesAsync(ct);
}

// 修正後
if (recommendations.Count > 0)
{
    try
    {
        await recommendationRepository.AddRangeAsync(recommendations, ct);
        await recommendationRepository.SaveChangesAsync(ct);
    }
    catch (DbUpdateException ex)
    {
        logger.LogError(ex, "頻繁に一緒に購入レコメンデーションの保存に失敗しました: ProductId={ProductId}", productId);
    }
}
```

**修正 3-5: `GetPersonalizedAsync` の修正（L86-148）**

`SaveChangesAsync` (L139) に try-catch を追加 + 空商品リスト時の警告ログを追加:

```csharp
// 修正前（L104-106）
var searchQuery = BuildPersonalizedSearchQuery(profile);
var products = await productClient.SearchProductsAsync(searchQuery, ct: ct);

// 修正後（空商品チェック追加）
var searchQuery = BuildPersonalizedSearchQuery(profile);
var products = await productClient.SearchProductsAsync(searchQuery, ct: ct);

if (products.Count == 0)
{
    logger.LogWarning("パーソナライズ商品が見つかりませんでした: UserId={UserId}, Query={Query}", userId, searchQuery);
    return [];
}
```

```csharp
// 修正前（L120-140）
if (recommendations.Count > 0)
{
    var productIds = products.Take(10).Select(p => p.Id).ToList();
    var outboxEvent = new OutboxEvent { ... };
    await outboxEventRepository.AddAsync(outboxEvent, ct);
    await recommendationRepository.SaveChangesAsync(ct);
}

// 修正後
if (recommendations.Count > 0)
{
    try
    {
        var productIds = products.Take(10).Select(p => p.Id).ToList();
        var outboxEvent = new OutboxEvent { ... };
        await outboxEventRepository.AddAsync(outboxEvent, ct);
        await recommendationRepository.SaveChangesAsync(ct);
    }
    catch (DbUpdateException ex)
    {
        logger.LogError(ex, "パーソナライズレコメンデーションの保存に失敗しました: UserId={UserId}", userId);
        // DB 保存に失敗してもレコメンデーション結果自体は返す
    }
}
```

**修正 3-6: `RecordFeedbackAsync` の修正（L444-461）**

`SaveChangesAsync` (L456) に try-catch を追加:

```csharp
// 修正前（L453-456）
if (request.FeedbackType is "CLICK" or "LIKE")
    recommendation.MarkViewed();

await recommendationRepository.SaveChangesAsync(ct);

// 修正後
if (request.FeedbackType is "CLICK" or "LIKE")
    recommendation.MarkViewed();

try
{
    await recommendationRepository.SaveChangesAsync(ct);
}
catch (DbUpdateException ex)
{
    logger.LogError(ex, "フィードバック保存に失敗しました: RecommendationId={RecommendationId}", request.RecommendationId);
    throw new BusinessException("フィードバックの保存に失敗しました。再度お試しください。");
}
```

#### using ディレクティブの追加

`RecommendationService.cs` の先頭に以下を追加（未追加の場合）:

```csharp
using Microsoft.EntityFrameworkCore;  // DbUpdateException のため
```

---

## Bug 4: 匿名ユーザープロファイルの未作成（Critical）

### 根本原因

`recommendations` テーブルの `user_id` カラムには `user_profiles` テーブルの `user_id` カラムへの外部キー制約が設定されている（CASCADE DELETE）。トレンド・季節・類似商品・頻繁購入のレコメンデーションは `AnonymousUserId = "anonymous"` を使用するが、`user_profiles` テーブルに `user_id = 'anonymous'` のレコードが存在しない。

#### DB 状態の確認結果

```sql
-- user_profiles テーブルは空
SELECT id, user_id FROM user_profiles;
-- (0 rows)

-- __EFMigrationsHistory テーブルが存在しない（マイグレーション未適用）
```

#### マイグレーションファイルの存在

`20260406130800_AddAnonymousUserProfile.cs` が存在し、以下の SQL を実行する:

```sql
INSERT INTO user_profiles (
    id, user_id, preferences_json, browsing_history_json,
    purchase_history_json, last_activity_at, created_at, updated_at, row_version
) VALUES (
    '00000000-0000-0000-0000-000000000001', 'anonymous',
    '{"type": "anonymous", ...}', '[]', '[]', NULL,
    CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, '\x00000000'::bytea
) ON CONFLICT (user_id) DO NOTHING;
```

しかし、このマイグレーションは DB に適用されていない。

### FK 制約の定義（AppDbContext.cs L190-194）

```csharp
entity.HasOne(e => e.UserProfile)
    .WithMany(p => p.Recommendations)
    .HasForeignKey(e => e.UserId)
    .HasPrincipalKey(p => p.UserId)  // user_profiles.user_id を参照
    .OnDelete(DeleteBehavior.Cascade);
```

### 修正内容

#### 修正方針の検討

| 案 | 内容 | メリット | デメリット |
|----|------|---------|----------|
| **案 A: EF Core マイグレーション適用** | `dotnet ef database update` でマイグレーションを適用 | 正統的アプローチ | Docker 環境での実行が煩雑 |
| **案 B: Program.cs での起動時シード（推奨）** | マイグレーション適用後に匿名ユーザーの存在確認・挿入 | アプリ起動のたびに確認。マイグレーションとも共存可能 | 起動時間が微増（無視可能） |
| **案 C: HasData + Program.cs 起動時シード** | OnModelCreating で HasData を追加 + 起動時確認 | 二重保証 | HasData は `RowVersion`（`[Timestamp]`）プロパティとの相性に注意（PostgreSQL の xmin は DB 管理のため、シードデータに含めるとマイグレーション生成時に問題が生じる可能性あり） |

**→ 案 B を採用する。**

理由:
1. `HasData` は `[Timestamp]` 付きの `RowVersion` プロパティを持つエンティティでは、マイグレーション生成時に `RowVersion` の固定値を要求するが、PostgreSQL の `xmin` 列はデータベースが自動管理するため、不適切な値を設定する可能性がある
2. Program.cs の起動時シードは、マイグレーション適用後に実行されるため確実
3. 既存のマイグレーション（`20260406130800_AddAnonymousUserProfile.cs`）も正常に動作するため、二重挿入は `ON CONFLICT DO NOTHING` で防止される

#### 修正 4-1: 即時修正（SQL 直接実行）

Docker コンテナ内で以下を実行:

```sql
-- aisupportdb に匿名ユーザープロファイルを挿入
INSERT INTO user_profiles (
    id, user_id, preferences_json, browsing_history_json,
    purchase_history_json, last_activity_at, created_at, updated_at, row_version
) VALUES (
    '00000000-0000-0000-0000-000000000001',
    'anonymous',
    '{"type": "anonymous", "description": "匿名ユーザー向けレコメンデーション用プロファイル"}',
    '[]',
    '[]',
    NULL,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP,
    '\x00000000'::bytea
) ON CONFLICT (user_id) DO NOTHING;
```

#### 修正 4-2: Program.cs での起動時シード（永続的保証）

**修正対象ファイル: `Services/AiSupportService/Program.cs`**

既存の DB マイグレーション自動適用ブロック（L371-384）の **直後** かつ `await app.RunAsync();`（L386）の **直前** に追加:

```csharp
// --- 匿名ユーザープロファイルのシード（L384 の直後に追加）---
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    
    // 匿名ユーザープロファイルの存在確認・作成
    var anonymousExists = await dbContext.UserProfiles
        .AnyAsync(p => p.UserId == "anonymous");
    
    if (!anonymousExists)
    {
        var seedLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        seedLogger.LogInformation("匿名ユーザープロファイルを作成します");
        
        dbContext.UserProfiles.Add(new UserProfile
        {
            Id = "00000000-0000-0000-0000-000000000001",
            UserId = "anonymous",
            PreferencesJson = "{\"type\": \"anonymous\", \"description\": \"匿名ユーザー向けレコメンデーション用プロファイル\"}",
            BrowsingHistoryJson = "[]",
            PurchaseHistoryJson = "[]",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        seedLogger.LogInformation("匿名ユーザープロファイルを作成しました");
    }
}
```

> **注意**: `AnyAsync` を使用して存在確認を行い、`FirstOrDefaultAsync` よりも効率的にチェックする。
> このコードはマイグレーション適用後に実行されるため、テーブルが存在することが保証されている。
> 既存のマイグレーション `20260406130800_AddAnonymousUserProfile.cs` の SQL にも `ON CONFLICT (user_id) DO NOTHING` が含まれているため、マイグレーション経由で挿入された場合でも二重挿入は発生しない。

**using ディレクティブの確認**: Program.cs に `using Microsoft.EntityFrameworkCore;` が必要（`AnyAsync` のため）。既に含まれている場合は不要。

---

## 修正の実施順序

### Phase 1: ProductClient の URL・レスポンス修正（Bug 1 + Bug 2）

| # | ファイル | 修正内容 | 優先度 |
|---|---------|---------|--------|
| 1 | `Services/AiSupportService/Services/Interfaces/IProductClient.cs` | ローカル DTO 追加（`PaginatedResponse<T>`, `InventoryProductDto`, `InventoryProductDetailDto` 等）、XML コメント修正 | Critical |
| 2 | `Services/AiSupportService/Services/ProductClient.cs` | URL パス修正（3 メソッド全て）+ デシリアライズ修正 | Critical |

### Phase 2: 匿名ユーザープロファイル作成（Bug 4）

| # | ファイル | 修正内容 | 優先度 |
|---|---------|---------|--------|
| 3 | SQL 直接実行 | `aisupportdb` に匿名ユーザープロファイルを INSERT（即時修正） | Critical |
| 4 | `Services/AiSupportService/Program.cs` | 起動時の匿名ユーザー確認・挿入ロジックを追加（L384 の直後） | Critical |

### Phase 3: エラーハンドリング強化（Bug 3）

| # | ファイル | 修正内容 | 優先度 |
|---|---------|---------|--------|
| 5 | `Services/AiSupportService/Services/RecommendationService.cs` | `GetTrendingAsync` — 空リストチェック + SaveChangesAsync try-catch | High |
| 6 | `Services/AiSupportService/Services/RecommendationService.cs` | `GetSeasonalAsync` — 同上 | High |
| 7 | `Services/AiSupportService/Services/RecommendationService.cs` | `GetSimilarProductsAsync` — SaveChangesAsync try-catch | High |
| 8 | `Services/AiSupportService/Services/RecommendationService.cs` | `GetFrequentlyBoughtTogetherAsync` — SaveChangesAsync try-catch | High |
| 9 | `Services/AiSupportService/Services/RecommendationService.cs` | `GetPersonalizedAsync` — 空商品チェック + SaveChangesAsync try-catch | High |
| 10 | `Services/AiSupportService/Services/RecommendationService.cs` | `RecordFeedbackAsync` — SaveChangesAsync try-catch | Medium |
| 11 | `Services/AiSupportService/Services/RecommendationService.cs` | `using Microsoft.EntityFrameworkCore;` の追加 | High |

### Phase 4: ビルド・デプロイ・検証

| # | 作業内容 | 備考 |
|---|---------|------|
| 12 | `dotnet build` で全体ビルド確認 | コンパイルエラーがないことを確認 |
| 13 | Docker イメージ再ビルド | `docker compose build ai-support-service` |
| 14 | コンテナ再起動 | `docker compose up -d ai-support-service` |
| 15 | 動作検証 | `GET /api/v1/ai/recommendations/trending` で HTTP 200 を確認 |

---

## 修正対象ファイル一覧

| # | ファイルパス | 修正種別 | Bug # |
|---|------------|---------|-------|
| 1 | `Services/AiSupportService/Services/Interfaces/IProductClient.cs` | 追加・修正 | 1, 2 |
| 2 | `Services/AiSupportService/Services/ProductClient.cs` | 修正 | 1, 2 |
| 3 | `Services/AiSupportService/Services/RecommendationService.cs` | 修正 | 3 |
| 4 | `Services/AiSupportService/Program.cs` | 修正 | 4 |

---

## 補足: InventoryManagementService のエンドポイント構成（確認済み）

### カテゴリエンドポイント（確認済み）

`CategoryEndpoints.cs` で以下が定義されている:

| HTTP メソッド | パス | 認可 | レスポンス |
|-------------|------|------|---------|
| GET | `/api/categories` | AllowAnonymous | `List<CategoryDto>` |
| GET | `/api/categories/{id}` | AllowAnonymous | `CategoryDto` |
| GET | `/api/categories/{id}/products` | AllowAnonymous | `PaginatedResult<ProductDto>` |

`CategoryDto` のフィールド: `Id`, `Name`, `Description`, `ParentId`, `Level`, `Path`, `IsActive`

### 商品エンドポイント（確認済み）

| HTTP メソッド | パス | 認可 | レスポンス |
|-------------|------|------|---------|
| GET | `/api/products` | AllowAnonymous | `PaginatedResult<ProductDto>` |
| GET | `/api/products/{id}` | AllowAnonymous | **`ProductDto`**（`ProductDetailDto` ではない） |
| GET | `/api/products/search` | AllowAnonymous | `PaginatedResult<ProductDto>` |

`ProductSearchParams` のパラメータ: `Keyword`, `CategoryId`, `Brand`, `Page`(=0), `Size`(=20), `SortBy`(="CreatedAt"), `Descending`(=true)

> **重要**: `ProductDetailDto` は定義されている（`Id`, `Sku`, `Name`, `Description`, `Brand`, `Attributes`, `Tags`, `Weight`, `IsActive`, `Category`(nested), `CurrentPrice`(nested), `Inventory`(nested), `Images`(list)）が、**現在のエンドポイントハンドラーでは使用されていない**。全ての GET エンドポイントが `ProductDto` を返す。

### 価格・在庫エンドポイント

| HTTP メソッド | パス | 認可 | レスポンス |
|-------------|------|------|---------|
| GET | `/api/prices/{productId}` | AllowAnonymous | `PriceDto` |
| GET | `/api/inventory/{productId}` | AllowAnonymous | `InventoryDto` |

これらは将来、AI レコメンデーションで価格・在庫情報が必要になった場合に利用可能。

---

## 検証チェックリスト

修正完了後、以下のエンドポイントで動作確認を行う:

| # | エンドポイント | 期待されるレスポンス | 備考 |
|---|-------------|-------------------|------|
| 1 | `GET /api/v1/ai/recommendations/trending` | HTTP 200 + レコメンデーション JSON（または空リスト `[]`） | Bug 1-4 の修正後 |
| 2 | `GET /api/v1/ai/recommendations/seasonal` | HTTP 200 + レコメンデーション JSON（または空リスト `[]`） | Bug 1-4 の修正後 |
| 3 | `GET /api/v1/ai/recommendations/similar/{productId}` | HTTP 200 + レコメンデーション JSON（または空リスト `[]`） | 有効な productId が必要 |
| 4 | `GET /api/v1/ai/recommendations/frequently-bought/{productId}` | HTTP 200 + レコメンデーション JSON（または空リスト `[]`） | 有効な productId が必要 |
| 5 | `GET /api/v1/ai/recommendations/personalized` | HTTP 200 + レコメンデーション JSON（または空リスト `[]`） | 認証トークン必要 |
| 6 | `GET /api/v1/ai/search?query=ski` | HTTP 200 + 検索結果 JSON | ProductClient 修正の検証 |

> **注意**: InventoryManagementService の DB に商品データが存在しない場合、レコメンデーションは空リストを返す（HTTP 200 + `[]`）。これは正常動作であり、エラーではない。HTTP 500 が返らないことが修正成功の判定基準。

---

## 修正前後の変更サマリ

### v2 更新内容（2026-04-07 検証後の修正）

本プランの初版作成後、ソースコードの精密な照合により以下の誤りを発見・修正した:

| # | 初版の誤り | 修正内容 |
|---|----------|---------|
| 1 | `GET /api/products/{id}` が `ProductDetailDto` を返すと想定 | **実際は `ProductDto` を返す**。`ProductDetailDto` は未使用。ネスト DTO（InventoryProductDetailDto 等）は不要 |
| 2 | `GetCategoriesAsync` のエンドポイントを「未定義」と記載 | **`GET /api/categories` として定義済み**。レスポンスは `List<CategoryDto>` |
| 3 | HasData による匿名ユーザーシードを推奨 | **`[Timestamp]` 付き RowVersion との相性問題**のため、Program.cs 起動時シードに変更 |
| 4 | `RecordFeedbackAsync` (L456) の SaveChangesAsync が未カバー | **修正 3-6 として追加** |
| 5 | `GetPersonalizedAsync` の空商品チェックが未記載 | **修正 3-5 に空商品チェック + 警告ログを追加** |
| 6 | 修正 3-3 の catch ブロックで RecommendationResponse を手動構築 | **不要な複雑さを排除**。DB 保存失敗でもレスポンスを返す簡潔な実装に変更 |
| 7 | 検証チェックリストに `frequently-bought/{productId}` が欠如 | **追加** |
| 8 | AppDbContext.cs を修正対象ファイルに含めていた | **不要**（HasData 削除に伴い修正対象から除外） |
