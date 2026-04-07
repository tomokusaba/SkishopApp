# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/inventory-management-design.md`
- **判定**: ✅ **Approved with Notes** — 推奨改善事項あり（Critical/High 指摘ゼロ）
- **レビュー日時**: 2026-04-03
- **イテレーション**: 4
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

## 段階的実行モード
- イテレーション: 4 回目
- 実行 Agent: 全 14 Agent（ユーザーから全量レビューの明示的要求）
- スキップ Agent: なし
- 実行理由: ユーザー指定による全量レビュー

## 前回 High 指摘の修正検証

| # | 前回指摘 | 修正状況 | 検証結果 |
|---|---------|---------|---------|
| H-01 | SKU/バリエーションモデル未定義 | Phase 1（JSONB 方式）+ Phase 2（`product_variants` テーブル SQL + 移行パス）を追記 | ✅ **解決** — Phase 分割の設計判断、移行 SQL、移行判断基準が明確 |
| H-02 | PUT/DELETE/ImageUpload API 欠落 | `PUT /api/products/{id}`, `DELETE /api/products/{id}`, `POST /api/products/{id}/images` を API テーブル・実装に追加。論理削除方針を明記 | ✅ **解決** — CRUD 完備、`ProductUpdateRequest` DTO・FluentValidation・Endpoint 実装を含む |
| H-03 | Saga ステップ番号矛盾 | SSOT 注記（spec.md §4 の 9 ステップ定義を正）を §5.1 冒頭に追加 | ✅ **解決** — 矛盾発見時の優先ルールが明確 |
| H-04 | `inventory.product_id` UNIQUE 制約の矛盾 | AppDbContext にフェーズ 1 設計根拠（単一倉庫）と Q1 移行パス（複合ユニークへの変更手順）をコメント記載 | ✅ **解決** — 将来の移行手順が 3 ステップで明記 |
| H-05 | 画像アップロード API + バリデーション未定義 | `ImageUploadRequestValidator`（マジックバイト検証、MIME ホワイトリスト、10MB 上限）とセキュリティ要件を追加 | ✅ **解決** — OWASP ファイルアップロード対策を網羅 |
| H-06 | gRPC 認証・認可の詳細不足 | `InternalServiceOnly` ポリシー、`AuthorizationInterceptor` コード例、トークンキャッシュ TTL（80%）、mTLS 方針を追加 | ✅ **解決** — 多層防御（JWT scope + client_id + mTLS）が明確 |
| H-07 | 低在庫アラート E2E フロー不完全 | トリガータイミング、Mermaid シーケンス図、MailSendService 連携、ダッシュボード API を追加 | ✅ **解決** — 6 ステップの E2E フローが図解付きで完備 |
| H-08 | コンカレンシーテスト未定義 | `InventoryReserveConcurrencyTest`（Testcontainers + Parallel.ForEachAsync 10 並行）とアサーション基準を追加 | ✅ **解決** — 正常系（全引当成功）+ 異常系（在庫不足時の拒否）の 2 テストケース |
| H-09 | `reviews.user_id` DSR 匿名化未記載 | 匿名化ポリシー（`DELETED_USER`）、`UserDeletedConsumer` BackgroundService コード、`user.deleted` イベント購読を追加 | ✅ **解決** — review_responses.responder_id の匿名化も含む |

**結論: 前回 High 9 件は全て適切に修正済み。**

---

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
| レジリエンス | Polly 8.* + Http.Resilience 9.* | Polly 8.* + Http.Resilience 9.* | ✅ |
| ログ | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| 分散トレーシング | OpenTelemetry 1.* | OpenTelemetry 1.* | ✅ |
| ヘルスチェック | AspNetCore.HealthChecks.NpgSql 9.* / Redis 9.* | 同左 | ✅ |
| 画像ストレージ | Azure Blob Storage (Azure.Storage.Blobs 12.*) | Azure Blob Storage | ✅ |
| テスト | xUnit / NSubstitute / Shouldly / Testcontainers | 同左 | ✅ |

## 指摘サマリー
| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ✅ | 0 | 0 | 0 | 1 |
| architect | ⚠️ | 0 | 0 | 2 | 0 |
| tech-lead | ⚠️ | 0 | 0 | 2 | 1 |
| programing-reviewer | ⚠️ | 0 | 0 | 4 | 1 |
| security-reviewer | ✅ | 0 | 0 | 0 | 0 |
| dba-reviewer | ⚠️ | 0 | 0 | 1 | 1 |
| qa-manager | ✅ | 0 | 0 | 0 | 0 |
| performance-reviewer | ⚠️ | 0 | 0 | 2 | 0 |
| compliance-reviewer | ⚠️ | 0 | 0 | 1 | 0 |
| oss-reviewer | ✅ | 0 | 0 | 0 | 1 |
| release-manager | ⚠️ | 0 | 0 | 1 | 1 |
| infra-ops-reviewer | ✅ | 0 | 0 | 0 | 1 |
| audit-reviewer | ⚠️ | 0 | 0 | 2 | 0 |
| ux-accessibility-reviewer | ✅ | 0 | 0 | 0 | 1 |
| **合計** | | **0** | **0** | **15** | **8** |

## 判定根拠
- 判定ルール適用結果: Critical 0 件、High 0 件、Medium 15 件、Low 8 件 → **✅ Approved with Notes**
- 前回 High 9 件が全て適切に修正され、新たな High/Critical 指摘なし
- Medium 指摘は実装フェーズで順次対応可能なレベル

---

## Medium 指摘一覧（改善推奨）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| M-01 | Medium | programing-reviewer | コード品質 | **`OutboxPublisher` / `InventoryReservationCleanupService` 内で `ExecuteSqlRawAsync` を使用**。Advisory Lock 取得に固定文字列 SQL を使用しているが、AGENTS.md で `FromSqlRaw` 系の文字列使用は原則禁止。セキュリティリスクは実質ゼロ（ユーザー入力なし）だが一貫性の観点で改善推奨 | `ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_lock(hashtext({'outbox_publisher'}))") ` に変更するか、Advisory Lock ID を定数パラメータとして渡す |
| M-02 | Medium | programing-reviewer | コード品質 | **`ReserveAsync` で生成する `reservation_id` が永続化されていない**。`Guid.NewGuid().ToString()` で生成して返却するが、DB に保存されていない。`ReleaseAsync` で `reservationId` を引数に取るが照合先がなく、引当特定が不可能 | `inventory_reservations` テーブルを新設するか、`inventory` テーブルに `reservation_id` カラムを追加して永続化する |
| M-03 | Medium | programing-reviewer | コード品質 | **`Inventory` エンティティに `[Timestamp]` / `RowVersion` が未定義**。`Product`, `Category`, `Price` 等には定義済み。`SELECT FOR UPDATE`（悲観的ロック）を使用する設計判断があるが、この設計判断理由が明記されていない | `Inventory` エンティティにも `RowVersion` を追加するか、「`SELECT FOR UPDATE` による悲観的ロックを採用するため `RowVersion` は不要」という設計判断を明記する |
| M-04 | Medium | architect | Saga 整合性 | **`OrderCreatedConsumer`（Kafka）と `InventoryGrpcService`（gRPC）の二重在庫引当リスクが未解決**。両方が `inventoryService.ReserveAsync()` を呼び出す設計のままであり、使い分けの明確な記載がない。Saga ステップ 2 は gRPC 経由だが、`order.created` Consumer でも引当を行うとべき等性保証が必要 | `OrderCreatedConsumer` の役割を明確化する。推奨: Saga 経由（gRPC）のみで在庫引当を行い、`OrderCreatedConsumer` は在庫照会・通知用途に限定する。または不要であれば削除してコードの混乱を防ぐ |
| M-05 | Medium | programing-reviewer | コード品質 | **`UpdateProduct` エンドポイントに FluentValidation が未統合**。`CreateProduct` は `IValidator<ProductCreateRequest>` を注入してバリデーション実行しているが、`UpdateProduct` は `ProductUpdateRequest` のバリデーターを注入していない。Weight の正値検証やフィールド長制限が適用されない | `ProductUpdateRequestValidator` を追加し、`UpdateProduct` エンドポイントに `IValidator<ProductUpdateRequest>` を注入してバリデーション実行する |
| M-06 | Medium | architect | イベント設計 | **`OrderCompletedConsumer` 内の `LowStockAlertEvent` / `StockDepletedEvent` の発行先トピックが不整合**。`PublishInventoryEventAsync` は `inventory.levels` トピックに発行するが、§6 イベント設計テーブルでは `InventoryLow` / `InventoryOutOfStock` は `inventory.alerts` トピックに発行する設計。H-07 の E2E フローも `inventory.alerts` を参照している | `IEventPublisherService` にアラート専用メソッド `PublishAlertEventAsync` を追加するか、`OrderCompletedConsumer` で `PublishAsync` を直接呼び出して `inventory.alerts` トピックを指定する |
| M-07 | Medium | tech-lead | 規約整合性 | **`inventory` テーブル名が単数形**。AGENTS.md / `sql-schema-review.instructions.md` ではテーブル名を snake_case **複数形** と規定。`products`, `categories`, `prices` 等は複数形だが `inventory` のみ単数形 | `inventories` に変更するか、不可算名詞としての例外を spec.md で明記する |
| M-08 | Medium | tech-lead | 設計整合性 | **`SaveChangesAsync` オーバーライドの型不整合**。`var now = _timeProvider.GetUtcNow().UtcDateTime;` は `DateTime` 型を返すが、全エンティティの `CreatedAt`/`UpdatedAt` は `DateTimeOffset` 型。EF Core が暗黙変換するが、タイムゾーン情報が失われるリスク | `var now = _timeProvider.GetUtcNow();` として `DateTimeOffset` のまま使用する |
| M-09 | Medium | performance-reviewer | パフォーマンス | **`SearchProductsAsync` の `Contains` が SQL `LIKE '%keyword%'` に変換されインデックス非活用**。§11 で GIN インデックス（`to_tsvector`）を定義しているが、EF Core のクエリコードが活用していない | `EF.Functions.ToTsVector("simple", ...)` または `FromSqlInterpolated` で `to_tsvector`/`to_tsquery` を使用する実装例に変更する |
| M-10 | Medium | performance-reviewer | パフォーマンス | **`CacheWarmupService` の起動時間上限が未定義**。「上位 100 商品」+「全カテゴリ（最大 1000 件）」をキャッシュに投入するが、大量データ時の所要時間上限やフォールバック（ウォームアップ未完了でもリクエスト受付）の設計がない | 起動時ウォームアップの所要時間上限（例: 30 秒）を設定し、超過時は未完了でもサービス開始する設計を追記 |
| M-11 | Medium | dba-reviewer | NuGet | **§2 NuGet パッケージ一覧に `AspNetCore.HealthChecks.Kafka` が欠落**。§12 のヘルスチェックコード例で `.AddKafka(...)` を使用しているが、パッケージが一覧にない | §2 の主要ライブラリテーブルに `AspNetCore.HealthChecks.Kafka` パッケージを追加する |
| M-12 | Medium | audit-reviewer | 監査 | **商品情報の変更（名前、説明、属性等）の監査証跡が未設計**。価格変更は `price_histories` テーブルで追跡可能だが、商品マスタの変更が追跡できない | 構造化ログによる変更前後の記録（`ILogger` + diff 出力）、または `product_audit_logs` テーブルの設計を検討する |
| M-13 | Medium | audit-reviewer | トレーサビリティ | **`InventoryReservationCleanupService` で期限切れ引当解放時に関連 `OrderId` が不明**。ログに `ProductId`, `ReservedQuantity`, `ReservedAt` は含まれるが、どの注文の引当だったかが追跡不可能 | `inventory` テーブルに `reserved_order_id` カラムを追加するか、`ReserveAsync` で `orderId` を `inventory` テーブルに記録する設計を追加 |
| M-14 | Medium | compliance-reviewer | データ保護 | **サプライヤーテーブルの個人情報（`contact_person`, `email`, `phone`, `address`）のデータ保持期間・削除ポリシー・アクセス制限が未記載**。AdminOnly API は認可設定済みだが、DB レベルのアクセス制御やデータライフサイクルの記載がない | サプライヤー個人情報の保持期間（例: 非アクティブ化後 3 年で匿名化）と AdminOnly アクセス制限を明記する |
| M-15 | Medium | release-manager | CI/CD | **CI/CD パイプラインでテスト失敗時のデプロイ中止条件が不明確**。`dotnet test` ジョブと Docker ビルド・プッシュジョブの依存関係（`needs`）や `if: success()` ガードが明示されていない | GitHub Actions の `needs: build` と `if: success()` を追加し、テスト失敗時にデプロイが進行しないことを明示する |

---

## Low 指摘一覧

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| L-01 | Low | programing-reviewer | コード品質 | `ReleaseReservation` gRPC メソッドに try-catch がない。例外発生時の `Success = false` 応答が未定義（`ReserveInventory` には存在） | `ReleaseReservation` にも try-catch を追加し、失敗時の応答を定義 |
| L-02 | Low | business-analyst | UX | 商品検索 API（`GET /api/products/search`）にソート指定パラメータ（`sortBy`, `sortDir`）がない。商品一覧 API（`GET /api/products`）には存在 | 検索 API にもソートパラメータを追加 |
| L-03 | Low | tech-lead | 表記統一 | §15 運用・保守の RTO が「4 時間以内」と記載されているが、ADR-0010 では RTO 1 時間に統一されている。矛盾が残存 | §15 の RTO を「1 時間以内」に修正（ADR-0010 準拠） |
| L-04 | Low | dba-reviewer | データモデル | `products` テーブルに spec.md で定義されている `price`/`cost` カラムが存在しない差異について、価格テーブル分離の設計判断コメントは追加されたが、`cost` カラムに関する言及がない | spec.md との差異一覧に `cost` カラムの不在理由（仕入原価は将来のサプライヤー管理機能で対応予定等）を明記 |
| L-05 | Low | oss-reviewer | ライセンス | `Azure.Identity 1.*` パッケージのローカル開発時のフォールバック設定（`DefaultAzureCredential` の環境変数 or マネージド ID）が不明確 | `appsettings.Development.json` のガイドを追加 |
| L-06 | Low | infra-ops-reviewer | インフラ | Dockerfile の `EXPOSE 5003` がハードコード。Aspire 環境では動的ポート割り当ての可能性がある | `ASPNETCORE_URLS` 環境変数による動的ポート設定との関係を注記 |
| L-07 | Low | release-manager | バージョニング | §14 Dockerfile の `EXPOSE 5003` と Aspire `WithEndpoint` 設定の一致確認コメントが不足 | Dockerfile に確認用コメントを追加 |
| L-08 | Low | ux-accessibility-reviewer | アクセシビリティ | `product_images.alt_text` が NULL 許容（`VARCHAR(200) NULL`）。WCAG 2.1 SC 1.1.1 では全ての意味のある画像に代替テキストが必要 | `NOT NULL DEFAULT ''` にするか、アプリケーション層で非装飾画像への必須バリデーションを追加 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-01 | 高優先 | architect | **`OrderCreatedConsumer` と `InventoryGrpcService` の二重在庫引当リスク**（M-04）。Saga フロー（gRPC）だけで在庫引当を行うのか、Kafka イベント経由でも行うのかの設計意図の明確化が必要 | テックリード |
| E-02 | 通常 | dba-reviewer | **`inventory` テーブル名の単数形/複数形**（M-07）。`inventory` は不可算名詞の側面があり、`inventories` への改名が適切かの言語的・慣習的判断 | テックリード |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| — | — | — | 本イテレーションでは Agent 間の競合なし | — | — |

---

## ドキュメント横断分析

### spec.md との整合性評価

| 項目 | spec.md の記載 | 設計書の対応 | 整合性 |
|------|-------------|-----------|--------|
| サービス責務 | 商品カタログ管理、在庫追跡、商品属性・カテゴリ管理、価格管理、入出荷、画像管理 | 全項目カバー + PUT/DELETE API 追加 | ✅ |
| 主要エンティティ | Product, Category, Inventory, Supplier, PriceHistory, ProductImage | 全エンティティ + Review, SizeGuide, OutboxEvent を追加 | ✅ |
| バリエーション管理 | サイズ・カラーのバリエーション | Phase 1: JSONB / Phase 2: ProductVariant テーブル（移行計画付き） | ✅ |
| データストア | PostgreSQL + Azure Blob Storage | 一致 | ✅ |
| Aggregate Root | Product, Review, Category | 一致 | ✅ |
| Saga ステップ 2 | 在庫確認・引当（gRPC） | SSOT 注記付きで整合 | ✅ |
| Kafka イベント | InventoryUpdated, InventoryLow 等 | 発行・購読イベント一覧が詳細定義 | ⚠️ トピック名不整合（M-06） |
| FK 制約 | Product→Category: RESTRICT, Inventory→Product: CASCADE | 一致 | ✅ |
| CHECK 制約 | inventory.quantity >= 0, quantity >= reserved_quantity | 一致 | ✅ |
| インデックス | sku UNIQUE, category_id, GIN full-text | 一致 | ✅ |
| 在庫引当排他制御 | `SELECT FOR UPDATE` | 実装コード記載あり | ✅ |
| 15 分タイムアウト解放 | spec.md 三層防御 | `InventoryReservationCleanupService` 実装あり | ✅ |
| Outbox パターン | ADR-0005 準拠 | OutboxPublisher + outbox_events テーブル定義あり | ✅ |
| ヘルスチェック | `/health` + `/health/ready` | 定義あり | ✅ |
| ミドルウェア順序 | AGENTS.md §11.3 準拠 | §12 で順序明示 | ✅ |
| GDPR DSR 対応 | ユーザー削除時の匿名化 | UserDeletedConsumer 実装あり | ✅ |
| gRPC 認証 | Client Credentials + スコープ | InternalServiceOnly ポリシー + mTLS 方針記載 | ✅ |
| 画像アップロード | ファイルバリデーション | マジックバイト検証 + MIME ホワイトリスト + サイズ制限 | ✅ |
| 低在庫アラート | 管理者ストーリー対応 | E2E フロー + Mermaid 図 | ✅ |
| RTO | ADR-0010: 1 時間 | §15 に「4 時間」の記載残存 | ⚠️ L-03 |

### サービス間整合性

| チェック項目 | 結果 | 詳細 |
|------------|------|------|
| gRPC proto 定義 | ✅ | `inventory.proto` の `ReserveInventory` / `ReleaseReservation` が spec.md と一致 |
| Kafka イベント発行整合性 | ⚠️ | `OrderCompletedConsumer` 内の `PublishInventoryEventAsync` が `inventory.levels` に固定発行するが、LowStockAlert / StockDepleted は `inventory.alerts` が正しいトピック（M-06） |
| Kafka イベント購読整合性 | ✅ | `order.created`, `order.completed`, `order.cancelled`, `user.deleted` が spec.md と整合 |
| gRPC Deadline | ✅ | 500ms（spec.md のバジェットと整合） |
| gRPC 認証 | ✅ | InternalServiceOnly ポリシー + AuthorizationInterceptor で保護 |

---

## Iteration 3 → Iteration 4 改善サマリー

| メトリクス | Iteration 3 | Iteration 4 | 変化 |
|-----------|------------|------------|------|
| Critical | 0 | 0 | → |
| High | 9 | 0 | ▼ 9（全件解決） |
| Medium | 22 | 15 | ▼ 7 |
| Low | 9 | 8 | ▼ 1 |
| 判定 | ⚠️ Conditional | ✅ Approved with Notes | ▲ 昇格 |

**Medium 減少の内訳**:
- 解決済み（2 件）: M-04（prices テーブル分離の設計判断記載）、M-05（JSONB メタデータ構造例の拡充）が Low に降格
- 新規（3 件）: M-05（UpdateProduct バリデーション欠落）、M-06（Kafka トピック名不整合）、M-13（OrderId 欠落: 前回 M-15 を継続）
- 統合（1 件）: M-06 と M-16（サプライヤー PII）を M-14 に統合
- High→解決（9 件）: H-01〜H-09 が全件解消し Medium/Low に収束

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### 評価: ✅ Approved with Notes

**良好な点**:
- 商品カタログ管理の CRUD が完備（H-02 修正により PUT/DELETE 追加）
- SKU/バリエーション管理の Phase 1/Phase 2 分割が明確（H-01 修正）
- 低在庫アラートの E2E フローが管理者ペルソナのストーリーを完全カバー（H-07 修正）
- サイズガイド機能はスキー用品 EC サイト特有の差別化要素
- 商品レビュー機能（承認ワークフロー、購入確認済みフラグ、管理者返信）が詳細

**指摘事項**:
- **L-02**: 検索 API のソートパラメータ欠如（商品一覧 API には存在）
</details>

<details>
<summary>architect レビューレポート</summary>

### 評価: ⚠️ Conditional（Medium 2 件）

**良好な点**:
- レイヤードアーキテクチャ（Endpoints → Services → Repositories）が AGENTS.md §2.1 に完全準拠
- Saga ステップ番号の SSOT 注記(H-03 修正)で spec.md との整合性が明確
- gRPC 認証の多層防御（H-06 修正）が堅牢
- Outbox パターン（ADR-0005）の実装が詳細で、動的バックオフ（100ms〜5s）も記載
- Phase 1/Phase 2 の SKU 管理方針（H-01 修正）が移行パス付きで明確

**指摘事項**:
- **M-04**: `OrderCreatedConsumer` と `InventoryGrpcService` の二重在庫引当リスクが未解決
- **M-06**: `OrderCompletedConsumer` 内のアラートイベント発行先トピック不整合
</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 評価: ⚠️ Conditional（Medium 2 件、Low 1 件）

**良好な点**:
- C# 14 の機能活用（primary constructor, record 型, switch 式）が AGENTS.md §4.4 に準拠
- CancellationToken が全 async メソッドに適切に伝搬
- `TimeProvider` DI によるテスト容易性確保
- 追加された `ProductEndpoints` の完全実装が参照パターンとして優れている
- `Program.cs` 統合ビューがサービス全体の構成を俯瞰可能にしている

**指摘事項**:
- **M-07**: テーブル名 `inventory` が単数形（規約は複数形）
- **M-08**: `SaveChangesAsync` での `DateTime` / `DateTimeOffset` 型不整合
- **L-03**: §15 の RTO「4 時間」が ADR-0010「1 時間」と矛盾
</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional（Medium 4 件、Low 1 件）

**良好な点**:
- 追加された `ProductEndpoints`（PUT/DELETE/ImageUpload）が AGENTS.md §6.2 のパターンに準拠
- `ImageUploadRequestValidator` のマジックバイト検証が OWASP ファイルアップロード対策として堅牢
- `InternalServiceOnly` ポリシーと `AuthorizationInterceptor` のコード例が実装可能な品質
- EF Core エンティティの `[Table]`/`[Column]` 属性による snake_case マッピングが全エンティティに適用
- イベント record 型定義が不変性を保証

**指摘事項**:
- **M-01**: `ExecuteSqlRawAsync` での Advisory Lock（セキュリティリスクは低いが規約一貫性の観点）
- **M-02**: `reservation_id` の非永続化
- **M-03**: `Inventory` エンティティの `RowVersion` 未定義（設計判断理由の明記推奨）
- **M-05**: `UpdateProduct` エンドポイントに FluentValidation 未統合（`CreateProduct` との不整合）
- **L-01**: `ReleaseReservation` gRPC メソッドのエラーハンドリング不足
</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 評価: ✅ Approved

**良好な点**:
- H-05 修正: 画像アップロードの FluentValidation（マジックバイト検証、MIME ホワイトリスト、10MB 上限）が堅牢
- H-06 修正: gRPC の `InternalServiceOnly` ポリシー + `AuthorizationInterceptor` + mTLS 方針が多層防御として十分
- H-09 修正: GDPR DSR 匿名化の `UserDeletedConsumer` が review_responses.responder_id も含めて網羅
- REST API のロールベース認可（AdminOnly / AllowAnonymous / RequireAuthorization）が適切
- `SELECT FOR UPDATE` での `FromSqlInterpolated` 使用が SQL インジェクション防止に一貫
- ファイル名を UUID でリネーム（パストラバーサル防止）、EXIF 除去（プライバシー保護）が設計済み
- SAS トークン（読み取り専用、1 時間有効期限、HTTPS のみ）が適切

**指摘事項**: なし
</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional（Medium 1 件、Low 1 件）

**良好な点**:
- H-04 修正: `inventory.product_id` UNIQUE 制約の Phase 1 設計根拠と Q1 移行パスが明確
- H-01 修正: Phase 2 の `product_variants` テーブル SQL が FK 付きで定義済み
- 全テーブルに CHECK 制約が DB レベルで定義（`sql-schema-review.instructions.md` 準拠）
- Outbox テーブルのパーシャルインデックス（`WHERE status = 'PENDING'`）が効率的
- AppDbContext の `HasCheckConstraint` で EF Core マイグレーションに制約が含まれる

**指摘事項**:
- **M-11**: §2 NuGet パッケージ一覧に `AspNetCore.HealthChecks.Kafka` が欠落
- **L-04**: spec.md との `cost` カラム差異の明記不足
</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 評価: ✅ Approved

**良好な点**:
- H-08 修正: `InventoryReserveConcurrencyTest` が Testcontainers + `Parallel.ForEachAsync`（10 並行）で設計済み
- テスト命名が `Should_X_When_Y` パターン、AAA（Arrange-Act-Assert）が全テストケースで遵守
- 正常系・異常系のバランスが良好（商品 CRUD + SKU 重複 + カテゴリ未検出 + キャンセルトークン）
- `InventoryService` のステータス判定テストが `DetermineStatus` メソッドの全分岐をカバー
- カバレッジ目標（Service 80%、Endpoints 80%、Repository 70%）が明記

**指摘事項**: なし（H-08 修正によりコンカレンシーテスト設計が十分）
</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional（Medium 2 件）

**良好な点**:
- Redis キャッシュ戦略（TTL 設計、イベント駆動無効化、ウォームアップ）が体系的
- PostgreSQL インデックス戦略（GIN 全文検索、パーシャルインデックス）が適切
- Kafka Producer 設定（`Acks.All`, `EnableIdempotence = true`）が信頼性重視
- `AsNoTracking()` が読み取り専用クエリに適用済み
- Outbox パターンの動的バックオフ（100ms〜5s）でポーリング負荷を制御

**指摘事項**:
- **M-09**: `SearchProductsAsync` の `Contains` が GIN インデックスを活用していない
- **M-10**: `CacheWarmupService` の起動時間上限未定義
</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional（Medium 1 件）

**良好な点**:
- H-09 修正: レビューの `user_id` 匿名化ポリシー（`DELETED_USER`）と `UserDeletedConsumer` が GDPR DSR に対応
- レビュー本文の残存が「正当な利益」に基づく保持としてGDPR 適法性根拠を記載
- `review_responses.responder_id` の匿名化も含む（管理者ユーザー削除時）
- 画像の EXIF/GPS メタデータ除去がプライバシー保護に寄与

**指摘事項**:
- **M-14**: サプライヤーテーブルの個人情報（`contact_person`, `email`, `phone`, `address`）のデータ保持期間・削除ポリシー・アクセス制限が未記載
</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 評価: ✅ Approved with Notes

**良好な点**:
- NuGet パッケージバージョンが全て安定版（GA）で `-preview`/`-beta`/`-rc` なし
- 禁止パッケージ（`Newtonsoft.Json`, `System.Web`, `log4net`, `EntityFramework` EF6）の使用なし
- `Azure.Storage.Blobs 12.*` と `Azure.Identity 1.*` の追加がプロジェクト要件に合致
- `System.Text.Json` がデフォルト JSON シリアライザとして使用

**指摘事項**:
- **L-05**: `Azure.Identity` のローカル開発時フォールバック設定ガイド不足
</details>

<details>
<summary>release-manager レビューレポート</summary>

### 評価: ⚠️ Conditional（Medium 1 件、Low 1 件）

**良好な点**:
- Dockerfile がマルチステージビルド + 非 root ユーザー + HEALTHCHECK で AGENTS.md §12.5 準拠
- CI/CD パイプラインが `dotnet restore` → `dotnet build` → `dotnet test` → Docker ビルドの順序で構成
- ACR + Azure Container Apps 自動更新が定義済み

**指摘事項**:
- **M-15**: テスト失敗時のデプロイ中止ガード（`needs`/`if: success()`）が不明確
- **L-07**: Dockerfile `EXPOSE 5003` と Aspire `WithEndpoint` 設定の一致確認コメント不足
</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 評価: ✅ Approved with Notes

**良好な点**:
- Docker セキュリティ（非 root ユーザー、SDK 非同梱、固定バージョンタグ）が AGENTS.md §12.5 準拠
- OpenTelemetry 統合（Traces + Metrics）が可観測性要件を満たす
- ヘルスチェック（PostgreSQL + Redis）が `/health` と `/health/ready` で Liveness/Readiness 分離
- Correlation ID ミドルウェアが Serilog LogContext に統合
- セキュリティヘッダー（`X-Content-Type-Options`, `X-Frame-Options`）がインラインミドルウェアに含まれている

**指摘事項**:
- **L-06**: Dockerfile `EXPOSE 5003` のハードコードと `ASPNETCORE_URLS` 環境変数の関係が不明確
</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional（Medium 2 件）

**良好な点**:
- 価格変更の監査証跡（`price_histories` テーブル + `changed_by`）が適切
- Outbox イベントのステータス追跡（`PENDING` → `PUBLISHED` / `FAILED`）が監査可能
- Correlation ID の伝搬が全リクエストに適用
- `UserDeletedConsumer` のログ出力に匿名化件数を含む（GDPR 監査対応）
- 構造化ログ（Serilog + CompactJsonFormatter）がログ集約基盤に適合

**指摘事項**:
- **M-12**: 商品情報変更の監査証跡が未設計（価格変更のみ追跡可能）
- **M-13**: 期限切れ引当解放時の `OrderId` トレーサビリティ欠如
</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 評価: ✅ Approved with Notes

**良好な点**:
- 商品画像の `alt_text` カラムが定義済み（アクセシビリティ対応の基盤）
- サイズガイド機能がサイズ不適合による返品率低減に直結（目標: 返品率 5% 以下）
- ページネーション API が `hasNext`/`hasPrevious` で UX を考慮
- レビュー機能の「参考になった」カウントがユーザー間情報共有を促進

**指摘事項**:
- **L-08**: `alt_text` が NULL 許容。WCAG 2.1 SC 1.1.1 では意味のある画像に代替テキスト必須
</details>
