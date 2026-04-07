# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/user-management-design.md`（1583 行、修正後イテレーション 2）
- **判定**: ⚠️ **Conditional Approval** — High 指摘 2 件あり（人間の判断を介在）
- **レビュー日時**: 2026-04-03
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)
- **前回レビュー**: check-report-1（❌ Rejected — Critical 12, High 14）
- **修正対応**: fix-report-1（26 件全件修正済み）

## 判定根拠
- **前回 Critical 12 件 → 0 件**: 全 Critical 指摘が是正されたことを確認
- **前回 High 14 件 → 0 件（既存）**: 全 High 指摘が是正されたことを確認
- **新規 High 2 件**: 修正に伴い新たに検出された設計上のギャップ
- **新規 Medium 7 件, Low 4 件**: 実装品質向上のための改善提案
- 最も重大な指摘: Outbox パターンの原子性違反（コード例）、Kafka 購読イベントの欠落（会員ランクリアルタイム昇格・在庫復活通知）

---

## 前回指摘の解決状況

### Critical 指摘（12 件 → 全件解決 ✅）

| # | 指摘ID | カテゴリ | 解決状況 | 確認箇所 |
|---|--------|---------|---------|---------|
| 1 | C-01 | 責務境界（AuthService 侵害） | ✅ 解決 | §1 概要で責務分離を明記。登録・パスワード・ロール管理 API を除去 |
| 2 | C-02 | Address/Wishlist/WishlistItem 欠落 | ✅ 解決 | §3 ER 図・§4 API・§14 エンティティ定義に追加 |
| 3 | C-03 | MemberRank 欠落 | ✅ 解決 | §3 ER 図・§14 エンティティ・MemberRankEvaluationService 追加 |
| 4 | C-04 | GDPR/DSR ワークフロー欠落 | ✅ 解決 | §6 に DeletionRequest・14 日猶予・DsrTimeoutMonitorService・シーケンス図を追加 |
| 5 | C-05 | Consent 管理欠落 | ✅ 解決 | §3 ER 図・§4 同意 API・§5 consent.revoked イベント追加 |
| 6 | C-06 | User に passwordHash | ✅ 解決 | §14 User エンティティから除去 |
| 7 | C-07 | User に roleId FK | ✅ 解決 | §14 User エンティティから除去。Role/Permission 参照なし |
| 8 | C-08 | DB 名 skishopdb | ✅ 解決 | §3 で `userdb` を明記。§11 環境変数も `Database=userdb` |
| 9 | C-09 | Kafka イベント設計乖離 | ✅ 解決 | §5 を spec.md 準拠に全面修正（発行: user.deleted, user.profile-updated, consent.revoked / 購読: user.registered, user.deletion.completed） |
| 10 | C-10 | Outbox パターン欠落 | ✅ 解決 | §3 ER 図に outbox_events 追加。§14 に OutboxPublisher BackgroundService（Advisory Lock + 動的バックオフ）追加 |
| 11 | C-11 | RFC 9457 非準拠 | ✅ 解決 | §7 を RFC 9457 Problem Details 形式に変更。TypedResults.Problem() 使用 |
| 12 | C-12 | 処理制限権（GDPR Art.18）欠落 | ✅ 解決 | §14 User エンティティに ProcessingRestricted/RestrictionReason/RestrictedAt 追加。§4 管理者 API に処理制限エンドポイント追加 |

### High 指摘（14 件 → 全件解決 ✅）

| # | 指摘ID | カテゴリ | 解決状況 | 確認箇所 |
|---|--------|---------|---------|---------|
| 1 | H-01 | Advisory Lock 設計欠落 | ✅ 解決 | §14 MemberRankEvaluationService に `pg_try_advisory_lock(hashtext('member_rank_eval'))` を追加 |
| 2 | H-02 | DsrTimeoutMonitorService 欠落 | ✅ 解決 | §3 クラス構成・§6 DSR セクションで仕様定義（1 時間ポーリング、24 時間タイムアウト、最大 3 リトライ） |
| 3 | H-03 | Redis 参照不一致 | ✅ 解決 | Redis 関連記述を全除去。§11 Aspire 注記で明確化 |
| 4 | H-04 | CHECK 制約未定義 | ✅ 解決 | §3 CHECK 制約テーブルに全ステータスカラムの制約を追加 |
| 5 | H-05 | DateTimeOffset 未使用 | ✅ 解決 | §3 ER 図・§14 全エンティティで DateTimeOffset 使用 |
| 6 | H-06 | FK 制約未定義 | ✅ 解決 | §3 FK 制約テーブルに ON DELETE/ON UPDATE を明記 |
| 7 | H-07 | IDOR 防止欠落 | ✅ 解決 | §6・§14 に ClaimsPrincipal からのオーナーシップ検証パターン追加 |
| 8 | H-08 | PII ログ出力 | ✅ 解決 | §6 PII ログ禁止セクション追加。§14 コード例で `{UserId}` のみ出力 |
| 9 | H-09 | Moq → NSubstitute | ✅ 解決 | §2 NuGet パッケージ・§10 テスト戦略で NSubstitute + Shouldly に変更 |
| 10 | H-10 | シーケンス図フロー誤り | ✅ 解決 | §5 に AuthService → Kafka(user.registered) → UserManagementService シーケンス図を追加 |
| 11 | H-11 | データポータビリティ欠落 | ✅ 解決 | §4 GDPR/DSR API に data-export エンドポイント追加。§6 で DataExportService 言及 |
| 12 | H-12 | MemberRank テスト戦略欠落 | ✅ 解決 | §10 に MemberRank テスト戦略テーブル追加（昇格・降格・Advisory Lock 検証） |
| 13 | H-13 | API バージョニング欠落 | ✅ 解決 | §4 全 API パスに `/api/v1/` プレフィックスを追加 |
| 14 | H-14 | Aggregate Root 違反 | ✅ 解決 | §14 UserService から roleRepository 参照を除去 |

---

## 技術スタック検証結果

| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| 言語 | C# 14 (.NET 10 LTS) | C# 14 (.NET 10) | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| オーケストレーション | .NET Aspire 13.1 | .NET Aspire 13.1 | ✅ |
| ORM | EF Core（Npgsql 10.*） | EF Core 10 | ✅ |
| DB | PostgreSQL (userdb) | PostgreSQL（ADR-0006 独立 DB） | ✅ |
| メッセージング | Confluent.Kafka 2.* | Confluent.Kafka 2.* | ✅ |
| キャッシュ | 未使用（spec.md Aspire 構成準拠） | Redis（本サービスでは不要） | ✅ |
| 認証 | JWT Bearer 10.* | JWT Bearer + Identity | ✅ |
| バリデーション | FluentValidation 11.* | FluentValidation 11.* | ✅ |
| ロギング | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| テスト | xUnit + NSubstitute + Shouldly | xUnit + NSubstitute + Shouldly | ✅ |
| テスト DB | Testcontainers.PostgreSql | Testcontainers.PostgreSql | ✅ |

---

## 指摘サマリー

| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ⚠️ Warn | 0 | 1 | 0 | 0 |
| architect | ⚠️ Warn | 0 | 1 | 1 | 1 |
| programing-reviewer | ⚠️ Warn | 0 | 1 | 2 | 1 |
| dba-reviewer | ✅ Pass | 0 | 0 | 2 | 0 |
| security-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| compliance-reviewer | ✅ Pass | 0 | 0 | 2 | 0 |
| qa-manager | ✅ Pass | 0 | 0 | 0 | 1 |
| performance-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| infra-ops-reviewer | ✅ Pass | 0 | 0 | 0 | 1 |
| release-manager | ✅ Pass | 0 | 0 | 0 | 0 |
| oss-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| audit-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| ux-accessibility-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| tech-lead | ✅ Pass | 0 | 0 | 0 | 0 |
| **合計** | | **0** | **2** | **7** | **4** |

---

## High 指摘一覧（修正強く推奨）

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|--------------|----------|----------|
| H-01 | **High** | programing-reviewer | Outbox 原子性 | §14 UserService.UpdateProfileAsync | **Outbox パターン原子性違反（ADR-0005）**: コード例で `await userRepository.SaveChangesAsync(ct)` の後に `await eventPublisher.PublishProfileUpdatedAsync(user.Id, ct)` を呼び出しており、ビジネスデータ変更（User 更新）と Outbox イベント書き込みが**別のトランザクション**で実行される。SaveChanges 後に PublishProfileUpdated が失敗した場合、プロファイルは更新されるがイベントは発行されず、データ不整合が発生する。ADR-0005 は「DB トランザクション内で outbox_events テーブルにイベントレコードを書き込み」と規定 | Outbox イベント書き込みを SaveChangesAsync の**前**に行い、同一 EF Core コンテキスト（同一 DbContext.SaveChangesAsync 呼び出し）で User 更新と Outbox レコード作成をコミットする。例: `eventPublisher.PublishProfileUpdatedAsync(user.Id, ct)` → `userRepository.SaveChangesAsync(ct)` の順序に変更し、SaveChanges が両テーブルへの変更を原子的にコミット |
| H-02 | **High** | business-analyst, architect | Kafka 購読欠落 | §5 購読イベント | **会員ランクリアルタイム昇格・在庫復活通知に必要な Kafka 購読イベントが未定義**: (a) spec.md L2609 は「昇格: リアルタイム判定。購入確定時点で年間累計が閾値を超えたら即時昇格」と規定するが、設計書の購読イベントに購入確定イベント（`order.confirmed` 等）が存在しない。`annual_purchase_amount` を更新する経路がなく、リアルタイム昇格が実装不可能。(b) spec.md L1545 は「`inventory.stock_updated` イベントを UserManagementService が購読」し在庫復活通知を行うと規定するが、設計書の購読イベント一覧に未記載 | (a) spec.md と協議の上、購入確定イベント（例: `order.confirmed` or `order.delivered`）を購読イベントに追加し、MemberRank.annualPurchaseAmount の更新 + リアルタイム昇格判定ロジックを設計。(b) `inventory.stock_updated` を購読イベントに追加し、WishlistItem.notifyOnRestock に基づく通知トリガーを設計 |

---

## Medium 指摘一覧（改善推奨）

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|--------------|----------|----------|
| M-01 | Medium | dba-reviewer | CHECK 制約整合性 | §3 CHECK 制約 | **Consent テーブル CHECK 制約が存在しないカラムを参照**: CHECK 制約テーブルは `consents \| status \| CHECK (status IN ('GRANTED','REVOKED'))` と定義するが、Consent エンティティ（§3 ER 図・§14 EF Core コード）には `status` カラムが存在しない。`is_granted` (Boolean) を使用している。Boolean カラムに対する `IN ('GRANTED','REVOKED')` CHECK 制約は型不一致 | 2 つのいずれかの方法で解決: (1) CHECK 制約テーブルから `consents.status` の行を削除（Boolean カラムに CHECK 制約は不要）、または (2) Consent エンティティの `is_granted` (bool) を `status` (string: GRANTED/REVOKED) に変更し CHECK 制約と整合させる |
| M-02 | Medium | dba-reviewer | Outbox ステータス | §3 CHECK 制約, §14 OutboxEvent | **OutboxEvent ステータス値が spec.md と不一致・CHECK 制約未定義**: 設計書の OutboxEvent エンティティは `PENDING`, `PUBLISHED`, `FAILED` の 3 値のみ定義。spec.md CHECK 制約（L2970）は `CHECK (status IN ('PENDING', 'PROCESSING', 'PUBLISHED', 'FAILED', 'DEAD_LETTER'))` と 5 値を定義。また §3 CHECK 制約テーブルに outbox_events の制約が記載されていない | (1) OutboxEvent エンティティのステータス値に `PROCESSING`, `DEAD_LETTER` を追加。(2) §3 CHECK 制約テーブルに `outbox_events \| status \| CHECK (status IN ('PENDING','PROCESSING','PUBLISHED','FAILED','DEAD_LETTER'))` と `outbox_events \| retry_count \| CHECK (retry_count >= 0)` を追加 |
| M-03 | Medium | programing-reviewer | RFC 9457 準拠 | §14 Endpoints 実装例 | **コード例の NotFound() が RFC 9457 非準拠**: `GetUserById` / `GetCurrentUser` が `Results.NotFound()` を返しているが、§7 で定義した RFC 9457 形式（`TypedResults.Problem(statusCode: 404, title: "Not Found", detail: "...")`)）と不一致。`Results.NotFound()` はボディなしの 404 を返すため、Problem Details 形式のレスポンスにならない | `Results.NotFound()` → `TypedResults.Problem(detail: "指定されたユーザーが見つかりません", statusCode: 404, title: "Not Found")` に変更。または §4.7 のグローバル例外ハンドラーパターンに合わせ `throw new NotFoundException(...)` を使用 |
| M-04 | Medium | programing-reviewer | コード例不整合 | §14 UserService.InitializeProfileAsync | **InitializeProfileAsync が UserPreference・MemberRank を未作成**: §5 シーケンス図は「プロファイル初期化（User, UserPreference, MemberRank）」と記載するが、§14 のコード例は User エンティティのみ作成。UserPreference（言語: ja, 通貨: JPY）と MemberRank（初期 BRONZE, pointRate: 0.01）の初期化が欠落しており、設計書内で記述が矛盾 | `InitializeProfileAsync` コード例に UserPreference（デフォルト値: language="ja", currency="JPY"）と MemberRank（currentRank="BRONZE", pointRate=0.01m, nextEvaluationDate=次年度4月1日）の作成を追加 |
| M-05 | Medium | compliance-reviewer | 匿名同意 | §4 同意管理 API, §6 同意管理 | **anonymous_consents エンティティが未定義**: §4 API テーブルに `POST /api/v1/anonymous-consents`（匿名ユーザー同意記録、認証不要）が定義され、§6 で「匿名ユーザーの同意は anonymous_consents テーブルで管理」と記載されているが、ER 図・エンティティ一覧・§14 EF Core コードのいずれにも anonymous_consents テーブル/エンティティの定義が存在しない | AnonymousConsent エンティティ（id, consent_type, is_granted, ip_address, user_agent, session_id, created_at）を §3 ER 図・エンティティ一覧に追加。ユーザー登録時の既存匿名同意マージロジックも設計に含める |
| M-06 | Medium | compliance-reviewer | GDPR Art.20 | §6 データポータビリティ | **DataExportService の実装詳細が不足**: §6 でデータエクスポート機能を概念的に記載しているが、エクスポートデータ形式（JSON スキーマ定義）、一時ファイル保存先（Azure Blob Storage 等）、ダウンロード提供方法（spec.md は「48 時間有効の署名付き URL」を規定）、エクスポート完了通知（メール等）の設計がない。GDPR 第 20 条の機械可読形式要件を満たす詳細が不足 | DataExportService の設計詳細を追加: (1) JSON 形式のエクスポートスキーマ定義（User, Address, Wishlist, Activity, Consent の全 PII データ）、(2) Azure Blob Storage への一時保存、(3) 48 時間有効の SAS 署名付き URL 生成、(4) MailSendService 経由のエクスポート完了通知 |
| M-07 | Medium | architect | Program.cs | （該当セクションなし） | **Program.cs ミドルウェアパイプライン例の欠落**: AGENTS.md §11.3 は「ミドルウェアパイプライン順序（厳守）」として UseExceptionHandler → UseHsts → UseHttpsRedirection → CorrelationId → UseSerilogRequestLogging → UseCors → UseAuthentication → UseAuthorization → UseRateLimiter → MapEndpoints の順序を規定。設計書に Program.cs の構成例がなく、実装時にミドルウェア順序誤りが発生するリスクがある | §14 に Program.cs の骨格コード（DI 登録 + ミドルウェアパイプライン順序 + エンドポイントマッピング + ヘルスチェック）の参考例を追加 |

---

## Low 指摘一覧（時間がある時に対応）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| L-01 | Low | programing-reviewer | コード例 | MemberRankEvaluationService のバッチ処理本体が `// ... バッチ処理 ...` のスタブ。降格 1 ランク制限・降格猶予（プラチナ 250,000 円以上でプラチナ維持）・累計リセットの骨格ロジックがあると実装者のガイドになる | バッチ処理内に spec.md 定義の降格ルールを反映した骨格ロジック（各ランクの閾値判定、previousYearAmount 更新、annualPurchaseAmount リセット）をコメント付きで追加 |
| L-02 | Low | architect | コード例 | Kafka Consumer BackgroundService（`user.registered` / `user.deletion.completed` イベント受信側）のコード例が §14 に不在。AGENTS.md §10.6 のパターン参照で対応可能だが、設計書内に記載があると実装ガイドとして完結する | §14 に Kafka Consumer BackgroundService の参考コード（IConsumer, Subscribe, Consume ループ, IServiceScopeFactory の使用パターン）を追加 |
| L-03 | Low | qa-manager | テスト戦略 | §10 GDPR/DSR テスト戦略に DataExportService のテスト項目（エクスポート対象データの網羅性、SAS URL 有効期限検証、個人情報マスキング確認）と anonymous_consents 関連テスト項目が未記載 | DataExportService テスト（エクスポートデータ完全性、有効期限超過時のアクセス拒否）と anonymous_consents テスト（匿名同意の記録、ユーザー登録時のマージ）を §10 に追加 |
| L-04 | Low | infra-ops-reviewer | ヘルスチェック | ヘルスチェックエンドポイント構成（MapHealthChecks, Liveness/Readiness 分離、PostgreSQL ヘルスチェック）のコード例が設計書に不在。§11 Dockerfile の HEALTHCHECK は `/health` を参照しているが構成コードがない | §14 に MapHealthChecks の構成例（Liveness: `/health`, Readiness: `/health/ready` with PostgreSQL check）を追加 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-01 | 高優先 | business-analyst, architect | **会員ランクリアルタイム昇格の Kafka イベント設計**: spec.md は「購入確定時点で即時昇格」を要求するが、spec.md 自体にも UserManagementService が購読すべき購入確定イベントが定義されていない。spec.md 側の Kafka トピック設計（SalesManagement の発行トピック追加）と合わせて解決する必要がある | プロダクトオーナー + テックリード |
| E-02 | 通常 | compliance-reviewer | **anonymous_consents のデータライフサイクル**: 匿名同意データの保持期間・ユーザー登録時のマージポリシー・未登録のまま放置された匿名同意の削除タイミングが未定義。GDPR Art.7 の同意立証義務との関係で方針決定が必要 | テックリード + 法務 |

---

## 競合解決記録

競合なし。Phase 4 はスキップ。

---

## ドキュメント横断分析

### spec.md とのエンティティ対応（前回比較）

| spec.md 定義エンティティ | 設計書に存在 | 前回 | 備考 |
|------------------------|------------|------|------|
| User | ✅ | ✅（但し passwordHash/roleId あり） | passwordHash/roleId 除去済み ✅ |
| UserPreference | ✅ | ✅（スキーマ不一致） | 構造化型（language/currency/JSONB）に修正済み ✅ |
| UserActivity | ✅ | ✅ | — |
| Address | ✅ | ❌ | **新規追加** ✅ |
| Wishlist | ✅ | ❌ | **新規追加** ✅ |
| WishlistItem | ✅ | ❌ | **新規追加** ✅ |
| MemberRank | ✅ | ❌ | **新規追加** ✅ |
| Consent | ✅ | ❌ | **新規追加** ✅ |
| DeletionRequest | ✅ | ❌ | **新規追加** ✅ |
| OutboxEvent | ✅ | ❌ | **新規追加** ✅ |

### Kafka トピック対応（前回比較）

| トピック | spec.md の定義 | 設計書の定義 | 整合性 | 前回 |
|---------|--------------|------------|--------|------|
| `user.registered` | AuthService → UserManagement（購読） | 購読 ✅ | ✅ | ❌ |
| `user.deleted` | UserManagement → 全サービス（発行） | 発行 ✅ | ✅ | ⚠️ |
| `user.profile-updated` | UserManagement → AiSupportService（発行） | 発行 ✅ | ✅ | ❌ |
| `consent.revoked` | UserManagement → Mail, AI, Auth（発行） | 発行 ✅ | ✅ | ❌ |
| `user.deletion.completed` | 各サービス → UserManagement（購読） | 購読 ✅ | ✅ | ❌ |
| `inventory.stock_updated` | Inventory → UserManagement（購読） | 未記載 | ❌ **欠落** | 未検出 |
| 購入確定イベント | spec.md 定義なし（機能要件との矛盾） | 未記載 | ⚠️ **要設計** | 未検出 |

### BackgroundService 対応（前回比較）

| BackgroundService | spec.md 定義 | 設計書に存在 | 前回 | 排他制御 |
|------------------|------------|------------|------|---------|
| OutboxPublisher | 必須（ADR-0005） | ✅ | ❌ | Advisory Lock ✅ |
| MemberRankEvaluationService | 必須（年次バッチ） | ✅ | ❌ | Advisory Lock ✅ |
| DsrTimeoutMonitorService | 必須（DSR タイムアウト） | ✅ | ❌ | — |
| DataExportService | 必須（GDPR 第 20 条） | ✅（概念のみ） | ❌ | 詳細設計不足 |

### CHECK 制約対応

| テーブル | spec.md 定義 | 設計書定義 | 整合性 |
|---------|------------|----------|--------|
| users.status | — | ✅ PENDING_VERIFICATION/ACTIVE/SUSPENDED/DEACTIVATED | ✅ |
| addresses.address_type | — | ✅ SHIPPING/BILLING | ✅ |
| member_ranks.current_rank | ✅ BRONZE/SILVER/GOLD/PLATINUM | ✅ | ✅ |
| member_ranks.annual_purchase_amount | ✅ >= 0 | ✅ | ✅ |
| member_ranks.point_rate | ✅ >= 0 AND <= 1 | ✅ | ✅ |
| consents.consent_type | ✅ MARKETING/PERSONALIZATION/ANALYTICS/THIRD_PARTY_SHARING | ✅ | ✅ |
| consents.status | ✅ GRANTED/REVOKED | ✅（⚠️ エンティティに status カラム不在） | ⚠️ M-01 |
| deletion_requests.status | ✅ 6 値 | ✅ | ✅ |
| deletion_requests.request_channel | ✅ 4 値 | ✅ | ✅ |
| outbox_events.status | ✅ 5 値 | 未記載 | ❌ M-02 |
| outbox_events.retry_count | ✅ >= 0 | 未記載 | ❌ M-02 |

### 改善トレンド

| 指標 | check-report-1 | check-report-2 | 改善率 |
|-----|---------------|---------------|--------|
| Critical | 12 | 0 | **100% 解消** |
| High | 14 | 2（新規） | **既存 100% 解消** |
| Medium | 8 | 7（新規） | — |
| Low | 1 | 4（新規） | — |
| spec.md エンティティ一致率 | 3/10 (30%) | 10/10 (100%) | **+70pt** |
| Kafka トピック一致率 | 1/6 (17%) | 5/7 (71%) | **+54pt** |
| ADR 準拠率 | 1/3 (33%) | 3/3 (100%) | **+67pt** |

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### business-analyst

**判定**: ⚠️ Warn

**前回指摘の確認**:
- C-12（処理制限権 GDPR Art.18 欠落）: ✅ 解決 — User エンティティに ProcessingRestricted 関連カラム追加、管理者 API 追加
- H-11（データポータビリティ GDPR Art.20 欠落）: ✅ 解決 — data-export API・DataExportService 追加

**新規指摘**:
| # | 重要度 | 指摘内容 |
|---|--------|---------|
| H-02 | **High** | 会員ランクのリアルタイム昇格機能が実装不可能。spec.md は「購入確定時点で即時昇格」を要求するが、購入データを受信する Kafka 購読イベントが未定義。annual_purchase_amount を更新する経路がない |

**良い点**:
- ユーザープロファイル管理の基本機能（CRUD）は網羅されている
- 住所管理・ウィッシュリスト管理の API 設計が適切
- GDPR/DSR の 14 日間猶予期間・キャンセル機能は EU 規制要件に沿っている

</details>

<details>
<summary>architect レビューレポート</summary>

### architect

**判定**: ⚠️ Warn

**前回指摘の確認**:
- C-01（責務境界）: ✅ 解決
- C-02（エンティティ欠落）: ✅ 解決
- C-03（MemberRank 欠落）: ✅ 解決
- C-09（Kafka 設計）: ✅ 解決
- H-01（Advisory Lock）: ✅ 解決
- H-02（DsrTimeoutMonitor）: ✅ 解決
- H-03（Redis 参照）: ✅ 解決
- H-13（API バージョニング）: ✅ 解決

**新規指摘**:
| # | 重要度 | 指摘内容 |
|---|--------|---------|
| H-02 | **High** | `inventory.stock_updated` Kafka 購読イベントの欠落。spec.md L1545 で在庫復活通知のために UserManagementService が購読すると明記されている |
| M-07 | Medium | Program.cs ミドルウェアパイプライン例の欠落。AGENTS.md §11.3 のミドルウェア順序要件と本サービスの構成（JWT 認証 + Serilog + CORS + エンドポイントマッピング）の具体例がない |
| L-02 | Low | Kafka Consumer BackgroundService のコード例不在 |

**良い点**:
- レイヤードアーキテクチャ（Endpoints → Services → Repositories）の依存方向が正しく設計されている
- Aspire AppHost 構成が spec.md 準拠（userDb + kafka、Redis なし）
- コンポーネントアーキテクチャ図が各クラスの依存関係を明確に示している
- Outbox パターン（ADR-0005）が正しく統合されている

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### programing-reviewer

**判定**: ⚠️ Warn

**前回指摘の確認**:
- H-09（NSubstitute）: ✅ 解決
- H-10（シーケンス図）: ✅ 解決
- H-14（Aggregate Root 違反）: ✅ 解決

**新規指摘**:
| # | 重要度 | 指摘内容 |
|---|--------|---------|
| H-01 | **High** | Outbox パターン原子性違反 — SaveChangesAsync と Outbox イベント書き込みが別コールで原子性未保証 |
| M-03 | Medium | Results.NotFound() が RFC 9457 Problem Details 形式ではない |
| M-04 | Medium | InitializeProfileAsync が UserPreference・MemberRank を未作成（§5 シーケンス図と矛盾） |
| L-01 | Low | MemberRankEvaluationService がスタブ |

**コード品質評価**:
- primary constructor の活用: ✅（UserService, OutboxPublisher, MemberRankEvaluationService）
- CancellationToken の伝搬: ✅（全 async メソッドに ct パラメータ）
- record 型の使用: ✅（DTO 用途で適切）
- PII ログ禁止: ✅（UserId のみ出力）
- FluentValidation 統合: ✅（UpdateUser エンドポイント）
- DI パターン: ✅（コンストラクタインジェクション、プロパティインジェクションなし）
- DateTimeOffset 使用: ✅（DateTime.UtcNow 不使用）

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### dba-reviewer

**判定**: ✅ Pass

**前回指摘の確認**:
- C-06（passwordHash）: ✅ 解決
- C-07（roleId FK）: ✅ 解決
- C-08（DB 名）: ✅ 解決
- H-04（CHECK 制約）: ✅ 解決
- H-05（DateTimeOffset）: ✅ 解決
- H-06（FK 制約）: ✅ 解決

**新規指摘**:
| # | 重要度 | 指摘内容 |
|---|--------|---------|
| M-01 | Medium | Consent CHECK 制約が存在しない `status` カラムを参照。エンティティは `is_granted` (bool) |
| M-02 | Medium | OutboxEvent ステータス値が spec.md 定義（5 値）と不一致（3 値）。CHECK 制約テーブルに outbox_events 未記載 |

**良い点**:
- テーブル名 snake_case 複数形、カラム名 snake_case: ✅
- 全カラムに `[Column("...")]` 属性で明示マッピング: ✅
- TIMESTAMP WITH TIME ZONE / DateTimeOffset 統一: ✅
- FK 制約（ON DELETE/ON UPDATE）の明示: ✅
- 部分インデックス（outbox_events PENDING/FAILED）: ✅
- JSONB カラムの適切な使用（notification_preferences, service_statuses）: ✅
- ER 図と EF Core エンティティの一貫性: ✅（M-01 の例外あり）

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### security-reviewer

**判定**: ✅ Pass

**前回指摘の確認**:
- C-10（Outbox パターン欠落）: ✅ 解決
- C-11（RFC 9457 非準拠）: ✅ 解決
- H-07（IDOR 防止欠落）: ✅ 解決
- H-08（PII ログ出力）: ✅ 解決

**新規指摘**: なし

**セキュリティ設計評価**:
- IDOR 防止（ClaimsPrincipal + IsInRole チェック）: ✅
- GDPR 処理制限（Art.18 ProcessingRestricted フラグ）: ✅
- PII ログ禁止（§6 明示 + コード例準拠）: ✅
- JWT Bearer 認証のみ（パスワード管理は AuthService 委譲）: ✅
- 管理者 API に RequireAuthorization("AdminOnly"): ✅（API テーブルで認証要件明示）
- データ保護（TLS 1.3、Azure Key Vault）: ✅
- RFC 9457 エラーレスポンス（スタックトレース非露出）: ✅
- Outbox パターンによるイベント整合性: ✅（ただし H-01 の原子性問題あり）

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### compliance-reviewer

**判定**: ✅ Pass

**前回指摘の確認**:
- C-04（DSR ワークフロー欠落）: ✅ 解決 — DeletionRequest エンティティ、14 日猶予、DsrTimeoutMonitorService、シーケンス図が全て追加
- C-05（Consent 管理欠落）: ✅ 解決 — Consent エンティティ、4 種別同意、consent.revoked イベント、ポリシーバージョン管理が追加
- C-12（処理制限権欠落）: ✅ 解決

**新規指摘**:
| # | 重要度 | 指摘内容 |
|---|--------|---------|
| M-05 | Medium | anonymous_consents エンティティ未定義 |
| M-06 | Medium | DataExportService 詳細不足（エクスポート形式・配信方法） |

**GDPR 対応チェックリスト**:
| GDPR 条項 | 対応状況 | 備考 |
|----------|---------|------|
| Art.15 アクセス権 | ✅ | GET /api/v1/users/me |
| Art.16 訂正権 | ✅ | PUT /api/v1/users/{id} |
| Art.17 削除権 | ✅ | DeletionRequest + 14 日猶予 + user.deleted |
| Art.18 処理制限権 | ✅ | ProcessingRestricted フラグ + 管理者 API |
| Art.20 データポータビリティ | ⚠️ | API 存在。詳細設計不足（M-06） |
| Art.7 同意立証 | ✅ | Consent エンティティ（version, policyTextHash, ipAddress, userAgent） |
| Art.21 異議権 | — | 本サービス範囲外 |
| RoPA（処理活動記録） | ✅ | UserActivity + 構造化ログ |
| DSR タイムアウト管理 | ✅ | DsrTimeoutMonitorService（24 時間 / 最大 3 リトライ / AWAITING_MANUAL_INTERVENTION） |

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### qa-manager

**判定**: ✅ Pass

**前回指摘の確認**:
- H-12（MemberRank テスト戦略欠落）: ✅ 解決

**新規指摘**:
| # | 重要度 | 指摘内容 |
|---|--------|---------|
| L-03 | Low | DataExportService・anonymous_consents のテスト項目未記載 |

**テスト戦略評価**:
- テストフレームワーク（xUnit + NSubstitute + Shouldly）: ✅
- Testcontainers.PostgreSql: ✅
- WebApplicationFactory: ✅
- カバレッジ目標（分岐 80%）: ✅
- MemberRank テスト（昇格・降格・Advisory Lock）: ✅
- GDPR/DSR テスト（猶予期間計算・キャンセル・タイムアウト検出・集約処理）: ✅

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### performance-reviewer

**判定**: ✅ Pass

**新規指摘**: なし

**パフォーマンス設計評価**:
- AsNoTracking() の使用（読み取り専用クエリ）: ✅
- インデックス設計（UNIQUE, Composite, Partial Index）: ✅
- 負荷テスト基準（500 req/s 通常、2,000 req/s ピーク）: ✅
- Outbox ポーリングの動的バックオフ（100ms〜5s）: ✅
- ページネーション実装: ✅（§8 で言及）
- Advisory Lock によるバッチ処理の排他制御: ✅

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### infra-ops-reviewer

**判定**: ✅ Pass

**新規指摘**:
| # | 重要度 | 指摘内容 |
|---|--------|---------|
| L-04 | Low | ヘルスチェックエンドポイント構成コード不在 |

**インフラ設計評価**:
- Dockerfile マルチステージビルド: ✅
- 非 root ユーザー（skishop）: ✅
- HEALTHCHECK 定義: ✅
- Aspire AppHost 参照（userDb + kafka）: ✅
- 構造化ログ（Serilog + JSON）: ✅
- OpenTelemetry メトリクス: ✅
- バックアップ戦略（日次フルバックアップ + WAL アーカイブ）: ✅

</details>

<details>
<summary>release-manager レビューレポート</summary>

### release-manager

**判定**: ✅ Pass

**評価**:
- Docker イメージビルド手順: ✅
- 環境変数の明示: ✅
- ゼロダウンタイムマイグレーション言及: ✅
- ロールバック計画: ✅
- スケーリング戦略: ✅

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### oss-reviewer

**判定**: ✅ Pass

**NuGet パッケージ検証**:
| パッケージ | バージョン | ライセンス | 状態 |
|-----------|----------|-----------|------|
| Microsoft.EntityFrameworkCore | 10.* | MIT | ✅ 適切 |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.* | PostgreSQL License | ✅ 適切 |
| FluentValidation | 11.* | Apache-2.0 | ✅ 適切 |
| Confluent.Kafka | 2.* | Apache-2.0 | ✅ 適切 |
| Serilog.AspNetCore | 8.* | Apache-2.0 | ✅ 適切 |
| OpenTelemetry.Extensions.Hosting | 1.* | Apache-2.0 | ✅ 適切 |
| AspNetCore.HealthChecks.NpgSql | 9.* | Apache-2.0 | ✅ 適切 |

- 禁止パッケージ（Newtonsoft.Json, log4net, EF6 等）: なし ✅
- プレリリースパッケージ: なし ✅
- `System.Web` 参照: なし ✅

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### audit-reviewer

**判定**: ✅ Pass

**トレーサビリティ評価**:
- 構造化ログ（ILogger<T> + Serilog）: ✅
- PII マスキングポリシー: ✅
- ユーザーアクティビティ記録（UserActivity エンティティ）: ✅
- DSR 処理の監査トレイル（DeletionRequest.serviceStatuses JSONB）: ✅
- 同意記録の証跡（Consent.version, policyTextHash, ipAddress, userAgent）: ✅
- Outbox イベントの追跡可能性（eventId, aggregateId, timestamp）: ✅

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### ux-accessibility-reviewer

**判定**: ✅ Pass

**評価**: UserManagementService はバックエンド API サービスであり、フロントエンド UI は含まない。API レスポンスの UX 面（RFC 9457 Problem Details、適切なステータスコード、日本語エラーメッセージ）は適切に設計されている。

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### tech-lead（初期レビュー）

**判定**: ✅ Pass

**技術標準準拠チェック**:
| 規約 | 状態 |
|------|------|
| C# 14 機能活用（primary constructor, record, pattern matching） | ✅ |
| CancellationToken 必須化（全 async メソッド） | ✅ |
| ILogger<T> 使用（Console.WriteLine なし） | ✅ |
| DI コンストラクタインジェクション（[Inject] なし） | ✅ |
| Endpoints → Services → Repositories 依存方向 | ✅ |
| ADR-0005 Outbox パターン準拠 | ✅（H-01 の原子性問題あり） |
| ADR-0006 独立 DB（userdb） | ✅ |
| ADR-0007 RFC 9457 | ✅（M-03 のコード例不一致あり） |
| spec.md エンティティ一致 | ✅（10/10） |
| AGENTS.md コーディング規約準拠 | ✅ |

**総合評価**: 前回レビューの大規模修正（12 Critical + 14 High）が適切に実施され、設計書の品質が大幅に向上した。残存する 2 件の High 指摘は設計の根本を覆すものではなく、局所的な修正で対応可能。特に H-02（Kafka 購読欠落）は spec.md 側の設計補完が必要な横断課題であり、設計書単独では完全に解決できない。

</details>

---

## 修正優先度ガイド

### 第 1 優先（コード例修正・影響小）
1. **H-01**: Outbox 原子性修正（§14 UserService.UpdateProfileAsync のコード修正のみ）

### 第 2 優先（Kafka 設計 — spec.md 調整を含む）
2. **H-02**: 購入確定イベント購読の追加（spec.md と設計書の両方で設計方針を決定）
3. **H-02**: `inventory.stock_updated` 購読の追加

### 第 3 優先（内部整合性修正）
4. **M-01**: Consent CHECK 制約 vs エンティティの整合
5. **M-02**: OutboxEvent ステータス値の spec.md 準拠 + CHECK 制約追加
6. **M-03**: Results.NotFound() → TypedResults.Problem()
7. **M-04**: InitializeProfileAsync に UserPreference/MemberRank 作成追加

### 第 4 優先（GDPR 補完）
8. **M-05**: anonymous_consents エンティティ定義
9. **M-06**: DataExportService 詳細設計
