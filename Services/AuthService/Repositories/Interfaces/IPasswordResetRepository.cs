using AuthService.Models;

namespace AuthService.Repositories.Interfaces;

/// <summary>
/// Repository interface for managing password reset tokens and email verification tokens.
/// </summary>
/// <remarks>
/// <para>
/// Provides data access operations for time-limited security tokens used in
/// password reset flows, email verification, and account recovery processes.
/// </para>
/// <para>
/// <strong>Security Considerations:</strong>
/// <list type="bullet">
/// <item><description>Reset tokens should be cryptographically random (minimum 256 bits of entropy).</description></item>
/// <item><description>Tokens must have short expiration times (typically 15-60 minutes for password reset).</description></item>
/// <item><description>Tokens should be single-use; mark as used immediately upon successful verification.</description></item>
/// <item><description>Rate limit token generation to prevent enumeration and abuse attacks.</description></item>
/// <item><description>Invalidate all existing tokens when a new token is requested to prevent token accumulation.</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Token Types:</strong>
/// This repository handles multiple token types including password reset tokens,
/// email verification tokens, and account recovery tokens, distinguished by the token type field.
/// </para>
/// </remarks>
public interface IPasswordResetRepository
{
    /// <summary>
    /// Finds a password reset or verification token by its token value.
    /// </summary>
    /// <param name="token">The token string to search for.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>
    /// The <see cref="PasswordReset"/> entity if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Validation:</strong> After retrieving the token, validate:
    /// <list type="bullet">
    /// <item><description>Token has not expired (check expiration timestamp).</description></item>
    /// <item><description>Token has not been used (check IsUsed flag).</description></item>
    /// <item><description>Token type matches the expected operation.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Uses tracking to allow immediate update of the token's IsUsed flag after validation.
    /// </para>
    /// </remarks>
    Task<PasswordReset?> FindByTokenAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Adds a new password reset or verification token.
    /// </summary>
    /// <param name="passwordReset">The password reset entity containing the token, user ID, and expiration.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Security:</strong> Generate the token using <c>RandomNumberGenerator</c>
    /// with at least 32 bytes of random data, then encode as Base64Url.
    /// </para>
    /// <para>
    /// Consider calling <see cref="InvalidateExistingTokensAsync"/> before adding
    /// a new token to prevent token accumulation.
    /// </para>
    /// </remarks>
    Task AddAsync(PasswordReset passwordReset, CancellationToken ct = default);

    /// <summary>
    /// Invalidates all existing unused tokens for a user and token type.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="tokenType">The type of token to invalidate (e.g., "PasswordReset", "EmailVerification").</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Security:</strong> Call this method before generating a new token to:
    /// <list type="bullet">
    /// <item><description>Prevent token accumulation that could be exploited.</description></item>
    /// <item><description>Ensure only the latest token is valid.</description></item>
    /// <item><description>Clean up tokens from incomplete or abandoned flows.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Transaction:</strong> Include this operation in the same transaction
    /// as the new token creation for atomicity.
    /// </para>
    /// </remarks>
    Task InvalidateExistingTokensAsync(string userId, string tokenType, CancellationToken ct = default);

    /// <summary>
    /// Persists all pending changes to the database.
    /// </summary>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveChangesAsync(CancellationToken ct = default);
}
