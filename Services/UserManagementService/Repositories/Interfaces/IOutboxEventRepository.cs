using UserManagementService.Models;

namespace UserManagementService.Repositories.Interfaces;

/// <summary>
/// Outbox イベント Repository。分散ロックによる排他制御と PENDING イベントのバッチ取得を提供する。
/// </summary>
public interface IOutboxEventRepository
{
    Task<bool> TryAcquirePublishLockAsync(CancellationToken ct = default);
    Task ReleasePublishLockAsync(CancellationToken ct = default);
    Task<List<OutboxEvent>> FindPendingAsync(int batchSize, CancellationToken ct = default);
    Task AddAsync(OutboxEvent outboxEvent, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
