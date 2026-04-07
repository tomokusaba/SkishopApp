using AuthService.Infrastructure.Persistence;
using AuthService.Models;
using AuthService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Repositories;

/// <summary>
/// Repository implementation for managing OAuth 2.0 client application registrations using Entity Framework Core.
/// </summary>
/// <remarks>
/// <para>
/// Provides data access for OAuth client applications that are authorized
/// to request access tokens on behalf of users or as service accounts.
/// </para>
/// <para>
/// <strong>Security:</strong> Client secrets must be stored as secure hashes.
/// Client registration should be restricted to administrators.
/// </para>
/// </remarks>
/// <param name="context">The database context for accessing OAuth client data.</param>
public class OAuthClientRepository(AuthDbContext context) : IOAuthClientRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Uses <c>AsNoTracking</c> for read-only access during OAuth authorization
    /// and token validation flows.
    /// </para>
    /// <para>
    /// Called during OAuth authorization requests to validate client credentials
    /// and retrieve allowed scopes and redirect URIs.
    /// </para>
    /// </remarks>
    public async Task<OAuthClient?> FindByClientIdAsync(string clientId, CancellationToken ct = default)
        => await context.OAuthClients
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.ClientId == clientId, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Registers a new OAuth client. The client secret should be hashed
    /// using a strong algorithm before calling this method.
    /// </para>
    /// <para>
    /// <strong>Administrative Operation:</strong> This operation requires administrative
    /// privileges and should be logged for audit purposes.
    /// </para>
    /// </remarks>
    public async Task AddAsync(OAuthClient client, CancellationToken ct = default)
        => await context.OAuthClients.AddAsync(client, ct);

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
