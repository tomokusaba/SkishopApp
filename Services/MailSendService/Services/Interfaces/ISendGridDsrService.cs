namespace MailSendService.Services.Interfaces;

/// <summary>
/// SendGrid 外部プロセッサーへの GDPR DSR（Data Subject Request）伝搬サービスインターフェース。
/// </summary>
/// <remarks>
/// spec.md §外部プロセッサーへの DSR 伝搬に基づき、
/// SendGrid API を呼び出してマーケティングコンタクトを削除する。
/// </remarks>
public interface ISendGridDsrService
{
    /// <summary>
    /// SendGrid のマーケティングコンタクトリストから指定メールアドレスのコンタクトを削除する。
    /// </summary>
    /// <param name="email">削除対象のメールアドレス。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    Task DeleteContactAsync(string email, CancellationToken ct = default);
}
