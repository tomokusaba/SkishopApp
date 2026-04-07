namespace AuthService.Configurations;

/// <summary>
/// JWT 認証トークン設定。
/// アクセストークンとリフレッシュトークンの生成・検証に使用するパラメータを定義する。
/// appsettings.json の "Jwt" セクションにバインドされる。
/// IOptions&lt;T&gt; でのバインディングに対応するため、init プロパティを使用。
/// </summary>
/// <remarks>
/// <para>セキュリティ要件:</para>
/// <list type="bullet">
///   <item><description>SecretKey は最低32文字以上の強力な文字列を使用すること</description></item>
///   <item><description>本番環境では SecretKey を環境変数または Azure Key Vault で管理すること</description></item>
///   <item><description>Issuer と Audience は本番 URL と一致させること</description></item>
/// </list>
/// </remarks>
public record JwtSettings
{
    /// <summary>トークン発行者（Issuer）。トークン検証時に確認される。</summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>トークン対象者（Audience）。トークン検証時に確認される。</summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>トークン署名・検証用の秘密鍵（HMAC SHA256）。最低32文字以上必須。</summary>
    public string SecretKey { get; init; } = string.Empty;

    /// <summary>アクセストークンの有効期限（秒）。デフォルト: 3600秒（1時間）。</summary>
    public int AccessExpirationSeconds { get; init; } = 3600;

    /// <summary>リフレッシュトークンの有効期限（秒）。デフォルト: 604800秒（7日間）。</summary>
    public int RefreshExpirationSeconds { get; init; } = 604800;

    /// <summary>ユーザーあたりの最大有効リフレッシュトークン数。超過時は古いものから無効化。デフォルト: 10。</summary>
    public int MaxActiveRefreshTokens { get; init; } = 10;
}
