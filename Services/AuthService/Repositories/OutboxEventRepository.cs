using AuthService.Enums;
using AuthService.Infrastructure.Persistence;
using AuthService.Models;
using AuthService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Repositories;

/// <summary>
/// Repository implementation for managing outbox events in the transactional outbox pattern using Entity Framework Core.
/// </summary>
/// <remarks>
/// <para>
/// Implements the Outbox Pattern for reliable event publishing, ensuring atomic
/// consistency between database operations and event emission to message brokers (e.g., Kafka).
/// </para>
/// <para>
/// <strong>Transaction Requirements:</strong> Outbox events must be added within the same
/// transaction as the business operation they represent to guarantee at-least-once delivery.
/// </para>
/// </remarks>
/// <param name="context">The database context for accessing outbox event data.</param>
public class OutboxEventRepository(AuthDbContext context) : IOutboxEventRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Retrieves pending events ordered by creation time (FIFO) to maintain event sequence integrity.
    /// Results are limited to the specified batch size for controlled processing.
    /// </para>
    /// <para>
    /// <strong>Concurrency:</strong> The background publisher should implement row locking
    /// or optimistic concurrency to prevent duplicate event processing across multiple instances.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<OutboxEvent>> FindPendingAsync(int batchSize = 10, CancellationToken ct = default)
        => await context.OutboxEvents
            .Where(o => o.Status == OutboxStatus.Pending)
            .OrderBy(o => o.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Adds the outbox event to the change tracker. This method must be called within
    /// the same transaction as the business operation for atomic consistency.
    /// </para>
    /// <para>
    /// The event payload should be serialized as JSON and should not contain
    /// sensitive data such as passwords or tokens.
    /// </para>
    /// </remarks>
    public async Task AddAsync(OutboxEvent outboxEvent, CancellationToken ct = default)
        => await context.OutboxEvents.AddAsync(outboxEvent, ct);

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
