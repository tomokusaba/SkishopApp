using SalesManagementService.Models;

namespace SalesManagementService.Repositories.Interfaces;

public interface IOutboxEventRepository
{
    Task<List<OutboxEvent>> FindPendingEventsAsync(int batchSize, CancellationToken ct = default);
    Task<int> CountPendingEventsAsync(CancellationToken ct = default);
    Task AddAsync(OutboxEvent outboxEvent, CancellationToken ct = default);
    Task DeletePublishedBeforeAsync(DateTimeOffset threshold, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
