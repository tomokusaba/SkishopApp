using NSubstitute;
using Shouldly;
using Microsoft.Extensions.Logging;
using UserManagementService.DTOs.Requests;
using UserManagementService.DTOs.Responses;
using UserManagementService.Exceptions;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;
using UserManagementService.Services;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.Tests.Unit.Services;

public class DsrServiceTests
{
    private readonly IDeletionRequestRepository _deletionRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEventPublisherService _eventPublisher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DsrService> _logger;
    private readonly DsrService _sut;

    private static readonly DateTimeOffset FixedNow =
        new(2026, 3, 18, 12, 0, 0, TimeSpan.Zero);

    public DsrServiceTests()
    {
        _deletionRequestRepository = Substitute.For<IDeletionRequestRepository>();
        _userRepository = Substitute.For<IUserRepository>();
        _eventPublisher = Substitute.For<IEventPublisherService>();
        _timeProvider = Substitute.For<TimeProvider>();
        _logger = Substitute.For<ILogger<DsrService>>();

        _timeProvider.GetUtcNow().Returns(FixedNow);

        _sut = new DsrService(
            _deletionRequestRepository, _userRepository, _eventPublisher, _timeProvider, _logger);
    }

    private static DeletionRequest CreateTestRequest(
        string userId = "user-1",
        string status = DeletionRequestStatus.Pending) => new()
    {
        Id = "req-1",
        UserId = userId,
        RequestedBy = userId,
        RequestChannel = RequestChannel.WebSelfService,
        RequestedAt = FixedNow,
        GracePeriodEndsAt = FixedNow.AddDays(14),
        Status = status
    };

    #region CreateDeletionRequestAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_CreateDeletionRequest_When_NoPendingRequestExists()
    {
        // Arrange
        _deletionRequestRepository.FindPendingByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns((DeletionRequest?)null);
        var request = new CreateDeletionRequest(RequestChannel.WebSelfService);

        // Act
        var result = await _sut.CreateDeletionRequestAsync("user-1", "user-1", request);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(DeletionRequestStatus.Pending);
        result.RequestedAt.ShouldBe(FixedNow);
        result.GracePeriodEndsAt.ShouldBe(FixedNow.AddDays(14));
        await _deletionRequestRepository.Received(1).AddAsync(
            Arg.Is<DeletionRequest>(d =>
                d.UserId == "user-1" &&
                d.RequestedBy == "user-1" &&
                d.RequestChannel == RequestChannel.WebSelfService &&
                d.Status == DeletionRequestStatus.Pending),
            Arg.Any<CancellationToken>());
        await _deletionRequestRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_PendingRequestAlreadyExists()
    {
        // Arrange
        var existingRequest = CreateTestRequest();
        _deletionRequestRepository.FindPendingByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(existingRequest);
        var request = new CreateDeletionRequest(RequestChannel.WebSelfService);

        // Act
        var act = async () => await _sut.CreateDeletionRequestAsync("user-1", "user-1", request);

        // Assert
        var ex = await Should.ThrowAsync<BusinessException>(act);
        ex.Message.ShouldContain("削除リクエスト");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_SetGracePeriod14Days_When_RequestCreated()
    {
        // Arrange
        _deletionRequestRepository.FindPendingByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns((DeletionRequest?)null);
        var request = new CreateDeletionRequest(RequestChannel.AdminConsole);

        // Act
        var result = await _sut.CreateDeletionRequestAsync("user-1", "admin-1", request);

        // Assert
        var gracePeriod = result.GracePeriodEndsAt - result.RequestedAt;
        gracePeriod.Days.ShouldBe(14);
    }

    #endregion

    #region CancelDeletionRequestAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_CancelRequest_When_StatusIsPending()
    {
        // Arrange
        var request = CreateTestRequest();
        _deletionRequestRepository.FindPendingByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(request);

        // Act
        await _sut.CancelDeletionRequestAsync("user-1");

        // Assert
        request.Status.ShouldBe(DeletionRequestStatus.Cancelled);
        await _deletionRequestRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowNotFoundException_When_NoPendingRequestToCancel()
    {
        // Arrange
        _deletionRequestRepository.FindPendingByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns((DeletionRequest?)null);

        // Act
        var act = async () => await _sut.CancelDeletionRequestAsync("user-1");

        // Assert
        var ex = await Should.ThrowAsync<NotFoundException>(act);
        ex.Message.ShouldContain("見つかりません");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_RequestIsNotPending()
    {
        // Arrange
        var request = CreateTestRequest(status: DeletionRequestStatus.Processing);
        _deletionRequestRepository.FindPendingByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(request);

        // Act
        var act = async () => await _sut.CancelDeletionRequestAsync("user-1");

        // Assert
        var ex = await Should.ThrowAsync<BusinessException>(act);
        ex.Message.ShouldContain("キャンセル");
    }

    #endregion

    #region GetDeletionRequestAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnDeletionRequestDto_When_PendingRequestExists()
    {
        // Arrange
        var request = CreateTestRequest();
        _deletionRequestRepository.FindPendingByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(request);

        // Act
        var result = await _sut.GetDeletionRequestAsync("user-1");

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("req-1");
        result.Status.ShouldBe(DeletionRequestStatus.Pending);
        result.GracePeriodEndsAt.ShouldBe(FixedNow.AddDays(14));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnNull_When_NoPendingRequestFound()
    {
        // Arrange
        _deletionRequestRepository.FindPendingByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns((DeletionRequest?)null);

        // Act
        var result = await _sut.GetDeletionRequestAsync("user-1");

        // Assert
        result.ShouldBeNull();
    }

    #endregion

    #region HandleDeletionCompletedAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_MarkCompleted_When_DeletionSucceeds()
    {
        // Arrange
        var request = CreateTestRequest();
        _deletionRequestRepository.FindPendingByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(request);
        var user = new User { Id = "user-1", Email = "test@example.com" };
        _userRepository.FindByIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        await _sut.HandleDeletionCompletedAsync("user-1", "AuthService", true, null);

        // Assert
        request.Status.ShouldBe(DeletionRequestStatus.Completed);
        request.CompletedAt.ShouldBe(FixedNow);
        await _eventPublisher.Received(1).PublishDeletionNotificationAsync(
            "user-1", Arg.Any<CancellationToken>());
        await _deletionRequestRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_MarkFailed_When_DeletionFails()
    {
        // Arrange
        var request = CreateTestRequest();
        _deletionRequestRepository.FindPendingByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(request);

        // Act
        await _sut.HandleDeletionCompletedAsync("user-1", "InventoryService", false, "DB接続エラー");

        // Assert
        request.Status.ShouldBe(DeletionRequestStatus.Failed);
        request.FailureReason.ShouldNotBeNull();
        request.FailureReason!.ShouldContain("InventoryService");
        request.FailureReason.ShouldContain("DB接続エラー");
        await _eventPublisher.DidNotReceive().PublishDeletionNotificationAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_DoNothing_When_NoRequestFoundForDeletionCompleted()
    {
        // Arrange
        _deletionRequestRepository.FindPendingByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns((DeletionRequest?)null);

        // Act
        await _sut.HandleDeletionCompletedAsync("user-1", "AuthService", true, null);

        // Assert
        await _deletionRequestRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_NotSendNotification_When_UserNotFoundOnCompletion()
    {
        // Arrange
        var request = CreateTestRequest();
        _deletionRequestRepository.FindPendingByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(request);
        _userRepository.FindByIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        await _sut.HandleDeletionCompletedAsync("user-1", "AuthService", true, null);

        // Assert
        request.Status.ShouldBe(DeletionRequestStatus.Completed);
        await _eventPublisher.DidNotReceive().PublishDeletionNotificationAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region ProcessExpiredGracePeriodRequestsAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_TransitionToProcessing_When_GracePeriodExpired()
    {
        // Arrange
        var request1 = CreateTestRequest("user-1");
        request1.UserId = "user-1";
        var request2 = CreateTestRequest("user-2");
        request2.UserId = "user-2";
        _deletionRequestRepository.FindExpiredGracePeriodAsync(Arg.Any<CancellationToken>())
            .Returns(new List<DeletionRequest> { request1, request2 });

        // Act
        await _sut.ProcessExpiredGracePeriodRequestsAsync();

        // Assert
        request1.Status.ShouldBe(DeletionRequestStatus.Processing);
        request2.Status.ShouldBe(DeletionRequestStatus.Processing);
        await _eventPublisher.Received(1).PublishUserDeletedAsync("user-1", Arg.Any<CancellationToken>());
        await _eventPublisher.Received(1).PublishUserDeletedAsync("user-2", Arg.Any<CancellationToken>());
        await _deletionRequestRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_SkipPublish_When_UserIdIsNull()
    {
        // Arrange
        var request = CreateTestRequest();
        request.UserId = null;
        _deletionRequestRepository.FindExpiredGracePeriodAsync(Arg.Any<CancellationToken>())
            .Returns(new List<DeletionRequest> { request });

        // Act
        await _sut.ProcessExpiredGracePeriodRequestsAsync();

        // Assert
        request.Status.ShouldBe(DeletionRequestStatus.Processing);
        await _eventPublisher.DidNotReceive().PublishUserDeletedAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_HandleEmptyList_When_NoExpiredRequests()
    {
        // Arrange
        _deletionRequestRepository.FindExpiredGracePeriodAsync(Arg.Any<CancellationToken>())
            .Returns(new List<DeletionRequest>());

        // Act
        await _sut.ProcessExpiredGracePeriodRequestsAsync();

        // Assert
        await _eventPublisher.DidNotReceive().PublishUserDeletedAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _deletionRequestRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion
}
