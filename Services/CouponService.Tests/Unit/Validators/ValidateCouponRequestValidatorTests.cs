using CouponService.DTOs.Requests;
using CouponService.Validators;
using Shouldly;
using Xunit;

namespace CouponService.Tests.Unit.Validators;

[Trait("Category", "Unit")]
public class ValidateCouponRequestValidatorTests
{
    private readonly ValidateCouponRequestValidator _validator = new();

    [Fact]
    public async Task Should_Pass_When_AllFieldsValid()
    {
        // Arrange
        var request = new ValidateCouponRequest("VALID10", 5000m, null);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Should_Fail_When_CodeIsEmpty(string code)
    {
        // Arrange
        var request = new ValidateCouponRequest(code, 5000m, null);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Fail_When_OrderAmountIsNegative()
    {
        // Arrange
        var request = new ValidateCouponRequest("CODE1", -100m, null);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Fail_When_OrderAmountIsZero()
    {
        // Arrange
        var request = new ValidateCouponRequest("CODE1", 0m, null);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Pass_When_CategoryIdIsNull()
    {
        // Arrange
        var request = new ValidateCouponRequest("CODE1", 1000m, null);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}

[Trait("Category", "Unit")]
public class CreateCouponRequestValidatorTests
{
    private readonly CreateCouponRequestValidator _validator = new();

    [Fact]
    public async Task Should_Pass_When_AllFieldsValid()
    {
        // Arrange
        var request = new CreateCouponRequest(
            "SUMMER2026", "type-1", null,
            (int)Models.DiscountType.Percentage, 10m, null,
            0m, 100, 1,
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Fail_When_CodeIsEmpty()
    {
        // Arrange
        var request = new CreateCouponRequest(
            "", null, null,
            (int)Models.DiscountType.Percentage, 10m, null,
            0m, 100, 1,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Fail_When_DiscountValueIsNegative()
    {
        // Arrange
        var request = new CreateCouponRequest(
            "CODE1", null, null,
            (int)Models.DiscountType.FixedAmount, -100m, null,
            0m, 100, 1,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }
}
