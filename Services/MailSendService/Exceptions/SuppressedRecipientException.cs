namespace MailSendService.Exceptions;

/// <summary>
/// 配信停止リストに登録済みのメールアドレスへ送信しようとした場合にスローされる例外（HTTP 422）。
/// </summary>
public class SuppressedRecipientException()
    : MailServiceException("配信停止済みのアドレスです");
