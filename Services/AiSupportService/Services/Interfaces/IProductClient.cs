using System.Text.Json.Serialization;

namespace AiSupportService.Services.Interfaces;

/// <summary>
/// InventoryManagementService の商品 API と通信するクライアントインターフェース。
/// </summary>
/// <remarks>
/// <para>
/// このクライアントは、AI 検索、レコメンデーション、チャットボットが
/// 商品情報にアクセスするために使用します。
/// </para>
/// <para>
/// 通信は <see cref="System.Net.Http.IHttpClientFactory"/> を介して行われ、
/// リトライやサーキットブレーカーなどの耐障害性パターンが適用されています。
/// </para>
/// <para>
/// API エラー発生時は、例外をスローせずに <c>null</c> または空のリストを返します。
/// これにより、外部サービスの障害が AI 機能全体の停止につながることを防ぎます。
/// </para>
/// </remarks>
public interface IProductClient
{
    /// <summary>
    /// クエリおよびフィルタ条件に基づいて商品を検索する。
    /// </summary>
    /// <param name="query">
    /// 検索クエリ文字列。商品名、説明、タグなどを対象に全文検索が行われます。
    /// </param>
    /// <param name="category">
    /// カテゴリでの絞り込み。<c>null</c> の場合は全カテゴリが対象。
    /// </param>
    /// <param name="minPrice">
    /// 最低価格（日本円）でのフィルタ。<c>null</c> の場合は下限なし。
    /// </param>
    /// <param name="maxPrice">
    /// 最高価格（日本円）でのフィルタ。<c>null</c> の場合は上限なし。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// 検索条件に一致する商品のリスト。該当商品がない場合、または API エラーが発生した場合は空のリスト。
    /// </returns>
    /// <remarks>
    /// <para>
    /// API 呼び出し: <c>GET /api/products/search?keyword={query}</c>
    /// </para>
    /// <para>
    /// InventoryManagementService は <see cref="PaginatedResponse{T}"/> 形式で応答するため、
    /// Items プロパティから商品リストを取得し、<see cref="ProductSearchResult"/> に変換します。
    /// </para>
    /// </remarks>
    Task<List<ProductSearchResult>> SearchProductsAsync(
        string query, string? category = null, decimal? minPrice = null, decimal? maxPrice = null,
        CancellationToken ct = default);

    /// <summary>
    /// 商品 ID に基づいて商品の詳細情報を取得する。
    /// </summary>
    /// <param name="productId">
    /// 取得する商品の ID。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// 商品の詳細情報。商品が見つからない場合、または API エラーが発生した場合は <c>null</c>。
    /// </returns>
    /// <remarks>
    /// <para>
    /// API 呼び出し: <c>GET /api/products/{productId}</c>
    /// </para>
    /// <para>
    /// InventoryManagementService は <see cref="InventoryProductDto"/> を返すため、
    /// <see cref="ProductDetail"/> に変換して返却します。価格・画像・在庫は別 API のため 0/null を設定します。
    /// </para>
    /// </remarks>
    Task<ProductDetail?> GetProductByIdAsync(string productId, CancellationToken ct = default);

    /// <summary>
    /// 利用可能な商品カテゴリの一覧を取得する。
    /// </summary>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// カテゴリ名のリスト。カテゴリがない場合、または API エラーが発生した場合は空のリスト。
    /// </returns>
    /// <remarks>
    /// <para>
    /// API 呼び出し: <c>GET /api/categories</c>
    /// </para>
    /// <para>
    /// InventoryManagementService は <c>List&lt;CategoryDto&gt;</c> を返すため、
    /// Name プロパティを抽出してカテゴリ名のリストとして返却します。
    /// </para>
    /// </remarks>
    Task<List<string>> GetCategoriesAsync(CancellationToken ct = default);
}

/// <summary>
/// 商品検索結果を表す DTO（AiSupportService 内部用）。
/// </summary>
/// <param name="Id">商品の一意識別子。</param>
/// <param name="Name">商品の表示名。</param>
/// <param name="Price">商品の価格（税込み、日本円）。価格情報が取得できない場合は 0。</param>
/// <param name="Category">商品が属するカテゴリ名。未分類の場合は <c>null</c>。</param>
/// <param name="ImageUrl">商品画像の URL。画像がない場合は <c>null</c>。</param>
public record ProductSearchResult(string Id, string Name, decimal Price, string? Category, string? ImageUrl);

/// <summary>
/// 商品の詳細情報を表す DTO（AiSupportService 内部用）。
/// </summary>
/// <param name="Id">商品の一意識別子。</param>
/// <param name="Name">商品の表示名。</param>
/// <param name="Description">商品の詳細説明。説明がない場合は <c>null</c>。</param>
/// <param name="Price">商品の価格（税込み、日本円）。価格情報が取得できない場合は 0。</param>
/// <param name="Category">商品が属するカテゴリ名。未分類の場合は <c>null</c>。</param>
/// <param name="ImageUrl">商品画像の URL。画像がない場合は <c>null</c>。</param>
/// <param name="StockQuantity">現在の在庫数。在庫情報が取得できない場合は 0。</param>
public record ProductDetail(string Id, string Name, string? Description, decimal Price, string? Category, string? ImageUrl, int StockQuantity);

/// <summary>
/// InventoryManagementService のページネーション応答を表す DTO。
/// </summary>
/// <typeparam name="T">Items に格納される要素の型。</typeparam>
/// <param name="Items">検索結果のアイテムリスト。</param>
/// <param name="TotalElements">検索条件に一致する全アイテム数。</param>
/// <param name="Page">現在のページ番号（0 始まり）。</param>
/// <param name="Size">1 ページあたりのアイテム数。</param>
public record PaginatedResponse<T>(
    [property: JsonPropertyName("items")] List<T> Items,
    [property: JsonPropertyName("totalElements")] long TotalElements,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("size")] int Size);

/// <summary>
/// InventoryManagementService の商品 DTO（ProductDto に対応）。
/// </summary>
/// <remarks>
/// フィールドは InventoryManagementService の ProductDto と一致:
/// Id, Sku, Name, Description, Brand, CategoryName, Weight, IsActive, CreatedAt.
/// 価格・画像・在庫は別 API（/api/prices, /api/inventory）で提供される。
/// </remarks>
public record InventoryProductDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("sku")] string? Sku,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("brand")] string? Brand,
    [property: JsonPropertyName("categoryName")] string? CategoryName,
    [property: JsonPropertyName("weight")] decimal? Weight,
    [property: JsonPropertyName("isActive")] bool IsActive,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt);

/// <summary>
/// InventoryManagementService のカテゴリ DTO（CategoryDto に対応）。
/// </summary>
public record InventoryCategoryDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("parentId")] string? ParentId,
    [property: JsonPropertyName("isActive")] bool IsActive);
