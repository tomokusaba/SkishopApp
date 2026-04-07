using UserManagementService.Models;

namespace UserManagementService.Repositories.Interfaces;

/// <summary>
/// ユーザー設定 Repository。User と 1:1 のプリファレンスを管理する。
/// </summary>
public interface IPreferenceRepository
{
    Task<UserPreference?> FindByUserIdAsync(string userId, CancellationToken ct = default);
    Task AddAsync(UserPreference preference, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
