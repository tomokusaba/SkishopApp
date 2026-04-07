using AuthService.Models;

namespace AuthService.Repositories.Interfaces;

/// <summary>
/// Repository interface for managing OAuth external identity provider account linkages.
/// </summary>
/// <remarks>
/// <para>
/// Provides data access operations for linking and managing external OAuth providers
/// (e.g., Google, Microsoft, GitHub) with internal user accounts.
/// </para>
/// <para>
/// <strong>Security Considerations:</strong>
/// <list type="bullet">
/// <item><description>Provider user IDs are considered PII and should be handled accordingly.</description></item>
/// <item><description>OAuth access tokens and refresh tokens stored in accounts must be encrypted at rest.</description></item>
/// <item><description>Account unlinking operations should be logged for security audit purposes.</description></item>
/// <item><description>Validate provider identity before linking to prevent account takeover attacks.</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Data Access Pattern:</strong>
/// A user may have multiple OAuth accounts (one per provider), while each provider
/// user ID maps to exactly one internal user account.
/// </para>
/// </remarks>
public interface IOAuthAccountRepository
{
    /// <summary>
    /// Finds an OAuth account by provider name and the provider's user identifier.
    /// </summary>
    /// <param name="provider">The OAuth provider name (e.g., "Google", "Microsoft", "GitHub").</param>
    /// <param name="providerUserId">The unique user identifier assigned by the OAuth provider.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>
    /// The <see cref="OAuthAccount"/> if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Used during OAuth login flow to check if the external identity is already linked
    /// to an existing user account.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Uses read-only tracking (AsNoTracking) for optimal query performance.
    /// </para>
    /// </remarks>
    Task<OAuthAccount?> FindByProviderAndProviderUserIdAsync(string provider, string providerUserId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all OAuth account linkages for a specific user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A read-only list of all OAuth accounts linked to the user.</returns>
    /// <remarks>
    /// Used to display connected accounts in user profile settings and to determine
    /// available authentication methods.
    /// </remarks>
    Task<IReadOnlyList<OAuthAccount>> FindByUserIdAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Removes the OAuth account linkage for a specific user and provider.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="provider">The OAuth provider name to unlink.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Security:</strong> This operation should be protected by re-authentication
    /// to prevent unauthorized unlinking.
    /// </para>
    /// <para>
    /// <strong>Transaction:</strong> Uses ExecuteDeleteAsync for efficient bulk deletion
    /// without loading entities into memory. Changes are immediately persisted.
    /// </para>
    /// </remarks>
    Task DeleteByUserIdAndProviderAsync(string userId, string provider, CancellationToken ct = default);

    /// <summary>
    /// Adds a new OAuth account linkage.
    /// </summary>
    /// <param name="account">The OAuth account entity containing provider details and tokens.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Security:</strong> Ensure the OAuth tokens in the account entity are encrypted
    /// before storage if token persistence is required.
    /// </para>
    /// <para>
    /// Call <see cref="SaveChangesAsync"/> to persist the new account linkage.
    /// </para>
    /// </remarks>
    Task AddAsync(OAuthAccount account, CancellationToken ct = default);

    /// <summary>
    /// Persists all pending changes to the database.
    /// </summary>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveChangesAsync(CancellationToken ct = default);
}
