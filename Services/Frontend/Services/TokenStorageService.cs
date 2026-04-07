using Frontend.Services.Interfaces;

namespace Frontend.Services;

/// <summary>
/// サーバーサイド トークン管理サービス
/// §16.2 準拠 — httpOnly Cookie による JWT 管理（BFF パターン）
/// </summary>
public class TokenStorageService(
    IHttpContextAccessor httpContextAccessor,
    ILogger<TokenStorageService> logger) : ITokenStorageService
{
    private const string AccessTokenCookieName = "access_token";
    private const string RefreshTokenCookieName = "refresh_token";

    public void StoreTokens(string accessToken, string refreshToken, int expiresInSeconds)
    {
        var context = httpContextAccessor.HttpContext;
        if (context is null)
        {
            logger.LogWarning("HttpContext is null — トークン保存をスキップ");
            return;
        }

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds)
        };

        context.Response.Cookies.Append(AccessTokenCookieName, accessToken, cookieOptions);

        var refreshCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        };

        context.Response.Cookies.Append(RefreshTokenCookieName, refreshToken, refreshCookieOptions);
    }

    public void ClearTokens()
    {
        var context = httpContextAccessor.HttpContext;
        if (context is null) return;

        context.Response.Cookies.Delete(AccessTokenCookieName, new CookieOptions { Path = "/" });
        context.Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions { Path = "/" });
    }

    public string? GetAccessToken()
        => httpContextAccessor.HttpContext?.Request.Cookies[AccessTokenCookieName];

    public string? GetRefreshToken()
        => httpContextAccessor.HttpContext?.Request.Cookies[RefreshTokenCookieName];
}
