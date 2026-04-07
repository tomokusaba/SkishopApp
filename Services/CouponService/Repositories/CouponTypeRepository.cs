using CouponService.Infrastructure.Persistence;
using CouponService.Models;
using CouponService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CouponService.Repositories;

public class CouponTypeRepository(AppDbContext context) : ICouponTypeRepository
{
    public async Task<CouponType?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.CouponTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<List<CouponType>> GetAllAsync(CancellationToken ct = default)
        => await context.CouponTypes.AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(CouponType couponType, CancellationToken ct = default)
        => await context.CouponTypes.AddAsync(couponType, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
