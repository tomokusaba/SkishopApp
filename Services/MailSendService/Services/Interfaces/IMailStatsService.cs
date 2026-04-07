using MailSendService.DTOs.Responses;

namespace MailSendService.Services.Interfaces;

/// <summary>
/// メール送信統計情報を提供するサービスインターフェース。
/// </summary>
/// <remarks>
/// P2-2: MailService からの責務分割。統計クエリの最適化とキャッシュ機能を提供する。
/// </remarks>
public interface IMailStatsService
{
    /// <summary>
    /// メール送信の統計情報を取得する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>送信件数・成功率等の統計情報。</returns>
    /// <remarks>
    /// Redis キャッシュ（5 分 TTL）を使用して DB 負荷を軽減する。
    /// </remarks>
    Task<MailStatsResponse> GetStatsAsync(CancellationToken ct = default);

    /// <summary>
    /// 統計情報のキャッシュを無効化する。
    /// </summary>
    /// <param name="ct">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    /// <remarks>
    /// メール送信完了時に呼び出し、次回取得時に最新データを反映する。
    /// </remarks>
    Task InvalidateStatsCacheAsync(CancellationToken ct = default);
}
