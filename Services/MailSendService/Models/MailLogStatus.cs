namespace MailSendService.Models;

/// <summary>
/// メール送信ログのステータスを表す定数クラス。
/// </summary>
public static class MailLogStatus
{
    /// <summary>送信待ち状態。</summary>
    public const string Pending = "PENDING";

    /// <summary>送信処理中。</summary>
    public const string Sending = "SENDING";

    /// <summary>送信完了。</summary>
    public const string Sent = "SENT";

    /// <summary>送信失敗。</summary>
    public const string Failed = "FAILED";

    /// <summary>バウンス（配信不能で返送された状態）。</summary>
    public const string Bounced = "BOUNCED";

    /// <summary>配信停止等の理由でスキップされた状態。</summary>
    public const string Skipped = "SKIPPED";
}
