using System.ComponentModel.DataAnnotations;

namespace McpServer.DTOs.Requests;

public sealed record ProductSearchRequest(
    [property: StringLength(200)]
    string? Keyword = null,
    [property: StringLength(128)]
    string? CategoryId = null,
    [property: StringLength(128)]
    string? Category = null,
    [property: StringLength(128)]
    string? Brand = null,
    [property: Range(0, int.MaxValue)]
    int Page = 0,
    [property: Range(1, 100)]
    int Size = 20);
