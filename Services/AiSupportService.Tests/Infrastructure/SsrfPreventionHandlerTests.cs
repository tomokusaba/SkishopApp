using System.Net;
using System.Net.Sockets;
using AiSupportService.Configurations;
using AiSupportService.Infrastructure.SemanticKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AiSupportService.Tests.Infrastructure;

public class SsrfPreventionHandlerTests
{
    private readonly SsrfPreventionHandler _sut;
    private readonly ILogger<SsrfPreventionHandler> _logger;

    public SsrfPreventionHandlerTests()
    {
        _logger = Substitute.For<ILogger<SsrfPreventionHandler>>();
        var settings = Options.Create(new ServiceEndpointSettings
        {
            InventoryManagementService = "https://inventory-service:5003",
            SalesManagementService = "https://sales-service:5004"
        });
        _sut = new SsrfPreventionHandler(settings, _logger)
        {
            InnerHandler = new DummyHandler()
        };
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_BlockRequest_When_UriIsNull()
    {
        // HttpClient validates URI before the handler, so we invoke SendAsync
        // on the handler directly using reflection to test null URI handling.
        var method = typeof(SsrfPreventionHandler)
            .GetMethod("SendAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("SendAsync method not found");

        var request = new HttpRequestMessage { Method = HttpMethod.Get, RequestUri = null };
        var task = (Task<HttpResponseMessage>)method.Invoke(_sut, [request, CancellationToken.None])!;
        var response = await task;

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("http://localhost/api")]
    [InlineData("http://127.0.0.1/api")]
    [InlineData("http://[::1]/api")]
    [InlineData("http://0.0.0.0/api")]
    public async Task Should_BlockRequest_When_HostIsBlockedIPv4OrLoopback(string url)
    {
        // Arrange
        using var client = new HttpClient(_sut);
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("http://[::ffff:127.0.0.1]/api")]
    [InlineData("http://[::ffff:10.0.0.1]/api")]
    public async Task Should_BlockRequest_When_HostIsIPv6MappedPrivate(string url)
    {
        // Arrange
        using var client = new HttpClient(_sut);
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_DetectPrivate_When_IPv6UniqueLocalAddress()
    {
        // fc00::/7 — ULA (Unique Local Address)
        var addr = IPAddress.Parse("fd12:3456:789a::1");
        IsPrivateOrLoopback(addr).ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_DetectPrivate_When_IPv6LinkLocalAddress()
    {
        // fe80::/10 — Link-Local
        var addr = IPAddress.Parse("fe80::1");
        IsPrivateOrLoopback(addr).ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_DetectPrivate_When_IPv6LoopbackAddress()
    {
        var addr = IPAddress.Parse("::1");
        IsPrivateOrLoopback(addr).ShouldBeTrue();
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.1.1")]
    [InlineData("127.0.0.1")]
    public void Should_DetectPrivate_When_IPv4PrivateRange(string ip)
    {
        var addr = IPAddress.Parse(ip);
        IsPrivateOrLoopback(addr).ShouldBeTrue();
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("203.0.113.1")]
    public void Should_NotDetectPrivate_When_PublicIPv4(string ip)
    {
        var addr = IPAddress.Parse(ip);
        IsPrivateOrLoopback(addr).ShouldBeFalse();
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("2001:db8::1")]
    [InlineData("2607:f8b0:4004:800::200e")]
    public void Should_NotDetectPrivate_When_PublicIPv6(string ip)
    {
        var addr = IPAddress.Parse(ip);
        IsPrivateOrLoopback(addr).ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Should_DetectPrivate_When_IPv4MappedToIPv6()
    {
        // ::ffff:192.168.1.1 → maps to 192.168.1.1 (private)
        var addr = IPAddress.Parse("::ffff:192.168.1.1");
        IsPrivateOrLoopback(addr).ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_AllowRequest_When_WhitelistedServiceEndpoint()
    {
        // Arrange
        using var client = new HttpClient(_sut);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://inventory-service:5003/api/products");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Reflection helper to test the private static IsPrivateOrLoopback method.
    /// </summary>
    private static bool IsPrivateOrLoopback(IPAddress address)
    {
        var method = typeof(SsrfPreventionHandler)
            .GetMethod("IsPrivateOrLoopback", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?? throw new InvalidOperationException("IsPrivateOrLoopback method not found");
        return (bool)method.Invoke(null, [address])!;
    }

    /// <summary>
    /// Dummy inner handler that returns 200 OK for allowed requests.
    /// </summary>
    private sealed class DummyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
