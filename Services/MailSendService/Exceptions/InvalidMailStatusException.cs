namespace MailSendService.Exceptions;

/// <summary>
/// メールログのステータスが期待値と一致しない場合にスローされる例外（HTTP 422）。
/// </summary>
public class InvalidMailStatusException(string currentStatus, string expectedStatus)
    : MailServiceException($"メールステータスが不正です。現在: {currentStatus}, 期待: {expectedStatus}");
