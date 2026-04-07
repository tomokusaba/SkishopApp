using PaymentCartService.Models;

namespace PaymentCartService.Repositories.Interfaces;

public interface IOutboxEventRepository
{
    Task<List<OutboxEvent>> FindPendingEventsAsync(int batchSize, CancellationToken ct = default);
    Task AddAsync(OutboxEvent outboxEvent, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
