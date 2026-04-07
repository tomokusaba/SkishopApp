using AuthService.DTOs.Responses;
using AuthService.Enums;
using AuthService.Exceptions;
using AuthService.Models;
using AuthService.Repositories.Interfaces;
using AuthService.Services;
using AuthService.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace AuthService.Tests.Services;

public class MfaServiceTests
{
    private readonly IMfaRepository _mfaRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITotpService _totpService;
    private readonly IEncryptionService _encryptionService;
    private readonly ISecurityService _securityService;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ILogger<MfaService> _logger;
    private readonly MfaService _sut;

    public MfaServiceTests()
    {
        _mfaRepository = Substitute.For<IMfaRepository>();
        _userRepository = Substitute.For<IUserRepository>();
        _totpService = Substitute.For<ITotpService>();
        _encryptionService = Substitute.For<IEncryptionService>();
        _securityService = Substitute.For<ISecurityService>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
        _logger = Substitute.For<ILogger<MfaService>>();

        _encryptionService.Encrypt(Arg.Any<string>())
            .Returns(callInfo => $"encrypted:{callInfo.Arg<string>()}");
        _encryptionService.Decrypt(Arg.Any<string>())
            .Returns(callInfo => callInfo.Arg<string>().Replace("encrypted:", ""));

        _sut = new MfaService(
            _mfaRepository,
            _userRepository,
            _totpService,
            _encryptionService,
            _securityService,
            _timeProvider,
            _logger);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnSetupResponse_When_SetupMfa()
    {
        // Arrange
        var userId = "user-mfa-1";
        var user = new User { Id = userId, Email = "mfa@example.com", Status = UserStatus.Active };
        var backupCodes = new List<string> { "code1", "code2", "code3" }.AsReadOnly();

        _userRepository.FindByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        _mfaRepository.FindByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((UserMfa?)null);
        _totpService.GenerateSecretAsync(Arg.Any<CancellationToken>())
            .Returns("GENERATED_SECRET");
        _totpService.GenerateQrCodeUri("mfa@example.com", "GENERATED_SECRET")
            .Returns("otpauth://totp/SkiShop:mfa@example.com?secret=GENERATED_SECRET");
        _totpService.GenerateBackupCodes(8)
            .Returns(backupCodes);

        // Act
        var result = await _sut.SetupMfaAsync(userId);

        // Assert
        result.ShouldNotBeNull();
        result.SecretKey.ShouldBe("GENERATED_SECRET");
        result.QrCodeUri.ShouldContain("otpauth://totp/");
        result.BackupCodes.Count.ShouldBe(3);
        await _mfaRepository.Received(1).AddAsync(Arg.Any<UserMfa>(), Arg.Any<CancellationToken>());
        await _mfaRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_MfaAlreadyEnabled()
    {
        // Arrange
        var userId = "user-mfa-2";
        var user = new User { Id = userId, Email = "mfa2@example.com", Status = UserStatus.Active };
        var existingMfa = new UserMfa { UserId = userId, IsEnabled = true, SecretKey = "existing-secret" };

        _userRepository.FindByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);
        _mfaRepository.FindByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(existingMfa);

        // Act
        var act = async () => await _sut.SetupMfaAsync(userId);

        // Assert
        var ex = await Should.ThrowAsync<BusinessException>(act);
        ex.Message.ShouldContain("MFA は既に有効化されています");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_DisableMfa_When_ValidUser()
    {
        // Arrange
        var userId = "user-mfa-3";
        var mfa = new UserMfa { UserId = userId, IsEnabled = true, SecretKey = "secret" };

        _mfaRepository.FindByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(mfa);

        // Act
        await _sut.DisableMfaAsync(userId);

        // Assert
        mfa.IsEnabled.ShouldBeFalse();
        await _mfaRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _securityService.Received(1).LogSecurityEventAsync(
            userId, "MFA_DISABLED", null, null, "MFA が無効化されました", Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowNotFoundException_When_MfaNotSetupForDisable()
    {
        // Arrange
        var userId = "user-mfa-4";
        _mfaRepository.FindByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((UserMfa?)null);

        // Act
        var act = async () => await _sut.DisableMfaAsync(userId);

        // Assert
        var ex = await Should.ThrowAsync<NotFoundException>(act);
        ex.Message.ShouldContain("MFA が設定されていません");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_VerifyAndEnableMfa_When_ValidCode()
    {
        // Arrange
        var userId = "user-mfa-5";
        var mfa = new UserMfa { UserId = userId, IsEnabled = false, SecretKey = "encrypted:secret-key" };

        _mfaRepository.FindByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(mfa);
        _totpService.VerifyCode("secret-key", "123456")
            .Returns(true);

        // Act
        var result = await _sut.VerifyMfaAsync(userId, "123456");

        // Assert
        result.ShouldBeTrue();
        mfa.IsEnabled.ShouldBeTrue();
        await _securityService.Received(1).LogSecurityEventAsync(
            userId, "MFA_ENABLED", null, null, "MFA が有効化されました", Arg.Any<CancellationToken>());
    }
}
