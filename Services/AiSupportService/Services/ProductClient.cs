using System.Net.Http.Json;
using AiSupportService.Services.Interfaces;

namespace AiSupportService.Services;

/// <summary>
/// <see cref="IProductClient"/> の実装。InventoryManagementService の REST API に HTTP リクエストを送信する。
/// </summary>
/// <remarks>
/// <para>
/// このクライアントは、AI 検索、レコメンデーション、チャットボットが
/// 商品情報にアクセスするために使用します。
/// </para>
/// <para>
/// 耐障害性:
/// <list type="bullet">
///   <item><description><see cref="HttpClient"/> は <see cref="System.Net.Http.IHttpClientFactory"/> で管理</description></item>
///   <item><description>HTTP エラー発生時は例外をスローせず、<c>null</c> または空のリストを返却</description></item>
///   <item><description>エラーはログに記録し、呼び出し元でのフォールバック処理を可能に</description></item>
/// </list>
/// </para>
/// <para>
/// このデザインにより、InventoryManagementService の障害が AI 機能全体の
/// 停止につながることを防ぎます。
/// </para>
/// </remarks>
/// <param name="httpClient">HTTP クライアント（DI で注入される型付きクライアント）。</param>
/// <param name="logger">ロガー。</param>
public class ProductClient(HttpClient httpClient, ILogger<ProductClient> logger) : IProductClient
{
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// API エンドポイント: <c>GET /api/products/search?keyword={query}</c>
    /// </para>
    /// <para>
    /// InventoryManagementService は <see cref="PaginatedResponse{T}"/> 形式で応答するため、
    /// Items プロパティから <see cref="InventoryProductDto"/> を取得し、
    /// <see cref="ProductSearchResult"/> に変換して返却します。
    /// </para>
    /// <para>
    /// エラー処理:
    /// <see cref="HttpRequestException"/> が発生した場合、エラーをログに記録し空のリストを返します。
    /// </para>
    /// </remarks>
    public async Task<List<ProductSearchResult>> SearchProductsAsync(
        string query, string? category = null, decimal? minPrice = null, decimal? maxPrice = null,
        CancellationToken ct = default)
    {
        var url = $"/api/products/search?keyword={Uri.EscapeDataString(query)}";
        if (category is not null) url += $"&brand={Uri.EscapeDataString(category)}";

        try
        {
            var response = await httpClient.GetFromJsonAsync<PaginatedResponse<InventoryProductDto>>(url, ct);
            if (response?.Items is null or { Count: 0 })
                return [];

            return response.Items.Select(p => new ProductSearchResult(
                p.Id,
                p.Name,
                0,
                p.CategoryName,
                null
            )).ToList();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "商品検索リクエスト失敗: Query={Query}", query);
            return [];
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// API エンドポイント: <c>GET /api/products/{productId}</c>
    /// </para>
    /// <para>
    /// InventoryManagementService は <see cref="InventoryProductDto"/> を返すため、
    /// <see cref="ProductDetail"/> に変換して返却します。
    /// 価格・画像・在庫は別 API で提供されるため、0/null を設定します。
    /// </para>
    /// </remarks>
    public async Task<ProductDetail?> GetProductByIdAsync(string productId, CancellationToken ct = default)
    {
        try
        {
            var product = await httpClient.GetFromJsonAsync<InventoryProductDto>($"/api/products/{productId}", ct);
            if (product is null)
                return null;

            return new ProductDetail(
                product.Id,
                product.Name,
                product.Description,
                0,
                product.CategoryName,
                null,
                0
            );
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "商品詳細取得失敗: ProductId={ProductId}", productId);
            return null;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// API エンドポイント: <c>GET /api/categories</c>
    /// </para>
    /// <para>
    /// InventoryManagementService は <c>List&lt;CategoryDto&gt;</c> を返すため、
    /// Name プロパティを抽出してカテゴリ名のリストとして返却します。
    /// </para>
    /// </remarks>
    public async Task<List<string>> GetCategoriesAsync(CancellationToken ct = default)
    {
        try
        {
            var categories = await httpClient.GetFromJsonAsync<List<InventoryCategoryDto>>("/api/categories", ct);
            if (categories is null or { Count: 0 })
                return [];

            return categories.Select(c => c.Name).ToList();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "カテゴリ一覧取得失敗");
            return [];
        }
    }
}
