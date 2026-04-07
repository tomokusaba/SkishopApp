using AuthService.Infrastructure.Persistence;
using AuthService.Models;
using AuthService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Repositories;

/// <summary>
/// Repository implementation for managing password reset and email verification tokens using Entity Framework Core.
/// </summary>
/// <remarks>
/// <para>
/// Provides data access for time-limited security tokens used in password reset flows,
/// email verification, and account recovery processes.
/// </para>
/// <para>
/// <strong>Security:</strong> Tokens should be cryptographically random (minimum 256 bits),
/// have short expiration times, and be single-use. All existing tokens are invalidated
/// when a new token is requested.
/// </para>
/// </remarks>
/// <param name="context">The database context for accessing password reset token data.</param>
public class PasswordResetRepository(AuthDbContext context) : IPasswordResetRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Uses tracked query to allow immediate update of the token's IsUsed flag
    /// after successful validation.
    /// </para>
    /// <para>
    /// <strong>Validation:</strong> After retrieval, the caller should validate that
    /// the token has not expired and has not been used.
    /// </para>
    /// </remarks>
    public async Task<PasswordReset?> FindByTokenAsync(string token, CancellationToken ct = default)
        => await context.PasswordResets
            .FirstOrDefaultAsync(p => p.Token == token, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Adds the password reset token to the change tracker.
    /// The token should be generated using <c>RandomNumberGenerator</c>
    /// with at least 32 bytes of random data.
    /// </para>
    /// <para>
    /// Consider calling <see cref="InvalidateExistingTokensAsync"/> before adding
    /// a new token to prevent token accumulation.
    /// </para>
    /// </remarks>
    public async Task AddAsync(PasswordReset passwordReset, CancellationToken ct = default)
        => await context.PasswordResets.AddAsync(passwordReset, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Loads all existing unused tokens for the user and token type, then marks
    /// them as used. This prevents token accumulation and ensures only the
    /// latest token is valid.
    /// </para>
    /// <para>
    /// <strong>Transaction:</strong> Include this operation in the same transaction
    /// as the new token creation for atomicity.
    /// </para>
    /// </remarks>
    public async Task InvalidateExistingTokensAsync(string userId, string tokenType, CancellationToken ct = default)
    {
        var existingTokens = await context.PasswordResets
            .Where(p => p.UserId == userId && p.TokenType == tokenType && !p.IsUsed)
            .ToListAsync(ct);

        foreach (var existingToken in existingTokens)
        {
            existingToken.IsUsed = true;
        }
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
