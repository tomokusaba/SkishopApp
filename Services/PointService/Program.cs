using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Confluent.Kafka;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PointService.BackgroundServices;
using PointService.Configurations;
using PointService.Consumers;
using PointService.Endpoints;
using PointService.Infrastructure.Middleware;
using PointService.Infrastructure.Persistence;
using PointService.Repositories;
using PointService.Repositories.Interfaces;
using PointService.Services;
using PointService.Services.Interfaces;
using Serilog;
using Serilog.Formatting.Compact;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ── Kestrel 設定 — gRPC (HTTP/2) + HTTP/1.1 両対応 ──
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
    options.AddServerHeader = false;
    // REST API 用: HTTP/1.1（ポート 5007）
    options.ListenAnyIP(5007);
    // gRPC 用: HTTP/2 専用（ポート 15007）— TLS なしで H2C を使用
    options.ListenAnyIP(15007, lo =>
        lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
});

// ── Serilog ──
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "PointService")
        .WriteTo.Console(new CompactJsonFormatter()));

// ── TimeProvider ──
builder.Services.AddSingleton(TimeProvider.System);

// ── 設定バインド ──
builder.Services.AddOptions<PointSettings>()
    .Bind(builder.Configuration.GetSection("Points"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<KafkaSettings>()
    .Bind(builder.Configuration.GetSection("Kafka"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ── EF Core + PostgreSQL ──
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("接続文字列 'DefaultConnection' が設定されていません");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// ── Redis ──
var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("接続文字列 'Redis' が設定されていません");
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect($"{redisConnectionString},abortConnect=false"));

// ── Kafka Producer（IOptions<KafkaSettings> 経由）──
builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KafkaSettings>>().Value;
    var config = new ProducerConfig
    {
        BootstrapServers = settings.BootstrapServers,
        Acks = Acks.All,
        EnableIdempotence = true
    };
    return new ProducerBuilder<string, string>(config).Build();
});

// ── DI: Repository ──
builder.Services.AddScoped<IPointAccountRepository, PointAccountRepository>();
builder.Services.AddScoped<IPointTransactionRepository, PointTransactionRepository>();
builder.Services.AddScoped<IPointExpiryRepository, PointExpiryRepository>();
builder.Services.AddScoped<IPointRuleRepository, PointRuleRepository>();
builder.Services.AddScoped<IPointCampaignRepository, PointCampaignRepository>();
builder.Services.AddScoped<ITierDefinitionRepository, TierDefinitionRepository>();
builder.Services.AddScoped<IOutboxEventRepository, OutboxEventRepository>();
builder.Services.AddScoped<IPointAuditLogRepository, PointAuditLogRepository>();

// ── DI: Service ──
builder.Services.AddScoped<IPointService, PointManagementService>();
builder.Services.AddScoped<IPointCalculator, PointCalculator>();
builder.Services.AddScoped<ITierService, TierService>();
builder.Services.AddScoped<IPointAnalyticsService, PointAnalyticsService>();
builder.Services.AddScoped<IPointRuleService, PointRuleService>();
builder.Services.AddScoped<IPointCampaignService, PointCampaignService>();
builder.Services.AddScoped<IExpiryService, ExpiryService>();
builder.Services.AddSingleton<IPointCacheService, PointCacheService>();

// ── FluentValidation ──
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ── BackgroundService ──
builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddHostedService<PointExpirationChecker>();
builder.Services.AddHostedService<TierRecalculationNotifier>();
builder.Services.AddHostedService<OrderEventConsumer>();
builder.Services.AddHostedService<UserRegisteredEventConsumer>();
builder.Services.AddHostedService<MemberRankEventConsumer>();
builder.Services.AddHostedService<UserDeletedEventConsumer>();
builder.Services.AddHostedService<PaymentRefundedEventConsumer>();

// ── gRPC ──
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();

// JWT署名キーの検証
if (string.IsNullOrEmpty(jwtSettings.SecretKey))
{
    throw new InvalidOperationException("JWT signature key 'Jwt:SecretKey' is not configured");
}

// ── 認証・認可 ──
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
            RequireSignedTokens = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            ClockSkew = TimeSpan.FromMinutes(5),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            NameClaimType = "sub",
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("ADMIN"));
    options.AddPolicy("InternalServiceOnly", p =>
    {
        p.RequireClaim("sub", "internal-service");
        p.RequireClaim("scope", "internal");
    });
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ── ヘルスチェック ──
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql", tags: ["ready"])
    .AddRedis(redisConnectionString, name: "redis", tags: ["ready"]);

// ── OpenTelemetry ──
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("PointService.*"))
    .WithMetrics(metrics => { });

// ── 例外ハンドラー（IExceptionHandler）──
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ── レート制限 ──
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

// ── ミドルウェアパイプライン（順序厳守）──

// 1. 例外ハンドラー（IExceptionHandler 経由）
app.UseExceptionHandler();

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
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? Guid.NewGuid().ToString();
    context.Response.Headers.Append("X-Correlation-Id", correlationId);
    using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next();
    }
});

// 4. Serilog リクエストログ
app.UseSerilogRequestLogging();

// 5. 認証・認可
app.UseAuthentication();
app.UseAuthorization();

// 6. レート制限
app.UseRateLimiter();

// ── エンドポイント ──
app.MapPointEndpoints();
app.MapTierEndpoints();
app.MapGrpcService<PointService.GrpcServices.PointGrpcService>()
    .RequireAuthorization("InternalServiceOnly");

// gRPC Reflection（開発・ステージング環境のみ）
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.MapGrpcReflectionService();
}

// ── ヘルスチェック（Liveness / Readiness 分離）──
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();

// --- 開発環境での自動マイグレーション ---
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        // マイグレーション履歴をチェック
        var migrations = await dbContext.Database.GetAppliedMigrationsAsync();
        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
        
        if (pendingMigrations.Any())
        {
            // ペンディング移行がある場合、実行
            await dbContext.Database.MigrateAsync();
        }
        else if (!migrations.Any())
        {
            // マイグレーション履歴がない場合、スキーマを直接作成
            app.Logger.LogInformation("No migrations found, creating database schema using EnsureCreatedAsync");
            await dbContext.Database.EnsureCreatedAsync();
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Database initialization failed: {Message}", ex.Message);
        // 続行（アプリケーション起動は失敗させない）
    }
}

await app.RunAsync();

public partial class Program;
