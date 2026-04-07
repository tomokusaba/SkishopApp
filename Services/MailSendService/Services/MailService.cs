using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MailSendService.Configurations;
using MailSendService.DTOs.Requests;
using MailSendService.DTOs.Responses;
using MailSendService.Exceptions;
using MailSendService.Infrastructure.Metrics;
using MailSendService.Models;
using MailSendService.Repositories.Interfaces;
using MailSendService.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace MailSendService.Services;

/// <summary>
/// メール送信サービスの中核オーケストレーター。
/// Kafka イベントの受信・処理、テストメール送信、送信リトライ、
/// 送信ログ管理、および GDPR 関連操作（同意撤回・PII 匿名化・処理制限）を統括する。
/// </summary>
/// <remarks>
/// <para>イベント処理は Outbox パターンによりメール送信と DB 更新のトランザクション整合性を保証する。</para>
/// <para>抑制チェックには Redis キャッシュ（1 分 TTL）を使用し、DB アクセスを最小化する。</para>
/// <para>セキュリティメール（パスワードリセット、メール認証等）はレート制限を適用しない。</para>
/// </remarks>
public partial class MailService(
    IMailLogRepository mailLogRepository,
    IMailSuppressionRepository suppressionRepository,
    IOutboxEventRepository outboxEventRepository,
    ITemplateService templateService,
    IAzureEmailSender emailSender,
    IMailEventResolver mailEventResolver,
    IDistributedCache cache,
    IOptions<MailSettings> mailOptions,
    MailMetrics metrics,
    TimeProvider timeProvider,
    IGdprComplianceService gdprComplianceService,
    IMailStatsService mailStatsService,
    ILogger<MailService> logger) : IMailService
{
    /// <summary>
    /// セキュリティ関連のテンプレート名セット。
    /// これらのテンプレートに該当するメールはレート制限（1 時間あたり 5 通）の対象外とする。
    /// </summary>
    private static readonly HashSet<string> SecurityEmails = ["password-reset", "email-verification", "email-change-verification"];

    /// <summary>
    /// Kafka イベントを受信し、べき等にメール送信処理を実行する。
    /// </summary>
    /// <param name="eventType">イベント種別（例: "user.registered", "order.created"）。</param>
    /// <param name="eventId">イベントの一意識別子。重複排除に使用する。</param>
    /// <param name="correlationId">分散トレーシング用の相関 ID。</param>
    /// <param name="payload">イベントの JSON ペイロード。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    /// <remarks>
    /// <para>処理フロー: 重複排除 → イベントデータ解決 → メールアドレス検証 → 抑制チェック → レート制限 → テンプレートレンダリング → 送信 → Outbox イベント記録。</para>
    /// <para>Outbox パターンにより、MailLog の保存と OutboxEvent の発行を同一トランザクションで実行する。</para>
    /// <para>セキュリティメール（パスワードリセット等）はレート制限をバイパスする。</para>
    /// </remarks>
    /// <exception cref="EventDeserializationException">サポートされていないイベント種別またはペイロードの解析に失敗した場合。</exception>
    /// <exception cref="TemplateNotFoundException">指定されたテンプレートが見つからない場合。</exception>
    /// <exception cref="UserInfoResolutionException">注文・配送イベントでユーザー情報の解決に失敗した場合。</exception>
    public async Task ProcessEventAsync(string eventType, string eventId, string correlationId,
        string payload, CancellationToken ct = default)
    {
        var existing = await mailLogRepository.FindByEventIdAsync(eventId, ct);
        if (existing is not null)
        {
            logger.LogInformation("Duplicate event skipped: {EventId}", eventId);
            return;
        }

        LogMailProcessingStarted(eventType, correlationId);

        var resolved = await mailEventResolver.ResolveEventDataAsync(eventType, payload, correlationId, ct);
        var (templateName, recipientEmail, recipientName, recipientUserId, variables) =
            (resolved.TemplateName, resolved.RecipientEmail, resolved.RecipientName,
             resolved.RecipientUserId, resolved.Variables);

        if (!IsValidEmail(recipientEmail))
        {
            await RecordSkippedAsync(eventType, eventId, correlationId, recipientEmail,
                recipientUserId, templateName, "Invalid email address", ct);
            metrics.RecordMailSkipped(eventType, "invalid_email");
            return;
        }

        if (await IsSuppressionCachedAsync(recipientEmail, ct))
        {
            LogMailSuppressed(MaskEmailForLog(recipientEmail), "Suppressed recipient");
            await RecordSkippedAsync(eventType, eventId, correlationId, recipientEmail,
                recipientUserId, templateName, "Suppressed recipient", ct);
            metrics.RecordMailSkipped(eventType, "suppressed");
            return;
        }

        if (!SecurityEmails.Contains(templateName))
        {
            var oneHourAgo = timeProvider.GetUtcNow().AddHours(-1);
            var recentCount = await mailLogRepository
                .CountByRecipientSinceAsync(recipientEmail, oneHourAgo, ct);
            if (recentCount >= 5)
            {
                await RecordSkippedAsync(eventType, eventId, correlationId, recipientEmail,
                    recipientUserId, templateName, "Rate limit exceeded (5/hour)", ct);
                metrics.RecordMailSkipped(eventType, "rate_limited");
                return;
            }
        }

        var rendered = await templateService.RenderAsync(templateName, variables, ct);

        await using var transaction = await outboxEventRepository.BeginTransactionAsync(ct);
        try
        {
            var mailLog = new MailLog
            {
                EventType = eventType,
                EventId = eventId,
                CorrelationId = correlationId,
                RecipientEmail = recipientEmail,
                RecipientName = recipientName,
                RecipientUserId = recipientUserId,
                TemplateName = templateName,
                Subject = rendered.Subject,
                TemplateVariablesJson = JsonSerializer.Serialize(variables)
            };
            // DDD: PENDING → SENDING 遷移
            mailLog.MarkAsSending();
            await mailLogRepository.AddAsync(mailLog, ct);
            await mailLogRepository.SaveChangesAsync(ct);

            var sw = Stopwatch.StartNew();
            try
            {
                var operationId = await emailSender.SendAsync(
                    recipientEmail, recipientName ?? string.Empty,
                    rendered.Subject, rendered.HtmlBody,
                    rendered.PlainTextBody, ct: ct);

                // DDD: SENDING → SENT 遷移
                mailLog.MarkAsSent(operationId, timeProvider.GetUtcNow());
                metrics.RecordMailSent(eventType);
                LogMailSent(mailLog.Id, MaskEmailForLog(recipientEmail));
            }
            catch (Exception ex)
            {
                LogMailSendFailed(ex, eventType, correlationId);
                // DDD: SENDING → FAILED 遷移
                mailLog.MarkAsFailed(ex.Message);
                metrics.RecordMailFailed(eventType, ex.GetType().Name);
            }
            finally
            {
                sw.Stop();
                metrics.RecordSendDuration(sw.Elapsed.TotalMilliseconds, eventType);
            }

            await mailLogRepository.SaveChangesAsync(ct);

            var outboxEvent = new OutboxEvent
            {
                EventType = mailLog.Status == MailLogStatus.Sent ? "mail.sent" : "mail.send.failed",
                Payload = JsonSerializer.Serialize(new
                {
                    mailLog.Id,
                    RecipientEmail = MaskEmailForOutbox(mailLog.RecipientEmail),
                    mailLog.TemplateName,
                    mailLog.Status,
                    mailLog.ErrorMessage,
                    OccurredAt = timeProvider.GetUtcNow()
                })
            };
            await outboxEventRepository.AddAsync(outboxEvent, ct);

            try
            {
                await outboxEventRepository.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                logger.LogWarning(ex, "Concurrency conflict saving outbox event: {EventId}", eventId);
                throw new ConcurrencyException("データが他のユーザーによって更新されました。再度お試しください。", ex);
            }

            await outboxEventRepository.CommitTransactionAsync(transaction, ct);
        }
        catch
        {
            await outboxEventRepository.RollbackTransactionAsync(transaction, ct);
            throw;
        }
    }

    /// <summary>
    /// 管理者検証用のテストメールを送信する。
    /// </summary>
    /// <param name="request">テストメールリクエスト（宛先、テンプレート名、変数）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>送信結果を含むメールログレスポンス。</returns>
    /// <remarks>
    /// テストメールは抑制チェック・レート制限をバイパスし、イベント ID に "test-" プレフィックスを付与する。
    /// </remarks>
    /// <exception cref="TemplateNotFoundException">指定されたテンプレートが見つからない場合。</exception>
    public async Task<MailLogResponse> SendTestMailAsync(TestMailRequest request, CancellationToken ct = default)
    {
        var variables = request.Variables ?? [];
        var rendered = await templateService.RenderAsync(request.TemplateName, variables, ct);

        var mailLog = new MailLog
        {
            EventType = "test.mail",
            EventId = $"test-{Guid.NewGuid()}",
            RecipientEmail = request.RecipientEmail,
            TemplateName = request.TemplateName,
            Subject = rendered.Subject
        };
        // DDD: PENDING → SENDING 遷移
        mailLog.MarkAsSending();
        await mailLogRepository.AddAsync(mailLog, ct);
        await mailLogRepository.SaveChangesAsync(ct);

        try
        {
            var operationId = await emailSender.SendAsync(
                request.RecipientEmail, string.Empty,
                rendered.Subject, rendered.HtmlBody,
                rendered.PlainTextBody, ct: ct);

            // DDD: SENDING → SENT 遷移
            mailLog.MarkAsSent(operationId, timeProvider.GetUtcNow());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Test mail send failed for template: {TemplateName}", request.TemplateName);
            // DDD: SENDING → FAILED 遷移
            mailLog.MarkAsFailed(ex.Message);
        }

        await mailLogRepository.SaveChangesAsync(ct);
        return ToResponse(mailLog);
    }

    /// <summary>
    /// 送信失敗したメールを手動でリトライする。
    /// </summary>
    /// <param name="mailLogId">リトライ対象のメールログ ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>リトライ結果を含むメールログレスポンス。</returns>
    /// <remarks>
    /// リトライは失敗ステータスのメールのみ対象。リトライ回数が設定上限（MaxAttempts）に達している場合は拒否される。
    /// テンプレートの変数は空で再レンダリングされる。
    /// </remarks>
    /// <exception cref="MailLogNotFoundException">指定された ID のメールログが存在しない場合。</exception>
    /// <exception cref="InvalidMailStatusException">メールログのステータスが Failed でない場合。</exception>
    /// <exception cref="RateLimitExceededException">リトライ回数が上限に達している場合。</exception>
    public async Task<MailLogResponse> RetryAsync(string mailLogId, CancellationToken ct = default)
    {
        var mailLog = await mailLogRepository.FindByIdAsync(mailLogId, true, ct)
            ?? throw new MailLogNotFoundException(mailLogId);

        if (mailLog.Status is not MailLogStatus.Failed)
            throw new InvalidMailStatusException(mailLog.Status, MailLogStatus.Failed);

        var settings = mailOptions.Value;
        if (mailLog.RetryCount >= settings.Retry.MaxAttempts)
            throw new RateLimitExceededException();

        // DDD: FAILED → PENDING → SENDING 遷移
        mailLog.PrepareForRetry();
        mailLog.MarkAsSending();
        await mailLogRepository.SaveChangesAsync(ct);
        metrics.RecordMailRetry(mailLog.EventType);

        try
        {
            var retryVariables = DeserializeTemplateVariables(mailLog.TemplateVariablesJson);
            var rendered = await templateService.RenderAsync(mailLog.TemplateName, retryVariables, ct);
            var operationId = await emailSender.SendAsync(
                mailLog.RecipientEmail, mailLog.RecipientName ?? string.Empty,
                rendered.Subject, rendered.HtmlBody,
                rendered.PlainTextBody, ct: ct);

            // DDD: SENDING → SENT 遷移
            mailLog.MarkAsSent(operationId, timeProvider.GetUtcNow());
            metrics.RecordMailSent(mailLog.EventType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Retry failed: {MailLogId}", mailLogId);
            // DDD: SENDING → FAILED 遷移
            mailLog.MarkAsFailed(ex.Message);
            metrics.RecordMailFailed(mailLog.EventType, ex.GetType().Name);
        }

        await mailLogRepository.SaveChangesAsync(ct);
        return ToResponse(mailLog);
    }

    /// <summary>
    /// リトライ対象の失敗メールログを取得する。
    /// </summary>
    /// <param name="maxCount">取得する最大件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>失敗メールログのレスポンスリスト。</returns>
    public async Task<IReadOnlyList<MailLogResponse>> GetFailedMailsForRetryAsync(int maxCount, CancellationToken ct = default)
    {
        var retrySettings = mailOptions.Value.Retry;
        var failedLogs = await mailLogRepository.FindFailedForRetryAsync(retrySettings.MaxAttempts, maxCount, ct);
        return failedLogs.Select(ToResponse).ToList();
    }

    /// <summary>
    /// メール送信ログをページネーション付きで取得する。
    /// </summary>
    /// <param name="page">ページ番号（1 始まり）。</param>
    /// <param name="pageSize">1 ページあたりの件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>ページネーションされたメールログレスポンスのリスト。</returns>
    public async Task<PaginatedResult<MailLogResponse>> GetLogsAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var (items, totalCount) = await mailLogRepository.FindAllPagedAsync(page, pageSize, ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        return new PaginatedResult<MailLogResponse>(
            items.Select(ToResponse).ToList(), page, pageSize, totalCount, totalPages);
    }

    /// <summary>
    /// 指定された ID のメール送信ログを取得する。
    /// </summary>
    /// <param name="id">メールログ ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>メールログレスポンス。</returns>
    /// <exception cref="MailLogNotFoundException">指定された ID のメールログが存在しない場合。</exception>
    public async Task<MailLogResponse> GetLogByIdAsync(string id, CancellationToken ct = default)
    {
        var mailLog = await mailLogRepository.FindByIdAsync(id, trackChanges: false, ct)
            ?? throw new MailLogNotFoundException(id);
        return ToResponse(mailLog);
    }

    /// <summary>
    /// メール送信の統計情報（送信済み・失敗・保留件数、成功率、テンプレート別件数）を取得する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>メール統計レスポンス。</returns>
    /// <remarks>
    /// P2-2: MailStatsService に委譲。Redis キャッシュ（5 分 TTL）を使用して DB 負荷を軽減する。
    /// </remarks>
    public Task<MailStatsResponse> GetStatsAsync(CancellationToken ct = default)
        => mailStatsService.GetStatsAsync(ct);

    /// <summary>
    /// GDPR 同意撤回イベントを処理し、マーケティングメールの抑制を設定する。
    /// </summary>
    /// <param name="userId">同意を撤回したユーザーの ID。</param>
    /// <param name="consentType">撤回された同意の種別。"marketing" のみ処理対象。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    /// <remarks>
    /// P2-2: GdprComplianceService に委譲。consentType が "marketing" 以外の場合は何もしない。
    /// </remarks>
    public Task ProcessConsentRevokedAsync(string userId, string consentType, CancellationToken ct = default)
        => gdprComplianceService.ProcessConsentRevokedAsync(userId, consentType, ct);

    /// <summary>
    /// GDPR 忘れられる権利（Right to Erasure）に基づき、削除されたユーザーの PII を匿名化する。
    /// </summary>
    /// <param name="userId">削除されたユーザーの ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    /// <remarks>
    /// P2-2: GdprComplianceService に委譲。
    /// </remarks>
    public Task ProcessUserDeletedAsync(string userId, CancellationToken ct = default)
        => gdprComplianceService.ProcessUserDeletedAsync(userId, ct);

    /// <summary>
    /// GDPR 処理制限要求に基づき、対象ユーザーのメール送信を抑制する。
    /// </summary>
    /// <param name="userId">処理制限が要求されたユーザーの ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    /// <remarks>
    /// P2-2: GdprComplianceService に委譲。
    /// </remarks>
    public Task ProcessUserProcessingRestrictedAsync(string userId, CancellationToken ct = default)
        => gdprComplianceService.ProcessUserProcessingRestrictedAsync(userId, ct);

    /// <summary>
    /// GDPR 処理制限解除に基づき、対象ユーザーのメール送信抑制を解除する。
    /// </summary>
    /// <param name="userId">処理制限が解除されたユーザーの ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    /// <remarks>
    /// P2-2: GdprComplianceService に委譲。
    /// </remarks>
    public Task ProcessUserProcessingUnrestrictedAsync(string userId, CancellationToken ct = default)
        => gdprComplianceService.ProcessUserProcessingUnrestrictedAsync(userId, ct);

    /// <summary>
    /// メール送信をスキップした理由をメールログに記録する。
    /// </summary>
    private async Task RecordSkippedAsync(string eventType, string eventId, string correlationId,
        string recipientEmail, string? recipientUserId, string templateName, string reason,
        CancellationToken ct)
    {
        var mailLog = new MailLog
        {
            EventType = eventType,
            EventId = eventId,
            CorrelationId = correlationId,
            RecipientEmail = recipientEmail,
            RecipientUserId = recipientUserId,
            TemplateName = templateName,
            Subject = string.Empty
        };
        // DDD: PENDING → SKIPPED 遷移
        mailLog.MarkAsSkipped(reason);
        await mailLogRepository.AddAsync(mailLog, ct);
        await mailLogRepository.SaveChangesAsync(ct);
        logger.LogInformation("Mail skipped: {EventId}, Reason: {Reason}", eventId, reason);
    }

    /// <summary>
    /// MailLog エンティティを MailLogResponse DTO に変換する。
    /// </summary>
    private static MailLogResponse ToResponse(MailLog log)
        => new(log.Id, log.EventType, log.RecipientEmail, log.RecipientName,
            log.TemplateName, log.Subject, log.Status, log.RetryCount,
            log.ErrorMessage, log.SentAt, log.CreatedAt);

    /// <summary>
    /// メールアドレスの簡易バリデーション（@ と . を含むかチェック）。
    /// </summary>
    private static bool IsValidEmail(string email)
        => !string.IsNullOrWhiteSpace(email) && email.Contains('@') && email.Contains('.');

    /// <summary>
    /// ログ出力用にメールアドレスをマスキングする（PII 保護 — GDPR 準拠）。
    /// </summary>
    /// <remarks>
    /// ローカルパートの先頭 1 文字を残し残りを "***" に置換する（例: "t***@example.com"）。
    /// @ が含まれない場合は全体をマスキングする。
    /// </remarks>
    private static string MaskEmailForLog(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
            return "***";
        return $"{email[0]}***{email[atIndex..]}";
    }

    /// <summary>
    /// Outbox ペイロード用にメールアドレスをマスキングする（PII 保護）。
    /// </summary>
    /// <remarks>
    /// ローカルパートの先頭 1 文字を残し残りを "***" に置換する（例: "t***@example.com"）。
    /// @ が含まれない場合は全体をマスキングする。
    /// </remarks>
    private static string MaskEmailForOutbox(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
            return "***";
        return $"{email[0]}***{email[atIndex..]}";
    }

    /// <summary>
    /// 入力文字列の SHA-256 ハッシュ値（小文字 16 進数）を計算する。PII 匿名化および抑制キャッシュキーに使用する。
    /// </summary>
    private static string ComputeSha256Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>
    /// 受信者メールアドレスが送信抑制リストに含まれているかを Redis キャッシュ経由で確認する。
    /// </summary>
    /// <remarks>
    /// キャッシュキーにはメールアドレスの SHA-256 ハッシュの先頭 16 文字を使用し、PII を保護する。
    /// キャッシュミス時は DB を照会し、結果を 1 分間の TTL で Redis にキャッシュする。
    /// キャッシュ値: "1" = 抑制中、"0" = 非抑制。
    /// </remarks>
    private async Task<bool> IsSuppressionCachedAsync(string email, CancellationToken ct)
    {
        var emailHash = ComputeSha256Hash(email.ToLowerInvariant())[..16];
        var cacheKey = $"mail:suppression:{emailHash}";

        var cached = await cache.GetStringAsync(cacheKey, ct);
        if (cached is not null)
            return cached == "1";

        var isSuppressed = await suppressionRepository.IsSuppressedAsync(email, ct);

        await cache.SetStringAsync(cacheKey,
            isSuppressed ? "1" : "0",
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
            }, ct);

        return isSuppressed;
    }

    /// <summary>
    /// 指定されたメールアドレスの抑制キャッシュエントリを Redis から削除する。
    /// </summary>
    private async Task InvalidateSuppressionCacheAsync(string email, CancellationToken ct)
    {
        var emailHash = ComputeSha256Hash(email.ToLowerInvariant())[..16];
        await cache.RemoveAsync($"mail:suppression:{emailHash}", ct);
    }

    /// <summary>
    /// MailLog に保存された JSON 文字列からテンプレート変数を復元する。
    /// 保存されていない場合は空の辞書を返す。
    /// </summary>
    private static Dictionary<string, object> DeserializeTemplateVariables(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    // --- LoggerMessage ソースジェネレーター（ホットパス最適化） ---

    [LoggerMessage(Level = LogLevel.Information, Message = "メール送信開始: EventType={EventType}, CorrelationId={CorrelationId}")]
    partial void LogMailProcessingStarted(string eventType, string correlationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "メール送信完了: MailLogId={MailLogId}, Recipient={Recipient}")]
    partial void LogMailSent(string mailLogId, string recipient);

    [LoggerMessage(Level = LogLevel.Error, Message = "メール送信失敗: EventType={EventType}, CorrelationId={CorrelationId}")]
    partial void LogMailSendFailed(Exception ex, string eventType, string correlationId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "送信抑制: Recipient={Recipient}, Reason={Reason}")]
    partial void LogMailSuppressed(string recipient, string reason);
}
