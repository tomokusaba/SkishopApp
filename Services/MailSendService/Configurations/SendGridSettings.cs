using System.ComponentModel.DataAnnotations;

namespace MailSendService.Configurations;

/// <summary>
/// SendGrid API の設定。GDPR DSR コンタクト削除に使用する。IOptions<T> でのバインディングに対応するため init プロパティを使用。
/// </summary>
public record SendGridSettings
{
    /// <summary>SendGrid API キー（環境変数または user-secrets で管理）。</summary>
    [Required(ErrorMessage = "SendGrid ApiKey は必須です")]
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>SendGrid API のベース URL。</summary>
    [Required(ErrorMessage = "SendGrid BaseUrl は必須です")]
    [Url(ErrorMessage = "SendGrid BaseUrl は有効な URL である必要があります")]
    public string BaseUrl { get; init; } = "https://api.sendgrid.com";
}
