namespace MailSendService.Models;

/// <summary>
/// メール配信停止の理由を表す定数クラス。
/// </summary>
public static class SuppressionReason
{
    /// <summary>ユーザーによる配信停止リクエスト。</summary>
    public const string Unsubscribe = "UNSUBSCRIBE";

    /// <summary>メールアドレスへの配信不能（バウンス）。</summary>
    public const string Bounce = "BOUNCE";

    /// <summary>受信者からの苦情報告。</summary>
    public const string Complaint = "COMPLAINT";

    /// <summary>GDPR 第 18 条に基づくデータ処理制限。</summary>
    public const string ProcessingRestricted = "PROCESSING_RESTRICTED";
}
