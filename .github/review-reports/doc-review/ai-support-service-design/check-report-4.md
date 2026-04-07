# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/ai-support-service-design.md`
- **判定**: ⚠️ **Conditional Approval** — High 指摘 1 件が残存（人間の判断を要する）
- **レビュー日時**: 2026-04-03 16:30 (イテレーション 4)
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

## 前回修正の検証結果

| # | 修正項目 | 検証結果 | 詳細 |
|---|---------|----------|------|
| H-01 | spec.md エンティティ対応表追加 | ✅ 修正済 | §5.0 に spec.md エンティティとの完全な対応表・Aggregate Root 変更理由・SSOT 宣言を追加。ADR-0006 を根拠として明示 |
| H-02 | spec.md インデックス不一致注記 | ✅ 修正済 | §5.0 末尾に spec.md インデックス定義の差異を明記し、spec.md 側の更新必要性を注記 |
| H-03 | ProductIndexSyncConsumer IServiceScopeFactory | ✅ 修正済 | §8.2 で `IServiceScopeFactory scopeFactory` をコンストラクタ注入し、`ExecuteAsync` 内で `scopeFactory.CreateScope()` + `GetRequiredService<Kernel>()` で Scoped 解決 |
| H-04 | FunctionChoiceBehavior.Auto() | ✅ 修正済 | §7.3 ChatService で `FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()` に更新。旧 API `ToolCallBehavior` を完全除去 |
| H-05 | OrderPlugin IDOR 防止（userId プリセット） | ⚠️ **一部不備** | §7.3 で `KernelArguments` に `userId` をプリセットするコードは追加されたが、`GetChatMessageContentsAsync` の呼び出しで `kernelArguments` ではなく `settings` が渡されており、プリセット値が実際の API 呼び出しに反映されない（**H-NEW-01 で再指摘**） |
| H-06 | ResponseFilter 漏洩対策強化 | ✅ 修正済 | §12.3 で複数パラフレーズキーワード検知・応答長切り捨て（4000 文字）を追加。§7.3 で `ResponseFilter.FilterResponse` の呼び出し箇所を明記 |
| H-07 | ForecastService FunctionChoiceBehavior.None() | ✅ 修正済 | §10.1 で `FunctionChoiceBehavior.None()` を明示指定し、kernel パラメータも追加。プラグイン不要の理由コメントを付記 |
| H-08 | Mermaid 図から未実装 ChatPlugin 削除 | ✅ 修正済 | §4.3 の Semantic Kernel アーキテクチャ図で `ChatPlugin` を削除し、`ProductPlugin`・`OrderPlugin`・`FaqPlugin` のみに修正 |
| H-09 | DataRetentionCleanupService browsing_history 除去 | ✅ 修正済 | §30.1 に `browsing_history_json` 30 日超過データ除去処理と `purchase_history_json` 月次匿名化集約ロジックを追加 |
| H-10 | JSONB プロパティに TypeName 追加 | ⚠️ **一部不備** | `UserProfile`（3 カラム）と `OutboxEvent.Payload` は修正済。しかし `Recommendation.ProductIdsJson`・`SearchAnalytics.ClickedProductIdsJson`・`ModelTraining.MetricsJson/ParametersJson`・`ChatSession.ContextJson`・`ChatMessage.MetadataJson` の 7 カラムが未修正（**M-NEW-01 で指摘**） |
| H-11 | Advisory Lock boolean 戻り値修正 | ✅ 修正済 | §30.1 で `SqlQueryRaw<bool>` + `FirstOrDefaultAsync` に変更。`ExecuteSqlInterpolatedAsync` の影響行数問題を解消 |

---

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
| business-analyst | PASS | 0 | 0 | 1 | 1 | 8/10 |
| architect | PASS | 0 | 0 | 2 | 0 | 8/10 |
| tech-lead | PASS | 0 | 0 | 0 | 0 | 9/10 |
| programing-reviewer | ⚠️ | 0 | 1 | 3 | 1 | 7/10 |
| security-reviewer | PASS | 0 | 0 | 2 | 0 | 8/10 |
| dba-reviewer | PASS | 0 | 0 | 2 | 1 | 8/10 |
| qa-manager | PASS | 0 | 0 | 2 | 0 | 8/10 |
| performance-reviewer | PASS | 0 | 0 | 3 | 1 | 8/10 |
| compliance-reviewer | PASS | 0 | 0 | 0 | 0 | 9/10 |
| oss-reviewer | PASS | 0 | 0 | 0 | 1 | 9/10 |
| release-manager | PASS | 0 | 0 | 0 | 1 | 9/10 |
| infra-ops-reviewer | PASS | 0 | 0 | 1 | 1 | 8/10 |
| audit-reviewer | PASS | 0 | 0 | 1 | 0 | 9/10 |
| ux-accessibility-reviewer | PASS | 0 | 0 | 1 | 0 | 9/10 |
| **合計** | | **0** | **1** | **18** | **7** | |

## 判定根拠
- 判定ルール適用結果: Critical 0 件、High 1 件 → **⚠️ Conditional Approval**（High 指摘のみ、人間の判断を介在）
- 最も重大な指摘: §7.3 ChatService の `KernelArguments` が `GetChatMessageContentsAsync` に渡されておらず、OrderPlugin IDOR 防止メカニズムが実質的に機能していない（H-05 修正の残存不具合）
- 前回 High 11 件 → 今回 High 1 件に大幅改善（10 件修正確認済、1 件に残存不備）

---

## Critical/High 指摘一覧（修正必須）

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------------|----------|----------|
| H-NEW-01 | **High** | programing-reviewer, security-reviewer | セキュリティ / コード品質 | §7.3 | §7.3 `ChatService.SendMessageAsync` で `KernelArguments` に `userId` をプリセットする IDOR 防止コードが追加されたが、直後の `chatCompletion.GetChatMessageContentsAsync(chatHistory, settings, kernel, ct)` で `kernelArguments` ではなく `settings`（`OpenAIPromptExecutionSettings` 型）が渡されている。`KernelArguments` 変数は生成後に使用されておらず、userId のプリセット値が Semantic Kernel の Function Calling に伝搬しない。加えて、`IChatCompletionService.GetChatMessageContentsAsync` の第 2 引数は `PromptExecutionSettings?` 型であり、`KernelArguments` 型を直接渡すことも API 上の互換性が不明確。**結果として、H-05 で指摘した IDOR リスクが依然として残存している**。 | **方式 A（推奨）**: `OrderPlugin` から `userId` パラメータを除去し、`IHttpContextAccessor` 経由で認証済み `ClaimsPrincipal` から userId を取得するよう設計変更する。Plugin 内部で認証情報を直接解決することで、AI による userId 指定の余地を完全に排除する。**方式 B**: `Kernel.InvokePromptAsync` + `KernelArguments` を使用する設計に変更し、Function Calling のパラメータオーバーライドが確実に機能することを保証する。いずれの方式でも、設計書に IDOR 防止メカニズムの動作原理を正確に記載すること。 |

---

## Medium/Low 指摘一覧（推奨改善事項）

### 新規指摘

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------------|----------|----------|
| M-NEW-01 | Medium | dba-reviewer | DB 設計 | §12d, §21, §22 | H-10 修正で `UserProfile` と `OutboxEvent` の JSONB カラムは修正されたが、以下の 7 カラムで `TypeName = "jsonb"` / `.HasColumnType("jsonb")` が未指定のまま: (1) `Recommendation.ProductIdsJson` (2) `SearchAnalytics.ClickedProductIdsJson` (3) `ModelTraining.MetricsJson` (4) `ModelTraining.ParametersJson` (5) `ChatSession.ContextJson` (6) `ChatMessage.MetadataJson` (7) `DemandForecast` には JSONB カラムなし（問題なし）。EF Core マイグレーション生成時に TEXT 型として作成されるリスクがある。 | 該当エンティティの `[Column]` 属性に `TypeName = "jsonb"` を追加し、§22 `OnModelCreating` でも `.HasColumnType("jsonb")` を追加する。 |
| M-NEW-02 | Medium | architect, programing-reviewer | DI 設計 | §32 | `IConsumer<string, string>` が Singleton として 1 インスタンスのみ登録されているが、`ProductIndexSyncConsumer` と `UserDeletionConsumer` の 2 つの BackgroundService で共有されている。Confluent.Kafka の `IConsumer` は `Subscribe()` 呼び出しが排他的（後からの Subscribe が前の購読を上書き）であり、2 つの BackgroundService が異なるトピック（`product.updated` / `user.deletion.requested`）に Subscribe すると、最後に Subscribe したトピックのみが有効になる。 | `IConsumer<string, string>` を Keyed Service（`.AddKeyedSingleton`）または Named 登録で分離し、各 BackgroundService に専用の Consumer インスタンスを注入する。例: `builder.Services.AddKeyedSingleton<IConsumer<string, string>>("product-sync", ...)` と `builder.Services.AddKeyedSingleton<IConsumer<string, string>>("user-deletion", ...)`。 |
| M-NEW-03 | Medium | performance-reviewer | パフォーマンス | §30.1 | `DataRetentionCleanupService` の browsing_history_json / purchase_history_json クリーンアップ処理が `context.UserProfiles.Where(p => p.BrowsingHistoryJson != null).ToListAsync()` で全対象プロファイルをメモリに読み込む。大規模ユーザーベース（100 万ユーザー超）ではメモリ枯渇リスクがある。 | バッチ処理化（`Take(1000)` + ループ）するか、PostgreSQL の JSONB 関数（`jsonb_array_elements`）を `ExecuteSqlInterpolatedAsync` で直接実行し、サーバーサイドでフィルタリングする設計に変更する。 |
| L-NEW-01 | Low | programing-reviewer | コード品質 | §9.2 | `RecommendationService.GetPersonalizedAsync` の `chatCompletion.GetChatMessageContentsAsync(chatHistory, cancellationToken: ct)` で `kernel` パラメータが省略されている。ForecastService（H-07）では `kernel` パラメータ追加と `FunctionChoiceBehavior.None()` 指定が修正されたが、RecommendationService は未修正。レコメンデーションではプラグイン呼び出し不要のため実影響は低いが、一貫性の観点で改善が望ましい。 | `FunctionChoiceBehavior.None()` を含む `PromptExecutionSettings` を定義し、`kernel` パラメータも追加して一貫性を確保する。 |

### 残存指摘（イテレーション 3 からの継続）

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------------|----------|----------|
| M-01 | Medium | programing-reviewer | コード品質 | §7.3 | `EstimateTokenCount` が `text.Length / 4` の概算。日本語では 1 文字 ≈ 1〜2 トークンであり精度不足 | `Microsoft.ML.Tokenizers` の `TiktokenTokenizer` 使用または日本語対応係数に変更 |
| M-02 | Medium | programing-reviewer | コード品質 | §9.2 | `AnonymizeUserPreferences` がハードコードダミー値を返している | JSONB パース処理の設計を記載するか `// TODO` コメントを明記 |
| M-03 | Medium | programing-reviewer | コード品質 | §25.2 | `[AsParameters]` + `[Required]` の自動バリデーション動作と FluentValidation の二重バリデーション懸念 | バリデーション戦略の役割分担を明確化 |
| M-04 | Medium | architect | 設計整合性 | §11.1, §32 | `USER_REGISTERED` イベントの Consumer（`UserRegisteredConsumer`）が未実装。§29 で `UserRegisteredEvent` record は定義済みだが、BackgroundService が存在しない | `UserRegisteredConsumer` を §30 に追加し、§32 DI 登録に `AddHostedService` を追加 |
| M-05 | Medium | architect | Bounded Context | §9.1 | 「よく一緒に購入」の共起分析に必要な注文明細データの同期方法が未定義 | Kafka `OrderCreated` イベントからローカル DB に蓄積するコンシューマー設計を追加 |
| M-06 | Medium | security-reviewer | セキュリティ | §12.6 | `SsrfPreventionHandler` が IPv4-mapped IPv6 アドレス（`::ffff:10.0.0.1`）を検出できない可能性 | `IPAddress.MapToIPv4()` で変換後にブロックリスト照合 |
| M-07 | Medium | security-reviewer | セキュリティ | §8.1 | `AllowedCategories` がハードコード静的 `HashSet<string>`。カテゴリマスタとの同期方法未定義 | Kafka イベントまたは Redis キャッシュ経由でカテゴリマスタを同期する設計を追加 |
| M-08 | Medium | dba-reviewer | DB 設計 | §5.2, §22 | `chat_sessions` テーブルの §5.2 定義に `user_id` インデックスが未記載（§22 Fluent API では定義あり） | §5.2 のインデックスセクションに追加し §22 と一致させる |
| M-10 | Medium | qa-manager | テスト | §33.2, §33.4 | `SearchServiceTests` と `ChatEndpointsIntegrationTests` が一部スケルトン | AAA パターンの完全な実装を追加 |
| M-11 | Medium | qa-manager | テスト | §15.2, §33 | トークン上限超過テスト・セッション上限超過テストが §33 に未実装 | §33.1 にテスト実装を追加 |
| M-12 | Medium | performance-reviewer | パフォーマンス | §8.1 | Azure AI Search `KNearestNeighborsCount = 50` が過剰。商品数 10,000 件以下では KNN=20〜30 で十分 | `AzureSearchSettings` に設定可能な KNN 値を追加 |
| M-13 | Medium | performance-reviewer | パフォーマンス | §9.2 | `RecommendationService.GetPersonalizedAsync` で AI 応答結果を商品検索に活用していない（`GetTopProductsAsync` で固定取得） | AI 応答のパース結果で `productClient` を条件付き呼び出しするロジックを設計 |
| M-14 | Medium | infra-ops-reviewer | インフラ | §17 | Dockerfile の `HEALTHCHECK` で `curl` を使用しているが `aspnet:10.0` に `curl` がプリインストールされていない | `wget --spider` または Azure Container Apps ヘルスプローブへの委譲 |
| M-15 | Medium | business-analyst | 機能要件 | §1, §6 | AI チャット解決率 60% の測定方法が未定義 | 判定基準・集計ロジック・API エンドポイントを追加 |
| M-16 | Medium | audit-reviewer | トレーサビリティ | §12b, §28 | `OutboxPublisher` のメッセージ発行時に Correlation ID がヘッダーに設定されていない | `correlation-id` ヘッダーを追加するか、`OutboxEvent` に `correlation_id` カラムを追加 |
| M-17 | Medium | ux-accessibility-reviewer | UX | §6.2, §7.3 | チャット API がストリーミング応答（SSE）に非対応。長い応答での体感レイテンシ大 | Phase 2 での SSE 導入計画を §20 に明記 |
| L-01 | Low | business-analyst | ドキュメント | §20 | AI チャット日本語限定と spec.md 多言語対応の Phase 整合性が不明確 | 両ドキュメントで Phase 定義を照合・明示 |
| L-03 | Low | dba-reviewer | DB 設計 | §5.2 | `confidence_score` DECIMAL(5,4) で 1.0 超の値が格納可能 | CHECK 制約 `confidence_score BETWEEN 0 AND 1` を追加 |
| L-04 | Low | performance-reviewer | パフォーマンス | §9.2 | レコメンデーション Redis TTL が固定 1 時間（タイプ別 TTL が望ましい） | タイプ別 TTL 設定を `AiLimitsSettings` に追加 |
| L-05 | Low | infra-ops-reviewer | インフラ | §18.3 | Bicep `minReplicas: 1` が ADR-0010（`minReplicas: 2`）と不整合 | `minReplicas` を 2 に変更 |
| L-06 | Low | release-manager | CI/CD | §19 | `dotnet-version: '10.0.x'` の SDK バージョン確認が必要 | .NET 10 LTS リリース後に確認・更新 |
| L-07 | Low | oss-reviewer | 依存関係 | §2 | `Azure.Search.Documents` 11.x と `Microsoft.SemanticKernel.Connectors.AzureOpenAI` の .NET 10 互換性未確認 | 実装フェーズ開始時に互換性を確認 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-01 | 高優先 | programing-reviewer, security-reviewer | OrderPlugin IDOR 防止の実装方式を確定する必要がある（H-NEW-01）。方式 A（Plugin 内で IHttpContextAccessor 使用）と方式 B（Kernel.InvokePromptAsync + KernelArguments）の選択は Semantic Kernel の API 仕様に依存するため、実装チームの技術検証が必要。 | テックリード |
| E-02 | 通常 | architect, programing-reviewer | §32 の Kafka Consumer Singleton 共有問題（M-NEW-02）。Keyed Service パターンへの移行は DI 設計全体に影響するため、実装フェーズ早期に対応方針を確定する必要がある。 | テックリード |

---

## 競合解決記録

本レビューでは Agent 間の競合は検出されなかった。

---

## ドキュメント横断分析

### spec.md との整合性チェック

| チェック項目 | 結果 | 詳細 |
|------------|------|------|
| Aggregate Root 定義 | ✅ 整合（注記付き） | §5.0 で spec.md との差異を対応表として明示。設計書を SSOT とし、spec.md 更新計画を注記 |
| エンティティ一覧 | ✅ 整合（注記付き） | §5.0 で spec.md 6 エンティティと設計書 8 エンティティの完全マッピングを記載 |
| インデックス定義 | ✅ 整合（注記付き） | §5.0 で spec.md の `user_interactions` / `recommendation_logs` テーブル参照が設計書に不存在であることを明示 |
| Kafka イベント定義 | ✅ 整合 | `user.deleted`, `user.profile-updated`, `consent.revoked` の購読設計あり |
| GDPR データ保持期間 | ✅ 整合 | チャット 90 日、browsing_history 30 日の保持・クリーンアップが §12.7 + §30.1 で完全実装 |
| ポート番号 | ✅ 整合 | 5009 で一致 |
| 技術スタック | ✅ 整合 | C# 14, .NET 10, Semantic Kernel 1.x, PostgreSQL 全て一致 |
| KPI「AI チャット解決率 60%」 | ⚠️ 部分対応 | 設計書に測定ロジックの詳細が未定義（M-15） |
| DPIA | ✅ 整合 | spec.md §DPIA 計画と設計書 §12.8 が整合 |
| SSRF 防止設計 | ✅ 整合 | spec.md §SSRF 防止の要件を設計書 §12.6 で多層防御実装 |
| スケーリング設計 | ⚠️ 部分整合 | spec.md: minReplicas=2 vs 設計書 Bicep: minReplicas=1（L-05） |

### 設計書の完成度評価（前回比較）

| セクション | 充実度 | 前回比 | 備考 |
|-----------|--------|--------|------|
| 概要・スコープ | ★★★★★ | → | 明確 |
| 技術スタック | ★★★★★ | → | AGENTS.md と整合 |
| システムアーキテクチャ | ★★★★★ | ↑ | H-08 修正により Mermaid 図がコードと一致 |
| データモデル | ★★★★☆ | ↑ | H-01/H-02 で spec.md 対応表追加。残存 JSONB 未指定（M-NEW-01） |
| API 設計 | ★★★★★ | → | 網羅的 |
| Semantic Kernel 統合 | ★★★★☆ | ↑ | H-04 修正。KernelArguments 使用箇所に不備（H-NEW-01） |
| 検索設計 | ★★★★★ | → | ハイブリッド検索 + OData インジェクション対策完備 |
| レコメンデーション | ★★★☆☆ | → | AI 応答結果の未活用（M-13）は残存 |
| イベント設計 | ★★★★☆ | → | UserRegisteredConsumer 未実装（M-04）は残存 |
| セキュリティ設計 | ★★★★☆ | ↑ | H-06 修正でフィルタ強化。IDOR 修正に残存不備（H-NEW-01） |
| 耐障害性 | ★★★★★ | → | フォールバック戦略が明確 |
| テスト戦略 | ★★★★☆ | → | 一部スケルトン（M-10） |
| Program.cs / DI 設計 | ★★★★☆ | ↓ | Kafka Consumer 共有問題（M-NEW-02）を新規検出 |
| インフラ（Bicep / CI/CD） | ★★★★☆ | → | minReplicas 不整合（L-05）は残存 |
| エンティティ定義 | ★★★★☆ | ↑ | H-10 部分修正。残存 JSONB 7 カラム（M-NEW-01） |
| Outbox パターン | ★★★★★ | → | 動的バックオフ完全実装 |
| GDPR | ★★★★★ | ↑ | H-09 修正で browsing_history + purchase_history クリーンアップ完備 |
| DataRetentionCleanup | ★★★★☆ | ↑ | H-09/H-11 修正。メモリ読み込み問題（M-NEW-03）を新規検出 |

### 改善トレンド

| イテレーション | Critical | High | Medium | Low | 判定 |
|-------------|----------|------|--------|-----|------|
| 1 | 0 | 15+ | — | — | ❌ Rejected |
| 2 | 0 | 14 | — | — | ⚠️ Conditional |
| 3 | 0 | 11 | 17 | 7 | ⚠️ Conditional |
| **4（今回）** | **0** | **1** | **18** | **7** | **⚠️ Conditional** |

High 指摘: 11 → 1（**10 件修正確認、1 件に残存不備**）

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### ビジネス要件の完全性

**評価: 8/10**（前回 7/10 → 改善）

#### 強み
- スコープ定義が明確で、4 つの主要機能の要件が具体的
- KPI「AI チャット解決率 60%」への対応としてエスカレーション機能設計あり
- フィードバック API によるアルゴリズム改善ループの設計あり
- spec.md エンティティ対応表（§5.0）により SSOT が明確化された

#### 残存指摘
| 重要度 | 指摘 |
|--------|------|
| Medium | AI チャット解決率 60% の測定方法が未定義（M-15 継続） |
| Low | spec.md の多言語対応 Phase 定義との整合性が不明確（L-01 継続） |

</details>

<details>
<summary>architect レビューレポート</summary>

### アーキテクチャ評価

**評価: 8/10**（前回 6/10 → 大幅改善）

#### 改善点
- H-01: spec.md エンティティ対応表追加により Bounded Context の乖離が明確化
- H-02: インデックス不整合の注記追加
- H-08: Mermaid 図とコード実装のプラグイン名称が一致

#### 残存指摘
| 重要度 | 指摘 |
|--------|------|
| Medium | UserRegisteredConsumer が未実装（M-04 継続）。§29 で record 定義済みだが BackgroundService なし |
| Medium | 共起分析データ同期方法が未定義（M-05 継続） |

#### 新規指摘
| 重要度 | 指摘 |
|--------|------|
| Medium | Kafka Consumer Singleton 共有問題（M-NEW-02） |

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 技術標準適合性

**評価: 9/10**（前回 7/10 → 大幅改善）

#### 改善点
- H-07: ForecastService に `FunctionChoiceBehavior.None()` と `kernel` パラメータを追加。規約準拠化
- H-01/H-02: spec.md との整合性問題を対応表で解決
- H-04: Semantic Kernel API の最新仕様に更新

#### 評価
AGENTS.md / Instructions の規約準拠度は全体的に高い水準に達している。前回指摘した ForecastService のパラメータ一貫性は修正済み。CancellationToken の伝搬も適切。H-NEW-01 の IDOR 修正残存は programing-reviewer / security-reviewer が担当する技術的な API 設計問題であり、tech-lead 観点では指摘なし。

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### コード品質評価

**評価: 7/10**（前回 6/10 → 改善）

#### 改善点
- H-03: ProductIndexSyncConsumer のキャプティブ依存を IServiceScopeFactory 経由に修正
- H-04: FunctionChoiceBehavior.Auto() に更新
- H-11: Advisory Lock の boolean 戻り値取得を正しい API に修正

#### 新規/残存 High 指摘
| 重要度 | 指摘 |
|--------|------|
| **High** | §7.3 `kernelArguments` が `GetChatMessageContentsAsync` に渡されていない（H-NEW-01）。IDOR 防止コードが死コード化 |

#### 残存 Medium 指摘
| 重要度 | 指摘 |
|--------|------|
| Medium | `EstimateTokenCount` のダミー実装（M-01 継続） |
| Medium | `AnonymizeUserPreferences` のハードコードダミー値（M-02 継続） |
| Medium | `[AsParameters]` + `[Required]` と FluentValidation の二重バリデーション（M-03 継続） |

#### 新規 Low 指摘
| 重要度 | 指摘 |
|--------|------|
| Low | `RecommendationService` で `kernel` パラメータ省略（L-NEW-01） |

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### セキュリティ評価

**評価: 8/10**（前回 7/10 → 改善）

#### 改善点
- H-05: IDOR 防止の設計意図が明確化（KernelArguments + userId プリセット + OrderPlugin ドキュメント）。ただし実装コードに不備あり（H-NEW-01 に包含）
- H-06: ResponseFilter の漏洩対策が多層化（パラフレーズキーワード + 応答長制限 + 呼び出し箇所明記）

#### 残存 Medium 指摘
| 重要度 | 指摘 |
|--------|------|
| Medium | SSRF 防止の IPv4-mapped IPv6 検出漏れ（M-06 継続） |
| Medium | AllowedCategories の静的ハードコード（M-07 継続） |

#### 総合評価
セキュリティ設計は多層防御が機能しており、プロンプトインジェクション対策（5 層）・SSRF 防止・レート制限・GDPR 対応が充実している。H-NEW-01 の IDOR 問題が修正されれば、セキュリティ観点での High 指摘はゼロとなる。

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### DB 設計評価

**評価: 8/10**（前回 7/10 → 改善）

#### 改善点
- H-10: UserProfile の JSONB カラム 3 件 + OutboxEvent.Payload に TypeName = "jsonb" を追加。OnModelCreating にも HasColumnType 追加済み
- H-11: Advisory Lock の戻り値取得を SqlQueryRaw<bool> に修正

#### 新規指摘
| 重要度 | 指摘 |
|--------|------|
| Medium | JSONB TypeName 未指定が 7 カラム残存（M-NEW-01） |

#### 残存指摘
| 重要度 | 指摘 |
|--------|------|
| Medium | chat_sessions の user_id インデックスが §5.2 と §22 で不整合（M-08 継続） |
| Low | confidence_score の DECIMAL(5,4) 範囲制約不足（L-03 継続） |

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### テスト戦略評価

**評価: 8/10**（前回と同等）

#### 残存指摘
| 重要度 | 指摘 |
|--------|------|
| Medium | SearchServiceTests / ChatEndpointsIntegrationTests のスケルトン（M-10 継続） |
| Medium | トークン/セッション上限超過テストが §33 に未実装（M-11 継続） |

#### 評価
テスト戦略の構造（Unit / Integration / DB / AI 品質テスト）は適切。AAA パターン・命名規約（Should_X_When_Y）は準拠済み。スケルトンテストの完成が実装フェーズでの品質担保に必要。

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### パフォーマンス評価

**評価: 8/10**（前回と同等）

#### 新規指摘
| 重要度 | 指摘 |
|--------|------|
| Medium | DataRetentionCleanupService のメモリ読み込み問題（M-NEW-03） |

#### 残存指摘
| 重要度 | 指摘 |
|--------|------|
| Medium | KNearestNeighborsCount=50 過剰（M-12 継続） |
| Medium | AI 応答結果の商品検索未活用（M-13 継続） |
| Low | レコメンデーション TTL タイプ別設定（L-04 継続） |

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### GDPR / 法規制適合性評価

**評価: 9/10**（前回 8/10 → 改善）

#### 改善点
- H-09: DataRetentionCleanupService に browsing_history_json（30 日）と purchase_history_json（年次匿名化集約）のクリーンアップを追加。§12.7 のデータ保持ポリシーとの整合性が確保された
- spec.md との GDPR データ保持期間の整合性が §5.0 の対応表で明確化

#### 評価
GDPR Art.5(1)(c)（データ最小化）、Art.5(1)(e)（保存期間制限）、Art.17（削除権）への対応が設計書レベルで完備。DPIA 参照（§12.8）、同意管理（PERSONALIZATION オプトアウト）も適切。指摘なし。

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 依存関係評価

**評価: 9/10**（前回と同等）

#### 残存指摘
| 重要度 | 指摘 |
|--------|------|
| Low | Azure.Search.Documents 11.x / SemanticKernel.Connectors.AzureOpenAI の .NET 10 互換性未確認（L-07 継続） |

#### 評価
NuGet パッケージは全て GA バージョン・AGENTS.md 準拠。禁止パッケージ（Newtonsoft.Json / log4net 等）の使用なし。Azure.Identity の追加も適切。

</details>

<details>
<summary>release-manager レビューレポート</summary>

### リリース戦略評価

**評価: 9/10**（前回と同等）

#### 残存指摘
| 重要度 | 指摘 |
|--------|------|
| Low | CI/CD の dotnet-version 確認（L-06 継続） |

#### 評価
CI/CD パイプライン（GitHub Actions）は build → test → deploy の標準フロー。マルチステージ Docker ビルド、ACR 連携、Container Apps デプロイが適切に設計されている。

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### インフラ・運用評価

**評価: 8/10**（前回と同等）

#### 残存指摘
| 重要度 | 指摘 |
|--------|------|
| Medium | Dockerfile HEALTHCHECK の curl 問題（M-14 継続） |
| Low | Bicep minReplicas 不整合（L-05 継続） |

#### 評価
Azure Container Apps + Managed Identity + RBAC の設計が充実。OpenTelemetry、ヘルスチェック、Serilog の可観測性設計は AGENTS.md §11 準拠。

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 監査・トレーサビリティ評価

**評価: 9/10**（前回と同等）

#### 残存指摘
| 重要度 | 指摘 |
|--------|------|
| Medium | OutboxPublisher の Correlation ID 未伝搬（M-16 継続） |

#### 評価
Correlation ID 設計（§12b）、構造化ログ（Serilog + メッセージテンプレート）、OpenTelemetry 分散トレーシングは適切。Outbox イベント経由の Correlation ID 伝搬が改善されれば、エンドツーエンドのトレーサビリティが完全となる。

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### UX / アクセシビリティ評価

**評価: 9/10**（前回と同等）

#### 残存指摘
| 重要度 | 指摘 |
|--------|------|
| Medium | ストリーミング応答（SSE）未対応（M-17 継続） |

#### 評価
チャットボットの基本 UX フロー（セッション管理、エスカレーション、検索サジェスト）は適切に設計されている。Phase 2 での SSE 導入計画が §20 に明記されれば十分。

</details>
