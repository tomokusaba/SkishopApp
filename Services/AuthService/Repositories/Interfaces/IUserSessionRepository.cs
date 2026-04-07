using AuthService.Models;

namespace AuthService.Repositories.Interfaces;

/// <summary>
/// Repository interface for managing user authentication sessions.
/// </summary>
/// <remarks>
/// <para>
/// Provides data access operations for user sessions which track active logins,
/// device information, and session metadata for security monitoring and management.
/// </para>
/// <para>
/// <strong>Security Considerations:</strong>
/// <list type="bullet">
/// <item><description>Session tokens must be cryptographically random (minimum 256 bits of entropy).</description></item>
/// <item><description>Store session tokens as secure hashes, not plaintext, for defense in depth.</description></item>
/// <item><description>Implement session timeout and absolute expiration policies.</description></item>
/// <item><description>Track device/IP information for suspicious activity detection.</description></item>
/// <item><description>Provide "active sessions" view for users to monitor and revoke sessions.</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Session vs Refresh Token:</strong>
/// Sessions represent the authentication state and may contain device metadata.
/// Refresh tokens are used for token rotation. A session may span multiple token rotations.
/// </para>
/// </remarks>
public interface IUserSessionRepository
{
    /// <summary>
    /// Finds a session by its unique session identifier.
    /// </summary>
    /// <param name="sessionId">The unique identifier of the session (primary key).</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>
    /// The <see cref="UserSession"/> entity if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Tracking:</strong> Uses tracked query to allow immediate session status updates.
    /// </para>
    /// <para>
    /// Used for session management operations such as viewing session details
    /// and administrative session revocation.
    /// </para>
    /// </remarks>
    Task<UserSession?> FindBySessionIdAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Finds a session by its session token value.
    /// </summary>
    /// <param name="sessionToken">The session token string used for authentication.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>
    /// The <see cref="UserSession"/> entity if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Authentication:</strong> Used to validate incoming requests that include
    /// a session token in headers or cookies.
    /// </para>
    /// <para>
    /// <strong>Validation:</strong> After retrieval, verify the session is still active
    /// and not expired before granting access.
    /// </para>
    /// <para>
    /// <strong>Tracking:</strong> Uses tracked query to allow update of LastActivityAt timestamp.
    /// </para>
    /// </remarks>
    Task<UserSession?> FindBySessionTokenAsync(string sessionToken, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all active sessions for a specific user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A read-only list of active <see cref="UserSession"/> entities.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item><description>"Active sessions" display in user security settings.</description></item>
    /// <item><description>Concurrent session limiting.</description></item>
    /// <item><description>Security audit and monitoring.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Uses read-only tracking for optimal query performance.
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<UserSession>> FindActiveByUserIdAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Deactivates (invalidates) a specific session.
    /// </summary>
    /// <param name="sessionId">The unique identifier of the session to deactivate.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item><description>User-initiated logout from a specific device.</description></item>
    /// <item><description>User-initiated "sign out from other devices".</description></item>
    /// <item><description>Administrative session termination.</description></item>
    /// <item><description>Session invalidation after password change.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Transaction:</strong> Uses bulk update for efficiency; changes are immediately persisted.
    /// </para>
    /// </remarks>
    Task DeactivateSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Adds a new user session to the repository.
    /// </summary>
    /// <param name="session">The session entity containing token, device info, and metadata.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Session Creation:</strong> Typically called during:
    /// <list type="bullet">
    /// <item><description>Successful login (with or without MFA).</description></item>
    /// <item><description>OAuth callback after external authentication.</description></item>
    /// <item><description>Token refresh that requires new session (sliding expiration).</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Security:</strong> Generate session tokens using <c>RandomNumberGenerator</c>
    /// with at least 32 bytes of random data.
    /// </para>
    /// </remarks>
    Task AddAsync(UserSession session, CancellationToken ct = default);

    /// <summary>
    /// Persists all pending changes to the database.
    /// </summary>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveChangesAsync(CancellationToken ct = default);
}
