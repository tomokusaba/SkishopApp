namespace MailSendService.Exceptions;

/// <summary>
/// メールサービスの例外階層の抽象基底クラス。
/// </summary>
/// <remarks>
/// すべてのメールサービス固有の例外はこのクラスを継承し、グローバル例外ハンドラーで統一的に処理される。
/// </remarks>
public abstract class MailServiceException(string message, Exception? innerException = null)
    : Exception(message, innerException);
