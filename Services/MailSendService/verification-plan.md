# MailSendService 検証計画書（verification-plan.md）

本ドキュメントは MailSendService の全エンドポイントに対する動作検証の手順とフォーマットを定義する。
検証結果は `Services-Verification-Report/MailSendService-verification-report.md` に記録し、MailSendService の使い方を理解するためのリファレンスとして活用する。

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

1. **機能確認**: 全 12 エンドポイント（+ 2 ヘルスチェック）が設計通りに動作することを確認する
2. **使い方の文書化**: 各 API の呼び出し方法・パラメータ・レスポンスを実例で示す
3. **異常系の確認**: バリデーションエラー、認証エラー、認可エラー等の動作を確認する
4. **セキュリティ検証**: AdminOnly / AdminOrManager ポリシーによるアクセス制御を確認する

---

## 2. 前提条件

### 2.1 環境構成

| 項目 | 値 |
|------|-----|
| MailSendService URL | `http://localhost:5008` |
| AuthService URL | `http://localhost:5001` |
| PostgreSQL | `localhost:5432`（DB: `mailsenddb`） |
| Redis | `localhost:6379` |
| Kafka | `localhost:9092`（トピック: `domain-events`） |

### 2.2 起動コマンド

```bash
# Docker Compose で MailSendService と依存サービスを起動
cd /Users/yoterada/GitHub/DotNet-Skishop-App
docker compose up -d mailsend-service auth-service
```

> **📝 注意**: MailSendService は AuthService が発行した JWT トークンを使用して認証するため、AuthService も同時に起動する必要がある。

### 2.3 事前確認コマンド

```bash
# MailSendService ヘルスチェック
curl -s http://localhost:5008/health
# 期待結果: Healthy

# AuthService ヘルスチェック
curl -s http://localhost:5001/health
# 期待結果: Healthy
```

### 2.4 テスト用トークン取得手順

検証開始前に AuthService でユーザーを登録し、JWT トークンを取得する。

#### 管理者トークン取得

MailSendService の管理エンドポイントは `AdminOnly` または `AdminOrManager` ポリシーで保護されているため、管理者ロールを持つユーザーのトークンが必要。

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
# 2. 管理者ステータスとロール設定（DB 直接操作）
docker exec -it skishop-postgres psql -U skishop -d authdb \
  -c "UPDATE users SET status = 'ACTIVE', email_verified = true, role = 'Admin' WHERE email = 'admin@example.com';"
```

```bash
# 3. 管理者ログインしてトークン取得
curl -s -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@example.com",
    "password": "AdminPass123!"
  }'

# レスポンスの accessToken を環境変数に保存
export ADMIN_TOKEN="eyJhbGci..."
```

#### マネージャートークン取得（AdminOrManager ポリシー用）

```bash
# 1. マネージャーユーザー登録
curl -s -X POST http://localhost:5001/api/v1/auth/users \
  -H "Content-Type: application/json" \
  -d '{
    "email": "manager@example.com",
    "password": "ManagerPass123!",
    "firstName": "Manager",
    "lastName": "User",
    "username": "manageruser"
  }'
```

```bash
# 2. マネージャーステータスとロール設定
docker exec -it skishop-postgres psql -U skishop -d authdb \
  -c "UPDATE users SET status = 'ACTIVE', email_verified = true, role = 'Manager' WHERE email = 'manager@example.com';"
```

```bash
# 3. マネージャーログイン
curl -s -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "manager@example.com",
    "password": "ManagerPass123!"
  }'

export MANAGER_TOKEN="eyJhbGci..."
```

#### 一般ユーザートークン取得（権限拒否テスト用）

```bash
# 1. 一般ユーザー登録
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
# 2. ユーザーステータスを ACTIVE に変更
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

export USER_TOKEN="eyJhbGci..."
```

---

## 3. エンドポイント一覧

### 3.1 ヘルスチェック

| メソッド | パス | 説明 | 認証 |
|---------|------|------|------|
| GET | `/health` | Liveness チェック | 不要 |
| GET | `/health/ready` | Readiness チェック（PostgreSQL, Redis, Kafka 疎通） | 不要 |

### 3.2 メール管理（Mail Administration）

| メソッド | パス | 説明 | 認証ポリシー | 備考 |
|---------|------|------|-------------|------|
| GET | `/admin/mail/logs` | メールログ一覧取得 | AdminOnly | ページネーション対応 |
| GET | `/admin/mail/logs/{id}` | メールログ詳細取得 | AdminOnly | |
| POST | `/admin/mail/logs/{id}/retry` | メール送信リトライ | AdminOnly | 202 Accepted |
| POST | `/admin/mail/test` | テストメール送信 | AdminOnly | レート制限: IP単位で1時間5回 |
| GET | `/admin/mail/stats` | 送信統計情報取得 | AdminOrManager | Redis キャッシュ（5分 TTL） |

### 3.3 テンプレート管理（Mail Templates）

| メソッド | パス | 説明 | 認証ポリシー | 備考 |
|---------|------|------|-------------|------|
| GET | `/admin/mail/templates` | テンプレート一覧取得 | AdminOnly | ページネーション対応 |
| GET | `/admin/mail/templates/{id}` | テンプレート詳細取得 | AdminOnly | |
| POST | `/admin/mail/templates` | テンプレート作成 | AdminOnly | |
| PUT | `/admin/mail/templates/{id}` | テンプレート更新 | AdminOnly | |
| DELETE | `/admin/mail/templates/{id}` | テンプレート無効化（論理削除） | AdminOnly | |

---

## 4. 検証手順

検証は以下の順序で実施する。依存関係を考慮した順番となっている。

---

### Phase 0: ヘルスチェック

#### テスト 0-1: GET /health（Liveness）

```bash
curl -s http://localhost:5008/health
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
curl -s http://localhost:5008/health/ready
```

**期待するレスポンス（200 OK）:**

```
Healthy
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] PostgreSQL と Redis の疎通が確認できている

---

### Phase 1: テンプレート管理（CRUD の順序に従う）

> **📝 前提**: AuthService で管理者ユーザー登録・ログイン済み。`ADMIN_TOKEN` を環境変数に設定済み。

#### テスト 1-1: POST /admin/mail/templates - テンプレート作成

```bash
curl -s -X POST http://localhost:5008/admin/mail/templates \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "test-template",
    "subject": "テストメール: {{userName}} 様",
    "htmlBody": "<html><body><h1>{{userName}} 様</h1><p>テストメールです。</p></body></html>",
    "textBody": "{{userName}} 様\n\nテストメールです。",
    "templateType": "TRANSACTIONAL",
    "variables": "{\"userName\": \"string\"}"
  }'
```

**期待するレスポンス（201 Created）:**

```json
{
  "id": "uuid-xxx",
  "name": "test-template",
  "subject": "テストメール: {{userName}} 様",
  "htmlBody": "<html><body><h1>{{userName}} 様</h1><p>テストメールです。</p></body></html>",
  "textBody": "{{userName}} 様\n\nテストメールです。",
  "templateType": "TRANSACTIONAL",
  "variables": "{\"userName\": \"string\"}",
  "isActive": true,
  "createdAt": "2026-04-06T...",
  "updatedAt": "2026-04-06T..."
}
```

**検証項目:**
- [ ] ステータスコード 201 を返す
- [ ] `Location` ヘッダーに新規リソースのパスが含まれる
- [ ] レスポンスに作成されたテンプレートの情報が含まれる
- [ ] `id` が UUID 形式で返される
- [ ] `isActive` が `true` で返される

```bash
# 作成されたテンプレート ID を環境変数に保存
export TEMPLATE_ID="取得したテンプレートID"
```

#### テスト 1-2: POST /admin/mail/templates - バリデーションエラー（name が空）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5008/admin/mail/templates \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "",
    "subject": "テスト",
    "templateType": "TRANSACTIONAL"
  }'
```

**期待するレスポンス（400 Bad Request）:**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Name": ["..."]
  }
}
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] バリデーションエラーの詳細が返される

#### テスト 1-2-b: POST /admin/mail/templates - バリデーションエラー（本文なし）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5008/admin/mail/templates \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "no-body-template",
    "subject": "テスト",
    "templateType": "TRANSACTIONAL"
  }'
```

**期待するレスポンス（400 Bad Request）:**

```json
{
  "errors": {
    "": ["HTML 本文またはテキスト本文の少なくとも一方を指定してください"]
  }
}
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] 「HTML本文またはテキスト本文が必要」エラーが返される

#### テスト 1-2-c: POST /admin/mail/templates - バリデーションエラー（無効な templateType）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5008/admin/mail/templates \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "invalid-type-template",
    "subject": "テスト",
    "htmlBody": "<html><body>テスト</body></html>",
    "templateType": "INVALID"
  }'
```

**期待するレスポンス（400 Bad Request）:**

```json
{
  "errors": {
    "TemplateType": ["テンプレートタイプは TRANSACTIONAL または MARKETING のみ"]
  }
}
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] templateType のバリデーションエラーが返される

#### テスト 1-2-d: POST /admin/mail/templates - バリデーションエラー（テンプレート名形式）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5008/admin/mail/templates \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Invalid_Template_Name",
    "subject": "テスト",
    "htmlBody": "<html><body>テスト</body></html>",
    "templateType": "TRANSACTIONAL"
  }'
```

**期待するレスポンス（400 Bad Request）:**

```json
{
  "errors": {
    "Name": ["テンプレート名は半角英小文字・数字・ハイフンのみ"]
  }
}
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] テンプレート名形式エラーが返される（半角英小文字・数字・ハイフンのみ）

#### テスト 1-3: POST /admin/mail/templates - 認証なしアクセス

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5008/admin/mail/templates \
  -H "Content-Type: application/json" \
  -d '{
    "name": "unauthorized-template",
    "subject": "テスト",
    "templateType": "TRANSACTIONAL"
  }'
```

**期待するレスポンス（401 Unauthorized）:**

**検証項目:**
- [ ] ステータスコード 401 を返す

#### テスト 1-4: POST /admin/mail/templates - 一般ユーザーによるアクセス拒否

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5008/admin/mail/templates \
  -H "Authorization: Bearer ${USER_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "user-template",
    "subject": "テスト",
    "templateType": "TRANSACTIONAL"
  }'
```

**期待するレスポンス（403 Forbidden）:**

**検証項目:**
- [ ] ステータスコード 403 を返す
- [ ] AdminOnly ポリシーが正しく機能している

#### テスト 1-5: GET /admin/mail/templates - テンプレート一覧取得

```bash
curl -s -X GET "http://localhost:5008/admin/mail/templates?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "items": [
    {
      "id": "uuid-xxx",
      "name": "test-template",
      "subject": "テストメール: {{userName}} 様",
      "htmlBody": "...",
      "textBody": "...",
      "templateType": "TRANSACTIONAL",
      "variables": "{\"userName\": \"string\"}",
      "isActive": true,
      "createdAt": "...",
      "updatedAt": "..."
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
- [ ] ページネーション情報が正しい
- [ ] 作成したテンプレートが一覧に含まれる

#### テスト 1-6: GET /admin/mail/templates - ページネーション（2 ページ目）

```bash
curl -s -X GET "http://localhost:5008/admin/mail/templates?page=2&pageSize=5" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `page` が 2 になっている

#### テスト 1-7: GET /admin/mail/templates/{id} - テンプレート詳細取得

```bash
curl -s -X GET "http://localhost:5008/admin/mail/templates/${TEMPLATE_ID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-xxx",
  "name": "test-template",
  "subject": "テストメール: {{userName}} 様",
  "htmlBody": "<html><body><h1>{{userName}} 様</h1><p>テストメールです。</p></body></html>",
  "textBody": "{{userName}} 様\n\nテストメールです。",
  "templateType": "TRANSACTIONAL",
  "variables": "{\"userName\": \"string\"}",
  "isActive": true,
  "createdAt": "...",
  "updatedAt": "..."
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 指定した ID のテンプレート情報が返される

#### テスト 1-8: GET /admin/mail/templates/{id} - 存在しない ID

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X GET "http://localhost:5008/admin/mail/templates/non-existent-id" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（404 Not Found）:**

**検証項目:**
- [ ] ステータスコード 404 を返す

#### テスト 1-9: PUT /admin/mail/templates/{id} - テンプレート更新

```bash
curl -s -X PUT "http://localhost:5008/admin/mail/templates/${TEMPLATE_ID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "subject": "更新されたテストメール: {{userName}} 様",
    "htmlBody": "<html><body><h1>更新: {{userName}} 様</h1><p>更新テストメールです。</p></body></html>",
    "isActive": true
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-xxx",
  "name": "test-template",
  "subject": "更新されたテストメール: {{userName}} 様",
  "htmlBody": "<html><body><h1>更新: {{userName}} 様</h1><p>更新テストメールです。</p></body></html>",
  "textBody": "{{userName}} 様\n\nテストメールです。",
  "templateType": "TRANSACTIONAL",
  "variables": "{\"userName\": \"string\"}",
  "isActive": true,
  "createdAt": "...",
  "updatedAt": "..."
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `subject` と `htmlBody` が更新されている
- [ ] `updatedAt` が更新されている
- [ ] 指定していない `textBody` は変更されていない

#### テスト 1-10: PUT /admin/mail/templates/{id} - 更新バリデーションエラー（フィールドなし）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X PUT "http://localhost:5008/admin/mail/templates/${TEMPLATE_ID}" \
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
    "": ["更新するフィールドを少なくとも 1 つ指定してください"]
  }
}
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] 「少なくとも1つのフィールドが必要」エラーが返される

#### テスト 1-11: DELETE /admin/mail/templates/{id} - テンプレート無効化

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X DELETE "http://localhost:5008/admin/mail/templates/${TEMPLATE_ID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（204 No Content）:**

**検証項目:**
- [ ] ステータスコード 204 を返す
- [ ] レスポンスボディが空である

#### テスト 1-12: GET /admin/mail/templates/{id} - 無効化後の取得

```bash
curl -s -X GET "http://localhost:5008/admin/mail/templates/${TEMPLATE_ID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-xxx",
  "name": "test-template",
  "isActive": false,
  ...
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `isActive` が `false` に変更されている
- [ ] 物理削除ではなく論理削除（無効化）である

---

### Phase 2: テストメール送信

> **📝 前提**: 有効なテンプレートが存在すること。テストでは新しいテンプレートを作成して使用する。

#### テスト 2-0: テスト用テンプレート作成

```bash
curl -s -X POST http://localhost:5008/admin/mail/templates \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "email-verification",
    "subject": "メール認証: {{userName}} 様",
    "htmlBody": "<html><body><h1>{{userName}} 様</h1><p>認証コード: {{code}}</p></body></html>",
    "textBody": "{{userName}} 様\n\n認証コード: {{code}}",
    "templateType": "TRANSACTIONAL",
    "variables": "{\"userName\": \"string\", \"code\": \"string\"}"
  }'

export TEST_TEMPLATE_ID="取得したテンプレートID"
```

#### テスト 2-1: POST /admin/mail/test - テストメール送信

```bash
curl -s -X POST http://localhost:5008/admin/mail/test \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "recipientEmail": "test@example.com",
    "templateName": "email-verification",
    "variables": {
      "userName": "テストユーザー",
      "code": "123456"
    }
  }'
```

**期待するレスポンス（201 Created）:**

```json
{
  "id": "uuid-xxx",
  "eventType": "test.mail",
  "recipientEmail": "t***@example.com",
  "recipientName": null,
  "templateName": "email-verification",
  "subject": "メール認証: テストユーザー 様",
  "status": "SENT",
  "retryCount": 0,
  "errorMessage": null,
  "sentAt": "2026-04-06T...",
  "createdAt": "2026-04-06T..."
}
```

**検証項目:**
- [ ] ステータスコード 201 を返す
- [ ] `Location` ヘッダーにメールログのパスが含まれる
- [ ] `recipientEmail` がマスキングされている（PII 保護）
- [ ] `status` が `SENT` または `PENDING` である
- [ ] テンプレートの変数が件名に正しく置換されている

```bash
# 作成されたメールログ ID を環境変数に保存
export MAIL_LOG_ID="取得したメールログID"
```

#### テスト 2-2: POST /admin/mail/test - バリデーションエラー（無効なメールアドレス）

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5008/admin/mail/test \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "recipientEmail": "invalid-email",
    "templateName": "email-verification"
  }'
```

**期待するレスポンス（400 Bad Request）:**

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] メールアドレス形式エラーが返される

#### テスト 2-3: POST /admin/mail/test - 存在しないテンプレート

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5008/admin/mail/test \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "recipientEmail": "test@example.com",
    "templateName": "non-existent-template"
  }'
```

**期待するレスポンス（404 Not Found または 422 Unprocessable Entity）:**

**検証項目:**
- [ ] 適切なエラーステータスコードを返す
- [ ] テンプレートが見つからない旨のエラーメッセージが返される

#### テスト 2-4: POST /admin/mail/test - テンプレート名形式エラー

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5008/admin/mail/test \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "recipientEmail": "test@example.com",
    "templateName": "Invalid_Template_Name"
  }'
```

**期待するレスポンス（400 Bad Request）:**

```json
{
  "errors": {
    "TemplateName": ["テンプレート名は半角英小文字・数字・ハイフンのみ"]
  }
}
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] テンプレート名形式エラーが返される（半角英小文字・数字・ハイフンのみ）

#### テスト 2-5: POST /admin/mail/test - 変数数上限エラー

> **📝 注意**: 変数は最大 50 個までに制限されている

```bash
# 51 個の変数を含むリクエスト（省略形）
curl -s -w "\nHTTP Status: %{http_code}" \
  -X POST http://localhost:5008/admin/mail/test \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "recipientEmail": "test@example.com",
    "templateName": "email-verification",
    "variables": {"v1":"1","v2":"2",...,"v51":"51"}
  }'
```

**期待するレスポンス（400 Bad Request）:**

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] 変数上限エラーが返される（最大50個）

#### テスト 2-6: POST /admin/mail/test - レート制限（※実施は任意）

> **📝 注意**: テストメールエンドポイントには IP アドレス単位で 1 時間 5 回のレート制限あり。このテストは連続実行により 429 を確認するもので、環境によってはスキップ可。

```bash
# 6 回目の呼び出しで 429 が返る
for i in {1..6}; do
  curl -s -w "\nHTTP Status: %{http_code}" \
    -X POST http://localhost:5008/admin/mail/test \
    -H "Authorization: Bearer ${ADMIN_TOKEN}" \
    -H "Content-Type: application/json" \
    -d '{"recipientEmail": "test@example.com", "templateName": "email-verification"}'
  echo ""
done
```

**期待するレスポンス（6 回目: 429 Too Many Requests）:**

**検証項目:**
- [ ] 6 回目の呼び出しでステータスコード 429 を返す
- [ ] レート制限が正しく機能している

---

### Phase 3: メールログ管理

#### テスト 3-1: GET /admin/mail/logs - メールログ一覧取得

```bash
curl -s -X GET "http://localhost:5008/admin/mail/logs?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "items": [
    {
      "id": "uuid-xxx",
      "eventType": "test.mail",
      "recipientEmail": "t***@example.com",
      "recipientName": null,
      "templateName": "email-verification",
      "subject": "メール認証: テストユーザー 様",
      "status": "SENT",
      "retryCount": 0,
      "errorMessage": null,
      "sentAt": "...",
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
- [ ] ページネーション情報が正しい
- [ ] メールアドレスがマスキングされている
- [ ] 送信したテストメールのログが含まれる

#### テスト 3-2: GET /admin/mail/logs - ページネーション（2 ページ目）

```bash
curl -s -X GET "http://localhost:5008/admin/mail/logs?page=2&pageSize=5" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `page` が 2 になっている

#### テスト 3-3: GET /admin/mail/logs/{id} - メールログ詳細取得

```bash
curl -s -X GET "http://localhost:5008/admin/mail/logs/${MAIL_LOG_ID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-xxx",
  "eventType": "test.mail",
  "recipientEmail": "t***@example.com",
  "recipientName": null,
  "templateName": "email-verification",
  "subject": "メール認証: テストユーザー 様",
  "status": "SENT",
  "retryCount": 0,
  "errorMessage": null,
  "sentAt": "...",
  "createdAt": "..."
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 指定した ID のメールログが返される

#### テスト 3-4: GET /admin/mail/logs/{id} - 存在しない ID

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X GET "http://localhost:5008/admin/mail/logs/non-existent-id" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（404 Not Found）:**

**検証項目:**
- [ ] ステータスコード 404 を返す

#### テスト 3-5: POST /admin/mail/logs/{id}/retry - メール送信リトライ

> **📝 注意**: リトライは `FAILED` ステータスのメールログに対してのみ有効。テスト用に失敗したメールログが必要な場合は、無効なテンプレートを使用してテストメールを送信するか、DB 直接操作でステータスを `FAILED` に変更する。

```bash
# 失敗メールログを作成（存在しない ACS 設定の場合に発生）
# または既存のメールログのステータスを FAILED に変更
docker exec -it skishop-postgres psql -U skishop -d mailsenddb \
  -c "UPDATE mail_logs SET status = 'FAILED', error_message = 'Test failure' WHERE id = '${MAIL_LOG_ID}';"

# リトライ実行
curl -s -X POST "http://localhost:5008/admin/mail/logs/${MAIL_LOG_ID}/retry" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（202 Accepted）:**

```json
{
  "id": "uuid-xxx",
  "status": "PENDING",
  "retryCount": 1,
  ...
}
```

**検証項目:**
- [ ] ステータスコード 202 を返す
- [ ] `retryCount` が増加している
- [ ] `status` が `PENDING` または `SENDING` に変更されている

---

### Phase 4: 統計情報

#### テスト 4-1: GET /admin/mail/stats - 管理者による統計取得

```bash
curl -s -X GET "http://localhost:5008/admin/mail/stats" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "totalSent": 1,
  "totalFailed": 0,
  "totalPending": 0,
  "successRate": 100.0,
  "sentByTemplate": {
    "email-verification": 1
  }
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 送信件数が正しい
- [ ] `successRate` が 0〜100 の範囲である
- [ ] テンプレート別の送信件数が返される

#### テスト 4-2: GET /admin/mail/stats - マネージャーによる統計取得（AdminOrManager ポリシー）

```bash
curl -s -X GET "http://localhost:5008/admin/mail/stats" \
  -H "Authorization: Bearer ${MANAGER_TOKEN}"
```

**期待するレスポンス（200 OK）:**

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] Manager ロールでもアクセス可能（AdminOrManager ポリシー）

#### テスト 4-3: GET /admin/mail/stats - 一般ユーザーによるアクセス拒否

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X GET "http://localhost:5008/admin/mail/stats" \
  -H "Authorization: Bearer ${USER_TOKEN}"
```

**期待するレスポンス（403 Forbidden）:**

**検証項目:**
- [ ] ステータスコード 403 を返す
- [ ] 一般ユーザーはアクセス不可

#### テスト 4-4: GET /admin/mail/stats - 認証なしアクセス

```bash
curl -s -w "\nHTTP Status: %{http_code}" \
  -X GET "http://localhost:5008/admin/mail/stats"
```

**期待するレスポンス（401 Unauthorized）:**

**検証項目:**
- [ ] ステータスコード 401 を返す

---

## 5. verification-report.md のフォーマット

検証結果は以下のフォーマットで `Services-Verification-Report/MailSendService-verification-report.md` に記録する。

```markdown
# MailSendService API 検証レポート

**実施日時**: YYYY-MM-DD  
**検証環境**: Docker Compose (localhost:5008)  
**検証対象**: MailSendService v1.0

---

## 1. 概要

本レポートは MailSendService で提供される全 API エンドポイントの動作検証結果を記録したものです。

### 検証環境の構成

```
- MailSendService: localhost:5008
- AuthService: localhost:5001 (トークン取得用)
- PostgreSQL: skishop-postgres (mailsenddb)
- Redis: skishop-redis
- Kafka: skishop-kafka
```

---

## 2. テスト結果サマリー

| Phase | テスト項目 | 結果 | 備考 |
|-------|-----------|------|------|
| 0 | GET /health | ✅ PASS | |
| 0 | GET /health/ready | ✅ PASS | |
| 1 | テンプレート作成 | ✅ PASS | |
| ... | ... | ... | ... |

---

## 3. Phase 0: ヘルスチェック

### テスト 0-1: GET /health

**リクエスト:**
```bash
curl -s http://localhost:5008/health
```

**レスポンス:**
```
Healthy
```

**結果:** ✅ PASS

---

## 4. Phase 1: テンプレート管理

### テスト 1-1: POST /admin/mail/templates - テンプレート作成

**リクエスト:**
```bash
curl -s -X POST http://localhost:5008/admin/mail/templates \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{"name": "test-template", ...}'
```

**レスポンス (201 Created):**
```json
{
  "id": "uuid-xxx",
  "name": "test-template",
  ...
}
```

**結果:** ✅ PASS

---
```

---

## 6. テスト用データ

### 6.1 テンプレートデータ

```json
{
  "name": "test-template",
  "subject": "テストメール: {{userName}} 様",
  "htmlBody": "<html><body><h1>{{userName}} 様</h1><p>テストメールです。</p></body></html>",
  "textBody": "{{userName}} 様\n\nテストメールです。",
  "templateType": "TRANSACTIONAL",
  "variables": "{\"userName\": \"string\"}"
}
```

### 6.2 テストメールリクエスト

```json
{
  "recipientEmail": "test@example.com",
  "templateName": "email-verification",
  "variables": {
    "userName": "テストユーザー",
    "code": "123456"
  }
}
```

---

## 7. トラブルシューティング

### 7.1 401 Unauthorized が返る

- JWT トークンの有効期限が切れている可能性がある
- トークンを再取得する:
  ```bash
  curl -s -X POST http://localhost:5001/api/v1/auth/login \
    -H "Content-Type: application/json" \
    -d '{"email": "admin@example.com", "password": "AdminPass123!"}'
  ```

### 7.2 403 Forbidden が返る

- ユーザーのロールが `Admin` または `Manager` でない
- DB で直接ロールを確認・修正:
  ```bash
  docker exec -it skishop-postgres psql -U skishop -d authdb \
    -c "SELECT email, role FROM users WHERE email = 'admin@example.com';"
  ```

### 7.3 サービスが起動しない

- Docker ログを確認:
  ```bash
  docker compose logs mailsend-service --tail=50
  ```
- 環境変数が不足している場合がある（SendGrid APIKey、UserManagement ApiKey 等）

### 7.4 テンプレートが見つからない

- テンプレートが `isActive = false` の可能性がある
- DB で直接確認:
  ```bash
  docker exec -it skishop-postgres psql -U skishop -d mailsenddb \
    -c "SELECT id, name, is_active FROM mail_templates;"
  ```

### 7.5 メール送信が失敗する

- Azure Communication Services の設定が必要
- 開発環境ではダミー設定で `FAILED` ステータスになることが正常
- エラーメッセージを確認:
  ```bash
  docker exec -it skishop-postgres psql -U skishop -d mailsenddb \
    -c "SELECT id, status, error_message FROM mail_logs ORDER BY created_at DESC LIMIT 5;"
  ```

---

## 8. 実行手順チェックリスト

### 8.1 事前準備

- [ ] Docker Desktop が起動している
- [ ] Docker Compose で依存サービスが起動している
  ```bash
  docker compose up -d postgres redis kafka auth-service mailsend-service
  ```
- [ ] ヘルスチェックが通る
  ```bash
  curl -s http://localhost:5008/health  # Healthy
  curl -s http://localhost:5001/health  # Healthy
  ```

### 8.2 トークン準備

- [ ] 管理者ユーザーを登録
- [ ] 管理者ユーザーのステータス・ロールを設定
- [ ] 管理者でログインしてトークン取得
- [ ] `ADMIN_TOKEN` 環境変数に設定
- [ ] （任意）マネージャーユーザーも同様に準備
- [ ] （任意）一般ユーザーも同様に準備

### 8.3 テスト実行

- [ ] Phase 0: ヘルスチェック
- [ ] Phase 1: テンプレート CRUD
- [ ] Phase 2: テストメール送信
- [ ] Phase 3: メールログ管理
- [ ] Phase 4: 統計情報

### 8.4 クリーンアップ

- [ ] テストデータの削除（任意）
  ```bash
  docker exec -it skishop-postgres psql -U skishop -d mailsenddb \
    -c "DELETE FROM mail_logs WHERE event_type = 'test.mail';"
  docker exec -it skishop-postgres psql -U skishop -d mailsenddb \
    -c "DELETE FROM mail_templates WHERE name LIKE 'test-%';"
  ```

---

## 付録: エンドポイント詳細仕様

### A.1 リクエスト DTO

#### TestMailRequest

| フィールド | 型 | 必須 | バリデーション |
|-----------|-----|------|--------------|
| recipientEmail | string | ✅ | Email形式, 最大255文字 |
| templateName | string | ✅ | 最大100文字, 半角英小文字・数字・ハイフンのみ（`^[a-z0-9-]+$`） |
| variables | Dictionary<string, object> | - | 最大50個まで |

#### TemplateCreateRequest

| フィールド | 型 | 必須 | バリデーション |
|-----------|-----|------|--------------|
| name | string | ✅ | 最大100文字, 半角英小文字・数字・ハイフンのみ（`^[a-z0-9-]+$`）, ユニーク |
| subject | string | ✅ | 最大500文字 |
| htmlBody | string | △ | 最大500KB（HtmlBody または TextBody の少なくとも一方が必須） |
| textBody | string | △ | 最大500KB（HtmlBody または TextBody の少なくとも一方が必須） |
| templateType | string | ✅ | `TRANSACTIONAL` または `MARKETING` のみ |
| variables | string | - | JSON形式, 最大10000文字 |

#### TemplateUpdateRequest

| フィールド | 型 | 必須 | バリデーション |
|-----------|-----|------|--------------|
| subject | string | - | 最大500文字 |
| htmlBody | string | - | 最大500KB |
| textBody | string | - | 最大500KB |
| variables | string | - | JSON形式, 最大10000文字 |
| isActive | bool | - | |

> **📝 注意**: TemplateUpdateRequest は少なくとも1つのフィールドが指定されている必要がある

### A.2 レスポンス DTO

#### MailLogResponse

| フィールド | 型 | 説明 |
|-----------|-----|------|
| id | string | メールログ ID |
| eventType | string | イベント種別 |
| recipientEmail | string | 送信先（マスキング済み） |
| recipientName | string? | 受信者名 |
| templateName | string | テンプレート名 |
| subject | string | 件名 |
| status | string | ステータス (PENDING/SENDING/SENT/FAILED/SKIPPED) |
| retryCount | int | リトライ回数 |
| errorMessage | string? | エラーメッセージ |
| sentAt | DateTimeOffset? | 送信完了日時 |
| createdAt | DateTimeOffset | 作成日時 |

#### MailStatsResponse

| フィールド | 型 | 説明 |
|-----------|-----|------|
| totalSent | long | 送信成功件数 |
| totalFailed | long | 送信失敗件数 |
| totalPending | long | 送信待ち件数 |
| successRate | double | 成功率（0.0〜100.0%） |
| sentByTemplate | Dictionary<string, long> | テンプレート別送信件数 |

#### MailTemplateResponse

| フィールド | 型 | 説明 |
|-----------|-----|------|
| id | string | テンプレート ID |
| name | string | テンプレート名 |
| subject | string | 件名テンプレート |
| htmlBody | string? | HTML 本文 |
| textBody | string? | テキスト本文 |
| templateType | string | 種別 (TRANSACTIONAL/MARKETING) |
| variables | string? | 変数定義 JSON |
| isActive | bool | 有効フラグ |
| createdAt | DateTimeOffset | 作成日時 |
| updatedAt | DateTimeOffset | 更新日時 |

---

**作成日**: 2026-04-06  
**作成者**: AI Agent  
**レビュー担当**: -
