namespace Frontend.Services.Interfaces;

/// <summary>
/// サーバーサイド トークン管理サービスインターフェース
/// §16.2 準拠 — httpOnly Cookie による JWT 管理（BFF パターン）
/// </summary>
public interface ITokenStorageService
{
    void StoreTokens(string accessToken, string refreshToken, int expiresInSeconds);
    void ClearTokens();
    string? GetAccessToken();
    string? GetRefreshToken();
}
