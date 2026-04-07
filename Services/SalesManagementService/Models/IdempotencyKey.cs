using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesManagementService.Models;

[Table("idempotency_keys")]
public class IdempotencyKey
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("idempotency_key")]
    [Required]
    [MaxLength(36)]
    public string Key { get; set; } = string.Empty;

    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    [Column("request_status")]
    [Required]
    [MaxLength(20)]
    public string RequestStatus { get; set; } = "PENDING";

    [Column("response_status")]
    public int? ResponseStatus { get; set; }

    [Column("response_body", TypeName = "jsonb")]
    public string? ResponseBody { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("expires_at")]
    public DateTimeOffset ExpiresAt { get; set; }
}
