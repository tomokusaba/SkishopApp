using Microsoft.EntityFrameworkCore;
using PointService.Infrastructure.Persistence;
using PointService.Models;
using PointService.Repositories.Interfaces;

namespace PointService.Repositories;

public class PointExpiryRepository(AppDbContext context, TimeProvider timeProvider) : IPointExpiryRepository
{
    public async Task<List<PointExpiry>> FindExpiredAsync(
        DateTime asOf, int batchSize, CancellationToken ct = default)
        => await context.PointExpiries
            .Where(e => e.Status == ExpiryStatuses.Active && e.ExpiresAt <= asOf)
            .OrderBy(e => e.ExpiresAt)
            .Take(batchSize)
            .ToListAsync(ct);

    public async Task<List<PointExpiry>> FindActiveByAccountIdAsync(
        string accountId, CancellationToken ct = default)
        => await context.PointExpiries
            .Where(e => e.AccountId == accountId && e.Status == ExpiryStatuses.Active)
            .OrderBy(e => e.ExpiresAt)
            .ToListAsync(ct);

    public async Task<List<PointExpiry>> FindActiveByUserIdAsync(
        string userId, CancellationToken ct = default)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return [];
        
        // EF Core クエリ翻訳問題を回避
        var items = await context.PointExpiries
            .AsNoTracking()
            .ToListAsync(ct);
        
        return items
            .Where(e => e.UserId == userGuid && e.Status == ExpiryStatuses.Active)
            .ToList();
    }

    public async Task<List<PointExpiry>> FindConsumedByAccountIdAsync(
        string accountId, CancellationToken ct = default)
        => await context.PointExpiries
            .Where(e => e.AccountId == accountId && e.Status == ExpiryStatuses.Consumed)
            .OrderByDescending(e => e.UpdatedAt)
            .ToListAsync(ct);

    public async Task<List<PointExpiry>> FindAllByUserIdAsync(
        string userId, CancellationToken ct = default)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return [];
        
        // EF Core クエリ翻訳問題を回避
        var items = await context.PointExpiries
            .AsNoTracking()
            .ToListAsync(ct);
        
        return items
            .Where(e => e.UserId == userGuid)
            .ToList();
    }

    public async Task<List<PointExpiry>> FindExpiringWithinAsync(
        string userId, int days, CancellationToken ct = default)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return [];
        var threshold = timeProvider.GetUtcNow().UtcDateTime.AddDays(days);
        
        // EF Core クエリ翻訳問題を回避
        var items = await context.PointExpiries
            .AsNoTracking()
            .ToListAsync(ct);
        
        return items
            .Where(e => e.UserId == userGuid && e.Status == ExpiryStatuses.Active && e.ExpiresAt <= threshold)
            .OrderBy(e => e.ExpiresAt)
            .ToList();
    }

    public async Task AnonymizeExpiriesAsync(string userId, string anonymizedId, CancellationToken ct = default)
    {
        if (!Guid.TryParse(userId, out var userGuid) || !Guid.TryParse(anonymizedId, out var anonymizedGuid))
            return;
        
        await context.PointExpiries
            .Where(e => e.UserId == userGuid)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.UserId, anonymizedGuid)
                .SetProperty(e => e.Status, ExpiryStatuses.Expired), ct);
    }

    public async Task AddAsync(PointExpiry expiry, CancellationToken ct = default)
        => await context.PointExpiries.AddAsync(expiry, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
