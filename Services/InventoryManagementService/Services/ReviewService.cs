using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.DTOs.Responses;
using InventoryManagementService.Events;
using InventoryManagementService.Exceptions;
using InventoryManagementService.Models;
using InventoryManagementService.Repositories.Interfaces;
using InventoryManagementService.Services.Interfaces;

namespace InventoryManagementService.Services;

/// <summary>
/// レビュー管理サービスの実装クラス。
/// レビューのライフサイクル（PENDING → APPROVED/REJECTED）、返信、投票、GDPR 匿名化を管理する。
/// </summary>
/// <remarks>
/// <para>重複防止: 同一ユーザーによる同一商品への重複レビューおよび重複投票を防止する。</para>
/// <para>Aggregate Root: 返信は Review エンティティ（Aggregate Root）経由で追加し、DDD の境界を維持する。</para>
/// <para>アトミック更新: HelpfulCount は ExecuteUpdateAsync によるアトミックインクリメントで更新する。</para>
/// <para>GDPR: AnonymizeUserReviewsAsync でユーザー ID を DELETED_USER に置換し、個人情報を匿名化する。</para>
/// </remarks>
public class ReviewService(
    IReviewRepository reviewRepository,
    IProductRepository productRepository,
    IEventPublisherService eventPublisher,
    ILogger<ReviewService> logger) : IReviewService
{
    /// <inheritdoc />
    public async Task<ReviewDto> CreateAsync(
        ReviewCreateRequest request, string userId, CancellationToken ct = default)
    {
        _ = await productRepository.FindByIdAsync(request.ProductId, ct)
            ?? throw new ResourceNotFoundException("Product", request.ProductId);

        if (await reviewRepository.ExistsByProductAndUserAsync(request.ProductId, userId, ct))
            throw new DuplicateResourceException("Review", "productId+userId",
                $"{request.ProductId}:{userId}");

        var review = new Review
        {
            ProductId = request.ProductId,
            UserId = userId,
            Rating = request.Rating,
            Title = request.Title,
            Content = request.Content,
            Status = "PENDING"
        };

        await reviewRepository.AddAsync(review, ct);

        await eventPublisher.PublishAsync("ReviewCreated", "inventory.reviews", review.Id, "Review",
            new ReviewCreatedEvent(review.Id, review.ProductId, userId, review.Rating,
                DateTimeOffset.UtcNow), ct);

        await reviewRepository.SaveChangesAsync(ct);

        logger.LogInformation("レビュー作成完了: ReviewId={ReviewId}, ProductId={ProductId}",
            review.Id, review.ProductId);
        return MapToDto(review);
    }

    /// <inheritdoc />
    /// <remarks>
    /// ドメインメソッド Approve() / Reject() を使用してステータス遷移のビジネスルール（PENDING からのみ遷移可能）を
    /// エンティティ側で保証する。APPROVED 時のみ ReviewApproved イベントを Outbox に登録する。
    /// </remarks>
    public async Task<ReviewDto> UpdateStatusAsync(
        string id, string status, CancellationToken ct = default)
    {
        var review = await reviewRepository.FindByIdAsync(id, ct)
            ?? throw new ResourceNotFoundException("Review", id);

        switch (status.ToUpperInvariant())
        {
            case "APPROVED":
                review.Approve();
                await eventPublisher.PublishAsync("ReviewApproved", "inventory.reviews", review.Id, "Review",
                    new ReviewApprovedEvent(review.Id, review.ProductId, review.Rating,
                        DateTimeOffset.UtcNow), ct);
                break;
            case "REJECTED":
                review.Reject();
                break;
            default:
                throw new InventoryException(ErrorCodes.InvalidOperation, $"不正なステータス: {status}");
        }

        await reviewRepository.SaveChangesAsync(ct);

        logger.LogInformation("レビューステータス更新: ReviewId={ReviewId}, Status={Status}",
            id, status);
        return MapToDto(review);
    }

    /// <inheritdoc />
    public async Task<ReviewDto> AddResponseAsync(
        string reviewId, ReviewResponseCreateRequest request, string responderId,
        CancellationToken ct = default)
    {
        var review = await reviewRepository.FindByIdAsync(reviewId, ct)
            ?? throw new ResourceNotFoundException("Review", reviewId);

        review.AddResponse(responderId, request.Content);
        await reviewRepository.SaveChangesAsync(ct);

        logger.LogInformation("レビュー返信追加: ReviewId={ReviewId}",
            reviewId);
        return MapToDto(review);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>トランザクション + 投票重複排除: 明示的トランザクション内で以下をアトミックに実行する。</para>
    /// <list type="number">
    ///   <item><description>ReviewVote テーブルにユーザー投票レコードを追加（重複防止のための一意制約）</description></item>
    ///   <item><description>ExecuteUpdateAsync で HelpfulCount をアトミックにインクリメント（楽観的ロック競合の回避）</description></item>
    /// </list>
    /// <para>重複投票は HasUserVotedAsync で事前チェックし、DuplicateResourceException を送出する。</para>
    /// </remarks>
    public async Task<ReviewDto> MarkHelpfulAsync(string id, string userId, CancellationToken ct = default)
    {
        _ = await reviewRepository.FindByIdAsync(id, ct)
            ?? throw new ResourceNotFoundException("Review", id);

        if (await reviewRepository.HasUserVotedAsync(id, userId, ct))
            throw new DuplicateResourceException("ReviewVote", "reviewId+userId", $"{id}:{userId}");

        await using var transaction = await reviewRepository.BeginTransactionAsync(ct);
        try
        {
            await reviewRepository.AddVoteAsync(new ReviewVote { ReviewId = id, UserId = userId }, ct);
            await reviewRepository.IncrementHelpfulCountAsync(id, ct);
            await reviewRepository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var updated = await reviewRepository.FindByIdAsync(id, ct);
            logger.LogInformation("レビュー参考になった: ReviewId={ReviewId}, UserId={UserId}", id, userId);
            return MapToDto(updated!);
        }
        // H-24: catch 内ログ追加
        catch (Exception ex)
        {
            logger.LogError(ex, "レビュー参考になった処理失敗、ロールバック: ReviewId={ReviewId}, UserId={UserId}", id, userId);
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ReviewDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var review = await reviewRepository.FindByIdAsync(id, ct);
        return review is null ? null : MapToDto(review);
    }

    /// <inheritdoc />
    public async Task<PaginatedResult<ReviewDto>> GetByProductIdAsync(
        string productId, int page, int size, CancellationToken ct = default)
    {
        var reviews = await reviewRepository.FindByProductIdAsync(productId, page, size, ct);
        var total = await reviewRepository.CountByProductIdAsync(productId, ct);
        return new PaginatedResult<ReviewDto>(reviews.Select(MapToDto).ToList(), total, page, size);
    }

    /// <inheritdoc />
    public async Task AnonymizeUserReviewsAsync(string userId, CancellationToken ct = default)
    {
        var reviews = await reviewRepository.FindByUserIdAsync(userId, ct);
        foreach (var review in reviews)
            review.UserId = "DELETED_USER";

        var responses = await reviewRepository.FindResponsesByResponderIdAsync(userId, ct);
        foreach (var response in responses)
            response.ResponderId = "DELETED_USER";

        await reviewRepository.SaveChangesAsync(ct);
        logger.LogInformation(
            "GDPR DSR レビュー匿名化完了: Reviews={ReviewCount}, Responses={ResponseCount}",
            reviews.Count, responses.Count);
    }

    /// <summary>
    /// Review エンティティを ReviewDto に変換する（返信を含む）。
    /// </summary>
    private static ReviewDto MapToDto(Review r) => new(
        r.Id, r.ProductId, r.UserId, r.Rating, r.Title, r.Content,
        r.IsVerifiedPurchase, r.HelpfulCount, r.Status,
        r.Responses.Select(resp => new ReviewResponseDto(
            resp.Id, resp.ReviewId, resp.ResponderId, resp.Content, resp.CreatedAt)).ToList(),
        r.CreatedAt);
}
