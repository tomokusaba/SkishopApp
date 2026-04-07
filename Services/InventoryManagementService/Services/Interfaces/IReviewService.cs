using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.DTOs.Responses;

namespace InventoryManagementService.Services.Interfaces;

/// <summary>
/// レビュー管理サービスのインターフェース。
/// レビューの作成・承認・返信・投票および GDPR 匿名化処理を提供する。
/// </summary>
public interface IReviewService
{
    /// <summary>
    /// 新規レビューを作成する。同一ユーザーによる同一商品への重複レビューを防止する。
    /// レビューは PENDING ステータスで作成され、管理者による承認を待つ。
    /// </summary>
    /// <param name="request">レビュー作成リクエスト</param>
    /// <param name="userId">投稿ユーザー ID</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>作成されたレビュー DTO</returns>
    /// <exception cref="Exceptions.ResourceNotFoundException">商品が存在しない場合</exception>
    /// <exception cref="Exceptions.DuplicateResourceException">同一ユーザー・同一商品の組み合わせが既に存在する場合</exception>
    Task<ReviewDto> CreateAsync(ReviewCreateRequest request, string userId, CancellationToken ct = default);

    /// <summary>
    /// レビューのステータスを更新する（PENDING → APPROVED / REJECTED）。
    /// APPROVED 時は ReviewApproved イベントを発行する。
    /// </summary>
    /// <param name="id">レビュー ID</param>
    /// <param name="status">新しいステータス（APPROVED / REJECTED）</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>更新後のレビュー DTO</returns>
    /// <exception cref="Exceptions.ResourceNotFoundException">レビューが存在しない場合</exception>
    Task<ReviewDto> UpdateStatusAsync(string id, string status, CancellationToken ct = default);

    /// <summary>
    /// レビューに対する返信を追加する。Aggregate Root 経由で子エンティティを操作する。
    /// </summary>
    /// <param name="reviewId">レビュー ID</param>
    /// <param name="request">返信作成リクエスト</param>
    /// <param name="responderId">返信者のユーザー ID</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>更新後のレビュー DTO（返信を含む）</returns>
    /// <exception cref="Exceptions.ResourceNotFoundException">レビューが存在しない場合</exception>
    Task<ReviewDto> AddResponseAsync(string reviewId, ReviewResponseCreateRequest request, string responderId, CancellationToken ct = default);

    /// <summary>
    /// レビューに「参考になった」投票を追加する。同一ユーザーの重複投票を防止する。
    /// HelpfulCount をアトミックにインクリメントする。
    /// </summary>
    /// <param name="id">レビュー ID</param>
    /// <param name="userId">投票ユーザー ID</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>更新後のレビュー DTO</returns>
    /// <exception cref="Exceptions.ResourceNotFoundException">レビューが存在しない場合</exception>
    /// <exception cref="Exceptions.DuplicateResourceException">同一ユーザーが既に投票済みの場合</exception>
    Task<ReviewDto> MarkHelpfulAsync(string id, string userId, CancellationToken ct = default);

    /// <summary>
    /// レビュー ID でレビューを取得する（返信を含む）。
    /// </summary>
    /// <param name="id">レビュー ID</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>レビュー DTO。存在しない場合は null</returns>
    Task<ReviewDto?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// 商品 ID で承認済みレビュー一覧を取得する。ページネーション対応。
    /// </summary>
    /// <param name="productId">商品 ID</param>
    /// <param name="page">ページ番号（0 始まり）</param>
    /// <param name="size">ページサイズ</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>ページネーション付きレビュー一覧</returns>
    Task<PaginatedResult<ReviewDto>> GetByProductIdAsync(
        string productId, int page, int size, CancellationToken ct = default);

    /// <summary>
    /// GDPR DSR に基づきユーザーのレビュー・返信を匿名化する。
    /// ユーザー ID を DELETED_USER に置換する。
    /// </summary>
    /// <param name="userId">匿名化対象のユーザー ID</param>
    /// <param name="ct">キャンセルトークン</param>
    Task AnonymizeUserReviewsAsync(string userId, CancellationToken ct = default);
}
