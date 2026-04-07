# fix-report-2: user-management-design.md

## 修正サマリー

| 指摘 ID | 重要度 | 内容 | 対応状況 |
|---------|--------|------|---------|
| H-01 | High | Outbox 原子性違反（SaveChangesAsync の後に PublishProfileUpdatedAsync） | ✅ 修正完了 |
| H-02 | High | Kafka 購読イベント欠落（order.confirmed, inventory.stock_updated） | ✅ 修正完了 |

## 修正詳細

### H-01: Outbox 原子性修正
- §14 UserService.UpdateProfileAsync のコード例を修正
- 修正前: `SaveChangesAsync(ct)` → `PublishProfileUpdatedAsync(user.Id, ct)`（別トランザクション）
- 修正後: `PublishProfileUpdatedAsync(user.Id, ct)` → `SaveChangesAsync(ct)`（同一 DbContext.SaveChangesAsync で User 更新と OutboxEvent INSERT を原子的にコミット）
- ADR-0005 準拠のコメント追加

### H-02: Kafka 購読イベント追加
§5 購読イベントテーブルに 2 イベントを追加:
1. `order.confirmed`（SalesManagementService）: 年間購入累計額の更新 + 会員ランクリアルタイム昇格判定（spec.md L2609 準拠）
2. `inventory.stock_updated`（InventoryManagementService）: WishlistItem の notifyOnRestock フラグに基づく在庫復活通知トリガー（spec.md L1545 準拠）

## 結果: Critical 0 / High 0（2 件修正完了）
