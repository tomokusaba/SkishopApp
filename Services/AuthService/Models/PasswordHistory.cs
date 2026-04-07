using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthService.Models;

/// <summary>
/// パスワード履歴エンティティ。過去に使用されたパスワードのハッシュを保持し、
/// パスワード再利用の防止に使用される。
/// </summary>
/// <remarks>
/// <para>
/// セキュリティ: パスワード変更時に過去 N 件（例: 5 件）の履歴と照合し、
/// 再利用を防止する。NIST SP 800-63B ガイドラインに準拠。
/// </para>
/// <para>
/// 注意: パスワードハッシュは保持するが、元のパスワードは復元不可能。
/// 古い履歴は定期的にクリーンアップすること。
/// </para>
/// </remarks>
[Table("password_histories")]
public class PasswordHistory
{
    /// <summary>
    /// パスワード履歴の一意識別子（UUID 形式）。
    /// </summary>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// このパスワードを使用していたユーザーの ID。<see cref="User"/> への外部キー。
    /// </summary>
    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 過去のパスワードのハッシュ値（PBKDF2 または Argon2）。
    /// </summary>
    /// <remarks>
    /// セキュリティ: ハッシュ化されたパスワードのみ保存。平文のパスワードは保存禁止。
    /// 新しいパスワード設定時にこのハッシュと照合し、再利用を検出する。
    /// </remarks>
    [Column("password_hash")]
    [Required]
    [MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// このパスワードが使用されていた期間の終了日時（変更日時）。
    /// </summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// この履歴を所有するユーザーへのナビゲーションプロパティ。
    /// </summary>
    public User User { get; set; } = null!;
}
