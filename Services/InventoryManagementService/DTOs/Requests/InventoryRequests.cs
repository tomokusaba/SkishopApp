using System.ComponentModel.DataAnnotations;

namespace InventoryManagementService.DTOs.Requests;

/// <summary>
/// 在庫情報更新リクエスト DTO。
/// PUT /inventory エンドポイントで使用する。
/// 商品の在庫数量・保管場所・再発注ポイントを設定する。
/// </summary>
/// <param name="ProductId">対象商品 ID（必須）</param>
/// <param name="Quantity">在庫数量（0 以上）</param>
/// <param name="LocationCode">保管場所コード（必須、最大 20 文字）</param>
/// <param name="ReorderPoint">再発注ポイント（0 以上、デフォルト 0）</param>
public record InventoryUpdateRequest(
    [Required]
    string ProductId,
    [Range(0, int.MaxValue)]
    int Quantity,
    [Required, StringLength(20)]
    string LocationCode,
    [Range(0, int.MaxValue)]
    int ReorderPoint = 0);

/// <summary>
/// 入庫リクエスト DTO。
/// POST /inventory/stock-in エンドポイントで使用する。
/// 指定商品の在庫を加算する。
/// </summary>
/// <param name="ProductId">対象商品 ID（必須）</param>
/// <param name="Quantity">入庫数量（1 以上）</param>
/// <param name="ReferenceId">参照 ID（任意、発注番号等の外部参照）</param>
public record StockInRequest(
    [Required]
    string ProductId,
    [Range(1, int.MaxValue, ErrorMessage = "入庫数量は 1 以上を指定してください")]
    int Quantity,
    string? ReferenceId = null);

/// <summary>
/// 出庫リクエスト DTO。
/// POST /inventory/stock-out エンドポイントで使用する。
/// 指定商品の在庫を減算する。
/// </summary>
/// <param name="ProductId">対象商品 ID（必須）</param>
/// <param name="Quantity">出庫数量（1 以上）</param>
/// <param name="Reason">出庫理由（必須、デフォルト "ADJUSTMENT"）</param>
public record StockOutRequest(
    [Required]
    string ProductId,
    [Range(1, int.MaxValue, ErrorMessage = "出庫数量は 1 以上を指定してください")]
    int Quantity,
    [Required]
    string Reason = "ADJUSTMENT");

/// <summary>
/// 在庫予約アイテム DTO。
/// 注文確定フローで商品の在庫を予約する際に使用する。
/// </summary>
/// <param name="ProductId">予約対象の商品 ID</param>
/// <param name="Quantity">予約数量</param>
public record ReserveItemDto(string ProductId, int Quantity);
