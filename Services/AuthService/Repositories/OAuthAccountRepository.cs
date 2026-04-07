using AuthService.Infrastructure.Persistence;
using AuthService.Models;
using AuthService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Repositories;

/// <summary>
/// Repository implementation for managing OAuth external identity provider account linkages using Entity Framework Core.
/// </summary>
/// <remarks>
/// <para>
/// Provides data access for linking and managing external OAuth providers
/// (e.g., Google, Microsoft, GitHub) with internal user accounts.
/// </para>
/// <para>
/// <strong>Security:</strong> OAuth tokens stored in accounts should be encrypted at rest.
/// Account unlinking operations should be logged for security audit purposes.
/// </para>
/// </remarks>
/// <param name="context">The database context for accessing OAuth account data.</param>
public class OAuthAccountRepository(AuthDbContext context) : IOAuthAccountRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Uses <c>AsNoTracking</c> for read-only access to improve query performance.
    /// </para>
    /// <para>
    /// Used during OAuth login flow to determine if the external identity
    /// is already linked to an existing user account.
    /// </para>
    /// </remarks>
    public async Task<OAuthAccount?> FindByProviderAndProviderUserIdAsync(string provider, string providerUserId, CancellationToken ct = default)
        => await context.OAuthAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Provider == provider && o.ProviderUserId == providerUserId, ct);

    /// <inheritdoc />
    /// <remarks>
    /// Uses <c>AsNoTracking</c> for read-only access. Returns all OAuth accounts
    /// linked to the user for display in profile settings.
    /// </remarks>
    public async Task<IReadOnlyList<OAuthAccount>> FindByUserIdAsync(string userId, CancellationToken ct = default)
        => await context.OAuthAccounts
            .AsNoTracking()
            .Where(o => o.UserId == userId)
            .ToListAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Uses <c>ExecuteDeleteAsync</c> for efficient bulk deletion without loading
    /// entities into memory. Changes are immediately persisted.
    /// </para>
    /// <para>
    /// <strong>Security:</strong> This operation should be protected by re-authentication
    /// and logged for audit purposes.
    /// </para>
    /// </remarks>
    public async Task DeleteByUserIdAndProviderAsync(string userId, string provider, CancellationToken ct = default)
    {
        await context.OAuthAccounts
            .Where(a => a.UserId == userId && a.Provider == provider)
            .ExecuteDeleteAsync(ct);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Adds the OAuth account linkage to the change tracker.
    /// Call <see cref="SaveChangesAsync"/> to persist the new linkage.
    /// </remarks>
    public async Task AddAsync(OAuthAccount account, CancellationToken ct = default)
        => await context.OAuthAccounts.AddAsync(account, ct);

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
