using AuthService.Infrastructure.Persistence;
using AuthService.Models;
using AuthService.Repositories.Interfaces;

namespace AuthService.Repositories;

/// <summary>
/// Repository implementation for managing audit log entries using Entity Framework Core.
/// </summary>
/// <remarks>
/// <para>
/// Provides append-only data access for audit trail records. Modification and deletion
/// operations are intentionally not implemented to maintain audit integrity.
/// </para>
/// <para>
/// <strong>Security:</strong> Audit logs should be created within the same transaction
/// as the audited operation to prevent gaps in the audit trail.
/// </para>
/// </remarks>
/// <param name="context">The database context for accessing audit log data.</param>
public class AuditLogRepository(AuthDbContext context) : IAuditLogRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// Adds the audit log entry to the change tracker. The entry is not persisted
    /// until <see cref="SaveChangesAsync"/> is called.
    /// </remarks>
    public async Task AddAsync(AuditLog auditLog, CancellationToken ct = default)
        => await context.AuditLogs.AddAsync(auditLog, ct);

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
