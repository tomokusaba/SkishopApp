using System.ComponentModel.DataAnnotations;

namespace PaymentCartService.Configurations;

public record StripeSettings
{
    [Required(ErrorMessage = "SecretKey は必須です（環境変数で設定）")]
    public string SecretKey { get; init; } = string.Empty;

    [Required(ErrorMessage = "WebhookSecret は必須です（環境変数で設定）")]
    public string WebhookSecret { get; init; } = string.Empty;

    [Required(ErrorMessage = "SuccessUrl は必須です")]
    [Url(ErrorMessage = "SuccessUrl は有効な URL を指定してください")]
    public string SuccessUrl { get; init; } = string.Empty;

    [Required(ErrorMessage = "CancelUrl は必須です")]
    [Url(ErrorMessage = "CancelUrl は有効な URL を指定してください")]
    public string CancelUrl { get; init; } = string.Empty;
}
