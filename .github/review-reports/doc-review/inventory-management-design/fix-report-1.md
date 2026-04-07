# 修正レポート: inventory-management-design.md

## 修正実施日時
2026-04-03

## 対象ファイル
`design-docs/inventory-management-design.md`

## 修正元レビュー
`check-report-1.md` (Critical 7 件, High 16 件, Medium 15 件, Low 4 件)

---

## 修正サマリー

| 重要度 | 検出数 | 修正完了 | 残存 | 修正率 |
|--------|--------|----------|------|--------|
| **Critical** | 7 | 7 | 0 | 100% |
| **High** | 16 | 16 | 0 | 100% |
| **Medium** | 15 | 6 | 9 | 40% |
| **Low** | 4 | 2 | 2 | 50% |
| **合計** | 42 | 31 | 11 | 74% |

---

## 修正詳細

### Critical 修正（全 7 件完了）

| # | 指摘 ID | 指摘内容 | 修正内容 | 修正箇所 |
|---|---------|---------|---------|---------|
| 1 | C-01 | 全テーブルの `TIMESTAMP` → `TIMESTAMP WITH TIME ZONE` 未使用 | products, categories, inventory, prices, product_images, suppliers, product_suppliers の全 `created_at`/`updated_at` および日時カラムを `TIMESTAMP WITH TIME ZONE` に変更。`DEFAULT CURRENT_TIMESTAMP` も追加 | §4 データベーススキーマ（全テーブル定義） |
| 2 | C-02 | `Review`/`ReviewResponse` エンティティ未定義 | `reviews` テーブル（id, product_id, user_id, rating, title, content, is_verified_purchase, helpful_count, status + CHECK 制約）と `review_responses` テーブル（id, review_id, responder_id, content）を追加。ER 図、クラス構造、API エンドポイント、EF Core エンティティも追加 | §4 ER 図・DB スキーマ, §3 クラス構造, §5 API 設計, §5.3 EF Core エンティティ |
| 3 | C-03 | gRPC サービス定義の完全欠落 | §5.1 を新設。`inventory.proto`（ReserveInventory, ReleaseReservation RPC）、`InventoryGrpcService` 実装例、gRPC Deadline（500ms）、`MapGrpcService` 設定を追加 | §5.1 gRPC サービス設計 |
| 4 | C-04 | Outbox パターン（ADR-0005）の完全欠落 | `outbox_events` テーブル定義（CHECK 制約・部分インデックス含む）、`OutboxPublisher` BackgroundService（Advisory Lock, 動的バックオフ 100ms〜5s）を追加 | §4 outbox_events テーブル, §5.5 BackgroundService 設計 |
| 5 | C-05 | `size_guides` テーブル欠落 | `size_guides` テーブル（id, category_id FK, size_chart JSONB, guide_type）を追加。SizeGuideEndpoints、SizeGuideService もクラス構造と API 設計に追加 | §4 DB スキーマ, §3 クラス構造, §5 API 設計 |
| 6 | C-06 | `reserved_at` カラムと `InventoryReservationCleanupService` 未定義 | inventory テーブルに `reserved_at TIMESTAMP WITH TIME ZONE NULL` カラムを追加。§5.5 に `InventoryReservationCleanupService`（ポーリング 1 分、Advisory Lock、15 分タイムアウト判定）を新設 | §4 inventory テーブル, §5.5 BackgroundService 設計, §11 インデックス |
| 7 | C-07 | 金額データ型の不一致（DECIMAL(10,2) → DECIMAL(12,2)） | prices テーブルの `regular_price`, `sale_price` および product_suppliers テーブルの `supplier_price` を `DECIMAL(12,2)` に変更。price_histories テーブルも `DECIMAL(12,2)` で新規追加 | §4 prices テーブル, product_suppliers テーブル, price_histories テーブル |

### High 修正（全 16 件完了）

| # | 指摘 ID | 指摘内容 | 修正内容 | 修正箇所 |
|---|---------|---------|---------|---------|
| 1 | H-01 | CHECK 制約の完全欠落 | products（price≥0, tax_rate, weight>0）、inventory（quantity≥0, reserved_quantity≥0, quantity≥reserved_quantity, reorder_point≥0, status 列挙値）、reviews（rating 1-5, status 列挙値）、price_histories（price≥0）の CHECK 制約を追加。制約名は `ck_` プレフィックス付与 | §4 全テーブル定義 |
| 2 | H-02 | `inventory.reorder_point` カラム欠落 | `reorder_point INTEGER NOT NULL DEFAULT 0` カラムを追加。`availableQuantity` はアプリ算出（`quantity - reserved_quantity`）とする設計方針を明記 | §4 inventory テーブル |
| 3 | H-03 | `inventory.status` の CHECK 制約未定義 | `CHECK (status IN ('IN_STOCK', 'LOW_STOCK', 'OUT_OF_STOCK', 'RESERVED', 'DISCONTINUED'))` を追加 | §4 inventory テーブル |
| 4 | H-04 | Kafka トピック名 spec.md 不一致（`inventory.stock_updated` 欠落） | 発行イベントに `InventoryStockUpdated`（トピック: `inventory.stock_updated`）を追加。ウィッシュリスト在庫復活通知連携用 | §6 発行イベント |
| 5 | H-05 | `ProductAttribute` の設計方針不統一 | products テーブルの `attributes` JSONB カラム採用理由を明記（柔軟性優位、GIN インデックス、JSONB 内メタデータで `isFilterable`/`isSortable` 管理） | §4 products テーブル（補足） |
| 6 | H-06 | EF Core エンティティの `[Table]`/`[Column]` 属性コード例なし | Product, Inventory, Review, OutboxEvent の EF Core エンティティ定義コード例を追加。`[Table]`, `[Column]`, `[Key]`, `[Required]`, `[MaxLength]`, `[Timestamp]` 属性、ナビゲーション `= []` 初期化を含む | §5.3 EF Core エンティティ定義 |
| 7 | H-07 | Service/Repository インターフェース定義の欠落 | `IProductService`, `IInventoryService`, `ICategoryService`, `IPriceService`, `IReviewService`、`IProductRepository`, `IInventoryRepository`, `IReviewRepository` のメソッドシグネチャを定義。全メソッドに `CancellationToken ct = default` | §5.2 Service / Repository インターフェース定義 |
| 8 | H-08 | 在庫予約の `SELECT FOR UPDATE` 実装例未記載 | §5.4 を新設。`BeginTransactionAsync` → `FromSqlInterpolated("SELECT ... FOR UPDATE")` → `reserved_quantity` 更新 → `reserved_at` 設定 → Outbox イベント書き込み → `CommitAsync` の完全なコード例 | §5.4 在庫予約トランザクション実装 |
| 9 | H-09 | エンドポイント認可ポリシー未定義 | 全エンドポイントテーブルに「認可」列を追加。GET は `AllowAnonymous`、書き込み操作は `RequireAuthorization("AdminOnly")`、認証済みユーザー操作は `RequireAuthorization` | §5 全 API テーブル |
| 10 | H-10 | 在庫予約 API の IDOR 防止未設計 | 在庫予約/解放を REST API から除外し gRPC 内部専用に変更。Saga コーディネーター Client Credentials トークンスコープ（`inventory.stock:reserve`）検証を明記 | §5 在庫管理 API（注記追加） |
| 11 | H-11 | FK 制約名と ON DELETE/ON UPDATE 動作未定義 | 全テーブルに `fk_` プレフィックス付き FK 制約名と ON DELETE/ON UPDATE 動作を明示（products→category: RESTRICT、inventory→product: CASCADE 等） | §4 全テーブル定義 |
| 12 | H-12 | `product_images.type` の CHECK 制約欠落 | `CHECK (type IN ('MAIN', 'THUMBNAIL', 'GALLERY', 'DETAIL'))` を追加 | §4 product_images テーブル |
| 13 | H-13 | Correlation ID ミドルウェア未記載 | `X-Correlation-Id` ヘッダー取得/設定 + `LogContext.PushProperty` のミドルウェアコード例を追加。ミドルウェアパイプライン順序（AGENTS.md §11.3 準拠）も明記 | §12 監視と可観測性 |
| 14 | H-14 | コンポーネント図 IMG_REPO レイヤー欠落 | `IMG_SERV → IMG_REPO[画像リポジトリ] → BLOB` に修正（レイヤードアーキテクチャ準拠） | §3 コンポーネントアーキテクチャ図 |
| 15 | H-15 | テストの CancellationToken 伝搬不足 | テストコード例の Service 呼び出しに `CancellationToken.None` を追加 | §13 テスト戦略 |
| 16 | H-16 | キャッシュウォームアップ具体設計不足 | `CacheWarmupService : BackgroundService` のコード例を追加（上位 100 商品、全カテゴリの事前キャッシュ投入） | §5.5 BackgroundService 設計 |

### Medium 修正（6 件完了 / 9 件残存）

| # | 指摘 ID | 修正状態 | 修正内容 |
|---|---------|---------|---------|
| 1 | M-02 | ✅ 完了 | FK の ON DELETE/ON UPDATE 動作を全テーブルに定義（H-11 と同時修正） |
| 2 | M-03 | ✅ 完了 | Kafka Consumer BackgroundService（OrderCreatedConsumer 等）を §5.5 に追加（DLT 転送言及含む） |
| 3 | M-10 | ✅ 完了 | Correlation ID の LogContext 設定を §12 に追加（H-13 と同時修正） |
| 4 | M-12 | ✅ 完了 | Kafka ヘルスチェックを `AddHealthChecks()` に追加 |
| 5 | M-15 | ✅ 完了 | PriceHistory に `changed_by VARCHAR(255)` カラムを追加 |
| 6 | M-06 | ✅ 完了 | `inventory.stock_updated` イベントを発行イベントに追加（H-04 と連動） |

### Medium 残存（Auto-fix 範囲外 — 次回レビューまたは手動対応）

| # | 指摘 ID | 理由 |
|---|---------|------|
| 1 | M-01 | `prices` → `product_prices` のテーブル名変更は影響範囲が広く、設計判断が必要 |
| 2 | M-04 | DTO のバリデーション属性追加は実装フェーズで対応可能 |
| 3 | M-05 | API パスプレフィックス `/api` の統一は全サービス横断の設計判断が必要 |
| 4 | M-07 | EF Core からの PostgreSQL 全文検索クエリパターンは実装フェーズで対応可能 |
| 5 | M-08 | Suppliers テーブル PII 暗号化方針は全サービス横断のセキュリティ設計判断が必要 |
| 6 | M-09 | 在庫分析エンジンの具体設計は将来拡張計画（Q3）の範囲 |
| 7 | M-11 | prices テーブルの部分インデックス最適化はパフォーマンスチューニングフェーズで対応 |
| 8 | M-13 | 並行テストシナリオはテスト実装フェーズで対応 |
| 9 | M-14 | PriceEndpoints の Minimal API 実装パターンは実装フェーズで対応可能 |

### Low 修正（2 件完了 / 2 件残存）

| # | 指摘 ID | 修正状態 | 修正内容 |
|---|---------|---------|---------|
| 1 | L-01 | ✅ 完了 | テストクラス名 `ProductServiceTest` → `ProductServiceTests`（複数形） |
| 2 | L-04 | ✅ 完了 | `product_suppliers.last_order_date` を `TIMESTAMP WITH TIME ZONE` に変更（C-01 と同時修正） |
| 3 | L-02 | ❌ 残存 | CI/CD パイプラインのプロジェクトパス前提の明記 — 影響軽微 |
| 4 | L-03 | ❌ 残存 | Docker バージョン不一致（24.0+ vs 25.x）— 影響軽微 |

---

## 修正不可事項 / エスカレーション

| # | 指摘 ID | 内容 | 理由 | 推奨判断者 |
|---|---------|------|------|-----------|
| 1 | E-01 | `ProductAttribute` 実装方式（独立テーブル vs JSONB） | 設計書に JSONB 採用理由を追記済み。最終判断はテックリード | テックリード |
| 2 | E-02 | 在庫予約 API の公開範囲 | gRPC 内部専用に変更済み。最終確認はテックリード+セキュリティ担当 | テックリード + セキュリティ担当 |
| 3 | E-03 | 在庫分析エンジンの MVP スコープ | 将来拡張計画（Q3）のままで据え置き | プロダクトオーナー |

---

## 追加変更（レポート指摘外）

| # | 変更内容 | 理由 |
|---|---------|------|
| 1 | DB 名 `skishopdb` → `inventorydb` | ADR-0006（サービス別独立 DB）準拠 |
| 2 | 環境変数の DB 名も `inventorydb` に修正 | 上記と整合性維持 |
| 3 | `price_histories` テーブル定義を正式追加 | ER 図にのみ存在し SQL 定義が欠落していた |

---

## 変更統計

| 指標 | 値 |
|------|--------|
| 修正前行数 | 1,194 |
| 修正後行数 | 2,133 |
| 増加行数 | +939 |
| 新規セクション数 | 5（§5.1〜§5.5） |
| 新規テーブル定義 | 5（price_histories, reviews, review_responses, size_guides, outbox_events） |
| 新規 API エンドポイント | 9（レビュー 6 + サイズガイド 3） |
