namespace MailSendService.Exceptions;

/// <summary>
/// 指定されたメールログが存在しない場合にスローされる例外（HTTP 404）。
/// </summary>
public class MailLogNotFoundException(string id)
    : MailServiceException($"メールログが見つかりません: {id}");
