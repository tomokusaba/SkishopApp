using Confluent.Kafka;
using MailSendService.Configurations;
using MailSendService.Infrastructure.Metrics;
using MailSendService.Services.Interfaces;
using Microsoft.Extensions.Options;
using Serilog.Context;
using System.Text.Json;

namespace MailSendService.Consumers;

/// <summary>
/// Kafka "mail-events" トピックからイベントを消費し、メール送信処理にルーティングするバックグラウンドサービス。
/// </summary>
/// <remarks>
/// <para>受信したイベントを <see cref="MailEvents"/> と <see cref="GdprEvents"/> に分類し、
/// メール送信イベントは <see cref="IMailService.ProcessEventAsync"/> へ、
/// GDPR イベントは <see cref="ProcessGdprEventAsync"/> へルーティングする。</para>
/// <para>Scoped サービスの解決には <see cref="IServiceScopeFactory"/> を使用する。</para>
/// <para>エラー発生時は 5 秒のバックオフ後にリトライし、<see cref="OperationCanceledException"/> は
/// シャットダウン要求として処理ループを終了する。</para>
/// <para>P1-11: 処理不能なメッセージは Dead Letter Topic に転送する。</para>
/// <para>P1-16: CorrelationId を LogContext に設定し、構造化ログに含める。</para>
/// </remarks>
/// <param name="consumer">Kafka コンシューマーインスタンス。</param>
/// <param name="producer">Dead Letter Topic への転送用プロデューサー。</param>
/// <param name="scopeFactory">Scoped サービス解決用のスコープファクトリ。</param>
/// <param name="kafkaOptions">Kafka 設定（トピック名等）。</param>
/// <param name="metrics">メール関連メトリクス記録用。</param>
/// <param name="logger">ロガー。</param>
public class MailEventConsumer(
    IConsumer<string, string> consumer,
    IProducer<string, string> producer,
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaSettings> kafkaOptions,
    MailMetrics metrics,
    ILogger<MailEventConsumer> logger) : BackgroundService
{
    /// <summary>メール送信対象のイベントタイプ一覧。</summary>
    private static readonly HashSet<string> MailEvents =
    [
        "user.registered",
        "password.reset.requested",
        "user.verified",
        "order.created",
        "order.cancelled",
        "shipment.status.updated",
        "user.email_changed"
    ];

    /// <summary>イベントエンベロープの JSON デシリアライズオプション。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>GDPR 関連のイベントタイプ一覧。</summary>
    private static readonly HashSet<string> GdprEvents =
    [
        "consent.revoked",
        "user.deleted",
        "user.processing-restricted",
        "user.processing-unrestricted"
    ];

    /// <summary>P1-11: Dead Letter Topic 名。</summary>
    private string DeadLetterTopic => $"{kafkaOptions.Value.Topic}.dlq";

    /// <summary>
    /// Kafka トピックを購読し、イベントを継続的に消費・処理するメインループを実行する。
    /// </summary>
    /// <param name="stoppingToken">サービス停止要求を通知するキャンセルトークン。</param>
    /// <remarks>
    /// <para>イベントタイプに応じてメール送信イベントと GDPR イベントを振り分ける。
    /// 未対応のイベントタイプはスキップし、Kafka オフセットをコミットする。</para>
    /// <para>ConsumeException 発生時は 1 秒、その他の例外発生時は 5 秒のバックオフを行う。</para>
    /// <para>P1-11: 処理失敗が連続した場合は Dead Letter Topic に転送する。</para>
    /// <para>P1-16: CorrelationId を LogContext に設定する。</para>
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var topic = kafkaOptions.Value.Topic;
        consumer.Subscribe(topic);
        logger.LogInformation("MailEventConsumer started. Topic: {Topic}, DLQ: {DeadLetterTopic}", topic, DeadLetterTopic);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;
            try
            {
                result = await Task.Run(() => consumer.Consume(stoppingToken), stoppingToken);
                if (result?.Message?.Value is null) continue;

                EventEnvelope? envelope;
                try
                {
                    envelope = JsonSerializer.Deserialize<EventEnvelope>(result.Message.Value, JsonOptions);
                }
                catch (JsonException ex)
                {
                    // P1-17: JSON パースエラーをログ出力して Dead Letter Topic に転送
                    logger.LogError(ex, "JSON parse error. Sending to DLQ. Offset: {Offset}", result.Offset);
                    await SendToDeadLetterAsync(result, "JSON_PARSE_ERROR", ex.Message, stoppingToken);
                    consumer.Commit(result);
                    continue;
                }

                if (envelope is null)
                {
                    logger.LogWarning("Null envelope. Sending to DLQ. Offset: {Offset}", result.Offset);
                    await SendToDeadLetterAsync(result, "NULL_ENVELOPE", "Deserialized to null", stoppingToken);
                    consumer.Commit(result);
                    continue;
                }

                var isMailEvent = MailEvents.Contains(envelope.EventType);
                var isGdprEvent = GdprEvents.Contains(envelope.EventType);

                if (!isMailEvent && !isGdprEvent)
                {
                    logger.LogDebug("Unsupported event ignored: {EventType}", envelope.EventType);
                    consumer.Commit(result);
                    continue;
                }

                metrics.RecordEventConsumed(envelope.EventType);

                // P1-16: CorrelationId を LogContext に設定
                var correlationId = envelope.CorrelationId ?? Guid.NewGuid().ToString();
                using (LogContext.PushProperty("CorrelationId", correlationId))
                {
                    logger.LogInformation(
                        "Processing: {EventType}, EventId: {EventId}",
                        envelope.EventType, envelope.EventId);

                    using var scope = scopeFactory.CreateScope();
                    var mailService = scope.ServiceProvider.GetRequiredService<IMailService>();

                    if (isMailEvent)
                    {
                        await mailService.ProcessEventAsync(
                            envelope.EventType, envelope.EventId,
                            correlationId, envelope.PayloadJson, stoppingToken);
                    }
                    else
                    {
                        await ProcessGdprEventAsync(mailService, envelope, stoppingToken);
                    }
                }

                consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume error: {Topic}, {Reason}",
                    ex.ConsumerRecord?.Topic, ex.Error.Reason);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Event processing error");
                // P1-11: 処理エラー時は Dead Letter Topic に転送
                if (result is not null)
                {
                    await SendToDeadLetterAsync(result, "PROCESSING_ERROR", ex.Message, stoppingToken);
                    consumer.Commit(result);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }

        consumer.Close();
        logger.LogInformation("MailEventConsumer stopped.");
    }

    /// <summary>
    /// P1-11: Dead Letter Topic にメッセージを転送する。
    /// </summary>
    private async Task SendToDeadLetterAsync(
        ConsumeResult<string, string> result,
        string errorCode,
        string errorMessage,
        CancellationToken ct)
    {
        try
        {
            var headers = new Headers
            {
                { "x-error-code", System.Text.Encoding.UTF8.GetBytes(errorCode) },
                { "x-error-message", System.Text.Encoding.UTF8.GetBytes(errorMessage) },
                { "x-original-topic", System.Text.Encoding.UTF8.GetBytes(result.Topic) },
                { "x-original-partition", System.Text.Encoding.UTF8.GetBytes(result.Partition.Value.ToString()) },
                { "x-original-offset", System.Text.Encoding.UTF8.GetBytes(result.Offset.Value.ToString()) }
            };

            var dlqMessage = new Message<string, string>
            {
                Key = result.Message.Key,
                Value = result.Message.Value,
                Headers = headers
            };

            await producer.ProduceAsync(DeadLetterTopic, dlqMessage, ct);
            logger.LogWarning("Message sent to DLQ: {ErrorCode}, Offset: {Offset}", errorCode, result.Offset);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send to DLQ: {ErrorCode}", errorCode);
        }
    }

    /// <summary>
    /// GDPR 関連イベントを種類別に処理する。
    /// </summary>
    /// <param name="mailService">メールサービスインスタンス。</param>
    /// <param name="envelope">受信したイベントエンベロープ。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <remarks>
    /// consent.revoked / user.deleted / user.processing-restricted / user.processing-unrestricted
    /// の各イベントタイプに応じて対応する <see cref="IMailService"/> メソッドを呼び出す。
    /// </remarks>
    private static async Task ProcessGdprEventAsync(
        IMailService mailService, EventEnvelope envelope, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(envelope.PayloadJson);
        var root = doc.RootElement;
        var userId = root.GetProperty("userId").GetString() ?? string.Empty;

        switch (envelope.EventType)
        {
            case "consent.revoked":
                var consentType = root.TryGetProperty("consentType", out var ct2) ? ct2.GetString() ?? "marketing" : "marketing";
                await mailService.ProcessConsentRevokedAsync(userId, consentType, ct);
                break;
            case "user.deleted":
                await mailService.ProcessUserDeletedAsync(userId, ct);
                break;
            case "user.processing-restricted":
                await mailService.ProcessUserProcessingRestrictedAsync(userId, ct);
                break;
            case "user.processing-unrestricted":
                await mailService.ProcessUserProcessingUnrestrictedAsync(userId, ct);
                break;
        }
    }
}
