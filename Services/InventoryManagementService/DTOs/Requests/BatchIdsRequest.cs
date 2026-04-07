using System.ComponentModel.DataAnnotations;

namespace InventoryManagementService.DTOs.Requests;

/// <summary>
/// 複数リソースの一括取得リクエスト DTO。
/// GET /products/batch および GET /inventory/batch エンドポイントで使用する。
/// 最大 50 件の ID を指定して一括取得を行う。
/// </summary>
/// <param name="Ids">取得対象の ID リスト（1〜50 件）</param>
public record BatchIdsRequest(
    [Required]
    [MinLength(1, ErrorMessage = "ID リストは 1 件以上指定してください")]
    [MaxLength(50, ErrorMessage = "一括取得の上限は 50 件です")]
    List<string> Ids);
