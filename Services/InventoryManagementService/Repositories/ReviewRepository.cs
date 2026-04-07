using InventoryManagementService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;
using InventoryManagementService.Models;
using InventoryManagementService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagementService.Repositories;

/// <summary>
/// レビューリポジトリの EF Core 実装クラス。
/// </summary>
/// <remarks>
/// <para>投票追跡: ReviewVotes テーブルで重複投票を防止する。</para>
/// <para>アトミック更新: IncrementHelpfulCountAsync は ExecuteUpdateAsync でアトミックにカウントをインクリメントする。</para>
/// <para>承認フィルタ: FindByProductIdAsync は APPROVED ステータスのレビューのみを返す。</para>
/// </remarks>
public class ReviewRepository(AppDbContext context) : IReviewRepository
{
    /// <inheritdoc />
    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default)
        => await context.Database.BeginTransactionAsync(ct);

    /// <inheritdoc />
    public async Task<Review?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.Reviews
            .Include(r => r.Responses)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <inheritdoc />
    /// <remarks>
    /// APPROVED ステータスのレビューのみを返す。Responses（返信）を Include し、
    /// 作成日降順でページネーションを適用する。AsNoTracking で読み取り最適化を行う。
    /// </remarks>
    public async Task<List<Review>> FindByProductIdAsync(
        string productId, int page, int size, CancellationToken ct = default)
        => await context.Reviews.AsNoTracking()
            .Where(r => r.ProductId == productId && r.Status == "APPROVED")
            .OrderByDescending(r => r.CreatedAt)
            .Skip(page * size).Take(size)
            .Include(r => r.Responses)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<Review>> FindByUserIdAsync(string userId, CancellationToken ct = default)
        => await context.Reviews
            .Where(r => r.UserId == userId)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<long> CountByProductIdAsync(string productId, CancellationToken ct = default)
        => await context.Reviews.LongCountAsync(
            r => r.ProductId == productId && r.Status == "APPROVED", ct);

    /// <inheritdoc />
    public async Task<bool> ExistsByProductAndUserAsync(
        string productId, string userId, CancellationToken ct = default)
        => await context.Reviews.AnyAsync(
            r => r.ProductId == productId && r.UserId == userId, ct);

    /// <inheritdoc />
    public async Task AddAsync(Review review, CancellationToken ct = default)
        => await context.Reviews.AddAsync(review, ct);

    /// <inheritdoc />
    public async Task AddResponseAsync(ReviewResponse response, CancellationToken ct = default)
        => await context.ReviewResponses.AddAsync(response, ct);

    /// <inheritdoc />
    public async Task<List<ReviewResponse>> FindResponsesByResponderIdAsync(
        string responderId, CancellationToken ct = default)
        => await context.ReviewResponses
            .Where(r => r.ResponderId == responderId)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<bool> HasUserVotedAsync(string reviewId, string userId, CancellationToken ct = default)
        => await context.ReviewVotes.AnyAsync(v => v.ReviewId == reviewId && v.UserId == userId, ct);

    /// <inheritdoc />
    public async Task AddVoteAsync(ReviewVote vote, CancellationToken ct = default)
        => await context.ReviewVotes.AddAsync(vote, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// HelpfulCount をアトミックにインクリメントする。
    /// DDD 的には Review Aggregate Root 経由での更新が望ましいが、
    /// 楽観的ロック競合を回避するパフォーマンス最適化として ExecuteUpdateAsync を採用。
    /// ReviewService.MarkHelpfulAsync で Review の存在確認後に実行されるため、
    /// ビジネスルールは保護されている。
    /// </para>
    /// <para>
    /// ExecuteUpdateAsync を使用して HelpfulCount をアトミックにインクリメントする。
    /// エンティティをロードせず、UPDATE reviews SET helpful_count = helpful_count + 1 を直接発行するため、
    /// 同時投票による競合を排除する。
    /// </para>
    /// </remarks>
    public async Task IncrementHelpfulCountAsync(string reviewId, CancellationToken ct = default)
        => await context.Reviews
            .Where(r => r.Id == reviewId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.HelpfulCount, r => r.HelpfulCount + 1), ct);

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
