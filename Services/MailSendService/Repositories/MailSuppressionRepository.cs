using Microsoft.EntityFrameworkCore;
using MailSendService.Infrastructure.Persistence;
using MailSendService.Models;
using MailSendService.Repositories.Interfaces;

namespace MailSendService.Repositories;

/// <summary>
/// <see cref="MailSuppression"/> エンティティの EF Core リポジトリ実装。
/// </summary>
/// <remarks>
/// メール送信抑制リスト（配信停止・バウンス・苦情）の管理を行う。
/// メールアドレスと理由の組み合わせで検索可能。
/// </remarks>
/// <param name="context">アプリケーション DbContext。</param>
public class MailSuppressionRepository(AppDbContext context) : IMailSuppressionRepository
{
    /// <summary>
    /// 指定メールアドレスが送信抑制リストに含まれているかを確認する。
    /// </summary>
    /// <param name="email">確認対象のメールアドレス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>抑制対象の場合は <c>true</c>。</returns>
    public async Task<bool> IsSuppressedAsync(string email, CancellationToken ct = default)
        => await context.MailSuppressions.AnyAsync(s => s.Email == email, ct);

    /// <summary>
    /// メールアドレスで抑制レコードを検索する（読み取り専用）。
    /// </summary>
    /// <param name="email">検索対象のメールアドレス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>該当する抑制レコード。見つからない場合は <c>null</c>。</returns>
    public async Task<MailSuppression?> FindByEmailAsync(string email, CancellationToken ct = default)
        => await context.MailSuppressions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Email == email, ct);

    /// <summary>
    /// メールアドレスと抑制理由の組み合わせで抑制レコードを検索する（読み取り専用）。
    /// </summary>
    /// <param name="email">検索対象のメールアドレス。</param>
    /// <param name="reason">抑制理由（UNSUBSCRIBE / BOUNCE / COMPLAINT）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>該当する抑制レコード。見つからない場合は <c>null</c>。</returns>
    public async Task<MailSuppression?> FindByEmailAndReasonAsync(string email, string reason, CancellationToken ct = default)
        => await context.MailSuppressions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Email == email && s.Reason == reason, ct);

    /// <summary>
    /// 指定メールアドレスの全抑制レコードを取得する（変更追跡有効）。
    /// </summary>
    /// <param name="email">検索対象のメールアドレス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>該当する抑制レコードのリスト。</returns>
    public async Task<List<MailSuppression>> FindByUserEmailAsync(string email, CancellationToken ct = default)
        => await context.MailSuppressions
            .Where(s => s.Email == email)
            .ToListAsync(ct);

    /// <summary>
    /// 全抑制レコードをページネーション付きで取得する（読み取り専用、作成日降順）。
    /// </summary>
    /// <param name="page">ページ番号（1 始まり）。</param>
    /// <param name="pageSize">1 ページあたりの件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>指定ページの抑制レコードリスト。</returns>
    public async Task<List<MailSuppression>> FindAllAsync(int page, int pageSize, CancellationToken ct = default)
        => await context.MailSuppressions.AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

    /// <summary>
    /// 送信抑制レコードを追加する。
    /// </summary>
    /// <param name="suppression">追加する抑制エンティティ。</param>
    /// <param name="ct">キャンセルトークン。</param>
    public async Task AddAsync(MailSuppression suppression, CancellationToken ct = default)
        => await context.MailSuppressions.AddAsync(suppression, ct);

    /// <summary>
    /// 送信抑制レコードを削除する。
    /// </summary>
    /// <param name="suppression">削除対象の抑制エンティティ。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>完了タスク。</returns>
    public Task RemoveAsync(MailSuppression suppression, CancellationToken ct = default)
    {
        context.MailSuppressions.Remove(suppression);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 保留中の変更をデータベースに保存する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
