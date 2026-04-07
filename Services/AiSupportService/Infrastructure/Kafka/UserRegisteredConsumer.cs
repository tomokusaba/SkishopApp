using System.Text.Json;
using AiSupportService.Configurations;
using AiSupportService.Models;
using AiSupportService.Repositories.Interfaces;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace AiSupportService.Infrastructure.Kafka;

/// <summary>
/// Kafka の "user.registered" トピックを購読し、新規ユーザーの AI プロファイルを作成する <see cref="BackgroundService"/>。
/// </summary>
/// <remarks>
/// <para>
/// AuthService から発行されるユーザー登録イベントを受信し、
/// AI サービス用のユーザープロファイルを初期化する。このプロファイルは
/// 閲覧履歴・購買履歴・レコメンデーションの基盤となる。
/// </para>
/// <para>
/// <b>べき等性:</b> 既にプロファイルが存在する場合はスキップするため、
/// 同一イベントが複数回処理されても重複作成は発生しない。
/// </para>
/// <para>
/// <b>初期プロファイル:</b>
/// <list type="bullet">
///   <item><description><c>PreferencesJson</c>: 空オブジェクト "{}"</description></item>
///   <item><description><c>BrowsingHistoryJson</c>: 空配列 "[]"</description></item>
///   <item><description><c>PurchaseHistoryJson</c>: 空配列 "[]"</description></item>
/// </list>
/// </para>
/// <para>
/// <b>エラーハンドリング:</b> 処理失敗時は 5 秒待機後にリトライする。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Program.cs での登録
/// builder.Services.AddHostedService&lt;UserRegisteredConsumer&gt;();
/// </code>
/// </example>
/// <param name="scopeFactory">
/// Scoped サービス取得用のファクトリ。<see cref="IUserProfileRepository"/> の取得に使用する。
/// </param>
/// <param name="kafkaOptions">Kafka 接続設定。</param>
/// <param name="logger">診断ログの出力先ロガー。</param>
public class UserRegisteredConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaSettings> kafkaOptions,
    ILogger<UserRegisteredConsumer> logger) : BackgroundService
{
    /// <summary>
    /// Kafka メッセージを継続的にコンシュームし、ユーザー登録イベントに対応するプロファイルを作成する。
    /// </summary>
    /// <param name="stoppingToken">
    /// サービス停止を通知するキャンセルトークン。
    /// このトークンがキャンセルされると、Consume のブロッキングが中断される。
    /// </param>
    /// <returns>サービス停止まで継続するタスク。</returns>
    /// <remarks>
    /// <para>
    /// <b>処理フロー:</b>
    /// <list type="number">
    ///   <item><description>Kafka からメッセージを受信</description></item>
    ///   <item><description>JSON デシリアライズで <see cref="UserRegisteredEvent"/> に変換</description></item>
    ///   <item><description>既存プロファイルの存在チェック</description></item>
    ///   <item><description>未存在の場合: 初期プロファイルを作成・保存</description></item>
    ///   <item><description>Kafka オフセットをコミット</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Consumer 設定:</b>
    /// <list type="bullet">
    ///   <item><description><c>AutoOffsetReset.Earliest</c>: 未処理メッセージを最初から読み取る</description></item>
    ///   <item><description><c>EnableAutoCommit = false</c>: 処理成功後に明示的にコミット</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaOptions.Value.BootstrapServers,
            GroupId = $"{kafkaOptions.Value.GroupId}-user-registered",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe("user.registered");

        logger.LogInformation("UserRegisteredConsumer 開始: Topic=user.registered");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                if (result?.Message?.Value is null) continue;

                var @event = JsonSerializer.Deserialize<UserRegisteredEvent>(result.Message.Value);
                if (@event is null) continue;

                using var scope = scopeFactory.CreateScope();
                var userProfileRepository = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

                var existingProfile = await userProfileRepository.FindByUserIdAsync(@event.UserId, stoppingToken);
                if (existingProfile is null)
                {
                    var profile = new UserProfile
                    {
                        UserId = @event.UserId,
                        PreferencesJson = "{}",
                        BrowsingHistoryJson = "[]",
                        PurchaseHistoryJson = "[]"
                    };
                    await userProfileRepository.AddAsync(profile, stoppingToken);
                    await userProfileRepository.SaveChangesAsync(stoppingToken);

                    logger.LogInformation("ユーザープロファイル作成: UserId={UserId}", @event.UserId);
                }

                consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume エラー: {Topic}", ex.ConsumerRecord?.Topic);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "UserRegistered イベント処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
