using CouponService.Infrastructure.Persistence;
using CouponService.Models;
using CouponService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CouponService.Repositories;

public class CouponUsageRepository(AppDbContext context) : ICouponUsageRepository
{
    public async Task<int> CountByUserAndCouponAsync(string couponId, string userId, CancellationToken ct = default)
        => await context.CouponUsages.AsNoTracking().CountAsync(u => u.CouponId == couponId && u.UserId == userId, ct);

    public async Task<List<CouponUsage>> GetByCouponIdAsync(string couponId, int page, int pageSize, CancellationToken ct = default)
        => await context.CouponUsages
            .AsNoTracking()
            .Where(u => u.CouponId == couponId)
            .OrderByDescending(u => u.UsedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    public async Task<int> CountByCouponIdAsync(string couponId, CancellationToken ct = default)
        => await context.CouponUsages.AsNoTracking().CountAsync(u => u.CouponId == couponId, ct);

    public async Task<CouponUsage?> FindByOrderIdAsync(string couponId, string orderId, CancellationToken ct = default)
        => await context.CouponUsages.FirstOrDefaultAsync(u => u.CouponId == couponId && u.OrderId == orderId, ct);

    public async Task<int> CountRecentByUserAsync(string userId, DateTimeOffset since, CancellationToken ct = default)
        => await context.CouponUsages.AsNoTracking().CountAsync(u => u.UserId == userId && u.UsedAt >= since, ct);

    public async Task AddAsync(CouponUsage usage, CancellationToken ct = default)
        => await context.CouponUsages.AddAsync(usage, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);

    public async Task<int> GetUniqueUserCountAsync(string couponId, CancellationToken ct = default)
        => await context.CouponUsages.AsNoTracking().Where(u => u.CouponId == couponId).Select(u => u.UserId).Distinct().CountAsync(ct);

    public async Task<decimal> GetTotalDiscountAmountAsync(string couponId, CancellationToken ct = default)
        => await context.CouponUsages.AsNoTracking().Where(u => u.CouponId == couponId).SumAsync(u => u.DiscountAmount, ct);

    public async Task<int> CountAllAsync(CancellationToken ct = default)
        => await context.CouponUsages.AsNoTracking().CountAsync(ct);

    public async Task<decimal> GetTotalDiscountAmountAllAsync(CancellationToken ct = default)
        => await context.CouponUsages.AsNoTracking().SumAsync(u => u.DiscountAmount, ct);

    public async Task<int> GetAllUniqueUserCountAsync(CancellationToken ct = default)
        => await context.CouponUsages.AsNoTracking().Select(u => u.UserId).Distinct().CountAsync(ct);

    public async Task<List<CouponUsage>> GetByDateRangeAsync(
        DateTimeOffset from, DateTimeOffset to, int page = 1, int pageSize = 100, CancellationToken ct = default)
        => await context.CouponUsages
            .AsNoTracking()
            .Where(u => u.UsedAt >= from && u.UsedAt <= to)
            .OrderByDescending(u => u.UsedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    public async Task<CouponUsage?> FindByUserAndCouponAsync(string userId, string couponId, CancellationToken ct = default)
        => await context.CouponUsages.FirstOrDefaultAsync(u => u.UserId == userId && u.CouponId == couponId, ct);
}
