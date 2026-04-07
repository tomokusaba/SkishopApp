using System.Diagnostics.Metrics;
using MailSendService.Configurations;
using MailSendService.DTOs.Requests;
using MailSendService.DTOs.Responses;
using MailSendService.Exceptions;
using MailSendService.Infrastructure.Metrics;
using MailSendService.Models;
using MailSendService.Repositories.Interfaces;
using MailSendService.Services;
using MailSendService.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace MailSendService.Tests.Services;

/// <summary>
/// <see cref="MailService"/> の追加テスト。
/// RetryAsync、SendTestMailAsync、ProcessConsentRevokedAsync、
/// ProcessUserDeletedAsync、ProcessUserProcessingRestrictedAsync をカバーする。
/// </summary>
[Trait("Category", "Unit")]
public class MailServiceAdditionalTests
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

    public MailServiceAdditionalTests()
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

    // ─── RetryAsync ─────────────────────────────────────────────────

    /// <summary>
    /// リトライ送信が成功した場合に、ステータスが SENT に更新され RetryCount がインクリメントされることを検証する。
    /// </summary>
    /// <remarks>
    /// テンプレートの再レンダリングと Azure Email 送信の両方が成功するようモックをセットアップしている。
    /// </remarks>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_RetrySendAndUpdateStatusToSent_When_RetrySucceeds()
    {
        // Arrange
        var mailLog = new MailLog
        {
            Id = "log-1",
            EventType = "order.created",
            EventId = "ev-1",
            RecipientEmail = "user@example.com",
            RecipientName = "User",
            TemplateName = "order-confirmation",
            Subject = "Order Confirmation",
            Status = MailLogStatus.Failed,
            RetryCount = 0
        };
        _mailLogRepository.FindByIdAsync("log-1", Arg.Any<CancellationToken>())
            .Returns(mailLog);
        _templateService.RenderAsync("order-confirmation", Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(new RenderedMail("Order Confirmation", "<p>Order details</p>", "Order details"));
        _emailSender.SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<IReadOnlyList<EmailAttachmentInfo>?>(), Arg.Any<CancellationToken>())
            .Returns("op-retry-1");

        // Act
        var result = await _sut.RetryAsync("log-1", default);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(MailLogStatus.Sent);
        mailLog.RetryCount.ShouldBe(1);
        mailLog.AzureOperationId.ShouldBe("op-retry-1");
        await _mailLogRepository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// リトライ時にメール送信が例外をスローした場合に、ステータスが FAILED に設定されエラーメッセージが記録されることを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_SetStatusToFailed_When_RetrySendFails()
    {
        // Arrange
        var mailLog = new MailLog
        {
            Id = "log-2",
            EventType = "order.created",
            EventId = "ev-2",
            RecipientEmail = "user@example.com",
            TemplateName = "order-confirmation",
            Subject = "Order Confirmation",
            Status = MailLogStatus.Failed,
            RetryCount = 0
        };
        _mailLogRepository.FindByIdAsync("log-2", Arg.Any<CancellationToken>())
            .Returns(mailLog);
        _templateService.RenderAsync("order-confirmation", Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(new RenderedMail("Order Confirmation", "<p>Body</p>", null));
        _emailSender.SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<IReadOnlyList<EmailAttachmentInfo>?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("SMTP timeout"));

        // Act
        var result = await _sut.RetryAsync("log-2", default);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(MailLogStatus.Failed);
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage!.ShouldContain("SMTP timeout");
        mailLog.RetryCount.ShouldBe(1);
    }

    /// <summary>
    /// 存在しないメールログ ID でリトライを試行した場合に、<see cref="MailLogNotFoundException"/> がスローされることを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowMailLogNotFoundException_When_RetryWithInvalidId()
    {
        // Arrange
        _mailLogRepository.FindByIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((MailLog?)null);

        // Act & Assert
        var act = async () => await _sut.RetryAsync("nonexistent", default);
        var ex = await Should.ThrowAsync<MailLogNotFoundException>(act);
        ex.Message.ShouldContain("nonexistent");
    }

    /// <summary>
    /// FAILED 以外のステータス（例: SENT）のメールログに対してリトライを試行した場合に、
    /// <see cref="InvalidMailStatusException"/> がスローされることを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowInvalidMailStatusException_When_RetryNonFailedMail()
    {
        // Arrange
        var mailLog = new MailLog
        {
            Id = "log-3",
            Status = MailLogStatus.Sent,
            EventType = "test",
            EventId = "ev-3",
            RecipientEmail = "user@example.com",
            TemplateName = "welcome",
            Subject = "Welcome"
        };
        _mailLogRepository.FindByIdAsync("log-3", Arg.Any<CancellationToken>())
            .Returns(mailLog);

        // Act & Assert
        var act = async () => await _sut.RetryAsync("log-3", default);
        await Should.ThrowAsync<InvalidMailStatusException>(act);
    }

    /// <summary>
    /// リトライ回数が上限（MaxRetryCount）を超過している場合に、
    /// <see cref="RateLimitExceededException"/> がスローされることを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowRateLimitExceeded_When_RetryCountExceedsMax()
    {
        // Arrange
        var mailLog = new MailLog
        {
            Id = "log-4",
            Status = MailLogStatus.Failed,
            RetryCount = 3,
            EventType = "test",
            EventId = "ev-4",
            RecipientEmail = "user@example.com",
            TemplateName = "welcome",
            Subject = "Welcome"
        };
        _mailLogRepository.FindByIdAsync("log-4", Arg.Any<CancellationToken>())
            .Returns(mailLog);

        // Act & Assert
        var act = async () => await _sut.RetryAsync("log-4", default);
        await Should.ThrowAsync<RateLimitExceededException>(act);
    }

    // ─── SendTestMailAsync ──────────────────────────────────────────

    /// <summary>
    /// 有効なテストメールリクエストを受信した場合に、テンプレートレンダリング→メール送信が正常完了し SENT ステータスが返ることを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_SendTestMail_When_ValidRequest()
    {
        // Arrange
        var request = new TestMailRequest("test@example.com", "welcome", new Dictionary<string, object> { ["name"] = "Tester" });
        _templateService.RenderAsync("welcome", Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(new RenderedMail("Welcome Tester", "<p>Hello Tester</p>", "Hello Tester"));
        _emailSender.SendAsync(
            "test@example.com", Arg.Any<string>(), "Welcome Tester",
            "<p>Hello Tester</p>", "Hello Tester",
            Arg.Any<IReadOnlyList<EmailAttachmentInfo>?>(), Arg.Any<CancellationToken>())
            .Returns("op-test-1");

        // Act
        var result = await _sut.SendTestMailAsync(request, default);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(MailLogStatus.Sent);
        result.EventType.ShouldBe("test.mail");
        result.RecipientEmail.ShouldBe("test@example.com");
        await _emailSender.Received(1).SendAsync(
            "test@example.com", Arg.Any<string>(), "Welcome Tester",
            "<p>Hello Tester</p>", "Hello Tester",
            Arg.Any<IReadOnlyList<EmailAttachmentInfo>?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// テストメール送信時にメール送信処理が例外をスローした場合に、ステータスが FAILED に設定されエラーメッセージが記録されることを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_SetStatusToFailed_When_TestMailSendThrows()
    {
        // Arrange
        var request = new TestMailRequest("test@example.com", "welcome", null);
        _templateService.RenderAsync("welcome", Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(new RenderedMail("Welcome", "<p>Hi</p>", null));
        _emailSender.SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<IReadOnlyList<EmailAttachmentInfo>?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Email service unavailable"));

        // Act
        var result = await _sut.SendTestMailAsync(request, default);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(MailLogStatus.Failed);
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage!.ShouldContain("Email service unavailable");
    }

    /// <summary>
    /// テストメールリクエストの Variables が null の場合に、空の変数辞書でテンプレートがレンダリングされ正常送信されることを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_UseEmptyVariables_When_TestMailRequestHasNullVariables()
    {
        // Arrange
        var request = new TestMailRequest("test@example.com", "welcome", null);
        _templateService.RenderAsync("welcome", Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .Returns(new RenderedMail("Welcome", "<p>Hi</p>", null));
        _emailSender.SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<IReadOnlyList<EmailAttachmentInfo>?>(), Arg.Any<CancellationToken>())
            .Returns("op-test-2");

        // Act
        var result = await _sut.SendTestMailAsync(request, default);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(MailLogStatus.Sent);
        await _templateService.Received(1).RenderAsync("welcome", Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>());
    }

    // ─── ProcessConsentRevokedAsync ─────────────────────────────────

    /// <summary>
    /// マーケティング同意が撤回された場合に、UNSUBSCRIBE 抑制レコードが追加されることを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_AddSuppression_When_MarketingConsentRevoked()
    {
        // Arrange
        var userId = "user-consent-1";
        _mailLogRepository.FindEmailByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns("consent@example.com");
        _suppressionRepository.FindByEmailAndReasonAsync("consent@example.com", SuppressionReason.Unsubscribe, Arg.Any<CancellationToken>())
            .Returns((MailSuppression?)null);

        // Act
        await _sut.ProcessConsentRevokedAsync(userId, "marketing", default);

        // Assert
        await _suppressionRepository.Received(1).AddAsync(
            Arg.Is<MailSuppression>(s => s.Email == "consent@example.com" && s.Reason == SuppressionReason.Unsubscribe),
            Arg.Any<CancellationToken>());
        await _suppressionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// マーケティング以外の同意種別（例: analytics）が撤回された場合に、抑制レコードが追加されないことを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_SkipSuppression_When_NonMarketingConsentRevoked()
    {
        // Arrange & Act
        await _sut.ProcessConsentRevokedAsync("user-1", "analytics", default);

        // Assert
        await _suppressionRepository.DidNotReceive().AddAsync(Arg.Any<MailSuppression>(), Arg.Any<CancellationToken>());
        await _suppressionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// 同意撤回時にユーザーのメールアドレスが見つからない場合に、抑制レコードが追加されないことを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_SkipSuppression_When_EmailNotFoundForConsentRevoked()
    {
        // Arrange
        _mailLogRepository.FindEmailByUserIdAsync("unknown-user", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        // Act
        await _sut.ProcessConsentRevokedAsync("unknown-user", "marketing", default);

        // Assert
        await _suppressionRepository.DidNotReceive().AddAsync(Arg.Any<MailSuppression>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// 同意撤回時に同一メールアドレス・同一理由の抑制レコードが既に存在する場合に、重複追加されないことを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_SkipDuplicate_When_SuppressionAlreadyExists()
    {
        // Arrange
        var userId = "user-dup";
        _mailLogRepository.FindEmailByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns("dup@example.com");
        _suppressionRepository.FindByEmailAndReasonAsync("dup@example.com", SuppressionReason.Unsubscribe, Arg.Any<CancellationToken>())
            .Returns(new MailSuppression { Email = "dup@example.com", Reason = SuppressionReason.Unsubscribe });

        // Act
        await _sut.ProcessConsentRevokedAsync(userId, "marketing", default);

        // Assert
        await _suppressionRepository.DidNotReceive().AddAsync(Arg.Any<MailSuppression>(), Arg.Any<CancellationToken>());
    }

    // ─── ProcessUserDeletedAsync ────────────────────────────────────

    /// <summary>
    /// ユーザー削除時に、メールログの PII（氏名・メールアドレス・ユーザー ID）が匿名化され、
    /// PENDING ステータスのログが SKIPPED に変更されることを検証する。
    /// </summary>
    /// <remarks>
    /// SENT ステータスのログはステータスが維持され、PENDING のログのみ SKIPPED + エラーメッセージ "User deleted" が設定される。
    /// </remarks>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_AnonymizePiiAndSkipPending_When_UserDeleted()
    {
        // Arrange
        var userId = "user-del-1";
        var logs = new List<MailLog>
        {
            new()
            {
                RecipientEmail = "deleted@example.com",
                RecipientName = "Deleted User",
                RecipientUserId = userId,
                Status = MailLogStatus.Sent,
                EventType = "order.created",
                EventId = "ev-d1",
                TemplateName = "order-confirmation",
                Subject = "Order"
            },
            new()
            {
                RecipientEmail = "deleted@example.com",
                RecipientName = "Deleted User",
                RecipientUserId = userId,
                Status = MailLogStatus.Pending,
                EventType = "order.shipped",
                EventId = "ev-d2",
                TemplateName = "shipment-notification",
                Subject = "Shipped"
            }
        };
        _mailLogRepository.FindByRecipientUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(logs);
        _suppressionRepository.FindByUserEmailAsync("deleted@example.com", Arg.Any<CancellationToken>())
            .Returns(new List<MailSuppression>());

        // Act
        await _sut.ProcessUserDeletedAsync(userId, default);

        // Assert
        logs[0].RecipientName.ShouldBeNull();
        logs[0].RecipientUserId.ShouldBeNull();
        logs[0].RecipientEmail.ShouldContain("@anonymized.local");
        logs[0].Status.ShouldBe(MailLogStatus.Sent);

        logs[1].RecipientName.ShouldBeNull();
        logs[1].RecipientUserId.ShouldBeNull();
        logs[1].RecipientEmail.ShouldContain("@anonymized.local");
        logs[1].Status.ShouldBe(MailLogStatus.Skipped);
        logs[1].ErrorMessage.ShouldBe("User deleted");

        await _mailLogRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// ユーザー削除時に、該当ユーザーのメール抑制レコードも併せて削除されることを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_RemoveSuppressions_When_UserDeletedHasSuppressions()
    {
        // Arrange
        var userId = "user-del-2";
        var logs = new List<MailLog>
        {
            new()
            {
                RecipientEmail = "sup@example.com",
                RecipientName = "Suppressed",
                RecipientUserId = userId,
                Status = MailLogStatus.Sent,
                EventType = "test",
                EventId = "ev-s1",
                TemplateName = "welcome",
                Subject = "Welcome"
            }
        };
        var suppressions = new List<MailSuppression>
        {
            new() { Email = "sup@example.com", Reason = SuppressionReason.Unsubscribe }
        };
        _mailLogRepository.FindByRecipientUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(logs);
        _suppressionRepository.FindByUserEmailAsync("sup@example.com", Arg.Any<CancellationToken>())
            .Returns(suppressions);

        // Act
        await _sut.ProcessUserDeletedAsync(userId, default);

        // Assert
        await _suppressionRepository.Received(1).RemoveAsync(
            Arg.Is<MailSuppression>(s => s.Email == "sup@example.com"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// ユーザー削除時に、該当ユーザーのメールログが存在しない場合に SaveChangesAsync が呼び出されないことを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_DoNothing_When_UserDeletedHasNoLogs()
    {
        // Arrange
        _mailLogRepository.FindByRecipientUserIdAsync("no-logs-user", Arg.Any<CancellationToken>())
            .Returns(new List<MailLog>());

        // Act
        await _sut.ProcessUserDeletedAsync("no-logs-user", default);

        // Assert
        await _mailLogRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ─── ProcessUserProcessingRestrictedAsync ───────────────────────

    /// <summary>
    /// ユーザーの処理制限要求を受信した場合に、UNSUBSCRIBE 抑制レコードが追加されることを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_AddSuppression_When_ProcessingRestricted()
    {
        // Arrange
        var userId = "user-restrict-1";
        _mailLogRepository.FindEmailByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns("restricted@example.com");
        _suppressionRepository.FindByEmailAndReasonAsync("restricted@example.com", SuppressionReason.Unsubscribe, Arg.Any<CancellationToken>())
            .Returns((MailSuppression?)null);

        // Act
        await _sut.ProcessUserProcessingRestrictedAsync(userId, default);

        // Assert
        await _suppressionRepository.Received(1).AddAsync(
            Arg.Is<MailSuppression>(s => s.Email == "restricted@example.com" && s.Reason == SuppressionReason.Unsubscribe),
            Arg.Any<CancellationToken>());
        await _suppressionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// 処理制限時にユーザーのメールアドレスが見つからない場合に、抑制レコードが追加されないことを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_SkipSuppression_When_ProcessingRestrictedEmailNotFound()
    {
        // Arrange
        _mailLogRepository.FindEmailByUserIdAsync("unknown", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        // Act
        await _sut.ProcessUserProcessingRestrictedAsync("unknown", default);

        // Assert
        await _suppressionRepository.DidNotReceive().AddAsync(Arg.Any<MailSuppression>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// 処理制限時に同一メールアドレスの抑制レコードが既に存在する場合に、重複追加されないことを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_SkipSuppression_When_ProcessingRestrictedAlreadySuppressed()
    {
        // Arrange
        var userId = "user-already-sup";
        _mailLogRepository.FindEmailByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns("already@example.com");
        _suppressionRepository.FindByEmailAndReasonAsync("already@example.com", SuppressionReason.Unsubscribe, Arg.Any<CancellationToken>())
            .Returns(new MailSuppression { Email = "already@example.com", Reason = SuppressionReason.Unsubscribe });

        // Act
        await _sut.ProcessUserProcessingRestrictedAsync(userId, default);

        // Assert
        await _suppressionRepository.DidNotReceive().AddAsync(Arg.Any<MailSuppression>(), Arg.Any<CancellationToken>());
    }

    // ─── CancellationToken ──────────────────────────────────────────

    /// <summary>
    /// RetryAsync の CancellationToken がキャンセル済みの場合に、<see cref="OperationCanceledException"/> がスローされることを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowOperationCanceled_When_RetryTokenCanceled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        _mailLogRepository.FindByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        var act = async () => await _sut.RetryAsync("log-cancel", cts.Token);
        await Should.ThrowAsync<OperationCanceledException>(act);
    }

    /// <summary>
    /// SendTestMailAsync の CancellationToken がキャンセル済みの場合に、<see cref="OperationCanceledException"/> がスローされることを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowOperationCanceled_When_SendTestMailTokenCanceled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        _templateService.RenderAsync(Arg.Any<string>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var request = new TestMailRequest("test@example.com", "welcome", null);

        // Act & Assert
        var act = async () => await _sut.SendTestMailAsync(request, cts.Token);
        await Should.ThrowAsync<OperationCanceledException>(act);
    }
}
