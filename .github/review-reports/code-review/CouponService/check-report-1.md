# ソースコードレビュー統合レポート

## 判定結果
- **対象**: CouponService（Services/CouponService/ 配下の全ソースコード）
- **判定**: ⚠️ **Conditional Approval** — High 指摘あり、人間の判断を介在
- **レビュー日時**: 2026-04-06
- **プロジェクト**: SkiShop (.NET 10 マイクロサービス EC サイト)

## 指摘サマリー

| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| tech-lead | ⚠️ | 0 | 2 | 1 | 0 |
| architecture-reviewer | ✅ | 0 | 0 | 2 | 1 |
| ddd-domain-reviewer | ⚠️ | 0 | 1 | 2 | 0 |
| api-endpoint-reviewer | ✅ | 0 | 0 | 2 | 1 |
| csharp-standards-reviewer | ✅ | 0 | 0 | 1 | 2 |
| async-concurrency-reviewer | ✅ | 0 | 0 | 1 | 0 |
| error-logging-reviewer | ✅ | 0 | 0 | 1 | 1 |
| data-access-reviewer | ⚠️ | 0 | 1 | 2 | 0 |
| config-di-reviewer | ✅ | 0 | 0 | 1 | 1 |
| security-reviewer | ⚠️ | 0 | 2 | 1 | 0 |
| dependency-reviewer | ✅ | 0 | 0 | 0 | 1 |
| test-quality-reviewer | ⚠️ | 0 | 1 | 2 | 0 |
| performance-reviewer | ✅ | 0 | 0 | 2 | 1 |
| resilience-reviewer | ✅ | 0 | 0 | 2 | 0 |
| **合計** | | **0** | **7** | **20** | **8** |

## 判定根拠
- Critical 指摘: 0 件
- High 指摘: 7 件（セキュリティ 2、DDD 1、データアクセス 1、テスト 1、Tech-Lead 2）
- 判定ルール適用: High 指摘のみ（Critical なし）→ ⚠️ Conditional Approval
- 最も重大な指摘: RedeemCoupon 内部 API で `ClaimsPrincipal` からユーザー ID を取得する設計の矛盾、CouponRestriction エンティティの `updated_at` カラム欠落

---

## Critical/High 指摘一覧（修正必須）

| # | 重要度 | 出典 Agent | カテゴリ | 対象ファイル | 行番号 | 指摘内容 | 修正方針 |
|---|--------|-----------|---------|------------|--------|----------|----------|
| 1 | **High** | security | 認証設計 | [InternalCouponEndpoints.cs](Services/CouponService/Endpoints/InternalCouponEndpoints.cs#L36-L44) | 36-44 | `RedeemCoupon`（内部 API）で `ClaimsPrincipal` からユーザー ID を取得しているが、内部サービス間通信ではユーザーコンテキストが存在しない場合がある。リクエスト DTO に `UserId` を含めるべき。`CalculateDiscountRequest` には `UserId` があるが `RedeemCouponRequest` にはない矛盾。 | `RedeemCouponRequest` に `UserId` フィールドを追加し、内部 API ではリクエストボディから取得する |
| 2 | **High** | security | 認可 | [InternalCouponEndpoints.cs](Services/CouponService/Endpoints/InternalCouponEndpoints.cs#L10-L14) | 10-14 | `InternalServiceOnly` ポリシーの認証チェックが `sub` クレームの値のみ（`internal-service`）。サービス間通信の認証トークンの発行・検証メカニズムが不明確。実運用ではサービス間の mTLS や専用 JWT の仕組みが必要。 | サービス間認証の仕組みを設計・実装。`InternalServiceHandler` にアクセス元 IP やサービス固有クレームの検証を追加検討 |
| 3 | **High** | ddd-domain | Aggregate 境界 | [CouponAppService.cs](Services/CouponService/Services/CouponAppService.cs#L176-L210) | 176-210 | `RedeemCouponAsync` で `Coupon`（Aggregate Root）の `CurrentUsageCount` 更新と `CouponUsage`（別エンティティ）の追加が同一トランザクションだが、Outbox イベント追加も同時実行。DB トランザクション境界が暗黙的（`SaveChangesAsync` 一回で全て永続化）であり、明示的なトランザクション境界がない。AGENTS.md §4.6 では `BeginTransactionAsync` の使用が推奨されている。 | 複数テーブル更新を伴う `RedeemCouponAsync` と `ReleaseCouponAsync` に明示的な `BeginTransactionAsync` / `CommitAsync` / `RollbackAsync` を追加 |
| 4 | **High** | data-access | エンティティ設計 | [CouponRestriction.cs](Services/CouponService/Models/CouponRestriction.cs) | 全体 | `CouponRestriction` に `updated_at` カラムが欠落。設計書 §5.2 では `coupon_restrictions` テーブルに `created_at`, `updated_at` が定義されている。また `CouponUsage` にも `created_at`、`updated_at` の `[Column]` 属性がない。 | `CouponRestriction` に `UpdatedAt` プロパティを追加。`CouponUsage` に `CreatedAt`, `UpdatedAt` プロパティを追加 |
| 5 | **High** | test-quality | テストカバレッジ | テストプロジェクト全体 | — | テストが Unit テストのみ。統合テスト（`WebApplicationFactory`）、Repository スライステスト（`Testcontainers`）が存在しない。Endpoint テスト、BackgroundService テスト（`OutboxPublisher`, `OrderEventConsumer`）も未作成。AGENTS.md §9.1 の要件を満たさない。Fixtures/ ディレクトリが空。 | 統合テスト・Repository スライステスト・Endpoint テストを計画的に追加 |
| 6 | **High** | tech-lead | 横断規約 | [CouponAppService.cs](Services/CouponService/Services/CouponAppService.cs#L226-L242) | 226-242 | `ReleaseCouponAsync` で `CouponUsage` の削除処理がない。`usage` を検索して存在確認のみ行い、`CurrentUsageCount` を減算しているが、usage レコード自体は DB に残存する。設計書の「クーポン利用取消」としてはべき等であるべきだが、usage レコードの取り扱い方針が不明確。 | usage レコードの削除 or ステータス変更（例: `status = "CANCELLED"`）を実装するか、設計書に残存方針を明記 |
| 7 | **High** | tech-lead | バリデーション | [CouponValidators.cs](Services/CouponService/Validators/CouponValidators.cs#L23-L28) | 23-28 | `CreateCouponRequestValidator` で `DiscountType == 0`（FixedAmount）の場合に `DiscountValue <= 100` の制約を設定しているが、これは Percentage タイプ（DiscountType == 1）に適用すべき制約。DiscountType の数値と enum の対応が逆転している。`DiscountType.FixedAmount = 0`, `DiscountType.Percentage = 1` なので、条件は `When(x => x.DiscountType == 1, ...)` でなければならない。 | バリデーション条件を `DiscountType == 1`（Percentage）に修正 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| 1 | 高 | security + config-di | サービス間通信の認証メカニズム設計が未確定。`InternalServiceOnly` ポリシーの実装が暫定的。mTLS/専用JWT等のインフラ設計を要決定。 | テックリード + インフラ担当 |
| 2 | 通常 | ddd-domain | `CouponRestriction.RestrictionType` が `string` 型で定義。enum 化するか DB の CHECK 制約に依存するか方針を要決定。 | テックリード |
| 3 | 通常 | architecture | gRPC サービス（`CouponGrpcServiceImpl`）は proto 未生成のスタブ実装。Saga ステップ 3 の本格実装時期を要決定。 | プロジェクトマネージャ |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| — | — | — | 本レビューでは重大な競合は検出されなかった | — | — |

---

## 設計書との照合結果

### 設計書からの逸脱

| # | 設計書セクション | 逸脱内容 | 影響度 |
|---|----------------|---------|-------|
| 1 | §5.2 coupon_restrictions | `updated_at` カラムが実装のエンティティに欠落 | Medium |
| 2 | §5.2 coupon_usages | `created_at`, `updated_at` カラムが `[Column]` 属性なし | Medium |
| 3 | §6.4 RedeemCouponRequest | 設計書では `UserId` フィールドが含まれないが、内部 API での認証設計との整合性に問題 | High |
| 4 | §5.1 Coupon.target_category / target_product_id | 設計書にこれらのフィールドが定義されているが、実装では Restrictions テーブルに移行済み。設計書の更新が必要 | Low |
| 5 | §6.1 API パス | 設計書では `/api/v1/coupons/available` 等だが、実装も一致。乖離なし | — |
| 6 | §10 gRPC | proto ファイル未生成。gRPC サービスはスタブ実装のみ | Medium |

### 未実装の設計要素

| # | 設計書セクション | 未実装の機能 |
|---|----------------|------------|
| 1 | §10 gRPC サービス | proto ファイルからの自動生成ベースクラス（スタブ実装のみ存在） |
| 2 | §8.1 不正検知ルール #2 | 同一 IP からの大量取得検知（10 分以内に同一 IP から 10 件以上） |
| 3 | §8.1 不正検知ルール #3 | 異常な割引パターン検知（全注文でクーポン利用：10 件連続） |
| 4 | §5.1 Promotion テーブル活用 | `Promotion` エンティティは定義されているが、Service/Endpoint が未実装 |

---

## 各 Agent 詳細レポート

<details>
<summary>tech-lead レビューレポート</summary>

### 技術標準の横断適合性

**判定: ⚠️ Conditional（High 2 件）**

#### 良い点
- primary constructor の一貫した使用（C# 12+）
- `TimeProvider` DI によるテスタブルな時刻管理
- Outbox パターンによるイベント発行保証
- 構造化ログ（`ILogger<T>` + メッセージテンプレート）の徹底
- FluentValidation による入力バリデーション
- レート制限の適用

#### 指摘事項

| 重要度 | 指摘内容 |
|--------|---------|
| **High** | `ReleaseCouponAsync` で usage レコードの削除/無効化が行われていない |
| **High** | `CreateCouponRequestValidator` の Percentage 判定条件が DiscountType enum 値と不一致（0 と 1 が逆） |
| Medium | `CouponRuleEngine.EvaluateRulesAsync` メソッドの末尾で `await Task.FromResult(...)` を使用 — 不要な `await`、直接 `Task.FromResult` を返すか、メソッドを非同期にしない |

</details>

<details>
<summary>architecture-reviewer レビューレポート</summary>

### レイヤー依存方向・プロジェクト構成

**判定: ✅ Approved with Notes**

#### 良い点
- Endpoints → Services → Repositories の依存方向を厳守
- AGENTS.md §2.2 のプロジェクト構成に準拠
- Endpoints が Repository を直接参照していない
- BackgroundService で `IServiceScopeFactory` を正しく使用
- Infrastructure 層が適切に分離（Persistence, Middleware, Authorization, Observability）

#### 指摘事項

| 重要度 | 指摘内容 |
|--------|---------|
| Medium | `Repositories.cs` に全 Repository 実装が 1 ファイルに集約。ファイルが 250行超で、個別ファイルに分割推奨 |
| Medium | `CouponGrpcServiceImpl` が `GrpcServices/` に配置されているが、proto 未生成のスタブ。`Program.cs` に `MapGrpcService` がなく、無効コード |
| Low | `Exceptions/CouponExceptions.cs` に全例外クラスが集約。個別ファイル分割の検討余地あり |

</details>

<details>
<summary>ddd-domain-reviewer レビューレポート</summary>

### Aggregate Root・Value Object・Domain Event

**判定: ⚠️ Conditional（High 1 件）**

#### 良い点
- `Coupon` が Aggregate Root として明確に機能（子エンティティ `CouponRestriction`, `UserCoupon`, `CouponUsage` をナビゲーション経由で管理）
- `Campaign` が独立した Aggregate Root として定義
- Outbox パターンによる Domain Event の疎結合化
- enum 型（`DiscountType`, `CampaignStatus` 等）が Value Object 的に定義

#### 指摘事項

| 重要度 | 指摘内容 |
|--------|---------|
| **High** | `RedeemCouponAsync` / `ReleaseCouponAsync` で複数テーブル更新が暗黙的トランザクション。AGENTS.md §4.6 では明示的トランザクション管理が推奨 |
| Medium | `CouponRestriction.RestrictionType` が `string` 型。DDD の Value Object / enum 化が望ましい（`"PRODUCT"`, `"CATEGORY"`, `"USER"` の 3 値のみ） |
| Medium | `Coupon` エンティティに `TargetCategory` / `TargetProductId` フィールドがなく、設計書 §5.1 との差異。Restrictions テーブルに移行した意図は理解できるが設計書の更新が未済 |

</details>

<details>
<summary>api-endpoint-reviewer レビューレポート</summary>

### Minimal API パターン・REST 規約・バリデーション

**判定: ✅ Approved with Notes**

#### 良い点
- `MapGroup` + `WithTags` + `WithName` による明確なグループ化
- `RequireAuthorization` による認可設定（AdminOnly, UserOrAdmin, InternalServiceOnly）
- FluentValidation によるバリデーション（全 POST/PUT エンドポイントで実施）
- `Results.Created` / `Results.Ok` / `Results.NotFound` / `Results.NoContent` の適切な使い分け
- `CancellationToken ct` が全ハンドラーで受け取り・伝搬
- ページネーションの上限制御（`Math.Min(pageSize, 100)`）

#### 指摘事項

| 重要度 | 指摘内容 |
|--------|---------|
| Medium | `AddOpenApi()` / `MapOpenApi()` が `Program.cs` に設定されていない。AGENTS.md §6.0 では .NET 10 方式の OpenAPI ドキュメント生成が推奨 |
| Medium | `CampaignEndpoints.ActivateCampaign` / `PauseCampaign` が `Results.Ok()` を返すが、状態変更のみのため `Results.NoContent()` が REST 的に適切 |
| Low | `GetAvailableCoupons` のデフォルト値（page=1, pageSize=20）がハンドラー内の条件分岐で処理。`[AsParameters]` + DTO のデフォルト値で統一する方が整合的 |

</details>

<details>
<summary>csharp-standards-reviewer レビューレポート</summary>

### 命名規則・C#14 機能活用・禁止パターン

**判定: ✅ Approved with Notes**

#### 良い点
- PascalCase / camelCase / `_camelCase` の命名規則を遵守
- `record` 型の DTO 使用
- primary constructor の一貫した使用
- `Console.WriteLine` なし、`ILogger<T>` を徹底
- パターンマッチング（`is { }`, `is not null`）の適切な使用
- コレクションの `= []` 初期化

#### 指摘事項

| 重要度 | 指摘内容 |
|--------|---------|
| Medium | `CouponRuleEngine.EvaluateRulesAsync` 末尾: `return await Task.FromResult(...)` — 非同期不要なメソッドで無駄な `await Task.FromResult`。`Task.FromResult` を直接返すか同期メソッドに変更 |
| Low | `CouponCacheServiceRedis` クラス名が `CouponCacheService` として DI 登録されているが、ファイル名は `CouponCacheServiceRedis.cs`。クラス名とファイル名の不一致 |
| Low | `ValidationResult` が `CouponService.DTOs.Responses` 名前空間に定義されているが、`FluentValidation.Results.ValidationResult` と名前衝突の可能性。`CouponValidationResult` 等への改名を検討 |

</details>

<details>
<summary>async-concurrency-reviewer レビューレポート</summary>

### CancellationToken 伝搬・async/await パターン

**判定: ✅ Approved with Notes**

#### 良い点
- 全 `async` メソッドに `CancellationToken ct = default` が含まれている
- Endpoint → Service → Repository への `ct` 伝搬が完全
- BackgroundService で `stoppingToken` が全下位呼び出しに伝搬
- `.Result` / `.Wait()` の使用なし
- `Thread.Sleep()` の使用なし（`Task.Delay` を使用）

#### 指摘事項

| 重要度 | 指摘内容 |
|--------|---------|
| Medium | `CouponCacheServiceRedis` の全メソッドで `CancellationToken ct` を受け取っているが、Redis の `StringGetAsync` / `StringSetAsync` / `KeyDeleteAsync` に `ct` を渡していない。StackExchange.Redis は `CancellationToken` ネイティブ非対応だが、将来の互換性のためコメントで明記推奨 |

</details>

<details>
<summary>error-logging-reviewer レビューレポート</summary>

### 例外処理階層・構造化ログ・Correlation ID

**判定: ✅ Approved with Notes**

#### 良い点
- カスタム例外階層（`CouponNotFoundException`, `BusinessException`, `ConcurrencyException` 等）が適切に定義
- グローバル例外ハンドラーで RFC 9457 Problem Details を返却
- 例外の重要度に応じた `LogError` / `LogWarning` の使い分け
- Correlation ID ミドルウェアが `Serilog.Context.PushProperty` で適切にログコンテキストに追加
- メッセージテンプレート形式のログ出力（文字列補間禁止を遵守）

#### 指摘事項

| 重要度 | 指摘内容 |
|--------|---------|
| Medium | `OutboxPublisher.ProcessOutboxEventsAsync` で `ProduceException` をキャッチしてログ出力しているが、catch ブロック内のログに `ex`（Exception オブジェクト）を渡しているのは良い。ただし、`AppDbContext.SaveChangesAsync` の例外は上位の `catch (Exception ex) when (ex is not OperationCanceledException)` でまとめてキャッチされ、リトライ戦略なしで `MaxPollingInterval` に退避するだけ。DB 一時障害時の具体的なリカバリ戦略がない |
| Low | `HandleUserDeleted` で「仮名化処理を実行」とログ出力しているが、実際の仮名化ロジックが未実装 |

</details>

<details>
<summary>data-access-reviewer レビューレポート</summary>

### EF Core エンティティ・クエリ品質・マイグレーション

**判定: ⚠️ Conditional（High 1 件）**

#### 良い点
- 全エンティティに `[Table]`, `[Column]` 属性で snake_case 明示
- `AsNoTracking()` が読み取り専用クエリで適切に使用
- `DateTimeOffset` 使用（`DateTime.Now` / `DateTime.UtcNow` の直接使用を回避）
- 楽観的ロック（`[Timestamp]` + `RowVersion`）を `Coupon` に実装
- `DbUpdateConcurrencyException` のハンドリングが適切
- `AppDbContext.SaveChangesAsync` オーバーライドで `UpdatedAt` 自動更新
- 複合インデックス定義（`CouponUsage`, `UserCoupon`）

#### 指摘事項

| 重要度 | 指摘内容 |
|--------|---------|
| **High** | `CouponRestriction` に `updated_at` プロパティが欠落。`CouponUsage` にも `created_at`/`updated_at` の明示的な `[Column]` 属性がない。設計書の DDL 定義との不整合 |
| Medium | `UserCoupon` に `created_at`/`updated_at` の `[Column]` 属性がない。`AppDbContext.SaveChangesAsync` のリフレクションベース更新に依存するが、`[Column]` 名が明示されていないため DB カラム名が PascalCase になる可能性 |
| Medium | `OutboxEvent` に `updated_at` プロパティがない。ステータス遷移（Pending→Processing→Published/Failed）の時刻追跡ができない |

</details>

<details>
<summary>config-di-reviewer レビューレポート</summary>

### DI 登録・ミドルウェア順序・appsettings 品質

**判定: ✅ Approved with Notes**

#### 良い点
- DI 登録が全て `Scoped`（Repository, Service）で適切
- ミドルウェアパイプラインの順序が AGENTS.md §11.3 に準拠（ExceptionHandler → HSTS → Correlation → Serilog → Auth → RateLimiter → Endpoints）
- `IOptions<T>` パターン（`CouponSettings`, `KafkaSettings`）による設定管理
- `ValidateDataAnnotations().ValidateOnStart()` による設定検証
- `appsettings.json` に秘密情報なし（接続文字列は空文字）
- `DetailedErrors: false` がデフォルト設定に含まれる

#### 指摘事項

| 重要度 | 指摘内容 |
|--------|---------|
| Medium | `AddOpenApi()` / `MapOpenApi()` が未設定。AGENTS.md §6.0 準拠で追加が必要 |
| Low | `KafkaSettings.BootstrapServers` に `[Required]` 属性が設定されているが、`appsettings.json` では空文字。`ValidateOnStart` で起動時エラーになる可能性。開発環境での挙動確認が必要 |

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### OWASP Top 10・認証/認可・秘密情報管理

**判定: ⚠️ Conditional（High 2 件）**

#### 良い点
- JWT Bearer 認証が適切に設定（`ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ClockSkew`）
- FallbackPolicy で全エンドポイントに認証必須（`AllowAnonymous` で明示的除外）
- セキュリティヘッダー（`X-Content-Type-Options`, `X-Frame-Options`, `CSP`, `Referrer-Policy`, `Permissions-Policy`）が完備
- HSTS + HTTPS リダイレクション
- レート制限（`coupon-api`, `redeem-api`）
- 秘密情報のハードコードなし
- 入力バリデーション（Data Annotations + FluentValidation）の二重防御
- `FromSqlRaw` の使用なし（EF Core LINQ のみ）
- SQL インジェクション対策は完全
- `Kestrel.AddServerHeader = false` でサーバーヘッダー非公開

#### 指摘事項

| 重要度 | 指摘内容 |
|--------|---------|
| **High** | `InternalCouponEndpoints.RedeemCoupon` が `ClaimsPrincipal` からユーザー ID を取得。内部 API（サービス間通信）でユーザーコンテキストが伝搬されない場合に `UnauthorizedException` が発生する設計矛盾 |
| **High** | `InternalServiceHandler` の認証チェックが `sub == "internal-service"` のみ。攻撃者がこのクレームを含むトークンを生成できれば全内部 API にアクセス可能。サービス間認証の強化が必要 |
| Medium | `CouponCodeGenerator.GenerateRandomCode` が `Random.Shared` を使用。クーポンコードはセキュリティトークンではないため暗号学的安全性は不要だが、予測可能性を排除するために `RandomNumberGenerator` の検討余地あり |

</details>

<details>
<summary>dependency-reviewer レビューレポート</summary>

### NuGet パッケージ品質・禁止パッケージ・ライセンス

**判定: ✅ Approved**

#### 良い点
- 全パッケージが GA 版（`-preview`, `-beta`, `-rc` なし）
- 禁止パッケージ（`System.Web`, `log4net`, `Newtonsoft.Json`, `WebClient` 等）が含まれていない
- `TreatWarningsAsErrors = true` が設定済み
- AGENTS.md §8.1 の必須パッケージリストに概ね準拠
- テストプロジェクトで `xunit`, `NSubstitute`, `Shouldly`, `coverlet.collector` を使用

#### 指摘事項

| 重要度 | 指摘内容 |
|--------|---------|
| Low | テストプロジェクトに `Microsoft.AspNetCore.Mvc.Testing`（統合テスト用）と `Testcontainers.PostgreSql`（DB スライステスト用）が未加入。AGENTS.md §8.1 では推奨パッケージ |

</details>

<details>
<summary>test-quality-reviewer レビューレポート</summary>

### テスト命名・AAA パターン・カバレッジ基準

**判定: ⚠️ Conditional（High 1 件）**

#### 良い点
- `Should_期待結果_When_条件` 命名パターンを遵守
- AAA パターン（Arrange-Act-Assert）が明確
- `[Trait("Category", "Unit")]` でカテゴリ分類
- `FakeTimeProvider` による時刻制御
- NSubstitute + Shouldly の適切な使用
- 正常系・異常系の両方をテスト（`CouponNotFoundException`, `CouponAlreadyAcquiredException` 等）
- べき等性テスト（`Should_ReturnExistingUsage_When_RedeemIdempotent`）が存在

#### 指摘事項

| 重要度 | 指摘内容 |
|--------|---------|
| **High** | テストが Unit テスト 4 ファイルのみ。統合テスト（`WebApplicationFactory`）、Repository テスト（`Testcontainers`）、Endpoint テスト、BackgroundService テスト、Security テストが全て欠如。AGENTS.md §9.4 の「分岐カバレッジ 80%」を達成できない可能性が高い |
| Medium | `CouponAnalyticsService`, `CouponCodeGenerator`, `CouponCacheServiceRedis`, `CouponCacheServiceInMemory` のテストが存在しない |
| Medium | `OutboxPublisher`, `CouponExpirationService`, `CampaignStatusService`, `OrderEventConsumer` の BackgroundService テストが存在しない |

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### N+1 クエリ・メモリ効率・キャッシュ戦略

**判定: ✅ Approved with Notes**

#### 良い点
- `AsNoTracking()` が読み取り専用クエリで一貫して使用
- `Include(c => c.Restrictions)` による明示的 Eager Loading（N+1 回避）
- Redis キャッシュ + InMemory キャッシュのフォールバック設計
- `ExecuteUpdateAsync` によるバルク更新（期限切れクーポン/キャンペーン無効化）
- `CouponMetrics` によるルールエンジン実行時間・キャッシュヒット率の計測
- ページネーション（`Skip` + `Take`）の適用
- `stackalloc` の使用（`CouponCodeGenerator.GenerateRandomCode`）

#### 指摘事項

| 重要度 | 指摘内容 |
|--------|---------|
| Medium | `GetAllCouponsAsync` で `GetAvailableCouponsAsync` を呼んでいるため、管理者向け「全クーポン一覧」でも有効期間フィルタが適用されてしまう。管理者は無効・期限切れも含めて表示したい場合がある |
| Medium | `CouponRuleEngine.ValidateAsync` で `couponRepository.FindByCodeAsync` がキャッシュミス時に呼ばれるが、`Include(c => c.Restrictions)` 付き。キャッシュした `Coupon` オブジェクトの `Restrictions` はシリアライズ/デシリアライズされるが、Redis から復元した `Coupon` のナビゲーションプロパティが不完全になる可能性 |
| Low | `CouponCodeGenerator.GenerateBatchAsync` で N 件のコード生成が直列実行。大量生成時にはパフォーマンスボトルネックになりうる |

</details>

<details>
<summary>resilience-reviewer レビューレポート</summary>

### Polly リトライ・サーキットブレーカー・ヘルスチェック

**判定: ✅ Approved with Notes**

#### 良い点
- ヘルスチェック `/health`（Liveness）と `/health/ready`（Readiness）が実装済み
- PostgreSQL + Redis のヘルスチェック（Redis は条件付き）
- Redis 接続エラー時のフォールバック（`CouponCacheServiceRedis` で `RedisConnectionException` をキャッチし `null` 返却 → DB フォールバック）
- `OutboxPublisher` が動的バックオフ（100ms〜5s）を実装
- `OutboxPublisher` が PostgreSQL Advisory Lock で二重発行防止
- Kafka Consumer にバックオフ（`Task.Delay(5s)`）を実装
- Docker HEALTHCHECK 設定済み

#### 指摘事項

| 重要度 | 指摘内容 |
|--------|---------|
| Medium | `Polly` / `Microsoft.Extensions.Http.Resilience` パッケージが `.csproj` に含まれているが、`Program.cs` で `IHttpClientFactory` + `AddStandardResilienceHandler` が未登録。他サービスへの HTTP 通信が発生した場合のリトライ/サーキットブレーカーが未設定。現時点では外部 HTTP 通信がないため問題ないが、将来的な追加時に要対応 |
| Medium | `OutboxPublisher` の `ProduceException` 発生時のリトライが Outbox パターン自体のリトライ（`RetryCount` インクリメント）のみで、Kafka Producer 自体にはリトライポリシーが未設定 |

</details>

---

## 総合評価

### 強み
1. **レイヤードアーキテクチャの遵守**: Endpoints → Services → Repositories の依存方向が完全に守られている
2. **Outbox パターンの適切な実装**: Advisory Lock による二重発行防止、動的バックオフ、リトライ管理が実装済み
3. **入力バリデーションの充実**: Data Annotations + FluentValidation の二重防御
4. **可観測性**: OpenTelemetry + Serilog + カスタムメトリクス + Correlation ID + ヘルスチェックが統合済み
5. **キャッシュ戦略**: Redis / InMemory のフォールバック設計
6. **セキュリティヘッダーの完備**: AGENTS.md §5.3 の要件を全て充足
7. **テスタブルな設計**: `TimeProvider` DI、インターフェース分離、primary constructor

### 改善が必要な領域
1. **テストカバレッジの大幅な拡充**（High）
2. **内部 API の認証設計見直し**（High）
3. **バリデーションロジックのバグ修正**（High: DiscountType 条件逆転）
4. **エンティティ設計の設計書との整合性確保**（High）
5. **トランザクション境界の明示化**（High）
6. **OpenAPI ドキュメント生成の追加**（Medium）
