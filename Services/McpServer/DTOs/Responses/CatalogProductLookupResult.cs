namespace McpServer.DTOs.Responses;

public sealed record CatalogProductLookupResult(
    bool Found,
    string? Message,
    CatalogProductDto? Product);
