using Microsoft.EntityFrameworkCore;
using UserManagementService.Infrastructure.Persistence;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;

namespace UserManagementService.Repositories;

/// <summary>
/// ユーザー設定の EF Core Repository 実装。
/// </summary>
public class PreferenceRepository(AppDbContext context) : IPreferenceRepository
{
    public async Task<UserPreference?> FindByUserIdAsync(string userId, CancellationToken ct = default)
        => await context.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

    public async Task AddAsync(UserPreference preference, CancellationToken ct = default)
        => await context.UserPreferences.AddAsync(preference, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
