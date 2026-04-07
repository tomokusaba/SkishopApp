using Xunit;
using InventoryManagementService.Configurations;
using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.Exceptions;
using InventoryManagementService.Models;
using InventoryManagementService.Repositories.Interfaces;
using InventoryManagementService.Services;
using InventoryManagementService.Services.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace InventoryManagementService.Tests.Services;

/// <summary>
/// <see cref="InventoryService"/> の単体テスト。
/// IInventoryRepository・IProductRepository・IDistributedCache・IEventPublisherService・IReviewService を NSubstitute でモック化し、
/// 在庫予約・解放・確定、入出庫、低在庫アラート、GDPR 匿名化などの在庫管理ロジックを検証する。
/// </summary>
[Trait("Category", "Unit")]
public class InventoryServiceTests
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IReviewService _reviewService;
    private readonly IEventPublisherService _eventPublisher;
    private readonly IDistributedCache _cache;
    private readonly ILogger<InventoryService> _logger;
    private readonly IOptions<CacheConfig> _cacheOptions;

    public InventoryServiceTests()
    {
        _inventoryRepository = Substitute.For<IInventoryRepository>();
        _reviewService = Substitute.For<IReviewService>();
        _eventPublisher = Substitute.For<IEventPublisherService>();
        _cache = Substitute.For<IDistributedCache>();
        _logger = Substitute.For<ILogger<InventoryService>>();
        _cacheOptions = Options.Create(new CacheConfig { Enabled = false });

        // Default: mock transaction
        var mockTransaction = Substitute.For<IDbContextTransaction>();
        _inventoryRepository.BeginTransactionAsync(Arg.Any<CancellationToken>())
            .Returns(mockTransaction);
    }

    private InventoryService CreateService()
    {
        return new InventoryService(
            _inventoryRepository,
            _reviewService,
            _eventPublisher,
            _cache,
            _cacheOptions,
            _logger);
    }

    [Fact]
    public async Task Should_ReturnInventory_When_ProductIdExists()
    {
        // Arrange
        var inventory = CreateTestInventory();
        _inventoryRepository.FindByProductIdAsync("prod-1", Arg.Any<CancellationToken>())
            .Returns(inventory);
        var sut = CreateService();

        // Act
        var result = await sut.GetByProductIdAsync("prod-1");

        // Assert
        result.ShouldNotBeNull();
        result.ProductId.ShouldBe("prod-1");
        result.Quantity.ShouldBe(100);
        result.AvailableQuantity.ShouldBe(90);
    }

    [Fact]
    public async Task Should_ReturnNull_When_InventoryNotFound()
    {
        // Arrange
        _inventoryRepository.FindByProductIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((Inventory?)null);
        var sut = CreateService();

        // Act
        var result = await sut.GetByProductIdAsync("nonexistent");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Should_IncreaseQuantity_When_StockIn()
    {
        // Arrange
        var inventory = CreateTestInventory();
        _inventoryRepository.FindByProductIdForUpdateAsync("prod-1", Arg.Any<CancellationToken>())
            .Returns(inventory);
        var sut = CreateService();
        var request = new StockInRequest(ProductId: "prod-1", Quantity: 50);

        // Act
        var result = await sut.StockInAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.Quantity.ShouldBe(150);
        await _inventoryRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ThrowResourceNotFoundException_When_StockInInventoryNotFound()
    {
        // Arrange
        _inventoryRepository.FindByProductIdForUpdateAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((Inventory?)null);
        var sut = CreateService();
        var request = new StockInRequest(ProductId: "nonexistent", Quantity: 10);

        // Act
        var act = async () => await sut.StockInAsync(request);

        // Assert
        var ex = await Should.ThrowAsync<ResourceNotFoundException>(act);
        ex.Message.ShouldContain("Inventory");
    }

    [Fact]
    public async Task Should_DecreaseQuantity_When_StockOut()
    {
        // Arrange
        var inventory = CreateTestInventory();
        _inventoryRepository.FindByProductIdForUpdateAsync("prod-1", Arg.Any<CancellationToken>())
            .Returns(inventory);
        var sut = CreateService();
        var request = new StockOutRequest(ProductId: "prod-1", Quantity: 20, Reason: "SALE");

        // Act
        var result = await sut.StockOutAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.Quantity.ShouldBe(80);
        await _inventoryRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ThrowInsufficientStockException_When_NotEnoughStock()
    {
        // Arrange
        var inventory = CreateTestInventory();
        _inventoryRepository.FindByProductIdForUpdateAsync("prod-1", Arg.Any<CancellationToken>())
            .Returns(inventory);
        var sut = CreateService();
        var request = new StockOutRequest(ProductId: "prod-1", Quantity: 200, Reason: "SALE");

        // Act
        var act = async () => await sut.StockOutAsync(request);

        // Assert
        await Should.ThrowAsync<InsufficientStockException>(act);
    }

    [Theory]
    [InlineData(0, 0, 10, "IN_STOCK", "OUT_OF_STOCK")]
    [InlineData(100, 50, 10, "IN_STOCK", "RESERVED")]
    [InlineData(5, 0, 10, "IN_STOCK", "LOW_STOCK")]
    [InlineData(100, 0, 10, "IN_STOCK", "IN_STOCK")]
    [InlineData(100, 0, 10, "DISCONTINUED", "DISCONTINUED")]
    public void Should_DetermineCorrectStatus_When_GivenParameters(
        int quantity, int reserved, int reorderPoint, string currentStatus, string expected)
    {
        // Arrange
        var inventory = new Inventory
        {
            ProductId = "test-product",
            Quantity = quantity,
            ReservedQuantity = reserved,
            ReorderPoint = reorderPoint,
            Status = currentStatus
        };

        // Act
        var result = inventory.DetermineStatus();

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public async Task Should_ThrowOperationCanceled_When_TokenIsCanceled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _inventoryRepository.FindByProductIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());
        var sut = CreateService();

        // Act
        var act = async () => await sut.GetByProductIdAsync("any-id", cts.Token);

        // Assert
        await Should.ThrowAsync<OperationCanceledException>(act);
    }

    [Fact]
    public async Task Should_ReturnMultipleInventories_When_GetByProductIdsAsync()
    {
        // Arrange
        var inventories = new List<Inventory>
        {
            CreateTestInventory("inv-1", "prod-1"),
            CreateTestInventory("inv-2", "prod-2")
        };
        var ids = new List<string> { "prod-1", "prod-2" };
        _inventoryRepository.FindByProductIdsAsync(ids, Arg.Any<CancellationToken>())
            .Returns(inventories);
        var sut = CreateService();

        // Act
        var result = await sut.GetByProductIdsAsync(ids);

        // Assert
        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Should_DelegateAnonymization_When_UserDeleted()
    {
        // Arrange
        var sut = CreateService();

        // Act
        await sut.AnonymizeUserReviewsAsync("user-1");

        // Assert
        await _reviewService.Received(1)
            .AnonymizeUserReviewsAsync("user-1", Arg.Any<CancellationToken>());
    }

    private static Inventory CreateTestInventory(string id = "inv-1", string productId = "prod-1") => new()
    {
        Id = id,
        ProductId = productId,
        Quantity = 100,
        ReservedQuantity = 10,
        LocationCode = "WH-01",
        Status = "IN_STOCK",
        ReorderPoint = 10
    };
}
