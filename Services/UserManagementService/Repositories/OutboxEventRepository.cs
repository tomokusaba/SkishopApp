using Microsoft.EntityFrameworkCore;
using UserManagementService.Infrastructure.Persistence;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;

namespace UserManagementService.Repositories;

/// <summary>
/// Outbox イベントの EF Core Repository 実装。PostgreSQL Advisory Lock で Publisher の排他制御を行う。
/// </summary>
public class OutboxEventRepository(AppDbContext context) : IOutboxEventRepository
{
    /// <summary>
    /// PostgreSQL pg_try_advisory_xact_lock で Outbox Publisher の排他ロックを取得する。
    /// </summary>
    public async Task<bool> TryAcquirePublishLockAsync(CancellationToken ct = default)
        => await context.Database.ExecuteSqlRawAsync(
            "SELECT pg_try_advisory_xact_lock(hashtext('outbox_publisher'))",
            ct) > 0;

    public Task ReleasePublishLockAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    public async Task<List<OutboxEvent>> FindPendingAsync(int batchSize, CancellationToken ct = default)
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
