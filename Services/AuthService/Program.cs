using System.Text;
using System.Text.Json;
using AuthService.Configurations;
using AuthService.Endpoints;
using AuthService.Exceptions;
using AuthService.Infrastructure.BackgroundServices;
using AuthService.Infrastructure.Cache;
using AuthService.Infrastructure.Metrics;
using AuthService.Infrastructure.Middleware;
using AuthService.Infrastructure.Persistence;
using AuthService.Infrastructure.Security;
using AuthService.Models;
using AuthService.Repositories;
using AuthService.Repositories.Interfaces;
using AuthService.Services;
using AuthService.Services.Interfaces;
using Confluent.Kafka;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;
using StackExchange.Redis;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);

var builder = WebApplication.CreateBuilder(args);

// --- Serilog ---
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "AuthService")
        .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
        .WriteTo.Console(new CompactJsonFormatter()));

// --- TimeProvider ---
builder.Services.AddSingleton(TimeProvider.System);

// --- DbContext ---
builder.Services.AddDbContext<AuthDbContext>((sp, options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.UseNpgsql(connectionString);
    }
});

// --- IOptions<T> with ValidateOnStart ---
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<AuthSettings>()
    .Bind(builder.Configuration.GetSection("Auth"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<SessionSettings>()
    .Bind(builder.Configuration.GetSection("Session"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<RedisSettings>()
    .Bind(builder.Configuration.GetSection("Redis"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// --- OpenApi ---
builder.Services.AddOpenApi();

// --- FluentValidation ---
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// --- Repository DI (Scoped) ---
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserSessionRepository, UserSessionRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IOAuthAccountRepository, OAuthAccountRepository>();
builder.Services.AddScoped<IOAuthClientRepository, OAuthClientRepository>();
builder.Services.AddScoped<IPasswordResetRepository, PasswordResetRepository>();
builder.Services.AddScoped<IPasswordHistoryRepository, PasswordHistoryRepository>();
builder.Services.AddScoped<IMfaRepository, MfaRepository>();
builder.Services.AddScoped<ISecurityLogRepository, SecurityLogRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IOutboxEventRepository, OutboxEventRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

// --- Service DI (Scoped) ---
builder.Services.AddScoped<IPasswordHasher<User>, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<AuthService.Services.Interfaces.IAuthService, AuthServiceImpl>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IUserRegistrationService, UserRegistrationService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IMfaService, MfaService>();
builder.Services.AddScoped<IOAuthService, OAuthService>();
builder.Services.AddScoped<ISecurityService, SecurityService>();
builder.Services.AddScoped<IClientCredentialsService, ClientCredentialsService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<ITotpService, TotpService>();
builder.Services.AddScoped<IEncryptionService, AesEncryptionService>();

// --- Redis ---
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnection))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(
        ConnectionMultiplexer.Connect(redisConnection));
}
builder.Services.AddScoped<IRedisCacheService, RedisCacheService>();
builder.Services.AddScoped<ITokenBlacklistService, TokenBlacklistService>();

// --- KafkaSettings ---
builder.Services.AddOptions<KafkaSettings>()
    .Bind(builder.Configuration.GetSection("Kafka"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// --- Kafka Producer ---
builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var kafkaSettings = sp.GetRequiredService<IOptions<KafkaSettings>>().Value;
    var config = new ProducerConfig
    {
        BootstrapServers = kafkaSettings.BootstrapServers,
        Acks = Acks.All,
        EnableIdempotence = true
    };
    return new ProducerBuilder<string, string>(config).Build();
});

// --- BackgroundServices ---
builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddHostedService<UserDeletedConsumer>();
builder.Services.AddHostedService<PermissionsUpdatedConsumer>();
builder.Services.AddHostedService<TokenCleanupService>();
builder.Services.AddHostedService<FailedAttemptResetService>();
builder.Services.AddHostedService<SessionTimeoutService>();
builder.Services.AddHostedService<SecurityLogAnonymizationService>();

// --- Authentication (JWT Bearer) ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtSettings>>((options, jwtSettings) =>
    {
        var settings = jwtSettings.Value;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = settings.Issuer,
            ValidAudience = settings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(settings.SecretKey)),
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

// --- Authorization ---
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("ADMIN"));
    options.AddPolicy("ManagerOrAdmin", p => p.RequireRole("MANAGER", "ADMIN"));
    options.AddPolicy("StaffOrAbove", p => p.RequireRole("STAFF", "MANAGER", "ADMIN"));
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// --- CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length > 0)
        {
            policy.WithOrigins(origins)
                .AllowAnyMethod()
                .AllowAnyHeader();
        }
    });
});

// --- Rate Limiting ---
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("general", limiter =>
    {
        limiter.PermitLimit = 100;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

// --- OpenTelemetry ---
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("AuthService"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("AuthService.*"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("AuthService.Metrics"));
builder.Services.AddSingleton<AuthMetrics>();

// --- HealthChecks ---
var pgConn = builder.Configuration.GetConnectionString("DefaultConnection");
var healthChecksBuilder = builder.Services.AddHealthChecks();
if (!string.IsNullOrEmpty(pgConn))
{
    healthChecksBuilder.AddNpgSql(pgConn, name: "postgresql", tags: ["ready"]);
}
if (!string.IsNullOrEmpty(redisConnection))
{
    healthChecksBuilder.AddRedis(redisConnection, name: "redis", tags: ["ready"]);
}

var app = builder.Build();

// --- Middleware Pipeline (ORDER MATTERS) ---

// 1. Global Exception Handler
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var error = feature?.Error;

        if (error is not (NotFoundException or BusinessException or UnauthorizedException
            or ForbiddenException or MfaRequiredException or AccountLockedException))
            logger.LogError(error, "Unhandled exception: {Message}", error?.Message);
        else
            logger.LogWarning("Handled exception: {ExceptionType} - {Message}",
                error.GetType().Name, error.Message);

        var (statusCode, message) = error switch
        {
            NotFoundException e => (404, e.Message),
            BusinessException e => (422, e.Message),
            UnauthorizedException e => (401, e.Message),
            ForbiddenException e => (403, e.Message),
            MfaRequiredException => (202, "MFA 認証が必要です"),
            AccountLockedException e => (423, e.Message),
            ConcurrencyException e => (409, e.Message),
            _ => (500, "内部エラーが発生しました")
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        if (error is MfaRequiredException mfaEx)
        {
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://httpstatuses.com/202",
                title = "MFA Required",
                status = 202,
                detail = mfaEx.Message,
                sessionToken = mfaEx.SessionToken
            });
            return;
        }

        await context.Response.WriteAsJsonAsync(new
        {
            type = $"https://httpstatuses.com/{statusCode}",
            title = statusCode switch
            {
                404 => "Not Found",
                401 => "Unauthorized",
                403 => "Forbidden",
                409 => "Conflict",
                422 => "Unprocessable Entity",
                202 => "MFA Required",
                423 => "Locked",
                _ => "Internal Server Error"
            },
            status = statusCode,
            detail = message
        });
    });
});

// 2. Security
app.UseHsts();
app.UseHttpsRedirection();

// 3. Custom Middleware
app.UseSecurityHeaders();
app.UseCorrelationId();

// 4. Serilog Request Logging
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
    };
});

// 5. CORS
app.UseCors("AllowFrontend");

// 6. Auth
app.UseAuthentication();
app.UseAuthorization();

// 7. Rate Limiting
app.UseRateLimiter();

// 8. Endpoints
app.MapAuthEndpoints();
app.MapTokenEndpoints();
app.MapPasswordEndpoints();
app.MapMfaEndpoints();
app.MapOAuthEndpoints();
app.MapEmailVerificationEndpoints();
app.MapUserRegistrationEndpoints();

// 9. Health Checks
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();

// --- 開発環境での自動マイグレーション + OpenAPI ---
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await dbContext.Database.MigrateAsync();
}

await app.RunAsync();
