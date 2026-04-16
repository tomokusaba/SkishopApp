using System.ComponentModel.DataAnnotations;
using McpServer.DTOs.Requests;
using McpServer.DTOs.Responses;
using McpServer.Services.Interfaces;

namespace McpServer.Services;

public sealed class CatalogToolService(
    IInventoryCatalogClient inventoryCatalogClient) : ICatalogToolService
{
    public async Task<PaginatedCatalogProductsResult> SearchProductsAsync(
        ProductSearchRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);

        return await inventoryCatalogClient.SearchProductsAsync(request, ct);
    }

    public async Task<CatalogProductDto?> GetProductByIdAsync(
        string productId,
        CancellationToken ct = default)
    {
        Validate(new ProductLookupRequest(productId));
        return await inventoryCatalogClient.GetProductByIdAsync(productId, ct);
    }

    public async Task<CatalogProductDto?> GetProductBySkuAsync(
        string sku,
        CancellationToken ct = default)
    {
        Validate(new ProductLookupRequest(sku));
        return await inventoryCatalogClient.GetProductBySkuAsync(sku, ct);
    }

    private static void Validate<T>(T instance)
    {
        var context = new ValidationContext(instance!);
        Validator.ValidateObject(instance!, context, validateAllProperties: true);
    }
}
