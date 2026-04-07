namespace MailSendService.Exceptions;

/// <summary>
/// 外部メール送信サービスへの送信が失敗した場合にスローされる例外（HTTP 502）。
/// </summary>
public class MailSendFailedException(string message, Exception? innerException = null)
    : MailServiceException(message, innerException);
