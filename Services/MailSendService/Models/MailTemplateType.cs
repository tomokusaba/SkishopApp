namespace MailSendService.Models;

/// <summary>
/// メールテンプレートの種別を表す定数クラス。
/// </summary>
public static class MailTemplateType
{
    /// <summary>トランザクションメール（注文確認・パスワードリセット等）。</summary>
    public const string Transactional = "TRANSACTIONAL";

    /// <summary>マーケティングメール（プロモーション・ニュースレター等）。</summary>
    public const string Marketing = "MARKETING";
}
