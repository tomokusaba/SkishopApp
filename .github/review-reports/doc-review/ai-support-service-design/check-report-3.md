# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/ai-support-service-design.md`
- **判定**: ⚠️ **Conditional Approval** — High 指摘が複数存在し、人間の判断を要する
- **レビュー日時**: 2026-04-03 (イテレーション 3)
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

## 技術スタック検証結果
| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| ランタイム | C# 14 / .NET 10 | C# 14 / .NET 10 | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| AI フレームワーク | Semantic Kernel 1.x | Semantic Kernel 1.x | ✅ |
| ORM | EF Core 10 | EF Core 10 | ✅ |
| DB | PostgreSQL (aisupportdb) | PostgreSQL | ✅ |
| メッセージング | Confluent.Kafka 2.x | Confluent.Kafka 2.x | ✅ |
| キャッシュ | StackExchange.Redis 2.x | StackExchange.Redis 2.x | ✅ |
| 認証 | JWT Bearer | ASP.NET Core Identity + JWT | ✅ |
| ロギング | Serilog 8.x | Serilog 8.x | ✅ |
| 可観測性 | OpenTelemetry 1.x | OpenTelemetry 1.x | ✅ |
| レジリエンス | Polly 8.x + Microsoft.Extensions.Http.Resilience 9.x | Polly 8.x + 同 9.x | ✅ |
| コンテナ | Docker (aspnet:10.0) | Docker 25.x | ✅ |
| バリデーション | FluentValidation 11.x | FluentValidation 11.x | ✅ |
| AI プロバイダ | Azure OpenAI (GPT-4o / text-embedding-3-small) | Azure OpenAI Service | ✅ |
| 検索エンジン | Azure AI Search | ー（設計書固有） | ✅ |

## 指摘サマリー
| Agent | 判定 | Critical | High | Medium | Low | スコア |
|-------|------|----------|------|--------|-----|--------|
| business-analyst | ⚠️ | 0 | 1 | 1 | 1 | 7/10 |
| architect | ⚠️ | 0 | 2 | 2 | 0 | 6/10 |
| tech-lead | ⚠️ | 0 | 2 | 1 | 0 | 7/10 |
| programing-reviewer | ⚠️ | 0 | 2 | 3 | 1 | 6/10 |
| security-reviewer | ⚠️ | 0 | 2 | 2 | 0 | 7/10 |
| dba-reviewer | ⚠️ | 0 | 1 | 2 | 1 | 7/10 |
| qa-manager | PASS | 0 | 0 | 2 | 1 | 8/10 |
| performance-reviewer | PASS | 0 | 0 | 2 | 1 | 8/10 |
| compliance-reviewer | ⚠️ | 0 | 1 | 1 | 0 | 8/10 |
| oss-reviewer | PASS | 0 | 0 | 1 | 0 | 9/10 |
| release-manager | PASS | 0 | 0 | 0 | 1 | 9/10 |
| infra-ops-reviewer | PASS | 0 | 0 | 1 | 1 | 8/10 |
| audit-reviewer | PASS | 0 | 0 | 1 | 0 | 9/10 |
| ux-accessibility-reviewer | PASS | 0 | 0 | 1 | 0 | 9/10 |
| **合計** | | **0** | **11** | **20** | **7** | |

## 判定根拠
- 判定ルール適用結果: Critical 0 件、High 11 件 → **⚠️ Conditional Approval**（High 指摘のみ、人間の判断を介在）
- 最も重大な指摘: spec.md との Aggregate Root / エンティティ定義の不整合（architect, tech-lead）、および Semantic Kernel DI キャプティブ依存の問題（programing-reviewer）

---

## Critical/High 指摘一覧（修正必須）

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------------|----------|----------|
| H-01 | **High** | architect, tech-lead | spec.md 整合性 | §5, §12d, §21 | spec.md §データモデル では AI サポートの Aggregate Root を `UserInteraction` と定義しているが、設計書にはこのエンティティが存在しない。代わりに `UserProfile` が中心エンティティとなっている。spec.md のエンティティ一覧（`UserInteraction`, `ProductRecommendation`, `SearchQuery`, `BehaviorAnalysis`）と設計書のエンティティ（`UserProfile`, `Recommendation`, `SearchAnalytics`, `DemandForecast`, `ModelTraining`）に大幅な乖離がある。 | spec.md と設計書のエンティティ定義を統一する。設計書が最新であれば spec.md を設計書に合わせて改訂し、Aggregate Root を `UserProfile`（または `ChatSession`）に更新する。改訂履歴に反映すること。 |
| H-02 | **High** | architect | spec.md 整合性 | §5.2 | spec.md §AI サポートサービスインデックスが参照する `user_interactions` テーブル、`recommendation_logs` テーブルは設計書に存在しない。spec.md のインデックス定義が設計書のテーブル構造と不整合。 | spec.md のインデックス定義を設計書の実テーブル（`user_profiles`, `chat_sessions`, `chat_messages`, `recommendations`, `search_analytics`）に合わせて更新する。 |
| H-03 | **High** | programing-reviewer | コード品質 | §8.2, §32 | `ProductIndexSyncConsumer` は `BackgroundService`（Singleton ライフタイム）だが、コンストラクタで `Kernel`（§32 で Scoped 登録）を直接注入している。これはキャプティブ依存（Captive Dependency）であり、`ObjectDisposedException` または古いサービスインスタンスの使用につながる。§32 の DI 登録では `BackgroundService` に `IServiceScopeFactory` を注入して Scoped サービスを取得するパターンが必要。 | `ProductIndexSyncConsumer` のコンストラクタから `Kernel` を除去し、`IServiceScopeFactory` 経由で `ExecuteAsync` 内でスコープを生成して `Kernel` を取得するよう修正する。`SearchClient` も同様に確認すること。 |
| H-04 | **High** | programing-reviewer | コード品質 | §7.3, §10 | `ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions` は Semantic Kernel 1.x の古い API。現行の Semantic Kernel 1.x（2024 年後半以降）では `FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()` に変更されている。実装時に `ToolCallBehavior` が廃止されている可能性がある。 | `FunctionChoiceBehavior.Auto()` に更新する。Semantic Kernel 1.x のリリースノートで API の最新仕様を確認し、設計書のコード例を修正すること。 |
| H-05 | **High** | security-reviewer | セキュリティ | §27 | `OrderPlugin.GetOrderStatusAsync` / `GetRecentOrdersAsync` の `userId` パラメータが `[Description]` アノテーションで「AI は変更不可」と注記されているが、Semantic Kernel の Function Calling では AI がパラメータ値を自由に指定可能。`ChatService` 側で `userId` を強制的に認証済みユーザー ID に上書きする実装が設計書に明記されていない。IDOR（Insecure Direct Object Reference）のリスクがある。 | `ChatService.SendMessageAsync` 内で `OrderPlugin` の `userId` パラメータをオーバーライドする仕組み（例: `KernelArguments` に `userId` をプリセットし、AI の指定値を無視する）を設計書に明記する。もしくは `OrderPlugin` から `userId` パラメータを除去し、Plugin 内で `HttpContext` / `IHttpContextAccessor` 経由で認証情報を取得する設計に変更する。 |
| H-06 | **High** | security-reviewer | セキュリティ | §12.3 | プロンプトインジェクション対策の第4層（応答後処理フィルタ `ResponseFilter.FilterResponse`）が `Contains("【重要な制約")` のような固定文字列チェックのみで極めて脆弱。AI が制約内容を言い換え（パラフレーズ）で漏洩した場合に検知できない。また、`ChatService.SendMessageAsync` のコードフロー上で `ResponseFilter.FilterResponse` が呼び出されていない（設計書に呼び出し箇所が未記載）。 | (1) `ResponseFilter` の呼び出し箇所を `ChatService.SendMessageAsync` 内の AI 応答取得後に明記する。(2) Azure OpenAI Content Safety のカスタムブロックリスト機能を活用し、サーバー側でフィルタリングを強化する設計を追加する。(3) 応答長が異常に長い場合の切り捨て処理を追加する。 |
| H-07 | **High** | tech-lead | 規約準拠 | §7.3 | `ChatService.SendMessageAsync` で `chatCompletion.GetChatMessageContentsAsync` 呼び出し時に `kernel` パラメータを渡しているが、`ForecastService.GenerateForecastAsync`（§10.1）では `kernel` パラメータが省略されており、プラグインの自動呼び出しが動作しない可能性がある。また `ForecastService` での `cancellationToken` 名のパラメータが名前付きで渡されており一貫性がない。 | `ForecastService.GenerateForecastAsync` の `GetChatMessageContentsAsync` 呼び出しに `kernel` パラメータを追加する（プラグイン呼び出しが不要なら明示的に `executionSettings` で `FunctionChoiceBehavior = FunctionChoiceBehavior.None()` を指定する）。CancellationToken の引数名を `ct` に統一する。 |
| H-08 | **High** | architect | DDD 整合性 | §4.3, §13 | §4.3 Semantic Kernel アーキテクチャ図のプラグイン定義に `ChatPlugin`（会話応答生成）が記載されているが、実装コード（§7.2, §27）では `ChatPlugin` は存在しない。実際には `ProductPlugin`、`OrderPlugin`、`FaqPlugin` の3つが定義されている。Mermaid 図とコード実装の不整合。 | §4.3 の Mermaid 図を実コード（`ProductPlugin`, `OrderPlugin`, `FaqPlugin`）に合わせて更新する。`ChatPlugin` を削除し、正しいプラグイン名に修正する。 |
| H-09 | **High** | compliance-reviewer | GDPR | §12.7, spec.md | spec.md §データ保持ポリシーでは AI チャット会話ログの保持期間は 90 日、AI ユーザーコンテキスト（`user_interactions`）は 180 日と定義されているが、設計書の `DataRetentionCleanupService`（§30.1）でも `user_profiles` テーブル自体の保持期間・クリーンアップが未定義。 `user_profiles` の `browsing_history_json` は §12.7 で「30 日分のみ保持」と記載されているが、`DataRetentionCleanupService` にこのロジックが実装されていない。 | `DataRetentionCleanupService` に `browsing_history_json` の 30 日超過データ除去処理を追加する。`purchase_history_json` の月次匿名化集約ロジックも追加する。 |
| H-10 | **High** | dba-reviewer | DB 設計 | §12d, §22 | `UserProfile.PreferencesJson` / `BrowsingHistoryJson` / `PurchaseHistoryJson` は C# 上で `string?` 型だが、§5.2 のテーブル定義では `JSONB` 型。EF Core の Npgsql プロバイダーで `string` プロパティを JSONB にマッピングするには `.HasColumnType("jsonb")` の明示指定が必要。§22 の `OnModelCreating` ではGIN インデックスを定義しているが、JSONB カラムタイプの明示指定がない。マイグレーション生成時に `TEXT` 型として作成される可能性がある。 | `OnModelCreating` 内で該当プロパティに `.HasColumnType("jsonb")` を追加するか、エンティティの `[Column]` 属性に `TypeName = "jsonb"` を指定する。§12d のエンティティ定義と §22 の Fluent API の両方を修正すること。 |
| H-11 | **High** | programing-reviewer | コード品質 | §30.1 | `DataRetentionCleanupService` の Advisory Lock 取得部分で `ExecuteSqlInterpolatedAsync` の戻り値を `> 0` で判定しているが、`SELECT pg_try_advisory_lock(...)` は PostgreSQL で boolean を返す SELECT 文であり、`ExecuteSqlInterpolatedAsync` は影響行数（この場合常に -1）を返す。Advisory Lock の取得判定が正しく動作しない。 | `FromSqlInterpolated` + `SingleOrDefaultAsync` でbooleanを取得するか、`ExecuteSqlRawAsync` ではなく ADO.NET の `NpgsqlCommand` で直接実行して結果を取得する設計に変更する。 |

---

## Medium/Low 指摘一覧（推奨改善事項）

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------------|----------|----------|
| M-01 | Medium | programing-reviewer | コード品質 | §7.3 | `ChatService` の `EstimateTokenCount` メソッドが `text.Length / 4` の概算で実装されている。日本語テキストは 1 文字あたり 1〜2 トークンであり、英語前提の概算（1 トークン ≈ 4 文字）と大幅にずれる。トークン使用量制限（§12.4）の判定精度に影響する。 | `Microsoft.ML.Tokenizers` パッケージの `TiktokenTokenizer` を使用して正確なトークン数を算出するか、精度の高い係数（日本語: `text.Length * 0.7` 等）を使用する。 |
| M-02 | Medium | programing-reviewer | コード品質 | §9.2 | `RecommendationService.GetPersonalizedAsync` 内の `AnonymizeUserPreferences` がハードコードされたダミー値（`"スキー板, ブーツ"`, `"中〜高価格帯"`, `"中級者"`）を返している。実装テンプレートとしてのプレースホルダーだが、本番コードにこのまま残った場合、全ユーザーに同一のレコメンデーションが生成される。 | `AnonymizeUserPreferences` の実装を JSONB パース処理に置き換えるロジックの設計を記載する。もしくは `// TODO: 実装時に JSONB からカテゴリ集計結果を抽出` コメントを明記する。 |
| M-03 | Medium | programing-reviewer | コード品質 | §25.2 | `SearchEndpoints.Search` メソッドで `[AsParameters] SearchRequest request` としてバインドしているが、`SearchRequest` の `Query` プロパティに `[Required]` 属性が付与されている。Minimal API の `[AsParameters]` バインド時に、`[Required]` が HTTP 400 を自動返却するかは ASP.NET Core のバージョンに依存する。FluentValidation との二重バリデーションの懸念もある。 | Data Annotations と FluentValidation の役割を明確化する。Endpoint レベルでは FluentValidation の `IValidator<T>` を明示的に呼び出すパターンに統一し、DTO の Data Annotations はドキュメント目的とする。 |
| M-04 | Medium | architect | 設計整合性 | §11 | §11.1 で `USER_REGISTERED` イベント（AuthService 発行）を購読して「ユーザープロファイル初期化」とあるが、§30.2 の `UserDeletionConsumer` は `user.deletion.requested`（UserManagementService 発行）を購読している。ユーザー登録イベントの Consumer が設計書に未実装。 | `UserRegisteredConsumer`（BackgroundService）を設計書に追加し、`USER_REGISTERED` イベント受信時に `UserProfile` を初期化するロジックを記載する。 |
| M-05 | Medium | architect | Bounded Context | §1.2, §9.1 | レコメンデーション戦略の「よく一緒に購入」（共起分析）が「注文明細データ」を使用するとあるが、注文明細は `SalesManagementService` の管轄であり、AiSupportService が直接参照できない。`IProductClient.GetSalesHistoryAsync` で間接取得する設計だが、大量の注文明細データの同期方法（Kafka イベント? API 呼び出し?）が未定義。 | Kafka `OrderCreated` イベントから注文明細データを AiSupportService のローカル DB に蓄積するコンシューマーを設計するか、`SalesManagementService` に共起分析用 API を追加する設計を明記する。 |
| M-06 | Medium | security-reviewer | セキュリティ | §12.6 | `SsrfPreventionHandler` が IPv4-mapped IPv6 アドレス（例: `::ffff:10.0.0.1`）を検出できない可能性がある。`IPNetwork.Parse("10.0.0.0/8")` は IPv4 ネットワークのみマッチし、`::ffff:10.0.0.1` はこのネットワークに含まれない場合がある。 | `IPAddress.MapToIPv4()` メソッドで IPv6 アドレスを IPv4 に変換した上でブロックリストと照合するロジックを追加する。 |
| M-07 | Medium | security-reviewer | セキュリティ | §8.1 | `SearchService` のカテゴリホワイトリスト（`AllowedCategories`）がハードコードされた静的な `HashSet<string>` である。InventoryManagementService のカテゴリマスタとの同期方法が未定義。カテゴリの追加・変更時にコードのデプロイが必要。 | Kafka イベントまたは Redis キャッシュ経由でカテゴリマスタを同期する設計を追加する。もしくはカテゴリのバリデーションを AiSupportService ではなく InventoryManagementService 側の API に委譲する。 |
| M-08 | Medium | dba-reviewer | DB 設計 | §5.2 | `chat_sessions` テーブルに `user_id` カラムのインデックスが §5.2 のテーブル定義で明示されていない（§22 の Fluent API では定義あり）。テーブル定義セクションとコード定義セクションのインデックス情報が分散し不整合リスクがある。 | §5.2 の全テーブル定義にインデックスセクションを追加し、§22 の Fluent API 定義と完全に一致させる。 |
| M-09 | Medium | dba-reviewer | DB 設計 | §5.2, §12d | `OutboxEvent.Payload` が `string` 型（C#）/ `JSONB`（SQL）の対応が必要だが、§22 の `OnModelCreating` で `.HasColumnType("jsonb")` が指定されていない。`TEXT` として作成される可能性がある。 | `OutboxEvent.Payload` に `.HasColumnType("jsonb")` を追加する（AGENTS.md の Outbox パターンではペイロードは JSON）。 |
| M-10 | Medium | qa-manager | テスト | §15, §33 | §33.2 `SearchServiceTests` と §33.4 `ChatEndpointsIntegrationTests` のテストメソッドが一部スケルトン（実装コメントのみ）で終わっている。テスト設計書としての完全性が不足。 | スケルトンテストに期待される Arrange/Act/Assert の疑似コードまたは完全な実装を追加する。 |
| M-11 | Medium | qa-manager | テスト | §15.2 | トークン上限超過テスト、セッション上限超過テスト（§12.4 の `AiLimits` に基づく）が §15.2 の「Service 層の必須テストケース一覧」に記載されているが、§33 のテストコード実装に含まれていない。 | §33.1 に `Should_ThrowTokenLimitExceededException_When_SessionTokenLimitExceeded` / `Should_ThrowSessionLimitExceededException_When_DailySessionLimitExceeded` のテスト実装を追加する。 |
| M-12 | Medium | performance-reviewer | パフォーマンス | §8.1 | Azure AI Search のハイブリッド検索で `KNearestNeighborsCount = 50` が設定されている。大規模インデックスでは KNN=50 は過剰であり、レイテンシが増加する。商品数が 10,000 件以下の場合、KNN=20〜30 で十分な精度が得られる。 | KNN 値を設定可能にする（`AzureSearchSettings` に `KNearestNeighborsCount` を追加）。初期値は 30 程度が推奨。 |
| M-13 | Medium | performance-reviewer | パフォーマンス | §9.2 | `RecommendationService.GetPersonalizedAsync` で Redis キャッシュと Azure OpenAI 呼び出しの両方を行っているが、キャッシュミス時に Azure OpenAI 呼び出し結果（プロンプト応答）を直接商品検索に活用していない（`productClient.GetTopProductsAsync("recommended", 10, ct)` で固定取得している）。AI の推薦結果が商品取得に反映されないロジック上の問題。 | AI の応答結果（カテゴリ・条件の JSON）をパースし、その条件で `productClient` を呼び出すロジックを設計する。現状の実装では AI を呼んでいるが結果を使っていない。 |
| M-14 | Medium | infra-ops-reviewer | インフラ | §17 | Dockerfile の `EXPOSE 5009` は `AGENTS.md` のポート定義と一致するが、`HEALTHCHECK` の `curl` コマンドが `aspnet` ベースイメージに含まれていない可能性がある。`mcr.microsoft.com/dotnet/aspnet:10.0` には `curl` がプリインストールされていない。 | `curl` の代わりに `wget --spider` を使用するか、`HEALTHCHECK` を Azure Container Apps のヘルスプローブに委ねる（Dockerfile から `HEALTHCHECK` を削除し、Bicep の `probes` で定義する）。 |
| M-15 | Medium | business-analyst | 機能要件 | §1, §6 | spec.md の KPI「AI チャット解決率 60% 以上」の測定方法が設計書で未定義。チャットが「AI で解決された」と判定する基準（エスカレーションなしでセッションが CLOSED されたことで判定するのか、ユーザーフィードバックに基づくのか）が不明。 | AI チャット解決率の測定ロジック（判定基準・集計 SQL・API エンドポイント）を §14 または §6.5 に追加する。 |
| M-16 | Medium | audit-reviewer | トレーサビリティ | §12b | Correlation ID の Kafka メッセージヘッダーへの伝搬が設計書で記載されているが、§28 の `OutboxPublisher` のメッセージ発行時に Correlation ID がヘッダーに設定されていない（`event-type` と `aggregate-type` のみ）。 | `OutboxPublisher` のメッセージ発行時に Correlation ID をヘッダーに追加するか、`OutboxEvent` エンティティに `correlation_id` カラムを追加して Outbox 経由で伝搬する設計にする。 |
| M-17 | Medium | ux-accessibility-reviewer | UX | §6.2, §7.3 | チャット API がストリーミング応答（SSE / Server-Sent Events）に対応していない。現在の設計では AI 応答が全て生成されてからまとめて返却されるため、長い応答（500〜1000 トークン）では体感レイテンシが大きい。 | Phase 1 では現状の非ストリーミング設計を維持し、Phase 2 で SSE（`IAsyncEnumerable` + チャンク応答）を導入する計画を §20 の制約セクションに明記する。 |
| L-01 | Low | business-analyst | ドキュメント | §20 | §20 の制約「チャットボットは日本語のみ対応（多言語対応は Phase 2 以降）」と spec.md の「多言語対応（日本語・英語）」の Phase 整合性が明確でない。 | spec.md の多言語対応 Phase 定義と設計書の Phase 定義を照合し、AI チャットの英語対応が Phase 2 であることを両方で明示する。 |
| L-02 | Low | programing-reviewer | コードスタイル | §7.3 | `ChatService` の `SystemPrompt` が `private static readonly string` で定義されているが、AGENTS.md の C# 14 推奨に従い `const string` で定義可能。ただし複数行リテラルでは `const` が使用できないため、現状の `static readonly` で問題ない。ログ出力のメッセージテンプレートは正しく使用されている。 | 指摘なし（現状で適切）。 |
| L-03 | Low | dba-reviewer | DB 設計 | §5.2 | `demand_forecasts` テーブルの `confidence_score` が `DECIMAL(5,4)` だが、スコアが 0.0000〜9.9999 の範囲となり、1.0 を超える値も格納可能。信頼度スコアが 0〜1 の範囲であれば `DECIMAL(3,2)` で十分。 | `DECIMAL(5,4)` を `DECIMAL(5,4)` のまま維持するか（将来の拡張余地）、CHECK 制約 `confidence_score BETWEEN 0 AND 1` を追加する。 |
| L-04 | Low | performance-reviewer | パフォーマンス | §9.2 | レコメンデーションの Redis キャッシュ TTL が固定 1 時間。トレンド商品レコメンデーション（売上変動が大きい）と類似商品レコメンデーション（変動が小さい）で異なる TTL が望ましい。 | レコメンデーションタイプごとの TTL 設定を `AiLimitsSettings` に追加する（トレンド: 15 分、パーソナライズド: 1 時間、類似: 2 時間等）。 |
| L-05 | Low | infra-ops-reviewer | インフラ | §18.3 | Bicep の `minReplicas: 1` は ADR-0010 の冗長化要件（`minReplicas: 2`）と不整合。spec.md のスケーリング設計では AiSupportService の `minReplicas` は 2 と定義されている。 | Bicep の `minReplicas` を 2 に変更する。 |
| L-06 | Low | release-manager | CI/CD | §19 | CI/CD パイプラインで `dotnet-version: '10.0.x'` が使用されているが、`10.0.x` が公開されている .NET SDK バージョンと一致するか確認が必要。また、`actions/checkout@v4` / `actions/setup-dotnet@v4` のメジャーバージョン固定は適切。 | .NET 10 LTS リリース後にSDKバージョン番号を確認・更新する。 |
| L-07 | Low | oss-reviewer | 依存関係 | §2 | `Azure.Search.Documents` のバージョンが `11.*` と指定されているが、AGENTS.md の必須パッケージリストに含まれていない（AI サービス固有）。パッケージの GA 安定性と .NET 10 互換性を確認する必要がある。また `Azure.Identity` パッケージが §2 のテーブルに含まれているのは適切。 | `Azure.Search.Documents` と `Microsoft.SemanticKernel.Connectors.AzureOpenAI` の .NET 10 互換性を実装フェーズ開始時に確認する。 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-01 | 高優先 | architect, tech-lead | spec.md と設計書のエンティティ定義に大幅な乖離がある（H-01, H-02）。設計書のエンティティが最新かつ正確であれば spec.md（SSOT）を更新する必要があるが、spec.md が SSOT であるため PO / テックリード承認が必要。 | テックリード |
| E-02 | 高優先 | security-reviewer | OrderPlugin の IDOR リスク（H-05）の対応方針。Semantic Kernel の Function Calling で `userId` を安全にプリセットする実装パターンを確定する必要がある。 | テックリード + セキュリティ担当 |
| E-03 | 通常 | compliance-reviewer | `user_interactions` テーブルが spec.md の GDPR データ保持テーブル（180 日）に記載されているが設計書に存在しない。GDPR 準拠のデータ保持ポリシーを正確に維持するため、spec.md と設計書の差異を解消する必要がある。 | テックリード + コンプライアンス担当 |

---

## 競合解決記録

本レビューでは Agent 間の競合は検出されなかった。

---

## ドキュメント横断分析

### spec.md との整合性チェック

| チェック項目 | 結果 | 詳細 |
|------------|------|------|
| Aggregate Root 定義 | ❌ 不整合 | spec.md: `UserInteraction` → 設計書: エンティティ未定義（`UserProfile` が中心） |
| エンティティ一覧 | ❌ 不整合 | spec.md 6 エンティティ vs 設計書 8 エンティティ。名称・構造が大幅に異なる |
| インデックス定義 | ❌ 不整合 | spec.md が参照する `user_interactions` / `recommendation_logs` テーブルが設計書に不存在 |
| Kafka イベント定義 | ✅ 整合 | `user.deleted`, `user.profile-updated`, `consent.revoked` の購読設計あり |
| GDPR データ保持期間 | ⚠️ 部分整合 | チャット 90 日は一致。`user_interactions` 180 日は設計書に対応テーブルなし |
| ポート番号 | ✅ 整合 | 5009 で一致 |
| 技術スタック | ✅ 整合 | C# 14, .NET 10, Semantic Kernel 1.x, PostgreSQL 全て一致 |
| KPI「AI チャット解決率 60%」 | ⚠️ 部分対応 | 設計書に測定ロジックの詳細が未定義 |
| DPIA | ✅ 整合 | spec.md §DPIA 計画と設計書 §12.8 が整合 |
| SSRF 防止設計 | ✅ 整合 | spec.md §SSRF 防止設計の要件を設計書 §12.6 で実装 |
| スケーリング設計 | ⚠️ 部分整合 | spec.md: minReplicas=2 vs 設計書 Bicep: minReplicas=1 |

### 設計書の完成度評価

| セクション | 充実度 | 備考 |
|-----------|--------|------|
| 概要・スコープ | ★★★★★ | 明確 |
| 技術スタック | ★★★★★ | AGENTS.md と整合 |
| システムアーキテクチャ | ★★★★☆ | Mermaid 図のプラグイン名称不整合（H-08） |
| データモデル | ★★★★☆ | JSONB カラムタイプ未指定（H-10） |
| API 設計 | ★★★★★ | 網羅的 |
| Semantic Kernel 統合 | ★★★★☆ | ToolCallBehavior API 更新必要（H-04） |
| 検索設計 | ★★★★★ | ハイブリッド検索 + OData インジェクション対策 |
| レコメンデーション | ★★★☆☆ | AI 応答結果の未活用（M-13） |
| イベント設計 | ★★★★☆ | UserRegisteredConsumer 未実装（M-04） |
| セキュリティ設計 | ★★★★☆ | 多層防御は優秀。IDOR リスク（H-05）要修正 |
| 耐障害性 | ★★★★★ | フォールバック戦略が明確 |
| テスト戦略 | ★★★★☆ | 一部スケルトン（M-10） |
| Program.cs / DI 設計 | ★★★★★ | 統合ビューが非常に充実 |
| インフラ（Bicep / CI/CD） | ★★★★☆ | RBAC 設計あり、minReplicas 不整合（L-05） |
| エンティティ定義 | ★★★★☆ | JSONB プロパティの型指定（H-10） |
| Outbox パターン | ★★★★★ | 動的バックオフ完全実装 |
| GDPR | ★★★★☆ | 保持ポリシー・削除フロー完備。browsing_history 未実装（H-09） |

### 未定義・曖昧な領域

| # | 領域 | 影響度 | 備考 |
|---|------|--------|------|
| 1 | spec.md とのエンティティ名称統一 | 高 | 実装時のブロッカー |
| 2 | AI チャット解決率の測定ロジック | 中 | KPI 達成判定に必要 |
| 3 | カテゴリマスタの同期方法 | 中 | 運用時の問題 |
| 4 | OrderPlugin の userId 安全注入メカニズム | 高 | セキュリティ上のブロッカー |
| 5 | UserRegisteredConsumer の実装 | 中 | ユーザー初期化フロー不完全 |
| 6 | ストリーミング応答の Phase 計画 | 低 | UX 改善の計画明確化 |

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### ビジネス要件の完全性

**評価: 7/10**

#### 強み
- スコープ定義（In Scope / Out of Scope）が明確
- 4 つの主要機能（レコメンデーション、チャットボット、検索、需要予測）の要件が具体的
- KPI「AI チャット解決率 60%」への対応としてエスカレーション機能を設計
- フィードバック API（レコメンデーション・検索）によるアルゴリズム改善ループの設計あり

#### 指摘事項
| 重要度 | 指摘 |
|--------|------|
| High | AI チャット解決率 60% の測定方法が未定義。「解決」の判定基準（エスカレーションなし CLOSED? ユーザー満足度?）を明確化すべき |
| Medium | 需要予測のビジネス価値（在庫最適化による売上改善効果の KPI）が未定義。需要予測精度の目標値も未設定 |
| Low | spec.md のビジネス要件「AI チャットボットによるカスタマーサポート」は達成。ただし「リアルタイム在庫確認と配送追跡」の AI 統合（例: チャットで在庫確認）は設計されていない |

</details>

<details>
<summary>architect レビューレポート</summary>

### アーキテクチャ評価

**評価: 6/10**

#### 強み
- Semantic Kernel の 3 層構成（Kernel → プラグイン → コネクタ）が明確
- Outbox パターン + BackgroundService の非同期イベント発行が ADR-0005 に完全準拠
- RAG パイプライン（Azure AI Search → Embedding → ハイブリッド検索）の設計が具体的
- フォールバック戦略（Azure OpenAI 障害時の代替応答）が実装レベルで記載

#### 指摘事項
| 重要度 | 指摘 |
|--------|------|
| High | spec.md の Aggregate Root `UserInteraction` が設計書に未定義。DDD の Bounded Context 定義が SSOT と不整合（H-01） |
| High | spec.md のインデックス定義が参照する `user_interactions` / `recommendation_logs` が設計書に不存在（H-02） |
| Medium | UserRegisteredConsumer が未実装。ユーザー登録→プロファイル初期化のフローが不完全（M-04） |
| Medium | 共起分析データ（注文明細）のサービス間データフロー未定義（M-05） |

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 技術標準の横断適合性

**評価: 7/10**

#### 強み
- AGENTS.md の全規約（naming、DI、CancellationToken、例外処理、ミドルウェア順序）に高い準拠度
- primary constructor の適切な使用
- `TimeProvider` の DI による DateTime テスタビリティ確保
- 構造化ログのメッセージテンプレート形式が一貫

#### 指摘事項
| 重要度 | 指摘 |
|--------|------|
| High | spec.md とのエンティティ定義乖離が SSOT 原則に違反。設計書が正ならば spec.md 更新が必須（E-01） |
| High | `ForecastService` の `GetChatMessageContentsAsync` 呼び出しで `kernel` パラメータ省略（H-07） |
| Medium | `AllowedHostsOptions` の `IsAllowed` メソッドがワイルドカード `*.openai.azure.com` を `host.EndsWith` で処理する際、`h[1..]` が `.openai.azure.com` となり先頭の `.` を含む。`evil-openai.azure.com` のようなサブドメインもマッチする可能性がある |

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### コード品質評価

**評価: 6/10**

#### 強み
- CancellationToken が全 async メソッドシグネチャに含まれている（前回レビューからの改善）
- record 型 DTO、primary constructor の適切な活用
- 例外クラス階層が AGENTS.md §4.7 に完全準拠
- 全エンティティに `[Table]` / `[Column]` データアノテーションが付与

#### 指摘事項
| 重要度 | 指摘 |
|--------|------|
| High | `ProductIndexSyncConsumer` のキャプティブ依存（H-03） |
| High | `ToolCallBehavior` API の非推奨化懸念（H-04） |
| Medium | `EstimateTokenCount` の日本語トークン概算精度（M-01） |
| Medium | `AnonymizeUserPreferences` のハードコード値（M-02） |
| Medium | `[AsParameters]` + FluentValidation の二重バリデーション（M-03） |
| Low | コード例全体の品質は高い。禁止パターン（`Console.WriteLine`、`FromSqlRaw` 文字列結合、`.Result`/`.Wait()`）は検出されず |

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### セキュリティ評価

**評価: 7/10**

#### 強み
- プロンプトインジェクション対策の多層防御（5 層）が設計されている
- SSRF 防止設計（URL ホワイトリスト + プライベート IP 拒否 + DNS リバインディング対策）が具体的
- Azure OpenAI への Managed Identity 認証（API キー排除）
- GDPR 準拠のデータ匿名化（`AnonymizeUserPreferences`）
- PII ログ禁止が遵守されている
- レート制限がエンドポイント種別ごとに設定されている

#### 指摘事項
| 重要度 | 指摘 |
|--------|------|
| High | OrderPlugin の IDOR リスク（H-05） — Semantic Kernel Function Calling 経由での userId 詐称 |
| High | ResponseFilter の呼び出し箇所未記載 + パラフレーズ漏洩への対策不足（H-06） |
| Medium | IPv4-mapped IPv6 アドレスの SSRF バイパス懸念（M-06） |
| Medium | カテゴリホワイトリストのハードコード問題（M-07） |

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### データベース設計評価

**評価: 7/10**

#### 強み
- 全テーブルに snake_case 命名、適切な CHECK 制約が定義
- インデックス設計が検索パターンに対応
- Outbox テーブルが ADR-0005 に準拠
- `SaveChangesAsync` オーバーライドによる `updated_at` 自動更新
- CASCADE / RESTRICT 削除ポリシーが明確

#### 指摘事項
| 重要度 | 指摘 |
|--------|------|
| High | JSONB カラムの型指定不足（H-10） |
| Medium | `chat_sessions` のインデックス定義がセクション間で不整合（M-08） |
| Medium | `OutboxEvent.Payload` の JSONB 型指定漏れ（M-09） |
| Low | `confidence_score` の DECIMAL 精度と CHECK 制約（L-03） |

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### テスト戦略評価

**評価: 8/10**

#### 強み
- テスト分類（Unit / Integration / DB / AI 品質テスト）が明確
- カバレッジ目標が AGENTS.md §9.4 に準拠（全体 80%、Service 80%、Repository 70%）
- AI 固有テスト（ゴールデンテスト、プロンプトインジェクション防御テスト、Grounding テスト）の定義が具体的
- テストメソッド命名が `Should_X_When_Y` パターンに従っている
- NSubstitute によるモック化パターンが適切

#### 指摘事項
| 重要度 | 指摘 |
|--------|------|
| Medium | 一部テストがスケルトン実装（M-10） |
| Medium | トークン/セッション上限テストの実装不足（M-11） |
| Low | AI 品質テストの「ゴールデンテストセット 20 件」の具体的なテストデータが未定義 |

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### パフォーマンス評価

**評価: 8/10**

#### 強み
- Azure OpenAI レジリエンス設定（リトライ 3 回、サーキットブレーカー 30 秒）が適切
- 検索応答時間のメトリクス（`ai.search.response.duration`）が監視対象
- Redis キャッシュによるレコメンデーション高速化
- 会話履歴のスライディングウィンドウ方式（直近 20 件）でトークン上限とメモリ消費を制御
- Outbox ポーリングの動的バックオフ（100ms〜5s）で低レイテンシと低 DB 負荷を両立

#### 指摘事項
| 重要度 | 指摘 |
|--------|------|
| Medium | KNN=50 が過剰（M-12） |
| Medium | AI 応答結果の未活用（M-13） — パフォーマンスコストを払って AI を呼んでいるが結果を捨てている |
| Low | レコメンデーションタイプごとの TTL 差別化（L-04） |

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### コンプライアンス評価

**評価: 8/10**

#### 強み
- GDPR Art.17（削除権）のカスケード削除フロー（§30.2）が具体的
- DPIA（データ保護影響評価）への言及と軽減措置の設計（§12.8）
- オプトアウト機能（PERSONALIZATION 同意撤回）の設計が spec.md と整合
- PII 除去プリプロセスが spec.md に記載
- Azure OpenAI データ送信ポリシーへの配慮

#### 指摘事項
| 重要度 | 指摘 |
|--------|------|
| High | `browsing_history_json` / `purchase_history_json` の保持期間ポリシーが `DataRetentionCleanupService` に未実装（H-09） |
| Medium | spec.md の `user_interactions` テーブル（180 日保持）が設計書に不存在。GDPR 記録（RoPA）と設計書の不整合（E-03） |

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### OSS / 依存関係評価

**評価: 9/10**

#### 強み
- 主要パッケージが AGENTS.md の必須パッケージリストに準拠
- プレリリースパッケージ（-preview, -beta, -rc）の使用なし
- `Newtonsoft.Json` ではなく `System.Text.Json` を使用
- 禁止パッケージ（`System.Web`, `log4net`, EF6, `WebClient`）の使用なし

#### 指摘事項
| 重要度 | 指摘 |
|--------|------|
| Medium | `Azure.Search.Documents 11.*` と `Microsoft.SemanticKernel.Connectors.AzureOpenAI 1.*` は AGENTS.md の必須リストに含まれない AI 固有パッケージ。.NET 10 互換性の事前確認を推奨 |

</details>

<details>
<summary>release-manager レビューレポート</summary>

### リリース準備評価

**評価: 9/10**

#### 強み
- CI/CD パイプライン（GitHub Actions）が完全定義
- Docker マルチステージビルド + 非 root ユーザー
- Azure Container Apps へのデプロイフロー定義あり
- コードカバレッジレポート（Codecov 統合）
- PR トリガー + main ブランチデプロイの適切な分離

#### 指摘事項
| 重要度 | 指摘 |
|--------|------|
| Low | .NET 10 SDK バージョン番号の事前確認（L-06） |

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### インフラ・運用評価

**評価: 8/10**

#### 強み
- Azure OpenAI / Azure AI Search の Bicep 定義が具体的
- RBAC（Cognitive Services OpenAI User / Search Index Data Reader）の Managed Identity 設定
- ヘルスチェック（PostgreSQL + Redis + Azure OpenAI + Azure AI Search）
- OpenTelemetry 統合（Traces + Metrics）
- アラート条件（5 項目）の定義
- Correlation ID の伝搬設計

#### 指摘事項
| 重要度 | 指摘 |
|--------|------|
| Medium | Dockerfile の `HEALTHCHECK` で `curl` が使えない懸念（M-14） |
| Low | Bicep の `minReplicas: 1` が ADR-0010 / spec.md と不整合（L-05） |

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 監査・トレーサビリティ評価

**評価: 9/10**

#### 強み
- Correlation ID のミドルウェア + 全外部通信への伝搬設計
- 構造化ログ（Serilog + CompactJsonFormatter）でメッセージテンプレート形式を一貫使用
- `model_trainings` テーブルに `created_by` 監査カラムあり
- OpenTelemetry による分散トレーシングが全レイヤーに適用
- Outbox テーブルにイベント発行のトレーサビリティ

#### 指摘事項
| 重要度 | 指摘 |
|--------|------|
| Medium | OutboxPublisher のメッセージヘッダーに Correlation ID が含まれていない（M-16） |

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### UX / アクセシビリティ評価

**評価: 9/10**

#### 強み
- チャット API のレスポンス構造が明確（`SendMessageResponse` に user/assistant メッセージを分離）
- エスカレーション機能によるフォールバック UX
- 検索サジェスト API により入力補助を提供
- フォールバック応答（AI 障害時の固定メッセージ）が定義済み

#### 指摘事項
| 重要度 | 指摘 |
|--------|------|
| Medium | ストリーミング応答未対応による体感レイテンシの問題（M-17） |

</details>

---

## 総評

本設計書は **4,100 行を超える非常に充実したドキュメント** であり、Semantic Kernel 統合、SSRF 多層防御、プロンプトインジェクション対策、GDPR データ保護、Outbox パターン、完全な DI 登録ビュー、FluentValidation、テスト戦略、インフラ（Bicep / CI/CD）まで網羅的に記載されている。前回レビュー（check-report-2）で指摘された CancellationToken の多数省略は全面的に改善されている。

最優先で対応すべきは以下の 3 点:
1. **spec.md とのエンティティ定義統一**（H-01, H-02）— SSOT 原則に基づき、どちらかを正として改訂する
2. **OrderPlugin の IDOR 対策**（H-05）— Semantic Kernel Function Calling での userId 安全注入メカニズムの設計
3. **ProductIndexSyncConsumer のキャプティブ依存修正**（H-03）— BackgroundService から Scoped サービスの安全な取得
