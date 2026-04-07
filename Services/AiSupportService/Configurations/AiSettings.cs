using System.ComponentModel.DataAnnotations;

namespace AiSupportService.Configurations;

/// <summary>
/// Azure OpenAI および Semantic Kernel の接続設定。
/// appsettings.json の "AzureOpenAI" セクションに対応する。
/// </summary>
public record AzureOpenAISettings
{
    /// <summary>Azure OpenAI リソースのエンドポイント URL。</summary>
    [Required, Url]
    public string Endpoint { get; init; } = string.Empty;
    /// <summary>チャット補完に使用するデプロイメント名。</summary>
    [Required]
    public string DeploymentName { get; init; } = string.Empty;
    /// <summary>埋め込みモデルに使用するデプロイメント名。</summary>
    [Required]
    public string EmbeddingDeploymentName { get; init; } = string.Empty;
    /// <summary>API キー（省略時は DefaultAzureCredential を使用）。</summary>
    public string? ApiKey { get; init; }
}

/// <summary>
/// Azure AI Search の接続設定。
/// appsettings.json の "AzureAISearch" セクションに対応する。
/// </summary>
public record AzureAISearchSettings
{
    /// <summary>Azure AI Search のエンドポイント URL。</summary>
    [Required, Url]
    public string Endpoint { get; init; } = string.Empty;
    /// <summary>使用するインデックス名。</summary>
    [Required]
    public string IndexName { get; init; } = string.Empty;
    /// <summary>API キー（省略時は DefaultAzureCredential を使用）。</summary>
    public string? ApiKey { get; init; }
}

/// <summary>
/// AI チャット機能の制限・パラメータ設定。
/// appsettings.json の "AiChat" セクションに対応する。
/// </summary>
public record AiChatSettings
{
    /// <summary>ユーザーあたりの最大セッション数。</summary>
    public int MaxSessionsPerUser { get; init; } = 10;
    /// <summary>セッションあたりの最大メッセージ数。</summary>
    public int MaxMessagesPerSession { get; init; } = 100;
    /// <summary>入力メッセージの最大文字数。</summary>
    public int MaxInputLength { get; init; } = 4000;
    /// <summary>AI 応答の最大出力トークン数。</summary>
    public int MaxOutputTokens { get; init; } = 2048;
    /// <summary>生成時の Temperature パラメータ（0.0〜1.0）。</summary>
    public double Temperature { get; init; } = 0.7;
}

/// <summary>
/// Apache Kafka の接続設定。
/// appsettings.json の "Kafka" セクションに対応する。
/// </summary>
public record KafkaSettings
{
    /// <summary>Kafka ブローカーのアドレス。</summary>
    [Required]
    public string BootstrapServers { get; init; } = string.Empty;
    /// <summary>コンシューマーグループ ID。</summary>
    [Required]
    public string GroupId { get; init; } = "ai-support-service";
}

/// <summary>
/// 連携先マイクロサービスのエンドポイント設定。
/// appsettings.json の "Services" セクションに対応する。
/// </summary>
public record ServiceEndpointSettings
{
    /// <summary>在庫管理サービスのベース URL。</summary>
    [Required, Url]
    public string InventoryManagementService { get; init; } = string.Empty;
    /// <summary>販売管理サービスのベース URL。</summary>
    [Required, Url]
    public string SalesManagementService { get; init; } = string.Empty;
}

/// <summary>
/// JWT 認証トークンの検証設定。
/// appsettings.json の "Jwt" セクションに対応する。
/// </summary>
public record JwtSettings
{
    /// <summary>トークン発行者（Issuer）。</summary>
    [Required]
    public string Issuer { get; init; } = string.Empty;
    /// <summary>トークン対象者（Audience）。</summary>
    [Required]
    public string Audience { get; init; } = string.Empty;
    /// <summary>署名検証用の秘密鍵（最低 32 文字）。</summary>
    [Required, MinLength(32)]
    public string SecretKey { get; init; } = string.Empty;
    /// <summary>トークン有効期限のクロックスキュー許容時間（分）。</summary>
    public int ClockSkewMinutes { get; init; } = 5;
}
