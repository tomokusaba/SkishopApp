# AuthService 修正計画書

> **対象レポート**: `check-report-1.md` (2026-04-06)
> **判定**: ❌ Rejected → 本計画の全 Critical/High 修正後に再レビューを実施し ✅ Approved を目指す
> **作成日**: 2026-04-06

---

## 目次

1. [Docker Compose 起動エラーの修正（P0）](#p0-docker-compose-起動エラーの修正)
2. [Critical 指摘の修正（C1〜C5）](#critical-指摘の修正c1c5)
3. [High 指摘の修正（H1〜H17）](#high-指摘の修正h1h17)
4. [実行順序・依存関係マップ](#実行順序依存関係マップ)

---

## P0: Docker Compose 起動エラーの修正

### エラー概要

```
42P01: relation "outbox_events" does not exist
```

`OutboxPublisher` (BackgroundService) が起動直後に `outbox_events` テーブルを SELECT しようとするが、テーブルが存在しない。

### 根本原因分析

| 要因 | 詳細 |
|------|------|
| **Migrations ディレクトリが空** | `Services/AuthService/Migrations/` にマイグレーションファイルが 1 件も存在しない |
| **`MigrateAsync()` が空振り** | `Program.cs` L349 で `await dbContext.Database.MigrateAsync()` を呼んでいるが、適用すべきマイグレーションが存在しないため何のテーブルも作成されない |
| **`init-databases.sql` はDB作成のみ** | `infra/postgres/init-databases.sql` は `CREATE DATABASE authdb;` のみ。テーブル定義は含まれない |
| **BackgroundService の早期起動** | `OutboxPublisher` 等の `BackgroundService` はアプリケーション起動と同時に実行を開始するため、テーブル未作成の状態で DB アクセスが発生する |
| **Docker の `ASPNETCORE_ENVIRONMENT`** | `docker-compose.yml` で `ASPNETCORE_ENVIRONMENT=Development` だが、Dockerfile は `ENV ASPNETCORE_ENVIRONMENT=Production` を設定。**docker-compose の環境変数が優先**するため `MigrateAsync()` のコードパスは通る。ただしマイグレーションが存在しないため効果がない |

### 修正計画

#### ステップ P0-1: EF Core Initial Migration の生成

**実行コマンド**（ローカル開発環境で実行）:

```bash
cd Services/AuthService
dotnet ef migrations add InitialCreate
```

**生成されるファイル**:
- `Services/AuthService/Migrations/YYYYMMDDHHMMSS_InitialCreate.cs` — Up/Down メソッド（テーブル作成/削除）
- `Services/AuthService/Migrations/YYYYMMDDHHMMSS_InitialCreate.Designer.cs` — スナップショットメタデータ
- `Services/AuthService/Migrations/AuthDbContextModelSnapshot.cs` — 現在のモデルスナップショット

**確認事項**: 生成されたマイグレーションに以下の 13 テーブルが含まれること:
`users`, `user_sessions`, `roles`, `user_roles`, `oauth_accounts`, `password_resets`, `user_mfa`, `security_logs`, `refresh_tokens`, `outbox_events`, `password_histories`, `oauth_clients`, `audit_logs`

#### ステップ P0-2: BackgroundService の DB 準備待機

マイグレーション生成だけでは、マイグレーション適用完了前に BackgroundService が起動する競合条件が残る。全 BackgroundService の `ExecuteAsync` 冒頭に DB 準備確認を追加する。

**対象ファイル**: `Infrastructure/BackgroundServices/` 配下の全 7 ファイル
- `OutboxPublisher.cs`
- `UserDeletedConsumer.cs`
- `PermissionsUpdatedConsumer.cs`
- `TokenCleanupService.cs`
- `FailedAttemptResetService.cs`
- `SessionTimeoutService.cs`
- `SecurityLogAnonymizationService.cs`

**修正方針（3 案）**:

| 案 | 方法 | メリット | デメリット |
|----|------|---------|----------|
| **A（推奨）** | `ExecuteAsync` の冒頭で DB 接続確認ループを追加 | 各 BackgroundService が独立して待機。実装がシンプル | 各ファイルに同じコードが必要（ヘルパーで共通化可能） |
| B | `IHostedService` の起動順序を制御し、マイグレーション専用の `IHostedService` を最初に登録 | 確実にマイグレーション完了後に起動 | ASP.NET Core の `IHostedService` は登録順に起動するが、前のサービスの完了を保証しない（StartAsync が完了すれば次に進む） |
| C | `Program.cs` の `MigrateAsync()` を `app.Run()` の直前ではなく `builder.Build()` 直後に移動し、全環境で実行 | BackgroundService 登録前にマイグレーション完了 | 本番環境でも自動マイグレーションが走るリスク |

**推奨案 A の実装**: 共通ヘルパーメソッドを作成し、各 BackgroundService で利用する。

**新規ファイル**: `Infrastructure/BackgroundServices/BackgroundServiceHelper.cs`

```csharp
using AuthService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.BackgroundServices;

public static class BackgroundServiceHelper
{
    public static async Task WaitForDatabaseAsync(
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        CancellationToken stoppingToken,
        int maxRetries = 30,
        TimeSpan? retryDelay = null)
    {
        var delay = retryDelay ?? TimeSpan.FromSeconds(2);
        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider
                    .GetRequiredService<Persistence.AuthDbContext>();
                // CanConnect は SELECT 1 を実行して接続可能性を確認する
                if (await dbContext.Database.CanConnectAsync(stoppingToken))
                {
                    // テーブルの存在確認（outbox_events が最も早く必要）
                    _ = await dbContext.OutboxEvents.Take(0).ToListAsync(stoppingToken);
                    logger.LogInformation("データベース準備完了");
                    return;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("データベース未準備（リトライ {Attempt}/{MaxRetries}）: {Message}",
                    i + 1, maxRetries, ex.Message);
            }
            await Task.Delay(delay, stoppingToken);
        }
        logger.LogError("データベースの準備が完了しませんでした（{MaxRetries} 回リトライ後）", maxRetries);
    }
}
```

**各 BackgroundService の修正例**（`OutboxPublisher.cs`）:

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    // ★追加: DB 準備待機
    await BackgroundServiceHelper.WaitForDatabaseAsync(
        scopeFactory, logger, stoppingToken);

    while (!stoppingToken.IsCancellationRequested)
    {
        // 既存のロジック...
    }
}
```

#### ステップ P0-3: Program.cs のマイグレーション実行位置を修正

現在の `MigrateAsync()` はエンドポイントマッピング後・`app.RunAsync()` 直前に配置されているが、**BackgroundService は `app.RunAsync()` 時に起動**するため、タイミングとしては正しい（`MigrateAsync()` → `RunAsync()` → BackgroundService 起動）。

ただし、マイグレーションが存在しなければ何も起きないため、**ステップ P0-1 が最も重要**。

**修正対象ファイル**: `Services/AuthService/Program.cs` L346-352

```csharp
// 修正前（Development 環境のみ）
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await dbContext.Database.MigrateAsync();
}

// 修正後（全環境でマイグレーション実行 — 安全なのは設計方針に依存）
// → 案 A を採用する場合は Development のみの現状維持で十分
```

---

## Critical 指摘の修正（C1〜C5）

### C1: MfaEndpoints のレイヤー違反

**問題**: `MfaEndpoints.VerifyMfaAsync` に 90 行のビジネスロジック + 6 Repository 直接注入

**修正対象ファイル** (4 ファイル):
1. `Services/AuthService/Endpoints/MfaEndpoints.cs` — VerifyMfaAsync の全面書き換え
2. `Services/AuthService/Services/Interfaces/IAuthService.cs` — `CompleteMfaLoginAsync` メソッド追加
3. `Services/AuthService/Services/AuthServiceImpl.cs` — `CompleteMfaLoginAsync` 実装追加
4. `Services/AuthService.Tests/Services/AuthServiceTests.cs` — テスト追加

**修正方針（3 案）**:

| 案 | 方法 | メリット | デメリット |
|----|------|---------|----------|
| **A（推奨）** | `IAuthService` に `CompleteMfaLoginAsync(string sessionToken, string code, CancellationToken)` を追加し、MfaEndpoints の 90 行を AuthServiceImpl に移動 | `LoginAsync` との DRY 化が容易。Endpoint は 2 行に簡素化 | AuthServiceImpl が更に肥大化（ただし正しいレイヤー） |
| B | `IMfaService` に `VerifyAndCreateSessionAsync` として実装 | MFA 責務が MfaService に集約 | MfaService が JWT/Session/RefreshToken を知る必要があり、関心事が混合 |
| C | 新規 `IMfaLoginService` を作成 | 責務が明確 | クラス増加、DI 登録追加 |

**推奨案 A の修正内容**:

**ファイル 1**: `Services/Interfaces/IAuthService.cs`

```csharp
// 追加
Task<LoginResponse> CompleteMfaLoginAsync(
    string sessionToken, string code, string? ipAddress, string? userAgent,
    CancellationToken ct = default);
```

**ファイル 2**: `Services/AuthServiceImpl.cs` — `CompleteMfaLoginAsync` を追加

`LoginAsync` のセッション生成〜トークン発行ロジック（L108-165）と同じパターンを MFA 用に実装する。`MfaEndpoints.VerifyMfaAsync` の L60-130 のロジックをそのまま移動し、`LoginAsync` 内の共通部分を private メソッドに抽出する。

```csharp
public async Task<LoginResponse> CompleteMfaLoginAsync(
    string sessionToken, string code, string? ipAddress, string? userAgent,
    CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(sessionToken);
    ArgumentNullException.ThrowIfNull(code);

    var mfaSession = await userSessionRepository.FindBySessionTokenAsync(sessionToken, ct)
        ?? throw new UnauthorizedException("MFA セッションが無効です");

    if (!mfaSession.IsActive || mfaSession.ExpiresAt < timeProvider.GetUtcNow())
        throw new UnauthorizedException("MFA セッションが期限切れです");

    // IMfaService.VerifyMfaAsync は AuthServiceImpl に注入する必要がある（DI 追加）
    // 以降、セッション生成・JWT発行・リフレッシュトークン・Outbox は LoginAsync と共通化
    // ...
}
```

**ファイル 3**: `Endpoints/MfaEndpoints.cs` — VerifyMfaAsync を簡素化

```csharp
private static async Task<IResult> VerifyMfaAsync(
    [FromBody] MfaVerificationRequest request,
    IValidator<MfaVerificationRequest> validator,
    Services.Interfaces.IAuthService authService,
    HttpContext httpContext,
    CancellationToken ct)
{
    var validationResult = await validator.ValidateAsync(request, ct);
    if (!validationResult.IsValid)
        return Results.ValidationProblem(validationResult.ToDictionary());

    var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
    var userAgent = httpContext.Request.Headers.UserAgent.ToString();
    return Results.Ok(await authService.CompleteMfaLoginAsync(
        request.SessionToken, request.Code, ipAddress, userAgent, ct));
}
```

**注意**: `AuthServiceImpl` の primary constructor に `IMfaService mfaService` パラメータを追加する必要がある。循環参照は発生しない（`MfaService` は `AuthServiceImpl` に依存していない）。

---

### C2: LogoutAsync のバグ修正

**問題**: `AuthEndpoints.LogoutAsync` が JWT の `session_id` クレーム（= `UserSession.Id`, GUID）を渡すが、`AuthServiceImpl.LogoutAsync` は `FindBySessionTokenAsync` で検索する（`SessionToken` = ランダム Base64）。**Id と SessionToken は異なるフィールド**のためログアウトが常に失敗する。

**修正対象ファイル** (1 ファイル):
- `Services/AuthService/Services/AuthServiceImpl.cs` L246 — `FindBySessionTokenAsync` → `FindBySessionIdAsync` に変更

**修正方針（3 案）**:

| 案 | 方法 | メリット | デメリット |
|----|------|---------|----------|
| **A（推奨）** | `AuthServiceImpl.LogoutAsync` の検索を `FindBySessionIdAsync` に変更 | 最小限の変更。JWT クレームの `session_id` は `UserSession.Id` と一致する設計意図に合致 | なし |
| B | JWT に `session_token` クレームを追加し、Endpoint で `session_token` を取得して渡す | クレームが増える | JWT ペイロードが肥大化。全トークン再発行が必要 |
| C | `IAuthService.LogoutAsync` のパラメータ名を `sessionId` に変更し、interface/実装両方を修正 | 明確な命名 | パラメータ名変更 + 検索ロジック変更の 2 箇所修正 |

**推奨案 A の修正内容**:

**ファイル**: `Services/AuthServiceImpl.cs` L246-253

```csharp
// 修正前
public async Task LogoutAsync(string sessionToken, CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(sessionToken);
    var session = await userSessionRepository.FindBySessionTokenAsync(sessionToken, ct);
    if (session is null)
    {
        logger.LogWarning("ログアウト対象セッションが見つかりません: SessionToken={SessionToken}", sessionToken);

// 修正後
public async Task LogoutAsync(string sessionId, CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(sessionId);
    var session = await userSessionRepository.FindBySessionIdAsync(sessionId, ct);
    if (session is null)
    {
        logger.LogWarning("ログアウト対象セッションが見つかりません: SessionId={SessionId}", sessionId);
```

**注意**: ログメッセージの `SessionToken=` → `SessionId=` も併せて修正する。

**ファイル**: `Services/Interfaces/IAuthService.cs` L9

```csharp
// 修正前
Task LogoutAsync(string sessionToken, CancellationToken ct = default);

// 修正後
Task LogoutAsync(string sessionId, CancellationToken ct = default);
```

---

### C3: Aggregate Root 境界違反（部分許容 + ドメインロジック集約）

**問題**: 子エンティティ（UserSession, RefreshToken 等）が独立 Repository で直接操作されている

**修正方針**: 認証ドメインの特性（高頻度のトークン操作、バッチ削除等）を考慮し、**Repository の独立は許容する**。ただし **User に失敗ログイン・ロック・ステータス遷移のドメインロジックを集約する**（C4 と統合して対応）。

**修正対象ファイル**: なし（C4 で対応）

---

### C4: 貧血ドメインモデルの改善

**問題**: `User` がデータコンテナのみ。ビジネスロジックが `SecurityService` 等に散在。

**修正対象ファイル** (3 ファイル):
1. `Services/AuthService/Models/User.cs` — ドメインメソッド追加
2. `Services/AuthService/Services/SecurityService.cs` — User のメソッド呼び出しに変更
3. `Services/AuthService.Tests/` — 対応テスト追加

**修正方針（3 案）**:

| 案 | 方法 | メリット | デメリット |
|----|------|---------|----------|
| **A（推奨）** | User に `RecordFailedLogin()`, `Unlock()`, `Activate()`, `IsLockExpired(DateTimeOffset now)` 等のメソッドを追加。Service は User のメソッドを呼ぶだけに | DDD 原則準拠。テスト容易 | 既存 Service コードの大幅修正 |
| B | イベントソーシングは導入せず、状態遷移の検証ロジックのみ User に移動 | 段階的移行が可能 | ロジック散在が部分的に残る |
| C | 現状維持（Service 層にロジック集中） | 変更なし | DDD 違反のまま |

**推奨案 A の修正内容**:

**ファイル**: `Models/User.cs` — ドメインメソッド追加

```csharp
// ドメインメソッド追加
public bool IsLockExpired(DateTimeOffset now, int autoUnlockMinutes)
    => AccountLocked && LockedAt.HasValue
        && LockedAt.Value.AddMinutes(autoUnlockMinutes) < now;

public void RecordFailedLogin(int maxFailedAttempts, DateTimeOffset now)
{
    FailedLoginAttempts++;
    if (FailedLoginAttempts >= maxFailedAttempts)
    {
        AccountLocked = true;
        LockedAt = now;
    }
}

public void Unlock()
{
    AccountLocked = false;
    FailedLoginAttempts = 0;
    LockedAt = null;
}

public void ResetFailedAttempts()
{
    FailedLoginAttempts = 0;
}

public void RecordLogin(DateTimeOffset now)
{
    LastLogin = now;
}
```

**ファイル**: `Services/SecurityService.cs` — ドメインメソッドを利用するようリファクタ

```csharp
// 修正前 (IncrementFailedAttemptsAsync 内)
user.FailedLoginAttempts++;
if (user.FailedLoginAttempts >= _authSettings.MaxFailedAttempts)
{
    user.AccountLocked = true;
    user.LockedAt = timeProvider.GetUtcNow();
    // ...
}

// 修正後
user.RecordFailedLogin(_authSettings.MaxFailedAttempts, timeProvider.GetUtcNow());
```

---

### C5: RefreshTokenRepository の TimeProvider 不使用

**問題**: `DateTimeOffset.UtcNow` を直接使用し、テスト時に時刻をモックできない

**修正対象ファイル** (2 ファイル):
1. `Services/AuthService/Repositories/RefreshTokenRepository.cs` — TimeProvider 注入
2. `Services/AuthService/Repositories/Interfaces/IRefreshTokenRepository.cs` — 変更なし（interface は変わらない）

**修正内容**:

```csharp
// 修正前
public class RefreshTokenRepository(AuthDbContext context) : IRefreshTokenRepository

// 修正後
public class RefreshTokenRepository(AuthDbContext context, TimeProvider timeProvider) : IRefreshTokenRepository

// FindActiveByUserIdAsync 内
// 修正前: .Where(r => r.UserId == userId && !r.IsRevoked && r.ExpiresAt > DateTimeOffset.UtcNow)
// 修正後: .Where(r => r.UserId == userId && !r.IsRevoked && r.ExpiresAt > timeProvider.GetUtcNow())

// CountActiveFamiliesByUserIdAsync 内も同様に修正
```

---

## High 指摘の修正（H1〜H17）

### H1: SecurityService.LogSecurityEventAsync のトランザクション境界破壊

**問題**: 内部で `SaveChangesAsync` を呼び、呼び出し元のトランザクションを破壊する

**修正対象ファイル** (1 ファイル): `Services/SecurityService.cs` L37-38

**修正方針（3 案）**:

| 案 | 方法 | メリット | デメリット |
|----|------|---------|----------|
| A | `SaveChangesAsync` を `LogSecurityEventAsync` 内から削除し、呼び出し元が一括で `SaveChangesAsync` を呼ぶ | トランザクション境界が呼び出し元で制御可能 | **危険**: `IncrementFailedAttemptsAsync` 内で ACCOUNT_LOCKED ログ記録後に例外 throw するケースで SecurityLog が永続化されない |
| **B（推奨）** | `bool saveImmediately = true` パラメータを追加。デフォルトは即時保存、トランザクション内では `false` を渡す | 後方互換性あり。既存呼び出し元は変更不要 | パラメータ増加 |
| C | `SecurityLog` をメモリ内キューに蓄積し、別のタイミングで一括保存 | 完全に独立 | 実装複雑度が高い |

**推奨案 B の修正内容**:

```csharp
// 修正前
public async Task LogSecurityEventAsync(
    string? userId, string eventType, string? ipAddress,
    string? userAgent, string? details, CancellationToken ct = default)
{
    // ...
    await securityLogRepository.AddAsync(log, ct);
    await securityLogRepository.SaveChangesAsync(ct);
}

// 修正後
public async Task LogSecurityEventAsync(
    string? userId, string eventType, string? ipAddress,
    string? userAgent, string? details,
    CancellationToken ct = default, bool saveImmediately = true)
{
    // ...
    await securityLogRepository.AddAsync(log, ct);
    if (saveImmediately)
        await securityLogRepository.SaveChangesAsync(ct);
}
```

**呼び出し元の修正**: トランザクション内で呼ばれる箇所のみ `saveImmediately: false` を渡す。デフォルト `true` のため既存呼び出し元は変更不要。

---

### H2: Argon2PasswordHasher の命名不整合

**問題**: クラス名が `Argon2PasswordHasher` だが実装は PBKDF2

**修正対象ファイル** (3 ファイル):
1. `Infrastructure/Security/Argon2PasswordHasher.cs` — ファイル名変更 + クラス名変更
2. `Program.cs` L97 — DI 登録のクラス名変更
3. テストで参照している箇所があれば修正

**修正方針（3 案）**:

| 案 | 方法 | メリット | デメリット |
|----|------|---------|----------|
| **A（推奨）** | `Pbkdf2PasswordHasher` にリネーム | 実態に即した命名 | 名前変更のみ |
| B | 実際に Argon2 (Konscious.Security.Cryptography) を実装 | 命名と実装が一致、セキュリティ向上 | NuGet 追加 + 実装コスト + 既存パスワードハッシュとの互換性 |
| C | `CustomPasswordHasher` にリネーム | 汎用的 | 何のアルゴリズムか不明 |

**推奨案 A の修正内容**:

```
Argon2PasswordHasher.cs → Pbkdf2PasswordHasher.cs
class Argon2PasswordHasher → class Pbkdf2PasswordHasher
Program.cs: AddScoped<IPasswordHasher<User>, Pbkdf2PasswordHasher>()
```

---

### H3: Enum 未使用（マジックストリング散在）

**問題**: `Enums/` に 4 つの enum が定義済みだが、コード全体で未使用。`"ACTIVE"`, `"PENDING_VERIFICATION"` 等のマジックストリングが散在。

**修正対象ファイル** (多数 — 影響範囲が広い):
1. `Models/User.cs` — Status/Role プロパティの型変更
2. `Models/OutboxEvent.cs` — Status プロパティの型変更
3. `Models/SecurityLog.cs` — EventType プロパティの型変更
4. `Infrastructure/Persistence/AuthDbContext.cs` — `HasConversion<string>()` 追加
5. `Services/` 配下全ファイル — マジックストリングを enum に置換
6. `Repositories/` 配下 — クエリの変更
7. テストファイル — 同様の修正

**修正方針（3 案）**:

| 案 | 方法 | メリット | デメリット |
|----|------|---------|----------|
| **A（推奨）** | enum 型をモデルに適用し、`HasConversion<string>()` で DB は文字列のまま | 型安全性 + DB 互換性維持 | 影響範囲が広い |
| B | enum を削除し、`const string` クラスに統一 | 影響範囲が小さい | 型安全性なし |
| C | 後回し（Medium に格下げ） | 変更なし | レビュー指摘が残る |

**推奨案 A の修正要点**:

**ファイル**: `Models/User.cs`

```csharp
// 修正前
public string Status { get; set; } = "PENDING_VERIFICATION";
public string Role { get; set; } = "USER";

// 修正後
public UserStatus Status { get; set; } = UserStatus.PendingVerification;
public UserRoleType Role { get; set; } = UserRoleType.User;
```

**ファイル**: `AuthDbContext.cs` の `ConfigureUser`

```csharp
entity.Property(u => u.Status)
    .HasConversion<string>()
    .HasMaxLength(50);
entity.Property(u => u.Role)
    .HasConversion<string>()
    .HasMaxLength(50);
```

**Enum の DB 値マッピング**: `HasConversion<string>()` のデフォルトは PascalCase（例: `PendingVerification`）だが、DB の check constraint は UPPER_SNAKE_CASE（`PENDING_VERIFICATION`）。**カスタム ValueConverter が必須**。

**新規ファイル**: `Infrastructure/Persistence/UpperSnakeCaseEnumConverter.cs`

```csharp
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AuthService.Infrastructure.Persistence;

public partial class UpperSnakeCaseEnumConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    public UpperSnakeCaseEnumConverter()
        : base(
            v => ToUpperSnakeCase(v.ToString()),
            v => Enum.Parse<TEnum>(FromUpperSnakeCase(v), ignoreCase: true))
    {
    }

    private static string ToUpperSnakeCase(string input)
        => PascalToSnakeRegex().Replace(input, "_$1").ToUpperInvariant();

    private static string FromUpperSnakeCase(string input)
    {
        var parts = input.Split('_');
        return string.Concat(parts.Select(p =>
            p.Length > 0 ? char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant() : p));
    }

    [GeneratedRegex(@"([A-Z])(?=[A-Z][a-z])|(?<=[a-z0-9])([A-Z])")]
    private static partial Regex PascalToSnakeRegex();
}
```

**AuthDbContext の修正**: `HasConversion<string>()` → `HasConversion(new UpperSnakeCaseEnumConverter<UserStatus>())` 等に変更。

**注意**: この修正はマイグレーション変更を伴わない（DB 側は文字列のまま）。カスタムコンバーターで check constraint の UPPER_SNAKE_CASE 値と enum 値の相互変換を保証する。

---

### H4: AuthMetrics カウンタの未使用

**問題**: 6 カウンタが定義のみで `.Add()` が一切呼ばれていない

**修正対象ファイル** (5 ファイル):
1. `Services/AuthServiceImpl.cs` — LoginAttempts, TokenRefreshes の .Add() 追加
2. `Services/MfaService.cs` — MfaVerifications の .Add() 追加
3. `Services/SecurityService.cs` — AccountLockouts の .Add() 追加
4. `Services/PasswordService.cs` — PasswordResets の .Add() 追加
5. `Services/UserRegistrationService.cs` — RegistrationAttempts の .Add() 追加

**修正内容例** (`AuthServiceImpl.LoginAsync` 内):

```csharp
// ログイン成功時
AuthMetrics.LoginAttempts.Add(1,
    new KeyValuePair<string, object?>("result", "success"),
    new KeyValuePair<string, object?>("method", "password"));

// ログイン失敗時
AuthMetrics.LoginAttempts.Add(1,
    new KeyValuePair<string, object?>("result", "failure"),
    new KeyValuePair<string, object?>("method", "password"));
```

**注意**: `AuthMetrics` は `Program.cs` で `Singleton` 登録済みだが、static カウンタのため DI 注入は不要。Service 内で `AuthMetrics.LoginAttempts.Add(...)` のように直接呼び出す。

---

### H5: OAuth スタブの残存

**問題**: 未実装のスタブが `AllowAnonymous` で公開中

**修正対象ファイル** (1 ファイル): `Endpoints/OAuthEndpoints.cs`

**修正方針（3 案）**:

| 案 | 方法 | メリット | デメリット |
|----|------|---------|----------|
| **A（推奨）** | 未実装エンドポイント（`StartOAuthFlowAsync`, `OAuthCallbackAsync`）を削除し、`IOAuthService.HandleOAuthCallbackAsync` も削除 | 攻撃面縮小 | OAuth 実装時に再追加が必要 |
| B | `AllowAnonymous` を削除し認証必須にする | 未認証アクセスは防げる | スタブ自体は残る |
| C | 全 OAuth エンドポイントファイルごと削除 | 完全にクリーン | リンク/アンリンク機能も削除される |

**推奨案 A の修正内容**:

- `StartOAuthFlowAsync` の Endpoint 登録削除（MapGet `/authorization/{provider}` を削除）
- `OAuthCallbackAsync` の Endpoint 登録削除（MapGet `/callback/{provider}` を削除）
- `OAuthService.HandleOAuthCallbackAsync` — メソッド本体を残すが、呼び出されないことを確認
- `IOAuthService` から `HandleOAuthCallbackAsync` を削除

---

### H6: OAuth 認可コードの ProviderUserId 誤用

**問題**: `LinkAccountAsync` で `code`（認可コード）を `ProviderUserId` として永続化

**修正対象ファイル** (1 ファイル): `Services/OAuthService.cs` L42-46

**修正内容**: OAuth 認可コードをアクセストークンに交換し、そのレスポンスからユーザー識別子を取得する処理が必要。ただし H5 で未実装スタブを削除する方針のため、**H5 の対応でカバーされる**。

`LinkAccountAsync` 自体は認証済みユーザーのみが呼べるため、認可コードの交換処理を実装するか、H5 同様にスタブとして `BusinessException` を投げるように修正する。

```csharp
// 修正
throw new BusinessException("OAuth アカウントリンクは現在利用できません。今後のアップデートで対応予定です。");
```

---

### H7: OAuthCallbackAsync の code バリデーションなし

**修正**: H5 でエンドポイント削除により解消される

---

### H8: `.WithOpenApi()` の全エンドポイント追加

**修正対象ファイル** (7 ファイル): 全 Endpoint ファイルの `MapGroup` に `.WithOpenApi()` を追加

```csharp
// 各 Endpoint ファイルの MapGroup に追加
var group = app.MapGroup("/api/v1/auth")
    .WithTags("Authentication")
    .RequireRateLimiting("auth")
    .WithOpenApi();  // ★追加
```

**対象ファイル一覧**:
- `Endpoints/AuthEndpoints.cs`
- `Endpoints/TokenEndpoints.cs`
- `Endpoints/PasswordEndpoints.cs`
- `Endpoints/MfaEndpoints.cs`
- `Endpoints/OAuthEndpoints.cs`
- `Endpoints/EmailVerificationEndpoints.cs`
- `Endpoints/UserRegistrationEndpoints.cs`

**前提**: 
1. `AuthService.csproj` に `<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.*" />` を追加する
2. `Program.cs` に `builder.Services.AddOpenApi()` を追加する

---

### H9: JWT Bearer 設定の IOptions 未使用

**問題**: `builder.Configuration["Jwt:*"]` を直接参照し、`IOptions<JwtSettings>` と二重管理

**修正対象ファイル** (1 ファイル): `Program.cs` L143-163

**修正内容**: JWT Bearer の `AddJwtBearer` 内で `IOptions<JwtSettings>` を使用する。ただし `AddJwtBearer` の設定時点では DI コンテナが構築されていないため、`builder.Configuration` を直接参照するのは技術的に正しい。

**前提**: `JwtSettings` に `SecretKey` プロパティを追加する（現在は未定義）。

**ファイル**: `Configurations/JwtSettings.cs`
```csharp
public record JwtSettings
{
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;  // ★追加
    public int AccessExpirationSeconds { get; init; } = 3600;
    public int RefreshExpirationSeconds { get; init; } = 604800;
    public int MaxActiveRefreshTokens { get; init; } = 10;
}
```

**修正方針**: `PostConfigure` パターンで IOptions 経由に統一する。

```csharp
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
```

**追加修正**: `ClientCredentialsService.cs` も `IConfiguration` で `Jwt:SecretKey` を直接参照している。`IOptions<JwtSettings>` の `SecretKey` に統一する（`IConfiguration configuration` パラメータを削除）。
```

---

### H10: KafkaSettings 設定クラスの追加

**修正対象ファイル** (2 ファイル):
1. `Configurations/KafkaSettings.cs` — 新規作成
2. `Program.cs` — IOptions 登録 + Kafka Producer 設定で使用

**新規ファイル**: `Configurations/KafkaSettings.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace AuthService.Configurations;

public record KafkaSettings
{
    [Required]
    public string BootstrapServers { get; init; } = string.Empty;
}
```

**Program.cs 修正**:

```csharp
builder.Services.AddOptions<KafkaSettings>()
    .Bind(builder.Configuration.GetSection("Kafka"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Producer 設定で IOptions<KafkaSettings> を使用
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
```

---

### H11: Null Safety（null! 使用）

**修正対象ファイル** (2 ファイル):
1. `Infrastructure/Security/Argon2PasswordHasher.cs` (→ Pbkdf2PasswordHasher.cs) L15 — `user!` → `ArgumentNullException.ThrowIfNull` でガード済みのため `user` に変更（PasswordHasher<User> が null を許容する場合はそのまま）
2. `Services/ClientCredentialsService.cs` L47 — `null!` の代替を検討

**修正内容**: H2 のリネーム時に合わせて修正。`user!` は PasswordHasher の内部実装が null を受け入れるため、`user` のまま渡しても動作する。ただし型の制約上 `user!` が必要な場合は、事前に `ArgumentNullException.ThrowIfNull(user)` でガードした上で渡す。

---

### H12: TotpService の TimeProvider 未注入

**修正対象ファイル** (1 ファイル): `Services/TotpService.cs` L8, L41

**修正内容**:

```csharp
// 修正前
public class TotpService(ILogger<TotpService> logger) : ITotpService

// 修正後
public class TotpService(TimeProvider timeProvider, ILogger<TotpService> logger) : ITotpService

// L41 修正前
var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

// L41 修正後
var unixTimestamp = timeProvider.GetUtcNow().ToUnixTimeSeconds();
```

---

### H13: User の bool プロパティ命名規則

**修正対象ファイル** (多数 — 全参照箇所):
1. `Models/User.cs` — `EmailVerified` → `IsEmailVerified`、`AccountLocked` → `IsAccountLocked`
2. `AuthDbContext.cs` — check constraint 参照名が変わるためマイグレーション再生成が必要
3. 全 Service/Repository ファイル — プロパティ参照の更新

**注意**: [Column] 属性でカラム名を指定しているため、DB カラム名は変わらない。C# プロパティ名のみの変更。

```csharp
[Column("email_verified")]
public bool IsEmailVerified { get; set; }  // 旧: EmailVerified

[Column("account_locked")]
public bool IsAccountLocked { get; set; }  // 旧: AccountLocked
```

---

### H14: 楽観的ロック（DbUpdateConcurrencyException）ハンドリング

**修正対象ファイル** (3 ファイル):
1. `Services/AuthServiceImpl.cs` — `SaveChangesAsync` 呼び出し周辺
2. `Services/SecurityService.cs` — 同上
| `Exceptions/ConcurrencyException.cs` — **既存確認済み**（新規作成不要）

**修正内容**: User エンティティを更新する `SaveChangesAsync` 呼び出しを `try-catch` で囲む。

```csharp
try
{
    await userRepository.SaveChangesAsync(ct);
}
catch (DbUpdateConcurrencyException ex)
{
    logger.LogWarning(ex, "楽観的ロック競合: {EntityType}", ex.Entries.FirstOrDefault()?.Entity.GetType().Name);
    throw new ConcurrencyException("データが他のユーザーによって更新されました。再度お試しください。");
}
```

---

### H15: ExecuteUpdateAsync で UpdatedAt が更新されない

**修正対象ファイル** (2 ファイル):
1. `Repositories/UserSessionRepository.cs` L27 — `SetProperty(s => s.UpdatedAt, ...)` 追加
2. `Repositories/RefreshTokenRepository.cs` L24, L38 — 同上

**修正内容例**:

```csharp
// UserSessionRepository.DeactivateSessionAsync
await context.UserSessions
    .Where(s => s.Id == sessionId)
    .ExecuteUpdateAsync(setters => setters
        .SetProperty(s => s.IsActive, false)
        .SetProperty(s => s.UpdatedAt, timeProvider.GetUtcNow()), ct);  // ★追加

// RefreshTokenRepository.RevokeAllByUserIdAsync / RevokeAllByFamilyIdAsync
.ExecuteUpdateAsync(s => s
    .SetProperty(r => r.IsRevoked, true)
    .SetProperty(r => r.RevokedAt, revokedAt)
    .SetProperty(r => r.UpdatedAt, timeProvider.GetUtcNow()), ct);  // ★追加
```

**注意**: `UserSessionRepository` にも `TimeProvider` を注入する必要がある。

---

### H16: Events 二重配置の解消

**問題**: `Events/` と `DTOs/Events/` に同名の record が重複定義

**修正対象ファイル**:
1. `Events/` ディレクトリ — 全 12 ファイル + `IAuthEvent.cs` を保持する **か** 削除する
2. `DTOs/Events/` ディレクトリ — 3 ファイルを保持する **か** 削除する

**修正方針（3 案）**:

| 案 | 方法 | メリット | デメリット |
|----|------|---------|----------|
| **A（推奨）** | `Events/` を全削除（完全なデッドコード）。`DTOs/Events/` のみ保持 | 即座にクリーン化。Events/ は 0 参照のため安全に削除可能 | なし |
| B | `Events/` を正とし `DTOs/Events/` を削除 | Events/ にはイベント Record が豊富（12 種） | DTOs/ からの参照修正が必要 |
| C | 両方残し namespace で使い分ける | 変更なし | 混乱の原因 |

**推奨案 A の修正内容**:

調査の結果、`Events/` 配下の 13 ファイルは全て **0 参照**（どのコードからも `using AuthService.Events;` されていない完全なデッドコード）であることが判明。`DTOs/Events/` が実際に使用されている唯一のイベント定義。

- `Events/` 配下の 13 ファイルを**全削除**（移動は不要）
- `DTOs/Events/` はそのまま保持
- `using AuthService.Events;` の参照は存在しないため修正不要

---

### H17: テスト不足の解消

**問題**: テストカバレッジが大幅に不足

**修正対象ファイル** (新規ファイル多数):

| 優先度 | テスト対象 | 新規ファイル |
|--------|-----------|------------|
| 1（最優先） | `RefreshTokenAsync`（リプレイ攻撃検出） | `AuthServiceTests.cs` にテストケース追加 |
| 2 | SecurityService Unit Test | `Services/SecurityServiceTests.cs` 新規 |
| 3 | TotpService Unit Test | `Services/TotpServiceTests.cs` 新規 |
| 4 | AuditLogService Unit Test | `Services/AuditLogServiceTests.cs` 新規 |
| 5 | Endpoint 統合テスト | `Integration/AuthEndpointsTests.cs` 新規 |
| 6 | OAuthService Unit Test | `Services/OAuthServiceTests.cs` 新規 |
| 7 | BackgroundService テスト | `BackgroundServices/OutboxPublisherTests.cs` 新規 |
| 8 | Repository DB テスト | `Repositories/UserRepositoryTests.cs` 新規（Testcontainers） |

---

## 実行順序・依存関係マップ

修正は以下の順序で実行する。依存関係のある修正は前提となる修正の完了後に着手する。

```
Phase 0: Docker エラー修正（最優先 — 起動できない状態の解消）
  P0-1: EF Core Migration 生成 (dotnet ef migrations add InitialCreate)
  P0-2: BackgroundService DB 準備待機ヘルパー追加
  P0-3: Program.cs マイグレーション位置確認
  ✅ 確認: docker compose up で outbox_events エラーが解消されること

Phase 1: Critical バグ修正
  C2: LogoutAsync のバグ修正（最小変更、即時修正）
  C5: RefreshTokenRepository の TimeProvider 注入
  ✅ 確認: dotnet build 成功

Phase 2: レイヤー違反・DDD 修正（相互依存あり）
  C4: User にドメインメソッド追加 ← 先に実施
  C1: MfaEndpoints → AuthServiceImpl に CompleteMfaLoginAsync 移動 ← C4 後
  C3: (C4 でカバー — 追加作業なし)
  H1: SecurityService の SaveChangesAsync 削除 ← C4 後
  ✅ 確認: dotnet build + dotnet test 成功

Phase 3: 命名・コード品質修正
  H2: Argon2PasswordHasher → Pbkdf2PasswordHasher リネーム
  H3: Enum 適用 + UpperSnakeCaseEnumConverter 新規作成（影響範囲が広いため慎重に）
  H11: null! 修正（H2 と同時に）
  H12: TotpService TimeProvider 注入
  H13: User bool プロパティ命名修正（H3 と同時に実施可能）— C4 と同時実施
  ✅ 確認: dotnet build + dotnet test 成功 + EF Core Migration 再生成

Phase 4: 設定・DI・API 修正
  H8: .WithOpenApi() 全エンドポイント追加
  H9: JWT Bearer 設定の IOptions 統一
  H10: KafkaSettings 設定クラス追加
  H5: OAuth スタブ削除（H6, H7 も同時解消）
  ✅ 確認: dotnet build 成功

Phase 5: データアクセス・可観測性修正
  H4: AuthMetrics カウンタの .Add() 呼び出し追加
  H14: DbUpdateConcurrencyException ハンドリング追加
  H15: ExecuteUpdateAsync の UpdatedAt 更新追加
  H16: Events 二重配置の解消
  ✅ 確認: dotnet build + dotnet test 成功

Phase 6: テスト追加
  H17: テストファイル新規作成（優先度順に）
  ✅ 確認: dotnet test --collect:"XPlat Code Coverage" でカバレッジ確認
```

### 修正ファイル一覧（サマリ）

| ファイル | 修正内容 | 関連 Issue |
|---------|---------|-----------|
| `Migrations/` (新規生成) | InitialCreate マイグレーション | P0 |
| `Infrastructure/BackgroundServices/BackgroundServiceHelper.cs` (新規) | DB 準備待機ヘルパー | P0 |
| `Infrastructure/BackgroundServices/*.cs` (7 ファイル) | WaitForDatabaseAsync 呼び出し追加 | P0 |
| `Services/AuthServiceImpl.cs` | LogoutAsync 修正, CompleteMfaLoginAsync 追加, Metrics 追加, ConcurrencyException ハンドリング | C1, C2, H4, H14 |
| `Services/Interfaces/IAuthService.cs` | CompleteMfaLoginAsync 追加, LogoutAsync パラメータ名修正 | C1, C2 |
| `Endpoints/MfaEndpoints.cs` | VerifyMfaAsync 簡素化 | C1 |
| `Models/User.cs` | ドメインメソッド追加, bool 命名修正, Enum 型適用 | C4, H3, H13 |
| `Services/SecurityService.cs` | ドメインメソッド利用, SaveChangesAsync 削除 | C4, H1 |
| `Repositories/RefreshTokenRepository.cs` | TimeProvider 注入, UpdatedAt 追加 | C5, H15 |
| `Repositories/UserSessionRepository.cs` | TimeProvider 注入, UpdatedAt 追加 | H15 |
| `Infrastructure/Security/Argon2PasswordHasher.cs` → `Pbkdf2PasswordHasher.cs` | リネーム + null! 修正 | H2, H11 |
| `Enums/*.cs` (4 ファイル) | DB 文字列マッピング追加 | H3 |
| `Infrastructure/Persistence/AuthDbContext.cs` | HasConversion 追加 | H3 |
| `Services/TotpService.cs` | TimeProvider 注入 | H12 |
| `Endpoints/*.cs` (7 ファイル) | .WithOpenApi() 追加 | H8 |
| `Endpoints/OAuthEndpoints.cs` | スタブエンドポイント削除 | H5 |
| `Services/OAuthService.cs` | HandleOAuthCallbackAsync 削除, LinkAccountAsync 修正 | H5, H6 |
| `Program.cs` | DI 名修正, IOptions 統一, KafkaSettings 登録, OpenApi 追加 | H2, H9, H10 |
| `Configurations/KafkaSettings.cs` (新規) | Kafka 設定クラス | H10 |
| `Services/MfaService.cs` | Metrics 追加 | H4 |
| `Services/PasswordService.cs` | Metrics 追加 | H4 |
| `Services/UserRegistrationService.cs` | Metrics 追加 | H4 |
| `Events/` (13 ファイル削除) | DTOs/Events/ に統合 | H16 |
| `DTOs/Events/` | Events/ から 9 ファイル移動 + IAuthEvent 追加 | H16 |
| `Services/ClientCredentialsService.cs` | null! 修正 | H11 |
| `AuthService.Tests/` (8 新規ファイル) | テスト追加 | H17 |
