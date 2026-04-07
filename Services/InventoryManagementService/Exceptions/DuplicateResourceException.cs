namespace InventoryManagementService.Exceptions;

/// <summary>
/// リソースの重複作成が検出された場合にスローされる例外。HTTP 409 Conflict にマッピングされる。
/// </summary>
/// <param name="resourceType">リソース種別（例: "Product", "Category"）。</param>
/// <param name="key">重複が検出されたフィールド名（例: "Sku", "Email"）。</param>
/// <param name="value">重複した値。</param>
public class DuplicateResourceException(string resourceType, string key, string value)
    : InventoryException("DUPLICATE_RESOURCE",
        $"{resourceType} は既に存在します ({key}: {value})",
        new Dictionary<string, object> { ["resourceType"] = resourceType, [key] = value });
