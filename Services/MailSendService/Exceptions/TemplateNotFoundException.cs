namespace MailSendService.Exceptions;

/// <summary>
/// 指定されたテンプレートが存在しない場合にスローされる例外（HTTP 404）。
/// </summary>
public class TemplateNotFoundException(string templateName)
    : MailServiceException($"テンプレートが見つかりません: {templateName}");
