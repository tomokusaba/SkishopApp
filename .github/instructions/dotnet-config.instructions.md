---
applyTo:
  - "**/appsettings*.json"
  - "**/Program.cs"
---

# ASP.NET Core 設定ファイル Instructions

本 Instructions は `**/appsettings*.json` および `**/Program.cs` に自動適用される。ASP.NET Core の設定ファイル作成・編集時に以下のチェック観点を遵守すること。

---

## 1. 秘密情報の外部化

### 絶対禁止事項
- **API キー、パスワード、トークン、接続文字列、秘密鍵を設定ファイルに直接記述することは絶対禁止**
- 違反は Critical 指摘

```jsonc
// ❌ Critical 違反: 秘密情報の直接記述
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Password=mysecretpassword"
  },
  "OpenAI": {
    "ApiKey": "sk-1234567890abcdef"
  }
}

// ✅ 良い例: 秘密情報を含めない（環境変数 / user-secrets で管理）
{
  "ConnectionStrings": {
    "DefaultConnection": ""
  }
}
```

### 外部化の方法

| 方法 | 適用場面 | セキュリティレベル |
|------|---------|----------------|
| `dotnet user-secrets` | ローカル開発 | 中（暗号化なし、ユーザーディレクトリに保存） |
| 環境変数 | コンテナ環境 | 中（プロセス一覧で見える可能性あり） |
| Azure Key Vault | エンタープライズ | 最高 |
| .NET Aspire のサービス参照 | マイクロサービス間 | 高（接続文字列の自動構成） |

```bash
# ✅ 開発時: dotnet user-secrets を使用
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=skishop;Username=dev;Password=devpass"
dotnet user-secrets set "Jwt:SecretKey" "dev-only-secret-key-minimum-32-chars"
```

```csharp
// ✅ Program.cs での接続文字列取得
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("接続文字列が設定されていません");
```

### テスト環境の秘密情報
- テスト用の秘密情報は `appsettings.Development.json` または `dotnet user-secrets` にのみ記載する
- **テスト用の値は本番値とは異なるダミー値**を使用する
- テスト用設定ファイルに本番の秘密情報を絶対に記載しない

---

## 2. プロファイル分離

### プロファイル構成

| ファイル | 用途 | 含める設定 |
|---------|------|-----------|
| `appsettings.json` | **デフォルト（全環境共通）** | アプリケーション名、共通設定、安全なデフォルト値 |
| `appsettings.Development.json` | 開発環境 | ローカル DB、Debug ログ、詳細エラー |
| `appsettings.Staging.json` | ステージング環境 | ステージング DB、Information ログ |
| `appsettings.Production.json` | 本番環境 | 環境変数参照、Warning ログ、セキュリティ強化 |

### プロファイル設計の原則
- **デフォルト（appsettings.json）に本番設定を含めない**。デフォルトは安全なフォールバックとする
- **デフォルトに秘密情報を含めない**
- デフォルトの設定は**安全側に倒す**（詳細エラー無効、ログ最小限）

```jsonc
// ✅ 良い例: appsettings.json（デフォルト = 安全側）
{
  "AllowedHosts": "*",
  "DetailedErrors": false,
  "Kestrel": {
    "AddServerHeader": false
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "SkiShop": "Information"
    }
  }
}
```

```jsonc
// ✅ 良い例: appsettings.Development.json（開発向け緩和）
{
  "DetailedErrors": true,
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Debug",
      "SkiShop": "Debug",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

---

## 3. ヘルスチェック設定

### Liveness / Readiness プローブ

```csharp
// ✅ Program.cs でのヘルスチェック設定
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql", tags: ["ready"])
    .AddRedis(redisConnectionString, name: "redis", tags: ["ready"]);

// Liveness: アプリケーションが生きているか（常に 200）
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

// Readiness: 依存サービスが準備完了しているか
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();
```

---

## 4. ログ設定

### ログレベルの環境別設定

| 環境 | Default | Microsoft.AspNetCore | SkiShop | EF Core |
|------|---------|---------------------|---------|---------|
| 本番（Production） | `Warning` | `Warning` | `Information` | `Warning` |
| ステージング | `Information` | `Warning` | `Information` | `Warning` |
| 開発（Development） | `Information` | `Debug` | `Debug` | `Information` |

### Serilog の構成

```csharp
// ✅ Program.cs — Serilog 設定
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "AuthService")
        .WriteTo.Console(new CompactJsonFormatter()));
```

### ログの禁止事項
- **EF Core の SQL ログは本番で無効**（パラメータ値の漏洩リスク）
- 個人情報をログに出力する設定にしない

```jsonc
// ❌ 悪い例: 本番で EF Core SQL ログを有効化
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

### 構造化ログ（推奨）
- JSON 形式のログ出力を推奨する（ログ集約ツールとの統合を容易にする）
- Serilog + `CompactJsonFormatter` を標準構成とする

---

## 5. Kestrel / サーバー設定

### リクエストサイズ制限

```csharp
// ✅ Program.cs: Kestrel のリクエストサイズ制限（DoS 防止）
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024;  // 10MB
    options.AddServerHeader = false;  // Server ヘッダーの非公開
});
```

### グレースフルシャットダウン

```csharp
// ✅ Program.cs: グレースフルシャットダウン
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});
```

---

## 6. 型安全な設定（IOptions<T> パターン）

### 設定クラスの定義

```csharp
// ✅ 設定クラス（record 推奨）
public record JwtSettings(
    string Issuer,
    string Audience,
    int ExpirationMinutes = 60);

public record MailSettings(
    string Host,
    int Port,
    string FromAddress);
```

### Program.cs での登録

```csharp
// ✅ IOptions<T> パターンで設定を注入（起動時バリデーション付き）
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateDataAnnotations()   // Data Annotations によるバリデーション
    .ValidateOnStart();          // 起動時に設定不備を検出（本番障害の予防）

builder.Services.AddOptions<MailSettings>()
    .Bind(builder.Configuration.GetSection("Mail"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

> **重要**: `ValidateOnStart()` を付与すると、アプリケーション起動時に設定値のバリデーションが実行される。
> 必須項目の欠落や型不整合を**デプロイ直後**に検出できるため、エンタープライズ運用では必須とする。

### IOptions<T> のライフサイクル選択

| インターフェース | ライフサイクル | 用途 |
|---|---|---|
| `IOptions<T>` | Singleton（起動時に 1 回のみ読み込み） | 変更不要の設定（JWT、DB 接続等） |
| `IOptionsSnapshot<T>` | Scoped（リクエストごとに最新値を取得） | リクエスト単位で最新設定が必要な場合 |
| `IOptionsMonitor<T>` | Singleton（変更通知コールバック付き） | 設定変更をリアルタイムに反映（ホットリロード） |

```csharp
// ✅ 設定がリクエスト中に変わる可能性がある場合
public class FeatureFlagService(IOptionsSnapshot<FeatureFlags> options) { }

// ✅ 設定変更をリアルタイムに検知する必要がある場合
public class RateLimitService(IOptionsMonitor<RateLimitSettings> options)
{
    // options.CurrentValue で最新値を取得
    // options.OnChange(settings => { /* 変更時のコールバック */ });
}
```

### Service での使用

```csharp
// ✅ IOptions<T> で注入
public class MailService(
    IOptions<MailSettings> mailOptions,
    ILogger<MailService> logger) : IMailService
{
    private readonly MailSettings _settings = mailOptions.Value;

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        logger.LogInformation("メール送信: To={MaskedTo}, Subject={Subject}",
            MaskEmail(to), subject);
        // _settings.Host, _settings.Port を使用
    }
}
```

### 禁止パターン

```csharp
// ❌ 禁止: IConfiguration を直接注入してマジックストリングで取得
public class MailService(IConfiguration config)
{
    private readonly string _host = config["Mail:Host"]!;  // 型安全でない
}
```

---

## 7. 認証・認可設定

```csharp
// ✅ Program.cs: JWT Bearer 認証 + 認可ポリシー
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("UserOrAdmin", p => p.RequireRole("User", "Admin"));
    // Fallback: 全エンドポイントに認証必須（AllowAnonymous で明示的に除外）
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

---

## 8. セキュリティ関連設定

### セキュリティヘッダー

```csharp
// ✅ Program.cs: セキュリティヘッダーの付与
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
```

### CORS 設定

```csharp
// ✅ 明示的なオリジン指定
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins("https://skishop.example.com")
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// ❌ 禁止: ワイルドカード
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin());  // 禁止
});
```

### レート制限

```csharp
// ✅ Program.cs: レート制限の設定
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
    });
});

app.UseRateLimiter();
```

---

## 9. 環境別設定の差分チェック

設定ファイル編集時は、以下の設定が環境間で意図的に異なることを確認する:

| 設定 | Development | Staging | Production | 検証ポイント |
|------|-------------|---------|------------|------------|
| `Logging:LogLevel:Default` | Information | Information | **Warning** | 本番で過剰なログを出力していないか |
| `DetailedErrors` | true | false | **false** | 本番で詳細エラーを出力していないか |
| `Kestrel:AddServerHeader` | false | false | **false** | Server ヘッダーが公開されていないか |
| EF Core SQL ログ | Information | Warning | **Warning** | 本番で SQL がログに出力されていないか |

---

## 10. ミドルウェアパイプライン順序（厳守）

ASP.NET Core のミドルウェアは**登録順序が動作に直結**する。以下の順序を厳守すること:

```csharp
// ✅ Program.cs — ミドルウェア登録順序（この順序を変更しない）
var app = builder.Build();

// 1. 例外ハンドラー（最も外側で全例外をキャッチ）
app.UseExceptionHandler();

// 2. セキュリティヘッダー
app.UseHsts();
app.UseHttpsRedirection();

// 3. Correlation ID ミドルウェア
app.UseCorrelationId();

// 4. Serilog リクエストログ
app.UseSerilogRequestLogging();

// 5. CORS（認証より前に配置）
app.UseCors();

// 6. 認証・認可（この順序は絶対）
app.UseAuthentication();
app.UseAuthorization();

// 7. レート制限（認証後に配置し、ユーザー単位の制限を可能に）
app.UseRateLimiter();

// 8. エンドポイントマッピング
app.MapProductEndpoints();
app.MapAuthEndpoints();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");

app.Run();
```

**禁止パターン**:
- `UseAuthentication()` を `UseAuthorization()` の後に配置する
- `UseExceptionHandler()` をパイプラインの途中に配置する
- `UseCors()` を `UseAuthentication()` の後に配置する

---

## 11. 禁止事項チェックリスト

| # | 禁止事項 | 重要度 | 理由 |
|---|---------|--------|------|
| 1 | 秘密情報（パスワード、API キー等）の appsettings.json 直接記述 | Critical | 情報漏洩リスク |
| 2 | 本番で `DetailedErrors: true` | Critical | スタックトレースによる内部構造漏洩 |
| 3 | 本番で EF Core SQL ログを有効化 | High | SQL パラメータ値の漏洩 |
| 4 | デフォルト（appsettings.json）に本番の秘密情報を含める | High | 環境未指定時に漏洩 |
| 5 | `Kestrel.AddServerHeader = true`（デフォルト） | Medium | サーバー情報の漏洩 |
| 6 | `IConfiguration` の直接注入によるマジックストリング | Medium | 型安全でない。`IOptions<T>` を使用 |
| 7 | ミドルウェアパイプラインの順序違反 | High | 認証・認可が正しく動作しない |
| 8 | グレースフルシャットダウン未設定 | Medium | 処理中リクエストの断絶 |
| 9 | ヘルスチェック未設定 | Medium | 障害検知の遅延 |
| 10 | CORS ワイルドカード設定 | Medium | セキュリティリスク |
| 11 | `ValidateOnStart()` 未設定の `IOptions<T>` | Medium | 設定不備が実行時まで検出されない |
| 12 | `IConfiguration` の直接注入（`config["Key"]`） | High | 型安全でない・テスト困難。`IOptions<T>` を使用 |
