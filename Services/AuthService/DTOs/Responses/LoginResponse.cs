namespace AuthService.DTOs.Responses;

/// <summary>
/// ユーザーログイン成功レスポンス DTO。
/// ログイン認証成功時に返されるトークン情報とユーザー基本情報。
/// POST /auth/login エンドポイントのレスポンスとして使用される。
/// </summary>
/// <param name="AccessToken">発行された JWT アクセストークン。Authorization ヘッダーで "Bearer {token}" 形式で使用。</param>
/// <param name="RefreshToken">リフレッシュトークン。アクセストークンの更新に使用。安全に保存すること。</param>
/// <param name="TokenType">トークンタイプ。常に "Bearer" を返す。</param>
/// <param name="ExpiresIn">アクセストークンの有効期限（秒）。デフォルトは3600秒（1時間）。</param>
/// <param name="User">ログインしたユーザーの基本情報。</param>
public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    UserDto User);
