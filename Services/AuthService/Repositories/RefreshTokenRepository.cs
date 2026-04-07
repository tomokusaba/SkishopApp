using AuthService.Infrastructure.Persistence;
using AuthService.Models;
using AuthService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Repositories;

/// <summary>
/// Repository implementation for managing OAuth 2.0 refresh tokens with token rotation support using Entity Framework Core.
/// </summary>
/// <remarks>
/// <para>
/// Provides data access for refresh tokens implementing secure token rotation and
/// family-based revocation for replay attack detection.
/// </para>
/// <para>
/// <strong>Security:</strong> Tokens are grouped into families representing login sessions.
/// If a revoked token is reused (replay attack), the entire family is revoked.
/// </para>
/// </remarks>
/// <param name="context">The database context for accessing refresh token data.</param>
/// <param name="timeProvider">The time provider for consistent timestamp generation.</param>
public class RefreshTokenRepository(AuthDbContext context, TimeProvider timeProvider) : IRefreshTokenRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Uses tracked query to allow immediate update of token status after validation.
    /// </para>
    /// <para>
    /// <strong>Security:</strong> If the token is found but revoked, this may indicate
    /// a replay attack - the caller should revoke the entire token family.
    /// </para>
    /// </remarks>
    public async Task<RefreshToken?> FindByTokenAsync(string token, CancellationToken ct = default)
        => await context.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == token, ct);

    /// <inheritdoc />
    /// <remarks>
    /// Uses <c>AsNoTracking</c> for read-only access. Filters for non-revoked tokens
    /// that have not expired based on the injected <see cref="TimeProvider"/>.
    /// </remarks>
    public async Task<IReadOnlyList<RefreshToken>> FindActiveByUserIdAsync(string userId, CancellationToken ct = default)
        => await context.RefreshTokens
            .AsNoTracking()
            .Where(r => r.UserId == userId && !r.IsRevoked && r.ExpiresAt > timeProvider.GetUtcNow())
            .ToListAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// Adds the refresh token to the change tracker. For new login sessions,
    /// generate a new family ID. For token rotation, preserve the existing family ID.
    /// </remarks>
    public async Task AddAsync(RefreshToken refreshToken, CancellationToken ct = default)
        => await context.RefreshTokens.AddAsync(refreshToken, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Uses <c>ExecuteUpdateAsync</c> for efficient bulk update without loading entities.
    /// Changes are immediately persisted.
    /// </para>
    /// <para>
    /// Common use cases: "Sign out from all devices", password change, account compromise.
    /// </para>
    /// </remarks>
    public async Task RevokeAllByUserIdAsync(string userId, DateTimeOffset revokedAt, CancellationToken ct = default)
        => await context.RefreshTokens
            .Where(r => r.UserId == userId && !r.IsRevoked)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.IsRevoked, true)
                .SetProperty(r => r.RevokedAt, revokedAt)
                .SetProperty(r => r.UpdatedAt, timeProvider.GetUtcNow()), ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Uses <c>ExecuteDeleteAsync</c> for efficient bulk deletion without loading entities.
    /// Only deletes tokens that are both expired AND revoked to preserve audit trail.
    /// </para>
    /// <para>
    /// Should be called periodically by a background service to prevent table growth.
    /// </para>
    /// </remarks>
    public async Task DeleteExpiredAsync(DateTimeOffset cutoff, CancellationToken ct = default)
        => await context.RefreshTokens
            .Where(r => r.ExpiresAt < cutoff && r.IsRevoked)
            .ExecuteDeleteAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Uses <c>ExecuteUpdateAsync</c> for efficient bulk update without loading entities.
    /// </para>
    /// <para>
    /// <strong>Replay Attack Detection:</strong> Call this method when a revoked token
    /// is presented for refresh to invalidate all tokens in the compromised family.
    /// </para>
    /// </remarks>
    public async Task RevokeAllByFamilyIdAsync(string familyId, DateTimeOffset revokedAt, CancellationToken ct = default)
        => await context.RefreshTokens
            .Where(r => r.FamilyId == familyId && !r.IsRevoked)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.IsRevoked, true)
                .SetProperty(r => r.RevokedAt, revokedAt)
                .SetProperty(r => r.UpdatedAt, timeProvider.GetUtcNow()), ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Uses <c>AsNoTracking</c> for read-only access. Counts distinct active families
    /// based on current time from the injected <see cref="TimeProvider"/>.
    /// </para>
    /// <para>
    /// Used to enforce concurrent session limits per user.
    /// </para>
    /// </remarks>
    public async Task<int> CountActiveFamiliesByUserIdAsync(string userId, CancellationToken ct = default)
        => await context.RefreshTokens
            .AsNoTracking()
            .Where(r => r.UserId == userId && !r.IsRevoked && r.ExpiresAt > timeProvider.GetUtcNow())
            .Select(r => r.FamilyId)
            .Distinct()
            .CountAsync(ct);

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
