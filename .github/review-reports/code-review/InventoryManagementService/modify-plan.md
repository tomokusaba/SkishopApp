# InventoryManagementService 修正プラン

**作成日**: 2026-04-07  
**対象レポート**: check-report-1.md  
**総指摘件数**: 69 件（Critical: 2, High: 25, Medium: 30, Low: 12）  
**推定修正ファイル数**: 約 32 ファイル

---

## 1. 修正概要

本ドキュメントは check-report-1.md に記載された 69 件の課題に対する修正方針を、ファイル単位でまとめたものである。各修正には具体的なコード変更例、変更行番号、影響範囲を明示する。

### 修正優先度ルール

| 優先度 | 対応期限 | 基準 |
|--------|----------|------|
| **P0: Critical** | 即時（マージブロッカー） | 本番障害を引き起こすバグ、セキュリティ脆弱性 |
| **P1: High** | 当スプリント内 | 設計書逸脱、レイヤー違反、REST 規約違反、テスト不足 |
| **P2: Medium** | 次回リファクタリング | DRY 違反、パフォーマンス改善、コード品質向上 |
| **P3: Low** | 任意 | 命名改善、XMLDoc 追加、sealed 付与 |

---

## 2. P0: Critical 修正（2 件）— マージ前必須

### C-1: OutboxEvent CHECK 制約に `PROCESSING` ステータス追加

**対象ファイル**: `Infrastructure/Persistence/AppDbContext.cs`  
**対象行**: L237  
**関連課題**: C-1, エスカレーション #1

#### 現状コード
```csharp
entity.ToTable(t => t.HasCheckConstraint("ck_outbox_events_status", 
    "status IN ('PENDING', 'PUBLISHED', 'FAILED', 'DEAD_LETTER')"));
```

#### 修正後コード
```csharp
entity.ToTable(t => t.HasCheckConstraint("ck_outbox_events_status", 
    "status IN ('PENDING', 'PROCESSING', 'PUBLISHED', 'FAILED', 'DEAD_LETTER')"));
```

#### 追加作業
1. EF Core マイグレーション生成: `dotnet ef migrations add AddProcessingStatusToOutboxEvent`
2. マイグレーション適用: `dotnet ef database update`
3. **本番環境への適用時注意**: 既存 Migration が本番に適用済みの場合、新規 Migration として追加する必要がある。DBA と連携して適用順序を確認すること。

### テックリードによるレビュー結果

1, 2 を適用して下さい。本番には適用していません。

---

### C-2: OutboxPublisher のバッチ SaveChanges 最適化

**対象ファイル**: `BackgroundServices/OutboxPublisher.cs`  
**対象行**: L78-114  
**関連課題**: C-2

#### 問題点
イベント 1 件ごとに `SaveChangesAsync` を 2 回呼び出しており、50 件バッチで 100 回の DB ラウンドトリップが発生する。

#### 解決策の検討

| 案 | 概要 | メリット | デメリット | 採用 |
|----|------|---------|----------|------|
| **案 A** | PROCESSING への一括更新後、各イベント発行、成功分を一括 PUBLISHED 更新 | DB ラウンドトリップ最小化（最大 3 回/バッチ） | 発行途中で失敗した場合の部分コミットが複雑 | ❌ |
| **案 B** | イベントごとに PROCESSING→発行→PUBLISHED を行うが、SaveChanges は発行成功・失敗時の 1 回のみ | 現状の処理順序を維持しつつ SaveChanges を削減 | 若干の複雑化 | ✅ **採用** |
| **案 C** | ExecuteUpdateAsync で PROCESSING への一括更新、個別発行後に再度 ExecuteUpdateAsync | EF Core の Change Tracker を使用しない高速更新 | トランザクション管理が複雑、RowVersion との競合リスク | ❌ |

#### 修正後コード（案 B）

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    logger.LogInformation("OutboxPublisher started");

    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await using var connection = context.Database.GetDbConnection();
            await connection.OpenAsync(stoppingToken);
            await using var lockCmd = connection.CreateCommand();
            lockCmd.CommandText = "SELECT pg_try_advisory_lock(12345)";
            var acquired = (bool)(await lockCmd.ExecuteScalarAsync(stoppingToken) ?? false);

            if (!acquired)
            {
                await Task.Delay(_currentDelayMs, stoppingToken);
                continue;
            }

            try
            {
                var events = await context.OutboxEvents
                    .Where(e => e.Status == "PENDING"
                        || (e.Status == "FAILED" && e.RetryCount < e.MaxRetries))
                    .OrderBy(e => e.CreatedAt)
                    .Take(50)
                    .ToListAsync(stoppingToken);

                if (events.Count > 0)
                {
                    // ステータスを PROCESSING に一括更新（1 回目の SaveChanges）
                    foreach (var evt in events)
                    {
                        evt.Status = "PROCESSING";
                    }
                    await context.SaveChangesAsync(stoppingToken);

                    // 各イベントを Kafka に発行
                    foreach (var evt in events)
                    {
                        try
                        {
                            var message = new Message<string, string>
                            {
                                Key = evt.AggregateId,
                                Value = evt.Payload,
                                Headers = new Headers
                                {
                                    { "event-type", Encoding.UTF8.GetBytes(evt.EventType) }
                                }
                            };
                            await producer.ProduceAsync(evt.Topic, message, stoppingToken);

                            evt.Status = "PUBLISHED";
                            evt.PublishedAt = DateTimeOffset.UtcNow;

                            logger.LogInformation(
                                "Outbox イベント発行: {EventType}, AggregateId={AggregateId}",
                                evt.EventType, evt.AggregateId);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            evt.Status = "FAILED";
                            evt.RetryCount++;
                            evt.LastError = ex.Message[..Math.Min(ex.Message.Length, 2000)];

                            if (evt.RetryCount >= evt.MaxRetries)
                                evt.Status = "DEAD_LETTER";

                            logger.LogError(ex, "Outbox イベント発行失敗: {EventId}", evt.Id);
                        }
                    }

                    // 全イベントのステータスを一括保存（2 回目の SaveChanges）
                    await context.SaveChangesAsync(stoppingToken);

                    _currentDelayMs = MinDelayMs;
                }
                else
                {
                    _currentDelayMs = Math.Min(_currentDelayMs * 2, MaxDelayMs);
                }
            }
            finally
            {
                await using var unlockCmd = connection.CreateCommand();
                unlockCmd.CommandText = "SELECT pg_advisory_unlock(12345)";
                await unlockCmd.ExecuteScalarAsync(CancellationToken.None); // H-15 対応
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "OutboxPublisher エラー: {Message}", ex.Message);
            _currentDelayMs = MaxDelayMs;
        }

        await Task.Delay(_currentDelayMs, stoppingToken);
    }
}
```

#### 変更ポイント
1. L78-114 のループ内で `SaveChangesAsync` を 2 回→バッチ開始/終了時の計 2 回に削減
2. 50 件バッチで 100 回→2 回に DB ラウンドトリップ削減（50 倍改善）
3. H-15 も同時修正: `finally` 内の `ExecuteScalarAsync` で `CancellationToken.None` を使用

---

## 3. P1: High 修正（25 件）

### H-1: JWT SecretKey のハードコード削除

**対象ファイル**: `appsettings.Development.json`  
**対象行**: L24  
**関連課題**: H-1, エスカレーション #2

#### 修正手順

1. **appsettings.Development.json から SecretKey を削除**

```diff
 "Jwt": {
   "Issuer": "SkiShop-InventoryService-Dev",
-  "Audience": "SkiShop-Dev",
-  "SecretKey": "dev-only-signing-key-minimum-32-characters-long"
+  "Audience": "SkiShop-Dev"
 },
```

2. **dotnet user-secrets に移行**

```bash
cd Services/InventoryManagementService
dotnet user-secrets init
dotnet user-secrets set "Jwt:SecretKey" "dev-only-signing-key-minimum-32-characters-long"
```

3. **appsettings.Production.json を確認**（環境変数参照のみであること）

```json
{
  "Jwt": {
    "Issuer": "${JWT_ISSUER}",
    "Audience": "${JWT_AUDIENCE}"
  }
}
```

#### エスカレーション対応
- **Git 履歴に残る SecretKey**: 開発用キーであり本番キーではないため、キーローテーションは不要。ただし、セキュリティポリシー上必要な場合はセキュリティ担当に確認。

### テックリードによるレビュー結果

現在はまだローカルでの開発であり、Git の履歴に残っていても問題ありません。
キーローテションも不要です。

---

### H-2, H-3, H-4: 設計書 CHECK 制約の不一致修正

**対象ファイル**: `Infrastructure/Persistence/AppDbContext.cs`

#### H-2: price_type CHECK 制約（L142）

**エスカレーション対応**: 設計書では `PROMOTION` だが実装は `CLEARANCE`。ビジネスオーナーに確認が必要。

**選択肢**:
| 案 | 変更内容 | 影響 |
|----|---------|------|
| 案 A | 実装を設計書に合わせる（`CLEARANCE` → `PROMOTION`） | 既存データの `CLEARANCE` が不正値になる |
| 案 B | 設計書を実装に合わせる（設計書を更新） | 他サービス連携に影響する可能性 |
| 案 C | 両方を許容する（`REGULAR, SALE, PROMOTION, CLEARANCE`） | 最も安全だが冗長 |

### テックリードによるレビュー結果

案 A の実装を設計書に合わせる(CLEARANCE→ PROMOTION)形で実装して下さい。

#### H-3: product_images type CHECK 制約（L152）

```diff
- entity.ToTable(t => t.HasCheckConstraint("ck_product_images_type", "type IN ('MAIN', 'GALLERY', 'THUMBNAIL')"));
+ entity.ToTable(t => t.HasCheckConstraint("ck_product_images_type", "type IN ('MAIN', 'GALLERY', 'THUMBNAIL', 'DETAIL')"));
```

#### H-4: inventories status CHECK 制約追加（L93-113）

```csharp
modelBuilder.Entity<Inventory>(entity =>
{
    // ... 既存の設定 ...
    entity.ToTable(t =>
    {
        t.HasCheckConstraint("ck_inventory_quantity", "quantity >= 0");
        t.HasCheckConstraint("ck_inventory_reserved_quantity", "reserved_quantity >= 0");
        t.HasCheckConstraint("ck_inventory_quantity_reserved", "quantity >= reserved_quantity");
        t.HasCheckConstraint("ck_inventory_reorder_point", "reorder_point >= 0");
        // H-4: 新規追加
        t.HasCheckConstraint("ck_inventory_status", 
            "status IN ('IN_STOCK', 'LOW_STOCK', 'OUT_OF_STOCK', 'RESERVED', 'DISCONTINUED')");
    });
    // ...
});
```

---

### H-5, H-6: Service の Repository バイパス修正

#### H-5: EventPublisherService の AppDbContext 直接参照

**対象ファイル**: `Services/EventPublisherService.cs`

**修正方針**: `IOutboxRepository` を新設し、`EventPublisherService` は Repository 経由で Outbox にアクセスする。

**新規ファイル**: `Repositories/Interfaces/IOutboxRepository.cs`

```csharp
namespace InventoryManagementService.Repositories.Interfaces;

/// <summary>
/// OutboxEvent エンティティのリポジトリインターフェース。
/// </summary>
public interface IOutboxRepository
{
    /// <summary>
    /// OutboxEvent を追加する。
    /// </summary>
    Task AddAsync(OutboxEvent outboxEvent, CancellationToken ct = default);
    
    /// <summary>
    /// 変更を永続化する。
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

**新規ファイル**: `Repositories/OutboxRepository.cs`

```csharp
namespace InventoryManagementService.Repositories;

/// <summary>
/// OutboxEvent エンティティのリポジトリ実装クラス。
/// </summary>
public class OutboxRepository(AppDbContext context) : IOutboxRepository
{
    /// <inheritdoc />
    public async Task AddAsync(OutboxEvent outboxEvent, CancellationToken ct = default)
        => await context.OutboxEvents.AddAsync(outboxEvent, ct);

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
```

**修正ファイル**: `Services/EventPublisherService.cs`

```csharp
public class EventPublisherService(
    IOutboxRepository outboxRepository,  // AppDbContext から変更
    ILogger<EventPublisherService> logger) : IEventPublisherService
{
    public async Task PublishAsync<TEvent>(
        string eventType, string topic, string aggregateId, string aggregateType,
        TEvent payload, CancellationToken ct = default) where TEvent : class
    {
        var outboxEvent = new OutboxEvent
        {
            EventType = eventType,
            Topic = topic,
            AggregateId = aggregateId,
            AggregateType = aggregateType,
            Payload = JsonSerializer.Serialize(payload)
        };
        await outboxRepository.AddAsync(outboxEvent, ct);
        logger.LogInformation("Outbox イベント登録: EventType={EventType}, AggregateId={AggregateId}",
            eventType, aggregateId);
    }
    // ...
}
```

**Program.cs への DI 登録追加**:

```csharp
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
```

#### H-6: MessageDeduplicationService の AppDbContext 直接参照

**同様に `IProcessedMessageRepository` を新設**

**新規ファイル**: `Repositories/Interfaces/IProcessedMessageRepository.cs`

```csharp
namespace InventoryManagementService.Repositories.Interfaces;

public interface IProcessedMessageRepository
{
    Task<bool> ExistsAsync(string messageId, CancellationToken ct = default);
    Task AddAsync(ProcessedMessage message, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

**新規ファイル**: `Repositories/ProcessedMessageRepository.cs`

```csharp
namespace InventoryManagementService.Repositories;

public class ProcessedMessageRepository(AppDbContext context) : IProcessedMessageRepository
{
    public async Task<bool> ExistsAsync(string messageId, CancellationToken ct = default)
        => await context.ProcessedMessages.AnyAsync(m => m.MessageId == messageId, ct);

    public async Task AddAsync(ProcessedMessage message, CancellationToken ct = default)
        => context.ProcessedMessages.Add(message);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
```

**修正ファイル**: `Services/MessageDeduplicationService.cs`

```csharp
public class MessageDeduplicationService(
    IProcessedMessageRepository repository,  // AppDbContext から変更
    ILogger<MessageDeduplicationService> logger) : IMessageDeduplicationService
{
    public async Task<bool> IsProcessedAsync(string messageId, CancellationToken ct = default)
        => await repository.ExistsAsync(messageId, ct);

    public async Task MarkAsProcessedAsync(
        string messageId, string topic, int partition, long offset, CancellationToken ct = default)
    {
        await repository.AddAsync(new ProcessedMessage
        {
            MessageId = messageId,
            Topic = topic,
            Partition = partition,
            Offset = offset
        }, ct);
        await repository.SaveChangesAsync(ct);
        logger.LogDebug("メッセージ処理済み記録: MessageId={MessageId}, Topic={Topic}", messageId, topic);
    }
}
```

---

### H-7: PaginationParams バリデーション未実行

**対象ファイル**: 
- `Validators/PaginationParamsValidator.cs`（新規）
- `Endpoints/PriceEndpoints.cs`, `InventoryEndpoints.cs`, `ReviewEndpoints.cs` 等

**新規ファイル**: `Validators/PaginationParamsValidator.cs`

```csharp
using FluentValidation;
using InventoryManagementService.DTOs.Requests;

namespace InventoryManagementService.Validators;

/// <summary>
/// PaginationParams のバリデーター。
/// </summary>
public class PaginationParamsValidator : AbstractValidator<PaginationParams>
{
    public PaginationParamsValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(0)
            .WithMessage("ページ番号は 0 以上である必要があります");
        
        RuleFor(x => x.Size)
            .InclusiveBetween(1, 100)
            .WithMessage("ページサイズは 1〜100 の範囲である必要があります");
    }
}
```

**エンドポイント修正例（PriceEndpoints.cs L67-72）**:

```csharp
private static async Task<IResult> GetPriceHistory(
    string productId,
    [AsParameters] PaginationParams query,
    IValidator<PaginationParams> validator,  // 追加
    IPriceService service,
    CancellationToken ct)
{
    var validationResult = await validator.ValidateAsync(query, ct);
    if (!validationResult.IsValid)
        return Results.ValidationProblem(validationResult.ToDictionary());
    
    return Results.Ok(await service.GetHistoryAsync(productId, query.Page, query.Size, ct));
}
```

**影響エンドポイント（7 箇所）**:
- `PriceEndpoints.GetPriceHistory`
- `InventoryEndpoints.GetLowStockProducts`
- `InventoryEndpoints.GetReservations`
- `ReviewEndpoints.GetProductReviews`
- `ReviewEndpoints.GetPendingReviews`
- `ProductEndpoints.Search`（ProductSearchParams も同様に対応）
- `CategoryEndpoints.GetAll`

---

### H-8, H-9: Location ヘッダー URI 修正

**対象ファイル**: `Endpoints/PriceEndpoints.cs`, `Endpoints/SizeGuideEndpoints.cs`

#### H-8: PriceEndpoints.cs L96

```diff
- return Results.Created($"/api/prices/{price.Id}", price);
+ return Results.Created($"/api/prices/{price.ProductId}", price);
```

#### H-9: SizeGuideEndpoints.cs L74

```diff
- return Results.Created($"/api/size-guides/{guide.Id}", guide);
+ return Results.Created($"/api/size-guides/{guide.CategoryId}", guide);
```

---

### H-10: PUT vs PATCH セマンティクス修正

**対象ファイル**: 各 UpdateEndpoint

**解決策の検討**:

| 案 | 変更内容 | 影響 |
|----|---------|------|
| 案 A | `MapPut` → `MapPatch` に変更 | API 契約の破壊的変更（クライアント影響調査必要） |
| 案 B | DTO を全フィールド必須に変更（真の PUT セマンティクス） | 部分更新が不可能になる |
| 案 C | `MapPatch` を追加し、`MapPut` は非推奨化 | 並行期間中の移行が可能 |

**推奨**: 案 C（並行運用）— API 設計担当と協議の上、クライアント移行計画を策定

**暫定対応（エスカレーション #7 完了まで）**:
- 各 PUT エンドポイントに `[Obsolete]` 相当のコメントを追加
- OpenAPI ドキュメントで PATCH 推奨を明記

### テックリードによるレビュー結果

案 A の MapPatch に変更して下さい。

---

### H-11: OpenAPI 設定追加

**対象ファイル**: `Program.cs`

**修正箇所**: L50 付近（基盤サービス登録セクション）に追加

```csharp
// ── OpenAPI ドキュメント生成（.NET 10 方式） ──
builder.Services.AddOpenApi();
```

**修正箇所**: L316 付近（ミドルウェア/エンドポイントマッピング後）に追加

```csharp
// ── OpenAPI エンドポイント ──
app.MapOpenApi();

// ── Endpoint マッピング — 各ドメインのエンドポイントを登録 ──
app.MapProductEndpoints();
// ...
```

---

### H-12: Kafka 設定の IOptions パターン統一

**対象ファイル**: `Program.cs` L117-149

**修正後コード**:

```csharp
// Kafka Producer — IOptions<KafkaConfig> を使用
builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var kafkaConfig = sp.GetRequiredService<IOptions<KafkaConfig>>().Value;
    var config = new ProducerConfig
    {
        BootstrapServers = kafkaConfig.BootstrapServers,
        EnableIdempotence = true,
        Acks = Acks.All
    };
    return new ProducerBuilder<string, string>(config).Build();
});

// Kafka Consumer Factory — IOptions<KafkaConfig> を使用
builder.Services.AddSingleton<Func<string, IConsumer<string, string>>>(sp =>
{
    var kafkaConfig = sp.GetRequiredService<IOptions<KafkaConfig>>().Value;
    return groupIdSuffix =>
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaConfig.BootstrapServers,
            GroupId = (kafkaConfig.GroupId ?? "inventory-service") + "-" + groupIdSuffix,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };
        return new ConsumerBuilder<string, string>(config).Build();
    };
});
```

**Configurations/KafkaConfig.cs の修正**（DataAnnotations 追加）:

```csharp
using System.ComponentModel.DataAnnotations;

namespace InventoryManagementService.Configurations;

public record KafkaConfig
{
    [Required(ErrorMessage = "Kafka:BootstrapServers は必須です")]
    public required string BootstrapServers { get; init; }
    
    public string? GroupId { get; init; }
}
```

---

### H-13: TimeProvider 未使用（PriceService.MapToDto）

**対象ファイル**: `Services/PriceService.cs`

**修正方針**: `MapToDto` を static からインスタンスメソッドに変更し、`TimeProvider` を使用

**修正後コード**:

```csharp
public class PriceService(
    // ... 既存のパラメータ ...
    TimeProvider timeProvider) : IPriceService  // 追加
{
    // ... 既存のフィールド ...

    /// <summary>
    /// Price エンティティを PriceDto に変換する。セール期間の有効判定も行う。
    /// </summary>
    private PriceDto MapToDto(Price p)  // static を削除
    {
        var now = timeProvider.GetUtcNow();  // DateTimeOffset.UtcNow から変更
        var onSale = p.SalePrice.HasValue
            && (!p.SaleStartDate.HasValue || p.SaleStartDate <= now)
            && (!p.SaleEndDate.HasValue || p.SaleEndDate >= now);
        return new PriceDto(p.Id, p.ProductId, p.RegularPrice, p.SalePrice,
            p.SaleStartDate, p.SaleEndDate, p.CurrencyCode, p.IsActive, onSale);
    }
}
```

---

### H-14: Redis キャッシュ Safe メソッドの共通化

**対象ファイル**: 
- `Services/ProductService.cs`
- `Services/PriceService.cs`
- `Services/CategoryService.cs`
- `Services/InventoryService.cs`

**新規ファイル**: `Infrastructure/ResilientCacheService.cs`

```csharp
using Microsoft.Extensions.Caching.Distributed;

namespace InventoryManagementService.Infrastructure;

/// <summary>
/// Redis キャッシュのレジリエントラッパー。障害時にグレースフルデグラデーションを行う。
/// </summary>
public class ResilientCacheService(
    IDistributedCache cache,
    ILogger<ResilientCacheService> logger) : IResilientCacheService
{
    /// <summary>
    /// キャッシュ値を安全に取得する。Redis 障害時は null を返す。
    /// </summary>
    public async Task<string?> GetSafeAsync(string key, CancellationToken ct = default)
    {
        try
        {
            return await cache.GetStringAsync(key, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis キャッシュ読み取りエラー（フォールバック）: Key={Key}", key);
            return null;
        }
    }

    /// <summary>
    /// キャッシュ値を安全に設定する。Redis 障害時はログ出力のみ。
    /// </summary>
    public async Task SetSafeAsync<T>(string key, T value, int ttlSeconds, CancellationToken ct = default)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(value);
            await cache.SetStringAsync(key, json,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttlSeconds)
                }, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis キャッシュ書き込みエラー（無視）: Key={Key}", key);
        }
    }

    /// <summary>
    /// キャッシュを安全に削除する。Redis 障害時はログ出力のみ。
    /// </summary>
    public async Task RemoveSafeAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await cache.RemoveAsync(key, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis キャッシュ削除エラー（無視）: Key={Key}", key);
        }
    }
}
```

**新規ファイル**: `Infrastructure/IResilientCacheService.cs`

```csharp
namespace InventoryManagementService.Infrastructure;

public interface IResilientCacheService
{
    Task<string?> GetSafeAsync(string key, CancellationToken ct = default);
    Task SetSafeAsync<T>(string key, T value, int ttlSeconds, CancellationToken ct = default);
    Task RemoveSafeAsync(string key, CancellationToken ct = default);
}
```

**Program.cs への DI 登録**:

```csharp
builder.Services.AddScoped<IResilientCacheService, ResilientCacheService>();
```

**各 Service の修正**: private メソッドを削除し、`IResilientCacheService` を使用

---

### H-15, H-16: finally 内の stoppingToken 使用修正

**対象ファイル**: 
- `BackgroundServices/OutboxPublisher.cs` L127-129（C-2 修正で同時対応済み）
- `BackgroundServices/InventoryReservationCleanupService.cs` L84-86

**修正後コード（InventoryReservationCleanupService.cs）**:

```diff
 finally
 {
     await using var unlockCmd = connection.CreateCommand();
     unlockCmd.CommandText = "SELECT pg_advisory_unlock(12346)";
-    await unlockCmd.ExecuteScalarAsync(stoppingToken);
+    await unlockCmd.ExecuteScalarAsync(CancellationToken.None);
 }
```

---

### H-17: changedBy の null チェック欠落

**対象ファイル**: `Endpoints/PriceEndpoints.cs` L94, L121

**修正後コード**:

```csharp
// L94
var changedBy = user.FindFirstValue(ClaimTypes.NameIdentifier)
    ?? throw new UnauthorizedAccessException("認証情報からユーザー ID を取得できません");

// L121
var changedBy = user.FindFirstValue(ClaimTypes.NameIdentifier)
    ?? throw new UnauthorizedAccessException("認証情報からユーザー ID を取得できません");
```

---

### H-18: EF Core Migrations 空ディレクトリ

**対象**: `Migrations/` ディレクトリ

**作業手順**:

```bash
cd Services/InventoryManagementService
dotnet ef migrations add InitialCreate
dotnet ef database update
```

**注意事項**: 
- C-1, H-2, H-3, H-4 の CHECK 制約修正を先に適用してからマイグレーションを生成すること
- マイグレーションファイルは必ず Git にコミットすること

---

### H-19: OpenApi パッケージバージョン不整合

**対象ファイル**: `InventoryManagementService.csproj` L15

```diff
- <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.5" />
+ <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.*" />
```

---

### H-20〜H-25: テスト関連（別途テスト計画で対応）

テスト関連の High 指摘は、テスト追加計画として別途ドキュメント化が必要。

| # | 課題 | 対応 |
|---|------|------|
| H-20 | カバレッジ ~45%（目標 80%） | 35+ テストメソッド追加 |
| H-21 | InMemory Provider 使用 | Testcontainers.PostgreSql に移行 |
| H-22 | 統合テスト不足 | TestAuthHandler 実装 + 認証済み CRUD テスト追加 |
| H-23 | PriceService のドメインロジック漏洩 | 部分更新用ドメインメソッド追加 |
| H-24 | catch 内ログ欠落 | PriceService/ReviewService に `logger.LogError` 追加 |
| H-25 | CorrelationId 未伝搬 | Kafka ヘッダーに `X-Correlation-Id` 追加 |

---

## 4. P2: Medium 修正（30 件）— 主要なもの抜粋

### M-1〜M-5: マジックストリング定数化

**新規ファイル**: `Constants/StatusCodes.cs`

```csharp
namespace InventoryManagementService.Constants;

/// <summary>
/// OutboxEvent ステータス定数。
/// </summary>
public static class OutboxStatus
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Published = "PUBLISHED";
    public const string Failed = "FAILED";
    public const string DeadLetter = "DEAD_LETTER";
}

/// <summary>
/// Inventory ステータス定数。
/// </summary>
public static class InventoryStatus
{
    public const string InStock = "IN_STOCK";
    public const string LowStock = "LOW_STOCK";
    public const string OutOfStock = "OUT_OF_STOCK";
    public const string Reserved = "RESERVED";
    public const string Discontinued = "DISCONTINUED";
}

/// <summary>
/// Review ステータス定数。
/// </summary>
public static class ReviewStatus
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
}
```

---

### M-6: InventoryMetrics DI 未登録

**対象ファイル**: `Program.cs`

**解決策の検討**:

| 案 | 概要 |
|----|------|
| 案 A | DI に登録して使用開始 |
| 案 B | 未使用のため YAGNI で削除 |

### テックリードによるレビュー結果

案 A で DI に登録し使用開始して下さい。

---

### M-7: Polly パッケージ未使用

**対象ファイル**: `InventoryManagementService.csproj`

**解決策の検討**:

| 案 | 概要 |
|----|------|
| 案 A | HTTP クライアント呼び出しに Polly リトライを適用 |
| 案 B | 未使用のためパッケージ参照を削除 |

**推奨**: 現状 HTTP クライアント呼び出しがないため案 B。将来的に外部サービス連携時に案 A を検討。

```diff
- <PackageReference Include="Polly" Version="8.*" />
- <PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="9.*" />
```

---

### M-8〜M-12: sealed record 未適用

**対象ファイル**: `DTOs/Responses/` 配下全 19 ファイル

**修正パターン**:

```diff
- public record ProductDto(...)
+ public sealed record ProductDto(...)
```

---

## 5. P3: Low 修正（12 件）— 任意対応

| # | 課題 | 対象ファイル | 修正内容 |
|---|------|------------|---------|
| L-1 | sealed 欠如（Exception クラス） | `Exceptions/*.cs` | `public sealed class` に変更 |
| L-2 | XMLDoc 欠如（ErrorCodes） | `Exceptions/ErrorCodes.cs` | XMLDoc コメント追加 |
| L-3〜L-12 | 命名改善、コメント追加 | 各ファイル | 軽微な改善 |

---

## 6. 修正ファイル一覧（重複排除）

| # | ファイルパス | 関連課題 | 修正内容 |
|---|------------|---------|---------|
| 1 | `Infrastructure/Persistence/AppDbContext.cs` | C-1, H-2, H-3, H-4 | CHECK 制約修正 4 箇所 |
| 2 | `BackgroundServices/OutboxPublisher.cs` | C-2, H-15, H-25 | バッチ SaveChanges + finally 修正 + CorrelationId |
| 3 | `appsettings.Development.json` | H-1 | JWT SecretKey 削除 |
| 4 | `Services/EventPublisherService.cs` | H-5 | IOutboxRepository 使用に変更 |
| 5 | `Services/MessageDeduplicationService.cs` | H-6 | IProcessedMessageRepository 使用に変更 |
| 6 | `Repositories/Interfaces/IOutboxRepository.cs` | H-5 | 新規作成 |
| 7 | `Repositories/OutboxRepository.cs` | H-5 | 新規作成 |
| 8 | `Repositories/Interfaces/IProcessedMessageRepository.cs` | H-6 | 新規作成 |
| 9 | `Repositories/ProcessedMessageRepository.cs` | H-6 | 新規作成 |
| 10 | `Validators/PaginationParamsValidator.cs` | H-7 | 新規作成 |
| 11 | `Endpoints/PriceEndpoints.cs` | H-7, H-8, H-17 | バリデーション + Location ヘッダー + null チェック |
| 12 | `Endpoints/SizeGuideEndpoints.cs` | H-9 | Location ヘッダー修正 |
| 13 | `Endpoints/InventoryEndpoints.cs` | H-7 | PaginationParams バリデーション追加 |
| 14 | `Endpoints/ReviewEndpoints.cs` | H-7 | PaginationParams バリデーション追加 |
| 15 | `Program.cs` | H-11, H-12, H-5, H-6, H-14 | OpenAPI + Kafka IOptions + DI 登録 |
| 16 | `Services/PriceService.cs` | H-13, H-14, H-23, H-24 | TimeProvider + キャッシュ共通化 + ドメインメソッド |
| 17 | `Services/ProductService.cs` | H-14 | キャッシュ共通化 |
| 18 | `Services/CategoryService.cs` | H-14 | キャッシュ共通化 |
| 19 | `Services/InventoryService.cs` | H-14 | キャッシュ共通化 |
| 20 | `BackgroundServices/InventoryReservationCleanupService.cs` | H-16 | finally 内 CancellationToken 修正 |
| 21 | `InventoryManagementService.csproj` | H-19, M-7 | パッケージバージョン修正 |
| 22 | `Infrastructure/ResilientCacheService.cs` | H-14 | 新規作成 |
| 23 | `Infrastructure/IResilientCacheService.cs` | H-14 | 新規作成 |
| 24 | `Constants/StatusCodes.cs` | M-1〜M-5 | 新規作成 |
| 25 | `Configurations/KafkaConfig.cs` | H-12 | DataAnnotations 追加 |

---

## 7. マイグレーション実行順序

1. **C-1, H-2, H-3, H-4 の CHECK 制約修正を AppDbContext.cs に適用**
2. **マイグレーション生成**: `dotnet ef migrations add FixCheckConstraints`
3. **マイグレーション適用**: `dotnet ef database update`
4. **既存データの検証**: 不正値がないか確認
5. **本番デプロイ時**: DBA と連携してマイグレーション適用順序を確認

---

## 8. テスト計画（H-20〜H-22 対応）

### 追加が必要なテストケース（優先度順）

| # | テスト対象 | テスト種別 | 推定工数 |
|---|-----------|----------|---------|
| 1 | `InventoryService.ReserveAsync` | Unit | 3h |
| 2 | `InventoryService.ReleaseAsync` | Unit | 2h |
| 3 | `InventoryService.ConfirmReservationAsync` | Unit | 2h |
| 4 | `OutboxPublisher` バッチ処理 | Integration | 4h |
| 5 | 全 Validator（12/15 未テスト） | Unit | 6h |
| 6 | 認証済み CRUD フロー | Integration | 8h |
| 7 | `AnonymizeUserReviewsAsync` | Unit | 2h |

**合計推定工数**: 27h（約 3.5 人日）

---

## 9. エスカレーション事項の対応

| # | 内容 | 推奨対応 |
|---|------|---------|
| 1 | C-1: Outbox CHECK 制約 | Migration 生成・適用。本番は DBA と連携 |
| 2 | H-1: JWT SecretKey Git 履歴 | 開発用キーのためローテーション不要 |
| 3 | H-2: price_type 不一致 | 実装を仕様に合わせて変更 |
| 4 | Aggregate 境界 | 現行実装（独立リポジトリ）を許容。ドキュメントを更新 |
| 5 | ReserveAsync テスト | スプリントバックログに追加（P1） |
| 6 | AnonymizeUserReviewsAsync テスト | GDPR 準拠のため優先度 High でバックログ追加 |
| 7 | PUT vs PATCH | 案 C（並行運用）で移行計画策定 |
| 8 | InventoryMetrics | メトリクスを DI に登録し利用開始 |
| 9 | Polly 未使用 | パッケージ参照を削除 |

---

## 10. 変更確認チェックリスト

修正完了後、以下を確認すること:

- [ ] `dotnet build` が警告なしで成功する
- [ ] `dotnet ef migrations add` でマイグレーションが正常に生成される
- [ ] `dotnet test` で既存テストが全て Pass する
- [ ] 新規追加したテストが全て Pass する
- [ ] テストカバレッジが 80% 以上である
- [ ] `docker build` でイメージが正常にビルドされる
- [ ] ヘルスチェック `/health` `/health/ready` が正常に応答する
- [ ] OpenAPI エンドポイント `/openapi/v1.json` が正常に応答する

---

## 付録: 修正コードスニペット集

（上記各セクションに記載済み）

---

## 11. 計画書レビューで特定された追記事項

以下は計画書の照合検証（check-report-1.md との整合性確認）で特定された抜け漏れ・追記事項である。

### 追記 1: 設計書逸脱 #4 — weight CHECK 制約

**対象ファイル**: `Infrastructure/Persistence/AppDbContext.cs` L80

**check-report-1.md からの抜粋**:
> products weight CHECK: 設計書では `weight > 0` だが、実装は `weight >= 0` で 0 を許容

**エスカレーション対応**: 配送料計算に影響するため、ビジネスオーナーに確認が必要。

| 案 | 変更内容 | 影響 |
|----|---------|------|
| 案 A | 実装を設計書に合わせる（`>= 0` → `> 0`） | 既存の weight=0 データが CHECK 違反になる |
| 案 B | 設計書を実装に合わせる | 配送料無料ロジックの追加が必要 |
| 案 C | `weight > 0 OR weight IS NULL` に変更 | NULL 許容で 0 を禁止（デジタル商品は NULL） |

**推奨**: 案 C（NULL 許容 + 0 禁止）— ただしビジネスオーナー確認後に最終決定

---

### 追記 2: ProductSearchParamsValidator の追加

**対象ファイル**: `Validators/ProductSearchParamsValidator.cs`（新規作成）

H-7 で `ProductEndpoints.SearchProducts` が `ProductSearchParams` を使用しているが、バリデーターが欠落。

**新規ファイル**: `Validators/ProductSearchParamsValidator.cs`

```csharp
using FluentValidation;
using InventoryManagementService.DTOs.Requests;

namespace InventoryManagementService.Validators;

/// <summary>
/// ProductSearchParams のバリデーター。
/// </summary>
public class ProductSearchParamsValidator : AbstractValidator<ProductSearchParams>
{
    public ProductSearchParamsValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(0)
            .WithMessage("ページ番号は 0 以上である必要があります");

        RuleFor(x => x.Size)
            .InclusiveBetween(1, 100)
            .WithMessage("ページサイズは 1〜100 の範囲である必要があります");

        RuleFor(x => x.Keyword)
            .MaximumLength(100)
            .When(x => x.Keyword is not null)
            .WithMessage("キーワードは 100 文字以内で入力してください");

        RuleFor(x => x.SortBy)
            .Must(s => new[] { "CreatedAt", "Name", "Price", "Rating" }.Contains(s))
            .WithMessage("ソートフィールドは CreatedAt, Name, Price, Rating のいずれかです");
    }
}
```

**エンドポイント修正（ProductEndpoints.cs L116-125）**:

```csharp
private static async Task<IResult> SearchProducts(
    [AsParameters] ProductSearchParams search,
    IValidator<ProductSearchParams> validator,  // 追加
    IProductService service,
    CancellationToken ct)
{
    var validationResult = await validator.ValidateAsync(search, ct);
    if (!validationResult.IsValid)
        return Results.ValidationProblem(validationResult.ToDictionary());

    var criteria = new ProductSearchCriteria(search.Keyword, search.CategoryId, search.Brand);
    return Results.Ok(await service.SearchAsync(criteria, search.Page, search.Size, ct));
}
```

---

### 追記 3: CategoryService のドメインメソッドバイパス修正（競合解決 #3）

**対象ファイル**: `Services/CategoryService.cs` L85-87

**check-report-1.md 競合解決記録 #3**:
> CategoryService の直接プロパティ操作が High に降格された

**現状コード**:
```csharp
if (request.Name is not null) category.Name = request.Name;
if (request.Description is not null) category.Description = request.Description;
if (request.IsActive.HasValue) category.IsActive = request.IsActive.Value;
```

**修正後コード**:
```csharp
if (request.Name is not null) category.Name = request.Name;
if (request.Description is not null) category.Description = request.Description;
if (request.IsActive.HasValue)
{
    if (request.IsActive.Value)
        category.Activate();
    else
        category.Deactivate();
}
```

---

### 追記 4: XML ドキュメント修正（競合解決 #1, #2）

**対象ファイル**:
- `Models/Inventory.cs` L9
- `Models/Price.cs` L8
- `Repositories/ReviewRepository.cs`

#### 競合解決 #1（Inventory/Price の独立リポジトリ）

`Models/Inventory.cs` の XML ドキュメントを修正:

```diff
- /// Product Aggregate の子エンティティとして、ロケーション別の在庫数量・予約数量・ステータスを管理する。
+ /// 独立 Aggregate Root として、ロケーション別の在庫数量・予約数量・ステータスを管理する。
+ /// SELECT FOR UPDATE を伴うトランザクション制御のため、ProductRepository 経由ではなく
+ /// IInventoryRepository 経由で操作する。
```

`Models/Price.cs` の XML ドキュメントを修正:

```diff
- /// Product Aggregate の子エンティティとして、通常価格・セール価格・適用期間を管理する。
+ /// 独立 Aggregate Root として、通常価格・セール価格・適用期間を管理する。
+ /// 価格更新トランザクションと履歴管理のため、IPriceRepository 経由で操作する。
```

#### 競合解決 #2（ReviewVote の ExecuteUpdateAsync）

`Repositories/ReviewRepository.cs` の `IncrementHelpfulCountAsync` メソッドに以下の XML ドキュメントを追記:

```csharp
/// <summary>
/// HelpfulCount をアトミックにインクリメントする。
/// </summary>
/// <remarks>
/// DDD 的には Review Aggregate Root 経由での更新が望ましいが、
/// 楽観的ロック競合を回避するパフォーマンス最適化として ExecuteUpdateAsync を採用。
/// ReviewService.MarkHelpfulAsync で Review の存在確認後に実行されるため、
/// ビジネスルールは保護されている。
/// </remarks>
```

---

### 追記 5: H-25 CorrelationId 伝搬の詳細実装

**問題**: OutboxEvent に CorrelationId カラムがなく、Kafka ヘッダーに伝搬されていない。

**対象ファイル**:
- `Models/OutboxEvent.cs`（カラム追加）
- `Services/EventPublisherService.cs`（CorrelationId 取得・設定）
- `BackgroundServices/OutboxPublisher.cs`（Kafka ヘッダー付与）

#### Step 1: OutboxEvent にカラム追加

`Models/OutboxEvent.cs`:

```csharp
/// <summary>リクエストの相関 ID。分散トレーシングに使用。</summary>
[Column("correlation_id")]
[MaxLength(36)]
public string? CorrelationId { get; set; }
```

#### Step 2: EventPublisherService で CorrelationId を取得

`Services/EventPublisherService.cs`:

```csharp
public class EventPublisherService(
    IOutboxRepository outboxRepository,  // H-5 で変更
    IHttpContextAccessor httpContextAccessor,  // 追加
    ILogger<EventPublisherService> logger) : IEventPublisherService
{
    public async Task PublishAsync<TEvent>(
        string eventType, string topic, string aggregateId, string aggregateType,
        TEvent payload, CancellationToken ct = default) where TEvent : class
    {
        var correlationId = httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString();

        var outboxEvent = new OutboxEvent
        {
            EventType = eventType,
            Topic = topic,
            AggregateId = aggregateId,
            AggregateType = aggregateType,
            Payload = JsonSerializer.Serialize(payload),
            CorrelationId = correlationId  // 追加
        };
        await outboxRepository.AddAsync(outboxEvent, ct);
        // ...
    }
}
```

**Program.cs への DI 登録**:

```csharp
builder.Services.AddHttpContextAccessor();
```

#### Step 3: OutboxPublisher で Kafka ヘッダーに付与

`BackgroundServices/OutboxPublisher.cs` L89-93:

```csharp
var message = new Message<string, string>
{
    Key = evt.AggregateId,
    Value = evt.Payload,
    Headers = new Headers
    {
        { "event-type", Encoding.UTF8.GetBytes(evt.EventType) },
        { "X-Correlation-Id", Encoding.UTF8.GetBytes(evt.CorrelationId ?? Guid.NewGuid().ToString()) }  // 追加
    }
};
```

---

### 追記 6: Azure Blob Storage ヘルスチェック（未実装の設計要素）

**check-report-1.md L130**:
> PostgreSQL/Redis/Kafka のみ。Blob Storage 欠落

**エスカレーション対応**: Azure Blob Storage を実際に使用しているか確認が必要。

**Option A**: Blob Storage を使用している場合 — ヘルスチェック追加

`Program.cs`:

```csharp
// ヘルスチェック — Blob Storage 追加
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql", tags: ["ready"])
    .AddRedis(redisConnectionString, name: "redis", tags: ["ready"])
    .AddAzureBlobStorage(blobConnectionString, name: "blob", tags: ["ready"]);
```

**Option B**: Blob Storage を使用していない場合 — 設計書を更新

---

## 12. 更新された修正ファイル一覧

| # | ファイルパス | 関連課題 | 修正内容 |
|---|------------|---------|---------|
| 26 | `Validators/ProductSearchParamsValidator.cs` | H-7 追記 | 新規作成 |
| 27 | `Models/OutboxEvent.cs` | H-25 追記 | CorrelationId カラム追加 |
| 28 | `Models/Inventory.cs` | 競合解決 #1 | XML ドキュメント修正 |
| 29 | `Models/Price.cs` | 競合解決 #1 | XML ドキュメント修正 |
| 30 | `Repositories/ReviewRepository.cs` | 競合解決 #2 | XML ドキュメント追記 |
| 31 | `Services/CategoryService.cs` | 競合解決 #3, H-14 | ドメインメソッド使用 + キャッシュ共通化 |
| 32 | `Endpoints/ProductEndpoints.cs` | H-7 追記 | ProductSearchParams バリデーション追加 |

---

## 13. 更新されたエスカレーション事項

| # | 内容 | 推奨対応 |
|---|------|---------|
| 10 | 設計書逸脱 #4: weight CHECK（`> 0` vs `>= 0`） | ビジネスオーナーに確認後、案 A/B/C から選択 |
| 11 | Azure Blob Storage ヘルスチェック | 使用有無を確認し、使用時のみ追加 |

---

## 14. 計画書更新サマリ

**更新日**: 2026-04-07（計画書レビュー後）

### 抜け漏れ項目の補完

| # | 項目 | 状態 | 対応セクション |
|---|------|------|---------------|
| 1 | 設計書逸脱 #4（weight CHECK） | ✅ 追記完了 | 追記 1 |
| 2 | ProductSearchParamsValidator | ✅ 追記完了 | 追記 2 |
| 3 | CategoryService ドメインメソッドバイパス | ✅ 追記完了 | 追記 3 |
| 4 | XML ドキュメント修正（競合解決 #1, #2） | ✅ 追記完了 | 追記 4 |
| 5 | H-25 CorrelationId 詳細実装 | ✅ 追記完了 | 追記 5 |
| 6 | Azure Blob Storage ヘルスチェック | ✅ 追記完了 | 追記 6 |

### 修正ファイル数の更新

- **当初**: 約 25 ファイル
- **更新後**: 約 32 ファイル（+7 ファイル）

### エスカレーション事項の更新

- **当初**: 9 件
- **更新後**: 11 件（+2 件）
