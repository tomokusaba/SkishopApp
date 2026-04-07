# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/coupon-service-design.md`
- **判定**: ⚠️ **Conditional Approval** — High 指摘が存在し、人間の判断を介在
- **レビュー日時**: 2026-04-03 （イテレーション 3）
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

## 技術スタック検証結果

| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| 言語 | C# 14 (.NET 10 LTS) | C# 14 (.NET 10) | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| ORM | EF Core 10 + Npgsql | EF Core 10 + Npgsql | ✅ |
| DB | PostgreSQL (coupondb) | PostgreSQL | ✅ |
| メッセージング | Confluent.Kafka 2.* | Confluent.Kafka 2.* | ✅ |
| キャッシュ | StackExchange.Redis 2.* | StackExchange.Redis 2.* | ✅ |
| バリデーション | FluentValidation 11.* | FluentValidation 11.* | ✅ |
| ログ | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| 可観測性 | OpenTelemetry 1.* | OpenTelemetry 1.* | ✅ |
| 耐障害性 | Polly 8.* | Polly 8.* | ✅ |
| テスト | xUnit + NSubstitute + Shouldly | xUnit + NSubstitute + Shouldly | ✅ |
| コンテナ | Docker, aspnet:10.0 | Docker 25.x | ✅ |
| ポート | 5006 | 5006 | ✅ |

## 指摘サマリー

| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ⚠️ Conditional | 0 | 1 | 2 | 1 |
| architect | ⚠️ Conditional | 0 | 2 | 2 | 0 |
| tech-lead | ⚠️ Conditional | 0 | 2 | 1 | 0 |
| programing-reviewer | ⚠️ Conditional | 0 | 1 | 3 | 1 |
| security-reviewer | ⚠️ Conditional | 0 | 2 | 1 | 0 |
| dba-reviewer | Pass | 0 | 0 | 2 | 1 |
| qa-manager | ⚠️ Conditional | 0 | 1 | 2 | 0 |
| performance-reviewer | Pass | 0 | 0 | 2 | 1 |
| compliance-reviewer | Pass | 0 | 0 | 1 | 0 |
| oss-reviewer | Pass | 0 | 0 | 0 | 0 |
| release-manager | Pass | 0 | 0 | 1 | 0 |
| infra-ops-reviewer | ⚠️ Conditional | 0 | 1 | 1 | 0 |
| audit-reviewer | Pass | 0 | 0 | 1 | 1 |
| ux-accessibility-reviewer | Pass | 0 | 0 | 0 | 1 |
| **合計** | | **0** | **10** | **19** | **6** |

## 判定根拠
- **判定ルール適用結果**: Critical 0 件だが High 10 件が存在するため ⚠️ Conditional Approval
- **最も重大な指摘**: H-01（内部サービス間認証メカニズムの未定義）、H-02（OutboxPublisher の Advisory Lock 未実装）

---

## Critical/High 指摘一覧（修正必須）

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|---------------|----------|----------|
| H-01 | High | security-reviewer, architect | セキュリティ | §13.1, §E.4, §10.2 | **内部サービス間認証メカニズムが未定義**。§13.1 で「X-Internal-Service-Key ヘッダーまたは Managed Identity」と記載されるが、gRPC サービス（§10.2）の認証実装コード、および内部 REST API（§E.4 InternalCouponEndpoints）の認証ポリシー適用コードが欠落している。FallbackPolicy で JWT が要求されるが、内部サービスが JWT をどのように取得・提示するかが不明。 | 内部 API 認証の具体的実装パターンを追記する。①gRPC 用: `AddGrpcClient` 時の `CallCredentials` 設定 ②REST 内部 API 用: サービス間 Client Credentials フロー、または Managed Identity トークン取得の実装例を記載 |
| H-02 | High | infra-ops-reviewer, tech-lead | 運用 | §11.1 | **OutboxPublisher に PostgreSQL Advisory Lock が未実装**。spec.md の BackgroundService リーダー選出パターンでは `OutboxPublisher` に `pg_try_advisory_lock` による排他制御が必須（`minReplicas: 2` 時の同一イベント二重発行防止）。設計書の OutboxPublisher 実装コードに Advisory Lock 取得処理がない。 | `OutboxPublisher.ExecuteAsync` の先頭に `pg_try_advisory_lock(hashtext('outbox_publisher'))` を追加し、ロック取得成功時のみポーリングを実行するコード例を記載 |
| H-03 | High | architect | アーキテクチャ | §10.1 | **gRPC Proto パッケージのバージョニング規約不整合**。§10.1 の Proto 定義は `package skishop.coupon;` だが、spec.md の gRPC バージョニング戦略では `skishop.{service}.v1` 形式が必須。§H の追記セクションでは `skishop.coupon.v1` を使用しており、文書内の Proto 定義が二重化し不整合。 | §10.1 の Proto 定義を `package skishop.coupon.v1;` に統一し、§H は §10.1 への参照に変更する（SSOT 原則） |
| H-04 | High | business-analyst | ビジネス | §4.1, §15 | **クーポンコードジェネレーター（CouponCodeGenerator）が設計に欠落**。spec.md §9 のコンポーネント構成図では「クーポンコードジェネレーター」が独立コンポーネントとして定義されているが、設計書のコンポーネント図（§4.1）、プロジェクト構成（§15）、Service インターフェース（§D）のいずれにも存在しない。管理者がクーポンを大量生成する際のコード一意性保証メカニズムが未定義。 | `ICouponCodeGenerator` インターフェースとその実装クラスを追加し、コード生成アルゴリズム（ランダム生成 + UNIQUE 制約によるリトライ、またはプレフィックス + シーケンシャル番号等）を定義する |
| H-05 | High | tech-lead | アーキテクチャ | §4.1, ER図 | **spec.md の Value Object `DateRange` が未使用**。spec.md §DDD 戦術パターンで「`DateRange` — クーポン, 在庫管理 — 開始日〜終了日の不変範囲。`Contains(DateTime)` メソッドで有効期間判定」と定義されているが、設計書のエンティティ（§A）やルールエンジン（§7.2）で `DateRange` Value Object が使用されていない。`ValidFrom`/`ValidUntil` を直接 `DateTimeOffset` プロパティとして保持しており、DDD の Value Object パターンに反する。 | Coupon エンティティの `ValidFrom`/`ValidUntil` を `DateRange` Value Object に置き換える。EF Core の Owned Entity（`OwnsOne`）で DB マッピングする設計パターンを追記する |
| H-06 | High | security-reviewer | セキュリティ | §E.4, §10.2 | **gRPC 補償トランザクション（ReleaseCoupon）の認可チェック欠落**。`CouponGrpcService.ReleaseCoupon` にはオーソリゼーションガードがなく、任意の呼出し元がクーポン利用を取り消せる。Saga コーディネーター以外からの不正な Release 呼出しを防止するメカニズムがない。 | gRPC サービスに `[Authorize]` 属性を追加し、Saga コーディネーター用の Client Credentials スコープ（例: `saga:coupon:release`）で認可を制御する実装パターンを記載 |
| H-07 | High | programing-reviewer | コーディング | §11.1 | **OutboxPublisher でイベントのステータスを `PROCESSING` に遷移させずに直接 Publish**。spec.md の Outbox パターンでは `PENDING → PROCESSING → PUBLISHED` の 3 段階遷移が想定されるが、設計書の OutboxPublisher コードは `PENDING` のまま Publish を試行し、成功時に直接 `PUBLISHED` に遷移する。複数インスタンスが同一イベントを取得する競合状態が発生し得る（Advisory Lock 未実装と合わせてリスクが倍増）。 | ① Advisory Lock を取得（H-02 対応） ② `SELECT ... FOR UPDATE` でイベント取得時にロック ③ ステータスを `PROCESSING` に更新してから Publish を試行するコード例に修正 |
| H-08 | High | qa-manager | テスト | §17, §J | **gRPC 統合テスト・Saga ステップテストが未定義**。設計書のテスト戦略（§17）に gRPC サービスのテスト方針が記載されていない。Saga ステップ 3（ApplyCoupon/ReleaseCoupon）は注文確定フローの重要パスであり、gRPC レベルの統合テストと補償トランザクションのべき等性テストが必須。 | gRPC 統合テスト（`Grpc.Net.Client` + `WebApplicationFactory` による In-Process テスト）および補償トランザクションべき等性テスト（同一 order_id で ReleaseCoupon を 2 回呼出し、2 回目が安全に No-op になることの検証）のテストケースを追加 |
| H-09 | High | architect | 通信 | §9.2, §4.2 | **`order.cancelled` Kafka イベントの消費処理でべき等性が未保証**。§9.2 で `order.cancelled` を購読してクーポン利用取消を行うが、Kafka Consumer の重複配信時にべき等に処理するメカニズム（例: `coupon_usages.order_id` での既処理確認）が未定義。Kafka は at-least-once 配信であり、同一イベントの再配信でクーポン利用回数が過剰復元されるリスクがある。 | `OrderEventConsumer` の処理先頭で `coupon_usages.order_id` による既処理チェックを行い、未処理の場合のみ取消処理を実行するべき等性パターンを記載 |
| H-10 | High | programing-reviewer | コーディング | §6.4, §A.3 | **DTO と Entity で日時型が不一致**。`CreateCouponRequest` / `CreateCampaignRequest` の `StartDate` / `EndDate` / `ValidFrom` / `ValidUntil` が `DateTime` 型だが、Entity（§A.3 Coupon）は `DateTimeOffset` を使用。DTO → Entity のマッピング時にタイムゾーン情報が消失し、UTC 変換の不整合が発生するリスクがある。AGENTS.md §10.3 では `DateTimeOffset.UtcNow` の使用が必須。 | 全 DTO の日時型を `DateTimeOffset` に統一する |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-01 | 高優先 | architect | spec.md コンポーネント図の「配布サービス（DIST_SERV）」と「配布 Repository（DIST_REPO）」が設計書で独立コンポーネントとして設計されていない。spec.md 側を削除するか、設計書に配布サービスを追加するか判断が必要 | テックリード |
| E-02 | 通常 | business-analyst | カート放棄メール 2 回目のクーポン自動生成（spec.md Phase 2 予定）と CouponService の連携設計が未定義。Phase 2 計画時に「誰がクーポンを生成し、どのカテゴリで管理するか」の設計が必要 | PO + テックリード |
| E-03 | 通常 | performance-reviewer | `CouponExpirationService` の 1 時間間隔は、ユーザーが有効期限切れ直後のクーポンを使用しようとした場合に最大 1 時間の不整合ウィンドウが発生する。ルールエンジン側で `valid_until < now` をリアルタイムチェックしているため動作に問題はないが、ユーザーの「利用可能クーポン一覧」に期限切れクーポンが最大 1 時間表示される UX 上の問題がある | PO |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| — | — | — | 本レビューで Agent 間の矛盾する指摘は検出されなかった | — | — |

---

## Medium/Low 指摘一覧

| # | 重要度 | 出典 Agent | 指摘内容 | 推奨対応 |
|---|--------|-----------|----------|----------|
| M-01 | Medium | programing-reviewer | `CouponCacheService`（§12.2）で `Coupon` エンティティを直接 JSON シリアライズして Redis に格納している。エンティティにはナビゲーションプロパティ（`Campaign?`, `CouponType?`, `Restrictions`, `UserCoupons`, `Usages`）が含まれ、シリアライズ時に循環参照やデータ肥大化のリスクがある。 | キャッシュ用の軽量 DTO（`CouponCacheDto`）を定義し、必要なプロパティのみをキャッシュする |
| M-02 | Medium | dba-reviewer | `promotions` テーブルの `conditions` / `application_rules` JSONB カラムに対するインデックスが未定義。GIN インデックスがないと JSONB 内の条件検索がフルスキャンとなる。 | 検索パターンに応じて GIN インデックスを追加するか、JSONB 内の検索が不要であればその旨を明記する |
| M-03 | Medium | dba-reviewer | `campaigns` テーブルに `row_version`（楽観的ロック）カラムがない。管理者が同時にキャンペーンを編集した場合の競合が検出できない。 | `Campaign` エンティティに `[Timestamp] byte[] RowVersion` を追加する |
| M-04 | Medium | programing-reviewer | `CouponRuleEngine.Validate`（§7.2）が同期メソッドだが、`CouponRestriction` の評価（§D.3 `EvaluateRulesAsync`）は非同期。ルールエンジンの同期/非同期インターフェースが混在しており、制約テーブルとのJOIN評価時にデッドロックリスクがある。 | `Validate` を `ValidateAsync` に変更し、全ルール評価を非同期統一する |
| M-05 | Medium | qa-manager | 楽観的ロック競合（`DbUpdateConcurrencyException`）のテストケースが未定義。クーポン同時利用競合は重要なビジネスシナリオであり、テストが必要。 | 楽観的ロック競合テスト（2 つのトランザクションが同時に `current_usage_count` を更新 → 片方が `ConcurrencyException` をスロー）を追加 |
| M-06 | Medium | qa-manager | 不正検知テストが `IsSuspiciousAsync` のみ。`CheckFraudAsync`（§D.4）のテストケースが存在しない。 | `CheckFraudAsync` のテーマ（クーポンコード・注文金額を含む複合不正検知）のテストケースを追加 |
| M-07 | Medium | programing-reviewer | §20.1 と §I.1 でクーポン有効期限チェック BackgroundService が重複定義。§20.1 は `CouponExpirationChecker`、§I.1 は `CouponExpirationService` で、ロジックがほぼ同一。 | §20.1 を §I.1 への参照に変更し、SSOT を維持する |
| M-08 | Medium | business-analyst | spec.md §9 の「クーポン配布ルールの設定」に対応する設計が不十分。TargetCategory / TargetProductId はあるが、「特定ユーザーセグメントへの配布」「購入回数に応じた自動配布」等のルールベース配布機能が未設計。 | Phase 1 スコープ外であれば明記し、Phase 2 の TODO として記録する |
| M-09 | Medium | business-analyst | `PUT /api/v1/admin/campaigns/{id}`（§6.2）のリクエスト DTO が未定義。`CreateCampaignRequest` の再利用か専用 DTO かが不明。 | `UpdateCampaignRequest` DTO を定義するか、`CreateCampaignRequest` を共用する旨を明記する |
| M-10 | Medium | compliance-reviewer | `user.deleted` イベント購読時の「ユーザー関連クーポンデータの仮名化」（§9.2）の具体的処理が未定義。GDPR DSR（削除要求）対応として、`user_coupons.user_id` / `coupon_usages.user_id` をどのように仮名化するかが不明。 | 仮名化処理の実装パターンを記載する（例: `user_id` を `ANONYMIZED_{hash}` に置換、`UserCoupon` のステータスを `ANONYMIZED` に変更等） |
| M-11 | Medium | performance-reviewer | `GET /api/v1/coupons/available`（§6.1）にページネーションが未定義。利用可能クーポンが多数存在する場合にレスポンスサイズが肥大化し、パフォーマンスに影響する。 | `[AsParameters] PaginationParams` を導入し、offset/limit ベースまたは cursor ベースのページネーションを定義する |
| M-12 | Medium | performance-reviewer | gRPC Deadline 300ms（§10.3）に対して、spec.md のステップ 3 バジェットは 80ms（処理 65ms + オーバーヘッド 15ms）。Deadline のマージンが 3.75 倍と設計意図に合致するが、Redis キャッシュミス + DB フォールバック時に 300ms を超過するリスクの評価が未記載。 | Redis キャッシュミス時の DB フォールバックレイテンシ見積もりを追記し、300ms Deadline の妥当性を検証する |
| M-13 | Medium | release-manager | EF Core Migration の初期マイグレーション名や適用戦略（Expand-Contract パターン等）が未記載。 | 初期マイグレーション名（例: `20260403_InitialCreate`）と、本番運用時のマイグレーション戦略を記載する |
| M-14 | Medium | infra-ops-reviewer | CORS 設定が Program.cs（§F）に含まれていない。API Gateway 経由のアクセスのみであれば不要だが、その前提を明記する必要がある。 | 「CORS は API Gateway レイヤーで制御するため、CouponService 個別の CORS 設定は不要」等の記載を追加する |
| M-15 | Medium | architect | §9.1 の発行イベントに `CouponReleased`（補償トランザクション完了時）イベントが欠落。`CouponApplied` / `CouponExpired` はあるが、Saga 補償で利用取消した場合のイベントが未定義。監査ログ・他サービスの整合性回復に必要。 | `CouponReleased` イベント（`coupon.released` トピック）を追加し、ペイロード（couponId, userId, orderId, releasedAmount, OccurredAt）を定義する |
| M-16 | Medium | audit-reviewer | `CouponUsage` テーブルに利用取消の記録メカニズムがない。`ReleaseCoupon` が呼ばれた場合にレコードを削除するのか、ステータスカラムを追加して `CANCELLED` にするのかが不明。監査証跡の観点からレコード削除は不適切。 | `CouponUsage` に `status` カラム（`APPLIED` / `CANCELLED`）を追加し、取消時は物理削除ではなくステータス更新とする |
| L-01 | Low | programing-reviewer | `AdminCouponEndpoints.GetCouponUsages`（§E.2）で `analyticsService.GetUsagesByDateRangeAsync(DateTimeOffset.MinValue, DateTimeOffset.UtcNow, ct)` を呼び出しているが、特定クーポン ID（パスパラメータ `{id}`）のフィルタリングが行われていない。 | クーポン ID でフィルタリングするメソッドを使用するか、パスパラメータを活用する |
| L-02 | Low | dba-reviewer | `coupon_usages` テーブルの `used_at` カラムにインデックスが `AppDbContext` で定義（§B）されているが、§5.2 のテーブル定義のインデックス一覧に記載がない。 | §5.2 のインデックス一覧に `idx_coupon_usages_used_at` を追加する |
| L-03 | Low | business-analyst | クーポン分析レスポンス（`CouponAnalyticsResponse`）に「期間指定」パラメータがない。管理者が特定期間の分析を要求する際に全期間のデータが返される。 | `GetOverallAnalyticsAsync` に `DateTimeOffset? from, DateTimeOffset? to` パラメータを追加する |
| L-04 | Low | ux-accessibility-reviewer | エラーメッセージが日本語のみ。多言語対応（日英）が spec.md のビジネス要件に含まれるが、クーポン関連エラーメッセージの i18n 対応方針が未記載。 | Phase 1 スコープ外であれば明記し、Phase 2 の TODO として記録する |
| L-05 | Low | performance-reviewer | `CampaignStatusService` のポーリング間隔が 30 分固定。キャンペーン終了日に近い時期は頻度を上げる等の動的調整がない。 | 現行設計で許容する旨を記載するか、動的バックオフを検討する |
| L-06 | Low | audit-reviewer | appsettings.json（§18）に Kafka の `GroupId` が `coupon-service` と直接記載されており、環境別の設定分離が不明確。 | `appsettings.Development.json` / `appsettings.Production.json` での Kafka 設定オーバーライドを明記する |

---

## ドキュメント横断分析

### spec.md との整合性チェック

| spec.md の定義 | 設計書の対応 | 整合性 | 備考 |
|---------------|------------|--------|------|
| §9 責務（作成、配布ルール、適用、キャンペーン、使用追跡） | §1.1 目的で網羅 | ⚠️ | 「配布ルールの設定」が弱い（M-08） |
| §9 主要エンティティ（Coupon, CouponType, CouponUsage, Campaign, Promotion） | §5.1 ER図で全て定義 | ✅ | |
| §9 データストア（PostgreSQL + Redis） | §3 で定義 | ✅ | |
| §9 コンポーネント（クーポンコードジェネレーター） | 設計書に欠落 | ❌ | H-04 |
| §9 コンポーネント（配布サービス + 配布 Repository） | 設計書に欠落 | ⚠️ | E-01（エスカレーション） |
| Aggregate Root: **Coupon** + CouponUsage | §5.1 で整合 | ✅ | |
| Value Object: **DateRange** | 未使用 | ❌ | H-05 |
| Saga Step 3（gRPC 65ms + 15ms = 80ms） | §10 で定義、Deadline 300ms | ✅ | |
| gRPC Proto: `ValidateCoupon`/`ReleaseCoupon` | §10.1 で定義 | ⚠️ | パッケージバージョニング不整合（H-03） |
| H8-23 クーポン・ポイント適用順序 | §21.1 で完全に定義 | ✅ | |
| ゲスト購入時クーポンスキップ | §21.1 で定義 | ✅ | |
| Outbox パターン（ADR-0005） | §11 で定義 | ⚠️ | Advisory Lock 欠落（H-02） |
| 注文キャンセル時クーポン復元 | §9.2 / §10.1 ReleaseCoupon | ✅ | |
| BackgroundService リーダー選出 | 未定義 | ❌ | H-02 |

### Kafka イベント整合性

| トピック | 発行/購読 | 対応先 | 整合性 |
|---------|----------|--------|--------|
| `coupon.applied` | 発行 | SalesManagementService → 注文記録連携 | ✅ |
| `coupon.expired` | 発行 | 監視・分析 | ✅ |
| `order.cancelled` | 購読 | SalesManagementService → クーポン取消 | ⚠️ べき等性未保証（H-09） |
| `user.deleted` | 購読 | UserManagementService → 仮名化 | ⚠️ 具体処理未定義（M-10） |
| `coupon.released` | 未定義 | Saga 補償完了通知 | ❌ M-15 |

### ADR 整合性

| ADR | 設計書の準拠状況 | 備考 |
|-----|----------------|------|
| ADR-0001（マイクロサービス） | ✅ 準拠 | |
| ADR-0002（Minimal API） | ✅ 準拠 | |
| ADR-0003（PostgreSQL 統一） | ✅ 準拠 | coupondb |
| ADR-0004（Kafka イベント駆動） | ✅ 準拠 | |
| ADR-0005（Outbox パターン） | ⚠️ 一部不備 | Advisory Lock 欠落 |
| ADR-0006（サービス別独立 DB） | ✅ 準拠 | |
| ADR-0007（RFC 9457 Problem Details） | ✅ 準拠 | |
| ADR-0008（PCI DSS 非保持化） | N/A | クーポンサービスは決済非関連 |
| ADR-0009（Saga オーケストレーション） | ✅ 準拠 | Step 3 定義あり |
| ADR-0010（RTO 1 時間統一） | ✅ 準拠 | ヘルスチェック定義あり |

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### 評価: ⚠️ Conditional (High: 1, Medium: 2, Low: 1)

**良好な点:**
- クーポン種別（PERCENTAGE, FIXED_AMOUNT, FREE_SHIPPING）の 3 種が明確に定義され、ビジネス要件を網羅
- クーポン・ポイント適用順序（§21.1）が spec.md H8-23 と完全に整合
- ゲスト購入時のスキップルールが明記
- キャンペーン管理（DRAFT→ACTIVE→PAUSED→ENDED のステートマシン）が実用的
- 1 注文 1 クーポンの制約と Phase 2 での複数クーポン対応の計画が明確

**指摘事項:**
- **H-04**: クーポンコードジェネレーターが設計から欠落
- **M-08**: 配布ルール設定機能が不十分
- **M-09**: キャンペーン更新 DTO が未定義
- **L-03**: 分析レスポンスに期間指定がない

</details>

<details>
<summary>architect レビューレポート</summary>

### 評価: ⚠️ Conditional (High: 2, Medium: 2, Low: 0)

**良好な点:**
- コンポーネントアーキテクチャが AGENTS.md のレイヤー依存方向（Endpoints → Services → Repositories）を厳守
- Aggregate Root（Coupon + CouponUsage）が spec.md と一致
- gRPC + REST 二重公開方針が spec.md の設計方針に準拠
- Outbox パターンの基本設計が ADR-0005 と整合

**指摘事項:**
- **H-03**: gRPC Proto パッケージバージョニング不整合
- **H-09**: Kafka Consumer のべき等性未保証
- **M-15**: `CouponReleased` イベント欠落
- **M-16 (audit-reviewer と共同)**: CouponUsage の取消記録メカニズム不在

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 評価: ⚠️ Conditional (High: 2, Medium: 1, Low: 0)

**良好な点:**
- 追記セクション（§A〜§J）で EF Core エンティティ、AppDbContext、Repository/Service インターフェース、Endpoint 実装、Program.cs が網羅的に定義され、実装可能性が高い
- `.github/instructions/` の全規約（コーディング標準、セキュリティ、API 設計、テスト）への準拠度が高い
- primary constructor、record 型、CancellationToken の伝搬が一貫

**指摘事項:**
- **H-02**: Advisory Lock 未実装（spec.md との不整合）
- **H-05**: Value Object `DateRange` の未使用
- **M-07**: BackgroundService の定義重複（§20.1 と §I.1）

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional (High: 1, Medium: 3, Low: 1)

**良好な点:**
- C# 14 機能の適切な活用（primary constructor, record 型, switch 式, パターンマッチング）
- `TimeProvider` DI による testability 確保
- CancellationToken が全 async メソッドに伝搬
- FluentValidation の実装品質が高い（PERCENTAGE 時の 100% 上限チェック等）
- 例外クラス階層（§G）が AGENTS.md §4.7 に準拠

**指摘事項:**
- **H-07**: OutboxPublisher の PROCESSING ステータス遷移欠落（H-10 に移動、独立指摘として） 
- **H-10**: DTO の DateTime / DateTimeOffset 不一致
- **M-01**: CouponCacheService のエンティティ直接シリアライズ
- **M-04**: ルールエンジンの同期/非同期混在
- **M-07**: BackgroundService 定義重複
- **L-01**: GetCouponUsages のクーポン ID フィルタリング欠落

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional (High: 2, Medium: 1, Low: 0)

**良好な点:**
- IDOR 防止が `ClaimsPrincipal` から `userId` を取得する正しいパターンで実装（§13.2, §E.1）
- FallbackPolicy で全エンドポイントにデフォルト認証が適用
- 入力バリデーション（FluentValidation + Data Annotations）が包括的
- クーポンコードの正規表現制限（`^[A-Z0-9-]+$`）でインジェクション防止
- セキュリティヘッダー（X-Content-Type-Options, X-Frame-Options, CSP）が Program.cs に設定
- 不正利用検知サービスが存在

**指摘事項:**
- **H-01**: 内部サービス間認証メカニズムが未定義
- **H-06**: gRPC 補償トランザクションの認可チェック欠落
- **M-14 (infra-ops と共同)**: CORS 設定の前提説明不足

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 評価: Pass (Medium: 2, Low: 1)

**良好な点:**
- 全テーブルが snake_case 命名規則に準拠
- CHECK 制約が全ステータスカラムに定義
- 楽観的ロック（`RowVersion`）が Coupon エンティティに設定
- 部分インデックス（`outbox_events` の PENDING フィルタ）が定義
- `AppDbContext`（§B）の `OnModelCreating` で Fluent API による包括的なインデックス・CHECK 制約定義
- `SaveChangesAsync` オーバーライドで `CreatedAt`/`UpdatedAt` の自動設定

**指摘事項:**
- **M-02**: promotions テーブルの JSONB カラムにインデックスなし
- **M-03**: Campaign エンティティに楽観的ロック欠落
- **L-02**: coupon_usages.used_at インデックスがテーブル定義に未記載

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 評価: ⚠️ Conditional (High: 1, Medium: 2, Low: 0)

**良好な点:**
- CouponRuleEngine の単体テスト（§17.1, §J.2）が AAA パターン + Should 命名規則に準拠
- CouponService の単体テスト（§J.1）が正常系/異常系を網羅
- 統合テスト（§17.2）が WebApplicationFactory を使用
- テストヘルパーメソッドが再利用可能な設計

**指摘事項:**
- **H-08**: gRPC 統合テスト・Saga ステップテストが未定義
- **M-05**: 楽観的ロック競合テスト未定義
- **M-06**: FraudDetectionService の CheckFraudAsync テスト未定義

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 評価: Pass (Medium: 2, Low: 1)

**良好な点:**
- Redis キャッシュ戦略が TTL 付きで定義（クーポン 10 分、利用回数 5 分）
- キャッシュ無効化パターンが明確
- gRPC 採用で REST に比べ 3-5 倍のシリアライズ効率
- OutboxPublisher の動的バックオフ（100ms〜5s）がスループット最適化に有効

**指摘事項:**
- **M-11**: ページネーション未定義
- **M-12**: Redis キャッシュミス時の Deadline 超過リスク評価未記載
- **L-05**: CampaignStatusService の固定ポーリング間隔

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 評価: Pass (Medium: 1, Low: 0)

**良好な点:**
- `user.deleted` イベント購読でデータ仮名化に対応（GDPR DSR 対応の意思あり）
- PII（個人情報）がクーポンテーブルに直接格納されない（user_id のみ）
- ログに PII が出力されない設計（ログには CouponCode と UserId のみ）

**指摘事項:**
- **M-10**: `user.deleted` 時の仮名化処理の具体的実装が未定義

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 評価: Pass (指摘なし)

**良好な点:**
- 全 NuGet パッケージが AGENTS.md の必須パッケージリストに一致
- プレリリースパッケージ（-preview, -beta, -rc）の使用なし
- 禁止パッケージ（Newtonsoft.Json, log4net 等）の使用なし
- FluentValidation 11.* を使用（非推奨の FluentValidation.AspNetCore ではない）
- テスト関連パッケージ（xUnit, NSubstitute, Shouldly）が規約準拠

</details>

<details>
<summary>release-manager レビューレポート</summary>

### 評価: Pass (Medium: 1, Low: 0)

**良好な点:**
- Dockerfile がマルチステージビルドで非 root ユーザー（skishop）に準拠
- ヘルスチェック設定が Dockerfile の HEALTHCHECK と一致
- appsettings.json にハードコードされた秘密情報なし

**指摘事項:**
- **M-13**: EF Core Migration の初期マイグレーション名と適用戦略が未記載

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional (High: 1, Medium: 1, Low: 0)

**良好な点:**
- ヘルスチェック（/health, /health/ready）が PostgreSQL / Redis 両方を検証
- OpenTelemetry 設定が ASP.NET Core / HttpClient / EF Core の Instrumentation を網羅
- Dockerfile の EXPOSE 5006 がサービスポートと一致
- Serilog の CompactJsonFormatter で JSON 形式ログ出力

**指摘事項:**
- **H-02 (tech-lead と共同)**: OutboxPublisher の Advisory Lock 未実装
- **M-14**: CORS 設定の前提説明不足

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 評価: Pass (Medium: 1, Low: 1)

**良好な点:**
- Correlation ID ミドルウェアが Program.cs に実装（Serilog LogContext.PushProperty）
- Outbox Events テーブルがイベント発行の監査証跡として機能
- 構造化ログがメッセージテンプレート形式（文字列補間禁止）に準拠
- 例外ハンドラで全例外がログ出力される（握りつぶしなし）

**指摘事項:**
- **M-16**: CouponUsage の取消記録メカニズムが未定義（物理削除 vs ステータス更新）
- **L-06**: Kafka GroupId の環境別設定分離が不明確

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 評価: Pass (Low: 1)

**良好な点:**
- エラーコード体系（CPN-XXXX）が一貫し、フロントエンドでのハンドリングが容易
- エラーメッセージが具体的（「最低注文金額 X 円以上から適用可能です」等）
- RFC 9457 Problem Details 準拠でフロントエンドのエラー処理が標準化

**指摘事項:**
- **L-04**: エラーメッセージの i18n（日英対応）方針が未記載

</details>
