# ドキュメントレビュー統合レポート — イテレーション 2

## 判定結果
- **対象**: `design-docs/inventory-management-design.md`（2,133 行、修正後イテレーション 2）
- **判定**: ❌ **Rejected** — Critical 指摘 1 件検出。是正完了後に再レビュー必須
- **レビュー日時**: 2026-04-03
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)
- **前回レビュー**: check-report-1.md (Critical 7, High 16) → fix-report-1.md (7C fixed, 16H fixed)

---

## 前回 Critical/High 修正検証結果

### Critical 修正（全 7 件 ✅ 解消確認済み）

| # | 指摘 ID | 指摘内容 | 修正状態 | 検証結果 |
|---|---------|---------|---------|---------|
| 1 | C-01 | `TIMESTAMP WITH TIME ZONE` 未使用 | ✅ 解消 | 全テーブル（products〜outbox_events）の日時カラムが `TIMESTAMP WITH TIME ZONE` に変更済み |
| 2 | C-02 | `Review`/`ReviewResponse` エンティティ未定義 | ✅ 解消 | `reviews`/`review_responses` テーブル、EF Core エンティティ、API、Service インターフェース全て追加済み |
| 3 | C-03 | gRPC サービス定義の完全欠落 | ✅ 解消 | §5.1 に `inventory.proto`、`InventoryGrpcService` 実装、gRPC Deadline（500ms）、スコープ認証を記載 |
| 4 | C-04 | Outbox パターン（ADR-0005）の完全欠落 | ✅ 解消 | `outbox_events` テーブル、`OutboxPublisher` BackgroundService（Advisory Lock、動的バックオフ 100ms〜5s）を追加 |
| 5 | C-05 | `size_guides` テーブル欠落 | ✅ 解消 | テーブル定義、FK 制約、SizeGuide API エンドポイント、クラス構造を追加 |
| 6 | C-06 | `reserved_at` + `InventoryReservationCleanupService` 未定義 | ✅ 解消 | inventory テーブルに `reserved_at` カラム追加、§5.5 に CleanupService（15 分タイムアウト、Advisory Lock）を追加 |
| 7 | C-07 | 金額データ型の不一致 `DECIMAL(10,2)` → `DECIMAL(12,2)` | ✅ 解消 | prices, price_histories, product_suppliers の全金額カラムが `DECIMAL(12,2)` に統一 |

### High 修正（全 16 件 ✅ 解消確認済み）

| # | 指摘 ID | 指摘内容 | 修正状態 |
|---|---------|---------|---------|
| 1 | H-01 | CHECK 制約の完全欠落 | ✅ 解消 — inventory, reviews, price_histories に CHECK 制約追加（※ products テーブルに新規問題あり、下記 C-NEW-01） |
| 2 | H-02 | `inventory.reorder_point` カラム欠落 | ✅ 解消 |
| 3 | H-03 | `inventory.status` CHECK 制約未定義 | ✅ 解消 |
| 4 | H-04 | Kafka トピック `inventory.stock_updated` 欠落 | ✅ 解消 — `InventoryStockUpdated` イベント追加 |
| 5 | H-05 | `ProductAttribute` JSONB 採用理由未記載 | ✅ 解消 — GIN インデックス・メタデータ設計を明記 |
| 6 | H-06 | EF Core エンティティ `[Table]`/`[Column]` 未定義 | ✅ 解消 — Product, Inventory, Review, OutboxEvent の完全定義追加 |
| 7 | H-07 | Service/Repository インターフェース未定義 | ✅ 解消 — 5 Service + 3 Repository インターフェース追加 |
| 8 | H-08 | `SELECT FOR UPDATE` 実装例未記載 | ✅ 解消 — §5.4 に完全なトランザクション実装例 |
| 9 | H-09 | エンドポイント認可ポリシー未定義 | ✅ 解消 — 全テーブルに「認可」列追加 |
| 10 | H-10 | 在庫予約 API の IDOR 防止未設計 | ✅ 解消 — gRPC 内部専用に変更、Client Credentials スコープ検証 |
| 11 | H-11 | FK 制約名・ON DELETE/ON UPDATE 未定義 | ✅ 解消 — 全テーブルに `fk_` プレフィックス付き制約名と動作を明示 |
| 12 | H-12 | `product_images.type` CHECK 制約欠落 | ✅ 解消 |
| 13 | H-13 | Correlation ID ミドルウェア未記載 | ✅ 解消 — §12 にインラインミドルウェア追加 |
| 14 | H-14 | コンポーネント図 IMG_REPO レイヤー欠落 | ✅ 解消 |
| 15 | H-15 | テスト CancellationToken 伝搬不足 | ✅ 解消 |
| 16 | H-16 | キャッシュウォームアップ具体設計不足 | ✅ 解消 — `CacheWarmupService` BackgroundService 追加 |

---

## 技術スタック検証結果

| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| ランタイム | .NET 10 | .NET 10 | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| ORM | EF Core 10.* | EF Core 10 | ✅ |
| DB | PostgreSQL (inventorydb) | PostgreSQL (inventorydb) | ✅ |
| キャッシュ | StackExchange.Redis 2.* | StackExchange.Redis 2.* | ✅ |
| メッセージング | Confluent.Kafka 2.* | Confluent.Kafka 2.* | ✅ |
| 認証 | JwtBearer 10.* | JwtBearer 10.* | ✅ |
| レジリエンス | Polly 8.* + Microsoft.Extensions.Http.Resilience 9.* | 同左 | ✅ |
| ロギング | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| OpenTelemetry | 1.* | 1.* | ✅ |
| DB 名 | inventorydb | inventorydb (ADR-0006) | ✅ |
| 金額データ型 | DECIMAL(12,2) | spec.md: DECIMAL(12,2) | ✅ |
| gRPC Saga | ✅ inventory.proto 定義あり | ADR-0009 準拠 | ✅ |
| Outbox パターン | ✅ outbox_events + OutboxPublisher | ADR-0005 準拠 | ✅ |
| エラーレスポンス | RFC 9457 Problem Details | ADR-0007 準拠 | ✅ |

---

## 指摘サマリー

| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ✅ Pass | 0 | 0 | 1 | 0 |
| architect | ⚠️ Conditional | 0 | 1 | 2 | 0 |
| programing-reviewer | ⚠️ Conditional | 0 | 1 | 2 | 0 |
| dba-reviewer | ❌ Rejected | 1 | 2 | 2 | 0 |
| security-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| compliance-reviewer | ✅ Pass | 0 | 0 | 1 | 0 |
| audit-reviewer | ✅ Pass | 0 | 0 | 1 | 0 |
| qa-manager | ✅ Pass | 0 | 0 | 1 | 0 |
| performance-reviewer | ✅ Pass | 0 | 0 | 1 | 0 |
| infra-ops-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| release-manager | ✅ Pass | 0 | 0 | 0 | 1 |
| oss-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| ux-accessibility-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| tech-lead | ⚠️ Conditional | 0 | 0 | 1 | 0 |
| **合計** | | **1** | **4** | **12** | **1** |

## 判定根拠
- **判定ルール適用結果**: Critical 指摘 1 件 ≥ 1 → 自動 Rejected
- **最も重大な指摘**: products テーブルの CHECK 制約が存在しないカラム（`price`, `tax_rate`, `weight`）を参照しており、DDL が実行不能
- **改善状況**: 前回 Critical 7 → 1（86% 削減）、High 20 → 4（80% 削減）。大幅な品質向上が確認される

---

## Critical/High 指摘一覧（修正必須）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| C-NEW-01 | **Critical** | dba-reviewer | DDL 整合性 | **products テーブルの CHECK 制約が存在しないカラムを参照（DDL 実行不能）**: H-01 修正で spec.md の CHECK 制約を追加した際、products テーブルのカラム一覧に存在しない `price`, `tax_rate`, `weight` に対する CHECK 制約が記載されている: `ck_products_price: CHECK (price >= 0)`、`ck_products_tax_rate: CHECK (tax_rate >= 0 AND tax_rate <= 1)`、`ck_products_weight: CHECK (weight > 0)`。これらのカラムは products テーブルの列定義に含まれていない（価格は `prices` テーブルに分離済み）。このまま DDL を適用すると PostgreSQL エラーとなり、EF Core Migrations が失敗する | **選択肢 A（推奨）**: products テーブルに `weight DECIMAL(8,2) NULL CHECK (weight > 0)` カラムを追加し、`ck_products_price`/`ck_products_tax_rate` は削除（価格・税率は prices テーブルで管理）。**選択肢 B**: 3 つの CHECK 制約を全て削除し、prices テーブルの CHECK 制約で代替。いずれの場合も spec.md のデータモデルとの乖離理由を補足に明記 |
| H-NEW-01 | **High** | dba-reviewer, programing-reviewer | DDL-Entity 不整合 | **products SQL DDL に `row_version` カラム未定義**: EF Core Product エンティティ（§5.3）に `[Timestamp] [Column("row_version")] public byte[] RowVersion` が定義されているが、products テーブルの SQL DDL カラム一覧に `row_version` が含まれていない。EF Core Migrations 自動生成時にカラムが追加されるが、設計書の DDL と実際のスキーマに乖離が生じる | products テーブルの DDL カラム一覧に `row_version BYTEA` を追加。AGENTS.md §10.3 の楽観的ロック設計パターンに準拠 |
| H-NEW-02 | **High** | dba-reviewer | CHECK 制約 | **`price_histories.price_type` の CHECK 制約欠落**: `price_type VARCHAR(50) NOT NULL` だが有効値の CHECK 制約がない。同ドキュメント内の他の状態・種別カラム（`inventory.status`, `reviews.status`, `product_images.type`, `outbox_events.status`）は全て CHECK 制約で列挙値を限定しており、設計方針が不統一 | `ck_price_histories_price_type: CHECK (price_type IN ('REGULAR', 'SALE', 'PROMOTION'))` を追加 |
| H-NEW-03 | **High** | architect | Outbox 設計 | **`outbox_events` テーブルに `aggregate_type` カラム欠落**: check-report-1.md の C-04 推奨対応に `aggregate_type` が含まれており、ADR-0005 でも `AggregateId` をキーにイベントの重複排除が可能と記載されている。`aggregate_type` なしでは同一 `aggregate_id` が異なるエンティティ型（Product vs Inventory）で衝突する可能性がある | outbox_events テーブルに `aggregate_type VARCHAR(100) NOT NULL` カラムを追加。EF Core OutboxEvent エンティティにも `[Column("aggregate_type")] [Required] [MaxLength(100)] public string AggregateType` を追加 |
| H-NEW-04 | **High** | architect | イベント設計 | **OutboxPublisher の EventType→Kafka トピック名マッピング不整合**: §5.5 の `OutboxPublisher` コードが `await producer.ProduceAsync(evt.EventType, ...)` で `EventType` をそのまま Kafka トピック名として使用しているが、EventType 値（例: `"InventoryReserved"`）と Kafka トピック名（例: `"inventory.reservations"`）が一致しない。§6 の発行イベント一覧では EventType とトピック名は別々に定義されている | **選択肢 A（推奨）**: outbox_events テーブルに `topic VARCHAR(255) NOT NULL` カラムを追加し、`OutboxPublisher` で `evt.Topic` を使用。**選択肢 B**: OutboxPublisher 内に EventType→Topic のマッピング辞書を定義 |

---

## Medium 指摘一覧（改善推奨）

### 新規指摘（修正に起因する問題）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| M-NEW-01 | Medium | programing-reviewer | EF Core | **§5.3 EF Core エンティティ定義が不完全**: Product, Inventory, Review, OutboxEvent の 4 エンティティのみ定義されているが、Price, Category, ProductImage, SizeGuide, ReviewResponse, PriceHistory, Supplier, ProductSupplier の定義が欠落。実装時にカラムマッピング（`[Table]`/`[Column]` 属性）が不明となる | 最低限 Price, Category, SizeGuide, ReviewResponse エンティティの定義を追加。他はコード例として「同パターンで実装」と明記 |
| M-NEW-02 | Medium | architect | インターフェース | **§5.2 に ISizeGuideService / ISizeGuideRepository インターフェース未定義**: §3 クラス構造に `ISizeGuideService` / `SizeGuideService` が記載されているが、§5.2 のインターフェース定義セクションに含まれていない | ISizeGuideService（GetByCategoryIdAsync, CreateAsync, UpdateAsync）のインターフェース定義を追加 |
| M-NEW-03 | Medium | audit-reviewer | 監査カラム | **products/categories テーブルに `created_by`/`updated_by` 監査カラム欠落**: inventory, prices, product_images, suppliers テーブルには `created_by`/`updated_by` が定義されているが、products/categories テーブルには存在しない。`sql-schema-review.instructions.md` §2 で全テーブルへの監査カラム設置を推奨 | products, categories テーブルに `created_by VARCHAR(255) NULL`, `updated_by VARCHAR(255) NULL` を追加 |
| M-NEW-04 | Medium | architect | BackgroundService | **InventoryReservationCleanupService の単一 `reserved_at` タイムスタンプによる多重予約管理の限界**: 現設計では 1 つの inventory レコードに対して 1 つの `reserved_at` タイムスタンプで管理しているが、複数の注文が同一商品を同時に予約した場合、最後の予約時刻のみが記録される。15 分タイムアウト判定時に先行予約のタイムアウトが正しく評価されない可能性がある | 設計書に「複数注文の同時予約時は最新の `reserved_at` が適用される。Saga タイムアウト（30 秒）と CleanupService タイムアウト（15 分）の間に十分な差があるため、実運用上の影響は限定的」等の設計判断を明記。または `inventory_reservations` 中間テーブルでの個別管理を検討 |
| M-NEW-05 | Medium | architect | 設計整合性 | **OrderCreatedConsumer と gRPC ReserveInventory の二重予約パス**: §5.5 の `OrderCreatedConsumer` が Kafka `order.created` イベントで `inventoryService.ReserveAsync()` を呼び出す一方、§5.1 の gRPC `ReserveInventory` も同じ `inventoryService.ReserveAsync()` を呼び出す。どちらが正規の予約パスか不明確。H-10 の修正で「在庫予約は gRPC 内部専用」と定義したにもかかわらず Kafka Consumer でも予約が行われる | 設計書内で gRPC と Kafka Consumer の役割分担を明確化。例: 「gRPC ReserveInventory は Saga オーケストレーターからの同期的な在庫引当に使用。OrderCreatedConsumer は Saga を使用しない非同期注文フロー（将来拡張）向けであり、現行設計では gRPC パスのみが有効」等 |
| M-NEW-06 | Medium | qa-manager | テスト | **テストコード例で Service の DI 引数不完全**: §13 の `ProductService` コンストラクタが `new ProductService(_productRepo, _categoryRepo, _logger)` だが、§5.2 の `IProductService` では 5 メソッドが定義されており、実装には EventPublisherService 等の追加依存が予想される。テストコードが実装時のコンストラクタと乖離する可能性 | テストコード例のコンストラクタ引数を Service 実装に合わせて更新するか、「最小構成の例示」であることを注記 |

### 前回からの残存指摘（Medium）

| # | 指摘 ID | 指摘内容 | 残存理由 |
|---|---------|---------|---------|
| 7 | M-01 | `prices` テーブル名が DB 予約語に近い → `product_prices` 検討 | 設計判断必要（影響範囲広い） |
| 8 | M-04 | リクエスト DTO のバリデーション属性未定義 | 実装フェーズで対応可能 |
| 9 | M-05 | API パスプレフィックス `/api` の統一 | 全サービス横断の設計判断必要 |
| 10 | M-07 | EF Core PostgreSQL 全文検索クエリパターン未記載 | 実装フェーズで対応可能 |
| 11 | M-08 | Suppliers PII 暗号化方針未記載 | 全サービス横断のセキュリティ設計判断必要 |
| 12 | M-09 | 在庫分析エンジン具体設計未記載（Q3 計画） | 将来拡張計画の範囲 |

---

## Low 指摘一覧（推奨改善）

| # | 重要度 | 出典 Agent | 指摘内容 |
|---|--------|-----------|----------|
| L-02 | Low | release-manager | CI/CD パイプラインのプロジェクトパス前提が未記載（前回残存） |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-01 | 高優先 | dba-reviewer | products テーブルの `weight`（商品重量）を products に持たせるか attributes JSONB に含めるかの設計判断。spec.md では Product に `weight` はないが、配送料計算（重量 10kg 超で追加料金）に使用される。専用カラムにすれば CHECK 制約・インデックスが利用可能、JSONB なら柔軟性を維持 | テックリード |
| E-02 | 通常 | architect | `inventory_reservations` 中間テーブルによる個別予約管理の導入要否。現設計の単一 `reserved_at` で運用上問題ないか、それとも注文別の予約追跡が必要か | テックリード |
| E-03 | 通常 | business-analyst | 在庫分析エンジンの MVP スコープ判断（前回 E-03 残存） | プロダクトオーナー |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| — | — | — | 競合なし | — | — |

---

## ドキュメント横断分析

### マイクロサービス設計書カバレッジ（inventory-management-design.md v2.0）

| 設計領域 | 記載状況 | 前回評価 | 今回評価 |
|---------|---------|---------|---------|
| API 定義 | 商品・カテゴリ・在庫・価格・レビュー・サイズガイドの REST API + 認可ポリシー明示 | ⚠️ | ✅ |
| gRPC 統合 | inventory.proto, InventoryGrpcService, Deadline 500ms, スコープ認証 | ❌ | ✅ |
| DB 設計 | 12 テーブル定義（products〜outbox_events）、FK/CHECK 制約、インデックス | ❌ | ⚠️ DDL 整合性に 1 件 Critical |
| EF Core エンティティ | Product, Inventory, Review, OutboxEvent の完全定義 | ❌ | ⚠️ 主要 4 エンティティのみ |
| Service/Repository | 5 Service + 3 Repository インターフェース定義 | ❌ | ⚠️ ISizeGuideService 欠落 |
| Outbox パターン | テーブル定義 + OutboxPublisher BackgroundService + Advisory Lock | ❌ | ⚠️ aggregate_type/topic 欠落 |
| BackgroundService | OutboxPublisher, CleanupService, OrderCreatedConsumer, CacheWarmupService | ❌ | ✅ |
| イベント設計 | 発行 11 種 + 購読 5 種 + スキーマ例 | ⚠️ | ✅ |
| セキュリティ | JWT, RBAC, エンドポイント認可, IDOR 防止, gRPC スコープ, 画像セキュリティ | ⚠️ | ✅ |
| 可観測性 | OpenTelemetry, Serilog, Correlation ID, ヘルスチェック（PostgreSQL+Redis+Kafka） | ⚠️ | ✅ |
| ミドルウェア順序 | AGENTS.md §11.3 準拠のパイプライン順序 | ❌ | ✅ |
| テスト戦略 | AAA パターン、CancellationToken、カバレッジ目標 | ⚠️ | ✅ |
| デプロイ | マルチステージ Dockerfile、非 root、HEALTHCHECK、CI/CD | ✅ | ✅ |

### サービス間整合性（前回比較）

| 連携先サービス | 連携方式 | 前回 | 今回 |
|-------------|---------|------|------|
| SalesManagementService（Saga ステップ 2） | gRPC（ReserveInventory / ReleaseReservation） | ❌ | ✅ |
| SalesManagementService（イベント） | Kafka（OrderCreated, OrderCompleted, OrderCancelled） | ⚠️ | ✅ Consumer 設計あり |
| UserManagementService（ウィッシュリスト通知） | Kafka（inventory.stock_updated） | ❌ | ✅ |
| PaymentCartService | Kafka（在庫変更通知） | ⚠️ | ✅ Outbox パターン経由 |
| AiSupportService | Kafka（商品情報連携） | ⚠️ | ✅ |

### 前回→今回の改善サマリ

| 指標 | check-report-1 | check-report-2 | 変化 |
|------|---------------|---------------|------|
| Critical | 7 | 1 | -86% |
| High | 20 | 4 | -80% |
| Medium | 15 | 12 | -20% |
| Low | 4 | 1 | -75% |
| 合計 | 46 | 18 | -61% |
| 設計書行数 | 1,194 | 2,133 | +79% |
| 新規セクション | — | 5（§5.1〜§5.5） | — |
| 新規テーブル | — | 5 | — |
| 新規 API | — | 9 | — |

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### 判定: ✅ Pass

**ビジネス要件の充足度評価（前回比較）**:

**前回の不足事項 → 今回の解消状況**:
- ✅ 商品レビュー機能: reviews/review_responses テーブル、6 API エンドポイント追加
- ✅ サイズガイド機能: size_guides テーブル、3 API エンドポイント追加
- ✅ ウィッシュリスト在庫復活通知: `inventory.stock_updated` イベント追加

**残存する改善提案**:
- **Medium**: 在庫分析エンジンの具体設計は Q3 将来拡張計画の範囲。MVP としての判断はプロダクトオーナーに委任（前回 E-03 残存）

</details>

<details>
<summary>architect レビューレポート</summary>

### 判定: ⚠️ Conditional

**前回の Critical/High → 解消確認**:
- ✅ gRPC Saga 統合: §5.1 で完全実装。proto 定義、サービス実装、Deadline、スコープ認証
- ✅ Outbox パターン: §5.5 OutboxPublisher + outbox_events テーブル
- ✅ Kafka トピック整合性: `inventory.stock_updated` 追加
- ✅ コンポーネント図: IMG_REPO レイヤー追加

**新規指摘**:
- **H-NEW-03**: outbox_events に `aggregate_type` カラム欠落（異なるエンティティ型の aggregate_id 衝突リスク）
- **H-NEW-04**: OutboxPublisher の EventType→Kafka トピック名マッピング不整合
- **M-NEW-02**: ISizeGuideService インターフェース未定義
- **M-NEW-04**: InventoryReservationCleanupService の単一 timestamp 設計限界
- **M-NEW-05**: OrderCreatedConsumer と gRPC の二重予約パス

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 判定: ⚠️ Conditional

**前回の High → 解消確認**:
- ✅ EF Core エンティティ: `[Table]`/`[Column]` 属性、ナビゲーション `= []` 初期化、`[Timestamp]` 楽観的ロック
- ✅ Service/Repository インターフェース: 全メソッドに `CancellationToken ct = default`
- ✅ SELECT FOR UPDATE: §5.4 に完全なトランザクション実装例
- ✅ primary constructor, record 型, switch 式の適切な活用

**良好な点**:
- gRPC サービス実装が AGENTS.md §10.6 のパターンに準拠
- OutboxPublisher の `TimeProvider` DI が AGENTS.md のテスタビリティ要件に適合
- 例外階層（InventoryException → ResourceNotFoundException / InsufficientStockException）が適切

**新規指摘**:
- **H-NEW-01**: products SQL DDL に `row_version` カラム未定義（EF Core エンティティとの不整合）
- **M-NEW-01**: §5.3 EF Core エンティティが 4 エンティティのみ（残り 8 エンティティ未定義）
- **M-NEW-06**: テストコードの Service コンストラクタ引数が実装と乖離する可能性

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 判定: ❌ Rejected

**前回の Critical/High → 解消確認**:
- ✅ TIMESTAMP WITH TIME ZONE: 全テーブル修正済み
- ✅ reviews/review_responses/size_guides/outbox_events: テーブル追加済み
- ✅ CHECK 制約: inventory, reviews, product_images, outbox_events に追加（※ products に新規問題）
- ✅ FK 制約名: `fk_` プレフィックス、ON DELETE/ON UPDATE 明示
- ✅ DECIMAL(12,2): 全金額カラム統一済み
- ✅ reorder_point カラム追加

**SQL DDL 品質チェック結果**:

| チェック項目 | 結果 | 備考 |
|------------|------|------|
| snake_case テーブル名（複数形） | ✅ | products, categories, inventory, prices, etc. |
| snake_case カラム名 | ✅ | created_at, product_id, etc. |
| TIMESTAMP WITH TIME ZONE | ✅ | 全日時カラム |
| DECIMAL(12,2) 金額カラム | ✅ | regular_price, sale_price, supplier_price, price |
| CHECK 制約（UPPER_CASE 値） | ✅ | 'IN_STOCK', 'PENDING', 'PUBLISHED' etc. |
| 監査カラム (created_at, updated_at) | ✅ | 全テーブル |
| created_by/updated_by | ⚠️ | products, categories テーブルに欠落 |
| FK 制約名 (fk_ プレフィックス) | ✅ | 全テーブル |
| CHECK 制約名 (ck_ プレフィックス) | ✅ | 全テーブル |
| インデックス (idx_ プレフィックス) | ✅ | 全テーブル |
| カラム存在整合性 | ❌ | products テーブル CHECK 制約が未定義カラムを参照 |
| DDL-Entity 整合性 | ❌ | row_version カラムが DDL に欠落 |

**Critical 指摘**:
- **C-NEW-01**: products テーブルの CHECK 制約が存在しないカラム（price, tax_rate, weight）を参照

**High 指摘**:
- **H-NEW-01**: products DDL に row_version カラム未定義
- **H-NEW-02**: price_histories.price_type の CHECK 制約欠落

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 判定: ✅ Pass

**前回の High → 解消確認**:
- ✅ エンドポイント認可ポリシー: 全テーブルに「認可」列追加（AllowAnonymous / RequireAuthorization / AdminOnly）
- ✅ IDOR 防止: 在庫予約を gRPC 内部専用に変更、Client Credentials スコープ検証
- ✅ Kestrel AddServerHeader: false
- ✅ セキュリティヘッダー（AGENTS.md §5.3 準拠のミドルウェア設定）

**セキュリティチェック結果**:
- SQL インジェクション防止: EF Core LINQ + FromSqlInterpolated ✅
- 認証/認可: JWT + RBAC + Fallback Policy ✅
- 秘密情報管理: 環境変数参照（appsettings に直接記述なし）✅
- レート制限: UseRateLimiter() ✅
- DetailedErrors: false ✅

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 判定: ✅ Pass

**残存指摘**:
- **M-08（残存）**: Suppliers テーブル PII（email, phone, address）の暗号化方針が未記載。全サービス横断のセキュリティ設計判断として残存

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 判定: ✅ Pass

**前回の High/Medium → 解消確認**:
- ✅ Correlation ID: §12 にミドルウェア実装（X-Correlation-Id + LogContext.PushProperty）
- ✅ PriceHistory.changed_by: 監査証跡カラム追加済み
- ✅ Serilog: ServiceName Enricher + CompactJsonFormatter

**新規指摘**:
- **M-NEW-03**: products/categories テーブルに `created_by`/`updated_by` 監査カラム欠落（他テーブルとの不統一）

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 判定: ✅ Pass

**前回の High/Medium → 解消確認**:
- ✅ CancellationToken 伝搬: テストコードで `CancellationToken.None` 使用
- ✅ テストクラス名: `ProductServiceTests`（複数形）
- ✅ AAA パターン: Arrange / Act / Assert コメント付き
- ✅ Shouldly アサーション: `ShouldNotBeNull()`, `ShouldBe()` 使用

**新規指摘**:
- **M-NEW-06**: テスト例の Service コンストラクタ引数が実装と乖離する可能性

**残存指摘**:
- M-13（残存）: 並行テスト（楽観的ロック競合）シナリオ — 実装フェーズで対応可能

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 判定: ✅ Pass

**前回の High → 解消確認**:
- ✅ CacheWarmupService: BackgroundService による上位 100 商品 + 全カテゴリの事前キャッシュ投入

**良好な点**:
- Redis キャッシュ TTL 設定が実用的（商品 30 分、カテゴリ 1 時間、在庫 5 分）
- PostgreSQL インデックス戦略 — GIN 全文検索、部分インデックス（reserved_at, outbox_events.status）
- Polly レジリエンスハンドラー（リトライ 3 回、サーキットブレーカー 10 秒）
- AsNoTracking の適切な使用
- OutboxPublisher の動的バックオフ（100ms〜5s）

**残存指摘**:
- M-11（残存）: prices テーブルの部分インデックス最適化 — パフォーマンスチューニングフェーズで対応

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 判定: ✅ Pass

**前回の Medium → 解消確認**:
- ✅ Kafka ヘルスチェック: `AddKafka(kafkaConfig, name: "kafka", tags: ["ready"])` 追加

**良好な点**:
- マルチステージ Dockerfile（ビルド + ランタイム分離）
- 非 root ユーザー実行（`skishop` ユーザー）
- HEALTHCHECK 設定（/health エンドポイント）
- ヘルスチェック — Liveness（/health）+ Readiness（/health/ready with PostgreSQL, Redis, Kafka）
- .NET Aspire AppHost でのローカル開発セットアップ手順

</details>

<details>
<summary>release-manager レビューレポート</summary>

### 判定: ✅ Pass

**残存指摘**:
- **L-02（残存）**: CI/CD パイプラインのプロジェクトパス前提の明記 — 影響軽微

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 判定: ✅ Pass

全 NuGet パッケージが GA バージョン。禁止パッケージの使用なし。
`Azure.Storage.Blobs 12.*` と `Azure.Identity 1.*` はサービス固有の追加として妥当。
Grpc.AspNetCore パッケージの追加が必要だが、§5.1 の gRPC 設計から暗示される。

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 判定: ✅ Pass

バックエンド設計書のため直接的なレビュー対象はない。商品画像の `alt_text` カラムはアクセシビリティに貢献する。

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 判定: ⚠️ Conditional

**総合評価**: 前回の致命的欠落（gRPC、Outbox、BackgroundService、Review/SizeGuide）が全て解消され、設計書の完成度は**大幅に向上**した。2,133 行の充実した設計書となり、実装可能性は高い。

ただし、H-01 修正時に導入された products テーブル CHECK 制約の不整合（C-NEW-01）が DDL 実行を阻害するため、Critical として是正が必要。

**前回の最重要課題 → 今回の状況**:
1. ✅ gRPC Saga 統合: 完全解消。proto 定義、gRPC Deadline、スコープ認証まで記載
2. ✅ Outbox パターン: 完全解消。Advisory Lock、動的バックオフ、部分インデックス
3. ✅ BackgroundService: 4 サービス（OutboxPublisher, CleanupService, OrderCreatedConsumer, CacheWarmupService）全て設計完了

**残る構造的課題（Medium）**:
- OutboxPublisher の EventType→Topic マッピングの明確化が必要
- gRPC と Kafka Consumer の二重予約パスの設計意図の明確化が必要

</details>

---

## 修正推奨の優先度

| 優先度 | 対応内容 | 工数（目安） |
|--------|---------|------------|
| 🔴 最優先 | C-NEW-01: products CHECK 制約の不整合修正（DDL 実行不能） | 小 |
| 🟠 高優先 | H-NEW-01: products DDL に row_version カラム追加 | 小 |
| 🟠 高優先 | H-NEW-02: price_histories.price_type CHECK 制約追加 | 小 |
| 🟠 高優先 | H-NEW-03: outbox_events に aggregate_type カラム追加 | 小 |
| 🟠 高優先 | H-NEW-04: OutboxPublisher の topic マッピング修正 | 中 |
| 🟡 中優先 | M-NEW-01〜06: EF Core エンティティ補完、インターフェース追加等 | 中 |
