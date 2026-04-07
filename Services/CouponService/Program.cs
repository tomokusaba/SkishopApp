using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Confluent.Kafka;
using CouponService.BackgroundServices;
using CouponService.Configurations;
using CouponService.Consumers;
using CouponService.Endpoints;
using CouponService.Exceptions;
using CouponService.Infrastructure.Middleware;
using CouponService.Infrastructure.Persistence;
using CouponService.Repositories;
using CouponService.Repositories.Interfaces;
using CouponService.Services;
using CouponService.Services.Interfaces;
using CouponService.Validators;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Formatting.Compact;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ──
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "CouponService")
        .WriteTo.Console(new CompactJsonFormatter()));

// ── EF Core (PostgreSQL) ──
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("接続文字列が設定されていません");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddSingleton(TimeProvider.System);

// ── IOptions<CouponSettings> ──
builder.Services.AddOptions<CouponSettings>()
    .Bind(builder.Configuration.GetSection("Coupon"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ── IOptions<KafkaSettings> ──
builder.Services.AddOptions<KafkaSettings>()
    .Bind(builder.Configuration.GetSection("Kafka"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ── 認証・認可 ──
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("JWT 署名鍵が設定されていません");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "https://skishop.local";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "skishop-api";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSigningKey)),
            ClockSkew = TimeSpan.FromMinutes(5),
            RequireSignedTokens = true,
            NameClaimType = "sub",
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin", "ADMIN"));
    options.AddPolicy("UserOrAdmin", p => p.RequireRole("User", "Admin", "USER", "ADMIN"));
    options.AddPolicy("InternalServiceOnly", p =>
    {
        p.RequireClaim("sub", "internal-service");
        p.RequireClaim("scope", "internal");
    });
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ── FluentValidation ──
builder.Services.AddValidatorsFromAssemblyContaining<CreateCouponRequestValidator>();

// ── OpenAPI ──
builder.Services.AddOpenApi();

// ── Redis ──
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnectionString))
{
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        var options = ConfigurationOptions.Parse(redisConnectionString);
        options.ConnectTimeout = 5000;
        options.SyncTimeout = 3000;
        options.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(options);
    });
    builder.Services.AddScoped<ICouponCacheService, CouponCacheServiceRedis>();
}
else
{
    builder.Services.AddMemoryCache();
    builder.Services.AddScoped<ICouponCacheService, CouponCacheServiceInMemory>();
}

// ── Kafka Producer ──
builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var kafkaSettings = sp.GetRequiredService<IOptions<KafkaSettings>>().Value;
    var config = new ProducerConfig
    {
        BootstrapServers = kafkaSettings.BootstrapServers,
        Acks = Acks.All,
        EnableIdempotence = true,
        MessageSendMaxRetries = 3,
        RetryBackoffMs = 100
    };
    return new ProducerBuilder<string, string>(config).Build();
});

// ── Kafka Consumer ──
builder.Services.AddSingleton<IConsumer<string, string>>(sp =>
{
    var kafkaSettings = sp.GetRequiredService<IOptions<KafkaSettings>>().Value;
    var config = new ConsumerConfig
    {
        BootstrapServers = kafkaSettings.BootstrapServers,
        GroupId = kafkaSettings.GroupId,
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = false
    };
    return new ConsumerBuilder<string, string>(config).Build();
});

// ── レート制限 ──
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("coupon-api", limiter =>
    {
        limiter.PermitLimit = 100;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("redeem-api", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── DI 登録（Repositories — Scoped） ──
builder.Services.AddScoped<ICouponRepository, CouponRepository>();
builder.Services.AddScoped<ICampaignRepository, CampaignRepository>();
builder.Services.AddScoped<ICouponTypeRepository, CouponTypeRepository>();
builder.Services.AddScoped<IUserCouponRepository, UserCouponRepository>();
builder.Services.AddScoped<ICouponUsageRepository, CouponUsageRepository>();
builder.Services.AddScoped<IOutboxEventRepository, OutboxEventRepository>();
builder.Services.AddScoped<ICouponRestrictionRepository, CouponRestrictionRepository>();

// ── DI 登録（Services — Scoped） ──
builder.Services.AddScoped<ICouponService, CouponAppService>();
builder.Services.AddScoped<ICampaignService, CampaignAppService>();
builder.Services.AddScoped<ICouponRuleEngine, CouponRuleEngine>();
builder.Services.AddScoped<IFraudDetectionService, FraudDetectionService>();
builder.Services.AddScoped<ICouponAnalyticsService, CouponAnalyticsService>();
builder.Services.AddScoped<ICouponCodeGenerator, CouponCodeGenerator>();

// ── BackgroundService ──
builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddHostedService<CouponExpirationService>();
builder.Services.AddHostedService<CampaignStatusService>();
builder.Services.AddHostedService<OrderEventConsumer>();

// ── ヘルスチェック ──
var healthChecksBuilder = builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql", tags: ["ready"]);

// Kafka ヘルスチェック追加
var kafkaBootstrapServers = builder.Configuration["Kafka:BootstrapServers"];
if (!string.IsNullOrEmpty(kafkaBootstrapServers))
{
    healthChecksBuilder.AddKafka(
        new ProducerConfig { BootstrapServers = kafkaBootstrapServers },
        name: "kafka", tags: ["ready"]);
}

if (!string.IsNullOrEmpty(redisConnectionString))
{
    healthChecksBuilder.AddRedis(redisConnectionString, name: "redis", tags: ["ready"]);
}

// ── OpenTelemetry ──
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("SkiShop.CouponService"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("SkiShop.CouponService"));

var app = builder.Build();

// ── ミドルウェアパイプライン（順序厳守） ──

// 1. 例外ハンドラー
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var error = feature?.Error;

        if (error is not CouponServiceException)
            logger.LogError(error, "Unhandled exception: {Message}", error?.Message);
        else
            logger.LogWarning("Handled exception: {ExceptionType} - {Message}",
                error.GetType().Name, error.Message);

        var problem = error switch
        {
            CouponNotFoundException e             => TypedResults.Problem(e.Message, statusCode: 404),
            CouponExpiredException e               => TypedResults.Problem(e.Message, statusCode: 422),
            CouponUsageLimitExceededException e    => TypedResults.Problem(e.Message, statusCode: 422),
            InvalidCouponException e               => TypedResults.Problem(e.Message, statusCode: 422),
            CouponAlreadyAcquiredException e       => TypedResults.Problem(e.Message, statusCode: 422),
            CampaignIssueLimitReachedException e   => TypedResults.Problem(e.Message, statusCode: 422),
            BusinessException e                    => TypedResults.Problem(e.Message, statusCode: 422),
            CouponFraudDetectedException e         => TypedResults.Problem(e.Message, statusCode: 403),
            UnauthorizedException                  => TypedResults.Problem(statusCode: 401),
            ForbiddenException                     => TypedResults.Problem(statusCode: 403),
            ConcurrencyException e                 => TypedResults.Problem(e.Message, statusCode: 409),
            _                                      => TypedResults.Problem("内部エラーが発生しました", statusCode: 500)
        };
        await problem.ExecuteAsync(context);
    });
});

// 2. セキュリティヘッダー
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

// 4. Serilog リクエストログ
app.UseSerilogRequestLogging();

// 5. 認証・認可
app.UseAuthentication();
app.UseAuthorization();

// 6. レート制限
app.UseRateLimiter();

// 7. エンドポイントマッピング
app.MapOpenApi();
app.MapCouponEndpoints();
app.MapAdminCouponEndpoints();
app.MapCampaignEndpoints();
app.MapInternalCouponEndpoints();

// 8. ヘルスチェック
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();

// --- 開発環境での自動マイグレーション ---
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

await app.RunAsync();
