// ─────────────────────────────────────────────────────────────
// HealthCheckTests — ヘルスチェックエンドポイントの統合テスト
//
// Liveness（/health）と Readiness（/health/ready）のエンドポイントが
// 正しいステータスコードと構造化 JSON を返すことを検証する。
// ─────────────────────────────────────────────────────────────

using System.Net;
using System.Text.Json;
using ApiGateway.Tests.Fixtures;
using Xunit;

namespace ApiGateway.Tests.HealthCheck;

/// <summary>
/// ヘルスチェックエンドポイント（<c>/health</c>、<c>/health/ready</c>）の
/// レスポンス形式とステータスコードを検証する統合テスト。
/// <para>
/// Readiness チェックは <see cref="ApiGateway.Infrastructure.HealthChecks.BackendServicesHealthCheck"/>
/// を実行するが、テスト環境ではバックエンドサービスが起動していないため
/// Degraded/Unhealthy が返る場合がある。
/// </para>
/// </summary>
[Trait("Category", "HealthCheck")]
public class HealthCheckTests(GatewayWebApplicationFactory factory)
    : IClassFixture<GatewayWebApplicationFactory>
{
    /// <summary>
    /// Liveness エンドポイント（<c>/health</c>）が常に 200 OK を返すことを検証する。
    /// </summary>
    [Fact]
    public async Task Should_Return200_When_LivenessEndpointRequested()
    {
        // Arrange
        var client = factory.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Readiness エンドポイント（<c>/health/ready</c>）が <c>status</c>、<c>duration</c>、
    /// <c>checks</c> を含む構造化 JSON を返すことを検証する。
    /// </summary>
    [Fact]
    public async Task Should_ReturnJsonResponse_When_ReadinessEndpointRequested()
    {
        // Arrange
        var client = factory.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/health/ready");
        var content = await response.Content.ReadAsStringAsync();

        // Assert — readiness may be Unhealthy (backends not running in test)
        // but should return structured JSON with status, duration, checks
        Assert.NotEmpty(content);
        var json = JsonDocument.Parse(content);
        Assert.True(json.RootElement.TryGetProperty("status", out _));
        Assert.True(json.RootElement.TryGetProperty("duration", out _));
        Assert.True(json.RootElement.TryGetProperty("checks", out _));
    }
}
