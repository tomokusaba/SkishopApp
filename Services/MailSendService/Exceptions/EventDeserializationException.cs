namespace MailSendService.Exceptions;

/// <summary>
/// Kafka イベントのデシリアライズに失敗した場合にスローされる内部例外。
/// </summary>
public class EventDeserializationException(string message, Exception? innerException = null)
    : MailServiceException(message, innerException);
