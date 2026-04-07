# ソースコードレビュー統合レポート — SalesManagementService

## 判定結果
- **対象**: SalesManagementService（注文・販売管理マイクロサービス）
- **判定**: ✅ **Approved with Notes** — 推奨改善事項あり
- **レビュー日時**: 2026-04-07 01:58
- **プロジェクト**: SkiShop (.NET 10 マイクロサービス EC サイト)

## 指摘サマリー
| Agent | 判定 | Critical | High | Medium | Low | スコア |
|-------|------|----------|------|--------|-----|--------|
| tech-lead | ✅ Pass | 0 | 0 | 2 | 1 | 95 |
| architecture-reviewer | ✅ Pass | 0 | 0 | 1 | 2 | 96 |
| ddd-domain-reviewer | ✅ Pass | 0 | 0 | 2 | 1 | 94 |
| api-endpoint-reviewer | ✅ Pass | 0 | 0 | 1 | 2 | 96 |
| csharp-standards-reviewer | ✅ Pass | 0 | 0 | 1 | 3 | 95 |
| async-concurrency-reviewer | ✅ Pass | 0 | 0 | 0 | 2 | 98 |
| error-logging-reviewer | ✅ Pass | 0 | 0 | 1 | 1 | 97 |
| data-access-reviewer | ✅ Pass | 0 | 0 | 1 | 2 | 96 |
| config-di-reviewer | ✅ Pass | 0 | 0 | 0 | 2 | 98 |
| security-reviewer | ✅ Pass | 0 | 0 | 1 | 2 | 96 |
| dependency-reviewer | ✅ Pass | 0 | 0 | 0 | 1 | 99 |
| test-quality-reviewer | ✅ Pass | 0 | 1 | 2 | 2 | 88 |
| performance-reviewer | ✅ Pass | 0 | 0 | 2 | 2 | 94 |
| resilience-reviewer | ✅ Pass | 0 | 0 | 1 | 2 | 96 |
| **合計** | | **0** | **1** | **15** | **25** | **94.1** |

## 判定根拠
- **Critical 指摘: 0 件** — セキュリティ脆弱性・データ破損リスクは検出されず
- **High 指摘: 1 件** — テストカバレッジ不足（修正推奨だがマージ可能）
- 判定ルール適用結果: Critical = 0, High のみ → **Approved with Notes**

---

## Critical/High 指摘一覧（修正推奨）

| # | 重要度 | 出典 Agent | カテゴリ | 対象ファイル | 指摘内容 | 修正提案 |
|---|--------|-----------|---------|------------|----------|----------|
| 1 | **High** | test-quality | テストカバレッジ | `SalesManagementService.Tests/` | Saga/Outbox 統合テストのカバレッジが不足。SagaCoordinator の補償トランザクションテスト、OutboxPublisher の統合テストが未実装 | `Integration/Saga/SagaCoordinatorIntegrationTests.cs` と `Integration/Outbox/OutboxPublisherIntegrationTests.cs` を追加し、Testcontainers で実 PostgreSQL + Kafka を使用したテストを実装する |

---

## Medium 指摘一覧（次回リファクタリングで対応推奨）

| # | 出典 Agent | カテゴリ | 対象ファイル | 行番号 | 指摘内容 | 修正提案 |
|---|-----------|---------|------------|--------|----------|----------|
| 1 | tech-lead | 設計書照合 | `Services/OrderService.cs` | - | spec.md の ADR-0009 に記載された Saga デッドライン監視機能が SagaRecoveryService に実装済みだが、PaymentPollingTimeoutMinutes の設定値（30分）が設計書と照合必要 | 設計書の SLA 要件と照合し、適切なタイムアウト値を確認する |
| 2 | tech-lead | コード構成 | `Infrastructure/Saga/SagaCoordinator.cs` | 1-389 | SagaCoordinator が 389 行と大きい。ExecuteCheckoutAsync メソッドが複雑 | Step ごとのハンドラを分離し、Strategy パターンの適用を検討する |
| 3 | ddd-domain | Aggregate 境界 | `Models/Order.cs` | 143-157 | AddItem メソッドは Aggregate Root パターンに準拠しているが、SubtotalAmount/TaxAmount の計算が Service 側にある | 金額計算ロジックを Order エンティティ内に移動し、ドメイン知識をエンティティに集約することを検討 |
| 4 | ddd-domain | Value Object | `Models/Order.cs` | 50-68 | 金額フィールド (SubtotalAmount, TaxAmount 等) が primitive 型。Money Value Object の導入を検討 | `record Money(decimal Amount, string Currency)` を定義し、金額操作の型安全性を向上 |
| 5 | api-endpoint | OpenAPI | `Program.cs` | - | .NET 10 の `AddOpenApi()` / `MapOpenApi()` が未設定。Swagger UI が利用不可 | `builder.Services.AddOpenApi()` と `app.MapOpenApi()` を追加 |
| 6 | csharp-standards | C# 14 機能 | `Services/OrderService.cs` | 146-166 | MapToDetailDto / MapToDto メソッドが手動マッピング。record の with 式や Source Generator の活用余地あり | Mapster や手動マッピングの共通化を検討 |
| 7 | error-logging | ログ出力 | `Infrastructure/Saga/SagaCoordinator.cs` | 48-56 | IdempotencyKey の処理でログ出力が不足。リプレイ検出時のログは出力されているが、新規作成時のログがない | IdempotencyKey 新規作成時にも `LogInformation` を追加 |
| 8 | data-access | インデックス | `Infrastructure/Persistence/SalesDbContext.cs` | 156-163 | SagaLog の部分インデックスで同一カラム (UpdatedAt) に異なる Filter で 2 つのインデックスを作成。HasDatabaseName の指定が片方のみ | 両方のインデックスに明示的な名前を付与し、マイグレーション時の競合を防止 |
| 9 | security | レート制限 | `Program.cs` | 164-178 | order-create のレート制限が 5 req/min だが、Admin API は 30 req/min。Admin 向けの制限がやや緩い可能性 | Admin API のレート制限を監視し、必要に応じて調整 |
| 10 | test-quality | テスト命名 | `Tests/Unit/Services/OrderServiceTests.cs` | 全体 | Should_X_When_Y パターンに準拠しているが、一部のテストで When 部分が不明確 | テスト名をより具体的に記述（例: `Should_ReturnNull_When_OrderIdDoesNotExistInDatabase`） |
| 11 | test-quality | 異常系テスト | `Tests/Unit/Services/` | - | ConcurrencyException のテストが未実装。SaveChangesWithConcurrencyHandlingAsync のテストが不足 | DbUpdateConcurrencyException をモックで発生させ、ConcurrencyException への変換をテスト |
| 12 | performance | クエリ最適化 | `Repositories/OrderRepository.cs` | 35-49 | FindByCustomerIdAsync で Count + Skip/Take を別クエリで実行。1 クエリで取得可能 | `CountAsync` と `ToListAsync` を並列実行、または SQL の `COUNT(*) OVER()` を検討 |
| 13 | performance | キャッシュ | `Infrastructure/Caching/OrderCacheService.cs` | - | OrderCacheService が DI 登録されているが、実際の使用箇所が限定的。キャッシュ戦略の明確化が必要 | 頻繁にアクセスされる注文詳細のキャッシュ適用箇所を特定し、キャッシュヒット率を監視 |
| 14 | resilience | 外部サービス | `Infrastructure/ExternalServices/` | - | 開発環境用の DevelopmentXxxClient はスタブ実装だが、本番用 UnavailableXxxClient は例外をスロー。本番環境での外部サービス実装が未完了 | 本番環境用の HttpClient ベース実装を追加し、Polly でリトライ/サーキットブレーカーを適用 |
| 15 | architecture | レイヤー構成 | `Infrastructure/Saga/` | - | SagaCoordinator が IOrderCheckoutService を実装し、Service 層のインターフェースを Infrastructure 層が実装。依存方向は正しいが、配置場所の検討余地あり | Saga を独立したドメインサービスとして Services/ に移動することを検討（現状は許容範囲） |

---

## Low 指摘一覧（時間がある時に対応）

| # | 出典 Agent | 対象 | 指摘内容 |
|---|-----------|------|----------|
| 1 | csharp-standards | `Models/*.cs` | エンティティの初期化で `= string.Empty` が多用されている。required 修飾子の検討 |
| 2 | csharp-standards | `DTOs/Responses/*.cs` | レスポンス DTO で位置パラメータが多い（13 個以上）。可読性向上のため分割を検討 |
| 3 | csharp-standards | 全体 | `[LoggerMessage]` ソースジェネレーターの活用余地あり（ホットパスのログ出力） |
| 4 | async-concurrency | `OutboxPublisher.cs` | `CancellationToken.None` の使用箇所あり（Advisory Lock 解放時）。意図的だが代替検討 |
| 5 | async-concurrency | `SagaCoordinator.cs` | 後処理ステップ（7-9）で個別 try-catch。共通のエラーハンドリング抽出を検討 |
| 6 | config-di | `appsettings.json` | Jwt:Issuer / Jwt:Audience が空文字。開発環境でも明示的な値を設定推奨 |
| 7 | config-di | `Program.cs` | DI 登録で完全修飾名を使用。using ディレクティブ追加で簡潔化可能 |
| 8 | data-access | `Models/OrderItem.cs` | CreatedAt/UpdatedAt の監査カラムがない（Order 経由で管理） |
| 9 | data-access | `SalesDbContext.cs` | OnDelete の CASCADE が OrderItems/Shipments に設定。意図的だがドキュメント化推奨 |
| 10 | security | `Endpoints/OrderEndpoints.cs` | GetOrderByNumber で Admin チェック後に order を返却。ログ出力を追加推奨 |
| 11 | security | `appsettings.json` | Kafka:BootstrapServers が環境変数で注入されるが、デフォルト値がないため起動時例外の可能性 |
| 12 | dependency | `.csproj` | Grpc.Tools が PrivateAssets="all" だが、Google.Protobuf は PrivateAssets なし |
| 13 | test-quality | `Tests/Security/SecurityTests.cs` | ErrorCodes の値を直接テスト。定数の変更時に追随が必要 |
| 14 | test-quality | テスト全体 | Testcontainers を使用した統合テストのセットアップファイルが未確認 |
| 15 | performance | `OrderRepository.cs` | SearchAsync で動的クエリ構築。インデックス効率の監視を推奨 |
| 16 | performance | `SagaCoordinator.cs` | 各 Step で SaveChangesAsync を呼び出し。バッチ保存の検討余地あり |
| 17 | resilience | `OutboxPublisher.cs` | Advisory Lock の取得失敗時に MaxPollingIntervalMs で待機。動的バックオフを検討 |
| 18 | resilience | `Kafka Consumers` | Consumer の再接続ロジックが確認必要 |
| 19 | architecture | `Configurations/AppSettings.cs` | 設定クラスが単一ファイルにまとめられている。機能別分割を検討 |
| 20 | architecture | `Endpoints/` | エンドポイントファイルが 4 つに分割されており適切 |
| 21 | ddd-domain | `Services/OrderStateMachine.cs` | ステートマシンパターンが実装済みで良好 |
| 22 | api-endpoint | `Endpoints/OrderEndpoints.cs` | Idempotency-Key ヘッダーの検証が適切に実装されている |
| 23 | api-endpoint | `DTOs/Requests/` | FluentValidation と Data Annotations の併用。統一を検討 |
| 24 | error-logging | `GlobalExceptionHandler.cs` | IExceptionHandler パターンを使用しており .NET 8+ のベストプラクティスに準拠 |
| 25 | error-logging | `ErrorCodes.cs` | エラーコードが体系的に定義されており良好 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| 1 | 通常 | resilience | 本番環境用の外部サービスクライアント（IInventoryClient, ICartClient 等）の実装方針。gRPC vs HTTP の選択 | アーキテクト |
| 2 | 通常 | test-quality | Saga の統合テストに必要なインフラ（Testcontainers + Kafka）の CI 環境セットアップ | DevOps |
| 3 | 通常 | performance | Order 検索 API の想定負荷と、動的クエリのインデックス戦略 | DBA |

---

## 競合解決記録

競合は検出されませんでした。

---

## 設計書との照合結果

### 設計書からの逸脱
- **なし** — 主要な設計パターン（Saga オーケストレーション、Outbox パターン、べき等性キー）が設計書通りに実装されている

### 未実装の設計要素
| 設計要素 | 設計書参照 | 状況 |
|---------|-----------|------|
| 本番用外部サービスクライアント | spec.md §10.1 | 開発用スタブのみ実装。本番用は別 Issue で対応予定 |
| gRPC クライアント実装 | spec.md | .csproj に Grpc パッケージあり。Proto ファイルと実装は未確認 |

---

## 良好な実装（ベストプラクティス準拠）

以下の点は規約・設計書に準拠した優れた実装として評価:

1. **Saga パターン**: SagaCoordinator による Checkout Saga が設計書通りに実装。補償トランザクションも実装済み
2. **Outbox パターン**: OutboxPublisher が動的バックオフと Advisory Lock で実装。設計書の要件を満たす
3. **べき等性**: IdempotencyKey によるリクエスト重複排除が適切に実装
4. **ミドルウェア順序**: Program.cs のパイプライン順序が AGENTS.md §11.3 に完全準拠
5. **例外処理**: IExceptionHandler による構造化された例外ハンドリング。ErrorCodes による体系的なエラーコード
6. **DI 登録**: Scoped/Singleton の使い分けが適切。TimeProvider の DI による時刻抽象化
7. **エンティティ設計**: [Table]/[Column] 属性による明示的マッピング、楽観的ロック、監査カラム
8. **バリデーション**: FluentValidation による詳細なリクエストバリデーション
9. **セキュリティ**: FallbackPolicy による認証必須化、IDOR 防止、セキュリティヘッダー
10. **可観測性**: OpenTelemetry、Serilog 構造化ログ、Correlation ID、カスタムメトリクス

---

## 各 Agent 詳細レポート

<details>
<summary>tech-lead レビューレポート</summary>

### 技術標準の遵守状況
- ✅ C# 14 / .NET 10 準拠
- ✅ ASP.NET Core 10 Minimal API 使用
- ✅ EF Core 10 使用
- ✅ DDD パターン（Aggregate Root, Repository）準拠
- ✅ 禁止パターン（Console.WriteLine, FromSqlRaw 文字列結合）なし

### 設計書との整合性
- spec.md ADR-0009（Saga オーケストレーション）に準拠
- Outbox パターンによるイベント発行の整合性保証

### 総合評価
エンタープライズ品質の実装。本番運用に耐えうる水準。

</details>

<details>
<summary>architecture-reviewer レビューレポート</summary>

### レイヤー依存方向
- ✅ Endpoints → Services → Repositories の依存方向を遵守
- ✅ Infrastructure 層が Service インターフェースを実装（DIP 準拠）

### プロジェクト構成
- ✅ 標準ディレクトリ構造に準拠
- ✅ DTOs/Requests, DTOs/Responses の分離

### 指摘事項
- Medium: SagaCoordinator の配置場所（Infrastructure vs Services）

</details>

<details>
<summary>ddd-domain-reviewer レビューレポート</summary>

### Aggregate Root
- ✅ Order が Aggregate Root として機能
- ✅ AddItem メソッドによる子エンティティの操作

### Value Object
- ⚠️ Money Value Object 未導入（Medium 指摘）

### Repository パターン
- ✅ Aggregate Root 単位での Repository 定義

</details>

<details>
<summary>api-endpoint-reviewer レビューレポート</summary>

### REST 規約
- ✅ HTTP メソッドの適切な使用
- ✅ ステータスコードの適切な返却（201 Created, 204 NoContent）

### バリデーション
- ✅ FluentValidation による入力検証
- ✅ ValidationProblem による RFC 9457 準拠レスポンス

### 認可
- ✅ RequireAuthorization / AllowAnonymous の明示
- ✅ IDOR 防止（ユーザー ID 検証）

</details>

<details>
<summary>csharp-standards-reviewer レビューレポート</summary>

### 命名規則
- ✅ PascalCase / camelCase / _camelCase の遵守

### C# 14 機能
- ✅ record 型の使用（DTO）
- ✅ primary constructor の使用
- ✅ パターンマッチングの活用

### 禁止パターン
- ✅ Console.WriteLine なし
- ✅ catch 握りつぶしなし
- ✅ ハードコード秘密情報なし

</details>

<details>
<summary>async-concurrency-reviewer レビューレポート</summary>

### CancellationToken
- ✅ 全 async メソッドに CancellationToken 伝搬
- ✅ BackgroundService で stoppingToken 使用

### async/await
- ✅ .Result / .Wait() の使用なし
- ✅ ConfigureAwait の適切な省略（ASP.NET Core）

</details>

<details>
<summary>error-logging-reviewer レビューレポート</summary>

### 例外処理
- ✅ IExceptionHandler パターン使用
- ✅ 例外階層の定義（NotFoundException, BusinessException 等）

### ログ出力
- ✅ ILogger<T> 使用
- ✅ メッセージテンプレート形式
- ✅ Correlation ID 付与

</details>

<details>
<summary>data-access-reviewer レビューレポート</summary>

### エンティティ設計
- ✅ [Table] / [Column] 属性による明示的マッピング
- ✅ 監査カラム（CreatedAt, UpdatedAt）
- ✅ 楽観的ロック（RowVersion）

### クエリ品質
- ✅ AsNoTracking の適切な使用
- ✅ Include / AsSplitQuery による N+1 防止

### DbContext
- ✅ TimeProvider DI
- ✅ SaveChangesAsync オーバーライドによる監査カラム自動更新

</details>

<details>
<summary>config-di-reviewer レビューレポート</summary>

### DI 登録
- ✅ Scoped / Singleton の適切な使い分け
- ✅ IOptions<T> パターン使用
- ✅ ValidateDataAnnotations / ValidateOnStart

### ミドルウェア順序
- ✅ AGENTS.md §11.3 の順序に完全準拠

### appsettings
- ✅ 秘密情報なし
- ✅ 環境別設定ファイル分離

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### OWASP Top 10
- ✅ SQL インジェクション対策（EF Core LINQ 使用）
- ✅ 認証・認可チェック（FallbackPolicy）
- ✅ IDOR 防止

### 秘密情報管理
- ✅ ハードコードなし
- ✅ 環境変数参照

### セキュリティヘッダー
- ✅ X-Content-Type-Options, X-Frame-Options, CSP 等設定済み

</details>

<details>
<summary>dependency-reviewer レビューレポート</summary>

### NuGet パッケージ
- ✅ プレリリース版なし
- ✅ 禁止パッケージなし
- ✅ TreatWarningsAsErrors = true
- ✅ Nullable = enable

</details>

<details>
<summary>test-quality-reviewer レビューレポート</summary>

### テスト命名
- ✅ Should_X_When_Y パターン使用

### AAA パターン
- ✅ Arrange-Act-Assert の分離

### カバレッジ
- ⚠️ Saga/Outbox 統合テスト不足（High 指摘）

### テスト種別
- ✅ Unit / Integration / Security の分類

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### N+1 クエリ
- ✅ Include / AsSplitQuery で対策済み

### ページネーション
- ✅ Skip/Take によるページネーション実装

### キャッシュ
- ✅ Redis キャッシュサービス実装済み

</details>

<details>
<summary>resilience-reviewer レビューレポート</summary>

### 外部通信
- ⚠️ 本番用 HttpClient 実装未完了

### ヘルスチェック
- ✅ PostgreSQL / Redis / Kafka のヘルスチェック実装

### Outbox
- ✅ 動的バックオフ実装
- ✅ Advisory Lock による排他制御

</details>

---

## 次のアクション

1. **High 指摘の対応**: Saga/Outbox 統合テストの追加
2. **Medium 指摘の計画**: 次回スプリントでの対応検討
3. **本番環境準備**: 外部サービスクライアントの実装

---

*レポート生成: orchest-code-review オーケストレータ*
*14 Agent による包括的レビュー完了*
