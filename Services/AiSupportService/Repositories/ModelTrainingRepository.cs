using AiSupportService.Infrastructure.Persistence;
using AiSupportService.Models;
using AiSupportService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AiSupportService.Repositories;

/// <summary>
/// <see cref="IModelTrainingRepository"/> の EF Core 実装。
/// </summary>
/// <remarks>
/// <para>
/// <strong>データベース操作:</strong>
/// PostgreSQL の model_trainings テーブルに対する CRUD 操作を提供します。
/// AI/ML モデルのトレーニング履歴、ステータス、パフォーマンスメトリクスを管理します。
/// </para>
/// <para>
/// <strong>パフォーマンス考慮:</strong>
/// <list type="bullet">
///   <item><description>一覧取得クエリでは AsNoTracking を使用し、変更追跡のオーバーヘッドを排除</description></item>
///   <item><description>FindByIdAsync は更新用途を想定し、変更追跡を有効化</description></item>
///   <item><description>model_name および status へのインデックスを前提とした設計</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>エラーハンドリング:</strong>
/// EF Core の例外（DbException）はそのまま上位に伝搬されます。
/// Service 層でビジネス例外への変換を行ってください。
/// </para>
/// </remarks>
/// <param name="context">EF Core DbContext。</param>
public class ModelTrainingRepository(AppDbContext context) : IModelTrainingRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// model_trainings テーブルから created_at 降順で全件取得します。
    /// 管理画面でのトレーニング履歴一覧表示に使用されます。
    /// </para>
    /// <para>
    /// <strong>パフォーマンス考慮:</strong>
    /// 件数制限がないため、データ量が多い場合はページネーション対応の
    /// メソッドの追加を検討してください。
    /// </para>
    /// </remarks>
    public async Task<List<ModelTraining>> FindAllAsync(CancellationToken ct = default)
        => await context.ModelTrainings
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// model_trainings テーブルから主キー（id）で検索します。
    /// 変更追跡が有効なため、取得後のステータス更新が可能です。
    /// </para>
    /// </remarks>
    public async Task<ModelTraining?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.ModelTrainings
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// model_trainings テーブルから model_name と status = 'COMPLETED' でフィルタし、
    /// completed_at 降順で最新の 1 件を取得します。
    /// </para>
    /// <para>
    /// <strong>生成される SQL（概要）:</strong>
    /// <code>
    /// SELECT * FROM model_trainings
    /// WHERE model_name = @modelName AND status = 'COMPLETED'
    /// ORDER BY completed_at DESC
    /// LIMIT 1
    /// </code>
    /// </para>
    /// </remarks>
    public async Task<ModelTraining?> FindLatestByModelNameAsync(string modelName, CancellationToken ct = default)
        => await context.ModelTrainings
            .AsNoTracking()
            .Where(t => t.ModelName == modelName && t.Status == "COMPLETED")
            .OrderByDescending(t => t.CompletedAt)
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// DbSet.AddAsync を使用して新しいトレーニング記録を追跡対象に追加します。
    /// 実際の INSERT は SaveChangesAsync 呼び出し時に実行されます。
    /// </para>
    /// </remarks>
    public async Task AddAsync(ModelTraining training, CancellationToken ct = default)
        => await context.ModelTrainings.AddAsync(training, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// DbContext.SaveChangesAsync を呼び出し、追跡中の全ての変更を
    /// 単一のトランザクションでデータベースにコミットします。
    /// </para>
    /// </remarks>
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
