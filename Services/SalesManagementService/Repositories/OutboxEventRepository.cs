using Microsoft.EntityFrameworkCore;
using SalesManagementService.Infrastructure.Persistence;
using SalesManagementService.Models;
using SalesManagementService.Repositories.Interfaces;

namespace SalesManagementService.Repositories;

public class OutboxEventRepository(SalesDbContext context) : IOutboxEventRepository
{
    public async Task<List<OutboxEvent>> FindPendingEventsAsync(
        int batchSize, CancellationToken ct = default)
        => await context.OutboxEvents
            .Where(e => e.Status == "PENDING")
            .OrderBy(e => e.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);

    public async Task<int> CountPendingEventsAsync(CancellationToken ct = default)
        => await context.OutboxEvents
            .CountAsync(e => e.Status == "PENDING", ct);

    public async Task AddAsync(OutboxEvent outboxEvent, CancellationToken ct = default)
        => await context.OutboxEvents.AddAsync(outboxEvent, ct);

    public async Task DeletePublishedBeforeAsync(DateTimeOffset threshold, CancellationToken ct = default)
        => await context.OutboxEvents
            .Where(e => e.Status == "PUBLISHED" && e.PublishedAt < threshold)
            .ExecuteDeleteAsync(ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
