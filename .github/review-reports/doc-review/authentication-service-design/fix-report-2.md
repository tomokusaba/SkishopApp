# fix-report-2: authentication-service-design.md

## 修正サマリー

| 指摘 ID | 重要度 | 内容 | 対応状況 |
|---------|--------|------|---------|
| NEW-H-1 | High | OAuthAccount, UserRole, Role, PasswordReset, UserMfa の 5 EF Core エンティティ未定義 | ✅ 修正完了 |

## 修正詳細

### NEW-H-1: 5 エンティティクラス追加
§12 EF Core エンティティ定義セクション末尾（OutboxEvent の後）に以下の 5 クラスを追加:

1. **OAuthAccount** (`[Table("oauth_accounts")]`): provider, provider_user_id, access_token, refresh_token, token_expires_at + User ナビゲーション
2. **Role** (`[Table("roles")]`): name, description + UserRoles コレクション `= []`
3. **UserRole** (`[Table("user_roles")]`): user_id, role_id, assigned_at, assigned_by + User/Role ナビゲーション
4. **PasswordReset** (`[Table("password_resets")]`): token, expires_at, is_used, used_at + User ナビゲーション
5. **UserMfa** (`[Table("user_mfa")]`): mfa_type, secret_key（AES-256-GCM 暗号化保存注記）, backup_codes（AES-256-GCM 暗号化保存注記）, is_enabled, verified_at + User ナビゲーション

全エンティティで AGENTS.md §10.3 準拠:
- `[Table("snake_case")]`, `[Column("snake_case")]` 属性
- `[Key]`, `[Required]`, `[MaxLength]` 属性
- `DateTime.UtcNow` で初期化
- コレクションナビゲーション `= []` 初期化

## 結果: Critical 0 / High 0（1 件修正完了）
