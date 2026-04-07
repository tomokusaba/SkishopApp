namespace InventoryManagementService.Exceptions;

/// <summary>
/// リソースが見つからない場合にスローされる例外。HTTP 404 Not Found にマッピングされる。
/// </summary>
/// <param name="resourceType">リソース種別（例: "Product", "Category"）。</param>
/// <param name="resourceId">検索対象のリソース ID。</param>
public class ResourceNotFoundException(string resourceType, string resourceId)
    : InventoryException("RESOURCE_NOT_FOUND",
        $"{resourceType} が見つかりません (ID: {resourceId})",
        new Dictionary<string, object> { ["resourceType"] = resourceType, ["resourceId"] = resourceId });
