using Microsoft.EntityFrameworkCore;
using PaymentCartService.Infrastructure.Persistence;
using PaymentCartService.Models;
using PaymentCartService.Models.Enums;
using PaymentCartService.Repositories.Interfaces;

namespace PaymentCartService.Repositories;

public class OutboxEventRepository(AppDbContext context) : IOutboxEventRepository
{
    public async Task<List<OutboxEvent>> FindPendingEventsAsync(int batchSize, CancellationToken ct = default)
        => await context.OutboxEvents
            .Where(e => e.Status == OutboxEventStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);

    public async Task AddAsync(OutboxEvent outboxEvent, CancellationToken ct = default)
        => await context.OutboxEvents.AddAsync(outboxEvent, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
