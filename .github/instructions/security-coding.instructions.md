---
applyTo:
  - "**/Services/**/*.cs"
  - "**/Endpoints/**/*.cs"
---

# セキュリティコーディング規約

本 Instructions は外部入力を受けるレイヤー（`**/Endpoints/**/*.cs`, `**/Services/**/*.cs`）に自動適用される。
Endpoints / Service クラスの作成・編集時に以下のセキュリティ規約を遵守すること。

> **重要**: セキュリティコーディング規約の違反は、他の全てのコーディング規約違反より優先的に是正する。

---

## 1. 入力検証（OWASP A03: インジェクション防止の基盤）

### バリデーション必須化
- **全ての外部入力**（リクエストボディ、パスパラメータ、クエリパラメータ、ヘッダー）にバリデーションを実施する
- Data Annotations（`[Required]`, `[StringLength]`, `[EmailAddress]` 等）または FluentValidation を使用する
- Minimal API では `IValidator<T>` を注入して明示的にバリデーションを実行する

```csharp
// ✅ 良い例: FluentValidation によるバリデーション付き Minimal API
app.MapPost("/users", async (
    [FromBody] CreateUserRequest request,
    IValidator<CreateUserRequest> validator,
    IUserService userService,
    CancellationToken ct) =>
{
    var validationResult = await validator.ValidateAsync(request, ct);
    if (!validationResult.IsValid)
        return Results.ValidationProblem(validationResult.ToDictionary());

    var user = await userService.CreateAsync(request, ct);
    return Results.Created($"/users/{user.Id}", user);
}).AllowAnonymous();

// ✅ 良い例: Data Annotations 付きリクエスト DTO
public record CreateUserRequest(
    [Required(ErrorMessage = "名前は必須です")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "名前は1〜50文字で入力してください")]
    string Name,

    [Required(ErrorMessage = "メールアドレスは必須です")]
    [EmailAddress(ErrorMessage = "有効なメールアドレスを入力してください")]
    [StringLength(255)]
    string Email);
```

### パスパラメータ・クエリパラメータの検証
- パスパラメータの型安全性は Minimal API の型バインディングで保証する
- 追加のバリデーションは Service 層またはガード節で実施する

```csharp
// ✅ 良い例: パスパラメータの検証
app.MapGet("/users/{id}", async (
    string id,
    IUserService userService,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(id))
        return Results.BadRequest("ID は必須です");

    return await userService.FindByIdAsync(id, ct) is { } user
        ? Results.Ok(user)
        : Results.NotFound();
}).RequireAuthorization();
```

### ホワイトリスト検証
- **ブラックリスト（禁止文字列の除外）ではなくホワイトリスト（許可パターンの限定）**で検証する

```csharp
// ❌ 悪い例: ブラックリスト（回避可能）
[RegularExpression(@"^(?!.*<script>).*$")]
public string Comment { get; set; }

// ✅ 良い例: ホワイトリスト（許可パターンのみ）
[RegularExpression(@"^[a-zA-Z0-9_-]{3,30}$", ErrorMessage = "英数字・ハイフン・アンダースコアのみ使用可能です")]
public string Username { get; set; }
```

### サイズ制限（DoS 防止）
- 全ての入力に**サイズ上限**を設定する。無制限の入力は DoS 攻撃のベクトルとなる
- リクエストボディサイズ: Kestrel の `MaxRequestBodySize` で制限
- ファイルアップロード: `IFormFile` の `Length` チェック + Kestrel の `MaxRequestBodySize`
- コレクションパラメータ: `[MaxLength]` で上限を設定

### カスタムバリデーター
- 複雑なビジネスバリデーションは FluentValidation の `AbstractValidator<T>` を使用する

```csharp
// ✅ 良い例: FluentValidation カスタムバリデーター
public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
    }
}
```

---

## 2. SQL インジェクション防止（OWASP A03）

### 絶対禁止事項
- **文字列結合による SQL 構築は絶対に禁止**。違反は Critical 指摘

```csharp
// ❌ Critical 違反: 文字列結合による SQL
var sql = $"SELECT * FROM users WHERE email = '{email}'";
var users = await _context.Users.FromSqlRaw(sql).ToListAsync();

// ❌ Critical 違反: FromSqlRaw での文字列結合
var users = await _context.Users
    .FromSqlRaw("SELECT * FROM users WHERE email = '" + email + "'")
    .ToListAsync();
```

### 安全な方法

| 方法 | 安全性 | 例 |
|------|--------|-----|
| EF Core LINQ クエリ | ✅ 安全（推奨） | `_context.Users.Where(u => u.Email == email)` |
| `FromSqlInterpolated` | ✅ 安全 | `FromSqlInterpolated($"... WHERE email = {email}")` |
| Dapper + パラメータ | ✅ 安全 | `connection.QueryAsync("... WHERE email = @Email", new { Email = email })` |
| `FromSqlRaw` + 文字列結合 | ❌ **絶対禁止** | `FromSqlRaw("... WHERE email = '" + email + "'")` |

```csharp
// ✅ 良い例: EF Core LINQ（推奨）
var user = await _context.Users
    .FirstOrDefaultAsync(u => u.Email == email, ct);

// ✅ 良い例: FromSqlInterpolated（パラメータ化）
var users = await _context.Users
    .FromSqlInterpolated($"SELECT * FROM users WHERE email = {email}")
    .ToListAsync(ct);
```

---

## 3. XSS 防止（OWASP A03）

- ユーザー入力をレスポンスに含める場合は**エスケープ処理**を行う
- JSON レスポンスでは `System.Text.Json` のデフォルトエスケープに依存可能
- **Razor / Blazor を使用する場合**はデフォルトの HTML エスケープを活用する。`Html.Raw()` / `MarkupString` の無検証使用は原則禁止
- ユーザー入力をそのまま HTML 属性、JavaScript コンテキスト、URL パラメータに埋め込まない

```csharp
// ❌ 悪い例: ユーザー入力をそのまま HTML に含める
return Results.Content($"<div>{userInput}</div>", "text/html");

// ✅ 良い例: レスポンスは JSON で返し、エスケープは System.Text.Json に委ねる
return Results.Ok(new UserResponse(user.Name, user.Email));
```

### HTTP セキュリティヘッダー
- 以下のセキュリティヘッダーをミドルウェアで設定する

| ヘッダー | 設定値 | 目的 |
|---------|--------|------|
| `Content-Security-Policy` | `default-src 'self'` | XSS・データインジェクション防止 |
| `X-Content-Type-Options` | `nosniff` | MIME スニッフィング防止 |
| `X-Frame-Options` | `DENY` | クリックジャッキング防止 |
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains` | HTTPS 強制 |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Referer ヘッダーの情報漏洩防止 |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` | ブラウザ機能の不必要な利用を制限 |

```csharp
// ✅ Program.cs でのセキュリティヘッダー設定
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

### CSRF / Anti-forgery 保護（非 API シナリオ）
- **Razor Pages / Blazor Server** を使用する場合は ASP.NET Core の Anti-forgery トークンを有効化する
- `builder.Services.AddAntiforgery()` を登録し、`app.UseAntiforgery()` をミドルウェアに追加する
- Minimal API（JSON のみ）ではブラウザフォーム送信がないため、Anti-forgery は不要（`Content-Type: application/json` の強制で防御可能）

### Mass Assignment / Over-posting 防止（OWASP A08）
- **エンティティ（EF Core Model）を直接 `[FromBody]` で受け取ることは禁止**
- 必ず専用の**リクエスト DTO（record）**を介してデータを受け取り、必要なプロパティのみをエンティティにマッピングする
- `IsAdmin`、`Role`、`PasswordHash` 等のセンシティブなプロパティがクライアント入力で上書きされるリスクを排除する

```csharp
// ❌ Critical 違反: エンティティを直接バインド（Mass Assignment 脆弱性）
app.MapPost("/users", async ([FromBody] User user, AppDbContext context) =>
{
    context.Users.Add(user);  // IsAdmin, Role 等が上書きされる危険
    await context.SaveChangesAsync();
    return Results.Created($"/users/{user.Id}", user);
});

// ✅ 良い例: リクエスト DTO 経由で必要なプロパティのみ受け取る
public record CreateUserRequest(
    [Required, StringLength(50)] string Name,
    [Required, EmailAddress] string Email);

app.MapPost("/users", async (
    [FromBody] CreateUserRequest request,
    IUserService userService,
    CancellationToken ct) =>
{
    var user = await userService.CreateAsync(request, ct);
    return Results.Created($"/users/{user.Id}", user);
});
```

---

## 4. 認証・認可チェック（OWASP A01 / A07）

### エンドポイントの認可必須化
- **Fallback Policy でデフォルト認証必須**とし、公開エンドポイントは `AllowAnonymous()` で明示的に除外する
- ロールベース認可は `RequireAuthorization("PolicyName")` で設定する

```csharp
// ✅ 良い例: Fallback Policy（Program.cs）
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ✅ 良い例: 管理者専用エンドポイント
app.MapDelete("/admin/users/{id}", async (
    string id,
    IUserService userService,
    CancellationToken ct) =>
{
    await userService.DeleteAsync(id, ct);
    return Results.NoContent();
}).RequireAuthorization("AdminOnly");

// ✅ 良い例: 意図的な公開エンドポイント
app.MapGet("/health", () => Results.Ok(new { Status = "UP" }))
    .AllowAnonymous();  // Fallback Policy を明示的に除外
```

### オブジェクトレベルの認可（IDOR 防止）
- パスパラメータの ID でリソースにアクセスする際、**そのリソースがリクエスト者に属するかを検証**する
- 他ユーザーのリソースに ID 推測でアクセスできてはならない

```csharp
// ❌ 悪い例: IDOR 脆弱性（誰でも任意のユーザーデータにアクセス可能）
app.MapGet("/users/{id}/orders", async (string id, IOrderService orderService) =>
    Results.Ok(await orderService.FindByUserIdAsync(id)));

// ✅ 良い例: オーナーシップチェック
app.MapGet("/users/{id}/orders", async (
    string id,
    ClaimsPrincipal user,
    IOrderService orderService,
    CancellationToken ct) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedException();
    if (userId != id && !user.IsInRole("Admin"))
        throw new ForbiddenException();
    return Results.Ok(await orderService.FindByUserIdAsync(id, ct));
}).RequireAuthorization();
```

### ロールベースアクセス制御

| 操作 | 必要なロール | 実装例 |
|------|------------|--------|
| ユーザー情報参照 | 全認証ユーザー | `.RequireAuthorization()` |
| ユーザー情報更新 | 本人または Admin | オーナーシップチェック + `.RequireAuthorization()` |
| ユーザー削除 | Admin のみ | `.RequireAuthorization("AdminOnly")` |
| 管理機能 | Admin のみ | `.RequireAuthorization("AdminOnly")` |

### ブルートフォース攻撃対策
- 認証エンドポイント（`/auth/login` 等）には**レート制限**を必ず設定する
- ASP.NET Core Identity を使用する場合は**アカウントロックアウトポリシー**を有効化する

```csharp
// ✅ Program.cs: 認証エンドポイントのレート制限
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 5;       // 1 分間に 5 回まで
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;        // キューイングなし（即座に 429 を返す）
    });
});

// ✅ Identity ロックアウト設定
builder.Services.Configure<IdentityOptions>(options =>
{
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
});
```

---

## 5. 秘密情報管理（OWASP A02 / A05）

### ハードコード禁止
- **API キー、パスワード、トークン、接続文字列、秘密鍵をソースコードにハードコードすることは絶対禁止**
- 違反は Critical 指摘

```csharp
// ❌ Critical 違反: 秘密情報のハードコード
private const string ApiKey = "sk-1234567890abcdef";
private const string DbPassword = "mysecretpassword";

// ❌ Critical 違反: appsettings.json に直接記述
// "ConnectionStrings": { "Default": "Host=localhost;Password=mysecret" }
```

### 外部化の方法

| 方法 | 適用場面 | セキュリティレベル |
|------|---------|----------------|
| `dotnet user-secrets`（開発時） | ローカル開発 | 中 |
| 環境変数 | コンテナ環境 | 中 |
| Azure Key Vault | エンタープライズ | 最高 |
| .NET Aspire のサービス参照 | マイクロサービス間 | 高（自動構成） |

```csharp
// ✅ 良い例: IConfiguration 経由で取得
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("接続文字列が設定されていません");

// ✅ 良い例: IOptions<T> パターン
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));
```

### テスト環境の秘密情報
- テスト用の秘密情報は `appsettings.Development.json` または `dotnet user-secrets` にのみ記載する
- **テスト用の値は本番値とは異なるダミー値**を使用する

### ログへの秘密情報出力禁止
- ログに秘密情報（パスワード、トークン、API キー）を出力しない
- リクエスト/レスポンスをログに記録する場合、**秘密情報をマスキング**する

---

## 6. エラーレスポンスのセキュリティ（OWASP A05）

### 内部情報の漏洩防止
- エラーレスポンスに以下の内部情報を含めない

| 漏洩してはならない情報 | 例 | リスク |
|---------------------|-----|------|
| スタックトレース | `System.NullReferenceException at ...` | 内部構造の露呈 |
| SQL エラー | `Npgsql.PostgresException: relation "users" does not exist` | DB 構造の露呈 |
| 内部パス | `/app/src/Services/UserService.cs` | サーバー構成の露呈 |
| クラス名 / メソッド名 | `UserService.FindByIdAsync()` | 内部実装の露呈 |
| DB カラム名 | `column 'password_hash' cannot be null` | DB スキーマの露呈 |

```csharp
// ❌ 悪い例: スタックトレースの露出
app.UseExceptionHandler(handler =>
{
    handler.Run(async context =>
    {
        var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        await context.Response.WriteAsync(error?.ToString() ?? "Error");  // スタックトレース漏洩
    });
});

// ✅ 良い例: 安全なエラーレスポンス（グローバル例外ハンドラー）
app.UseExceptionHandler(handler =>
{
    handler.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        logger.LogError(error, "予期しないエラー: {Message}", error?.Message);  // ログには詳細を記録

        var problem = TypedResults.Problem("サーバーでエラーが発生しました", statusCode: 500);
        await problem.ExecuteAsync(context);  // クライアントには最小限の情報のみ
    });
});
```

### ASP.NET Core の設定

```json
// ✅ 良い例: appsettings.json（本番環境）
{
  "DetailedErrors": false,
  "Kestrel": {
    "AddServerHeader": false
  }
}
```

---

## 7. SSRF 防止（OWASP A10）

- ユーザー入力の URL に対してサーバーサイドから HTTP リクエストを送信するパターンを検出する
- URL のホワイトリスト検証を必須化する

```csharp
// ❌ 悪い例: ユーザー入力の URL にそのままリクエスト
app.MapGet("/proxy", async (string url, IHttpClientFactory factory) =>
{
    var client = factory.CreateClient();
    var response = await client.GetStringAsync(url);  // SSRF 脆弱性
    return Results.Ok(response);
});

// ✅ 良い例: 許可ドメインのホワイトリスト検証
app.MapGet("/proxy", async (string url, IHttpClientFactory factory, IUrlValidator validator) =>
{
    if (!validator.IsAllowedDomain(url))
        throw new BusinessException("許可されていないドメインです");

    var client = factory.CreateClient();
    var response = await client.GetStringAsync(url);
    return Results.Ok(response);
});
```

- **内部ネットワーク（`localhost`, `127.0.0.1`, `10.x.x.x`, `192.168.x.x`）へのアクセスを禁止**する

---

## 8. セキュリティログ（OWASP A09）

### 記録すべきセキュリティイベント

| イベント | ログレベル | 記録内容 |
|---------|----------|---------|
| 認証成功 | Information | ユーザー ID、IP アドレス |
| 認証失敗 | Warning | 試行ユーザー名（**パスワードは絶対に記録しない**）、IP、失敗理由 |
| 認可失敗（権限不足） | Warning | ユーザー ID、アクセス先リソース、必要な権限 |
| 入力バリデーション失敗 | Warning | リクエスト元 IP、失敗フィールド（**入力値自体は記録しない**） |
| データの削除 / 重要な更新 | Information | 操作者、対象リソース |

```csharp
// ✅ 良い例: セキュリティイベントのログ出力
_logger.LogWarning("認証失敗: Email={MaskedEmail}, IP={IpAddress}, Reason={Reason}",
    MaskEmail(email), context.Connection.RemoteIpAddress, "パスワード不一致");
```

### ログに含めてはならない情報
- パスワード、トークン、API キー、クレジットカード番号
- 完全な個人情報（マスキング必須: `user@example.com` → `u***@example.com`）
- セッション ID / JWT トークン（セッションハイジャックのリスク）

---

## 9. パスワードハッシュ

```csharp
// ✅ ASP.NET Core Identity のデフォルト（PBKDF2）を使用
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ✅ カスタムハッシュが必要な場合（Argon2 等）
builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, Argon2PasswordHasher>();
```

- **平文パスワードの保存は絶対禁止**
- **MD5 / SHA1 / SHA256 の直接使用は禁止**（レインボーテーブル攻撃に脆弱）
- ASP.NET Core Identity の `IPasswordHasher<T>` を使用する

---

## 10. 禁止事項チェックリスト

| # | 禁止事項 | OWASP | 重要度 | 検出パターン |
|---|---------|-------|--------|------------|
| 1 | `FromSqlRaw` での文字列結合 SQL | A03 | Critical | `FromSqlRaw("..." + param)`, `FromSqlRaw($"...{param}")` |
| 2 | 秘密情報のハードコード | A02 | Critical | `password = "..."`, `apiKey = "..."`, `token = "..."` |
| 3 | バリデーションなしの外部入力受付 | A03 | High | `IValidator<T>` / Data Annotations なし |
| 4 | `RequireAuthorization()` なしのエンドポイント（意図的明記なし） | A01 | High | `.AllowAnonymous()` も `.RequireAuthorization()` もなし |
| 5 | スタックトレースのクライアント返却 | A05 | High | `error.ToString()` をレスポンスに含める |
| 6 | ユーザー入力 URL への無検証サーバーサイドリクエスト | A10 | High | `HttpClient.GetAsync(userInput)` |
| 7 | `Html.Raw()` / `MarkupString` の無検証使用 | A03 | High | Razor / Blazor 内でのエスケープなし出力 |
| 8 | ログへの秘密情報・個人情報出力 | A09 | High | `_logger.LogInformation("password: {Password}", password)` |
| 9 | セキュリティ用途での `Random` 使用 | A02 | Medium | トークン・ID 生成に `Random` を使用（`RandomNumberGenerator` を使用すべき） |
| 10 | CORS の `*` ワイルドカード設定 | A05 | Medium | `.AllowAnyOrigin()` |
| 11 | エンティティを `[FromBody]` で直接バインド（Mass Assignment） | A08 | Critical | `[FromBody] User user` 等（DTO を使用すべき） |
| 12 | Anti-forgery 未設定の Razor / Blazor フォーム送信 | A05 | High | `AddAntiforgery()` / `UseAntiforgery()` の欠如 |
| 13 | 認証エンドポイントにレート制限なし | A07 | High | `/auth/login` に `AddRateLimiter` / `RequireRateLimiting` なし |
