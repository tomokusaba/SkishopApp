using MailSendService.Models;

namespace MailSendService.Repositories.Interfaces;

/// <summary>
/// メールテンプレートの永続化および検索を提供するリポジトリインターフェース。
/// </summary>
public interface IMailTemplateRepository
{
    /// <summary>
    /// 指定された ID のテンプレートを取得する。
    /// </summary>
    /// <param name="id">テンプレートの一意識別子。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>テンプレート。存在しない場合は <c>null</c>。</returns>
    Task<MailTemplate?> FindByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// 指定された名前のテンプレートを取得する（有効・無効を問わない）。
    /// </summary>
    /// <param name="name">テンプレート名。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>テンプレート。存在しない場合は <c>null</c>。</returns>
    Task<MailTemplate?> FindByNameAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// 指定された名前の有効なテンプレートを取得する。
    /// </summary>
    /// <param name="name">テンプレート名。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>有効なテンプレート。存在しない場合は <c>null</c>。</returns>
    Task<MailTemplate?> FindActiveByNameAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// 全てのテンプレートを取得する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>テンプレートの一覧。</returns>
    Task<List<MailTemplate>> FindAllAsync(CancellationToken ct = default);

    /// <summary>
    /// テンプレートの一覧をページネーション付きで取得する。
    /// </summary>
    /// <param name="page">取得するページ番号（1 始まり）。</param>
    /// <param name="pageSize">1 ページあたりの件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>テンプレートの一覧と総件数のタプル。</returns>
    Task<(List<MailTemplate> Items, long TotalCount)> FindAllPagedAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 指定された種別のテンプレートを取得する。
    /// </summary>
    /// <param name="templateType">テンプレート種別（例: transactional, marketing）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>条件に一致するテンプレートの一覧。</returns>
    Task<List<MailTemplate>> FindByTypeAsync(string templateType, CancellationToken ct = default);

    /// <summary>
    /// 指定された名前のテンプレートが存在するかどうかを判定する。
    /// </summary>
    /// <param name="name">テンプレート名。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>存在する場合は <c>true</c>。</returns>
    Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// 新しいテンプレートを追加する。
    /// </summary>
    /// <param name="template">追加するテンプレートエンティティ。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    Task AddAsync(MailTemplate template, CancellationToken ct = default);

    /// <summary>
    /// 変更をデータベースに永続化する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    Task SaveChangesAsync(CancellationToken ct = default);
}
