using Microsoft.EntityFrameworkCore;
using SalesManagementService.Infrastructure.Persistence;
using SalesManagementService.Models;
using SalesManagementService.Repositories.Interfaces;

namespace SalesManagementService.Repositories;

public class SagaLogRepository(SalesDbContext context) : ISagaLogRepository
{
    public async Task<SagaLog?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.SagaLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<SagaLog?> FindByOrderIdAsync(string orderId, CancellationToken ct = default)
        => await context.SagaLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.OrderId == orderId, ct);

    public async Task<List<SagaLog>> FindStuckSagasAsync(
        DateTimeOffset threshold, int limit, CancellationToken ct = default)
        => await context.SagaLogs
            .Where(s => s.Status == "PROCESSING" && s.UpdatedAt < threshold)
            .OrderBy(s => s.UpdatedAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<List<SagaLog>> FindPendingPaymentSagasAsync(
        DateTimeOffset threshold, int limit, CancellationToken ct = default)
        => await context.SagaLogs
            .Where(s => s.Status == "PENDING_PAYMENT" && s.UpdatedAt < threshold)
            .OrderBy(s => s.UpdatedAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task AddAsync(SagaLog sagaLog, CancellationToken ct = default)
        => await context.SagaLogs.AddAsync(sagaLog, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
