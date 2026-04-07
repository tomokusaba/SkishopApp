using UserManagementService.Models;

namespace UserManagementService.Repositories.Interfaces;

/// <summary>
/// 同意管理 Repository。ユーザーごとの同意状態を管理する。
/// </summary>
public interface IConsentRepository
{
    Task<List<Consent>> FindByUserIdAsync(string userId, CancellationToken ct = default);
    Task<Consent?> FindByUserIdAndTypeAsync(string userId, string consentType, CancellationToken ct = default);
    Task AddAsync(Consent consent, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
