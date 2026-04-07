using Xunit;
using FluentValidation.TestHelper;
using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.Validators;
using Shouldly;

namespace InventoryManagementService.Tests.Validators;

/// <summary>
/// <see cref="PriceCreateRequestValidator"/> の単体テスト。
/// FluentValidation の TestValidateAsync を使用し、
/// 価格範囲・通貨コード・セール日付の整合性バリデーションルールを検証する。
/// </summary>
[Trait("Category", "Unit")]
public class PriceCreateRequestValidatorTests
{
    private readonly PriceCreateRequestValidator _validator = new();

    [Fact]
    public async Task Should_PassValidation_When_AllFieldsValid()
    {
        // Arrange
        var request = new PriceCreateRequest(
            ProductId: "prod-1",
            RegularPrice: 29800m,
            SalePrice: null,
            SaleStartDate: null,
            SaleEndDate: null,
            CurrencyCode: "JPY");

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_FailValidation_When_ProductIdIsEmpty()
    {
        // Arrange
        var request = new PriceCreateRequest(
            ProductId: "",
            RegularPrice: 10000m,
            SalePrice: null,
            SaleStartDate: null,
            SaleEndDate: null,
            CurrencyCode: "JPY");

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(x => x.ProductId);
    }

    [Fact]
    public async Task Should_FailValidation_When_SalePriceIsHigherThanRegularPrice()
    {
        // Arrange
        var request = new PriceCreateRequest(
            ProductId: "prod-1",
            RegularPrice: 10000m,
            SalePrice: 15000m,
            SaleStartDate: null,
            SaleEndDate: null,
            CurrencyCode: "JPY");

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(x => x.SalePrice);
    }

    [Theory]
    [InlineData("")]
    [InlineData("GBP")]
    [InlineData("CNY")]
    public async Task Should_FailValidation_When_CurrencyCodeIsInvalid(string currencyCode)
    {
        // Arrange
        var request = new PriceCreateRequest(
            ProductId: "prod-1",
            RegularPrice: 10000m,
            SalePrice: null,
            SaleStartDate: null,
            SaleEndDate: null,
            CurrencyCode: currencyCode);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(x => x.CurrencyCode);
    }

    [Fact]
    public async Task Should_FailValidation_When_SaleEndDateIsBeforeStartDate()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var request = new PriceCreateRequest(
            ProductId: "prod-1",
            RegularPrice: 10000m,
            SalePrice: 8000m,
            SaleStartDate: now.AddDays(5),
            SaleEndDate: now.AddDays(1),
            CurrencyCode: "JPY");

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(x => x.SaleEndDate);
    }
}
