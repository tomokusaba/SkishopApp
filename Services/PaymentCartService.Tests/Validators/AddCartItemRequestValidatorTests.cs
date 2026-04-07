using PaymentCartService.DTOs.Requests;
using PaymentCartService.Validators;
using Shouldly;
using Xunit;

namespace PaymentCartService.Tests.Validators;

public class AddCartItemRequestValidatorTests
{
    private readonly AddCartItemRequestValidator _validator = new();

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_ValidRequest()
    {
        // Arrange
        var request = new AddCartItemRequest("prod-123", "テスト商品", "SKU-001", 1500m, 2);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_ProductIdIsEmpty(string productId)
    {
        // Arrange
        var request = new AddCartItemRequest(productId, "テスト商品", "SKU-001", 1500m, 1);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_QuantityIsOutOfRange(int quantity)
    {
        // Arrange
        var request = new AddCartItemRequest("prod-123", "テスト商品", "SKU-001", 1500m, quantity);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_QuantityIsBoundaryValue()
    {
        // Arrange
        var request = new AddCartItemRequest("prod-123", "テスト商品", "SKU-001", 1500m, 10);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
