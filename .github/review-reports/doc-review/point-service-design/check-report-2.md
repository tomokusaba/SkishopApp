# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/point-service-design.md`（PointService 詳細設計書）— イテレーション 2（修正確認レビュー）
- **判定**: ⚠️ **Conditional Approval** — Critical 0 件、High 5 件検出（人間判断を介在）
- **レビュー日時**: 2026-04-03
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)
- **前回レビュー**: check-report-1.md（❌ Rejected — Critical 6 件、High 15 件）
- **修正履歴**: fix-report-1.md（Critical 6/6 修正、High 15/15 修正）

## 前回指摘の修正確認

### Critical 修正確認（6/6 解決）

| # | 指摘 ID | 修正内容 | 検証結果 |
|---|---------|---------|---------|
| 1 | C-01 | `PointBalance` → `PointAccount` にリネーム。Aggregate Root 名を spec.md 統一。`account_id` FK 追加 | ✅ 解決 — ER 図・テーブル定義・コード例全てで `PointAccount` 使用 |
| 2 | C-02 | gRPC サービス定義 §6.5 追加。`point.proto` に `ReservePoints`/`ReleasePoints`/`AwardPoints` 定義 | ✅ 解決 — proto package `skishop.point.v1` は spec.md 準拠。ただし `ConfirmPoints` RPC 欠落（下記 H-NEW-03） |
| 3 | C-03 | ティア昇格基準を年間購入金額（税込）に変更。還元率 1%/3%/5%/7% に統一 | ✅ 解決 — §8.2/§8.3 の計算例・ティア定義が spec.md §会員ランク制度と一致 |
| 4 | C-04 | MemberRank 管理を UserManagementService に移管。`MemberRankEventConsumer` 追加 | ✅ 解決 — 責務分担が spec.md 準拠。Redis キャッシュ同期設計あり |
| 5 | C-05 | `PointRule`/`PointCampaign`/`PointConversionRate` エンティティ追加 | ✅ 解決 — テーブル定義・ER 図・Repository/Service/PointCalculator 統合済み |
| 6 | C-06 | Outbox パターン追加。`outbox_events` テーブル、`OutboxPublisher` BackgroundService 追加 | ✅ 解決 — ADR-0005 準拠の設計。動的バックオフ (100ms-5s) 実装済み |

### High 修正確認（15/15 解決）

| # | 指摘 ID | 検証結果 | 備考 |
|---|---------|---------|------|
| 1 | H-01 | ✅ | CHECK 制約に `RESERVE`/`RELEASE`/`REFUND` 追加済み |
| 2 | H-02 | ✅（エスカレーション対応） | INTEGER 維持。spec.md 側の修正を提案として記録 |
| 3 | H-03 | ✅ | 全テーブルで `TIMESTAMP WITH TIME ZONE` 使用 |
| 4 | H-04 | ✅ | 降格評価日 4/1、猶予条件 83%、1 ランク降格に修正 |
| 5 | H-05 | ✅ | `user.deleted`/`payment.refunded`/`member_rank.updated` 購読追加 |
| 6 | H-06 | ✅ | トピック名（小文字ドット区切り）↔ record 名の対応表追加 |
| 7 | H-07 | ✅ | 還元率ベースの計算ロジックに変更。計算例が spec.md と一致 |
| 8 | H-08 | ✅ | §6.6 にサービス間認証（Client Credentials + JWT）追加 |
| 9 | H-09 | ✅ | DB 名 `pointdb` に修正（ADR-0006 準拠） |
| 10 | H-10 | ✅ | `pg_try_advisory_lock(hashtext('point_expiry'))` 追加。ただし返値処理に問題あり（下記 H-NEW-05） |
| 11 | H-11 | ✅ | `status VARCHAR(20)` + CHECK 制約に変更 |
| 12 | H-12 | ✅ | 全イベント record に `CorrelationId` 追加 |
| 13 | H-13 | ✅ | テストケース大幅拡充（PointCalculator/ExpiryService/Saga 冪等性/楽観的ロック） |
| 14 | H-14 | ✅ | バッチサイズ分割処理（do-while ループ）導入 |
| 15 | H-15 | ✅ | §18.2 に冪等性設計追加（orderId + type 重複チェック） |

---

## 技術スタック検証結果

| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| ランタイム | .NET 10 (C# 14) | .NET 10 (C# 14) | ✅ |
| フレームワーク | ASP.NET Core 10 Minimal API | ASP.NET Core 10 Minimal API | ✅ |
| ORM | EF Core 10 (Npgsql 10.*) | EF Core 10 | ✅ |
| DB | PostgreSQL (pointdb) | PostgreSQL (pointdb — ADR-0006) | ✅ |
| メッセージング | Confluent.Kafka 2.* | Confluent.Kafka 2.* | ✅ |
| キャッシュ | StackExchange.Redis 2.* | StackExchange.Redis 2.* | ✅ |
| バリデーション | FluentValidation 11.* | FluentValidation 11.* | ✅ |
| ログ | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| 可観測性 | OpenTelemetry 1.* | OpenTelemetry 1.* | ✅ |
| 耐障害性 | Polly 8.* + Http.Resilience 9.* | Polly 8.* + Http.Resilience 9.* | ✅ |
| Saga 通信 | gRPC (point.proto) + REST | gRPC (point.proto) | ✅ |
| Outbox パターン | ADR-0005 準拠 | ADR-0005 準拠 | ✅ |

---

## 指摘サマリー

| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ✅ Pass | 0 | 0 | 0 | 0 |
| architect | ⚠️ Warn | 0 | 2 | 1 | 0 |
| programing-reviewer | ⚠️ Warn | 0 | 1 | 3 | 1 |
| dba-reviewer | ⚠️ Warn | 0 | 1 | 3 | 1 |
| security-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| compliance-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| audit-reviewer | ✅ Pass | 0 | 0 | 1 | 0 |
| qa-manager | ✅ Pass | 0 | 0 | 0 | 0 |
| performance-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| infra-ops-reviewer | ⚠️ Warn | 0 | 0 | 1 | 1 |
| release-manager | ✅ Pass | 0 | 0 | 0 | 0 |
| oss-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| ux-accessibility-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| tech-lead | ⚠️ Warn | 0 | 1 | 0 | 0 |
| **合計** | | **0** | **5** | **9** | **3** |

---

## 判定根拠
- 判定ルール適用結果: Critical 0 件、High 5 件 → **⚠️ Conditional Approval**（人間の判断を介在）
- 最も重大な指摘: ポイント付与の二重パス問題（Kafka `order.created` + gRPC `AwardPoints` の両方がポイント付与をトリガーし得る設計）。OutboxPublisher の Advisory Lock 欠落（spec.md L988-999 準拠違反）。gRPC proto に `ConfirmPoints` RPC 欠落。
- 前回比: Critical 6 → 0（全解決）、High 15 → 5（新規 5 件）。設計品質は大幅改善。

---

## Critical/High 指摘一覧（修正必須）

### Critical 指摘: 0 件 ✅

### High 指摘: 5 件（全て新規）

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|---------------|----------|----------|
| H-NEW-01 | **High** | architect, tech-lead | ポイント付与設計 | §9.2, §18.1 | **ポイント付与の二重パス問題**。§9.2 で `order.created` Kafka イベント購読時に「ポイント付与」と記載し、§18.1 では Saga ステップ 7 の `gRPC: AwardPoints` でもポイント付与を行う。両方が同一注文に対してポイントを付与すると**二重付与**が発生する。spec.md §Saga ステップ定義ではステップ 7（gRPC）がポイント付与の正規パスであり、Kafka `order.created` での付与は矛盾する。 | §9.2 の `order.created` 購読の処理内容を「ポイント付与」から別の処理（例: ポイント付与の確認ログ記録、分析用イベント処理等）に変更するか、「Saga ステップ 7（gRPC: AwardPoints）がポイント付与の唯一のパスである」旨を明記して `order.created` での付与処理を削除する。 |
| H-NEW-02 | **High** | architect | Outbox パターン | §9.4 | **OutboxPublisher に Advisory Lock が未実装**。spec.md L988-999 の BackgroundService リーダー選出パターンで `OutboxPublisher` に `hashtext('outbox_publisher')` による `pg_try_advisory_lock` が必須と定義されている。§7.3 の `PointExpirationChecker` には正しく実装されているが、§9.4 の `OutboxPublisher` には Advisory Lock が欠落。`minReplicas: 2` 以上で同一イベントの二重発行リスクがある。 | §9.4 `OutboxPublisher.ExecuteAsync` の冒頭に `pg_try_advisory_lock(hashtext('outbox_publisher'))` によるインスタンス排他制御を追加する。§7.3 の `PointExpirationChecker` と同じパターンを適用。 |
| H-NEW-03 | **High** | architect | gRPC 設計 | §6.5, §18.1 | **gRPC proto に `ConfirmPoints` RPC が欠落**。§6.3 の REST 内部 API には `POST /api/v1/internal/points/confirm`（ポイント消費確定）が定義されているが、§6.5 の gRPC proto には `ConfirmPoints` RPC が存在しない。§18.1 では「成功: ポイント消費確定 (gRPC: ReleasePoints → type=REDEEM で確定)」と記載し、`ReleasePoints` を消費確定に転用しているが、`ReleasePoints` は本来「仮消費解放」（キャンセル時）の RPC であり、意味が矛盾する。| gRPC proto に `ConfirmPoints` RPC を追加するか、`ReleasePoints` の代わりに仮消費確定用の別名 RPC（例: `CommitPoints`）を追加する。あるいは §18.1 の記述を修正し、消費確定は REST `/confirm` エンドポイント経由とする設計判断を明記する。 |
| H-NEW-04 | **High** | dba-reviewer | データモデル | §5.1, §5.2 | **`point_expiries` テーブルに `account_id` FK カラムが欠落**。ER 図（§5.1）では `PointExpiry` に `account_id FK` を描画し、`PointAccount ||--o{ PointExpiry` のリレーションを示している。spec.md FK 制約（L2845）でも `PointExpiry.accountId → PointAccount` (ON DELETE CASCADE) を定義。しかし §5.2 の `point_expiries` テーブル定義には `account_id` カラムが存在せず、`user_id` のみ。ER 図と DDL 定義が矛盾。 | `point_expiries` に `account_id VARCHAR(36) FK(point_accounts.id) NOT NULL` を追加する。`ON DELETE CASCADE` を spec.md FK 制約に合わせて明記する。 |
| H-NEW-05 | **High** | programing-reviewer | コード品質 | §7.3 | **`pg_try_advisory_lock` の返値処理が不正**。`ExecuteSqlRawAsync` は SQL の影響行数を返す（SELECT 文の場合は `-1` が典型）。`pg_try_advisory_lock` の `true`/`false` 結果を正しく取得できない。`> 0` の条件は常に `false` となる可能性が高く、ロック取得判定が機能しない。 | `FromSqlInterpolated` でスカラー値を取得するか、`context.Database.SqlQueryRaw<bool>("SELECT pg_try_advisory_lock(hashtext('point_expiry'))").FirstOrDefaultAsync(ct)` を使用して、boolean 結果を正しくキャプチャする。解放処理も同様に修正する。 |

---

## Medium 指摘一覧

| # | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|-----------|---------|---------------|----------|----------|
| M-01 | dba-reviewer | DDL 整合性 | §5.2 `point_expiries` | **CHECK 制約のカラム名不一致**。テーブル定義のカラム名は `points` だが、CHECK 制約は `CHECK (amount > 0)` と `amount` を参照しており、DDL として不正。spec.md CHECK 制約（L2934）も `amount` を使用。 | CHECK 制約を `CHECK (points > 0)` に修正するか、カラム名を `amount` に変更して spec.md と統一する。 |
| M-02 | programing-reviewer | コード品質 | §7.3 | **`PointsExpiredEvent` 生成時に `CorrelationId` が空文字**。バッチ処理内で生成するイベントに `""` を渡しており、相関追跡が不可能。BackgroundService でも `Guid.NewGuid().ToString()` 等でバッチ単位の CorrelationId を生成すべき。 | バッチ処理開始時に `var batchCorrelationId = Guid.NewGuid().ToString()` を生成し、全イベントに付与する。 |
| M-03 | programing-reviewer | コード品質 | §7.3, §9.4 | **`DateTime.UtcNow` と `TimeProvider` の不整合**。§7.3 で `TimeProvider timeProvider` を DI 注入しているが、Outbox イベント作成時に `DateTime.UtcNow` を直接使用（AGENTS.md §10.4 `TimeProvider` DI 推奨に違反）。§9.4 `OutboxPublisher` も `DateTime.UtcNow` を使用。 | `DateTime.UtcNow` を `timeProvider.GetUtcNow().UtcDateTime` に置換する。`OutboxPublisher` にも `TimeProvider` を DI 注入する。 |
| M-04 | architect | DTO 設計 | §6.4 | **gRPC `ReservePointsRequest` と C# DTO の不整合**。gRPC proto の `ReservePointsRequest` には `idempotency_key` フィールドがあるが、C# DTO（§6.4）の `ReservePointsRequest` record には存在しない。§18.2 の冪等性設計では `orderId + type` で重複チェックしており、`idempotency_key` の用途が不明確。 | gRPC proto の `idempotency_key` フィールドの使用方針を明確化する。`orderId` を冪等性キーとして使用するなら proto から `idempotency_key` を削除するか、C# DTO にも追加して両方の冪等性チェックを設計する。 |
| M-05 | dba-reviewer | FK 制約 | §5.2 | **FK の ON DELETE/ON UPDATE アクションが未指定**。spec.md FK 制約定義（L2844-2845）で `PointTransaction.accountId → PointAccount` は `ON DELETE RESTRICT`、`PointExpiry.accountId → PointAccount` は `ON DELETE CASCADE` と明記。設計書のテーブル定義では FK カラムを `FK(point_accounts.id)` と記載するのみで、参照アクションが未記載。 | テーブル定義に `ON DELETE RESTRICT, ON UPDATE CASCADE`（PointTransaction）/ `ON DELETE CASCADE, ON UPDATE CASCADE`（PointExpiry）を明記する。 |
| M-06 | programing-reviewer | DTO 設計 | §6.4 | **`TierInfoResponse` のフィールドが C-04 修正と不整合**。`EarnRateMultiplier`（倍率）は廃止され spec.md では `PointRate`（還元率）に変更。`CurrentYearPoints` はポイントベースの名前だが、C-03 修正で年間購入金額ベースに変更済。`PointsToNextTier` も同様にポイントではなく金額ベースに変更が必要。 | `EarnRateMultiplier` → `PointRate`、`CurrentYearPoints` → `CurrentYearPurchaseAmount`、`PointsToNextTier` → `AmountToNextTier` に修正する。ただし、これらは UserManagementService から取得する値であるため、PointService が直接保持するか API 経由で取得するかの設計判断も必要。 |
| M-07 | infra-ops-reviewer | Dockerfile | §17 | **EXPOSE ポート不整合（M-10 未解決）**。`EXPOSE 5007` + `HEALTHCHECK CMD curl -f http://localhost:5007/health` だが、.NET 8+ のコンテナデフォルトポートは 8080。`ASPNETCORE_URLS` 環境変数でポートを明示するか、`EXPOSE 8080` に統一する必要がある。また `curl` が aspnet ランタイムイメージに含まれない場合がある（L-03 未解決）。 | `ENV ASPNETCORE_URLS=http://+:5007` を追加するか、`EXPOSE 8080` + `http://localhost:8080/health` に統一する。HEALTHCHECK は `wget -q --spider` または .NET HealthCheck ツールに変更する。 |
| M-08 | dba-reviewer | 命名規則 | §5.2 | **`PointTransaction.points` と spec.md `PointTransaction.amount` の命名不一致**。spec.md エンティティ一覧（L2770）は `amount DECIMAL(12,2)` と定義。設計書は `points INTEGER` を使用。型の違い（E-02 エスカレーション対応済み）とは別に、カラム名の不一致が残存。 | カラム名を統一する。spec.md 側を `points` に修正するか、設計書を `amount` に変更する。型は INTEGER 維持（E-02 対応方針）。 |
| M-09 | audit-reviewer | トレーサビリティ | §16, Program.cs | **Correlation ID ミドルウェアが Program.cs に未記載（M-11 未解決）**。AGENTS.md §11.2/§11.3 でミドルウェアパイプラインに `UseCorrelationId()` を含めることが必須。§16 の appsettings.json 設計にも §14 のメトリクス設計にも Correlation ID ミドルウェアの設定が含まれていない。 | Program.cs のミドルウェア設計セクションを追加し、AGENTS.md §11.3 のミドルウェアパイプライン順序に準拠した設計を記載する。 |

---

## Low 指摘一覧

| # | 出典 Agent | カテゴリ | 概要 |
|---|-----------|---------|------|
| L-01 | dba-reviewer | 命名規則 | `sql-schema-review.instructions.md` §2/§3 で制約名に `pk_`/`fk_`/`uq_`/`ck_`/`idx_` プレフィックスを要求しているが、設計書のテーブル定義では制約に名前が付与されていない（インライン制約のみ）。実装時に EF Core が自動生成する名前に依存する設計となっている。 |
| L-02 | infra-ops-reviewer | Dockerfile | HEALTHCHECK で `curl` を使用しているが、`mcr.microsoft.com/dotnet/aspnet:10.0` イメージに `curl` が含まれない可能性がある。`wget -q --spider` または dotnet-based ヘルスチェックツールの使用を推奨（L-03 の再掲）。 |
| L-03 | programing-reviewer | コード品質 | §8.3 `PointCalculator.IsApplicable` メソッドの `campaign.TargetProducts.Contains(category)` は JSON 配列カラム（JSONB）に対する文字列検索であり、実装時のデシリアライズ方法と一致しない可能性がある。JSON の構造を明確化するか、型定義（`List<string>` 等）を明記すべき。 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-01 | **高優先** | architect | H-NEW-01: ポイント付与の正規パスを決定する必要がある。Saga ステップ 7（gRPC `AwardPoints`）と Kafka `order.created` の両方でポイント付与が可能な設計になっている。spec.md では Saga gRPC が正規パスだが、`order.created` 購読の処理内容を明確にする必要がある。 | テックリード |
| E-02 | **継続** | dba-reviewer | ポイント値のデータ型（INTEGER vs DECIMAL(12,2)）。前回 E-02 から継続。設計書は INTEGER 維持（§19）。spec.md 側の修正 ADR が未起票。 | プロダクトオーナー |
| E-03 | **継続** | architect | Saga ステップの取引タイプ（RESERVE/RELEASE/REFUND）を spec.md の CHECK 制約に追加する ADR。前回 E-04 から継続。設計書側は対応済みだが spec.md 未修正。 | テックリード |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| — | — | — | 競合なし | — | — |

---

## ドキュメント横断分析

### サービス間整合性

| 整合性チェック項目 | 結果 | 詳細 |
|------------------|------|------|
| Kafka トピック名（spec.md ↔ 設計書） | ✅ 一致 | §9.1 で小文字ドット区切り命名に統一 |
| 購読イベント（spec.md ↔ 設計書） | ⚠️ 一部曖昧 | `user.deleted`/`payment.refunded`/`member_rank.updated` 追加済み。ただし `order.created` の処理が gRPC AwardPoints と重複（H-NEW-01） |
| エンティティ名（spec.md ↔ 設計書） | ✅ 一致 | `PointAccount` で統一 |
| FK 制約設計（spec.md ↔ 設計書） | ⚠️ 一部欠落 | `point_expiries.account_id` FK 欠落（H-NEW-04）、ON DELETE/UPDATE 未記載（M-05） |
| Saga ステップ定義（spec.md ↔ 設計書） | ⚠️ 一部欠落 | gRPC proto に `ConfirmPoints` RPC 欠落（H-NEW-03） |
| DB 名（ADR-0006 ↔ 設計書） | ✅ 一致 | `pointdb` |
| ポイント還元率（spec.md ↔ 設計書） | ✅ 一致 | 1%/3%/5%/7% で統一 |
| Outbox パターン（ADR-0005 ↔ 設計書） | ⚠️ 一部欠落 | Outbox テーブル・Publisher あり。ただし OutboxPublisher Advisory Lock 欠落（H-NEW-02） |
| ティア責務分担（spec.md ↔ 設計書） | ✅ 一致 | MemberRank は UserManagementService。PointService はイベント購読 + Redis 同期 |

### 前回からの改善点

| 領域 | 前回 | 今回 | 改善度 |
|------|------|------|--------|
| Aggregate Root 命名 | ❌ PointBalance（不一致） | ✅ PointAccount（spec.md 統一） | 完全解決 |
| Saga 通信プロトコル | ❌ REST のみ | ✅ gRPC + REST 二重公開 | 大幅改善（ConfirmPoints 残課題） |
| ティア管理責務 | ❌ PointService 内（境界違反） | ✅ UserManagementService 移管 + イベント同期 | 完全解決 |
| Outbox パターン | ❌ 完全欠落 | ✅ テーブル + Publisher 実装 | 大幅改善（Advisory Lock 残課題） |
| エンティティ網羅性 | ❌ 3 エンティティ欠落 | ✅ PointRule/Campaign/ConversionRate 追加 | 完全解決 |
| ポイント計算ロジック | ❌ 倍率ベース（spec.md 乖離） | ✅ 還元率ベース（spec.md 一致） | 完全解決 |

### 未解決・曖昧な領域

| 領域 | 影響 | ブロッカーリスク |
|------|------|-------------|
| ポイント付与の二重パス（H-NEW-01） | 二重付与リスク | **高**（実装前に設計判断が必要） |
| ConfirmPoints gRPC 定義欠落（H-NEW-03） | Saga フロー完全性 | **中**（REST fallback で暫定対応可能） |
| point_expiries.account_id FK 欠落（H-NEW-04） | ER 図と DDL の矛盾 | **中**（user_id で代替参照は可能） |
| spec.md の CHECK 制約拡張 ADR（E-03） | RESERVE/RELEASE/REFUND の正式承認 | **低**（設計書側は対応済み） |

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### 観点: ビジネス要件の完全性

**指摘: 0 件**

前回 Critical/High の全指摘（C-03 ティア基準矛盾、H-04 降格ルール矛盾、H-07 計算ロジック矛盾）が適切に修正されている。

- ティア昇格基準: spec.md §会員ランク制度と一致（年間購入金額ベース、1%/3%/5%/7%） ✅
- 降格ルール: 4 月 1 日評価、83% 猶予、1 ランク降格 ✅
- ポイント計算例: Bronze ¥10,000→100pt、Silver→300pt、Gold→500pt、Platinum→700pt ✅
- ポイントライフサイクル: 付与→仮消費→確定/解放→失効の全フロー記載 ✅

</details>

<details>
<summary>architect レビューレポート</summary>

### 観点: マイクロサービス設計・DDD・Saga 統合

**High (2件)**:
- H-NEW-01: ポイント付与の二重パス問題（Kafka `order.created` + gRPC `AwardPoints`）
- H-NEW-03: gRPC proto に `ConfirmPoints` RPC 欠落

**Medium (1件)**:
- M-04: gRPC proto の `idempotency_key` と C# DTO/ Saga 冪等性設計の不整合

**改善評価**:
- Bounded Context 境界: MemberRank の UserManagementService 移管は正しい設計判断 ✅
- Outbox パターン: ADR-0005 準拠の設計に改善 ✅
- gRPC + REST 二重公開: spec.md の設計方針に整合 ✅
- コンポーネント構成図: ルールサービス・キャンペーンサービスが追加され spec.md と整合 ✅

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 観点: コード例の正確性・C# 14 / .NET 10 規約

**High (1件)**:
- H-NEW-05: `pg_try_advisory_lock` の `ExecuteSqlRawAsync` 返値処理が不正

**Medium (3件)**:
- M-02: `PointsExpiredEvent` の CorrelationId が空文字
- M-03: `DateTime.UtcNow` と `TimeProvider` の不整合使用
- M-06: `TierInfoResponse` フィールドが C-04 修正後の設計と不整合

**Low (1件)**:
- L-03: `PointCalculator.IsApplicable` の JSONB カラムに対する文字列検索の曖昧さ

**改善評価**:
- primary constructor: 全 Service/Repository/BackgroundService で使用 ✅
- CancellationToken: 全 async メソッドに `ct = default` 伝搬 ✅
- record DTO: リクエスト/レスポンス DTO は不変 record ✅
- DI パターン: コンストラクタインジェクション（primary constructor）のみ使用 ✅
- PointCalculator: static → DI 対応インスタンスメソッドに改善 ✅
- ILogger<T>: メッセージテンプレート形式で出力 ✅（`$""` 文字列補間なし）

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 観点: DB スキーマ設計・EF Core マッピング

**High (1件)**:
- H-NEW-04: `point_expiries` テーブルに `account_id` FK カラム欠落（ER 図・spec.md FK 制約と矛盾）

**Medium (3件)**:
- M-01: `point_expiries` CHECK 制約のカラム名不一致（`amount` vs `points`）
- M-05: FK の ON DELETE/ON UPDATE アクション未指定
- M-08: `PointTransaction.points` vs spec.md `amount` の命名不一致

**Low (1件)**:
- L-01: 制約名に `pk_`/`fk_` 等のプレフィックスが未付与

**改善評価**:
- Aggregate Root: `PointAccount` で spec.md と統一 ✅
- `TIMESTAMP WITH TIME ZONE`: 全テーブルで使用 ✅
- CHECK 制約: `available_points >= 0`、`points != 0`、`status IN (...)` 等が追加 ✅
- Outbox テーブル: `outbox_events` が ADR-0005 準拠で設計 ✅
- インデックス: 部分インデックス（`WHERE status = 'ACTIVE'`、`WHERE status = 'PENDING'`）適切 ✅
- 楽観的ロック: `row_version BYTEA` + `[Timestamp]` ✅

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 観点: OWASP Top 10・認証/認可・秘密情報管理

**指摘: 0 件**

- サービス間認証: §6.6 に Client Credentials + JWT 設計が追加 ✅
- IDOR 防止: §12.2 でログインユーザー ID 照合を実装 ✅
- 管理者 API: `RequireAuthorization("AdminOnly")` 適用 ✅
- 秘密情報: appsettings.json にパスワード/トークンのハードコードなし ✅
- 入力バリデーション: DTO に Data Annotations 適用 ✅
- gRPC Interceptor: JWT トークン検証設計あり ✅

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 観点: GDPR・個人情報保護・PCI DSS

**指摘: 0 件**

- PII ログ制限: メトリクス（§14）でユーザー単位の残高は集計レベルで出力 ✅
- user.deleted イベント購読: ポイントアカウント無効化処理が設計済み ✅
- ログ出力: UserId のみ出力、メールアドレス等の PII はログに含まれない ✅

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 観点: トレーサビリティ・ADR 整合性

**Medium (1件)**:
- M-09: Correlation ID ミドルウェアが Program.cs 設計に未記載

**改善評価**:
- CorrelationId: 全イベント record に追加 ✅
- 監査ログ: 管理者ポイント調整の監査ログ記録が §12.3 に記載 ✅
- ADR 参照: C-04（Bounded Context）、C-06（Outbox）の変更理由・ADR 参照が設計書に記載 ✅
- Outbox イベント追跡: `outbox_events.status` (PENDING/PUBLISHED/FAILED) + `retry_count` で発行状態を追跡可能 ✅

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 観点: テスト戦略・カバレッジ

**指摘: 0 件**

- テスト命名: `Should_期待結果_When_条件` パターン準拠 ✅
- AAA パターン: Arrange/Act/Assert が明確に分離 ✅
- テストカバレッジ: PointCalculator（ランク別 Theory）、ExpiryService（バッチ/部分失効）、Saga 冪等性、楽観的ロック競合のテストケースが記載 ✅
- Testcontainers: §15.2 で PostgreSql コンテナによる統合テストを設計 ✅
- WebApplicationFactory: 統合テストで使用 ✅
- NSubstitute + Shouldly: 単体テストで適切に使用 ✅

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 観点: パフォーマンス SLA・スケーラビリティ

**指摘: 0 件**

- バッチ処理: `ExpiryBatchSize` 件ずつ独立トランザクション（H-14 対応済み） ✅
- Redis キャッシュ: 残高 5 分、ランク 24 時間、ティア定義 1 時間の適切な TTL 設計 ✅
- キャッシュ無効化: ポイント変動時の即時無効化 ✅
- Outbox 動的バックオフ: 100ms-5s の指数バックオフ ✅
- 部分インデックス: `WHERE status = 'ACTIVE'`/`WHERE status = 'PENDING'` で読取効率化 ✅
- gRPC SLO: ポイント仮消費 80ms バジェット（spec.md ステップ 4 準拠） ✅

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 観点: コンテナ設計・可観測性・ヘルスチェック

**Medium (1件)**:
- M-07: Dockerfile EXPOSE ポート不整合 + HEALTHCHECK curl 問題

**Low (1件)**:
- L-02: HEALTHCHECK の curl が aspnet イメージに含まれない可能性

**改善評価**:
- マルチステージビルド: sdk → aspnet ✅
- 非 root ユーザー: `skishop` ユーザー作成 + `USER skishop` ✅
- ベースイメージバージョン固定: `10.0`（latest 不使用） ✅
- ヘルスチェック: `/health`（Liveness）+ `/health/ready`（Readiness）✅
- OpenTelemetry: §14 でメトリクス定義あり ✅

</details>

<details>
<summary>release-manager レビューレポート</summary>

### 観点: リリース戦略・バージョニング

**指摘: 0 件**

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 観点: NuGet ライセンス・依存関係脆弱性

**指摘: 0 件**

- `Microsoft.Extensions.Http.Resilience 9.*` が §2 に追加済み（M-12 対応） ✅
- 全パッケージが AGENTS.md §8.1 の許可リストに含まれる ✅
- プレリリースパッケージなし ✅
- 禁止パッケージ（Newtonsoft.Json, log4net 等）使用なし ✅

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 観点: UX 設計・WCAG 2.1 準拠

**指摘: 0 件**

（PointService はバックエンドサービスであり、UX/アクセシビリティの直接的な指摘対象外）

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 観点: 技術標準横断適合性・実装実現可能性

**High (1件)**:
- H-NEW-01: ポイント付与の二重パス問題は設計上の重大なリスク。実装前に Saga gRPC パスと Kafka イベントパスの責務を明確に分離する必要がある

**総合評価**:
前回の 6 Critical + 15 High が全て修正され、設計品質は大幅に改善された。残存する 5 件の High 指摘は全て修正可能な範囲であり、構造的な設計変更は不要。特に以下の改善が優れている:

1. MemberRank の責務分担明確化（C-04）: イベント駆動による疎結合設計が DDD 原則に適合
2. gRPC サービス定義追加（C-02）: spec.md の Saga 設計と整合。proto バージョニングも適切
3. Outbox パターン導入（C-06）: ADR-0005 準拠の信頼性のある設計
4. ポイント計算ロジックの統一（C-03/H-07）: spec.md の還元率に完全準拠

</details>
