using AuthService.Models;

namespace AuthService.Repositories.Interfaces;

/// <summary>
/// Repository interface for managing OAuth 2.0 refresh tokens with token rotation support.
/// </summary>
/// <remarks>
/// <para>
/// Provides data access operations for refresh tokens used in the OAuth 2.0 token refresh flow,
/// implementing secure token rotation and family-based revocation for replay attack detection.
/// </para>
/// <para>
/// <strong>Security Considerations:</strong>
/// <list type="bullet">
/// <item><description>Refresh tokens must be stored as secure hashes (not plaintext) for defense in depth.</description></item>
/// <item><description>Token rotation: Issue a new refresh token on each use and revoke the old one.</description></item>
/// <item><description>Family-based revocation: All tokens in a family are revoked if replay is detected.</description></item>
/// <item><description>Limit concurrent refresh token families per user to prevent session proliferation.</description></item>
/// <item><description>Implement periodic cleanup of expired and revoked tokens.</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Token Families:</strong>
/// Refresh tokens are grouped into families. A family represents a single login session.
/// Token rotation creates new tokens within the same family. If a revoked token is reused
/// (replay attack), the entire family must be revoked.
/// </para>
/// </remarks>
public interface IRefreshTokenRepository
{
    /// <summary>
    /// Finds a refresh token by its token value.
    /// </summary>
    /// <param name="token">The refresh token string to search for.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>
    /// The <see cref="RefreshToken"/> entity if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Security:</strong> After retrieval, validate:
    /// <list type="bullet">
    /// <item><description>Token is not expired (ExpiresAt &gt; current time).</description></item>
    /// <item><description>Token is not revoked (IsRevoked = false).</description></item>
    /// <item><description>If token is revoked but found, this indicates a replay attack - revoke entire family.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Uses tracking to allow immediate update of token status after validation.
    /// </para>
    /// </remarks>
    Task<RefreshToken?> FindByTokenAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all active (non-revoked, non-expired) refresh tokens for a user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A read-only list of active refresh tokens for the user.</returns>
    /// <remarks>
    /// Used for session management features such as "active sessions" display
    /// and "sign out from all devices" functionality.
    /// </remarks>
    Task<IReadOnlyList<RefreshToken>> FindActiveByUserIdAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Adds a new refresh token to the repository.
    /// </summary>
    /// <param name="refreshToken">The refresh token entity containing the token, family ID, and expiration.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Token Generation:</strong> Generate tokens using <c>RandomNumberGenerator</c>
    /// with at least 32 bytes of random data.
    /// </para>
    /// <para>
    /// For new login sessions, generate a new family ID. For token rotation within
    /// an existing session, preserve the family ID.
    /// </para>
    /// </remarks>
    Task AddAsync(RefreshToken refreshToken, CancellationToken ct = default);

    /// <summary>
    /// Revokes all active refresh tokens for a user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="revokedAt">The timestamp when the revocation occurred.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item><description>User-initiated "sign out from all devices".</description></item>
    /// <item><description>Password change (security best practice).</description></item>
    /// <item><description>Account compromise detection.</description></item>
    /// <item><description>Administrative account suspension.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Uses bulk update for efficiency; changes are immediately persisted.
    /// </para>
    /// </remarks>
    Task RevokeAllByUserIdAsync(string userId, DateTimeOffset revokedAt, CancellationToken ct = default);

    /// <summary>
    /// Deletes expired and revoked refresh tokens that are past the retention cutoff.
    /// </summary>
    /// <param name="cutoff">Tokens expiring before this date that are also revoked will be deleted.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Maintenance:</strong> Should be called periodically by a background service
    /// to prevent unbounded table growth.
    /// </para>
    /// <para>
    /// Only deletes tokens that are both expired AND revoked to preserve audit trail
    /// for recently revoked tokens.
    /// </para>
    /// </remarks>
    Task DeleteExpiredAsync(DateTimeOffset cutoff, CancellationToken ct = default);

    /// <summary>
    /// Revokes all refresh tokens in a specific token family.
    /// </summary>
    /// <param name="familyId">The unique identifier of the token family.</param>
    /// <param name="revokedAt">The timestamp when the revocation occurred.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Replay Attack Detection:</strong> If a revoked token is presented for refresh,
    /// call this method to revoke all tokens in that family. This protects against
    /// token theft where an attacker replays a stolen refresh token.
    /// </para>
    /// </remarks>
    Task RevokeAllByFamilyIdAsync(string familyId, DateTimeOffset revokedAt, CancellationToken ct = default);

    /// <summary>
    /// Counts the number of distinct active token families for a user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>The count of active token families.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Session Limiting:</strong> Use this to enforce a maximum number of
    /// concurrent sessions per user. If the limit is exceeded, consider revoking
    /// the oldest family or rejecting the new login.
    /// </para>
    /// </remarks>
    Task<int> CountActiveFamiliesByUserIdAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Persists all pending changes to the database.
    /// </summary>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveChangesAsync(CancellationToken ct = default);
}
