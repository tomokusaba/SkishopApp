using Frontend.DTOs;
using Frontend.Models;
using Frontend.Services;
using Frontend.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Frontend.Tests.Services;

[Trait("Category", "Unit")]
public class ProductApiClientTests
{
    private readonly IApiGatewayClient _apiClient;
    private readonly CacheService _cacheService;
    private readonly ILogger<ProductApiClient> _logger;
    private readonly ProductApiClient _productClient;

    public ProductApiClientTests()
    {
        _apiClient = Substitute.For<IApiGatewayClient>();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cacheLogger = Substitute.For<ILogger<CacheService>>();
        _cacheService = new CacheService(memoryCache, cacheLogger);
        _logger = Substitute.For<ILogger<ProductApiClient>>();
        _productClient = new ProductApiClient(_apiClient, _cacheService, _logger);
    }

    [Fact]
    public async Task Should_ReturnProducts_When_GetAllCalled()
    {
        // Arrange
        var products = new PaginatedResult<ProductDto>(
            Items:
            [
                new ProductDto("p1", "スキーブーツ", "高性能ブーツ", 29800m, null, "cat1", "ブーツ",
                    ["img1.jpg"], 10, ["26cm"], ["黒"], 4.5, 10, DateTime.UtcNow),
                new ProductDto("p2", "ゴーグル", "UV カットゴーグル", 12000m, null, "cat2", "アクセサリ",
                    ["img2.jpg"], 5, [], ["白"], 4.0, 8, DateTime.UtcNow)
            ],
            TotalElements: 2,
            Page: 0,
            Size: 20);

        _apiClient.GetAsync<PaginatedResult<ProductDto>>(
            Arg.Is<string>(s => s.StartsWith("/api/products")),
            Arg.Any<CancellationToken>())
            .Returns(products);

        // Act
        var result = await _productClient.GetProductsAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(2);
        result.Items[0].Name.ShouldBe("スキーブーツ");
        result.TotalElements.ShouldBe(2);
    }

    [Fact]
    public async Task Should_ReturnProduct_When_GetByIdCalled()
    {
        // Arrange
        var product = new ProductDto("p1", "スキーブーツ", "高性能ブーツ", 29800m, null, "cat1", "ブーツ",
            ["img1.jpg"], 10, ["26cm"], ["黒"], 4.5, 10, DateTime.UtcNow);

        _apiClient.GetAsync<ProductDto>("/api/products/p1", Arg.Any<CancellationToken>())
            .Returns(product);

        // Act
        var result = await _productClient.GetProductByIdAsync("p1");

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("p1");
        result.Name.ShouldBe("スキーブーツ");
        result.Price.ShouldBe(29800m);
    }

    [Fact]
    public async Task Should_ReturnEmptyList_When_NoProductsFound()
    {
        // Arrange
        _apiClient.GetAsync<PaginatedResult<ProductDto>>(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns((PaginatedResult<ProductDto>?)null);

        // Act
        var result = await _productClient.GetNewArrivalsAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }
}
