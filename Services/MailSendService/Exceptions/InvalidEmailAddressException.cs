namespace MailSendService.Exceptions;

/// <summary>
/// 無効なメールアドレス形式が検出された場合にスローされる例外（HTTP 422）。
/// </summary>
public class InvalidEmailAddressException(string message)
    : MailServiceException(message);
