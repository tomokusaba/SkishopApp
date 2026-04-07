using System.Diagnostics.Metrics;
using AiSupportService.Configurations;
using AiSupportService.DTOs.Requests;
using AiSupportService.Exceptions;
using AiSupportService.Infrastructure.Metrics;
using AiSupportService.Models;
using AiSupportService.Repositories.Interfaces;
using AiSupportService.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AiSupportService.Tests.Services;

public class ChatServiceTests
{
    private readonly IChatSessionRepository _sessionRepository;
    private readonly IChatMessageRepository _messageRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IOutboxEventRepository _outboxEventRepository;
    private readonly ChatService _sut;

    public ChatServiceTests()
    {
        _sessionRepository = Substitute.For<IChatSessionRepository>();
        _messageRepository = Substitute.For<IChatMessageRepository>();
        _userProfileRepository = Substitute.For<IUserProfileRepository>();
        _outboxEventRepository = Substitute.For<IOutboxEventRepository>();

        var kernel = Kernel.CreateBuilder().Build();
        var meterFactory = new TestMeterFactory();
        var metrics = new AiSupportMetrics(meterFactory);
        var chatSettings = Options.Create(new AiChatSettings { MaxSessionsPerUser = 10 });
        var logger = Substitute.For<ILogger<ChatService>>();

        _sut = new ChatService(
            _sessionRepository, _messageRepository, _userProfileRepository,
            _outboxEventRepository, kernel, metrics, chatSettings, logger);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_CreateSession_When_ValidUserIdProvided()
    {
        // Arrange
        var request = new CreateChatSessionRequest("テスト");
        _sessionRepository.CountActiveByUserIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(0);
        _userProfileRepository.GetOrCreateAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(new UserProfile { UserId = "user-1" });

        // Act
        var result = await _sut.CreateSessionAsync("user-1", request);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe("ACTIVE");
        result.UserId.ShouldBe("user-1");
        result.Title.ShouldBe("テスト");
        await _sessionRepository.Received(1).AddAsync(Arg.Any<ChatSession>(), Arg.Any<CancellationToken>());
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_MaxSessionsReached()
    {
        // Arrange
        var request = new CreateChatSessionRequest("テスト");
        _sessionRepository.CountActiveByUserIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(10);

        // Act
        var act = async () => await _sut.CreateSessionAsync("user-1", request);

        // Assert
        var ex = await Should.ThrowAsync<BusinessException>(act);
        ex.Message.ShouldContain("上限");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnSessions_When_UserHasSessions()
    {
        // Arrange
        var sessions = new List<ChatSession>
        {
            new() { UserId = "user-1", Title = "セッション1", Status = "ACTIVE" },
            new() { UserId = "user-1", Title = "セッション2", Status = "CLOSED" }
        };
        _sessionRepository.FindByUserIdPagedAsync("user-1", 1, 20, Arg.Any<CancellationToken>())
            .Returns((sessions, 2));

        // Act
        var result = await _sut.GetSessionsAsync("user-1");

        // Assert
        result.TotalCount.ShouldBe(2);
        result.Items[0].Title.ShouldBe("セッション1");
        result.Items[1].Status.ShouldBe("CLOSED");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnMessages_When_ValidSessionProvided()
    {
        // Arrange
        var session = new ChatSession { Id = "session-1", UserId = "user-1", Status = "ACTIVE" };
        _sessionRepository.FindByIdAndUserIdAsync("session-1", "user-1", Arg.Any<CancellationToken>())
            .Returns(session);

        var messages = new List<ChatMessage>
        {
            new() { SessionId = "session-1", Role = "system", Content = "システムプロンプト" },
            new() { SessionId = "session-1", Role = "user", Content = "こんにちは" },
            new() { SessionId = "session-1", Role = "assistant", Content = "はい、こんにちは" }
        };
        _messageRepository.FindBySessionIdAsync("session-1", Arg.Any<CancellationToken>()).Returns(messages);

        // Act
        var result = await _sut.GetMessagesAsync("session-1", "user-1");

        // Assert
        result.TotalCount.ShouldBe(2);
        result.Items.ShouldAllBe(m => m.Role != "system");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_CloseSession_When_SessionIsActive()
    {
        // Arrange
        var session = new ChatSession { Id = "session-1", UserId = "user-1", Status = "ACTIVE" };
        _sessionRepository.FindByIdAndUserIdAsync("session-1", "user-1", Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        await _sut.CloseSessionAsync("session-1", "user-1");

        // Assert
        session.Status.ShouldBe("CLOSED");
        session.ClosedAt.ShouldNotBeNull();
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_ClosingNonActiveSession()
    {
        // Arrange
        var session = new ChatSession { Id = "session-1", UserId = "user-1", Status = "CLOSED" };
        _sessionRepository.FindByIdAndUserIdAsync("session-1", "user-1", Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        var act = async () => await _sut.CloseSessionAsync("session-1", "user-1");

        // Assert
        var ex = await Should.ThrowAsync<BusinessException>(act);
        ex.Message.ShouldContain("アクティブでないセッション");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_EscalateSession_When_SessionIsActive()
    {
        // Arrange
        var session = new ChatSession { Id = "session-1", UserId = "user-1", Status = "ACTIVE" };
        _sessionRepository.FindByIdAndUserIdAsync("session-1", "user-1", Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        await _sut.EscalateSessionAsync("session-1", "user-1");

        // Assert
        session.Status.ShouldBe("ESCALATED");
        await _outboxEventRepository.Received(1).AddAsync(Arg.Any<OutboxEvent>(), Arg.Any<CancellationToken>());
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowNotFoundException_When_SessionNotFound()
    {
        // Arrange
        _sessionRepository.FindByIdAndUserIdAsync("nonexistent", "user-1", Arg.Any<CancellationToken>())
            .Returns((ChatSession?)null);

        // Act
        var act = async () => await _sut.GetMessagesAsync("nonexistent", "user-1");

        // Assert
        var ex = await Should.ThrowAsync<NotFoundException>(act);
        ex.Message.ShouldContain("nonexistent");
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];
        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(options);
            _meters.Add(meter);
            return meter;
        }
        public void Dispose()
        {
            foreach (var meter in _meters) meter.Dispose();
            _meters.Clear();
        }
    }
}
