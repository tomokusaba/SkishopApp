# 修正レポート: authentication-service-design.md

## 概要
- **対象**: `design-docs/authentication-service-design.md`
- **修正日時**: 2026-04-03
- **基準レポート**: `check-report-1.md`

---

## Critical 修正結果: 3/3 完了

| # | 指摘 | 修正内容 | 状態 |
|---|------|---------|------|
| C-1 | 全日時カラムが `TIMESTAMP`（TIMESTAMP WITH TIME ZONE 必須） | users, user_sessions, oauth_accounts, security_logs, user_roles, password_resets, user_mfa, outbox_events の全日時カラムを `TIMESTAMP WITH TIME ZONE` に修正。ER 図の型も `timestamptz` に修正 | ✅ 完了 |
| C-2 | DB 名が `skishopdb`（ADR-0006 違反） | サービス情報テーブル・環境変数の DB 名を `authdb` に修正 | ✅ 完了 |
| C-3 | JWT アルゴリズム HS512（対称鍵、セキュリティリスク） | RS256（非対称鍵）に変更。AuthService のみ秘密鍵保持、他サービスは公開鍵で検証。環境変数を `Jwt__PrivateKeyPath`/`Jwt__PublicKeyPath`/`Jwt__KeyVaultKeyId` に変更。ログインレスポンス例の JWT ヘッダーも RS256 に修正 | ✅ 完了 |

---

## High 修正結果: 20/20 完了

| # | チェックレポート# | 指摘 | 修正内容 | 状態 |
|---|-----------------|------|---------|------|
| H-1 | #4 | Outbox パターン欠落（ADR-0005） | `outbox_events` テーブル定義（CHECK 制約・インデックス含む）、`OutboxEvent` EF Core エンティティ、`OutboxPublisher` BackgroundService（動的バックオフ設計）を追加。§14 として新セクション追加 | ✅ 完了 |
| H-2 | #5 | Kafka トピック命名不整合 | `auth.users`/`auth.sessions`/`auth.security` → `user.registered`, `auth.login.success`, `auth.login.failed`, `auth.session.ended`, `auth.security.incident` に統一 | ✅ 完了 |
| H-3 | #6 | パスワードハッシュ BCrypt（AGENTS.md 不整合） | BCrypt → Argon2id に変更（spec.md の性能要件にも記載あり）。カスタム `IPasswordHasher<ApplicationUser>` 実装として明記 | ✅ 完了 |
| H-4 | #7 | イベントペイロードに PII（email） | `UserAuthenticated` イベントから `email` フィールド除去。`UserRegistered` ペイロードからもメール・登録データを除去し userId/ロール/日時のみに。PII 最小化原則の注記を追加 | ✅ 完了 |
| H-5 | #8 | users.status に CHECK 制約欠落 | `CONSTRAINT ck_users_status CHECK (status IN ('PENDING_VERIFICATION', 'ACTIVE', 'SUSPENDED'))` + NOT NULL + DEFAULT 追加 | ✅ 完了 |
| H-6 | #9 | users.role に CHECK 制約欠落 | `CONSTRAINT ck_users_role CHECK (role IN ('ADMIN', 'MANAGER', 'STAFF', 'EMPLOYEE', 'USER', 'CUSTOMER'))` 追加 | ✅ 完了 |
| H-7 | #10 | roles テーブル定義欠落 | `roles` テーブル（id, name, description, created_at, updated_at）+ CHECK 制約 + UNIQUE を追加 | ✅ 完了 |
| H-8 | #11 | EF Core エンティティ定義欠落 | §12 として User, UserSession, SecurityLog, OutboxEvent の完全な C# クラス定義を追加。`[Table]`, `[Column]`, `[Key]`, `[Required]`, `[MaxLength]`, `[Timestamp]` 属性、`= []` コレクション初期化、`DateTime.UtcNow` 使用 | ✅ 完了 |
| H-9 | #12 | Service/Repository インターフェース欠落 | §13 として IAuthService, IJwtTokenService, IMfaService, IPasswordService, IUserRepository, IUserSessionRepository, ISecurityLogRepository を追加。全メソッドに `CancellationToken ct = default` 含む | ✅ 完了 |
| H-10 | #13 | CancellationToken 記載欠落 | Minimal API エンドポイント実装パターン（AuthEndpoints）に `CancellationToken ct` パラメータを含む完全な実装例を追加 | ✅ 完了 |
| H-11 | #14 | エラーレスポンスが RFC 9457 非準拠（ADR-0007） | 独自 JSON 形式 → RFC 9457 Problem Details 形式に変更。`TypedResults.Problem()` 使用例・カスタムエラーコードは `extensions` フィールドに格納 | ✅ 完了 |
| H-12 | #15 | MFA シークレットの暗号化記載なし | `secret_key`・`backup_codes` カラム説明に AES-256-GCM 暗号化保存・Azure Key Vault 鍵管理を明記 | ✅ 完了 |
| H-13 | #16 | Kafka イベントの PII / GDPR 未対応 | イベントペイロード PII 最小化の注記を追加（GDPR 第 17 条対応）。H-4 と同時対応済み | ✅ 完了 |
| H-14 | #17 | SecurityLog details の PII 混入リスク | `details` JSONB フィールドのホワイトリスト定義（authMethod, mfaUsed, failureReason, provider, riskScore, actionTaken）と PII 格納禁止の明示的注記を追加 | ✅ 完了 |
| H-15 | #18 | Dockerfile HEALTHCHECK が curl 使用（未インストール） | `curl -f` → `wget --spider -q` に変更。ポートも 5001 → 8080 に修正（.NET 8+ デフォルト） | ✅ 完了 |
| H-16 | #19 | テストモック Moq（AGENTS.md は NSubstitute） | `Moq` → `NSubstitute, Shouldly` に変更（テスト記述 + AGENTS.md §8.1 必須パッケージリスト） | ✅ 完了 |
| H-17 | #20 | テスト戦略セクション欠落 | §16 としてテスト戦略を追加。テスト種別、命名規約（Should_期待結果_When_条件）、AAA パターン例、主要テストケース一覧、カバレッジ目標（80%+） | ✅ 完了 |
| H-18 | #21 | API パスバージョニング不統一 | `/api/auth/users` → `/api/v1/auth/users` に統一。`/api/user/me` → `/api/v1/auth/user/me`、`/api/graph/user` → `/api/v1/auth/graph/user`、`/oauth2/authorization/azure` → `/api/v1/auth/oauth2/authorization/azure` に修正。`/signin-oidc` はフレームワーク制約として注記 | ✅ 完了 |
| H-19 | #22 | ログインレスポンスの success ラッパー | `success` フィールドを除去。成功時は 200 OK + レスポンスボディ、失敗時は RFC 9457 Problem Details | ✅ 完了 |
| H-20 | #23 | 外部サービスに Polly/Resilience 設計なし | §15 として耐障害性設計を追加。Azure AD / Graph API 用の `IHttpClientFactory` + `AddStandardResilienceHandler` 設定、ログインエンドポイント専用レート制限（5 req/min）を追加 | ✅ 完了 |

---

## 追加修正（Medium/Low 指摘の一部対応）

| # | 指摘 | 修正内容 |
|---|------|---------|
| M-1 | Serilog.Sinks.Console 6.* 欠落（OSS-2） | NuGet パッケージリストに追加 |
| M-2 | FluentValidation.DependencyInjectionExtensions 欠落（PR-6） | NuGet パッケージリストに追加 |
| M-3 | Polly / Microsoft.Extensions.Http.Resilience 欠落 | NuGet パッケージリストに追加 |
| M-4 | Docker バージョン「最新」（INFRA-3） | 技術スタック詳細テーブルで `25.x` に固定 |
| M-5 | パフォーマンス「現在値」が設計段階で不適切（PERF-3） | 「現在値」列を除去し「目標値」のみに変更 |
| M-6 | マイクロサービス関連図に「監査サービス」（ARCH-5） | 監査サービスを図から除去（SecurityLog + OpenTelemetry で実現） |
| M-7 | oauth_accounts に UNIQUE 制約欠落（DBA-5） | `CONSTRAINT uq_oauth_provider_user UNIQUE (provider, provider_user_id)` を追加 |
| M-8 | security_logs の updated_at 欠落理由（DBA-6） | append-only 設計のため不要である旨を明記 |
| M-9 | 外部キー ON DELETE 動作未定義（DBA-7） | 全 FK の ON DELETE 動作一覧テーブルを追加 |
| M-10 | password_resets.token 生成方式（DBA-8） | `RandomNumberGenerator.GetBytes(64)` による暗号論的疑似乱数生成を注記 |
| M-11 | DTO record 定義欠落（PR-4） | §13 で LoginRequest, TokenRefreshRequest 等の record 定義 + Data Annotations を追加 |
| M-12 | Minimal API エンドポイントパターン欠落（PR-5） | §13 で AuthEndpoints.MapAuthEndpoints() の完全実装パターンを追加 |
| M-13 | Dockerfile ポート不整合（INFRA-2） | EXPOSE 5001 → 8080 に変更、ASPNETCORE_URLS も 8080 に統一 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 内容 | 対応状況 |
|---|--------|------|---------|
| 1 | ~~最優先~~ | JWT アルゴリズム → RS256 に修正済み | ✅ 対応完了（spec.md に基づき RS256 を採用） |
| 2 | ~~最優先~~ | パスワードハッシュ → Argon2id に修正済み | ✅ 対応完了（spec.md に基づき Argon2id を採用） |
| 3 | 高優先 | AuthService と UserManagementService の責務境界 | ⚠️ 未対応 — API パスは統一済みだが、ユーザー CRUD が AuthService に残存。プロダクトオーナー + テックリードの判断が必要 |
| 4 | 高優先 | SecurityLog の保持期間・削除ポリシー | ⚠️ 未対応 — GDPR「忘れられる権利」と監査要件のバランスは法務 + セキュリティアーキテクトの判断が必要 |

---

## 修正統計サマリー

| 重要度 | 検出数 | 修正数 | 残件数 |
|--------|--------|--------|--------|
| **Critical** | 3 | 3 | 0 |
| **High** | 20 | 20 | 0 |
| **Medium** | 21 | 13 | 8 |
| **Low** | 4 | 1 | 3 |

**判定**: Critical 0 件 / High 0 件 → 再レビューで **✅ Pass** 判定を期待

---

## 新規追加セクション一覧

| セクション番号 | タイトル | 内容 |
|--------------|---------|------|
| §12 | EF Core エンティティ定義 | User, UserSession, SecurityLog, OutboxEvent の完全な C# クラス定義 |
| §13 | Service / Repository インターフェース定義 | IAuthService, IJwtTokenService 等のインターフェース + DTO record 定義 + Minimal API エンドポイントパターン |
| §14 | Outbox パターン実装設計 | OutboxPublisher BackgroundService（動的バックオフ） |
| §15 | 耐障害性（Resilience）設計 | IHttpClientFactory + Polly 設定 + ログイン専用レート制限 |
| §16 | テスト戦略 | テスト種別・命名規約・主要テストケース・カバレッジ目標 |
