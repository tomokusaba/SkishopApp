using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.DTOs.Responses;

namespace InventoryManagementService.Services.Interfaces;

/// <summary>
/// 在庫管理サービスのインターフェース。
/// 在庫の引当・解放・確定、入出庫処理、在庫状況照会、低在庫アラート、GDPR 匿名化を提供する。
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// 注文に対して複数商品の在庫を引き当てる。
    /// デッドロック防止のため商品 ID 順にソートし、悲観的ロック（SELECT FOR UPDATE）で排他制御する。
    /// </summary>
    /// <param name="orderId">注文 ID</param>
    /// <param name="items">引当対象の商品リスト（商品 ID と数量）</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>引当 ID（予約識別子）</returns>
    /// <exception cref="Exceptions.ResourceNotFoundException">在庫レコードが存在しない場合</exception>
    /// <exception cref="Exceptions.InventoryException">在庫不足の場合</exception>
    Task<string> ReserveAsync(string orderId, List<ReserveItemDto> items, CancellationToken ct = default);

    /// <summary>
    /// 引き当て済みの在庫を解放する（注文キャンセル時）。InventoryReleased イベントを発行する。
    /// </summary>
    /// <param name="orderId">注文 ID</param>
    /// <param name="reservationId">引当 ID</param>
    /// <param name="items">解放対象の商品リスト</param>
    /// <param name="ct">キャンセルトークン</param>
    Task ReleaseAsync(string orderId, string reservationId, List<ReserveItemDto> items, CancellationToken ct = default);

    /// <summary>
    /// 在庫引当を確定する（注文完了時）。引当済み数量を実在庫から差し引く。
    /// </summary>
    /// <param name="orderId">注文 ID</param>
    /// <param name="items">確定対象の商品リスト</param>
    /// <param name="ct">キャンセルトークン</param>
    Task ConfirmReservationAsync(string orderId, List<ReserveItemDto> items, CancellationToken ct = default);

    /// <summary>
    /// 入庫処理を実行する。悲観的ロックでの排他制御と InventoryUpdated イベント発行を行う。
    /// </summary>
    /// <param name="request">入庫リクエスト（商品 ID、数量、参照 ID）</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>更新後の在庫 DTO</returns>
    /// <exception cref="Exceptions.ResourceNotFoundException">在庫レコードが存在しない場合</exception>
    Task<InventoryDto> StockInAsync(StockInRequest request, CancellationToken ct = default);

    /// <summary>
    /// 出庫処理を実行する。在庫枯渇時は StockDepleted、低在庫時は LowStockAlert イベントを発行する。
    /// </summary>
    /// <param name="request">出庫リクエスト（商品 ID、数量、理由）</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>更新後の在庫 DTO</returns>
    /// <exception cref="Exceptions.ResourceNotFoundException">在庫レコードが存在しない場合</exception>
    /// <exception cref="Exceptions.InventoryException">在庫不足の場合</exception>
    Task<InventoryDto> StockOutAsync(StockOutRequest request, CancellationToken ct = default);

    /// <summary>
    /// 商品 ID で在庫情報を取得する。Redis キャッシュを優先的に参照する。
    /// </summary>
    /// <param name="productId">商品 ID</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>在庫 DTO。存在しない場合は null</returns>
    Task<InventoryDto?> GetByProductIdAsync(string productId, CancellationToken ct = default);

    /// <summary>
    /// 複数商品の在庫情報を一括取得する。
    /// </summary>
    /// <param name="productIds">商品 ID のリスト</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>在庫 DTO のリスト</returns>
    Task<List<InventoryDto>> GetByProductIdsAsync(List<string> productIds, CancellationToken ct = default);

    /// <summary>
    /// 商品の在庫ステータス（総数量・引当数量・有効数量・ステータス文字列）を取得する。
    /// </summary>
    /// <param name="productId">商品 ID</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>在庫ステータス DTO。存在しない場合は null</returns>
    Task<InventoryStatusDto?> GetStatusAsync(string productId, CancellationToken ct = default);

    /// <summary>
    /// 在庫数が閾値以下の低在庫商品一覧を取得する。ページネーション対応。
    /// </summary>
    /// <param name="threshold">低在庫閾値</param>
    /// <param name="page">ページ番号（0 始まり）</param>
    /// <param name="size">ページサイズ</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>ページネーション付き在庫一覧</returns>
    Task<PaginatedResult<InventoryDto>> GetLowStockAsync(
        int threshold, int page, int size, CancellationToken ct = default);

    /// <summary>
    /// GDPR DSR（データ主体権利）に基づくユーザーレビューの匿名化処理を実行する。
    /// 内部的に ReviewService に委譲する。
    /// </summary>
    /// <param name="userId">匿名化対象のユーザー ID</param>
    /// <param name="ct">キャンセルトークン</param>
    Task AnonymizeUserReviewsAsync(string userId, CancellationToken ct = default);
}
