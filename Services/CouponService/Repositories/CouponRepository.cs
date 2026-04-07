using CouponService.DTOs.Requests;
using CouponService.Infrastructure.Persistence;
using CouponService.Models;
using CouponService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CouponService.Repositories;

public class CouponRepository(AppDbContext context) : ICouponRepository
{
    public async Task<Coupon?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.Coupons
            .Include(c => c.Restrictions)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Coupon?> FindByCodeAsync(string code, CancellationToken ct = default)
        => await context.Coupons
            .Include(c => c.Restrictions)
            .FirstOrDefaultAsync(c => c.Code == code, ct);

    public async Task<bool> ExistsAsync(string code, CancellationToken ct = default)
        => await context.Coupons.AsNoTracking().AnyAsync(c => c.Code == code, ct);

    public async Task<List<Coupon>> GetAvailableCouponsAsync(DateTimeOffset now, int page, int pageSize, CancellationToken ct = default)
        => await context.Coupons
            .AsNoTracking()
            .Where(c => c.IsActive && c.ValidFrom <= now && c.ValidUntil >= now && c.CurrentUsageCount < c.MaxUsageCount)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    public async Task<int> CountAvailableCouponsAsync(DateTimeOffset now, CancellationToken ct = default)
        => await context.Coupons
            .AsNoTracking()
            .CountAsync(c => c.IsActive && c.ValidFrom <= now && c.ValidUntil >= now && c.CurrentUsageCount < c.MaxUsageCount, ct);

    public async Task AddAsync(Coupon coupon, CancellationToken ct = default)
        => await context.Coupons.AddAsync(coupon, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);

    public async Task<List<Coupon>> GetExpiredActiveCouponsAsync(DateTimeOffset now, CancellationToken ct = default)
        => await context.Coupons
            .AsNoTracking()
            .Where(c => c.IsActive && c.ValidUntil < now)
            .ToListAsync(ct);

    public async Task<int> DeactivateExpiredCouponsAsync(DateTimeOffset now, CancellationToken ct = default)
        => await context.Coupons
            .Where(c => c.IsActive && c.ValidUntil < now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.IsActive, false)
                .SetProperty(c => c.UpdatedAt, now), ct);

    public async Task<int> CountAllAsync(CancellationToken ct = default)
        => await context.Coupons.AsNoTracking().CountAsync(ct);

    public async Task<List<Coupon>> GetTopByUsageAsync(int topN, CancellationToken ct = default)
        => await context.Coupons
            .AsNoTracking()
            .OrderByDescending(c => c.CurrentUsageCount)
            .Take(topN)
            .ToListAsync(ct);

    public async Task<List<Coupon>> GetAllCouponsAsync(CouponQueryParams query, CancellationToken ct = default)
        => await context.Coupons
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

    public async Task<int> CountAllCouponsAsync(CancellationToken ct = default)
        => await context.Coupons.AsNoTracking().CountAsync(ct);

    public async Task<HashSet<string>> FindExistingCodesAsync(
        IEnumerable<string> codes, CancellationToken ct = default)
    {
        var codeList = codes.ToList();
        var existing = await context.Coupons
            .AsNoTracking()
            .Where(c => codeList.Contains(c.Code))
            .Select(c => c.Code)
            .ToListAsync(ct);
        return existing.ToHashSet();
    }
}
