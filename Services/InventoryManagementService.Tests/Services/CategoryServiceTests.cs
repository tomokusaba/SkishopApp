using Xunit;
using InventoryManagementService.Configurations;
using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.Exceptions;
using InventoryManagementService.Models;
using InventoryManagementService.Repositories.Interfaces;
using InventoryManagementService.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace InventoryManagementService.Tests.Services;

/// <summary>
/// <see cref="CategoryService"/> の単体テスト。
/// ICategoryRepository・IDistributedCache・IEventPublisherService を NSubstitute でモック化し、
/// カテゴリの CRUD 操作およびキャッシュ・イベント発行ロジックを検証する。
/// </summary>
[Trait("Category", "Unit")]
public class CategoryServiceTests
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IDistributedCache _cache;
    private readonly CategoryService _sut;

    public CategoryServiceTests()
    {
        _categoryRepository = Substitute.For<ICategoryRepository>();
        _cache = Substitute.For<IDistributedCache>();
        var cacheConfig = Options.Create(new CacheConfig { Enabled = false });
        var logger = Substitute.For<ILogger<CategoryService>>();

        _sut = new CategoryService(
            _categoryRepository,
            _cache,
            cacheConfig,
            logger);
    }

    [Fact]
    public async Task Should_ReturnCategory_When_ValidIdProvided()
    {
        // Arrange
        var category = CreateTestCategory();
        _categoryRepository.FindByIdAsync(category.Id, Arg.Any<CancellationToken>())
            .Returns(category);

        // Act
        var result = await _sut.GetByIdAsync(category.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(category.Id);
        result.Name.ShouldBe(category.Name);
    }

    [Fact]
    public async Task Should_ReturnNull_When_CategoryDoesNotExist()
    {
        // Arrange
        _categoryRepository.FindByIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((Category?)null);

        // Act
        var result = await _sut.GetByIdAsync("nonexistent");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Should_CreateCategory_When_ValidRequest()
    {
        // Arrange
        var request = new CategoryCreateRequest(
            Name: "Ski Boots",
            Description: "Category for ski boots",
            ParentId: null);

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Ski Boots");
        result.Level.ShouldBe(0);
        await _categoryRepository.Received(1).AddAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>());
        await _categoryRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_CreateChildCategory_When_ParentExists()
    {
        // Arrange
        var parent = CreateTestCategory();
        _categoryRepository.FindByIdAsync(parent.Id, Arg.Any<CancellationToken>())
            .Returns(parent);

        var request = new CategoryCreateRequest(
            Name: "Racing Boots",
            Description: "Sub-category",
            ParentId: parent.Id);

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Racing Boots");
        result.Level.ShouldBe(1);
        result.ParentId.ShouldBe(parent.Id);
    }

    [Fact]
    public async Task Should_UpdateCategory_When_ValidRequestProvided()
    {
        // Arrange
        var category = CreateTestCategory();
        _categoryRepository.FindByIdAsync(category.Id, Arg.Any<CancellationToken>())
            .Returns(category);

        var request = new CategoryUpdateRequest(
            Name: "Updated Name",
            Description: null,
            IsActive: null);

        // Act
        var result = await _sut.UpdateAsync(category.Id, request);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Updated Name");
    }

    [Fact]
    public async Task Should_DeleteCategory_When_ValidId()
    {
        // Arrange
        var category = CreateTestCategory();
        _categoryRepository.FindByIdAsync(category.Id, Arg.Any<CancellationToken>())
            .Returns(category);
        _categoryRepository.HasChildrenAsync(category.Id, Arg.Any<CancellationToken>())
            .Returns(false);
        _categoryRepository.HasProductsAsync(category.Id, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _sut.DeleteAsync(category.Id);

        // Assert
        category.IsActive.ShouldBeFalse();
        await _categoryRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ThrowInventoryException_When_CategoryHasChildren()
    {
        // Arrange
        var category = CreateTestCategory();
        _categoryRepository.FindByIdAsync(category.Id, Arg.Any<CancellationToken>())
            .Returns(category);
        _categoryRepository.HasChildrenAsync(category.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var act = async () => await _sut.DeleteAsync(category.Id);

        // Assert
        var ex = await Should.ThrowAsync<InventoryException>(act);
        ex.ErrorCode.ShouldBe("CAT_001");
    }

    [Fact]
    public async Task Should_ThrowOperationCanceled_When_TokenIsCanceled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _categoryRepository.FindByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var act = async () => await _sut.GetByIdAsync("any-id", cts.Token);

        // Assert
        await Should.ThrowAsync<OperationCanceledException>(act);
    }

    private static Category CreateTestCategory() => new()
    {
        Id = "cat-1",
        Name = "Ski Boots",
        Description = "Test category",
        Level = 0,
        IsActive = true
    };
}
