# API Gateway 統合検証計画

**作成日**: 2026-04-07  
**更新日**: 2026-04-07  
**目的**: Docker Compose 環境で API Gateway とバックエンドサービスの統合動作を検証

---

## 0. 事前準備（重要）

### 0.1 環境変数ファイルの作成

AiSupportService は Azure OpenAI / AI Search を使用するため、`.env` ファイルの作成が必要です：

```bash
# プロジェクトルートに .env ファイルを作成
cd /Users/yoterada/GitHub/DotNet-Skishop-App

cat > .env << 'EOF'
# JWT 共通設定
JWT_SECRET_KEY=dev-signing-key-minimum-32-characters-long

# Azure OpenAI（AI 機能を使用しない場合はダミー値でも可）
AZURE_OPENAI_ENDPOINT=https://your-openai.openai.azure.com/
AZURE_OPENAI_API_KEY=your-api-key
AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4
AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME=text-embedding-ada-002

# Azure AI Search（AI 機能を使用しない場合はダミー値でも可）
AZURE_AI_SEARCH_ENDPOINT=https://your-search.search.windows.net
AZURE_AI_SEARCH_API_KEY=your-search-api-key
EOF

echo "✅ .env ファイルを作成しました"
```

> **注意**: Azure OpenAI / AI Search を利用しない場合、AiSupportService の一部機能（チャット、検索）は動作しませんが、Gateway のルーティング検証は可能です。

### 0.2 データベース初期化スクリプトの確認

```bash
# 初期化 SQL が存在することを確認
ls -la infra/postgres/init-databases.sql

# 内容確認（9 データベースが作成されること）
head -30 infra/postgres/init-databases.sql
```

---

## 1. 検証環境の起動手順

### 1.1 前提条件

- Docker Desktop がインストール済み
- `docker compose` コマンドが利用可能
- ポート 8080, 5001-5009, 3000, 5432, 6379, 9092 が空いている

### 1.2 起動コマンド

```bash
# プロジェクトルートに移動
cd /Users/yoterada/GitHub/DotNet-Skishop-App

# 全サービスをビルド・起動（初回は時間がかかる）
docker compose up -d --build

# 起動状況の確認
docker compose ps

# ログの確認（全サービス）
docker compose logs -f

# API Gateway のログのみ確認
docker compose logs -f api-gateway
```

### 1.3 サービス一覧とポート

| サービス | コンテナ名 | ポート | ヘルスチェック |
|---------|-----------|--------|---------------|
| PostgreSQL | skishop-postgres | 5432 | `pg_isready` |
| Redis | skishop-redis | 6379 | `redis-cli ping` |
| Kafka | skishop-kafka | 9092 | broker-api-versions |
| AuthService | skishop-auth-service | 5001 | /health |
| UserManagementService | skishop-user-management-service | 5002 | /health |
| InventoryManagementService | skishop-inventory-management-service | 5003 | /health |
| SalesManagementService | skishop-sales-management-service | 5004 | /health |
| PaymentCartService | skishop-payment-cart-service | 5005 | /health |
| CouponService | skishop-coupon-service | 5006 | /health |
| PointService | skishop-point-service | 5007 | /health |
| MailSendService | skishop-mailsend-service | 5008 | /health |
| AiSupportService | skishop-ai-support-service | 5009 | /health |
| **API Gateway** | **skishop-api-gateway** | **8080** | **/health** |
| Kafka UI | skishop-kafka-ui | 8090 | - |

### 1.4 起動確認コマンド

```bash
# 全サービスが healthy になるまで待機
watch -n 5 'docker compose ps --format "table {{.Name}}\t{{.Status}}"'

# API Gateway のヘルスチェック
curl -s http://localhost:8080/health | jq

# 各バックエンドサービスのヘルスチェック（直接）
curl -s http://localhost:5001/health  # AuthService
curl -s http://localhost:5002/health  # UserManagementService
curl -s http://localhost:5003/health  # InventoryManagementService
curl -s http://localhost:5004/health  # SalesManagementService
curl -s http://localhost:5005/health  # PaymentCartService
curl -s http://localhost:5006/health  # CouponService
curl -s http://localhost:5007/health  # PointService
curl -s http://localhost:5008/health  # MailSendService
curl -s http://localhost:5009/health  # AiSupportService
```

---

## 2. 検証項目一覧

### 2.1 基本機能検証

| # | 検証項目 | 期待結果 | 優先度 |
|---|---------|---------|--------|
| B-1 | ヘルスチェック | 200 OK + JSON | 必須 |
| B-2 | OpenAPI ドキュメント | /openapi/v1.json が取得可能 | 必須 |
| B-3 | CORS ヘッダー | 適切な CORS ヘッダーが返される | 必須 |
| B-4 | X-Correlation-Id | レスポンスに相関 ID が含まれる | 必須 |

### 2.2 ルーティング検証（9 サービス × 代表エンドポイント）

| # | サービス | 検証エンドポイント | 認証 | 期待結果 |
|---|---------|------------------|------|---------|
| R-1 | AuthService | POST /api/v1/auth/login | 不要 | 400/401（認証エラー） |
| R-2 | AuthService | POST /api/v1/auth/users | 不要 | 400/201（ユーザー登録） |
| R-3 | UserManagement | GET /api/v1/users/me | 必要 | 401（未認証） |
| R-4 | UserManagement | GET /api/v1/admin/users | 必要(Admin) | 401/403 |
| R-5 | Inventory | GET /api/products | 不要 | 200 + 商品一覧 |
| R-6 | Inventory | GET /api/categories | 不要 | 200 + カテゴリ一覧 |
| R-7 | Inventory | GET /api/inventory/low-stock | 必要(Admin) | 401/403 |
| R-8 | Sales | GET /api/v1/orders | 必要 | 401（未認証） |
| R-9 | Sales | GET /api/v1/shipments | 必要 | 401（未認証） |
| R-10 | PaymentCart | GET /api/v1/cart/items | 不要 | 200/404 |
| R-11 | PaymentCart | POST /api/v1/payments/checkout | 必要 | 401（未認証） |
| R-12 | Coupon | GET /api/v1/coupons | 不要 | 200 + クーポン一覧 |
| R-13 | Coupon | POST /api/v1/coupons/validate | 必要 | 401（未認証）※認証必須ルート |
| R-14 | Point | GET /api/v1/points | 必要 | 401（未認証） |
| R-15 | Point | GET /api/v1/admin/points/adjustments | 必要(Admin) | 401/403 |
| R-16 | AiSupport | GET /api/v1/ai/recommendations/trending | 不要 | 200 + レコメンド |
| R-17 | AiSupport | POST /api/v1/ai/chat/sessions | 必要 | 401（未認証） |
| R-18 | MailSend | GET /admin/mail/templates | 必要(Admin) | 401/403 |
| R-19 | AiSupport | GET /api/v1/ai/search | 不要 | 200 + 検索結果 |

### 2.3 認証・認可検証

| # | 検証項目 | テスト方法 | 期待結果 |
|---|---------|----------|---------|
| A-1 | 未認証リクエスト（保護ルート） | Authorization ヘッダーなし | 401 Unauthorized |
| A-2 | 無効なトークン | 改ざんした JWT | 401 Unauthorized |
| A-3 | 期限切れトークン | 期限切れ JWT | 401 Unauthorized |
| A-4 | 有効なトークン | 正規の JWT | 200/ルーティング成功 |
| A-5 | 権限不足（Admin ルート） | User ロールの JWT | 403 Forbidden |
| A-6 | 管理者アクセス | Admin ロールの JWT | 200/ルーティング成功 |

### 2.4 レート制限検証

| # | ポリシー | 制限 | テスト方法 | 期待結果 |
|---|---------|------|----------|---------|
| RL-1 | login | 5 req/min/IP | 6 回連続 POST /api/v1/auth/login | 429 + Retry-After |
| RL-2 | checkout | 10 req/min/user | 11 回連続 POST /api/v1/cart/checkout | 429 + Retry-After |
| RL-3 | products | 60 req/min/IP | 61 回連続 GET /api/products | 429 + Retry-After |
| RL-4 | ai-api | 30 req/min/user | 31 回連続 GET /api/v1/ai/* | 429 + Retry-After |

### 2.5 耐障害性検証

| # | 検証項目 | テスト方法 | 期待結果 |
|---|---------|----------|---------|
| F-1 | バックエンド停止時 | 1 サービスを停止 | 502/503 + 相関 ID 保持 |
| F-2 | サーキットブレーカー | 連続失敗後の Open 状態 | 503 即座返却 |
| F-3 | タイムアウト | 遅延バックエンドシミュレート | 504 Gateway Timeout |

---

## 3. 詳細検証手順

### 3.1 基本機能検証

#### B-1: ヘルスチェック

```bash
# Gateway ヘルスチェック
curl -v http://localhost:8080/health

# 期待結果:
# HTTP/1.1 200 OK
# Content-Type: application/json
# {"status":"Healthy","results":{...}}

# バックエンドサービスの健全性確認
curl -s http://localhost:8080/health/ready | jq
```

#### B-2: OpenAPI ドキュメント

```bash
# OpenAPI ドキュメント取得
curl -s http://localhost:8080/openapi/v1.json | jq '.info'

# 期待結果:
# {
#   "title": "ApiGateway",
#   "version": "1.0"
# }
```

#### B-3: CORS ヘッダー

```bash
# OPTIONS プリフライトリクエスト
curl -v -X OPTIONS http://localhost:8080/api/products \
  -H "Origin: http://localhost:3000" \
  -H "Access-Control-Request-Method: GET"

# 期待結果:
# Access-Control-Allow-Origin: http://localhost:3000
# Access-Control-Allow-Methods: GET, POST, PUT, DELETE, PATCH, OPTIONS
# Access-Control-Allow-Headers: ...
```

#### B-4: X-Correlation-Id

```bash
# 相関 ID の確認
curl -v http://localhost:8080/api/products 2>&1 | grep -i x-correlation-id

# 期待結果:
# x-correlation-id: <GUID>

# カスタム相関 ID の伝搬
curl -v http://localhost:8080/api/products \
  -H "X-Correlation-Id: test-correlation-123" 2>&1 | grep -i x-correlation-id

# 期待結果:
# x-correlation-id: test-correlation-123
```

---

### 3.2 ルーティング検証

#### R-1: AuthService - ログイン（認証不要）

```bash
# ログインエンドポイント（無効な認証情報）
curl -s -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"wrongpassword"}' | jq

# 期待結果: 401 Unauthorized または 400 Bad Request
```

#### R-2: AuthService - ユーザー登録

```bash
# ユーザー登録
curl -s -X POST http://localhost:8080/api/v1/auth/users \
  -H "Content-Type: application/json" \
  -d '{
    "email":"newuser@example.com",
    "password":"Password123!",
    "confirmPassword":"Password123!",
    "firstName":"Test",
    "lastName":"User"
  }' | jq

# 期待結果: 201 Created または 400 (既存ユーザー)
```

#### R-3〜R-4: UserManagementService

```bash
# プロフィール取得（認証必要）
curl -s http://localhost:8080/api/v1/users/me | jq
# 期待結果: 401 Unauthorized

# 管理者ユーザー一覧（Admin 必要）
curl -s http://localhost:8080/api/v1/admin/users | jq
# 期待結果: 401 Unauthorized
```

#### R-5〜R-7: InventoryManagementService

```bash
# 商品一覧（認証不要）
curl -s http://localhost:8080/api/products | jq

# カテゴリ一覧（認証不要）
curl -s http://localhost:8080/api/categories | jq

# 低在庫アラート（Admin 必要）
curl -s http://localhost:8080/api/inventory/low-stock | jq
# 期待結果: 401 Unauthorized
```

#### R-8〜R-9: SalesManagementService

```bash
# 注文一覧（認証必要）
curl -s http://localhost:8080/api/v1/orders | jq
# 期待結果: 401 Unauthorized

# 配送一覧（認証必要）
curl -s http://localhost:8080/api/v1/shipments | jq
# 期待結果: 401 Unauthorized
```

#### R-10〜R-11: PaymentCartService

```bash
# カートアイテム取得（ゲスト可）
curl -s http://localhost:8080/api/v1/cart/items \
  -H "Cookie: CartId=test-cart-123" | jq

# 決済チェックアウト（認証必要）
curl -s -X POST http://localhost:8080/api/v1/payments/checkout \
  -H "Content-Type: application/json" \
  -d '{}' | jq
# 期待結果: 401 Unauthorized
```

#### R-12〜R-13: CouponService

```bash
# R-12: クーポン一覧（認証不要）
curl -s http://localhost:8080/api/v1/coupons | jq

# R-13: クーポン検証（認証必要）
curl -s -X POST http://localhost:8080/api/v1/coupons/validate \
  -H "Content-Type: application/json" \
  -d '{"code":"TESTCODE"}' | jq
# 期待結果: 401 Unauthorized（認証必須）
```

#### R-14〜R-15: PointService

```bash
# ポイント残高（認証必要）
curl -s http://localhost:8080/api/v1/points | jq
# 期待結果: 401 Unauthorized

# ポイント調整一覧（Admin 必要）
curl -s http://localhost:8080/api/v1/admin/points/adjustments | jq
# 期待結果: 401 Unauthorized
```

#### R-16〜R-19: AiSupportService

```bash
# R-16: トレンド商品（認証不要）
curl -s http://localhost:8080/api/v1/ai/recommendations/trending | jq

# R-19: 商品検索（認証不要）
curl -s "http://localhost:8080/api/v1/ai/search?q=ski" | jq

# R-17: AI チャットセッション作成（認証必要）
curl -s -X POST http://localhost:8080/api/v1/ai/chat/sessions \
  -H "Content-Type: application/json" \
  -d '{}' | jq
# 期待結果: 401 Unauthorized
```

#### R-18: MailSendService

```bash
# メールテンプレート一覧（Admin 必要）
curl -s http://localhost:8080/admin/mail/templates | jq
# 期待結果: 401 Unauthorized
```

---

### 3.3 認証・認可検証

#### JWT トークン取得（テスト用）

```bash
# Step 1: ユーザー登録（初回のみ）
curl -s -X POST http://localhost:8080/api/v1/auth/users \
  -H "Content-Type: application/json" \
  -d '{
    "email":"testuser@example.com",
    "password":"TestPassword123!",
    "confirmPassword":"TestPassword123!",
    "firstName":"Test",
    "lastName":"User"
  }'

# Step 2: ログインしてトークン取得
TOKEN=$(curl -s -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email":"testuser@example.com",
    "password":"TestPassword123!"
  }' | jq -r '.token')

echo "Token: $TOKEN"
```

#### A-1〜A-4: 認証検証

```bash
# A-1: 未認証リクエスト
curl -s http://localhost:8080/api/v1/users/me | jq
# 期待結果: 401

# A-2: 無効なトークン
curl -s http://localhost:8080/api/v1/users/me \
  -H "Authorization: Bearer invalid.token.here" | jq
# 期待結果: 401

# A-4: 有効なトークン
curl -s http://localhost:8080/api/v1/users/me \
  -H "Authorization: Bearer $TOKEN" | jq
# 期待結果: 200 + ユーザー情報
```

#### A-5〜A-6: 認可検証

```bash
# A-5: 一般ユーザーで Admin ルートへアクセス
curl -s http://localhost:8080/api/v1/admin/users \
  -H "Authorization: Bearer $TOKEN" | jq
# 期待結果: 403 Forbidden

# A-6: Admin ユーザーでアクセス（Admin トークンが必要）
# 管理者トークンを使用
ADMIN_TOKEN="<admin_token_here>"
curl -s http://localhost:8080/api/v1/admin/users \
  -H "Authorization: Bearer $ADMIN_TOKEN" | jq
# 期待結果: 200 + ユーザー一覧
```

---

### 3.4 レート制限検証

#### RL-1: ログインレート制限（5 req/min/IP）

```bash
# 6 回連続でログインリクエスト
for i in {1..6}; do
  echo "Request $i:"
  curl -s -o /dev/null -w "Status: %{http_code}\n" \
    -X POST http://localhost:8080/api/v1/auth/login \
    -H "Content-Type: application/json" \
    -d '{"email":"test@example.com","password":"test"}'
done

# 期待結果:
# Request 1-5: Status: 400 または 401
# Request 6: Status: 429
```

#### RL-1 の詳細確認

```bash
# 429 レスポンスの詳細確認
curl -v -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"test"}'

# 期待結果:
# HTTP/1.1 429 Too Many Requests
# Retry-After: <seconds>
```

#### RL-3: 商品一覧レート制限（60 req/min/IP）

```bash
# 61 回連続でリクエスト（約 1 分以内）
for i in {1..61}; do
  STATUS=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:8080/api/products)
  if [ "$STATUS" = "429" ]; then
    echo "Rate limited at request $i"
    break
  fi
  echo "Request $i: $STATUS"
done
```

---

### 3.5 耐障害性検証

#### F-1: バックエンド停止時の動作

```bash
# Step 1: 在庫サービスを停止
docker compose stop inventory-management-service

# Step 2: 商品一覧にアクセス
curl -v http://localhost:8080/api/products

# 期待結果:
# HTTP/1.1 502 Bad Gateway または 503 Service Unavailable
# x-correlation-id: <GUID> が保持されている

# Step 3: サービスを再起動
docker compose start inventory-management-service

# Step 4: 復旧確認
sleep 30
curl -s http://localhost:8080/api/products | jq
```

#### F-2: サーキットブレーカー検証

```bash
# Step 1: 在庫サービスを停止
docker compose stop inventory-management-service

# Step 2: 連続リクエストでサーキットブレーカーを開く
for i in {1..10}; do
  curl -s -o /dev/null -w "Request $i: %{http_code}\n" \
    http://localhost:8080/api/products
  sleep 0.5
done

# 期待結果: 最初の数リクエストは 502、その後は即座に 503

# Step 3: 復旧
docker compose start inventory-management-service
```

---

## 4. 検証シナリオスクリプト

### 4.1 全自動検証スクリプト

```bash
#!/bin/bash
# verify-gateway.sh - API Gateway 検証スクリプト

set -e

GATEWAY_URL="http://localhost:8080"
PASS=0
FAIL=0

# ヘルパー関数
check() {
  local name="$1"
  local expected="$2"
  local actual="$3"
  
  if [[ "$actual" == *"$expected"* ]]; then
    echo "✅ PASS: $name"
    ((PASS++))
  else
    echo "❌ FAIL: $name (expected: $expected, got: $actual)"
    ((FAIL++))
  fi
}

echo "=== API Gateway 検証開始 ==="
echo ""

# B-1: ヘルスチェック
echo "--- 基本機能検証 ---"
STATUS=$(curl -s -o /dev/null -w "%{http_code}" $GATEWAY_URL/health)
check "B-1: ヘルスチェック" "200" "$STATUS"

# B-4: 相関 ID
CORR_ID=$(curl -s -I $GATEWAY_URL/api/products | grep -i x-correlation-id)
check "B-4: X-Correlation-Id" "x-correlation-id" "$CORR_ID"

# R-5: 商品一覧（認証不要）
echo ""
echo "--- ルーティング検証（認証不要） ---"
STATUS=$(curl -s -o /dev/null -w "%{http_code}" $GATEWAY_URL/api/products)
check "R-5: 商品一覧" "200" "$STATUS"

# R-6: カテゴリ一覧（認証不要）
STATUS=$(curl -s -o /dev/null -w "%{http_code}" $GATEWAY_URL/api/categories)
check "R-6: カテゴリ一覧" "200" "$STATUS"

# R-12: クーポン一覧（認証不要）
STATUS=$(curl -s -o /dev/null -w "%{http_code}" $GATEWAY_URL/api/v1/coupons)
check "R-12: クーポン一覧" "200" "$STATUS"

# R-16: AI レコメンド（認証不要）
STATUS=$(curl -s -o /dev/null -w "%{http_code}" $GATEWAY_URL/api/v1/ai/recommendations/trending)
check "R-16: AI レコメンド" "200" "$STATUS"

# R-19: AI 検索（認証不要）
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$GATEWAY_URL/api/v1/ai/search?q=ski")
check "R-19: AI 検索" "200" "$STATUS"

# A-1: 未認証リクエスト
echo ""
echo "--- 認証・認可検証 ---"
STATUS=$(curl -s -o /dev/null -w "%{http_code}" $GATEWAY_URL/api/v1/users/me)
check "A-1: 未認証リクエスト" "401" "$STATUS"

# R-3: ユーザープロフィール（認証必要）
STATUS=$(curl -s -o /dev/null -w "%{http_code}" $GATEWAY_URL/api/v1/users/me)
check "R-3: ユーザープロフィール (未認証)" "401" "$STATUS"

# R-7: 低在庫アラート（Admin 必要）
STATUS=$(curl -s -o /dev/null -w "%{http_code}" $GATEWAY_URL/api/inventory/low-stock)
check "R-7: 低在庫アラート (未認証)" "401" "$STATUS"

# R-13: クーポン検証（認証必要）
STATUS=$(curl -s -o /dev/null -w "%{http_code}" -X POST $GATEWAY_URL/api/v1/coupons/validate -H "Content-Type: application/json" -d '{}')
check "R-13: クーポン検証 (未認証)" "401" "$STATUS"

# R-17: AI チャット（認証必要）
STATUS=$(curl -s -o /dev/null -w "%{http_code}" -X POST $GATEWAY_URL/api/v1/ai/chat/sessions -H "Content-Type: application/json" -d '{}')
check "R-17: AI チャット (未認証)" "401" "$STATUS"

# R-18: メールテンプレート（Admin 必要）
STATUS=$(curl -s -o /dev/null -w "%{http_code}" $GATEWAY_URL/admin/mail/templates)
check "R-18: メールテンプレート (未認証)" "401" "$STATUS"

echo ""
echo "=== 検証結果 ==="
echo "PASS: $PASS"
echo "FAIL: $FAIL"
echo ""

if [ $FAIL -eq 0 ]; then
  echo "✅ 全検証項目が成功しました"
  exit 0
else
  echo "❌ $FAIL 件の検証が失敗しました"
  exit 1
fi
```

### 4.2 スクリプトの実行

```bash
# スクリプトを保存
chmod +x verify-gateway.sh

# 実行
./verify-gateway.sh
```

---

## 5. トラブルシューティング

### 5.1 よくある問題と対処法

| 問題 | 原因 | 対処法 |
|------|------|--------|
| 502 Bad Gateway | バックエンドサービスが起動していない | `docker compose ps` で状態確認、`docker compose up -d` で再起動 |
| 401 Unauthorized（想定外） | JWT 設定の不整合 | 環境変数 `Jwt__SigningKey` を確認 |
| 404 Not Found | ルートが見つからない | `appsettings.json` のルート定義を確認 |
| 503 Service Unavailable | サーキットブレーカー Open | しばらく待つ（自動回復） |
| Connection refused | Docker ネットワーク問題 | `docker network inspect skishop-network` |

### 5.2 ログ確認コマンド

```bash
# API Gateway のログ
docker compose logs -f api-gateway

# 特定のエラーを検索
docker compose logs api-gateway 2>&1 | grep -i error

# 全サービスの起動状況
docker compose ps -a

# 特定サービスの再起動
docker compose restart api-gateway
```

### 5.3 デバッグモード

```bash
# 環境変数を変更して詳細ログを有効化
docker compose down
ASPNETCORE_ENVIRONMENT=Development docker compose up -d api-gateway

# Serilog の詳細ログを確認
docker compose logs -f api-gateway | grep -E "(Request|Response|Error)"
```

---

## 6. 検証チェックリスト

検証完了時に以下をチェック：

### 基本機能
- [ ] B-1: ヘルスチェック 200 OK
- [ ] B-2: OpenAPI ドキュメント取得可能
- [ ] B-3: CORS ヘッダー正常
- [ ] B-4: X-Correlation-Id 含まれる

### ルーティング（9 サービス × 19 エンドポイント）
- [ ] R-1: AuthService POST /api/v1/auth/login
- [ ] R-2: AuthService POST /api/v1/auth/users
- [ ] R-3: UserManagement GET /api/v1/users/me → 401
- [ ] R-4: UserManagement GET /api/v1/admin/users → 401
- [ ] R-5: Inventory GET /api/products → 200
- [ ] R-6: Inventory GET /api/categories → 200
- [ ] R-7: Inventory GET /api/inventory/low-stock → 401
- [ ] R-8: Sales GET /api/v1/orders → 401
- [ ] R-9: Sales GET /api/v1/shipments → 401
- [ ] R-10: PaymentCart GET /api/v1/cart/items
- [ ] R-11: PaymentCart POST /api/v1/payments/checkout → 401
- [ ] R-12: Coupon GET /api/v1/coupons → 200
- [ ] R-13: Coupon POST /api/v1/coupons/validate → 401
- [ ] R-14: Point GET /api/v1/points → 401
- [ ] R-15: Point GET /api/v1/admin/points/adjustments → 401
- [ ] R-16: AiSupport GET /api/v1/ai/recommendations/trending → 200
- [ ] R-17: AiSupport POST /api/v1/ai/chat/sessions → 401
- [ ] R-18: MailSend GET /admin/mail/templates → 401
- [ ] R-19: AiSupport GET /api/v1/ai/search → 200

### 認証・認可
- [ ] A-1: 未認証リクエスト → 401
- [ ] A-2: 無効トークン → 401
- [ ] A-4: 有効トークン → ルーティング成功
- [ ] A-5: 権限不足 → 403
- [ ] A-6: Admin アクセス → 成功

### レート制限
- [ ] RL-1: login ポリシー（5 req/min）動作
- [ ] RL-3: products ポリシー（60 req/min）動作

### 耐障害性
- [ ] F-1: バックエンド停止時 502/503
- [ ] F-2: サーキットブレーカー動作

---

## 7. 環境クリーンアップ

```bash
# サービス停止
docker compose down

# ボリュームも含めて完全削除
docker compose down -v

# イメージも削除（ディスク容量確保）
docker compose down -v --rmi all
```

---

**検証計画終了**
