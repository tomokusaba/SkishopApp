using McpServer.DTOs.Requests;
using McpServer.DTOs.Responses;

namespace McpServer.Services.Interfaces;

public interface ICatalogToolService
{
    Task<PaginatedCatalogProductsResult> SearchProductsAsync(ProductSearchRequest request, CancellationToken ct = default);

    Task<CatalogProductDto?> GetProductByIdAsync(string productId, CancellationToken ct = default);

    Task<CatalogProductDto?> GetProductBySkuAsync(string sku, CancellationToken ct = default);
}
