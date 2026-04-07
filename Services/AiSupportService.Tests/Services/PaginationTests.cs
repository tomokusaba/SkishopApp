using System.Diagnostics.Metrics;
using AiSupportService.Configurations;
using AiSupportService.DTOs.Requests;
using AiSupportService.DTOs.Responses;
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

public class PaginationTests
{
    private readonly IChatSessionRepository _sessionRepository;
    private readonly IChatMessageRepository _messageRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IOutboxEventRepository _outboxEventRepository;
    private readonly ChatService _chatService;

    public PaginationTests()
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

        _chatService = new ChatService(
            _sessionRepository, _messageRepository, _userProfileRepository,
            _outboxEventRepository, kernel, metrics, chatSettings, logger);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnPagedSessions_When_MultipleSessionsExist()
    {
        // Arrange
        var sessions = new List<ChatSession>
        {
            new() { UserId = "user-1", Title = "S1", Status = "ACTIVE" },
            new() { UserId = "user-1", Title = "S2", Status = "ACTIVE" }
        };
        _sessionRepository.FindByUserIdPagedAsync("user-1", 1, 2, Arg.Any<CancellationToken>())
            .Returns((sessions, 5));

        // Act
        var result = await _chatService.GetSessionsAsync("user-1", 1, 2);

        // Assert
        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(5);
        result.Page.ShouldBe(1);
        result.PageSize.ShouldBe(2);
        result.TotalPages.ShouldBe(3);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnCorrectTotalPages_When_ExactDivision()
    {
        // Arrange
        var sessions = new List<ChatSession>
        {
            new() { UserId = "user-1", Title = "S1", Status = "ACTIVE" }
        };
        _sessionRepository.FindByUserIdPagedAsync("user-1", 2, 5, Arg.Any<CancellationToken>())
            .Returns((sessions, 10));

        // Act
        var result = await _chatService.GetSessionsAsync("user-1", 2, 5);

        // Assert
        result.TotalPages.ShouldBe(2);
        result.Page.ShouldBe(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnEmptyPage_When_NoSessions()
    {
        // Arrange
        _sessionRepository.FindByUserIdPagedAsync("user-1", 1, 20, Arg.Any<CancellationToken>())
            .Returns((new List<ChatSession>(), 0));

        // Act
        var result = await _chatService.GetSessionsAsync("user-1");

        // Assert
        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
        result.TotalPages.ShouldBe(0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PaginateMessages_When_MultipleMessagesExist()
    {
        // Arrange
        var session = new ChatSession { Id = "s-1", UserId = "user-1", Status = "ACTIVE" };
        _sessionRepository.FindByIdAndUserIdAsync("s-1", "user-1", Arg.Any<CancellationToken>())
            .Returns(session);

        var messages = Enumerable.Range(1, 10).Select(i =>
            new ChatMessage { SessionId = "s-1", Role = "user", Content = $"Message {i}" }).ToList();
        messages.Insert(0, new ChatMessage { SessionId = "s-1", Role = "system", Content = "System" });
        _messageRepository.FindBySessionIdAsync("s-1", Arg.Any<CancellationToken>()).Returns(messages);

        // Act — page 2, pageSize 3
        var result = await _chatService.GetMessagesAsync("s-1", "user-1", 2, 3);

        // Assert
        result.TotalCount.ShouldBe(10); // system excluded
        result.Page.ShouldBe(2);
        result.PageSize.ShouldBe(3);
        result.Items.Count.ShouldBe(3);
        result.Items[0].Content.ShouldBe("Message 4"); // skip 3, take 3 → messages 4,5,6
        result.TotalPages.ShouldBe(4); // ceil(10/3) = 4
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ExcludeSystemMessages_When_PaginatingMessages()
    {
        // Arrange
        var session = new ChatSession { Id = "s-1", UserId = "user-1", Status = "ACTIVE" };
        _sessionRepository.FindByIdAndUserIdAsync("s-1", "user-1", Arg.Any<CancellationToken>())
            .Returns(session);

        var messages = new List<ChatMessage>
        {
            new() { SessionId = "s-1", Role = "system", Content = "System Prompt" },
            new() { SessionId = "s-1", Role = "user", Content = "Hello" },
            new() { SessionId = "s-1", Role = "assistant", Content = "Hi" }
        };
        _messageRepository.FindBySessionIdAsync("s-1", Arg.Any<CancellationToken>()).Returns(messages);

        // Act
        var result = await _chatService.GetMessagesAsync("s-1", "user-1", 1, 50);

        // Assert
        result.Items.ShouldAllBe(m => m.Role != "system");
        result.TotalCount.ShouldBe(2);
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
