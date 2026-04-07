using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AuthService.Infrastructure.Persistence;

namespace AuthService.Models;

/// <summary>
/// リフレッシュトークンエンティティ。JWT アクセストークンの更新に使用される。
/// セキュアなトークンローテーション機構を実装する。
/// </summary>
/// <remarks>
/// <para>
/// トークンファミリー: 同一ログインセッションから派生した全てのトークンを追跡し、
/// トークン再利用攻撃を検知する。再利用が検出された場合、ファミリー全体を無効化する。
/// </para>
/// <para>
/// セキュリティ: リフレッシュトークンは 1 回使用可能（使用後は新トークンに置換）。
/// 無効化されたトークンの再利用試行はセキュリティインシデントとして記録する。
/// </para>
/// </remarks>
[Table("refresh_tokens")]
public class RefreshToken : IHasTimestamps
{
    /// <summary>
    /// リフレッシュトークンの一意識別子（UUID 形式）。
    /// </summary>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// このトークンを所有するユーザーの ID。<see cref="User"/> への外部キー。
    /// </summary>
    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// リフレッシュトークンの値。クライアントに返却される実際のトークン文字列。
    /// </summary>
    /// <remarks>
    /// セキュリティ: 暗号学的に安全な乱数から生成。推測不可能であること。
    /// </remarks>
    [Column("token")]
    [Required]
    [MaxLength(512)]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// 対応する JWT アクセストークンの JTI（JWT ID）。トークンペアの紐付けに使用。
    /// </summary>
    [Column("jti")]
    [Required]
    [MaxLength(36)]
    public string Jti { get; set; } = string.Empty;

    /// <summary>
    /// トークンファミリー ID。同一ログインセッションから派生したトークンを追跡する。
    /// </summary>
    /// <remarks>
    /// トークン再利用攻撃の検出: 同一 FamilyId で複数のアクティブなトークンが存在する場合、
    /// 攻撃の可能性があるためファミリー全体を無効化する。
    /// </remarks>
    [Column("family_id")]
    [Required]
    [MaxLength(36)]
    public string FamilyId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// このトークンの直前のトークン ID。トークンチェーンの追跡に使用。
    /// </summary>
    [Column("previous_token_id")]
    [MaxLength(36)]
    public string? PreviousTokenId { get; set; }

    /// <summary>
    /// トークンファミリーの絶対有効期限（UTC）。スライディング更新に関わらず、この日時で無効化される。
    /// </summary>
    [Column("absolute_expiry")]
    public DateTimeOffset AbsoluteExpiry { get; set; }

    /// <summary>
    /// このトークンの有効期限（UTC）。スライディング有効期限。
    /// </summary>
    [Column("expires_at")]
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// トークンが明示的に無効化されたかどうか。ログアウトまたはセキュリティ理由で無効化される。
    /// </summary>
    [Column("is_revoked")]
    public bool IsRevoked { get; set; }

    /// <summary>
    /// トークンが無効化された日時（UTC）。
    /// </summary>
    [Column("revoked_at")]
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>
    /// このトークンを置き換えた新しいトークンの ID。トークンローテーションの追跡に使用。
    /// </summary>
    [Column("replaced_by_token")]
    [MaxLength(36)]
    public string? ReplacedByToken { get; set; }

    /// <summary>
    /// トークンの作成日時（UTC）。
    /// </summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// トークンの最終更新日時（UTC）。
    /// </summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// このトークンを所有するユーザーへのナビゲーションプロパティ。
    /// </summary>
    public User User { get; set; } = null!;
}
