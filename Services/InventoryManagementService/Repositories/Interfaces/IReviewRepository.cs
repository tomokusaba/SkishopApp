using InventoryManagementService.Models;

using Microsoft.EntityFrameworkCore.Storage;

namespace InventoryManagementService.Repositories.Interfaces;

/// <summary>
/// レビューリポジトリのインターフェース。Review Aggregate Root および関連エンティティのデータアクセスを提供する。
/// 投票追跡と HelpfulCount のアトミック更新を含む。
/// </summary>
public interface IReviewRepository
{
    /// <summary>
    /// レビュー ID でレビューを取得する。Responses（返信）を Include する。
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
    Task<Review?> FindByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// 商品 ID で承認済みレビューを取得する（作成日降順、ページネーション対応、Responses を Include）。
    /// </summary>
    Task<List<Review>> FindByProductIdAsync(string productId, int page, int size, CancellationToken ct = default);

    /// <summary>
    /// ユーザー ID でレビューを取得する。GDPR 匿名化処理用。
    /// </summary>
    Task<List<Review>> FindByUserIdAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// 商品 ID の承認済みレビュー総件数を取得する。
    /// </summary>
    Task<long> CountByProductIdAsync(string productId, CancellationToken ct = default);

    /// <summary>
    /// 同一商品・同一ユーザーのレビューが存在するかを確認する。重複防止用。
    /// </summary>
    Task<bool> ExistsByProductAndUserAsync(string productId, string userId, CancellationToken ct = default);

    /// <summary>
    /// レビューエンティティを追加する。
    /// </summary>
    Task AddAsync(Review review, CancellationToken ct = default);

    /// <summary>
    /// レビュー返信エンティティを追加する。
    /// </summary>
    Task AddResponseAsync(ReviewResponse response, CancellationToken ct = default);

    /// <summary>
    /// 返信者 ID でレビュー返信を取得する。GDPR 匿名化処理用。
    /// </summary>
    Task<List<ReviewResponse>> FindResponsesByResponderIdAsync(string responderId, CancellationToken ct = default);

    /// <summary>
    /// 指定レビューに対してユーザーが既に投票済みかを確認する。重複投票防止用。
    /// </summary>
    Task<bool> HasUserVotedAsync(string reviewId, string userId, CancellationToken ct = default);

    /// <summary>
    /// レビュー投票エンティティを追加する。
    /// </summary>
    Task AddVoteAsync(ReviewVote vote, CancellationToken ct = default);

    /// <summary>
    /// レビューの HelpfulCount をアトミックにインクリメントする（ExecuteUpdateAsync 使用）。
    /// </summary>
    Task IncrementHelpfulCountAsync(string reviewId, CancellationToken ct = default);

    /// <summary>
    /// 変更をデータベースに永続化する。
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
