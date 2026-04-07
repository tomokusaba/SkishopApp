# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/sales-management-design.md`（販売管理サービス詳細設計書、1761 行、修正後）
- **判定**: ⚠️ **Conditional Approval** — Critical 指摘なし、High 指摘 4 件が残存（人間の判断を介在）
- **イテレーション**: 2（check-report-1: 7 Critical / 25 High → fix-report-1 で全件修正済み → 本レビュー）
- **レビュー日時**: 2026-04-03
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

---

## 前回指摘の解消状況

### Critical 指摘（7 件）: ✅ 全件解消

| # | 指摘ID | 内容 | 解消状況 |
|---|--------|------|---------|
| 1 | C-1 | Saga 9 ステップ設計欠落 | ✅ §7 に 9 ステップ Saga シーケンス図・詳細テーブル・補償設計を追加 |
| 2 | C-2 | Outbox パターン設計欠落 | ✅ §7.1 に OutboxPublisher BackgroundService・Advisory Lock・動的バックオフ追加 |
| 3 | C-3 | SagaRecoveryService 欠落 | ✅ §7.2 に SagaRecoveryService・SELECT FOR UPDATE SKIP LOCKED・30 秒ポーリング追加 |
| 4 | C-4 | saga_logs テーブル欠落 | ✅ DB スキーマセクションに 14 カラム + CHECK 制約 + 部分インデックス追加 |
| 5 | C-5 | 注文ステータス状態遷移図欠落 | ✅ §5 に Mermaid stateDiagram-v2 + OrderStateMachine コード例追加 |
| 6 | C-6 | Idempotency-Key 設計欠落 | ✅ idempotency_keys テーブル・ヘッダー仕様・処理フロー・TTL 24 時間追加 |
| 7 | C-7 | gRPC サービス定義・Deadline 設計欠落 | ✅ proto 定義 5 サービス・ステップ別 Deadline・SLO 1,000ms 追加 |

### High 指摘（25 件）: ✅ 全件解消

| # | 指摘ID | 内容 | 解消状況 |
|---|--------|------|---------|
| 1 | H-1 | Kafka トピック名不整合 | ✅ `order.created`, `order.cancelled` 等に統一 |
| 2 | H-2 | Invoice エンティティ欠落 | ✅ invoices テーブル定義追加 |
| 3 | H-3 | OrderStatus ルックアップテーブル欠落 | ✅ VARCHAR + CHECK 制約で対応 |
| 4 | H-4 | TIMESTAMP WITH TIME ZONE 未使用 | ✅ 全テーブルで `TIMESTAMP WITH TIME ZONE` に修正 |
| 5 | H-5 | row_version 欠落 | ✅ orders, shipments, returns, saga_logs に row_version BYTEA 追加 |
| 6 | H-6 | CHECK 制約なし | ✅ 全テーブルに CHECK 制約追加 |
| 7 | H-7 | 補償設計が 9 ステップ非対応 | ✅ 9 ステップ対応の補償トランザクション表追加 |
| 8 | H-8 | 送料無料閾値不整合 | ✅ 基本閾値 10,000 円 + 会員ランク別引き下げ追加（**ただし新たな不整合あり → 本レビュー H-1**） |
| 9 | H-9 | 税計算ルール未定義 | ✅ TaxCalculator（外税方式・切り捨て）追加 |
| 10 | H-10 | productSnapshot JSONB 欠落 | ✅ JSONB カラム + JSON スキーマ例追加 |
| 11 | H-11 | レイテンシバジェット欠落 | ✅ 9 ステップ全体 930ms のバジェット表追加 |
| 12 | H-12 | PENDING_PAYMENT 状態遷移未定義 | ✅ PENDING_PAYMENT 設計・SagaRecoveryService ポーリング追加 |
| 13 | H-13 | IDOR 防止設計不十分 | ✅ ClaimsPrincipal userId 照合コード例追加 |
| 14 | H-14 | PII ログマスキング未定義 | ✅ PII ログマスキングルール表追加 |
| 15 | H-15 | RTO 不整合（4 時間→1 時間） | ✅ RTO 1 時間・ADR-0010 準拠に修正 |
| 16 | H-16 | /health/ready 未定義 | ✅ PostgreSQL + Redis + Kafka のヘルスチェック追加 |
| 17 | H-17 | PUT cancel → POST cancel | ✅ `POST /api/v1/orders/{orderId}/cancel` に修正 |
| 18 | H-18 | SagaCoordinator コード例欠落 | ✅ 詳細な SagaCoordinator 実装例追加（補償含む） |
| 19 | H-19 | RTO 整合性（H-15 重複） | ✅ H-15 と同時解消 |
| 20 | H-20 | ClosedXML プレリリース版 | ✅ 代替検討注記追加（バージョンは 0.104.* のまま） |
| 21 | H-21 | created_by/updated_by 欠落 | ✅ orders, returns テーブルに監査カラム追加 |
| 22 | H-22 | user.deleted 購読欠落 | ✅ 購読イベントに user.deleted + 匿名化処理追加 |
| 23 | H-23 | Saga ステップ番号不整合 | ✅ spec.md 9 ステップを SSOT として明記 |
| 24 | H-24 | テスト設計不十分 | ✅ Saga・Outbox・補償テスト戦略追加 |
| 25 | H-25 | Advisory Lock 設計欠落 | ✅ OutboxPublisher に Advisory Lock 実装追加 |

---

## 技術スタック検証結果

| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| 言語 | C# 14 (.NET 10 LTS) | C# 14 (.NET 10) | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| ORM | Microsoft.EntityFrameworkCore 10.* | Entity Framework Core 10 | ✅ |
| DB | PostgreSQL (salesdb) | PostgreSQL | ✅ |
| メッセージング | Confluent.Kafka 2.* | Confluent.Kafka 2.* | ✅ |
| キャッシュ | StackExchange.Redis 2.* | StackExchange.Redis 2.* | ✅ |
| 認証 | Microsoft.AspNetCore.Authentication.JwtBearer 10.* | 10.* | ✅ |
| バリデーション | FluentValidation 11.* | FluentValidation 11.* | ✅ |
| 耐障害性 | Polly 8.* + Microsoft.Extensions.Http.Resilience 9.* | 同上 | ✅ |
| ログ | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| テレメトリ | OpenTelemetry.Extensions.Hosting 1.* | 1.* | ✅ |
| ヘルスチェック | AspNetCore.HealthChecks.NpgSql 9.* + Redis 9.* + Kafka 9.* | NpgSql 9.* + Redis 9.* | ✅ |
| gRPC | Grpc.AspNetCore 2.* + Grpc.Net.Client 2.* | — (AGENTS.md に明示なし) | ⚠️ |
| レポート | ClosedXML 0.104.* + QuestPDF 2024.* | — (AGENTS.md に明示なし) | ⚠️ |
| オーケストレーション | .NET Aspire 13.1 | .NET Aspire 13.1 | ✅ |
| コンテナ | Docker 25.x | Docker 25.x | ✅ |
| サービスポート | 5004 | 5004 | ✅ |
| DB 名 | salesdb | salesdb | ✅ |

---

## 指摘サマリー

| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| architect | ⚠️ Warn | 0 | 1 | 2 | 0 |
| dba-reviewer | ✅ Pass | 0 | 0 | 1 | 0 |
| security-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| programing-reviewer | ⚠️ Warn | 0 | 1 | 2 | 0 |
| business-analyst | ⚠️ Warn | 0 | 2 | 1 | 1 |
| performance-reviewer | ✅ Pass | 0 | 0 | 1 | 0 |
| qa-manager | ✅ Pass | 0 | 0 | 0 | 0 |
| compliance-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| infra-ops-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| release-manager | ✅ Pass | 0 | 0 | 0 | 0 |
| oss-reviewer | ✅ Pass | 0 | 0 | 0 | 1 |
| audit-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| ux-accessibility-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| tech-lead | ⚠️ Warn | 0 | 0 | 0 | 0 |
| **合計** | | **0** | **4** | **7** | **2** |

---

## 判定根拠
- **判定ルール適用結果**: Critical 指摘 0 件。High 指摘 4 件が残存するため `⚠️ Conditional Approval`
- **前回からの改善**: Critical 7 → 0（全件解消）、High 25 → 4（21 件解消、4 件新規検出）
- **最も重大な指摘**: 送料金額が設計書内で自己矛盾（テーブル: 500 円 / config: 800 円 / spec.md: 550 円）

---

## High 指摘一覧（修正強く推奨）

| # | 重要度 | 出典 Agent | カテゴリ | 対象箇所 | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|---------|----------|----------|
| H-1 | **High** | business-analyst | 送料金額不整合 | §5 配送料計算ルール (L557) / §15 appsettings.json (L1700) | **デフォルト送料が 3 箇所で矛盾**。配送料計算ルール表は「上記以外: **500 円**」、`appsettings.json` の `DefaultShippingFee` は **800**、spec.md §配送料計算は「一律 **550 円**（税込）」。修正時に統一されなかった。spec.md は本州・四国・九州: 550 円 / 北海道・沖縄: 1,100 円 / お急ぎ便: +330 円 / 大型商品: +1,650 円の区分も定義しているが、設計書に未反映 | spec.md に統一（550 円）。`DefaultShippingFee: 550` に修正。地域別・お急ぎ便・大型商品の料金区分も反映 |
| H-2 | **High** | business-analyst | 設定キー誤り | §15 appsettings.json (L1703-1704) | **`MemberRankDiscounts` のキーが不正**。config は `"Gold": 8000, "Platinum": 5000` だが、§5 の配送料テーブルと spec.md の定義では **Silver: 8,000 円 / Gold: 5,000 円 / Platinum: 常時無料**。Gold と Platinum の値が Silver と Gold にすべきものに誤マッピングされており、Silver キーが完全に欠落。Platinum（常時無料）の特別扱いも未設計 | `"Silver": 8000, "Gold": 5000` に修正。Platinum は閾値 0 または個別フラグで「常時無料」を表現 |
| H-3 | **High** | architect | 状態遷移欠落 | §5 状態遷移図 (L509-540) / OrderStateMachine (L543-570) | **spec.md で定義された 2 つの遷移が欠落**。① `Shipped → Returned`（受取拒否 / 配送事故）: spec.md で明示的に定義されているが設計書に含まれず、出荷済み→返品のビジネスシナリオが処理不能。② `Cancelled → Refunded`（決済キャプチャ済みの場合のみ）: 決済キャプチャ後にキャンセルされた注文の返金処理パスが存在しない | OrderStateMachine に `(Shipped, Returned)` と `(Cancelled, Refunded)` の遷移を追加。状態遷移図も同様に更新 |
| H-4 | **High** | programing-reviewer | 決済プロトコル不整合 | §7 Saga ステップ詳細 (L1165) / SagaCoordinator コード (L1350-1360) / gRPC proto (L1265) | **PaymentService の通信プロトコルが設計書内で矛盾**。① Saga ステップ詳細テーブルはステップ 6 を「通信プロトコル: **HTTPS**」と定義。② アーキテクチャ図は `PAY_HTTPS[PaymentService<br>HTTPS]` と表記。③ SagaCoordinator のコンストラクタは `IPaymentClient paymentClient`（HTTPS クライアント）を注入。④ **しかし**、SagaCoordinator の例外ハンドラーは `catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded && sagaLog.CurrentStep == 6)` で **gRPC 固有の例外**をキャッチ。HTTPS クライアントは `RpcException` をスローしないため、**PENDING_PAYMENT 状態への遷移パスが到達不能**。⑤ gRPC proto 定義セクションに `service PaymentService { rpc ProcessPayment ... }` が含まれており混乱を助長 | 方針を統一: (A) ステップ 6 を HTTPS とする場合、catch を `HttpRequestException` / `TaskCanceledException` に変更し、proto から PaymentService を除外。(B) 内部 PaymentService wrapper を gRPC にする場合、ステップ詳細テーブルとアーキテクチャ図を gRPC に修正 |

---

## Medium 指摘一覧（改善推奨）

| # | 重要度 | 出典 Agent | カテゴリ | 対象箇所 | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|---------|----------|----------|
| M-1 | Medium | programing-reviewer | コード品質 | §7 補償トランザクション設計 (L1211) | **Step 5 補償の記述が誤解を招く**。補償テーブルで Step 6（決済）失敗時の Step 5 補償を「**DB ロールバック（EF Core TX）**」と記載しているが、Step 6 に到達した時点で Step 5 のローカル TX は既にコミット済み。実際の補償コード（L1447）は `orderService.CancelOrderAsync()` を呼び出している。「DB ロールバック」は Step 5 **自身の**失敗時（コミット前）にのみ正しい表現であり、Step 6 失敗後の補償は「**注文キャンセル処理**」が正確 | 補償テーブルの Step 5 補償アクションを「注文キャンセル処理（`OrderService.CancelOrderAsync`）」に修正。Step 5 自身の失敗時は「ローカル TX ロールバック」と区別して記載 |
| M-2 | Medium | programing-reviewer | コード品質 | §7.1 OutboxPublisher (L1395-1400) | **Advisory Lock 取得に `ExecuteSqlRawAsync` を使用しているが、SELECT クエリには不適切**。`ExecuteSqlRawAsync` は DML（INSERT/UPDATE/DELETE）用であり、`SELECT pg_try_advisory_lock(...)` の戻り値（boolean）を正しく取得できない（return 値は affected rows = -1）。`> 0` の判定は常に false となる可能性がある | `Database.SqlQueryRaw<bool>(AdvisoryLockQuery)` または ADO.NET の `ExecuteScalarAsync` を使用して boolean 結果を正しく取得する |
| M-3 | Medium | dba-reviewer | DB 設計 | outbox_events テーブル CHECK 制約 (L460) / OutboxPublisher コード (L1395-1460) | **outbox_events の status 値とコード例が不一致**。① CHECK 制約に `'PROCESSING'` が定義されているが、OutboxPublisher コードは PENDING → PUBLISHED/FAILED と直接遷移し、PROCESSING を使用しない（クラッシュリカバリ時の中間状態検出が不可能）。② CHECK 制約に `'DEAD_LETTER'` が定義されているが、コードは `max_retries` 到達時に `"FAILED"` を設定（DEAD_LETTER は未使用） | (A) OutboxPublisher で PENDING → PROCESSING → PUBLISHED/FAILED の 2 段階遷移に修正（クラッシュ耐性向上）。(B) DEAD_LETTER を使用する場合はコード例に反映、使用しない場合は CHECK 制約から除外 |
| M-4 | Medium | performance-reviewer | 並行処理 | §7.2 SagaRecoveryService (L1488-1510) | **PENDING_PAYMENT Saga のクエリに行ロック（FOR UPDATE SKIP LOCKED）が未適用**。PROCESSING Saga には `SELECT FOR UPDATE SKIP LOCKED` を使用しているが、PENDING_PAYMENT Saga のクエリ（L1500-1505）は通常の `Where()` + `ToListAsync()` で取得。`minReplicas: 2` 環境で複数インスタンスが同一 PENDING_PAYMENT Saga を同時処理するリスクがある | PENDING_PAYMENT Saga のクエリにも `FromSqlInterpolated` + `FOR UPDATE SKIP LOCKED` を適用 |
| M-5 | Medium | architect | ER 図 | §4 エンティティ関連図 (L100) | **Order-SagaLog のカーディナリティが不正確**。ER 図で `Order \|\|--o\| SagaLog : "保有"` (1:0..1) と記載されているが、Order は複数の SagaLog を持ち得る（ORDER_CHECKOUT, ORDER_CANCEL, ORDER_RETURN）。正しくは `Order \|\|--o{ SagaLog : "保有"` (1:0..*) | ER 図のカーディナリティを 1:0..* に修正 |
| M-6 | Medium | architect | 状態設計 | §5 状態遷移図 (L509-540) | **InventoryShortage / PaymentFailed / PendingPayment の 3 状態が spec.md に存在しない**。design doc はこれらの状態を追加しているが、spec.md の状態遷移図では在庫不足 / 決済失敗は `Cancelled` への遷移として処理（理由を区別）。設計書の方がオペレーション上の粒度が高く合理的だが、spec.md との乖離が明示されておらず、ADR としての記録もない | spec.md との差異を「設計ノート」として状態遷移図の直後に明記。追加状態の採用理由（障害分類の粒度向上、監視メトリクスの精度向上）を記録 |
| M-7 | Medium | business-analyst | 送料設計 | §5 配送料計算ルール (L556-562) | **spec.md の地域別・オプション別送料が未反映**。spec.md は北海道・沖縄: 1,100 円、お急ぎ便: +330 円、大型商品: +1,650 円を定義しているが、設計書の配送料テーブルはフラットな閾値比較のみ。`ShippingFeeCalculator` サービスの入力パラメータ（配送先都道府県、商品重量/寸法、お急ぎフラグ）も未定義 | spec.md の配送料計算ルール全件を反映。`ShippingFeeCalculator` の入出力仕様を追加 |

---

## Low 指摘一覧（時間がある時に対応）

| # | 重要度 | 出典 Agent | カテゴリ | 対象箇所 | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|---------|----------|----------|
| L-1 | Low | business-analyst | 設定値 | §15 appsettings.json (L1707) | **返品許可日数が spec.md と不一致**。config の `AllowedDays: 30` だが、spec.md §注文ステータス遷移ルールでは「配達後 **14 日**以内」と定義。ビジネス要件としての正式な値の確認が必要 | spec.md（14 日）を正とする場合は `AllowedDays: 14` に修正 |
| L-2 | Low | oss-reviewer | 依存関係 | §2 主要ライブラリ (L37) | **ClosedXML 0.104.* はメジャーバージョン 0**（セマンティックバージョニング上 API 安定性未保証）。設計書に「※GA 版リリース後に移行予定。代替として EPPlus 7.* を検討」の注記があり、リスク認識と移行計画は記録済み | GA 版リリース時に速やかに移行。移行計画を TODO として追跡 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-1 | 高優先 | business-analyst | **デフォルト送料の正式値確定**: 設計書テーブル（500 円）、config（800 円）、spec.md（550 円）の 3 値が矛盾。ビジネス要件として正しい送料の確定が必要 | プロダクトオーナー |
| E-2 | 通常 | architect | **ステップ 6（決済）の通信プロトコル確定**: 外部 PG（Stripe/GMO）は HTTPS API だが、内部 PaymentService ラッパーを gRPC にするか HTTPS にするかで設計が分かれている。SagaCoordinator の例外ハンドリングに直接影響 | テックリード |
| E-3 | 通常 | architect | **追加状態（InventoryShortage / PaymentFailed / PendingPayment）の spec.md 反映**: 設計書が追加した 3 状態は運用上有用だが spec.md に存在しない。spec.md を更新するか、設計書を spec.md に合わせるかの判断が必要 | テックリード / アーキテクト |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| — | — | — | 競合なし | — | — |

---

## spec.md との整合性チェック（前回比較）

| 設計要素 | check-report-1 | check-report-2 | 変化 |
|---------|---------------|---------------|------|
| Saga 9 ステップ構成 | ❌ 不整合 | ✅ 整合 | 解消 |
| 注文ステータス状態遷移図 | ❌ 欠落 | ⚠️ 部分整合（2 遷移欠落） | 大幅改善 |
| OrderStateMachine | ❌ 欠落 | ⚠️ 部分整合（2 遷移欠落） | 大幅改善 |
| saga_logs テーブル | ❌ 欠落 | ✅ 整合 | 解消 |
| outbox_events テーブル | ❌ 欠落 | ✅ 整合 | 解消 |
| OutboxPublisher BackgroundService | ❌ 欠落 | ⚠️ コード品質課題あり | 大幅改善 |
| SagaRecoveryService BackgroundService | ❌ 欠落 | ⚠️ PENDING_PAYMENT ロック未適用 | 大幅改善 |
| idempotency_keys テーブル | ❌ 欠落 | ✅ 整合 | 解消 |
| gRPC proto 定義 | ❌ 欠落 | ⚠️ PaymentService プロトコル矛盾 | 大幅改善 |
| Saga ステップ別 Deadline | ❌ 欠落 | ✅ 整合 | 解消 |
| SLO Deadline 1,000ms | ❌ 欠落 | ✅ 整合 | 解消 |
| PENDING_PAYMENT 状態 | ❌ 欠落 | ⚠️ コードパス到達不能 | 大幅改善 |
| Invoice エンティティ | ❌ 欠落 | ✅ 整合 | 解消 |
| Kafka トピック名 | ❌ 不整合 | ✅ 整合 | 解消 |
| 配送料無料閾値 | ❌ 不整合（5,000 円） | ⚠️ テーブル/config/spec.md 三者不一致 | 部分改善 |
| RTO | ❌ 不整合（4 時間） | ✅ 整合（1 時間） | 解消 |
| productSnapshot JSONB | ⚠️ 方式差異 | ✅ 整合 | 解消 |
| DECIMAL 精度 | ⚠️ 不整合（10,2） | ✅ 整合（12,2） | 解消 |
| 配送料計算ルール | ⚠️ 不足 | ⚠️ 地域別/大型/お急ぎ未反映 | 部分改善 |
| 消費税計算ルール | ⚠️ 不足 | ✅ 整合 | 解消 |
| user.deleted 購読 | ❌ 欠落 | ✅ 整合 | 解消 |
| TIMESTAMP WITH TIME ZONE | ⚠️ 不整合 | ✅ 整合 | 解消 |
| row_version | ⚠️ 欠落 | ✅ 整合 | 解消 |
| Dockerfile マルチステージビルド | ✅ 整合 | ✅ 整合 | 維持 |
| サービスポート 5004 | ✅ 整合 | ✅ 整合 | 維持 |
| Shipped → Returned 遷移 | — (未検出) | ❌ 欠落 | 新規検出 |
| Cancelled → Refunded 遷移 | — (未検出) | ❌ 欠落 | 新規検出 |

---

## ドキュメント品質推移

| 指標 | check-report-1 | check-report-2 | 変化 |
|------|---------------|---------------|------|
| Critical | 7 | **0** | -7 (✅ 全件解消) |
| High | 25 | **4** | -21 (4 件新規) |
| Medium | 13 | **7** | -6 |
| Low | 1 | **2** | +1 |
| 合計 | 46 | **13** | -33 (72% 削減) |
| 判定 | ❌ Rejected | ⚠️ **Conditional** | 1 段階改善 |

---

## 各 Agent 詳細レポート

<details>
<summary>architect レビューレポート</summary>

### 判定: ⚠️ Warn (Critical: 0, High: 1, Medium: 2)

**概要**: 前回の根本的な設計欠落（Saga 9 ステップ、Outbox、SagaRecoveryService、saga_logs、gRPC 等）は全て解消された。設計書は spec.md/ADR-0009 の Saga オーケストレーション設計を忠実に反映しており、注文確定フローの実装に必要な情報が揃っている。

**残存課題**:
- **H-3**: spec.md の `Shipped → Returned` と `Cancelled → Refunded` の 2 遷移が未反映。特に `Cancelled → Refunded` は決済キャプチャ済み注文の返金パスとして不可欠
- **M-5**: ER 図の Order-SagaLog カーディナリティが 1:0..1 だが、1 注文に対して複数の Saga（checkout, cancel, return）が存在し得るため 1:0..* が正確
- **M-6**: InventoryShortage / PaymentFailed / PendingPayment の追加状態は運用上合理的だが、spec.md との乖離が文書化されていない

**ポジティブ評価**:
- Saga 9 ステップのシーケンス図が詳細で、各ステップの通信プロトコル・Deadline・補償対象が一覧表で整理されている
- レイテンシバジェット（合計 930ms / SLO 1,000ms）が具体的数値で設計されており、SLO 達成の見通しが立つ
- PENDING_PAYMENT 状態の設計（SagaRecoveryService による Stripe ポーリング + 30 分タイムアウト）が実装可能なレベルで記述されている

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 判定: ✅ Pass (Critical: 0, High: 0, Medium: 1)

**概要**: 前回指摘の全テーブル改修（TIMESTAMP WITH TIME ZONE、row_version、CHECK 制約、DECIMAL(12,2)、Invoice、saga_logs、outbox_events、idempotency_keys、監査カラム）が全て完了。DB スキーマは sql-schema-review.instructions.md に概ね準拠している。

**残存課題**:
- **M-3**: outbox_events の status CHECK 制約に PROCESSING / DEAD_LETTER が含まれているが、OutboxPublisher コード例ではこれらの状態を使用していない

**ポジティブ評価**:
- 全テーブルの CHECK 制約が適切（quantity > 0, amount >= 0, status IN (...)）
- 部分インデックス（outbox:PENDING、saga:PROCESSING、saga:PENDING_PAYMENT）が適切に設計されている
- idempotency_keys テーブルの UNIQUE(idempotency_key, user_id) 複合一意制約が正しい
- productSnapshot JSONB の設計が明確で、JSON スキーマ例も提示されている

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 判定: ✅ Pass (Critical: 0, High: 0, Medium: 0)

**概要**: 前回指摘の IDOR 防止パターン、PII ログマスキングルールが追加された。セキュリティ設計は AGENTS.md §5 / security-coding.instructions.md に準拠。

**ポジティブ評価**:
- IDOR 防止のコード例（ClaimsPrincipal → userId 照合）が具体的
- PII ログマスキングルール表が明確（住所・電話番号・宛名は禁止、注文 ID・金額は許可）
- JWT 認証 + ロールベース認可が全エンドポイントに適用
- Idempotency-Key による二重注文防止が適切に設計
- user.deleted 購読時のデータ匿名化処理（customer_id ハッシュ化、PII を [DELETED] に置換）が GDPR 対応として適切

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 判定: ⚠️ Warn (Critical: 0, High: 1, Medium: 2)

**概要**: SagaCoordinator コード例は C# 14 の機能（primary constructor、record、パターンマッチング）を活用しており、AGENTS.md のコーディング規約に概ね準拠。CancellationToken の伝搬、TimeProvider の DI 注入、ILogger のメッセージテンプレート形式が適切。

**残存課題**:
- **H-4**: PaymentService の通信プロトコルが設計書内で矛盾しており、RpcException キャッチが HTTPS クライアントでは到達不能
- **M-1**: 補償テーブルの Step 5 記述が誤解を招く（コミット済み TX に対する「DB ロールバック」表記）
- **M-2**: OutboxPublisher の Advisory Lock コードが `ExecuteSqlRawAsync` を SELECT に使用しており、EF Core の API 選択が不適切

**ポジティブ評価**:
- SagaCoordinator の primary constructor パターンが AGENTS.md §4.3 に準拠
- `TimeProvider` の DI 注入によりテスタビリティが確保されている
- `CancellationTokenSource.CreateLinkedTokenSource` による SLO Deadline 強制が正しく実装
- 補償トランザクションの逆順実行ループが明確で、各ステップのべき等性が `saga_step_id` で保証
- グローバル例外ハンドラーが RFC 9457 準拠（TypedResults.Problem）

</details>

<details>
<summary>business-analyst レビューレポート</summary>

### 判定: ⚠️ Warn (Critical: 0, High: 2, Medium: 1, Low: 1)

**概要**: 注文ライフサイクル（作成→確認→出荷→配達→返品→返金）のビジネスフローが明確になった。Saga 9 ステップ設計により、複数サービスにまたがるチェックアウトフローの整合性が保証されている。

**残存課題**:
- **H-1**: デフォルト送料が 3 箇所で矛盾（500 / 800 / 550 円）
- **H-2**: MemberRankDiscounts の config キーが Silver/Gold ではなく Gold/Platinum に誤マッピング
- **M-7**: spec.md の地域別・オプション別料金（北海道/沖縄、お急ぎ便、大型商品）が未反映
- **L-1**: 返品許可日数 30 日と spec.md 14 日の差異

**ポジティブ評価**:
- 配送料テーブルの会員ランク別閾値（一般: 10,000 / Silver: 8,000 / Gold: 5,000 / Platinum: 無料）は spec.md と一致
- 消費税計算（外税方式、1 円未満切り捨て、TaxCalculator）が明確
- Idempotency-Key による二重注文防止がユーザー体験を保護
- PENDING_PAYMENT 時の 202 Accepted + メール通知のユーザーコミュニケーション設計が適切

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 判定: ✅ Pass (Critical: 0, High: 0, Medium: 1)

**概要**: Saga レイテンシバジェット（930ms / SLO 1,000ms）が具体的数値で設計され、gRPC Deadline もステップ別に適切なマージンが設定されている。

**残存課題**:
- **M-4**: SagaRecoveryService の PENDING_PAYMENT クエリに行ロックが未適用

**ポジティブ評価**:
- レイテンシバジェットテーブルが全 9 ステップ + API Gateway を含む包括的な設計
- gRPC Deadline のマージン倍率（3.75x〜6.7x）が実運用に適切
- Redis キャッシュ戦略（注文: 1h、レポート: 24h、配送追跡: 30min）が明確
- 部分インデックスによる OutboxPublisher / SagaRecoveryService のクエリ最適化が設計済み
- 動的バックオフ（100ms〜5s 指数増加）が AGENTS.md §10.4 に準拠
- 監視メトリクス（10 項目）と閾値が定義済み

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 判定: ✅ Pass (Critical: 0, High: 0, Medium: 0)

**概要**: テスト戦略が前回から大幅に充実。Saga 完走テスト、補償テスト（各ステップ障害注入）、PENDING_PAYMENT テスト、SagaRecoveryService テスト、OutboxPublisher テスト（動的バックオフ・リトライ）が追加された。

**ポジティブ評価**:
- テスト種別（単体/統合/Saga/Outbox/API/負荷）が体系的に整理
- Saga 補償テストの具体的シナリオ（各ステップ失敗→逆順補償→最終状態検証）が明記
- test-standards.instructions.md の AAA パターン・命名規則が反映されている
- Testcontainers.PostgreSql による実 DB テスト方針が明確

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 判定: ✅ Pass (Critical: 0, High: 0, Medium: 0)

**概要**: user.deleted 購読による注文データ匿名化（customer_id ハッシュ化、PII の [DELETED] 置換）、注文データの法的保持義務（税法 7 年）との両立が設計されている。PII ログマスキングルールも明確。

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 判定: ✅ Pass (Critical: 0, High: 0, Medium: 0)

**概要**: RTO 1 時間（ADR-0010 準拠）、/health + /health/ready（PostgreSQL + Redis + Kafka）、Dockerfile（マルチステージビルド + 非 root ユーザー + HEALTHCHECK）、OutboxPublisher Advisory Lock による複数インスタンス対応が全て解消。

**ポジティブ評価**:
- スケーリング戦略（min: 2, max: 10, CPU 70% トリガー）が明確
- Outbox/SagaLog/Idempotency-Key のクリーンアップ定期ジョブが定義済み
- CI/CD パイプライン（GitHub Actions: build→test→Docker→ACR→Container Apps）が記載

</details>

<details>
<summary>release-manager レビューレポート</summary>

### 判定: ✅ Pass (Critical: 0, High: 0, Medium: 0)

**概要**: Dockerfile、CI/CD パイプライン、RPO/RTO が ADR-0010 準拠に修正。バックアップ保持期間 35 日も適切。

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 判定: ✅ Pass (Critical: 0, High: 0, Low: 1)

**概要**: NuGet パッケージは nuget-dependency.instructions.md の禁止パッケージに該当しない。ClosedXML 0.x の注記付き継続使用は妥協可能。

**残存課題**:
- **L-2**: ClosedXML 0.104.* は技術的にはメジャーバージョン 0 だが、移行計画が記載されている

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 判定: ✅ Pass (Critical: 0, High: 0, Medium: 0)

**概要**: saga_logs テーブルによる Saga 状態の監査証跡、created_by/updated_by 監査カラム、ADR-0005/0006/0009/0010 への参照が追加された。Correlation ID 設計も §11 に記載。

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 判定: ✅ Pass (Critical: 0, High: 0, Medium: 0)

**概要**: PENDING_PAYMENT 時の 202 Accepted「お支払い処理中です。確定次第メールでお知らせします」のユーザーコミュニケーション設計が適切。エラーレスポンスが RFC 9457 準拠で統一されている。

</details>

<details>
<summary>tech-lead レビューレポート（総合評価）</summary>

### 判定: ⚠️ Warn (Critical: 0, High: 0, Medium: 0)

**総合評価**: check-report-1 の 7 Critical / 25 High が全件解消され、設計書の品質は大幅に向上した。販売管理サービスの中核機能（Saga オーケストレーション、Outbox パターン、SagaRecoveryService、Idempotency-Key）の設計が spec.md / ADR と整合しており、実装に着手可能な水準に近い。

**残存 High 4 件の評価**:
- H-1/H-2（送料設定不整合）: config の数値修正で速やかに解消可能。実装ブロッカーではない
- H-3（状態遷移欠落）: `Shipped → Returned` と `Cancelled → Refunded` は重要なビジネスシナリオだが、初期実装では対応しなくても MVP には影響しない。Phase 2 以降で追加可能
- H-4（決済プロトコル矛盾）: PENDING_PAYMENT のコードパスに影響するため、実装前に方針を確定すべき最重要課題

**実装着手の可否**: H-4（決済プロトコル方針）を確定すれば、残りの High は実装と並行して修正可能。**条件付きで実装着手を推奨**。

**AGENTS.md / Instructions 規約との整合性**:
- dotnet-coding-standards.instructions.md: ✅ primary constructor、CancellationToken、ILogger メッセージテンプレート、DI 規約に準拠
- api-design.instructions.md: ✅ POST cancel、RFC 9457、ページネーション、認証に準拠
- security-coding.instructions.md: ✅ IDOR 防止、PII マスキング、入力バリデーション、FluentValidation に準拠
- sql-schema-review.instructions.md: ✅ snake_case、TIMESTAMP WITH TIME ZONE、CHECK 制約、DECIMAL(12,2) に準拠
- test-standards.instructions.md: ✅ AAA パターン、命名規則、カバレッジ目標 80% に準拠
- dockerfile-infra.instructions.md: ✅ マルチステージビルド、非 root、HEALTHCHECK、バージョン固定に準拠
- nuget-dependency.instructions.md: ✅ 禁止パッケージなし（ClosedXML は注記付き）

</details>
