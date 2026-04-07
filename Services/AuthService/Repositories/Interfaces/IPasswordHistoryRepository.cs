using AuthService.Models;

namespace AuthService.Repositories.Interfaces;

/// <summary>
/// Repository interface for managing password history records.
/// </summary>
/// <remarks>
/// <para>
/// Provides data access operations for tracking password changes to enforce
/// password reuse policies and security compliance requirements.
/// </para>
/// <para>
/// <strong>Security Considerations:</strong>
/// <list type="bullet">
/// <item><description>Password hashes in history records must use the same strong hashing algorithm as current passwords (e.g., PBKDF2, Argon2).</description></item>
/// <item><description>Password history is security-sensitive data; access should be restricted to password change operations only.</description></item>
/// <item><description>Consider implementing a retention policy to limit history storage while meeting compliance requirements.</description></item>
/// <item><description>Never log or expose password history hashes in error messages or responses.</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Policy Enforcement:</strong>
/// Typically configured to retain the last 5-12 password hashes to prevent
/// recent password reuse while limiting storage requirements.
/// </para>
/// </remarks>
public interface IPasswordHistoryRepository
{
    /// <summary>
    /// Retrieves the most recent password history entries for a user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="count">The maximum number of recent entries to retrieve. Default is 5.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A read-only list of recent <see cref="PasswordHistory"/> entries, ordered by most recent first.</returns>
    /// <remarks>
    /// <para>
    /// Used during password change to verify the new password has not been used recently.
    /// Compare the new password hash against each historical hash to enforce reuse policy.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Uses read-only tracking and limits results to the
    /// configured policy count for optimal query performance.
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<PasswordHistory>> FindRecentByUserIdAsync(string userId, int count = 5, CancellationToken ct = default);

    /// <summary>
    /// Adds a new password history entry when a user changes their password.
    /// </summary>
    /// <param name="history">The password history entity containing the hashed password and timestamp.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Security:</strong> The password hash should be computed using the same
    /// algorithm as the user's current password hash.
    /// </para>
    /// <para>
    /// <strong>Transaction:</strong> Add the history entry within the same transaction
    /// as the password update to maintain consistency.
    /// </para>
    /// </remarks>
    Task AddAsync(PasswordHistory history, CancellationToken ct = default);

    /// <summary>
    /// Persists all pending changes to the database.
    /// </summary>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveChangesAsync(CancellationToken ct = default);
}
