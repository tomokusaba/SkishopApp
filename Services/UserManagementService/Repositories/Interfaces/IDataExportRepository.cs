using UserManagementService.Models;

namespace UserManagementService.Repositories.Interfaces;

/// <summary>
/// GDPR データエクスポート用 Repository。エクスポート対象ユーザーの取得と完了マーク。
/// </summary>
public interface IDataExportRepository
{
    Task<List<string>> FindPendingExportUserIdsAsync(int batchSize, CancellationToken ct = default);
    Task<User?> FindUserExportDataAsync(string userId, CancellationToken ct = default);
    Task MarkExportCompletedAsync(string userId, CancellationToken ct = default);
}
