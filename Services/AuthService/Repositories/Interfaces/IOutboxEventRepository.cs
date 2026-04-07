using AuthService.Models;

namespace AuthService.Repositories.Interfaces;

/// <summary>
/// Repository interface for managing outbox events in the transactional outbox pattern.
/// </summary>
/// <remarks>
/// <para>
/// Implements the Outbox Pattern for reliable event publishing, ensuring atomic
/// consistency between database operations and event emission to message brokers (e.g., Kafka).
/// </para>
/// <para>
/// <strong>Pattern Description:</strong>
/// <list type="number">
/// <item><description>Business operation and outbox event are saved in the same database transaction.</description></item>
/// <item><description>A background publisher polls for pending events and publishes to the message broker.</description></item>
/// <item><description>Successfully published events are marked as completed.</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Security Considerations:</strong>
/// <list type="bullet">
/// <item><description>Event payloads may contain user identifiers; avoid including sensitive data like passwords or tokens.</description></item>
/// <item><description>Outbox events should be processed in order to maintain event sequence integrity.</description></item>
/// <item><description>Failed events should be retried with exponential backoff and eventually moved to a dead-letter queue.</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Transaction Requirements:</strong>
/// Outbox events must be added within the same transaction as the business operation
/// they represent to guarantee at-least-once delivery semantics.
/// </para>
/// </remarks>
public interface IOutboxEventRepository
{
    /// <summary>
    /// Retrieves pending outbox events that have not yet been published.
    /// </summary>
    /// <param name="batchSize">The maximum number of events to retrieve per batch. Default is 10.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A read-only list of pending <see cref="OutboxEvent"/> entities ordered by creation time.</returns>
    /// <remarks>
    /// <para>
    /// Events are returned in creation order (FIFO) to maintain event sequence integrity.
    /// </para>
    /// <para>
    /// <strong>Concurrency:</strong> The background publisher should implement row locking
    /// or optimistic concurrency to prevent duplicate event processing across multiple instances.
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<OutboxEvent>> FindPendingAsync(int batchSize = 10, CancellationToken ct = default);

    /// <summary>
    /// Adds a new outbox event to be published asynchronously.
    /// </summary>
    /// <param name="outboxEvent">The outbox event containing the event type, payload, and metadata.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Transaction:</strong> This method must be called within the same transaction
    /// as the business operation to ensure atomic consistency.
    /// </para>
    /// <para>
    /// Call <see cref="SaveChangesAsync"/> as part of the business transaction to persist
    /// both the business data and the outbox event atomically.
    /// </para>
    /// </remarks>
    Task AddAsync(OutboxEvent outboxEvent, CancellationToken ct = default);

    /// <summary>
    /// Persists all pending changes to the database.
    /// </summary>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveChangesAsync(CancellationToken ct = default);
}
