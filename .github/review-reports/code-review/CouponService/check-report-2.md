# ソースコードレビュー統合レポート — CouponService

## 判定結果
- **対象**: `Services/CouponService/` 配下の全ソースコード（32 ファイル）+ `Services/CouponService.Tests/`（5 テストファイル, 38 テストメソッド）
- **判定**: ❌ **Rejected** — Critical 指摘が 3 件以上検出
- **レビュー日時**: 2026-04-06
- **プロジェクト**: SkiShop (.NET 10 マイクロサービス EC サイト)

---

## 🚨 CRITICAL 指摘検出 — 自動 Rejected

---

## 指摘サマリー

| Agent | 判定 | Critical | High | Medium | Low | スコア |
|-------|------|----------|------|--------|-----|--------|
| tech-lead | ❌ | 2 | 7 | 4 | 2 | 16/20 |
| architecture-reviewer | ⚠️ | 0 | 2 | 5 | 4 | 21/25 |
| ddd-domain-reviewer | ❌ | 4 | 6 | 4 | 2 | 11/30 |
| api-endpoint-reviewer | ⚠️ | 1 | 5 | 6 | 2 | 19/25 |
| csharp-standards-reviewer | ⚠️ | 0 | 1 | 4 | 1 | 24/25 |
| async-concurrency-reviewer | ✅ | 0 | 1 | 2 | 2 | 24/25 |
| error-logging-reviewer | ⚠️ | 0 | 2 | 4 | 2 | 21/25 |
| data-access-reviewer | ⚠️ | 0 | 4 | 6 | 2 | 20/25 |
| config-di-reviewer | ⚠️ | 0 | 2 | 3 | 1 | 22/25 |
| security-reviewer | ❌ | 2 | 5 | 4 | 2 | — |
| dependency-reviewer | ⚠️ | 0 | 2 | 3 | 2 | 22/25 |
| test-quality-reviewer | ⚠️ | 0 | 9 | 7 | 2 | 17/25 |
| performance-reviewer | ⚠️ | 1 | 8 | 5 | 2 | 15/25 |
| resilience-reviewer | ⚠️ | 0 | 5 | 5 | 2 | 17/25 |
| **合計（重複排除前）** | | **10** | **59** | **62** | **28** | |
| **合計（重複排除後）** | | **3** | **21** | **27** | **12** | |

---

## 判定根拠

- **判定ルール適用結果**: Critical 指摘が 3 件検出されたため、自動 ❌ Rejected
- **最も重大な指摘**:
  1. `ReleaseCouponAsync` が冪等ではなく、二重呼び出しで `CurrentUsageCount` の不正減算が発生する（Saga 補償トランザクションの根幹に関わる）
  2. `CreateCouponRequestValidator` / `UpdateCouponRequestValidator` で `DiscountType == 0`（FixedAmount）にパーセント割引の上限制約を適用。パーセント割引（`DiscountType == 1`）には上限チェックが無い
  3. JWT `ValidAlgorithms` が未指定で `alg:none` / 鍵混同攻撃のリスク

---

## Critical/High 指摘一覧（修正必須）

### Critical（3 件）

| # | 重要度 | 出典 Agent | カテゴリ | 対象ファイル | 行番号 | 指摘内容 | 修正コード例 |
|---|--------|-----------|---------|------------|--------|----------|------------|
| C-1 | **Critical** | tech-lead, ddd-domain | データ整合性 | [CouponAppService.cs](Services/CouponService/Services/CouponAppService.cs#L195-L218) | L195-218 | **`ReleaseCouponAsync` が冪等ではない。** `CouponUsage` レコードを削除/無効化せずに `CurrentUsageCount` のみ減算。再呼び出しで同一 usage を再検出し二重減算が発生する。Saga 補償トランザクションとして冪等性は必須。 | usage レコードの削除 or `IsReleased` フラグ追加。削除後に usage が `null` なら early return で冪等性保証 |
| C-2 | **Critical** | tech-lead, csharp, api-endpoint, architecture | バリデーション | [CouponValidators.cs](Services/CouponService/Validators/CouponValidators.cs#L27-L30) | L27-30, L125 | **パーセント割引の上限バリデーションが誤った DiscountType に適用。** `When(x => x.DiscountType == 0, ...)` は `FixedAmount` だが「100%以下」制約を適用。結果: (1) 固定額が≤100に不当制限 (2) パーセント割引に上限なし（100%超の割引作成可能）。`UpdateCouponRequestValidator` (L125) も同バグ。 | `When(x => x.DiscountType == 1, ...)` に修正 |
| C-3 | **Critical** | security | 認証 | [Program.cs](Services/CouponService/Program.cs#L60-L68) | L60-68 | **JWT `ValidAlgorithms` 未指定。** `alg:none` 攻撃や RS256→HS256 鍵混同攻撃のリスク。`RequireSignedTokens` も未設定。 | `ValidAlgorithms = ["RS256"]`, `RequireSignedTokens = true` を追加 |

### High（21 件）

| # | 重要度 | 出典 Agent | カテゴリ | 対象ファイル | 行番号 | 指摘内容 | 修正コード例 |
|---|--------|-----------|---------|------------|--------|----------|------------|
| H-1 | High | security, api-endpoint | DoS | [Program.cs](Services/CouponService/Program.cs#L115-L129) / 全 Endpoints | — | **レート制限が定義済みだがエンドポイントに未適用。** `coupon-api` / `redeem-api` ポリシーが `.RequireRateLimiting()` で適用されていない。 | 各エンドポイントグループに `.RequireRateLimiting("coupon-api")` 追加 |
| H-2 | High | security, tech-lead | データ整合性 | [CouponAppService.cs](Services/CouponService/Services/CouponAppService.cs#L134-L139) | L134 | **`RedeemCouponAsync` でクーポンの有効性を再検証していない。** アクティブ/期限/利用上限のチェックなしに利用確定。TOCTOU リスク。 | Redeem 前に `ruleEngine.ValidateAsync()` を呼び出し |
| H-3 | High | security | データ改ざん | [CouponAppService.cs](Services/CouponService/Services/CouponAppService.cs#L143-L148) | L143 | **`RedeemCouponRequest.DiscountAmount` をサーバー側で再計算していない。** リクエストの割引額をそのまま記録。内部サービス侵害時に任意の割引額を記録可能。 | 割引額の整合性チェックを追加 |
| H-4 | High | security | 認証設計 | [InternalCouponEndpoints.cs](Services/CouponService/Endpoints/InternalCouponEndpoints.cs#L43-L46) | L43-46 | **`RedeemCoupon` の userId が `"internal-service"` になる。** 内部 JWT の `sub` から取得される userId が "internal-service" となり、ユーザーごとの利用制限が機能しない。 | `RedeemCouponRequest` に `UserId` フィールドを追加し、リクエストボディから取得 |
| H-5 | High | security | 認証脆弱性 | [InternalServiceHandler.cs](Services/CouponService/Infrastructure/Authorization/InternalServiceHandler.cs#L10-L14) | L10-14 | **`InternalServiceHandler` が `sub == "internal-service"` のみで認証。** 追加の検証（scope, iss 等）がなく、JWT 脆弱性と複合するとバイパス可能。 | 複数クレーム（sub + scope + iss）による検証強化 |
| H-6 | High | architecture, config-di, dependency, tech-lead | 規約違反 | [Program.cs](Services/CouponService/Program.cs) | — | **`AddOpenApi()` / `MapOpenApi()` 未設定。** AGENTS.md §6.0 で .NET 10 では必須。`Microsoft.AspNetCore.OpenApi` パッケージも欠落。 | パッケージ追加 + `builder.Services.AddOpenApi()` + `app.MapOpenApi()` |
| H-7 | High | error-logging, resilience, tech-lead | 可観測性 | [Program.cs](Services/CouponService/Program.cs#L186-L193) | L186 | **OpenTelemetry に `AddEntityFrameworkCoreInstrumentation()` 未設定。** EF Core SQL クエリトレーシングが無効。 | `.AddEntityFrameworkCoreInstrumentation()` 追加 |
| H-8 | High | architecture, tech-lead | レイヤー違反 | [OutboxPublisher.cs](Services/CouponService/BackgroundServices/OutboxPublisher.cs#L46-L100) | L46-100 | **`OutboxPublisher` が `AppDbContext` を直接操作。** Repository パターンをバイパス。Advisory Lock は妥当だが、イベント操作は `IOutboxEventRepository` に移譲すべき。 | Repository にメソッド追加し移譲 |
| H-9 | High | data-access | 楽観的ロック漏れ | [CouponAppService.cs](Services/CouponService/Services/CouponAppService.cs#L227-L260) | L260 | **`ReleaseCouponAsync` で `DbUpdateConcurrencyException` をハンドリングしていない。** `CurrentUsageCount` 変更時にロック競合が起こり得る。 | `try/catch (DbUpdateConcurrencyException)` 追加 |
| H-10 | High | data-access | エンティティ不備 | [CouponRestriction.cs](Services/CouponService/Models/CouponRestriction.cs) | — | **`CouponRestriction` に `updated_at` プロパティが欠落。** 他の変更可能エンティティは全て `UpdatedAt` を持つ。AppDbContext の自動更新が機能しない。 | `[Column("updated_at")] public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;` 追加 |
| H-11 | High | dependency | パッケージ欠落 | [CouponService.csproj](Services/CouponService/CouponService.csproj) | — | **`Microsoft.Identity.Web` (Version `3.*`) 欠落。** AGENTS.md §8.1 の必須パッケージ。他の全サービスに含まれている。 | パッケージ追加 |
| H-12 | High | resilience | Kafka Producer 設定不足 | [Program.cs](Services/CouponService/Program.cs#L112-L116) | L112 | **`ProducerConfig` に `Acks`/`EnableIdempotence`/`MessageSendMaxRetries` 未設定。** メッセージロスのリスク。 | `Acks = Acks.All`, `EnableIdempotence = true` を設定 |
| H-13 | High | resilience | ヘルスチェック | [Program.cs](Services/CouponService/Program.cs#L181-L184) | L181 | **Kafka のヘルスチェック未実装。** Readiness チェックに Kafka 接続確認を含めるべき。 | `AspNetCore.HealthChecks.Kafka` パッケージ追加 + Readiness に追加 |
| H-14 | High | resilience | Redis フォールバック不完全 | [CouponCacheServiceRedis.cs](Services/CouponService/Services/CouponCacheServiceRedis.cs#L28-L31) | L28 | **`RedisConnectionException` のみ catch。** `RedisTimeoutException`/`RedisServerException` も発生しうる。`catch (RedisException)` に変更すべき。 | `catch (RedisException ex)` に変更 |
| H-15 | High | resilience | Redis 動的フォールバック | [Program.cs](Services/CouponService/Program.cs#L95-L103) | L95 | **Redis フォールバックが起動時の接続文字列有無のみで決定。** 稼働中の Redis ダウン時に InMemory へ動的フォールスルーする仕組みなし。 | サーキットブレーカー付きデコレータパターンの導入 |
| H-16 | High | resilience | Consumer バックオフ | [OrderEventConsumer.cs](Services/CouponService/Consumers/OrderEventConsumer.cs#L49) | L49 | **Kafka Consumer のバックオフが固定 5 秒。** 連続障害時にリトライストームリスク。指数バックオフが必要。 | 動的バックオフ（100ms〜30s）に変更 |
| H-17 | High | async-concurrency | Redis ct 未伝搬 | [CouponCacheServiceRedis.cs](Services/CouponService/Services/CouponCacheServiceRedis.cs#L22) | L22 | **StackExchange.Redis の非同期メソッドに CancellationToken 未伝搬。** ct をシグネチャで受け取りながら未使用。 | 各 Redis 呼び出し前に `ct.ThrowIfCancellationRequested()` を追加 |
| H-18 | High | performance | ループ内 DB クエリ | [CouponCodeGenerator.cs](Services/CouponService/Services/CouponCodeGenerator.cs#L30-L35) | L30 | **`GenerateBatchAsync` が `count` 回ループで都度 `ExistsAsync()` DB クエリ。** `count=100` で最大 1,000 回のラウンドトリップ。 | 一括生成 + `WHERE code IN (...)` で一括存在チェック |
| H-19 | High | performance | 二重クエリ | [CouponAppService.cs](Services/CouponService/Services/CouponAppService.cs#L162-L170) | L162 | **`CalculateDiscountAsync` で `FindByCodeAsync` を二重実行。** `ruleEngine.ValidateAsync` 内で取得済みのクーポンを再取得。 | `ValidateAsync` の戻り値に CouponId 等を含める |
| H-20 | High | performance | ページネーション欠落 | [Repositories.cs](Services/CouponService/Repositories/Repositories.cs) | 複数 | **`GetByUserIdAsync`/`GetByStatusAsync`/`GetByDateRangeAsync` にページネーション未実装。** 大量データで OOM リスク。 | `Skip/Take` パラメータ追加 |
| H-21 | High | test-quality, tech-lead | テスト不足 | テストプロジェクト全体 | — | **統合テスト・Endpoint テスト・Repository テスト・セキュリティテストが存在しない。** Unit テスト 38 件のみ。カバレッジ 80% 未達見込み。3 Service クラスが完全未テスト。 | `WebApplicationFactory` + `Testcontainers.PostgreSql` でテスト追加 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| 1 | **最優先** | tech-lead, ddd | `ReleaseCouponAsync` の冪等性修正方針（usage 削除 vs IsReleased フラグ）の決定 — Saga 補償トランザクションの設計に直結 | テックリード + ドメインエキスパート |
| 2 | **最優先** | security | `InternalServiceHandler` の認証強化方針 — 複数クレーム検証の設計 | セキュリティチーム |
| 3 | **高優先** | security | `HandleUserDeleted` の仮名化処理未実装 — GDPR 対応リスク | 法務 + プロダクトオーナー |
| 4 | **高優先** | resilience | Redis 動的フォールバック戦略 — サーキットブレーカーパターン導入可否 | インフラチーム |
| 5 | **高優先** | ddd | Aggregate Root 境界の再編成（`CouponUsageRepository`/`UserCouponRepository` の統合）— 大規模リファクタリングの可否 | テックリード |
| 6 | **通常** | dependency | xUnit v3 (`xunit.v3`) 移行がプロジェクト全体の方針か確認 — AGENTS.md §8.1 との乖離 | テックリード |
| 7 | **通常** | config-di | `UseCors()` の要否 — 内部サービスとしてブラウザ直接アクセスなしなら不要の場合あり | アーキテクト |

---

## 競合解決記録

Phase 3 で Agent 間の矛盾する指摘は検出されませんでした。各 Agent は異なる観点から同一問題を検出しており、指摘の方向性は一致しています。

---

## 設計書との照合結果

### 設計書からの逸脱
| 設計書セクション | 逸脱内容 |
|----------------|---------|
| §5 エンティティ定義 | `coupons.target_category`, `coupons.target_product_id` が欠落（`CouponRestriction` で代替 — 改善として解釈可能だが設計書未更新） |
| §5 DiscountType の型 | 設計書: VARCHAR + CHECK 制約、実装: enum int。DB 表現が異なる |
| §5 CouponRestriction | `updated_at` カラム欠落 |
| §10 gRPC | スタブ実装のみ（意図的 — proto 整備後に有効化と明記） |
| AGENTS.md §6.0 | `AddOpenApi()` / `MapOpenApi()` 未設定 |

### 未実装の設計要素
| 要素 | 内容 |
|------|------|
| gRPC サービス | proto ファイル未生成、スタブのみ |
| GDPR 仮名化処理 | `user.deleted` イベントのログ出力のみ |
| Dead Letter Topic | Kafka Consumer の DLT 転送未実装 |

---

## 各 Agent 詳細レポート

<details>
<summary>tech-lead レビューレポート</summary>

**判定**: ❌ Fail | Critical: 2 / High: 7 / Medium: 4 / Low: 2 | スコア: 16/20

禁止事項は全 10 項目クリア。Critical 2 件（Release 冪等性欠如・Validator バグ）の修正が最優先。テスト不足（統合/Endpoint/Repository テスト皆無）も早期対応必要。設計書との部分的逸脱あり。
</details>

<details>
<summary>architecture-reviewer レビューレポート</summary>

**判定**: ⚠️ Warning | Critical: 0 / High: 2 / Medium: 5 / Low: 4 | スコア: 21/25

レイヤー依存方向（Endpoints→Services→Repositories）は完全準拠。プロジェクト構成も規約に適合。High 指摘: OutboxPublisher の DbContext 直接操作、OpenAPI 未設定。Medium: BackgroundService からの Service 層バイパス（2 件）、GetAllCouponsAsync の誤ったメソッド呼出し、ファイル粒度。
</details>

<details>
<summary>ddd-domain-reviewer レビューレポート</summary>

**判定**: ❌ Fail | Critical: 4 / High: 6 / Medium: 4 / Low: 2 | スコア: 11/30

Aggregate Root 境界が破られている（子エンティティに独立 Repository 3 つ）。Value Object が一切未定義。全ビジネスロジックが Service 層に漏洩（貧血ドメインモデル）。ReleaseCouponAsync の CouponUsage 未削除問題。Domain Event に型付きイベントではなく匿名型を使用。
</details>

<details>
<summary>api-endpoint-reviewer レビューレポート</summary>

**判定**: ⚠️ Warning | Critical: 1 / High: 5 / Medium: 6 / Low: 2 | スコア: 19/25

Critical: AcquireCoupon のパスパラメータ未検証。High: OpenAPI 未設定、`.Produces<T>()` 未設定、ActivateCampaign の空 `Ok()` レスポンス、ページネーションパラメータのサイレント補正、CalculateDiscount の IDOR 設計。Minimal API パターン（専用クラス分離、MapGroup、WithTags/WithName）は模範的。
</details>

<details>
<summary>csharp-standards-reviewer レビューレポート</summary>

**判定**: ⚠️ Warning | Critical: 0 / High: 1 / Medium: 4 / Low: 1 | スコア: 24/25

禁止パターン全 10 項目クリア。命名規則完全準拠。C# 14 機能（record, primary constructor, switch 式, コレクション式）の活用は優秀。High: Validator の DiscountType バグ。Medium: 陳腐化コメント、Redis の `!` 演算子使用、ファイル名/クラス名不一致。
</details>

<details>
<summary>async-concurrency-reviewer レビューレポート</summary>

**判定**: ✅ Pass | Critical: 0 / High: 1 / Medium: 2 / Low: 2 | スコア: 24/25

全インターフェース・実装で `CancellationToken ct = default` が完備。下位呼び出し伝搬も完全。`.Result`/`.Wait()`/`Thread.Sleep()`/`async void` 未検出。BackgroundService 4 つ全てが `IServiceScopeFactory` + `stoppingToken` + 例外ハンドリングを適切に実装。High: Redis メソッドの ct 未伝搬のみ。
</details>

<details>
<summary>error-logging-reviewer レビューレポート</summary>

**判定**: ⚠️ Warning | Critical: 0 / High: 2 / Medium: 4 / Low: 2 | スコア: 21/25

例外処理品質は高い（握りつぶしなし、catch 内ログ出力徹底）。構造化ログは全箇所メッセージテンプレート形式。PII ログ未検出。Correlation ID ミドルウェア実装済み。High: EF Core インスツルメンテーション欠落、サービス間 Correlation ID 伝搬未実装。
</details>

<details>
<summary>data-access-reviewer レビューレポート</summary>

**判定**: ⚠️ Warning | Critical: 0 / High: 4 / Medium: 6 / Low: 2 | スコア: 20/25

エンティティ設計は [Table]/[Column]/DateTimeOffset で概ね準拠。AsNoTracking は広く適用。High: 未使用メソッドの AsNoTracking 欠落、ReleaseCouponAsync の楽観的ロック未ハンドリング、CouponRestriction の updated_at 欠落。Medium: Campaign の RowVersion 未定義、明示的トランザクション未使用、MarkAsPublishedAsync の DateTimeOffset.UtcNow 直接使用。
</details>

<details>
<summary>config-di-reviewer レビューレポート</summary>

**判定**: ⚠️ Warning | Critical: 0 / High: 2 / Medium: 3 / Low: 1 | スコア: 22/25

DI 登録は全ペア完備、ライフタイム適切。ミドルウェア順序は規約完全準拠。appsettings.json に秘密情報なし。High: OpenAPI 未設定、CORS 未構成。Medium: CouponSettings の Data Annotations 不足、接続文字列の二重参照、Production 固有 DetailedErrors 未明示。
</details>

<details>
<summary>security-reviewer レビューレポート</summary>

**判定**: ❌ Fail | Critical: 2 / High: 5 / Medium: 4 / Low: 2

Critical: JWT ValidAlgorithms 未指定、レート制限未適用。High: RedeemCouponAsync の利用上限未再検証（TOCTOU）、DiscountAmount の未再計算、RedeemCoupon の userId 不正取得、InternalServiceHandler の脆弱認証、ReleaseCoupon の所有権未検証。セキュリティヘッダーは全て設定済みで良好。
</details>

<details>
<summary>dependency-reviewer レビューレポート</summary>

**判定**: ⚠️ Warning | Critical: 0 / High: 2 / Medium: 3 / Low: 2 | スコア: 22/25

禁止パッケージ・プレリリース版ともにゼロ検出。プロジェクト設定（TargetFramework, Nullable, TreatWarningsAsErrors）完全適合。High: Microsoft.Identity.Web / Microsoft.AspNetCore.OpenApi が欠落。Medium: Testcontainers.PostgreSql の未追加、TimeProvider.Testing のバージョン不整合、xUnit v3 の規約乖離。
</details>

<details>
<summary>test-quality-reviewer レビューレポート</summary>

**判定**: ⚠️ Warning | Critical: 0 / High: 9 / Medium: 7 / Low: 2 | スコア: 17/25

テスト命名 `Should_X_When_Y` パターン・AAA コメント明示は 5/5 で完璧。しかしテスト種別は Unit のみ（1/5）。3 Service クラスが完全未テスト。Endpoint/Repository/セキュリティテスト皆無。全体カバレッジ 80% 未達見込み（推定 30-40%）。FraudDetectionServiceTests のテスト名と Assert の矛盾あり。
</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

**判定**: ⚠️ Warning | Critical: 1 / High: 8 / Medium: 5 / Low: 2 | スコア: 15/25

Critical: GenerateBatchAsync のループ内 DB クエリ。High: 全リスト取得で Select プロジェクション未使用（6 箇所）、GetAvailableCoupons + Count の同一条件二重クエリ、CalculateDiscountAsync の二重 FindByCodeAsync、3 メソッドのページネーション欠落。キャッシュ戦略（Redis/InMemory 二重実装）と AsNoTracking 適用は良好。
</details>

<details>
<summary>resilience-reviewer レビューレポート</summary>

**判定**: ⚠️ Warning | Critical: 0 / High: 5 / Medium: 5 / Low: 2 | スコア: 17/25

外部 HTTP 呼び出しなし（`new HttpClient()` 未使用）は良好。Outbox パターンの Advisory Lock は模範的。High: Kafka Producer 設定不足、Kafka ヘルスチェック欠落、Redis RedisConnectionException のみの catch、Redis 動的フォールバックなし、Consumer バックオフ固定。Medium: OutboxPublisher の PROCESSING ステータスリカバリ、BackgroundService のエラー後バックオフ不在、ConsumeException バックオフなし、Dead Letter Topic 未実装。
</details>

---

## 肯定的評価（強み）

| 項目 | 詳細 |
|------|------|
| **レイヤー依存方向** | Endpoints→Services→Repositories の依存方向に違反ゼロ。逆依存もなし |
| **禁止パターン遵守** | Console.WriteLine, catch(Exception){}, .Result/.Wait(), Thread.Sleep(), ハードコード秘密情報 — 全 10 項目未検出 |
| **CancellationToken** | 全 async メソッドに `ct = default` 完備。Endpoints→Service→Repository 全層で伝搬 |
| **構造化ログ** | `ILogger<T>` + メッセージテンプレート形式を徹底。PII なし |
| **primary constructor** | 全 Service/Repository/BackgroundService で一貫して使用 |
| **record DTO** | 全リクエスト/レスポンス DTO が不変 record 型 |
| **Outbox パターン** | Advisory Lock によるリーダー選出 + 動的バックオフ + リトライ上限 + DeadLetter 遷移 |
| **ミドルウェア順序** | AGENTS.md §11.3 に完全準拠 |
| **セキュリティヘッダー** | X-Content-Type-Options, X-Frame-Options, CSP, HSTS, Referrer-Policy, Permissions-Policy 設定済み |
| **カスタムメトリクス** | 8 種のビジネスメトリクス（検証回数、適用回数、不正検知、ルールエンジンレイテンシ、キャッシュヒット/ミス、割引額分布、利用確定回数） |
| **べき等性** | `RedeemCouponAsync` は `FindByOrderIdAsync` で既存使用履歴チェック。二重確定を防止 |
| **テスト命名/AAA** | 全 38 テストが `Should_X_When_Y` パターン + `// Arrange/Act/Assert` コメント明示 |
