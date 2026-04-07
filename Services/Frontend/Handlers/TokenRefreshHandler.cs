using System.Net.Http.Headers;

namespace Frontend.Handlers;

/// <summary>
/// BFF パターン: サーバーサイドでの自動トークンリフレッシュ
/// HttpClient の DelegatingHandler として実装
/// §16.2 準拠 — 401 受信時に 1 回のみリフレッシュ試行（SemaphoreSlim で並行制御）
/// H-06: static SemaphoreSlim で Transient スコープでも単一ロック
/// H-05/H-17: リフレッシュ成功後の新トークン Cookie 保存
/// </summary>
public class TokenRefreshHandler(
    IHttpContextAccessor httpContextAccessor,
    ILogger<TokenRefreshHandler> logger) : DelegatingHandler
{
    private static readonly SemaphoreSlim _refreshLock = new(1, 1);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        AttachAccessToken(request);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            if (!await _refreshLock.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken))
            {
                logger.LogWarning("トークンリフレッシュのロック取得タイムアウト");
                return response;
            }
            try
            {
                var refreshToken = httpContextAccessor.HttpContext?.Request.Cookies["refresh_token"];
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
                    refreshRequest.Content = JsonContent.Create(new { RefreshToken = refreshToken });

                    var refreshResponse = await base.SendAsync(refreshRequest, cancellationToken);
                    if (refreshResponse.IsSuccessStatusCode)
                    {
                        var tokenResponse = await refreshResponse.Content
                            .ReadFromJsonAsync<TokenRefreshResponse>(cancellationToken);
                        if (tokenResponse is not null)
                        {
                            var httpContext = httpContextAccessor.HttpContext;
                            if (httpContext is not null)
                            {
                                var accessCookieOptions = new CookieOptions
                                {
                                    HttpOnly = true,
                                    Secure = true,
                                    SameSite = SameSiteMode.Strict,
                                    Expires = DateTimeOffset.UtcNow.AddHours(24)
                                };
                                httpContext.Response.Cookies.Append(
                                    "access_token", tokenResponse.AccessToken, accessCookieOptions);
                                var refreshCookieOptions = new CookieOptions
                                {
                                    HttpOnly = true,
                                    Secure = true,
                                    SameSite = SameSiteMode.Strict,
                                    Expires = DateTimeOffset.UtcNow.AddDays(7)
                                };
                                httpContext.Response.Cookies.Append(
                                    "refresh_token", tokenResponse.RefreshToken, refreshCookieOptions);
                            }
                            logger.LogInformation("トークンリフレッシュ成功: 新トークンを Cookie に保存");
                            request.Headers.Authorization =
                                new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);
                            response = await base.SendAsync(request, cancellationToken);
                        }
                    }
                    else
                    {
                        logger.LogWarning("トークンリフレッシュ失敗: Status={StatusCode}",
                            (int)refreshResponse.StatusCode);
                        // リフレッシュ失敗時: Authorization ヘッダーを除去して再試行
                        // （公開エンドポイントへの古いトークン付きリクエストを救済）
                        request.Headers.Authorization = null;
                        response = await base.SendAsync(request, cancellationToken);
                    }
                }
                else
                {
                    // リフレッシュトークンなし: Authorization ヘッダーを除去して再試行
                    // （公開エンドポイントへの古いアクセストークン付きリクエストを救済）
                    logger.LogDebug("リフレッシュトークンなし: Authorization ヘッダーを除去して再試行");
                    request.Headers.Authorization = null;
                    response = await base.SendAsync(request, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "トークンリフレッシュ中にエラー発生");
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        return response;
    }

    private void AttachAccessToken(HttpRequestMessage request)
    {
        var accessToken = httpContextAccessor.HttpContext?.Request.Cookies["access_token"];
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
    }
}

internal record TokenRefreshResponse(string AccessToken, string RefreshToken);
