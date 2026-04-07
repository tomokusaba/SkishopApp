# MailSendService フェーズ別実装計画書

> **対象サービス**: MailSendService（トランザクションメール配信）
> **ポート**: 5008
> **DB**: PostgreSQL (mailsenddb)
> **メッセージング**: Apache Kafka（イベント購読）
> **メールプロバイダ**: Azure Communication Services Email
> **設計書**: `design-docs/mailsend-service-design.md`
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

MailSendService プロジェクトの骨格を構築する。ビルド可能な最小構成（Program.cs + csproj + appsettings）を作成し、以降のフェーズの土台とする。

### 作成ファイル一覧

| # | ファイルパス | 内容 |
|---|------------|------|
| 1 | `MailSendService/MailSendService.csproj` | プロジェクト定義 |
| 2 | `MailSendService/Program.cs` | エントリポイントスケルトン |
| 3 | `MailSendService/appsettings.json` | 共通設定（安全なデフォルト値のみ） |
| 4 | `MailSendService/appsettings.Development.json` | 開発環境設定 |
| 5 | `MailSendService/appsettings.Production.json` | 本番環境設定（環境変数参照のみ） |

### 1.1 MailSendService.csproj

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

    <!-- Azure Communication Services Email -->
    <PackageReference Include="Azure.Communication.Email" Version="1.*" />
    <PackageReference Include="Azure.Identity" Version="1.*" />

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
    <PackageReference Include="AspNetCore.HealthChecks.Kafka" Version="9.*" />
  </ItemGroup>
</Project>
```

### 1.2 ディレクトリ構造

```
MailSendService/
├── MailSendService.csproj
├── Program.cs
├── Endpoints/
│   └── MailEndpoints.cs
├── Services/
│   ├── Interfaces/
│   │   ├── IMailService.cs
│   │   ├── ITemplateService.cs
│   │   ├── IAzureEmailSender.cs
│   │   └── IUserInfoResolver.cs
│   ├── MailService.cs
│   ├── TemplateService.cs
│   ├── AzureEmailSender.cs
│   └── UserInfoResolver.cs
├── Consumers/
│   ├── MailEventConsumer.cs
│   ├── MailRetryService.cs
│   └── PiiCleanupService.cs
├── Repositories/
│   ├── Interfaces/
│   │   ├── IMailLogRepository.cs
│   │   ├── IMailTemplateRepository.cs
│   │   └── IMailSuppressionRepository.cs
│   ├── MailLogRepository.cs
│   ├── MailTemplateRepository.cs
│   └── MailSuppressionRepository.cs
├── Models/
│   ├── MailLog.cs
│   ├── MailAttachment.cs
│   ├── MailTemplate.cs
│   └── MailSuppression.cs
├── DTOs/
│   ├── Requests/
│   │   ├── TestMailRequest.cs
│   │   ├── TemplateCreateRequest.cs
│   │   └── TemplateUpdateRequest.cs
│   └── Responses/
│       ├── MailLogResponse.cs
│       ├── MailStatsResponse.cs
│       ├── MailTemplateResponse.cs
│       └── PaginatedResult.cs
├── Configurations/
│   ├── MailSettings.cs
│   ├── KafkaSettings.cs
│   └── AzureEmailSettings.cs
├── Exceptions/
│   ├── MailServiceException.cs
│   ├── MailSendFailedException.cs
│   ├── TemplateNotFoundException.cs
│   ├── RateLimitExceededException.cs
│   ├── SuppressedRecipientException.cs
│   ├── InvalidEmailAddressException.cs
│   ├── EventDeserializationException.cs
│   ├── UserInfoResolutionException.cs
│   └── TemplateRenderException.cs
├── Validators/
│   ├── TestMailRequestValidator.cs
│   ├── TemplateCreateRequestValidator.cs
│   └── TemplateUpdateRequestValidator.cs
├── Infrastructure/
│   ├── Persistence/
│   │   └── AppDbContext.cs
│   └── Middleware/
│       ├── CorrelationIdMiddleware.cs
│       ├── CorrelationIdMiddlewareExtensions.cs
│       ├── SecurityHeadersMiddleware.cs
│       └── SecurityHeadersMiddlewareExtensions.cs
├── Templates/
│   └── Mail/
│       ├── Layout/
│       │   └── _Base.cshtml
│       ├── EmailVerification.cshtml
│       ├── Welcome.cshtml
│       ├── PasswordReset.cshtml
│       ├── OrderConfirmation.cshtml
│       ├── OrderCancelled.cshtml
│       ├── ShipmentNotification.cshtml
│       ├── DeliveryConfirmation.cshtml
│       └── EmailChangeVerification.cshtml
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

// TimeProvider（テスト時に時刻固定可能）
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
  "Kafka": {
    "BootstrapServers": "",
    "GroupId": "mailsend-service",
    "Topic": "domain-events"
  },
  "Azure": {
    "Communication": {
      "Endpoint": "",
      "SenderAddress": ""
    }
  },
  "Mail": {
    "Retry": {
      "MaxAttempts": 3,
      "InitialIntervalMs": 30000,
      "Multiplier": 2.0
    },
    "BaseUrl": ""
  },
  "Services": {
    "UserManagement": {
      "Url": ""
    }
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
  "Kafka": {
    "BootstrapServers": "localhost:9092"
  },
  "Azure": {
    "Communication": {
      "Endpoint": "https://skishop-acs.communication.azure.com",
      "SenderAddress": "DoNotReply@skishop-acs.azurecomm.net"
    }
  },
  "Mail": {
    "BaseUrl": "http://localhost:3000"
  },
  "Services": {
    "UserManagement": {
      "Url": "http://localhost:5002"
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

> **注記**: 本番の接続文字列・Azure エンドポイント・Kafka ブローカーは全て環境変数で提供する。

### Phase 1 完了チェックリスト

- [ ] `dotnet build MailSendService/MailSendService.csproj` — 警告なし成功
- [ ] `dotnet run --project MailSendService` — 起動確認（`/health` で 200 応答）
- [ ] .csproj: `TreatWarningsAsErrors=true`, `Nullable=enable`, `TargetFramework=net10.0`
- [ ] .csproj: プレリリース版パッケージなし（`-preview`, `-beta`, `-rc` なし）
- [ ] appsettings.json: 秘密情報なし（パスワード、API キー、接続文字列なし）
- [ ] appsettings.json: `DetailedErrors: false`, `AddServerHeader: false`
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 2: エンティティ・Value Object・Enum 定義

### 目的

設計書 §4（データモデル）および §21〜§22 に基づき、EF Core エンティティ、例外クラス階層、DTO、設定クラスを定義する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `MailSendService/Models/MailLog.cs` | 作成 | メール送信ログエンティティ |
| 2 | `MailSendService/Models/MailAttachment.cs` | 作成 | 添付ファイルエンティティ |
| 3 | `MailSendService/Models/MailTemplate.cs` | 作成 | メールテンプレートエンティティ |
| 4 | `MailSendService/Models/MailSuppression.cs` | 作成 | 配信停止リストエンティティ |
| 5 | `MailSendService/Infrastructure/Persistence/AppDbContext.cs` | 作成 | EF Core DbContext |
| 6 | `MailSendService/DTOs/Requests/TestMailRequest.cs` | 作成 | テストメール送信リクエスト DTO |
| 7 | `MailSendService/DTOs/Requests/TemplateCreateRequest.cs` | 作成 | テンプレート作成リクエスト DTO |
| 8 | `MailSendService/DTOs/Requests/TemplateUpdateRequest.cs` | 作成 | テンプレート更新リクエスト DTO |
| 9 | `MailSendService/DTOs/Responses/MailLogResponse.cs` | 作成 | メールログレスポンス DTO |
| 10 | `MailSendService/DTOs/Responses/MailStatsResponse.cs` | 作成 | メール統計レスポンス DTO |
| 11 | `MailSendService/DTOs/Responses/MailTemplateResponse.cs` | 作成 | テンプレートレスポンス DTO |
| 12 | `MailSendService/DTOs/Responses/PaginatedResult.cs` | 作成 | ページネーション結果 DTO |
| 13 | `MailSendService/Configurations/MailSettings.cs` | 作成 | メール設定クラス |
| 14 | `MailSendService/Configurations/KafkaSettings.cs` | 作成 | Kafka 設定クラス |
| 15 | `MailSendService/Configurations/AzureEmailSettings.cs` | 作成 | Azure ACS 設定クラス |
| 16 | `MailSendService/Exceptions/*.cs` | 作成 | 例外クラス階層（9 ファイル） |
| 17 | `MailSendService/Program.cs` | 更新 | EF Core + PostgreSQL 登録 |

### 2.1 MailLog エンティティ（設計書 §4.2 準拠）

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MailSendService.Models;

[Table("mail_logs")]
public class MailLog
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("event_type")]
    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    [Column("event_id")]
    [Required]
    [MaxLength(100)]
    public string EventId { get; set; } = string.Empty;

    [Column("correlation_id")]
    [MaxLength(100)]
    public string? CorrelationId { get; set; }

    [Column("recipient_email")]
    [Required]
    [MaxLength(255)]
    public string RecipientEmail { get; set; } = string.Empty;

    [Column("recipient_name")]
    [MaxLength(200)]
    public string? RecipientName { get; set; }

    [Column("template_name")]
    [Required]
    [MaxLength(100)]
    public string TemplateName { get; set; } = string.Empty;

    [Column("template_id")]
    [MaxLength(36)]
    public string? TemplateId { get; set; }

    [Column("recipient_user_id")]
    [MaxLength(36)]
    public string? RecipientUserId { get; set; }

    [Column("subject")]
    [Required]
    [MaxLength(500)]
    public string Subject { get; set; } = string.Empty;

    [Column("status")]
    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = "PENDING";

    [Column("azure_operation_id")]
    [MaxLength(200)]
    public string? AzureOperationId { get; set; }

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("retry_count")]
    public int RetryCount { get; set; }

    [Column("sent_at")]
    public DateTimeOffset? SentAt { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<MailAttachment> Attachments { get; set; } = [];

    // ナビゲーションプロパティ（FK: mail_templates）
    public MailTemplate? Template { get; set; }
}
```

### 2.2 MailAttachment エンティティ

```csharp
namespace MailSendService.Models;

[Table("mail_attachments")]
public class MailAttachment
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("mail_log_id")]
    [Required]
    [MaxLength(36)]
    public string MailLogId { get; set; } = string.Empty;

    [Column("filename")]
    [Required]
    [MaxLength(500)]
    public string Filename { get; set; } = string.Empty;

    [Column("content_type")]
    [Required]
    [MaxLength(200)]
    public string ContentType { get; set; } = string.Empty;

    [Column("content_id")]
    [MaxLength(200)]
    public string? ContentId { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public MailLog MailLog { get; set; } = null!;
}
```

### 2.3 MailTemplate エンティティ（設計書 §21.2 準拠）

```csharp
namespace MailSendService.Models;

[Table("mail_templates")]
public class MailTemplate
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("name")]
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("subject")]
    [Required]
    [MaxLength(500)]
    public string Subject { get; set; } = string.Empty;

    [Column("html_body")]
    public string? HtmlBody { get; set; }

    [Column("text_body")]
    public string? TextBody { get; set; }

    [Column("template_type")]
    [Required]
    [MaxLength(30)]
    public string TemplateType { get; set; } = "TRANSACTIONAL";

    [Column("variables", TypeName = "jsonb")]
    public string? Variables { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

### 2.4 MailSuppression エンティティ

```csharp
namespace MailSendService.Models;

[Table("mail_suppressions")]
public class MailSuppression
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("email")]
    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Column("reason")]
    [Required]
    [MaxLength(30)]
    public string Reason { get; set; } = string.Empty;

    [Column("suppressed_at")]
    public DateTimeOffset SuppressedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

### 2.5 AppDbContext（設計書 §21.1 準拠）

```csharp
using Microsoft.EntityFrameworkCore;
using MailSendService.Models;

namespace MailSendService.Infrastructure.Persistence;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    TimeProvider timeProvider) : DbContext(options)
{
    public DbSet<MailLog> MailLogs => Set<MailLog>();
    public DbSet<MailAttachment> MailAttachments => Set<MailAttachment>();
    public DbSet<MailTemplate> MailTemplates => Set<MailTemplate>();
    public DbSet<MailSuppression> MailSuppressions => Set<MailSuppression>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── MailLog ──
        modelBuilder.Entity<MailLog>(entity =>
        {
            entity.ToTable("mail_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasMaxLength(30).HasDefaultValue("PENDING");
            entity.Property(e => e.RetryCount).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.ToTable(t => t.HasCheckConstraint(
                "ck_mail_logs_status",
                "status IN ('PENDING', 'SENDING', 'SENT', 'FAILED', 'BOUNCED', 'SKIPPED')"));
            entity.ToTable(t => t.HasCheckConstraint(
                "ck_mail_logs_retry_count", "retry_count >= 0"));

            entity.HasIndex(e => e.EventId).IsUnique()
                .HasDatabaseName("idx_mail_logs_event_id");
            entity.HasIndex(e => new { e.Status, e.CreatedAt })
                .HasDatabaseName("idx_mail_logs_status_created_at");
            entity.HasIndex(e => e.RecipientEmail)
                .HasDatabaseName("idx_mail_logs_recipient");
            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("idx_mail_logs_created_at");
            entity.HasIndex(e => e.TemplateId)
                .HasDatabaseName("idx_mail_logs_template_id");
            entity.HasIndex(e => e.RecipientUserId)
                .HasDatabaseName("idx_mail_logs_recipient_user_id");

            // FK: mail_templates（spec.md FK 制約設計準拠）
            entity.HasOne(e => e.Template)
                .WithMany()
                .HasForeignKey(e => e.TemplateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Attachments)
                .WithOne(a => a.MailLog)
                .HasForeignKey(a => a.MailLogId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── MailAttachment ──
        modelBuilder.Entity<MailAttachment>(entity =>
        {
            entity.ToTable("mail_attachments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.MailLogId)
                .HasDatabaseName("idx_mail_attachments_mail_log_id");
        });

        // ── MailTemplate ──
        modelBuilder.Entity<MailTemplate>(entity =>
        {
            entity.ToTable("mail_templates");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Variables).HasColumnType("jsonb");

            entity.ToTable(t => t.HasCheckConstraint(
                "ck_mail_templates_type",
                "template_type IN ('TRANSACTIONAL', 'MARKETING')"));

            entity.HasIndex(e => e.Name).IsUnique()
                .HasDatabaseName("idx_mail_templates_name");
            entity.HasIndex(e => new { e.TemplateType, e.IsActive })
                .HasDatabaseName("idx_mail_templates_type_active");
        });

        // ── MailSuppression ──
        modelBuilder.Entity<MailSuppression>(entity =>
        {
            entity.ToTable("mail_suppressions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.ToTable(t => t.HasCheckConstraint(
                "ck_mail_suppressions_reason",
                "reason IN ('UNSUBSCRIBE', 'BOUNCE', 'COMPLAINT')"));

            entity.HasIndex(e => new { e.Email, e.Reason }).IsUnique()
                .HasDatabaseName("uq_mail_suppressions_email_reason");
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        foreach (var entry in ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            if (entry.Entity is MailLog mailLog)
            {
                if (entry.State == EntityState.Added) mailLog.CreatedAt = now;
                mailLog.UpdatedAt = now;
            }
            else if (entry.Entity is MailAttachment attachment)
            {
                if (entry.State == EntityState.Added) attachment.CreatedAt = now;
                attachment.UpdatedAt = now;
            }
            else if (entry.Entity is MailTemplate template)
            {
                if (entry.State == EntityState.Added) template.CreatedAt = now;
                template.UpdatedAt = now;
            }
            else if (entry.Entity is MailSuppression suppression)
            {
                if (entry.State == EntityState.Added) suppression.CreatedAt = now;
                suppression.UpdatedAt = now;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
```

### 2.6 DTO 定義（設計書 §7.2 / §24.4 準拠）

```csharp
// DTOs/Requests/TestMailRequest.cs
using System.ComponentModel.DataAnnotations;

namespace MailSendService.DTOs.Requests;

public record TestMailRequest(
    [Required(ErrorMessage = "送信先メールアドレスは必須です")]
    [EmailAddress(ErrorMessage = "有効なメールアドレスを入力してください")]
    [StringLength(255)]
    string RecipientEmail,

    [Required(ErrorMessage = "テンプレート名は必須です")]
    [StringLength(100)]
    string TemplateName,

    Dictionary<string, object>? Variables);

// DTOs/Requests/TemplateCreateRequest.cs
public record TemplateCreateRequest(
    [Required, StringLength(100, MinimumLength = 1)]
    string Name,
    [Required, StringLength(500, MinimumLength = 1)]
    string Subject,
    string? HtmlBody,
    string? TextBody,
    [Required, StringLength(30)]
    string TemplateType,
    string? Variables);

// DTOs/Requests/TemplateUpdateRequest.cs
public record TemplateUpdateRequest(
    [StringLength(500, MinimumLength = 1)]
    string? Subject,
    string? HtmlBody,
    string? TextBody,
    string? Variables,
    bool? IsActive);

// DTOs/Responses/MailLogResponse.cs
public record MailLogResponse(
    string Id, string EventType, string RecipientEmail, string? RecipientName,
    string TemplateName, string Subject, string Status, int RetryCount,
    string? ErrorMessage, DateTimeOffset? SentAt, DateTimeOffset CreatedAt);

// DTOs/Responses/MailStatsResponse.cs
public record MailStatsResponse(
    long TotalSent, long TotalFailed, long TotalPending,
    double SuccessRate, Dictionary<string, long> SentByTemplate);

// DTOs/Responses/MailTemplateResponse.cs
public record MailTemplateResponse(
    string Id, string Name, string Subject, string? HtmlBody, string? TextBody,
    string TemplateType, string? Variables, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

// DTOs/Responses/PaginatedResult.cs
public record PaginatedResult<T>(
    List<T> Items, int Page, int PageSize, long TotalCount, int TotalPages);
```

### 2.7 設定クラス（IOptions<T> パターン）

```csharp
// Configurations/KafkaSettings.cs
namespace MailSendService.Configurations;

public record KafkaSettings(
    string BootstrapServers,
    string GroupId,
    string Topic);

// Configurations/MailSettings.cs
public record RetrySettings(int MaxAttempts = 3, int InitialIntervalMs = 30000, double Multiplier = 2.0);
public record MailSettings(RetrySettings Retry, string BaseUrl);

// Configurations/AzureEmailSettings.cs
public record AzureEmailSettings(string Endpoint, string SenderAddress);
```

### 2.8 例外クラス階層（設計書 §22.1 準拠）

```csharp
// Exceptions/MailServiceException.cs
namespace MailSendService.Exceptions;

public abstract class MailServiceException(string message, Exception? innerException = null)
    : Exception(message, innerException);

// MailSendFailedException.cs — リトライ対象
public class MailSendFailedException(string message, Exception? innerException = null)
    : MailServiceException(message, innerException);

// TemplateNotFoundException.cs — HTTP 404
public class TemplateNotFoundException(string templateName)
    : MailServiceException($"テンプレートが見つかりません: {templateName}");

// RateLimitExceededException.cs — SKIPPED
public class RateLimitExceededException(string recipientEmail)
    : MailServiceException($"レート制限超過: {recipientEmail}");

// SuppressedRecipientException.cs — SKIPPED
public class SuppressedRecipientException(string recipientEmail)
    : MailServiceException($"配信停止済み: {recipientEmail}");

// InvalidEmailAddressException.cs — SKIPPED
public class InvalidEmailAddressException(string message)
    : MailServiceException(message);

// EventDeserializationException.cs — DLT 転送検討
public class EventDeserializationException(string message, Exception? innerException = null)
    : MailServiceException(message, innerException);

// UserInfoResolutionException.cs — リトライ対象
public class UserInfoResolutionException(string message, Exception? innerException = null)
    : MailServiceException(message, innerException);

// TemplateRenderException.cs — FAILED
public class TemplateRenderException(string templateName, Exception? innerException = null)
    : MailServiceException($"テンプレートレンダリング失敗: {templateName}", innerException);
```

### 2.9 Program.cs 更新（EF Core 登録）

```csharp
// Program.cs に追加
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
```

### Phase 2 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] `dotnet ef migrations add Initial --project MailSendService` — マイグレーション生成成功
- [ ] 全エンティティ: `[Table("snake_case")]`, `[Column("snake_case")]` 属性あり
- [ ] 全エンティティ: `created_at`, `updated_at` カラムあり
- [ ] MailLog: `event_id` に UNIQUE 制約（冪等性保証）
- [ ] MailLog: `status` に CHECK 制約（PENDING, SENDING, SENT, FAILED, BOUNCED, SKIPPED）— 設計書 §21.1 準拠
- [ ] MailLog: `template_id` カラム追加（FK: mail_templates、nullable）
- [ ] MailLog: `recipient_user_id` カラム追加（GDPR DSR 処理用、nullable）
- [ ] MailLog: `Template` ナビゲーションプロパティ追加（FK → MailTemplate）
- [ ] MailLog: 複合インデックス `idx_mail_logs_status_created_at` (status, created_at)
- [ ] MailLog: インデックス `idx_mail_logs_template_id` (template_id)
- [ ] MailLog: インデックス `idx_mail_logs_recipient_user_id` (recipient_user_id)
- [ ] MailLog: FK `HasOne(Template).WithMany().HasForeignKey(TemplateId).OnDelete(Restrict)`
- [ ] MailSuppression: `(email, reason)` に複合 UNIQUE 制約（設計書 §4.2 / §21.1 準拠）
- [ ] MailTemplate: `name` に UNIQUE 制約
- [ ] MailTemplate: `template_type` に CHECK 制約（TRANSACTIONAL, MARKETING）
- [ ] MailTemplate: `variables` に JSONB カラム型指定
- [ ] AppDbContext: `SaveChangesAsync` で `CreatedAt`/`UpdatedAt` を `TimeProvider` で自動更新
- [ ] AppDbContext: CASCADE DELETE — MailLog → MailAttachment
- [ ] OutboxEvent エンティティ: `outbox_events` テーブル定義（Outbox パターン用、設計書 §12.4 / AGENTS.md §10.4 準拠）
- [ ] DTO: 全て不変 record 型
- [ ] 例外クラス: 全て `MailServiceException` を継承
- [ ] `DateTime.Now` の使用なし（`DateTimeOffset.UtcNow` / `TimeProvider` のみ）
- [ ] コレクションナビゲーション: `= []` で初期化
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 3: Repository 層実装

### 目的

設計書 §23 に基づき、Aggregate Root 単位の Repository インターフェースと EF Core 実装を作成する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `MailSendService/Repositories/Interfaces/IMailLogRepository.cs` | 作成 | メールログ Repository インターフェース |
| 2 | `MailSendService/Repositories/Interfaces/IMailTemplateRepository.cs` | 作成 | テンプレート Repository インターフェース |
| 3 | `MailSendService/Repositories/Interfaces/IMailSuppressionRepository.cs` | 作成 | 配信停止 Repository インターフェース |
| 4 | `MailSendService/Repositories/MailLogRepository.cs` | 作成 | メールログ Repository 実装 |
| 5 | `MailSendService/Repositories/MailTemplateRepository.cs` | 作成 | テンプレート Repository 実装 |
| 6 | `MailSendService/Repositories/MailSuppressionRepository.cs` | 作成 | 配信停止 Repository 実装 |
| 7 | `MailSendService/Program.cs` | 更新 | Repository DI 登録 |

### 3.1 IMailLogRepository（設計書 §23.1 準拠）

```csharp
namespace MailSendService.Repositories.Interfaces;

public interface IMailLogRepository
{
    Task<MailLog?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<MailLog?> FindByEventIdAsync(string eventId, CancellationToken ct = default);
    Task<List<MailLog>> FindByStatusAsync(string status, int limit = 50, CancellationToken ct = default);
    Task<List<MailLog>> FindByRecipientEmailAsync(string recipientEmail, int page, int pageSize, CancellationToken ct = default);
    Task<int> CountByRecipientSinceAsync(string recipientEmail, DateTimeOffset since, CancellationToken ct = default);
    Task<long> CountByStatusAsync(string status, CancellationToken ct = default);
    Task<Dictionary<string, long>> CountByTemplateAsync(CancellationToken ct = default);
    Task<List<MailLog>> FindFailedForRetryAsync(int maxRetryCount, int limit = 20, CancellationToken ct = default);
    Task<List<MailLog>> FindOlderThanAsync(DateTimeOffset cutoff, int limit = 100, CancellationToken ct = default);
    Task<(List<MailLog> Items, long TotalCount)> FindAllPagedAsync(int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(MailLog mailLog, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

### 3.2 MailLogRepository 実装

```csharp
using Microsoft.EntityFrameworkCore;
using MailSendService.Infrastructure.Persistence;
using MailSendService.Models;
using MailSendService.Repositories.Interfaces;

namespace MailSendService.Repositories;

public class MailLogRepository(AppDbContext context) : IMailLogRepository
{
    public async Task<MailLog?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.MailLogs
            .Include(m => m.Attachments)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<MailLog?> FindByEventIdAsync(string eventId, CancellationToken ct = default)
        => await context.MailLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.EventId == eventId, ct);

    public async Task<List<MailLog>> FindByStatusAsync(string status, int limit = 50, CancellationToken ct = default)
        => await context.MailLogs
            .AsNoTracking()
            .Where(m => m.Status == status)
            .OrderBy(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<List<MailLog>> FindByRecipientEmailAsync(string recipientEmail, int page, int pageSize, CancellationToken ct = default)
        => await context.MailLogs
            .AsNoTracking()
            .Where(m => m.RecipientEmail == recipientEmail)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    public async Task<int> CountByRecipientSinceAsync(string recipientEmail, DateTimeOffset since, CancellationToken ct = default)
        => await context.MailLogs
            .CountAsync(m => m.RecipientEmail == recipientEmail
                && m.CreatedAt >= since
                && m.Status != "SKIPPED", ct);

    public async Task<long> CountByStatusAsync(string status, CancellationToken ct = default)
        => await context.MailLogs.LongCountAsync(m => m.Status == status, ct);

    public async Task<Dictionary<string, long>> CountByTemplateAsync(CancellationToken ct = default)
        => await context.MailLogs
            .Where(m => m.Status == "SENT")
            .GroupBy(m => m.TemplateName)
            .Select(g => new { Template = g.Key, Count = g.LongCount() })
            .ToDictionaryAsync(g => g.Template, g => g.Count, ct);

    public async Task<List<MailLog>> FindFailedForRetryAsync(int maxRetryCount, int limit = 20, CancellationToken ct = default)
        => await context.MailLogs
            .Where(m => m.Status == "FAILED" && m.RetryCount < maxRetryCount)
            .OrderBy(m => m.UpdatedAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<List<MailLog>> FindOlderThanAsync(DateTimeOffset cutoff, int limit = 100, CancellationToken ct = default)
        => await context.MailLogs
            .Where(m => m.CreatedAt < cutoff && !m.RecipientEmail.EndsWith("@anonymized.local"))
            .OrderBy(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<(List<MailLog> Items, long TotalCount)> FindAllPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.MailLogs.AsNoTracking().OrderByDescending(m => m.CreatedAt);
        var totalCount = await query.LongCountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, totalCount);
    }

    public async Task AddAsync(MailLog mailLog, CancellationToken ct = default)
        => await context.MailLogs.AddAsync(mailLog, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
```

### 3.3 IMailTemplateRepository（設計書 §23.2 準拠）

```csharp
namespace MailSendService.Repositories.Interfaces;

public interface IMailTemplateRepository
{
    Task<MailTemplate?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<MailTemplate?> FindByNameAsync(string name, CancellationToken ct = default);
    Task<MailTemplate?> FindActiveByNameAsync(string name, CancellationToken ct = default);
    Task<List<MailTemplate>> FindAllAsync(CancellationToken ct = default);
    Task<List<MailTemplate>> FindByTypeAsync(string templateType, CancellationToken ct = default);
    Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default);
    Task AddAsync(MailTemplate template, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

### 3.4 MailTemplateRepository 実装

```csharp
namespace MailSendService.Repositories;

public class MailTemplateRepository(AppDbContext context) : IMailTemplateRepository
{
    public async Task<MailTemplate?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.MailTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<MailTemplate?> FindByNameAsync(string name, CancellationToken ct = default)
        => await context.MailTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Name == name, ct);

    public async Task<MailTemplate?> FindActiveByNameAsync(string name, CancellationToken ct = default)
        => await context.MailTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Name == name && t.IsActive, ct);

    public async Task<List<MailTemplate>> FindAllAsync(CancellationToken ct = default)
        => await context.MailTemplates.AsNoTracking()
            .OrderBy(t => t.Name).ToListAsync(ct);

    public async Task<List<MailTemplate>> FindByTypeAsync(string templateType, CancellationToken ct = default)
        => await context.MailTemplates.AsNoTracking()
            .Where(t => t.TemplateType == templateType && t.IsActive)
            .OrderBy(t => t.Name).ToListAsync(ct);

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default)
        => await context.MailTemplates.AnyAsync(t => t.Name == name, ct);

    public async Task AddAsync(MailTemplate template, CancellationToken ct = default)
        => await context.MailTemplates.AddAsync(template, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
```

### 3.5 IMailSuppressionRepository / 実装（設計書 §23.3 準拠）

```csharp
// インターフェース
namespace MailSendService.Repositories.Interfaces;

public interface IMailSuppressionRepository
{
    Task<bool> IsSuppressedAsync(string email, CancellationToken ct = default);
    Task<MailSuppression?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<List<MailSuppression>> FindAllAsync(int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(MailSuppression suppression, CancellationToken ct = default);
    Task RemoveAsync(MailSuppression suppression, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

// 実装
namespace MailSendService.Repositories;

public class MailSuppressionRepository(AppDbContext context) : IMailSuppressionRepository
{
    public async Task<bool> IsSuppressedAsync(string email, CancellationToken ct = default)
        => await context.MailSuppressions.AnyAsync(s => s.Email == email, ct);

    public async Task<MailSuppression?> FindByEmailAsync(string email, CancellationToken ct = default)
        => await context.MailSuppressions.FirstOrDefaultAsync(s => s.Email == email, ct);

    public async Task<List<MailSuppression>> FindAllAsync(int page, int pageSize, CancellationToken ct = default)
        => await context.MailSuppressions.AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

    public async Task AddAsync(MailSuppression suppression, CancellationToken ct = default)
        => await context.MailSuppressions.AddAsync(suppression, ct);

    public async Task RemoveAsync(MailSuppression suppression, CancellationToken ct = default)
        => context.MailSuppressions.Remove(suppression);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
```

### 3.6 Program.cs 更新（Repository DI 登録）

```csharp
builder.Services.AddScoped<IMailLogRepository, MailLogRepository>();
builder.Services.AddScoped<IMailTemplateRepository, MailTemplateRepository>();
builder.Services.AddScoped<IMailSuppressionRepository, MailSuppressionRepository>();
```

### Phase 3 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] 全 Repository: Aggregate Root 単位のインターフェース定義
- [ ] 全 Repository: primary constructor による DI
- [ ] 全 async メソッド: `CancellationToken ct = default` パラメータあり
- [ ] 読み取り専用クエリ: `AsNoTracking()` 使用
- [ ] MailLogRepository: `CountByRecipientSinceAsync` — レート制限用カウントメソッド
- [ ] MailLogRepository: `FindFailedForRetryAsync` — リトライ対象検索
- [ ] MailLogRepository: `FindOlderThanAsync` — PII クリーンアップ用
- [ ] MailSuppressionRepository: `IsSuppressedAsync` — 配信停止チェック
- [ ] Program.cs: 全 Repository が `AddScoped` で登録
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 4: Service 層実装

### 目的

設計書 §24 に基づき、メール送信ビジネスロジック、テンプレート管理、Azure ACS 送信ラッパー、UserManagementService API クライアントを実装する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `MailSendService/Services/Interfaces/IMailService.cs` | 作成 | メール送信サービスインターフェース |
| 2 | `MailSendService/Services/Interfaces/ITemplateService.cs` | 作成 | テンプレートサービスインターフェース |
| 3 | `MailSendService/Services/Interfaces/IAzureEmailSender.cs` | 作成 | Azure ACS 送信インターフェース |
| 4 | `MailSendService/Services/Interfaces/IUserInfoResolver.cs` | 作成 | ユーザー情報取得インターフェース |
| 5 | `MailSendService/Services/MailService.cs` | 作成 | メール送信ビジネスロジック（冪等性・レート制限・配信停止チェック） |
| 6 | `MailSendService/Services/TemplateService.cs` | 作成 | テンプレート管理サービス |
| 7 | `MailSendService/Services/AzureEmailSender.cs` | 作成 | Azure ACS 送信ラッパー |
| 8 | `MailSendService/Services/UserInfoResolver.cs` | 作成 | UserManagementService API クライアント |
| 9 | `MailSendService/Validators/TestMailRequestValidator.cs` | 作成 | テストメール送信バリデーター |
| 10 | `MailSendService/Validators/TemplateCreateRequestValidator.cs` | 作成 | テンプレート作成バリデーター |
| 11 | `MailSendService/Validators/TemplateUpdateRequestValidator.cs` | 作成 | テンプレート更新バリデーター |
| 12 | `MailSendService/Program.cs` | 更新 | Service DI 登録 |

### 4.1 IMailService（設計書 §24.1 準拠）

```csharp
namespace MailSendService.Services.Interfaces;

public interface IMailService
{
    Task ProcessEventAsync(string eventType, string eventId, string correlationId,
        string payload, CancellationToken ct = default);
    Task<MailLogResponse> SendTestMailAsync(TestMailRequest request, CancellationToken ct = default);
    Task<MailLogResponse> RetryAsync(string mailLogId, CancellationToken ct = default);
    Task<PaginatedResult<MailLogResponse>> GetLogsAsync(int page, int pageSize, CancellationToken ct = default);
    Task<MailLogResponse> GetLogByIdAsync(string id, CancellationToken ct = default);
    Task<MailStatsResponse> GetStatsAsync(CancellationToken ct = default);

    // GDPR イベントハンドラー（設計書 §30 準拠 — Phase 1 必須: GDPR コンプライアンス）
    Task ProcessConsentRevokedAsync(string userId, string consentType, CancellationToken ct = default);
    Task ProcessUserDeletedAsync(string userId, CancellationToken ct = default);
    Task ProcessUserProcessingRestrictedAsync(string userId, CancellationToken ct = default);
    Task ProcessUserProcessingUnrestrictedAsync(string userId, CancellationToken ct = default);
}
```

### 4.2 MailService 実装（設計書 §8.2〜§9.5 準拠）

```csharp
namespace MailSendService.Services;

public class MailService(
    IMailLogRepository mailLogRepository,
    IMailSuppressionRepository suppressionRepository,
    ITemplateService templateService,
    IAzureEmailSender emailSender,
    IUserInfoResolver userInfoResolver,
    IOptions<MailSettings> mailOptions,
    ILogger<MailService> logger) : IMailService
{
    private static readonly HashSet<string> SecurityEmails = ["password-reset", "email-verification"];

    public async Task ProcessEventAsync(string eventType, string eventId, string correlationId,
        string payload, CancellationToken ct = default)
    {
        // 1. 冪等性チェック（event_id UNIQUE）
        var existing = await mailLogRepository.FindByEventIdAsync(eventId, ct);
        if (existing is not null)
        {
            logger.LogInformation("Duplicate event skipped: {EventId}", eventId);
            return;
        }

        // 2. イベントタイプに応じたテンプレート・受信者の解決
        var (templateName, recipientEmail, recipientName, variables) =
            await ResolveEventDataAsync(eventType, payload, ct);

        // 3. メールアドレスバリデーション
        if (!IsValidEmail(recipientEmail))
        {
            await RecordSkippedAsync(eventType, eventId, correlationId, recipientEmail,
                templateName, "Invalid email address", ct);
            return;
        }

        // 4. 配信停止チェック
        if (await suppressionRepository.IsSuppressedAsync(recipientEmail, ct))
        {
            await RecordSkippedAsync(eventType, eventId, correlationId, recipientEmail,
                templateName, "Suppressed recipient", ct);
            return;
        }

        // 5. レート制限チェック（セキュリティメールは対象外）
        if (!SecurityEmails.Contains(templateName))
        {
            var oneHourAgo = DateTimeOffset.UtcNow.AddHours(-1);
            var recentCount = await mailLogRepository
                .CountByRecipientSinceAsync(recipientEmail, oneHourAgo, ct);
            if (recentCount >= 5)
            {
                await RecordSkippedAsync(eventType, eventId, correlationId, recipientEmail,
                    templateName, "Rate limit exceeded (5/hour)", ct);
                return;
            }
        }

        // 6. テンプレートレンダリング
        var rendered = await templateService.RenderAsync(templateName, variables, ct);

        // 7. MailLog 作成（PENDING → SENDING）
        var mailLog = new MailLog
        {
            EventType = eventType,
            EventId = eventId,
            CorrelationId = correlationId,
            RecipientEmail = recipientEmail,
            RecipientName = recipientName,
            TemplateName = templateName,
            Subject = rendered.Subject,
            Status = "SENDING"
        };
        await mailLogRepository.AddAsync(mailLog, ct);
        await mailLogRepository.SaveChangesAsync(ct);

        // 8. Azure ACS 送信
        try
        {
            var operationId = await emailSender.SendAsync(
                recipientEmail, recipientName ?? string.Empty,
                rendered.Subject, rendered.HtmlBody,
                rendered.PlainTextBody, ct: ct);

            mailLog.Status = "SENT";
            mailLog.AzureOperationId = operationId;
            mailLog.SentAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Mail send failed: {EventId}, Template: {Template}",
                eventId, templateName);
            mailLog.Status = "FAILED";
            mailLog.ErrorMessage = ex.Message;
        }

        await mailLogRepository.SaveChangesAsync(ct);
    }

    // イベントタイプ別のデータ解決
    private async Task<(string templateName, string email, string? name, Dictionary<string, object> variables)>
        ResolveEventDataAsync(string eventType, string payload, CancellationToken ct)
    {
        // イベントタイプに応じてペイロードをデシリアライズし、
        // テンプレート名・受信者情報・テンプレート変数を返す
        // order.created/order.cancelled/shipment.status.updated は
        // UserInfoResolver 経由で顧客情報を取得
        return eventType switch
        {
            "user.registered" => ResolveUserRegistered(payload),
            "password.reset.requested" => ResolvePasswordReset(payload),
            "user.verified" => ResolveUserVerified(payload),
            "order.created" => await ResolveOrderCreated(payload, ct),
            "order.cancelled" => await ResolveOrderCancelled(payload, ct),
            "shipment.status.updated" => await ResolveShipmentStatusUpdated(payload, ct),
            "user.email_changed" => ResolveEmailChanged(payload),
            _ => throw new EventDeserializationException($"Unsupported event type: {eventType}")
        };
    }

    private static bool IsValidEmail(string email)
        => !string.IsNullOrWhiteSpace(email) && email.Contains('@') && email.Contains('.');

    // ... 省略: 各イベントタイプの Resolve メソッドと RecordSkippedAsync
}
```

### 4.3 IAzureEmailSender / AzureEmailSender（設計書 §9 / §24.3 準拠）

```csharp
// インターフェース
namespace MailSendService.Services.Interfaces;

public interface IAzureEmailSender
{
    Task<string> SendAsync(
        string recipientEmail, string recipientName,
        string subject, string htmlBody, string? plainTextBody = null,
        IReadOnlyList<EmailAttachmentInfo>? attachments = null,
        CancellationToken ct = default);
}

public record EmailAttachmentInfo(string Filename, string ContentType, BinaryData Content, string? ContentId = null);

// 実装（Azure Communication Services Email）
namespace MailSendService.Services;

public class AzureEmailSender(
    EmailClient emailClient,
    IOptions<AzureEmailSettings> settings,
    ILogger<AzureEmailSender> logger) : IAzureEmailSender
{
    public async Task<string> SendAsync(
        string recipientEmail, string recipientName,
        string subject, string htmlBody, string? plainTextBody = null,
        IReadOnlyList<EmailAttachmentInfo>? attachments = null,
        CancellationToken ct = default)
    {
        var senderAddress = settings.Value.SenderAddress;

        var emailMessage = new EmailMessage(
            senderAddress: senderAddress,
            recipientAddress: recipientEmail,
            content: new EmailContent(subject)
            {
                Html = htmlBody,
                PlainText = plainTextBody
            });

        var operation = await emailClient.SendAsync(
            WaitUntil.Completed, emailMessage, ct);

        logger.LogInformation("Email sent: Recipient={MaskedEmail}, OperationId={OperationId}",
            MaskEmail(recipientEmail), operation.Id);

        return operation.Id;
    }

    private static string MaskEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 1) return "***@***";
        return $"{email[0]}***@{email[(atIndex + 1)..]}";
    }
}
```

### 4.4 IUserInfoResolver / UserInfoResolver（設計書 §12.3 準拠）

```csharp
// インターフェース
namespace MailSendService.Services.Interfaces;

public interface IUserInfoResolver
{
    Task<UserInfo?> ResolveAsync(string customerId, CancellationToken ct = default);
}

public record UserInfo(string Id, string Email, string FirstName, string LastName);

// 実装
namespace MailSendService.Services;

public class UserInfoResolver(
    HttpClient httpClient,
    ILogger<UserInfoResolver> logger) : IUserInfoResolver
{
    public async Task<UserInfo?> ResolveAsync(string customerId, CancellationToken ct = default)
    {
        try
        {
            var response = await httpClient.GetAsync($"/api/v1/users/{customerId}", ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                logger.LogWarning("ユーザーが見つかりません: {CustomerId}", customerId);
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<UserInfo>(ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "UserManagementService API エラー: {StatusCode}, CustomerId: {CustomerId}",
                ex.StatusCode, customerId);
            throw new UserInfoResolutionException(
                $"ユーザー情報取得失敗: {customerId}", ex);
        }
    }
}
```

### 4.5 FluentValidation バリデーター（設計書 §25 準拠）

```csharp
// TestMailRequestValidator.cs
namespace MailSendService.Validators;

public class TestMailRequestValidator : AbstractValidator<TestMailRequest>
{
    public TestMailRequestValidator()
    {
        RuleFor(x => x.RecipientEmail)
            .NotEmpty().WithMessage("送信先メールアドレスは必須です")
            .MaximumLength(255)
            .EmailAddress().WithMessage("有効なメールアドレスを入力してください");

        RuleFor(x => x.TemplateName)
            .NotEmpty().WithMessage("テンプレート名は必須です")
            .MaximumLength(100)
            .Matches(@"^[a-z0-9-]+$").WithMessage("テンプレート名は半角英小文字・数字・ハイフンのみ");
    }
}

// TemplateCreateRequestValidator.cs
public class TemplateCreateRequestValidator : AbstractValidator<TemplateCreateRequest>
{
    private static readonly string[] AllowedTypes = ["TRANSACTIONAL", "MARKETING"];

    public TemplateCreateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100)
            .Matches(@"^[a-z0-9-]+$");
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(500);
        RuleFor(x => x.TemplateType).NotEmpty()
            .Must(t => AllowedTypes.Contains(t));
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.HtmlBody) || !string.IsNullOrWhiteSpace(x.TextBody))
            .WithMessage("HTML 本文またはテキスト本文の少なくとも一方を指定してください");
    }
}

// TemplateUpdateRequestValidator.cs
public class TemplateUpdateRequestValidator : AbstractValidator<TemplateUpdateRequest>
{
    public TemplateUpdateRequestValidator()
    {
        RuleFor(x => x.Subject).MaximumLength(500).When(x => x.Subject is not null);
        RuleFor(x => x)
            .Must(x => x.Subject is not null || x.HtmlBody is not null
                || x.TextBody is not null || x.Variables is not null || x.IsActive is not null)
            .WithMessage("更新するフィールドを少なくとも 1 つ指定してください");
    }
}
```

### 4.6 Program.cs 更新（Service DI 登録）

```csharp
// Services
builder.Services.AddScoped<IMailService, MailService>();
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<IAzureEmailSender, AzureEmailSender>();

// HttpClient（UserManagementService 呼び出し用 — IHttpClientFactory + Polly）
builder.Services.AddHttpClient<IUserInfoResolver, UserInfoResolver>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:UserManagement:Url"]!);
    client.Timeout = TimeSpan.FromSeconds(5);
})
.AddStandardResilienceHandler();

// IOptions<T> 設定
builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection("Kafka"));
builder.Services.Configure<MailSettings>(builder.Configuration.GetSection("Mail"));
builder.Services.Configure<AzureEmailSettings>(builder.Configuration.GetSection("Azure:Communication"));

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<TestMailRequestValidator>();

// Azure Communication Services Email Client
builder.Services.AddSingleton<EmailClient>(sp =>
{
    var endpoint = builder.Configuration["Azure:Communication:Endpoint"]!;
    if (builder.Environment.IsDevelopment())
    {
        var connectionString = builder.Configuration["Azure:Communication:ConnectionString"];
        if (!string.IsNullOrEmpty(connectionString))
            return new EmailClient(connectionString);
    }
    return new EmailClient(new Uri(endpoint), new DefaultAzureCredential());
});
```

### Phase 4 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] 全 Service: primary constructor による DI
- [ ] 全 async メソッド: `CancellationToken ct = default` パラメータあり
- [ ] MailService: 冪等性チェック（event_id UNIQUE）実装済み
- [ ] MailService: 配信停止チェック（mail_suppressions）実装済み
- [ ] MailService: レート制限チェック（1 時間 5 通、セキュリティメール除外）実装済み
- [ ] MailService: メールアドレスバリデーション実装済み
- [ ] AzureEmailSender: PII マスキング（メールアドレス部分マスク）
- [ ] UserInfoResolver: `IHttpClientFactory` + `AddStandardResilienceHandler` 使用
- [ ] UserInfoResolver: タイムアウト 5 秒設定
- [ ] FluentValidation: 3 つのバリデーター定義済み
- [ ] `new HttpClient()` の直接使用なし
- [ ] 秘密情報のハードコードなし
- [ ] ログ: メッセージテンプレート形式（文字列補間禁止）
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 5: Endpoints 実装

### 目的

設計書 §7（管理用 REST API）および §26 に基づき、管理者向け Minimal API エンドポイントを実装する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `MailSendService/Endpoints/MailEndpoints.cs` | 作成 | メール管理エンドポイント |
| 2 | `MailSendService/Program.cs` | 更新 | エンドポイント登録 |

### 5.1 エンドポイント一覧（設計書 §7.1 / §26.1 準拠）

| メソッド | パス | ロール | 説明 | レスポンス |
|--------|------|------|------|----------|
| GET | `/admin/mail/logs` | ADMIN | メール送信履歴一覧（ページネーション） | `PaginatedResult<MailLogResponse>` |
| GET | `/admin/mail/logs/{id}` | ADMIN | メール送信履歴詳細 | `MailLogResponse` |
| POST | `/admin/mail/logs/{id}/retry` | ADMIN | 失敗メールの手動リトライ | `MailLogResponse` |
| GET | `/admin/mail/stats` | ADMIN, MANAGER | メール送信統計 | `MailStatsResponse` |
| POST | `/admin/mail/test` | ADMIN | テストメール送信 | `MailLogResponse` |
| GET | `/admin/mail/templates` | ADMIN | テンプレート一覧 | `List<MailTemplateResponse>` |
| GET | `/admin/mail/templates/{id}` | ADMIN | テンプレート詳細 | `MailTemplateResponse` |
| POST | `/admin/mail/templates` | ADMIN | テンプレート作成 | `MailTemplateResponse` (201) |
| PUT | `/admin/mail/templates/{id}` | ADMIN | テンプレート更新 | `MailTemplateResponse` |
| DELETE | `/admin/mail/templates/{id}` | ADMIN | テンプレート無効化（論理削除） | 204 No Content |

### 5.2 MailEndpoints 実装（設計書 §26 準拠）

```csharp
using FluentValidation;
using MailSendService.DTOs.Requests;
using MailSendService.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MailSendService.Endpoints;

public static class MailEndpoints
{
    public static void MapMailEndpoints(this IEndpointRouteBuilder app)
    {
        // メール管理 API
        var adminGroup = app.MapGroup("/admin/mail")
            .WithTags("Mail Administration")
            .RequireAuthorization("AdminOnly")
            .WithOpenApi();

        adminGroup.MapGet("/logs", GetMailLogs).WithName("GetMailLogs");
        adminGroup.MapGet("/logs/{id}", GetMailLogById).WithName("GetMailLogById");
        adminGroup.MapPost("/logs/{id}/retry", RetryMailSend).WithName("RetryMailSend");
        adminGroup.MapGet("/stats", GetMailStats)
            .RequireAuthorization("AdminOrManager")
            .WithName("GetMailStats");
        adminGroup.MapPost("/test", SendTestMail)
            .RequireRateLimiting("test-mail")
            .WithName("SendTestMail");

        // テンプレート管理 API
        var templateGroup = app.MapGroup("/admin/mail/templates")
            .WithTags("Mail Templates")
            .RequireAuthorization("AdminOnly")
            .WithOpenApi();

        templateGroup.MapGet("/", GetAllTemplates).WithName("GetAllTemplates");
        templateGroup.MapGet("/{id}", GetTemplateById).WithName("GetTemplateById");
        templateGroup.MapPost("/", CreateTemplate).WithName("CreateTemplate");
        templateGroup.MapPut("/{id}", UpdateTemplate).WithName("UpdateTemplate");
        templateGroup.MapDelete("/{id}", DeactivateTemplate).WithName("DeactivateTemplate");
    }

    private static async Task<IResult> GetMailLogs(
        [AsParameters] MailLogQueryParams query,
        IMailService mailService,
        CancellationToken ct)
        => Results.Ok(await mailService.GetLogsAsync(query.Page, query.PageSize, ct));

    private static async Task<IResult> GetMailLogById(
        string id,
        IMailService mailService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Results.BadRequest("ID は必須です");
        return Results.Ok(await mailService.GetLogByIdAsync(id, ct));
    }

    private static async Task<IResult> RetryMailSend(
        string id,
        IMailService mailService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Results.BadRequest("ID は必須です");
        return Results.Ok(await mailService.RetryAsync(id, ct));
    }

    private static async Task<IResult> GetMailStats(
        IMailService mailService,
        CancellationToken ct)
        => Results.Ok(await mailService.GetStatsAsync(ct));

    private static async Task<IResult> SendTestMail(
        [FromBody] TestMailRequest request,
        IValidator<TestMailRequest> validator,
        IMailService mailService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());
        return Results.Ok(await mailService.SendTestMailAsync(request, ct));
    }

    private static async Task<IResult> GetAllTemplates(
        ITemplateService templateService,
        CancellationToken ct)
        => Results.Ok(await templateService.GetAllAsync(ct));

    private static async Task<IResult> GetTemplateById(
        string id,
        ITemplateService templateService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Results.BadRequest("ID は必須です");
        return Results.Ok(await templateService.GetByIdAsync(id, ct));
    }

    private static async Task<IResult> CreateTemplate(
        [FromBody] TemplateCreateRequest request,
        IValidator<TemplateCreateRequest> validator,
        ITemplateService templateService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());
        var created = await templateService.CreateAsync(request, ct);
        return Results.Created($"/admin/mail/templates/{created.Id}", created);
    }

    private static async Task<IResult> UpdateTemplate(
        string id,
        [FromBody] TemplateUpdateRequest request,
        IValidator<TemplateUpdateRequest> validator,
        ITemplateService templateService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Results.BadRequest("ID は必須です");
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());
        return Results.Ok(await templateService.UpdateAsync(id, request, ct));
    }

    private static async Task<IResult> DeactivateTemplate(
        string id,
        ITemplateService templateService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Results.BadRequest("ID は必須です");
        await templateService.DeactivateAsync(id, ct);
        return Results.NoContent();
    }
}

public record MailLogQueryParams(int Page = 1, int PageSize = 20);
```

### 5.3 Program.cs 更新

```csharp
// エンドポイント登録
app.MapMailEndpoints();
```

### Phase 5 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] 全エンドポイント: `RequireAuthorization("AdminOnly")` または `RequireAuthorization("AdminOrManager")` 設定
- [ ] GET `/admin/mail/stats`: `AdminOrManager` ポリシー（Admin + Manager アクセス可）
- [ ] POST `/admin/mail/test`: `IValidator<TestMailRequest>` によるバリデーション
- [ ] POST `/admin/mail/templates`: `IValidator<TemplateCreateRequest>` によるバリデーション
- [ ] POST `/admin/mail/templates`: `Results.Created()` で 201 レスポンス + Location ヘッダー
- [ ] DELETE `/admin/mail/templates/{id}`: `Results.NoContent()` で 204 レスポンス
- [ ] 全エンドポイント: `CancellationToken ct` パラメータあり
- [ ] 全エンドポイント: `.WithOpenApi()` 設定
- [ ] 全エンドポイント: `.WithName()` 設定
- [ ] Endpoints が Repository を直接参照していないこと（Service 経由のみ）
- [ ] Endpoints にビジネスロジックがないこと
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 6: Kafka イベント連携

### 目的

設計書 §8（イベント消費設計）および §28 に基づき、Kafka BackgroundService コンシューマーと関連する BackgroundService（リトライ、PII クリーンアップ）を実装する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `MailSendService/Consumers/MailEventConsumer.cs` | 作成 | Kafka イベント BackgroundService |
| 2 | `MailSendService/Consumers/MailRetryService.cs` | 作成 | FAILED メールリトライ BackgroundService |
| 3 | `MailSendService/Consumers/PiiCleanupService.cs` | 作成 | PII 匿名化バッチ BackgroundService |
| 4 | `MailSendService/Consumers/EventEnvelope.cs` | 作成 | Kafka イベント共通エンベロープ |
| 5 | `MailSendService/Program.cs` | 更新 | BackgroundService 登録 |

### 6.1 消費するイベント一覧（設計書 §6.1 / §12.1 準拠）

| # | イベントタイプ | 発行元 | テンプレート名 | 優先度 | 顧客情報取得 |
|---|-------------|-------|------------|--------|-----------|
| 1 | `user.registered` | AuthService | `email-verification` | HIGH | ペイロードに含まれる |
| 2 | `password.reset.requested` | AuthService | `password-reset` | HIGH | ペイロードに含まれる |
| 3 | `user.verified` | UserManagementService | `welcome` | MEDIUM | ペイロードに含まれる |
| 4 | `order.created` | SalesManagementService | `order-confirmation` | HIGH | UserInfoResolver で取得 |
| 5 | `order.cancelled` | SalesManagementService | `order-cancelled` | HIGH | UserInfoResolver で取得 |
| 6 | `shipment.status.updated` (SHIPPED) | SalesManagementService | `shipment-notification` | HIGH | UserInfoResolver で取得 |
| 7 | `shipment.status.updated` (DELIVERED) | SalesManagementService | `delivery-confirmation` | MEDIUM | UserInfoResolver で取得 |
| 8 | `user.email_changed` | UserManagementService | `email-change-verification` | HIGH | ペイロードに含まれる |

### 6.2 発行するイベント（設計書 §12.4 準拠 — Outbox パターン）

| イベントタイプ | トリガー | ペイロード |
|-------------|---------|----------|
| `mail.sent` | メール送信成功 | `{ mailLogId, eventType, recipientEmail }` |
| `mail.send.failed` | リトライ上限到達 | `{ mailLogId, eventType, recipientEmail, errorMessage }` |

> **Outbox パターン**: `mail.sent` / `mail.send.failed` イベントの発行は、MailLog のステータス更新と同一トランザクション内で `outbox_events` テーブルに書き込む（Outbox パターン）。`OutboxPublisher` BackgroundService が `outbox_events` テーブルをポーリングし、Kafka に発行後に `PUBLISHED` に更新する。Outbox の実装は spec.md ADR-0009 / AGENTS.md §10.4 に準拠する。

#### OutboxEvent エンティティ（Phase 2 追加）

```csharp
[Table("outbox_events")]
public class OutboxEvent
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("event_type")]
    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    [Column("payload")]
    [Required]
    public string Payload { get; set; } = "{}";

    [Column("status")]
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "PENDING";  // PENDING, PUBLISHED, FAILED

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("published_at")]
    public DateTimeOffset? PublishedAt { get; set; }
}
```

#### イベント発行（MailService 内）

```csharp
// メール送信成功時 — SaveChangesAsync と同一トランザクション内
var outboxEvent = new OutboxEvent
{
    EventType = "mail.sent",
    Payload = JsonSerializer.Serialize(new
    {
        mailLogId = mailLog.Id,
        eventType = mailLog.EventType,
        recipientEmail = mailLog.RecipientEmail
    })
};
_context.Set<OutboxEvent>().Add(outboxEvent);
// MailLog ステータス更新と Outbox 書き込みは同一 SaveChangesAsync で保証
```

#### OutboxPublisher BackgroundService

```csharp
// 動的バックオフ（100ms〜5s）で outbox_events をポーリングし、Kafka に発行
// 詳細実装は AGENTS.md §10.4 / spec.md ADR-0009 に準拠
public class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var pendingEvents = await context.Set<OutboxEvent>()
                .Where(e => e.Status == "PENDING")
                .OrderBy(e => e.CreatedAt)
                .Take(20)
                .ToListAsync(stoppingToken);

            if (pendingEvents.Count == 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                continue;
            }

            foreach (var evt in pendingEvents)
            {
                try
                {
                    await producer.ProduceAsync("mail-events",
                        new Message<string, string> { Key = evt.Id, Value = evt.Payload },
                        stoppingToken);
                    evt.Status = "PUBLISHED";
                    evt.PublishedAt = DateTimeOffset.UtcNow;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Outbox publish failed: {EventId}", evt.Id);
                    evt.Status = "FAILED";
                }
            }

            await context.SaveChangesAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
        }
    }
}
```

### 6.3 MailEventConsumer（設計書 §28.1 準拠）

```csharp
using Confluent.Kafka;
using MailSendService.Configurations;
using MailSendService.Services.Interfaces;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace MailSendService.Consumers;

public class MailEventConsumer(
    IConsumer<string, string> consumer,
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaSettings> kafkaOptions,
    ILogger<MailEventConsumer> logger) : BackgroundService
{
    private static readonly HashSet<string> SupportedEvents =
    [
        "user.registered",
        "password.reset.requested",
        "user.verified",
        "order.created",
        "order.cancelled",
        "shipment.status.updated",
        "user.email_changed",
        // GDPR イベント（設計書 §30 / spec.md §同意撤回処理フロー / §DSR リクエスト処理フロー）
        "consent.revoked",
        "user.deleted",
        "user.processing-restricted",
        "user.processing-unrestricted"
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var topic = kafkaOptions.Value.Topic;
        consumer.Subscribe(topic);
        logger.LogInformation("MailEventConsumer started. Topic: {Topic}", topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                if (result?.Message?.Value is null) continue;

                var envelope = JsonSerializer.Deserialize<EventEnvelope>(
                    result.Message.Value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (envelope is null)
                {
                    logger.LogWarning("Null envelope. Offset: {Offset}", result.Offset);
                    consumer.Commit(result);
                    continue;
                }

                if (!SupportedEvents.Contains(envelope.EventType))
                {
                    logger.LogDebug("Unsupported event ignored: {EventType}", envelope.EventType);
                    consumer.Commit(result);
                    continue;
                }

                logger.LogInformation(
                    "Processing: {EventType}, EventId: {EventId}, CorrelationId: {CorrelationId}",
                    envelope.EventType, envelope.EventId, envelope.CorrelationId);

                using var scope = scopeFactory.CreateScope();
                var mailService = scope.ServiceProvider.GetRequiredService<IMailService>();

                await mailService.ProcessEventAsync(
                    envelope.EventType, envelope.EventId,
                    envelope.CorrelationId ?? string.Empty,
                    envelope.PayloadJson, stoppingToken);

                consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume error: {Topic}, {Reason}",
                    ex.ConsumerRecord?.Topic, ex.Error.Reason);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Event processing error: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        consumer.Close();
        logger.LogInformation("MailEventConsumer stopped.");
    }
}
```

### 6.4 EventEnvelope（イベント共通エンベロープ）

```csharp
using System.Text.Json.Serialization;

namespace MailSendService.Consumers;

public record EventEnvelope
{
    public string EventId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string Producer { get; init; } = string.Empty;
    public string? CorrelationId { get; init; }

    [JsonPropertyName("payload")]
    public string PayloadJson { get; init; } = "{}";
}
```

### 6.5 MailRetryService（設計書 §28.2 準拠）

```csharp
namespace MailSendService.Consumers;

public class MailRetryService(
    IServiceScopeFactory scopeFactory,
    IOptions<MailSettings> mailOptions,
    ILogger<MailRetryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MailRetryService started.");
        var retrySettings = mailOptions.Value.Retry;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var mailLogRepository = scope.ServiceProvider
                    .GetRequiredService<IMailLogRepository>();
                var mailService = scope.ServiceProvider
                    .GetRequiredService<IMailService>();

                var failedMails = await mailLogRepository
                    .FindFailedForRetryAsync(retrySettings.MaxAttempts, limit: 20, stoppingToken);

                if (failedMails.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                logger.LogInformation("Retrying {Count} failed mails.", failedMails.Count);

                foreach (var mail in failedMails)
                {
                    try
                    {
                        await mailService.RetryAsync(mail.Id, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Retry failed: {MailLogId}", mail.Id);
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "MailRetryService error: {Message}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        logger.LogInformation("MailRetryService stopped.");
    }
}
```

### 6.6 PiiCleanupService（設計書 §28.3 / §4.2 PII 保護方針準拠）

```csharp
using System.Security.Cryptography;
using System.Text;

namespace MailSendService.Consumers;

public class PiiCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<PiiCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PiiCleanupService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            var nextRun = now.Date.AddDays(1).AddHours(3);
            if (now.Hour < 3)
                nextRun = now.Date.AddHours(3);

            var delay = nextRun - now;
            if (delay > TimeSpan.Zero)
            {
                logger.LogInformation("PiiCleanupService next run: {NextRun}", nextRun);
                await Task.Delay(delay, stoppingToken);
            }

            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PII cleanup error: {Message}", ex.Message);
            }
        }

        logger.LogInformation("PiiCleanupService stopped.");
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMailLogRepository>();

        var cutoff = DateTimeOffset.UtcNow.AddDays(-365);
        var oldRecords = await repository.FindOlderThanAsync(cutoff, limit: 500, ct);

        if (oldRecords.Count == 0)
        {
            logger.LogInformation("PII cleanup: no records older than 365 days.");
            return;
        }

        logger.LogInformation("PII cleanup: anonymizing {Count} records.", oldRecords.Count);

        foreach (var record in oldRecords)
        {
            record.RecipientEmail = HashEmail(record.RecipientEmail);
            record.RecipientName = null;
        }

        await repository.SaveChangesAsync(ct);
        logger.LogInformation("PII cleanup completed: {Count} records anonymized.", oldRecords.Count);
    }

    private static string HashEmail(string email)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(email.ToLowerInvariant()));
        return $"anon_{Convert.ToHexStringLower(hash)[..16]}@anonymized.local";
    }
}
```

### 6.7 リトライ戦略（設計書 §8.4 準拠）

| パラメータ | 値 |
|-----------|-----|
| 最大リトライ回数 | 3 |
| 初回リトライ間隔 | 30 秒 |
| バックオフ倍率 | 2.0（30s → 60s → 120s） |
| リトライ対象 | ネットワークエラー、Azure ACS 一時エラー（429, 5xx） |
| リトライ非対象 | バリデーションエラー（400）、認証エラー（401, 403） |

### 6.8 Program.cs 更新

```csharp
// Kafka Consumer（Singleton）
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

// BackgroundService 登録
builder.Services.AddHostedService<MailEventConsumer>();
builder.Services.AddHostedService<MailRetryService>();
builder.Services.AddHostedService<PiiCleanupService>();
builder.Services.AddHostedService<OutboxPublisher>();
```

### 6.6 GDPR イベントハンドリング（設計書 §30 準拠 — GDPR コンプライアンス必須）

MailEventConsumer が受信した GDPR イベントは `ProcessEventAsync` 内でイベントタイプを判別し、以下の専用メソッドにディスパッチする。

#### `consent.revoked` イベント（マーケティング配信停止）

```csharp
// MailService に実装
public async Task ProcessConsentRevokedAsync(string userId, string consentType, CancellationToken ct = default)
{
    // consentType が "MARKETING" の場合のみ処理（他の同意種別は MailSendService の責務外）
    if (consentType != "MARKETING") return;

    // userId に紐づくメールアドレスを UserManagementService API で取得
    var userInfo = await _userInfoResolver.ResolveAsync(userId, ct)
        ?? throw new BusinessException($"User not found for consent revocation: userId={userId}");

    // mail_suppressions に reason='UNSUBSCRIBE' で登録（冪等: 既存エントリがあればスキップ）
    var existing = await _mailSuppressionRepository.FindByEmailAndReasonAsync(
        userInfo.Email, "UNSUBSCRIBE", ct);
    if (existing is null)
    {
        await _mailSuppressionRepository.AddAsync(new MailSuppression
        {
            Email = userInfo.Email,
            Reason = "UNSUBSCRIBE",
            Source = "consent.revoked"
        }, ct);
        await _mailSuppressionRepository.SaveChangesAsync(ct);
    }

    // PENDING のマーケティングメールを SKIPPED に更新
    // （recipient_user_id + status='PENDING' + template_type='MARKETING' のレコード）
    var pendingMails = await _mailLogRepository.FindPendingByUserIdAsync(userId, ct);
    foreach (var mail in pendingMails)
    {
        mail.Status = "SKIPPED";
        mail.ErrorMessage = "Consent revoked: MARKETING";
    }
    await _mailLogRepository.SaveChangesAsync(ct);

    _logger.LogInformation("Consent revoked processed: UserId={UserId}, ConsentType={ConsentType}",
        userId, consentType);
}
```

#### `user.deleted` イベント（PII 仮名化 — GDPR 第 17 条）

```csharp
// MailService に実装
public async Task ProcessUserDeletedAsync(string userId, CancellationToken ct = default)
{
    // mail_logs で recipient_user_id = userId の全レコードを検索
    var records = await _mailLogRepository.FindByRecipientUserIdAsync(userId, ct);

    foreach (var record in records)
    {
        // recipient_email を SHA-256 ハッシュに置換
        record.RecipientEmail = HashEmail(record.RecipientEmail);
        record.RecipientName = null;       // 氏名は完全削除
        record.RecipientUserId = null;     // ユーザー ID を NULL 化

        // PENDING ステータスのメールは SKIPPED に変更
        if (record.Status == "PENDING")
        {
            record.Status = "SKIPPED";
            record.ErrorMessage = "User deleted (GDPR Art.17)";
        }
    }
    await _mailLogRepository.SaveChangesAsync(ct);

    // mail_suppressions で該当メールアドレスもハッシュ化
    // （バウンス/配信停止判定はハッシュベースで継続）
    var suppressions = await _mailSuppressionRepository.FindByUserEmailAsync(userId, ct);
    foreach (var suppression in suppressions)
    {
        suppression.Email = HashEmail(suppression.Email);
    }
    await _mailSuppressionRepository.SaveChangesAsync(ct);

    _logger.LogInformation("User deleted processed (PII anonymized): UserId={UserId}, Records={Count}",
        userId, records.Count);
}

private static string HashEmail(string email)
{
    var hash = System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(email.ToLowerInvariant()));
    return $"anon_{Convert.ToHexStringLower(hash)[..16]}@anonymized.local";
}
```

#### `user.processing-restricted` / `user.processing-unrestricted` イベント（GDPR 第 18 条）

```csharp
// MailService に実装
public async Task ProcessUserProcessingRestrictedAsync(string userId, CancellationToken ct = default)
{
    // userId に紐づくメールアドレスを取得
    var userInfo = await _userInfoResolver.ResolveAsync(userId, ct)
        ?? throw new BusinessException($"User not found for processing restriction: userId={userId}");

    // mail_suppressions に reason='UNSUBSCRIBE' で登録（マーケティング配信停止）
    var existing = await _mailSuppressionRepository.FindByEmailAndReasonAsync(
        userInfo.Email, "UNSUBSCRIBE", ct);
    if (existing is null)
    {
        await _mailSuppressionRepository.AddAsync(new MailSuppression
        {
            Email = userInfo.Email,
            Reason = "UNSUBSCRIBE",
            Source = "user.processing-restricted"
        }, ct);
        await _mailSuppressionRepository.SaveChangesAsync(ct);
    }

    // PENDING のマーケティングメールを SKIPPED に更新
    // 注意: トランザクションメール（注文確認、パスワードリセット等）は処理制限中でも送信可能
    //       （GDPR 第 18 条: 契約履行に必要な処理は制限対象外）
    var pendingMails = await _mailLogRepository.FindPendingByUserIdAsync(userId, ct);
    foreach (var mail in pendingMails)
    {
        mail.Status = "SKIPPED";
        mail.ErrorMessage = "User processing restricted (GDPR Art.18)";
    }
    await _mailLogRepository.SaveChangesAsync(ct);

    _logger.LogInformation("Processing restriction applied: UserId={UserId}", userId);
}

public async Task ProcessUserProcessingUnrestrictedAsync(string userId, CancellationToken ct = default)
{
    // userId に紐づくメールアドレスを取得
    var userInfo = await _userInfoResolver.ResolveAsync(userId, ct)
        ?? throw new BusinessException($"User not found for processing unrestriction: userId={userId}");

    // mail_suppressions から該当エントリを削除（マーケティング配信再開）
    var suppression = await _mailSuppressionRepository.FindByEmailAndReasonAsync(
        userInfo.Email, "UNSUBSCRIBE", ct);
    if (suppression is not null)
    {
        await _mailSuppressionRepository.RemoveAsync(suppression, ct);
        await _mailSuppressionRepository.SaveChangesAsync(ct);
    }

    _logger.LogInformation("Processing restriction removed: UserId={UserId}", userId);
}
```

#### GDPR イベントで追加が必要な Repository メソッド

以下のメソッドを Phase 3 の各 Repository インターフェース・実装に追加すること:

| Repository | 追加メソッド | 用途 |
|-----------|------------|------|
| `IMailLogRepository` | `FindByRecipientUserIdAsync(string userId, CancellationToken ct)` | `user.deleted` イベントでの PII 仮名化対象検索 |
| `IMailLogRepository` | `FindPendingByUserIdAsync(string userId, CancellationToken ct)` | PENDING マーケティングメールの SKIPPED 更新 |
| `IMailSuppressionRepository` | `FindByEmailAndReasonAsync(string email, string reason, CancellationToken ct)` | 配信停止エントリの冪等チェック |
| `IMailSuppressionRepository` | `FindByUserEmailAsync(string userId, CancellationToken ct)` | `user.deleted` イベントでの配信停止エントリ仮名化 |
| `IMailSuppressionRepository` | `RemoveAsync(MailSuppression suppression, CancellationToken ct)` | `user.processing-unrestricted` での配信停止解除 |

### Phase 6 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] MailEventConsumer: 12 種のイベントタイプを `SupportedEvents` に定義（メール送信 7 種 + 旧メール変更 1 種 + GDPR 4 種）
- [ ] MailEventConsumer: `IServiceScopeFactory` で Scoped サービスを取得
- [ ] MailEventConsumer: `stoppingToken` を全下位呼び出しに伝搬
- [ ] MailEventConsumer: `consumer.Commit(result)` でオフセットコミット（`EnableAutoCommit = false`）
- [ ] MailEventConsumer: `ConsumeException` を個別キャッチ（DLT 転送検討コメント付き）
- [ ] MailEventConsumer: 未知イベントタイプを無視（ログのみ）
- [ ] MailRetryService: 動的バックオフ（対象なし: 5s、対象あり: 100ms）
- [ ] MailRetryService: `maxRetryCount` を `IOptions<MailSettings>` から取得
- [ ] PiiCleanupService: 365 日（1 年）経過レコードの PII 匿名化（SHA-256 ハッシュ）— 設計書 §4.2 / §28.3 準拠
- [ ] PiiCleanupService: 日次実行（UTC 03:00）
- [ ] PiiCleanupService: `RecipientName` は完全削除（`null` 化）
- [ ] 全 BackgroundService: primary constructor による DI
- [ ] 全 BackgroundService: ログ出力にメッセージテンプレート形式を使用
- [ ] `Thread.Sleep()` の使用なし（`Task.Delay()` のみ）
- [ ] GDPR: `consent.revoked` ハンドラー — `consentType=MARKETING` のみ処理、`mail_suppressions` 登録（冪等）、PENDING マーケティングメール SKIPPED 化
- [ ] GDPR: `user.deleted` ハンドラー — `mail_logs` の PII 仮名化（SHA-256）、`recipient_name` null 化、`recipient_user_id` null 化、`mail_suppressions` のメールアドレスもハッシュ化
- [ ] GDPR: `user.processing-restricted` ハンドラー — マーケティング配信停止（トランザクションメールは送信継続）
- [ ] GDPR: `user.processing-unrestricted` ハンドラー — 配信停止エントリの削除
- [ ] GDPR: Phase 3 Repository に GDPR 用メソッド追加（`FindByRecipientUserIdAsync`, `FindPendingByUserIdAsync`, `FindByEmailAndReasonAsync`, `RemoveAsync`）
- [ ] Outbox パターン: `OutboxEvent` エンティティ定義（`outbox_events` テーブル、Phase 2 追加）
- [ ] Outbox パターン: `mail.sent` / `mail.send.failed` イベントを MailLog ステータス更新と同一トランザクションで Outbox に書き込み
- [ ] Outbox パターン: `OutboxPublisher` BackgroundService（動的バックオフ 100ms〜5s、`PENDING` → `PUBLISHED` 更新）
- [ ] Outbox パターン: `OutboxPublisher` を `AddHostedService` で登録
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 7: Redis キャッシュ連携

### 目的

テンプレートメタデータのキャッシュを Redis で管理し、Kafka イベント処理のパフォーマンスを向上させる。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `MailSendService/Services/TemplateService.cs` | 更新 | Redis キャッシュ付きテンプレート取得 |
| 2 | `MailSendService/Program.cs` | 更新 | Redis 接続登録 |

### 7.1 キャッシュ戦略

| キャッシュ対象 | キー | TTL | 無効化タイミング |
|-------------|-----|-----|------------|
| テンプレートメタデータ（名前→テンプレート） | `mail:template:{name}` | 5 分 | テンプレート更新/無効化時 |
| 配信停止チェック結果 | `mail:suppression:{email_hash}` | 1 分 | 配信停止リスト変更時 |

> **注記**: 設計書 §12.3 — 顧客情報（UserManagementService API）はキャッシュしない（トランザクションメールは最新情報で送信すべき）。

### 7.2 Redis 接続登録

```csharp
// Program.cs
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "MailSendService:";
});
```

### 7.3 TemplateService キャッシュ統合

```csharp
public class TemplateService(
    IMailTemplateRepository templateRepository,
    IDistributedCache cache,
    ILogger<TemplateService> logger) : ITemplateService
{
    private const int CacheTtlMinutes = 5;

    public async Task<RenderedMail> RenderAsync(
        string templateName,
        Dictionary<string, object> variables,
        CancellationToken ct = default)
    {
        // 1. Redis キャッシュからテンプレート取得
        var cacheKey = $"mail:template:{templateName}";
        var cached = await cache.GetStringAsync(cacheKey, ct);
        MailTemplate? template;

        if (cached is not null)
        {
            template = JsonSerializer.Deserialize<MailTemplate>(cached);
        }
        else
        {
            template = await templateRepository.FindActiveByNameAsync(templateName, ct)
                ?? throw new TemplateNotFoundException(templateName);

            await cache.SetStringAsync(cacheKey,
                JsonSerializer.Serialize(template),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheTtlMinutes)
                }, ct);
        }

        // 2. テンプレートレンダリング（変数置換）
        var subject = ReplaceVariables(template!.Subject, variables);
        var htmlBody = ReplaceVariables(template.HtmlBody ?? string.Empty, variables);
        var textBody = template.TextBody is not null
            ? ReplaceVariables(template.TextBody, variables) : null;

        return new RenderedMail(subject, htmlBody, textBody);
    }

    // テンプレート更新時にキャッシュ無効化
    public async Task<MailTemplateResponse> UpdateAsync(
        string id, TemplateUpdateRequest request, CancellationToken ct = default)
    {
        var template = await templateRepository.FindByIdAsync(id, ct)
            ?? throw new TemplateNotFoundException(id);

        // ... 更新処理 ...

        // キャッシュ無効化
        await cache.RemoveAsync($"mail:template:{template.Name}", ct);

        return ToResponse(template);
    }

    private static string ReplaceVariables(string template, Dictionary<string, object> variables)
    {
        var result = template;
        foreach (var (key, value) in variables)
        {
            result = result.Replace($"{{{{{key}}}}}", value?.ToString() ?? string.Empty);
        }
        return result;
    }
}
```

### 7.4 配信停止チェックキャッシュ（設計書 §12.3 準拠）

メール送信前の配信停止チェック結果を Redis にキャッシュする（TTL: 1 分）。メールアドレスのハッシュ値をキーに使用し、PII をキャッシュキーに含めない。

```csharp
// MailService 内の配信停止チェックで使用
private async Task<bool> IsSuppressionCachedAsync(string email, CancellationToken ct)
{
    var emailHash = Convert.ToHexStringLower(
        System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(email.ToLowerInvariant())))[..16];
    var cacheKey = $"mail:suppression:{emailHash}";

    var cached = await _cache.GetStringAsync(cacheKey, ct);
    if (cached is not null)
        return cached == "1";  // "1" = 配信停止中

    // DB 確認
    var isSuppressed = await _mailSuppressionRepository.ExistsByEmailAsync(email, ct);

    // キャッシュに登録（TTL: 1 分）
    await _cache.SetStringAsync(cacheKey,
        isSuppressed ? "1" : "0",
        new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
        }, ct);

    return isSuppressed;
}
```

配信停止リスト変更時（`consent.revoked`, `user.processing-restricted`, `user.processing-unrestricted`）にキャッシュを無効化する:

```csharp
// GDPR イベントハンドラー内でキャッシュ無効化
private async Task InvalidateSuppressionCacheAsync(string email, CancellationToken ct)
{
    var emailHash = Convert.ToHexStringLower(
        System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(email.ToLowerInvariant())))[..16];
    await _cache.RemoveAsync($"mail:suppression:{emailHash}", ct);
}
```

### Phase 7 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] Redis: `AddStackExchangeRedisCache` で登録（`IDistributedCache`）
- [ ] テンプレートキャッシュ: TTL 5 分
- [ ] テンプレート更新時: `cache.RemoveAsync` でキャッシュ無効化
- [ ] 配信停止チェックキャッシュ: キー `mail:suppression:{email_hash}`、TTL 1 分
- [ ] 配信停止キャッシュ: メールアドレスの SHA-256 ハッシュをキーに使用（PII をキャッシュキーに含めない）
- [ ] 配信停止キャッシュ: 配信停止リスト変更時（GDPR イベント）にキャッシュ無効化
- [ ] 顧客情報: キャッシュしない（最新情報で送信）
- [ ] Redis 接続文字列: 環境変数 / `dotnet user-secrets` で管理（ハードコード禁止）
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 8: 認証・認可・セキュリティ

### 目的

設計書 §13（セキュリティ設計）および AGENTS.md §5 に基づき、JWT 認証、認可ポリシー、セキュリティヘッダー、グローバル例外ハンドラーを実装する。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `MailSendService/Infrastructure/Middleware/SecurityHeadersMiddleware.cs` | 作成 | セキュリティヘッダーミドルウェア |
| 2 | `MailSendService/Infrastructure/Middleware/SecurityHeadersMiddlewareExtensions.cs` | 作成 | `UseSecurityHeaders()` 拡張メソッド |
| 3 | `MailSendService/Infrastructure/Middleware/CorrelationIdMiddleware.cs` | 作成 | Correlation ID ミドルウェア |
| 4 | `MailSendService/Infrastructure/Middleware/CorrelationIdMiddlewareExtensions.cs` | 作成 | `UseCorrelationId()` 拡張メソッド |
| 5 | `MailSendService/Program.cs` | 更新 | 認証・認可・セキュリティヘッダー・グローバル例外ハンドラー登録 |

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

| ポリシー名 | 適用対象 | 条件 |
|-----------|---------|------|
| `AdminOnly` | `/admin/mail/**`（統計以外） | `RequireRole("Admin")` |
| `AdminOrManager` | `GET /admin/mail/stats` | `RequireRole("Admin", "Manager")` |
| `FallbackPolicy` | 全エンドポイント（デフォルト） | `RequireAuthenticatedUser()` |

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("AdminOrManager", p => p.RequireRole("Admin", "Manager"));
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

### 8.3 セキュリティヘッダーミドルウェア

| ヘッダー | 値 |
|---------|---|
| `X-Content-Type-Options` | `nosniff` |
| `X-Frame-Options` | `DENY` |
| `Content-Security-Policy` | `default-src 'self'` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` |

```csharp
namespace MailSendService.Infrastructure.Middleware;

public class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ContentTypeOptions = "nosniff";
    private const string FrameOptions = "DENY";
    private const string ContentSecurityPolicy = "default-src 'self'";
    private const string ReferrerPolicy = "strict-origin-when-cross-origin";
    private const string PermissionsPolicy = "camera=(), microphone=(), geolocation=()";

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Append("X-Content-Type-Options", ContentTypeOptions);
            context.Response.Headers.Append("X-Frame-Options", FrameOptions);
            context.Response.Headers.Append("Content-Security-Policy", ContentSecurityPolicy);
            context.Response.Headers.Append("Referrer-Policy", ReferrerPolicy);
            context.Response.Headers.Append("Permissions-Policy", PermissionsPolicy);
            return Task.CompletedTask;
        });

        await next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
        => builder.UseMiddleware<SecurityHeadersMiddleware>();
}
```

### 8.4 Correlation ID ミドルウェア

```csharp
namespace MailSendService.Infrastructure.Middleware;

public class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        context.Items["CorrelationId"] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Append(HeaderName, correlationId);
            return Task.CompletedTask;
        });

        using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
        => builder.UseMiddleware<CorrelationIdMiddleware>();
}
```

### 8.5 テストメール送信レート制限（設計書 §7.1 準拠）

```csharp
// Program.cs — テストメール送信のレート制限（1 時間に 5 通まで）
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("test-mail", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromHours(1);
        limiter.QueueLimit = 0;
    });
});

// ミドルウェアパイプライン（UseAuthorization の後に配置）
app.UseRateLimiter();
```

### 8.6 グローバル例外ハンドラー（設計書 §22.2 準拠）

```csharp
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var error = feature?.Error;

        if (error is not (TemplateNotFoundException or RateLimitExceededException
            or SuppressedRecipientException or InvalidEmailAddressException))
        {
            logger.LogError(error, "Unhandled exception: {Message}", error?.Message);
        }
        else
        {
            logger.LogWarning("Handled exception: {ExceptionType} - {Message}",
                error.GetType().Name, error.Message);
        }

        var problem = error switch
        {
            TemplateNotFoundException       => TypedResults.Problem(error.Message, statusCode: 404),
            RateLimitExceededException      => TypedResults.Problem("送信頻度制限を超過しました", statusCode: 429),
            SuppressedRecipientException    => TypedResults.Problem("配信停止済みのアドレスです", statusCode: 422),
            InvalidEmailAddressException    => TypedResults.Problem(error.Message, statusCode: 422),
            MailSendFailedException         => TypedResults.Problem("メール送信に失敗しました", statusCode: 502),
            UserInfoResolutionException     => TypedResults.Problem("ユーザー情報の取得に失敗しました", statusCode: 502),
            _                               => TypedResults.Problem("内部エラーが発生しました", statusCode: 500)
        };

        await problem.ExecuteAsync(context);
    });
});
```

### 8.6 ミドルウェアパイプライン順序（AGENTS.md §11.3 厳守）

```
1. UseExceptionHandler()          ← 最外層で全例外をキャッチ
2. UseHsts()                      ← HSTS ヘッダー
3. UseHttpsRedirection()          ← HTTPS 強制
4. UseSecurityHeaders()           ← セキュリティヘッダー
5. UseCorrelationId()             ← Correlation ID
6. UseSerilogRequestLogging()     ← リクエストログ
7. UseAuthentication()            ← JWT 認証
8. UseAuthorization()             ← 認可（Authentication の後に必ず配置）
9. UseRateLimiter()               ← レート制限（認証後に配置し、ユーザー単位の制限を可能に）
10. MapMailEndpoints()             ← エンドポイント
11. MapHealthChecks()             ← ヘルスチェック（AllowAnonymous）
```

### Phase 8 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] JWT 認証: `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey` が全て `true`
- [ ] JWT 認証: `ClockSkew = TimeSpan.FromMinutes(5)`
- [ ] 認可: `FallbackPolicy` に `RequireAuthenticatedUser()` 設定
- [ ] 認可: `AdminOnly`, `AdminOrManager` ポリシー定義済み
- [ ] `/health`, `/health/ready`: `.AllowAnonymous()` 設定
- [ ] SecurityHeadersMiddleware: ヘッダー値が定数化（リクエストごとのアロケーション回避）
- [ ] CorrelationIdMiddleware: `LogContext.PushProperty` で Serilog コンテキストに付与
- [ ] CorrelationIdMiddleware: `context.Items["CorrelationId"]` に格納
- [ ] グローバル例外ハンドラー: RFC 9457 Problem Details 形式
- [ ] グローバル例外ハンドラー: スタックトレースがクライアントに返却されないこと
- [ ] グローバル例外ハンドラー: `catch` ブロックで `ILogger.LogError(ex, ...)` で記録
- [ ] ミドルウェア順序: `UseAuthentication()` → `UseAuthorization()` の順序厳守
- [ ] ミドルウェア順序: `UseExceptionHandler()` がパイプライン最上位
- [ ] `appsettings.json`: `"DetailedErrors": false`
- [ ] `Kestrel.AddServerHeader = false`
- [ ] 秘密情報のハードコードなし
- [ ] レート制限: テストメール送信エンドポイント（`/admin/mail/test`）に `test-mail` ポリシー（1 時間 5 通上限）
- [ ] レート制限: `UseRateLimiter()` が `UseAuthorization()` の後に配置
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 9: テスト

### 目的

AGENTS.md §9 テスト規約に基づき、Unit Test、Integration Test（WebApplicationFactory）、DB スライステスト（Testcontainers）を実装する。分岐カバレッジ 80% 以上を達成する。

### 作成ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `MailSendService.Tests/MailSendService.Tests.csproj` | 作成 | テストプロジェクト |
| 2 | `MailSendService.Tests/Services/MailServiceTests.cs` | 作成 | MailService 単体テスト |
| 3 | `MailSendService.Tests/Services/TemplateServiceTests.cs` | 作成 | TemplateService 単体テスト |
| 4 | `MailSendService.Tests/Services/AzureEmailSenderTests.cs` | 作成 | AzureEmailSender 単体テスト |
| 5 | `MailSendService.Tests/Services/UserInfoResolverTests.cs` | 作成 | UserInfoResolver 単体テスト |
| 6 | `MailSendService.Tests/Services/SuppressionServiceTests.cs` | 作成 | SuppressionService 単体テスト |
| 7 | `MailSendService.Tests/Consumers/MailEventConsumerTests.cs` | 作成 | Kafka Consumer 単体テスト |
| 8 | `MailSendService.Tests/Consumers/MailRetryServiceTests.cs` | 作成 | MailRetryService 単体テスト |
| 9 | `MailSendService.Tests/Consumers/PiiCleanupServiceTests.cs` | 作成 | PiiCleanupService 単体テスト |
| 10 | `MailSendService.Tests/Endpoints/MailEndpointsTests.cs` | 作成 | WebApplicationFactory 統合テスト |
| 11 | `MailSendService.Tests/Repositories/MailLogRepositoryTests.cs` | 作成 | Testcontainers DB テスト |
| 12 | `MailSendService.Tests/Repositories/MailTemplateRepositoryTests.cs` | 作成 | Testcontainers DB テスト |
| 13 | `MailSendService.Tests/Validators/TestMailRequestValidatorTests.cs` | 作成 | バリデーター単体テスト |
| 14 | `MailSendService.Tests/Validators/TemplateCreateRequestValidatorTests.cs` | 作成 | バリデーター単体テスト |

### 9.1 テストプロジェクト設定

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="NSubstitute" Version="5.*" />
    <PackageReference Include="Shouldly" Version="4.*" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.*" />
    <PackageReference Include="Testcontainers.PostgreSql" Version="4.*" />
    <PackageReference Include="coverlet.collector" Version="6.*" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../MailSendService/MailSendService.csproj" />
  </ItemGroup>
</Project>
```

### 9.2 MailServiceTests（単体テスト）

```csharp
namespace MailSendService.Tests.Services;

[Trait("Category", "Unit")]
public class MailServiceTests
{
    private readonly IMailLogRepository _mailLogRepository;
    private readonly IMailTemplateRepository _templateRepository;
    private readonly IMailSuppressionRepository _suppressionRepository;
    private readonly IEmailSender _emailSender;
    private readonly ITemplateService _templateService;
    private readonly IUserInfoResolver _userInfoResolver;
    private readonly ILogger<MailService> _logger;
    private readonly MailService _sut;

    public MailServiceTests()
    {
        _mailLogRepository = Substitute.For<IMailLogRepository>();
        _templateRepository = Substitute.For<IMailTemplateRepository>();
        _suppressionRepository = Substitute.For<IMailSuppressionRepository>();
        _emailSender = Substitute.For<IEmailSender>();
        _templateService = Substitute.For<ITemplateService>();
        _userInfoResolver = Substitute.For<IUserInfoResolver>();
        _logger = Substitute.For<ILogger<MailService>>();

        _sut = new MailService(
            _mailLogRepository,
            _suppressionRepository,
            _emailSender,
            _templateService,
            _userInfoResolver,
            _logger);
    }

    [Fact]
    public async Task Should_SendMail_When_ValidEventReceived()
    {
        // Arrange
        var eventId = Guid.NewGuid().ToString();
        _mailLogRepository.FindByEventIdAsync(eventId, default)
            .Returns((MailLog?)null);
        _suppressionRepository.IsEmailSuppressedAsync(Arg.Any<string>(), default)
            .Returns(false);
        _templateService.RenderAsync(Arg.Any<string>(), Arg.Any<Dictionary<string, object>>(), default)
            .Returns(new RenderedMail("Subject", "<p>Body</p>", "Body"));
        _emailSender.SendAsync(Arg.Any<MailMessage>(), default)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ProcessEventAsync(
            "user.registered", eventId, "corr-123",
            """{"userId":"u1","email":"test@example.com","verificationToken":"tok123"}""",
            default);

        // Assert
        await _mailLogRepository.Received(1).AddAsync(Arg.Any<MailLog>(), default);
        await _emailSender.Received(1).SendAsync(Arg.Any<MailMessage>(), default);
    }

    [Fact]
    public async Task Should_SkipSending_When_DuplicateEventId()
    {
        // Arrange
        var eventId = Guid.NewGuid().ToString();
        _mailLogRepository.FindByEventIdAsync(eventId, default)
            .Returns(new MailLog { EventId = eventId, Status = "SENT" });

        // Act
        await _sut.ProcessEventAsync(
            "user.registered", eventId, "corr-123", "{}", default);

        // Assert
        await _emailSender.DidNotReceive().SendAsync(Arg.Any<MailMessage>(), default);
    }

    [Fact]
    public async Task Should_SkipSending_When_RecipientSuppressed()
    {
        // Arrange
        var eventId = Guid.NewGuid().ToString();
        _mailLogRepository.FindByEventIdAsync(eventId, default)
            .Returns((MailLog?)null);
        _suppressionRepository.IsEmailSuppressedAsync("suppressed@example.com", default)
            .Returns(true);

        // Act & Assert
        var act = async () => await _sut.ProcessEventAsync(
            "user.registered", eventId, "corr-123",
            """{"userId":"u1","email":"suppressed@example.com","verificationToken":"tok"}""",
            default);
        await Should.ThrowAsync<SuppressedRecipientException>(act);
    }

    [Fact]
    public async Task Should_ThrowNotFoundException_When_MailLogIdNotFound()
    {
        // Arrange
        _mailLogRepository.FindByIdAsync("nonexistent", default)
            .Returns((MailLog?)null);

        // Act & Assert
        var act = async () => await _sut.GetLogByIdAsync("nonexistent");
        var ex = await Should.ThrowAsync<TemplateNotFoundException>(act);
        ex.Message.ShouldContain("nonexistent");
    }
}
```

### 9.3 MailEndpointsTests（統合テスト）

```csharp
namespace MailSendService.Tests.Endpoints;

[Trait("Category", "Integration")]
public class MailEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public MailEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // テスト用サービス差し替え
            });
        }).CreateClient();
    }

    [Fact]
    public async Task Should_Return401_When_NoAuthToken()
    {
        // Act
        var response = await _client.GetAsync("/admin/mail/logs");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_Return403_When_UserRoleAccessAdmin()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateToken("User"));

        // Act
        var response = await _client.GetAsync("/admin/mail/logs");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
```

### 9.4 MailLogRepositoryTests（Testcontainers）

```csharp
namespace MailSendService.Tests.Repositories;

[Trait("Category", "Repository")]
public class MailLogRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .Build();

    private AppDbContext _context = null!;
    private MailLogRepository _sut = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        _context = new AppDbContext(options, TimeProvider.System);
        await _context.Database.EnsureCreatedAsync();
        _sut = new MailLogRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Should_AddAndFindById()
    {
        // Arrange
        var mailLog = new MailLog
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = "user.registered",
            RecipientEmail = "test@example.com",
            RecipientName = "Test User",
            Subject = "テスト",
            Status = "PENDING"
        };

        // Act
        await _sut.AddAsync(mailLog);
        await _sut.SaveChangesAsync();
        var found = await _sut.FindByIdAsync(mailLog.Id);

        // Assert
        found.ShouldNotBeNull();
        found.EventId.ShouldBe(mailLog.EventId);
        found.RecipientEmail.ShouldBe("test@example.com");
    }

    [Fact]
    public async Task Should_EnforceUniqueEventId()
    {
        // Arrange
        var eventId = Guid.NewGuid().ToString();
        var log1 = new MailLog { EventId = eventId, EventType = "test", Status = "SENT",
            RecipientEmail = "a@test.com", Subject = "s" };
        var log2 = new MailLog { EventId = eventId, EventType = "test", Status = "SENT",
            RecipientEmail = "b@test.com", Subject = "s" };

        await _sut.AddAsync(log1);
        await _sut.SaveChangesAsync();

        // Act & Assert
        await _sut.AddAsync(log2);
        var act = async () => await _sut.SaveChangesAsync();
        await Should.ThrowAsync<DbUpdateException>(act);
    }
}
```

### 9.5 テストカテゴリ

| カテゴリ | 実行コマンド | 対象 |
|---------|------------|------|
| Unit | `dotnet test --filter Category=Unit` | Service, Validator, Consumer |
| Repository | `dotnet test --filter Category=Repository` | Testcontainers DB テスト |
| Integration | `dotnet test --filter Category=Integration` | WebApplicationFactory テスト |
| Security | `dotnet test --filter Category=Security` | 認証・認可テスト |

### Phase 9 完了チェックリスト

- [ ] `dotnet test` — 全テスト通過
- [ ] 分岐カバレッジ 80% 以上: `dotnet test --collect:"XPlat Code Coverage"`
- [ ] テストメソッド命名: `Should_期待結果_When_条件` パターン
- [ ] 全テスト: AAA パターン（Arrange / Act / Assert コメント付き）
- [ ] 全テスト: `[Trait("Category", "...")]` 付与
- [ ] モック: NSubstitute（`Substitute.For<T>()`, `Received()`, `DidNotReceive()`）
- [ ] アサーション: Shouldly（`ShouldBe()`, `ShouldNotBeNull()`, `ShouldContain()`）
- [ ] 異常系テスト: 正常系と同等以上のテストケース数
- [ ] 例外テスト: `Should.ThrowAsync<TException>` で型 + メッセージ検証
- [ ] DB テスト: Testcontainers.PostgreSql + `IAsyncLifetime`
- [ ] 統合テスト: `WebApplicationFactory<Program>` 使用
- [ ] テスト用個人情報: ダミーデータのみ（`test@example.com` 等）
- [ ] `CancellationToken` テスト: キャンセル時の動作テスト含む
- [ ] MailServiceTests: イベント重複チェック（べき等性テスト）
- [ ] MailServiceTests: 配信停止チェックテスト
- [ ] MailServiceTests: レート制限テスト
- [ ] MailServiceTests: GDPR `consent.revoked` — MARKETING 同意撤回時の配信停止登録 + PENDING メール SKIPPED 化テスト
- [ ] MailServiceTests: GDPR `consent.revoked` — MARKETING 以外の同意種別は無視テスト
- [ ] MailServiceTests: GDPR `user.deleted` — PII 仮名化（SHA-256）テスト
- [ ] MailServiceTests: GDPR `user.deleted` — PENDING メール SKIPPED 化テスト
- [ ] MailServiceTests: GDPR `user.processing-restricted` — マーケティング配信停止テスト
- [ ] MailServiceTests: GDPR `user.processing-unrestricted` — 配信停止解除テスト
- [ ] PiiCleanupServiceTests: 365 日（1 年）経過レコードの匿名化テスト
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 10: 可観測性（Observability）

### 目的

AGENTS.md §11.2 可観測性設計に基づき、構造化ログ（Serilog）、分散トレーシング（OpenTelemetry）、ヘルスチェック、カスタムメトリクスを統合する。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `MailSendService/Program.cs` | 更新 | Serilog / OpenTelemetry / HealthChecks 登録 |
| 2 | `MailSendService/Infrastructure/Metrics/MailMetrics.cs` | 作成 | カスタムメトリクス定義 |

### 10.1 構造化ログ（Serilog）

```csharp
// Program.cs — Serilog 設定
builder.Host.UseSerilog((context, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "MailSendService")
        .WriteTo.Console(new CompactJsonFormatter()));
```

### 10.2 OpenTelemetry

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource("MailSendService.*"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("MailSendService.Metrics"));
```

### 10.3 ヘルスチェック

```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString!, name: "postgresql", tags: ["ready"])
    .AddRedis(redisConnectionString!, name: "redis", tags: ["ready"])
    .AddCheck("kafka", new KafkaHealthCheck(kafkaBootstrapServers), tags: ["ready"]);

// Liveness（常に 200）
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

// Readiness（PostgreSQL + Redis + Kafka 疎通確認）
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();
```

### 10.4 カスタムメトリクス（設計書 §16 準拠）

```csharp
using System.Diagnostics.Metrics;

namespace MailSendService.Infrastructure.Metrics;

public class MailMetrics
{
    private readonly Counter<long> _mailSentCounter;
    private readonly Counter<long> _mailFailedCounter;
    private readonly Counter<long> _mailSkippedCounter;
    private readonly Counter<long> _mailRetryCounter;
    private readonly Counter<long> _mailEventConsumedCounter;
    private readonly Histogram<double> _sendDurationHistogram;

    public MailMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("MailSendService.Metrics");

        _mailSentCounter = meter.CreateCounter<long>(
            "mail.sent.count", "mails", "メール送信成功数");
        _mailFailedCounter = meter.CreateCounter<long>(
            "mail.failed.count", "mails", "メール送信失敗数");
        _mailSkippedCounter = meter.CreateCounter<long>(
            "mail.skipped.count", "mails", "メール送信スキップ数");
        _mailRetryCounter = meter.CreateCounter<long>(
            "mail.retry.count", "retries", "リトライ実行回数");
        _mailEventConsumedCounter = meter.CreateCounter<long>(
            "mail.event.consumed.count", "events", "受信イベント総数");
        _sendDurationHistogram = meter.CreateHistogram<double>(
            "mail.send.duration", "ms", "メール送信処理時間");
    }

    public void RecordMailSent(string eventType)
        => _mailSentCounter.Add(1, new KeyValuePair<string, object?>("event_type", eventType));

    public void RecordMailFailed(string eventType, string reason)
        => _mailFailedCounter.Add(1,
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("reason", reason));

    public void RecordMailSkipped(string eventType, string reason)
        => _mailSkippedCounter.Add(1,
            new KeyValuePair<string, object?>("event_type", eventType),
            new KeyValuePair<string, object?>("reason", reason));

    public void RecordMailRetry(string eventType)
        => _mailRetryCounter.Add(1,
            new KeyValuePair<string, object?>("event_type", eventType));

    public void RecordEventConsumed(string eventType)
        => _mailEventConsumedCounter.Add(1,
            new KeyValuePair<string, object?>("event_type", eventType));

    public void RecordSendDuration(double milliseconds, string eventType)
        => _sendDurationHistogram.Record(milliseconds,
            new KeyValuePair<string, object?>("event_type", eventType));
}
```

### 10.5 ログレベル設定

| 環境 | Default | Microsoft.AspNetCore | SkiShop | EF Core |
|------|---------|---------------------|---------|---------|
| Production | `Warning` | `Warning` | `Information` | `Warning` |
| Development | `Information` | `Debug` | `Debug` | `Information` |

### 10.6 アラート条件（設計書 §14.3 準拠）

以下のアラート条件をメトリクスに基づいて設定する（監視基盤側の設定）:

| 条件 | 重要度 | 対応 |
|------|--------|------|
| メール送信成功率 < 95%（直近 1 時間） | CRITICAL | 即時調査 |
| 未送信メール（PENDING）が 100 件超 | WARNING | キュー詰まり確認 |
| リトライ上限到達（FAILED）が 10 件/時超 | CRITICAL | ACS ステータス確認 |

### Phase 10 完了チェックリスト

- [ ] `dotnet build` — 警告なし成功
- [ ] Serilog: `CompactJsonFormatter` で JSON 形式ログ出力
- [ ] Serilog: `ServiceName` プロパティを全ログに付与（`"MailSendService"`）
- [ ] Serilog: `CorrelationId` が全リクエストログに含まれること
- [ ] OpenTelemetry: ASP.NET Core + HttpClient + EF Core Instrumentation 設定済み
- [ ] OpenTelemetry: カスタム `MailSendService.Metrics` メーターを `AddMeter` で登録
- [ ] HealthChecks: `/health`（Liveness） — `AllowAnonymous()` + `Predicate = _ => false`
- [ ] HealthChecks: `/health/ready`（Readiness） — PostgreSQL + Redis + Kafka
- [ ] カスタムメトリクス: 送信成功/失敗/スキップカウンター + リトライカウンター + イベント受信カウンター + 送信時間ヒストグラム（設計書 §14.1 の 5 メトリクス完全実装）
- [ ] ログ出力: メッセージテンプレート形式（文字列補間禁止）
- [ ] ログ出力: PII 情報をログに出力しない
- [ ] 本番: EF Core SQL ログが `Warning` レベル
- [ ] 本番: `DetailedErrors: false`
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## Phase 11: Docker / デプロイ

### 目的

AGENTS.md §12.5 Dockerfile 規約に基づき、マルチステージビルド Dockerfile、.dockerignore、AppHost 統合を実装する。

### 作成・更新ファイル一覧

| # | ファイルパス | 操作 | 内容 |
|---|------------|------|------|
| 1 | `MailSendService/Dockerfile` | 作成 | マルチステージビルド |
| 2 | `MailSendService/.dockerignore` | 作成 | Docker ビルド除外設定 |
| 3 | `AppHost/Program.cs` | 更新 | MailSendService サービス参照追加 |

### 11.1 Dockerfile

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["MailSendService/MailSendService.csproj", "MailSendService/"]
RUN dotnet restore "MailSendService/MailSendService.csproj"
COPY . .
WORKDIR "/src/MailSendService"
RUN dotnet publish "MailSendService.csproj" -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# 非 root ユーザー（必須）
RUN groupadd -r skishop && useradd -r -g skishop -d /app -s /sbin/nologin skishop
COPY --from=build --chown=skishop:skishop /app/publish .

USER skishop

# ランタイム設定
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "MailSendService.dll"]
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
**/node_modules/
**/*.Tests/
design-docs/
impl-plan/
```

### 11.3 AppHost 統合

```csharp
// AppHost/Program.cs
var mailSendService = builder.AddProject<Projects.MailSendService>("mailsend-service")
    .WithReference(postgres)
    .WithReference(redis)
    .WithReference(kafka);
```

### 11.4 .NET Aspire サービス参照

| 依存リソース | 参照名 | 用途 |
|------------|-------|------|
| PostgreSQL | `postgres` → `mailsenddb` | メール送信ログ / テンプレート / 配信停止リスト |
| Redis | `redis` | テンプレートキャッシュ |
| Kafka | `kafka` | イベント消費（domain-events トピック） |

### Phase 11 完了チェックリスト

- [ ] `docker build .` — ビルド成功
- [ ] Dockerfile: マルチステージビルド（`sdk` → `aspnet`）
- [ ] Dockerfile: `FROM` タグはバージョン固定（`latest` 禁止）
- [ ] Dockerfile: `USER skishop`（非 root ユーザー）
- [ ] Dockerfile: `HEALTHCHECK` 設定済み（`/health` エンドポイント）
- [ ] Dockerfile: `EXPOSE 8080` のみ（不要なポート公開なし）
- [ ] .dockerignore: `bin/`, `obj/`, `.git/`, `*.md`, テストプロジェクト除外
- [ ] AppHost: `WithReference(postgres)`, `WithReference(redis)`, `WithReference(kafka)` 設定
- [ ] 環境変数: `ASPNETCORE_ENVIRONMENT=Production` 設定
- [ ] 秘密情報: Dockerfile / docker-compose に含まれていないこと
- [ ] **TODO/FIXME/HACK コメント残存チェック** — ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること
- [ ] **Mock/Stub/仮実装の残存チェック** — テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと
- [ ] **未実装メソッド・空メソッドチェック** — 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること

---

## 横断チェックリスト（全 Phase 共通）

全 Phase 完了後に以下の横断チェックを実施する。

### AGENTS.md 禁止事項チェック（grep コマンド）

```bash
# 1. 秘密情報のハードコードチェック
grep -r "Password\s*=\s*\"" --include="*.cs" MailSendService/

# 2. Console.WriteLine チェック
grep -r "Console\.Write" --include="*.cs" MailSendService/

# 3. SQL 文字列結合チェック
grep -r "FromSqlRaw.*\+" --include="*.cs" MailSendService/

# 4. .Result / .Wait() チェック（デッドロックリスク）
grep -rP "\.(Result|Wait)\(\)" --include="*.cs" MailSendService/

# 5. プロパティインジェクションチェック
grep -r "\[Inject\]" --include="*.cs" MailSendService/

# 6. Thread.Sleep() チェック
grep -r "Thread\.Sleep" --include="*.cs" MailSendService/

# 7. DateTime.Now チェック（DateTime.UtcNow / TimeProvider を使用すべき）
grep -r "DateTime\.Now[^U]" --include="*.cs" MailSendService/

# 8. new HttpClient() チェック（IHttpClientFactory を使用すべき）
grep -r "new HttpClient()" --include="*.cs" MailSendService/

# 9. 文字列補間ログチェック
grep -rP '_logger\.Log.*\$"' --include="*.cs" MailSendService/

# 10. プレリリースパッケージチェック
grep -r "preview\|beta\|-rc" --include="*.csproj" MailSendService/
```

### コーディング規約チェック

| # | チェック項目 | 対応 AGENTS.md セクション |
|---|------------|----------------------|
| 1 | 全 `async` メソッドに `CancellationToken ct = default` パラメータ | §4.5 |
| 2 | primary constructor による DI（プロパティインジェクション禁止） | §4.3 |
| 3 | `ILogger<T>` + メッセージテンプレート形式のログ出力 | §4 禁止事項 |
| 4 | Nullable Reference Types 有効（`<Nullable>enable</Nullable>`） | §4.8 |
| 5 | `TreatWarningsAsErrors` が `true` | §8.1 |
| 6 | EF Core エンティティ: `[Table]` / `[Column]` で snake_case 明示 | §10.3 |
| 7 | EF Core エンティティ: コレクション `= []` で初期化 | §10.3 |
| 8 | EF Core: 読み取り専用クエリに `AsNoTracking()` | §4.6 |
| 9 | record 型で DTO / Value Object を定義 | §4.4 |
| 10 | 例外は具体的な型でキャッチ（`catch (Exception)` 禁止） | §4.7 |
| 11 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 12 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 13 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

### セキュリティ規約チェック

| # | チェック項目 | 対応セクション |
|---|------------|-------------|
| 1 | 全エンドポイント: `RequireAuthorization()` または `AllowAnonymous()` 明示 | §5.4 |
| 2 | FallbackPolicy: `RequireAuthenticatedUser()` 設定 | §5.4 |
| 3 | 入力バリデーション: FluentValidation / Data Annotations | §5.1 |
| 4 | `FromSqlRaw` 文字列結合 SQL なし | §5.2 |
| 5 | スタックトレースのクライアント返却なし | §5.6 |
| 6 | `appsettings.json` に秘密情報なし | §5.5 |
| 7 | セキュリティヘッダー 5 種設定済み | §5.3 |
| 8 | CORS: `AllowAnyOrigin()` 禁止 | §5.3 |
| 9 | ログに PII / パスワード / トークン出力なし | §5.7 |
| 10 | `Kestrel.AddServerHeader = false` | §5.3 |
| 11 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 12 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 13 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

### テスト規約チェック

| # | チェック項目 | 対応セクション |
|---|------------|-------------|
| 1 | テストメソッド命名: `Should_X_When_Y` パターン | §9.2 |
| 2 | AAA パターン（Arrange / Act / Assert コメント付き） | §9.3 |
| 3 | 分岐カバレッジ 80% 以上 | §9.4 |
| 4 | 異常系テスト: 正常系以上のケース数 | §9 テスト規約 |
| 5 | Shouldly アサーション使用 | §9 テスト規約 |
| 6 | `[Trait("Category", "...")]` 付与 | §9 テスト規約 |
| 7 | テスト用個人情報: ダミーデータのみ | §9 テスト規約 |
| 8 | アサーションなしテスト禁止 | §9 テスト規約 |
| 9 | DB テスト: Testcontainers（InMemory Provider 禁止） | §9.1 |
| 10 | 統合テスト: `WebApplicationFactory<Program>` | §9.1 |
| 11 | **TODO/FIXME/HACK コメント残存チェック** | ソースコード内に `TODO`, `FIXME`, `HACK`, `UNDONE`, `XXX` コメントが残存していないこと。`grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" src/` で 0 件であること |
| 12 | **Mock/Stub/仮実装の残存チェック** | テスト用 Mock やスタブ実装が本番コードに混入していないこと。`NotImplementedException`, `throw new NotImplementedException()`, 仮のハードコード値（`"dummy"`, `"test"`, `"xxx"` 等）が本番コードに残存していないこと |
| 13 | **未実装メソッド・空メソッドチェック** | 空のメソッドボディ `{ }` や `=> throw new NotImplementedException()` が本番コードに残存していないこと。全パブリックメソッドが設計書通りに実装されていること |

### Phase 別ビルド・テストコマンド

| Phase | 確認コマンド |
|-------|-----------|
| Phase 1-4 | `dotnet build MailSendService/` |
| Phase 5 | `dotnet build MailSendService/` |
| Phase 6 | `dotnet build MailSendService/` |
| Phase 7 | `dotnet build MailSendService/` |
| Phase 8 | `dotnet build MailSendService/` |
| Phase 9 | `dotnet test MailSendService.Tests/ --collect:"XPlat Code Coverage"` |
| Phase 10 | `dotnet build MailSendService/` |
| Phase 11 | `docker build -f MailSendService/Dockerfile .` |
| 全体 | `dotnet test --collect:"XPlat Code Coverage"` + カバレッジ 80% 確認 |
