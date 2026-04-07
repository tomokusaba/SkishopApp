using AiSupportService.Infrastructure.Persistence;
using AiSupportService.Models;
using AiSupportService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AiSupportService.Repositories;

/// <summary>
/// <see cref="IRecommendationRepository"/> の EF Core 実装。
/// </summary>
/// <remarks>
/// <para>
/// <strong>データベース操作:</strong>
/// PostgreSQL の recommendations テーブルに対する CRUD 操作を提供します。
/// AI ベースの商品レコメンデーションデータを永続化し、
/// パーソナライズされた商品推薦を支援します。
/// </para>
/// <para>
/// <strong>パフォーマンス考慮:</strong>
/// <list type="bullet">
///   <item><description>読み取りクエリでは AsNoTracking を使用し、変更追跡のオーバーヘッドを排除</description></item>
///   <item><description>user_id, type, created_at へのインデックスを前提とした設計</description></item>
///   <item><description>AddRangeAsync でバッチ挿入を効率化</description></item>
///   <item><description>統計クエリは GroupBy でサーバーサイド集計を実行</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>エラーハンドリング:</strong>
/// EF Core の例外（DbException）はそのまま上位に伝搬されます。
/// Service 層でビジネス例外への変換を行ってください。
/// </para>
/// </remarks>
/// <param name="context">EF Core DbContext。</param>
public class RecommendationRepository(AppDbContext context) : IRecommendationRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// recommendations テーブルから user_id でフィルタし、created_at 降順で取得します。
    /// 最新のレコメンデーションが先頭に表示されます。
    /// </para>
    /// </remarks>
    public async Task<List<Recommendation>> FindByUserIdAsync(string userId, CancellationToken ct = default)
        => await context.Recommendations
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// recommendations テーブルから user_id と type でフィルタし、score 降順で取得します。
    /// 推薦スコアの高い順に表示するため、商品詳細ページなどで使用されます。
    /// </para>
    /// </remarks>
    public async Task<List<Recommendation>> FindByUserIdAndTypeAsync(string userId, string type, CancellationToken ct = default)
        => await context.Recommendations
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.Type == type)
            .OrderByDescending(r => r.Score)
            .ToListAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// recommendations テーブルから created_at の範囲条件で取得します。
    /// 管理者向けの分析用途を想定しています。
    /// </para>
    /// </remarks>
    public async Task<List<Recommendation>> FindByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
        => await context.Recommendations
            .AsNoTracking()
            .Where(r => r.CreatedAt >= from && r.CreatedAt <= to)
            .ToListAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// recommendations テーブルから主キー（id）で検索します。
    /// 変更追跡が有効なため、IsViewed フラグの更新などが可能です。
    /// </para>
    /// </remarks>
    public async Task<Recommendation?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.Recommendations
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// DbSet.AddAsync を使用して新しいレコメンデーションを追跡対象に追加します。
    /// 実際の INSERT は SaveChangesAsync 呼び出し時に実行されます。
    /// </para>
    /// </remarks>
    public async Task AddAsync(Recommendation recommendation, CancellationToken ct = default)
        => await context.Recommendations.AddAsync(recommendation, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// DbSet.AddRangeAsync を使用して複数のレコメンデーションを一括で追跡対象に追加します。
    /// バッチ処理でのレコメンデーション生成時に効率的です。
    /// </para>
    /// <para>
    /// <strong>パフォーマンス考慮:</strong>
    /// 個別の AddAsync を繰り返すよりも効率的に処理されます。
    /// ただし、非常に大量のデータ（数千件以上）の場合は分割挿入を検討してください。
    /// </para>
    /// </remarks>
    public async Task AddRangeAsync(IEnumerable<Recommendation> recommendations, CancellationToken ct = default)
        => await context.Recommendations.AddRangeAsync(recommendations, ct);

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
    /// recommendations テーブルの created_at 範囲条件でカウントクエリを実行します。
    /// レコメンデーション生成量の統計に使用されます。
    /// </para>
    /// </remarks>
    public async Task<int> CountByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
        => await context.Recommendations
            .Where(r => r.CreatedAt >= from && r.CreatedAt <= to)
            .CountAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// recommendations テーブルの created_at 範囲と is_viewed = true 条件でカウントクエリを実行します。
    /// レコメンデーション閲覧率（CTR）の計算に使用されます。
    /// </para>
    /// </remarks>
    public async Task<int> CountViewedByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
        => await context.Recommendations
            .Where(r => r.CreatedAt >= from && r.CreatedAt <= to && r.IsViewed)
            .CountAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// recommendations テーブルを type でグループ化し、各タイプの件数を集計します。
    /// サーバーサイドで集計処理を行うため、大量データでも効率的です。
    /// </para>
    /// <para>
    /// <strong>生成される SQL（概要）:</strong>
    /// <code>
    /// SELECT type, COUNT(*) FROM recommendations
    /// WHERE created_at BETWEEN @from AND @to
    /// GROUP BY type
    /// </code>
    /// </para>
    /// </remarks>
    public async Task<Dictionary<string, int>> GetTypeDistributionByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
        => await context.Recommendations
            .Where(r => r.CreatedAt >= from && r.CreatedAt <= to)
            .GroupBy(r => r.Type)
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
    /// (user_id, created_at) の複合インデックスが存在する場合、効率的に処理されます。
    /// </para>
    /// </remarks>
    public async Task<(List<Recommendation> Items, int TotalCount)> FindByUserIdPagedAsync(
        string userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.Recommendations.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
