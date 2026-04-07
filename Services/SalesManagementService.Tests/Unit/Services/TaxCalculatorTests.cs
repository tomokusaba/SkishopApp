using SalesManagementService.Services;
using Shouldly;

namespace SalesManagementService.Tests.Unit.Services;

public class TaxCalculatorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Should_Return100_When_Subtotal1000()
    {
        // Arrange
        var subtotal = 1000m;

        // Act
        var tax = TaxCalculator.CalculateStandardTax(subtotal);

        // Assert
        tax.ShouldBe(100m);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_ReturnZero_When_SubtotalIsZero()
    {
        // Arrange
        var subtotal = 0m;

        // Act
        var tax = TaxCalculator.CalculateStandardTax(subtotal);

        // Assert
        tax.ShouldBe(0m);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_TruncateFraction_When_FractionalResult()
    {
        // Arrange — 999 * 0.10 = 99.9 → 99 (切り捨て)
        var subtotal = 999m;

        // Act
        var tax = TaxCalculator.CalculateStandardTax(subtotal);

        // Assert
        tax.ShouldBe(99m);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(100, 10)]
    [InlineData(5000, 500)]
    [InlineData(12345, 1234)]
    [InlineData(1, 0)]
    public void Should_CalculateCorrectTax_When_VariousSubtotals(decimal subtotal, decimal expectedTax)
    {
        // Arrange & Act
        var tax = TaxCalculator.CalculateStandardTax(subtotal);

        // Assert
        tax.ShouldBe(expectedTax);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_HandleLargeAmount_When_VeryHighSubtotal()
    {
        // Arrange
        var subtotal = 10_000_000m;

        // Act
        var tax = TaxCalculator.CalculateStandardTax(subtotal);

        // Assert
        tax.ShouldBe(1_000_000m);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_HandleSmallAmount_When_OneYen()
    {
        // Arrange
        var subtotal = 1m;

        // Act
        var tax = TaxCalculator.CalculateStandardTax(subtotal);

        // Assert
        tax.ShouldBe(0m);
    }
}
