using CouponService.DTOs.Responses;
using CouponService.Exceptions;
using CouponService.Infrastructure.Observability;
using CouponService.Models;
using CouponService.Repositories.Interfaces;
using CouponService.Services.Interfaces;
using CouponService.Configurations;
using System.Diagnostics;

namespace CouponService.Services;

public class CouponRuleEngine(
    ICouponRepository couponRepository,
    ICouponUsageRepository couponUsageRepository,
    IFraudDetectionService fraudDetectionService,
    ICouponCacheService cacheService,
    TimeProvider timeProvider,
    ILogger<CouponRuleEngine> logger) : ICouponRuleEngine
{
    public async Task<ValidationResult> ValidateAsync(
        string couponCode, string userId, decimal orderAmount,
        string? categoryId, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        CouponMetrics.ValidationCount.Add(1);

        // Try cache first, fallback to DB
        var coupon = await cacheService.GetCouponAsync(couponCode, ct);
        if (coupon is not null)
        {
            CouponMetrics.CacheHitCount.Add(1);
        }
        else
        {
            CouponMetrics.CacheMissCount.Add(1);
            coupon = await couponRepository.FindByCodeAsync(couponCode, ct);
        }

        if (coupon is null)
            return new ValidationResult(false, "CPN-4001", "クーポンが見つかりません", 0m);

        // Cache the coupon for future lookups
        await cacheService.SetCouponAsync(couponCode, coupon, ct);

        // Step 1: アクティブチェック
        if (!coupon.IsActive)
            return new ValidationResult(false, "CPN-4010", "クーポンが無効です", 0m);

        // Step 2: 有効期間チェック
        var now = timeProvider.GetUtcNow();
        if (now < coupon.ValidFrom || now > coupon.ValidUntil)
            return new ValidationResult(false, "CPN-4002", "クーポンの有効期間外です", 0m);

        // Step 3: 全体利用上限チェック
        if (coupon.CurrentUsageCount >= coupon.MaxUsageCount)
            return new ValidationResult(false, "CPN-4003", "クーポンの利用上限に達しました", 0m);

        // Step 4: ユーザー利用回数チェック
        var cachedCount = await cacheService.GetUserUsageCountAsync(coupon.Id, userId, ct);
        var userUsageCount = cachedCount ?? await couponUsageRepository.CountByUserAndCouponAsync(coupon.Id, userId, ct);
        if (cachedCount is null)
            await cacheService.SetUserUsageCountAsync(coupon.Id, userId, userUsageCount, ct);

        if (userUsageCount >= coupon.MaxUsagePerUser)
            return new ValidationResult(false, "CPN-4004", "このクーポンは既に利用済みです", 0m);

        // Step 5: 最低注文金額チェック
        if (orderAmount < coupon.MinOrderAmount)
            return new ValidationResult(false, "CPN-4005",
                $"最低注文金額 {coupon.MinOrderAmount:N0} 円以上から適用可能です", 0m);

        // Step 6: カテゴリチェック (via restrictions)
        if (categoryId is not null)
        {
            var restrictions = coupon.Restrictions.ToList();
            var categoryRestrictions = restrictions
                .Where(r => r.RestrictionType == "CATEGORY")
                .ToList();

            if (categoryRestrictions.Count > 0 &&
                !categoryRestrictions.Any(r => r.RestrictionValue == categoryId))
            {
                return new ValidationResult(false, "CPN-4006",
                    "対象カテゴリの商品が含まれていません", 0m);
            }
        }

        // Fraud detection
        var fraudResult = await fraudDetectionService.CheckAsync(userId, coupon, ct);
        if (fraudResult.IsSuspicious)
        {
            CouponMetrics.FraudDetectionCount.Add(1);
            return new ValidationResult(false, "CPN-4007",
                $"不正利用の疑い: {fraudResult.Reason}", 0m);
        }

        // Step 7: 割引額計算
        var discount = CalculateDiscount(coupon, orderAmount);

        stopwatch.Stop();
        CouponMetrics.RuleEngineLatency.Record(stopwatch.Elapsed.TotalMilliseconds);
        CouponMetrics.AppliedCount.Add(1);
        CouponMetrics.DiscountAmount.Record((double)discount);

        logger.LogInformation(
            "クーポン検証成功: {CouponCode}, 割引額: {Discount}", coupon.Code, discount);

        return new ValidationResult(true, null, null, discount,
            coupon.Id, (int)coupon.DiscountType, coupon.DiscountValue);
    }

    internal static decimal CalculateDiscount(Coupon coupon, decimal orderAmount)
    {
        var discount = coupon.DiscountType switch
        {
            DiscountType.Percentage => orderAmount * coupon.DiscountValue / 100m,
            DiscountType.FixedAmount => coupon.DiscountValue,
            DiscountType.FreeShipping => 0m,
            _ => 0m
        };

        if (coupon.MaxDiscountAmount.HasValue && discount > coupon.MaxDiscountAmount.Value)
            discount = coupon.MaxDiscountAmount.Value;

        return Math.Min(discount, orderAmount);
    }

    public async Task<ValidationResult> EvaluateRulesAsync(
        Coupon coupon, DTOs.Requests.ValidateCouponRequest request,
        List<CouponRestriction> restrictions, CancellationToken ct = default)
    {
        // Active check
        if (!coupon.IsActive)
            return new ValidationResult(false, "CPN-4010", "クーポンが無効です", 0m);

        // Period check
        var now = timeProvider.GetUtcNow();
        if (now < coupon.ValidFrom || now > coupon.ValidUntil)
            return new ValidationResult(false, "CPN-4002", "クーポンの有効期間外です", 0m);

        // Global usage limit
        if (coupon.CurrentUsageCount >= coupon.MaxUsageCount)
            return new ValidationResult(false, "CPN-4003", "クーポンの利用上限に達しました", 0m);

        // Min order amount
        if (request.OrderAmount < coupon.MinOrderAmount)
            return new ValidationResult(false, "CPN-4005",
                $"最低注文金額 {coupon.MinOrderAmount:N0} 円以上から適用可能です", 0m);

        // Category restrictions
        if (request.CategoryId is not null)
        {
            var categoryRestrictions = restrictions
                .Where(r => r.RestrictionType == "CATEGORY")
                .ToList();

            if (categoryRestrictions.Count > 0 &&
                !categoryRestrictions.Any(r => r.RestrictionValue == request.CategoryId))
            {
                return new ValidationResult(false, "CPN-4006",
                    "対象カテゴリの商品が含まれていません", 0m);
            }
        }

        var discount = CalculateDiscount(coupon, request.OrderAmount);

        logger.LogInformation(
            "ルール評価成功: CouponId={CouponId}, 割引額: {Discount}", coupon.Id, discount);

        return await Task.FromResult(new ValidationResult(true, null, null, discount));
    }
}
