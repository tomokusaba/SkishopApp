# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/payment-cart-service-design.md`（決済・カートサービス 詳細設計書、1695 行）
- **判定**: ⚠️ **Conditional Approval** — Critical 指摘 0 件、High 指摘 4 件（人間の判断を介在）
- **レビュー日時**: 2026-04-03
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)
- **イテレーション**: 2（前回: check-report-1.md — ❌ Rejected、7 Critical / 25 High）
- **前回修正対応**: fix-report-1.md — 7C 修正済み / 22H 修正済み / 3H エスカレーション

---

## 前回指摘の修正状況

### Critical 指摘（7 件）— 全件修正確認 ✅

| # | 指摘 ID | 指摘概要 | 修正状況 | 検証結果 |
|---|---------|---------|---------|---------|
| 1 | C-01 | Outbox パターン未設計（ADR-0005 違反） | §19 に OutboxPublisher + outbox_events テーブル追加 | ✅ 動的バックオフ（100ms〜5s）、トランザクション統合コード例、Advisory Lock 言及あり |
| 2 | C-02 | Kafka トピック名 spec.md 不整合 | `payment.events` → `payment.completed` / `payment.failed` / `payment.refunded` に分割 | ✅ パーティション数・キー・購読先サービスが spec.md §Kafka トピック設計と一致 |
| 3 | C-03 | エンティティ欠落（Transaction, PaymentMethod） | ER 図・DB スキーマ・EF Core エンティティに追加 | ⚠️ PaymentMethod の EF Core エンティティクラスが §21 に未記載（→ 新規 H-01） |
| 4 | C-04 | PCI DSS 方針矛盾（PaymentIntent vs Hosted Payment Page） | Stripe Checkout（SAQ A）に全面改訂 | ✅ ADR-0008 準拠。シーケンス図、コード例とも Hosted Payment Page ベース |
| 5 | C-05 | Webhook 署名検証コード未記載 | `EventUtility.ConstructEvent()` + `Stripe-Signature` 検証追加 | ✅ `AllowAnonymous()` + 署名検証の組み合わせ明記 |
| 6 | C-06 | TIMESTAMP WITH TIME ZONE 不使用 | 全テーブルの日時カラムを修正 | ✅ 全テーブル確認済み |
| 7 | C-07 | Cart に楽観的ロック未設計 | `row_version BYTEA` + `[Timestamp]` + 409 ハンドリング追加 | ✅ §21 にエンティティ・§21 末尾にハンドリングコード |

### High 指摘（25 件）— 22 件修正済み / 3 件エスカレーション維持

| # | 指摘 ID | 指摘概要 | 修正状況 | 検証結果 |
|---|---------|---------|---------|---------|
| 1 | H-01 | Saga 統合設計欠如 | §20 新設 | ✅ ステップ 1/6/8 の役割・補償操作・冪等性保証を定義 |
| 2 | H-02 | コンポーネント図不整合 | PriceService, DocService, SecurityComponent 追加 | ✅ spec.md と一致 |
| 3 | H-03 | Write-Through パターン未記載 | §26 新設 | ✅ 書込み/読取りフロー、Redis 障害時縮退を記載 |
| 4 | H-04 | API バージョニング不整合 | **エスカレーション維持** | ⚠️ API Gateway 設計者との調整が必要 |
| 5 | H-05 | CHECK 制約未記載 | DB スキーマに追加 | ✅ 全 CHECK 制約が UPPER_CASE 値で定義 |
| 6 | H-06 | ステータス値不整合 | `SUCCEEDED` → `COMPLETED` | ✅ spec.md CHECK 制約準拠 |
| 7 | H-07 | FK 制約名・インデックス未反映 | §8 に追加 | ✅ FK 制約名、UNIQUE 制約、部分インデックス完備 |
| 8 | H-08 | EF Core エンティティ定義なし | §21 新設 | ⚠️ PaymentMethod エンティティクラスが欠落（→ 新規 H-01） |
| 9 | H-09 | Service/Repository インターフェースなし | §22 新設 | ⚠️ IPaymentMethodRepository が欠落（→ 新規 M-08） |
| 10 | H-10 | DTO record 定義なし | §23 新設 | ⚠️ JSON 例（§API 設計）との不整合あり（→ 新規 M-03） |
| 11 | H-11 | IDOR 防止コード例なし | §24 追加 | ✅ ClaimsPrincipal → userId → オーナーシップ検証 |
| 12 | H-12 | レート制限未定義 | §24 追加 | ✅ チェックアウト/カート/返金の具体値あり |
| 13 | H-13 | Webhook 認可設定不明 | AllowAnonymous + 署名検証を明記 | ✅ |
| 14 | H-14 | PII ログマスキング不足 | §24 PII マスキングルール表追加 | ✅ |
| 15 | H-15 | データ保持期間 法的不整合 | 6 ヶ月 → 7 年（電子帳簿保存法）に修正 | ✅ §29 にアーカイブ戦略 |
| 16 | H-16 | テスト戦略が概要のみ | §28 詳細化 | ✅ Should_X_When_Y パターン、AAA 例、カバレッジ 80% |
| 17 | H-17 | Saga テスト未記載 | §28 テストケース一覧に追加 | ✅ |
| 18 | H-18 | カート TTL 不整合 | §26 二段階 TTL 設計 | ✅ Redis 7 日 / PostgreSQL 30 日 |
| 19 | H-19 | BackgroundService 固定間隔 | 注記追加 | ✅ ExpiredCartCleanupService=固定許容、OutboxPublisher=動的必須を明記 |
| 20 | H-20 | `user.deleted` 購読欠落 | 購読イベント一覧に追加 | ⚠️ `user.deletion.completed` 応答が未設計（→ 新規 M-07） |
| 21 | H-21 | cart_items 非正規化根拠なし | 設計根拠コメント追加 | ✅ spec.md §データ複製パターン参照 |
| 22 | H-22 | 価格・税計算ロジック欠如 | §25 新設 | ✅ IPriceService, ITaxCalculator, IShippingFeeCalculator |
| 23 | H-23 | 監査カラム未定義 | payments に created_by/updated_by 追加 | ✅ |
| 24 | H-24 | ゲスト購入 API 未定義 | **エスカレーション維持** | ⚠️ §27 に責務境界概要を記載済み |
| 25 | H-25 | チェックアウトと Saga 責務境界不明 | **エスカレーション維持** | ⚠️ §27 に責務境界表を記載済み |

---

## 技術スタック検証結果

| カテゴリ | 設計書記載 | AGENTS.md / spec.md 定義 | 整合性 |
|---------|-----------|------------------------|--------|
| ランタイム | C# 14 / .NET 10 | C# 14 / .NET 10 | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| オーケストレーション | .NET Aspire 13.1 | .NET Aspire 13.1 | ✅ |
| ORM | EF Core 10 | EF Core 10 | ✅ |
| DB 名 | `paymentcartdb` | `paymentdb`（spec.md L5640, ADR-0006） | ❌ 新規 H-02 |
| メッセージング | Confluent.Kafka 2.* | Confluent.Kafka 2.* | ✅ |
| キャッシュ | StackExchange.Redis 2.* | StackExchange.Redis 2.* | ✅ |
| 認証 | JwtBearer 10.* + Microsoft.Identity.Web 3.* | JwtBearer 10.* + Microsoft.Identity.Web 3.* | ✅ （前回指摘修正済み） |
| 決済 | Stripe.net 46.* | Stripe（spec.md 準拠） | ✅ |
| テスト | xUnit, NSubstitute, Shouldly, Testcontainers | 同一 | ✅ |
| バリデーション | FluentValidation 11.* | FluentValidation 11.* | ✅ |
| 耐障害性 | Polly 8.*, Http.Resilience 9.* | 同一 | ✅ |
| ロギング | Serilog.AspNetCore 8.*, Serilog.Sinks.Console 6.*, Serilog.Formatting.Compact 3.* | 同一 | ✅ （前回指摘修正済み） |
| OpenTelemetry | OTel.Extensions.Hosting 1.*, OTel.Instrumentation.AspNetCore 1.* | 同一 | ✅ （前回指摘修正済み） |
| ヘルスチェック | AspNetCore.HealthChecks.NpgSql 9.*, AspNetCore.HealthChecks.Redis 9.* | 同一 | ✅ |

---

## 指摘サマリー（イテレーション 2）

| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ✅ Approved with Notes | 0 | 0 | 1 | 0 |
| architect | ⚠️ Conditional | 0 | 1 | 1 | 1 |
| programing-reviewer | ⚠️ Conditional | 0 | 1 | 2 | 0 |
| dba-reviewer | ⚠️ Conditional | 0 | 1 | 1 | 0 |
| security-reviewer | ⚠️ Conditional | 0 | 1 | 0 | 0 |
| compliance-reviewer | ✅ Approved with Notes | 0 | 0 | 1 | 0 |
| audit-reviewer | ✅ Approved with Notes | 0 | 0 | 1 | 0 |
| qa-manager | ✅ Approved | 0 | 0 | 0 | 0 |
| performance-reviewer | ✅ Approved with Notes | 0 | 0 | 0 | 1 |
| infra-ops-reviewer | ✅ Approved with Notes | 0 | 0 | 0 | 1 |
| release-manager | ✅ Approved | 0 | 0 | 0 | 0 |
| oss-reviewer | ✅ Approved | 0 | 0 | 0 | 0 |
| ux-accessibility-reviewer | ✅ Approved | 0 | 0 | 0 | 0 |
| tech-lead | ⚠️ Conditional | 0 | 0 | 2 | 1 |
| **合計** | | **0** | **4** | **9** | **4** |

---

## 判定根拠
- **判定ルール適用結果**: Critical 0 件、High 4 件 → **⚠️ Conditional Approval**（人間の判断を介在）
- **前回比改善**: Critical 7→0（-7）、High 25→4（-21）、Medium 30→9（-21）、Low 10→4（-6）
- **前回 Critical 7 件**: 全件修正確認済み
- **前回 High 25 件**: 22 件修正確認済み、3 件はエスカレーション維持（H-04, H-24, H-25）
- **新規指摘**: High 4 件（うち 2 件は前回修正の不完全性、2 件は新規検出）、Medium 9 件、Low 4 件
- **最も重大な指摘**: DB 名不整合（`paymentcartdb` vs `paymentdb`）、PaymentMethod EF Core エンティティ欠落、購読トピック未定義、価格操作リスク

---

## 新規 High 指摘一覧（修正推奨）

| # | 重要度 | 出典 Agent | カテゴリ | 対象箇所 | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|----------|
| H-01 | **High** | programing-reviewer | EF Core エンティティ | §21 | **PaymentMethod の EF Core エンティティクラスが §21 に未記載**。fix-report-1 では「Transaction, PaymentMethod をデータモデル ER 図・DB スキーマ・EF Core エンティティに追加」と記載されているが、§21 の EF Core エンティティ定義には `Cart`, `CartItem`, `Payment`, `Transaction`, `OutboxEvent` の 5 クラスのみで `PaymentMethod` クラスが欠落している。ER 図と DB スキーマには `payment_methods` テーブルが存在するため、実装時にエンティティ定義が不足する | §21 に `PaymentMethod` エンティティクラスを追加する。`[Table("payment_methods")]`、`[Column("snake_case")]`、`[Key]`、`[Required]`、`[MaxLength]` 等の Data Annotations を完備する。spec.md の属性定義（id, userId, type, provider, accountReference, isDefault, expiryDate, billingAddressId, createdAt, updatedAt）に準拠する |
| H-02 | **High** | dba-reviewer | DB 名 | §サービス情報テーブル | **DB 名が spec.md / ADR-0006 と不整合**。設計書では `paymentcartdb` と記載されているが、spec.md の AppHost 設定（L5640）では `postgres.AddDatabase("paymentdb")`、ADR-0006 の結果セクションでも `paymentdb` と定義されている。前回 check-report-1 で `skishopdb` → サービス別独立 DB への修正が指摘され、修正時に `paymentcartdb` と命名されたが、spec.md の定義と不一致 | サービス情報テーブルの DB 名を `paymentcartdb` → `paymentdb` に修正する（spec.md L5640 および ADR-0006 準拠） |
| H-03 | **High** | architect | Kafka 購読トピック | §イベント設計 購読イベント | **`inventory.reserved` および `inventory.reservation.failed` トピックが spec.md の Kafka トピック一覧に存在しない**。設計書の購読イベント表では `InventoryReserved`（トピック: `inventory.reserved`）と `InventoryReservationFailed`（トピック: `inventory.reservation.failed`）を購読しているが、spec.md §Kafka トピック設計の全 19 トピック一覧にこれらは含まれていない。Saga フロー（ADR-0009）で在庫予約の結果通知をこれらのトピックで受け取る設計であれば、spec.md のトピック一覧にも追加が必要 | ① spec.md の Kafka トピック一覧に `inventory.reserved` と `inventory.reservation.failed` を追加するよう提案する（spec.md 側の修正）、または ② Saga フロー（ADR-0009）の通信方式（gRPC 同期 vs Kafka 非同期）を確認し、gRPC 同期であればこれらのトピック購読を削除する |
| H-04 | **High** | security-reviewer | 価格操作リスク | §23 `AddCartItemRequest` | **`AddCartItemRequest` DTO がクライアントからの `UnitPrice` を受け入れており、サーバーサイドの価格検証が未記載**。DTO に `[Required, Range(0.01, 9999999.99)] decimal UnitPrice` が定義されているが、悪意あるクライアントが任意の低額を送信して商品を不正価格で購入するリスクがある。spec.md のシーケンス図では `CartService->>PriceService: 小計計算要求` とあり、価格はサーバーサイドで取得・検証すべきことが示唆されている | ① `AddCartItemRequest` から `UnitPrice`, `ProductName`, `Sku` を削除し、`ProductId` と `Quantity` のみにする。サーバーサイドで InventoryManagementService API を呼び出して商品情報（名前、SKU、単価）を取得・スナップショット保存する設計に変更する、または ② DTO に含める場合は、カート追加時にサーバーサイドで InventoryManagementService に問い合わせて価格を検証するステップをフローに追加し、不一致の場合は拒否する旨を明記する |

---

## 新規 Medium 指摘一覧

| # | 重要度 | 出典 Agent | カテゴリ | 対象箇所 | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|----------|
| M-01 | Medium | tech-lead | 例外ハンドラー | §7 グローバル例外ハンドラー | **`ConcurrencyException → 409` と `ForbiddenException → 403` が例外ハンドラーに未定義**。AGENTS.md §4.7 の例外クラス階層では `ConcurrencyException → HTTP 409`、`ForbiddenException → HTTP 403` が必須。§21 には `DbUpdateConcurrencyException` → `ConcurrencyException` の変換コードがあるが、§7 の `UseExceptionHandler` でこれを 409 にマッピングしていない | §7 のグローバル例外ハンドラーの switch 式に `ConcurrencyException e => TypedResults.Problem(e.Message, statusCode: 409)` と `ForbiddenException => TypedResults.Problem(statusCode: 403)` を追加する |
| M-02 | Medium | tech-lead | RTO | §13 運用・保守 | **RTO 4 時間が spec.md / ADR-0010 と不整合**。§13 で「RPO: 1 時間以内、RTO: 4 時間以内」と記載されているが、spec.md（L5888）および ADR-0010 では全サービス統一で RTO 1 時間と定義されている | `RTO: 4 時間以内` → `RTO: 1 時間以内`（ADR-0010 準拠）に修正する |
| M-03 | Medium | business-analyst | API 設計整合性 | §API 設計 JSON 例 vs §23 DTO | **チェックアウトリクエストの JSON 例と DTO 定義が不整合**。§API 設計の JSON 例には `shippingAddress`, `couponCode`, `usedPoints` フィールドが含まれているが、§23 の `CheckoutRequest` record には `CartId`, `CustomerId`, `PaymentMethod` の 3 フィールドのみ。§27 で責務境界を整理した結果 DTO を簡素化したと推測されるが、JSON 例が更新されていない | §API 設計の JSON 例を §23 の DTO 定義に合わせて更新するか、責務境界の決定待ちである旨の注記を追加する |
| M-04 | Medium | architect | インデックス | §8 インデックス設計 | **`payment_methods` テーブルのインデックスが未定義**。`payment_methods` テーブルはスキーマに存在するが、§8 のインデックス設計表に `payment_methods` 用のインデックス（特に `idx_payment_methods_user_id (user_id)` — ユーザー別決済方法検索）が含まれていない | `payment_methods` テーブルに `idx_payment_methods_user_id (user_id)` インデックスを追加する |
| M-05 | Medium | programing-reviewer | Decimal 精度 | §21 EF Core エンティティ | **EF Core エンティティの decimal プロパティに精度指定がない**。DB スキーマでは `DECIMAL(12,2)` で定義されているが、EF Core エンティティの `Amount`, `UnitPrice`, `Subtotal` プロパティに `[Precision(12, 2)]` または `[Column(TypeName = "decimal(12,2)")]` が指定されていない。EF Core のデフォルト精度と PostgreSQL の精度が異なる場合、マイグレーション時に不整合が発生する | `Cart.Amount`, `CartItem.UnitPrice`, `CartItem.Subtotal`, `Payment.Amount`, `Transaction.Amount` に `[Precision(12, 2)]` 属性を追加する |
| M-06 | Medium | programing-reviewer | OutboxPublisher | §19 OutboxPublisher | **OutboxPublisher がイベントを `PROCESSING` ステータスに遷移させずに直接 `PUBLISHED` に更新している**。`outbox_events` テーブルの CHECK 制約に `PROCESSING` ステータスが定義されているが、OutboxPublisher コードではイベントを `PENDING` から直接 `PUBLISHED` に更新している。複数 OutboxPublisher インスタンスが同時動作した場合（Advisory Lock 取得前の競合等）、同一イベントの重複発行リスクがある | イベント処理前に `Status = "PROCESSING"` + `SaveChangesAsync()` で排他的にステータスを変更し、発行成功後に `PUBLISHED` に更新する 2 段階パターンにする。または Advisory Lock のコード例を具体的に記載し、単一インスタンスのみが動作することを保証する |
| M-07 | Medium | compliance-reviewer | GDPR 削除フロー | §イベント設計 購読イベント | **`user.deleted` 処理後の `user.deletion.completed` イベント応答が未設計**。spec.md §ユーザー削除フロー（L4524-4573）では、各サービスが `user.deleted` を処理した後に `user.deletion.completed` イベントを UserManagementService に返す Saga 構成が定義されている。設計書では `user.deleted` の購読とアクション（カート削除・決済匿名化）は記載されているが、完了通知の発行が欠落している | 購読イベント `user.deleted` のアクション欄に「処理完了後 `user.deletion.completed` イベントを発行」を追加する。発行イベント表にも `user.deletion.completed` を追加する |
| M-08 | Medium | architect | Repository | §22 | **`IPaymentMethodRepository` インターフェースが未定義**。`payment_methods` テーブルと EF Core エンティティ（未記載だが H-01 で追加予定）に対応する Repository インターフェースが §22 に存在しない | §22 に `IPaymentMethodRepository` インターフェースを追加する（`FindByUserIdAsync`, `FindByIdAsync`, `AddAsync`, `SaveChangesAsync`） |
| M-09 | Medium | audit-reviewer | PaymentStatus | §データモデル | **spec.md の `PaymentStatus` エンティティの設計判断が文書化されていない**。spec.md では `PaymentStatus`（id, name, description, isSuccess, isProcessing, isFailed）がエンティティとして定義されているが、設計書では `payments.status` の CHECK 制約で代替している。この設計判断の根拠（ステータスマスターテーブル vs CHECK 制約のトレードオフ）が文書化されていない | データモデルセクションに「`PaymentStatus` エンティティは spec.md で定義されているが、本設計ではステータス値が固定的であるため `payments.status` カラムの CHECK 制約で実装する。将来ステータスの動的追加が必要になった場合はマスターテーブル方式に移行する」旨の設計判断を記載する |

---

## 新規 Low 指摘

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| L-01 | Low | infra-ops-reviewer | Dockerfile | §12 Dockerfile の `HEALTHCHECK` に `--start-period` パラメータがない（前回指摘からの残存。dockerfile-infra.instructions.md 推奨 30s） | `HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3` に修正 |
| L-02 | Low | architect | ER 図 | §4 ER 図の `Customer`, `Order`, `Product` は他サービスのエンティティだが、クロスサービス参照であることが注記されていない | ER 図に「※ `Customer` / `Order` / `Product` は外部サービスのエンティティ。FK 制約なし、結果整合性で担保（ADR-0006）」の注記を追加 |
| L-03 | Low | tech-lead | Markdown | §15 Stripe Webhook コードブロック末尾 | Webhook コードブロックの閉じ ` ``` ` の後に `### Stripe API キー管理` が始まっているが、`### Stripe API キー管理` の直前のコードブロックが閉じられていない可能性がある（マークダウンレンダリング確認推奨） |
| L-04 | Low | performance-reviewer | ExpiredCartCleanupService | §14 | `ExpiredCartCleanupService` は `cart.Status = "EXPIRED"` を設定するが、実際のレコード削除（`Remove()` / `RemoveRange()`）は行っていない。spec.md の「PostgreSQL は 30 日で自動削除」は物理削除を示唆している。論理削除（ステータス変更）なのか物理削除なのか明確にする | `ExpiredCartCleanupService` の削除方式（論理削除 vs 物理削除）を明確化する。論理削除の場合はアーカイブポリシーも定義する |

---

## エスカレーション事項（前回からの引き継ぎ + 更新）

| # | 優先度 | 出典 | 内容 | 対応状況 |
|---|--------|------|------|---------|
| E-01 | ~~最優先~~ | security-reviewer | ADR-0008 Hosted Payment Page vs PaymentIntent の方針 | **✅ 解決済み**: ADR-0008（SAQ A）に準拠して Stripe Checkout に修正 |
| E-02 | 高優先 | architect | checkoutRequest の shippingAddress/couponCode の責務帰属 | ⚠️ 継続。§27 に責務境界表を記載済みだが、Saga フローの詳細設計は未完了 |
| E-03 | ~~高優先~~ | compliance-reviewer | 決済データ保持期間の法的整合性 | **✅ 解決済み**: 7 年保持に修正（電子帳簿保存法準拠） |
| E-04 | ~~通常~~ | performance-reviewer | Redis TTL と PostgreSQL 保持期間の二重管理 | **✅ 解決済み**: §26 に Write-Through パターンと二段階 TTL を設計 |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| — | — | — | 本レビューでは Agent 間の矛盾する指摘は検出されなかった | — | — |

---

## ドキュメント横断分析

### 前回からの改善度

| 指標 | check-report-1 | check-report-2 | 改善率 |
|------|---------------|---------------|--------|
| Critical | 7 | 0 | **-100%** |
| High | 25 | 4（新規） + 3（エスカレーション維持） | **-72%** |
| Medium | 30 | 9 | **-70%** |
| Low | 10 | 4 | **-60%** |
| 合計 | 72 | 17 | **-76%** |
| 行数 | 803 | 1695 | +111%（11 セクション追加） |

### セクションカバレッジ

| セクション | 存在 | 詳細度 | 前回比 |
|-----------|------|--------|--------|
| 概要（§1） | ✅ | 十分 | → |
| 技術スタック（§2） | ✅ | 十分 | ↑ |
| アーキテクチャ図（§3） | ✅ | 十分 | ↑（PriceService, DocService 追加） |
| データモデル ER 図（§4） | ✅ | 概ね十分 | ↑（Transaction, PaymentMethod 追加） |
| DB スキーマ | ✅ | 十分 | ↑（CHECK/FK/インデックス完備） |
| API 設計 | ✅ | 概ね十分 | ↑（ゲスト API §27 追加） |
| イベント設計 | ✅ | 概ね十分 | ↑（Outbox 準拠、トピック名統一） |
| エラーハンドリング（§7） | ✅ | 概ね十分 | → |
| パフォーマンス（§8） | ✅ | 十分 | ↑（Write-Through、部分インデックス） |
| セキュリティ（§9） | ✅ | 十分 | ↑↑（PCI DSS SAQ A 準拠） |
| 監視・ロギング（§10） | ✅ | 十分 | ↑（PII マスキングルール追加） |
| テスト戦略（§11, §28） | ✅ | 十分 | ↑↑（テストケース・カバレッジ目標追加） |
| Dockerfile（§12） | ✅ | 概ね十分 | → |
| 運用・保守（§13） | ✅ | 概ね十分 | ↑（データ保持 7 年修正） |
| Outbox パターン（§19） | ✅ **新設** | 十分 | ↑↑（前回欠如） |
| Saga 統合（§20） | ✅ **新設** | 十分 | ↑↑（前回欠如） |
| EF Core エンティティ（§21） | ✅ **新設** | 概ね十分 | ↑↑（PaymentMethod 欠落あり） |
| Service/Repository IF（§22） | ✅ **新設** | 概ね十分 | ↑↑（IPaymentMethodRepo 欠落あり） |
| DTO 定義（§23） | ✅ **新設** | 十分 | ↑↑ |
| IDOR/レート制限（§24） | ✅ **新設** | 十分 | ↑↑ |
| 価格計算（§25） | ✅ **新設** | 十分 | ↑↑ |
| キャッシュ詳細（§26） | ✅ **新設** | 十分 | ↑↑ |
| ゲスト購入（§27） | ✅ **新設** | 概ね十分 | ↑（詳細は SalesManagement 連携待ち） |
| テスト詳細（§28） | ✅ **新設** | 十分 | ↑↑ |
| データ保持（§29） | ✅ **新設** | 十分 | ↑↑ |

### spec.md との整合性チェック

| チェック項目 | 結果 | 備考 |
|------------|------|------|
| エンティティ一覧（6 個） | ⚠️ | Cart ✅, CartItem ✅, Payment ✅, PaymentMethod ✅（EF Core 欠落）, Transaction ✅, PaymentStatus ⚠️（CHECK 制約で代替、設計判断未文書化） |
| Kafka 発行トピック | ✅ | `payment.completed`(P12), `payment.failed`(P3), `payment.refunded`(P3) — spec.md 完全一致 |
| Kafka 購読トピック | ⚠️ | `order.created` ✅, `order.cancelled` ✅, `user.deleted` ✅, `inventory.reserved` ❌（spec.md に未定義）, `inventory.reservation.failed` ❌（spec.md に未定義） |
| DB 名 | ❌ | `paymentcartdb` ≠ `paymentdb`（spec.md / ADR-0006） |
| ポート | ✅ | 5005（spec.md 一致） |
| Outbox パターン（ADR-0005） | ✅ | outbox_events テーブル + OutboxPublisher 設計あり |
| PCI DSS（ADR-0008） | ✅ | Stripe Checkout（SAQ A）準拠 |
| RFC 9457（ADR-0007） | ✅ | `TypedResults.Problem()` 使用 |
| 独立 DB（ADR-0006） | ⚠️ | 独立 DB 方針は準拠するが DB 名が不整合 |
| RTO（ADR-0010） | ⚠️ | 設計書は 4 時間、ADR-0010 は 1 時間 |
| サービス間 FK 禁止 | ✅ | `order_id`, `customer_id`, `product_id` は FK 制約なし |

---

## 総括

前回レビュー（check-report-1: ❌ Rejected）からの大幅な品質向上が確認できる。Critical 指摘 7 件は全て解消され、High 指摘も 25 件中 22 件が修正された。11 の新規セクション（§19-§29、+892 行）により、Outbox パターン、Saga 統合、EF Core エンティティ、Service/Repository インターフェース、DTO 定義、IDOR 防止、価格計算、テスト詳細等の設計が充実した。

残存する High 4 件は主に修正の不完全性（PaymentMethod エンティティ漏れ、DB 名の誤り）と新規検出（購読トピック未定義、価格操作リスク）である。いずれも設計書内の修正で対応可能であり、次イテレーションでの解消が見込まれる。

エスカレーション事項 4 件中 3 件が解決済みとなり、残る 1 件（E-02: チェックアウト責務境界）はクロスサービスの設計判断が必要である。

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### 判定: ✅ Approved with Notes

**改善確認**:
- H-24（ゲスト購入フロー）: §27 に概要記載。サービス責務境界の整理は適切
- H-25（チェックアウト責務境界）: §27 の責務境界表で PaymentCartService の範囲が明確化

**新規指摘**:
- **M-03**: チェックアウトリクエスト JSON 例と DTO 定義の不整合（§API 設計 vs §23）

**評価**: ビジネス要件のカバレッジは改善。カート操作・決済フロー・返金・ゲスト購入の主要フローが設計済み。価格計算インターフェース（§25）の追加により、配送料・消費税の計算ロジックが明確化された。

</details>

<details>
<summary>architect レビューレポート</summary>

### 判定: ⚠️ Conditional

**改善確認**:
- C-01（Outbox パターン）: ✅ §19 に包括的な設計
- C-02（Kafka トピック名）: ✅ spec.md と完全一致
- C-03（エンティティ追加）: ⚠️ ER 図・スキーマは OK、EF Core エンティティに PaymentMethod 漏れ
- H-01（Saga 統合）: ✅ §20 のステップ定義・補償設計は十分
- H-02（コンポーネント図）: ✅ spec.md と一致
- H-03（Write-Through）: ✅ §26 の設計は適切

**新規指摘**:
- **H-03（新）**: `inventory.reserved` / `inventory.reservation.failed` トピックが spec.md に未定義
- **M-04**: payment_methods テーブルのインデックス未定義
- **L-02**: ER 図のクロスサービスエンティティ注記なし

**評価**: アーキテクチャ設計の完成度は大幅に向上。Outbox パターン、Saga 統合、Write-Through キャッシュの設計が追加され、マイクロサービスアーキテクチャとしての整合性が確保された。残存する購読トピックの整合性問題は Saga フローの通信方式決定に依存する。

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 判定: ⚠️ Conditional

**改善確認**:
- C-07（楽観的ロック）: ✅ `[Timestamp]` + RowVersion + 409 ハンドリング
- H-08（EF Core エンティティ）: ⚠️ 5/6 エンティティ定義済み、PaymentMethod 欠落
- H-09（Service/Repository IF）: ✅ CancellationToken、DI 登録例完備
- H-10（DTO 定義）: ✅ Data Annotations 完備

**新規指摘**:
- **H-01（新）**: PaymentMethod EF Core エンティティクラス欠落
- **M-05**: decimal プロパティに `[Precision(12, 2)]` 未指定
- **M-06**: OutboxPublisher が `PROCESSING` ステータスを使用していない

**コード品質チェック（§14, §19-§24）**:
- ✅ primary constructor 使用
- ✅ CancellationToken 全メソッドに伝搬
- ✅ `ILogger<T>` メッセージテンプレート形式
- ✅ `IServiceScopeFactory` で Scoped サービス取得（BackgroundService）
- ✅ `TimeProvider` DI（OutboxPublisher）
- ✅ `AsNoTracking()` 方針明記
- ✅ `= []` コレクション初期化

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 判定: ⚠️ Conditional

**改善確認**:
- C-06（TIMESTAMP WITH TIME ZONE）: ✅ 全テーブル確認済み
- C-07（RowVersion）: ✅ carts テーブルに `row_version BYTEA`
- H-05（CHECK 制約）: ✅ 全制約が UPPER_CASE 値で定義
- H-06（ステータス値統一）: ✅ COMPLETED に統一
- H-07（FK/インデックス）: ✅ FK 制約名、UNIQUE 制約、部分インデックス完備
- H-23（監査カラム）: ✅ payments に created_by/updated_by

**新規指摘**:
- **H-02（新）**: DB 名 `paymentcartdb` ≠ spec.md `paymentdb`

**DB スキーマ品質チェック**:
- ✅ テーブル名: snake_case 複数形（carts, cart_items, payments, transactions, payment_methods, outbox_events）
- ✅ カラム名: snake_case
- ✅ 日時カラム: `TIMESTAMP WITH TIME ZONE`
- ✅ CHECK 制約値: UPPER_CASE
- ✅ FK 制約名: `fk_` プレフィックス
- ✅ UNIQUE 制約: `(cart_id, product_id)` カート内商品重複防止
- ✅ 部分インデックス: `idx_outbox_events_pending WHERE status = 'PENDING'`
- ✅ 複合インデックス: `idx_carts_customer_status (customer_id, status)`
- ✅ outbox_events テーブル: ADR-0005 準拠
- ⚠️ payment_methods テーブル: user_id インデックスなし
- ⚠️ 非正規化根拠: ✅ 記載済み（cart_items のスナップショット）

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 判定: ⚠️ Conditional

**改善確認**:
- C-04（PCI DSS 矛盾）: ✅ SAQ A 準拠に全面改訂
- C-05（Webhook 署名検証）: ✅ `EventUtility.ConstructEvent()` コード例追加
- H-11（IDOR 防止）: ✅ コード例付きで実装パターン明記
- H-12（レート制限）: ✅ 具体的な制限値とポリシー定義
- H-13（Webhook 認可）: ✅ `AllowAnonymous()` + 署名検証

**新規指摘**:
- **H-04（新）**: AddCartItemRequest のクライアント指定 UnitPrice による価格操作リスク

**セキュリティチェック結果**:
- ✅ PCI DSS SAQ A 準拠（Hosted Payment Page）
- ✅ Webhook 署名検証
- ✅ IDOR 防止パターン
- ✅ レート制限設定
- ✅ Cookie セキュリティ（HttpOnly, Secure, SameSite=Strict）
- ✅ PII ログマスキングルール
- ✅ 秘密情報ハードコード禁止（環境変数 / Key Vault）
- ⚠️ クライアント信頼の価格データ（server-side validation 未記載）

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 判定: ✅ Approved with Notes

**改善確認**:
- H-15（データ保持期間）: ✅ 電子帳簿保存法 7 年準拠
- H-20（user.deleted）: ⚠️ 購読・匿名化処理は記載済みだが完了通知が欠落

**新規指摘**:
- **M-07**: `user.deletion.completed` イベント応答が未設計

**データ保護チェック**:
- ✅ PII マスキングルール定義済み
- ✅ データ保持期間 7 年（電子帳簿保存法）
- ✅ `user.deleted` 受信時の匿名化設計（customer_id ハッシュ化）
- ⚠️ 削除完了通知（`user.deletion.completed`）が未ファイル

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 判定: ✅ Approved with Notes

**改善確認**:
- H-23（監査カラム）: ✅ payments テーブルに created_by/updated_by

**新規指摘**:
- **M-09**: PaymentStatus の spec.md からの設計判断逸脱が文書化されていない

**監査チェック**:
- ✅ 監査カラム（created_by, updated_by）が payments テーブルに追加
- ✅ Correlation ID の言及あり（§10）
- ✅ 構造化ログ（Serilog + ILogger<T>）
- ✅ ADR 参照が正確（ADR-0005, ADR-0006, ADR-0008, ADR-0009）

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 判定: ✅ Approved

**改善確認**:
- H-16（テスト戦略詳細）: ✅ §28 に Should_X_When_Y パターン、AAA 例、カバレッジ 80% 目標
- H-17（Saga テスト）: ✅ テストケース一覧に Saga 補償テストケース追加

**テスト設計チェック**:
- ✅ テストメソッド命名パターン準拠（Should_X_When_Y）
- ✅ AAA パターンのコード例
- ✅ 分岐カバレッジ 80% 目標
- ✅ 必須テストケース一覧（18 ケース: カート 7 + 決済 4 + 返金 2 + セキュリティ 1 + Saga 1 + Webhook 2 + 楽観的ロック 1）
- ✅ テスト種別の明記（Unit / Integration）
- ✅ Stripe テストモード設定

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 判定: ✅ Approved with Notes

**改善確認**:
- H-03（Write-Through）: ✅ §26 に詳細なフロー定義
- H-18（カート TTL）: ✅ Redis 7 日 / PostgreSQL 30 日
- H-19（バックオフ）: ✅ OutboxPublisher 動的バックオフ必須を明記

**新規指摘**:
- **L-04**: ExpiredCartCleanupService の論理削除 vs 物理削除が曖昧

**パフォーマンスチェック**:
- ✅ Write-Through パターン設計
- ✅ Redis 縮退運転方針
- ✅ `AsNoTracking()` 適用方針
- ✅ キーセットページネーション
- ✅ 部分インデックス（outbox_events, carts）
- ✅ バッチサイズ制限（500 件）
- ✅ 動的バックオフ（OutboxPublisher）

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 判定: ✅ Approved with Notes

**新規指摘**:
- **L-01**: Dockerfile HEALTHCHECK に `--start-period` なし（前回残存）

**インフラチェック**:
- ✅ マルチステージビルド
- ✅ 非 root ユーザー（skishop）
- ✅ バージョン固定（aspnet:10.0）
- ✅ HEALTHCHECK 設定あり
- ✅ ヘルスチェック NpgSql + Redis
- ⚠️ `--start-period` パラメータなし（Low）

</details>

<details>
<summary>release-manager レビューレポート</summary>

### 判定: ✅ Approved

**リリースチェック**:
- ✅ CI/CD パイプライン（GitHub Actions）定義
- ✅ Docker イメージビルド・プッシュ
- ✅ Azure Container Apps デプロイ
- ✅ カバレッジ収集

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 判定: ✅ Approved

**OSS チェック**:
- ✅ 全パッケージが AGENTS.md の必須・許可パッケージリストに含まれる
- ✅ Stripe.net（Apache 2.0）— ライセンス適合
- ✅ プレリリースパッケージなし
- ✅ 禁止パッケージ（Newtonsoft.Json, System.Web 等）なし

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 判定: ✅ Approved

**UX チェック**:
- ✅ Cookie ベースのゲストカート体験
- ✅ ログイン時のカートマージ
- ✅ Stripe Checkout リダイレクト UX

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 判定: ⚠️ Conditional

**改善確認**:
- H-20（user.deleted 購読）: ✅ 追加済み
- H-21（非正規化根拠）: ✅ コメント追加済み
- H-22（価格計算）: ✅ §25 にインターフェース定義

**新規指摘**:
- **M-01**: グローバル例外ハンドラーに ConcurrencyException/ForbiddenException 未定義
- **M-02**: RTO 4 時間 ≠ ADR-0010 1 時間
- **L-03**: Webhook コードブロックのマークダウン構造

**総合評価**: 設計書全体の技術標準適合性は大幅に改善。AGENTS.md のコーディング規約（primary constructor、CancellationToken、ILogger<T>、DI 登録）に準拠したコード例が充実。残存する High 4 件は次イテレーションで解消可能な範囲。

</details>
