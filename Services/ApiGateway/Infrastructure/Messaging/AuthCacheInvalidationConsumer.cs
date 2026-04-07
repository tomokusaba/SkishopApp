// ─────────────────────────────────────────────────────────────
// AuthCacheInvalidationConsumer — Kafka 認証キャッシュ無効化コンシューマー
//
// 設計書 §6 準拠。user.permission_changed トピックを購読し、
// 認証キャッシュ（Redis）の該当エントリを無効化する。
// ─────────────────────────────────────────────────────────────

using Confluent.Kafka;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using ApiGateway.Configurations;
using Serilog.Context;

namespace ApiGateway.Infrastructure.Messaging;

/// <summary>
/// Kafka の <c>user.permission_changed</c> トピックを購読し、
/// ユーザー権限変更イベントを受信して認証キャッシュ（Redis）を無効化する
/// バックグラウンドサービス。
/// <para>設計書 §6「API ゲートウェイ イベント購読」準拠。</para>
/// </summary>
public sealed class AuthCacheInvalidationConsumer(
    IOptions<KafkaSettings> kafkaOptions,
    IDistributedCache cache,
    ILogger<AuthCacheInvalidationConsumer> logger) : BackgroundService
{
    /// <summary>購読する Kafka トピック名。</summary>
    private const string TopicName = "user.permission_changed";

    /// <summary>
    /// Kafka コンシューマーを起動し、権限変更イベントを継続的に処理する。
    /// </summary>
    /// <param name="stoppingToken">サービス停止トークン。</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = kafkaOptions.Value;
        if (string.IsNullOrEmpty(settings.BootstrapServers))
        {
            logger.LogWarning(
                "Kafka:BootstrapServers が未設定のため、認証キャッシュ無効化コンシューマーは無効です");
            return;
        }

        var config = new ConsumerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            GroupId = settings.GroupId,
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(TopicName);

        logger.LogInformation(
            "Kafka コンシューマー開始: Topic={Topic}, Group={GroupId}",
            TopicName, settings.GroupId);

        // H-1: Task.Run で同期 Consume() をラップし、非同期ループを解放
        // これにより BackgroundService のシャットダウンがスムーズになる
        try
        {
            await Task.Run(async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var result = consumer.Consume(stoppingToken);
                        if (result?.Message?.Key is { } userId)
                        {
                            // Correlation ID を Kafka メッセージヘッダーまたは新規生成で付与（H-8）
                            var correlationId = ExtractCorrelationId(result.Message.Headers)
                                ?? Guid.NewGuid().ToString();
                            using (LogContext.PushProperty("CorrelationId", correlationId))
                            {
                                // 該当ユーザーの認証キャッシュエントリを削除
                                var cacheKey = $"auth:user:{userId}";
                                await cache.RemoveAsync(cacheKey, stoppingToken);

                                logger.LogInformation(
                                    "認証キャッシュ無効化: UserId={UserId}, Topic={Topic}",
                                    userId, TopicName);

                                // H-3: Commit 失敗を個別にハンドリング
                                try
                                {
                                    consumer.Commit(result);
                                }
                                catch (KafkaException ex)
                                {
                                    logger.LogError(ex,
                                        "Kafka commit エラー: Topic={Topic}, Offset={Offset}",
                                        TopicName, result.Offset.Value);
                                }
                            }
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        logger.LogError(ex,
                            "Kafka consume エラー: Topic={Topic}, Reason={Reason}",
                            TopicName, ex.Error.Reason);
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogError(ex,
                            "認証キャッシュ無効化エラー: {Message}", ex.Message);
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    }
                }
            }, stoppingToken);
        }
        finally
        {
            // H-2: OperationCanceledException 発生時でも確実に Close を呼び出す
            consumer.Close();
            logger.LogInformation("Kafka コンシューマー停止: Topic={Topic}", TopicName);
        }
    }

    /// <summary>
    /// Kafka メッセージヘッダーから Correlation ID を抽出する。
    /// </summary>
    private static string? ExtractCorrelationId(Headers? headers)
    {
        if (headers is null) return null;
        var header = headers.FirstOrDefault(h => h.Key == "X-Correlation-Id");
        return header is not null
            ? System.Text.Encoding.UTF8.GetString(header.GetValueBytes())
            : null;
    }
}
