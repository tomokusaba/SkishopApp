using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AuthService.Infrastructure.Persistence;

namespace AuthService.Models;

/// <summary>
/// パスワードリセットトークンエンティティ。パスワードリセット要求を追跡し、
/// セキュアなパスワード変更プロセスを実現する。
/// </summary>
/// <remarks>
/// <para>
/// セキュリティ: トークンは暗号学的に安全な乱数から生成され、有効期限が設定される。
/// 1 回使用されると無効化され、再利用はできない。
/// </para>
/// <para>
/// 有効期限は通常 1 時間。期限切れのトークンは定期的にクリーンアップされる。
/// </para>
/// </remarks>
[Table("password_resets")]
public class PasswordReset : IHasTimestamps
{
    /// <summary>
    /// パスワードリセット要求の一意識別子（UUID 形式）。
    /// </summary>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// リセットを要求したユーザーの ID。<see cref="User"/> への外部キー。
    /// </summary>
    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// パスワードリセットトークン。メールリンクに含まれる。
    /// </summary>
    /// <remarks>
    /// セキュリティ: 暗号学的に安全な乱数から生成。推測不可能であること。
    /// </remarks>
    [Column("token")]
    [Required]
    [MaxLength(512)]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// トークンの種別（例: "PASSWORD_RESET", "EMAIL_VERIFICATION"）。
    /// </summary>
    [Column("token_type")]
    [Required]
    [MaxLength(30)]
    public string TokenType { get; set; } = "PASSWORD_RESET";

    /// <summary>
    /// トークンの有効期限（UTC）。この日時を過ぎるとトークンは無効。
    /// </summary>
    [Column("expires_at")]
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// トークンが使用済みかどうか。一度使用されると再利用不可。
    /// </summary>
    [Column("is_used")]
    public bool IsUsed { get; set; }

    /// <summary>
    /// トークンが使用された日時（UTC）。
    /// </summary>
    [Column("used_at")]
    public DateTimeOffset? UsedAt { get; set; }

    /// <summary>
    /// リセット要求の作成日時（UTC）。
    /// </summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// リセット要求の最終更新日時（UTC）。
    /// </summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// リセットを要求したユーザーへのナビゲーションプロパティ。
    /// </summary>
    public User User { get; set; } = null!;
}
