# CouponService 検証計画書（verification-plan.md）

本ドキュメントは CouponService の全エンドポイントに対する動作検証の手順とフォーマットを定義する。
検証結果は `verification-report.md` に記録し、CouponService の使い方を理解するためのリファレンスとして活用する。

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

1. **機能確認**: 全 20 エンドポイント（+ 2 ヘルスチェック + 1 OpenAPI ドキュメント）が設計通りに動作することを確認する
2. **使い方の文書化**: 各 API の呼び出し方法・パラメータ・レスポンスを実例で示す
3. **異常系の確認**: バリデーションエラー、認証エラー、認可エラー、ビジネスルール違反等の動作を確認する
4. **セキュリティ検証**: 認証必須エンドポイントのアクセス制御、管理者専用エンドポイントの権限検証、内部サービス専用エンドポイントの保護、レート制限の動作を確認する

---

## 2. 前提条件

### 2.1 環境構成

| 項目 | 値 |
|------|-----|
| CouponService URL | `http://localhost:5006` |
| AuthService URL | `http://localhost:5001` |
| PostgreSQL | `localhost:5432`（DB: `coupondb`） |
| Redis | `localhost:6379` |
| Kafka | `localhost:9092`（内部: `kafka:29092`） |

### 2.2 起動コマンド

```bash
# Docker Compose で CouponService と依存サービスを起動
cd /Users/yoterada/GitHub/DotNet-Skishop-App
docker compose up -d auth-service coupon-service
```

> **📝 注意**: CouponService は AuthService が発行した JWT トークンを使用して認証するため、AuthService も同時に起動する必要がある。また、PostgreSQL、Redis、Kafka のインフラサービスが事前に起動済みであること。

### 2.3 事前確認コマンド

```bash
# CouponService ヘルスチェック
curl -s http://localhost:5006/health
# 期待結果: Healthy

# CouponService Readiness チェック
curl -s http://localhost:5006/health/ready
# 期待結果: Healthy

# AuthService ヘルスチェック
curl -s http://localhost:5001/health
# 期待結果: Healthy
```

### 2.4 テスト用トークン取得手順

検証開始前に AuthService でユーザーを登録し、JWT トークンを取得する。

#### 一般ユーザートークン取得

```bash
# 1. ユーザー登録（既に登録済みの場合はスキップ）
curl -s -X POST http://localhost:5001/api/v1/auth/users \
  -H "Content-Type: application/json" \
  -d '{
    "email": "coupontest@example.com",
    "password": "SecurePass123!",
    "firstName": "Coupon",
    "lastName": "Tester",
    "phoneNumber": "090-1234-5678"
  }'
```

```bash
# 2. ユーザーステータスを ACTIVE に変更（DB 直接操作）
docker exec -it skishop-postgres psql -U skishop -d authdb \
  -c "UPDATE users SET status = 'ACTIVE', email_verified = true WHERE email = 'coupontest@example.com';"
```

```bash
# 3. ログインしてトークン取得
LOGIN_RESPONSE=$(curl -s -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email": "coupontest@example.com", "password": "SecurePass123!"}')

# レスポンスからトークンとユーザーIDを抽出
ACCESS_TOKEN=$(echo "$LOGIN_RESPONSE" | python3 -c "import sys, json; print(json.load(sys.stdin)['accessToken'])")
USER_ID=$(echo "$LOGIN_RESPONSE" | python3 -c "import sys, json; print(json.load(sys.stdin)['user']['id'])")

echo "ACCESS_TOKEN=${ACCESS_TOKEN}"
echo "USER_ID=${USER_ID}"
```

#### 管理者トークン取得

```bash
# 1. 管理者ユーザー登録（既に登録済みの場合はスキップ）
curl -s -X POST http://localhost:5001/api/v1/auth/users \
  -H "Content-Type: application/json" \
  -d '{
    "email": "couponadmin@example.com",
    "password": "AdminPass123!",
    "firstName": "Coupon",
    "lastName": "Admin",
    "phoneNumber": "090-9876-5432"
  }'
```

```bash
# 2. 管理者ステータスとロール設定
docker exec -it skishop-postgres psql -U skishop -d authdb \
  -c "UPDATE users SET status = 'ACTIVE', email_verified = true, role = 'Admin' WHERE email = 'couponadmin@example.com';"
```

```bash
# 3. 管理者ログイン
ADMIN_RESPONSE=$(curl -s -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email": "couponadmin@example.com", "password": "AdminPass123!"}')

ADMIN_TOKEN=$(echo "$ADMIN_RESPONSE" | python3 -c "import sys, json; print(json.load(sys.stdin)['accessToken'])")
ADMIN_USER_ID=$(echo "$ADMIN_RESPONSE" | python3 -c "import sys, json; print(json.load(sys.stdin)['user']['id'])")

echo "ADMIN_TOKEN=${ADMIN_TOKEN}"
echo "ADMIN_USER_ID=${ADMIN_USER_ID}"
```

### 2.5 テスト用マスターデータの準備

CouponService のクーポン作成には `CouponType`（クーポンタイプ）が必要。テスト開始前に DB に直接挿入する。

```bash
# CouponType マスターデータの挿入
docker exec -it skishop-postgres psql -U skishop -d coupondb -c "
INSERT INTO coupon_types (id, name, description, usage_limitation_type, created_at, updated_at)
VALUES
  ('type-fixed-001', '固定額割引', '注文金額から固定額を割引', 1, NOW(), NOW()),
  ('type-percent-001', 'パーセント割引', '注文金額から指定%を割引', 1, NOW(), NOW()),
  ('type-shipping-001', '送料無料', '送料を無料にする', 0, NOW(), NOW())
ON CONFLICT (id) DO NOTHING;
"
```

---

## 3. エンドポイント一覧

### 3.1 ヘルスチェック

| メソッド | パス | 説明 | 認証 |
|---------|------|------|------|
| GET | `/health` | Liveness チェック | 不要 |
| GET | `/health/ready` | Readiness チェック（PostgreSQL, Redis, Kafka 疎通） | 不要 |

### 3.2 一般ユーザー向け — クーポン（CouponEndpoints）

| メソッド | パス | 説明 | 認証 | レート制限 |
|---------|------|------|------|----------|
| GET | `/api/v1/coupons/available` | 利用可能クーポン一覧取得 | UserOrAdmin | coupon-api (100/min) |
| GET | `/api/v1/coupons/mine` | 自分の保有クーポン一覧取得 | 認証必須 | coupon-api (100/min) |
| POST | `/api/v1/coupons/{code}/acquire` | クーポンの取得（アクワイア） | 認証必須 | coupon-api (100/min) |
| POST | `/api/v1/coupons/validate` | クーポンの検証・割引計算 | 認証必須 | coupon-api (100/min) |

### 3.3 管理者向け — クーポン管理（AdminCouponEndpoints）

| メソッド | パス | 説明 | 認証 | レート制限 |
|---------|------|------|------|----------|
| GET | `/api/v1/admin/coupons` | クーポン一覧取得（全件） | AdminOnly | coupon-api (100/min) |
| POST | `/api/v1/admin/coupons` | クーポン作成 | AdminOnly | coupon-api (100/min) |
| GET | `/api/v1/admin/coupons/{id}` | クーポン詳細取得 | AdminOnly | coupon-api (100/min) |
| PUT | `/api/v1/admin/coupons/{id}` | クーポン更新 | AdminOnly | coupon-api (100/min) |
| DELETE | `/api/v1/admin/coupons/{id}` | クーポン無効化（論理削除） | AdminOnly | coupon-api (100/min) |
| GET | `/api/v1/admin/coupons/{id}/usages` | クーポン利用履歴取得 | AdminOnly | coupon-api (100/min) |
| GET | `/api/v1/admin/coupons/analytics` | クーポン分析データ取得 | AdminOnly | coupon-api (100/min) |

### 3.4 管理者向け — キャンペーン管理（CampaignEndpoints）

| メソッド | パス | 説明 | 認証 | レート制限 |
|---------|------|------|------|----------|
| GET | `/api/v1/admin/campaigns` | キャンペーン一覧取得 | AdminOnly | coupon-api (100/min) |
| POST | `/api/v1/admin/campaigns` | キャンペーン作成 | AdminOnly | coupon-api (100/min) |
| GET | `/api/v1/admin/campaigns/{id}` | キャンペーン詳細取得 | AdminOnly | coupon-api (100/min) |
| PUT | `/api/v1/admin/campaigns/{id}` | キャンペーン更新 | AdminOnly | coupon-api (100/min) |
| POST | `/api/v1/admin/campaigns/{id}/activate` | キャンペーン有効化 | AdminOnly | coupon-api (100/min) |
| POST | `/api/v1/admin/campaigns/{id}/pause` | キャンペーン一時停止 | AdminOnly | coupon-api (100/min) |

### 3.5 内部サービス向け — クーポン操作（InternalCouponEndpoints）

| メソッド | パス | 説明 | 認証 | レート制限 |
|---------|------|------|------|----------|
| POST | `/api/v1/internal/coupons/calculate` | 割引金額計算 | InternalServiceOnly | redeem-api (10/min) |
| POST | `/api/v1/internal/coupons/redeem` | クーポン利用（消費） | InternalServiceOnly | redeem-api (10/min) |
| POST | `/api/v1/internal/coupons/release` | クーポンリリース（Saga 補償） | InternalServiceOnly | redeem-api (10/min) |

---

## 4. 検証手順

検証は以下の Phase 順序で実施する。依存関係を考慮した順番となっている。

---

### Phase 0: ヘルスチェック

#### テスト 0-1: GET /health（Liveness）

```bash
curl -s http://localhost:5006/health
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
curl -s http://localhost:5006/health/ready
```

**期待するレスポンス（200 OK）:**

```
Healthy
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] PostgreSQL, Redis, Kafka の疎通が確認できている

#### テスト 0-3: GET /openapi/v1.json（OpenAPI ドキュメント）

```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:5006/openapi/v1.json
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] JSON 形式の OpenAPI ドキュメントが返る
- [ ] `paths` に全エンドポイントが含まれる

---

### Phase 1: 管理者 — キャンペーン管理

> **📝 前提**: `ADMIN_TOKEN` を環境変数に設定済み。キャンペーンは後続のクーポン作成で使用するため、最初に作成する。

#### テスト 1-1: POST /api/v1/admin/campaigns（キャンペーン作成）

```bash
CAMPAIGN_RESPONSE=$(curl -s -X POST http://localhost:5006/api/v1/admin/campaigns \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "2026年春のスキーセール",
    "description": "春シーズン終了に伴う在庫一掃セール",
    "startDate": "2026-04-01T00:00:00+09:00",
    "endDate": "2026-05-31T23:59:59+09:00",
    "maxCoupons": 1000
  }')
echo "$CAMPAIGN_RESPONSE" | python3 -m json.tool

CAMPAIGN_ID=$(echo "$CAMPAIGN_RESPONSE" | python3 -c "import sys, json; print(json.load(sys.stdin)['id'])")
echo "CAMPAIGN_ID=${CAMPAIGN_ID}"
```

**期待するレスポンス（201 Created）:**

```json
{
    "id": "uuid-xxx",
    "name": "2026年春のスキーセール",
    "description": "春シーズン終了に伴う在庫一掃セール",
    "status": 0,
    "startDate": "2026-04-01T00:00:00+09:00",
    "endDate": "2026-05-31T23:59:59+09:00",
    "maxCoupons": 1000,
    "issuedCount": 0,
    "createdAt": "2026-04-07T..."
}
```

**検証項目:**
- [ ] ステータスコード 201 を返す
- [ ] `id` が UUID 形式で自動生成される
- [ ] `status` が `0`（Draft）である
- [ ] `issuedCount` が `0` である
- [ ] `Location` ヘッダーに作成リソースの URL が含まれる

#### テスト 1-2: POST /api/v1/admin/campaigns（バリデーションエラー — 名前なし）

```bash
curl -s -X POST http://localhost:5006/api/v1/admin/campaigns \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "",
    "startDate": "2026-04-01T00:00:00+09:00",
    "endDate": "2026-03-01T00:00:00+09:00",
    "maxCoupons": 0
  }'
```

**期待するレスポンス（400 Bad Request）:**

```json
{
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    "title": "One or more validation errors occurred.",
    "status": 400,
    "errors": {
        "Name": ["キャンペーン名は必須です"],
        "EndDate": ["終了日は開始日より後を指定してください"],
        "MaxCoupons": ["最大発行数は1以上を指定してください"]
    }
}
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] 各フィールドのバリデーションエラーメッセージが返る

#### テスト 1-3: POST /api/v1/admin/campaigns（認証なしアクセス）

```bash
curl -s -o /dev/null -w "%{http_code}" -X POST http://localhost:5006/api/v1/admin/campaigns \
  -H "Content-Type: application/json" \
  -d '{"name": "test", "startDate": "2026-04-01T00:00:00Z", "endDate": "2026-05-01T00:00:00Z", "maxCoupons": 100}'
```

**期待するレスポンス**: 401 Unauthorized

**検証項目:**
- [ ] ステータスコード 401 を返す

#### テスト 1-4: POST /api/v1/admin/campaigns（一般ユーザーによるアクセス拒否）

```bash
curl -s -o /dev/null -w "%{http_code}" -X POST http://localhost:5006/api/v1/admin/campaigns \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"name": "test", "startDate": "2026-04-01T00:00:00Z", "endDate": "2026-05-01T00:00:00Z", "maxCoupons": 100}'
```

**期待するレスポンス**: 403 Forbidden

**検証項目:**
- [ ] ステータスコード 403 を返す
- [ ] AdminOnly ポリシーが正しく動作している

#### テスト 1-5: GET /api/v1/admin/campaigns（キャンペーン一覧取得）

```bash
curl -s -X GET "http://localhost:5006/api/v1/admin/campaigns?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
    "items": [
        {
            "id": "uuid-xxx",
            "name": "2026年春のスキーセール",
            "description": "春シーズン終了に伴う在庫一掃セール",
            "status": 0,
            "startDate": "...",
            "endDate": "...",
            "maxCoupons": 1000,
            "issuedCount": 0,
            "createdAt": "..."
        }
    ],
    "totalCount": 1,
    "page": 1,
    "pageSize": 10,
    "totalPages": 1
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] ページネーション情報（`totalCount`, `page`, `pageSize`, `totalPages`）が含まれる
- [ ] 作成済みキャンペーンが一覧に表示される

#### テスト 1-6: GET /api/v1/admin/campaigns/{id}（キャンペーン詳細取得）

```bash
curl -s -X GET "http://localhost:5006/api/v1/admin/campaigns/${CAMPAIGN_ID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
    "id": "uuid-xxx",
    "name": "2026年春のスキーセール",
    "description": "春シーズン終了に伴う在庫一掃セール",
    "status": 0,
    "startDate": "...",
    "endDate": "...",
    "maxCoupons": 1000,
    "issuedCount": 0,
    "createdAt": "..."
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 指定した ID のキャンペーン情報が返る

#### テスト 1-7: GET /api/v1/admin/campaigns/{id}（存在しない ID — 404）

```bash
curl -s -o /dev/null -w "%{http_code}" -X GET \
  "http://localhost:5006/api/v1/admin/campaigns/non-existent-id" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス**: 404 Not Found

**検証項目:**
- [ ] ステータスコード 404 を返す

#### テスト 1-8: PUT /api/v1/admin/campaigns/{id}（キャンペーン更新）

```bash
curl -s -X PUT "http://localhost:5006/api/v1/admin/campaigns/${CAMPAIGN_ID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "2026年春のスキーセール（延長）",
    "endDate": "2026-06-30T23:59:59+09:00",
    "maxCoupons": 1500
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
    "id": "uuid-xxx",
    "name": "2026年春のスキーセール（延長）",
    "description": "春シーズン終了に伴う在庫一掃セール",
    "status": 0,
    "startDate": "...",
    "endDate": "2026-06-30T23:59:59+09:00",
    "maxCoupons": 1500,
    "issuedCount": 0,
    "createdAt": "..."
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 更新したフィールド（`name`, `endDate`, `maxCoupons`）が反映される
- [ ] 更新しなかったフィールド（`description`）が保持される

#### テスト 1-9: PUT /api/v1/admin/campaigns/{id}（存在しない ID — 404）

```bash
curl -s -o /dev/null -w "%{http_code}" -X PUT \
  "http://localhost:5006/api/v1/admin/campaigns/non-existent-campaign-id" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"name": "dummy"}'
```

**期待するレスポンス**: 404 Not Found

**検証項目:**
- [ ] ステータスコード 404 を返す

#### テスト 1-10: PUT /api/v1/admin/campaigns/{id}（バリデーションエラー — 空名前）

```bash
curl -s -X PUT "http://localhost:5006/api/v1/admin/campaigns/${CAMPAIGN_ID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"name": ""}'
```

**期待するレスポンス（400 Bad Request）:**

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] バリデーションエラーメッセージが返る

#### テスト 1-11: POST /api/v1/admin/campaigns/{id}/activate（キャンペーン有効化）

```bash
curl -s -o /dev/null -w "%{http_code}" -X POST \
  "http://localhost:5006/api/v1/admin/campaigns/${CAMPAIGN_ID}/activate" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス**: 204 No Content

**検証項目:**
- [ ] ステータスコード 204 を返す

**確認（ステータスが Active に変わったことを確認）:**

```bash
curl -s -X GET "http://localhost:5006/api/v1/admin/campaigns/${CAMPAIGN_ID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" | python3 -c "import sys, json; print('status:', json.load(sys.stdin)['status'])"
# 期待結果: status: 1 (Active)
```

#### テスト 1-12: POST /api/v1/admin/campaigns/{id}/activate（存在しない ID — 404）

```bash
curl -s -o /dev/null -w "%{http_code}" -X POST \
  "http://localhost:5006/api/v1/admin/campaigns/non-existent-id/activate" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス**: 404 Not Found

**検証項目:**
- [ ] ステータスコード 404 を返す

#### テスト 1-13: POST /api/v1/admin/campaigns/{id}/activate（既に Active のキャンペーンを再 activate — 422）

> **📝 注意**: テスト 1-11 でキャンペーンを Active にした直後に実行する。

```bash
curl -s -o /dev/null -w "%{http_code}" -X POST \
  "http://localhost:5006/api/v1/admin/campaigns/${CAMPAIGN_ID}/activate" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス**: 422 Unprocessable Entity または 204 No Content（冪等実装の場合）

**検証項目:**
- [ ] ステータスコード 422 を返す（ビジネスルール違反）、または 204（冪等性により正常終了）
- [ ] 実際の動作を確認し記録する

#### テスト 1-14: POST /api/v1/admin/campaigns/{id}/pause（キャンペーン一時停止）

```bash
curl -s -o /dev/null -w "%{http_code}" -X POST \
  "http://localhost:5006/api/v1/admin/campaigns/${CAMPAIGN_ID}/pause" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス**: 204 No Content

**検証項目:**
- [ ] ステータスコード 204 を返す

**確認（ステータスが Paused に変わったことを確認）:**

```bash
curl -s -X GET "http://localhost:5006/api/v1/admin/campaigns/${CAMPAIGN_ID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" | python3 -c "import sys, json; print('status:', json.load(sys.stdin)['status'])"
# 期待結果: status: 2 (Paused)
```

#### テスト 1-15: POST /api/v1/admin/campaigns/{id}/pause（存在しない ID — 404）

```bash
curl -s -o /dev/null -w "%{http_code}" -X POST \
  "http://localhost:5006/api/v1/admin/campaigns/non-existent-id/pause" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス**: 404 Not Found

**検証項目:**
- [ ] ステータスコード 404 を返す

> **📝 注意**: 後続テストのためにキャンペーンを Active に戻しておく。

```bash
curl -s -o /dev/null -w "%{http_code}" -X POST \
  "http://localhost:5006/api/v1/admin/campaigns/${CAMPAIGN_ID}/activate" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

---

### Phase 2: 管理者 — クーポン管理

> **📝 前提**: `ADMIN_TOKEN` を環境変数に設定済み。Phase 1 で `CAMPAIGN_ID` を取得済み。CouponType マスターデータが投入済み。

#### テスト 2-1: POST /api/v1/admin/coupons（固定額割引クーポン作成）

```bash
COUPON_RESPONSE=$(curl -s -X POST http://localhost:5006/api/v1/admin/coupons \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "code": "SPRING-2026-500",
    "couponTypeId": "type-fixed-001",
    "campaignId": "'${CAMPAIGN_ID}'",
    "discountType": 0,
    "discountValue": 500,
    "maxDiscountAmount": null,
    "minOrderAmount": 3000,
    "maxUsageCount": 100,
    "maxUsagePerUser": 1,
    "validFrom": "2026-04-01T00:00:00+09:00",
    "validUntil": "2026-05-31T23:59:59+09:00"
  }')
echo "$COUPON_RESPONSE" | python3 -m json.tool

COUPON_ID=$(echo "$COUPON_RESPONSE" | python3 -c "import sys, json; print(json.load(sys.stdin)['id'])")
COUPON_CODE="SPRING-2026-500"
echo "COUPON_ID=${COUPON_ID}"
```

**期待するレスポンス（201 Created）:**

```json
{
    "id": "uuid-xxx",
    "code": "SPRING-2026-500",
    "couponTypeId": "type-fixed-001",
    "campaignId": "uuid-xxx",
    "discountType": 0,
    "discountValue": 500,
    "maxDiscountAmount": null,
    "minOrderAmount": 3000,
    "maxUsageCount": 100,
    "currentUsageCount": 0,
    "maxUsagePerUser": 1,
    "validFrom": "2026-04-01T00:00:00+09:00",
    "validUntil": "2026-05-31T23:59:59+09:00",
    "isActive": true,
    "createdAt": "2026-04-07T..."
}
```

**検証項目:**
- [ ] ステータスコード 201 を返す
- [ ] `id` が UUID 形式で自動生成される
- [ ] `discountType` が `0`（FixedAmount）である
- [ ] `currentUsageCount` が `0` である
- [ ] `isActive` が `true` である
- [ ] `Location` ヘッダーに作成リソースの URL が含まれる

#### テスト 2-2: POST /api/v1/admin/coupons（パーセント割引クーポン作成）

```bash
COUPON_PCT_RESPONSE=$(curl -s -X POST http://localhost:5006/api/v1/admin/coupons \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "code": "SKI-10PCT-OFF",
    "couponTypeId": "type-percent-001",
    "campaignId": "'${CAMPAIGN_ID}'",
    "discountType": 1,
    "discountValue": 10,
    "maxDiscountAmount": 2000,
    "minOrderAmount": 5000,
    "maxUsageCount": 50,
    "maxUsagePerUser": 2,
    "validFrom": "2026-04-01T00:00:00+09:00",
    "validUntil": "2026-05-31T23:59:59+09:00"
  }')
echo "$COUPON_PCT_RESPONSE" | python3 -m json.tool

COUPON_PCT_ID=$(echo "$COUPON_PCT_RESPONSE" | python3 -c "import sys, json; print(json.load(sys.stdin)['id'])")
COUPON_PCT_CODE="SKI-10PCT-OFF"
echo "COUPON_PCT_ID=${COUPON_PCT_ID}"
```

**期待するレスポンス（201 Created）:**

**検証項目:**
- [ ] ステータスコード 201 を返す
- [ ] `discountType` が `1`（Percentage）である
- [ ] `maxDiscountAmount` が `2000` に設定される（パーセント割引の上限）

#### テスト 2-3: POST /api/v1/admin/coupons（バリデーションエラー — 不正クーポンコード）

```bash
curl -s -X POST http://localhost:5006/api/v1/admin/coupons \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "code": "invalid code!",
    "couponTypeId": "type-fixed-001",
    "discountType": 0,
    "discountValue": 0,
    "maxUsageCount": 0,
    "maxUsagePerUser": 0,
    "validFrom": "2026-05-01T00:00:00Z",
    "validUntil": "2026-04-01T00:00:00Z"
  }'
```

**期待するレスポンス（400 Bad Request）:**

```json
{
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    "title": "One or more validation errors occurred.",
    "status": 400,
    "errors": {
        "Code": ["クーポンコードは英大文字・数字・ハイフンのみ使用可能です"],
        "DiscountValue": ["割引値は0より大きい値を指定してください"],
        "MaxUsageCount": ["最大利用回数は1以上を指定してください"],
        "MaxUsagePerUser": ["ユーザーあたり最大利用回数は1以上を指定してください"],
        "ValidUntil": ["有効終了日は有効開始日より後を指定してください"]
    }
}
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] 各フィールドのバリデーションエラーメッセージが返る
- [ ] クーポンコード書式、割引値、利用回数、日付範囲の検証が動作する

#### テスト 2-4: POST /api/v1/admin/coupons（バリデーションエラー — パーセント割引 > 100%）

```bash
curl -s -X POST http://localhost:5006/api/v1/admin/coupons \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "code": "OVER-100PCT",
    "couponTypeId": "type-percent-001",
    "discountType": 1,
    "discountValue": 150,
    "maxUsageCount": 10,
    "maxUsagePerUser": 1,
    "validFrom": "2026-04-01T00:00:00Z",
    "validUntil": "2026-05-01T00:00:00Z"
  }'
```

**期待するレスポンス（400 Bad Request）:**

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] `DiscountValue` に「パーセント割引は100%以下を指定してください」エラーが返る

#### テスト 2-5: POST /api/v1/admin/coupons（認証なしアクセス）

```bash
curl -s -o /dev/null -w "%{http_code}" -X POST http://localhost:5006/api/v1/admin/coupons \
  -H "Content-Type: application/json" \
  -d '{"code": "TEST", "discountType": 0, "discountValue": 100, "maxUsageCount": 1, "maxUsagePerUser": 1, "validFrom": "2026-04-01T00:00:00Z", "validUntil": "2026-05-01T00:00:00Z"}'
```

**期待するレスポンス**: 401 Unauthorized

**検証項目:**
- [ ] ステータスコード 401 を返す

#### テスト 2-6: POST /api/v1/admin/coupons（一般ユーザーによるアクセス拒否）

```bash
curl -s -o /dev/null -w "%{http_code}" -X POST http://localhost:5006/api/v1/admin/coupons \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"code": "TEST", "discountType": 0, "discountValue": 100, "maxUsageCount": 1, "maxUsagePerUser": 1, "validFrom": "2026-04-01T00:00:00Z", "validUntil": "2026-05-01T00:00:00Z"}'
```

**期待するレスポンス**: 403 Forbidden

**検証項目:**
- [ ] ステータスコード 403 を返す

#### テスト 2-7: GET /api/v1/admin/coupons（クーポン一覧取得）

```bash
curl -s -X GET "http://localhost:5006/api/v1/admin/coupons?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
    "items": [
        {
            "id": "uuid-xxx",
            "code": "SPRING-2026-500",
            "couponTypeId": "type-fixed-001",
            "campaignId": "uuid-xxx",
            "discountType": 0,
            "discountValue": 500.0,
            "maxDiscountAmount": null,
            "minOrderAmount": 3000.0,
            "maxUsageCount": 100,
            "currentUsageCount": 0,
            "maxUsagePerUser": 1,
            "validFrom": "...",
            "validUntil": "...",
            "isActive": true,
            "createdAt": "..."
        }
    ],
    "totalCount": 2,
    "page": 1,
    "pageSize": 10,
    "totalPages": 1
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] ページネーション情報が含まれる
- [ ] 作成済みクーポンが一覧に表示される

#### テスト 2-8: GET /api/v1/admin/coupons（ページネーション — ページ2）

```bash
curl -s -X GET "http://localhost:5006/api/v1/admin/coupons?page=2&pageSize=1" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `page` が `2`、`pageSize` が `1` で正しくページネーションされている
- [ ] `totalPages` が正しく計算されている

#### テスト 2-9: GET /api/v1/admin/coupons（ソート — SortBy/Descending パラメータ）

```bash
curl -s -X GET "http://localhost:5006/api/v1/admin/coupons?page=1&pageSize=10&sortBy=Code&descending=false" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `SortBy=Code` によりクーポンコードの昇順（`descending=false`）でソートされている
- [ ] SKI-10PCT-OFF → SPRING-2026-500 の順で返る（アルファベット順）

#### テスト 2-10: GET /api/v1/admin/coupons/{id}（クーポン詳細取得）

```bash
curl -s -X GET "http://localhost:5006/api/v1/admin/coupons/${COUPON_ID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 指定した ID のクーポン情報が正しく返る

#### テスト 2-11: GET /api/v1/admin/coupons/{id}（存在しない ID — 404）

```bash
curl -s -o /dev/null -w "%{http_code}" -X GET \
  "http://localhost:5006/api/v1/admin/coupons/non-existent-coupon-id" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス**: 404 Not Found

**検証項目:**
- [ ] ステータスコード 404 を返す

#### テスト 2-12: PUT /api/v1/admin/coupons/{id}（クーポン更新）

```bash
curl -s -X PUT "http://localhost:5006/api/v1/admin/coupons/${COUPON_ID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "maxUsageCount": 200,
    "minOrderAmount": 2000
  }'
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 更新したフィールド（`maxUsageCount`, `minOrderAmount`）が反映される
- [ ] 更新しなかったフィールドが保持される

#### テスト 2-13: PUT /api/v1/admin/coupons/{id}（バリデーションエラー）

```bash
curl -s -X PUT "http://localhost:5006/api/v1/admin/coupons/${COUPON_ID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "discountType": 1,
    "discountValue": 200
  }'
```

**期待するレスポンス（400 Bad Request）:**

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] パーセント割引 > 100% のバリデーションエラーが返る

#### テスト 2-14: PUT /api/v1/admin/coupons/{id}（存在しない ID — 404）

```bash
curl -s -o /dev/null -w "%{http_code}" -X PUT \
  "http://localhost:5006/api/v1/admin/coupons/non-existent-coupon-id" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"maxUsageCount": 999}'
```

**期待するレスポンス**: 404 Not Found

**検証項目:**
- [ ] ステータスコード 404 を返す

#### テスト 2-15: GET /api/v1/admin/coupons/{id}/usages（クーポン利用履歴 — 未使用時は空）

```bash
curl -s -X GET "http://localhost:5006/api/v1/admin/coupons/${COUPON_ID}/usages?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
    "items": [],
    "totalCount": 0,
    "page": 1,
    "pageSize": 10,
    "totalPages": 0
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 未使用のため空リストが返る
- [ ] `totalCount` が `0` である

#### テスト 2-16: GET /api/v1/admin/coupons/{id}/usages（存在しない ID — 404）

```bash
curl -s -o /dev/null -w "%{http_code}" -X GET \
  "http://localhost:5006/api/v1/admin/coupons/non-existent-id/usages?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス**: 404 Not Found または 空リストの 200 OK

**検証項目:**
- [ ] ステータスコード 404 を返す、または `totalCount: 0` の空レスポンス（実装に依存）
- [ ] 実際の動作を確認し記録する

#### テスト 2-17: GET /api/v1/admin/coupons/analytics（分析データ取得 — 全体）

```bash
curl -s -X GET "http://localhost:5006/api/v1/admin/coupons/analytics" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 全体のアナリティクスデータが返る

#### テスト 2-18: GET /api/v1/admin/coupons/analytics?couponId={id}（分析データ取得 — 個別クーポン）

```bash
curl -s -X GET "http://localhost:5006/api/v1/admin/coupons/analytics?couponId=${COUPON_ID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
    "couponId": "uuid-xxx",
    "code": "SPRING-2026-500",
    "totalUsageCount": 0,
    "totalDiscountAmount": 0,
    "uniqueUserCount": 0
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 指定クーポンのアナリティクスデータが返る

#### テスト 2-19: GET /api/v1/admin/coupons/analytics?couponId={non-existent}（存在しない couponId — 404）

```bash
curl -s -o /dev/null -w "%{http_code}" -X GET \
  "http://localhost:5006/api/v1/admin/coupons/analytics?couponId=non-existent-id" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス**: 404 Not Found または 0 件のアナリティクスデータ

**検証項目:**
- [ ] ステータスコード 404 を返す、または空のアナリティクスデータ（実装に依存）
- [ ] 実際の動作を確認し記録する

#### テスト 2-20: DELETE /api/v1/admin/coupons/{id}（クーポン無効化）

> **📝 注意**: テスト用のクーポンを別途作成して無効化テストを行う。メインテスト用クーポンは無効化しない。

```bash
# 無効化テスト用クーポンを作成
DEACT_RESPONSE=$(curl -s -X POST http://localhost:5006/api/v1/admin/coupons \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "code": "TO-DEACTIVATE",
    "couponTypeId": "type-fixed-001",
    "discountType": 0,
    "discountValue": 100,
    "maxUsageCount": 10,
    "maxUsagePerUser": 1,
    "validFrom": "2026-04-01T00:00:00Z",
    "validUntil": "2026-05-01T00:00:00Z"
  }')
DEACT_ID=$(echo "$DEACT_RESPONSE" | python3 -c "import sys, json; print(json.load(sys.stdin)['id'])")

# 無効化
curl -s -o /dev/null -w "%{http_code}" -X DELETE \
  "http://localhost:5006/api/v1/admin/coupons/${DEACT_ID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス**: 204 No Content

**検証項目:**
- [ ] ステータスコード 204 を返す

**確認（無効化されたことを確認）:**

```bash
curl -s -X GET "http://localhost:5006/api/v1/admin/coupons/${DEACT_ID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" | python3 -c "import sys, json; print('isActive:', json.load(sys.stdin)['isActive'])"
# 期待結果: isActive: False
```

#### テスト 2-21: DELETE /api/v1/admin/coupons/{id}（存在しない ID — 404）

```bash
curl -s -o /dev/null -w "%{http_code}" -X DELETE \
  "http://localhost:5006/api/v1/admin/coupons/non-existent-coupon-id" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス**: 404 Not Found

**検証項目:**
- [ ] ステータスコード 404 を返す

---

### Phase 3: 一般ユーザー — クーポン利用

> **📝 前提**: `ACCESS_TOKEN`, `USER_ID` を環境変数に設定済み。Phase 2 でクーポン（`COUPON_CODE`, `COUPON_PCT_CODE`）を作成済み。

#### テスト 3-1: GET /api/v1/coupons/available（利用可能クーポン一覧取得）

```bash
curl -s -X GET "http://localhost:5006/api/v1/coupons/available?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
    "items": [
        {
            "id": "uuid-xxx",
            "code": "SPRING-2026-500",
            "discountType": 0,
            "discountValue": 500.0,
            "maxDiscountAmount": null,
            "validFrom": "...",
            "validUntil": "...",
            "isActive": true
        }
    ],
    "totalCount": 2,
    "page": 1,
    "pageSize": 10,
    "totalPages": 1
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] アクティブなクーポンのみが表示される（無効化されたクーポンは含まれない）
- [ ] ページネーション情報が含まれる
- [ ] レスポンスは `CouponSummaryResponse`（簡易情報）形式である

#### テスト 3-2: GET /api/v1/coupons/available（認証なしアクセス）

```bash
curl -s -o /dev/null -w "%{http_code}" -X GET \
  "http://localhost:5006/api/v1/coupons/available?page=1&pageSize=10"
```

**期待するレスポンス**: 401 Unauthorized

**検証項目:**
- [ ] ステータスコード 401 を返す

#### テスト 3-3: POST /api/v1/coupons/{code}/acquire（クーポン取得）

```bash
ACQUIRE_RESPONSE=$(curl -s -X POST \
  "http://localhost:5006/api/v1/coupons/${COUPON_CODE}/acquire" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}")
echo "$ACQUIRE_RESPONSE" | python3 -m json.tool
```

**期待するレスポンス（201 Created）:**

```json
{
    "id": "uuid-xxx",
    "couponCode": "SPRING-2026-500",
    "discountType": 0,
    "discountValue": 500.0,
    "maxDiscountAmount": null,
    "validFrom": "...",
    "validUntil": "...",
    "status": "Available",
    "acquiredAt": "2026-04-07T..."
}
```

**検証項目:**
- [ ] ステータスコード 201 を返す
- [ ] `status` が `Available` である
- [ ] `acquiredAt` に取得日時が設定される
- [ ] `couponCode` がリクエストしたコードと一致する

#### テスト 3-4: POST /api/v1/coupons/{code}/acquire（同一クーポンの重複取得）

```bash
curl -s -X POST \
  "http://localhost:5006/api/v1/coupons/${COUPON_CODE}/acquire" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（422 Unprocessable Entity）:**

**検証項目:**
- [ ] ステータスコード 422 を返す
- [ ] 重複取得防止のビジネスルールエラーメッセージが返る

#### テスト 3-5: POST /api/v1/coupons/{code}/acquire（存在しないクーポンコード）

```bash
curl -s -o /dev/null -w "%{http_code}" -X POST \
  "http://localhost:5006/api/v1/coupons/NON-EXISTENT-CODE/acquire" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス**: 404 Not Found

**検証項目:**
- [ ] ステータスコード 404 を返す

#### テスト 3-6: POST /api/v1/coupons/{code}/acquire（認証なしアクセス）

```bash
curl -s -o /dev/null -w "%{http_code}" -X POST \
  "http://localhost:5006/api/v1/coupons/${COUPON_CODE}/acquire"
```

**期待するレスポンス**: 401 Unauthorized

**検証項目:**
- [ ] ステータスコード 401 を返す

#### テスト 3-7: GET /api/v1/coupons/mine（保有クーポン一覧取得）

```bash
curl -s -X GET "http://localhost:5006/api/v1/coupons/mine" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
[
    {
        "id": "uuid-xxx",
        "couponCode": "SPRING-2026-500",
        "discountType": 0,
        "discountValue": 500.0,
        "maxDiscountAmount": null,
        "validFrom": "...",
        "validUntil": "...",
        "status": "Available",
        "acquiredAt": "..."
    }
]
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] Phase 3-3 で取得したクーポンが一覧に含まれる
- [ ] 他ユーザーのクーポンが含まれない（自分のクーポンのみ）

#### テスト 3-8: GET /api/v1/coupons/mine（認証なしアクセス）

```bash
curl -s -o /dev/null -w "%{http_code}" -X GET "http://localhost:5006/api/v1/coupons/mine"
```

**期待するレスポンス**: 401 Unauthorized

**検証項目:**
- [ ] ステータスコード 401 を返す

#### テスト 3-9: POST /api/v1/coupons/validate（クーポン検証 — 有効）

```bash
curl -s -X POST "http://localhost:5006/api/v1/coupons/validate" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "code": "SPRING-2026-500",
    "orderAmount": 5000
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
    "isValid": true,
    "errorCode": null,
    "errorMessage": null,
    "discountAmount": 500.0,
    "couponId": "uuid-xxx",
    "discountType": 0,
    "discountValue": 500.0
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `isValid` が `true` である
- [ ] `discountAmount` が正しく計算される（固定額 500 円）
- [ ] `couponId` が存在する

#### テスト 3-10: POST /api/v1/coupons/validate（クーポン検証 — 最低注文金額未満）

```bash
curl -s -X POST "http://localhost:5006/api/v1/coupons/validate" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "code": "SPRING-2026-500",
    "orderAmount": 1000
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
    "isValid": false,
    "errorCode": "...",
    "errorMessage": "...",
    "discountAmount": 0
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `isValid` が `false` である
- [ ] 最低注文金額未達のエラーメッセージが返る

#### テスト 3-11: POST /api/v1/coupons/validate（存在しないクーポンコード）

```bash
curl -s -X POST "http://localhost:5006/api/v1/coupons/validate" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "code": "NON-EXISTENT",
    "orderAmount": 5000
  }'
```

**期待するレスポンス（200 OK or 404）:**

**検証項目:**
- [ ] `isValid` が `false` である、またはステータスコード 404 を返す

#### テスト 3-12: POST /api/v1/coupons/validate（バリデーションエラー — コード空）

```bash
curl -s -X POST "http://localhost:5006/api/v1/coupons/validate" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "code": "",
    "orderAmount": 0
  }'
```

**期待するレスポンス（400 Bad Request）:**

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] バリデーションエラーメッセージが返る

#### テスト 3-13: POST /api/v1/coupons/validate（パーセント割引の検証）

```bash
curl -s -X POST "http://localhost:5006/api/v1/coupons/validate" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "code": "SKI-10PCT-OFF",
    "orderAmount": 30000
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
    "isValid": true,
    "errorCode": null,
    "errorMessage": null,
    "discountAmount": 2000.0,
    "couponId": "uuid-xxx",
    "discountType": 1,
    "discountValue": 10.0
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `isValid` が `true` である
- [ ] `discountAmount` が `maxDiscountAmount`（2000 円）に制限される（30000 × 10% = 3000 だが上限 2000）

---

### Phase 4: 内部サービス — クーポン操作

> **⚠️ 前提**: 内部サービス向けエンドポイントは `InternalServiceOnly` ポリシーで保護されている。このポリシーは JWT の `sub` が `internal-service`、`scope` に `coupon:manage` を含む、`iss` が存在することを要求する。AuthService が発行する内部サービストークンが必要であり、通常のユーザー/管理者トークンではアクセスできない。

> **📝 内部サービストークンが取得できない場合**: AuthService が内部サービストークン発行機能を未実装の場合、テスト 4-1〜4-3（認証必須の確認）のみ実施し、4-4〜4-12 はスキップする。代替として DB 直接操作でテストデータを作成する方法も記載する。

#### テスト 4-1: POST /api/v1/internal/coupons/calculate（認証なしアクセス）

```bash
curl -s -o /dev/null -w "%{http_code}" -X POST \
  http://localhost:5006/api/v1/internal/coupons/calculate \
  -H "Content-Type: application/json" \
  -d '{"couponCode": "SPRING-2026-500", "userId": "test-user", "orderAmount": 5000}'
```

**期待するレスポンス**: 401 Unauthorized

**検証項目:**
- [ ] ステータスコード 401 を返す

#### テスト 4-2: POST /api/v1/internal/coupons/calculate（一般ユーザートークンでのアクセス拒否）

```bash
curl -s -o /dev/null -w "%{http_code}" -X POST \
  http://localhost:5006/api/v1/internal/coupons/calculate \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"couponCode": "SPRING-2026-500", "userId": "'${USER_ID}'", "orderAmount": 5000}'
```

**期待するレスポンス**: 403 Forbidden

**検証項目:**
- [ ] ステータスコード 403 を返す
- [ ] 一般ユーザートークンでは InternalServiceOnly ポリシーを通過できない

#### テスト 4-3: POST /api/v1/internal/coupons/calculate（管理者トークンでのアクセス拒否）

```bash
curl -s -o /dev/null -w "%{http_code}" -X POST \
  http://localhost:5006/api/v1/internal/coupons/calculate \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"couponCode": "SPRING-2026-500", "userId": "'${USER_ID}'", "orderAmount": 5000}'
```

**期待するレスポンス**: 403 Forbidden

**検証項目:**
- [ ] ステータスコード 403 を返す
- [ ] 管理者トークンであっても InternalServiceOnly ポリシーを通過できない

#### テスト 4-4: POST /api/v1/internal/coupons/calculate（割引金額計算）

> **📝 前提**: 内部サービストークン `INTERNAL_TOKEN` を取得済み。

```bash
curl -s -X POST http://localhost:5006/api/v1/internal/coupons/calculate \
  -H "Authorization: Bearer ${INTERNAL_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "couponCode": "SPRING-2026-500",
    "userId": "'${USER_ID}'",
    "orderAmount": 5000
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
    "isValid": true,
    "errorCode": null,
    "errorMessage": null,
    "couponId": "uuid-xxx",
    "discountAmount": 500.0,
    "discountType": 0,
    "discountValue": 500.0
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `isValid` が `true` である
- [ ] `discountAmount` が正しく計算される

#### テスト 4-5: POST /api/v1/internal/coupons/calculate（バリデーションエラー）

```bash
curl -s -X POST http://localhost:5006/api/v1/internal/coupons/calculate \
  -H "Authorization: Bearer ${INTERNAL_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "couponCode": "",
    "userId": "",
    "orderAmount": 0
  }'
```

**期待するレスポンス（400 Bad Request）:**

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] 各フィールドのバリデーションエラーが返る

#### テスト 4-6: POST /api/v1/internal/coupons/redeem（クーポン利用）

```bash
ORDER_ID=$(python3 -c "import uuid; print(str(uuid.uuid4()))")
REDEEM_RESPONSE=$(curl -s -X POST http://localhost:5006/api/v1/internal/coupons/redeem \
  -H "Authorization: Bearer ${INTERNAL_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "couponId": "'${COUPON_ID}'",
    "orderId": "'${ORDER_ID}'",
    "discountAmount": 5000,
    "userId": "'${USER_ID}'"
  }')
echo "$REDEEM_RESPONSE" | python3 -m json.tool

USAGE_ID=$(echo "$REDEEM_RESPONSE" | python3 -c "import sys, json; print(json.load(sys.stdin)['id'])")
echo "USAGE_ID=${USAGE_ID}"
echo "ORDER_ID=${ORDER_ID}"
```

**期待するレスポンス（201 Created）:**

```json
{
    "id": "uuid-xxx",
    "userId": "uuid-xxx",
    "orderId": "uuid-xxx",
    "discountAmount": 500.0,
    "usedAt": "2026-04-07T..."
}
```

**検証項目:**
- [ ] ステータスコード 201 を返す
- [ ] `discountAmount` がサーバー側で再計算された値（500 円）である（リクエストの 5000 ではない）
- [ ] `userId` がリクエストで指定したユーザー ID と一致する
- [ ] `orderId` がリクエストで指定した注文 ID と一致する
- [ ] `Location` ヘッダーに作成リソースの URL が含まれる

#### テスト 4-7: POST /api/v1/internal/coupons/redeem（同一注文 ID での重複利用 — 冪等性）

```bash
curl -s -X POST http://localhost:5006/api/v1/internal/coupons/redeem \
  -H "Authorization: Bearer ${INTERNAL_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "couponId": "'${COUPON_ID}'",
    "orderId": "'${ORDER_ID}'",
    "discountAmount": 5000,
    "userId": "'${USER_ID}'"
  }'
```

**期待するレスポンス（201 Created — 冪等: 既存の usage を返す）:**

**検証項目:**
- [ ] ステータスコード 201 を返す（冪等性により既存レコードを返す）
- [ ] `currentUsageCount` が二重にインクリメントされない

#### テスト 4-8: POST /api/v1/internal/coupons/redeem（バリデーションエラー）

```bash
curl -s -X POST http://localhost:5006/api/v1/internal/coupons/redeem \
  -H "Authorization: Bearer ${INTERNAL_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "couponId": "",
    "orderId": "",
    "discountAmount": 0,
    "userId": ""
  }'
```

**期待するレスポンス（400 Bad Request）:**

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] 各フィールドのバリデーションエラーが返る

#### テスト 4-9: POST /api/v1/internal/coupons/release（クーポンリリース — Saga 補償）

```bash
curl -s -o /dev/null -w "%{http_code}" -X POST \
  http://localhost:5006/api/v1/internal/coupons/release \
  -H "Authorization: Bearer ${INTERNAL_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "couponId": "'${COUPON_ID}'",
    "orderId": "'${ORDER_ID}'"
  }'
```

**期待するレスポンス**: 204 No Content

**検証項目:**
- [ ] ステータスコード 204 を返す
- [ ] `currentUsageCount` が減算される

**確認（利用回数がリリースされたことを確認）:**

```bash
curl -s -X GET "http://localhost:5006/api/v1/admin/coupons/${COUPON_ID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" | python3 -c "import sys, json; print('currentUsageCount:', json.load(sys.stdin)['currentUsageCount'])"
# 期待結果: currentUsageCount: 0（リリースにより 1 → 0 に戻る）
```

#### テスト 4-10: POST /api/v1/internal/coupons/release（冪等性確認 — 同一注文 ID で再リリース）

```bash
curl -s -o /dev/null -w "%{http_code}" -X POST \
  http://localhost:5006/api/v1/internal/coupons/release \
  -H "Authorization: Bearer ${INTERNAL_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "couponId": "'${COUPON_ID}'",
    "orderId": "'${ORDER_ID}'"
  }'
```

**期待するレスポンス**: 204 No Content

**検証項目:**
- [ ] ステータスコード 204 を返す（冪等性により正常終了）
- [ ] `currentUsageCount` が二重減算されない（0 のまま）

#### テスト 4-11: POST /api/v1/internal/coupons/release（バリデーションエラー）

```bash
curl -s -X POST http://localhost:5006/api/v1/internal/coupons/release \
  -H "Authorization: Bearer ${INTERNAL_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "couponId": "",
    "orderId": ""
  }'
```

**期待するレスポンス（400 Bad Request）:**

**検証項目:**
- [ ] ステータスコード 400 を返す

#### テスト 4-12: POST /api/v1/internal/coupons/redeem（利用上限超過）

> **📝 注意**: MaxUsagePerUser=1 のクーポン（SPRING-2026-500）で同一ユーザーが 2 回目の利用を試みる。

```bash
NEW_ORDER_ID=$(python3 -c "import uuid; print(str(uuid.uuid4()))")
curl -s -X POST http://localhost:5006/api/v1/internal/coupons/redeem \
  -H "Authorization: Bearer ${INTERNAL_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "couponId": "'${COUPON_ID}'",
    "orderId": "'${NEW_ORDER_ID}'",
    "discountAmount": 5000,
    "userId": "'${USER_ID}'"
  }'
```

**期待するレスポンス（422 Unprocessable Entity）:**

**検証項目:**
- [ ] ステータスコード 422 を返す
- [ ] 利用上限超過のエラーメッセージが返る

---

### Phase 5: クーポン利用後の管理者確認

> **📝 前提**: Phase 4 でクーポンが利用・リリースされた後の状態を確認する。

#### テスト 5-1: GET /api/v1/admin/coupons/{id}/usages（利用履歴確認 — リリース済み）

```bash
curl -s -X GET "http://localhost:5006/api/v1/admin/coupons/${COUPON_ID}/usages?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] リリース済み利用レコードの状態が確認できる

#### テスト 5-2: GET /api/v1/admin/coupons/analytics?couponId={id}（利用後のアナリティクス）

```bash
curl -s -X GET "http://localhost:5006/api/v1/admin/coupons/analytics?couponId=${COUPON_ID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 利用履歴がアナリティクスに反映されている

---

### Phase 6: 無効化されたクーポンの操作テスト

> **📝 目的**: 無効化（DeactivateCoupon）されたクーポンに対する各操作が適切に拒否されるかを確認する。

#### テスト 6-1: POST /api/v1/coupons/{code}/acquire（無効化クーポンの取得試行）

```bash
curl -s -X POST \
  "http://localhost:5006/api/v1/coupons/TO-DEACTIVATE/acquire" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**検証項目:**
- [ ] ステータスコード 422 または 404 を返す
- [ ] 無効化されたクーポンは取得できない

#### テスト 6-2: POST /api/v1/coupons/validate（無効化クーポンの検証）

```bash
curl -s -X POST "http://localhost:5006/api/v1/coupons/validate" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "code": "TO-DEACTIVATE",
    "orderAmount": 5000
  }'
```

**検証項目:**
- [ ] `isValid` が `false` である、または適切なエラーレスポンスが返る

---

## 5. verification-report.md のフォーマット

検証結果は以下のフォーマットで `verification-report.md` に記録する。

```markdown
# CouponService API 検証レポート

**実施日時**: YYYY-MM-DD
**検証環境**: Docker Compose (localhost:5006)
**検証対象**: CouponService v1.0

---

## テスト結果サマリー

| Phase | テスト項目 | 結果 | 備考 |
|-------|-----------|------|------|
| 0 | GET /health | ✅ PASS / ❌ FAIL | - |
| ... | ... | ... | ... |

**全体結果**: XX/YY テスト PASS（Z 件は既知のバグ）

---

## Phase X: テスト項目名

### Test X-Y: エンドポイント説明

**リクエスト**:
```bash
curl コマンド
```

**レスポンス**:
```json
{
    "key": "value"
}
```

**HTTP Status**: XXX
**結果**: ✅ PASS / ❌ FAIL（理由）
```

---

## 6. テスト用データ

### 6.1 CouponType マスターデータ

| ID | 名前 | 説明 | 利用制限タイプ |
|----|------|------|-------------|
| `type-fixed-001` | 固定額割引 | 注文金額から固定額を割引 | MultiUse (1) |
| `type-percent-001` | パーセント割引 | 注文金額から指定%を割引 | MultiUse (1) |
| `type-shipping-001` | 送料無料 | 送料を無料にする | SingleUse (0) |

### 6.2 テストクーポン

| コード | 割引タイプ | 割引値 | 上限割引額 | 最低注文額 | 利用上限 | ユーザー上限 |
|--------|----------|--------|----------|----------|---------|-----------|
| `SPRING-2026-500` | FixedAmount (0) | 500 | なし | 3000 | 100 | 1 |
| `SKI-10PCT-OFF` | Percentage (1) | 10% | 2000 | 5000 | 50 | 2 |
| `TO-DEACTIVATE` | FixedAmount (0) | 100 | なし | 0 | 10 | 1 |

### 6.3 テストキャンペーン

| 名前 | 開始日 | 終了日 | 最大発行数 |
|------|--------|--------|----------|
| 2026年春のスキーセール | 2026-04-01 | 2026-05-31 | 1000 |

### 6.4 レート制限設定

| ポリシー名 | 上限 | ウィンドウ | 適用エンドポイント |
|----------|------|----------|----------------|
| `coupon-api` | 100 リクエスト | 1 分 | CouponEndpoints, AdminCouponEndpoints, CampaignEndpoints |
| `redeem-api` | 10 リクエスト | 1 分 | InternalCouponEndpoints |

### 6.5 認可ポリシー

| ポリシー名 | 要件 | 適用エンドポイント |
|----------|------|----------------|
| `AdminOnly` | ロール: Admin | AdminCouponEndpoints, CampaignEndpoints |
| `UserOrAdmin` | ロール: User または Admin | GET /api/v1/coupons/available |
| `InternalServiceOnly` | sub=internal-service, scope=coupon:manage, iss 存在 | InternalCouponEndpoints |
| FallbackPolicy | 認証済みユーザー | その他全エンドポイント |

### 6.6 DiscountType 一覧

| 値 | 名前 | 説明 |
|----|------|------|
| 0 | FixedAmount | 固定額割引 |
| 1 | Percentage | パーセント割引（100% 以下） |
| 2 | FreeShipping | 送料無料 |

### 6.7 CampaignStatus 一覧

| 値 | 名前 | 説明 |
|----|------|------|
| 0 | Draft | 下書き |
| 1 | Active | 有効 |
| 2 | Paused | 一時停止 |
| 3 | Ended | 終了 |

### 6.8 例外とHTTPステータスコードのマッピング

| 例外クラス | HTTP ステータスコード | 発生条件 |
|----------|-------------------|---------|
| `CouponNotFoundException` | 404 | クーポンが見つからない |
| `CouponExpiredException` | 422 | クーポンの有効期限切れ |
| `CouponUsageLimitExceededException` | 422 | 利用上限超過 |
| `InvalidCouponException` | 422 | クーポンが無効（IsActive=false 等） |
| `CouponAlreadyAcquiredException` | 422 | クーポンの重複取得 |
| `CampaignIssueLimitReachedException` | 422 | キャンペーン発行上限到達 |
| `BusinessException` | 422 | その他のビジネスルール違反 |
| `CouponFraudDetectedException` | 403 | 不正利用検出 |
| `UnauthorizedException` | 401 | 認証エラー |
| `ForbiddenException` | 403 | 認可エラー |
| `ConcurrencyException` | 409 | 楽観的ロック競合 |
| その他 | 500 | 内部エラー |

---

## 7. トラブルシューティング

### 7.1 ヘルスチェックが Unhealthy の場合

```bash
# コンテナのログを確認
docker compose logs coupon-service --tail 50

# PostgreSQL 接続確認
docker exec -it skishop-postgres psql -U skishop -d coupondb -c "SELECT 1;"

# Redis 接続確認
docker exec -it skishop-redis redis-cli ping

# Kafka 接続確認
docker exec -it skishop-kafka kafka-topics --bootstrap-server localhost:9092 --list
```

### 7.2 401 Unauthorized が返る場合

```bash
# JWT トークンの有効期限を確認（base64 デコード）
echo "$ACCESS_TOKEN" | cut -d. -f2 | base64 -d 2>/dev/null | python3 -m json.tool

# トークンの再取得
LOGIN_RESPONSE=$(curl -s -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email": "coupontest@example.com", "password": "SecurePass123!"}')
ACCESS_TOKEN=$(echo "$LOGIN_RESPONSE" | python3 -c "import sys, json; print(json.load(sys.stdin)['accessToken'])")
```

### 7.3 DB テーブルの確認

```bash
# テーブル一覧
docker exec -it skishop-postgres psql -U skishop -d coupondb -c "\dt"

# クーポンデータ確認
docker exec -it skishop-postgres psql -U skishop -d coupondb -c "SELECT id, code, is_active, current_usage_count FROM coupons;"

# キャンペーンデータ確認
docker exec -it skishop-postgres psql -U skishop -d coupondb -c "SELECT id, name, status FROM campaigns;"

# CouponType データ確認
docker exec -it skishop-postgres psql -U skishop -d coupondb -c "SELECT id, name FROM coupon_types;"

# 利用履歴確認
docker exec -it skishop-postgres psql -U skishop -d coupondb -c "SELECT id, coupon_id, user_id, order_id, discount_amount, is_released FROM coupon_usages;"
```

### 7.4 テストデータのクリーンアップ

```bash
# テストデータを全削除（検証のやり直し時に使用）
docker exec -it skishop-postgres psql -U skishop -d coupondb -c "
DELETE FROM coupon_usages;
DELETE FROM user_coupons;
DELETE FROM coupon_restrictions;
DELETE FROM coupons;
DELETE FROM campaigns;
-- CouponType マスターデータは保持
"
```

### 7.5 マイグレーションの確認

```bash
# 適用済みマイグレーション一覧
docker exec -it skishop-postgres psql -U skishop -d coupondb \
  -c "SELECT migration_id, product_version FROM \"__EFMigrationsHistory\" ORDER BY migration_id;"
```

---

## 8. 実行手順チェックリスト

検証実行時に以下のチェックリストを順に確認・実行する。

### 事前準備

- [ ] Docker Compose でインフラサービスが起動済み（`docker compose -f docker-compose.infra.yml up -d`）
- [ ] AuthService が起動済み（`docker compose up -d auth-service`）
- [ ] CouponService が起動済み（`docker compose up -d coupon-service`）
- [ ] CouponService のヘルスチェックが Healthy（`curl -s http://localhost:5006/health`）
- [ ] AuthService のヘルスチェックが Healthy（`curl -s http://localhost:5001/health`）
- [ ] CouponType マスターデータが投入済み（§2.5）
- [ ] 一般ユーザーの JWT トークンを取得済み（`ACCESS_TOKEN`）
- [ ] 管理者の JWT トークンを取得済み（`ADMIN_TOKEN`）

### 検証実行

- [ ] Phase 0: ヘルスチェック（2 テスト）
- [ ] Phase 1: 管理者 — キャンペーン管理（11 テスト）
- [ ] Phase 2: 管理者 — クーポン管理（16 テスト）
- [ ] Phase 3: 一般ユーザー — クーポン利用（13 テスト）
- [ ] Phase 4: 内部サービス — クーポン操作（12 テスト）
- [ ] Phase 5: クーポン利用後の管理者確認（2 テスト）
- [ ] Phase 6: 無効化クーポンの操作テスト（2 テスト）

### 検証完了

- [ ] 全テスト結果を `verification-report.md` に記録
- [ ] テスト結果サマリーテーブルを作成
- [ ] 失敗テストの原因と対応策を Appendix に記載
- [ ] テストデータのクリーンアップ（必要に応じて §7.4 参照）

**合計テスト数: 58 テスト**（ヘルスチェック 2 + キャンペーン管理 11 + クーポン管理 16 + ユーザー操作 13 + 内部サービス 12 + 利用後確認 2 + 無効化テスト 2）
