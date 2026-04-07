using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserManagementService.Models;

/// <summary>
/// ユーザー Aggregate Root エンティティ。
/// ユーザープロファイル情報・ステータス管理・GDPR データエクスポート状態を保持する。
/// </summary>
[Table("users")]
public class User
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("email")]
    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Column("first_name")]
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Column("last_name")]
    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Column("phone_number")]
    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [Column("birth_date")]
    public DateOnly? BirthDate { get; set; }

    [Column("status")]
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = UserStatus.PendingVerification;

    [Column("processing_restricted")]
    public bool IsProcessingRestricted { get; set; }

    [Column("restriction_reason")]
    [MaxLength(500)]
    public string? RestrictionReason { get; set; }

    [Column("restricted_at")]
    public DateTimeOffset? RestrictedAt { get; set; }

    [Column("last_login_at")]
    public DateTimeOffset? LastLoginAt { get; set; }

    [Column("data_export_requested")]
    public bool IsDataExportRequested { get; set; }

    [Column("data_export_completed_at")]
    public DateTimeOffset? DataExportCompletedAt { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    // ── ナビゲーションプロパティ（LazyLoading 無効、Include() で明示的 Eager Loading）──
    public UserPreference? Preference { get; set; }
    public MemberRank? MemberRank { get; set; }
    public ICollection<Address> Addresses { get; private set; } = [];
    public ICollection<Wishlist> Wishlists { get; private set; } = [];
    public ICollection<UserActivity> Activities { get; private set; } = [];
    public ICollection<Consent> Consents { get; private set; } = [];
    public ICollection<DeletionRequest> DeletionRequests { get; private set; } = [];

    /// <summary>
    /// AuthService の <c>user.registered</c> Kafka イベントから新規ユーザーを生成するファクトリメソッド。
    /// </summary>
    public static User CreateRegistered(
        string userId,
        string email,
        string firstName,
        string lastName)
        => new()
        {
            Id = userId,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Status = UserStatus.PendingVerification
        };

    /// <summary>
    /// プロファイル情報を部分更新する。null のフィールドは更新しない。
    /// </summary>
    public void UpdateProfile(string? firstName, string? lastName, string? phoneNumber, DateOnly? birthDate)
    {
        if (firstName is not null)
            FirstName = firstName;
        if (lastName is not null)
            LastName = lastName;
        if (phoneNumber is not null)
            PhoneNumber = phoneNumber;
        if (birthDate is not null)
            BirthDate = birthDate;
    }

    public void ChangeStatus(string status)
        => Status = status;

    /// <summary>
    /// GDPR Article 18 に基づく処理制限を設定/解除する。
    /// </summary>
    public void SetProcessingRestriction(bool restricted, string? reason, DateTimeOffset? restrictedAt)
    {
        IsProcessingRestricted = restricted;
        RestrictionReason = reason;
        RestrictedAt = restrictedAt;
    }

    public void ResetLastLogin()
        => LastLoginAt = null;

    /// <summary>
    /// GDPR Article 20 に基づくデータポータビリティ（エクスポート）をリクエストする。
    /// </summary>
    public void RequestDataExport()
    {
        IsDataExportRequested = true;
        DataExportCompletedAt = null;
    }

    public void CompleteDataExport(DateTimeOffset completedAt)
    {
        IsDataExportRequested = false;
        DataExportCompletedAt = completedAt;
    }
}

/// <summary>
/// ユーザーステータスの定数定義。DB の CHECK 制約と一致させること。
/// </summary>
public static class UserStatus
{
    public const string PendingVerification = "PENDING_VERIFICATION";
    public const string Active = "ACTIVE";
    public const string Suspended = "SUSPENDED";
    public const string Deactivated = "DEACTIVATED";
}
