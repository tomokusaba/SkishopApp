using AuthService.Models;

namespace AuthService.Repositories.Interfaces;

/// <summary>
/// Repository interface for managing audit log entries.
/// </summary>
/// <remarks>
/// <para>
/// Provides data access operations for audit trail records that track
/// security-sensitive actions within the authentication system.
/// </para>
/// <para>
/// <strong>Security Considerations:</strong>
/// <list type="bullet">
/// <item><description>Audit logs are append-only; modification and deletion operations are intentionally omitted to maintain integrity.</description></item>
/// <item><description>All audit entries should be created within the same transaction as the audited operation.</description></item>
/// <item><description>Do not log sensitive data such as passwords, tokens, or PII in audit entries.</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Transaction Requirements:</strong>
/// Call <see cref="SaveChangesAsync"/> after <see cref="AddAsync"/> to persist the audit log
/// within the same transaction boundary as the operation being audited.
/// </para>
/// </remarks>
public interface IAuditLogRepository
{
    /// <summary>
    /// Adds a new audit log entry to the repository.
    /// </summary>
    /// <param name="auditLog">The audit log entity containing the action details, user ID, and timestamp.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// The audit log entry is not persisted until <see cref="SaveChangesAsync"/> is called.
    /// This allows multiple operations to be batched within a single transaction.
    /// </para>
    /// <para>
    /// <strong>Security:</strong> Ensure the audit log is added within the same transaction
    /// as the audited operation to prevent audit gaps.
    /// </para>
    /// </remarks>
    Task AddAsync(AuditLog auditLog, CancellationToken ct = default);

    /// <summary>
    /// Persists all pending changes to the database.
    /// </summary>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method commits all pending audit log entries added via <see cref="AddAsync"/>.
    /// Should be called as part of the transaction that includes the audited operation.
    /// </remarks>
    Task SaveChangesAsync(CancellationToken ct = default);
}
