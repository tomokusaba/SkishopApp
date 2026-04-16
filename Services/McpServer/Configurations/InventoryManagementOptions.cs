using System.ComponentModel.DataAnnotations;

namespace McpServer.Configurations;

public sealed class InventoryManagementOptions
{
    public const string SectionName = "Services:InventoryManagement";

    [Required]
    [Url]
    public string BaseUrl { get; init; } = string.Empty;
}
