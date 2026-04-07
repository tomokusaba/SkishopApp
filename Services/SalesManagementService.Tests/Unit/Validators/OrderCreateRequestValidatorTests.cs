using FluentValidation.TestHelper;
using SalesManagementService.DTOs.Requests;
using SalesManagementService.Infrastructure.Validators;
using Shouldly;

namespace SalesManagementService.Tests.Unit.Validators;

public class OrderCreateRequestValidatorTests
{
    private readonly OrderCreateRequestValidator _validator = new();

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_Pass_When_ValidRequest()
    {
        // Arrange
        var request = CreateValidRequest();

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_Fail_When_CustomerIdIsEmpty()
    {
        // Arrange
        var request = CreateValidRequest() with { CustomerId = "" };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(x => x.CustomerId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_Fail_When_ItemsEmpty()
    {
        // Arrange
        var request = CreateValidRequest() with { Items = [] };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(x => x.Items);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_Fail_When_PaymentMethodIsEmpty()
    {
        // Arrange
        var request = CreateValidRequest() with { PaymentMethod = "" };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(x => x.PaymentMethod);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_Fail_When_ShippingAddressRecipientNameIsEmpty()
    {
        // Arrange
        var address = new ShippingAddressRequest("", "100-0001", "東京都", "千代田区", "番地1-1", null, "03-1234-5678");
        var request = CreateValidRequest() with { ShippingAddress = address };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_Fail_When_ShippingAddressPostalCodeIsEmpty()
    {
        // Arrange
        var address = new ShippingAddressRequest("テスト太郎", "", "東京都", "千代田区", "番地1-1", null, "03-1234-5678");
        var request = CreateValidRequest() with { ShippingAddress = address };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_Fail_When_ItemProductIdIsEmpty()
    {
        // Arrange
        var items = new List<OrderItemRequest>
        {
            new("", "テスト商品", "SKU-001", 1000m, 1)
        };
        var request = CreateValidRequest() with { Items = items };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_Fail_When_ItemPriceIsZero()
    {
        // Arrange
        var items = new List<OrderItemRequest>
        {
            new("prod-1", "テスト商品", "SKU-001", 0m, 1)
        };
        var request = CreateValidRequest() with { Items = items };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_Fail_When_ItemQuantityIsZero()
    {
        // Arrange
        var items = new List<OrderItemRequest>
        {
            new("prod-1", "テスト商品", "SKU-001", 1000m, 0)
        };
        var request = CreateValidRequest() with { Items = items };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_Fail_When_ItemQuantityExceeds99()
    {
        // Arrange
        var items = new List<OrderItemRequest>
        {
            new("prod-1", "テスト商品", "SKU-001", 1000m, 100)
        };
        var request = CreateValidRequest() with { Items = items };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_Pass_When_UsedPointsIsZero()
    {
        // Arrange
        var request = CreateValidRequest() with { UsedPoints = 0 };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_Fail_When_UsedPointsIsNegative()
    {
        // Arrange
        var request = CreateValidRequest() with { UsedPoints = -1 };

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    private static OrderCreateRequest CreateValidRequest()
    {
        var items = new List<OrderItemRequest>
        {
            new("prod-1", "テストスキー板", "SKI-001", 50000m, 1)
        };
        var address = new ShippingAddressRequest(
            "テスト太郎", "100-0001", "東京都", "千代田区", "番地1-1", null, "03-1234-5678");

        return new OrderCreateRequest(
            "cust-1", items, address, "CREDIT_CARD",
            CouponCode: null, UsedPoints: 0, Notes: null);
    }
}
