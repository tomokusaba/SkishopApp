using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagementService.Models;

/// <summary>
/// レビューへのスタッフ返信エンティティ。review_responses テーブルにマッピングされる。
/// Review Aggregate の子エンティティとして、管理者やスタッフからの公式回答を管理する。
/// </summary>
[Table("review_responses")]
public class ReviewResponse
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("review_id")]
    [Required]
    [MaxLength(36)]
    public string ReviewId { get; set; } = string.Empty;

    /// <summary>返信者（スタッフ / 管理者）のユーザー ID。</summary>
    [Column("responder_id")]
    [Required]
    [MaxLength(36)]
    public string ResponderId { get; set; } = string.Empty;

    [Column("content")]
    [Required]
    public string Content { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>楽観的ロック用のタイムスタンプ。</summary>
    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    /// <summary>親レビューへのナビゲーション。</summary>
    public Review Review { get; set; } = null!;
}
