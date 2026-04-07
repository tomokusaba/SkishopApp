using NSubstitute;
using Shouldly;
using Microsoft.Extensions.Logging;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;
using UserManagementService.Services;

namespace UserManagementService.Tests.Unit.Services;

public class EventPublisherServiceTests
{
    private readonly IOutboxEventRepository _outboxRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EventPublisherService> _logger;
    private readonly EventPublisherService _sut;

    public EventPublisherServiceTests()
    {
        _outboxRepository = Substitute.For<IOutboxEventRepository>();
        _timeProvider = Substitute.For<TimeProvider>();
        _logger = Substitute.For<ILogger<EventPublisherService>>();
        _timeProvider.GetUtcNow().Returns(new DateTimeOffset(2026, 3, 18, 12, 0, 0, TimeSpan.Zero));
        _sut = new EventPublisherService(_outboxRepository, _timeProvider, _logger);
    }

    #region PublishProfileUpdatedAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_AddOutboxEvent_When_ProfileUpdatedPublished()
    {
        // Arrange & Act
        await _sut.PublishProfileUpdatedAsync("user-1");

        // Assert
        await _outboxRepository.Received(1).AddAsync(
            Arg.Is<OutboxEvent>(e =>
                e.EventType == "user.profile-updated" &&
                e.AggregateId == "user-1" &&
                e.Payload.Contains("user-1")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_IncludeUpdatedFieldsInPayload_When_ProfileUpdatedPublished()
    {
        // Arrange & Act
        await _sut.PublishProfileUpdatedAsync("user-1");

        // Assert
        await _outboxRepository.Received(1).AddAsync(
            Arg.Is<OutboxEvent>(e =>
                e.Payload.Contains("firstName") &&
                e.Payload.Contains("lastName") &&
                e.Payload.Contains("phoneNumber") &&
                e.Payload.Contains("birthDate")),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region PublishUserDeletedAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_AddOutboxEvent_When_UserDeletedPublished()
    {
        // Arrange & Act
        await _sut.PublishUserDeletedAsync("user-1");

        // Assert
        await _outboxRepository.Received(1).AddAsync(
            Arg.Is<OutboxEvent>(e =>
                e.EventType == "user.deleted" &&
                e.AggregateId == "user-1" &&
                e.Payload.Contains("user-1")),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region PublishConsentRevokedAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_AddOutboxEvent_When_ConsentRevokedPublished()
    {
        // Arrange & Act
        await _sut.PublishConsentRevokedAsync("user-1", "MARKETING");

        // Assert
        await _outboxRepository.Received(1).AddAsync(
            Arg.Is<OutboxEvent>(e =>
                e.EventType == "consent.revoked" &&
                e.AggregateId == "user-1" &&
                e.Payload.Contains("MARKETING")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_IncludeConsentTypeInPayload_When_ConsentRevokedPublished()
    {
        // Arrange & Act
        await _sut.PublishConsentRevokedAsync("user-1", "ANALYTICS");

        // Assert
        await _outboxRepository.Received(1).AddAsync(
            Arg.Is<OutboxEvent>(e =>
                e.Payload.Contains("ANALYTICS") &&
                e.Payload.Contains("user-1")),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region PublishMemberRankUpdatedAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_AddOutboxEvent_When_MemberRankUpdatedPublished()
    {
        // Arrange & Act
        await _sut.PublishMemberRankUpdatedAsync("user-1", "BRONZE", "SILVER", 0.03m);

        // Assert
        await _outboxRepository.Received(1).AddAsync(
            Arg.Is<OutboxEvent>(e =>
                e.EventType == "member-rank.updated" &&
                e.AggregateId == "user-1" &&
                e.Payload.Contains("BRONZE") &&
                e.Payload.Contains("SILVER")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_IncludePointRateInPayload_When_MemberRankUpdatedPublished()
    {
        // Arrange & Act
        await _sut.PublishMemberRankUpdatedAsync("user-1", "SILVER", "GOLD", 0.05m);

        // Assert
        await _outboxRepository.Received(1).AddAsync(
            Arg.Is<OutboxEvent>(e =>
                e.Payload.Contains("0.05")),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region PublishDeletionNotificationAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_AddOutboxEvent_When_DeletionNotificationPublished()
    {
        // Arrange & Act
        await _sut.PublishDeletionNotificationAsync("user-1");

        // Assert
        await _outboxRepository.Received(1).AddAsync(
            Arg.Is<OutboxEvent>(e =>
                e.EventType == "user.deletion.notification" &&
                e.AggregateId == "user-1"),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Cross-cutting

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_SetCorrectAggregateId_When_AnyEventPublished()
    {
        // Arrange
        var userId = "specific-user-id";

        // Act
        await _sut.PublishProfileUpdatedAsync(userId);
        await _sut.PublishUserDeletedAsync(userId);

        // Assert
        await _outboxRepository.Received(2).AddAsync(
            Arg.Is<OutboxEvent>(e => e.AggregateId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_SetPendingStatus_When_OutboxEventCreated()
    {
        // Arrange & Act
        await _sut.PublishUserDeletedAsync("user-1");

        // Assert
        await _outboxRepository.Received(1).AddAsync(
            Arg.Is<OutboxEvent>(e => e.Status == OutboxEventStatus.Pending),
            Arg.Any<CancellationToken>());
    }

    #endregion
}
