using Microsoft.EntityFrameworkCore;
using PointService.Infrastructure.Persistence;
using PointService.Models;
using PointService.Repositories.Interfaces;

namespace PointService.Repositories;

public class OutboxEventRepository(AppDbContext context) : IOutboxEventRepository
{
    public async Task<List<OutboxEvent>> FindPendingAsync(
        int batchSize, CancellationToken ct = default)
        => await context.OutboxEvents
            .Where(e => e.Status == OutboxStatuses.Pending)
            .OrderBy(e => e.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);

    public async Task AddAsync(OutboxEvent outboxEvent, CancellationToken ct = default)
        => await context.OutboxEvents.AddAsync(outboxEvent, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
