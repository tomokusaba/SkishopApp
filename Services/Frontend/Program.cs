using Fluxor;
using MudBlazor.Services;
using Serilog;
using Serilog.Formatting.Compact;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Frontend.Infrastructure.Middleware;
using Frontend.Services;
using Frontend.Services.Interfaces;
using Frontend.Handlers;
using Frontend.Endpoints;
using Microsoft.AspNetCore.Localization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Serilog 構成
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "Frontend")
        .WriteTo.Console(new CompactJsonFormatter()));

// Blazor Web App（Phase 1: InteractiveServer のみ。WASM は Phase 5 で追加）
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// SignalR（AI チャットストリーミング用 — §16.3）
builder.Services.AddSignalR();

// MudBlazor
builder.Services.AddMudServices();

// Fluxor 状態管理
builder.Services.AddFluxor(options =>
    options.ScanAssemblies(typeof(Program).Assembly));

// 認証 Cookie 設定（BFF パターン）
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/auth/login";
        options.LogoutPath = "/auth/logout";
        options.AccessDeniedPath = "/auth/access-denied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// DelegatingHandler 登録
builder.Services.AddTransient<TokenRefreshHandler>();
builder.Services.AddTransient<CorrelationIdHandler>();

// H-04: IOptions<T> パターンで ApiGateway 設定
builder.Services.AddOptions<ApiGatewaySettings>()
    .Bind(builder.Configuration.GetSection("ApiGateway"))
    .ValidateOnStart();

// API Gateway HttpClient（IHttpClientFactory + Polly + DelegatingHandlers）
var apiGatewayBaseUrl = builder.Configuration["ApiGateway:BaseUrl"] ?? string.Empty;

builder.Services.AddHttpClient("ApiGateway", client =>
{
    client.BaseAddress = new Uri(apiGatewayBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddHttpMessageHandler<CorrelationIdHandler>()
.AddHttpMessageHandler<TokenRefreshHandler>()
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
    options.Retry.Delay = TimeSpan.FromMilliseconds(500);
    // H-19: サーキットブレーカーの明示パラメータ設定
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.MinimumThroughput = 10;
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
});

// AI チャット用 HttpClient（AI 応答は長時間かかるためタイムアウトを延長、リトライなし）
builder.Services.AddHttpClient("AiChat", client =>
{
    client.BaseAddress = new Uri(apiGatewayBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(120);
})
.AddHttpMessageHandler<CorrelationIdHandler>()
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 1;
    options.Retry.Delay = TimeSpan.FromSeconds(1);
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(300);
    options.CircuitBreaker.FailureRatio = 0.8;
    options.CircuitBreaker.MinimumThroughput = 5;
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(120);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
});

// サービス DI 登録
builder.Services.AddScoped<INotificationService, SnackbarNotificationService>();
builder.Services.AddScoped<IApiErrorHandler, ApiErrorHandler>();
builder.Services.AddScoped<IHtmlSanitizationService, HtmlSanitizationService>();
builder.Services.AddScoped<IApiGatewayClient>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("ApiGateway");
    var errorHandler = sp.GetRequiredService<IApiErrorHandler>();
    var logger = sp.GetRequiredService<ILogger<ApiGatewayClient>>();
    return new ApiGatewayClient(httpClient, errorHandler, logger);
});
builder.Services.AddScoped<IAuthApiClient, AuthApiClient>();
builder.Services.AddScoped<ITokenStorageService, TokenStorageService>();
builder.Services.AddScoped<IProductApiClient, ProductApiClient>();
builder.Services.AddScoped<IAiApiClient, AiApiClient>();
builder.Services.AddScoped<ICartApiClient, CartApiClient>();
builder.Services.AddScoped<IOrderApiClient, OrderApiClient>();
builder.Services.AddScoped<ICouponApiClient, CouponApiClient>();
builder.Services.AddScoped<IPaymentApiClient, PaymentApiClient>();
builder.Services.AddScoped<IUserApiClient, UserApiClient>();
builder.Services.AddScoped<IShipmentReturnApiClient, ShipmentReturnApiClient>();
builder.Services.AddScoped<IPointApiClient, PointApiClient>();
builder.Services.AddScoped<IWishlistApiClient, WishlistApiClient>();
builder.Services.AddScoped<ICacheService, CacheService>();

// ヘルスチェック
builder.Services.AddHealthChecks();

// TimeProvider
builder.Services.AddSingleton(TimeProvider.System);

// IMemoryCache
builder.Services.AddMemoryCache();

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("Frontend.*"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation());

// i18n ローカライゼーション
// SDK スタイルプロジェクトでは .resx の埋め込みリソース名にフォルダパスが含まれないため
// ResourcesPath を指定しない（指定すると "Frontend.Resources.SharedResources" で検索されるが、
// 実際の埋め込み名は "Frontend.SharedResources" となり不一致になる）
builder.Services.AddLocalization();

// H-03: レート制限
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("ai-chat", limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

// ミドルウェアパイプライン（順序厳守）（H-02: 全環境で例外ハンドラーを有効化）
app.UseExceptionHandler("/Error");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// セキュリティヘッダー
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseSerilogRequestLogging();
app.UseStaticFiles();
app.UseRouting();

// i18n — RequestLocalization ミドルウェア
var supportedCultures = new[] { "ja", "en" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("ja")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
localizationOptions.RequestCultureProviders.Insert(0,
    new QueryStringRequestCultureProvider());
app.UseRequestLocalization(localizationOptions);

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// H-03: レート制限ミドルウェア
app.UseRateLimiter();

// ヘルスチェック（Liveness + Readiness）
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();

// サイトマップ
app.MapSitemapEndpoint();

// SignalR Hub（AI チャットストリーミング — §16.3）
app.MapHub<Frontend.Hubs.AiChatHub>("/hubs/ai-chat");

// Blazor
app.MapRazorComponents<Frontend.App>()
    .AddInteractiveServerRenderMode();

app.Run();

// H-04: ApiGateway 設定クラス
public record ApiGatewaySettings
{
    public string BaseUrl { get; init; } = string.Empty;
}
