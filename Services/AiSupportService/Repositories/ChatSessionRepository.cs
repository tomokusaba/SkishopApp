using AiSupportService.Infrastructure.Persistence;
using AiSupportService.Models;
using AiSupportService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AiSupportService.Repositories;

/// <summary>
/// <see cref="IChatSessionRepository"/> の EF Core 実装。
/// </summary>
/// <remarks>
/// <para>
/// <strong>データベース操作:</strong>
/// PostgreSQL の chat_sessions テーブルに対する CRUD 操作を提供します。
/// ChatSession は Aggregate Root として、関連する ChatMessage の
/// ライフサイクルも管理します。
/// </para>
/// <para>
/// <strong>パフォーマンス考慮:</strong>
/// <list type="bullet">
///   <item><description>一覧取得クエリでは AsNoTracking を使用し、変更追跡のオーバーヘッドを排除</description></item>
///   <item><description>詳細取得では Include による Eager Loading で N+1 問題を防止</description></item>
///   <item><description>ページネーション対応メソッドでは Skip/Take による効率的な取得を実装</description></item>
///   <item><description>統計クエリは GroupBy + ToDictionary でサーバーサイド集計を実行</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>エラーハンドリング:</strong>
/// EF Core の例外（DbException）はそのまま上位に伝搬されます。
/// Service 層でビジネス例外への変換を行ってください。
/// </para>
/// </remarks>
/// <param name="context">EF Core DbContext。</param>
public class ChatSessionRepository(AppDbContext context) : IChatSessionRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// chat_sessions テーブルと chat_messages テーブルを JOIN し、
    /// 指定 ID のセッションをメッセージ付きで取得します。
    /// </para>
    /// <para>
    /// <strong>パフォーマンス考慮:</strong>
    /// Include による Eager Loading を使用し、1 回のクエリで関連データを取得します。
    /// 変更追跡が有効なため、取得後の更新操作が可能です。
    /// </para>
    /// </remarks>
    public async Task<ChatSession?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.ChatSessions
            .Include(s => s.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// chat_sessions テーブルから id と user_id の両方が一致するセッションを取得します。
    /// IDOR 防止のためのオーナーシップ検証を兼ねています。
    /// </para>
    /// </remarks>
    public async Task<ChatSession?> FindByIdAndUserIdAsync(string id, string userId, CancellationToken ct = default)
        => await context.ChatSessions
            .Include(s => s.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// chat_sessions テーブルから user_id でフィルタし、updated_at 降順で取得します。
    /// </para>
    /// <para>
    /// <strong>パフォーマンス考慮:</strong>
    /// AsNoTracking を使用し、読み取り専用クエリとして最適化されています。
    /// Messages は Include しないため、一覧表示用に軽量です。
    /// </para>
    /// </remarks>
    public async Task<List<ChatSession>> FindByUserIdAsync(string userId, CancellationToken ct = default)
        => await context.ChatSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// chat_sessions テーブルから status = 'ACTIVE' かつ指定 user_id のレコード数をカウントします。
    /// </para>
    /// </remarks>
    public async Task<int> CountActiveByUserIdAsync(string userId, CancellationToken ct = default)
        => await context.ChatSessions
            .CountAsync(s => s.UserId == userId && s.Status == "ACTIVE", ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// chat_sessions テーブルから created_at の範囲条件で取得します。
    /// 管理者向けの分析用途を想定しています。
    /// </para>
    /// </remarks>
    public async Task<List<ChatSession>> FindByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
        => await context.ChatSessions
            .AsNoTracking()
            .Where(s => s.CreatedAt >= from && s.CreatedAt <= to)
            .ToListAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// DbSet.AddAsync を使用して新しいセッションを追跡対象に追加します。
    /// 実際の INSERT は SaveChangesAsync 呼び出し時に実行されます。
    /// </para>
    /// </remarks>
    public async Task AddAsync(ChatSession session, CancellationToken ct = default)
        => await context.ChatSessions.AddAsync(session, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// DbContext.SaveChangesAsync を呼び出し、追跡中の全ての変更を
    /// 単一のトランザクションでデータベースにコミットします。
    /// </para>
    /// <para>
    /// <strong>エラーハンドリング:</strong>
    /// 一意制約違反、外部キー制約違反などは DbUpdateException として伝搬されます。
    /// </para>
    /// </remarks>
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// chat_sessions テーブルの created_at 範囲条件でカウントクエリを実行します。
    /// </para>
    /// </remarks>
    public async Task<int> CountByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
        => await context.ChatSessions
            .Where(s => s.CreatedAt >= from && s.CreatedAt <= to)
            .CountAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// chat_sessions テーブルの created_at 範囲と status 条件でカウントクエリを実行します。
    /// ステータス別の統計に使用されます。
    /// </para>
    /// </remarks>
    public async Task<int> CountByDateRangeAndStatusAsync(DateTime from, DateTime to, string status, CancellationToken ct = default)
        => await context.ChatSessions
            .Where(s => s.CreatedAt >= from && s.CreatedAt <= to && s.Status == status)
            .CountAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// chat_sessions テーブルを status でグループ化し、各ステータスの件数を集計します。
    /// サーバーサイドで集計処理を行うため、大量データでも効率的です。
    /// </para>
    /// <para>
    /// <strong>生成される SQL（概要）:</strong>
    /// <code>
    /// SELECT status, COUNT(*) FROM chat_sessions
    /// WHERE created_at BETWEEN @from AND @to
    /// GROUP BY status
    /// </code>
    /// </para>
    /// </remarks>
    public async Task<Dictionary<string, int>> GetStatusDistributionByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
        => await context.ChatSessions
            .Where(s => s.CreatedAt >= from && s.CreatedAt <= to)
            .GroupBy(s => s.Status)
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
    /// created_at のインデックスが存在する場合、ソートは効率的に処理されます。
    /// </para>
    /// </remarks>
    public async Task<(List<ChatSession> Items, int TotalCount)> FindByUserIdPagedAsync(
        string userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.ChatSessions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
