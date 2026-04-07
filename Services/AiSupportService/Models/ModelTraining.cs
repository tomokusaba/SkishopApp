using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AiSupportService.Models;

/// <summary>
/// AI モデルのトレーニング実行履歴を表すエンティティ。
/// </summary>
/// <remarks>
/// <para>
/// このエンティティは機械学習モデルのトレーニングジョブを追跡します。
/// 需要予測モデル、レコメンデーションモデル、チャット応答モデルなど、
/// 様々な AI モデルのトレーニング履歴を一元管理します。
/// </para>
/// <para>
/// テーブル名: <c>model_trainings</c>
/// </para>
/// <para>
/// トレーニングのライフサイクル:
/// <list type="number">
///   <item><description><c>PENDING</c> - トレーニングジョブが作成され、実行待ち</description></item>
///   <item><description><c>RUNNING</c> - トレーニング実行中</description></item>
///   <item><description><c>COMPLETED</c> - トレーニング正常完了</description></item>
///   <item><description><c>FAILED</c> - トレーニング失敗</description></item>
///   <item><description><c>CANCELLED</c> - トレーニングがキャンセルされた</description></item>
/// </list>
/// </para>
/// <para>
/// 楽観的同時実行制御: <see cref="RowVersion"/> で競合を検出します。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var training = new ModelTraining
/// {
///     ModelName = "demand-forecast",
///     ModelVersion = "v2.1.0",
///     Status = "PENDING",
///     CreatedBy = "admin-user-123",
///     ParametersJson = JsonSerializer.Serialize(new { epochs = 100, learningRate = 0.001 })
/// };
/// </code>
/// </example>
[Table("model_trainings")]
public class ModelTraining
{
    /// <summary>
    /// トレーニングジョブの一意識別子（UUID 形式）。
    /// </summary>
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// トレーニング対象のモデル名。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><description><c>"demand-forecast"</c> - 需要予測モデル</description></item>
    ///   <item><description><c>"recommendation"</c> - レコメンデーションモデル</description></item>
    ///   <item><description><c>"chat-assistant"</c> - チャットアシスタントの微調整</description></item>
    ///   <item><description><c>"embedding"</c> - 商品埋め込みモデル</description></item>
    /// </list>
    /// </remarks>
    [Column("model_name")]
    [Required]
    [MaxLength(100)]
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// トレーニングによって生成されるモデルのバージョン。
    /// </summary>
    /// <remarks>
    /// トレーニング完了後に設定されます。
    /// セマンティックバージョニング形式（例: <c>"v2.1.0"</c>）を推奨します。
    /// </remarks>
    [Column("model_version")]
    [MaxLength(50)]
    public string? ModelVersion { get; set; }

    /// <summary>
    /// トレーニングジョブの現在のステータス。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><description><c>"PENDING"</c> - 実行待ち（デフォルト）</description></item>
    ///   <item><description><c>"RUNNING"</c> - 実行中</description></item>
    ///   <item><description><c>"COMPLETED"</c> - 正常完了</description></item>
    ///   <item><description><c>"FAILED"</c> - 失敗</description></item>
    ///   <item><description><c>"CANCELLED"</c> - キャンセル済み</description></item>
    /// </list>
    /// </remarks>
    [Column("status")]
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "PENDING";

    /// <summary>
    /// トレーニングを作成したユーザーの ID。
    /// </summary>
    /// <remarks>
    /// 管理者ユーザーまたはスケジュールされたジョブの識別子です。
    /// </remarks>
    [Column("created_by")]
    [MaxLength(36)]
    public string? CreatedBy { get; set; }

    /// <summary>
    /// トレーニング結果のメトリクス（JSON 形式）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// トレーニング完了後に設定されます。以下のようなメトリクスを含みます：
    /// </para>
    /// <list type="bullet">
    ///   <item><description><c>accuracy</c> - 精度</description></item>
    ///   <item><description><c>loss</c> - 損失値</description></item>
    ///   <item><description><c>f1_score</c> - F1 スコア</description></item>
    ///   <item><description><c>mse</c> - 平均二乗誤差（回帰モデル）</description></item>
    /// </list>
    /// <para>
    /// PostgreSQL の <c>jsonb</c> 型として格納されます。
    /// </para>
    /// </remarks>
    [Column("metrics_json", TypeName = "jsonb")]
    public string? MetricsJson { get; set; }

    /// <summary>
    /// トレーニングに使用したハイパーパラメータ（JSON 形式）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// モデルのトレーニング設定を格納します：
    /// </para>
    /// <list type="bullet">
    ///   <item><description><c>epochs</c> - エポック数</description></item>
    ///   <item><description><c>learningRate</c> - 学習率</description></item>
    ///   <item><description><c>batchSize</c> - バッチサイズ</description></item>
    ///   <item><description><c>trainingDataRange</c> - トレーニングデータの期間</description></item>
    /// </list>
    /// <para>
    /// PostgreSQL の <c>jsonb</c> 型として格納されます。
    /// </para>
    /// </remarks>
    [Column("parameters_json", TypeName = "jsonb")]
    public string? ParametersJson { get; set; }

    /// <summary>
    /// トレーニングが開始された日時（UTC）。
    /// </summary>
    /// <remarks>
    /// ステータスが <c>RUNNING</c> に変更された際に設定されます。
    /// </remarks>
    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// トレーニングが完了した日時（UTC）。
    /// </summary>
    /// <remarks>
    /// ステータスが <c>COMPLETED</c> または <c>FAILED</c> に変更された際に設定されます。
    /// <see cref="StartedAt"/> との差分でトレーニング所要時間を計算できます。
    /// </remarks>
    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// トレーニングジョブの作成日時（UTC）。
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// トレーニングジョブの最終更新日時（UTC）。
    /// </summary>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 楽観的同時実行制御用のタイムスタンプ。
    /// </summary>
    /// <remarks>
    /// EF Core がレコードの競合を検出するために使用します。
    /// 更新時に自動的にインクリメントされます。
    /// </remarks>
    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];
}
