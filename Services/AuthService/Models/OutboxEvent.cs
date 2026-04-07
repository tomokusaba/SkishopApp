using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AuthService.Enums;
using AuthService.Infrastructure.Persistence;

namespace AuthService.Models;

/// <summary>
/// Outbox イベントエンティティ。トランザクショナルアウトボックスパターンを実装し、
/// イベント発行の信頼性を保証する。
/// </summary>
/// <remarks>
/// <para>
/// Outbox パターン: DB トランザクションとイベント発行の整合性を保証するために、
/// イベントを一旦 DB に保存し、BackgroundService が非同期で Kafka に発行する。
/// </para>
/// <para>
/// リトライ戦略: 指数バックオフでリトライし、MaxRetries に達した場合は DeadLetter 状態に遷移。
/// </para>
/// </remarks>
[Table("outbox_events")]
public class OutboxEvent : IHasTimestamps
{
    /// <summary>
    /// Outbox イベントの一意識別子（UUID 形式）。
    /// </summary>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// イベントの種別名（例: "UserRegistered", "PasswordChanged"）。Consumer 側でのデシリアライズに使用。
    /// </summary>
    [Column("event_type")]
    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// イベントの発行先 Kafka トピック名。
    /// </summary>
    [Column("topic")]
    [Required]
    [MaxLength(200)]
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Kafka メッセージのパーティションキー。同一キーは同一パーティションに配置され、順序が保証される。
    /// </summary>
    [Column("key")]
    [MaxLength(255)]
    public string? Key { get; set; }

    /// <summary>
    /// イベントのペイロード（JSON 形式）。イベント固有のデータを含む。
    /// </summary>
    [Column("payload")]
    [Required]
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// イベントの現在の処理状態。<see cref="OutboxStatus"/> を参照。
    /// </summary>
    [Column("status")]
    [Required]
    [MaxLength(20)]
    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;

    /// <summary>
    /// 現在のリトライ回数。失敗するたびにインクリメントされる。
    /// </summary>
    [Column("retry_count")]
    public int RetryCount { get; set; }

    /// <summary>
    /// 最大リトライ回数。この回数に達すると DeadLetter 状態に遷移する。
    /// </summary>
    [Column("max_retries")]
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// 最後の処理エラーメッセージ。デバッグおよび障害分析に使用。
    /// </summary>
    [Column("error_message")]
    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// イベントが正常に発行された日時（UTC）。Published 状態への遷移時に設定。
    /// </summary>
    [Column("processed_at")]
    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>
    /// イベントの作成日時（UTC）。
    /// </summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// イベントの最終更新日時（UTC）。ステータス変更時に更新される。
    /// </summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
