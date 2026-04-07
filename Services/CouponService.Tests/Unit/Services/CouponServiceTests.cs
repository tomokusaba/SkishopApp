using CouponService.DTOs.Requests;
using CouponService.DTOs.Responses;
using CouponService.Exceptions;
using CouponService.Models;
using CouponService.Repositories.Interfaces;
using CouponService.Services;
using CouponService.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace CouponService.Tests.Unit.Services;

[Trait("Category", "Unit")]
public class CouponServiceTests
{
    private readonly ICouponRepository _couponRepository;
    private readonly IUserCouponRepository _userCouponRepository;
    private readonly ICouponUsageRepository _couponUsageRepository;
    private readonly IOutboxEventRepository _outboxEventRepository;
    private readonly ICouponRuleEngine _ruleEngine;
    private readonly ICouponCacheService _cacheService;
    private readonly FakeTimeProvider _timeProvider;
    private readonly CouponAppService _service;

    public CouponServiceTests()
    {
        _couponRepository = Substitute.For<ICouponRepository>();
        _userCouponRepository = Substitute.For<IUserCouponRepository>();
        _couponUsageRepository = Substitute.For<ICouponUsageRepository>();
        _outboxEventRepository = Substitute.For<IOutboxEventRepository>();
        _ruleEngine = Substitute.For<ICouponRuleEngine>();
        _cacheService = Substitute.For<ICouponCacheService>();
        _timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
        var logger = Substitute.For<ILogger<CouponAppService>>();

        _service = new CouponAppService(
            _couponRepository, _userCouponRepository,
            _couponUsageRepository, _outboxEventRepository,
            _ruleEngine, _cacheService, _timeProvider, logger);
    }

    [Fact]
    public async Task Should_ReturnAvailableCoupons_When_CouponsExist()
    {
        // Arrange
        var coupons = new List<Coupon>
        {
            new()
            {
                Id = "c1", Code = "SAVE10", DiscountType = DiscountType.Percentage,
                DiscountValue = 10m, ValidFrom = DateTimeOffset.UtcNow.AddDays(-1),
                ValidUntil = DateTimeOffset.UtcNow.AddDays(30), IsActive = true
            }
        };
        _couponRepository.GetAvailableCouponsAsync(Arg.Any<DateTimeOffset>(), 1, 20, default)
            .Returns(coupons);
        _couponRepository.CountAvailableCouponsAsync(Arg.Any<DateTimeOffset>(), default)
            .Returns(1);

        // Act
        var result = await _service.GetAvailableCouponsAsync(1, 20);

        // Assert
        result.TotalCount.ShouldBe(1);
        result.Items.Count().ShouldBe(1);
    }

    [Fact]
    public async Task Should_AcquireCoupon_When_ValidRequest()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow();
        var coupon = new Coupon
        {
            Id = "c1", Code = "FREE500", IsActive = true,
            ValidFrom = now.AddDays(-1),
            ValidUntil = now.AddDays(30)
        };
        _couponRepository.FindByCodeAsync("FREE500", default).Returns(coupon);
        _userCouponRepository.FindByUserAndCouponAsync("user-1", "c1", default).Returns((UserCoupon?)null);

        // Act
        var result = await _service.AcquireCouponAsync("FREE500", "user-1");

        // Assert
        result.ShouldNotBeNull();
        result.CouponCode.ShouldBe("FREE500");
        await _userCouponRepository.Received(1).AddAsync(Arg.Any<UserCoupon>(), default);
    }

    [Fact]
    public async Task Should_ThrowCouponAlreadyAcquired_When_DuplicateAcquire()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow();
        var coupon = new Coupon
        {
            Id = "c1", Code = "ONCE", IsActive = true,
            ValidFrom = now.AddDays(-1),
            ValidUntil = now.AddDays(30)
        };
        _couponRepository.FindByCodeAsync("ONCE", default).Returns(coupon);
        _userCouponRepository.FindByUserAndCouponAsync("user-1", "c1", default)
            .Returns(new UserCoupon { UserId = "user-1", CouponId = "c1" });

        // Act & Assert
        var act = async () => await _service.AcquireCouponAsync("ONCE", "user-1");
        var ex = await Should.ThrowAsync<CouponAlreadyAcquiredException>(act);
        ex.Message.ShouldContain("取得済み");
    }

    [Fact]
    public async Task Should_ThrowCouponNotFound_When_AcquireNonExistentCoupon()
    {
        // Arrange
        _couponRepository.FindByCodeAsync("MISSING", default).Returns((Coupon?)null);

        // Act & Assert
        var act = async () => await _service.AcquireCouponAsync("MISSING", "user-1");
        await Should.ThrowAsync<CouponNotFoundException>(act);
    }

    [Fact]
    public async Task Should_CalculateDiscount_When_ValidationPasses()
    {
        // Arrange
        var request = new CalculateDiscountRequest("CODE1", "user-1", 10000m, null);
        _ruleEngine.ValidateAsync("CODE1", "user-1", 10000m, null, default)
            .Returns(new ValidationResult(true, null, null, 1000m));

        // Act
        var result = await _service.CalculateDiscountAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.DiscountAmount.ShouldBe(1000m);
    }

    [Fact]
    public async Task Should_ReturnInvalid_When_CalculateDiscountFails()
    {
        // Arrange
        var request = new CalculateDiscountRequest("BAD", "user-1", 100m, null);
        _ruleEngine.ValidateAsync("BAD", "user-1", 100m, null, default)
            .Returns(new ValidationResult(false, "CPN-4005", "最低注文金額未満", 0m));

        // Act
        var result = await _service.CalculateDiscountAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ErrorCode.ShouldBe("CPN-4005");
    }

    [Fact]
    public async Task Should_RedeemCoupon_When_ValidRequest()
    {
        // Arrange
        var coupon = new Coupon
        {
            Id = "c1", Code = "REDEEM10", CurrentUsageCount = 0,
            IsActive = true, MaxUsageCount = 100,
            DiscountType = DiscountType.FixedAmount, DiscountValue = 500m,
            ValidFrom = _timeProvider.GetUtcNow().AddDays(-1),
            ValidUntil = _timeProvider.GetUtcNow().AddDays(30)
        };
        _couponRepository.FindByIdAsync("c1", default).Returns(coupon);
        _couponUsageRepository.FindByOrderIdAsync("c1", "order-1", default).Returns((CouponUsage?)null);
        var request = new RedeemCouponRequest("c1", "order-1", 500m, "user-1");

        // Act
        var result = await _service.RedeemCouponAsync("user-1", request);

        // Assert
        result.ShouldNotBeNull();
        result.OrderId.ShouldBe("order-1");
        result.DiscountAmount.ShouldBe(500m);
        await _couponUsageRepository.Received(1).AddAsync(Arg.Any<CouponUsage>(), default);
        coupon.CurrentUsageCount.ShouldBe(1);
    }

    [Fact]
    public async Task Should_ReturnExistingUsage_When_RedeemIdempotent()
    {
        // Arrange
        var coupon = new Coupon { Id = "c1", Code = "REDEEM10", CurrentUsageCount = 1 };
        _couponRepository.FindByIdAsync("c1", default).Returns(coupon);
        var existingUsage = new CouponUsage
        {
            Id = "u1", CouponId = "c1", UserId = "user-1",
            OrderId = "order-1", DiscountAmount = 500m
        };
        _couponUsageRepository.FindByOrderIdAsync("c1", "order-1", default).Returns(existingUsage);
        var request = new RedeemCouponRequest("c1", "order-1", 500m, "user-1");

        // Act
        var result = await _service.RedeemCouponAsync("user-1", request);

        // Assert
        result.Id.ShouldBe("u1");
        await _couponUsageRepository.DidNotReceive().AddAsync(Arg.Any<CouponUsage>(), default);
    }

    [Fact]
    public async Task Should_ThrowCouponNotFound_When_RedeemNonExistentCoupon()
    {
        // Arrange
        _couponRepository.FindByIdAsync("missing", default).Returns((Coupon?)null);
        var request = new RedeemCouponRequest("missing", "order-1", 500m, "user-1");

        // Act & Assert
        var act = async () => await _service.RedeemCouponAsync("user-1", request);
        var ex = await Should.ThrowAsync<CouponNotFoundException>(act);
        ex.Message.ShouldContain("missing");
    }

    [Fact]
    public async Task Should_ReleaseCoupon_When_UsageExists()
    {
        // Arrange
        var coupon = new Coupon { Id = "c1", Code = "REL", CurrentUsageCount = 1 };
        _couponRepository.FindByIdAsync("c1", default).Returns(coupon);
        var usage = new CouponUsage
        {
            Id = "u1", CouponId = "c1", UserId = "user-1",
            OrderId = "order-1", DiscountAmount = 500m
        };
        _couponUsageRepository.FindByOrderIdAsync("c1", "order-1", default).Returns(usage);
        var request = new ReleaseCouponRequest("c1", "order-1");

        // Act
        await _service.ReleaseCouponAsync(request);

        // Assert
        coupon.CurrentUsageCount.ShouldBe(0);
    }

    [Fact]
    public async Task Should_BeIdempotent_When_ReleaseAlreadyReleased()
    {
        // Arrange
        var coupon = new Coupon { Id = "c1", Code = "REL", CurrentUsageCount = 0 };
        _couponRepository.FindByIdAsync("c1", default).Returns(coupon);
        _couponUsageRepository.FindByOrderIdAsync("c1", "order-1", default).Returns((CouponUsage?)null);
        var request = new ReleaseCouponRequest("c1", "order-1");

        // Act — should not throw
        await _service.ReleaseCouponAsync(request);

        // Assert
        coupon.CurrentUsageCount.ShouldBe(0);
    }
}
