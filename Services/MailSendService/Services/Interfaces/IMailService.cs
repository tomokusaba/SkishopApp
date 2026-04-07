using MailSendService.DTOs.Requests;
using MailSendService.DTOs.Responses;

namespace MailSendService.Services.Interfaces;

/// <summary>
/// メール送信・ログ管理・GDPR 対応を統括するサービスインターフェース。
/// </summary>
public interface IMailService
{
    /// <summary>
    /// Kafka 等から受信したイベントを処理し、対応するメールを送信する。
    /// </summary>
    /// <param name="eventType">イベント種別（例: order.created）。</param>
    /// <param name="eventId">イベントの一意識別子。</param>
    /// <param name="correlationId">分散トレーシング用の相関 ID。</param>
    /// <param name="payload">イベントペイロード（JSON 文字列）。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    Task ProcessEventAsync(string eventType, string eventId, string correlationId,
        string payload, CancellationToken ct = default);

    /// <summary>
    /// テストメールを送信し、送信結果のログを返す。
    /// </summary>
    /// <param name="request">テストメール送信リクエスト。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>送信結果のメールログ。</returns>
    Task<MailLogResponse> SendTestMailAsync(TestMailRequest request, CancellationToken ct = default);

    /// <summary>
    /// 送信失敗したメールを再送する。
    /// </summary>
    /// <param name="mailLogId">再送対象のメールログ ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>再送結果のメールログ。</returns>
    /// <exception cref="NotFoundException">指定されたメールログが存在しない場合。</exception>
    Task<MailLogResponse> RetryAsync(string mailLogId, CancellationToken ct = default);

    /// <summary>
    /// メール送信ログをページネーション付きで取得する。
    /// </summary>
    /// <param name="page">取得するページ番号（1 始まり）。</param>
    /// <param name="pageSize">1 ページあたりの件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>ページネーション付きのメールログ一覧。</returns>
    Task<PaginatedResult<MailLogResponse>> GetLogsAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 指定された ID のメール送信ログを取得する。
    /// </summary>
    /// <param name="id">メールログの一意識別子。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>メールログの詳細。</returns>
    /// <exception cref="NotFoundException">指定されたメールログが存在しない場合。</exception>
    Task<MailLogResponse> GetLogByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// メール送信の統計情報を取得する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>送信件数・成功率等の統計情報。</returns>
    Task<MailStatsResponse> GetStatsAsync(CancellationToken ct = default);

    /// <summary>
    /// GDPR 同意撤回イベントを処理し、該当ユーザーへのメール送信を抑制する。
    /// </summary>
    /// <param name="userId">同意を撤回したユーザーの ID。</param>
    /// <param name="consentType">撤回された同意の種別。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    Task ProcessConsentRevokedAsync(string userId, string consentType, CancellationToken ct = default);

    /// <summary>
    /// GDPR ユーザー削除イベントを処理し、関連する個人情報を匿名化する。
    /// </summary>
    /// <param name="userId">削除対象のユーザー ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    Task ProcessUserDeletedAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// GDPR データ処理制限イベントを処理し、該当ユーザーへのメール送信を一時停止する。
    /// </summary>
    /// <param name="userId">処理を制限するユーザーの ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    Task ProcessUserProcessingRestrictedAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// GDPR データ処理制限の解除イベントを処理し、該当ユーザーへのメール送信を再開する。
    /// </summary>
    /// <param name="userId">処理制限を解除するユーザーの ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    Task ProcessUserProcessingUnrestrictedAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// リトライ対象の失敗メールログを取得する。
    /// </summary>
    /// <param name="maxCount">取得する最大件数。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>失敗メールログのレスポンスリスト。</returns>
    Task<IReadOnlyList<MailLogResponse>> GetFailedMailsForRetryAsync(int maxCount, CancellationToken ct = default);
}
