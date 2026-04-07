using Xunit;
using AiSupportService.DTOs.Requests;
using AiSupportService.Validators;
using Shouldly;

namespace AiSupportService.Tests.Validators;

public class ValidatorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_ValidSendMessageRequest()
    {
        // Arrange
        var validator = new SendMessageRequestValidator();
        var request = new SendMessageRequest("スキーブーツのおすすめは？");

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Should_FailValidation_When_MessageIsEmpty(string? message)
    {
        // Arrange
        var validator = new SendMessageRequestValidator();
        var request = new SendMessageRequest(message!);

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Message");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_MessageExceedsMaxLength()
    {
        // Arrange
        var validator = new SendMessageRequestValidator();
        var request = new SendMessageRequest(new string('あ', 4001));

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_ValidSearchRequest()
    {
        // Arrange
        var validator = new SearchRequestValidator();
        var request = new SearchRequest("スキーブーツ", null, null, null, 1, 20);

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_SearchQueryIsEmpty()
    {
        // Arrange
        var validator = new SearchRequestValidator();
        var request = new SearchRequest("", null, null, null, 1, 20);

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_PageSizeExceedsMax()
    {
        // Arrange
        var validator = new SearchRequestValidator();
        var request = new SearchRequest("スキー", null, null, null, 1, 200);

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("CLICK", true)]
    [InlineData("PURCHASE", true)]
    [InlineData("INVALID", false)]
    [InlineData("", false)]
    public async Task Should_ValidateRecommendationFeedback_Correctly(string feedbackType, bool expectedValid)
    {
        // Arrange
        var validator = new RecommendationFeedbackRequestValidator();
        var request = new RecommendationFeedbackRequest("rec-1", feedbackType, "prod-1");

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBe(expectedValid);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_ValidSearchFeedback()
    {
        // Arrange
        var validator = new SearchFeedbackRequestValidator();
        var request = new SearchFeedbackRequest("search-1", "prod-1");

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_SearchFeedbackMissingSearchId()
    {
        // Arrange
        var validator = new SearchFeedbackRequestValidator();
        var request = new SearchFeedbackRequest("", "prod-1");

        // Act
        var result = await validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
    }
}
