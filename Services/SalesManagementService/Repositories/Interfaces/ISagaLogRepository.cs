using SalesManagementService.Models;

namespace SalesManagementService.Repositories.Interfaces;

public interface ISagaLogRepository
{
    Task<SagaLog?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<SagaLog?> FindByOrderIdAsync(string orderId, CancellationToken ct = default);
    Task<List<SagaLog>> FindStuckSagasAsync(DateTimeOffset threshold, int limit, CancellationToken ct = default);
    Task<List<SagaLog>> FindPendingPaymentSagasAsync(DateTimeOffset threshold, int limit, CancellationToken ct = default);
    Task AddAsync(SagaLog sagaLog, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
