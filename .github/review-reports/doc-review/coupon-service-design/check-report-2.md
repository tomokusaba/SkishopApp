# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/coupon-service-design.md`
- **判定**: ✅ **Approved with Notes** — Critical 0 件、High 0 件。前回の Critical 2 件・High 8 件は全て是正確認済み。新規 Medium 7 件 + 残存 Medium 13 件あり
- **レビュー日時**: 2026-04-03
- **イテレーション**: 2（check-report-1 → fix-report-1 → 本レポート）
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

## 技術スタック検証結果
| カテゴリ | 設計書記載 | AGENTS.md / spec.md 定義 | 整合性 |
|---------|-----------|------------------------|--------|
| ランタイム | .NET 10 (C# 14) | .NET 10 (C# 14) | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| ORM | EF Core 10 | EF Core 10 | ✅ |
| DB | PostgreSQL (coupondb) | PostgreSQL (coupondb — ADR-0006) | ✅ 修正確認 |
| メッセージング | Confluent.Kafka 2.* | Confluent.Kafka 2.* | ✅ |
| キャッシュ | StackExchange.Redis 2.* | StackExchange.Redis 2.* | ✅ |
| バリデーション | FluentValidation 11.* | FluentValidation 11.* | ✅ |
| ログ | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| テスト | xUnit / NSubstitute / Shouldly | xUnit / NSubstitute / Shouldly | ✅ |
| コンテナ化 | Docker 25.x | Docker 25.x | ✅ |
| ポート | 5006 | 5006 | ✅ |

## 前回指摘の是正確認

### Critical 是正: 2/2 ✅
| # | 指摘 ID | 内容 | 是正状態 | 確認結果 |
|---|---------|------|----------|----------|
| 1 | C1 | 全テーブル日時カラムが `TIMESTAMP` のみ | ✅ 修正済 | 全 8 テーブル（campaigns, coupons, user_coupons, coupon_usages, coupon_types, coupon_restrictions, promotions, outbox_events）の全日時カラムが `TIMESTAMP WITH TIME ZONE` に修正されていることを確認 |
| 2 | C2 | `CouponType`, `CouponRestriction`, `Promotion` エンティティの欠落 | ✅ 修正済 | ER 図（§5.1）にリレーション追加、テーブル定義（§5.2）に 3 テーブル追加、プロジェクト構成（§15）に Models/Repositories 追加を確認 |

### High 是正: 8/8 ✅
| # | 指摘 ID | 内容 | 是正状態 | 確認結果 |
|---|---------|------|----------|----------|
| 1 | H1 | gRPC サービス定義の欠落 | ✅ 修正済 | §10 に `coupon.proto`（ValidateCoupon / ReleaseCoupon RPC）、メッセージ型、gRPC サーバー実装、Deadline 設計（300ms / 500ms）を確認 |
| 2 | H2 | Outbox パターンの欠落 | ✅ 修正済 | §5.2 に `outbox_events` テーブル定義、§11 に OutboxPublisher（動的バックオフ 100ms〜5s）・Outbox 書込パターンを確認 |
| 3 | H3 | coupons テーブル CHECK 制約の欠落 | ✅ 修正済 | 4 CHECK 制約（`ck_coupons_discount_value`, `ck_coupons_min_order_amount`, `ck_coupons_max_usage_count`, `ck_coupons_discount_type`）を確認 |
| 4 | H4 | `FREE_SHIPPING` 割引タイプの欠落 | ✅ 修正済 | テーブル定義、CHECK 制約、`CalculateDiscount` switch 式、FluentValidation に `FREE_SHIPPING` 追加を確認 |
| 5 | H5 | `DateTime.UtcNow` 直接使用 | ✅ 修正済 | CouponRuleEngine・FraudDetectionService が `TimeProvider` DI + `timeProvider.GetUtcNow()` を使用していることを確認 |
| 6 | H6 | DB 名が `skishopdb`（ADR-0006 違反） | ✅ 修正済 | §3 サービス情報・§4.1 アーキテクチャ図が `coupondb` に修正されていることを確認 |
| 7 | H7 | Kafka トピック名の不整合 | ✅ 修正済 | §9.1 が `coupon.applied` / `coupon.expired`（ドット区切り小文字）に統一、§9.2 に `user.deleted` 購読追加を確認 |
| 8 | H8 | クーポン・ポイント適用順序の欠落 | ✅ 修正済 | §21.1 に 6 ステップの適用順序（商品合計→クーポン→ポイント→配送料→消費税→最終金額）、ゲスト購入スキップ、ポイント充当範囲制約を確認 |

---

## 指摘サマリー
| Agent | 判定 | Critical | High | Medium (新規) | Medium (残存) | Low (新規) | Low (残存) |
|-------|------|----------|------|--------------|--------------|-----------|-----------|
| architect | Pass w/ Notes | 0 | 0 | 1 | 2 | 0 | 0 |
| dba-reviewer | Pass w/ Notes | 0 | 0 | 1 | 0 | 1 | 1 |
| security-reviewer | Pass w/ Notes | 0 | 0 | 0 | 1 | 0 | 0 |
| programing-reviewer | Pass w/ Notes | 0 | 0 | 4 | 3 | 1 | 1 |
| business-analyst | Pass | 0 | 0 | 0 | 0 | 0 | 0 |
| qa-manager | Pass w/ Notes | 0 | 0 | 0 | 2 | 0 | 0 |
| performance-reviewer | Pass w/ Notes | 0 | 0 | 0 | 1 | 0 | 0 |
| compliance-reviewer | Pass w/ Notes | 0 | 0 | 0 | 1 | 0 | 0 |
| oss-reviewer | Pass | 0 | 0 | 0 | 0 | 0 | 0 |
| release-manager | Pass | 0 | 0 | 0 | 0 | 0 | 1 |
| infra-ops-reviewer | Pass w/ Notes | 0 | 0 | 0 | 1 | 0 | 0 |
| audit-reviewer | Pass w/ Notes | 0 | 0 | 0 | 1 | 0 | 0 |
| ux-accessibility-reviewer | N/A | 0 | 0 | 0 | 0 | 0 | 0 |
| tech-lead | Pass w/ Notes | 0 | 0 | 1 | 1 | 0 | 0 |
| **合計** | | **0** | **0** | **7** | **13** | **2** | **3** |

**総指摘数**: Critical 0 / High 0 / Medium 20 (新規 7 + 残存 13) / Low 5 (新規 2 + 残存 3)

## 判定根拠
- 判定ルール適用結果: Critical 0 件・High 0 件 → **Approved with Notes**（Medium/Low のみ）
- 前回 Critical 2 件・High 8 件が全て是正済み — 実装フェーズへの進行が可能
- 新規 Medium 7 件は修正時に導入された不整合（TimeProvider 未統一、ER 図↔テーブル定義の乖離等）で、実装フェーズで対応可能
- 残存 Medium 13 件は前回の判断通り、横断的設計（ミドルウェア順序、Correlation ID 等）または実装フェーズ対応の項目

---

## 新規指摘一覧（fix-report-1 による修正で導入された問題）

| # | 重要度 | 出典 Agent | カテゴリ | 対象箇所 | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|----------|
| N1 | **Medium** | dba-reviewer | データモデル不整合 | §5.1 ER 図 vs §5.2 coupons テーブル | ER 図（§5.1 L147）で `Coupon` エンティティに `coupon_type_id FK` が定義され、`CouponType ||--o{ Coupon : categorizes` リレーションが記載されているが、§5.2 の `coupons` テーブル定義に `coupon_type_id` カラムが存在しない。FK 制約も未定義。CouponType テーブルが追加されたが、coupons テーブルへの FK 追加が漏れている | `coupons` テーブルに `coupon_type_id VARCHAR(36) FK(coupon_types.id)` カラムを追加。FK 制約 `fk_coupons_coupon_type` を定義 |
| N2 | **Medium** | programing-reviewer | コード例不整合 | §11.1 OutboxPublisher | `evt.PublishedAt = timeProvider.GetUtcNow().UtcDateTime` が `DateTimeOffset` → `DateTime` に変換しており、`TIMESTAMP WITH TIME ZONE` カラム（C1 修正で適用）と不整合。`published_at` は `TIMESTAMP WITH TIME ZONE` = `DateTimeOffset` であるべき | `evt.PublishedAt = timeProvider.GetUtcNow()` に修正（`.UtcDateTime` を削除）。`OutboxEvent.PublishedAt` プロパティの型を `DateTimeOffset?` にする |
| N3 | **Medium** | programing-reviewer | TimeProvider 未統一 | §11.2 Outbox 書込パターン | Outbox イベント書き込み例（§11.2）で `DateTimeOffset.UtcNow` を直接使用（`OccurredAt = DateTimeOffset.UtcNow`）。H5 修正で `TimeProvider` DI が必須化されたが、新規追加コードに未適用 | `TimeProvider timeProvider` を Service に DI し、`OccurredAt = timeProvider.GetUtcNow()` に修正 |
| N4 | **Medium** | programing-reviewer | TimeProvider 未統一 | §17.1 テストヘルパー | テストコードの `CreateCoupon()` ヘルパーで `ValidFrom = DateTime.UtcNow.AddDays(-1)`, `ValidUntil = DateTime.UtcNow.AddDays(30)` が `DateTime.UtcNow` を直接使用。H5 修正と矛盾 | テスト内で `FakeTimeProvider` を使用し、`ValidFrom = timeProvider.GetUtcNow().AddDays(-1).DateTime` のパターンに修正 |
| N5 | **Medium** | programing-reviewer | gRPC 補償トランザクション | §10.2 CouponGrpcService | `ReleaseCoupon` メソッドが例外を処理していない。補償トランザクションはべき等であるべき（AGENTS.md §10.4）だが、既にリリース済みのクーポンに対する再呼出し時に `NotFoundException` がスローされると gRPC エラーとなり、Saga コーディネータがリトライを繰り返すリスクがある | `ReleaseCoupon` 内で `NotFoundException` / `BusinessException` を catch し、`ReleaseCouponResponse { Success = true }` を返すべき等パターンを実装。ログで重複呼出しを記録 |
| N6 | **Medium** | architect | OutboxPublisher 排他制御 | §11.1 OutboxPublisher | OutboxPublisher のイベント取得クエリが `Where(e => e.Status == "PENDING").OrderBy(e => e.CreatedAt).Take(100)` のみで、`SELECT ... FOR UPDATE SKIP LOCKED` パターンが未使用。複数インスタンスデプロイ時に同一イベントを二重処理するリスクがある | `FromSqlInterpolated` で `SELECT ... FROM outbox_events WHERE status = 'PENDING' ORDER BY created_at LIMIT 100 FOR UPDATE SKIP LOCKED` パターンを適用。または `status` を `PROCESSING` に更新する楽観的ロック方式を追記 |
| N7 | **Medium** | tech-lead | プロジェクト構成不足 | §15 プロジェクト構成 | `CouponType`, `Promotion` エンティティ（C2 修正で追加）に対応する Repository が §15 のプロジェクト構成に未定義。`ICouponRestrictionRepository` は追加済みだが、`ICouponTypeRepository` / `IPromotionRepository` が欠落。Aggregate Root 単位の Repository パターン（AGENTS.md §3.4）に照らし、独立エンティティには Repository が必要 | §15 Repositories/ に `ICouponTypeRepository.cs` / `CouponTypeRepository.cs` / `IPromotionRepository.cs` / `PromotionRepository.cs` を追加 |

---

## 残存指摘一覧（check-report-1 から未修正）

| # | 旧 ID | 重要度 | 出典 Agent | 対象箇所 | 指摘内容 | 備考 |
|---|-------|--------|-----------|----------|----------|------|
| R1 | M1 | Medium | architect | §4.1 | spec.md §9 の「配布サービス（DistService）」「クーポンコードジェネレーター」が設計書に未反映 | Phase 判断待ち（E1） |
| R2 | M2 | Medium | architect | §5.2 | `UserCoupon` テーブルが spec.md エンティティ一覧に存在しない独自追加。位置づけ明確化が必要 | spec.md との調整要 |
| R3 | M6 | Medium | programing-reviewer | §6.4 | `CreateCampaignRequest` / `CreateCouponRequest` / レスポンス DTO の日時プロパティが `DateTime` 型。`DateTimeOffset` 使用が推奨 | ER 図は更新済、コード例は未修正 |
| R4 | M7 | Medium | programing-reviewer | §5 全体 | EF Core エンティティクラスの完全定義（`[Table]`, `[Column]`, `[Key]`, `[MaxLength]` 属性付き）が設計書に欠落 | 実装フェーズで対応可 |
| R5 | M8 | Medium | qa-manager | §17 | `FraudDetectionService` / `CampaignService` のテストケースが未定義。テスト規約（分岐カバレッジ 80%）の網羅性不足 | テスト設計の拡充推奨 |
| R6 | M9 | Medium | qa-manager | §17.2 | `Testcontainers.PostgreSql` による Repository 層の DB スライステストが未設計 | AGENTS.md §9.1 推奨 |
| R7 | M10 | Medium | performance-reviewer | §12 | Redis フォールバック実装例（try-catch + DB 問い合わせ）が `CouponCacheService` に未記載。§21 制約 5 で方針のみ記載 | サーキットブレーカー付きフォールバック推奨 |
| R8 | M11 | Medium | compliance-reviewer | §8.2 | ログ出力のデータ分類（PII/非PII）の方針が未明記。userId は直接 PII ではないがログ設計方針の明文化推奨 | §13 セキュリティ設計に追記推奨 |
| R9 | M12 | Medium | infra-ops-reviewer | §19 | Dockerfile `EXPOSE 5006` が AGENTS.md Dockerfile 規約の `EXPOSE 8080` デフォルトと不一致 | `ASPNETCORE_URLS` 環境変数による制御推奨 |
| R10 | M13 | Medium | audit-reviewer | 全体 | Correlation ID ミドルウェアの設計が欠落。AGENTS.md §11.3 で全サービスに必須 | Program.cs ミドルウェア順序に追記推奨 |
| R11 | M14 | Medium | security-reviewer | §6.3, §13.1 | 内部 API 認証に `X-Internal-Service-Key` が残存。gRPC Client Credentials / Managed Identity への完全移行が望ましい | 「または Managed Identity」と追記されたが、静的キー方式が依然として許容されている |
| R12 | M15 | Medium | programing-reviewer | §12 | `CouponCacheService` の Redis 操作（`StringGetAsync` / `StringSetAsync`）に `CancellationToken` が未伝搬 | StackExchange.Redis の `CommandFlags` 経由で ct 伝搬 |
| R13 | M16 | Medium | tech-lead | 全体 | Program.cs ミドルウェアパイプライン設計が欠落。AGENTS.md §11.3 の順序（ExceptionHandler → HSTS → Correlation ID → Serilog → CORS → Authentication → Authorization → RateLimiter → Endpoints）が未記載 | Program.cs 構成セクション追加推奨 |

---

## Low 指摘一覧（新規 + 残存）

| # | 重要度 | 新規/残存 | 出典 Agent | 対象箇所 | 指摘内容 |
|---|--------|----------|-----------|----------|----------|
| L-N1 | Low | 新規 | programing-reviewer | §10.2 | `decimal.Parse(request.OrderAmount)` が `CultureInfo.InvariantCulture` を指定していない。ロケール依存でパースが失敗するリスク |
| L-N2 | Low | 新規 | dba-reviewer | §5.2 coupon_restrictions | FK 制約 `ON DELETE CASCADE` の使用理由コメントが sql-schema-review.instructions.md §3 の要件通り記載されていない |
| L-R1 | Low | 残存 | dba-reviewer | §5.2 | `coupons.id` / `campaigns.id` 等が `VARCHAR(36)` だが PostgreSQL ネイティブ `UUID` 型の方がストレージ効率・インデックス性能で優位 |
| L-R2 | Low | 残存 | programing-reviewer | §6.4 | `CreateCouponRequest` の `ValidFrom = default`, `ValidUntil = default` で `DateTime.MinValue` が入り `[Required]` バリデーションをすり抜ける可能性 |
| L-R3 | Low | 残存 | release-manager | §19 | Dockerfile `HEALTHCHECK` で `curl` を使用しているが `aspnet` ランタイムイメージにプリインストールされていない場合がある |

---

## エスカレーション事項（据え置き — 前回から継続）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E1 | 高優先 | architect | `CouponType` / `CouponRestriction` / `Promotion` のテーブル定義は追加済みだが、Service / Repository / Endpoints の実装範囲（Phase 1 vs Phase 2）の判断が必要 | プロダクトオーナー / テックリード |
| E2 | 高優先 | architect | gRPC + REST 二重公開の実装優先度・タイミングの判断が必要（§10 で設計は追加済み） | テックリード |
| E3 | 通常 | business-analyst | E2E テストシナリオ E6（クーポン・ポイント併用購入）のテスト範囲が不明確 | QA リード |

---

## 競合解決記録

本イテレーションでは Agent 間の競合は検出されなかった。

---

## ドキュメント横断分析

### サービス間整合性（前回からの改善状況）

| 連携先 | 連携方式 | 設計書記載 | spec.md 定義 | 整合性 | 前回比 |
|--------|---------|-----------|-------------|--------|--------|
| SalesManagementService → CouponService (Saga Step 3) | gRPC | ✅ §10 `coupon.proto` | gRPC (`coupon.proto`) | ✅ | ❌→✅ |
| SalesManagementService → CouponService (補償) | gRPC | ✅ §10 `ReleaseCoupon` | gRPC (`ReleaseCoupon`) | ✅ | ❌→✅ |
| CouponService → Kafka (`coupon.applied`) | Outbox → Kafka | ✅ §11 Outbox パターン | Outbox パターン (ADR-0005) | ✅ | ❌→✅ |
| CouponService → Kafka (`coupon.expired`) | Outbox → Kafka | ✅ §9.1 + §11 | Outbox パターン (ADR-0005) | ✅ | ❌→✅ |
| Kafka (`order.cancelled`) → CouponService | Consumer | ✅ §9.2 | ✅ | ✅ | ✅→✅ |
| Kafka (`user.deleted`) → CouponService | Consumer | ✅ §9.2 | ✅ | ✅ | 新規追加 |
| PaymentCartService → CouponService (割引計算) | REST Internal API | ✅ §6.3 | △ (gRPC 推奨) | ⚠️ | ⚠️→⚠️ |

### Kafka トピック整合性（改善済み）

| 設計書イベント | spec.md トピック | 整合 | 前回比 |
|--------------|----------------|------|--------|
| CouponApplied | `coupon.applied` | ✅ | ❓→✅ |
| CouponExpired | `coupon.expired` | ✅ | ✅→✅ |
| `order.cancelled` (購読) | `order.cancelled` | ✅ | ✅→✅ |
| `user.deleted` (購読) | `user.deleted` | ✅ | 新規追加 |

### 改善サマリ（前回 → 今回）

| 項目 | check-report-1 | check-report-2 | 改善 |
|------|---------------|----------------|------|
| Critical | 2 | **0** | ✅ 全件解消 |
| High | 8 | **0** | ✅ 全件解消 |
| サービス間 gRPC 整合性 | ❌ 5 件不整合 | **✅ 全件整合** | ✅ |
| Kafka トピック整合性 | ❌ 5 件不整合 | **✅ 全件整合** | ✅ |
| DB 名 (ADR-0006) | ❌ `skishopdb` | **✅ `coupondb`** | ✅ |
| Outbox パターン (ADR-0005) | ❌ 未設計 | **✅ 設計済み** | ✅ |
| TIMESTAMP WITH TIME ZONE | ❌ 全テーブル違反 | **✅ 全テーブル準拠** | ✅ |
| CouponType/CouponRestriction/Promotion | ❌ 欠落 | **✅ 追加済み**（※N1: FK 漏れ）| ⚠️ |

---

## 各 Agent 詳細レポート

<details>
<summary>architect レビューレポート</summary>

### 観点: マイクロサービス分割・Bounded Context・DDD パターン

**判定**: ✅ Pass with Notes（新規 Medium 1 件、残存 Medium 2 件）

**前回指摘の確認**:
- [H1] gRPC サービス定義: ✅ 解決確認 — §10 に `coupon.proto`, gRPC サーバー実装, Deadline 設計が完備
- [H2] Outbox パターン: ✅ 解決確認 — §11 に OutboxPublisher, outbox_events テーブル, 動的バックオフが完備
- [M1] 配布サービス: 未解決（Phase 判断待ち）
- [M2] UserCoupon テーブル: 未解決（spec.md 調整待ち）

**新規指摘**:
1. **[N6] OutboxPublisher 排他制御欠如**: `SELECT ... FOR UPDATE SKIP LOCKED` パターンが未使用。複数インスタンスデプロイ時に同一イベントの二重処理リスク。spec.md で定義されたパターン（AGENTS.md §10.4 参照）の適用が必要。

**良い点（修正による改善）**:
- gRPC サービス定義が Saga ステップ 3 の要件を正確に実装（ValidateCoupon / ReleaseCoupon）
- Outbox パターンが ADR-0005 に準拠し、動的バックオフ（100ms〜5s）を採用
- REST + gRPC の同一ポート多重化パターン（`MapGrpcService<T>` + `MapEndpoints`）が明記
- Deadline 設計（検証 300ms / 補償 500ms）が実装可能な精度で記述

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 観点: DB スキーマ設計・EF Core マッピング・マイグレーション安全性

**判定**: ✅ Pass with Notes（新規 Medium 1 件、新規 Low 1 件、残存 Low 1 件）

**前回指摘の確認**:
- [C1] TIMESTAMP WITH TIME ZONE: ✅ 全 8 テーブルの全日時カラムで確認
- [H3] CHECK 制約: ✅ coupons テーブルに 4 CHECK 制約追加を確認
- [H4] FREE_SHIPPING: ✅ `discount_type IN ('PERCENTAGE', 'FIXED_AMOUNT', 'FREE_SHIPPING')` を確認
- [M3] coupon_usages.updated_at: ✅ 追加確認
- [M4] campaigns status CHECK: ✅ `ck_campaigns_status` 追加確認
- [M5] user_coupons status CHECK: ✅ `ck_user_coupons_status` 追加確認

**新規指摘**:
1. **[N1] ER 図 ↔ テーブル定義の乖離**: ER 図（§5.1 L147）で `Coupon` エンティティに `coupon_type_id FK` が明示されているが、§5.2 の `coupons` テーブル定義カラム一覧に `coupon_type_id` が存在しない。`CouponType ||--o{ Coupon : categorizes` リレーションの物理実装が不完全。C2 修正で `coupon_types` テーブルは追加されたが、参照元 FK カラムの追加が漏れている。
2. **[L-N2] CASCADE 使用理由の欠落**: `coupon_restrictions` テーブルの FK 制約 `ON DELETE CASCADE, ON UPDATE CASCADE` に、sql-schema-review.instructions.md §3 で要求される使用理由コメントが未記載。

**良い点**:
- 新規追加 3 テーブル（coupon_types, coupon_restrictions, promotions）の設計品質が高い
- outbox_events テーブルに部分インデックス（`WHERE status = 'PENDING'`）が設計済み
- 全テーブルのステータス値が UPPER_CASE で一貫
- CHECK 制約に `ck_` プレフィックスの命名規則が適用済み
- coupon_restrictions に適切なインデックス設計あり

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 観点: OWASP Top 10・認証/認可設計・秘密情報管理

**判定**: ✅ Pass with Notes（残存 Medium 1 件）

**前回指摘の確認**:
- [M14] 内部 API 認証方式: 部分改善 — 「`X-Internal-Service-Key` ヘッダーまたは Managed Identity」と選択肢が追記されたが、静的 API キーが依然許容されている。

**新規指摘**: なし

**良い点**:
- gRPC エンドポイント追加による新たなセキュリティリスクは確認されなかった
- gRPC は Saga コーディネータからの内部呼出しのみで、外部公開されない設計
- IDOR 防止（§13.2）が引き続き適切
- FluentValidation に `FREE_SHIPPING` が追加され、ホワイトリストバリデーション維持
- appsettings.json に秘密情報のハードコードなし

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 観点: コード例の正確性・C# 14 / .NET 10 機能活用・禁止パターン

**判定**: ✅ Pass with Notes（新規 Medium 4 件、新規 Low 1 件、残存 Medium 3 件、残存 Low 1 件）

**前回指摘の確認**:
- [H5] TimeProvider DI: ✅ CouponRuleEngine / FraudDetectionService で `TimeProvider` + `timeProvider.GetUtcNow()` 使用を確認
- [M6] DTO DateTime 型: 未修正（ER 図更新済、コード例は `DateTime` のまま）
- [M7] EF Core エンティティ属性: 未修正（テーブル定義で補完済）
- [M15] CancellationToken Redis 伝搬: 未修正
- [L2] CreateCouponRequest default 値: 未修正

**新規指摘**:
1. **[N2] OutboxPublisher の DateTimeOffset 変換**: `evt.PublishedAt = timeProvider.GetUtcNow().UtcDateTime` — `.UtcDateTime` プロパティで `DateTimeOffset` → `DateTime` に変換しており、C1 修正で全カラムが `TIMESTAMP WITH TIME ZONE`（= `DateTimeOffset`）に統一された方針と矛盾。
2. **[N3] Outbox 書込パターンの TimeProvider 未使用**: §11.2 で `DateTimeOffset.UtcNow` を直接使用。H5 修正で確立した `TimeProvider` DI パターンが新規コードに未適用。
3. **[N4] テストヘルパーの DateTime.UtcNow**: §17.1 `CreateCoupon()` で `DateTime.UtcNow.AddDays(-1)` / `DateTime.UtcNow.AddDays(30)` を使用。`TimeProvider` / `FakeTimeProvider` への移行が必要。
4. **[N5] gRPC ReleaseCoupon のべき等性**: 補償トランザクションで例外未処理。既にリリース済みクーポンへの再呼出し時に無条件で例外がスローされると、Saga コーディネータがリトライを繰り返すリスク。
5. **[L-N1] decimal.Parse のロケール**: `decimal.Parse(request.OrderAmount)` に `CultureInfo.InvariantCulture` が未指定。

**良い点（維持）**:
- primary constructor の適切な使用（全 Service / Repository / BackgroundService）
- record 型による DTO 定義
- `ILogger<T>` メッセージテンプレート形式の一貫使用
- switch 式の活用（`DiscountType switch {...}` に `FREE_SHIPPING` ケース追加）
- OutboxPublisher で `IServiceScopeFactory` の正しい使用
- gRPC サーバー実装が `ServerCallContext.CancellationToken` を適切に伝搬

</details>

<details>
<summary>business-analyst レビューレポート</summary>

### 観点: ビジネス要件の完全性・ユーザーストーリー・受入基準

**判定**: ✅ Pass

**前回指摘の確認**:
- [C2] 主要エンティティ欠落: ✅ `CouponType`, `CouponRestriction`, `Promotion` がテーブル定義・ER 図・プロジェクト構成に追加
- [H8] クーポン・ポイント適用順序: ✅ §21.1 に spec.md H8-23 準拠の 6 ステップ適用順序が記載。ゲスト購入スキップ、ポイント充当範囲制約を含む

**新規指摘**: なし

**良い点**:
- クーポンライフサイクル（作成→配布→利用→期限切れ）の完全なカバー
- 不正利用検知（§8）の閾値設計が維持
- 「1 注文 1 クーポン」制約が明記（§21）
- ゲスト購入時のスキップルールが明確化（§21.1）
- 送料無料判定が「クーポン・ポイント適用前の商品合計金額」ベースであることが明記

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 観点: テスト戦略・カバレッジ目標・受入基準の検証可能性

**判定**: ✅ Pass with Notes（残存 Medium 2 件）

**前回指摘の確認**:
- [M8] FraudDetection / CampaignService テスト: 未修正
- [M9] DB スライステスト: 未修正

**新規指摘**: なし（N4 は programing-reviewer と重複）

**良い点（維持）**:
- CouponRuleEngine の単体テストが AAA パターンで記述（§17.1）
- パーセンテージ割引・最大割引上限のテストケースあり
- 統合テスト（WebApplicationFactory）で認証テストあり（§17.2）
- テスト命名が `Should_期待結果_When_条件` パターンに準拠

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 観点: パフォーマンス SLA・スケーラビリティ・キャッシュ戦略

**判定**: ✅ Pass with Notes（残存 Medium 1 件）

**前回指摘の確認**:
- [M10] Redis フォールバック: 未修正（§21 制約 5 で方針のみ）

**新規指摘**: なし（N6 OutboxPublisher 排他制御は architect と重複）

**良い点（修正による改善）**:
- OutboxPublisher の動的バックオフ（100ms〜5s 指数バックオフ）でポーリング負荷を最適化
- outbox_events の部分インデックスで未発行イベント取得を高速化
- gRPC Deadline 設計（300ms）が Redis キャッシュ + DB アクセスの実測に基づく適切な値

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 観点: GDPR・個人情報保護法・PCI DSS・データガバナンス

**判定**: ✅ Pass with Notes（残存 Medium 1 件）

**前回指摘の確認**:
- [M11] PII ログ方針: 未修正

**新規指摘**: なし

**良い点**:
- `user.deleted` イベントの購読が追加（§9.2）され、ユーザー削除時のデータ仮名化対応が設計済み
- gRPC メッセージに PII が含まれない設計（userId はシステム ID）

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 観点: NuGet ライセンス適合性・依存関係脆弱性・禁止パッケージ

**判定**: ✅ Pass

**前回指摘**: なし（前回も Pass）

- §2 の主要ライブラリ一覧が AGENTS.md / spec.md と一致
- 禁止パッケージの使用なし
- プレリリース版パッケージの使用なし
- gRPC 関連パッケージ（`Grpc.AspNetCore`）の記載は §2 に未追加だが、.NET 10 SDK 同梱のため影響なし

</details>

<details>
<summary>release-manager レビューレポート</summary>

### 観点: リリース戦略・ロールバック計画・バージョニング

**判定**: ✅ Pass（残存 Low 1 件）

**前回指摘の確認**:
- [L3] Dockerfile HEALTHCHECK curl: 未修正

**新規指摘**: なし

**良い点（維持）**:
- マルチステージビルド Dockerfile
- 非 root ユーザー（skishop）での実行
- HEALTHCHECK 設定あり

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 観点: コンテナ設計・可観測性・ヘルスチェック・DR 計画

**判定**: ✅ Pass with Notes（残存 Medium 1 件）

**前回指摘の確認**:
- [M12] Dockerfile EXPOSE 5006 vs 8080: 未修正

**新規指摘**: なし

**良い点（維持）**:
- ヘルスチェック（`/health`, `/health/ready`）実装あり
- PostgreSQL / Redis ヘルスチェック登録あり
- OpenTelemetry メトリクス設計あり

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 観点: トレーサビリティ・ドキュメント整合性・ADR

**判定**: ✅ Pass with Notes（残存 Medium 1 件）

**前回指摘の確認**:
- [M13] Correlation ID: 未修正

**新規指摘**: なし

**良い点（修正による改善）**:
- Outbox パターンの追加により、イベント発行の監査トレイル（outbox_events テーブル）が確立
- outbox_events に `retry_count`, `last_error`, `status` があり、障害調査が可能
- ADR-0005 への準拠が明確に設計に反映

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 観点: UX 設計品質・WCAG 2.1 準拠

**判定**: N/A — CouponService はバックエンドサービスのため UX レビュー対象外。

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 観点: 技術標準の横断適合性・実装実現可能性・規約遵守

**判定**: ✅ Pass with Notes（新規 Medium 1 件、残存 Medium 1 件）

**前回指摘の確認**:
- [H6] DB 名 coupondb: ✅ 解決確認
- [H7] Kafka トピック名: ✅ 解決確認 — `coupon.applied` / `coupon.expired` に統一
- [M16] ミドルウェアパイプライン: 未修正

**新規指摘**:
1. **[N7] プロジェクト構成の Repository 欠落**: C2 修正で `CouponType`, `Promotion` エンティティが追加されたが、対応する Repository インターフェース・実装が §15 プロジェクト構成に未定義。`ICouponRestrictionRepository` は追加済み。

**総評（イテレーション 2）**:
前回の Critical 2 件・High 8 件が全て是正され、設計書の品質が大幅に向上した。特に以下の改善が顕著:

1. **gRPC サービス定義（§10）**: Saga ステップ 3 の実装に必要な proto 定義・Deadline 設計・サーバー実装パターンが完備され、SalesManagementService との連携が実装可能になった
2. **Outbox パターン（§11）**: ADR-0005 準拠のイベント発行保証が設計され、outbox_events テーブル・OutboxPublisher・動的バックオフが実装レベルで記述された
3. **エンティティ完備**: CouponType / CouponRestriction / Promotion のテーブル定義が追加され、spec.md との整合性が確保された（ただし N1: FK 漏れあり）
4. **Kafka 整合性**: トピック名がドット区切り小文字形式に統一され、`user.deleted` 購読が追加された
5. **クーポン・ポイント適用順序（§21.1）**: Saga ステップとの整合を取った 6 ステップの適用順序が明確化された

新規 Medium 7 件は修正時に導入された軽微な不整合（TimeProvider 未統一、ER 図↔テーブル定義の FK 漏れ等）であり、実装フェーズでの解消が可能。実装フェーズへの進行を推奨する。

</details>
