# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/user-management-design.md`
- **判定**: ❌ **Rejected** — 重大な不備あり
- **レビュー日時**: 2026-04-03
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

## 判定根拠
- Critical 指摘 12 件、High 指摘 14 件を検出
- spec.md で定義された UserManagementService の責務・エンティティ・Kafka 設計・GDPR ワークフローと設計書の内容が根本的に乖離
- 最も重大な指摘: 責務境界違反（AuthService の責務を UserManagementService が担当）、GDPR 対応設計の全面的欠落、spec.md 定義エンティティの大量欠落

---

## 指摘サマリー

| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| architect | ❌ Fail | 3 | 3 | 1 | 0 |
| security-reviewer | ❌ Fail | 2 | 2 | 1 | 0 |
| dba-reviewer | ❌ Fail | 3 | 3 | 2 | 0 |
| compliance-reviewer | ❌ Fail | 2 | 2 | 1 | 0 |
| programing-reviewer | ⚠️ Warn | 1 | 2 | 2 | 1 |
| business-analyst | ❌ Fail | 1 | 1 | 0 | 0 |
| qa-manager | ⚠️ Warn | 0 | 1 | 1 | 0 |
| **合計** | | **12** | **14** | **8** | **1** |

---

## Critical 指摘一覧（修正必須）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| C-01 | **Critical** | architect | 責務境界 | **AuthService 責務の侵害**: 設計書がユーザー登録（`POST /api/users`）、パスワード管理（`PUT /api/users/me/password`）、ロール管理（`POST /api/admin/roles` 等）を UserManagementService の責務として定義している。spec.md L497 は「認証・認可・JWT 発行・セッション管理・ロール管理は AuthService の責務」と明記。`Role`, `Permission`, `RolePermission` テーブルは AuthService の DB に所属すると定義されている | ユーザー登録・パスワード管理・ロール管理の全 API/Service/Repository を設計書から除去。AuthService からの `user.registered` Kafka イベント購読によるプロファイル初期化フローに書き換え |
| C-02 | **Critical** | architect | エンティティ欠落 | **spec.md 定義エンティティの大量欠落**: spec.md L2700 のユーザー管理サービスエンティティ一覧に定義された `Address`, `Wishlist`, `WishlistItem` が設計書に存在しない。ER 図・モデル定義・API エンドポイントのいずれにも含まれていない | spec.md のエンティティ定義（Address: addressType/recipient/zipCode/prefecture/city/streetAddress/building/phoneNumber/isDefault、Wishlist: name/isDefault、WishlistItem: productId/addedAt/notifyOnRestock/notifiedAt）に基づきエンティティ・ER 図・API を追加 |
| C-03 | **Critical** | architect | エンティティ欠落 | **MemberRank エンティティの欠落**: spec.md L2615-2630 で定義された `MemberRank` エンティティ（current_rank, annual_purchase_amount, previous_year_amount, rank_updated_at, next_evaluation_date, point_rate）と `MemberRankEvaluationService`（BackgroundService）が設計書に一切存在しない。会員ランク制度はビジネス要件の中核機能 | spec.md の会員ランク定義に基づき MemberRank エンティティ、昇格・降格ロジック、MemberRankEvaluationService（年次評価バッチ）、Advisory Lock 設計を追加 |
| C-04 | **Critical** | compliance-reviewer | GDPR | **GDPR/DSR ワークフローの全面的欠落**: 設計書の GDPR 対応は §6「個人データ削除リクエスト対応機能、データエクスポート機能、監査ログ記録」の 3 行のみ。spec.md L4489-4700 で定義された DSR ワークフロー（DeletionRequest エンティティ、14 日猶予期間、各サービス並行削除フロー、完了集約タイムアウト/リトライ設計、外部プロセッサー DSR 伝搬、匿名化 vs 仮名化の区別）が全て欠落 | spec.md の DSR 設計（DeletionRequest エンティティ・ステータス遷移・DsrTimeoutMonitorService・Kafka user.deleted イベント発行・user.deletion.completed 受信集約）を設計書に反映 |
| C-05 | **Critical** | compliance-reviewer | 同意管理 | **Consent 管理設計の全面的欠落**: spec.md L4700-4850 で定義された同意管理（Consent エンティティ、4 種別の同意タイプ、同意 API、未認証ユーザー Cookie 同意、同意撤回時の `consent.revoked` Kafka イベント、同意バージョン管理、処理制限権）が設計書に一切存在しない | Consent エンティティ（consent_type, is_granted, version, policy_text_hash, ip_address, user_agent）、同意 API（GET/PUT/DELETE /users/{userId}/consents）、anonymous_consents テーブル、consent.revoked イベント発行、ConsentVersion テーブルを追加 |
| C-06 | **Critical** | dba-reviewer | データモデル | **User エンティティに passwordHash カラムが存在**: 設計書の User エンティティ（§3 ER 図および §14 实装参考コード）に `password_hash` カラムが含まれている。spec.md は認証情報（passwordHash）を AuthService の `AuthUser` エンティティで管理すると定義。UserManagementService がパスワードハッシュを保持することはセキュリティリスクかつ責務境界違反 | User エンティティから `password_hash` カラムを除去。認証関連データは AuthService に委譲 |
| C-07 | **Critical** | dba-reviewer | データモデル | **User エンティティに roleId FK が存在**: 設計書の User エンティティに `roleId` FK と `Role` ナビゲーションプロパティが定義されている。spec.md は Role テーブルを AuthService の DB に所属させ、UserManagementService は読み取り専用ビューとしてのみ参照すると定義（ADR-0006 準拠）。サービス間 FK 制約は禁止 | User エンティティから `roleId` FK と `Role` ナビゲーションを除去。ロール情報が必要な場合は AuthService からの Kafka イベントでローカルキャッシュに同期 |
| C-08 | **Critical** | dba-reviewer | DB 設計 | **独立 DB 名の不一致**: 設計書 §11 環境変数で `Database=skishopdb` と記載。spec.md の Aspire 構成（L5656）では `postgres.AddDatabase("userdb")` と定義されており、ADR-0006（サービス別独立 DB）に基づき UserManagementService は専用の `userdb` を使用する。`skishopdb` は共有 DB 名であり ADR-0006 違反 | 接続文字列のデータベース名を `userdb` に変更 |
| C-09 | **Critical** | architect | Kafka 設計 | **Kafka イベント設計の根本的乖離**: spec.md の Kafka トピック一覧（L6038）では `user.registered` は AuthService が発行（購読先: UserManagementService, MailSendService, PointService）。設計書は UserManagementService が `user.created`, `user.login`, `user.password_changed` 等を発行する設計になっているが、login/password_changed は AuthService の責務。さらに spec.md で定義された発行トピック `user.deleted`, `user.profile-updated`, `consent.revoked`, `objection.approved` と購読トピック `user.registered`, `user.deletion.completed` が設計書に欠落 | Kafka イベントを spec.md L6038 のトピック一覧に完全準拠させる。発行: `user.deleted`, `user.profile-updated`, `consent.revoked`。購読: `user.registered`（AuthService から）, `user.deletion.completed`（各サービスから）。`user.created`, `user.login`, `user.password_changed` を除去 |
| C-10 | **Critical** | security-reviewer | Outbox パターン | **Outbox パターンの欠落**: ADR-0005 で全マイクロサービスに Outbox パターンの適用が決定されているが、設計書に `outbox_events` テーブル、`OutboxPublisher` BackgroundService、Outbox を使用したイベント発行パターンが一切記載されていない。直接 Kafka に発行する設計（`eventPublisher.PublishUserCreatedAsync`）は ADR-0005 違反であり、DB コミットとイベント発行の原子性が保証されない | outbox_events テーブル定義、OutboxPublisher BackgroundService（Advisory Lock + 動的バックオフ 100ms〜5s）、OutboxEvent エンティティ（PENDING/PUBLISHED/FAILED ステータス、snake_case カラム名）を追加。全イベント発行を Outbox 経由に変更 |
| C-11 | **Critical** | security-reviewer | エラーレスポンス | **RFC 9457 Problem Details 非準拠**: 設計書 §7 のエラーレスポンス形式は独自 JSON フォーマット（`timestamp`, `error`, `code`, `details` 等）を使用。AGENTS.md §4.7 および api-design.instructions.md §2 は RFC 9457 Problem Details 形式の必須化を規定。`TypedResults.Problem()` の使用が必須 | エラーレスポンスを RFC 9457 Problem Details 形式（`type`, `title`, `status`, `detail`, `instance`）に変更。§4.7 のグローバル例外ハンドラーパターンを適用 |
| C-12 | **Critical** | business-analyst | データモデル | **処理制限権（GDPR 第 18 条）の欠落**: spec.md L4850+ で定義された処理制限設計（`processing_restricted` フラグ、`restriction_reason`、制限中のデータ処理ルール、API エンドポイント）が設計書に存在しない。User エンティティに処理制限関連カラムが未定義 | User エンティティに `processing_restricted`, `restriction_reason`, `restricted_at`, `restriction_requested_by` カラムを追加。処理制限 API（POST/DELETE/GET `/api/v1/users/{userId}/processing-restriction`）を追加。サービス層での制限チェックロジックを設計 |

---

## High 指摘一覧（修正強く推奨）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| H-01 | **High** | architect | BackgroundService | **MemberRankEvaluationService の Advisory Lock 設計欠落**: spec.md L990 で `MemberRankEvaluationService` に PostgreSQL Advisory Lock（`hashtext('member_rank_eval')`）が必須と定義。`minReplicas: 2` 以上で重複実行（二重降格判定）を防止するための排他制御が未設計 | Advisory Lock 取得（`pg_try_advisory_lock(hashtext('member_rank_eval'))`）のコード例と、ロック取得失敗時のスキップロジックを追加 |
| H-02 | **High** | architect | BackgroundService | **DsrTimeoutMonitorService の欠落**: spec.md DSR 設計で定義された `DsrTimeoutMonitorService`（BackgroundService、1 時間ごとにポーリング、タイムアウト検出・リトライ実行、最大 3 回リトライで `AWAITING_MANUAL_INTERVENTION` 遷移）が設計書に存在しない | DsrTimeoutMonitorService の設計を追加（ポーリング間隔、リトライロジック、アラート通知） |
| H-03 | **High** | architect | Aspire | **Redis 参照の不一致**: 設計書は Redis を依存関係としてリストし、NuGet パッケージ（StackExchange.Redis）、キャッシュ戦略、Redis 接続環境変数を定義している。しかし spec.md の Aspire 構成（L5656）では `userService` に `.WithReference(redis)` が含まれていない（`userDb` と `kafka` のみ）。Redis の使用有無を明確化する必要がある | spec.md の Aspire 構成を確認し、Redis が必要な場合は spec.md 側に `.WithReference(redis)` を追加。不要な場合は設計書から Redis 関連の記述を除去 |
| H-04 | **High** | dba-reviewer | CHECK 制約 | **CHECK 制約の未定義**: spec.md L2946-2949 で `member_ranks`（current_rank, annual_purchase_amount, point_rate）および `consents`（consent_type, status）、`deletion_requests`（status, request_channel）の CHECK 制約が定義されているが、設計書にこれらテーブル自体が存在せず、既存テーブルにも CHECK 制約の記載がない | 全テーブルに spec.md で定義された CHECK 制約を反映。特に status カラムの値域制約は DB 層で防御必須（sql-schema-review.instructions.md §3 準拠） |
| H-05 | **High** | dba-reviewer | 日時型 | **TIMESTAMP WITH TIME ZONE 未使用**: 設計書の ER 図および EF Core エンティティで `Timestamp` / `DateTime` 型を使用。sql-schema-review.instructions.md §2 は全日時カラムに `TIMESTAMP WITH TIME ZONE` を必須としている。EF Core エンティティでも `DateTime` ではなく `DateTimeOffset` を使用し、`[Column("created_at", TypeName = "timestamp with time zone")]` でマッピングすべき | 全日時カラムを `TIMESTAMP WITH TIME ZONE` に変更。EF Core エンティティの `DateTime` を `DateTimeOffset` に変更。`DateTime.UtcNow` → `DateTimeOffset.UtcNow` に統一 |
| H-06 | **High** | dba-reviewer | FK 制約 | **FK 制約の ON DELETE/ON UPDATE 未定義**: spec.md L2820-2830 で UserManagementService の FK 制約（UserPreference→User: CASCADE、Address→User: CASCADE、UserActivity→User: CASCADE）が定義されているが、設計書に FK 制約の詳細が未記載 | spec.md の FK 制約設計テーブルを設計書に反映。特に CASCADE 使用箇所のコメント記載（sql-schema-review.instructions.md §3 準拠） |
| H-07 | **High** | security-reviewer | IDOR | **IDOR 防止設計の欠落**: 設計書の Endpoint 実装例（§14）で `GetUserById` は ID パラメータのみで取得しており、ログインユーザーの ID との照合（オーナーシップ検証）が行われていない。AGENTS.md §5.6 は IDOR 防止を必須としている | 全ユーザー固有リソースの API で `ClaimsPrincipal` から `NameIdentifier` を取得し、リソースの所有者 ID と照合するパターンを追加 |
| H-08 | **High** | security-reviewer | PII ログ | **PII ログ出力**: 設計書 §14 のサービス実装例で `logger.LogInformation("ユーザーが正常に登録されました: {Email}", user.Email)` とメールアドレスをログ出力している。AGENTS.md §5.7 は Email のログ出力を禁止。`DeleteUserAsync` でも `user.Email` を参照しているが、削除時のイベントペイロードにもメールアドレスが含まれる可能性がある | PII（Email, PhoneNumber, Address 等）のログ出力をマスキングまたは除去。`logger.LogInformation("ユーザーが正常に登録されました: {UserId}", user.Id)` に変更 |
| H-09 | **High** | programing-reviewer | NuGet | **テストライブラリの不一致**: 設計書 §2 および §10 で Moq を使用と記載。AGENTS.md §9.1 および NuGet 依存関係管理規約（nuget-dependency.instructions.md）は NSubstitute (5.*) を指定。Moq は禁止パッケージではないが、プロジェクト規約で NSubstitute が標準 | Moq → NSubstitute に変更。Shouldly をアサーションライブラリとして追加 |
| H-10 | **High** | programing-reviewer | 命名規約 | **シーケンス図のフロー誤り（ユーザー登録）**: 設計書 §3 のシーケンス図でユーザー登録リクエストが UserManagementService に直接送信され、JWT トークン生成に言及している。spec.md はユーザー登録を AuthService の責務とし、AuthService → Kafka「UserRegistered」→ UserManagementService（プロファイル初期化）のフローを定義 | シーケンス図を AuthService → `user.registered` Kafka イベント → UserManagementService（プロファイル初期化）のフローに書き換え |
| H-11 | **High** | business-analyst | データポータビリティ | **データポータビリティ権（GDPR 第 20 条）の欠落**: spec.md でデータエクスポート API（`POST /api/v1/users/{userId}/data-export`、BackgroundService による非同期処理、48 時間有効の署名付き URL）が定義されているが、設計書に未反映 | データエクスポート API、DataExportService（BackgroundService）、エクスポート対象データ定義を追加 |
| H-12 | **High** | qa-manager | テスト戦略 | **MemberRank 関連テスト戦略の欠落**: MemberRank エンティティ自体が設計書に存在しないため、昇格・降格ロジック、年次バッチ処理、Advisory Lock の排他制御に関するテスト戦略が定義されていない | MemberRankEvaluationService のテスト戦略を追加（正常昇格、降格1ランクのみ、プラチナ降格猶予、並行実行時の Advisory Lock 検証） |
| H-13 | **High** | architect | API バージョニング | **API バージョニングの欠落**: 設計書の API パスは `/api/users` 形式でバージョン情報を含んでいない。spec.md の API 設計では `/api/v1/` バージョンプレフィックスが使用されている（DSR API: `/api/v1/users/{userId}/data-export`） | 全 API パスに `/api/v1/` プレフィックスを追加。Minimal API の `MapGroup("/api/v1/users")` に変更 |
| H-14 | **High** | programing-reviewer | コード品質 | **UserService の RegisterUserAsync が Aggregate Root 原則に違反**: 設計書 §14 のサービス実装例で `RegisterUserAsync` が `roleRepository.FindByNameAsync` を呼び出しており、異なる Aggregate（Role は AuthService 管轄）の Repository を直接参照している。AGENTS.md §3.4 は Repository は Aggregate Root 単位で定義し、異なる Aggregate のクエリを混在させないことを規定 | RegisterUserAsync から roleRepository 参照を除去。AuthService からの Kafka イベントでロール情報を取得する設計に変更 |

---

## Medium 指摘一覧（改善推奨）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| M-01 | Medium | dba-reviewer | インデックス | **spec.md 定義インデックスの未反映**: spec.md L3043 で定義されたユーザー管理サービスのインデックス（`addresses.(user_id, is_default)`, `user_preferences.user_id` UNIQUE, `user_activities.(user_id, activity_type)` 等）が設計書の §8 パフォーマンス最適化に十分反映されていない | spec.md のインデックス設計テーブルを設計書に完全転記 |
| M-02 | Medium | dba-reviewer | 監査カラム | **created_by / updated_by 監査カラムの検討不足**: sql-schema-review.instructions.md §2 は `created_by` / `updated_by` を「必要に応じて」としているが、GDPR 要件（誰がデータを変更したかのトレーサビリティ）を考慮すると、User, Address, Consent 等の PII テーブルには監査列の追加が推奨される | PII を含むテーブルに `created_by`, `updated_by` カラムの追加を検討 |
| M-03 | Medium | programing-reviewer | コード品質 | **UserPreference エンティティの設計不一致**: 設計書 ER 図の UserPreference は `prefKey`/`prefValue`/`prefType` の Key-Value ストア型だが、spec.md のエンティティ定義は `language`, `currency`, `notificationPreferences`, `displayPreferences` の構造化型。設計を統一する必要がある | spec.md の UserPreference 構造に合わせて、Key-Value 型から構造化型に変更 |
| M-04 | Medium | security-reviewer | セキュリティ | **JWT Secret のハードコード環境変数名**: 設計書 §11 で `Jwt__Secret` 環境変数を定義。AGENTS.md で Azure Key Vault の使用が規定されており、JWT 署名鍵は Key Vault から取得すべき | `Jwt__Secret` 環境変数は開発用に限定する旨を明記。本番環境では Key Vault 参照（`Azure.Identity` + `KeyVault.Secrets`）を使用する設計に変更 |
| M-05 | Medium | compliance-reviewer | 同意管理 | **匿名同意テーブルの欠落**: spec.md で定義された `anonymous_consents` テーブル（未認証ユーザーの Cookie 同意管理）が設計書に未反映 | anonymous_consents テーブル（consent_id, consent_type, is_granted, created_at）とユーザー登録時のマージロジックを追加 |
| M-06 | Medium | programing-reviewer | Dockerfile | **Dockerfile EXPOSE ポート不一致**: Dockerfile で `EXPOSE 5002` を設定しているが、本番環境では Azure Container Apps のポート設定に合わせるべき。AGENTS.md §12.5 Dockerfile 規約では `EXPOSE 8080` が推奨パターン | EXPOSE ポートを 8080 に変更し、環境変数 `ASPNETCORE_URLS=http://+:8080` を設定 |
| M-07 | Medium | qa-manager | テスト | **テストカテゴリの未定義**: テスト実行コマンドで `Category=Unit` / `Category=Integration` フィルタを使用しているが、テストクラスへのカテゴリ属性（`[Trait("Category", "Unit")]`）の付与方針が未定義 | テストカテゴリの付与ルール（Unit/Integration/Repository/Security）を明記 |
| M-08 | Medium | programing-reviewer | コード品質 | **DeleteUserAsync のソフトデリート実装**: 設計書 §14 の `DeleteUserAsync` はメールアドレスに `.deleted.{timestamp}` を付加するソフトデリート方式だが、spec.md の DSR ワークフローは 14 日猶予期間後の物理削除を規定。ソフトデリート（ステータス変更）と DSR（物理削除/仮名化）は別フローとして区別すべき | アカウント無効化（ソフトデリート: ステータス→DEACTIVATED）と DSR 削除（物理削除: DeletionRequest フロー）を明確に分離した設計に変更 |

---

## Low 指摘一覧（時間がある時に対応）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| L-01 | Low | programing-reviewer | 文書品質 | **NuGet パッケージ一覧の欠落**: Polly / Microsoft.Extensions.Http.Resilience が NuGet パッケージ一覧に含まれていない。AGENTS.md §11.1 は外部 HTTP 通信に Polly を必須としている | NuGet パッケージ一覧に Polly 8.* / Microsoft.Extensions.Http.Resilience 9.* を追加 |

---

## ドキュメント横断分析

### spec.md とのエンティティ対応

| spec.md 定義エンティティ | 設計書に存在 | 備考 |
|------------------------|------------|------|
| User | ✅ | ただし passwordHash, roleId を除去する必要あり |
| UserPreference | ✅ | スキーマ形式が不一致（Key-Value vs 構造化） |
| UserActivity | ✅ | — |
| Address | ❌ | **欠落**: addressType, recipient, zipCode, prefecture, city, streetAddress, building, phoneNumber, isDefault |
| Wishlist | ❌ | **欠落**: name, isDefault |
| WishlistItem | ❌ | **欠落**: productId, addedAt, notifyOnRestock, notifiedAt |
| MemberRank | ❌ | **欠落**: current_rank, annual_purchase_amount, previous_year_amount, rank_updated_at, next_evaluation_date, point_rate |
| Consent | ❌ | **欠落**: consent_type, is_granted, version, policy_text_hash, ip_address, user_agent |
| DeletionRequest | ❌ | **欠落**: DSR ワークフロー全体 |
| Role (読取専用ビュー) | ❌（設計書は FK 直接参照） | AuthService 管轄。Kafka 同期の読取専用ビューとして再設計必要 |
| Permission (読取専用ビュー) | ❌（同上） | 同上 |

### Kafka トピック対応

| トピック | spec.md の定義 | 設計書の定義 | 整合性 |
|---------|--------------|------------|--------|
| `user.registered` | AuthService → UserManagement（購読） | 未定義（設計書は user.created を発行） | ❌ 不一致 |
| `user.deleted` | UserManagement → 全サービス（発行） | user.deleted（発行のみ記載） | ⚠️ 部分一致（DSR フロー未記載） |
| `user.profile-updated` | UserManagement → AiSupportService（発行） | user.updated（名称異なる） | ❌ 不一致 |
| `consent.revoked` | UserManagement → Mail, AI, Auth（発行） | 未定義 | ❌ 欠落 |
| `user.deletion.completed` | 各サービス → UserManagement（購読） | 未定義 | ❌ 欠落 |
| `order.completed` | 定義なし | UserManagement（購読として設計書に記載） | ❌ spec.md に存在しないトピック |
| `payment.completed` | PaymentCart → Sales, Mail（発行） | UserManagement（購読として設計書に記載） | ❌ UserManagement は購読先に含まれない |
| `product.viewed` | 定義なし | UserManagement（購読として設計書に記載） | ❌ spec.md に存在しないトピック |
| `user.login` | 定義なし | UserManagement（発行として設計書に記載） | ❌ AuthService の責務 |
| `user.password_changed` | 定義なし（PasswordChanged は AuthService→UserManagement） | UserManagement（発行として設計書に記載） | ❌ AuthService が発行元 |
| `user.role_changed` | 定義なし | UserManagement（発行として設計書に記載） | ❌ AuthService の責務 |

### BackgroundService 対応

| BackgroundService | spec.md 定義 | 設計書に存在 | 排他制御 |
|------------------|------------|------------|---------|
| OutboxPublisher | 必須（ADR-0005） | ❌ 欠落 | Advisory Lock(`hashtext('outbox_publisher')`) |
| MemberRankEvaluationService | 必須（年次バッチ） | ❌ 欠落 | Advisory Lock(`hashtext('member_rank_eval')`) |
| DsrTimeoutMonitorService | 必須（DSR タイムアウト監視） | ❌ 欠落 | — |
| DataExportService | 必須（GDPR 第 20 条） | ❌ 欠落 | — |

---

## 修正優先度ガイド

### 第 1 優先（責務境界・データモデル修正）
1. AuthService 責務の除去（C-01, C-06, C-07, H-14）
2. passwordHash / roleId の User エンティティからの除去
3. ユーザー登録フローの `user.registered` 購読パターンへの書き換え
4. DB 名を `userdb` に変更（C-08）

### 第 2 優先（欠落エンティティ追加）
5. Address エンティティの追加（C-02）
6. MemberRank エンティティ + MemberRankEvaluationService の追加（C-03）
7. Consent エンティティ + 同意 API の追加（C-05）
8. DeletionRequest エンティティ + DSR ワークフローの追加（C-04）
9. Wishlist / WishlistItem エンティティの追加（C-02）
10. 処理制限権カラムの追加（C-12）

### 第 3 優先（Kafka・Outbox・BackgroundService）
11. Kafka イベント設計の全面書き換え（C-09）
12. Outbox パターンの導入（C-10）
13. BackgroundService の追加（H-01, H-02）

### 第 4 優先（規約準拠）
14. RFC 9457 Problem Details への変更（C-11）
15. CHECK 制約の追加（H-04）
16. TIMESTAMP WITH TIME ZONE への変更（H-05）
17. IDOR 防止パターンの追加（H-07）
18. PII ログ除去（H-08）
