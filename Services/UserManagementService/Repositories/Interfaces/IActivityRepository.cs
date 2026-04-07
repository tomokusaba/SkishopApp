using UserManagementService.Models;

namespace UserManagementService.Repositories.Interfaces;

/// <summary>
/// アクティビティ履歴 Repository。ページネーション付き取得を提供する。
/// </summary>
public interface IActivityRepository
{
    Task<(List<UserActivity> Items, int TotalCount)> FindByUserIdAsync(string userId, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(UserActivity activity, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
