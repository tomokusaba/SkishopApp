// ─────────────────────────────────────────────────────────────
// CircuitBreakerTests — 耐障害性・レジリエンスの統合テスト
//
// バックエンドサービス障害時のゲートウェイ動作を検証する。
// テスト環境ではバックエンドが起動していないため、意図的に障害状態を再現する。
// ─────────────────────────────────────────────────────────────

using System.Net;
using ApiGateway.Tests.Fixtures;
using Xunit;

namespace ApiGateway.Tests.Resilience;

/// <summary>
/// バックエンドサービス障害時のゲートウェイレスポンス（5xx エラー）と
/// 相関 ID の保持を検証する統合テスト。
/// テスト環境ではバックエンドサービスが起動していないため、
/// 全リクエストが YARP 転送失敗となり障害状態を自然に再現する。
/// </summary>
[Trait("Category", "Resilience")]
public class CircuitBreakerTests(GatewayWebApplicationFactory factory)
    : IClassFixture<GatewayWebApplicationFactory>
{
    /// <summary>
    /// バックエンドサービスが未起動の場合、ゲートウェイが 5xx エラーを返すことを検証する。
    /// R-C1 修正により正しいパス /api/v1/users/profile を使用。
    /// </summary>
    [Fact]
    public async Task Should_Return502_When_BackendServiceUnavailable()
    {
        // Arrange
        var client = factory.CreateAuthenticatedClient("User");

        // Act — request to a route whose backend is not running
        var response = await client.GetAsync("/api/v1/users/profile");

        // Assert — should get 502 Bad Gateway since backend is down
        Assert.True(
            response.StatusCode is HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout
                or HttpStatusCode.InternalServerError,
            $"Expected 5xx error, got {response.StatusCode}");
    }

    /// <summary>
    /// バックエンドエラー発生時でも <c>X-Correlation-Id</c> ヘッダーが保持されることを検証する。
    /// </summary>
    [Fact]
    public async Task Should_IncludeCorrelationId_When_BackendError()
    {
        // Arrange
        var client = factory.CreateAuthenticatedClient("User");
        var correlationId = "resilience-test-123";
        client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);

        // Act
        var response = await client.GetAsync("/api/users/profile");

        // Assert — correlation ID should be preserved even on errors
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
        Assert.Equal(correlationId, response.Headers.GetValues("X-Correlation-Id").First());
    }
}
