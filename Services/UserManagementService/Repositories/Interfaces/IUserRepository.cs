using UserManagementService.Models;

namespace UserManagementService.Repositories.Interfaces;

/// <summary>
/// User Aggregate Root 用 Repository。ユーザーの検索・保存を担当する。
/// </summary>
public interface IUserRepository
{
    Task<User?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<User?> FindByIdReadOnlyAsync(string id, CancellationToken ct = default);
    Task<User?> FindByIdWithDetailsAsync(string id, CancellationToken ct = default);
    Task<User?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<(List<User> Items, int TotalCount)> FindAllAsync(int page, int pageSize, string? statusFilter, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
