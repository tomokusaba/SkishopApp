using System.Net;
using InventoryManagementService.Tests.Fixtures;
using Shouldly;
using Xunit;

namespace InventoryManagementService.Tests.Integration;

/// <summary>
/// Inventory API エンドポイントの統合テスト。
/// IntegrationTestFactory（Testcontainers PostgreSQL）を使用し、
/// AdminOnly エンドポイントの認証・認可を検証する。
/// </summary>
[Trait("Category", "Integration")]
public class InventoryEndpointsIntegrationTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Should_Return401_When_GetLowStockWithoutAuth()
    {
        // Act
        var response = await _client.GetAsync("/api/inventory/low-stock?threshold=10");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_Return401_When_StockInWithoutAuth()
    {
        // Arrange
        var content = new StringContent(
            "{\"productId\":\"prod-1\",\"quantity\":10,\"locationCode\":\"WH-01\"}",
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/inventory/stock-in", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_Return401_When_StockOutWithoutAuth()
    {
        // Arrange
        var content = new StringContent(
            "{\"productId\":\"prod-1\",\"quantity\":5}",
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/inventory/stock-out", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
