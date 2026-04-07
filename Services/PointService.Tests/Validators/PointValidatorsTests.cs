using FluentValidation.TestHelper;
using PointService.DTOs.Requests;
using PointService.Validators;
using Shouldly;

namespace PointService.Tests.Validators;

public class PointValidatorsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Should_FailValidation_When_AdjustPointsReasonIsEmpty()
    {
        // Arrange
        var validator = new AdjustPointsRequestValidator();
        var request = new AdjustPointsRequest(10, string.Empty);

        // Act
        var result = validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_FailValidation_When_PaginationPageIsLessThanOne()
    {
        // Arrange
        var validator = new PaginationQueryValidator();
        var query = new PaginationQuery(0, 20);

        // Act
        var result = validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_PassValidation_When_UpdateTierRequestIsWithinRange()
    {
        // Arrange
        var validator = new UpdateTierRequestValidator();
        var request = new UpdateTierRequest(0.05m, 1000, "priority support");

        // Act
        var result = validator.TestValidate(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
