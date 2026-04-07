using UserManagementService.Models;

namespace UserManagementService.Repositories.Interfaces;

/// <summary>
/// 処理済みイベント Repository。Kafka イベントのべき等性保証に使用する。
/// </summary>
public interface IProcessedEventRepository
{
    Task<bool> ExistsAsync(string eventId, string eventType, CancellationToken ct = default);
    Task AddAsync(ProcessedEvent processedEvent, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
