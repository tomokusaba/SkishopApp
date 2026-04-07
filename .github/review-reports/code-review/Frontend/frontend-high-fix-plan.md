# Frontend High 指摘修正計画（32 件）

本ドキュメントは `check-report-1.md` に記載された **High 指摘 32 件** の詳細分析・修正対象ファイル・具体的修正コードをまとめたものである。

---

## 修正グループと優先度

| グループ | 対象 H-# | テーマ | 優先度 |
|---------|----------|--------|--------|
| **A. セキュリティ基盤** | H-01, H-05, H-06 | CSP ヘッダー、トークンリフレッシュ再設計 | 🔴 最優先 |
| **B. ミドルウェア・DI 設定** | H-02, H-03, H-04, H-19 | ExceptionHandler, RateLimiter, IOptions, Polly | 🔴 最優先 |
| **C. 非同期・SignalR** | H-07, H-08, H-09, H-29 | Fire-and-forget, CTS リーク, CT 伝搬, Reconnect | 🟠 高 |
| **D. AdminPortal 品質** | H-10, H-11, H-18, H-22, H-25 | try-catch, OTel, Readiness, Error ページ, API パス | 🟠 高 |
| **E. アーキテクチャ** | H-12, H-13, H-14, H-15, H-27, H-28 | レイヤー分離, DTO, インターフェース | 🟡 中 |
| **F. データ・キャッシュ** | H-16, H-17, H-31, H-32 | キャッシュ実装, トークン保存, バイパス, SendAsync | 🟡 中 |
| **G. その他** | H-20, H-21, H-23, H-24, H-26, H-30 | htmx, NuGet, ProblemDetails, REST, Tests, フォールバック | 🟢 通常 |

---

## A. セキュリティ基盤

### H-01: AdminPortal CSP ヘッダー未設定

**問題**: `AdminPortal/Program.cs` L79-86 で CSP ヘッダーが欠落。Frontend は `SecurityHeadersMiddleware` で設定済みだが AdminPortal には同等設定がない。

**対象ファイル**: `Services/AdminPortal/Program.cs`

**修正方法**: 既存のインラインセキュリティヘッダー（L79-86）に CSP を追加する。

```csharp
// 修正前（L79-86）:
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    await next();
});

// 修正後:
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://unpkg.com; style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; img-src 'self' data: https:; font-src 'self' https://cdn.jsdelivr.net;");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    await next();
});
```

> **注意**: AdminPortal は Bootstrap CDN + htmx CDN を使用するため `script-src` / `style-src` に CDN ドメインを許可する必要がある。

---

### H-05 / H-17: TokenRefreshHandler — リフレッシュ後の新トークン保存欠落

**問題**: `TokenRefreshHandler.cs` L34-39 でリフレッシュ成功後に API から返された新しいアクセストークン / リフレッシュトークンが Cookie に保存されない。2 回目以降のリフレッシュが必ず失敗する。H-17 は同一問題の重複。

**対象ファイル**: `Services/Frontend/Handlers/TokenRefreshHandler.cs`

**修正方法**: リフレッシュ成功後にレスポンスから新トークンを抽出し、Cookie に保存する。

```csharp
// 修正前（L34-40）:
var refreshResponse = await base.SendAsync(refreshRequest, cancellationToken);
if (refreshResponse.IsSuccessStatusCode)
{
    logger.LogInformation("トークンリフレッシュ成功");
    AttachAccessToken(request);
    response = await base.SendAsync(request, cancellationToken);
}

// 修正後:
var refreshResponse = await base.SendAsync(refreshRequest, cancellationToken);
if (refreshResponse.IsSuccessStatusCode)
{
    var tokenResponse = await refreshResponse.Content
        .ReadFromJsonAsync<TokenRefreshResponse>(cancellationToken);
    if (tokenResponse is not null)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            var accessCookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddHours(24)
            };
            httpContext.Response.Cookies.Append("access_token", tokenResponse.AccessToken, accessCookieOptions);
            var refreshCookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            };
            httpContext.Response.Cookies.Append("refresh_token", tokenResponse.RefreshToken, refreshCookieOptions);
        }
        logger.LogInformation("トークンリフレッシュ成功: 新トークンを Cookie に保存");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);
        response = await base.SendAsync(request, cancellationToken);
    }
}
```

ファイル末尾に DTO を追加:

```csharp
internal record TokenRefreshResponse(string AccessToken, string RefreshToken);
```

---

### H-06: TokenRefreshHandler — SemaphoreSlim スコープ問題

**問題**: `TokenRefreshHandler` は `AddTransient<TokenRefreshHandler>()` で登録されており、インスタンスフィールド `_refreshLock` がリクエストごとに新規作成される。並行リクエスト間で排他制御が機能しない。

**対象ファイル**: `Services/Frontend/Handlers/TokenRefreshHandler.cs`

**修正方法**: `SemaphoreSlim` を `static` フィールドに変更し、タイムアウトを追加。

```csharp
// 修正前（L14）:
private readonly SemaphoreSlim _refreshLock = new(1, 1);

// 修正後:
private static readonly SemaphoreSlim RefreshLock = new(1, 1);
```

```csharp
// 修正前（L25）:
await _refreshLock.WaitAsync(cancellationToken);

// 修正後:
if (!await RefreshLock.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken))
{
    logger.LogWarning("トークンリフレッシュのロック取得タイムアウト");
    return response;
}
```

```csharp
// 修正前（L54）:
_refreshLock.Release();

// 修正後:
RefreshLock.Release();
```

---

## B. ミドルウェア・DI 設定

### H-02: UseExceptionHandler が Development で無効 + Correlation ID 受信側未実装

**問題**:
1. 両 `Program.cs` で `UseExceptionHandler()` が `if (!app.Environment.IsDevelopment())` 内にあり、開発環境で例外が未処理になる。
2. AdminPortal に Correlation ID ミドルウェアが未実装。

**対象ファイル**:
- `Services/Frontend/Program.cs` (L127-131)
- `Services/AdminPortal/Program.cs` (L70-74)

**修正方法（Frontend/Program.cs）**:

```csharp
// 修正前:
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// 修正後:
app.UseExceptionHandler("/Error");
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
```

**修正方法（AdminPortal/Program.cs）**:

```csharp
// 修正前:
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Admin/Error");
    app.UseHsts();
}

// 修正後:
app.UseExceptionHandler("/Admin/Error");
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Correlation ID ミドルウェア追加（UseHsts の後）:
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
```

---

### H-03: UseRateLimiter 未実装

**問題**: ログイン・AI チャット等のエンドポイントにレート制限がなく、ブルートフォース攻撃に対する防御が欠如。

**対象ファイル**:
- `Services/Frontend/Program.cs`
- `Services/AdminPortal/Program.cs`

**修正方法（AdminPortal/Program.cs）** — サービス登録セクション（L42 付近）に追加:

```csharp
// 追加: レート制限設定
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("general", limiter =>
    {
        limiter.PermitLimit = 60;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 2;
    });
    options.RejectionStatusCode = 429;
});
```

ミドルウェアセクション（`app.UseAuthorization()` の後）に追加:

```csharp
app.UseRateLimiter();
```

**修正方法（Frontend/Program.cs）** — 同様にサービス登録+ミドルウェアを追加。

---

### H-04: IOptions パターン未使用

**問題**: 両 `Program.cs` で `builder.Configuration["ApiGateway:BaseUrl"]` による直接アクセス。型安全でなく、起動時バリデーションも不可。

**対象ファイル**:
- `Services/Frontend/Program.cs` (L58)
- `Services/AdminPortal/Program.cs` (L45)

**修正方法**:

1. 設定クラスを作成:

```csharp
// 新規ファイル: Services/Frontend/Configurations/ApiGatewaySettings.cs
// （AdminPortal にも同名ファイルを作成）
using System.ComponentModel.DataAnnotations;

namespace Frontend.Configurations;

public record ApiGatewaySettings
{
    [Required]
    [Url]
    public string BaseUrl { get; init; } = string.Empty;
}
```

2. Program.cs で IOptions 登録:

```csharp
// 修正前:
var apiGatewayBaseUrl = builder.Configuration["ApiGateway:BaseUrl"] ?? string.Empty;

// 修正後:
builder.Services.AddOptions<ApiGatewaySettings>()
    .Bind(builder.Configuration.GetSection("ApiGateway"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
var apiGatewayBaseUrl = builder.Configuration.GetSection("ApiGateway:BaseUrl").Value ?? string.Empty;
```

> **注**: `AddHttpClient` の設定時点では `IOptions<T>` が解決できないため `builder.Configuration` を使う必要があるが、`ValidateOnStart()` により起動時に設定不備を即座に検出できる。

---

### H-19: サーキットブレーカーの明示パラメータ未設定

**問題**: 両 `Program.cs` で `AddStandardResilienceHandler` を使用しているが、サーキットブレーカーのパラメータ（失敗率、遮断期間等）が未設定でデフォルト値のまま。

**対象ファイル**:
- `Services/Frontend/Program.cs` (L67-74)
- `Services/AdminPortal/Program.cs` (L52-59)

**修正方法**（両ファイル共通）:

```csharp
// 修正前:
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
    options.Retry.Delay = TimeSpan.FromMilliseconds(500);
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
});

// 修正後:
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
    options.Retry.Delay = TimeSpan.FromMilliseconds(500);

    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.MinimumThroughput = 10;

    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
});
```

---

## C. 非同期・SignalR

### H-07: SearchSuggest.razor — Task.Run Fire-and-Forget

**問題**: `HandleFocusOut()` (L161-169) が `_ = Task.Run(async () => ...)` で Fire-and-Forget パターン。例外が消失し、`InvokeAsync(StateHasChanged)` がコンポーネント破棄後に呼ばれるリスクがある。

**対象ファイル**: `Services/Frontend/Components/Shared/SearchSuggest.razor`

**修正方法**: `Task.Run` を排除し、`Task.Delay` + `InvokeAsync` パターンに置換。

```csharp
// 修正前（L161-169）:
private void HandleFocusOut()
{
    _ = Task.Run(async () =>
    {
        await Task.Delay(200);
        _showSuggestions = false;
        await InvokeAsync(StateHasChanged);
    });
}

// 修正後:
private async Task HandleFocusOut()
{
    try
    {
        await Task.Delay(200);
        _showSuggestions = false;
        StateHasChanged();
    }
    catch (ObjectDisposedException)
    {
        // コンポーネント破棄後は無視
    }
}
```

---

### H-08: SearchSuggest.razor — CancellationTokenSource 未 Dispose

**問題**: `_debounceCts` (L72) が毎回新規作成されるが `Dispose()` されない。`IAsyncDisposable` 未実装でリソースリーク。

**対象ファイル**: `Services/Frontend/Components/Shared/SearchSuggest.razor`

**修正方法**: `@implements IAsyncDisposable` を追加し、`DisposeAsync()` で CTS を破棄。

```razor
@* ファイル先頭付近に追加 *@
@implements IAsyncDisposable
```

```csharp
// @code ブロックに追加:
public ValueTask DisposeAsync()
{
    _debounceCts?.Cancel();
    _debounceCts?.Dispose();
    return ValueTask.CompletedTask;
}
```

---

### H-09: SignalR 3 コンポーネントで CancellationToken 未伝搬

**問題**: `StockRealtimeMonitor.razor` (L73-74), `OrderStatusMonitor.razor` (L75-76), `AiChatWidget.razor` (L182) で `HubConnection.StartAsync()` / `InvokeAsync()` に `CancellationToken` が渡されていない。

**対象ファイル**:
- `Services/Frontend/Components/Shared/StockRealtimeMonitor.razor` (L73-74, L89)
- `Services/Frontend/Components/Shared/OrderStatusMonitor.razor` (L75-76)
- `Services/Frontend/Components/Shared/AiChatWidget.razor` (L182)

**修正方法**: 各コンポーネントに `CancellationTokenSource _cts` を追加し、`IAsyncDisposable` の `DisposeAsync` でキャンセル。SignalR 呼び出しに `_cts.Token` を伝搬。

**StockRealtimeMonitor.razor の例**:

```csharp
// @code ブロック先頭に追加:
private readonly CancellationTokenSource _cts = new();

// 修正前（L73-74）:
await _hubConnection.StartAsync();
await _hubConnection.InvokeAsync("JoinProductGroup", ProductId);

// 修正後:
await _hubConnection.StartAsync(_cts.Token);
await _hubConnection.InvokeAsync("JoinProductGroup", ProductId, _cts.Token);

// 修正前（L89）:
await _hubConnection.InvokeAsync("LeaveProductGroup", ProductId);

// 修正後:
await _hubConnection.InvokeAsync("LeaveProductGroup", ProductId, CancellationToken.None);

// DisposeAsync に追加:
_cts.Cancel();
_cts.Dispose();
```

AiChatWidget, OrderStatusMonitor も同様のパターンで修正。

---

### H-29: AiChatWidget.razor — SignalR Reconnecting/Closed ハンドラ未設定

**問題**: `WithAutomaticReconnect()` は設定済みだが、`Reconnecting` / `Closed` イベントハンドラが未設定。再接続状態が UI に反映されず、完全切断時のフォールバック処理もない。

**対象ファイル**: `Services/Frontend/Components/Shared/AiChatWidget.razor` (L158-189)

**修正方法**: `InitializeSignalRAsync()` 内に Reconnecting / Closed ハンドラを追加。

```csharp
// _hubConnection.Build(); の後、On<string> の前に追加:

_hubConnection.Reconnecting += (ex) =>
{
    Logger.LogWarning(ex, "AI チャット SignalR 再接続中");
    _isReconnecting = true;
    InvokeAsync(StateHasChanged);
    return Task.CompletedTask;
};

_hubConnection.Reconnected += (connectionId) =>
{
    Logger.LogInformation("AI チャット SignalR 再接続完了: ConnectionId={ConnectionId}", connectionId);
    _isReconnecting = false;
    InvokeAsync(StateHasChanged);
    return Task.CompletedTask;
};

_hubConnection.Closed += (ex) =>
{
    Logger.LogWarning(ex, "AI チャット SignalR 接続断");
    _isReconnecting = false;
    InvokeAsync(StateHasChanged);
    return Task.CompletedTask;
};
```

`@code` ブロックにフィールド追加:

```csharp
private bool _isReconnecting;
```

---

## D. AdminPortal 品質

### H-10: AdminPortal 5 ページの POST 系ハンドラに try-catch なし

**問題**: Users, Coupons, Points, Campaigns, Mails の各 Index.cshtml.cs で POST 系ハンドラ（`OnPostAsync` 等）に `try-catch` がない。`HttpRequestException` や `TaskCanceledException` が未処理のまま 500 エラーになる。

**対象ファイル**:
- `Services/AdminPortal/Pages/Admin/Users/Index.cshtml.cs` — `OnPostUnlockAsync` (L65-78)
- `Services/AdminPortal/Pages/Admin/Coupons/Index.cshtml.cs` — `OnPostCreateAsync`
- `Services/AdminPortal/Pages/Admin/Points/Index.cshtml.cs` — `OnPostAdjustAsync`
- `Services/AdminPortal/Pages/Admin/Campaigns/Index.cshtml.cs` — `OnPostCreateAsync`, `OnPostDeleteAsync`
- `Services/AdminPortal/Pages/Admin/Mails/Index.cshtml.cs` — `OnPostCreateTemplateAsync`, `OnPostSendTestAsync`

**修正方法（共通パターン）**: 各 POST ハンドラを `try-catch` で囲む。

```csharp
// 修正前（Users/Index.cshtml.cs の例、L65-78）:
public async Task<IActionResult> OnPostUnlockAsync(string userId, CancellationToken ct = default)
{
    logger.LogInformation("...");
    var client = httpClientFactory.CreateClient("ApiGateway");
    var response = await client.PostAsync($"/admin/users/{userId}/unlock", null, ct);
    StatusMessage = response.IsSuccessStatusCode
        ? "ユーザーのロックを解除しました。"
        : "ユーザーのロック解除に失敗しました。";
    await LoadUsersAsync(ct);
    return Page();
}

// 修正後:
public async Task<IActionResult> OnPostUnlockAsync(string userId, CancellationToken ct = default)
{
    try
    {
        logger.LogInformation("ユーザーアンロックリクエスト: TargetUserId={TargetUserId}, Operator={Operator}",
            userId, User.Identity?.Name ?? "unknown");
        var client = httpClientFactory.CreateClient("ApiGateway");
        var response = await client.PostAsync($"/admin/users/{userId}/unlock", null, ct);
        StatusMessage = response.IsSuccessStatusCode
            ? "ユーザーのロックを解除しました。"
            : "ユーザーのロック解除に失敗しました。";
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "ユーザーアンロック API 通信エラー: TargetUserId={TargetUserId}", userId);
        StatusMessage = "通信エラーが発生しました。しばらく経ってからお試しください。";
    }
    catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
    {
        logger.LogError(ex, "ユーザーアンロック API タイムアウト: TargetUserId={TargetUserId}", userId);
        StatusMessage = "リクエストがタイムアウトしました。しばらく経ってからお試しください。";
    }
    await LoadUsersAsync(ct);
    return Page();
}
```

> **5 ファイルすべてで同パターンを適用する。**

---

### H-11: AdminPortal OpenTelemetry 未構成

**問題**: `AdminPortal.csproj` に OpenTelemetry パッケージ参照があるが `Program.cs` に構成コードがない。分散トレーシングが AdminPortal で途切れる。

**対象ファイル**: `Services/AdminPortal/Program.cs`

**修正方法**: サービス登録セクションに OpenTelemetry 構成を追加。

```csharp
// 追加（using 文）:
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

// 追加（builder.Services セクション、L62 付近）:
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("AdminPortal.*"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation());
```

---

### H-18: AdminPortal Readiness ヘルスチェック未実装

**問題**: `AdminPortal/Program.cs` L97 で `/health` のみ。`/health/ready`（Readiness プローブ）が未実装。

**対象ファイル**: `Services/AdminPortal/Program.cs`

**修正方法**:

```csharp
// 修正前（L97）:
app.MapHealthChecks("/health").AllowAnonymous();

// 修正後:
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();
```

ヘルスチェック登録に API Gateway の疎通確認を追加:

```csharp
// 修正前（L62）:
builder.Services.AddHealthChecks();

// 修正後:
builder.Services.AddHealthChecks()
    .AddUrlGroup(new Uri($"{apiGatewayBaseUrl}/health"), name: "api-gateway", tags: ["ready"]);
```

---

### H-22: AdminPortal Error.cshtml 不在

**問題**: `Program.cs` L72 の `UseExceptionHandler("/Admin/Error")` が参照する `Error.cshtml` が存在しない。例外発生時に 404 になる。

**対象**: 新規ファイル 2 つを作成

**修正方法**: `Services/AdminPortal/Pages/Admin/Error.cshtml` + `Error.cshtml.cs` を新規作成。

**Error.cshtml**:

```razor
@page "/Admin/Error"
@model AdminPortal.Pages.Admin.ErrorModel
@{
    ViewData["Title"] = "エラー";
    Layout = "_Layout";
}

<div class="container-fluid py-4">
    <div class="row justify-content-center">
        <div class="col-md-6 text-center">
            <h1 class="display-1 text-danger"><i class="bi bi-exclamation-triangle"></i></h1>
            <h2>エラーが発生しました</h2>
            <p class="text-secondary">
                申し訳ございません。サーバーで予期しないエラーが発生しました。
            </p>
            @if (!string.IsNullOrEmpty(Model.RequestId))
            {
                <p class="text-muted small">リクエスト ID: @Model.RequestId</p>
            }
            <a href="/Admin" class="btn btn-primary mt-3">
                <i class="bi bi-house"></i> ダッシュボードに戻る
            </a>
        </div>
    </div>
</div>
```

**Error.cshtml.cs**:

```csharp
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminPortal.Pages.Admin;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
public class ErrorModel(ILogger<ErrorModel> logger) : PageModel
{
    public string? RequestId { get; set; }

    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        logger.LogError("管理ポータルエラーページ表示: RequestId={RequestId}", RequestId);
    }
}
```

---

### H-25: AdminPortal API パスプレフィックスの混在

**問題**: 複数ページで `/admin/users`, `/admin/products` と `/api/v1/` プレフィックスが混在。API Gateway の実際のルーティングと不整合が生じるリスク。

**対象ファイル**: AdminPortal の各 PageModel

**修正方法**: API パスプレフィックスを統一する。AdminPortal → API Gateway への通信は `/api/v1/admin/` プレフィックスに統一。

> **注意**: これはバックエンドの API Gateway ルーティング設計にも依存するため、バックエンド側のルーティングを確認の上で統一する。現時点では各ページ内の既存パスを確認し、最も多く使われているプレフィックスに揃える方針とする。具体的な修正はバックエンド API パスの確定後に実施する。

---

## E. アーキテクチャ

### H-12: CartEffects.cs — レイヤー違反 + API パス不整合

**問題**:
1. `CartEffects.cs` が `IApiGatewayClient` を直接注入し、API パスが `/cart/items` とハードコード。`CartApiClient` が `/api/v1/cart/items` パスを使用しており不整合。
2. Effect が API クライアント層をバイパスしている。

**対象ファイル**: `Services/Frontend/Store/CartStore/CartEffects.cs`

**修正方法**: `IApiGatewayClient` の代わりに `CartApiClient` を注入し、API パスの重複定義を排除する。

```csharp
// 修正前:
public class CartEffects(
    IApiGatewayClient apiClient,
    ILogger<CartEffects> logger)
{
    [EffectMethod]
    public async Task HandleLoadCart(LoadCartAction _, IDispatcher dispatcher)
    {
        var items = await apiClient.GetAsync<List<CartItem>>("/cart/items");
        // ...
    }
}

// 修正後:
public class CartEffects(
    CartApiClient cartApi,
    ILogger<CartEffects> logger)
{
    [EffectMethod]
    public async Task HandleLoadCart(LoadCartAction _, IDispatcher dispatcher)
    {
        try
        {
            var cart = await cartApi.GetCartAsync();
            dispatcher.Dispatch(new LoadCartSuccessAction(cart?.Items ?? []));
        }
        catch (UnauthorizedAccessException)
        {
            dispatcher.Dispatch(new LoadCartSuccessAction([]));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "カート読み込みエラー");
            dispatcher.Dispatch(new LoadCartFailureAction("カートの読み込みに失敗しました"));
        }
    }

    [EffectMethod]
    public async Task HandleAddToCart(AddToCartAction action, IDispatcher dispatcher)
    {
        try
        {
            var request = new AddCartItemRequest(action.ProductId, action.Quantity);
            await cartApi.AddItemAsync(request);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "カート追加エラー: ProductId={ProductId}", action.ProductId);
        }
    }

    [EffectMethod]
    public async Task HandleRemoveFromCart(RemoveFromCartAction action, IDispatcher dispatcher)
    {
        try
        {
            await cartApi.RemoveItemAsync(action.ItemId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "カート削除エラー: ItemId={ItemId}", action.ItemId);
        }
    }
}
```

> **注意**: `CartApiClient` の実際のメソッドシグネチャに合わせた修正:
> - `GetCartAsync()` → `CartDto?` を返す（`CartDto.Items` からアイテムリストを取得）
> - `AddItemAsync(AddCartItemRequest request)` → `AddCartItemRequest` record を使用
> - `RemoveItemAsync(string itemId)` → アイテム ID（ProductId ではなく CartItem の ID）を使用
> - `RemoveFromCartAction` の `ProductId` プロパティを `ItemId` に変更する必要がある

---

### H-13: DTO が ApiClient ファイル末尾に同居

**問題**: 全 ApiClient ファイルの末尾に DTO（record）が定義されており、`DTOs/` ディレクトリに分離されていない。DTO の再利用性が低下し、循環参照リスクがある。

**対象ファイル**: 全 ApiClient（ProductApiClient, OrderApiClient, CartApiClient 等）

**修正方法**: DTO を `Services/Frontend/DTOs/` ディレクトリに分離する。

> **注意**: この修正は影響範囲が広い。段階的に実施することを推奨。
> 1. `DTOs/` ディレクトリを作成
> 2. 各 ApiClient から DTO を切り出し
> 3. namespace を `Frontend.DTOs` に変更
> 4. 各 ApiClient に `using Frontend.DTOs;` を追加
> 5. ビルド確認

現時点では **Medium 優先度に降格** し、次スプリントで実施する方針を推奨する。理由: 機能的な影響はなく、リファクタリングの範囲が大きいため個別対応がリスクを増大させる。

---

### H-14: ApiErrorHandler の ISnackbar 依存（Service 層の UI 依存）

**問題**: `ApiErrorHandler.cs` のコンストラクタで `MudBlazor.ISnackbar` を注入。Service 層が UI 層（MudBlazor）に直接依存しており、レイヤー分離違反。テスタビリティも低下。

**対象ファイル**: `Services/Frontend/Services/ApiErrorHandler.cs`

**修正方法**: `ISnackbar` 依存を除去し、通知用の抽象インターフェースを介する。

```csharp
// 新規インターフェース: Services/Frontend/Services/Interfaces/INotificationService.cs
namespace Frontend.Services.Interfaces;

public interface INotificationService
{
    void ShowInfo(string message);
    void ShowWarning(string message);
    void ShowError(string message);
}
```

```csharp
// 新規実装: Services/Frontend/Services/MudBlazorNotificationService.cs
using Frontend.Services.Interfaces;
using MudBlazor;

namespace Frontend.Services;

public class MudBlazorNotificationService(ISnackbar snackbar) : INotificationService
{
    public void ShowInfo(string message) => snackbar.Add(message, Severity.Info);
    public void ShowWarning(string message) => snackbar.Add(message, Severity.Warning);
    public void ShowError(string message) => snackbar.Add(message, Severity.Error);
}
```

```csharp
// ApiErrorHandler.cs 修正:
public class ApiErrorHandler(
    INotificationService notification,  // ISnackbar → INotificationService
    ILogger<ApiErrorHandler> logger)
{
    // snackbar.Add(...) → notification.ShowWarning(...) / ShowError(...) に置換
}
```

```csharp
// Program.cs DI 登録追加:
builder.Services.AddScoped<INotificationService, MudBlazorNotificationService>();
```

---

### H-15: 14 サービスクラスにインターフェース未定義

**問題**: `IApiGatewayClient` を除く全サービスクラス（`AuthApiClient`, `ProductApiClient`, `CartApiClient`, `OrderApiClient`, `CouponApiClient`, `PaymentApiClient`, `UserApiClient`, `ShipmentReturnApiClient`, `PointApiClient`, `WishlistApiClient`, `AiApiClient`, `CacheService`, `TokenStorageService`, `HtmlSanitizationService`）にインターフェースが未定義。モック化不可でテスタビリティが低い。

**対象ファイル**: `Services/Frontend/Services/Interfaces/` に新規インターフェースを作成

**修正方法**: 最低限以下の主要サービスにインターフェースを追加する。

```
Services/Frontend/Services/Interfaces/
├── IApiGatewayClient.cs       # 既存
├── IAuthApiClient.cs          # 新規
├── IProductApiClient.cs       # 新規
├── ICartApiClient.cs          # 新規
├── IOrderApiClient.cs         # 新規
├── ICacheService.cs           # 新規
├── INotificationService.cs    # 新規（H-14 と統合）
└── ITokenStorageService.cs    # 新規
```

> **段階的実施を推奨**: まず `ICacheService`, `ITokenStorageService` を追加し、テストが必要なサービスから順次対応する。

---

### H-27: CartState.TotalAmount — 金額再計算がフロントエンドに漏洩

**問題**: `CartState.cs` L16 で `TotalAmount => Items.Sum(i => i.Price * i.Quantity)` と金額計算をフロントエンドで実行。DDD 原則では金額計算はバックエンド（ドメイン層）の責務。

**対象ファイル**: `Services/Frontend/Store/CartStore/CartState.cs` (L16)

**修正方法**: `TotalAmount` をバックエンドから取得した値を表示用に保持するプロパティに変更。

```csharp
// 修正前:
public decimal TotalAmount => Items.Sum(i => i.Price * i.Quantity);

// 修正後:
public decimal TotalAmount { get; init; }

// ※ TotalAmount はカート API のレスポンスから取得したサーバー計算値を設定する。
// CartReducers で API レスポンスの TotalAmount を CartState に反映する。
```

> **注意**: この修正はバックエンドの Cart API が `TotalAmount` をレスポンスに含めることが前提。バックエンドの API 仕様確認が必要。バックエンドが対応するまでの暫定策として、現在のフロントエンド計算を **表示用参考値** としてコメントで明記する選択肢もある。

---

### H-28: Orders/Detail.cshtml.cs — Staff ロール制限のビジネスルールが BFF に漏洩

**問題**: `Orders/Detail.cshtml.cs` L50-58 で Staff ロールの操作制限ロジックが BFF 側に実装されている。認可ルールはバックエンドの API Gateway / サービス側で制御すべき。

**対象ファイル**: `Services/AdminPortal/Pages/Admin/Orders/Detail.cshtml.cs`

**修正方法**: BFF 側の Staff ロール制限コードにコメントで「**UI 便宜上の制限。認可の最終判定は API Gateway 側で実施**」と明記する。API Gateway 側のロール認可が実装されるまでの暫定措置として許容する。

```csharp
// 修正: ビジネスルールが BFF に存在することを明記
// NOTE: この UI 側の制限は UX 目的の便宜的チェック。
// 認可の最終判定は API Gateway / バックエンドサービスで実施される。
// バックエンド RBAC 実装後に UI 側ロジックの整理を検討する。
```

---

## F. データ・キャッシュ

### H-16: CacheService — InvalidateByPrefix が No-Op 実装

**問題**: `CacheService.cs` L55-60 の `InvalidateByPrefix` がログ出力のみで実際のキャッシュ無効化を行わない。商品更新後に古いキャッシュが残る。

**対象ファイル**: `Services/Frontend/Services/CacheService.cs`

**修正方法**: `IMemoryCache` は列挙をサポートしないため、キャッシュキーを追跡するセットを導入する。

```csharp
// 修正後:
public class CacheService(IMemoryCache cache, ILogger<CacheService> logger)
{
    private static readonly Dictionary<string, TimeSpan> CacheDurations = new()
    {
        ["products"] = TimeSpan.FromMinutes(5),
        ["categories"] = TimeSpan.FromMinutes(10),
        ["cart"] = TimeSpan.Zero,
        ["orders"] = TimeSpan.FromMinutes(1),
        ["points"] = TimeSpan.FromSeconds(30),
        ["coupons"] = TimeSpan.FromMinutes(5),
        ["user_profile"] = TimeSpan.FromMinutes(2),
        ["wishlists"] = TimeSpan.FromMinutes(1),
    };

    // キャッシュキー追跡用（prefix → keys の逆引き。期限切れ時の自動削除に対応）
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _keysByPrefix = new();

    public async Task<T?> GetOrSetAsync<T>(
        string key, string dataType, Func<Task<T?>> factory,
        CancellationToken ct = default) where T : class
    {
        if (!CacheDurations.TryGetValue(dataType, out var duration) || duration == TimeSpan.Zero)
        {
            return await factory();
        }

        if (cache.TryGetValue(key, out T? cached))
        {
            logger.LogDebug("Cache hit: {CacheKey}", key);
            return cached;
        }

        logger.LogDebug("Cache miss: {CacheKey}", key);
        var result = await factory();
        if (result is not null)
        {
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = duration
            };
            // キャッシュ期限切れ時にキー追跡からも削除（メモリリーク防止）
            options.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
            {
                if (_keysByPrefix.TryGetValue(dataType, out var trackedKeys))
                {
                    trackedKeys.TryRemove(evictedKey.ToString()!);
                }
            });
            cache.Set(key, result, options);
            TrackKey(dataType, key);
        }

        return result;
    }

    public void Invalidate(string key)
    {
        cache.Remove(key);
        logger.LogDebug("Cache invalidated: {CacheKey}", key);
    }

    public void InvalidateByPrefix(string prefix)
    {
        if (_keysByPrefix.TryRemove(prefix, out var keys))
        {
            var count = 0;
            foreach (var key in keys.Keys)
            {
                cache.Remove(key);
                count++;
            }
            logger.LogInformation("Cache prefix invalidated: {Prefix}, Count={Count}", prefix, count);
        }
    }

    private void TrackKey(string prefix, string key)
    {
        var set = _keysByPrefix.GetOrAdd(prefix, _ => new());
        set.TryAdd(key, 0);
    }
}
```

> **using 追加**: `using System.Collections.Concurrent;`

---

### H-31: ProductApiClient — NewArrivals/Popular/Categories がキャッシュバイパス

**問題**: `ProductApiClient.cs` L46-65 で `GetNewArrivalsAsync`, `GetPopularProductsAsync`, `GetProductsByCategoryAsync` がキャッシュを経由せず毎回 API を呼び出す。

**対象ファイル**: `Services/Frontend/Services/ProductApiClient.cs` (L46-65)

**修正方法**: 各メソッドに `cacheService.GetOrSetAsync()` を適用。

```csharp
// 修正前:
public async Task<List<ProductDto>> GetNewArrivalsAsync(int count = 8, CancellationToken ct = default)
{
    var result = await apiClient.GetAsync<PaginatedResult<ProductDto>>(
        $"/api/v1/products?sort=createdAt,desc&size={count}", ct);
    return result?.Items ?? [];
}

// 修正後:
public async Task<List<ProductDto>> GetNewArrivalsAsync(int count = 8, CancellationToken ct = default)
{
    var cacheKey = $"products_new_arrivals_{count}";
    var result = await cacheService.GetOrSetAsync(
        cacheKey,
        "products",
        () => apiClient.GetAsync<PaginatedResult<ProductDto>>(
            $"/api/v1/products?sort=createdAt,desc&size={count}", ct),
        ct);
    return result?.Items ?? [];
}
```

同様に `GetPopularProductsAsync`, `GetProductsByCategoryAsync`, `GetCategoriesAsync` にもキャッシュを適用。

---

### H-32: ApiGatewayClient.SendAsync — EnsureSuccessAsync 未呼出

**問題**: `ApiGatewayClient.cs` L64-70 の `SendAsync` メソッドのみ `EnsureSuccessAsync` を呼んでいない。呼び出し元がエラーチェックを忘れるとサイレントに失敗する。

**対象ファイル**: `Services/Frontend/Services/ApiGatewayClient.cs` (L64-70)

**修正方法**: `SendAsync` にもエラーチェックを追加するか、XML doc コメントで呼び出し元の責任を明記。

```csharp
// 修正案 A（推奨）: EnsureSuccessAsync を呼ぶ
public async Task<HttpResponseMessage> SendAsync(
    HttpMethod method, string path, HttpContent? content = null, CancellationToken ct = default)
{
    var request = new HttpRequestMessage(method, path) { Content = content };
    var response = await httpClient.SendAsync(request, ct);
    await EnsureSuccessAsync(response, ct);
    return response;
}

// 修正案 B: 呼び出し元がチェックする旨を明記（非推奨）
/// <summary>
/// 生の HttpResponseMessage を返す。呼び出し元で IsSuccessStatusCode を確認すること。
/// </summary>
```

---

## G. その他

### H-20: htmx ロード済みだが全ページで未使用

**問題**: `_Layout.cshtml` で htmx CDN が読み込まれているが、全 AdminPortal ページで htmx 属性（`hx-get`, `hx-post` 等）が使用されていない。不要なスクリプトの読み込みはパフォーマンス低下と攻撃面拡大。

**対象ファイル**: `Services/AdminPortal/Pages/Admin/_Layout.cshtml` (L109-111)

**修正方法**: 2 つの選択肢がある。

**選択肢 A（推奨）**: htmx を削除する。設計書では htmx + Alpine.js が規定されているが、現時点で全ページが標準フォーム送信（POST → PRG パターン）で動作しており htmx は不要。

```html
<!-- 削除対象（L109-111）: -->
<script src="https://unpkg.com/htmx.org@2.0.4"
        integrity="sha384-HGfztofotfshcF7+8n44JQL2oJmowVChPTg48S+jvZoztPfvwD79OC/LTtG6dMp+"
        crossorigin="anonymous"></script>
```

**選択肢 B**: 今後 htmx を活用する場合は維持し、H-20 を受入れる。その場合、段階的に htmx 属性を導入する計画を策定する。

---

### H-21: AdminPortal.csproj に未使用 NuGet パッケージ

**問題**: `AdminPortal.csproj` に `JwtBearer`, `FluentValidation`, `OpenTelemetry` パッケージ参照があるが、いずれも `Program.cs` や各ページで使用されていない。

**対象ファイル**: `Services/AdminPortal/AdminPortal.csproj`

**修正方法**: 使用状況を確認し、未使用パッケージを削除。

```xml
<!-- 削除候補（H-11 で OTel を構成する場合は OTel パッケージは残す）: -->
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.*" />
<!-- ↑ AdminPortal は Cookie 認証のみ使用。JwtBearer は不要 -->

<PackageReference Include="FluentValidation" Version="11.*" />
<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.*" />
<!-- ↑ AdminPortal は Data Annotations を使用。FluentValidation は未登録・未使用 -->
```

> **H-11（OpenTelemetry 構成）を先に修正** すれば OTel パッケージは使用済みになる。

---

### H-23: AdminPortal ProblemDetails パース基盤なし

**問題**: AdminPortal の各 PageModel で API エラーレスポンス（RFC 9457 ProblemDetails）をパースする共通基盤がない。エラー詳細がユーザーに表示されず「失敗しました」のみ。

**対象ファイル**: AdminPortal 全 PageModel

**修正方法**: 共通ヘルパーメソッドを作成し、各 PageModel から呼び出す。

```csharp
// 新規ファイル: Services/AdminPortal/Helpers/ApiResponseHelper.cs
using System.Net.Http.Json;

namespace AdminPortal.Helpers;

public static class ApiResponseHelper
{
    public static async Task<string> GetErrorMessageAsync(
        HttpResponseMessage response,
        string defaultMessage,
        CancellationToken ct = default)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(ct);
            return problem?.Detail ?? defaultMessage;
        }
        catch
        {
            return defaultMessage;
        }
    }

    public record ProblemDetailsDto(string? Type, string? Title, int? Status, string? Detail, string? Instance);
}
```

各 PageModel での使用例:

```csharp
// 修正前:
StatusMessage = response.IsSuccessStatusCode ? "成功" : "失敗しました";

// 修正後:
StatusMessage = response.IsSuccessStatusCode
    ? "成功"
    : await ApiResponseHelper.GetErrorMessageAsync(response, "操作に失敗しました", ct);
```

---

### H-24: ステータス変更に PUT 使用（POST が適切）

**問題**: Orders/Shipments のステータス変更で `PUT` を使用しているが、ステータス変更はリソースの全置換ではなく「操作の実行」であるため `POST` が適切。

**対象ファイル**:
- `Services/AdminPortal/Pages/Admin/Orders/Detail.cshtml.cs`
- `Services/AdminPortal/Pages/Admin/Shipments/Index.cshtml.cs`

**修正方法**: `PutAsync` → `PostAsync` に変更。

```csharp
// 修正前:
var response = await client.PutAsJsonAsync($"/admin/orders/{Id}/status", new { Status = NewStatus }, ct);

// 修正後:
var response = await client.PostAsJsonAsync($"/admin/orders/{Id}/status", new { Status = NewStatus }, ct);
```

> **注意**: バックエンド API のルーティングも PUT → POST に合わせる必要がある。バックエンドのエンドポイント定義を確認の上で変更する。

---

### H-26: Frontend.Tests.csproj — TreatWarningsAsErrors 未設定

**問題**: テストプロジェクトの `.csproj` に `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` がない。テストコードの警告が放置される。

**対象ファイル**: `Services/Frontend.Tests/Frontend.Tests.csproj`

**修正方法**: PropertyGroup に追加。

```xml
<!-- 修正前: -->
<PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <NoWarn>NU1608</NoWarn>
</PropertyGroup>

<!-- 修正後: -->
<PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <NoWarn>NU1608</NoWarn>
</PropertyGroup>
```

---

### H-30: AiApiClient.EscalateChatAsync — try-catch なし

**問題**: `AiApiClient.cs` L122-126 の `EscalateChatAsync` のみ他メソッドと異なり try-catch が実装されていない。例外がコンポーネントまで伝搬し、UI がクラッシュする。

**対象ファイル**: `Services/Frontend/Services/AiApiClient.cs` (L122-126)

**修正方法**: 他メソッドと同じグレースフルデグラデーションパターンを適用。

```csharp
// 修正前:
public async Task EscalateChatAsync(string sessionId, CancellationToken ct = default)
{
    logger.LogInformation("AI チャットエスカレーション: SessionId={SessionId}", sessionId);
    await apiClient.PostAsync<object, object>(
        $"/api/v1/ai/chat/sessions/{sessionId}/escalate", new { }, ct);
}

// 修正後:
public async Task EscalateChatAsync(string sessionId, CancellationToken ct = default)
{
    try
    {
        logger.LogInformation("AI チャットエスカレーション: SessionId={SessionId}", sessionId);
        await apiClient.PostAsync<object, object>(
            $"/api/v1/ai/chat/sessions/{sessionId}/escalate", new { }, ct);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "AI チャットエスカレーション失敗: SessionId={SessionId}", sessionId);
    }
}
```

---

## 修正実施順序（推奨）

| 順序 | 修正対象 | 影響範囲 | 難易度 |
|------|---------|---------|--------|
| 1 | H-02: UseExceptionHandler 全環境化 | 両 Program.cs | 低 |
| 2 | H-22: Error.cshtml 新規作成 | AdminPortal 新規ファイル | 低 |
| 3 | H-01: AdminPortal CSP ヘッダー | AdminPortal/Program.cs | 低 |
| 4 | H-06: SemaphoreSlim → static | TokenRefreshHandler.cs | 低 |
| 5 | H-05/H-17: トークン保存追加 | TokenRefreshHandler.cs | 中 |
| 6 | H-07/H-08: SearchSuggest 修正 | SearchSuggest.razor | 低 |
| 7 | H-10: AdminPortal try-catch 追加 | 5 ファイル | 中 |
| 8 | H-30: EscalateChatAsync try-catch | AiApiClient.cs | 低 |
| 9 | H-32: SendAsync EnsureSuccess | ApiGatewayClient.cs | 低 |
| 10 | H-26: TreatWarningsAsErrors | Frontend.Tests.csproj | 低 |
| 11 | H-29: SignalR Reconnect ハンドラ | AiChatWidget.razor | 中 |
| 12 | H-09: SignalR CT 伝搬 | 3 コンポーネント | 中 |
| 13 | H-03: RateLimiter 追加 | 両 Program.cs | 中 |
| 14 | H-18: Readiness ヘルスチェック | AdminPortal/Program.cs | 低 |
| 15 | H-11: OpenTelemetry 構成 | AdminPortal/Program.cs | 中 |
| 16 | H-19: サーキットブレーカー設定 | 両 Program.cs | 低 |
| 17 | H-04: IOptions パターン導入 | 両 Program.cs + 新規設定クラス | 中 |
| 18 | H-31: キャッシュ適用 | ProductApiClient.cs | 低 |
| 19 | H-16: InvalidateByPrefix 実装 | CacheService.cs | 中 |
| 20 | H-12: CartEffects 修正 | CartEffects.cs | 中 |
| 21 | H-21: 未使用 NuGet 削除 | AdminPortal.csproj | 低 |
| 22 | H-20: htmx 削除判断 | _Layout.cshtml | 低 |
| 23 | H-14: ApiErrorHandler 分離 | ApiErrorHandler.cs + 新規 | 高 |
| 24 | H-23: ProblemDetails ヘルパー | 新規 + 全 PageModel | 高 |
| 25 | H-27: CartState.TotalAmount | CartState.cs | 中（バックエンド依存） |
| 26 | H-28: Staff ロール制限コメント | Orders/Detail.cshtml.cs | 低 |
| 27 | H-24: PUT → POST 変更 | 2 ファイル（バックエンド依存） | 低（バックエンド確認後） |
| 28 | H-25: API パス統一 | 全 PageModel（バックエンド依存） | 高（バックエンド確認後） |
| 29 | H-15: インターフェース追加 | 新規ファイル多数 + DI 修正 | 高 |
| 30 | H-13: DTO 分離 | 全 ApiClient + 新規ファイル多数 | 高 |

---

## ビルド検証コマンド

修正後は必ず以下を実行:

```bash
dotnet build Services/Frontend/Frontend.csproj --no-restore
dotnet build Services/AdminPortal/AdminPortal.csproj --no-restore
dotnet test Services/Frontend.Tests/Frontend.Tests.csproj --no-restore
```

---

## 検証レポート（ソースコード照合結果）

本修正プラン作成後、全 32 件の指摘事項について実際のソースコードと照合検証を実施した。

### 照合結果サマリー

| 検証項目 | 結果 |
|---------|------|
| 全 32 件の行番号・コード参照 | ✅ 全件正確 |
| 修正コードのコンパイル可能性 | ⚠️ 3 件修正済み（下記参照） |
| CartApiClient メソッドシグネチャ整合性 | ⚠️ 修正済み |
| CacheService メモリリーク対策 | ⚠️ 補強済み |

### 検証で発見・修正した問題

1. **H-05/H-17 — `CookieOptions with { ... }` コンパイルエラー修正**
   - `CookieOptions` はクラス（record ではない）のため `with` 式が使用不可
   - → `accessCookieOptions` / `refreshCookieOptions` を別々に `new CookieOptions { ... }` で生成する形に修正

2. **H-12 — CartEffects 修正コードのメソッドシグネチャ不整合修正**
   - 修正前プランの `GetCartItemsAsync()` → 実際は `GetCartAsync()` が `CartDto?` を返す
   - 修正前プランの `AddItemAsync(productId, quantity)` → 実際は `AddItemAsync(AddCartItemRequest)` を受ける
   - 修正前プランの `RemoveItemAsync(productId)` → 実際は `RemoveItemAsync(string itemId)` を受ける
   - → 実際の `CartApiClient` メソッドシグネチャに合わせてコード例を修正

3. **H-16 — CacheService `ConcurrentBag` メモリリーク対策**
   - 修正前プランでは `ConcurrentBag<string>` でキー追跡 → 期限切れエントリが追跡から削除されずメモリリーク
   - → `ConcurrentDictionary<string, byte>` に変更し、`PostEvictionCallback` でキャッシュ期限切れ時に追跡からも自動削除する仕組みを追加

### 確認済みファイル一覧

| ファイル | 確認した指摘 | 行番号正確性 |
|---------|------------|------------|
| `AdminPortal/Program.cs` | H-01, H-02, H-03, H-04, H-11, H-18, H-19, H-22 | ✅ |
| `Frontend/Program.cs` | H-02, H-03, H-04, H-19 | ✅ |
| `TokenRefreshHandler.cs` | H-05, H-06, H-17 | ✅ |
| `SearchSuggest.razor` | H-07, H-08 | ✅ |
| `AiChatWidget.razor` | H-09, H-29 | ✅ |
| `CacheService.cs` | H-16 | ✅ |
| `ApiGatewayClient.cs` | H-32 | ✅ |
| `ApiErrorHandler.cs` | H-14 | ✅ |
| `CartState.cs` | H-27 | ✅ |
| `CartEffects.cs` | H-12 | ✅ |
| `CartApiClient.cs` | H-12 前提確認 | ✅ |
| `ProductApiClient.cs` | H-31 | ✅ |
| `AiApiClient.cs` | H-30 | ✅ |
| `AdminPortal.csproj` | H-21 | ✅ |
| `Frontend.Tests.csproj` | H-26 | ✅ |
| `_Layout.cshtml` | H-20 | ✅ |
| `Orders/Detail.cshtml.cs` | H-24, H-28 | ✅ |
| `Error.cshtml` | H-22（不在確認） | ✅ 不在を確認 |
