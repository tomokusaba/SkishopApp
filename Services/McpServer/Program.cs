using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using McpServer.Configurations;
using McpServer.Infrastructure.ErrorHandling;
using McpServer.Infrastructure.Http;
using McpServer.Services;
using McpServer.Services.Interfaces;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Context;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "McpServer")
        .WriteTo.Console(new CompactJsonFormatter()));

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1024 * 1024;
    options.AddServerHeader = false;
});

builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddOptions<InventoryManagementOptions>()
    .Bind(builder.Configuration.GetSection(InventoryManagementOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<McpServerSecurityOptions>()
    .Bind(builder.Configuration.GetSection(McpServerSecurityOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey), "McpServer:Security:ApiKey must be configured.")
    .ValidateOnStart();

var inventoryBaseUrl = builder.Configuration[$"{InventoryManagementOptions.SectionName}:BaseUrl"]
    ?? throw new InvalidOperationException("Services:InventoryManagement:BaseUrl is not configured.");

if (!Uri.TryCreate(inventoryBaseUrl, UriKind.Absolute, out var inventoryBaseUri))
{
    throw new InvalidOperationException("Services:InventoryManagement:BaseUrl must be an absolute URI.");
}

builder.Services.AddTransient<CorrelationIdDelegatingHandler>();
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        if (!httpContext.Request.Path.StartsWithSegments("/mcp"))
        {
            return RateLimitPartition.GetNoLimiter("non-mcp");
        }

        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
    options.OnRejected = static async (context, _) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await TypedResults.Problem(
            detail: "Too many MCP requests. Please retry shortly.",
            statusCode: StatusCodes.Status429TooManyRequests,
            title: "Too Many Requests")
            .ExecuteAsync(context.HttpContext);
    };
});

builder.Services.AddHttpClient<IInventoryCatalogClient, InventoryCatalogClient>((serviceProvider, httpClient) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<InventoryManagementOptions>>().Value;
    httpClient.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
})
.AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
    options.Retry.Delay = TimeSpan.FromMilliseconds(500);
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.MinimumThroughput = 3;
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddScoped<ICatalogToolService, CatalogToolService>();

builder.Services.AddHealthChecks()
    .AddUrlGroup(
        uri: new Uri(inventoryBaseUri, "/health/ready"),
        name: "inventory-management",
        tags: ["ready"],
        timeout: TimeSpan.FromSeconds(3));

var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("SkiShop.McpServer");

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    });

builder.Services.AddMcpServer(options =>
{
    options.ServerInfo = new()
    {
        Name = "SkiShopMcpServer",
        Version = "1.0.0"
    };
})
.WithHttpTransport(options => options.Stateless = true)
.WithToolsFromAssembly();

var app = builder.Build();

app.UseExceptionHandler();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

    await next();
});

app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? Guid.NewGuid().ToString();

    context.Request.Headers["X-Correlation-Id"] = correlationId;
    context.Response.Headers["X-Correlation-Id"] = correlationId;

    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next();
    }
});

app.UseSerilogRequestLogging();
app.UseRateLimiter();
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/health"),
    branch => branch.Use(async (context, next) =>
    {
        var securityOptions = context.RequestServices.GetRequiredService<IOptions<McpServerSecurityOptions>>().Value;
        if (!context.Request.Headers.TryGetValue(McpServerSecurityOptions.ApiKeyHeaderName, out var providedApiKey)
            || !MatchesApiKey(providedApiKey.ToString(), securityOptions.ApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await TypedResults.Problem(
                    detail: $"The '{McpServerSecurityOptions.ApiKeyHeaderName}' header is required to access the MCP server.",
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Unauthorized")
                .ExecuteAsync(context);
            return;
        }

        await next();
    }));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();
app.MapMcp("/mcp");

app.Run();

static bool MatchesApiKey(string providedApiKey, string expectedApiKey)
{
    var providedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(providedApiKey));
    var expectedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(expectedApiKey));

    return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
}
