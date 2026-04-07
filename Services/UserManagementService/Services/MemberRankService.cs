using UserManagementService.DTOs.Responses;
using UserManagementService.Exceptions;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.Services;

/// <summary>
/// 会員ランクのビジネスロジック。購入金額による自動昇格と年次評価（降格含む）を担当する。
/// 年次評価は分散ロックで排他制御し、バッチサイズ 500 件で処理する。
/// </summary>
public class MemberRankService(
    IMemberRankRepository memberRankRepository,
    IProcessedEventRepository processedEventRepository,
    IEventPublisherService eventPublisher,
    TimeProvider timeProvider,
    ILogger<MemberRankService> logger) : IMemberRankService
{
    /// <summary>年次ランク評価のバッチ処理サイズ。メモリ消費を抑えつつ大量ユーザーを処理する。</summary>
    private const int EvaluationBatchSize = 500;

    public async Task<MemberRankDto?> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        var rank = await memberRankRepository.FindByUserIdReadOnlyAsync(userId, ct);
        return rank is null ? null : MapToDto(rank);
    }

    public async Task InitializeAsync(string userId, CancellationToken ct = default)
    {
        var existing = await memberRankRepository.FindByUserIdAsync(userId, ct);
        if (existing is not null) return;

        var rank = MemberRank.CreateDefault(userId, timeProvider.GetUtcNow());

        await memberRankRepository.AddAsync(rank, ct);
        await memberRankRepository.SaveChangesAsync(ct);
        logger.LogInformation("会員ランクが初期化されました: {UserId}", userId);
    }

    /// <summary>
    /// 注文確定イベントによる購入金額の累積と自動昇格判定。
    /// <see cref="IProcessedEventRepository"/> でイベントIDの重複処理を防止する（べき等性保証）。
    /// </summary>
    public async Task AddPurchaseAmountAsync(
        string userId, string orderId, decimal amount, CancellationToken ct = default)
    {
        if (await processedEventRepository.ExistsAsync(orderId, "OrderConfirmed", ct))
        {
            logger.LogInformation("重複イベントをスキップ: OrderId={OrderId}, UserId={UserId}", orderId, userId);
            return;
        }

        var rank = await memberRankRepository.FindByUserIdAsync(userId, ct)
            ?? throw new NotFoundException($"会員ランクが見つかりません (UserId: {userId})");

        var promotion = rank.AddPurchaseAmount(amount, timeProvider.GetUtcNow());
        if (promotion is not null)
        {
            logger.LogInformation("会員ランク昇格: {UserId}, {OldRank} → {NewRank}",
                userId, promotion.Value.PreviousRank, promotion.Value.CurrentRank);

            await eventPublisher.PublishMemberRankUpdatedAsync(
                userId,
                promotion.Value.PreviousRank,
                promotion.Value.CurrentRank,
                promotion.Value.PointRate,
                ct);
        }

        await processedEventRepository.AddAsync(new ProcessedEvent
        {
            EventId = orderId,
            EventType = "OrderConfirmed",
            ProcessedAt = timeProvider.GetUtcNow()
        }, ct);

        await memberRankRepository.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 全ユーザーの年次ランク評価をバッチ実行する。ランク変動時は member-rank.updated イベントを発行する。
    /// </summary>
    public async Task EvaluateAllRanksAsync(CancellationToken ct = default)
    {
        var evaluationDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var processedCount = 0;

        while (true)
        {
            var ranks = await memberRankRepository.FindAllForEvaluationAsync(
                evaluationDate,
                EvaluationBatchSize,
                ct);
            if (ranks.Count == 0)
                break;

            foreach (var rank in ranks)
            {
                var result = rank.EvaluateAnnual(evaluationDate, timeProvider.GetUtcNow());
                if (result is not null)
                {
                    logger.LogInformation("年次ランク評価: {UserId}, {OldRank} → {NewRank}",
                        rank.UserId, result.Value.PreviousRank, result.Value.CurrentRank);

                    await eventPublisher.PublishMemberRankUpdatedAsync(
                        rank.UserId,
                        result.Value.PreviousRank,
                        result.Value.CurrentRank,
                        result.Value.PointRate,
                        ct);
                }
            }

            processedCount += ranks.Count;
            await memberRankRepository.SaveChangesAsync(ct);
        }

        logger.LogInformation("年次ランク評価完了: {Count} 件処理", processedCount);
    }

    private static MemberRankDto MapToDto(MemberRank m) =>
        new(m.Id, m.UserId, m.CurrentRank, m.AnnualPurchaseAmount,
            m.PreviousYearAmount, m.PointRate, m.RankUpdatedAt, m.NextEvaluationDate);

    /// <summary>
    /// 分散ロックを取得して年次評価を実行する。ロック取得失敗時は他インスタンスが実行中とみなしスキップする。
    /// </summary>
    public async Task TryExecuteEvaluationWithLockAsync(CancellationToken ct = default)
    {
        var lockAcquired = await memberRankRepository.TryAcquireEvaluationLockAsync(ct);
        if (!lockAcquired) return;

        try
        {
            await EvaluateAllRanksAsync(ct);
        }
        finally
        {
            await memberRankRepository.ReleaseEvaluationLockAsync(CancellationToken.None);
        }
    }
}
