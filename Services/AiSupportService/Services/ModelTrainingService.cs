using AiSupportService.DTOs.Responses;
using AiSupportService.Models;
using AiSupportService.Repositories.Interfaces;
using AiSupportService.Services.Interfaces;

namespace AiSupportService.Services;

/// <summary>
/// <see cref="IModelTrainingService"/> の実装。AI モデルのトレーニングジョブの登録・一覧取得を行う。
/// </summary>
/// <remarks>
/// <para>
/// このサービスは、AI モデル（レコメンデーション、検索ランキング、需要予測など）の
/// トレーニングジョブを管理します。
/// </para>
/// <para>
/// 現在の実装ではトレーニングジョブの登録のみを行い、実際のトレーニング処理は
/// 別のバックグラウンドワーカーまたは外部システムで実行されることを想定しています。
/// </para>
/// <para>
/// モデルバージョン命名規則:
/// <c>v{yyyyMMddHHmmss}</c> 形式（例: v20260118153045）
/// </para>
/// </remarks>
/// <param name="modelTrainingRepository">モデルトレーニングリポジトリ。</param>
/// <param name="logger">ロガー。</param>
public class ModelTrainingService(
    IModelTrainingRepository modelTrainingRepository,
    ILogger<ModelTrainingService> logger) : IModelTrainingService
{
    /// <inheritdoc />
    /// <remarks>
    /// 結果は作成日時の降順（新しい順）でソートされます。
    /// </remarks>
    public async Task<List<ModelTrainingResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var trainings = await modelTrainingRepository.FindAllAsync(ct);
        return trainings.Select(t => new ModelTrainingResponse(
            t.Id, t.ModelName, t.ModelVersion, t.Status,
            t.StartedAt, t.CompletedAt, t.CreatedAt)).ToList();
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// トレーニング開始の処理フロー:
    /// <list type="number">
    ///   <item><description>新しいトレーニングジョブを PENDING 状態で作成</description></item>
    ///   <item><description>モデルバージョンを現在日時から生成</description></item>
    ///   <item><description>作成者として管理者ユーザー ID を記録</description></item>
    ///   <item><description>データベースに保存</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 実際のトレーニング処理は非同期で実行され、このメソッドは
    /// ジョブがキューに追加された時点で即座にレスポンスを返します。
    /// </para>
    /// </remarks>
    public async Task<ModelTrainingResponse> StartTrainingAsync(string modelName, string adminUserId, CancellationToken ct = default)
    {
        var training = new ModelTraining
        {
            ModelName = modelName,
            Status = "PENDING",
            ModelVersion = $"v{DateTime.UtcNow:yyyyMMddHHmmss}",
            CreatedBy = adminUserId
        };
        await modelTrainingRepository.AddAsync(training, ct);
        await modelTrainingRepository.SaveChangesAsync(ct);

        logger.LogInformation("モデルトレーニング開始: ModelName={ModelName}, Version={Version}",
            modelName, training.ModelVersion);

        return new ModelTrainingResponse(
            training.Id, training.ModelName, training.ModelVersion,
            training.Status, training.StartedAt, training.CompletedAt, training.CreatedAt);
    }
}
