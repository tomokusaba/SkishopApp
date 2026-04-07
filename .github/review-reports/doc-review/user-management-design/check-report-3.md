# ドキュメントレビュー統合レポート — UserManagementService 詳細設計書

## 判定結果
- **対象**: `design-docs/user-management-design.md`
- **判定**: ⚠️ **Conditional Approval** — High 指摘あり、人間の判断を介在
- **レビュー日時**: 2026-04-03
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)
- **イテレーション**: check-report-3

## 技術スタック検証結果
| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| ランタイム | .NET 10 LTS | .NET 10 (LTS) | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| ORM | EF Core 10 (暗黙) | Entity Framework Core 10 | ✅ |
| DB | PostgreSQL (userdb) | PostgreSQL | ✅ |
| メッセージング | Confluent.Kafka 2.* | Apache Kafka (Confluent.Kafka) 2.* | ✅ |
| キャッシュ | 未記載 | Redis (StackExchange.Redis) | ⚠️ 後述 |
| オーケストレーション | .NET Aspire 13.1 | .NET Aspire 13.1 | ✅ |
| テスト | xUnit, NSubstitute, Shouldly, Testcontainers | xUnit, NSubstitute, Shouldly, Testcontainers | ✅ |
| ロギング | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| 可観測性 | OpenTelemetry | OpenTelemetry | ✅ |
| ヘルスチェック | AspNetCore.HealthChecks.NpgSql 9.* | AspNetCore.HealthChecks.NpgSql 9.* | ✅ |
| 耐障害性 | 未記載 | Polly 8.* / Microsoft.Extensions.Http.Resilience 9.* | ⚠️ 後述 |

## 指摘サマリー
| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ⚠️ | 0 | 1 | 2 | 1 |
| architect | ⚠️ | 0 | 2 | 2 | 0 |
| tech-lead | ⚠️ | 0 | 1 | 1 | 1 |
| programing-reviewer | ⚠️ | 0 | 1 | 2 | 1 |
| security-reviewer | ⚠️ | 0 | 2 | 1 | 0 |
| dba-reviewer | Pass | 0 | 0 | 2 | 1 |
| qa-manager | ⚠️ | 0 | 1 | 1 | 0 |
| performance-reviewer | ⚠️ | 0 | 1 | 2 | 0 |
| compliance-reviewer | ⚠️ | 0 | 1 | 1 | 1 |
| oss-reviewer | Pass | 0 | 0 | 1 | 0 |
| release-manager | Pass | 0 | 0 | 1 | 1 |
| infra-ops-reviewer | Pass | 0 | 0 | 2 | 0 |
| audit-reviewer | Pass | 0 | 0 | 1 | 1 |
| ux-accessibility-reviewer | Pass | 0 | 0 | 0 | 1 |
| **合計** | | **0** | **10** | **19** | **8** |

## 判定根拠
- Critical 指摘: 0 件（自動 Rejected の条件に該当しない）
- High 指摘: 10 件（人間の判断が必要）
- 最も重大な指摘: spec.md に定義された Redis キャッシュ戦略の未反映、Polly リトライポリシー未定義、`PasswordChanged` Kafka イベントの購読欠落、GDPR データエクスポート形式の未定義

---

## Critical/High 指摘一覧（修正必須）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| H-01 | High | architect | イベント設計 | spec.md L503 で定義された `AuthService → Kafka「PasswordChanged」→ UserManagementService` イベントの購読が設計書に記載されていない。購読イベント一覧（§5）に `password.changed` が欠落している | 購読イベント一覧に `password.changed` を追加し、受信時のアクション（全セッション無効化の連携処理等）を定義する |
| H-02 | High | architect | キャッシュ戦略 | spec.md のユーザー管理サービス定義（L513-514）では Redis がデータストアとして記載されているが、設計書§2 の NuGet パッケージに `StackExchange.Redis` が含まれず、§3 のコンポーネントアーキテクチャ図にも Redis が存在しない。キャッシュ戦略が未定義 | Redis キャッシュの用途（セッション情報、ユーザープロファイルキャッシュ等）をコンポーネント図と NuGet パッケージリストに追加する。AGENTS.md §11.1 に基づき `StackExchange.Redis 2.*` + `AspNetCore.HealthChecks.Redis 9.*` を追加 |
| H-03 | High | security-reviewer | GDPR | データポータビリティ（§6 GDPR Art.20）でエクスポート形式が「JSON 形式」とのみ記載されているが、具体的なスキーマ定義・含まれるデータ項目が未定義。GDPR ではデータ主体が「構造化された、一般的に使用され、機械可読な形式」でのデータ受領権を有する | エクスポート JSON の具体的スキーマ（User, Address, Wishlist, Consent, Activity 等の全 PII）を定義し、PCI DSS 対象データ（決済情報）が含まれないことを明示する |
| H-04 | High | security-reviewer | 認証・認可 | 管理者向け API（§4）の `PUT /api/v1/admin/users/{id}/processing-restriction` は GDPR Art.18 に基づく重要な操作だが、管理者の操作ログ（監査ログ）の記録要件が明示されていない。誰が・いつ・どのユーザーの処理制限を設定/解除したかのトレーサビリティが必要 | 管理者操作（ステータス変更・処理制限設定/解除）に対する監査ログの記録要件を明示する。`UserActivity` テーブルに管理者操作種別を追加、または専用の `AdminAuditLog` テーブルを設計する |
| H-05 | High | business-analyst | ビジネスロジック | ウィッシュリストからカートへの移動 API（spec.md L1551 `POST /wishlists/{id}/items/{itemId}/cart`）が設計書の API 設計（§4）に定義されていない。spec.md には明示されているがエンドポイントテーブルに欠落している | `POST /api/v1/users/{userId}/wishlists/{id}/items/{itemId}/cart` エンドポイントを §4 に追加し、WishlistService に `MoveToCartAsync` メソッドを定義する |
| H-06 | High | programing-reviewer | 実装コード | MemberRankEvaluationService（§14）の実装例で Advisory Lock の取得を `ExecuteSqlRawAsync` で行っているが、戻り値の判定が不正確。`ExecuteSqlRawAsync` は影響を受けた行数を返すため、`pg_try_advisory_lock` の `true/false` 戻り値の判定には不適切。`FromSqlRaw` + `SingleAsync` または `SqlQueryRaw<bool>` を使用すべき | Advisory Lock 取得のコード例を修正し、`context.Database.SqlQueryRaw<bool>("SELECT pg_try_advisory_lock(hashtext('member_rank_eval'))").SingleAsync(ct)` 等の安全な実装パターンを示す |
| H-07 | High | performance-reviewer | 耐障害性 | AGENTS.md §11.1 で全外部 HTTP 通信に必須とされる Polly リトライ/サーキットブレーカーポリシーが設計書に一切記載されていない。UserManagementService は直接の外部 HTTP 呼出しは少ないが、MailSendService への Kafka イベント発行失敗時のフォールバック戦略が未定義 | §8 パフォーマンス最適化に耐障害性セクションを追加し、Outbox パブリッシャーの障害時リトライ戦略・外部サービス障害時のフォールバック方針を定義する |
| H-08 | High | qa-manager | テスト戦略 | テスト戦略（§10）に `DataExportService` BackgroundService のテスト計画が欠落している。GDPR データエクスポートは法的義務であり、エクスポートデータの完全性を検証するテストが不可欠 | §10 テスト戦略に DataExportService のテスト（エクスポートデータの全 PII 含有確認、大量データ時のパフォーマンステスト）を追加する |
| H-09 | High | compliance-reviewer | GDPR | DSR データ削除フロー（§6）で、削除完了後の **確認通知** がユーザーに送信される設計が欠落している。GDPR Art.12(3) ではデータ主体の要求に対して「不当な遅延なく、いかなる場合も受領後 1 ヶ月以内に」対応＋通知する義務がある | DSR 完了時に `MailSendService` 経由でユーザーにデータ削除完了通知メールを送信するフローを追加する。`user.deletion.completed` 集約完了後のアクションとして定義 |
| H-10 | High | tech-lead | 規約整合性 | spec.md のユーザー管理サービス主要エンティティ（L653-672）に `passwordHash` が `User` の主要属性として記載されているが、設計書の User エンティティ（§14）には `passwordHash` カラムが存在しない。UserManagementService は認証情報を持たない（AuthService の責務）ため spec.md 側の記載が古い可能性があるが、SSOT との不整合として記録する | spec.md のユーザー管理サービス User エンティティから `passwordHash` を削除するか、UserManagementService 側で保持しない旨の注記を追加する。テックリードに spec.md 修正の判断を求める |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-01 | 高優先 | architect | spec.md のコンポーネント図（L522-540）と設計書のコンポーネント図が構造的に異なる（spec.md にはキャッシュサービス・Redis 接続が描画されている）。Redis を含める設計に統一するか、spec.md を修正するか判断が必要 | テックリード |
| E-02 | 通常 | business-analyst | NPS 測定のデータ保存先が spec.md (L225) で `UserManagementService` と指定されているが、設計書に `nps_responses` テーブルの定義が存在しない。Phase 2 スコープのため現時点では不要だが、テーブル予約の要否を判断 | PO / テックリード |
| E-03 | 高優先 | compliance-reviewer | DSR 削除フローの「14日間猶予期間」が GDPR の 1 ヶ月期限とどう整合するか。猶予期間 14 日 + 処理期間 16 日 = 30 日で GDPR 期限内に収まる想定だが、処理失敗時の延長申請手順が未定義 | 法務 / DPO |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| — | — | — | 本レビューで Agent 間の矛盾した指摘は検出されなかった | — | — |

---

## ドキュメント横断分析

### マイクロサービス設計書カバレッジ（UserManagementService 関連）
| 観点 | 設計書 | spec.md | 整合性 |
|------|--------|---------|--------|
| Aggregate Root（User） | User + Address, UserPreference（§3） | User + Address, UserPreference（L339） | ✅ |
| Value Object（EmailAddress, PhoneNumber, PostalAddress） | 未明示的に定義 | L363-365 で定義 | ⚠️ 設計書で Value Object の使用を明示推奨 |
| DB 名（userdb） | userdb（§3） | ADR-0006 に準拠 | ✅ |
| ポート | 5002（§11 Dockerfile） | 5002（AGENTS.md マイクロサービス一覧） | ✅ |
| Kafka イベント発行 | user.deleted, user.profile-updated, consent.revoked | user.deleted, ProfileUpdated | ⚠️ イベント名の表記が一部異なる |
| Kafka イベント購読 | user.registered, user.deletion.completed, order.confirmed, inventory.stock_updated | user.registered, PasswordChanged | ⚠️ H-01 参照 |
| FK 制約 | §3 FK 制約テーブル | L2794 ユーザー管理サービス FK 制約 | ⚠️ spec.md に Wishlist, MemberRank, Consent, DeletionRequest の FK 未記載 |
| CHECK 制約 | §3 CHECK 制約テーブル | L2946-2948 で member_ranks のみ記載 | ⚠️ 設計書が詳細、spec.md 側が不足 |

### サービス間整合性
- **API 契約**: spec.md のウィッシュリスト API（L1541-1552）と設計書 §4 が概ね一致するが、`POST /wishlists/{id}/items/{itemId}/cart`（カートへの移動）が設計書に欠落（H-05）
- **Kafka イベント**: `user.profile-updated` イベントのペイロードスキーマは設計書で明示的に定義済み。spec.md 側での購読元（AiSupportService）との整合性は確認済み
- **会員ランク × PointService**: spec.md L2272 で PointService が `MemberRankUpdated` イベントで会員ランクを事前同期する設計だが、設計書の発行イベント一覧（§5）に `member-rank.updated` イベントが未定義

### 記載カバレッジ分析
| セクション | 網羅性 | 備考 |
|-----------|--------|------|
| §1 概要 | ✅ 十分 | AuthService との責務分離が明確 |
| §2 技術スタック | ⚠️ 部分不足 | Redis, Polly, Http.Resilience パッケージ欠落 |
| §3 DB 設計 | ✅ 十分 | ER 図、FK 制約、CHECK 制約、インデックス設計が包括的 |
| §4 API 設計 | ⚠️ 部分不足 | ウィッシュリスト→カート移動 API 欠落、ページネーション設計の一部未定義 |
| §5 イベント設計 | ⚠️ 部分不足 | `password.changed` 購読、`member-rank.updated` 発行が欠落 |
| §6 セキュリティ設計 | ⚠️ 部分不足 | GDPR エクスポートスキーマ未定義、DSR 完了通知欠落 |
| §7 エラー処理 | ✅ 十分 | RFC 9457 準拠、例外マッピング完備 |
| §8 パフォーマンス | ⚠️ 部分不足 | Redis キャッシュ戦略、Polly レジリエンス設計が欠落 |
| §9 監視・ロギング | ✅ 十分 | PII マスキング、Correlation ID、構造化ログ対応 |
| §10 テスト戦略 | ⚠️ 部分不足 | DataExportService テスト計画欠落 |
| §11 デプロイメント | ✅ 十分 | Dockerfile、環境変数、Aspire 設定完備 |
| §14 実装参考コード | ✅ 充実 | エンティティ、Endpoints、Service、BackgroundService の実装例が豊富 |
| 追記セクション A-L | ✅ 非常に充実 | AppDbContext、DTO、Repository IF、Service IF、FluentValidation、会員ランクロジックが網羅的 |

### 未定義・曖昧な領域
| 領域 | 影響 | 推奨 |
|------|------|------|
| Redis キャッシュ戦略 | 実装時にキャッシュ対象・TTL・無効化戦略が不明 | §8 にキャッシュ設計セクションを追加 |
| `member-rank.updated` イベント | PointService がランク情報を同期できない | §5 発行イベントに追加 |
| `password.changed` 購読 | パスワード変更時の連携処理が未実装になるリスク | §5 購読イベントに追加 |
| データエクスポート JSON スキーマ | GDPR Art.20 への適合性が検証不能 | §6 にスキーマ定義を追加 |
| 住所の最大登録数 | DoS 対策として上限が必要 | §4 API 設計またはビジネスルールに上限を定義（例: 10件） |
| ウィッシュリストの最大登録数 | 同上 | 同上（例: ウィッシュリスト 5件、アイテム 100件/リスト） |

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### business-analyst レビュー

**判定**: ⚠️ High 指摘あり

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| BA-H01 | High | spec.md L1551 に定義された `POST /wishlists/{id}/items/{itemId}/cart`（ウィッシュリストからカートへの移動）が設計書 §4 API エンドポイントに欠落。ユーザーストーリー「ウィッシュリストの商品をカートに入れて購入したい」の実現に必要 |
| BA-M01 | Medium | 会員ランクの特典（§J）にポイント還元率のみ記載。spec.md L2601-2604 で定義された「送料無料ライン引き下げ」「先行セールアクセス」「誕生月 2 倍ポイント」「プラチナ専用クーポン」の実現方法が設計書に未記載 |
| BA-M02 | Medium | ユーザーアクティビティ API（§4）にページネーションパラメータの定義がない。`GET /api/v1/users/{id}/activities` は大量データを返す可能性があり、ページネーション必須 |
| BA-L01 | Low | ペルソナ 2（法人顧客）の「複数の配送先を登録し、一括注文したい」は Phase 2 スコープだが、現在の住所の最大登録数が未定義。Phase 1 でも個人ユーザーの住所数に上限を設けるべき |

</details>

<details>
<summary>architect レビューレポート</summary>

### architect レビュー

**判定**: ⚠️ High 指摘あり

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| AR-H01 | High | spec.md L503 の `PasswordChanged` Kafka イベント購読が設計書 §5 購読イベント一覧に欠落。AuthService → UserManagementService のイベント連携が不完全 |
| AR-H02 | High | spec.md L513-514 で定義された Redis（セッション情報、一時データ）が設計書のコンポーネントアーキテクチャ（§3）に存在しない。spec.md と設計書間のインフラ構成に不整合 |
| AR-M01 | Medium | spec.md L2272 で PointService が `MemberRankUpdated` イベントで会員ランクを事前同期する設計だが、UserManagementService の発行イベント（§5）に `member-rank.updated` が未定義。サービス間のイベント契約が不完全 |
| AR-M02 | Medium | DDD Value Object（spec.md L363-365: `EmailAddress`, `PhoneNumber`, `PostalAddress`）の使用が設計書のエンティティ定義（§14）で反映されていない。User エンティティの `Email` は `string` 型で定義されており、Value Object によるドメイン制約の内包がされていない |

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### tech-lead レビュー

**判定**: ⚠️ High 指摘あり

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| TL-H01 | High | spec.md ユーザー管理サービスの主要エンティティ（L653）に `passwordHash` が User 属性として記載されているが、設計書では User エンティティから除外（AuthService 責務のため正しい）。SSOT（spec.md）側の修正が必要 |
| TL-M01 | Medium | Program.cs 統合ビュー（§H）で一部の Endpoint マッピングがコメントアウトされている（`MapPreferenceEndpoints`, `MapActivityEndpoints`, `MapConsentEndpoints`, `MapDsrEndpoints`, `MapMemberRankEndpoints`, `MapAdminUserEndpoints`）。実装参考コードとしてコメントアウトの意図が不明確 |
| TL-L01 | Low | Aspire AppHost 設定（§11）の注意書きで「spec.md の AppHost 定義では userService に `.WithReference(redis)` は含まれない」と記載。Redis 未使用の正当性を裏付けるが、H-02 の指摘と矛盾しており、統一的な判断が必要 |

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### programing-reviewer レビュー

**判定**: ⚠️ High 指摘あり

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| PR-H01 | High | OutboxPublisher・MemberRankEvaluationService の Advisory Lock 取得コード（§14）で `ExecuteSqlRawAsync` の戻り値を `> 0` で判定しているが、`ExecuteSqlRawAsync` は影響行数を返す関数であり `pg_try_advisory_lock` の `boolean` 戻り値を正しく判定できない |
| PR-M01 | Medium | SaveChangesAsync オーバーライド（追記§A）で `updated_at` の自動更新を型ごとの `if/else if` で実装しているが、`IHasTimestamps` インターフェースを導入すれば重複コードを排除できる。spec.md L2641 でも `AuditableEntity` 基底クラスまたは `IHasTimestamps` インターフェースの適用が推奨されている |
| PR-M02 | Medium | User エンティティ（§14）に `[Timestamp] RowVersion` プロパティが定義されていないが、追記§L で楽観的ロック対象として明示されている。§14 のエンティティ定義と §L の追記に不整合があり、実装時に混乱の原因となる |
| PR-L01 | Low | Consent エンティティ（§14）の CHECK 制約に `status` カラムのチェック（`GRANTED/REVOKED`）が定義されているが、エンティティクラスには `Status` プロパティが存在せず `IsGranted` (bool) で管理。CHECK 制約テーブルの `consents.status` 定義とエンティティ定義に不整合がある |

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### security-reviewer レビュー

**判定**: ⚠️ High 指摘あり

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| SR-H01 | High | データポータビリティ（§6 GDPR Art.20）でエクスポート JSON のスキーマが未定義。どの PII が含まれるか、決済情報が除外されるか、エクスポートファイルの暗号化要件があるかが不明 |
| SR-H02 | High | 管理者操作（ステータス変更・処理制限設定/解除）の監査ログ記録要件が未定義。GDPR Art.5(2) のアカウンタビリティ原則に基づき、PII 処理の記録が必要 |
| SR-M01 | Medium | `POST /api/v1/anonymous-consents`（§4 同意管理 API）が認証不要（`AllowAnonymous`）で設計されているが、レート制限の記載がない。匿名エンドポイントはアビュースのリスクが高く、IP ベースのレート制限が必要 |

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### dba-reviewer レビュー

**判定**: ✅ Pass（Medium/Low のみ）

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| DB-M01 | Medium | `outbox_events` テーブルの肥大化対策（アーカイブ・削除ポリシー）が未定義。ADR-0005 で「処理済みイベントのアーカイブ/削除が必要」と記載されているが、具体的な保持期間・クリーンアップバッチの設計がない |
| DB-M02 | Medium | `user_activities` テーブルは INSERT のみで更新がない（追記§L で確認）が、大量蓄積時のパーティション設計が未検討。アクティビティログは時系列データであり、月次パーティションの検討を推奨 |
| DB-L01 | Low | `addresses` テーブルの `(user_id, is_default)` 複合インデックスで、同一ユーザーに複数の `is_default = true` が設定される制約がない。ビジネスロジックで防ぐか、部分ユニークインデックス `UNIQUE (user_id) WHERE is_default = true` で DB レベルの保証を推奨 |

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### qa-manager レビュー

**判定**: ⚠️ High 指摘あり

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| QA-H01 | High | テスト戦略（§10）にデータエクスポート（`DataExportService`）のテスト計画が欠落。GDPR データポータビリティは法的義務であり、エクスポートの完全性・正確性のテストが不可欠 |
| QA-M01 | Medium | テスト戦略（§10）に記載された「主要フロー 100% カバー」の「主要フロー」の定義が曖昧。ユーザー登録（Kafka イベント経由）→ プロファイル更新 → 住所 CRUD → DSR 削除の E2E フローテスト計画が具体化されていない |

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### performance-reviewer レビュー

**判定**: ⚠️ High 指摘あり

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| PF-H01 | High | Polly リトライ・サーキットブレーカーポリシーが設計書に未記載。AGENTS.md §11.1 で全外部 HTTP 通信に必須。UserManagementService は直接 HTTP 呼出しは少ないが、将来の外部サービス連携に備えた標準設計が必要 |
| PF-M01 | Medium | 負荷テスト基準（§8）で「通常時 500 req/s、ピーク時 2,000 req/s」とあるが、ユーザー管理サービスのトラフィック特性（読み取り多・書き込み少）を考慮した Read/Write 比率の想定が未記載 |
| PF-M02 | Medium | MemberRankEvaluationService（§14）の年次バッチで全 MemberRank レコードを一括処理する設計だが、ユーザー数が大規模（100 万件以上）になった場合のバッチ分割戦略が未定義 |

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### compliance-reviewer レビュー

**判定**: ⚠️ High 指摘あり

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| CO-H01 | High | DSR 削除完了後のユーザーへの確認通知が未設計。GDPR Art.12(3) では 1 ヶ月以内の対応＋通知が義務。削除完了メールの送信フローを定義すべき |
| CO-M01 | Medium | 同意管理（§6）で同意の「バージョン管理」として `version` カラムが定義されているが、ポリシーテキストの変更時にユーザーへの再同意要求フローが未定義。GDPR では同意は「明確な肯定的行為」に基づく必要があり、ポリシー変更時は再同意が必要 |
| CO-L01 | Low | `UserActivity` テーブルに `ipAddress` と `deviceInfo` が記録されているが、これらの個人データの保持期間が未定義。GDPR の「保存期間の制限」原則に基づき、保持期間と自動削除ポリシーを定義すべき |

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### oss-reviewer レビュー

**判定**: ✅ Pass（Medium のみ）

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| OS-M01 | Medium | §2 NuGet パッケージに `StackExchange.Redis`、`AspNetCore.HealthChecks.Redis`、`Polly`、`Microsoft.Extensions.Http.Resilience` が欠落。AGENTS.md §8 の必須パッケージリストと不整合 |

</details>

<details>
<summary>release-manager レビューレポート</summary>

### release-manager レビュー

**判定**: ✅ Pass（Medium/Low のみ）

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| RM-M01 | Medium | §12 データマイグレーションで「ゼロダウンタイムマイグレーション手順」が言及されているが具体的な Expand-Contract パターンの適用方法が未定義 |
| RM-L01 | Low | §11 Dockerfile で `EXPOSE 5002` だが、AGENTS.md の Dockerfile 規約（§12.5）では `EXPOSE 8080` が標準。ポート番号の不整合 |

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### infra-ops-reviewer レビュー

**判定**: ✅ Pass（Medium のみ）

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| IO-M01 | Medium | ヘルスチェック（§H Program.cs）で PostgreSQL のみチェック。Kafka ブローカーへの疎通確認が Ready ヘルスチェックに含まれていない。OutboxPublisher が Kafka に依存するため、Kafka ヘルスチェックの追加を推奨 |
| IO-M02 | Medium | 自動スケーリング設定（§12）で「CPU 使用率 70% 超過時」とあるが、Azure Container Apps の具体的なスケーリングルール（min/max replicas、scale rule の Kafka キューベース等）が未定義 |

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### audit-reviewer レビュー

**判定**: ✅ Pass（Medium/Low のみ）

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| AU-M01 | Medium | Correlation ID ミドルウェア（§H Program.cs）は実装されているが、Kafka イベント間の Correlation ID 伝搬設計が未定義。`user.registered` → プロファイル初期化の一連のフローで Correlation ID が途切れる |
| AU-L01 | Low | 改訂履歴が設計書に含まれていない。spec.md には改訂履歴テーブルが存在するが、個別設計書にも同様の変更追跡が推奨される |

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### ux-accessibility-reviewer レビュー

**判定**: ✅ Pass（Low のみ）

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| UX-L01 | Low | ユーザープロファイル更新 API（§4）のレスポンスに `updatedAt` フィールドが含まれていない（UserDto に `UpdatedAt` なし）。フロントエンドで「最終更新日時」を表示する場合に不足 |

</details>

---

## 総評

本設計書は前回レビュー（check-report-2）からの修正を受け、追記セクション A-L で AppDbContext、DTO、Repository IF、Service IF、FluentValidation、BackgroundService、会員ランクロジックなど実装に必要な定義が大幅に充実した。特に以下の点が高く評価される:

1. **GDPR/DSR 対応**: 14 日間猶予期間、DsrTimeoutMonitorService、処理制限（Art.18）が詳細に設計されている
2. **会員ランク**: spec.md のランク定義・昇格降格ルールが忠実に反映され、`MemberRankCalculator` のコード例まで提供されている
3. **Outbox パターン**: ADR-0005 準拠の実装例がコード付きで提供されている
4. **FluentValidation**: 全リクエスト DTO に対するバリデーター定義が完備している

残存する High 指摘 10 件のうち、spec.md との整合性に関するもの（H-01, H-02, H-05, H-10）と GDPR 対応の深化に関するもの（H-03, H-04, H-08, H-09）に分類される。いずれも設計書単体の致命的な欠陥ではなく、修正可能な範囲である。
