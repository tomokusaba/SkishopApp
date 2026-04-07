# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/authentication-service-design.md`
- **判定**: ⚠️ **Conditional Approval** — Critical 指摘なし、High 指摘 4 件（全て修正起因の新規リグレッション）
- **レビュー日時**: 2026-04-03
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)
- **レポート番号**: check-report-4
- **イテレーション**: 4（check-report-3 の High 16 件修正後の再レビュー）

## 前回 High 指摘の修正確認結果

| # | 指摘内容 | 修正状況 | 修正箇所 | 新規リグレッション |
|---|---------|---------|---------|-----------------|
| H-01 | spec.md エンティティ名対応表 | ✅ 修正済 | §4 直後に詳細な対応表を追加。SSOT 方針を明記 | なし |
| H-02 | RefreshToken Replay Detection | ✅ 修正済 | §19 に `family_id`, `previous_token_id`, `absolute_expiry` を追加。§29.10 に Replay Detection ロジックを定義 | なし |
| H-03 | リフレッシュトークン有効期限 14日統一 | ✅ 修正済 | §11 環境変数 `Jwt__RefreshExpirationSeconds=1209600`（14日）、§27 にも 14日と明記 | なし |
| H-04 | ロールモデル対応表 | ✅ 修正済 | §4 直後に「ロールモデル対応表（spec.md との整合）」を追加。スコープベース認可の拡張方針も記載 | なし |
| H-05 | password_histories テーブル | ✅ 修正済 | §29.1 にスキーマ・エンティティ・Repository・ロジックを定義 | ⚠️ AuthDbContext 未統合（NEW H-02） |
| H-06 | メール検証エンドポイント | ✅ 修正済 | §29.2 にフロー・エンドポイント・`token_type` 拡張を定義 | ⚠️ Program.ps 未統合（NEW H-03） |
| H-07 | oauth_accounts カラム追加 | ✅ 修正済 | DB スキーマに `access_token`, `refresh_token`, `token_expires_at` を追加 | ⚠️ エンティティから `ProfileData` が脱落（NEW H-01） |
| H-08 | user_roles カラム追加 | ✅ 修正済 | DB スキーマ・エンティティ両方に `assigned_at`, `assigned_by`, `expires_at` を統合 | なし |
| H-09 | DSR イベントハンドラー | ✅ 修正済 | §29.3 に DSR 処理対象データ一覧・`UserDeletedEventHandler` 実装を定義 | なし |
| H-10 | security_logs PII 保持期間 | ✅ 修正済 | §29.4 に 3 フェーズ保持ポリシー・`SecurityLogAnonymizationService` を定義 | なし |
| H-11 | Client Credentials エンドポイント | ✅ 修正済 | §29.5 に `OAuthClient` エンティティ・スコープ定義・DTO・Endpoint・Service を定義 | ⚠️ AuthDbContext/Program.cs 未統合（NEW H-02, H-03） |
| H-12 | Npgsql DateTime 設定 | ✅ 修正済 | §29.6 に `AppContext.SetSwitch` 設定と根拠を明記 | なし |
| H-13 | EmailAddress Value Object Phase 2 計画 | ✅ 修正済 | §29.7 に Phase 1/Phase 2 の段階的導入方針を定義 | なし |
| H-14 | ユーザー登録の主体確定 | ✅ 修正済 | §29.8 に AuthService を主体とする方針・責務分担表・ゲスト→会員変換フローを定義 | なし |
| H-15 | AuditLogInterceptor | ✅ 修正済 | §29.9 に監査対象操作・Interceptor 登録・実装コードを定義 | ⚠️ AuthDbContext 未統合（NEW H-02） |
| H-16 | エンティティ対応表 | ✅ 修正済 | H-01 と同一の対応表で解決 | なし |

**修正確認結論**: 全 16 件の High 指摘が設計書に反映されていることを確認。ただし、§29 の追記セクションで追加された新エンティティ・サービス・エンドポイントが §18（AuthDbContext）・§24（Program.cs）の既存セクションに統合されておらず、**4 件の新規リグレッション** が発生している。

---

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

---

## 指摘サマリー
| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ✅ | 0 | 0 | 2 | 1 |
| architect | ⚠️ | 0 | 1 | 2 | 0 |
| tech-lead | ⚠️ | 0 | 1 | 1 | 1 |
| programing-reviewer | ⚠️ | 0 | 2 | 3 | 1 |
| security-reviewer | ✅ | 0 | 0 | 2 | 0 |
| dba-reviewer | ⚠️ | 0 | 1 | 2 | 1 |
| qa-manager | ✅ | 0 | 0 | 2 | 1 |
| performance-reviewer | ✅ | 0 | 0 | 1 | 1 |
| compliance-reviewer | ✅ | 0 | 0 | 1 | 0 |
| oss-reviewer | ✅ | 0 | 0 | 1 | 0 |
| release-manager | ✅ | 0 | 0 | 1 | 1 |
| infra-ops-reviewer | ✅ | 0 | 0 | 1 | 1 |
| audit-reviewer | ✅ | 0 | 0 | 1 | 0 |
| ux-accessibility-reviewer | ✅ | 0 | 0 | 0 | 1 |
| **合計** | | **0** | **4** | **20** | **9** |

## 判定根拠
- 判定ルール適用結果: Critical 指摘 0 件、High 指摘 4 件（全て新規リグレッション）。High のみのため ⚠️ Conditional Approval
- 前回の 16 件の High 指摘は全て修正確認済み
- 新規 High 指摘は全て「§29 追記セクションの定義が §18/§24 の既存セクションに統合されていない」という同一カテゴリの問題であり、修正が容易

---

## Critical/High 指摘一覧（修正必須）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| NEW-H-01 | **High** | programing-reviewer, dba-reviewer | エンティティ・スキーマ不整合（リグレッション） | H-07 修正時に `OAuthAccount` エンティティ（§12）へ `AccessToken`, `RefreshToken`, `TokenExpiresAt` を追加したが、元々存在していた `ProfileData` プロパティが脱落している。一方、DB スキーマ（§oauth_accounts テーブル）には `profile_data JSONB` カラムが残存し、§18 `OnModelCreating` にも `entity.Property(o => o.ProfileData).HasColumnType("jsonb")` が記載されている。このままでは **コンパイルエラー** になる | §12 `OAuthAccount` エンティティに `[Column("profile_data")] public string? ProfileData { get; set; }` を追加する |
| NEW-H-02 | **High** | architect, programing-reviewer | AuthDbContext 未統合（リグレッション） | §29 の追記セクションで追加された 3 エンティティ（`PasswordHistory`、`OAuthClient`、`AuditLog`）が §18 の `AuthDbContext` に統合されていない。`DbSet` プロパティ未定義・`OnModelCreating` 設定未記載のため、EF Core マイグレーション時にこれらのテーブルが生成されない。**具体的な欠落**: ① `DbSet<PasswordHistory> PasswordHistories` ② `DbSet<OAuthClient> OAuthClients` ③ `DbSet<AuditLog> AuditLogs`（spec.md 参照） ④ 各エンティティの `OnModelCreating` 設定（FK、インデックス、CHECK 制約） | §18 `AuthDbContext` に上記 3 エンティティの `DbSet` と `OnModelCreating` 設定を追加する。§29 の定義は補足ではなく、§18 に統合すべき |
| NEW-H-03 | **High** | tech-lead, programing-reviewer | Program.cs 未統合（リグレッション） | §29 で追加されたサービス・BackgroundService・Endpoint マッピングが §24 の `Program.cs` 統合ビューに反映されていない。追記ノート（「Program.ps 追記」）が散在するのみで、§24 を参照した実装者がこれらを見落とすリスクが高い。**具体的な欠落**: ① DI 登録: `IClientCredentialsService`, `IPasswordHistoryRepository` ② BackgroundService: `SecurityLogAnonymizationService` ③ DbContext: `AuditLogInterceptor` 追加 ④ Endpoint: `app.MapTokenEndpoints()`, `app.MapEmailVerificationEndpoints()` | §24 の Program.cs 統合ビューに §29 で追加された全登録を直接記載する。散在する「追記」ノートを廃止し、§24 を SSOT とする |
| NEW-H-04 | **High** | dba-reviewer, programing-reviewer | user_mfa スキーマ・エンティティ不整合 | `user_mfa.secret_key` のデータ型・制約が DB スキーマとエンティティで矛盾している: ① DB スキーマ: `VARCHAR(255) NULL` ② エンティティ（§12）: `[Required] [MaxLength(500)]`。MaxLength の差異（255 vs 500）と NULL 許容 vs Required の矛盾がある。MFA 未セットアップ時に `SecretKey` が空文字列（`string.Empty`）で初期化されるが、`[Required]` は空文字列を許容するため表面上は動作するが、DB の `NULL` 許容と C# の `[Required]` の設計意図が不明確 | スキーマとエンティティを統一する。推奨: DB スキーマを `VARCHAR(500) NOT NULL DEFAULT ''` に変更し、エンティティの `[Required]` をそのまま保持する。または MFA 未セットアップ時は `UserMfa` レコード自体を作成しない設計であれば、`NULL` 許容を維持しエンティティから `[Required]` を除去する |

---

## Medium 指摘一覧

### 前回からの継続指摘（修正対象外のため残存）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | ステータス |
|---|--------|-----------|---------|----------|----------|
| M-01 | Medium | programing-reviewer | MFA 方式の限定 | Phase 1 は TOTP のみ対応。`mfa_type` CHECK 制約に将来の値が含まれていない | 継続（前回 M-01） |
| M-02 | Medium | security-reviewer | OAuth state パラメータ検証ロジック未定義 | §23 `StartOAuthFlowAsync` で `state` を生成するが保存・検証ロジックが未記載。CSRF リスク | 継続（前回 M-02） |
| M-03 | Medium | security-reviewer | アカウントロック時間のハードコード | §26 `FailedAttemptResetService` で `AutoUnlockAfter = TimeSpan.FromMinutes(30)` がハードコード。§27 で設定キー `Auth:AutoUnlockMinutes` を定義済みだが未使用 | 継続（前回 M-03） |
| M-04 | Medium | dba-reviewer | security_logs パーティション設計なし | §29.4 で保持期間・クリーンアップは定義されたが、テーブルパーティショニング戦略は未記載 | 部分改善（前回 M-04） |
| M-05 | Medium | dba-reviewer | OutboxPublisher Advisory Lock 未使用 | §14 `OutboxPublisher` に PostgreSQL Advisory Lock がなく、`minReplicas: 2` 時にイベント二重発行リスク | 継続（前回 M-05） |
| M-06 | Medium | programing-reviewer | IHasTimestamps の不完全な実装 | §18 で `IHasTimestamps` を定義し `SaveChangesAsync` で使用しているが、§12 のエンティティクラス定義に `: IHasTimestamps` が記載されていない | 継続（前回 M-06） |
| M-07 | Medium | qa-manager | 異常系テストケースの不足 | 「同時セッション上限超過」「パスワード履歴チェック」「並行リフレッシュトークン利用検出」「Replay Detection」のテストケースが欠落 | 継続（前回 M-07）。§29 で追加された機能のテストケースも未追記 |
| M-08 | Medium | qa-manager | サーキットブレーカー動作テストが「推奨」分類 | 外部 IdP 障害時のサーキットブレーカー動作テストは「必須」であるべき | 継続（前回 M-08） |
| M-09 | Medium | architect | Web エンドポイント（HTML 返却）の位置づけ不明確 | `GET /`, `GET /home`, `GET /profile` 等の HTML エンドポイントの本番/デバッグ用途が未明記 | 継続（前回 M-09） |
| M-10 | Medium | oss-reviewer | Azure.Identity パッケージのバージョン未固定 | `Azure.Identity` が「最新」と記載。`1.*` 等の固定パターンに変更すべき | 継続（前回 M-10） |
| M-11 | Medium | release-manager | 環境変数にサンプル接続文字列パターン残存 | `${...}` パターンがコピー&ペーストでそのままデプロイされるリスク | 継続（前回 M-11） |
| M-12 | Medium | infra-ops-reviewer | Docker HEALTHCHECK の curl/wget 不統一 | AGENTS.md は `curl` だが設計書は `wget` を使用 | 継続（前回 M-12） |
| M-13 | Medium | business-analyst | ソーシャルログイン対象の不一致 | spec.md の Google/Facebook/Apple/LINE vs 設計書の Azure AD のみ | 継続（前回 M-13） |
| M-14 | Medium | business-analyst | ゲスト購入時の認証フロー未連携 | §29.8 でゲスト→会員変換フローを追加済みだが、SalesManagementService 側のイベント購読詳細は本設計書の管轄外 | 部分改善（前回 M-14） |
| M-15 | Medium | compliance-reviewer | 同意（Consent）管理との連携未記載 | spec.md の `OAuthConsent` が設計書の対象か UserManagementService の管轄かが未明記 | 継続（前回 M-15） |
| M-16 | Medium | architect | `PermissionsUpdated` 発行元の不整合 | 購読イベントの `PermissionsUpdated` 発行元が「ユーザー管理サービス」だが、Role/Permission は AuthService の責務 | 継続（前回 M-16） |
| M-17 | Medium | performance-reviewer | Redis フェイルオーバー時の動作未定義 | Redis 障害 = 全認証停止の SPOF リスク。フォールバック戦略が未記載 | 継続（前回 M-17） |
| M-18 | Medium | programing-reviewer | SecurityLog Details カラム型不一致 | C# エンティティで `string?` だが DB は `JSONB`。型安全性が不足 | 継続（前回 M-18） |
| M-19 | Medium | audit-reviewer | SecurityLog event_type の許容値未定義 | `event_type` に CHECK 制約がなく、ログ分析時に種別の揺れが発生する | 継続（前回 M-19） |

### 新規 Medium 指摘

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| NEW-M-01 | Medium | programing-reviewer | SecurityLogAnonymizationService のハッシュ判別ロジック不備 | §29.4 の `SecurityLogAnonymizationService` で、仮名化済み IP アドレスの判別に `s.IpAddress.Length <= 45` を使用しているが、SHA-256 ハッシュを `[..12]` で切り詰めた結果は 12 文字であり `<= 45` を満たすため、再度ハッシュ化される。冪等性が確保されていない | 仮名化済みフラグ（`is_anonymized` カラム追加）を使用するか、ハッシュ値にプレフィックス（例: `ANON:`）を付与して判別する |

---

## Low 指摘一覧

| # | 出典 Agent | 指摘内容 | ステータス |
|---|-----------|----------|----------|
| L-01 | business-analyst | §17（まとめ）に「✅ 完了」「🔄 進行中」の実装進捗ステータスが残存 | 継続 |
| L-02 | tech-lead | §4 ER 図に `OAuthClient`（§29.5）が未反映 | 新規（修正起因） |
| L-03 | programing-reviewer | §12 の `PasswordReset` エンティティに §29.2 で追加された `TokenType` プロパティが未統合 | 新規（修正起因） |
| L-04 | dba-reviewer | `users.role`（単一ロール）と `user_roles`（多対多）テーブルの二重管理 | 継続 |
| L-05 | performance-reviewer | Argon2id のパラメータ（メモリコスト、反復回数）が未明記 | 継続 |
| L-06 | release-manager | Docker EXPOSE が `8080` だが §サービス情報テーブルでポートは `5001` | 継続 |
| L-07 | ux-accessibility-reviewer | HTML エンドポイントの WCAG 2.1 AA 準拠要件が未記載 | 継続 |
| L-08 | tech-lead | `DEAD_LETTER` の運用手順が未記載 | 継続 |
| L-09 | qa-manager | テストカバレッジ 80% 達成に必要な工数見積もり情報なし | 継続 |

---

## エスカレーション事項（要人間判断）

前回 (check-report-3) のエスカレーション事項の解決状況:

| # | 前回内容 | 解決状況 |
|---|---------|---------|
| E-01 | spec.md と設計書のエンティティモデルの根本的な乖離 | ✅ 解決 — §4 直後に対応表を追加し SSOT 方針を明確化 |
| E-02 | ユーザー登録の責務所在 | ✅ 解決 — §29.8 で AuthService を主体と明確化 |
| E-03 | リフレッシュトークン有効期限 7 日 vs 14 日 | ✅ 解決 — 14 日に統一 |
| E-04 | security_logs の IP アドレス保持期間 | ✅ 解決 — §29.4 で 3 フェーズ保持ポリシーを定義 |

**新規エスカレーション**: なし

---

## 競合解決記録

前回 (check-report-3) の競合:

| # | Agent A | Agent B | 前回状況 | 今回状況 |
|---|---------|---------|---------|---------|
| C-01 | architect（spec.md モデル統一） | programing-reviewer（現設計書モデル維持） | エスカレーション | ✅ 解決 — 設計書エンティティを正とし、対応表で spec.md との関係を明示する方針が採用された |

今回新規競合: なし

---

## ドキュメント横断分析

### マイクロサービス設計書カバレッジ

| 項目 | 記載状況 | 前回比 |
|------|---------|--------|
| API 定義 | ✅ 全エンドポイント定義済み。メール検証（§29.2）・Client Credentials（§29.5）を追加 | ↑ 改善 |
| DB 設計 | ⚠️ スキーマ定義あり。ただし §29 追加エンティティの AuthDbContext 統合が不完全 | → 横ばい（リグレッション） |
| イベント定義 | ✅ 発行イベント 8 種 + DSR 関連イベント追加、購読イベント 2 種。PII 最小化原則準拠 | ↑ 改善 |
| セキュリティ | ✅ Client Credentials 追加（§29.5）、DSR 対応追加（§29.3）。OAuth state 検証は未解決 | ↑ 改善 |
| 非機能要件 | ✅ パフォーマンスメトリクス、キャッシュ戦略、可観測性、耐障害性が記載 | → 横ばい |
| テスト戦略 | ⚠️ §29 で追加された機能のテストケースが未追記（パスワード履歴チェック、Replay Detection 等） | → 横ばい |
| EF Core エンティティ | ⚠️ §29 追加エンティティが AuthDbContext に未統合 | → 横ばい（リグレッション） |
| Service/Repository | ✅ §29 で不足分を補完済み（IPasswordHistoryRepository, IClientCredentialsService 等） | ↑ 改善 |
| Program.cs 統合 | ⚠️ §24 が §29 追加分を未反映 | → 横ばい（リグレッション） |
| Dockerfile | ✅ マルチステージビルド・非 root ユーザー・HEALTHCHECK 記載 | → 横ばい |
| Outbox パターン | ✅ ADR-0005 準拠 | → 横ばい |
| Replay Detection | ✅ §19 + §29.10 で完全に定義 | ↑ 改善（新規追加） |
| DSR 対応 | ✅ §29.3 で定義 | ↑ 改善（新規追加） |
| AuditLog 統合 | ⚠️ §29.9 で定義されたが AuthDbContext/Program.ps に未統合 | ↑ 部分改善（リグレッション） |
| パスワード履歴 | ⚠️ §29.1 で定義されたが AuthDbContext に未統合 | ↑ 部分改善（リグレッション） |

### サービス間整合性

| チェック項目 | 結果 | 前回比 |
|------------|------|--------|
| UserManagementService との Kafka イベント整合性 | ⚠️ `PermissionsUpdated` の発行元矛盾は継続（M-16） | → 横ばい |
| API Gateway との JWT 検証整合性 | ✅ RS256, `kid` 鍵ローテーション対応が spec.md と一致 | → 横ばい |
| MailSendService との連携 | ✅ §29.2 で `EmailVerificationRequested` イベント発行を追加 | ↑ 改善 |
| SalesManagementService との連携 | ✅ §29.8 でゲスト→会員変換フローを定義 | ↑ 改善 |

### 未定義・曖昧な領域

| 項目 | 影響度 | 実装ブロッカーか | 前回比 |
|------|-------|--------------|--------|
| §29 追加定義の §18/§24 への統合 | 高 | ⚠️ 統合しないと EF Core マイグレーション失敗・DI 未登録 | NEW |
| OAuthAccount.ProfileData 脱落 | 高 | ⚠️ コンパイルエラー | NEW |
| user_mfa.secret_key スキーマ不整合 | 中 | ⚠️ マイグレーション時にカラム定義が不正 | NEW |
| OAuth state 検証ロジック | 中 | ⚠️ CSRF 攻撃リスク | 継続 |
| OutboxPublisher Advisory Lock | 中 | ⚠️ イベント二重発行リスク | 継続 |
| Redis フェイルオーバー戦略 | 中 | ⚠️ SPOF リスク | 継続 |

---

## 改善評価サマリ

### 定量比較

| 指標 | check-report-3 | check-report-4 | 変化 |
|------|---------------|---------------|------|
| Critical | 0 | 0 | → |
| High | 16 | 4 | ▼ 12 (-75%) |
| Medium | 20 | 20 | → |
| Low | 9 | 9 | → |
| エスカレーション | 4 | 0 | ▼ 4 (-100%) |
| 競合 | 1 | 0 | ▼ 1 (-100%) |

### 定性評価

**大幅に改善された領域**:
- spec.md との整合性（エンティティ対応表、ロールモデル対応表、SSOT 方針）
- リフレッシュトークンセキュリティ（Replay Detection、family_id、絶対有効期限）
- GDPR 対応（DSR イベントハンドラー、security_logs 仮名化ポリシー）
- サービス間 M2M 認証（Client Credentials エンドポイント）
- 監査ログ統合（AuditLogInterceptor）

**残存課題**:
- §29 追記セクションの散在（既存セクションへの統合が不完全）
- OAuth2 state パラメータ検証の未定義
- テストケースの不足（§29 追加機能のカバレッジ）

### 次回修正の推奨優先度

| 優先度 | 修正内容 | 工数見積 |
|--------|---------|---------|
| 1 | NEW-H-01: OAuthAccount に ProfileData 復元 | 小 |
| 2 | NEW-H-02: AuthDbContext に PasswordHistory/OAuthClient/AuditLog 統合 | 中 |
| 3 | NEW-H-03: Program.cs に §29 追加分を統合 | 中 |
| 4 | NEW-H-04: user_mfa.secret_key スキーマ統一 | 小 |

> **注記**: 4 件の High 指摘は全て「§29 追記セクションと既存セクションの統合不備」に起因しており、修正の性質上リスクが低く、作業量も限定的である。

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### 評価: ✅ Pass

**改善確認**:
- H-14（ユーザー登録の責務境界）: §29.8 で AuthService を主体とする方針が明確に定義された。責務分担表・ゲスト→会員変換フローも追加済み

**残存指摘**:
- **M-13**: ソーシャルログイン対象の不一致。Azure AD のみ実装で Google/Facebook/Apple/LINE の計画が未記載（前回より継続）
- **M-14**: ゲスト購入フローの AuthService 側責務は §29.8 で定義済み。SalesManagementService 側は管轄外のため部分改善
- **L-01**: §17（まとめ）に実装進捗ステータスが残存

</details>

<details>
<summary>architect レビューレポート</summary>

### 評価: ⚠️ Conditional

**改善確認**:
- H-01/H-16（spec.md エンティティ対応表）: 詳細な対応表を追加。SSOT 方針が明記され、設計書エンティティを実装レベルの正式名称と定義
- H-02（Replay Detection）: §19 に family_id/previous_token_id/absolute_expiry を追加。§29.10 にロジックを定義
- H-04（ロールモデル対応表）: spec.md ロールとのマッピングテーブルを追加。スコープベース認可の拡張方針も明確化

**新規指摘**:
- **NEW-H-02**（部分担当）: §29 で追加されたエンティティ（PasswordHistory, OAuthClient, AuditLog）が §18 AuthDbContext の `OnModelCreating` に未統合
- **M-09**: Web エンドポイント（HTML 返却）の位置づけが引き続き不明確
- **M-16**: `PermissionsUpdated` イベントの発行元矛盾が継続

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 評価: ⚠️ Conditional

**改善確認**:
- H-01（spec.md 整合性）: 対応表の追加により解決
- H-03（14日統一）: 環境変数・ビジネスルール共に 14 日に統一

**新規指摘**:
- **NEW-H-03**: §24 Program.cs が §29 追加分を未反映。実装者は §24 を参照して Program.cs を構築するが、§29 の散在するノートを見落とすリスクが高い
- **M-06**: IHasTimestamps の implements 漏れが継続
- **L-02**: §4 ER 図に `OAuthClient`（§29.5 追加）が未反映
- **L-08**: `DEAD_LETTER` の運用手順が引き続き未記載

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional

**改善確認**:
- H-07（oauth_accounts カラム追加）: DB スキーマにカラム追加済み。ただしリグレッションあり
- H-12（Npgsql DateTime 設定）: §29.6 で `AppContext.SetSwitch` を明記
- H-13（Value Object Phase 2 計画）: §29.7 で段階的導入方針を定義

**新規指摘**:
- **NEW-H-01**: OAuthAccount エンティティから `ProfileData` が脱落。§18 `OnModelCreating` で `o.ProfileData` を参照しておりコンパイルエラー
- **NEW-H-04**（部分担当）: user_mfa.secret_key の MaxLength 不整合（エンティティ 500 vs スキーマ 255）
- **M-01**: MFA 方式限定は継続
- **M-06**: IHasTimestamps の implements 漏れは継続
- **M-18**: SecurityLog.Details の型マッピングは継続
- **L-03**: §12 PasswordReset エンティティに §29.2 の TokenType プロパティが未統合

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 評価: ✅ Pass

**改善確認**:
- H-02（Replay Detection）: ファミリーベースの一括無効化ロジックが §29.10 で完全定義
- H-05（パスワード履歴）: §29.1 でテーブル・エンティティ・履歴チェックロジックを定義
- H-06（メール検証）: §29.2 でエンドポイント・フロー・トークン管理を定義
- H-11（Client Credentials）: §29.5 で M2M トークン発行の完全な設計を定義

**残存指摘**:
- **M-02**: OAuth state パラメータの保存・検証ロジックが引き続き未定義。§23 `StartOAuthFlowAsync` で生成するが、`HandleCallbackAsync` での照合が未記載
- **M-03**: アカウントロック時間のハードコードが継続。`IOptions<AuthSecurityOptions>` 経由にすべき

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional

**改善確認**:
- H-07（oauth_accounts カラム不一致）: DB スキーマにカラム追加済み
- H-08（user_roles カラム統一）: スキーマ・エンティティ両方で統一

**新規指摘**:
- **NEW-H-04**: user_mfa.secret_key のスキーマ・エンティティ不整合
- **M-04**: security_logs のパーティション設計は引き続き未記載（§29.4 でクリーンアップは定義済み）
- **M-05**: OutboxPublisher の Advisory Lock 未使用は継続
- **L-04**: users.role と user_roles の二重管理は継続

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 評価: ✅ Pass

**残存指摘**:
- **M-07**: §29 で追加された機能（パスワード履歴チェック、Replay Detection、DSR 処理、Client Credentials、メール検証）に対応するテストケースが §16 に未追記
- **M-08**: サーキットブレーカー動作テストが引き続き「推奨」分類
- **L-09**: テストカバレッジ工数見積もり情報なし

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 評価: ✅ Pass

**残存指摘**:
- **M-17**: Redis フェイルオーバー時のフォールバック戦略が引き続き未定義
- **L-05**: Argon2id のパラメータが引き続き未明記

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 評価: ✅ Pass

**改善確認**:
- H-09（DSR 対応）: §29.3 で AuthService の DSR 処理対象データ・仮名化ポリシー・イベントハンドラーを完全定義
- H-10（PII 保持期間）: §29.4 で 3 フェーズ保持ポリシーを定義。BackgroundService で自動匿名化・削除を実装

**残存指摘**:
- **M-15**: OAuthConsent の管轄（AuthService vs UserManagementService）が引き続き未明記

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 評価: ✅ Pass

**残存指摘**:
- **M-10**: Azure.Identity パッケージのバージョンが引き続き「最新」と記載

</details>

<details>
<summary>release-manager レビューレポート</summary>

### 評価: ✅ Pass

**残存指摘**:
- **M-11**: 環境変数の `${...}` パターンのリスク注記は引き続き不足
- **L-06**: Docker EXPOSE と §サービス情報テーブルのポート不一致

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 評価: ✅ Pass

**残存指摘**:
- **M-12**: Docker HEALTHCHECK の `wget` → `curl` 統一は継続
- **L-NEW**: §29.4 `SecurityLogAnonymizationService` が §24 の BackgroundService 登録に未反映（NEW-H-03 に含む）

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 評価: ✅ Pass

**改善確認**:
- H-15（AuditLogInterceptor）: §29.9 で監査対象操作テーブル・Interceptor 登録・実装コードを定義。ハッシュチェーン設計は spec.md を SSOT として参照

**残存指摘**:
- **M-19**: security_logs.event_type の許容値 CHECK 制約が引き続き未定義

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 評価: ✅ Pass

**残存指摘**:
- **L-07**: HTML エンドポイントの WCAG 2.1 AA 準拠要件が引き続き未記載

</details>
