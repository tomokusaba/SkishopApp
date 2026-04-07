using System.Net;
using InventoryManagementService.Tests.Fixtures;
using Shouldly;
using Xunit;

namespace InventoryManagementService.Tests.Integration;

/// <summary>
/// Product API エンドポイントの統合テスト。
/// IntegrationTestFactory（Testcontainers PostgreSQL）を使用し、
/// HTTP リクエスト/レスポンスをエンドツーエンドで検証する。
/// </summary>
[Trait("Category", "Integration")]
public class ProductEndpointsIntegrationTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Should_Return401_When_CreateProductWithoutAuth()
    {
        // Arrange
        var content = new StringContent(
            "{\"sku\":\"TEST\",\"name\":\"Test\"}",
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/products", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_ReturnHealthy_When_CheckHealth()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
