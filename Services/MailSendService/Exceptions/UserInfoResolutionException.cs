namespace MailSendService.Exceptions;

/// <summary>
/// ユーザー管理サービスからのユーザー情報取得に失敗した場合にスローされる例外（HTTP 502）。
/// </summary>
public class UserInfoResolutionException(string message, Exception? innerException = null)
    : MailServiceException(message, innerException);
