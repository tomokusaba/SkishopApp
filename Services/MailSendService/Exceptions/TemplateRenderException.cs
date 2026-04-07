namespace MailSendService.Exceptions;

/// <summary>
/// メールテンプレートのレンダリングに失敗した場合にスローされる内部例外。
/// </summary>
public class TemplateRenderException(string templateName, Exception? innerException = null)
    : MailServiceException($"テンプレートレンダリング失敗: {templateName}", innerException);
