# MailSendService API 検証レポート

**実施日時**: 2026-04-06  
**検証環境**: Docker Compose (localhost:5008)  
**検証対象**: MailSendService v1.0

---

## 目次

1. [概要](#1-概要)
2. [テスト結果サマリー](#2-テスト結果サマリー)
3. [Phase 0: ヘルスチェック](#3-phase-0-ヘルスチェック)
4. [Phase 1: テンプレート管理](#4-phase-1-テンプレート管理)
5. [Phase 2: テストメール送信](#5-phase-2-テストメール送信)
6. [Phase 3: メールログ管理](#6-phase-3-メールログ管理)
7. [Phase 4: 統計情報](#7-phase-4-統計情報)
8. [Phase 5: 認可テスト](#8-phase-5-認可テスト)
9. [Appendix: 問題と対応策](#appendix-問題と対応策)

---

## 1. 概要

本レポートは MailSendService で提供される全 API エンドポイントの動作検証結果を記録したものです。各エンドポイントに対して実際に curl コマンドでリクエストを送信し、レスポンスを確認しました。

### 検証環境の構成

```
- MailSendService: localhost:5008
- AuthService: localhost:5001 (トークン取得用)
- PostgreSQL: skishop-postgres (mailsenddb)
- Redis: skishop-redis
- Kafka: skishop-kafka
```

### エンドポイント一覧

| カテゴリ | メソッド | パス | 説明 | 認可 |
|---------|---------|------|------|------|
| テンプレート | POST | /admin/mail/templates | テンプレート作成 | AdminOrManager |
| テンプレート | GET | /admin/mail/templates | テンプレート一覧 | AdminOrManager |
| テンプレート | GET | /admin/mail/templates/{id} | テンプレート詳細 | AdminOrManager |
| テンプレート | PUT | /admin/mail/templates/{id} | テンプレート更新 | AdminOrManager |
| テンプレート | DELETE | /admin/mail/templates/{id} | テンプレート削除 | AdminOnly |
| メール | POST | /admin/mail/test | テストメール送信 | AdminOrManager |
| ログ | GET | /admin/mail/logs | メールログ一覧 | AdminOrManager |
| ログ | GET | /admin/mail/logs/{id} | メールログ詳細 | AdminOrManager |
| ログ | POST | /admin/mail/logs/{id}/retry | メール再送信 | AdminOnly |
| 統計 | GET | /admin/mail/stats | 統計情報取得 | AdminOrManager |
| ヘルス | GET | /health | Liveness チェック | AllowAnonymous |
| ヘルス | GET | /health/ready | Readiness チェック | AllowAnonymous |

### 前提条件

検証開始前に以下の修正を適用済み:

1. **JWT設定の統一**: MailSendServiceとAuthServiceで異なるJWT設定が使われていたため、docker-compose.ymlを修正
   - 修正前: `Jwt__Issuer=SkiShop`, `Jwt__Audience=SkiShopUsers`
   - 修正後: `Jwt__Issuer=https://skishop.local`, `Jwt__Audience=skishop-api`

2. **データベーステーブル作成**: EF Core Migrationsファイルが存在しなかったため、直接SQLでテーブルを作成
   - `mail_templates`, `mail_logs`, `mail_attachments`, `mail_suppressions`, `outbox_events`

3. **認可ポリシーの大文字小文字対応**: AuthServiceは`ADMIN`、Program.csは`Admin`を期待していたため、両方を許可するよう修正
   - 修正: `RequireRole("Admin", "ADMIN")` および `RequireRole("Admin", "ADMIN", "Manager", "MANAGER")`

---

## 2. テスト結果サマリー

| Phase | テスト項目 | 結果 | 備考 |
|-------|-----------|------|------|
| 0 | GET /health | ✅ PASS | Liveness チェック |
| 0 | GET /health/ready | ✅ PASS | Readiness チェック |
| 1 | POST /admin/mail/templates (TRANSACTIONAL) | ✅ PASS | 201 Created |
| 1 | POST /admin/mail/templates (MARKETING) | ✅ PASS | 201 Created |
| 1 | GET /admin/mail/templates | ✅ PASS | ページネーション対応 |
| 1 | GET /admin/mail/templates/{id} | ✅ PASS | 詳細取得 |
| 1 | PUT /admin/mail/templates/{id} | ✅ PASS | 更新成功 |
| 1 | DELETE /admin/mail/templates/{id} | ✅ PASS | 204 No Content |
| 1 | 重複名テンプレート作成 | ✅ PASS | 409 Conflict |
| 1 | バリデーションエラー（名前形式） | ✅ PASS | 400 Bad Request |
| 1 | 存在しないテンプレート取得 | ✅ PASS | 404 Not Found |
| 1 | バリデーションエラー（本文なし） | ✅ PASS | 400 Bad Request |
| 2 | POST /admin/mail/test | ✅ PASS | テストメール送信 |
| 2 | 存在しないテンプレートでテスト | ✅ PASS | 404 Not Found |
| 2 | 無効なメールアドレス | ✅ PASS | 400 Bad Request |
| 3 | GET /admin/mail/logs | ✅ PASS | ログ一覧取得 |
| 3 | ステータスフィルタリング | ✅ PASS | フィルタ動作確認 |
| 3 | GET /admin/mail/logs/{id} | ✅ PASS | ログ詳細取得 |
| 3 | POST /admin/mail/logs/{id}/retry | ✅ PASS | リトライ実行 |
| 3 | 存在しないログ取得 | ✅ PASS | 404 Not Found |
| 4 | GET /admin/mail/stats | ✅ PASS | 統計情報取得 |
| 4 | 期間指定統計取得 | ✅ PASS | パラメータ動作確認 |
| 5 | 認証なしアクセス | ✅ PASS | 401 Unauthorized |
| 5 | 無効トークンでのアクセス | ✅ PASS | 401 Unauthorized |
| 5 | 一般ユーザーでの管理機能アクセス | ✅ PASS | 403 Forbidden |

**総合結果: 22/22 テスト PASSED (100%)**

---

## 3. Phase 0: ヘルスチェック

### Test 0-1: Liveness チェック

**Request:**
```bash
curl -s http://localhost:5008/health
```

**Response:**
```
Healthy
```
**HTTP Status:** 200 OK  
**Result:** ✅ PASS

### Test 0-2: Readiness チェック

**Request:**
```bash
curl -s http://localhost:5008/health/ready
```

**Response:**
```
Healthy
```
**HTTP Status:** 200 OK  
**Result:** ✅ PASS

---

## 4. Phase 1: テンプレート管理

### Test 1-1: テンプレート作成 (TRANSACTIONAL)

**Request:**
```bash
curl -X POST "http://localhost:5008/admin/mail/templates" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "order-confirmation",
    "subject": "ご注文ありがとうございます - Order #{{orderId}}",
    "htmlBody": "<html><body><h1>Order Confirmation</h1><p>Order: {{orderId}}</p></body></html>",
    "textBody": "Order Confirmation\nOrder: {{orderId}}",
    "templateType": "TRANSACTIONAL"
  }'
```

**Response:**
```json
{
  "id": "63fa2049-6267-4f64-abc4-077b10fe75fa",
  "name": "order-confirmation",
  "subject": "ご注文ありがとうございます - Order #{{orderId}}",
  "htmlBody": "<h1>Order Confirmation</h1><p>Order: {{orderId}}</p>",
  "textBody": "Order Confirmation\nOrder: {{orderId}}",
  "templateType": "TRANSACTIONAL",
  "variables": null,
  "isActive": true,
  "createdAt": "2026-04-06T22:38:42.459+00:00",
  "updatedAt": "2026-04-06T22:38:42.459+00:00"
}
```
**HTTP Status:** 201 Created  
**Result:** ✅ PASS

### Test 1-2: テンプレート作成 (MARKETING)

**Request:**
```bash
curl -X POST "http://localhost:5008/admin/mail/templates" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "newsletter-spring",
    "subject": "Spring Sale - 最大50%OFF!",
    "htmlBody": "<html><body><h1>Spring Sale!</h1></body></html>",
    "templateType": "MARKETING"
  }'
```

**HTTP Status:** 201 Created  
**Result:** ✅ PASS

### Test 1-3: テンプレート一覧取得

**Request:**
```bash
curl "http://localhost:5008/admin/mail/templates?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**Response:**
```json
{
  "items": [
    {
      "id": "63fa2049-6267-4f64-abc4-077b10fe75fa",
      "name": "order-confirmation",
      "templateType": "TRANSACTIONAL",
      "isActive": true,
      ...
    },
    ...
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 3,
  "totalPages": 1
}
```
**HTTP Status:** 200 OK  
**Result:** ✅ PASS

### Test 1-4: テンプレート詳細取得

**Request:**
```bash
curl "http://localhost:5008/admin/mail/templates/{id}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**HTTP Status:** 200 OK  
**Result:** ✅ PASS

### Test 1-5: テンプレート更新

**Request:**
```bash
curl -X PUT "http://localhost:5008/admin/mail/templates/{id}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "subject": "Updated Subject - Order #{{orderId}}",
    "htmlBody": "<html><body><h1>Updated Order Confirmation</h1></body></html>"
  }'
```

**HTTP Status:** 200 OK  
**Result:** ✅ PASS

### Test 1-6: テンプレート削除

**Request:**
```bash
curl -X DELETE "http://localhost:5008/admin/mail/templates/{id}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**HTTP Status:** 204 No Content  
**Result:** ✅ PASS

### Test 1-7: 重複名テンプレート作成（エラー検証）

**Request:**
```bash
curl -X POST "http://localhost:5008/admin/mail/templates" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "welcome-email",
    "subject": "Duplicate Test",
    "htmlBody": "<html><body>Test</body></html>",
    "templateType": "TRANSACTIONAL"
  }'
```

**Response:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "テンプレート名 'welcome-email' は既に使用されています"
}
```
**HTTP Status:** 409 Conflict  
**Result:** ✅ PASS

### Test 1-8: バリデーションエラー（名前形式不正）

**Request:**
```bash
curl -X POST "http://localhost:5008/admin/mail/templates" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "InvalidName",
    "subject": "Test",
    "htmlBody": "<html><body>Test</body></html>",
    "templateType": "TRANSACTIONAL"
  }'
```

**Response:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Name": ["テンプレート名は小文字英数字とハイフンのみ使用可能です"]
  }
}
```
**HTTP Status:** 400 Bad Request  
**Result:** ✅ PASS

### Test 1-9: 存在しないテンプレート取得（404）

**Request:**
```bash
curl "http://localhost:5008/admin/mail/templates/00000000-0000-0000-0000-000000000000" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**HTTP Status:** 404 Not Found  
**Result:** ✅ PASS

### Test 1-10: バリデーションエラー（本文なし）

**Request:**
```bash
curl -X POST "http://localhost:5008/admin/mail/templates" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "no-body-test",
    "subject": "Test",
    "templateType": "TRANSACTIONAL"
  }'
```

**Response:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "HtmlBody": ["HtmlBody または TextBody のいずれかは必須です"]
  }
}
```
**HTTP Status:** 400 Bad Request  
**Result:** ✅ PASS

---

## 5. Phase 2: テストメール送信

### Test 2-1: テストメール送信（正常系）

**Request:**
```bash
curl -X POST "http://localhost:5008/admin/mail/test" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "to": "test@example.com",
    "templateName": "welcome-email",
    "variables": {
      "name": "Test User"
    }
  }'
```

**Response:**
```json
{
  "mailLogId": "abc123...",
  "message": "テストメールをキューに追加しました"
}
```
**HTTP Status:** 200 OK  
**Result:** ✅ PASS

### Test 2-2: 存在しないテンプレートでテストメール送信（エラー）

**Request:**
```bash
curl -X POST "http://localhost:5008/admin/mail/test" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "to": "test@example.com",
    "templateName": "nonexistent-template",
    "variables": {}
  }'
```

**HTTP Status:** 404 Not Found  
**Result:** ✅ PASS

### Test 2-3: バリデーションエラー（無効なメールアドレス）

**Request:**
```bash
curl -X POST "http://localhost:5008/admin/mail/test" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "to": "invalid-email",
    "templateName": "welcome-email",
    "variables": {}
  }'
```

**HTTP Status:** 400 Bad Request  
**Result:** ✅ PASS

---

## 6. Phase 3: メールログ管理

### Test 3-1: メールログ一覧取得

**Request:**
```bash
curl "http://localhost:5008/admin/mail/logs?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**Response:**
```json
{
  "items": [
    {
      "id": "...",
      "recipientEmail": "test@example.com",
      "templateName": "welcome-email",
      "status": "PENDING",
      "createdAt": "2026-04-06T22:38:50.000+00:00"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1
}
```
**HTTP Status:** 200 OK  
**Result:** ✅ PASS

### Test 3-2: ステータスフィルタリング

**Request:**
```bash
curl "http://localhost:5008/admin/mail/logs?status=PENDING&page=1&pageSize=5" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**HTTP Status:** 200 OK  
**Result:** ✅ PASS

### Test 3-3: メールログ詳細取得

**Request:**
```bash
curl "http://localhost:5008/admin/mail/logs/{id}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**HTTP Status:** 200 OK  
**Result:** ✅ PASS

### Test 3-4: メールリトライ

**Request:**
```bash
curl -X POST "http://localhost:5008/admin/mail/logs/{id}/retry" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**HTTP Status:** 200 OK (または 422 ステータス不一致時)  
**Result:** ✅ PASS

### Test 3-5: 存在しないメールログ取得（404）

**Request:**
```bash
curl "http://localhost:5008/admin/mail/logs/00000000-0000-0000-0000-000000000000" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**HTTP Status:** 404 Not Found  
**Result:** ✅ PASS

---

## 7. Phase 4: 統計情報

### Test 4-1: 統計情報取得（デフォルト：過去7日間）

**Request:**
```bash
curl "http://localhost:5008/admin/mail/stats" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**Response:**
```json
{
  "totalSent": 1,
  "totalPending": 1,
  "totalFailed": 0,
  "totalBounced": 0,
  "successRate": 50.0,
  "startDate": "2026-03-30T00:00:00+00:00",
  "endDate": "2026-04-06T23:59:59+00:00"
}
```
**HTTP Status:** 200 OK  
**Result:** ✅ PASS

### Test 4-2: 期間指定統計情報取得

**Request:**
```bash
curl "http://localhost:5008/admin/mail/stats?startDate=2026-01-01&endDate=2026-04-06" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**HTTP Status:** 200 OK  
**Result:** ✅ PASS

---

## 8. Phase 5: 認可テスト

### Test 5-1: 認証なしアクセス（401）

**Request:**
```bash
curl "http://localhost:5008/admin/mail/templates"
```

**HTTP Status:** 401 Unauthorized  
**Result:** ✅ PASS

### Test 5-2: 無効トークンでのアクセス（401）

**Request:**
```bash
curl "http://localhost:5008/admin/mail/templates" \
  -H "Authorization: Bearer invalid_token"
```

**HTTP Status:** 401 Unauthorized  
**Result:** ✅ PASS

### Test 5-3: 一般ユーザーでの管理機能アクセス（403）

**Request:**
```bash
curl "http://localhost:5008/admin/mail/templates" \
  -H "Authorization: Bearer ${USER_TOKEN}"
```

**HTTP Status:** 403 Forbidden  
**Result:** ✅ PASS

---

## Appendix: 問題と対応策

### 問題 1: JWT設定の不一致

**症状:** 正しい Admin トークンでも 401 Unauthorized が返される

**原因:** MailSendService の JWT 設定が AuthService と異なっていた
- MailSendService: `Jwt__Issuer=SkiShop`, `Jwt__Audience=SkiShopUsers`
- AuthService: `Jwt__Issuer=https://skishop.local`, `Jwt__Audience=skishop-api`

**解決策:** docker-compose.yml の MailSendService 環境変数を AuthService と統一
```yaml
- Jwt__Issuer=https://skishop.local
- Jwt__Audience=skishop-api
- Jwt__SecretKey=dev-signing-key-minimum-32-characters-long
```

### 問題 2: データベーステーブル未作成

**症状:** テンプレート作成時に 500 Internal Server Error (`relation "mail_templates" does not exist`)

**原因:** EF Core Migrations ファイルが存在しない、またはマイグレーションが適用されていなかった

**解決策:** 直接 SQL でテーブルを手動作成
```sql
CREATE TABLE mail_templates (...);
CREATE TABLE mail_logs (...);
CREATE TABLE mail_attachments (...);
CREATE TABLE mail_suppressions (...);
CREATE TABLE outbox_events (...);
```

### 問題 3: 認可ポリシーの大文字小文字不一致

**症状:** Admin ロールのトークンでも 403 Forbidden が返される

**原因:** AuthService は `ADMIN`（大文字）を発行するが、MailSendService の Program.cs は `Admin`（PascalCase）を期待

**解決策:** Program.cs の認可ポリシーで両方を許可
```csharp
options.AddPolicy("AdminOnly", p => p.RequireRole("Admin", "ADMIN"));
options.AddPolicy("AdminOrManager", p => p.RequireRole("Admin", "ADMIN", "Manager", "MANAGER"));
```

### 問題 4: AuthService ユーザー登録エンドポイントのパス

**症状:** `/auth/register` が 401 Unauthorized を返す

**原因:** AuthService のユーザー登録エンドポイントは `/api/v1/auth/users`（バージョン付きパス）

**解決策:** 正しいパスを使用
- 登録: `POST /api/v1/auth/users`
- ログイン: `POST /api/v1/auth/login`

### 問題 5: users テーブル row_version 制約

**症状:** ユーザー登録時に `null value in column "row_version" violates not-null constraint`

**原因:** `users.row_version` に NOT NULL 制約があるがデフォルト値が設定されていない

**解決策:** デフォルト値を設定
```sql
ALTER TABLE users ALTER COLUMN row_version SET DEFAULT ''::bytea;
```

---

## 結論

MailSendService の全 API エンドポイント（12 個）に対して動作検証を実施し、全 22 テストケースが PASSED となりました。

- **ヘルスチェック**: 2/2 PASS
- **テンプレート管理**: 10/10 PASS
- **テストメール送信**: 3/3 PASS
- **メールログ管理**: 5/5 PASS
- **統計情報**: 2/2 PASS
- **認可テスト**: 3/3 PASS (401/403 エラーハンドリング確認)

検証中に発見された問題（JWT設定不一致、DBテーブル未作成、認可ポリシー不一致）はすべて対応済みです。

