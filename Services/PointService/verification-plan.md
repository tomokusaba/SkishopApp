# PointService 検証計画書（verification-plan.md）

本ドキュメントは PointService の全エンドポイントに対する動作検証の手順とフォーマットを定義する。
検証結果は `verification-report.md` に記録し、PointService の使い方を理解するためのリファレンスとして活用する。

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

1. **機能確認**: 全 14 REST エンドポイント（+ 2 ヘルスチェック）が設計通りに動作することを確認する
2. **使い方の文書化**: 各 API の呼び出し方法・パラメータ・レスポンスを実例で示す
3. **異常系の確認**: バリデーションエラー、認証エラー、認可エラー等の動作を確認する
4. **セキュリティ検証**: 管理者専用エンドポイント、内部 API エンドポイントのアクセス制御を確認する
5. **ポイント操作フロー確認**: ポイント付与 → 仮消費 → 確定 / 解放の一連のフローを確認する

---

## 2. 前提条件

### 2.1 環境構成

| 項目 | 値 |
|------|-----|
| PointService URL | `http://localhost:5007` |
| AuthService URL | `http://localhost:5001` |
| PostgreSQL | `localhost:5432`（DB: `pointdb`） |
| Redis | `localhost:6379` |
| Kafka | `localhost:9092` |

### 2.2 起動コマンド

```bash
# Docker Compose で PointService と依存サービスを起動
cd /Users/yoterada/GitHub/DotNet-Skishop-App
docker compose up -d auth-service point-service
```

> **📝 注意**: PointService は AuthService が発行した JWT トークンを使用して認証するため、AuthService も同時に起動する必要がある。

### 2.3 事前確認コマンド

```bash
# PointService ヘルスチェック
curl -s http://localhost:5007/health
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
    "email": "testuser@example.com",
    "password": "Password123!",
    "firstName": "Test",
    "lastName": "User",
    "username": "testuser"
  }'
```

```bash
# 2. ユーザーステータスを ACTIVE に変更（DB 直接操作）
docker exec -it skishop-postgres psql -U skishop -d authdb \
  -c "UPDATE users SET status = 'ACTIVE', email_verified = true WHERE email = 'testuser@example.com';"
```

```bash
# 3. ログインしてトークン取得
curl -s -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "testuser@example.com",
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
    "email": "admin@example.com",
    "password": "AdminPass123!",
    "firstName": "Admin",
    "lastName": "User",
    "username": "adminuser"
  }'
```

```bash
# 2. 管理者ステータスとロール設定
docker exec -it skishop-postgres psql -U skishop -d authdb \
  -c "UPDATE users SET status = 'ACTIVE', email_verified = true, role = 'Admin' WHERE email = 'admin@example.com';"
```

```bash
# 3. 管理者ログイン
curl -s -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@example.com",
    "password": "AdminPass123!"
  }'

# レスポンスの accessToken を環境変数に保存
export ADMIN_TOKEN="eyJhbGci..."
export ADMIN_USER_ID="取得した管理者ユーザーID"
```

#### 内部サービストークン取得（InternalServiceOnly 用）

内部 API は `scope=internal` クレームを含む JWT が必要。テスト用に特殊なトークンを発行するか、AuthService の設定で内部サービス用トークンを発行する。

```bash
# 内部サービス用トークン（AuthService の内部サービス認証機能を使用）
# ※実装に応じて調整が必要
export INTERNAL_TOKEN="eyJhbGci..."
```

> **📝 注意**: 内部 API のテストは、内部サービストークンが利用できない場合はスキップする。

### 2.5 PointService 用のポイントアカウント初期化

PointService は Kafka イベント（`user.registered`）を購読してポイントアカウントを自動作成する設計。
AuthService でのユーザー登録後、Kafka 経由で PointService にポイントアカウントが作成されているか確認すること。

手動でポイントアカウントを作成する場合は、以下の SQL を使用する。

```bash
# ポイントアカウントが存在するか確認
docker exec -it skishop-postgres psql -U skishop -d pointdb \
  -c "SELECT id, user_id, available_points, pending_points FROM point_accounts WHERE user_id = '${USER_ID}';"

# 存在しない場合、手動で挿入
docker exec -it skishop-postgres psql -U skishop -d pointdb \
  -c "INSERT INTO point_accounts (id, user_id, available_points, pending_points, total_earned, total_spent, total_expired, created_at, updated_at)
      VALUES (gen_random_uuid(), '${USER_ID}', 0, 0, 0, 0, 0, NOW(), NOW())
      ON CONFLICT (user_id) DO NOTHING;"
```

### 2.6 テスト用ポイント付与（検証準備）

ポイント消費系のテストを行う前に、テスト用ポイントを付与する。

```bash
# 管理者によるポイント調整でテスト用ポイントを付与
curl -s -X POST "http://localhost:5007/api/v1/admin/points/users/${USER_ID}/adjust" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "points": 10000,
    "reason": "検証用初期ポイント付与"
  }'
```

---

## 3. エンドポイント一覧

### 3.1 ヘルスチェック

| メソッド | パス | 説明 | 認証 |
|---------|------|------|------|
| GET | `/health` | Liveness チェック | 不要 |
| GET | `/health/ready` | Readiness チェック（PostgreSQL, Redis 疎通） | 不要 |

### 3.2 一般ユーザー向け（Points）

| メソッド | パス | 説明 | 認証 | 備考 |
|---------|------|------|------|------|
| GET | `/api/v1/points/balance` | 自分のポイント残高取得 | 必要 | |
| GET | `/api/v1/points/history` | 自分のポイント履歴取得 | 必要 | ページネーション対応 |
| GET | `/api/v1/points/tier` | 自分の会員ティア情報取得 | 必要 | |
| GET | `/api/v1/points/expiring` | 期限切れ間近のポイント取得 | 必要 | 30日以内に失効するポイント |

### 3.3 管理者向け（PointsAdmin）

| メソッド | パス | 説明 | 認証 | 備考 |
|---------|------|------|------|------|
| GET | `/api/v1/admin/points/users/{userId}/balance` | 指定ユーザーのポイント残高取得 | Admin のみ | |
| POST | `/api/v1/admin/points/users/{userId}/adjust` | ポイント手動調整（加算/減算） | Admin のみ | 監査ログ記録 |
| GET | `/api/v1/admin/points/analytics` | ポイント分析データ取得 | Admin のみ | |

### 3.4 ティア管理（Tiers）

| メソッド | パス | 説明 | 認証 | 備考 |
|---------|------|------|------|------|
| GET | `/api/v1/admin/tiers` | ティア定義一覧取得 | Admin のみ | |
| PUT | `/api/v1/admin/tiers/{id}` | ティア定義更新 | Admin のみ | |

### 3.5 内部 API（PointsInternal）

| メソッド | パス | 説明 | 認証 | 備考 |
|---------|------|------|------|------|
| GET | `/api/v1/internal/points/users/{userId}/balance` | ユーザーのポイント残高取得 | InternalServiceOnly | 他サービスからの呼び出し用 |
| POST | `/api/v1/internal/points/reserve` | ポイント仮消費（Reserve） | InternalServiceOnly | Saga 連携 |
| POST | `/api/v1/internal/points/confirm` | ポイント消費確定（Confirm） | InternalServiceOnly | Saga 連携 |
| POST | `/api/v1/internal/points/release` | ポイント仮消費解放（Release） | InternalServiceOnly | Saga 補償 |
| POST | `/api/v1/internal/points/award` | ポイント付与（Award） | InternalServiceOnly | 注文完了時のポイント付与 |

### 3.6 gRPC サービス

| サービス | メソッド | 説明 | 認証 |
|---------|---------|------|------|
| PointGrpc | ReservePoints | ポイント仮消費 | InternalServiceOnly |
| PointGrpc | ReleasePoints | ポイント解放 | InternalServiceOnly |
| PointGrpc | AwardPoints | ポイント付与 | InternalServiceOnly |
| PointGrpc | ConfirmPoints | ポイント確定 | InternalServiceOnly |
| PointGrpc | GetBalance | 残高取得 | InternalServiceOnly |

> **📝 注意**: gRPC サービスの検証は別途 gRPC クライアントツール（grpcurl 等）を使用する。本計画書では REST API の検証を中心に記載する。

---

## 4. 検証手順

検証は以下の順序で実施する。依存関係を考慮した順番となっている。

---

### Phase 0: ヘルスチェック

#### テスト 0-1: GET /health（Liveness）

```bash
curl -s http://localhost:5007/health
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
curl -s http://localhost:5007/health/ready
```

**期待するレスポンス（200 OK）:**

```
Healthy
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] PostgreSQL と Redis の疎通が確認できている

---

### Phase 1: 一般ユーザー向けエンドポイント（ポイント照会）

> **📝 前提**: AuthService でユーザー登録・ログイン済み。`ACCESS_TOKEN`、`USER_ID` を環境変数に設定済み。PointService の `pointdb` にポイントアカウントが存在すること。

#### テスト 1-1: GET /api/v1/points/balance - ポイント残高取得

```bash
curl -s -X GET http://localhost:5007/api/v1/points/balance \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "userId": "uuid-xxx",
  "availablePoints": 10000,
  "pendingPoints": 0,
  "totalEarned": 10000,
  "totalSpent": 0,
  "totalExpired": 0
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] JWT の `sub` クレームと一致するユーザーのポイント情報が返る
- [ ] `availablePoints`、`pendingPoints`、`totalEarned`、`totalSpent`、`totalExpired` が含まれる

#### テスト 1-2: GET /api/v1/points/balance - 認証なし

```bash
curl -s -w "\nHTTP Status: %{http_code}" http://localhost:5007/api/v1/points/balance
```

**期待するレスポンス（401 Unauthorized）:**

**検証項目:**
- [ ] ステータスコード 401 を返す

#### テスト 1-3: GET /api/v1/points/history - ポイント履歴取得

```bash
curl -s -X GET "http://localhost:5007/api/v1/points/history?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "items": [
    {
      "id": "uuid-xxx",
      "type": "ADJUST",
      "points": 10000,
      "balanceAfter": 10000,
      "referenceId": null,
      "referenceType": "ADMIN_ADJUST",
      "description": "検証用初期ポイント付与",
      "expiresAt": null,
      "createdAt": "2026-04-06T10:00:00+00:00"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 10
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] ページネーション情報（`totalCount`、`page`、`pageSize`）が含まれる
- [ ] ポイント取引履歴が返る
- [ ] 各取引に `type`、`points`、`balanceAfter`、`createdAt` が含まれる

#### テスト 1-4: GET /api/v1/points/history - ページネーションバリデーションエラー

```bash
curl -s -X GET "http://localhost:5007/api/v1/points/history?page=0&pageSize=200" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（400 Bad Request）:**

```json
{
  "type": "...",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Page": ["ページ番号は 1 以上で指定してください"],
    "PageSize": ["ページサイズは 1〜100 の範囲で指定してください"]
  }
}
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] バリデーションエラーの詳細が返される

#### テスト 1-5: GET /api/v1/points/tier - ティア情報取得

```bash
curl -s -X GET http://localhost:5007/api/v1/points/tier \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "tierName": "BRONZE",
  "earnRateMultiplier": 0.01,
  "totalEarnedPoints": 10000,
  "currentYearPoints": 10000,
  "nextTierName": "SILVER",
  "pointsToNextTier": 40000,
  "benefits": ["送料無料クーポン（年1回）"]
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 現在のティア名（`tierName`）が含まれる
- [ ] ポイント還元率（`earnRateMultiplier`）が含まれる
- [ ] 次ティアへの必要ポイント（`pointsToNextTier`）が含まれる（最上位ティアの場合は `null`）
- [ ] ティア特典（`benefits`）が配列で返る

#### テスト 1-6: GET /api/v1/points/expiring - 期限切れ間近のポイント取得

```bash
curl -s -X GET http://localhost:5007/api/v1/points/expiring \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "items": [],
  "totalExpiringPoints": 0
}
```

> **📝 注意**: 初期状態では期限切れ間近のポイントは存在しない場合がある。ポイント付与後 12 ヶ月の有効期限が設定されるため、実際のテストデータでは空配列が返ることが多い。

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `items` 配列と `totalExpiringPoints` が含まれる
- [ ] 30 日以内に失効するポイントのみが返る

---

### Phase 2: 管理者向けエンドポイント

> **📝 前提**: `ADMIN_TOKEN` を環境変数に設定済み。

#### テスト 2-1: GET /api/v1/admin/points/users/{userId}/balance - 指定ユーザーの残高取得

```bash
curl -s -X GET "http://localhost:5007/api/v1/admin/points/users/${USER_ID}/balance" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "userId": "uuid-xxx",
  "availablePoints": 10000,
  "pendingPoints": 0,
  "totalEarned": 10000,
  "totalSpent": 0,
  "totalExpired": 0
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 指定したユーザーのポイント情報が返る

#### テスト 2-2: GET /api/v1/admin/points/users/{userId}/balance - 一般ユーザーによるアクセス（権限不足）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X GET "http://localhost:5007/api/v1/admin/points/users/${USER_ID}/balance" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（403 Forbidden）:**

**検証項目:**
- [ ] ステータスコード 403 を返す
- [ ] AdminOnly ポリシーが正しく動作している

#### テスト 2-3: POST /api/v1/admin/points/users/{userId}/adjust - ポイント加算

```bash
curl -s -X POST "http://localhost:5007/api/v1/admin/points/users/${USER_ID}/adjust" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "points": 500,
    "reason": "キャンペーン特典ポイント付与"
  }'
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] ユーザーのポイント残高が 500 ポイント増加する

```bash
# 残高確認
curl -s -X GET http://localhost:5007/api/v1/points/balance \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
# availablePoints が 10500 になっていることを確認
```

#### テスト 2-4: POST /api/v1/admin/points/users/{userId}/adjust - ポイント減算

```bash
curl -s -X POST "http://localhost:5007/api/v1/admin/points/users/${USER_ID}/adjust" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "points": -200,
    "reason": "ポイント不正利用による減算"
  }'
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] ユーザーのポイント残高が 200 ポイント減少する

> **📝 注意**: FluentValidation の `AdjustPointsRequestValidator` では `Points` に `NotEqual(0)` と絶対値の上限チェックがあるが、負数も許容される設計。Data Annotation の `[Range(1, int.MaxValue)]` と矛盾する可能性があるため、実際の動作を確認すること。

#### テスト 2-5: POST /api/v1/admin/points/users/{userId}/adjust - バリデーションエラー（ポイント 0）

```bash
curl -s -X POST "http://localhost:5007/api/v1/admin/points/users/${USER_ID}/adjust" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "points": 0,
    "reason": "テスト"
  }'
```

**期待するレスポンス（400 Bad Request）:**

```json
{
  "type": "...",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Points": ["ポイント数は 0 以外である必要があります"]
  }
}
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] バリデーションエラーの詳細が返される

#### テスト 2-6: POST /api/v1/admin/points/users/{userId}/adjust - バリデーションエラー（理由なし）

```bash
curl -s -X POST "http://localhost:5007/api/v1/admin/points/users/${USER_ID}/adjust" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "points": 100
  }'
```

**期待するレスポンス（400 Bad Request）:**

```json
{
  "type": "...",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Reason": ["調整理由は必須です"]
  }
}
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] 調整理由が必須であることが検証される

#### テスト 2-7: POST /api/v1/admin/points/users/{userId}/adjust - 上限超過

```bash
curl -s -X POST "http://localhost:5007/api/v1/admin/points/users/${USER_ID}/adjust" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "points": 200000,
    "reason": "大量ポイント付与テスト"
  }'
```

**期待するレスポンス（400 Bad Request）:**

```json
{
  "type": "...",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Points": ["1 回の調整は 100,000 ポイントが上限です"]
  }
}
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] 1 回の調整上限（100,000 ポイント）が検証される

#### テスト 2-8: GET /api/v1/admin/points/analytics - ポイント分析データ取得

```bash
curl -s -X GET http://localhost:5007/api/v1/admin/points/analytics \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "totalPointsIssued": 10300,
  "totalPointsRedeemed": 0,
  "totalPointsExpired": 0,
  "redemptionRate": 0.0,
  "pointsByTier": {
    "BRONZE": 10300,
    "SILVER": 0,
    "GOLD": 0,
    "PLATINUM": 0
  },
  "tierDistribution": {
    "BRONZE": 1,
    "SILVER": 0,
    "GOLD": 0,
    "PLATINUM": 0
  }
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 発行ポイント総数（`totalPointsIssued`）が含まれる
- [ ] 消費ポイント総数（`totalPointsRedeemed`）が含まれる
- [ ] 失効ポイント総数（`totalPointsExpired`）が含まれる
- [ ] 消費率（`redemptionRate`）が含まれる
- [ ] ティア別ポイント（`pointsByTier`）が含まれる
- [ ] ティア別ユーザー分布（`tierDistribution`）が含まれる

---

### Phase 3: ティア管理

> **📝 前提**: `ADMIN_TOKEN` を環境変数に設定済み。

#### テスト 3-1: GET /api/v1/admin/tiers - ティア定義一覧取得

```bash
curl -s -X GET http://localhost:5007/api/v1/admin/tiers \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
[
  {
    "id": "uuid-bronze",
    "name": "BRONZE",
    "pointRate": 0.01,
    "minAnnualPoints": 0,
    "benefits": "送料無料クーポン（年1回）",
    "sortOrder": 1
  },
  {
    "id": "uuid-silver",
    "name": "SILVER",
    "pointRate": 0.02,
    "minAnnualPoints": 50000,
    "benefits": "送料無料クーポン（年2回）,誕生日ポイント2倍",
    "sortOrder": 2
  },
  {
    "id": "uuid-gold",
    "name": "GOLD",
    "pointRate": 0.03,
    "minAnnualPoints": 100000,
    "benefits": "送料無料,誕生日ポイント3倍,先行セール",
    "sortOrder": 3
  },
  {
    "id": "uuid-platinum",
    "name": "PLATINUM",
    "pointRate": 0.05,
    "minAnnualPoints": 300000,
    "benefits": "送料無料,誕生日ポイント5倍,先行セール,専用サポート",
    "sortOrder": 4
  }
]
```

> **📝 メモ**: 取得したティアの `id` を `TIER_ID` として以降のテストで使用する。

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 4 つのティア定義（BRONZE, SILVER, GOLD, PLATINUM）が返る
- [ ] 各ティアに `id`、`name`、`pointRate`、`minAnnualPoints`、`benefits`、`sortOrder` が含まれる

#### テスト 3-2: PUT /api/v1/admin/tiers/{id} - ティア定義更新

```bash
# TIER_ID には実際のティア ID を設定
export TIER_ID="uuid-bronze"

curl -s -X PUT "http://localhost:5007/api/v1/admin/tiers/${TIER_ID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "pointRate": 0.015,
    "minAnnualPoints": 0,
    "benefits": "送料無料クーポン（年1回）,新規特典追加"
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-bronze",
  "name": "BRONZE",
  "pointRate": 0.015,
  "minAnnualPoints": 0,
  "benefits": "送料無料クーポン（年1回）,新規特典追加",
  "sortOrder": 1
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 更新された `pointRate` と `benefits` がレスポンスに反映される

> **📝 注意**: テスト後に元の値に戻すこと。

```bash
# ティア定義を元に戻す
curl -s -X PUT "http://localhost:5007/api/v1/admin/tiers/${TIER_ID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "pointRate": 0.01,
    "minAnnualPoints": 0,
    "benefits": "送料無料クーポン（年1回）"
  }'
```

#### テスト 3-3: PUT /api/v1/admin/tiers/{id} - バリデーションエラー（ポイントレート範囲外）

```bash
curl -s -X PUT "http://localhost:5007/api/v1/admin/tiers/${TIER_ID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "pointRate": 2.0,
    "minAnnualPoints": 0
  }'
```

**期待するレスポンス（400 Bad Request）:**

```json
{
  "type": "...",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "PointRate": ["ポイントレートは 0.001〜1.0 の範囲で指定してください"]
  }
}
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] ポイントレートの範囲バリデーションが動作する

#### テスト 3-4: PUT /api/v1/admin/tiers/{id} - 存在しないティア ID

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X PUT "http://localhost:5007/api/v1/admin/tiers/non-existent-id" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "pointRate": 0.01,
    "minAnnualPoints": 0
  }'
```

**期待するレスポンス（404 Not Found）:**

```json
{
  "type": "...",
  "title": "Not Found",
  "status": 404,
  "detail": "指定されたティア定義が見つかりません"
}
```

**検証項目:**
- [ ] ステータスコード 404 を返す
- [ ] RFC 9457 Problem Details 形式のエラーが返る

---

### Phase 4: 内部 API（Saga 連携）

> **📝 前提**: `INTERNAL_TOKEN` を環境変数に設定済み。内部サービストークンが利用できない場合はこの Phase をスキップする。

#### テスト 4-1: POST /api/v1/internal/points/reserve - ポイント仮消費

```bash
export ORDER_ID="test-order-$(date +%s)"

curl -s -X POST http://localhost:5007/api/v1/internal/points/reserve \
  -H "Authorization: Bearer ${INTERNAL_TOKEN}" \
  -H "Content-Type: application/json" \
  -d "{
    \"userId\": \"${USER_ID}\",
    \"orderId\": \"${ORDER_ID}\",
    \"points\": 1000
  }"
```

**期待するレスポンス（200 OK）:**

```json
{
  "success": true,
  "remainingBalance": 9300,
  "errorMessage": null
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `success` が `true`
- [ ] `remainingBalance` が仮消費後の残高になっている
- [ ] ポイント残高の `availablePoints` が減少し、`pendingPoints` が増加する

```bash
# 残高確認
curl -s -X GET http://localhost:5007/api/v1/points/balance \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
# availablePoints が 9300、pendingPoints が 1000 になっていることを確認
```

#### テスト 4-2: POST /api/v1/internal/points/reserve - 残高不足

> **📝 注意**: バリデーション上限（500,000 ポイント）以内で、かつ残高を超える値を指定する。

```bash
curl -s -X POST http://localhost:5007/api/v1/internal/points/reserve \
  -H "Authorization: Bearer ${INTERNAL_TOKEN}" \
  -H "Content-Type: application/json" \
  -d "{
    \"userId\": \"${USER_ID}\",
    \"orderId\": \"test-order-insufficient\",
    \"points\": 100000
  }"
```

**期待するレスポンス（422 Unprocessable Entity）:**

```json
{
  "success": false,
  "remainingBalance": 9300,
  "errorMessage": "ポイント残高不足"
}
```

**検証項目:**
- [ ] ステータスコード 422 を返す
- [ ] `success` が `false`
- [ ] `errorMessage` に残高不足のメッセージが含まれる

#### テスト 4-2a: POST /api/v1/internal/points/reserve - バリデーションエラー（上限超過）

> **📝 注意**: ReservePointsRequest の Points は FluentValidation で 1〜500,000 の範囲に制限されている。

```bash
curl -s -X POST http://localhost:5007/api/v1/internal/points/reserve \
  -H "Authorization: Bearer ${INTERNAL_TOKEN}" \
  -H "Content-Type: application/json" \
  -d "{
    \"userId\": \"${USER_ID}\",
    \"orderId\": \"test-order-over-limit\",
    \"points\": 600000
  }"
```

**期待するレスポンス（400 Bad Request）:**

```json
{
  "type": "...",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Points": ["'Points' must be less than or equal to '500000'."]
  }
}
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] バリデーションエラーの詳細が返される
- [ ] 500,000 ポイントの上限が検証されている

#### テスト 4-3: POST /api/v1/internal/points/confirm - ポイント消費確定

```bash
curl -s -X POST http://localhost:5007/api/v1/internal/points/confirm \
  -H "Authorization: Bearer ${INTERNAL_TOKEN}" \
  -H "Content-Type: application/json" \
  -d "{
    \"userId\": \"${USER_ID}\",
    \"orderId\": \"${ORDER_ID}\"
  }"
```

**期待するレスポンス（200 OK）:**

```json
{
  "confirmedPoints": 1000
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `confirmedPoints` に確定されたポイント数が返る
- [ ] `pendingPoints` が 0 になる
- [ ] `totalSpent` が増加する

```bash
# 残高確認
curl -s -X GET http://localhost:5007/api/v1/points/balance \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
# pendingPoints が 0、totalSpent が 1000 になっていることを確認
```

#### テスト 4-4: POST /api/v1/internal/points/release - ポイント仮消費解放

まず新しい仮消費を作成してから解放する。

```bash
export ORDER_ID_RELEASE="test-order-release-$(date +%s)"

# 仮消費
curl -s -X POST http://localhost:5007/api/v1/internal/points/reserve \
  -H "Authorization: Bearer ${INTERNAL_TOKEN}" \
  -H "Content-Type: application/json" \
  -d "{
    \"userId\": \"${USER_ID}\",
    \"orderId\": \"${ORDER_ID_RELEASE}\",
    \"points\": 500
  }"

# 解放
curl -s -X POST http://localhost:5007/api/v1/internal/points/release \
  -H "Authorization: Bearer ${INTERNAL_TOKEN}" \
  -H "Content-Type: application/json" \
  -d "{
    \"userId\": \"${USER_ID}\",
    \"orderId\": \"${ORDER_ID_RELEASE}\"
  }"
```

**期待するレスポンス（200 OK）:**

```json
{
  "releasedPoints": 500
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `releasedPoints` に解放されたポイント数が返る
- [ ] `availablePoints` が復元される
- [ ] `pendingPoints` が減少する

#### テスト 4-5: POST /api/v1/internal/points/award - ポイント付与

```bash
export AWARD_ORDER_ID="award-order-$(date +%s)"

curl -s -X POST http://localhost:5007/api/v1/internal/points/award \
  -H "Authorization: Bearer ${INTERNAL_TOKEN}" \
  -H "Content-Type: application/json" \
  -d "{
    \"userId\": \"${USER_ID}\",
    \"orderId\": \"${AWARD_ORDER_ID}\",
    \"orderAmount\": 50000
  }"
```

**期待するレスポンス（200 OK）:**

```json
{
  "earnedPoints": 500
}
```

> **📝 注意**: 付与ポイント数は `orderAmount * pointRate * campaignMultiplier` で計算される。BRONZE ティア（pointRate=0.01）の場合、50000 * 0.01 = 500 ポイント。

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `earnedPoints` に付与されたポイント数が返る
- [ ] `availablePoints` が増加する
- [ ] `totalEarned` が増加する

#### テスト 4-6: POST /api/v1/internal/points/award - 冪等性確認（同じ注文 ID で再実行）

```bash
curl -s -X POST http://localhost:5007/api/v1/internal/points/award \
  -H "Authorization: Bearer ${INTERNAL_TOKEN}" \
  -H "Content-Type: application/json" \
  -d "{
    \"userId\": \"${USER_ID}\",
    \"orderId\": \"${AWARD_ORDER_ID}\",
    \"orderAmount\": 50000
  }"
```

**期待するレスポンス（200 OK）:**

```json
{
  "earnedPoints": 500
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] ポイントが重複付与されない（同じ注文 ID の場合は既存のポイント数が返る）
- [ ] 冪等性が保証されている

#### テスト 4-7: GET /api/v1/internal/points/users/{userId}/balance - 内部残高取得

```bash
curl -s -X GET "http://localhost:5007/api/v1/internal/points/users/${USER_ID}/balance" \
  -H "Authorization: Bearer ${INTERNAL_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "userId": "uuid-xxx",
  "availablePoints": 9800,
  "pendingPoints": 0,
  "totalEarned": 10800,
  "totalSpent": 1000,
  "totalExpired": 0
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 指定ユーザーのポイント情報が返る

#### テスト 4-8: 内部 API - 一般ユーザートークンでのアクセス（権限不足）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5007/api/v1/internal/points/reserve \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "test",
    "orderId": "test",
    "points": 100
  }'
```

**期待するレスポンス（403 Forbidden）:**

**検証項目:**
- [ ] ステータスコード 403 を返す
- [ ] InternalServiceOnly ポリシーが正しく動作している

---

### Phase 5: エラーケース・エッジケース

#### テスト 5-1: 存在しないユーザーのポイント残高取得（管理者）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X GET "http://localhost:5007/api/v1/admin/points/users/non-existent-user-id/balance" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（404 Not Found）:**

```json
{
  "type": "...",
  "title": "Not Found",
  "status": 404,
  "detail": "ポイントアカウントが見つかりません"
}
```

**検証項目:**
- [ ] ステータスコード 404 を返す
- [ ] RFC 9457 Problem Details 形式のエラーが返る

#### テスト 5-2: 無効な JSON リクエスト

```bash
curl -s -X POST "http://localhost:5007/api/v1/admin/points/users/${USER_ID}/adjust" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d 'invalid json'
```

**期待するレスポンス（400 Bad Request）:**

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] JSON パースエラーが適切に処理される

#### テスト 5-3: 楽観的ロック競合（シミュレーション）

> **📝 注意**: 楽観的ロック競合のテストは、複数の同時リクエストを発行する必要がある。以下は概念的なテスト手順。

```bash
# 2 つのターミナルから同時に実行
curl -s -X POST "http://localhost:5007/api/v1/admin/points/users/${USER_ID}/adjust" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"points": 100, "reason": "並行テスト1"}'

curl -s -X POST "http://localhost:5007/api/v1/admin/points/users/${USER_ID}/adjust" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"points": 100, "reason": "並行テスト2"}'
```

**期待する動作:**
- 一方は成功（200 OK）
- 他方は 409 Conflict または再試行後に成功

**検証項目:**
- [ ] 楽観的ロック競合時に適切なエラーが返る
- [ ] データ不整合が発生しない

---

## 5. verification-report.md のフォーマット

検証結果は以下のフォーマットで記録する。

### 5.1 ヘッダー

```markdown
# PointService 検証レポート

| 項目 | 内容 |
|------|------|
| 検証日時 | 2026-04-06 10:00 |
| 検証者 | [名前] |
| 環境 | Docker Compose (localhost) |
| PointService バージョン | x.x.x |
| 総テスト数 | XX |
| 成功数 | XX |
| 失敗数 | XX |
| スキップ数 | XX |
```

### 5.2 サマリテーブル

```markdown
## サマリ

| Phase | カテゴリ | テスト数 | 成功 | 失敗 | スキップ |
|-------|---------|---------|------|------|---------|
| 0 | ヘルスチェック | 2 | 2 | 0 | 0 |
| 1 | 一般ユーザー向け | 6 | 6 | 0 | 0 |
| 2 | 管理者向け | 8 | 8 | 0 | 0 |
| 3 | ティア管理 | 4 | 4 | 0 | 0 |
| 4 | 内部 API | 9 | 9 | 0 | 0 |
| 5 | エラーケース | 3 | 3 | 0 | 0 |
| **合計** | | **32** | **32** | **0** | **0** |
```

### 5.3 詳細結果

各テストについて以下の情報を記録する:

1. **テスト ID**: テスト番号（例: 1-1）
2. **テスト名**: テストの説明
3. **結果**: ✅ 成功 / ❌ 失敗 / ⏭️ スキップ
4. **リクエスト**: 実際に送信した curl コマンド
5. **レスポンス**: 受信したレスポンス（ステータスコード + ボディ）
6. **備考**: 問題があった場合の詳細、特記事項

---

## 6. テスト用データ

### 6.1 テストユーザー

| 役割 | メールアドレス | パスワード | 用途 |
|------|-------------|----------|------|
| 一般ユーザー | testuser@example.com | Password123! | 一般機能テスト |
| 管理者 | admin@example.com | AdminPass123! | 管理者機能テスト |

### 6.2 テスト用ポイントデータ

| フィールド | 値 |
|---------|------|
| 初期ポイント | 10000 |
| 調整ポイント（加算） | 500 |
| 調整ポイント（減算） | -200 |
| 仮消費ポイント | 1000 |
| 解放ポイント | 500 |

### 6.3 テスト用ティアデータ

| ティア名 | ポイントレート | 最小年間ポイント |
|---------|-------------|----------------|
| BRONZE | 0.01 (1%) | 0 |
| SILVER | 0.02 (2%) | 50,000 |
| GOLD | 0.03 (3%) | 100,000 |
| PLATINUM | 0.05 (5%) | 300,000 |

### 6.4 テスト用注文データ（内部 API 用）

| フィールド | 値 |
|---------|------|
| orderId | test-order-{timestamp} |
| orderAmount | 50000 |
| points（仮消費） | 1000 |

---

## 7. トラブルシューティング

### 7.1 よくある問題

#### ヘルスチェックが失敗する

```bash
# コンテナのログを確認
docker logs skishop-point-service

# データベース接続を確認
docker exec -it skishop-postgres psql -U skishop -d pointdb -c "\dt"

# Redis 接続を確認
docker exec -it skishop-redis redis-cli ping
```

#### 401 Unauthorized が返される

- JWT トークンの有効期限を確認する（デフォルト 3600 秒）
- AuthService と PointService で同じ `Jwt__SigningKey` が設定されているか確認する
- `Authorization: Bearer` プレフィックスが正しいか確認する
- 必要に応じて AuthService で再ログインしてトークンを再取得する

```bash
# トークン再取得
curl -s -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email": "testuser@example.com", "password": "Password123!"}'
```

#### 403 Forbidden が返される

- ユーザーロールを確認する（管理者エンドポイントには `Admin` ロールが必要）
- 内部 API には `scope=internal` クレームが必要
- JWT のクレームに正しいユーザー ID と ロールが含まれているか確認する

```bash
# JWT トークンのペイロードをデコード（bash）
echo "${ACCESS_TOKEN}" | cut -d. -f2 | base64 -d 2>/dev/null | python3 -m json.tool
```

#### ポイントアカウントが存在しない（404）

PointService は Kafka イベント（`user.registered`）経由でポイントアカウントを自動作成する。

```bash
# pointdb にポイントアカウントが存在するか確認
docker exec -it skishop-postgres psql -U skishop -d pointdb \
  -c "SELECT id, user_id, available_points FROM point_accounts;"

# 存在しない場合、手動で挿入
docker exec -it skishop-postgres psql -U skishop -d pointdb \
  -c "INSERT INTO point_accounts (id, user_id, available_points, pending_points, total_earned, total_spent, total_expired, created_at, updated_at)
      VALUES (gen_random_uuid(), '${USER_ID}', 0, 0, 0, 0, 0, NOW(), NOW())
      ON CONFLICT (user_id) DO NOTHING;"
```

#### ティア定義が存在しない

```bash
# ティア定義を確認
docker exec -it skishop-postgres psql -U skishop -d pointdb \
  -c "SELECT id, name, point_rate, min_annual_points FROM tier_definitions ORDER BY sort_order;"

# 存在しない場合、シードデータを挿入
docker exec -it skishop-postgres psql -U skishop -d pointdb \
  -c "INSERT INTO tier_definitions (id, name, point_rate, min_annual_points, benefits, sort_order, created_at, updated_at) VALUES
      (gen_random_uuid(), 'BRONZE', 0.01, 0, '送料無料クーポン（年1回）', 1, NOW(), NOW()),
      (gen_random_uuid(), 'SILVER', 0.02, 50000, '送料無料クーポン（年2回）,誕生日ポイント2倍', 2, NOW(), NOW()),
      (gen_random_uuid(), 'GOLD', 0.03, 100000, '送料無料,誕生日ポイント3倍,先行セール', 3, NOW(), NOW()),
      (gen_random_uuid(), 'PLATINUM', 0.05, 300000, '送料無料,誕生日ポイント5倍,先行セール,専用サポート', 4, NOW(), NOW())
      ON CONFLICT (name) DO NOTHING;"
```

#### バリデーションエラーだが Content-Type が正しい

- リクエストボディの JSON 形式を確認する
- `[Required]` 属性のフィールドが含まれているか確認する
- 数値の範囲制約を確認する

#### 内部 API にアクセスできない（InternalServiceOnly）

- 内部サービス用トークンが正しく発行されているか確認する
- JWT に `scope=internal` クレームが含まれているか確認する
- テスト環境で内部トークンが利用できない場合は、Phase 4 をスキップする

---

## 8. 実行手順チェックリスト

検証実施時に以下の順序で進める:

- [ ] Docker Compose で環境を起動（`docker compose up -d auth-service point-service`）
- [ ] AuthService ヘルスチェック確認（`curl -s http://localhost:5001/health`）
- [ ] PointService ヘルスチェック確認（`curl -s http://localhost:5007/health`）
- [ ] AuthService でテストユーザー作成・トークン取得（一般ユーザー + 管理者）
- [ ] PointService の pointdb にポイントアカウントが存在するか確認
- [ ] ティア定義が存在するか確認
- [ ] テスト用ポイントを付与（管理者によるポイント調整）
- [ ] Phase 0: ヘルスチェックを実施
- [ ] Phase 1: 一般ユーザー向けエンドポイントを検証
- [ ] Phase 2: 管理者向けエンドポイントを検証
- [ ] Phase 3: ティア管理を検証
- [ ] Phase 4: 内部 API を検証（内部トークンが利用可能な場合）
- [ ] Phase 5: エラーケース・エッジケースを検証
- [ ] verification-report.md に結果を記録
