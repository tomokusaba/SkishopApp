using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserManagementService.Models;

/// <summary>
/// GDPR 同意管理エンティティ。マーケティング・パーソナライズ等の同意状態をバージョン管理で保持する。
/// 同意の付与/撤回時には IP アドレスと UserAgent を証跡として記録する。
/// </summary>
[Table("consents")]
public class Consent
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    [Column("consent_type")]
    [Required]
    [MaxLength(50)]
    public string ConsentType { get; set; } = string.Empty;

    [Column("is_granted")]
    public bool IsGranted { get; set; }

    [Column("version")]
    public int Version { get; set; } = 1;

    [Column("policy_text_hash")]
    [MaxLength(64)]
    public string? PolicyTextHash { get; set; }

    [Column("ip_address")]
    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User User { get; set; } = null!;

    /// <summary>
    /// 同意を付与し、バージョンをインクリメントする。
    /// </summary>
    public void Grant(string? ipAddress, string? userAgent, string? policyTextHash)
    {
        IsGranted = true;
        Version++;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        PolicyTextHash = policyTextHash;
    }

    /// <summary>
    /// 同意を撤回し、バージョンをインクリメントする。撤回時には <c>consent.revoked</c> イベントが発行される。
    /// </summary>
    public void Revoke(string? ipAddress, string? userAgent)
    {
        IsGranted = false;
        Version++;
        IpAddress = ipAddress;
        UserAgent = userAgent;
    }
}

/// <summary>
/// 同意種別の定数定義。DB の CHECK 制約と一致させること。
/// </summary>
public static class ConsentType
{
    public const string Marketing = "MARKETING";
    public const string Personalization = "PERSONALIZATION";
    public const string Analytics = "ANALYTICS";
    public const string ThirdPartySharing = "THIRD_PARTY_SHARING";
}
