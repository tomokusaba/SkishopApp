using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AiSupportService.Models;

/// <summary>
/// ユーザーの検索行動を記録する分析エンティティ。
/// </summary>
/// <remarks>
/// <para>
/// このエンティティはユーザーの検索クエリとその結果に関する分析データを収集します。
/// 検索機能の改善、トレンド分析、パーソナライゼーションの強化に活用されます。
/// </para>
/// <para>
/// テーブル名: <c>search_analytics</c>
/// </para>
/// <para>
/// 主な用途:
/// <list type="bullet">
///   <item><description>検索クエリの分析（人気キーワード、ゼロ結果クエリの特定）</description></item>
///   <item><description>検索パフォーマンスの監視（応答時間の追跡）</description></item>
///   <item><description>クリックスルー率（CTR）の計算</description></item>
///   <item><description>検索結果の品質改善（クリックされた商品の分析）</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var analytics = new SearchAnalytics
/// {
///     UserId = "user-123", // 未ログインユーザーの場合は null
///     Query = "初心者向け スキー板",
///     SearchType = "SEMANTIC",
///     ResultsCount = 15,
///     ResponseTimeMs = 125,
///     ClickedProductIdsJson = JsonSerializer.Serialize(new[] { "prod-001", "prod-005" })
/// };
/// </code>
/// </example>
[Table("search_analytics")]
public class SearchAnalytics
{
    /// <summary>
    /// 検索分析レコードの一意識別子（UUID 形式）。
    /// </summary>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 検索を実行したユーザーの ID。
    /// </summary>
    /// <remarks>
    /// 未ログインユーザーの検索の場合は <c>null</c> になります。
    /// ログインユーザーの場合、パーソナライズされた検索結果の改善に使用します。
    /// </remarks>
    [Column("user_id")]
    [MaxLength(36)]
    public string? UserId { get; set; }

    /// <summary>
    /// ユーザーが入力した検索クエリ。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 検索キーワードや自然言語のクエリを格納します。
    /// </para>
    /// <para>
    /// 注意: 個人情報が含まれる可能性があるため、分析時は適切なマスキングや集計を行ってください。
    /// </para>
    /// </remarks>
    [Column("query")]
    [Required]
    [MaxLength(500)]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// 使用された検索の種類。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><description><c>"KEYWORD"</c> - キーワード検索（デフォルト）</description></item>
    ///   <item><description><c>"SEMANTIC"</c> - セマンティック（意味ベース）検索</description></item>
    ///   <item><description><c>"HYBRID"</c> - キーワードとセマンティックのハイブリッド検索</description></item>
    ///   <item><description><c>"FILTER"</c> - フィルター/ファセット検索</description></item>
    ///   <item><description><c>"AUTOCOMPLETE"</c> - オートコンプリート候補の取得</description></item>
    /// </list>
    /// </remarks>
    [Column("search_type")]
    [Required]
    [MaxLength(30)]
    public string SearchType { get; set; } = "KEYWORD";

    /// <summary>
    /// 検索結果の件数。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>0</c> の場合は「ゼロ結果クエリ」として分析対象になります。
    /// ゼロ結果クエリの頻出パターンを分析し、検索機能の改善に活用します。
    /// </para>
    /// </remarks>
    [Column("results_count")]
    public int ResultsCount { get; set; }

    /// <summary>
    /// ユーザーがクリックした商品 ID のリスト（JSON 形式）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 検索結果からユーザーがクリックした商品の ID を配列形式で格納します。
    /// 例: <c>["prod-001", "prod-005"]</c>
    /// </para>
    /// <para>
    /// クリックスルー率（CTR）の計算や、検索結果の関連性評価に使用します。
    /// PostgreSQL の <c>jsonb</c> 型として格納されます。
    /// </para>
    /// </remarks>
    [Column("clicked_product_ids_json", TypeName = "jsonb")]
    public string? ClickedProductIdsJson { get; set; }

    /// <summary>
    /// 検索の応答時間（ミリ秒）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 検索リクエストを受信してから結果を返すまでの時間を記録します。
    /// パフォーマンス監視と SLA 遵守の確認に使用します。
    /// </para>
    /// <para>
    /// 目標値: 200ms 以下（P95）
    /// </para>
    /// </remarks>
    [Column("response_time_ms")]
    public int ResponseTimeMs { get; set; }

    /// <summary>
    /// 検索が実行された日時（UTC）。
    /// </summary>
    /// <remarks>
    /// 時間帯別の検索傾向分析やトレンドの時系列分析に使用します。
    /// </remarks>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
