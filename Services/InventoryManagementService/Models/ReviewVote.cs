using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagementService.Models;

/// <summary>
/// レビュー投票エンティティ。review_votes テーブルにマッピングされる。
/// ユーザーがレビューに対して「参考になった」と投票した記録を保持する。
/// </summary>
/// <remarks>
/// ReviewId + UserId の一意制約により、同一ユーザーが同一レビューに重複投票することを防止する。
/// </remarks>
[Table("review_votes")]
public class ReviewVote
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("review_id")]
    [Required]
    [MaxLength(36)]
    public string ReviewId { get; set; } = string.Empty;

    /// <summary>投票したユーザーの ID。</summary>
    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>投票対象レビューへのナビゲーション。</summary>
    public Review Review { get; set; } = null!;
}
