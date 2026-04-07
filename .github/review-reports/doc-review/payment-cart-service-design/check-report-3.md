# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/payment-cart-service-design.md`
- **判定**: ⚠️ **Conditional Approval** — High 指摘あり（Critical なし）
- **レビュー日時**: 2026-04-03 (イテレーション 3)
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

## 技術スタック検証結果
| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| ランタイム | .NET 10 (LTS) | .NET 10 (LTS) | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| ORM | EF Core 10 | EF Core 10 | ✅ |
| DB | PostgreSQL (paymentdb) | PostgreSQL (ADR-0006) | ✅ |
| キャッシュ | Redis (StackExchange.Redis) | Redis (StackExchange.Redis) | ✅ |
| メッセージング | Kafka (Confluent.Kafka) | Kafka (Confluent.Kafka) | ✅ |
| 認証 | JWT (JwtBearer) | ASP.NET Core Identity + JwtBearer | ✅ |
| 決済 | Stripe.net 46.* | 外部決済 (Stripe) | ✅ |
| バリデーション | FluentValidation 11.* | FluentValidation 11.* | ✅ |
| ログ | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| テレメトリ | OpenTelemetry 1.* | OpenTelemetry 1.* | ✅ |
| 耐障害性 | Polly 8.* + Http.Resilience 9.* | Polly 8.* + Http.Resilience 9.* | ✅ |

## 指摘サマリー
| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| architect | ⚠️ | 0 | 2 | 2 | 0 |
| security-reviewer | ⚠️ | 0 | 2 | 1 | 0 |
| dba-reviewer | ⚠️ | 0 | 1 | 2 | 1 |
| performance-reviewer | ✅ | 0 | 0 | 2 | 1 |
| business-analyst | ✅ | 0 | 0 | 1 | 1 |
| compliance-reviewer | ⚠️ | 0 | 1 | 1 | 0 |
| infra-ops-reviewer | ✅ | 0 | 0 | 1 | 1 |
| oss-reviewer | ✅ | 0 | 0 | 0 | 1 |
| programing-reviewer | ✅ | 0 | 0 | 2 | 1 |
| qa-manager | ✅ | 0 | 0 | 2 | 0 |
| release-manager | ✅ | 0 | 0 | 0 | 1 |
| tech-lead | ⚠️ | 0 | 1 | 1 | 0 |
| ux-accessibility-reviewer | ✅ | 0 | 0 | 0 | 1 |
| audit-reviewer | ⚠️ | 0 | 1 | 1 | 0 |
| **合計** | | **0** | **8** | **16** | **8** |

## 判定根拠
- 判定ルール適用結果: Critical = 0、High = 8 → ⚠️ Conditional Approval（人間の判断を介在）
- 最も重大な指摘: Saga ステップ番号の不整合（H1）、gRPC proto 定義の不足（H2）、Webhook IP 制限の未記載（H4）

---

## Critical/High 指摘一覧（修正必須）

| # | 重要度 | 出典 Agent | カテゴリ | 対象箇所 | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|---------|----------|----------|
| H1 | **High** | architect, tech-lead | Saga 整合性 | §20 Saga 統合設計 | **Saga ステップ番号の不整合**: spec.md では決済認証は **ステップ 6**（Stripe/GMO への HTTPS 呼出し）と定義されているが、ADR-0009 では **ステップ 2** が PaymentCartService の決済処理となっている。設計書§20のテーブルでは「ステップ 6（決済認証）」と記載しており spec.md と一致するが、ADR-0009 のステップ構成テーブルとの齟齬がある。ADR-0009 は初期版であり spec.md が SSOT であるため、設計書の記載自体は正しいが、**ADR-0009 への参照注記**が必要。 | §20 冒頭に「※ ADR-0009 のステップ番号は初期定義。spec.md のステップ定義（1:カート取得, 2:在庫引当, 3:クーポン, 4:ポイント, 5:注文作成, 6:決済認証, 7:ポイント確定, 8:カートクリア, 9:Outbox）が SSOT」と明記 |
| H2 | **High** | architect | gRPC 設計 | §20 / §3 | **gRPC インターフェース定義の不足**: spec.md で定義された `cart.proto` の `GetCart`/`ClearCart` および `payment.proto` の `ProcessPayment`/`Refund` について、設計書に**メッセージ型の詳細定義**（リクエスト/レスポンスの各フィールド）が記載されていない。REST DTO は§23で定義されているが、gRPC 用 `.proto` メッセージ定義は対応するサービス設計書に記載すべき。 | §20 に `GetCartRequest`/`GetCartResponse`、`ClearCartRequest`/`ClearCartResponse`、`ProcessPaymentRequest`/`ProcessPaymentResponse`、`RefundRequest`/`RefundResponse` の `.proto` メッセージ型詳細定義を追加 |
| H3 | **High** | security-reviewer | Webhook セキュリティ | §15 | **Webhook エンドポイントの IP 制限未記載**: Stripe Webhook の署名検証は実装されているが、**Stripe の Webhook IP レンジによるネットワークレベルの制限**（Azure Container Apps のネットワーク設定 / API Gateway のルール）が設計されていない。多層防御の観点から、署名検証に加えて IP レベルの制限も必要。 | §15 に「Stripe Webhook IP レンジ（`https://stripe.com/docs/ips`）による IP ホワイトリスト設定を API Gateway または Azure Container Apps のインバウンドルールに追加」を記載 |
| H4 | **High** | security-reviewer | 決済セキュリティ | §9 / §15 | **Webhook エンドポイントのレート制限未設定**: §24 のレート制限設定で Webhook エンドポイント（`/api/v1/payments/webhook`）に対するレート制限が未設定。DDoS 攻撃により署名検証処理が過負荷になるリスクがある。 | §24 のレート制限設定に Webhook 用ポリシー（例: `AddFixedWindowLimiter("webhook", opt => { opt.PermitLimit = 100; opt.Window = TimeSpan.FromMinutes(1); })`）を追加 |
| H5 | **High** | dba-reviewer | データモデル | §4 / §21 | **Payment エンティティの楽観的ロック欠如**: Cart には `[Timestamp] RowVersion` が定義されているが、Payment エンティティには楽観的ロック用の `row_version` カラムが**存在しない**。Saga 補償トランザクション（返金処理）と Webhook による同時更新で競合が発生した場合、データ不整合のリスクがある。 | payments テーブルに `row_version BYTEA NOT NULL` を追加し、Payment エンティティに `[Timestamp] public byte[] RowVersion { get; set; } = [];` を追加 |
| H6 | **High** | compliance-reviewer | データ保護 | §29 | **DSR 処理の仮名化方法が spec.md と一致しない**: spec.md では PaymentCartService の DSR 削除は「`userId` → **ソルト付き SHA-256 ハッシュ化**」と定義されているが、§29 では「`payments.customer_id` を**ハッシュ値に置換**」と記載しており、ソルトへの言及がない。ソルトなしのハッシュはレインボーテーブル攻撃に脆弱。 | §29 の PII 匿名化セクションを「`payments.customer_id` をソルト付き SHA-256 ハッシュ値に置換。ソルトは Azure Key Vault で管理（spec.md §DSR 処理準拠）」に修正 |
| H7 | **High** | audit-reviewer | 監査証跡 | §21 / §30 | **Payment テーブルの `created_by`/`updated_by` カラムが他エンティティに欠如**: payments テーブルには `created_by`/`updated_by` 監査カラムが定義されているが、`carts`、`transactions`、`payment_methods` テーブルには監査カラムがない。spec.md の AuditLog 設計に基づき、**金融データを扱う全テーブルに監査カラムが必要**。 | transactions テーブルに `created_by`/`updated_by` を追加（最低限）。carts は操作頻度が高いため、必要に応じて AuditLog テーブルとの連携で代替可能 |
| H8 | **High** | tech-lead | 全体設計 | §27 | **ゲストチェックアウトの Saga 統合設計が不足**: §27 でゲスト購入フローのエンドポイントは定義されているが、ゲスト購入時に Saga がどのように動作するか（`userId = null` の場合のポイント・クーポン処理スキップの詳細設計）が記載されていない。spec.md では「ゲスト注文にはポイント付与・クーポン適用を行わない」と明記されているが、**Saga ステップのスキップロジック**が設計書に反映されていない。 | §27 に「ゲスト購入時の Saga 動作: ステップ 3（クーポン検証）・ステップ 4（ポイント仮消費）・ステップ 7（ポイント確定付与）をスキップ。SagaCoordinator はゲストフラグに基づきステップを動的にスキップする」を追記 |

---

## Medium 指摘一覧（改善推奨）

| # | 重要度 | 出典 Agent | カテゴリ | 対象箇所 | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|---------|----------|----------|
| M1 | Medium | architect | イベント設計 | §イベント設計 | **`cart.abandoned` イベントの設計が不足**: spec.md では `CartCleanupService` が `cart.abandoned` イベントを Kafka に発行し、カート放棄メールの送信をトリガーすると定義されている（カート離脱率 KPI 対応）。しかし設計書のイベント設計セクションの「発行イベント」に `cart.abandoned` が含まれていない。 | 発行イベントテーブルに `cart.abandoned`（トピック: `cart.abandoned`, パーティション: 6, キー: cartId, 購読先: MailSendService）を追加 |
| M2 | Medium | architect | BackgroundService | §14 / §34 | **`CartCleanupService` と `CartExpirationService` と `ExpiredCartCleanupService` の命名・責務の混乱**: spec.md では `CartCleanupService` という名前で定義されているが、設計書では `ExpiredCartCleanupService`（§14: 30 日経過カート削除）と `CartExpirationService`（§34: 24 時間経過の EXPIRED 化）の 2 つに分離されており、さらに spec.md のカート放棄メール発行の責務が `CartCleanupService` に割り当てられている。命名と責務の対応関係を明確化すべき。 | § 14 冒頭に BackgroundService 一覧テーブルを追加し、各 Service の名称・責務・ポーリング間隔・排他制御方式を明確化 |
| M3 | Medium | dba-reviewer | インデックス | §8 / §30 | **`payment_methods` テーブルのインデックスが不足**: §8 のインデックス設計テーブルに `payment_methods` テーブルのインデックスが含まれていない。`user_id` での検索が主要ユースケースであるため、`idx_payment_methods_user_id` が必要（§30 の Fluent API では定義されている）。 | §8 のインデックス設計テーブルに `payment_methods | idx_payment_methods_user_id | user_id | ユーザー別決済方法検索` を追加し、§30 との一貫性を確保 |
| M4 | Medium | dba-reviewer | データ保持 | §29 | **outbox_events のアーカイブ戦略が「30 日」のみで DLT への転送設計が不足**: outbox_events の `FAILED`/`DEAD_LETTER` ステータスのイベントについて、Dead Letter Topic (DLT) への転送とアラート設計が §19 の OutboxPublisher コードには含まれていない。spec.md の DLT 戦略参照。 | §19 の OutboxPublisher に `FAILED` イベントの DLT 転送ロジックを追記。または別の BackgroundService として `OutboxDeadLetterService` を設計 |
| M5 | Medium | performance-reviewer | キャッシュ | §26 | **Redis 障害時の縮退運転のレイテンシ影響が未分析**: §26 で Redis 障害時に PostgreSQL にフォールバックすると記載されているが、Redis 障害時のレイテンシ増加（Redis: ~1ms → PostgreSQL: ~10-50ms）が Saga ステップ 1 のレイテンシバジェット（50ms）に収まるかの分析がない。 | §26 に「Redis 障害時のレイテンシ影響: PostgreSQL 直接アクセスで ~30ms 増加。Saga ステップ 1 の gRPC Deadline 200ms の範囲内で許容可能」等の分析を追加 |
| M6 | Medium | performance-reviewer | Kafka | §イベント設計 | **`payment.completed` のパーティション数 12 の根拠が不明**: spec.md と一致しているが、パーティション数 12 の選定根拠（ピーク TPS、Consumer 並列度等）が設計書に記載されていない。 | §イベント設計に「パーティション数の選定根拠: ピーク時 checkout 100/分（~1.7 TPS）に対し、Consumer 並列度・将来の成長を考慮して 12 パーティション」等の根拠を追加 |
| M7 | Medium | business-analyst | ビジネスロジック | §25 | **配送料計算の離島対応が未定義**: §25 の `IShippingFeeCalculator` で「北海道・沖縄: 1,100 円、その他: 550 円」と定義されているが、離島（沖縄離島、小笠原等）の追加料金が未考慮。 | §25 に「離島追加料金: Phase 1 では一律 1,100 円（北海道・沖縄と同額）、Phase 2 で郵便番号ベースの離島判定を実装」を追記 |
| M8 | Medium | compliance-reviewer | 電子帳簿 | §29 | **決済データの「コールドストレージ」の具体的な設計が不足**: §29 で「7 年保持後コールドストレージにアーカイブ」と記載されているが、Azure Blob Storage の Tier（Cool/Archive）、アーカイブ形式（Parquet/CSV）、アクセス方法が未定義。 | §29 に「アーカイブ先: Azure Blob Storage Archive Tier、形式: JSON Lines、アクセス: 法的要請時に Rehydrate（最大 15 時間）」を追記 |
| M9 | Medium | infra-ops-reviewer | Dockerfile | §12 | **Dockerfile の EXPOSE ポートが 5005 だが、コンテナ環境では 8080 が推奨**: Azure Container Apps および ASP.NET Core のデフォルトでは 8080 が推奨。EXPOSE 5005 は開発用ポートであり、本番環境との乖離がある。 | Dockerfile の `EXPOSE` を `8080` に変更し、`ENV ASPNETCORE_URLS=http://+:8080` を追加 |
| M10 | Medium | programing-reviewer | コード品質 | §21 | **PaymentMethod エンティティの `AccountReference` が Nullable だが DB スキーマでは NOT NULL**: §付近の EF Core エンティティで `public string? AccountReference` と Nullable で定義されているが、DB スキーマ（§payment_methods テーブル）では `NOT NULL` と定義。EF Core マイグレーション生成時に不整合が発生する。 | エンティティ定義を `[Required] public string AccountReference { get; set; } = string.Empty;` に修正（DB スキーマと整合） |
| M11 | Medium | programing-reviewer | コード品質 | §23 | **CheckoutRequest DTO に `ShippingAddress` と `CouponCode` / `UsedPoints` が欠落**: API 設計セクション（§API 設計）のチェックアウトリクエスト例では `shippingAddress`、`couponCode`、`usedPoints` フィールドが含まれているが、§23 の `CheckoutRequest` record 定義にはこれらが含まれていない。 | §23 の `CheckoutRequest` に `ShippingAddress?`、`string? CouponCode`、`int UsedPoints = 0` フィールドを追加 |
| M12 | Medium | qa-manager | テスト | §28 | **Stripe テストモードの環境変数管理が未記載**: §11 で「Stripe テスト API キー (`sk_test_*`) を使用」と記載されているが、テスト実行時に Stripe テストキーをどこから取得するか（`dotnet user-secrets` / 環境変数 / CI/CD シークレット）が未定義。 | §28 に「Stripe テストキーの管理: ローカル開発は `dotnet user-secrets`、CI/CD は GitHub Actions Secrets（`STRIPE_TEST_SECRET_KEY`）を使用」を追記 |
| M13 | Medium | qa-manager | テスト | §28 | **Saga 補償トランザクションのテストシナリオが不足**: §28 のテストケース一覧に「Saga 補償: 決済成功後にポイント付与失敗 → 返金が実行されること」は含まれているが、**決済タイムアウト時の `PENDING_PAYMENT` 状態遷移テスト**が欠落。spec.md では決済タイムアウト時の詳細な状態遷移が定義されている。 | §28 のテストケース一覧に「Saga 決済タイムアウト: Stripe API タイムアウト時に PENDING_PAYMENT 状態に遷移し、SagaRecoveryService がポーリングすること」を追加 |
| M14 | Medium | tech-lead | 全体設計 | §25 | **消費税率のハードコードリスク**: §25 の `ITaxCalculator` インターフェースで `decimal taxRate` をパラメータとして受け取っているが、税率の取得元（設定ファイル / DB / 外部サービス）が未定義。消費税率変更（8%→10% 等の過去事例）への対応設計が不足。 | §25 に「税率は `appsettings.json` の `TaxSettings:StandardRate`（10%）と `TaxSettings:ReducedRate`（8%）から取得。軽減税率対象商品の判定は InventoryManagementService の `Product.TaxCategory` に基づく」を追記 |
| M15 | Medium | architect | 認証 | §セキュリティ | **カート API の認可境界が曖昧**: カート API は `AllowAnonymous` だが、ログイン済みユーザーのカートに対する IDOR 対策が `ResolveCartId` ヘルパーに依存している。ログイン済みユーザーが Cookie の CartId を操作して他ユーザーのカートにアクセスするシナリオへの対策が明示されていない。 | §31 の `ResolveCartId` ロジックに「ログイン済みの場合は Cookie の CartId を無視し、`userId` ベースでカートを解決する」ことを明示的に記載 |
| M16 | Medium | audit-reviewer | 監査 | §10 | **決済ステータス変更の監査ログが未設計**: payments テーブルのステータス遷移（PENDING→PROCESSING→COMPLETED 等）は金融監査上重要だが、ステータス変更の履歴を transactions テーブルに加えて監査ログ（AuditLog）として記録する設計が明示されていない。 | §10 の監視セクションに「decistion_status_changed 監査イベント: 全ステータス変更を AuditLog に記録（旧ステータス、新ステータス、変更者、タイムスタンプ）」を追加 |

---

## Low 指摘一覧（軽微・推奨）

| # | 重要度 | 出典 Agent | カテゴリ | 対象箇所 | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|---------|----------|----------|
| L1 | Low | dba-reviewer | 命名 | §DB スキーマ | `transactions` テーブル名が汎用的すぎる。`payment_transactions` の方がドメインコンテキストが明確。 | テーブル名を `payment_transactions` に変更検討（影響範囲が広いため Phase 2 で対応可） |
| L2 | Low | performance-reviewer | キャッシュ | §8 | `cart:session:{sessionId}` と `cart:user:{customerId}` キーが並存するが、ログイン時マージ後の旧キー削除タイミングが未記載。 | §26 に「ログイン時マージ後、`cart:session:{sessionId}` キーを Redis から削除」を追記 |
| L3 | Low | business-analyst | UX | §API 設計 | カートレスポンスの `expiresAt` のみでなく、残り時間（`remainingMinutes`）を返すとフロントエンド実装が容易。 | レスポンス DTO に `int? RemainingMinutes` の追加を検討 |
| L4 | Low | oss-reviewer | 依存関係 | §2 | `Stripe.net 46.*` のバージョン指定が具体的すぎる。他のパッケージは `10.*` 等のワイルドカードだが、Stripe は外部 SDK のためメジャーバージョン固定が妥当かの判断が必要。 | Stripe.net のバージョンポリシーを確認し、`46.*` が妥当であれば理由をコメントとして追記 |
| L5 | Low | programing-reviewer | コード品質 | §33 | Program.cs の HttpClient 登録で `"InventoryService"` が文字列リテラル。定数化が望ましい。 | `const string InventoryServiceClient = "InventoryService";` として定数化 |
| L6 | Low | release-manager | CI/CD | §12 | CI/CD パイプラインで `dotnet test` の `--filter` オプションが未使用。単体テストと統合テストの分離実行が未設計。 | `dotnet test --filter Category=Unit` と `dotnet test --filter Category=Integration` を分離 |
| L7 | Low | ux-accessibility-reviewer | フロントエンド連携 | 全体 | 設計書にフロントエンドとの連携 UI 要件（spec.md の注文確認画面 UI 要件等）への参照が不足。 | §27 に spec.md §注文確認画面 UI 要件へのクロスリファレンスを追加 |
| L8 | Low | infra-ops-reviewer | ヘルスチェック | §33 | Stripe API のヘルスチェックが含まれていない。Stripe API の疎通確認を Readiness プローブに含めることで、Stripe 障害時の早期検知が可能。 | ヘルスチェックに Stripe API ステータスの確認を追加検討（ただし頻繁な API コールは避ける） |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E1 | 高優先 | architect | ADR-0009 のステップ番号と spec.md のステップ番号の乖離について、ADR-0009 を spec.md に合わせて改訂するか、ADR-0009 に「spec.md が SSOT」の注記のみ追加するかの判断 | テックリード |
| E2 | 高優先 | compliance-reviewer | DSR 処理における「仮名化データの 7 年保持」の法的妥当性について、法務チームと GDPR 専門家によるレビューが spec.md に TODO として残っている。設計書もこの前提に依存。 | 法務チーム + テックリード |
| E3 | 通常 | security-reviewer | Stripe Webhook の IP ホワイトリスト設定を API Gateway レベルで実装するか、Azure Container Apps のネットワークルールで実装するかの方式選定 | インフラチーム |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|---------|----------|
| — | — | — | 本レビューにおいて Agent 間の競合は検出されなかった | — | — |

---

## ドキュメント横断分析

### spec.md との整合性チェック結果

| 検証項目 | 結果 | 備考 |
|---------|------|------|
| サービス名・ポート | ✅ | PaymentCartService / 5005 — spec.md と一致 |
| DB 名 | ✅ | paymentdb — ADR-0006 準拠 |
| Aggregate Root 定義 | ✅ | Cart, Payment — spec.md の DDD 戦術パターンと一致 |
| Kafka トピック（発行） | ⚠️ | `payment.completed`, `payment.failed`, `payment.refunded` は一致。`cart.abandoned` が設計書に未記載 |
| Kafka トピック（購読） | ✅ | `order.created`, `order.cancelled`, `user.deleted` — spec.md と一致 |
| Saga ステップの役割 | ✅ | ステップ 1（カート取得）、ステップ 6（決済認証）、ステップ 8（カートクリア）— spec.md と一致 |
| gRPC proto 定義 | ⚠️ | spec.md で `cart.proto` / `payment.proto` が定義済みだが、設計書にメッセージ型の詳細が不足 |
| Write-Through パターン | ✅ | Redis キャッシュ + PostgreSQL 永続化 — spec.md と一致 |
| カート TTL | ✅ | Redis 7 日 / PostgreSQL 30 日 — spec.md と一致 |
| PCI DSS 非保持化 | ✅ | Stripe Checkout (Hosted Payment Page) / SAQ A — ADR-0008 と一致 |
| Outbox パターン | ✅ | 動的バックオフ（100ms〜5s）— ADR-0005 / AGENTS.md §10.4 と一致 |
| べき等性設計 | ✅ | spec.md の `idempotency_keys` テーブル設計は SalesManagementService 側の責務であり、PaymentCartService は Stripe の `IdempotencyKey` で対応 — 整合 |
| DSR 処理 | ⚠️ | 基本方針は一致するが、ソルト付き SHA-256 の明記が不足（H6） |
| 電子帳簿保存法 | ✅ | 7 年保持 — spec.md と一致 |
| ゲスト購入フロー | ⚠️ | エンドポイントは定義済みだが Saga 統合の詳細設計が不足（H8） |
| Advisory Lock ID | ✅ | `CartCleanupService` → `hashtext('cart_cleanup')` — spec.md の命名規約と一致 |
| ミドルウェアパイプライン順序 | ✅ | AGENTS.md §11.3 の順序を厳守 |
| 例外ハンドラー | ✅ | ForbiddenException、ConcurrencyException が追加されている（AGENTS.md §4.7 拡張） |

### 設計書の充実度評価

| セクション | 充実度 | 備考 |
|-----------|--------|------|
| 概要・技術スタック | ★★★★★ | 完全 |
| データモデル | ★★★★☆ | Payment の楽観的ロック欠如（H5） |
| API 設計 | ★★★★☆ | gRPC メッセージ定義不足（H2） |
| イベント設計 | ★★★★☆ | `cart.abandoned` 欠落（M1） |
| セキュリティ | ★★★★☆ | Webhook IP 制限未記載（H3, H4） |
| Saga 統合 | ★★★★☆ | ステップ番号注記・ゲスト対応不足（H1, H8） |
| EF Core エンティティ | ★★★★★ | Fluent API を含む完全定義 |
| Endpoint 実装 | ★★★★★ | Cart/Payment 両方の完全実装パターン |
| FluentValidation | ★★★★★ | 全 DTO にバリデーター定義 |
| Program.cs | ★★★★★ | DI 登録 + ミドルウェアの完全定義 |
| テスト戦略 | ★★★★☆ | Saga タイムアウトテスト不足（M13） |
| Dockerfile / CI/CD | ★★★★☆ | EXPOSE ポート番号（M9） |
| Outbox パターン | ★★★★★ | 動的バックオフ + Advisory Lock 完備 |
| 価格計算 | ★★★☆☆ | 税率取得元・離島対応が不足（M7, M14） |
| データ保持 | ★★★★☆ | アーカイブ先の具体設計不足（M8） |

### 未定義・曖昧な領域

| 領域 | 影響度 | 対応推奨時期 |
|------|--------|------------|
| gRPC メッセージ型の詳細定義 | High | 実装前（Phase 1） |
| ゲスト購入時の Saga ステップスキップ設計 | High | 実装前（Phase 1） |
| 消費税率の取得元・軽減税率対応 | Medium | 実装時 |
| 離島配送料の計算ロジック | Medium | Phase 2 |
| コールドストレージの具体設計 | Medium | Phase 2 |

---

## 各 Agent 詳細レポート

<details>
<summary>architect レビューレポート</summary>

### 観点: アーキテクチャ設計・サービス境界・DDD パターン

**評価**: ⚠️ Conditional（High 2 件, Medium 2 件）

**良い点**:
- Aggregate Root（Cart, Payment）の定義が spec.md と完全一致
- Outbox パターン・Write-Through キャッシュの設計が詳細かつ ADR-0005 準拠
- SalesManagementService との Saga 統合の責務境界が明確
- BackgroundService のリーダー選出（Advisory Lock）が設計済み

**指摘**:
- H1: Saga ステップ番号の ADR-0009 との乖離に対する注記が必要
- H2: gRPC `.proto` メッセージ型の詳細定義が不足
- M1: `cart.abandoned` イベントの設計が発行イベントテーブルに含まれていない
- M2: BackgroundService の命名・責務の整理が必要（CartCleanupService vs ExpiredCartCleanupService vs CartExpirationService）
</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 観点: OWASP Top 10・PCI DSS・認証認可・秘密情報管理

**評価**: ⚠️ Conditional（High 2 件, Medium 1 件）

**良い点**:
- PCI DSS SAQ A 準拠の Stripe Checkout (Hosted Payment Page) 設計が完全
- Webhook 署名検証（`EventUtility.ConstructEvent`）が正しく実装
- IDOR 防止パターンが全エンドポイントに適用
- Cookie 設定（HttpOnly, Secure, SameSite=Strict）が適切
- PII ログマスキングルールが詳細に定義
- Stripe API キーの環境変数管理が明示

**指摘**:
- H3: Webhook エンドポイントの IP レベル制限が未設計（多層防御の欠如）
- H4: Webhook エンドポイントのレート制限が未設定
- M15: ログイン済みユーザーの Cookie CartId 操作によるカートアクセスのリスクが明示されていない
</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 観点: DB スキーマ設計・EF Core マッピング・インデックス・マイグレーション

**評価**: ⚠️ Conditional（High 1 件, Medium 2 件, Low 1 件）

**良い点**:
- スキーマ定義が snake_case 命名規約に完全準拠
- CHECK 制約（status IN (...), amount > 0, quantity > 0）が適切に定義
- Fluent API（§30）で全インデックス・リレーション・制約が網羅されている
- 部分インデックス（`idx_carts_expired`, `idx_outbox_events_pending`）の設計が優秀
- cart_items の非正規化の設計根拠が明記（マイクロサービス間 JOIN 不可）
- ON DELETE CASCADE / RESTRICT の使い分けが適切

**指摘**:
- H5: Payment エンティティに楽観的ロック（RowVersion）がない
- M3: payment_methods テーブルのインデックスが §8 のテーブルに未記載
- M4: outbox_events の FAILED/DEAD_LETTER イベントの DLT 転送設計が不足
- L1: `transactions` テーブル名が汎用的すぎる
</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 観点: パフォーマンス SLA・スケーラビリティ・キャッシュ戦略

**評価**: ✅ Approved with Notes（Medium 2 件, Low 1 件）

**良い点**:
- Saga ステップ別レイテンシバジェット（spec.md 準拠）への整合性が取れている
- Write-Through キャッシュの Read/Write フロー設計が詳細
- キーセットページネーションの採用
- `AsNoTracking()` の徹底（読み取り最適化）
- レート制限（checkout: 10/min, cart: 60/min, refund: 5/hour）が負荷防止に適切
- 監視メトリクス（payment-success-rate < 95% でアラート等）が実用的

**指摘**:
- M5: Redis 障害時のレイテンシ影響分析が不足
- M6: Kafka パーティション数 12 の選定根拠が不明
- L2: カートマージ後の Redis キー削除タイミングが未記載
</details>

<details>
<summary>business-analyst レビューレポート</summary>

### 観点: ビジネス要件の完全性・ユーザーストーリー・受入基準

**評価**: ✅ Approved with Notes（Medium 1 件, Low 1 件）

**良い点**:
- カート離脱率 30% 以下の KPI に対応するカート放棄メール設計への言及
- ゲスト購入フローの設計（spec.md 準拠）
- 適切なカート有効期限管理（Redis 7 日 / PostgreSQL 30 日）
- 決済方法の拡張計画（Apple Pay / Google Pay / コンビニ決済）が将来計画に記載

**指摘**:
- M7: 離島配送料の計算ロジックが未定義
- L3: カートレスポンスの残り時間フィールドの追加提案
</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 観点: GDPR・個人情報保護法・PCI DSS・電子帳簿保存法

**評価**: ⚠️ Conditional（High 1 件, Medium 1 件）

**良い点**:
- PCI DSS 非保持化方針（ADR-0008）の徹底（カード情報が自社インフラを一切通過しない）
- `user.deleted` イベント購読による GDPR 対応（カートデータ削除・決済データ匿名化）
- PII ログ禁止ルールの詳細定義（カード番号・CVV は絶対禁止）
- 電子帳簿保存法に基づく 7 年保持の設計

**指摘**:
- H6: DSR 処理の仮名化にソルトへの言及がない（spec.md の要件と不整合）
- M8: コールドストレージの具体的な Azure Blob Storage Tier・形式が未定義
</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 観点: コンテナ設計・可観測性・ヘルスチェック・DR

**評価**: ✅ Approved with Notes（Medium 1 件, Low 1 件）

**良い点**:
- Dockerfile がマルチステージビルド + 非 root ユーザー + HEALTHCHECK で適切
- OpenTelemetry（Traces + Metrics + RuntimeInstrumentation）の統合設定が完全
- ヘルスチェック（/health + /health/ready）が PostgreSQL・Redis の疎通を確認
- Serilog + Correlation ID による構造化ログ設計
- 水平スケーリング（min: 2, max: 10, CPU 70% 閾値）の設計
- バックアップ戦略（RPO: 1 時間, RTO: 4 時間）の定義

**指摘**:
- M9: Dockerfile の EXPOSE ポートが 5005（本番環境では 8080 が推奨）
- L8: Stripe API のヘルスチェックが Readiness プローブに含まれていない
</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 観点: NuGet パッケージライセンス・脆弱性・禁止パッケージ

**評価**: ✅ Approved with Notes（Low 1 件）

**良い点**:
- 全パッケージが AGENTS.md / `nuget-dependency.instructions.md` の推奨パッケージと一致
- `Newtonsoft.Json` は使用されていない（`System.Text.Json` 前提）
- プレリリース版（`-preview`, `-beta`, `-rc`）のパッケージは含まれていない
- `FluentValidation.DependencyInjectionExtensions` が正しく使用（`FluentValidation.AspNetCore` は非推奨）

**指摘**:
- L4: `Stripe.net 46.*` のバージョン指定の妥当性確認が必要
</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 観点: C# 14 機能活用・コーディング規約・禁止パターン

**評価**: ✅ Approved with Notes（Medium 2 件, Low 1 件）

**良い点**:
- primary constructor の適切な使用（BackgroundService, Validator 等）
- record 型による DTO 定義（不変性の保証）
- `CancellationToken ct = default` の全 async メソッドへの適用
- `DateTime.UtcNow` の使用（`DateTime.Now` 禁止ルール準拠）
- `TimeProvider` DI による テスタブルな時刻管理（§30 AppDbContext）
- `ILogger<T>` + メッセージテンプレート形式のログ出力
- `Console.WriteLine` の使用なし
- `IServiceScopeFactory` による BackgroundService の Scoped サービス取得
- コレクションの `= []` 初期化（C# 12+ コレクション式）

**指摘**:
- M10: `PaymentMethod.AccountReference` の Nullable 定義と DB スキーマの NOT NULL の不整合
- M11: `CheckoutRequest` DTO に API 設計セクションのフィールドが反映されていない
- L5: HttpClient 名の文字列リテラルの定数化
</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 観点: テスト戦略・カバレッジ目標・テストケース充実度

**評価**: ✅ Approved with Notes（Medium 2 件）

**良い点**:
- テストメソッド命名（`Should_期待結果_When_条件`）が規約準拠
- AAA パターン（Arrange-Act-Assert）の徹底
- NSubstitute + Shouldly の使用
- §28 の必須テストケース一覧が網羅的（正常系 + 異常系 + セキュリティ）
- 分岐カバレッジ 80% 以上の目標が明示
- §35 の CartService ユニットテスト実装例が実用的

**指摘**:
- M12: Stripe テストキーの環境変数管理が未記載
- M13: Saga 決済タイムアウト時の `PENDING_PAYMENT` 状態遷移テストが欠落
</details>

<details>
<summary>release-manager レビューレポート</summary>

### 観点: リリース戦略・CI/CD・ロールバック計画

**評価**: ✅ Approved with Notes（Low 1 件）

**良い点**:
- GitHub Actions CI/CD パイプラインが定義済み（build → test → Docker build → push → deploy）
- Azure Container Registry + Container Apps へのデプロイフローが明確
- パス制限（`PaymentCartService/**`）による不要ビルドの防止

**指摘**:
- L6: 単体テストと統合テストの分離実行が CI パイプラインに未反映
</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 観点: 技術標準の横断適合性・実装実現可能性・規約遵守

**評価**: ⚠️ Conditional（High 1 件, Medium 1 件）

**良い点**:
- AGENTS.md の全規約（§4〜§11）への高い適合度
- DI 登録（Scoped 基本）、ミドルウェアパイプライン順序、例外ハンドラーが完全準拠
- §30 の AppDbContext が `TimeProvider` DI、`IHasTimestamps` インターフェース、Fluent API を網羅
- コード例が実装可能な品質（コピー&ペーストでほぼ動作する水準）

**指摘**:
- H8: ゲスト購入時の Saga ステップスキップ設計が不足（spec.md 要件の未反映）
- M14: 消費税率の取得元が未定義（ハードコードリスク）
</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 観点: トレーサビリティ・ドキュメント整合性・承認プロセス

**評価**: ⚠️ Conditional（High 1 件, Medium 1 件）

**良い点**:
- Correlation ID の全リクエストへの付与と Serilog への統合
- Outbox パターンによるイベント発行の監査証跡（`outbox_events` テーブル）
- payments テーブルの `created_by`/`updated_by` カラム
- PII ログマスキングルールの詳細定義

**指摘**:
- H7: `transactions`, `carts`, `payment_methods` テーブルの監査カラム欠如
- M16: 決済ステータス変更の監査ログ記録設計が不足
</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 観点: UX 設計品質・WCAG 準拠・レスポンシブ設計

**評価**: ✅ Approved with Notes（Low 1 件）

**良い点**:
- バックエンドサービスの設計書としてはフロントエンド連携の記述は十分
- エラーコード定義（CART-4001〜, PAY-4001〜）がフロントエンドのエラー表示に活用可能
- RFC 9457 Problem Details 準拠のエラーレスポンスがフロントエンドの統一的エラーハンドリングを支援

**指摘**:
- L7: spec.md の注文確認画面 UI 要件へのクロスリファレンスが不足
</details>

---

## 総括

設計書は全体として**高品質**であり、以下の点が特に優れている:

1. **spec.md との高い整合性**: Aggregate Root 定義、Kafka トピック、Saga ステップ、キャッシュ戦略が SSOT と一致
2. **コード例の実装可能性**: §30-35 のコード例は実装時にほぼそのまま使用可能な品質
3. **セキュリティの徹底**: PCI DSS 非保持化、IDOR 防止、Webhook 署名検証、Cookie セキュリティが適切
4. **Outbox パターンの完成度**: 動的バックオフ、Advisory Lock、DI 登録が完全に設計済み

High 指摘 8 件のうち、**H2（gRPC メッセージ定義）、H5（Payment 楽観的ロック）、H8（ゲスト Saga 設計）** は実装前に必ず解決すべき設計上の欠落である。その他は注記追加・セクション拡充で対応可能。
