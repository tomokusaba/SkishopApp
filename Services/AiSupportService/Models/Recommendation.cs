using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AiSupportService.Models;

/// <summary>
/// ユーザー向け商品レコメンデーションを表すエンティティ。
/// </summary>
/// <remarks>
/// <para>
/// このエンティティは AI によって生成されたユーザーへの商品レコメンデーションを管理します。
/// 各レコメンデーションは複数の商品 ID を含み、種別に応じた有効期限を持ちます。
/// </para>
/// <para>
/// テーブル名: <c>recommendations</c>
/// </para>
/// <para>
/// 外部キー: <see cref="UserId"/> → <see cref="UserProfile.UserId"/>
/// </para>
/// <para>
/// レコメンデーションの生成には以下のファクトリメソッドを使用してください：
/// <list type="bullet">
///   <item><description><see cref="CreatePersonalized"/> - パーソナライズされたおすすめ（有効期限: 1 日）</description></item>
///   <item><description><see cref="CreateTrending"/> - トレンド商品（有効期限: 6 時間）</description></item>
///   <item><description><see cref="CreateSimilar"/> - 類似商品（有効期限: 7 日）</description></item>
///   <item><description><see cref="CreateSeasonal"/> - 季節のおすすめ（有効期限: 30 日）</description></item>
///   <item><description><see cref="CreateFrequentlyBoughtTogether"/> - よく一緒に購入される商品（有効期限: 14 日）</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // パーソナライズされたレコメンデーションを作成
/// var recommendation = Recommendation.CreatePersonalized(
///     userId: "user-123",
///     productIds: ["prod-001", "prod-002", "prod-003"],
///     score: 0.92m,
///     reason: "過去の購入履歴に基づくおすすめ"
/// );
/// 
/// // ユーザーが閲覧した場合
/// recommendation.MarkViewed();
/// </code>
/// </example>
[Table("recommendations")]
public class Recommendation
{
    /// <summary>
    /// レコメンデーションの一意識別子（UUID 形式）。
    /// </summary>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// レコメンデーション対象のユーザー ID。
    /// </summary>
    /// <remarks>
    /// <see cref="UserProfile.UserId"/> への外部キー。
    /// </remarks>
    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// レコメンデーションの種別（文字列形式）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 有効な値: <c>"PERSONALIZED"</c>, <c>"TRENDING"</c>, <c>"SIMILAR"</c>,
    /// <c>"SEASONAL"</c>, <c>"FREQUENTLY_BOUGHT_TOGETHER"</c>
    /// </para>
    /// <para>
    /// 列挙型での取得は <see cref="TypeEnum"/> プロパティを使用してください。
    /// </para>
    /// </remarks>
    /// <seealso cref="RecommendationType"/>
    [Column("type")]
    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 推薦する商品 ID のリスト（JSON 形式）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// JSON 配列形式で商品 ID を格納します。
    /// 例: <c>["prod-001", "prod-002", "prod-003"]</c>
    /// </para>
    /// <para>
    /// PostgreSQL の <c>jsonb</c> 型として格納されます。
    /// </para>
    /// </remarks>
    [Column("product_ids_json", TypeName = "jsonb")]
    [Required]
    public string ProductIdsJson { get; set; } = "[]";

    /// <summary>
    /// レコメンデーションの推薦スコア（0.0〜1.0）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// AI モデルがこのレコメンデーションをどの程度強く推薦しているかを示します。
    /// スコアが高いほど、ユーザーが興味を持つ可能性が高いと予測されます。
    /// </para>
    /// <para>
    /// 表示順序の決定やフィルタリングに使用します。
    /// </para>
    /// </remarks>
    [Column("score")]
    public decimal Score { get; set; }

    /// <summary>
    /// レコメンデーションの理由（説明テキスト）。
    /// </summary>
    /// <remarks>
    /// ユーザーに表示する推薦理由です。
    /// 例: <c>"スキー板をご覧になった方におすすめ"</c>, <c>"今週の人気商品"</c>
    /// </remarks>
    [Column("reason")]
    [MaxLength(500)]
    public string? Reason { get; set; }

    /// <summary>
    /// ユーザーがこのレコメンデーションを閲覧したかどうか。
    /// </summary>
    /// <remarks>
    /// レコメンデーションの効果測定（CTR 計算等）に使用します。
    /// <see cref="MarkViewed"/> メソッドで <c>true</c> に設定されます。
    /// </remarks>
    [Column("is_viewed")]
    public bool IsViewed { get; set; }

    /// <summary>
    /// レコメンデーションの有効期限（UTC）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// この日時を過ぎたレコメンデーションは表示対象から除外されます。
    /// 種別ごとに適切な有効期限が設定されます。
    /// </para>
    /// <para>
    /// 期限切れのレコメンデーションは定期的なバッチ処理で削除されます。
    /// </para>
    /// </remarks>
    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// レコメンデーションの作成日時（UTC）。
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// レコメンデーションの最終更新日時（UTC）。
    /// </summary>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 関連するユーザープロファイルへのナビゲーションプロパティ。
    /// </summary>
    public UserProfile? UserProfile { get; set; }

    /// <summary>
    /// 文字列型のレコメンデーション種別を <see cref="RecommendationType"/> 列挙型に変換して返す。
    /// </summary>
    [NotMapped]
    public RecommendationType TypeEnum =>
        Type switch
        {
            "PERSONALIZED" => RecommendationType.Personalized,
            "TRENDING" => RecommendationType.Trending,
            "SIMILAR" => RecommendationType.Similar,
            "SEASONAL" => RecommendationType.Seasonal,
            "FREQUENTLY_BOUGHT_TOGETHER" => RecommendationType.FrequentlyBoughtTogether,
            _ => throw new InvalidOperationException($"Unknown recommendation type: {Type}")
        };

    // --- Factory Methods ---

    /// <summary>
    /// パーソナライズされたレコメンデーションを作成する。有効期限: 1 日。
    /// </summary>
    /// <param name="userId">対象ユーザー ID。</param>
    /// <param name="productIds">推薦する商品 ID のリスト。</param>
    /// <param name="score">推薦スコア（0〜1）。</param>
    /// <param name="reason">推薦理由。</param>
    /// <returns>作成された <see cref="Recommendation"/>。</returns>
    public static Recommendation CreatePersonalized(
        string userId, List<string> productIds, decimal score, string? reason = null)
        => Create(userId, RecommendationType.Personalized, productIds, score, reason,
            DateTime.UtcNow.AddDays(1));

    /// <summary>
    /// トレンド商品のレコメンデーションを作成する。有効期限: 6 時間。
    /// </summary>
    /// <param name="userId">対象ユーザー ID。</param>
    /// <param name="productIds">推薦する商品 ID のリスト。</param>
    /// <param name="score">推薦スコア（0〜1）。</param>
    /// <param name="reason">推薦理由。</param>
    /// <returns>作成された <see cref="Recommendation"/>。</returns>
    public static Recommendation CreateTrending(
        string userId, List<string> productIds, decimal score, string? reason = null)
        => Create(userId, RecommendationType.Trending, productIds, score, reason,
            DateTime.UtcNow.AddHours(6));

    /// <summary>
    /// 類似商品のレコメンデーションを作成する。有効期限: 7 日。
    /// </summary>
    /// <param name="userId">対象ユーザー ID。</param>
    /// <param name="productIds">推薦する商品 ID のリスト。</param>
    /// <param name="score">推薦スコア（0〜1）。</param>
    /// <param name="reason">推薦理由。</param>
    /// <returns>作成された <see cref="Recommendation"/>。</returns>
    public static Recommendation CreateSimilar(
        string userId, List<string> productIds, decimal score, string? reason = null)
        => Create(userId, RecommendationType.Similar, productIds, score, reason,
            DateTime.UtcNow.AddDays(7));

    /// <summary>
    /// 季節商品のレコメンデーションを作成する。有効期限: 30 日。
    /// </summary>
    /// <param name="userId">対象ユーザー ID。</param>
    /// <param name="productIds">推薦する商品 ID のリスト。</param>
    /// <param name="score">推薦スコア（0〜1）。</param>
    /// <param name="reason">推薦理由。</param>
    /// <returns>作成された <see cref="Recommendation"/>。</returns>
    public static Recommendation CreateSeasonal(
        string userId, List<string> productIds, decimal score, string? reason = null)
        => Create(userId, RecommendationType.Seasonal, productIds, score, reason,
            DateTime.UtcNow.AddDays(30));

    /// <summary>
    /// よく一緒に購入される商品のレコメンデーションを作成する。有効期限: 14 日。
    /// </summary>
    /// <param name="userId">対象ユーザー ID。</param>
    /// <param name="productIds">推薦する商品 ID のリスト。</param>
    /// <param name="score">推薦スコア（0〜1）。</param>
    /// <param name="reason">推薦理由。</param>
    /// <returns>作成された <see cref="Recommendation"/>。</returns>
    public static Recommendation CreateFrequentlyBoughtTogether(
        string userId, List<string> productIds, decimal score, string? reason = null)
        => Create(userId, RecommendationType.FrequentlyBoughtTogether, productIds, score, reason,
            DateTime.UtcNow.AddDays(14));

    /// <summary>
    /// レコメンデーションを閲覧済みとしてマークする。
    /// </summary>
    public void MarkViewed()
    {
        IsViewed = true;
        UpdatedAt = DateTime.UtcNow;
    }

    private static Recommendation Create(
        string userId, RecommendationType type, List<string> productIds,
        decimal score, string? reason, DateTime expiresAt)
    {
        ArgumentNullException.ThrowIfNull(productIds);
        ArgumentOutOfRangeException.ThrowIfNegative(score);

        return new Recommendation
        {
            UserId = userId,
            Type = type.ToString().ToUpperInvariant(),
            ProductIdsJson = System.Text.Json.JsonSerializer.Serialize(productIds),
            Score = Math.Min(score, 1.0m),
            Reason = reason,
            ExpiresAt = expiresAt
        };
    }
}
