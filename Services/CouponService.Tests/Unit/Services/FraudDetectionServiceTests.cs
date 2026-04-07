using CouponService.Configurations;
using CouponService.DTOs.Responses;
using CouponService.Models;
using CouponService.Repositories.Interfaces;
using CouponService.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace CouponService.Tests.Unit.Services;

[Trait("Category", "Unit")]
public class FraudDetectionServiceTests
{
    private readonly ICouponUsageRepository _usageRepository;
    private readonly FakeTimeProvider _timeProvider;
    private readonly FraudDetectionService _service;

    public FraudDetectionServiceTests()
    {
        _usageRepository = Substitute.For<ICouponUsageRepository>();
        _timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
        var settings = Options.Create(new CouponSettings
        {
            FraudDetection = new FraudDetectionSettings
            {
                WindowMinutes = 10,
                MaxUsagesInWindow = 3
            }
        });
        var logger = Substitute.For<ILogger<FraudDetectionService>>();

        _service = new FraudDetectionService(
            _usageRepository, _timeProvider, settings, logger);
    }

    [Fact]
    public async Task Should_ReturnClear_When_NormalUsage()
    {
        // Arrange
        _usageRepository.CountRecentByUserAsync("user-1", Arg.Any<DateTimeOffset>(), default)
            .Returns(1);
        var coupon = new Coupon { Id = "c1", Code = "TEST" };

        // Act
        var result = await _service.CheckAsync("user-1", coupon);

        // Assert
        result.IsSuspicious.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_ReturnSuspicious_When_HighFrequencyUsage()
    {
        // Arrange
        _usageRepository.CountRecentByUserAsync("user-1", Arg.Any<DateTimeOffset>(), default)
            .Returns(5);
        var coupon = new Coupon { Id = "c1", Code = "TEST" };

        // Act
        var result = await _service.CheckAsync("user-1", coupon);

        // Assert
        result.IsSuspicious.ShouldBeTrue();
        result.Reason!.ShouldContain("5回");
    }

    [Fact]
    public async Task Should_ReturnFalse_When_IsSuspiciousAsync_NormalUsage()
    {
        // Arrange
        _usageRepository.CountRecentByUserAsync("user-1", Arg.Any<DateTimeOffset>(), default)
            .Returns(0);

        // Act
        var result = await _service.IsSuspiciousAsync("user-1");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_ReturnTrue_When_CheckFraudAsync_Suspicious()
    {
        // Arrange
        _usageRepository.CountRecentByUserAsync("user-1", Arg.Any<DateTimeOffset>(), default)
            .Returns(10);

        // Act
        var result = await _service.CheckFraudAsync("user-1", "CODE1", 5000m);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_ReturnSuspicious_When_ExactlyAtThreshold()
    {
        // Arrange
        _usageRepository.CountRecentByUserAsync("user-1", Arg.Any<DateTimeOffset>(), default)
            .Returns(3);
        var coupon = new Coupon { Id = "c1", Code = "TEST" };

        // Act
        var result = await _service.CheckAsync("user-1", coupon);

        // Assert
        result.IsSuspicious.ShouldBeTrue();
    }
}
