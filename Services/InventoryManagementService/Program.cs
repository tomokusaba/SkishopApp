// ──────────────────────────────────────────────────────────────
// InventoryManagementService — Program.cs
// 在庫管理マイクロサービスのエントリポイント。
// DI 登録、ミドルウェアパイプライン構築、認証・認可設定、
// レート制限、OpenTelemetry、ヘルスチェック、gRPC サービスの
// マッピングを一元管理する。
// ──────────────────────────────────────────────────────────────

using System.Threading.RateLimiting;
using Azure.Storage.Blobs;
using Confluent.Kafka;
using FluentValidation;
using InventoryManagementService.BackgroundServices;
using InventoryManagementService.Configurations;
using InventoryManagementService.Endpoints;
using InventoryManagementService.Infrastructure;
using InventoryManagementService.Infrastructure.Http;
using InventoryManagementService.Infrastructure.Metrics;
using InventoryManagementService.Infrastructure.Middleware;
using InventoryManagementService.Infrastructure.Persistence;
using InventoryManagementService.Repositories;
using InventoryManagementService.Repositories.Interfaces;
using InventoryManagementService.Services;
using InventoryManagementService.Services.Interfaces;
using InventoryManagementService.Validators;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;
using InventoryManagementService.Infrastructure.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog 構成 — 構造化ログ（JSON 形式）で集約基盤に送信 ──
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "InventoryManagementService")
        .WriteTo.Console(new CompactJsonFormatter()));

// ── 基盤サービス登録 — TimeProvider、HttpContextAccessor、CorrelationId 伝搬 ──
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CorrelationIdDelegatingHandler>();

// ── Kestrel 設定 — リクエストサイズ制限（10MB）+ Server ヘッダー非公開 ──
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
    options.AddServerHeader = false;
    // REST API 用: HTTP/1.1（ポート 5003）
    options.ListenAnyIP(5003);
    // gRPC 用: HTTP/2 専用（ポート 15003）— TLS なしで H2C を使用
    options.ListenAnyIP(15003, lo =>
        lo.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
});
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});

// ── EF Core（PostgreSQL）— AppDbContext 登録 ──
// EnableRetryOnFailure: PostgreSQL の一時的な接続障害（ネットワーク瞬断、フェイルオーバー等）に対して
// 最大 3 回・最大 5 秒間隔で自動リトライし、一時的な障害でリクエストが即失敗することを防止する。
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), npgsqlOptions =>
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null));
});

// ── Azure Blob Storage — 商品画像保存用コンテナクライアント ──
// 指数バックオフリトライ（最大 3 回、1〜10 秒）で Blob Storage の一時障害に耐える。
// 開発環境では接続文字列が未設定の場合があるため、条件付きで登録する。
var blobConnectionString = builder.Configuration.GetConnectionString("BlobStorage");
if (!string.IsNullOrEmpty(blobConnectionString))
{
    builder.Services.AddSingleton(_ =>
    {
        var blobClientOptions = new BlobClientOptions
        {
            Retry =
            {
                MaxRetries = 3,
                Delay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(10),
                Mode = Azure.Core.RetryMode.Exponential
            },
            Diagnostics =
            {
                IsLoggingEnabled = true
            }
        };
        var blobServiceClient = new BlobServiceClient(blobConnectionString, blobClientOptions);
        return blobServiceClient.GetBlobContainerClient("product-images");
    });
}
else
{
    // 開発環境では null を注入（画像アップロード機能は使用不可）
    builder.Services.AddSingleton<BlobContainerClient>(_ => null!);
}

// ── Redis Cache — 商品・カテゴリのキャッシュ ──
// InstanceName でキーにプレフィックスを付与し、他サービスとのキー衝突を防止する。
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "InventoryService:";
});

builder.Services.AddOptions<CacheConfig>()
    .Bind(builder.Configuration.GetSection("Inventory:Cache"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ── Kafka 設定 — IOptions パターンで KafkaConfig を登録 ──
builder.Services.AddOptions<KafkaConfig>()
    .Bind(builder.Configuration.GetSection("Kafka"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Kafka Producer — EnableIdempotence=true で重複メッセージ送信を防止し、
// Acks=All で全レプリカへの書き込み確認後に応答することでメッセージ消失を防ぐ。
builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var kafkaConfig = builder.Configuration.GetSection("Kafka");
    var bootstrapServers = kafkaConfig["BootstrapServers"]
        ?? throw new InvalidOperationException("Kafka:BootstrapServers が設定されていません");
    var config = new ProducerConfig
    {
        BootstrapServers = bootstrapServers,
        EnableIdempotence = true,
        Acks = Acks.All
    };
    return new ProducerBuilder<string, string>(config).Build();
});

// Kafka Consumer Factory — 各 BackgroundService が独自の Consumer グループで生成。
// AutoOffsetReset.Earliest: 初回参加時に最古のメッセージから処理しメッセージ欠損を防止。
// EnableAutoCommit=false: 処理完了後に手動コミットすることで At-Least-Once 保証を実現する。
builder.Services.AddSingleton<Func<string, IConsumer<string, string>>>(sp =>
{
    var kafkaConfig = builder.Configuration.GetSection("Kafka");
    var bootstrapServers = kafkaConfig["BootstrapServers"]
        ?? throw new InvalidOperationException("Kafka:BootstrapServers が設定されていません");
    return groupIdSuffix =>
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = (kafkaConfig["GroupId"] ?? "inventory-service") + "-" + groupIdSuffix,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };
        return new ConsumerBuilder<string, string>(config).Build();
    };
});

// ── Repository DI 登録（Scoped — リクエスト単位のライフサイクル） ──
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<IPriceRepository, PriceRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<ISizeGuideRepository, SizeGuideRepository>();
builder.Services.AddScoped<IImageRepository, ImageRepository>();
// H-5, H-6: Outbox / ProcessedMessage リポジトリ追加
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<IProcessedMessageRepository, ProcessedMessageRepository>();

// ── Service DI 登録（Scoped — リクエスト単位のライフサイクル） ──
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IPriceService, PriceService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<ISizeGuideService, SizeGuideService>();
builder.Services.AddScoped<IEventPublisherService, EventPublisherService>();
builder.Services.AddScoped<IMessageDeduplicationService, MessageDeduplicationService>();
// H-14: ResilientCacheService 登録
builder.Services.AddScoped<IResilientCacheService, ResilientCacheService>();

// M-6: InventoryMetrics 登録（カスタムビジネスメトリクス）
builder.Services.AddSingleton<InventoryMetrics>();

// FluentValidation — アセンブリスキャンで全バリデーターを自動登録
builder.Services.AddValidatorsFromAssemblyContaining<ProductCreateRequestValidator>();

// ── BackgroundService 登録 — Outbox 発行、Kafka コンシューマー、定期クリーンアップ ──
builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddHostedService<OrderCreatedConsumer>();
builder.Services.AddHostedService<OrderCompletedConsumer>();
builder.Services.AddHostedService<OrderCancelledConsumer>();
builder.Services.AddHostedService<InventoryReservationCleanupService>();
builder.Services.AddHostedService<CacheWarmupService>();
builder.Services.AddHostedService<UserDeletedConsumer>();

// ── JWT 設定 — IOptions パターンで JwtSettings を登録（起動時バリデーション付き） ──
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ── JWT Bearer 認証 — トークン検証パラメータの構成 ──
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();

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
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = string.IsNullOrEmpty(jwtSettings.SecretKey)
                ? null
                : new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            // ClockSkew: 分散環境でのサーバー間時刻差を最大 5 分まで許容し、
            // トークン有効期限の判定ずれによる誤拒否を防止する。
            ClockSkew = TimeSpan.FromMinutes(5),
            NameClaimType = "sub",
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
    });

// ── 認可ポリシー — AdminOnly / UserOrAdmin / InternalServiceOnly + FallbackPolicy（認証必須） ──
// 注意: JWT の role クレームは大文字（"ADMIN", "USER"）で発行されるため、両方を許可する
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

// ── CORS 設定 — IOptions パターンで許可オリジンを構成 ──
builder.Services.AddOptions<CorsConfig>()
    .Bind(builder.Configuration.GetSection("Cors"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var corsConfig = builder.Configuration.GetSection("Cors").Get<CorsConfig>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(corsConfig?.AllowedOrigins ?? ["https://skishop.example.com"])
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// ── レート制限 — default: 100req/分、search: 30req/分 ──
// default: 一般 API に対する DoS 防止。QueueLimit=0 で超過時に即座に 429 を返す（キューイングなし）。
// search: 検索 API は DB 負荷が高いため、一般 API より厳格な制限（30 req/分）を適用する。
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("default", limiter =>
    {
        limiter.PermitLimit = 100;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("search", limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

// ── グローバル例外ハンドラー — GlobalExceptionHandler を登録 ──
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ── OpenTelemetry — 分散トレーシング + メトリクス（ASP.NET Core / HttpClient / Runtime） ──
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("SkiShop.InventoryManagementService"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation());

// ── ヘルスチェック — PostgreSQL / Redis / Kafka の疎通確認 ──
var pgConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? string.Empty;
var kafkaBootstrapServers = builder.Configuration.GetSection("Kafka")["BootstrapServers"]
    ?? throw new InvalidOperationException("Kafka:BootstrapServers が設定されていません");
builder.Services.AddHealthChecks()
    .AddNpgSql(pgConnectionString, name: "postgresql", tags: ["ready"])
    .AddRedis(redisConnectionString, name: "redis", tags: ["ready"])
    .AddKafka(new ProducerConfig
    {
        BootstrapServers = kafkaBootstrapServers
    }, name: "kafka", tags: ["ready"]);

builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

// H-11: .NET 10 OpenAPI ドキュメント生成（カスタムトランスフォーマー使用）
// B-3 修正: AsParameters との互換性問題を解決するためにスキーマ変換器を追加
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<DocumentInfoTransformer>();
    options.AddSchemaTransformer<ParameterSchemaTransformer>();
});

var app = builder.Build();

// ── ミドルウェアパイプライン（登録順序が動作に直結 — 変更禁止） ──
// 1. CorrelationId    — 全リクエストに相関IDを付与（最初に配置）
// 2. ExceptionHandler — 全例外をキャッチして ProblemDetails に変換
// 3. HSTS             — HTTP Strict Transport Security ヘッダー付与
// 4. HttpsRedirection — HTTP → HTTPS リダイレクト
// 5. SecurityHeaders  — セキュリティ関連ヘッダー付与
// 6. SerilogRequestLogging — リクエストログ記録
// 7. CORS             — 認証より前に配置（プリフライトリクエスト対応）
// 8. Authentication   — JWT トークン検証
// 9. Authorization    — 認可ポリシー適用（Authentication の後に配置必須）
// 10. RateLimiter     — レート制限（認証後に配置しユーザー単位制限を実現）
app.UseCorrelationId();
app.UseExceptionHandler();
app.UseHsts();
app.UseHttpsRedirection();
app.UseSecurityHeaders();
app.UseSerilogRequestLogging();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// ── Endpoint マッピング — 各ドメインのエンドポイントを登録 ──
app.MapProductEndpoints();
app.MapCategoryEndpoints();
app.MapInventoryEndpoints();
app.MapPriceEndpoints();
app.MapReviewEndpoints();
app.MapSizeGuideEndpoints();

// H-11: OpenAPI エンドポイント公開（/openapi/v1.json）— 開発ツール用に匿名アクセス許可
// B-3 修正: .NET 10 推奨の Scalar UI を追加
app.MapOpenApi().AllowAnonymous();
app.MapScalarApiReference(options =>
{
    options.WithTitle("InventoryManagementService API")
           .WithTheme(ScalarTheme.Default)
           .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
}).AllowAnonymous();

// ── gRPC サービスマッピング — 内部サービス間通信専用（InternalServiceOnly 認可必須） ──
app.MapGrpcService<InventoryManagementService.GrpcServices.InventoryGrpcService>()
    .RequireAuthorization("InternalServiceOnly");

// gRPC Reflection（開発・ステージング環境のみ）
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.MapGrpcReflectionService();
}

// ── ヘルスチェックエンドポイント — Liveness（/health）+ Readiness（/health/ready） ──
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false })
    .AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();

// --- 開発環境での自動マイグレーション ---
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // マイグレーションがない場合は EnsureCreated でスキーマを作成
    await dbContext.Database.EnsureCreatedAsync();
}

await app.RunAsync();
