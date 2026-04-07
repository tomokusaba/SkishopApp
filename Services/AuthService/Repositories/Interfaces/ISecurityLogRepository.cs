using AuthService.Models;

namespace AuthService.Repositories.Interfaces;

/// <summary>
/// Repository interface for managing security event logs.
/// </summary>
/// <remarks>
/// <para>
/// Provides data access operations for recording and querying security-relevant events
/// such as login attempts, password changes, MFA events, and suspicious activity.
/// </para>
/// <para>
/// <strong>Security Considerations:</strong>
/// <list type="bullet">
/// <item><description>Security logs are append-only; modification and deletion operations are intentionally omitted.</description></item>
/// <item><description>Log IP addresses for forensic analysis but ensure GDPR/privacy compliance.</description></item>
/// <item><description>Never log sensitive data such as passwords, tokens, or full credit card numbers.</description></item>
/// <item><description>Implement log retention policies according to compliance requirements.</description></item>
/// <item><description>Security logs should be stored separately from application logs for isolation.</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Event Types:</strong>
/// Common security events include: LoginSuccess, LoginFailure, PasswordChange, MfaEnabled,
/// MfaDisabled, AccountLocked, AccountUnlocked, TokenRevoked, SuspiciousActivity.
/// </para>
/// </remarks>
public interface ISecurityLogRepository
{
    /// <summary>
    /// Adds a new security event log entry.
    /// </summary>
    /// <param name="log">The security log entity containing event details, user ID, IP address, and timestamp.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Transaction:</strong> Security logs should be added within the same transaction
    /// as the security event when possible, but logging should never block or fail the primary operation.
    /// </para>
    /// <para>
    /// <strong>Sensitive Data:</strong> Ensure the log entry does not contain:
    /// <list type="bullet">
    /// <item><description>Plaintext passwords or password hashes.</description></item>
    /// <item><description>Full authentication tokens or session IDs.</description></item>
    /// <item><description>Full credit card numbers or SSNs.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    Task AddAsync(SecurityLog log, CancellationToken ct = default);

    /// <summary>
    /// Retrieves recent security log entries for a specific user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="limit">The maximum number of log entries to retrieve. Default is 50.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A read-only list of <see cref="SecurityLog"/> entries, ordered by most recent first.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item><description>User security activity dashboard.</description></item>
    /// <item><description>Administrative security review.</description></item>
    /// <item><description>Suspicious activity analysis during incident response.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Uses read-only tracking and limits results for efficiency.
    /// For comprehensive log analysis, use dedicated log aggregation tools.
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<SecurityLog>> FindByUserIdAsync(string userId, int limit = 50, CancellationToken ct = default);

    /// <summary>
    /// Persists all pending changes to the database.
    /// </summary>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveChangesAsync(CancellationToken ct = default);
}
