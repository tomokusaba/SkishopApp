using Microsoft.EntityFrameworkCore;
using UserManagementService.Infrastructure.Persistence;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;

namespace UserManagementService.Repositories;

/// <summary>
/// 住所の EF Core Repository 実装。デフォルト住所優先でソートして返却する。
/// </summary>
public class AddressRepository(AppDbContext context) : IAddressRepository
{
    public async Task<Address?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.Addresses.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<List<Address>> FindByUserIdAsync(string userId, CancellationToken ct = default)
        => await context.Addresses
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

    public async Task<Address?> FindDefaultByUserIdAsync(string userId, string addressType, CancellationToken ct = default)
        => await context.Addresses
            .FirstOrDefaultAsync(a => a.UserId == userId && a.AddressType == addressType && a.IsDefault, ct);

    public async Task<int> CountByUserIdAsync(string userId, CancellationToken ct = default)
        => await context.Addresses.CountAsync(a => a.UserId == userId, ct);

    public async Task AddAsync(Address address, CancellationToken ct = default)
        => await context.Addresses.AddAsync(address, ct);

    public void Remove(Address address)
        => context.Addresses.Remove(address);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
