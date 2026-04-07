using System.Text;
using System.Threading.RateLimiting;
using Confluent.Kafka;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;
using StackExchange.Redis;
using UserManagementService.BackgroundServices;
using UserManagementService.Configurations;
using UserManagementService.Endpoints;
using UserManagementService.Exceptions;
using UserManagementService.Infrastructure.HealthChecks;
using UserManagementService.Infrastructure.Middleware;
using UserManagementService.Infrastructure.Persistence;
using UserManagementService.Repositories;
using UserManagementService.Repositories.Interfaces;
using UserManagementService.Services;
using UserManagementService.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ──
// 構造化ログを CompactJsonFormatter で出力。ServiceName プロパティで識別する。
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "UserManagementService")
        .WriteTo.Console(new CompactJsonFormatter()));

// TimeProvider（テスタビリティ確保）
builder.Services.AddSingleton(TimeProvider.System);

// ── EF Core DbContext ──
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? string.Empty;
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? new JwtSettings();
if (string.IsNullOrWhiteSpace(jwtSettings.Issuer)
    || string.IsNullOrWhiteSpace(jwtSettings.Audience)
    || string.IsNullOrWhiteSpace(jwtSettings.SigningKey))
{
    if (builder.Environment.IsEnvironment("Testing"))
    {
        jwtSettings = new JwtSettings
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningKey = "test-signing-key-with-32-chars-min!"
        };
    }
    else
    {
        throw new InvalidOperationException("Jwt settings are not configured.");
    }
}

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey));

// ── JWT 認証 ──
// AuthService と同一の SigningKey で検証する。HMAC-SHA256 のみ許可。
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
            IssuerSigningKey = signingKey,
            RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

// ── 認可ポリシー ──
// FallbackPolicy で全エンドポイントを認証必須にし、AllowAnonymous で明示的に除外する。
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin", "ADMIN"));
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(
            builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .AllowAnyMethod()
            .AllowAnyHeader());
});

// ── Kafka Settings ──
// IOptions<T> + ValidateOnStart で起動時にバリデーションエラーを検出する。
builder.Services.AddOptions<KafkaSettings>()
    .Bind(builder.Configuration.GetSection("Kafka"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<DataExportSettings>()
    .Bind(builder.Configuration.GetSection("DataExport"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ── レート制限 ──
// anonymous-consent は匿名アクセス用（10 req/min）、api は認証後（300 req/min）。
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("anonymous-consent", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });

    options.AddFixedWindowLimiter("api", limiter =>
    {
        limiter.PermitLimit = 300;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});

// ── Kafka Producer ──
// べき等プロデューサー（Acks.All + EnableIdempotence）で Outbox イベントを発行する。
var kafkaSettings = builder.Configuration.GetSection("Kafka").Get<KafkaSettings>()
    ?? new KafkaSettings();
builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var config = new ProducerConfig
    {
        BootstrapServers = kafkaSettings.BootstrapServers,
        Acks = Acks.All,
        EnableIdempotence = true
    };
    return new ProducerBuilder<string, string>(config).Build();
});

// ── Kafka Consumer Factory ──
// 各 BackgroundService が個別の ConsumerGroup で Consumer を生成する。
builder.Services.AddSingleton<IKafkaConsumerFactory, KafkaConsumerFactory>();

// Redis
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? string.Empty;
if (!string.IsNullOrEmpty(redisConnectionString))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(
        ConnectionMultiplexer.Connect(redisConnectionString));
}
else
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
        ConnectionMultiplexer.Connect(new ConfigurationOptions { AbortOnConnectFail = false }));
}
builder.Services.AddScoped<ICacheService, CacheService>();

// ── Repository DI ──
// 全 Repository を Scoped で登録（DbContext のライフサイクルと一致）。
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAddressRepository, AddressRepository>();
builder.Services.AddScoped<IWishlistRepository, WishlistRepository>();
builder.Services.AddScoped<IPreferenceRepository, PreferenceRepository>();
builder.Services.AddScoped<IActivityRepository, ActivityRepository>();
builder.Services.AddScoped<IConsentRepository, ConsentRepository>();
builder.Services.AddScoped<IDeletionRequestRepository, DeletionRequestRepository>();
builder.Services.AddScoped<IMemberRankRepository, MemberRankRepository>();
builder.Services.AddScoped<IOutboxEventRepository, OutboxEventRepository>();
builder.Services.AddScoped<IDataExportRepository, DataExportRepository>();
builder.Services.AddScoped<IProcessedEventRepository, ProcessedEventRepository>();

// ── Service DI ──
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IWishlistService, WishlistService>();
builder.Services.AddScoped<IPreferenceService, PreferenceService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddScoped<IConsentService, ConsentService>();
builder.Services.AddScoped<IDsrService, DsrService>();
builder.Services.AddScoped<IMemberRankService, MemberRankService>();
builder.Services.AddScoped<IEventPublisherService, EventPublisherService>();

// ── BackgroundServices ──
// Outbox Publisher + 5 Kafka Consumer + 4 定期バッチを登録する。
builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddHostedService<UserRegisteredConsumer>();
builder.Services.AddHostedService<UserDeletionCompletedConsumer>();
builder.Services.AddHostedService<OrderConfirmedConsumer>();
builder.Services.AddHostedService<InventoryStockUpdatedConsumer>();
builder.Services.AddHostedService<PasswordChangedConsumer>();
builder.Services.AddHostedService<DeletionRequestProcessor>();
builder.Services.AddHostedService<MemberRankEvaluationService>();
builder.Services.AddHostedService<DsrTimeoutMonitorService>();
builder.Services.AddHostedService<DataExportService>();

// ── FluentValidation ──
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ── OpenTelemetry ──
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("SkiShop.UserManagement.*"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation());

// ── HealthChecks ──
var healthChecks = builder.Services.AddHealthChecks();

if (!string.IsNullOrEmpty(connectionString))
{
    healthChecks.AddNpgSql(connectionString, name: "postgresql", tags: ["ready"]);
}

var redisHealthConnectionString = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisHealthConnectionString))
{
    healthChecks.AddRedis(redisHealthConnectionString, name: "redis", tags: ["ready"]);
}

if (!string.IsNullOrWhiteSpace(kafkaSettings.BootstrapServers))
{
    healthChecks.AddCheck<KafkaHealthCheck>("kafka", tags: ["ready"]);
}

// ── OpenAPI ──
builder.Services.AddOpenApi();

var app = builder.Build();

// ── ミドルウェアパイプライン ──
// 登録順序は AGENTS.md §11.3 に準拠する。変更禁止。

// 1. グローバル例外ハンドラー（最外層）
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var exLogger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var error = feature?.Error;

        if (error is not (NotFoundException or BusinessException
            or UnauthorizedException or ForbiddenException))
            exLogger.LogError(error, "Unhandled exception: {Message}", error?.Message);
        else
            exLogger.LogWarning("Handled exception: {ExceptionType} - {Message}",
                error.GetType().Name, error.Message);

        var problem = error switch
        {
            NotFoundException e    => TypedResults.Problem(e.Message, statusCode: 404),
            BusinessException e    => TypedResults.Problem(e.Message, statusCode: 422),
            UnauthorizedException  => TypedResults.Problem(statusCode: 401),
            ForbiddenException     => TypedResults.Problem(statusCode: 403),
            ConcurrencyException e => TypedResults.Problem(e.Message, statusCode: 409),
            _                      => TypedResults.Problem("内部エラーが発生しました", statusCode: 500)
        };
        await problem.ExecuteAsync(context);
    });
});

// 2. セキュリティ
app.UseHsts();
app.UseHttpsRedirection();

// 3. セキュリティヘッダー
app.UseMiddleware<SecurityHeadersMiddleware>();

// 4. Correlation ID
app.UseMiddleware<CorrelationIdMiddleware>();

// 5. Serilog リクエストログ
app.UseSerilogRequestLogging();

// 6. CORS（認証より前）
app.UseCors("AllowFrontend");

// 7. 認証・認可
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// OpenAPI
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

// ── エンドポイント登録 ──
app.MapUserEndpoints();
app.MapAddressEndpoints();
app.MapWishlistEndpoints();
app.MapPreferenceEndpoints();
app.MapActivityEndpoints();
app.MapConsentEndpoints();
app.MapDsrEndpoints();
app.MapMemberRankEndpoints();
app.MapAdminUserEndpoints();

// ── ヘルスチェック ──
// Liveness: アプリケーション生存確認
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

// Readiness: PostgreSQL 疎通確認
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
