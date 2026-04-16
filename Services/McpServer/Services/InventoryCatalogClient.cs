using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using McpServer.DTOs.Requests;
using McpServer.DTOs.Responses;
using McpServer.Services.Interfaces;
using Microsoft.AspNetCore.WebUtilities;

namespace McpServer.Services;

public sealed class InventoryCatalogClient(HttpClient httpClient, ILogger<InventoryCatalogClient> logger) : IInventoryCatalogClient
{
    public async Task<PaginatedCatalogProductsResult> SearchProductsAsync(ProductSearchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestPath = QueryHelpers.AddQueryString("/api/products/search", new Dictionary<string, string?>
        {
            ["keyword"] = request.Keyword,
            ["categoryId"] = request.CategoryId,
            ["category"] = request.Category,
            ["brand"] = request.Brand,
            ["page"] = request.Page.ToString(CultureInfo.InvariantCulture),
            ["size"] = request.Size.ToString(CultureInfo.InvariantCulture)
        });

        using var response = await httpClient.GetAsync(requestPath, ct);
        return await ReadRequiredAsync<PaginatedCatalogProductsResult>(response, "search products", ct);
    }

    public async Task<CatalogProductDto?> GetProductByIdAsync(string productId, CancellationToken ct = default)
    {
        var requestPath = $"/api/products/{Uri.EscapeDataString(productId)}";

        using var response = await httpClient.GetAsync(requestPath, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadRequiredAsync<CatalogProductDto>(response, "get product by id", ct);
    }

    public async Task<CatalogProductDto?> GetProductBySkuAsync(string sku, CancellationToken ct = default)
    {
        var requestPath = $"/api/products/sku/{Uri.EscapeDataString(sku)}";

        using var response = await httpClient.GetAsync(requestPath, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        return await ReadRequiredAsync<CatalogProductDto>(response, "get product by sku", ct);
    }

    private async Task<T> ReadRequiredAsync<T>(HttpResponseMessage response, string operation, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Inventory API request failed during {Operation}: {StatusCode}",
                operation,
                (int)response.StatusCode);

            throw new HttpRequestException(
                $"Inventory API request failed during {operation} with status code {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
        return payload
            ?? throw new InvalidOperationException($"Inventory API returned an empty payload during {operation}.");
    }
}
