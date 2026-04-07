using AuthService.Configurations;
using AuthService.DTOs.Requests;
using AuthService.DTOs.Responses;
using AuthService.Enums;
using AuthService.Exceptions;
using AuthService.Models;
using AuthService.Repositories.Interfaces;
using AuthService.Services;
using AuthService.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace AuthService.Tests.Services;

public class AuthServiceTests
{
    private readonly IUserRepository _userRepository;
    private readonly IUserSessionRepository _userSessionRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ISecurityService _securityService;
    private readonly IOutboxEventRepository _outboxEventRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ILogger<AuthServiceImpl> _logger;
    private readonly AuthServiceImpl _sut;

    public AuthServiceTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _userSessionRepository = Substitute.For<IUserSessionRepository>();
        _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
        _jwtTokenService = Substitute.For<IJwtTokenService>();
        _securityService = Substitute.For<ISecurityService>();
        _outboxEventRepository = Substitute.For<IOutboxEventRepository>();
        _passwordHasher = Substitute.For<IPasswordHasher<User>>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
        _logger = Substitute.For<ILogger<AuthServiceImpl>>();

        var authSettings = Options.Create(new AuthSettings
        {
            MaxFailedAttempts = 5,
            AutoUnlockMinutes = 30,
            FailedAttemptResetMinutes = 15
        });

        var sessionSettings = Options.Create(new SessionSettings
        {
            Timeout = 1800,
            MaxConcurrentSessions = 3
        });

        _sut = new AuthServiceImpl(
            _userRepository,
            _userSessionRepository,
            _refreshTokenRepository,
            _jwtTokenService,
            _securityService,
            Substitute.For<IMfaService>(),
            _outboxEventRepository,
            _passwordHasher,
            _timeProvider,
            authSettings,
            sessionSettings,
            _logger);
    }

    private static User CreateTestUser(UserStatus? status = null, bool mfaEnabled = false) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Email = "test@example.com",
        Username = "testuser",
        PasswordHash = "hashed-password",
        FirstName = "Test",
        LastName = "User",
        Status = status ?? UserStatus.Active,
        Role = UserRoleType.User,
        IsActive = true,
        IsAccountLocked = false,
        IsEmailVerified = true,
        FailedLoginAttempts = 0,
        Mfa = mfaEnabled ? new UserMfa { IsEnabled = true, SecretKey = "secret" } : null,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnLoginResponse_When_ValidCredentials()
    {
        // Arrange
        var user = CreateTestUser();
        var request = new LoginRequest("test@example.com", "ValidPassword1!");

        _userRepository.FindByEmailForLoginAsync("test@example.com", Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher.VerifyHashedPassword(user, user.PasswordHash!, request.Password)
            .Returns(PasswordVerificationResult.Success);
        _userSessionRepository.FindActiveByUserIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(new List<UserSession>().AsReadOnly());
        _jwtTokenService.GenerateAccessToken(user, Arg.Any<string>())
            .Returns("test-access-token");
        _jwtTokenService.GenerateRefreshToken()
            .Returns("test-refresh-token");

        // Act
        var result = await _sut.LoginAsync(request, "127.0.0.1", "TestAgent");

        // Assert
        result.ShouldNotBeNull();
        result.AccessToken.ShouldBe("test-access-token");
        result.RefreshToken.ShouldBe("test-refresh-token");
        result.TokenType.ShouldBe("Bearer");
        result.ExpiresIn.ShouldBe(3600);
        result.User.ShouldNotBeNull();
        result.User.Id.ShouldBe(user.Id);

        await _securityService.Received(1).ResetFailedAttemptsAsync(user.Id, Arg.Any<CancellationToken>());
        await _userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _outboxEventRepository.Received(1).AddAsync(Arg.Any<OutboxEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowUnauthorizedException_When_UserNotFound()
    {
        // Arrange
        var request = new LoginRequest("nonexistent@example.com", "Password123!");
        _userRepository.FindByEmailForLoginAsync("nonexistent@example.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var act = async () => await _sut.LoginAsync(request, "127.0.0.1", "TestAgent");

        // Assert
        var ex = await Should.ThrowAsync<UnauthorizedException>(act);
        ex.Message.ShouldContain("メールアドレスまたはパスワードが正しくありません");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowUnauthorizedException_When_InvalidPassword()
    {
        // Arrange
        var user = CreateTestUser();
        var request = new LoginRequest("test@example.com", "WrongPassword1!");

        _userRepository.FindByEmailForLoginAsync("test@example.com", Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher.VerifyHashedPassword(user, user.PasswordHash!, request.Password)
            .Returns(PasswordVerificationResult.Failed);
        _securityService.IncrementFailedAttemptsAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var act = async () => await _sut.LoginAsync(request, "127.0.0.1", "TestAgent");

        // Assert
        var ex = await Should.ThrowAsync<UnauthorizedException>(act);
        ex.Message.ShouldContain("メールアドレスまたはパスワードが正しくありません");
        await _securityService.Received(1).IncrementFailedAttemptsAsync(user.Id, Arg.Any<CancellationToken>());
        await _securityService.Received(1).LogSecurityEventAsync(
            user.Id, "LOGIN_FAILED", "127.0.0.1", "TestAgent", "パスワード不一致", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowAccountLockedException_When_AccountIsLocked()
    {
        // Arrange
        var user = CreateTestUser();
        user.IsAccountLocked = true;
        user.LockedAt = _timeProvider.GetUtcNow();

        var request = new LoginRequest("test@example.com", "Password123!");
        _userRepository.FindByEmailForLoginAsync("test@example.com", Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var act = async () => await _sut.LoginAsync(request, "127.0.0.1", "TestAgent");

        // Assert
        var ex = await Should.ThrowAsync<AccountLockedException>(act);
        ex.Message.ShouldContain("アカウントがロックされています");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_AccountInactive()
    {
        // Arrange
        var user = CreateTestUser(status: UserStatus.PendingVerification);
        var request = new LoginRequest("test@example.com", "Password123!");

        _userRepository.FindByEmailForLoginAsync("test@example.com", Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher.VerifyHashedPassword(user, user.PasswordHash!, request.Password)
            .Returns(PasswordVerificationResult.Success);

        // Act
        var act = async () => await _sut.LoginAsync(request, "127.0.0.1", "TestAgent");

        // Assert
        var ex = await Should.ThrowAsync<BusinessException>(act);
        ex.Message.ShouldContain("アカウントが有効ではありません");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowMfaRequiredException_When_MfaEnabled()
    {
        // Arrange
        var user = CreateTestUser(mfaEnabled: true);
        var request = new LoginRequest("test@example.com", "ValidPassword1!");

        _userRepository.FindByEmailForLoginAsync("test@example.com", Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher.VerifyHashedPassword(user, user.PasswordHash!, request.Password)
            .Returns(PasswordVerificationResult.Success);

        // Act
        var act = async () => await _sut.LoginAsync(request, "127.0.0.1", "TestAgent");

        // Assert
        var ex = await Should.ThrowAsync<MfaRequiredException>(act);
        ex.SessionToken.ShouldNotBeNullOrEmpty();
        await _userSessionRepository.Received(1).AddAsync(Arg.Any<UserSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_EvictOldestSession_When_MaxSessionsReached()
    {
        // Arrange
        var user = CreateTestUser();
        var request = new LoginRequest("test@example.com", "ValidPassword1!");

        _userRepository.FindByEmailForLoginAsync("test@example.com", Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher.VerifyHashedPassword(user, user.PasswordHash!, request.Password)
            .Returns(PasswordVerificationResult.Success);

        var existingSessions = new List<UserSession>
        {
            new() { Id = "s1", UserId = user.Id, IsActive = true, LastActivity = DateTimeOffset.UtcNow.AddHours(-3) },
            new() { Id = "s2", UserId = user.Id, IsActive = true, LastActivity = DateTimeOffset.UtcNow.AddHours(-1) },
            new() { Id = "s3", UserId = user.Id, IsActive = true, LastActivity = DateTimeOffset.UtcNow }
        };
        _userSessionRepository.FindActiveByUserIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(existingSessions.AsReadOnly());

        _jwtTokenService.GenerateAccessToken(user, Arg.Any<string>()).Returns("access-token");
        _jwtTokenService.GenerateRefreshToken().Returns("refresh-token");

        // Act
        var result = await _sut.LoginAsync(request, "127.0.0.1", "TestAgent");

        // Assert
        result.ShouldNotBeNull();
        await _userSessionRepository.Received(1).DeactivateSessionAsync("s1", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowAccountLockedException_When_InvalidPasswordCausesLockout()
    {
        // Arrange
        var user = CreateTestUser();
        var request = new LoginRequest("test@example.com", "WrongPassword!");

        _userRepository.FindByEmailForLoginAsync("test@example.com", Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher.VerifyHashedPassword(user, user.PasswordHash!, request.Password)
            .Returns(PasswordVerificationResult.Failed);
        _securityService.IncrementFailedAttemptsAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var act = async () => await _sut.LoginAsync(request, "127.0.0.1", "TestAgent");

        // Assert
        var ex = await Should.ThrowAsync<AccountLockedException>(act);
        ex.Message.ShouldContain("ログイン試行回数が上限に達しました");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_LogoutSuccessfully_When_ValidSessionToken()
    {
        // Arrange
        var session = new UserSession
        {
            Id = "session-1",
            UserId = "user-1",
            SessionToken = "valid-session-token",
            IsActive = true,
            IpAddress = "127.0.0.1",
            UserAgent = "TestAgent"
        };
        _userSessionRepository.FindBySessionIdAsync("session-1", Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        await _sut.LogoutAsync("session-1");

        // Assert
        session.IsActive.ShouldBeFalse();
        await _userSessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _refreshTokenRepository.Received(1).RevokeAllByUserIdAsync(
            session.UserId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnUserInfo_When_ValidUserId()
    {
        // Arrange
        var user = CreateTestUser();
        _userRepository.FindByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var result = await _sut.GetCurrentUserAsync(user.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(user.Id);
        result.Email.ShouldBe("test@example.com");
        result.FirstName.ShouldBe("Test");
        result.LastName.ShouldBe("User");
        result.Role.ShouldBe("USER");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowNotFoundException_When_UserNotFoundForGetCurrentUser()
    {
        // Arrange
        _userRepository.FindByIdAsync("nonexistent-id", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var act = async () => await _sut.GetCurrentUserAsync("nonexistent-id");

        // Assert
        var ex = await Should.ThrowAsync<NotFoundException>(act);
        ex.Message.ShouldContain("ユーザーが見つかりません");
    }
}
