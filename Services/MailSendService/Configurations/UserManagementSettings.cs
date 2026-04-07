using System.ComponentModel.DataAnnotations;

namespace MailSendService.Configurations;

/// <summary>
/// ユーザー管理サービスの接続設定。IOptions<T> でのバインディングに対応するため init プロパティを使用。
/// </summary>
public record UserManagementSettings
{
    /// <summary>ユーザー管理サービスのベース URL。</summary>
    [Required(ErrorMessage = "UserManagement URL は必須です")]
    [Url(ErrorMessage = "UserManagement URL は有効な URL である必要があります")]
    public string Url { get; init; } = "http://localhost:5002";

    /// <summary>サービス間認証用の API キー（環境変数または user-secrets で管理）。</summary>
    [Required(ErrorMessage = "UserManagement ApiKey は必須です（サービス間認証に使用）")]
    public string ApiKey { get; init; } = string.Empty;
}
