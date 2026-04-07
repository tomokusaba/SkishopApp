using Frontend.DTOs;
using Frontend.Models;
using Frontend.Services.Interfaces;

namespace Frontend.Services;

/// <summary>
/// 商品 API クライアント（§9.3 商品 API）— キャッシュ対応
/// </summary>
public class ProductApiClient(
    IApiGatewayClient apiClient,
    ICacheService cacheService,
    ILogger<ProductApiClient> logger) : IProductApiClient
{
    public async Task<PaginatedResult<ProductDto>?> GetProductsAsync(
        int page = 0, int size = 20, string? sort = null,
        string? category = null, decimal? minPrice = null, decimal? maxPrice = null,
        string? keyword = null, CancellationToken ct = default)
    {
        var queryParts = new List<string> { $"page={page}", $"size={size}" };
        if (!string.IsNullOrWhiteSpace(sort)) queryParts.Add($"sort={sort}");
        if (!string.IsNullOrWhiteSpace(category)) queryParts.Add($"category={Uri.EscapeDataString(category)}");
        if (minPrice.HasValue) queryParts.Add($"minPrice={minPrice.Value}");
        if (maxPrice.HasValue) queryParts.Add($"maxPrice={maxPrice.Value}");
        if (!string.IsNullOrWhiteSpace(keyword)) queryParts.Add($"keyword={Uri.EscapeDataString(keyword)}");

        var path = $"/api/products?{string.Join("&", queryParts)}";
        var cacheKey = $"products_list_{page}_{size}_{sort}_{category}_{minPrice}_{maxPrice}_{keyword}";

        return await cacheService.GetOrSetAsync(
            cacheKey,
            "products",
            () => apiClient.GetAsync<PaginatedResult<ProductDto>>(path, ct),
            ct);
    }

    public async Task<ProductDto?> GetProductByIdAsync(string id, CancellationToken ct = default)
    {
        var cacheKey = $"product_{id}";
        return await cacheService.GetOrSetAsync(
            cacheKey,
            "products",
            () => apiClient.GetAsync<ProductDto>($"/api/products/{id}", ct),
            ct);
    }

    public async Task<List<ProductDto>> GetNewArrivalsAsync(int count = 8, CancellationToken ct = default)
    {
        return await cacheService.GetOrSetAsync(
            $"products:new-arrivals:{count}",
            "products",
            async () =>
            {
                var result = await apiClient.GetAsync<PaginatedResult<ProductDto>>(
                    $"/api/products?sort=createdAt,desc&size={count}", ct);
                return result?.Items ?? [];
            },
            ct) ?? [];
    }

    public async Task<List<ProductDto>> GetPopularProductsAsync(int count = 8, CancellationToken ct = default)
    {
        return await cacheService.GetOrSetAsync(
            $"products:popular:{count}",
            "products",
            async () =>
            {
                var result = await apiClient.GetAsync<PaginatedResult<ProductDto>>(
                    $"/api/products?sort=salesCount,desc&size={count}", ct);
                return result?.Items ?? [];
            },
            ct) ?? [];
    }

    public async Task<List<ProductDto>> GetProductsByCategoryAsync(string categoryId, int count = 8, CancellationToken ct = default)
    {
        return await cacheService.GetOrSetAsync(
            $"products:category:{categoryId}:{count}",
            "products",
            async () =>
            {
                var result = await apiClient.GetAsync<PaginatedResult<ProductDto>>(
                    $"/api/products?category={categoryId}&size={count}", ct);
                return result?.Items ?? [];
            },
            ct) ?? [];
    }

    public async Task<SizeGuideDto?> GetSizeGuideAsync(string categoryId, CancellationToken ct = default)
        => await apiClient.GetAsync<SizeGuideDto>($"/api/size-guides/{categoryId}", ct);

    public async Task<List<ReviewDto>> GetProductReviewsAsync(string productId, int page = 0, int size = 10, CancellationToken ct = default)
    {
        var result = await apiClient.GetAsync<PaginatedResult<ReviewDto>>(
            $"/api/reviews/product/{productId}?page={page}&size={size}", ct);
        return result?.Items ?? [];
    }

    public async Task PostReviewAsync(string productId, CreateReviewRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("レビュー投稿: ProductId={ProductId}", productId);
        await apiClient.PostAsync<CreateReviewRequest, object>(
            "/api/reviews", request, ct);
    }

    public async Task<List<string>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var categories = await apiClient.GetAsync<List<CategoryApiResponse>>("/api/categories", ct);
        return categories?.Where(c => c.IsActive).Select(c => c.Name).ToList() ?? [];
    }

    /// <summary>
    /// カテゴリ API レスポンスの内部 DTO（InventoryManagementService の CategoryDto と対応）
    /// </summary>
    private sealed record CategoryApiResponse(
        string Id,
        string Name,
        string? Description,
        string? ParentId,
        int Level,
        string? Path,
        bool IsActive);
}

