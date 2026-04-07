using AuthService.Infrastructure.Persistence;
using AuthService.Models;
using AuthService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Repositories;

/// <summary>
/// Repository implementation for managing Multi-Factor Authentication (MFA) configurations using Entity Framework Core.
/// </summary>
/// <remarks>
/// <para>
/// Provides data access for MFA settings including TOTP secret keys, backup codes,
/// and MFA enablement status.
/// </para>
/// <para>
/// <strong>Security:</strong> MFA secrets are highly sensitive cryptographic material.
/// Ensure data at rest encryption is enabled for the database.
/// </para>
/// </remarks>
/// <param name="context">The database context for accessing MFA configuration data.</param>
public class MfaRepository(AuthDbContext context) : IMfaRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// Returns the MFA configuration with tracking enabled to allow updates
    /// to backup code usage and other mutable MFA settings.
    /// </remarks>
    public async Task<UserMfa?> FindByUserIdAsync(string userId, CancellationToken ct = default)
        => await context.UserMfa
            .FirstOrDefaultAsync(m => m.UserId == userId, ct);

    /// <inheritdoc />
    /// <remarks>
    /// Adds the MFA configuration to the change tracker. Ensure the TOTP secret
    /// is generated using <c>RandomNumberGenerator</c> before calling this method.
    /// </remarks>
    public async Task AddAsync(UserMfa mfa, CancellationToken ct = default)
        => await context.UserMfa.AddAsync(mfa, ct);

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
