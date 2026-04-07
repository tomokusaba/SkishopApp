using MailSendService.Models;

namespace MailSendService.Repositories.Interfaces;

/// <summary>
/// メール送信ログの永続化および検索を提供するリポジトリインターフェース。
/// </summary>
public interface IMailLogRepository
{
    /// <summary>
    /// 指定された ID のメールログを取得する。
    /// </summary>
    /// <param name="id">メールログの一意識別子。</param>
    /// <param name="trackChanges">変更追跡を有効にするかどうか。読み取り専用の場合は false を指定。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>メールログ。存在しない場合は <c>null</c>。</returns>
    Task<MailLog?> FindByIdAsync(string id, bool trackChanges = true, CancellationToken ct = default);

    /// <summary>
    /// 指定されたイベント ID に対応するメールログを取得する（べき等性チェック用）。
    /// </summary>
    /// <param name="eventId">イベントの一意識別子。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>メールログ。存在しない場合は <c>null</c>。</returns>
    Task<MailLog?> FindByEventIdAsync(string eventId, CancellationToken ct = default);

    /// <summary>
    /// 指定されたステータスのメールログを取得する。
    /// </summary>
    /// <param name="status">検索対象のステータス（例: SENT, FAILED）。</param>
    /// <param name="limit">取得する最大件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>条件に一致するメールログの一覧。</returns>
    Task<List<MailLog>> FindByStatusAsync(string status, int limit = 50, CancellationToken ct = default);

    /// <summary>
    /// 指定された受信者メールアドレスのメールログをページネーション付きで取得する。
    /// </summary>
    /// <param name="recipientEmail">受信者のメールアドレス。</param>
    /// <param name="page">取得するページ番号（1 始まり）。</param>
    /// <param name="pageSize">1 ページあたりの件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>条件に一致するメールログの一覧。</returns>
    Task<List<MailLog>> FindByRecipientEmailAsync(string recipientEmail, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 指定された受信者に対して、指定日時以降に送信されたメール件数を取得する（レート制限用）。
    /// </summary>
    /// <param name="recipientEmail">受信者のメールアドレス。</param>
    /// <param name="since">集計開始日時。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>送信件数。</returns>
    Task<int> CountByRecipientSinceAsync(string recipientEmail, DateTimeOffset since, CancellationToken ct = default);

    /// <summary>
    /// 指定されたステータスのメールログ件数を取得する。
    /// </summary>
    /// <param name="status">集計対象のステータス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>件数。</returns>
    Task<long> CountByStatusAsync(string status, CancellationToken ct = default);

    /// <summary>
    /// テンプレート別のメール送信件数を取得する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>テンプレート名をキー、送信件数を値とするディクショナリ。</returns>
    Task<Dictionary<string, long>> CountByTemplateAsync(CancellationToken ct = default);

    /// <summary>
    /// リトライ対象の失敗メールログを取得する。
    /// </summary>
    /// <param name="maxRetryCount">リトライ回数の上限。この値未満のログのみ取得する。</param>
    /// <param name="limit">取得する最大件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>リトライ対象のメールログ一覧。</returns>
    Task<List<MailLog>> FindFailedForRetryAsync(int maxRetryCount, int limit = 20, CancellationToken ct = default);

    /// <summary>
    /// 指定された日時より古いメールログを取得する（データ保持期間管理用）。
    /// </summary>
    /// <param name="cutoff">この日時より古いログを取得する。</param>
    /// <param name="limit">取得する最大件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>条件に一致するメールログの一覧。</returns>
    Task<List<MailLog>> FindOlderThanAsync(DateTimeOffset cutoff, int limit = 100, CancellationToken ct = default);

    /// <summary>
    /// 全メールログをページネーション付きで取得する。
    /// </summary>
    /// <param name="page">取得するページ番号（1 始まり）。</param>
    /// <param name="pageSize">1 ページあたりの件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>メールログの一覧と総件数のタプル。</returns>
    Task<(List<MailLog> Items, long TotalCount)> FindAllPagedAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 指定されたユーザー ID の受信者に紐づくメールログを取得する。
    /// </summary>
    /// <param name="userId">ユーザーの一意識別子。</param>
    /// <param name="limit">取得する最大件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>条件に一致するメールログの一覧。</returns>
    Task<List<MailLog>> FindByRecipientUserIdAsync(string userId, int limit = 1000, CancellationToken ct = default);

    /// <summary>
    /// 指定されたユーザー ID に対応するメールアドレスをログから取得する。
    /// </summary>
    /// <param name="userId">ユーザーの一意識別子。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>メールアドレス。該当するログが存在しない場合は <c>null</c>。</returns>
    Task<string?> FindEmailByUserIdAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// 指定されたユーザー ID の送信待ち（PENDING）メールログを取得する。
    /// </summary>
    /// <param name="userId">ユーザーの一意識別子。</param>
    /// <param name="limit">取得する最大件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>送信待ちのメールログ一覧。</returns>
    Task<List<MailLog>> FindPendingByUserIdAsync(string userId, int limit = 100, CancellationToken ct = default);

    /// <summary>
    /// 一定時間以上 SENDING ステータスのままの孤児化メールログを取得する。
    /// </summary>
    /// <param name="cutoff">この日時より前に SENDING になったレコードを対象とする。</param>
    /// <param name="limit">取得する最大件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>孤児化したメールログの一覧。</returns>
    Task<List<MailLog>> FindOrphanedSendingAsync(DateTimeOffset cutoff, int limit = 50, CancellationToken ct = default);

    /// <summary>
    /// 新しいメールログを追加する。
    /// </summary>
    /// <param name="mailLog">追加するメールログエンティティ。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    Task AddAsync(MailLog mailLog, CancellationToken ct = default);

    /// <summary>
    /// 変更をデータベースに永続化する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    Task SaveChangesAsync(CancellationToken ct = default);
}
