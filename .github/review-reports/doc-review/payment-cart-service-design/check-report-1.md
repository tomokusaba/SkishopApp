# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/payment-cart-service-design.md`（決済・カートサービス 詳細設計書）
- **判定**: ❌ **Rejected** — 重大な不備あり（Critical 指摘 7 件検出）
- **レビュー日時**: 2026-04-03 （レビュー実行日）
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

## 技術スタック検証結果
| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| ランタイム | C# 14 / .NET 10 | C# 14 / .NET 10 | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| オーケストレーション | .NET Aspire 13.1 | .NET Aspire 13.1 | ✅ |
| ORM | EF Core 10 | EF Core 10 | ✅ |
| DB | PostgreSQL (skishopdb) | PostgreSQL（サービス別独立 DB） | ⚠️ DB 名が `skishopdb` — ADR-0006 では `paymentdb` 等のサービス別論理 DB |
| メッセージング | Confluent.Kafka 2.* | Confluent.Kafka 2.* | ✅ |
| キャッシュ | StackExchange.Redis 2.* | StackExchange.Redis 2.* | ✅ |
| 認証 | JwtBearer 10.* | JwtBearer 10.* + Microsoft.Identity.Web 3.* | ⚠️ Microsoft.Identity.Web 未記載 |
| 決済 | Stripe.net 46.* | — (spec.md 参照) | ✅ |
| テスト | xUnit, NSubstitute, Shouldly, Testcontainers | xUnit, NSubstitute, Shouldly, Testcontainers | ✅ |
| バリデーション | FluentValidation 11.* | FluentValidation 11.* | ✅ |
| 耐障害性 | Polly 8.*, Http.Resilience 9.* | Polly 8.*, Http.Resilience 9.* | ✅ |
| ロギング | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ (ただし Serilog.Sinks.Console, Serilog.Formatting.Compact 未記載) |
| OpenTelemetry | OpenTelemetry.Extensions.Hosting 1.* | OTel Hosting 1.* + OTel.Instrumentation.AspNetCore 1.* | ⚠️ Instrumentation.AspNetCore 未記載 |

## 指摘サマリー
| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ⚠️ Conditional | 0 | 2 | 3 | 1 |
| architect | ❌ Rejected | 3 | 4 | 2 | 0 |
| programing-reviewer | ❌ Rejected | 1 | 3 | 3 | 1 |
| dba-reviewer | ❌ Rejected | 1 | 3 | 4 | 1 |
| security-reviewer | ❌ Rejected | 2 | 3 | 2 | 0 |
| compliance-reviewer | ⚠️ Conditional | 0 | 2 | 2 | 1 |
| audit-reviewer | ⚠️ Conditional | 0 | 1 | 2 | 1 |
| qa-manager | ⚠️ Conditional | 0 | 2 | 3 | 0 |
| performance-reviewer | ⚠️ Conditional | 0 | 2 | 2 | 1 |
| infra-ops-reviewer | ✅ Approved with Notes | 0 | 0 | 2 | 2 |
| release-manager | ✅ Approved with Notes | 0 | 0 | 1 | 1 |
| oss-reviewer | ✅ Approved with Notes | 0 | 0 | 1 | 1 |
| ux-accessibility-reviewer | ✅ Approved with Notes | 0 | 0 | 1 | 0 |
| tech-lead | ❌ Rejected | 0 | 3 | 2 | 0 |
| **合計** | | **7** | **25** | **30** | **10** |

## 判定根拠
- **判定ルール適用結果**: Critical 指摘が 7 件検出 → 自動 **❌ Rejected**
- **最も重大な指摘カテゴリ**:
  1. Outbox パターン未設計（ADR-0005 違反）
  2. Kafka トピック名が spec.md と不整合
  3. spec.md 定義エンティティ（PaymentMethod, Transaction, PaymentStatus）の欠落
  4. PCI DSS 方針とチェックアウトフロー設計の矛盾（ADR-0008 vs 設計書）
  5. Saga 統合設計の欠如
  6. DB スキーマの TIMESTAMP WITH TIME ZONE 不使用
  7. RowVersion（楽観的ロック）の欠如

---

## 🚨 Critical/High 指摘一覧（修正必須）

### Critical 指摘

| # | 重要度 | 出典 Agent | カテゴリ | 対象箇所 | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|----------|
| C-01 | **Critical** | architect | Outbox パターン | 設計書全体 | **Outbox パターンが一切記載されていない**。ADR-0005 により、全 Kafka イベント発行は `outbox_events` テーブル経由の Outbox パターンで行う必要がある。設計書のイベント設計（§イベント設計）では PaymentProcessed/PaymentFailed/RefundProcessed/CartCheckedOut/CartAbandoned イベントの発行元として Kafka への直接 Publish を前提としているが、DB トランザクションとの原子性が保証されない | `outbox_events` テーブルの定義を追加し、`OutboxPublisher`（BackgroundService）の設計を記載する。各イベント発行箇所を Outbox 経由に修正する。PostgreSQL Advisory Lock によるリーダー選出パターン（spec.md §BackgroundService リーダー選出パターン参照）も記載する |
| C-02 | **Critical** | architect | Kafka トピック | §イベント設計 | **Kafka トピック名が spec.md と不整合**。設計書では `payment.events` と `cart.events` を使用しているが、spec.md §Kafka トピック設計では `payment.completed`（パーティション 12）、`payment.failed`（パーティション 3）、`payment.refunded`（パーティション 3）として個別トピックで定義されている。単一トピック `payment.events` ではパーティション数・キー設計・Consumer Group の分離ができず、spec.md の設計と完全に矛盾する | spec.md のトピック名に統一する（`payment.completed`, `payment.failed`, `payment.refunded`）。`cart.events` についても spec.md に定義がないため、必要であれば spec.md への追加を先に行う。各トピックのパーティション数・レプリカ数・キー・購読先サービスを spec.md と整合させる |
| C-03 | **Critical** | architect | エンティティ | §データモデル / §データベーススキーマ | **spec.md で定義されている主要エンティティが欠落**。spec.md §支払い・カートサービスでは `Cart`, `CartItem`, `Payment`, `PaymentMethod`, `Transaction`, `PaymentStatus` の 6 エンティティが定義されているが、設計書では `Cart`, `CartItem`, `Payment` の 3 エンティティしか記載されていない。`PaymentMethod`（ユーザーの保存済み決済方法）、`Transaction`（決済トランザクション明細）、`PaymentStatus`（ステータスマスター）が欠落している | 3 つの欠落エンティティをデータモデルと DB スキーマに追加する。spec.md の属性定義（`Transaction`: id, paymentId, type, amount, status, gatewayResponse, createdAt, updatedAt 等）に準拠して定義する |
| C-04 | **Critical** | security-reviewer | PCI DSS | §9 セキュリティ対策 / §15 Stripe 連携 | **PCI DSS 方針と設計書のチェックアウトフローが矛盾**。ADR-0008 は **Hosted Payment Page（Stripe Checkout, SAQ A）** を採用決定しているが、設計書 §15 の PaymentIntent ワークフローは **Stripe.js Tokenization（SAQ A-EP）** レベルの実装を記述している。PaymentIntent の `client_secret` をフロントエンドに返し `stripe.confirmCardPayment()` を呼び出すフローは SAQ A-EP に該当し、ADR-0008 の決定と矛盾する | ADR-0008 に準拠し、Stripe Checkout（Hosted Payment Page）方式に設計を修正する。具体的には、`CheckoutSession.Create()` で Stripe 側のチェックアウトページ URL を生成し、クライアントをリダイレクトするフローに変更する。または ADR-0008 を改訂して SAQ A-EP を許容する判断を記録する |
| C-05 | **Critical** | security-reviewer | Webhook 検証 | §15 Stripe 連携 | **Stripe Webhook の署名検証コードが未記載**。Webhook エンドポイント `/api/v1/payments/webhook` は外部から受信する HTTP リクエストであり、Stripe-Signature ヘッダーによる署名検証が必須。署名検証なしでは Webhook のなりすまし攻撃が可能であり、不正な決済完了・返金処理が実行されるリスクがある | `EventUtility.ConstructEvent(json, stripeSignature, webhookSecret)` による署名検証コード例を追加する。`webhookSecret` は環境変数 / Azure Key Vault で管理し、ハードコードを禁止する旨を明記する |
| C-06 | **Critical** | dba-reviewer | DB スキーマ | §データベーススキーマ全テーブル | **全テーブルの TIMESTAMP カラムが `TIMESTAMP WITH TIME ZONE` でない**。sql-schema-review.instructions.md §2 および spec.md §監査カラム必須化ルールにより、日時カラムは `TIMESTAMP WITH TIME ZONE` が必須。設計書では `TIMESTAMP` のみで定義されており、タイムゾーン情報が失われる | 全テーブルの `created_at`, `updated_at`, `expires_at`, `added_at`, `paid_at` を `TIMESTAMP WITH TIME ZONE` に修正する |
| C-07 | **Critical** | programing-reviewer | 楽観的ロック | §データモデル / §データベーススキーマ | **spec.md で定義された楽観的ロック（RowVersion）が Cart エンティティに未設計**。spec.md §楽観的ロック: Cart エンティティは「中: 複数ブラウザタブからの同時カート操作」のリスクがあり `[Timestamp]` + `row_version` カラムが必須。設計書には楽観的ロックへの言及が一切ない | `carts` テーブルに `row_version BYTEA` カラムを追加し、EF Core エンティティに `[Timestamp]` 属性を定義する。`DbUpdateConcurrencyException` のハンドリング（HTTP 409 Conflict マッピング）を例外ハンドラーに追加する |

### High 指摘

| # | 重要度 | 出典 Agent | カテゴリ | 対象箇所 | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|----------|
| H-01 | **High** | architect | Saga 統合 | 設計書全体 | **Saga オーケストレーションへの参画設計が欠如**。ADR-0009 により PaymentCartService は Saga ステップ 1（カート取得）、ステップ 6（決済認証）、ステップ 8（カートクリア）に参画するが、設計書にはこの統合の詳細がない。特にステップ 6 の決済認証の補償トランザクション（Refund）の発動条件・冪等性保証が未定義 | Saga 統合セクションを新設し、PaymentCartService が担う各 Saga ステップの入力・出力・補償操作・冪等性キー・タイムアウトを明記する |
| H-02 | **High** | architect | コンポーネント設計 | §3 コンポーネントアーキテクチャ図 | **spec.md のコンポーネント構成図と不整合**。spec.md では `PriceService`（価格計算）、`DocService`（ドキュメント/領収書生成）、`SecurityComponent`（セキュリティコンポーネント）が存在するが、設計書のコンポーネント図には `PriceService` と `DocService` が欠落している | spec.md のコンポーネント構成図に合わせて、PriceService（税計算・配送料計算）と DocService（領収書/注文確認書生成）を追加する |
| H-03 | **High** | architect | キャッシュ戦略 | §8 キャッシュ戦略 | **Redis キャッシュの Write-Through パターンが未記載**。spec.md ではカートデータに Write-Through パターン（書込み時に PostgreSQL → Redis 更新、読取り時に Redis → Cache Miss 時 PostgreSQL）を明示的に定義しているが、設計書ではキャッシュ戦略に Write-Through への言及がなく、キャッシュ無効化戦略も不明確 | spec.md の Write-Through パターン定義を設計書に反映する。Cache Miss 時のフォールバック、Redis 障害時の縮退運転方針も記載する |
| H-04 | **High** | architect | バージョニング | §API 設計 | **API バージョニングプレフィックス `/api/v1/` が spec.md の URI バージョニング戦略と整合未確認**。設計書では `/api/v1/cart`, `/api/v1/payments` を使用しているが、spec.md のゲスト購入フローでは `/cart/items`, `/checkout/guest` のようにバージョンプレフィックスなしの URI が使用されている | API Gateway 経由のルーティングを考慮し、内部 API パス vs 外部公開パスの対応表を追加する。spec.md のフロー図で使用されている URI との整合性を取る |
| H-05 | **High** | dba-reviewer | CHECK 制約 | §データベーススキーマ | **spec.md で定義された CHECK 制約が設計書に一切記載されていない**。spec.md §支払い・カートサービス CHECK 制約では、`cart_items.quantity CHECK (quantity > 0)`, `carts.status CHECK (status IN (...))`, `payments.amount CHECK (amount > 0)`, `payments.status CHECK (status IN (...))` 等が定義されている | spec.md の全 CHECK 制約を設計書の DB スキーマセクションに転記する |
| H-06 | **High** | dba-reviewer | ステータス値 | §API 設計 / §データベーススキーマ | **決済ステータス値が spec.md と不整合**。spec.md では `PENDING`, `PROCESSING`, `COMPLETED`, `FAILED`, `REFUNDED`, `CANCELLED` と定義。カートステータスは `ACTIVE`, `EXPIRED`, `CHECKED_OUT`, `ABANDONED`。しかし設計書のレスポンス例では決済ステータスに `SUCCEEDED` を使用しており（spec.md では `COMPLETED`）、カートの `CHECKED_OUT`, `ABANDONED` ステータスの遷移も未定義 | ステータス値を spec.md の CHECK 制約に定義された値に統一する。決済ステータス `SUCCEEDED` → `COMPLETED` に修正。カート・決済のステータス遷移図（状態遷移図）を追加する |
| H-07 | **High** | dba-reviewer | FK 制約 / インデックス | §データベーススキーマ | **spec.md で定義された FK 制約名・インデックスが設計書に未反映**。spec.md §FK 制約設計: `fk_cart_items_cart_id (ON DELETE CASCADE)`, §インデックス設計: `carts (user_id, status)` 複合インデックス、`cart_items (cart_id, product_id) UNIQUE` 等が定義されているが、設計書のスキーマには制約名・UNIQUE 制約・複合インデックスの記載がない | spec.md のインデックス設計・FK 制約設計を設計書に反映する。特に `cart_items (cart_id, product_id) UNIQUE`（カート内商品重複防止）は実装必須 |
| H-08 | **High** | programing-reviewer | EF Core エンティティ | §データモデル | **EF Core エンティティクラスの定義がない**。AGENTS.md §10.3 により、全エンティティには `[Table("snake_case")]`, `[Column("snake_case")]`, `[Key]`, `[Required]`, `[MaxLength]` 等の属性が必須。また `DateTime.UtcNow` の使用、コレクションナビゲーションの `= []` 初期化等も必要だが、設計書にはエンティティクラスの定義が一切ない | 最低限 `Cart`, `CartItem`, `Payment` の EF Core エンティティクラス定義を Data Annotations 付きで追加する。AGENTS.md §10.3 のルールに準拠する |
| H-09 | **High** | programing-reviewer | Service/Repository インターフェース | 設計書全体 | **Service/Repository のインターフェース定義がない**。AGENTS.md §2.2 および §10.1 により、各サービスは `ICartService`, `IPaymentService`, `IRefundService` 等のインターフェースと、`ICartRepository`, `IPaymentRepository` 等の Repository インターフェースを定義し、DI コンテナで登録する必要がある | Service インターフェース（メソッドシグネチャ、CancellationToken パラメータ含む）と Repository インターフェースの定義を追加する |
| H-10 | **High** | programing-reviewer | DTOs | §API 設計 | **リクエスト/レスポンス DTO の record 型定義がない**。JSON の例示はあるが、C# record 型 + Data Annotations（`[Required]`, `[StringLength]`, `[Range]` 等）の定義がなく、実装者がバリデーションルールを判断できない | `AddCartItemRequest`, `UpdateCartItemRequest`, `CheckoutRequest`, `RefundRequest` 等のリクエスト DTO record を Data Annotations 付きで定義する。`CartResponse`, `PaymentResponse`, `PaymentDetailResponse`, `RefundResponse` 等のレスポンス DTO record も定義する |
| H-11 | **High** | security-reviewer | IDOR 防止 | §9 セキュリティ対策 | **IDOR 防止の具体実装が不足**。設計書ではオーナーシップ検証に言及しているが、コード例がない。決済エンドポイント `GET /api/v1/payments/{paymentId}` で他ユーザーの決済情報が取得可能な脆弱性リスクがある | AGENTS.md §5.6 準拠の IDOR 防止コード例を追加する。`ClaimsPrincipal` から `userId` を取得し、Payment の `customerId` と照合するロジックを各エンドポイントに記載する |
| H-12 | **High** | security-reviewer | レート制限 | §9 セキュリティ対策 | **レート制限の具体的な設定が未定義**。「決済エンドポイントに対する厳格なレート制限」と記載があるが、対象エンドポイント・制限値（例: チェックアウト: 10 回/分/ユーザー）・制限方式（Fixed Window / Sliding Window）が未定義 | 各エンドポイントのレート制限ポリシー（対象、制限値、Window 方式）を定義し、`AddRateLimiter` の設定例を記載する |
| H-13 | **High** | security-reviewer | Webhook セキュリティ | §API 設計 | **Stripe Webhook エンドポイント `/api/v1/payments/webhook` の認可設定が不明**。Webhook は Stripe サーバーから呼ばれるため `AllowAnonymous()` + 署名検証が必要だが、設計書の API 表には認可設定の記載がない | Webhook エンドポイントの認可設定（`AllowAnonymous()` + `Stripe-Signature` 検証 + IP 制限の検討）を明記する |
| H-14 | **High** | compliance-reviewer | PII ログ | §10 監視とロギング | **ログ設計で PII 保護ルールが不十分**。「クレジットカード番号・CVV をログに出力しない」と記載があるが、顧客 ID・メールアドレス・住所等の PII マスキングルールが未定義。AGENTS.md §4.2 禁止事項: 「ログへの個人情報出力」 | 決済サービス固有の PII マスキングルールを定義する（customerId は出力可、email/address はマスキング、Stripe ID は出力可等） |
| H-15 | **High** | compliance-reviewer | データ保持 | §13 運用・保守 | **決済データの保持期間が法的要件と整合性未確認**。§13 では「古い決済データのアーカイブ（6 ヶ月以上）」と記載があるが、電子帳簿保存法では取引記録の 7 年間保存が必要。また GDPR/個人情報保護法のデータ最小化原則との整合性が未検討 | 決済データの保持期間を法的要件（電子帳簿保存法 7 年、税法要件）と照合し、アーカイブ戦略を修正する。PII を含むデータの匿名化基準も定義する |
| H-16 | **High** | qa-manager | テスト戦略 | §11 テスト戦略 | **テスト戦略が概要レベルのみで具体性が不足**。テスト対象・フレームワークの記載はあるが、テストメソッド命名パターン（`Should_X_When_Y`）、AAA パターン、分岐カバレッジ 80% 目標（test-standards.instructions.md 準拠）への言及がない | テストクラス・メソッドの具体例を追加する。特にカート操作（追加/更新/削除/有効期限切れ）の正常系・異常系、決済成功/失敗/タイムアウト、返金処理のテストケース一覧を記載する |
| H-17 | **High** | qa-manager | Saga テスト | §11 テスト戦略 | **Saga 補償トランザクションのテスト戦略がない**。spec.md §テスト戦略: 「チェックアウト Saga の各ステップで障害が発生した場合の補償トランザクションが正しく動作することを検証する」と定義されているが、設計書にはこの種のテストケースが皆無 | Saga 参画時の補償テストケースを追加する（例: 決済成功後にポイント付与が失敗 → 決済 Refund が呼ばれることを検証） |
| H-18 | **High** | performance-reviewer | カート TTL | §8 / §14 | **カート TTL の設計がスペックと不整合**。spec.md では「未ログインカートは Redis TTL 7 日、PostgreSQL は 30 日で自動削除」と定義。設計書 §14 の `ExpiredCartCleanupService` では `ExpiresAt < DateTime.UtcNow` でチェックしているが、Redis TTL 7 日 vs PostgreSQL 30 日の二段階 TTL 設計が反映されていない | spec.md のカート TTL 設計（Redis 7 日 / PostgreSQL 30 日）を設計書に反映する。`ExpiredCartCleanupService` のロジックを PostgreSQL 30 日削除に修正する |
| H-19 | **High** | performance-reviewer | バックオフ | §14 `ExpiredCartCleanupService` | **BackgroundService のポーリング間隔が固定 1 時間**。AGENTS.md §10.4: 「Outbox Polling: 動的バックオフ（100ms〜5s）。固定間隔 1 秒は禁止」のルールが BackgroundService 全般に適用される。`ExpiredCartCleanupService` は `Task.Delay(TimeSpan.FromHours(1))` で固定間隔 | `ExpiredCartCleanupService` は定期バッチのため固定間隔は許容されうるが、`OutboxPublisher`（未設計の C-01 対応）には動的バックオフが必須である旨を明記する |
| H-20 | **High** | tech-lead | 購読イベント | §イベント設計 | **`user.deleted` イベントの購読が欠落**。spec.md §Kafka トピック設計: `user.deleted` トピックの購読先サービスに PaymentCartService が含まれているが、設計書の購読イベント一覧に `user.deleted` がない。GDPR/個人情報保護法のデータ削除要件に影響する | `user.deleted` イベントの購読とアクション（該当ユーザーのカートデータ・決済データの匿名化/削除）を追加する |
| H-21 | **High** | tech-lead | 正規化 | §データベーススキーマ cart_items | **cart_items テーブルの非正規化フィールドに設計根拠がない**。`product_name`, `sku`, `unit_price` は products テーブル（InventoryManagementService）のデータを複製している。sql-schema-review.instructions.md §1: 「意図的な非正規化を行う場合は、パフォーマンス上の根拠をコメントで明記する」 | 非正規化の根拠（例: マイクロサービス間 JOIN 不可のため、カート追加時点のスナップショットを保持）をコメントとして明記する。spec.md §クロスサービス参照時の API 問い合わせ設計の「データ複製（Read Model）」パターンに該当することを記載する |
| H-22 | **High** | tech-lead | 価格・税計算 | 設計書全体 | **価格計算・配送料計算・消費税計算のロジックが欠如**。spec.md では `ShippingFeeCalculator`, `TaxCalculator` サービスが定義され、配送料ルール（10,000 円以上無料等）、消費税計算ルール（外税方式、1 円未満切り捨て）が詳細に記述されている。設計書にはこれらのロジックへの言及がない | 価格計算セクションを新設し、spec.md §配送料・消費税計算ルールを参照して `PriceService`/`TaxCalculator`/`ShippingFeeCalculator` のインターフェースと主要ロジックを定義する |
| H-23 | **High** | audit-reviewer | 監査証跡 | §データベーススキーマ | **`created_by` / `updated_by` 監査カラムが未定義**。sql-schema-review.instructions.md §2 必須カラム: `created_by`, `updated_by` を「必要に応じて」含めると定義されているが、決済テーブルは監査要件が高く、操作者の記録が必要 | `payments` テーブルに `created_by`, `updated_by` カラムを追加する。管理者による手動返金操作等の監査に必要 |
| H-24 | **High** | business-analyst | ゲスト購入 | §API 設計 | **ゲスト購入フローの API が未定義**。spec.md §ゲスト購入フローでは `POST /checkout/guest` エンドポイントと、ゲスト注文作成のフローが詳細に定義されているが、設計書の API 一覧にゲスト用チェックアウト API がない | spec.md のゲスト購入フロー設計に準拠したエンドポイント（ゲストチェックアウト、ゲスト注文追跡等）を API 設計に追加する |
| H-25 | **High** | business-analyst | カート→注文フロー | §API 設計 / §イベント設計 | **チェックアウト API（`POST /api/v1/payments/checkout`）のフローと Saga フローの関係が不明確**。チェックアウトリクエストに `shippingAddress` や `couponCode` が含まれているが、これらは SalesManagementService が管理すべき情報。PaymentCartService の責務範囲が曖昧 | チェックアウトフロー全体を Saga ステップに沿って整理し、PaymentCartService が担当する部分（カート情報の提供・決済処理）とSalesManagementService が担当する部分（注文作成・配送手配）の責務境界を明確にする |

---

## Medium/Low 指摘サマリー

### Medium 指摘（30 件）

| カテゴリ | 指摘概要 | 出典 Agent |
|---------|---------|-----------|
| API 設計 | API パスに `/api/v1` プレフィックスとバージョニング戦略の明示が不十分 | programing-reviewer |
| API 設計 | `POST /api/v1/payments/checkout` のレスポンスに `Location` ヘッダーの記載なし（201 Created 時の規約） | programing-reviewer |
| API 設計 | ページネーションの `page` パラメータが 1-based か 0-based か未定義 | programing-reviewer |
| DB スキーマ | `carts.session_id` に UNIQUE 制約の有無が未定義 | dba-reviewer |
| DB スキーマ | `payments.order_id` の UNIQUE 制約が設計書にあるが、返金時の複数決済レコードとの関係が未整理 | dba-reviewer |
| DB スキーマ | `cart_items.subtotal` は計算値であり非正規化。更新時の整合性保証メカニズムが未記載 | dba-reviewer |
| DB スキーマ | 部分インデックス（`idx_carts_expired`）が spec.md に定義されているが設計書に未反映 | dba-reviewer |
| セキュリティ | Cookie の `MaxAge = 7 日` は長すぎる可能性（セッションハイジャックリスク）。spec.md の TTL と整合しているが、リスク評価が未記載 | security-reviewer |
| セキュリティ | `CheckoutRequest` の `shippingAddress` フィールドにバリデーションルール（住所長、電話番号形式等）が未定義 | security-reviewer |
| データ保護 | `user.deleted` イベント受信時のデータ匿名化処理の設計が欠如（GDPR 対応） | compliance-reviewer |
| データ保護 | ゲスト購入時のメールアドレス暗号化保存（AES-256）の仕組みが設計書に未反映 | compliance-reviewer |
| 監査 | Correlation ID のミドルウェア設定コード例がない | audit-reviewer |
| 監査 | 決済操作（チェックアウト・返金）の監査ログ出力設計が不足 | audit-reviewer |
| テスト | Stripe テスト用の Mock/Stub 戦略が不十分（Stripe.net をどの層で Mock するか） | qa-manager |
| テスト | セキュリティテスト（認可テスト・IDOR テスト）のケース定義がない | qa-manager |
| テスト | Edge case テスト（0 円決済、最大金額、通貨変換等）が未定義 | qa-manager |
| パフォーマンス | Redis キャッシュの TTL と PostgreSQL データの TTL の二重管理による整合性リスクの検討が不足 | performance-reviewer |
| パフォーマンス | `AsNoTracking()` の適用方針が読み取り専用クエリに対して未明示 | performance-reviewer |
| インフラ | Dockerfile の `HEALTHCHECK` に `--start-period` パラメータがない（dockerfile-infra.instructions.md では推奨 30s） | infra-ops-reviewer |
| インフラ | `EXPOSE 5005` — コンテナ内部ポートは 8080 が推奨（Kestrel デフォルト。ただし spec.md ではポート 5005 と定義されておりどちらが正か要確認） | infra-ops-reviewer |
| ビジネス | カートの最大アイテム数上限（`MaxItemsPerCart: 50`）の根拠とバリデーションエンドポイントでの検証コードが未記載 | business-analyst |
| ビジネス | 「返金処理」の部分返金・全額返金の区別が未定義 | business-analyst |
| ビジネス | クーポン適用時のカート合計再計算フローが未記載 | business-analyst |
| リリース | CI/CD パイプラインでのカバレッジ 80% ゲートが未設定 | release-manager |
| OSS | `Stripe.net 46.*` のライセンス（Apache 2.0）確認と脆弱性チェック方針が未記載 | oss-reviewer |
| UX | Webhook 処理完了後のフロントエンド通知方式（WebSocket / Polling / Redirect）が未定義 | ux-accessibility-reviewer |
| DDD | `CartItem` が Value Object として定義可能だが、独自の `id` PK を持つ Entity として設計されている。DDD 観点での設計判断の根拠がない | architect |
| DDD | `Money` Value Object（金額 + 通貨コード）が未定義。`DECIMAL(10,2)` の金額フィールドが散在しており、通貨計算の安全性が未保証 | architect |
| ミドルウェア | Program.cs のミドルウェアパイプライン順序（AGENTS.md §11.3）が未記載 | tech-lead |
| 設定管理 | `appsettings.json` の `App.Cart.ExpiryDays` 等のカスタム設定に対応する `IOptions<T>` 設定クラスが未定義 | tech-lead |

### Low 指摘（10 件）

| カテゴリ | 指摘概要 | 出典 Agent |
|---------|---------|-----------|
| 命名規則 | ER 図の属性名が PascalCase（C# 寄り）だがDB スキーマは snake_case。統一方針の明示がない | dba-reviewer |
| ドキュメント構造 | セクション番号が §4 から §7 にジャンプしている（§5, §6 が欠番） | audit-reviewer |
| ドキュメント構造 | §サービス情報テーブルが §4 と §データベーススキーマの間に挿入されており、ドキュメント構造が不自然 | business-analyst |
| 技術スタック | Serilog.Sinks.Console, Serilog.Formatting.Compact がライブラリ一覧に未記載 | oss-reviewer |
| 技術スタック | OpenTelemetry.Instrumentation.AspNetCore がライブラリ一覧に未記載 | programing-reviewer |
| CI/CD | Docker イメージタグ戦略（`${{ github.sha }}` のみ。セマンティックバージョニングタグの併用推奨） | release-manager |
| RPO/RTO | §13 の RPO 1 時間 / RTO 4 時間が spec.md の RTO 1 時間（ADR-0010）と不整合の可能性 | infra-ops-reviewer |
| Mermaid 図 | §15 PaymentIntent ワークフローの Mermaid 図に `ExternalPayment` 参加者の宣言漏れ（spec.md のシーケンス図不整合） | infra-ops-reviewer |
| 将来計画 | §16 「暗号通貨決済対応」は法的・規制リスクが高く、計画に含めるべきか要検討 | compliance-reviewer |
| BackgroundService | `ExpiredCartCleanupService` で大量の期限切れカートを一括取得しているが、バッチサイズ制限がない | performance-reviewer |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-01 | **最優先** | security-reviewer | ADR-0008 は Hosted Payment Page (SAQ A) を決定しているが、設計書は PaymentIntent (SAQ A-EP) で設計されている。どちらを正とするか | テックリード + セキュリティチーム |
| E-02 | **高優先** | architect | `POST /api/v1/payments/checkout` の `shippingAddress` / `couponCode` は SalesManagementService の責務か PaymentCartService の責務か。Saga フローにおけるサービス責務境界の最終決定が必要 | テックリード + ドメインエキスパート |
| E-03 | **高優先** | compliance-reviewer | 決済データの保持期間（設計書: 6 ヶ月 vs 電子帳簿保存法: 7 年）の法的整合性確認 | 法務チーム |
| E-04 | **通常** | performance-reviewer | Redis キャッシュ TTL（7 日）と PostgreSQL 保持期間（30 日）の二重管理の運用可否 | インフラチーム |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| — | — | — | 本レビューでは Agent 間の矛盾する指摘は検出されなかった | — | — |

---

## ドキュメント横断分析

### マイクロサービス設計書カバレッジ

| セクション | 存在 | 詳細度 | 備考 |
|-----------|------|--------|------|
| 概要 | ✅ | 十分 | |
| 技術スタック | ✅ | 概ね十分 | 一部パッケージ漏れ |
| アーキテクチャ図 | ✅ | 部分的 | PriceService/DocService 欠落 |
| データモデル (ER 図) | ✅ | 不足 | 3/6 エンティティ欠落 |
| DB スキーマ | ✅ | 不足 | CHECK 制約・FK 制約名・インデックス欠落 |
| API 設計 | ✅ | 概ね十分 | ゲストフロー・バリデーション DTO 欠落 |
| イベント設計 | ✅ | 不足 | トピック名不整合・Outbox 未設計 |
| エラーハンドリング | ✅ | 十分 | |
| パフォーマンス | ✅ | 概ね十分 | Write-Through・AsNoTracking 方針不足 |
| セキュリティ | ✅ | 不足 | PCI DSS 矛盾・IDOR コード例なし |
| 監視・ロギング | ✅ | 概ね十分 | PII マスキングルール不足 |
| テスト戦略 | ✅ | 不足 | 具体テストケース・Saga テストなし |
| Dockerfile | ✅ | 概ね十分 | `--start-period` 欠落 |
| CI/CD | ✅ | 十分 | |
| 運用・保守 | ✅ | 概ね十分 | データ保持期間の法的整合性未確認 |
| Saga 統合設計 | ❌ | **欠如** | **新規セクション追加必須** |
| Outbox パターン設計 | ❌ | **欠如** | **新規セクション追加必須** |
| EF Core エンティティ定義 | ❌ | **欠如** | **新規セクション追加必須** |
| Service/Repository インターフェース | ❌ | **欠如** | **新規セクション追加必須** |
| リクエスト/レスポンス DTO 定義 | ❌ | **欠如** | **新規セクション追加必須** |
| 価格・税計算ロジック | ❌ | **欠如** | **新規セクション追加必須** |
| Program.cs ミドルウェア構成 | ❌ | **欠如** | **新規セクション追加必須** |

### サービス間整合性
- **Kafka イベントの発行元/購読先は spec.md のトピック一覧と不整合** — トピック名・購読先サービスの修正が必要
- **`user.deleted` イベントの購読が欠落** — データ削除/匿名化対応に必要
- **Saga ステップでの PaymentCartService の役割が設計書に反映されていない**

### 未定義・曖昧な領域
1. Saga 統合の具体的なインターフェース（SagaCoordinator からの呼び出し方法: gRPC / REST / Kafka）
2. マルチ通貨対応時の `DECIMAL(10,2)` → `DECIMAL(12,2)` への移行計画（spec.md では `DECIMAL(12,2)` だが設計書は `DECIMAL(10,2)`）
3. Redis 障害時のフォールバック戦略
4. 同時カート操作時の楽観的ロック競合 UX（409 Conflict 返却時のフロントエンド挙動）
5. `PaymentMethod` エンティティが意味するもの（保存済みカード情報の場合は PCI DSS SAQ レベルに影響）

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### 観点: ビジネス要件の完全性・ユーザーストーリーとの整合性

**判定**: ⚠️ Conditional Approval

- spec.md のユーザーストーリー「チェックアウトフロー受入基準」（Given/When/Then）に記載された 6 つの受入基準のうち、設計書で完全にカバーされているのは 3 つのみ（カート→チェックアウト遷移、Stripe リダイレクト、決済成功時の注文確定）
- ゲスト購入フロー（spec.md §ゲスト購入フロー）の API が設計書に未反映
- カート離脱率 KPI（30% 以下）の測定方法として `CartAbandoned` イベントが定義されているが、Kafka トピック名の不整合により実装に影響
- 部分返金 vs 全額返金の業務ルール未定義
- チェックアウトリクエストに `couponCode` と `usedPoints` が含まれるが、これらが PaymentCartService の責務かは疑問（Saga 経由で CouponService / PointService が担当すべき）

</details>

<details>
<summary>architect レビューレポート</summary>

### 観点: マイクロサービス分割・DDD・Bounded Context・アーキテクチャパターン

**判定**: ❌ Rejected（Critical 3 件）

- **C-01**: Outbox パターン未設計（ADR-0005 違反）
- **C-02**: Kafka トピック名不整合（spec.md vs 設計書）
- **C-03**: 3 エンティティ欠落（PaymentMethod, Transaction, PaymentStatus）
- DDD 観点: Aggregate Root の明示的な定義がない。`Cart` が Aggregate Root であり `CartItem` は子エンティティであることの記載が必要
- Value Object（`Money` record）が未定義。金額フィールドが `decimal` で散在している
- Bounded Context の境界が曖昧。チェックアウトリクエストに配送先住所・クーポンコードが含まれており、責務の越境が発生している

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 観点: C# 14 / .NET 10 コード品質・禁止パターン・コーディング規約

**判定**: ❌ Rejected（Critical 1 件）

- **C-07**: Cart エンティティの楽観的ロック（RowVersion）欠如
- EF Core エンティティクラスの定義なし — `[Table]`, `[Column]`, `[Key]` 等の Data Annotations が必要
- Service/Repository インターフェース未定義 — DI 登録パターンが不明
- リクエスト/レスポンス DTO record 型の定義なし — バリデーションルールが実装者に伝達されない
- `ExpiredCartCleanupService` コード例は概ね AGENTS.md 準拠だが、`IServiceScopeFactory` の使用パターンは正しい
- Webhook エンドポイントのコード例にバリデーション（FluentValidation）が未適用
- `CancellationToken ct = default` パラメータは一部のコード例にのみ記載

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 観点: DB スキーマ設計・EF Core マッピング・マイグレーション安全性

**判定**: ❌ Rejected（Critical 1 件）

- **C-06**: `TIMESTAMP WITH TIME ZONE` 未使用
- spec.md の CHECK 制約が設計書に未反映
- spec.md の FK 制約名・インデックス定義が未反映
- `DECIMAL(10,2)` — spec.md では `DECIMAL(12,2)` で定義されており精度が異なる
- `cart_items (cart_id, product_id) UNIQUE` 制約が欠落（同一カート内の商品重複を DB レベルで防止できない）
- 部分インデックス `idx_carts_expired` が spec.md に定義されているが設計書に未反映
- `payments.order_id` に UNIQUE 制約があるが、部分返金で複数 Payment レコードが必要になる場合の設計が未整理

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 観点: OWASP Top 10・認証/認可・PCI DSS・秘密情報管理

**判定**: ❌ Rejected（Critical 2 件）

- **C-04**: PCI DSS 方針矛盾（ADR-0008 SAQ A vs 設計書 SAQ A-EP）
- **C-05**: Stripe Webhook 署名検証コード未記載
- IDOR 防止の具体的コード例なし
- レート制限の詳細設定なし
- Webhook エンドポイントの認可設定（AllowAnonymous + 署名検証）が不明確
- Stripe API キー管理は適切に設計されている（環境変数 / Key Vault）
- Cookie セキュリティ設定は適切（HttpOnly, Secure, SameSite=Strict）

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 観点: GDPR・個人情報保護法・PCI DSS・データガバナンス

**判定**: ⚠️ Conditional Approval

- 決済データの保持期間が法的要件と整合性未確認
- PII ログマスキングルールが不十分
- `user.deleted` イベント受信時のデータ匿名化処理が未設計
- ゲスト購入時のメールアドレス暗号化（spec.md: AES-256）が設計書に未反映
- カード情報非保持（PCI DSS SAQ A）の方針自体は適切

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 観点: トレーサビリティ・ドキュメント整合性・ADR 準拠

**判定**: ⚠️ Conditional Approval

- `created_by` / `updated_by` 監査カラムが payments テーブルに未定義
- Correlation ID のミドルウェア設定コード例がない
- 決済操作の監査ログ出力設計が不足
- ドキュメントのセクション番号に欠番がある（§5, §6 が飛んでいる）
- ADR-0005, ADR-0006, ADR-0008, ADR-0009 との整合性に複数の問題あり

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 観点: テスト戦略・カバレッジ・受入基準の検証可能性

**判定**: ⚠️ Conditional Approval

- テスト戦略が概要レベルのみで具体テストケースがない
- Saga 補償トランザクションのテストケースが皆無
- テストメソッド命名パターン（`Should_X_When_Y`）への言及なし
- 分岐カバレッジ 80% 目標への言及なし
- Stripe テストモードの記載は適切
- セキュリティテスト（認可・IDOR）のケース定義がない

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 観点: パフォーマンス SLA・キャッシュ戦略・スケーラビリティ

**判定**: ⚠️ Conditional Approval

- Redis Write-Through パターンが設計書に未反映
- カート TTL の二段階設計（Redis 7 日 / PostgreSQL 30 日）が未反映
- 監視メトリクスは十分に定義されている（payment-success-rate 95% 目標等）
- キーセットページネーションの言及は適切
- `AsNoTracking()` の適用方針が明示されていない
- `ExpiredCartCleanupService` のバッチサイズ制限がない

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 観点: コンテナ設計・可観測性・ヘルスチェック・DR 計画

**判定**: ✅ Approved with Notes

- Dockerfile はマルチステージビルド・非 root 実行・HEALTHCHECK を含み概ね適切
- `--start-period` パラメータの追加を推奨
- CI/CD パイプライン（GitHub Actions）は基本構成を含んでいる
- バックアップ戦略・スケーリング戦略は定義されている
- RTO 4 時間は ADR-0010 の RTO 1 時間と不整合の可能性

</details>

<details>
<summary>release-manager レビューレポート</summary>

### 観点: リリース戦略・ロールバック計画・バージョニング

**判定**: ✅ Approved with Notes

- CI/CD パイプラインの基本構成は定義されている
- Docker イメージのセマンティックバージョニングタグの併用を推奨
- カバレッジゲート（80%）が CI パイプラインに未設定

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 観点: NuGet ライセンス・CVE・禁止パッケージ

**判定**: ✅ Approved with Notes

- 全記載パッケージは AGENTS.md の必須パッケージリストに準拠
- `Stripe.net 46.*` は Apache 2.0 ライセンスで商用利用可
- プレリリースパッケージの使用なし
- Serilog.Sinks.Console, Serilog.Formatting.Compact, OpenTelemetry.Instrumentation.AspNetCore がライブラリ一覧に未記載

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 観点: UX 設計品質・WCAG 2.1・レスポンシブ

**判定**: ✅ Approved with Notes

- バックエンドサービスの設計書であり、UX の直接的な範囲は限定的
- Webhook 処理完了後のフロントエンド通知方式が未定義（ユーザーの待機体験に影響）
- エラーレスポンスのメッセージがユーザーフレンドリーであるかのガイドラインがない

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 観点: 技術標準の横断適合性・実装実現可能性・規約遵守

**判定**: ❌ Rejected

- AGENTS.md / spec.md / ADR との整合性に関する横断的な問題が多数
- 実装に着手するには、Saga 統合設計・Outbox パターン・EF Core エンティティ定義・Service/Repository インターフェース・価格計算ロジック・Program.cs ミドルウェア構成の 7 セクションの追加が必要
- `user.deleted` イベント購読の欠落は GDPR 対応に影響し、法的リスクを伴う
- 全体として設計書の骨格（API 設計、エラーハンドリング、Stripe 連携、インフラ）は良好だが、AGENTS.md が要求する詳細度に達していない

</details>
