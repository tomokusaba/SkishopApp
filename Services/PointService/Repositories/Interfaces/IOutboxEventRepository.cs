using PointService.Models;

namespace PointService.Repositories.Interfaces;

public interface IOutboxEventRepository
{
    Task<List<OutboxEvent>> FindPendingAsync(int batchSize, CancellationToken ct = default);
    Task AddAsync(OutboxEvent outboxEvent, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
