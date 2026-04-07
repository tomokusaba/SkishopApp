using System.ComponentModel.DataAnnotations;

namespace PaymentCartService.Configurations;

public record PaymentSettings
{
    [Required(ErrorMessage = "DefaultCurrency は必須です")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "DefaultCurrency は 3 文字の ISO 4217 コードを指定してください")]
    public string DefaultCurrency { get; init; } = "JPY";

    [Range(1, 10, ErrorMessage = "MaxRetries は 1〜10 の範囲で指定してください")]
    public int MaxRetries { get; init; } = 3;

    [Range(60, 600, ErrorMessage = "WebhookToleranceSeconds は 60〜600 の範囲で指定してください")]
    public long WebhookToleranceSeconds { get; init; } = 300;
}
