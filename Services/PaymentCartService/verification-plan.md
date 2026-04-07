# PaymentCartService 検証計画書（verification-plan.md）

本ドキュメントは PaymentCartService の全エンドポイントに対する動作検証の手順とフォーマットを定義する。
検証結果は `verification-report.md` に記録し、PaymentCartService の使い方を理解するためのリファレンスとして活用する。

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

1. **機能確認**: 全 13 REST エンドポイント + 4 gRPC メソッド（+ 2 ヘルスチェック）が設計通りに動作することを確認する
2. **使い方の文書化**: 各 API の呼び出し方法・パラメータ・レスポンスを実例で示す
3. **異常系の確認**: バリデーションエラー、認証エラー、認可エラー（IDOR 防止）等の動作を確認する
4. **セキュリティ検証**: IDOR 保護、管理者専用エンドポイントのアクセス制御を確認する
5. **Cookie 管理確認**: CartId / SessionToken の Cookie ベースセッション管理が正しく動作することを確認する
6. **gRPC サービス検証**: マイクロサービス間通信用の gRPC インターフェースが正しく動作することを確認する

---

## 2. 前提条件

### 2.1 環境構成

| 項目 | 値 |
|------|-----|
| PaymentCartService URL | `http://localhost:5005` |
| AuthService URL | `http://localhost:5001` |
| PostgreSQL | `localhost:5432`（DB: `paymentdb`） |
| Redis | `localhost:6379` |
| Kafka | `localhost:9092` |
| Stripe | テスト環境（WebhookSecret 設定済み） |

### 2.2 起動コマンド

```bash
# Docker Compose で PaymentCartService と依存サービスを起動
cd /Users/yoterada/GitHub/DotNet-Skishop-App
docker compose up -d auth-service payment-cart-service
```

> **📝 注意**: PaymentCartService は AuthService が発行した JWT トークンを使用して認証するため、AuthService も同時に起動する必要がある。

### 2.3 事前確認コマンド

```bash
# PaymentCartService ヘルスチェック
curl -s http://localhost:5005/health
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
    "email": "paymentuser@example.com",
    "password": "Password123!",
    "firstName": "Payment",
    "lastName": "User",
    "username": "paymentuser"
  }'
```

```bash
# 2. ユーザーステータスを ACTIVE に変更（DB 直接操作）
docker exec -it skishop-postgres psql -U skishop -d authdb \
  -c "UPDATE users SET status = 'ACTIVE', email_verified = true WHERE email = 'paymentuser@example.com';"
```

```bash
# 3. ログインしてトークン取得
curl -s -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "paymentuser@example.com",
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
    "email": "paymentadmin@example.com",
    "password": "AdminPass123!",
    "firstName": "Payment",
    "lastName": "Admin",
    "username": "paymentadmin"
  }'
```

```bash
# 2. 管理者ステータスとロール設定
docker exec -it skishop-postgres psql -U skishop -d authdb \
  -c "UPDATE users SET status = 'ACTIVE', email_verified = true, role = 'Admin' WHERE email = 'paymentadmin@example.com';"
```

```bash
# 3. 管理者ログイン
curl -s -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "paymentadmin@example.com",
    "password": "AdminPass123!"
  }'

# レスポンスの accessToken を環境変数に保存
export ADMIN_TOKEN="eyJhbGci..."
export ADMIN_USER_ID="取得した管理者ユーザーID"
```

### 2.5 Cookie 管理について

PaymentCartService はカート管理に Cookie を使用する：

| Cookie 名 | 用途 | 有効期限 |
|-----------|------|---------|
| `CartId` | カート識別子 | 7日間 |
| `SessionToken` | セッション識別子 | 30日間 |

curl での Cookie 管理方法：

```bash
# Cookie を保存
curl -c cookies.txt ...

# 保存した Cookie を送信
curl -b cookies.txt ...

# Cookie の保存と送信を同時に行う
curl -b cookies.txt -c cookies.txt ...
```

---

## 3. エンドポイント一覧

### 3.1 ヘルスチェック

| メソッド | パス | 説明 | 認証 |
|---------|------|------|------|
| GET | `/health` | Liveness チェック | 不要 |
| GET | `/health/ready` | Readiness チェック（PostgreSQL, Redis, Kafka 疎通） | 不要 |

### 3.2 カート管理（CartEndpoints）

| メソッド | パス | 説明 | 認証 | 備考 |
|---------|------|------|------|------|
| GET | `/api/v1/cart` | カート取得・作成 | **不要** | Cookie ベース |
| POST | `/api/v1/cart/items` | カートにアイテム追加 | **不要** | Cookie ベース |
| PUT | `/api/v1/cart/items/{itemId}` | アイテム数量更新 | **不要** | Cookie ベース |
| DELETE | `/api/v1/cart/items/{itemId}` | アイテム削除 | **不要** | Cookie ベース |
| DELETE | `/api/v1/cart` | カートクリア | **不要** | Cookie ベース |
| POST | `/api/v1/cart/merge` | カートマージ | **必要** | ログイン後にゲストカートをユーザーカートにマージ |

### 3.3 決済管理（PaymentEndpoints）

| メソッド | パス | 説明 | 認証 | 備考 |
|---------|------|------|------|------|
| POST | `/api/v1/payments/checkout` | チェックアウト（決済開始） | **必要** | Stripe Checkout セッション作成 |
| GET | `/api/v1/payments/{paymentId}` | 決済情報取得 | **必要** | 自分の決済のみ / Admin は全件 |
| GET | `/api/v1/payments/order/{orderId}` | 注文 ID で決済検索 | **必要** | 自分の決済のみ / Admin は全件 |
| GET | `/api/v1/payments/customer/{customerId}` | 顧客の決済履歴 | **必要** | ページネーション対応 |
| POST | `/api/v1/payments/{paymentId}/refund` | 返金処理 | **Admin のみ** | 管理者専用 |
| POST | `/api/v1/payments/webhook` | Stripe Webhook | **不要** | Stripe 署名検証必須 |

### 3.4 ゲストチェックアウト（GuestCheckoutEndpoints）

| メソッド | パス | 説明 | 認証 | 備考 |
|---------|------|------|------|------|
| POST | `/api/v1/checkout/guest` | ゲストチェックアウト | **不要** | Cookie または cartId パラメータ必須 |

### 3.5 gRPC サービス（内部通信用）

> **📝 注意**: gRPC サービスはマイクロサービス間の内部通信用であり、認証が必須（`RequireAuthorization()`）。
> テストには `grpcurl` ツールを使用する。

#### CartGrpcService

| メソッド | 説明 | 認証 | 備考 |
|---------|------|------|------|
| `GetCartSnapshot` | カートのスナップショット取得 | **必要** | 注文確定時に SalesManagementService から呼び出される |
| `ClearCart` | カートクリア | **必要** | 注文確定後にカートを空にする |

#### PaymentGrpcService

| メソッド | 説明 | 認証 | 備考 |
|---------|------|------|------|
| `ProcessPayment` | 決済処理（Saga 用） | **必要** | SalesManagementService から Saga オーケストレーションで呼び出される |
| `RefundPayment` | 返金処理（補償トランザクション用） | **必要** | Saga の補償トランザクションで使用 |

---

## 4. 検証手順

検証は以下の順序で実施する。依存関係を考慮した順番となっている。

---

### Phase 0: ヘルスチェック

#### テスト 0-1: GET /health（Liveness）

```bash
curl -s http://localhost:5005/health
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
curl -s http://localhost:5005/health/ready
```

**期待するレスポンス（200 OK）:**

```
Healthy
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] PostgreSQL、Redis、Kafka の疎通が確認できている

---

### Phase 1: カート管理（ゲストユーザー）

> **📝 前提**: Cookie ファイルを初期化して開始する

```bash
# Cookie ファイルを削除してクリーン状態から開始
rm -f cookies.txt
```

#### テスト 1-1: GET /api/v1/cart - 新規カート作成

```bash
curl -s -b cookies.txt -c cookies.txt \
  http://localhost:5005/api/v1/cart
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-xxx",
  "customerId": null,
  "sessionId": "session-uuid",
  "status": "Active",
  "items": [],
  "totalItems": 0,
  "totalAmount": 0.00,
  "expiresAt": "2026-04-13T00:00:00Z",
  "createdAt": "2026-04-06T12:00:00Z",
  "updatedAt": "2026-04-06T12:00:00Z"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 新規カートが作成される
- [ ] `Set-Cookie: CartId=...` ヘッダーが返る
- [ ] `Set-Cookie: SessionToken=...` ヘッダーが返る
- [ ] Cookie に `HttpOnly`, `Secure`, `SameSite=Strict` が設定される

```bash
# Cookie が設定されたか確認
cat cookies.txt
```

#### テスト 1-2: POST /api/v1/cart/items - アイテム追加

```bash
curl -s -b cookies.txt -c cookies.txt \
  -X POST http://localhost:5005/api/v1/cart/items \
  -H "Content-Type: application/json" \
  -d '{
    "productId": "PROD-001",
    "productName": "スキーブーツ プロモデル",
    "sku": "SKI-BOOT-PRO-001",
    "unitPrice": 45000.00,
    "quantity": 1
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "cart-uuid",
  "customerId": null,
  "sessionId": "session-uuid",
  "status": "Active",
  "items": [
    {
      "id": "item-uuid",
      "productId": "PROD-001",
      "productName": "スキーブーツ プロモデル",
      "sku": "SKI-BOOT-PRO-001",
      "unitPrice": 45000.00,
      "quantity": 1,
      "subtotal": 45000.00,
      "createdAt": "2026-04-06T12:00:00Z"
    }
  ],
  "totalItems": 1,
  "totalAmount": 45000.00,
  "expiresAt": "2026-04-13T00:00:00Z",
  "createdAt": "2026-04-06T12:00:00Z",
  "updatedAt": "2026-04-06T12:00:00Z"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] アイテムがカートに追加される
- [ ] `totalItems` と `totalAmount` が正しく計算される
- [ ] アイテム ID が返される（環境変数に保存）

```bash
export CART_ITEM_ID="取得したアイテムID"
```

#### テスト 1-3: POST /api/v1/cart/items - 2つ目のアイテム追加

```bash
curl -s -b cookies.txt -c cookies.txt \
  -X POST http://localhost:5005/api/v1/cart/items \
  -H "Content-Type: application/json" \
  -d '{
    "productId": "PROD-002",
    "productName": "スキーポール カーボン",
    "sku": "SKI-POLE-CARBON-002",
    "unitPrice": 15000.00,
    "quantity": 2
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "cart-uuid",
  "items": [
    { "productId": "PROD-001", "quantity": 1, "subtotal": 45000.00 },
    { "productId": "PROD-002", "quantity": 2, "subtotal": 30000.00 }
  ],
  "totalItems": 3,
  "totalAmount": 75000.00
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 2つ目のアイテムがカートに追加される
- [ ] `totalItems` が 3 (1 + 2) になる
- [ ] `totalAmount` が 75000 (45000 + 30000) になる

#### テスト 1-4: POST /api/v1/cart/items - 同じ商品を追加（数量加算）

```bash
curl -s -b cookies.txt -c cookies.txt \
  -X POST http://localhost:5005/api/v1/cart/items \
  -H "Content-Type: application/json" \
  -d '{
    "productId": "PROD-001",
    "productName": "スキーブーツ プロモデル",
    "sku": "SKI-BOOT-PRO-001",
    "unitPrice": 45000.00,
    "quantity": 1
  }'
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 既存アイテムの数量が加算される（1 → 2）または
- [ ] 新しいアイテムとして追加される（実装による）

#### テスト 1-5: POST /api/v1/cart/items - バリデーションエラー（数量超過）

```bash
curl -s -b cookies.txt -c cookies.txt \
  -X POST http://localhost:5005/api/v1/cart/items \
  -H "Content-Type: application/json" \
  -d '{
    "productId": "PROD-003",
    "productName": "Test Product",
    "sku": "TEST-001",
    "unitPrice": 1000.00,
    "quantity": 11
  }'
```

**期待するレスポンス（400 Bad Request）:**

```json
{
  "type": "...",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Quantity": ["数量は 1〜10 の範囲で指定してください"]
  }
}
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] バリデーションエラーメッセージが含まれる

#### テスト 1-6: POST /api/v1/cart/items - バリデーションエラー（必須項目不足）

```bash
curl -s -b cookies.txt -c cookies.txt \
  -X POST http://localhost:5005/api/v1/cart/items \
  -H "Content-Type: application/json" \
  -d '{
    "productId": "",
    "productName": "",
    "sku": "",
    "unitPrice": 0,
    "quantity": 0
  }'
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] 全ての必須フィールドのエラーが一括で返る

#### テスト 1-7: PUT /api/v1/cart/items/{itemId} - 数量更新

```bash
curl -s -b cookies.txt -c cookies.txt \
  -X PUT http://localhost:5005/api/v1/cart/items/${CART_ITEM_ID} \
  -H "Content-Type: application/json" \
  -d '{
    "quantity": 3
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "items": [
    { "id": "item-uuid", "quantity": 3, "subtotal": 135000.00 }
  ],
  "totalItems": 5,
  "totalAmount": 165000.00
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] アイテムの数量が 3 に更新される
- [ ] `subtotal` が正しく再計算される

#### テスト 1-8: PUT /api/v1/cart/items/{itemId} - 存在しないアイテム

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -b cookies.txt -c cookies.txt \
  -X PUT http://localhost:5005/api/v1/cart/items/non-existent-id \
  -H "Content-Type: application/json" \
  -d '{ "quantity": 1 }'
```

**検証項目:**
- [ ] ステータスコード 404 を返す

#### テスト 1-9: DELETE /api/v1/cart/items/{itemId} - アイテム削除

```bash
curl -s -b cookies.txt -c cookies.txt \
  -X DELETE http://localhost:5005/api/v1/cart/items/${CART_ITEM_ID}
```

**期待するレスポンス（200 OK）:**

```json
{
  "items": [
    { "productId": "PROD-002", "quantity": 2, "subtotal": 30000.00 }
  ],
  "totalItems": 2,
  "totalAmount": 30000.00
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 指定したアイテムが削除される
- [ ] `totalItems` と `totalAmount` が再計算される

#### テスト 1-10: DELETE /api/v1/cart/items/{itemId} - Cookie なしでアクセス

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X DELETE http://localhost:5005/api/v1/cart/items/some-item-id
```

**検証項目:**
- [ ] ステータスコード 404 を返す
- [ ] "カートが見つかりません" エラー

#### テスト 1-11: GET /api/v1/cart - 再度カート取得

```bash
curl -s -b cookies.txt -c cookies.txt \
  http://localhost:5005/api/v1/cart
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 同じカート（Cookie のカート ID）が返る
- [ ] アイテムが保持されている

#### テスト 1-12: DELETE /api/v1/cart - カートクリア

```bash
curl -s -b cookies.txt -c cookies.txt \
  -X DELETE http://localhost:5005/api/v1/cart
```

**期待するレスポンス（204 No Content）:**

**検証項目:**
- [ ] ステータスコード 204 を返す
- [ ] レスポンスボディがない

#### テスト 1-13: GET /api/v1/cart - クリア後のカート確認

```bash
curl -s -b cookies.txt -c cookies.txt \
  http://localhost:5005/api/v1/cart
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] カートは存在するが `items` が空配列になっている、または
- [ ] 新しいカートが作成される

---

### Phase 2: カートマージ（認証ユーザー）

> **📝 前提**: ゲストカートにアイテムを追加済み、ユーザートークン取得済み

#### テスト 2-0: 事前準備 - ゲストカートにアイテム追加

```bash
# 新しい Cookie ファイルを使用
rm -f guest_cookies.txt

# ゲストカート作成＆アイテム追加
curl -s -b guest_cookies.txt -c guest_cookies.txt \
  -X POST http://localhost:5005/api/v1/cart/items \
  -H "Content-Type: application/json" \
  -d '{
    "productId": "PROD-GUEST-001",
    "productName": "ゲストカート商品",
    "sku": "GUEST-SKU-001",
    "unitPrice": 10000.00,
    "quantity": 1
  }'

# ゲストカート ID を取得
export GUEST_CART_ID=$(curl -s -b guest_cookies.txt http://localhost:5005/api/v1/cart | jq -r '.id')
echo "Guest Cart ID: ${GUEST_CART_ID}"
```

#### テスト 2-1: POST /api/v1/cart/merge - 認証なし

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5005/api/v1/cart/merge \
  -H "Content-Type: application/json" \
  -d "{\"guestCartId\": \"${GUEST_CART_ID}\"}"
```

**検証項目:**
- [ ] ステータスコード 401 を返す

#### テスト 2-2: POST /api/v1/cart/merge - 正常なマージ

```bash
curl -s -X POST http://localhost:5005/api/v1/cart/merge \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d "{\"guestCartId\": \"${GUEST_CART_ID}\"}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "new-or-merged-cart-id",
  "customerId": "user-id",
  "items": [
    { "productId": "PROD-GUEST-001", "quantity": 1 }
  ],
  "totalItems": 1,
  "totalAmount": 10000.00
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] ゲストカートのアイテムがユーザーカートにマージされる
- [ ] `customerId` がログインユーザーの ID になる

#### テスト 2-3: POST /api/v1/cart/merge - バリデーションエラー

```bash
curl -s -X POST http://localhost:5005/api/v1/cart/merge \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"guestCartId": ""}'
```

**検証項目:**
- [ ] ステータスコード 400 を返す

#### テスト 2-4: POST /api/v1/cart/merge - 存在しないゲストカート

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5005/api/v1/cart/merge \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"guestCartId": "non-existent-cart-id"}'
```

**検証項目:**
- [ ] ステータスコード 404 を返す

---

### Phase 3: 決済（認証ユーザー）

> **📝 前提**: ユーザートークン取得済み、カートにアイテムが存在する

#### テスト 3-0: 事前準備 - チェックアウト用カート作成

```bash
# ユーザーカートにアイテム追加
rm -f user_cookies.txt

curl -s -b user_cookies.txt -c user_cookies.txt \
  -X POST http://localhost:5005/api/v1/cart/items \
  -H "Content-Type: application/json" \
  -d '{
    "productId": "PROD-CHECKOUT-001",
    "productName": "チェックアウトテスト商品",
    "sku": "CHECKOUT-SKU-001",
    "unitPrice": 25000.00,
    "quantity": 2
  }'

# カート ID を取得
export CHECKOUT_CART_ID=$(curl -s -b user_cookies.txt http://localhost:5005/api/v1/cart | jq -r '.id')
echo "Checkout Cart ID: ${CHECKOUT_CART_ID}"
```

#### テスト 3-1: POST /api/v1/payments/checkout - 認証なし

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5005/api/v1/payments/checkout \
  -H "Content-Type: application/json" \
  -d "{
    \"cartId\": \"${CHECKOUT_CART_ID}\",
    \"paymentMethod\": \"card\",
    \"shippingAddress\": {
      \"recipientName\": \"山田太郎\",
      \"postalCode\": \"100-0001\",
      \"prefecture\": \"東京都\",
      \"city\": \"千代田区\",
      \"addressLine1\": \"丸の内1-1-1\",
      \"phoneNumber\": \"03-1234-5678\"
    }
  }"
```

**検証項目:**
- [ ] ステータスコード 401 を返す

#### テスト 3-2: POST /api/v1/payments/checkout - 正常なチェックアウト

```bash
curl -s -X POST http://localhost:5005/api/v1/payments/checkout \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d "{
    \"cartId\": \"${CHECKOUT_CART_ID}\",
    \"paymentMethod\": \"card\",
    \"shippingAddress\": {
      \"recipientName\": \"山田太郎\",
      \"postalCode\": \"100-0001\",
      \"prefecture\": \"東京都\",
      \"city\": \"千代田区\",
      \"addressLine1\": \"丸の内1-1-1\",
      \"addressLine2\": \"〇〇ビル 5F\",
      \"phoneNumber\": \"03-1234-5678\"
    },
    \"couponCode\": null,
    \"usedPoints\": 0
  }"
```

**期待するレスポンス（201 Created）:**

```json
{
  "id": "payment-uuid",
  "orderId": "order-uuid",
  "customerId": "user-id",
  "amount": 50000.00,
  "currencyCode": "JPY",
  "status": "PENDING",
  "paymentMethod": "card",
  "checkoutUrl": "https://checkout.stripe.com/...",
  "createdAt": "2026-04-06T12:00:00Z",
  "updatedAt": "2026-04-06T12:00:00Z"
}
```

**検証項目:**
- [ ] ステータスコード 201 を返す
- [ ] `Location` ヘッダーに決済リソース URL が含まれる
- [ ] `checkoutUrl` が Stripe Checkout URL である
- [ ] `status` が `PENDING` である
- [ ] 決済 ID を環境変数に保存

```bash
export PAYMENT_ID="取得した決済ID"
export ORDER_ID="取得した注文ID"
```

#### テスト 3-3: POST /api/v1/payments/checkout - バリデーションエラー（必須項目不足）

```bash
curl -s -X POST http://localhost:5005/api/v1/payments/checkout \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "cartId": "",
    "paymentMethod": ""
  }'
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] 必須フィールドのエラーが返る

#### テスト 3-4: POST /api/v1/payments/checkout - 存在しないカート

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5005/api/v1/payments/checkout \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "cartId": "non-existent-cart-id",
    "paymentMethod": "card"
  }'
```

**検証項目:**
- [ ] ステータスコード 404 を返す

#### テスト 3-5: GET /api/v1/payments/{paymentId} - 決済情報取得

```bash
curl -s -X GET http://localhost:5005/api/v1/payments/${PAYMENT_ID} \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "payment-uuid",
  "orderId": "order-uuid",
  "customerId": "user-id",
  "amount": 50000.00,
  "currencyCode": "JPY",
  "status": "PENDING",
  "paymentMethod": "card",
  "stripeCheckoutSessionId": "cs_xxx",
  "stripePaymentIntentId": null,
  "paidAt": null,
  "createdAt": "2026-04-06T12:00:00Z",
  "updatedAt": "2026-04-06T12:00:00Z"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 自分の決済情報が取得できる

#### テスト 3-6: GET /api/v1/payments/{paymentId} - 認証なし

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  http://localhost:5005/api/v1/payments/${PAYMENT_ID}
```

**検証項目:**
- [ ] ステータスコード 401 を返す

#### テスト 3-7: GET /api/v1/payments/{paymentId} - 他ユーザーの決済（IDOR 防止）

```bash
# 別ユーザーのトークンで他人の決済にアクセス
# ※ 事前に別ユーザーを作成しトークンを取得
curl -s -w "\nHTTP Status: %{http_code}" \
  -X GET http://localhost:5005/api/v1/payments/${PAYMENT_ID} \
  -H "Authorization: Bearer ${OTHER_USER_TOKEN}"
```

**検証項目:**
- [ ] ステータスコード 403 または 404 を返す
- [ ] 他ユーザーの決済にはアクセスできない

#### テスト 3-8: GET /api/v1/payments/{paymentId} - 存在しない決済

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X GET http://localhost:5005/api/v1/payments/non-existent-id \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**検証項目:**
- [ ] ステータスコード 404 を返す

#### テスト 3-9: GET /api/v1/payments/order/{orderId} - 注文 ID で検索

```bash
curl -s -X GET http://localhost:5005/api/v1/payments/order/${ORDER_ID} \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 注文 ID に紐づく決済が返る

#### テスト 3-10: GET /api/v1/payments/customer/{customerId} - 顧客決済履歴

```bash
curl -s -X GET "http://localhost:5005/api/v1/payments/customer/${USER_ID}?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "items": [
    {
      "id": "payment-uuid",
      "orderId": "order-uuid",
      "amount": 50000.00,
      "status": "PENDING"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] ページネーション情報が含まれる

---

### Phase 4: 返金（管理者のみ）

> **📝 前提**: 管理者トークン取得済み、完了済みの決済が存在する

#### テスト 4-1: POST /api/v1/payments/{paymentId}/refund - 一般ユーザー

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5005/api/v1/payments/${PAYMENT_ID}/refund \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 10000.00,
    "reason": "お客様のご要望による返品"
  }'
```

**検証項目:**
- [ ] ステータスコード 403 を返す
- [ ] 一般ユーザーは返金処理できない

#### テスト 4-2: POST /api/v1/payments/{paymentId}/refund - 認証なし

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5005/api/v1/payments/${PAYMENT_ID}/refund \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 10000.00,
    "reason": "返品"
  }'
```

**検証項目:**
- [ ] ステータスコード 401 を返す

#### テスト 4-3: POST /api/v1/payments/{paymentId}/refund - 管理者による返金

```bash
curl -s -X POST http://localhost:5005/api/v1/payments/${PAYMENT_ID}/refund \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 10000.00,
    "reason": "お客様のご要望による部分返品"
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "refund-uuid",
  "paymentId": "payment-uuid",
  "refundAmount": 10000.00,
  "status": "REFUNDED",
  "createdAt": "2026-04-06T12:00:00Z"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 返金情報が返る
- [ ] 決済ステータスが REFUNDED に更新される

> **📝 注意**: この テストは Stripe 決済が完了している必要があります。PENDING 状態の決済では返金できない可能性があります。

#### テスト 4-4: POST /api/v1/payments/{paymentId}/refund - バリデーションエラー

```bash
curl -s -X POST http://localhost:5005/api/v1/payments/${PAYMENT_ID}/refund \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 0,
    "reason": ""
  }'
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] バリデーションエラーが返る

---

### Phase 5: ゲストチェックアウト

> **📝 前提**: Cookie ベースのゲストカートにアイテムが存在する

#### テスト 5-0: 事前準備 - ゲストカート作成

```bash
rm -f guest_checkout_cookies.txt

curl -s -b guest_checkout_cookies.txt -c guest_checkout_cookies.txt \
  -X POST http://localhost:5005/api/v1/cart/items \
  -H "Content-Type: application/json" \
  -d '{
    "productId": "PROD-GUEST-CHECKOUT",
    "productName": "ゲストチェックアウト商品",
    "sku": "GUEST-CHECKOUT-SKU",
    "unitPrice": 8000.00,
    "quantity": 1
  }'

export GUEST_CHECKOUT_CART_ID=$(curl -s -b guest_checkout_cookies.txt http://localhost:5005/api/v1/cart | jq -r '.id')
echo "Guest Checkout Cart ID: ${GUEST_CHECKOUT_CART_ID}"
```

#### テスト 5-1: POST /api/v1/checkout/guest - Cookie ベースのチェックアウト

```bash
curl -s -b guest_checkout_cookies.txt -c guest_checkout_cookies.txt \
  -X POST http://localhost:5005/api/v1/checkout/guest \
  -H "Content-Type: application/json" \
  -d '{
    "paymentMethod": "card",
    "shippingAddress": {
      "recipientName": "ゲストユーザー",
      "postalCode": "150-0001",
      "prefecture": "東京都",
      "city": "渋谷区",
      "addressLine1": "渋谷1-1-1",
      "phoneNumber": "03-9876-5432"
    },
    "email": "guest@example.com"
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "payment-uuid",
  "orderId": "order-uuid",
  "customerId": "guest-session-id",
  "amount": 8000.00,
  "currencyCode": "JPY",
  "status": "PENDING",
  "paymentMethod": "card",
  "checkoutUrl": "https://checkout.stripe.com/...",
  "createdAt": "2026-04-06T12:00:00Z",
  "updatedAt": "2026-04-06T12:00:00Z"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] Cookie からカート ID が取得される
- [ ] `checkoutUrl` が Stripe Checkout URL である

#### テスト 5-2: POST /api/v1/checkout/guest - cartId パラメータ指定

```bash
# Cookie なしで cartId を直接指定
curl -s -X POST http://localhost:5005/api/v1/checkout/guest \
  -H "Content-Type: application/json" \
  -d "{
    \"cartId\": \"${GUEST_CHECKOUT_CART_ID}\",
    \"paymentMethod\": \"card\",
    \"shippingAddress\": {
      \"recipientName\": \"パラメータ指定ユーザー\",
      \"postalCode\": \"150-0002\",
      \"prefecture\": \"東京都\",
      \"city\": \"渋谷区\",
      \"addressLine1\": \"渋谷2-2-2\",
      \"phoneNumber\": \"03-1111-2222\"
    },
    \"email\": \"param@example.com\"
  }"
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] リクエストボディの cartId が使用される

#### テスト 5-3: POST /api/v1/checkout/guest - バリデーションエラー

```bash
curl -s -X POST http://localhost:5005/api/v1/checkout/guest \
  -H "Content-Type: application/json" \
  -d '{
    "paymentMethod": "",
    "shippingAddress": null,
    "email": "invalid-email"
  }'
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] 全ての必須フィールドのエラーが返る

#### テスト 5-4: POST /api/v1/checkout/guest - カート ID なし

```bash
# Cookie なし、cartId パラメータもなし
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5005/api/v1/checkout/guest \
  -H "Content-Type: application/json" \
  -d '{
    "paymentMethod": "card",
    "shippingAddress": {
      "recipientName": "テスト",
      "postalCode": "100-0001",
      "prefecture": "東京都",
      "city": "千代田区",
      "addressLine1": "1-1-1",
      "phoneNumber": "03-0000-0000"
    },
    "email": "test@example.com"
  }'
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] "カート ID が特定できません" エラー

---

### Phase 6: Stripe Webhook

> **📝 注意**: Webhook テストは Stripe CLI またはモックを使用して行う

#### テスト 6-1: POST /api/v1/payments/webhook - 署名なし

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5005/api/v1/payments/webhook \
  -H "Content-Type: application/json" \
  -d '{"type": "checkout.session.completed"}'
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] `Stripe-Signature` ヘッダーが必須

#### テスト 6-2: POST /api/v1/payments/webhook - 不正な署名

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5005/api/v1/payments/webhook \
  -H "Content-Type: application/json" \
  -H "Stripe-Signature: t=1234567890,v1=invalid_signature" \
  -d '{"type": "checkout.session.completed"}'
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] 署名検証失敗

#### テスト 6-3: Stripe CLI でのテスト

```bash
# Stripe CLI をインストール済みの場合
stripe listen --forward-to localhost:5005/api/v1/payments/webhook

# 別ターミナルでイベントをトリガー
stripe trigger checkout.session.completed
```

**検証項目:**
- [ ] Webhook イベントが正常に処理される
- [ ] 決済ステータスが COMPLETED に更新される

---

### Phase 7: gRPC サービス（内部通信）

> **📝 前提**: 
> - `grpcurl` ツールがインストールされていること（`brew install grpcurl` など）
> - gRPC リフレクションが有効であること、または proto ファイルを使用
> - 認証トークンが取得済みであること

#### テスト 7-1: CartGrpcService.GetCartSnapshot - 正常系

```bash
# カートを事前に作成
export GRPC_CART_ID=$(curl -s -c - http://localhost:5005/api/v1/cart | jq -r '.id')

# gRPC 呼び出し（grpcurl 使用）
grpcurl -plaintext \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -d "{\"cartId\": \"${GRPC_CART_ID}\"}" \
  localhost:5005 \
  SkiShop.Contracts.Cart.V1.CartGrpcService/GetCartSnapshot
```

**期待するレスポンス:**

```json
{
  "cartId": "uuid-xxx",
  "customerId": "",
  "totalAmountMinorUnits": "0",
  "currencyCode": "JPY",
  "items": []
}
```

**検証項目:**
- [ ] gRPC 呼び出しが成功する
- [ ] カート情報が正しく返る
- [ ] 金額が minor units（100倍）で返る

#### テスト 7-2: CartGrpcService.GetCartSnapshot - 認証なし

```bash
grpcurl -plaintext \
  -d '{"cartId": "some-cart-id"}' \
  localhost:5005 \
  SkiShop.Contracts.Cart.V1.CartGrpcService/GetCartSnapshot
```

**検証項目:**
- [ ] UNAUTHENTICATED エラーが返る

#### テスト 7-3: CartGrpcService.GetCartSnapshot - 存在しないカート

```bash
grpcurl -plaintext \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -d '{"cartId": "non-existent-cart-id"}' \
  localhost:5005 \
  SkiShop.Contracts.Cart.V1.CartGrpcService/GetCartSnapshot
```

**検証項目:**
- [ ] NOT_FOUND エラーが返る

#### テスト 7-4: CartGrpcService.ClearCart - 正常系

```bash
grpcurl -plaintext \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -d "{\"cartId\": \"${GRPC_CART_ID}\"}" \
  localhost:5005 \
  SkiShop.Contracts.Cart.V1.CartGrpcService/ClearCart
```

**期待するレスポンス:**

```json
{
  "success": true,
  "message": "カートをクリアしました"
}
```

**検証項目:**
- [ ] gRPC 呼び出しが成功する
- [ ] カートがクリアされる

#### テスト 7-5: CartGrpcService.ClearCart - べき等性確認

```bash
# 既にクリアされたカートを再度クリア
grpcurl -plaintext \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -d "{\"cartId\": \"${GRPC_CART_ID}\"}" \
  localhost:5005 \
  SkiShop.Contracts.Cart.V1.CartGrpcService/ClearCart
```

**検証項目:**
- [ ] 2回目の呼び出しも成功する（べき等）
- [ ] "カートは既にクリア済みです" メッセージが返る

#### テスト 7-6: PaymentGrpcService.ProcessPayment - 正常系

```bash
grpcurl -plaintext \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -d '{
    "orderId": "test-order-001",
    "customerId": "test-customer-001",
    "amountMinorUnits": "5000000",
    "currencyCode": "JPY",
    "paymentMethod": "card",
    "idempotencyKey": "idem-key-001"
  }' \
  localhost:5005 \
  SkiShop.Contracts.Payment.V1.PaymentGrpcService/ProcessPayment
```

**期待するレスポンス:**

```json
{
  "paymentId": "uuid-xxx",
  "stripeCheckoutSessionId": "",
  "checkoutUrl": "",
  "status": "PENDING"
}
```

**検証項目:**
- [ ] 決済レコードが作成される
- [ ] Outbox イベントが作成される
- [ ] ステータスが PENDING である

#### テスト 7-7: PaymentGrpcService.ProcessPayment - べき等性確認

```bash
# 同じ orderId で再度呼び出し
grpcurl -plaintext \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -d '{
    "orderId": "test-order-001",
    "customerId": "test-customer-001",
    "amountMinorUnits": "5000000",
    "currencyCode": "JPY",
    "paymentMethod": "card",
    "idempotencyKey": "idem-key-001"
  }' \
  localhost:5005 \
  SkiShop.Contracts.Payment.V1.PaymentGrpcService/ProcessPayment
```

**検証項目:**
- [ ] 2回目の呼び出しも成功する（べき等）
- [ ] 同じ paymentId が返る
- [ ] 新しいレコードは作成されない

#### テスト 7-8: PaymentGrpcService.RefundPayment - 認証なし

```bash
grpcurl -plaintext \
  -d '{
    "paymentId": "some-payment-id",
    "reason": "テスト返金",
    "idempotencyKey": "refund-key-001"
  }' \
  localhost:5005 \
  SkiShop.Contracts.Payment.V1.PaymentGrpcService/RefundPayment
```

**検証項目:**
- [ ] UNAUTHENTICATED エラーが返る

#### テスト 7-9: PaymentGrpcService.RefundPayment - 存在しない決済

```bash
grpcurl -plaintext \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -d '{
    "paymentId": "non-existent-payment-id",
    "reason": "テスト返金",
    "idempotencyKey": "refund-key-002"
  }' \
  localhost:5005 \
  SkiShop.Contracts.Payment.V1.PaymentGrpcService/RefundPayment
```

**検証項目:**
- [ ] NOT_FOUND エラーが返る

#### テスト 7-10: PaymentGrpcService.RefundPayment - ステータスが Completed でない

```bash
# PENDING ステータスの決済に対して返金を試みる
grpcurl -plaintext \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -d "{
    \"paymentId\": \"${GRPC_PAYMENT_ID}\",
    \"reason\": \"テスト返金\",
    \"idempotencyKey\": \"refund-key-003\"
  }" \
  localhost:5005 \
  SkiShop.Contracts.Payment.V1.PaymentGrpcService/RefundPayment
```

**検証項目:**
- [ ] FAILED_PRECONDITION エラーが返る
- [ ] "決済ステータスが Completed ではありません" メッセージが含まれる

---

## 5. verification-report.md のフォーマット

検証結果は以下のフォーマットで `verification-report.md` に記録する：

```markdown
# PaymentCartService API 検証レポート

**実施日時**: YYYY-MM-DD
**検証環境**: Docker Compose (localhost:5005)
**検証対象**: PaymentCartService v1.0

---

## テスト結果サマリー

| Phase | テスト項目 | 結果 | 備考 |
|-------|-----------|------|------|
| 0 | GET /health | ✅ PASS / ❌ FAIL | |
| 0 | GET /health/ready | ✅ PASS / ❌ FAIL | |
| 1 | カート取得 | ✅ PASS / ❌ FAIL | |
| ... | ... | ... | |

---

## Phase 0: ヘルスチェック

### テスト 0-1: GET /health

**リクエスト:**
\`\`\`bash
curl -s http://localhost:5005/health
\`\`\`

**レスポンス:**
\`\`\`
Healthy
\`\`\`

**結果**: ✅ PASS

---
（以下、各テストケースの結果を記録）
```

---

## 6. テスト用データ

### 6.1 商品データサンプル

| productId | productName | sku | unitPrice |
|-----------|-------------|-----|-----------|
| PROD-001 | スキーブーツ プロモデル | SKI-BOOT-PRO-001 | 45000 |
| PROD-002 | スキーポール カーボン | SKI-POLE-CARBON-002 | 15000 |
| PROD-003 | スキーゴーグル UV対応 | SKI-GOGGLE-UV-003 | 8000 |
| PROD-004 | スキーウェア 防水 | SKI-WEAR-WP-004 | 35000 |
| PROD-005 | スキーグローブ 保温 | SKI-GLOVE-WARM-005 | 5000 |

### 6.2 配送先住所サンプル

```json
{
  "recipientName": "山田太郎",
  "postalCode": "100-0001",
  "prefecture": "東京都",
  "city": "千代田区",
  "addressLine1": "丸の内1-1-1",
  "addressLine2": "〇〇ビル 5F",
  "phoneNumber": "03-1234-5678"
}
```

### 6.3 バリデーションルール

| フィールド | ルール |
|-----------|--------|
| `quantity` | 1〜10 の範囲 |
| `unitPrice` | 0.01〜99999999.99 の範囲 |
| `productId` | 必須、最大100文字 |
| `productName` | 必須、最大200文字 |
| `sku` | 必須、最大100文字 |
| `email` | 必須、有効なメールアドレス形式、最大255文字 |
| `cartId` | 最大36文字（UUID形式） |
| `paymentMethod` | 必須、最大50文字 |

---

## 7. トラブルシューティング

### 7.1 よくある問題

| 問題 | 原因 | 解決方法 |
|------|------|---------|
| 401 Unauthorized | トークン期限切れ | 再ログインしてトークン再取得 |
| 404 カートが見つかりません | Cookie が送信されていない | `-b cookies.txt -c cookies.txt` オプション確認 |
| 500 Internal Server Error | Stripe API キー未設定 | `docker compose` の環境変数確認 |
| Kafka 接続エラー | Kafka 未起動 | `docker compose up -d kafka` |
| DB 接続エラー | PostgreSQL 未起動 | `docker compose up -d postgres` |
| gRPC 接続エラー | gRPC リフレクション未有効 | proto ファイルを `-proto` オプションで指定 |
| UNAUTHENTICATED | トークン未設定または期限切れ | `-H "Authorization: Bearer ${ACCESS_TOKEN}"` を確認 |

### 7.2 ログ確認

```bash
# PaymentCartService のログ
docker compose logs -f payment-cart-service

# 直近100行のログ
docker compose logs --tail=100 payment-cart-service
```

### 7.3 データベース確認

```bash
# PostgreSQL に接続
docker exec -it skishop-postgres psql -U skishop -d paymentdb

# カートテーブル確認
SELECT * FROM carts LIMIT 10;

# 決済テーブル確認
SELECT * FROM payments LIMIT 10;

# カートアイテム確認
SELECT * FROM cart_items LIMIT 10;
```

### 7.4 Cookie のデバッグ

```bash
# Cookie ファイルの内容確認
cat cookies.txt

# レスポンスヘッダーに Set-Cookie が含まれているか確認
curl -v http://localhost:5005/api/v1/cart 2>&1 | grep -i "set-cookie"
```

---

## 8. 実行手順チェックリスト

### 事前準備

- [ ] Docker Compose で全サービスが起動している
- [ ] PaymentCartService のヘルスチェックが通る（`/health` → `Healthy`）
- [ ] AuthService のヘルスチェックが通る
- [ ] テスト用一般ユーザーを作成してトークン取得（`ACCESS_TOKEN`, `USER_ID`）
- [ ] テスト用管理者ユーザーを作成してトークン取得（`ADMIN_TOKEN`, `ADMIN_USER_ID`）
- [ ] Cookie ファイルを初期化（`rm -f cookies.txt`）
- [ ] grpcurl がインストールされている（gRPC テスト用）

### テスト実行

- [ ] Phase 0: ヘルスチェック（2件）
- [ ] Phase 1: カート管理（13件）
- [ ] Phase 2: カートマージ（4件）
- [ ] Phase 3: 決済（10件）
- [ ] Phase 4: 返金（4件）
- [ ] Phase 5: ゲストチェックアウト（4件）
- [ ] Phase 6: Webhook（3件）
- [ ] Phase 7: gRPC サービス（10件）

### 検証完了

- [ ] 全テストケースの結果を `verification-report.md` に記録
- [ ] 失敗したテストの原因を調査・記録
- [ ] 発見したバグを Issue として登録
