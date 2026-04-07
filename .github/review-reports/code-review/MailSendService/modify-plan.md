# MailSendService 修正計画書 (modify-plan.md)

**作成日**: 2026-04-07  
**最終更新**: 2025-06-06  
**対象**: `Services/MailSendService/`  
**ステータス**: ❌ Rejected → 修正必須  
**元レポート**: `check-report-1.md`

---

## 目次

1. [概要](#概要)
2. [P0: 即座対応 — Critical 指摘修正](#p0-即座対応--critical-指摘修正)
3. [P1: リリース前対応 — High 指摘修正](#p1-リリース前対応--high-指摘修正)
4. [P1 追加事項（check-report との照合により追記）](#p1-追加事項check-report-との照合により追記)
5. [P2: 次回リファクタリング — Medium 指摘修正](#p2-次回リファクタリング--medium-指摘修正)
6. [P2 追加事項（check-report との照合により追記）](#p2-追加事項check-report-との照合により追記)
7. [エスカレーション事項](#エスカレーション事項)
8. [修正後の検証手順](#修正後の検証手順)
9. [修正ファイル一覧](#修正ファイル一覧優先度順)
10. [サマリー](#サマリー)

---

## 概要

本ドキュメントは `check-report-1.md` で報告された課題に対する修正計画を記載する。
Critical 指摘 10 件、High 指摘 60 件（主要 20 件抜粋）の修正が必要。

### 修正優先度

| 優先度 | 件数 | 対応期限 |
|--------|------|---------|
| P0（Critical） | 10 件 | 即座対応（マージブロッカー） |
| P1（High 主要） | 20 件 | リリース前 |
| P2（Medium） | 6 件 | 次回リファクタリング |
| エスカレーション | 7 件 | 人間判断待ち |

### 照合結果サマリー

2025-06-06 の照合作業により、以下の追加事項を検出：

- **P1 追加**: 8 件（P1-13 〜 P1-20）
- **P2 追加**: 3 件（P2-4 〜 P2-6）
- **エスカレーション追加**: なし

---

## P0: 即座対応 — Critical 指摘修正

### P0-1: PII ログ漏洩の修正（LogMailSent / LogMailSuppressed）

**対象ファイル**: `Services/MailSendService/Services/MailService.cs`

**現状（L640-647）**:
```csharp
[LoggerMessage(Level = LogLevel.Information, Message = "メール送信完了: MailLogId={MailLogId}, Recipient={Recipient}")]
partial void LogMailSent(string mailLogId, string recipient);

[LoggerMessage(Level = LogLevel.Warning, Message = "送信抑制: Recipient={Recipient}, Reason={Reason}")]
partial void LogMailSuppressed(string recipient, string reason);
```

**問題点**: 
- メールアドレスが平文でログ出力 → GDPR 違反リスク

**修正方法**:

```csharp
// 1. マスキング関数を追加（既存の MaskEmailForOutbox を流用可能）
/// <summary>ログ出力用のメールアドレスマスキング。</summary>
private static string MaskEmailForLog(string email)
{
    var atIndex = email.IndexOf('@');
    return atIndex <= 0 ? "***" : $"{email[0]}***{email[atIndex..]}";
}

// 2. LoggerMessage の呼び出し箇所を修正
// L641 の呼び出し箇所:
LogMailSent(mailLog.Id, MaskEmailForLog(mailLog.RecipientEmail));

// L647 の呼び出し箇所:
LogMailSuppressed(MaskEmailForLog(recipientEmail), "Suppressed recipient");
```

**影響範囲**:
- `ProcessEventAsync` メソッド内の `LogMailSent` 呼び出し
- `ProcessEventAsync` メソッド内の `LogMailSuppressed` 呼び出し
- 関連する全ての `LogMailSent` / `LogMailSuppressed` 呼び出し箇所

---

### P0-2: OpenAPI 設定の追加

**対象ファイル**: `Services/MailSendService/Program.cs`

**現状**: 
- `AddOpenApi()` / `MapOpenApi()` が未実装（.NET 10 必須）

**修正方法**:

```csharp
// Program.cs の DI 登録セクションに追加（L113 付近、バリデーション登録の後）:
// --- DI: OpenAPI ドキュメント生成 ---
builder.Services.AddOpenApi();

// エンドポイント登録セクションに追加（L325 付近、MapMailEndpoints() の後）:
// --- OpenAPI エンドポイント ---
app.MapOpenApi();
```

**注意**: 
- 個別エンドポイントへの `.WithOpenApi()` は不要（.NET 10 ではサービスレベルで自動生成）

---

### P0-3: ConcurrencyException の HTTP 409 マッピング追加

**対象ファイル**: `Services/MailSendService/Program.cs`

**現状（L294-306）**:
```csharp
var problem = error switch
{
    TemplateNotFoundException       => TypedResults.Problem(error.Message, statusCode: 404),
    // ... 他の例外
    _                               => TypedResults.Problem("内部エラーが発生しました", statusCode: 500)
};
```

**問題点**: 
- `ConcurrencyException` が 500 を返す（正しくは 409）

**修正方法**:

```csharp
var problem = error switch
{
    TemplateNotFoundException       => TypedResults.Problem(error.Message, statusCode: 404),
    MailLogNotFoundException        => TypedResults.Problem(error.Message, statusCode: 404),
    RateLimitExceededException      => TypedResults.Problem("送信頻度制限を超過しました", statusCode: 429),
    SuppressedRecipientException    => TypedResults.Problem("配信停止済みのアドレスです", statusCode: 422),
    InvalidEmailAddressException    => TypedResults.Problem(error.Message, statusCode: 422),
    InvalidMailStatusException      => TypedResults.Problem(error.Message, statusCode: 422),
    DuplicateTemplateNameException  => TypedResults.Problem(error.Message, statusCode: 409),
    ConcurrencyException            => TypedResults.Problem(error.Message, statusCode: 409),  // ★追加
    MailSendFailedException         => TypedResults.Problem("メール送信に失敗しました", statusCode: 502),
    UserInfoResolutionException     => TypedResults.Problem("ユーザー情報の取得に失敗しました", statusCode: 502),
    _                               => TypedResults.Problem("内部エラーが発生しました", statusCode: 500)
};
```

---

### P0-4: JwtSettings に Data Annotations 追加

**対象ファイル**: `Services/MailSendService/Configurations/JwtSettings.cs`

**現状**:
```csharp
public record JwtSettings
{
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
}
```

**問題点**: 
- 必須項目のバリデーションなし
- 空または弱い秘密鍵を許容

**修正方法**:

```csharp
using System.ComponentModel.DataAnnotations;

namespace MailSendService.Configurations;

/// <summary>
/// JWT (JSON Web Token) 認証設定。appsettings.json の "Jwt" セクションにバインドされる。
/// </summary>
public record JwtSettings
{
    /// <summary>JWT 発行者（iss クレーム）。</summary>
    [Required(ErrorMessage = "JWT Issuer は必須です")]
    public string Issuer { get; init; } = string.Empty;

    /// <summary>JWT 対象者（aud クレーム）。</summary>
    [Required(ErrorMessage = "JWT Audience は必須です")]
    public string Audience { get; init; } = string.Empty;

    /// <summary>JWT 署名キー。最低 32 文字以上を推奨（256 ビット以上の鍵強度）。</summary>
    [Required(ErrorMessage = "JWT SecretKey は必須です")]
    [MinLength(32, ErrorMessage = "JWT SecretKey は 32 文字以上である必要があります")]
    public string SecretKey { get; init; } = string.Empty;
}
```

---

### P0-5: SendGrid Named HttpClient の DI 登録

**対象ファイル**: `Services/MailSendService/Program.cs`

**現状**: 
- `SendGridDsrService` が `httpClientFactory.CreateClient("SendGrid")` を呼び出すが、Named HttpClient が未登録

**修正方法**:

```csharp
// Program.cs の HttpClient 登録セクション（L100 付近）に追加:

// --- DI: SendGrid API HttpClient + Resilience ---
// GDPR DSR 機能で SendGrid API を呼び出すための HttpClient。
// 24 時間以内の DSR 完了要件を満たすためリトライポリシーを適用。
builder.Services.Configure<SendGridSettings>(builder.Configuration.GetSection("SendGrid"));
builder.Services.AddHttpClient("SendGrid", (sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<SendGridSettings>>().Value;
    client.BaseAddress = new Uri("https://api.sendgrid.com");
    client.DefaultRequestHeaders.Authorization = 
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.ApiKey);
})
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.Delay = TimeSpan.FromMilliseconds(500);
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
});
```

**追加作業**: `Configurations/SendGridSettings.cs` に `[Required]` 属性を追加:

```csharp
using System.ComponentModel.DataAnnotations;

namespace MailSendService.Configurations;

/// <summary>
/// SendGrid API 設定。GDPR DSR 連携に使用。
/// </summary>
public record SendGridSettings
{
    /// <summary>SendGrid API キー。</summary>
    [Required(ErrorMessage = "SendGrid ApiKey は必須です")]
    public string ApiKey { get; init; } = string.Empty;
}
```

---

### P0-6: Anemic Domain Model のリッチ化 — MailLog

**対象ファイル**: `Services/MailSendService/Models/MailLog.cs`

**現状**: 
- 状態遷移ロジックが外部（Service 層）に存在
- Aggregate の不変条件が保護されていない

**修正方法**:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MailSendService.Models;

/// <summary>
/// メール送信ログを管理する EF Core エンティティ（テーブル: mail_logs）。
/// </summary>
[Table("mail_logs")]
public class MailLog
{
    // ... 既存のプロパティ（省略）...

    /// <summary>
    /// メール送信を開始状態に遷移する。
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// ステータスが PENDING または FAILED 以外の場合。
    /// </exception>
    public void MarkAsSending()
    {
        if (Status is not (MailLogStatus.Pending or MailLogStatus.Failed))
            throw new InvalidOperationException(
                $"Cannot transition to SENDING from {Status}. Allowed: PENDING, FAILED");
        Status = MailLogStatus.Sending;
    }

    /// <summary>
    /// メール送信完了状態に遷移する。
    /// </summary>
    /// <param name="azureOperationId">Azure Communication Services の操作 ID。</param>
    /// <param name="sentAt">送信完了日時。</param>
    /// <exception cref="InvalidOperationException">
    /// ステータスが SENDING 以外の場合。
    /// </exception>
    public void MarkAsSent(string azureOperationId, DateTimeOffset sentAt)
    {
        if (Status is not MailLogStatus.Sending)
            throw new InvalidOperationException(
                $"Cannot transition to SENT from {Status}. Allowed: SENDING");
        Status = MailLogStatus.Sent;
        AzureOperationId = azureOperationId;
        SentAt = sentAt;
        ErrorMessage = null;
    }

    /// <summary>
    /// メール送信失敗状態に遷移する。
    /// </summary>
    /// <param name="errorMessage">エラーメッセージ。</param>
    /// <exception cref="InvalidOperationException">
    /// ステータスが SENDING 以外の場合。
    /// </exception>
    public void MarkAsFailed(string errorMessage)
    {
        if (Status is not MailLogStatus.Sending)
            throw new InvalidOperationException(
                $"Cannot transition to FAILED from {Status}. Allowed: SENDING");
        Status = MailLogStatus.Failed;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// リトライを試行する。
    /// </summary>
    /// <param name="maxRetryCount">最大リトライ回数。</param>
    /// <exception cref="InvalidOperationException">
    /// ステータスが FAILED 以外、またはリトライ上限に達している場合。
    /// </exception>
    public void IncrementRetryCount(int maxRetryCount)
    {
        if (Status is not MailLogStatus.Failed)
            throw new InvalidOperationException(
                $"Cannot retry from {Status}. Allowed: FAILED");
        if (RetryCount >= maxRetryCount)
            throw new InvalidOperationException(
                $"Retry limit exceeded: {RetryCount} >= {maxRetryCount}");
        RetryCount++;
        Status = MailLogStatus.Sending;
        ErrorMessage = null;
    }

    /// <summary>
    /// PII 匿名化処理を適用する。
    /// </summary>
    /// <param name="anonymizedEmail">匿名化されたメールアドレス。</param>
    public void Anonymize(string anonymizedEmail)
    {
        RecipientEmail = anonymizedEmail;
        RecipientName = null;
        RecipientUserId = null;
        if (Status == MailLogStatus.Pending)
        {
            Status = MailLogStatus.Skipped;
            ErrorMessage = "Anonymized due to GDPR erasure request";
        }
    }
}
```

**Service 層の修正（MailService.cs）**:
- 直接 `mailLog.Status = ...` を設定している箇所を、ドメインメソッド呼び出しに変更

---

### P0-7: Anemic Domain Model のリッチ化 — OutboxEvent

**対象ファイル**: `Services/MailSendService/Models/OutboxEvent.cs`

**修正方法**:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MailSendService.Models;

/// <summary>
/// Outbox パターンで使用するイベントを管理する EF Core エンティティ（テーブル: outbox_events）。
/// </summary>
[Table("outbox_events")]
public class OutboxEvent
{
    // ... 既存のプロパティ（省略）...

    /// <summary>
    /// イベントを発行済み状態に遷移する。
    /// </summary>
    /// <param name="publishedAt">発行完了日時。</param>
    /// <exception cref="InvalidOperationException">
    /// ステータスが PENDING 以外の場合。
    /// </exception>
    public void MarkAsPublished(DateTimeOffset publishedAt)
    {
        if (Status is not OutboxEventStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot transition to PUBLISHED from {Status}. Allowed: PENDING");
        Status = OutboxEventStatus.Published;
        PublishedAt = publishedAt;
    }

    /// <summary>
    /// イベント発行失敗状態に遷移する。
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// ステータスが PENDING 以外の場合。
    /// </exception>
    public void MarkAsFailed()
    {
        if (Status is not OutboxEventStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot transition to FAILED from {Status}. Allowed: PENDING");
        Status = OutboxEventStatus.Failed;
    }
}
```

---

### P0-8: Anemic Domain Model のリッチ化 — MailTemplate

**対象ファイル**: `Services/MailSendService/Models/MailTemplate.cs`

**修正方法**:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MailSendService.Models;

/// <summary>
/// メールテンプレートを管理する EF Core エンティティ（テーブル: mail_templates）。
/// </summary>
[Table("mail_templates")]
public class MailTemplate
{
    // ... 既存のプロパティ（省略）...

    /// <summary>
    /// テンプレートを無効化する。
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// テンプレートを有効化する。
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// テンプレートのコンテンツを更新する。
    /// </summary>
    /// <param name="subject">新しい件名。</param>
    /// <param name="htmlBody">新しい HTML 本文。</param>
    /// <param name="textBody">新しいテキスト本文。</param>
    /// <param name="templateType">新しいテンプレート種別。</param>
    public void UpdateContent(string subject, string? htmlBody, string? textBody, string templateType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject, nameof(subject));
        
        Subject = subject;
        HtmlBody = htmlBody;
        TextBody = textBody;
        TemplateType = templateType;
    }
}
```

---

### P0-9: Kafka SASL 認証設定（エスカレーション案件）

**対象ファイル**: `Services/MailSendService/Configurations/KafkaSettings.cs`, `Program.cs`

**現状**: 
- Kafka 認証なし — 攻撃者がフィッシングメール送信イベント注入可能

**分析**: インフラ連携が必要なため、3 つの解決案を検討

#### 案 1: SASL/SCRAM 認証（推奨）

```csharp
// KafkaSettings.cs に認証設定を追加
public record KafkaSettings
{
    public string BootstrapServers { get; init; } = string.Empty;
    public string GroupId { get; init; } = string.Empty;
    public string Topic { get; init; } = string.Empty;
    
    // SASL 認証設定
    public string? SaslMechanism { get; init; }  // "SCRAM-SHA-512"
    public string? SaslUsername { get; init; }
    public string? SaslPassword { get; init; }
    public string? SecurityProtocol { get; init; }  // "SASL_SSL"
}

// Program.cs の Consumer 設定
var config = new ConsumerConfig
{
    BootstrapServers = kafkaSettings.BootstrapServers,
    GroupId = kafkaSettings.GroupId,
    AutoOffsetReset = AutoOffsetReset.Earliest,
    EnableAutoCommit = false,
    // SASL 認証
    SecurityProtocol = Enum.Parse<SecurityProtocol>(kafkaSettings.SecurityProtocol ?? "Plaintext"),
    SaslMechanism = Enum.Parse<SaslMechanism>(kafkaSettings.SaslMechanism ?? "Plain"),
    SaslUsername = kafkaSettings.SaslUsername,
    SaslPassword = kafkaSettings.SaslPassword
};
```

#### 案 2: mTLS 認証

- クライアント証明書ベースの認証
- 証明書管理の運用負荷が高い
- インフラチームとの調整が必要

#### 案 3: Network Policy による分離

- Kubernetes Network Policy で Kafka への接続元を制限
- アプリケーションコード変更不要
- インフラチームとの調整が必要

**採用案**: 案 1（SASL/SCRAM）を推奨  
**理由**: 
- 設定変更のみで対応可能
- Confluent.Kafka ライブラリが標準サポート
- 運用負荷が低い

**アクション**: インフラリードにエスカレーションし、Kafka 認証情報の提供を依頼

---

### P0-10: サービス間認証の追加（UserInfoResolver）

**対象ファイル**: `Services/MailSendService/Services/UserInfoResolver.cs`

**現状**: 
- UserManagementService 呼び出しに認証なし

**分析**: 3 つの解決案を検討

#### 案 1: API キー認証（採用）

```csharp
// Program.cs に API キー設定を追加
builder.Services.Configure<UserManagementSettings>(
    builder.Configuration.GetSection("Services:UserManagement"));

builder.Services.AddHttpClient<IUserInfoResolver, UserInfoResolver>((sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<UserManagementSettings>>().Value;
    client.BaseAddress = new Uri(settings.Url);
    // API キー認証ヘッダーを追加
    client.DefaultRequestHeaders.Add("X-Api-Key", settings.ApiKey);
})
// ... 既存の Resilience 設定

// UserManagementSettings.cs を更新
public record UserManagementSettings
{
    [Required]
    public string Url { get; init; } = string.Empty;
    
    [Required]
    public string ApiKey { get; init; } = string.Empty;
}
```

#### 案 2: クライアント証明書（mTLS）

- 最も安全だが運用負荷が高い
- Kubernetes 環境では SPIFFE/SPIRE との統合を検討

#### 案 3: JWT サービスアカウント

- AuthService からサービスアカウント用 JWT を取得
- 既存の JWT インフラを再利用可能

**採用案**: 案 1（API キー認証）  
**理由**:
- 実装がシンプル
- 即座に対応可能
- 既存の HttpClient 設定に組み込みやすい

---

## P1: リリース前対応 — High 指摘修正

### P1-1: SendGridDsrService の DI 登録

**対象ファイル**: `Services/MailSendService/Program.cs`

**修正方法**:

```csharp
// DI 登録セクションに追加（L74 付近）:
builder.Services.AddScoped<ISendGridDsrService, SendGridDsrService>();
```

---

### P1-2: OutboxPurgeService の DI 登録

**対象ファイル**: `Services/MailSendService/Program.cs`

**現状**: 
- コードは存在するが `AddHostedService` で登録されていない

**確認結果**: L259 で既に登録済み
```csharp
builder.Services.AddHostedService<OutboxPurgeService>();
```

→ **対応不要**（レポートの誤検出の可能性）

---

### P1-3: PermissionsPolicy 構文エラーの修正

**対象ファイル**: `Services/MailSendService/Infrastructure/Middleware/SecurityHeadersMiddleware.cs`

**現状（L23）**:
```csharp
private const string PermissionsPolicy = "camera=(), microphone=(), geolocation()";
```

**問題点**: 
- `geolocation()` は不正な構文（`=` が必要）

**修正方法**:

```csharp
private const string PermissionsPolicy = "camera=(), microphone=(), geolocation=()";
```

---

### P1-4: 設定クラスに ValidateOnStart() 追加

**対象ファイル**: `Services/MailSendService/Program.cs`

**現状**: 
- `KafkaSettings`, `MailSettings`, `AzureEmailSettings` に `ValidateOnStart()` なし

**修正方法**:

```csharp
// L103-106 を以下に変更:
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
```

**追加作業**: 各設定クラスに `[Required]` 属性を追加

```csharp
// KafkaSettings.cs
using System.ComponentModel.DataAnnotations;

public record KafkaSettings
{
    [Required(ErrorMessage = "Kafka BootstrapServers は必須です")]
    public string BootstrapServers { get; init; } = string.Empty;
    
    [Required(ErrorMessage = "Kafka GroupId は必須です")]
    public string GroupId { get; init; } = string.Empty;
    
    [Required(ErrorMessage = "Kafka Topic は必須です")]
    public string Topic { get; init; } = string.Empty;
}

// AzureEmailSettings.cs
using System.ComponentModel.DataAnnotations;

public record AzureEmailSettings
{
    [Required(ErrorMessage = "Azure Communication Endpoint は必須です")]
    public string Endpoint { get; init; } = string.Empty;
    
    [Required(ErrorMessage = "Azure Communication SenderAddress は必須です")]
    [EmailAddress(ErrorMessage = "有効なメールアドレスを入力してください")]
    public string SenderAddress { get; init; } = string.Empty;
}
```

---

### P1-5: GetStatsAsync の並行クエリ問題修正

**対象ファイル**: `Services/MailSendService/Services/MailService.cs`

**現状（L350-366）**:
```csharp
var sentTask = mailLogRepository.CountByStatusAsync(MailLogStatus.Sent, ct);
var failedTask = mailLogRepository.CountByStatusAsync(MailLogStatus.Failed, ct);
var pendingTask = mailLogRepository.CountByStatusAsync(MailLogStatus.Pending, ct);
var sentByTemplateTask = mailLogRepository.CountByTemplateAsync(ct);

await Task.WhenAll(sentTask, failedTask, pendingTask, sentByTemplateTask);
```

**問題点**: 
- 同一 DbContext で 4 クエリを並行実行 — EF Core はスレッドセーフでない

**修正方法**:

```csharp
public async Task<MailStatsResponse> GetStatsAsync(CancellationToken ct = default)
{
    // EF Core DbContext はスレッドセーフでないため逐次実行
    var sent = await mailLogRepository.CountByStatusAsync(MailLogStatus.Sent, ct);
    var failed = await mailLogRepository.CountByStatusAsync(MailLogStatus.Failed, ct);
    var pending = await mailLogRepository.CountByStatusAsync(MailLogStatus.Pending, ct);
    var sentByTemplate = await mailLogRepository.CountByTemplateAsync(ct);

    var total = sent + failed + pending;
    var successRate = total > 0 ? (double)sent / total * 100 : 0;
    return new MailStatsResponse(sent, failed, pending, successRate, sentByTemplate);
}
```

---

### P1-6: FindFailedForRetryAsync に AsNoTracking 追加

**対象ファイル**: `Services/MailSendService/Repositories/MailLogRepository.cs`

**現状（L113-118）**:
```csharp
public async Task<List<MailLog>> FindFailedForRetryAsync(int maxRetryCount, int limit = 20, CancellationToken ct = default)
    => await context.MailLogs
        .Where(m => m.Status == MailLogStatus.Failed && m.RetryCount < maxRetryCount)
        .OrderBy(m => m.UpdatedAt)
        .Take(limit)
        .ToListAsync(ct);
```

**問題点**: 
- 読み取り専用クエリだが変更追跡が有効

**分析**: 
- ただし、リトライ処理では取得した MailLog を更新する必要がある
- `FindByIdAsync` で再取得しているため、このメソッド自体は読み取り専用として扱える

**修正方法**:

```csharp
public async Task<List<MailLog>> FindFailedForRetryAsync(int maxRetryCount, int limit = 20, CancellationToken ct = default)
    => await context.MailLogs
        .AsNoTracking()
        .Where(m => m.Status == MailLogStatus.Failed && m.RetryCount < maxRetryCount)
        .OrderBy(m => m.UpdatedAt)
        .Take(limit)
        .ToListAsync(ct);
```

---

### P1-7: FindByRecipientUserIdAsync にページネーション追加

**対象ファイル**: `Services/MailSendService/Repositories/MailLogRepository.cs`

**現状（L172-175）**:
```csharp
public async Task<List<MailLog>> FindByRecipientUserIdAsync(string userId, CancellationToken ct = default)
    => await context.MailLogs
        .Where(m => m.RecipientUserId == userId)
        .ToListAsync(ct);
```

**問題点**: 
- ページネーション/上限なし — メモリ圧迫リスク

**修正方法**:

```csharp
public async Task<List<MailLog>> FindByRecipientUserIdAsync(
    string userId, 
    int limit = 1000,  // GDPR 匿名化用途のため十分な上限
    CancellationToken ct = default)
    => await context.MailLogs
        .Where(m => m.RecipientUserId == userId)
        .OrderByDescending(m => m.CreatedAt)
        .Take(limit)
        .ToListAsync(ct);
```

**影響範囲**: 
- `IMailLogRepository` インターフェースも更新
- 呼び出し元（`MailService.ProcessUserDeletedAsync`）は影響なし（1000 件で十分）

---

### P1-8: FindPendingByUserIdAsync に上限追加

**対象ファイル**: `Services/MailSendService/Repositories/MailLogRepository.cs`

**現状（L196-199）**:
```csharp
public async Task<List<MailLog>> FindPendingByUserIdAsync(string userId, CancellationToken ct = default)
    => await context.MailLogs
        .Where(m => m.RecipientUserId == userId && m.Status == MailLogStatus.Pending)
        .ToListAsync(ct);
```

**修正方法**:

```csharp
public async Task<List<MailLog>> FindPendingByUserIdAsync(
    string userId, 
    int limit = 100,
    CancellationToken ct = default)
    => await context.MailLogs
        .Where(m => m.RecipientUserId == userId && m.Status == MailLogStatus.Pending)
        .OrderBy(m => m.CreatedAt)
        .Take(limit)
        .ToListAsync(ct);
```

---

### P1-9: エンティティの TimeProvider 対応

**対象ファイル**: 
- `Services/MailSendService/Models/MailLog.cs`
- `Services/MailSendService/Models/OutboxEvent.cs`
- `Services/MailSendService/Models/MailTemplate.cs`
- `Services/MailSendService/Models/MailSuppression.cs`
- `Services/MailSendService/Models/MailAttachment.cs`

**現状**: 
- エンティティで `DateTimeOffset.UtcNow` 直接使用

**修正方法**:

DbContext の `SaveChangesAsync` オーバーライドで `TimeProvider` を使用し、自動的に `CreatedAt` / `UpdatedAt` を設定:

```csharp
// Infrastructure/Persistence/AppDbContext.cs に追加
public class AppDbContext : DbContext
{
    private readonly TimeProvider _timeProvider;

    public AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider timeProvider) 
        : base(options)
    {
        _timeProvider = timeProvider;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow();
        
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity is IHasTimestamps entity)
                {
                    entity.CreatedAt = now;
                    entity.UpdatedAt = now;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                if (entry.Entity is IHasTimestamps entity)
                {
                    entity.UpdatedAt = now;
                }
            }
        }

        return await base.SaveChangesAsync(ct);
    }
}

// 共通インターフェース
public interface IHasTimestamps
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
}
```

**エンティティの修正**:

```csharp
// MailLog.cs
public class MailLog : IHasTimestamps
{
    // CreatedAt / UpdatedAt のデフォルト値を削除（DbContext が設定）
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}
```

---

### P1-10: HtmlBody の XSS サニタイズ

**対象ファイル**: `Services/MailSendService/Services/TemplateService.cs`

**問題点**: 
- 管理者入力の `HtmlBody` がサニタイズされずに保存

**修正方法**:

```csharp
// HtmlSanitizer NuGet パッケージを追加
// <PackageReference Include="HtmlSanitizer" Version="8.*" />

// TemplateService.cs にサニタイズ処理を追加
using Ganss.Xss;

public class TemplateService : ITemplateService
{
    private readonly HtmlSanitizer _sanitizer = new();

    public async Task<MailTemplateResponse> CreateAsync(TemplateCreateRequest request, CancellationToken ct)
    {
        // HtmlBody をサニタイズ
        var sanitizedHtmlBody = request.HtmlBody is not null 
            ? _sanitizer.Sanitize(request.HtmlBody) 
            : null;
        
        // ... 以降の処理
    }
}
```

---

### P1-11: MailEventConsumer の Dead Letter Topic 実装

**対象ファイル**: `Services/MailSendService/Consumers/MailEventConsumer.cs`

**問題点**: 
- Poison メッセージが無限リトライ

**修正方法**:

```csharp
// Poison メッセージをトラッキングする辞書を追加
private readonly Dictionary<string, int> _failedMessageCounts = [];
private const int MaxRetryPerMessage = 3;
private const string DeadLetterTopic = "mail-events-dlq";

// catch ブロックを修正
catch (Exception ex)
{
    var messageKey = result?.Message?.Key ?? "unknown";
    _failedMessageCounts.TryGetValue(messageKey, out var count);
    _failedMessageCounts[messageKey] = ++count;

    if (count >= MaxRetryPerMessage)
    {
        logger.LogError(ex, "Message exceeded retry limit. Moving to DLT: {MessageKey}", messageKey);
        
        // Dead Letter Topic に送信
        using var scope = scopeFactory.CreateScope();
        var producer = scope.ServiceProvider.GetRequiredService<IProducer<string, string>>();
        await producer.ProduceAsync(DeadLetterTopic, result.Message, stoppingToken);
        
        _failedMessageCounts.Remove(messageKey);
        consumer.Commit(result);
    }
    else
    {
        logger.LogWarning(ex, "Event processing error (attempt {Attempt}/{Max})", 
            count, MaxRetryPerMessage);
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
    }
}
```

---

### P1-12: MailLogRepository.FindByIdAsync に AsNoTracking オプション追加

**対象ファイル**: `Services/MailSendService/Repositories/MailLogRepository.cs`

**現状**: 
- `FindByIdAsync` は常に変更追跡有効

**分析**: 
- 読み取りのみのケース（`GetLogByIdAsync`）と更新が必要なケース（`RetryAsync`）が混在
- 使い分けが必要

**修正方法**:

```csharp
public async Task<MailLog?> FindByIdAsync(string id, bool trackChanges = true, CancellationToken ct = default)
{
    var query = context.MailLogs.Include(m => m.Attachments);
    if (!trackChanges)
        query = query.AsNoTracking();
    return await query.FirstOrDefaultAsync(m => m.Id == id, ct);
}
```

**インターフェース更新**:
```csharp
Task<MailLog?> FindByIdAsync(string id, bool trackChanges = true, CancellationToken ct = default);
```

---

## P2: 次回リファクタリング — Medium 指摘修正

### P2-1: Value Object の導入

**対象**: EmailAddress, MailLogStatus 等を Value Object として定義

```csharp
// Value Object: EmailAddress
public readonly record struct EmailAddress
{
    public string Value { get; }
    
    public EmailAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('@') || !value.Contains('.'))
            throw new InvalidEmailAddressException(value);
        Value = value.ToLowerInvariant();
    }
    
    public string Masked => Value.Length > 0 
        ? $"{Value[0]}***{Value[Value.IndexOf('@')..]}" 
        : "***";
    
    public static implicit operator string(EmailAddress email) => email.Value;
}
```

### P2-2: MailService の責務分割

**現状**: 11 の依存関係 — SRP 違反の疑い

**推奨分割**:
- `MailSendingService`: メール送信コアロジック
- `GdprComplianceService`: GDPR 関連操作
- `MailStatsService`: 統計情報取得

### P2-3: Redis キャッシュによる統計クエリ最適化

**対象**: `GetStatsAsync` の 4 クエリにキャッシュなし

```csharp
public async Task<MailStatsResponse> GetStatsAsync(CancellationToken ct = default)
{
    const string cacheKey = "mail:stats";
    var cached = await cache.GetStringAsync(cacheKey, ct);
    if (cached is not null)
        return JsonSerializer.Deserialize<MailStatsResponse>(cached)!;

    // 実際のクエリ実行...
    
    await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result),
        new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) }, ct);
    
    return result;
}
```

---

## エスカレーション事項

| # | 優先度 | 内容 | 推奨判断者 | 修正計画での対応 |
|---|--------|------|-----------|----------------|
| 1 | 最優先 | Kafka SASL/mTLS 認証の導入 | インフラリード | P0-9 で案 1（SASL/SCRAM）を推奨 |
| 2 | 最優先 | サービス間認証方式の選定 | アーキテクト | P0-10 で案 1（API キー）を採用 |
| 3 | 高優先 | 既存ログ集約システムの PII 遡及削除 | セキュリティチーム | 別途対応必要 |
| 4 | 高優先 | `SendGridDsrService` の仕様確認 | プロダクトオーナー | P1-1 で DI 登録を実施 |
| 5 | 通常 | `SENDING` ステータス孤児化の復旧ポリシー | ビジネスアナリスト | 次回リファクタリングで対応 |

---

## P1 追加事項（check-report との照合により追記）

### P1-13: リトライバックオフの UpdatedAt 使用修正

**対象ファイル**: `Services/MailSendService/Consumers/OutboxPublisher.cs`

**現状（L66-67）**:
```csharp
var retryableEvents = failedEvents
    .Where(e => e.CreatedAt.Add(FailedRetryCooldown) <= now)
```

**問題点**: 
- `CreatedAt` ではなく `UpdatedAt` を使用すべき（リトライ時刻の基準が誤っている）

**修正方法**:

```csharp
var retryableEvents = failedEvents
    .Where(e => e.UpdatedAt.Add(FailedRetryCooldown) <= now)
    .ToList();
```

**追加作業**: `OutboxEvent` エンティティに `UpdatedAt` プロパティがない場合は追加

```csharp
// OutboxEvent.cs に追加
[Column("updated_at")]
public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
```

---

### P1-14: SENDING ステータス孤児化の復旧機構

**対象ファイル**: `Services/MailSendService/Consumers/MailRetryService.cs` または新規作成

**問題点**: 
- プロセスクラッシュ時に SENDING ステータスのメールログが孤児化する

**修正方法**:

```csharp
// MailRetryService.cs に復旧ロジックを追加
// 一定時間（例: 10分）以上 SENDING のままのレコードを FAILED に戻す

public async Task RecoverOrphanedSendingAsync(CancellationToken ct)
{
    var cutoff = timeProvider.GetUtcNow().AddMinutes(-10);
    var orphanedLogs = await mailLogRepository.FindOrphanedSendingAsync(cutoff, ct);
    
    foreach (var log in orphanedLogs)
    {
        log.Status = MailLogStatus.Failed;
        log.ErrorMessage = "Orphaned SENDING status recovered by cleanup job";
        logger.LogWarning("Recovered orphaned SENDING mail: {MailLogId}", log.Id);
    }
    
    await mailLogRepository.SaveChangesAsync(ct);
}
```

**Repository に追加**:
```csharp
Task<List<MailLog>> FindOrphanedSendingAsync(DateTimeOffset cutoff, CancellationToken ct = default);
```

---

### P1-15: MailService の LogMailSent 呼び出し箇所の修正（追加確認）

**対象ファイル**: `Services/MailSendService/Services/MailService.cs`

**現状（L146）**:
```csharp
LogMailSent(mailLog.Id, recipientEmail);
```

**問題点**: 
- P0-1 で定義した `MaskEmailForLog` を適用する必要がある

**修正方法**:
```csharp
LogMailSent(mailLog.Id, MaskEmailForLog(recipientEmail));
```

---

### P1-16: BackgroundService への Correlation ID 追加

**対象ファイル**: 
- `Services/MailSendService/Consumers/MailRetryService.cs`
- `Services/MailSendService/Consumers/PiiCleanupService.cs`
- `Services/MailSendService/Consumers/OutboxPublisher.cs`

**問題点**: 
- 3 つの BackgroundService に Correlation ID がない

**修正方法**:

```csharp
// 各 BackgroundService の ExecuteAsync 内で Correlation ID を生成
using (Serilog.Context.LogContext.PushProperty("CorrelationId", Guid.NewGuid().ToString()))
{
    // 既存の処理...
}
```

---

### P1-17: DeserializeTemplateVariables の JsonException 処理

**対象ファイル**: `Services/MailSendService/Services/MailService.cs`

**問題点**: 
- `DeserializeTemplateVariables` で JsonException が発生した場合、サイレントに空辞書を返している

**修正方法**:

```csharp
private Dictionary<string, string> DeserializeTemplateVariables(string? json)
{
    if (string.IsNullOrEmpty(json))
        return [];

    try
    {
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
    }
    catch (JsonException ex)
    {
        // ★ サイレントではなく Warning ログを出力
        logger.LogWarning(ex, "Failed to deserialize template variables JSON: {Json}", 
            json.Length > 100 ? json[..100] + "..." : json);
        return [];
    }
}
```

---

### P1-18: API レスポンスのメールアドレスマスキング

**対象ファイル**: `Services/MailSendService/DTOs/Responses/MailLogResponse.cs`

**問題点**: 
- API レスポンスに `RecipientEmail` が平文で含まれる（管理者向けだが、念のためマスキング検討）

**修正方法（オプション）**:

```csharp
// ToResponse メソッドでマスキングを適用する場合
private static MailLogResponse ToResponse(MailLog log)
    => new(log.Id, log.EventType, 
        MaskEmailForLog(log.RecipientEmail),  // マスキング適用
        log.RecipientName,
        log.TemplateName, log.Subject, log.Status, log.RetryCount,
        log.ErrorMessage, log.SentAt, log.CreatedAt);
```

**注意**: 管理者が完全なメールアドレスを確認する必要がある場合は、別途 API を提供するか、この修正をスキップ

---

### P1-19: OutboxPublisher のバッチ送信最適化

**対象ファイル**: `Services/MailSendService/Consumers/OutboxPublisher.cs`

**問題点**: 
- Kafka メッセージを逐次送信している（パフォーマンス改善の余地あり）

**修正方法**:

```csharp
// 逐次送信から並列送信に変更（ただし、順序保証が必要な場合は逐次のまま）
var publishTasks = pendingEvents.Select(async evt =>
{
    try
    {
        await producer.ProduceAsync("mail-events",
            new Message<string, string> { Key = evt.Id, Value = evt.Payload },
            stoppingToken);
        evt.MarkAsPublished(timeProvider.GetUtcNow());
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Outbox publish failed: {EventId}", evt.Id);
        evt.MarkAsFailed();
    }
});

await Task.WhenAll(publishTasks);
```

---

### P1-20: HtmlBody サイズ制限の明確化

**対象ファイル**: `Services/MailSendService/Validators/TemplateCreateRequestValidator.cs`

**問題点**: 
- `HtmlBody` 500KB 制限が DoS 攻撃のリスク（既存だが、より厳しい制限を検討）

**修正方法**:

```csharp
public class TemplateCreateRequestValidator : AbstractValidator<TemplateCreateRequest>
{
    private const int MaxHtmlBodySize = 100 * 1024;  // 100KB に制限（500KB から縮小）
    
    public TemplateCreateRequestValidator()
    {
        // 既存のルール...
        
        RuleFor(x => x.HtmlBody)
            .Must(body => body is null || Encoding.UTF8.GetByteCount(body) <= MaxHtmlBodySize)
            .WithMessage($"HtmlBody は {MaxHtmlBodySize / 1024}KB 以下である必要があります");
    }
}
```

---

## P2 追加事項（check-report との照合により追記）

### P2-4: 設計書との乖離対応 — テンプレートエンジン

**問題点**: 
- 設計書記載の Razor 方式ではなく正規表現ベース

**対応方針**:
1. 設計書を更新して現状の正規表現方式を正とする（推奨）
2. または、Razor テンプレートエンジンに移行する（コスト高）

**推奨**: 設計書の更新（テックリード判断）

---

### P2-5: MailLog の xmin 楽観的ロック追加

**対象ファイル**: `Services/MailSendService/Models/MailLog.cs`

**問題点**: 
- 楽観的ロックが未設定

**修正方法**:

```csharp
// PostgreSQL xmin を使用した楽観的ロック
[Column("xmin")]
[ConcurrencyCheck]
public uint RowVersion { get; set; }
```

---

### P2-6: DesignTimeDbContextFactory のフォールバック接続文字列削除

**対象ファイル**: `Services/MailSendService/Infrastructure/Persistence/DesignTimeDbContextFactory.cs`

**問題点**: 
- フォールバック接続文字列に DB ユーザー名が含まれている

**修正方法**:

```csharp
// フォールバック接続文字列を削除し、環境変数必須とする
public AppDbContext CreateDbContext(string[] args)
{
    var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? throw new InvalidOperationException(
            "ConnectionStrings__DefaultConnection 環境変数が設定されていません。" +
            "dotnet ef コマンド実行前に設定してください。");
    
    var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
    optionsBuilder.UseNpgsql(connectionString);
    return new AppDbContext(optionsBuilder.Options, TimeProvider.System);
}
```

---

## 修正後の検証手順

### 1. ビルド確認

```bash
cd Services/MailSendService
dotnet build
```

### 2. 単体テスト実行

```bash
dotnet test --filter Category=Unit
```

### 3. PII ログ出力確認

```bash
# ログにメールアドレスが平文で出力されていないことを確認
grep -r "Recipient=" logs/ | grep -v "***"
```

### 4. OpenAPI エンドポイント確認

```bash
curl http://localhost:5008/openapi/v1.json
```

### 5. 設定バリデーション確認

```bash
# 必須設定を空にして起動 → 起動時エラーを確認
dotnet run
```

### 6. ConcurrencyException の HTTP 409 確認

```bash
# 楽観的ロック競合を意図的に発生させ、409 が返ることを確認
```

---

## 修正ファイル一覧（優先度順）

### P0（即座対応）— 10 件

| # | ファイル | 修正内容 | 関連チケット |
|---|---------|---------|-------------|
| 1 | `Services/MailService.cs` | PII ログマスキング | P0-1 |
| 2 | `Program.cs` | OpenAPI, ConcurrencyException, HttpClient 登録, 設定バリデーション | P0-2, P0-3, P0-6 |
| 3 | `Configurations/JwtSettings.cs` | Data Annotations 追加 | P0-4 |
| 4 | `Configurations/KafkaSettings.cs` | Data Annotations + SASL 設定追加 | P0-9 |
| 5 | `Configurations/SendGridSettings.cs` | Data Annotations 追加 | P0-5 |
| 6 | `Configurations/AzureEmailSettings.cs` | Data Annotations 追加 | P1-4 |
| 7 | `Configurations/UserManagementSettings.cs` | ApiKey 追加 | P0-10 |
| 8 | `Models/MailLog.cs` | ドメインメソッド追加 | P0-7 |
| 9 | `Models/OutboxEvent.cs` | ドメインメソッド追加, UpdatedAt 追加 | P0-8, P1-13 |
| 10 | `Models/MailTemplate.cs` | ドメインメソッド追加 | P0-8 |

### P1（リリース前対応）— 20 件

| # | ファイル | 修正内容 | 関連チケット |
|---|---------|---------|-------------|
| 1 | `Program.cs` | SendGridDsrService DI 登録, ValidateOnStart 追加 | P1-1, P1-4 |
| 2 | `Infrastructure/Middleware/SecurityHeadersMiddleware.cs` | PermissionsPolicy 修正 | P1-3 |
| 3 | `Repositories/MailLogRepository.cs` | AsNoTracking, ページネーション追加, FindOrphanedSendingAsync 追加 | P1-5, P1-6, P1-7, P1-8, P1-12, P1-14 |
| 4 | `Repositories/Interfaces/IMailLogRepository.cs` | インターフェース更新 | P1-7, P1-8, P1-12, P1-14 |
| 5 | `Consumers/MailEventConsumer.cs` | Dead Letter Topic 実装 | P1-11 |
| 6 | `Consumers/OutboxPublisher.cs` | UpdatedAt 使用修正, Correlation ID 追加 | P1-13, P1-16 |
| 7 | `Consumers/MailRetryService.cs` | Correlation ID 追加, 孤児化復旧ロジック追加 | P1-14, P1-16 |
| 8 | `Consumers/PiiCleanupService.cs` | Correlation ID 追加 | P1-16 |
| 9 | `Infrastructure/Persistence/AppDbContext.cs` | TimeProvider 対応, IHasTimestamps 実装 | P1-9 |
| 10 | `Services/TemplateService.cs` | HtmlBody サニタイズ | P1-10 |
| 11 | `Services/MailService.cs` | LogMailSent 呼び出し修正, DeserializeTemplateVariables 修正 | P1-15, P1-17 |
| 12 | `Validators/TemplateCreateRequestValidator.cs` | HtmlBody サイズ制限強化 | P1-20 |
| 13 | `DTOs/Responses/MailLogResponse.cs` | メールアドレスマスキング（オプション） | P1-18 |
| 14 | `Models/MailLog.cs` | IHasTimestamps 実装, xmin 楽観的ロック | P1-9, P2-5 |
| 15 | `Models/OutboxEvent.cs` | IHasTimestamps 実装 | P1-9 |
| 16 | `Models/MailTemplate.cs` | IHasTimestamps 実装 | P1-9 |
| 17 | `Models/MailSuppression.cs` | IHasTimestamps 実装 | P1-9 |
| 18 | `Models/MailAttachment.cs` | IHasTimestamps 実装 | P1-9 |
| 19 | `Models/IHasTimestamps.cs` | 新規作成 | P1-9 |
| 20 | `MailSendService.csproj` | HtmlSanitizer パッケージ追加 | P1-10 |

### P2（次回リファクタリング）— 6 件

| # | ファイル | 修正内容 | 関連チケット |
|---|---------|---------|-------------|
| 1 | `Models/ValueObjects/EmailAddress.cs` | Value Object 新規作成 | P2-1 |
| 2 | `Services/MailService.cs` | 責務分割（SRP 対応） | P2-2 |
| 3 | `Services/MailStatsService.cs` | 新規作成（MailService から分割） | P2-2, P2-3 |
| 4 | `Services/GdprComplianceService.cs` | 新規作成（MailService から分割） | P2-2 |
| 5 | `design-docs/mailsend-service-design.md` | 設計書更新（テンプレートエンジン仕様） | P2-4 |
| 6 | `Infrastructure/Persistence/DesignTimeDbContextFactory.cs` | フォールバック接続文字列削除 | P2-6 |

---

## サマリー

| 優先度 | 件数 | 対応目安 |
|--------|------|---------|
| **P0（Critical）** | 10 件 | 即座対応 |
| **P1（High）** | 20 件 | リリース前 |
| **P2（Medium）** | 6 件 | 次回リファクタリング |
| **エスカレーション** | 7 件 | 人間判断待ち |
| **合計** | 43 件 | — |

---

## 注意事項

1. **P0 の修正は全て完了するまでリリースしない**
2. エスカレーション事項は各判断者の判断を待ってから着手
3. P1-19（OutboxPublisher バッチ送信）はメッセージ順序保証要件を確認後に判断
4. P1-18（API レスポンスマスキング）は管理者要件を確認後に判断

---

*本計画書は `check-report-1.md` の指摘に基づき作成されました。*
*最終更新: 2025-06-06*
