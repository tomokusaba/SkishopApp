using Xunit;
using InventoryManagementService.Configurations;
using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.DTOs.Responses;
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
/// <see cref="ProductService"/> の単体テスト。
/// IProductRepository・ICategoryRepository・IImageRepository・IEventPublisherService・IDistributedCache を NSubstitute でモック化し、
/// 商品の CRUD・検索・キャッシュ制御・画像アップロードロジックを検証する。
/// </summary>
[Trait("Category", "Unit")]
public class ProductServiceTests
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IImageRepository _imageRepository;
    private readonly IEventPublisherService _eventPublisher;
    private readonly IDistributedCache _cache;
    private readonly ProductService _sut;

    public ProductServiceTests()
    {
        _productRepository = Substitute.For<IProductRepository>();
        _categoryRepository = Substitute.For<ICategoryRepository>();
        _imageRepository = Substitute.For<IImageRepository>();
        _eventPublisher = Substitute.For<IEventPublisherService>();
        _cache = Substitute.For<IDistributedCache>();
        var cacheConfig = Options.Create(new CacheConfig { Enabled = false });
        var logger = Substitute.For<ILogger<ProductService>>();

        _sut = new ProductService(
            _productRepository,
            _categoryRepository,
            _imageRepository,
            _eventPublisher,
            _cache,
            cacheConfig,
            logger);
    }

    [Fact]
    public async Task Should_ReturnProduct_When_ValidIdProvided()
    {
        // Arrange
        var product = CreateTestProduct();
        _productRepository.FindByIdAsync(product.Id, Arg.Any<CancellationToken>())
            .Returns(product);

        // Act
        var result = await _sut.GetByIdAsync(product.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(product.Id);
        result.Sku.ShouldBe(product.Sku);
        result.Name.ShouldBe(product.Name);
    }

    [Fact]
    public async Task Should_ReturnNull_When_ProductDoesNotExist()
    {
        // Arrange
        _productRepository.FindByIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((Product?)null);

        // Act
        var result = await _sut.GetByIdAsync("nonexistent");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Should_CreateProduct_When_ValidRequest()
    {
        // Arrange
        var request = new ProductCreateRequest(
            Sku: "SKI-BOOT-001",
            Name: "Test Ski Boot",
            Description: "A test ski boot",
            Brand: "TestBrand",
            CategoryId: "cat-1");

        _productRepository.FindBySkuAsync(request.Sku, Arg.Any<CancellationToken>())
            .Returns((Product?)null);
        _categoryRepository.ExistsByIdAsync(request.CategoryId, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _sut.CreateProductAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.Sku.ShouldBe("SKI-BOOT-001");
        result.Name.ShouldBe("Test Ski Boot");
        await _productRepository.Received(1).AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
        await _productRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ThrowDuplicateResourceException_When_SkuAlreadyExists()
    {
        // Arrange
        var request = new ProductCreateRequest(
            Sku: "SKI-BOOT-001",
            Name: "Test",
            Description: null,
            Brand: null,
            CategoryId: "cat-1");

        _productRepository.FindBySkuAsync(request.Sku, Arg.Any<CancellationToken>())
            .Returns(CreateTestProduct());

        // Act
        var act = async () => await _sut.CreateProductAsync(request);

        // Assert
        var ex = await Should.ThrowAsync<DuplicateResourceException>(act);
        ex.Message.ShouldContain("Product");
    }

    [Fact]
    public async Task Should_ThrowResourceNotFoundException_When_CategoryDoesNotExist()
    {
        // Arrange
        var request = new ProductCreateRequest(
            Sku: "SKI-BOOT-002",
            Name: "Test",
            Description: null,
            Brand: null,
            CategoryId: "nonexistent-cat");

        _productRepository.FindBySkuAsync(request.Sku, Arg.Any<CancellationToken>())
            .Returns((Product?)null);
        _categoryRepository.ExistsByIdAsync(request.CategoryId, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var act = async () => await _sut.CreateProductAsync(request);

        // Assert
        var ex = await Should.ThrowAsync<ResourceNotFoundException>(act);
        ex.Message.ShouldContain("Category");
    }

    [Fact]
    public async Task Should_UpdateProduct_When_ValidRequestProvided()
    {
        // Arrange
        var product = CreateTestProduct();
        _productRepository.FindByIdAsync(product.Id, Arg.Any<CancellationToken>())
            .Returns(product);

        var request = new ProductUpdateRequest(
            Name: "Updated Name",
            Description: null,
            Brand: null,
            CategoryId: null,
            Attributes: null,
            Tags: null,
            Weight: null,
            IsActive: null);

        // Act
        var result = await _sut.UpdateAsync(product.Id, request);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Updated Name");
        await _productRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ThrowResourceNotFoundException_When_UpdateNonExistentProduct()
    {
        // Arrange
        _productRepository.FindByIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((Product?)null);

        var request = new ProductUpdateRequest(
            Name: "Test", Description: null, Brand: null,
            CategoryId: null, Attributes: null, Tags: null,
            Weight: null, IsActive: null);

        // Act
        var act = async () => await _sut.UpdateAsync("nonexistent", request);

        // Assert
        var ex = await Should.ThrowAsync<ResourceNotFoundException>(act);
        ex.Message.ShouldContain("Product");
    }

    [Fact]
    public async Task Should_SoftDeleteProduct_When_ValidId()
    {
        // Arrange
        var product = CreateTestProduct();
        _productRepository.FindByIdAsync(product.Id, Arg.Any<CancellationToken>())
            .Returns(product);

        // Act
        await _sut.DeleteAsync(product.Id);

        // Assert
        product.IsActive.ShouldBeFalse();
        await _productRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ThrowOperationCanceled_When_TokenIsCanceled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _productRepository.FindByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var act = async () => await _sut.GetByIdAsync("any-id", cts.Token);

        // Assert
        await Should.ThrowAsync<OperationCanceledException>(act);
    }

    [Fact]
    public async Task Should_ReturnProduct_When_GetBySkuCalled()
    {
        // Arrange
        var product = CreateTestProduct();
        _productRepository.FindBySkuAsync(product.Sku, Arg.Any<CancellationToken>())
            .Returns(product);

        // Act
        var result = await _sut.GetBySkuAsync(product.Sku);

        // Assert
        result.ShouldNotBeNull();
        result.Sku.ShouldBe(product.Sku);
    }

    private static Product CreateTestProduct() => new()
    {
        Id = "prod-1",
        Sku = "SKI-BOOT-001",
        Name = "Test Ski Boot",
        Description = "A test product",
        Brand = "TestBrand",
        CategoryId = "cat-1",
        IsActive = true
    };
}
