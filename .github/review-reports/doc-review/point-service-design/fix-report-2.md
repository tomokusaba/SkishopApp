# 修正レポート — point-service-design.md（イテレーション 2）

## 概要

| 項目 | 内容 |
|------|------|
| 対象ドキュメント | `design-docs/point-service-design.md` |
| 元レポート | `check-report-2.md` |
| 修正日 | 2026-04-03 |
| 修正対象 | High 5 件 |
| 修正完了 | 5/5 件 |

## 修正詳細

### H-NEW-01: ポイント付与の二重パス問題（修正済み ✅）

**問題**: §9.2 で `order.created` Kafka イベント購読時に「ポイント付与」と記載し、§18.1 では Saga ステップ 7 の `gRPC: AwardPoints` でもポイント付与を行う設計。二重付与リスク。

**修正内容**:
1. §9.2 `order.created` の処理内容を「ポイント付与」→「注文情報のログ記録・分析用データ蓄積」に変更。Saga gRPC が唯一の正規パスである旨を注記追加
2. §18.1 項目 3 を修正。Kafka `order.created` 経由ではなく `gRPC: AwardPoints` が唯一のポイント付与パスであることを明記
3. §18.1 項目 2 の成功パスを `ReleasePoints` 転用から `ConfirmPoints`（H-NEW-03 と連動）に変更

### H-NEW-02: OutboxPublisher に Advisory Lock 未実装（修正済み ✅）

**問題**: spec.md L988-999 で `OutboxPublisher` に `pg_try_advisory_lock(hashtext('outbox_publisher'))` が必須だが、§9.4 の実装に欠落。

**修正内容**:
- §9.4 `OutboxPublisher.ExecuteAsync` の冒頭に `pg_try_advisory_lock(hashtext('outbox_publisher'))` を追加
- §7.3 `PointExpirationChecker` と同じパターン（ロック取得 → try/finally → ロック解放）を適用
- ロック未取得時は 10 秒待機してスキップするログ出力を追加
- `SqlQueryRaw<bool>` を使用（H-NEW-05 と整合）

### H-NEW-03: gRPC proto に ConfirmPoints RPC 欠落（修正済み ✅）

**問題**: §6.3 の REST 内部 API には `POST /api/v1/internal/points/confirm` があるが、§6.5 の gRPC proto に `ConfirmPoints` RPC が存在しない。`ReleasePoints` を消費確定に転用しており意味が矛盾。

**修正内容**:
- §6.5 gRPC proto の `service PointService` に `rpc ConfirmPoints (ConfirmPointsRequest) returns (ConfirmPointsResponse)` を追加
- `ConfirmPointsRequest` メッセージ（user_id, order_id, idempotency_key）を追加
- `ConfirmPointsResponse` メッセージ（success, confirmed_points, new_balance, error_message）を追加
- §18.1 の成功パスを `ReleasePoints` → `ConfirmPoints` に変更

### H-NEW-04: point_expiries テーブルに account_id FK 欠落（修正済み ✅）

**問題**: ER 図（§5.1）では `PointExpiry` に `account_id FK` があり `PointAccount ||--o{ PointExpiry` のリレーションを示すが、§5.2 の DDL には `account_id` カラムが存在しない。

**修正内容**:
- `point_expiries` テーブルに `account_id VARCHAR(36) NOT NULL, FK(point_accounts.id) ON DELETE CASCADE ON UPDATE CASCADE` カラムを追加
- `idx_point_expiry_account_id` インデックスを追加
- 副次修正（M-01）: CHECK 制約を `CHECK (amount > 0)` → `CHECK (points > 0)` に修正（カラム名と一致させた）

### H-NEW-05: ExecuteSqlRawAsync で Advisory Lock 結果取得不可（修正済み ✅）

**問題**: §7.3 `PointExpirationChecker` で `ExecuteSqlRawAsync` を使用して `pg_try_advisory_lock` の結果を取得しているが、`ExecuteSqlRawAsync` は影響行数を返すため `> 0` 条件が正しく機能しない。

**修正内容**:
- §7.3 ロック取得: `ExecuteSqlRawAsync(...) > 0` → `SqlQueryRaw<bool>(...).FirstOrDefaultAsync(stoppingToken)` に変更
- §7.3 ロック解放: `ExecuteSqlRawAsync` → `SqlQueryRaw<bool>(...).FirstOrDefaultAsync(stoppingToken)` に変更
- §9.4 OutboxPublisher（H-NEW-02 新規追加分）も同じ `SqlQueryRaw<bool>` パターンを使用

## 副次修正

| # | 対応チェックレポート指摘 | 修正内容 |
|---|----------------------|---------|
| 1 | M-01 | `point_expiries` の CHECK 制約を `CHECK (amount > 0)` → `CHECK (points > 0)` に修正 |

## 未修正（Medium/Low — 次イテレーション以降）

- M-02〜M-09: 9 件（Medium）
- L-01〜L-03: 3 件（Low）
- E-01〜E-03: 3 件（エスカレーション — 人間判断待ち）
