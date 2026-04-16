using System.Threading.RateLimiting;
using AiSupportService.Configurations;
using AiSupportService.Endpoints;
using AiSupportService.Exceptions;
using AiSupportService.Infrastructure.Http;
using AiSupportService.Infrastructure.Kafka;
using AiSupportService.Infrastructure.Metrics;
using AiSupportService.Infrastructure.Middleware;
using AiSupportService.Infrastructure.Persistence;
using AiSupportService.Infrastructure.SemanticKernel;
using AiSupportService.Infrastructure.SemanticKernel.Plugins;
using AiSupportService.Repositories;
using AiSupportService.Repositories.Interfaces;
using AiSupportService.Services;
using AiSupportService.Services.Interfaces;
using AiSupportService.Validators;
using Confluent.Kafka;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// --- Serilog ---
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "AiSupportService")
        .WriteTo.Console(new CompactJsonFormatter()));

// --- Kestrel ---
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
    options.AddServerHeader = false;
});

// --- Graceful Shutdown ---
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});

// --- TimeProvider ---
builder.Services.AddSingleton(TimeProvider.System);

// --- DbContext ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("接続文字列 'DefaultConnection' が設定されていません");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// --- Settings ---
// Azure OpenAI/AI Search 設定: 開発環境では ValidateOnStart をスキップ（Azure リソースがない場合も起動可能に）
var azureOpenAIOptions = builder.Services.AddOptions<AzureOpenAISettings>()
    .Bind(builder.Configuration.GetSection("AzureOpenAI"))
    .ValidateDataAnnotations();
if (!builder.Environment.IsDevelopment())
    azureOpenAIOptions.ValidateOnStart();

var azureAISearchOptions = builder.Services.AddOptions<AzureAISearchSettings>()
    .Bind(builder.Configuration.GetSection("AzureAISearch"))
    .ValidateDataAnnotations();
if (!builder.Environment.IsDevelopment())
    azureAISearchOptions.ValidateOnStart();

builder.Services.AddOptions<AiChatSettings>()
    .Bind(builder.Configuration.GetSection("AiChat"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<KafkaSettings>()
    .Bind(builder.Configuration.GetSection("Kafka"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<ServiceEndpointSettings>()
    .Bind(builder.Configuration.GetSection("Services"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// --- Repositories ---
builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();
builder.Services.AddScoped<IChatSessionRepository, ChatSessionRepository>();
builder.Services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
builder.Services.AddScoped<IRecommendationRepository, RecommendationRepository>();
builder.Services.AddScoped<ISearchAnalyticsRepository, SearchAnalyticsRepository>();
builder.Services.AddScoped<IDemandForecastRepository, DemandForecastRepository>();
builder.Services.AddScoped<IModelTrainingRepository, ModelTrainingRepository>();
builder.Services.AddScoped<IOutboxEventRepository, OutboxEventRepository>();
builder.Services.AddSingleton<IFaqRepository, FaqRepository>();

// --- Services ---
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<IForecastService, ForecastService>();
builder.Services.AddScoped<IAiAnalyticsService, AiAnalyticsService>();
builder.Services.AddScoped<IModelTrainingService, ModelTrainingService>();

// --- Semantic Kernel (Scoped — プラグインが Scoped 依存を持つため) ---
builder.Services.AddScoped<Kernel>(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.Services.AddSingleton(sp.GetRequiredService<ILoggerFactory>());

    var aiSettings = sp.GetRequiredService<IOptions<AzureOpenAISettings>>().Value;
    if (!string.IsNullOrEmpty(aiSettings.ApiKey))
    {
        kernelBuilder.AddAzureOpenAIChatCompletion(
            deploymentName: aiSettings.DeploymentName,
            endpoint: aiSettings.Endpoint,
            apiKey: aiSettings.ApiKey);
    }
    else
    {
        kernelBuilder.AddAzureOpenAIChatCompletion(
            deploymentName: aiSettings.DeploymentName,
            endpoint: aiSettings.Endpoint,
            credentials: new Azure.Identity.DefaultAzureCredential());
    }

    var kernel = kernelBuilder.Build();
    kernel.Plugins.AddFromObject(sp.GetRequiredService<ProductPlugin>(), "ProductPlugin");
    kernel.Plugins.AddFromObject(sp.GetRequiredService<OrderPlugin>(), "OrderPlugin");
    kernel.Plugins.AddFromObject(sp.GetRequiredService<FaqPlugin>(), "FaqPlugin");
    return kernel;
});
builder.Services.AddScoped<ProductPlugin>();
builder.Services.AddScoped<OrderPlugin>();
builder.Services.AddScoped<FaqPlugin>();

// --- HTTP Clients (Polly 標準レジリエンスハンドラー付き) ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<SsrfPreventionHandler>();
builder.Services.AddTransient<CorrelationIdDelegatingHandler>();

var inventoryServiceUrl = builder.Configuration["Services:InventoryManagementService"]
    ?? throw new InvalidOperationException("Services:InventoryManagementService が設定されていません");
builder.Services.AddHttpClient<IProductClient, ProductClient>(client =>
{
    client.BaseAddress = new Uri(inventoryServiceUrl);
}).AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
.AddHttpMessageHandler<SsrfPreventionHandler>()
.AddStandardResilienceHandler();

var salesServiceUrl = builder.Configuration["Services:SalesManagementService"]
    ?? throw new InvalidOperationException("Services:SalesManagementService が設定されていません");
builder.Services.AddHttpClient<IOrderClient, OrderClient>(client =>
{
    client.BaseAddress = new Uri(salesServiceUrl);
}).AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
.AddHttpMessageHandler<SsrfPreventionHandler>()
.AddStandardResilienceHandler();

// --- Validators ---
builder.Services.AddValidatorsFromAssemblyContaining<SendMessageRequestValidator>(ServiceLifetime.Scoped);

// --- Kafka Producer ---
builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var bootstrapServers = builder.Configuration["Kafka:BootstrapServers"]
        ?? throw new InvalidOperationException("Kafka:BootstrapServers が設定されていません");
    var config = new ProducerConfig
    {
        BootstrapServers = bootstrapServers
    };
    return new ProducerBuilder<string, string>(config).Build();
});

// --- BackgroundServices ---
builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddHostedService<ProductIndexSyncConsumer>();
builder.Services.AddHostedService<UserDeletionConsumer>();
builder.Services.AddHostedService<OrderCreatedConsumer>();
builder.Services.AddHostedService<UserRegisteredConsumer>();
builder.Services.AddHostedService<DataRetentionCleanupService>();

// --- Metrics ---
builder.Services.AddSingleton<AiSupportMetrics>();

// --- OpenTelemetry ---
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("SkiShop.AiSupportService"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("SkiShop.AiSupportService"));

var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("接続文字列 'Redis' が設定されていません");

// --- Redis Cache (分散キャッシュ) ---
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "AiSupport:";
});
builder.Services.AddScoped<ICacheService, CacheService>();

// --- Health Checks ---
var healthChecks = builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql", tags: ["ready"])
    .AddRedis(redisConnectionString, name: "redis", tags: ["ready"])
    .AddCheck<AiSupportService.Infrastructure.HealthChecks.KafkaHealthCheck>("kafka", tags: ["ready"]);

if (!builder.Environment.IsDevelopment())
{
    healthChecks
        .AddCheck<AiSupportService.Infrastructure.HealthChecks.AzureOpenAIHealthCheck>("azure-openai", tags: ["ready"])
        .AddCheck<AiSupportService.Infrastructure.HealthChecks.AzureAISearchHealthCheck>("azure-ai-search", tags: ["ready"]);
}

// --- Exception Handler ---
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// --- JWT Settings ---
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT 設定が見つかりません");

// --- Authentication ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            ClockSkew = TimeSpan.FromMinutes(jwtSettings.ClockSkewMinutes),
            ValidAlgorithms = ["HS256", "RS256"]
        };
    });

// --- Authorization ---
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin", "ADMIN"));
    options.AddPolicy("UserOrAdmin", p => p.RequireRole("User", "Admin", "USER", "ADMIN"));
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// --- Rate Limiting ---
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("chat-api", context =>
        RateLimitPartition.GetTokenBucketLimiter(
            context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 10,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                TokensPerPeriod = 10,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 2
            }));
    options.AddPolicy("search-api", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1)
            }));
    options.AddPolicy("recommendation-api", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1)
            }));
    options.AddPolicy("admin-api", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1)
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// --- CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();

// === Middleware Pipeline (順序厳守) ===

// 1. Exception Handler
app.UseExceptionHandler();

// 2. Security Headers
app.UseHsts();
app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    await next();
});

// 3. Correlation ID
app.UseCorrelationId();

// 4. Serilog Request Logging
app.UseSerilogRequestLogging();

// 5. CORS
app.UseCors("AllowFrontend");

// 6. Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// 7. Rate Limiter
app.UseRateLimiter();

// 8. Endpoints
app.MapChatEndpoints();
app.MapSearchEndpoints();
app.MapRecommendationEndpoints();
app.MapForecastEndpoints();
app.MapAnalyticsEndpoints();
app.MapAdminEndpoints();

// 9. Health Checks
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();

// --- DB マイグレーション自動適用 ---
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
    if (pendingMigrations.Any())
    {
        var migrationLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        migrationLogger.LogInformation("未適用マイグレーション {Count} 件を適用します: {Migrations}",
            pendingMigrations.Count(), string.Join(", ", pendingMigrations));
        await dbContext.Database.MigrateAsync();
        migrationLogger.LogInformation("マイグレーション適用完了");
    }
}

// --- 匿名ユーザープロファイルのシード ---
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var anonymousExists = await dbContext.UserProfiles
        .AnyAsync(p => p.UserId == "anonymous");

    if (!anonymousExists)
    {
        var seedLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        seedLogger.LogInformation("匿名ユーザープロファイルを作成します");

        var id = "00000000-0000-0000-0000-000000000001";
        var userId = "anonymous";
        var prefs = """{"type": "anonymous", "description": "匿名ユーザー向けレコメンデーション用プロファイル"}""";
        var emptyArray = "[]";
        var rowVersion = new byte[] { 0 };

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO user_profiles (id, user_id, preferences_json, browsing_history_json, purchase_history_json, created_at, updated_at, row_version)
            VALUES ({id}, {userId}, CAST({prefs} AS jsonb), CAST({emptyArray} AS jsonb), CAST({emptyArray} AS jsonb), CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, {rowVersion})
            ON CONFLICT (user_id) DO NOTHING
            """);

        seedLogger.LogInformation("匿名ユーザープロファイルを作成しました");
    }
}

await app.RunAsync();
