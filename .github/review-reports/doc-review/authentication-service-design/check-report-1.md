# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/authentication-service-design.md`
- **判定**: ❌ **Rejected** — Critical 指摘 3 件検出。是正完了後に再レビュー必須
- **レビュー日時**: 2026-04-03
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

## 技術スタック検証結果

| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| ランタイム | .NET 10 (LTS) | .NET 10 (LTS) | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| ORM | EF Core 10 | EF Core 10 | ✅ |
| DB | PostgreSQL (skishopdb) | PostgreSQL（サービス別独立 DB: `authdb`） | ❌ ADR-0006 違反 |
| キャッシュ | Redis (StackExchange.Redis 2.*) | Redis (StackExchange.Redis 2.*) | ✅ |
| メッセージング | Confluent.Kafka 2.* | Confluent.Kafka 2.* | ✅ |
| 認証 | ASP.NET Core Identity + Microsoft.Identity.Web 3.* | 同左 | ✅ |
| オーケストレーション | .NET Aspire 13.1 | .NET Aspire 13.1 | ✅ |
| テストモック | **Moq** | **NSubstitute 5.*** | ❌ 不一致 |
| ログ | Serilog.AspNetCore 8.* + Serilog.Formatting.Compact 3.* | 同左 + **Serilog.Sinks.Console 6.*** | ⚠️ Sinks.Console 欠落 |
| パスワードハッシュ | **BCrypt (コスト係数 12)** | ASP.NET Core Identity デフォルト (**PBKDF2**) / Argon2 | ❌ 不一致 |
| JWT アルゴリズム | **HS512**（共有鍵） | AGENTS.md §5.3 — `ValidateIssuerSigningKey` 構成（RS256 前提パターン） | ⚠️ 要検討 |
| Docker バージョン | "最新" | Docker **25.x** 固定 | ⚠️ バージョン未固定 |

---

## 指摘サマリー

| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ⚠️ Conditional | 0 | 1 | 2 | 0 |
| architect | ❌ Rejected | 1 | 3 | 2 | 0 |
| programing-reviewer | ⚠️ Conditional | 0 | 3 | 2 | 1 |
| dba-reviewer | ❌ Rejected | 1 | 3 | 3 | 1 |
| security-reviewer | ❌ Rejected | 1 | 3 | 2 | 0 |
| compliance-reviewer | ⚠️ Conditional | 0 | 1 | 2 | 0 |
| audit-reviewer | ⚠️ Conditional | 0 | 1 | 1 | 0 |
| qa-manager | ⚠️ Conditional | 0 | 1 | 1 | 0 |
| performance-reviewer | ✅ Pass | 0 | 0 | 2 | 1 |
| infra-ops-reviewer | ⚠️ Conditional | 0 | 1 | 1 | 1 |
| release-manager | ✅ Pass | 0 | 0 | 1 | 0 |
| oss-reviewer | ⚠️ Conditional | 0 | 1 | 1 | 0 |
| ux-accessibility-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| tech-lead | ❌ Rejected | 0 | 2 | 1 | 0 |
| **合計** | | **3** | **20** | **21** | **4** |

## 判定根拠
- **判定ルール適用結果**: Critical 指摘 3 件検出 → 自動 `❌ Rejected`
- **最も重大な指摘**:
  1. DB スキーマの全日時カラムが `TIMESTAMP` であり `TIMESTAMP WITH TIME ZONE` ではない（sql-schema-review.instructions.md §2 違反）
  2. ADR-0006（サービス別独立 DB）違反 — 設計書は `skishopdb`（共有 DB）を記載
  3. JWT アルゴリズム HS512 はマイクロサービス構成で全サービスに秘密鍵を共有する必要があり、セキュリティリスクが高い

---

## Critical/High 指摘一覧（修正必須）

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|---------------|----------|----------|
| 1 | **Critical** | dba-reviewer | DB スキーマ | §DB スキーマ全テーブル | 全テーブルの日時カラム（`created_at`, `updated_at`, `locked_at`, `last_login`, `expires_at`, `verified_at`）が `TIMESTAMP` と記載。`sql-schema-review.instructions.md` §2 は `TIMESTAMP WITH TIME ZONE` を必須としている。タイムゾーン非対応では UTC 変換ミス・サマータイム問題が発生する | 全日時カラムを `TIMESTAMP WITH TIME ZONE` に修正 |
| 2 | **Critical** | architect | アーキテクチャ | §サービス情報 | DB 名が `skishopdb`（共有 DB）と記載。ADR-0006「サービス別独立 DB」により AuthService は専用の論理 DB `authdb` を使用すべき。共有 DB は独立デプロイ・障害分離を阻害する | DB 名を `authdb` に修正。.NET Aspire の `AddDatabase("authdb")` で分離 |
| 3 | **Critical** | security-reviewer | セキュリティ | §7 セキュリティ機能 | JWT アルゴリズムが HS512（対称鍵）。マイクロサービス構成では全サービス（API Gateway 含む）に同一の秘密鍵を配布する必要があり、鍵漏洩リスクが増大。AGENTS.md §5.3 の `ValidateIssuerSigningKey` + `ValidateIssuer` + `ValidateAudience` は RS256（非対称鍵）パターンと整合する | RS256（非対称鍵）に変更し、AuthService のみが秘密鍵を保持、他サービスは公開鍵で検証する設計に修正。ログインレスポンス例の `"eyJhbGciOiJIUzUxMiJ9..."` も `RS256` に修正 |
| 4 | **High** | architect | イベント設計 | §イベント設計 | `UserRegistered` 等のイベントを Kafka に発行するが、`outbox_events` テーブルが DB スキーマに存在しない。ADR-0005 は Outbox パターンを必須としており、DB トランザクションとイベント発行の原子性が保証されない | `outbox_events` テーブルを DB スキーマに追加。カラム: `id`, `aggregate_type`, `aggregate_id`, `event_type`, `payload`, `status` (PENDING/PUBLISHED/FAILED), `created_at`, `published_at`。`OutboxPublisher` (BackgroundService) の設計も追記 |
| 5 | **High** | architect | イベント設計 | §イベント設計 | Kafka トピック名が `auth.users`, `auth.sessions`, `auth.security` と記載。spec.md のイベント設計パターンとの整合性が不明。`user.registered` のような `{aggregate}.{event}` 形式との不一致の可能性がある | トピック命名規則を spec.md と統一。`user.registered`, `auth.login.success`, `auth.login.failed` 等の標準命名を採用 |
| 6 | **High** | security-reviewer | セキュリティ | §7 セキュリティ機能 | パスワードハッシュに BCrypt（コスト係数 12）を使用。AGENTS.md §5.4 は ASP.NET Core Identity のデフォルト（PBKDF2）を推奨し、カスタムの場合は Argon2 を例示。BCrypt は AGENTS.md に記載がなく、選択理由が不明 | AGENTS.md に基づき ASP.NET Core Identity デフォルト（PBKDF2）を採用するか、Argon2 を選択する場合はその根拠を ADR として記録。BCrypt を選択する場合も同様に根拠を明記 |
| 7 | **High** | security-reviewer | セキュリティ | §イベントスキーマ例 | `UserAuthenticated` イベントのペイロードに `"email": "user@example.com"` が含まれている。AGENTS.md §5.7「PII ログ禁止」によりメールアドレスのログ出力は禁止。Kafka イベントはログ基盤や分析サービスで消費されるため PII 漏洩リスクがある | イベントペイロードから `email` フィールドを除去し、`userId` のみで識別。必要な場合はイベント消費者が UserManagementService API 経由で取得する設計に変更 |
| 8 | **High** | dba-reviewer | DB スキーマ | §users テーブル | `status` カラム（VARCHAR(50)）に CHECK 制約がない。設計書に記載の値 `PENDING_VERIFICATION`, `ACTIVE`, `SUSPENDED` を DB 層で保証する CHECK 制約が必要。`sql-schema-review.instructions.md` §3「制約は DB 層で必ず設定する」違反 | `CONSTRAINT ck_users_status CHECK (status IN ('PENDING_VERIFICATION', 'ACTIVE', 'SUSPENDED'))` を追加 |
| 9 | **High** | dba-reviewer | DB スキーマ | §users テーブル | `role` カラム（VARCHAR(50)）に CHECK 制約がない。設計書 §7 のロール一覧（ADMIN, MANAGER, STAFF, EMPLOYEE, USER, CUSTOMER）を DB 層で保証すべき | `CONSTRAINT ck_users_role CHECK (role IN ('ADMIN', 'MANAGER', 'STAFF', 'EMPLOYEE', 'USER', 'CUSTOMER'))` を追加 |
| 10 | **High** | dba-reviewer | DB スキーマ | §ER 図 + テーブル定義 | `roles` テーブルの定義が欠落。ER 図に `Role` エンティティが存在し、`user_roles.role_id` が FK 参照しているが、参照先テーブルの DDL がない | `roles` テーブル（`id UUID PK`, `name VARCHAR(50) NOT NULL UNIQUE`, `description TEXT`, `created_at`, `updated_at`）を追加定義 |
| 11 | **High** | programing-reviewer | コード設計 | 設計書全体 | EF Core エンティティクラスの C# 定義が存在しない。AGENTS.md §10.3 は `[Table("snake_case")]`, `[Column("snake_case")]` 属性、`= []` コレクション初期化、`DateTime.UtcNow` 使用、`[Timestamp]` 楽観的ロックを必須としている | 主要エンティティ（User, UserSession, OAuthAccount, SecurityLog, UserMfa, PasswordReset, UserRole）の C# クラス定義を追加 |
| 12 | **High** | programing-reviewer | コード設計 | 設計書全体 | Service / Repository のインターフェース定義（`IAuthService`, `IUserRepository` 等）が記載されていない。AGENTS.md §10.1 の Service・Repository 実装パターンに従った設計が不明 | `IAuthService`, `IJwtTokenService`, `IUserRepository`, `ISecurityLogRepository` 等の主要インターフェースとメソッドシグネチャ（`CancellationToken ct = default` 含む）を定義 |
| 13 | **High** | programing-reviewer | コード設計 | §API 設計 | 全 API エンドポイントの定義に `CancellationToken` の記述がない。AGENTS.md §4.5 は全 async メソッドで `CancellationToken ct = default` を必須としている | Minimal API エンドポイント例（`ProductEndpoints` パターン準拠）に `CancellationToken ct` パラメータを含む実装例を追加 |
| 14 | **High** | architect | エラー設計 | §8 エラーハンドリング | エラーレスポンス形式が独自 JSON（`{ "success": false, "error": { "code": "AUTH-4001", ... } }`）。ADR-0007 および `api-design.instructions.md` §2 は RFC 9457 Problem Details 形式（`TypedResults.Problem()` 使用）を必須としている | RFC 9457 準拠の Problem Details 形式に統一。カスタムエラーコード（AUTH-4001 等）は `extensions` フィールドに含める。`TypedResults.Problem()` の使用例を記載 |
| 15 | **High** | security-reviewer | セキュリティ | §user_mfa テーブル | `secret_key`（TOTP シークレット）と `backup_codes`（バックアップコード）が VARCHAR/TEXT で平文保存の可能性。暗号化の記載がない。MFA シークレットは AES-256 等で暗号化保存が必須 | `secret_key` および `backup_codes` の暗号化保存（AES-256-GCM 等）を明記。暗号鍵は Azure Key Vault で管理 |
| 16 | **High** | compliance-reviewer | コンプライアンス | §イベント設計 | `UserRegistered` イベントのペイロードに「メール」「登録データ」が含まれる。個人情報を Kafka イベントに含める場合の GDPR / 個人情報保護法への対応（保持期間、削除要件）が未定義 | イベントペイロードの PII を最小化（userId のみ）。PII を含める必要がある場合は保持期間・削除手順・暗号化要件を明記 |
| 17 | **High** | audit-reviewer | トレーサビリティ | §SecurityLog | SecurityLog の `details` JSONB カラムに格納される情報の範囲が未定義。AGENTS.md §5.7 で禁止されている PII（Email, Address 等）が混入するリスクがある | `details` フィールドに格納可能な項目をホワイトリストで定義（例: `authMethod`, `mfaUsed`, `failureReason`）。PII 格納を明示的に禁止する制約を追記 |
| 18 | **High** | infra-ops-reviewer | Dockerfile | §11 Dockerfile | Dockerfile の `HEALTHCHECK` が `curl` を使用しているが、ASP.NET Core ランタイムイメージには `curl` がインストールされていない。ヘルスチェックが常に失敗する | `wget` を使用するか、`.NET` の `HealthCheckPublisher` を使用。Chiseled イメージではシェルがないため、Docker の `HEALTHCHECK` ではなく Azure Container Apps のヘルスプローブ設定を使用 |
| 19 | **High** | oss-reviewer | 依存関係 | §2 NuGet パッケージ | テストフレームワークに `Moq` を記載。AGENTS.md §9 は `NSubstitute 5.*` を指定。Moq は AGENTS.md の必須パッケージリストに含まれていない | `Moq` → `NSubstitute 5.*` に変更 |
| 20 | **High** | qa-manager | テスト | 設計書全体 | テスト戦略の記載が完全に欠落。AGENTS.md §9 は Service 80%+、Endpoints 80%+、Repository 70%+ のカバレッジ目標を定義。テストメソッド命名（`Should_期待結果_When_条件`）、AAA パターン、テスト種別の記載がない | テスト戦略セクションを追加。対象テスト種別（Unit / Integration / Security）、主要テストケース一覧、カバレッジ目標を記載 |
| 21 | **High** | tech-lead | 横断整合性 | §API 設計 | API パスのバージョニングが不統一。Auth API は `/api/v1/auth/...`、ユーザー登録は `/api/auth/users`（`v1` 欠落）、OAuth2 は `/oauth2/authorization/azure`、Graph は `/api/user/me`。URI 設計が一貫していない | 全エンドポイントを `/api/v1/auth/...` に統一。OAuth2 コールバック（`/signin-oidc`）等はフレームワーク制約のため例外として明記 |
| 22 | **High** | tech-lead | 規約整合性 | §ログインレスポンス | ログインレスポンスに `"success": true/false` ラッパーを使用。REST 原則では HTTP ステータスコードで成功/失敗を表現すべき。`api-design.instructions.md` は HTTP ステータスコードの意味論的な使用を規定 | `success` フィールドを除去。成功時は `200 OK` + レスポンスボディ、失敗時は適切な 4xx/5xx + Problem Details |
| 23 | **High** | tech-lead | Resilience | 設計書全体 | 外部サービス（Azure AD, Microsoft Graph API）呼び出しに Polly / `AddStandardResilienceHandler` の設計がない。AGENTS.md §11.1 は全外部 HTTP 通信に `IHttpClientFactory` + Polly を必須としている | Azure AD / Graph API 呼出し用の `IHttpClientFactory` 登録 + `AddStandardResilienceHandler` 設定（リトライ 3 回、サーキットブレーカー 10 秒遮断）を追記 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| 1 | 最優先 | security-reviewer | JWT アルゴリズム（HS512 vs RS256）の最終決定。マイクロサービス構成での鍵管理方針を含む | セキュリティアーキテクト |
| 2 | 最優先 | security-reviewer, architect | パスワードハッシュアルゴリズムの最終決定（PBKDF2 / BCrypt / Argon2）。ASP.NET Core Identity デフォルトとの整合性 | テックリード |
| 3 | 高優先 | architect | AuthService と UserManagementService の責務境界。設計書では AuthService が直接ユーザー登録 (`POST /api/auth/users`) を受け付けているが、spec.md §7 のシーケンス図では `UserService` に委譲している。ユーザー CRUD が AuthService と UserManagementService のどちらに属するかの明確化が必要 | プロダクトオーナー + テックリード |
| 4 | 高優先 | compliance-reviewer | SecurityLog の保持期間・削除ポリシー。GDPR の「忘れられる権利」と監査要件のバランス | 法務 + セキュリティアーキテクト |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| — | — | — | Phase 2 で競合は検出されなかった | — | — |

---

## ドキュメント横断分析

### サービス間整合性

| 検証項目 | 結果 | 詳細 |
|---------|------|------|
| DB 名と ADR-0006 | ❌ 不整合 | 設計書: `skishopdb` / ADR-0006: `authdb` |
| Kafka トピック名 | ⚠️ 要確認 | 設計書固有のトピック名（`auth.users` 等）が他サービスの購読設計と整合するか未検証 |
| UserService との責務境界 | ⚠️ 曖昧 | ユーザー登録・削除が AuthService に存在（`POST /api/auth/users`）。UserManagementService との責務重複の可能性 |
| Outbox パターン | ❌ 欠落 | ADR-0005 必須だが `outbox_events` テーブル・`OutboxPublisher` の設計がない |
| エラーレスポンス形式 | ❌ 不整合 | ADR-0007 (RFC 9457) 非準拠の独自形式 |

### 未定義・曖昧な領域

| # | 領域 | 詳細 | 実装ブロッカーリスク |
|---|------|------|-------------------|
| 1 | Client Credentials フロー | サービス間認証（API Gateway → AuthService）の具体的な認証フロー（Client Credentials Grant）の設計が未記載 | 高 |
| 2 | トークン失効（Revocation） | JWT ブラックリストの具体的な実装設計（Redis キー設計は記載あり、実装フロー未定義） | 中 |
| 3 | MFA リカバリーフロー | バックアップコード使用時のフロー、MFA デバイス紛失時のリカバリー手順が未定義 | 中 |
| 4 | 同時セッション制限 | 同一ユーザーの最大同時セッション数の制限ポリシーが未定義 | 低 |
| 5 | アカウントロック解除フロー | 5 回失敗でロックされた後の解除手順（自動解除タイマー or 管理者操作 or パスワードリセット）が未記載 | 中 |
| 6 | Correlation ID | AGENTS.md §11.2 必須の Correlation ID ミドルウェアの設計が未記載 | 中 |

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### business-analyst レビュー

**判定**: ⚠️ Conditional

| 重要度 | 件数 |
|--------|------|
| Critical | 0 |
| High | 1 |
| Medium | 2 |
| Low | 0 |

#### 指摘一覧

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|----------|----------|
| BA-1 | High | ロール定義に USER と CUSTOMER が別々に存在するが、EC サイトにおける使い分け（一般ユーザーと顧客の違い）が不明確。ビジネス要件として両者の権限差分が未定義 | ロールの権限マトリクスを詳細化し、USER と CUSTOMER の具体的な差分を明記。不要であれば統合 |
| BA-2 | Medium | ゲスト購入フローにおける「会員登録時に過去のゲスト注文を紐付け」の仕様は spec.md に記載があるが、AuthService 側でのユーザー登録後イベント（`UserRegistered`）とゲスト注文紐付けの連携が不明確 | UserRegistered イベント発行後の SalesManagementService 側での紐付け処理フローを明記 |
| BA-3 | Medium | パスワードポリシー（最小長、複雑性要件）の具体的なルールが未定義。エラーコード AUTH-4221, AUTH-4222 が存在するが、ポリシーの内容が記載されていない | パスワードポリシー（最小 8 文字、大文字・小文字・数字・記号の組み合わせ要件等）を明記 |

</details>

<details>
<summary>architect レビューレポート</summary>

### architect レビュー

**判定**: ❌ Rejected

| 重要度 | 件数 |
|--------|------|
| Critical | 1 |
| High | 3 |
| Medium | 2 |
| Low | 0 |

#### 指摘一覧

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|----------|----------|
| ARCH-1 | Critical | DB 名が `skishopdb`（共有 DB）と記載。ADR-0006 により AuthService は専用の `authdb` を使用すべき | DB 名を `authdb` に修正 |
| ARCH-2 | High | Outbox パターン（ADR-0005）の設計が欠落。`outbox_events` テーブル未定義、`OutboxPublisher` 未設計 | Outbox テーブル・Publisher の設計を追加 |
| ARCH-3 | High | Kafka トピック名の命名規則が他サービスと不整合の可能性 | トピック命名規則を spec.md と統一 |
| ARCH-4 | High | エラーレスポンスが RFC 9457 非準拠（ADR-0007 違反） | Problem Details 形式に統一 |
| ARCH-5 | Medium | マイクロサービス関連図に「監査サービス」が記載されているが、マイクロサービス一覧（AGENTS.md §2.3）に該当サービスが存在しない | 図を修正。監査機能は SecurityLog + OpenTelemetry で実現 |
| ARCH-6 | Medium | AuthService と UserManagementService の責務重複。ユーザー登録・削除 API が AuthService に存在するが、spec.md ではユーザー CRUD は UserManagementService の責務 | 責務境界を明確化。AuthService は認証・トークン管理に専念し、ユーザー CRUD は UserManagementService に委譲 |

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### programing-reviewer レビュー

**判定**: ⚠️ Conditional

| 重要度 | 件数 |
|--------|------|
| Critical | 0 |
| High | 3 |
| Medium | 2 |
| Low | 1 |

#### 指摘一覧

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|----------|----------|
| PR-1 | High | EF Core エンティティクラスの C# 定義が存在しない。`[Table]`, `[Column]` 属性、コレクション初期化、`DateTime.UtcNow` 使用等の AGENTS.md §10.3 準拠の定義が必要 | 主要エンティティの C# クラス定義を追加 |
| PR-2 | High | Service / Repository インターフェース定義が欠落 | `IAuthService`, `IJwtTokenService` 等のインターフェース定義を追加 |
| PR-3 | High | CancellationToken の記載が全 API 定義に欠落 | 全 async メソッドシグネチャに `CancellationToken ct = default` を追加 |
| PR-4 | Medium | DTO（リクエスト/レスポンス）の C# record 定義が欠落。Data Annotations バリデーション属性の記載がない | `LoginRequest`, `TokenRefreshRequest` 等の record 定義と `[Required]`, `[EmailAddress]`, `[StringLength]` 属性を追加 |
| PR-5 | Medium | Minimal API エンドポイントの実装パターン（`MapGroup` + 拡張メソッド）の設計例がない。AGENTS.md §6.2 の `ProductEndpoints` パターンに準拠した `AuthEndpoints` の設計が必要 | `AuthEndpoints.MapAuthEndpoints()` の実装パターン例を追加 |
| PR-6 | Low | NuGet パッケージに `FluentValidation.DependencyInjectionExtensions` が欠落。AGENTS.md §8.1 は両方を必須としている | パッケージリストに追加 |

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### dba-reviewer レビュー

**判定**: ❌ Rejected

| 重要度 | 件数 |
|--------|------|
| Critical | 1 |
| High | 3 |
| Medium | 3 |
| Low | 1 |

#### 指摘一覧

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|----------|----------|
| DBA-1 | Critical | 全日時カラムが `TIMESTAMP` であり `TIMESTAMP WITH TIME ZONE` ではない | 全日時カラムを `TIMESTAMP WITH TIME ZONE` に修正 |
| DBA-2 | High | `status` カラムに CHECK 制約がない | `ck_users_status CHECK (status IN ('PENDING_VERIFICATION', 'ACTIVE', 'SUSPENDED'))` を追加 |
| DBA-3 | High | `role` カラムに CHECK 制約がない | `ck_users_role CHECK (role IN ('ADMIN', 'MANAGER', 'STAFF', 'EMPLOYEE', 'USER', 'CUSTOMER'))` を追加 |
| DBA-4 | High | `roles` テーブルの定義が欠落しているが `user_roles.role_id` が FK 参照している | `roles` テーブル定義を追加 |
| DBA-5 | Medium | `oauth_accounts` テーブルの `(provider, provider_user_id)` に UNIQUE 制約がない。インデックス `idx_oauth_provider_user` は存在するが UNIQUE ではないため、同一 OAuth アカウントの重複登録が可能 | `CONSTRAINT uq_oauth_provider_user UNIQUE (provider, provider_user_id)` を追加 |
| DBA-6 | Medium | `security_logs` テーブルに `updated_at` カラムが欠落。`sql-schema-review.instructions.md` §2 は全テーブルに `created_at` + `updated_at` を求めている。append-only 設計であれば明示的にその旨を注記すべき | append-only テーブルとして `updated_at` 不要の根拠を明記。または `updated_at` を追加 |
| DBA-7 | Medium | 外部キー制約の `ON DELETE` / `ON UPDATE` 動作が全テーブルで未定義。`sql-schema-review.instructions.md` §3 は動作の明示を求めている | 各 FK に `ON DELETE RESTRICT` / `ON DELETE CASCADE` 等を明示。特に `user_sessions`, `oauth_accounts` のユーザー削除時動作 |
| DBA-8 | Low | `password_resets.token` の VARCHAR(255) は十分だが、セキュアなランダムトークンの生成方式（`RandomNumberGenerator.GetBytes` 等）の記載がない | トークン生成方式を暗号論的疑似乱数生成器で行う旨を明記 |

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### security-reviewer レビュー

**判定**: ❌ Rejected

| 重要度 | 件数 |
|--------|------|
| Critical | 1 |
| High | 3 |
| Medium | 2 |
| Low | 0 |

#### 指摘一覧

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|----------|----------|
| SEC-1 | Critical | JWT アルゴリズム HS512 は対称鍵であり、マイクロサービス間で秘密鍵を共有する必要がある。鍵漏洩リスクが高い | RS256 に変更 |
| SEC-2 | High | パスワードハッシュに BCrypt を使用しているが AGENTS.md との不整合 | PBKDF2 または Argon2 に変更し根拠を明記 |
| SEC-3 | High | イベントペイロードに PII（email）が含まれている | email を除去し userId のみで識別 |
| SEC-4 | High | MFA シークレット（`secret_key`, `backup_codes`）の暗号化保存の記載がない | AES-256-GCM 暗号化保存を明記 |
| SEC-5 | Medium | レート制限の設計が「IP あたり 100 リクエスト/分」のみ。認証エンドポイント（`/auth/login`）には専用の厳しいレート制限（`security-coding.instructions.md` §4 — 1 分間 5 回）が必要 | `/auth/login` 専用のレート制限ポリシー（1 分間 5 回）を追加 |
| SEC-6 | Medium | CSRF 保護が「Web エンドポイントで有効」と記載されているが、OAuth2 の `state` パラメータによる CSRF 防止の設計が不明確。OAuth2 フローでの CSRF 攻撃（authorization code interception）への対策が未記載 | OAuth2 `state` パラメータの生成・検証フローを明記。PKCE（Proof Key for Code Exchange）の採用を検討 |

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### compliance-reviewer レビュー

**判定**: ⚠️ Conditional

| 重要度 | 件数 |
|--------|------|
| Critical | 0 |
| High | 1 |
| Medium | 2 |
| Low | 0 |

#### 指摘一覧

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|----------|----------|
| COMP-1 | High | Kafka イベントに PII を含める場合のデータ保持・削除ポリシーが未定義（GDPR 第 17 条「忘れられる権利」） | イベントペイロードの PII 最小化。保持期間・削除手順を明記 |
| COMP-2 | Medium | `security_logs` の保持期間が未定義。監査要件と GDPR のデータ最小化原則のバランスが必要 | 保持期間（例: 90 日 / 1 年）と自動削除ポリシーを追記 |
| COMP-3 | Medium | パスワードリセットメール送信時のメールアドレスの取り扱い（暗号化 in transit、ログ出力禁止）が未記載 | メール送信フローでの PII 保護方針を追記 |

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### audit-reviewer レビュー

**判定**: ⚠️ Conditional

| 重要度 | 件数 |
|--------|------|
| Critical | 0 |
| High | 1 |
| Medium | 1 |
| Low | 0 |

#### 指摘一覧

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|----------|----------|
| AUD-1 | High | SecurityLog の `details` JSONB に格納可能な情報のホワイトリストが未定義。PII 混入リスク | 格納項目のホワイトリストを定義 |
| AUD-2 | Medium | Correlation ID の設計が未記載。AGENTS.md §11.2 は全リクエストに Correlation ID を付与し、マイクロサービス間で伝搬することを必須としている | Correlation ID ミドルウェアの設計を追記 |

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### qa-manager レビュー

**判定**: ⚠️ Conditional

| 重要度 | 件数 |
|--------|------|
| Critical | 0 |
| High | 1 |
| Medium | 1 |
| Low | 0 |

#### 指摘一覧

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|----------|----------|
| QA-1 | High | テスト戦略セクションが完全に欠落 | テスト戦略（Unit / Integration / Security テスト）、主要テストケース一覧、カバレッジ目標を追加 |
| QA-2 | Medium | テスト用のモックライブラリが `Moq` と記載されているが、AGENTS.md は `NSubstitute` を指定（oss-reviewer の指摘と重複） | NSubstitute に統一 |

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### performance-reviewer レビュー

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
| PERF-1 | Medium | Redis のセッション TTL（30 分）とアイドルタイムアウト（30 分）の関係が不明確。スライディング有効期限 vs 絶対有効期限の設計が未記載 | スライディング/絶対期限の方式を明記 |
| PERF-2 | Medium | JWT ブラックリストの Redis 検索がトークン検証の都度発生する場合のパフォーマンス影響が未分析。大量のブラックリストエントリ時の O(1) アクセス保証が必要 | Redis SET 型 + `SISMEMBER` による O(1) 検索を明記 |
| PERF-3 | Low | パフォーマンスメトリクスの「現在値」（例: 平均 150ms）が設計書段階で記載されている。実装前に実測値を記載するのは不適切 | 「現在値」列を除去、または「目標値」のみに変更 |

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### infra-ops-reviewer レビュー

**判定**: ⚠️ Conditional

| 重要度 | 件数 |
|--------|------|
| Critical | 0 |
| High | 1 |
| Medium | 1 |
| Low | 1 |

#### 指摘一覧

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|----------|----------|
| INFRA-1 | High | Dockerfile の HEALTHCHECK が `curl -f http://localhost:5001/health` を使用しているが、`mcr.microsoft.com/dotnet/aspnet:10.0` イメージには `curl` が含まれていない | `wget --spider` に変更、またはマルチステージビルドで `curl` を追加。Azure Container Apps 環境ではコンテナオーケストレーターのヘルスプローブ設定を推奨 |
| INFRA-2 | Medium | Dockerfile で `ASPNETCORE_URLS` が設定されていないが `EXPOSE 5001` と `HEALTHCHECK` で 5001 番ポートを期待。.NET 8+ のデフォルトリスンポートは 8080 のため、このままでは HEALTHCHECK が失敗する | `ENV ASPNETCORE_URLS=http://+:5001` を Dockerfile に追加。または `EXPOSE 8080` + port 8080 に統一 |
| INFRA-3 | Low | Docker のバージョンが「最新」と記載。AGENTS.md は Docker 25.x を指定 | `Docker 25.x` に固定 |

</details>

<details>
<summary>release-manager レビューレポート</summary>

### release-manager レビュー

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
| REL-1 | Medium | MFA 機能のステータスが「🔄 進行中」だが、リリーススコープ（MVP に含むか Phase 2 送りか）が不明確 | MFA を MVP / Post-MVP のどちらに含めるかを明記 |

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### oss-reviewer レビュー

**判定**: ⚠️ Conditional

| 重要度 | 件数 |
|--------|------|
| Critical | 0 |
| High | 1 |
| Medium | 1 |
| Low | 0 |

#### 指摘一覧

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|----------|----------|
| OSS-1 | High | テストモックライブラリが `Moq` と記載。AGENTS.md は `NSubstitute 5.*` を指定 | `NSubstitute 5.*` に変更 |
| OSS-2 | Medium | `Serilog.Sinks.Console 6.*` が NuGet パッケージリストに欠落。AGENTS.md §8.1 の必須パッケージ | パッケージリストに追加 |

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### ux-accessibility-reviewer レビュー

**判定**: ✅ Pass（該当なし）

認証サービスはバックエンド API サービスであり、UX/アクセシビリティの直接的な対象外。フロントエンドの認証 UI は `front-end-need.md` で別途レビュー対象。

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### tech-lead レビュー

**判定**: ❌ Rejected

| 重要度 | 件数 |
|--------|------|
| Critical | 0 |
| High | 2 |
| Medium | 1 |
| Low | 0 |

#### 指摘一覧

| # | 重要度 | 指摘内容 | 推奨対応 |
|---|--------|----------|----------|
| TL-1 | High | API パスのバージョニングが不統一（`/api/v1/auth/...` vs `/api/auth/users` vs `/oauth2/...` vs `/api/user/me`） | 全エンドポイントを `/api/v1/auth/...` に統一 |
| TL-2 | High | ログインレスポンスの `"success": true/false` ラッパーが REST 原則・RFC 9457 と不整合 | `success` フィールドを除去 |
| TL-3 | Medium | 外部サービス呼び出しに Polly / `AddStandardResilienceHandler` の設計がない | Resilience 設計を追加 |

**Tech-Lead 総合所見**:

本設計書は認証サービスの概要・API 定義・DB スキーマ・イベント設計・監視設計を網羅的にカバーしており、全体構成は妥当である。しかし、以下の 3 点で AGENTS.md / ADRs / Instructions との重大な不整合が存在し、是正なしに実装に着手すべきでない:

1. **DB スキーマの TIMESTAMP WITH TIME ZONE 違反** — sql-schema-review.instructions.md の必須要件
2. **ADR-0006 違反（共有 DB `skishopdb`）** — マイクロサービスの独立性を根本から損なう
3. **JWT HS512 のセキュリティリスク** — マイクロサービス間での対称鍵共有は鍵管理コストとリスクが高い

上記 Critical 3 件の是正後、C# エンティティ定義・インターフェース定義・CancellationToken・Outbox パターン・RFC 9457 準拠のエラー形式等の High 指摘を対応することで、実装可能な品質に到達する。

</details>
