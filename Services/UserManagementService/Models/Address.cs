using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserManagementService.Models;

/// <summary>
/// 配送先/請求先住所エンティティ。1 ユーザーあたり最大 10 件まで登録可能。
/// </summary>
[Table("addresses")]
public class Address
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    [Column("address_type")]
    [Required]
    [MaxLength(20)]
    public string AddressType { get; set; } = string.Empty;

    [Column("recipient")]
    [Required]
    [MaxLength(100)]
    public string Recipient { get; set; } = string.Empty;

    [Column("zip_code")]
    [Required]
    [MaxLength(10)]
    public string ZipCode { get; set; } = string.Empty;

    [Column("prefecture")]
    [Required]
    [MaxLength(50)]
    public string Prefecture { get; set; } = string.Empty;

    [Column("city")]
    [Required]
    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Column("street_address")]
    [Required]
    [MaxLength(255)]
    public string StreetAddress { get; set; } = string.Empty;

    [Column("building")]
    [MaxLength(255)]
    public string? Building { get; set; }

    [Column("phone_number")]
    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [Column("is_default")]
    public bool IsDefault { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    public User User { get; set; } = null!;

    /// <summary>
    /// 住所情報を更新する。全フィールドを置き換えるため、部分更新は呼び出し元で制御する。
    /// </summary>
    public void Update(string recipient, string zipCode, string prefecture,
        string city, string streetAddress, string? building, string? phoneNumber)
    {
        Recipient = recipient;
        ZipCode = zipCode;
        Prefecture = prefecture;
        City = city;
        StreetAddress = streetAddress;
        Building = building;
        PhoneNumber = phoneNumber;
    }

    public void SetAsDefault() => IsDefault = true;
    public void UnsetDefault() => IsDefault = false;
}

/// <summary>
/// 住所種別の定数定義。DB の CHECK 制約で「SHIPPING」または「BILLING」のみ許可。
/// </summary>
public static class AddressType
{
    public const string Shipping = "SHIPPING";
    public const string Billing = "BILLING";
}
