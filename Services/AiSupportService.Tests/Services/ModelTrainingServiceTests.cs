using AiSupportService.Models;
using AiSupportService.Repositories.Interfaces;
using AiSupportService.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AiSupportService.Tests.Services;

public class ModelTrainingServiceTests
{
    private readonly IModelTrainingRepository _modelTrainingRepository;
    private readonly ModelTrainingService _sut;

    public ModelTrainingServiceTests()
    {
        _modelTrainingRepository = Substitute.For<IModelTrainingRepository>();
        var logger = Substitute.For<ILogger<ModelTrainingService>>();

        _sut = new ModelTrainingService(_modelTrainingRepository, logger);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnAllTrainings_When_Called()
    {
        // Arrange
        var trainings = new List<ModelTraining>
        {
            new() { ModelName = "recommendation-model", Status = "COMPLETED", ModelVersion = "v1.0" },
            new() { ModelName = "search-model", Status = "PENDING", ModelVersion = "v2.0" }
        };
        _modelTrainingRepository.FindAllAsync(Arg.Any<CancellationToken>()).Returns(trainings);

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Count.ShouldBe(2);
        result[0].ModelName.ShouldBe("recommendation-model");
        result[0].Status.ShouldBe("COMPLETED");
        result[1].ModelName.ShouldBe("search-model");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_StartTraining_When_ValidModelNameProvided()
    {
        // Arrange
        var modelName = "recommendation-model";

        // Act
        var result = await _sut.StartTrainingAsync(modelName, "admin-1");

        // Assert
        result.ShouldNotBeNull();
        result.ModelName.ShouldBe("recommendation-model");
        result.Status.ShouldBe("PENDING");
        result.ModelVersion.ShouldNotBeNull();
        result.ModelVersion.ShouldStartWith("v");
        await _modelTrainingRepository.Received(1).AddAsync(Arg.Any<ModelTraining>(), Arg.Any<CancellationToken>());
        await _modelTrainingRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
