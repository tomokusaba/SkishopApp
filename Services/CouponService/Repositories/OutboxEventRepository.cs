using CouponService.Infrastructure.Persistence;
using CouponService.Models;
using CouponService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CouponService.Repositories;

public class OutboxEventRepository(AppDbContext context, TimeProvider timeProvider) : IOutboxEventRepository
{
    public async Task<List<OutboxEvent>> GetPendingEventsAsync(int batchSize, CancellationToken ct = default)
        => await context.OutboxEvents
            .Where(e => e.Status == OutboxEventStatus.Pending && e.RetryCount < e.MaxRetries)
            .OrderBy(e => e.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);

    public async Task AddAsync(OutboxEvent outboxEvent, CancellationToken ct = default)
        => await context.OutboxEvents.AddAsync(outboxEvent, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);

    public async Task MarkAsPublishedAsync(string eventId, CancellationToken ct = default)
    {
        var outboxEvent = await context.OutboxEvents.FindAsync([eventId], ct);
        if (outboxEvent is not null)
        {
            outboxEvent.Status = OutboxEventStatus.Published;
            outboxEvent.PublishedAt = timeProvider.GetUtcNow();
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task MarkAsFailedAsync(string eventId, CancellationToken ct = default)
    {
        var outboxEvent = await context.OutboxEvents.FindAsync([eventId], ct);
        if (outboxEvent is not null)
        {
            outboxEvent.Status = OutboxEventStatus.Failed;
            outboxEvent.RetryCount++;
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task<int> MoveToDeadLetterAsync(int maxRetryCount, CancellationToken ct = default)
        => await context.OutboxEvents
            .Where(e => e.Status == OutboxEventStatus.Failed && e.RetryCount >= maxRetryCount)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, OutboxEventStatus.DeadLetter), ct);

    public async Task<bool> TryAcquireAdvisoryLockAsync(long lockId, CancellationToken ct = default)
        => await context.Database
            .SqlQuery<bool>($"SELECT pg_try_advisory_lock({lockId}) AS \"Value\"")
            .FirstOrDefaultAsync(ct);

    public async Task ReleaseAdvisoryLockAsync(long lockId, CancellationToken ct = default)
        => await context.Database
            .ExecuteSqlAsync($"SELECT pg_advisory_unlock({lockId})", ct);

    public async Task<List<OutboxEvent>> GetPendingAndMarkProcessingAsync(int batchSize, CancellationToken ct = default)
    {
        var pendingEvents = await context.OutboxEvents
            .Where(e => e.Status == OutboxEventStatus.Pending && e.RetryCount < e.MaxRetries)
            .OrderBy(e => e.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);

        foreach (var evt in pendingEvents)
            evt.Status = OutboxEventStatus.Processing;
        await context.SaveChangesAsync(ct);
        return pendingEvents;
    }

    public async Task UpdateBatchAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);

    public async Task RecoverProcessingEventsAsync(CancellationToken ct = default)
        => await context.OutboxEvents
            .Where(e => e.Status == OutboxEventStatus.Processing)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, OutboxEventStatus.Pending), ct);
}
