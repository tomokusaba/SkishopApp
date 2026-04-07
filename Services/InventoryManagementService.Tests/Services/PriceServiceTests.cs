using Xunit;
using InventoryManagementService.Configurations;
using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.Exceptions;
using InventoryManagementService.Models;
using InventoryManagementService.Repositories.Interfaces;
using InventoryManagementService.Services;
using InventoryManagementService.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace InventoryManagementService.Tests.Services;

/// <summary>
/// <see cref="PriceService"/> の単体テスト。
/// IPriceRepository・IProductRepository・IEventPublisherService・IDistributedCache を NSubstitute でモック化し、
/// 価格の作成・更新・履歴取得およびイベント発行ロジックを検証する。
/// </summary>
[Trait("Category", "Unit")]
public class PriceServiceTests
{
    private readonly IPriceRepository _priceRepository;
    private readonly IProductRepository _productRepository;
    private readonly IEventPublisherService _eventPublisher;
    private readonly IDistributedCache _cache;
    private readonly PriceService _sut;

    public PriceServiceTests()
    {
        _priceRepository = Substitute.For<IPriceRepository>();
        _productRepository = Substitute.For<IProductRepository>();
        _eventPublisher = Substitute.For<IEventPublisherService>();
        _cache = Substitute.For<IDistributedCache>();
        var cacheConfig = Options.Create(new CacheConfig { Enabled = false });
        var logger = Substitute.For<ILogger<PriceService>>();

        _sut = new PriceService(
            _priceRepository,
            _productRepository,
            _eventPublisher,
            _cache,
            cacheConfig,
            logger);
    }

    [Fact]
    public async Task Should_CreatePrice_When_ValidRequest()
    {
        // Arrange
        var product = new Product { Id = "prod-1", Sku = "SKI-BOOT-001", Name = "Boot" };
        _productRepository.FindByIdAsync("prod-1", Arg.Any<CancellationToken>())
            .Returns(product);
        _priceRepository.FindByProductIdAsync("prod-1", Arg.Any<CancellationToken>())
            .Returns(new List<Price>());

        var request = new PriceCreateRequest(
            ProductId: "prod-1",
            RegularPrice: 29800m,
            SalePrice: null,
            SaleStartDate: null,
            SaleEndDate: null,
            CurrencyCode: "JPY");

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.ProductId.ShouldBe("prod-1");
        result.RegularPrice.ShouldBe(29800m);
        result.CurrencyCode.ShouldBe("JPY");
        await _priceRepository.Received(1).AddAsync(Arg.Any<Price>(), Arg.Any<CancellationToken>());
        await _priceRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ThrowResourceNotFoundException_When_ProductNotFoundOnCreate()
    {
        // Arrange
        _productRepository.FindByIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((Product?)null);

        var request = new PriceCreateRequest(
            ProductId: "nonexistent",
            RegularPrice: 10000m,
            SalePrice: null,
            SaleStartDate: null,
            SaleEndDate: null,
            CurrencyCode: "JPY");

        // Act
        var act = async () => await _sut.CreateAsync(request);

        // Assert
        var ex = await Should.ThrowAsync<ResourceNotFoundException>(act);
        ex.Message.ShouldContain("Product");
    }

    [Fact]
    public async Task Should_UpdatePrice_When_ValidRequest()
    {
        // Arrange
        var price = CreateTestPrice();
        _priceRepository.FindByIdAsync(price.Id, Arg.Any<CancellationToken>())
            .Returns(price);

        var request = new PriceUpdateRequest(
            RegularPrice: 35000m,
            SalePrice: null,
            SaleStartDate: null,
            SaleEndDate: null,
            IsActive: null);

        // Act
        var result = await _sut.UpdateAsync(price.Id, request);

        // Assert
        result.ShouldNotBeNull();
        result.RegularPrice.ShouldBe(35000m);
        await _priceRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ThrowResourceNotFoundException_When_PriceNotFoundOnUpdate()
    {
        // Arrange
        _priceRepository.FindByIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((Price?)null);

        var request = new PriceUpdateRequest(
            RegularPrice: 10000m, SalePrice: null,
            SaleStartDate: null, SaleEndDate: null, IsActive: null);

        // Act
        var act = async () => await _sut.UpdateAsync("nonexistent", request);

        // Assert
        await Should.ThrowAsync<ResourceNotFoundException>(act);
    }

    [Fact]
    public async Task Should_ReturnNull_When_NoActivePriceForProduct()
    {
        // Arrange
        _priceRepository.FindActiveByProductIdAsync("prod-1", Arg.Any<CancellationToken>())
            .Returns((Price?)null);

        // Act
        var result = await _sut.GetByProductIdAsync("prod-1");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Should_ThrowOperationCanceled_When_TokenIsCanceled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _priceRepository.FindActiveByProductIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var act = async () => await _sut.GetByProductIdAsync("prod-1", cts.Token);

        // Assert
        await Should.ThrowAsync<OperationCanceledException>(act);
    }

    private static Price CreateTestPrice() => new()
    {
        Id = "price-1",
        ProductId = "prod-1",
        RegularPrice = 29800m,
        CurrencyCode = "JPY",
        IsActive = true
    };
}
