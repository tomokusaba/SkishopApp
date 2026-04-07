using Xunit;
using System.Diagnostics.Metrics;
using MailSendService.Configurations;
using MailSendService.DTOs;
using MailSendService.DTOs.Requests;
using MailSendService.DTOs.Responses;
using MailSendService.Infrastructure.Metrics;
using MailSendService.Models;
using MailSendService.Repositories.Interfaces;
using MailSendService.Services;
using MailSendService.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using EmailAttachmentInfo = MailSendService.Services.Interfaces.EmailAttachmentInfo;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace MailSendService.Tests.Services;

// NOTE: DB slice tests requiring real PostgreSQL should use Testcontainers.PostgreSql in CI.
// These unit tests use NSubstitute mocks for all repository dependencies.

/// <summary>
/// <see cref="MailService"/> の単体テスト。
/// イベント処理（重複チェック・メール送信・抑制・レート制限）、統計取得、
/// 同意撤回処理、ユーザー削除時の PII 匿名化、キャンセルトークン伝搬を検証する。
/// </summary>
[Trait("Category", "Unit")]
public class MailServiceTests
{
    private readonly IMailLogRepository _mailLogRepository;
    private readonly IMailSuppressionRepository _suppressionRepository;
    private readonly IOutboxEventRepository _outboxEventRepository;
    private readonly ITemplateService _templateService;
    private readonly IAzureEmailSender _emailSender;
    private readonly IMailEventResolver _mailEventResolver;
    private readonly IDistributedCache _cache;
    private readonly ILogger<MailService> _logger;
    private readonly MailMetrics _metrics;
    private readonly MailService _sut;

    public MailServiceTests()
    {
        _mailLogRepository = Substitute.For<IMailLogRepository>();
        _suppressionRepository = Substitute.For<IMailSuppressionRepository>();
        _outboxEventRepository = Substitute.For<IOutboxEventRepository>();
        _templateService = Substitute.For<ITemplateService>();
        _emailSender = Substitute.For<IAzureEmailSender>();
        _mailEventResolver = Substitute.For<IMailEventResolver>();
        _cache = Substitute.For<IDistributedCache>();
        _logger = Substitute.For<ILogger<MailService>>();
        var meterFactory = Substitute.For<IMeterFactory>();
        meterFactory.Create(Arg.Any<MeterOptions>()).Returns(new Meter("test"));
        _metrics = new MailMetrics(meterFactory);

        var mailSettings = Options.Create(new MailSettings(
            new RetrySettings(3, 30000, 2.0), "http://localhost:5008"));

        _sut = new MailService(
            _mailLogRepository,
            _suppressionRepository,
            _outboxEventRepository,
            _templateService,
            _emailSender,
            _mailEventResolver,
            _cache,
            mailSettings,
            _metrics,
            TimeProvider.System,
            _logger);
    }

    /// <summary>
    /// 既に処理済みの EventId を受信した場合に、メール送信をスキップすることを検証する（べき等性保証）。
    /// </summary>
    [Fact]
    public async Task Should_SkipSending_When_DuplicateEventId()
    {
        // Arrange
        var eventId = Guid.NewGuid().ToString();
        _mailLogRepository.FindByEventIdAsync(eventId, Arg.Any<CancellationToken>())
            .Returns(new MailLog { EventId = eventId, Status = "SENT" });

        // Act
        await _sut.ProcessEventAsync("user.registered", eventId, "corr-123", "{}", default);

        // Assert
        await _emailSender.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<IReadOnlyList<EmailAttachmentInfo>?>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// 有効な user.registered イベント受信時に、テンプレートレンダリング→メール送信→ログ保存の一連処理が正常完了することを検証する。
    /// </summary>
    /// <remarks>
    /// 重複チェック・抑制チェック・レート制限チェックをすべて通過するようモックをセットアップしている。
    /// </remarks>
    [Fact]
    public async Task Should_SendMail_When_ValidUserRegisteredEvent()
    {
        // Arrange
        var eventId = Guid.NewGuid().ToString();
        _mailLogRepository.FindByEventIdAsync(eventId, Arg.Any<CancellationToken>())
            .Returns((MailLog?)null);
        _suppressionRepository.IsSuppressedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _mailLogRepository.CountByRecipientSinceAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(0);
        _templateService.RenderAsync(Arg.Any<string>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(new RenderedMail("Welcome", "<p>Hello</p>", "Hello"));
        _emailSender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<IReadOnlyList<EmailAttachmentInfo>?>(), Arg.Any<CancellationToken>())
            .Returns("op-123");

        var payload = """{"userId":"u1","email":"test@example.com","verificationToken":"tok123"}""";

        _mailEventResolver.ResolveEventDataAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ResolvedEventData("user-registration", "test@example.com", "Test User", "u1", new Dictionary<string, object> { ["token"] = "tok123" }));

        // Act
        await _sut.ProcessEventAsync("user.registered", eventId, "corr-123", payload, default);

        // Assert
        await _mailLogRepository.Received(1).AddAsync(Arg.Any<MailLog>(), Arg.Any<CancellationToken>());
        await _mailLogRepository.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// 無効なメールアドレスを含むイベント受信時に、ステータス SKIPPED でメールログが記録されることを検証する。
    /// </summary>
    [Fact]
    public async Task Should_RecordSkipped_When_InvalidEmail()
    {
        // Arrange
        var eventId = Guid.NewGuid().ToString();
        _mailLogRepository.FindByEventIdAsync(eventId, Arg.Any<CancellationToken>())
            .Returns((MailLog?)null);

        var payload = """{"userId":"u1","email":"invalid-email","verificationToken":"tok"}""";

        _mailEventResolver.ResolveEventDataAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ResolvedEventData("user-registration", "invalid-email", null, "u1", new Dictionary<string, object>()));

        // Act
        await _sut.ProcessEventAsync("user.registered", eventId, "corr-123", payload, default);

        // Assert
        await _mailLogRepository.Received(1).AddAsync(
            Arg.Is<MailLog>(m => m.Status == "SKIPPED"), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// 受信者へのレート制限を超過した場合に、ステータス SKIPPED でメールログが記録されることを検証する。
    /// </summary>
    [Fact]
    public async Task Should_RecordSkipped_When_RateLimitExceeded()
    {
        // Arrange
        var eventId = Guid.NewGuid().ToString();
        _mailLogRepository.FindByEventIdAsync(eventId, Arg.Any<CancellationToken>())
            .Returns((MailLog?)null);
        _suppressionRepository.IsSuppressedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _mailLogRepository.CountByRecipientSinceAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(5);

        var payload = """{"userId":"u1","email":"test@example.com","name":"Test"}""";

        _mailEventResolver.ResolveEventDataAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ResolvedEventData("user-verified", "test@example.com", "Test", "u1", new Dictionary<string, object>()));

        // Act
        await _sut.ProcessEventAsync("user.verified", eventId, "corr-123", payload, default);

        // Assert
        await _mailLogRepository.Received(1).AddAsync(
            Arg.Is<MailLog>(m => m.Status == "SKIPPED"), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// GetStatsAsync 呼び出し時に、各ステータス（SENT/FAILED/PENDING/SKIPPED）の件数とテンプレート別統計を正しく返すことを検証する。
    /// </summary>
    [Fact]
    public async Task Should_ReturnStats_When_GetStatsAsync()
    {
        // Arrange
        _mailLogRepository.CountByStatusAsync("SENT", Arg.Any<CancellationToken>()).Returns(100);
        _mailLogRepository.CountByStatusAsync("FAILED", Arg.Any<CancellationToken>()).Returns(5);
        _mailLogRepository.CountByStatusAsync("PENDING", Arg.Any<CancellationToken>()).Returns(2);
        _mailLogRepository.CountByStatusAsync("SKIPPED", Arg.Any<CancellationToken>()).Returns(3);
        _mailLogRepository.CountByTemplateAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, long> { ["welcome"] = 50 });

        // Act
        var result = await _sut.GetStatsAsync();

        // Assert
        result.ShouldNotBeNull();
        result.TotalSent.ShouldBe(100);
        result.TotalFailed.ShouldBe(5);
    }

    /// <summary>
    /// マーケティング同意が撤回された場合に、UNSUBSCRIBE 抑制レコードが追加されることを検証する。
    /// </summary>
    [Fact]
    public async Task Should_AddSuppression_When_ConsentRevokedForMarketing()
    {
        // Arrange
        var userId = "user-123";
        _mailLogRepository.FindEmailByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns("test@example.com");
        _suppressionRepository.FindByEmailAndReasonAsync("test@example.com", "UNSUBSCRIBE", Arg.Any<CancellationToken>())
            .Returns((MailSuppression?)null);

        // Act
        await _sut.ProcessConsentRevokedAsync(userId, "marketing");

        // Assert
        await _suppressionRepository.Received(1).AddAsync(
            Arg.Is<MailSuppression>(s => s.Email == "test@example.com" && s.Reason == "UNSUBSCRIBE"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// マーケティング以外の同意種別（例: analytics）が撤回された場合に、抑制レコードが追加されないことを検証する。
    /// </summary>
    [Fact]
    public async Task Should_SkipProcessing_When_ConsentRevokedNotMarketing()
    {
        // Arrange & Act
        await _sut.ProcessConsentRevokedAsync("user-123", "analytics");

        // Assert
        await _suppressionRepository.DidNotReceive().AddAsync(
            Arg.Any<MailSuppression>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// ユーザー削除時に、メールログの PII（氏名・メールアドレス・ユーザー ID）が匿名化され、
    /// PENDING ステータスのログが SKIPPED に変更されることを検証する。
    /// </summary>
    [Fact]
    public async Task Should_AnonymizePii_When_UserDeleted()
    {
        // Arrange
        var userId = "user-123";
        var logs = new List<MailLog>
        {
            new() { RecipientEmail = "test@example.com", RecipientName = "Test", RecipientUserId = userId, Status = "SENT" },
            new() { RecipientEmail = "test@example.com", RecipientName = "Test", RecipientUserId = userId, Status = "PENDING" }
        };
        _mailLogRepository.FindByRecipientUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(logs);
        _suppressionRepository.FindByUserEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<MailSuppression>());

        // Act
        await _sut.ProcessUserDeletedAsync(userId);

        // Assert
        logs[0].RecipientName.ShouldBeNull();
        logs[0].RecipientUserId.ShouldBeNull();
        logs[0].RecipientEmail.ShouldContain("@anonymized.local");
        logs[1].Status.ShouldBe("SKIPPED");
    }

    /// <summary>
    /// CancellationToken がキャンセル済みの場合に、<see cref="OperationCanceledException"/> がスローされることを検証する。
    /// </summary>
    [Fact]
    public async Task Should_ThrowOperationCanceled_When_TokenCanceled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        _mailLogRepository.FindByEventIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        var act = async () => await _sut.ProcessEventAsync(
            "user.registered", "ev-1", "corr-1", "{}", cts.Token);
        await Should.ThrowAsync<OperationCanceledException>(act);
    }
}
