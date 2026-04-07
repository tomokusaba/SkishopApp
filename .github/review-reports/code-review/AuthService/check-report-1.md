# ソースコードレビュー統合レポート

## 判定結果
- **対象**: `Services/AuthService/` — 全 65+ ソースファイル（Endpoints 7, Services 11, Repositories 12, Models 13, Infrastructure, DTOs, Validators, Configurations, Exceptions, Enums）+ `Services/AuthService.Tests/`（8 テストファイル）
- **判定**: ❌ **Rejected** — 重大な不備あり
- **レビュー日時**: 2026-04-06
- **プロジェクト**: SkiShop (.NET 10 マイクロサービス EC サイト)

## 指摘サマリー

| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| tech-lead | ❌ Fail | 2 | 7 | 5 | 2 |
| architecture-reviewer | ❌ Fail | 1 | 2 | 3 | 2 |
| ddd-domain-reviewer | ❌ Fail | 5 | 7 | 5 | 2 |
| api-endpoint-reviewer | ❌ Fail | 2 | 3 | 4 | 2 |
| csharp-standards-reviewer | ⚠️ Warning | 0 | 8 | 4 | 2 |
| async-concurrency-reviewer | ⚠️ Warning | 0 | 3 | 1 | 4 |
| error-logging-reviewer | ⚠️ Warning | 0 | 3 | 3 | 2 |
| data-access-reviewer | ⚠️ Warning | 1 | 3 | 4 | 2 |
| config-di-reviewer | ⚠️ Warning | 0 | 3 | 4 | 1 |
| security-reviewer | ⚠️ Warning | 0 | 4 | 4 | 3 |
| dependency-reviewer | ⚠️ Warning | 0 | 3 | 3 | 1 |
| test-quality-reviewer | ❌ Fail | 0 | 12 | 5 | 2 |
| performance-reviewer | ⚠️ Warning | 0 | 2 | 3 | 2 |
| resilience-reviewer | ⚠️ 不完全 | — | — | — | — |
| **合計（重複排除後）** | | **5** | **24** | **18** | **10** |

## 判定根拠
- **Critical 指摘が 5 件検出**されたため、自動的に **❌ Rejected** 判定
- 最も重大な指摘: ログアウト機能のバグ（#C2）、MfaEndpoints のレイヤー違反（#C1）、User が貧血ドメインモデル（#C5）

---

## Critical/High 指摘一覧（修正必須）

### 🚨 Critical（5 件）

| # | 出典 Agent | カテゴリ | 対象ファイル | 指摘内容 | 修正方針 |
|---|-----------|---------|------------|---------|---------|
| C1 | architecture, api-endpoint, csharp-standards, tech-lead | レイヤー違反 | `Endpoints/MfaEndpoints.cs` L42-130 | `VerifyMfaAsync` が **6 つの Repository を直接注入**し、90 行のビジネスロジック（セッション生成・JWT 発行・トークン永続化・Outbox イベント）を Endpoint 内に実装。AGENTS.md §2.1 の依存方向に完全違反。`AuthServiceImpl.LoginAsync` とロジックが重複し DRY 原則も破壊。 | `IAuthService` に `CompleteMfaLoginAsync` を追加し、全ロジックを Service 層に移動。Endpoint は `IMfaService.VerifyMfaAsync` + `IAuthService.CompleteMfaLoginAsync` の 2 呼び出しのみに |
| C2 | tech-lead | バグ | `Endpoints/AuthEndpoints.cs` L86-92 + `Services/AuthServiceImpl.cs` L248 | `LogoutAsync` で JWT の `session_id` クレーム値（= `UserSession.Id`、GUID）を渡すが、実装は `FindBySessionTokenAsync`（`SessionToken` フィールド = ランダム Base64）で検索。**Id ≠ SessionToken のためログアウトが常に失敗**する。セッション無効化もリフレッシュトークン無効化も実行されない。 | `LogoutAsync` の検索を `FindByIdAsync(sessionId)` に変更するか、JWT クレームに `session_token` を含める |
| C3 | ddd-domain | Aggregate 境界違反 | 全子エンティティ Repository | `UserSession`, `RefreshToken`, `UserMfa`, `OAuthAccount`, `PasswordHistory`, `PasswordReset` がそれぞれ独立 Repository で直接操作され、Aggregate Root（`User`）を経由していない | 認証ドメインの特性上、子エンティティ単位での効率的操作が必要なため **部分的に許容**。ただしドメインロジックは `User` に集約する |
| C4 | ddd-domain | 貧血ドメインモデル | `Models/User.cs` | `User` が getter/setter のみのデータコンテナ。ロック判定・ステータス遷移・セッション管理等のビジネスロジックが全て Service 層に散在。 | `User` に `RecordFailedLogin()`, `Unlock()`, `Activate()`, `Suspend()` 等のドメインメソッドを追加 |
| C5 | data-access | TimeProvider 不使用 | `Repositories/RefreshTokenRepository.cs` L17, L45 | `DateTimeOffset.UtcNow` を直接使用。DI 注入された `TimeProvider` を経由しておらず、テスト時に時刻をモックできない | `TimeProvider` を Repository に注入し `timeProvider.GetUtcNow()` に置換 |

### ⚠️ High（24 件 → 重複排除後 17 件）

| # | 出典 Agent | カテゴリ | 対象ファイル | 指摘内容 |
|---|-----------|---------|------------|---------|
| H1 | async, tech-lead | トランザクション境界 | `Services/SecurityService.cs` L37-38 | `LogSecurityEventAsync` 内で独自に `SaveChangesAsync` を呼ぶため呼び出し元のトランザクション境界を破壊 |
| H2 | csharp, tech-lead | 命名不整合 | `Infrastructure/Security/Argon2PasswordHasher.cs` L6 | クラス名が `Argon2PasswordHasher` だが実装は PBKDF2（`PasswordHasher<User>` 600K iterations） |
| H3 | csharp, ddd, tech-lead | Enum 未使用 | `Enums/` 全 4 ファイル | `UserStatus`, `UserRoleType`, `SecurityEventType`, `OutboxStatus` が定義済みだがコード全体で未使用。マジックストリングが散在 |
| H4 | tech-lead | 可観測性 | `Infrastructure/Metrics/AuthMetrics.cs` L5-37 | 6 カウンタが定義のみで `.Add()` が一切呼ばれていない。メトリクスが収集されない |
| H5 | api-endpoint, tech-lead | スタブ残存 | `Endpoints/OAuthEndpoints.cs` L47-53 + `Services/OAuthService.cs` L17-25 | `StartOAuthFlowAsync` はダミー URL 返却、`HandleOAuthCallbackAsync` は常に例外。`AllowAnonymous` で公開中 |
| H6 | tech-lead | セキュリティ | `Services/OAuthService.cs` L45 | `LinkAccountAsync` で OAuth 認可コードを `ProviderUserId` として DB に永続化。認可コードは一時クレデンシャルでありユーザー識別子ではない |
| H7 | api-endpoint | 入力バリデーション | `Endpoints/OAuthEndpoints.cs` L55-62 | `OAuthCallbackAsync` の `code` パラメータにバリデーションなし（OWASP A03） |
| H8 | api-endpoint, tech-lead | OpenAPI | 全 7 Endpoint ファイル | `.WithOpenApi()` が全エンドポイントで欠落 |
| H9 | config-di | IOptions 未使用 | `Program.cs` L151-155 | JWT Bearer 設定で `builder.Configuration["Jwt:*"]` を直接参照。`IOptions<JwtSettings>` との二重管理 |
| H10 | config-di | Kafka 設定 | `Program.cs` L124 + BackgroundServices | `KafkaSettings` 設定クラスが存在しない。`IConfiguration` から直接取得 |
| H11 | csharp | Null Safety | `Argon2PasswordHasher.cs` L15 + `ClientCredentialsService.cs` L47 | `user!` / `null!` の null 強制演算子使用 |
| H12 | csharp | TimeProvider 不使用 | `Services/TotpService.cs` L41 | `DateTimeOffset.UtcNow` を直接使用。`TimeProvider` 未注入 |
| H13 | csharp | 命名規則 | `Models/User.cs` L49, L55 | `EmailVerified` → `IsEmailVerified`、`AccountLocked` → `IsAccountLocked` |
| H14 | data-access | 楽観的ロック | 全 Service | `User` に `[Timestamp]` があるが `DbUpdateConcurrencyException` のハンドリングが一切なし |
| H15 | data-access | ExecuteUpdateAsync | `UserSessionRepository.cs` L27 + `RefreshTokenRepository.cs` L24 | `ExecuteUpdateAsync` で `UpdatedAt` が更新されない（Change Tracker バイパス） |
| H16 | ddd | Events 二重配置 | `Events/` と `DTOs/Events/` | Domain Event が 2 箇所に重複定義。`Events/` の `IAuthEvent` は未使用 |
| H17 | test-quality | テスト不足 | `AuthService.Tests/` | Endpoint 統合テスト 0 件、Repository テスト 0 件、BackgroundService テスト 0 件、Service テスト 4 クラス欠落、`RefreshTokenAsync` 未テスト（セキュリティリスク） |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| 1 | **最優先** | tech-lead | **ログアウト機能が動作しない（C2）** — `session_id` vs `SessionToken` の不整合で常にセッション未検出。本番稼働前に即時修正が必要 | テックリード |
| 2 | **最優先** | tech-lead | **MfaEndpoints のリファクタリング範囲（C1）** — Service 層への移動は Endpoint + Service + Interface の変更を伴う | テックリード |
| 3 | **高優先** | tech-lead, security | **Argon2 vs PBKDF2 の方針決定（H2）** — セキュリティ要件として Argon2 が必要か、PBKDF2 600K iterations で十分かの判断 | セキュリティ責任者 |
| 4 | **高優先** | tech-lead | **OAuth 実装のロードマップ判断（H5）** — 未実装スタブが `AllowAnonymous` で公開中。実装予定がなければ削除推奨 | プロダクトオーナー |
| 5 | **高優先** | csharp, ddd | **Enum ↔ マジックストリングの統一方針（H3）** — 全サービスに波及する変更。EF Core `HasConversion<string>()` 採用可否を決定 | テックリード |
| 6 | **通常** | test-quality | **テスト追加の優先順位判断（H17）** — Unit Test の品質は高いが、統合テスト・DB テスト・セキュリティテストが完全不在 | テックリード |

---

## 競合解決記録

Agent 間の競合は検出されなかった。全 Agent が同一方向の指摘を行っており、裁定は不要。

---

## 設計書との照合結果

### 設計書からの逸脱
| 設計書 | 逸脱内容 |
|--------|---------|
| AGENTS.md §2.1 レイヤー依存方向 | `MfaEndpoints` が Repository を直接参照（C1） |
| AGENTS.md §10.3 エンティティ規約 | Model 初期値で `DateTimeOffset.UtcNow` を直接使用（TimeProvider 非経由） |
| AGENTS.md §11.2 可観測性 | `AuthMetrics` カウンタが全く計測されていない（H4） |
| AGENTS.md §4.2 禁止事項 | 禁止事項 10 項目はすべてクリア ✅ |

### 未実装の設計要素
- OAuth2 フロー（Google/GitHub/Microsoft 連携）が未実装スタブのまま
- リプレイ攻撃検出のテストが未実装
- `AuthDbContext.SaveChangesAsync` オーバーライドで `IHasTimestamps` の自動更新が未確認

---

## 良い点（特筆事項）

以下の点は高品質であり、他サービスの模範となる:

1. **禁止事項の完全遵守**: `Console.WriteLine`, `.Result/.Wait()`, ハードコード秘密情報, `new HttpClient()` 等の 10 項目がゼロ検出
2. **primary constructor の統一**: 全 Service/Repository で primary constructor を使用
3. **record 型 DTO**: 全リクエスト/レスポンスが不変 record 型で定義
4. **CancellationToken の伝搬**: 全 async メソッドに `CancellationToken ct = default` が含まれ、下位呼び出しに伝搬
5. **BackgroundService の品質**: 全 7 サービスが `IServiceScopeFactory` + `stoppingToken` 伝搬 + 適切な例外ハンドリングの模範実装
6. **リフレッシュトークンリプレイ検出**: FamilyId ベースの全トークン無効化ロジックが実装済み
7. **パスワード履歴チェック**: 過去 5 件のパスワード再利用防止が実装済み
8. **セキュリティログの匿名化**: 90 日後の IP アドレス SHA-256 ハッシュ化 + 365 日後の物理削除
9. **テストの命名規約**: 全テストが `Should_X_When_Y` パターンに完全準拠
10. **appsettings.json の秘密情報管理**: JWT SecretKey, 接続文字列等が設定ファイルに記載なし

---

## 各 Agent 詳細レポート

<details>
<summary>tech-lead レビューレポート</summary>

**判定**: ❌ Fail — Critical: 2 / High: 7 / Medium: 5 / Low: 2

主要指摘:
1. [Critical] MfaEndpoints のレイヤー違反（6 Repository 直接注入 + 90 行のビジネスロジック）
2. [Critical] LogoutAsync のバグ（session_id vs SessionToken の不整合でログアウト常時失敗）
3. [High] SecurityService.LogSecurityEventAsync のトランザクション境界破壊
4. [High] AuthMetrics の 6 カウンタが定義のみで未使用
5. [High] Argon2PasswordHasher の命名が PBKDF2 実装と不一致
6. [High] Enum 4 種が定義済みだがマジックストリングが散在
7. [High] OAuth スタブが AllowAnonymous で公開中
8. [High] OAuth 認可コードを ProviderUserId として永続化
</details>

<details>
<summary>architecture-reviewer レビューレポート</summary>

**判定**: ❌ Fail — Critical: 1 / High: 2 / Medium: 3 / Low: 2

主要指摘:
1. [Critical] MfaEndpoints が Repository を直接参照（レイヤー依存方向違反）
2. [High] MfaEndpoints.VerifyMfaAsync にビジネスロジック集中
3. [High] DTOs/Events/ と Events/ の二重配置
</details>

<details>
<summary>ddd-domain-reviewer レビューレポート</summary>

**判定**: ❌ Fail — Critical: 5 / High: 7 / Medium: 5 / Low: 2

主要指摘:
1. [Critical] Aggregate Root 境界違反（子エンティティの独立 Repository 操作）
2. [Critical] 貧血ドメインモデル（User が getter/setter のみ）
3. [High] Value Object 未使用（Email/Status/Role が string 型）
4. [High] Enum 定義が存在するが未使用
5. [High] ドメインロジック漏洩（SecurityService が User の状態を直接変更）
</details>

<details>
<summary>api-endpoint-reviewer レビューレポート</summary>

**判定**: ❌ Fail — Critical: 2 / High: 3 / Medium: 4 / Low: 2

主要指摘:
1. [Critical] MfaEndpoints の責務違反（90 行のビジネスロジック）
2. [Critical] OAuthCallbackAsync の code パラメータにバリデーションなし
3. [High] OAuth スタブの残存
4. [High] .WithOpenApi() が全エンドポイントで欠落
</details>

<details>
<summary>csharp-standards-reviewer レビューレポート</summary>

**判定**: ⚠️ Warning — Critical: 0 / High: 8 / Medium: 4 / Low: 2

主要指摘:
1. [High] User.EmailVerified/AccountLocked の Is プレフィックス欠如
2. [High] Argon2PasswordHasher の命名不整合
3. [High] null! 使用（Argon2PasswordHasher, ClientCredentialsService）
4. [High] TotpService の TimeProvider 未注入
5. [High] Enum 4 種が未使用（マジックストリング散在）
</details>

<details>
<summary>async-concurrency-reviewer レビューレポート</summary>

**判定**: ⚠️ Warning — Critical: 0 / High: 3 / Medium: 1 / Low: 4

主要指摘:
1. [High] SecurityService.LogSecurityEventAsync のトランザクション境界破壊（3 箇所）
2. [Medium] RedisCacheService の CancellationToken が StackExchange.Redis で無視される
3. [Low] ValidateTokenAsync, StartOAuthFlowAsync の不要な async
</details>

<details>
<summary>error-logging-reviewer レビューレポート</summary>

**判定**: ⚠️ Warning — Critical: 0 / High: 3 / Medium: 3 / Low: 2

主要指摘:
1. [High] AuthMetrics カウンタが未使用（メトリクス収集なし）
2. [High] グローバル例外ハンドラーで TypedResults.Problem ではなく匿名型を使用
3. [Medium] MfaRequiredException の HTTP 202 は RFC 9457 非準拠
</details>

<details>
<summary>data-access-reviewer レビューレポート</summary>

**判定**: ⚠️ Warning — Critical: 1 / High: 3 / Medium: 4 / Low: 2

主要指摘:
1. [Critical] RefreshTokenRepository で DateTimeOffset.UtcNow 直接使用（TimeProvider 不使用）
2. [High] ExecuteUpdateAsync で UpdatedAt が更新されない
3. [High] DbUpdateConcurrencyException のハンドリングなし
4. [Medium] Migrations ディレクトリが空
</details>

<details>
<summary>config-di-reviewer レビューレポート</summary>

**判定**: ⚠️ Warning — Critical: 0 / High: 3 / Medium: 4 / Low: 1

主要指摘:
1. [High] JWT Bearer 設定で IOptions 未使用（Configuration 直接参照）
2. [High] KafkaSettings 設定クラスが存在しない
3. [Medium] 設定クラスに Data Annotations 未付与（ValidateOnStart が空振り）
</details>

<details>
<summary>security-reviewer レビューレポート</summary>

**判定**: ⚠️ Warning — Critical: 0 / High: 4 / Medium: 4 / Low: 3

主要指摘:
1. [High] OAuth 認可コードを ProviderUserId として永続化
2. [High] OAuthCallbackAsync/LinkAccountAsync の code パラメータ未検証
3. [Medium] provider パスパラメータのホワイトリスト検証なし
4. [Medium] ユーザー登録・メール確認の Rate Limiting が緩い（general ポリシー）
</details>

<details>
<summary>dependency-reviewer レビューレポート</summary>

**判定**: ⚠️ Warning — Critical: 0 / High: 3 / Medium: 3 / Low: 1

主要指摘:
1. [High] Polly, Microsoft.Extensions.Http.Resilience, Azure.Identity が未使用
2. [Medium] Microsoft.Identity.Web が未使用
3. [Medium] Testcontainers.PostgreSql がテストプロジェクトに未追加
</details>

<details>
<summary>test-quality-reviewer レビューレポート</summary>

**判定**: ❌ Fail — Critical: 0 / High: 12 / Medium: 5 / Low: 2

主要指摘:
1. [High] SecurityService, OAuthService, AuditLogService, TotpService のテスト不在
2. [High] Endpoint 統合テスト（WebApplicationFactory）不在
3. [High] Repository DB テスト（Testcontainers）不在
4. [High] BackgroundService テスト不在
5. [High] RefreshTokenAsync のテスト不在（セキュリティリスク）
</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

**判定**: ⚠️ Warning — Critical: 0 / High: 2 / Medium: 3 / Low: 2

主要指摘:
1. [High] PermissionsUpdatedConsumer でトークンを全件ロード後ループ更新（ExecuteUpdateAsync 推奨）
2. [Medium] SecurityLogAnonymizationService の大量ログ処理でメモリ圧力
3. [Medium] SanitizeUserAgent の毎回アロケーション
</details>

<details>
<summary>resilience-reviewer レビューレポート</summary>

**判定**: ⚠️ レビュー不完全（エラー）

Agent がエラー終了。レビュー結果は取得できなかった。
確認済みの事項（Phase 1 での事前調査）:
- HealthChecks: /health + /health/ready 実装済み（NpgSql + Redis）
- Polly/Microsoft.Extensions.Http.Resilience は .csproj に記載あるが未使用
- Redis 障害時のフォールバックなし
- OutboxPublisher に Kafka 送信リトライ + DEAD_LETTER 処理あり
</details>
