namespace AuthService.DTOs.Responses;

/// <summary>
/// OAuth 2.0 Client Credentials グラントレスポンス DTO。
/// マシン間（M2M）認証成功時に返されるアクセストークン情報。
/// POST /auth/oauth/token エンドポイントのレスポンスとして使用される。
/// </summary>
/// <param name="AccessToken">発行された JWT アクセストークン。Authorization ヘッダーで "Bearer {token}" 形式で使用。</param>
/// <param name="TokenType">トークンタイプ。常に "Bearer" を返す。</param>
/// <param name="ExpiresIn">アクセストークンの有効期限（秒）。デフォルトは3600秒（1時間）。</param>
/// <param name="Scope">許可されたスコープ。リクエストされたスコープのうち、許可されたもののみ返される。</param>
public record ClientCredentialsResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string Scope);
