using MailSendService.Models;

namespace MailSendService.Repositories.Interfaces;

/// <summary>
/// メール送信抑制（サプレッション）リストの永続化および検索を提供するリポジトリインターフェース。
/// </summary>
public interface IMailSuppressionRepository
{
    /// <summary>
    /// 指定されたメールアドレスが送信抑制対象かどうかを判定する。
    /// </summary>
    /// <param name="email">判定対象のメールアドレス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>送信抑制対象の場合は <c>true</c>。</returns>
    Task<bool> IsSuppressedAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// 指定されたメールアドレスの抑制レコードを取得する。
    /// </summary>
    /// <param name="email">検索対象のメールアドレス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>抑制レコード。存在しない場合は <c>null</c>。</returns>
    Task<MailSuppression?> FindByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// 指定されたメールアドレスと抑制理由に一致するレコードを取得する。
    /// </summary>
    /// <param name="email">検索対象のメールアドレス。</param>
    /// <param name="reason">抑制理由（例: bounce, complaint, consent_revoked）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>抑制レコード。存在しない場合は <c>null</c>。</returns>
    Task<MailSuppression?> FindByEmailAndReasonAsync(string email, string reason, CancellationToken ct = default);

    /// <summary>
    /// 指定されたメールアドレスに紐づく全ての抑制レコードを取得する。
    /// </summary>
    /// <param name="email">検索対象のメールアドレス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>抑制レコードの一覧。</returns>
    Task<List<MailSuppression>> FindByUserEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// 抑制レコードの一覧をページネーション付きで取得する。
    /// </summary>
    /// <param name="page">取得するページ番号（1 始まり）。</param>
    /// <param name="pageSize">1 ページあたりの件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>抑制レコードの一覧。</returns>
    Task<List<MailSuppression>> FindAllAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 新しい抑制レコードを追加する。
    /// </summary>
    /// <param name="suppression">追加する抑制エンティティ。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    Task AddAsync(MailSuppression suppression, CancellationToken ct = default);

    /// <summary>
    /// 指定された抑制レコードを削除する。
    /// </summary>
    /// <param name="suppression">削除対象の抑制エンティティ。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    Task RemoveAsync(MailSuppression suppression, CancellationToken ct = default);

    /// <summary>
    /// 変更をデータベースに永続化する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    Task SaveChangesAsync(CancellationToken ct = default);
}
