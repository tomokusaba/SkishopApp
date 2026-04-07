using AiSupportService.Infrastructure.Persistence;
using AiSupportService.Models;
using AiSupportService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AiSupportService.Repositories;

/// <summary>
/// <see cref="IDemandForecastRepository"/> の EF Core 実装。
/// </summary>
/// <remarks>
/// <para>
/// <strong>データベース操作:</strong>
/// PostgreSQL の demand_forecasts テーブルに対する CRUD 操作を提供します。
/// AI モデルによる需要予測結果を永続化し、在庫管理の意思決定を支援します。
/// </para>
/// <para>
/// <strong>パフォーマンス考慮:</strong>
/// <list type="bullet">
///   <item><description>読み取りクエリでは AsNoTracking を使用し、変更追跡のオーバーヘッドを排除</description></item>
///   <item><description>FindAllAsync は最大 100 件に制限し、大量データでのメモリ消費を防止</description></item>
///   <item><description>product_id および created_at へのインデックスを前提とした設計</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>エラーハンドリング:</strong>
/// EF Core の例外（DbException）はそのまま上位に伝搬されます。
/// Service 層でビジネス例外への変換を行ってください。
/// </para>
/// </remarks>
/// <param name="context">EF Core DbContext。</param>
public class DemandForecastRepository(AppDbContext context) : IDemandForecastRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// demand_forecasts テーブルから product_id でフィルタし、forecast_date 降順で取得します。
    /// 最新の予測から過去の予測まで時系列で確認できます。
    /// </para>
    /// </remarks>
    public async Task<List<DemandForecast>> FindByProductIdAsync(string productId, CancellationToken ct = default)
        => await context.DemandForecasts
            .AsNoTracking()
            .Where(f => f.ProductId == productId)
            .OrderByDescending(f => f.ForecastDate)
            .ToListAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// demand_forecasts テーブルから created_at 降順で最大 100 件取得します。
    /// </para>
    /// <para>
    /// <strong>パフォーマンス考慮:</strong>
    /// Take(100) により結果セットサイズを制限し、大量データでのメモリ消費を防止しています。
    /// 大量データが必要な場合は FindAllPagedAsync を使用してください。
    /// </para>
    /// </remarks>
    public async Task<List<DemandForecast>> FindAllAsync(CancellationToken ct = default)
        => await context.DemandForecasts
            .AsNoTracking()
            .OrderByDescending(f => f.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// DbSet.AddAsync を使用して新しい予測データを追跡対象に追加します。
    /// 実際の INSERT は SaveChangesAsync 呼び出し時に実行されます。
    /// </para>
    /// </remarks>
    public async Task AddAsync(DemandForecast forecast, CancellationToken ct = default)
        => await context.DemandForecasts.AddAsync(forecast, ct);

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

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// demand_forecasts テーブルから product_id でフィルタし、
    /// created_at 降順で上位 limit 件を取得します。
    /// 直近の予測トレンドを素早く確認するために使用されます。
    /// </para>
    /// </remarks>
    public async Task<List<DemandForecast>> FindByProductIdRecentAsync(
        string productId, int limit = 10, CancellationToken ct = default)
        => await context.DemandForecasts
            .AsNoTracking()
            .Where(f => f.ProductId == productId)
            .OrderByDescending(f => f.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// 2 回のクエリを実行します: 1) 全件数のカウント、2) ページ分のデータ取得。
    /// Skip/Take による効率的なページネーションを実装しています。
    /// </para>
    /// <para>
    /// <strong>パフォーマンス考慮:</strong>
    /// 大きなページ番号（深いページ）ではオフセットスキャンのコストが増加する可能性があります。
    /// created_at のインデックスが存在する場合、ソートは効率的に処理されます。
    /// </para>
    /// </remarks>
    public async Task<(List<DemandForecast> Items, int TotalCount)> FindAllPagedAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.DemandForecasts.AsNoTracking()
            .OrderByDescending(f => f.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
