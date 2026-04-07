# UserManagementService 修正計画

> **基準レポート**: `check-report-1.md` (2025-07-25 イテレーション 1)
> **対象**: `Services/UserManagementService/` 全ソースコード
> **方針**: Critical → High → Medium の優先度順に、各指摘に対する具体的な修正内容を記載。各項目にはファイルパス・現状コード・修正後コードを含む。

---

## 目次

- [Critical 修正（4 件）](#critical-修正4-件)
- [High 修正 — セキュリティ（6 件）](#high-修正--セキュリティ6-件)
- [High 修正 — アーキテクチャ・DDD（5 件）](#high-修正--アーキテクチャddd5-件)
- [High 修正 — OpenAPI・設定・DI（4 件）](#high-修正--openapi設定di4-件)
- [High 修正 — データアクセス・パフォーマンス（6 件）](#high-修正--データアクセスパフォーマンス6-件)
- [High 修正 — 非同期・並行処理（1 件）](#high-修正--非同期並行処理1-件)
- [High 修正 — エラー処理・ログ（2 件）](#high-修正--エラー処理ログ2-件)
- [High 修正 — 耐障害性（3 件）](#high-修正--耐障害性3-件)
- [High 修正 — C# 規約（1 件）](#high-修正--c-規約1-件)
- [High 修正 — NuGet 依存関係（2 件）](#high-修正--nuget-依存関係2-件)
- [High 修正 — 機能完全性（2 件）](#high-修正--機能完全性2-件)
- [High 修正 — API 設計（2 件）](#high-修正--api-設計2-件)
- [High 修正 — テスト品質（8 件）](#high-修正--テスト品質8-件)
- [Medium 修正一覧](#medium-修正一覧)

---

## Critical 修正（4 件）

### C-1: AES-CBC → AES-GCM（認証付き暗号）への変更

**レポート指摘**: security — AES-CBC without HMAC。パディングオラクル攻撃で PII 復号のリスク。
**対象ファイル**: `Services/UserManagementService/BackgroundServices/DataExportService.cs`

**現状コード** (L78-90):
```csharp
private byte[] EncryptData(byte[] data)
{
    using var aes = Aes.Create();
    aes.KeySize = 256;
    aes.Key = Convert.FromBase64String(exportOptions.Value.EncryptionKeyBase64);
    aes.GenerateIV();

    using var encryptor = aes.CreateEncryptor();
    var encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);

    var result = new byte[aes.IV.Length + encrypted.Length];
    Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
    Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);
    return result;
}
```

**修正後コード**:
```csharp
private byte[] EncryptData(byte[] data)
{
    var key = Convert.FromBase64String(exportOptions.Value.EncryptionKeyBase64);
    var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
    RandomNumberGenerator.Fill(nonce);
    var tag = new byte[AesGcm.TagByteSizes.MaxSize];     // 16 bytes
    var ciphertext = new byte[data.Length];

    using var aesGcm = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
    aesGcm.Encrypt(nonce, data, ciphertext, tag);

    // Nonce(12) + Tag(16) + CipherText を連結
    var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
    Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
    Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
    Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);
    return result;
}
```

**修正のポイント**:
- `Aes.Create()` + CBC → `AesGcm` に置換。AES-GCM は AEAD（認証付き暗号）であり、暗号文の改ざん検知が可能
- Nonce（12 bytes）は `RandomNumberGenerator.Fill` で生成（暗号学的に安全な乱数）
- 出力形式: `[Nonce 12][Tag 16][Ciphertext N]`
- `using System.Security.Cryptography;` は既存 import で利用可能

---

### C-2: WishlistItem の Aggregate 境界違反 — RestockNotification を Aggregate Root 経由に修正

**レポート指摘**: ddd-domain — `WishlistRepository.FindItemsByProductIdWithRestockNotifyAsync` が `WishlistItem` を直接クエリし、Wishlist Aggregate Root をバイパス。
**対象ファイル**:
- `Services/UserManagementService/Repositories/WishlistRepository.cs`
- `Services/UserManagementService/Repositories/Interfaces/IWishlistRepository.cs`

**現状コード** (WishlistRepository.cs L38-43):
```csharp
public async Task<List<WishlistItem>> FindItemsByProductIdWithRestockNotifyAsync(
    string productId, CancellationToken ct = default)
    => await context.WishlistItems
        .AsNoTracking()
        .Where(i => i.ProductId == productId && i.NotifyOnRestock && i.NotifiedAt == null)
        .ToListAsync(ct);
```

**修正方針**: Aggregate Root（`Wishlist`）経由のクエリに変更。対象アイテムを含む Wishlist を取得し、Service 層で処理する。

**修正後コード** (WishlistRepository.cs):
```csharp
// 旧メソッドを削除し、Aggregate Root 経由のメソッドに置換
public async Task<List<Wishlist>> FindByProductIdWithRestockNotifyAsync(
    string productId, int batchSize, CancellationToken ct = default)
    => await context.Wishlists
        .AsNoTracking()
        .Include(w => w.Items.Where(i => i.ProductId == productId && i.NotifyOnRestock && i.NotifiedAt == null))
        .Where(w => w.Items.Any(i => i.ProductId == productId && i.NotifyOnRestock && i.NotifiedAt == null))
        .Take(batchSize)
        .ToListAsync(ct);
```

**IWishlistRepository.cs の修正**:
```csharp
// 削除
// Task<List<WishlistItem>> FindItemsByProductIdWithRestockNotifyAsync(string productId, CancellationToken ct = default);
// 追加
Task<List<Wishlist>> FindByProductIdWithRestockNotifyAsync(string productId, int batchSize, CancellationToken ct = default);
```

**WishlistService.cs `ProcessRestockNotificationAsync` の修正**:
```csharp
public async Task ProcessRestockNotificationAsync(string productId, CancellationToken ct = default)
{
    var wishlists = await wishlistRepository.FindByProductIdWithRestockNotifyAsync(productId, 100, ct);
    var itemCount = wishlists.SelectMany(w => w.Items).Count();
    logger.LogInformation("在庫復活通知対象: ProductId={ProductId}, 件数={Count}", productId, itemCount);
}
```

---

### C-3: DeletionRequest に状態遷移ドメインメソッド追加

**レポート指摘**: ddd-domain — `DsrService` が `DeletionRequest` の `Status`、`CompletedAt`、`FailureReason` を直接プロパティ代入。状態遷移ルールがドメインモデルに集約されていない。
**対象ファイル**:
- `Services/UserManagementService/Models/DeletionRequest.cs` — ドメインメソッド追加
- `Services/UserManagementService/Services/DsrService.cs` — ドメインメソッド呼び出しに変更

**DeletionRequest.cs への追加**（ファイル末尾の `}` の前に追加、`DeletionRequestStatus` クラスの前）:

```csharp
    // ── 状態遷移ドメインメソッド ──

    public void Cancel()
    {
        if (Status != DeletionRequestStatus.Pending)
            throw new Exceptions.BusinessException("猶予期間中の削除リクエストのみキャンセルできます");
        Status = DeletionRequestStatus.Cancelled;
    }

    public void StartProcessing()
    {
        if (Status != DeletionRequestStatus.Pending)
            throw new Exceptions.BusinessException("処理開始は Pending 状態のリクエストのみ可能です");
        Status = DeletionRequestStatus.Processing;
    }

    public void Complete(DateTimeOffset completedAt)
    {
        if (Status != DeletionRequestStatus.Processing)
            throw new Exceptions.BusinessException("完了は Processing 状態のリクエストのみ可能です");
        Status = DeletionRequestStatus.Completed;
        CompletedAt = completedAt;
    }

    public void Fail(string reason)
    {
        if (Status is not (DeletionRequestStatus.Processing or DeletionRequestStatus.Pending))
            throw new Exceptions.BusinessException("失敗はPending/Processing状態のリクエストのみ可能です");
        Status = DeletionRequestStatus.Failed;
        FailureReason = reason;
    }

    public void IncrementRetry(int maxRetries, string failureReason)
    {
        RetryCount++;
        if (RetryCount >= maxRetries)
            Fail(failureReason);
        // else: Status は Processing のまま維持
    }
```

**DsrService.cs の修正箇所**:

1. `CancelDeletionRequestAsync` (L55-62):
```csharp
// 修正前
if (request.Status != DeletionRequestStatus.Pending)
    throw new BusinessException("猶予期間中の削除リクエストのみキャンセルできます");
request.Status = DeletionRequestStatus.Cancelled;

// 修正後
request.Cancel();
```

2. `ProcessExpiredGracePeriodRequestsAsync` (L70-71):
```csharp
// 修正前
request.Status = DeletionRequestStatus.Processing;

// 修正後
request.StartProcessing();
```

3. `HandleDeletionCompletedAsync` (L86-91):
```csharp
// 修正前
request.Status = DeletionRequestStatus.Failed;
request.FailureReason = $"{serviceName}: {error}";

// 修正後
request.Fail($"{serviceName}: {error}");
```

4. `HandleDeletionCompletedAsync` (L94-95):
```csharp
// 修正前
request.Status = DeletionRequestStatus.Completed;
request.CompletedAt = timeProvider.GetUtcNow();

// 修正後
request.Complete(timeProvider.GetUtcNow());
```

5. `DsrTimeoutMonitorService.cs` (L35-44):
```csharp
// 修正前
request.RetryCount++;
if (request.RetryCount >= MaxRetries) { request.Status = ...; request.FailureReason = ...; }
else { request.Status = DeletionRequestStatus.Processing; }

// 修正後
request.IncrementRetry(MaxRetries, $"タイムアウト: {MaxRetries} 回のリトライ後も完了せず");
```

---

### C-4: AppDbContext — ConcurrencyException 変換時のログ出力追加

**レポート指摘**: error-logging — `DbUpdateConcurrencyException` を catch しているが、ログ出力なし。運用監視で検知不可。
**対象ファイル**: `Services/UserManagementService/Infrastructure/Persistence/AppDbContext.cs`

**現状コード** (L208-213):
```csharp
catch (DbUpdateConcurrencyException)
{
    throw new ConcurrencyException(
        "データが他のユーザーによって更新されました。再度お試しください。");
}
```

**修正後コード**:
```csharp
catch (DbUpdateConcurrencyException ex)
{
    var entityType = ex.Entries.FirstOrDefault()?.Entity.GetType().Name ?? "Unknown";
    // ILogger<AppDbContext> は primary constructor に追加が必要
    // ただし DbContext では ILogger を直接注入する代わりに、
    // ILoggerFactory を使用するパターンを採用
    throw new ConcurrencyException(
        "データが他のユーザーによって更新されました。再度お試しください。");
}
```

**追加修正**: AppDbContext の primary constructor に `ILoggerFactory` を追加。

```csharp
// 修正前
public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    TimeProvider timeProvider) : DbContext(options)

// 修正後
public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory) : DbContext(options)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<AppDbContext>();
```

**catch ブロック修正**:
```csharp
catch (DbUpdateConcurrencyException ex)
{
    var entityType = ex.Entries.FirstOrDefault()?.Entity.GetType().Name ?? "Unknown";
    _logger.LogWarning(ex, "楽観的ロック競合: {EntityType}", entityType);
    throw new ConcurrencyException(
        "データが他のユーザーによって更新されました。再度お試しください。");
}
```

---

## High 修正 — セキュリティ（6 件）

### H-1: Kafka `order.confirmed` リプレイ攻撃対策 — 冪等性チェックの導入

**レポート指摘**: security — 同一注文イベントの再送で年間購入額を不正加算可能。冪等性チェックなし。
**対象ファイル**:
- `Services/UserManagementService/BackgroundServices/OrderConfirmedConsumer.cs`
- `Services/UserManagementService/Services/MemberRankService.cs`
- `Services/UserManagementService/Services/Interfaces/IMemberRankService.cs`

**修正方針**: `MemberRankService.AddPurchaseAmountAsync` 内で、処理済み OrderId をチェックする。OrderId を専用の `processed_events` テーブルに記録し、重複処理を防止する。

> **⚠️ 注意**: `MemberRankService` は `AppDbContext` を直接注入していない（`IMemberRankRepository` 経由）。冪等性チェック用に `IProcessedEventRepository` を新設し、DI で注入する。

**修正 1 — MemberRankService.AddPurchaseAmountAsync のシグネチャ変更**:

```csharp
// IMemberRankService.cs — 修正後
Task AddPurchaseAmountAsync(
    string userId, string orderId, decimal amount, CancellationToken ct = default);
```

**修正 2 — 処理済みイベントテーブル追加**:

新規ファイル `Models/ProcessedEvent.cs`:
```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UserManagementService.Models;

[Table("processed_events")]
public class ProcessedEvent
{
    [Key]
    [Column("id")]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Column("event_id")]
    [Required]
    [MaxLength(100)]
    public string EventId { get; set; } = string.Empty;

    [Column("event_type")]
    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    [Column("processed_at")]
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

**修正 3 — AppDbContext に DbSet 追加**:
```csharp
public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();
```

**修正 4 — AppDbContext.OnModelCreating にインデックス追加**:
```csharp
modelBuilder.Entity<ProcessedEvent>(entity =>
{
    entity.HasIndex(e => new { e.EventId, e.EventType }).IsUnique();
});
```

**修正 5a — IProcessedEventRepository の新設**:

新規ファイル `Repositories/Interfaces/IProcessedEventRepository.cs`:
```csharp
namespace UserManagementService.Repositories.Interfaces;

public interface IProcessedEventRepository
{
    Task<bool> ExistsAsync(string eventId, string eventType, CancellationToken ct = default);
    Task AddAsync(ProcessedEvent processedEvent, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

新規ファイル `Repositories/ProcessedEventRepository.cs`:
```csharp
public class ProcessedEventRepository(AppDbContext context) : IProcessedEventRepository
{
    public async Task<bool> ExistsAsync(string eventId, string eventType, CancellationToken ct = default)
        => await context.ProcessedEvents
            .AnyAsync(e => e.EventId == eventId && e.EventType == eventType, ct);

    public async Task AddAsync(ProcessedEvent processedEvent, CancellationToken ct = default)
        => await context.ProcessedEvents.AddAsync(processedEvent, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
```

**修正 5b — MemberRankService に IProcessedEventRepository を DI 追加**:
```csharp
public class MemberRankService(
    IMemberRankRepository memberRankRepository,
    IProcessedEventRepository processedEventRepository,  // 追加
    IEventPublisherService eventPublisher,
    TimeProvider timeProvider,
    ILogger<MemberRankService> logger) : IMemberRankService
```

**修正 5c — MemberRankService.AddPurchaseAmountAsync 内に冪等性チェック**:
```csharp
public async Task AddPurchaseAmountAsync(
    string userId, string orderId, decimal amount, CancellationToken ct = default)
{
    // 冪等性チェック
    if (await processedEventRepository.ExistsAsync(orderId, "order.confirmed", ct))
    {
        logger.LogWarning("重複イベントをスキップ: OrderId={OrderId}", orderId);
        return;
    }

    var rank = await memberRankRepository.FindByUserIdAsync(userId, ct)
        ?? throw new NotFoundException($"会員ランクが見つかりません (UserId: {userId})");

    var promotion = rank.AddPurchaseAmount(amount, timeProvider.GetUtcNow());
    if (promotion is not null)
    {
        // ... 既存のランク昇格イベント発行処理 ...
    }

    // 処理済み記録（SaveChanges で購入額更新と同一トランザクション）
    await processedEventRepository.AddAsync(new ProcessedEvent
    {
        EventId = orderId,
        EventType = "order.confirmed"
    }, ct);

    await memberRankRepository.SaveChangesAsync(ct);
}
```

**修正 5d — Program.cs に DI 登録追加**:
```csharp
builder.Services.AddScoped<IProcessedEventRepository, ProcessedEventRepository>();
```

**修正 6 — OrderConfirmedConsumer.cs で OrderId を渡す**:
```csharp
// 修正前
await memberRankService.AddPurchaseAmountAsync(
    @event.UserId, @event.TotalAmount, stoppingToken);

// 修正後
await memberRankService.AddPurchaseAmountAsync(
    @event.UserId, @event.OrderId, @event.TotalAmount, stoppingToken);
```

---

### H-2: 負の購入金額バリデーション追加

**レポート指摘**: security — `MemberRank.AddPurchaseAmount` に金額下限チェックがない。
**対象ファイル**: `Services/UserManagementService/Models/MemberRank.cs`

**現状コード** (L63):
```csharp
public (string PreviousRank, string CurrentRank, decimal PointRate)? AddPurchaseAmount(decimal amount, DateTimeOffset now)
{
    AnnualPurchaseAmount += amount;
```

**修正後コード**:
```csharp
public (string PreviousRank, string CurrentRank, decimal PointRate)? AddPurchaseAmount(decimal amount, DateTimeOffset now)
{
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
    AnnualPurchaseAmount += amount;
```

---

### H-3: レート制限のエンドポイント適用

**レポート指摘**: security, config-di, resilience, dependency — `"api"` リミッターが定義済みだが未適用。
**対象ファイル**:
- `Services/UserManagementService/Endpoints/UserEndpoints.cs`
- `Services/UserManagementService/Endpoints/AddressEndpoints.cs`
- `Services/UserManagementService/Endpoints/WishlistEndpoints.cs`
- `Services/UserManagementService/Endpoints/PreferenceEndpoints.cs`
- `Services/UserManagementService/Endpoints/ActivityEndpoints.cs`
- `Services/UserManagementService/Endpoints/ConsentEndpoints.cs`
- `Services/UserManagementService/Endpoints/DsrEndpoints.cs`
- `Services/UserManagementService/Endpoints/MemberRankEndpoints.cs`
- `Services/UserManagementService/Endpoints/AdminUserEndpoints.cs`

**修正方針**: 各エンドポイントの `MapGroup()` チェーンに `.RequireRateLimiting("api")` を追加。

**修正例** (UserEndpoints.cs):
```csharp
// 修正前
var group = app.MapGroup("/api/v1/users")
    .WithTags("ユーザープロファイル")
    .RequireAuthorization();

// 修正後
var group = app.MapGroup("/api/v1/users")
    .WithTags("ユーザープロファイル")
    .RequireAuthorization()
    .RequireRateLimiting("api");
```

**全 9 ファイルで同様の修正を実施。** `ConsentEndpoints.cs` の匿名エンドポイントは既存の `"anonymous-consent"` リミッターが設定されているため変更不要。

---

### H-4: Kafka Outbox から Email 平文を除去

**レポート指摘**: security — `PublishDeletionNotificationAsync` で Email を平文で Kafka に発行。PII 漏洩リスク。
**対象ファイル**:
- `Services/UserManagementService/Services/EventPublisherService.cs`
- `Services/UserManagementService/Services/Interfaces/IEventPublisherService.cs`
- `Services/UserManagementService/Events/KafkaEvents.cs`
- `Services/UserManagementService/Services/DsrService.cs`

**修正 1 — IEventPublisherService.cs のシグネチャ変更**:
```csharp
// 修正前
Task PublishDeletionNotificationAsync(string userId, string email, CancellationToken ct = default);

// 修正後
Task PublishDeletionNotificationAsync(string userId, CancellationToken ct = default);
```

**修正 2 — EventPublisherService.cs の修正**:
```csharp
// 修正前
public async Task PublishDeletionNotificationAsync(
    string userId, string email, CancellationToken ct = default)
{
    var payload = new UserDeletionNotificationEventPayload(
        userId, email, timeProvider.GetUtcNow());

// 修正後
public async Task PublishDeletionNotificationAsync(
    string userId, CancellationToken ct = default)
{
    var payload = new UserDeletionNotificationEventPayload(
        userId, timeProvider.GetUtcNow());
```

**修正 3 — KafkaEvents.cs のペイロード変更**:
```csharp
// 修正前
public record UserDeletionNotificationEventPayload(
    string UserId,
    string Email,
    DateTimeOffset CompletedAt);

// 修正後（Email を除去、受信側が自DB から取得）
public record UserDeletionNotificationEventPayload(
    string UserId,
    DateTimeOffset CompletedAt);
```

**修正 4 — DsrService.cs の呼び出し元修正** (L97):
```csharp
// 修正前
await eventPublisher.PublishDeletionNotificationAsync(userId, user.Email, ct);

// 修正後
await eventPublisher.PublishDeletionNotificationAsync(userId, ct);
```

> **⚠️ ダウンストリーム影響**: このイベントは `HandleDeletionCompletedAsync` の成功時（ユーザー削除完了後）に発行される。受信側の `MailSendService` が userId でメールアドレスを検索する場合、ユーザーデータが既に削除されている可能性がある。以下のいずれかで対応:
> - **方法 A**: 削除完了後の一定期間はメールアドレスを保持（論理削除）
> - **方法 B**: イベント発行時にハッシュ化したメールアドレスを含める（通知先参照のみで「お客様のおメールアドレス宛」のような宛先表示用）
> - **エスカレーション**: メール通知のタイミングについてメールサービスチームと設計判断が必要

---

### H-5: ActivityDto から一般ユーザー向け IP アドレスを除去

**レポート指摘**: security — ActivityDto に `IpAddress` フィールドが含まれ、一般ユーザーに IP が露出。
**対象ファイル**:
- `Services/UserManagementService/DTOs/Responses/UserResponses.cs`
- `Services/UserManagementService/Services/ActivityService.cs` (DTO マッピング)

**修正方針**: ユーザー向けレスポンスの `ActivityDto` から `IpAddress` と `DeviceInfo` を除去。管理者向けには別 DTO を用意。

**修正 1 — UserResponses.cs**:
```csharp
// 一般ユーザー向け（IP/Device 除去）
public record ActivityDto(
    string Id,
    string ActivityType,
    DateTimeOffset Timestamp,
    string? Details);

// 管理者向け（IP/Device 含む）
public record AdminActivityDto(
    string Id,
    string ActivityType,
    DateTimeOffset Timestamp,
    string? Details,
    string? IpAddress,
    string? DeviceInfo);
```

**修正 2 — ActivityService.cs の MapToDto**:
- 一般ユーザー向け: `ActivityDto` にマッピング（IpAddress, DeviceInfo 除外）
- 管理者向け: `AdminActivityDto` にマッピング（既存の全フィールド含む）

---

### H-6: ウィッシュリストアイテム数の上限確認

**レポート指摘**: security, performance — アイテム数制限がない。
**対象ファイル**: `Services/UserManagementService/Models/Wishlist.cs`

**現状確認**: Wishlist.cs L10 に `private const int MaxItems = 100;` が定義済み、L49 に `if (Items.Count >= MaxItems)` チェックあり。

**結論**: **既に対策済み。レビュー指摘は誤検出。** `AddItem` メソッドに最大 100 件の制限が実装されている。追加修正不要。

---

## High 修正 — アーキテクチャ・DDD（5 件）

### H-7: BackgroundService の Service 層バイパス修正

**レポート指摘**: architecture — OutboxPublisher, DsrTimeoutMonitorService, MemberRankEvaluationService が Repository を直接参照。
**対象ファイル**:
- `Services/UserManagementService/BackgroundServices/DsrTimeoutMonitorService.cs`
- `Services/UserManagementService/BackgroundServices/MemberRankEvaluationService.cs`

**修正方針**:
- **OutboxPublisher**: Outbox の発行はインフラ関心事であり、ビジネスロジックを含まないため Repository 直接参照は許容と判断。**修正不要**
- **DsrTimeoutMonitorService**: `IDsrService` にタイムアウト処理メソッドを追加し、Service 経由に変更
- **MemberRankEvaluationService**: 既に `IMemberRankService.EvaluateAllRanksAsync` を呼んでおり、ロック取得のみ Repository 直接。ロック取得を `IMemberRankService` に委譲

**MemberRankEvaluationService.cs の修正**:
```csharp
// 修正前: IMemberRankRepository を直接使用してロック取得
var memberRankRepository = scope.ServiceProvider.GetRequiredService<IMemberRankRepository>();
var lockAcquired = await memberRankRepository.TryAcquireEvaluationLockAsync(stoppingToken);
if (lockAcquired)
{
    try
    {
        var memberRankService = scope.ServiceProvider.GetRequiredService<IMemberRankService>();
        await memberRankService.EvaluateAllRanksAsync(stoppingToken);
    }
    finally
    {
        await memberRankRepository.ReleaseEvaluationLockAsync(CancellationToken.None);
    }
}

// 修正後: IMemberRankService 経由でロック取得含めて委譲
var memberRankService = scope.ServiceProvider.GetRequiredService<IMemberRankService>();
await memberRankService.TryExecuteEvaluationWithLockAsync(stoppingToken);
```

**IMemberRankService.cs にメソッド追加**:
```csharp
Task TryExecuteEvaluationWithLockAsync(CancellationToken ct = default);
```

**MemberRankService.cs にメソッド追加**:
```csharp
public async Task TryExecuteEvaluationWithLockAsync(CancellationToken ct = default)
{
    var lockAcquired = await memberRankRepository.TryAcquireEvaluationLockAsync(ct);
    if (!lockAcquired) return;

    try
    {
        await EvaluateAllRanksAsync(ct);
    }
    finally
    {
        await memberRankRepository.ReleaseEvaluationLockAsync(CancellationToken.None);
    }
}
```

> **注意**: `using` 宣言の削除に伴い、MemberRankEvaluationService.cs の `using UserManagementService.Repositories.Interfaces;` も不要になる場合は削除すること。

**DsrTimeoutMonitorService.cs の修正**:
```csharp
// 修正前: IDeletionRequestRepository を直接使用
// 修正後: IDsrService 経由

public class DsrTimeoutMonitorService(
    IServiceScopeFactory scopeFactory,
    ILogger<DsrTimeoutMonitorService> logger) : BackgroundService
{
    // ...
    // scopeから IDsrService を取得して呼び出し
    var dsrService = scope.ServiceProvider.GetRequiredService<IDsrService>();
    await dsrService.ProcessTimedOutRequestsAsync(stoppingToken);
}
```

**IDsrService.cs に追加**:
```csharp
Task ProcessTimedOutRequestsAsync(CancellationToken ct = default);
```

**DsrService.cs にメソッド追加**:
```csharp
public async Task ProcessTimedOutRequestsAsync(CancellationToken ct = default)
{
    var processingRequests = await deletionRequestRepository
        .FindTimedOutProcessingAsync(TimeSpan.FromHours(24), ct);

    foreach (var request in processingRequests)
    {
        request.IncrementRetry(3, $"タイムアウト: 3 回のリトライ後も完了せず");
        if (request.Status == DeletionRequestStatus.Failed)
            logger.LogWarning("DSR タイムアウト（最大リトライ到達）: {RequestId}", request.Id);
        else
            logger.LogWarning("DSR タイムアウトリトライ: {RequestId}, RetryCount={RetryCount}",
                request.Id, request.RetryCount);
    }

    await deletionRequestRepository.SaveChangesAsync(ct);
}
```

---

### H-8: Address モデルにドメインメソッド追加

**レポート指摘**: ddd-domain — Address が Anemic Model。
**対象ファイル**: `Services/UserManagementService/Models/Address.cs`

**追加するドメインメソッド**:
```csharp
public void Update(string recipient, string zipCode, string prefecture,
    string city, string streetAddress, string? building, string? phoneNumber)
{
    Recipient = recipient;
    ZipCode = zipCode;
    Prefecture = prefecture;
    City = city;
    StreetAddress = streetAddress;
    Building = building;
    PhoneNumber = phoneNumber;
}

public void SetAsDefault() => IsDefault = true;
public void UnsetDefault() => IsDefault = false;
```

**AddressService.cs の修正**: 直接プロパティ代入を `address.Update(...)` に置換。

---

### H-9: Consent モデルにドメインメソッド追加

**レポート指摘**: ddd-domain — Consent が Anemic Model。
**対象ファイル**: `Services/UserManagementService/Models/Consent.cs`

**追加するドメインメソッド**:
```csharp
public void Grant(string? ipAddress, string? userAgent, string? policyTextHash)
{
    IsGranted = true;
    Version++;
    IpAddress = ipAddress;
    UserAgent = userAgent;
    PolicyTextHash = policyTextHash;
}

public void Revoke(string? ipAddress, string? userAgent)
{
    IsGranted = false;
    Version++;
    IpAddress = ipAddress;
    UserAgent = userAgent;
}
```

**ConsentService.cs の修正**: 直接プロパティ代入を `consent.Grant(...)` / `consent.Revoke(...)` に置換。

---

### H-10: Value Object 導入（設計検討項目）

**レポート指摘**: ddd-domain — Email, PhoneNumber, PostalCode が string 型のまま。
**対象**: 全 Models

**修正方針**: EF Core 10 の Value Conversion と組み合わせてValue Objectを導入する。ただし、影響範囲が広いため段階的に実施。

**第 1 段階で導入する Value Object**:
- なし（影響範囲が広すぎるため、エスカレーション E-4 として設計判断を優先）

**将来的に導入する候補**:
```csharp
public record EmailAddress
{
    public string Value { get; }
    public EmailAddress(string value)
    {
        if (!value.Contains('@'))
            throw new BusinessException("無効なメールアドレスです");
        Value = value;
    }
}
```

**備考**: 現時点では FluentValidation によるエンドポイント層のバリデーションで入力検証が担保されており、即時のリスクは低い。将来の改善項目として記録。

---

### H-11: Aggregate ナビゲーションプロパティのカプセル化

**レポート指摘**: ddd-domain — `User.Addresses` 等のコレクションが `ICollection<T>` で public setter。
**対象ファイル**: `Services/UserManagementService/Models/User.cs`

**修正方針**: setter を `private set` に変更し、外部からの直接操作を防止。ただし EF Core のマテリアライゼーションとの互換性を維持する必要がある。

**修正後コード** (User.cs):
```csharp
// 修正前
public ICollection<Address> Addresses { get; set; } = [];
public ICollection<Wishlist> Wishlists { get; set; } = [];
public ICollection<UserActivity> Activities { get; set; } = [];
public ICollection<Consent> Consents { get; set; } = [];
public ICollection<DeletionRequest> DeletionRequests { get; set; } = [];

// 修正後（EF Core はバッキングフィールド or private setter 経由でもマテリアライズ可能）
public ICollection<Address> Addresses { get; private set; } = [];
public ICollection<Wishlist> Wishlists { get; private set; } = [];
public ICollection<UserActivity> Activities { get; private set; } = [];
public ICollection<Consent> Consents { get; private set; } = [];
public ICollection<DeletionRequest> DeletionRequests { get; private set; } = [];
```

---

## High 修正 — OpenAPI・設定・DI（4 件）

### H-12: `app.MapOpenApi()` 追加

**レポート指摘**: api-endpoint, tech-lead — `AddOpenApi()` は登録済みだが `MapOpenApi()` がない。
**対象ファイル**: `Services/UserManagementService/Program.cs`

**修正**: `// 8. Endpoints` コメントの直前に追加。

```csharp
// 修正前
// 8. Endpoints
app.MapUserEndpoints();

// 修正後
// OpenAPI（開発環境のみ公開、または FallbackPolicy 適用下のため AllowAnonymous 付与）
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

// 8. Endpoints
app.MapUserEndpoints();
```

> **⚠️ 注意**: Program.cs で `FallbackPolicy = RequireAuthenticatedUser()` が設定されている場合、`MapOpenApi()` にも認証が要求される。開発用途であれば `AllowAnonymous()` を付与し、本番環境では公開しないようガードすること。

```csharp
```

---

### H-13: KafkaSettings プロパティ名の統一

**レポート指摘**: config-di — `KafkaSettings.GroupId` と `appsettings.json` の `ConsumerGroupId` が不一致。
**対象ファイル**: `Services/UserManagementService/Configurations/KafkaSettings.cs`

**修正方針**: appsettings.json のキー名に合わせて KafkaSettings のプロパティ名を変更。

```csharp
// 修正前
public string GroupId { get; init; } = "user-management-service";

// 修正後
public string ConsumerGroupId { get; init; } = "user-management-service";
```

**影響箇所**: `KafkaConsumerFactory.cs` など、`GroupId` を参照している箇所を全て `ConsumerGroupId` に変更。

---

### H-14: JwtSettings に ValidateOnStart 追加

**レポート指摘**: config-di — JWT 設定の不備が起動時に検出されない。
**対象ファイル**: `Services/UserManagementService/Program.cs`

**現状コード** (L104-106):
```csharp
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateDataAnnotations();
```

**修正後コード**:
```csharp
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

---

### H-15: Redis ヘルスチェック追加

**レポート指摘**: resilience, config-di, dependency, tech-lead — Redis ヘルスチェックが未登録。
**対象ファイル**:
- `Services/UserManagementService/UserManagementService.csproj` — パッケージ追加
- `Services/UserManagementService/Program.cs` — ヘルスチェック登録

**修正 1 — .csproj にパッケージ追加**:
```xml
<PackageReference Include="AspNetCore.HealthChecks.Redis" Version="9.*" />
```

**修正 2 — Program.cs のヘルスチェック登録** (L221-240 付近):

> **⚠️ 現状のバグ**: `AddHealthChecks()` が PostgreSQL 登録と Kafka 登録で二重呼び出しされている。Redis 追加と合わせて、単一のチェーンに統合する。

```csharp
// 修正前（PostgreSQL と Kafka で AddHealthChecks() が二重呼び出し）
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "postgresql", tags: ["ready"]);
}
else
{
    builder.Services.AddHealthChecks();
}

if (!string.IsNullOrWhiteSpace(kafkaSettings.BootstrapServers))
{
    builder.Services.AddHealthChecks()
        .AddCheck<KafkaHealthCheck>("kafka", tags: ["ready"]);
}

// 修正後（単一チェーンに統合）
var healthChecks = builder.Services.AddHealthChecks();

if (!string.IsNullOrEmpty(connectionString))
{
    healthChecks.AddNpgSql(connectionString, name: "postgresql", tags: ["ready"]);
}

if (!string.IsNullOrEmpty(redisConnectionString))
{
    healthChecks.AddRedis(redisConnectionString, name: "redis", tags: ["ready"]);
}

if (!string.IsNullOrWhiteSpace(kafkaSettings.BootstrapServers))
{
    healthChecks.AddCheck<KafkaHealthCheck>("kafka", tags: ["ready"]);
}
```

---

## High 修正 — データアクセス・パフォーマンス（6 件）

### H-16: UserRepository — 読み取り専用メソッドの追加

**レポート指摘**: data-access, performance — `FindByIdAsync` に AsNoTracking 未適用。読み取り/更新で共用。
**対象ファイル**:
- `Services/UserManagementService/Repositories/UserRepository.cs`
- `Services/UserManagementService/Repositories/Interfaces/IUserRepository.cs`

**修正方針**: 既存の `FindByIdAsync` は更新用として維持し、読み取り専用の `FindByIdReadOnlyAsync` を追加。

**IUserRepository.cs に追加**:
```csharp
Task<User?> FindByIdReadOnlyAsync(string id, CancellationToken ct = default);
```

**UserRepository.cs に追加**:
```csharp
public async Task<User?> FindByIdReadOnlyAsync(string id, CancellationToken ct = default)
    => await context.Users
        .AsNoTracking()
        .FirstOrDefaultAsync(u => u.Id == id, ct);
```

**UserService.cs の `GetByIdAsync` を修正**: `findByIdAsync` → `FindByIdReadOnlyAsync` に変更。

---

### H-17: MemberRankRepository — 読み取り専用メソッドの追加

**レポート指摘**: data-access, performance — `FindByUserIdAsync` に AsNoTracking 未適用。
**対象ファイル**: `Services/UserManagementService/Repositories/MemberRankRepository.cs`

**同 H-16 と同じパターンで `FindByUserIdReadOnlyAsync` を追加。**

```csharp
public async Task<MemberRank?> FindByUserIdReadOnlyAsync(string userId, CancellationToken ct = default)
    => await context.MemberRanks
        .AsNoTracking()
        .FirstOrDefaultAsync(m => m.UserId == userId, ct);
```

---

### H-18: DeletionRequestRepository — 無制限クエリに Take() 追加

**レポート指摘**: data-access, performance — 3 メソッドが `Take()` なしの全件取得。
**対象ファイル**: `Services/UserManagementService/Repositories/DeletionRequestRepository.cs`

**修正後コード**:
```csharp
public async Task<List<DeletionRequest>> FindExpiredGracePeriodAsync(CancellationToken ct = default)
    => await context.DeletionRequests
        .Where(d => d.Status == DeletionRequestStatus.Pending
            && d.GracePeriodEndsAt <= timeProvider.GetUtcNow())
        .Take(100)
        .ToListAsync(ct);

public async Task<List<DeletionRequest>> FindTimedOutProcessingAsync(TimeSpan timeout, CancellationToken ct = default)
    => await context.DeletionRequests
        .Where(d => d.Status == DeletionRequestStatus.Processing
            && d.RequestedAt.Add(timeout) <= timeProvider.GetUtcNow())
        .Take(100)
        .ToListAsync(ct);

public async Task<List<DeletionRequest>> FindByStatusAsync(string status, CancellationToken ct = default)
    => await context.DeletionRequests
        .Where(d => d.Status == status)
        .Take(100)
        .ToListAsync(ct);
```

---

### H-19: UserRepository.FindAllAsync — Select プロジェクション追加

**レポート指摘**: performance — 全カラム取得で PasswordHash 等の不要カラムが転送される。
**対象ファイル**: `Services/UserManagementService/Repositories/UserRepository.cs`

**修正方針**: 現状 User には PasswordHash カラムは存在しない（AuthService 管轄）ため、セキュリティ上の即時リスクなし。転送量削減のため将来的に DTO プロジェクションを検討。

**現時点では修正不要**（PasswordHash が存在しないため）。ただし、User モデルにナビゲーションプロパティ分の不要なカラム（RowVersion 等）がある点は軽微。

---

### H-20: ActivityRepository.FindByUserIdAsync — Select プロジェクション追加

**レポート指摘**: performance — 全カラム取得後に Service 層で DTO 変換。
**対象ファイル**: `Services/UserManagementService/Repositories/ActivityRepository.cs`

**修正方針**: H-5 で ActivityDto から IpAddress/DeviceInfo を除去するため、リポジトリレベルでの DB プロジェクションとの整合性も考慮。ただし、EF Core の変更追跡が不要（AsNoTracking 済み）で、レコード数もページネーション済みのため、現時点の優先度は低い。

**将来改善**: 必要に応じて `Select(a => new ActivityDto(...))` に変更。

---

### H-21: Address API にページネーション追加

**レポート指摘**: performance — `GET /addresses` にページネーションなし。
**対象**: `Services/UserManagementService/Endpoints/AddressEndpoints.cs`

**現状分析**: `MaxAddressesPerUser=10` でガードされており実害は限定的。API 設計の一貫性のために追加を検討するが、アドレスは件数が少ないため **現時点では修正不要**。

---

## High 修正 — 非同期・並行処理（1 件）

### H-22: PostgreSQL Advisory Lock のセッション管理改善

**レポート指摘**: async-concurrency — EF Core 接続プーリングにより Advisory Lock が不安定。
**対象ファイル**:
- `Services/UserManagementService/Repositories/OutboxEventRepository.cs`
- `Services/UserManagementService/Repositories/MemberRankRepository.cs`

**修正方針**: `pg_try_advisory_lock` をトランザクションスコープの `pg_try_advisory_xact_lock` に変更。トランザクションスコープのロックはトランザクション終了時に自動解放されるため、接続プーリングの影響を受けない。

> **⚠️ 重要**: `pg_try_advisory_xact_lock` はアクティブなトランザクションが存在しない場合、コマンド実行後即座にロックが解放される。そのため、呼び出し元の OutboxPublisher / MemberRankEvaluationService で明示的にトランザクションを開始する必要がある。`BeginTransactionAsync` でトランザクションを開始し、ロック取得 + 業務処理 + コミットを一連のトランザクション内で実行すること。

**修正後コード** (OutboxEventRepository.cs):
```csharp
// 修正前
public async Task<bool> TryAcquirePublishLockAsync(CancellationToken ct = default)
    => await context.Database.ExecuteSqlRawAsync(
        "SELECT pg_try_advisory_lock(hashtext('outbox_publisher'))", ct) > 0;

public async Task ReleasePublishLockAsync(CancellationToken ct = default)
    => await context.Database.ExecuteSqlRawAsync(
        "SELECT pg_advisory_unlock(hashtext('outbox_publisher'))", ct);

// 修正後（トランザクションスコープ。手動 unlock 不要）
public async Task<bool> TryAcquirePublishLockAsync(CancellationToken ct = default)
    => await context.Database.ExecuteSqlRawAsync(
        "SELECT pg_try_advisory_xact_lock(hashtext('outbox_publisher'))", ct) > 0;

// ReleasePublishLockAsync は不要に（トランザクション終了時に自動解放）
// ただし、既存の呼び出し元との互換性のため空実装を残す
public Task ReleasePublishLockAsync(CancellationToken ct = default)
    => Task.CompletedTask;
```

**注意**: OutboxPublisher の `try-finally` パターンは維持しつつ、`ReleasePublishLockAsync` を no-op にする。実際のロック解放はトランザクション/スコープ終了時に自動で行われる。

**OutboxPublisher.cs のトランザクション対応** (呼び出し元も修正必須):
```csharp
// OutboxPublisher.ExecuteAsync 内のロック取得部分をトランザクション内に移動
using var scope = scopeFactory.CreateScope();
var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxEventRepository>();
var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

await using var transaction = await context.Database.BeginTransactionAsync(stoppingToken);
try
{
    var lockAcquired = await outboxRepository.TryAcquirePublishLockAsync(stoppingToken);
    if (!lockAcquired)
    {
        await transaction.RollbackAsync(stoppingToken);
        await Task.Delay(MaxDelay, stoppingToken);
        continue;
    }

    var events = await outboxRepository.FindPendingAsync(100, stoppingToken);
    // ... イベント発行処理 ...
    await outboxRepository.SaveChangesAsync(stoppingToken);
    await transaction.CommitAsync(stoppingToken);
    // トランザクション終了時に xact_lock が自動解放
}
catch
{
    await transaction.RollbackAsync(stoppingToken);
    throw;
}
```

**MemberRankEvaluationService.cs** も同様にトランザクション内でロック取得する形に変更。

---

## High 修正 — エラー処理・ログ（2 件）

### H-23: AppDbContext の catch ブロックにログ追加

**C-4 と同一。C-4 の修正で対応済み。**

---

### H-24: Kafka Consumer で Correlation ID を伝搬

**レポート指摘**: error-logging — Kafka メッセージヘッダーから `X-Correlation-Id` を取得していない。
**対象ファイル**: 全 Kafka Consumer（5 ファイル）

**修正方針**: 各 Consumer の `try` ブロック先頭で、Kafka メッセージヘッダーから Correlation ID を取り出し、`LogContext.PushProperty` で設定。

**修正例** (OrderConfirmedConsumer.cs):
```csharp
var result = await Task.Run(() => consumer.Consume(stoppingToken), stoppingToken);

// Correlation ID をログコンテキストに設定
var correlationId = result.Message.Headers?
    .FirstOrDefault(h => h.Key == "X-Correlation-Id")?
    .GetValueBytes() is { } bytes
    ? System.Text.Encoding.UTF8.GetString(bytes)
    : Guid.NewGuid().ToString();

using (LogContext.PushProperty("CorrelationId", correlationId))
{
    var @event = JsonSerializer.Deserialize<OrderConfirmedEvent>(result.Message.Value);
    // ... 以降の処理
}
```

**全 5 Consumer に同様のパターンを適用。** 共通化のためヘルパーメソッドの抽出も検討。

---

## High 修正 — 耐障害性（3 件）

### H-25: Kafka Consumer に ConsumeException バックオフ追加

**レポート指摘**: resilience — `ConsumeException` 発生時にバックオフなし。
**対象ファイル**: 全 Kafka Consumer（5 ファイル）

**修正例** (OrderConfirmedConsumer.cs L40-42):
```csharp
// 修正前
catch (ConsumeException ex)
{
    logger.LogError(ex, "Kafka consume エラー: {Topic}", ex.ConsumerRecord?.Topic);
}

// 修正後
catch (ConsumeException ex)
{
    logger.LogError(ex, "Kafka consume エラー: {Topic}", ex.ConsumerRecord?.Topic);
    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
}
```

---

### H-26: Dead Letter Topic (DLT) の実装

**レポート指摘**: resilience — デシリアライズ失敗やビジネスロジック例外で処理不能なメッセージの退避先がない。
**対象ファイル**: 全 Kafka Consumer

**修正方針**: 各 Consumer の一般例外ハンドラー内で、処理失敗したメッセージを DLT に転送する。

> **⚠️ スコープ上の注意**: `result` 変数は `try` ブロック内で宣言されるため、`Consume()` 呼び出し前の例外では `result` が未定義となる。`result` を `try` ブロックの外側で `ConsumeResult<string, string>? result = null;` として宣言し、DLT 転送時に `result is not null` をチェックすること。

**修正例** (OrderConfirmedConsumer.cs):
```csharp
ConsumeResult<string, string>? result = null;
try
{
    result = await Task.Run(() => consumer.Consume(stoppingToken), stoppingToken);
    // ... イベント処理 ...
    consumer.Commit(result);
}
catch (ConsumeException ex)
{
    logger.LogError(ex, "Kafka consume エラー: {Topic}", ex.ConsumerRecord?.Topic);
    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    logger.LogError(ex, "イベント処理エラー: {Message}", ex.Message);

    // Dead Letter Topic に転送（result が取得済みの場合のみ）
    if (result is not null)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dlProducer = scope.ServiceProvider.GetRequiredService<IProducer<string, string>>();
            var dlMessage = new Message<string, string>
            {
                Key = result.Message.Key,
                Value = result.Message.Value,
                Headers = new Headers
                {
                    { "X-Error-Message", System.Text.Encoding.UTF8.GetBytes(ex.Message) },
                    { "X-Original-Topic", System.Text.Encoding.UTF8.GetBytes("order.confirmed") }
                }
            };
            await dlProducer.ProduceAsync("order.confirmed.dlt", dlMessage, stoppingToken);
            consumer.Commit(result);
        }
        catch (Exception dlEx)
        {
            logger.LogError(dlEx, "DLT 転送失敗: {Topic}", "order.confirmed.dlt");
        }
    }

    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
}
```

**注意**: DLT 転送の共通ヘルパー化を推奨。コードの重複を防ぐ。

---

### H-27: CacheService の Redis 例外ハンドリング拡張

**レポート指摘**: resilience — `catch (RedisConnectionException)` が限定的すぎる。
**対象ファイル**: `Services/UserManagementService/Services/CacheService.cs`

**修正方針**: `RedisConnectionException` を `RedisException`（基底クラス）に変更し、`RedisTimeoutException`、`RedisServerException` 等も包含。

```csharp
// 修正前（3 箇所とも同様に修正）
catch (RedisConnectionException ex)

// 修正後
catch (RedisException ex)
```

---

## High 修正 — C# 規約（1 件）

### H-28: bool プロパティ命名規約違反の修正

**レポート指摘**: csharp-standards — `ProcessingRestricted` → `IsProcessingRestricted` 等。
**対象ファイル**:
- `Services/UserManagementService/Models/User.cs`
- `Services/UserManagementService/Models/WishlistItem.cs`
- `Services/UserManagementService/DTOs/Responses/UserResponses.cs`

**修正対象プロパティ一覧**:

| 現在の名前 | 修正後の名前 | ファイル |
|-----------|------------|--------|
| `ProcessingRestricted` | `IsProcessingRestricted` | User.cs, UserResponses.cs |
| `DataExportRequested` | `IsDataExportRequested` | User.cs |
| `NotifyOnRestock` | `ShouldNotifyOnRestock` | WishlistItem.cs, UserResponses.cs |

**注意**: データベースカラム名 `[Column("processing_restricted")]` はそのまま維持。C# プロパティ名のみ変更。

**影響範囲**: Service, Endpoint, DTO, テストファイル全てでプロパティ名参照を更新する必要あり。リファクタリングツール（Rename Symbol）を使用して一括変更。

---

## High 修正 — NuGet 依存関係（2 件）

### H-29: StackExchange.Redis バージョンをワイルドカードに変更

**対象ファイル**: `Services/UserManagementService/UserManagementService.csproj`

```xml
<!-- 修正前 -->
<PackageReference Include="StackExchange.Redis" Version="2.12.14" />

<!-- 修正後 -->
<PackageReference Include="StackExchange.Redis" Version="2.*" />
```

---

### H-30: OpenTelemetry パッケージバージョンをワイルドカードに変更

**対象ファイル**: `Services/UserManagementService/UserManagementService.csproj`

```xml
<!-- 修正前 -->
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.15.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.15.0" />

<!-- 修正後 -->
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.*" />
<PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.*" />
```

---

## High 修正 — 機能完全性（2 件）

### H-31: DataExportService — エクスポートデータの保存処理追加

**レポート指摘**: tech-lead — 暗号化後にデータを破棄。ユーザーがエクスポートデータを取得する手段がない。
**対象ファイル**: `Services/UserManagementService/BackgroundServices/DataExportService.cs`

**修正方針**: 暗号化データをファイルシステムに保存。ダウンロード用エンドポイントは別途実装。

**修正後コード** (DataExportService.cs L38-40):
```csharp
var encryptedData = EncryptData(jsonBytes);

// エクスポートデータをファイルに保存
// ⚠️ パストラバーサル防止: userId をファイル名安全な文字列に正規化
var safeUserId = Path.GetFileName(userId);  // ../ 等のパストラバーサルを防止
if (string.IsNullOrWhiteSpace(safeUserId) || safeUserId != userId)
    throw new BusinessException($"無効な UserId: {userId}");

var exportDir = Path.Combine(
    exportOptions.Value.ExportDirectory ?? "/tmp/exports",
    safeUserId);
Directory.CreateDirectory(exportDir);
var fileName = $"export_{timeProvider.GetUtcNow():yyyyMMdd_HHmmss}.enc";
var filePath = Path.Combine(exportDir, fileName);
await File.WriteAllBytesAsync(filePath, encryptedData, stoppingToken);

await dataExportRepository.MarkExportCompletedAsync(userId, stoppingToken);
logger.LogInformation("データエクスポート完了: {UserId}, Path={Path}, EncryptedSize={Size}",
    userId, filePath, encryptedData.Length);
```

**DataExportSettings.cs に追加**:
```csharp
public string? ExportDirectory { get; init; }
```

**備考**: 本番環境では Blob Storage 等の永続化ストレージを使用すべき。ファイルシステム保存は開発環境向けの暫定実装。エスカレーション E-2 でインフラチームと最終方針を決定。

---

### H-32: outbox_events テーブルの FAILED 部分インデックス追加

**レポート指摘**: tech-lead — 設計書で要求されている FAILED 部分インデックスが未実装。
**対象ファイル**: `Services/UserManagementService/Infrastructure/Persistence/AppDbContext.cs`

**現状コード** (L163-165):
```csharp
modelBuilder.Entity<OutboxEvent>(entity =>
{
    entity.HasIndex(e => e.Status)
        .HasFilter("status = 'PENDING'");
});
```

**修正後コード**:
```csharp
modelBuilder.Entity<OutboxEvent>(entity =>
{
    // ⚠️ 同一カラム（Status）に複数の部分インデックスを定義するため、
    // HasDatabaseName で明示的に名前を付けないと EF Core が名前衝突を起こす
    entity.HasIndex(e => e.Status)
        .HasDatabaseName("ix_outbox_events_status_pending")
        .HasFilter("status = 'PENDING'");

    entity.HasIndex(e => e.Status)
        .HasDatabaseName("ix_outbox_events_status_failed")
        .HasFilter("status = 'FAILED'");
});
```

> **⚠️ 既存 PENDING インデックスの変更**: 現在の PENDING インデックスには `HasDatabaseName` が付与されていないため、FAILED インデックスを追加すると EF Core が同一カラムのインデックス名を自動生成し衝突する。**既存の PENDING インデックスにも `HasDatabaseName("ix_outbox_events_status_pending")` を追加する必要がある**。

**備考**: マイグレーション追加が必要。`dotnet ef migrations add AddFailedOutboxIndex`

---

## High 修正 — API 設計（2 件）

### H-41: ページネーションバリデーションの修正

**レポート指摘**: api-endpoint — pageSize と page の両方が無効な場合に 2 つのエラーが返される。
**対象ファイル**: `Services/UserManagementService/Endpoints/UserEndpoints.cs` (該当メソッドを確認)、`AdminUserEndpoints.cs` 等

**修正方針**: 現状のバリデーションロジックを確認し、エラーを Dictionary に集約して `Results.ValidationProblem` で一括返却するパターンが正しい。 RFC 9457 に準拠して複数エラー返却は正当であるため、レビュー指摘の是非を再検討 → **現時点では修正不要**（複数バリデーションエラーの同時返却は REST API のベストプラクティス）。

---

### H-42: PreferenceEndpoints のパスパラメータ名統一

**レポート指摘**: api-endpoint — 他は `{userId}` だが PreferenceEndpoints のみ `{id}` を使用。
**対象ファイル**: `Services/UserManagementService/Endpoints/PreferenceEndpoints.cs`

**現状コード** (L16):
```csharp
var group = app.MapGroup("/api/v1/users/{id}/preferences")
```

**修正後コード**:
```csharp
var group = app.MapGroup("/api/v1/users/{userId}/preferences")
```

**メソッドシグネチャの修正** (全メソッドで `string id` → `string userId` に変更):
```csharp
// 修正前
private static async Task<IResult> GetPreferences(
    string id, ClaimsPrincipal user, ...

// 修正後
private static async Task<IResult> GetPreferences(
    string userId, ClaimsPrincipal user, ...
```

---

## High 修正 — テスト品質（8 件）

### H-33〜H-40: テストカバレッジ拡充計画

**レポート指摘**: test-quality — テストカバレッジが推定 25-30%、目標 80% に大幅未達。

**追加が必要なテストファイル一覧**:

| # | テストファイル（新規作成） | テスト対象 | テストケース数（目安） |
|---|------------------------|----------|-----------------|
| H-33 | `Unit/Services/WishlistServiceTests.cs` | WishlistService（8 メソッド） | ~20 |
| H-34 | `Unit/Services/PreferenceServiceTests.cs` | PreferenceService（3 メソッド） | ~8 |
| H-35 | `Unit/Services/ActivityServiceTests.cs` | ActivityService（2 メソッド） | ~6 |
| H-36a | `Unit/Validators/UpdateAddressRequestValidatorTests.cs` | UpdateAddressRequestValidator | ~8 |
| H-36b | `Unit/Validators/AddWishlistItemRequestValidatorTests.cs` | AddWishlistItemRequestValidator | ~6 |
| H-36c | `Unit/Validators/UpdateWishlistRequestValidatorTests.cs` | UpdateWishlistRequestValidator | ~6 |
| H-36d | `Unit/Validators/UpdateUserStatusRequestValidatorTests.cs` | UpdateUserStatusRequestValidator | ~4 |
| H-36e | `Unit/Validators/CreateDeletionRequestValidatorTests.cs` | CreateDeletionRequestValidator | ~6 |
| H-36f | `Unit/Validators/ConsentUpdateRequestValidatorTests.cs` | ConsentUpdateRequestValidator | ~6 |
| H-37 | `Integration/Endpoints/AddressEndpointsTests.cs` 他 8 ファイル | 各 Endpoint（認証/認可/正常系/異常系） | ~各 10 |
| H-38 | `Integration/Repositories/*.cs` | 全 10 Repository（Testcontainers.PostgreSql 使用） | ~各 8 |
| H-39a | `Unit/BackgroundServices/OutboxPublisherTests.cs` | OutboxPublisher | ~8 |
| H-39b | `Unit/BackgroundServices/OrderConfirmedConsumerTests.cs` | OrderConfirmedConsumer | ~6 |
| H-39c | 他 7 BackgroundService テスト | 各 BackgroundService | ~各 6 |
| H-40 | `Fixtures/CustomWebApplicationFactory.cs` 修正 | InMemory → Testcontainers.PostgreSql | — |

**テスト実装のガイドライン**:
- 命名: `Should_X_When_Y` パターン遵守（既存テストと統一）
- AAA: `// Arrange` / `// Act` / `// Assert` コメント必須
- モック: NSubstitute 使用
- アサーション: Shouldly 使用
- Repository テスト: `Testcontainers.PostgreSql` で実 PostgreSQL を使用
- 統合テスト: `CustomWebApplicationFactory` + `FakeAuthHandler` 使用

**H-40: CustomWebApplicationFactory の Testcontainers 移行**:
```csharp
// 修正前
services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid()));

// 修正後
services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(_container.GetConnectionString()));
```

---

## Medium 修正一覧

以下は Medium 優先度の修正項目。Critical/High 修正完了後に段階的に対応。

| # | 出典 Agent | 対象ファイル | 指摘内容 | 修正方針 |
|---|-----------|------------|----------|---------|
| M-1 | security | 全 Kafka Consumer | **冪等性チェック不足**: `UserRegisteredConsumer` のみ重複チェック済み。他 Consumer にも同様の冪等性チェック追加 | H-1 で導入する `ProcessedEvent` テーブルを他 Consumer にも適用 |
| M-2 | security | `UserService.cs` L136-145 | **データエクスポートのクールダウン期間なし**: エクスポート完了後の即時再リクエスト可能 | `DataExportCompletedAt` から 24 時間以内は再リクエスト拒否する条件追加 |
| M-3 | security | `appsettings.Development.json` | **Development で `DetailedErrors: true`**: 開発環境でも機密情報漏洩のリスク | `"DetailedErrors": false` に変更（開発環境でもスタックトレースは Serilog 経由で取得可能） |
| M-4 | security | `UserService.cs` | **`DbUpdateConcurrencyException` の Service 層ハンドリング**: AppDbContext で変換処理済み（C-4）。Service 層からも `ConcurrencyException` を catch して 409 応答するフローは既にグローバル例外ハンドラーで対応。 | **修正不要**（C-4 でログ追加、グローバルハンドラーで 409 返却済み） |
| M-5 | security | `KafkaHealthCheck.cs` L27-30 | **HealthCheck エラー詳細の情報漏洩**: `HealthCheckResult.Unhealthy` に例外オブジェクトを渡している | `HealthCheckResult.Unhealthy("Kafka health check failed.")` に変更（例外オブジェクトを除去） |
| M-6 | security | 全 Kafka Consumer | **デシリアライズ結果のプロパティバリデーション不足**: `UserId` や `Email` が空文字の場合も処理される | デシリアライズ後に `string.IsNullOrWhiteSpace(@event.UserId)` 等のチェック追加 |
| M-7 | performance | `AppDbContext.cs` L172-210 | **ChangeTracker 走査の最適化**: `ISoftTimestampEntity` インターフェース導入で型判定を簡素化 | 影響が広いため将来改善。現状のオーバーヘッドは軽微 |
| M-8 | performance | `UserService.cs` L101-108 | **`SetProcessingRestrictionAsync` でキャッシュ無効化漏れ**: `SaveChangesAsync` 後に `RemoveAsync` が呼ばれていない | `await cacheService.RemoveAsync(ProfileCacheKey(id), ct);` を追加 |
| M-9 | performance | `WishlistRepository.cs` L36-40 | **`FindItemsByProductIdWithRestockNotifyAsync` に Take() なし** | C-2 の修正で Aggregate Root 経由に変更する際に `Take(batchSize)` を追加（対応済み） |
| M-10 | tech-lead | `CacheService.cs` L14-49 | **Redis 操作に CancellationToken 未伝搬**: StackExchange.Redis の制約 | 各メソッド冒頭に `ct.ThrowIfCancellationRequested();` を追加 |
| M-11 | tech-lead | `IUserService.cs`, `UserService.cs` | **`InitializeProfileAsync` がデッドコード**: 呼び出し元なし | インターフェースと実装の両方からメソッドを削除 |
| M-12 | tech-lead | `EventPublisherService.cs` L19 | **`PublishProfileUpdatedAsync` の固定フィールドリスト**: `["firstName", "lastName", "phoneNumber", "birthDate"]` が常に固定 | `UpdateUserRequest` の非 null フィールドのみを動的に収集。シグネチャに `UpdateUserRequest` を追加 |
| M-13 | tech-lead | `DsrTimeoutMonitorService.cs` L32 | **冗長なタイムアウト再チェック**: Repository の条件と重複 | C-3 / H-7 の修正で DsrService 経由に変更し、冗長チェックを削除 |
| M-14 | config-di | `Program.cs` | **Kafka 設定の二重読み込み**: `IOptions<KafkaSettings>` と `builder.Configuration.GetSection` で二重読み込み | `IOptions<KafkaSettings>` のみを使用し、直接 GetSection を排除 |
| M-15 | data-access | `Consent`, `Preference`, `Wishlist` | **[Timestamp] (RowVersion) 未設定**: 楽観的ロックが適用されていない | 各モデルに `[Timestamp] [Column("row_version")] public byte[] RowVersion { get; set; } = [];` を追加 |
| M-16 | data-access | `AppDbContext.cs` | **フィルタ付きインデックスの不足**: `DeletionRequest.Status` の PENDING/PROCESSING フィルタインデックス | `entity.HasIndex(d => d.Status).HasFilter("status = 'PENDING'");` 等を追加 |
| M-17 | architecture | BackgroundService 群 | **共通パターンのベースクラス抽出**: Advisory Lock 取得、エラーハンドリング、ループ構造 | `BackgroundServiceBase` 抽象クラスを作成し共通処理を集約。将来改善 |
| M-18 | api-endpoint | 各 Endpoint | **`.WithName()` が一部欠損**: OpenAPI ドキュメントのオペレーション名が自動生成になる | 全エンドポイントに `.WithName("適切な名前")` を追加 |
| M-19 | resilience | `Program.cs` Redis 接続設定 | **Redis 接続に Retry/CircuitBreaker ポリシー未設定**: Redis 一時障害時に即座にフォールバックし、再試行が行われない | `ConfigurationOptions` の `ConnectRetry`、`ReconnectRetryPolicy` を設定。または `StackExchange.Redis` の `IConnectionMultiplexer` 登録時に Polly ラッパーを適用 |

---

## 修正実施順序まとめ

### Phase 1: Critical 修正（即時）
1. C-1: AES-GCM 変更
2. C-4: AppDbContext ログ追加
3. C-3: DeletionRequest ドメインメソッド
4. C-2: WishlistItem Aggregate 境界

### Phase 2: High — セキュリティ
5. H-2: 負の金額バリデーション
6. H-3: レート制限適用
7. H-4: Email 平文除去
8. H-1: Kafka 冪等性チェック
9. H-5: ActivityDto IP 除去

### Phase 3: High — 設定・API
10. H-12: MapOpenApi() 追加
11. H-13: KafkaSettings 名統一
12. H-14: ValidateOnStart 追加
13. H-15: Redis ヘルスチェック
14. H-42: PreferenceEndpoints パスパラメータ統一

### Phase 4: High — DDD・アーキテクチャ
15. H-8: Address ドメインメソッド
16. H-9: Consent ドメインメソッド
17. H-11: ナビゲーションプロパティ private set
18. H-7: BackgroundService 層バイパス修正

### Phase 5: High — パフォーマンス・耐障害性
19. H-16, H-17: ReadOnly メソッド追加
20. H-18: DeletionRequest Take() 追加
21. H-22: Advisory Lock 改善
22. H-25: ConsumeException バックオフ
23. H-27: Redis 例外ハンドリング拡張
24. H-24: Correlation ID 伝搬

### Phase 6: High — NuGet・機能・C# 規約
25. H-29, H-30: バージョンワイルドカード
26. H-28: bool 命名規約
27. H-32: FAILED インデックス
28. H-31: DataExport 保存処理

### Phase 7: High — テスト
29. H-40: CustomWebApplicationFactory 修正
30. H-33〜H-39: テストファイル追加

### Phase 8: Medium 修正
31. M-1〜M-19: 一覧表に従い順次修正

---

*修正計画作成完了。各 Phase の完了後に `dotnet build` で成功を確認し、Phase 7 完了後に `dotnet test --collect:"XPlat Code Coverage"` でカバレッジ 80% を確認すること。*
