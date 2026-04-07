using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AuthService.Infrastructure.Persistence;

namespace AuthService.Models;

/// <summary>
/// ユーザーセッションエンティティ。ログイン中のユーザーセッションを管理する。
/// セッション管理、同時ログイン制御、セキュリティ監視に使用される。
/// </summary>
/// <remarks>
/// <para>
/// セッションはログイン成功時に作成され、ログアウト、タイムアウト、または明示的な無効化で終了する。
/// </para>
/// <para>
/// セキュリティ: SessionToken は暗号学的に安全な乱数から生成される。
/// IP アドレスと User-Agent を記録し、セッションハイジャック検知に使用する。
/// </para>
/// <para>
/// 同時ログイン制御: ユーザーごとの同時アクティブセッション数を制限できる。
/// 新しいセッション作成時に最も古いセッションを無効化する等のポリシーを適用可能。
/// </para>
/// </remarks>
[Table("user_sessions")]
public class UserSession : IHasTimestamps
{
    /// <summary>
    /// セッションの一意識別子（UUID 形式）。
    /// </summary>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// このセッションを所有するユーザーの ID。<see cref="User"/> への外部キー。
    /// </summary>
    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// セッショントークン。クライアントの Cookie または Authorization ヘッダーで送信される。
    /// </summary>
    /// <remarks>
    /// セキュリティ: 暗号学的に安全な乱数から生成。推測不可能であること。
    /// </remarks>
    [Column("session_token")]
    [Required]
    [MaxLength(512)]
    public string SessionToken { get; set; } = string.Empty;

    /// <summary>
    /// セッション開始時のクライアント IP アドレス（IPv4/IPv6 対応）。
    /// </summary>
    /// <remarks>
    /// セッションハイジャック検知: IP アドレスの急激な変更を監視する。
    /// </remarks>
    [Column("ip_address")]
    [MaxLength(45)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// セッション開始時のクライアント User-Agent。
    /// </summary>
    /// <remarks>
    /// セッションハイジャック検知: User-Agent の急激な変更を監視する。
    /// 注意: User-Agent は偽装可能なため、唯一の判断基準としない。
    /// </remarks>
    [Column("user_agent")]
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// セッションがアクティブかどうか。false の場合、このセッションは無効。
    /// </summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// セッションの有効期限（UTC）。この日時を過ぎるとセッションは無効。
    /// </summary>
    [Column("expires_at")]
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// 最終アクティビティ日時（UTC）。スライディングセッションの延長判定に使用。
    /// </summary>
    [Column("last_activity")]
    public DateTimeOffset LastActivity { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// セッションの作成日時（UTC）。
    /// </summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// セッションの最終更新日時（UTC）。
    /// </summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// このセッションを所有するユーザーへのナビゲーションプロパティ。
    /// </summary>
    public User User { get; set; } = null!;
}
