using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using SalesManagementService.Configurations;
using SalesManagementService.Endpoints;
using SalesManagementService.Infrastructure.Caching;
using SalesManagementService.Infrastructure.Middleware;
using SalesManagementService.Infrastructure.Persistence;
using SalesManagementService.Infrastructure.Security;
using SalesManagementService.Infrastructure.Telemetry;
using Serilog;
using Serilog.Formatting.Compact;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ──
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "SalesManagementService")
        .WriteTo.Console(new CompactJsonFormatter()));

// ── TimeProvider ──
builder.Services.AddSingleton(TimeProvider.System);

// ── IOptions<T> 設定バインド ──
builder.Services.AddOptions<OrderSettings>()
    .Bind(builder.Configuration.GetSection("App:Order"))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<ShippingSettings>()
    .Bind(builder.Configuration.GetSection("App:Shipping"))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<ReturnSettings>()
    .Bind(builder.Configuration.GetSection("App:Return"))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<SagaSettings>()
    .Bind(builder.Configuration.GetSection("App:Saga"))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<OutboxSettings>()
    .Bind(builder.Configuration.GetSection("App:Outbox"))
    .ValidateDataAnnotations().ValidateOnStart();

// ── EF Core ──
builder.Services.AddDbContext<SalesManagementService.Infrastructure.Persistence.SalesDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("salesdb")));

// ── Repository DI 登録 ──
builder.Services.AddScoped<SalesManagementService.Repositories.Interfaces.IOrderRepository, SalesManagementService.Repositories.OrderRepository>();
builder.Services.AddScoped<SalesManagementService.Repositories.Interfaces.IShipmentRepository, SalesManagementService.Repositories.ShipmentRepository>();
builder.Services.AddScoped<SalesManagementService.Repositories.Interfaces.IReturnRepository, SalesManagementService.Repositories.ReturnRepository>();
builder.Services.AddScoped<SalesManagementService.Repositories.Interfaces.ISagaLogRepository, SalesManagementService.Repositories.SagaLogRepository>();
builder.Services.AddScoped<SalesManagementService.Repositories.Interfaces.IOutboxEventRepository, SalesManagementService.Repositories.OutboxEventRepository>();
builder.Services.AddScoped<SalesManagementService.Repositories.Interfaces.IIdempotencyKeyRepository, SalesManagementService.Repositories.IdempotencyKeyRepository>();
builder.Services.AddScoped<SalesManagementService.Repositories.Interfaces.IReportQueryRepository, SalesManagementService.Repositories.ReportQueryRepository>();

// ── Service DI 登録 ──
builder.Services.AddScoped<SalesManagementService.Services.Interfaces.IOrderService, SalesManagementService.Services.OrderService>();
builder.Services.AddScoped<SalesManagementService.Services.Interfaces.IShipmentService, SalesManagementService.Services.ShipmentService>();
builder.Services.AddScoped<SalesManagementService.Services.Interfaces.IReturnService, SalesManagementService.Services.ReturnService>();
builder.Services.AddScoped<SalesManagementService.Services.Interfaces.IReportService, SalesManagementService.Services.ReportService>();
builder.Services.AddScoped<SalesManagementService.Services.OrderNumberGenerator>();
builder.Services.AddScoped<SalesManagementService.Services.ShippingFeeCalculator>();

// ── FluentValidation ──
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ── Kafka Producer ──
builder.Services.AddSingleton<Confluent.Kafka.IProducer<string, string>>(sp =>
{
    var config = new Confluent.Kafka.ProducerConfig
    {
        BootstrapServers = builder.Configuration["Kafka:BootstrapServers"],
        Acks = Confluent.Kafka.Acks.All,
        EnableIdempotence = true
    };
    return new Confluent.Kafka.ProducerBuilder<string, string>(config).Build();
});

// ── Outbox ──
builder.Services.AddScoped<SalesManagementService.Services.Interfaces.IOutboxWriter, SalesManagementService.Infrastructure.Outbox.OutboxWriter>();
builder.Services.AddHostedService<SalesManagementService.Infrastructure.Outbox.OutboxPublisher>();
builder.Services.AddHostedService<SalesManagementService.Infrastructure.Outbox.OutboxCleanupService>();

// ── Kafka Consumers ──
builder.Services.AddHostedService<SalesManagementService.Infrastructure.Kafka.PaymentCompletedConsumer>();
builder.Services.AddHostedService<SalesManagementService.Infrastructure.Kafka.PaymentFailedConsumer>();
builder.Services.AddHostedService<SalesManagementService.Infrastructure.Kafka.InventoryReservedConsumer>();
builder.Services.AddHostedService<SalesManagementService.Infrastructure.Kafka.InventoryReleasedConsumer>();
builder.Services.AddHostedService<SalesManagementService.Infrastructure.Kafka.UserDeletedConsumer>();

// ── メンテナンス BackgroundService ──
builder.Services.AddHostedService<SalesManagementService.Infrastructure.Maintenance.IdempotencyKeyCleanupService>();
builder.Services.AddHostedService<SalesManagementService.Infrastructure.Maintenance.SagaLogArchivalService>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<SalesManagementService.Infrastructure.ExternalServices.IInventoryClient, SalesManagementService.Infrastructure.ExternalServices.DevelopmentInventoryClient>();
    builder.Services.AddScoped<SalesManagementService.Infrastructure.ExternalServices.ICartClient, SalesManagementService.Infrastructure.ExternalServices.DevelopmentCartClient>();
    builder.Services.AddScoped<SalesManagementService.Infrastructure.ExternalServices.ICouponClient, SalesManagementService.Infrastructure.ExternalServices.DevelopmentCouponClient>();
    builder.Services.AddScoped<SalesManagementService.Infrastructure.ExternalServices.IPointClient, SalesManagementService.Infrastructure.ExternalServices.DevelopmentPointClient>();
    builder.Services.AddScoped<SalesManagementService.Infrastructure.ExternalServices.IPaymentClient, SalesManagementService.Infrastructure.ExternalServices.DevelopmentPaymentClient>();
}
else
{
    // gRPC チャネルの登録
    var inventoryGrpcAddress = builder.Configuration["GrpcEndpoints:Inventory"] ?? "http://localhost:15003";
    var paymentGrpcAddress = builder.Configuration["GrpcEndpoints:Payment"] ?? "http://localhost:15005";
    var cartGrpcAddress = builder.Configuration["GrpcEndpoints:Cart"] ?? "http://localhost:15005";
    var pointGrpcAddress = builder.Configuration["GrpcEndpoints:Point"] ?? "http://localhost:15007";

    builder.Services.AddSingleton(sp =>
        new InventoryManagementService.Protos.InventoryService.InventoryServiceClient(
            Grpc.Net.Client.GrpcChannel.ForAddress(inventoryGrpcAddress)));
    builder.Services.AddSingleton(sp =>
        new SkiShop.Contracts.Payment.V1.PaymentGrpcService.PaymentGrpcServiceClient(
            Grpc.Net.Client.GrpcChannel.ForAddress(paymentGrpcAddress)));
    builder.Services.AddSingleton(sp =>
        new SkiShop.Contracts.Cart.V1.CartGrpcService.CartGrpcServiceClient(
            Grpc.Net.Client.GrpcChannel.ForAddress(cartGrpcAddress)));
    builder.Services.AddSingleton(sp =>
        new PointService.Protos.PointGrpc.PointGrpcClient(
            Grpc.Net.Client.GrpcChannel.ForAddress(pointGrpcAddress)));

    builder.Services.AddScoped<SalesManagementService.Infrastructure.ExternalServices.IInventoryClient, SalesManagementService.Infrastructure.ExternalServices.GrpcInventoryClient>();
    builder.Services.AddScoped<SalesManagementService.Infrastructure.ExternalServices.ICartClient, SalesManagementService.Infrastructure.ExternalServices.GrpcCartClient>();
    builder.Services.AddScoped<SalesManagementService.Infrastructure.ExternalServices.ICouponClient, SalesManagementService.Infrastructure.ExternalServices.DevelopmentCouponClient>();
    builder.Services.AddScoped<SalesManagementService.Infrastructure.ExternalServices.IPointClient, SalesManagementService.Infrastructure.ExternalServices.GrpcPointClient>();
    builder.Services.AddScoped<SalesManagementService.Infrastructure.ExternalServices.IPaymentClient, SalesManagementService.Infrastructure.ExternalServices.GrpcPaymentClient>();
}

// ── Saga Coordinators ──
builder.Services.AddScoped<SalesManagementService.Infrastructure.Saga.ISagaCoordinator, SalesManagementService.Infrastructure.Saga.SagaCoordinator>();
builder.Services.AddScoped<SalesManagementService.Services.Interfaces.IOrderCheckoutService, SalesManagementService.Infrastructure.Saga.SagaCoordinator>();
builder.Services.AddScoped<SalesManagementService.Services.Interfaces.IOrderCancellationService, SalesManagementService.Infrastructure.Saga.CancelSagaCoordinator>();
builder.Services.AddScoped<SalesManagementService.Infrastructure.Saga.CancelSagaCoordinator>();
builder.Services.AddScoped<SalesManagementService.Infrastructure.Saga.ReturnSagaCoordinator>();
builder.Services.AddHostedService<SalesManagementService.Infrastructure.Saga.SagaRecoveryService>();

// ── Authentication & Authorization ──
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]
                    ?? throw new InvalidOperationException("JWT SecretKey is not configured"))),
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("UserOrAdmin", p => p.RequireRole("User", "Admin"));
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ── Global Exception Handler ──
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ── Rate Limiting ──
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("order-create", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("admin-api", limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

// ── CORS ──
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins("https://skishop.example.com")
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// ── OpenTelemetry ──
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("SkiShop.SalesManagement"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(SalesMetrics.MeterName)
        .AddMeter(SalesMetrics.MeterName + ".Observable"));

// ── Custom Metrics ──
builder.Services.AddSingleton<SalesMetrics>();
builder.Services.AddSingleton<SalesObservableMetrics>();

// ── Redis ──
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(
        builder.Configuration.GetConnectionString("redis") ?? "localhost"));
builder.Services.AddScoped<OrderCacheService>();

// ── Health Checks ──
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("salesdb") ?? "",
        name: "salesdb-postgresql",
        tags: ["ready"])
    .AddRedis(
        builder.Configuration.GetConnectionString("redis") ?? "localhost",
        name: "redis",
        tags: ["ready"])
    .AddKafka(
        new Confluent.Kafka.ProducerConfig
        {
            BootstrapServers = builder.Configuration["Kafka:BootstrapServers"]
                ?? throw new InvalidOperationException("Kafka:BootstrapServers が設定されていません")
        },
        name: "kafka",
        tags: ["ready"]);

var app = builder.Build();

// ── Middleware Pipeline (順序厳守) ──

// 1. 例外ハンドラー（最外側）
app.UseExceptionHandler();

// 2. セキュリティヘッダー
app.UseSecurityHeaders();
app.UseHsts();
app.UseHttpsRedirection();

// 3. Correlation ID
app.UseCorrelationId();

// 4. Serilog リクエストログ
app.UseSerilogRequestLogging();

// 5. CORS（認証より前）
app.UseCors("AllowFrontend");

// 6. 認証・認可（順序厳守）
app.UseAuthentication();
app.UseAuthorization();

// 7. レート制限（認証後）
app.UseRateLimiter();

// ── Health Checks ──
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();

// ── エンドポイントマッピング ──
app.MapOrderEndpoints();
app.MapShipmentEndpoints();
app.MapReturnEndpoints();
app.MapReportEndpoints();

// --- 開発環境での自動マイグレーション ---
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
    await dbContext.Database.MigrateAsync();
}

await app.RunAsync();

// WebApplicationFactory テスト用にクラスを公開
public partial class Program;
