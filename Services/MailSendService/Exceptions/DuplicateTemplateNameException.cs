namespace MailSendService.Exceptions;

/// <summary>
/// 同名のテンプレートが既に存在する場合にスローされる例外（HTTP 409）。
/// </summary>
public class DuplicateTemplateNameException(string name)
    : MailServiceException($"テンプレート名が既に使用されています: {name}");
