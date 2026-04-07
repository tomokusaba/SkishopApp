using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AuthService.Infrastructure.Persistence;

namespace AuthService.Models;

/// <summary>
/// ユーザー MFA（多要素認証）設定エンティティ。TOTP ベースの二要素認証を管理する。
/// </summary>
/// <remarks>
/// <para>
/// TOTP: RFC 6238 に準拠した Time-based One-Time Password を使用。
/// Google Authenticator, Microsoft Authenticator 等と互換性がある。
/// </para>
/// <para>
/// セキュリティ: SecretKey は暗号化して保存すること。漏洩した場合は即座に再生成が必要。
/// BackupCodes はハッシュ化して保存し、1 回使用で無効化する。
/// </para>
/// </remarks>
[Table("user_mfa")]
public class UserMfa : IHasTimestamps
{
    /// <summary>
    /// MFA 設定の一意識別子（UUID 形式）。
    /// </summary>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// この MFA 設定を所有するユーザーの ID。<see cref="User"/> への外部キー。
    /// </summary>
    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// TOTP 生成に使用するシークレットキー（Base32 エンコード）。
    /// </summary>
    /// <remarks>
    /// セキュリティ: 暗号化して保存すること。このキーが漏洩すると MFA がバイパスされる。
    /// QR コード生成時に使用されるが、表示は初回のみに限定する。
    /// </remarks>
    [Column("secret_key")]
    [Required]
    [MaxLength(255)]
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// MFA が有効かどうか。false の場合、ログイン時に TOTP 検証をスキップ。
    /// </summary>
    [Column("is_enabled")]
    public bool IsEnabled { get; set; }

    /// <summary>
    /// MFA 設定が最初に検証された日時（UTC）。初回 TOTP コード入力成功時に設定。
    /// </summary>
    [Column("verified_at")]
    public DateTimeOffset? VerifiedAt { get; set; }

    /// <summary>
    /// バックアップコード（ハッシュ化済み、カンマ区切り）。デバイス紛失時の緊急アクセス用。
    /// </summary>
    /// <remarks>
    /// セキュリティ: 各コードはハッシュ化して保存。使用されたコードは無効化する。
    /// ユーザーには生成時に 1 度だけ平文を表示し、安全な場所に保管するよう指示する。
    /// </remarks>
    [Column("backup_codes")]
    [MaxLength(1000)]
    public string? BackupCodes { get; set; }

    /// <summary>
    /// MFA 設定の作成日時（UTC）。
    /// </summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// MFA 設定の最終更新日時（UTC）。
    /// </summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// この MFA 設定を所有するユーザーへのナビゲーションプロパティ。
    /// </summary>
    public User User { get; set; } = null!;
}
