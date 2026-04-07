namespace InventoryManagementService.DTOs.Responses;

/// <summary>
/// サイズガイドレスポンス DTO。
/// サイズガイド API のレスポンスとして返却される。
/// カテゴリに紐づくサイズ表データとガイド種別を含む。
/// </summary>
/// <param name="Id">サイズガイド ID</param>
/// <param name="CategoryId">対象カテゴリ ID</param>
/// <param name="SizeChart">サイズ表データ（JSON 形式）</param>
/// <param name="GuideType">ガイド種別（例: "SKI_BOOTS", "SKI_WEAR"）</param>
/// <param name="CreatedAt">作成日時</param>
/// <param name="UpdatedAt">最終更新日時</param>
// M-12: sealed record 追加
public sealed record SizeGuideDto(
    string Id,
    string CategoryId,
    string SizeChart,
    string GuideType,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
