using System.ComponentModel.DataAnnotations;
using McpServer.DTOs.Requests;
using McpServer.DTOs.Responses;
using McpServer.Services;
using McpServer.Services.Interfaces;
using NSubstitute;
using Shouldly;
using Xunit;

namespace McpServer.Tests.Services;

public class CatalogToolServiceTests
{
    private readonly IInventoryCatalogClient _inventoryCatalogClient;
    private readonly CatalogToolService _sut;

    public CatalogToolServiceTests()
    {
        _inventoryCatalogClient = Substitute.For<IInventoryCatalogClient>();
        _sut = new CatalogToolService(_inventoryCatalogClient);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnProducts_When_SearchRequestIsValid()
    {
        // Arrange
        var request = new ProductSearchRequest("ski", null, null, null, 0, 20);
        var expected = new PaginatedCatalogProductsResult([], 0, 0, 20);
        _inventoryCatalogClient.SearchProductsAsync(request, Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        var result = await _sut.SearchProductsAsync(request);

        // Assert
        result.ShouldBe(expected);
        await _inventoryCatalogClient.Received(1).SearchProductsAsync(request, Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowValidationException_When_SearchSizeIsOutOfRange()
    {
        // Arrange
        var request = new ProductSearchRequest(null, null, null, null, 0, 101);

        // Act
        var act = async () => await _sut.SearchProductsAsync(request);

        // Assert
        var ex = await Should.ThrowAsync<ValidationException>(act);
        ex.Message.ShouldContain("Size");
        await _inventoryCatalogClient.DidNotReceive().SearchProductsAsync(Arg.Any<ProductSearchRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnProduct_When_ProductIdIsValid()
    {
        // Arrange
        var expected = new CatalogProductDto(
            "product-1",
            "SKU-001",
            "Powder Ski",
            "All-mountain ski",
            "North Ridge",
            "Skis",
            2.5m,
            true,
            DateTimeOffset.Parse("2026-01-15T12:00:00Z"));
        _inventoryCatalogClient.GetProductByIdAsync("product-1", Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        var result = await _sut.GetProductByIdAsync("product-1");

        // Assert
        result.ShouldBe(expected);
        await _inventoryCatalogClient.Received(1).GetProductByIdAsync("product-1", Arg.Any<CancellationToken>());
    }
}
