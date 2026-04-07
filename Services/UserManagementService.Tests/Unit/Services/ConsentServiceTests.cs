using NSubstitute;
using Shouldly;
using Microsoft.Extensions.Logging;
using UserManagementService.DTOs.Requests;
using UserManagementService.DTOs.Responses;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;
using UserManagementService.Services;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.Tests.Unit.Services;

public class ConsentServiceTests
{
    private readonly IConsentRepository _consentRepository;
    private readonly IEventPublisherService _eventPublisher;
    private readonly ILogger<ConsentService> _logger;
    private readonly ConsentService _sut;

    public ConsentServiceTests()
    {
        _consentRepository = Substitute.For<IConsentRepository>();
        _eventPublisher = Substitute.For<IEventPublisherService>();
        _logger = Substitute.For<ILogger<ConsentService>>();
        _sut = new ConsentService(_consentRepository, _eventPublisher, _logger);
    }

    private static Consent CreateTestConsent(
        string userId = "user-1",
        string consentType = ConsentType.Marketing,
        bool isGranted = true) => new()
    {
        Id = Guid.NewGuid().ToString(),
        UserId = userId,
        ConsentType = consentType,
        IsGranted = isGranted,
        Version = 1,
        IpAddress = "192.168.1.1",
        UserAgent = "TestAgent/1.0",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    #region GetByUserIdAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnConsentList_When_UserHasConsents()
    {
        // Arrange
        var consents = new List<Consent>
        {
            CreateTestConsent(consentType: ConsentType.Marketing),
            CreateTestConsent(consentType: ConsentType.Analytics, isGranted: false)
        };
        _consentRepository.FindByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(consents);

        // Act
        var result = await _sut.GetByUserIdAsync("user-1");

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldContain(c => c.ConsentType == ConsentType.Marketing && c.IsGranted);
        result.ShouldContain(c => c.ConsentType == ConsentType.Analytics && !c.IsGranted);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnEmptyList_When_UserHasNoConsents()
    {
        // Arrange
        _consentRepository.FindByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(new List<Consent>());

        // Act
        var result = await _sut.GetByUserIdAsync("user-1");

        // Assert
        result.ShouldBeEmpty();
    }

    #endregion

    #region UpdateAsync — Create new consent

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_CreateNewConsent_When_NoExistingConsentFound()
    {
        // Arrange
        _consentRepository.FindByUserIdAndTypeAsync("user-1", ConsentType.Marketing, Arg.Any<CancellationToken>())
            .Returns((Consent?)null);
        var request = new ConsentUpdateRequest(ConsentType.Marketing, true, 1);

        // Act
        var result = await _sut.UpdateAsync("user-1", request, "10.0.0.1", "Browser/2.0");

        // Assert
        result.ShouldNotBeNull();
        result.ConsentType.ShouldBe(ConsentType.Marketing);
        result.IsGranted.ShouldBeTrue();
        await _consentRepository.Received(1).AddAsync(
            Arg.Is<Consent>(c =>
                c.UserId == "user-1" &&
                c.ConsentType == ConsentType.Marketing &&
                c.IsGranted &&
                c.IpAddress == "10.0.0.1" &&
                c.UserAgent == "Browser/2.0"),
            Arg.Any<CancellationToken>());
        await _consentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region UpdateAsync — Update existing consent

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_UpdateExistingConsent_When_ConsentExists()
    {
        // Arrange
        var existing = CreateTestConsent(isGranted: true);
        _consentRepository.FindByUserIdAndTypeAsync("user-1", ConsentType.Marketing, Arg.Any<CancellationToken>())
            .Returns(existing);
        var request = new ConsentUpdateRequest(ConsentType.Marketing, true, 2);

        // Act
        var result = await _sut.UpdateAsync("user-1", request, "10.0.0.2", "NewAgent");

        // Assert
        result.ShouldNotBeNull();
        result.IsGranted.ShouldBeTrue();
        existing.Version.ShouldBe(2);
        existing.IpAddress.ShouldBe("10.0.0.2");
        existing.UserAgent.ShouldBe("NewAgent");
        await _consentRepository.DidNotReceive().AddAsync(Arg.Any<Consent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PublishConsentRevoked_When_GrantedConsentRevoked()
    {
        // Arrange
        var existing = CreateTestConsent(isGranted: true);
        _consentRepository.FindByUserIdAndTypeAsync("user-1", ConsentType.Marketing, Arg.Any<CancellationToken>())
            .Returns(existing);
        var request = new ConsentUpdateRequest(ConsentType.Marketing, false, 2);

        // Act
        await _sut.UpdateAsync("user-1", request, "10.0.0.1", "Agent");

        // Assert
        existing.IsGranted.ShouldBeFalse();
        await _eventPublisher.Received(1).PublishConsentRevokedAsync(
            "user-1", ConsentType.Marketing, Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_NotPublishConsentRevoked_When_AlreadyRevokedConsentUpdated()
    {
        // Arrange
        var existing = CreateTestConsent(isGranted: false);
        _consentRepository.FindByUserIdAndTypeAsync("user-1", ConsentType.Marketing, Arg.Any<CancellationToken>())
            .Returns(existing);
        var request = new ConsentUpdateRequest(ConsentType.Marketing, false, 2);

        // Act
        await _sut.UpdateAsync("user-1", request, "10.0.0.1", "Agent");

        // Assert
        await _eventPublisher.DidNotReceive().PublishConsentRevokedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_NotPublishConsentRevoked_When_ConsentGranted()
    {
        // Arrange
        var existing = CreateTestConsent(isGranted: false);
        _consentRepository.FindByUserIdAndTypeAsync("user-1", ConsentType.Analytics, Arg.Any<CancellationToken>())
            .Returns(existing);
        var request = new ConsentUpdateRequest(ConsentType.Analytics, true, 2);

        // Act
        await _sut.UpdateAsync("user-1", request, "10.0.0.1", "Agent");

        // Assert
        existing.IsGranted.ShouldBeTrue();
        await _eventPublisher.DidNotReceive().PublishConsentRevokedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region CreateAnonymousConsentAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_CreateAnonymousConsent_When_Called()
    {
        // Arrange
        var request = new ConsentUpdateRequest(ConsentType.Analytics, true, 1);

        // Act
        var result = await _sut.CreateAnonymousConsentAsync(request, "192.168.0.1", "AnonymousBrowser");

        // Assert
        result.ShouldNotBeNull();
        result.ConsentType.ShouldBe(ConsentType.Analytics);
        result.IsGranted.ShouldBeTrue();
        await _consentRepository.Received(1).AddAsync(
            Arg.Is<Consent>(c =>
                c.UserId == "anonymous" &&
                c.ConsentType == ConsentType.Analytics &&
                c.IpAddress == "192.168.0.1" &&
                c.UserAgent == "AnonymousBrowser"),
            Arg.Any<CancellationToken>());
        await _consentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_HandleNullIpAndUserAgent_When_AnonymousConsentCreated()
    {
        // Arrange
        var request = new ConsentUpdateRequest(ConsentType.Marketing, false, 1);

        // Act
        var result = await _sut.CreateAnonymousConsentAsync(request, null, null);

        // Assert
        result.ShouldNotBeNull();
        result.IsGranted.ShouldBeFalse();
        await _consentRepository.Received(1).AddAsync(
            Arg.Is<Consent>(c => c.IpAddress == null && c.UserAgent == null),
            Arg.Any<CancellationToken>());
    }

    #endregion
}
