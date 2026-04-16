namespace McpServer.DTOs.Responses;

public sealed record CatalogProductDto(
    string Id,
    string Sku,
    string Name,
    string? Description,
    string? Brand,
    string? CategoryName,
    decimal? Weight,
    bool IsActive,
    DateTimeOffset CreatedAt);
