using AuthService.Infrastructure.Persistence;
using AuthService.Models;
using AuthService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Repositories;

/// <summary>
/// Repository implementation for managing user account data using Entity Framework Core.
/// </summary>
/// <remarks>
/// <para>
/// Provides data access for user accounts, the primary identity entity in the
/// authentication system. This is the core repository for user identity management.
/// </para>
/// <para>
/// <strong>Security:</strong> Passwords must be hashed using a strong algorithm before storage.
/// User lookup methods should have consistent timing to prevent enumeration attacks.
/// </para>
/// </remarks>
/// <param name="context">The database context for accessing user data.</param>
public class UserRepository(AuthDbContext context) : IUserRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Uses <c>AsNoTracking</c> for read-only access during existence checks
    /// and profile display. Does not load related data for performance.
    /// </para>
    /// <para>
    /// <strong>Security:</strong> Email comparison is case-insensitive to prevent
    /// duplicate accounts with different casing.
    /// </para>
    /// </remarks>
    public async Task<User?> FindByEmailAsync(string email, CancellationToken ct = default)
        => await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Uses tracked query with eager loading of MFA configuration via
    /// <c>Include(u =&gt; u.Mfa)</c> for the authentication flow.
    /// </para>
    /// <para>
    /// <strong>Tracking:</strong> Enables immediate update of login-related fields
    /// such as LastLoginAt, FailedLoginAttempts, and LockoutEnd.
    /// </para>
    /// </remarks>
    public async Task<User?> FindByEmailForLoginAsync(string email, CancellationToken ct = default)
        => await context.Users
            .Include(u => u.Mfa)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

    /// <inheritdoc />
    /// <remarks>
    /// Uses tracked query to allow modifications to the user entity.
    /// Commonly used for profile updates, password changes, and role assignments.
    /// </remarks>
    public async Task<User?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.Users
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Uses <c>AsNoTracking</c> for read-only access during username
    /// uniqueness validation.
    /// </para>
    /// <para>
    /// <strong>Security:</strong> Username comparison should be case-insensitive
    /// to prevent confusion and potential impersonation.
    /// </para>
    /// </remarks>
    public async Task<User?> FindByUsernameAsync(string username, CancellationToken ct = default)
        => await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Adds the user entity to the change tracker.
    /// </para>
    /// <para>
    /// <strong>Pre-conditions:</strong> Email should be normalized (lowercase, trimmed),
    /// password should be hashed, and username uniqueness should be verified.
    /// </para>
    /// </remarks>
    public async Task AddAsync(User user, CancellationToken ct = default)
        => await context.Users.AddAsync(user, ct);

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
