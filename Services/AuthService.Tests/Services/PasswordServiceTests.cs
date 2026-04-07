using AuthService.Configurations;
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
using Shouldly;

namespace AuthService.Tests.Services;

public class PasswordServiceTests
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetRepository _passwordResetRepository;
    private readonly IPasswordHistoryRepository _passwordHistoryRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IOutboxEventRepository _outboxEventRepository;
    private readonly ISecurityService _securityService;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ILogger<PasswordService> _logger;
    private readonly PasswordService _sut;

    public PasswordServiceTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _passwordResetRepository = Substitute.For<IPasswordResetRepository>();
        _passwordHistoryRepository = Substitute.For<IPasswordHistoryRepository>();
        _passwordHasher = Substitute.For<IPasswordHasher<User>>();
        _outboxEventRepository = Substitute.For<IOutboxEventRepository>();
        _securityService = Substitute.For<ISecurityService>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
        _logger = Substitute.For<ILogger<PasswordService>>();

        var authSettings = Options.Create(new AuthSettings
        {
            MaxFailedAttempts = 5,
            AutoUnlockMinutes = 30,
            FailedAttemptResetMinutes = 15
        });

        _sut = new PasswordService(
            _userRepository,
            _passwordResetRepository,
            _passwordHistoryRepository,
            _passwordHasher,
            _outboxEventRepository,
            _securityService,
            _timeProvider,
            authSettings,
            _logger);
    }

    private static User CreateTestUser() => new()
    {
        Id = "user-pwd-1",
        Email = "pwd@example.com",
        Username = "pwduser",
        PasswordHash = "current-hashed-password",
        Status = UserStatus.Active,
        Role = UserRoleType.User,
        IsActive = true
    };

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_RequestReset_When_ValidEmail()
    {
        // Arrange
        var user = CreateTestUser();
        _userRepository.FindByEmailAsync("pwd@example.com", Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await _sut.RequestResetAsync("pwd@example.com");

        // Assert
        await _passwordResetRepository.Received(1).InvalidateExistingTokensAsync(
            user.Id, "PASSWORD_RESET", Arg.Any<CancellationToken>());
        await _passwordResetRepository.Received(1).AddAsync(
            Arg.Any<PasswordReset>(), Arg.Any<CancellationToken>());
        await _outboxEventRepository.Received(1).AddAsync(
            Arg.Any<OutboxEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_NotThrow_When_ResetRequestedForNonexistentEmail()
    {
        // Arrange
        _userRepository.FindByEmailAsync("nonexistent@example.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act & Assert (should silently return for security reasons)
        await Should.NotThrowAsync(() => _sut.RequestResetAsync("nonexistent@example.com"));

        await _passwordResetRepository.DidNotReceive().AddAsync(
            Arg.Any<PasswordReset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ChangePassword_When_ValidCurrentPassword()
    {
        // Arrange
        var user = CreateTestUser();
        _userRepository.FindByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher.VerifyHashedPassword(user, user.PasswordHash!, "CurrentPass1!")
            .Returns(PasswordVerificationResult.Success);
        _passwordHistoryRepository.FindRecentByUserIdAsync(user.Id, 5, Arg.Any<CancellationToken>())
            .Returns(new List<PasswordHistory>().AsReadOnly());
        _passwordHasher.HashPassword(user, "NewStrongP@ss1")
            .Returns("new-hashed-password");

        // Act
        await _sut.ChangePasswordAsync(user.Id, "CurrentPass1!", "NewStrongP@ss1");

        // Assert
        user.PasswordHash.ShouldBe("new-hashed-password");
        await _passwordHistoryRepository.Received(1).AddAsync(
            Arg.Any<PasswordHistory>(), Arg.Any<CancellationToken>());
        await _userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _outboxEventRepository.Received(1).AddAsync(
            Arg.Any<OutboxEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_CurrentPasswordIncorrect()
    {
        // Arrange
        var user = CreateTestUser();
        _userRepository.FindByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher.VerifyHashedPassword(user, user.PasswordHash!, "WrongPassword!")
            .Returns(PasswordVerificationResult.Failed);

        // Act
        var act = async () => await _sut.ChangePasswordAsync(user.Id, "WrongPassword!", "NewPass1!");

        // Assert
        var ex = await Should.ThrowAsync<BusinessException>(act);
        ex.Message.ShouldContain("現在のパスワードが正しくありません");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_PasswordReusedFromHistory()
    {
        // Arrange
        var user = CreateTestUser();
        _userRepository.FindByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher.VerifyHashedPassword(user, user.PasswordHash!, "CurrentPass1!")
            .Returns(PasswordVerificationResult.Success);

        var historyEntries = new List<PasswordHistory>
        {
            new() { UserId = user.Id, PasswordHash = "old-hash-1" }
        };
        _passwordHistoryRepository.FindRecentByUserIdAsync(user.Id, 5, Arg.Any<CancellationToken>())
            .Returns(historyEntries.AsReadOnly());
        _passwordHasher.VerifyHashedPassword(user, "old-hash-1", "ReusedPassword1!")
            .Returns(PasswordVerificationResult.Success);

        // Act
        var act = async () => await _sut.ChangePasswordAsync(user.Id, "CurrentPass1!", "ReusedPassword1!");

        // Assert
        var ex = await Should.ThrowAsync<BusinessException>(act);
        ex.Message.ShouldContain("過去に使用したパスワードは再利用できません");
    }
}
