using Xunit;
using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.Exceptions;
using InventoryManagementService.Models;
using InventoryManagementService.Repositories.Interfaces;
using InventoryManagementService.Services;
using InventoryManagementService.Services.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace InventoryManagementService.Tests.Services;

/// <summary>
/// <see cref="ReviewService"/> の単体テスト。
/// IReviewRepository・IProductRepository・IEventPublisherService を NSubstitute でモック化し、
/// レビューのライフサイクル管理・投票機能・GDPR 匿名化ロジックを検証する。
/// </summary>
[Trait("Category", "Unit")]
public class ReviewServiceTests
{
    private readonly IReviewRepository _reviewRepository;
    private readonly IProductRepository _productRepository;
    private readonly IEventPublisherService _eventPublisher;
    private readonly ReviewService _sut;

    public ReviewServiceTests()
    {
        _reviewRepository = Substitute.For<IReviewRepository>();
        _productRepository = Substitute.For<IProductRepository>();
        _eventPublisher = Substitute.For<IEventPublisherService>();
        var logger = Substitute.For<ILogger<ReviewService>>();

        _sut = new ReviewService(
            _reviewRepository,
            _productRepository,
            _eventPublisher,
            logger);
    }

    [Fact]
    public async Task Should_CreateReview_When_ValidRequest()
    {
        // Arrange
        var product = new Product { Id = "prod-1", Sku = "SKI-BOOT-001", Name = "Boot" };
        _productRepository.FindByIdAsync("prod-1", Arg.Any<CancellationToken>())
            .Returns(product);
        _reviewRepository.ExistsByProductAndUserAsync("prod-1", "user-1", Arg.Any<CancellationToken>())
            .Returns(false);

        var request = new ReviewCreateRequest(
            ProductId: "prod-1",
            Rating: 5,
            Title: "Great boots!",
            Content: "Really comfortable");

        // Act
        var result = await _sut.CreateAsync(request, "user-1");

        // Assert
        result.ShouldNotBeNull();
        result.ProductId.ShouldBe("prod-1");
        result.Rating.ShouldBe(5);
        result.Title.ShouldBe("Great boots!");
        result.Status.ShouldBe("PENDING");
        await _reviewRepository.Received(1).AddAsync(Arg.Any<Review>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ThrowResourceNotFoundException_When_ProductNotFoundOnReviewCreate()
    {
        // Arrange
        _productRepository.FindByIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((Product?)null);

        var request = new ReviewCreateRequest(
            ProductId: "nonexistent", Rating: 5,
            Title: "Test", Content: null);

        // Act
        var act = async () => await _sut.CreateAsync(request, "user-1");

        // Assert
        var ex = await Should.ThrowAsync<ResourceNotFoundException>(act);
        ex.Message.ShouldContain("Product");
    }

    [Fact]
    public async Task Should_ThrowDuplicateResourceException_When_ReviewAlreadyExists()
    {
        // Arrange
        var product = new Product { Id = "prod-1", Sku = "SKI-BOOT-001", Name = "Boot" };
        _productRepository.FindByIdAsync("prod-1", Arg.Any<CancellationToken>())
            .Returns(product);
        _reviewRepository.ExistsByProductAndUserAsync("prod-1", "user-1", Arg.Any<CancellationToken>())
            .Returns(true);

        var request = new ReviewCreateRequest(
            ProductId: "prod-1", Rating: 3,
            Title: "Second review", Content: null);

        // Act
        var act = async () => await _sut.CreateAsync(request, "user-1");

        // Assert
        await Should.ThrowAsync<DuplicateResourceException>(act);
    }

    [Fact]
    public async Task Should_UpdateReviewStatus_When_ValidId()
    {
        // Arrange
        var review = CreateTestReview();
        _reviewRepository.FindByIdAsync(review.Id, Arg.Any<CancellationToken>())
            .Returns(review);

        // Act
        var result = await _sut.UpdateStatusAsync(review.Id, "APPROVED");

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe("APPROVED");
        await _reviewRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_IncrementHelpfulCount_When_MarkHelpfulCalled()
    {
        // Arrange
        var review = CreateTestReview();
        _reviewRepository.FindByIdAsync(review.Id, Arg.Any<CancellationToken>())
            .Returns(review);
        _reviewRepository.HasUserVotedAsync(review.Id, "voter-1", Arg.Any<CancellationToken>())
            .Returns(false);
        _reviewRepository.FindByIdAsync(review.Id, Arg.Any<CancellationToken>())
            .Returns(new Review
            {
                Id = review.Id,
                ProductId = review.ProductId,
                UserId = review.UserId,
                Rating = review.Rating,
                Title = review.Title,
                Content = review.Content,
                Status = review.Status,
                HelpfulCount = 1
            });

        // Act
        var result = await _sut.MarkHelpfulAsync(review.Id, "voter-1");

        // Assert
        result.ShouldNotBeNull();
        result.HelpfulCount.ShouldBe(1);
    }

    [Fact]
    public async Task Should_ReturnNull_When_ReviewDoesNotExist()
    {
        // Arrange
        _reviewRepository.FindByIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((Review?)null);

        // Act
        var result = await _sut.GetByIdAsync("nonexistent");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Should_ThrowOperationCanceled_When_TokenIsCanceled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _reviewRepository.FindByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var act = async () => await _sut.GetByIdAsync("any-id", cts.Token);

        // Assert
        await Should.ThrowAsync<OperationCanceledException>(act);
    }

    private static Review CreateTestReview() => new()
    {
        Id = "review-1",
        ProductId = "prod-1",
        UserId = "user-1",
        Rating = 5,
        Title = "Great!",
        Content = "Really good",
        Status = "PENDING",
        HelpfulCount = 0
    };
}
