using AuthService.Infrastructure.Persistence;
using AuthService.Models;
using AuthService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Repositories;

/// <summary>
/// Repository implementation for managing user authentication sessions using Entity Framework Core.
/// </summary>
/// <remarks>
/// <para>
/// Provides data access for user sessions which track active logins, device information,
/// and session metadata for security monitoring and management.
/// </para>
/// <para>
/// <strong>Security:</strong> Session tokens must be cryptographically random and should
/// be stored as secure hashes for defense in depth.
/// </para>
/// </remarks>
/// <param name="context">The database context for accessing session data.</param>
/// <param name="timeProvider">The time provider for consistent timestamp generation.</param>
public class UserSessionRepository(AuthDbContext context, TimeProvider timeProvider) : IUserSessionRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// Uses tracked query to allow immediate session status updates.
    /// Used for session management operations and administrative session revocation.
    /// </remarks>
    public async Task<UserSession?> FindBySessionIdAsync(string sessionId, CancellationToken ct = default)
        => await context.UserSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Uses tracked query to allow update of LastActivityAt timestamp
    /// during session validation.
    /// </para>
    /// <para>
    /// <strong>Validation:</strong> After retrieval, verify the session is still active
    /// and not expired before granting access.
    /// </para>
    /// </remarks>
    public async Task<UserSession?> FindBySessionTokenAsync(string sessionToken, CancellationToken ct = default)
        => await context.UserSessions
            .FirstOrDefaultAsync(s => s.SessionToken == sessionToken, ct);

    /// <inheritdoc />
    /// <remarks>
    /// Uses <c>AsNoTracking</c> for read-only access. Returns only active sessions
    /// for display in user security settings and concurrent session limiting.
    /// </remarks>
    public async Task<IReadOnlyList<UserSession>> FindActiveByUserIdAsync(string userId, CancellationToken ct = default)
        => await context.UserSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Uses <c>ExecuteUpdateAsync</c> for efficient bulk update without loading entities.
    /// Changes are immediately persisted.
    /// </para>
    /// <para>
    /// Updates both the IsActive flag and UpdatedAt timestamp using the
    /// injected <see cref="TimeProvider"/>.
    /// </para>
    /// </remarks>
    public async Task DeactivateSessionAsync(string sessionId, CancellationToken ct = default)
    {
        await context.UserSessions
            .Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.IsActive, false)
                .SetProperty(s => s.UpdatedAt, timeProvider.GetUtcNow()), ct);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Adds the session entity to the change tracker.
    /// </para>
    /// <para>
    /// <strong>Security:</strong> Generate session tokens using <c>RandomNumberGenerator</c>
    /// with at least 32 bytes of random data.
    /// </para>
    /// </remarks>
    public async Task AddAsync(UserSession session, CancellationToken ct = default)
        => await context.UserSessions.AddAsync(session, ct);

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
