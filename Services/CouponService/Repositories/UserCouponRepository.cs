using CouponService.Infrastructure.Persistence;
using CouponService.Models;
using CouponService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CouponService.Repositories;

public class UserCouponRepository(AppDbContext context) : IUserCouponRepository
{
    public async Task<UserCoupon?> FindByUserAndCouponAsync(string userId, string couponId, CancellationToken ct = default)
        => await context.UserCoupons.FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CouponId == couponId, ct);

    public async Task<List<UserCoupon>> GetByUserIdAsync(
        string userId, int page = 1, int pageSize = 50, CancellationToken ct = default)
        => await context.UserCoupons
            .AsNoTracking()
            .Include(uc => uc.Coupon)
            .Where(uc => uc.UserId == userId)
            .OrderByDescending(uc => uc.AcquiredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    public async Task AddAsync(UserCoupon userCoupon, CancellationToken ct = default)
        => await context.UserCoupons.AddAsync(userCoupon, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);

    public async Task<int> ExpireByDateAsync(DateTimeOffset now, CancellationToken ct = default)
        => await context.UserCoupons
            .Where(uc => uc.Status == UserCouponStatus.Available && uc.Coupon != null && uc.Coupon.ValidUntil < now)
            .ExecuteUpdateAsync(s => s.SetProperty(uc => uc.Status, UserCouponStatus.Expired), ct);
}
