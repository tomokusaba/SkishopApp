// ─────────────────────────────────────────────────────────────
// GatewayWebApplicationFactory — テスト用 WebApplicationFactory
//
// ApiGateway の統合テスト基盤。JWT 認証を TestAuthHandler に
// 差し替え、ヘッダーベースの認証・認可シミュレーションを提供する。
// ValidateOnStart() に必要な JWT/CORS 設定もテスト用値で注入する。
// ─────────────────────────────────────────────────────────────

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ApiGateway.Infrastructure.Metrics;
using ApiGateway.Infrastructure.Resilience;

namespace ApiGateway.Tests.Fixtures;

/// <summary>
/// ApiGateway の統合テスト用 <see cref="WebApplicationFactory{TEntryPoint}"/>。
/// </summary>
public class GatewayWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // ValidateOnStart() で必須となる JWT / CORS 設定をテスト用に注入
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "https://test.skieshop.com",
                ["Jwt:Audience"] = "skieshop-test-api",
                ["Jwt:SigningKey"] = "test-signing-key-minimum-32-characters-long-for-hs256",
                ["Cors:AllowedOrigins:0"] = "http://localhost:3000"
            });
        });

        builder.ConfigureServices(services =>
        {
            // テスト用認証スキーム "Test" を登録
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

            // 本番 JwtBearer の全リクエストを TestAuthHandler に転送
            services.PostConfigure<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(
                Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.ForwardDefaultSelector = _ => "Test";
                });

            // テスト用 DI 登録: GatewayMetrics（IMeterFactory 必須）
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<GatewayMetrics>();
            services.AddSingleton<ICircuitBreakerService, CircuitBreakerService>();
            services.AddSingleton<IFallbackService, FallbackService>();
            services.AddDistributedMemoryCache();
        });
    }

    /// <summary>
    /// 指定ロールで認証済みの <see cref="HttpClient"/> を生成する。
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string role = "User")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true");
        return client;
    }

    /// <summary>
    /// 未認証状態の <see cref="HttpClient"/> を生成する。
    /// </summary>
    public HttpClient CreateUnauthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Authenticated", "false");
        return client;
    }
}

/// <summary>
/// テスト用カスタム認証ハンドラー。
/// HTTP ヘッダーに基づいて認証結果を返す。
/// </summary>
public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <inheritdoc/>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var isAuthenticated = Request.Headers["X-Test-Authenticated"].FirstOrDefault();
        if (isAuthenticated != "true")
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var role = Request.Headers["X-Test-Role"].FirstOrDefault() ?? "User";
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "test-user-id"),
            new(ClaimTypes.Name, "testuser"),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <inheritdoc/>
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.ContentType = "application/problem+json";
        await Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
            title = "Unauthorized",
            status = 401,
            detail = "無効な認証トークンです",
            instance = Request.Path.Value,
            code = "GW-4001",
            traceId = Context.TraceIdentifier
        });
    }

    /// <inheritdoc/>
    protected override async Task HandleForbiddenAsync(AuthenticationProperties? properties)
    {
        Response.StatusCode = 403;
        Response.ContentType = "application/problem+json";
        await Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
            title = "Forbidden",
            status = 403,
            detail = "このリソースへのアクセス権限がありません",
            instance = Request.Path.Value,
            code = "GW-4002",
            traceId = Context.TraceIdentifier
        });
    }
}
