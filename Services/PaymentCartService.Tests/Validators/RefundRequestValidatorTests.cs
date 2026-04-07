using PaymentCartService.DTOs.Requests;
using PaymentCartService.Validators;
using Shouldly;
using Xunit;

namespace PaymentCartService.Tests.Validators;

public class RefundRequestValidatorTests
{
    private readonly RefundRequestValidator _validator = new();

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_ValidRequest()
    {
        // Arrange
        var request = new RefundRequest(5000m, "返品のため");

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_AmountIsZero()
    {
        // Arrange
        var request = new RefundRequest(0m, "返品のため");

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_AmountIsNegative()
    {
        // Arrange
        var request = new RefundRequest(-100m, "返品のため");

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_ReasonIsEmpty()
    {
        // Arrange
        var request = new RefundRequest(5000m, "");

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_ReasonExceedsMaxLength()
    {
        // Arrange
        var longReason = new string('あ', 501);
        var request = new RefundRequest(5000m, longReason);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }
}
