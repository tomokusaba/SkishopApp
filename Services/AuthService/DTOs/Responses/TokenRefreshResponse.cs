namespace AuthService.DTOs.Responses;

/// <summary>
/// トークンリフレッシュレスポンス DTO。
/// リフレッシュトークンを使用してアクセストークンを更新した際に返される新しいトークン情報。
/// POST /auth/token/refresh エンドポイントのレスポンスとして使用される。
/// </summary>
/// <remarks>
/// リフレッシュトークンローテーション方式を採用しているため、
/// 古いリフレッシュトークンは無効化され、新しいリフレッシュトークンが発行される。
/// </remarks>
/// <param name="AccessToken">新しく発行された JWT アクセストークン。Authorization ヘッダーで使用。</param>
/// <param name="RefreshToken">新しく発行されたリフレッシュトークン。次回のトークン更新に使用。</param>
/// <param name="ExpiresIn">アクセストークンの有効期限（秒）。</param>
public record TokenRefreshResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn);
