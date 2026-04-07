using Microsoft.EntityFrameworkCore;
using MailSendService.Infrastructure.Persistence;
using MailSendService.Models;
using MailSendService.Repositories.Interfaces;

namespace MailSendService.Repositories;

/// <summary>
/// <see cref="MailLog"/> エンティティの EF Core リポジトリ実装。
/// </summary>
/// <remarks>
/// 読み取り専用クエリでは <c>AsNoTracking()</c> を使用してパフォーマンスを最適化する。
/// 書き込み操作では変更追跡を有効にし、<see cref="SaveChangesAsync"/> で永続化する。
/// </remarks>
/// <param name="context">アプリケーション DbContext。</param>
public class MailLogRepository(AppDbContext context) : IMailLogRepository
{
    /// <summary>
    /// 指定された ID のメールログを添付ファイル含めて取得する。
    /// </summary>
    /// <param name="id">メールログ ID。</param>
    /// <param name="trackChanges">変更追跡を有効にするかどうか。読み取り専用の場合は false を指定。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>該当するメールログ。見つからない場合は <c>null</c>。</returns>
    public async Task<MailLog?> FindByIdAsync(string id, bool trackChanges = true, CancellationToken ct = default)
    {
        var query = context.MailLogs.Include(m => m.Attachments);
        if (!trackChanges)
            return await query.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
        return await query.FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    /// <summary>
    /// イベント ID でメールログを検索する（読み取り専用）。
    /// </summary>
    /// <param name="eventId">Kafka イベント ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>該当するメールログ。見つからない場合は <c>null</c>。</returns>
    public async Task<MailLog?> FindByEventIdAsync(string eventId, CancellationToken ct = default)
        => await context.MailLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.EventId == eventId, ct);

    /// <summary>
    /// 指定ステータスのメールログを作成日昇順で取得する（読み取り専用）。
    /// </summary>
    /// <param name="status">フィルタするステータス（PENDING, SENT, FAILED 等）。</param>
    /// <param name="limit">取得上限件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>条件に一致するメールログのリスト。</returns>
    public async Task<List<MailLog>> FindByStatusAsync(string status, int limit = 50, CancellationToken ct = default)
        => await context.MailLogs
            .AsNoTracking()
            .Where(m => m.Status == status)
            .OrderBy(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

    /// <summary>
    /// 受信者メールアドレスでメールログをページネーション付きで検索する（読み取り専用）。
    /// </summary>
    /// <param name="recipientEmail">受信者メールアドレス。</param>
    /// <param name="page">ページ番号（1 始まり）。</param>
    /// <param name="pageSize">1 ページあたりの件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>条件に一致するメールログのリスト（作成日降順）。</returns>
    public async Task<List<MailLog>> FindByRecipientEmailAsync(string recipientEmail, int page, int pageSize, CancellationToken ct = default)
        => await context.MailLogs
            .AsNoTracking()
            .Where(m => m.RecipientEmail == recipientEmail)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    /// <summary>
    /// 指定期間以降に特定の受信者へ送信された（スキップ除く）メール件数を取得する。
    /// </summary>
    /// <param name="recipientEmail">受信者メールアドレス。</param>
    /// <param name="since">集計開始日時。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>該当するメール件数。</returns>
    public async Task<int> CountByRecipientSinceAsync(string recipientEmail, DateTimeOffset since, CancellationToken ct = default)
        => await context.MailLogs
            .CountAsync(m => m.RecipientEmail == recipientEmail
                && m.CreatedAt >= since
                && m.Status != MailLogStatus.Skipped, ct);

    /// <summary>
    /// 指定ステータスのメールログ件数を取得する。
    /// </summary>
    /// <param name="status">ステータス文字列。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>該当するメールログの件数。</returns>
    public async Task<long> CountByStatusAsync(string status, CancellationToken ct = default)
        => await context.MailLogs.LongCountAsync(m => m.Status == status, ct);

    /// <summary>
    /// テンプレート名別の送信成功メール件数を集計する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>テンプレート名をキー、送信成功件数を値とする辞書。</returns>
    public async Task<Dictionary<string, long>> CountByTemplateAsync(CancellationToken ct = default)
        => await context.MailLogs
            .Where(m => m.Status == MailLogStatus.Sent)
            .GroupBy(m => m.TemplateName)
            .Select(g => new { Template = g.Key, Count = g.LongCount() })
            .ToDictionaryAsync(g => g.Template, g => g.Count, ct);

    /// <summary>
    /// リトライ対象の失敗メールログを取得する（P1-6: AsNoTracking 追加）。
    /// </summary>
    /// <param name="maxRetryCount">最大リトライ回数（この回数未満のレコードを返す）。</param>
    /// <param name="limit">取得上限件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>FAILED ステータスかつリトライ回数が上限未満のメールログリスト（更新日昇順）。</returns>
    /// <remarks>
    /// P1-6: リトライ処理では取得後に FindByIdAsync で再取得するため、
    /// このメソッドは読み取り専用として AsNoTracking を適用する。
    /// </remarks>
    public async Task<List<MailLog>> FindFailedForRetryAsync(int maxRetryCount, int limit = 20, CancellationToken ct = default)
        => await context.MailLogs
            .AsNoTracking()
            .Where(m => m.Status == MailLogStatus.Failed && m.RetryCount < maxRetryCount)
            .OrderBy(m => m.UpdatedAt)
            .Take(limit)
            .ToListAsync(ct);

    /// <summary>
    /// 指定日時より古い未匿名化のメールログを取得する（PII クリーンアップ用）。
    /// </summary>
    /// <param name="cutoff">この日時より前に作成されたレコードを対象とする。</param>
    /// <param name="limit">取得上限件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>匿名化対象のメールログリスト（@anonymized.local ドメインを除外）。</returns>
    public async Task<List<MailLog>> FindOlderThanAsync(DateTimeOffset cutoff, int limit = 100, CancellationToken ct = default)
        => await context.MailLogs
            .Where(m => m.CreatedAt < cutoff && !m.RecipientEmail.EndsWith("@anonymized.local"))
            .OrderBy(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

    /// <summary>
    /// メールログをページネーション付きで全件取得する（読み取り専用）。
    /// </summary>
    /// <param name="page">ページ番号（1 始まり）。</param>
    /// <param name="pageSize">1 ページあたりの件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>メールログリストと総件数のタプル。</returns>
    public async Task<(List<MailLog> Items, long TotalCount)> FindAllPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.MailLogs.AsNoTracking().OrderByDescending(m => m.CreatedAt);
        var totalCount = await query.LongCountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, totalCount);
    }

    /// <summary>
    /// 受信者ユーザー ID でメールログを検索する（P1-7: ページネーション追加）。
    /// </summary>
    /// <param name="userId">受信者のユーザー ID。</param>
    /// <param name="limit">取得上限件数（GDPR 匿名化用途のため 1000 を推奨）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>該当するメールログのリスト。</returns>
    public async Task<List<MailLog>> FindByRecipientUserIdAsync(string userId, int limit = 1000, CancellationToken ct = default)
        => await context.MailLogs
            .Where(m => m.RecipientUserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

    /// <summary>
    /// ユーザー ID から受信者メールアドレスを取得する（読み取り専用）。
    /// </summary>
    /// <param name="userId">ユーザー ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>最初に見つかったメールアドレス。見つからない場合は <c>null</c>。</returns>
    public async Task<string?> FindEmailByUserIdAsync(string userId, CancellationToken ct = default)
        => await context.MailLogs
            .AsNoTracking()
            .Where(m => m.RecipientUserId == userId)
            .Select(m => m.RecipientEmail)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// 指定ユーザーの未送信（PENDING）メールログを取得する（P1-8: 上限追加）。
    /// </summary>
    /// <param name="userId">受信者のユーザー ID。</param>
    /// <param name="limit">取得上限件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>PENDING ステータスのメールログリスト。</returns>
    public async Task<List<MailLog>> FindPendingByUserIdAsync(string userId, int limit = 100, CancellationToken ct = default)
        => await context.MailLogs
            .Where(m => m.RecipientUserId == userId && m.Status == MailLogStatus.Pending)
            .OrderBy(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

    /// <summary>
    /// 一定時間以上 SENDING ステータスのままの孤児化メールログを取得する（P1-14）。
    /// </summary>
    /// <param name="cutoff">この日時より前に SENDING になったレコードを対象とする。</param>
    /// <param name="limit">取得上限件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>孤児化したメールログのリスト。</returns>
    public async Task<List<MailLog>> FindOrphanedSendingAsync(DateTimeOffset cutoff, int limit = 50, CancellationToken ct = default)
        => await context.MailLogs
            .Where(m => m.Status == MailLogStatus.Sending && m.UpdatedAt < cutoff)
            .OrderBy(m => m.UpdatedAt)
            .Take(limit)
            .ToListAsync(ct);

    /// <summary>
    /// メールログを追加する。
    /// </summary>
    /// <param name="mailLog">追加するメールログエンティティ。</param>
    /// <param name="ct">キャンセルトークン。</param>
    public async Task AddAsync(MailLog mailLog, CancellationToken ct = default)
        => await context.MailLogs.AddAsync(mailLog, ct);

    /// <summary>
    /// 保留中の変更をデータベースに保存する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
