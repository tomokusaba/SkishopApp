// ─────────────────────────────────────────────────────────────
// CorrelationIdMiddlewareTests — 相関 ID ミドルウェアの統合テスト
//
// X-Correlation-Id ヘッダーの自動生成・クライアント提供値の保持を検証する。
// ─────────────────────────────────────────────────────────────

using System.Net;
using ApiGateway.Tests.Fixtures;
using Xunit;

namespace ApiGateway.Tests.Middleware;

/// <summary>
/// <see cref="ApiGateway.Infrastructure.Middleware.CorrelationIdMiddleware"/> の
/// 相関 ID 付与・バリデーション・レスポンス伝搬を検証する統合テスト。
/// </summary>
[Trait("Category", "Middleware")]
public class CorrelationIdMiddlewareTests(GatewayWebApplicationFactory factory)
    : IClassFixture<GatewayWebApplicationFactory>
{
    /// <summary>
    /// <c>X-Correlation-Id</c> ヘッダーが未提供の場合、新規 GUID が自動生成されることを検証する。
    /// </summary>
    [Fact]
    public async Task Should_GenerateCorrelationId_When_HeaderNotProvided()
    {
        // Arrange
        var client = factory.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
        var correlationId = response.Headers.GetValues("X-Correlation-Id").First();
        Assert.True(Guid.TryParse(correlationId, out _));
    }

    /// <summary>
    /// クライアントが提供した有効な相関 ID がレスポンスにそのまま返されることを検証する。
    /// </summary>
    [Fact]
    public async Task Should_PreserveCorrelationId_When_HeaderProvided()
    {
        // Arrange
        var client = factory.CreateUnauthenticatedClient();
        var expectedId = "test-correlation-12345";
        client.DefaultRequestHeaders.Add("X-Correlation-Id", expectedId);

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
        var correlationId = response.Headers.GetValues("X-Correlation-Id").First();
        Assert.Equal(expectedId, correlationId);
    }
}
