using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AuthService.Infrastructure.Persistence;

namespace AuthService.Models;

/// <summary>
/// OAuth クライアントエンティティ。外部アプリケーション（マイクロサービス、サードパーティ連携）の認証情報を管理する。
/// OAuth 2.0 Client Credentials フローおよび Authorization Code フローで使用される。
/// </summary>
/// <remarks>
/// <para>
/// ClientSecretHash は暗号化されたクライアントシークレットを保持する。
/// 平文のシークレットはシステム内に保存されない。
/// </para>
/// <para>
/// 無効化されたクライアント（IsActive = false）からのトークン発行リクエストは拒否される。
/// </para>
/// </remarks>
[Table("oauth_clients")]
public class OAuthClient : IHasTimestamps
{
    /// <summary>
    /// OAuth クライアントの一意識別子（UUID 形式）。内部管理用。
    /// </summary>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 公開クライアント ID。OAuth フローで client_id パラメータとして使用される。
    /// </summary>
    [Column("client_id")]
    [Required]
    [MaxLength(100)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// クライアントシークレットのハッシュ値（PBKDF2 または Argon2 でハッシュ化）。
    /// </summary>
    /// <remarks>
    /// セキュリティ: 平文のシークレットは保存禁止。ハッシュ化して保存すること。
    /// シークレットの漏洩が疑われる場合は即座に再生成が必要。
    /// </remarks>
    [Column("client_secret_hash")]
    [Required]
    [MaxLength(255)]
    public string ClientSecretHash { get; set; } = string.Empty;

    /// <summary>
    /// クライアントアプリケーションの表示名。
    /// </summary>
    [Column("name")]
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// クライアントアプリケーションの説明。管理画面での識別に使用。
    /// </summary>
    [Column("description")]
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// このクライアントに許可されたスコープ（スペース区切り）。例: "read:users write:orders"。
    /// </summary>
    /// <remarks>
    /// 最小権限の原則に従い、必要最小限のスコープのみを許可すること。
    /// </remarks>
    [Column("allowed_scopes")]
    [Required]
    [MaxLength(1000)]
    public string AllowedScopes { get; set; } = string.Empty;

    /// <summary>
    /// クライアントの有効状態。false の場合、このクライアントからのリクエストは全て拒否される。
    /// </summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// クライアント登録日時（UTC）。
    /// </summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// クライアント情報の最終更新日時（UTC）。
    /// </summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
