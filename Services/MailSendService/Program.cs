// =============================================================================
// MailSendService — エントリポイント
// Azure Communication Services を使用したメール送信マイクロサービス。
// Kafka イベント駆動 + Outbox パターンで非同期メール配信を実現する。
//
// 主な責務:
//   - Kafka "mail-events" トピックからのイベント消費とメール送信
//   - Outbox パターンによるイベント発行のトランザクション整合性保証
//   - GDPR 準拠の PII 匿名化・同意管理
//   - 失敗メールの指数バックオフリトライ
//   - 管理者向け REST API（ログ参照・テスト送信・テンプレート CRUD）
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Azure.Communication.Email;
using Azure.Identity;
using FluentValidation;
using MailSendService.Configurations;
using MailSendService.Infrastructure.Persistence;
using MailSendService.Infrastructure.Middleware;
using MailSendService.Infrastructure.Metrics;
using MailSendService.Infrastructure.Health;
using MailSendService.Repositories;
using MailSendService.Repositories.Interfaces;
using MailSendService.Services;
using MailSendService.Services.Interfaces;
using MailSendService.Validators;
using MailSendService.Endpoints;
using MailSendService.Consumers;
using MailSendService.Exceptions;
using Confluent.Kafka;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Polly;
using Serilog;
using Serilog.Formatting.Compact;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// --- DI: Serilog 構造化ログ ---
// CompactJsonFormatter で JSON 形式のログを出力し、ログ集約基盤との統合を容易にする。
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "MailSendService")
        .WriteTo.Console(new CompactJsonFormatter()));

// --- DI: 基盤サービス登録 ---
// TimeProvider: テスト時に時刻を固定可能にする抽象化。
builder.Services.AddSingleton(TimeProvider.System);

// --- DI: OpenAPI ドキュメント生成（.NET 10 方式） ---
// P0-2: AddOpenApi() + MapOpenApi() によりサービスレベルで OpenAPI ドキュメントを自動生成。
builder.Services.AddOpenApi();

// --- DI: EF Core + PostgreSQL ---
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- DI: Repository 登録（Scoped — リクエスト単位） ---
builder.Services.AddScoped<IMailLogRepository, MailLogRepository>();
builder.Services.AddScoped<IMailTemplateRepository, MailTemplateRepository>();
builder.Services.AddScoped<IMailSuppressionRepository, MailSuppressionRepository>();
builder.Services.AddScoped<IOutboxEventRepository, OutboxEventRepository>();

// --- DI: Service 登録（Scoped — リクエスト単位） ---
builder.Services.AddScoped<IMailService, MailService>();
builder.Services.AddScoped<IMailEventResolver, MailEventResolver>();
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<IAzureEmailSender, AzureEmailSender>();
builder.Services.AddScoped<ISendGridDsrService, SendGridDsrService>();  // P1-1: GDPR DSR 対応
builder.Services.AddScoped<IGdprComplianceService, GdprComplianceService>();  // P2-2: GDPR 責務分割
builder.Services.AddScoped<IMailStatsService, MailStatsService>();  // P2-2: 統計サービス責務分割

// --- DI: HttpClient + Resilience（UserInfoResolver 用） ---
// IHttpClientFactory + Polly による指数バックオフリトライ・サーキットブレーカー・タイムアウトを適用。
// サービス間認証用の API キーをヘッダーに追加（P0-10）。
builder.Services.AddHttpClient<IUserInfoResolver, UserInfoResolver>((sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<UserManagementSettings>>().Value;
    client.BaseAddress = new Uri(settings.Url);
    client.DefaultRequestHeaders.Add("X-Api-Key", settings.ApiKey);
})
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.Delay = TimeSpan.FromMilliseconds(500);

    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.MinimumThroughput = 10;

    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
});

// --- DI: HttpClient + Resilience（SendGrid DSR 用）---
// P0-5: Named HttpClient "SendGrid" を登録。
builder.Services.AddHttpClient("SendGrid", (sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<SendGridSettings>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {settings.ApiKey}");
})
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.Delay = TimeSpan.FromMilliseconds(500);

    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(10);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.MinimumThroughput = 10;

    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
});

// --- DI: 設定バインド（ValidateOnStart で起動時検証） ---
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<KafkaSettings>()
    .Bind(builder.Configuration.GetSection("Kafka"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<MailSettings>()
    .Bind(builder.Configuration.GetSection("Mail"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AzureEmailSettings>()
    .Bind(builder.Configuration.GetSection("Azure:Communication"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<SendGridSettings>()
    .Bind(builder.Configuration.GetSection("SendGrid"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<UserManagementSettings>()
    .Bind(builder.Configuration.GetSection("Services:UserManagement"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// --- DI: バリデーション（FluentValidation） ---
builder.Services.AddValidatorsFromAssemblyContaining<TestMailRequestValidator>();

// --- DI: Redis 分散キャッシュ（送信抑制チェックに使用） ---
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "MailSendService:";
});

// --- DI: OpenTelemetry メトリクス ---
builder.Services.AddSingleton<MailMetrics>();

// OpenTelemetry: 分散トレーシング（ASP.NET Core・HttpClient）+ カスタムメトリクス。
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("MailSendService.*"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("MailSendService.Metrics"));

// --- DI: ヘルスチェック（PostgreSQL・Redis・Kafka） ---
var kafkaBootstrapServers = builder.Configuration["Kafka:BootstrapServers"]
    ?? throw new InvalidOperationException("Kafka:BootstrapServers が設定されていません");

builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection が設定されていません"),
        name: "postgresql", tags: ["ready"])
    .AddRedis(
        builder.Configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("ConnectionStrings:Redis が設定されていません"),
        name: "redis", tags: ["ready"])
    .AddCheck("kafka",
        new KafkaHealthCheck(kafkaBootstrapServers), tags: ["ready"]);

// --- DI: 認証・認可（JWT Bearer + ロールベースポリシー） ---
// FallbackPolicy で全エンドポイントに認証必須を設定し、AllowAnonymous で明示的に除外する。
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT settings not configured");

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
                System.Text.Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            ClockSkew = TimeSpan.FromMinutes(5),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin", "ADMIN"));
    options.AddPolicy("AdminOrManager", p => p.RequireRole("Admin", "ADMIN", "Manager", "MANAGER"));
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// --- DI: レート制限（テストメール送信用 — IP アドレス単位、1 時間 5 回） ---
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("test-mail", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0
            }));
});

// --- DI: Azure Communication Services EmailClient ---
// 開発環境では接続文字列、本番環境では DefaultAzureCredential + エンドポイントで認証する。
builder.Services.AddSingleton(sp =>
{
    var emailClientOptions = new EmailClientOptions
    {
        Retry =
        {
            MaxRetries = 3,
            Delay = TimeSpan.FromMilliseconds(500),
            Mode = Azure.Core.RetryMode.Exponential
        }
    };

    var endpoint = builder.Configuration["Azure:Communication:Endpoint"];
    if (builder.Environment.IsDevelopment())
    {
        var connectionString = builder.Configuration["Azure:Communication:ConnectionString"];
        if (!string.IsNullOrEmpty(connectionString))
            return new EmailClient(connectionString, emailClientOptions);
    }
    return new EmailClient(new Uri(endpoint ?? "https://localhost"), new DefaultAzureCredential(), emailClientOptions);
});

// --- DI: Kafka Consumer / Producer ---
// Consumer: AutoCommit 無効化し、明示的コミットで at-least-once デリバリーを保証。
// Producer: Acks=All で全レプリカへの書き込みを保証。
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

builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var kafkaSettings = sp.GetRequiredService<IOptions<KafkaSettings>>().Value;
    var config = new ProducerConfig
    {
        BootstrapServers = kafkaSettings.BootstrapServers,
        Acks = Acks.All
    };
    return new ProducerBuilder<string, string>(config).Build();
});

// --- DI: バックグラウンドサービス登録 ---
// MailEventConsumer:        Kafka イベント消費 → メール送信ルーティング
// MailRetryService:         失敗メールの指数バックオフリトライ
// PiiCleanupService:        GDPR 準拠の PII 定期匿名化（毎日 UTC 03:00）
// OutboxPublisher:          Outbox テーブルから Kafka へのイベント発行（動的バックオフ）
// OutboxPurgeService:       古い Outbox イベントの定期削除
// SendingOrphanRecoveryService: P1-14 SENDING 孤児化リカバリ
builder.Services.AddHostedService<MailEventConsumer>();
builder.Services.AddHostedService<MailRetryService>();
builder.Services.AddHostedService<PiiCleanupService>();
builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddHostedService<OutboxPurgeService>();
builder.Services.AddHostedService<SendingOrphanRecoveryService>();

var app = builder.Build();

// --- Kafka Producer のグレースフルシャットダウン ---
// ApplicationStopping イベントで未送信メッセージをフラッシュしてからリソースを解放する。
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
var kafkaProducer = app.Services.GetRequiredService<IProducer<string, string>>();
lifetime.ApplicationStopping.Register(() =>
{
    kafkaProducer.Flush(TimeSpan.FromSeconds(10));
    kafkaProducer.Dispose();
});

// --- グローバル例外ハンドラー ---
// MailServiceException 派生例外は Warning レベルで記録し、適切な HTTP ステータスコードにマッピング。
// その他の未処理例外は Error レベルで記録し、500 を返す。スタックトレースはクライアントに返さない。
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var exLogger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var error = feature?.Error;

        if (error is not MailServiceException)
        {
            exLogger.LogError(error, "Unhandled exception: {Message}", error?.Message);
        }
        else
        {
            exLogger.LogWarning("Handled exception: {ExceptionType} - {Message}",
                error.GetType().Name, error.Message);
        }

        var problem = error switch
        {
            TemplateNotFoundException       => TypedResults.Problem(error.Message, statusCode: 404),
            MailLogNotFoundException        => TypedResults.Problem(error.Message, statusCode: 404),
            RateLimitExceededException      => TypedResults.Problem("送信頻度制限を超過しました", statusCode: 429),
            SuppressedRecipientException    => TypedResults.Problem("配信停止済みのアドレスです", statusCode: 422),
            InvalidEmailAddressException    => TypedResults.Problem(error.Message, statusCode: 422),
            InvalidMailStatusException      => TypedResults.Problem(error.Message, statusCode: 422),
            DuplicateTemplateNameException  => TypedResults.Problem(error.Message, statusCode: 409),
            ConcurrencyException            => TypedResults.Problem(error.Message, statusCode: 409),  // P0-3: 楽観的ロック競合
            MailSendFailedException         => TypedResults.Problem("メール送信に失敗しました", statusCode: 502),
            UserInfoResolutionException     => TypedResults.Problem("ユーザー情報の取得に失敗しました", statusCode: 502),
            _                               => TypedResults.Problem("内部エラーが発生しました", statusCode: 500)
        };

        await problem.ExecuteAsync(context);
    });
});

// --- Middleware パイプライン ---
// 登録順序は ASP.NET Core のミドルウェア規約に従い厳守する。
// 1) セキュリティヘッダー → 2) Correlation ID → 3) リクエストログ → 4) 認証・認可 → 5) レート制限
app.UseHsts();
app.UseHttpsRedirection();
app.UseSecurityHeaders();
app.UseCorrelationId();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// --- エンドポイント登録 ---
app.MapMailEndpoints();

// --- OpenAPI エンドポイント（P0-2）---
app.MapOpenApi();

// --- ヘルスチェックエンドポイント ---
// /health:       Liveness（常に 200 — アプリケーションが生存しているか）
// /health/ready:  Readiness（PostgreSQL・Redis・Kafka の疎通確認）
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
