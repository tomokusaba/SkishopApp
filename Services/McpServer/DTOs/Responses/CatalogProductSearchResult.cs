namespace McpServer.DTOs.Responses;

public sealed record CatalogProductSearchResult(
    bool Succeeded,
    string? Message,
    PaginatedCatalogProductsResult Products);
