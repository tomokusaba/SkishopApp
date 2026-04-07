# ドキュメント修正ログ
- **対象**: design-docs/payment-cart-service-design.md
- **イテレーション**: 1 / 5
- **修正日時**: 2026-04-03
- **対応レビュー**: check-report-1.md

## 修正サマリー
| 修正件数 | Critical | High | 合計 |
|---------|----------|------|------|
| 修正済み | 7 | 22 | 29 |
| 修正不可（エスカレーション） | 0 | 3 | 3 |

## 修正詳細
| # | 指摘ID | 重要度 | 修正内容の概要 |
|---|--------|--------|-------------|
| 1 | C-01 | Critical | Outbox パターン設計を追加（§19）。`outbox_events` テーブル定義、`OutboxPublisher` BackgroundService（動的バックオフ 100ms〜5s）、イベント発行時のトランザクション統合コード例を記載 |
| 2 | C-02 | Critical | Kafka トピック名を spec.md に統一。`payment.events` → `payment.completed` / `payment.failed` / `payment.refunded` に分割。パーティション数・キー・購読先サービスを追加 |
| 3 | C-03 | Critical | 3 欠落エンティティ（Transaction, PaymentMethod）をデータモデル ER 図・DB スキーマ・EF Core エンティティに追加。spec.md の属性定義に準拠。PaymentStatus はステータスマスターではなく payments.status の CHECK 制約で実装 |
| 4 | C-04 | Critical | Stripe 連携を PaymentIntent（SAQ A-EP）から Stripe Checkout（Hosted Payment Page, SAQ A）に修正。ADR-0008 準拠。シーケンス図を `CheckoutSession.CreateAsync()` ベースに全面改訂 |
| 5 | C-05 | Critical | Stripe Webhook 署名検証コード例を追加。`EventUtility.ConstructEvent()` + `Stripe-Signature` ヘッダー検証。`AllowAnonymous()` + 署名検証の組み合わせを明記 |
| 6 | C-06 | Critical | 全テーブルの日時カラムを `TIMESTAMP` → `TIMESTAMP WITH TIME ZONE` に修正（carts 7列、cart_items 2列、payments 3列、新規テーブル含む全日時カラム） |
| 7 | C-07 | Critical | Cart エンティティに `row_version BYTEA` カラムと EF Core `[Timestamp]` 属性を追加。`DbUpdateConcurrencyException` → HTTP 409 Conflict のハンドリングコード例を記載 |
| 8 | H-01 | High | Saga 統合設計セクション（§20）を新設。PaymentCartService の Saga ステップ 1/6/8 の役割・入出力・補償操作・冪等性保証を定義。補償トランザクション（Refund）のコード例を追加 |
| 9 | H-02 | High | コンポーネントアーキテクチャ図に `PriceService`（価格計算サービス）と `DocService`（ドキュメントサービス）を追加。SecurityComponent も追加 |
| 10 | H-03 | High | キャッシュ戦略に Write-Through パターンの説明を追加。Redis 障害時の縮退運転方針を §26 に記載 |
| 11 | H-05 | High | DB スキーマに CHECK 制約を追加: `cart_items.quantity > 0`, `carts.status IN (...)`, `payments.amount > 0`, `payments.status IN (...)`, `transactions.amount > 0`, `outbox_events.status IN (...)` |
| 12 | H-06 | High | 決済ステータス `SUCCEEDED` → `COMPLETED` に修正（spec.md CHECK 制約準拠）。payments.status の CHECK 制約値を UPPER_CASE で明示 |
| 13 | H-07 | High | FK 制約名（`fk_cart_items_cart_id ON DELETE CASCADE`, `fk_transactions_payment_id ON DELETE RESTRICT`）を明記。`cart_items (cart_id, product_id) UNIQUE` 制約を追加。複合インデックス・部分インデックスを追加 |
| 14 | H-08 | High | EF Core エンティティクラス定義を §21 に追加（Cart, CartItem, Payment, Transaction, OutboxEvent）。Data Annotations（`[Table]`, `[Column]`, `[Key]`, `[Required]`, `[MaxLength]`）完備 |
| 15 | H-09 | High | Service / Repository インターフェース定義を §22 に追加（ICartService, IPaymentService, IRefundService, ICartRepository, IPaymentRepository）。CancellationToken パラメータ・DI 登録例を含む |
| 16 | H-10 | High | リクエスト/レスポンス DTO record 定義を §23 に追加。Data Annotations（`[Required]`, `[MaxLength]`, `[Range]`）によるバリデーションルールを明示 |
| 17 | H-11 | High | IDOR 防止コード例を §24 に追加（ClaimsPrincipal → userId 取得 → オーナーシップ検証） |
| 18 | H-12 | High | レート制限ポリシーを §24 に追加（チェックアウト: 10回/分、カート: 60回/分、返金: 5回/時間） |
| 19 | H-13 | High | Webhook エンドポイントの認可設定を明記: `AllowAnonymous()` + `Stripe-Signature` 検証 |
| 20 | H-14 | High | PII ログマスキングルールを §24 に定義（customerId: 出力可、email: マスキング必須、カード情報: 絶対禁止） |
| 21 | H-15 | High | データ保持期間を 6ヶ月 → 7年に修正（電子帳簿保存法準拠）。§29 にデータ保持・アーカイブ戦略を追加 |
| 22 | H-16 | High | テスト戦略詳細を §28 に追加。テストメソッド命名パターン（Should_X_When_Y）、AAA パターン例、必須テストケース一覧、分岐カバレッジ 80% 目標を明記 |
| 23 | H-17 | High | Saga 補償トランザクションのテストケースを §28 テストケース一覧に追加 |
| 24 | H-18 | High | カート TTL を spec.md 準拠（Redis 7日 / PostgreSQL 30日）に修正。§26 に二段階 TTL 設計を記載 |
| 25 | H-19 | High | `ExpiredCartCleanupService` を PostgreSQL 30日 TTL に修正し、バッチサイズ制限（500件）を追加。OutboxPublisher には動的バックオフが必須である旨を明記 |
| 26 | H-20 | High | `user.deleted` イベントの購読を追加（トピック: `user.deleted`）。アクション: カートデータ削除・決済データの匿名化 |
| 27 | H-21 | High | cart_items テーブルの非正規化フィールドに設計根拠をコメントとして追加（マイクロサービス間 JOIN 不可、スナップショット保持） |
| 28 | H-22 | High | 価格計算セクション（§25）を新設。`IPriceService`, `ITaxCalculator`, `IShippingFeeCalculator` のインターフェース定義を追加（spec.md §配送料・消費税計算ルール準拠） |
| 29 | H-23 | High | `payments` テーブルに `created_by`, `updated_by` 監査カラムを追加 |

## 修正不可事項（エスカレーション）
| # | 指摘ID | 重要度 | 理由 | 推奨判断者 |
|---|--------|--------|------|-----------|
| 1 | H-04 | High | API バージョニング `/api/v1/` と spec.md の URI（バージョンプレフィックスなし）の整合性は API Gateway のルーティング設計に依存するため、本設計書単独では解決不可 | テックリード + API Gateway 設計者 |
| 2 | H-24 | High | ゲスト購入フロー API は spec.md で定義済みだが、SalesManagementService とのサービス責務境界が未確定のため詳細設計は保留。§27 に責務境界の概要を記載済み | テックリード + ドメインエキスパート |
| 3 | H-25 | High | チェックアウト API と Saga フローの責務境界（shippingAddress / couponCode の責務帰属）は複数サービスに跨る設計判断が必要。§27 に責務境界の概要を記載済み | テックリード + ドメインエキスパート |

## エスカレーション事項（check-report-1.md から引き継ぎ）
| # | 優先度 | 内容 | 対応状況 |
|---|--------|------|---------|
| E-01 | 最優先 | ADR-0008 Hosted Payment Page vs PaymentIntent の方針 | **修正済み**: ADR-0008（SAQ A）に準拠して Stripe Checkout に修正 |
| E-02 | 高優先 | checkoutRequest の shippingAddress/couponCode の責務帰属 | H-25 として修正不可（エスカレーション維持） |
| E-03 | 高優先 | 決済データ保持期間の法的整合性 | **修正済み**: 7年間保持に修正（電子帳簿保存法準拠） |
| E-04 | 通常 | Redis TTL と PostgreSQL 保持期間の二重管理 | **修正済み**: §26 に Write-Through パターンと二段階 TTL を設計 |

## 変更統計
- **修正前行数**: 803 行
- **追加セクション数**: 11 セクション（§19-§29）
- **主要変更**: DB スキーマ修正（5テーブル）、新規テーブル追加（3テーブル）、Stripe フロー全面改訂、Outbox/Saga 設計追加
