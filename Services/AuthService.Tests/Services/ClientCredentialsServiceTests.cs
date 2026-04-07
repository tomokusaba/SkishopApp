using AuthService.Configurations;
using AuthService.DTOs.Requests;
using AuthService.Exceptions;
using AuthService.Models;
using AuthService.Repositories.Interfaces;
using AuthService.Services;
using AuthService.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace AuthService.Tests.Services;

public class ClientCredentialsServiceTests
{
    private readonly IOAuthClientRepository _oAuthClientRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ISecurityService _securityService;
    private readonly ILogger<ClientCredentialsService> _logger;
    private readonly ClientCredentialsService _sut;

    public ClientCredentialsServiceTests()
    {
        _oAuthClientRepository = Substitute.For<IOAuthClientRepository>();
        _passwordHasher = Substitute.For<IPasswordHasher<User>>();
        _securityService = Substitute.For<ISecurityService>();
        _logger = Substitute.For<ILogger<ClientCredentialsService>>();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "ThisIsAVeryLongSecretKeyForTestingPurposesOnly1234567890"
            })
            .Build();

        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));

        var jwtSettings = Options.Create(new JwtSettings
        {
            Issuer = "https://skishop.test",
            Audience = "https://skishop.test",
            SecretKey = "ThisIsAVeryLongSecretKeyForTestingPurposesOnly1234567890",
            AccessExpirationSeconds = 3600
        });

        _sut = new ClientCredentialsService(
            _oAuthClientRepository,
            _passwordHasher,
            _securityService,
            timeProvider,
            jwtSettings,
            _logger);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_IssueToken_When_ValidCredentials()
    {
        // Arrange
        var request = new ClientCredentialsRequest("client-id", "client-secret", "read write", "client_credentials");
        var client = new OAuthClient
        {
            ClientId = "client-id",
            ClientSecretHash = "hashed-secret",
            Name = "Test Client",
            AllowedScopes = "read,write",
            IsActive = true
        };

        _oAuthClientRepository.FindByClientIdAsync("client-id", Arg.Any<CancellationToken>())
            .Returns(client);
        _passwordHasher.VerifyHashedPassword(Arg.Any<User>(), "hashed-secret", "client-secret")
            .Returns(PasswordVerificationResult.Success);

        // Act
        var result = await _sut.IssueTokenAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.AccessToken.ShouldNotBeNullOrEmpty();
        result.TokenType.ShouldBe("Bearer");
        result.ExpiresIn.ShouldBe(3600);
        result.Scope.ShouldBe("read write");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowUnauthorizedException_When_InvalidClientSecret()
    {
        // Arrange
        var request = new ClientCredentialsRequest("client-id", "wrong-secret", "read", "client_credentials");
        var client = new OAuthClient
        {
            ClientId = "client-id",
            ClientSecretHash = "hashed-secret",
            Name = "Test Client",
            AllowedScopes = "read",
            IsActive = true
        };

        _oAuthClientRepository.FindByClientIdAsync("client-id", Arg.Any<CancellationToken>())
            .Returns(client);
        _passwordHasher.VerifyHashedPassword(Arg.Any<User>(), "hashed-secret", "wrong-secret")
            .Returns(PasswordVerificationResult.Failed);

        // Act
        var act = async () => await _sut.IssueTokenAsync(request);

        // Assert
        var ex = await Should.ThrowAsync<UnauthorizedException>(act);
        ex.Message.ShouldContain("無効なクライアント認証情報です");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_InvalidGrantType()
    {
        // Arrange
        var request = new ClientCredentialsRequest("client-id", "secret", "read", "authorization_code");

        // Act
        var act = async () => await _sut.IssueTokenAsync(request);

        // Assert
        var ex = await Should.ThrowAsync<BusinessException>(act);
        ex.Message.ShouldContain("サポートされていないグラントタイプです");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowUnauthorizedException_When_ClientNotFound()
    {
        // Arrange
        var request = new ClientCredentialsRequest("unknown-client", "secret", "read", "client_credentials");
        _oAuthClientRepository.FindByClientIdAsync("unknown-client", Arg.Any<CancellationToken>())
            .Returns((OAuthClient?)null);

        // Act
        var act = async () => await _sut.IssueTokenAsync(request);

        // Assert
        var ex = await Should.ThrowAsync<UnauthorizedException>(act);
        ex.Message.ShouldContain("無効なクライアント認証情報です");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_ScopeNotAllowed()
    {
        // Arrange
        var request = new ClientCredentialsRequest("client-id", "client-secret", "admin", "client_credentials");
        var client = new OAuthClient
        {
            ClientId = "client-id",
            ClientSecretHash = "hashed-secret",
            Name = "Test Client",
            AllowedScopes = "read,write",
            IsActive = true
        };

        _oAuthClientRepository.FindByClientIdAsync("client-id", Arg.Any<CancellationToken>())
            .Returns(client);
        _passwordHasher.VerifyHashedPassword(Arg.Any<User>(), "hashed-secret", "client-secret")
            .Returns(PasswordVerificationResult.Success);

        // Act
        var act = async () => await _sut.IssueTokenAsync(request);

        // Assert
        var ex = await Should.ThrowAsync<BusinessException>(act);
        ex.Message.ShouldContain("許可されていません");
    }
}
