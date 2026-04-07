// ─────────────────────────────────────────────────────────────
// YarpRoutingTests — YARP ルーティング構成の統合テスト
//
// appsettings.json の 45 ルート / 9 クラスター定義が正しく
// YARP に読み込まれているかを検証する。
// バックエンドが起動していない環境でもルーティング構成の正確性を確認可能。
// ─────────────────────────────────────────────────────────────

using System.Net;
using ApiGateway.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace ApiGateway.Tests.Routing;

/// <summary>
/// YARP リバースプロキシのルーティング構成テスト。
/// <para>
/// appsettings.json から読み込まれた YARP のルート・クラスター定義が
/// 設計書 §7 / §8 の仕様に準拠しているかを検証する。
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class YarpRoutingTests : IClassFixture<GatewayWebApplicationFactory>
{
    private readonly GatewayWebApplicationFactory _factory;

    /// <summary>テストフィクスチャの初期化。</summary>
    public YarpRoutingTests(GatewayWebApplicationFactory factory) => _factory = factory;

    /// <summary>
    /// 設計書 §7 で定義された全 45 ルートが YARP 構成に登録されていることを検証する。
    /// R-C1〜R-C3, R-H1〜R-H4 の修正により、9 つのバックエンドサービスを網羅するルート数に拡張。
    /// </summary>
    [Fact]
    public void Should_LoadAllRoutes_When_ConfigurationIsValid()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var proxyConfig = scope.ServiceProvider.GetRequiredService<IProxyConfigProvider>();
        var config = proxyConfig.GetConfig();

        // Assert — 45 ルートが全て読み込まれている（修正後の正しいルート数）
        Assert.NotNull(config.Routes);
        Assert.Equal(45, config.Routes.Count);
    }

    /// <summary>
    /// 設計書 §7 で定義された全 9 クラスターが YARP 構成に登録されていることを検証する。
    /// R-C2 の修正により mail-cluster が追加され、9 クラスターとなった。
    /// </summary>
    [Fact]
    public void Should_LoadAllClusters_When_ConfigurationIsValid()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var proxyConfig = scope.ServiceProvider.GetRequiredService<IProxyConfigProvider>();
        var config = proxyConfig.GetConfig();

        // Assert — 9 クラスター（mail-cluster 追加後）
        Assert.NotNull(config.Clusters);
        Assert.Equal(9, config.Clusters.Count);
    }

    /// <summary>
    /// 全クラスターにアクティブヘルスチェックが設定されていることを検証する。
    /// </summary>
    [Fact]
    public void Should_HaveActiveHealthCheck_ForAllClusters()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var proxyConfig = scope.ServiceProvider.GetRequiredService<IProxyConfigProvider>();
        var config = proxyConfig.GetConfig();

        // Assert — 全クラスターにアクティブヘルスチェックが設定されている
        foreach (var cluster in config.Clusters)
        {
            Assert.NotNull(cluster.HealthCheck);
            Assert.NotNull(cluster.HealthCheck.Active);
            Assert.True(cluster.HealthCheck.Active.Enabled,
                $"Cluster '{cluster.ClusterId}' にアクティブヘルスチェックが未設定");
        }
    }

    /// <summary>
    /// 各ルートに RateLimiterPolicy が割り当てられていることを検証する。
    /// </summary>
    [Fact]
    public void Should_HaveRateLimiterPolicy_ForAllRoutes()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var proxyConfig = scope.ServiceProvider.GetRequiredService<IProxyConfigProvider>();
        var config = proxyConfig.GetConfig();

        // Assert
        foreach (var route in config.Routes)
        {
            Assert.False(string.IsNullOrEmpty(route.RateLimiterPolicy),
                $"Route '{route.RouteId}' に RateLimiterPolicy が未設定");
        }
    }

    /// <summary>
    /// 期待される 9 クラスター ID が全て存在することを検証する。
    /// mail-cluster は R-C2 修正で追加。
    /// </summary>
    [Theory]
    [InlineData("auth-cluster")]
    [InlineData("user-cluster")]
    [InlineData("inventory-cluster")]
    [InlineData("sales-cluster")]
    [InlineData("payment-cart-cluster")]
    [InlineData("points-cluster")]
    [InlineData("coupons-cluster")]
    [InlineData("ai-cluster")]
    [InlineData("mail-cluster")]
    public void Should_ContainExpectedCluster(string expectedClusterId)
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var proxyConfig = scope.ServiceProvider.GetRequiredService<IProxyConfigProvider>();
        var config = proxyConfig.GetConfig();

        // Assert
        Assert.Contains(config.Clusters, c => c.ClusterId == expectedClusterId);
    }

    /// <summary>
    /// 認証不要のルートへの匿名アクセスが YARP に到達することを検証する
    /// （バックエンドが起動していないため 502 が期待値）。
    /// R-C1 修正により InventoryManagementService は /api/* パターンを使用。
    /// </summary>
    [Theory]
    [InlineData("/api/products")]
    [InlineData("/api/categories")]
    public async Task Should_ReachYarp_When_AnonymousRouteAccessed(string path)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(path);

        // Assert — バックエンドが未起動のため 502（Bad Gateway）が返る
        // これは YARP に正しくルーティングされたことを示す
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    /// <summary>
    /// 認証が必要なルートへの未認証アクセスが 401 を返すことを検証する。
    /// R-C1 修正により正しい API パスを使用。
    /// </summary>
    [Theory]
    [InlineData("/api/v1/orders")]
    [InlineData("/api/v1/cart/items")]
    [InlineData("/api/v1/users/profile")]
    public async Task Should_Return401_When_AuthRequiredRouteAccessedWithoutAuth(string path)
    {
        // Arrange
        var client = _factory.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// 管理者専用ルートへの一般ユーザーアクセスが 403 を返すことを検証する。
    /// AdminOrManager ポリシーが設定されたルートを対象とする。
    /// R-C1, R-C2, R-H3 修正により正しい管理者ルートパスを使用。
    /// </summary>
    [Theory]
    [InlineData("/api/v1/admin/users")]
    [InlineData("/api/v1/admin/points/adjustments")]
    [InlineData("/admin/mail/templates/welcome")]
    public async Task Should_Return403_When_AdminRouteAccessedByUser(string path)
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient("User");

        // Act
        var response = await client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
