using System.ComponentModel.DataAnnotations;

namespace InventoryManagementService.DTOs.Requests;

/// <summary>
/// サイズガイド新規作成リクエスト DTO。
/// POST /size-guides エンドポイントで使用する。
/// カテゴリに紐づくサイズ表とガイド種別を登録する。
/// </summary>
/// <param name="CategoryId">対象カテゴリ ID（必須）</param>
/// <param name="SizeChart">サイズ表データ（必須、JSON 形式）</param>
/// <param name="GuideType">ガイド種別（必須、最大 50 文字。例: "SKI_BOOTS", "SKI_WEAR"）</param>
public record SizeGuideCreateRequest(
    [Required]
    string CategoryId,
    [Required]
    string SizeChart,
    [Required, StringLength(50)]
    string GuideType);

/// <summary>
/// サイズガイド更新リクエスト DTO。
/// PUT /size-guides/{id} エンドポイントで使用する。
/// 指定されたフィールドのみ部分更新を行う。
/// </summary>
/// <param name="SizeChart">サイズ表データ（任意、JSON 形式）</param>
/// <param name="GuideType">ガイド種別（任意、最大 50 文字）</param>
public record SizeGuideUpdateRequest(
    string? SizeChart,
    [StringLength(50)]
    string? GuideType);
