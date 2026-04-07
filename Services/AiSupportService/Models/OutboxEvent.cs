using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AiSupportService.Models;

/// <summary>
/// Outbox パターンで Kafka へ発行するイベントを表すエンティティ。
/// </summary>
/// <remarks>
/// <para>
/// このエンティティは Transactional Outbox パターンを実装し、
/// データベーストランザクションとイベント発行の整合性を保証します。
/// ビジネスロジックの実行と同一トランザクション内でイベントを DB に書き込み、
/// 別プロセス（OutboxPublisher）が非同期に Kafka へ発行します。
/// </para>
/// <para>
/// テーブル名: <c>outbox_events</c>
/// </para>
/// <para>
/// イベントのライフサイクル:
/// <list type="number">
///   <item><description><c>PENDING</c> - イベント作成済み、Kafka への発行待ち</description></item>
///   <item><description><c>PUBLISHED</c> - Kafka への発行成功</description></item>
///   <item><description><c>FAILED</c> - 発行失敗（リトライ上限超過）</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var outboxEvent = new OutboxEvent
/// {
///     AggregateType = "ChatSession",
///     AggregateId = session.Id,
///     EventType = "ChatSessionClosed",
///     Topic = "ai.chat-session.closed",
///     Payload = JsonSerializer.Serialize(new { SessionId = session.Id, UserId = session.UserId })
/// };
/// context.OutboxEvents.Add(outboxEvent);
/// await context.SaveChangesAsync(ct); // ビジネスデータと同一トランザクション
/// </code>
/// </example>
[Table("outbox_events")]
public class OutboxEvent
{
    /// <summary>
    /// イベントの一意識別子（UUID 形式）。
    /// </summary>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// イベントを発生させた集約（Aggregate）の種類。
    /// </summary>
    /// <remarks>
    /// DDD の Aggregate Root の名前を指定します。
    /// 例: <c>"ChatSession"</c>, <c>"UserProfile"</c>, <c>"Recommendation"</c>
    /// </remarks>
    [Column("aggregate_type")]
    [Required]
    [MaxLength(100)]
    public string AggregateType { get; set; } = string.Empty;

    /// <summary>
    /// イベントを発生させた集約のインスタンス ID。
    /// </summary>
    /// <remarks>
    /// 具体的なエンティティの ID を指定します。
    /// Kafka のメッセージキーとしても使用され、同一集約のイベントは同一パーティションに送信されます。
    /// </remarks>
    [Column("aggregate_id")]
    [Required]
    [MaxLength(36)]
    public string AggregateId { get; set; } = string.Empty;

    /// <summary>
    /// イベントの種類。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 発生したビジネスイベントを識別します。命名規則: <c>{AggregateType}{Action}</c>
    /// </para>
    /// <list type="bullet">
    ///   <item><description><c>"ChatSessionCreated"</c> - チャットセッション作成</description></item>
    ///   <item><description><c>"ChatSessionClosed"</c> - チャットセッション終了</description></item>
    ///   <item><description><c>"RecommendationGenerated"</c> - レコメンデーション生成</description></item>
    ///   <item><description><c>"DemandForecastUpdated"</c> - 需要予測更新</description></item>
    /// </list>
    /// </remarks>
    [Column("event_type")]
    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// イベントの送信先 Kafka トピック。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 命名規則: <c>{service}.{aggregate}.{action}</c>
    /// </para>
    /// <list type="bullet">
    ///   <item><description><c>"ai.chat-session.created"</c></description></item>
    ///   <item><description><c>"ai.recommendation.generated"</c></description></item>
    ///   <item><description><c>"ai.demand-forecast.updated"</c></description></item>
    /// </list>
    /// </remarks>
    [Column("topic")]
    [Required]
    [MaxLength(200)]
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// イベントのペイロード（JSON 形式）。
    /// </summary>
    /// <remarks>
    /// イベントの詳細データを JSON 文字列として格納します。
    /// スキーマはイベント種類ごとに定義されます。
    /// </remarks>
    [Column("payload")]
    [Required]
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// イベントの発行ステータス。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><description><c>"PENDING"</c> - 発行待ち（デフォルト）</description></item>
    ///   <item><description><c>"PUBLISHED"</c> - 発行成功</description></item>
    ///   <item><description><c>"FAILED"</c> - 発行失敗（リトライ上限超過）</description></item>
    /// </list>
    /// </remarks>
    [Column("status")]
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "PENDING";

    /// <summary>
    /// 発行のリトライ回数。
    /// </summary>
    /// <remarks>
    /// Kafka への発行が失敗するたびにインクリメントされます。
    /// 上限（通常 3〜5 回）に達すると <see cref="Status"/> が <c>FAILED</c> に変更されます。
    /// </remarks>
    [Column("retry_count")]
    public int RetryCount { get; set; }

    /// <summary>
    /// 発行失敗時のエラーメッセージ。
    /// </summary>
    /// <remarks>
    /// 最後に発生したエラーの詳細を格納します。
    /// トラブルシューティングやアラートの詳細情報として使用します。
    /// </remarks>
    [Column("error_message")]
    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// イベントが Kafka に発行された日時（UTC）。
    /// </summary>
    /// <remarks>
    /// <see cref="Status"/> が <c>PUBLISHED</c> に変更された際に設定されます。
    /// </remarks>
    [Column("published_at")]
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// イベントレコードの作成日時（UTC）。
    /// </summary>
    /// <remarks>
    /// ビジネストランザクションでイベントが作成された時刻です。
    /// </remarks>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
