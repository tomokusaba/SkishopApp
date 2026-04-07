namespace MailSendService.Exceptions;

/// <summary>
/// 楽観的ロック競合（DbUpdateConcurrencyException）発生時にスローされる例外。HTTP 409 にマッピングされる。
/// </summary>
public class ConcurrencyException(string message, Exception? innerException = null)
    : MailServiceException(message, innerException);
