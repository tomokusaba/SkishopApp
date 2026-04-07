using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.DTOs.Responses;

namespace InventoryManagementService.Services.Interfaces;

/// <summary>
/// 価格管理サービスのインターフェース。
/// 商品価格の設定・更新および価格変更履歴の取得を提供する。
/// </summary>
public interface IPriceService
{
    /// <summary>
    /// 商品の新規価格を設定する。既存のアクティブ価格は自動的に無効化される。
    /// 価格変更履歴も同時に記録する。
    /// </summary>
    /// <param name="request">価格作成リクエスト</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>作成された価格 DTO</returns>
    /// <exception cref="Exceptions.ResourceNotFoundException">商品が存在しない場合</exception>
    Task<PriceDto> CreateAsync(PriceCreateRequest request, string? changedBy = null, CancellationToken ct = default);

    /// <summary>
    /// 既存の価格情報を部分更新する。通常価格が変更された場合は履歴を記録し PriceUpdated イベントを発行する。
    /// </summary>
    /// <param name="id">価格 ID</param>
    /// <param name="request">更新リクエスト（null フィールドは更新対象外）</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>更新後の価格 DTO</returns>
    /// <exception cref="Exceptions.ResourceNotFoundException">価格レコードが存在しない場合</exception>
    Task<PriceDto> UpdateAsync(string id, PriceUpdateRequest request, string? changedBy = null, CancellationToken ct = default);

    /// <summary>
    /// 商品 ID でアクティブな価格情報を部分更新する。
    /// エンドポイント用に商品 ID から価格を取得し、価格 ID ベースの UpdateAsync に委譲する。
    /// </summary>
    /// <param name="productId">商品 ID</param>
    /// <param name="request">更新リクエスト（null フィールドは更新対象外）</param>
    /// <param name="changedBy">変更者</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>更新後の価格 DTO</returns>
    /// <exception cref="Exceptions.ResourceNotFoundException">商品のアクティブ価格が存在しない場合</exception>
    Task<PriceDto> UpdateByProductIdAsync(string productId, PriceUpdateRequest request, string? changedBy = null, CancellationToken ct = default);

    /// <summary>
    /// 商品 ID でアクティブな価格情報を取得する。Redis キャッシュを優先的に参照する。
    /// </summary>
    /// <param name="productId">商品 ID</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>価格 DTO。アクティブな価格が存在しない場合は null</returns>
    Task<PriceDto?> GetByProductIdAsync(string productId, CancellationToken ct = default);

    /// <summary>
    /// 複数商品 ID でアクティブな価格情報を一括取得する（N+1 防止用バッチ API）。
    /// </summary>
    /// <param name="productIds">商品 ID のリスト</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>価格 DTO のリスト。価格が存在しない商品は結果から除外される</returns>
    Task<List<PriceDto>> GetByProductIdsAsync(List<string> productIds, CancellationToken ct = default);

    /// <summary>
    /// 商品の価格変更履歴を取得する。ページネーション対応。適用日の降順でソートされる。
    /// </summary>
    /// <param name="productId">商品 ID</param>
    /// <param name="page">ページ番号（0 始まり）</param>
    /// <param name="size">ページサイズ</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>ページネーション付き価格変更履歴</returns>
    Task<PaginatedResult<PriceHistoryDto>> GetHistoryAsync(
        string productId, int page, int size, CancellationToken ct = default);
}
