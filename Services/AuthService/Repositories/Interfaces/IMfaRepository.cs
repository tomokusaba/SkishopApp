using AuthService.Models;

namespace AuthService.Repositories.Interfaces;

/// <summary>
/// Repository interface for managing Multi-Factor Authentication (MFA) configurations.
/// </summary>
/// <remarks>
/// <para>
/// Provides data access operations for MFA settings including TOTP secret keys,
/// backup codes, and MFA enablement status.
/// </para>
/// <para>
/// <strong>Security Considerations:</strong>
/// <list type="bullet">
/// <item><description>MFA secrets are highly sensitive cryptographic material and must be stored encrypted at rest.</description></item>
/// <item><description>Backup codes should be hashed before storage; never store plaintext backup codes.</description></item>
/// <item><description>Access to MFA data should be restricted to the owning user and authorized administrative operations.</description></item>
/// <item><description>MFA secret retrieval should be logged for security audit purposes.</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Data Access Pattern:</strong>
/// Each user has at most one MFA configuration record. The repository enforces
/// this one-to-one relationship through the user ID lookup.
/// </para>
/// </remarks>
public interface IMfaRepository
{
    /// <summary>
    /// Retrieves the MFA configuration for a specific user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user whose MFA configuration is being retrieved.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>
    /// The <see cref="UserMfa"/> entity if found; otherwise, <c>null</c> indicating MFA is not configured.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Security:</strong> The returned entity contains the TOTP secret key.
    /// Ensure proper access control before exposing this data.
    /// </para>
    /// <para>
    /// Returns null when the user has not set up MFA, which is the default state for new users.
    /// </para>
    /// </remarks>
    Task<UserMfa?> FindByUserIdAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Adds a new MFA configuration for a user.
    /// </summary>
    /// <param name="mfa">The MFA configuration entity containing the secret key and settings.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Security:</strong> The MFA secret should be generated using a cryptographically
    /// secure random number generator before being passed to this method.
    /// </para>
    /// <para>
    /// <strong>Transaction:</strong> Call <see cref="SaveChangesAsync"/> to persist the MFA configuration.
    /// Consider wrapping this in a transaction with user verification status updates.
    /// </para>
    /// </remarks>
    Task AddAsync(UserMfa mfa, CancellationToken ct = default);

    /// <summary>
    /// Persists all pending changes to the database.
    /// </summary>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveChangesAsync(CancellationToken ct = default);
}
