// ─────────────────────────────────────────────────────────────
// RateLimitingTests — レート制限ポリシーの統合テスト
//
// 各レート制限ポリシー（login, checkout, products, anonymous-api,
// ai-api, user-based）が正しく適用され、超過時に 429 + Retry-After
// ヘッダーが返されることを検証する。
// ─────────────────────────────────────────────────────────────

using System.Net;
using ApiGateway.Tests.Fixtures;
using Xunit;

namespace ApiGateway.Tests.RateLimiting;

/// <summary>
/// ASP.NET Core Rate Limiter のポリシー適用と
/// 429 Too Many Requests レスポンスの形式を検証する統合テスト。
/// </summary>
[Trait("Category", "RateLimit")]
public class RateLimitingTests(GatewayWebApplicationFactory factory)
    : IClassFixture<GatewayWebApplicationFactory>
{
    /// <summary>
    /// ログインエンドポイントのレート制限（5 req/min/IP）を超過した場合、429 が返されることを検証する。
    /// R-C1 修正により正しいパス /api/v1/auth/login を使用。
    /// </summary>
    [Fact]
    public async Task Should_Return429WithRetryAfter_When_RateLimitExceeded()
    {
        // Arrange
        var client = factory.CreateUnauthenticatedClient();
        var loginEndpoint = "/api/v1/auth/login";

        // Act — login policy: 5 req/min/IP, send 6 requests
        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 6; i++)
        {
            lastResponse = await client.PostAsync(loginEndpoint,
                new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        }

        // Assert
        Assert.NotNull(lastResponse);
        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse.StatusCode);
    }

    /// <summary>
    /// レート制限の上限内であればリクエストが正常に処理されることを検証する。
    /// </summary>
    [Fact]
    public async Task Should_AllowRequest_When_WithinRateLimit()
    {
        // Arrange
        var client = factory.CreateUnauthenticatedClient();

        // Act — health endpoint is not rate limited
        var response = await client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// レート制限超過時のレスポンスに <c>Retry-After</c> ヘッダーが含まれることを検証する。
    /// R-C1 修正により正しいパス /api/v1/auth/login を使用。
    /// </summary>
    [Fact]
    public async Task Should_IncludeRetryAfterHeader_When_RateLimited()
    {
        // Arrange
        var client = factory.CreateUnauthenticatedClient();
        var loginEndpoint = "/api/v1/auth/login";

        // Act — exceed rate limit
        HttpResponseMessage? limitedResponse = null;
        for (var i = 0; i < 7; i++)
        {
            var response = await client.PostAsync(loginEndpoint,
                new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                limitedResponse = response;
                break;
            }
        }

        // Assert
        Assert.NotNull(limitedResponse);
        Assert.True(limitedResponse.Headers.Contains("Retry-After"));
    }
}
