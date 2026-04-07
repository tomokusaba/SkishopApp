# AuthService フェーズ別実装計画書

> **対象サービス**: AuthService（認証・認可マイクロサービス）
> **ポート**: 5001
> **DB**: PostgreSQL (authdb)
> **キャッシュ**: Redis（セッション、トークンブラックリスト、レート制限）
> **メッセージング**: Apache Kafka（Outbox パターン）
> **設計書**: `design-docs/authentication-service-design.md`
> **規約**: AGENTS.md / `.github/instructions/` 配下のインストラクションファイル群

---

## 目次

1. [Phase 1: プロジェクト基盤構築](#phase-1-プロジェクト基盤構築)
2. [Phase 2: エンティティ・Value Object・Enum 定義](#phase-2-エンティティvalue-objectenum-定義)
3. [Phase 3: Repository 層実装](#phase-3-repository-層実装)
4. [Phase 4: Service 層実装](#phase-4-service-層実装)
5. [Phase 5: Endpoints 実装](#phase-5-endpoints-実装)
6. [Phase 6: Kafka イベント連携](#phase-6-kafka-イベント連携)
7. [Phase 7: Redis キャッシュ連携](#phase-7-redis-キャッシュ連携)
8. [Phase 8: 認証・認可・セキュリティ](#phase-8-認証認可セキュリティ)
9. [Phase 9: 単体テスト・統合テスト](#phase-9-単体テスト統合テスト)
10. [Phase 10: 可観測性](#phase-10-可観測性)
11. [Phase 11: Docker / デプロイ準備](#phase-11-docker--デプロイ準備)

---

## Phase 1: プロジェクト基盤構築

### 目的

AuthService プロジェクトの骨格を構築する。ビルド可能な最小構成を作成し、以降のフェーズの土台とする。

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `AuthService/AuthService.csproj` | プロジェクト定義（EF Core, JWT, Serilog, Redis, Kafka, FluentValidation 等） |
| 2 | `AuthService/Program.cs` | エントリポイントスケルトン（最小構成） |
| 3 | `AuthService/appsettings.json` | 共通設定（安全なデフォルト値のみ、秘密情報禁止） |
| 4 | `AuthService/appsettings.Development.json` | 開発環境設定 |
| 5 | `AuthService/appsettings.Production.json` | 本番環境設定（環境変数参照のみ） |
| 6 | `AuthService/Dockerfile` | マルチステージビルド + 非 root 実行 |
| 7 | `AuthService/.dockerignore` | ビルド不要ファイルの除外 |

### 1.1 AuthService.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <!-- ORM -->
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.*" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.*" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.*" PrivateAssets="all" />

    <!-- 認証 -->
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.*" />
    <PackageReference Include="Microsoft.Identity.Web" Version="3.*" />
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.*" />

    <!-- バリデーション -->
    <PackageReference Include="FluentValidation" Version="11.*" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.*" />

    <!-- メッセージング -->
    <PackageReference Include="Confluent.Kafka" Version="2.*" />

    <!-- キャッシュ -->
    <PackageReference Include="StackExchange.Redis" Version="2.*" />

    <!-- 耐障害性 -->
    <PackageReference Include="Polly" Version="8.*" />
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="9.*" />

    <!-- ロギング -->
    <PackageReference Include="Serilog.AspNetCore" Version="8.*" />
    <PackageReference Include="Serilog.Sinks.Console" Version="6.*" />
    <PackageReference Include="Serilog.Formatting.Compact" Version="3.*" />

    <!-- OpenTelemetry -->
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.*" />

    <!-- ヘルスチェック -->
    <PackageReference Include="AspNetCore.HealthChecks.NpgSql" Version="9.*" />
    <PackageReference Include="AspNetCore.HealthChecks.Redis" Version="9.*" />

    <!-- Azure Identity -->
    <PackageReference Include="Azure.Identity" Version="1.*" />
  </ItemGroup>
</Project>
```

### 1.2 ディレクトリ構造

```
AuthService/
├── AuthService.csproj
├── Program.cs
├── Endpoints/
│   ├── AuthEndpoints.cs
│   ├── MfaEndpoints.cs
│   ├── PasswordEndpoints.cs
│   ├── UserRegistrationEndpoints.cs
│   └── OAuthEndpoints.cs
├── Services/
│   ├── Interfaces/
│   │   ├── IAuthService.cs
│   │   ├── IJwtTokenService.cs
│   │   ├── IMfaService.cs
│   │   ├── IPasswordService.cs
│   │   ├── ISecurityService.cs
│   │   ├── IOAuthService.cs
│   │   ├── ITotpService.cs
│   │   └── IUserRegistrationService.cs
│   ├── AuthService.cs
│   ├── JwtTokenService.cs
│   ├── MfaService.cs
│   ├── PasswordService.cs
│   ├── SecurityService.cs
│   ├── OAuthService.cs
│   ├── TotpService.cs
│   └── UserRegistrationService.cs
├── Repositories/
│   ├── Interfaces/
│   │   ├── IUserRepository.cs
│   │   ├── IUserSessionRepository.cs
│   │   ├── ISecurityLogRepository.cs
│   │   ├── IOAuthAccountRepository.cs
│   │   ├── IPasswordResetRepository.cs
│   │   ├── IMfaRepository.cs
│   │   ├── IRefreshTokenRepository.cs
│   │   ├── IOutboxEventRepository.cs
│   │   └── IRoleRepository.cs
│   ├── UserRepository.cs
│   ├── UserSessionRepository.cs
│   ├── SecurityLogRepository.cs
│   ├── OAuthAccountRepository.cs
│   ├── PasswordResetRepository.cs
│   ├── MfaRepository.cs
│   ├── RefreshTokenRepository.cs
│   ├── OutboxEventRepository.cs
│   └── RoleRepository.cs
├── Models/
│   ├── User.cs
│   ├── UserSession.cs
│   ├── Role.cs
│   ├── UserRole.cs
│   ├── OAuthAccount.cs
│   ├── PasswordReset.cs
│   ├── UserMfa.cs
│   ├── SecurityLog.cs
│   ├── RefreshToken.cs
│   └── OutboxEvent.cs
├── DTOs/
│   ├── Requests/
│   │   ├── LoginRequest.cs
│   │   ├── TokenRefreshRequest.cs
│   │   ├── PasswordResetRequest.cs
│   │   ├── PasswordResetConfirmRequest.cs
│   │   ├── PasswordChangeRequest.cs
│   │   ├── MfaVerificationRequest.cs
│   │   └── UserCreateRequest.cs
│   └── Responses/
│       ├── LoginResponse.cs
│       ├── TokenRefreshResponse.cs
│       ├── UserInfoResponse.cs
│       ├── UserResponse.cs
│       ├── MfaSetupResponse.cs
│       ├── MessageResponse.cs
│       ├── LogoutResponse.cs
│       ├── TokenValidationResponse.cs
│       ├── GraphUserResponse.cs
│       └── OAuthAccountDto.cs
├── Configurations/
│   ├── JwtSettings.cs
│   ├── AuthSettings.cs
│   ├── SessionSettings.cs
│   └── RedisSettings.cs
├── Enums/
│   ├── UserStatus.cs
│   ├── UserRoleType.cs
│   ├── SecurityEventType.cs
│   └── OutboxStatus.cs
├── Exceptions/
│   ├── NotFoundException.cs
│   ├── BusinessException.cs
│   ├── UnauthorizedException.cs
│   ├── ForbiddenException.cs
│   ├── ConcurrencyException.cs
│   ├── AccountLockedException.cs
│   └── MfaRequiredException.cs
├── Validators/
│   ├── LoginRequestValidator.cs
│   ├── RegisterRequestValidator.cs
│   ├── MfaCodeValidator.cs
│   ├── PasswordResetRequestValidator.cs
│   ├── PasswordResetConfirmRequestValidator.cs
│   ├── PasswordChangeRequestValidator.cs
│   └── TokenRefreshRequestValidator.cs
├── Events/
│   ├── IAuthEvent.cs
│   ├── UserRegisteredEvent.cs
│   ├── UserAuthenticatedEvent.cs
│   ├── UserLoggedOutEvent.cs
│   ├── LoginFailedEvent.cs
│   ├── AccountLockedEvent.cs
│   ├── AccountUnlockedEvent.cs
│   ├── PasswordChangedEvent.cs
│   ├── MfaEnabledEvent.cs
│   ├── MfaDisabledEvent.cs
│   ├── SecurityIncidentEvent.cs
│   ├── PasswordResetRequestedEvent.cs
│   └── TokenRevokedEvent.cs
├── Infrastructure/
│   ├── Persistence/
│   │   └── AuthDbContext.cs
│   ├── Middleware/
│   │   ├── CorrelationIdMiddleware.cs
│   │   ├── CorrelationIdMiddlewareExtensions.cs
│   │   ├── SecurityHeadersMiddleware.cs
│   │   └── SecurityHeadersMiddlewareExtensions.cs
│   ├── BackgroundServices/
│   │   ├── OutboxPublisher.cs
│   │   ├── TokenCleanupService.cs
│   │   ├── FailedAttemptResetService.cs
│   │   └── SessionTimeoutService.cs
│   └── Security/
│       └── Argon2PasswordHasher.cs
├── Migrations/
├── appsettings.json
├── appsettings.Development.json
├── appsettings.Production.json
├── Dockerfile
└── .dockerignore
```

### 1.3 Program.cs スケルトン

```csharp
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { Status = "UP" }));

app.Run();
```

### 1.4 appsettings.json（安全なデフォルト値）

```json
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
  },
  "Auth": {
    "MaxFailedAttempts": 5,
    "AutoUnlockMinutes": 30,
    "FailedAttemptResetMinutes": 15
  },
  "Session": {
    "Timeout": 1800,
    "MaxConcurrentSessions": 5
  },
  "Jwt": {
    "Issuer": "",
    "Audience": "",
    "AccessExpirationSeconds": 3600,
    "RefreshExpirationSeconds": 604800,
    "MaxActiveRefreshTokens": 10
  }
}
```

### 1.5 appsettings.Development.json

```json
{
  "DetailedErrors": true,
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Debug",
      "SkiShop": "Debug",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000", "http://localhost:5173"]
  }
}
```

### 1.6 appsettings.Production.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "SkiShop": "Information"
    }
  }
}
```

### 1.7 Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["AuthService/AuthService.csproj", "AuthService/"]
RUN dotnet restore "AuthService/AuthService.csproj"
COPY . .
WORKDIR "/src/AuthService"
RUN dotnet publish "AuthService.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN groupadd -r skishop && useradd -r -g skishop -d /app -s /sbin/nologin skishop
COPY --from=build --chown=skishop:skishop /app/publish .
USER skishop

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD wget --spider -q http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "AuthService.dll"]
```

### 1.8 .dockerignore

```
**/bin/
**/obj/
.git/
.github/
*.md
*.sln.DotSettings
.idea/
.vs/
design-docs/
impl-plan/
```

### Phase 1 完了チェックリスト

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | `dotnet build AuthService/AuthService.csproj` | 警告なし成功 |
| 2 | `dotnet run --project AuthService` | 起動確認（`/health` で 200 応答） |
| 3 | .csproj: `TreatWarningsAsErrors=true` | 設定済み |
| 4 | .csproj: `Nullable=enable` | 設定済み |
| 5 | .csproj: `TargetFramework=net10.0` | 設定済み |
| 6 | .csproj: プレリリース版パッケージなし | `-preview`, `-beta`, `-rc` なし |
| 7 | appsettings.json: 秘密情報なし | パスワード、API キー、接続文字列なし |
| 8 | appsettings.json: `DetailedErrors: false` | 設定済み |
| 9 | appsettings.json: `AddServerHeader: false` | 設定済み |
| 10 | Dockerfile: マルチステージビルド | `sdk` → `aspnet` 分離 |
| 11 | Dockerfile: 非 root ユーザー | `USER skishop` |
| 12 | Dockerfile: HEALTHCHECK あり | `/health` エンドポイント |
| 13 | Dockerfile: `latest` タグなし | `10.0` 固定 |
| 14 | .dockerignore: 不要ファイル除外 | `bin/`, `obj/`, `.git/`, `*.md` |
| 15 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 16 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 17 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## Phase 2: エンティティ・Value Object・Enum 定義

### 目的

設計書 §4（データモデル）および §12（EF Core エンティティ定義）に基づき、全エンティティ、Enum、Value Object、DbContext、マイグレーションを作成する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `AuthService/Enums/UserStatus.cs` | 作成 | ユーザーステータス Enum |
| 2 | `AuthService/Enums/UserRoleType.cs` | 作成 | ロール種別 Enum |
| 3 | `AuthService/Enums/SecurityEventType.cs` | 作成 | セキュリティイベント種別 Enum |
| 4 | `AuthService/Enums/OutboxStatus.cs` | 作成 | Outbox ステータス Enum |
| 5 | `AuthService/Models/User.cs` | 作成 | ユーザー Aggregate Root |
| 6 | `AuthService/Models/UserSession.cs` | 作成 | セッションエンティティ |
| 7 | `AuthService/Models/Role.cs` | 作成 | ロールエンティティ |
| 8 | `AuthService/Models/UserRole.cs` | 作成 | ユーザーロール中間エンティティ |
| 9 | `AuthService/Models/OAuthAccount.cs` | 作成 | OAuth アカウントエンティティ |
| 10 | `AuthService/Models/PasswordReset.cs` | 作成 | パスワードリセットエンティティ |
| 11 | `AuthService/Models/UserMfa.cs` | 作成 | MFA エンティティ |
| 12 | `AuthService/Models/SecurityLog.cs` | 作成 | セキュリティログエンティティ |
| 13 | `AuthService/Models/RefreshToken.cs` | 作成 | リフレッシュトークンエンティティ |
| 14 | `AuthService/Models/OutboxEvent.cs` | 作成 | Outbox イベントエンティティ |
| 15 | `AuthService/Models/PasswordHistory.cs` | 作成 | パスワード履歴エンティティ（設計書 §29.1） |
| 16 | `AuthService/Models/OAuthClient.cs` | 作成 | M2M Client Credentials エンティティ（設計書 §29.5） |
| 17 | `AuthService/Models/AuditLog.cs` | 作成 | 監査ログエンティティ（設計書 §29.9） |
| 18 | `AuthService/Infrastructure/Persistence/AuthDbContext.cs` | 作成 | DbContext + Fluent API + SaveChanges 自動タイムスタンプ |
| 19 | `AuthService/Infrastructure/Persistence/IHasTimestamps.cs` | 作成 | タイムスタンプマーカーインターフェース |
| 20 | `AuthService/Infrastructure/Persistence/AuditLogInterceptor.cs` | 作成 | SaveChangesInterceptor による監査ログ自動記録（設計書 §29.9） |
| 21 | `AuthService/Exceptions/*.cs` | 作成 | ドメイン例外クラス群 |

### 2.1 Enum 定義

```csharp
// Enums/UserStatus.cs
public enum UserStatus
{
    PendingVerification,
    Active,
    Suspended
}

// Enums/UserRoleType.cs
public enum UserRoleType
{
    Admin,
    Manager,
    Staff,
    Employee,
    User,
    Customer
}

// Enums/SecurityEventType.cs
public enum SecurityEventType
{
    LoginSuccess,
    LoginFailed,
    Logout,
    AccountLocked,
    AccountUnlocked,
    PasswordChanged,
    PasswordResetRequested,
    PasswordResetCompleted,
    MfaEnabled,
    MfaDisabled,
    MfaVerified,
    MfaFailed,
    TokenRevoked,
    SessionCreated,
    SessionExpired,
    OAuthLinked,
    OAuthUnlinked,
    UserRegistered,
    UserDeleted,
    SecurityIncident
}

// Enums/OutboxStatus.cs
public enum OutboxStatus
{
    Pending,
    Processing,
    Published,
    Failed,
    DeadLetter
}
```

> **注記**: DB カラムには文字列（UPPER_CASE）で保存し、EF Core の `HasConversion` で Enum ↔ 文字列変換を行う。

### 2.2 エンティティ定義（設計書 §12 準拠）

User エンティティ（Aggregate Root）:

```csharp
[Table("users")]
public class User : IHasTimestamps
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("email")]
    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Column("username")]
    [MaxLength(100)]
    public string? Username { get; set; }

    [Column("password_hash")]
    [MaxLength(255)]
    public string? PasswordHash { get; set; }

    [Column("first_name")]
    [MaxLength(100)]
    public string? FirstName { get; set; }

    [Column("last_name")]
    [MaxLength(100)]
    public string? LastName { get; set; }

    [Column("status")]
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "PENDING_VERIFICATION";

    [Column("role")]
    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = "USER";

    [Column("email_verified")]
    public bool EmailVerified { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("account_locked")]
    public bool AccountLocked { get; set; }

    [Column("locked_at")]
    public DateTime? LockedAt { get; set; }

    [Column("failed_login_attempts")]
    public int FailedLoginAttempts { get; set; }

    [Column("last_login")]
    public DateTime? LastLogin { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Timestamp]
    [Column("row_version")]
    public byte[] RowVersion { get; set; } = [];

    // ナビゲーションプロパティ
    public ICollection<UserSession> Sessions { get; set; } = [];
    public ICollection<OAuthAccount> OAuthAccounts { get; set; } = [];
    public ICollection<SecurityLog> SecurityLogs { get; set; } = [];
    public ICollection<UserRole> UserRoles { get; set; } = [];
    public UserMfa? Mfa { get; set; }
}
```

残りのエンティティ（UserSession, Role, UserRole, OAuthAccount, UserMfa, SecurityLog, OutboxEvent）は設計書 §12 に定義された通り実装する。全エンティティに `[Table]`, `[Column]` 属性で snake_case マッピング、`IHasTimestamps` 実装（SecurityLog 除く）を含める。

#### PasswordReset エンティティ（設計書 §29.2 拡張）

`TokenType` カラムを追加し、パスワードリセットとメール検証の両方に使用する:

```csharp
[Table("password_resets")]
public class PasswordReset : IHasTimestamps
{
    // ... 既存プロパティ ...

    /// <summary>トークン種別（PASSWORD_RESET / EMAIL_VERIFICATION）</summary>
    [Column("token_type")]
    [Required]
    [MaxLength(30)]
    public string TokenType { get; set; } = "PASSWORD_RESET";
}
```

#### RefreshToken エンティティ（設計書 §19 / §29.10 準拠 — Replay Detection 対応）

設計書 §19 に基づき、トークンローテーション + Replay Detection に必要なフィールドを含める:

```csharp
[Table("refresh_tokens")]
public class RefreshToken : IHasTimestamps
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    [Column("token")]
    [Required]
    [MaxLength(512)]
    public string Token { get; set; } = string.Empty;

    [Column("jti")]
    [Required]
    [MaxLength(36)]
    public string Jti { get; set; } = string.Empty;

    /// <summary>トークンファミリー ID（同一ログインセッション由来のトークン群を識別）</summary>
    [Column("family_id")]
    [Required]
    [MaxLength(36)]
    public string FamilyId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>前トークン ID（ローテーションチェーン追跡用）</summary>
    [Column("previous_token_id")]
    [MaxLength(36)]
    public string? PreviousTokenId { get; set; }

    /// <summary>絶対有効期限（ファミリー全体の最大寿命、90 日）</summary>
    [Column("absolute_expiry")]
    public DateTime AbsoluteExpiry { get; set; }

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("is_revoked")]
    public bool IsRevoked { get; set; }

    [Column("revoked_at")]
    public DateTime? RevokedAt { get; set; }

    /// <summary>このトークンを置き換えた後続トークンの ID</summary>
    [Column("replaced_by_token")]
    [MaxLength(36)]
    public string? ReplacedByToken { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ナビゲーションプロパティ
    public User User { get; set; } = null!;
}
```

#### PasswordHistory エンティティ（設計書 §29.1）

パスワード再利用防止（直近 5 回分チェック）用:

```csharp
[Table("password_histories")]
public class PasswordHistory
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("user_id")]
    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    [Column("password_hash")]
    [Required]
    [MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ナビゲーションプロパティ
    public User User { get; set; } = null!;
}
```

#### OAuthClient エンティティ（設計書 §29.5 — M2M Client Credentials）

サービス間認証（OAuth 2.0 Client Credentials Grant）用:

```csharp
[Table("oauth_clients")]
public class OAuthClient : IHasTimestamps
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("client_id")]
    [Required]
    [MaxLength(100)]
    public string ClientId { get; set; } = string.Empty;

    [Column("client_secret_hash")]
    [Required]
    [MaxLength(255)]
    public string ClientSecretHash { get; set; } = string.Empty;

    [Column("name")]
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    [MaxLength(500)]
    public string? Description { get; set; }

    [Column("allowed_scopes")]
    [Required]
    [MaxLength(1000)]
    public string AllowedScopes { get; set; } = string.Empty;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

#### AuditLog エンティティ（設計書 §29.9）

spec.md §監査ログ設計に基づく監査証跡。`AuditLogInterceptor` で自動記録:

```csharp
[Table("audit_logs")]
public class AuditLog
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("service_name")]
    [Required]
    [MaxLength(100)]
    public string ServiceName { get; set; } = "AuthService";

    [Column("entity_type")]
    [Required]
    [MaxLength(100)]
    public string EntityType { get; set; } = string.Empty;

    [Column("entity_id")]
    [Required]
    [MaxLength(36)]
    public string EntityId { get; set; } = string.Empty;

    [Column("action")]
    [Required]
    [MaxLength(20)]
    public string Action { get; set; } = string.Empty;

    [Column("actor_id")]
    [MaxLength(36)]
    public string? ActorId { get; set; }

    [Column("actor_role")]
    [MaxLength(50)]
    public string? ActorRole { get; set; }

    [Column("ip_address")]
    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [Column("correlation_id")]
    [MaxLength(36)]
    public string? CorrelationId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

### 2.3 AuthDbContext（設計書 §18 準拠）

```csharp
public class AuthDbContext(
    DbContextOptions<AuthDbContext> options,
    TimeProvider timeProvider) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<OAuthAccount> OAuthAccounts => Set<OAuthAccount>();
    public DbSet<PasswordReset> PasswordResets => Set<PasswordReset>();
    public DbSet<UserMfa> UserMfa => Set<UserMfa>();
    public DbSet<SecurityLog> SecurityLogs => Set<SecurityLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    public DbSet<PasswordHistory> PasswordHistories => Set<PasswordHistory>();
    public DbSet<OAuthClient> OAuthClients => Set<OAuthClient>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User: UNIQUE インデックス + CHECK 制約
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.Username).IsUnique().HasFilter("username IS NOT NULL");
            entity.HasIndex(u => u.Status).HasDatabaseName("idx_users_status");
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_users_status",
                    "status IN ('PENDING_VERIFICATION', 'ACTIVE', 'SUSPENDED')");
                t.HasCheckConstraint("ck_users_role",
                    "role IN ('ADMIN', 'MANAGER', 'STAFF', 'EMPLOYEE', 'USER', 'CUSTOMER')");
            });
            entity.Property(u => u.RowVersion).IsRowVersion();
        });

        // Role: シードデータ
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(r => r.Name).IsUnique();
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_roles_name",
                    "name IN ('ADMIN', 'MANAGER', 'STAFF', 'EMPLOYEE', 'USER', 'CUSTOMER')");
            });
            entity.HasData(
                new Role { Id = "role-admin", Name = "ADMIN", Description = "システム管理者" },
                new Role { Id = "role-manager", Name = "MANAGER", Description = "店舗マネージャー" },
                new Role { Id = "role-staff", Name = "STAFF", Description = "店舗スタッフ" },
                new Role { Id = "role-employee", Name = "EMPLOYEE", Description = "従業員" },
                new Role { Id = "role-user", Name = "USER", Description = "一般ユーザー" },
                new Role { Id = "role-customer", Name = "CUSTOMER", Description = "顧客" }
            );
        });

        // 以下、UserSession, UserRole, OAuthAccount, PasswordReset, UserMfa,
        // SecurityLog, RefreshToken, OutboxEvent のリレーション・インデックス・CHECK 制約を
        // 設計書 §18 に完全準拠して定義する（ON DELETE 動作含む）

        // PasswordReset: token_type CHECK 制約（設計書 §29.2）
        modelBuilder.Entity<PasswordReset>(entity =>
        {
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("ck_password_resets_token_type",
                    "token_type IN ('PASSWORD_RESET', 'EMAIL_VERIFICATION')");
            });
        });

        // RefreshToken: family_id インデックス + Replay Detection 用（設計書 §29.10）
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(r => r.Token).IsUnique();
            entity.HasIndex(r => r.FamilyId).HasDatabaseName("idx_refresh_tokens_family_id");
            entity.HasIndex(r => new { r.UserId, r.IsRevoked }).HasDatabaseName("idx_refresh_tokens_user_active");
        });

        // PasswordHistory: ユーザー別・作成日時降順インデックス（設計書 §29.1）
        modelBuilder.Entity<PasswordHistory>(entity =>
        {
            entity.HasIndex(p => new { p.UserId, p.CreatedAt })
                .HasDatabaseName("idx_password_histories_user_id")
                .IsDescending(false, true);
            entity.HasOne(p => p.User).WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // OAuthClient: client_id UNIQUE（設計書 §29.5）
        modelBuilder.Entity<OAuthClient>(entity =>
        {
            entity.HasIndex(o => o.ClientId).IsUnique();
        });

        // AuditLog: インデックス設計（設計書 §29.9）
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(a => a.EntityType).HasDatabaseName("idx_audit_logs_entity_type");
            entity.HasIndex(a => a.ActorId).HasDatabaseName("idx_audit_logs_actor_id");
            entity.HasIndex(a => a.CreatedAt).HasDatabaseName("idx_audit_logs_created_at");
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var entry in ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            if (entry.Entity is IHasTimestamps entity)
            {
                if (entry.State == EntityState.Added)
                    entity.CreatedAt = now;
                entity.UpdatedAt = now;
            }
            if (entry.State == EntityState.Added && entry.Entity is SecurityLog log)
                log.CreatedAt = now;
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
```

### 2.4 例外クラス（設計書 §28 準拠）

```csharp
public class NotFoundException(string message) : Exception(message);
public class BusinessException(string message) : Exception(message);
public class UnauthorizedException(string? message = null) : Exception(message ?? "認証が必要です");
public class ForbiddenException(string? message = null) : Exception(message ?? "アクセスが拒否されました");
public class ConcurrencyException(string message) : Exception(message);
public class AccountLockedException(string message) : Exception(message);
public class MfaRequiredException(string sessionToken) : Exception("MFA 認証が必要です")
{
    public string SessionToken { get; } = sessionToken;
}
```

### 2.5 Program.cs 更新（DbContext 登録）

```csharp
// Npgsql タイムスタンプ動作設定（設計書 §29.6）
// DateTime.Kind == DateTimeKind.Utc を保証し、PostgreSQL の timestamptz との整合性を確保
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);

builder.Services.AddSingleton(TimeProvider.System);

// AuditLogInterceptor 登録（設計書 §29.9）
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditLogInterceptor>();
builder.Services.AddDbContext<AuthDbContext>((sp, options) =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.AddInterceptors(sp.GetRequiredService<AuditLogInterceptor>());
});
```

### Phase 2 完了チェックリスト

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | `dotnet build` | 警告なし成功 |
| 2 | `dotnet ef migrations add Initial` | マイグレーション生成成功 |
| 3 | 全 13 エンティティ定義 | User, UserSession, Role, UserRole, OAuthAccount, PasswordReset, UserMfa, SecurityLog, RefreshToken, OutboxEvent, PasswordHistory, OAuthClient, AuditLog |
| 4 | 全プロパティに `[Column("snake_case")]` | カラム名明示 |
| 5 | コレクションナビゲーション | `= []` で初期化 |
| 6 | `IHasTimestamps` 実装 | SecurityLog, PasswordHistory, AuditLog 以外の全エンティティ |
| 7 | `SaveChangesAsync` オーバーライド | TimeProvider による `CreatedAt`/`UpdatedAt` 自動管理 |
| 8 | FK ON DELETE 動作 | 設計書 §外部キー制約テーブル準拠 |
| 9 | CHECK 制約 | `ck_users_status`, `ck_users_role`, `ck_roles_name`, `ck_outbox_status`, `ck_password_resets_token_type` |
| 10 | シードデータ | 6 ロールの HasData |
| 11 | インデックス設計 | 設計書 §インデックス設計テーブル準拠 |
| 12 | `DateTime.Now` 不使用 | `DateTime.UtcNow` / `TimeProvider` を使用 |
| 13 | RefreshToken | `FamilyId`, `PreviousTokenId`, `AbsoluteExpiry`, `RevokedAt`, `ReplacedByToken` フィールド含む（設計書 §19） |
| 14 | PasswordReset | `TokenType` カラム含む（設計書 §29.2） |
| 15 | AuditLogInterceptor | `SaveChangesInterceptor` 実装 + DbContext に統合（設計書 §29.9） |
| 16 | Npgsql 設定 | `Npgsql.EnableLegacyTimestampBehavior = false`（設計書 §29.6） |
| 13 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 14 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 15 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## Phase 3: Repository 層実装

### 目的

設計書 §13 / §21 に基づき、全 Aggregate Root の Repository インターフェースおよび EF Core 実装を作成する。DDD の Repository パターン（1 Aggregate Root = 1 Repository）を厳守する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `AuthService/Repositories/Interfaces/IUserRepository.cs` | 作成 | User Aggregate Repository インターフェース |
| 2 | `AuthService/Repositories/Interfaces/IUserSessionRepository.cs` | 作成 | UserSession Repository インターフェース |
| 3 | `AuthService/Repositories/Interfaces/ISecurityLogRepository.cs` | 作成 | SecurityLog Repository インターフェース |
| 4 | `AuthService/Repositories/Interfaces/IOAuthAccountRepository.cs` | 作成 | OAuthAccount Repository インターフェース |
| 5 | `AuthService/Repositories/Interfaces/IPasswordResetRepository.cs` | 作成 | PasswordReset Repository インターフェース |
| 6 | `AuthService/Repositories/Interfaces/IMfaRepository.cs` | 作成 | UserMfa Repository インターフェース |
| 7 | `AuthService/Repositories/Interfaces/IRefreshTokenRepository.cs` | 作成 | RefreshToken Repository インターフェース |
| 8 | `AuthService/Repositories/Interfaces/IOutboxEventRepository.cs` | 作成 | OutboxEvent Repository インターフェース |
| 9 | `AuthService/Repositories/Interfaces/IRoleRepository.cs` | 作成 | Role Repository インターフェース |
| 10 | `AuthService/Repositories/Interfaces/IPasswordHistoryRepository.cs` | 作成 | PasswordHistory Repository インターフェース（設計書 §29.1） |
| 11 | `AuthService/Repositories/Interfaces/IOAuthClientRepository.cs` | 作成 | OAuthClient Repository インターフェース（設計書 §29.5） |
| 12-22 | `AuthService/Repositories/*.cs` | 作成 | 各 Repository の EF Core 実装 |

### 3.1 Repository インターフェース（設計書 §13 / §21 準拠）

全 async メソッドに `CancellationToken ct = default` を含む。

```csharp
public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<User?> FindByUsernameAsync(string username, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IUserSessionRepository
{
    Task<UserSession?> FindBySessionIdAsync(string sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<UserSession>> FindActiveByUserIdAsync(string userId, CancellationToken ct = default);
    Task AddAsync(UserSession session, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface ISecurityLogRepository
{
    Task AddAsync(SecurityLog log, CancellationToken ct = default);
    Task<IReadOnlyList<SecurityLog>> FindByUserIdAsync(string userId, int limit = 50, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByTokenAsync(string token, CancellationToken ct = default);
    Task<IReadOnlyList<RefreshToken>> FindActiveByUserIdAsync(string userId, CancellationToken ct = default);
    Task AddAsync(RefreshToken refreshToken, CancellationToken ct = default);
    Task RevokeAllByUserIdAsync(string userId, DateTime revokedAt, CancellationToken ct = default);
    Task DeleteExpiredAsync(DateTime cutoff, CancellationToken ct = default);
    /// <summary>family_id に属する全トークンを一括無効化（Replay Detection 用、設計書 §29.10）</summary>
    Task RevokeAllByFamilyIdAsync(string familyId, DateTime revokedAt, CancellationToken ct = default);
    /// <summary>ユーザーのアクティブファミリー数を取得（同時セッション制限用、設計書 §29.10）</summary>
    Task<int> CountActiveFamiliesByUserIdAsync(string userId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>パスワード履歴 Repository（設計書 §29.1）</summary>
public interface IPasswordHistoryRepository
{
    Task<IReadOnlyList<PasswordHistory>> FindRecentByUserIdAsync(
        string userId, int count = 5, CancellationToken ct = default);
    Task AddAsync(PasswordHistory history, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>OAuthClient Repository（設計書 §29.5）</summary>
public interface IOAuthClientRepository
{
    Task<OAuthClient?> FindByClientIdAsync(string clientId, CancellationToken ct = default);
    Task AddAsync(OAuthClient client, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

> 残りの `IOAuthAccountRepository`, `IPasswordResetRepository`, `IMfaRepository`, `IOutboxEventRepository`, `IRoleRepository` は設計書 §21 に定義された通り実装する。

### 3.2 Repository 実装パターン

```csharp
public class UserRepository(AuthDbContext context) : IUserRepository
{
    public async Task<User?> FindByEmailAsync(string email, CancellationToken ct = default)
        => await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task<User?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.Users
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> FindByUsernameAsync(string username, CancellationToken ct = default)
        => await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username, ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await context.Users.AddAsync(user, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
```

### 3.3 Program.cs 更新（DI 登録）

```csharp
// Repository 登録（Scoped）
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserSessionRepository, UserSessionRepository>();
builder.Services.AddScoped<ISecurityLogRepository, SecurityLogRepository>();
builder.Services.AddScoped<IOAuthAccountRepository, OAuthAccountRepository>();
builder.Services.AddScoped<IPasswordResetRepository, PasswordResetRepository>();
builder.Services.AddScoped<IMfaRepository, MfaRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IOutboxEventRepository, OutboxEventRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IPasswordHistoryRepository, PasswordHistoryRepository>();
builder.Services.AddScoped<IOAuthClientRepository, OAuthClientRepository>();
```

### Phase 3 完了チェックリスト

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | `dotnet build` | 警告なし成功 |
| 2 | 全 11 Repository インターフェース定義 | 設計書 §13 / §21 / §29.1 / §29.5 準拠（IPasswordHistoryRepository, IOAuthClientRepository 含む） |
| 3 | 全 11 Repository 実装 | EF Core による実装 |
| 4 | 全 async メソッドに `CancellationToken ct = default` | パラメータ含む |
| 5 | 読み取りクエリに `AsNoTracking()` | 変更追跡不要なクエリ |
| 6 | DI 登録（Program.cs） | 全 Repository が Scoped で登録 |
| 7 | 1 Aggregate Root = 1 Repository | 異なる Aggregate のクエリ混在なし |
| 8 | コンストラクタインジェクション | primary constructor 使用 |
| 9 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 10 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 11 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## Phase 4: Service 層実装

### 目的

設計書 §13 / §22 に基づき、ビジネスロジックを Service 層に実装する。JWT トークン発行（RS256）、パスワードハッシュ（Argon2id）、アカウントロックアウト、MFA（TOTP）、セキュリティログを含む。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `AuthService/Services/Interfaces/IAuthService.cs` | 作成 | 認証 Service インターフェース |
| 2 | `AuthService/Services/Interfaces/IJwtTokenService.cs` | 作成 | JWT トークン Service インターフェース |
| 3 | `AuthService/Services/Interfaces/IMfaService.cs` | 作成 | MFA Service インターフェース |
| 4 | `AuthService/Services/Interfaces/IPasswordService.cs` | 作成 | パスワード管理 Service インターフェース |
| 5 | `AuthService/Services/Interfaces/ISecurityService.cs` | 作成 | セキュリティ Service インターフェース |
| 6 | `AuthService/Services/Interfaces/IOAuthService.cs` | 作成 | OAuth2 Service インターフェース |
| 7 | `AuthService/Services/Interfaces/ITotpService.cs` | 作成 | TOTP Service インターフェース |
| 8 | `AuthService/Services/Interfaces/IUserRegistrationService.cs` | 作成 | ユーザー登録 Service インターフェース |
| 9 | `AuthService/Services/Interfaces/IClientCredentialsService.cs` | 作成 | M2M Client Credentials Service インターフェース（設計書 §29.5） |
| 10 | `AuthService/Services/Interfaces/IAuditLogService.cs` | 作成 | 監査ログ Service インターフェース（設計書 §29.9） |
| 11 | `AuthService/Services/AuthService.cs` | 作成 | ログイン・ログアウト・トークンリフレッシュ |
| 12 | `AuthService/Services/JwtTokenService.cs` | 作成 | JWT 生成（RS256）・検証・失効 |
| 13 | `AuthService/Services/MfaService.cs` | 作成 | MFA セットアップ・検証・無効化 |
| 14 | `AuthService/Services/PasswordService.cs` | 作成 | パスワードリセット・変更（履歴チェック含む） |
| 15 | `AuthService/Services/SecurityService.cs` | 作成 | セキュリティログ・アカウントロック管理 |
| 16 | `AuthService/Services/OAuthService.cs` | 作成 | OAuth2 フロー・アカウントリンク |
| 17 | `AuthService/Services/TotpService.cs` | 作成 | TOTP シークレット生成・コード検証 |
| 18 | `AuthService/Services/UserRegistrationService.cs` | 作成 | ユーザー登録・論理削除・物理削除・メール検証 |
| 19 | `AuthService/Services/ClientCredentialsService.cs` | 作成 | M2M トークン発行（設計書 §29.5） |
| 20 | `AuthService/Services/AuditLogService.cs` | 作成 | 手動監査ログ記録（設計書 §29.9） |
| 21 | `AuthService/Infrastructure/Security/Argon2PasswordHasher.cs` | 作成 | Argon2id カスタム `IPasswordHasher<User>` |
| 22 | `AuthService/Configurations/JwtSettings.cs` | 作成 | JWT 設定（IOptions<T>） |
| 23 | `AuthService/Configurations/AuthSettings.cs` | 作成 | 認証設定（IOptions<T>） |
| 24 | `AuthService/Configurations/SessionSettings.cs` | 作成 | セッション設定（IOptions<T>） |

### 4.1 Service インターフェース（設計書 §13 / §22 準拠）

```csharp
public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken ct = default);
    Task<TokenRefreshResponse> RefreshTokenAsync(TokenRefreshRequest request, CancellationToken ct = default);
    Task LogoutAsync(string sessionId, CancellationToken ct = default);
    Task<UserInfoResponse> GetCurrentUserAsync(string userId, CancellationToken ct = default);
}

public interface IJwtTokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    Task<bool> ValidateTokenAsync(string token, CancellationToken ct = default);
    Task RevokeTokenAsync(string tokenId, CancellationToken ct = default);
}

public interface ISecurityService
{
    Task LogSecurityEventAsync(string? userId, string eventType, string? ipAddress,
        string? userAgent, Dictionary<string, object>? details = null, CancellationToken ct = default);
    Task<bool> IncrementFailedAttemptsAsync(string userId, CancellationToken ct = default);
    Task ResetFailedAttemptsAsync(string userId, CancellationToken ct = default);
    Task<bool> IsAccountLockedAsync(string userId, CancellationToken ct = default);
    Task UnlockAccountAsync(string userId, CancellationToken ct = default);
}

/// <summary>M2M Client Credentials Service（設計書 §29.5）</summary>
public interface IClientCredentialsService
{
    Task<ClientCredentialsResponse> IssueTokenAsync(
        ClientCredentialsRequest request, CancellationToken ct = default);
}

/// <summary>監査ログ Service（設計書 §29.9）</summary>
public interface IAuditLogService
{
    Task LogAsync(string action, string entityType, string entityId,
        string? actorId = null, string? actorRole = null, CancellationToken ct = default);
}
```

### 4.2 AuthService 実装（コア認証ロジック）

```csharp
public class AuthServiceImpl(
    IUserRepository userRepository,
    IUserSessionRepository sessionRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IJwtTokenService jwtTokenService,
    ISecurityService securityService,
    IOutboxEventRepository outboxRepository,
    IPasswordHasher<User> passwordHasher,
    TimeProvider timeProvider,
    IOptions<AuthSettings> authOptions,
    IOptions<SessionSettings> sessionOptions,
    ILogger<AuthServiceImpl> logger) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(
        LoginRequest request, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        var user = await userRepository.FindByEmailAsync(request.Email, ct)
            ?? throw new UnauthorizedException("無効な資格情報が提供されました");

        // アカウントロック確認
        if (user.AccountLocked)
            throw new AccountLockedException("アカウントがロックされています");

        // アカウント状態確認
        if (user.Status != "ACTIVE")
            throw new BusinessException("アカウントが有効ではありません");

        // パスワード検証
        var verifyResult = passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash ?? string.Empty, request.Password);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            var locked = await securityService.IncrementFailedAttemptsAsync(user.Id, ct);
            await securityService.LogSecurityEventAsync(user.Id, "LOGIN_FAILED",
                ipAddress, userAgent,
                new Dictionary<string, object> { ["failureReason"] = "invalid_credentials" }, ct);
            throw new UnauthorizedException("無効な資格情報が提供されました");
        }

        // MFA チェック
        if (user.Mfa is { IsEnabled: true })
        {
            var sessionToken = Guid.NewGuid().ToString();
            // Redis にセッショントークンを保存（MFA 検証用、5分間有効）
            throw new MfaRequiredException(sessionToken);
        }

        // ログイン成功処理
        await securityService.ResetFailedAttemptsAsync(user.Id, ct);
        user.LastLogin = timeProvider.GetUtcNow().UtcDateTime;

        // セッション作成
        var session = new UserSession
        {
            UserId = user.Id,
            SessionId = GenerateSessionId(),
            IpAddress = ipAddress,
            UserAgent = userAgent,
            ExpiresAt = timeProvider.GetUtcNow().UtcDateTime.AddSeconds(sessionOptions.Value.Timeout)
        };
        await sessionRepository.AddAsync(session, ct);

        // トークン生成
        var accessToken = jwtTokenService.GenerateAccessToken(user);
        var refreshToken = jwtTokenService.GenerateRefreshToken();

        // リフレッシュトークン保存
        await refreshTokenRepository.AddAsync(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            Jti = Guid.NewGuid().ToString(),
            ExpiresAt = timeProvider.GetUtcNow().UtcDateTime.AddDays(7)
        }, ct);

        // Outbox イベント
        await outboxRepository.AddAsync(new OutboxEvent
        {
            EventType = "auth.login.success",
            AggregateId = user.Id,
            Payload = JsonSerializer.Serialize(new UserAuthenticatedEvent(
                Guid.NewGuid().ToString(), user.Id, session.SessionId,
                ipAddress, userAgent, "password", false,
                timeProvider.GetUtcNow().UtcDateTime))
        }, ct);

        await userRepository.SaveChangesAsync(ct);

        await securityService.LogSecurityEventAsync(user.Id, "LOGIN_SUCCESS",
            ipAddress, userAgent,
            new Dictionary<string, object> { ["authMethod"] = "password" }, ct);

        return new LoginResponse(
            accessToken, refreshToken, "Bearer", 3600,
            new UserDto(user.Id, user.FirstName ?? "", user.LastName ?? "", user.Role));
    }

    private static string GenerateSessionId()
    {
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    // RefreshTokenAsync, LogoutAsync, GetCurrentUserAsync の実装は省略（同パターン）
    // ※ RefreshTokenAsync では設計書 §29.10 の Replay Detection を実装:
    //   1. トークンが is_revoked=true の場合 → Replay Attack 判定 → family_id 全トークン無効化
    //   2. 有効トークンの場合 → 旧トークン失効 → 新トークン生成（family_id 継承）
    //   3. absolute_expiry 超過の場合 → ファミリー全トークン無効化 → 再ログイン要求
}
```

### 4.3b パスワード履歴チェック（設計書 §29.1）

`IPasswordService.ChangePasswordAsync` および `ConfirmResetAsync` 実行時に、以下のロジックを適用する:

1. `password_histories` テーブルから当該ユーザーの直近 5 件のハッシュを取得
2. 新パスワードを各ハッシュと照合（`IPasswordHasher<T>.VerifyHashedPassword`）
3. いずれかと一致した場合、`BusinessException("直近5回分のパスワードは使用できません")` をスロー
4. パスワード変更成功時に、旧パスワードハッシュを `password_histories` に記録

```csharp
// PasswordService 内のパスワード履歴チェック（概要）
private async Task ValidatePasswordHistoryAsync(
    string userId, string newPassword, CancellationToken ct)
{
    var recentHashes = await _passwordHistoryRepository.FindRecentByUserIdAsync(userId, 5, ct);
    foreach (var history in recentHashes)
    {
        if (_passwordHasher.VerifyHashedPassword(null!, history.PasswordHash, newPassword)
            != PasswordVerificationResult.Failed)
            throw new BusinessException("直近5回分のパスワードは使用できません");
    }
}
```

### 4.3 Argon2 パスワードハッシャー

```csharp
public class Argon2PasswordHasher : IPasswordHasher<User>
{
    public string HashPassword(User user, string password)
    {
        // Argon2id でハッシュ化（ASP.NET Core Identity 互換形式）
        // 実装: Konscious.Security.Cryptography.Argon2id または同等ライブラリ
        throw new NotImplementedException("Phase 4 で実装");
    }

    public PasswordVerificationResult VerifyHashedPassword(
        User user, string hashedPassword, string providedPassword)
    {
        // ハッシュ検証 + 必要に応じてリハッシュ
        throw new NotImplementedException("Phase 4 で実装");
    }
}
```

### 4.4 設定クラス（IOptions<T>）

```csharp
public record JwtSettings(
    string Issuer,
    string Audience,
    int AccessExpirationSeconds = 3600,
    int RefreshExpirationSeconds = 604800,
    int MaxActiveRefreshTokens = 10);

public record AuthSettings(
    int MaxFailedAttempts = 5,
    int AutoUnlockMinutes = 30,
    int FailedAttemptResetMinutes = 15);

public record SessionSettings(
    int Timeout = 1800,
    int MaxConcurrentSessions = 5);
```

### 4.5 Program.cs 更新（Service DI 登録）

```csharp
// IOptions<T> 登録
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

// Service 登録（Scoped）
builder.Services.AddScoped<IAuthService, AuthServiceImpl>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IMfaService, MfaService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<ISecurityService, SecurityService>();
builder.Services.AddScoped<IOAuthService, OAuthService>();
builder.Services.AddScoped<ITotpService, TotpService>();
builder.Services.AddScoped<IUserRegistrationService, UserRegistrationService>();
builder.Services.AddScoped<IClientCredentialsService, ClientCredentialsService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

// Argon2 パスワードハッシャー
builder.Services.AddScoped<IPasswordHasher<User>, Argon2PasswordHasher>();
```

### Phase 4 完了チェックリスト

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | `dotnet build` | 警告なし成功 |
| 2 | 全 10 Service インターフェース定義 | 設計書 §13 / §22 / §29.5 / §29.9 準拠（IClientCredentialsService, IAuditLogService 含む） |
| 3 | 全 10 Service 実装 | ビジネスロジック実装 |
| 4 | 全 async メソッドに `CancellationToken ct = default` | パラメータ含む |
| 5 | primary constructor による DI | 全 Service |
| 6 | `ILogger<T>` メッセージテンプレート | 文字列補間禁止 |
| 7 | Argon2id パスワードハッシャー | `IPasswordHasher<User>` 実装 |
| 8 | IOptions<T> パターン | JwtSettings, AuthSettings, SessionSettings |
| 9 | ValidateOnStart() | 起動時設定バリデーション |
| 10 | アカウントロックアウト | 5 回失敗でロック（設計書 §27） |
| 11 | セッション管理 | 同時 5 セッション上限（設計書 §27） |
| 12 | `Console.WriteLine` なし | `ILogger<T>` のみ |
| 13 | 秘密情報ハードコードなし | 環境変数 / `dotnet user-secrets` |
| 14 | パスワード履歴チェック | 直近 5 回分の重複不可（設計書 §29.1） |
| 15 | RefreshToken Replay Detection | family_id ベースのトークンローテーション + 再利用検出（設計書 §29.10） |
| 16 | Client Credentials Service | M2M トークン発行（設計書 §29.5） |
| 17 | メール検証 | `IUserRegistrationService` に `VerifyEmailAsync`, `ResendVerificationEmailAsync` 含む（設計書 §29.2） |
| 14 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 15 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 16 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## Phase 5: Endpoints 実装

### 目的

設計書 §API 設計 および §23 に基づき、全 Minimal API エンドポイントを実装する。Endpoints → Services の依存方向を厳守し、ビジネスロジックは Service 層に委譲する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `AuthService/Endpoints/AuthEndpoints.cs` | 作成 | ログイン・ログアウト・リフレッシュ・トークン検証・ユーザー情報取得 |
| 2 | `AuthService/Endpoints/MfaEndpoints.cs` | 作成 | MFA セットアップ・検証・無効化 |
| 3 | `AuthService/Endpoints/PasswordEndpoints.cs` | 作成 | パスワードリセット要求・確認・変更 |
| 4 | `AuthService/Endpoints/UserRegistrationEndpoints.cs` | 作成 | ユーザー登録・論理削除・物理削除 |
| 5 | `AuthService/Endpoints/OAuthEndpoints.cs` | 作成 | OAuth2 フロー・アカウントリンク |
| 6 | `AuthService/Endpoints/EmailVerificationEndpoints.cs` | 作成 | メール検証・再送信（設計書 §29.2） |
| 7 | `AuthService/Endpoints/TokenEndpoints.cs` | 作成 | M2M Client Credentials トークン発行（設計書 §29.5） |
| 8 | `AuthService/DTOs/Requests/*.cs` | 作成 | 全リクエスト DTO（record 型 + Data Annotations） |
| 9 | `AuthService/DTOs/Responses/*.cs` | 作成 | 全レスポンス DTO（record 型） |
| 10 | `AuthService/Validators/*.cs` | 作成 | 全 FluentValidation バリデーター |

### 5.1 エンドポイント一覧（設計書 §API 設計 完全準拠）

#### 認証 API（AuthEndpoints）

| メソッド | パス | 認可 | 名前 |
|---------|-----|------|------|
| POST | `/api/v1/auth/login` | AllowAnonymous | Login |
| POST | `/api/v1/auth/refresh` | AllowAnonymous | RefreshToken |
| POST | `/api/v1/auth/logout` | RequireAuthorization | Logout |
| POST | `/api/v1/auth/validate` | RequireAuthorization | ValidateToken |
| GET | `/api/v1/auth/me` | RequireAuthorization | GetCurrentUser |

#### MFA API（MfaEndpoints）

| メソッド | パス | 認可 | 名前 |
|---------|-----|------|------|
| POST | `/api/v1/auth/mfa/verify` | AllowAnonymous | VerifyMfa |
| POST | `/api/v1/auth/mfa/setup` | RequireAuthorization | SetupMfa |
| DELETE | `/api/v1/auth/mfa/disable` | RequireAuthorization | DisableMfa |

#### パスワード管理 API（PasswordEndpoints）

| メソッド | パス | 認可 | 名前 |
|---------|-----|------|------|
| POST | `/api/v1/auth/password/reset` | AllowAnonymous | RequestPasswordReset |
| POST | `/api/v1/auth/password/confirm` | AllowAnonymous | ConfirmPasswordReset |
| PUT | `/api/v1/auth/password/change` | RequireAuthorization | ChangePassword |

#### ユーザー登録 API（UserRegistrationEndpoints）

| メソッド | パス | 認可 | 名前 |
|---------|-----|------|------|
| POST | `/api/v1/auth/users` | AllowAnonymous | RegisterUser |
| DELETE | `/api/v1/auth/users/{userId}` | RequireAuthorization | SoftDeleteUser |
| DELETE | `/api/v1/auth/users/{userId}/hard` | RequireAuthorization("AdminOnly") | HardDeleteUser |

#### OAuth2 API（OAuthEndpoints）

| メソッド | パス | 認可 | 名前 |
|---------|-----|------|------|
| GET | `/api/v1/auth/oauth2/authorization/{provider}` | AllowAnonymous | StartOAuthFlow |
| GET | `/api/v1/auth/oauth2/callback/{provider}` | AllowAnonymous | OAuthCallback |
| POST | `/api/v1/auth/oauth2/link/{provider}` | RequireAuthorization | LinkOAuthAccount |
| DELETE | `/api/v1/auth/oauth2/link/{provider}` | RequireAuthorization | UnlinkOAuthAccount |
| GET | `/api/v1/auth/oauth2/accounts` | RequireAuthorization | GetLinkedOAuthAccounts |

#### メール検証 API（EmailVerificationEndpoints、設計書 §29.2）

| メソッド | パス | 認可 | 名前 |
|---------|-----|------|------|
| POST | `/api/v1/auth/email/verify` | AllowAnonymous | VerifyEmail |
| POST | `/api/v1/auth/email/resend` | RequireAuthorization | ResendVerification |

#### Client Credentials API（TokenEndpoints、設計書 §29.5）

| メソッド | パス | 認可 | 名前 |
|---------|-----|------|------|
| POST | `/api/v1/auth/token` | AllowAnonymous | IssueToken |

#### 監視エンドポイント

| メソッド | パス | 認可 | 名前 |
|---------|-----|------|------|
| GET | `/health` | AllowAnonymous | HealthLiveness |
| GET | `/health/ready` | AllowAnonymous | HealthReadiness |

### 5.2 AuthEndpoints 実装（設計書 §23 準拠）

```csharp
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Authentication")
            .WithOpenApi();

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .WithName("Login")
            .Produces<LoginResponse>(200)
            .ProducesValidationProblem()
            .Produces(401);

        group.MapPost("/refresh", RefreshTokenAsync)
            .AllowAnonymous()
            .WithName("RefreshToken")
            .Produces<TokenRefreshResponse>(200)
            .Produces(401);

        group.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .WithName("Logout")
            .Produces<MessageResponse>(204);

        group.MapPost("/validate", ValidateTokenAsync)
            .RequireAuthorization()
            .WithName("ValidateToken")
            .Produces<TokenValidationResponse>(200);

        group.MapGet("/me", GetCurrentUserAsync)
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .Produces<UserInfoResponse>(200);
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] LoginRequest request,
        IValidator<LoginRequest> validator,
        IAuthService authService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        return Results.Ok(await authService.LoginAsync(request, ipAddress, userAgent, ct));
    }

    private static async Task<IResult> RefreshTokenAsync(
        [FromBody] TokenRefreshRequest request,
        IValidator<TokenRefreshRequest> validator,
        IAuthService authService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        return Results.Ok(await authService.RefreshTokenAsync(request, ct));
    }

    private static async Task<IResult> LogoutAsync(
        ClaimsPrincipal user,
        IAuthService authService,
        CancellationToken ct)
    {
        var sessionId = user.FindFirstValue("session_id")
            ?? throw new UnauthorizedException();
        await authService.LogoutAsync(sessionId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ValidateTokenAsync(
        ClaimsPrincipal user,
        IJwtTokenService jwtTokenService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = user.FindFirstValue(ClaimTypes.Role);
        var expClaim = user.FindFirstValue("exp");
        DateTime? expiresAt = expClaim is not null
            ? DateTimeOffset.FromUnixTimeSeconds(long.Parse(expClaim)).UtcDateTime
            : null;

        return Results.Ok(new TokenValidationResponse(true, userId, role, expiresAt));
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ClaimsPrincipal user,
        IAuthService authService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        return Results.Ok(await authService.GetCurrentUserAsync(userId, ct));
    }
}
```

### 5.3 DTO 定義（record 型 + Data Annotations）

```csharp
// リクエスト DTO
public record LoginRequest(
    [Required, EmailAddress, StringLength(255)] string Email,
    [Required, StringLength(100, MinimumLength = 8)] string Password);

public record TokenRefreshRequest(
    [Required] string RefreshToken);

public record UserCreateRequest(
    [Required, EmailAddress, StringLength(255)] string Email,
    [Required, StringLength(100, MinimumLength = 3)] string Username,
    [Required, StringLength(100, MinimumLength = 8)] string Password,
    [StringLength(100)] string? FirstName,
    [StringLength(100)] string? LastName);

public record PasswordResetRequest(
    [Required, EmailAddress, StringLength(255)] string Email);

public record PasswordResetConfirmRequest(
    [Required] string Token,
    [Required, StringLength(100, MinimumLength = 8)] string NewPassword);

public record PasswordChangeRequest(
    [Required] string CurrentPassword,
    [Required, StringLength(100, MinimumLength = 8)] string NewPassword);

public record MfaVerificationRequest(
    [Required, StringLength(6, MinimumLength = 6)] string Code,
    [Required] string SessionToken);

// レスポンス DTO
public record LoginResponse(string AccessToken, string RefreshToken, string TokenType, int ExpiresIn, UserDto User);
public record TokenRefreshResponse(string AccessToken, string RefreshToken, int ExpiresIn);
public record UserDto(string Id, string FirstName, string LastName, string Role);
public record UserInfoResponse(string Id, string Email, string FirstName, string LastName, string Role, DateTime CreatedAt);
public record UserResponse(string Id, string Email, string Username, string? FirstName, string? LastName, string Status, string Role, DateTime CreatedAt);
public record MfaSetupResponse(string SecretKey, string QrCodeUri, IReadOnlyList<string> BackupCodes);
public record MessageResponse(string Message);
public record LogoutResponse(string Message, DateTime LoggedOutAt);
public record TokenValidationResponse(bool IsValid, string? UserId, string? Role, DateTime? ExpiresAt);
public record GraphUserResponse(string Id, string DisplayName, string? Mail, string? JobTitle, string? Department);
public record OAuthAccountDto(string Provider, string ProviderUserId, DateTime CreatedAt);

// メール検証 DTO（設計書 §29.2）
public record EmailVerificationRequest([Required] string Token);

// Client Credentials DTO（設計書 §29.5）
public record ClientCredentialsRequest(
    [Required, MaxLength(100)] string ClientId,
    [Required, MaxLength(255)] string ClientSecret,
    [Required] string Scope,
    [Required] string GrantType);
public record ClientCredentialsResponse(string AccessToken, string TokenType, int ExpiresIn, string Scope);
```

### 5.4 FluentValidation バリデーター（設計書 §20 準拠）

```csharp
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("メールアドレスは必須です")
            .EmailAddress().WithMessage("有効なメールアドレスを入力してください")
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("パスワードは必須です")
            .MinimumLength(8).WithMessage("パスワードは8文字以上必要です")
            .MaximumLength(100)
            .Matches(@"[A-Z]").WithMessage("パスワードには大文字を1文字以上含めてください")
            .Matches(@"[a-z]").WithMessage("パスワードには小文字を1文字以上含めてください")
            .Matches(@"\d").WithMessage("パスワードには数字を1文字以上含めてください")
            .Matches(@"[!@#$%^&*(),.?""{}|<>]").WithMessage("パスワードには特殊文字を1文字以上含めてください");
    }
}
```

> 残りのバリデーター（RegisterRequestValidator, MfaCodeValidator, PasswordResetRequestValidator, PasswordResetConfirmRequestValidator, PasswordChangeRequestValidator, TokenRefreshRequestValidator）は設計書 §20 に定義された通り実装する。

### 5.5 Program.cs 更新（エンドポイントマッピング + FluentValidation 登録）

```csharp
// FluentValidation 登録
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();

// エンドポイントマッピング（ミドルウェアパイプラインの最後）
app.MapAuthEndpoints();
app.MapMfaEndpoints();
app.MapPasswordEndpoints();
app.MapUserRegistrationEndpoints();
app.MapOAuthEndpoints();
app.MapEmailVerificationEndpoints();
app.MapTokenEndpoints();
```

### Phase 5 完了チェックリスト

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | `dotnet build` | 警告なし成功 |
| 2 | 全 24 エンドポイント定義 | 設計書 §API 設計テーブル完全網羅（EmailVerification + TokenEndpoints 含む） |
| 3 | 全エンドポイントに認可設定 | `.RequireAuthorization()` / `.AllowAnonymous()` 明示 |
| 4 | 全 POST/PUT に FluentValidation | `IValidator<T>` 注入 + `ValidateAsync` |
| 5 | 全リクエスト DTO | record 型 + Data Annotations |
| 6 | 全レスポンス DTO | record 型 |
| 7 | エンドポイントにビジネスロジックなし | Service 層に委譲 |
| 8 | `WithOpenApi()` 設定 | OpenAPI メタデータ |
| 9 | `WithName()` 設定 | 操作 ID |
| 10 | `CancellationToken ct` 伝搬 | 全 async エンドポイント |
| 11 | IDOR 防止 | ユーザーリソースのオーナーシップチェック |
| 12 | 管理者エンドポイント | `RequireAuthorization("AdminOnly")` |
| 13 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 14 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 15 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## Phase 6: Kafka イベント連携

### 目的

設計書 §イベント設計 および §14（Outbox パターン）に基づき、Kafka イベント発行・購読を実装する。全イベント発行は Outbox パターンで DB トランザクションとの原子性を保証する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `AuthService/Events/IAuthEvent.cs` | 作成 | イベント共通マーカーインターフェース |
| 2 | `AuthService/Events/UserRegisteredEvent.cs` | 作成 | ユーザー登録イベント |
| 3 | `AuthService/Events/UserAuthenticatedEvent.cs` | 作成 | ログイン成功イベント |
| 4 | `AuthService/Events/UserLoggedOutEvent.cs` | 作成 | ログアウトイベント |
| 5 | `AuthService/Events/LoginFailedEvent.cs` | 作成 | ログイン失敗イベント |
| 6 | `AuthService/Events/AccountLockedEvent.cs` | 作成 | アカウントロックイベント |
| 7 | `AuthService/Events/AccountUnlockedEvent.cs` | 作成 | アカウントロック解除イベント |
| 8 | `AuthService/Events/PasswordChangedEvent.cs` | 作成 | パスワード変更イベント |
| 9 | `AuthService/Events/MfaEnabledEvent.cs` | 作成 | MFA 有効化イベント |
| 10 | `AuthService/Events/MfaDisabledEvent.cs` | 作成 | MFA 無効化イベント |
| 11 | `AuthService/Events/SecurityIncidentEvent.cs` | 作成 | セキュリティインシデントイベント |
| 12 | `AuthService/Events/PasswordResetRequestedEvent.cs` | 作成 | パスワードリセット要求イベント |
| 13 | `AuthService/Events/TokenRevokedEvent.cs` | 作成 | トークン失効イベント |
| 14 | `AuthService/Infrastructure/BackgroundServices/OutboxPublisher.cs` | 作成 | Outbox ポーリング + Kafka 発行 |
| 15 | `AuthService/Infrastructure/BackgroundServices/UserDeletedConsumer.cs` | 作成 | UserDeleted イベント購読（DSR 対応、設計書 §29.3） |
| 16 | `AuthService/Infrastructure/BackgroundServices/PermissionsUpdatedConsumer.cs` | 作成 | PermissionsUpdated イベント購読 |
| 17 | `AuthService/Infrastructure/BackgroundServices/TokenCleanupService.cs` | 作成 | 期限切れトークンの定期削除（設計書 §26） |
| 18 | `AuthService/Infrastructure/BackgroundServices/FailedAttemptResetService.cs` | 作成 | 失敗回数の自動リセット・アカウント自動ロック解除（設計書 §26） |
| 19 | `AuthService/Infrastructure/BackgroundServices/SessionTimeoutService.cs` | 作成 | アイドルタイムアウト超過セッションの無効化（設計書 §26） |
| 20 | `AuthService/Infrastructure/BackgroundServices/SecurityLogAnonymizationService.cs` | 作成 | security_logs の PII 仮名化・物理削除（設計書 §29.4） |

### 6.1 イベント定義（設計書 §25 準拠）

```csharp
public interface IAuthEvent
{
    string EventId { get; }
    DateTime OccurredAt { get; }
}

public record UserRegisteredEvent(
    string EventId, string UserId, string Role,
    DateTime RegisteredAt, DateTime OccurredAt) : IAuthEvent;

public record UserAuthenticatedEvent(
    string EventId, string UserId, string SessionId,
    string? IpAddress, string? UserAgent, string AuthMethod,
    bool MfaUsed, DateTime OccurredAt) : IAuthEvent;

public record LoginFailedEvent(
    string EventId, string? UserId, string? IpAddress,
    string FailureReason, int FailedAttemptCount, DateTime OccurredAt) : IAuthEvent;

public record AccountLockedEvent(
    string EventId, string UserId, string LockReason,
    int FailedAttemptCount, DateTime LockedAt, DateTime OccurredAt) : IAuthEvent;

// 残りのイベント record は設計書 §25 に定義された通り実装する
```

> **PII 最小化原則**: イベントペイロードにメールアドレス・氏名を含めない。`userId` のみで識別。

### 6.2 発行イベント — トピック対応表

| イベント | Kafka トピック |
|---------|-------------|
| UserRegisteredEvent | `user.registered` |
| UserAuthenticatedEvent | `auth.login.success` |
| UserLoggedOutEvent | `auth.session.ended` |
| LoginFailedEvent | `auth.login.failed` |
| AccountLockedEvent | `auth.security.incident` |
| AccountUnlockedEvent | `auth.security.incident` |
| PasswordChangedEvent | `auth.security.incident` |
| MfaEnabledEvent | `auth.security.incident` |
| MfaDisabledEvent | `auth.security.incident` |
| SecurityIncidentEvent | `auth.security.incident` |
| PasswordResetRequestedEvent | `auth.security.incident` |
| TokenRevokedEvent | `auth.security.incident` |

### 6.3 購読イベント

| イベント | 発行元 | Kafka トピック | アクション |
|---------|--------|-------------|----------|
| UserDeleted | UserManagementService | `user.deleted` | ユーザーアカウント無効化、全セッション終了 |
| PermissionsUpdated | UserManagementService | `user.permissions.updated` | ロール更新、既存トークン無効化 |

### 6.4 OutboxPublisher（BackgroundService、設計書 §14 準拠）

```csharp
public class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    TimeProvider timeProvider,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private static readonly TimeSpan MinPollingInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MaxPollingInterval = TimeSpan.FromSeconds(5);
    private TimeSpan _currentInterval = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var pendingEvents = await context.OutboxEvents
                .Where(e => e.Status == "PENDING")
                .OrderBy(e => e.CreatedAt)
                .Take(100)
                .ToListAsync(stoppingToken);

            foreach (var evt in pendingEvents)
            {
                try
                {
                    await producer.ProduceAsync(evt.EventType,
                        new Message<string, string>
                        {
                            Key = evt.AggregateId,
                            Value = evt.Payload
                        }, stoppingToken);
                    evt.Status = "PUBLISHED";
                    evt.PublishedAt = timeProvider.GetUtcNow().UtcDateTime;
                }
                catch (Exception ex)
                {
                    evt.RetryCount++;
                    evt.LastError = ex.Message[..Math.Min(ex.Message.Length, 2000)];
                    if (evt.RetryCount >= evt.MaxRetries)
                        evt.Status = "FAILED";
                    logger.LogError(ex, "Outbox publish failed: {EventId}, retry: {RetryCount}",
                        evt.Id, evt.RetryCount);
                }
            }
            await context.SaveChangesAsync(stoppingToken);

            // 動的バックオフ（100ms〜5s）
            _currentInterval = pendingEvents.Count > 0
                ? MinPollingInterval
                : TimeSpan.FromTicks(Math.Min(
                    _currentInterval.Ticks * 2, MaxPollingInterval.Ticks));

            await Task.Delay(_currentInterval, stoppingToken);
        }
    }
}
```

### 6.5 Program.cs 更新（Kafka 設定）

```csharp
// Kafka Producer 登録
builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var config = new ProducerConfig
    {
        BootstrapServers = builder.Configuration["Kafka:BootstrapServers"],
        Acks = Acks.All,
        EnableIdempotence = true
    };
    return new ProducerBuilder<string, string>(config).Build();
});

// BackgroundService 登録
builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddHostedService<UserDeletedConsumer>();
builder.Services.AddHostedService<PermissionsUpdatedConsumer>();
builder.Services.AddHostedService<TokenCleanupService>();
builder.Services.AddHostedService<FailedAttemptResetService>();
builder.Services.AddHostedService<SessionTimeoutService>();
builder.Services.AddHostedService<SecurityLogAnonymizationService>();
```

### 6.6 UserDeleted Consumer — DSR 対応（設計書 §29.3）

`user.deleted` トピックを購読し、GDPR/DSR に基づくデータ削除・仮名化を実行:

```csharp
// UserDeletedConsumer は BackgroundService として実装
// 処理内容:
// 1. oauth_accounts, refresh_tokens, user_sessions, user_roles,
//    user_mfa, password_resets, password_histories を物理削除
// 2. security_logs の仮名化（user_id → NULL, ip_address → SHA-256 ハッシュ化）
// 3. users レコードの物理削除
// 全処理をトランザクション内で実行
```

### 6.7 TokenCleanupService（設計書 §26）

期限切れのリフレッシュトークンとパスワードリセットトークンを定期的に削除する BackgroundService:

- **実行間隔**: 1 時間
- **対象**: 7 日以上前に失効したリフレッシュトークン、使用済み/期限切れのパスワードリセットトークン
- **パターン**: `IServiceScopeFactory` で Scoped サービス取得、`stoppingToken` 伝搬

### 6.8 FailedAttemptResetService（設計書 §26）

ロック解除条件を満たしたアカウントの失敗回数を自動リセットする BackgroundService:

- **実行間隔**: 5 分
- **ロック解除**: ロック後 30 分経過したアカウントを自動解除
- **失敗回数リセット**: 非ロック・最終更新から 15 分経過したアカウントの `FailedLoginAttempts` を 0 にリセット

### 6.9 SessionTimeoutService（設計書 §26）

アイドルタイムアウトを超過したセッションを無効化する BackgroundService:

- **実行間隔**: 5 分
- **対象**: `ExpiresAt < now` のアクティブセッションを `IsActive = false` に更新

### 6.10 SecurityLogAnonymizationService（設計書 §29.4）

security_logs の PII 保持期間ポリシーを自動適用する BackgroundService:

- **実行間隔**: 6 時間
- **90 日経過**: `ip_address` を SHA-256 ハッシュ化（仮名化）
- **1 年超過**: レコードを物理削除
- **バッチサイズ**: 1000 件ずつ処理

### Phase 6 完了チェックリスト

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | `dotnet build` | 警告なし成功 |
| 2 | 全 12 発行イベント record 定義 | `IAuthEvent` 実装 |
| 3 | PII 最小化 | イベントペイロードにメール・氏名なし |
| 4 | OutboxPublisher | 動的バックオフ（100ms〜5s） |
| 5 | OutboxPublisher | `IServiceScopeFactory` で Scoped サービス取得 |
| 6 | OutboxPublisher | `stoppingToken` 全下位呼び出しに伝搬 |
| 7 | Kafka Producer | `EnableIdempotence = true`, `Acks = All` |
| 8 | 購読 Consumer | UserDeleted（DSR 対応、設計書 §29.3）, PermissionsUpdated の BackgroundService |
| 9 | Outbox ステータス | `PENDING` → `PUBLISHED` / `FAILED` / `DEAD_LETTER` |
| 10 | `catch (Exception)` | ログ出力 + リトライ（握りつぶしなし） |
| 11 | TokenCleanupService | 1 時間間隔、期限切れトークン削除（設計書 §26） |
| 12 | FailedAttemptResetService | 5 分間隔、30 分後自動ロック解除 + 15 分後失敗回数リセット（設計書 §26） |
| 13 | SessionTimeoutService | 5 分間隔、アイドルタイムアウト超過セッション無効化（設計書 §26） |
| 14 | SecurityLogAnonymizationService | 6 時間間隔、90 日→仮名化、1 年→物理削除（設計書 §29.4） |
| 15 | 全 BackgroundService | `IServiceScopeFactory` + `stoppingToken` 伝搬 + `TimeProvider` 使用 |
| 11 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 12 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 13 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## Phase 7: Redis キャッシュ連携

### 目的

設計書 §9（キャッシュ戦略）に基づき、Redis キャッシュをセッション管理、トークンブラックリスト、レート制限カウンタ、ログイン失敗回数管理に統合する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `AuthService/Infrastructure/Cache/IRedisCacheService.cs` | 作成 | Redis キャッシュ抽象インターフェース |
| 2 | `AuthService/Infrastructure/Cache/RedisCacheService.cs` | 作成 | StackExchange.Redis 実装 |
| 3 | `AuthService/Infrastructure/Cache/TokenBlacklistService.cs` | 作成 | JWT ブラックリスト管理 |
| 4 | `AuthService/Configurations/RedisSettings.cs` | 作成 | Redis 設定（IOptions<T>） |
| 5 | `AuthService/Program.cs` | 更新 | Redis 接続登録 |

### 7.1 Redis キャッシュキー設計（設計書 §9 準拠）

| キー | TTL | 用途 |
|------|-----|------|
| `session:{sessionId}` | 30 分 | ユーザーセッション |
| `rate:{ipAddress}:{endpoint}` | 1 分 | レート制限カウンタ |
| `attempts:{email}` | 15 分 | ログイン失敗回数 |
| `blacklist:{tokenId}` | トークンの残存有効期限 | JWT ブラックリスト |
| `mfa_session:{sessionToken}` | 5 分 | MFA 検証待ちセッション |

### 7.2 Redis キャッシュサービス

```csharp
public interface IRedisCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task<long> IncrementAsync(string key, TimeSpan? expiration = null, CancellationToken ct = default);
}

public class RedisCacheService(
    IConnectionMultiplexer redis,
    ILogger<RedisCacheService> logger) : IRedisCacheService
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var value = await _db.StringGetAsync(key);
        if (value.IsNullOrEmpty) return default;
        return JsonSerializer.Deserialize<T>(value!);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(value);
        await _db.StringSetAsync(key, json, expiration);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
        => await _db.KeyDeleteAsync(key);

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => await _db.KeyExistsAsync(key);

    public async Task<long> IncrementAsync(string key, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        var result = await _db.StringIncrementAsync(key);
        if (expiration.HasValue && result == 1)
            await _db.KeyExpireAsync(key, expiration.Value);
        return result;
    }
}
```

### 7.3 トークンブラックリスト

```csharp
public class TokenBlacklistService(
    IRedisCacheService cache,
    ILogger<TokenBlacklistService> logger)
{
    public async Task BlacklistTokenAsync(string jti, TimeSpan remaining, CancellationToken ct = default)
    {
        await cache.SetAsync($"blacklist:{jti}", true, remaining, ct);
        logger.LogInformation("Token blacklisted: {Jti}", jti);
    }

    public async Task<bool> IsBlacklistedAsync(string jti, CancellationToken ct = default)
        => await cache.ExistsAsync($"blacklist:{jti}", ct);
}
```

### 7.4 Program.cs 更新（Redis 登録）

```csharp
// Redis 接続
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"]
        ?? throw new InvalidOperationException("Redis 接続文字列が設定されていません")));

builder.Services.AddScoped<IRedisCacheService, RedisCacheService>();
builder.Services.AddScoped<TokenBlacklistService>();
```

### Phase 7 完了チェックリスト

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | `dotnet build` | 警告なし成功 |
| 2 | Redis 接続 | `IConnectionMultiplexer` Singleton 登録 |
| 3 | キャッシュキー設計 | 設計書 §9 準拠（5 キーパターン） |
| 4 | トークンブラックリスト | ログアウト時にブラックリスト登録 |
| 5 | MFA セッションキャッシュ | 5 分 TTL |
| 6 | ログイン失敗回数 | Redis + DB 併用（Redis は 15 分 TTL） |
| 7 | 秘密情報 | Redis 接続文字列は環境変数 / `dotnet user-secrets` |
| 8 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 9 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 10 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## Phase 8: 認証・認可・セキュリティ

### 目的

設計書 §7（セキュリティ機能）および §24 に基づき、JWT Bearer 認証、ロールベース認可、セキュリティヘッダー、CORS、レート制限、グローバル例外ハンドラーを構成する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `AuthService/Infrastructure/Middleware/SecurityHeadersMiddleware.cs` | 作成 | セキュリティヘッダーミドルウェア |
| 2 | `AuthService/Infrastructure/Middleware/SecurityHeadersMiddlewareExtensions.cs` | 作成 | `UseSecurityHeaders()` 拡張メソッド |
| 3 | `AuthService/Infrastructure/Middleware/CorrelationIdMiddleware.cs` | 作成 | Correlation ID ミドルウェア |
| 4 | `AuthService/Infrastructure/Middleware/CorrelationIdMiddlewareExtensions.cs` | 作成 | `UseCorrelationId()` 拡張メソッド |
| 5 | `AuthService/Program.cs` | 更新 | 認証・認可・ミドルウェアパイプライン統合 |

### 8.1 JWT Bearer 認証設定

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });
```

### 8.2 認可ポリシー（設計書 §24 準拠）

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("ADMIN"));
    options.AddPolicy("ManagerOrAdmin", p => p.RequireRole("ADMIN", "MANAGER"));
    options.AddPolicy("StaffOrAbove", p => p.RequireRole("ADMIN", "MANAGER", "STAFF"));
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

### 8.3 レート制限（設計書 §15 準拠）

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddSlidingWindowLimiter("general", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.SegmentsPerWindow = 6;
    });
    options.AddFixedWindowLimiter("login", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
    });
    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.Headers.Append("Retry-After", "60");
        await TypedResults.Problem(
            "レート制限を超過しました。しばらく待ってからリトライしてください。",
            statusCode: 429).ExecuteAsync(context.HttpContext);
    };
});
```

### 8.4 グローバル例外ハンドラー（設計書 §24 / AGENTS.md §4.7 準拠）

```csharp
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var error = feature?.Error;

        if (error is not (NotFoundException or BusinessException
            or UnauthorizedException or ForbiddenException))
            logger.LogError(error, "Unhandled exception: {Message}", error?.Message);
        else
            logger.LogWarning("Handled exception: {ExceptionType} - {Message}",
                error.GetType().Name, error.Message);

        var problem = error switch
        {
            NotFoundException e       => TypedResults.Problem(e.Message, statusCode: 404),
            BusinessException e       => TypedResults.Problem(e.Message, statusCode: 422),
            UnauthorizedException     => TypedResults.Problem(statusCode: 401),
            ForbiddenException        => TypedResults.Problem(statusCode: 403),
            ConcurrencyException e    => TypedResults.Problem(e.Message, statusCode: 409),
            AccountLockedException e  => TypedResults.Problem(e.Message, statusCode: 423),
            MfaRequiredException mfa  => TypedResults.Json(
                new { requiresMfa = true, sessionToken = mfa.SessionToken },
                statusCode: 202),
            _                         => TypedResults.Problem("内部エラーが発生しました", statusCode: 500)
        };
        await problem.ExecuteAsync(context);
    });
});
```

### 8.5 ミドルウェアパイプライン順序（AGENTS.md §11.3 厳守）

```
┌─────────────────────────────────────────────────┐
│ 1. UseExceptionHandler()                         │ ← 最外層: 全例外をキャッチ
│ 2. UseHsts() + UseHttpsRedirection()             │ ← セキュリティ
│ 2.5 UseSecurityHeaders()                         │ ← セキュリティヘッダー
│ 3. UseCorrelationId()                            │ ← Correlation ID
│ 4. UseSerilogRequestLogging()                    │ ← リクエストログ
│ 5. UseCors()                                     │ ← CORS（認証より前）
│ 6. UseAuthentication()                           │ ← 認証
│    UseAuthorization()                            │ ← 認可（認証の直後）
│ 7. UseRateLimiter()                              │ ← レート制限（認証後）
│ 8. MapAuthEndpoints()                            │ ← エンドポイント
│    MapMfaEndpoints()                             │
│    MapPasswordEndpoints()                        │
│    MapUserRegistrationEndpoints()                │
│    MapOAuthEndpoints()                           │
│    MapEmailVerificationEndpoints()               │ ← 設計書 §29.2
│    MapTokenEndpoints()                           │ ← 設計書 §29.5
│    MapHealthChecks("/health")                    │
│    MapHealthChecks("/health/ready")              │
└─────────────────────────────────────────────────┘
```

### 8.6 セキュリティヘッダー

| ヘッダー | 値 |
|---------|---|
| `X-Content-Type-Options` | `nosniff` |
| `X-Frame-Options` | `DENY` |
| `Content-Security-Policy` | `default-src 'self'` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` |

### 8.7 CORS 設定（設計書 §24 準拠）

```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
```

> **禁止**: `AllowAnyOrigin()` は絶対に使用しない。許可オリジンは設定ファイルで明示指定する。

### Phase 8 完了チェックリスト

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | `dotnet build` | 警告なし成功 |
| 2 | JWT 認証 | `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey` = true |
| 3 | JWT 認証 | `ClockSkew = TimeSpan.FromMinutes(5)` |
| 4 | 認可 | FallbackPolicy に `RequireAuthenticatedUser()` |
| 5 | 認可ポリシー | `AdminOnly`, `ManagerOrAdmin`, `StaffOrAbove` |
| 6 | レート制限 | ログインエンドポイント 5 req/min/IP |
| 7 | レート制限 | 429 + `Retry-After` ヘッダー |
| 8 | グローバル例外ハンドラー | RFC 9457 Problem Details 形式 |
| 9 | MfaRequiredException | HTTP 202 + `sessionToken` |
| 10 | AccountLockedException | HTTP 423 Locked |
| 11 | セキュリティヘッダー | 全 5 ヘッダー設定 |
| 12 | CORS | `AllowAnyOrigin()` 不使用 |
| 13 | ミドルウェア順序 | AGENTS.md §11.3 厳守 |
| 14 | `UseAuthentication()` → `UseAuthorization()` | この順序 |
| 15 | `UseExceptionHandler()` | パイプライン最上位 |
| 16 | `DetailedErrors: false` | スタックトレース非公開 |
| 17 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 18 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 19 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## Phase 9: テスト

### 目的

設計書 §16（テスト方針）および `.github/instructions/test-standards.instructions.md` に基づき、単体テスト・統合テスト・セキュリティテストを実装する。カバレッジ 80% 以上を達成する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `AuthService.Tests/AuthService.Tests.csproj` | 作成 | テストプロジェクト |
| 2 | `AuthService.Tests/Services/AuthServiceTests.cs` | 作成 | AuthService 単体テスト |
| 3 | `AuthService.Tests/Services/JwtTokenServiceTests.cs` | 作成 | JWT トークンサービステスト |
| 4 | `AuthService.Tests/Services/UserRegistrationServiceTests.cs` | 作成 | ユーザー登録テスト |
| 5 | `AuthService.Tests/Services/PasswordServiceTests.cs` | 作成 | パスワードサービステスト |
| 6 | `AuthService.Tests/Services/MfaServiceTests.cs` | 作成 | MFA サービステスト |
| 7 | `AuthService.Tests/Services/SessionServiceTests.cs` | 作成 | セッション管理テスト |
| 8 | `AuthService.Tests/Validators/LoginRequestValidatorTests.cs` | 作成 | バリデーターテスト |
| 9 | `AuthService.Tests/Validators/UserCreateRequestValidatorTests.cs` | 作成 | 登録バリデーターテスト |
| 10 | `AuthService.Tests/Repositories/UserRepositoryTests.cs` | 作成 | DB スライステスト（Testcontainers） |
| 11 | `AuthService.Tests/Endpoints/AuthEndpointsTests.cs` | 作成 | 統合テスト（WebApplicationFactory） |
| 12 | `AuthService.Tests/Endpoints/SecurityTests.cs` | 作成 | セキュリティテスト |
| 13 | `AuthService.Tests/Services/ClientCredentialsServiceTests.cs` | 作成 | M2M トークン発行テスト（設計書 §29.5） |
| 14 | `AuthService.Tests/Services/PasswordHistoryTests.cs` | 作成 | パスワード履歴チェックテスト（設計書 §29.1） |
| 15 | `AuthService.Tests/Services/RefreshTokenReplayDetectionTests.cs` | 作成 | Replay Detection テスト（設計書 §29.10） |
| 16 | `AuthService.Tests/BackgroundServices/TokenCleanupServiceTests.cs` | 作成 | BackgroundService テスト（設計書 §26） |
| 17 | `AuthService.Tests/BackgroundServices/FailedAttemptResetServiceTests.cs` | 作成 | BackgroundService テスト（設計書 §26） |
| 18 | `AuthService.Tests/Helpers/TestAuthHandler.cs` | 作成 | テスト用認証ハンドラー |
| 19 | `AuthService.Tests/Helpers/TestWebApplicationFactory.cs` | 作成 | テスト用 WebApplicationFactory |

### 9.1 テストプロジェクト構成

```
AuthService.Tests/
├── AuthService.Tests.csproj
├── Services/
│   ├── AuthServiceTests.cs
│   ├── JwtTokenServiceTests.cs
│   ├── UserRegistrationServiceTests.cs
│   ├── PasswordServiceTests.cs
│   ├── MfaServiceTests.cs
│   ├── SessionServiceTests.cs
│   ├── ClientCredentialsServiceTests.cs     ← 設計書 §29.5
│   ├── PasswordHistoryTests.cs              ← 設計書 §29.1
│   └── RefreshTokenReplayDetectionTests.cs  ← 設計書 §29.10
├── BackgroundServices/
│   ├── TokenCleanupServiceTests.cs          ← 設計書 §26
│   └── FailedAttemptResetServiceTests.cs    ← 設計書 §26
├── Validators/
│   ├── LoginRequestValidatorTests.cs
│   └── UserCreateRequestValidatorTests.cs
├── Repositories/
│   └── UserRepositoryTests.cs
├── Endpoints/
│   ├── AuthEndpointsTests.cs
│   └── SecurityTests.cs
└── Helpers/
    ├── TestAuthHandler.cs
    └── TestWebApplicationFactory.cs
```

### 9.2 テストプロジェクト .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" PrivateAssets="all" />
    <PackageReference Include="NSubstitute" Version="5.*" />
    <PackageReference Include="Shouldly" Version="4.*" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.*" />
    <PackageReference Include="Testcontainers.PostgreSql" Version="4.*" />
    <PackageReference Include="coverlet.collector" Version="6.*" PrivateAssets="all" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\AuthService\AuthService.csproj" />
  </ItemGroup>
</Project>
```

### 9.3 AuthService 単体テスト（Should_X_When_Y パターン）

```csharp
public class AuthServiceTests
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly ISecurityLogRepository _securityLogRepository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IRedisCacheService _cacheService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuthService> _logger;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
        _sessionRepository = Substitute.For<ISessionRepository>();
        _securityLogRepository = Substitute.For<ISecurityLogRepository>();
        _outboxRepository = Substitute.For<IOutboxRepository>();
        _jwtTokenService = Substitute.For<IJwtTokenService>();
        _passwordHasher = Substitute.For<IPasswordHasher<User>>();
        _cacheService = Substitute.For<IRedisCacheService>();
        _timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-01T12:00:00Z"));
        _logger = Substitute.For<ILogger<AuthService>>();
        _authService = new AuthService(
            _userRepository, _refreshTokenRepository, _sessionRepository,
            _securityLogRepository, _outboxRepository,
            _jwtTokenService, _passwordHasher, _cacheService,
            _timeProvider, _logger);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnLoginResponse_When_ValidCredentials()
    {
        // Arrange
        var user = CreateTestUser();
        _userRepository.FindByEmailAsync("test@example.com", default)
            .Returns(user);
        _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, "Password1!")
            .Returns(PasswordVerificationResult.Success);
        _sessionRepository.CountActiveByUserIdAsync(user.Id, default).Returns(0);
        _jwtTokenService.GenerateAccessToken(Arg.Any<User>(), Arg.Any<string>())
            .Returns("access-token");
        _jwtTokenService.GenerateRefreshToken()
            .Returns(("refresh-token", "refresh-hash"));

        // Act
        var result = await _authService.LoginAsync(
            new LoginRequest("test@example.com", "Password1!"),
            "127.0.0.1", "TestAgent");

        // Assert
        result.ShouldNotBeNull();
        result.AccessToken.ShouldBe("access-token");
        result.RefreshToken.ShouldBe("refresh-token");
        result.User.Email.ShouldNotBeNull();
        await _userRepository.Received(1).FindByEmailAsync("test@example.com", default);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowUnauthorizedException_When_UserNotFound()
    {
        // Arrange
        _userRepository.FindByEmailAsync("notfound@example.com", default)
            .Returns((User?)null);

        // Act & Assert
        var act = async () => await _authService.LoginAsync(
            new LoginRequest("notfound@example.com", "Password1!"),
            "127.0.0.1", "TestAgent");
        var ex = await Should.ThrowAsync<UnauthorizedException>(act);
        ex.Message.ShouldContain("認証情報が無効です");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowUnauthorizedException_When_InvalidPassword()
    {
        // Arrange
        var user = CreateTestUser();
        _userRepository.FindByEmailAsync("test@example.com", default)
            .Returns(user);
        _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, "WrongPass1!")
            .Returns(PasswordVerificationResult.Failed);

        // Act & Assert
        var act = async () => await _authService.LoginAsync(
            new LoginRequest("test@example.com", "WrongPass1!"),
            "127.0.0.1", "TestAgent");
        await Should.ThrowAsync<UnauthorizedException>(act);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowAccountLockedException_When_AccountIsLocked()
    {
        // Arrange
        var user = CreateTestUser();
        user.IsLocked = true;
        user.LockoutEnd = DateTime.UtcNow.AddMinutes(30);
        _userRepository.FindByEmailAsync("test@example.com", default)
            .Returns(user);

        // Act & Assert
        var act = async () => await _authService.LoginAsync(
            new LoginRequest("test@example.com", "Password1!"),
            "127.0.0.1", "TestAgent");
        await Should.ThrowAsync<AccountLockedException>(act);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowMfaRequiredException_When_MfaEnabled()
    {
        // Arrange
        var user = CreateTestUser();
        user.IsMfaEnabled = true;
        _userRepository.FindByEmailAsync("test@example.com", default)
            .Returns(user);
        _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, "Password1!")
            .Returns(PasswordVerificationResult.Success);

        // Act & Assert
        var act = async () => await _authService.LoginAsync(
            new LoginRequest("test@example.com", "Password1!"),
            "127.0.0.1", "TestAgent");
        var ex = await Should.ThrowAsync<MfaRequiredException>(act);
        ex.SessionToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_EvictOldestSession_When_MaxSessionsReached()
    {
        // Arrange
        var user = CreateTestUser();
        _userRepository.FindByEmailAsync("test@example.com", default)
            .Returns(user);
        _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, "Password1!")
            .Returns(PasswordVerificationResult.Success);
        _sessionRepository.CountActiveByUserIdAsync(user.Id, default).Returns(5); // MAX
        _sessionRepository.FindOldestActiveByUserIdAsync(user.Id, default)
            .Returns(new UserSession { Id = "oldest-session" });
        _jwtTokenService.GenerateAccessToken(Arg.Any<User>(), Arg.Any<string>())
            .Returns("access-token");
        _jwtTokenService.GenerateRefreshToken()
            .Returns(("refresh-token", "refresh-hash"));

        // Act
        var result = await _authService.LoginAsync(
            new LoginRequest("test@example.com", "Password1!"),
            "127.0.0.1", "TestAgent");

        // Assert
        result.ShouldNotBeNull();
        await _sessionRepository.Received(1).FindOldestActiveByUserIdAsync(user.Id, default);
    }

    private static User CreateTestUser() => new()
    {
        Id = Guid.NewGuid().ToString(),
        Email = "test@example.com",
        Username = "testuser",
        PasswordHash = "hashed-password",
        FirstName = "Test",
        LastName = "User",
        Status = "ACTIVE",
        IsLocked = false,
        IsMfaEnabled = false,
        CreatedAt = DateTime.UtcNow
    };
}
```

### 9.4 バリデーターテスト

```csharp
public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_EmailIsEmpty()
    {
        // Arrange
        var request = new LoginRequest("", "Password1!");

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Email");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_FailValidation_When_PasswordTooShort()
    {
        // Arrange
        var request = new LoginRequest("test@example.com", "Pass1!");

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Password");
    }

    [Theory]
    [InlineData("Password1!")]
    [InlineData("MyP@ssw0rd")]
    [Trait("Category", "Unit")]
    public async Task Should_PassValidation_When_ValidCredentials(string password)
    {
        // Arrange
        var request = new LoginRequest("test@example.com", password);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
```

### 9.5 統合テスト（WebApplicationFactory）

```csharp
public class AuthEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public AuthEndpointsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_Return200_When_LoginWithValidCredentials()
    {
        // Arrange
        var request = new LoginRequest("admin@skishop.com", "Password1!");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        body.ShouldNotBeNull();
        body.AccessToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_Return400_When_LoginWithInvalidEmail()
    {
        // Arrange
        var request = new LoginRequest("not-an-email", "Password1!");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_Return401_When_LoginWithWrongPassword()
    {
        // Arrange
        var request = new LoginRequest("admin@skishop.com", "WrongPass1!");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
```

### 9.6 セキュリティテスト

```csharp
public class SecurityTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SecurityTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task Should_Return401_When_NoAuthToken()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/auth/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task Should_ContainSecurityHeaders_When_AnyResponse()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.Headers.ShouldContainHeader("X-Content-Type-Options", "nosniff");
        response.Headers.ShouldContainHeader("X-Frame-Options", "DENY");
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task Should_Return403_When_NonAdminAccessAdminEndpoint()
    {
        // Arrange: 通常ユーザーでログイン
        var loginResponse = await LoginAsUser();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResponse.AccessToken);

        // Act
        var response = await _client.DeleteAsync("/api/v1/auth/users/some-id/hard");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
```

### 9.7 DB スライステスト（Testcontainers）

```csharp
public class UserRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .Build();
    private AuthDbContext _context = null!;
    private UserRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        _context = new AuthDbContext(options, TimeProvider.System);
        await _context.Database.EnsureCreatedAsync();
        _repository = new UserRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    [Trait("Category", "Repository")]
    public async Task Should_FindUser_When_EmailExists()
    {
        // Arrange
        var user = new User
        {
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = "hash",
            Status = "ACTIVE"
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.FindByEmailAsync("test@example.com");

        // Assert
        result.ShouldNotBeNull();
        result.Email.ShouldBe("test@example.com");
    }

    [Fact]
    [Trait("Category", "Repository")]
    public async Task Should_ReturnNull_When_EmailNotFound()
    {
        // Act
        var result = await _repository.FindByEmailAsync("notfound@example.com");

        // Assert
        result.ShouldBeNull();
    }
}
```

### 9.8 テストカバレッジ計測

```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"./TestResults/**/coverage.cobertura.xml" \
                -targetdir:"./TestResults/CoverageReport" \
                -reporttypes:Html
```

### テスト一覧（設計書 §16 準拠）

#### 単体テスト（Service 層）

| テスト対象 | テストケース | カテゴリ |
|---------|----------|-------|
| AuthService.LoginAsync | 正常系: 有効なクレデンシャル | Unit |
| AuthService.LoginAsync | 異常系: ユーザー未存在 | Unit |
| AuthService.LoginAsync | 異常系: パスワード不一致 | Unit |
| AuthService.LoginAsync | 異常系: アカウントロック | Unit |
| AuthService.LoginAsync | 異常系: アカウント無効化 | Unit |
| AuthService.LoginAsync | 分岐: MFA 有効 → MfaRequiredException | Unit |
| AuthService.LoginAsync | 分岐: セッション上限 → 最古セッション削除 | Unit |
| AuthService.LoginAsync | 分岐: 5 回失敗 → アカウントロック | Unit |
| AuthService.RefreshTokenAsync | 正常系: 有効なリフレッシュトークン | Unit |
| AuthService.RefreshTokenAsync | 異常系: 期限切れトークン | Unit |
| AuthService.RefreshTokenAsync | 異常系: 使用済みトークン（リプレイ検出） | Unit |
| AuthService.LogoutAsync | 正常系: セッション終了 + ブラックリスト登録 | Unit |
| JwtTokenService | 正常系: トークン生成 + 検証 | Unit |
| UserRegistrationService | 正常系: ユーザー登録 | Unit |
| UserRegistrationService | 異常系: メールアドレス重複 | Unit |
| PasswordService | 正常系: パスワード変更 | Unit |
| PasswordService | 正常系: パスワードリセットフロー | Unit |
| MfaService | 正常系: TOTP セットアップ + 検証 | Unit |
| SessionService | 正常系: アイドルタイムアウト検出 | Unit |
| PasswordService | 異常系: 直近 5 回分のパスワード再利用拒否（設計書 §29.1） | Unit |
| PasswordService | 正常系: パスワード変更時に履歴記録 | Unit |
| AuthService.RefreshTokenAsync | 異常系: Replay Detection — 失効済みトークン使用で全ファミリー無効化（設計書 §29.10） | Unit |
| AuthService.RefreshTokenAsync | 異常系: 絶対有効期限超過で再ログイン要求 | Unit |
| AuthService.RefreshTokenAsync | 正常系: ファミリー ID 継承 + 前トークン ID 設定 | Unit |
| ClientCredentialsService | 正常系: M2M トークン発行（設計書 §29.5） | Unit |
| ClientCredentialsService | 異常系: 無効な grant_type | Unit |
| ClientCredentialsService | 異常系: 無効なクライアントシークレット | Unit |
| UserRegistrationService | 正常系: メール検証トークンの検証（設計書 §29.2） | Unit |
| UserRegistrationService | 異常系: 期限切れメール検証トークン | Unit |
| TokenCleanupService | 正常系: 期限切れトークン削除（設計書 §26） | Unit |
| FailedAttemptResetService | 正常系: 30 分後自動ロック解除（設計書 §26） | Unit |
| SecurityLogAnonymizationService | 正常系: 90 日後 IP アドレス仮名化（設計書 §29.4） | Unit |

#### 統合テスト（Endpoints）

| テスト対象 | テストケース | カテゴリ |
|---------|----------|-------|
| POST /auth/login | 200: 正常ログイン | Integration |
| POST /auth/login | 400: バリデーションエラー | Integration |
| POST /auth/login | 401: 認証失敗 | Integration |
| POST /auth/login | 423: アカウントロック | Integration |
| GET /auth/me | 200: トークン認証成功 | Integration |
| GET /auth/me | 401: トークンなし | Integration |
| DELETE /auth/users/{id}/hard | 403: 非管理者 | Security |
| POST /auth/email/verify | 200: 正常メール検証（設計書 §29.2） | Integration |
| POST /auth/email/verify | 400: 無効トークン | Integration |
| POST /auth/token | 200: Client Credentials トークン発行（設計書 §29.5） | Integration |
| POST /auth/token | 400: 無効な grant_type | Integration |
| セキュリティヘッダー | 全レスポンスに含まれること（5 ヘッダー） | Security |

### Phase 9 完了チェックリスト

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | `dotnet test` | 全テスト成功 |
| 2 | 分岐カバレッジ 80% 以上 | `dotnet test --collect:"XPlat Code Coverage"` |
| 3 | 命名規則 | `Should_X_When_Y` パターン |
| 4 | AAA パターン | Arrange / Act / Assert 分離 |
| 5 | `[Trait("Category", "...")]` | 全テストにカテゴリ付与 |
| 6 | Shouldly | `result.ShouldBe()` / `ShouldNotBeNull()` |
| 7 | NSubstitute | `Received(1)` / `DidNotReceive()` 検証 |
| 8 | 例外テスト | 型 + メッセージ内容を検証 |
| 9 | 境界値テスト | null / 空文字 / 最大長 / 最大長+1 |
| 10 | TimeProvider | `FakeTimeProvider` でテスト時刻固定 |
| 11 | Testcontainers | PostgreSQL 17 で DB スライステスト |
| 12 | WebApplicationFactory | 統合テスト |
| 13 | セキュリティテスト | 認証なしアクセス / 権限不足 / ヘッダー検証 |
| 14 | パスワード履歴テスト | 直近 5 回分の再利用拒否（設計書 §29.1） |
| 15 | Replay Detection テスト | ファミリー無効化・絶対有効期限（設計書 §29.10） |
| 16 | Client Credentials テスト | M2M トークン発行・無効クライアント（設計書 §29.5） |
| 17 | Email Verification テスト | 検証・再送・期限切れ（設計書 §29.2） |
| 18 | BackgroundService テスト | TokenCleanup / FailedAttemptReset / SecurityLogAnonymization（設計書 §26） |
| 19 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 20 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 21 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## Phase 10: 可観測性（Observability）

### 目的

AGENTS.md §11.2 および設計書 §11 に基づき、Serilog 構造化ログ、OpenTelemetry 分散トレーシング・メトリクス、ヘルスチェック、Correlation ID を統合する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `AuthService/Program.cs` | 更新 | Serilog + OpenTelemetry + ヘルスチェック統合 |
| 2 | `AuthService/Infrastructure/Middleware/CorrelationIdMiddleware.cs` | 更新 | `X-Correlation-Id` 伝搬 |
| 3 | `AuthService/Infrastructure/Metrics/AuthMetrics.cs` | 作成 | カスタムメトリクス定義（設計書 §10） |

### 10.1 Serilog 構成（AGENTS.md §11.2 準拠）

```csharp
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "AuthService")
        .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
        .WriteTo.Console(new CompactJsonFormatter()));
```

### 10.2 OpenTelemetry 統合

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource("SkiShop.AuthService"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation());
```

### 10.3 ヘルスチェック

```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql", tags: ["ready"])
    .AddRedis(redisConnectionString, name: "redis", tags: ["ready"]);

// Liveness: アプリ生存確認（常に 200）
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

// Readiness: PostgreSQL + Redis 疎通確認
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();
```

### 10.4 Correlation ID ミドルウェア

```csharp
public class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();
        context.Response.Headers.Append("X-Correlation-Id", correlationId);
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
```

### 10.5 Serilog リクエストログ

```csharp
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
    };
});
```

### 10.6 AuthService カスタムメトリクス（設計書 §10 準拠）

設計書 §10 で定義されたカスタムメトリクスを `System.Diagnostics.Metrics` で実装する。

```csharp
public class AuthMetrics
{
    private static readonly Meter Meter = new("SkiShop.AuthService", "1.0.0");

    // カウンター
    public static readonly Counter<long> LoginAttempts =
        Meter.CreateCounter<long>("auth_login_attempts_total", description: "ログイン試行回数（result: success/failure, method: password/oauth）");
    public static readonly Counter<long> TokenRefreshes =
        Meter.CreateCounter<long>("auth_token_refresh_total", description: "トークンリフレッシュ回数");
    public static readonly Counter<long> MfaVerifications =
        Meter.CreateCounter<long>("auth_mfa_verification_total", description: "MFA 検証回数（result: success/failure）");
    public static readonly Counter<long> AccountLockouts =
        Meter.CreateCounter<long>("auth_account_lockouts_total", description: "アカウントロックアウト回数");
    public static readonly Counter<long> PasswordResets =
        Meter.CreateCounter<long>("auth_password_resets_total", description: "パスワードリセット回数");

    // ゲージ
    public static readonly ObservableGauge<long> ActiveSessions =
        Meter.CreateObservableGauge<long>("auth_active_sessions", description: "アクティブセッション数");
}
```

> **OpenTelemetry 登録**: `AddMeter("SkiShop.AuthService")` を `WithMetrics` に追加すること。

```csharp
.WithMetrics(metrics => metrics
    .AddAspNetCoreInstrumentation()
    .AddHttpClientInstrumentation()
    .AddRuntimeInstrumentation()
    .AddMeter("SkiShop.AuthService"));  // カスタムメトリクス登録
```

### 10.7 パフォーマンス目標（設計書 §9 準拠）

| 操作 | 目標レスポンスタイム | 備考 |
|------|-------------------|------|
| ログイン（`POST /auth/login`） | < 200ms（p95） | Redis キャッシュ + Argon2id |
| トークン検証 | < 50ms（p95） | JWT ローカル検証 |
| トークンリフレッシュ | < 100ms（p95） | DB 1 読 + 1 書 |
| MFA 検証 | < 100ms（p95） | TOTP 計算 + Redis |
| ユーザー登録 | < 300ms（p95） | Argon2id ハッシュ計算含む |

> **計測方法**: OpenTelemetry の ASP.NET Core 計装でリクエスト duration ヒストグラムを取得し、Grafana ダッシュボードで p95 を監視する。

### Phase 10 完了チェックリスト

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | `dotnet build` | 警告なし成功 |
| 2 | Serilog | CompactJsonFormatter + ServiceName エンリッチ |
| 3 | OpenTelemetry | Tracing + Metrics + ASP.NET Core / EF Core / HTTP 計装 |
| 4 | ヘルスチェック | `/health`（Liveness）+ `/health/ready`（Readiness） |
| 5 | ヘルスチェック | PostgreSQL + Redis 疎通確認 |
| 6 | Correlation ID | `X-Correlation-Id` ヘッダー付与 + ログプロパティ |
| 7 | PII 非出力 | ログにメール・パスワード・トークン値なし |
| 8 | ログレベル | 本番: Warning デフォルト、SkiShop: Information |
| 9 | カスタムメトリクス | `auth_login_attempts_total`, `auth_active_sessions` 等 6 メトリクス登録（設計書 §10） |
| 10 | パフォーマンス目標 | ログイン <200ms, トークン検証 <50ms（設計書 §9） |
| 11 | `AddMeter("SkiShop.AuthService")` | OpenTelemetry にカスタム Meter 登録 |
| 12 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 13 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 14 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## Phase 11: Docker / デプロイ

### 目的

AGENTS.md §12.5 および `.github/instructions/dockerfile-infra.instructions.md` に基づき、Dockerfile を最終化し、.NET Aspire AppHost にサービスを統合する。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `AuthService/Dockerfile` | 更新 | マルチステージビルド最終化 |
| 2 | `AppHost/Program.cs` | 更新 | AuthService 参照追加 |

### 11.1 Dockerfile（最終版）

```dockerfile
# ============= Build Stage =============
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["AuthService/AuthService.csproj", "AuthService/"]
RUN dotnet restore "AuthService/AuthService.csproj"

COPY . .
WORKDIR "/src/AuthService"
RUN dotnet publish "AuthService.csproj" -c Release -o /app/publish --no-restore

# ============= Runtime Stage =============
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# 非 root ユーザー作成（必須）
RUN groupadd -r skishop && useradd -r -g skishop -d /app -s /sbin/nologin skishop
COPY --from=build --chown=skishop:skishop /app/publish .

USER skishop

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "AuthService.dll"]
```

### 11.2 .dockerignore

```
**/bin/
**/obj/
.git/
.github/
*.md
*.sln.DotSettings
.idea/
.vs/
design-docs/
impl-plan/
TestResults/
```

### 11.3 .NET Aspire AppHost 統合

```csharp
// AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin()
    .AddDatabase("authdb");

var redis = builder.AddRedis("redis").WithRedisInsight();
var kafka = builder.AddKafka("kafka");

var authService = builder.AddProject<Projects.AuthService>("auth-service")
    .WithReference(postgres)
    .WithReference(redis)
    .WithReference(kafka)
    .WithExternalHttpEndpoints();

// API Gateway からの参照
builder.AddProject<Projects.ApiGateway>("api-gateway")
    .WithReference(authService);

builder.Build().Run();
```

### 11.4 環境変数一覧

| 変数名 | 用途 | 設定元 |
|--------|------|--------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL 接続文字列 | 環境変数 / Aspire |
| `Redis__ConnectionString` | Redis 接続文字列 | 環境変数 / Aspire |
| `Kafka__BootstrapServers` | Kafka ブートストラップサーバー | 環境変数 / Aspire |
| `Jwt__Issuer` | JWT 発行者 | 環境変数 |
| `Jwt__Audience` | JWT 対象者 | 環境変数 |
| `Jwt__SecretKey` | JWT 署名鍵 | 環境変数 / Key Vault |
| `ASPNETCORE_ENVIRONMENT` | 環境名 | 環境変数 |

### Phase 11 完了チェックリスト

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | マルチステージビルド | SDK → aspnet ランタイム分離 |
| 2 | ベースイメージ | `mcr.microsoft.com/dotnet/aspnet:10.0`（latest 禁止） |
| 3 | 非 root 実行 | `USER skishop` |
| 4 | HEALTHCHECK | `/health` エンドポイント |
| 5 | .dockerignore | 不要ファイル除外 |
| 6 | `docker build` | イメージビルド成功 |
| 7 | Aspire AppHost | PostgreSQL + Redis + Kafka 参照 |
| 8 | 秘密情報 | Dockerfile / docker-compose に直接記述なし |
| 9 | 環境変数 | `ASPNETCORE_ENVIRONMENT=Production` |
| 10 | ポート | 8080 のみ EXPOSE |
| 11 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 12 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 13 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

---

## 最終チェックリスト

### AGENTS.md 禁止事項チェック（Critical / High）

実装完了後、以下のコマンドを実行し、全て **0 件** であることを確認する。

```bash
# 1. Console.WriteLine チェック（Critical）
grep -r "Console\.Write" --include="*.cs" AuthService/

# 2. 例外の握りつぶしチェック（Critical）
grep -rn "catch.*Exception.*{" --include="*.cs" AuthService/ | head -20
# → 全 catch ブロックにログ出力 or 再スローがあることを手動確認

# 3. 秘密情報ハードコードチェック（Critical）
grep -r "Password\s*=\s*\"" --include="*.cs" AuthService/
grep -r "ApiKey\s*=\s*\"" --include="*.cs" AuthService/
grep -r "SecretKey\s*=\s*\"" --include="*.cs" AuthService/

# 4. SQL インジェクションチェック（Critical）
grep -r "FromSqlRaw" --include="*.cs" AuthService/

# 5. 直接インスタンス化チェック（Critical）
grep -r "new HttpClient()" --include="*.cs" AuthService/

# 6. Thread.Sleep チェック（High）
grep -r "Thread\.Sleep" --include="*.cs" AuthService/

# 7. .Result / .Wait() チェック（High）
grep -rE "\.(Result|Wait)\(\)" --include="*.cs" AuthService/

# 8. DateTime.Now チェック（High）
grep -r "DateTime\.Now[^U]" --include="*.cs" AuthService/

# 9. プロパティインジェクションチェック（High）
grep -r "\[Inject\]" --include="*.cs" AuthService/

# 10. 文字列補間ログチェック（Medium）
grep -rE '_logger\.Log(Information|Warning|Error|Debug)\(\$"' --include="*.cs" AuthService/
```

### コーディング規約準拠チェック

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | 命名規則 | PascalCase（クラス）/ camelCase（ローカル）/ `_camelCase`（フィールド） |
| 2 | レイヤー依存方向 | Endpoints → Services → Repositories（逆転なし） |
| 3 | DI | primary constructor + Scoped 登録（プロパティインジェクション禁止） |
| 4 | CancellationToken | 全 async メソッドに `CancellationToken ct = default` |
| 5 | record 型 | 全 DTO は record |
| 6 | `ILogger<T>` | 全 Service / Repository にインジェクション |
| 7 | Null Safety | `<Nullable>enable</Nullable>` + `??` / `?.` / `ThrowIfNull()` |
| 8 | `AsNoTracking()` | 読み取り専用クエリに付与 |
| 9 | EF Core エンティティ | `[Table]` + `[Column]` snake_case / `= []` コレクション初期化 |
| 10 | `DateTime.UtcNow` / `TimeProvider` | ローカル時刻使用なし |
| 11 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 12 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 13 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

### セキュリティ規約準拠チェック

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | 入力バリデーション | 全エンドポイントに FluentValidation |
| 2 | SQL インジェクション | EF Core LINQ のみ使用 |
| 3 | 認証・認可 | FallbackPolicy + ロールベース |
| 4 | IDOR 防止 | オーナーシップチェック |
| 5 | パスワードハッシュ | Argon2id |
| 6 | セキュリティヘッダー | 全 5 ヘッダー |
| 7 | CORS | ワイルドカード不使用 |
| 8 | レート制限 | 認証エンドポイント 5 req/min |
| 9 | PII ログ禁止 | メール・パスワード・トークンなし |
| 10 | `DetailedErrors: false` | 本番設定 |
| 11 | Mass Assignment 防止 | リクエスト DTO 経由のみ |
| 12 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 13 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 14 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

### テスト規約準拠チェック

| # | チェック項目 | 基準 |
|---|-----------|------|
| 1 | カバレッジ | 分岐カバレッジ 80% 以上 |
| 2 | 命名 | `Should_X_When_Y` |
| 3 | AAA パターン | Arrange / Act / Assert 分離 |
| 4 | Shouldly | `ShouldBe()` / `ShouldNotBeNull()` |
| 5 | NSubstitute | `Received()` / `DidNotReceive()` |
| 6 | Testcontainers | PostgreSQL 17 で DB テスト |
| 7 | WebApplicationFactory | 統合テスト |
| 8 | 異常系テスト | 正常系と同等以上 |
| 9 | `[Trait("Category", "...")]` | 全テストにカテゴリ |
| 10 | 例外テスト | 型 + メッセージ検証 |
| 11 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 12 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 13 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

### フェーズ依存関係図

```
Phase 1 ─── Phase 2 ─── Phase 3 ─── Phase 4 ─── Phase 5
 (基盤)      (Entity)    (Repository) (Service)   (Endpoints)
                 │                        │            │
                 └── Phase 6 ─────────────┘            │
                     (Kafka)                           │
                                                       │
Phase 7 ─────────── Phase 8 ──────────────────────── Phase 9
(Redis)             (認証/認可/Security)              (テスト)
                                                       │
                                                   Phase 10
                                                  (可観測性)
                                                       │
                                                   Phase 11
                                                   (Docker)
```

### 最終確認コマンド

```bash
# 1. ビルド確認
dotnet build AuthService/AuthService.csproj

# 2. テスト実行（全カテゴリ）
dotnet test AuthService.Tests/AuthService.Tests.csproj

# 3. カバレッジ確認
dotnet test AuthService.Tests/AuthService.Tests.csproj \
    --collect:"XPlat Code Coverage" \
    --results-directory ./TestResults

# 4. カテゴリ別テスト
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
dotnet test --filter "Category=Security"
dotnet test --filter "Category=Repository"

# 5. Docker イメージビルド
docker build -t skishop-auth-service:latest -f AuthService/Dockerfile .

# 6. 禁止事項チェック（上記 AGENTS.md 禁止事項チェック参照）
```
