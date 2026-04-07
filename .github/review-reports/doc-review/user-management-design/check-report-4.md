# ドキュメントレビュー統合レポート — UserManagementService 詳細設計書

## 判定結果
- **対象**: `design-docs/user-management-design.md`
- **判定**: ✅ **Approved with Notes** — Critical/High 指摘なし、推奨改善事項あり
- **レビュー日時**: 2026-04-03
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)
- **イテレーション**: check-report-4（Iteration 3 の High 10 件修正後の再レビュー）

## 段階的実行モード
- イテレーション: 4 回目
- 実行 Agent: 全 14 Agent（全 Agent が前回 Medium 以上の指摘あり → Active）
- スキップ Agent（Stable）: なし（3 回連続ゼロを満たす Agent なし）
- 実行理由: Active（全 Agent が前回 C/H/M 指摘あり）

## 技術スタック検証結果
| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| ランタイム | .NET 10 LTS | .NET 10 (LTS) | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| ORM | EF Core 10 (暗黙) | Entity Framework Core 10 | ✅ |
| DB | PostgreSQL (userdb) | PostgreSQL | ✅ |
| メッセージング | Confluent.Kafka 2.* | Apache Kafka (Confluent.Kafka) 2.* | ✅ |
| キャッシュ | StackExchange.Redis 2.* (§2, §8) | Redis (StackExchange.Redis) | ✅ |
| オーケストレーション | .NET Aspire 13.1 | .NET Aspire 13.1 | ✅ |
| テスト | xUnit, NSubstitute, Shouldly, Testcontainers | xUnit, NSubstitute, Shouldly, Testcontainers | ✅ |
| ロギング | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| 可観測性 | OpenTelemetry | OpenTelemetry | ✅ |
| ヘルスチェック | AspNetCore.HealthChecks.NpgSql 9.*, Redis 9.* | AspNetCore.HealthChecks.NpgSql 9.*, Redis 9.* | ✅ |
| 耐障害性 | Polly 8.*, Microsoft.Extensions.Http.Resilience 9.* | Polly 8.* / Microsoft.Extensions.Http.Resilience 9.* | ✅ |

## Iteration 3 High 指摘の修正確認

| # | 指摘 | 修正確認 | 修正箇所 |
|---|------|---------|---------|
| H-01 | password.changed Kafka 購読イベント欠落 | ✅ 修正済 | §5 購読イベント一覧に追加。受信アクション（PASSWORD_CHANGED アクティビティ記録、lastLoginAt リセット、updatedAt 更新によるキャッシュ無効化）を詳細定義。§M に `PasswordChangedEvent` record 定義 |
| H-02 | Redis キャッシュ戦略未定義 | ✅ 修正済 | §8 に Redis キャッシュ戦略セクション追加（キャッシュ対象/キーパターン/TTL/無効化トリガー）。§2 NuGet に StackExchange.Redis, AspNetCore.HealthChecks.Redis 追加。§3 Mermaid に CACHE 追加。§11 Aspire に `.WithReference(redis)` 追加 |
| H-03 | GDPR データエクスポート JSON スキーマ未定義 | ✅ 修正済 | §6 に JSON Schema (draft/2020-12) 準拠の完全なスキーマ定義を追加。User, Address, Wishlist, Preference, Consent, Activity, MemberRank の全 PII を網羅。PCI DSS 除外事項・AES-256 暗号化・ダウンロード URL 有効期限 (24h) も明記 |
| H-04 | 管理者操作監査ログ要件未定義 | ✅ 修正済 | §6 に「管理者操作監査ログ」セクション追加。ADMIN_STATUS_CHANGED, ADMIN_PROCESSING_RESTRICTION_SET/REMOVED, ADMIN_DELETION_REQUESTED の 4 種別を定義。details JSONB 構造、コード例、保持期間（最低 3 年）を明記 |
| H-05 | ウィッシュリスト→カート移動 API 欠落 | ✅ 修正済 | §4 ウィッシュリスト API に `POST /api/v1/users/{userId}/wishlists/{id}/items/{itemId}/cart` 追加。§G WishlistEndpoints に `MoveItemToCart` 実装例追加。§F IWishlistService に `MoveItemToCartAsync` メソッド定義 |
| H-06 | Advisory Lock boolean 戻り値の不正確な判定 | ✅ 修正済 | §14 OutboxPublisher, MemberRankEvaluationService の両方で `SqlQueryRaw<bool>("SELECT pg_try_advisory_lock(...)").SingleAsync(ct)` パターンに修正 |
| H-07 | Polly リトライ/サーキットブレーカー未定義 | ✅ 修正済 | §8 に「耐障害性（Resilience）設計」セクション追加。IHttpClientFactory + Polly v8 `AddStandardResilienceHandler` コード例、Outbox パブリッシャー障害時リトライ戦略テーブル、Kafka Consumer 障害時フォールバックテーブルを定義 |
| H-08 | DataExportService テスト計画欠落 | ✅ 修正済 | §10 に「DataExportService テスト戦略」テーブル追加。Unit Test（スキーマ準拠、全 PII 含有、PCI DSS 除外、AES-256 暗号化）、Integration Test（大量データ 60 秒以内、ダウンロード URL 記録）、Security Test（URL 有効期限）を定義 |
| H-09 | DSR 削除完了通知フロー欠落 | ✅ 修正済 | §5 発行イベントに `user.deletion.notification` 追加。§6 に DSR 削除完了通知フローの Mermaid シーケンス図追加。GDPR Art.12(3) 準拠の 30 日期限を明記。§M に `UserDeletionNotificationEvent` record 定義 |
| H-10 | AuthService 責務分担注記 | ✅ 修正済 | §14 User エンティティ定義の直前に「spec.md との不整合に関する注記」を追加。passwordHash 非保持の理由と spec.md 修正推奨を明記 |

**結論: Iteration 3 の High 10 件は全て適切に修正されている。**

---

## 指摘サマリー
| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | Pass | 0 | 0 | 2 | 1 |
| architect | Pass | 0 | 0 | 1 | 0 |
| tech-lead | Pass | 0 | 0 | 1 | 0 |
| programing-reviewer | Pass | 0 | 0 | 4 | 1 |
| security-reviewer | Pass | 0 | 0 | 1 | 0 |
| dba-reviewer | Pass | 0 | 0 | 2 | 1 |
| qa-manager | Pass | 0 | 0 | 1 | 0 |
| performance-reviewer | Pass | 0 | 0 | 2 | 0 |
| compliance-reviewer | Pass | 0 | 0 | 1 | 1 |
| oss-reviewer | Pass | 0 | 0 | 0 | 0 |
| release-manager | Pass | 0 | 0 | 1 | 1 |
| infra-ops-reviewer | Pass | 0 | 0 | 3 | 0 |
| audit-reviewer | Pass | 0 | 0 | 1 | 1 |
| ux-accessibility-reviewer | Pass | 0 | 0 | 0 | 1 |
| **合計** | | **0** | **0** | **20** | **7** |

## 判定根拠
- Critical 指摘: 0 件
- High 指摘: 0 件（前回 10 件 → 全件修正済）
- Medium 指摘: 20 件（前回 19 件から 5 件解消、6 件新規検出）
- Low 指摘: 7 件（前回 8 件から 1 件解消）
- 判定ルール適用: Critical/High なし → **✅ Approved with Notes**
- 全 14 Agent が Pass 判定

---

## Medium 指摘一覧（推奨改善事項）

### 前回から継続（15 件）

| # | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|-----------|---------|----------|----------|
| M-01 | business-analyst | ビジネスロジック | 会員ランク特典（§J）にポイント還元率のみ記載。spec.md L2601-2604 の「送料無料ライン引き下げ」「先行セールアクセス」「誕生月 2 倍ポイント」「プラチナ専用クーポン」の実現方法が未記載 | §J に特典の実現サービス（CouponService/SalesManagementService 連携）を注記 |
| M-02 | business-analyst | API 設計 | `GET /api/v1/users/{id}/activities` にページネーションパラメータ（`page`, `pageSize`）の定義がない（§4）。大量データ返却の可能性あり | §4 API テーブルにクエリパラメータを追記 |
| M-03 | architect | DDD | DDD Value Object（spec.md L363-365: `EmailAddress`, `PhoneNumber`, `PostalAddress`）が設計書のエンティティ定義（§14）で反映されていない。User の `Email` は `string` 型 | 実装時に Value Object の導入を検討（設計書に TODO 注記） |
| M-04 | tech-lead | 実装参考コード | §H Program.cs で `MapPreferenceEndpoints`, `MapActivityEndpoints`, `MapConsentEndpoints`, `MapDsrEndpoints`, `MapMemberRankEndpoints`, `MapAdminUserEndpoints` がコメントアウトされており、意図が不明確 | コメントアウトの意図（段階実装計画等）を注記するか、コメントを解除 |
| M-05 | programing-reviewer | コード品質 | §A SaveChangesAsync で `updated_at` 自動更新を型ごとの `if/else if` で実装。`IHasTimestamps` インターフェース導入で重複コード排除可能（spec.md L2641 でも推奨） | `IHasTimestamps` インターフェースを定義して適用 |
| M-06 | programing-reviewer | 整合性 | §14 User エンティティ定義に `[Timestamp] RowVersion` プロパティがないが、§L で楽観的ロック対象として明示。§14 と §L に不整合 | §14 エンティティ定義に `RowVersion` プロパティを追記 |
| M-07 | security-reviewer | セキュリティ | `POST /api/v1/anonymous-consents`（§4）が認証不要で設計されているが、レート制限の記載がない。匿名エンドポイントはアビュースリスク高 | IP ベースのレート制限を §4 または §6 に明記 |
| M-08 | dba-reviewer | データ管理 | `outbox_events` テーブルの肥大化対策（保持期間・クリーンアップバッチ）が未定義。ADR-0005 で言及されている | §12 運用・保守に Outbox イベント保持ポリシーを追加 |
| M-09 | dba-reviewer | データ管理 | `user_activities` テーブルは INSERT のみだが、大量蓄積時のパーティション設計が未検討。時系列データのため月次パーティション推奨 | §3 DB 設計にパーティション戦略の注記を追加 |
| M-10 | qa-manager | テスト戦略 | §10「主要フロー 100% カバー」の「主要フロー」定義が曖昧。E2E フロー（Kafka user.registered → プロファイル更新 → 住所 CRUD → DSR 削除）の具体的テスト計画が未詳細化 | 主要フローの定義リストを §10 に追加 |
| M-11 | performance-reviewer | パフォーマンス | 負荷テスト基準（§8）に Read/Write 比率の想定が未記載。ユーザー管理は読み取り多・書き込み少のトラフィック特性を持つ | Read/Write 比率（例: 90:10）を §8 に追記 |
| M-12 | performance-reviewer | スケーラビリティ | MemberRankEvaluationService（§14）の年次バッチで大規模データ（100 万件以上）時のバッチ分割戦略が未定義 | チャンク処理戦略（例: 1,000 件/バッチ）を §J または §14 に追記 |
| M-13 | compliance-reviewer | GDPR | 同意管理（§6）で `version` カラムによるバージョン管理は定義済みだが、ポリシーテキスト変更時のユーザーへの再同意要求フローが未定義。GDPR では同意は「明確な肯定的行為」が基盤 | ポリシー変更時の再同意フローを §6 に追加 |
| M-14 | release-manager | デプロイメント | §12「ゼロダウンタイムマイグレーション手順」の具体的な Expand-Contract パターン適用方法が未定義 | Expand-Contract の適用例を §12 に追記 |
| M-15 | audit-reviewer | トレーサビリティ | Kafka イベント間の Correlation ID 伝搬設計が未定義。`user.registered` → プロファイル初期化のフローで Correlation ID が途切れる可能性 | §9 監視・ロギングに Kafka メッセージヘッダーでの Correlation ID 伝搬設計を追加 |

### 今回新規検出（5 件）

| # | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|-----------|---------|----------|----------|
| M-16 | programing-reviewer | 整合性 | §14 `UserService.MapToDto` が 6 引数（Id, Email, FirstName, LastName, Status, CreatedAt）だが、§D の `UserDto` record は 10 プロパティ（PhoneNumber, BirthDate, ProcessingRestricted, LastLoginAt を含む）。マッピングの不一致 | §14 MapToDto のコード例を §D UserDto に合わせて更新 |
| M-17 | programing-reviewer | 整合性 | §F `IEventPublisherService` に `PublishDeletionNotificationAsync`（user.deletion.notification）と `PublishMemberRankUpdatedAsync`（member-rank.updated）のメソッドが未定義。§5 の発行イベント 5 件中 3 件のみインターフェースに定義 | §F に不足メソッドを追加 |
| M-18 | infra-ops-reviewer | 整合性 | §H Program.cs のヘルスチェック登録が PostgreSQL のみ（`.AddNpgSql`）。§8 では Redis ヘルスチェック（`.AddRedis`）も定義されているが §H に未反映。また Redis DI 登録（`AddStackExchangeRedisCache` 等）も §H に未記載 | §H Program.cs に Redis DI 登録とヘルスチェック追加を反映 |
| M-19 | infra-ops-reviewer | 整合性 | §H Program.cs の BackgroundService 登録に `DataExportService` が欠落（§3 クラス構成で BackgroundService として定義済み）。また `user.registered`, `password.changed`, `order.confirmed` の Kafka Consumer BackgroundService クラス定義・登録がない | §H に DataExportService 登録を追加。主要 Kafka Consumer の BackgroundService クラスを §I に追加 |
| M-20 | infra-ops-reviewer | スケーリング | §12 自動スケーリング設定で「CPU 使用率 70% 超過時」とあるが、Azure Container Apps の具体的スケーリングルール（min/max replicas, scale rule）が未定義 | スケーリングルールの具体値を §12 に定義 |

---

## Low 指摘一覧（任意対応）

| # | 出典 Agent | カテゴリ | 指摘内容 |
|---|-----------|---------|----------|
| L-01 | business-analyst | ビジネスルール | 住所の最大登録数が未定義。DoS 対策として上限（例: 10 件）の設定を推奨 |
| L-02 | programing-reviewer | 整合性 | §3 CHECK 制約テーブルに `consents.status IN ('GRANTED','REVOKED')` が定義されているが、Consent エンティティは `IsGranted` (bool) で管理。CHECK 制約定義とエンティティ定義に軽微な不整合 |
| L-03 | dba-reviewer | DB 設計 | `addresses` テーブルの `(user_id, is_default)` 複合インデックスで、同一ユーザーに複数の `is_default = true` を防ぐ部分ユニークインデックス `UNIQUE (user_id) WHERE is_default = true` の検討を推奨 |
| L-04 | compliance-reviewer | データ保持 | `UserActivity` テーブルの `ipAddress`, `deviceInfo` の保持期間が未定義。GDPR「保存期間の制限」原則に基づき自動削除ポリシーの定義を推奨 |
| L-05 | release-manager | Dockerfile | §11 Dockerfile で `EXPOSE 5002` だが、AGENTS.md の Dockerfile 規約（§12.5）では `EXPOSE 8080` が標準。ポート番号の確認を推奨 |
| L-06 | audit-reviewer | ドキュメント管理 | 改訂履歴テーブルが設計書に含まれていない。spec.md には改訂履歴が存在するため、個別設計書にも同様の変更追跡を推奨 |
| L-07 | ux-accessibility-reviewer | API 設計 | ユーザープロファイル更新レスポンス（§14 `UserService.MapToDto`）に `updatedAt` フィールドが含まれていない。フロントエンドの「最終更新日時」表示に不足 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-01 | 通常 | business-analyst | NPS 測定のデータ保存先が spec.md (L225) で `UserManagementService` と指定されているが、設計書に `nps_responses` テーブルの定義が存在しない。Phase 2 スコープのため現時点では不要だが、テーブル予約の要否を判断 | PO / テックリード |
| E-02 | 通常 | compliance-reviewer | DSR 削除フローの「14 日間猶予期間」＋「処理期間 16 日」= 30 日で GDPR 期限内に収まる想定。処理失敗時の延長申請手順が未定義 | 法務 / DPO |

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
| Value Object（EmailAddress, PhoneNumber, PostalAddress） | 未明示的に定義 | L363-365 で定義 | ⚠️ M-03 |
| DB 名（userdb） | userdb（§3） | ADR-0006 に準拠 | ✅ |
| ポート | 5002（§11 Dockerfile） | 5002（AGENTS.md マイクロサービス一覧） | ✅ |
| Kafka イベント発行 | user.deleted, user.profile-updated, user.deletion.notification, member-rank.updated, consent.revoked | user.deleted, ProfileUpdated, MemberRankUpdated | ✅ 設計書が上位互換 |
| Kafka イベント購読 | user.registered, password.changed, user.deletion.completed, order.confirmed, inventory.stock_updated | user.registered, PasswordChanged | ✅ 設計書が上位互換 |
| Redis キャッシュ | §2 NuGet, §3 Mermaid, §8 キャッシュ戦略, §11 Aspire | L513-514 Redis 記載 | ✅ |
| Polly / 耐障害性 | §2 NuGet, §8 Resilience 設計 | AGENTS.md §11.1 必須 | ✅ |

### サービス間整合性
- **API 契約**: spec.md のウィッシュリスト API（L1541-1552）と設計書 §4 が一致（ウィッシュリスト→カート移動 API 修正済）✅
- **Kafka イベント**: §5 の発行イベントに `member-rank.updated` が追加され、PointService（spec.md L2272）との連携が整合 ✅
- **password.changed 購読**: spec.md L503 の AuthService → UserManagementService 連携が §5 に反映 ✅
- **DSR 削除完了通知**: `user.deletion.notification` イベントによる MailSendService 連携が §5, §6 に定義 ✅

### 記載カバレッジ分析
| セクション | 網羅性 | 前回比 |
|-----------|--------|--------|
| §1 概要 | ✅ 十分 | → |
| §2 技術スタック | ✅ 十分 | ↑ Redis, Polly パッケージ追加 |
| §3 DB 設計 | ✅ 十分 | → |
| §4 API 設計 | ✅ 十分 | ↑ ウィッシュリスト→カート API 追加 |
| §5 イベント設計 | ✅ 十分 | ↑ password.changed 購読, user.deletion.notification/member-rank.updated 発行追加 |
| §6 セキュリティ設計 | ✅ 十分 | ↑ GDPR エクスポートスキーマ, 管理者監査ログ, DSR 通知フロー追加 |
| §7 エラー処理 | ✅ 十分 | → |
| §8 パフォーマンス | ✅ 十分 | ↑ Redis キャッシュ戦略, Polly 耐障害性設計追加 |
| §9 監視・ロギング | ✅ 十分 | → |
| §10 テスト戦略 | ✅ 十分 | ↑ DataExportService テスト計画追加 |
| §11 デプロイメント | ✅ 十分 | ↑ Aspire に Redis 参照追加 |
| §14 実装参考コード | ✅ 十分 | ↑ Advisory Lock 修正, AuthService 注記追加 |
| 追記 A-M | ✅ 非常に充実 | ↑ Kafka record 定義, PasswordChangedEvent 追加 |

### 未定義・曖昧な領域（Medium 以下のみ — ブロッカーなし）
| 領域 | 影響度 | 対応時期 |
|------|--------|---------|
| DDD Value Object の明示的使用 | 低（実装時に判断可能） | 実装フェーズ |
| §H Program.cs の Redis DI/ヘルスチェック反映 | 低（§8 に定義済み） | 実装フェーズ |
| Kafka Consumer BackgroundService の完全定義 | 低（イベント→アクション定義は §5 に完備） | 実装フェーズ |
| IEventPublisherService メソッド補完 | 低（§5, §M にイベント定義済み） | 実装フェーズ |
| 住所/ウィッシュリストの最大登録数 | 低（DoS 対策として実装時に設定） | 実装フェーズ |

---

## Iteration 3 → 4 の改善サマリー

| 指標 | Iteration 3 | Iteration 4 | 変化 |
|------|------------|------------|------|
| Critical | 0 | 0 | → |
| High | **10** | **0** | ✅ 全件解消 |
| Medium | 19 | 20 | △ 5 件解消, 6 件新規（修正に伴う内部整合性） |
| Low | 8 | 7 | ↓ 1 件解消 |
| 判定 | ⚠️ Conditional Approval | ✅ **Approved with Notes** | ✅ 格上げ |

### 解消された指摘（Iteration 3 → 4）
| 指摘 | 出典 | 解消理由 |
|------|------|---------|
| OS-M01: NuGet パッケージ欠落 | oss-reviewer | §2 に Redis, Polly, Http.Resilience 追加 |
| AR-M01: member-rank.updated 未定義 | architect | §5 発行イベントに追加 |
| TL-L01: Aspire Redis 参照不整合 | tech-lead | §11 に `.WithReference(redis)` 追加 |
| H-01〜H-10 (10 件) | 各 Agent | 全件修正済（上記修正確認テーブル参照） |

### 新規検出の主因
H-02（Redis 追加）と H-01/H-09（Kafka イベント追加）の修正に伴い、§8 に追加されたコードと §H Program.cs の間、および §5 に追加されたイベントと §F インターフェースの間に **内部整合性の差分** が発生した。これは修正箇所のみを更新し、関連する既存セクションへの波及更新が不完全であったことが原因であり、設計上の重大な問題ではない。

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### business-analyst レビュー

**判定**: ✅ Pass（Medium/Low のみ）

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| BA-M01 | Medium | 会員ランク特典（§J）にポイント還元率のみ記載。spec.md L2601-2604 の他特典（送料無料ライン等）の実現方法が未記載（前回 BA-M01 継続） |
| BA-M02 | Medium | ユーザーアクティビティ API（§4）にページネーションパラメータの定義がない（前回 BA-M02 継続） |
| BA-L01 | Low | 住所の最大登録数が未定義（前回 BA-L01 継続） |

**前回 BA-H01（ウィッシュリスト→カート移動 API）**: §4 に `POST /api/v1/users/{userId}/wishlists/{id}/items/{itemId}/cart` が追加され、§G に MoveItemToCart 実装例も完備。✅ 解消

</details>

<details>
<summary>architect レビューレポート</summary>

### architect レビュー

**判定**: ✅ Pass（Medium のみ）

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| AR-M01 | Medium | DDD Value Object（`EmailAddress`, `PhoneNumber`, `PostalAddress`）が §14 エンティティで未使用。User.Email は `string` 型のまま（前回 AR-M02 継続） |

**前回 AR-H01（password.changed 未定義）**: §5 購読イベントに `password.changed` が追加され、受信アクションも詳細定義。spec.md L503 との整合性確保。✅ 解消

**前回 AR-H02（Redis 未定義）**: §2 NuGet, §3 Mermaid, §8 キャッシュ戦略, §11 Aspire に一貫して Redis 追加。✅ 解消

**前回 AR-M01（member-rank.updated 未定義）**: §5 発行イベントに `member-rank.updated` 追加、§M に `MemberRankUpdatedEvent` record 定義。PointService（spec.md L2272）との連携が整合。✅ 解消

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### tech-lead レビュー

**判定**: ✅ Pass（Medium のみ）

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| TL-M01 | Medium | §H Program.cs で 6 件の Endpoint マッピングがコメントアウト。段階実装の意図を注記すべき（前回 TL-M01 継続） |

**前回 TL-H01（passwordHash 注記）**: §14 User エンティティ定義の直前に spec.md との不整合注記を追加。AuthService 責務であることを明確化。✅ 解消

**前回 TL-L01（Aspire Redis 参照不整合）**: §11 Aspire AppHost 設定に `.WithReference(redis)` 追加、注記も付与。✅ 解消

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### programing-reviewer レビュー

**判定**: ✅ Pass（Medium/Low のみ）

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| PR-M01 | Medium | §A SaveChangesAsync の `if/else if` 型判定。`IHasTimestamps` インターフェース導入推奨（前回 PR-M01 継続） |
| PR-M02 | Medium | §14 User エンティティに `[Timestamp] RowVersion` 未定義、§L で楽観的ロック対象として明示。セクション間不整合（前回 PR-M02 継続） |
| PR-M03 | Medium | **新規**: §14 `UserService.MapToDto` が `new(Id, Email, FirstName, LastName, Status, CreatedAt)` の 6 フィールドだが、§D `UserDto` は `PhoneNumber`, `BirthDate`, `ProcessingRestricted`, `LastLoginAt` を含む 10 フィールド。マッピング不一致 |
| PR-M04 | Medium | **新規**: §F `IEventPublisherService` に `PublishDeletionNotificationAsync`（user.deletion.notification）と `PublishMemberRankUpdatedAsync`（member-rank.updated）メソッドが未定義。§5 で定義された 5 つの発行イベントのうち 3 つのみインターフェースにメソッド定義 |
| PR-L01 | Low | §3 CHECK 制約の `consents.status` と Consent エンティティの `IsGranted` (bool) に軽微な不整合（前回 PR-L01 継続） |

**前回 PR-H01（Advisory Lock boolean 戻り値）**: §14 OutboxPublisher, MemberRankEvaluationService 共に `SqlQueryRaw<bool>("SELECT pg_try_advisory_lock(...)").SingleAsync(ct)` に修正。✅ 解消

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### security-reviewer レビュー

**判定**: ✅ Pass（Medium のみ）

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| SR-M01 | Medium | `POST /api/v1/anonymous-consents` が認証不要だが IP ベースレート制限の記載なし（前回 SR-M01 継続） |

**前回 SR-H01（GDPR エクスポートスキーマ未定義）**: §6 に JSON Schema (draft/2020-12) 準拠の完全定義を追加。全 PII 網羅、PCI DSS 除外、AES-256 暗号化、24h URL 有効期限を明記。✅ 解消

**前回 SR-H02（管理者操作監査ログ未定義）**: §6 に管理者操作 4 種別の監査ログ定義を追加。details JSONB 構造、コード例、保持期間 3 年を明記。GDPR Art.5(2) 準拠。✅ 解消

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### dba-reviewer レビュー

**判定**: ✅ Pass（Medium/Low のみ — 前回と同一）

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| DB-M01 | Medium | `outbox_events` テーブルの肥大化対策が未定義（前回 DB-M01 継続） |
| DB-M02 | Medium | `user_activities` テーブルの月次パーティション設計が未検討（前回 DB-M02 継続） |
| DB-L01 | Low | `addresses` テーブルの is_default 部分ユニークインデックス未定義（前回 DB-L01 継続） |

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### qa-manager レビュー

**判定**: ✅ Pass（Medium のみ）

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| QA-M01 | Medium | §10「主要フロー 100% カバー」の定義が曖昧。具体的な E2E フローリストの追記推奨（前回 QA-M01 継続） |

**前回 QA-H01（DataExportService テスト計画欠落）**: §10 に DataExportService テスト戦略テーブル追加。Unit Test 4 件（スキーマ準拠・全 PII 含有・PCI DSS 除外・AES-256 暗号化）、Integration Test 2 件（大量データ性能・ダウンロード URL 記録）、Security Test 1 件（URL 有効期限）を定義。✅ 解消

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### performance-reviewer レビュー

**判定**: ✅ Pass（Medium のみ）

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| PF-M01 | Medium | 負荷テスト基準（§8）に Read/Write 比率の想定が未記載（前回 PF-M01 継続） |
| PF-M02 | Medium | 年次バッチの大規模データ時のバッチ分割戦略が未定義（前回 PF-M02 継続） |

**前回 PF-H01（Polly リトライ未定義）**: §8 に「耐障害性（Resilience）設計」セクションを追加。IHttpClientFactory + Polly v8 設定コード例、Outbox パブリッシャー/Kafka Consumer 障害時のフォールバック戦略テーブルを定義。AGENTS.md §11.1 準拠。✅ 解消

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### compliance-reviewer レビュー

**判定**: ✅ Pass（Medium/Low のみ）

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| CO-M01 | Medium | ポリシーテキスト変更時の再同意要求フローが未定義（前回 CO-M01 継続） |
| CO-L01 | Low | `UserActivity` の `ipAddress`, `deviceInfo` の保持期間が未定義（前回 CO-L01 継続） |

**前回 CO-H01（DSR 削除完了通知欠落）**: §5 に `user.deletion.notification` イベント追加、§6 に DSR 削除完了通知フローの Mermaid シーケンス図追加。メールアドレスの一時保存設計、GDPR Art.12(3) 30 日期限を明記。✅ 解消

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### oss-reviewer レビュー

**判定**: ✅ Pass（指摘なし）

**前回 OS-M01（NuGet パッケージ欠落）**: §2 NuGet パッケージ一覧に `StackExchange.Redis 2.*`, `AspNetCore.HealthChecks.Redis 9.*`, `Polly 8.*`, `Microsoft.Extensions.Http.Resilience 9.*` が追加され、AGENTS.md §8 の必須パッケージリストと整合。✅ 解消

</details>

<details>
<summary>release-manager レビューレポート</summary>

### release-manager レビュー

**判定**: ✅ Pass（Medium/Low のみ — 前回と同一）

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| RM-M01 | Medium | Expand-Contract マイグレーションパターンの具体例が未定義（前回 RM-M01 継続） |
| RM-L01 | Low | §11 Dockerfile `EXPOSE 5002` と AGENTS.md 標準 `EXPOSE 8080` の不整合（前回 RM-L01 継続） |

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### infra-ops-reviewer レビュー

**判定**: ✅ Pass（Medium のみ）

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| IO-M01 | Medium | **更新**: §H Program.cs のヘルスチェック登録が PostgreSQL のみ（§8 では Redis ヘルスチェックも定義済み）。Redis DI 登録（`AddStackExchangeRedisCache` 等）も §H に未記載。H-02 修正時に §8 に追加されたが §H への波及が不完全 |
| IO-M02 | Medium | Azure Container Apps のスケーリングルール（min/max replicas, Kafka キューベース等）が未定義（前回 IO-M02 継続） |
| IO-M03 | Medium | **新規**: §H Program.cs の BackgroundService 登録に `DataExportService` が欠落。また `user.registered`, `password.changed`, `order.confirmed` イベントの Kafka Consumer BackgroundService クラス定義・登録がない（§5 では購読イベントとアクションを定義済みだが、消費インフラが未定義） |

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### audit-reviewer レビュー

**判定**: ✅ Pass（Medium/Low のみ — 前回と同一）

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| AU-M01 | Medium | Kafka イベント間の Correlation ID 伝搬設計が未定義（前回 AU-M01 継続） |
| AU-L01 | Low | 個別設計書に改訂履歴テーブルが存在しない（前回 AU-L01 継続） |

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### ux-accessibility-reviewer レビュー

**判定**: ✅ Pass（Low のみ — 前回と同一）

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| UX-L01 | Low | §14 UserService.MapToDto のレスポンスに `updatedAt` フィールドが含まれていない（前回 UX-L01 継続） |

</details>

---

## 総評

Iteration 3 で指摘された High 10 件が全て適切に修正され、設計書の品質は **Approved with Notes** レベルに到達した。特に以下の修正が設計の完成度を大きく向上させている:

1. **Redis キャッシュ戦略の完全定義（H-02）**: キャッシュ対象・キーパターン・TTL・無効化トリガーが明確になり、実装時の判断基準が確立された
2. **GDPR 対応の深化（H-03, H-04, H-09）**: エクスポート JSON スキーマ、管理者監査ログ、削除完了通知フローの追加により、GDPR Art.12/18/20 への適合性が大幅に向上した
3. **耐障害性設計の追加（H-07）**: Polly v8 統合、Outbox/Kafka 障害時フォールバック戦略の定義により、AGENTS.md §11.1 の要件を充足した
4. **Kafka イベント設計の完全化（H-01, H-09）**: password.changed 購読、user.deletion.notification/member-rank.updated 発行の追加により、サービス間のイベント契約が完全になった

残存する Medium 20 件は、大きく以下の 3 カテゴリに分類される:
- **ドキュメント内部整合性（M-04, M-06, M-16, M-17, M-18, M-19）**: 修正箇所と既存セクション間の波及更新漏れ。実装時に自然に解消される
- **設計詳細化の推奨（M-01, M-02, M-03, M-05, M-10, M-11, M-12, M-14, M-20）**: 実装品質を向上させる追加定義。実装フェーズで対応可能
- **運用・コンプライアンス改善（M-07, M-08, M-09, M-13, M-15）**: 運用開始前に対応すべきだが、実装を妨げるものではない

**本設計書は実装フェーズへの進行に十分な品質を有する。**
