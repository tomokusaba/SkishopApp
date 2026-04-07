using AiSupportService.Models;
using AiSupportService.Repositories.Interfaces;
using AiSupportService.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AiSupportService.Tests.Services;

public class AiAnalyticsServiceTests
{
    private readonly ISearchAnalyticsRepository _searchAnalyticsRepository;
    private readonly IRecommendationRepository _recommendationRepository;
    private readonly IChatSessionRepository _chatSessionRepository;
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly AiAnalyticsService _sut;

    public AiAnalyticsServiceTests()
    {
        _searchAnalyticsRepository = Substitute.For<ISearchAnalyticsRepository>();
        _recommendationRepository = Substitute.For<IRecommendationRepository>();
        _chatSessionRepository = Substitute.For<IChatSessionRepository>();
        _chatMessageRepository = Substitute.For<IChatMessageRepository>();
        var logger = Substitute.For<ILogger<AiAnalyticsService>>();

        _sut = new AiAnalyticsService(
            _searchAnalyticsRepository, _recommendationRepository,
            _chatSessionRepository, _chatMessageRepository, logger);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnSearchAnalytics_When_DataExists()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;
        _searchAnalyticsRepository.CountByDateRangeAsync(from, to, Arg.Any<CancellationToken>()).Returns(3);
        _searchAnalyticsRepository.CountDistinctUsersByDateRangeAsync(from, to, Arg.Any<CancellationToken>()).Returns(2);
        _searchAnalyticsRepository.AverageResponseTimeByDateRangeAsync(from, to, Arg.Any<CancellationToken>()).Returns(123.5);
        _searchAnalyticsRepository.GetTopQueriesByDateRangeAsync(from, to, 10, Arg.Any<CancellationToken>())
            .Returns(new List<(string Query, int Count)> { ("スキー板", 5), ("ブーツ", 3) });
        _searchAnalyticsRepository.GetSearchTypeDistributionByDateRangeAsync(from, to, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, int> { { "KEYWORD", 3 } });

        // Act
        var result = await _sut.GetSearchAnalyticsAsync(from, to);

        // Assert
        result.ShouldNotBeNull();
        result.TotalSearches.ShouldBe(3);
        result.UniqueUsers.ShouldBe(2);
        result.AvgResponseTimeMs.ShouldBe(123.5);
        result.TopQueries.Count.ShouldBe(2);
        result.TopQueries[0].Query.ShouldBe("スキー板");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnZeroDefaults_When_NoSearchData()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;
        _searchAnalyticsRepository.CountByDateRangeAsync(from, to, Arg.Any<CancellationToken>()).Returns(0);
        _searchAnalyticsRepository.CountDistinctUsersByDateRangeAsync(from, to, Arg.Any<CancellationToken>()).Returns(0);
        _searchAnalyticsRepository.AverageResponseTimeByDateRangeAsync(from, to, Arg.Any<CancellationToken>()).Returns(0.0);
        _searchAnalyticsRepository.GetTopQueriesByDateRangeAsync(from, to, 10, Arg.Any<CancellationToken>())
            .Returns(new List<(string Query, int Count)>());
        _searchAnalyticsRepository.GetSearchTypeDistributionByDateRangeAsync(from, to, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, int>());

        // Act
        var result = await _sut.GetSearchAnalyticsAsync(from, to);

        // Assert
        result.TotalSearches.ShouldBe(0);
        result.UniqueUsers.ShouldBe(0);
        result.AvgResponseTimeMs.ShouldBe(0);
        result.TopQueries.Count.ShouldBe(0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnRecommendationAnalytics_When_DataExists()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;
        _recommendationRepository.CountByDateRangeAsync(from, to, Arg.Any<CancellationToken>()).Returns(3);
        _recommendationRepository.CountViewedByDateRangeAsync(from, to, Arg.Any<CancellationToken>()).Returns(2);
        _recommendationRepository.GetTypeDistributionByDateRangeAsync(from, to, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, int> { { "PERSONALIZED", 2 }, { "TRENDING", 1 } });

        // Act
        var result = await _sut.GetRecommendationAnalyticsAsync(from, to);

        // Assert
        result.ShouldNotBeNull();
        result.TotalRecommendations.ShouldBe(3);
        result.ViewedCount.ShouldBe(2);
        result.ViewRate.ShouldBeGreaterThan(0);
        result.TypeDistribution.ShouldContainKey("PERSONALIZED");
        result.TypeDistribution["PERSONALIZED"].ShouldBe(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnChatAnalytics_When_DataExists()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;
        _chatSessionRepository.CountByDateRangeAsync(from, to, Arg.Any<CancellationToken>()).Returns(3);
        _chatMessageRepository.CountByDateRangeAsync(from, to, Arg.Any<CancellationToken>()).Returns(15);
        _chatSessionRepository.CountByDateRangeAndStatusAsync(from, to, "ESCALATED", Arg.Any<CancellationToken>()).Returns(1);
        _chatSessionRepository.GetStatusDistributionByDateRangeAsync(from, to, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, int> { { "ACTIVE", 1 }, { "CLOSED", 1 }, { "ESCALATED", 1 } });

        // Act
        var result = await _sut.GetChatAnalyticsAsync(from, to);

        // Assert
        result.ShouldNotBeNull();
        result.TotalSessions.ShouldBe(3);
        result.TotalMessages.ShouldBe(15);
        result.AvgMessagesPerSession.ShouldBe(5.0);
        result.EscalatedSessions.ShouldBe(1);
        result.StatusDistribution.ShouldContainKey("ESCALATED");
    }
}
