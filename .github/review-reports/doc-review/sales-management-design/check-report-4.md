# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/sales-management-design.md`
- **判定**: ✅ **Approved with Notes** — Critical/High 指摘 0 件。Medium/Low の改善推奨事項あり
- **レビュー日時**: 2026-04-03
- **イテレーション**: 4（Iteration 3 の修正確認レビュー）
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

---

## 段階的実行モード
- イテレーション: 4 回目
- 実行 Agent: 全 14 Agent（前回は全 Agent に Critical/High/Medium 指摘があり Stable ステータスの Agent なし）
- スキップ Agent（Stable）: なし
- 実行理由: 全量実行（全 Agent が前回 Active）

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
| Excel 生成 | EPPlus 7.*（ClosedXML → EPPlus 方針変更済み） | — | ✅ 注記付きで解決済み |

---

## 前回修正項目の確認結果

| # | 前回指摘 | 修正内容 | 確認結果 | 詳細 |
|---|---------|---------|---------|------|
| C-01 | spec.md `orders.status` CHECK 制約の不整合（8 vs 11） | §17「spec.md 更新提案」セクション追加。11 ステータスの必要性根拠テーブル・影響範囲を明記 | ✅ **修正確認済み** | 設計書内の全定義（ER 図・テーブル定義・AppDbContext・OrderStateMachine・状態遷移図）は 11 ステータスで整合。spec.md 側の更新は別途実施対象として分離されている |
| H-01 | ADR-0009 Saga ステップ数（6 vs 9） | §7 に「ADR-0009 には 6 ステップが記載されているが、spec.md の 9 ステップが最新」注記 + 「ADR-0009 更新提案」ブロック追加 | ✅ **修正確認済み** | 不整合が明示的に文書化され、更新提案として分離されている |
| H-02 | ShipmentItem 省略の設計判断根拠 | shipments テーブル直上に設計判断注記を追加。Phase 1 は 1:1（UNIQUE 制約）、Phase 2 で 1:N 移行パスを明記 | ✅ **修正確認済み** | spec.md との差異と Phase 2 移行パスが具体的 |
| H-03 | ClosedXML → EPPlus 方針変更 | §2 ライブラリテーブルの ClosedXML エントリに EPPlus 7.* への方針変更注記を追加 | ✅ **修正確認済み** | 注記で Phase 1 は EPPlus を使用する方針が明確。ただし文書内に ClosedXML への参照が残存（後述 M-04） |
| H-04 | ゲスト購入カラム追加 | `is_guest`, `guest_email` を ER 図・orders テーブル・Order C# エンティティ・AppDbContext 全てに反映 | ✅ **修正確認済み** | 4 箇所全てで整合。`guest_email` は AES-256-GCM 暗号化保存の注記も追加済み |
| H-05 | ORDER_CANCEL / ORDER_RETURN Saga 詳細化 | §7 に ORDER_CANCEL Saga（6 ステップ）と ORDER_RETURN Saga（5 ステップ）のステップ構成テーブル・補償設計・Deadline を追加 | ✅ **修正確認済み** | キャンセル Saga の返金失敗時の手動対応キュー、返品 Saga の部分返品対応も記載 |
| H-06 | spec.md エンティティ属性差異注記 | shipments テーブル直上に `userId→customer_id`、`shippingAddressId→インライン`, `billingAddressId→Phase 2`, `paymentId→ADR-0008` の設計判断注記を追加 | ✅ **修正確認済み** | ADR-0006/ADR-0008 を根拠とした論理的な説明 |
| H-07 | GDPR 匿名化対象フィールド | §6 購読イベントの `user.deleted` に匿名化対象フィールド一覧テーブル（10 フィールド）を追加。`guest_email` の NULL 化、`notes` の置換、`product_snapshot` の PII 確認方針を明記 | ✅ **修正確認済み** | カスタマイズ属性（名入れ商品等）の `attributes` フィールド対応も記載 |

**結論**: Iteration 3 の C-01 + H-01〜H-07 は全て修正対応が確認できた。修正により新たな Critical/High は発生していない。

---

## 指摘サマリー

| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | Pass | 0 | 0 | 2 | 1 |
| architect | Pass | 0 | 0 | 1 | 0 |
| programing-reviewer | Pass | 0 | 0 | 2 | 1 |
| dba-reviewer | Pass | 0 | 0 | 2 | 1 |
| security-reviewer | Pass | 0 | 0 | 2 | 1 |
| compliance-reviewer | Pass | 0 | 0 | 1 | 0 |
| audit-reviewer | Pass | 0 | 0 | 1 | 1 |
| qa-manager | Pass | 0 | 0 | 2 | 0 |
| performance-reviewer | Pass | 0 | 0 | 1 | 1 |
| infra-ops-reviewer | Pass | 0 | 0 | 1 | 1 |
| release-manager | Pass | 0 | 0 | 1 | 0 |
| oss-reviewer | Pass | 0 | 0 | 0 | 1 |
| ux-accessibility-reviewer | Pass | 0 | 0 | 0 | 1 |
| tech-lead | Pass | 0 | 0 | 0 | 0 |
| **合計** | | **0** | **0** | **16** | **8** |

---

## 判定根拠

- **判定ルール適用結果**: Critical 0 件、High 0 件。Medium/Low のみ → ✅ **Approved with Notes**
- **前回 Critical/High 8 件 → 今回 0 件**: 全修正が正しく適用され、新たな矛盾は発生していない
- **最も注意すべき改善事項**: §6 API 設計セクションに残存する ClosedXML 参照（EPPlus への方針変更が §2 注記で明確だが §6 未反映）

---

## Medium 指摘一覧（改善推奨・判定には影響しない）

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|--------------|----------|----------|
| M-01 | Medium | dba-reviewer | DB スキーマ | §B AppDbContext | **shipments.order_id の UNIQUE 制約が AppDbContext に未反映**: テーブル定義（§4）では `order_id` に `UNIQUE` 制約を明記（Phase 1 の 1:1 制約）。しかし AppDbContext の Shipment 設定では `entity.HasIndex(s => s.OrderId)` のみで `.IsUnique()` が欠落。EF Core マイグレーションで UNIQUE 制約が生成されない | `entity.HasIndex(s => s.OrderId).IsUnique()` を追加。Phase 2 で 1:N 移行時に `.IsUnique()` を削除する移行パスをコメントで明記 |
| M-02 | Medium | dba-reviewer | DB スキーマ | §B AppDbContext | **SagaLog の OnDelete が Cascade のまま**（check-report-3 DB-M1 継続）: SagaLog は監査・デバッグ用途であり、Order 削除時に連鎖削除すべきではない。spec.md の FK 設計は `RESTRICT` | `OnDelete(DeleteBehavior.Cascade)` → `OnDelete(DeleteBehavior.Restrict)` に変更 |
| M-03 | Medium | architect | gRPC 定義 | §7 ORDER_CANCEL/ORDER_RETURN Saga | **gRPC サービス定義に `VoidPayment` / `PartialRefund` が未定義**: ORDER_CANCEL Saga ステップ 5 は「AUTHORIZED の場合はキャンセルのみ」、ORDER_RETURN Saga ステップ 2 は `PartialRefund(paymentId, refundAmount)` を参照。しかし §7 の PaymentService gRPC 定義は `Refund` のみ | PaymentService の gRPC 定義に `VoidPayment` と `PartialRefund` RPC を追加するか、`Refund` RPC の中でこれらの処理を行う旨を注記 |
| M-04 | Medium | programing-reviewer | ドキュメント整合性 | §6 API 設計 > 実装上の注意事項 | **ClosedXML 参照の残存**: §2 ライブラリテーブルの注記で「Phase 1 では EPPlus 7.* を使用する」と方針変更しているが、§6 の「レポートエクスポート: Excel は ClosedXML、PDF は QuestPDF で生成」が未更新のまま残存 | §6 の記述を「Excel は EPPlus（§2 注記参照）、PDF は QuestPDF で生成」に修正 |
| M-05 | Medium | programing-reviewer | コード品質 | §A エンティティ / §C DTO | **Value Object（Money, PostalAddress, Quantity）未適用**（check-report-3 PR-M2 継続）: spec.md で定義された Value Object が Order エンティティ・DTO で未使用。`decimal SubtotalAmount` ではなく `Money SubtotalAmount` 等の方針が未定義 | Value Object の適用方針を記載（EF Core Owned Entity / ValueConverter の複雑性とのトレードオフを含む）。Phase 2 スコープへの先送りも可 |
| M-06 | Medium | business-analyst | ビジネスルール | §D FluentValidation | **返品可能期間（30 日）バリデーション未実装**（check-report-3 BA-M2 継続）: §15 設定ファイルで `Return.AllowedDays: 30` を定義しているが、ReturnCreateRequestValidator に日数チェックのルールが存在しない | ReturnCreateRequestValidator に `RuleFor` を追加するか、Service 層での検証として日数チェックを ReturnsService に実装する方針を記載 |
| M-07 | Medium | business-analyst | 機能要件 | §6 API 設計 | **再注文 API 未定義**（check-report-3 BA-M1 継続）: ペルソナ 1 ユーザーストーリーの再注文機能に対応するエンドポイント（例: `POST /api/v1/orders/{id}/reorder`）が未定義 | 再注文 API を Phase 2 スコープとして明記するか、Phase 1 に含める場合はエンドポイント定義を追加 |
| M-08 | Medium | security-reviewer | 認可設計 | §6 API 設計 | **管理者向けエンドポイントの認可ポリシー未記載**（check-report-3 SEC-M1 継続）: `PUT /api/v1/orders/{orderId}/status`、`PUT /api/v1/returns/{id}/status`、`PUT /api/v1/shipments/{id}/status` 等の管理者操作エンドポイントに `RequireAuthorization("AdminOnly")` の注記がエンドポイント一覧テーブルに未反映 | 管理者操作エンドポイントに認可ポリシーカラムを追加するか、テーブル直下に管理者エンドポイント一覧を添付 |
| M-09 | Medium | security-reviewer | ログ設計 | §11 ログ設計 | **注文 ID → PII 到達パスのログルール未明記**（check-report-3 SEC-M2 継続）: 注文 ID はログ出力可能だが、注文コンテンツのクエリ結果（配送先住所等）をログに出力していないかの確認ルールが PII マスキングテーブルに未記載 | PII マスキングテーブルに「注文クエリ結果（配送先住所を含む）のログ出力禁止」ルールを追加 |
| M-10 | Medium | audit-reviewer | 監査カラム | §4 invoices テーブル | **Invoice に `created_by` / `updated_by` 監査カラムが未設定**（check-report-3 AUD-M1 継続）: 電子帳簿保存法対応で請求書の作成者記録が必要な可能性。Order・Return には存在するが Invoice には未設定 | Invoice テーブルと Invoice C# エンティティに `created_by`, `updated_by` カラムを追加 |
| M-11 | Medium | qa-manager | テスト戦略 | §12 テスト戦略 | **OrderStateMachine 全遷移テスト未明記**（check-report-3 QA-M1 継続）: 16 の有効遷移と不正遷移（例: SHIPPED → PENDING）の網羅テストが §12 テスト戦略セクションに未記載 | テスト戦略に「OrderStateMachine: 16 有効遷移 + 主要無効遷移パスのテスト」を追加 |
| M-12 | Medium | qa-manager | テスト戦略 | §12 テスト戦略 | **Idempotency-Key エッジケーステスト未明記**（check-report-3 QA-M2 継続）: TTL 境界値テスト（23:59:59 / 24:00:01）、同時到着テストの記載が望ましい | テスト戦略に Idempotency-Key のエッジケースを追記 |
| M-13 | Medium | compliance-reviewer | データ保持 | §6 イベント設計 / §14 運用・保守 | **データ保持ポリシーの体系化不足**（check-report-3 COM-M1 継続）: 税法 7 年保持義務が §6 で一文のみ言及。テーブルごとの保持期間・アーカイブ・匿名化タイムラインが未整理 | 「データ保持ポリシー」セクションを追加し、テーブル別の保持期間を定義 |
| M-14 | Medium | performance-reviewer | クエリ最適化 | §9 インデックス設計 | **returns テーブルの複合インデックス未定義**（check-report-3 DB-M2 継続）: `order_id` + `order_item_id` の複合インデックスが未定義。返品検索のパフォーマンスに影響 | `idx_returns_order_id_order_item_id` の追加を検討 |
| M-15 | Medium | infra-ops-reviewer | Dockerfile | §13 Dockerfile | **HEALTHCHECK の `curl` 利用に関する懸念**（check-report-3 SEC-L1 → Medium に引上げ）: `aspnet:10.0` イメージに `curl` が同梱されていない可能性。`wget -q --spider` への変更を推奨 | HEALTHCHECK を `wget -q --spider http://localhost:5004/health` に変更するか、`dotnet` ベースのヘルスチェックを検討 |
| M-16 | Medium | release-manager | CI/CD | §13 CI/CD パイプライン | **テストプロジェクトの指定なし**: `dotnet test --no-build --collect:"XPlat Code Coverage"` でテストプロジェクトの明示的指定がなく、カバレッジレポートの出力先・閾値チェックステップが未定義 | `--results-directory` の指定と `dotnet-coverage` によるカバレッジ閾値チェックステップの追加を検討 |

---

## Low 指摘一覧（参考情報）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 |
|---|--------|-----------|---------|---------|
| L-01 | Low | business-analyst | 機能整理 | Invoice テーブルが Phase 1 に含まれるが、法人顧客向け請求書発行は Phase 2 スコープ。Phase 1 での Invoice 用途（個人向け領収書）を明記すべき |
| L-02 | Low | programing-reviewer | コード設計 | Order エンティティの `Status` が `string` 型だが `OrderStatus` enum が定義済み。EF Core ValueConverter での相互変換方針を明記すべき |
| L-03 | Low | dba-reviewer | Outbox 運用 | `outbox_events` の `DEAD_LETTER` ステータスへの遷移条件と後処理（管理者通知等）の仕様が未記載 |
| L-04 | Low | audit-reviewer | Saga 監査 | `saga_logs.step_results`（JSONB）のサンプル JSON 構造が未記載。監査時の解析容易性向上のため定義推奨 |
| L-05 | Low | performance-reviewer | レポート | レポートサービスのクエリ設計（売上レポート・商品別レポートの具体的な SQL/LINQ）が未記載 |
| L-06 | Low | security-reviewer | セキュリティ | Dockerfile の `EXPOSE 5004` は内部ポート指定のみ（動作に影響なし）。5004 → 8080 への統一を検討 |
| L-07 | Low | oss-reviewer | ライセンス | EPPlus 7.* の Polyform Noncommercial License は商用利用で有償ライセンスが必要。PO 判断事項として記録済みだが、ライセンス費用見積りの優先度を上げることを推奨 |
| L-08 | Low | ux-accessibility-reviewer | API 設計 | 注文レスポンス JSON の `orderDate` が `2024-04-15T14:30:25.123Z`（UTC + Z サフィックス）だが、日本のユーザー向け表示では JST 変換のフロントエンド対応が必要。API ドキュメントに「全日時は UTC（ISO 8601）で返却」を明記すべき |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-1 | **高優先** | architect, dba-reviewer | C-01 の spec.md `orders.status` CHECK 制約の更新。設計書側の更新提案（§17）が完成済み。spec.md の SSOT 更新承認が必要 | テックリード |
| E-2 | **高優先** | architect | H-01 の ADR-0009 改訂。設計書の更新提案に基づき ADR を 9 ステップに更新する改訂承認が必要 | テックリード |
| E-3 | **通常** | oss-reviewer | EPPlus 7.* の商用ライセンス費用対効果評価（設計書注記で PO 判断事項として整理済み） | テックリード + PO |

---

## 競合解決記録

Phase 3 で競合は検出されなかった。Phase 4（競合解決）はスキップ。

---

## ドキュメント横断分析

### マイクロサービス設計書カバレッジ（SalesManagementService 単体）

| 設計領域 | 存在 | 詳細度 | 備考 |
|---------|------|--------|------|
| API 定義 | ✅ | 高 | REST エンドポイント一覧 + リクエスト/レスポンス JSON 例 + Idempotency-Key 設計 + Endpoint 実装パターン（§G） |
| DB 設計 | ✅ | 高 | テーブル定義 + CHECK 制約 + インデックス + 部分インデックス SQL + EF Core エンティティクラス（§A） + AppDbContext（§B） |
| イベント定義 | ✅ | 高 | 発行/購読イベント一覧 + Outbox パターン設計 + OutboxPublisher 実装例 + GDPR 匿名化フィールド一覧 |
| Saga 設計 | ✅ | 高 | ORDER_CHECKOUT: 9 ステップ詳細 + ORDER_CANCEL: 6 ステップ + ORDER_RETURN: 5 ステップ + 補償設計 + Deadline |
| セキュリティ | ✅ | 高 | IDOR 防止 + PII マスキング + JWT 認証 + FluentValidation 実装例（§D） |
| 非機能要件 | ✅ | 中 | SLO 1,000ms + レイテンシバジェット。RTO/RPO は ADR-0010 参照 |
| テスト戦略 | ✅ | 高 | テスト種別一覧 + テストケース実装例（§J） |
| DI / Program.cs | ✅ | 高 | 完全な Program.cs 統合ビュー（§H） |
| デプロイ | ✅ | 高 | Dockerfile + CI/CD パイプライン + スケーリング戦略 |
| 運用・保守 | ✅ | 中 | バックアップ + スケーリング + メンテナンスジョブ |
| 監視 | ✅ | 高 | メトリクス定義 + 閾値 + ヘルスチェック実装 |
| spec.md 差異記録 | ✅ | 高 | §17「spec.md 更新提案」で 3 件の差異を根拠付きで文書化 |
| ゲスト購入対応 | ✅ | 高 | ER 図・テーブル・エンティティ・匿名化全てに反映 |

### サービス間整合性

| チェック項目 | 結果 | 詳細 |
|------------|------|------|
| Saga gRPC サービス定義 | ✅ | InventoryService, CouponService, PointService, CartService, PaymentService の RPC が設計書と spec.md で一致 |
| Kafka トピック名 | ✅ | `order.created`, `order.cancelled`, `order.shipped`, `order.delivered`, `order.status-changed` が spec.md と整合 |
| 購読イベント | ✅ | `payment.completed`, `payment.failed`, `inventory.reserved`, `inventory.released`, `user.deleted` が関連サービス設計と整合 |
| Outbox パターン | ✅ | ADR-0005 準拠。動的バックオフ（100ms〜5s）、部分インデックスが一致 |
| Saga Status 値 | ✅ | `CREATED`, `PROCESSING`, `COMPLETED`, `COMPENSATING`, `COMPENSATED`, `FAILED`, `PENDING_PAYMENT` が spec.md と一致 |
| ゲスト購入設計 | ✅ | spec.md ゲスト購入フロー準拠。`is_guest`, `guest_email` が反映済み |
| GDPR 匿名化 | ✅ | `user.deleted` 購読時の匿名化対象が `guest_email`, `notes`, `product_snapshot` を含めて網羅 |
| ORDER_CANCEL/ORDER_RETURN Saga | ✅ | spec.md のキャンセル・返品シーケンス図と整合するステップ構成 |

### 未定義・曖昧な領域（残存）

| # | 領域 | 影響度 | 内容 | 前回からの変化 |
|---|------|--------|------|-------------|
| 1 | Value Object 適用 | Medium | spec.md で定義された `Money`, `PostalAddress`, `Quantity` が設計書のエンティティ・DTO に未適用 | 変化なし |
| 2 | データ保持ポリシー | Medium | テーブルごとの保持期間・アーカイブタイムラインが未整理 | 変化なし |
| 3 | 返品 30 日バリデーション | Medium | ReturnCreateRequestValidator に日数チェック未実装 | 変化なし |
| 4 | 注文番号生成ロジック | Low | `ORD-YYYYMMDD-NNNNN` 等の生成ルール詳細仕様が未記載 | 変化なし |
| 5 | gRPC `VoidPayment` / `PartialRefund` | Medium | ORDER_CANCEL/ORDER_RETURN Saga で参照されるが gRPC 定義に未記載 | **新規**（H-05 修正に伴う新発見） |

---

## Iteration 3 → 4 の改善サマリ

| 指標 | Iteration 3 | Iteration 4 | 変化 |
|------|------------|-------------|------|
| Critical | 1 | 0 | ⬇️ -1 |
| High | 7 | 0 | ⬇️ -7 |
| Medium | 17 | 16 | ⬇️ -1 |
| Low | 8 | 8 | → 0 |
| 判定 | ⚠️ Conditional Approval | ✅ Approved with Notes | ⬆️ 改善 |

**修正品質の評価**: 8 件の修正（C-01, H-01〜H-07）は全て設計書内で整合的に反映されており、新たな Critical/High は発生していない。特に以下の修正品質が高い:
- C-01 の `spec.md 更新提案` セクションは、11 ステータスの必要性根拠を個別に説明しており、spec.md 更新時のエビデンスとして十分
- H-05 の ORDER_CANCEL / ORDER_RETURN Saga 詳細化は、補償設計・Deadline・手動対応キューまで含む実装可能な品質
- H-07 の匿名化対象フィールド一覧は、`product_snapshot` 内のカスタマイズ属性まで考慮した網羅的な定義

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### ビジネス要件の完全性

**判定**: Pass

#### 前回修正確認
- H-04（ゲスト購入カラム）: ✅ `is_guest`, `guest_email` が全定義箇所に反映済み

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| BA-M1 | Medium | 再注文 API（`POST /api/v1/orders/{id}/reorder`）が未定義。ペルソナ 1 のユーザーストーリー対応（継続） | Phase 2 スコープとして明記 |
| BA-M2 | Medium | 返品可能期間（30 日）バリデーションが ReturnCreateRequestValidator に未実装（継続） | Service 層での日数チェック方針を記載 |
| BA-L1 | Low | Invoice テーブルが Phase 1 に含まれるが、Phase 1 での用途（個人向け領収書）が未明記（継続） | Phase 1 スコープを明記 |

#### 評価ポイント
- ゲスト購入フロー（spec.md §ゲスト購入フロー）のデータモデル対応が完了 ✅
- ORDER_CANCEL / ORDER_RETURN Saga の詳細化により、キャンセル・返品ビジネスフローの設計品質が向上 ✅

</details>

<details>
<summary>architect レビューレポート</summary>

### アーキテクチャ設計品質

**判定**: Pass

#### 前回修正確認
- C-01（orders.status 11 ステータス）: ✅ spec.md 更新提案セクションで文書化済み。設計書内の全定義は整合
- H-01（ADR-0009 ステップ数）: ✅ ADR-0009 更新提案が明示的に記載
- H-02（ShipmentItem 省略）: ✅ Phase 1/2 の設計判断と移行パスが明確
- H-05（ORDER_CANCEL/ORDER_RETURN Saga）: ✅ ステップ構成・補償・Deadline が詳細化
- H-06（spec.md エンティティ差異）: ✅ ADR-0006/0008 に基づく論理的な設計判断注記

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| AR-M1 | Medium | gRPC PaymentService 定義に `VoidPayment` / `PartialRefund` RPC が未定義。ORDER_CANCEL Saga（AUTHORIZED のキャンセル）と ORDER_RETURN Saga（部分返金）で必要 | gRPC 定義を拡張するか、`Refund` RPC の引数で処理を分岐する設計方針を注記 |

#### 評価ポイント
- 全 3 種類の Saga（CHECKOUT/CANCEL/RETURN）が詳細設計レベルで定義 ✅
- spec.md との差異が 3 件の更新提案として体系的に文書化 ✅
- コンポーネントアーキテクチャ図と実装（§G Endpoints, §H Program.cs）が整合 ✅

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### コード例の正確性・C# 14 機能活用

**判定**: Pass

#### 前回修正確認
- H-05 の ORDER_CANCEL / ORDER_RETURN 実装: ステップ構成テーブルと補償設計が追加。SagaCoordinator レベルの実装例は §7 の CHECKOUT 実装例を参照する形式で合理的

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| PR-M1 | Medium | §6 API 設計セクションの ClosedXML 参照が未更新（§2 の EPPlus 方針変更と矛盾） | §6 の記述を EPPlus に修正 |
| PR-M2 | Medium | Value Object（Money, PostalAddress）が Order エンティティ・DTO で未使用（継続） | 適用方針を記載（Phase 2 への先送りも可） |
| PR-L1 | Low | Order.Status の string → enum 変換方針（EF Core ValueConverter）が未明記（継続） | 実装方針を注記 |

#### 評価ポイント
- FluentValidation バリデーター（§D）が追加され、入力検証の実装品質が大幅に向上 ✅
- C# 14 機能（record 型 DTO、primary constructor、パターンマッチング）の活用が適切 ✅
- Endpoint 実装パターン（§G）が IDOR 防止・FluentValidation 統合を含む完全な実装例 ✅
- Program.cs 統合ビュー（§H）が AGENTS.md ミドルウェア順序規約に準拠 ✅

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### DB スキーマ設計品質

**判定**: Pass

#### 前回修正確認
- H-04（ゲスト購入カラム）: ✅ `is_guest`, `guest_email` が orders テーブル・Order エンティティ・AppDbContext に反映
- H-06（spec.md 差異注記）: ✅ 命名変更理由（ADR-0006）とインライン配送先設計の根拠が明確

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| DB-M1 | Medium | shipments.order_id の UNIQUE 制約が AppDbContext に未反映。テーブル DDL では UNIQUE、EF Core 設定では通常 INDEX | `.IsUnique()` を追加 |
| DB-M2 | Medium | SagaLog の OnDelete が Cascade のまま（継続）。監査用データの連鎖削除は不適切 | Restrict に変更 |
| DB-L1 | Low | DEAD_LETTER ステータスの遷移条件・後処理が未記載（継続） | 運用セクションに追記 |

#### 評価ポイント
- ER 図・テーブル定義・C# エンティティ・AppDbContext の 4 層が整合（ゲスト購入カラム含む） ✅
- CHECK 制約が全テーブルで EF Core Fluent API（`HasCheckConstraint`）で定義 ✅
- 部分インデックス（outbox_events, saga_logs）が DDL と AppDbContext で一致 ✅

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### セキュリティ設計品質

**判定**: Pass

#### 前回修正確認
- H-04（guest_email 暗号化）: ✅ AES-256-GCM 暗号化保存の注記が追加
- H-07（匿名化対象フィールド）: ✅ 匿名化フィールド一覧テーブルに `guest_email` の NULL 化が追加

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| SEC-M1 | Medium | 管理者向けエンドポイントの認可ポリシーがエンドポイント一覧テーブルに未記載（継続） | AdminOnly ポリシーの明記 |
| SEC-M2 | Medium | 注文 ID → PII 到達パスのログルールが PII マスキングテーブルに未記載（継続） | ログルール追加 |
| SEC-L1 | Low | Dockerfile EXPOSE 5004。AGENTS.md の Dockerfile 例は 8080 を使用 | 統一検討 |

#### 評価ポイント
- GDPR 匿名化フィールド一覧が 10 フィールドを網羅（guest_email, notes, product_snapshot 含む） ✅
- FluentValidation バリデーター（§D）がホワイトリスト検証（郵便番号 Regex 等）を適用 ✅
- Idempotency-Key 設計が詳細（TTL, 処理中リクエスト, クリーンアップジョブ） ✅
- FallbackPolicy = RequireAuthenticatedUser が Program.cs に設定済み ✅

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 法規制・データ保護準拠

**判定**: Pass

#### 前回修正確認
- H-07（GDPR 匿名化）: ✅ 匿名化対象フィールド一覧が追加。guest_email・notes・product_snapshot のカスタマイズ属性まで網羅

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| COM-M1 | Medium | データ保持ポリシーの体系化不足（継続）。税法 7 年保持義務の記載はあるが、テーブル別の保持・アーカイブ・匿名化タイムラインが未整理 | 「データ保持ポリシー」セクション追加 |

#### 評価ポイント
- GDPR 第 17 条対応（忘れられる権利）が匿名化フィールド一覧で具体的に設計 ✅
- PCI DSS 非保持化（ADR-0008）準拠で SalesManagementService は決済情報を保持しない ✅
- guest_email の AES-256-GCM 暗号化 + 匿名化時の完全削除（NULL 化）が設計済み ✅

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### トレーサビリティ・監査証跡

**判定**: Pass

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| AUD-M1 | Medium | Invoice に `created_by` / `updated_by` 監査カラム未設定（継続） | 電子帳簿保存法対応で追加検討 |
| AUD-L1 | Low | saga_logs.step_results のサンプル JSON 構造が未記載（継続） | サンプル構造を追記 |

#### 評価ポイント
- ORDER_CANCEL / ORDER_RETURN Saga のステップ詳細が追加され、全 3 種類の Saga 実行履歴が SagaLog で追跡可能 ✅
- 匿名化処理は `user.deleted` イベント購読で実行され、監査可能な非同期フローで設計 ✅
- spec.md 差異の記録・更新提案が「spec.md 更新提案」セクションとして監査可能な形式で文書化 ✅

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### テスト戦略品質

**判定**: Pass

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| QA-M1 | Medium | OrderStateMachine 全遷移テスト未明記（継続） | テスト戦略に 16 遷移テストを追加 |
| QA-M2 | Medium | Idempotency-Key エッジケーステスト未明記（継続） | TTL 境界値テストを追記 |

#### 評価ポイント
- §J でテストケース実装例が追加（OrderService Unit Test + SagaCoordinator Integration Test） ✅
- AAA パターン・Should_When 命名規約に準拠したテストコード例 ✅
- Saga テスト戦略に ORDER_CANCEL / ORDER_RETURN の補償テストを追加可能な設計 ✅

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### パフォーマンス設計品質

**判定**: Pass

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| PERF-M1 | Medium | returns テーブルの `order_id` + `order_item_id` 複合インデックス未定義（継続） | インデックス追加 |
| PERF-L1 | Low | レポートクエリ設計（SQL/LINQ）が未記載（継続） | 集計クエリの設計を追記 |

#### 評価ポイント
- ORDER_CANCEL Saga（全体 30 秒）と ORDER_RETURN Saga（全体 60 秒）の Deadline 設計が追加 ✅
- SLO 1,000ms のレイテンシバジェット設計が 9 ステップ全てで定義済み ✅
- 部分インデックスが OutboxPublisher・SagaRecoveryService の両方に最適化 ✅

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### インフラ・運用設計品質

**判定**: Pass

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| INFRA-M1 | Medium | HEALTHCHECK の `curl` 利用に関する懸念。`aspnet:10.0` に同梱されていない可能性 | `wget -q --spider` に変更 |
| INFRA-L1 | Low | ローカル開発セクション（§15）の Docker バージョンが `24.0+` と記載、§2 技術スタックは `25.x`。統一推奨 | バージョン統一 |

#### 評価ポイント
- Dockerfile がマルチステージビルド + 非 root ユーザー実行で AGENTS.md 規約準拠 ✅
- OutboxPublisher の Advisory Lock によりスケールアウト環境でのリーダー選出が安全 ✅
- SagaRecoveryService の `SELECT FOR UPDATE SKIP LOCKED` で複数インスタンス安全 ✅

</details>

<details>
<summary>release-manager レビューレポート</summary>

### リリース戦略品質

**判定**: Pass

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| REL-M1 | Medium | CI/CD パイプラインでカバレッジ閾値チェックステップが未定義 | `dotnet-coverage` による閾値チェック追加 |

#### 評価ポイント
- GitHub Actions CI/CD パイプラインが定義済み（ビルド→テスト→Docker→デプロイ） ✅
- Azure Container Apps へのデプロイフローが明確 ✅

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### NuGet ライセンス・依存関係

**判定**: Pass

#### 前回修正確認
- H-03（ClosedXML → EPPlus）: ✅ 方針変更注記で AGENTS.md プレリリース版禁止事項に対応

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| OSS-L1 | Low | EPPlus 7.* の Polyform Noncommercial License は商用利用で有償ライセンスが必要。設計書で PO 判断事項として整理済み | ライセンス費用見積りの優先度を上げる |

#### 評価ポイント
- プレリリース版（ClosedXML 0.104.*）の使用が Phase 1 で EPPlus に代替され、AGENTS.md 禁止事項に対応 ✅
- 全 NuGet パッケージが AGENTS.md §8 の必須パッケージ一覧と整合 ✅

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### UX 設計品質

**判定**: Pass

#### 指摘事項

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|---------|---------|
| UX-L1 | Low | API レスポンスの日時フォーマットが UTC（ISO 8601）であることの明記が不足。フロントエンド側の JST 変換責務の明確化 | API ドキュメントに「全日時は UTC で返却」を明記 |

#### 評価ポイント
- PENDING_PAYMENT 状態の UX（202 + 「お支払い処理中です」メッセージ）が設計済み ✅
- エラーレスポンスが RFC 9457 準拠で、フロントエンドでのエラー表示が統一的 ✅

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 技術標準横断適合性

**判定**: Pass（指摘なし）

#### 前回修正確認の総合評価
全 8 件の修正（C-01, H-01〜H-07）は設計書内で整合的に反映されており、AGENTS.md 規約・spec.md 設計方針との矛盾は検出されなかった。

- **spec.md 更新提案セクション（§17）**: 設計書を SSOT としつつ、spec.md 側の更新提案を根拠付きで文書化する方法論は適切。spec.md は公式承認プロセスを経て更新すべきであり、設計書側で一方的に変更しない判断が正しい
- **レイヤードアーキテクチャ**: Endpoints → Services → Repositories の依存方向が §G, §H で一貫
- **DDD 原則**: Order が Aggregate Root として AddItem ドメインメソッドを持ち、子エンティティ操作が Root 経由で実行される設計が適切
- **C# 14 機能活用**: primary constructor、record 型 DTO、パターンマッチング（switch 式）が全コード例で適用
- **CancellationToken**: 全 async メソッドのシグネチャに `CancellationToken ct = default` が含まれている
- **ミドルウェア順序**: §H Program.cs が AGENTS.md §11.3 の順序規約（ExceptionHandler → HSTS → CorrelationId → Serilog → CORS → Authentication → Authorization → RateLimiter → Endpoints）に完全準拠

#### 実装実現可能性の評価
設計書は **3,900 行以上** のドキュメントであり、以下の全てが実装可能なレベルで定義されている:
- EF Core エンティティクラス（§A）+ AppDbContext（§B）: そのまま `.cs` ファイルにコピー可能
- DTO 定義（§C）+ FluentValidation（§D）: 実装可能な品質
- Repository / Service インターフェース（§E, §F）: CancellationToken 必須化済み
- Endpoint 実装パターン（§G）: IDOR 防止・認証・バリデーション統合済み
- Program.cs 統合ビュー（§H）: DI 登録・ミドルウェア・ヘルスチェック・OpenTelemetry の完全構成

</details>
