// ─────────────────────────────────────────────────────────────
// ApiGateway — Program.cs
// SkiShop API Gateway エントリポイント
//
// YARP リバースプロキシを中心に、認証・認可・レート制限・
// セキュリティヘッダー・可観測性・ヘルスチェックを統合する。
// ミドルウェアパイプラインの登録順序は ASP.NET Core の仕様に
// 従い厳密に制御する（詳細は設計書 §9 参照）。
// ─────────────────────────────────────────────────────────────

using System.Diagnostics;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;
using Yarp.ReverseProxy.Transforms;
using ApiGateway.Configurations;
using ApiGateway.Infrastructure.HealthChecks;
using ApiGateway.Infrastructure.Logging;
using ApiGateway.Infrastructure.Messaging;
using ApiGateway.Infrastructure.Metrics;
using ApiGateway.Infrastructure.Middleware;
using ApiGateway.Infrastructure.Resilience;

var builder = WebApplication.CreateBuilder(args);

// Serilog 構造化ログ（設計書 §9）+ PII マスキングエンリッチャー（C-3）
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "ApiGateway")
        .Enrich.With<PiiMaskingEnricher>()
        .WriteTo.Console(new CompactJsonFormatter()));

// Kestrel セキュリティ設定（設計書 §5）
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 5 * 1024 * 1024; // 5MB
    options.AddServerHeader = false;
});

// JWT 設定バリデーション（H-2: ValidateOnStart による起動時検証）
// Issuer / Audience の必須チェック + SigningKey or Authority の存在チェック
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>();

// Kafka 設定（C-2: AuthCacheInvalidationConsumer 用）
builder.Services.AddOptions<KafkaSettings>()
    .Bind(builder.Configuration.GetSection("Kafka"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// CORS 設定バリデーション（H-2: AllowedOrigins の起動時検証）
builder.Services.AddOptions<CorsSettings>()
    .Bind(builder.Configuration.GetSection("Cors"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ForwardedHeaders 設定（設計書 §5）— KnownProxies/KnownNetworks で信頼範囲を制限
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedPrefix;
    options.ForwardLimit = 2;
    // 本番環境では環境変数 ForwardedHeaders__KnownProxies で信頼する IP を設定
    var knownProxies = builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>();
    if (knownProxies is { Length: > 0 })
    {
        options.KnownProxies.Clear();
        foreach (var proxy in knownProxies)
        {
            if (System.Net.IPAddress.TryParse(proxy, out var ip))
                options.KnownProxies.Add(ip);
        }
    }
});

// CORS 設定（設計書 §6）
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(allowedOrigins)
              .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
              .WithHeaders("Content-Type", "Authorization", "X-Correlation-Id", "Accept-Language", "X-Request-Id")
              .SetPreflightMaxAge(TimeSpan.FromSeconds(3600)));
});

// JWT 認証設定（設計書 §11）
// 鍵の種類に応じてアルゴリズムを自動選択:
//   - SigningKey 設定時: HS256（対称鍵）で検証
//   - Authority 設定時: RS256/ES256（非対称鍵）で JWKS 自動取得
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSection = builder.Configuration.GetSection("Jwt");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            RequireSignedTokens = true,  // alg:none 攻撃を防止
            ClockSkew = TimeSpan.FromMinutes(5)
        };

        // IssuerSigningKey は環境変数 / user-secrets から取得（ハードコード禁止）
        var signingKey = jwtSection["SigningKey"];
        if (!string.IsNullOrEmpty(signingKey))
        {
            options.TokenValidationParameters.IssuerSigningKey =
                new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(signingKey));
            // SymmetricSecurityKey 使用時は HS256 を許可
            options.TokenValidationParameters.ValidAlgorithms = ["HS256"];
        }

        // Authority（JWKS エンドポイント）設定時は RS256/ES256 を使用
        var authority = jwtSection["Authority"];
        if (!string.IsNullOrEmpty(authority))
        {
            options.Authority = authority;
            options.TokenValidationParameters.ValidAlgorithms = ["RS256", "ES256"];
            // JWKS エンドポイント経由で公開鍵を自動取得するため IssuerSigningKey は不要
            options.TokenValidationParameters.IssuerSigningKey = null;
        }

        // JWT 認証エラー時のカスタムレスポンス（RFC 9457 Problem Details 形式）
        // OnChallenge: 401 Unauthorized（トークン無効・期限切れ）
        // OnForbidden: 403 Forbidden（権限不足）
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/problem+json";

                var errorCode = context.ErrorDescription?.Contains("expired") == true
                    ? "GW-4003" : "GW-4001";
                var detail = context.ErrorDescription?.Contains("expired") == true
                    ? "トークンの有効期限が切れています" : "無効な認証トークンです";

                await context.Response.WriteAsJsonAsync(new
                {
                    type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                    title = "Unauthorized",
                    status = 401,
                    detail,
                    instance = context.Request.Path.Value,
                    code = errorCode,
                    traceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier
                }, context.HttpContext.RequestAborted);
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = 403;
                context.Response.ContentType = "application/problem+json";

                await context.Response.WriteAsJsonAsync(new
                {
                    type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                    title = "Forbidden",
                    status = 403,
                    detail = "このリソースへのアクセス権限がありません",
                    instance = context.Request.Path.Value,
                    code = "GW-4002",
                    traceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier
                }, context.HttpContext.RequestAborted);
            }
        };
    });

// 認可ポリシー
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin", "ADMIN"));
    options.AddPolicy("AdminOrManager", p => p.RequireRole("Admin", "ADMIN", "Manager", "MANAGER"));
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// OpenTelemetry（設計書 §14）
// H-6: OTLP Exporter は環境変数 OTEL_EXPORTER_OTLP_ENDPOINT で設定
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("SkiShop.ApiGateway");
        if (!string.IsNullOrEmpty(otlpEndpoint))
        {
            tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter(GatewayMetrics.MeterName);
        if (!string.IsNullOrEmpty(otlpEndpoint))
        {
            metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    });

// ヘルスチェック（設計書 §21）— YARP の IProxyStateLookup 経由でクラスターの状態を参照
builder.Services.AddHealthChecks()
    .AddCheck<BackendServicesHealthCheck>("backend-services", tags: ["ready"]);

// ─────────────────────────────────────────────────────────────
// C-1: DI 登録（未登録コンポーネントの解決）
// ─────────────────────────────────────────────────────────────

// TimeProvider: CircuitBreakerService で使用
builder.Services.AddSingleton(TimeProvider.System);

// GatewayMetrics: OpenTelemetry カスタムメトリクス
builder.Services.AddSingleton<GatewayMetrics>();

// CircuitBreakerService: クラスター別サーキットブレーカー
builder.Services.AddSingleton<ICircuitBreakerService, CircuitBreakerService>();

// FallbackService: サーキットブレーカー Open 時のフォールバック
builder.Services.AddSingleton<IFallbackService, FallbackService>();

// Redis 分散キャッシュ（フォールバック用）with MemoryCache fallback（C-1-D）
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "ApiGateway:";
    });
}
else
{
    // Redis 未設定時は MemoryCache にフォールバック
    builder.Services.AddDistributedMemoryCache();
}

// C-2: AuthCacheInvalidationConsumer（Kafka 認証キャッシュ無効化）
builder.Services.AddHostedService<AuthCacheInvalidationConsumer>();

// レート制限（設計書 §8）
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.Append("Retry-After", "60");
        context.HttpContext.Response.ContentType = "application/problem+json";
        var traceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

        // H-8: レート制限拒否メトリクス記録
        context.HttpContext.RequestServices.GetRequiredService<GatewayMetrics>().RateLimiterLimited.Add(1,
            new KeyValuePair<string, object?>("path", context.HttpContext.Request.Path.Value));

        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc6585#section-4",
            title = "Too Many Requests",
            status = 429,
            detail = "レート制限を超過しました。しばらく待ってからリトライしてください。",
            instance = context.HttpContext.Request.Path.Value,
            code = "GW-4291",
            traceId
        }, cancellationToken);
    };

    // ログインエンドポイント: Fixed Window 5 req/min/IP
    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // 決済エンドポイント: Fixed Window 10 req/min/user
    // H-4: 未認証時は IP ベースにフォールバック（全員が "anonymous" に集約されるのを防止）
    options.AddPolicy("checkout", context =>
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var partitionKey = !string.IsNullOrEmpty(userId)
            ? $"user:{userId}"
            : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: partitionKey,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });

    // 商品一覧: Token Bucket 300 req/min/IP
    options.AddPolicy("products", context =>
        RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 300,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                TokensPerPeriod = 300,
                QueueLimit = 0
            }));

    // 匿名エンドポイント用: IP ベース Token Bucket 30 req/min（H-3: DoS / 列挙攻撃防止）
    options.AddPolicy("anonymous-api", context =>
        RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 30,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                TokensPerPeriod = 30,
                QueueLimit = 0
            }));

    // AI エンドポイント用: IP ベース Token Bucket 10 req/min（H-3: LLM API コスト暴走防止）
    options.AddPolicy("ai-api", context =>
        RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 10,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                TokensPerPeriod = 10,
                QueueLimit = 0
            }));

    // 認証済みユーザー汎用: Token Bucket 120 req/min（H-7: 全認証ルートに割り当て）
    // H-4: 未認証時は IP ベースにフォールバック（全員が "anonymous" に集約されるのを防止）
    options.AddPolicy("user-based", context =>
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var partitionKey = !string.IsNullOrEmpty(userId)
            ? $"user:{userId}"
            : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

        return RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: partitionKey,
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 120,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                TokensPerPeriod = 120,
                QueueLimit = 0
            });
    });
});

// YARP リバースプロキシ設定（設計書 §7）
// - appsettings.json から 18 ルート / 8 クラスターを読み込み
// - リクエスト転送時に X-Correlation-Id ヘッダーを付与（分散トレーシング）
// - クラスター別の接続タイムアウトを設定（認証 2s、決済 10s 等）
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(builderContext =>
    {
        builderContext.AddRequestTransform(transformContext =>
        {
            if (transformContext.HttpContext.Items.TryGetValue("CorrelationId", out var correlationId))
            {
                var value = correlationId?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    transformContext.ProxyRequest.Headers.Remove("X-Correlation-Id");
                    transformContext.ProxyRequest.Headers.Add("X-Correlation-Id", value);
                }
            }
            return ValueTask.CompletedTask;
        });
    })
    .ConfigureHttpClient((context, handler) =>
    {
        // クラスター別タイムアウト設定（設計書 §7）
        var timeout = context.ClusterId switch
        {
            "auth-cluster" => TimeSpan.FromSeconds(2),
            "user-cluster" => TimeSpan.FromSeconds(3),
            "inventory-cluster" => TimeSpan.FromSeconds(5),
            "sales-cluster" => TimeSpan.FromSeconds(4),
            "payment-cart-cluster" => TimeSpan.FromSeconds(10),
            "points-cluster" => TimeSpan.FromSeconds(3),
            "coupons-cluster" => TimeSpan.FromSeconds(3),
            "ai-cluster" => TimeSpan.FromSeconds(5),
            "mail-cluster" => TimeSpan.FromSeconds(5),
            _ => TimeSpan.FromSeconds(5)
        };

        handler.ConnectTimeout = timeout;
        handler.SslOptions.RemoteCertificateValidationCallback =
            builder.Environment.IsDevelopment() ? (_, _, _, _) => true : null;
    });

var app = builder.Build();

// ─────────────────────────────────────────────────────────────
// ミドルウェアパイプライン（順序厳守 — 設計書 §9）
// 1. ExceptionHandler: 最上位で全例外をキャッチ
// 2. ForwardedHeaders: リバースプロキシ背後の実 IP 復元
// 3. HSTS + HTTPS: トランスポート層セキュリティ
// 4. SecurityHeaders: OWASP 推奨レスポンスヘッダー
// 5. ResponseTime: 処理時間計測 + メトリクス記録
// 6. CorrelationId: 分散トレーシング用相関 ID
// 7. Serilog: 構造化リクエストログ
// 8. CORS: クロスオリジン制御
// 9. Authentication → Authorization: 認証・認可（順序不変）
// 10. RateLimiter: レート制限（認証後に配置しユーザー単位制限を実現）
// ─────────────────────────────────────────────────────────────

// グローバル例外ハンドラー（設計書 §11 — パイプライン最上位）
// GW-5001: ゲートウェイタイムアウト (504)
// GW-5003: バックエンドサービスエラー (502)
// GW-5004: 内部エラー (500)
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var error = feature?.Error;

        logger.LogError(error, "Unhandled exception: {Message}", error?.Message);

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        var (statusCode, errorCode, detail) = error switch
        {
            TimeoutException => (504, "GW-5001", "ゲートウェイタイムアウト"),
            HttpRequestException => (502, "GW-5003", "バックエンドサービスエラー"),
            _ => (500, "GW-5004", "内部エラーが発生しました")
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = $"https://tools.ietf.org/html/rfc9110#section-15.6",
            title = ReasonPhrases.GetReasonPhrase(statusCode),
            status = statusCode,
            detail,
            instance = context.Request.Path.Value,
            code = errorCode,
            traceId
        }, context.RequestAborted);
    });
});

app.UseForwardedHeaders();
app.UseHsts();
app.UseHttpsRedirection();
app.UseSecurityHeaders();
app.UseMiddleware<ResponseTimeMiddleware>();
app.UseCorrelationId();
app.UseSerilogRequestLogging();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// C-4: サーキットブレーカー & ステータスコードミドルウェア（MapReverseProxy の直前）
app.UseMiddleware<StatusCodeMiddleware>();
app.UseMiddleware<CircuitBreakerMiddleware>();

app.MapReverseProxy();

// ヘルスチェックエンドポイント（設計書 §21）
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponseAsync
}).AllowAnonymous();

app.Run();

/// <summary>
/// ヘルスチェック結果を JSON 形式でレスポンスに書き出すカスタムライター。
/// 各チェック項目の名前・ステータス・処理時間・詳細データを含む構造化レスポンスを返す。
/// </summary>
static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    var result = new
    {
        status = report.Status.ToString(),
        duration = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            duration = e.Value.Duration.TotalMilliseconds,
            description = e.Value.Description,
            data = e.Value.Data
        })
    };
    return context.Response.WriteAsJsonAsync(result, context.RequestAborted);
}
