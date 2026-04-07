# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/inventory-management-design.md`
- **判定**: ⚠️ **Conditional Approval** — High 指摘あり、人間の判断を介在
- **レビュー日時**: 2026-04-03 
- **イテレーション**: 3
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

## 技術スタック検証結果
| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| ランタイム | .NET 10 (C# 14) | .NET 10 (C# 14) | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| ORM | Entity Framework Core 10 | Entity Framework Core 10 | ✅ |
| DB | PostgreSQL (inventorydb) | PostgreSQL | ✅ |
| キャッシュ | StackExchange.Redis | StackExchange.Redis | ✅ |
| メッセージング | Confluent.Kafka | Confluent.Kafka | ✅ |
| オーケストレーション | .NET Aspire 13.1 | .NET Aspire 13.1 | ✅ |
| 認証 | JWT Bearer | JWT Bearer | ✅ |
| バリデーション | FluentValidation 11.* | FluentValidation 11.* | ✅ |
| レジリエンス | Polly 8.* + Microsoft.Extensions.Http.Resilience 9.* | Polly 8.* + Http.Resilience 9.* | ✅ |
| ログ | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| 分散トレーシング | OpenTelemetry 1.* | OpenTelemetry 1.* | ✅ |
| ヘルスチェック | AspNetCore.HealthChecks.NpgSql 9.* / Redis 9.* | 同左 | ✅ |
| 画像ストレージ | Azure Blob Storage (Azure.Storage.Blobs 12.*) | Azure Blob Storage | ✅ |
| テスト | xUnit / NSubstitute / Shouldly / Testcontainers | 同左 | ✅ |

## 指摘サマリー
| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ⚠️ | 0 | 1 | 2 | 1 |
| architect | ⚠️ | 0 | 2 | 2 | 0 |
| tech-lead | ⚠️ | 0 | 1 | 2 | 1 |
| programing-reviewer | ✅ | 0 | 0 | 3 | 1 |
| security-reviewer | ⚠️ | 0 | 2 | 1 | 0 |
| dba-reviewer | ⚠️ | 0 | 1 | 3 | 1 |
| qa-manager | ⚠️ | 0 | 1 | 1 | 0 |
| performance-reviewer | ✅ | 0 | 0 | 2 | 1 |
| compliance-reviewer | ⚠️ | 0 | 1 | 1 | 0 |
| oss-reviewer | ✅ | 0 | 0 | 0 | 1 |
| release-manager | ✅ | 0 | 0 | 1 | 1 |
| infra-ops-reviewer | ✅ | 0 | 0 | 2 | 0 |
| audit-reviewer | ✅ | 0 | 0 | 2 | 1 |
| ux-accessibility-reviewer | ✅ | 0 | 0 | 0 | 1 |
| **合計** | | **0** | **9** | **22** | **9** |

## 判定根拠
- 判定ルール適用結果: Critical 0 件、High 9 件 → **⚠️ Conditional Approval**（人間が High 指摘の受容/是正を判断）
- 最も重大な指摘: SKU/バリエーション管理モデルの不在（H-01）、商品の PUT/PATCH/DELETE API の欠落（H-02）、gRPC 認証設計の詳細不足（H-06）

---

## Critical/High 指摘一覧（修正推奨）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| H-01 | High | architect, business-analyst | データモデル | **SKU/バリエーション（サイズ・カラー）管理モデルが未定義**。spec.md のビジネス要件に「スキー板のサイズ（170cm/180cm/190cm）、ブーツのサイズ、ウェアのカラー」が含まれるが、設計書では `Product` エンティティにバリエーション概念がない。API レスポンス例には `availableSizes` が含まれるが、データモデル・エンティティにサイズ/カラー別 SKU の管理構造がなく、実装不可能な状態。`attributes` JSONB カラムでのバリエーション管理は検索・在庫引当の観点で不十分 | `ProductVariant` エンティティ（`id`, `productId`, `sku`, `size`, `color`, `additionalPrice`, `stockQuantity`）を追加するか、`Inventory` テーブルに `variant_sku` / `size` / `color` カラムを追加し、バリエーション別在庫管理を可能にする。商品詳細 API レスポンスの `availableSizes` をデータモデルから導出可能にする |
| H-02 | High | architect, tech-lead | API 設計 | **商品の更新（PUT/PATCH）・削除（DELETE）エンドポイントが未定義**。`POST /api/products`（作成）は定義済みだが、`PUT /api/products/{id}` および `DELETE /api/products/{id}` が API 一覧テーブルに存在しない。spec.md §3.3「在庫管理サービス」の責務「商品カタログ管理」を満たすには CRUD の全操作が必要。管理者ストーリー「商品情報の編集・削除」が実装不可能 | `PUT /api/products/{id}`（AdminOnly）および `DELETE /api/products/{id}`（AdminOnly）を API テーブルに追加。対応する `IProductService` のメソッド（`UpdateAsync`, `DeleteAsync`）とリクエスト DTO（`ProductUpdateRequest`）も定義する。論理削除（`active = false`）か物理削除かの設計判断も記載する |
| H-03 | High | architect | Saga/gRPC | **Saga ステップ番号の矛盾**。spec.md の Saga ステップ構成（ADR-0009）では在庫確認・引当は「ステップ 1」（最初のステップ）だが、spec.md §4「販売管理サービス」の Saga ステップ一覧では「ステップ 2」として記載。設計書タイトルでも「Saga ステップ 2」と記載。spec.md 内での SSOT が不明確で、実装時に混乱を招く | spec.md の ADR-0009 テーブルと §4 販売管理の Saga ステップ一覧を照合し、統一的なステップ番号を確定させる。設計書内の参照も統一する。SSOT を spec.md §4 の 9 ステップ定義に統一することを推奨 |
| H-04 | High | dba-reviewer | データモデル | **`inventory` テーブルの `product_id` に UNIQUE インデックスが設定されているが、マルチロケーション（将来拡張 Q1）と矛盾**。AppDbContext で `entity.HasIndex(e => e.ProductId).IsUnique()` が定義されているが、将来の拡張計画（§17 Q1 マルチロケーション対応）では同一商品が複数倉庫に存在する。UNIQUE 制約は複合ユニーク (`product_id`, `location_code`) であるべき。spec.md のインデックス設計でも `(product_id, warehouse_id) UNIQUE` と定義されており矛盾する | AppDbContext の `HasIndex(e => e.ProductId).IsUnique()` を `HasIndex(e => new { e.ProductId, e.LocationCode }).IsUnique()` に変更する。spec.md のインデックス設計と整合させる |
| H-05 | High | security-reviewer | セキュリティ | **画像アップロードの API エンドポイントと FluentValidation ルールが未定義**。§7 セキュリティ設計で「画像セキュリティ」として概要は記載されているが、画像アップロード用の REST API エンドポイント（`POST /api/products/{id}/images`）が API 一覧に存在しない。ファイルタイプ検証（マジックバイト検証 vs 拡張子のみ）、最大ファイルサイズ、同時アップロード数制限の詳細設計がない | `POST /api/products/{id}/images`（AdminOnly）を API テーブルに追加。`ImageUploadRequest` の FluentValidation ルール（ファイルサイズ上限 10MB、MIME type ホワイトリスト `image/jpeg,image/png,image/webp`、マジックバイト検証、ファイル名のサニタイズ）を明記する |
| H-06 | High | security-reviewer | セキュリティ | **gRPC サービスの認証・認可の具体的実装が不足**。§5.1 で `Client Credentials（スコープ: inventory.stock:reserve）` と記載されているが、JWT トークンの取得フロー、スコープ検証の実装場所（gRPC Interceptor）、トークンキャッシュ戦略の詳細がない。不正な gRPC 呼び出しを防止する多層防御（mTLS + JWT + スコープ）の設計が曖昧 | gRPC Interceptor で `inventory.stock:reserve` スコープを検証するコード例を追加。`IAuthorizationService` との統合パターン、Client Credentials トークンのキャッシュ TTL（トークン有効期限の 80%）、mTLS の適用有無を明記する |
| H-07 | High | business-analyst | ビジネス要件 | **在庫アラート（低在庫通知）のエンドツーエンドフローが不完全**。spec.md のペルソナ 3 管理者ストーリー「在庫が閾値を下回った商品の自動アラートを受け取りたい」に対し、設計書では `InventoryLow` イベント発行と `GET /api/inventory/low-stock` API は定義されているが、閾値到達を検知するトリガータイミング（入庫/出庫操作時 vs 定期バッチ）、MailSendService との `inventory.low-stock` イベント連携の詳細、管理画面ダッシュボード通知の仕組みが未記載 | `StockInAsync` / `ReserveAsync` の各メソッド内で `quantity - reservedQuantity <= reorderPoint` 判定を追加し、条件成立時に `InventoryLow` イベントを Outbox 経由で発行するフローを記載。MailSendService が `inventory.low-stock` トピックを購読してアラートメールを送信するシーケンス図を追加 |
| H-08 | High | qa-manager | テスト | **在庫予約のコンカレンシーテストが未定義**。`SELECT FOR UPDATE` による排他制御は実装コードが記載されているが、複数の Saga が同時に同一商品の在庫を引当するケースの統合テスト設計がない。在庫引当はミッションクリティカルなビジネスロジックであり、コンカレンシーテストの欠如は本番障害リスク | `Testcontainers.PostgreSql` を使用した並行在庫引当テスト（`Parallel.ForEachAsync` で 10 並行リクエスト → 在庫合計の整合性検証）のテストケースとアサーション基準を追記する |
| H-09 | High | compliance-reviewer | データ保護 | **レビューの `userId` に対する GDPR/個人情報保護法対応（DSR 削除・匿名化）の設計が未記載**。`reviews` テーブルの `user_id` はマイクロサービス間参照（FK 制約なし）だが、ユーザーの削除要求（DSR）時に `user_id` の匿名化またはレビューの削除が必要。spec.md の GDPR 対応セクションとの整合が不明 | ユーザー削除時の `reviews.user_id` 匿名化ポリシー（例: `user_id = 'DELETED_USER'` に更新、レビュー本文は残存）を記載。`user.deleted` Kafka イベントを購読して匿名化を実行する BackgroundService の設計を追加 |

---

## Medium 指摘一覧

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| M-01 | Medium | programing-reviewer | コード品質 | `OutboxPublisher` 内で `ExecuteSqlRawAsync` を使用して Advisory Lock を取得しているが、パラメータ化されていない。AGENTS.md で `FromSqlRaw` での文字列結合は禁止されているが、Advisory Lock のような固定 SQL 文字列はセキュリティリスクは低い。ただし一貫性のため `ExecuteSqlInterpolatedAsync` の使用を推奨 | `ExecuteSqlRawAsync("SELECT pg_advisory_lock(...")` を `ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_lock(hashtext({'outbox_publisher'}))")` に変更するか、定数パラメータとして明示的に渡す |
| M-02 | Medium | programing-reviewer | コード品質 | `ReserveAsync` メソッド内で `reservation_id` を `Guid.NewGuid().ToString()` で生成して返却しているが、この ID がどこにも永続化されていない。`ReleaseAsync` で `reservationId` を引数に取るが、照合先がないため解放時に引当の特定ができない | `inventory` テーブルに `reservation_id` カラムを追加するか、`inventory_reservations` テーブルを新設して予約 ID を永続化する。`ReleaseAsync` で `reservation_id` による照合を可能にする |
| M-03 | Medium | programing-reviewer | コード品質 | `Inventory` エンティティに `[Timestamp]` / `RowVersion` が定義されていない。`Product` には定義済み。在庫操作での楽観的ロック競合検出が不可能 | `Inventory` エンティティにも `RowVersion` プロパティを追加（ただし `SELECT FOR UPDATE` 使用時は不要な場合もある。設計判断を明記すべき） |
| M-04 | Medium | dba-reviewer | データモデル | `products` テーブルに spec.md で定義されている `price`（DECIMAL(12,2)）、`cost`（DECIMAL(12,2)）、`taxRate` カラムが存在しない。`prices` テーブル分離設計は理解できるが、spec.md のエンティティ定義との差異が明示的に記録されていない | spec.md との差異を補足説明に追記。`products` テーブルに `price`/`cost` を持たない設計判断の根拠（価格テーブル分離方針）を明記する |
| M-05 | Medium | dba-reviewer | データモデル | spec.md で定義されている `ProductAttribute` テーブルが設計書では JSONB 方式に置き換えられている。GIN インデックスでの検索性能は説明されているが、`isFilterable`/`isSortable` の JSONB 内メタデータ構造の具体例が不十分 | JSONB 内のメタデータスキーマ例を拡充する。カテゴリ別の属性定義テンプレート（スキー板: length/width/flex/radius、ブーツ: size/flex/lastWidth）を追加 |
| M-06 | Medium | dba-reviewer | データモデル | `suppliers` テーブルの `email`, `phone`, `address` はサプライヤーの個人情報に該当する可能性がある（担当者名あり）。暗号化やアクセス制限の設計がない | サプライヤー連絡先情報のアクセス制限（AdminOnly）を明記。GDPR 対応が必要な場合は暗号化要件を追記 |
| M-07 | Medium | architect | Saga 整合性 | `OrderCreatedConsumer`（Kafka Consumer）と `InventoryGrpcService`（gRPC）の両方で在庫予約を行っているが、使い分けの設計意図が不明確。Saga ステップ 2 は gRPC 経由だが、`order.created` イベントの Consumer でも在庫予約を行うのは二重引当のリスク | `OrderCreatedConsumer` の役割を明確化する。Saga 経由の在庫引当とイベント経由の在庫引当が共存する場合のべき等性保証と処理の優先順位を記載。不要であれば `OrderCreatedConsumer` を削除し、gRPC 経由のみに統一することを推奨 |
| M-08 | Medium | tech-lead | 規約整合性 | Inventory エンティティの DB テーブル名が `inventory`（単数形）。AGENTS.md / `sql-schema-review.instructions.md` ではテーブル名を `snake_case` **複数形** と規定。`products`, `categories`, `prices` は複数形だが `inventory` のみ単数形 | テーブル名を `inventories` に統一するか、spec.md 側と協議して例外を明記 |
| M-09 | Medium | tech-lead | 設計整合性 | AppDbContext の `SaveChangesAsync` オーバーライドで `UpdatedAt` を `_timeProvider.GetUtcNow().UtcDateTime` で設定しているが、エンティティのプロパティ型は `DateTimeOffset`。`DateTime` と `DateTimeOffset` の型不整合が発生する | `_timeProvider.GetUtcNow()` の戻り値（`DateTimeOffset`）をそのまま使用するか、全エンティティの `CreatedAt`/`UpdatedAt` の型を統一する |
| M-10 | Medium | performance-reviewer | パフォーマンス | `SearchProductsAsync` でキーワード検索に `p.Name.Contains(criteria.Keyword)` を使用しているが、SQL の `LIKE '%keyword%'` に変換されインデックスが使用されない。§11 で定義されている GIN インデックス（`to_tsvector`）が活用されていない | EF Core の `EF.Functions.ToTsVector("simple", ...)` を使用した全文検索クエリに変更するか、`FromSqlInterpolated` で `to_tsvector` / `to_tsquery` を使用する実装例を追記 |
| M-11 | Medium | performance-reviewer | パフォーマンス | `CacheWarmupService` が起動時に「上位 100 商品」と「全カテゴリ（最大 1000 件）」をキャッシュに投入するが、大量商品（数千件以上）のスケール時の起動時間への影響が未評価 | 起動時キャッシュウォームアップの所要時間の上限（例: 30 秒以内）と、超過時のフォールバック（ウォームアップ未完了でもリクエスト受付開始）を記載 |
| M-12 | Medium | infra-ops-reviewer | インフラ | Dockerfile の `EXPOSE 5003` だが HEALTHCHECK URL も `http://localhost:5003/health`。Aspire 環境や Azure Container Apps では動的ポート割り当ての可能性があり、ハードコードされたポート番号の適切性を確認すべき | `ASPNETCORE_URLS` 環境変数による動的ポート設定のサポートを記載。HEALTHCHECK の URL もポート変数化を検討 |
| M-13 | Medium | infra-ops-reviewer | 可観測性 | Kafka ヘルスチェック（`AddKafka(kafkaConfig, ...)`）が §12 のコード例に記載されているが、NuGet パッケージ一覧（§2）に `AspNetCore.HealthChecks.Kafka` パッケージが含まれていない | §2 の主要ライブラリテーブルに `AspNetCore.HealthChecks.Kafka` パッケージを追加するか、Kafka ヘルスチェックのカスタム実装を記載 |
| M-14 | Medium | audit-reviewer | 監査 | 価格変更の監査証跡として `price_histories` テーブルと `changed_by` カラムが定義されているが、商品情報の変更（名前、説明、属性等）の監査証跡が設計されていない | 商品情報変更の監査ログ出力（構造化ログによる変更前後の記録、または `product_audit_logs` テーブル）を検討 |
| M-15 | Medium | audit-reviewer | トレーサビリティ | `InventoryReservationCleanupService` で期限切れ引当を解放する際、対象となった `OrderId` がログに含まれていない。在庫引当の `reserved_at` は記録されているが、どの注文の引当だったかのトレーサビリティが欠如 | `inventory` テーブルに `reserved_order_id` カラムを追加するか、クリーンアップ時のログに関連する注文 ID を含める設計を追加 |
| M-16 | Medium | compliance-reviewer | データ保護 | サプライヤーテーブルに `contact_person`（担当者名）、`email`、`phone`、`address` が含まれるが、これらの個人情報のデータ保持期間・削除ポリシーが未記載 | サプライヤー個人情報のデータライフサイクル（保持期間、非アクティブ化後の匿名化タイミング）を記載 |
| M-17 | Medium | release-manager | リリース | CI/CD パイプラインで `dotnet test` の失敗時にデプロイを中止する条件（teststep の `if: success()` ガード）が明示されていない。テスト失敗時にもデプロイが進行するリスク | GitHub Actions の `needs` / `if: success()` による依存関係を明示するか、テスト失敗時のデプロイ中止を確認する |

---

## Low 指摘一覧

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| L-01 | Low | programing-reviewer | コード品質 | `InventoryGrpcService.ReleaseReservation` メソッドのエラーハンドリングが不足。例外発生時に `ReleaseReservationResponse { Success = false }` を返す try-catch がない（ReserveInventory には存在） | `ReleaseReservation` にも try-catch を追加し、失敗時の応答を定義 |
| L-02 | Low | business-analyst | UX | 商品検索 API（`GET /api/products/search`）にソート指定パラメータ（`sortBy`, `sortDir`）が含まれていない。商品一覧 API（`GET /api/products`）には存在する | 検索 API にもソートパラメータを追加 |
| L-03 | Low | tech-lead | 表記統一 | §1「概要」で `TIMESTAMP` 表記と §4 テーブル定義で `TIMESTAMP WITH TIME ZONE` が混在。EF Core エンティティでは `DateTimeOffset` を使用しており、PostgreSQL 側は `TIMESTAMP WITH TIME ZONE` が正。一貫性を確保すべき | 全テーブル定義で `TIMESTAMP WITH TIME ZONE` に統一（既に概ね統一されているが、概要文の表記も合わせる） |
| L-04 | Low | dba-reviewer | インデックス | `categories` テーブルの `name` カラムにインデックスが定義されていない。カテゴリ一覧 API の `name` フィルタクエリに影響 | `CREATE INDEX idx_categories_name ON categories(name)` を追加 |
| L-05 | Low | oss-reviewer | ライセンス | `Azure.Identity 1.*` パッケージが NuGet 一覧に含まれているが、ローカル開発時にも Azure 認証が必要になるかの考慮が不明確 | 開発環境での DefaultAzureCredential のフォールバック（環境変数 or マネージド ID）を appsettings.Development.json で設定するガイドを追加 |
| L-06 | Low | performance-reviewer | パフォーマンス | `CacheConfig` 設定クラスが設定ファイルには定義されているがクラス定義（record 型）のコード例がない | `CacheConfig` と `KafkaConfig` の record 型定義と `IOptions<T>` バインディングのコード例を追加 |
| L-07 | Low | release-manager | バージョニング | §14 Dockerfile で `EXPOSE 5003` が固定されているが、.NET Aspire 環境では `ASPNETCORE_URLS` で上書きされる。EXPOSE はドキュメント的な役割のみ | EXPOSE の値が Aspire の `WithEndpoint` 設定と一致することを確認する旨のコメントを Dockerfile に追加 |
| L-08 | Low | audit-reviewer | ドキュメント | §15 運用・保守の RTO が「4 時間以内」と記載されているが、ADR-0010 では RTO 1 時間に統一されている。矛盾 | §15 の RTO を「1 時間以内」に修正（ADR-0010 準拠） |
| L-09 | Low | ux-accessibility-reviewer | UX | 商品画像の `alt_text` が NULL 許容（`VARCHAR(200) NULL`）。WCAG 2.1 SC 1.1.1 では全ての意味のある画像に代替テキストが必要 | `alt_text` を `NOT NULL` 制約に変更するか、`DEFAULT ''`（装飾画像用）とし、アプリケーション層で非装飾画像への `altText` 必須バリデーションを追加 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-01 | 高優先 | architect, business-analyst | **SKU/バリエーションモデルの設計方針決定**（H-01）。`ProductVariant` テーブル方式 vs JSONB 属性方式 vs `Inventory` テーブル拡張方式のトレードオフ評価が必要。EC サイトの在庫管理の根幹に関わるため、PO・テックリードの判断が必要 | テックリード + PO |
| E-02 | 高優先 | architect | **`OrderCreatedConsumer` と `InventoryGrpcService` の二重在庫引当リスク**（M-07）。Saga フロー（gRPC）だけで在庫引当を行うのか、Kafka イベント経由でも行うのか、設計意図の明確化が必要 | テックリード |
| E-03 | 通常 | dba-reviewer | **`inventory` テーブル名の単数形/複数形**（M-08）。`inventory` は不可算名詞の側面があり、`inventories` への改名が適切かの言語的・慣習的判断 | テックリード |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| C-01 | architect（`ProductVariant` テーブル推奨） | dba-reviewer（JSONB 拡張推奨） | バリエーション管理のデータモデル方式が相反 | **エスカレーション（E-01）** — ビジネス要件（在庫引当の粒度・検索性能）に依存するため、PO + テックリードの判断を要する | SKU 別在庫引当が必須の場合は `ProductVariant` + リレーショナル方式、属性検索のみの場合は JSONB 方式がコスト効率が高い |

---

## ドキュメント横断分析

### spec.md との整合性評価

| 項目 | spec.md の記載 | 設計書の対応 | 整合性 |
|------|-------------|-----------|--------|
| サービス責務 | 商品カタログ管理、在庫追跡、商品属性・カテゴリ管理、価格管理、入出荷、画像管理 | 全項目カバー | ✅ |
| 主要エンティティ | Product, Category, Inventory, Supplier, PriceHistory, ProductImage | 全エンティティ + Review, SizeGuide, OutboxEvent を追加 | ✅ |
| データストア | PostgreSQL + Azure Blob Storage | 一致 | ✅ |
| Aggregate Root | Product (子: ProductImage, PriceHistory, ProductAttribute), Review (子: ReviewResponse), Category | 一致（ProductAttribute は JSONB 方式に変更、設計判断記載あり） | ⚠️ 差異あり（M-05） |
| Value Object | Money, DateRange, Quantity, Rating | 設計書でエンティティにインラインで使用（Value Object クラス定義は未記載） | ⚠️ 未記載 |
| Saga ステップ 2 | 在庫確認・引当（gRPC） | `InventoryGrpcService` で実装。proto 定義あり | ✅ |
| Kafka イベント | InventoryUpdated, InventoryLow | 発行・購読イベント一覧が詳細に定義 | ✅ |
| FK 制約 | Product→Category: RESTRICT, Inventory→Product: CASCADE | 一致 | ✅ |
| CHECK 制約 | inventory.quantity >= 0, quantity >= reserved_quantity | 一致 | ✅ |
| インデックス | sku UNIQUE, category_id, GIN full-text | 一致（+ 追加のインデックス定義あり） | ✅ |
| 在庫引当の排他制御 | `SELECT FOR UPDATE` | 実装コード記載あり | ✅ |
| 15 分タイムアウト解放 | spec.md 三層防御 第 3 層 | `InventoryReservationCleanupService` で実装 | ✅ |
| Outbox パターン | ADR-0005 準拠 | `OutboxPublisher` + `outbox_events` テーブル定義あり | ✅ |
| ヘルスチェック | `/health` + `/health/ready` | 定義あり | ✅ |
| ミドルウェア順序 | AGENTS.md §11.3 準拠 | §12 で順序を明示 | ✅ |
| 非機能要件 | SLO 99.95%, API p95 300ms | 設計書では直接的な SLO 言及なし | ⚠️ 明示的記載推奨 |

### サービス間整合性

| チェック項目 | 結果 | 詳細 |
|------------|------|------|
| gRPC proto 定義 | ✅ | `inventory.proto` の `ReserveInventory`/`ReleaseReservation` が spec.md の定義と一致 |
| Kafka イベント整合性 | ✅ | `inventory.products`, `inventory.levels`, `inventory.alerts`, `inventory.reservations`, `inventory.pricing`, `inventory.stock_updated` トピック定義が spec.md と整合 |
| 購読イベント整合性 | ⚠️ | `OrderCreated`, `OrderCompleted`, `OrderCancelled` は spec.md と一致するが、`ShipmentCompleted`, `ReturnProcessed` の発行元とスキーマの詳細が spec.md 側で未確認 |
| gRPC Deadline | ✅ | 500ms（spec.md のステップ 2 バジェット 100ms の 5 倍マージンと一致） |

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### 評価: ⚠️ Conditional

**良好な点**:
- 商品カタログ管理、在庫追跡、カテゴリ管理、価格管理の基本機能が網羅的に設計されている
- spec.md のペルソナ 3（管理者）の「在庫アラート」ストーリーに対応する `InventoryLow` イベントが定義されている
- サイズガイド機能はスキー用品 EC サイト特有のドメイン知識を反映した優れた設計
- 商品レビュー機能（承認ワークフロー、購入確認済みフラグ、管理者返信）が詳細に設計されている

**指摘事項**:
- **H-01**: SKU/バリエーション管理モデルが未定義
- **H-07**: 低在庫アラートのエンドツーエンドフローが不完全
- **M-xx**: 商品の「予約販売」ステータス対応が Phase 2 スコープとの整理が不明確（spec.md では Phase 2 と明記されているが、設計書に Phase 1/2 の境界が記載されていない）
- **L-02**: 検索 API のソートパラメータ欠如
</details>

<details>
<summary>architect レビューレポート</summary>

### 評価: ⚠️ Conditional

**良好な点**:
- レイヤードアーキテクチャ（Endpoints → Services → Repositories）が AGENTS.md §2.1 に完全準拠
- Outbox パターン（ADR-0005）の実装が詳細で、動的バックオフ（100ms〜5s）も記載
- gRPC サービス設計が Saga オーケストレーション（ADR-0009）と整合
- Mermaid 図によるコンポーネントアーキテクチャが明確
- Aggregate Root の定義（Product, Review, Category）が spec.md と一致

**指摘事項**:
- **H-02**: 商品 CRUD の更新・削除 API 未定義
- **H-03**: Saga ステップ番号の spec.md 内矛盾
- **M-07**: `OrderCreatedConsumer` と gRPC の二重在庫引当リスク
</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 評価: ⚠️ Conditional

**良好な点**:
- C# 14 の機能活用（primary constructor, record 型, switch 式）が AGENTS.md §4.4 に準拠
- CancellationToken が全 async メソッドシグネチャに含まれ、下位呼び出しに伝搬
- `TimeProvider` DI によるテスト容易性の確保
- 構造化ログ（メッセージテンプレート）が AGENTS.md §4.2 に準拠
- AppDbContext の `SaveChangesAsync` オーバーライドによるタイムスタンプ自動更新

**指摘事項**:
- **H-02**: 商品 PUT/DELETE API の欠落（管理者フロー不完全）
- **M-08**: テーブル名 `inventory` が単数形（規約は複数形）
- **M-09**: `DateTime` と `DateTimeOffset` の型不整合
- **L-03**: TIMESTAMP 表記の不統一
</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 評価: ✅ Approved with Notes

**良好な点**:
- Minimal API パターン（`MapGroup` + 拡張メソッド）が AGENTS.md §6.2 に準拠
- Service/Repository インターフェース定義が 1 Aggregate Root = 1 Repository の原則に準拠
- EF Core エンティティの `[Table]` / `[Column]` 属性による snake_case マッピングが規約準拠
- コレクションナビゲーションプロパティが `= []` で初期化済み
- 例外階層が RFC 9457 準拠の Problem Details にマッピング

**指摘事項**:
- **M-01**: `ExecuteSqlRawAsync` でのAdvisory Lock
- **M-02**: `reservation_id` の非永続化
- **M-03**: `Inventory` エンティティの `RowVersion` 未定義
- **L-01**: `ReleaseReservation` のエラーハンドリング不足
</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional

**良好な点**:
- REST API のロールベース認可（AdminOnly / AllowAnonymous / RequireAuthorization）が適切に設定
- gRPC サービスを REST API として外部公開せず、内部通信専用にする設計は IDOR 防止として優れている
- 画像セキュリティの概要（ファイルタイプ制限、メタデータ除去、SAS トークン）が記載
- SQL インジェクション防止（EF Core LINQ + `FromSqlInterpolated`）が徹底
- `SELECT FOR UPDATE` での `FromSqlInterpolated` 使用は安全

**指摘事項**:
- **H-05**: 画像アップロード API とバリデーションルールの未定義
- **H-06**: gRPC 認証の具体的実装（Interceptor、mTLS）の不足
- **M-xx**: `suppliers.email` / `suppliers.phone` の暗号化検討（個人情報の可能性）
</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional

**良好な点**:
- PostgreSQL テーブル定義が snake_case で統一、CHECK 制約が詳細に定義
- インデックス戦略が GIN（全文検索）、B-Tree（複合）を適切に使い分け
- 部分インデックス（`WHERE status = 'PENDING'`）によるポーリングクエリの最適化
- FK 制約の ON DELETE/ON UPDATE が適切に設計（Category→RESTRICT, Inventory→CASCADE）
- EF Core Fluent API（`HasCheckConstraint`）でマイグレーションに CHECK 制約が含まれる

**指摘事項**:
- **H-04**: `inventory.product_id` の UNIQUE 制約がマルチロケーション拡張と矛盾
- **M-04**: spec.md の `products.price`/`cost` カラムとの差異の明示不足
- **M-05**: JSONB 属性メタデータのスキーマ例不足
- **M-06**: サプライヤー個人情報のアクセス制限設計不足
- **L-04**: `categories.name` インデックス未定義
</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 評価: ⚠️ Conditional

**良好な点**:
- 単体テスト例（xUnit + NSubstitute + Shouldly）が AAA パターンで記載
- 統合テスト例（WebApplicationFactory + Testcontainers.PostgreSql）が記載
- テストカバレッジ目標（Service 80%, 統合 70%, API 90%）が設定

**指摘事項**:
- **H-08**: 在庫予約の並行テスト（コンカレンシーテスト）の設計が未定義
- **M-xx**: gRPC サービスのテスト方針（`Grpc.Core.Testing` の使用可否）が未記載
</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 評価: ✅ Approved with Notes

**良好な点**:
- Redis キャッシュ戦略が TTL 階層（商品 30 分、カテゴリ 1 時間、在庫 5 分）で適切に設計
- `AsNoTracking()` が読み取り専用クエリで一貫して使用されている
- PostgreSQL インデックス戦略が網羅的（GIN, B-Tree, 部分インデックス）
- Polly によるサーキットブレーカー・リトライが設定されている
- 商品検索のページネーションが設計されている

**指摘事項**:
- **M-10**: 全文検索で `Contains` (LIKE) ではなく GIN インデックス活用を推奨
- **M-11**: `CacheWarmupService` の起動時間の上限設計が未記載
- **L-06**: `CacheConfig`/`KafkaConfig` の record 型定義のコード例不足
</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional

**良好な点**:
- `reviews.user_id` がマイクロサービス間参照（FK 制約なし）で ADR-0006 準拠
- 画像メタデータ除去が §7 に記載（位置情報等の個人データ漏洩防止）

**指摘事項**:
- **H-09**: GDPR DSR 対応（レビューの user_id 匿名化）の設計未記載
- **M-16**: サプライヤー個人情報のデータ保持ポリシー未記載
</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 評価: ✅ Approved with Notes

**良好な点**:
- 全 NuGet パッケージが GA バージョン（`-preview`/`-beta` なし）
- AGENTS.md の禁止パッケージ（`Newtonsoft.Json`, `log4net`, `System.Web`）が使用されていない
- `Azure.Storage.Blobs` はドメイン固有の Azure 依存として適切

**指摘事項**:
- **L-05**: `Azure.Identity` のローカル開発時フォールバック設定のガイド不足
</details>

<details>
<summary>release-manager レビューレポート</summary>

### 評価: ✅ Approved with Notes

**良好な点**:
- Dockerfile がマルチステージビルド + 非 root ユーザー（skishop）で AGENTS.md §12.5 準拠
- CI/CD パイプライン（GitHub Actions）が restore → build → test → Docker build → push → deploy の標準フロー
- ベースイメージバージョンが `10.0` で固定（`latest` 不使用）

**指摘事項**:
- **M-17**: テスト失敗時のデプロイ中止条件の明示不足
- **L-07**: `EXPOSE 5003` のハードコード
</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 評価: ✅ Approved with Notes

**良好な点**:
- ヘルスチェック（Liveness `/health` + Readiness `/health/ready`）が定義
- OpenTelemetry によるトレーシング・メトリクスが設定
- Serilog + CompactJsonFormatter による構造化ログ
- Correlation ID ミドルウェアが AGENTS.md §11.3 準拠で実装
- スケーリング戦略（CPU 70% → スケールアウト、min 2 / max 10）が定義

**指摘事項**:
- **M-12**: Dockerfile のポート番号ハードコード
- **M-13**: Kafka ヘルスチェックパッケージの抜け
</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 評価: ✅ Approved with Notes

**良好な点**:
- `price_histories` テーブルによる価格変更の監査証跡
- `outbox_events` テーブルによるイベント発行の追跡
- Correlation ID によるリクエスト横断トレーサビリティ
- `created_by` / `updated_by` カラムによる操作者記録

**指摘事項**:
- **M-14**: 商品情報変更の監査ログ設計の不足
- **M-15**: 在庫引当クリーンアップ時の注文 ID トレーサビリティ欠如
- **L-08**: §15 RTO の ADR-0010 との矛盾
</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 評価: ✅ Approved with Notes

**良好な点**:
- 商品画像に `alt_text` カラムが定義されている（WCAG 1.1.1 対応の基盤）
- API レスポンスにページネーション情報（`hasNext`, `hasPrevious`, `totalPages`）が含まれ、フロントエンド UX を支援

**指摘事項**:
- **L-09**: `alt_text` の NULL 許容が WCAG 準拠の妨げになる可能性
</details>

---

## 総合所見

`inventory-management-design.md` は、SkiShop 在庫管理サービスの設計書として全体的に高品質であり、spec.md / AGENTS.md / ADR との整合性は概ね良好である。特に以下の点が優れている:

1. **Saga 統合設計**: gRPC の proto 定義、実装コード、Deadline 設定が spec.md の Saga ステップ 2 と整合
2. **排他制御設計**: `SELECT FOR UPDATE` による悲観的ロック + 15 分タイムアウト解放の三層防御が明確
3. **Outbox パターン**: `OutboxPublisher` の動的バックオフ、Advisory Lock による多重起動防止が ADR-0005 に完全準拠
4. **コード品質**: C# 14 / .NET 10 の推奨パターン（primary constructor, record, TimeProvider DI）を一貫して使用
5. **データベース設計**: CHECK 制約、部分インデックス、FK 制約の設計が詳細かつ堅牢

主な改善領域は、**SKU/バリエーション管理モデル**（H-01）、**商品 CRUD API の完成**（H-02）、**セキュリティ設計の詳細化**（H-05, H-06）、および **GDPR 対応**（H-09）である。これらの High 指摘の是正により、エンタープライズ品質のドキュメントとして完成度が大幅に向上する。
