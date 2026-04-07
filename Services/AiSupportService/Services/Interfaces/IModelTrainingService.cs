using AiSupportService.DTOs.Responses;

namespace AiSupportService.Services.Interfaces;

/// <summary>
/// AI モデルのトレーニング管理を提供するサービスインターフェース。
/// </summary>
/// <remarks>
/// <para>
/// このサービスは、AI モデル（レコメンデーション、検索ランキング、需要予測など）の
/// トレーニングジョブを管理します。トレーニングの開始、進捗の監視、履歴の参照が可能です。
/// </para>
/// <para>
/// トレーニングジョブのライフサイクル:
/// <list type="number">
///   <item><description>PENDING: トレーニングがキューに追加された状態</description></item>
///   <item><description>RUNNING: トレーニングが実行中</description></item>
///   <item><description>COMPLETED: トレーニングが正常に完了</description></item>
///   <item><description>FAILED: トレーニングが失敗</description></item>
/// </list>
/// </para>
/// <para>
/// モデルバージョンは、トレーニング開始日時に基づいて自動生成されます
/// （例: v20260118153045）。これにより、モデルの世代管理が可能になります。
/// </para>
/// </remarks>
public interface IModelTrainingService
{
    /// <summary>
    /// 全てのモデルトレーニング履歴を取得する。
    /// </summary>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// トレーニング履歴レスポンスのリスト。モデル名、バージョン、ステータス、
    /// 開始・完了日時を含みます。トレーニング履歴がない場合は空のリストを返します。
    /// </returns>
    /// <remarks>
    /// 結果は作成日時の降順（新しい順）でソートされます。
    /// </remarks>
    Task<List<ModelTrainingResponse>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// 指定モデルのトレーニングを開始する。
    /// </summary>
    /// <param name="modelName">
    /// トレーニング対象のモデル名。例: "recommendation-v2", "search-ranking", "demand-forecast"
    /// </param>
    /// <param name="adminUserId">
    /// トレーニングを開始する管理者のユーザー ID。監査ログ用に記録されます。
    /// </param>
    /// <param name="ct">
    /// 操作のキャンセルに使用するキャンセルトークン。
    /// </param>
    /// <returns>
    /// 開始されたトレーニングのレスポンス。トレーニング ID、モデル名、バージョン、
    /// 初期ステータス（PENDING）を含みます。
    /// </returns>
    /// <remarks>
    /// <para>
    /// トレーニングは非同期で実行されます。このメソッドはトレーニングジョブを
    /// キューに追加し、即座にレスポンスを返します。
    /// </para>
    /// <para>
    /// 実際のトレーニング処理はバックグラウンドワーカーによって実行されます。
    /// トレーニングの進捗は <see cref="GetAllAsync"/> で確認できます。
    /// </para>
    /// </remarks>
    Task<ModelTrainingResponse> StartTrainingAsync(string modelName, string adminUserId, CancellationToken ct = default);
}
