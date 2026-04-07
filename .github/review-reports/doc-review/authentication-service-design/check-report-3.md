# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/authentication-service-design.md`
- **判定**: ⚠️ **Conditional Approval** — Critical 指摘なし、High 指摘あり。人間の判断を介在
- **レビュー日時**: 2026-04-03
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)
- **レポート番号**: check-report-3

## 技術スタック検証結果
| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| ランタイム | .NET 10 (LTS) | .NET 10 (LTS) | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| ORM | EF Core 10 | EF Core 10 | ✅ |
| DB | PostgreSQL (authdb) | PostgreSQL | ✅ |
| キャッシュ | Redis (StackExchange.Redis 2.*) | Redis (StackExchange.Redis 2.*) | ✅ |
| メッセージング | Confluent.Kafka 2.* | Confluent.Kafka 2.* | ✅ |
| 認証 | Microsoft.Identity.Web 3.* / JWT Bearer 10.* | 同一 | ✅ |
| ビルドツール | dotnet CLI / MSBuild | 同一 | ✅ |
| コンテナ化 | Docker 25.x | Docker 25.x | ✅ |
| オーケストレーション | .NET Aspire 13.1 | .NET Aspire 13.1 | ✅ |
| ロギング | Serilog.AspNetCore 8.* | 同一 | ✅ |
| 耐障害性 | Polly 8.* / Microsoft.Extensions.Http.Resilience 9.* | 同一 | ✅ |
| テスト | xUnit / NSubstitute / Shouldly / Testcontainers | 同一 | ✅ |

## 指摘サマリー
| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ⚠️ | 0 | 1 | 2 | 1 |
| architect | ⚠️ | 0 | 3 | 2 | 0 |
| tech-lead | ⚠️ | 0 | 2 | 1 | 1 |
| programing-reviewer | ⚠️ | 0 | 2 | 3 | 1 |
| security-reviewer | ⚠️ | 0 | 3 | 2 | 0 |
| dba-reviewer | ⚠️ | 0 | 2 | 2 | 1 |
| qa-manager | ✅ | 0 | 0 | 2 | 1 |
| performance-reviewer | ✅ | 0 | 0 | 1 | 1 |
| compliance-reviewer | ⚠️ | 0 | 2 | 1 | 0 |
| oss-reviewer | ✅ | 0 | 0 | 1 | 0 |
| release-manager | ✅ | 0 | 0 | 1 | 1 |
| infra-ops-reviewer | ✅ | 0 | 0 | 1 | 1 |
| audit-reviewer | ⚠️ | 0 | 1 | 1 | 0 |
| ux-accessibility-reviewer | ✅ | 0 | 0 | 0 | 1 |
| **合計** | | **0** | **16** | **20** | **9** |

## 判定根拠
- 判定ルール適用結果: Critical 指摘 0 件、High 指摘 16 件。High のみのため ⚠️ Conditional Approval
- 最も重大な指摘: spec.md の Aggregate Root 定義・リフレッシュトークンローテーション設計・ロールモデルとの不整合

---

## Critical/High 指摘一覧（修正必須）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| H-01 | **High** | architect, tech-lead | Aggregate Root 不整合 | spec.md では認証サービスの Aggregate Root を `AuthUser`（子: UserCredential, LoginAttempt, SecurityLog）と `OAuthClient`（子: OAuthToken, OAuthConsent）と定義しているが、設計書のエンティティモデルは `User` を中心に構成されており、`AuthUser`, `OAuthClient`, `OAuthToken`, `OAuthScope`, `OAuthConsent`, `UserCredential`, `LoginAttempt` が存在しない。DDD の Aggregate Root 境界が spec.md と一致していない | spec.md の Aggregate Root 定義に合わせて `AuthUser` を正式な Aggregate Root として Position 付け、`OAuthClient` 系エンティティを追加するか、spec.md 側を更新して整合を取る |
| H-02 | **High** | architect, security-reviewer | リフレッシュトークンローテーション設計の不一致 | spec.md §認証・認可で `family_id`（UUID）、`previous_token_id` によるファミリー検出・Replay Detection、絶対有効期限 90 日を明確に定義しているが、設計書の `RefreshToken` エンティティには `family_id` / `previous_token_id` がなく、`replaced_by_token` のみ。ファミリーベースの一括無効化ロジックが実装不可能 | `RefreshToken` エンティティに `family_id`（UUID）、`previous_token_id`（FK → self）、`absolute_expiry`（ファミリー作成時から 90 日）を追加し、spec.md の Replay Detection 設計に準拠させる |
| H-03 | **High** | architect, tech-lead | リフレッシュトークン有効期限の矛盾 | spec.md §認証・認可では「リフレッシュトークン: 長期（14日）」と記載。設計書 §27 では `Jwt:RefreshExpirationSeconds=604800`（7 日間）と定義しており、2 倍の差異がある | どちらかに統一する。SSOT（spec.md）の 14 日に合わせるか、spec.md を 7 日に修正する |
| H-04 | **High** | architect | ロールモデルの不一致 | spec.md §認可モデルのロール定義は `Customer`, `PremiumCustomer`, `StoreAdmin`, `InventoryManager`, `SalesManager`, `SystemAdmin` だが、設計書は `ADMIN`, `MANAGER`, `STAFF`, `EMPLOYEE`, `USER`, `CUSTOMER` と全く異なるロール名を使用。CHECK 制約やシードデータも設計書独自のロール名で定義済み | spec.md のロール定義をSSOTとして設計書のロール名を統一するか、双方でマッピングテーブルを定義する |
| H-05 | **High** | security-reviewer | パスワード履歴テーブルの未定義 | §27 のパスワードポリシーで「直近 5 回分のパスワードと重複不可」と記載されているが、`password_histories` テーブルが DB スキーマ / エンティティ定義のいずれにも存在せず、実装に必要なデータストアが未定義 | `password_histories` テーブル（`id`, `user_id` FK, `password_hash`, `created_at`）を追加し、`IPasswordService.ChangePasswordAsync` の仕様に履歴チェックロジックを明記する |
| H-06 | **High** | security-reviewer | メール検証フローの未定義 | `users.email_verified` カラムと `PENDING_VERIFICATION` ステータスが定義されているが、メール検証のエンドポイント（`POST /api/v1/auth/email/verify`）、検証トークンの生成・検証ロジック、検証完了後のステータス遷移が設計書に記載されていない | メール検証用のエンドポイント・トークンテーブル（または `password_resets` を流用）・ステータス遷移図を追加する |
| H-07 | **High** | dba-reviewer, programing-reviewer | OAuthAccount エンティティとスキーマの不一致 | DB スキーマ（`oauth_accounts` テーブル）では `profile_data JSONB` のみ定義されているが、C# エンティティの `OAuthAccount` クラスには `AccessToken`, `RefreshToken`, `TokenExpiresAt` プロパティが追加されている。スキーマにないカラムが存在し、EF Core マイグレーションでスキーマエラーとなる | DB スキーマ側に `access_token`, `refresh_token`, `token_expires_at` カラムを追加するか、C# エンティティから当該プロパティを削除し spec.md の `OAuthToken` エンティティで管理する設計に統一する |
| H-08 | **High** | dba-reviewer | UserRole エンティティとスキーマのカラム不一致 | `user_roles` テーブルスキーマでは `expires_at` カラムが定義されているが、C# `UserRole` エンティティには `AssignedAt`, `AssignedBy` プロパティがあり `ExpiresAt` がない。逆にスキーマには `assigned_at`, `assigned_by` がない | エンティティとスキーマを統一する。両方のカラム（`expires_at`, `assigned_at`, `assigned_by`）をスキーマに含め、エンティティにも対応プロパティを追加する |
| H-09 | **High** | compliance-reviewer | DSR（データ主体リクエスト）対応の未記載 | spec.md に詳細な DSR ワークフロー設計（14 日猶予期間、各サービスの削除対象、仮名化方針）が定義されているが、設計書では `DELETE /api/v1/auth/users/{userId}` の論理/物理削除のみ記載。AuthService が DSR イベント（`user.deleted`）を受信した際の処理（`oauth_tokens` 物理削除、`security_logs` 仮名化 1 年保持）が記載されていない | spec.md の DSR ワークフロー §各サービスの削除対象データに記載された AuthService の責務を設計書に反映。`security_logs` の仮名化保持ポリシーとイベントハンドラーを追加する |
| H-10 | **High** | compliance-reviewer | security_logs の PII 保持期間未定義 | `security_logs` テーブルに `ip_address`、`user_agent` が記録されるが、GDPR 上これらは個人データに該当し得る。保持期間・匿名化ポリシーが設計書に未記載 | `security_logs` の保持期間（推奨: 1 年後に `ip_address` を匿名化）と、DSR 時の `user_id` SET NULL 後の `ip_address` 匿名化ポリシーを明記する |
| H-11 | **High** | security-reviewer | Client Credentials エンドポイントの未定義 | spec.md で Client Credentials フロー（サービス間 M2M トークン取得）が定義され、スコープ定義テーブルまで存在するが、設計書に Client Credentials Grant 用のエンドポイント（`POST /api/v1/auth/token` grant_type=client_credentials）が定義されていない | Client Credentials Grant 用の `/api/v1/auth/token` エンドポイントと、`OAuthClient` エンティティ（clientId, clientSecret, allowedScopes）を設計書に追加する |
| H-12 | **High** | programing-reviewer | DateTime vs DateTimeOffset の不統一 | AGENTS.md §10.3 で `DateTime.UtcNow` の使用を許可しているが、EF Core エンティティの全 `DateTime` プロパティが `DateTimeOffset` ではなく `DateTime` を使用。PostgreSQL の `TIMESTAMP WITH TIME ZONE` カラムとの対応で、`DateTime.UtcNow` の `.Kind == DateTimeKind.Utc` が保証されない場面でタイムゾーン不整合が発生する可能性がある | EF Core エンティティのタイムスタンプ系プロパティを `DateTimeOffset` に統一するか、Npgsql の `AppContext` 設定（`Npgsql.EnableLegacyTimestampBehavior`）を明記する |
| H-13 | **High** | programing-reviewer | Value Object 未使用 | spec.md で `EmailAddress` Value Object が「ユーザー管理, 認証」サービスで使用と定義されているが、設計書の全コード例で `string Email` がそのまま使用されており、ドメインモデルとしての形式検証が Value Object に内包されていない | `EmailAddress` Value Object を DTO / エンティティに適用するか、spec.md の Value Object 一覧から認証サービスを除外する |
| H-14 | **High** | business-analyst | ユーザー登録フローの責務境界が曖昧 | spec.md のシーケンス図ではユーザー登録は UserManagementService が処理し、JWT 発行は AuthService が `UserRegistered` イベントを購読して行うと記載。しかし設計書では `POST /api/v1/auth/users` で AuthService が直接ユーザー登録を行っており、UserManagementService との責務分担が spec.md と矛盾 | ユーザー登録の主体が AuthService なのか UserManagementService なのかを明確化。spec.md のシーケンス図と整合させる |
| H-15 | **High** | audit-reviewer | AuditLog 統合の未記載 | spec.md に AuditLog エンティティ・SaveChanges インターセプター・ハッシュチェーン・改ざん防止策が詳細に定義されているが、設計書に AuditLog への言及がない。AuthService の `Program.cs` 統合ビュー（§24）にも `AuditLogInterceptor` の登録がない | spec.md の AuditLog 設計に基づき、AuthService の DbContext に `AuditLogInterceptor` を登録し、監査対象操作（ログイン成功/失敗、ロール変更）を明記する |
| H-16 | **High** | architect | spec.md エンティティとの大幅な乖離 | spec.md の認証サービスエンティティ一覧（`AuthUser`, `UserCredential`, `OAuthClient`, `OAuthToken`, `OAuthScope`, `OAuthConsent`, `MfaMethod`, `LoginAttempt`）と、設計書のエンティティ（`User`, `UserSession`, `OAuthAccount`, `SecurityLog`, `UserRole`, `PasswordReset`, `UserMfa`, `Role`, `OutboxEvent`, `RefreshToken`）が根本的に異なる。設計意図の不明確さが実装時の混乱を招く | spec.md のエンティティ定義か設計書のエンティティ定義のどちらかに統一し、SSOT を明確にする。設計書側が最新であれば spec.md を更新する |

---

## Medium 指摘一覧

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| M-01 | Medium | programing-reviewer | MFA 方式の限定 | spec.md では SMS、Email、TOTP、Push の 4 方式を定義しているが、設計書は TOTP のみ対応。`user_mfa.mfa_type` の CHECK 制約に他の方式が含まれておらず、将来の拡張設計が不足 | Phase 1 で TOTP のみ対応であることを明記し、`mfa_type` CHECK 制約に将来の値（`SMS`, `EMAIL`, `PUSH`）を含める or Phase 2 のマイグレーション計画を記載する |
| M-02 | Medium | security-reviewer | OAuth state パラメータの検証ロジック未定義 | `OAuthEndpoints.StartOAuthFlowAsync` で `state = Guid.NewGuid().ToString()` を生成しているが、この state を Redis/Cookie に保存しコールバック時に検証するロジックが設計書に記載されていない。CSRF 攻撃のリスクあり | state パラメータの保存先（Redis or 暗号化 Cookie）と、`HandleCallbackAsync` での state 検証ロジックを明記する |
| M-03 | Medium | security-reviewer | アカウントロック時間のハードコード | §27 で「自動ロック解除までの待機時間 30 分」と記載されているが、`FailedAttemptResetService` の `AutoUnlockAfter = TimeSpan.FromMinutes(30)` はハードコード。設定キー `Auth:AutoUnlockMinutes` が使われていない | `IOptions<AuthSecurityOptions>` から設定値を読み込むように変更する |
| M-04 | Medium | dba-reviewer | security_logs テーブルにパーティション設計なし | 認証イベントの高頻度な append-only テーブルであり、長期運用でテーブル肥大化が懸念される。spec.md の保持期間（1 年仮名化保持）に対するアーカイブ/パーティション戦略が未記載 | 時系列パーティショニング（月次）またはアーカイブ/削除の BackgroundService を設計する |
| M-05 | Medium | dba-reviewer | OutboxPublisher の Advisory Lock 未使用 | spec.md §4（販売管理サービス）で `OutboxPublisher` に PostgreSQL Advisory Lock を使用するリーダー選出パターンが定義されているが、設計書の `OutboxPublisher` 実装にはロック取得処理がない。`minReplicas: 2` 時にイベント二重発行のリスクあり | `pg_try_advisory_lock(hashtext('outbox_publisher'))` によるリーダー選出を追加する |
| M-06 | Medium | programing-reviewer | IHasTimestamps の不完全な実装 | §18 で `IHasTimestamps` マーカーインターフェースが定義されているが、`User`, `UserSession` 等のエンティティクラス定義（§12）に `IHasTimestamps` の implements が記載されていない | §12 のエンティティ定義に `: IHasTimestamps` を追記するか、補足セクション（§18）の記述と統合する |
| M-07 | Medium | qa-manager | 異常系テストケースの不足 | テストケース一覧に「同時セッション上限超過時のテスト」「パスワード履歴チェックのテスト」「並行リフレッシュトークン利用検出のテスト」が欠落。spec.md のビジネスルールに対応するテストが網羅されていない | spec.md の認証仕様（§27 のビジネスルール）に対応するテストケースを追加する |
| M-08 | Medium | qa-manager | サーキットブレーカー動作テストが「推奨」分類 | Azure AD / Graph API 障害時のサーキットブレーカー動作テストが「推奨」に分類されているが、外部 IdP 障害はミッションクリティカルなシナリオであり「必須」であるべき | テストケースの重要度を「必須」に変更する |
| M-09 | Medium | architect | Web エンドポイント（HTML 返却）の位置づけ不明確 | §API 設計の OAuth2 Web エンドポイントに `GET /`（ホームページ → HTML）、`GET /home`、`GET /profile` 等の HTML 返却エンドポイントが含まれている。Minimal API バックエンドサービスで HTML を返すのは一般的ではなく、フロントエンド（Next.js）との責務分担が不明確 | これらの HTML エンドポイントが開発時のデバッグ用か本番用かを明記。本番では Next.js フロントエンドが担当するため削除検討 |
| M-10 | Medium | oss-reviewer | Azure.Identity パッケージのバージョン未固定 | NuGet パッケージ一覧で `Azure.Identity` が「最新」と記載。AGENTS.md は `-preview` / `-beta` の使用禁止を規定しているが、`最新` 指定では GA 以外のバージョンが混入するリスクがある | `Azure.Identity` のバージョンを `1.*` 等の固定パターンに変更する |
| M-11 | Medium | release-manager | 環境変数にサンプルパスワードが残存 | §11 の環境変数セクションで `Password=${DB_PASSWORD_SECRET}` は環境変数参照だが、`Redis__ConnectionString=localhost:6379,password=${REDIS_PASSWORD_SECRET}` のような接続文字列パターンは、コピー&ペーストで `${...}` 部分が実際の値に置き換えられずにデプロイされるリスクがある | 環境変数参照は明確に「Azure Key Vault / dotnet user-secrets から取得」と注記。サンプル接続文字列を `<see Azure Key Vault>` に置換する |
| M-12 | Medium | infra-ops-reviewer | Docker HEALTHCHECK の curl/wget 不統一 | AGENTS.md の Dockerfile 規約では `curl -f http://localhost:8080/health` を使用しているが、設計書の Dockerfile では `wget --spider -q http://localhost:8080/health` を使用。統一されていない | AGENTS.md に合わせて `curl` に統一するか、`aspnet` ベースイメージに `curl` がない場合は `wget` を使う旨をコメントに記載する |
| M-13 | Medium | business-analyst | ソーシャルログイン対象の不一致 | spec.md では Google, Facebook, Apple, LINE を定義しているが、設計書の OAuth プロバイダー設計は Azure AD（Microsoft Entra ID）のみ実装。4 つのソーシャルログインプロバイダーの実装計画が記載されていない | Phase 1 のソーシャルログイン対応範囲を明記し、Google/Facebook/Apple/LINE の実装計画（Phase 2 以降か）を記載する |
| M-14 | Medium | business-analyst | ゲスト購入時の認証フロー未連携 | spec.md にゲスト購入フロー（認証不要 → 購入完了後の会員登録誘導）が詳細に設計されているが、設計書で AuthService がゲスト→会員変換時にどう関与するか（既存ゲスト注文の userId 紐付け等）が未記載 | ゲスト購入完了後の `POST /api/v1/auth/users` 呼び出し時に、同一メールアドレスのゲスト注文を紐付ける設計を追加する |
| M-15 | Medium | compliance-reviewer | 同意（Consent）管理との連携未記載 | spec.md で `consents` テーブル（`MARKETING`, `PERSONALIZATION`, `ANALYTICS`, `THIRD_PARTY_SHARING`）と `OAuthConsent` が定義されているが、AuthService の設計書に同意管理エンティティや Consent 関連エンドポイントがない | Consent 管理が UserManagementService の責務であればその旨を明記。AuthService の OAuth2 フローでの scope 同意（`OAuthConsent`）は本設計書に含めるべき |
| M-16 | Medium | architect | イベント購読の `PermissionsUpdated` 発行元の不整合 | §イベント設計で `PermissionsUpdated` の発行元を「ユーザー管理サービス」としているが、spec.md では Role/Permission は AuthService の責務と明記されている。AuthService が自身で管理する権限変更を、外部サービスから購読するのは矛盾 | `PermissionsUpdated` の発行元を AuthService 自身に変更するか、UserManagementService からのイベントで何を受信するかを再定義する |
| M-17 | Medium | performance-reviewer | Redis フェイルオーバー時の動作未定義 | Redis がセッションストレージ・レート制限・JWT ブラックリストに使用されているが、Redis 障害時のフォールバック戦略が未記載。Redis 障害 = 全認証が停止する SPOF リスク | Redis 接続不可時の DB フォールバック（セッション）、レート制限の一時無効化、JWT ブラックリストの DB 参照等のフォールバック戦略を追加する |
| M-18 | Medium | programing-reviewer | SecurityLog の Details カラムの型不一致 | C# エンティティで `Details` は `string?` と定義されているが、DB スキーマでは `JSONB` 型。`HasColumnType("jsonb")` の設定が SecurityLog の `OnModelCreating` に記載されているが、DTO 層での型安全性（`Dictionary<string, object>?` vs `string?`）が不足 | `Details` プロパティの型を `Dictionary<string, object>?` にするか、`JsonDocument?` に変更し、`HasConversion` で JSONB マッピングを明記する |
| M-19 | Medium | audit-reviewer | SecurityLog のイベント種別が曖昧 | `security_logs.event_type` が `VARCHAR(50)` で自由テキスト。§イベントテーブルの `details` JSONB のホワイトリストは定義されているが、`event_type` 自体の許容値一覧（CHECK 制約）がない。ログ分析時に種別の揺れが発生する | `event_type` の許容値を enum / CHECK 制約で定義する（例: `LOGIN_SUCCESS`, `LOGIN_FAILED`, `ACCOUNT_LOCKED`, `PASSWORD_CHANGED`, `MFA_ENABLED`, `TOKEN_REVOKED` 等） |
| M-20 | Medium | infra-ops-reviewer | BackgroundService の Advisory Lock ID 未登録 | spec.md で Advisory Lock ID の命名規約テーブルに `OutboxPublisher` のロック ID が定義されているが、AuthService 固有の BackgroundService（`TokenCleanupService`, `FailedAttemptResetService`, `SessionTimeoutService`）のロック ID が未登録 | AuthService の BackgroundService が `minReplicas: 2` で重複実行されるリスクを評価し、必要な場合は Advisory Lock ID をテーブルに追加する |

---

## Low 指摘一覧

| # | 出典 Agent | 指摘内容 |
|---|-----------|----------|
| L-01 | business-analyst | §17（まとめ）セクションの記述が開発ステータスの「✅ 完了」「🔄 進行中」を含んでおり、設計書としての適切さに欠ける。設計書は「何を作るか」の定義であり、実装進捗は別途管理すべき |
| L-02 | tech-lead | §4 の ER 図（Mermaid）に `PasswordReset`, `UserMFA` テーブルが含まれているが、`RefreshToken` テーブルが含まれていない。§19 で追加されたが ER 図が未更新 |
| L-03 | programing-reviewer | `UserMfa` エンティティで `SecretKey` の `[Required]` 属性が付与されているが、DB スキーマでは `secret_key VARCHAR(255) NULL` と NULL 許容。MFA 未セットアップ時に NOT NULL 制約違反が発生する |
| L-04 | dba-reviewer | `users.role` カラムと `user_roles` テーブルの二重管理。ユーザーに複数ロールを割り当てる場合の正規化が不十分（`users.role` は単一ロール、`user_roles` は多対多） |
| L-05 | performance-reviewer | ログインレスポンスタイム目標 < 200ms は Argon2id のコスト（通常 50-200ms）を考慮すると、DB アクセス + Redis アクセスの合計で達成困難な場合がある。Argon2id のパラメータ（メモリコスト、反復回数）を明記すべき |
| L-06 | release-manager | §11 の Docker EXPOSE が `8080` だが、§サービス情報テーブルでポートは `5001` と記載。開発環境と本番環境でポートが異なる旨の注記がない |
| L-07 | ux-accessibility-reviewer | OAuth2 Web エンドポイントが HTML を返却する設計の場合、WCAG 2.1 AA 準拠のアクセシビリティ要件が適用されるが、HTML テンプレートの設計が記載されていない |
| L-08 | tech-lead | `DEAD_LETTER` ステータスが `outbox_events` の CHECK 制約に含まれているが、Dead Letter 処理の運用手順（誰が・いつ・どのように対処するか）が設計書に記載されていない |
| L-09 | qa-manager | テストカバレッジ目標が §16 に記載されているが、テスト対象のクラス数・ファイル数の概算がなく、80% カバレッジ達成に必要な工数見積もりが困難 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-01 | 最優先 | architect, tech-lead | spec.md と設計書のエンティティモデルの根本的な乖離（H-01, H-16）。どちらを SSOT とするかの判断が必要。設計書側が最新の場合は spec.md の認証サービスエンティティ一覧を更新する必要がある | テックリード |
| E-02 | 最優先 | architect | ユーザー登録の責務所在（H-14）。AuthService が直接登録するか、UserManagementService が登録して AuthService がイベント経由で認証情報を作成するか。マイクロサービス間の責務境界に関わる設計判断 | テックリード + PO |
| E-03 | 高優先 | security-reviewer | リフレッシュトークン有効期限（H-03）の 7 日 vs 14 日の決定。セキュリティ（短期間）vs UX（長期間）のトレードオフ | セキュリティリード |
| E-04 | 高優先 | compliance-reviewer | `security_logs` の IP アドレス保持期間（H-10）。GDPR 上の個人データ該当性と、不正アクセス調査の必要性のバランス | 法務 + セキュリティリード |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| C-01 | architect（spec.md のエンティティモデルに統一すべき） | programing-reviewer（現設計書のエンティティが実装に近く合理的） | spec.md の `AuthUser`/`OAuthClient` モデルか、設計書の `User`/`OAuthAccount` モデルか | **エスカレーション** — 技術的影響範囲が大きいため、テックリードの判断を要する | 両モデルのトレードオフ（DDD 純粋性 vs 実装の簡潔さ）を比較した上で判断が必要 |

---

## ドキュメント横断分析

### マイクロサービス設計書カバレッジ

| 項目 | 記載状況 |
|------|---------|
| API 定義 | ✅ 全エンドポイント定義済み（認証、MFA、パスワード、ユーザー登録、OAuth2、ヘルスチェック） |
| DB 設計 | ⚠️ スキーマ定義あり。ただし spec.md との整合性に複数の問題あり |
| イベント定義 | ✅ 発行イベント 8 種、購読イベント 2 種が定義。PII 最小化原則に準拠 |
| セキュリティ | ⚠️ 認証方式・ロール設計・レート制限は記載。Client Credentials 未定義、DSR 未対応 |
| 非機能要件 | ✅ パフォーマンスメトリクス、キャッシュ戦略、可観測性、耐障害性が記載 |
| テスト戦略 | ✅ テスト種別・命名規約・主要テストケース・カバレッジ目標が記載 |
| EF Core エンティティ | ✅ 全エンティティの C# クラス定義が記載 |
| Service/Repository | ✅ インターフェース定義が記載 |
| Program.cs 統合 | ✅ ミドルウェアパイプライン順序が AGENTS.md §11.3 準拠 |
| Dockerfile | ✅ マルチステージビルド・非 root ユーザー・HEALTHCHECK が記載 |
| Outbox パターン | ✅ ADR-0005 準拠の OutboxPublisher 実装が記載 |

### サービス間整合性

| チェック項目 | 結果 |
|------------|------|
| UserManagementService との Kafka イベント整合性 | ⚠️ 発行側（`UserRegistered`）は整合。購読側（`UserDeleted`, `PermissionsUpdated`）の発行元に矛盾あり（M-16） |
| API Gateway との JWT 検証整合性 | ✅ RS256、`kid` による鍵ローテーション対応が spec.md と一致 |
| MailSendService との連携 | ⚠️ パスワードリセットメール、アカウント削除通知メールの Kafka イベント発行が設計書に未記載 |

### 未定義・曖昧な領域

| 項目 | 影響度 | 実装ブロッカーか |
|------|-------|--------------|
| spec.md エンティティモデルとの統一 | 高 | ⚠️ 設計書のエンティティで実装可能だが、spec.md との整合性を取らないと他サービスとの連携で問題が発生する可能性 |
| リフレッシュトークンのファミリー検出 | 高 | ✅ ブロッカー — 現エンティティでは Replay Detection が実装不可能 |
| Client Credentials エンドポイント | 高 | ✅ ブロッカー — サービス間認証の M2M トークン発行が不可能 |
| メール検証フロー | 中 | ⚠️ ユーザー登録後のメール検証が設計されておらず、`PENDING_VERIFICATION` → `ACTIVE` 遷移が実装不可能 |
| パスワード履歴テーブル | 中 | ⚠️ パスワード履歴チェックのビジネスルールが実装不可能 |

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### 評価: ⚠️ Conditional

**良い点:**
- EC サイト認証に必要な基本機能（ログイン、登録、パスワード管理、MFA、OAuth2）が網羅されている
- ロールベースアクセス制御の設計が明確
- アカウントロックアウトポリシーが定義されている

**指摘事項:**
- **H-14**: ユーザー登録フローの責務境界が spec.md と矛盾
- **M-13**: ソーシャルログイン対象の不一致（Azure AD のみ vs spec.md の 4 プロバイダー）
- **M-14**: ゲスト購入フローとの連携が未記載
- **L-01**: 設計書に実装進捗ステータスが混在

</details>

<details>
<summary>architect レビューレポート</summary>

### 評価: ⚠️ Conditional

**良い点:**
- レイヤードアーキテクチャ（Endpoints → Services → Repositories）が AGENTS.md 準拠
- Outbox パターンによるイベント発行が ADR-0005 準拠
- コンポーネントアーキテクチャ図・マイクロサービス関連図が明確

**指摘事項:**
- **H-01**: Aggregate Root 定義が spec.md と不整合（`User` vs `AuthUser`）
- **H-04**: ロールモデルが spec.md と完全に異なる
- **H-16**: エンティティモデル全体の乖離
- **M-09**: HTML 返却 Web エンドポイントの位置づけが不明確
- **M-16**: `PermissionsUpdated` イベントの発行元が矛盾

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 評価: ⚠️ Conditional

**良い点:**
- AGENTS.md の全規約（命名規則、DI、CancellationToken、構造化ログ）に十分に準拠
- Program.cs 統合ビューがミドルウェアパイプライン順序を厳守
- FluentValidation バリデーターが全 DTO に定義

**指摘事項:**
- **H-02**: リフレッシュトークンローテーション設計の spec.md との不一致（ファミリー検出の欠落）
- **H-03**: リフレッシュトークン有効期限の矛盾（7 日 vs 14 日）
- **M-XX**: 追記セクション（§18-28）の内容が既存セクション（§1-17）と重複・矛盾する箇所があり、統合整理が望ましい
- **L-02**: ER 図に RefreshToken テーブルが未追加
- **L-08**: DEAD_LETTER の運用手順未記載

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional

**良い点:**
- C# 14 の機能（record 型、primary constructor、コレクション式 `[]`）を適切に活用
- CancellationToken が全 async メソッドに正しく伝搬
- EF Core エンティティが AGENTS.md §10.3 準拠（`[Table]`, `[Column]`, snake_case）
- 例外クラス階層が AGENTS.md §4.7 準拠

**指摘事項:**
- **H-07**: OAuthAccount エンティティとスキーマの不一致
- **H-12**: DateTime vs DateTimeOffset の不統一
- **H-13**: Value Object（`EmailAddress`）の未使用
- **M-01**: MFA 方式が TOTP のみ
- **M-06**: IHasTimestamps の implements 記載漏れ
- **M-18**: SecurityLog.Details の型不一致（string vs JSONB）
- **L-03**: UserMfa.SecretKey の Required 属性とスキーマの NULL 許容の矛盾

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional

**良い点:**
- Argon2id によるパスワードハッシュ（IPasswordHasher カスタム実装）
- RS256（非対称鍵）による JWT 署名。Azure Key Vault での鍵管理
- MFA シークレットの AES-256-GCM 暗号化保存
- IDOR 防止パターンの適用（`ClaimsPrincipal` による userId 検証）
- タイミング攻撃防止（パスワードリセット時の統一レスポンス）
- レート制限設計（一般 + ログイン専用）
- セキュリティヘッダー（X-Content-Type-Options, X-Frame-Options, CSP）

**指摘事項:**
- **H-02**: リフレッシュトークンの Replay Detection 未実装
- **H-05**: パスワード履歴テーブル未定義
- **H-06**: メール検証フロー未定義
- **H-11**: Client Credentials エンドポイント未定義
- **M-02**: OAuth state パラメータの検証ロジック未記載
- **M-03**: セキュリティ設定値のハードコード

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional

**良い点:**
- テーブル命名が snake_case 複数形で統一
- インデックス設計が適切（メール検索、セッション検索、セキュリティログの時間ベースクエリ）
- Outbox テーブルの部分インデックス設計
- FK 制約の ON DELETE 動作が明確に定義
- CHECK 制約（ユーザーステータス、ロール名、Outbox ステータス）

**指摘事項:**
- **H-07**: OAuthAccount エンティティとスキーマの不一致
- **H-08**: UserRole エンティティとスキーマのカラム不一致
- **M-04**: security_logs テーブルのパーティション設計なし
- **M-05**: OutboxPublisher の Advisory Lock 未使用
- **L-04**: users.role と user_roles の二重管理

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 評価: ✅ Approved with Notes

**良い点:**
- テスト戦略が AGENTS.md §9 準拠（xUnit + NSubstitute + Shouldly + Testcontainers）
- AAA パターンのテスト例が具体的
- テストメソッド命名が `Should_期待結果_When_条件` パターン
- カバレッジ目標（80%）が層別に定義

**指摘事項:**
- **M-07**: 異常系テストケースの不足
- **M-08**: サーキットブレーカー動作テストの重要度が低い
- **L-09**: テスト工数見積もりの根拠不足

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 評価: ✅ Approved with Notes

**良い点:**
- パフォーマンスメトリクス目標値が明確（ログイン < 200ms、トークン検証 < 50ms）
- Redis キャッシュ戦略が TTL 付きで設計
- キャッシュキー設計が体系的
- OpenTelemetry によるメトリクス収集が設計済み

**指摘事項:**
- **M-17**: Redis フェイルオーバー時の動作未定義
- **L-05**: Argon2id パラメータ未記載による性能見積もり困難

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional

**良い点:**
- PII 最小化原則がイベントペイロードに適用（userId のみ）
- security_logs の details JSONB フィールドに PII 格納禁止が明記
- パスワードリセットトークンの安全な生成（`RandomNumberGenerator.GetBytes(64)`）

**指摘事項:**
- **H-09**: DSR（データ主体リクエスト）対応の未記載
- **H-10**: security_logs の PII 保持期間未定義
- **M-15**: 同意（Consent）管理との連携未記載

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 評価: ✅ Approved with Notes

**良い点:**
- 全パッケージが AGENTS.md の必須パッケージと一致
- 禁止パッケージ（`Newtonsoft.Json`, `log4net`, `EntityFramework 6` 等）が使用されていない
- バージョン指定がワイルドカード（`10.*`, `2.*`）で GA のみを対象

**指摘事項:**
- **M-10**: Azure.Identity のバージョンが「最新」で未固定

</details>

<details>
<summary>release-manager レビューレポート</summary>

### 評価: ✅ Approved with Notes

**良い点:**
- Dockerfile がマルチステージビルド・非 root ユーザー・HEALTHCHECK を含む
- 環境変数が環境ごとに分離

**指摘事項:**
- **M-11**: 環境変数セクションのサンプル接続文字列にリスク
- **L-06**: EXPOSE ポートとサービス情報ポートの不一致

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 評価: ✅ Approved with Notes

**良い点:**
- ヘルスチェック（Liveness + Readiness）が定義
- OpenTelemetry のトレーシング・メトリクスが設定
- Serilog の構造化ログ（CompactJsonFormatter）
- Correlation ID ミドルウェアが定義

**指摘事項:**
- **M-12**: Docker HEALTHCHECK の curl/wget 不統一
- **M-20**: BackgroundService の Advisory Lock ID 未登録
- **L-XX**: Kestrel の `AddServerHeader: false` 設定が appsettings.json に未記載

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional

**良い点:**
- SecurityLog エンティティが append-only 設計
- セキュリティイベントロギングの対象が網羅的（認証試行、トークン操作、アカウント状態変更）
- Correlation ID によるリクエスト追跡

**指摘事項:**
- **H-15**: AuditLog 統合の未記載（spec.md の AuditLog 設計が反映されていない）
- **M-19**: SecurityLog の event_type に CHECK 制約がない

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 評価: ✅ Approved with Notes

**良い点:**
- バックエンド API サービスとしての UX/アクセシビリティ要件は限定的
- エラーレスポンスが RFC 9457 準拠の Problem Details で構造化されている
- エラーコードが体系的（AUTH-XXXX）

**指摘事項:**
- **L-07**: HTML 返却 Web エンドポイントがある場合は WCAG 2.1 AA 準拠が必要

</details>
