# ソースコードレビュー統合レポート

## 判定結果
- **対象**: InventoryManagementService（全 80+ ファイル）
- **判定**: ❌ **Rejected** — 重大な不備あり
- **レビュー日時**: 2026-04-06 23:22
- **プロジェクト**: SkiShop (.NET 10 マイクロサービス EC サイト)

## 判定根拠
- **判定ルール適用結果**: Critical 指摘が 2 件検出 → 自動判定 ❌ Rejected
- **最も重大な指摘**:
  1. OutboxEvent の CHECK 制約に `PROCESSING` ステータスが欠落しており、OutboxPublisher の全イベント発行が実行時に失敗する致命的バグ
  2. OutboxPublisher のイベント 1 件ごとに `SaveChangesAsync` を 2 回呼び出すループ設計で、50 件バッチ処理時に 100 回の DB ラウンドトリップが発生し本番 DB を圧迫

---

## 指摘サマリー

| Agent | 判定 | Critical | High | Medium | Low | スコア |
|-------|------|----------|------|--------|-----|--------|
| tech-lead | ⚠️ Warning | 0 | 4 | 7 | 3 | 17/20 |
| architecture-reviewer | ⚠️ Warning | 1 | 4 | 4 | 1 | 21/25 |
| ddd-domain-reviewer | ❌ Fail | 5 | 9 | 7 | 3 | — |
| api-endpoint-reviewer | ⚠️ Warning | 0 | 6 | 4 | 2 | 20/25 |
| csharp-standards-reviewer | ⚠️ Warning | 0 | 3 | 10 | 5 | 23/25 |
| async-concurrency-reviewer | ⚠️ Warning | 0 | 2 | 3 | 2 | 23/25 |
| error-logging-reviewer | ⚠️ Warning | 0 | 4 | 5 | 3 | — |
| data-access-reviewer | ✅ Pass | 0 | 1 | 6 | 2 | 26/30 |
| config-di-reviewer | ⚠️ Warning | 0 | 3 | 5 | 2 | 22/25 |
| security-reviewer | ⚠️ Warning | 1 | 5 | 8 | 3 | — |
| dependency-reviewer | ⚠️ Warning | 0 | 1 | 2 | 2 | 24/25 |
| test-quality-reviewer | ❌ Fail | 0 | 12 | 10 | 2 | 19/25 |
| performance-reviewer | ⚠️ Warning | 1 | 9 | 8 | 4 | — |
| resilience-reviewer | ⚠️ Warning | 0 | 3 | 5 | 3 | — |
| **合計（重複排除前）** | | **8** | **66** | **84** | **37** | |

### 重複排除後の指摘サマリー

重複する指摘を統合し、最も高い重要度を採用した結果:

| 重要度 | 重複排除前 | 重複排除後 | 主な重複パターン |
|--------|----------|----------|---------------|
| **Critical** | 8 | **2** | OutboxEvent CHECK 制約（Architecture+Resilience）、DDD Aggregate 境界（5件→競合解決で降格） |
| **High** | 66 | **25** | Redis キャッシュ重複（4 Agent）、JWT SecretKey（2 Agent）、設計書 CHECK 制約不一致（2 Agent） |
| **Medium** | 84 | **30** | マジックストリング（3 Agent）、ドメインメソッドバイパス（2 Agent）、ハードコード閾値（3 Agent） |
| **Low** | 37 | **12** | sealed 欠如、XMLDoc 欠如、命名改善 |

---

## 🚨 Critical 指摘検出 — 以下の修正なしではマージすべきでない

## Critical/High 指摘一覧（修正必須）

| # | 重要度 | 出典 Agent | カテゴリ | 対象ファイル | 行番号 | 指摘内容 | 修正コード例 |
|---|--------|-----------|---------|------------|--------|----------|------------|
| C-1 | **Critical** | architecture, resilience | データ整合性 | `Infrastructure/Persistence/AppDbContext.cs` | L237 | **OutboxEvent CHECK 制約に `PROCESSING` ステータスが欠落。** `OutboxPublisher` は `evt.Status = "PROCESSING"` を設定するが、CHECK 制約は `('PENDING', 'PUBLISHED', 'FAILED', 'DEAD_LETTER')` のみ許容。実行時に CHECK 制約違反で全 Outbox イベント発行が失敗する。 | `"status IN ('PENDING', 'PROCESSING', 'PUBLISHED', 'FAILED', 'DEAD_LETTER')"` に修正 |
| C-2 | **Critical** | performance | パフォーマンス | `BackgroundServices/OutboxPublisher.cs` | L78-114 | **イベント 1 件ごとに `SaveChangesAsync` を 2 回呼出。** 50 件バッチで 100 回の DB ラウンドトリップが発生。100ms ポーリングで DB 接続プールを圧迫する。 | バッチ単位でのステータス一括更新に変更 |
| H-1 | **High** | config-di, security | 秘密情報 | `appsettings.Development.json` | L24 | **JWT SecretKey が平文でハードコード。** `"SecretKey": "dev-only-signing-key-..."` が Git リポジトリに残留。 | `dotnet user-secrets` に移行し、appsettings から削除 |
| H-2 | **High** | architecture, tech-lead | 設計書逸脱 | `AppDbContext.cs` | L142 | **price_type CHECK 制約不一致。** 設計書: `('REGULAR', 'SALE', 'PROMOTION')`、実装: `('REGULAR', 'SALE', 'CLEARANCE')`。 | 設計者と合意の上統一 |
| H-3 | **High** | architecture, tech-lead | 設計書逸脱 | `AppDbContext.cs` | L152 | **product_images type CHECK 制約に `DETAIL` が欠落。** 設計書: `('MAIN', 'THUMBNAIL', 'GALLERY', 'DETAIL')`。 | `DETAIL` を追加 |
| H-4 | **High** | tech-lead | 設計書逸脱 | `AppDbContext.cs` | L93-113 | **inventories status の CHECK 制約が完全欠落。** 不正ステータス値が DB 保存可能。 | `CHECK (status IN ('IN_STOCK', 'LOW_STOCK', 'OUT_OF_STOCK', 'RESERVED', 'DISCONTINUED'))` を追加 |
| H-5 | **High** | architecture | レイヤー違反 | `Services/EventPublisherService.cs` | L17 | **EventPublisherService が AppDbContext を直接参照。** Service→Repository→EF Core のレイヤー規約に違反。 | `IOutboxRepository` を新設し委譲 |
| H-6 | **High** | architecture | レイヤー違反 | `Services/MessageDeduplicationService.cs` | L22 | **MessageDeduplicationService が AppDbContext を直接参照。** 同上。 | `IProcessedMessageRepository` を新設 |
| H-7 | **High** | api-endpoint | 入力バリデーション | `DTOs/Requests/ProductRequests.cs` | L84-103 | **PaginationParams のバリデーション未実行。** `Size=100000` 指定で巨大 DB クエリが発行可能。7 エンドポイントに影響。 | `PaginationParamsValidator` を新設し各エンドポイントで適用 |
| H-8 | **High** | api-endpoint | REST 規約 | `PriceEndpoints.cs` | L96 | **CreatePrice の Location ヘッダーが到達不能 URI。** `price.Id` で構成するが GET は `productId` ベース。 | `$"/api/prices/{price.ProductId}"` に修正 |
| H-9 | **High** | api-endpoint | REST 規約 | `SizeGuideEndpoints.cs` | L74 | **CreateSizeGuide の Location ヘッダーが到達不能 URI。** 同上パターン。 | `$"/api/size-guides/{guide.CategoryId}"` に修正 |
| H-10 | **High** | api-endpoint | REST 規約 | 各 UpdateEndpoints | — | **PUT エンドポイントが PATCH セマンティクスで設計。** 全フィールド nullable の部分更新を PUT で実行。 | `MapPatch` に変更、または DTO を全フィールド必須に |
| H-11 | **High** | config-di, api-endpoint | OpenAPI 未設定 | `Program.cs` | — | **`AddOpenApi()` / `MapOpenApi()` が未設定。** AGENTS.md §6.0 必須。csproj にパッケージは含まれるが未使用。 | `builder.Services.AddOpenApi();` + `app.MapOpenApi();` |
| H-12 | **High** | config-di | IOptions 不整合 | `Program.cs` | L119-149 | **Kafka Producer/Consumer が `IOptions<KafkaConfig>` を使用せず直接 Configuration アクセス。** ValidateOnStart のバリデーションパスと乖離。 | `KafkaConfig` を一度取得して再利用 |
| H-13 | **High** | csharp-standards | テスト可能性 | `Services/PriceService.cs` | L249 | **PriceService.MapToDto で `DateTimeOffset.UtcNow` を直接使用。** セール期間判定がテスト時に制御不能。 | `TimeProvider` を DI して `timeProvider.GetUtcNow()` を使用 |
| H-14 | **High** | csharp-standards, tech-lead | DRY 違反 | `Services/ProductService.cs` 他 3 件 | 各所 | **Redis キャッシュ Safe メソッドが 4 サービスに 12 メソッド重複。** バグ修正時に更新漏れリスク。 | 共通 `ICacheHelper` / `ResilientCacheService` に抽出 |
| H-15 | **High** | async-concurrency | BackgroundService | `OutboxPublisher.cs` | L127-129 | **finally 内の advisory unlock で `stoppingToken` 使用。** Graceful Shutdown 時にロック解放が失敗するリスク。 | `CancellationToken.None` を使用 |
| H-16 | **High** | async-concurrency | BackgroundService | `InventoryReservationCleanupService.cs` | L84-86 | **同上パターン。** finally 内の advisory unlock で `stoppingToken` 使用。 | `CancellationToken.None` を使用 |
| H-17 | **High** | api-endpoint | 入力バリデーション | `PriceEndpoints.cs` | L94, L121 | **changedBy の null チェック欠落。** `FindFirstValue()` 結果を null チェックなしで Service に渡す。 | `?? throw new UnauthorizedAccessException()` を追加 |
| H-18 | **High** | data-access | マイグレーション | `Migrations/` | — | **マイグレーションファイルが 0 件。** `MigrateAsync()` の適用対象なし。 | `dotnet ef migrations add InitialCreate` を実行 |
| H-19 | **High** | dependency | バージョン不整合 | `.csproj` | L15 | **`Microsoft.AspNetCore.OpenApi` が `10.0.5` ピン留め。** 他パッケージ・他サービスは `10.*` で不統一。 | `Version="10.*"` に変更 |
| H-20 | **High** | test-quality | カバレッジ不足 | テストプロジェクト全体 | — | **推定カバレッジ ~45%（目標 80%）。** InventoryService の ReserveAsync/ReleaseAsync/ConfirmReservationAsync（コアビジネスロジック）が完全未テスト。 | 35+ テストメソッドの追加が必要 |
| H-21 | **High** | test-quality | InMemory Provider | `EventPublisherServiceTests.cs` | L19-25 | **UseInMemoryDatabase が使用されている。** PostgreSQL 方言との非互換で本番バグを見逃す。 | Testcontainers.PostgreSql に移行 |
| H-22 | **High** | test-quality | 統合テスト不足 | `Integration/` | — | **全統合テストが 401 返却の検証のみ。** 認証済み CRUD フローのテストが完全欠如。 | TestAuthHandler を実装し認証済みテストを追加 |
| H-23 | **High** | ddd-domain → **降格** | ドメインロジック漏洩 | `Services/PriceService.cs` | L123-125 | **Price のセール属性が直接代入され `ApplySale()` バリデーションをバイパス。** 開始日 ≥ 終了日の不正セールが設定可能。 | 部分更新用ドメインメソッドを追加 |
| H-24 | **High** | error-logging | 例外処理 | `PriceService.cs`, `ReviewService.cs` | 各 catch | **トランザクション catch 内にログ出力がない。** InventoryService と一貫性がない。 | `logger.LogError(ex, ...)` を追加 |
| H-25 | **High** | error-logging | Correlation ID | `OutboxPublisher.cs` | — | **Outbox→Kafka 発行時に CorrelationId が Kafka ヘッダーに付与されない。** 受信側でのトレース追跡が不能。 | Kafka メッセージヘッダーに `X-Correlation-Id` を付与 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| 1 | **最優先** | architecture, performance | **C-1: Outbox CHECK 制約修正**: 既存 Migration が生成済みかどうかの確認が必要。本番デプロイ済みの場合は新規 Migration の追加が必須。 | テックリード + DBA |
| 2 | **最優先** | security, config-di | **H-1: JWT SecretKey の Git 履歴問題**: `dotnet user-secrets` に移行しても Git 履歴に残留する。キーのローテーションが必要か判断。 | セキュリティ担当 |
| 3 | **高優先** | architecture, tech-lead | **H-2: price_type の `PROMOTION` vs `CLEARANCE`**: 設計書の意図的な変更か誤りかをビジネスオーナーに確認。他サービスとの連携影響あり。 | PO + 設計者 |
| 4 | **高優先** | ddd-domain | **Aggregate 境界の設計判断**: Inventory/Price を Product の子エンティティとして維持するか、独立 Aggregate Root に昇格するか。現行実装は独立リポジトリだが、ドキュメントは子エンティティと記述しており矛盾。 | アーキテクト |
| 5 | **高優先** | test-quality | **ReserveAsync/ReleaseAsync のテスト追加をスプリントバックログに組込**: 注文処理のクリティカルパスがテストなし。 | テックリード + QA |
| 6 | **高優先** | test-quality | **GDPR AnonymizeUserReviewsAsync のテスト未実装**: GDPR Article 17（消去権）の技術的保証が欠如。法的リスクの評価が必要。 | 法務 + テックリード |
| 7 | **通常** | api-endpoint | **PUT vs PATCH の設計方針決定**: 全更新エンドポイントに影響する API 契約の破壊的変更。クライアント影響調査後に判断。 | API 設計担当 |
| 8 | **通常** | tech-lead | **InventoryMetrics の要否判断**: 定義済みだが DI 未登録・未使用。本番運用に必要なら早急に組込み、不要なら YAGNI で削除。 | テックリード |
| 9 | **通常** | resilience | **Polly パッケージの要否精査**: `Polly 8.*` と `Http.Resilience 9.*` が csproj に含まれるが一度も使用されていない。 | テックリード |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| 1 | ddd-domain (Critical) | architecture (High) + data-access (Pass) | **Inventory/Price の独立 Repository が Aggregate 境界違反か否か。** DDD は Product Aggregate の子エンティティとして独立 Repository を否定。Architecture/DataAccess はレイヤー構成として適切と評価。 | **Medium に降格** — ドキュメントの矛盾を修正する方向で解決 | 在庫操作は SELECT FOR UPDATE + トランザクション制御が必要であり、ProductRepository 経由では実装が過度に複雑化する。マイクロサービス境界内での実用的な設計判断として許容。ただし XML ドキュメントの「Product Aggregate の子エンティティ」記述を「独立 Aggregate Root」に修正すべき |
| 2 | ddd-domain (Critical) | performance (推奨) + data-access (模範的) | **ReviewVote の `ExecuteUpdateAsync` による HelpfulCount アトミックインクリメントが DDD Aggregate 境界を越えるか。** DDD は Review Aggregate Root 経由を要求。Performance/DataAccess は効率的なアトミック更新として高評価。 | **Medium に降格** — パフォーマンス最適化を優先 | `ExecuteUpdateAsync` は楽観的ロック競合を回避する秀逸な設計。DDD 的にはトレードオフだが、`ReviewService.MarkHelpfulAsync` で Review の存在確認後に実行しておりビジネスルールは保護されている。XML ドキュメントにトレードオフを明記 |
| 3 | ddd-domain (Critical) | csharp-standards (Medium) + tech-lead (Medium) | **CategoryService の直接プロパティ操作の重要度。** DDD は Critical（ドメインロジック漏洩）、他は Medium（一貫性の問題）。 | **High に降格** — 修正は必要だが Critical ではない | Category は単純な CRUD エンティティであり、`Activate()`/`Deactivate()` メソッドが既に存在する。メソッドの使用を徹底すれば解決する比較的軽微な修正。ただし Product との一貫性確保のため High を維持 |

---

## 設計書との照合結果

### 設計書からの逸脱

| # | 設計書の記述 | 実装の状態 | 影響度 |
|---|-----------|----------|--------|
| 1 | OutboxEvent status CHECK: `PENDING, PROCESSING, PUBLISHED, FAILED, DEAD_LETTER` | `PROCESSING` 欠落 | **Critical** — 全イベント発行失敗 |
| 2 | price_type CHECK: `REGULAR, SALE, PROMOTION` | `PROMOTION` → `CLEARANCE` に変更 | **High** — 仕様不一致 |
| 3 | product_images type CHECK: `MAIN, THUMBNAIL, GALLERY, DETAIL` | `DETAIL` 欠落 | **High** — 商品詳細画像登録不可 |
| 4 | products weight CHECK: `weight > 0` | `weight >= 0` で 0 許容 | **High** — 配送料計算リスク |
| 5 | inventories status CHECK 制約の定義 | CHECK 制約自体が欠落 | **High** — 不正値保存可能 |

### 未実装の設計要素

| # | 設計書に記載の機能 | 状態 |
|---|----------------|------|
| 1 | InventoryMetrics（カスタムメトリクス収集） | クラス定義済みだが DI 未登録・未使用 |
| 2 | OpenAPI ドキュメント生成 | パッケージ含まれるが `AddOpenApi()`/`MapOpenApi()` 未呼出 |
| 3 | Azure Blob Storage ヘルスチェック | PostgreSQL/Redis/Kafka のみ。Blob Storage 欠落 |
| 4 | Polly レジリエンスハンドラー | パッケージ含まれるが一度も使用されていない |
| 5 | EF Core マイグレーション | Migrations/ ディレクトリが空 |

---

## 各 Agent 詳細レポート

<details>
<summary>tech-lead レビューレポート（Critical: 0 / High: 4 / Medium: 7 / Low: 3）</summary>

**判定**: ⚠️ Warning — スコア 17/20

**優秀な実装パターン**:
- 禁止事項違反ゼロ（全10項目クリア）
- Outbox パターンの完全実装（pg_try_advisory_lock + 動的バックオフ + DEAD_LETTER 遷移）
- デッドロック防止戦略（productId 昇順ソート + SELECT FOR UPDATE ORDER BY）
- Kafka At-Least-Once + 冪等性保証（MessageDeduplicationService）
- セキュリティ多層防御（FallbackPolicy + SecurityHeaders + CORS + レート制限 + HSTS）

**主要指摘**: 設計書 CHECK 制約 4 件の不一致、Redis キャッシュヘルパー 4 重複、CategoryService/PriceService のドメインメソッドバイパス、InventoryMetrics DI 未登録
</details>

<details>
<summary>architecture-reviewer レビューレポート（Critical: 1 / High: 4 / Medium: 4 / Low: 1）</summary>

**判定**: ⚠️ Warning — スコア 21/25

**主要指摘**: OutboxEvent CHECK 制約 PROCESSING 欠落（Critical）、EventPublisherService/MessageDeduplicationService の Repository バイパス（High×2）、price_type/product_images CHECK 制約不一致（High×2）

**優秀な点**: Endpoints→Services→Repositories の基本レイヤー構造が正確、プロジェクト構成が設計書と完全一致、マイクロサービス境界が明確（他サービス DB への直接参照なし）
</details>

<details>
<summary>ddd-domain-reviewer レビューレポート（Critical: 5 / High: 9 / Medium: 7 / Low: 3）</summary>

**判定**: ❌ Fail

**競合解決により降格された指摘**:
- Critical→Medium: Inventory/Price の独立 Repository（実用的設計判断として許容。ドキュメント修正で解決）
- Critical→Medium: ReviewVote の ExecuteUpdateAsync（パフォーマンス最適化として許容）
- Critical→High: CategoryService の直接プロパティ操作（修正必要だが Critical ではない）

**残存する重要指摘**: Value Object 未定義（Money, SKU, LocationCode 等）、マジックストリングによるステータス管理、5 エンティティの貧血ドメインモデル、プロパティの `{ get; set; }` 公開による Aggregate カプセル化の欠如
</details>

<details>
<summary>api-endpoint-reviewer レビューレポート（Critical: 0 / High: 6 / Medium: 4 / Low: 2）</summary>

**判定**: ⚠️ Warning — スコア 20/25

**優秀な点**: 全 34 エンドポイントが 6 専用クラスに分離、FluentValidation の一貫適用（15 バリデーター）、CancellationToken 100% 伝搬、画像アップロードのマジックバイト検証

**主要指摘**: PaginationParams バリデーション未実行、Location ヘッダー URI 不正（Price/SizeGuide）、PUT/PATCH セマンティクス混同、OpenAPI 未設定、カテゴリ別商品取得ルート重複
</details>

<details>
<summary>csharp-standards-reviewer レビューレポート（Critical: 0 / High: 3 / Medium: 10 / Low: 5）</summary>

**判定**: ⚠️ Warning — スコア 23/25

**優秀な点**: 禁止パターン検出ゼロ、命名規則違反ゼロ、C# 14 機能（record, primary constructor, pattern matching, collection expressions）の積極活用、XML ドキュメントコメントの高品質

**主要指摘**: TimeProvider 未使用（PriceService, Inventory.Reserve）、キャッシュ Safe メソッド 4 重複、マジックストリング定数化未実施、sealed record 未適用（19 ファイル）
</details>

<details>
<summary>async-concurrency-reviewer レビューレポート（Critical: 0 / High: 2 / Medium: 3 / Low: 2）</summary>

**判定**: ⚠️ Warning — スコア 23/25

**優秀な点**: CancellationToken 完全伝搬（全 100+ async メソッド）、同期ブロッキングゼロ（.Result/.Wait()/Thread.Sleep()/async void 全て不検出）、Task.Yield() による起動ブロッキング回避、OperationCanceledException の例外フィルター分離

**主要指摘**: finally 内の advisory unlock で stoppingToken 使用（2 箇所）、キャッシュ無効化の逐次 await（Task.WhenAll で並列化可能）、RollbackAsync の ct 使用
</details>

<details>
<summary>error-logging-reviewer レビューレポート（Critical: 0 / High: 4 / Medium: 5 / Low: 3）</summary>

**判定**: ⚠️ Warning

**優秀な点**: PII ログ出力ゼロ、構造化ログ品質が全ファイルで統一（`{PropertyName}` テンプレート）、例外握りつぶしゼロ、`throw ex;` 使用ゼロ、CorrelationId ミドルウェアの適切な実装

**主要指摘**: PriceService/ReviewService の catch 内ログ欠落、Outbox→Kafka 発行時の CorrelationId 未伝搬、GlobalExceptionHandler での ErrorCode/Details 未伝搬、ErrorCodes.InvalidOperation XMLDoc 欠落
</details>

<details>
<summary>data-access-reviewer レビューレポート（Critical: 0 / High: 1 / Medium: 6 / Low: 2）</summary>

**判定**: ✅ Pass — スコア 26/30

**優秀な点**: 全 14 エンティティの設計品質が高い（DateTime.Now ゼロ、全 DateTimeOffset.UtcNow）、DbContext の包括的 OnModelCreating、N+1 リスクゼロ、悲観的ロックのデッドロック防止が模範的、ExecuteUpdateAsync の適切な活用、TimeProvider による SaveChanges Override

**主要指摘**: Migrations/ ディレクトリ空、FindByIdAsync を存在確認のみに使用（重量クエリ）、PriceHistory の不要な RowVersion、Product/Category の CreatedBy/UpdatedBy 欠落
</details>

<details>
<summary>config-di-reviewer レビューレポート（Critical: 0 / High: 3 / Medium: 5 / Low: 2）</summary>

**判定**: ⚠️ Warning — スコア 22/25

**優秀な点**: 全 BackgroundService が IServiceScopeFactory 経由で Scoped サービス取得（Captive Dependency なし）、ミドルウェアパイプライン順序が合理的、JWT 認証設定が包括的（ValidateIssuer/Audience/Lifetime/SigningKey 全 true）、FallbackPolicy による安全なデフォルト

**主要指摘**: JWT SecretKey ハードコード、Kafka 設定の直接 Configuration アクセス、OpenAPI 未設定、CacheConfig の DataAnnotations 未設定
</details>

<details>
<summary>security-reviewer レビューレポート（Critical: 1 / High: 5 / Medium: 8 / Low: 3）</summary>

**判定**: ⚠️ Warning

**優秀な点**: OWASP A01（アクセス制御）完全準拠、A03（インジェクション）リスクゼロ、A08（Mass Assignment）全エンドポイントで専用 DTO 使用、セキュリティヘッダー 7 種完備、全認証・認可設定が適切

**主要指摘**: JWT SecretKey ハードコード（Critical）、PaginationParams 未検証による巨大クエリ DoS リスク、IDOR 防止の追加検討（レビュー編集・削除権限）、画像アップロードのファイルサイズ制限強化、Kafka メッセージの認証・暗号化設定
</details>

<details>
<summary>performance-reviewer レビューレポート（Critical: 1 / High: 9 / Medium: 8 / Low: 4）</summary>

**判定**: ⚠️ Warning

**優秀な点**: AsNoTracking の使い分け適切、AnyAsync による存在確認、ページネーション対応の大半のエンドポイント

**主要指摘**: OutboxPublisher の N+1 SaveChanges（Critical）、CategoryEndpoints の全件取得→メモリ内 Take(100)、FindByIdAsync を存在確認のみに使用（4 つの Include 付き重量クエリ）、キャッシュ無効化の逐次 await、Select プロジェクション未使用の多数のクエリ
</details>

<details>
<summary>resilience-reviewer レビューレポート（Critical: 0 / High: 3 / Medium: 5 / Low: 3）</summary>

**判定**: ⚠️ Warning

**優秀な点**: PostgreSQL EnableRetryOnFailure 適切、Azure Blob Storage Exponential Retry 適切、Redis グレースフルデグラデーションが全 4 サービスで統一、ヘルスチェック 3 リソース（PostgreSQL/Redis/Kafka）、Outbox DEAD_LETTER 遷移、Advisory Lock 排他制御、Graceful Shutdown 30 秒

**主要指摘**: Polly パッケージ未使用（csproj にのみ存在）、Azure Blob Storage ヘルスチェック欠落、Kafka Consumer のポイズンメッセージ対策不足、Outbox PROCESSING ステータス滞留リスク
</details>

<details>
<summary>dependency-reviewer レビューレポート（Critical: 0 / High: 1 / Medium: 2 / Low: 2）</summary>

**判定**: ⚠️ Warning — スコア 24/25

**優秀な点**: 禁止パッケージゼロ、プレリリースゼロ、全必須パッケージが適切なバージョン帯で含まれる、PrivateAssets 設定適切、InternalsVisibleTo 設定済み

**主要指摘**: Microsoft.AspNetCore.OpenApi の `10.0.5` ピン留め不整合、テストプロジェクトの TreatWarningsAsErrors 未設定、InMemory パッケージと Testcontainers の共存
</details>

<details>
<summary>test-quality-reviewer レビューレポート（Critical: 0 / High: 12 / Medium: 10 / Low: 2）</summary>

**判定**: ❌ Fail — スコア 19/25

**優秀な点**: 全 73 テストが Should_X_When_Y パターン完全準拠、AAA パターンの厳格な適用（コメント付き）、NSubstitute + Shouldly の適切な使用、Testcontainers の正しい活用（Repository テスト）

**主要指摘**: 推定カバレッジ ~45%（目標 80%）、コアビジネスロジック（ReserveAsync/ReleaseAsync/ConfirmReservationAsync）の完全未テスト、InMemory Provider 使用（2 箇所）、Validator 12/15 未テスト、BackgroundService 6/7 未テスト、統合テストが 401 検証のみ
</details>

<details>
<summary>resilience-reviewer レビューレポート（Critical: 0 / High: 3 / Medium: 5 / Low: 3）</summary>

※ 上記に記載済み
</details>

---

## 総評

### 優秀な実装パターン（全 Agent 共通の高評価ポイント）

1. **禁止パターン検出ゼロ**: `.Result`、`.Wait()`、`Thread.Sleep()`、`Console.WriteLine`、`DateTime.Now`、`new HttpClient()` 等の全禁止パターンが一切使用されていない
2. **CancellationToken 完全伝搬**: 全 100+ async メソッドで CancellationToken が Endpoint → Service → Repository → EF Core まで完全に伝搬
3. **Outbox パターンの堅牢な設計**: DB トランザクション内 Outbox 書込み + pg_try_advisory_lock + 動的バックオフ + DEAD_LETTER 遷移（ただし CHECK 制約バグあり）
4. **デッドロック防止**: productId 昇順ソート + SELECT FOR UPDATE ORDER BY による循環待ち回避
5. **Redis グレースフルデグラデーション**: 全 4 サービスで Redis 障害時の DB フォールバックが一貫実装
6. **セキュリティ多層防御**: FallbackPolicy + SecurityHeaders(7種) + CORS + レート制限(2種) + HSTS + Server ヘッダー非公開
7. **構造化ログの統一**: 全ファイルで `{PropertyName}` テンプレート + PII ログ出力ゼロ

### 修正必須事項（マージ前）

1. **C-1**: OutboxEvent CHECK 制約に `PROCESSING` を追加
2. **C-2**: OutboxPublisher のバッチ SaveChanges 最適化
3. **H-1**: JWT SecretKey の appsettings からの除去

### 推奨改善事項（次回リファクタリング）

1. テストカバレッジの 45% → 80% 引上げ（特にコアビジネスロジック）
2. Redis キャッシュ Safe メソッドの共通化
3. マジックストリングの定数/enum 化
4. DDD Value Object の導入（Money, SKU 等）
5. 設計書 CHECK 制約の完全適合
