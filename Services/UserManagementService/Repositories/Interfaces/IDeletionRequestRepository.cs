using UserManagementService.Models;

namespace UserManagementService.Repositories.Interfaces;

/// <summary>
/// GDPR 削除リクエスト Repository。献予期間切れ・タイムアウト検出用クエリを提供する。
/// </summary>
public interface IDeletionRequestRepository
{
    Task<DeletionRequest?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<DeletionRequest?> FindPendingByUserIdAsync(string userId, CancellationToken ct = default);
    Task<List<DeletionRequest>> FindExpiredGracePeriodAsync(CancellationToken ct = default);
    Task<List<DeletionRequest>> FindTimedOutProcessingAsync(TimeSpan timeout, CancellationToken ct = default);
    Task<List<DeletionRequest>> FindByStatusAsync(string status, CancellationToken ct = default);
    Task AddAsync(DeletionRequest request, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
