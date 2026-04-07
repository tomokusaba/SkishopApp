using AuthService.Infrastructure.Persistence;
using AuthService.Models;
using AuthService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Repositories;

/// <summary>
/// Repository implementation for managing password history records using Entity Framework Core.
/// </summary>
/// <remarks>
/// <para>
/// Provides data access for tracking password changes to enforce password reuse policies
/// and security compliance requirements.
/// </para>
/// <para>
/// <strong>Security:</strong> Password hashes in history records use the same strong
/// hashing algorithm as current passwords. Access should be restricted to password
/// change operations only.
/// </para>
/// </remarks>
/// <param name="context">The database context for accessing password history data.</param>
public class PasswordHistoryRepository(AuthDbContext context) : IPasswordHistoryRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Uses <c>AsNoTracking</c> for read-only access during password change validation.
    /// Results are ordered by most recent first and limited to the specified count.
    /// </para>
    /// <para>
    /// Compare the new password hash against each historical hash to enforce
    /// the password reuse policy.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<PasswordHistory>> FindRecentByUserIdAsync(string userId, int count = 5, CancellationToken ct = default)
        => await context.PasswordHistories
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(count)
            .ToListAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Adds the password history entry to the change tracker.
    /// Should be called within the same transaction as the password update.
    /// </para>
    /// <para>
    /// <strong>Security:</strong> The password hash should be computed using the same
    /// algorithm as the user's current password hash.
    /// </para>
    /// </remarks>
    public async Task AddAsync(PasswordHistory history, CancellationToken ct = default)
        => await context.PasswordHistories.AddAsync(history, ct);

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
