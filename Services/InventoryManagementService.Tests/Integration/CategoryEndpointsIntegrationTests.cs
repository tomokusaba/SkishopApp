using System.Net;
using InventoryManagementService.Tests.Fixtures;
using Shouldly;
using Xunit;

namespace InventoryManagementService.Tests.Integration;

/// <summary>
/// Category API エンドポイントの統合テスト。
/// IntegrationTestFactory（Testcontainers PostgreSQL）を使用し、
/// HTTP リクエスト/レスポンスをエンドツーエンドで検証する。
/// </summary>
[Trait("Category", "Integration")]
public class CategoryEndpointsIntegrationTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Should_Return200_When_GetAllCategories()
    {
        // Act
        var response = await _client.GetAsync("/api/categories");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_Return401_When_CreateCategoryWithoutAuth()
    {
        // Arrange
        var content = new StringContent(
            "{\"name\":\"Test Category\"}",
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/categories", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_Return401_When_DeleteCategoryWithoutAuth()
    {
        // Arrange
        var categoryId = "nonexistent-id";

        // Act
        var response = await _client.DeleteAsync($"/api/categories/{categoryId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
