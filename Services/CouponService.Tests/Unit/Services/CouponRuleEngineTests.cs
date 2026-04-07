using CouponService.DTOs.Responses;
using CouponService.Models;
using CouponService.Repositories.Interfaces;
using CouponService.Services;
using CouponService.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CouponService.Tests.Unit.Services;

[Trait("Category", "Unit")]
public class CouponRuleEngineTests
{
    private readonly ICouponRepository _couponRepository;
    private readonly ICouponUsageRepository _couponUsageRepository;
    private readonly IFraudDetectionService _fraudDetectionService;
    private readonly ICouponCacheService _cacheService;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ICouponRuleEngine _ruleEngine;

    public CouponRuleEngineTests()
    {
        _couponRepository = Substitute.For<ICouponRepository>();
        _couponUsageRepository = Substitute.For<ICouponUsageRepository>();
        _fraudDetectionService = Substitute.For<IFraudDetectionService>();
        _cacheService = Substitute.For<ICouponCacheService>();
        _timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
        var logger = Substitute.For<ILogger<CouponRuleEngine>>();

        _ruleEngine = new CouponRuleEngine(
            _couponRepository, _couponUsageRepository,
            _fraudDetectionService, _cacheService, _timeProvider, logger);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_CouponDoesNotExist()
    {
        // Arrange
        _cacheService.GetCouponAsync("MISSING", default).Returns((Coupon?)null);
        _couponRepository.FindByCodeAsync("MISSING", default).Returns((Coupon?)null);

        // Act
        var result = await _ruleEngine.ValidateAsync("MISSING", "user-1", 1000m, null);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorCode.ShouldBe("CPN-4001");
    }

    [Fact]
    public async Task Should_ReturnInvalid_When_CouponIsInactive()
    {
        // Arrange
        var coupon = CreateCoupon(isActive: false);
        _cacheService.GetCouponAsync("TEST", default).Returns(coupon);

        // Act
        var result = await _ruleEngine.ValidateAsync("TEST", "user-1", 1000m, null);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorCode.ShouldBe("CPN-4010");
    }

    [Fact]
    public async Task Should_ReturnInvalid_When_CouponIsExpired()
    {
        // Arrange
        var coupon = CreateCoupon(
            validFrom: DateTimeOffset.UtcNow.AddDays(-30),
            validTo: DateTimeOffset.UtcNow.AddDays(-1));
        _cacheService.GetCouponAsync("EXPIRED", default).Returns(coupon);

        // Act
        var result = await _ruleEngine.ValidateAsync("EXPIRED", "user-1", 1000m, null);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorCode.ShouldBe("CPN-4002");
    }

    [Fact]
    public async Task Should_ReturnInvalid_When_GlobalUsageLimitReached()
    {
        // Arrange
        var coupon = CreateCoupon(maxUsageCount: 100, currentUsageCount: 100);
        _cacheService.GetCouponAsync("MAXED", default).Returns(coupon);

        // Act
        var result = await _ruleEngine.ValidateAsync("MAXED", "user-1", 1000m, null);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorCode.ShouldBe("CPN-4003");
    }

    [Fact]
    public async Task Should_ReturnInvalid_When_UserUsageLimitReached()
    {
        // Arrange
        var coupon = CreateCoupon(maxUsagePerUser: 1);
        _cacheService.GetCouponAsync("ONCE", default).Returns(coupon);
        _cacheService.GetUserUsageCountAsync(coupon.Id, "user-1", default).Returns((int?)null);
        _couponUsageRepository.CountByUserAndCouponAsync(coupon.Id, "user-1", default).Returns(1);

        // Act
        var result = await _ruleEngine.ValidateAsync("ONCE", "user-1", 1000m, null);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorCode.ShouldBe("CPN-4004");
    }

    [Fact]
    public async Task Should_ReturnInvalid_When_OrderAmountBelowMinimum()
    {
        // Arrange
        var coupon = CreateCoupon(minOrderAmount: 5000m);
        _cacheService.GetCouponAsync("BIG", default).Returns(coupon);
        _cacheService.GetUserUsageCountAsync(coupon.Id, "user-1", default).Returns(0);

        // Act
        var result = await _ruleEngine.ValidateAsync("BIG", "user-1", 3000m, null);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorCode.ShouldBe("CPN-4005");
    }

    [Fact]
    public async Task Should_ReturnInvalid_When_FraudDetected()
    {
        // Arrange
        var coupon = CreateCoupon();
        _cacheService.GetCouponAsync("FRAUD", default).Returns(coupon);
        _cacheService.GetUserUsageCountAsync(coupon.Id, "user-1", default).Returns(0);
        _fraudDetectionService.CheckAsync("user-1", coupon, default)
            .Returns(FraudCheckResult.Suspicious("高頻度利用"));

        // Act
        var result = await _ruleEngine.ValidateAsync("FRAUD", "user-1", 10000m, null);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorCode.ShouldBe("CPN-4007");
    }

    [Fact]
    public async Task Should_ReturnValid_When_AllRulesPass_PercentageDiscount()
    {
        // Arrange
        var coupon = CreateCoupon(discountType: DiscountType.Percentage, discountValue: 10m);
        _cacheService.GetCouponAsync("PCT10", default).Returns(coupon);
        _cacheService.GetUserUsageCountAsync(coupon.Id, "user-1", default).Returns(0);
        _fraudDetectionService.CheckAsync("user-1", coupon, default)
            .Returns(FraudCheckResult.Clear);

        // Act
        var result = await _ruleEngine.ValidateAsync("PCT10", "user-1", 10000m, null);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.DiscountAmount.ShouldBe(1000m);
    }

    [Fact]
    public async Task Should_ReturnValid_When_AllRulesPass_FixedDiscount()
    {
        // Arrange
        var coupon = CreateCoupon(discountType: DiscountType.FixedAmount, discountValue: 500m);
        _cacheService.GetCouponAsync("FIX500", default).Returns(coupon);
        _cacheService.GetUserUsageCountAsync(coupon.Id, "user-1", default).Returns(0);
        _fraudDetectionService.CheckAsync("user-1", coupon, default)
            .Returns(FraudCheckResult.Clear);

        // Act
        var result = await _ruleEngine.ValidateAsync("FIX500", "user-1", 10000m, null);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.DiscountAmount.ShouldBe(500m);
    }

    private Coupon CreateCoupon(
        bool isActive = true,
        DateTimeOffset? validFrom = null,
        DateTimeOffset? validTo = null,
        int maxUsageCount = 1000,
        int currentUsageCount = 0,
        int maxUsagePerUser = 10,
        decimal minOrderAmount = 0m,
        DiscountType discountType = DiscountType.FixedAmount,
        decimal discountValue = 500m) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Code = "TEST",
        IsActive = isActive,
        ValidFrom = validFrom ?? _timeProvider.GetUtcNow().AddDays(-7),
        ValidUntil = validTo ?? _timeProvider.GetUtcNow().AddDays(30),
        MaxUsageCount = maxUsageCount,
        CurrentUsageCount = currentUsageCount,
        MaxUsagePerUser = maxUsagePerUser,
        MinOrderAmount = minOrderAmount,
        DiscountType = discountType,
        DiscountValue = discountValue
    };
}
