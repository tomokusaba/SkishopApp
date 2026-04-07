# Fix Report — inventory-management-design.md イテレーション 2

## 概要

| 項目 | 値 |
|------|-----|
| 対象 | `design-docs/inventory-management-design.md` |
| 基準レポート | `check-report-2.md` |
| 修正日 | 2026-04-03 |
| Critical 修正 | 1/1 |
| High 修正 | 4/4 |
| 合計 | **5/5 全件修正完了** |

---

## 修正詳細

### C-NEW-01 ✅ — products テーブル CHECK 制約が存在しないカラムを参照（DDL 実行不能）

**問題**: `ck_products_price`（`price`）、`ck_products_tax_rate`（`tax_rate`）、`ck_products_weight`（`weight`）の 3 CHECK 制約が products テーブルに存在しないカラムを参照していた。`price`/`tax_rate` は `prices` テーブルに分離済み。

**修正内容（選択肢 A を採用）**:
- `ck_products_price` / `ck_products_tax_rate` を **削除**（価格・税率は `prices` テーブルで管理）
- products テーブルに `weight DECIMAL(8,2) NULL CHECK (weight > 0)` カラムを **追加**（配送料計算用）
- `ck_products_weight` を正しいカラム参照として **維持**
- 補足コメントで設計判断を明記（価格関連は prices テーブル管理）
- EF Core Product エンティティに `[Column("weight")] public decimal? Weight` を追加

---

### H-NEW-01 ✅ — products SQL DDL に `row_version` カラム未定義

**問題**: EF Core Product エンティティに `[Timestamp] [Column("row_version")] public byte[] RowVersion` があるが、DDL カラム一覧に `row_version` が未記載。

**修正内容**:
- products テーブルの DDL カラム一覧に `row_version BYTEA NOT NULL` を追加
- 説明に「楽観的ロック用バージョン（EF Core `[Timestamp]`）」を明記

---

### H-NEW-02 ✅ — `price_histories.price_type` の CHECK 制約欠落

**問題**: `price_type VARCHAR(50) NOT NULL` に有効値の CHECK 制約がなく、他の状態・種別カラム（`inventory.status`, `reviews.status` 等）との設計方針が不統一。

**修正内容**:
- CHECK 制約セクションに `ck_price_histories_price_type: CHECK (price_type IN ('REGULAR', 'SALE', 'PROMOTION'))` を追加

---

### H-NEW-03 ✅ — `outbox_events` テーブルに `aggregate_type` カラム欠落

**問題**: ADR-0005 で `AggregateId` による重複排除が規定されているが、`aggregate_type` なしでは異なるエンティティ型（Product vs Inventory）の同一 ID が衝突する可能性。

**修正内容**:
- outbox_events テーブル DDL に `aggregate_type VARCHAR(100) NOT NULL` カラムを追加
- EF Core OutboxEvent エンティティに `[Column("aggregate_type")] [Required] [MaxLength(100)] public string AggregateType` プロパティを追加
- 在庫引当コードの OutboxEvent 生成時に `AggregateType = "Inventory"` を設定

---

### H-NEW-04 ✅ — OutboxPublisher の EventType→Kafka トピック名マッピング不整合

**問題**: `OutboxPublisher` が `evt.EventType`（例: `"InventoryReserved"`）をそのまま Kafka トピック名として使用していたが、§6 の発行イベント一覧ではトピック名は別名（例: `"inventory.reservations"`）で定義されている。

**修正内容（選択肢 A を採用）**:
- outbox_events テーブル DDL に `topic VARCHAR(255) NOT NULL` カラムを追加
- EF Core OutboxEvent エンティティに `[Column("topic")] [Required] [MaxLength(255)] public string Topic` プロパティを追加
- `OutboxPublisher` のイベント発行コードを `evt.EventType` → `evt.Topic` に変更
- 在庫引当コードの OutboxEvent 生成時に `Topic = "inventory.reservations"` を設定

---

## Medium/Low 指摘（今回未対応）

check-report-2 の Medium 12 件・Low 1 件は今回のスコープ外。次回イテレーションで対応検討。
