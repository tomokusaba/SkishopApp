# AuthService 検証計画書（verification-plan.md）

本ドキュメントは AuthService の全エンドポイントに対する動作検証の手順とフォーマットを定義する。
検証結果は `verification-report.md` に記録し、AuthService の使い方を理解するためのリファレンスとして活用する。

---

## 1. 検証の目的

1. **機能確認**: 全エンドポイントが設計通りに動作することを確認する
2. **使い方の文書化**: 各 API の呼び出し方法・パラメータ・レスポンスを実例で示す
3. **異常系の確認**: バリデーションエラー、認証エラー等の動作を確認する

---

## 2. 前提条件

### 2.1 環境構成

| 項目 | 値 |
|------|-----|
| AuthService URL | `http://localhost:5001` |
| PostgreSQL | `localhost:5432` |
| Redis | `localhost:6379` |
| Kafka | `localhost:9092` |

### 2.2 起動コマンド

```bash
# Docker Compose で AuthService と依存サービスを起動
cd /Users/yoterada/GitHub/DotNet-Skishop-App
docker compose up -d auth-service
```

### 2.3 事前確認コマンド

```bash
# ヘルスチェック
curl -s http://localhost:5001/health
# 期待結果: Healthy
```

---

## 3. エンドポイント一覧

### 3.1 User Registration（ユーザー登録）

| メソッド | パス | 説明 | 認証 |
|---------|------|------|------|
| POST | `/api/v1/auth/users` | 新規ユーザー登録 | 不要 |
| DELETE | `/api/v1/auth/users/{userId}` | ユーザー論理削除（退会） | 必要 |
| DELETE | `/api/v1/auth/users/{userId}/hard` | ユーザー物理削除 | Admin のみ |

### 3.2 Authentication（認証）

| メソッド | パス | 説明 | 認証 |
|---------|------|------|------|
| POST | `/api/v1/auth/login` | ログイン | 不要 |
| POST | `/api/v1/auth/refresh` | トークンリフレッシュ | 不要 |
| POST | `/api/v1/auth/logout` | ログアウト | 必要 |
| POST | `/api/v1/auth/validate` | トークン検証 | 必要 |
| GET | `/api/v1/auth/me` | 現在のユーザー情報取得 | 必要 |

### 3.3 Password（パスワード）

| メソッド | パス | 説明 | 認証 |
|---------|------|------|------|
| POST | `/api/v1/auth/password/reset` | パスワードリセット要求 | 不要 |
| POST | `/api/v1/auth/password/confirm` | パスワードリセット確定 | 不要 |
| PUT | `/api/v1/auth/password/change` | パスワード変更 | 必要 |

### 3.4 MFA（多要素認証）

| メソッド | パス | 説明 | 認証 |
|---------|------|------|------|
| POST | `/api/v1/auth/mfa/verify` | MFA コード検証 | 不要（SessionToken 必要） |
| POST | `/api/v1/auth/mfa/setup` | MFA セットアップ開始 | 必要 |
| DELETE | `/api/v1/auth/mfa/disable` | MFA 無効化 | 必要 |

### 3.5 Email Verification（メール確認）

| メソッド | パス | 説明 | 認証 |
|---------|------|------|------|
| POST | `/api/v1/auth/email/verify` | メール確認トークン検証 | 不要 |
| POST | `/api/v1/auth/email/resend` | 確認メール再送信 | 必要 |

### 3.6 OAuth 2.0 / Client Credentials

| メソッド | パス | 説明 | 認証 |
|---------|------|------|------|
| POST | `/api/v1/auth/token` | Client Credentials トークン取得 | 不要 |
| GET | `/api/v1/auth/oauth2/authorization/{provider}` | OAuth フロー開始（未実装） | 不要 |
| POST | `/api/v1/auth/oauth2/link/{provider}?code=xxx` | OAuth アカウント連携 | 必要 |
| DELETE | `/api/v1/auth/oauth2/link/{provider}` | OAuth アカウント連携解除 | 必要 |
| GET | `/api/v1/auth/oauth2/accounts` | 連携済みアカウント一覧 | 必要 |

> **📝 注意**: `/api/v1/auth/oauth2/link/{provider}` は認可コードを `code` クエリパラメータで受け取る。

---

## 4. 検証手順

検証は以下の順序で実施する。依存関係を考慮した順番となっている。

### Phase 1: ユーザー登録とログイン（基本フロー）

#### テスト 1-1: 新規ユーザー登録

```bash
curl -X POST http://localhost:5001/api/v1/auth/users \
  -H "Content-Type: application/json" \
  -d '{
    "email": "testuser@example.com",
    "username": "testuser",
    "password": "Password123!",
    "firstName": "Test",
    "lastName": "User"
  }'
```

**期待するレスポンス（201 Created）:**

```json
{
  "id": "uuid-xxx",
  "email": "testuser@example.com",
  "username": "testuser",
  "firstName": "Test",
  "lastName": "User",
  "status": "PENDING_VERIFICATION",
  "role": "USER",
  "createdAt": "2026-04-06T10:00:00Z"
}
```

**検証項目:**
- [ ] ステータスコード 201 を返す
- [ ] `Location` ヘッダーに新しいユーザーの URI が含まれる
- [ ] レスポンスにユーザー情報が含まれる（パスワードは含まれない）
- [ ] `status` が `PENDING_VERIFICATION` である（メール未確認状態）

#### テスト 1-2: ユーザー登録（バリデーションエラー）

```bash
curl -X POST http://localhost:5001/api/v1/auth/users \
  -H "Content-Type: application/json" \
  -d '{
    "email": "invalid-email",
    "username": "ab",
    "password": "short"
  }'
```

**期待するレスポンス（400 Bad Request）:**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Email": ["有効なメールアドレスを入力してください"],
    "Username": ["ユーザー名は3〜100文字で入力してください"],
    "Password": ["パスワードは8〜100文字で入力してください"]
  }
}
```

**検証項目:**
- [ ] ステータスコード 400 を返す
- [ ] 全てのバリデーションエラーが一度に返される

#### テスト 1-3: ユーザー登録（メールアドレス重複）

```bash
# 同じメールアドレスで再度登録
curl -X POST http://localhost:5001/api/v1/auth/users \
  -H "Content-Type: application/json" \
  -d '{
    "email": "testuser@example.com",
    "username": "testuser2",
    "password": "Password123!"
  }'
```

**期待するレスポンス（409 Conflict または 422 Unprocessable Entity）:**

```json
{
  "type": "...",
  "title": "Conflict",
  "status": 409,
  "detail": "指定されたメールアドレスは既に登録されています"
}
```

**検証項目:**
- [ ] ステータスコード 409 または 422 を返す
- [ ] 適切なエラーメッセージが含まれる

#### テスト 1-4: ログイン

```bash
curl -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "testuser@example.com",
    "password": "Password123!"
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "uuid-refresh-token",
  "tokenType": "Bearer",
  "expiresIn": 3600,
  "user": {
    "id": "uuid-xxx",
    "firstName": "Test",
    "lastName": "User",
    "role": "USER"
  }
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `accessToken` が JWT 形式である
- [ ] `refreshToken` が含まれる
- [ ] `expiresIn` がアクセストークンの有効期限（秒）を示す
- [ ] `user` オブジェクトにユーザー基本情報が含まれる

> **📝 メモ**: 取得した `accessToken` と `refreshToken` を以降のテストで使用するため記録しておく。

#### テスト 1-5: ログイン（認証失敗）

```bash
curl -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "testuser@example.com",
    "password": "WrongPassword!"
  }'
```

**期待するレスポンス（401 Unauthorized）:**

```json
{
  "type": "...",
  "title": "Unauthorized",
  "status": 401,
  "detail": "認証情報が無効です"
}
```

**検証項目:**
- [ ] ステータスコード 401 を返す
- [ ] パスワードが正しいかどうかの詳細は漏らさない

#### テスト 1-6: MFA 有効ユーザーのログイン（MFA 要求レスポンス）

> **📝 注意**: このテストは MFA を有効化した後に実行する。Phase 5 で MFA をセットアップ後に検証。

```bash
curl -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "mfa-user@example.com",
    "password": "Password123!"
  }'
```

**期待するレスポンス（202 Accepted）:**

```json
{
  "type": "https://httpstatuses.com/202",
  "title": "MFA Required",
  "status": 202,
  "detail": "MFA 認証が必要です",
  "sessionToken": "base64-encoded-session-token"
}
```

**検証項目:**
- [ ] ステータスコード 202 を返す
- [ ] `sessionToken` が含まれる（MFA 検証で使用）
- [ ] この時点ではアクセストークンは発行されない

---

### Phase 2: 認証済みエンドポイントの検証

#### テスト 2-1: 現在のユーザー情報取得（/me）

```bash
# ACCESS_TOKEN は Phase 1 で取得したトークン
curl -X GET http://localhost:5001/api/v1/auth/me \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "id": "uuid-xxx",
  "email": "testuser@example.com",
  "firstName": "Test",
  "lastName": "User",
  "role": "USER",
  "createdAt": "2026-04-06T10:00:00Z"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] JWT のユーザー情報と一致する

#### テスト 2-2: トークン検証

```bash
curl -X POST http://localhost:5001/api/v1/auth/validate \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "isValid": true,
  "userId": "uuid-xxx",
  "role": "USER",
  "expiresAt": "2026-04-06T11:00:00Z"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `isValid` が `true` である

#### テスト 2-3: 認証なしでアクセス

```bash
curl -X GET http://localhost:5001/api/v1/auth/me
```

**期待するレスポンス（401 Unauthorized）:**

**検証項目:**
- [ ] ステータスコード 401 を返す

---

### Phase 3: トークンリフレッシュとログアウト

#### テスト 3-1: トークンリフレッシュ

```bash
# REFRESH_TOKEN は Phase 1 で取得したトークン
curl -X POST http://localhost:5001/api/v1/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "'${REFRESH_TOKEN}'"
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "new-uuid-refresh-token",
  "expiresIn": 3600
}
```

> **📝 注意**: TokenRefreshResponse には `tokenType` フィールドがない。

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 新しい `accessToken` と `refreshToken` が発行される
- [ ] 古い `refreshToken` は無効化される（再利用不可）

#### テスト 3-2: 古いリフレッシュトークンの再利用（失敗するべき）

```bash
# 古い REFRESH_TOKEN を使用
curl -X POST http://localhost:5001/api/v1/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "'${OLD_REFRESH_TOKEN}'"
  }'
```

**期待するレスポンス（401 Unauthorized）:**

**検証項目:**
- [ ] ステータスコード 401 を返す
- [ ] トークンローテーションが正しく機能している

#### テスト 3-3: ログアウト

```bash
curl -X POST http://localhost:5001/api/v1/auth/logout \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（204 No Content）:**

（ボディなし）

**検証項目:**
- [ ] ステータスコード 204 を返す
- [ ] ログアウト後、リフレッシュトークンが無効化される

---

### Phase 4: パスワード管理

#### テスト 4-1: パスワード変更

```bash
curl -X PUT http://localhost:5001/api/v1/auth/password/change \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "currentPassword": "Password123!",
    "newPassword": "NewPassword456!"
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "message": "パスワードを変更しました"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 新しいパスワードでログインできる
- [ ] 古いパスワードでログインできなくなる

#### テスト 4-2: パスワードリセット要求

```bash
curl -X POST http://localhost:5001/api/v1/auth/password/reset \
  -H "Content-Type: application/json" \
  -d '{
    "email": "testuser@example.com"
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "message": "パスワードリセットメールを送信しました"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す（メールアドレスが存在しない場合も同じレスポンス）
- [ ] メール列挙攻撃を防止している

#### テスト 4-3: パスワードリセット確定

> **📝 注意**: 実際のリセットトークンはメールで送信されるため、テスト環境ではメールサービスのモックまたはログからトークンを取得する必要がある。

```bash
curl -X POST http://localhost:5001/api/v1/auth/password/confirm \
  -H "Content-Type: application/json" \
  -d '{
    "token": "reset-token-from-email",
    "newPassword": "ResetPassword789!"
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "message": "パスワードをリセットしました"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 新しいパスワードでログインできる

---

### Phase 5: MFA（多要素認証）

#### テスト 5-1: MFA セットアップ開始

```bash
curl -X POST http://localhost:5001/api/v1/auth/mfa/setup \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "secretKey": "BASE32ENCODEDKEY",
  "qrCodeUri": "otpauth://totp/SkiShop:user@example.com?secret=BASE32ENCODEDKEY&issuer=SkiShop",
  "backupCodes": [
    "ABCD-1234-EFGH",
    "IJKL-5678-MNOP",
    "..."
  ]
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] `qrCodeUri` をスキャンして認証アプリに登録できる
- [ ] `secretKey` を使って TOTP コードを生成できる
- [ ] `backupCodes` は安全な場所に保存する

#### テスト 5-2: MFA コード検証（セットアップ完了）

> **📝 注意**: 認証アプリで生成した 6 桁コードが必要。

```bash
# MFA セットアップ中に、確認のため検証が必要な場合
# または MFA 有効化後のログインフロー
curl -X POST http://localhost:5001/api/v1/auth/mfa/verify \
  -H "Content-Type: application/json" \
  -d '{
    "code": "123456",
    "sessionToken": "session-token-from-login"
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "uuid-refresh-token",
  "tokenType": "Bearer",
  "expiresIn": 3600,
  "user": {
    "id": "uuid-xxx",
    "firstName": "Test",
    "lastName": "User",
    "role": "USER"
  }
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 正しい TOTP コードで認証が完了する
- [ ] `user` オブジェクトにユーザー情報が含まれる

#### テスト 5-3: MFA 無効化

```bash
curl -X DELETE http://localhost:5001/api/v1/auth/mfa/disable \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（204 No Content）:**

（ボディなし）

**検証項目:**
- [ ] ステータスコード 204 を返す
- [ ] 以降のログインで MFA が不要になる

---

### Phase 6: Email Verification（メール確認）

#### テスト 6-1: メール確認トークン検証

> **📝 注意**: 確認トークンはユーザー登録時に送信されるメールに含まれる。

```bash
curl -X POST http://localhost:5001/api/v1/auth/email/verify \
  -H "Content-Type: application/json" \
  -d '{
    "token": "email-verification-token"
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "message": "メールアドレスを確認しました"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] ユーザーの `emailVerified` が `true` になる

#### テスト 6-2: 確認メール再送信

```bash
curl -X POST http://localhost:5001/api/v1/auth/email/resend \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
{
  "message": "確認メールを再送信しました"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 新しい確認メールが送信される

---

### Phase 7: Client Credentials（サービス間認証）

#### テスト 7-1: Client Credentials トークン取得

> **📝 前提**: AuthService に OAuth クライアントが事前登録されている必要がある。

```bash
curl -X POST http://localhost:5001/api/v1/auth/token \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "inventory-service",
    "clientSecret": "client-secret-xxx",
    "scope": "inventory:read inventory:write",
    "grantType": "client_credentials"
  }'
```

**期待するレスポンス（200 OK）:**

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "tokenType": "Bearer",
  "expiresIn": 3600,
  "scope": "inventory:read inventory:write"
}
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 要求したスコープがレスポンスに含まれる

---

### Phase 8: OAuth アカウント連携

#### テスト 8-1: OAuth 認可フロー開始（未実装）

```bash
curl -X GET http://localhost:5001/api/v1/auth/oauth2/authorization/google
```

**期待するレスポンス（501 Not Implemented）:**

```json
{
  "type": "...",
  "title": "Not Implemented",
  "status": 501,
  "detail": "OAuth authorization flow is not yet implemented"
}
```

**検証項目:**
- [ ] ステータスコード 501 を返す（未実装であることを明示）

#### テスト 8-2: OAuth アカウント連携（認証必要）

> **📝 注意**: 実際の OAuth フローでは、Google/GitHub 等から取得した認可コードを使用する。
> テスト環境では擬似的なコードを使用してエラーレスポンスを検証する。

```bash
curl -X POST "http://localhost:5001/api/v1/auth/oauth2/link/google?code=test-auth-code" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（成功時 200 OK）:**

```json
{
  "message": "アカウント連携が完了しました"
}
```

**期待するレスポンス（無効なコード 400 Bad Request）:**

```json
{
  "type": "...",
  "title": "Bad Request",
  "status": 400,
  "detail": "無効な認可コードです"
}
```

**検証項目:**
- [ ] 認可コードをクエリパラメータ `?code=xxx` で渡す
- [ ] 認証が必要（`Authorization` ヘッダー必須）

#### テスト 8-3: 連携済み OAuth アカウント一覧取得

```bash
curl -X GET http://localhost:5001/api/v1/auth/oauth2/accounts \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（200 OK）:**

```json
[
  {
    "provider": "google",
    "providerUserId": "google-user-id-xxx",
    "createdAt": "2026-04-06T10:00:00Z"
  }
]
```

**検証項目:**
- [ ] ステータスコード 200 を返す
- [ ] 連携済みの OAuth アカウント一覧が返る
- [ ] 未連携の場合は空配列 `[]` が返る

#### テスト 8-4: OAuth アカウント連携解除

```bash
curl -X DELETE http://localhost:5001/api/v1/auth/oauth2/link/google \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（204 No Content）:**

（ボディなし）

**検証項目:**
- [ ] ステータスコード 204 を返す
- [ ] 連携解除後、アカウント一覧から該当プロバイダーが消える

#### テスト 8-5: OAuth アカウント連携解除（認証なし）

```bash
curl -X DELETE http://localhost:5001/api/v1/auth/oauth2/link/google
```

**期待するレスポンス（401 Unauthorized）:**

**検証項目:**
- [ ] ステータスコード 401 を返す

---

### Phase 9: ユーザー削除

#### テスト 9-1: ユーザー論理削除（退会）

```bash
# 自身のアカウントを削除（USER_ID は自分の ID）
curl -X DELETE http://localhost:5001/api/v1/auth/users/${USER_ID} \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（204 No Content）:**

**検証項目:**
- [ ] ステータスコード 204 を返す
- [ ] 削除後はログインできなくなる

#### テスト 9-2: 他ユーザーの削除（権限なし）

```bash
# 他のユーザーを削除しようとする
curl -X DELETE http://localhost:5001/api/v1/auth/users/other-user-id \
  -H "Authorization: Bearer ${ACCESS_TOKEN}"
```

**期待するレスポンス（403 Forbidden）:**

```json
{
  "type": "...",
  "title": "Forbidden",
  "status": 403,
  "detail": "他のユーザーを削除する権限がありません"
}
```

**検証項目:**
- [ ] ステータスコード 403 を返す
- [ ] IDOR 攻撃が防止されている

#### テスト 9-3: ユーザー物理削除（管理者のみ）

```bash
# ADMIN ロールを持つユーザーのトークンを使用
curl -X DELETE http://localhost:5001/api/v1/auth/users/${USER_ID}/hard \
  -H "Authorization: Bearer ${ADMIN_ACCESS_TOKEN}"
```

**期待するレスポンス（204 No Content）:**

**検証項目:**
- [ ] ステータスコード 204 を返す
- [ ] ユーザーデータがデータベースから完全に削除される
- [ ] 通常ユーザーが実行すると 403 Forbidden を返す

---

## 5. verification-report.md のフォーマット

検証結果は以下のフォーマットで `verification-report.md` に記録する。

### 5.1 レポート構成

```markdown
# AuthService 検証レポート

## 実施情報

| 項目 | 内容 |
|------|------|
| 実施日 | YYYY-MM-DD |
| 実施者 | 名前 |
| AuthService バージョン | v1.0.0 |
| 環境 | Docker Compose (Development) |

## サマリー

| カテゴリ | テスト数 | 成功 | 失敗 | スキップ |
|---------|---------|------|------|---------|
| User Registration | 3 | 3 | 0 | 0 |
| Authentication | 5 | 5 | 0 | 0 |
| ... | ... | ... | ... | ... |
| **合計** | **XX** | **XX** | **XX** | **XX** |

## 詳細結果

### Phase 1: ユーザー登録とログイン

#### テスト 1-1: 新規ユーザー登録

- **結果**: ✅ 成功 / ❌ 失敗 / ⏭️ スキップ
- **ステータスコード**: 201
- **実行コマンド**:

\```bash
curl -X POST http://localhost:5001/api/v1/auth/users \
  -H "Content-Type: application/json" \
  -d '{
    "email": "testuser@example.com",
    "username": "testuser",
    "password": "Password123!",
    "firstName": "Test",
    "lastName": "User"
  }'
\```

- **レスポンス**:

\```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "testuser@example.com",
  "username": "testuser",
  "firstName": "Test",
  "lastName": "User",
  "emailVerified": false,
  "mfaEnabled": false,
  "roles": ["USER"],
  "createdAt": "2026-04-06T10:15:30Z"
}
\```

- **備考**: （特記事項があれば記載）
```

### 5.2 記録する項目

各テストケースで以下を記録する:

1. **テスト名と番号**
2. **結果**: ✅ 成功 / ❌ 失敗 / ⏭️ スキップ
3. **ステータスコード**: 実際に返されたステータスコード
4. **実行コマンド**: 実際に実行した curl コマンド（トークンは `${VAR}` 形式に置換可）
5. **レスポンス**: 実際のレスポンス JSON（長い場合は重要部分のみ）
6. **備考**: 問題があった場合の詳細、特記事項

---

## 6. テスト用データ

### 6.1 テストユーザー

| 役割 | メールアドレス | パスワード | 用途 |
|------|-------------|----------|------|
| 一般ユーザー | testuser@example.com | Password123! | 基本機能テスト |
| 管理者 | admin@example.com | AdminPass123! | 管理者機能テスト |
| MFA 有効ユーザー | mfa-user@example.com | MfaPass123! | MFA フローテスト |

### 6.2 OAuth クライアント（Client Credentials 用）

| Client ID | Client Secret | Scope | 用途 |
|-----------|--------------|-------|------|
| inventory-service | secret-xxx | inventory:read inventory:write | サービス間認証テスト |

---

## 7. トラブルシューティング

### 7.1 よくある問題

#### ヘルスチェックが失敗する

```bash
# コンテナのログを確認
docker logs auth-service

# データベース接続を確認
docker exec -it auth-db psql -U postgres -d authdb -c "\dt"
```

#### トークンが無効と返される

- JWT の有効期限を確認する
- Jwt__SecretKey の設定を確認する
- トークンの形式（Bearer プレフィックス）を確認する

#### バリデーションエラーの詳細が分からない

- Content-Type: application/json ヘッダーを確認する
- JSON の形式が正しいか確認する

---

## 8. 実行手順チェックリスト

検証実施時に以下の順序で進める:

- [ ] Docker Compose で環境を起動
- [ ] ヘルスチェックで正常起動を確認
- [ ] Phase 1: ユーザー登録とログインを実施（トークン取得）
- [ ] Phase 2: 認証済みエンドポイントを検証
- [ ] Phase 3: トークンリフレッシュとログアウトを検証
- [ ] Phase 4: パスワード管理を検証
- [ ] Phase 5: MFA（必要に応じて）
- [ ] Phase 6: Email Verification（必要に応じて）
- [ ] Phase 7: Client Credentials（必要に応じて）
- [ ] Phase 8: OAuth アカウント連携（必要に応じて）
- [ ] Phase 9: ユーザー削除を検証
- [ ] verification-report.md に結果を記録
