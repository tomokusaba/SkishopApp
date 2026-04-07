# UserManagementService 検証計画書（verification-plan.md）

本ドキュメントは UserManagementService の全エンドポイントに対する動作検証の手順とフォーマットを定義する。
検証結果は `verification-report.md` に記録し、UserManagementService の使い方を理解するためのリファレンスとして活用する。

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

1. **機能確認**: 全 27 エンドポイント（+ 2 ヘルスチェック）が設計通りに動作することを確認する
2. **使い方の文書化**: 各 API の呼び出し方法・パラメータ・レスポンスを実例で示す
3. **異常系の確認**: バリデーションエラー、認証エラー、認可エラー（IDOR 防止）等の動作を確認する
4. **セキュリティ検証**: IDOR 保護、管理者専用エンドポイントのアクセス制御を確認する

---

## 2. 前提条件

### 2.1 環境構成

| 項目 | 値 |
|------|-----|
| UserManagementService URL | `http://localhost:5002` |
| AuthService URL | `http://localhost:5001` |
| PostgreSQL | `localhost:5432`（DB: `userdb`） |
| Redis | `localhost:6379` |
| Kafka | `localhost:9092` |

### 2.2 起動コマンド

```bash
# Docker Compose で UserManagementService と依存サービスを起動
cd /Users/yoterada/GitHub/DotNet-Skishop-App
docker compose up -d auth-service user-management-service
```

> **📝 注意**: UserManagementService は AuthService が発行した JWT トークンを使用して認証するため、AuthService も同時に起動する必要がある。

### 2.3 事前確認コマンド

```bash
# UserManagementService ヘルスチェック
curl -s http://localhost:5002/health
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

### 2.5 UserManagementService 用のユーザーデータ初期化

AuthService で作成したユーザーは AuthService の DB（`authdb`）にのみ存在する。
UserManagementService の DB（`userdb`）にも対応するユーザーレコードが必要になる場合がある。

> **📝 注意**: UserManagementService は Kafka イベント（`user.registered`）を購読してユーザーを自動登録する設計。AuthService でのユーザー登録後、Kafka 経由で UserManagementService にユーザーが作成されているか確認すること。手動でレコードを作成する場合は、以下の SQL を使用する。

```bash
docker exec -it skishop-postgres psql -U skishop -d userdb \
  -c "INSERT INTO users (id, email, first_name, last_name, status, created_at, updated_at)
      VALUES ('${USER_ID}', 'testuser@example.com', 'Test', 'User', 'ACTIVE', NOW(), NOW())
      ON CONFLICT (id) DO NOTHING;"
```

---

## 3. エンドポイント一覧

### 3.1 ヘルスチェック

| メソッド | パス | 説明 | 認証 |
|---------|------|------|------|
| GET | `/health` | Liveness チェック | 不要 |
| GET | `/health/ready` | Readiness チェック（PostgreSQL, Redis 疎通） | 不要 |

### 3.2 ユーザープロファイル（UserEndpoints）

| メソッド | パス | 説明 | 認証 | 備考 |
|---------|------|------|------|------|
| GET | `/api/v1/users/me` | 現在のユーザー情報取得 | 必要 | |
| GET | `/api/v1/users/{id}` | ユーザー情報取得 | 必要 | Admin: 全ユーザー / User: 自分のみ |
| PUT | `/api/v1/users/{id}` | ユーザー情報更新 | 必要 | Admin: 全ユーザー / User: 自分のみ |

### 3.3 管理者向け（AdminUserEndpoints）

| メソッド | パス | 説明 | 認証 | 備考 |
|---------|------|------|------|------|
| GET | `/api/v1/admin/users` | ユーザー一覧取得 | Admin のみ | ページネーション対応 |
| PUT | `/api/v1/admin/users/{id}/status` | ユーザーステータス変更 | Admin のみ | |
| PUT | `/api/v1/admin/users/{id}/processing-restriction` | 処理制限設定 | Admin のみ | |

### 3.4 住所管理（AddressEndpoints）

| メソッド | パス | 説明 | 認証 | 備考 |
|---------|------|------|------|------|
| GET | `/api/v1/users/{userId}/addresses` | 住所一覧取得 | 必要 | Admin: 全ユーザー / User: 自分のみ |
| POST | `/api/v1/users/{userId}/addresses` | 住所追加 | 必要 | Owner のみ |
| PUT | `/api/v1/users/{userId}/addresses/{id}` | 住所更新 | 必要 | Owner のみ |
| DELETE | `/api/v1/users/{userId}/addresses/{id}` | 住所削除 | 必要 | Owner のみ |

### 3.5 アクティビティ（ActivityEndpoints）

| メソッド | パス | 説明 | 認証 | 備考 |
|---------|------|------|------|------|
| GET | `/api/v1/users/{id}/activities` | アクティビティ一覧取得 | 必要 | Admin: 全ユーザー / User: 自分のみ |
| GET | `/api/v1/users/me/activities` | 自分のアクティビティ取得 | 必要 | |

### 3.6 同意管理（ConsentEndpoints）

| メソッド | パス | 説明 | 認証 | 備考 |
|---------|------|------|------|------|
| GET | `/api/v1/users/{userId}/consents` | 同意一覧取得 | 必要 | Owner のみ |
| PUT | `/api/v1/users/{userId}/consents` | 同意更新 | 必要 | Owner のみ |
| POST | `/api/v1/anonymous-consents` | 匿名同意作成 | **不要** | レート制限: 10 req/min |

### 3.7 GDPR/DSR（DsrEndpoints）

| メソッド | パス | 説明 | 認証 | 備考 |
|---------|------|------|------|------|
| POST | `/api/v1/users/{userId}/deletion-request` | 削除リクエスト作成 | 必要 | Admin: 全ユーザー / User: 自分のみ |
| GET | `/api/v1/users/{userId}/deletion-request` | 削除リクエスト取得 | 必要 | Admin: 全ユーザー / User: 自分のみ |
| POST | `/api/v1/users/{userId}/deletion-request/cancel` | 削除リクエスト取消 | 必要 | Owner のみ |
| POST | `/api/v1/users/{userId}/data-export` | データエクスポート要求 | 必要 | Owner のみ |

### 3.8 会員ランク（MemberRankEndpoints）

| メソッド | パス | 説明 | 認証 | 備考 |
|---------|------|------|------|------|
| GET | `/api/v1/users/{userId}/member-rank` | 会員ランク取得 | 必要 | Admin: 全ユーザー / User: 自分のみ |

### 3.9 ユーザー設定（PreferenceEndpoints）

| メソッド | パス | 説明 | 認証 | 備考 |
|---------|------|------|------|------|
| GET | `/api/v1/users/{userId}/preferences` | 設定取得 | 必要 | Admin: 全ユーザー / User: 自分のみ |
| PUT | `/api/v1/users/{userId}/preferences` | 設定更新 | 必要 | Owner のみ |

### 3.10 ウィッシュリスト（WishlistEndpoints）

| メソッド | パス | 説明 | 認証 | 備考 |
|---------|------|------|------|------|
| GET | `/api/v1/users/{userId}/wishlists` | ウィッシュリスト一覧取得 | 必要 | Owner のみ |
| POST | `/api/v1/users/{userId}/wishlists` | ウィッシュリスト作成 | 必要 | Owner のみ |
| PUT | `/api/v1/users/{userId}/wishlists/{id}` | ウィッシュリスト更新 | 必要 | Owner のみ |
| DELETE | `/api/v1/users/{userId}/wishlists/{id}` | ウィッシュリスト削除 | 必要 | Owner のみ |
| POST | `/api/v1/users/{userId}/wishlists/{id}/items` | アイテム追加 | 必要 | Owner のみ |
| POST | `/api/v1/users/{userId}/wishlists/{id}/items/{itemId}/cart` | カートへ移動 | 必要 | Owner のみ |
| DELETE | `/api/v1/users/{userId}/wishlists/{id}/items/{itemId}` | アイテム削除 | 必要 | Owner のみ |

---

## 4. 検証手順

検証は以下の順序で実施する。依存関係を考慮した順番となっている。

---

### Phase 0: ヘルスチェック

#### テスト 0-1: GET /health（Liveness）

```bash
curl -s http://localhost:5002/health
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
curl -s http://localhost:5002/health/ready
```

**期待するレスポンス（200 OK）:**

```
Healthy
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] PostgreSQL と Redis の疎通が確認できている

---

### Phase 1: ユーザープロファイル

> **📝 前提**: AuthService でユーザー登録・ログイン済み。`ACCESS_TOKEN`、`USER_ID` を環境変数に設定済み。UserManagementService の `userdb` にユーザーレコードが存在すること。

#### テスト 1-1: GET /api/v1/users/me - 現在のユーザー情報取得

```bash
curl -s -X GET http://localhost:5002/api/v1/users/me \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-xxx",
  "email": "testuser@example.com",
  "firstName": "Test",
  "lastName": "User",
  "phoneNumber": null,
  "birthDate": null,
  "status": "ACTIVE",
  "isProcessingRestricted": false,
  "lastLoginAt": null,
  "createdAt": "2026-04-06T10:00:00+00:00",
  "updatedAt": "2026-04-06T10:00:00+00:00"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] JWT の `sub` クレームと一致するユーザー情報が返る
- [ ] パスワード等の秘密情報が含まれない

#### テスト 1-2: GET /api/v1/users/me - 認証なし

```bash
curl -s -w "\nHTTP Status: %{http_code}" http://localhost:5002/api/v1/users/me
```

**期待するレスポンス（401 Unauthorized）:**

**検証項目:**
- [ ] ステータスコード 401 を返す

#### テスト 1-3: GET /api/v1/users/{id} - 自分のユーザー情報取得

```bash
curl -s -X GET http://localhost:5002/api/v1/users/${USER_ID} \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-xxx",
  "email": "testuser@example.com",
  "firstName": "Test",
  "lastName": "User",
  "phoneNumber": null,
  "birthDate": null,
  "status": "ACTIVE",
  "isProcessingRestricted": false,
  "lastLoginAt": null,
  "createdAt": "2026-04-06T10:00:00+00:00",
  "updatedAt": "2026-04-06T10:00:00+00:00"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 自分の ID でアクセス可能

#### テスト 1-4: GET /api/v1/users/{id} - 他ユーザーのプロファイル取得（IDOR 防止）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X GET http://localhost:5002/api/v1/users/other-user-id \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（403 Forbidden）:**

```json
{
  "type": "...",
  "title": "Forbidden",
  "status": 403,
  "detail": "..."
}
```

**検証項目:**
- [ ] ステータスコード 403 を返す
- [ ] IDOR 攻撃が防止されている

#### テスト 1-5: GET /api/v1/users/{id} - 管理者による他ユーザー参照

```bash
curl -s -X GET http://localhost:5002/api/v1/users/${USER_ID} \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 管理者は他ユーザーのプロファイルを参照できる

#### テスト 1-6: PUT /api/v1/users/{id} - ユーザー情報更新

```bash
curl -s -X PUT http://localhost:5002/api/v1/users/${USER_ID} \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "Updated",
    "lastName": "Name",
    "phoneNumber": "090-1234-5678",
    "birthDate": "1990-01-15"
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-xxx",
  "email": "testuser@example.com",
  "firstName": "Updated",
  "lastName": "Name",
  "phoneNumber": "090-1234-5678",
  "birthDate": "1990-01-15",
  "status": "ACTIVE",
  "isProcessingRestricted": false,
  "lastLoginAt": null,
  "createdAt": "2026-04-06T10:00:00+00:00",
  "updatedAt": "2026-04-06T10:30:00+00:00"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 更新されたフィールドがレスポンスに反映される
- [ ] `updatedAt` が更新されている

#### テスト 1-7: PUT /api/v1/users/{id} - 他ユーザーの更新（一般ユーザーによる IDOR 防止）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X PUT http://localhost:5002/api/v1/users/other-user-id \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"firstName": "Hacked"}'
```

**期待するレスポンス（403 Forbidden）:**

**検証項目:**
- [ ] ステータスコード 403 を返す
- [ ] 一般ユーザーは他ユーザーのプロファイルを更新できない

#### テスト 1-8: PUT /api/v1/users/{id} - 管理者による他ユーザーの更新

```bash
curl -s -X PUT http://localhost:5002/api/v1/users/${USER_ID} \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "AdminUpdated",
    "lastName": "ByAdmin"
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-xxx",
  "email": "testuser@example.com",
  "firstName": "AdminUpdated",
  "lastName": "ByAdmin",
  "phoneNumber": "090-1234-5678",
  "birthDate": "1990-01-15",
  "status": "ACTIVE",
  "isProcessingRestricted": false,
  "lastLoginAt": null,
  "createdAt": "2026-04-06T10:00:00+00:00",
  "updatedAt": "2026-04-06T10:45:00+00:00"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 管理者は他ユーザーのプロファイルを更新できる
- [ ] 更新されたフィールドがレスポンスに反映される

> **📝 注意**: テスト後に元の値に戻すこと。

```bash
# 名前を元に戻す
curl -s -X PUT http://localhost:5002/api/v1/users/${USER_ID} \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"firstName": "Updated", "lastName": "Name"}'
```

---

### Phase 2: 管理者向けエンドポイント

> **📝 前提**: `ADMIN_TOKEN` を環境変数に設定済み。

#### テスト 2-1: GET /api/v1/admin/users - ユーザー一覧取得

```bash
curl -s -X GET "http://localhost:5002/api/v1/admin/users?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "items": [
    {
      "id": "uuid-xxx",
      "email": "testuser@example.com",
      "firstName": "Test",
      "lastName": "User",
      "status": "ACTIVE",
      ...
    }
  ],
  "totalCount": 2,
  "page": 1,
  "pageSize": 10
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] ページネーション情報が含まれる
- [ ] 登録済みユーザーが一覧に表示される

#### テスト 2-2: GET /api/v1/admin/users - ステータスフィルタリング

```bash
curl -s -X GET "http://localhost:5002/api/v1/admin/users?page=1&pageSize=10&status=ACTIVE" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `ACTIVE` ステータスのユーザーのみが返される

#### テスト 2-3: GET /api/v1/admin/users - 一般ユーザーによるアクセス（権限不足）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X GET "http://localhost:5002/api/v1/admin/users?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（403 Forbidden）:**

**検証項目:**
- [ ] ステータスコード 403 を返す
- [ ] AdminOnly ポリシーが正しく動作している

#### テスト 2-4: PUT /api/v1/admin/users/{id}/status - ステータス変更

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X PUT http://localhost:5002/api/v1/admin/users/${USER_ID}/status \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "status": "SUSPENDED"
  }'
```

**期待するレスポンス（204 No Content）:**

**検証項目:**
- [ ] ステータスコード 204 を返す
- [ ] ユーザーのステータスが `SUSPENDED` に変更される

> **📝 注意**: テスト後にステータスを `ACTIVE` に戻すこと。

```bash
# ステータスを元に戻す
curl -s -w "\nHTTP Status: %{http_code}" \
  -X PUT http://localhost:5002/api/v1/admin/users/${USER_ID}/status \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"status": "ACTIVE"}'
```

#### テスト 2-5: PUT /api/v1/admin/users/{id}/processing-restriction - 処理制限設定

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X PUT http://localhost:5002/api/v1/admin/users/${USER_ID}/processing-restriction \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "isProcessingRestricted": true,
    "restrictionReason": "GDPR Article 18 に基づく制限"
  }'
```

**期待するレスポンス（204 No Content）:**

**検証項目:**
- [ ] ステータスコード 204 を返す
- [ ] ユーザーの `isProcessingRestricted` が `true` に変更される

> **📝 注意**: テスト後に処理制限を解除すること。

```bash
# 処理制限を解除
curl -s -w "\nHTTP Status: %{http_code}" \
  -X PUT http://localhost:5002/api/v1/admin/users/${USER_ID}/processing-restriction \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"isProcessingRestricted": false}'
```

#### テスト 2-6: PUT /api/v1/admin/users/{id}/status - バリデーションエラー

```bash
curl -s -X PUT http://localhost:5002/api/v1/admin/users/${USER_ID}/status \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{}'
```

**期待するレスポンス（400 Bad Request）:**

```json
{
  "type": "...",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Status": ["..."]
  }
}
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] バリデーションエラーの詳細が返される

---

### Phase 3: 住所管理

#### テスト 3-1: POST /api/v1/users/{userId}/addresses - 住所追加

```bash
curl -s -X POST http://localhost:5002/api/v1/users/${USER_ID}/addresses \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "addressType": "HOME",
    "recipient": "山田 太郎",
    "zipCode": "100-0001",
    "prefecture": "東京都",
    "city": "千代田区",
    "streetAddress": "千代田1-1-1",
    "building": "スカイタワー 301号室",
    "phoneNumber": "03-1234-5678"
  }'
```

**期待するレスポンス（201 Created）:**

```json
{
  "id": "uuid-xxx",
  "userId": "uuid-xxx",
  "addressType": "HOME",
  "recipient": "山田 太郎",
  "zipCode": "100-0001",
  "prefecture": "東京都",
  "city": "千代田区",
  "streetAddress": "千代田1-1-1",
  "building": "スカイタワー 301号室",
  "phoneNumber": "03-1234-5678",
  "isDefault": false,
  "createdAt": "2026-04-06T10:00:00+00:00"
}
```

**検証項目:**
- [ ] ステータスコード 201 を返す
- [ ] `Location` ヘッダーが含まれる
- [ ] 登録した住所情報がレスポンスに含まれる

> **📝 メモ**: 取得した住所の `id` を `ADDRESS_ID` として以降のテストで使用する。

#### テスト 3-2: POST /api/v1/users/{userId}/addresses - バリデーションエラー

```bash
curl -s -X POST http://localhost:5002/api/v1/users/${USER_ID}/addresses \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "addressType": "",
    "recipient": "",
    "zipCode": "",
    "prefecture": "",
    "city": "",
    "streetAddress": ""
  }'
```

**期待するレスポンス（400 Bad Request）:**

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] 必須フィールドごとのバリデーションエラーが含まれる

#### テスト 3-3: GET /api/v1/users/{userId}/addresses - 住所一覧取得

```bash
curl -s -X GET http://localhost:5002/api/v1/users/${USER_ID}/addresses \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
[
  {
    "id": "uuid-xxx",
    "userId": "uuid-xxx",
    "addressType": "HOME",
    "recipient": "山田 太郎",
    ...
  }
]
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 先ほど追加した住所が一覧に含まれる

#### テスト 3-4: PUT /api/v1/users/{userId}/addresses/{id} - 住所更新

```bash
curl -s -X PUT http://localhost:5002/api/v1/users/${USER_ID}/addresses/${ADDRESS_ID} \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "recipient": "山田 花子",
    "isDefault": true
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-xxx",
  "userId": "uuid-xxx",
  "addressType": "HOME",
  "recipient": "山田 花子",
  ...
  "isDefault": true,
  ...
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `recipient` が更新されている
- [ ] `isDefault` が `true` に変更されている

#### テスト 3-5: DELETE /api/v1/users/{userId}/addresses/{id} - 住所削除

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X DELETE http://localhost:5002/api/v1/users/${USER_ID}/addresses/${ADDRESS_ID} \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（204 No Content）:**

**検証項目:**
- [ ] ステータスコード 204 を返す
- [ ] 削除後に GET で 404 または空リストが返る

#### テスト 3-6: POST /api/v1/users/{userId}/addresses - 他ユーザーへの住所追加（IDOR 防止）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5002/api/v1/users/other-user-id/addresses \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "addressType": "HOME",
    "recipient": "Hacker",
    "zipCode": "000-0000",
    "prefecture": "東京都",
    "city": "渋谷区",
    "streetAddress": "渋谷1-1-1"
  }'
```

**期待するレスポンス（403 Forbidden）:**

**検証項目:**
- [ ] ステータスコード 403 を返す
- [ ] IDOR 攻撃が防止されている

---

### Phase 4: アクティビティ

#### テスト 4-1: GET /api/v1/users/me/activities - 自分のアクティビティ取得

```bash
curl -s -X GET "http://localhost:5002/api/v1/users/me/activities?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "items": [
    {
      "id": "uuid-xxx",
      "activityType": "PROFILE_UPDATED",
      "timestamp": "2026-04-06T10:30:00+00:00",
      "details": "..."
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 10
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] ページネーション情報が含まれる
- [ ] Phase 1 のプロファイル更新がアクティビティとして記録されている

#### テスト 4-2: GET /api/v1/users/{id}/activities - ID 指定でアクティビティ取得

```bash
curl -s -X GET "http://localhost:5002/api/v1/users/${USER_ID}/activities?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 自分の ID で取得可能

#### テスト 4-3: GET /api/v1/users/{id}/activities - 他ユーザーのアクティビティ取得（IDOR 防止）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X GET "http://localhost:5002/api/v1/users/other-user-id/activities?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（403 Forbidden）:**

**検証項目:**
- [ ] ステータスコード 403 を返す

#### テスト 4-4: GET /api/v1/users/{id}/activities - 管理者による他ユーザーのアクティビティ取得

```bash
curl -s -X GET "http://localhost:5002/api/v1/users/${USER_ID}/activities?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] Admin は他ユーザーのアクティビティを参照できる
- [ ] Admin の場合、`AdminActivityDto`（`ipAddress`, `deviceInfo` 付き）で返る可能性がある

#### テスト 4-5: GET /api/v1/users/me/activities - ページネーションバリデーション

```bash
curl -s -X GET "http://localhost:5002/api/v1/users/me/activities?page=0&pageSize=200" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（400 Bad Request）:**

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] ページ番号やページサイズの不正値に対してバリデーションエラーが返る

---

### Phase 5: 同意管理

#### テスト 5-1: PUT /api/v1/users/{userId}/consents - 同意の付与

```bash
curl -s -X PUT http://localhost:5002/api/v1/users/${USER_ID}/consents \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "consentType": "MARKETING",
    "isGranted": true,
    "policyVersion": 1
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-xxx",
  "consentType": "MARKETING",
  "isGranted": true,
  "version": 1,
  "updatedAt": "2026-04-06T10:00:00+00:00"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 同意情報が正しく保存される

#### テスト 5-2: GET /api/v1/users/{userId}/consents - 同意一覧取得

```bash
curl -s -X GET http://localhost:5002/api/v1/users/${USER_ID}/consents \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
[
  {
    "id": "uuid-xxx",
    "consentType": "MARKETING",
    "isGranted": true,
    "version": 1,
    "updatedAt": "2026-04-06T10:00:00+00:00"
  }
]
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 先ほど付与した同意が一覧に含まれる

#### テスト 5-3: PUT /api/v1/users/{userId}/consents - 同意の取消

```bash
curl -s -X PUT http://localhost:5002/api/v1/users/${USER_ID}/consents \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "consentType": "MARKETING",
    "isGranted": false,
    "policyVersion": 1
  }'
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `isGranted` が `false` に変更される

#### テスト 5-4: PUT /api/v1/users/{userId}/consents - バリデーションエラー

```bash
curl -s -X PUT http://localhost:5002/api/v1/users/${USER_ID}/consents \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{}'
```

**期待するレスポンス（400 Bad Request）:**

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] `consentType` が必須であるバリデーションエラーが含まれる

#### テスト 5-5: POST /api/v1/anonymous-consents - 匿名同意作成（認証不要）

```bash
curl -s -X POST http://localhost:5002/api/v1/anonymous-consents \
  -H "Content-Type: application/json" \
  -d '{
    "consentType": "COOKIE_TRACKING",
    "isGranted": true,
    "policyVersion": 1
  }'
```

**期待するレスポンス（201 Created）:**

```json
{
  "id": "uuid-xxx",
  "consentType": "COOKIE_TRACKING",
  "isGranted": true,
  "version": 1,
  "updatedAt": "2026-04-06T10:00:00+00:00"
}
```

**検証項目:**
- [ ] ステータスコード 201 を返す
- [ ] 認証ヘッダーなしでアクセス可能
- [ ] レート制限（10 req/min）が適用されている

#### テスト 5-6: POST /api/v1/anonymous-consents - レート制限確認

```bash
# 11 回連続でリクエストを送信してレート制限を確認
for i in $(seq 1 11); do
  echo "Request $i:"
  curl -s -w " HTTP Status: %{http_code}\n" \
    -X POST http://localhost:5002/api/v1/anonymous-consents \
    -H "Content-Type: application/json" \
    -d '{"consentType": "COOKIE_TRACKING", "isGranted": true, "policyVersion": 1}'
done
```

**期待するレスポンス（最後のリクエストで 429 Too Many Requests）:**

**検証項目:**
- [ ] 11 回目のリクエストでステータスコード 429 が返る
- [ ] レート制限が `anonymous-consent`（10 req/min）で正しく動作している

---

### Phase 6: ユーザー設定

#### テスト 6-1: GET /api/v1/users/{userId}/preferences - 設定取得

```bash
curl -s -X GET http://localhost:5002/api/v1/users/${USER_ID}/preferences \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-xxx",
  "userId": "uuid-xxx",
  "language": "ja",
  "currency": "JPY",
  "notificationPreferences": null,
  "displayPreferences": null,
  "updatedAt": "2026-04-06T10:00:00+00:00"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] デフォルト設定値が返る

#### テスト 6-2: PUT /api/v1/users/{userId}/preferences - 設定更新

```bash
curl -s -X PUT http://localhost:5002/api/v1/users/${USER_ID}/preferences \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "language": "en",
    "currency": "USD",
    "notificationPreferences": "{\"email\": true, \"push\": false}",
    "displayPreferences": "{\"theme\": \"dark\", \"itemsPerPage\": 20}"
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-xxx",
  "userId": "uuid-xxx",
  "language": "en",
  "currency": "USD",
  "notificationPreferences": "{\"email\": true, \"push\": false}",
  "displayPreferences": "{\"theme\": \"dark\", \"itemsPerPage\": 20}",
  "updatedAt": "2026-04-06T10:30:00+00:00"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 更新されたフィールドがレスポンスに反映される

#### テスト 6-3: PUT /api/v1/users/{userId}/preferences - 他ユーザーの設定更新（IDOR 防止）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X PUT http://localhost:5002/api/v1/users/other-user-id/preferences \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"language": "zh"}'
```

**期待するレスポンス（403 Forbidden）:**

**検証項目:**
- [ ] ステータスコード 403 を返す

---

### Phase 7: 会員ランク

#### テスト 7-1: GET /api/v1/users/{userId}/member-rank - 会員ランク取得

```bash
curl -s -X GET http://localhost:5002/api/v1/users/${USER_ID}/member-rank \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-xxx",
  "userId": "uuid-xxx",
  "currentRank": "REGULAR",
  "annualPurchaseAmount": 0,
  "previousYearAmount": 0,
  "pointRate": 0.01,
  "rankUpdatedAt": "2026-04-06T10:00:00+00:00",
  "nextEvaluationDate": "2027-04-01"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 初期ランクが `REGULAR` である
- [ ] 各フィールドが正しい型で返される

#### テスト 7-2: GET /api/v1/users/{userId}/member-rank - 他ユーザーの会員ランク取得（IDOR 防止）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X GET http://localhost:5002/api/v1/users/other-user-id/member-rank \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（403 Forbidden）:**

**検証項目:**
- [ ] ステータスコード 403 を返す

#### テスト 7-3: GET /api/v1/users/{userId}/member-rank - 管理者による他ユーザーの会員ランク取得

```bash
curl -s -X GET http://localhost:5002/api/v1/users/${USER_ID}/member-rank \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 管理者は他ユーザーの会員ランクを参照できる

---

### Phase 8: ウィッシュリスト

#### テスト 8-1: POST /api/v1/users/{userId}/wishlists - ウィッシュリスト作成

```bash
curl -s -X POST http://localhost:5002/api/v1/users/${USER_ID}/wishlists \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "スキー用品リスト",
    "isDefault": true
  }'
```

**期待するレスポンス（201 Created）:**

```json
{
  "id": "uuid-xxx",
  "userId": "uuid-xxx",
  "name": "スキー用品リスト",
  "isDefault": true,
  "items": [],
  "createdAt": "2026-04-06T10:00:00+00:00"
}
```

**検証項目:**
- [ ] ステータスコード 201 を返す
- [ ] `Location` ヘッダーが含まれる
- [ ] `items` が空配列で初期化されている

> **📝 メモ**: 取得したウィッシュリストの `id` を `WISHLIST_ID` として以降のテストで使用する。

#### テスト 8-2: POST /api/v1/users/{userId}/wishlists - バリデーションエラー

```bash
curl -s -X POST http://localhost:5002/api/v1/users/${USER_ID}/wishlists \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{}'
```

**期待するレスポンス（400 Bad Request）:**

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] `name` が必須であるバリデーションエラーが含まれる

#### テスト 8-3: GET /api/v1/users/{userId}/wishlists - ウィッシュリスト一覧取得

```bash
curl -s -X GET http://localhost:5002/api/v1/users/${USER_ID}/wishlists \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
[
  {
    "id": "uuid-xxx",
    "userId": "uuid-xxx",
    "name": "スキー用品リスト",
    "isDefault": true,
    "items": [],
    "createdAt": "2026-04-06T10:00:00+00:00"
  }
]
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 作成したウィッシュリストが一覧に含まれる

#### テスト 8-4: PUT /api/v1/users/{userId}/wishlists/{id} - ウィッシュリスト更新

```bash
curl -s -X PUT http://localhost:5002/api/v1/users/${USER_ID}/wishlists/${WISHLIST_ID} \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "今シーズンの欲しいもの"
  }'
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `name` が更新されている

#### テスト 8-5: POST /api/v1/users/{userId}/wishlists/{id}/items - アイテム追加

```bash
curl -s -X POST http://localhost:5002/api/v1/users/${USER_ID}/wishlists/${WISHLIST_ID}/items \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "productId": "test-product-001",
    "shouldNotifyOnRestock": true
  }'
```

**期待するレスポンス（201 Created）:**

```json
{
  "id": "uuid-xxx",
  "productId": "test-product-001",
  "addedAt": "2026-04-06T10:00:00+00:00",
  "shouldNotifyOnRestock": true,
  "notifiedAt": null
}
```

**検証項目:**
- [ ] ステータスコード 201 を返す
- [ ] `Location` ヘッダーが含まれる
- [ ] `shouldNotifyOnRestock` が `true` に設定されている

> **📝 メモ**: 取得したアイテムの `id` を `ITEM_ID` として以降のテストで使用する。

#### テスト 8-6: POST /api/v1/users/{userId}/wishlists/{id}/items/{itemId}/cart - カートへ移動

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5002/api/v1/users/${USER_ID}/wishlists/${WISHLIST_ID}/items/${ITEM_ID}/cart \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] アイテムがカートに移動される（PaymentCartService との連携）

> **📝 注意**: PaymentCartService が稼働していない場合は、エラーレスポンスになる可能性がある。その場合はエラー内容を記録する。

#### テスト 8-7: DELETE /api/v1/users/{userId}/wishlists/{id}/items/{itemId} - アイテム削除

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X DELETE http://localhost:5002/api/v1/users/${USER_ID}/wishlists/${WISHLIST_ID}/items/${ITEM_ID} \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（204 No Content）:**

**検証項目:**
- [ ] ステータスコード 204 を返す
- [ ] ウィッシュリストからアイテムが削除される

#### テスト 8-8: DELETE /api/v1/users/{userId}/wishlists/{id} - ウィッシュリスト削除

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X DELETE http://localhost:5002/api/v1/users/${USER_ID}/wishlists/${WISHLIST_ID} \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（204 No Content）:**

**検証項目:**
- [ ] ステータスコード 204 を返す

#### テスト 8-9: POST /api/v1/users/{userId}/wishlists - 他ユーザーのウィッシュリスト作成（IDOR 防止）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5002/api/v1/users/other-user-id/wishlists \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"name": "Hacked Wishlist"}'
```

**期待するレスポンス（403 Forbidden）:**

**検証項目:**
- [ ] ステータスコード 403 を返す
- [ ] IDOR 攻撃が防止されている

---

### Phase 9: GDPR/DSR（データ主体権利）

#### テスト 9-1: POST /api/v1/users/{userId}/deletion-request - 削除リクエスト作成

```bash
curl -s -X POST http://localhost:5002/api/v1/users/${USER_ID}/deletion-request \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "requestChannel": "WEB"
  }'
```

**期待するレスポンス（201 Created）:**

```json
{
  "id": "uuid-xxx",
  "status": "PENDING",
  "requestedAt": "2026-04-06T10:00:00+00:00",
  "gracePeriodEndsAt": "2026-04-20T10:00:00+00:00",
  "completedAt": null,
  "failureReason": null
}
```

**検証項目:**
- [ ] ステータスコード 201 を返す
- [ ] `Location` ヘッダーが含まれる
- [ ] `status` が `PENDING` である
- [ ] `gracePeriodEndsAt` が 14 日後に設定されている

#### テスト 9-2: POST /api/v1/users/{userId}/deletion-request - バリデーションエラー

```bash
curl -s -X POST http://localhost:5002/api/v1/users/${USER_ID}/deletion-request \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{}'
```

**期待するレスポンス（400 Bad Request）:**

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] `requestChannel` が必須であるバリデーションエラーが含まれる

#### テスト 9-3: GET /api/v1/users/{userId}/deletion-request - 削除リクエスト取得

```bash
curl -s -X GET http://localhost:5002/api/v1/users/${USER_ID}/deletion-request \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-xxx",
  "status": "PENDING",
  "requestedAt": "2026-04-06T10:00:00+00:00",
  "gracePeriodEndsAt": "2026-04-20T10:00:00+00:00",
  "completedAt": null,
  "failureReason": null
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 先ほど作成した削除リクエストの情報が返る

#### テスト 9-4: POST /api/v1/users/{userId}/deletion-request/cancel - 削除リクエスト取消

```bash
curl -s -X POST http://localhost:5002/api/v1/users/${USER_ID}/deletion-request/cancel \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 削除リクエストのステータスが `CANCELLED` に変更される

#### テスト 9-5: POST /api/v1/users/{userId}/data-export - データエクスポート要求

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5002/api/v1/users/${USER_ID}/data-export \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（202 Accepted）:**

**検証項目:**
- [ ] ステータスコード 202 を返す
- [ ] データエクスポートリクエストが受け付けられる

#### テスト 9-6: POST /api/v1/users/{userId}/data-export - 24 時間クールダウン

```bash
# 直後に再度リクエスト（24 時間以内のため拒否される）
curl -s -X POST http://localhost:5002/api/v1/users/${USER_ID}/data-export \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（422 Unprocessable Entity）:**

**検証項目:**
- [ ] ステータスコード 422 を返す
- [ ] 24 時間のクールダウン制限が機能している

#### テスト 9-7: POST /api/v1/users/{userId}/deletion-request - 他ユーザーの削除リクエスト取消（IDOR 防止）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5002/api/v1/users/other-user-id/deletion-request/cancel \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（403 Forbidden）:**

**検証項目:**
- [ ] ステータスコード 403 を返す

#### テスト 9-8: POST /api/v1/users/{userId}/deletion-request - 管理者による削除リクエスト作成

```bash
curl -s -X POST http://localhost:5002/api/v1/users/${USER_ID}/deletion-request \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"requestChannel": "ADMIN"}'
```

**期待するレスポンス（201 Created）:**

**検証項目:**
- [ ] ステータスコード 201 を返す
- [ ] 管理者は他ユーザーの削除リクエストを作成できる

---

## 5. verification-report.md のフォーマット

検証結果は以下のフォーマットで `verification-report.md` に記録する。

### 5.1 レポート構成

```markdown
# UserManagementService 検証レポート

**実施日時**: YYYY-MM-DD
**検証環境**: Docker Compose (localhost:5002)
**検証対象**: UserManagementService v1.0

---

## テスト結果サマリー

| Phase | テスト項目 | 結果 | 備考 |
|-------|-----------|------|------|
| 0 | ヘルスチェック（Liveness） | ✅ PASS / ❌ FAIL | - |
| 0 | ヘルスチェック（Readiness） | ✅ PASS / ❌ FAIL | - |
| 1 | ユーザープロファイル取得 | ✅ PASS / ❌ FAIL | - |
| ... | ... | ... | ... |

**全体結果**: XX/XX テスト PASS ✅

---

## Phase X: カテゴリ名

### Test X-Y: エンドポイント名

**リクエスト**:
```bash
curl -s -X METHOD http://localhost:5002/path \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{...}'
```

**レスポンス**:
```json
{
  ...
}
```

**HTTP Status**: XXX
**結果**: ✅ PASS / ❌ FAIL
```

### 5.2 記録する項目

各テストケースで以下を記録する:

1. **テスト名と番号**
2. **結果**: ✅ PASS / ❌ FAIL / ⏭️ SKIP
3. **ステータスコード**: 実際に返されたステータスコード
4. **実行コマンド**: 実際に実行した curl コマンド（トークンは `${VAR}` 形式に置換可）
5. **レスポンス**: 実際のレスポンス JSON
6. **備考**: 問題があった場合の詳細、特記事項

---

## 6. テスト用データ

### 6.1 テストユーザー

| 役割 | メールアドレス | パスワード | 用途 |
|------|-------------|----------|------|
| 一般ユーザー | testuser@example.com | Password123! | 一般機能テスト |
| 管理者 | admin@example.com | AdminPass123! | 管理者機能テスト |
| 第二ユーザー | testuser2@example.com | Password123! | IDOR テスト用 |

### 6.2 テスト用住所データ

| フィールド | 値 |
|---------|------|
| addressType | HOME |
| recipient | 山田 太郎 |
| zipCode | 100-0001 |
| prefecture | 東京都 |
| city | 千代田区 |
| streetAddress | 千代田1-1-1 |
| building | スカイタワー 301号室 |
| phoneNumber | 03-1234-5678 |

### 6.3 テスト用ウィッシュリストデータ

| フィールド | 値 |
|---------|------|
| name | スキー用品リスト |
| isDefault | true |
| productId（アイテム用） | test-product-001 |
| shouldNotifyOnRestock | true |

### 6.4 テスト用同意データ

| フィールド | 値 |
|---------|------|
| consentType（認証済み） | MARKETING |
| consentType（匿名） | COOKIE_TRACKING |
| policyVersion | 1 |

---

## 7. トラブルシューティング

### 7.1 よくある問題

#### ヘルスチェックが失敗する

```bash
# コンテナのログを確認
docker logs skishop-user-management-service

# データベース接続を確認
docker exec -it skishop-postgres psql -U skishop -d userdb -c "\dt"

# Redis 接続を確認
docker exec -it skishop-redis redis-cli ping
```

#### 401 Unauthorized が返される

- JWT トークンの有効期限を確認する（デフォルト 3600 秒）
- AuthService と UserManagementService で同じ `Jwt__SigningKey` が設定されているか確認する
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
- IDOR 保護により、自分以外のリソースへのアクセスは禁止されている
- JWT のクレームに正しいユーザー ID と ロールが含まれているか確認する

```bash
# JWT トークンのペイロードをデコード（bash）
echo "${ACCESS_TOKEN}" | cut -d. -f2 | base64 -d 2>/dev/null | python3 -m json.tool
```

#### UserManagementService の DB にユーザーが存在しない

AuthService と UserManagementService は独立した DB を使用する。ユーザーは Kafka イベント経由で同期される。

```bash
# userdb にユーザーが存在するか確認
docker exec -it skishop-postgres psql -U skishop -d userdb \
  -c "SELECT id, email, status FROM users;"

# 存在しない場合、手動で挿入
docker exec -it skishop-postgres psql -U skishop -d userdb \
  -c "INSERT INTO users (id, email, first_name, last_name, status, created_at, updated_at)
      VALUES ('${USER_ID}', 'testuser@example.com', 'Test', 'User', 'ACTIVE', NOW(), NOW())
      ON CONFLICT (id) DO NOTHING;"
```

#### バリデーションエラーだが Content-Type が正しい

- リクエストボディの JSON 形式を確認する
- `[Required]` 属性のフィールドが含まれているか確認する
- `[MaxLength]` 制約を超えていないか確認する

#### レート制限（429）に引っかかる

- `api` レート制限: 300 req/min（通常は問題なし）
- `anonymous-consent` レート制限: 10 req/min（匿名同意エンドポイントのみ）
- 1 分待ってからリトライする

---

## 8. 実行手順チェックリスト

検証実施時に以下の順序で進める:

- [ ] Docker Compose で環境を起動（`docker compose up -d auth-service user-management-service`）
- [ ] AuthService ヘルスチェック確認（`curl -s http://localhost:5001/health`）
- [ ] UserManagementService ヘルスチェック確認（`curl -s http://localhost:5002/health`）
- [ ] AuthService でテストユーザー作成・トークン取得（一般ユーザー + 管理者）
- [ ] UserManagementService の userdb にユーザーデータが存在するか確認
- [ ] Phase 0: ヘルスチェックを実施
- [ ] Phase 1: ユーザープロファイルを検証
- [ ] Phase 2: 管理者向けエンドポイントを検証
- [ ] Phase 3: 住所管理を検証
- [ ] Phase 4: アクティビティを検証
- [ ] Phase 5: 同意管理を検証（匿名同意含む）
- [ ] Phase 6: ユーザー設定を検証
- [ ] Phase 7: 会員ランクを検証
- [ ] Phase 8: ウィッシュリストを検証
- [ ] Phase 9: GDPR/DSR を検証
- [ ] verification-report.md に結果を記録
