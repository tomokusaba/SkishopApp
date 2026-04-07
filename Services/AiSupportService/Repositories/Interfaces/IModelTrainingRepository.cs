using AiSupportService.Models;

namespace AiSupportService.Repositories.Interfaces;

/// <summary>
/// Repository for managing ModelTraining entity persistence.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Aggregate Root:</strong> ModelTraining
/// </para>
/// <para>
/// このリポジトリは AI/ML モデルのトレーニング履歴を管理します。
/// モデルのバージョン管理、トレーニング結果の追跡、
/// パフォーマンスメトリクスの記録を行います。
/// </para>
/// <para>
/// <strong>主な責務:</strong>
/// <list type="bullet">
///   <item><description>トレーニング記録の永続化</description></item>
///   <item><description>モデル別の履歴管理</description></item>
///   <item><description>最新の完了済みモデルの取得</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>トレーニングステータス:</strong>
/// <list type="bullet">
///   <item><description>PENDING: トレーニング待機中</description></item>
///   <item><description>RUNNING: トレーニング実行中</description></item>
///   <item><description>COMPLETED: トレーニング完了</description></item>
///   <item><description>FAILED: トレーニング失敗</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IModelTrainingRepository
{
    /// <summary>
    /// 全てのモデルトレーニング記録を作成日時の降順で取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 管理画面でのトレーニング履歴一覧表示に使用されます。
    /// 最新のトレーニングから過去のトレーニングまで時系列で確認できます。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>CreatedAt の降順でソートされる</description></item>
    ///   <item><description>全てのステータスのトレーニング記録を含む</description></item>
    ///   <item><description>読み取り専用クエリとして実行される</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>モデルトレーニング記録のリスト。</returns>
    Task<List<ModelTraining>> FindAllAsync(CancellationToken ct = default);

    /// <summary>
    /// 指定された ID のモデルトレーニング記録を取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// トレーニング詳細の表示やステータス更新に使用されます。
    /// 変更追跡が有効な状態で取得するため、更新操作が可能です。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>存在しない ID を指定した場合は null を返す</description></item>
    ///   <item><description>変更追跡が有効（更新可能な状態で取得）</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="id">トレーニング記録 ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>見つかった場合はトレーニング記録、見つからない場合は null。</returns>
    Task<ModelTraining?> FindByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// 指定されたモデル名の完了済みトレーニング記録のうち最新のものを取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 推論実行時に最新の有効なモデルを取得するために使用されます。
    /// Status が "COMPLETED" のトレーニング記録のみを対象とします。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>Status = "COMPLETED" のみを対象とする</description></item>
    ///   <item><description>CompletedAt の降順で最新の 1 件を返す</description></item>
    ///   <item><description>完了済みトレーニングがない場合は null を返す</description></item>
    ///   <item><description>読み取り専用クエリとして実行される</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="modelName">モデル名（例: "demand-forecast", "recommendation"）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>見つかった場合は最新のトレーニング記録、見つからない場合は null。</returns>
    Task<ModelTraining?> FindLatestByModelNameAsync(string modelName, CancellationToken ct = default);

    /// <summary>
    /// 新しいモデルトレーニング記録を追加する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 新規トレーニングジョブの開始時に呼び出されます。
    /// 追加された記録は <see cref="SaveChangesAsync"/> を呼び出すまで永続化されません。
    /// </para>
    /// <para>
    /// <strong>期待される動作:</strong>
    /// <list type="bullet">
    ///   <item><description>トレーニング記録を DbContext に追加する（未コミット状態）</description></item>
    ///   <item><description>Id は事前に設定されている必要がある</description></item>
    ///   <item><description>初期 Status は "PENDING" または "RUNNING" を想定</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="training">追加するトレーニング記録。</param>
    /// <param name="ct">キャンセルトークン。</param>
    Task AddAsync(ModelTraining training, CancellationToken ct = default);

    /// <summary>
    /// 保留中の変更をデータベースに保存する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unit of Work パターンに基づき、追跡中の全ての変更を
    /// 単一のトランザクションとしてデータベースにコミットします。
    /// </para>
    /// </remarks>
    /// <param name="ct">キャンセルトークン。</param>
    /// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateException">データベース更新時にエラーが発生した場合。</exception>
    Task SaveChangesAsync(CancellationToken ct = default);
}
