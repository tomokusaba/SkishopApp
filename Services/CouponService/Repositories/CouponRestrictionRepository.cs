using CouponService.Infrastructure.Persistence;
using CouponService.Models;
using CouponService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CouponService.Repositories;

public class CouponRestrictionRepository(AppDbContext context) : ICouponRestrictionRepository
{
    public async Task<List<CouponRestriction>> GetByCouponIdAsync(string couponId, CancellationToken ct = default)
        => await context.CouponRestrictions
            .AsNoTracking()
            .Where(r => r.CouponId == couponId)
            .ToListAsync(ct);

    public async Task AddAsync(CouponRestriction restriction, CancellationToken ct = default)
        => await context.CouponRestrictions.AddAsync(restriction, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
