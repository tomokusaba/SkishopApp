using MailSendService.DTOs;

namespace MailSendService.Services.Interfaces;

/// <summary>
/// メールイベントのペイロードからメール送信に必要なデータを解決するリゾルバー。
/// </summary>
public interface IMailEventResolver
{
    /// <summary>
    /// イベント種別とペイロードから送信データを解決する。
    /// </summary>
    /// <param name="eventType">イベント種別（例: "user.registered", "order.created"）。</param>
    /// <param name="payload">イベントの JSON ペイロード文字列。</param>
    /// <param name="correlationId">分散トレーシング用の相関 ID。</param>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>解決されたメール送信データ。</returns>
    /// <exception cref="Exceptions.EventDeserializationException">サポートされていないイベント種別またはペイロード解析失敗。</exception>
    /// <exception cref="Exceptions.UserInfoResolutionException">ユーザー情報の解決に失敗した場合。</exception>
    Task<ResolvedEventData> ResolveEventDataAsync(
        string eventType,
        string payload,
        string correlationId,
        CancellationToken ct = default);
}
