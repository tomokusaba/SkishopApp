using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryManagementService.Models;

/// <summary>
/// カスタマーレビューの Aggregate Root。reviews テーブルにマッピングされる。
/// 商品に対するユーザーの評価・コメントを管理し、レビュー承認ワークフロー（PENDING → APPROVED）を制御する。
/// </summary>
/// <remarks>
/// ReviewResponse（スタッフ返信）を子エンティティとして保持する。
/// </remarks>
[Table("reviews")]
public class Review
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("product_id")]
    [Required]
    [MaxLength(36)]
    public string ProductId { get; set; } = string.Empty;

    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>評価スコア（1〜5）。</summary>
    [Column("rating")]
    public int Rating { get; set; }

    [Column("title")]
    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [Column("content")]
    public string? Content { get; set; }

    /// <summary>購入済みユーザーによるレビューかどうか。注文履歴から自動判定される。</summary>
    [Column("is_verified_purchase")]
    public bool IsVerifiedPurchase { get; set; }

    /// <summary>「参考になった」投票数。ReviewVote の集計値。</summary>
    [Column("helpful_count")]
    public int HelpfulCount { get; set; }

    /// <summary>レビューの承認ステータス。PENDING（承認待ち）/ APPROVED（承認済み）。</summary>
    [Column("status")]
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "PENDING";

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    /// <summary>
    /// レビューを承認する。PENDING ステータスのレビューのみ承認可能。
    /// </summary>
    /// <exception cref="InvalidOperationException">PENDING 以外のステータスの場合</exception>
    public void Approve()
    {
        if (Status != "PENDING")
            throw new InvalidOperationException($"ステータス '{Status}' のレビューは承認できません。PENDING のみ可能です。");
        Status = "APPROVED";
    }

    /// <summary>
    /// レビューを却下する。PENDING ステータスのレビューのみ却下可能。
    /// </summary>
    /// <exception cref="InvalidOperationException">PENDING 以外のステータスの場合</exception>
    public void Reject()
    {
        if (Status != "PENDING")
            throw new InvalidOperationException($"ステータス '{Status}' のレビューは却下できません。PENDING のみ可能です。");
        Status = "REJECTED";
    }

    /// <summary>
    /// レビューの「参考になった」カウントをインクリメントする。
    /// </summary>
    public void IncrementHelpfulCount()
    {
        HelpfulCount++;
    }

    /// <summary>
    /// レビューに管理者/スタッフの返信を追加する（Aggregate Root 経由）。
    /// </summary>
    /// <param name="responderId">回答者 ID</param>
    /// <param name="content">回答内容</param>
    /// <returns>作成された ReviewResponse エンティティ</returns>
    public ReviewResponse AddResponse(string responderId, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var response = new ReviewResponse
        {
            ReviewId = Id,
            ResponderId = responderId,
            Content = content
        };
        Responses.Add(response);
        return response;
    }

    /// <summary>レビュー対象商品へのナビゲーション。</summary>
    public Product Product { get; set; } = null!;

    /// <summary>スタッフ・管理者による返信一覧。</summary>
    public ICollection<ReviewResponse> Responses { get; set; } = [];
}
