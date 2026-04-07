using Frontend.DTOs;
using Frontend.Models;

namespace Frontend.Services.Interfaces;

/// <summary>
/// 商品 API クライアントインターフェース（§9.3 商品 API）— キャッシュ対応
/// </summary>
public interface IProductApiClient
{
    Task<PaginatedResult<ProductDto>?> GetProductsAsync(
        int page = 0, int size = 20, string? sort = null,
        string? category = null, decimal? minPrice = null, decimal? maxPrice = null,
        string? keyword = null, CancellationToken ct = default);

    Task<ProductDto?> GetProductByIdAsync(string id, CancellationToken ct = default);
    Task<List<ProductDto>> GetNewArrivalsAsync(int count = 8, CancellationToken ct = default);
    Task<List<ProductDto>> GetPopularProductsAsync(int count = 8, CancellationToken ct = default);
    Task<List<ProductDto>> GetProductsByCategoryAsync(string categoryId, int count = 8, CancellationToken ct = default);
    Task<SizeGuideDto?> GetSizeGuideAsync(string categoryId, CancellationToken ct = default);
    Task<List<ReviewDto>> GetProductReviewsAsync(string productId, int page = 0, int size = 10, CancellationToken ct = default);
    Task PostReviewAsync(string productId, CreateReviewRequest request, CancellationToken ct = default);
    Task<List<string>> GetCategoriesAsync(CancellationToken ct = default);
}
