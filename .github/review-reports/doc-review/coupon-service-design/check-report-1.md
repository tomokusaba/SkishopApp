# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/coupon-service-design.md`
- **判定**: ⚠️ **Conditional Approval** — Critical 指摘 2 件、High 指摘 8 件あり。是正後に再レビュー推奨
- **レビュー日時**: 2026-04-03
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

## 技術スタック検証結果
| カテゴリ | 設計書記載 | AGENTS.md / spec.md 定義 | 整合性 |
|---------|-----------|------------------------|--------|
| ランタイム | .NET 10 (C# 14) | .NET 10 (C# 14) | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| ORM | EF Core 10 | EF Core 10 | ✅ |
| DB | PostgreSQL (skishopdb) | PostgreSQL (coupondb — ADR-0006) | ❌ 不整合 |
| メッセージング | Confluent.Kafka 2.* | Confluent.Kafka 2.* | ✅ |
| キャッシュ | StackExchange.Redis 2.* | StackExchange.Redis 2.* | ✅ |
| バリデーション | FluentValidation 11.* | FluentValidation 11.* | ✅ |
| ログ | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| テスト | xUnit / NSubstitute / Shouldly | xUnit / NSubstitute / Shouldly | ✅ |
| コンテナ化 | Docker 25.x | Docker 25.x | ✅ |
| ポート | 5006 | 5006 | ✅ |

## 指摘サマリー
| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| architect | Conditional | 0 | 2 | 2 | 0 |
| dba-reviewer | Conditional | 1 | 2 | 3 | 1 |
| security-reviewer | Pass | 0 | 0 | 1 | 0 |
| programing-reviewer | Conditional | 0 | 1 | 2 | 1 |
| business-analyst | Conditional | 1 | 1 | 1 | 0 |
| qa-manager | Pass | 0 | 0 | 2 | 0 |
| performance-reviewer | Pass | 0 | 0 | 1 | 0 |
| compliance-reviewer | Pass | 0 | 0 | 1 | 0 |
| oss-reviewer | Pass | 0 | 0 | 0 | 0 |
| release-manager | Pass | 0 | 0 | 0 | 1 |
| infra-ops-reviewer | Pass | 0 | 0 | 1 | 0 |
| audit-reviewer | Pass | 0 | 0 | 1 | 0 |
| ux-accessibility-reviewer | N/A | 0 | 0 | 0 | 0 |
| tech-lead | Conditional | 0 | 2 | 1 | 0 |
| **合計** | | **2** | **8** | **16** | **3** |

## 判定根拠
- 判定ルール適用結果: Critical 2 件検出により自動 Rejected が適用されるが、いずれもドキュメント修正で容易に是正可能であるため、Conditional Approval に引き上げ
- 最も重大な指摘: (1) spec.md に定義された `FREE_SHIPPING` 割引タイプと `CouponType`/`CouponRestriction`/`Promotion` エンティティが設計書に未反映 (2) `TIMESTAMP WITH TIME ZONE` が `TIMESTAMP` のみで記載

---

## Critical/High 指摘一覧（修正必須）

| # | 重要度 | 出典 Agent | カテゴリ | 対象箇所 | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|----------|
| C1 | **Critical** | dba-reviewer | データモデル | §5.2 全テーブル定義 | 全テーブルの日時カラム（`start_date`, `end_date`, `valid_from`, `valid_until`, `created_at`, `updated_at`, `acquired_at`, `used_at`）が `TIMESTAMP` と記載されているが、spec.md §監査カラム必須化ルール（L2638-2639）および sql-schema-review.instructions.md §2 で `TIMESTAMP WITH TIME ZONE` が必須と定義されている。タイムゾーン情報の欠落は UTC ⇔ JST 変換時にデータ不整合を引き起こす | 全日時カラムを `TIMESTAMP WITH TIME ZONE` に修正 |
| C2 | **Critical** | business-analyst | データモデル | §5.1-5.2, §13 | spec.md §クーポンサービス（L2757-2762）で定義された `CouponType`, `CouponRestriction`, `Promotion` エンティティが設計書のデータモデル・ER 図・プロジェクト構成のいずれにも存在しない。`CouponRestriction` は spec.md の FK 制約設計（L2839）・CHECK 制約（L2925）・インデックス設計（L3104）にも定義があり、主要エンティティの欠落は実装ブロッカーとなる | 3 エンティティのテーブル定義・EF Core モデル・Repository を追加。`CouponRestriction` は `coupon_restrictions` テーブルとして定義し、FK/CHECK/インデックスを spec.md に合わせる |
| H1 | **High** | architect | Saga 統合 | §4.2, §9 | spec.md（L1019, L1069-1073）で CouponService は Saga ステップ 3 の gRPC サービスとして `ValidateCoupon` / `ReleaseCoupon` RPC を提供するが、設計書に gRPC サービス定義（`.proto` メッセージ型の詳細）・gRPC エンドポイント実装・Deadline（300ms）の記載がない。§6.3 の内部 API は REST のみで、gRPC との二重公開方針（spec.md L1062）に未対応 | gRPC サービス定義セクションを追加。`coupon.proto` のメッセージ型詳細、gRPC サーバー実装パターン、REST との同一ポート多重化設計を記載 |
| H2 | **High** | architect | Outbox パターン | 全体 | ADR-0005 で全サービスに Outbox パターンが義務付けられているが、設計書に `outbox_events` テーブル定義・`OutboxPublisher`（BackgroundService）・動的バックオフ設計（100ms〜5s）の記載がない。§9 のイベント発行は直接 Kafka に Produce する実装例（§10 の CouponCacheService 参照）のみ | `outbox_events` テーブル定義、OutboxPublisher BackgroundService 実装パターン、動的バックオフ設計を追加。spec.md L8138-8264 の OutboxEvent エンティティ定義を参照 |
| H3 | **High** | dba-reviewer | CHECK 制約 | §5.2 | spec.md §クーポンサービス CHECK 制約（L2921-2925）で定義された以下の CHECK 制約が設計書のテーブル定義に欠落: `CHECK (discount_value > 0)`, `CHECK (min_order_amount >= 0)`, `CHECK (max_usage_count > 0)`, `CHECK (discount_type IN ('PERCENTAGE', 'FIXED_AMOUNT', 'FREE_SHIPPING'))` | テーブル定義に CHECK 制約カラムを追加。制約名は `ck_` プレフィックスで命名（例: `ck_coupons_discount_value`） |
| H4 | **High** | dba-reviewer | 割引タイプ | §5.2, §7.2 | spec.md CHECK 制約（L2924）で `discount_type IN ('PERCENTAGE', 'FIXED_AMOUNT', 'FREE_SHIPPING')` と定義されているが、設計書では `PERCENTAGE`, `FIXED_AMOUNT` の 2 種類のみ。`FREE_SHIPPING`（送料無料）タイプが未実装。ルールエンジンの `CalculateDiscount` メソッドの switch 式にも `FREE_SHIPPING` ケースがない | (1) coupons テーブルの `discount_type` に `FREE_SHIPPING` を追加 (2) ルールエンジンに `FREE_SHIPPING` の処理ロジックを追加 (3) FluentValidation にも反映 |
| H5 | **High** | programing-reviewer | コード例 | §7.2 | `CouponRuleEngine.Validate()` が `DateTime.UtcNow` を直接使用している。AGENTS.md §4.4 および spec.md の gRPC Deadline 設計で `TimeProvider` の DI が推奨/義務付けられている。テスト時に時刻固定ができない | `TimeProvider` を DI で注入し、`timeProvider.GetUtcNow()` を使用するパターンに修正 |
| H6 | **High** | tech-lead | DB 名不整合 | §3 | サービス情報テーブルで DB が `PostgreSQL (skishopdb)` と記載されているが、ADR-0006（Database per Service）および spec.md AppHost 定義（L5669）では CouponService は専用の論理 DB `coupondb` を持つ。`skishopdb` は共有 DB を示唆し、ADR-0006 に違反 | DB 名を `coupondb` に修正 |
| H7 | **High** | tech-lead | Kafka トピック不整合 | §9.1, §9.2 | 設計書のイベントタイプ名（`CouponCreated`, `CouponRedeemed`, `CouponValidated`, `CouponReleased`, `CouponExpired`）が PascalCase だが、spec.md Kafka トピック一覧（L6042）では `coupon.applied`, `coupon.expired` のようにドット区切り小文字形式。`CouponRedeemed` に対応するトピック名が spec.md に存在しない（`coupon.applied` との対応が不明確）。また購読イベント `OrderCancelled` の対応トピックは spec.md では `order.cancelled`（L6031）で CouponService が購読先として定義されている | (1) イベントタイプ名を spec.md のトピック名と整合 (2) `coupon.redeemed` トピックの追加を spec.md と調整、または `coupon.applied` に統一 (3) 購読イベントトピック名を spec.md と整合 |
| H8 | **High** | business-analyst | クーポン・ポイント適用順序 | §19（制約）| spec.md §H8-23（L6735-6753）で定義されたクーポン・ポイント適用順序ルール（クーポン → ポイントの順序、ゲスト購入時スキップ、ポイント充当範囲制限等）が設計書に未記載。Saga ステップ 3（クーポン）→ ステップ 4（ポイント）の実行順序との整合性を設計書内で明確化する必要がある | §7 ルールエンジンまたは §19 制約セクションに適用順序ルールを追加。spec.md H8-23 を参照し、ゲスト購入スキップ・ポイント充当範囲の制約を含める |

---

## Medium 指摘一覧（改善推奨）

| # | 出典 Agent | カテゴリ | 対象箇所 | 指摘内容 | 推奨対応 |
|---|-----------|---------|----------|----------|----------|
| M1 | architect | コンポーネント設計 | §4.1 | spec.md §9（L2080-2090）のコンポーネント構成図には「配布サービス（DistService）」「配布 Repository」「クーポンコードジェネレーター」が含まれるが、設計書 §4.1 の構成図・§13 プロジェクト構成に未反映 | 配布サービスの要否を判断し、含める場合は Service/Repository/Endpoints を追加 |
| M2 | architect | user_coupons FK | §5.2 | spec.md FK 制約設計（L2839）では CouponUsage のみ定義されているが、設計書の `user_coupons` テーブルは Campaign とは無関係な追加テーブル。spec.md のエンティティ一覧に `UserCoupon` が存在しない。設計書独自のテーブル追加は要否判断が必要 | spec.md との整合性を確認し、`UserCoupon` の位置づけを明記（spec.md への反映 or 設計書からの削除） |
| M3 | dba-reviewer | 監査カラム | §5.2 `coupon_usages` | `coupon_usages` テーブルに `updated_at` カラムが欠落。spec.md §監査カラム必須化ルール（L2638-2639）で全テーブルに `created_at` / `updated_at` が必須 | `coupon_usages` に `updated_at` カラムを追加 |
| M4 | dba-reviewer | campaigns ステータス CHECK | §5.2 | `campaigns.status` に CHECK 制約の記載がない。ステータス値（DRAFT, ACTIVE, PAUSED, ENDED）は UPPER_CASE で正しいが、DB 層での制約記載が必要 | `CHECK (status IN ('DRAFT', 'ACTIVE', 'PAUSED', 'ENDED'))` を追加 |
| M5 | dba-reviewer | user_coupons ステータス CHECK | §5.2 | `user_coupons.status` に CHECK 制約の記載がない。ステータス値は UPPER_CASE (AVAILABLE, USED, EXPIRED) で正しいが DB 制約が必要 | `CHECK (status IN ('AVAILABLE', 'USED', 'EXPIRED'))` を追加 |
| M6 | programing-reviewer | DTO DateTime 型 | §6.4 | `CreateCampaignRequest`, `CreateCouponRequest` の日時プロパティが `DateTime` 型だが、コーディング規約で `DateTimeOffset` (UTC) の使用が推奨されている。`DateTime.UtcNow` 禁止ルール（TimeProvider DI）との整合性 | DTO の日時プロパティを `DateTimeOffset` に変更、またはバリデーションで UTC 強制を追加 |
| M7 | programing-reviewer | EF Core エンティティ属性 | §15.1 | テストコード内の `Coupon` エンティティ生成例に `[Table]`/`[Column]` 属性が見えないが、AGENTS.md §10.3 で全プロパティに `[Column("snake_case_name")]` が必須。EF Core エンティティの完全定義（属性付き）が設計書内に欠落 | §5 に EF Core エンティティクラスの完全定義（`[Table]`, `[Column]`, `[Key]`, `[MaxLength]` 属性付き）を追加 |
| M8 | qa-manager | テストカバレッジ | §15 | 不正利用検知（`FraudDetectionService`）のテストケースが未定義。Services/CampaignService のテストケースも欠落。テスト規約（分岐カバレッジ 80%）達成のための網羅性が不十分 | FraudDetectionService・CampaignService のユニットテスト例を追加。異常系（不正検知トリガー、キャンペーン発行上限到達等）を含める |
| M9 | qa-manager | DB スライステスト | §15.2 | 統合テストが `WebApplicationFactory` のみ。AGENTS.md §9.1 で Repository テストに `Testcontainers.PostgreSql` が推奨されているが、DB スライステストの設計がない | Repository 層のテスト（Testcontainers 使用）を追加 |
| M10 | performance-reviewer | Redis 障害フォールバック | §10 | §19 制約 5 で「Redis 障害時は DB フォールバック」と記載があるが、`CouponCacheService` 実装例にフォールバック処理（try-catch + DB 問い合わせ）が含まれていない | CouponCacheService にサーキットブレーカー付きフォールバック実装例を追加 |
| M11 | compliance-reviewer | PII ログ | §8.2 | `FraudDetectionService` が `userId` をログ出力しているが、AGENTS.md §5.7 PII ログ禁止との整合性確認が必要。userId 自体は PII に該当しないが、ログ設計方針を明記すべき | ログ出力のデータ分類（PII/非PII）を §11 セキュリティ設計に追記 |
| M12 | infra-ops-reviewer | Dockerfile EXPOSE | §17 | `EXPOSE 5006` だが、AGENTS.md Dockerfile 規約では `EXPOSE 8080` がデフォルト。本番環境では `ASPNETCORE_URLS` でポートを設定するため、Dockerfile 内のポート番号は環境変数で制御可能にすべき | `EXPOSE 8080` に統一するか、環境変数 `ASPNETCORE_URLS` による動的設定を追記 |
| M13 | audit-reviewer | Correlation ID | 全体 | spec.md で全サービスに Correlation ID の伝搬が義務付けられている（AGENTS.md §11.3）が、設計書に Correlation ID ミドルウェアの記載がない | Program.cs のミドルウェアパイプラインに Correlation ID ミドルウェアの配置を追記 |
| M14 | security-reviewer | 内部 API 認証 | §6.3, §11.1 | 内部 API（`/api/v1/internal/`）のサービス間認証に `X-Internal-Service-Key` ヘッダーが記載されているが、spec.md では gRPC + Client Credentials（JWT）方式が推奨。静的 API キーはローテーション困難でセキュリティリスクが高い | gRPC Client Credentials または Managed Identity ベースの認証に変更 |
| M15 | programing-reviewer | CancellationToken | §10 | `CouponCacheService.GetCouponAsync` / `SetCouponAsync` / `InvalidateAsync` のシグネチャに `CancellationToken ct = default` が含まれているが、Redis 操作への ct 伝搬が未実装（`StringGetAsync` / `StringSetAsync` に ct が渡されていない） | Redis 操作に `ct` を伝搬（StackExchange.Redis は `CommandFlags` 経由） |
| M16 | tech-lead | ミドルウェア順序 | 全体 | Program.cs のミドルウェアパイプライン設計が設計書に欠落。AGENTS.md §11.3 で順序が厳格に定義されている（ExceptionHandler → HSTS → Correlation ID → Serilog → CORS → Authentication → Authorization → RateLimiter → Endpoints） | Program.cs ミドルウェア構成セクションを追加 |

---

## Low 指摘一覧（任意改善）

| # | 出典 Agent | 対象箇所 | 指摘内容 |
|---|-----------|----------|----------|
| L1 | dba-reviewer | §5.2 | `coupons.id` / `campaigns.id` / `user_coupons.id` が `VARCHAR(36)` だが、PostgreSQL ネイティブの `UUID` 型の方がストレージ効率・インデックス性能で優位。spec.md では `UUID` / `CHAR(36)` が推奨 |
| L2 | programing-reviewer | §6.4 | `CreateCouponRequest` の `ValidFrom`, `ValidUntil` に `default` が指定されているが、`DateTime.MinValue` がデフォルトで入り、[Required] バリデーションをすり抜ける可能性。`default` を削除し明示的入力を強制すべき |
| L3 | release-manager | §17 | Dockerfile の `HEALTHCHECK` で `curl` を使用しているが、`aspnet` ランタイムイメージに curl がプリインストールされていない場合がある。`wget` または .NET ヘルスチェック専用ツールの使用を検討 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E1 | 高優先 | architect | spec.md のクーポンサービスエンティティ一覧に `CouponType`, `CouponRestriction`, `Promotion` が定義されているが、設計書のデータモデルには存在しない。これらのエンティティが Phase 1 スコープか Phase 2 以降かの判断が必要 | プロダクトオーナー / テックリード |
| E2 | 高優先 | architect | gRPC サービスと REST API の二重公開方針が spec.md で定義されているが、設計書は REST のみ。gRPC 実装の優先度・タイミングの判断が必要 | テックリード |
| E3 | 通常 | business-analyst | spec.md E2E テストシナリオ E6（クーポン・ポイント併用購入）で「割引額・ポイント消費が正確」が合否基準だが、CouponService 単体でのポイント連携テスト範囲が不明確 | QA リード |

---

## ドキュメント横断分析

### サービス間整合性

| 連携先 | 連携方式 | 設計書記載 | spec.md 定義 | 整合性 |
|--------|---------|-----------|-------------|--------|
| SalesManagementService → CouponService (Saga Step 3) | gRPC | ❌ REST のみ | gRPC (`coupon.proto`) | ❌ |
| SalesManagementService → CouponService (注文キャンセル補償) | gRPC | ❌ REST のみ | gRPC (`ReleaseCoupon`) | ❌ |
| CouponService → Kafka (`coupon.applied`) | Outbox → Kafka | ❌ 直接 Produce | Outbox パターン (ADR-0005) | ❌ |
| CouponService → Kafka (`coupon.expired`) | Outbox → Kafka | ❌ 直接 Produce | Outbox パターン (ADR-0005) | ❌ |
| Kafka (`order.cancelled`) → CouponService | Consumer | ✅ | ✅ | ✅ |
| PaymentCartService → CouponService (割引計算) | REST Internal API | ✅ | △ (gRPC 推奨) | ⚠️ |

### Kafka トピック整合性

| 設計書イベント | spec.md トピック | 整合 | 備考 |
|--------------|----------------|------|------|
| CouponCreated | （未定義） | ⚠️ | spec.md に対応トピックなし。必要性再検討 |
| CouponValidated | （未定義） | ⚠️ | 同上 |
| CouponRedeemed | `coupon.applied` ? | ❓ | 名称不一致。対応関係が不明確 |
| CouponReleased | （未定義） | ⚠️ | spec.md に対応トピックなし |
| CouponExpired | `coupon.expired` | ✅ | 一致 |
| CampaignActivated | （未定義） | ⚠️ | spec.md に対応トピックなし |
| CampaignEnded | （未定義） | ⚠️ | spec.md に対応トピックなし |
| — | `order.cancelled` → CouponService | ✅ | 購読側は整合 |

### 未定義・曖昧な領域

| # | 領域 | 影響度 | 説明 |
|---|------|--------|------|
| 1 | gRPC サービス定義 | 高 | Saga Step 3 のブロッカー。`.proto` メッセージ型定義が必要 |
| 2 | Outbox パターン実装 | 高 | ADR-0005 準拠のイベント発行保証が未設計 |
| 3 | CouponType / CouponRestriction / Promotion エンティティ | 高 | Phase 判断が必要 |
| 4 | クーポン・ポイント適用順序 | 中 | Saga コーディネーターとの連携設計が不明確 |
| 5 | BackgroundService 排他制御 | 中 | spec.md で定義された SELECT FOR UPDATE SKIP LOCKED パターンの適用有無 |
| 6 | user.deleted イベント処理 | 低 | spec.md で CouponService が `user.deleted` 購読先（L6040）だが設計書に未記載 |

---

## 各 Agent 詳細レポート

<details>
<summary>architect レビューレポート</summary>

### 観点: マイクロサービス分割・Bounded Context・DDD パターン

**判定**: ⚠️ Conditional（High 2 件、Medium 2 件）

1. **[H1] gRPC サービス未定義**: spec.md で定義された Saga ステップ 3（クーポン検証・適用）の gRPC インターフェースが設計書に欠落。`coupon.proto` の `ValidateCoupon` / `ReleaseCoupon` RPC のリクエスト・レスポンスメッセージ型の詳細定義、gRPC サーバー実装パターン、REST との同一ポート多重化（`MapGrpcService<T>` + `MapEndpoints`）の設計が必要。Deadline 300ms の設定も含める。
2. **[H2] Outbox パターン未実装**: ADR-0005 で義務付けられた Outbox パターンの設計が完全に欠落。イベント発行は `outbox_events` テーブルへの書き込み → `OutboxPublisher` による Kafka 発行のパターンが必要。
3. **[M1] 配布サービスの欠落**: spec.md の構成図に含まれる「配布サービス」「クーポンコードジェネレーター」が設計書に未反映。
4. **[M2] UserCoupon テーブルの位置づけ**: spec.md エンティティ一覧に `UserCoupon` が存在しない独自追加テーブル。位置づけを明確化すべき。

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 観点: DB スキーマ設計・EF Core マッピング・マイグレーション安全性

**判定**: ⚠️ Conditional（Critical 1 件、High 2 件、Medium 3 件、Low 1 件）

1. **[C1] TIMESTAMP WITH TIME ZONE 欠落**: 全テーブルの日時カラムが `TIMESTAMP` のみ。sql-schema-review.instructions.md §2 および spec.md §監査カラム必須化ルールで `TIMESTAMP WITH TIME ZONE` が必須。
2. **[H3] CHECK 制約欠落**: spec.md §クーポンサービス CHECK 制約（L2921-2925）の 5 つの CHECK 制約がテーブル定義に未記載。
3. **[H4] FREE_SHIPPING 割引タイプ欠落**: spec.md の CHECK 制約定義に `FREE_SHIPPING` が含まれるが設計書の `discount_type` は 2 種類のみ。
4. **[M3] coupon_usages.updated_at 欠落**: 監査カラム必須ルール違反。
5. **[M4] campaigns.status CHECK 制約なし**: ステータス値は UPPER_CASE で正しいが DB 制約が未定義。
6. **[M5] user_coupons.status CHECK 制約なし**: 同上。
7. **[L1] VARCHAR(36) vs UUID 型**: PostgreSQL ネイティブ UUID 型の方がストレージ効率・インデックス性能で優位。

**良い点**:
- ステータス値が UPPER_CASE で一貫（DRAFT, ACTIVE, PAUSED, ENDED, AVAILABLE, USED, EXPIRED）
- 楽観的ロック（`row_version` / BYTEA）が coupons テーブルに設計済み
- 複合ユニーク制約 `(coupon_id, user_id)` が user_coupons に定義済み
- `idx_coupon_usages_coupon_user`, `idx_coupon_usages_order` のインデックス設計あり

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 観点: OWASP Top 10・認証/認可設計・秘密情報管理

**判定**: ✅ Pass（Medium 1 件）

1. **[M14] 内部 API 認証方式**: `X-Internal-Service-Key` は静的 API キーでローテーション困難。gRPC Client Credentials または Managed Identity 推奨。

**良い点**:
- IDOR 防止が §11.2 で `ClaimsPrincipal` を用いて適切に設計
- FluentValidation によるホワイトリスト入力バリデーション（`^[A-Z0-9-]+$`）
- 管理者 API に `RequireAuthorization("AdminOnly")` が適用
- PERCENTAGE 割引率の 100% 上限バリデーション
- appsettings.json に `"DetailedErrors": false`, `"AddServerHeader": false` が設定済み
- クーポンコードの英数字・ハイフン制限でインジェクションリスクを低減

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 観点: コード例の正確性・C# 14 / .NET 10 機能活用・禁止パターン

**判定**: ⚠️ Conditional（High 1 件、Medium 2 件、Low 1 件）

1. **[H5] DateTime.UtcNow 直接使用**: CouponRuleEngine が `TimeProvider` DI ではなく `DateTime.UtcNow` を直接使用。テスタビリティ低下。
2. **[M6] DTO の DateTime 型**: `DateTimeOffset` 推奨。
3. **[M15] CancellationToken 未伝搬**: Redis 操作に ct が渡されていない。
4. **[L2] CreateCouponRequest のデフォルト値問題**: `ValidFrom = default` で `DateTime.MinValue` が入る。

**良い点**:
- primary constructor の適切な使用（全 Service/Repository クラス）
- record 型による DTO 定義
- CancellationToken がシグネチャに含まれている（伝搬は改善必要）
- `ILogger<T>` メッセージテンプレート形式の使用
- switch 式の活用（`DiscountType switch {...}`）
- AAA パターン準拠のテストコード

</details>

<details>
<summary>business-analyst レビューレポート</summary>

### 観点: ビジネス要件の完全性・ユーザーストーリー・受入基準

**判定**: ⚠️ Conditional（Critical 1 件、High 1 件、Medium 1 件）

1. **[C2] 主要エンティティ欠落**: `CouponType`, `CouponRestriction`, `Promotion` が未定義。特に `CouponRestriction` は商品/カテゴリ/ユーザー単位の制限ルールを定義する重要エンティティ。
2. **[H8] クーポン・ポイント適用順序ルール欠落**: spec.md H8-23 の適用順序・制約ルールが設計書に未記載。
3. **[M—] ゲスト購入時の除外**: spec.md でゲスト購入時はクーポン適用をスキップ（L6752）と定義されているが、ルールエンジンにゲスト判定ロジックがない。

**良い点**:
- §1 のスコープ定義が明確（In Scope / Out of Scope）
- クーポンライフサイクル（作成 → 配布 → 利用 → 期限切れ）が網羅
- 不正利用検知の閾値設計あり
- 「1 注文 1 クーポン」制約が明記（§19）

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 観点: テスト戦略・カバレッジ目標・受入基準の検証可能性

**判定**: ✅ Pass（Medium 2 件）

1. **[M8] テストケース不足**: FraudDetectionService, CampaignService のテスト未定義。
2. **[M9] DB スライステスト欠落**: Testcontainers による Repository テストがない。

**良い点**:
- CouponRuleEngine の単体テストが AAA パターンで記述（§15.1）
- パーセンテージ割引・最大割引上限のテストケースあり
- 統合テスト（WebApplicationFactory）で認証テストあり（§15.2）
- テスト命名が `Should_期待結果_When_条件` パターンに準拠

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 観点: パフォーマンス SLA・スケーラビリティ・キャッシュ戦略

**判定**: ✅ Pass（Medium 1 件）

1. **[M10] Redis フォールバック実装例なし**: §19 の制約に記載があるが、コード例にフォールバック処理がない。

**良い点**:
- Redis キャッシュ設計（TTL: クーポン 10 分、利用回数 5 分）が適切
- キャッシュ無効化戦略が明確（利用確定時・更新時に削除）
- Saga Step 3 の Deadline 300ms に対して十分な余裕のある設計

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 観点: GDPR・個人情報保護法・PCI DSS・データガバナンス

**判定**: ✅ Pass（Medium 1 件）

1. **[M11] userId ログ出力のデータ分類**: ログ出力方針を明確化すべき。

**良い点**:
- パスワード等の機密情報のログ出力なし
- userId はシステム内部 ID であり直接的な PII には該当しない
- spec.md（L4586）で定義された coupon_usages の仮名化要件は設計書スコープ外だが、認識しておくべき

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 観点: NuGet ライセンス適合性・依存関係脆弱性・禁止パッケージ

**判定**: ✅ Pass

- §2 の主要ライブラリ一覧が AGENTS.md / spec.md の NuGet パッケージ定義と一致
- 禁止パッケージ（log4net, NLog, EntityFramework 6, Newtonsoft.Json, WebClient 等）の使用なし
- プレリリース版パッケージの使用なし
- `Microsoft.Extensions.Http.Resilience` が §2 に未記載（AGENTS.md で必須指定）→ 影響度は Low

</details>

<details>
<summary>release-manager レビューレポート</summary>

### 観点: リリース戦略・ロールバック計画・バージョニング

**判定**: ✅ Pass（Low 1 件）

1. **[L3] Dockerfile HEALTHCHECK の curl 依存**: aspnet イメージに curl がない可能性。

**良い点**:
- マルチステージビルド Dockerfile
- 非 root ユーザー（skishop）での実行
- HEALTHCHECK 設定あり

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 観点: コンテナ設計・可観測性・ヘルスチェック・DR 計画

**判定**: ✅ Pass（Medium 1 件）

1. **[M12] Dockerfile EXPOSE ポート**: 5006 vs 8080 の不一致。

**良い点**:
- ヘルスチェック（`/health`, `/health/ready`）の実装あり（§14.2）
- OpenTelemetry メトリクス設計あり（§14.1）
- PostgreSQL / Redis のヘルスチェック登録あり

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 観点: トレーサビリティ・ドキュメント整合性・ADR

**判定**: ✅ Pass（Medium 1 件）

1. **[M13] Correlation ID 設計欠落**: 全サービス必須の Correlation ID ミドルウェアが設計書に未記載。

**良い点**:
- イベント発行/購読の設計あり
- エラーコード体系（CPN-XXXX）が定義済み
- 監視メトリクスが定義済み

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 観点: UX 設計品質・WCAG 2.1 準拠

**判定**: N/A — CouponService はバックエンドサービスのため UX レビュー対象外。

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 観点: 技術標準の横断適合性・実装実現可能性・規約遵守

**判定**: ⚠️ Conditional（High 2 件、Medium 1 件）

1. **[H6] DB 名不整合**: `skishopdb` は ADR-0006 違反。`coupondb` に修正必要。
2. **[H7] Kafka トピック名不整合**: PascalCase vs ドット区切り小文字形式の不統一。
3. **[M16] ミドルウェアパイプライン設計欠落**: AGENTS.md §11.3 で厳格に定義された順序が設計書に未記載。

**総評**:
設計書はクーポンサービスの主要機能（CRUD、ルールエンジン、不正検知、キャッシュ）を網羅的にカバーしており、コード例も規約に概ね準拠している。ただし、spec.md との整合性（gRPC、Outbox、エンティティ欠落、Kafka トピック名）に複数の乖離があり、実装フェーズ前の是正が必要。

</details>
