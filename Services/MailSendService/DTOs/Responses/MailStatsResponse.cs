namespace MailSendService.DTOs.Responses;

/// <summary>
/// メール送信統計情報の API レスポンス DTO。
/// </summary>
/// <param name="TotalSent">送信成功の総件数。</param>
/// <param name="TotalFailed">送信失敗の総件数。</param>
/// <param name="TotalPending">送信待ちの総件数。</param>
/// <param name="SuccessRate">送信成功率（0.0〜100.0（パーセント））。</param>
/// <param name="SentByTemplate">テンプレート名ごとの送信件数。</param>
public record MailStatsResponse(
    long TotalSent,
    long TotalFailed,
    long TotalPending,
    double SuccessRate,
    Dictionary<string, long> SentByTemplate);
