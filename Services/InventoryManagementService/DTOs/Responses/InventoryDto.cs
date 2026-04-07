namespace InventoryManagementService.DTOs.Responses;

/// <summary>
/// 在庫情報レスポンス DTO。
/// 在庫 API のレスポンスとして返却される。
/// 在庫数量・予約数量から算出した利用可能数量を含む。
/// </summary>
/// <param name="Id">在庫レコード ID</param>
/// <param name="ProductId">商品 ID</param>
/// <param name="Quantity">在庫数量（物理在庫）</param>
/// <param name="ReservedQuantity">予約済み数量</param>
/// <param name="AvailableQuantity">利用可能数量（Quantity - ReservedQuantity）</param>
/// <param name="LocationCode">保管場所コード</param>
/// <param name="Status">在庫ステータス（IN_STOCK, LOW_STOCK, OUT_OF_STOCK 等）</param>
/// <param name="ReorderPoint">再発注ポイント</param>
// M-9: sealed record 追加
public sealed record InventoryDto(
    string Id,
    string ProductId,
    int Quantity,
    int ReservedQuantity,
    int AvailableQuantity,
    string LocationCode,
    string Status,
    int ReorderPoint);

/// <summary>
/// 在庫ステータスサマリー DTO。
/// 商品単位の在庫状況を集約したサマリー情報を返す。
/// 複数ロケーションの合計値を含む。
/// </summary>
/// <param name="ProductId">商品 ID</param>
/// <param name="TotalQuantity">合計在庫数量</param>
/// <param name="TotalReserved">合計予約済み数量</param>
/// <param name="TotalAvailable">合計利用可能数量</param>
/// <param name="Status">総合在庫ステータス</param>
// M-9: sealed record 追加
public sealed record InventoryStatusDto(
    string ProductId,
    int TotalQuantity,
    int TotalReserved,
    int TotalAvailable,
    string Status);
