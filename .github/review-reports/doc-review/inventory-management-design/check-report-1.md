# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/inventory-management-design.md`
- **判定**: ❌ **Rejected** — Critical 指摘 5 件検出。是正完了後に再レビュー必須
- **レビュー日時**: 2026-04-03 （orchest-doc-review 実行）
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

## 技術スタック検証結果
| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| ランタイム | .NET 10 | .NET 10 | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| ORM | EF Core 10.* | EF Core 10 | ✅ |
| DB | PostgreSQL | PostgreSQL | ✅ |
| キャッシュ | StackExchange.Redis 2.* | StackExchange.Redis 2.* | ✅ |
| メッセージング | Confluent.Kafka 2.* | Confluent.Kafka 2.* | ✅ |
| 認証 | JwtBearer 10.* | JwtBearer 10.* | ✅ |
| レジリエンス | Polly 8.* | Polly 8.* | ✅ |
| ロギング | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| OpenTelemetry | 1.* | 1.* | ✅ |
| Blob Storage | Azure.Storage.Blobs 12.* | 記載なし（サービス固有） | ⚠️ nuget-dependency.instructions.md の「必須パッケージ」に含まれないが、サービス固有の追加として許容 |
| 金額データ型 | DECIMAL(10,2) | spec.md: DECIMAL(12,2) | ❌ 不一致 |

## 指摘サマリー
| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ⚠️ Conditional | 1 | 2 | 1 | 0 |
| architect | ❌ Rejected | 2 | 3 | 2 | 0 |
| programing-reviewer | ⚠️ Conditional | 0 | 3 | 2 | 1 |
| dba-reviewer | ❌ Rejected | 3 | 5 | 3 | 1 |
| security-reviewer | ⚠️ Conditional | 0 | 2 | 1 | 0 |
| compliance-reviewer | ✅ Pass | 0 | 0 | 1 | 0 |
| audit-reviewer | ✅ Pass | 0 | 1 | 1 | 0 |
| qa-manager | ✅ Pass | 0 | 1 | 1 | 0 |
| performance-reviewer | ✅ Pass | 0 | 1 | 1 | 0 |
| infra-ops-reviewer | ✅ Pass | 0 | 0 | 1 | 1 |
| release-manager | ✅ Pass | 0 | 0 | 0 | 1 |
| oss-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| ux-accessibility-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| tech-lead | ❌ Rejected | 1 | 2 | 1 | 0 |
| **合計** | | **7** | **20** | **15** | **4** |

## 判定根拠
- **判定ルール適用結果**: Critical 指摘 7 件 ≥ 1 → 自動 Rejected
- **最も重大な指摘**: spec.md で定義された `Review`/`ReviewResponse` エンティティ、`size_guides` テーブル、Outbox パターン実装、gRPC Saga 統合が設計書から完全に欠落しており、実装が不可能

---

## Critical/High 指摘一覧（修正必須）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| C-01 | **Critical** | dba-reviewer | データモデル | **`TIMESTAMP WITH TIME ZONE` 未使用**: 全テーブル（products, categories, inventory, prices, product_images, suppliers, product_suppliers）の `created_at`/`updated_at` が `TIMESTAMP` と記載されている。`sql-schema-review.instructions.md` §2 および AGENTS.md §10.3 では `TIMESTAMP WITH TIME ZONE` を必須としている。タイムゾーン非対応はマルチリージョン運用時のデータ不整合に直結する | 全 `TIMESTAMP` カラムを `TIMESTAMP WITH TIME ZONE` に変更。EF Core エンティティでも `DateTime` → `DateTimeOffset` への変更を検討 |
| C-02 | **Critical** | architect, business-analyst | エンティティ欠落 | **`Review`/`ReviewResponse` エンティティ未定義**: spec.md（L2676-2677）で在庫管理サービスの Aggregate Root として `Review`（商品レビュー）と `ReviewResponse`（レビュー返信）が定義されているが、設計書のデータモデル（§4）、ER 図、DB スキーマに一切記載がない。spec.md（L341）では Review は独立した Aggregate Root として定義されている | `reviews` テーブル（id, product_id, user_id, rating, title, content, is_verified_purchase, helpful_count, status, created_at, updated_at）と `review_responses` テーブル（id, review_id, responder_id, content, created_at）を追加。CHECK 制約（rating 1-5, status IN ('PENDING','APPROVED','REJECTED')）を含める。ReviewEndpoints, ReviewService, IReviewRepository も追加 |
| C-03 | **Critical** | architect, tech-lead | Saga 統合 | **gRPC サービス定義の完全欠落**: spec.md で在庫管理サービスは Saga ステップ 2 として `inventory.proto`（`ReserveInventory`, `ReleaseReservation` RPC）を提供すると定義されている。設計書には gRPC サービス、`.proto` ファイル、gRPC Deadline 設計（500ms）、gRPC エンドポイントの記載が一切ない。Saga オーケストレーション（ADR-0009）による注文確定フローの実装が不可能 | §3 に gRPC サービス実装セクションを追加。`inventory.proto` のメッセージ定義（ReserveInventoryRequest/Response, ReleaseReservationRequest/Response）、gRPC Deadline（500ms）、`MapGrpcService<InventoryGrpcService>` 設定、Polly リトライ設定を記載 |
| C-04 | **Critical** | architect, dba-reviewer | イベント駆動 | **Outbox パターン（ADR-0005）の完全欠落**: 在庫変更イベント（InventoryUpdated, InventoryLow 等）の発行方法について、ADR-0005 で必須とされた Outbox パターンへの言及がない。`outbox_events` テーブル定義、`OutboxPublisher` BackgroundService の設計、動的バックオフ（100ms〜5s）が未記載。現状の設計では DB コミット成功後のイベント発行失敗時にデータ不整合が発生する | §6 に Outbox パターン実装を追加: `outbox_events` テーブル（id, aggregate_type, aggregate_id, event_type, payload, status, created_at, published_at, retry_count）、`OutboxPublisher` BackgroundService（Advisory Lock ID: `hashtext('outbox_publisher')`）、部分インデックス `WHERE status = 'PENDING'`、動的ポーリング間隔 |
| C-05 | **Critical** | dba-reviewer | データモデル | **`size_guides` テーブル欠落**: spec.md（L6782）で在庫管理サービスに `size_guides` テーブル（`category_id`, `size_chart` (JSONB)）が定義されているが、設計書に記載なし。スキー用品 EC サイトの返品率低減（目標 5% 以下、サイズ不適合 3% 以下）に直結する機能の設計が欠如している | `size_guides` テーブル（id, category_id FK, size_chart JSONB, guide_type, created_at, updated_at）を追加。SizeGuideEndpoints（GET /api/size-guides/{categoryId}）、SizeGuideService を追加。`compatibility_matrix` テーブルも spec.md に準じて追加検討 |
| C-06 | **Critical** | dba-reviewer, architect | データモデル | **`reserved_at` カラムと `InventoryReservationCleanupService` 未定義**: spec.md（L6579）で在庫引当タイムアウト解放の多層防御（3 層目）として、`inventory` テーブルの `reserved_at` カラムと `InventoryReservationCleanupService`（BackgroundService、15 分タイムアウト）が必須と定義されている。設計書の inventory テーブルスキーマに `reserved_at` カラムがなく、BackgroundService の設計セクションも存在しない | inventory テーブルに `reserved_at TIMESTAMP WITH TIME ZONE NULL` カラムを追加。§ に BackgroundService セクションを新設し、`InventoryReservationCleanupService` の設計（ポーリング間隔、Advisory Lock、15 分タイムアウト判定ロジック、`reserved_quantity` の解放処理）を記載 |
| C-07 | **Critical** | tech-lead | 整合性 | **金額データ型の不一致**: spec.md では monetary カラムに `DECIMAL(12,2)` を統一使用しているが、設計書の prices テーブルでは `DECIMAL(10,2)` を使用。高額スキー用品（10 万円超）や将来の多通貨対応を考慮すると、precision 不足のリスクがある | prices テーブルの `regular_price`, `sale_price` および price_histories テーブルの `price` を `DECIMAL(12,2)` に変更。supplier_price も `DECIMAL(12,2)` に統一 |
| H-01 | **High** | dba-reviewer | CHECK 制約 | **CHECK 制約の完全欠落**: spec.md（L2886-2893）で在庫管理サービスに 10 件の CHECK 制約が定義されている（products: price≥0, tax_rate, weight>0; inventory: quantity≥0, reserved_quantity≥0, quantity≥reserved_quantity, reorder_point≥0; reviews: rating 1-5, status 列挙値; price_histories: price≥0）。設計書の DB スキーマに CHECK 制約の記載が一切ない | 各テーブル定義に spec.md 準拠の CHECK 制約を追加。`sql-schema-review.instructions.md` §3 に従い制約名は `ck_` プレフィックスを付与 |
| H-02 | **High** | dba-reviewer | データモデル | **`inventory` テーブルのカラム名不一致**: spec.md（L2675）では `stockQuantity`, `availableQuantity`, `warehouseId`, `reorderLevel` を定義。設計書では `quantity`, `reserved_quantity`, `location_code`, `status` となっており、`availableQuantity`（計算カラムまたはアプリ算出）と `reorderLevel`（発注点）が欠落。`reorder_point` は CHECK 制約でも参照されており整合性がない | `reorder_point INTEGER NOT NULL DEFAULT 0` カラムを追加。`availableQuantity` は `quantity - reserved_quantity` のアプリ算出とするか、generated column とするか設計方針を明記 |
| H-03 | **High** | dba-reviewer | データモデル | **`inventory.status` の CHECK 制約未定義**: status カラムは `VARCHAR(50) NOT NULL DEFAULT 'IN_STOCK'` だが、有効なステータス値の CHECK 制約が未設定。spec.md の他テーブル（orders, payments 等）はすべて status カラムに列挙値 CHECK を定義しており、設計方針が不統一 | `CHECK (status IN ('IN_STOCK', 'LOW_STOCK', 'OUT_OF_STOCK', 'RESERVED', 'DISCONTINUED'))` 等のステータス CHECK 制約を追加 |
| H-04 | **High** | architect | Kafka | **Kafka トピック名と spec.md の不一致**: 設計書では `inventory.products`, `inventory.levels`, `inventory.alerts`, `inventory.reservations`, `inventory.pricing` の 5 トピックを定義。spec.md（L1545）では `inventory.stock_updated` を参照。ウィッシュリスト在庫復活通知連携で使用される `inventory.stock_updated` が設計書に存在しない | Kafka トピック一覧を spec.md と整合させる。`inventory.stock_updated`（在庫数変更）を追加するか、既存の `inventory.levels` が同等であることを明記。他サービス設計書との購読/発行の対応表を追加 |
| H-05 | **High** | architect | エンティティ | **`ProductAttribute` エンティティの設計方針不統一**: spec.md（L2675）では `ProductAttribute` を独立エンティティ（id, productId, attributeName, attributeValue, isFilterable, isSortable）として定義。設計書では Product エンティティの `attributes JSONB` カラムで代替している。JSONB 方式には柔軟性のメリットがあるが、spec.md との乖離理由と、`isFilterable`/`isSortable` のインデックス戦略が未記載 | JSONB 方式の採用理由をドキュメントに明記（ADR として記録推奨）。フィルタリング・ソート可能属性の検索にどのインデックス（GIN/BTREE on JSONB path）を使用するか設計を追加 |
| H-06 | **High** | programing-reviewer | コード例 | **EF Core エンティティの `[Table]`/`[Column]` 属性未定義**: AGENTS.md §10.3 で必須とされている `[Table("snake_case")]` および `[Column("snake_case")]` 属性付きのエンティティ定義コードが設計書に存在しない。テーブル定義は SQL スキーマとして記載されているが、C# エンティティクラスとのマッピングが不明 | 主要エンティティ（Product, Category, Inventory, Price, ProductImage）の EF Core エンティティコード例を追加。`[Table("products")]`, `[Column("sku")]` 等の属性、ナビゲーションプロパティ `= []` 初期化、`[Timestamp]` 楽観的ロック属性を含める |
| H-07 | **High** | programing-reviewer | コード例 | **Service/Repository インターフェース定義の欠落**: クラス構造セクションでクラス名は列挙されているが、インターフェース定義（`IProductService`, `IInventoryService`, `ICategoryService`, `IProductRepository`, `IInventoryRepository` 等）のシグネチャが記載されていない。AGENTS.md §10.1 のパターンに従ったインターフェース定義が必要 | `IProductService`（CreateProductAsync, GetByIdAsync, SearchAsync）、`IInventoryService`（ReserveAsync, ReleaseAsync, StockInAsync）、各 Repository インターフェースのメソッドシグネチャを定義。全メソッドに `CancellationToken ct = default` を含める |
| H-08 | **High** | programing-reviewer | コード例 | **在庫予約（Reserve）の `SELECT FOR UPDATE` 実装例が未記載**: spec.md の Saga ステップ 2 において、在庫引当は `SELECT FOR UPDATE` + 在庫減算で実装すると定義されている。設計書の在庫予約 API（POST /api/inventory/reserve）には実装パターンのコード例がなく、楽観的/悲観的ロックの選択根拠も未記載 | 在庫引当のトランザクション実装例を追加: `_context.Database.BeginTransactionAsync()` → `FromSqlInterpolated("SELECT ... FOR UPDATE")` → `reserved_quantity` 更新 → `reserved_at = DateTime.UtcNow` → `SaveChangesAsync` → `CommitAsync` |
| H-09 | **High** | security-reviewer | 認可 | **管理者専用エンドポイントの認可ポリシー未定義**: 商品作成（POST /api/products）、カテゴリ CRUD、在庫操作（stock-in, stock-out）は管理者操作だが、各エンドポイントに `RequireAuthorization("AdminOnly")` の明示がない。§7 で「全ての書き込み操作には適切なロールベース認証が必要」と記載されているが具体的なポリシー割当が未定義 | エンドポイント一覧テーブルに「認可」列を追加し、各エンドポイントの認可ポリシー（`AllowAnonymous`, `RequireAuthorization`, `RequireAuthorization("AdminOnly")`）を明示。GET /api/products, GET /api/categories は `AllowAnonymous`、書き込み操作は `RequireAuthorization("AdminOnly")` |
| H-10 | **High** | security-reviewer | IDOR | **在庫予約 API の所有者検証が未設計**: POST /api/inventory/reserve はユーザーのカート操作から呼ばれるが、リクエストに含まれる productId の正当性検証のみで、ユーザーに紐づかない在庫予約が可能な設計になっている。Saga 経由の内部呼出しと外部 API 呼出しの区別が未記載 | 在庫予約 API を内部（gRPC/サービス間）専用とし、外部 REST API からは直接アクセス不可とする設計を明記。または Saga コーディネーターからの Client Credentials トークンのスコープ検証（`inventory.stock:reserve`）を追加 |
| H-11 | **High** | dba-reviewer | インデックス | **Product テーブルの `category_id` FK インデックス命名**: `idx_products_category_id` は設定されているが、外部キー制約名（`fk_products_category`）が DB スキーマに明示されていない。`sql-schema-review.instructions.md` §3 で制約名のプレフィックス（`fk_`, `ck_`, `uq_`）が必須 | 全テーブルの FK 制約名を明示（`fk_inventory_product`, `fk_prices_product`, `fk_product_images_product` 等）。ON DELETE / ON UPDATE 動作も明記 |
| H-12 | **High** | dba-reviewer | テーブル設計 | **`product_images.type` カラムの CHECK 制約欠落**: `type VARCHAR(50) NOT NULL DEFAULT 'MAIN'` に有効値の CHECK 制約がない。`sql-schema-review.instructions.md` §2 に従い、ステータス・種別カラムには `SMALLINT + CHECK` または `VARCHAR + CHECK` を使用すべき | `CHECK (type IN ('MAIN', 'THUMBNAIL', 'GALLERY', 'DETAIL'))` 等の CHECK 制約を追加 |
| H-13 | **High** | audit-reviewer | Correlation ID | **Correlation ID ミドルウェアの設計未記載**: AGENTS.md §11.3 で必須のミドルウェアパイプライン順序に含まれる Correlation ID ミドルウェアが、設計書の Program.cs コード例（§12）に含まれていない | §12 の Program.cs 設定に `app.UseCorrelationId()` ミドルウェアを追加。リクエスト/レスポンスヘッダーへの `X-Correlation-Id` 設定と `LogContext.PushProperty` を記載 |
| H-14 | **High** | architect | コンポーネント | **コンポーネント図の不一致**: spec.md のコンポーネント図（L633）には `IMG_SERV[メディアサービス]` → `IMG_REPO[画像 Repository]` → `BLOB[(Azure Blob Storage)]` が含まれるが、設計書のコンポーネント図では `IMG_SERV[画像サービス]` → `BLOB` の直接接続となり、Repository レイヤーを経由しない。レイヤードアーキテクチャの依存方向（Endpoints → Services → Repositories）に違反する構造 | コンポーネント図に `IMG_REPO[画像 Repository]` を追加し、`IMG_SERV → IMG_REPO → BLOB` の依存関係に修正 |
| H-15 | **High** | qa-manager | テスト | **テストの `CancellationToken` 伝搬不足**: テスト例（§13）の `CreateProductAsync(request)` 呼び出しに `CancellationToken` が渡されていない。AGENTS.md §4.5 で全 async メソッドへの `CancellationToken ct = default` 伝搬が必須 | テストコード例の Service 呼び出しに `CancellationToken` を追加: `svc.CreateProductAsync(request, CancellationToken.None)` |
| H-16 | **High** | performance-reviewer | キャッシュ | **キャッシュウォームアップ戦略の具体設計が不足**: §9 で「ウォームアップ戦略: 頻繁にアクセスされるデータの事前キャッシュ投入」と記載されているが、実装パターン（BackgroundService による起動時ウォームアップ等）の詳細がない | キャッシュウォームアップの実装パターン（`CacheWarmupService : BackgroundService`）の設計を追加。ウォームアップ対象（上位 100 商品、全カテゴリ）と起動時実行ロジックを記載 |

---

## Medium 指摘一覧（改善推奨）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| M-01 | Medium | dba-reviewer | テーブル命名 | `prices` テーブル名は DB 予約語に近い。`product_prices` の方が明確 | テーブル名を `product_prices` に変更検討 |
| M-02 | Medium | dba-reviewer | FK 設計 | 全テーブルの ON DELETE / ON UPDATE 動作が未定義。spec.md（L2812-2813）では Review→Product は CASCADE と定義 | 明示的に定義（products→category: RESTRICT、product_images→product: CASCADE 等） |
| M-03 | Medium | architect | イベント購読 | 購読イベントの Consumer 実装（BackgroundService）の設計が未記載。OrderCreated / OrderCompleted / OrderCancelled の Consumer 設計が必要 | Kafka Consumer BackgroundService のクラス設計、エラーハンドリング（DLT 転送）、べき等性保証を追加 |
| M-04 | Medium | programing-reviewer | DTO | リクエスト DTO に Data Annotations/FluentValidation のバリデーション定義がない。`ProductCreateRequest` のフィールドバリデーション（`[Required]`, `[StringLength]`, `[Range]` 等）が未記載 | DTO 定義にバリデーション属性を追加。FluentValidation の Validator クラス例も記載 |
| M-05 | Medium | programing-reviewer | API 設計 | API パス `/api/products` のプレフィックス `/api` が spec.md のエンドポイント設計（`/products`）と不一致。AGENTS.md §6.1 では `/products` を使用 | `/api` プレフィックスの使用有無を統一。API バージョニング（`/api/v1/products`）を採用する場合はその旨を明記 |
| M-06 | Medium | architect | 設計詳細 | 在庫復活通知連携の設計が欠落。spec.md（L1545）で `inventory.stock_updated` → UserManagementService → MailSendService の通知フローが定義されているが、設計書の発行イベントに対応する記述がない | `InventoryUpdated` イベント発行時に「在庫 0→1 以上変化」の条件を検出し、ウィッシュリスト通知用のイベントペイロードを含める設計を追加 |
| M-07 | Medium | performance-reviewer | クエリ設計 | 商品検索の全文検索インデックス（GIN + `to_tsvector`）が SQL で定義されているが、EF Core からの利用方法（`FromSqlInterpolated` or `EF.Functions.ToTsVector`）が未記載 | EF Core からの PostgreSQL 全文検索クエリパターンを追加 |
| M-08 | Medium | compliance-reviewer | PII | Suppliers テーブルに `email`, `phone`, `address` の PII が含まれるが、PII 取扱い方針（暗号化、アクセス制限、保持期間）が未記載 | サプライヤー PII の暗号化方針（Azure Key Vault 管理鍵による列暗号化 or アプリ層暗号化）を §7 に追加 |
| M-09 | Medium | business-analyst | 機能 | 在庫分析エンジン（Analytics Engine）がコンポーネント図に含まれるが、具体的な分析機能（在庫回転率、ABC 分析、需要予測）の設計が未記載 | 分析機能の要件定義と実装優先度を追加（MVP 対象外の場合はその旨を明記） |
| M-10 | Medium | audit-reviewer | ログ | §12 の Serilog 設定に `Enrich.WithProperty("ServiceName", "InventoryManagementService")` があるが、Correlation ID Enricher（`LogContext.PushProperty("CorrelationId", correlationId)`）が欠落 | Correlation ID の LogContext 設定を追加 |
| M-11 | Medium | dba-reviewer | インデックス | `prices` テーブルの `idx_price_sale_dates` 複合インデックスのカラム順序が `(sale_start_date, sale_end_date)` だが、セール中商品の検索では `WHERE sale_start_date <= NOW AND sale_end_date >= NOW` となるため、カバリングインデックスまたは部分インデックスの方が効率的 | `CREATE INDEX idx_prices_active_sale ON prices(product_id) WHERE is_active = true AND sale_start_date <= CURRENT_TIMESTAMP AND sale_end_date >= CURRENT_TIMESTAMP` の部分インデックスを検討 |
| M-12 | Medium | infra-ops-reviewer | ヘルスチェック | Kafka ヘルスチェックが未設定。`builder.Services.AddHealthChecks()` に PostgreSQL と Redis のみ。AGENTS.md §11.2 で Kafka 疎通確認を含むヘルスチェックが推奨 | `AspNetCore.HealthChecks.Kafka` パッケージを追加し、Readiness チェックに Kafka 疎通確認を含める |
| M-13 | Medium | qa-manager | テスト | テスト戦略に在庫予約/解放の並行テスト（楽観的ロック競合テスト）が含まれていない | 同一商品への複数同時在庫予約テストシナリオを追加 |
| M-14 | Medium | architect | コンポーネント | `PriceService`/`PriceEndpoints` がクラス構造で定義されているが、§5 の REST API エンドポイント設計には含まれていない。エンドポイント一覧には価格管理 API が存在するが、`PriceEndpoints` クラスの Minimal API 実装パターン（`MapGroup`, `WithTags`, `WithOpenApi`）が未記載 | PriceEndpoints の実装パターンコード例を追加 |
| M-15 | Medium | business-analyst | 機能 | 価格管理 API（POST/PUT /api/prices）に監査ログ（誰がいつ価格を変更したか）の設計が未記載。PriceHistory テーブルは存在するが、変更者の追跡（`changed_by`）が欠落 | PriceHistory に `changed_by VARCHAR(255)` カラムを追加し、価格変更操作の監査証跡を確保 |

---

## Low 指摘一覧（推奨改善）

| # | 重要度 | 出典 Agent | 指摘内容 |
|---|--------|-----------|----------|
| L-01 | Low | programing-reviewer | テストクラス名が `ProductServiceTest` だが、テスト規約（test-standards.instructions.md §1）では `ProductServiceTests`（複数形）が推奨 |
| L-02 | Low | release-manager | CI/CD パイプラインの `dotnet-version` が `'10.0.x'` → ベースイメージの `10.0` と一致するが、パイプライン内のプロジェクトパス `InventoryManagementService/**` がソリューション構成に依存する前提が未記載 |
| L-03 | Low | infra-ops-reviewer | Dockerfile の `Docker 24.0+` が前提条件だが、ライブラリ一覧では `Docker 25.x` と記載されており微妙な不一致 |
| L-04 | Low | dba-reviewer | `product_suppliers.last_order_date` が `TIMESTAMP` だが他の日時カラムと同様に `TIMESTAMP WITH TIME ZONE` とすべき |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-01 | 高優先 | architect | `ProductAttribute` の実装方式（独立テーブル vs JSONB カラム）について、spec.md との乖離を許容するか判断が必要。JSONB は柔軟だがフィルタリング性能への影響評価が必要 | テックリード |
| E-02 | 高優先 | architect | 在庫予約 API（POST /api/inventory/reserve）を外部 REST API として公開するか、gRPC 内部専用とするかの判断が必要。セキュリティと Saga 設計に影響 | テックリード + セキュリティ担当 |
| E-03 | 通常 | business-analyst | 在庫分析エンジン（Analytics Engine）の MVP スコープ判断。Q3 の拡張計画に含まれるべきか、MVP に含めるべきか | プロダクトオーナー |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| — | — | — | 競合なし | — | — |

---

## ドキュメント横断分析

### マイクロサービス設計書カバレッジ（inventory-management-design.md）

| 設計領域 | 記載状況 | 評価 |
|---------|---------|------|
| API 定義 | 商品・カテゴリ・在庫・価格の REST API エンドポイント定義あり | ⚠️ gRPC 未記載、認可ポリシー未記載 |
| DB 設計 | products, categories, inventory, prices, product_images, suppliers, product_suppliers テーブル定義あり | ❌ reviews, review_responses, size_guides, outbox_events, compatibility_matrix テーブル欠落 |
| イベント定義 | 発行イベント 10 種、購読イベント 5 種定義あり | ⚠️ Outbox パターン未記載、トピック名 spec.md との不一致 |
| セキュリティ | TLS, JWT, RBAC, レート制限, CORS, 画像セキュリティ記載あり | ⚠️ エンドポイント別認可ポリシー未定義、IDOR 防止未設計 |
| 非機能要件 | キャッシュ戦略、パフォーマンス最適化、インデックス設計あり | ✅ 概ね充実 |
| テスト戦略 | 単体テスト・統合テスト例、カバレッジ目標あり | ⚠️ CancellationToken 不足、並行テスト不足 |
| 可観測性 | OpenTelemetry, ヘルスチェック, Serilog あり | ⚠️ Kafka ヘルスチェック・Correlation ID 不足 |
| デプロイ | Dockerfile, CI/CD パイプライン定義あり | ✅ 概ね充実 |
| BackgroundService | 設計セクション自体が存在しない | ❌ InventoryReservationCleanupService, OutboxPublisher, Kafka Consumer 全て欠落 |
| gRPC 統合 | 記載なし | ❌ 完全欠落 |

### サービス間整合性

| 連携先サービス | 連携方式 | 設計書の記載状況 |
|-------------|---------|---------------|
| SalesManagementService（Saga ステップ 2） | gRPC（ReserveInventory / ReleaseReservation） | ❌ 未記載 |
| SalesManagementService（イベント） | Kafka（OrderCreated, OrderCompleted, OrderCancelled） | ⚠️ 購読イベントは記載あるが Consumer 実装設計なし |
| UserManagementService（ウィッシュリスト通知） | Kafka（inventory.stock_updated） | ❌ 未記載 |
| PaymentCartService | Kafka（在庫変更通知） | ⚠️ イベント発行のみ記載、Outbox パターン未設計 |
| AiSupportService | Kafka（商品情報連携） | ⚠️ イベント発行のみ記載 |

### 未定義・曖昧な領域（実装ブロッカー候補）

| # | 領域 | 説明 | ブロッカーリスク |
|---|------|------|---------------|
| 1 | gRPC サービス実装 | Saga ステップ 2 の gRPC 実装が完全に欠落 | **高**: 注文確定フロー実装不可 |
| 2 | Outbox パターン | イベント発行の原子性保証が未設計 | **高**: データ不整合リスク |
| 3 | BackgroundService 全般 | InventoryReservationCleanupService, OutboxPublisher, Kafka Consumer | **高**: 運用上の自動化処理が実装不可 |
| 4 | Review 機能 | エンティティ・API・テスト全て欠落 | **中**: 独立実装可能だが spec.md と大きな乖離 |
| 5 | size_guides テーブル | サイズガイド機能のデータモデルが欠落 | **中**: EC サイトの返品率低減機能に影響 |

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### 判定: ⚠️ Conditional

**ビジネス要件の充足度評価**:

**充足している要件**:
- 商品カタログ管理（CRUD、検索、カテゴリ分類）
- 在庫管理（入庫、出庫、予約、解放、低在庫アラート）
- 価格管理（通常価格、セール価格、価格履歴）
- 商品画像管理（Azure Blob Storage 統合）
- イベント駆動設計（在庫変更通知、価格更新通知）

**不足している要件**:
- **Critical**: 商品レビュー機能（spec.md で在庫管理サービスの責務として定義）が完全欠落
- **High**: サイズガイド・互換性情報（spec.md H8-26）が未設計
- **High**: ウィッシュリスト在庫復活通知連携の設計が不足
- **Medium**: 在庫分析エンジンの具体的な機能設計が未記載

</details>

<details>
<summary>architect レビューレポート</summary>

### 判定: ❌ Rejected

**アーキテクチャ評価**:

**良好な点**:
- レイヤードアーキテクチャ（Endpoints → Services → Repositories）の基本構造は適切
- Redis キャッシュ戦略は実用的（TTL 設定、無効化戦略）
- コンポーネント分離（ProductService, InventoryService, CategoryService, PriceService）が明確

**Critical 指摘**:
1. gRPC Saga 統合の完全欠落（spec.md inventory.proto）
2. Outbox パターンの未実装（ADR-0005 違反）

**High 指摘**:
1. Kafka トピック名の spec.md 不一致
2. ProductAttribute の設計方針不統一
3. コンポーネント図の IMG_REPO レイヤー欠落

**Medium 指摘**:
1. Kafka Consumer BackgroundService の設計欠落
2. PriceEndpoints の実装パターン未記載

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 判定: ⚠️ Conditional

**コード品質評価**:

**良好な点**:
- C# 14 機能の活用（primary constructor、record 型）
- EF Core LINQ クエリの適切な使用（AsNoTracking、ページネーション）
- 例外階層の設計が AGENTS.md §4.7 準拠
- IDistributedCache によるキャッシュ実装パターンが適切

**High 指摘**:
1. EF Core エンティティの `[Table]`/`[Column]` 属性コード例なし
2. Service/Repository インターフェースのシグネチャ未定義
3. 在庫予約の `SELECT FOR UPDATE` 実装例なし

**Medium 指摘**:
1. DTO のバリデーション属性未定義
2. API パスプレフィックス `/api` の統一性

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 判定: ❌ Rejected

**DB 設計評価**:

**良好な点**:
- snake_case テーブル・カラム命名規則の遵守
- PostgreSQL インデックス戦略（GIN 全文検索インデックス含む）
- 監査カラム（created_at, updated_at, created_by, updated_by）の設定

**Critical 指摘**:
1. 全テーブルの `TIMESTAMP` → `TIMESTAMP WITH TIME ZONE` 未使用
2. `reviews`/`review_responses` テーブル欠落
3. `size_guides` テーブル欠落

**High 指摘**:
1. CHECK 制約の完全欠落
2. `inventory.reorder_point` カラム欠落
3. `inventory.status` の CHECK 制約未定義
4. FK 制約名と ON DELETE/ON UPDATE 動作の未定義
5. `product_images.type` の CHECK 制約欠落

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 判定: ⚠️ Conditional

**セキュリティ評価**:

**良好な点**:
- JWT 認証基盤の記載
- 画像アップロードのセキュリティ（ファイルタイプ・サイズ制限、メタデータ除去、SAS トークン）
- SQL インジェクション防止（EF Core LINQ 使用）
- Kestrel の `AddServerHeader: false` 設定

**High 指摘**:
1. エンドポイント別認可ポリシー（AdminOnly, AllowAnonymous）の未定義
2. 在庫予約 API の IDOR 防止が未設計

**Medium 指摘**:
1. appsettings.Development.json に秘密情報が含まれていないことは確認済みだが、Redis/Kafka 接続設定の本番環境変数参照が簡略化されている

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 判定: ✅ Pass

**Medium**: Suppliers テーブルの PII（email, phone, address）の取扱い方針が未記載

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 判定: ✅ Pass（条件付き）

**High**: Correlation ID ミドルウェアの設計未記載
**Medium**: 価格変更の監査証跡（PriceHistory.changed_by）が不完全

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 判定: ✅ Pass（条件付き）

**良好な点**:
- AAA パターン準拠のテストコード例
- NSubstitute + Shouldly の使用
- WebApplicationFactory による統合テスト
- カバレッジ目標の明示（単体 80%, 統合 70%, API 90%）

**High**: テストコードの CancellationToken 伝搬不足
**Medium**: 並行テスト（在庫予約競合）シナリオの欠落

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 判定: ✅ Pass（条件付き）

**良好な点**:
- Redis キャッシュ TTL 設定が実用的
- PostgreSQL インデックス戦略が適切
- Polly レジリエンスハンドラー設定あり
- AsNoTracking の適切な使用

**High**: キャッシュウォームアップの具体設計不足
**Medium**: 全文検索の EF Core 連携パターン未記載

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 判定: ✅ Pass

**良好な点**:
- マルチステージ Dockerfile（ビルド + ランタイム分離）
- 非 root ユーザー実行
- HEALTHCHECK 設定
- .NET Aspire の AppHost セットアップ手順

**Medium**: Kafka ヘルスチェック未設定
**Low**: Docker バージョンの微妙な不一致（24.0+ vs 25.x）

</details>

<details>
<summary>release-manager レビューレポート</summary>

### 判定: ✅ Pass

**Low**: CI/CD パイプラインのプロジェクトパス前提の明記不足

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 判定: ✅ Pass

全 NuGet パッケージが GA バージョン、禁止パッケージの使用なし。
`Azure.Storage.Blobs` と `Azure.Identity` はサービス固有の追加として妥当。

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 判定: ✅ Pass

バックエンド設計書のため UX/アクセシビリティの直接的なレビュー対象はない。
API レスポンス設計（i18n 対応の日本語商品名等）は適切。

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 判定: ❌ Rejected

**総合評価**: 基盤設計（レイヤー構造、キャッシュ、エラーハンドリング）は堅実だが、spec.md / ADR との整合性に **致命的な欠落が複数存在**する。

**最重要課題**:
1. **gRPC Saga 統合の完全欠落**: 注文確定フロー（ADR-0009）の実装可能性に直結
2. **Outbox パターンの未設計**: ADR-0005 違反。データ不整合リスク
3. **BackgroundService 設計セクションの不在**: InventoryReservationCleanupService（spec.md 三層防御）、OutboxPublisher、Kafka Consumer が全て未設計

**AGENTS.md 準拠状況**:
- §10.3 EF Core エンティティ: 未準拠（[Table]/[Column] 属性コード例なし）
- §10.4 Outbox パターン: 未準拠（設計セクション自体がない）
- §10.6 Kafka Consumer パターン: 未準拠（Consumer BackgroundService 未設計）
- §11.3 ミドルウェアパイプライン: 部分準拠（Correlation ID 欠落）

**実装ブロッカー**: gRPC 統合と Outbox パターンが解決されない限り、Phase 4（Service 実装）以降に進行不可。是正後の再レビューを強く推奨する。

</details>
