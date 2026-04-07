using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PointService.Models;

/// <summary>
/// ポイントアカウント（Aggregate Root）。
/// ユーザーごとのポイント残高を管理する。
/// </summary>
[Table("point_accounts")]
public class PointAccount
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; private set; } = Guid.NewGuid().ToString();

    [Column("user_id")]
    [Required]
    public Guid UserId { get; set; }

    [Column("available_points")]
    [Required]
    public int AvailablePoints { get; private set; }

    [Column("pending_points")]
    [Required]
    public int PendingPoints { get; private set; }

    [Column("total_earned")]
    [Required]
    public int TotalEarned { get; private set; }

    [Column("total_spent")]
    [Required]
    public int TotalSpent { get; private set; }

    [Column("total_expired")]
    [Required]
    public int TotalExpired { get; private set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = default;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = default;

    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    public ICollection<PointTransaction> Transactions { get; set; } = [];
    public ICollection<PointExpiry> Expiries { get; set; } = [];

    /// <summary>ポイント付与</summary>
    public void EarnPoints(int points)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);
        AvailablePoints += points;
        TotalEarned += points;
    }

    /// <summary>ポイント仮消費（Reserve）— 事前チェック済みの前提</summary>
    public void ReservePoints(int points)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);
        AvailablePoints -= points;
        PendingPoints += points;
    }

    /// <summary>仮消費の確定（Confirm）</summary>
    public void ConfirmReservation(int points)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);
        PendingPoints -= points;
        TotalSpent += points;
    }

    /// <summary>仮消費の解放（Release）</summary>
    public void ReleaseReservation(int points)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);
        AvailablePoints += points;
        PendingPoints -= points;
    }

    /// <summary>ポイント失効</summary>
    public void ExpirePoints(int points)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);
        AvailablePoints -= points;
        TotalExpired += points;
    }

    /// <summary>管理者によるポイント調整</summary>
    public void AdjustPoints(int points)
    {
        AvailablePoints += points;
    }

    /// <summary>匿名化（GDPR 準拠）</summary>
    public void Anonymize(string anonymizedId)
    {
        ArgumentNullException.ThrowIfNull(anonymizedId);
        UserId = Guid.Parse(anonymizedId);
        AvailablePoints = 0;
        PendingPoints = 0;
    }
}
