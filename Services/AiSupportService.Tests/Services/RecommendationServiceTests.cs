using System.Diagnostics.Metrics;
using System.Text.Json;
using AiSupportService.DTOs.Requests;
using AiSupportService.Exceptions;
using AiSupportService.Infrastructure.Metrics;
using AiSupportService.Models;
using AiSupportService.Repositories.Interfaces;
using AiSupportService.Services;
using AiSupportService.Services.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AiSupportService.Tests.Services;

public class RecommendationServiceTests
{
    private readonly IRecommendationRepository _recommendationRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IProductClient _productClient;
    private readonly IOutboxEventRepository _outboxEventRepository;
    private readonly RecommendationService _sut;

    public RecommendationServiceTests()
    {
        _recommendationRepository = Substitute.For<IRecommendationRepository>();
        _userProfileRepository = Substitute.For<IUserProfileRepository>();
        _productClient = Substitute.For<IProductClient>();
        _outboxEventRepository = Substitute.For<IOutboxEventRepository>();
        var meterFactory = new TestMeterFactory();
        var metrics = new AiSupportMetrics(meterFactory);
        var logger = Substitute.For<ILogger<RecommendationService>>();

        var cacheService = Substitute.For<ICacheService>();
        _sut = new RecommendationService(
            _recommendationRepository, _userProfileRepository,
            _productClient, _outboxEventRepository, metrics, logger, cacheService);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnCachedRecommendations_When_ExistingPersonalizedFound()
    {
        // Arrange
        var existing = new List<Recommendation>
        {
            new()
            {
                UserId = "user-1", Type = "PERSONALIZED",
                ProductIdsJson = JsonSerializer.Serialize(new[] { "p-1", "p-2" }),
                Score = 0.85m, Reason = "おすすめ"
            }
        };
        _recommendationRepository.FindByUserIdAndTypeAsync("user-1", "PERSONALIZED", Arg.Any<CancellationToken>())
            .Returns(existing);

        // Act
        var result = await _sut.GetPersonalizedAsync("user-1");

        // Assert
        result.Count.ShouldBe(1);
        result[0].Type.ShouldBe("PERSONALIZED");
        result[0].ProductIds.Count.ShouldBe(2);
        await _productClient.DidNotReceive().SearchProductsAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<decimal?>(), Arg.Any<decimal?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_GenerateNewRecommendations_When_NoExistingFound()
    {
        // Arrange
        _recommendationRepository.FindByUserIdAndTypeAsync("user-1", "PERSONALIZED", Arg.Any<CancellationToken>())
            .Returns(new List<Recommendation>());
        _userProfileRepository.GetOrCreateAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(new UserProfile { UserId = "user-1" });
        var products = new List<ProductSearchResult>
        {
            new("p-1", "スキー板A", 50000m, "スキー", null),
            new("p-2", "スキー板B", 60000m, "スキー", null)
        };
        _productClient.SearchProductsAsync("スキー 人気", Arg.Any<string?>(), Arg.Any<decimal?>(), Arg.Any<decimal?>(), Arg.Any<CancellationToken>())
            .Returns(products);

        // Act
        var result = await _sut.GetPersonalizedAsync("user-1");

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldAllBe(r => r.Type == "PERSONALIZED");
        await _recommendationRepository.Received(2).AddAsync(Arg.Any<Recommendation>(), Arg.Any<CancellationToken>());
        await _outboxEventRepository.Received(1).AddAsync(Arg.Any<OutboxEvent>(), Arg.Any<CancellationToken>());
        await _recommendationRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnSimilarProducts_When_ProductExists()
    {
        // Arrange
        var product = new ProductDetail("p-1", "スキー板A", "説明", 50000m, "スキー", null, 10);
        _productClient.GetProductByIdAsync("p-1", Arg.Any<CancellationToken>()).Returns(product);

        var similar = new List<ProductSearchResult>
        {
            new("p-1", "スキー板A", 50000m, "スキー", null),
            new("p-2", "スキー板B", 60000m, "スキー", null),
            new("p-3", "スキー板C", 70000m, "スキー", null)
        };
        _productClient.SearchProductsAsync("スキー", null, null, null, Arg.Any<CancellationToken>())
            .Returns(similar);

        // Act
        var result = await _sut.GetSimilarProductsAsync("p-1");

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldAllBe(r => r.Type == "SIMILAR");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnEmptyList_When_ProductNotFound()
    {
        // Arrange
        _productClient.GetProductByIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((ProductDetail?)null);

        // Act
        var result = await _sut.GetSimilarProductsAsync("nonexistent");

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnTrending_When_Called()
    {
        // Arrange
        var products = new List<ProductSearchResult>
        {
            new("p-1", "人気商品A", 30000m, "スキー", null),
            new("p-2", "人気商品B", 40000m, "スキー", null)
        };
        _productClient.SearchProductsAsync("人気 トレンド", null, null, null, Arg.Any<CancellationToken>())
            .Returns(products);

        // Act
        var result = await _sut.GetTrendingAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Type.ShouldBe("TRENDING");
        result[0].ProductIds.Count.ShouldBe(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnSeasonal_When_Called()
    {
        // Arrange
        _productClient.SearchProductsAsync(Arg.Any<string>(), null, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<ProductSearchResult>
            {
                new("p-1", "シーズン商品", 25000m, "スキー", null)
            });

        // Act
        var result = await _sut.GetSeasonalAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Type.ShouldBe("SEASONAL");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_RecordFeedback_When_RecommendationExists()
    {
        // Arrange
        var recommendation = new Recommendation
        {
            Id = "rec-1", UserId = "user-1", Type = "PERSONALIZED",
            ProductIdsJson = "[]", IsViewed = false
        };
        _recommendationRepository.FindByIdAsync("rec-1", Arg.Any<CancellationToken>())
            .Returns(recommendation);

        var request = new RecommendationFeedbackRequest("rec-1", "CLICK", "p-1");

        // Act
        await _sut.RecordFeedbackAsync("user-1", request);

        // Assert
        recommendation.IsViewed.ShouldBeTrue();
        await _recommendationRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowNotFoundException_When_RecommendationNotFound()
    {
        // Arrange
        _recommendationRepository.FindByIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((Recommendation?)null);

        var request = new RecommendationFeedbackRequest("nonexistent", "CLICK");

        // Act
        var act = async () => await _sut.RecordFeedbackAsync("user-1", request);

        // Assert
        var ex = await Should.ThrowAsync<NotFoundException>(act);
        ex.Message.ShouldContain("nonexistent");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowForbiddenException_When_FeedbackOnOtherUsersRecommendation()
    {
        // Arrange
        var recommendation = new Recommendation
        {
            Id = "rec-1", UserId = "other-user", Type = "PERSONALIZED",
            ProductIdsJson = "[]", IsViewed = false
        };
        _recommendationRepository.FindByIdAsync("rec-1", Arg.Any<CancellationToken>())
            .Returns(recommendation);

        var request = new RecommendationFeedbackRequest("rec-1", "CLICK", "p-1");

        // Act
        var act = async () => await _sut.RecordFeedbackAsync("user-1", request);

        // Assert
        await Should.ThrowAsync<ForbiddenException>(act);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_AllowFeedback_When_RecommendationIsSystemOwned()
    {
        // Arrange
        var recommendation = new Recommendation
        {
            Id = "rec-1", UserId = "system", Type = "TRENDING",
            ProductIdsJson = "[]", IsViewed = false
        };
        _recommendationRepository.FindByIdAsync("rec-1", Arg.Any<CancellationToken>())
            .Returns(recommendation);

        var request = new RecommendationFeedbackRequest("rec-1", "CLICK", "p-1");

        // Act
        await _sut.RecordFeedbackAsync("user-1", request);

        // Assert
        recommendation.IsViewed.ShouldBeTrue();
        await _recommendationRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];
        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(options);
            _meters.Add(meter);
            return meter;
        }
        public void Dispose()
        {
            foreach (var meter in _meters) meter.Dispose();
            _meters.Clear();
        }
    }
}
