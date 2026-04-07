using System.ComponentModel.DataAnnotations;

namespace MailSendService.Configurations;

/// <summary>
/// Azure Communication Services のメール送信設定。IOptions<T> でのバインディングに対応するため init プロパティを使用。
/// </summary>
public record AzureEmailSettings
{
    /// <summary>Azure Communication Services のエンドポイント URL。</summary>
    [Required(ErrorMessage = "Azure Communication Endpoint は必須です")]
    [Url(ErrorMessage = "Azure Communication Endpoint は有効な URL である必要があります")]
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>送信元メールアドレス。</summary>
    [Required(ErrorMessage = "Azure Communication SenderAddress は必須です")]
    [EmailAddress(ErrorMessage = "有効なメールアドレスを入力してください")]
    public string SenderAddress { get; init; } = string.Empty;
}
