namespace SalesManagementService.Configurations;

/// <summary>
/// 注文設定。IOptions<T> でのバインディングに対応するため init プロパティを使用。
/// </summary>
public record OrderSettings
{
    public int ExpiryHours { get; init; } = 24;
    public bool AutoCancelEnabled { get; init; } = true;
}

/// <summary>
/// 配送料設定。IOptions<T> でのバインディングに対応するため init プロパティを使用。
/// </summary>
public record ShippingSettings
{
    public decimal FreeShippingThreshold { get; init; } = 10000;
    public decimal DefaultShippingFee { get; init; } = 550;
    public decimal HokkaidoOkinawaFee { get; init; } = 1100;
    public decimal ExpressSurcharge { get; init; } = 330;
    public decimal LargeItemSurcharge { get; init; } = 1650;
    public Dictionary<string, decimal>? MemberRankDiscounts { get; init; } = null;
}

/// <summary>
/// 返品設定。IOptions<T> でのバインディングに対応するため init プロパティを使用。
/// </summary>
public record ReturnSettings
{
    public int AllowedDays { get; init; } = 30;
    public decimal AutoApprovalThreshold { get; init; } = 5000;
}

/// <summary>
/// Saga 設定。IOptions<T> でのバインディングに対応するため init プロパティを使用。
/// </summary>
public record SagaSettings
{
    public int SloDeadlineMs { get; init; } = 1000;
    public int CompensationTimeoutSeconds { get; init; } = 30;
    public int RecoveryPollingIntervalSeconds { get; init; } = 30;
    public int StallThresholdMinutes { get; init; } = 5;
    public int PaymentPollingTimeoutMinutes { get; init; } = 30;
}

/// <summary>
/// Outbox 設定。IOptions<T> でのバインディングに対応するため init プロパティを使用。
/// </summary>
public record OutboxSettings
{
    public int MinPollingIntervalMs { get; init; } = 100;
    public int MaxPollingIntervalMs { get; init; } = 5000;
    public int BatchSize { get; init; } = 50;
    public int MaxRetries { get; init; } = 5;
}
