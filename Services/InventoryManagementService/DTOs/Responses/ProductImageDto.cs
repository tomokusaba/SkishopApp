namespace InventoryManagementService.DTOs.Responses;

/// <summary>
/// 商品画像レスポンス DTO。
/// 商品に紐づく画像の情報を返す。
/// サムネイル URL・表示順序・代替テキストを含む。
/// </summary>
/// <param name="Id">画像 ID</param>
/// <param name="Url">画像の URL</param>
/// <param name="ThumbnailUrl">サムネイル画像の URL</param>
/// <param name="Type">画像種別（MAIN, SUB, DETAIL 等）</param>
/// <param name="SortOrder">表示順序</param>
/// <param name="AltText">代替テキスト（アクセシビリティ用）</param>
// M-12: sealed record 追加
public sealed record ProductImageDto(
    string Id,
    string Url,
    string? ThumbnailUrl,
    string Type,
    int SortOrder,
    string? AltText);
