using AuthService.Infrastructure.Persistence;
using AuthService.Models;
using AuthService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Repositories;

/// <summary>
/// Repository implementation for managing user roles and permissions using Entity Framework Core.
/// </summary>
/// <remarks>
/// <para>
/// Provides read-only data access for roles used in Role-Based Access Control (RBAC).
/// Role management operations are typically handled through administrative interfaces.
/// </para>
/// <para>
/// <strong>Caching:</strong> Role data changes infrequently. Consider implementing
/// caching for role lookups with proper cache invalidation.
/// </para>
/// </remarks>
/// <param name="context">The database context for accessing role data.</param>
public class RoleRepository(AuthDbContext context) : IRoleRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Uses <c>AsNoTracking</c> for read-only access. Role lookups are common
    /// during user registration and authorization checks.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Consider caching role lookups as roles change infrequently.
    /// </para>
    /// </remarks>
    public async Task<Role?> FindByNameAsync(string name, CancellationToken ct = default)
        => await context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == name, ct);

    /// <inheritdoc />
    /// <remarks>
    /// Uses <c>AsNoTracking</c> for read-only access. Returns all available roles
    /// for administrative interfaces and role assignment UI.
    /// </remarks>
    public async Task<IReadOnlyList<Role>> FindAllAsync(CancellationToken ct = default)
        => await context.Roles
            .AsNoTracking()
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
