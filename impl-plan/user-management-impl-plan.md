# UserManagementService フェーズ別実装計画書

> **対象サービス**: UserManagementService（ユーザープロファイル管理マイクロサービス）
> **ポート**: 5002
> **DB**: PostgreSQL（`userdb`）— EF Core 10 Migrations 管理
> **メッセージング**: Apache Kafka（Outbox パターン — ADR-0005 準拠）
> **設計書**: `design-docs/user-management-design.md`
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

UserManagementService プロジェクトの骨格を構築する。ビルド可能な最小構成を作成し、以降のフェーズの土台とする。ポート 5002 で起動し `/health` で 200 応答を確認する。

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `UserManagementService/UserManagementService.csproj` | プロジェクト定義（EF Core, Kafka, Serilog, OpenTelemetry 等） |
| 2 | `UserManagementService/Program.cs` | エントリポイントスケルトン（最小構成） |
| 3 | `UserManagementService/appsettings.json` | 共通設定（安全なデフォルト値のみ、秘密情報禁止） |
| 4 | `UserManagementService/appsettings.Development.json` | 開発環境設定 |
| 5 | `UserManagementService/appsettings.Production.json` | 本番環境設定（環境変数参照のみ） |
| 6 | `UserManagementService/Dockerfile` | マルチステージビルド + 非 root 実行 |
| 7 | `UserManagementService/.dockerignore` | ビルド不要ファイルの除外 |

### 1.1 UserManagementService.csproj

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

    <!-- バリデーション -->
    <PackageReference Include="FluentValidation" Version="11.*" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.*" />

    <!-- メッセージング -->
    <PackageReference Include="Confluent.Kafka" Version="2.*" />

    <!-- ロギング -->
    <PackageReference Include="Serilog.AspNetCore" Version="8.*" />
    <PackageReference Include="Serilog.Sinks.Console" Version="6.*" />
    <PackageReference Include="Serilog.Formatting.Compact" Version="3.*" />

    <!-- OpenTelemetry -->
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.*" />

    <!-- 耐障害性 -->
    <PackageReference Include="Polly" Version="8.*" />
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="9.*" />

    <!-- ヘルスチェック -->
    <PackageReference Include="AspNetCore.HealthChecks.NpgSql" Version="9.*" />
  </ItemGroup>
</Project>
```

> **注記**: Redis パッケージは Phase 7 で追加する。`StackExchange.Redis` / `AspNetCore.HealthChecks.Redis` は Phase 1 では不要。
> spec.md の AppHost 定義では userService に `.WithReference(redis)` は含まれないため、キャッシュ利用は任意拡張。

### 1.2 ディレクトリ構造

```
UserManagementService/
├── UserManagementService.csproj
├── Program.cs
├── Endpoints/
│   ├── UserEndpoints.cs
│   ├── AddressEndpoints.cs
│   ├── WishlistEndpoints.cs
│   ├── PreferenceEndpoints.cs
│   ├── ActivityEndpoints.cs
│   ├── ConsentEndpoints.cs
│   ├── DsrEndpoints.cs
│   ├── MemberRankEndpoints.cs
│   └── AdminUserEndpoints.cs
├── Services/
│   ├── Interfaces/
│   │   ├── IUserService.cs
│   │   ├── IAddressService.cs
│   │   ├── IWishlistService.cs
│   │   ├── IPreferenceService.cs
│   │   ├── IActivityService.cs
│   │   ├── IConsentService.cs
│   │   ├── IDsrService.cs
│   │   ├── IMemberRankService.cs
│   │   └── IEventPublisherService.cs
│   ├── UserService.cs
│   ├── AddressService.cs
│   ├── WishlistService.cs
│   ├── PreferenceService.cs
│   ├── ActivityService.cs
│   ├── ConsentService.cs
│   ├── DsrService.cs
│   ├── MemberRankService.cs
│   └── EventPublisherService.cs
├── Repositories/
│   ├── Interfaces/
│   │   ├── IUserRepository.cs
│   │   ├── IAddressRepository.cs
│   │   ├── IWishlistRepository.cs
│   │   ├── IPreferenceRepository.cs
│   │   ├── IActivityRepository.cs
│   │   ├── IConsentRepository.cs
│   │   ├── IDeletionRequestRepository.cs
│   │   ├── IMemberRankRepository.cs
│   │   └── IOutboxEventRepository.cs
│   ├── UserRepository.cs
│   ├── AddressRepository.cs
│   ├── WishlistRepository.cs
│   ├── PreferenceRepository.cs
│   ├── ActivityRepository.cs
│   ├── ConsentRepository.cs
│   ├── DeletionRequestRepository.cs
│   ├── MemberRankRepository.cs
│   └── OutboxEventRepository.cs
├── Models/
│   ├── User.cs
│   ├── Address.cs
│   ├── Wishlist.cs
│   ├── WishlistItem.cs
│   ├── UserPreference.cs
│   ├── UserActivity.cs
│   ├── MemberRank.cs
│   ├── Consent.cs
│   ├── DeletionRequest.cs
│   └── OutboxEvent.cs
├── DTOs/
│   ├── Requests/
│   │   ├── UpdateUserRequest.cs
│   │   ├── CreateAddressRequest.cs
│   │   ├── UpdateAddressRequest.cs
│   │   ├── CreateWishlistRequest.cs
│   │   ├── UpdateWishlistRequest.cs
│   │   ├── AddWishlistItemRequest.cs
│   │   ├── UpdatePreferenceRequest.cs
│   │   ├── ConsentUpdateRequest.cs
│   │   ├── CreateDeletionRequest.cs
│   │   ├── UpdateUserStatusRequest.cs
│   │   └── UpdateProcessingRestrictionRequest.cs
│   └── Responses/
│       ├── UserDto.cs
│       ├── AddressDto.cs
│       ├── WishlistDto.cs
│       ├── WishlistItemDto.cs
│       ├── PreferenceDto.cs
│       ├── ActivityDto.cs
│       ├── ConsentDto.cs
│       ├── MemberRankDto.cs
│       └── DeletionRequestDto.cs
├── Configurations/
│   └── KafkaSettings.cs
├── Validators/
│   ├── UpdateUserRequestValidator.cs
│   ├── CreateAddressRequestValidator.cs
│   ├── UpdateAddressRequestValidator.cs
│   ├── CreateWishlistRequestValidator.cs
│   ├── UpdatePreferenceRequestValidator.cs
│   └── ConsentUpdateRequestValidator.cs
├── Exceptions/
│   ├── NotFoundException.cs
│   ├── BusinessException.cs
│   ├── UnauthorizedException.cs
│   ├── ForbiddenException.cs
│   └── ConcurrencyException.cs
├── Events/
│   ├── UserRegisteredEvent.cs
│   ├── UserDeletedEvent.cs
│   ├── UserProfileUpdatedEvent.cs
│   ├── ConsentRevokedEvent.cs
│   ├── OrderConfirmedEvent.cs
│   ├── InventoryStockUpdatedEvent.cs
│   ├── PasswordChangedEvent.cs
│   ├── MemberRankUpdatedEvent.cs
│   └── UserDeletionNotificationEvent.cs
├── BackgroundServices/
│   ├── OutboxPublisher.cs
│   ├── MemberRankEvaluationService.cs
│   ├── DsrTimeoutMonitorService.cs
│   ├── DeletionRequestProcessor.cs
│   ├── UserRegisteredConsumer.cs
│   ├── UserDeletionCompletedConsumer.cs
│   ├── OrderConfirmedConsumer.cs
│   ├── InventoryStockUpdatedConsumer.cs
│   ├── PasswordChangedConsumer.cs
│   └── DataExportService.cs
├── Infrastructure/
│   ├── Persistence/
│   │   └── AppDbContext.cs
│   └── Middleware/
│       ├── CorrelationIdMiddleware.cs
│       └── SecurityHeadersMiddleware.cs
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

// Serilog（最小構成）
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "UserManagementService")
        .WriteTo.Console(new CompactJsonFormatter()));

// TimeProvider（テスタビリティ確保）
builder.Services.AddSingleton(TimeProvider.System);

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
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "Kafka": {
    "BootstrapServers": "",
    "ConsumerGroupId": "user-management-service"
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
COPY ["UserManagementService/UserManagementService.csproj", "UserManagementService/"]
RUN dotnet restore "UserManagementService/UserManagementService.csproj"
COPY . .
WORKDIR "/src/UserManagementService"
RUN dotnet publish "UserManagementService.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN groupadd -r skishop && useradd -r -g skishop -d /app -s /sbin/nologin skishop
COPY --from=build --chown=skishop:skishop /app/publish .
USER skishop

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 5002
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:5002/health || exit 1
ENTRYPOINT ["dotnet", "UserManagementService.dll"]
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

- [ ] `dotnet build UserManagementService/UserManagementService.csproj` — 警告なし成功
- [ ] `dotnet run --project UserManagementService` — 起動確認（`/health` で 200 応答）
- [ ] .csproj: `TreatWarningsAsErrors=true`, `Nullable=enable`, `TargetFramework=net10.0`
- [ ] .csproj: プレリリース版パッケージなし（`-preview`, `-beta`, `-rc` なし）
- [ ] appsettings.json: 秘密情報なし（パスワード、API キー、接続文字列の値なし）
- [ ] appsettings.json: `DetailedErrors: false`, `AddServerHeader: false`
- [ ] Dockerfile: マルチステージビルド、非 root ユーザー（`skishop`）、HEALTHCHECK あり
- [ ] Dockerfile: ベースイメージに `latest` タグなし（`10.0` 固定）
- [ ] .dockerignore: `bin/`, `obj/`, `.git/`, `*.md` 除外
- [ ] **TODO/FIXME/HACK コメント残存チェック**: ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック**: テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック**: 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 2: エンティティ・Value Object・Enum 定義

### 目的

設計書 §3（データベース設計）および §14（エンティティ定義例）に基づき、全 10 エンティティ、ステータス定数クラス、DTO（record 型）、カスタム例外クラス、イベント型、AppDbContext を定義する。EF Core Migrations で `userdb` にスキーマを生成する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `UserManagementService/Models/User.cs` | 作成 | User エンティティ（Aggregate Root） |
| 2 | `UserManagementService/Models/Address.cs` | 作成 | Address エンティティ |
| 3 | `UserManagementService/Models/Wishlist.cs` | 作成 | Wishlist エンティティ |
| 4 | `UserManagementService/Models/WishlistItem.cs` | 作成 | WishlistItem エンティティ |
| 5 | `UserManagementService/Models/UserPreference.cs` | 作成 | UserPreference エンティティ（1:1） |
| 6 | `UserManagementService/Models/UserActivity.cs` | 作成 | UserActivity エンティティ |
| 7 | `UserManagementService/Models/MemberRank.cs` | 作成 | MemberRank エンティティ（1:1） |
| 8 | `UserManagementService/Models/Consent.cs` | 作成 | Consent エンティティ |
| 9 | `UserManagementService/Models/DeletionRequest.cs` | 作成 | DeletionRequest エンティティ（GDPR DSR） |
| 10 | `UserManagementService/Models/OutboxEvent.cs` | 作成 | OutboxEvent エンティティ（Outbox パターン） |
| 11 | `UserManagementService/Infrastructure/Persistence/AppDbContext.cs` | 作成 | DbContext（全 DbSet, FK, CHECK 制約, インデックス, SaveChanges オーバーライド） |
| 12 | `UserManagementService/DTOs/Requests/*.cs` | 作成 | リクエスト DTO（record 型、全 11 ファイル） |
| 13 | `UserManagementService/DTOs/Responses/*.cs` | 作成 | レスポンス DTO（record 型、全 9 ファイル） |
| 14 | `UserManagementService/Exceptions/*.cs` | 作成 | カスタム例外クラス（5 ファイル） |
| 15 | `UserManagementService/Events/*.cs` | 作成 | Kafka イベント型（6 ファイル） |
| 16 | `UserManagementService/Program.cs` | 更新 | EF Core DbContext DI 登録追加 |

### 2.1 エンティティ一覧（設計書 §3 ER 図 + §14 準拠）

| エンティティ | テーブル名 | PK | 主な FK / 制約 |
|------------|----------|-----|---------------|
| `User` | `users` | `id` (UUID) | `UNIQUE(email)`, CHECK(status) |
| `Address` | `addresses` | `id` (UUID) | `FK(user_id)→users ON DELETE CASCADE`, CHECK(address_type) |
| `Wishlist` | `wishlists` | `id` (UUID) | `FK(user_id)→users ON DELETE CASCADE` |
| `WishlistItem` | `wishlist_items` | `id` (UUID) | `FK(wishlist_id)→wishlists ON DELETE CASCADE` |
| `UserPreference` | `user_preferences` | `id` (UUID) | `FK(user_id)→users ON DELETE CASCADE`, UNIQUE(user_id) |
| `UserActivity` | `user_activities` | `id` (UUID) | `FK(user_id)→users ON DELETE CASCADE` |
| `MemberRank` | `member_ranks` | `id` (UUID) | `FK(user_id)→users ON DELETE CASCADE`, UNIQUE(user_id), CHECK(current_rank), CHECK(annual_purchase_amount >= 0), CHECK(point_rate) |
| `Consent` | `consents` | `id` (UUID) | `FK(user_id)→users ON DELETE CASCADE`, CHECK(consent_type) |
| `DeletionRequest` | `deletion_requests` | `id` (UUID) | `FK(user_id)→users ON DELETE SET NULL`, CHECK(status), CHECK(request_channel) |
| `OutboxEvent` | `outbox_events` | `id` (UUID) | Partial Index(status='PENDING') |

### 2.2 User エンティティ（設計書 §14 完全準拠）

```csharp
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

    [Column("first_name")]
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Column("last_name")]
    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Column("phone_number")]
    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [Column("birth_date")]
    public DateOnly? BirthDate { get; set; }

    [Column("status")]
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = UserStatus.PendingVerification;

    [Column("processing_restricted")]
    public bool ProcessingRestricted { get; set; } = false;

    [Column("restriction_reason")]
    [MaxLength(500)]
    public string? RestrictionReason { get; set; }

    [Column("restricted_at")]
    public DateTimeOffset? RestrictedAt { get; set; }

    [Column("last_login_at")]
    public DateTimeOffset? LastLoginAt { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // ナビゲーションプロパティ
    public UserPreference? Preference { get; set; }
    public MemberRank? MemberRank { get; set; }
    public ICollection<Address> Addresses { get; set; } = [];
    public ICollection<Wishlist> Wishlists { get; set; } = [];
    public ICollection<UserActivity> Activities { get; set; } = [];
    public ICollection<Consent> Consents { get; set; } = [];
    public ICollection<DeletionRequest> DeletionRequests { get; set; } = [];
}

public static class UserStatus
{
    public const string PendingVerification = "PENDING_VERIFICATION";
    public const string Active = "ACTIVE";
    public const string Suspended = "SUSPENDED";
    public const string Deactivated = "DEACTIVATED";
}
```

### 2.3 ステータス・種別定数クラス

```csharp
public static class AddressType
{
    public const string Shipping = "SHIPPING";
    public const string Billing = "BILLING";
}

public static class MemberRankLevel
{
    public const string Bronze = "BRONZE";
    public const string Silver = "SILVER";
    public const string Gold = "GOLD";
    public const string Platinum = "PLATINUM";
}

public static class ConsentType
{
    public const string Marketing = "MARKETING";
    public const string Personalization = "PERSONALIZATION";
    public const string Analytics = "ANALYTICS";
    public const string ThirdPartySharing = "THIRD_PARTY_SHARING";
}

public static class DeletionRequestStatus
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";
    public const string AwaitingManualIntervention = "AWAITING_MANUAL_INTERVENTION";
}

public static class RequestChannel
{
    public const string WebSelfService = "WEB_SELF_SERVICE";
    public const string AdminConsole = "ADMIN_CONSOLE";
    public const string EmailDsr = "EMAIL_DSR";
    public const string Api = "API";
}

public static class OutboxEventStatus
{
    public const string Pending = "PENDING";
    public const string Published = "PUBLISHED";
    public const string Failed = "FAILED";
}
```

### 2.4 AppDbContext（設計書 §A 完全準拠）

設計書の追記セクション §A に定義された `AppDbContext` をそのまま実装する。

- 全 10 エンティティの `DbSet<T>` プロパティ
- FK 制約（CASCADE / SET NULL）
- CHECK 制約（status, address_type, current_rank, consent_type, request_channel, deletion_requests.status）
- インデックス（UNIQUE, Composite, Partial Index）
- `SaveChangesAsync` オーバーライドで `CreatedAt` / `UpdatedAt` 自動管理（`TimeProvider` 使用）

### 2.5 DTO 定義（record 型 — 設計書 §D 準拠）

**リクエスト DTO（全 11 ファイル）**:

```csharp
// ── ユーザープロファイル ──
public record UpdateUserRequest(
    string? FirstName, string? LastName,
    string? PhoneNumber, DateOnly? BirthDate);

// ── 住所 ──
public record CreateAddressRequest(
    [Required] string AddressType,
    [Required, MaxLength(100)] string Recipient,
    [Required, MaxLength(10)] string ZipCode,
    [Required, MaxLength(50)] string Prefecture,
    [Required, MaxLength(100)] string City,
    [Required, MaxLength(255)] string StreetAddress,
    [MaxLength(255)] string? Building,
    [MaxLength(20)] string? PhoneNumber);

public record UpdateAddressRequest(
    string? Recipient, string? ZipCode, string? Prefecture,
    string? City, string? StreetAddress, string? Building,
    string? PhoneNumber, bool? IsDefault);

// ── ウィッシュリスト ──
public record CreateWishlistRequest(
    [Required, MaxLength(100)] string Name, bool IsDefault = false);
public record UpdateWishlistRequest(string? Name, bool? IsDefault);
public record AddWishlistItemRequest(
    [Required, MaxLength(36)] string ProductId, bool NotifyOnRestock = false);

// ── ユーザー設定 ──
public record UpdatePreferenceRequest(
    string? Language, string? Currency,
    string? NotificationPreferences, string? DisplayPreferences);

// ── 同意管理 ──
public record ConsentUpdateRequest(
    [Required] string ConsentType, bool IsGranted, int PolicyVersion);

// ── GDPR/DSR ──
public record CreateDeletionRequest([Required, MaxLength(30)] string RequestChannel);

// ── 管理者 ──
public record UpdateUserStatusRequest([Required, MaxLength(50)] string Status);
public record UpdateProcessingRestrictionRequest(
    bool ProcessingRestricted, [MaxLength(500)] string? RestrictionReason);
```

**レスポンス DTO（全 9 ファイル）** — 設計書 §D のレスポンス DTO をそのまま `record` で定義。

### 2.6 カスタム例外クラス

```csharp
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
public class UnauthorizedException : Exception
{
    public UnauthorizedException() : base("認証が必要です") { }
    public UnauthorizedException(string message) : base(message) { }
}
public class ForbiddenException : Exception
{
    public ForbiddenException() : base("アクセスが拒否されました") { }
    public ForbiddenException(string message) : base(message) { }
}
public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message) { }
    public ConcurrencyException(string message, Exception innerException) : base(message, innerException) { }
}
```

### 2.7 Kafka イベント型（record 定義）

```csharp
// 購読イベント
public record UserRegisteredEvent(
    string UserId, string Email, string FirstName, string LastName, DateTimeOffset RegisteredAt);
public record OrderConfirmedEvent(
    string OrderId, string UserId, decimal TotalAmount, DateTimeOffset ConfirmedAt);
public record InventoryStockUpdatedEvent(
    string ProductId, int PreviousQuantity, int NewQuantity, DateTimeOffset UpdatedAt);
public record UserDeletionCompletedEvent(
    string UserId, string ServiceName, bool Success, string? Error, DateTimeOffset CompletedAt);
public record PasswordChangedEvent(
    string UserId, DateTimeOffset ChangedAt);

// 発行イベント（Outbox ペイロード用）
public record UserDeletedEventPayload(string UserId, DateTimeOffset DeletedAt);
public record UserProfileUpdatedEventPayload(
    string UserId, List<string> UpdatedFields, DateTimeOffset UpdatedAt);
public record ConsentRevokedEventPayload(
    string UserId, string ConsentType, DateTimeOffset RevokedAt);
public record MemberRankUpdatedEventPayload(
    string UserId, string PreviousRank, string NewRank, decimal PointRate, DateTimeOffset UpdatedAt);
public record UserDeletionNotificationEventPayload(
    string UserId, string Email, DateTimeOffset CompletedAt);
```

### Phase 2 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] `dotnet ef migrations add InitialCreate --project UserManagementService` — マイグレーション生成成功
- [ ] 全 10 エンティティ: `[Table("snake_case")]` + `[Column("snake_case")]` 属性付き
- [ ] 全エンティティ: `CreatedAt` / `UpdatedAt` に `DateTimeOffset` 使用（`DateTime.Now` 禁止）
- [ ] 楽観的ロック: `User`, `Address`, `MemberRank`, `DeletionRequest` エンティティに `[Timestamp] RowVersion` プロパティが追加されていること（設計書 §L 準拠）
- [ ] コレクションナビゲーション: `= []` で初期化（null 防止）
- [ ] AppDbContext: 全 FK 制約（CASCADE 7 件 + SET NULL 1 件）が定義されていること
- [ ] AppDbContext: 全 CHECK 制約（8 件）が定義されていること
- [ ] AppDbContext: 全インデックス（13 件）が定義されていること
- [ ] AppDbContext: `SaveChangesAsync` で `TimeProvider` 使用
- [ ] DTO: 全て `record` 型で定義
- [ ] カスタム例外: 5 クラス定義（`NotFoundException`, `BusinessException`, `UnauthorizedException`, `ForbiddenException`, `ConcurrencyException`）
- [ ] イベント record: 購読 5 件（`UserRegisteredEvent`, `OrderConfirmedEvent`, `InventoryStockUpdatedEvent`, `UserDeletionCompletedEvent`, `PasswordChangedEvent`）+ 発行ペイロード 5 件（`UserDeletedEventPayload`, `UserProfileUpdatedEventPayload`, `ConsentRevokedEventPayload`, `MemberRankUpdatedEventPayload`, `UserDeletionNotificationEventPayload`）が定義されていること
- [ ] `InventoryStockUpdatedEvent` に `PreviousQuantity` フィールドが含まれていること（在庫復活判定: `PreviousQuantity == 0 && NewQuantity > 0`）
- [ ] **TODO/FIXME/HACK コメント残存チェック**: ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック**: テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック**: 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 3: Repository 層実装

### 目的

設計書 §E（Repository インターフェース定義）に基づき、全 9 Repository のインターフェースと EF Core 実装を作成する。Aggregate Root 単位の原則を遵守し、読み取り専用クエリには `AsNoTracking()` を適用する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `UserManagementService/Repositories/Interfaces/IUserRepository.cs` | 作成 | ユーザーリポジトリインターフェース |
| 2 | `UserManagementService/Repositories/Interfaces/IAddressRepository.cs` | 作成 | 住所リポジトリインターフェース |
| 3 | `UserManagementService/Repositories/Interfaces/IWishlistRepository.cs` | 作成 | ウィッシュリストリポジトリインターフェース |
| 4 | `UserManagementService/Repositories/Interfaces/IPreferenceRepository.cs` | 作成 | 設定リポジトリインターフェース |
| 5 | `UserManagementService/Repositories/Interfaces/IActivityRepository.cs` | 作成 | アクティビティリポジトリインターフェース |
| 6 | `UserManagementService/Repositories/Interfaces/IConsentRepository.cs` | 作成 | 同意リポジトリインターフェース |
| 7 | `UserManagementService/Repositories/Interfaces/IDeletionRequestRepository.cs` | 作成 | 削除リクエストリポジトリインターフェース |
| 8 | `UserManagementService/Repositories/Interfaces/IMemberRankRepository.cs` | 作成 | 会員ランクリポジトリインターフェース |
| 9 | `UserManagementService/Repositories/Interfaces/IOutboxEventRepository.cs` | 作成 | Outbox イベントリポジトリインターフェース |
| 10-18 | `UserManagementService/Repositories/*.cs` | 作成 | 各リポジトリの EF Core 実装（9 ファイル） |
| 19 | `UserManagementService/Program.cs` | 更新 | Repository DI 登録（全 9 件 AddScoped） |

### 3.1 Repository インターフェース定義（設計書 §E 準拠）

全インターフェースは設計書 §E のシグネチャを忠実に実装する。全 `async` メソッドに `CancellationToken ct = default` を含める。

### 3.2 UserRepository 実装例

```csharp
public class UserRepository(AppDbContext context) : IUserRepository
{
    public async Task<User?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> FindByIdWithDetailsAsync(string id, CancellationToken ct = default)
        => await context.Users
            .Include(u => u.Preference)
            .Include(u => u.MemberRank)
            .Include(u => u.Addresses)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> FindByEmailAsync(string email, CancellationToken ct = default)
        => await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task<(List<User> Items, int TotalCount)> FindAllAsync(
        int page, int pageSize, string? statusFilter, CancellationToken ct = default)
    {
        var query = context.Users.AsNoTracking().AsQueryable();
        if (statusFilter is not null)
            query = query.Where(u => u.Status == statusFilter);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await context.Users.AddAsync(user, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
```

### 3.3 DeletionRequestRepository 実装例（DSR 固有クエリ）

```csharp
public class DeletionRequestRepository(AppDbContext context) : IDeletionRequestRepository
{
    public async Task<DeletionRequest?> FindPendingByUserIdAsync(
        string userId, CancellationToken ct = default)
        => await context.DeletionRequests
            .FirstOrDefaultAsync(d => d.UserId == userId
                && d.Status == DeletionRequestStatus.Pending, ct);

    public async Task<List<DeletionRequest>> FindExpiredGracePeriodAsync(
        CancellationToken ct = default)
        => await context.DeletionRequests
            .Where(d => d.Status == DeletionRequestStatus.Pending
                && d.GracePeriodEndsAt <= DateTimeOffset.UtcNow)
            .ToListAsync(ct);

    public async Task<List<DeletionRequest>> FindTimedOutProcessingAsync(
        TimeSpan timeout, CancellationToken ct = default)
        => await context.DeletionRequests
            .Where(d => d.Status == DeletionRequestStatus.Processing
                && d.RequestedAt.Add(timeout) <= DateTimeOffset.UtcNow)
            .ToListAsync(ct);

    // ... 他メソッド省略
}
```

### Phase 3 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] 全 9 Repository インターフェース: 全 async メソッドに `CancellationToken ct = default` あり
- [ ] 全 9 Repository 実装: primary constructor で `AppDbContext` を注入
- [ ] 読み取り専用クエリ: `AsNoTracking()` 適用
- [ ] ページネーション: `FindAllAsync` で `Skip/Take` 使用
- [ ] Program.cs: 全 9 Repository が `AddScoped` で登録されていること
- [ ] 禁止事項: 異なる Aggregate のクエリが 1 つの Repository に混在していないこと
- [ ] **TODO/FIXME/HACK コメント残存チェック**: ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック**: テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック**: 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 4: Service 層実装

### 目的

設計書 §F（Service インターフェース定義）および §14（サービス実装例）に基づき、全 9 Service のインターフェースと実装を作成する。ビジネスロジックは Service 層に集約し、Endpoints から Repository を直接呼び出さない。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `UserManagementService/Services/Interfaces/IUserService.cs` | 作成 | |
| 2 | `UserManagementService/Services/Interfaces/IAddressService.cs` | 作成 | |
| 3 | `UserManagementService/Services/Interfaces/IWishlistService.cs` | 作成 | |
| 4 | `UserManagementService/Services/Interfaces/IPreferenceService.cs` | 作成 | |
| 5 | `UserManagementService/Services/Interfaces/IActivityService.cs` | 作成 | |
| 6 | `UserManagementService/Services/Interfaces/IConsentService.cs` | 作成 | |
| 7 | `UserManagementService/Services/Interfaces/IDsrService.cs` | 作成 | |
| 8 | `UserManagementService/Services/Interfaces/IMemberRankService.cs` | 作成 | |
| 9 | `UserManagementService/Services/Interfaces/IEventPublisherService.cs` | 作成 | |
| 10-18 | `UserManagementService/Services/*.cs` | 作成 | 各 Service の実装（9 ファイル） |
| 19 | `UserManagementService/Validators/*.cs` | 作成 | FluentValidation バリデーター（6 ファイル） |
| 20 | `UserManagementService/Program.cs` | 更新 | Service DI 登録（全 9 件 AddScoped）+ FluentValidation 登録 |

### 4.1 Service インターフェース一覧（設計書 §F 準拠）

| サービス | 主要メソッド | 責務 |
|---------|------------|------|
| `IUserService` | `GetByIdAsync`, `UpdateProfileAsync`, `InitializeProfileAsync`, `UpdateStatusAsync`, `SetProcessingRestrictionAsync`, `GetAllAsync` | プロファイル管理 |
| `IAddressService` | `GetByUserIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync` | 住所管理（最大 10 件制限） |
| `IWishlistService` | `GetByUserIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `AddItemAsync`, `RemoveItemAsync`, `MoveItemToCartAsync`, `ProcessRestockNotificationAsync` | ウィッシュリスト管理 |
| `IPreferenceService` | `GetByUserIdAsync`, `UpdateAsync`, `InitializeAsync` | ユーザー設定管理 |
| `IActivityService` | `GetByUserIdAsync`, `RecordAsync` | アクティビティ記録 |
| `IConsentService` | `GetByUserIdAsync`, `UpdateAsync` | 同意管理（GDPR） |
| `IDsrService` | `CreateDeletionRequestAsync`, `GetDeletionRequestAsync`, `CancelDeletionRequestAsync`, `ProcessExpiredGracePeriodRequestsAsync`, `HandleDeletionCompletedAsync` | GDPR/DSR 処理 |
| `IMemberRankService` | `GetByUserIdAsync`, `InitializeAsync`, `AddPurchaseAmountAsync`, `EvaluateAllRanksAsync` | 会員ランク管理 |
| `IEventPublisherService` | `PublishProfileUpdatedAsync`, `PublishUserDeletedAsync`, `PublishConsentRevokedAsync`, `PublishMemberRankUpdatedAsync`, `PublishDeletionNotificationAsync` | Outbox イベント発行 |

### 4.2 UserService 実装（設計書 §14 準拠）

```csharp
public class UserService(
    IUserRepository userRepository,
    IEventPublisherService eventPublisher,
    ILogger<UserService> logger) : IUserService
{
    public async Task<UserDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var user = await userRepository.FindByIdAsync(id, ct);
        return user is null ? null : MapToDto(user);
    }

    public async Task<UserDto> UpdateProfileAsync(
        string id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await userRepository.FindByIdAsync(id, ct)
            ?? throw new NotFoundException($"ユーザーが見つかりません (ID: {id})");

        var updatedFields = new List<string>();
        if (request.FirstName is not null) { user.FirstName = request.FirstName; updatedFields.Add("firstName"); }
        if (request.LastName is not null) { user.LastName = request.LastName; updatedFields.Add("lastName"); }
        if (request.PhoneNumber is not null) { user.PhoneNumber = request.PhoneNumber; updatedFields.Add("phoneNumber"); }
        if (request.BirthDate is not null) { user.BirthDate = request.BirthDate; updatedFields.Add("birthDate"); }

        await eventPublisher.PublishProfileUpdatedAsync(user.Id, ct);
        await userRepository.SaveChangesAsync(ct);

        logger.LogInformation("ユーザープロファイルが更新されました: {UserId}", user.Id);
        return MapToDto(user);
    }

    public async Task InitializeProfileAsync(
        UserRegisteredEvent @event, CancellationToken ct = default)
    {
        var user = new User
        {
            Id = @event.UserId,
            Email = @event.Email,
            FirstName = @event.FirstName,
            LastName = @event.LastName,
            Status = UserStatus.PendingVerification
        };

        await userRepository.AddAsync(user, ct);
        await userRepository.SaveChangesAsync(ct);
        logger.LogInformation("ユーザープロファイルが初期化されました: {UserId}", user.Id);
    }

    private static UserDto MapToDto(User user) =>
        new(user.Id, user.Email, user.FirstName, user.LastName,
            user.PhoneNumber, user.BirthDate, user.Status,
            user.ProcessingRestricted, user.LastLoginAt, user.CreatedAt);
}
```

### 4.3 MemberRankService 実装（ランク判定ロジック）

```csharp
public class MemberRankService(
    IMemberRankRepository memberRankRepository,
    IEventPublisherService eventPublisher,
    ILogger<MemberRankService> logger) : IMemberRankService
{
    public async Task AddPurchaseAmountAsync(
        string userId, decimal amount, CancellationToken ct = default)
    {
        var rank = await memberRankRepository.FindByUserIdAsync(userId, ct)
            ?? throw new NotFoundException($"会員ランクが見つかりません (UserId: {userId})");

        rank.AnnualPurchaseAmount += amount;

        // リアルタイム昇格判定（spec.md L2609 準拠）
        var previousRank = rank.CurrentRank;
        var newRank = EvaluateRank(rank.AnnualPurchaseAmount);
        if (IsPromotion(rank.CurrentRank, newRank))
        {
            rank.CurrentRank = newRank;
            rank.PointRate = GetPointRate(newRank);
            rank.RankUpdatedAt = DateTimeOffset.UtcNow;
            logger.LogInformation("会員ランク昇格: {UserId}, {OldRank} → {NewRank}",
                userId, previousRank, newRank);

            // member-rank.updated イベント発行（設計書 §5 / §M 準拠）
            await eventPublisher.PublishMemberRankUpdatedAsync(
                userId, previousRank, newRank, rank.PointRate, ct);
        }

        await memberRankRepository.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 年次バッチ（4月1日実行）で全ユーザーのランクを再評価する（設計書 §J 準拠）
    /// - previousYearAmount に前年度の annualPurchaseAmount をコピー
    /// - annualPurchaseAmount を 0 にリセット
    /// - 降格制限: 最大 1 ランクのみ降格（PLATINUM → GOLD は可、PLATINUM → SILVER は不可）
    /// - PLATINUM 降格猶予: 前年度 250,000 円以上なら PLATINUM 維持
    /// </summary>
    public async Task EvaluateAllRanksAsync(CancellationToken ct = default)
    {
        var evaluationDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var ranks = await memberRankRepository.FindAllForEvaluationAsync(evaluationDate, ct);

        foreach (var rank in ranks)
        {
            var previousRank = rank.CurrentRank;
            rank.PreviousYearAmount = rank.AnnualPurchaseAmount;
            rank.AnnualPurchaseAmount = 0;

            var (newRank, newPointRate) = EvaluateAnnual(rank.CurrentRank, rank.PreviousYearAmount);

            if (newRank != rank.CurrentRank)
            {
                rank.CurrentRank = newRank;
                rank.PointRate = newPointRate;
                rank.RankUpdatedAt = DateTimeOffset.UtcNow;
                logger.LogInformation("年次ランク評価: {UserId}, {OldRank} → {NewRank}",
                    rank.UserId, previousRank, newRank);

                await eventPublisher.PublishMemberRankUpdatedAsync(
                    rank.UserId, previousRank, newRank, newPointRate, ct);
            }

            rank.NextEvaluationDate = evaluationDate.AddYears(1);
        }

        await memberRankRepository.SaveChangesAsync(ct);
        logger.LogInformation("年次ランク評価完了: {Count} 件処理", ranks.Count);
    }

    private static string EvaluateRank(decimal annualAmount) => annualAmount switch
    {
        >= 300_000m => MemberRankLevel.Platinum,
        >= 100_000m => MemberRankLevel.Gold,
        >= 50_000m => MemberRankLevel.Silver,
        _ => MemberRankLevel.Bronze
    };

    private static decimal GetPointRate(string rank) => rank switch
    {
        MemberRankLevel.Platinum => 0.07m,
        MemberRankLevel.Gold => 0.05m,
        MemberRankLevel.Silver => 0.03m,
        _ => 0.01m
    };

    /// <summary>
    /// 年次バッチでの降格判定（設計書 §J 準拠: 1 ランクのみ降格、PLATINUM 猶予あり）
    /// </summary>
    private static (string Rank, decimal PointRate) EvaluateAnnual(
        string currentRank, decimal previousYearAmount)
    {
        // PLATINUM 降格猶予: 前年度 250,000 円以上なら維持
        if (currentRank == MemberRankLevel.Platinum && previousYearAmount >= 250_000m)
            return (MemberRankLevel.Platinum, 0.07m);

        var newRank = EvaluateRank(previousYearAmount);
        var newPointRate = GetPointRate(newRank);

        // 1 ランクのみ降格制限
        var currentOrder = GetRankOrder(currentRank);
        var newOrder = GetRankOrder(newRank);

        if (currentOrder - newOrder > 1)
        {
            // 最大 1 ランク降格
            var demotedRank = GetRankByOrder(currentOrder - 1);
            return (demotedRank, GetPointRate(demotedRank));
        }

        return (newRank, newPointRate);
    }

    private static bool IsPromotion(string current, string next) =>
        GetRankOrder(next) > GetRankOrder(current);

    private static int GetRankOrder(string rank) => rank switch
    {
        MemberRankLevel.Bronze => 1,
        MemberRankLevel.Silver => 2,
        MemberRankLevel.Gold => 3,
        MemberRankLevel.Platinum => 4,
        _ => 0
    };

    private static string GetRankByOrder(int order) => order switch
    {
        1 => MemberRankLevel.Bronze,
        2 => MemberRankLevel.Silver,
        3 => MemberRankLevel.Gold,
        4 => MemberRankLevel.Platinum,
        _ => MemberRankLevel.Bronze
    };
}
```

### 4.4 EventPublisherService 実装（Outbox パターン）

```csharp
public class EventPublisherService(
    IOutboxEventRepository outboxRepository,
    ILogger<EventPublisherService> logger) : IEventPublisherService
{
    public async Task PublishProfileUpdatedAsync(string userId, CancellationToken ct = default)
    {
        var payload = new UserProfileUpdatedEventPayload(
            userId, ["firstName", "lastName", "phoneNumber", "birthDate"],
            DateTimeOffset.UtcNow);

        var outboxEvent = new OutboxEvent
        {
            EventType = "user.profile-updated",
            AggregateId = userId,
            Payload = JsonSerializer.Serialize(payload)
        };
        await outboxRepository.AddAsync(outboxEvent, ct);
        logger.LogInformation("Outbox イベント追加: {EventType}, {UserId}",
            outboxEvent.EventType, userId);
    }

    public async Task PublishUserDeletedAsync(string userId, CancellationToken ct = default)
    {
        var payload = new UserDeletedEventPayload(userId, DateTimeOffset.UtcNow);
        var outboxEvent = new OutboxEvent
        {
            EventType = "user.deleted",
            AggregateId = userId,
            Payload = JsonSerializer.Serialize(payload)
        };
        await outboxRepository.AddAsync(outboxEvent, ct);
    }

    public async Task PublishConsentRevokedAsync(
        string userId, string consentType, CancellationToken ct = default)
    {
        var payload = new ConsentRevokedEventPayload(userId, consentType, DateTimeOffset.UtcNow);
        var outboxEvent = new OutboxEvent
        {
            EventType = "consent.revoked",
            AggregateId = userId,
            Payload = JsonSerializer.Serialize(payload)
        };
        await outboxRepository.AddAsync(outboxEvent, ct);
    }

    /// <summary>
    /// member-rank.updated イベントを Outbox に追加（設計書 §5 / §M 準拠）
    /// PointService にランク変更を通知する
    /// </summary>
    public async Task PublishMemberRankUpdatedAsync(
        string userId, string previousRank, string newRank, decimal pointRate,
        CancellationToken ct = default)
    {
        var payload = new MemberRankUpdatedEventPayload(
            userId, previousRank, newRank, pointRate, DateTimeOffset.UtcNow);
        var outboxEvent = new OutboxEvent
        {
            EventType = "member-rank.updated",
            AggregateId = userId,
            Payload = JsonSerializer.Serialize(payload)
        };
        await outboxRepository.AddAsync(outboxEvent, ct);
        logger.LogInformation("Outbox イベント追加: {EventType}, {UserId}, {PreviousRank} → {NewRank}",
            outboxEvent.EventType, userId, previousRank, newRank);
    }

    /// <summary>
    /// user.deletion.notification イベントを Outbox に追加（設計書 §5 準拠）
    /// GDPR Art.12(3) — DSR 完了後に MailSendService 経由で削除完了通知を送信
    /// </summary>
    public async Task PublishDeletionNotificationAsync(
        string userId, string email, CancellationToken ct = default)
    {
        var payload = new UserDeletionNotificationEventPayload(
            userId, email, DateTimeOffset.UtcNow);
        var outboxEvent = new OutboxEvent
        {
            EventType = "user.deletion.notification",
            AggregateId = userId,
            Payload = JsonSerializer.Serialize(payload)
        };
        await outboxRepository.AddAsync(outboxEvent, ct);
        logger.LogInformation("Outbox イベント追加: {EventType}, {UserId}",
            outboxEvent.EventType, userId);
    }
}
```

### 4.5 FluentValidation バリデーター（設計書 §C 準拠）

設計書 §C に定義された 6 バリデーターを全て実装:

| バリデーター | 対象 DTO | 主なルール |
|------------|---------|-----------|
| `UpdateUserRequestValidator` | `UpdateUserRequest` | FirstName/LastName 100 文字以下、電話番号正規表現、誕生日は過去日 |
| `CreateAddressRequestValidator` | `CreateAddressRequest` | 住所種別 SHIPPING/BILLING、郵便番号正規表現、47 都道府県バリデーション |
| `UpdateAddressRequestValidator` | `UpdateAddressRequest` | CreateAddress と同等の任意フィールドバリデーション |
| `CreateWishlistRequestValidator` | `CreateWishlistRequest` | 名前 1〜100 文字 |
| `UpdatePreferenceRequestValidator` | `UpdatePreferenceRequest` | 言語 ja/en、通貨 JPY/USD |
| `ConsentUpdateRequestValidator` | `ConsentUpdateRequest` | 同意種別 4 種、PolicyVersion > 0 |

### Phase 4 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] 全 9 Service インターフェース: 全 async メソッドに `CancellationToken ct = default` あり
- [ ] 全 9 Service 実装: primary constructor で依存関係を注入
- [ ] UserService: `InitializeProfileAsync` — `user.registered` イベントからプロファイル初期化
- [ ] MemberRankService: ランク判定ロジック（BRONZE/SILVER 50k/GOLD 100k/PLATINUM 300k 閾値 — 設計書 §J 準拠）
- [ ] MemberRankService: リアルタイム昇格判定（購入確定時点で閾値超過なら即時昇格）
- [ ] MemberRankService: 年次降格評価 `EvaluateAllRanksAsync` — 1 ランク降格制限 + PLATINUM 降格猶予（前年購入額 ≥ 300,000×83% で維持）
- [ ] MemberRankService: ランク変更時に `IEventPublisherService.PublishMemberRankUpdatedAsync` 呼び出し
- [ ] EventPublisherService: Outbox テーブルへのイベント書き込み（5 イベント: user.deleted, user.profile-updated, consent.revoked, member-rank.updated, user.deletion.notification）
- [ ] DsrService: 14 日間猶予期間、キャンセル機能、削除完了集約
- [ ] FluentValidation: 全 6 バリデーター定義
- [ ] Program.cs: 全 9 Service が `AddScoped` で登録 + `AddValidatorsFromAssemblyContaining`
- [ ] 禁止事項: `Console.WriteLine` なし、PII ログ出力なし、`DateTime.Now` なし
- [ ] **TODO/FIXME/HACK コメント残存チェック**: ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック**: テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック**: 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 5: Endpoints 実装

### 目的

設計書 §4（API 設計）および §G（Endpoint 実装）に基づき、全 9 Endpoint クラスを Minimal API パターンで実装する。REST 原則に従い、全エンドポイントに認可設定を明示する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `UserManagementService/Endpoints/UserEndpoints.cs` | 作成 | ユーザープロファイル API（3 エンドポイント） |
| 2 | `UserManagementService/Endpoints/AddressEndpoints.cs` | 作成 | 住所管理 API（4 エンドポイント） |
| 3 | `UserManagementService/Endpoints/WishlistEndpoints.cs` | 作成 | ウィッシュリスト API（6 エンドポイント） |
| 4 | `UserManagementService/Endpoints/PreferenceEndpoints.cs` | 作成 | ユーザー設定 API（2 エンドポイント） |
| 5 | `UserManagementService/Endpoints/ActivityEndpoints.cs` | 作成 | アクティビティ API（2 エンドポイント） |
| 6 | `UserManagementService/Endpoints/ConsentEndpoints.cs` | 作成 | 同意管理 API（3 エンドポイント） |
| 7 | `UserManagementService/Endpoints/DsrEndpoints.cs` | 作成 | GDPR/DSR API（4 エンドポイント） |
| 8 | `UserManagementService/Endpoints/MemberRankEndpoints.cs` | 作成 | 会員ランク API（1 エンドポイント） |
| 9 | `UserManagementService/Endpoints/AdminUserEndpoints.cs` | 作成 | 管理者向け API（3 エンドポイント） |
| 10 | `UserManagementService/Program.cs` | 更新 | 全 Endpoint マッピング登録 |

### 5.1 全 API エンドポイント一覧（設計書 §4 完全準拠）

#### ユーザープロファイル API（UserEndpoints）

| # | メソッド | パス | 認可 | ハンドラ |
|---|--------|------|------|---------|
| 1 | GET | `/api/v1/users/{id}` | 要認証（本人 or Admin） | `GetUserById` |
| 2 | PUT | `/api/v1/users/{id}` | 要認証（本人 or Admin） | `UpdateUser` |
| 3 | GET | `/api/v1/users/me` | 要認証 | `GetCurrentUser` |

#### 住所管理 API（AddressEndpoints）

| # | メソッド | パス | 認可 | ハンドラ |
|---|--------|------|------|---------|
| 4 | GET | `/api/v1/users/{userId}/addresses` | 要認証（本人 or Admin） | `GetAddresses` |
| 5 | POST | `/api/v1/users/{userId}/addresses` | 要認証（本人） | `CreateAddress` |
| 6 | PUT | `/api/v1/users/{userId}/addresses/{id}` | 要認証（本人） | `UpdateAddress` |
| 7 | DELETE | `/api/v1/users/{userId}/addresses/{id}` | 要認証（本人） | `DeleteAddress` |

#### ウィッシュリスト API（WishlistEndpoints）

| # | メソッド | パス | 認可 | ハンドラ |
|---|--------|------|------|---------|
| 8 | GET | `/api/v1/users/{userId}/wishlists` | 要認証（本人） | `GetWishlists` |
| 9 | POST | `/api/v1/users/{userId}/wishlists` | 要認証（本人） | `CreateWishlist` |
| 10 | PUT | `/api/v1/users/{userId}/wishlists/{id}` | 要認証（本人） | `UpdateWishlist` |
| 11 | DELETE | `/api/v1/users/{userId}/wishlists/{id}` | 要認証（本人） | `DeleteWishlist` |
| 12 | POST | `/api/v1/users/{userId}/wishlists/{id}/items` | 要認証（本人） | `AddItem` |
| 13 | POST | `/api/v1/users/{userId}/wishlists/{id}/items/{itemId}/cart` | 要認証（本人） | `MoveItemToCart`（設計書 §G / spec.md L1542 準拠: PaymentCartService の POST /api/v1/cart/items を呼び出し、成功後にウィッシュリストから削除） |
| 14 | DELETE | `/api/v1/users/{userId}/wishlists/{id}/items/{itemId}` | 要認証（本人） | `RemoveItem` |

#### ユーザー設定 API（PreferenceEndpoints）

| # | メソッド | パス | 認可 | ハンドラ |
|---|--------|------|------|---------|
| 15 | GET | `/api/v1/users/{id}/preferences` | 要認証（本人 or Admin） | `GetPreferences` |
| 16 | PUT | `/api/v1/users/{id}/preferences` | 要認証（本人） | `UpdatePreferences` |

#### ユーザーアクティビティ API（ActivityEndpoints）

| # | メソッド | パス | 認可 | ハンドラ |
|---|--------|------|------|---------|
| 17 | GET | `/api/v1/users/{id}/activities` | 要認証（本人 or Admin） | `GetActivities` |
| 18 | GET | `/api/v1/users/me/activities` | 要認証 | `GetMyActivities` |

#### 同意管理 API（ConsentEndpoints）

| # | メソッド | パス | 認可 | ハンドラ |
|---|--------|------|------|---------|
| 19 | GET | `/api/v1/users/{userId}/consents` | 要認証（本人） | `GetConsents` |
| 20 | PUT | `/api/v1/users/{userId}/consents` | 要認証（本人） | `UpdateConsent` |
| 21 | POST | `/api/v1/anonymous-consents` | 不要（AllowAnonymous） | `RecordAnonymousConsent` |

#### GDPR/DSR API（DsrEndpoints）

| # | メソッド | パス | 認可 | ハンドラ |
|---|--------|------|------|---------|
| 22 | POST | `/api/v1/users/{userId}/deletion-request` | 要認証（本人 or Admin） | `CreateDeletionRequest` |
| 23 | GET | `/api/v1/users/{userId}/deletion-request` | 要認証（本人 or Admin） | `GetDeletionRequest` |
| 24 | POST | `/api/v1/users/{userId}/deletion-request/cancel` | 要認証（本人） | `CancelDeletionRequest` |
| 25 | POST | `/api/v1/users/{userId}/data-export` | 要認証（本人） | `RequestDataExport` |

#### 会員ランク API（MemberRankEndpoints）

| # | メソッド | パス | 認可 | ハンドラ |
|---|--------|------|------|---------|
| 26 | GET | `/api/v1/users/{userId}/member-rank` | 要認証（本人 or Admin） | `GetMemberRank` |

#### 管理者向け API（AdminUserEndpoints）

| # | メソッド | パス | 認可 | ハンドラ |
|---|--------|------|------|---------|
| 27 | GET | `/api/v1/admin/users` | 要認証（Admin） | `GetAllUsers` |
| 28 | PUT | `/api/v1/admin/users/{id}/status` | 要認証（Admin） | `UpdateUserStatus`（adminId を ClaimsPrincipal から抽出し、UserActivity に ADMIN_STATUS_CHANGED 記録） |
| 29 | PUT | `/api/v1/admin/users/{id}/processing-restriction` | 要認証（Admin） | `UpdateProcessingRestriction`（adminId を ClaimsPrincipal から抽出し、UserActivity に ADMIN_PROCESSING_RESTRICTION_SET/REMOVED 記録） |

### 5.2 Endpoint 実装パターン（IDOR 防止付き）

```csharp
public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users")
            .WithTags("ユーザープロファイル")
            .WithOpenApi();

        group.MapGet("/me", GetCurrentUser)
            .RequireAuthorization()
            .WithName("GetCurrentUser");
        group.MapGet("/{id}", GetUserById)
            .RequireAuthorization()
            .WithName("GetUserById");
        group.MapPut("/{id}", UpdateUser)
            .RequireAuthorization()
            .WithName("UpdateUser");
    }

    private static async Task<IResult> GetUserById(
        string id,
        ClaimsPrincipal user,
        IUserService userService,
        CancellationToken ct)
    {
        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        var isAdmin = user.IsInRole("Admin");

        if (!isAdmin && authenticatedUserId != id)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");

        return await userService.GetByIdAsync(id, ct) is { } profile
            ? Results.Ok(profile)
            : Results.NotFound();
    }

    private static async Task<IResult> GetCurrentUser(
        ClaimsPrincipal user,
        IUserService userService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        return await userService.GetByIdAsync(userId, ct) is { } profile
            ? Results.Ok(profile)
            : Results.NotFound();
    }

    private static async Task<IResult> UpdateUser(
        string id,
        [FromBody] UpdateUserRequest request,
        ClaimsPrincipal user,
        IValidator<UpdateUserRequest> validator,
        IUserService userService,
        CancellationToken ct)
    {
        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        var isAdmin = user.IsInRole("Admin");

        if (!isAdmin && authenticatedUserId != id)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");

        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var updated = await userService.UpdateProfileAsync(id, request, ct);
        return Results.Ok(updated);
    }
}
```

### 5.3 AdminUserEndpoints 実装（管理者専用）

```csharp
public static class AdminUserEndpoints
{
    public static void MapAdminUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/users")
            .WithTags("管理者ユーザー管理")
            .RequireAuthorization("AdminOnly")
            .WithOpenApi();

        group.MapGet("/", GetAllUsers).WithName("GetAllUsers");
        group.MapPut("/{id}/status", UpdateUserStatus).WithName("UpdateUserStatus");
        group.MapPut("/{id}/processing-restriction", UpdateProcessingRestriction)
            .WithName("UpdateProcessingRestriction");
    }

    private static async Task<IResult> GetAllUsers(
        [AsParameters] UserQueryParams query,
        IUserService userService,
        CancellationToken ct)
    {
        var (items, totalCount) = await userService.GetAllAsync(
            query.Page, query.PageSize, query.Status, ct);
        return Results.Ok(new { Items = items, TotalCount = totalCount,
            Page = query.Page, PageSize = query.PageSize });
    }

    private static async Task<IResult> UpdateUserStatus(
        string id,
        [FromBody] UpdateUserStatusRequest request,
        ClaimsPrincipal user,
        IUserService userService,
        IActivityService activityService,
        CancellationToken ct)
    {
        var adminId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();

        await userService.UpdateStatusAsync(id, request.Status, ct);

        // 管理者操作監査ログ記録（設計書 §6 GDPR Art.5(2) 準拠）
        await activityService.RecordAsync(id, "ADMIN_STATUS_CHANGED",
            JsonSerializer.Serialize(new { AdminId = adminId, NewStatus = request.Status }),
            ipAddress: null, deviceInfo: null, ct);

        return Results.NoContent();
    }

    private static async Task<IResult> UpdateProcessingRestriction(
        string id,
        [FromBody] UpdateProcessingRestrictionRequest request,
        ClaimsPrincipal user,
        IUserService userService,
        IActivityService activityService,
        CancellationToken ct)
    {
        var adminId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();

        await userService.SetProcessingRestrictionAsync(
            id, request.ProcessingRestricted, request.RestrictionReason, ct);

        // 管理者操作監査ログ記録（設計書 §6 GDPR Art.5(2) 準拠）
        var activityType = request.ProcessingRestricted
            ? "ADMIN_PROCESSING_RESTRICTION_SET"
            : "ADMIN_PROCESSING_RESTRICTION_REMOVED";
        await activityService.RecordAsync(id, activityType,
            JsonSerializer.Serialize(new { AdminId = adminId, Reason = request.RestrictionReason }),
            ipAddress: null, deviceInfo: null, ct);

        return Results.NoContent();
    }
}

public record UserQueryParams(
    int Page = 1,
    [Range(1, 100)] int PageSize = 20,
    string? Status = null);
```

### 5.4 Program.cs — Endpoint マッピング登録

```csharp
// 7. Endpoint マッピング
app.MapUserEndpoints();
app.MapAddressEndpoints();
app.MapWishlistEndpoints();
app.MapPreferenceEndpoints();
app.MapActivityEndpoints();
app.MapConsentEndpoints();
app.MapDsrEndpoints();
app.MapMemberRankEndpoints();
app.MapAdminUserEndpoints();
```

### Phase 5 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] 全 29 エンドポイントが定義されていること（設計書 §4 と完全一致、MoveItemToCart 含む）
- [ ] 全 Endpoint: `MapGroup` + `WithTags` + `WithOpenApi` 使用
- [ ] 全 Endpoint: `RequireAuthorization()` または `AllowAnonymous()` が明示されていること
- [ ] IDOR 防止: 全ユーザー固有リソースで `ClaimsPrincipal` によるオーナーシップ検証
- [ ] 管理者 API: `RequireAuthorization("AdminOnly")` がグループレベルで設定
- [ ] 管理者 API: `UpdateUserStatus` — `ClaimsPrincipal` から `adminId` 取得 + `ADMIN_STATUS_CHANGED` アクティビティ記録（GDPR Art.5(2) 準拠）
- [ ] 管理者 API: `UpdateProcessingRestriction` — `ADMIN_PROCESSING_RESTRICTION_SET` / `ADMIN_PROCESSING_RESTRICTION_REMOVED` アクティビティ記録
- [ ] 匿名 API: `POST /api/v1/anonymous-consents` に `.AllowAnonymous()` 設定
- [ ] POST 成功: `Results.Created()` + Location ヘッダー
- [ ] DELETE 成功: `Results.NoContent()`（ボディなし）
- [ ] バリデーション: POST/PUT の `[FromBody]` に `IValidator<T>` 適用
- [ ] Endpoint にビジネスロジックが直接記述されていないこと（Service 経由）
- [ ] EF Core エンティティを直接レスポンスとして返していないこと（DTO 経由）
- [ ] **TODO/FIXME/HACK コメント残存チェック**: ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック**: テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック**: 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 6: Kafka イベント連携

### 目的

設計書 §5（イベント設計）に基づき、Outbox パターンによるイベント発行と、5 つの Kafka Consumer（BackgroundService）を実装する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `UserManagementService/BackgroundServices/OutboxPublisher.cs` | 作成 | Outbox テーブルから Kafka 発行（Advisory Lock + 動的バックオフ） |
| 2 | `UserManagementService/BackgroundServices/UserRegisteredConsumer.cs` | 作成 | `user.registered` イベント消費 → プロファイル初期化 |
| 3 | `UserManagementService/BackgroundServices/UserDeletionCompletedConsumer.cs` | 作成 | `user.deletion.completed` イベント消費 → DSR 完了集約 |
| 4 | `UserManagementService/BackgroundServices/OrderConfirmedConsumer.cs` | 作成 | `order.confirmed` イベント消費 → 年間購入額更新 + ランク昇格判定 |
| 5 | `UserManagementService/BackgroundServices/InventoryStockUpdatedConsumer.cs` | 作成 | `inventory.stock_updated` イベント消費 → 在庫復活通知トリガー |
| 6 | `UserManagementService/BackgroundServices/PasswordChangedConsumer.cs` | 作成 | `password.changed` イベント消費 → PASSWORD_CHANGED アクティビティ記録 + `lastLoginAt` リセット + キャッシュ無効化（設計書 §5 準拠） |
| 7 | `UserManagementService/BackgroundServices/DataExportService.cs` | 作成 | GDPR Art.20 データエクスポート処理（JSON 生成 + AES-256 暗号化 + 24 時間ダウンロード URL 発行）（設計書 §3 / §6 準拠） |
| 8 | `UserManagementService/BackgroundServices/DeletionRequestProcessor.cs` | 作成 | 猶予期間経過後の削除処理開始 |
| 9 | `UserManagementService/BackgroundServices/MemberRankEvaluationService.cs` | 作成 | 年次会員ランク評価バッチ |
| 10 | `UserManagementService/BackgroundServices/DsrTimeoutMonitorService.cs` | 作成 | DSR タイムアウト監視 |
| 11 | `UserManagementService/Configurations/KafkaSettings.cs` | 作成 | Kafka 設定クラス（IOptions<T>） |
| 12 | `UserManagementService/Program.cs` | 更新 | Kafka Producer DI + BackgroundService 登録 |

### 6.1 発行イベント一覧（Outbox パターン）

| イベント名 | トピック | ペイロード | トリガー |
|-----------|---------|-----------|---------|
| `user.deleted` | `user.deleted` | `{ userId, deletedAt }` | DSR 猶予期間経過後 |
| `user.profile-updated` | `user.profile-updated` | `{ userId, updatedFields, updatedAt }` | プロファイル更新時 |
| `consent.revoked` | `consent.revoked` | `{ userId, consentType, revokedAt }` | 同意撤回時 |
| `member-rank.updated` | `member-rank.updated` | `{ userId, previousRank, newRank, pointRate, updatedAt }` | ランク昇格/降格時（PointService に通知）（設計書 §5 / §M 準拠） |
| `user.deletion.notification` | `user.deletion.notification` | `{ userId, email, completedAt }` | DSR ステータス COMPLETED 時（GDPR Art.12(3) — MailSendService 経由で削除完了通知）（設計書 §5 準拠） |

### 6.2 購読イベント一覧

| イベント名 | トピック | 発行元 | Consumer | アクション |
|-----------|---------|--------|----------|----------|
| `user.registered` | `user.registered` | AuthService | `UserRegisteredConsumer` | User + UserPreference + MemberRank 作成 |
| `user.deletion.completed` | `user.deletion.completed` | 各サービス | `UserDeletionCompletedConsumer` | DeletionRequest の serviceStatuses 更新 |
| `order.confirmed` | `order.confirmed` | SalesManagementService | `OrderConfirmedConsumer` | annualPurchaseAmount 更新 + ランク昇格判定 |
| `inventory.stock_updated` | `inventory.stock_updated` | InventoryManagementService | `InventoryStockUpdatedConsumer` | WishlistItem の notifyOnRestock による在庫復活通知（`PreviousQuantity == 0 && NewQuantity > 0` で判定） |
| `password.changed` | `password.changed` | AuthService | `PasswordChangedConsumer` | PASSWORD_CHANGED アクティビティ記録 + `lastLoginAt` リセット + `updatedAt` 更新（キャッシュ無効化トリガー）（設計書 §5 準拠） |

### 6.3 OutboxPublisher 実装（設計書 §14 準拠）

```csharp
public class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private static readonly TimeSpan MinDelay = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var currentDelay = MinDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var lockAcquired = await context.Database
                    .ExecuteSqlRawAsync(
                        "SELECT pg_try_advisory_lock(hashtext('outbox_publisher'))",
                        stoppingToken) > 0;

                if (!lockAcquired)
                {
                    await Task.Delay(MaxDelay, stoppingToken);
                    continue;
                }

                try
                {
                    var events = await context.OutboxEvents
                        .Where(e => e.Status == OutboxEventStatus.Pending)
                        .OrderBy(e => e.CreatedAt)
                        .Take(100)
                        .ToListAsync(stoppingToken);

                    if (events.Count == 0)
                    {
                        currentDelay = TimeSpan.Min(currentDelay * 2, MaxDelay);
                        await Task.Delay(currentDelay, stoppingToken);
                        continue;
                    }

                    currentDelay = MinDelay;

                    foreach (var @event in events)
                    {
                        var message = new Message<string, string>
                        {
                            Key = @event.AggregateId,
                            Value = @event.Payload
                        };
                        await producer.ProduceAsync(@event.EventType, message, stoppingToken);
                        @event.Status = OutboxEventStatus.Published;
                        @event.PublishedAt = DateTimeOffset.UtcNow;
                    }

                    await context.SaveChangesAsync(stoppingToken);
                }
                finally
                {
                    await context.Database
                        .ExecuteSqlRawAsync(
                            "SELECT pg_advisory_unlock(hashtext('outbox_publisher'))",
                            stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Outbox パブリッシャーでエラーが発生しました: {Message}", ex.Message);
                await Task.Delay(MaxDelay, stoppingToken);
            }
        }
    }
}
```

### 6.4 UserRegisteredConsumer 実装

```csharp
public class UserRegisteredConsumer(
    IConsumer<string, string> consumer,
    IServiceScopeFactory scopeFactory,
    ILogger<UserRegisteredConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        consumer.Subscribe("user.registered");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var @event = JsonSerializer.Deserialize<UserRegisteredEvent>(result.Message.Value);
                if (@event is not null)
                {
                    using var scope = scopeFactory.CreateScope();
                    var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
                    var preferenceService = scope.ServiceProvider.GetRequiredService<IPreferenceService>();
                    var memberRankService = scope.ServiceProvider.GetRequiredService<IMemberRankService>();

                    await userService.InitializeProfileAsync(@event, stoppingToken);
                    await preferenceService.InitializeAsync(@event.UserId, stoppingToken);
                    await memberRankService.InitializeAsync(@event.UserId, stoppingToken);

                    logger.LogInformation("ユーザー登録イベント処理完了: {UserId}", @event.UserId);
                }
                consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume エラー: {Topic}", ex.ConsumerRecord?.Topic);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "イベント処理エラー: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
```

### 6.5 BackgroundService 一覧

| サービス | Advisory Lock | ポーリング間隔 | 責務 |
|---------|-------------|-------------|------|
| `OutboxPublisher` | `hashtext('outbox_publisher')` | 動的バックオフ 100ms〜5s | Outbox → Kafka 発行 |
| `MemberRankEvaluationService` | `hashtext('member_rank_eval')` | 1 時間（4/1 のみ実行） | 年次ランク評価バッチ |
| `DsrTimeoutMonitorService` | — | 1 時間 | DSR 24 時間タイムアウト検出（最大 3 リトライ） |
| `DeletionRequestProcessor` | — | 1 時間 | 猶予期間経過した PENDING → PROCESSING 遷移 |

### Phase 6 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] OutboxPublisher: Advisory Lock（`hashtext('outbox_publisher')`）使用
- [ ] OutboxPublisher: 動的バックオフ（100ms〜5s）— 固定間隔 1 秒は禁止
- [ ] OutboxPublisher: `IServiceScopeFactory` で Scoped サービス取得
- [ ] OutboxPublisher: `stoppingToken` を全下位呼び出しに伝搬
- [ ] 全 Consumer: `BackgroundService` を継承
- [ ] 全 Consumer: `catch (ConsumeException)` + バックオフ
- [ ] UserRegisteredConsumer: User + UserPreference + MemberRank を初期化
- [ ] OrderConfirmedConsumer: `MemberRankService.AddPurchaseAmountAsync` 呼び出し
- [ ] InventoryStockUpdatedConsumer: `WishlistService.ProcessRestockNotificationAsync` 呼び出し（`PreviousQuantity == 0 && NewQuantity > 0` で在庫復活を判定）
- [ ] PasswordChangedConsumer: PASSWORD_CHANGED アクティビティ記録 + `lastLoginAt` リセット + `updatedAt` 更新（キャッシュ無効化トリガー）
- [ ] DataExportService: GDPR Art.20 データエクスポート処理（JSON 生成 + AES-256 暗号化 + 24 時間ダウンロード URL 発行）
- [ ] MemberRankEvaluationService: 4 月 1 日のみ実行 + Advisory Lock
- [ ] DsrTimeoutMonitorService: 24 時間タイムアウト + 最大 3 リトライ
- [ ] DeletionRequestProcessor: 猶予期間（14 日）経過チェック
- [ ] Program.cs: Kafka Producer DI 登録 + 全 BackgroundService 登録（PasswordChangedConsumer, DataExportService を含む全 10 件）
- [ ] 発行イベント: 5 件登録（`user.deleted`, `user.profile-updated`, `consent.revoked`, `member-rank.updated`, `user.deletion.notification`）
- [ ] 購読イベント: 5 件消費（`user.registered`, `user.deletion.completed`, `order.confirmed`, `inventory.stock_updated`, `password.changed`）
- [ ] 禁止事項: `Thread.Sleep()` なし（`await Task.Delay()` 使用）
- [ ] **TODO/FIXME/HACK コメント残存チェック**: ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック**: テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック**: 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 7: Redis キャッシュ連携

### 目的

ユーザープロファイルの読み取りパフォーマンスを向上させるため、Redis キャッシュ層を追加する。キャッシュ戦略は Read-Through / Write-Through パターンとする。

> **注記**: spec.md の AppHost 定義では UserManagementService に `.WithReference(redis)` は含まれない。本フェーズは任意拡張であり、将来の要件に応じて実装する。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `UserManagementService/UserManagementService.csproj` | 更新 | `StackExchange.Redis` パッケージ追加 |
| 2 | `UserManagementService/Services/CacheService.cs` | 作成 | Redis キャッシュ操作（ICacheService） |
| 3 | `UserManagementService/Services/Interfaces/ICacheService.cs` | 作成 | キャッシュインターフェース |
| 4 | `UserManagementService/Program.cs` | 更新 | Redis 接続 DI 登録 |

### 7.1 キャッシュ戦略

| キャッシュキー | TTL | 更新タイミング |
|-------------|-----|-------------|
| `user:profile:{userId}` | 30 分 | プロファイル更新時 / `password.changed` イベント受信時に無効化（設計書 §8 準拠） |
| `user:pref:{userId}` | 1 時間 | 設定更新時に無効化 |
| `user:rank:{userId}` | 1 時間 | ランク更新時に無効化 |

> **キャッシュ無効化トリガー（設計書 §8 準拠）**: `password.changed` イベント受信時は `user:profile:{userId}` キャッシュを無効化する。`PasswordChangedConsumer` が `updatedAt` を更新し、キャッシュと DB の整合性を保証する。

### 7.2 ICacheService インターフェース

```csharp
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class;
    Task RemoveAsync(string key, CancellationToken ct = default);
}
```

### Phase 7 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] Redis 接続: `IConnectionMultiplexer` が DI 登録されていること
- [ ] キャッシュ: TTL が設定されていること（無期限キャッシュ禁止）
- [ ] キャッシュ無効化: プロファイル更新時に関連キャッシュが削除されること
- [ ] キャッシュ無効化: `password.changed` イベント受信時に `user:profile:{userId}` キャッシュが無効化されること（設計書 §8 準拠）
- [ ] キャッシュキーパターン: `user:profile:{userId}`, `user:pref:{userId}`, `user:rank:{userId}` が設計書 §8 と一致すること
- [ ] Redis 障害時: キャッシュミス扱いで DB フォールバック（サービス停止しない）
- [ ] 禁止事項: `new HttpClient()` なし
- [ ] **TODO/FIXME/HACK コメント残存チェック**: ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック**: テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック**: 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 8: 認証・認可・セキュリティ

### 目的

設計書 §6（セキュリティ設計）および §H（Program.cs 統合ビュー）に基づき、JWT Bearer 認証、認可ポリシー、セキュリティヘッダー、CORS、グローバル例外ハンドラーを構成する。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `UserManagementService/Infrastructure/Middleware/CorrelationIdMiddleware.cs` | 作成 | Correlation ID ミドルウェア |
| 2 | `UserManagementService/Infrastructure/Middleware/SecurityHeadersMiddleware.cs` | 作成 | セキュリティヘッダーミドルウェア |
| 3 | `UserManagementService/Program.cs` | 更新 | 認証・認可 + ミドルウェアパイプライン統合 |

### 8.1 認証設定

```csharp
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
```

### 8.2 認可ポリシー

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

### 8.3 セキュリティヘッダー

| ヘッダー | 値 |
|---------|---|
| `X-Content-Type-Options` | `nosniff` |
| `X-Frame-Options` | `DENY` |
| `Content-Security-Policy` | `default-src 'self'` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` |

### 8.4 グローバル例外ハンドラー（設計書 §7 / §H 準拠）

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
            NotFoundException e    => TypedResults.Problem(e.Message, statusCode: 404),
            BusinessException e    => TypedResults.Problem(e.Message, statusCode: 422),
            UnauthorizedException  => TypedResults.Problem(statusCode: 401),
            ForbiddenException     => TypedResults.Problem(statusCode: 403),
            ConcurrencyException e => TypedResults.Problem(e.Message, statusCode: 409),
            _                      => TypedResults.Problem("内部エラーが発生しました", statusCode: 500)
        };
        await problem.ExecuteAsync(context);
    });
});
```

### 8.5 ミドルウェアパイプライン順序（厳守 — AGENTS.md §11.3 準拠）

```
┌─────────────────────────────────────────────────┐
│ 1. UseExceptionHandler()                         │ ← 最外層: 全例外をキャッチ
│ 2. UseHsts() + UseHttpsRedirection()             │ ← セキュリティ
│ 3. SecurityHeadersMiddleware                     │ ← セキュリティヘッダー
│ 4. CorrelationIdMiddleware                       │ ← Correlation ID
│ 5. UseSerilogRequestLogging()                    │ ← リクエストログ
│ 6. UseCors()                                     │ ← CORS（認証より前）
│ 7. UseAuthentication()                           │ ← 認証
│    UseAuthorization()                            │ ← 認可（認証の直後）
│ 8. Endpoint マッピング                             │ ← 全 Endpoints + HealthChecks
└─────────────────────────────────────────────────┘
```

### 8.6 PII ログ禁止（設計書 §6 準拠）

ログに出力してはいけない情報:
- `Email`（メールアドレス全文）→ ユーザー ID のみ出力
- `Address`（住所情報全般）
- `PhoneNumber`（電話番号）
- JWT トークン

```csharp
// ❌ 禁止
logger.LogInformation("ユーザー更新: {Email}", user.Email);

// ✅ 正しい
logger.LogInformation("ユーザー更新: {UserId}", user.Id);
```

### Phase 8 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] JWT 認証: `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey` が全て `true`
- [ ] JWT 認証: `ClockSkew = TimeSpan.FromMinutes(5)`
- [ ] 認可: `FallbackPolicy` に `RequireAuthenticatedUser()` 設定
- [ ] 認可: `AdminOnly` ポリシー定義
- [ ] セキュリティヘッダー: 全 5 ヘッダー設定
- [ ] グローバル例外ハンドラー: `UseExceptionHandler()` がパイプライン最上位
- [ ] グローバル例外ハンドラー: RFC 9457 Problem Details 形式
- [ ] グローバル例外ハンドラー: スタックトレースをクライアントに返さない
- [ ] ミドルウェア順序: §8.5 の順序と完全一致
- [ ] `UseAuthentication()` が `UseAuthorization()` の直前
- [ ] `UseCors()` が `UseAuthentication()` の前
- [ ] PII ログ禁止: Email, Address, PhoneNumber がログに出力されないこと
- [ ] `DetailedErrors: false` が設定されていること
- [ ] **TODO/FIXME/HACK コメント残存チェック**: ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック**: テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック**: 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 9: テスト実装

### 目的

設計書の全機能に対するテストを実装する。テスト規約（AGENTS.md §9, `.github/instructions/test-standards.instructions.md`）に従い、`Should_X_When_Y` 命名、AAA パターン、NSubstitute + Shouldly を使用する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `UserManagementService.Tests/UserManagementService.Tests.csproj` | 作成 | テストプロジェクト定義（xUnit + NSubstitute + Shouldly + Testcontainers） |
| 2 | `UserManagementService.Tests/Unit/Services/UserServiceTests.cs` | 作成 | UserService 単体テスト |
| 3 | `UserManagementService.Tests/Unit/Services/AddressServiceTests.cs` | 作成 | AddressService 単体テスト |
| 4 | `UserManagementService.Tests/Unit/Services/WishlistServiceTests.cs` | 作成 | WishlistService 単体テスト |
| 5 | `UserManagementService.Tests/Unit/Services/PreferenceServiceTests.cs` | 作成 | PreferenceService 単体テスト |
| 6 | `UserManagementService.Tests/Unit/Services/ActivityServiceTests.cs` | 作成 | ActivityService 単体テスト |
| 7 | `UserManagementService.Tests/Unit/Services/ConsentServiceTests.cs` | 作成 | ConsentService 単体テスト |
| 8 | `UserManagementService.Tests/Unit/Services/DsrServiceTests.cs` | 作成 | DsrService 単体テスト |
| 9 | `UserManagementService.Tests/Unit/Services/MemberRankServiceTests.cs` | 作成 | MemberRankService 単体テスト（ランク昇格ロジック + 年次バッチ降格ルール: 1 ランク降格制限、PLATINUM 猶予） |
| 10 | `UserManagementService.Tests/Unit/Services/EventPublisherServiceTests.cs` | 作成 | EventPublisherService 単体テスト（`PublishMemberRankUpdatedAsync`, `PublishDeletionNotificationAsync` を含む全 5 メソッド） |
| 11 | `UserManagementService.Tests/Unit/Validators/CreateAddressRequestValidatorTests.cs` | 作成 | バリデーターテスト |
| 12 | `UserManagementService.Tests/Unit/Validators/UpdateUserRequestValidatorTests.cs` | 作成 | バリデーターテスト |
| 13 | `UserManagementService.Tests/Unit/Validators/CreateWishlistRequestValidatorTests.cs` | 作成 | バリデーターテスト |
| 14 | `UserManagementService.Tests/Unit/BackgroundServices/PasswordChangedConsumerTests.cs` | 作成 | PasswordChangedConsumer 単体テスト（PASSWORD_CHANGED アクティビティ記録、lastLoginAt リセット、キャッシュ無効化トリガー） |
| 15 | `UserManagementService.Tests/Unit/BackgroundServices/DataExportServiceTests.cs` | 作成 | DataExportService 単体テスト（JSON スキーマ検証、AES-256 暗号化、24 時間 URL 有効期限） |
| 16 | `UserManagementService.Tests/Integration/Endpoints/UserEndpointsTests.cs` | 作成 | UserEndpoints 統合テスト |
| 17 | `UserManagementService.Tests/Integration/Endpoints/AdminUserEndpointsTests.cs` | 作成 | AdminUserEndpoints 統合テスト（認可検証 + 管理者監査ログ記録の検証） |
| 18 | `UserManagementService.Tests/Integration/Endpoints/DsrEndpointsTests.cs` | 作成 | GDPR/DSR 統合テスト |
| 19 | `UserManagementService.Tests/Integration/Repositories/UserRepositoryTests.cs` | 作成 | DB スライステスト（Testcontainers） |
| 20 | `UserManagementService.Tests/Fixtures/CustomWebApplicationFactory.cs` | 作成 | テスト用 WebApplicationFactory |
| 21 | `UserManagementService.Tests/Fixtures/FakeAuthHandler.cs` | 作成 | テスト用認証ハンドラー |
| 22 | `UserManagementService.Tests/Fixtures/PostgresContainerFixture.cs` | 作成 | Testcontainers 共有フィクスチャ |

### 9.1 テストプロジェクト .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
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
    <ProjectReference Include="..\UserManagementService\UserManagementService.csproj" />
  </ItemGroup>
</Project>
```

### 9.2 UserService 単体テスト例

```csharp
public class UserServiceTests
{
    private readonly IUserRepository _userRepository;
    private readonly IEventPublisherService _eventPublisher;
    private readonly ILogger<UserService> _logger;
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _eventPublisher = Substitute.For<IEventPublisherService>();
        _logger = Substitute.For<ILogger<UserService>>();
        _sut = new UserService(_userRepository, _eventPublisher, _logger);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ReturnUser_When_ValidIdProvided()
    {
        // Arrange
        var user = new User { Id = "user-1", Email = "test@example.com", UserName = "testuser" };
        _userRepository.FindByIdAsync("user-1", default).Returns(user);

        // Act
        var result = await _sut.GetByIdAsync("user-1");

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("user-1");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowNotFoundException_When_UserDoesNotExist()
    {
        // Arrange
        _userRepository.FindByIdAsync("invalid-id", default).Returns((User?)null);

        // Act & Assert
        var act = async () => await _sut.GetByIdAsync("invalid-id");
        var ex = await Should.ThrowAsync<NotFoundException>(act);
        ex.Message.ShouldContain("invalid-id");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PublishProfileUpdatedEvent_When_ProfileUpdated()
    {
        // Arrange
        var user = new User { Id = "user-1", Email = "test@example.com", UserName = "testuser" };
        _userRepository.FindByIdAsync("user-1", default).Returns(user);
        _userRepository.SaveChangesAsync(default).Returns(Task.CompletedTask);

        var request = new UpdateUserRequest("NewName", null, null, null);

        // Act
        await _sut.UpdateProfileAsync("user-1", request);

        // Assert
        await _eventPublisher.Received(1)
            .PublishProfileUpdatedEventAsync("user-1", Arg.Any<List<string>>(), Arg.Any<CancellationToken>());
    }
}
```

### 9.3 MemberRankService テスト（ランク昇格ロジック）

```csharp
public class MemberRankServiceTests
{
    private readonly IMemberRankRepository _memberRankRepository;
    private readonly IEventPublisherService _eventPublisher;
    private readonly ILogger<MemberRankService> _logger;
    private readonly MemberRankService _sut;

    public MemberRankServiceTests()
    {
        _memberRankRepository = Substitute.For<IMemberRankRepository>();
        _eventPublisher = Substitute.For<IEventPublisherService>();
        _logger = Substitute.For<ILogger<MemberRankService>>();
        _sut = new MemberRankService(_memberRankRepository, _eventPublisher, _logger);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(0, MemberRankLevel.Bronze)]
    [InlineData(49_999, MemberRankLevel.Bronze)]
    [InlineData(50_000, MemberRankLevel.Silver)]
    [InlineData(99_999, MemberRankLevel.Silver)]
    [InlineData(100_000, MemberRankLevel.Gold)]
    [InlineData(299_999, MemberRankLevel.Gold)]
    [InlineData(300_000, MemberRankLevel.Platinum)]
    [InlineData(1_000_000, MemberRankLevel.Platinum)]
    public async Task Should_EvaluateCorrectRank_When_PurchaseAmountProvided(
        decimal purchaseAmount, string expectedRank)
    {
        // Arrange
        var memberRank = new MemberRank
        {
            UserId = "user-1",
            CurrentRank = MemberRankLevel.Bronze,
            AnnualPurchaseAmount = purchaseAmount
        };
        _memberRankRepository.FindByUserIdAsync("user-1", default).Returns(memberRank);

        // Act
        var result = await _sut.EvaluateRankAsync("user-1");

        // Assert
        result.CurrentRank.ShouldBe(expectedRank);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_PublishMemberRankUpdatedEvent_When_RankPromoted()
    {
        // Arrange: BRONZE ユーザーに 300,000 円の購入
        // Act: AddPurchaseAmountAsync
        // Assert: PublishMemberRankUpdatedAsync が 1 回呼ばれ、BRONZE → PLATINUM の引数
    }

    // ── 年次バッチ降格テスト（設計書 §J 準拠） ──

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_DemoteByOneRank_When_AnnualEvaluationExceedsOneRankDrop()
    {
        // Arrange: PLATINUM ユーザー、前年度購入額 40,000 円（BRONZE 相当）
        // Act: EvaluateAllRanksAsync
        // Assert: GOLD に降格（1 ランクのみ降格制限）、SILVER/BRONZE にはならない
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_MaintainPlatinum_When_PreviousYearAmountAboveGraceThreshold()
    {
        // Arrange: PLATINUM ユーザー、前年度購入額 250,000 円（猶予閾値 = 300,000 × 83%）
        // Act: EvaluateAllRanksAsync
        // Assert: PLATINUM 維持（降格猶予適用）
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ResetAnnualAmount_When_AnnualEvaluationExecuted()
    {
        // Arrange: 任意のランク、annualPurchaseAmount > 0
        // Act: EvaluateAllRanksAsync
        // Assert: annualPurchaseAmount == 0, previousYearAmount == 旧 annualPurchaseAmount
    }
}
```

### 9.4 GDPR/DSR テスト戦略

```csharp
public class DsrServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_CreateDeletionRequest_When_NoExistingPendingRequest()
    {
        // Arrange: 既存 PENDING リクエストなし
        // Act: CreateDeletionRequestAsync
        // Assert: DeletionRequest.Status == PENDING, GracePeriodEndsAt == 14 日後
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_PendingDeletionRequestAlreadyExists()
    {
        // Arrange: 既存 PENDING リクエストあり
        // Act & Assert: BusinessException
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_CancelDeletionRequest_When_WithinGracePeriod()
    {
        // Arrange: 猶予期間内の PENDING リクエスト
        // Act: CancelDeletionRequestAsync
        // Assert: Status == CANCELLED
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Should_ThrowBusinessException_When_CancelAfterGracePeriod()
    {
        // Arrange: 猶予期間経過後の PROCESSING リクエスト
        // Act & Assert: BusinessException
    }
}
```

### 9.5 統合テスト（WebApplicationFactory）

```csharp
public class UserEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UserEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_Return200_When_GetCurrentUser()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/users/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task Should_Return403_When_AccessOtherUserProfile()
    {
        // Act: 認証済みユーザー "user-1" が "user-2" のプロファイルにアクセス
        var response = await _client.GetAsync("/api/v1/users/user-2");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task Should_Return401_When_NoAuthToken()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();  // 認証トークンなし

        // Act
        var response = await unauthClient.GetAsync("/api/v1/users/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
```

### 9.6 DB スライステスト（Testcontainers）

```csharp
public class UserRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .Build();
    private AppDbContext _context = null!;
    private UserRepository _sut = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        _context = new AppDbContext(options, TimeProvider.System);
        await _context.Database.MigrateAsync();
        _sut = new UserRepository(_context);
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
            Id = Guid.NewGuid().ToString(),
            Email = "test@example.com",
            UserName = "testuser",
            PasswordHash = "hashed",
            Status = UserStatus.Active
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.FindByEmailAsync("test@example.com");

        // Assert
        result.ShouldNotBeNull();
        result.Email.ShouldBe("test@example.com");
    }
}
```

### 9.7 テスト分類と実行コマンド

| カテゴリ | `[Trait]` 値 | 実行コマンド |
|---------|-------------|------------|
| ユニットテスト | `"Category", "Unit"` | `dotnet test --filter "Category=Unit"` |
| 統合テスト | `"Category", "Integration"` | `dotnet test --filter "Category=Integration"` |
| セキュリティテスト | `"Category", "Security"` | `dotnet test --filter "Category=Security"` |
| Repository テスト | `"Category", "Repository"` | `dotnet test --filter "Category=Repository"` |
| 全テスト | — | `dotnet test` |
| カバレッジ計測 | — | `dotnet test --collect:"XPlat Code Coverage"` |

### 9.8 カバレッジ目標

| レイヤー | 目標 | 主要テスト対象 |
|---------|------|-------------|
| Service | 80% 以上 | ビジネスロジック（ランク昇格、DSR 処理、猶予期間計算） |
| Endpoints | 80% 以上 | HTTP ステータスコード、IDOR 防止、バリデーション |
| Repository | 70% 以上 | CRUD 操作、複合クエリ |
| Validators | 90% 以上 | 全バリデーションルール |
| 全体 | 80% 以上 | `dotnet test --collect:"XPlat Code Coverage"` で確認 |

### Phase 9 完了チェックリスト

- [ ] `dotnet test` — 全テスト PASS
- [ ] テストメソッド命名: `Should_X_When_Y` パターン
- [ ] AAA パターン: Arrange / Act / Assert セクション明確
- [ ] モック: NSubstitute 使用（`Substitute.For<T>()`）
- [ ] アサーション: Shouldly 使用（`ShouldBe`, `ShouldNotBeNull`, `ShouldThrowAsync`）
- [ ] `[Trait("Category", "...")]` が全テストに付与されていること
- [ ] CancellationToken テスト: キャンセル時の `OperationCanceledException` 検証
- [ ] MemberRank テスト: 全ランク閾値（BRONZE/SILVER/GOLD/PLATINUM）の境界値テスト（SILVER=50,000, GOLD=100,000, PLATINUM=300,000）
- [ ] MemberRank テスト: 年次バッチ降格テスト — 1 ランク降格制限、PLATINUM 降格猶予（前年 250,000 以上で維持）、annualPurchaseAmount リセット
- [ ] MemberRank テスト: `member-rank.updated` イベント発行の検証（昇格時・降格時）
- [ ] PasswordChangedConsumer テスト: PASSWORD_CHANGED アクティビティ記録、lastLoginAt リセット
- [ ] DataExportService テスト: JSON スキーマ検証、AES-256 暗号化
- [ ] AdminUserEndpoints テスト: 管理者監査ログ（ADMIN_STATUS_CHANGED, ADMIN_PROCESSING_RESTRICTION_SET/REMOVED）の UserActivity 記録検証
- [ ] DSR テスト: 猶予期間内キャンセル / 期間経過後キャンセル拒否
- [ ] セキュリティテスト: 認証なし 401、権限なし 403、IDOR 防止
- [ ] Testcontainers: PostgreSQL 17 イメージ使用
- [ ] アサーションなしテスト: ゼロ件（Critical 違反チェック）
- [ ] カバレッジ: 80% 以上達成確認
- [ ] **TODO/FIXME/HACK コメント残存チェック**: ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック**: テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック**: 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 10: 可観測性（Observability）

### 目的

AGENTS.md §11.2 に基づき、Serilog 構造化ログ、OpenTelemetry（トレーシング + メトリクス）、ヘルスチェックを統合する。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `UserManagementService/Program.cs` | 更新 | Serilog + OpenTelemetry + HealthChecks 統合 |

### 10.1 Serilog 構造化ログ設定

```csharp
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "UserManagementService")
        .WriteTo.Console(new CompactJsonFormatter()));
```

### 10.2 OpenTelemetry 設定

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource("SkiShop.UserManagement.*"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation());
```

### 10.3 ヘルスチェック（PostgreSQL のみ — spec.md 準拠）

```csharp
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("接続文字列が設定されていません");

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql", tags: ["ready"]);

// Liveness: アプリケーション生存確認
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

// Readiness: PostgreSQL 疎通確認
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
```

### 10.5 構造化ログ出力規約

```csharp
// ✅ 正しい: メッセージテンプレート形式
logger.LogInformation("ユーザープロファイル更新: {UserId}", userId);
logger.LogWarning("DSR 猶予期間経過: {DeletionRequestId}", requestId);
logger.LogError(ex, "ランク評価エラー: {UserId}, {Message}", userId, ex.Message);

// ❌ 禁止: 文字列補間
logger.LogInformation($"ユーザー更新: {userId}");

// ❌ 禁止: PII 出力
logger.LogInformation("ユーザー更新: {Email}", email);
```

### Phase 10 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] Serilog: `CompactJsonFormatter` で JSON 構造化ログ出力
- [ ] Serilog: `ServiceName` プロパティ = `"UserManagementService"`
- [ ] OpenTelemetry: ASP.NET Core + HttpClient + EF Core インストルメンテーション
- [ ] ヘルスチェック: `/health`（Liveness）エンドポイント存在
- [ ] ヘルスチェック: `/health/ready`（Readiness）エンドポイント存在 + PostgreSQL チェック
- [ ] ヘルスチェック: 両エンドポイントに `AllowAnonymous()` 設定
- [ ] Correlation ID: `X-Correlation-Id` ヘッダー付与 + Serilog LogContext 連携
- [ ] ログ形式: メッセージテンプレート `{Placeholder}` 使用（文字列補間禁止）
- [ ] PII ログ禁止: Email, Address, PhoneNumber がログに含まれないこと
- [ ] **TODO/FIXME/HACK コメント残存チェック**: ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック**: テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック**: 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 11: Docker・デプロイ

### 目的

AGENTS.md §12.5（Dockerfile 規約）および `.github/instructions/dockerfile-infra.instructions.md` に準拠した Dockerfile の最終化と、.NET Aspire AppHost への統合を行う。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `UserManagementService/Dockerfile` | 更新 | 最終 Dockerfile（マルチステージ + 非 root + ヘルスチェック） |
| 2 | `UserManagementService/.dockerignore` | 確認 | Docker コンテキスト除外設定 |
| 3 | `AppHost/Program.cs` | 更新 | UserManagementService の Aspire 統合 |

### 11.1 最終 Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["UserManagementService/UserManagementService.csproj", "UserManagementService/"]
RUN dotnet restore "UserManagementService/UserManagementService.csproj"
COPY . .
WORKDIR "/src/UserManagementService"
RUN dotnet publish "UserManagementService.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN groupadd -r skishop && useradd -r -g skishop -d /app -s /sbin/nologin skishop
COPY --from=build --chown=skishop:skishop /app/publish .

USER skishop

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "UserManagementService.dll"]
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
```

### 11.3 .NET Aspire AppHost 統合（spec.md 準拠）

```csharp
// AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var userDb = postgres.AddDatabase("userdb");
var kafka = builder.AddKafka("kafka");

var userService = builder
    .AddProject<Projects.UserManagementService>("user-management-service")
    .WithReference(userDb)
    .WithReference(kafka);

builder.Build().Run();
```

> **重要**: spec.md の AppHost 定義では UserManagementService に `.WithReference(redis)` は含まれない。Redis 統合は Phase 7（任意拡張）で対応する。

### 11.4 最終 Program.cs ミドルウェアパイプライン

```
┌──────────────────────────────────────────────────────────────┐
│ ① UseExceptionHandler()                                      │
│ ② UseHsts() + UseHttpsRedirection()                          │
│ ③ SecurityHeadersMiddleware                                  │
│ ④ CorrelationIdMiddleware                                    │
│ ⑤ UseSerilogRequestLogging()                                 │
│ ⑥ UseCors()                                                  │
│ ⑦ UseAuthentication() → UseAuthorization()                   │
│ ⑧ MapUserEndpoints()                                        │
│   MapAddressEndpoints()                                      │
│   MapWishlistEndpoints()                                     │
│   MapPreferenceEndpoints()                                   │
│   MapActivityEndpoints()                                     │
│   MapConsentEndpoints()                                      │
│   MapDsrEndpoints()                                          │
│   MapMemberRankEndpoints()                                   │
│   MapAdminUserEndpoints()                                    │
│   MapHealthChecks("/health") + MapHealthChecks("/health/ready")│
└──────────────────────────────────────────────────────────────┘
```

### 11.5 最終ディレクトリツリー

```
UserManagementService/
├── UserManagementService.csproj
├── Program.cs
├── Dockerfile
├── .dockerignore
├── appsettings.json
├── appsettings.Development.json
├── appsettings.Production.json
├── Endpoints/
│   ├── UserEndpoints.cs
│   ├── AddressEndpoints.cs
│   ├── WishlistEndpoints.cs
│   ├── PreferenceEndpoints.cs
│   ├── ActivityEndpoints.cs
│   ├── ConsentEndpoints.cs
│   ├── DsrEndpoints.cs
│   ├── MemberRankEndpoints.cs
│   └── AdminUserEndpoints.cs
├── Services/
│   ├── Interfaces/
│   │   ├── IUserService.cs
│   │   ├── IAddressService.cs
│   │   ├── IWishlistService.cs
│   │   ├── IPreferenceService.cs
│   │   ├── IActivityService.cs
│   │   ├── IConsentService.cs
│   │   ├── IDsrService.cs
│   │   ├── IMemberRankService.cs
│   │   └── IEventPublisherService.cs
│   ├── UserService.cs
│   ├── AddressService.cs
│   ├── WishlistService.cs
│   ├── PreferenceService.cs
│   ├── ActivityService.cs
│   ├── ConsentService.cs
│   ├── DsrService.cs
│   ├── MemberRankService.cs
│   └── EventPublisherService.cs
├── Repositories/
│   ├── Interfaces/
│   │   ├── IUserRepository.cs
│   │   ├── IAddressRepository.cs
│   │   ├── IWishlistRepository.cs
│   │   ├── IPreferenceRepository.cs
│   │   ├── IActivityRepository.cs
│   │   ├── IConsentRepository.cs
│   │   ├── IDeletionRequestRepository.cs
│   │   ├── IMemberRankRepository.cs
│   │   └── IOutboxEventRepository.cs
│   ├── UserRepository.cs
│   ├── AddressRepository.cs
│   ├── WishlistRepository.cs
│   ├── PreferenceRepository.cs
│   ├── ActivityRepository.cs
│   ├── ConsentRepository.cs
│   ├── DeletionRequestRepository.cs
│   ├── MemberRankRepository.cs
│   └── OutboxEventRepository.cs
├── Models/
│   ├── User.cs
│   ├── Address.cs
│   ├── Wishlist.cs
│   ├── WishlistItem.cs
│   ├── UserPreference.cs
│   ├── UserActivity.cs
│   ├── MemberRank.cs
│   ├── Consent.cs
│   ├── DeletionRequest.cs
│   └── OutboxEvent.cs
├── DTOs/
│   ├── Requests/
│   │   ├── UpdateUserRequest.cs
│   │   ├── CreateAddressRequest.cs
│   │   ├── UpdateAddressRequest.cs
│   │   ├── CreateWishlistRequest.cs
│   │   ├── UpdateWishlistRequest.cs
│   │   ├── AddWishlistItemRequest.cs
│   │   ├── UpdatePreferencesRequest.cs
│   │   ├── UpdateConsentRequest.cs
│   │   ├── CreateDeletionRequest.cs
│   │   ├── UpdateUserStatusRequest.cs
│   │   └── UpdateProcessingRestrictionRequest.cs
│   └── Responses/
│       ├── UserResponse.cs
│       ├── AddressResponse.cs
│       ├── WishlistResponse.cs
│       ├── WishlistItemResponse.cs
│       ├── PreferenceResponse.cs
│       ├── ActivityResponse.cs
│       ├── ConsentResponse.cs
│       ├── DeletionRequestResponse.cs
│       └── MemberRankResponse.cs
├── Configurations/
│   └── KafkaSettings.cs
├── Exceptions/
│   ├── NotFoundException.cs
│   ├── BusinessException.cs
│   ├── UnauthorizedException.cs
│   ├── ForbiddenException.cs
│   └── ConcurrencyException.cs
├── Events/
│   ├── UserRegisteredEvent.cs
│   ├── UserDeletedEvent.cs
│   ├── UserProfileUpdatedEvent.cs
│   ├── ConsentRevokedEvent.cs
│   ├── OrderConfirmedEvent.cs
│   ├── UserDeletionCompletedEvent.cs
│   ├── InventoryStockUpdatedEvent.cs
│   ├── PasswordChangedEvent.cs
│   ├── MemberRankUpdatedEvent.cs
│   └── UserDeletionNotificationEvent.cs
├── BackgroundServices/
│   ├── OutboxPublisher.cs
│   ├── UserRegisteredConsumer.cs
│   ├── UserDeletionCompletedConsumer.cs
│   ├── OrderConfirmedConsumer.cs
│   ├── InventoryStockUpdatedConsumer.cs
│   ├── PasswordChangedConsumer.cs
│   ├── DataExportService.cs
│   ├── DeletionRequestProcessor.cs
│   ├── MemberRankEvaluationService.cs
│   └── DsrTimeoutMonitorService.cs
├── Validators/
│   ├── UpdateUserRequestValidator.cs
│   ├── CreateAddressRequestValidator.cs
│   ├── UpdateAddressRequestValidator.cs
│   ├── CreateWishlistRequestValidator.cs
│   ├── UpdatePreferencesRequestValidator.cs
│   └── UpdateConsentRequestValidator.cs
├── Infrastructure/
│   ├── Persistence/
│   │   └── AppDbContext.cs
│   └── Middleware/
│       ├── CorrelationIdMiddleware.cs
│       └── SecurityHeadersMiddleware.cs
└── Migrations/

UserManagementService.Tests/
├── UserManagementService.Tests.csproj
├── Unit/
│   ├── Services/
│   │   ├── UserServiceTests.cs
│   │   ├── AddressServiceTests.cs
│   │   ├── WishlistServiceTests.cs
│   │   ├── PreferenceServiceTests.cs
│   │   ├── ActivityServiceTests.cs
│   │   ├── ConsentServiceTests.cs
│   │   ├── DsrServiceTests.cs
│   │   ├── MemberRankServiceTests.cs
│   │   └── EventPublisherServiceTests.cs
│   ├── BackgroundServices/
│   │   ├── PasswordChangedConsumerTests.cs
│   │   └── DataExportServiceTests.cs
│   └── Validators/
│       ├── UpdateUserRequestValidatorTests.cs
│       ├── CreateAddressRequestValidatorTests.cs
│       └── CreateWishlistRequestValidatorTests.cs
├── Integration/
│   ├── Endpoints/
│   │   ├── UserEndpointsTests.cs
│   │   ├── AdminUserEndpointsTests.cs
│   │   └── DsrEndpointsTests.cs
│   └── Repositories/
│       └── UserRepositoryTests.cs
└── Fixtures/
    ├── CustomWebApplicationFactory.cs
    ├── FakeAuthHandler.cs
    └── PostgresContainerFixture.cs
```

### Phase 11 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] `dotnet test` — 全テスト PASS
- [ ] `dotnet publish` — 成功
- [ ] Dockerfile: マルチステージビルド（`sdk` → `aspnet`）
- [ ] Dockerfile: ベースイメージタグ固定（`latest` 禁止）
- [ ] Dockerfile: `USER skishop`（非 root 実行）
- [ ] Dockerfile: `HEALTHCHECK` 設定
- [ ] Dockerfile: `EXPOSE 8080`
- [ ] .dockerignore: `bin/`, `obj/`, `.git/`, `*.md` 除外
- [ ] AppHost: `.WithReference(userDb).WithReference(kafka)` 設定
- [ ] AppHost: `.WithReference(redis)` は含まれないこと（spec.md 準拠）
- [ ] ミドルウェアパイプライン: §11.4 の順序と完全一致
- [ ] **TODO/FIXME/HACK コメント残存チェック**: ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック**: テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック**: 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## 横断チェックリスト

全 Phase 完了後に実施する最終確認事項。

### AGENTS.md 禁止事項検証コマンド

```bash
# 秘密情報のハードコードチェック
grep -r "Password\s*=\s*\"" --include="*.cs" UserManagementService/

# Console.WriteLine チェック
grep -r "Console\.Write" --include="*.cs" UserManagementService/

# SQL 文字列結合チェック
grep -r "FromSqlRaw.*\+" --include="*.cs" UserManagementService/

# .Result / .Wait() チェック（デッドロックリスク）
grep -rP "\.(Result|Wait)\(\)" --include="*.cs" UserManagementService/

# プロパティインジェクションチェック
grep -r "\[Inject\]" --include="*.cs" UserManagementService/

# DateTime.Now チェック（ローカル時刻禁止）
grep -r "DateTime\.Now[^U]" --include="*.cs" UserManagementService/

# new HttpClient() チェック
grep -r "new HttpClient()" --include="*.cs" UserManagementService/

# Thread.Sleep チェック
grep -r "Thread\.Sleep" --include="*.cs" UserManagementService/
```

### コーディング規約チェック表

| チェック項目 | 確認方法 | 合否 |
|------------|---------|------|
| クラス名: PascalCase | 目視 | [ ] |
| インターフェース: `I` プレフィックス | 目視 | [ ] |
| 非同期メソッド: `Async` サフィックス | 目視 | [ ] |
| プライベートフィールド: `_camelCase` | 目視 | [ ] |
| primary constructor 使用 | 目視 | [ ] |
| `CancellationToken ct = default` 全 async メソッド | grep | [ ] |
| `ILogger<T>` メッセージテンプレート形式 | grep | [ ] |
| `AsNoTracking()` 読み取りクエリ | grep | [ ] |
| `[Column("snake_case")]` 全プロパティ | 目視 | [ ] |
| `[Table("snake_case")]` 全エンティティ | 目視 | [ ] |
| **TODO/FIXME/HACK コメント残存チェック** | `grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件 | [ ] |
| **Mock/Stub/仮実装の残存チェック** | `grep -rn "NotImplementedException\|\"dummy\"\|\"test\"\|\"xxx\"" --include="*.cs" src/` で本番コードに 0 件 | [ ] |
| **未実装メソッド・空メソッドチェック** | 空のメソッドボディや未実装例外スローが本番コードに残存していないこと | [ ] |

### テスト規約チェック表

| チェック項目 | 確認方法 | 合否 |
|------------|---------|------|
| `Should_X_When_Y` 命名 | 目視 | [ ] |
| AAA パターン（Arrange/Act/Assert） | 目視 | [ ] |
| Shouldly 使用（`ShouldBe`, `ShouldNotBeNull`） | 目視 | [ ] |
| NSubstitute 使用（`Substitute.For<T>()`） | 目視 | [ ] |
| `[Trait("Category", "...")]` 全テスト | grep | [ ] |
| アサーションなしテスト: 0 件 | テスト実行 | [ ] |
| カバレッジ 80% 以上 | `dotnet test --collect:"XPlat Code Coverage"` | [ ] |
| **TODO/FIXME/HACK コメント残存チェック** | `grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件 | [ ] |
| **Mock/Stub/仮実装の残存チェック** | `grep -rn "NotImplementedException\|\"dummy\"\|\"test\"\|\"xxx\"" --include="*.cs" src/` で本番コードに 0 件 | [ ] |
| **未実装メソッド・空メソッドチェック** | 空のメソッドボディや未実装例外スローが本番コードに残存していないこと | [ ] |

### フェーズ依存関係図

```
Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5
                                  ↓          ↓
                              Phase 6    Phase 8
                                  ↓
                              Phase 7（任意）
                                  
Phase 1〜8 完了 → Phase 9（テスト）
Phase 1〜9 完了 → Phase 10（可観測性）
Phase 1〜10 完了 → Phase 11（Docker・デプロイ）
```

### 参照ドキュメント一覧

| ドキュメント | 参照セクション |
|------------|-------------|
| `design-docs/user-management-design.md` | §3〜§14, §A〜§I |
| `design-docs/spec.md` | §2（アーキテクチャ）, ADR-0006（独立 DB）, AppHost 定義 |
| `AGENTS.md` | §2〜§14 |
| `.github/instructions/dotnet-coding-standards.instructions.md` | 全セクション |
| `.github/instructions/security-coding.instructions.md` | 全セクション |
| `.github/instructions/api-design.instructions.md` | 全セクション |
| `.github/instructions/dotnet-config.instructions.md` | 全セクション |
| `.github/instructions/nuget-dependency.instructions.md` | 全セクション |
| `.github/instructions/test-standards.instructions.md` | 全セクション |
| `.github/instructions/dockerfile-infra.instructions.md` | 全セクション |
| `.github/instructions/sql-schema-review.instructions.md` | 全セクション |
