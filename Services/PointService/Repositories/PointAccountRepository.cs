using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PointService.Infrastructure.Persistence;
using PointService.Models;
using PointService.Repositories.Interfaces;

namespace PointService.Repositories;

public class PointAccountRepository(AppDbContext context) : IPointAccountRepository
{
    public async Task<PointAccount?> FindByUserIdAsync(
        string userId, CancellationToken ct = default)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return null;
        
        // EF Core のクエリ翻訳問題を回避（uuid = character varying エラー回避）
        var accounts = await context.PointAccounts
            .AsNoTracking()
            .ToListAsync(ct);
        
        return accounts.FirstOrDefault(a => a.UserId == userGuid);
    }

    public async Task<PointAccount?> FindByIdAsync(
        string id, CancellationToken ct = default)
        => await context.PointAccounts.FindAsync([id], ct);

    public async Task<List<PointAccount>> FindByTotalEarnedRangeAsync(
        int min, int max, CancellationToken ct = default)
        => await context.PointAccounts
            .AsNoTracking()
            .Where(a => a.TotalEarned >= min && a.TotalEarned < max)
            .ToListAsync(ct);

    public async Task<int> CountByTotalEarnedRangeAsync(
        int min, int max, CancellationToken ct = default)
        => await context.PointAccounts
            .AsNoTracking()
            .CountAsync(a => a.TotalEarned >= min && a.TotalEarned < max, ct);

    public async Task<int> CountAllAsync(CancellationToken ct = default)
        => await context.PointAccounts
            .AsNoTracking()
            .CountAsync(ct);

    public async Task AddAsync(PointAccount account, CancellationToken ct = default)
        => await context.PointAccounts.AddAsync(account, ct);

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default)
        => await context.Database.BeginTransactionAsync(ct);

    public async Task AnonymizeAccountAsync(string userId, string anonymizedId, CancellationToken ct = default)
    {
        if (!Guid.TryParse(userId, out var userGuid) || !Guid.TryParse(anonymizedId, out var anonymizedGuid))
            return;
        
        await context.PointAccounts
            .Where(a => a.UserId == userGuid)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.UserId, anonymizedGuid)
                .SetProperty(a => a.AvailablePoints, 0)
                .SetProperty(a => a.PendingPoints, 0), ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
