using System.Diagnostics.Metrics;
using AiSupportService.DTOs.Requests;
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

public class SearchServiceTests
{
    private readonly IProductClient _productClient;
    private readonly ISearchAnalyticsRepository _analyticsRepository;
    private readonly SearchService _sut;

    public SearchServiceTests()
    {
        _productClient = Substitute.For<IProductClient>();
        _analyticsRepository = Substitute.For<ISearchAnalyticsRepository>();
        var meterFactory = new TestMeterFactory();
        var metrics = new AiSupportMetrics(meterFactory);
        var logger = Substitute.For<ILogger<SearchService>>();

        var cacheService = Substitute.For<ICacheService>();
        _sut = new SearchService(_productClient, _analyticsRepository, metrics, logger, cacheService);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnResults_When_ValidQueryProvided()
    {
        // Arrange
        var request = new SearchRequest("スキー板");
        var products = new List<ProductSearchResult>
        {
            new("p-1", "スキー板A", 50000m, "スキー", null),
            new("p-2", "スキー板B", 60000m, "スキー", null)
        };
        _productClient.SearchProductsAsync("スキー板", null, null, null, Arg.Any<CancellationToken>())
            .Returns(products);

        // Act
        var result = await _sut.SearchAsync(request, "user-1");

        // Assert
        result.ShouldNotBeNull();
        result.Query.ShouldBe("スキー板");
        result.TotalCount.ShouldBe(2);
        result.Items.Count.ShouldBe(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_RecordAnalytics_When_SearchExecuted()
    {
        // Arrange
        var request = new SearchRequest("ブーツ");
        _productClient.SearchProductsAsync("ブーツ", null, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<ProductSearchResult>());

        // Act
        await _sut.SearchAsync(request, "user-1");

        // Assert
        await _analyticsRepository.Received(1).AddAsync(Arg.Any<SearchAnalytics>(), Arg.Any<CancellationToken>());
        await _analyticsRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_RecordFeedback_When_ValidFeedbackProvided()
    {
        // Arrange
        var request = new SearchFeedbackRequest("search-1", "product-1");

        // Act
        await _sut.RecordFeedbackAsync(request, "user-1");

        // Assert
        await _analyticsRepository.Received(1).AddAsync(
            Arg.Is<SearchAnalytics>(a => a.Query.Contains("feedback:search-1")),
            Arg.Any<CancellationToken>());
        await _analyticsRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnSuggestions_When_QueryProvided()
    {
        // Arrange
        var products = new List<ProductSearchResult>
        {
            new("p-1", "スキー板A", 50000m, "スキー", null),
            new("p-2", "スキー板B", 60000m, "スキー", null),
            new("p-3", "スキー板C", 70000m, "スキー", null)
        };
        _productClient.SearchProductsAsync("スキー", null, null, null, Arg.Any<CancellationToken>())
            .Returns(products);

        // Act
        var result = await _sut.GetSuggestionsAsync("スキー");

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result[0].ShouldBe("スキー板A");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PaginateResults_When_PageAndPageSizeProvided()
    {
        // Arrange — 5 products, request page 2 with pageSize 2
        var products = Enumerable.Range(1, 5)
            .Select(i => new ProductSearchResult($"p-{i}", $"Product{i}", i * 10000m, "スキー", null))
            .ToList();
        _productClient.SearchProductsAsync("スキー板", null, null, null, Arg.Any<CancellationToken>())
            .Returns(products);

        var request = new SearchRequest("スキー板", Page: 2, PageSize: 2);

        // Act
        var result = await _sut.SearchAsync(request, "user-1");

        // Assert
        result.TotalCount.ShouldBe(5);
        result.Items.Count.ShouldBe(2);
        result.Items[0].ProductId.ShouldBe("p-3"); // skip 2, take 2 → p-3, p-4
        result.Items[1].ProductId.ShouldBe("p-4");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnLastPagePartial_When_PageExceedsEvenDivision()
    {
        // Arrange — 5 products, request page 3 with pageSize 2
        var products = Enumerable.Range(1, 5)
            .Select(i => new ProductSearchResult($"p-{i}", $"Product{i}", i * 10000m, "スキー", null))
            .ToList();
        _productClient.SearchProductsAsync("スキー板", null, null, null, Arg.Any<CancellationToken>())
            .Returns(products);

        var request = new SearchRequest("スキー板", Page: 3, PageSize: 2);

        // Act
        var result = await _sut.SearchAsync(request, "user-1");

        // Assert
        result.TotalCount.ShouldBe(5);
        result.Items.Count.ShouldBe(1); // skip 4, take 2 → only p-5
        result.Items[0].ProductId.ShouldBe("p-5");
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
