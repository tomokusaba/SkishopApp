using UserManagementService.Models;

namespace UserManagementService.Repositories.Interfaces;

/// <summary>
/// 会員ランク Repository。年次評価用の分散ロック機構を含む。
/// </summary>
public interface IMemberRankRepository
{
    Task<bool> TryAcquireEvaluationLockAsync(CancellationToken ct = default);
    Task ReleaseEvaluationLockAsync(CancellationToken ct = default);
    Task<MemberRank?> FindByUserIdAsync(string userId, CancellationToken ct = default);
    Task<MemberRank?> FindByUserIdReadOnlyAsync(string userId, CancellationToken ct = default);
    Task<List<MemberRank>> FindAllForEvaluationAsync(DateOnly evaluationDate, int batchSize, CancellationToken ct = default);
    Task AddAsync(MemberRank memberRank, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
