using CouponService.DTOs.Responses;
using CouponService.Models;
using CouponService.Repositories.Interfaces;
using CouponService.Services.Interfaces;

namespace CouponService.Services;

public class CouponAnalyticsService(
    ICouponRepository couponRepository,
    ICouponUsageRepository couponUsageRepository,
    ILogger<CouponAnalyticsService> logger) : ICouponAnalyticsService
{
    public async Task<CouponAnalyticsResponse> GetAnalyticsAsync(
        string couponId, CancellationToken ct = default)
    {
        var coupon = await couponRepository.FindByIdAsync(couponId, ct);
        if (coupon is null)
            return new CouponAnalyticsResponse(couponId, string.Empty, 0, 0m, 0);

        var totalUsage = coupon.CurrentUsageCount;
        var totalDiscountAmount = await couponUsageRepository.GetTotalDiscountAmountAsync(couponId, ct);
        var uniqueUserCount = await couponUsageRepository.GetUniqueUserCountAsync(couponId, ct);

        logger.LogInformation("クーポン分析: {CouponCode}, 利用回数={TotalUsage}", coupon.Code, totalUsage);

        return new CouponAnalyticsResponse(
            coupon.Id, coupon.Code, totalUsage, totalDiscountAmount, uniqueUserCount);
    }

    public async Task<CouponAnalyticsResponse> GetOverallAnalyticsAsync(CancellationToken ct = default)
    {
        var totalUsageCount = await couponUsageRepository.CountAllAsync(ct);
        var totalDiscountAmount = await couponUsageRepository.GetTotalDiscountAmountAllAsync(ct);
        var uniqueUserCount = await couponUsageRepository.GetAllUniqueUserCountAsync(ct);

        logger.LogInformation("全体分析: 総利用回数={TotalUsage}", totalUsageCount);

        return new CouponAnalyticsResponse(
            string.Empty, "ALL", totalUsageCount, totalDiscountAmount, uniqueUserCount);
    }

    public async Task<double> GetRedemptionRateAsync(string? campaignId = null, CancellationToken ct = default)
    {
        var totalCoupons = await couponRepository.CountAllAsync(ct);
        if (totalCoupons == 0)
            return 0.0;

        var usedCount = await couponUsageRepository.CountAllAsync(ct);
        var rate = (double)usedCount / totalCoupons;

        logger.LogInformation("利用率算出: CampaignId={CampaignId}, Rate={Rate:P2}", campaignId ?? "ALL", rate);
        return rate;
    }

    public async Task<List<CouponResponse>> GetTopCouponsAsync(int topN = 10, CancellationToken ct = default)
    {
        topN = Math.Min(topN, 100);
        var topCoupons = await couponRepository.GetTopByUsageAsync(topN, ct);
        return topCoupons.Select(c => new CouponResponse(
            c.Id, c.Code, c.CouponTypeId, c.CampaignId,
            (int)c.DiscountType, c.DiscountValue, c.MaxDiscountAmount,
            c.MinOrderAmount, c.MaxUsageCount, c.CurrentUsageCount,
            c.MaxUsagePerUser, c.ValidFrom, c.ValidUntil, c.IsActive, c.CreatedAt)).ToList();
    }

    public async Task<List<CouponUsage>> GetUsagesByDateRangeAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        logger.LogInformation("利用履歴取得: From={From}, To={To}", from, to);
        return await couponUsageRepository.GetByDateRangeAsync(from, to, page: 1, pageSize: 100, ct);
    }
}
