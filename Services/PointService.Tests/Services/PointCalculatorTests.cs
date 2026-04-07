using PointService.Services;
using Shouldly;

namespace PointService.Tests.Services;

public class PointCalculatorTests
{
    private readonly PointCalculator _sut = new();

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_ReturnFlooredPoints_When_ValidInputsProvided()
    {
        // Arrange
        const decimal orderAmount = 1234.56m;
        const decimal pointRate = 0.05m;
        const decimal campaignMultiplier = 1.5m;

        // Act
        var result = _sut.Calculate(orderAmount, pointRate, campaignMultiplier);

        // Assert
        result.ShouldBe(92);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_ThrowArgumentOutOfRangeException_When_OrderAmountIsZero()
    {
        // Arrange
        const decimal pointRate = 0.05m;
        const decimal campaignMultiplier = 1.0m;

        // Act
        var act = () => _sut.Calculate(0m, pointRate, campaignMultiplier);

        // Assert
        Should.Throw<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_ThrowArgumentOutOfRangeException_When_PointRateIsNegative()
    {
        // Arrange
        const decimal orderAmount = 1000m;
        const decimal campaignMultiplier = 1.0m;

        // Act
        var act = () => _sut.Calculate(orderAmount, -0.1m, campaignMultiplier);

        // Assert
        Should.Throw<ArgumentOutOfRangeException>(act);
    }
}
