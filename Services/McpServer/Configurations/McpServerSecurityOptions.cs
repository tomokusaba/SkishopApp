using System.ComponentModel.DataAnnotations;

namespace McpServer.Configurations;

public sealed class McpServerSecurityOptions
{
    public const string SectionName = "McpServer:Security";
    public const string ApiKeyHeaderName = "X-Api-Key";

    [Required]
    [MinLength(32)]
    public string ApiKey { get; init; } = string.Empty;
}
