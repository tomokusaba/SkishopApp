# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/sales-management-design.md`（販売管理サービス詳細設計書）
- **判定**: ❌ **Rejected** — 重大な不備あり
- **レビュー日時**: 2026-04-03
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

---

## 指摘サマリー

| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| architect | ❌ Fail | 3 | 4 | 1 | 0 |
| dba-reviewer | ❌ Fail | 1 | 4 | 3 | 0 |
| security-reviewer | ⚠️ Warn | 0 | 2 | 2 | 0 |
| programing-reviewer | ⚠️ Warn | 0 | 2 | 1 | 0 |
| business-analyst | ⚠️ Warn | 0 | 2 | 1 | 0 |
| performance-reviewer | ⚠️ Warn | 0 | 2 | 1 | 0 |
| qa-manager | ⚠️ Warn | 0 | 1 | 1 | 0 |
| compliance-reviewer | ✅ Pass | 0 | 0 | 1 | 0 |
| infra-ops-reviewer | ⚠️ Warn | 0 | 2 | 1 | 0 |
| release-manager | ⚠️ Warn | 0 | 1 | 0 | 0 |
| oss-reviewer | ⚠️ Warn | 0 | 1 | 0 | 0 |
| audit-reviewer | ⚠️ Warn | 0 | 1 | 1 | 0 |
| ux-accessibility-reviewer | ✅ Pass | 0 | 0 | 0 | 1 |
| tech-lead | ❌ Fail | 3 | 3 | 0 | 0 |
| **合計** | | **7** | **25** | **13** | **1** |

## 判定根拠
- **判定ルール適用結果**: Critical 指摘が 7 件検出されたため、自動的に `❌ Rejected` 判定
- **最も重大な指摘**: Saga オーケストレーションの 9 ステップ設計が spec.md/ADR-0009 と根本的に不整合。Outbox パターン設計が完全に欠落。SagaRecoveryService/OutboxPublisher BackgroundService の設計が欠落

---

## 🚨 Critical 指摘一覧（修正必須・実装ブロッカー）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| C-1 | **Critical** | architect, tech-lead | Saga 設計 | **Saga 9 ステップ構成が spec.md と根本的に不整合**。設計書の §7 Saga シーケンス図は「在庫確認→決済→注文確定」の 3 ステップのみ記載。spec.md（§4 販売管理サービス）および ADR-0009 が定義する 9 ステップ（1.カート取得→2.在庫確認・引当→3.クーポン検証・適用→4.ポイント仮消費→5.注文作成→6.決済認証→7.ポイント確定付与→8.カートクリア→9.Outbox書込み）が反映されていない。特にクーポン検証（Step 3）とポイント仮消費（Step 4）が完全に欠落 | spec.md §4 の 9 ステップ Saga 構成を設計書に完全に反映。各ステップのローカル TX 内容・補償トランザクション・通信プロトコル（gRPC/ローカル/HTTPS）を明記 |
| C-2 | **Critical** | architect, tech-lead | Outbox パターン | **Outbox パターン設計が完全に欠落**。ADR-0005 で採用が決定されている `outbox_events` テーブル設計、`OutboxPublisher` BackgroundService、動的バックオフポーリング（AGENTS.md §10.4: 100ms〜5s、固定間隔 1 秒禁止）の記載が一切ない。イベント発行の信頼性保証が設計されていない | `outbox_events` テーブル定義（status: PENDING/PROCESSING/PUBLISHED/FAILED/DEAD_LETTER）、`OutboxPublisher` BackgroundService 設計（Advisory Lock による排他制御、動的バックオフ）、部分インデックス設計を追加 |
| C-3 | **Critical** | architect, tech-lead | BackgroundService | **SagaRecoveryService の設計が欠落**。spec.md では `SagaRecoveryService`（BackgroundService）が PROCESSING 状態で滞留した Saga を検出し自動再開する設計が定義されているが、設計書に一切記載なし。`SELECT FOR UPDATE SKIP LOCKED` による排他制御、ポーリング間隔 30 秒、5 分タイムアウト超過時の補償トランザクション開始ロジックが未定義 | SagaRecoveryService の動作仕様（検出条件、再開ロジック、タイムアウト処理、排他制御方式）を追加 |
| C-4 | **Critical** | architect | saga_logs テーブル | **`saga_logs` テーブル設計が欠落**。spec.md で定義されている Saga 状態永続化テーブル（id, saga_type, order_id, user_id, status, current_step, step_results(jsonb), started_at, completed_at, timeout_at, retry_count, last_error, row_version）が設計書に含まれていない。Saga の状態管理・障害復旧・監査証跡の基盤が未定義 | spec.md の `saga_logs` テーブル定義を設計書のデータベーススキーマセクションに追加。CHECK 制約（status, saga_type, current_step, retry_count）と部分インデックスも含める |
| C-5 | **Critical** | architect | 注文ステータス | **注文ステータス状態遷移図が欠落**。spec.md（§ビジネスルール詳細定義）では Mermaid stateDiagram-v2 による状態遷移図と遷移ルールテーブル（Pending→Confirmed→Processing→Shipped→Delivered→Returned→Refunded→Cancelled）が定義されているが、設計書に注文ステータスの定義値一覧も遷移ルールも記載なし。`OrderStateMachine` による不正遷移防止の実装指針も未定義 | spec.md の注文ステータス状態遷移図・遷移ルールテーブル・OrderStateMachine 設計を設計書に追加 |
| C-6 | **Critical** | dba-reviewer | idempotency_keys | **べき等性設計（Idempotency-Key）が欠落**。spec.md では `POST /api/v1/orders` に `Idempotency-Key` ヘッダーを必須とし、`idempotency_keys` テーブルによる二重注文防止を定義しているが、設計書に一切記載なし | spec.md の Idempotency-Key 設計（ヘッダー仕様、テーブル定義、処理フロー、TTL 24 時間）を追加 |
| C-7 | **Critical** | architect | gRPC 設計 | **gRPC サービス定義・proto ファイル設計が欠落**。spec.md では Saga ステップ間の通信に gRPC を採用（InventoryService, CouponService, PointService, CartService, PaymentService の proto 定義あり）しているが、設計書は通信プロトコルの記載なし。gRPC Deadline 設計（ステップ別: 200ms〜500ms）も未定義 | spec.md の gRPC proto 定義参照、Saga ステップ別の通信プロトコル・Deadline 設計、SLO Deadline 1,000ms の設計を追加 |

---

## High 指摘一覧（修正強く推奨）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| H-1 | **High** | architect | Kafka トピック名 | **Kafka トピック名が spec.md と不整合**。設計書は `sales.orders`, `sales.shipments`, `sales.returns` を使用しているが、spec.md §Kafka トピック設計では `order.created`, `order.cancelled`, `order.shipped` 等を定義。購読イベント名も `InventoryReserved`, `PaymentProcessed` 等独自の名称を使用しており、spec.md のトピック名（`payment.completed`, `payment.failed` 等）と不一致 | spec.md §Kafka トピック設計のトピック名・パーティション数・キー設計に統一。発行/購読イベント一覧を spec.md 準拠で書き直し |
| H-2 | **High** | dba-reviewer | エンティティ欠落 | **Invoice エンティティが欠落**。spec.md §販売管理サービスのデータモデルでは `Invoice`（請求書情報: id, orderId, invoiceNumber, issuedDate, dueDate, paidDate, amount, status）を主要エンティティとして定義しているが、設計書の ER 図・テーブル定義に含まれていない | Invoice エンティティのスキーマ定義・ER 図への追加。電子帳簿保存法対応の監査要件との整合性も確認 |
| H-3 | **High** | dba-reviewer | エンティティ欠落 | **OrderStatus / ShipmentStatus ルックアップテーブルが欠落**。spec.md では `OrderStatus`（id, name, description, sequenceOrder）と `ShipmentStatus` を独立エンティティとして定義しているが、設計書では orders.status を VARCHAR で直接管理。ステータス値の型安全性・マスタ管理が不十分 | OrderStatus・ShipmentStatus のルックアップテーブル追加、または VARCHAR + CHECK 制約でステータス値を制限 |
| H-4 | **High** | dba-reviewer | データ型 | **TIMESTAMP 型が `WITH TIME ZONE` なし**。sql-schema-review.instructions.md §2 では日時カラムに `TIMESTAMP WITH TIME ZONE` を必須としているが、設計書の全テーブル（orders, order_items, shipments, returns）で `TIMESTAMP` のみ使用。タイムゾーン非対応はグローバル展開時に重大な問題を引き起こす | 全 TIMESTAMP カラムを `TIMESTAMP WITH TIME ZONE` に変更 |
| H-5 | **High** | dba-reviewer | 楽観的ロック | **row_version（楽観的ロック）カラムが欠落**。AGENTS.md §10.3 では楽観的ロックが必要なエンティティに `[Timestamp]` プロパティを追加すると規定。orders テーブル・saga_logs テーブルは同時更新が頻出するため楽観的ロックが必須だが、設計書に row_version カラムの定義なし | orders, shipments, returns テーブルに `row_version BYTEA` カラムを追加。DbUpdateConcurrencyException のハンドリング設計も追加 |
| H-6 | **High** | dba-reviewer | CHECK 制約 | **CHECK 制約が全く定義されていない**。sql-schema-review.instructions.md §3 では DB 層での制約設定を必須としているが、設計書のテーブル定義に CHECK 制約が一切なし。quantity > 0, amount >= 0, status IN (...) 等の基本的なデータ整合性保証が未定義 | spec.md §CHECK 制約一覧に準拠した CHECK 制約を全テーブルに追加 |
| H-7 | **High** | architect | Saga 補償設計 | **補償トランザクション設計が 9 ステップに対応していない**。設計書 §7 の補償トランザクション表は在庫予約・決済・注文確定・配送・ポイントの 5 項目のみだが、spec.md の 9 ステップ Saga ではステップ 1（カート取得: 読取り専用→補償 No-op）、ステップ 3（クーポン検証: ReleaseCoupon）、ステップ 5 失敗時のローカル TX ロールバック後の逆順補償が定義されている | 9 ステップの各ステップに対する補償トランザクション定義を追加。補償の実行順序（逆順）と各ステップの冪等性保証も明記 |
| H-8 | **High** | business-analyst | 配送料計算 | **送料無料閾値が spec.md と不整合**。設計書の `appsettings.json` では `FreeShippingThreshold: 5000`（5,000 円）だが、spec.md §配送料計算ルールでは「合計金額 ≥ 10,000 円（国内）で無料」と定義。会員ランクによる引き下げ（シルバー: 8,000 円、ゴールド: 5,000 円、プラチナ: 全品無料）も未反映 | spec.md の配送料計算ルール（基本 10,000 円 + 会員ランク別引き下げ + 北海道/沖縄/大型商品/お急ぎ便の追加料金）を設計書に反映 |
| H-9 | **High** | business-analyst | 税計算 | **消費税計算ルールが未定義**。spec.md では外税方式・1 円未満切り捨て・`TaxCalculator` サービス・プロダクトマスターの `taxRate` カラムによる税率管理が定義されているが、設計書にはレスポンス例に `taxAmount` が存在するのみで、計算ロジックの設計が欠落 | spec.md の消費税計算ルール（`TaxCalculator` サービス設計、外税方式、切り捨てルール、税率管理方式）を追加 |
| H-10 | **High** | architect | productSnapshot | **OrderItem の productSnapshot JSONB 設計が欠落**。spec.md では `OrderItem.productSnapshot` を PostgreSQL JSONB カラムとして定義し、注文時点の商品スナップショットを不変保持する設計だが、設計書の order_items テーブルは product_name, sku, unit_price 等を個別カラムで保持。スナップショットの不変性保証・スキーマ定義が不十分 | spec.md の productSnapshot JSONB スキーマ（productId, sku, name, brand, categoryName, description, imageUrl, weight, taxRate, attributes, snapshotAt）を設計に反映するか、現行の個別カラム方式を採用する場合はその根拠を ADR として記録 |
| H-11 | **High** | performance-reviewer | Saga タイムアウト | **Saga ステップ別レイテンシバジェット・SLO Deadline 設計が欠落**。spec.md では SLO 95 パーセンタイル 1,000ms 以内、各ステップのバジェット配分（合計 930ms、マージン 70ms）、CancellationTokenSource(1,000ms) による累積 Deadline 強制を定義しているが、設計書に記載なし | spec.md のレイテンシバジェットテーブル、SLO Deadline と Recovery タイムアウト（5 分）の関係性、決済タイムアウト時の PENDING_PAYMENT 状態遷移設計を追加 |
| H-12 | **High** | performance-reviewer | 決済タイムアウト | **決済タイムアウト時の PENDING_PAYMENT 状態遷移が未定義**。spec.md ではステップ 6 の外部 PG タイムアウト時に Saga を PENDING_PAYMENT に遷移させ、SagaRecoveryService が Stripe API をポーリング（30 秒間隔）して決済結果を確認し、成功→再開 / 失敗（or 30 分タイムアウト）→補償を開始する設計が定義されているが、設計書にこの設計が欠落 | PENDING_PAYMENT 状態遷移・SagaRecoveryService による Stripe ポーリング・ユーザーへの即時返却メッセージ設計を追加 |
| H-13 | **High** | security-reviewer | IDOR 防止 | **IDOR（オブジェクトレベル認可）防止設計が不十分**。注文詳細取得（`GET /api/v1/orders/{orderId}`）や顧客注文履歴取得で、ログインユーザーの所有権検証（userId 照合）の設計が明示されていない。AGENTS.md §5.6 では全ユーザー固有リソースに対するオーナーシップ検証を必須としている | 各エンドポイントのアクセス制御ルール（管理者: 全注文参照可 / 一般ユーザー: 自分の注文のみ）を明記。ClaimsPrincipal からの userId 抽出と照合ロジックを設計 |
| H-14 | **High** | security-reviewer | PII ログ | **PII（個人情報）ログ出力防止の設計が不十分**。§11 のログ設計で「エラー発生と例外スタック」をログポイントとして定義しているが、orders テーブルに含まれる配送先住所・電話番号・宛名等の PII がログに出力されないための防止策（マスキング or 除外設計）が未記載。AGENTS.md §5.7 で PII ログ禁止が規定されている | ログ出力時の PII マスキングルール（住所・電話番号・宛名の除外または部分マスク）を §11 ログ設計に追加 |
| H-15 | **High** | infra-ops-reviewer | RTO 不整合 | **RTO が ADR-0010 と不整合**。設計書 §14 では「RTO: 4 時間以内」と記載されているが、ADR-0010 で全サービスに **RTO 1 時間** が統一的に適用されることが承認済み | RTO を 1 時間に修正。ADR-0010 への参照を追加 |
| H-16 | **High** | infra-ops-reviewer | ヘルスチェック | **Readiness エンドポイント `/health/ready` が未定義**。AGENTS.md §11.2 では `/health`（Liveness）と `/health/ready`（Readiness: DB・Redis・Kafka 疎通確認）の両方を実装必須としているが、設計書は `/health` のみ記載 | `/health/ready` エンドポイントの追加。PostgreSQL・Redis・Kafka の疎通確認を含む Readiness チェック設計を追加 |
| H-17 | **High** | programing-reviewer | API 動詞使用 | **`PUT /api/v1/orders/{orderId}/cancel` が REST 設計原則に違反**。api-design.instructions.md §1 では URI に動詞を含めないことを規定。キャンセルは状態変更操作であり、`POST /api/v1/orders/{orderId}/cancel` が適切 | `PUT` を `POST` に変更、または `PUT /api/v1/orders/{orderId}/status` に統一 |
| H-18 | **High** | programing-reviewer | コード例 | **Saga コーディネーターのコード例が欠落**。設計書に SagaCoordinator の実装パターン・ステップ実行ループ・補償実行ロジックのコード例がない。spec.md では OrderCreationSaga のコード例が参照されているが、設計書に具体的な実装指針がない | SagaCoordinator の基本構造（ステップ定義、実行ループ、補償逆順実行、TimeProvider DI、CancellationToken 伝搬）のコード例を追加 |
| H-19 | **High** | release-manager | RTO 整合性 | H-15 と同一。RTO 4 時間は ADR-0010（1 時間統一）違反 | — |
| H-20 | **High** | oss-reviewer | プレリリース版 | **ClosedXML 0.* はプレリリース版**。AGENTS.md §禁止事項では `-preview`, `-beta`, `-rc` パッケージの本番ブランチ含入を禁止。ClosedXML バージョン `0.*` はメジャーバージョン 0 であり、セマンティックバージョニング上 API の安定性が保証されない | ClosedXML を GA バージョンに更新するか、代替ライブラリ（EPPlus 等）を検討。nuget-dependency.instructions.md の禁止パッケージリストとの整合性も確認 |
| H-21 | **High** | audit-reviewer | 監査証跡 | **`created_by` / `updated_by` カラムが欠落**。sql-schema-review.instructions.md §2 では必須監査カラムとして `created_by`, `updated_by` を定義しているが、設計書の全テーブルに含まれていない。特に returns テーブルでは管理者承認操作の証跡が不可欠 | 全テーブルへの `created_by`, `updated_by` カラム追加を検討。少なくとも orders, returns テーブルには必須 |
| H-22 | **High** | architect | user.deleted 購読 | **`user.deleted` Kafka トピックの購読が欠落**。spec.md §Kafka トピック設計では SalesManagementService が `user.deleted` を購読し、ユーザー削除時の注文データ匿名化処理を行う必要があるが、設計書の購読イベント一覧に含まれていない | `user.deleted` トピックの購読と、注文データの匿名化（個人情報のマスキング/削除）処理を設計に追加 |
| H-23 | **High** | architect | Saga ステップ番号 | **Saga ステップ番号が ADR-0009 と不整合**。ADR-0009 は 6 ステップ（Inventory→Payment→Order→Point→Shipment→Outbox）、spec.md の §4 販売管理サービスは 9 ステップ（Cart→Inventory→Coupon→Point→Order→Payment→PointAward→CartClear→Outbox）を定義。設計書は 3 ステップのみ。ADR-0009 と spec.md 自体にもステップ構成の差異があり、SSOT が曖昧 | spec.md §4 の 9 ステップを SSOT（AGENTS.md §10.4 で明記）として設計書に反映。ADR-0009 の 6 ステップ定義との差異は ADR-0009 の追記で解消すべき |
| H-24 | **High** | qa-manager | テスト設計 | **Saga 補償トランザクションのテスト設計が欠落**。テスト戦略（§12）に補償トランザクションの単体テスト・統合テストの記述がない。Saga の各ステップ失敗時の補償実行（逆順・冪等性）は最もテストが重要な箇所 | 補償トランザクションテスト（各ステップ失敗→逆順補償→最終状態検証）、OutboxPublisher テスト、SagaRecoveryService テストの設計を追加 |
| H-25 | **High** | architect | Advisory Lock | **OutboxPublisher の PostgreSQL Advisory Lock 設計が欠落**。spec.md では `minReplicas: 2` での重複実行防止に `pg_try_advisory_lock(hashtext('outbox_publisher'))` を使用する設計が定義されているが、設計書に排他制御の設計なし | Advisory Lock による OutboxPublisher の排他制御設計を追加 |

---

## Medium 指摘一覧（改善推奨）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| M-1 | Medium | dba-reviewer | インデックス | **複合インデックスの選択性順序が未検証**。§9 のインデックス設計で `idx_orders_customer_id` と `idx_orders_status` を個別に定義しているが、顧客×ステータスの複合検索（API: `/api/v1/orders/search`）には `idx_orders_customer_status (customer_id, status)` の複合インデックスが有効。sql-schema-review.instructions.md §4 の選択性順序ルールに従い再設計が必要 | 複合インデックス `(customer_id, status, order_date)` を検討。個別インデックスとの包含関係を整理 |
| M-2 | Medium | dba-reviewer | 外部キー | **外部キーの ON DELETE 動作が未定義**。sql-schema-review.instructions.md §3 では ON DELETE / ON UPDATE の動作を明示することを必須としているが、orders→order_items, orders→shipments, orders→returns の FK に CASCADE/RESTRICT の指定がない | 各 FK の ON DELETE 動作を明示。order_items は CASCADE（注文と不可分）、shipments/returns は RESTRICT を推奨 |
| M-3 | Medium | dba-reviewer | 金額精度 | **金額カラムの精度が不十分な可能性**。設計書は DECIMAL(10,2) を使用しているが、spec.md のデータモデルでは DECIMAL(12,2) を使用。将来の多通貨対応・高額商品を考慮すると DECIMAL(12,2) が適切 | spec.md に合わせて DECIMAL(12,2) に統一 |
| M-4 | Medium | security-reviewer | バリデーション | **リクエスト DTO のバリデーション属性が不完全**。注文作成リクエスト例に `customerId`, `items`, `shippingAddress` 等のフィールドがあるが、FluentValidation / Data Annotations の定義（`[Required]`, `[StringLength]`, コレクション上限等）が設計書に含まれていない | 主要リクエスト DTO のバリデーションルール（必須項目、文字列長上限、コレクション件数上限、金額範囲）を設計 |
| M-5 | Medium | security-reviewer | セキュリティヘッダー | **Program.cs のセキュリティヘッダー設定が不完全**。AGENTS.md §5.3 では `X-Content-Type-Options`, `X-Frame-Options`, `Content-Security-Policy`, `Referrer-Policy`, `Permissions-Policy` を必須としているが、設計書の Program.cs 例に含まれていない | セキュリティヘッダーミドルウェア設定を Program.cs 例に追加 |
| M-6 | Medium | programing-reviewer | DTO 設計 | **配送先住所の ER 図上の表現が不統一**。ER 図の Order エンティティでは `shippingAddress` を単一フィールドとして表記しているが、DB スキーマでは `shipping_postal_code`, `shipping_prefecture`, `shipping_city`, `shipping_address_line1`, `shipping_address_line2`, `shipping_recipient_name`, `shipping_phone_number` に分割。ER 図をスキーマに合わせて更新すべき | ER 図を DB スキーマに準拠して更新。ShippingAddress Value Object の設計も検討 |
| M-7 | Medium | business-analyst | 返品期限 | **返品許可日数の定義元が不明確**。設計書の `appsettings.json` では `AllowedDays: 30` だが、spec.md の注文ステータス遷移ルールでは「配達後 14 日以内」と定義。どちらが正か不明 | spec.md の「配達後 14 日以内」を正とし、appsettings.json を `AllowedDays: 14` に修正 |
| M-8 | Medium | performance-reviewer | キャッシュ設計 | **キャッシュ無効化戦略が未定義**。§9 でキャッシュ TTL は定義されているが、注文ステータス変更時のキャッシュ無効化（Write-Through/Write-Behind）方式が未記載。特に `orders:user:{userId}` キャッシュはステータス変更のたびに古いデータを返すリスクがある | キャッシュ無効化イベント（注文ステータス変更、配送ステータス変更時のキャッシュ削除）を設計 |
| M-9 | Medium | qa-manager | カバレッジ | **テスト戦略が具体性に欠ける**。§12 のテスト戦略は種別とフレームワークの列挙のみで、test-standards.instructions.md が求める分岐カバレッジ 80% 達成のための具体的テストケース一覧・優先テスト対象が未記載 | Service 層の主要メソッドごとのテストケース一覧（正常系 + 異常系）を追加。特に OrderService.CreateOrderAsync の分岐パターンを網羅 |
| M-10 | Medium | compliance-reviewer | データ保持 | **注文データの保持期間・アーカイブポリシーが不十分**。§14 で「古い分析データのアーカイブ（6 ヶ月以上）」と記載されているが、注文データ自体の保持期間（法定保存義務）と個人情報の削除ポリシーが未定義。電子帳簿保存法では最低 7 年の保存が必要 | 注文データの法定保存期間（7 年）、個人情報の匿名化タイミング、アーカイブ戦略を定義 |
| M-11 | Medium | infra-ops-reviewer | Correlation ID | **Correlation ID ミドルウェアの設計が未記載**。AGENTS.md §11.3 では Correlation ID をミドルウェアで付与し、サービス間で伝搬することを必須としているが、設計書に具体的な設計なし | Correlation ID ミドルウェア設計（ヘッダー名、生成ルール、ログコンテキストへの注入）を追加 |
| M-12 | Medium | architect | ミドルウェア順序 | **Program.cs のミドルウェアパイプライン順序が AGENTS.md §11.3 に準拠していない**。設計書のグローバル例外ハンドラーの後に UseAuthentication/UseAuthorization が明示されていない | AGENTS.md §11.3 のミドルウェアパイプライン順序（ExceptionHandler→HSTS→HTTPS→CorrelationId→SerilogRequestLogging→CORS→Authentication→Authorization→RateLimiter→Endpoints）を設計書に明記 |
| M-13 | Medium | audit-reviewer | ADR 参照 | **ADR への参照が不十分**。設計書内で Saga パターン・Outbox パターン・独立 DB 等の設計判断を行っているが、対応する ADR（ADR-0005, ADR-0006, ADR-0009, ADR-0010）への明示的な参照・リンクがない | 各設計判断に対応する ADR 番号を明記。特に Saga 設計は ADR-0009、Outbox は ADR-0005、RTO は ADR-0010 を参照 |

---

## Low 指摘一覧（時間がある時に対応）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| L-1 | Low | ux-accessibility-reviewer | API レスポンス | 注文レスポンス例の日付が `2024-04-15` と古い。プロジェクトの現在時点に合わせて更新推奨 | レスポンス例の日付を現在に近い日付に更新 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-1 | 最優先 | architect | **ADR-0009 と spec.md のステップ構成差異**: ADR-0009 は 6 ステップ、spec.md §4 は 9 ステップを定義。AGENTS.md §10.4 は spec.md を SSOT と指定しているが、ADR-0009 の更新が必要。どちらを正とするか最終決定が必要 | テックリード / アーキテクト |
| E-2 | 高優先 | business-analyst | **配送料無料閾値**: spec.md は基本 10,000 円 + 会員ランク別引き下げだが、設計書は 5,000 円固定。ビジネス要件として正しい閾値の確定が必要 | プロダクトオーナー |
| E-3 | 高優先 | dba-reviewer | **productSnapshot の実装方式**: spec.md は JSONB カラムによるスナップショット保持を定義しているが、設計書は個別カラム方式を採用。JSONB はスキーマレスで柔軟だがクエリ性能がカラム方式に劣る。方式の最終決定が必要 | テックリード / DBA |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|----------|----------|
| — | — | — | 競合なし | — | — |

---

## ドキュメント横断分析

### spec.md との整合性チェック

| 設計要素 | spec.md の定義 | 設計書の状態 | 整合性 |
|---------|--------------|------------|--------|
| Saga 9 ステップ構成 | 9 ステップ（Cart→Inv→Coupon→Point→Order→Payment→PointAward→CartClear→Outbox） | 3 ステップ（Inv→Payment→Order）のみ | ❌ 不整合 |
| 注文ステータス状態遷移図 | Mermaid stateDiagram-v2 + 遷移ルールテーブル | 未定義 | ❌ 欠落 |
| OrderStateMachine | Order.TransitionTo() + BusinessException | 未定義 | ❌ 欠落 |
| saga_logs テーブル | 14 カラム + CHECK 制約 + 部分インデックス | 未定義 | ❌ 欠落 |
| outbox_events テーブル | 共通テーブル + 部分インデックス | 未定義 | ❌ 欠落 |
| OutboxPublisher BackgroundService | Advisory Lock + 動的バックオフ 100ms-5s | 未定義 | ❌ 欠落 |
| SagaRecoveryService BackgroundService | SELECT FOR UPDATE SKIP LOCKED + 30 秒ポーリング | 未定義 | ❌ 欠落 |
| idempotency_keys テーブル | テーブル定義 + Idempotency-Key ヘッダー | 未定義 | ❌ 欠落 |
| gRPC proto 定義 | 5 サービスの proto 定義 | 未定義 | ❌ 欠落 |
| Saga ステップ別 Deadline | 200ms〜500ms | 未定義 | ❌ 欠落 |
| SLO Deadline 1,000ms | CancellationTokenSource(1,000ms) | 未定義 | ❌ 欠落 |
| PENDING_PAYMENT 状態 | 決済タイムアウト時の Saga 状態 | 未定義 | ❌ 欠落 |
| Invoice エンティティ | 主要エンティティとして定義 | 未定義 | ❌ 欠落 |
| Kafka トピック名 | order.created, order.cancelled, order.shipped | sales.orders, sales.shipments, sales.returns | ❌ 不整合 |
| 配送料無料閾値 | 10,000 円（基本） | 5,000 円 | ❌ 不整合 |
| RTO | 1 時間（ADR-0010） | 4 時間 | ❌ 不整合 |
| productSnapshot JSONB | JSONB カラム | 個別カラム | ⚠️ 方式差異 |
| DECIMAL 精度 | DECIMAL(12,2) | DECIMAL(10,2) | ⚠️ 不整合 |
| 配送料計算ルール | ShippingFeeCalculator + 地域別/大型商品/お急ぎ便 | 設定値のみ（ロジック未定義） | ⚠️ 不足 |
| 消費税計算ルール | TaxCalculator + 外税方式 + 切り捨て | 未定義 | ⚠️ 不足 |
| user.deleted 購読 | SalesManagementService が購読 | 購読イベントに含まれていない | ❌ 欠落 |
| 返品期限 | 配達後 14 日以内 | 30 日 | ⚠️ 不整合 |
| TIMESTAMP WITH TIME ZONE | 必須 | TIMESTAMP のみ | ⚠️ 不整合 |
| row_version | 楽観的ロック必須 | 未定義 | ⚠️ 欠落 |
| orders テーブル・配送先住所の構造 | 個別カラム（postal_code, prefecture, city 等） | 個別カラム（一致） | ✅ 整合 |
| orders テーブル・クーポン/ポイントカラム | coupon_code, used_points, point_discount_amount | 一致 | ✅ 整合 |
| エラーハンドリング（RFC 9457） | TypedResults.Problem | 設計書に例外ハンドラー記載あり | ✅ 整合 |
| サービスポート | 5004 | 5004 | ✅ 整合 |
| Dockerfile マルチステージビルド | 必須 | 記載あり（非 root ユーザー含む） | ✅ 整合 |

### 未定義・曖昧な領域（実装ブロッカーリスク）

| # | 領域 | 影響度 | 内容 |
|---|------|--------|------|
| 1 | Saga コーディネーター実装 | **ブロッカー** | 9 ステップの実行ループ・状態管理・補償逆順実行の具体的設計がないため、実装に着手できない |
| 2 | Outbox パブリッシャー | **ブロッカー** | イベント発行の信頼性保証メカニズムが未設計のため、Kafka イベント発行が実装できない |
| 3 | gRPC サービス間通信 | **ブロッカー** | Saga ステップの通信プロトコル・proto 定義がないため、他サービスとの結合設計ができない |
| 4 | 注文ステータス管理 | **高** | ステータス値の定義と遷移ルールがないため、OrderService の状態管理ロジックが実装できない |
| 5 | 配送料/税計算 | **高** | 計算ロジックの仕様がないため、PriceService/TaxCalculator の実装仕様が不明 |

---

## 各 Agent 詳細レポート

<details>
<summary>architect レビューレポート</summary>

### 判定: ❌ Fail (Critical: 3, High: 4, Medium: 1)

**概要**: 販売管理サービスの詳細設計書は、基本的なCRUD API・データモデル・エラーハンドリングは記載されているが、本サービスの中核機能である **Saga オーケストレーション** の設計が spec.md/ADR-0009 と根本的に乖離している。具体的には:

1. **Saga 9 ステップ（C-1）**: spec.md が定義する 9 ステップ Saga のうち、設計書には 3 ステップ（在庫→決済→注文確定）しか記載されていない。クーポン検証（Step 3）とポイント仮消費（Step 4）が完全に欠落しており、カート取得（Step 1）・ポイント確定付与（Step 7）・カートクリア（Step 8）・Outbox 書込み（Step 9）も未記載
2. **Outbox パターン（C-2）**: ADR-0005 で採用決定済みの Outbox パターンが設計書に一切反映されていない。outbox_events テーブル、OutboxPublisher BackgroundService、動的バックオフポーリングの設計が全て欠落
3. **saga_logs テーブル（C-4）**: Saga 状態永続化の基盤テーブルが未定義。SagaRecoveryService（C-3）による障害復旧も設計されていない
4. **gRPC 設計（C-7）**: Saga ステップ間通信の gRPC proto 定義・Deadline 設計が欠落。SLO 1,000ms の強制メカニズムも未定義

**根本原因の推定**: 設計書が spec.md の Saga 設計を十分に参照せずに独自の簡略 Saga を定義した可能性が高い。spec.md §4 の詳細な Saga 設計は設計書作成後に追加された可能性がある。

**推奨**: spec.md §4（販売管理サービス）の全セクションを設計書に反映する包括的な更新が必要。特に Saga オーケストレーション・Outbox パターン・BackgroundService・gRPC 通信設計は実装の前提条件であり、最優先で追加すべき。

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 判定: ❌ Fail (Critical: 1, High: 4, Medium: 3)

**概要**: DB スキーマ設計は基本的なテーブル構造は定義されているが、以下の重大な問題がある:

1. **idempotency_keys テーブル欠落（C-6）**: 二重注文防止の基盤テーブルが未定義
2. **TIMESTAMP WITH TIME ZONE 未使用（H-4）**: 全日時カラムがタイムゾーン非対応
3. **row_version 欠落（H-5）**: 楽観的ロック用バージョンカラムが未定義
4. **CHECK 制約なし（H-6）**: status, quantity, amount 等のデータ整合性制約がDB層で未保証
5. **Invoice テーブル欠落（H-2）**: spec.md で定義されている請求書エンティティが未実装
6. **DECIMAL(10,2) vs DECIMAL(12,2)（M-3）**: spec.md との精度不整合
7. **ON DELETE 動作未定義（M-2）**: FK の削除アクション未指定

また、saga_logs テーブルが architect の指摘（C-4）として欠落しており、outbox_events テーブルも含まれていない。

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 判定: ⚠️ Warn (High: 2, Medium: 2)

**概要**: 基本的なセキュリティ設計（JWT 認証、グローバル例外ハンドラー、Dockerfile の非 root ユーザー）は記載されているが:

1. **IDOR 防止（H-13）**: 注文データへのアクセス時のオーナーシップ検証が明示されていない
2. **PII ログ防止（H-14）**: 配送先住所・電話番号等の PII がログに出力されないための防止策が未記載
3. **バリデーション設計（M-4）**: リクエスト DTO のバリデーション属性の具体的定義がない
4. **セキュリティヘッダー（M-5）**: Program.cs 例にセキュリティヘッダーミドルウェアが含まれていない

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 判定: ⚠️ Warn (High: 2, Medium: 1)

1. **PUT /cancel のREST違反（H-17）**: キャンセル操作は状態変更であり POST が適切
2. **SagaCoordinator コード例欠落（H-18）**: 最も複雑な実装の指針が不足
3. **ER 図とスキーマの不整合（M-6）**: shippingAddress の表現が統一されていない

</details>

<details>
<summary>business-analyst レビューレポート</summary>

### 判定: ⚠️ Warn (High: 2, Medium: 1)

1. **配送料閾値不整合（H-8）**: spec.md の 10,000 円 vs 設計書の 5,000 円
2. **消費税計算未定義（H-9）**: TaxCalculator の設計が欠落
3. **返品期限不整合（M-7）**: spec.md の 14 日 vs 設計書の 30 日

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 判定: ⚠️ Warn (High: 2, Medium: 1)

1. **SLO Deadline 設計欠落（H-11）**: ステップ別レイテンシバジェット未定義
2. **PENDING_PAYMENT 設計欠落（H-12）**: 決済タイムアウトの状態遷移未定義
3. **キャッシュ無効化（M-8）**: ステータス変更時のキャッシュ無効化戦略未設計

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 判定: ⚠️ Warn (High: 1, Medium: 1)

1. **Saga テスト設計欠落（H-24）**: 補償トランザクション・OutboxPublisher・SagaRecoveryService のテスト設計なし
2. **テストケース具体性不足（M-9）**: テスト戦略が種別列挙のみで具体的テストケースがない

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 判定: ✅ Pass (Medium: 1)

1. **データ保持期間（M-10）**: 注文データの法定保存義務（電子帳簿保存法 7 年）と個人情報削除ポリシーの定義が不十分

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 判定: ⚠️ Warn (High: 2, Medium: 1)

1. **RTO 不整合（H-15）**: 4 時間 vs ADR-0010 の 1 時間
2. **Readiness チェック欠落（H-16）**: /health/ready エンドポイント未定義
3. **Correlation ID 設計欠落（M-11）**: サービス間伝搬設計なし

</details>

<details>
<summary>release-manager レビューレポート</summary>

### 判定: ⚠️ Warn (High: 1)

1. **RTO 不整合（H-19）**: H-15 と同一。ADR-0010 違反

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 判定: ⚠️ Warn (High: 1)

1. **ClosedXML 0.* プレリリース（H-20）**: メジャーバージョン 0 は API 安定性未保証。AGENTS.md 禁止事項に抵触する可能性

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 判定: ⚠️ Warn (High: 1, Medium: 1)

1. **監査カラム欠落（H-21）**: created_by/updated_by が全テーブルに未定義
2. **ADR 参照欠落（M-13）**: 設計判断と ADR の紐付けが不明確

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 判定: ✅ Pass (Low: 1)

1. **レスポンス例の日付（L-1）**: サンプル日付が 2024 年と古い

販売管理サービスはバックエンド API であり、UX/アクセシビリティの直接的な観点は限定的。API レスポンス設計は適切。

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 判定: ❌ Fail (Critical: 3, High: 3)

**総合評価**: 販売管理サービスの詳細設計書は、**CRUD 操作レベルの設計としては基本的な品質を満たしている**が、本サービスの最も重要な責務である **注文確定 Saga オーケストレーション** の設計が spec.md と根本的に乖離しており、**このまま実装に着手すると、後で大規模な手戻りが発生する**。

**Critical 指摘（C-1, C-2, C-3）は全て Saga/Outbox/Recovery に関連**しており、これらは販売管理サービスのコア機能である。設計書をこのまま実装ガイドとして使用することは推奨しない。

**修正の優先順序**:
1. **最優先**: Saga 9 ステップ設計の反映（C-1, C-7, H-7, H-11, H-12, H-23）
2. **優先**: Outbox パターン・BackgroundService 設計の追加（C-2, C-3, C-4, H-25）
3. **優先**: 注文ステータス状態遷移・べき等性設計の追加（C-5, C-6）
4. **高**: Kafka トピック名統一・DB スキーマ修正（H-1, H-2〜H-6）
5. **中**: ビジネスルール整合性の修正（H-8, H-9, H-15）

</details>
