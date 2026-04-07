using Microsoft.EntityFrameworkCore;
using MailSendService.Infrastructure.Persistence;
using MailSendService.Models;
using MailSendService.Repositories.Interfaces;

namespace MailSendService.Repositories;

/// <summary>
/// <see cref="MailTemplate"/> エンティティの EF Core リポジトリ実装。
/// </summary>
/// <remarks>
/// 読み取り専用クエリでは <c>AsNoTracking()</c> を使用する。ページネーション付き一覧取得をサポートする。
/// </remarks>
/// <param name="context">アプリケーション DbContext。</param>
public class MailTemplateRepository(AppDbContext context) : IMailTemplateRepository
{
    /// <summary>
    /// 指定された ID のメールテンプレートを取得する。
    /// </summary>
    /// <param name="id">テンプレート ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>該当するテンプレート。見つからない場合は <c>null</c>。</returns>
    public async Task<MailTemplate?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.MailTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);

    /// <summary>
    /// テンプレート名で検索する（読み取り専用）。
    /// </summary>
    /// <param name="name">テンプレート名。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>該当するテンプレート。見つからない場合は <c>null</c>。</returns>
    public async Task<MailTemplate?> FindByNameAsync(string name, CancellationToken ct = default)
        => await context.MailTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Name == name, ct);

    /// <summary>
    /// テンプレート名で有効なテンプレートのみを検索する（読み取り専用）。
    /// </summary>
    /// <param name="name">テンプレート名。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>該当する有効なテンプレート。見つからない場合は <c>null</c>。</returns>
    public async Task<MailTemplate?> FindActiveByNameAsync(string name, CancellationToken ct = default)
        => await context.MailTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Name == name && t.IsActive, ct);

    /// <summary>
    /// 全テンプレートを名前順で取得する（読み取り専用）。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>全テンプレートのリスト。</returns>
    public async Task<List<MailTemplate>> FindAllAsync(CancellationToken ct = default)
        => await context.MailTemplates.AsNoTracking()
            .OrderBy(t => t.Name).ToListAsync(ct);

    /// <summary>
    /// テンプレートをページネーション付きで取得する（読み取り専用）。
    /// </summary>
    /// <param name="page">ページ番号（1 始まり）。</param>
    /// <param name="pageSize">1 ページあたりの件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>指定ページのテンプレートリストと総件数のタプル（名前順）。</returns>
    public async Task<(List<MailTemplate> Items, long TotalCount)> FindAllPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.MailTemplates.AsNoTracking();
        var totalCount = await query.LongCountAsync(ct);
        var items = await query
            .OrderBy(t => t.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, totalCount);
    }

    /// <summary>
    /// テンプレートタイプで有効なテンプレートを検索する（読み取り専用）。
    /// </summary>
    /// <param name="templateType">テンプレートタイプ（TRANSACTIONAL / MARKETING）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>条件に一致するテンプレートリスト（名前順）。</returns>
    public async Task<List<MailTemplate>> FindByTypeAsync(string templateType, CancellationToken ct = default)
        => await context.MailTemplates.AsNoTracking()
            .Where(t => t.TemplateType == templateType && t.IsActive)
            .OrderBy(t => t.Name).ToListAsync(ct);

    /// <summary>
    /// 指定名のテンプレートが既に存在するかを確認する。
    /// </summary>
    /// <param name="name">テンプレート名。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>存在する場合は <c>true</c>。</returns>
    public async Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default)
        => await context.MailTemplates.AnyAsync(t => t.Name == name, ct);

    /// <summary>
    /// メールテンプレートを追加する。
    /// </summary>
    /// <param name="template">追加するテンプレートエンティティ。</param>
    /// <param name="ct">キャンセルトークン。</param>
    public async Task AddAsync(MailTemplate template, CancellationToken ct = default)
        => await context.MailTemplates.AddAsync(template, ct);

    /// <summary>
    /// 保留中の変更をデータベースに保存する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
