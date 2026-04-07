using System.Net;
using System.Net.Http.Headers;
using Frontend.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Frontend.Tests.Services;

[Trait("Category", "Unit")]
public class TokenRefreshHandlerTests
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<TokenRefreshHandler> _logger;

    public TokenRefreshHandlerTests()
    {
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _logger = Substitute.For<ILogger<TokenRefreshHandler>>();
    }

    [Fact]
    public async Task Should_AddAuthHeader_When_TokenExists()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Append("Cookie", "access_token=test-jwt-token");
        _httpContextAccessor.HttpContext.Returns(httpContext);

        var innerHandler = new FakeInnerHandler(HttpStatusCode.OK);
        var handler = new TokenRefreshHandler(_httpContextAccessor, _logger)
        {
            InnerHandler = innerHandler
        };
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };

        // Act
        var response = await client.GetAsync("/api/test");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        innerHandler.LastRequest.ShouldNotBeNull();
        innerHandler.LastRequest.Headers.Authorization.ShouldNotBeNull();
        innerHandler.LastRequest.Headers.Authorization.Scheme.ShouldBe("Bearer");
        innerHandler.LastRequest.Headers.Authorization.Parameter.ShouldBe("test-jwt-token");
    }

    [Fact]
    public async Task Should_PassThrough_When_NoToken()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        _httpContextAccessor.HttpContext.Returns(httpContext);

        var innerHandler = new FakeInnerHandler(HttpStatusCode.OK);
        var handler = new TokenRefreshHandler(_httpContextAccessor, _logger)
        {
            InnerHandler = innerHandler
        };
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://localhost") };

        // Act
        var response = await client.GetAsync("/api/test");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        innerHandler.LastRequest.ShouldNotBeNull();
        innerHandler.LastRequest.Headers.Authorization.ShouldBeNull();
    }

    /// <summary>
    /// テスト用の内部ハンドラー — DelegatingHandler チェーンのテストに使用
    /// </summary>
    private sealed class FakeInnerHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }
}
