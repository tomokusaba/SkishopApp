using PaymentCartService.DTOs.Requests;
using PaymentCartService.Validators;
using Shouldly;
using Xunit;

namespace PaymentCartService.Tests.Validators;

public class CheckoutRequestValidatorTests
{
    private readonly CheckoutRequestValidator _validator = new();

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_ValidRequest()
    {
        // Arrange
        var request = new CheckoutRequest("cart-1", "CREDIT_CARD", null, null, null);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_CartIdIsEmpty()
    {
        // Arrange
        var request = new CheckoutRequest("", "CreditCard", null, null, null);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_PaymentMethodIsEmpty()
    {
        // Arrange
        var request = new CheckoutRequest("cart-1", "", null, null, null);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_UsedPointsIsNegative()
    {
        // Arrange
        var request = new CheckoutRequest("cart-1", "CreditCard", null, null, -1);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }
}
