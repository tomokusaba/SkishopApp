using Xunit;
using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.Exceptions;
using InventoryManagementService.Models;
using InventoryManagementService.Repositories.Interfaces;
using InventoryManagementService.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace InventoryManagementService.Tests.Services;

/// <summary>
/// <see cref="SizeGuideService"/> の単体テスト。
/// ISizeGuideRepository・ICategoryRepository を NSubstitute でモック化し、
/// サイズガイドの CRUD 操作ロジックを検証する。
/// </summary>
[Trait("Category", "Unit")]
public class SizeGuideServiceTests
{
    private readonly ISizeGuideRepository _sizeGuideRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly SizeGuideService _sut;

    public SizeGuideServiceTests()
    {
        _sizeGuideRepository = Substitute.For<ISizeGuideRepository>();
        _categoryRepository = Substitute.For<ICategoryRepository>();
        var logger = Substitute.For<ILogger<SizeGuideService>>();

        _sut = new SizeGuideService(
            _sizeGuideRepository,
            _categoryRepository,
            logger);
    }

    [Fact]
    public async Task Should_ReturnSizeGuide_When_CategoryIdExists()
    {
        // Arrange
        var guide = CreateTestSizeGuide();
        _sizeGuideRepository.FindByCategoryIdAsync("cat-1", Arg.Any<CancellationToken>())
            .Returns(guide);

        // Act
        var result = await _sut.GetByCategoryIdAsync("cat-1");

        // Assert
        result.ShouldNotBeNull();
        result.CategoryId.ShouldBe("cat-1");
        result.GuideType.ShouldBe("SKI_BOOTS");
    }

    [Fact]
    public async Task Should_ReturnNull_When_SizeGuideDoesNotExist()
    {
        // Arrange
        _sizeGuideRepository.FindByCategoryIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((SizeGuide?)null);

        // Act
        var result = await _sut.GetByCategoryIdAsync("nonexistent");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Should_CreateSizeGuide_When_ValidRequest()
    {
        // Arrange
        _categoryRepository.ExistsByIdAsync("cat-1", Arg.Any<CancellationToken>())
            .Returns(true);

        var request = new SizeGuideCreateRequest(
            CategoryId: "cat-1",
            SizeChart: "{\"sizes\":[\"S\",\"M\",\"L\"]}",
            GuideType: "SKI_BOOTS");

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.CategoryId.ShouldBe("cat-1");
        result.GuideType.ShouldBe("SKI_BOOTS");
        await _sizeGuideRepository.Received(1).AddAsync(Arg.Any<SizeGuide>(), Arg.Any<CancellationToken>());
        await _sizeGuideRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ThrowResourceNotFoundException_When_CategoryNotFoundOnCreate()
    {
        // Arrange
        _categoryRepository.ExistsByIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns(false);

        var request = new SizeGuideCreateRequest(
            CategoryId: "nonexistent",
            SizeChart: "{}",
            GuideType: "SKI_BOOTS");

        // Act
        var act = async () => await _sut.CreateAsync(request);

        // Assert
        var ex = await Should.ThrowAsync<ResourceNotFoundException>(act);
        ex.Message.ShouldContain("Category");
    }

    [Fact]
    public async Task Should_UpdateSizeGuide_When_ValidRequest()
    {
        // Arrange
        var guide = CreateTestSizeGuide();
        _sizeGuideRepository.FindByIdAsync(guide.Id, Arg.Any<CancellationToken>())
            .Returns(guide);

        var request = new SizeGuideUpdateRequest(
            SizeChart: "{\"sizes\":[\"XS\",\"S\",\"M\",\"L\",\"XL\"]}",
            GuideType: null);

        // Act
        var result = await _sut.UpdateAsync(guide.Id, request);

        // Assert
        result.ShouldNotBeNull();
        result.SizeChart.ShouldContain("XL");
    }

    [Fact]
    public async Task Should_ThrowOperationCanceled_When_TokenIsCanceled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _sizeGuideRepository.FindByCategoryIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var act = async () => await _sut.GetByCategoryIdAsync("any-id", cts.Token);

        // Assert
        await Should.ThrowAsync<OperationCanceledException>(act);
    }

    private static SizeGuide CreateTestSizeGuide() => new()
    {
        Id = "guide-1",
        CategoryId = "cat-1",
        SizeChart = "{\"sizes\":[\"S\",\"M\",\"L\"]}",
        GuideType = "SKI_BOOTS"
    };
}
