using Microsoft.EntityFrameworkCore;
using UserManagementService.Infrastructure.Persistence;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;

namespace UserManagementService.Repositories;

/// <summary>
/// 会員ランクの EF Core Repository 実装。PostgreSQL Advisory Lock で年次評価の排他制御を行う。
/// </summary>
public class MemberRankRepository(AppDbContext context) : IMemberRankRepository
{
    /// <summary>
    /// PostgreSQL pg_try_advisory_xact_lock でトランザクションスコープのロックを取得する。
    /// ロックはトランザクション終了時に自動解放される。
    /// </summary>
    public async Task<bool> TryAcquireEvaluationLockAsync(CancellationToken ct = default)
        => await context.Database.ExecuteSqlRawAsync(
            "SELECT pg_try_advisory_xact_lock(hashtext('member_rank_eval'))",
            ct) > 0;

    /// <summary>Advisory Lock はトランザクションスコープのため明示的解放は不要。No-op 。</summary>
    public Task ReleaseEvaluationLockAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    public async Task<MemberRank?> FindByUserIdAsync(string userId, CancellationToken ct = default)
        => await context.MemberRanks
            .FirstOrDefaultAsync(m => m.UserId == userId, ct);

    public async Task<MemberRank?> FindByUserIdReadOnlyAsync(string userId, CancellationToken ct = default)
        => await context.MemberRanks
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == userId, ct);

    public async Task<List<MemberRank>> FindAllForEvaluationAsync(DateOnly evaluationDate, int batchSize, CancellationToken ct = default)
        => await context.MemberRanks
            .Where(m => m.NextEvaluationDate <= evaluationDate)
            .OrderBy(m => m.NextEvaluationDate)
            .Take(batchSize)
            .ToListAsync(ct);

    public async Task AddAsync(MemberRank memberRank, CancellationToken ct = default)
        => await context.MemberRanks.AddAsync(memberRank, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
