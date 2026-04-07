using PaymentCartService.DTOs.Requests;
using PaymentCartService.Validators;
using Shouldly;
using Xunit;

namespace PaymentCartService.Tests.Validators;

public class UpdateCartItemRequestValidatorTests
{
    private readonly UpdateCartItemRequestValidator _validator = new();

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_ValidQuantity()
    {
        // Arrange
        var request = new UpdateCartItemRequest(5);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_QuantityIsOutOfRange(int quantity)
    {
        // Arrange
        var request = new UpdateCartItemRequest(quantity);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_QuantityIsBoundary(int quantity)
    {
        // Arrange
        var request = new UpdateCartItemRequest(quantity);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
