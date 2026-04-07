using AuthService.Configurations;
using AuthService.Enums;
using AuthService.Infrastructure.Cache;
using AuthService.Models;
using AuthService.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace AuthService.Tests.Services;

public class JwtTokenServiceTests
{
    private readonly JwtTokenService _sut;

    public JwtTokenServiceTests()
    {
        var jwtSettings = Options.Create(new JwtSettings
        {
            Issuer = "https://skishop.test",
            Audience = "https://skishop.test",
            AccessExpirationSeconds = 3600,
            RefreshExpirationSeconds = 604800,
            MaxActiveRefreshTokens = 10
        });

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "ThisIsAVeryLongSecretKeyForTestingPurposesOnly1234567890"
            })
            .Build();

        var logger = Substitute.For<ILogger<JwtTokenService>>();
        var tokenBlacklistService = Substitute.For<ITokenBlacklistService>();

        _sut = new JwtTokenService(jwtSettings, configuration, TimeProvider.System, tokenBlacklistService, logger);
    }

    private static User CreateTestUser() => new()
    {
        Id = "user-123",
        Email = "jwt@example.com",
        Username = "jwtuser",
        Role = UserRoleType.User,
        Status = UserStatus.Active,
        IsActive = true
    };

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_GenerateAccessToken_When_ValidUserProvided()
    {
        // Arrange
        var user = CreateTestUser();
        var sessionId = "session-456";

        // Act
        var token = _sut.GenerateAccessToken(user, sessionId);

        // Assert
        token.ShouldNotBeNullOrEmpty();
        token.Split('.').Length.ShouldBe(3);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_GenerateUniqueRefreshTokens_When_CalledMultipleTimes()
    {
        // Arrange & Act
        var token1 = _sut.GenerateRefreshToken();
        var token2 = _sut.GenerateRefreshToken();

        // Assert
        token1.ShouldNotBeNullOrEmpty();
        token2.ShouldNotBeNullOrEmpty();
        token1.ShouldNotBe(token2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ValidateToken_When_ValidTokenProvided()
    {
        // Arrange
        var user = CreateTestUser();
        var token = _sut.GenerateAccessToken(user, "session-789");

        // Act
        var result = await _sut.ValidateTokenAsync(token);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.UserId.ShouldBe("user-123");
        result.Role.ShouldBe("USER");
        result.ExpiresAt.ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_GenerateRefreshToken_WithSufficientLength()
    {
        // Arrange & Act
        var token = _sut.GenerateRefreshToken();

        // Assert
        token.ShouldNotBeNullOrEmpty();
        var bytes = Convert.FromBase64String(token);
        bytes.Length.ShouldBe(32);
    }
}
