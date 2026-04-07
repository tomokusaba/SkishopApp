using UserManagementService.Models;

namespace UserManagementService.Repositories.Interfaces;

/// <summary>
/// 住所 Repository。ユーザーごとの住所検索・保存・削除を担当する。
/// </summary>
public interface IAddressRepository
{
    Task<Address?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<List<Address>> FindByUserIdAsync(string userId, CancellationToken ct = default);
    Task<Address?> FindDefaultByUserIdAsync(string userId, string addressType, CancellationToken ct = default);
    Task<int> CountByUserIdAsync(string userId, CancellationToken ct = default);
    Task AddAsync(Address address, CancellationToken ct = default);
    void Remove(Address address);
    Task SaveChangesAsync(CancellationToken ct = default);
}
