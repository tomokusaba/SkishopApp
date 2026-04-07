using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AuthService.Enums;
using AuthService.Infrastructure.Persistence;

namespace AuthService.Models;

/// <summary>
/// ユーザーエンティティ（Aggregate Root）。認証・認可システムの中心となるエンティティ。
/// ユーザーアカウント情報、認証状態、セキュリティ設定を管理する。
/// </summary>
/// <remarks>
/// <para>
/// DDD: User は Aggregate Root であり、Session, OAuthAccount, RefreshToken 等の
/// 子エンティティは User を通じてのみ操作される。
/// </para>
/// <para>
/// セキュリティ: PasswordHash は PBKDF2 または Argon2 でハッシュ化される。
/// 平文のパスワードはシステム内に保存されない。
/// </para>
/// <para>
/// アカウントロック: 連続したログイン失敗（デフォルト 5 回）でアカウントが自動ロックされる。
/// ロックは管理者操作または一定時間経過で解除される。
/// </para>
/// </remarks>
[Table("users")]
public class User : IHasTimestamps
{
    /// <summary>
    /// ユーザーの一意識別子（UUID 形式）。
    /// </summary>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// ユーザーのメールアドレス。システム内で一意。ログイン識別子として使用される。
    /// </summary>
    [Column("email")]
    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// ユーザーの表示名。任意設定。
    /// </summary>
    [Column("username")]
    [MaxLength(100)]
    public string? Username { get; set; }

    /// <summary>
    /// パスワードのハッシュ値。OAuth のみのユーザーは null。
    /// </summary>
    /// <remarks>
    /// セキュリティ: PBKDF2 または Argon2 でハッシュ化。平文のパスワードは保存禁止。
    /// </remarks>
    [Column("password_hash")]
    [MaxLength(255)]
    public string? PasswordHash { get; set; }

    /// <summary>
    /// ユーザーの名（ファーストネーム）。
    /// </summary>
    [Column("first_name")]
    [MaxLength(100)]
    public string? FirstName { get; set; }

    /// <summary>
    /// ユーザーの姓（ラストネーム）。
    /// </summary>
    [Column("last_name")]
    [MaxLength(100)]
    public string? LastName { get; set; }

    /// <summary>
    /// ユーザーアカウントの状態。<see cref="UserStatus"/> を参照。
    /// </summary>
    [Column("status")]
    [Required]
    [MaxLength(50)]
    public UserStatus Status { get; set; } = UserStatus.PendingVerification;

    /// <summary>
    /// ユーザーのプライマリロール。アクセス制御の基本判定に使用される。
    /// </summary>
    [Column("role")]
    [Required]
    [MaxLength(50)]
    public UserRoleType Role { get; set; } = UserRoleType.User;

    /// <summary>
    /// メールアドレスが認証済みかどうか。
    /// </summary>
    [Column("email_verified")]
    public bool IsEmailVerified { get; set; }

    /// <summary>
    /// アカウントがアクティブかどうか。false の場合、ログイン不可。
    /// </summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// アカウントがロックされているかどうか。連続ログイン失敗でロックされる。
    /// </summary>
    [Column("account_locked")]
    public bool IsAccountLocked { get; set; }

    /// <summary>
    /// アカウントがロックされた日時（UTC）。自動ロック解除の判定に使用。
    /// </summary>
    [Column("locked_at")]
    public DateTimeOffset? LockedAt { get; set; }

    /// <summary>
    /// 連続ログイン失敗回数。成功時にリセットされる。
    /// </summary>
    [Column("failed_login_attempts")]
    public int FailedLoginAttempts { get; set; }

    /// <summary>
    /// 最終ログイン日時（UTC）。
    /// </summary>
    [Column("last_login")]
    public DateTimeOffset? LastLogin { get; set; }

    /// <summary>
    /// アカウントの作成日時（UTC）。
    /// </summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// アカウントの最終更新日時（UTC）。
    /// </summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 楽観的ロック用の行バージョン。同時更新競合の検出に使用。
    /// </summary>
    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    /// <summary>
    /// ユーザーのアクティブなセッション一覧。
    /// </summary>
    public ICollection<UserSession> Sessions { get; set; } = [];

    /// <summary>
    /// 連携された OAuth アカウント一覧。
    /// </summary>
    public ICollection<OAuthAccount> OAuthAccounts { get; set; } = [];

    /// <summary>
    /// ユーザーに関連するセキュリティログ一覧。
    /// </summary>
    public ICollection<SecurityLog> SecurityLogs { get; set; } = [];

    /// <summary>
    /// ユーザーに割り当てられたロール一覧（多対多）。
    /// </summary>
    public ICollection<UserRole> UserRoles { get; set; } = [];

    /// <summary>
    /// ユーザーのリフレッシュトークン一覧。
    /// </summary>
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];

    /// <summary>
    /// ユーザーの MFA（多要素認証）設定。
    /// </summary>
    public UserMfa? Mfa { get; set; }

    // --- Domain Methods ---

    /// <summary>
    /// アカウントロックが時間経過により解除可能かどうかを判定する。
    /// </summary>
    /// <param name="now">現在日時（UTC）。</param>
    /// <param name="autoUnlockMinutes">自動ロック解除までの分数。</param>
    /// <returns>ロック期限切れの場合は true。</returns>
    public bool IsLockExpired(DateTimeOffset now, int autoUnlockMinutes)
        => IsAccountLocked && LockedAt.HasValue
            && LockedAt.Value.AddMinutes(autoUnlockMinutes) < now;

    /// <summary>
    /// ログイン失敗を記録し、必要に応じてアカウントをロックする。
    /// </summary>
    /// <param name="maxFailedAttempts">ロックまでの最大失敗回数。</param>
    /// <param name="now">現在日時（UTC）。</param>
    public void RecordFailedLogin(int maxFailedAttempts, DateTimeOffset now)
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= maxFailedAttempts)
        {
            IsAccountLocked = true;
            LockedAt = now;
        }
    }

    /// <summary>
    /// アカウントロックを解除し、失敗カウンターをリセットする。
    /// </summary>
    public void Unlock()
    {
        IsAccountLocked = false;
        FailedLoginAttempts = 0;
        LockedAt = null;
    }

    /// <summary>
    /// ログイン失敗カウンターをリセットする。ログイン成功時に呼び出される。
    /// </summary>
    public void ResetFailedAttempts()
    {
        FailedLoginAttempts = 0;
    }

    /// <summary>
    /// ログイン成功を記録し、最終ログイン日時を更新する。
    /// </summary>
    /// <param name="now">現在日時（UTC）。</param>
    public void RecordLogin(DateTimeOffset now)
    {
        LastLogin = now;
    }
}
