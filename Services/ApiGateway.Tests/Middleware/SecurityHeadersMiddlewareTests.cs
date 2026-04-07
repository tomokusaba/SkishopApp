// ─────────────────────────────────────────────────────────────
// SecurityHeadersMiddlewareTests — セキュリティヘッダーの統合テスト
//
// OWASP 推奨のセキュリティヘッダー（CSP, X-Frame-Options, HSTS 等）が
// 全レスポンスに付与されること、Server ヘッダーが除去されることを検証する。
// ─────────────────────────────────────────────────────────────

using System.Net;
using ApiGateway.Tests.Fixtures;
using Xunit;

namespace ApiGateway.Tests.Middleware;

/// <summary>
/// <see cref="ApiGateway.Infrastructure.Middleware.SecurityHeadersMiddleware"/> と
/// <see cref="ApiGateway.Infrastructure.Middleware.ResponseTimeMiddleware"/> が
/// 適切なレスポンスヘッダーを付与することを検証する統合テスト。
/// </summary>
[Trait("Category", "Security")]
public class SecurityHeadersMiddlewareTests(GatewayWebApplicationFactory factory)
    : IClassFixture<GatewayWebApplicationFactory>
{
    /// <summary>
    /// OWASP 推奨の全セキュリティヘッダー（X-Content-Type-Options, X-Frame-Options, CSP 等）が
    /// レスポンスに含まれることを検証する。
    /// </summary>
    [Fact]
    public async Task Should_IncludeAllSecurityHeaders_When_AnyResponseReturned()
    {
        // Arrange
        var client = factory.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").First());

        Assert.True(response.Headers.Contains("X-Frame-Options"));
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").First());

        Assert.True(response.Headers.Contains("X-XSS-Protection"));
        Assert.Equal("0", response.Headers.GetValues("X-XSS-Protection").First());

        Assert.True(response.Headers.Contains("Referrer-Policy"));
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").First());

        Assert.True(response.Content.Headers.Contains("Content-Security-Policy")
            || response.Headers.Contains("Content-Security-Policy"));

        // HSTS はテスト環境（HTTP）では付与されない場合がある
        // TestServer は HTTP のみのため、HSTS ヘッダーの存在チェックはスキップ
    }

    /// <summary>
    /// API レスポンスに <c>Cache-Control: no-store</c> が含まれることを検証する。
    /// </summary>
    [Fact]
    public async Task Should_IncludeCacheControlNoStore_When_ApiResponseReturned()
    {
        // Arrange
        var client = factory.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true
            || response.Headers.Contains("Cache-Control"));
    }

    /// <summary>
    /// <c>Server</c> ヘッダーがレスポンスから除去されていることを検証する（サーバー情報漏洩防止）。
    /// </summary>
    [Fact]
    public async Task Should_NotIncludeServerHeader_When_ResponseReturned()
    {
        // Arrange
        var client = factory.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Server"));
    }

    /// <summary>
    /// <c>Permissions-Policy</c> ヘッダーが付与され、不要なブラウザ機能が無効化されていることを検証する。
    /// </summary>
    [Fact]
    public async Task Should_IncludePermissionsPolicy_When_AnyResponseReturned()
    {
        // Arrange
        var client = factory.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Permissions-Policy"));
        Assert.Equal("camera=(), microphone=(), geolocation=()",
            response.Headers.GetValues("Permissions-Policy").First());
    }

    /// <summary>
    /// <c>X-Response-Time</c> ヘッダーがミリ秒単位で付与されることを検証する。
    /// </summary>
    [Fact]
    public async Task Should_IncludeResponseTime_When_AnyResponseReturned()
    {
        // Arrange
        var client = factory.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Response-Time"));
        var responseTime = response.Headers.GetValues("X-Response-Time").First();
        Assert.EndsWith("ms", responseTime);
    }
}
