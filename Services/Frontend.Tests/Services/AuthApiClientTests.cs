using Frontend.DTOs;
using Frontend.Services;
using Frontend.Services.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Frontend.Tests.Services;

[Trait("Category", "Unit")]
public class AuthApiClientTests
{
    private readonly IApiGatewayClient _apiClient;
    private readonly ILogger<AuthApiClient> _logger;
    private readonly AuthApiClient _authClient;

    public AuthApiClientTests()
    {
        _apiClient = Substitute.For<IApiGatewayClient>();
        _logger = Substitute.For<ILogger<AuthApiClient>>();
        _authClient = new AuthApiClient(_apiClient, _logger);
    }

    [Fact]
    public async Task Should_ReturnToken_When_LoginSucceeds()
    {
        // Arrange
        var expectedResponse = new LoginResponse(
            AccessToken: "test-access-token",
            RefreshToken: "test-refresh-token",
            TokenType: "Bearer",
            ExpiresIn: 3600,
            User: new LoginUserInfo("user-1", "太郎", "田中", "User"));

        _apiClient.PostAsync<LoginRequest, LoginResponse>(
            "/api/v1/auth/login",
            Arg.Any<LoginRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        // Act
        var result = await _authClient.LoginAsync("test@example.com", "password123");

        // Assert
        result.ShouldNotBeNull();
        result.AccessToken.ShouldBe("test-access-token");
        result.User.Id.ShouldBe("user-1");
    }

    [Fact]
    public async Task Should_ThrowUnauthorized_When_LoginFails()
    {
        // Arrange
        _apiClient.PostAsync<LoginRequest, LoginResponse>(
            "/api/v1/auth/login",
            Arg.Any<LoginRequest>(),
            Arg.Any<CancellationToken>())
            .Returns((LoginResponse?)null);

        // Act & Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _authClient.LoginAsync("test@example.com", "wrong-password"));
        ex.Message.ShouldContain("デシリアライズに失敗");
    }

    [Fact]
    public async Task Should_CallCorrectEndpoint_When_RegisterCalled()
    {
        // Arrange
        var request = new RegisterRequest(
            Email: "new@example.com",
            Password: "securePass123",
            FirstName: "太郎",
            LastName: "田中",
            Username: "tanaka");

        _apiClient.PostAsync<RegisterRequest, object>(
            "/api/v1/auth/users",
            Arg.Any<RegisterRequest>(),
            Arg.Any<CancellationToken>())
            .Returns((object?)null);

        // Act
        await _authClient.RegisterAsync(request);

        // Assert
        await _apiClient.Received(1).PostAsync<RegisterRequest, object>(
            "/api/v1/auth/users",
            Arg.Is<RegisterRequest>(r => r.Email == "new@example.com"),
            Arg.Any<CancellationToken>());
    }
}
