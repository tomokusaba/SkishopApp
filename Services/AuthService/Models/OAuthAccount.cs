using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AuthService.Infrastructure.Persistence;

namespace AuthService.Models;

/// <summary>
/// OAuth 外部認証アカウントエンティティ。ユーザーと外部 OAuth プロバイダー（Google, GitHub 等）の連携情報を保持する。
/// ソーシャルログイン機能の実現に使用される。
/// </summary>
/// <remarks>
/// <para>
/// 1 人のユーザーは複数の OAuth プロバイダーとアカウントを連携できる（1:N の関係）。
/// </para>
/// <para>
/// セキュリティ: AccessToken および RefreshTokenValue は暗号化して保存すること。
/// トークンの有効期限管理と自動更新機構が必要。
/// </para>
/// </remarks>
[Table("oauth_accounts")]
public class OAuthAccount : IHasTimestamps
{
    /// <summary>
    /// OAuth アカウント連携の一意識別子（UUID 形式）。
    /// </summary>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// このアカウントを所有するユーザーの ID。<see cref="User"/> への外部キー。
    /// </summary>
    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// OAuth プロバイダーの識別名（例: "google", "github", "microsoft"）。
    /// </summary>
    [Column("provider")]
    [Required]
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// プロバイダー側でのユーザー一意識別子。プロバイダーごとに形式が異なる。
    /// </summary>
    [Column("provider_user_id")]
    [Required]
    [MaxLength(255)]
    public string ProviderUserId { get; set; } = string.Empty;

    /// <summary>
    /// プロバイダーから取得したメールアドレス。ユーザーのメールと異なる場合がある。
    /// </summary>
    [Column("provider_email")]
    [MaxLength(255)]
    public string? ProviderEmail { get; set; }

    /// <summary>
    /// プロバイダーの API にアクセスするためのアクセストークン。
    /// </summary>
    /// <remarks>
    /// セキュリティ: 暗号化して保存すること。プレーンテキストでの保存は禁止。
    /// </remarks>
    [Column("access_token")]
    [MaxLength(2000)]
    public string? AccessToken { get; set; }

    /// <summary>
    /// アクセストークンを更新するためのリフレッシュトークン。
    /// </summary>
    /// <remarks>
    /// セキュリティ: 暗号化して保存すること。リフレッシュトークンが漏洩した場合は即座に無効化が必要。
    /// </remarks>
    [Column("refresh_token")]
    [MaxLength(2000)]
    public string? RefreshTokenValue { get; set; }

    /// <summary>
    /// アクセストークンの有効期限（UTC）。期限切れ前にリフレッシュが必要。
    /// </summary>
    [Column("token_expires_at")]
    public DateTimeOffset? TokenExpiresAt { get; set; }

    /// <summary>
    /// OAuth 連携の作成日時（UTC）。
    /// </summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// OAuth 連携の最終更新日時（UTC）。トークン更新時に更新される。
    /// </summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// この OAuth アカウントを所有するユーザーへのナビゲーションプロパティ。
    /// </summary>
    public User User { get; set; } = null!;
}
