using System.Diagnostics.Metrics;
using AiSupportService.DTOs.Requests;
using AiSupportService.Infrastructure.Metrics;
using AiSupportService.Models;
using AiSupportService.Repositories.Interfaces;
using AiSupportService.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AiSupportService.Tests.Services;

public class ForecastServiceTests
{
    private readonly IDemandForecastRepository _forecastRepository;
    private readonly ForecastService _sut;

    public ForecastServiceTests()
    {
        _forecastRepository = Substitute.For<IDemandForecastRepository>();
        var meterFactory = new TestMeterFactory();
        var metrics = new AiSupportMetrics(meterFactory);
        var logger = Substitute.For<ILogger<ForecastService>>();

        _sut = new ForecastService(_forecastRepository, metrics, logger);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_GenerateForecast_When_ValidRequestProvided()
    {
        // Arrange
        var request = new GenerateForecastRequest("product-1", "MONTHLY", "SKU-001");
        _forecastRepository.FindByProductIdRecentAsync("product-1", 10, Arg.Any<CancellationToken>())
            .Returns(new List<DemandForecast>());

        // Act
        var result = await _sut.GenerateAsync(request, "admin-1");

        // Assert
        result.ShouldNotBeNull();
        result.ProductId.ShouldBe("product-1");
        result.ForecastPeriod.ShouldBe("MONTHLY");
        result.Sku.ShouldBe("SKU-001");
        result.ModelVersion.ShouldBe("moving-average-v1");
        await _forecastRepository.Received(1).AddAsync(Arg.Any<DemandForecast>(), Arg.Any<CancellationToken>());
        await _forecastRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnForecasts_When_ProductIdProvided()
    {
        // Arrange
        var forecasts = new List<DemandForecast>
        {
            new() { ProductId = "product-1", ForecastPeriod = "WEEKLY", PredictedDemand = 50 },
            new() { ProductId = "product-1", ForecastPeriod = "MONTHLY", PredictedDemand = 200 }
        };
        _forecastRepository.FindByProductIdAsync("product-1", Arg.Any<CancellationToken>()).Returns(forecasts);

        // Act
        var result = await _sut.GetByProductIdAsync("product-1");

        // Assert
        result.Count.ShouldBe(2);
        result[0].ProductId.ShouldBe("product-1");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnAllForecasts_When_Called()
    {
        // Arrange
        var forecasts = new List<DemandForecast>
        {
            new() { ProductId = "p-1", ForecastPeriod = "WEEKLY", PredictedDemand = 50 },
            new() { ProductId = "p-2", ForecastPeriod = "MONTHLY", PredictedDemand = 200 },
            new() { ProductId = "p-3", ForecastPeriod = "QUARTERLY", PredictedDemand = 600 }
        };
        _forecastRepository.FindAllAsync(Arg.Any<CancellationToken>()).Returns(forecasts);

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Count.ShouldBe(3);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_SetCorrectDemand_When_WeeklyPeriod()
    {
        // Arrange
        var request = new GenerateForecastRequest("product-1", "WEEKLY");
        _forecastRepository.FindByProductIdRecentAsync("product-1", 10, Arg.Any<CancellationToken>())
            .Returns(new List<DemandForecast>());

        // Act
        var result = await _sut.GenerateAsync(request, "admin-1");

        // Assert
        result.PredictedDemand.ShouldBe(50);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_SetCorrectDemand_When_MonthlyPeriod()
    {
        // Arrange
        var request = new GenerateForecastRequest("product-1", "MONTHLY");
        _forecastRepository.FindByProductIdRecentAsync("product-1", 10, Arg.Any<CancellationToken>())
            .Returns(new List<DemandForecast>());

        // Act
        var result = await _sut.GenerateAsync(request, "admin-1");

        // Assert
        result.PredictedDemand.ShouldBe(200);
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
