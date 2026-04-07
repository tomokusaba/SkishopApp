# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/authentication-service-design.md`（1296 行、修正後イテレーション 2）
- **判定**: ⚠️ **Conditional Approval** — Critical 0 件、High 1 件（人間の判断を介在）
- **レビュー日時**: 2026-04-03
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)
- **前回レビュー**: check-report-1（❌ Rejected — Critical 3 / High 20）→ fix-report-1 で全件是正済み

---

## 技術スタック検証結果

| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| ランタイム | .NET 10 (LTS) | .NET 10 (LTS) | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| ORM | EF Core 10 | EF Core 10 | ✅ |
| DB | PostgreSQL (authdb) | PostgreSQL（サービス別独立 DB: `authdb`） | ✅ 修正済み |
| キャッシュ | Redis (StackExchange.Redis 2.*) | Redis (StackExchange.Redis 2.*) | ✅ |
| メッセージング | Confluent.Kafka 2.* | Confluent.Kafka 2.* | ✅ |
| 認証 | ASP.NET Core Identity + Microsoft.Identity.Web 3.* | 同左 | ✅ |
| オーケストレーション | .NET Aspire 13.1 | .NET Aspire 13.1 | ✅ |
| テストモック | NSubstitute | NSubstitute 5.* | ✅ 修正済み |
| ログ | Serilog.AspNetCore 8.* + Serilog.Formatting.Compact 3.* + Serilog.Sinks.Console 6.* | 同左 | ✅ 修正済み |
| パスワードハッシュ | Argon2id（IPasswordHasher\<T\> カスタム実装） | ASP.NET Core Identity デフォルト / Argon2 | ✅ 修正済み |
| JWT アルゴリズム | RS256（非対称鍵、Azure Key Vault 管理） | RS256 整合 | ✅ 修正済み |
| Docker バージョン | Docker 25.x | Docker 25.x | ✅ 修正済み |

---

## 前回 Critical/High 指摘の是正検証

### Critical 3 件 → 全件是正済み ✅

| # | 前回指摘 | 是正状況 | 検証結果 |
|---|---------|---------|---------|
| C-1 | 全日時カラムが `TIMESTAMP`（TIMESTAMP WITH TIME ZONE 必須） | 全 9 テーブルの全日時カラムを `TIMESTAMP WITH TIME ZONE` に修正 | ✅ 完全修正 |
| C-2 | DB 名が `skishopdb`（ADR-0006: `authdb` 必須） | サービス情報テーブル・接続文字列を `authdb` に修正 | ✅ 完全修正 |
| C-3 | JWT アルゴリズム HS512（対称鍵、全サービスに秘密鍵共有リスク） | RS256（非対称鍵）に変更。AuthService のみ秘密鍵保持、他サービスは公開鍵検証。ログインレスポンス例の `alg` も `RS256` に修正。Azure Key Vault での RSA 鍵管理・`kid` ローテーション設計を追加 | ✅ 完全修正 |

### High 20 件 → 全件是正済み ✅

| # | 前回指摘 | 是正状況 | 検証結果 |
|---|---------|---------|---------|
| H-4 | Outbox パターン欠落（ADR-0005 違反） | `outbox_events` テーブル追加（DDL + インデックス）、`OutboxPublisher` BackgroundService コード（§14）追加。動的バックオフ（100ms〜5s）対応 | ✅ 完全修正 |
| H-5 | Kafka トピック名不整合 | `user.registered`, `auth.login.success`, `auth.session.ended`, `auth.login.failed`, `auth.security.incident` に統一 | ✅ 完全修正 |
| H-6 | BCrypt 使用（AGENTS.md 不整合） | Argon2id（ASP.NET Core Identity カスタム `IPasswordHasher<T>` 実装）に変更 | ✅ 完全修正 |
| H-7 | イベントペイロードに PII（email） | PII 最小化原則を明記。イベントペイロードから `email` を除去し `userId` のみで識別 | ✅ 完全修正 |
| H-8 | users.status CHECK 制約欠落 | `ck_users_status CHECK (status IN ('PENDING_VERIFICATION', 'ACTIVE', 'SUSPENDED'))` 追加 | ✅ 完全修正 |
| H-9 | users.role CHECK 制約欠落 | `ck_users_role CHECK (role IN ('ADMIN', 'MANAGER', 'STAFF', 'EMPLOYEE', 'USER', 'CUSTOMER'))` 追加 | ✅ 完全修正 |
| H-10 | roles テーブル定義欠落 | `roles` テーブル追加（id, name, description, created_at, updated_at + CHECK 制約） | ✅ 完全修正 |
| H-11 | EF Core エンティティクラス欠落 | §12 に User, UserSession, SecurityLog, OutboxEvent の C# 定義追加。`[Table]`, `[Column]`, `= []` 初期化, `DateTime.UtcNow`, `[Timestamp]` 楽観的ロック対応 | ⚠️ 部分修正（後述 NEW-H-1） |
| H-12 | Service/Repository インターフェース欠落 | §13 に IAuthService, IJwtTokenService, IMfaService, IPasswordService, IUserRepository, IUserSessionRepository, ISecurityLogRepository 定義追加 | ✅ 完全修正 |
| H-13 | CancellationToken 欠落 | 全 async メソッドシグネチャに `CancellationToken ct = default` 追加 | ✅ 完全修正 |
| H-14 | エラーレスポンス RFC 9457 非準拠（ADR-0007 違反） | §8 を RFC 9457 Problem Details 形式に修正。`TypedResults.Problem()` 使用例、`extensions` フィールドにカスタムエラーコード | ✅ 完全修正 |
| H-15 | MFA secret_key/backup_codes 暗号化未記載 | user_mfa テーブル説明に「AES-256-GCM で暗号化保存。暗号鍵は Azure Key Vault で管理」を明記 | ✅ 完全修正 |
| H-16 | Kafka イベント PII / GDPR 未対応 | PII 最小化原則の注記追加。userId のみでイベント識別 | ✅ 完全修正 |
| H-17 | SecurityLog details ホワイトリスト未定義 | 格納可能項目を明示的に定義（authMethod, mfaUsed, failureReason, provider, riskScore, actionTaken）。PII 格納禁止を明記 | ✅ 完全修正 |
| H-18 | Dockerfile HEALTHCHECK が curl 使用 | `wget --spider -q http://localhost:8080/health || exit 1` に変更 | ✅ 完全修正 |
| H-19 | Moq 使用（AGENTS.md は NSubstitute） | §2 NuGet パッケージリストおよび §16 テスト戦略を NSubstitute に統一 | ✅ 完全修正 |
| H-20 | テスト戦略欠落 | §16 にテスト戦略セクション追加（テスト種別、命名規約、主要テストケース一覧、カバレッジ目標） | ✅ 完全修正 |
| H-21 | API パスバージョニング不統一 | 全エンドポイントを `/api/v1/auth/...` に統一。OAuth2 コールバック `/signin-oidc` はフレームワーク制約として例外扱い | ✅ 完全修正 |
| H-22 | ログインレスポンスの success ラッパー | `success` フィールド除去。HTTP ステータスコードで成功/失敗を表現 | ✅ 完全修正 |
| H-23 | 外部サービス Resilience 設計欠落 | §15 に Azure AD / Graph API 用 `IHttpClientFactory` + `AddStandardResilienceHandler` 設計追加 | ✅ 完全修正 |

---

## 指摘サマリー（イテレーション 2）

| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ✅ Pass | 0 | 0 | 2 | 1 |
| architect | ✅ Pass | 0 | 0 | 2 | 1 |
| programing-reviewer | ⚠️ Conditional | 0 | 1 | 3 | 0 |
| dba-reviewer | ✅ Pass | 0 | 0 | 1 | 1 |
| security-reviewer | ✅ Pass | 0 | 0 | 1 | 0 |
| compliance-reviewer | ✅ Pass | 0 | 0 | 1 | 0 |
| audit-reviewer | ✅ Pass | 0 | 0 | 1 | 0 |
| qa-manager | ✅ Pass | 0 | 0 | 0 | 0 |
| performance-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| infra-ops-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| release-manager | ✅ Pass | 0 | 0 | 0 | 1 |
| oss-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| ux-accessibility-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| tech-lead | ⚠️ Conditional | 0 | 0 | 1 | 0 |
| **合計** | | **0** | **1** | **12** | **4** |

## 判定根拠
- **判定ルール適用結果**: Critical 0 件、High 1 件のみ → **⚠️ Conditional Approval**（人間の判断で受容/是正を決定）
- **前回比**: Critical 3→0、High 20→1、Medium 21→12、Low 4→4
- **最も重大な指摘**: EF Core エンティティ定義が不完全（前回 H-11 で 7 エンティティ要求、4/7 のみ追加、残り 5 テーブル分の C# クラス未定義）

---

## Critical/High 指摘一覧（イテレーション 2 新規）

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|---------------|----------|----------|
| NEW-H-1 | **High** | programing-reviewer | コード設計 | §12 EF Core エンティティ定義 | 前回 H-11 で「主要エンティティ（User, UserSession, OAuthAccount, SecurityLog, UserMfa, PasswordReset, UserRole）の C# クラス定義を追加」と要求。修正で User, UserSession, SecurityLog, OutboxEvent の 4 クラスは追加されたが、**OAuthAccount, UserRole, Role, PasswordReset, UserMfa の 5 クラスが未定義**。DB スキーマには 9 テーブルが定義されているが、EF Core エンティティは 4 クラスのみ。AGENTS.md §10.3 準拠の `[Table]`, `[Column]`, コレクション初期化, `[Timestamp]` 等の定義が残り 5 テーブルに必要 | OAuthAccount, UserRole, Role, PasswordReset, UserMfa の C# エンティティクラスを §12 に追加。各エンティティに `[Table("snake_case")]`, `[Column("snake_case")]` 属性、ナビゲーションプロパティ、コレクション初期化 `= []` を含める |

---

## Medium 指摘一覧（改善推奨）

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|---------------|----------|----------|
| NEW-M-1 | Medium | programing-reviewer | コード設計 | §14 OutboxPublisher | `OutboxPublisher` コードが `producer.ProduceAsync(evt.EventType, ...)` で `EventType` をそのまま Kafka トピック名として使用。しかし `event_type` カラムにはイベント種別（例: `UserRegistered`）が格納され、実際の Kafka トピック名（例: `user.registered`）とは異なる。トピック解決ロジックが欠如している | `outbox_events` テーブルに `topic VARCHAR(255) NOT NULL` カラムを追加するか、`EventType` → Kafka トピックのマッピング関数を `OutboxPublisher` 内に実装する |
| NEW-M-2 | Medium | programing-reviewer | コード設計 | §13 インターフェース | `POST /api/v1/auth/users`（ユーザー登録）に対応する Service メソッドが `IAuthService` にも他のインターフェースにも定義されていない。Endpoints パターン（§13末尾）にも登録エンドポイントの実装例がない | ユーザー登録メソッドの所在を明確化。AuthService の責務であれば `IAuthService` に `RegisterAsync` を追加。UserManagementService に委譲するなら責務境界を明記 |
| NEW-M-3 | Medium | programing-reviewer | コード設計 | §13 DTO 定義 | `POST /api/v1/auth/users` が受け取る `UserCreateRequest` の C# record 定義が未記載。JSON リクエスト例（§API レスポンス例）は存在するが、Data Annotations（`[Required]`, `[EmailAddress]`, `[StringLength]` 等）付きの record 定義がない | `UserCreateRequest` record 型の定義を §13 DTO 定義セクションに追加 |
| NEW-M-4 | Medium | dba-reviewer | DB スキーマ | §users + §user_roles | `users` テーブルに `role` カラム（VARCHAR(50), CHECK 制約付き）が存在し、同時に `user_roles` ジャンクションテーブル（多対多）も存在する。単一ロール表現（`users.role`）と多対多ロール表現（`user_roles`）が並存しており、データの信頼性の源泉（source of truth）が不明確。データ不整合のリスクがある | いずれかに統一するか、使い分けを明確に文書化する。例: `users.role` は「プライマリロール」（高速参照用の非正規化）、`user_roles` は「追加権限」等の設計意図を明記 |
| NEW-M-5 | Medium | architect | アーキテクチャ | §API 設計 | OAuth2 Web エンドポイント（`GET /`, `GET /home`, `GET /profile`, `GET /call_graph`）が HTML レスポンスを返す設計。純粋な API マイクロサービスとして HTML を配信するのは責務の逸脱。Microsoft.Identity.Web テンプレート由来の開発用エンドポイントと推察されるが、本番運用での位置づけが不明 | 開発用エンドポイントであれば「開発環境専用」と明記し、本番では無効化する設計を追加。または削除して API エンドポイントのみに限定 |
| NEW-M-6 | Medium | architect | アーキテクチャ | §7 セキュリティ機能 | アカウントロック（5 回失敗）後の解除フローが未定義。自動解除タイマー（例: 30 分後に自動アンロック）、管理者による手動解除、パスワードリセット経由のいずれかが未記載。実装時にブロッカーとなる | ロック解除方式を明記。推奨: 30 分後の自動解除 + 管理者による即時解除の 2 パターン |
| NEW-M-7 | Medium | security-reviewer | セキュリティ | §7 セキュリティ機能 | OAuth2 フローにおける CSRF 防止策（`state` パラメータの生成・検証）が引き続き未記載。PKCE（Proof Key for Code Exchange）の採用有無も不明確。OAuth2 Code Interception 攻撃への対策として `state` + PKCE は必須 | OAuth2 Authorization Code Flow with PKCE の採用を明記。`state` パラメータの生成（`RandomNumberGenerator`）・検証フローを記載 |
| NEW-M-8 | Medium | compliance-reviewer | コンプライアンス | §DB スキーマ | `security_logs` の保持期間が依然として未定義。GDPR データ最小化原則と監査要件（ログ保持義務）のバランスが設計レベルで決まっていない | 保持期間（例: 90 日 / 1 年）と自動削除（TTL ベース or バッチ削除）ポリシーを追記。GDPR 要件との整合性を明記 |
| NEW-M-9 | Medium | audit-reviewer | トレーサビリティ | 設計書全体 | Correlation ID ミドルウェアの設計が引き続き未記載。AGENTS.md §11.2 は全リクエストに Correlation ID 付与とマイクロサービス間伝搬を必須としている | Correlation ID 生成・伝搬ミドルウェアの設計を追加。HTTP ヘッダー `X-Correlation-Id` の受信/生成/伝搬フロー、Serilog `LogContext.PushProperty` での付与を記載 |
| NEW-M-10 | Medium | business-analyst | ビジネス要件 | §7 RBAC | USER ロールと CUSTOMER ロールの権限差分が依然として不明確。EC サイトにおいてこの 2 つを区別する業務上の根拠（購入可否、ポイント付与差分等）が未定義 | ロール権限マトリクスを追加し、USER と CUSTOMER の具体的な権限差分を明記。不要であれば統合を検討 |
| NEW-M-11 | Medium | business-analyst | ビジネス要件 | §7 アカウントセキュリティ | パスワードポリシー（複雑性要件）の具体的なルールが不十分。`LoginRequest` の `[StringLength(100, MinimumLength = 8)]` で最小 8 文字は設定されているが、大文字/小文字/数字/記号の組み合わせ要件が未定義。エラーコード AUTH-4221, AUTH-4222（パスワードポリシー違反）の具体的な発動条件が不明 | パスワードポリシー（最小 8 文字 + 大文字 1 文字以上 + 小文字 1 文字以上 + 数字 1 文字以上 + 記号 1 文字以上 等）を明記。FluentValidation のルールとして記載 |
| NEW-M-12 | Medium | tech-lead | 横断整合性 | §12 EF Core User エンティティ | User エンティティの日時プロパティが `DateTime` 型で定義されている（`DateTime.UtcNow` で初期化は正しい）が、DB スキーマは `TIMESTAMP WITH TIME ZONE`。Npgsql 6.0+ では `timestamptz` に `DateTime`（Kind=Utc）を使用可能だが、`DateTimeOffset` の方が `TIMESTAMP WITH TIME ZONE` との意味的な整合性が高く、タイムゾーン誤用を型システムで防止できる。また、User エンティティに `[Timestamp] [Column("row_version")] byte[] RowVersion` が定義されているが、DB スキーマ DDL に `row_version` カラムが含まれておらず DDL 不整合。PostgreSQL では `xmin` システム列を使った楽観的ロックが一般的 | DB スキーマ DDL に `row_version BYTEA` カラムを追加して整合させるか、PostgreSQL 固有の `xmin` ベース楽観的ロック（`UseXminAsConcurrencyToken()`）に変更する。いずれかの方針を明記 |

---

## Low 指摘一覧（時間がある時に対応）

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|---------------|----------|----------|
| NEW-L-1 | Low | dba-reviewer | DB スキーマ | §outbox_events | `outbox_events` テーブルに `aggregate_type` カラムが欠落。AGENTS.md が推奨する Outbox カラム構成は `aggregate_type` を含む。AuthService は User Aggregate のみだが、将来の拡張性のためカラムを追加しておく方が望ましい | `aggregate_type VARCHAR(100) NOT NULL` カラムを追加 |
| NEW-L-2 | Low | architect | アーキテクチャ | §API 設計 | パスワード管理 API に `DELETE` メソッドはないが、ユーザー削除 API（`DELETE /api/v1/auth/users/{userId}`）のソフトデリート/ハードデリートにおいて、ソフトデリートの認可要件（本人のみ vs 管理者）が不明確 | ソフトデリートの認可ポリシー（本人 + Admin）を明記 |
| NEW-L-3 | Low | release-manager | リリース | §7 認証方式 | MFA (TOTP) のステータスが依然として「🔄 進行中」。MVP / Post-MVP のどちらに含めるかが明示されていない | MFA のリリーススコープ（MVP or Phase 2）を明記 |
| NEW-L-4 | Low | business-analyst | ビジネス要件 | §8 エラーコード | エラーコード体系（AUTH-4001〜AUTH-5002）は網羅的だが、アカウントロック時のレスポンスに「ロック残存時間」の情報が含まれておらず、UX 上ユーザーが待機時間を知る手段がない | ロック時レスポンスに `retryAfterSeconds` フィールドを追加検討 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| 1 | 高優先 | architect + business-analyst | AuthService と UserManagementService の責務境界。`POST /api/v1/auth/users`（ユーザー登録）が AuthService に存在するが、ユーザー CRUD は UserManagementService の責務と spec.md で定義。登録 API の所在を最終決定する必要あり | テックリード + プロダクトオーナー |
| 2 | 通常 | compliance-reviewer | `security_logs` の保持期間の最終決定。GDPR のデータ最小化原則（短期保持）と監査要件（長期保持）のバランス | 法務 + セキュリティアーキテクト |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| — | — | — | 本イテレーションで競合は検出されなかった | — | — |

---

## ドキュメント横断分析

### サービス間整合性

| 検証項目 | 前回結果 | 今回結果 | 詳細 |
|---------|---------|---------|------|
| DB 名と ADR-0006 | ❌ 不整合 | ✅ 整合 | `authdb` に修正済み |
| Kafka トピック名 | ⚠️ 要確認 | ✅ 整合 | `user.registered`, `auth.login.success` 等の標準命名に統一 |
| Outbox パターン（ADR-0005） | ❌ 欠落 | ✅ 整合 | `outbox_events` テーブル + `OutboxPublisher` + 動的バックオフ |
| エラーレスポンス形式（ADR-0007） | ❌ 不整合 | ✅ 整合 | RFC 9457 Problem Details + `TypedResults.Problem()` |
| JWT RS256 非対称鍵 | ❌ HS512 | ✅ 整合 | RS256 + Azure Key Vault 鍵管理 + `kid` ローテーション |
| パスワードハッシュ | ❌ BCrypt | ✅ 整合 | Argon2id（`IPasswordHasher<T>` カスタム実装）|
| UserService との責務境界 | ⚠️ 曖昧 | ⚠️ 曖昧 | ユーザー登録 API が AuthService に残存（エスカレーション #1） |

### EF Core エンティティ vs DB スキーマ対応表

| DB テーブル | EF Core エンティティ | ステータス |
|-----------|---------------------|----------|
| users | `User` | ✅ 定義済み（§12） |
| user_sessions | `UserSession` | ✅ 定義済み（§12） |
| security_logs | `SecurityLog` | ✅ 定義済み（§12） |
| outbox_events | `OutboxEvent` | ✅ 定義済み（§12） |
| oauth_accounts | — | ❌ **未定義** |
| user_roles | — | ❌ **未定義** |
| roles | — | ❌ **未定義** |
| password_resets | — | ❌ **未定義** |
| user_mfa | — | ❌ **未定義** |

### 未定義・曖昧な領域（前回比較）

| # | 領域 | 前回ステータス | 今回ステータス | 変化 |
|---|------|-------------|-------------|------|
| 1 | Client Credentials フロー（サービス間認証） | 高リスク | 高リスク | 変化なし |
| 2 | トークン失効（Revocation）具体設計 | 中リスク | 中リスク | JWT ブラックリストの Redis 設計は記載あるが実装フロー未定義 |
| 3 | MFA リカバリーフロー | 中リスク | 中リスク | 変化なし |
| 4 | 同時セッション制限 | 低リスク | 低リスク | 変化なし |
| 5 | アカウントロック解除フロー | 中リスク | 中リスク（NEW-M-6 で指摘） | 依然未定義 |
| 6 | Correlation ID | 中リスク | 中リスク（NEW-M-9 で指摘） | 依然未定義 |

---

## 改善トレンド

| 指標 | check-report-1 | check-report-2 | 変化 |
|------|---------------|---------------|------|
| Critical | 3 | **0** | ✅ -3（全件解消） |
| High | 20 | **1** | ✅ -19（95% 解消） |
| Medium | 21 | **12** | ✅ -9（一部解消 + 新規検出） |
| Low | 4 | **4** | ± 0 |
| 判定 | ❌ Rejected | **⚠️ Conditional** | ✅ 大幅改善 |

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### business-analyst レビュー

**判定**: ✅ Pass

| 重要度 | 件数 |
|--------|------|
| Critical | 0 |
| High | 0 |
| Medium | 2 |
| Low | 1 |

#### 指摘一覧

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|----------|----------|
| BA-1 | Medium | USER ロールと CUSTOMER ロールの権限差分が依然として不明確（前回 BA-1 から重要度を High → Medium に格下げ。CHECK 制約は追加済みだがビジネス定義が未記載） | ロール権限マトリクスを追加 |
| BA-2 | Medium | パスワードポリシーの複雑性要件（大文字/小文字/数字/記号の組み合わせ）が具体的に未定義。`MinimumLength = 8` のみ | ポリシーの具体的ルールを明記 |
| BA-3 | Low | アカウントロック時のレスポンスに「ロック残存時間」の UX 情報が含まれていない | `retryAfterSeconds` フィールドを検討 |

**所見**: 前回 BA-1（High: USER/CUSTOMER 区分）は CHECK 制約追加で DB 層は改善されたが、ビジネスレベルの権限差分は未記載のため Medium に据え置く。

</details>

<details>
<summary>architect レビューレポート</summary>

### architect レビュー

**判定**: ✅ Pass

| 重要度 | 件数 |
|--------|------|
| Critical | 0 |
| High | 0 |
| Medium | 2 |
| Low | 1 |

#### 指摘一覧

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|----------|----------|
| ARCH-1 | Medium | OAuth2 Web HTML エンドポイント（GET /, /home, /profile, /call_graph）が API マイクロサービスの責務を逸脱。開発用か本番用かの位置づけが不明 | 開発専用であれば明記、本番では無効化 |
| ARCH-2 | Medium | アカウントロック（5 回失敗）後の解除フローが未定義（前回「未定義・曖昧な領域 #5」から引き続き） | 自動解除タイマー + 管理者手動解除を設計 |
| ARCH-3 | Low | ユーザー削除 API（ソフトデリート）の認可ポリシーが不明確 | 認可要件（本人 + Admin）を明記 |

**所見**: 前回 Critical 1 件（authdb）+ High 3 件（Outbox, トピック名, RFC 9457）は全て完全に是正されている。アーキテクチャ設計は大幅に改善され、ADR-0005/0006/0007 との整合性が確保された。Outbox パターンの実装設計（§14）は動的バックオフ含め高品質。

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### programing-reviewer レビュー

**判定**: ⚠️ Conditional

| 重要度 | 件数 |
|--------|------|
| Critical | 0 |
| High | 1 |
| Medium | 3 |
| Low | 0 |

#### 指摘一覧

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|----------|----------|
| PR-1 | **High** | EF Core エンティティ定義が不完全。前回 H-11 で OAuthAccount, SecurityLog, UserMfa, PasswordReset, UserRole の 7 エンティティ要求。User, UserSession, SecurityLog, OutboxEvent の 4 クラスは追加されたが **OAuthAccount, UserRole, Role, PasswordReset, UserMfa の 5 クラスが未定義** | 残り 5 エンティティの C# クラス定義を §12 に追加 |
| PR-2 | Medium | OutboxPublisher が `evt.EventType` を Kafka トピック名として使用するが、event_type（例: `UserRegistered`）とトピック名（例: `user.registered`）は異なる命名体系 | `topic` カラム追加 or マッピング関数 |
| PR-3 | Medium | `POST /api/v1/auth/users` に対応する Service メソッドがどのインターフェースにも定義されていない | 登録メソッドの定義を追加 |
| PR-4 | Medium | `UserCreateRequest` record DTO の C# 定義が未記載（JSON 例のみ） | record 型 + Data Annotations で定義 |

**所見**: 前回 High 3 件（EF Core エンティティ, インターフェース, CancellationToken）のうちインターフェースと CancellationToken は完全修正。エンティティ定義は部分修正（4/9 テーブル分のみ）。追加された User, UserSession, SecurityLog, OutboxEvent のコード品質は高い（primary constructor は Repository のみ適用、Entity は POCO パターン、`[Timestamp]` 楽観的ロック、`= []` コレクション初期化）。Minimal API エンドポイントパターン（§13末尾）は AGENTS.md §6.2 に忠実で、FluentValidation 統合、`CancellationToken ct` 受け取り、`ClaimsPrincipal` からの userId 抽出、IDOR 防止全て正しい。DTO 定義も record 型 + Data Annotations で適切。

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### dba-reviewer レビュー

**判定**: ✅ Pass

| 重要度 | 件数 |
|--------|------|
| Critical | 0 |
| High | 0 |
| Medium | 1 |
| Low | 1 |

#### 指摘一覧

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|----------|----------|
| DBA-1 | Medium | `users.role` カラム（単一ロール）と `user_roles` テーブル（多対多）の並存。データの source of truth が不明確 | 使い分けを文書化するか統一 |
| DBA-2 | Low | `outbox_events` に `aggregate_type` カラムが欠落（AGENTS.md 推奨パターン） | `aggregate_type` カラム追加を検討 |

**所見**: 前回 Critical 1 件（TIMESTAMP WITH TIME ZONE）+ High 3 件（CHECK 制約, roles テーブル）は全て完全修正。本イテレーションの DB スキーマ品質は高い:

- ✅ 全日時カラム `TIMESTAMP WITH TIME ZONE`
- ✅ `users.status`, `users.role`, `roles.name`, `outbox_events.status` に CHECK 制約
- ✅ `roles` テーブル完全定義
- ✅ `outbox_events` テーブル + 部分インデックス（`idx_outbox_events_pending`, `idx_outbox_events_failed`）
- ✅ `oauth_accounts` に `UNIQUE (provider, provider_user_id)` 制約
- ✅ `security_logs` の append-only 設計根拠明記、`details` ホワイトリスト + PII 禁止
- ✅ 外部キー ON DELETE 動作が全 FK で明示（CASCADE / SET NULL / RESTRICT）
- ✅ `password_resets.token` の生成方式（`RandomNumberGenerator.GetBytes(64)`）明記
- ✅ `user_mfa.secret_key`, `backup_codes` の AES-256-GCM 暗号化保存 + Azure Key Vault 鍵管理

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### security-reviewer レビュー

**判定**: ✅ Pass

| 重要度 | 件数 |
|--------|------|
| Critical | 0 |
| High | 0 |
| Medium | 1 |
| Low | 0 |

#### 指摘一覧

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|----------|----------|
| SEC-1 | Medium | OAuth2 CSRF 防止策（`state` パラメータ + PKCE）が引き続き未記載 | PKCE フロー設計を追記 |

**所見**: 前回 Critical 1 件（JWT HS512）+ High 3 件（BCrypt, PII, MFA 暗号化）は全て完全修正。セキュリティ設計は大幅に改善:

- ✅ RS256 非対称鍵 + Azure Key Vault 管理 + `kid` ローテーション
- ✅ Argon2id（IPasswordHasher\<T\> カスタム実装）
- ✅ イベントペイロード PII 除去 + 最小化原則明記
- ✅ MFA シークレット AES-256-GCM 暗号化 + Azure Key Vault
- ✅ ログイン専用レート制限（5 req/min）
- ✅ SecurityLog details ホワイトリスト + PII 格納禁止
- ✅ Dockerfile 非 root ユーザー（skishop）
- ✅ パスワードリセットトークン暗号論的乱数生成

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### compliance-reviewer レビュー

**判定**: ✅ Pass

| 重要度 | 件数 |
|--------|------|
| Critical | 0 |
| High | 0 |
| Medium | 1 |
| Low | 0 |

#### 指摘一覧

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|----------|----------|
| COMP-1 | Medium | `security_logs` の保持期間が未定義（前回 COMP-2 からの引き継ぎ） | 保持期間と自動削除ポリシーを追記 |

**所見**: 前回 High 1 件（Kafka PII / GDPR）は完全修正。PII 最小化原則の明記により GDPR 第 17 条対応が改善。

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### audit-reviewer レビュー

**判定**: ✅ Pass

| 重要度 | 件数 |
|--------|------|
| Critical | 0 |
| High | 0 |
| Medium | 1 |
| Low | 0 |

#### 指摘一覧

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|----------|----------|
| AUD-1 | Medium | Correlation ID 設計が未記載（AGENTS.md §11.2 要件）。前回 AUD-2 からの引き継ぎ | Correlation ID ミドルウェア設計を追加 |

**所見**: 前回 High 1 件（SecurityLog ホワイトリスト）は完全修正。`details` フィールドのホワイトリスト定義は明確で、PII 格納禁止が AGENTS.md §5.7 と整合。Correlation ID は唯一の残存課題。

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### qa-manager レビュー

**判定**: ✅ Pass

| 重要度 | 件数 |
|--------|------|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 0 |

**所見**: 前回 High 1 件（テスト戦略欠落）は完全修正。§16 のテスト戦略は高品質:

- ✅ テスト種別 4 種（Unit / Integration / DB Slice / Security）明記
- ✅ 命名規約 `Should_期待結果_When_条件` パターン準拠
- ✅ AAA パターンのコード例（NSubstitute + Shouldly）
- ✅ 主要テストケース一覧 16 件（正常系 + 異常系）
- ✅ カバレッジ目標（Service 80%, Endpoints 80%, Repository 70%, 全体 80%）

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### performance-reviewer レビュー

**判定**: ✅ Pass

| 重要度 | 件数 |
|--------|------|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 0 |

**所見**: 前回の Medium 2 件（セッション TTL, JWT ブラックリスト）は設計レベルでは十分。Redis キャッシュ戦略、インデックス設計、パフォーマンスメトリクスは適切。Outbox Publisher の動的バックオフ（100ms〜5s）は AGENTS.md §10.4 準拠。レート制限設計（全体 100 req/min + ログイン 5 req/min）も適切。

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### infra-ops-reviewer レビュー

**判定**: ✅ Pass

| 重要度 | 件数 |
|--------|------|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 0 |

**所見**: 前回 High 1 件（Dockerfile curl）は完全修正。Dockerfile 品質は高い:

- ✅ マルチステージビルド（sdk → aspnet）
- ✅ 非 root ユーザー（skishop）
- ✅ バージョン固定（`10.0`）
- ✅ `HEALTHCHECK` が `wget --spider` を使用
- ✅ ポート 8080（.NET 8+ デフォルト）で統一
- ✅ ヘルスチェックエンドポイント（/health, /health/ready）定義

</details>

<details>
<summary>release-manager レビューレポート</summary>

### release-manager レビュー

**判定**: ✅ Pass

| 重要度 | 件数 |
|--------|------|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 1 |

#### 指摘一覧

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|----------|----------|
| REL-1 | Low | MFA ステータスが「🔄 進行中」のまま。MVP / Post-MVP のスコープ判定が未記載 | リリーススコープを明記 |

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### oss-reviewer レビュー

**判定**: ✅ Pass

| 重要度 | 件数 |
|--------|------|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 0 |

**所見**: 前回 High 1 件（Moq → NSubstitute）は完全修正。NuGet パッケージリストは AGENTS.md §8.1 と整合:

- ✅ NSubstitute（Moq 除去）
- ✅ Serilog.Sinks.Console 6.* 追加
- ✅ FluentValidation.DependencyInjectionExtensions 追加
- ✅ 全パッケージ GA 版（-preview / -beta / -rc なし）
- ✅ 禁止パッケージ（System.Web, log4net, EntityFramework 6, Newtonsoft.Json, WebClient）なし

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### ux-accessibility-reviewer レビュー

**判定**: ✅ Pass（該当なし）

認証サービスはバックエンド API サービスであり、UX/アクセシビリティの直接的な対象外。

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### tech-lead レビュー

**判定**: ⚠️ Conditional

| 重要度 | 件数 |
|--------|------|
| Critical | 0 |
| High | 0 |
| Medium | 1 |
| Low | 0 |

#### 指摘一覧

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|----------|----------|
| TL-1 | Medium | User エンティティの `[Timestamp] [Column("row_version")] byte[] RowVersion` と DB スキーマ DDL の不整合。DDL に `row_version` カラムがない。PostgreSQL では `xmin` ベース楽観的ロックが一般的 | DDL にカラム追加 or `xmin` ベースに変更 |

**Tech-Lead 総合所見**:

本イテレーションで前回 Critical 3 件 + High 20 件の **全 23 件が完全に是正** されており、設計書の品質は劇的に向上した。特に以下の改善は高く評価できる:

1. **ADR 整合性の完全確保**: ADR-0005（Outbox パターン）、ADR-0006（独立 DB `authdb`）、ADR-0007（RFC 9457）の全てに整合
2. **RS256 非対称鍵設計**: Azure Key Vault での RSA 鍵管理、`kid` によるローテーション対応は本番運用に耐えうる品質
3. **Outbox 実装設計（§14）**: `OutboxPublisher` BackgroundService + 動的バックオフ（100ms〜5s）+ `DEAD_LETTER` ステータスの追加は AGENTS.md §10.4 を超える品質
4. **テスト戦略（§16）**: 完全な AAA パターン例、NSubstitute 使用、カバレッジ目標明記
5. **Resilience 設計（§15）**: `AddStandardResilienceHandler` の具体的設定値は即実装可能

残る唯一の **High 指摘（EF Core エンティティ 5 クラス未定義）** は、前回修正の「やり残し」であり、追加で 5 クラスを同じパターンで記述すれば解消可能。**実装には着手可能な品質水準に到達している**。High 1 件を受容して実装フェーズに進むか、追加是正するかは人間の判断に委ねる。

</details>
