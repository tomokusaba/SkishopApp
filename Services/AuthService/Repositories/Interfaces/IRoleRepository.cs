using AuthService.Models;

namespace AuthService.Repositories.Interfaces;

/// <summary>
/// Repository interface for managing user roles and permissions.
/// </summary>
/// <remarks>
/// <para>
/// Provides read-only data access operations for roles used in Role-Based Access Control (RBAC).
/// Role management (create, update, delete) is typically handled through administrative interfaces.
/// </para>
/// <para>
/// <strong>Security Considerations:</strong>
/// <list type="bullet">
/// <item><description>Role assignments should be logged for audit trail purposes.</description></item>
/// <item><description>Built-in roles (Admin, User) should be protected from modification or deletion.</description></item>
/// <item><description>Role name lookups should be case-insensitive to prevent bypass through case manipulation.</description></item>
/// <item><description>Cache role data carefully; role changes should invalidate cached permissions promptly.</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Predefined Roles:</strong>
/// The system typically includes predefined roles such as "Admin", "User", and "Moderator"
/// with associated permission sets defined in the application configuration.
/// </para>
/// </remarks>
public interface IRoleRepository
{
    /// <summary>
    /// Finds a role by its unique name.
    /// </summary>
    /// <param name="name">The role name (e.g., "Admin", "User", "Moderator").</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>
    /// The <see cref="Role"/> entity if found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Used during user registration to assign default roles and during
    /// role management operations.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Uses read-only tracking for optimal query performance.
    /// Consider caching role lookups as roles change infrequently.
    /// </para>
    /// </remarks>
    Task<Role?> FindByNameAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all available roles in the system.
    /// </summary>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A read-only list of all <see cref="Role"/> entities.</returns>
    /// <remarks>
    /// <para>
    /// Used in administrative interfaces for role assignment UI and
    /// role management screens.
    /// </para>
    /// <para>
    /// <strong>Caching:</strong> Results can be cached as roles change infrequently.
    /// Implement cache invalidation when roles are modified.
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<Role>> FindAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Persists all pending changes to the database.
    /// </summary>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveChangesAsync(CancellationToken ct = default);
}
