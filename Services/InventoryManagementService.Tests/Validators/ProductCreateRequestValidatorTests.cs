using Xunit;
using FluentValidation.TestHelper;
using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.Validators;
using Shouldly;

namespace InventoryManagementService.Tests.Validators;

/// <summary>
/// <see cref="ProductCreateRequestValidator"/> の単体テスト。
/// FluentValidation の TestValidateAsync を使用し、
/// SKU フォーマット・商品名の長さ・重量範囲のバリデーションルールを検証する。
/// </summary>
[Trait("Category", "Unit")]
public class ProductCreateRequestValidatorTests
{
    private readonly ProductCreateRequestValidator _validator = new();

    [Fact]
    public async Task Should_PassValidation_When_AllFieldsValid()
    {
        // Arrange
        var request = new ProductCreateRequest(
            Sku: "SKI-BOOT-001",
            Name: "Test Ski Boot",
            Description: "A test product",
            Brand: "TestBrand",
            CategoryId: "cat-1");

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_FailValidation_When_SkuIsBlank(string sku)
    {
        // Arrange
        var request = new ProductCreateRequest(
            Sku: sku,
            Name: "Test",
            Description: null,
            Brand: null,
            CategoryId: "cat-1");

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(x => x.Sku);
    }

    [Theory]
    [InlineData("invalid-sku")]
    [InlineData("123")]
    [InlineData("ski-boot-001")]
    public async Task Should_FailValidation_When_SkuFormatInvalid(string sku)
    {
        // Arrange
        var request = new ProductCreateRequest(
            Sku: sku,
            Name: "Test",
            Description: null,
            Brand: null,
            CategoryId: "cat-1");

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(x => x.Sku);
    }

    [Fact]
    public async Task Should_FailValidation_When_NameIsEmpty()
    {
        // Arrange
        var request = new ProductCreateRequest(
            Sku: "SKI-BOOT-001",
            Name: "",
            Description: null,
            Brand: null,
            CategoryId: "cat-1");

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public async Task Should_FailValidation_When_CategoryIdIsEmpty()
    {
        // Arrange
        var request = new ProductCreateRequest(
            Sku: "SKI-BOOT-001",
            Name: "Test",
            Description: null,
            Brand: null,
            CategoryId: "");

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(x => x.CategoryId);
    }

    [Fact]
    public async Task Should_FailValidation_When_WeightIsNegative()
    {
        // Arrange
        var request = new ProductCreateRequest(
            Sku: "SKI-BOOT-001",
            Name: "Test",
            Description: null,
            Brand: null,
            CategoryId: "cat-1",
            Weight: -1m);

        // Act
        var result = await _validator.TestValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(x => x.Weight);
    }
}
