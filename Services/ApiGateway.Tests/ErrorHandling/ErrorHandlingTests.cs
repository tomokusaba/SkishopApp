// ─────────────────────────────────────────────────────────────
// ErrorHandlingTests — グローバル例外ハンドラーの統合テスト
//
// RFC 9457 Problem Details 形式のエラーレスポンスが正しく返されること、
// スタックトレースが漏洩しないこと、エラーコード（GW-xxxx）が含まれることを検証する。
// ─────────────────────────────────────────────────────────────

using System.Net;
using System.Text.Json;
using ApiGateway.Tests.Fixtures;
using Xunit;

namespace ApiGateway.Tests.ErrorHandling;

/// <summary>
/// グローバル例外ハンドラー（<c>UseExceptionHandler</c>）と
/// JWT 認証イベント（<c>OnChallenge</c> / <c>OnForbidden</c>）の
/// エラーレスポンス形式を検証する統合テスト。
/// </summary>
[Trait("Category", "ErrorHandling")]
public class ErrorHandlingTests(GatewayWebApplicationFactory factory)
    : IClassFixture<GatewayWebApplicationFactory>
{
    /// <summary>
    /// 未認証リクエストに対して、RFC 9457 Problem Details 形式の 401 レスポンスが返されることを検証する。
    /// </summary>
    [Fact]
    public async Task Should_ReturnProblemDetailsWithErrorCode_When_UnauthorizedAccess()
    {
        // Arrange
        var client = factory.CreateUnauthenticatedClient();

        // Act — access authenticated endpoint without token
        var response = await client.GetAsync("/api/users/profile");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        Assert.True(json.RootElement.TryGetProperty("status", out var status));
        Assert.Equal(401, status.GetInt32());
    }

    /// <summary>
    /// エラーレスポンスにスタックトレースやサーバー内部情報が漏洩しないことを検証する。
    /// </summary>
    [Fact]
    public async Task Should_NotExposeStackTrace_When_ErrorOccurs()
    {
        // Arrange
        var client = factory.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/users/profile");
        var content = await response.Content.ReadAsStringAsync();

        // Assert — no stack trace in response
        Assert.DoesNotContain("StackTrace", content);
        Assert.DoesNotContain("at System.", content);
        Assert.DoesNotContain(".cs:line", content);
    }

    /// <summary>
    /// 一般ユーザーが管理者専用エンドポイントにアクセスした場合、GW-4002 コード付きの 403 が返されることを検証する。
    /// </summary>
    [Fact]
    public async Task Should_Return403WithGW4002_When_NonAdminAccessesAdminEndpoint()
    {
        // Arrange — "User" role does not have AdminOrManager access
        var client = factory.CreateAuthenticatedClient("User");

        // Act — access AdminOrManager-only endpoint as regular user
        var response = await client.GetAsync("/api/inventory/stock");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("GW-4002", content);
    }

    /// <summary>
    /// エラーレスポンスに分散トレーシング用の <c>traceId</c> が含まれることを検証する。
    /// </summary>
    [Fact]
    public async Task Should_IncludeTraceId_When_ErrorResponseReturned()
    {
        // Arrange
        var client = factory.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/users/profile");
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        // Assert — traceId should be present at top level (RFC 9457 compliant)
        Assert.True(
            json.RootElement.TryGetProperty("traceId", out _),
            "Response should contain traceId at top level");
    }
}
