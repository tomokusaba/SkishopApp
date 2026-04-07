using CouponService.Configurations;
using CouponService.DTOs.Responses;
using CouponService.Models;
using CouponService.Repositories.Interfaces;
using CouponService.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace CouponService.Services;

public class FraudDetectionService(
    ICouponUsageRepository usageRepository,
    TimeProvider timeProvider,
    IOptions<CouponSettings> settings,
    ILogger<FraudDetectionService> logger) : IFraudDetectionService
{
    public async Task<FraudCheckResult> CheckAsync(
        string userId, Coupon coupon, CancellationToken ct = default)
    {
        var config = settings.Value.FraudDetection;
        var since = timeProvider.GetUtcNow().AddMinutes(-config.WindowMinutes);
        var recentUsages = await usageRepository.CountRecentByUserAsync(userId, since, ct);

        if (recentUsages >= config.MaxUsagesInWindow)
        {
            logger.LogWarning(
                "不正利用の疑い: UserId={UserId}, 直近{Window}分の利用回数={Count}",
                userId, config.WindowMinutes, recentUsages);
            return FraudCheckResult.Suspicious(
                $"直近{config.WindowMinutes}分間に{recentUsages}回のクーポン利用を検出");
        }

        return FraudCheckResult.Clear;
    }

    public async Task<bool> IsSuspiciousAsync(string userId, CancellationToken ct = default)
    {
        var config = settings.Value.FraudDetection;
        var since = timeProvider.GetUtcNow().AddMinutes(-config.WindowMinutes);
        var recentUsages = await usageRepository.CountRecentByUserAsync(userId, since, ct);
        return recentUsages >= config.MaxUsagesInWindow;
    }

    public async Task<bool> CheckFraudAsync(
        string userId, string couponCode, decimal orderAmount, CancellationToken ct = default)
    {
        var isSuspicious = await IsSuspiciousAsync(userId, ct);
        if (isSuspicious)
        {
            logger.LogWarning(
                "不正利用検出: UserId={UserId}, CouponCode={CouponCode}, OrderAmount={OrderAmount}",
                userId, couponCode, orderAmount);
        }
        return isSuspicious;
    }
}
