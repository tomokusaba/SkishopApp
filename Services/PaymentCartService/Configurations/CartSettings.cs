using System.ComponentModel.DataAnnotations;

namespace PaymentCartService.Configurations;

public record CartSettings
{
    [Range(1, 365, ErrorMessage = "ExpiryDays は 1〜365 の範囲で指定してください")]
    public int ExpiryDays { get; init; } = 7;

    [Range(1, 100, ErrorMessage = "MaxItemsPerCart は 1〜100 の範囲で指定してください")]
    public int MaxItemsPerCart { get; init; } = 50;

    [Range(1, 99, ErrorMessage = "MaxQuantityPerItem は 1〜99 の範囲で指定してください")]
    public int MaxQuantityPerItem { get; init; } = 10;

    [Range(1, 1440, ErrorMessage = "CleanupIntervalMinutes は 1〜1440 の範囲で指定してください")]
    public int CleanupIntervalMinutes { get; init; } = 60;

    [Range(1, 10000, ErrorMessage = "CleanupBatchSize は 1〜10000 の範囲で指定してください")]
    public int CleanupBatchSize { get; init; } = 500;

    [Range(1, 1440, ErrorMessage = "ExpirationCheckIntervalMinutes は 1〜1440 の範囲で指定してください")]
    public int ExpirationCheckIntervalMinutes { get; init; } = 10;

    [Range(1, 10000, ErrorMessage = "ExpirationBatchSize は 1〜10000 の範囲で指定してください")]
    public int ExpirationBatchSize { get; init; } = 200;
}
