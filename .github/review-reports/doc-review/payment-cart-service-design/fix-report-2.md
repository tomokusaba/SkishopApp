# Fix Report — Iteration 2

## 対象ドキュメント
`design-docs/payment-cart-service-design.md`

## 修正日時
2026-04-03

## 修正サマリー

| # | 指摘 ID | 重要度 | 指摘概要 | 修正内容 | 結果 |
|---|---------|--------|----------|----------|------|
| 1 | H-01 | High | PaymentMethod EF Core エンティティクラスが §21 に未記載 | §21 の OutboxEvent エンティティ直後に `PaymentMethod` エンティティクラスを追加。`[Table("payment_methods")]`、`[Column("snake_case")]`、`[Key]`、`[Required]`、`[MaxLength]` 属性を完備。spec.md の属性定義（id, userId, type, provider, accountReference, isDefault, expiryDate, billingAddressId, createdAt, updatedAt）に準拠 | ✅ 修正済み |
| 2 | H-02 | High | DB 名 `paymentcartdb` ≠ spec.md `paymentdb` | サービス情報テーブルの DB 名を `paymentcartdb` → `paymentdb` に修正（spec.md L5640 `postgres.AddDatabase("paymentdb")` および ADR-0006 準拠） | ✅ 修正済み |
| 3 | H-03 | High | `inventory.reserved` / `inventory.reservation.failed` が spec.md Kafka トピック一覧に未定義 | 購読イベント表から `InventoryReserved`（`inventory.reserved`）と `InventoryReservationFailed`（`inventory.reservation.failed`）の 2 行を削除。ADR-0009 に基づき Saga オーケストレーション（gRPC 同期呼び出し）で在庫予約通信を行う設計判断注記を追加 | ✅ 修正済み |
| 4 | H-04 | High | `AddCartItemRequest` がクライアント指定 `UnitPrice` を受け入れ — 価格操作リスク | `AddCartItemRequest` DTO から `ProductName`、`Sku`、`UnitPrice` フィールドを削除し、`ProductId` と `Quantity` のみに簡素化。サーバーサイドで InventoryManagementService API から商品情報を取得してスナップショット保存する設計判断コメントを追加 | ✅ 修正済み |

## 修正統計
- **High 修正**: 4/4 件（100%）
- **Medium 未修正**: 9 件（次イテレーションで対応）
- **Low 未修正**: 4 件（次イテレーションで対応）

## 修正箇所詳細

### H-01: PaymentMethod エンティティ追加
- **箇所**: §21 EF Core エンティティ定義（OutboxEvent の直後）
- **追加内容**: `[Table("payment_methods")] public class PaymentMethod` — 10 プロパティ（Id, UserId, Type, Provider, AccountReference, IsDefault, ExpiryDate, BillingAddressId, CreatedAt, UpdatedAt）

### H-02: DB 名修正
- **箇所**: サービス情報テーブル（L193）
- **変更**: `PostgreSQL (paymentcartdb)` → `PostgreSQL (paymentdb)`

### H-03: Kafka 購読トピック削除 + 設計判断注記
- **箇所**: §購読イベントテーブル（L430-431）
- **削除**: `InventoryReserved` / `InventoryReservationFailed` の 2 行
- **追加**: 設計判断ブロッククォート — Saga オーケストレーション（ADR-0009）gRPC 同期呼び出し方式の根拠を説明

### H-04: AddCartItemRequest 簡素化
- **箇所**: §23 リクエスト DTO（L1475）
- **削除フィールド**: `ProductName`、`Sku`、`UnitPrice`
- **残存フィールド**: `ProductId`、`Quantity`
- **追加**: サーバーサイド価格取得の設計判断コメント
