using AiSupportService.Infrastructure.Persistence;
using AiSupportService.Models;
using AiSupportService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AiSupportService.Repositories;

/// <summary>
/// <see cref="IChatMessageRepository"/> の EF Core 実装。
/// </summary>
/// <remarks>
/// <para>
/// <strong>データベース操作:</strong>
/// PostgreSQL の chat_messages テーブルに対する読み取り専用操作を提供します。
/// ChatMessage は ChatSession の子エンティティであるため、書き込み操作は
/// <see cref="ChatSessionRepository"/> を通じて行われます。
/// </para>
/// <para>
/// <strong>パフォーマンス考慮:</strong>
/// <list type="bullet">
///   <item><description>全ての読み取りクエリで AsNoTracking を使用し、変更追跡のオーバーヘッドを排除</description></item>
///   <item><description>セッション ID に対するインデックスを前提とした設計</description></item>
///   <item><description>カウントクエリは COUNT(*) に変換され、全件取得より効率的</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>エラーハンドリング:</strong>
/// EF Core の例外（DbException）はそのまま上位に伝搬されます。
/// Service 層でビジネス例外への変換を行ってください。
/// </para>
/// </remarks>
/// <param name="context">EF Core DbContext。</param>
public class ChatMessageRepository(AppDbContext context) : IChatMessageRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// chat_messages テーブルから session_id でフィルタし、created_at 昇順で取得します。
    /// </para>
    /// <para>
    /// <strong>生成される SQL（概要）:</strong>
    /// <code>
    /// SELECT * FROM chat_messages
    /// WHERE session_id = @sessionId
    /// ORDER BY created_at ASC
    /// </code>
    /// </para>
    /// </remarks>
    public async Task<List<ChatMessage>> FindBySessionIdAsync(string sessionId, CancellationToken ct = default)
        => await context.ChatMessages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// chat_messages テーブルの session_id 条件でカウントクエリを実行します。
    /// 全件取得せずに件数のみを取得するため、大量メッセージでも高速です。
    /// </para>
    /// </remarks>
    public async Task<int> CountBySessionIdAsync(string sessionId, CancellationToken ct = default)
        => await context.ChatMessages
            .CountAsync(m => m.SessionId == sessionId, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>データベース操作:</strong>
    /// chat_messages テーブルの created_at 範囲条件でカウントクエリを実行します。
    /// 統計情報の集計に使用されます。
    /// </para>
    /// <para>
    /// <strong>パフォーマンス考慮:</strong>
    /// created_at カラムへのインデックスが存在する場合、範囲スキャンで効率的に処理されます。
    /// </para>
    /// </remarks>
    public async Task<int> CountByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
        => await context.ChatMessages
            .CountAsync(m => m.CreatedAt >= from && m.CreatedAt <= to, ct);
}
