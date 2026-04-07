---
applyTo: "**/*.cs"
---

# C# コーディング規約

本 Instructions は `**/*.cs` に自動適用される。全ての C# ファイルの作成・編集時に以下の規約を遵守すること。

---

## 1. 命名規則

### 基本ルール

| 対象 | 規約 | 良い例 | 悪い例 |
|------|------|--------|--------|
| クラス名 | PascalCase | `UserService`, `OrderController` | `userService`, `order_controller` |
| インターフェース名 | `I` プレフィックス + PascalCase | `IUserRepository`, `IOrderService` | `UserRepository`（インターフェース） |
| メソッド名 | PascalCase + 動詞始まり + `Async` サフィックス（非同期） | `FindByEmailAsync`, `CreateOrderAsync` | `userSearch`, `data` |
| プロパティ | PascalCase | `UserName`, `OrderItems` | `user_name`, `userName` |
| ローカル変数 | camelCase（意味のある名前） | `userName`, `orderItems` | `x`, `tmp`, `data` |
| パラメータ | camelCase | `userId`, `cancellationToken` | `UserId`, `CancellationToken_` |
| プライベートフィールド | `_camelCase` | `_userRepository`, `_logger` | `userRepository`, `m_logger` |
| 定数（const） | PascalCase | `MaxRetryCount`, `DefaultPageSize` | `MAX_RETRY_COUNT`, `maxRetryCount` |
| enum 値 | PascalCase | `OrderStatus.Pending` | `ORDER_STATUS_PENDING` |
| bool プロパティ | `Is/Has/Can/Should` プレフィックス | `IsActive`, `HasPermission` | `active`, `checkPerm` |
| コレクション変数 | 複数形 | `users`, `orderItems` | `userList`, `itemsArr` |
| 名前空間 | `<Company>.<Product>.<Feature>` | `SkiShop.Auth.Services` | `auth_services` |

### 命名のアンチパターン

| アンチパターン | 問題点 | 改善例 |
|-------------|--------|--------|
| `Manager`, `Helper`, `Util` の安易な使用 | 責務が曖昧になる | 具体的な責務を表す名前に変更 |
| 1〜2 文字の変数名（`x`, `s`） | 意味が読み取れない | 意味のある名前に変更（ラムダの引数を除く） |
| 型名の繰り返し（`userList`, `nameString`） | 冗長 | `users`, `name` に簡潔化 |
| 略語の多用（`dept`, `usr`, `mgr`） | 可読性低下 | `department`, `user`, `manager` に展開 |

---

## 2. プロジェクト構成

- 各マイクロサービスは `<ServiceName>/` 配下に以下のディレクトリを配置する
- **レイヤードアーキテクチャの依存方向を厳守**: Endpoints → Services → Repositories

```
<ServiceName>/
├── <ServiceName>.csproj
├── Program.cs                    # エントリポイント・DI 登録・ミドルウェア設定
├── Endpoints/                    # Minimal API エンドポイント定義
├── Services/                     # ビジネスロジック
│   └── Interfaces/               # Service インターフェース
├── Repositories/                 # データアクセス層
│   └── Interfaces/               # Repository インターフェース
├── Models/                       # EF Core エンティティ
├── DTOs/
│   ├── Requests/                 # リクエスト DTO (record)
│   └── Responses/                # レスポンス DTO (record)
├── Configurations/               # 設定クラス (IOptions<T>)
├── Infrastructure/
│   └── Persistence/
│       └── AppDbContext.cs       # EF Core DbContext
├── Migrations/                   # EF Core 自動生成マイグレーション
├── appsettings.json
├── appsettings.Development.json
└── appsettings.Production.json
```

- **依存方向の違反を禁止**: Endpoints が Repository を直接呼び出す、Repository が Endpoints に依存する等
- **循環依存の禁止**: プロジェクト間・名前空間間の双方向依存は許容しない

---

## 3. C# 14 機能の積極活用

プロジェクトの技術スタック（C# 14 / .NET 10）に基づき、モダン機能を積極的に活用する。

### record 型（推奨）
- 不変 DTO、Value Object、設定パラメータには record 型を使用する
- 従来のクラス + getter/setter を record に置換する

```csharp
// ❌ 悪い例: 従来のクラス
public class UserResponse
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

// ✅ 良い例: record 型
public record UserResponse(string Name, string Email);
```

### sealed record（推奨）
- 限定された型階層には sealed record を使用する

```csharp
// ✅ 良い例: sealed record による限定型階層
public abstract record PaymentResult;
public sealed record PaymentSuccess(string TransactionId) : PaymentResult;
public sealed record PaymentFailure(string ErrorCode, string Message) : PaymentResult;
public sealed record PaymentPending(string ReferenceId) : PaymentResult;
```

### パターンマッチング（推奨）

```csharp
// ❌ 悪い例: 型キャスト
if (obj is string)
{
    var s = (string)obj;
    Process(s);
}

// ✅ 良い例: パターンマッチング
if (obj is string s)
{
    Process(s);
}
```

### switch 式（推奨）

```csharp
// ❌ 悪い例: 旧来の switch
string label;
switch (status)
{
    case OrderStatus.Pending: label = "処理中"; break;
    case OrderStatus.Completed: label = "完了"; break;
    default: label = "不明"; break;
}

// ✅ 良い例: switch 式
var label = status switch
{
    OrderStatus.Pending => "処理中",
    OrderStatus.Completed => "完了",
    _ => "不明"
};
```

### primary constructor（推奨）
- Service / Repository の DI にはprimary constructorを使用する

```csharp
// ✅ 良い例: primary constructor による DI
public class UserService(
    IUserRepository userRepository,
    ILogger<UserService> logger) : IUserService
{
    public async Task<UserDto?> FindByIdAsync(string id, CancellationToken ct = default)
    {
        logger.LogInformation("ユーザー検索: {UserId}", id);
        return await userRepository.FindByIdAsync(id, ct);
    }
}
```

### null 条件代入 `?.=`（C# 14 新機能）

C# 14 で導入された `?.=` 演算子は、左辺のレシーバが null でない場合にのみ代入を行う。
C# 8 の `??=`（null 合体代入）とは異なる演算子であるため、混同しないこと。

```csharp
// ✅ C# 14: null 条件代入 ?.= — レシーバが null でなければ代入
user?.LastLoginAt = DateTime.UtcNow;              // user が null でなければ代入
user?.Profile?.DisplayName = "Anonymous";          // user と Profile が null でなければ代入

// ✅ C# 8: null 合体代入 ??= — 変数自体が null なら代入（引き続き有効）
user.LastLoginAt ??= DateTime.UtcNow;              // LastLoginAt が null なら代入
cachedValue ??= await FetchFromDatabaseAsync(ct);  // キャッシュが null なら取得

// ❌ 混同禁止: ?.= と ??= は目的が異なる
// ?.=  → レシーバの null チェック（代入するかどうか）
// ??=  → 変数値の null チェック（値を初期化するかどうか）
```

### extension types / extension blocks（C# 14 新機能）

C# 14 で導入された extension types は、従来の `static class` + `this` 拡張メソッドを置換する新構文。
型に対してメソッド・プロパティ・演算子を追加できる。

```csharp
// ✅ 良い例: 名前付き extension type（推奨 — 再利用性が高い）
implicit extension StringValidation for string
{
    public bool IsValidEmail => this.Contains('@') && this.Contains('.');
    public string Truncate(int maxLength)
        => this.Length <= maxLength ? this : this[..maxLength] + "...";
}

// ✅ 良い例: extension block（匿名形式 — 簡潔な記述用）
extension(string s)
{
    public bool IsValidEmail => s.Contains('@') && s.Contains('.');
}

// ❌ 非推奨: 従来の拡張メソッド（新規コードでは extension types を使用）
public static class StringExtensions
{
    public static bool IsValidEmail(this string s) => s.Contains('@') && s.Contains('.');
}
```

### `field` キーワード（C# 14 新機能）

自動プロパティのバッキングフィールドを直接参照できる。バリデーション付きプロパティや変換ロジックの記述が簡潔になる。

```csharp
// ✅ 良い例: field キーワードによる自動プロパティのバッキングフィールド参照
public string Name
{
    get => field;
    set => field = value?.Trim() ?? throw new ArgumentNullException(nameof(value));
}

// ✅ 良い例: field でバリデーション付きプロパティ
public decimal Price
{
    get => field;
    set => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
}
```

### コレクション式（C# 12+）

```csharp
// ✅ 良い例: コレクション式 — 型を問わず統一的な初期化
List<string> names = ["Alice", "Bob"];
int[] numbers = [1, 2, 3];
Span<int> span = [1, 2, 3];
ImmutableArray<string> immutable = ["a", "b"];

// ✅ スプレッド演算子
int[] combined = [..firstArray, ..secondArray, 42];

// ❌ 悪い例: 旧構文
var names = new List<string> { "Alice", "Bob" };
```

### `params` コレクション（C# 13+）

```csharp
// ✅ 良い例: params に ReadOnlySpan / IEnumerable 等を使用可能（C# 13+）
public void LogMessages(params ReadOnlySpan<string> messages)
{
    foreach (var msg in messages)
        _logger.LogInformation("メッセージ: {Message}", msg);
}

// ✅ 呼び出し側 — 配列アロケーションなし
LogMessages("開始", "処理中", "完了");
```

### 高パフォーマンスログ `[LoggerMessage]`（.NET 8+、必須推奨）

ホットパスでのログ出力には `[LoggerMessage]` ソースジェネレーターを使用し、アロケーションを削減する:

```csharp
// ✅ 推奨: ソースジェネレーターによる高パフォーマンスログ
public partial class OrderService(
    IOrderRepository orderRepository,
    ILogger<OrderService> logger) : IOrderService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "注文作成: OrderId={OrderId}, UserId={UserId}")]
    partial void LogOrderCreated(string orderId, string userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "注文作成失敗: OrderId={OrderId}")]
    partial void LogOrderCreationFailed(Exception ex, string orderId);

    public async Task<Order> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct)
    {
        // ...
        LogOrderCreated(order.Id, request.UserId);  // アロケーションフリー
        return order;
    }
}

// ❌ ホットパスでは避ける: 毎回ボクシング/アロケーションが発生
_logger.LogInformation("注文作成: OrderId={OrderId}", orderId);
```

**ルール**: ループ内・リクエストごとに呼ばれるログ出力には `[LoggerMessage]` を優先使用する。設定・起動時の 1 回限りのログは従来の `_logger.LogXxx()` で問題ない。

### `TimeProvider` 抽象化（.NET 8+、テスタビリティ必須）

`DateTime.UtcNow` / `DateTimeOffset.UtcNow` の直接使用はテストで時刻を固定できない。`TimeProvider` を DI でインジェクションし、テスト時にモック可能にする:

```csharp
// ✅ 良い例: TimeProvider を使用（テスト可能）
public class TokenService(TimeProvider timeProvider, ILogger<TokenService> logger) : ITokenService
{
    public bool IsTokenExpired(Token token)
        => token.ExpiresAt < timeProvider.GetUtcNow();

    public Token GenerateToken(string userId)
        => new Token(userId, timeProvider.GetUtcNow().AddHours(1));
}

// ✅ Program.cs での登録
builder.Services.AddSingleton(TimeProvider.System);  // 本番: システム時刻

// ✅ テストでの使用
var fakeTimeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
var service = new TokenService(fakeTimeProvider, logger);
fakeTimeProvider.Advance(TimeSpan.FromHours(2));  // 時刻を進める
Assert.True(service.IsTokenExpired(token));

// ❌ 非推奨: DateTime.UtcNow を直接使用（テスト不可能）
public bool IsTokenExpired(Token token)
    => token.ExpiresAt < DateTime.UtcNow;  // テストで時刻を制御できない
```

**ルール**: 時刻取得が必要なサービスでは `TimeProvider` を DI でインジェクションする。エンティティの `CreatedAt` / `UpdatedAt` の設定も `TimeProvider` 経由で行うことを推奨する。

---

## 4. 例外処理

### 基本原則
- **例外の握りつぶしは絶対禁止**: `catch` ブロック内で必ずログ出力または再スローを行う
- カスタム例外は用途に応じた基底クラスを継承する
- グローバル例外ハンドラーで統一的に HTTP ステータスコードにマッピングする

```csharp
// ❌ Critical 違反: 例外の握りつぶし
try
{
    await service.ExecuteAsync(ct);
}
catch (Exception)
{
    // 何もしない
}

// ✅ 良い例: ログ出力 + 再スロー
try
{
    await service.ExecuteAsync(ct);
}
catch (InvalidOperationException ex)
{
    _logger.LogError(ex, "サービス実行エラー: {Message}", ex.Message);
    throw new BusinessException("処理に失敗しました", ex);
}
```

### 例外の粒度
- `catch (Exception)` で全例外をキャッチしない。**具体的な例外型**でキャッチする
- 再スロー時は**原因例外（inner exception）を保持**する: `throw new XxxException(message, ex)`

### 例外階層の設計

```csharp
// ✅ 推奨: ドメイン固有の例外階層
// NotFoundException       → HTTP 404
// BusinessException       → HTTP 422（ビジネスルール違反）
// UnauthorizedException   → HTTP 401
// ForbiddenException      → HTTP 403
// ConcurrencyException    → HTTP 409（楽観的ロック競合）

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
    public NotFoundException(string message, Exception innerException) : base(message, innerException) { }
}

public class BusinessException : Exception
{
    public BusinessException(string message) : base(message) { }
    public BusinessException(string message, Exception innerException) : base(message, innerException) { }
}
```

### グローバル例外ハンドラー（Program.cs）

```csharp
// ✅ 推奨（ASP.NET Core 8+）: IExceptionHandler による構造化された例外ハンドリング
// DI 対応・テスタブル・複数ハンドラーのチェイン可能
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken ct)
    {
        var (statusCode, message) = exception switch
        {
            NotFoundException e    => (404, e.Message),
            BusinessException e    => (422, e.Message),
            UnauthorizedException  => (401, "認証が必要です"),
            ForbiddenException     => (403, "アクセスが拒否されました"),
            ConcurrencyException e => (409, e.Message),
            _                      => (500, "内部エラーが発生しました")
        };

        if (statusCode >= 500)
            logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        else
            logger.LogWarning("Handled exception: {ExceptionType} - {Message}",
                exception.GetType().Name, exception.Message);

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Detail = message,
            Title = ReasonPhrases.GetReasonPhrase(statusCode)
        }, ct);
        return true;
    }
}

// ✅ Program.cs での登録
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
// ...
app.UseExceptionHandler();
```

> **注意**: ASP.NET Core 8 以前の `UseExceptionHandler(lambda)` パターンも動作するが、
> `IExceptionHandler` は DI 対応・テスト容易・チェイン可能なため、新規実装では `IExceptionHandler` を使用すること。

---

## 5. ログ出力

### 基本ルール
- **`Console.WriteLine` / `Console.Error.WriteLine` は絶対禁止**。`ILogger<T>` を使用する
- パラメータは**メッセージテンプレート `{Placeholder}` を使用**（文字列補間禁止）

```csharp
// ❌ Critical 違反
Console.WriteLine($"User created: {user.Name}");

// ❌ 悪い例: 文字列補間
_logger.LogInformation($"User created: {user.Name}");

// ✅ 良い例: メッセージテンプレート
_logger.LogInformation("ユーザーを作成しました: UserId={UserId}", user.Id);
```

### ログレベルの使い分け

| レベル | 用途 | 本番環境 | 例 |
|--------|------|---------|-----|
| `Error` | システム障害、回復不能なエラー | 有効 | DB 接続不能、外部 API の致命的障害 |
| `Warning` | 想定外だが回復可能な状態 | 有効 | リトライ成功、フォールバック動作 |
| `Information` | ビジネスイベント、処理の開始/完了 | 有効 | ユーザー作成、注文処理完了 |
| `Debug` | デバッグ情報 | **無効** | メソッド引数、中間計算結果 |
| `Trace` | 詳細トレース | **無効** | 全メソッド呼び出し記録 |

### ログの禁止事項
- **個人情報をログに出力しない**: パスワード、トークン、クレジットカード番号、メールアドレス全文等
- **秘密情報をログに出力しない**: API キー、秘密鍵、接続文字列等
- **大量データをログに出力しない**: コレクションの全要素ダンプ、大きなリクエストボディ等

### 例外ログ
- 例外をログに記録する際は**スタックトレース全体を含める**（第 1 引数に例外オブジェクト）

```csharp
// ❌ 悪い例: メッセージのみ（スタックトレースなし）
_logger.LogError("処理エラー: {Message}", ex.Message);

// ✅ 良い例: スタックトレースを含む
_logger.LogError(ex, "処理エラー: {Message}", ex.Message);
```

---

## 6. Null Safety

### 基本原則
- Nullable Reference Types を有効化（`<Nullable>enable</Nullable>`）
- **コレクションの代わりに `null` を返さない**。空コレクション `[]`（C# 12+）または `Enumerable.Empty<T>()` を返す
- 引数の null チェックは `ArgumentNullException.ThrowIfNull()` を使用する

```csharp
// ❌ 悪い例: null / null! の乱用
return null;               // コレクション型の場合
list!.FirstOrDefault();    // null 強制許容演算子の乱用

// ✅ 良い例: 空コレクション
return [];                 // C# 12+
return Enumerable.Empty<Product>();

// ✅ 良い例: null チェック + 例外
var user = await _repository.FindByIdAsync(id, ct)
    ?? throw new NotFoundException($"User {id} not found");

// ✅ 良い例: 引数の null チェック
ArgumentNullException.ThrowIfNull(request);
ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
```

---

## 7. DI（依存性注入）規約

### コンストラクタインジェクション必須
- **プロパティインジェクション（`[Inject]` 等）は禁止**
- primary constructor によるコンストラクタインジェクションを推奨
- `new UserService()` のような直接インスタンス化は禁止（DI コンテナ経由のみ）

```csharp
// ❌ 悪い例: プロパティインジェクション
public class UserService
{
    [Inject]
    public IUserRepository UserRepository { get; set; }  // 禁止
}

// ❌ 悪い例: 直接インスタンス化
var service = new UserService();  // DI 管理外

// ✅ 良い例: primary constructor
public class UserService(
    IUserRepository userRepository,
    ILogger<UserService> logger) : IUserService
{
    // ...
}
```

### DI 登録（Program.cs）

```csharp
// ✅ Scoped が基本（リクエスト単位のライフサイクル）
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

// Singleton: アプリケーション全体で 1 インスタンス（スレッドセーフ必須）
// Transient: 呼び出しごとに新規インスタンス（軽量オブジェクトのみ）
```

### OpenAPI 設定（.NET 10 方式）

**.NET 10 では `AddOpenApi()` + `MapOpenApi()` によりサービスレベルで OpenAPI ドキュメントを自動生成するため、個別エンドポイントへの `.WithOpenApi()` は不要。**

```csharp
// ✅ .NET 10 推奨: Program.cs でのサービスレベル OpenAPI 設定
builder.Services.AddOpenApi();  // OpenAPI ドキュメント生成を有効化

var app = builder.Build();
app.MapOpenApi();  // /openapi/v1.json エンドポイントを公開

// ❌ .NET 10 では不要（禁止）: 個別エンドポイントへの .WithOpenApi()
group.MapGet("/", GetAllProducts).WithOpenApi();  // 不要
```

### Keyed Service（.NET 8+）

同一インターフェースに複数の実装を登録し、キーで区別する場合に使用する:

```csharp
// ✅ 良い例: Keyed Service 登録
builder.Services.AddKeyedScoped<IPaymentProcessor, StripeProcessor>("stripe");
builder.Services.AddKeyedScoped<IPaymentProcessor, PayPalProcessor>("paypal");

// ✅ 良い例: Keyed Service のインジェクション
public class CheckoutService(
    [FromKeyedServices("stripe")] IPaymentProcessor stripeProcessor,
    ILogger<CheckoutService> logger) : ICheckoutService
{
    // ...
}

// ❌ 非推奨: Factory パターンで手動解決（Keyed Service で置換可能）
public class PaymentProcessorFactory
{
    public IPaymentProcessor Create(string provider) => provider switch { ... };
}
```

---

## 8. CancellationToken の必須化

全ての `async` メソッドのシグネチャに `CancellationToken ct = default` を含め、下位呼び出しに伝搬する:

```csharp
// ✅ 正しい: CancellationToken を シグネチャに含め、すべての下位呼び出しに伝搬
public async Task<UserDto?> FindByEmailAsync(string email, CancellationToken ct = default)
    => await _context.Users
        .AsNoTracking()
        .FirstOrDefaultAsync(u => u.Email == email, ct);

// ❌ 禁止: CancellationToken を省略
public async Task<UserDto?> FindByEmailAsync(string email)
    => await _context.Users.FirstOrDefaultAsync(u => u.Email == email);  // ct 未伝搬
```

**ルール**:
- Endpoint（Minimal API）では自動バインドされた `CancellationToken ct` を受け取り、Service → Repository へ伝搬
- `IHttpClientFactory` 経由の HTTP 呼び出しにも必ず `ct` を渡す
- `BackgroundService.ExecuteAsync` では `stoppingToken` を全下位呼び出しに伝搬

---

## 9. 非同期プログラミング

### async/await の正しい使用
- `Thread.Sleep()` は禁止。`await Task.Delay()` を使用する
- `.Result` / `.Wait()` は禁止（デッドロックリスク）。`await` を使用する
- 非同期メソッド名には `Async` サフィックスを付与する

```csharp
// ❌ 禁止: デッドロックリスク
var result = service.GetDataAsync().Result;
service.GetDataAsync().Wait();
Thread.Sleep(1000);

// ✅ 良い例
var result = await service.GetDataAsync(ct);
await Task.Delay(1000, ct);
```

### `ValueTask` vs `Task` の使い分け

- `ValueTask<T>` はキャッシュから同期的に返る可能性が高い場合に使用する（アロケーション削減）
- `Task<T>` は常に非同期になる場合に使用する（一般的なケース）
- **`ValueTask` は一度だけ await できる**。複数回の await や `WhenAll` に渡すことは禁止

```csharp
// ✅ 良い例: キャッシュヒットが多い場合に ValueTask を使用
public ValueTask<ProductDto?> GetCachedProductAsync(string id, CancellationToken ct)
{
    if (_cache.TryGetValue(id, out var cached))
        return ValueTask.FromResult<ProductDto?>(cached);  // 同期的に返却（アロケーションなし）

    return new ValueTask<ProductDto?>(LoadFromDatabaseAsync(id, ct));  // 非同期フォールバック
}

// ❌ 禁止: ValueTask を複数回 await
var valueTask = GetCachedProductAsync("123", ct);
var result1 = await valueTask;
var result2 = await valueTask;  // 未定義動作！

// ❌ 禁止: ValueTask を WhenAll に渡す
await Task.WhenAll(valueTask1.AsTask(), valueTask2.AsTask());  // AsTask() が必要
```

### `IAsyncDisposable` / `await using`

`IAsyncDisposable` を実装するリソースには `await using` を使用する:

```csharp
// ✅ 良い例: await using（非同期リソースの確実な解放）
await using var transaction = await _context.Database.BeginTransactionAsync(ct);
await using var connection = new NpgsqlConnection(connectionString);

// ✅ 良い例: IAsyncDisposable の実装
public class KafkaProducerWrapper : IAsyncDisposable
{
    private readonly IProducer<string, string> _producer;

    public async ValueTask DisposeAsync()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
        await ValueTask.CompletedTask;
        GC.SuppressFinalize(this);
    }
}
```

### `ConfigureAwait` ポリシー

ASP.NET Core では `SynchronizationContext` が存在しないため、`ConfigureAwait(false)` は通常不要。
ただし、**ライブラリコード**（NuGet パッケージとして再利用される可能性があるコード）では `ConfigureAwait(false)` を付与する:

```csharp
// ✅ アプリケーションコード: ConfigureAwait 不要
var user = await _repository.FindByIdAsync(id, ct);

// ✅ ライブラリコード: ConfigureAwait(false) を付与
var data = await httpClient.GetStringAsync(url, ct).ConfigureAwait(false);
```

### 読み取り専用クエリの最適化
- 読み取り専用クエリでは `AsNoTracking()` を使用し、変更追跡のオーバーヘッドを排除する

```csharp
// ✅ 読み取り専用クエリ
public async Task<List<ProductDto>> GetProductsAsync(CancellationToken ct)
    => await _context.Products
        .AsNoTracking()
        .Select(p => new ProductDto(p.Id, p.Name, p.Price))
        .ToListAsync(ct);
```

---

## 10. コード構造

### メソッド設計
- 1 メソッド **30 行以下**を推奨。50 行を超える場合は分割を検討する
- メソッドのパラメータ数は **3 個以下**を推奨。4 個以上はパラメータオブジェクト（record）への集約を検討する
- ネスト深度は **3 段階以下**。早期リターン（ガード節）パターンを活用する

```csharp
// ❌ 悪い例: 深いネスト
public void Process(Order order)
{
    if (order != null)
    {
        if (order.IsValid())
        {
            if (order.HasItems())
            {
                // 処理...
            }
        }
    }
}

// ✅ 良い例: ガード節による早期リターン
public void Process(Order order)
{
    ArgumentNullException.ThrowIfNull(order);
    if (!order.IsValid()) return;
    if (!order.HasItems()) return;
    // 処理...
}
```

### クラス設計
- 1 クラス **300 行以下**を推奨。500 行を超える場合は SRP 違反の可能性を検討する
- 単一責任原則（SRP）: 1 クラスが複数の変更理由を持たないようにする

### マジックナンバー / マジックストリングの禁止
- リテラル値は定数として切り出す

```csharp
// ❌ 悪い例: マジックナンバー
if (retryCount > 3) { /* ... */ }
if (status == "ACTIVE") { /* ... */ }

// ✅ 良い例: 定数 / enum 使用
private const int MaxRetryCount = 3;
if (retryCount > MaxRetryCount) { /* ... */ }
if (status == UserStatus.Active) { /* ... */ }  // enum を使用
```

---

## 11. EF Core エンティティ規約

### エンティティの必須ルール

```csharp
// ✅ EF Core エンティティの正しい定義
[Table("users")]
public class User
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("email")]
    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Column("password_hash")]
    [Required]
    [MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // コレクションナビゲーションは = [] で初期化（null 防止）
    public ICollection<Order> Orders { get; set; } = [];
}
```

### `CreatedAt` / `UpdatedAt` の DB デフォルト値設定

エンティティの C# 初期化子 `= DateTime.UtcNow` はオブジェクト生成時の時刻を記録するが、
**DB 側のデフォルト値**も Fluent API で設定し、DB 直接操作時の整合性を担保する。
また、`SaveChanges` オーバーライドで `UpdatedAt` を自動更新する:

```csharp
// ✅ DbContext の OnModelCreating でデフォルト値を設定
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<User>(entity =>
    {
        entity.Property(e => e.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        entity.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    });
}

// ✅ DbContext に TimeProvider をインジェクション
public class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider timeProvider)
    : DbContext(options)
{
    // ...
}

// ✅ SaveChanges オーバーライドで UpdatedAt を自動更新
public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
{
    var entries = ChangeTracker.Entries()
        .Where(e => e.State is EntityState.Added or EntityState.Modified);

    var now = timeProvider.GetUtcNow().UtcDateTime;  // TimeProvider 使用（primary constructor 経由）

    foreach (var entry in entries)
    {
        if (entry.State == EntityState.Added)
            entry.Property("CreatedAt").CurrentValue = now;
        entry.Property("UpdatedAt").CurrentValue = now;
    }

    return await base.SaveChangesAsync(ct);
}
```

### System.Text.Json ソースジェネレーター（AOT 対応）

DTO の JSON シリアライズには `System.Text.Json` ソースジェネレーターを使用し、
リフレクションフリーのシリアライズを実現する（AOT 互換・高パフォーマンス）:

```csharp
// ✅ 良い例: JsonSerializerContext によるソースジェネレーション
[JsonSerializable(typeof(ProductDto))]
[JsonSerializable(typeof(List<ProductDto>))]
[JsonSerializable(typeof(OrderResponse))]
public partial class AppJsonSerializerContext : JsonSerializerContext { }

// ✅ Program.cs での設定
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});
```

**チェック項目**:
- `DateTime.Now`（ローカル時刻）は使用禁止 → `DateTime.UtcNow` / `DateTimeOffset.UtcNow` / `TimeProvider` を使用
- 全プロパティに `[Column("snake_case_name")]` でカラム名を明示
- コレクションナビゲーションは `= []` で初期化
- `LazyLoading` は無効。`Include()` / `ThenInclude()` で明示的 Eager Loading
- 楽観的ロックが必要なエンティティには `[Timestamp]` プロパティを追加
- `SaveChangesAsync` オーバーライドで `CreatedAt` / `UpdatedAt` を自動管理

---

## 12. 禁止事項チェックリスト

| # | 禁止事項 | 重要度 | 理由 |
|---|---------|--------|------|
| 1 | `Console.WriteLine` / `Console.Error.WriteLine` | Critical | ログ基盤と統合不可。`ILogger<T>` を使用 |
| 2 | `catch (Exception) { }` 例外の握りつぶし | Critical | 障害の検知不能。ログ出力または再スロー必須 |
| 3 | 秘密情報のハードコード（`var password = "..."` 等） | Critical | 情報漏洩リスク。環境変数 / `dotnet user-secrets` を使用 |
| 4 | `FromSqlRaw` での文字列結合 SQL | Critical | SQL インジェクション。EF Core LINQ / `FromSqlInterpolated` を使用 |
| 5 | `new UserService()` による直接インスタンス化 | Critical | DI 管理外。コンストラクタインジェクション必須 |
| 6 | プロパティインジェクション（`[Inject]` 等） | High | テスタビリティ低下。コンストラクタインジェクション必須 |
| 7 | null 参照の無検証使用 | High | `NullReferenceException`。`?.`, `??`, `ThrowIfNull()` を使用 |
| 8 | ログへの個人情報出力 | High | GDPR / 個人情報保護法違反 |
| 9 | Endpoints が Repository を直接参照 | High | レイヤー違反。Service 経由必須 |
| 10 | Endpoints にビジネスロジックを記述 | High | 責務混在。Service 層に移動 |
| 11 | `Thread.Sleep()` の使用 | High | スレッドブロック。`await Task.Delay()` を使用 |
| 12 | `.Result` / `.Wait()` の使用 | High | デッドロックリスク。`await` を使用 |
| 13 | `DateTime.Now` の使用 | High | タイムゾーン問題。`DateTime.UtcNow` / `TimeProvider` を使用 |
| 14 | `new HttpClient()` の直接使用 | High | ソケット枯渇。`IHttpClientFactory` を使用 |
| 15 | 文字列補間によるログ出力 | Medium | 構造化ログが壊れる。メッセージテンプレートを使用 |
| 16 | `ValueTask` の複数回 await | High | 未定義動作。1 回のみ await する |
| 17 | `using` で `IAsyncDisposable` を使用 | Medium | `await using` を使用すること |
| 18 | テスト不可能な `DateTime.UtcNow` 直接使用 | Medium | `TimeProvider` を DI 経由で使用 |
| 19 | ライブラリコードで `ConfigureAwait(false)` なし | Medium | デッドロックリスク（非 ASP.NET Core ホスト時） |
| 20 | 個別エンドポイントへの `.WithOpenApi()` | Medium | .NET 10 では `AddOpenApi()` + `MapOpenApi()` を使用。個別不要 |
