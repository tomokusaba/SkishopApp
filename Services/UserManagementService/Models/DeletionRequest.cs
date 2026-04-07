using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserManagementService.Models;

/// <summary>
/// GDPR Article 17（忘れられる権利）に基づくアカウント削除リクエストエンティティ。
/// 14 日間の献予期間（Grace Period）後に Saga パターンで各サービスへ削除を伝播する。
/// 状態遷移: PENDING → PROCESSING → COMPLETED / FAILED / CANCELLED
/// </summary>
[Table("deletion_requests")]
public class DeletionRequest
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("user_id")]
    [MaxLength(36)]
    public string? UserId { get; set; }

    [Column("requested_by")]
    [Required]
    [MaxLength(36)]
    public string RequestedBy { get; set; } = string.Empty;

    [Column("approved_by")]
    [MaxLength(36)]
    public string? ApprovedBy { get; set; }

    [Column("request_channel")]
    [Required]
    [MaxLength(30)]
    public string RequestChannel { get; set; } = string.Empty;

    [Column("requested_at")]
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("grace_period_ends_at")]
    public DateTimeOffset GracePeriodEndsAt { get; set; }

    [Column("status")]
    [Required]
    [MaxLength(40)]
    public string Status { get; set; } = DeletionRequestStatus.Pending;

    [Column("completed_at")]
    public DateTimeOffset? CompletedAt { get; set; }

    [Column("service_statuses", TypeName = "jsonb")]
    public string? ServiceStatuses { get; set; }

    [Column("failure_reason")]
    [MaxLength(1000)]
    public string? FailureReason { get; set; }

    [Column("retry_count")]
    public int RetryCount { get; set; }

    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    public User? User { get; set; }

    // ── 状態遷移ドメインメソッド ──

    /// <summary>
    /// 削除リクエストをキャンセルする。PENDING 状態のみキャンセル可能。
    /// </summary>
    /// <exception cref="Exceptions.BusinessException">PENDING 以外の状態で呼び出した場合。</exception>
    public void Cancel()
    {
        if (Status != DeletionRequestStatus.Pending)
            throw new Exceptions.BusinessException("猶予期間中の削除リクエストのみキャンセルできます");
        Status = DeletionRequestStatus.Cancelled;
    }

    /// <summary>
    /// 削除処理を開始する。PENDING 状態から PROCESSING へ遷移する。
    /// </summary>
    public void StartProcessing()
    {
        if (Status != DeletionRequestStatus.Pending)
            throw new Exceptions.BusinessException("処理開始は Pending 状態のリクエストのみ可能です");
        Status = DeletionRequestStatus.Processing;
    }

    /// <summary>
    /// 削除処理を完了する。PROCESSING 状態から COMPLETED へ遷移する。
    /// </summary>
    public void Complete(DateTimeOffset completedAt)
    {
        if (Status != DeletionRequestStatus.Processing)
            throw new Exceptions.BusinessException("完了は Processing 状態のリクエストのみ可能です");
        Status = DeletionRequestStatus.Completed;
        CompletedAt = completedAt;
    }

    /// <summary>
    /// 削除処理を失敗としてマークする。
    /// </summary>
    public void Fail(string reason)
    {
        if (Status is not (DeletionRequestStatus.Processing or DeletionRequestStatus.Pending))
            throw new Exceptions.BusinessException("失敗はPending/Processing状態のリクエストのみ可能です");
        Status = DeletionRequestStatus.Failed;
        FailureReason = reason;
    }

    /// <summary>
    /// リトライカウントをインクリメントし、最大リトライ数に達した場合は FAILED へ遷移する。
    /// </summary>
    public void IncrementRetry(int maxRetries, string failureReason)
    {
        RetryCount++;
        if (RetryCount >= maxRetries)
            Fail(failureReason);
        // else: Status は Processing のまま維持
    }
}

/// <summary>
/// 削除リクエストの状態定数。DB の CHECK 制約と一致させること。
/// </summary>
public static class DeletionRequestStatus
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";
    public const string AwaitingManualIntervention = "AWAITING_MANUAL_INTERVENTION";
}

/// <summary>
/// 削除リクエストのチャネル定数（WEB / Admin Console / Email DSR / API）。
/// </summary>
public static class RequestChannel
{
    public const string WebSelfService = "WEB_SELF_SERVICE";
    public const string AdminConsole = "ADMIN_CONSOLE";
    public const string EmailDsr = "EMAIL_DSR";
    public const string Api = "API";
}
