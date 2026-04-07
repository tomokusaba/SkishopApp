using Microsoft.EntityFrameworkCore;
using UserManagementService.Infrastructure.Persistence;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;

namespace UserManagementService.Repositories;

/// <summary>
/// GDPR データエクスポートの EF Core Repository 実装。エクスポート対象ユーザーの
/// 全関連データを Include で Eager Loading して取得する。
/// </summary>
public class DataExportRepository(AppDbContext context, TimeProvider timeProvider) : IDataExportRepository
{
    public async Task<List<string>> FindPendingExportUserIdsAsync(int batchSize, CancellationToken ct = default)
        => await context.Users
            .AsNoTracking()
            .Where(user => user.IsDataExportRequested)
            .OrderBy(user => user.CreatedAt)
            .Select(user => user.Id)
            .Take(batchSize)
            .ToListAsync(ct);

    public async Task<User?> FindUserExportDataAsync(string userId, CancellationToken ct = default)
        => await context.Users
            .AsNoTracking()
            .Include(user => user.Addresses)
            .Include(user => user.Consents)
            .Include(user => user.Activities)
            .Include(user => user.Preference)
            .FirstOrDefaultAsync(user => user.Id == userId, ct);

    public async Task MarkExportCompletedAsync(string userId, CancellationToken ct = default)
    {
        var user = await context.Users.FirstOrDefaultAsync(current => current.Id == userId, ct);
        if (user is null)
            return;

        user.CompleteDataExport(timeProvider.GetUtcNow());
        await context.SaveChangesAsync(ct);
    }
}
