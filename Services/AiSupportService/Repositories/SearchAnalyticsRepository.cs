using AiSupportService.Infrastructure.Persistence;
using AiSupportService.Models;
using AiSupportService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AiSupportService.Repositories;

/// <summary>
/// <see cref="ISearchAnalyticsRepository"/> の EF Core 実装。
/// </summary>
/// <remarks>
/// <para>
/// <strong>データベース操作:</strong>
/// PostgreSQL の search_analytics テーブルに対する CRUD 操作を提供します。
/// AI 検索機能の利用分析データを永続化し、サービス改善のための
/// 統計情報を提供します。
/// </para>
/// <para>
/// <strong>パフォーマンス考慮:</strong>
/// <list type="bullet">
///   <item><description>読み取りクエリでは AsNoTracking を使用し、変更追跡のオーバーヘッドを排除</description></item>
///   <item><description>created_at, user_id, search_type へのインデックスを前提とした設計</description></item>
///   <item><description>統計クエリは GroupBy / Distinct でサーバーサイド集計を実行</description></item>
///   <item><description>AverageAsync は NULL 許容で安全に処理</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>エラーハンドリング:</strong>
/// EF Core の例外（DbException）はそのまま上位に伝搬されます。
/// Service 層でビジネス例外への変換を行ってください。
/// </para>
/// </remarks>
/// <param name="context">EF Core DbContext。</param>
public class SearchAnalyticsRepository(AppDbContext context) : ISearchAnalyticsRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// DbSet.AddAsync を使用して新しい検索分析データを追跡対象に追加します。
    /// 検索 API 呼び出し時のログ記録に使用されます。
    /// </para>
    /// </remarks>
    public async Task AddAsync(SearchAnalytics analytics, CancellationToken ct = default)
        => await context.SearchAnalytics.AddAsync(analytics, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// search_analytics テーブルから created_at の範囲条件でフィルタし、
    /// created_at 降順で取得します。
    /// </para>
    /// <para>
    /// <strong>パフォーマンス考慮:</strong>
    /// 大量データの場合は FindByDateRangePagedAsync を使用してください。
    /// </para>
    /// </remarks>
    public async Task<List<SearchAnalytics>> FindByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
        => await context.SearchAnalytics
            .AsNoTracking()
            .Where(a => a.CreatedAt >= from && a.CreatedAt <= to)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// search_analytics テーブルから user_id でフィルタし、created_at 降順で取得します。
    /// ユーザーの検索履歴表示に使用されます。
    /// </para>
    /// </remarks>
    public async Task<List<SearchAnalytics>> FindByUserIdAsync(string userId, CancellationToken ct = default)
        => await context.SearchAnalytics
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

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
    /// search_analytics テーブルの created_at 範囲条件でカウントクエリを実行します。
    /// 検索利用量の統計に使用されます。
    /// </para>
    /// </remarks>
    public async Task<int> CountByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
        => await context.SearchAnalytics
            .Where(a => a.CreatedAt >= from && a.CreatedAt <= to)
            .CountAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// search_analytics テーブルから user_id が NULL でないレコードの
    /// user_id をユニークカウントします。
    /// </para>
    /// <para>
    /// <strong>生成される SQL（概要）:</strong>
    /// <code>
    /// SELECT COUNT(DISTINCT user_id) FROM search_analytics
    /// WHERE created_at BETWEEN @from AND @to AND user_id IS NOT NULL
    /// </code>
    /// </para>
    /// </remarks>
    public async Task<int> CountDistinctUsersByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
        => await context.SearchAnalytics
            .Where(a => a.CreatedAt >= from && a.CreatedAt <= to && a.UserId != null)
            .Select(a => a.UserId)
            .Distinct()
            .CountAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// search_analytics テーブルの response_time_ms カラムの平均値を計算します。
    /// NULL 許容の AverageAsync を使用し、データがない場合は 0.0 を返します。
    /// </para>
    /// <para>
    /// <strong>パフォーマンス考慮:</strong>
    /// AVG 集計関数はサーバーサイドで実行され、効率的に処理されます。
    /// </para>
    /// </remarks>
    public async Task<double> AverageResponseTimeByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
        => await context.SearchAnalytics
            .Where(a => a.CreatedAt >= from && a.CreatedAt <= to)
            .AverageAsync(a => (double?)a.ResponseTimeMs, ct) ?? 0.0;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// search_analytics テーブルを query でグループ化し、出現回数をカウント後、
    /// 降順でソートして上位 limit 件を取得します。
    /// </para>
    /// <para>
    /// <strong>生成される SQL（概要）:</strong>
    /// <code>
    /// SELECT query, COUNT(*) as count FROM search_analytics
    /// WHERE created_at BETWEEN @from AND @to
    /// GROUP BY query
    /// ORDER BY count DESC
    /// LIMIT @limit
    /// </code>
    /// </para>
    /// <para>
    /// <strong>パフォーマンス考慮:</strong>
    /// GROUP BY + ORDER BY + LIMIT はサーバーサイドで処理されます。
    /// query カラムへのインデックスがあると集計が効率化されます。
    /// </para>
    /// </remarks>
    public async Task<List<(string Query, int Count)>> GetTopQueriesByDateRangeAsync(DateTime from, DateTime to, int limit = 10, CancellationToken ct = default)
        => await context.SearchAnalytics
            .Where(a => a.CreatedAt >= from && a.CreatedAt <= to)
            .GroupBy(a => a.Query)
            .OrderByDescending(g => g.Count())
            .Take(limit)
            .Select(g => new ValueTuple<string, int>(g.Key, g.Count()))
            .ToListAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// search_analytics テーブルを search_type でグループ化し、各タイプの件数を集計します。
    /// サーバーサイドで集計処理を行うため、大量データでも効率的です。
    /// </para>
    /// </remarks>
    public async Task<Dictionary<string, int>> GetSearchTypeDistributionByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
        => await context.SearchAnalytics
            .Where(a => a.CreatedAt >= from && a.CreatedAt <= to)
            .GroupBy(a => a.SearchType)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

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
    /// created_at のインデックスが存在する場合、ソートとフィルタリングは効率的に処理されます。
    /// </para>
    /// </remarks>
    public async Task<(List<SearchAnalytics> Items, int TotalCount)> FindByDateRangePagedAsync(
        DateTime from, DateTime to, int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.SearchAnalytics.AsNoTracking()
            .Where(a => a.CreatedAt >= from && a.CreatedAt <= to)
            .OrderByDescending(a => a.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
