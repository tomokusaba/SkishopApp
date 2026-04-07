namespace MailSendService.Exceptions;

/// <summary>
/// メール送信のレート制限を超過した場合にスローされる例外（HTTP 429）。
/// </summary>
public class RateLimitExceededException()
    : MailServiceException("送信頻度制限を超過しました");
