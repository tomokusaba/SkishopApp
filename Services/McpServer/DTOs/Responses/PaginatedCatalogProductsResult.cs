namespace McpServer.DTOs.Responses;

public sealed record PaginatedCatalogProductsResult(
    IReadOnlyList<CatalogProductDto> Items,
    long TotalElements,
    int Page,
    int Size)
{
    public int TotalPages => Size == 0 ? 0 : (int)Math.Ceiling((double)TotalElements / Size);

    public bool HasNext => Page < TotalPages - 1;

    public bool HasPrevious => Page > 0;
}
