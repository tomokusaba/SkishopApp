using CouponService.DTOs.Requests;
using CouponService.Models;

namespace CouponService.Repositories.Interfaces;

public interface ICouponRepository
{
    Task<Coupon?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<Coupon?> FindByCodeAsync(string code, CancellationToken ct = default);
    Task<bool> ExistsAsync(string code, CancellationToken ct = default);
    Task<List<Coupon>> GetAvailableCouponsAsync(DateTimeOffset now, int page, int pageSize, CancellationToken ct = default);
    Task<int> CountAvailableCouponsAsync(DateTimeOffset now, CancellationToken ct = default);
    Task AddAsync(Coupon coupon, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task<List<Coupon>> GetExpiredActiveCouponsAsync(DateTimeOffset now, CancellationToken ct = default);
    Task<int> DeactivateExpiredCouponsAsync(DateTimeOffset now, CancellationToken ct = default);
    Task<int> CountAllAsync(CancellationToken ct = default);
    Task<List<Coupon>> GetTopByUsageAsync(int topN, CancellationToken ct = default);
    Task<List<Coupon>> GetAllCouponsAsync(CouponQueryParams query, CancellationToken ct = default);
    Task<int> CountAllCouponsAsync(CancellationToken ct = default);
    Task<HashSet<string>> FindExistingCodesAsync(IEnumerable<string> codes, CancellationToken ct = default);
}

public interface ICampaignRepository
{
    Task<Campaign?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<List<Campaign>> GetAllAsync(int page, int pageSize, CancellationToken ct = default);
    Task<int> CountAllAsync(CancellationToken ct = default);
    Task AddAsync(Campaign campaign, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task<List<Campaign>> GetActiveCampaignsAsync(CancellationToken ct = default);
    Task<List<Campaign>> GetCampaignsToEndAsync(DateTimeOffset now, CancellationToken ct = default);
    Task<List<Campaign>> GetCampaignsToStartAsync(DateTimeOffset now, CancellationToken ct = default);
    Task<int> CompleteExpiredCampaignsAsync(DateTimeOffset now, CancellationToken ct = default);
    Task<List<Campaign>> GetByStatusAsync(CampaignStatus status, int page = 1, int pageSize = 50, CancellationToken ct = default);
}

public interface ICouponTypeRepository
{
    Task<CouponType?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<List<CouponType>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(CouponType couponType, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IUserCouponRepository
{
    Task<UserCoupon?> FindByUserAndCouponAsync(string userId, string couponId, CancellationToken ct = default);
    Task<List<UserCoupon>> GetByUserIdAsync(string userId, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task AddAsync(UserCoupon userCoupon, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task<int> ExpireByDateAsync(DateTimeOffset now, CancellationToken ct = default);
}

public interface ICouponUsageRepository
{
    Task<int> CountByUserAndCouponAsync(string couponId, string userId, CancellationToken ct = default);
    Task<List<CouponUsage>> GetByCouponIdAsync(string couponId, int page, int pageSize, CancellationToken ct = default);
    Task<int> CountByCouponIdAsync(string couponId, CancellationToken ct = default);
    Task<CouponUsage?> FindByOrderIdAsync(string couponId, string orderId, CancellationToken ct = default);
    Task<int> CountRecentByUserAsync(string userId, DateTimeOffset since, CancellationToken ct = default);
    Task<CouponUsage?> FindByUserAndCouponAsync(string userId, string couponId, CancellationToken ct = default);
    Task AddAsync(CouponUsage usage, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task<int> GetUniqueUserCountAsync(string couponId, CancellationToken ct = default);
    Task<decimal> GetTotalDiscountAmountAsync(string couponId, CancellationToken ct = default);
    Task<int> CountAllAsync(CancellationToken ct = default);
    Task<decimal> GetTotalDiscountAmountAllAsync(CancellationToken ct = default);
    Task<int> GetAllUniqueUserCountAsync(CancellationToken ct = default);
    Task<List<CouponUsage>> GetByDateRangeAsync(DateTimeOffset from, DateTimeOffset to, int page = 1, int pageSize = 100, CancellationToken ct = default);
}

public interface IOutboxEventRepository
{
    Task<List<OutboxEvent>> GetPendingEventsAsync(int batchSize, CancellationToken ct = default);
    Task AddAsync(OutboxEvent outboxEvent, CancellationToken ct = default);
    Task MarkAsPublishedAsync(string eventId, CancellationToken ct = default);
    Task MarkAsFailedAsync(string eventId, CancellationToken ct = default);
    Task<int> MoveToDeadLetterAsync(int maxRetryCount, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task<bool> TryAcquireAdvisoryLockAsync(long lockId, CancellationToken ct = default);
    Task ReleaseAdvisoryLockAsync(long lockId, CancellationToken ct = default);
    Task<List<OutboxEvent>> GetPendingAndMarkProcessingAsync(int batchSize, CancellationToken ct = default);
    Task UpdateBatchAsync(CancellationToken ct = default);
    Task RecoverProcessingEventsAsync(CancellationToken ct = default);
}

public interface ICouponRestrictionRepository
{
    Task<List<CouponRestriction>> GetByCouponIdAsync(string couponId, CancellationToken ct = default);
    Task AddAsync(CouponRestriction restriction, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
