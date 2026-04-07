# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/sales-management-design.md`
- **判定**: ⚠️ **Conditional Approval** — Critical 指摘 1 件 + High 指摘 7 件
- **レビュー日時**: 2026-04-03
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

---

## 技術スタック検証結果

| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| 言語 | C# 14 (.NET 10) | C# 14 (.NET 10) | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| ORM | EF Core 10 | EF Core 10 | ✅ |
| DB | PostgreSQL (salesdb) | PostgreSQL | ✅ |
| メッセージング | Confluent.Kafka 2.* | Confluent.Kafka 2.* | ✅ |
| キャッシュ | StackExchange.Redis 2.* | StackExchange.Redis 2.* | ✅ |
| 耐障害性 | Polly 8.* + Microsoft.Extensions.Http.Resilience 9.* | 同左 | ✅ |
| ログ | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| テスト | xUnit + NSubstitute + Shouldly + Testcontainers | 同左 | ✅ |
| オーケストレーション | .NET Aspire 13.1 | .NET Aspire 13.1 | ✅ |
| コンテナ | Docker 25.x | Docker 25.x | ✅ |
| gRPC | Grpc.AspNetCore 2.* | — (AGENTS.md に明記なし) | ⚠️ 設計書独自追加（spec.md で定義済み） |
| Excel 生成 | ClosedXML 0.104.* | — | ❌ プレリリース版（後述） |

---

## 指摘サマリー

| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ⚠️ | 0 | 1 | 2 | 1 |
| architect | ⚠️ | 1 | 2 | 1 | 0 |
| programing-reviewer | ⚠️ | 0 | 1 | 2 | 1 |
| dba-reviewer | ⚠️ | 0 | 1 | 2 | 1 |
| security-reviewer | Pass | 0 | 0 | 2 | 1 |
| compliance-reviewer | ⚠️ | 0 | 1 | 1 | 0 |
| audit-reviewer | Pass | 0 | 0 | 1 | 1 |
| qa-manager | Pass | 0 | 0 | 2 | 0 |
| performance-reviewer | Pass | 0 | 0 | 1 | 1 |
| infra-ops-reviewer | Pass | 0 | 0 | 1 | 1 |
| release-manager | Pass | 0 | 0 | 1 | 0 |
| oss-reviewer | ⚠️ | 0 | 1 | 0 | 0 |
| ux-accessibility-reviewer | Pass | 0 | 0 | 0 | 1 |
| tech-lead | ⚠️ | 0 | 0 | 1 | 0 |
| **合計** | | **1** | **7** | **17** | **8** |

---

## 判定根拠

- **判定ルール適用結果**: Critical 指摘 1 件（`orders.status` CHECK 制約の spec.md との不整合）が検出されたため、自動判定では ❌ Rejected 相当。ただし、当該 Critical は **spec.md 側の CHECK 制約が設計書の状態遷移図と一致しない（spec.md 側が古い）** ことに起因しており、設計書側の定義が正しい可能性が高いため、⚠️ Conditional Approval として人間の判断を求める。
- **最も重大な指摘**: spec.md の `orders.status` CHECK 制約に `INVENTORY_SHORTAGE`, `PAYMENT_FAILED`, `PENDING_PAYMENT` が含まれていない。設計書の状態遷移図・Saga フローでは明確にこれらのステータスが使われているため、**spec.md 側の更新が必要**。

---

## 🚨 Critical/High 指摘一覧（修正必須）

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|--------------|----------|----------|
| C-1 | **Critical** | architect, dba-reviewer | データ整合性 | §4 データモデル / spec.md CHECK 制約 | **orders.status CHECK 制約の spec.md との不整合**: spec.md（§販売管理サービス CHECK 制約）は `CHECK (status IN ('PENDING', 'CONFIRMED', 'PROCESSING', 'SHIPPED', 'DELIVERED', 'CANCELLED', 'RETURNED', 'REFUNDED'))` の **8 ステータス** のみを定義。設計書は `INVENTORY_SHORTAGE`, `PAYMENT_FAILED`, `PENDING_PAYMENT` を加えた **11 ステータス** を定義。状態遷移図・Saga フロー・OrderStateMachine 実装は 11 ステータスを前提としており、spec.md 側が古い。 | **spec.md の orders.status CHECK 制約を 11 ステータスに更新する**。設計書側が Saga フローと整合する正しい定義。 |
| H-1 | **High** | architect | 設計整合性 | §7 Saga / ADR-0009 | **ADR-0009 の Saga ステップ数が古い**: ADR-0009 は 6 ステップ構成だが、設計書・spec.md は 9 ステップ（補償対象 6 + 後処理 3）を定義。設計書内の注記で「spec.md の 9 ステップが最新」と記載されているが、ADR-0009 自体が更新されていないため、ADR の正式な承認ステータスと齟齬が生じている。 | ADR-0009 を 9 ステップ構成に更新し、改訂履歴を追記する。 |
| H-2 | **High** | architect | Aggregate Root 定義 | §4 ER 図 / spec.md DDD パターン | **Shipment の Aggregate Root 定義と子エンティティの不一致**: spec.md は `Shipment` Aggregate Root の子エンティティとして `ShipmentItem` を定義しているが、設計書の Shipment エンティティに `ShipmentItem` は存在しない。設計書判断（単一配送のため不要）が正しい場合、spec.md 側を修正する必要がある。 | spec.md の Aggregate Root 一覧で `Shipment` の子エンティティから `ShipmentItem` を削除するか、設計書に `ShipmentItem` を追加する（設計方針に応じて決定）。 |
| H-3 | **High** | oss-reviewer | NuGet 依存関係 | §2 主要ライブラリ | **ClosedXML 0.104.* はプレリリース版**: AGENTS.md で `プレリリース版（-preview, -beta, -rc）の NuGet パッケージを本番ブランチに含めること` は禁止事項。ClosedXML 0.104 は GA 版未到達のバージョン。設計書内でも「GA 版リリース後に移行予定。代替として EPPlus 7.* を検討」と記載。 | ClosedXML を EPPlus 7.* に変更するか、ClosedXML の GA 版リリース待ちとして Phase 1 のスコープからレポートエクスポート機能を除外する。 |
| H-4 | **High** | business-analyst | ゲスト購入対応 | §4 Order エンティティ | **ゲスト購入用カラム（`is_guest`, `guest_email`）未定義**: spec.md のゲスト購入フロー設計では `orders.is_guest`（bool）と `orders.guest_email`（AES-256 暗号化）が必要とされているが、設計書の orders テーブル定義・Order エンティティ C# クラスにこれらのカラムが存在しない。 | orders テーブルに `is_guest BOOLEAN DEFAULT false` と `guest_email VARCHAR(255) NULL` を追加。Order エンティティの C# クラスにも対応プロパティを追加。 |
| H-5 | **High** | architect, programing-reviewer | Saga 設計 | §7 Saga パターン | **ORDER_CANCEL Saga と ORDER_RETURN Saga の未詳細化**: `saga_logs.saga_type` CHECK 制約で `'ORDER_CANCEL'`, `'ORDER_RETURN'` が定義されているが、これらの Saga のステップ詳細・補償トランザクション設計・シーケンス図が設計書内に存在しない。spec.md には注文キャンセル・返品フローのシーケンス図が記載されているが、設計書側で 9 ステップ相当の詳細設計が不足。 | ORDER_CANCEL Saga（キャンセルフロー）と ORDER_RETURN Saga（返品フロー）のステップ詳細・補償設計・Deadline 設計を追加する。最低限、ステップ構成テーブルと補償テーブルを記載する。 |
| H-6 | **High** | dba-reviewer | DB スキーマ | §4 orders テーブル | **spec.md のエンティティ属性との不一致**: spec.md の Order エンティティは `userId`, `shippingAddressId`, `billingAddressId`, `paymentId` を定義しているが、設計書は `customer_id`（userId に相当）と**インライン配送先住所カラム**（`shipping_postal_code`, `shipping_prefecture` 等）を使用。設計書のインライン方式は ADR-0006（DB per Service）準拠で合理的だが、spec.md との命名不一致と `billingAddressId`/`paymentId` の省略が未説明。 | spec.md 側を設計書に合わせて更新するか、設計書内で spec.md との差異を明示的に記載する（例：「spec.md の `shippingAddressId` はインライン化した」旨の注記）。 |
| H-7 | **High** | compliance-reviewer | データ保護 | §6 イベント設計 / §10 セキュリティ | **`user.deleted` イベント購読時の匿名化処理の不完全性**: 設計書は `customer_id` のハッシュ化と配送先の `[DELETED]` 置換を記載しているが、**`guest_email`**（ゲスト購入対応で追加予定のカラム）の匿名化が未記載。また、`order_items.product_snapshot` 内にユーザー固有情報が含まれる可能性（例：名入れ商品の属性）への対応も未記載。 | ゲスト購入カラム追加時に `guest_email` の匿名化処理を明記する。`product_snapshot` 内の PII がないことを確認するか、PII 含有時の匿名化方針を追加する。 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-1 | **最優先** | architect, dba-reviewer | C-1 の spec.md `orders.status` CHECK 制約の更新。spec.md が SSOT であるため、spec.md 側の変更には承認プロセスが必要。 | テックリード |
| E-2 | **高優先** | architect | H-2 の `ShipmentItem` 設計方針の確定。単一配送（Order:Shipment = 1:1）で進めるか、複数配送（1:N）に対応するかの設計判断。 | テックリード + PO |
| E-3 | **高優先** | oss-reviewer | H-3 の ClosedXML 代替判断。EPPlus のライセンス（商用利用は有償ライセンス要）の費用対効果評価が必要。 | テックリード + PO |
| E-4 | **通常** | business-analyst | 返品・キャンセル Saga の詳細設計の優先度。Phase 1 で必要か、Phase 2 に回すか。 | PO |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| 1 | architect（spec.md SSOT 尊重） | dba-reviewer（設計書の 11 ステータスが正しい） | orders.status CHECK 制約：spec.md の 8 ステータス vs 設計書の 11 ステータス | **設計書の 11 ステータスが正しい。spec.md を更新する方向で対応**。 | 状態遷移図、OrderStateMachine、Saga フロー、PENDING_PAYMENT の spec.md 内受入基準（§ペルソナ 1）がすべて 11 ステータスを前提としており、spec.md の CHECK 制約テーブルのみが古い。 |
| 2 | architect（マクロ設計） | dba-reviewer（DB 設計） | Shipment の UNIQUE 制約: `order_id` に UNIQUE がある（1:1）vs spec.md は Shipment を独立 Aggregate Root（1:N 可能）と定義 | **1:1（UNIQUE）を Phase 1 の設計とし、Phase 2 で 1:N 対応時に UNIQUE を撤去する移行パスを明記**。 | Phase 1 スコープでは分割配送は不要。spec.md の Aggregate Root 定義は将来拡張を見据えた設計。 |

---

## ドキュメント横断分析

### マイクロサービス設計書カバレッジ（SalesManagementService 単体）

| 設計領域 | 存在 | 詳細度 | 備考 |
|---------|------|--------|------|
| API 定義 | ✅ | 高 | REST エンドポイント一覧 + リクエスト/レスポンス JSON 例 + Idempotency-Key 設計 |
| DB 設計 | ✅ | 高 | テーブル定義 + CHECK 制約 + インデックス + 部分インデックス SQL + EF Core エンティティクラス |
| イベント定義 | ✅ | 高 | 発行/購読イベント一覧 + Outbox パターン設計 + OutboxPublisher 実装例 |
| Saga 設計 | ✅ | 高 | ORDER_CHECKOUT: 9 ステップ詳細 + 補償 + Deadline + 実装例 |
| セキュリティ | ✅ | 中 | IDOR 防止 + PII マスキング + JWT 認証。ただし FluentValidation 実装例が不足 |
| 非機能要件 | ✅ | 中 | SLO 1,000ms + レイテンシバジェット。RTO/RPO は ADR-0010 参照 |
| テスト戦略 | ✅ | 中 | テスト種別一覧 + Saga テスト・Outbox テストの方針 |
| デプロイ | ✅ | 高 | Dockerfile + CI/CD パイプライン + スケーリング戦略 |
| 運用・保守 | ✅ | 中 | バックアップ + スケーリング + メンテナンスジョブ |
| 監視 | ✅ | 高 | メトリクス定義 + 閾値 + ヘルスチェック実装 |

### サービス間整合性

| チェック項目 | 結果 | 詳細 |
|------------|------|------|
| Saga gRPC サービス定義 | ✅ | InventoryService, CouponService, PointService, CartService, PaymentService の RPC が設計書と spec.md で一致 |
| Kafka トピック名 | ✅ | `order.created`, `order.cancelled`, `order.shipped`, `order.delivered`, `order.status-changed` が spec.md イベントペイロード例と整合 |
| 購読イベント | ✅ | `payment.completed`, `payment.failed`, `inventory.reserved`, `inventory.released`, `user.deleted` が関連サービス設計と整合 |
| Outbox パターン | ✅ | ADR-0005 準拠。動的バックオフ（100ms〜5s）、部分インデックスが一致 |
| Saga Status 値 | ✅ | `CREATED`, `PROCESSING`, `COMPLETED`, `COMPENSATING`, `COMPENSATED`, `FAILED`, `PENDING_PAYMENT` が spec.md と一致 |

### 未定義・曖昧な領域

| # | 領域 | 影響度 | 内容 |
|---|------|--------|------|
| 1 | ORDER_CANCEL Saga 詳細 | High | ステップ構成・補償設計が未記載 |
| 2 | ORDER_RETURN Saga 詳細 | High | ステップ構成・補償設計が未記載 |
| 3 | ゲスト購入カラム | High | `is_guest`, `guest_email` が スキーマ・エンティティに未反映 |
| 4 | Value Object 適用 | Medium | spec.md で定義された `Money`, `PostalAddress`, `Quantity` が設計書のエンティティ・DTO に適用されていない |
| 5 | FluentValidation ルール | Medium | DTO に Data Annotations は付与されているが、FluentValidation の Validator クラス設計が未記載 |
| 6 | レポートサービスのクエリ設計 | Medium | 売上レポート・商品別レポートの具体的な SQL/LINQ クエリ設計が未記載 |
| 7 | 注文番号生成ロジック | Low | `OrderNumberGenerator` がコンポーネント図に存在するが、生成ルール（`ORD-YYYYMMDD-NNNNN` 等）の詳細仕様が未記載 |
| 8 | OutboxPublisher の `ExecuteSqlRawAsync` | Medium | Advisory Lock 取得に `ExecuteSqlRawAsync` を使用しているが、パラメータ化されていない固定文字列のため SQL インジェクションリスクはないが、AGENTS.md の `FromSqlRaw での文字列結合禁止` との整合性を確認する注記が望ましい |

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### ビジネス要件の完全性

**判定**: ⚠️ Conditional

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| BA-H1 | High | ゲスト購入フロー（spec.md §ゲスト購入フロー）に必要な `is_guest`, `guest_email` カラムが設計書の Order エンティティ・DB スキーマに未反映。ゲスト購入は CVR 向上の重要施策であり、Phase 1 スコープ内。 | orders テーブルおよび Order エンティティに `is_guest`, `guest_email` を追加 |
| BA-M1 | Medium | 再注文機能（ペルソナ 1 ユーザーストーリー）に対応する API エンドポイント（`POST /api/v1/orders/{id}/reorder` 等）が設計書に未定義 | 再注文 API を設計するか、Phase 2 スコープ明記 |
| BA-M2 | Medium | 返品可能期間（§15 設定ファイル `Return.AllowedDays: 30`）のビジネスルール適用箇所が Return スキーマ・API に未反映。返品申請時に 30 日を超過した場合のバリデーション仕様が未記載 | ReturnCreateRequest のバリデーションルールに日数チェックを追加 |
| BA-L1 | Low | 法人顧客向け機能（ペルソナ 2）が Phase 2 スコープであることは明記されているが、Invoice テーブルの設計が Phase 1 に含まれており、法人請求書発行との関係性が不明確 | Invoice が Phase 1 の個人顧客向け領収書発行に限定されることを明記 |

</details>

<details>
<summary>architect レビューレポート</summary>

### アーキテクチャ設計品質

**判定**: ⚠️ Conditional (Critical 1 件)

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| AR-C1 | **Critical** | **orders.status CHECK 制約の SSOT 不一致**。設計書の状態遷移図は 11 ステータスを定義するが、spec.md の CHECK 制約は 8 ステータスのみ。`INVENTORY_SHORTAGE`, `PAYMENT_FAILED`, `PENDING_PAYMENT` が spec.md に欠落。Saga フロー・SagaRecoveryService・OrderStateMachine すべてがこれら 3 ステータスに依存しており、spec.md 側が古い。 | spec.md の orders.status CHECK 制約を更新 |
| AR-H1 | High | ADR-0009 の Saga ステップ構成が 6 ステップのまま未更新。設計書・spec.md は 9 ステップ（補償対象 6 + 後処理 3）。ADR は承認済みドキュメントのため改訂が必要 | ADR-0009 を 9 ステップに更新し改訂履歴追記 |
| AR-H2 | High | `ORDER_CANCEL` / `ORDER_RETURN` Saga のステップ詳細が未設計。spec.md のキャンセル・返品シーケンス図は概要レベルであり、Deadline 設計・補償設計・SagaLog 状態遷移が不足 | キャンセル・返品 Saga のステップ詳細を追加 |
| AR-M1 | Medium | spec.md の Aggregate Root 定義で `Shipment` の子エンティティが `ShipmentItem` だが、設計書の Shipment エンティティに子エンティティなし。shipments テーブルに `order_id UNIQUE` 制約があり 1:1 を前提。spec.md との乖離を注記すべき | spec.md との差異を明示的に記載（「Phase 1 は 1:1 配送」） |

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### コード例の正確性・C# 14 機能活用

**判定**: ⚠️ Conditional

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| PR-H1 | High | ORDER_CANCEL / ORDER_RETURN の SagaCoordinator 実装が未記載。ORDER_CHECKOUT の実装例は高品質だが、キャンセル・返品の Saga 実行ループ・補償ロジックがない | 少なくともインターフェース定義とステップ構成を記載 |
| PR-M1 | Medium | OutboxPublisher の `ExecuteSqlRawAsync(AdvisoryLockQuery)` は固定文字列のため安全だが、AGENTS.md の `FromSqlRaw での文字列結合禁止` ルールとの整合性の注記がない。コードレビュー時に誤検知される可能性 | Advisory Lock 用の `ExecuteSqlRawAsync` が固定文字列であり安全である旨のコメントを実装時に追記する旨を設計書に記載 |
| PR-M2 | Medium | spec.md で定義された Value Object（`Money`, `PostalAddress`, `Quantity`）が Order エンティティ・DTO で未使用。`decimal SubtotalAmount` ではなく `Money SubtotalAmount` 等の適用が DDD 原則として期待される | Value Object の適用方針を記載（EF Core マッピング複雑性との兼ね合いを含む） |
| PR-L1 | Low | Order エンティティの `Status` プロパティが `string` 型だが、`OrderStatus` enum が別途定義されている。実装時に enum を使うか string を使うかの方針が不明確 | 「DB カラムは string、C# プロパティは enum、EF Core ValueConverter で変換」等の方針を明記 |

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### DB スキーマ設計品質

**判定**: ⚠️ Conditional

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| DB-H1 | High | spec.md の Order エンティティ属性定義との列名・構造不一致。spec.md は `userId`, `shippingAddressId`, `billingAddressId`, `paymentId` を記載し、設計書は `customer_id` + インライン住所カラム。差異の理由（ADR-0006 準拠でアドレス正規化不要）を設計書に明記すべき | spec.md 側を更新するか、設計書に差異説明を追記 |
| DB-M1 | Medium | `saga_logs.order_id` の FK がテーブル定義には記載されているが、ON DELETE が spec.md（`RESTRICT`）と設計書 AppDbContext（`Cascade`）で異なる。SagaLog はデバッグ・監査用途のため `RESTRICT` が望ましい | AppDbContext の `OnDelete(DeleteBehavior.Cascade)` を `DeleteBehavior.Restrict` に修正 |
| DB-M2 | Medium | `returns` テーブルの `order_id` + `order_item_id` に対する複合インデックスが未定義。返品検索は注文 ID + 明細アイテム ID の組み合わせが頻出パターン | `idx_returns_order_id_order_item_id` の追加を検討 |
| DB-L1 | Low | `outbox_events` テーブルに `DEAD_LETTER` ステータスが定義されているが、Dead Letter への遷移条件と後処理（管理者通知等）の仕様が未記載 | DEAD_LETTER 遷移条件と後処理を運用セクションに追記 |

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### セキュリティ設計品質

**判定**: Pass

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| SEC-M1 | Medium | IDOR 防止パターンは `GET /api/v1/orders/{orderId}` に実装例があるが、管理者向け API（`PUT /api/v1/orders/{orderId}/status`、`PUT /api/v1/returns/{id}/status`）の認可ポリシー（`AdminOnly` 等）がエンドポイント一覧に未記載 | 各管理者向けエンドポイントに `RequireAuthorization("AdminOnly")` を明記 |
| SEC-M2 | Medium | PII ログマスキングルールに `customer_id` → `order_id` の逆引き可能性への対策が未記載。注文 ID から配送先住所への到達パスがログ上で追跡可能 | ログ設計に「注文 ID はログ出力可能だが、注文内容のクエリ結果はログ出力禁止」のルールを追加 |
| SEC-L1 | Low | Dockerfile の `EXPOSE 5004` は内部ポート。HEALTHCHECK の `curl` は `aspnet:10.0` イメージに含まれない可能性がある（`wget` を使用するか、`dotnet` ベースのヘルスチェックを検討） | HEALTHCHECK を `wget -q --spider` に変更するか、`curl` のインストール手順を追加 |

#### 評価ポイント
- JWT 認証の `RequireAuthorization()` が全エンドポイントに適用されている ✅
- Idempotency-Key による二重注文防止が詳細設計されている ✅
- PII ログマスキングテーブルが明確に定義されている ✅
- IDOR 防止の実装例が具体的 ✅

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 法規制・データ保護準拠

**判定**: ⚠️ Conditional

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| COM-H1 | High | `user.deleted` イベント購読時の匿名化対象に `guest_email`（ゲスト購入対応予定カラム）が未考慮。GDPR 第 17 条（忘れられる権利）への対応漏れリスク | ゲスト購入カラム追加時に匿名化対象リストを更新 |
| COM-M1 | Medium | 注文データの法的保持義務（税法 7 年）がイベント購読セクションで一文のみ言及。具体的な保持ポリシー（何年後にアーカイブ or 匿名化 or 削除）がセクションとして未整理 | 「データ保持ポリシー」セクションを追加し、テーブルごとの保持期間・アーカイブ方針を定義 |

#### 評価ポイント
- 配送先住所・電話番号のログ出力禁止が明確 ✅
- `user.deleted` イベントでの匿名化処理フローが定義されている ✅
- PCI DSS 非保持化（ADR-0008）準拠で決済情報を保持しない設計 ✅

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### トレーサビリティ・監査証跡

**判定**: Pass

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| AUD-M1 | Medium | 監査カラム（`created_by`, `updated_by`）が Order と Return に存在するが、OrderItem・Shipment・Invoice には未設定。特に Invoice は電子帳簿保存法対応で `created_by` が必要な可能性 | Invoice に `created_by`, `updated_by` を追加検討 |
| AUD-L1 | Low | Saga の `step_results`（JSONB）のスキーマ定義が未記載。監査時にステップ結果を解析するための構造が不明確 | `step_results` のサンプル JSON 構造を記載 |

#### 評価ポイント
- SagaLog による Saga 実行履歴の完全な記録 ✅
- Correlation ID のサービス間伝搬が設計されている ✅
- 楽観的ロック（RowVersion）による同時更新検出 ✅
- 注文ステータス変更イベント（`order.status-changed`）による変更履歴の外部化 ✅

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### テスト戦略品質

**判定**: Pass

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| QA-M1 | Medium | テスト戦略に OrderStateMachine の状態遷移テスト（全遷移パスの網羅）が明記されていない。16 の有効遷移と不正遷移のテストが必要 | テスト戦略に「OrderStateMachine 全遷移テスト」を追加 |
| QA-M2 | Medium | Idempotency-Key のテストケースが概要のみ。TTL 境界値テスト（23:59:59 / 24:00:01）、並行リクエストテスト（2 リクエストが同時到着）の記載が望ましい | Idempotency-Key のエッジケーステストを追記 |

#### 評価ポイント
- Saga 完走テスト・補償テスト・PENDING_PAYMENT テスト・SagaRecoveryService テストが明記 ✅
- Outbox テスト（動的バックオフ・リトライ・全フロー）が明記 ✅
- 負荷テスト（k6 / NBomber）による SLO 検証が計画 ✅

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### パフォーマンス設計品質

**判定**: Pass

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| PERF-M1 | Medium | レポートクエリ（`GET /api/v1/reports/sales` 等）に対する読み取りレプリカへのルーティング設計が概要のみ。EF Core での読み取り/書き込み DbContext の分離パターンが未記載 | `SalesDbReadOnlyContext` 等のパターンを追記 |
| PERF-L1 | Low | Redis キャッシュの TTL 設計（注文: 1 時間、レポート: 1 日、配送追跡: 30 分）は記載されているが、キャッシュ無効化（注文ステータス変更時のキャッシュ破棄）のトリガー設計が未記載 | キャッシュ無効化トリガーを追記 |

#### 評価ポイント
- SLO 1,000ms のレイテンシバジェットが全 9 ステップで詳細に設計 ✅
- gRPC Deadline がバジェットの 3〜6.7 倍マージンで設定 ✅
- 部分インデックスで OutboxPublisher・SagaRecoveryService のポーリング高速化 ✅
- キーセットページネーションの採用が明記 ✅

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### インフラ・運用設計品質

**判定**: Pass

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| INFRA-M1 | Medium | ヘルスチェック実装で `builder.Configuration.GetConnectionString("salesdb")!` と null 抑制演算子を使用。接続文字列が未設定の場合の起動時エラーハンドリングが不明確 | `?? throw new InvalidOperationException("salesdb 接続文字列が未設定")` を推奨 |
| INFRA-L1 | Low | CI/CD パイプラインに EF Core マイグレーション適用ステップが未記載。`dotnet ef database update` のタイミング（デプロイ前 or コンテナ起動時）の設計が未記載 | マイグレーション適用戦略を追記 |

#### 評価ポイント
- Dockerfile がマルチステージビルド + 非 root ユーザー + バージョン固定で AGENTS.md 準拠 ✅
- minReplicas: 2 + Advisory Lock / SELECT FOR UPDATE SKIP LOCKED による排他制御 ✅
- HEALTHCHECK 設定あり ✅
- ヘルスチェックに PostgreSQL + Redis + Kafka の疎通確認を含む ✅

</details>

<details>
<summary>release-manager レビューレポート</summary>

### リリース戦略品質

**判定**: Pass

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| REL-M1 | Medium | CI/CD パイプラインの `dotnet test` ステップにカバレッジ閾値チェックが未設定。AGENTS.md のカバレッジ目標 80% を自動ゲートとして適用すべき | `dotnet test --collect:"XPlat Code Coverage"` 後にカバレッジ閾値チェックステップを追加 |

#### 評価ポイント
- GitHub Actions ベースの CI/CD パイプラインが定義 ✅
- Azure Container Registry + Azure Container Apps へのデプロイフローが記載 ✅
- path フィルタリング（`SalesManagementService/**`）で不要ビルドを回避 ✅

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### NuGet ライセンス・依存関係

**判定**: ⚠️ Conditional

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| OSS-H1 | High | ClosedXML 0.104.* はプレリリースバージョン。AGENTS.md §8.2 で `-preview`, `-beta`, `-rc` パッケージの本番ブランチ含有が禁止。設計書内でも「GA 版リリース後に移行予定」と認識済み。代替として EPPlus 7.* が記載されているが、EPPlus は商用利用で有償ライセンスが必要（Polyform Noncommercial License 1.0.0）。 | (a) ClosedXML GA 版リリースを待つ（レポートエクスポートを Phase 2 に移動）、(b) EPPlus のライセンス費用を承認して採用、(c) 別の OSS ライブラリ（NPOI 等、Apache License 2.0）を検討 |

#### 評価ポイント
- その他の NuGet パッケージはすべて GA 版で AGENTS.md 準拠 ✅
- `Newtonsoft.Json` 不使用（`System.Text.Json` 使用）✅
- `WebClient` / `HttpWebRequest` 不使用 ✅

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### UX・アクセシビリティ

**判定**: Pass

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| UX-L1 | Low | API レスポンスの日時フォーマットが ISO 8601（`2024-04-15T14:30:25.123Z`）で統一されているが、ユーザー向け表示時のタイムゾーン変換（JST 表示等）の方針がバックエンド設計書では言及されていない（フロントエンド責務だが、`DateTimeOffset` の UTC 保証が明記されている点は良い） | フロントエンド設計書との整合性確認（本設計書の追記は不要） |

#### 評価ポイント
- 注文作成レスポンスに必要な全フィールドが含まれている ✅
- エラーレスポンスが RFC 9457 準拠（Problem Details） ✅

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 技術標準の横断適合性

**判定**: ⚠️ Conditional

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| TL-M1 | Medium | 設計書全体として AGENTS.md・spec.md との整合性は高い。最大の懸念は **spec.md 側の orders.status CHECK 制約の更新漏れ**（C-1）であり、設計書側ではなく spec.md 側の修正で解決可能。本設計書の品質はエンタープライズ水準に達しており、C-1・H-1〜H-7 の修正後に Approved 相当となる。 | 上記 Critical/High の修正を実施 |

#### 総合評価

**強み**:
- 9 ステップ Saga オーケストレーションの設計が高品質（実装例・補償設計・Deadline・SLO バジェットが全てカバー）
- Outbox パターン・SagaRecoveryService・PENDING_PAYMENT 設計が障害復旧を網羅
- EF Core エンティティ・AppDbContext・DTO の実装可能なコード例が豊富
- Idempotency-Key による二重注文防止が DB スキーマ〜API 設計〜処理フローまで一貫
- IDOR 防止・PII マスキング・監査カラム等のセキュリティ設計が AGENTS.md 準拠

**改善必要箇所**:
- spec.md との CHECK 制約不整合の解消（最優先）
- ORDER_CANCEL / ORDER_RETURN Saga の詳細化
- ゲスト購入カラムの追加
- ClosedXML 代替の確定

</details>

---

## レビュー完了

本レポートは `design-docs/sales-management-design.md` に対する全 14 Agent 観点のレビュー結果を統合したものである。**Critical 指摘 1 件**（spec.md 側の orders.status CHECK 制約更新漏れ）と **High 指摘 7 件** の修正後、再レビューを推奨する。
