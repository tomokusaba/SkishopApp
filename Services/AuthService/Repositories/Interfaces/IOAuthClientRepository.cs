using AuthService.Models;

namespace AuthService.Repositories.Interfaces;

/// <summary>
/// Repository interface for managing OAuth 2.0 client application registrations.
/// </summary>
/// <remarks>
/// <para>
/// Provides data access operations for OAuth client applications that are authorized
/// to request access tokens on behalf of users or as service accounts.
/// </para>
/// <para>
/// <strong>Security Considerations:</strong>
/// <list type="bullet">
/// <item><description>Client secrets must be stored using one-way hashing (e.g., PBKDF2, Argon2); never store plaintext secrets.</description></item>
/// <item><description>Client credentials should be rotated periodically according to security policy.</description></item>
/// <item><description>Redirect URIs must be validated exactly; wildcard patterns should be avoided.</description></item>
/// <item><description>Client registration and modification operations require administrative privileges.</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>OAuth 2.0 Compliance:</strong>
/// Client entities should store all required OAuth 2.0 metadata including
/// allowed grant types, scopes, and redirect URIs.
/// </para>
/// </remarks>
public interface IOAuthClientRepository
{
    /// <summary>
    /// Finds an OAuth client by its client identifier.
    /// </summary>
    /// <param name="clientId">The public client identifier used in OAuth flows.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>
    /// The <see cref="OAuthClient"/> if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Called during OAuth authorization and token requests to validate the client
    /// and retrieve its configuration (allowed scopes, redirect URIs, grant types).
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Uses read-only tracking for optimal query performance
    /// as client validation is a read-only operation.
    /// </para>
    /// </remarks>
    Task<OAuthClient?> FindByClientIdAsync(string clientId, CancellationToken ct = default);

    /// <summary>
    /// Registers a new OAuth client application.
    /// </summary>
    /// <param name="client">The OAuth client entity containing client ID, hashed secret, and configuration.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Security:</strong> The client secret in the entity must be hashed before
    /// passing to this method. Never store plaintext client secrets.
    /// </para>
    /// <para>
    /// <strong>Administrative Operation:</strong> Client registration should be restricted
    /// to administrators and logged for audit purposes.
    /// </para>
    /// </remarks>
    Task AddAsync(OAuthClient client, CancellationToken ct = default);

    /// <summary>
    /// Persists all pending changes to the database.
    /// </summary>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveChangesAsync(CancellationToken ct = default);
}
