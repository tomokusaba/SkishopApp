using AuthService.Models;

namespace AuthService.Repositories.Interfaces;

/// <summary>
/// Repository interface for managing user account data.
/// </summary>
/// <remarks>
/// <para>
/// Provides data access operations for user accounts, which serve as the primary
/// identity entity in the authentication system. This is the core repository for
/// user identity management.
/// </para>
/// <para>
/// <strong>Security Considerations:</strong>
/// <list type="bullet">
/// <item><description>Passwords must be hashed using a strong algorithm (PBKDF2, Argon2) before storage; never store plaintext.</description></item>
/// <item><description>Email addresses should be normalized (lowercase, trimmed) to prevent duplicate accounts.</description></item>
/// <item><description>User lookup methods should have consistent timing to prevent enumeration attacks.</description></item>
/// <item><description>PII (Personally Identifiable Information) access should be logged for compliance.</description></item>
/// <item><description>Implement soft delete to retain audit trail while respecting GDPR deletion requests.</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Data Access Patterns:</strong>
/// <list type="bullet">
/// <item><description><see cref="FindByEmailAsync"/>: Read-only lookup for existence checks and profile display.</description></item>
/// <item><description><see cref="FindByEmailForLoginAsync"/>: Tracked query with MFA data for authentication flow.</description></item>
/// <item><description><see cref="FindByIdAsync"/>: Tracked query for user data modifications.</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IUserRepository
{
    /// <summary>
    /// Finds a user by their email address (read-only).
    /// </summary>
    /// <param name="email">The email address to search for.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>
    /// The <see cref="User"/> entity if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Used for existence checks during registration and profile lookups.
    /// Does not load related data (MFA, roles) for performance.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Uses read-only tracking (AsNoTracking) for optimal query performance.
    /// </para>
    /// <para>
    /// <strong>Security:</strong> Email comparison should be case-insensitive to prevent
    /// duplicate accounts with different casing.
    /// </para>
    /// </remarks>
    Task<User?> FindByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Finds a user by email with MFA configuration for authentication.
    /// </summary>
    /// <param name="email">The email address to search for.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>
    /// The <see cref="User"/> entity with MFA data loaded if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Authentication Flow:</strong> This method is specifically designed for
    /// the login process where MFA status must be checked. Includes eager loading of
    /// MFA configuration via <c>Include(u =&gt; u.Mfa)</c>.
    /// </para>
    /// <para>
    /// <strong>Tracking:</strong> Uses tracked query to allow immediate update of
    /// login-related fields (LastLoginAt, FailedLoginAttempts, etc.).
    /// </para>
    /// <para>
    /// <strong>Security:</strong> Ensure consistent response time regardless of whether
    /// user exists to prevent timing-based enumeration attacks.
    /// </para>
    /// </remarks>
    Task<User?> FindByEmailForLoginAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Finds a user by their unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the user.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>
    /// The <see cref="User"/> entity if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Tracking:</strong> Uses tracked query to allow modifications to the user entity.
    /// Use this method when you need to update user data.
    /// </para>
    /// <para>
    /// Commonly used for:
    /// <list type="bullet">
    /// <item><description>Profile updates.</description></item>
    /// <item><description>Password changes.</description></item>
    /// <item><description>Account status modifications.</description></item>
    /// <item><description>Role assignments.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    Task<User?> FindByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Finds a user by their username.
    /// </summary>
    /// <param name="username">The username to search for.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>
    /// The <see cref="User"/> entity if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Used for username uniqueness validation during registration and
    /// username-based login if supported.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Uses read-only tracking for optimal query performance.
    /// </para>
    /// <para>
    /// <strong>Security:</strong> Username comparison should be case-insensitive to prevent
    /// confusion and potential impersonation.
    /// </para>
    /// </remarks>
    Task<User?> FindByUsernameAsync(string username, CancellationToken ct = default);

    /// <summary>
    /// Adds a new user account to the repository.
    /// </summary>
    /// <param name="user">The user entity containing account details.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Pre-conditions:</strong>
    /// <list type="bullet">
    /// <item><description>Email must be validated and normalized (lowercase, trimmed).</description></item>
    /// <item><description>Password must be hashed using a strong algorithm.</description></item>
    /// <item><description>Username uniqueness must be verified.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Transaction:</strong> Call <see cref="SaveChangesAsync"/> to persist the new user.
    /// Consider wrapping registration in a transaction that includes role assignment
    /// and welcome email scheduling.
    /// </para>
    /// </remarks>
    Task AddAsync(User user, CancellationToken ct = default);

    /// <summary>
    /// Persists all pending changes to the database.
    /// </summary>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveChangesAsync(CancellationToken ct = default);
}
