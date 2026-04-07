namespace MailSendService.Configurations;

/// <summary>
/// メール送信リトライの設定。IOptions<T> でのバインディングに対応するため init プロパティを使用。
/// </summary>
public record RetrySettings
{
    /// <summary>最大リトライ回数。</summary>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>初回リトライまでの待機時間（ミリ秒）。</summary>
    public int InitialIntervalMs { get; init; } = 30000;

    /// <summary>リトライ間隔の乗数（指数バックオフ用）。</summary>
    public double Multiplier { get; init; } = 2.0;
}

/// <summary>
/// メールサービスの全般設定。IOptions<T> でのバインディングに対応するため init プロパティを使用。
/// </summary>
public record MailSettings
{
    /// <summary>リトライに関する設定。</summary>
    public RetrySettings Retry { get; init; } = new();

    /// <summary>メールサービスのベース URL。</summary>
    public string BaseUrl { get; init; } = string.Empty;
}
