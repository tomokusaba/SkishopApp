using AuthService.Infrastructure.Persistence;
using AuthService.Models;
using AuthService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Repositories;

/// <summary>
/// Repository implementation for managing security event logs using Entity Framework Core.
/// </summary>
/// <remarks>
/// <para>
/// Provides append-only data access for security-relevant events such as login attempts,
/// password changes, MFA events, and suspicious activity.
/// </para>
/// <para>
/// <strong>Security:</strong> Modification and deletion operations are intentionally omitted
/// to maintain audit integrity. Never log sensitive data such as passwords or tokens.
/// </para>
/// </remarks>
/// <param name="context">The database context for accessing security log data.</param>
public class SecurityLogRepository(AuthDbContext context) : ISecurityLogRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Adds the security log entry to the change tracker.
    /// Logging should never block or fail the primary security operation.
    /// </para>
    /// <para>
    /// <strong>Sensitive Data:</strong> Ensure the log entry does not contain
    /// passwords, tokens, or full PII that could be exploited.
    /// </para>
    /// </remarks>
    public async Task AddAsync(SecurityLog log, CancellationToken ct = default)
        => await context.SecurityLogs.AddAsync(log, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Uses <c>AsNoTracking</c> for read-only access. Results are ordered by
    /// most recent first and limited to the specified count for efficiency.
    /// </para>
    /// <para>
    /// Used for user security activity dashboards and administrative security reviews.
    /// For comprehensive log analysis, use dedicated log aggregation tools.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<SecurityLog>> FindByUserIdAsync(string userId, int limit = 50, CancellationToken ct = default)
        => await context.SecurityLogs
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
