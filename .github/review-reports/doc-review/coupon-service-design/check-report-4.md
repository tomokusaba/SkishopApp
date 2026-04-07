# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/coupon-service-design.md`
- **判定**: ⚠️ **Conditional Approval** — High 指摘が 1 件存在し、人間の判断を介在
- **レビュー日時**: 2026-04-03 16:30（イテレーション 4）
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

## 段階的実行モード
- イテレーション: 4 回目
- 実行 Agent: 全 14 Agent（business-analyst, architect, tech-lead, programing-reviewer, security-reviewer, dba-reviewer, qa-manager, performance-reviewer, compliance-reviewer, oss-reviewer, release-manager, infra-ops-reviewer, audit-reviewer, ux-accessibility-reviewer）
- スキップ Agent（Stable）: なし
- 実行理由: H-01〜H-10 の修正が認証・コード・エンティティ・テスト・Kafka 等の広範な領域に影響し、全 Agent が Active または Affected に該当

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

## H-01〜H-10 修正検証結果

| # | 修正内容 | 検証結果 | 備考 |
|---|---------|---------|------|
| H-01 | InternalServiceOnly 認証ポリシー追加 | ✅ 修正済み | §13.1.1 に Client Credentials フロー + InternalServiceHandler 実装が追記。ただし §F Program.cs にポリシー定義が未反映（M-17 として新規指摘） |
| H-02 | OutboxPublisher Advisory Lock 追加 | ⚠️ 部分修正 | §11.1 に pg_try_advisory_lock が追加されたが、`ExecuteSqlRawAsync` の戻り値セマンティクスが不正確（H-11 として新規指摘） |
| H-03 | gRPC Proto パッケージ統一 | ✅ 修正済み | §10.1 を `package skishop.coupon.v1;` に統一。§H は §10.1 を SSOT として参照 |
| H-04 | CouponCodeGenerator 追加 | ✅ 修正済み | §D.7 に ICouponCodeGenerator / CouponCodeGenerator 実装。§4.1 コンポーネント図にも追加。DI 登録あり |
| H-05 | DateRange Value Object 追加 | ✅ 修正済み | §D.8 に readonly record struct DateRange 定義。OwnsOne EF Core マッピング + 移行方針記載 |
| H-06 | gRPC Authorize 追加 | ✅ 修正済み | §10.2 のクラスレベルおよび ReleaseCoupon メソッドに `[Authorize(Policy = "InternalServiceOnly")]` 追加 |
| H-07 | OutboxPublisher ステータス遷移 3 段階化 | ✅ 修正済み | §11.1 の実装コードが PENDING → PROCESSING → PUBLISHED の 3 段階遷移 + `FOR UPDATE SKIP LOCKED` に更新 |
| H-08 | gRPC 統合テスト 4 件追加 | ✅ 修正済み | §J.3 に ApplyCoupon 成功、ReleaseCoupon 成功、べき等性テスト、無効コードテストの 4 件追加 |
| H-09 | order.cancelled べき等性保証追記 | ⚠️ 部分修正 | §J.3 末尾に「OrderEventConsumerTest で実施」と記載あるが、OrderEventConsumer の実装コード自体が設計書に欠落（M-20 として新規指摘） |
| H-10 | DTO 日時型を DateTimeOffset に統一 | ✅ 修正済み | §6.4 の DTO が DateTimeOffset に統一。ただし §17.1・§J.1 テストコードに DateTime.UtcNow が残留（M-18 として新規指摘） |

## 指摘サマリー

| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | Pass | 0 | 0 | 2 | 1 |
| architect | ⚠️ Conditional | 0 | 0 | 3 | 0 |
| tech-lead | ⚠️ Conditional | 0 | 1 | 2 | 0 |
| programing-reviewer | Pass | 0 | 0 | 5 | 1 |
| security-reviewer | Pass | 0 | 0 | 2 | 0 |
| dba-reviewer | Pass | 0 | 0 | 2 | 1 |
| qa-manager | Pass | 0 | 0 | 3 | 0 |
| performance-reviewer | Pass | 0 | 0 | 2 | 1 |
| compliance-reviewer | Pass | 0 | 0 | 1 | 0 |
| oss-reviewer | Pass | 0 | 0 | 0 | 0 |
| release-manager | Pass | 0 | 0 | 1 | 0 |
| infra-ops-reviewer | Pass | 0 | 0 | 1 | 0 |
| audit-reviewer | Pass | 0 | 0 | 1 | 1 |
| ux-accessibility-reviewer | Pass | 0 | 0 | 0 | 1 |
| **合計** | | **0** | **1** | **25** | **6** |

## 判定根拠
- **判定ルール適用結果**: Critical 0 件だが High 1 件が存在するため ⚠️ Conditional Approval
- **最も重大な指摘**: H-11（OutboxPublisher の `ExecuteSqlRawAsync` による Advisory Lock 取得が機能しない設計バグ）
- **前回比**: High 10 件 → 1 件（9 件解消、1 件新規）。全 H-01〜H-10 の修正意図は正しく反映されている

---

## Critical/High 指摘一覧（修正必須）

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|---------------|----------|----------|
| H-11 | High | tech-lead, programing-reviewer | コーディング | §11.1 | **OutboxPublisher の Advisory Lock 取得ロジックが機能しない設計バグ**。H-02 修正で追加された `ExecuteSqlRawAsync("SELECT pg_try_advisory_lock(hashtext('outbox_publisher'))")` は、EF Core の `ExecuteSqlRawAsync` が内部で `ExecuteNonQueryAsync` を呼び出すため、SELECT 文の結果ではなく「影響を受けた行数」を返す。PostgreSQL では SELECT 文に対して -1 が返却されるため、`if (acquired <= 0)` が**常に true** となりポーリングループに入れない。結果として Outbox イベントが一切発行されない。同様に finally ブロックの `pg_advisory_unlock` も戻り値が無視されるが、こちらは副作用として unlock は実行される。 | `ExecuteSqlRawAsync` を `SqlQueryRaw<bool>` または ADO.NET の `ExecuteScalarAsync` に変更し、`pg_try_advisory_lock` の boolean 戻り値を正しく取得する。修正例: `var lockResult = await context.Database.SqlQueryRaw<bool>("SELECT pg_try_advisory_lock(hashtext('outbox_publisher'))").FirstAsync(stoppingToken);` |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-01 | 高優先 | architect | spec.md コンポーネント図の「配布サービス（DIST_SERV）」と「配布 Repository（DIST_REPO）」が設計書で独立コンポーネントとして設計されていない。spec.md 側を削除するか、設計書に配布サービスを追加するか判断が必要（前回から継続） | テックリード |
| E-02 | 通常 | business-analyst | カート放棄メール 2 回目のクーポン自動生成（spec.md Phase 2 予定）と CouponService の連携設計が未定義（前回から継続） | PO + テックリード |
| E-03 | 通常 | performance-reviewer | `CouponExpirationService` の 1 時間間隔による最大 1 時間の不整合ウィンドウ（UX 上の問題）（前回から継続） | PO |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| — | — | — | 本レビューで Agent 間の矛盾する指摘は検出されなかった | — | — |

---

## Medium/Low 指摘一覧

### 新規指摘（Iteration 4 で検出）

| # | 重要度 | 出典 Agent | 指摘内容 | 推奨対応 |
|---|--------|-----------|----------|----------|
| M-17 | Medium | tech-lead, security-reviewer | **Program.cs §F の認可設定に `InternalServiceOnly` ポリシーが未反映**。§13.1.1 で定義された `InternalServiceOnly` ポリシー（`RequireClaim("client_id")` + `InternalServiceRequirement`）が §F の `AddAuthorization` ブロックに含まれていない。§F では `InternalServiceHandler` の DI 登録はあるが、ポリシー定義がないため、`RequireAuthorization("InternalServiceOnly")` を使用する全エンドポイント（§E.4, §10.2）が実行時に「Policy not found」エラーとなる。 | §F の `AddAuthorization` ブロックを §13.1.1 のコードと統一する（InternalServiceOnly ポリシー定義を追加） |
| M-18 | Medium | programing-reviewer, qa-manager | **テストコードに `DateTime.UtcNow` が残留**。H-10 で DTO を `DateTimeOffset` に統一したが、§17.1 テストヘルパー（`ValidFrom = DateTime.UtcNow.AddDays(-1)`, `ValidUntil = DateTime.UtcNow.AddDays(30)`）および §J.1 の `CreateCouponRequest` テストデータ（`ValidFrom: DateTime.UtcNow`）がまだ `DateTime.UtcNow` を使用。DTO が `DateTimeOffset` に変更されたため、テストコードもコンパイルエラーになる可能性がある。 | テストコード内の `DateTime.UtcNow` を全て `DateTimeOffset.UtcNow` に置換する |
| M-19 | Medium | programing-reviewer | **`ICouponUsageRepository.CountRecentUsagesAsync` のパラメータ型が不統一**。§C.3 のインターフェース定義で `DateTime since` を受け取るが、他の全ての日時型は `DateTimeOffset` に統一されている。§8.2 の呼出し元では `timeProvider.GetUtcNow().AddMinutes(-10).UtcDateTime` と `.UtcDateTime` で明示変換しているが、統一性に欠ける。 | パラメータを `DateTimeOffset since` に変更し、Repository 実装側で必要に応じて変換する |
| M-20 | Medium | architect, qa-manager | **`OrderEventConsumer` の実装コードが設計書に欠落**。§15 プロジェクト構成に `Consumers/OrderEventConsumer.cs` が記載され、§9.2 で `order.cancelled` の購読が定義されているが、消費処理の実装コード（BackgroundService + Consumer パターン）が文書内に存在しない。H-09 で要件とされた `coupon_usages.order_id` による既処理チェックのべき等性パターンも具体的コードとして未記載。 | `OrderEventConsumer` の BackgroundService 実装コードを追加する。AGENTS.md §10.6 の Kafka Consumer パターン（`IServiceScopeFactory` + `try/catch` + Dead Letter Topic）に準拠し、べき等性チェック（`order_id` 重複確認）を含むコード例を記載する |
| M-21 | Medium | architect, programing-reviewer | **gRPC Proto 定義の命名不整合（§10.1 vs §H）**。§10.1 では `service CouponService` + `rpc ValidateCoupon`、§H では `service CouponGrpcService` + `rpc ApplyCoupon`。§H は「§10.1 を SSOT とする」と宣言しつつ、異なるサービス名・RPC 名を定義している。§10.2 実装は `ValidateCoupon` を使用し、§J.3 テストは `ApplyCouponAsync` を呼び出しており、テストと実装が不整合。 | §10.1 の RPC 名を `ApplyCoupon` / `ReleaseCoupon` に統一するか、§H を削除して §10.1 のみとする。§10.2 実装と §J.3 テストの RPC 名を統一する |
| M-22 | Medium | security-reviewer | **Program.cs §F のセキュリティヘッダーに `Referrer-Policy` と `Permissions-Policy` が未設定**。`.github/instructions/security-coding.instructions.md` §3 では `Referrer-Policy: strict-origin-when-cross-origin` および `Permissions-Policy: camera=(), microphone=(), geolocation=()` が必須とされているが、§F のセキュリティヘッダーミドルウェアに含まれていない。 | §F のセキュリティヘッダーミドルウェアに `Referrer-Policy` と `Permissions-Policy` を追加する |
| M-23 | Medium | programing-reviewer | **gRPC テスト（§J.3）の Deadline 指定が `DateTime.UtcNow` を使用**。`deadline: DateTime.UtcNow.AddMilliseconds(300)` は gRPC C# クライアントの API として正しいが、テスト内で相対時間（300ms 後）を固定的に指定しており、CI 環境の負荷によりテストが不安定になるリスクがある。 | Deadline の代わりに `headers: new Metadata { { "grpc-timeout", "300m" } }` を使用するか、テスト環境用に十分なマージン（例: 5000ms）を設定する |

### 継続指摘（Iteration 3 から継続）

| # | 重要度 | 出典 Agent | 指摘内容 | 推奨対応 |
|---|--------|-----------|----------|----------|
| M-01 | Medium | programing-reviewer | `CouponCacheService`（§12.2）で `Coupon` エンティティを直接 JSON シリアライズして Redis に格納。ナビゲーションプロパティによる循環参照・データ肥大化リスク。 | キャッシュ用の軽量 DTO（`CouponCacheDto`）を定義 |
| M-02 | Medium | dba-reviewer | `promotions` テーブルの `conditions` / `application_rules` JSONB カラムに GIN インデックスが未定義 | GIN インデックスを追加するか、検索不要であればその旨を明記 |
| M-03 | Medium | dba-reviewer | `campaigns` テーブルに `row_version`（楽観的ロック）カラムがない | `Campaign` エンティティに `[Timestamp] byte[] RowVersion` を追加 |
| M-04 | Medium | programing-reviewer | `CouponRuleEngine.Validate`（§7.2）が同期メソッドだが、`EvaluateRulesAsync`（§D.3）は非同期。同期/非同期インターフェース混在 | `Validate` を `ValidateAsync` に統一 |
| M-05 | Medium | qa-manager | 楽観的ロック競合（`DbUpdateConcurrencyException`）のテストケースが未定義 | 同時更新競合テストを追加 |
| M-06 | Medium | qa-manager | `CheckFraudAsync`（§D.4）のテストケースが存在しない | 複合不正検知テストケースを追加 |
| M-07 | Medium | programing-reviewer | §20.1 と §I.1 でクーポン有効期限チェック BackgroundService が重複定義 | §20.1 を §I.1 への参照に変更（SSOT） |
| M-08 | Medium | business-analyst | spec.md §9 の「クーポン配布ルールの設定」に対応する設計が不十分 | Phase 1 スコープ外であれば明記 |
| M-09 | Medium | business-analyst | `PUT /api/v1/admin/campaigns/{id}` のリクエスト DTO が未定義 | `UpdateCampaignRequest` DTO を定義 |
| M-10 | Medium | compliance-reviewer | `user.deleted` イベント購読時の仮名化処理の具体的実装が未定義 | 仮名化パターン（`ANONYMIZED_{hash}` 等）を記載 |
| M-11 | Medium | performance-reviewer | `GET /api/v1/coupons/available` にページネーションが未定義 | `[AsParameters] PaginationParams` を導入 |
| M-12 | Medium | performance-reviewer | Redis キャッシュミス + DB フォールバック時の 300ms Deadline 超過リスク評価が未記載 | レイテンシ見積もりを追記 |
| M-13 | Medium | release-manager | EF Core Migration の初期マイグレーション名・適用戦略が未記載 | マイグレーション戦略を記載 |
| M-14 | Medium | infra-ops-reviewer | CORS 設定の前提（API Gateway で制御するため個別設定不要）が未記載 | 前提を明記 |
| M-16 | Medium | audit-reviewer | `CouponUsage` テーブルに利用取消の記録メカニズムがない。`ReleaseCoupon` 時のレコード処理が不明（物理削除 vs ステータス更新） | `status` カラム（`APPLIED` / `CANCELLED`）を追加 |
| L-01 | Low | programing-reviewer | `AdminCouponEndpoints.GetCouponUsages`（§E.2）でクーポン ID フィルタリングが未実施 | パスパラメータ `{id}` を活用 |
| L-02 | Low | dba-reviewer | `coupon_usages.used_at` インデックスが §5.2 のテーブル定義に未記載 | §5.2 のインデックス一覧に追加 |
| L-03 | Low | business-analyst | `CouponAnalyticsResponse` に期間指定パラメータがない | `from` / `to` パラメータを追加 |
| L-04 | Low | ux-accessibility-reviewer | エラーメッセージの i18n（日英対応）方針が未記載 | Phase 2 スコープとして記録 |
| L-05 | Low | performance-reviewer | `CampaignStatusService` のポーリング間隔が 30 分固定 | 現行設計で許容する旨を記載 |
| L-06 | Low | audit-reviewer | appsettings.json の Kafka `GroupId` の環境別設定分離が不明確 | 環境別設定オーバーライドを明記 |

### 解消された指摘（Iteration 3 → 4）

| # | 指摘内容 | 解消方法 |
|---|---------|---------|
| H-01 | 内部サービス間認証メカニズム未定義 | §13.1.1 に Client Credentials フロー + InternalServiceHandler 追加 |
| H-02 | OutboxPublisher Advisory Lock 未実装 | §11.1 に pg_try_advisory_lock パターン追加（ただし実装バグあり → H-11） |
| H-03 | gRPC Proto パッケージバージョニング不整合 | §10.1 を `skishop.coupon.v1` に統一 |
| H-04 | CouponCodeGenerator 欠落 | §D.7 にインターフェース・実装追加 |
| H-05 | DateRange Value Object 未使用 | §D.8 に定義 + OwnsOne マッピング追加 |
| H-06 | gRPC 認可チェック欠落 | §10.2 に `[Authorize(Policy = "InternalServiceOnly")]` 追加 |
| H-07 | OutboxPublisher ステータス遷移 2 段階 | §11.1 を PENDING → PROCESSING → PUBLISHED の 3 段階に更新 |
| H-08 | gRPC 統合テスト未定義 | §J.3 に 4 テストケース追加 |
| H-09 | order.cancelled べき等性未保証 | §J.3 にべき等性テスト方針追記（ただし Consumer 実装コード欠落 → M-20） |
| H-10 | DTO 日時型不統一 | §6.4 DTO を DateTimeOffset に統一 |
| M-15 | CouponReleased イベント欠落 | §9.1 に `CouponReleased` / `coupon.released` トピック追加 |

---

## ドキュメント横断分析

### spec.md との整合性チェック

| spec.md の定義 | 設計書の対応 | 整合性 | 備考 |
|---------------|------------|--------|------|
| §9 責務（作成、配布ルール、適用、キャンペーン、使用追跡） | §1.1 目的で網羅 | ⚠️ | 「配布ルールの設定」が弱い（M-08） |
| §9 主要エンティティ | §5.1 ER 図で全て定義 | ✅ | |
| §9 データストア（PostgreSQL + Redis） | §3 で定義 | ✅ | |
| §9 コンポーネント（クーポンコードジェネレーター） | §D.7 に追加済み | ✅ | H-04 修正済み |
| §9 コンポーネント（配布サービス + 配布 Repository） | 設計書に欠落 | ⚠️ | E-01（エスカレーション） |
| Aggregate Root: **Coupon** + CouponUsage | §5.1 で整合 | ✅ | |
| Value Object: **DateRange** | §D.8 に定義済み | ✅ | H-05 修正済み |
| Saga Step 3（gRPC 65ms + 15ms = 80ms） | §10 で定義、Deadline 300ms | ✅ | |
| gRPC Proto: `skishop.coupon.v1` | §10.1 + §H で定義 | ⚠️ | §10.1 と §H で RPC 名不整合（M-21） |
| H8-23 クーポン・ポイント適用順序 | §21.1 で完全定義 | ✅ | |
| ゲスト購入時クーポンスキップ | §21.1 で定義 | ✅ | |
| Outbox パターン（ADR-0005） | §11 で定義 | ⚠️ | Advisory Lock に実装バグ（H-11） |
| 注文キャンセル時クーポン復元 | §9.2 / §10.1 ReleaseCoupon | ✅ | |
| BackgroundService リーダー選出 | §11.1 に追加済み | ⚠️ | ExecuteSqlRawAsync の戻り値問題（H-11） |
| 内部サービス間認証 | §13.1.1 に追加済み | ✅ | Program.cs §F との不整合あり（M-17） |

### Kafka イベント整合性

| トピック | 発行/購読 | 対応先 | 整合性 |
|---------|----------|--------|--------|
| `coupon.applied` | 発行 | SalesManagementService → 注文記録連携 | ✅ |
| `coupon.released` | 発行 | Saga 補償完了通知 | ✅ M-15 解消済み |
| `coupon.expired` | 発行 | 監視・分析 | ✅ |
| `order.cancelled` | 購読 | SalesManagementService → クーポン取消 | ⚠️ Consumer 実装コード欠落（M-20） |
| `user.deleted` | 購読 | UserManagementService → 仮名化 | ⚠️ 具体処理未定義（M-10） |

### ADR 整合性

| ADR | 設計書の準拠状況 | 備考 |
|-----|----------------|------|
| ADR-0001（マイクロサービス） | ✅ 準拠 | |
| ADR-0002（Minimal API） | ✅ 準拠 | |
| ADR-0003（PostgreSQL 統一） | ✅ 準拠 | coupondb |
| ADR-0004（Kafka イベント駆動） | ✅ 準拠 | |
| ADR-0005（Outbox パターン） | ⚠️ 一部不備 | Advisory Lock 実装バグ（H-11） |
| ADR-0006（サービス別独立 DB） | ✅ 準拠 | |
| ADR-0007（RFC 9457 Problem Details） | ✅ 準拠 | |
| ADR-0008（PCI DSS 非保持化） | N/A | クーポンサービスは決済非関連 |
| ADR-0009（Saga オーケストレーション） | ✅ 準拠 | Step 3 定義 + gRPC テスト追加 |
| ADR-0010（RTO 1 時間統一） | ✅ 準拠 | ヘルスチェック定義あり |

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### 評価: Pass (Medium: 2, Low: 1)

**良好な点:**
- H-04 修正により CouponCodeGenerator が追加され、spec.md §9 のコンポーネント構成との整合性が改善
- クーポン種別（PERCENTAGE, FIXED_AMOUNT, FREE_SHIPPING）の 3 種が明確に定義
- クーポン・ポイント適用順序（§21.1）が spec.md H8-23 と完全に整合
- キャンペーン管理のステートマシン（DRAFT→ACTIVE→PAUSED→ENDED）が実用的
- §D.7 のバッチ発行パターン（`GenerateBatchAsync`）が大量クーポン生成のビジネス要件に対応

**継続指摘:**
- **M-08**: 配布ルール設定機能が不十分（Phase 1 スコープ判断が必要）
- **M-09**: キャンペーン更新 DTO（`UpdateCampaignRequest`）が未定義
- **L-03**: 分析レスポンスに期間指定がない

</details>

<details>
<summary>architect レビューレポート</summary>

### 評価: ⚠️ Conditional (Medium: 3, Low: 0)

**良好な点:**
- H-03 修正で gRPC Proto パッケージが `skishop.coupon.v1` に統一
- H-05 修正で DateRange Value Object が DDD 戦術パターンに準拠して導入
- H-09 修正でべき等性テストの方針が §J.3 に記載
- Outbox パターンの 3 段階ステータス遷移（H-07）がイベント整合性の信頼性を向上
- CouponReleased イベント（M-15 解消）が Kafka イベント整合性を完成

**指摘事項:**
- **M-20**: OrderEventConsumer 実装コード欠落
- **M-21**: gRPC Proto 定義の §10.1 / §H 命名不整合（ValidateCoupon vs ApplyCoupon）
- **M-08 (business-analyst と共同)**: 配布ルール設計不十分

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 評価: ⚠️ Conditional (High: 1, Medium: 2, Low: 0)

**良好な点:**
- H-01〜H-10 の全修正意図が正しく反映されている
- §13.1.1 の InternalServiceOnly 実装パターン（Client Credentials + InternalServiceHandler）がエンタープライズ品質
- §D.7 CouponCodeGenerator の紛らわしい文字除外（I/O/0/1 除外）が実用的
- §D.8 DateRange の EF Core OwnsOne マッピングパターンが正確
- .github/instructions/ の全規約（コーディング標準、セキュリティ、API 設計）への準拠度が前回から向上

**指摘事項:**
- **H-11**: OutboxPublisher の `ExecuteSqlRawAsync` が `pg_try_advisory_lock` の boolean 戻り値を正しく取得できない設計バグ
- **M-17**: Program.cs §F に InternalServiceOnly ポリシー定義が未反映
- **M-07**: BackgroundService の定義重複（§20.1 と §I.1）が継続

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 評価: Pass (Medium: 5, Low: 1)

**良好な点:**
- H-10 修正により §6.4 の DTO が DateTimeOffset に正しく統一
- H-07 修正で OutboxPublisher のステータス遷移が PENDING → PROCESSING → PUBLISHED に改善
- H-04 のコード生成アルゴリズム（`stackalloc` + `Random.Shared`）がパフォーマンス効率的
- primary constructor、record 型、CancellationToken 伝搬が一貫して維持
- §13.1.1 の `InternalServiceRequirement` / `InternalServiceHandler` が ASP.NET Core 認可パイプラインに準拠

**指摘事項:**
- **M-18**: テストコードに `DateTime.UtcNow` が残留（§17.1, §J.1）
- **M-19**: `CountRecentUsagesAsync` パラメータ型が `DateTime`（他は `DateTimeOffset` に統一済み）
- **M-04**: ルールエンジンの同期/非同期混在（継続）
- **M-01**: CouponCacheService のエンティティ直接シリアライズ（継続）
- **M-21 (architect と共同)**: gRPC Proto 命名不整合
- **L-01**: GetCouponUsages のクーポン ID フィルタリング欠落（継続）

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 評価: Pass (Medium: 2, Low: 0)

**良好な点:**
- H-01 修正で内部サービス間認証が OAuth2 Client Credentials フローに統一（X-Internal-Service-Key 廃止は正しい判断）
- H-06 修正で gRPC サービスに `[Authorize(Policy = "InternalServiceOnly")]` が適用
- §13.1.1 の `InternalServiceHandler` が `AllowedInternalClients` 設定リストによるホワイトリスト方式で安全
- gRPC クライアント側の `AddCallCredentials` パターンが正しい実装
- IDOR 防止、FallbackPolicy、入力バリデーション、クーポンコード正規表現制限が維持

**指摘事項:**
- **M-17 (tech-lead と共同)**: Program.cs §F に InternalServiceOnly ポリシー定義が未反映
- **M-22**: セキュリティヘッダーに Referrer-Policy / Permissions-Policy が未設定

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 評価: Pass (Medium: 2, Low: 1)

**良好な点:**
- H-05 修正の DateRange OwnsOne マッピングで既存カラム（valid_from/valid_until）を再利用する設計が移行コストゼロ
- `outbox_events` テーブルの CHECK 制約に `PROCESSING` ステータスが追加済み（H-07 対応に整合）
- 全テーブルの snake_case 命名規則、CHECK 制約、楽観的ロック（Coupon エンティティ）が維持
- 部分インデックス（outbox_events PENDING フィルタ）が定義

**継続指摘:**
- **M-02**: promotions テーブルの JSONB カラムにインデックスなし
- **M-03**: Campaign エンティティに楽観的ロック欠落
- **L-02**: coupon_usages.used_at インデックスが §5.2 テーブル定義に未記載

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 評価: Pass (Medium: 3, Low: 0)

**良好な点:**
- H-08 修正で gRPC 統合テスト 4 件が追加（Apply 成功、Release 成功、べき等性、無効コード）
- べき等性テスト（§J.3 `Should_BeIdempotent_When_ReleaseCouponCalledTwice`）がSaga 補償の重要パスをカバー
- AAA パターン + Should 命名規則が全テストで一貫
- テストヘルパーメソッドが再利用可能

**指摘事項:**
- **M-18 (programing-reviewer と共同)**: テストコードの DateTime.UtcNow 残留
- **M-05**: 楽観的ロック競合テスト未定義（継続）
- **M-06**: CheckFraudAsync テスト未定義（継続）

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 評価: Pass (Medium: 2, Low: 1)

**良好な点:**
- OutboxPublisher の動的バックオフ（100ms〜5s）が維持
- Redis キャッシュ戦略（TTL 付き: クーポン 10 分、利用回数 5 分）が維持
- gRPC Deadline 設計（300ms / 500ms）が Saga タイムバジェットに整合

**継続指摘:**
- **M-11**: ページネーション未定義
- **M-12**: Redis キャッシュミス時の Deadline 超過リスク評価未記載
- **L-05**: CampaignStatusService の固定ポーリング間隔

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 評価: Pass (Medium: 1, Low: 0)

**良好な点:**
- `user.deleted` イベント購読でデータ仮名化の意思が明確
- PII が直接格納されない設計（user_id のみ）
- ログに PII が出力されない

**継続指摘:**
- **M-10**: `user.deleted` 時の仮名化処理の具体的実装が未定義

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 評価: Pass (指摘なし)

**良好な点:**
- 全 NuGet パッケージが AGENTS.md の必須パッケージリストに一致
- プレリリースパッケージの使用なし
- 禁止パッケージの使用なし
- FluentValidation 11.* を使用（非推奨の FluentValidation.AspNetCore ではない）
- H-08 で追加された gRPC テスト用パッケージ（Grpc.Net.Client）も問題なし

</details>

<details>
<summary>release-manager レビューレポート</summary>

### 評価: Pass (Medium: 1, Low: 0)

**良好な点:**
- Dockerfile がマルチステージビルドで非 root ユーザーに準拠
- ヘルスチェック + HEALTHCHECK が一致

**継続指摘:**
- **M-13**: EF Core Migration の初期マイグレーション名・適用戦略が未記載

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 評価: Pass (Medium: 1, Low: 0)

**良好な点:**
- H-02 修正で Advisory Lock パターンが導入（概念的に正しい）
- OpenTelemetry、ヘルスチェック、Serilog CompactJsonFormatter が維持
- Dockerfile の EXPOSE 5006 がサービスポートと一致

**指摘事項:**
- **M-14**: CORS 設定の前提説明不足（継続）

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 評価: Pass (Medium: 1, Low: 1)

**良好な点:**
- CouponReleased イベント追加（M-15 解消）により補償トランザクション完了の監査証跡が確保
- Correlation ID、構造化ログ、例外ハンドラの全例外ログ出力が維持
- Outbox Events テーブルのステータス遷移 3 段階化でイベント処理の追跡性が向上

**継続指摘:**
- **M-16**: CouponUsage の取消記録メカニズムが未定義
- **L-06**: Kafka GroupId の環境別設定分離が不明確

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 評価: Pass (Low: 1)

**良好な点:**
- エラーコード体系（CPN-XXXX）が一貫
- RFC 9457 Problem Details 準拠
- エラーメッセージが具体的

**継続指摘:**
- **L-04**: エラーメッセージの i18n 方針が未記載

</details>
