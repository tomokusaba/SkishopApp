using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using McpServer.DTOs.Requests;
using McpServer.DTOs.Responses;
using McpServer.Services.Interfaces;
using ModelContextProtocol.Server;
using Polly.CircuitBreaker;

namespace McpServer.Tools;

[McpServerToolType]
public sealed class CatalogTools(
    ICatalogToolService catalogToolService,
    ILogger<CatalogTools> logger)
{
    [McpServerTool]
    [Description("Search the read-only SkiShop catalog. Use keyword, categoryId, category, and brand filters. Page is zero-based and size must be between 1 and 100.")]
    public async Task<CatalogProductSearchResult> SearchProductsAsync(
        [Description("Optional keyword filter up to 200 characters.")] string? keyword = null,
        [Description("Optional category ID filter up to 128 characters.")] string? categoryId = null,
        [Description("Optional category name filter up to 128 characters.")] string? category = null,
        [Description("Optional brand filter up to 128 characters.")] string? brand = null,
        [Description("Zero-based page number. Must be 0 or greater.")] int page = 0,
        [Description("Page size. Must be between 1 and 100.")] int size = 20,
        CancellationToken ct = default)
    {
        try
        {
            var products = await catalogToolService.SearchProductsAsync(
                new ProductSearchRequest(keyword, categoryId, category, brand, page, size),
                ct);

            return new CatalogProductSearchResult(true, null, products);
        }
        catch (ValidationException ex)
        {
            logger.LogWarning(ex, "Catalog search validation failed");
            return new CatalogProductSearchResult(false, ex.Message, CreateEmptySearchResult(page, size));
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Inventory catalog request failed during SearchProducts");
            return new CatalogProductSearchResult(
                false,
                "Inventory catalog is temporarily unavailable.",
                CreateEmptySearchResult(page, size));
        }
        catch (TimeoutException ex)
        {
            logger.LogWarning(ex, "Inventory catalog request timed out during SearchProducts");
            return new CatalogProductSearchResult(
                false,
                "Inventory catalog request timed out. Please try again later.",
                CreateEmptySearchResult(page, size));
        }
        catch (BrokenCircuitException ex)
        {
            logger.LogWarning(ex, "Inventory catalog circuit breaker is open during SearchProducts");
            return new CatalogProductSearchResult(
                false,
                "Inventory catalog is temporarily unavailable.",
                CreateEmptySearchResult(page, size));
        }
    }

    [McpServerTool]
    [Description("Get a single SkiShop catalog product by its exact internal product ID. The productId must be a non-empty string up to 128 characters.")]
    public async Task<CatalogProductLookupResult> GetProductByIdAsync(
        [Description("Exact SkiShop product ID. Non-empty and up to 128 characters.")] string productId,
        CancellationToken ct = default)
    {
        try
        {
            var product = await catalogToolService.GetProductByIdAsync(productId, ct);
            return product is null
                ? new CatalogProductLookupResult(false, "No catalog product was found for the specified product ID.", null)
                : new CatalogProductLookupResult(true, null, product);
        }
        catch (ValidationException ex)
        {
            logger.LogWarning(ex, "Catalog product lookup by id validation failed");
            return new CatalogProductLookupResult(false, ex.Message, null);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Inventory catalog request failed during GetProductById");
            return new CatalogProductLookupResult(false, "Inventory catalog is temporarily unavailable.", null);
        }
        catch (TimeoutException ex)
        {
            logger.LogWarning(ex, "Inventory catalog request timed out during GetProductById");
            return new CatalogProductLookupResult(false, "Inventory catalog request timed out. Please try again later.", null);
        }
        catch (BrokenCircuitException ex)
        {
            logger.LogWarning(ex, "Inventory catalog circuit breaker is open during GetProductById");
            return new CatalogProductLookupResult(false, "Inventory catalog is temporarily unavailable.", null);
        }
    }

    [McpServerTool]
    [Description("Get a single SkiShop catalog product by its exact SKU code. The sku must be a non-empty string up to 128 characters.")]
    public async Task<CatalogProductLookupResult> GetProductBySkuAsync(
        [Description("Exact SKU code. Non-empty and up to 128 characters.")] string sku,
        CancellationToken ct = default)
    {
        try
        {
            var product = await catalogToolService.GetProductBySkuAsync(sku, ct);
            return product is null
                ? new CatalogProductLookupResult(false, "No catalog product was found for the specified SKU.", null)
                : new CatalogProductLookupResult(true, null, product);
        }
        catch (ValidationException ex)
        {
            logger.LogWarning(ex, "Catalog product lookup by SKU validation failed");
            return new CatalogProductLookupResult(false, ex.Message, null);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Inventory catalog request failed during GetProductBySku");
            return new CatalogProductLookupResult(false, "Inventory catalog is temporarily unavailable.", null);
        }
        catch (TimeoutException ex)
        {
            logger.LogWarning(ex, "Inventory catalog request timed out during GetProductBySku");
            return new CatalogProductLookupResult(false, "Inventory catalog request timed out. Please try again later.", null);
        }
        catch (BrokenCircuitException ex)
        {
            logger.LogWarning(ex, "Inventory catalog circuit breaker is open during GetProductBySku");
            return new CatalogProductLookupResult(false, "Inventory catalog is temporarily unavailable.", null);
        }
    }

    private static PaginatedCatalogProductsResult CreateEmptySearchResult(int page, int size)
    {
        var safePage = Math.Max(page, 0);
        var safeSize = size is >= 1 and <= 100 ? size : 20;
        return new PaginatedCatalogProductsResult([], 0, safePage, safeSize);
    }
}
