# InventoryManagementService 検証計画書（verification-plan.md）

本ドキュメントは InventoryManagementService の全エンドポイントに対する動作検証の手順とフォーマットを定義する。
検証結果は `verification-report.md` に記録し、InventoryManagementService の使い方を理解するためのリファレンスとして活用する。

---

## 1. 検証の目的

1. **機能確認**: 全エンドポイントが設計通りに動作することを確認する
2. **使い方の文書化**: 各 API の呼び出し方法・パラメータ・レスポンスを実例で示す
3. **異常系の確認**: バリデーションエラー、認証エラー、リソース未検出等の動作を確認する

---

## 2. 前提条件

### 2.1 環境構成

| 項目 | 値 |
|------|-----|
| InventoryManagementService URL | `http://localhost:5003` |
| PostgreSQL | `localhost:5432` |
| Redis | `localhost:6379` |
| Kafka | `localhost:9092` |
| AuthService URL（トークン取得用） | `http://localhost:5001` |

### 2.2 起動コマンド

```bash
# Docker Compose で InventoryManagementService と依存サービスを起動
cd /Users/yoterada/GitHub/DotNet-Skishop-App
docker compose up -d inventory-management-service
```

### 2.3 事前確認コマンド

```bash
# ヘルスチェック
curl -s http://localhost:5003/health
# 期待結果: Healthy

# Readiness チェック
curl -s http://localhost:5003/health/ready
# 期待結果: Healthy
```

### 2.4 認証トークンの取得

管理者権限が必要なエンドポイントの検証には、AuthService から JWT トークンを取得する必要がある。

```bash
# 管理者ユーザーでログイン
curl -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@example.com",
    "password": "Admin123!"
  }'
```

取得した `accessToken` を環境変数に設定:

```bash
export ADMIN_TOKEN="eyJhbGciOiJIUzI1NiIs..."
export USER_TOKEN="eyJhbGciOiJIUzI1NiIs..."
```

---

## 3. エンドポイント一覧

### 3.1 Products（商品）

| # | メソッド | パス | 説明 | 認証 |
|---|---------|------|------|------|
| P-1 | GET | `/api/products` | 商品一覧取得（ページネーション） | 不要 |
| P-2 | GET | `/api/products/{id}` | 商品詳細取得 | 不要 |
| P-3 | GET | `/api/products/sku/{sku}` | SKU コードで商品取得 | 不要 |
| P-4 | GET | `/api/products/search` | 商品検索（キーワード・カテゴリ・ブランド） | 不要 |
| P-5 | GET | `/api/products/category/{categoryId}` | カテゴリ別商品一覧 | 不要 |
| P-6 | POST | `/api/products/batch` | 複数商品ID一括取得 | 不要 |
| P-7 | POST | `/api/products` | 商品作成 | Admin |
| P-8 | PATCH | `/api/products/{id}` | 商品更新（部分更新） | Admin |
| P-9 | DELETE | `/api/products/{id}` | 商品削除 | Admin |
| P-10 | POST | `/api/products/{id}/images` | 商品画像アップロード | Admin |

### 3.2 Categories（カテゴリ）

| # | メソッド | パス | 説明 | 認証 |
|---|---------|------|------|------|
| C-1 | GET | `/api/categories` | カテゴリ一覧取得 | 不要 |
| C-2 | GET | `/api/categories/{id}` | カテゴリ詳細取得 | 不要 |
| C-3 | GET | `/api/categories/{id}/products` | カテゴリ別商品一覧 | 不要 |
| C-4 | POST | `/api/categories` | カテゴリ作成 | Admin |
| C-5 | PATCH | `/api/categories/{id}` | カテゴリ更新 | Admin |
| C-6 | DELETE | `/api/categories/{id}` | カテゴリ削除 | Admin |

### 3.3 Inventory（在庫）

| # | メソッド | パス | 説明 | 認証 |
|---|---------|------|------|------|
| I-1 | GET | `/api/inventory/{productId}` | 商品の在庫情報取得 | 不要 |
| I-2 | POST | `/api/inventory/batch` | 複数商品在庫一括取得 | 不要 |
| I-3 | GET | `/api/inventory/low-stock` | 低在庫商品一覧（閾値10以下） | Admin |
| I-4 | POST | `/api/inventory/stock-in` | 入庫処理 | Admin |
| I-5 | POST | `/api/inventory/stock-out` | 出庫処理 | Admin |
| I-6 | GET | `/api/inventory/status/{productId}` | 在庫ステータス取得 | 必要 |

### 3.4 Prices（価格）

| # | メソッド | パス | 説明 | 認証 |
|---|---------|------|------|------|
| PR-1 | GET | `/api/prices/{productId}` | 商品の現在価格取得 | 不要 |
| PR-2 | GET | `/api/prices/history/{productId}` | 価格変更履歴取得 | Admin |
| PR-3 | POST | `/api/prices` | 価格作成 | Admin |
| PR-4 | PATCH | `/api/prices/{productId}` | 価格更新（部分更新） | Admin |

### 3.5 Reviews（レビュー）

| # | メソッド | パス | 説明 | 認証 |
|---|---------|------|------|------|
| R-1 | GET | `/api/reviews/product/{productId}` | 商品のレビュー一覧 | 不要 |
| R-2 | GET | `/api/reviews/{id}` | レビュー詳細取得 | 不要 |
| R-3 | POST | `/api/reviews` | レビュー投稿 | 必要 |
| R-4 | POST | `/api/reviews/{id}/response` | 管理者返信追加 | Admin |
| R-5 | POST | `/api/reviews/{id}/helpful` | 「参考になった」投票 | 必要 |
| R-6 | PATCH | `/api/reviews/{id}/status` | レビューステータス更新 | Admin |

### 3.6 Size Guides（サイズガイド）

| # | メソッド | パス | 説明 | 認証 |
|---|---------|------|------|------|
| S-1 | GET | `/api/size-guides/{categoryId}` | カテゴリのサイズガイド取得 | 不要 |
| S-2 | POST | `/api/size-guides` | サイズガイド作成 | Admin |
| S-3 | PATCH | `/api/size-guides/{id}` | サイズガイド更新 | Admin |

### 3.7 Health（ヘルスチェック）

| # | メソッド | パス | 説明 | 認証 |
|---|---------|------|------|------|
| H-1 | GET | `/health` | Liveness チェック | 不要 |
| H-2 | GET | `/health/ready` | Readiness チェック | 不要 |

### 3.8 OpenAPI（API ドキュメント）

| # | メソッド | パス | 説明 | 認証 |
|---|---------|------|------|------|
| O-1 | GET | `/openapi/v1.json` | OpenAPI 仕様ドキュメント | 不要 |

> **📝 注意**: .NET 10 Preview の既知問題により、`AsParameters` を使用するエンドポイントで OpenAPI スキーマ生成に問題がある場合がある。

---

## 4. 検証手順

検証は以下の順序で実施する。依存関係を考慮した順番となっている。

---

### Phase 1: ヘルスチェック

#### テスト H-1: Liveness チェック

```bash
curl -s http://localhost:5003/health
```

**期待するレスポンス（200 OK）:**

```
Healthy
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] レスポンスボディが `Healthy` である

#### テスト H-2: Readiness チェック

```bash
curl -s http://localhost:5003/health/ready
```

**期待するレスポンス（200 OK）:**

```
Healthy
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] レスポンスボディが `Healthy` である

#### テスト O-1: OpenAPI 仕様ドキュメント取得

```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:5003/openapi/v1.json
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] JSON 形式の OpenAPI 仕様が返される
- [ ] 認証不要でアクセスできる

> **📝 注意**: .NET 10 Preview の既知問題により、`AsParameters` を使用するエンドポイントで 500 エラーが発生する場合がある。その場合は既知問題として記録する。

---

### Phase 2: カテゴリ管理（Categories）

カテゴリは商品の前提条件となるため、最初に作成する。

#### テスト C-4: カテゴリ作成

```bash
curl -X POST http://localhost:5003/api/categories \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{
    "name": "スキー板",
    "description": "各種スキー板のカテゴリ",
    "slug": "skis",
    "parentId": null
  }'
```

**期待するレスポンス（201 Created）:**

```json
{
  "id": "uuid-xxx",
  "name": "スキー板",
  "description": "各種スキー板のカテゴリ",
  "slug": "skis",
  "parentId": null,
  "isActive": true,
  "createdAt": "2026-04-06T15:00:00Z",
  "updatedAt": "2026-04-06T15:00:00Z"
}
```

**検証項目:**
- [ ] ステータスコード 201 を返す
- [ ] `Location` ヘッダーに `/api/categories/{id}` が含まれる
- [ ] レスポンスにカテゴリ情報が含まれる
- [ ] `isActive` が `true` である

> **📝 メモ**: 作成されたカテゴリ ID を `$CATEGORY_ID` として記録する。

#### テスト C-4-E1: カテゴリ作成（認証なし）

```bash
curl -X POST http://localhost:5003/api/categories \
  -H "Content-Type: application/json" \
  -d '{
    "name": "ブーツ",
    "slug": "boots"
  }'
```

**期待するレスポンス（401 Unauthorized）:**

```json
{
  "type": "...",
  "title": "Unauthorized",
  "status": 401
}
```

**検証項目:**
- [ ] ステータスコード 401 を返す

#### テスト C-4-E2: カテゴリ作成（バリデーションエラー）

```bash
curl -X POST http://localhost:5003/api/categories \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{
    "name": "",
    "slug": ""
  }'
```

**期待するレスポンス（400 Bad Request）:**

```json
{
  "type": "...",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Name": ["カテゴリ名は必須です"],
    "Slug": ["スラグは必須です"]
  }
}
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] 全てのバリデーションエラーが一度に返される

#### テスト C-1: カテゴリ一覧取得

```bash
curl -s http://localhost:5003/api/categories
```

**期待するレスポンス（200 OK）:**

```json
[
  {
    "id": "uuid-xxx",
    "name": "スキー板",
    "description": "各種スキー板のカテゴリ",
    "slug": "skis",
    "parentId": null,
    "isActive": true,
    "createdAt": "2026-04-06T15:00:00Z",
    "updatedAt": "2026-04-06T15:00:00Z"
  }
]
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 配列形式でカテゴリが返される
- [ ] 認証不要でアクセスできる

#### テスト C-2: カテゴリ詳細取得

```bash
curl -s http://localhost:5003/api/categories/$CATEGORY_ID
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-xxx",
  "name": "スキー板",
  "description": "各種スキー板のカテゴリ",
  "slug": "skis",
  "parentId": null,
  "isActive": true,
  "createdAt": "2026-04-06T15:00:00Z",
  "updatedAt": "2026-04-06T15:00:00Z"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 指定した ID のカテゴリが返される

#### テスト C-2-E1: カテゴリ詳細取得（存在しない ID）

```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:5003/api/categories/non-existent-id
```

**期待するレスポンス（404 Not Found）:**

**検証項目:**
- [ ] ステータスコード 404 を返す

#### テスト C-5: カテゴリ更新

```bash
curl -X PATCH http://localhost:5003/api/categories/$CATEGORY_ID \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{
    "description": "オールマウンテン・レーシング・フリーライドなど各種スキー板"
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-xxx",
  "name": "スキー板",
  "description": "オールマウンテン・レーシング・フリーライドなど各種スキー板",
  "slug": "skis",
  "parentId": null,
  "isActive": true,
  "createdAt": "2026-04-06T15:00:00Z",
  "updatedAt": "2026-04-06T15:05:00Z"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 指定したフィールドのみ更新される（部分更新）
- [ ] `updatedAt` が更新される

#### テスト C-3: カテゴリ別商品一覧取得

```bash
curl -s "http://localhost:5003/api/categories/$CATEGORY_ID/products?page=1&size=10"
```

**期待するレスポンス（200 OK）:**

```json
{
  "items": [],
  "totalCount": 0,
  "page": 1,
  "size": 10,
  "totalPages": 0
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] ページネーション情報が含まれる
- [ ] 認証不要でアクセスできる

> **📝 注意**: Phase 3 で商品を作成後に再度テストし、商品が含まれることを確認する。

#### テスト C-5-E1: カテゴリ更新（一般ユーザー権限で 403）

```bash
curl -X PATCH http://localhost:5003/api/categories/$CATEGORY_ID \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -d '{
    "description": "更新テスト"
  }'
```

**期待するレスポンス（403 Forbidden）:**

**検証項目:**
- [ ] ステータスコード 403 を返す
- [ ] AdminOnly ポリシーが適用されている

---

### Phase 3: 商品管理（Products）

#### テスト P-7: 商品作成

```bash
curl -X POST http://localhost:5003/api/products \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{
    "sku": "SKI-001",
    "name": "オールマウンテンスキー Pro",
    "description": "上級者向けオールラウンドスキー板",
    "categoryId": "'$CATEGORY_ID'",
    "brand": "SkiShop Original",
    "price": 89000,
    "isActive": true
  }'
```

**期待するレスポンス（201 Created）:**

```json
{
  "id": "uuid-product",
  "sku": "SKI-001",
  "name": "オールマウンテンスキー Pro",
  "description": "上級者向けオールラウンドスキー板",
  "categoryId": "uuid-category",
  "brand": "SkiShop Original",
  "price": 89000,
  "isActive": true,
  "images": [],
  "createdAt": "2026-04-06T15:10:00Z",
  "updatedAt": "2026-04-06T15:10:00Z"
}
```

**検証項目:**
- [ ] ステータスコード 201 を返す
- [ ] `Location` ヘッダーに `/api/products/{id}` が含まれる
- [ ] SKU がユニークであることを確認

> **📝 メモ**: 作成された商品 ID を `$PRODUCT_ID` として記録する。

#### テスト P-7-E1: 商品作成（SKU 重複）

```bash
curl -X POST http://localhost:5003/api/products \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{
    "sku": "SKI-001",
    "name": "別の商品",
    "categoryId": "'$CATEGORY_ID'"
  }'
```

**期待するレスポンス（409 Conflict または 422 Unprocessable Entity）:**

**検証項目:**
- [ ] ステータスコード 409 または 422 を返す
- [ ] SKU 重複エラーメッセージが含まれる

#### テスト P-1: 商品一覧取得（ページネーション）

```bash
curl -s "http://localhost:5003/api/products?page=1&size=10"
```

**期待するレスポンス（200 OK）:**

```json
{
  "items": [
    {
      "id": "uuid-product",
      "sku": "SKI-001",
      "name": "オールマウンテンスキー Pro",
      ...
    }
  ],
  "totalCount": 1,
  "page": 1,
  "size": 10,
  "totalPages": 1
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] ページネーション情報が含まれる
- [ ] 認証不要でアクセスできる

#### テスト P-1-E1: 商品一覧（ページネーションバリデーションエラー）

```bash
curl -s "http://localhost:5003/api/products?page=0&size=200"
```

**期待するレスポンス（400 Bad Request）:**

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] `page` は 1 以上、`size` は 1〜100 の範囲である必要がある

#### テスト P-2: 商品詳細取得

```bash
curl -s http://localhost:5003/api/products/$PRODUCT_ID
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-product",
  "sku": "SKI-001",
  "name": "オールマウンテンスキー Pro",
  "description": "上級者向けオールラウンドスキー板",
  "categoryId": "uuid-category",
  "brand": "SkiShop Original",
  "price": 89000,
  "isActive": true,
  "images": [],
  "createdAt": "2026-04-06T15:10:00Z",
  "updatedAt": "2026-04-06T15:10:00Z"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 完全な商品情報が返される

#### テスト P-3: SKU で商品取得

```bash
curl -s http://localhost:5003/api/products/sku/SKI-001
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] SKU に一致する商品が返される

#### テスト P-4: 商品検索

```bash
curl -s "http://localhost:5003/api/products/search?keyword=スキー&page=1&size=10"
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] キーワードに一致する商品が返される
- [ ] ページネーション情報が含まれる

#### テスト P-4-2: 商品検索（ブランド + カテゴリ絞り込み）

```bash
curl -s "http://localhost:5003/api/products/search?brand=SkiShop%20Original&categoryId=$CATEGORY_ID&page=1&size=10"
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] ブランドとカテゴリに一致する商品のみが返される
- [ ] `search` レート制限ポリシーが適用されている

> **📝 注意**: このエンドポイントには `search` レート制限ポリシーが適用されており、短時間に大量のリクエストを送ると 429 Too Many Requests が返される可能性がある。

#### テスト P-5: カテゴリ別商品一覧

```bash
curl -s "http://localhost:5003/api/products/category/$CATEGORY_ID?page=1&size=10"
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 指定カテゴリの商品のみが返される

#### テスト P-6: 複数商品 ID で一括取得

```bash
curl -X POST http://localhost:5003/api/products/batch \
  -H "Content-Type: application/json" \
  -d '{
    "ids": ["'$PRODUCT_ID'"]
  }'
```

**期待するレスポンス（200 OK）:**

```json
[
  {
    "id": "uuid-product",
    "sku": "SKI-001",
    ...
  }
]
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 指定した ID の商品のみが返される

#### テスト P-8: 商品更新（部分更新）

```bash
curl -X PATCH http://localhost:5003/api/products/$PRODUCT_ID \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{
    "price": 79000
  }'
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 指定したフィールドのみ更新される
- [ ] `updatedAt` が更新される

#### テスト P-10: 商品画像アップロード

```bash
curl -X POST http://localhost:5003/api/products/$PRODUCT_ID/images \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -F "file=@/path/to/test-image.jpg"
```

**期待するレスポンス（201 Created）:**

```json
{
  "id": "uuid-image",
  "productId": "uuid-product",
  "url": "https://...",
  "displayOrder": 0,
  "imageType": "MAIN",
  "createdAt": "2026-04-06T15:15:00Z"
}
```

**検証項目:**
- [ ] ステータスコード 201 を返す
- [ ] `Location` ヘッダーに画像 URI が含まれる
- [ ] 画像情報が返される

---

### Phase 4: 在庫管理（Inventory）

#### テスト I-4: 入庫処理

```bash
curl -X POST http://localhost:5003/api/inventory/stock-in \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{
    "productId": "'$PRODUCT_ID'",
    "quantity": 100,
    "location": "倉庫A",
    "reason": "初期入荷"
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-inventory",
  "productId": "uuid-product",
  "quantity": 100,
  "reservedQuantity": 0,
  "availableQuantity": 100,
  "location": "倉庫A",
  "status": "IN_STOCK",
  "lowStockThreshold": 10,
  "updatedAt": "2026-04-06T15:20:00Z"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `quantity` が入庫分増加する
- [ ] `availableQuantity` が計算される

#### テスト I-1: 商品の在庫情報取得

```bash
curl -s http://localhost:5003/api/inventory/$PRODUCT_ID
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-inventory",
  "productId": "uuid-product",
  "quantity": 100,
  "reservedQuantity": 0,
  "availableQuantity": 100,
  "location": "倉庫A",
  "status": "IN_STOCK",
  "lowStockThreshold": 10,
  "updatedAt": "2026-04-06T15:20:00Z"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 認証不要でアクセスできる

#### テスト I-2: 複数商品の在庫一括取得

```bash
curl -X POST http://localhost:5003/api/inventory/batch \
  -H "Content-Type: application/json" \
  -d '{
    "ids": ["'$PRODUCT_ID'"]
  }'
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 指定した商品の在庫情報のみが返される

#### テスト I-5: 出庫処理

```bash
curl -X POST http://localhost:5003/api/inventory/stock-out \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{
    "productId": "'$PRODUCT_ID'",
    "quantity": 10,
    "reason": "販売出庫"
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-inventory",
  "productId": "uuid-product",
  "quantity": 90,
  "reservedQuantity": 0,
  "availableQuantity": 90,
  ...
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `quantity` が出庫分減少する

#### テスト I-5-E1: 出庫処理（在庫不足）

```bash
curl -X POST http://localhost:5003/api/inventory/stock-out \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{
    "productId": "'$PRODUCT_ID'",
    "quantity": 10000
  }'
```

**期待するレスポンス（422 Unprocessable Entity）:**

**検証項目:**
- [ ] ステータスコード 422 を返す
- [ ] 在庫不足エラーメッセージが含まれる

#### テスト I-3: 低在庫商品一覧

```bash
curl -s "http://localhost:5003/api/inventory/low-stock?page=1&size=10" \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 閾値 10 以下の在庫のみが返される
- [ ] Admin 認証が必要

#### テスト I-6: 在庫ステータス取得

```bash
curl -s http://localhost:5003/api/inventory/status/$PRODUCT_ID \
  -H "Authorization: Bearer $USER_TOKEN"
```

**期待するレスポンス（200 OK）:**

```json
{
  "productId": "uuid-product",
  "status": "IN_STOCK",
  "availableQuantity": 90,
  "isAvailable": true
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 認証が必要（User または Admin）

---

### Phase 5: 価格管理（Prices）

#### テスト PR-3: 価格作成

```bash
curl -X POST http://localhost:5003/api/prices \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{
    "productId": "'$PRODUCT_ID'",
    "regularPrice": 89000,
    "currencyCode": "JPY"
  }'
```

**期待するレスポンス（201 Created）:**

```json
{
  "id": "uuid-price",
  "productId": "uuid-product",
  "regularPrice": 89000,
  "salePrice": null,
  "saleStartDate": null,
  "saleEndDate": null,
  "currencyCode": "JPY",
  "isActive": true,
  "createdAt": "2026-04-06T15:25:00Z",
  "updatedAt": "2026-04-06T15:25:00Z"
}
```

**検証項目:**
- [ ] ステータスコード 201 を返す
- [ ] `Location` ヘッダーに `/api/prices/{productId}` が含まれる

#### テスト PR-1: 商品の現在価格取得

```bash
curl -s http://localhost:5003/api/prices/$PRODUCT_ID
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 認証不要でアクセスできる

#### テスト PR-4: 価格更新（セール価格設定）

```bash
curl -X PATCH http://localhost:5003/api/prices/$PRODUCT_ID \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{
    "salePrice": 69000,
    "saleStartDate": "2026-04-01T00:00:00Z",
    "saleEndDate": "2026-04-30T23:59:59Z"
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-price",
  "productId": "uuid-product",
  "regularPrice": 89000,
  "salePrice": 69000,
  "saleStartDate": "2026-04-01T00:00:00Z",
  "saleEndDate": "2026-04-30T23:59:59Z",
  ...
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] セール価格と期間が設定される

#### テスト PR-2: 価格変更履歴取得

```bash
curl -s "http://localhost:5003/api/prices/history/$PRODUCT_ID?page=1&size=10" \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

**期待するレスポンス（200 OK）:**

```json
{
  "items": [
    {
      "id": "uuid-history",
      "productId": "uuid-product",
      "price": 89000,
      "priceType": "REGULAR",
      "effectiveDate": "2026-04-06T15:25:00Z",
      "changedBy": "admin-user-id",
      "reason": "価格作成"
    }
  ],
  "totalCount": 1,
  ...
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 価格変更履歴が時系列で返される
- [ ] Admin 認証が必要

---

### Phase 6: レビュー管理（Reviews）

#### テスト R-3: レビュー投稿

```bash
curl -X POST http://localhost:5003/api/reviews \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -d '{
    "productId": "'$PRODUCT_ID'",
    "rating": 5,
    "title": "最高のスキー板！",
    "content": "とても滑りやすく、コントロールしやすいです。上級者におすすめ。"
  }'
```

**期待するレスポンス（201 Created）:**

```json
{
  "id": "uuid-review",
  "productId": "uuid-product",
  "userId": "uuid-user",
  "rating": 5,
  "title": "最高のスキー板！",
  "content": "とても滑りやすく、コントロールしやすいです。上級者におすすめ。",
  "status": "PENDING",
  "helpfulCount": 0,
  "responses": [],
  "createdAt": "2026-04-06T15:30:00Z"
}
```

**検証項目:**
- [ ] ステータスコード 201 を返す
- [ ] `status` が `PENDING` で初期化される
- [ ] ユーザー認証が必要

> **📝 メモ**: 作成されたレビュー ID を `$REVIEW_ID` として記録する。

#### テスト R-1: 商品のレビュー一覧取得

```bash
curl -s "http://localhost:5003/api/reviews/product/$PRODUCT_ID?page=1&size=10"
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 認証不要でアクセスできる
- [ ] ページネーション情報が含まれる

#### テスト R-2: レビュー詳細取得

```bash
curl -s http://localhost:5003/api/reviews/$REVIEW_ID
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 認証不要でアクセスできる

#### テスト R-5: 「参考になった」投票

```bash
curl -X POST http://localhost:5003/api/reviews/$REVIEW_ID/helpful \
  -H "Authorization: Bearer $USER_TOKEN"
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-review",
  ...
  "helpfulCount": 1,
  ...
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `helpfulCount` が 1 増加する
- [ ] ユーザー認証が必要

#### テスト R-6: レビューステータス更新（承認）

```bash
curl -X PATCH http://localhost:5003/api/reviews/$REVIEW_ID/status \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{
    "status": "APPROVED"
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-review",
  ...
  "status": "APPROVED",
  ...
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `status` が `APPROVED` に更新される
- [ ] Admin 認証が必要

#### テスト R-4: 管理者返信追加

```bash
curl -X POST http://localhost:5003/api/reviews/$REVIEW_ID/response \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{
    "content": "レビューありがとうございます！引き続きご愛顧のほどよろしくお願いいたします。"
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-review",
  ...
  "responses": [
    {
      "id": "uuid-response",
      "reviewId": "uuid-review",
      "responderId": "admin-user-id",
      "content": "レビューありがとうございます！引き続きご愛顧のほどよろしくお願いいたします。",
      "createdAt": "2026-04-06T15:35:00Z"
    }
  ]
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `responses` 配列に返信が追加される
- [ ] Admin 認証が必要

---

### Phase 7: サイズガイド（Size Guides）

#### テスト S-2: サイズガイド作成

```bash
curl -X POST http://localhost:5003/api/size-guides \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{
    "categoryId": "'$CATEGORY_ID'",
    "name": "スキー板サイズガイド",
    "description": "身長に基づくスキー板の選び方",
    "measurements": [
      { "label": "身長 150-160cm", "size": "155cm" },
      { "label": "身長 160-170cm", "size": "165cm" },
      { "label": "身長 170-180cm", "size": "175cm" }
    ]
  }'
```

**期待するレスポンス（201 Created）:**

```json
{
  "id": "uuid-sizeguide",
  "categoryId": "uuid-category",
  "name": "スキー板サイズガイド",
  "description": "身長に基づくスキー板の選び方",
  "measurements": [...],
  "createdAt": "2026-04-06T15:40:00Z",
  "updatedAt": "2026-04-06T15:40:00Z"
}
```

**検証項目:**
- [ ] ステータスコード 201 を返す
- [ ] `Location` ヘッダーに `/api/size-guides/{categoryId}` が含まれる

> **📝 メモ**: 作成されたサイズガイド ID を `$SIZEGUIDE_ID` として記録する。

#### テスト S-1: カテゴリのサイズガイド取得

```bash
curl -s http://localhost:5003/api/size-guides/$CATEGORY_ID
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 認証不要でアクセスできる

#### テスト S-3: サイズガイド更新

```bash
curl -X PATCH http://localhost:5003/api/size-guides/$SIZEGUIDE_ID \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{
    "description": "身長・体重に基づくスキー板の選び方ガイド"
  }'
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 指定したフィールドのみ更新される
- [ ] Admin 認証が必要

---

### Phase 8: クリーンアップ（削除テスト）

#### テスト P-9: 商品削除

```bash
curl -X DELETE http://localhost:5003/api/products/$PRODUCT_ID \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

**期待するレスポンス（204 No Content）:**

**検証項目:**
- [ ] ステータスコード 204 を返す
- [ ] レスポンスボディがない

#### テスト C-6: カテゴリ削除

```bash
curl -X DELETE http://localhost:5003/api/categories/$CATEGORY_ID \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

**期待するレスポンス（204 No Content）:**

**検証項目:**
- [ ] ステータスコード 204 を返す

---

## 5. 検証結果テンプレート

検証結果は以下のフォーマットで `verification-report.md` に記録する。

```markdown
## テスト X-Y: [テスト名]

**実行日時**: 2026-04-06T15:00:00Z

**リクエスト:**
\`\`\`bash
curl -X METHOD http://localhost:5003/api/xxx ...
\`\`\`

**レスポンス:**
- ステータスコード: 200
- ヘッダー: `Location: /api/xxx/id`
- ボディ:
\`\`\`json
{ ... }
\`\`\`

**検証結果:**
- [x] ステータスコード 200 を返す
- [x] レスポンスに期待する項目が含まれる
- [ ] 失敗した項目があれば ❌ マーク

**備考:**
(気づいた点やエラーの詳細など)
```

---

## 6. エンドポイント数サマリー

| カテゴリ | エンドポイント数 | 認証不要 | 認証必要 | Admin 必須 |
|----------|-----------------|---------|---------|-----------|
| Products | 10 | 6 | 0 | 4 |
| Categories | 6 | 3 | 0 | 3 |
| Inventory | 6 | 2 | 1 | 3 |
| Prices | 4 | 1 | 0 | 3 |
| Reviews | 6 | 2 | 2 | 2 |
| Size Guides | 3 | 1 | 0 | 2 |
| Health | 2 | 2 | 0 | 0 |
| OpenAPI | 1 | 1 | 0 | 0 |
| **合計** | **38** | **18** | **3** | **17** |

---

## 7. 注意事項

1. **認証トークンの有効期限**: JWT アクセストークンは有効期限がある。期限切れの場合は再取得する。
2. **依存関係**: 商品はカテゴリに依存するため、カテゴリを先に作成する。在庫・価格・レビューは商品に依存する。
3. **べき等性**: DELETE は複数回実行しても同じ結果（204）を返すべきである。
4. **レート制限**: 検索エンドポイント (`/api/products/search`) には `search` レート制限ポリシーが適用される。
5. **画像アップロード**: `POST /api/products/{id}/images` は `multipart/form-data` で送信する。
6. **OpenAPI の制限事項**: .NET 10 Preview では `AsParameters` を使用するエンドポイントで OpenAPI スキーマ生成に問題がある場合がある。
7. **サイズガイドの ID**: サイズガイドの取得は `GET /api/size-guides/{categoryId}`（カテゴリ ID）だが、更新は `PATCH /api/size-guides/{id}`（サイズガイド ID）である点に注意。

---

## 8. 変更履歴

| 日付 | バージョン | 変更内容 |
|------|-----------|---------|
| 2026-04-06 | 1.0 | 初版作成 |
| 2026-04-06 | 1.1 | OpenAPI エンドポイント追加、C-3 テスト追加、検索テスト拡充、403 テスト追加 |
