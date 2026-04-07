using Microsoft.EntityFrameworkCore;
using UserManagementService.Infrastructure.Persistence;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;

namespace UserManagementService.Repositories;

/// <summary>
/// User Aggregate Root の EF Core Repository 実装。
/// 読み取り専用クエリでは AsNoTracking を適用しパフォーマンスを最適化する。
/// </summary>
public class UserRepository(AppDbContext context) : IUserRepository
{
    public async Task<User?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> FindByIdReadOnlyAsync(string id, CancellationToken ct = default)
        => await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> FindByIdWithDetailsAsync(string id, CancellationToken ct = default)
        => await context.Users
            .Include(u => u.Preference)
            .Include(u => u.MemberRank)
            .Include(u => u.Addresses)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> FindByEmailAsync(string email, CancellationToken ct = default)
        => await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task<(List<User> Items, int TotalCount)> FindAllAsync(
        int page, int pageSize, string? statusFilter, CancellationToken ct = default)
    {
        var query = context.Users.AsNoTracking().AsQueryable();
        if (statusFilter is not null)
            query = query.Where(u => u.Status == statusFilter);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await context.Users.AddAsync(user, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
