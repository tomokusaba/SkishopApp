using Microsoft.EntityFrameworkCore;
using UserManagementService.Infrastructure.Persistence;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;

namespace UserManagementService.Repositories;

/// <summary>
/// アクティビティ履歴の EF Core Repository 実装。タイムスタンプ降順のページネーションを提供する。
/// </summary>
public class ActivityRepository(AppDbContext context) : IActivityRepository
{
    public async Task<(List<UserActivity> Items, int TotalCount)> FindByUserIdAsync(
        string userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.UserActivities
            .AsNoTracking()
            .Where(a => a.UserId == userId);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(UserActivity activity, CancellationToken ct = default)
        => await context.UserActivities.AddAsync(activity, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
