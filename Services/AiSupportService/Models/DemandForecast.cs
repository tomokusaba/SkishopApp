using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AiSupportService.Models;

/// <summary>
/// AI による商品需要予測を表すエンティティ。
/// </summary>
/// <remarks>
/// <para>
/// このエンティティは機械学習モデルによって生成された商品の需要予測を格納します。
/// 在庫管理サービス（InventoryManagementService）が発注計画を立てる際に参照します。
/// </para>
/// <para>
/// テーブル名: <c>demand_forecasts</c>
/// </para>
/// <para>
/// 予測は以下の粒度で生成されます：
/// <list type="bullet">
///   <item><description>日次（DAILY）: 翌日〜7日先までの予測</description></item>
///   <item><description>週次（WEEKLY）: 翌週〜4週先までの予測</description></item>
///   <item><description>月次（MONTHLY）: 翌月〜3ヶ月先までの予測</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var forecast = new DemandForecast
/// {
///     ProductId = "prod-123",
///     Sku = "SKI-BOARD-001",
///     ForecastDate = DateTime.UtcNow.AddDays(7),
///     ForecastPeriod = "WEEKLY",
///     PredictedDemand = 150,
///     ConfidenceScore = 0.85m,
///     ModelVersion = "v2.1.0"
/// };
/// </code>
/// </example>
[Table("demand_forecasts")]
public class DemandForecast
{
    /// <summary>
    /// 需要予測レコードの一意識別子（UUID 形式）。
    /// </summary>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 予測対象の商品 ID。
    /// </summary>
    /// <remarks>
    /// InventoryManagementService で管理される商品 ID を参照します。
    /// </remarks>
    [Column("product_id")]
    [Required]
    [MaxLength(36)]
    public string ProductId { get; set; } = string.Empty;

    /// <summary>
    /// 商品の SKU（Stock Keeping Unit）コード。
    /// </summary>
    /// <remarks>
    /// 商品のバリエーション（サイズ、色など）を識別するためのコードです。
    /// 在庫管理との連携時に使用します。
    /// </remarks>
    [Column("sku")]
    [MaxLength(50)]
    public string? Sku { get; set; }

    /// <summary>
    /// 予測対象の日付。
    /// </summary>
    /// <remarks>
    /// この日付における需要を予測しています。
    /// 過去の日付の予測は実績との比較に使用できます。
    /// </remarks>
    [Column("forecast_date")]
    public DateTime ForecastDate { get; set; }

    /// <summary>
    /// 予測期間の粒度。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><description><c>"DAILY"</c> - 日次予測</description></item>
    ///   <item><description><c>"WEEKLY"</c> - 週次予測</description></item>
    ///   <item><description><c>"MONTHLY"</c> - 月次予測</description></item>
    /// </list>
    /// </remarks>
    [Column("forecast_period")]
    [Required]
    [MaxLength(20)]
    public string ForecastPeriod { get; set; } = string.Empty;

    /// <summary>
    /// 予測された需要数量。
    /// </summary>
    /// <remarks>
    /// 指定された期間における予想販売数量です。
    /// 発注量の決定や在庫アラートの基準値として使用します。
    /// </remarks>
    [Column("predicted_demand")]
    public int PredictedDemand { get; set; }

    /// <summary>
    /// 予測の信頼度スコア（0.0〜1.0）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// モデルがこの予測にどの程度自信を持っているかを示します。
    /// </para>
    /// <list type="bullet">
    ///   <item><description>0.8 以上: 高信頼度 - 発注計画に直接使用可能</description></item>
    ///   <item><description>0.5〜0.8: 中信頼度 - 参考値として使用</description></item>
    ///   <item><description>0.5 未満: 低信頼度 - 人間による確認が必要</description></item>
    /// </list>
    /// </remarks>
    [Column("confidence_score")]
    public decimal ConfidenceScore { get; set; }

    /// <summary>
    /// 予測に使用した AI モデルのバージョン。
    /// </summary>
    /// <remarks>
    /// モデルの改善履歴を追跡し、予測精度の変化を分析するために使用します。
    /// セマンティックバージョニング形式（例: <c>"v2.1.0"</c>）を推奨します。
    /// </remarks>
    [Column("model_version")]
    [MaxLength(50)]
    public string? ModelVersion { get; set; }

    /// <summary>
    /// 予測レコードの作成日時（UTC）。
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 予測レコードの最終更新日時（UTC）。
    /// </summary>
    /// <remarks>
    /// 予測値や信頼度が再計算された場合に更新されます。
    /// </remarks>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
