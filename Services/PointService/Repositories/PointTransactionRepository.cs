using Microsoft.EntityFrameworkCore;
using PointService.Infrastructure.Persistence;
using PointService.Models;
using PointService.Repositories.Interfaces;

namespace PointService.Repositories;

public class PointTransactionRepository(AppDbContext context) : IPointTransactionRepository
{
    public async Task<PointTransaction?> FindByIdAsync(
        string id, CancellationToken ct = default)
        => await context.PointTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<List<PointTransaction>> FindByAccountIdAsync(
        string accountId, CancellationToken ct = default)
        => await context.PointTransactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task<List<PointTransaction>> FindByUserIdAsync(
        string userId, CancellationToken ct = default)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return [];
        return await context.PointTransactions
            .AsNoTracking()
            .Where(t => t.UserId == userGuid)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<(List<PointTransaction> Items, int TotalCount)> GetPagedAsync(
        string userId, int page, int pageSize, CancellationToken ct = default)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return ([], 0);
        
        // EF Core のクエリ翻訳問題を回避するため、同期的にデータ取得後、クライアント側でフィルタリング
        var userIdString = userGuid.ToString();
        
        var query = context.PointTransactions
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Where(t => t.UserId == userGuid);

        var allItems = await query.ToListAsync(ct);
        
        var totalCount = allItems.Count;
        var items = allItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }

    public async Task<PointTransaction?> FindByReferenceAsync(
        string referenceId, string type, CancellationToken ct = default)
        => await context.PointTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ReferenceId == referenceId && t.Type == type, ct);

    public async Task AddAsync(PointTransaction transaction, CancellationToken ct = default)
        => await context.PointTransactions.AddAsync(transaction, ct);

    public async Task<long> SumPointsByTypeAsync(string type, CancellationToken ct = default)
        => await context.PointTransactions
            .AsNoTracking()
            .Where(t => t.Type == type)
            .SumAsync(t => (long)Math.Abs(t.Points), ct);

    public async Task AnonymizeTransactionsAsync(string userId, string anonymizedId, CancellationToken ct = default)
    {
        var userIdGuid = Guid.Parse(userId);
        var anonymizedIdGuid = Guid.Parse(anonymizedId);
        await context.PointTransactions
            .Where(t => t.UserId == userIdGuid)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.UserId, anonymizedIdGuid)
                .SetProperty(t => t.Description, "匿名化済み"), ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
