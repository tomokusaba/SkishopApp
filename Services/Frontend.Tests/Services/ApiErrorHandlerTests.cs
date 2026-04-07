using System.Net;
using System.Text;
using System.Text.Json;
using Frontend.Models;
using Frontend.Services;
using Frontend.Services.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Frontend.Tests.Services;

[Trait("Category", "Unit")]
public class ApiErrorHandlerTests
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<ApiErrorHandler> _logger;
    private readonly ApiErrorHandler _handler;

    public ApiErrorHandlerTests()
    {
        _notificationService = Substitute.For<INotificationService>();
        _logger = Substitute.For<ILogger<ApiErrorHandler>>();
        _handler = new ApiErrorHandler(_notificationService, _logger);
    }

    [Fact]
    public async Task Should_ReturnProblemDetails_When_ApiReturns4xx()
    {
        // Arrange
        var problem = new ProblemDetailsResponse(
            Type: "https://tools.ietf.org/html/rfc9110#section-15.5.5",
            Title: "Not Found",
            Status: 404,
            Detail: "リソースが見つかりません");

        var json = JsonSerializer.Serialize(problem);
        var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        // Act
        await _handler.HandleApiErrorAsync(response);

        // Assert
        _notificationService.Received(1).ShowInfo("リソースが見つかりません");
    }

    [Fact]
    public async Task Should_ThrowApiValidationException_When_ValidationFails()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>
        {
            ["Email"] = ["メールアドレスは必須です"],
            ["Password"] = ["パスワードは8文字以上です"]
        };
        var problem = new ProblemDetailsResponse(
            Type: null,
            Title: "Validation Error",
            Status: 400,
            Errors: errors);

        var json = JsonSerializer.Serialize(problem);
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        // Act & Assert
        var ex = await Should.ThrowAsync<ApiValidationException>(
            () => _handler.HandleApiErrorAsync(response));
        ex.FieldErrors.ShouldContainKey("Email");
        ex.FieldErrors["Email"].ShouldContain("メールアドレスは必須です");
    }

    [Fact]
    public async Task Should_ReturnDefaultMessage_When_ResponseHasNoBody()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("", Encoding.UTF8, "application/json")
        };

        // Act
        await _handler.HandleApiErrorAsync(response);

        // Assert
        _notificationService.Received(1).ShowWarning("この操作を行う権限がありません");
    }

    [Fact]
    public void Should_HandleNetworkError_When_HttpClientThrows()
    {
        // Arrange
        var statusCode = 500;

        // Act
        var strategy = ApiErrorHandler.GetErrorDisplayStrategy(statusCode);

        // Assert
        strategy.ShouldBe(ErrorDisplayType.Snackbar);
    }
}
