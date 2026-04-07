// ─────────────────────────────────────────────────────────────
// AuthenticationFilterTests — 認証・認可フィルタの統合テスト
//
// Fallback Policy（全エンドポイント認証必須）、AllowAnonymous 除外、
// ロールベース認可（AdminOnly, AdminOrManager）の動作を検証する。
// ─────────────────────────────────────────────────────────────

using System.Net;
using ApiGateway.Tests.Fixtures;
using Xunit;

namespace ApiGateway.Tests.Security;

/// <summary>
/// JWT Bearer 認証フィルタおよびロールベース認可ポリシーの統合テスト。
/// <list type="bullet">
///   <item>匿名エンドポイント（<c>/health</c>）へのアクセス許可</item>
///   <item>認証必須エンドポイントへの未認証アクセス拒否（401）</item>
///   <item>AdminOrManager ポリシーによるロール制限（403）</item>
///   <item>Admin ロールでの管理エンドポイントアクセス許可</item>
/// </list>
/// </summary>
public class AuthenticationFilterTests : IClassFixture<GatewayWebApplicationFactory>
{
    private readonly GatewayWebApplicationFactory _factory;

    public AuthenticationFilterTests(GatewayWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// 匿名許可エンドポイント（<c>/health</c>, <c>/health/ready</c>）に未認証でアクセスしても
    /// 401/403 が返されないことを検証する。
    /// </summary>
    [Theory]
    [Trait("Category", "Security")]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    public async Task Should_AllowAccess_When_AnonymousEndpointRequested(string path)
    {
        // Arrange
        var client = _factory.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync(path);

        // Assert
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// 認証必須エンドポイントにトークンなしでアクセスした場合、401 が返されることを検証する。
    /// </summary>
    [Theory]
    [Trait("Category", "Security")]
    [InlineData("/api/orders")]
    [InlineData("/api/users/profile")]
    [InlineData("/api/points/balance")]
    [InlineData("/api/cart/checkout")]
    public async Task Should_Return401_When_AuthenticatedEndpointAccessedWithoutToken(string path)
    {
        // Arrange
        var client = _factory.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// User ロールで AdminOrManager ポリシーが設定されたエンドポイントにアクセスした場合、403 が返されることを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Security")]
    public async Task Should_Return403_When_NonAdminAccessesAdminEndpoint()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient("User");

        // Act
        var response = await client.GetAsync("/api/inventory/stock");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Admin ロールで管理者専用エンドポイントにアクセスした場合、認証・認可エラーが発生しないことを検証する。
    /// </summary>
    [Fact]
    [Trait("Category", "Security")]
    public async Task Should_AllowAccess_When_AdminAccessesAdminEndpoint()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient("Admin");

        // Act
        var response = await client.GetAsync("/api/inventory/stock");

        // Assert
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
