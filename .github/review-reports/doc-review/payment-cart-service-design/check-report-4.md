# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/payment-cart-service-design.md`
- **判定**: ✅ **Approved with Notes** — Critical/High 指摘なし。Medium/Low の改善推奨事項のみ
- **レビュー日時**: 2026-04-03（イテレーション 4）
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)
- **前回判定**: ⚠️ Conditional Approval（High 8 件）→ 全 8 件修正済み

## Iteration 3 → 4 修正検証結果

| # | 修正項目 | 検証結果 | 確認箇所 |
|---|---------|---------|---------|
| H1 | ADR-0009 SSOT 注記 | ✅ **修正確認** | §20 冒頭に SSOT 注記追加。spec.md のステップ順序（1〜9）が明記され、ADR-0009 との番号差異が説明されている |
| H2 | gRPC proto 完全定義 | ✅ **修正確認** | §36 に `payment.proto`（ProcessPayment/RefundPayment）と `cart.proto`（GetCartSnapshot/ClearCart）の完全な `.proto` メッセージ型定義が追加 |
| H3 | Webhook IP ホワイトリスト多層防御 | ✅ **修正確認** | §15 に 3 層防御テーブル（L1: ネットワーク制限, L2: レート制限, L3: 署名検証）と Azure Container Apps の IP 制限 JSON 設定例が追加 |
| H4 | Webhook レート制限ポリシー | ✅ **修正確認** | §24 に `AddFixedWindowLimiter("webhook", ...)` 100回/分が追加。エンドポイントマッピングで `.RequireRateLimiting("webhook")` 適用済み |
| H5 | Payment RowVersion 楽観的ロック | ✅ **修正確認** | payments テーブルに `row_version BYTEA NOT NULL` 追加。Payment エンティティに `[Timestamp] public byte[] RowVersion` 追加。§21 に専用の楽観的ロックハンドリングコード追加 |
| H6 | PII 匿名化ソルト付き SHA-256 | ✅ **修正確認** | §29 に「ソルト付き SHA-256 ハッシュ値」「`HMACSHA256(customerId, salt)`」「ソルトは Azure Key Vault（`KeyVault:DsrHashSalt`）で管理」が明記。spec.md の DSR 処理要件と整合 |
| H7 | transactions 監査カラム | ✅ **修正確認** | transactions テーブルに `created_by VARCHAR(100) NULL` / `updated_by VARCHAR(100) NULL` 追加。Transaction エンティティにも反映済み |
| H8 | ゲスト購入 Saga ステップスキップ | ✅ **修正確認** | §27 に全 9 ステップのゲスト購入時動作テーブル（実行/スキップ/条件）追加。SagaCoordinator の擬似コード付きスキップロジック追加。`SagaStepStatus.Skipped` の記録設計も含む |

---

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
| gRPC | proto3 定義（§36） | Saga gRPC 通信（spec.md） | ✅ |

## 指摘サマリー
| Agent | 判定 | Critical | High | Medium | Low | スコア |
|-------|------|----------|------|--------|-----|--------|
| business-analyst | ✅ | 0 | 0 | 1 | 1 | Pass |
| architect | ✅ | 0 | 0 | 2 | 0 | Pass with Notes |
| tech-lead | ✅ | 0 | 0 | 1 | 0 | Pass with Notes |
| programing-reviewer | ✅ | 0 | 0 | 3 | 1 | Pass with Notes |
| security-reviewer | ✅ | 0 | 0 | 1 | 0 | Pass with Notes |
| dba-reviewer | ✅ | 0 | 0 | 2 | 1 | Pass with Notes |
| qa-manager | ✅ | 0 | 0 | 2 | 0 | Pass with Notes |
| performance-reviewer | ✅ | 0 | 0 | 2 | 1 | Pass with Notes |
| compliance-reviewer | ✅ | 0 | 0 | 1 | 0 | Pass with Notes |
| oss-reviewer | ✅ | 0 | 0 | 0 | 1 | Pass |
| release-manager | ✅ | 0 | 0 | 0 | 1 | Pass |
| infra-ops-reviewer | ✅ | 0 | 0 | 1 | 1 | Pass with Notes |
| audit-reviewer | ✅ | 0 | 0 | 1 | 0 | Pass with Notes |
| ux-accessibility-reviewer | ✅ | 0 | 0 | 0 | 1 | Pass |
| **合計** | | **0** | **0** | **17** | **8** | |

## 判定根拠
- 判定ルール適用結果: Critical = 0、High = 0 → ✅ Approved with Notes（Medium/Low のみ）
- Iteration 3 の High 8 件が全て適切に修正されたことを検証済み
- 新規 High 指摘なし。Medium 17 件は実装と並行して対応可能なレベル
- **H1-H8 修正品質**: 全修正が spec.md・AGENTS.md・ADR との整合性を保持しており、修正による新たな矛盾は発生していない

---

## Medium 指摘一覧（改善推奨 — 実装と並行して対応可能）

| # | 重要度 | 出典 Agent | カテゴリ | 対象箇所 | 指摘内容 | 推奨対応 | 状態 |
|---|--------|-----------|---------|---------|----------|----------|------|
| M1 | Medium | architect | イベント設計 | §イベント設計 | **`cart.abandoned` イベントが発行イベントテーブルに未記載**: spec.md のカート放棄メール設計で定義された `cart.abandoned` イベントが、設計書の発行イベントテーブルに含まれていない | 発行イベントテーブルに `cart.abandoned`（トピック: `cart.abandoned`, パーティション: 6, キー: cartId, 購読先: MailSendService）を追加 | 継続 |
| M2 | Medium | architect | BackgroundService | §14 / §34 | **BackgroundService の命名・責務の整理が不足**: `ExpiredCartCleanupService`（§14: 30 日削除）、`CartExpirationService`（§34: 24 時間 EXPIRED 化）、spec.md の `CartCleanupService`（カート放棄メール発行）の 3 つの責務対応が不明確 | §14 冒頭に BackgroundService 一覧テーブル（名称・責務・ポーリング間隔・排他制御方式）を追加 | 継続 |
| M3 | Medium | dba-reviewer | インデックス | §8 | **`payment_methods` テーブルのインデックスが §8 テーブルに未記載**: Fluent API（§30）では `idx_payment_methods_user_id` が定義されているが、§8 のインデックス設計テーブルには含まれていない | §8 のインデックス設計テーブルに `payment_methods \| idx_payment_methods_user_id \| user_id \| ユーザー別決済方法検索` を追加 | 継続 |
| M4 | Medium | dba-reviewer | データ保持 | §19 / §29 | **outbox_events の FAILED イベント DLT 転送設計が不足**: OutboxPublisher で `FAILED` ステータスに遷移したイベントの Dead Letter Topic への転送・アラート設計が §19 に含まれていない | §19 に DLT 転送ロジック（BackgroundService or OutboxPublisher の追加ステップ）を追記 | 継続 |
| M5 | Medium | performance-reviewer | キャッシュ | §26 | **Redis 障害時のレイテンシ影響分析が不足**: Redis → PostgreSQL フォールバック時のレイテンシ増加（~1ms → ~30ms）が Saga ステップ 1 のレイテンシバジェットに収まるかの分析がない | §26 に「Redis 障害時: PostgreSQL 直接アクセスで ~30ms。Saga ステップ 1 の gRPC Deadline 200ms の範囲内で許容可能」等の分析を追加 | 継続 |
| M6 | Medium | performance-reviewer | Kafka | §イベント設計 | **`payment.completed` のパーティション数 12 の選定根拠が不明**: パーティション数の決定基準（ピーク TPS、Consumer 並列度等）が未記載 | §イベント設計に選定根拠（ピーク時 TPS、Consumer 並列度、将来成長）を追記 | 継続 |
| M7 | Medium | business-analyst | ビジネスロジック | §25 | **配送料計算の離島対応が未定義**: 離島（沖縄離島、小笠原等）の追加料金が未考慮 | §25 に「離島: Phase 1 では一律 1,100 円、Phase 2 で郵便番号ベースの離島判定を実装」を追記 | 継続 |
| M8 | Medium | compliance-reviewer | 電子帳簿 | §29 | **コールドストレージの具体設計が不足**: 「7 年保持後コールドストレージにアーカイブ」の Azure Blob Storage Tier・形式・アクセス方法が未定義 | §29 に「Azure Blob Storage Archive Tier、JSON Lines 形式、法的要請時に Rehydrate（最大 15 時間）」を追記 | 継続 |
| M9 | Medium | infra-ops-reviewer | Dockerfile | §12 | **Dockerfile の EXPOSE 5005 が本番非推奨**: Azure Container Apps / ASP.NET Core のデフォルト 8080 と乖離 | `EXPOSE 8080` + `ENV ASPNETCORE_URLS=http://+:8080` に変更 | 継続 |
| M10 | Medium | programing-reviewer | コード品質 | §21 | **PaymentMethod.AccountReference が Nullable だが DB スキーマは NOT NULL**: エンティティ `string? AccountReference` と DB `NOT NULL` の不整合で EF Core マイグレーション不整合が発生する | `[Required] public string AccountReference { get; set; } = string.Empty;` に修正 | 継続 |
| M11 | Medium | programing-reviewer | コード品質 | §23 | **CheckoutRequest DTO にフィールド欠落**: API 設計セクションの JSON 例には `shippingAddress`、`couponCode`、`usedPoints` があるが §23 の record 定義に含まれていない | §23 の `CheckoutRequest` に `ShippingAddress?`、`string? CouponCode`、`int UsedPoints = 0` を追加 | 継続 |
| M12 | Medium | qa-manager | テスト | §28 | **Stripe テストキーの環境変数管理方法が未記載**: ローカル開発（`dotnet user-secrets`）/ CI/CD（GitHub Actions Secrets）の区分が未定義 | §28 に「Stripe テストキー管理: ローカル → `dotnet user-secrets`、CI/CD → GitHub Actions Secrets（`STRIPE_TEST_SECRET_KEY`）」を追記 | 継続 |
| M13 | Medium | qa-manager | テスト | §28 | **Saga 決済タイムアウトのテストケースが欠落**: Stripe API タイムアウト時の `PENDING_PAYMENT` 状態遷移テストが §28 テストケース一覧に含まれていない | テストケースに「Saga 決済タイムアウト: Stripe API タイムアウト → PENDING_PAYMENT 状態遷移 → SagaRecoveryService ポーリング」を追加 | 継続 |
| M14 | Medium | tech-lead | 全体設計 | §25 | **消費税率の取得元が未定義**: `ITaxCalculator` で `taxRate` パラメータを受け取るが、取得元（`appsettings.json` / DB / 外部サービス）が未定義。消費税率変更への対応設計が不足 | §25 に「`TaxSettings:StandardRate`（10%）/ `TaxSettings:ReducedRate`（8%）を `appsettings.json` から取得。軽減税率判定は InventoryManagementService の `Product.TaxCategory`」を追記 | 継続 |
| M15 | Medium | security-reviewer | 認証 | §31 | **カート API の IDOR 防止ロジックの明示不足**: `ResolveCartId` でログイン済みユーザーは `userId` を返し Cookie を無視する設計だが、「ログイン済みの場合は Cookie の CartId を無視する」ことの意図が明示されていない | §31 の `ResolveCartId` コメントに「セキュリティ: ログイン済みユーザーが Cookie の CartId を操作して他ユーザーのカートにアクセスすることを防止するため、Cookie を無視し userId ベースで解決する」を追記 | 継続 |
| M16 | Medium | audit-reviewer | 監査 | §10 | **決済ステータス変更の監査ログが未設計**: payments テーブルのステータス遷移（PENDING→COMPLETED 等）は金融監査上重要だが、AuditLog への記録設計が明示されていない | §10 に「payment_status_changed 監査イベント: 全ステータス変更を AuditLog に記録（旧ステータス、新ステータス、変更者、タイムスタンプ）」を追加 | 継続 |
| M17 | Medium | programing-reviewer | コード品質 | §33 | **例外ハンドラーの switch 式に `UnauthorizedException` / `ForbiddenException` が欠落（新規）**: §33 のログ分岐 `if` では `UnauthorizedException` を Warning 扱いにしているが、switch 式に `UnauthorizedException` / `ForbiddenException` のケースがなく、ワイルドカード `_` に落ちて HTTP 500 を返す。AGENTS.md §4.7 では 401/403 を返すべきと定義 | switch 式に `UnauthorizedException => TypedResults.Problem(statusCode: 401)` と `ForbiddenException => TypedResults.Problem(statusCode: 403)` を追加。ログ分岐の `if` にも `ForbiddenException` / `ConcurrencyException` を追加 | **新規** |

---

## Low 指摘一覧（軽微・推奨）

| # | 重要度 | 出典 Agent | カテゴリ | 対象箇所 | 指摘内容 | 推奨対応 | 状態 |
|---|--------|-----------|---------|---------|----------|----------|------|
| L1 | Low | dba-reviewer | 命名 | §DB スキーマ | `transactions` テーブル名が汎用的。`payment_transactions` の方がドメインコンテキスト明確 | Phase 2 で検討 | 継続 |
| L2 | Low | performance-reviewer | キャッシュ | §26 | ログイン時マージ後の旧 Redis キー（`cart:session:{sessionId}`）の削除タイミングが未記載 | §26 に削除タイミングを追記 | 継続 |
| L3 | Low | business-analyst | UX | §API 設計 | カートレスポンスに `remainingMinutes` を返すとフロントエンド実装が容易 | レスポンス DTO に `int? RemainingMinutes` の追加検討 | 継続 |
| L4 | Low | oss-reviewer | 依存関係 | §2 | `Stripe.net 46.*` のメジャーバージョン固定の妥当性確認 | バージョンポリシーを確認し理由をコメント追記 | 継続 |
| L5 | Low | programing-reviewer | コード品質 | §33 | HttpClient 登録の `"InventoryService"` が文字列リテラル。定数化推奨 | `const string InventoryServiceClient = "InventoryService";` | 継続 |
| L6 | Low | release-manager | CI/CD | §12 | 単体テストと統合テストの分離実行（`--filter Category=Unit/Integration`）が CI パイプラインに未反映 | CI パイプラインのステップ分離 | 継続 |
| L7 | Low | ux-accessibility-reviewer | フロントエンド連携 | 全体 | spec.md §注文確認画面 UI 要件へのクロスリファレンスが不足 | §27 にクロスリファレンス追加 | 継続 |
| L8 | Low | infra-ops-reviewer | ヘルスチェック | §33 | Stripe API のヘルスチェックが Readiness プローブに未含 | Stripe API ステータス確認を Readiness に追加検討 | 継続 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 | 状態 |
|---|--------|-----------|------|-----------|------|
| E1 | 高優先 | architect | ADR-0009 のステップ番号を spec.md に合わせて改訂するか、注記のみとするかの判断。**§20 の SSOT 注記追加（H1 修正）により設計書側は解決済み。ADR-0009 本体の改訂要否は別途判断** | テックリード | 継続（設計書側は解決） |
| E2 | 高優先 | compliance-reviewer | DSR 処理の仮名化データ 7 年保持の法的妥当性。spec.md に TODO として残存。**§29 のソルト付き SHA-256 修正（H6 修正）により技術設計は解決済み** | 法務チーム + テックリード | 継続（技術側は解決） |
| E3 | 通常 | security-reviewer | Stripe Webhook IP ホワイトリストの実装箇所（API Gateway vs Azure Container Apps）。**§15 に Azure Container Apps のインバウンドルール設定例が追加（H3 修正）済みだが、最終方式選定は要判断** | インフラチーム | 継続（設計例は追加済み） |

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
| Kafka トピック（発行） | ⚠️ | `payment.completed/failed/refunded` は一致。`cart.abandoned` が発行イベントテーブルに未記載（M1） |
| Kafka トピック（購読） | ✅ | `order.created`, `order.cancelled`, `user.deleted` — spec.md と一致 |
| Saga ステップの役割 | ✅ | ステップ 1/6/8 — spec.md と一致。**SSOT 注記あり（H1 修正済み）** |
| gRPC proto 定義 | ✅ | **§36 に完全な `.proto` 定義追加（H2 修正済み）** |
| Webhook 多層防御 | ✅ | **§15 に IP 制限 + レート制限 + 署名検証の 3 層設計（H3/H4 修正済み）** |
| Write-Through パターン | ✅ | Redis キャッシュ + PostgreSQL 永続化 — spec.md と一致 |
| カート TTL | ✅ | Redis 7 日 / PostgreSQL 30 日 — spec.md と一致 |
| PCI DSS 非保持化 | ✅ | Stripe Checkout (Hosted Payment Page) / SAQ A — ADR-0008 と一致 |
| Outbox パターン | ✅ | 動的バックオフ（100ms〜5s）— ADR-0005 / AGENTS.md §10.4 と一致 |
| Payment 楽観的ロック | ✅ | **RowVersion 追加（H5 修正済み）** |
| DSR 処理（PII 匿名化） | ✅ | **ソルト付き SHA-256 + Azure Key Vault（H6 修正済み）** |
| transactions 監査カラム | ✅ | **created_by / updated_by 追加（H7 修正済み）** |
| ゲスト購入 Saga 統合 | ✅ | **ステップスキップロジック詳細設計（H8 修正済み）** |
| べき等性設計 | ✅ | Stripe `IdempotencyKey` — spec.md と整合 |
| 電子帳簿保存法 | ✅ | 7 年保持 — spec.md と一致 |
| Advisory Lock ID | ✅ | `CartCleanupService` → `hashtext('cart_cleanup')` — spec.md 準拠 |
| ミドルウェアパイプライン順序 | ✅ | AGENTS.md §11.3 の順序を厳守 |

### 設計書の充実度評価

| セクション | 充実度 | 前回比 | 備考 |
|-----------|--------|--------|------|
| 概要・技術スタック | ★★★★★ | → | 完全 |
| データモデル | ★★★★★ | ↑ | Payment RowVersion 追加（H5）、transactions 監査カラム追加（H7） |
| API 設計 | ★★★★★ | ↑ | gRPC proto 完全定義追加（H2） |
| イベント設計 | ★★★★☆ | → | `cart.abandoned` 未記載（M1） |
| セキュリティ | ★★★★★ | ↑ | Webhook 多層防御（H3/H4）、PII ソルト付きハッシュ（H6） |
| Saga 統合 | ★★★★★ | ↑ | SSOT 注記（H1）、ゲスト Saga スキップ（H8） |
| EF Core エンティティ | ★★★★★ | → | Fluent API + IHasTimestamps 完備 |
| Endpoint 実装 | ★★★★★ | → | Cart/Payment 完全パターン |
| FluentValidation | ★★★★★ | → | 全 DTO にバリデーター定義 |
| Program.cs | ★★★★☆ | → | 例外ハンドラーの switch 欠落（M17） |
| テスト戦略 | ★★★★☆ | → | Saga タイムアウトテスト不足（M13） |
| Dockerfile / CI/CD | ★★★★☆ | → | EXPOSE ポート（M9） |
| Outbox パターン | ★★★★★ | → | 動的バックオフ + Advisory Lock 完備 |
| 価格計算 | ★★★☆☆ | → | 税率取得元・離島（M7/M14） |
| データ保持 | ★★★★☆ | ↑ | PII 匿名化は解決（H6）、アーカイブ先詳細は未定義（M8） |
| gRPC proto | ★★★★★ | ↑ | §36 に完全定義（H2） |
| ゲスト購入フロー | ★★★★★ | ↑ | Saga スキップロジック完全設計（H8） |

### 未定義・曖昧な領域

| 領域 | 影響度 | 対応推奨時期 |
|------|--------|------------|
| ~~gRPC メッセージ型~~ | ~~High~~ | ✅ **H2 で解決** |
| ~~ゲスト購入 Saga ステップスキップ~~ | ~~High~~ | ✅ **H8 で解決** |
| 消費税率の取得元・軽減税率対応 | Medium | 実装時（M14） |
| 離島配送料の計算ロジック | Medium | Phase 2（M7） |
| コールドストレージの具体設計 | Medium | Phase 2（M8） |
| `cart.abandoned` イベント定義 | Medium | 実装前（M1） |
| 例外ハンドラーの switch 不整合 | Medium | 実装前（M17） |

---

## 各 Agent 詳細レポート

<details>
<summary>architect レビューレポート</summary>

### 観点: アーキテクチャ設計・サービス境界・DDD パターン

**評価**: ✅ Approved with Notes（Medium 2 件）

**良い点**:
- Aggregate Root（Cart, Payment）の定義が spec.md と完全一致
- Outbox パターン・Write-Through キャッシュの設計が詳細かつ ADR-0005 準拠
- **[H1 修正確認]** §20 の SSOT 注記が適切。spec.md のステップ番号と ADR-0009 の差異が明確化
- **[H2 修正確認]** §36 の gRPC proto 完全定義が高品質。`amount_minor_units` (int64) による通貨精度処理も適切
- **[H8 修正確認]** §27 のゲスト Saga スキップロジックが詳細で、SagaCoordinator との責務境界も明確
- SalesManagementService との Saga 統合の責務境界が明確
- BackgroundService のリーダー選出（Advisory Lock）が設計済み

**指摘**:
- M1: `cart.abandoned` イベントが発行イベントテーブルに含まれていない（継続）
- M2: BackgroundService（ExpiredCartCleanupService / CartExpirationService / CartCleanupService）の命名・責務整理が必要（継続）
</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 観点: OWASP Top 10・PCI DSS・認証認可・秘密情報管理

**評価**: ✅ Approved with Notes（Medium 1 件）

**良い点**:
- PCI DSS SAQ A 準拠の Stripe Checkout 設計が完全
- **[H3 修正確認]** Webhook 多層防御（L1:IP 制限, L2:レート制限, L3:署名検証）が適切に設計。Azure Container Apps の IP 制限 JSON 例も実用的
- **[H4 修正確認]** Webhook レート制限（100 回/分）が §24 に追加され、エンドポイントに `.RequireRateLimiting("webhook")` が適用済み
- **[H6 修正確認]** PII 匿名化が spec.md 準拠（ソルト付き SHA-256 + Azure Key Vault）。`HMACSHA256` とキーパスまで明記
- IDOR 防止パターンが全エンドポイントに適用
- Cookie 設定（HttpOnly, Secure, SameSite=Strict）が適切
- Stripe API キーの環境変数管理が明示

**指摘**:
- M15: `ResolveCartId` のログイン済みユーザー Cookie 無視の意図（IDOR 防止）がコメントで明示されていない（継続）
</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 観点: DB スキーマ設計・EF Core マッピング・インデックス・マイグレーション

**評価**: ✅ Approved with Notes（Medium 2 件, Low 1 件）

**良い点**:
- スキーマ定義が snake_case 命名規約に完全準拠
- CHECK 制約（status IN (...), amount > 0, quantity > 0）が全テーブルに適切に定義
- Fluent API で全インデックス・リレーション・制約が網羅
- 部分インデックス（`idx_carts_expired`, `idx_outbox_events_pending`）の設計が優秀
- **[H5 修正確認]** payments テーブルに `row_version BYTEA NOT NULL` が追加。Payment エンティティにも `[Timestamp]` 適用。Fluent API で `entity.Property(p => p.RowVersion).IsRowVersion()` が設定済み
- **[H7 修正確認]** transactions テーブルに `created_by`/`updated_by` 監査カラムが追加。エンティティにも反映

**指摘**:
- M3: §8 のインデックス設計テーブルに payment_methods のインデックスが未記載（Fluent API §30 では定義済み）（継続）
- M4: outbox_events の FAILED イベント DLT 転送設計が不足（継続）
- L1: `transactions` テーブル名が汎用的（継続）
</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 観点: パフォーマンス SLA・スケーラビリティ・キャッシュ戦略

**評価**: ✅ Approved with Notes（Medium 2 件, Low 1 件）

**良い点**:
- Saga ステップ別レイテンシバジェットへの整合性が取れている
- Write-Through キャッシュの Read/Write フロー設計が詳細
- キーセットページネーションの採用
- `AsNoTracking()` の徹底（読み取り最適化）
- レート制限（checkout: 10/min, cart: 60/min, refund: 5/hour, **webhook: 100/min [H4 修正]**）が負荷防止に適切

**指摘**:
- M5: Redis 障害時のレイテンシ影響分析が不足（継続）
- M6: Kafka パーティション数 12 の選定根拠が不明（継続）
- L2: カートマージ後の Redis キー削除タイミングが未記載（継続）
</details>

<details>
<summary>business-analyst レビューレポート</summary>

### 観点: ビジネス要件の完全性・ユーザーストーリー・受入基準

**評価**: ✅ Approved with Notes（Medium 1 件, Low 1 件）

**良い点**:
- カート離脱率 30% 以下の KPI に対応するカート放棄メール設計への言及
- **[H8 修正確認]** ゲスト購入フローの Saga 統合が詳細に設計され、ゲストユーザーの購入体験が明確化
- 適切なカート有効期限管理（Redis 7 日 / PostgreSQL 30 日）
- 決済方法の拡張計画（Apple Pay / Google Pay / コンビニ決済）

**指摘**:
- M7: 離島配送料の計算ロジックが未定義（継続）
- L3: カートレスポンスの残り時間フィールド提案（継続）
</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 観点: GDPR・個人情報保護法・PCI DSS・電子帳簿保存法

**評価**: ✅ Approved with Notes（Medium 1 件）

**良い点**:
- PCI DSS 非保持化方針（ADR-0008）の徹底
- `user.deleted` イベント購読による GDPR 対応
- **[H6 修正確認]** PII 匿名化が spec.md の DSR 処理要件と完全一致。ソルト付き SHA-256 + HMACSHA256 + Azure Key Vault のキーパスまで明記。レインボーテーブル攻撃への対策が十分
- 電子帳簿保存法に基づく 7 年保持の設計

**指摘**:
- M8: コールドストレージの Azure Blob Storage Tier・形式・アクセス方法が未定義（継続）
</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 観点: コンテナ設計・可観測性・ヘルスチェック・DR

**評価**: ✅ Approved with Notes（Medium 1 件, Low 1 件）

**良い点**:
- Dockerfile がマルチステージビルド + 非 root ユーザー + HEALTHCHECK で適切
- OpenTelemetry 統合設定が完全
- ヘルスチェック（/health + /health/ready）が PostgreSQL・Redis を確認
- Serilog + Correlation ID による構造化ログ設計
- **[H3 修正確認]** Azure Container Apps の IP 制限設定（`ipSecurityRestrictions`）が JSON 例付きで追加。運用注意（IP リスト定期更新）も記載

**指摘**:
- M9: Dockerfile EXPOSE 5005 → 8080 推奨（継続）
- L8: Stripe API ヘルスチェックが Readiness プローブに未含（継続）
</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 観点: NuGet パッケージライセンス・脆弱性・禁止パッケージ

**評価**: ✅ Approved（Low 1 件）

**良い点**:
- 全パッケージが AGENTS.md の推奨パッケージと一致
- `Newtonsoft.Json` 不使用、プレリリース版パッケージなし
- `FluentValidation.DependencyInjectionExtensions` が正しく使用

**指摘**:
- L4: `Stripe.net 46.*` のバージョン指定の妥当性確認（継続）
</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 観点: C# 14 機能活用・コーディング規約・禁止パターン

**評価**: ✅ Approved with Notes（Medium 3 件, Low 1 件）

**良い点**:
- primary constructor の適切な使用
- record 型による DTO 定義（不変性保証）
- `CancellationToken ct = default` の全 async メソッドへの適用
- `TimeProvider` DI によるテスタブルな時刻管理
- **[H5 修正確認]** Payment エンティティの楽観的ロックハンドリングコードが適切（`DbUpdateConcurrencyException` → `ConcurrencyException`）
- **[H8 修正確認]** §27 のゲスト Saga 擬似コードが C# のパターンマッチング（`is null`, `is not null`）を活用

**指摘**:
- M10: PaymentMethod.AccountReference の Nullable/NOT NULL 不整合（継続）
- M11: CheckoutRequest DTO のフィールド欠落（継続）
- M17: 例外ハンドラーの switch に UnauthorizedException/ForbiddenException が欠落（**新規**）
- L5: HttpClient 名の文字列リテラル定数化（継続）
</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 観点: テスト戦略・カバレッジ目標・テストケース充実度

**評価**: ✅ Approved with Notes（Medium 2 件）

**良い点**:
- テストメソッド命名（`Should_/When_`）が規約準拠
- AAA パターンの徹底
- §35 のテストケース実装例が実用的
- **[H5 修正確認]** 楽観的ロックの ConcurrencyException テストが §28 に含まれている

**指摘**:
- M12: Stripe テストキーの環境変数管理が未記載（継続）
- M13: Saga 決済タイムアウトの PENDING_PAYMENT 状態遷移テストが欠落（継続）
</details>

<details>
<summary>release-manager レビューレポート</summary>

### 観点: リリース戦略・CI/CD・ロールバック計画

**評価**: ✅ Approved（Low 1 件）

**良い点**:
- GitHub Actions CI/CD パイプラインが定義済み
- Azure Container Registry + Container Apps デプロイフローが明確

**指摘**:
- L6: 単体テスト/統合テストの分離実行（継続）
</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 観点: 技術標準の横断適合性・実装実現可能性・規約遵守

**評価**: ✅ Approved with Notes（Medium 1 件）

**良い点**:
- **[H1 修正確認]** SSOT 注記が spec.md の SSOT 原則を正しく反映
- **[H2 修正確認]** gRPC proto 定義が Saga のステップ 1/6/8 の責務を正確に表現
- **[H8 修正確認]** ゲスト Saga スキップロジックは SalesManagementService の `SagaCoordinator` 責務であることが明確化され、PaymentCartService の責務境界が維持されている
- 全体として AGENTS.md / Instructions との整合性が高い
- Iteration 3 → 4 の修正品質が高く、修正による新たな矛盾は発生していない

**指摘**:
- M14: 消費税率の取得元が未定義（継続）
</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 観点: トレーサビリティ・ドキュメント整合性・ADR 準拠

**評価**: ✅ Approved with Notes（Medium 1 件）

**良い点**:
- **[H1 修正確認]** ADR-0009 との関係が SSOT 注記で明確化。トレーサビリティが確保
- **[H6 修正確認]** PII 匿名化の実装詳細（HMACSHA256 + KeyVault パス）が監査可能な粒度で記載
- **[H7 修正確認]** transactions テーブルの監査カラム（created_by/updated_by）追加により、決済トランザクションの操作者追跡が可能に
- OpenTelemetry + Serilog + Correlation ID による分散トレーシング設計が完全
- Outbox パターンのイベント追跡（created_at / published_at / retry_count / status）が監査要件を満たす

**指摘**:
- M16: 決済ステータス変更の AuditLog 記録設計が明示されていない（継続）
</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 観点: UX 設計品質・フロントエンド連携

**評価**: ✅ Approved（Low 1 件）

**良い点**:
- エラーレスポンスが RFC 9457（Problem Details）準拠で、フロントエンドのエラーハンドリングが容易
- **[H8 修正確認]** ゲスト購入フローが詳細化され、ゲストユーザーの UX が明確に

**指摘**:
- L7: spec.md §注文確認画面 UI 要件へのクロスリファレンス不足（継続）
</details>

---

## イテレーション間の品質推移

| イテレーション | Critical | High | Medium | Low | 判定 |
|-------------|----------|------|--------|-----|------|
| 1 | — | — | — | — | — |
| 2 | — | — | — | — | — |
| 3 | 0 | 8 | 16 | 8 | ⚠️ Conditional |
| **4（今回）** | **0** | **0** | **17** | **8** | **✅ Approved with Notes** |

- High: 8 → 0（全 8 件修正済み）
- Medium: 16 → 17（16 件継続 + 1 件新規 M17）
- Low: 8 → 8（全件継続）
- **新規指摘**: M17（例外ハンドラーの switch 式に UnauthorizedException/ForbiddenException 欠落）

---

## 次のアクション推奨

1. **実装フェーズへの進行を推奨**: Critical/High = 0 のため、設計書品質は実装着手に十分
2. **実装時に並行対応する Medium 項目**:
   - M10（AccountReference Nullable 不整合）: エンティティ作成時に修正
   - M11（CheckoutRequest DTO フィールド欠落）: DTO 作成時に修正
   - M17（例外ハンドラー switch 欠落）: Program.cs 作成時に修正
   - M1（cart.abandoned イベント）: イベント設計時に追加
3. **Phase 2 以降で対応する項目**: M7（離島配送料）、M8（コールドストレージ）
