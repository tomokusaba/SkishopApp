using System.ComponentModel.DataAnnotations;

namespace McpServer.DTOs.Requests;

public sealed record ProductLookupRequest(
    [property: Required]
    [property: StringLength(128, MinimumLength = 1)]
    string Value);
