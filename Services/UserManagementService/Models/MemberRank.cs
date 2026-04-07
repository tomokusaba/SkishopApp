using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace UserManagementService.Models;

/// <summary>
/// 会員ランクエンティティ。年間購入額に基づいて Bronze → Silver → Gold → Platinum のランクを管理する。
/// 昇格は即時、降格は年次評価時に最大1段階ずつ行う設計（Platinum 維持条件¥250,000 以上）。
/// </summary>
[Table("member_ranks")]
public class MemberRank
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    [Column("current_rank")]
    [Required]
    [MaxLength(20)]
    public string CurrentRank { get; set; } = MemberRankLevel.Bronze;

    [Column("annual_purchase_amount")]
    [Precision(12, 2)]
    public decimal AnnualPurchaseAmount { get; set; }

    [Column("previous_year_amount")]
    [Precision(12, 2)]
    public decimal PreviousYearAmount { get; set; }

    [Column("rank_updated_at")]
    public DateTimeOffset RankUpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("next_evaluation_date")]
    public DateOnly NextEvaluationDate { get; set; }

    [Column("point_rate")]
    [Precision(5, 4)]
    public decimal PointRate { get; set; } = 0.01m;

    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    public User User { get; set; } = null!;

    /// <summary>
    /// デフォルトの Bronze ランクで初期化するファクトリメソッド。
    /// </summary>
    public static MemberRank CreateDefault(string userId, DateTimeOffset now)
        => new()
        {
            UserId = userId,
            CurrentRank = MemberRankLevel.Bronze,
            PointRate = 0.01m,
            RankUpdatedAt = now,
            NextEvaluationDate = new DateOnly(now.Year + 1, 4, 1)
        };

    /// <summary>
    /// 購入額を加算し、昇格条件を満たした場合はランクを即時昇格する。
    /// </summary>
    /// <returns>昇格した場合は前後のランクとポイントレートを返す。昇格なしの場合は null。</returns>
    public (string PreviousRank, string CurrentRank, decimal PointRate)? AddPurchaseAmount(decimal amount, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        AnnualPurchaseAmount += amount;

        var previousRank = CurrentRank;
        var evaluatedRank = EvaluateRank(AnnualPurchaseAmount);
        if (!IsPromotion(CurrentRank, evaluatedRank))
            return null;

        CurrentRank = evaluatedRank;
        PointRate = GetPointRate(evaluatedRank);
        RankUpdatedAt = now;
        return (previousRank, CurrentRank, PointRate);
    }

    /// <summary>
    /// 年次ランク評価を実行する。降格は最大 1 段階、Platinum は¥250,000 以上で維持。
    /// </summary>
    public (string PreviousRank, string CurrentRank, decimal PointRate)? EvaluateAnnual(DateOnly evaluationDate, DateTimeOffset now)
    {
        var previousRank = CurrentRank;
        PreviousYearAmount = AnnualPurchaseAmount;
        AnnualPurchaseAmount = 0;

        var (newRank, newPointRate) = EvaluateAnnualRank(CurrentRank, PreviousYearAmount);
        if (newRank != CurrentRank)
        {
            CurrentRank = newRank;
            PointRate = newPointRate;
            RankUpdatedAt = now;
        }

        NextEvaluationDate = evaluationDate.AddYears(1);
        return newRank == previousRank
            ? null
            : (previousRank, CurrentRank, PointRate);
    }

    // ── ランク評価ロジック（private）──

    /// <summary>年間購入額からランクを決定する。</summary>
    private static string EvaluateRank(decimal annualAmount) => annualAmount switch
    {
        >= 300_000m => MemberRankLevel.Platinum,
        >= 100_000m => MemberRankLevel.Gold,
        >= 50_000m => MemberRankLevel.Silver,
        _ => MemberRankLevel.Bronze
    };

    /// <summary>ランクごとのポイント還元率を取得する。</summary>
    private static decimal GetPointRate(string rank) => rank switch
    {
        MemberRankLevel.Platinum => 0.07m,
        MemberRankLevel.Gold => 0.05m,
        MemberRankLevel.Silver => 0.03m,
        _ => 0.01m
    };

    /// <summary>
    /// 年次ランク評価。Platinum 維持条件と降格上限 1 段階ルールを適用する。
    /// </summary>
    private static (string Rank, decimal PointRate) EvaluateAnnualRank(
        string currentRank, decimal previousYearAmount)
    {
        if (currentRank == MemberRankLevel.Platinum && previousYearAmount >= 250_000m)
            return (MemberRankLevel.Platinum, 0.07m);

        var newRank = EvaluateRank(previousYearAmount);
        var newPointRate = GetPointRate(newRank);

        var currentOrder = GetRankOrder(currentRank);
        var newOrder = GetRankOrder(newRank);

        if (currentOrder - newOrder > 1)
        {
            var demotedRank = GetRankByOrder(currentOrder - 1);
            return (demotedRank, GetPointRate(demotedRank));
        }

        return (newRank, newPointRate);
    }

    private static bool IsPromotion(string current, string next) =>
        GetRankOrder(next) > GetRankOrder(current);

    private static int GetRankOrder(string rank) => rank switch
    {
        MemberRankLevel.Bronze => 1,
        MemberRankLevel.Silver => 2,
        MemberRankLevel.Gold => 3,
        MemberRankLevel.Platinum => 4,
        _ => 0
    };

    private static string GetRankByOrder(int order) => order switch
    {
        1 => MemberRankLevel.Bronze,
        2 => MemberRankLevel.Silver,
        3 => MemberRankLevel.Gold,
        4 => MemberRankLevel.Platinum,
        _ => MemberRankLevel.Bronze
    };
}

/// <summary>
/// 会員ランクレベルの定数定義。DB CHECK 制約と一致させること。
/// </summary>
public static class MemberRankLevel
{
    public const string Bronze = "BRONZE";
    public const string Silver = "SILVER";
    public const string Gold = "GOLD";
    public const string Platinum = "PLATINUM";
}
