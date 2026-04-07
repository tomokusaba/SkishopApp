# SalesManagementService 検証計画書（verification-plan.md）

本ドキュメントは SalesManagementService の全エンドポイントに対する動作検証の手順とフォーマットを定義する。
検証結果は `verification-report.md` に記録し、SalesManagementService の使い方を理解するためのリファレンスとして活用する。

---

## 目次

1. [検証の目的](#1-検証の目的)
2. [前提条件](#2-前提条件)
3. [エンドポイント一覧](#3-エンドポイント一覧)
4. [検証手順](#4-検証手順)
5. [verification-report.md のフォーマット](#5-verification-reportmd-のフォーマット)
6. [テスト用データ](#6-テスト用データ)
7. [トラブルシューティング](#7-トラブルシューティング)
8. [実行手順チェックリスト](#8-実行手順チェックリスト)

---

## 1. 検証の目的

1. **機能確認**: 全 19 エンドポイント（+ 2 ヘルスチェック = 合計 21）が設計通りに動作することを確認する
2. **使い方の文書化**: 各 API の呼び出し方法・パラメータ・レスポンスを実例で示す
3. **異常系の確認**: バリデーションエラー、認証エラー、認可エラー（IDOR 防止）等の動作を確認する
4. **セキュリティ検証**: IDOR 保護、管理者専用エンドポイントのアクセス制御を確認する

---

## 2. 前提条件

### 2.1 環境構成

| 項目 | 値 |
|------|-----|
| SalesManagementService URL | `http://localhost:5004` |
| AuthService URL | `http://localhost:5001` |
| PostgreSQL | `localhost:5432`（DB: `salesdb`） |
| Redis | `localhost:6379` |
| Kafka | `localhost:9092` |

### 2.2 起動コマンド

```bash
# Docker Compose で SalesManagementService と依存サービスを起動
cd /Users/yoterada/GitHub/DotNet-Skishop-App
docker compose up -d auth-service sales-management-service
```

> **📝 注意**: SalesManagementService は AuthService が発行した JWT トークンを使用して認証するため、AuthService も同時に起動する必要がある。

### 2.3 事前確認コマンド

```bash
# SalesManagementService ヘルスチェック
curl -s http://localhost:5004/health
# 期待結果: Healthy

# AuthService ヘルスチェック
curl -s http://localhost:5001/health
# 期待結果: Healthy
```

### 2.4 テスト用トークン取得手順

検証開始前に AuthService でユーザーを登録し、JWT トークンを取得する。

#### 一般ユーザートークン取得

```bash
# 1. ユーザー登録
curl -s -X POST http://localhost:5001/api/v1/auth/users \
  -H "Content-Type: application/json" \
  -d '{
    "email": "salestest@example.com",
    "password": "Password123!",
    "firstName": "Sales",
    "lastName": "TestUser",
    "username": "salestest"
  }'
```

```bash
# 2. ユーザーステータスを ACTIVE に変更（DB 直接操作）
docker exec -it skishop-postgres psql -U skishop -d authdb \
  -c "UPDATE users SET status = 'ACTIVE', email_verified = true WHERE email = 'salestest@example.com';"
```

```bash
# 3. ログインしてトークン取得
curl -s -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "salestest@example.com",
    "password": "Password123!"
  }'

# レスポンスの accessToken を環境変数に保存
export ACCESS_TOKEN="eyJhbGci..."
export USER_ID="取得したユーザーID"
```

#### 管理者トークン取得

```bash
# 1. 管理者ユーザー登録
curl -s -X POST http://localhost:5001/api/v1/auth/users \
  -H "Content-Type: application/json" \
  -d '{
    "email": "salesadmin@example.com",
    "password": "AdminPass123!",
    "firstName": "Sales",
    "lastName": "Admin",
    "username": "salesadmin"
  }'
```

```bash
# 2. 管理者ステータスとロール設定
docker exec -it skishop-postgres psql -U skishop -d authdb \
  -c "UPDATE users SET status = 'ACTIVE', email_verified = true, role = 'Admin' WHERE email = 'salesadmin@example.com';"
```

```bash
# 3. 管理者ログイン
curl -s -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "salesadmin@example.com",
    "password": "AdminPass123!"
  }'

# レスポンスの accessToken を環境変数に保存
export ADMIN_TOKEN="eyJhbGci..."
export ADMIN_USER_ID="取得した管理者ユーザーID"
```

### 2.5 テスト用注文データの準備

一部の検証（出荷管理、返品管理、レポート等）は事前に注文データが存在する必要がある。
注文作成 API（Phase 2）を先に実行し、`ORDER_ID`、`ORDER_NUMBER`、`ORDER_ITEM_ID` を取得しておくこと。

---

## 3. エンドポイント一覧

### 3.1 ヘルスチェック

| メソッド | パス | 説明 | 認証 |
|---------|------|------|------|
| GET | `/health` | Liveness チェック | 不要 |
| GET | `/health/ready` | Readiness チェック（PostgreSQL, Redis, Kafka 疎通） | 不要 |

### 3.2 注文管理（OrderEndpoints）

| メソッド | パス | 説明 | 認証 | 備考 |
|---------|------|------|------|------|
| GET | `/api/v1/orders/{orderId}` | 注文詳細取得 | 必要 | IDOR 保護: `GetByIdAndUserIdAsync` で自分の注文のみ取得可能 |
| GET | `/api/v1/orders/number/{orderNumber}` | 注文番号で取得 | 必要 | Admin: 全注文 / User: 自分の注文のみ（IDOR 保護） |
| GET | `/api/v1/orders/customer/{customerId}` | 顧客の注文一覧取得 | 必要 | Admin: 全顧客 / User: 自分のみ（IDOR 保護） |
| GET | `/api/v1/orders/search` | 注文検索 | Admin のみ | ページネーション・フィルタリング対応（`CustomerId`, `Status`, `PaymentStatus`） |
| POST | `/api/v1/orders` | 注文作成 | 必要 | `Idempotency-Key` ヘッダー必須、レート制限: `order-create` |
| PUT | `/api/v1/orders/{orderId}/status` | 注文ステータス更新 | Admin のみ | |
| POST | `/api/v1/orders/{orderId}/cancel` | 注文キャンセル | 必要 | オーナーのみ（`GetByIdAndUserIdAsync` で検証） |

### 3.3 出荷管理（ShipmentEndpoints）

| メソッド | パス | 説明 | 認証 | 備考 |
|---------|------|------|------|------|
| GET | `/api/v1/shipments` | 出荷一覧取得 | Admin のみ | ステータスフィルタリング対応（デフォルト: `PREPARING`） |
| GET | `/api/v1/shipments/{id}` | 出荷詳細取得 | Admin のみ | |
| GET | `/api/v1/shipments/order/{orderId}` | 注文に紐づく出荷取得 | Admin のみ | |
| POST | `/api/v1/shipments` | 出荷作成 | Admin のみ | レート制限: `admin-api` |
| PUT | `/api/v1/shipments/{id}/status` | 出荷ステータス更新 | Admin のみ | レート制限: `admin-api` |
| PUT | `/api/v1/shipments/{id}/tracking` | 追跡番号更新 | Admin のみ | レート制限: `admin-api`（内部的には `UpdateShipmentAsync` を使用） |

### 3.4 返品管理（ReturnEndpoints）

| メソッド | パス | 説明 | 認証 | 備考 |
|---------|------|------|------|------|
| GET | `/api/v1/returns` | 返品一覧取得 | Admin のみ | ステータスフィルタリング対応（空文字で全件） |
| GET | `/api/v1/returns/{id}` | 返品詳細取得 | 必要 | IDOR 保護: `CustomerId` と照合（Admin は全返品取得可能） |
| GET | `/api/v1/returns/order/{orderId}` | 注文に紐づく返品一覧取得 | 必要 | IDOR 保護: `GetByIdAndUserIdAsync` で注文のオーナーシップを検証 |
| POST | `/api/v1/returns` | 返品申請作成 | 必要 | `userId` は JWT から取得 |
| PUT | `/api/v1/returns/{id}/status` | 返品ステータス更新（処理） | Admin のみ | `ProcessReturnAsync` で処理 |

### 3.5 レポート（ReportEndpoints）

| メソッド | パス | 説明 | 認証 | 備考 |
|---------|------|------|------|------|
| GET | `/api/v1/reports/sales` | 売上レポート取得 | Admin のみ | バリデーション: `fromDate <= toDate`、`toDate <= 今日`、期間 <= 366日 |

---

## 4. 検証手順

検証は以下の順序で実施する。依存関係を考慮した順番となっている。

---

### Phase 0: ヘルスチェック

#### テスト 0-1: GET /health（Liveness）

```bash
curl -s http://localhost:5004/health
```

**期待するレスポンス（200 OK）:**

```
Healthy
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] レスポンスが `Healthy` である

#### テスト 0-2: GET /health/ready（Readiness）

```bash
curl -s http://localhost:5004/health/ready
```

**期待するレスポンス（200 OK）:**

```
Healthy
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] PostgreSQL、Redis、Kafka の疎通が確認できている

---

### Phase 1: 認証・認可の基本確認

> **📝 前提**: AuthService でユーザー登録・ログイン済み。`ACCESS_TOKEN`、`ADMIN_TOKEN` を環境変数に設定済み。

#### テスト 1-1: 認証なしでのアクセス（401 Unauthorized）

```bash
curl -s -w "\nHTTP Status: %{http_code}" http://localhost:5004/api/v1/orders/customer/${USER_ID}
```

**期待するレスポンス（401 Unauthorized）:**

**検証項目:**
- [ ] ステータスコード 401 を返す

#### テスト 1-2: 一般ユーザーによる管理者専用エンドポイントアクセス（403 Forbidden）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X GET "http://localhost:5004/api/v1/orders/search?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（403 Forbidden）:**

**検証項目:**
- [ ] ステータスコード 403 を返す
- [ ] 管理者専用エンドポイントへのアクセスが拒否される

---

### Phase 2: 注文管理

> **📝 前提**: `ACCESS_TOKEN`、`USER_ID` を環境変数に設定済み。

#### テスト 2-1: POST /api/v1/orders - 注文作成（正常系）

```bash
export IDEMPOTENCY_KEY=$(uuidgen)

curl -s -X POST http://localhost:5004/api/v1/orders \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: ${IDEMPOTENCY_KEY}" \
  -d '{
    "customerId": "'${USER_ID}'",
    "items": [
      {
        "productId": "prod-001",
        "productName": "スキー板 Model X",
        "sku": "SKI-MODEL-X-170",
        "unitPrice": 89000,
        "quantity": 1
      },
      {
        "productId": "prod-002",
        "productName": "スキーブーツ Pro",
        "sku": "BOOT-PRO-27",
        "unitPrice": 45000,
        "quantity": 1
      }
    ],
    "shippingAddress": {
      "recipientName": "山田 太郎",
      "postalCode": "100-0001",
      "prefecture": "東京都",
      "city": "千代田区",
      "addressLine1": "丸の内1-1-1",
      "addressLine2": "SkiShopビル3F",
      "phoneNumber": "03-1234-5678"
    },
    "paymentMethod": "CREDIT_CARD",
    "couponCode": null,
    "usedPoints": 0,
    "notes": "配送時間指定: 午前中"
  }'
```

**期待するレスポンス（201 Created）:**

```json
{
  "id": "uuid-order-001",
  "orderNumber": "ORD-20260406-XXXXX",
  "customerId": "uuid-user-xxx",
  "orderDate": "2026-04-06T17:00:00+00:00",
  "status": "PENDING",
  "paymentStatus": "PENDING",
  "subtotalAmount": 134000,
  "taxAmount": 13400,
  "shippingFee": 0,
  "discountAmount": 0,
  "totalAmount": 147400,
  "currencyCode": "JPY",
  "createdAt": "2026-04-06T17:00:00+00:00"
}
```

**検証項目:**
- [ ] ステータスコード 201 を返す
- [ ] `Location` ヘッダーに新規注文の URI が含まれる
- [ ] `orderNumber` が生成されている
- [ ] `status` が `PENDING` である
- [ ] `totalAmount` が正しく計算されている（小計 + 税 + 送料 - 割引）

**環境変数に保存:**
```bash
export ORDER_ID="取得した注文ID"
export ORDER_NUMBER="取得した注文番号"
```

#### テスト 2-2: POST /api/v1/orders - Idempotency-Key なし（400 Bad Request）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5004/api/v1/orders \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "'${USER_ID}'",
    "items": [{"productId": "prod-001", "productName": "Test", "sku": "TEST", "unitPrice": 1000, "quantity": 1}],
    "shippingAddress": {"recipientName": "Test", "postalCode": "100-0001", "prefecture": "東京都", "city": "千代田区", "addressLine1": "Test", "addressLine2": null, "phoneNumber": "03-0000-0000"},
    "paymentMethod": "CREDIT_CARD"
  }'
```

**期待するレスポンス（400 Bad Request）:**

```json
{
  "error": "Idempotency-Key ヘッダーは必須です"
}
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] エラーメッセージに `Idempotency-Key` が必須であることが示される

#### テスト 2-3: POST /api/v1/orders - 同一 Idempotency-Key での再送信（冪等性確認）

```bash
# 同一の Idempotency-Key で再送信
curl -s -X POST http://localhost:5004/api/v1/orders \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: ${IDEMPOTENCY_KEY}" \
  -d '{
    "customerId": "'${USER_ID}'",
    "items": [{"productId": "prod-001", "productName": "スキー板 Model X", "sku": "SKI-MODEL-X-170", "unitPrice": 89000, "quantity": 1}],
    "shippingAddress": {"recipientName": "山田 太郎", "postalCode": "100-0001", "prefecture": "東京都", "city": "千代田区", "addressLine1": "丸の内1-1-1", "addressLine2": null, "phoneNumber": "03-1234-5678"},
    "paymentMethod": "CREDIT_CARD"
  }'
```

**期待するレスポンス（201 または 200）:**

**検証項目:**
- [ ] 同一の注文データが返される（新規注文が重複作成されていない）
- [ ] 冪等性が保証されている

#### テスト 2-4: POST /api/v1/orders - バリデーションエラー（422 Unprocessable Entity）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5004/api/v1/orders \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $(uuidgen)" \
  -d '{
    "customerId": "'${USER_ID}'",
    "items": [],
    "shippingAddress": {"recipientName": "", "postalCode": "", "prefecture": "", "city": "", "addressLine1": "", "phoneNumber": ""},
    "paymentMethod": ""
  }'
```

**期待するレスポンス（400 Bad Request）:**

```json
{
  "type": "...",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Items": ["..."],
    "ShippingAddress.RecipientName": ["..."],
    "PaymentMethod": ["..."]
  }
}
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] 各フィールドのバリデーションエラーが返される

#### テスト 2-5: GET /api/v1/orders/{orderId} - 注文詳細取得（オーナー）

```bash
curl -s -X GET http://localhost:5004/api/v1/orders/${ORDER_ID} \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-order-001",
  "orderNumber": "ORD-20260406-XXXXX",
  "customerId": "uuid-user-xxx",
  "orderDate": "2026-04-06T17:00:00+00:00",
  "status": "PENDING",
  "paymentStatus": "PENDING",
  "subtotalAmount": 134000,
  "taxAmount": 13400,
  "shippingFee": 0,
  "discountAmount": 0,
  "totalAmount": 147400,
  "currencyCode": "JPY",
  "createdAt": "2026-04-06T17:00:00+00:00"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 自分の注文を取得できる

#### テスト 2-6: GET /api/v1/orders/{orderId} - 他ユーザーの注文取得（IDOR 防止）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X GET http://localhost:5004/api/v1/orders/other-order-id \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（404 Not Found または 403 Forbidden）:**

**検証項目:**
- [ ] 他ユーザーの注文は取得できない（IDOR 防止）

#### テスト 2-7: GET /api/v1/orders/number/{orderNumber} - 注文番号で取得

```bash
curl -s -X GET http://localhost:5004/api/v1/orders/number/${ORDER_NUMBER} \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 注文番号で注文を取得できる

#### テスト 2-8: GET /api/v1/orders/customer/{customerId} - 顧客の注文一覧取得

```bash
curl -s -X GET "http://localhost:5004/api/v1/orders/customer/${USER_ID}?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "items": [...],
  "page": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] ページネーションが機能している
- [ ] 自分の注文のみが返される

#### テスト 2-9: GET /api/v1/orders/customer/{customerId} - 他ユーザーの注文一覧取得（IDOR 防止）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X GET "http://localhost:5004/api/v1/orders/customer/other-user-id?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（403 Forbidden）:**

**検証項目:**
- [ ] ステータスコード 403 を返す
- [ ] 他ユーザーの注文一覧は取得できない

#### テスト 2-10: GET /api/v1/orders/search - 注文検索（管理者）

```bash
curl -s -X GET "http://localhost:5004/api/v1/orders/search?page=1&pageSize=10&status=PENDING" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "items": [...],
  "page": 1,
  "pageSize": 10,
  "totalCount": ...,
  "totalPages": ...
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] ステータスフィルタリングが機能している
- [ ] 全顧客の注文が検索できる（管理者権限）

#### テスト 2-11: PUT /api/v1/orders/{orderId}/status - ステータス更新（管理者）

```bash
curl -s -X PUT http://localhost:5004/api/v1/orders/${ORDER_ID}/status \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"status": "CONFIRMED"}'
```

**期待するレスポンス（204 No Content）:**

**検証項目:**
- [ ] ステータスコード 204 を返す
- [ ] 注文ステータスが更新される

#### テスト 2-12: PUT /api/v1/orders/{orderId}/status - 一般ユーザーによるステータス更新（403 Forbidden）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X PUT http://localhost:5004/api/v1/orders/${ORDER_ID}/status \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"status": "SHIPPED"}'
```

**期待するレスポンス（403 Forbidden）:**

**検証項目:**
- [ ] ステータスコード 403 を返す
- [ ] 一般ユーザーはステータスを更新できない

#### テスト 2-13: POST /api/v1/orders/{orderId}/cancel - 注文キャンセル

> **📝 前提**: キャンセル検証用に新しい注文を作成する（キャンセル後は元に戻せないため）

```bash
# キャンセル用の新規注文作成
export CANCEL_IDEMPOTENCY_KEY=$(uuidgen)
CANCEL_ORDER=$(curl -s -X POST http://localhost:5004/api/v1/orders \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: ${CANCEL_IDEMPOTENCY_KEY}" \
  -d '{
    "customerId": "'${USER_ID}'",
    "items": [{"productId": "prod-cancel", "productName": "Cancel Test", "sku": "CANCEL-001", "unitPrice": 1000, "quantity": 1}],
    "shippingAddress": {"recipientName": "Cancel Test", "postalCode": "100-0001", "prefecture": "東京都", "city": "千代田区", "addressLine1": "Test", "addressLine2": null, "phoneNumber": "03-0000-0000"},
    "paymentMethod": "CREDIT_CARD"
  }')
export CANCEL_ORDER_ID=$(echo $CANCEL_ORDER | jq -r '.id')
```

```bash
# 注文キャンセル
curl -s -X POST http://localhost:5004/api/v1/orders/${CANCEL_ORDER_ID}/cancel \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"reason": "購入を取りやめたため"}'
```

**期待するレスポンス（204 No Content）:**

**検証項目:**
- [ ] ステータスコード 204 を返す
- [ ] 注文がキャンセルされる

#### テスト 2-14: POST /api/v1/orders/{orderId}/cancel - 他ユーザーの注文キャンセル（IDOR 防止）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5004/api/v1/orders/other-order-id/cancel \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"reason": "不正なキャンセル試行"}'
```

**期待するレスポンス（404 Not Found）:**

**検証項目:**
- [ ] 他ユーザーの注文はキャンセルできない

---

### Phase 3: 出荷管理（管理者専用）

> **📝 前提**: `ADMIN_TOKEN`、`ORDER_ID` を環境変数に設定済み。

#### テスト 3-1: POST /api/v1/shipments - 出荷作成

```bash
curl -s -X POST http://localhost:5004/api/v1/shipments \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "orderId": "'${ORDER_ID}'",
    "carrier": "ヤマト運輸",
    "trackingNumber": "1234-5678-9012"
  }'
```

**期待するレスポンス（201 Created）:**

```json
{
  "id": "uuid-shipment-001",
  "orderId": "uuid-order-001",
  "carrier": "ヤマト運輸",
  "trackingNumber": "1234-5678-9012",
  "status": "PREPARING",
  "shippedAt": null,
  "estimatedDeliveryAt": null,
  "deliveredAt": null,
  "createdAt": "2026-04-06T17:30:00+00:00"
}
```

**検証項目:**
- [ ] ステータスコード 201 を返す
- [ ] `status` が `PREPARING` である
- [ ] `Location` ヘッダーに新規出荷の URI が含まれる

**環境変数に保存:**
```bash
export SHIPMENT_ID="取得した出荷ID"
```

#### テスト 3-2: POST /api/v1/shipments - 一般ユーザーによる出荷作成（403 Forbidden）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5004/api/v1/shipments \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "orderId": "'${ORDER_ID}'",
    "carrier": "ヤマト運輸"
  }'
```

**期待するレスポンス（403 Forbidden）:**

**検証項目:**
- [ ] ステータスコード 403 を返す
- [ ] 一般ユーザーは出荷を作成できない

#### テスト 3-3: GET /api/v1/shipments - 出荷一覧取得

```bash
curl -s -X GET "http://localhost:5004/api/v1/shipments?status=PREPARING&page=1&pageSize=10" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "items": [...],
  "page": 1,
  "pageSize": 10,
  "totalCount": ...,
  "totalPages": ...
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] ステータスフィルタリングが機能している

#### テスト 3-4: GET /api/v1/shipments/{id} - 出荷詳細取得

```bash
curl -s -X GET http://localhost:5004/api/v1/shipments/${SHIPMENT_ID} \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す

#### テスト 3-5: GET /api/v1/shipments/order/{orderId} - 注文に紐づく出荷取得

```bash
curl -s -X GET http://localhost:5004/api/v1/shipments/order/${ORDER_ID} \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 注文 ID に紐づく出荷が返される

#### テスト 3-6: PUT /api/v1/shipments/{id}/status - 出荷ステータス更新

```bash
curl -s -X PUT http://localhost:5004/api/v1/shipments/${SHIPMENT_ID}/status \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "status": "SHIPPED",
    "trackingNumber": "1234-5678-9012"
  }'
```

**期待するレスポンス（204 No Content）:**

**検証項目:**
- [ ] ステータスコード 204 を返す
- [ ] 出荷ステータスが更新される

#### テスト 3-7: PUT /api/v1/shipments/{id}/tracking - 追跡番号更新

```bash
curl -s -X PUT http://localhost:5004/api/v1/shipments/${SHIPMENT_ID}/tracking \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "status": "IN_TRANSIT",
    "trackingNumber": "UPDATED-1234-5678"
  }'
```

**期待するレスポンス（204 No Content）:**

**検証項目:**
- [ ] ステータスコード 204 を返す
- [ ] 追跡番号が更新される

---

### Phase 4: 返品管理

> **📝 前提**: 注文が存在し、`ORDER_ID` と `ORDER_ITEM_ID` が環境変数に設定済み。注文明細 ID は注文詳細 API から取得する。

#### テスト 4-0: 注文明細 ID の取得

```bash
# 注文詳細を取得し、ORDER_ITEM_ID を環境変数に保存
ORDER_DETAIL=$(curl -s -X GET http://localhost:5004/api/v1/orders/${ORDER_ID} \
  -H "Authorization: Bearer ${ACCESS_TOKEN}")
# 実際にはより詳細なエンドポイントや DB から ORDER_ITEM_ID を取得する必要がある
export ORDER_ITEM_ID="注文明細ID"
```

#### テスト 4-1: POST /api/v1/returns - 返品申請作成

```bash
curl -s -X POST http://localhost:5004/api/v1/returns \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "orderId": "'${ORDER_ID}'",
    "orderItemId": "'${ORDER_ITEM_ID}'",
    "reason": "DEFECTIVE",
    "reasonDetail": "商品に傷があったため",
    "quantity": 1
  }'
```

**期待するレスポンス（201 Created）:**

```json
{
  "id": "uuid-return-001",
  "returnNumber": "RET-20260406-XXXXX",
  "orderId": "uuid-order-001",
  "orderItemId": "uuid-order-item-001",
  "customerId": "uuid-user-xxx",
  "reason": "DEFECTIVE",
  "reasonDetail": "商品に傷があったため",
  "quantity": 1,
  "refundAmount": 0,
  "status": "REQUESTED",
  "requestedAt": "2026-04-06T18:00:00+00:00",
  "approvedAt": null,
  "receivedAt": null,
  "refundedAt": null,
  "createdAt": "2026-04-06T18:00:00+00:00"
}
```

**検証項目:**
- [ ] ステータスコード 201 を返す
- [ ] `returnNumber` が生成されている
- [ ] `status` が `REQUESTED` である

**環境変数に保存:**
```bash
export RETURN_ID="取得した返品ID"
```

#### テスト 4-2: POST /api/v1/returns - 無効な理由コード（バリデーションエラー）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5004/api/v1/returns \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "orderId": "'${ORDER_ID}'",
    "orderItemId": "'${ORDER_ITEM_ID}'",
    "reason": "INVALID_REASON",
    "quantity": 1
  }'
```

**期待するレスポンス（400 Bad Request）:**

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] 無効な理由コードに対するエラーメッセージが返される

#### テスト 4-3: GET /api/v1/returns/{id} - 返品詳細取得（オーナー）

```bash
curl -s -X GET http://localhost:5004/api/v1/returns/${RETURN_ID} \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 自分の返品申請を取得できる

#### テスト 4-4: GET /api/v1/returns/{id} - 他ユーザーの返品取得（IDOR 防止）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X GET http://localhost:5004/api/v1/returns/other-return-id \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（404 Not Found または 403 Forbidden）:**

**検証項目:**
- [ ] 他ユーザーの返品申請は取得できない

#### テスト 4-5: GET /api/v1/returns/order/{orderId} - 注文に紐づく返品一覧取得

```bash
curl -s -X GET "http://localhost:5004/api/v1/returns/order/${ORDER_ID}?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "items": [...],
  "page": 1,
  "pageSize": 10,
  "totalCount": ...,
  "totalPages": ...
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 自分の注文に紐づく返品のみが返される

#### テスト 4-6: GET /api/v1/returns - 返品一覧取得（管理者）

```bash
curl -s -X GET "http://localhost:5004/api/v1/returns?status=REQUESTED&page=1&pageSize=10" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] ステータスフィルタリングが機能している

#### テスト 4-7: PUT /api/v1/returns/{id}/status - 返品処理（管理者による承認）

```bash
curl -s -X PUT http://localhost:5004/api/v1/returns/${RETURN_ID}/status \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "status": "APPROVED",
    "adminNotes": "返品を承認しました"
  }'
```

**期待するレスポンス（204 No Content）:**

**検証項目:**
- [ ] ステータスコード 204 を返す
- [ ] 返品ステータスが `APPROVED` に更新される

#### テスト 4-8: PUT /api/v1/returns/{id}/status - 一般ユーザーによる返品処理（403 Forbidden）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X PUT http://localhost:5004/api/v1/returns/${RETURN_ID}/status \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"status": "APPROVED"}'
```

**期待するレスポンス（403 Forbidden）:**

**検証項目:**
- [ ] ステータスコード 403 を返す
- [ ] 一般ユーザーは返品を処理できない

---

### Phase 5: レポート（管理者専用）

#### テスト 5-1: GET /api/v1/reports/sales - 売上レポート取得

```bash
curl -s -X GET "http://localhost:5004/api/v1/reports/sales?fromDate=2026-01-01T00:00:00Z&toDate=2026-04-06T23:59:59Z" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "fromDate": "2026-01-01T00:00:00+00:00",
  "toDate": "2026-04-06T23:59:59+00:00",
  "totalOrders": 5,
  "totalRevenue": 500000,
  "totalTax": 50000,
  "totalShippingFee": 0,
  "averageOrderValue": 100000,
  "dailySales": [
    {"date": "2026-04-06", "orderCount": 2, "revenue": 200000},
    ...
  ]
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 指定期間の売上データが返される
- [ ] `dailySales` に日別データが含まれる

#### テスト 5-2: GET /api/v1/reports/sales - 一般ユーザーによるアクセス（403 Forbidden）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X GET "http://localhost:5004/api/v1/reports/sales?fromDate=2026-01-01T00:00:00Z&toDate=2026-04-06T23:59:59Z" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（403 Forbidden）:**

**検証項目:**
- [ ] ステータスコード 403 を返す
- [ ] 一般ユーザーはレポートを取得できない

#### テスト 5-3: GET /api/v1/reports/sales - 無効な期間（バリデーションエラー）

```bash
# fromDate > toDate
curl -s -w "\nHTTP Status: %{http_code}" \
  -X GET "http://localhost:5004/api/v1/reports/sales?fromDate=2026-04-06T00:00:00Z&toDate=2026-01-01T00:00:00Z" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（400 Bad Request）:**

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] エラーメッセージに期間の不正が示される

#### テスト 5-4: GET /api/v1/reports/sales - 366日を超える期間（バリデーションエラー）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X GET "http://localhost:5004/api/v1/reports/sales?fromDate=2024-01-01T00:00:00Z&toDate=2026-04-06T00:00:00Z" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（400 Bad Request）:**

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] 366日を超える期間は拒否される

#### テスト 5-5: GET /api/v1/reports/sales - 未来日指定（バリデーションエラー）

```bash
# toDate に未来日を指定
curl -s -w "\nHTTP Status: %{http_code}" \
  -X GET "http://localhost:5004/api/v1/reports/sales?fromDate=2026-04-01T00:00:00Z&toDate=2027-01-01T00:00:00Z" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（400 Bad Request）:**

```json
{
  "type": "...",
  "title": "...",
  "status": 400,
  "detail": "toDate に未来日は指定できません"
}
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] 未来日が拒否される

---

## 5. verification-report.md のフォーマット

検証結果は以下のフォーマットで `verification-report.md` に記録する。

```markdown
# SalesManagementService 検証レポート

## 検証概要

| 項目 | 値 |
|------|-----|
| 検証日時 | 2026-04-06 17:00 JST |
| 検証者 | GitHub Copilot |
| サービスバージョン | 1.0.0 |
| 総エンドポイント数 | 21（ヘルスチェック含む） |
| テスト実施数 | XX |
| 成功 | XX |
| 失敗 | XX |
| スキップ | XX |

## 検証結果サマリー

| Phase | 対象 | 結果 | 備考 |
|-------|------|------|------|
| 0 | ヘルスチェック | ✅ PASS | |
| 1 | 認証・認可 | ✅ PASS | |
| 2 | 注文管理 | ✅ PASS | |
| 3 | 出荷管理 | ✅ PASS | |
| 4 | 返品管理 | ✅ PASS | |
| 5 | レポート | ✅ PASS | |

## 詳細結果

### Phase 0: ヘルスチェック

#### テスト 0-1: GET /health

**リクエスト:**
```bash
curl -s http://localhost:5004/health
```

**レスポンス:**
```
Healthy
```

**結果:** ✅ PASS

---
（以下、各テストの結果を記録）
```

---

## 6. テスト用データ

### 6.1 有効な注文ステータス値

| ステータス | 説明 |
|-----------|------|
| `PENDING` | 保留中（初期状態） |
| `CONFIRMED` | 確認済み |
| `PROCESSING` | 処理中 |
| `SHIPPED` | 出荷済み |
| `DELIVERED` | 配達完了 |
| `RETURNED` | 返品済み |
| `REFUNDED` | 返金済み |
| `CANCELLED` | キャンセル |
| `INVENTORY_SHORTAGE` | 在庫不足 |
| `PAYMENT_FAILED` | 決済失敗 |
| `PENDING_PAYMENT` | 決済待ち |

### 6.2 有効な決済ステータス値

| ステータス | 説明 |
|-----------|------|
| `PENDING` | 保留中 |
| `AUTHORIZED` | オーソリ済み |
| `CAPTURED` | キャプチャ済み |
| `FAILED` | 失敗 |
| `REFUNDED` | 返金済み |
| `PARTIALLY_REFUNDED` | 一部返金済み |

### 6.3 有効な出荷ステータス値

| ステータス | 説明 |
|-----------|------|
| `PREPARING` | 準備中 |
| `SHIPPED` | 出荷済み |
| `IN_TRANSIT` | 配送中 |
| `DELIVERED` | 配達完了 |
| `FAILED` | 配送失敗 |

### 6.4 有効な返品理由コード

| 理由コード | 説明 |
|-----------|------|
| `DEFECTIVE` | 不良品 |
| `WRONG_ITEM` | 誤配送 |
| `SIZE_MISMATCH` | サイズ不一致 |
| `NOT_AS_DESCRIBED` | 説明と異なる |
| `CHANGED_MIND` | 気が変わった |
| `OTHER` | その他 |

### 6.5 有効な返品ステータス値

| ステータス | 説明 |
|-----------|------|
| `REQUESTED` | 申請中 |
| `APPROVED` | 承認済み |
| `REJECTED` | 却下 |
| `RECEIVED` | 商品受領済み |
| `REFUNDED` | 返金済み |
| `CLOSED` | クローズ |

---

## 7. トラブルシューティング

### 7.1 よくある問題と解決策

| 問題 | 原因 | 解決策 |
|------|------|--------|
| 401 Unauthorized | トークンが無効または期限切れ | AuthService で再ログインしてトークンを取得 |
| 403 Forbidden | 権限不足 | 管理者トークンを使用するか、自分のリソースにアクセス |
| 404 Not Found | リソースが存在しない | ID の確認、または先に必要なリソースを作成 |
| 500 Internal Server Error | サーバー側エラー | ログを確認: `docker logs skishop-sales-management-service` |
| 接続拒否 | サービス未起動 | `docker compose up -d sales-management-service` |
| Idempotency-Key エラー | ヘッダー未設定 | `Idempotency-Key` ヘッダーを追加 |

### 7.2 ログの確認

```bash
# SalesManagementService のログを確認
docker logs -f skishop-sales-management-service --tail 100

# PostgreSQL の直接確認
docker exec -it skishop-postgres psql -U skishop -d salesdb
```

### 7.3 データベースの確認

```bash
# 注文テーブルの確認
docker exec -it skishop-postgres psql -U skishop -d salesdb \
  -c "SELECT id, order_number, customer_id, status, total_amount FROM orders LIMIT 10;"

# 出荷テーブルの確認
docker exec -it skishop-postgres psql -U skishop -d salesdb \
  -c "SELECT id, order_id, carrier, status FROM shipments LIMIT 10;"

# 返品テーブルの確認
docker exec -it skishop-postgres psql -U skishop -d salesdb \
  -c "SELECT id, return_number, order_id, status FROM returns LIMIT 10;"
```

---

## 8. 実行手順チェックリスト

### 検証開始前

- [ ] Docker Compose で必要なサービスが起動している
- [ ] SalesManagementService のヘルスチェックが `Healthy` を返す
- [ ] AuthService のヘルスチェックが `Healthy` を返す
- [ ] 一般ユーザーの JWT トークンを取得し `ACCESS_TOKEN` に設定
- [ ] 一般ユーザー ID を `USER_ID` に設定
- [ ] 管理者の JWT トークンを取得し `ADMIN_TOKEN` に設定
- [ ] 管理者ユーザー ID を `ADMIN_USER_ID` に設定

### Phase 0: ヘルスチェック

- [ ] テスト 0-1: GET /health 完了
- [ ] テスト 0-2: GET /health/ready 完了

### Phase 1: 認証・認可の基本確認

- [ ] テスト 1-1: 認証なしアクセス（401）完了
- [ ] テスト 1-2: 一般ユーザーによる管理者エンドポイントアクセス（403）完了

### Phase 2: 注文管理

- [ ] テスト 2-1: 注文作成（正常系）完了
- [ ] テスト 2-2: Idempotency-Key なし（400）完了
- [ ] テスト 2-3: 冪等性確認完了
- [ ] テスト 2-4: バリデーションエラー（400）完了
- [ ] テスト 2-5: 注文詳細取得（オーナー）完了
- [ ] テスト 2-6: 他ユーザーの注文取得（IDOR 防止）完了
- [ ] テスト 2-7: 注文番号で取得完了
- [ ] テスト 2-8: 顧客の注文一覧取得完了
- [ ] テスト 2-9: 他ユーザーの注文一覧取得（IDOR 防止）完了
- [ ] テスト 2-10: 注文検索（管理者）完了
- [ ] テスト 2-11: ステータス更新（管理者）完了
- [ ] テスト 2-12: 一般ユーザーによるステータス更新（403）完了
- [ ] テスト 2-13: 注文キャンセル完了
- [ ] テスト 2-14: 他ユーザーの注文キャンセル（IDOR 防止）完了

### Phase 3: 出荷管理

- [ ] テスト 3-1: 出荷作成完了
- [ ] テスト 3-2: 一般ユーザーによる出荷作成（403）完了
- [ ] テスト 3-3: 出荷一覧取得完了
- [ ] テスト 3-4: 出荷詳細取得完了
- [ ] テスト 3-5: 注文に紐づく出荷取得完了
- [ ] テスト 3-6: 出荷ステータス更新完了
- [ ] テスト 3-7: 追跡番号更新完了

### Phase 4: 返品管理

- [ ] テスト 4-0: 注文明細 ID の取得完了
- [ ] テスト 4-1: 返品申請作成完了
- [ ] テスト 4-2: 無効な理由コード（バリデーションエラー）完了
- [ ] テスト 4-3: 返品詳細取得（オーナー）完了
- [ ] テスト 4-4: 他ユーザーの返品取得（IDOR 防止）完了
- [ ] テスト 4-5: 注文に紐づく返品一覧取得完了
- [ ] テスト 4-6: 返品一覧取得（管理者）完了
- [ ] テスト 4-7: 返品処理（管理者による承認）完了
- [ ] テスト 4-8: 一般ユーザーによる返品処理（403）完了

### Phase 5: レポート

- [ ] テスト 5-1: 売上レポート取得完了
- [ ] テスト 5-2: 一般ユーザーによるアクセス（403）完了
- [ ] テスト 5-3: 無効な期間（バリデーションエラー）完了
- [ ] テスト 5-4: 366日を超える期間（バリデーションエラー）完了
- [ ] テスト 5-5: 未来日指定（バリデーションエラー）完了

### 検証完了後

- [ ] 全テスト結果を `verification-report.md` に記録
- [ ] テスト用データのクリーンアップ（必要に応じて）
- [ ] 発見された問題の Issue 登録（問題があれば）

---

## 補足: エンドポイント数の内訳

| カテゴリ | エンドポイント数 |
|---------|----------------|
| ヘルスチェック | 2 |
| 注文管理（Orders） | 7 |
| 出荷管理（Shipments） | 6 |
| 返品管理（Returns） | 5 |
| レポート（Reports） | 1 |
| **合計** | **21** |
