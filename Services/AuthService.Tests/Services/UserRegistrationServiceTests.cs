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
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace AuthService.Tests.Services;

public class UserRegistrationServiceTests
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPasswordResetRepository _passwordResetRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IOutboxEventRepository _outboxEventRepository;
    private readonly ISecurityService _securityService;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ILogger<UserRegistrationService> _logger;
    private readonly UserRegistrationService _sut;

    public UserRegistrationServiceTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _roleRepository = Substitute.For<IRoleRepository>();
        _passwordResetRepository = Substitute.For<IPasswordResetRepository>();
        _passwordHasher = Substitute.For<IPasswordHasher<User>>();
        _outboxEventRepository = Substitute.For<IOutboxEventRepository>();
        _securityService = Substitute.For<ISecurityService>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
        _logger = Substitute.For<ILogger<UserRegistrationService>>();

        _sut = new UserRegistrationService(
            _userRepository,
            _roleRepository,
            _passwordResetRepository,
            _passwordHasher,
            _outboxEventRepository,
            _securityService,
            _timeProvider,
            _logger);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_RegisterUser_When_ValidRequest()
    {
        // Arrange
        var request = new UserCreateRequest("new@example.com", "newuser", "StrongP@ss1", "First", "Last");
        _userRepository.FindByEmailAsync("new@example.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);
        _userRepository.FindByUsernameAsync("newuser", Arg.Any<CancellationToken>())
            .Returns((User?)null);
        _passwordHasher.HashPassword(Arg.Any<User>(), "StrongP@ss1")
            .Returns("hashed-strong-password");
        _roleRepository.FindByNameAsync("USER", Arg.Any<CancellationToken>())
            .Returns(new Role { Id = "role-1", Name = "USER" });

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.Email.ShouldBe("new@example.com");
        result.Username.ShouldBe("newuser");
        result.Status.ShouldBe("PENDINGVERIFICATION");
        result.Role.ShouldBe("USER");

        await _userRepository.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _passwordResetRepository.Received(1).AddAsync(Arg.Any<PasswordReset>(), Arg.Any<CancellationToken>());
        await _outboxEventRepository.Received(2).AddAsync(Arg.Any<OutboxEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_EmailAlreadyExists()
    {
        // Arrange
        var request = new UserCreateRequest("existing@example.com", "newuser", "StrongP@ss1", null, null);
        _userRepository.FindByEmailAsync("existing@example.com", Arg.Any<CancellationToken>())
            .Returns(new User { Email = "existing@example.com" });

        // Act
        var act = async () => await _sut.RegisterAsync(request);

        // Assert
        var ex = await Should.ThrowAsync<BusinessException>(act);
        ex.Message.ShouldContain("既に登録されています");
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_UsernameAlreadyExists()
    {
        // Arrange
        var request = new UserCreateRequest("new@example.com", "existinguser", "StrongP@ss1", null, null);
        _userRepository.FindByEmailAsync("new@example.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);
        _userRepository.FindByUsernameAsync("existinguser", Arg.Any<CancellationToken>())
            .Returns(new User { Username = "existinguser" });

        // Act
        var act = async () => await _sut.RegisterAsync(request);

        // Assert
        var ex = await Should.ThrowAsync<BusinessException>(act);
        ex.Message.ShouldContain("既に使用されています");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_VerifyEmail_When_ValidToken()
    {
        // Arrange
        var userId = "user-1";
        var user = new User { Id = userId, Status = UserStatus.PendingVerification, IsEmailVerified = false };
        var passwordReset = new PasswordReset
        {
            UserId = userId,
            Token = "valid-token",
            TokenType = "EMAIL_VERIFICATION",
            IsUsed = false,
            ExpiresAt = _timeProvider.GetUtcNow().AddHours(1)
        };

        _passwordResetRepository.FindByTokenAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(passwordReset);
        _userRepository.FindByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await _sut.VerifyEmailAsync("valid-token");

        // Assert
        user.IsEmailVerified.ShouldBeTrue();
        user.Status.ShouldBe(UserStatus.Active);
        passwordReset.IsUsed.ShouldBeTrue();
        await _userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
