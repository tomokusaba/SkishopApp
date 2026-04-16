using System.ComponentModel.DataAnnotations;
using McpServer.DTOs.Requests;
using McpServer.DTOs.Responses;
using McpServer.Services.Interfaces;
using McpServer.Tools;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace McpServer.Tests.Tools;

public class CatalogToolsTests
{
    private readonly ICatalogToolService _catalogToolService;
    private readonly ILogger<CatalogTools> _logger;
    private readonly CatalogTools _sut;

    public CatalogToolsTests()
    {
        _catalogToolService = Substitute.For<ICatalogToolService>();
        _logger = Substitute.For<ILogger<CatalogTools>>();
        _sut = new CatalogTools(_catalogToolService, _logger);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnSearchResult_When_SearchSucceeds()
    {
        // Arrange
        var expected = new PaginatedCatalogProductsResult([], 5, 0, 20);
        _catalogToolService.SearchProductsAsync(
                Arg.Is<ProductSearchRequest>(request => request.Keyword == "ski" && request.Page == 0 && request.Size == 20),
                Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        var result = await _sut.SearchProductsAsync(keyword: "ski", page: 0, size: 20);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.Message.ShouldBeNull();
        result.Products.ShouldBe(expected);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnFailureResult_When_SearchValidationFails()
    {
        // Arrange
        _catalogToolService.SearchProductsAsync(Arg.Any<ProductSearchRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ValidationException("Page must be 0 or greater."));

        // Act
        var result = await _sut.SearchProductsAsync(page: -1, size: 0);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("Page");
        result.Products.TotalElements.ShouldBe(0);
        result.Products.Page.ShouldBe(0);
        result.Products.Size.ShouldBe(20);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnNotFoundResult_When_ProductDoesNotExist()
    {
        // Arrange
        _catalogToolService.GetProductByIdAsync("missing-product", Arg.Any<CancellationToken>())
            .Returns((CatalogProductDto?)null);

        // Act
        var result = await _sut.GetProductByIdAsync("missing-product");

        // Assert
        result.Found.ShouldBeFalse();
        result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("product ID");
        result.Product.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnFailureResult_When_SkuLookupHitsUpstreamError()
    {
        // Arrange
        _catalogToolService.GetProductBySkuAsync("SKU-001", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Inventory unavailable"));

        // Act
        var result = await _sut.GetProductBySkuAsync("SKU-001");

        // Assert
        result.Found.ShouldBeFalse();
        result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("temporarily unavailable");
        result.Product.ShouldBeNull();
    }
}
