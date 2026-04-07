# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/ai-support-service-design.md`（AiSupportService 詳細設計書 — 修正後イテレーション 2）
- **判定**: ⚠️ **Conditional Approval** — High 指摘あり、人間の判断を介在
- **レビュー日時**: 2026-04-03 14:30
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)
- **前回レビュー**: check-report-1.md（❌ Rejected: 4 Critical, 21 High）
- **修正レポート**: fix-report-1.md（4C 修正済, 17H 修正済, +688 行）

## 段階的実行モード
- イテレーション: 2 回目
- 実行 Agent: 全 14 Agent（初期〜3 回目は全量実行）
- スキップ Agent: なし
- 実行理由: 全量実行（イテレーション 3 回目以内）

## 前回指摘の修正検証結果

### Critical 修正（4/4 件 ✅ 全件解消確認済み）

| # | 指摘 | 修正内容 | 検証結果 |
|---|------|---------|----------|
| C-1 | SSRF 防止設計の完全欠如 | §12.6 新設: URL ホワイトリスト(5 ドメイン)、プライベート IP 拒否(8 CIDR)、DNS リバインディング対策、`SsrfPreventionHandler` 実装コード | ✅ 解消 |
| C-2 | OData フィルタインジェクション | §8.1: `AllowedCategories` HashSet ホワイトリスト検証 + `SearchRequest` DTO に `[RegularExpression]` 追加 | ✅ 解消 |
| C-3 | API キー直接取得パターン | §7.1: `DefaultAzureCredential` に変更、`!` → `?? throw new InvalidOperationException()`、`Azure.Identity` 追加 | ✅ 解消 |
| C-4 | ユーザー購入履歴の AI プロンプト直接埋め込み | §9.2: `AnonymizeUserPreferences` メソッド追加、匿名化サマリのみ AI に送信 | ✅ 解消 |

### High 修正（17/17 件 ✅ 全件解消確認済み）

| # | 指摘 | 修正箇所 | 検証結果 |
|---|------|---------|----------|
| H-5 | プロンプトインジェクション（ブラックリスト） | §12.3: 5 層多層防御に改訂 | ✅ 解消 |
| H-6 | Polly レジリエンス未設計 | §12a.1: `AddStandardResilienceHandler` 設定追加 | ✅ 解消（ただし新規 High #1 参照） |
| H-7 | ミドルウェアパイプライン欠如 | §12c: AGENTS.md §11.3 準拠の登録順序追加 | ✅ 解消 |
| H-8 | レート制限未設計 | §12.5: 4 種類のレート制限定義追加 | ✅ 解消（ただし新規 Medium #3 参照） |
| H-9 | GDPR データ保持期間未定義 | §12.7: 6 テーブル保持期間 + Art.17 削除フロー + BackgroundService | ✅ 解消 |
| H-10 | DPIA 計画未反映 | §12.8: 4 軽減措置の実装設計追加 | ✅ 解消 |
| H-11 | Correlation ID 欠如 | §12b: 伝搬設計 + ミドルウェアコード追加 | ✅ 解消 |
| H-12 | EF Core エンティティ属性欠如 | §12d: 4 エンティティに `[Table]`/`[Column]`/`[Key]`/`[MaxLength]` 追加 | ✅ 解消 |
| H-13 | `created_by`/`updated_by` 監査カラム欠如 | §5.2: `model_trainings` に `created_by` 追加 + 他テーブル省略理由注記 | ✅ 解消 |
| H-14 | Kernel Singleton 登録 | §7.1: `AddSingleton` → `AddScoped` に変更 | ✅ 解消 |
| H-15 | 会話履歴の全件取得 | §7.3: `FindRecentBySessionIdAsync(maxMessages: 20)` スライディングウィンドウ | ✅ 解消 |
| H-16 | ProductPlugin 従来コンストラクタ | §7.2: primary constructor パターンに変更 | ✅ 解消 |
| H-17 | テストカバレッジ目標不足 | §15.2: 80% 目標 + AI 品質基準 + Service 必須テストケース一覧 | ✅ 解消 |

---

## 技術スタック検証結果
| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| ランタイム | .NET 10 (LTS) | .NET 10 (LTS) | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| AI フレームワーク | Semantic Kernel 1.x | Semantic Kernel 1.x | ✅ |
| ORM | EF Core 10 | EF Core 10 | ✅ |
| DB | PostgreSQL (aisupportdb) | PostgreSQL (サービス別独立 DB) | ✅ |
| メッセージング | Confluent.Kafka 2.* | Confluent.Kafka 2.* | ✅ |
| キャッシュ | StackExchange.Redis 2.* | StackExchange.Redis 2.* | ✅ |
| 耐障害性 | Polly 8.* + Microsoft.Extensions.Http.Resilience 9.* | Polly 8.* + Http.Resilience 9.* | ✅ |
| ログ | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| テスト | xUnit / NSubstitute / Shouldly | xUnit / NSubstitute / Shouldly | ✅ |
| 認証 | DefaultAzureCredential (Azure.Identity) | Azure.Identity + Managed Identity | ✅ |
| ベクトル DB | Azure AI Search | spec.md: Qdrant 等 | ⚠️ 差異あり（Low #3） |

---

## 指摘サマリー
| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ✅ Approved with Notes | 0 | 0 | 0 | 1 |
| architect | ⚠️ Conditional | 0 | 2 | 1 | 0 |
| programing-reviewer | ⚠️ Conditional | 0 | 1 | 2 | 0 |
| dba-reviewer | ⚠️ Conditional | 0 | 1 | 1 | 0 |
| security-reviewer | ⚠️ Conditional | 0 | 1 | 2 | 0 |
| compliance-reviewer | ✅ Approved with Notes | 0 | 0 | 0 | 0 |
| audit-reviewer | ✅ Approved with Notes | 0 | 0 | 1 | 0 |
| qa-manager | ✅ Approved | 0 | 0 | 0 | 0 |
| performance-reviewer | ✅ Approved with Notes | 0 | 0 | 0 | 1 |
| infra-ops-reviewer | ⚠️ Conditional | 0 | 1 | 1 | 0 |
| release-manager | ✅ Approved with Notes | 0 | 0 | 0 | 1 |
| oss-reviewer | ✅ Approved | 0 | 0 | 0 | 0 |
| ux-accessibility-reviewer | ✅ Approved with Notes | 0 | 0 | 0 | 1 |
| tech-lead | ⚠️ Conditional | 0 | 1 | 1 | 1 |
| **合計** | | **0** | **7** | **9** | **5** |

## 判定根拠
- **判定ルール適用結果**: Critical 0 件、High 7 件 → ⚠️ Conditional Approval（高リスク指摘の受容/是正は人間が判断）
- **前回比**: Critical 4→0（-4）、High 21→7（-14）、全体 53→21（-32） — **大幅改善**
- **最も重大な指摘**: Semantic Kernel の内部 HTTP クライアントがレジリエンスパイプラインを迂回する設計ギャップ、Bicep テンプレートと Managed Identity アプローチの矛盾

---

## Critical/High 指摘一覧（修正必須）

| # | 重要度 | 出典 Agent | カテゴリ | 対象箇所 | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|---------|----------|----------|
| 1 | **High** | `architect`, `tech-lead` | アーキテクチャ / 耐障害性 | §7.1 vs §12a.1 | **Semantic Kernel の内部 HTTP クライアントがレジリエンスパイプラインを迂回する**。§12a.1 で Named HttpClient（`"AzureOpenAI"`, `"AzureSearch"`）に `AddStandardResilienceHandler` を設定しているが、§7.1 の `kernelBuilder.AddAzureOpenAIChatCompletion()` は Semantic Kernel 内部の HTTP クライアントを使用する。Named HttpClient に設定したリトライ・サーキットブレーカー・タイムアウトが実際の Azure OpenAI 呼び出しに適用されない | §7.1 の Kernel セットアップで、`IHttpClientFactory` から取得したレジリエンス設定済み `HttpClient` を Semantic Kernel のコネクタに渡す設計に変更する。`kernelBuilder.AddAzureOpenAIChatCompletion(deploymentName, endpoint, credentials, httpClient: sp.GetRequiredService<IHttpClientFactory>().CreateClient("AzureOpenAI"))` のパターンを採用するか、Semantic Kernel の `KernelBuilder.Services` に HttpClientFactory を登録する方法を設計すること |
| 2 | **High** | `infra-ops-reviewer`, `security-reviewer` | インフラ / 秘密情報管理 | §18.3 Bicep — Container Apps 定義 | **Bicep テンプレートが API キーベースのまま**で、§7.1 の `DefaultAzureCredential`（Managed Identity）アプローチと矛盾する。`secrets` に `openAi.listKeys().key1` と `searchService.listAdminKeys().primaryKey` を設定し、環境変数 `AzureOpenAI__ApiKey`/`AzureSearch__ApiKey` で渡しているが、コードは `DefaultAzureCredential` を使用する。また Container App への Managed Identity 割当と RBAC ロール付与が欠如している | ① Bicep の `secrets` セクションから API キー関連を削除 ② Container App に System-assigned Managed Identity を有効化 ③ `Cognitive Services OpenAI User` ロールと `Search Index Data Reader` ロールの RBAC 割当を追加 ④ 環境変数 `AzureOpenAI__ApiKey`/`AzureSearch__ApiKey` を削除 |
| 3 | **High** | `architect`, `dba-reviewer` | アーキテクチャ / ADR-0005 準拠 | §5.2 テーブル定義、§11.2 発行イベント | **`outbox_events` テーブルが未定義**。§11.2 で `RecommendationGenerated`、`ChatSessionEscalated`、`ForecastGenerated` の 3 イベントを Kafka 発行すると定義しているが、ADR-0005（Outbox パターンによるイベント発行保証）に必要な `outbox_events` テーブルが §5.2 に存在しない。イベントの DB トランザクション原子性が保証されない | ① §5.2 に `outbox_events` テーブルを追加（AGENTS.md §10.4 準拠: `[Table("outbox_events")]`、ステータス `PENDING`/`PUBLISHED`/`FAILED`） ② `OutboxPublisher` BackgroundService の設計を追加（動的バックオフ 100ms〜5s） ③ イベント発行を Outbox 経由に変更 ④ §12d に `OutboxEvent` エンティティ定義を追加 |
| 4 | **High** | `programing-reviewer`, `architect` | アーキテクチャ / DI | §7.1 Kernel セットアップ L543-545 | **Kernel プラグインの DI 解決ギャップ**。`kernelBuilder.Plugins.AddFromType<ProductPlugin>()` は Kernel 内部の ServiceProvider からプラグインを解決するが、`ProductPlugin` が依存する `IProductClient` はホスト ApplicationServices にのみ登録されている。Kernel の内部 ServiceCollection に `IProductClient` は未登録のため、プラグイン解決時に `InvalidOperationException` が発生する可能性がある | ① `kernelBuilder.Plugins.AddFromObject(new ProductPlugin(sp.GetRequiredService<IProductClient>()))` で事前解決したインスタンスを登録する方式に変更 ② または `kernelBuilder.Services.AddScoped<IProductClient>(sp2 => sp.GetRequiredService<IProductClient>())` で Kernel の ServiceCollection にもホストのサービスを橋渡し登録する |
| 5 | **High** | `security-reviewer` | セキュリティ / SSRF | §12.6 `SsrfPreventionHandler` コード L1315-1320 | **SSRF ブロックリストから IPv6 アドレス範囲が欠落**。§12.6 のテーブルには `::1/128`（IPv6 ループバック）、`fc00::/7`（IPv6 ユニークローカル）、`0.0.0.0/8`（現在のネットワーク）が記載されているが、C# コードの `BlockedNetworks` 配列には IPv4 の 5 エントリのみ含まれ、IPv6 と `0.0.0.0/8` が欠落している。IPv6 ホストを経由した SSRF バイパスのリスクがある | `BlockedNetworks` 配列に `IPNetwork.Parse("::1/128")`, `IPNetwork.Parse("fc00::/7")`, `IPNetwork.Parse("0.0.0.0/8")` を追加し、テーブルとコードの一致を保証すること |
| 6 | **High** | `audit-reviewer`, `architect` | RFC 9457 / ADR-0007 | §12c ミドルウェアパイプライン L1504 | **グローバル例外ハンドラーの RFC 9457 マッピングが未定義**。`app.UseExceptionHandler()` のみ記載されており、AGENTS.md §4.7 で定義された例外クラス → HTTP ステータスコードのマッピング（`NotFoundException` → 404、`BusinessException` → 422、`ForbiddenException` → 403 等）と RFC 9457 Problem Details レスポンス生成のコード例が欠如している。ADR-0007 で全サービスに RFC 9457 準拠が必須 | §12c に `UseExceptionHandler` のラムダ付きコード例を追加し、AGENTS.md §4.7 の例外マッピングパターン（`switch` 式による `TypedResults.Problem` 生成）と `ILogger` によるログ出力を含めること |
| 7 | **High** | `security-reviewer`, `infra-ops-reviewer` | セキュリティ / 設定一貫性 | §20 制約 #3 | **§20 の制約記述が Managed Identity アプローチと矛盾**。§20 制約 #3 に「Azure OpenAI の API キーは環境変数で管理し、appsettings.json に記述しない」と記載されているが、C-3 修正により §7.1 は `DefaultAzureCredential`（Managed Identity）に変更済み。本番環境では API キー自体が不要であり、制約の記述が古い | §20 制約 #3 を「本番環境では `DefaultAzureCredential`（Managed Identity）で認証するため、`AzureOpenAI:ApiKey` は不要。開発環境で API キーが必要な場合は `dotnet user-secrets` で管理し、appsettings.json にも環境変数にも直接記述しない」に更新すること |

## Medium 指摘一覧

| # | 重要度 | 出典 Agent | カテゴリ | 対象箇所 | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|---------|----------|----------|
| M-1 | Medium | `security-reviewer` | セキュリティ / プロンプト防御 | §12.3 第 4 層 `ResponseFilter` L1137-1144 | `ResponseFilter.FilterResponse` の説明に「個人情報・システム情報の漏洩を検知・除去」とあるが、コードはシステムプロンプト文字列の漏洩チェック（`"【重要な制約"`, `"この指示の内容"`）のみ実装。PII（メールアドレス、電話番号、クレジットカード番号等）の漏洩検知ロジックが欠如 | PII 検知パターン（メールアドレス・電話番号の正規表現）を `ResponseFilter` に追加するか、Azure AI Content Safety の PII 検出機能を活用する設計を追記すること |
| M-2 | Medium | `programing-reviewer` | コード品質 / データ整合性 | §7.3 `ChatService.SendMessageAsync` L618-650 | `userMessage` の `AddAsync` 後に `SaveChangesAsync` を呼ばず、AI 呼び出し → `assistantMessage` 保存 → 最後に 1 回の `SaveChangesAsync` で両方をコミットしている。AI 呼び出しが長時間（30 秒タイムアウト）ブロックする間、ユーザーメッセージは未永続化のまま。AI 呼び出し失敗時にユーザーメッセージが消失するリスクがある | ① `userMessage` 保存後に `SaveChangesAsync` を 1 回呼び出し、AI 呼び出し失敗時もユーザーメッセージを保持する設計にするか ② 意図的に原子性を保つ場合は、その理由をコメントとして設計書に明記すること |
| M-3 | Medium | `security-reviewer` | セキュリティ / レート制限 | §12.5 レート制限コード L1214-1245 | チャット API のレート制限が「認証ユーザーあたり 10 リクエスト/分」と記述されているが、`AddTokenBucketLimiter("chat-api", ...)` コードにユーザー単位の `PartitionKey` 設定が含まれていない。現在のコードではグローバルなバケットとなり、全ユーザーで共有される。`AddTokenBucketLimiter` → `AddPolicy` + `RateLimitPartition.GetTokenBucketLimiter` でユーザー ID ベースのパーティション設定が必要 | `builder.Services.AddRateLimiter(options => { options.AddPolicy("chat-api", context => RateLimitPartition.GetTokenBucketLimiter(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous", _ => new TokenBucketRateLimiterOptions { ... })); })` のパターンに変更すること |
| M-4 | Medium | `dba-reviewer` | DB 設計 / FK 制約 | §5.2 `chat_messages` テーブル / §12.7 クリーンアップ | `chat_messages.session_id` の FK 制約に `ON DELETE CASCADE` が明示されていない。§12.7 の `DataRetentionCleanupService` は `ChatSessions` に対して `ExecuteDeleteAsync` を実行するが、これは EF Core の変更追跡を迂回するため、FK の CASCADE 設定がない限り、参照整合性エラーまたは孤立した `chat_messages` レコードが発生する | ① §5.2 の FK 定義に `ON DELETE CASCADE` を明記する ② `DataRetentionCleanupService` で `ChatMessages` を先に削除してから `ChatSessions` を削除する設計にするか、両方の対策を講じること |
| M-5 | Medium | `architect` | アーキテクチャ / DDD | 設計書全体 | **Aggregate Root が明示的に定義されていない**。AGENTS.md §3.1 で各マイクロサービスに 1 つ以上の Aggregate Root の定義が必須とされているが、AiSupportService の設計書にはどのエンティティが Aggregate Root であるかの記述がない。`ChatSession`（`ChatMessage` を子エンティティとして包含）と `UserProfile` が Aggregate Root の候補 | §5 データモデルまたは §12d エンティティ定義に Aggregate Root の指定を追加し、`ChatMessage` は `ChatSession` Aggregate Root 経由でのみ操作されることを明記すること |
| M-6 | Medium | `programing-reviewer` | コード品質 / EF Core | §12d エンティティ定義 | `UserProfile.ChatSessions` ナビゲーションプロパティが `ChatSession.UserId` → `UserProfile.UserId` の関係を暗示するが、`UserProfile.UserId` は PK ではなく UNIQUE 制約カラムであり、EF Core の規約ベースの FK 解決では正しく関連付けられない可能性がある。`OnModelCreating` での Fluent API 設定（`HasMany/WithOne` + `HasForeignKey` + `HasPrincipalKey`）が必要だが設計書に記載がない | ナビゲーション関係の EF Core 設定方針を追記するか、`AppDbContext.OnModelCreating` のコード例を追加すること |
| M-7 | Medium | `tech-lead` | 設計一貫性 | §12.7 `DataRetentionCleanupService` L1374-1395 | クリーンアップ BackgroundService のコード例が `ChatSessions` の削除のみ実装しており、§12.7 の保持ポリシーテーブルに定義された他の 5 テーブル（`browsing_history_json` 30 日、`purchase_history_json` 1 年、`search_analytics` 180 日、`recommendations` expires_at+30 日、`demand_forecasts` 1 年）のクリーンアップロジックが示されていない | コード例に全テーブルのクリーンアップロジックを含めるか、「以下は ChatSessions の例。全テーブルのクリーンアップは同様のパターンで実装する」旨のコメントを添えること |
| M-8 | Medium | `infra-ops-reviewer` | インフラ / Bicep | §18.3 Container Apps L2113 | Bicep テンプレートの `image` フィールドで `'${containerRegistry.properties.loginServer}/ai-support-service:latest'` を使用している。`latest` タグは `dockerfile-infra.instructions.md` §2 で明確に禁止されている | `latest` → `${containerImageTag}` パラメータに変更し、CI/CD パイプラインで `${{ github.sha }}` 等の一意タグを渡す設計にすること |
| M-9 | Medium | `security-reviewer` | セキュリティ / 入力検証 | §6.6 DTO / §12.3 | `SendMessageRequest` の `[StringLength(4000)]` でメッセージ長を制限しているが、`CreateChatSessionRequest.InitialMessage` は `string?` で長さ制限がない。初期メッセージも 4000 文字制限を適用すべき | `CreateChatSessionRequest` にも `[StringLength(4000)]` を追加すること |

## Low 指摘一覧

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 |
|---|--------|-----------|---------|----------|
| L-1 | Low | `ux-accessibility-reviewer` | UX / レスポンス体験 | チャット AI の応答遅延時のユーザー体験（ストリーミング応答 / Server-Sent Events）が未検討。Azure OpenAI は SSE 対応だが、バックエンド設計に反映されていない |
| L-2 | Low | `release-manager` | リリース / AI モデル | Azure OpenAI モデルバージョン更新時（GPT-4o のバージョン変更等）のリリース / ロールバック手順が AI デプロイメントとアプリケーションデプロイメントで分離されていない |
| L-3 | Low | `tech-lead` | 技術選定 / ADR | spec.md のベクトル DB「Qdrant 等」から Azure AI Search への変更が ADR として記録されていない |
| L-4 | Low | `business-analyst` | ビジネス / KPI | レコメンデーション機能のビジネス KPI（CVR/AOV 向上率等）と成功指標の定量的定義が不足 |
| L-5 | Low | `performance-reviewer` | パフォーマンス / トークン | `EstimateTokenCount` の `text.Length / 4` 概算は不正確。tiktoken 等の正規トークナイザーの採用を検討すべき |

---

## エスカレーション事項（要人間判断）
| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| 1 | 最優先 | `compliance-reviewer` | DPIA（§12.8 参照）が開発フェーズ開始前に完了済みか確認が必要。§12.8 に「開発フェーズ開始前（設計段階で完了必須）」と記載あり | プライバシー責任者 / DPO |
| 2 | 高優先 | `architect` | Semantic Kernel の HTTP クライアントにレジリエンスポリシーを適用するアーキテクチャ方針の最終決定。Semantic Kernel 1.x の `HttpClient` 注入パターンの確認が必要 | テックリード |
| 3 | 通常 | `architect` | ベクトル DB の技術選定変更（spec.md Qdrant → Azure AI Search）を ADR-00XX として正式記録するか判断 | テックリード / アーキテクト |
| 4 | 通常 | `performance-reviewer` | Azure OpenAI GPT-4o の TPM 制限とピーク時のスロットリング対策、日次トークン予算（1,000,000）の妥当性 | SRE / インフラチーム |

## 競合解決記録

競合は検出されませんでした。

---

## ドキュメント横断分析

### サービス間整合性

| 検証項目 | 結果 | 詳細 |
|---------|------|------|
| Kafka イベント定義 | ⚠️ 一部不整合 | §11.1 購読イベント名の命名規則が不統一（`ProductUpdated` PascalCase vs `USER_REGISTERED` UPPER_SNAKE_CASE）。トピック名（`product.updated`）とイベント型名（`ProductUpdated`）の対応も不明確 |
| API 契約 | ✅ 整合 | ApiGateway からの `/api/v1/ai/**` ルーティングは api-gateway-design.md と整合 |
| DB 分離 | ✅ 整合 | ADR-0006（サービス別独立 DB）に準拠。`aisupportdb` を使用 |
| Outbox パターン | ❌ 不整合 | ADR-0005 でイベント発行は Outbox パターン必須だが、`outbox_events` テーブルが未定義（High #3） |
| RFC 9457 | ⚠️ 部分的 | ADR-0007 で Problem Details 必須。DTO に RFC 9457 対応はあるがグローバル例外ハンドラーのマッピングが未定義（High #6） |

### 記載カバレッジ分析（前回比較）

| セクション | 前回 | 今回 | 改善 |
|-----------|------|------|------|
| 概要・スコープ | ✅ | ✅ | — |
| 技術スタック | ✅ | ✅ | `Azure.Identity` / `Http.Resilience` 追加 |
| アーキテクチャ | ✅ | ✅ | — |
| データモデル | ⚠️ | ✅ | CHECK 制約、インデックス、TIMESTAMP WITH TIME ZONE 追加 |
| API 設計 | ✅ | ✅ | DTO バリデーション属性追加 |
| Semantic Kernel 統合 | ✅ | ⚠️ | DefaultAzureCredential 改善、だが HTTP クライアントギャップ（High #1） |
| セキュリティ | ❌ | ✅ | SSRF、レート制限、多層防御、GDPR、DPIA 追加。ただし一部 Medium 残 |
| ミドルウェアパイプライン | ❌ | ⚠️ | §12c 追加済み、だが例外ハンドラー詳細欠如（High #6） |
| 耐障害性 | ❌ | ⚠️ | §12a 追加済み、だが Semantic Kernel 連携ギャップ（High #1） |
| Correlation ID | ❌ | ✅ | §12b 追加 |
| EF Core エンティティ | ❌ | ✅ | §12d 追加 |
| テスト戦略 | ⚠️ | ✅ | §15.2 カバレッジ目標・AI 品質基準追加 |
| Outbox パターン | — | ❌ | 未対応（High #3） |
| Bicep インフラ | ⚠️ | ⚠️ | API キー → Managed Identity 未反映（High #2） |

### 未定義・曖昧な領域

| # | 領域 | 影響度 | 詳細 |
|---|------|--------|------|
| 1 | Outbox パターン | High | イベント発行の原子性未保証。`outbox_events` テーブル・OutboxPublisher 未定義 |
| 2 | Kernel ↔ HttpClient 統合 | High | レジリエンス設定が実際の AI 呼び出しに適用されない可能性 |
| 3 | Managed Identity インフラ | High | Bicep テンプレートと認証コードの不一致 |
| 4 | AppDbContext 設定 | Medium | ナビゲーションプロパティ・FK 関係の Fluent API 設定が未定義 |
| 5 | 例外 → Problem Details マッピング | Medium | グローバル例外ハンドラーの具体的な実装が不明 |

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### ビジネス要件の完全性

| 観点 | 判定 | 詳細 |
|------|------|------|
| ユーザーストーリー | ⚠️ | 前回指摘から変化なし。明示的なユーザーストーリー未定義だが、API 設計で機能要件は把握可能 |
| 受入基準 | ✅ | §15.2 に AI 品質テスト合否基準が追加された |
| スコープ | ✅ | In/Out of Scope が明確 |
| ビジネス KPI | ⚠️ | CVR/AOV への寄与目標が依然として未定義（Low #4） |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| BA-1 | Low | レコメンデーション機能のビジネス KPI（CVR/AOV 向上率等）と成功指標が未定義（前回 BA-1 から残存だが影響度は Low） |

</details>

<details>
<summary>architect レビューレポート</summary>

### アーキテクチャ設計の評価

| 観点 | 判定 | 詳細 |
|------|------|------|
| Bounded Context | ✅ | AI 機能が適切に分離 |
| DDD パターン | ⚠️ | Aggregate Root 未定義（Medium #5） |
| サービス間通信 | ✅ | HTTP + Kafka の二重連携が設計済み |
| レジリエンス | ⚠️ | 設定は追加されたが Semantic Kernel に適用されないギャップ |
| イベント設計 | ⚠️ | Outbox パターン未対応（High #3） |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| AR-1 | High | Semantic Kernel HTTP クライアント ↔ レジリエンスパイプラインの断絶（→ 統合指摘 High #1） |
| AR-2 | High | `outbox_events` テーブル未定義（→ 統合指摘 High #3） |
| AR-3 | Medium | Aggregate Root 未定義（→ 統合指摘 Medium #5） |

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### コード例の規約適合性

| 観点 | 判定 | 詳細 |
|------|------|------|
| Primary constructor | ✅ | ProductPlugin 修正済み。全 Service/Plugin が primary constructor パターン |
| CancellationToken | ✅ | 全 async メソッドに `ct` 伝搬 |
| record 型 DTO | ✅ | 全 DTO が record 型 |
| ILogger メッセージテンプレート | ✅ | 構造化ログ形式で記述 |
| C# 14 機能活用 | ✅ | record、パターンマッチング、null 条件演算子適切に使用 |
| Null Safety | ✅ | `!` 演算子排除済み。`?? throw` パターンを使用 |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| PR-1 | High | Kernel プラグイン DI 解決ギャップ（→ 統合指摘 High #4） |
| PR-2 | Medium | `ChatService.SendMessageAsync` の userMessage 永続化タイミング（→ 統合指摘 Medium #2） |
| PR-3 | Medium | `UserProfile.ChatSessions` ナビゲーションの EF Core 設定不足（→ 統合指摘 Medium #6） |

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### DB スキーマ設計

| 観点 | 判定 | 詳細 |
|------|------|------|
| 命名規則（snake_case） | ✅ | テーブル・カラム名は snake_case 準拠 |
| データ型 | ✅ | `TIMESTAMP WITH TIME ZONE` 統一、`DECIMAL(5,4)` 適切 |
| CHECK 制約 | ✅ | 全ステータスカラムに CHECK 制約定義済み |
| インデックス | ✅ | 必要な FK・クエリ用インデックス定義済み |
| 監査カラム | ✅ | `created_at`/`updated_at` 全テーブル、`created_by` は必要箇所に限定（理由注記あり） |
| EF Core エンティティ属性 | ✅ | §12d に `[Table]`/`[Column]`/`[Key]`/`[MaxLength]` 定義済み |
| Outbox テーブル | ❌ | `outbox_events` テーブル未定義 |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| DB-1 | High | `outbox_events` テーブル未定義（→ 統合指摘 High #3） |
| DB-2 | Medium | `chat_messages` FK に `ON DELETE CASCADE` 未記載（→ 統合指摘 Medium #4） |

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### セキュリティ設計の評価

| 観点 | 判定 | 詳細 |
|------|------|------|
| SSRF 防止 | ⚠️ | §12.6 追加済み。ただし IPv6 ブロックリストが C# コードに未反映（High #5） |
| OData インジェクション | ✅ | ホワイトリスト検証に修正済み |
| 認証・認可 | ✅ | ロールベースアクセス制御が適切に定義 |
| プロンプトインジェクション | ✅ | 5 層多層防御に改善、ブラックリストは補助的位置づけ |
| データ漏洩 | ✅ | 匿名化サマリのみ AI に送信する設計に変更済み |
| レート制限 | ⚠️ | 設定追加済みだが PartitionKey 未設定（Medium #3） |
| 入力バリデーション | ⚠️ | `SendMessageRequest` OK、`CreateChatSessionRequest` に長さ制限なし（Medium #9） |
| API キー管理 | ⚠️ | コードは Managed Identity だが Bicep/§20 が矛盾（High #2, #7） |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| SEC-1 | High | SSRF ブロックリスト IPv6 欠落（→ 統合指摘 High #5） |
| SEC-2 | Medium | `ResponseFilter` PII 検知ロジック欠如（→ 統合指摘 Medium #1） |
| SEC-3 | Medium | `CreateChatSessionRequest.InitialMessage` 長さ制限なし（→ 統合指摘 Medium #9） |

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### GDPR / 法規制適合性

| 観点 | 判定 | 詳細 |
|------|------|------|
| DPIA 要件 | ✅ | §12.8 に spec.md DPIA 計画参照 + 4 軽減措置追加 |
| データ保持期間 | ✅ | §12.7 に 6 テーブルの保持期間定義 + クリーンアップ BackgroundService |
| データ主体の権利 | ✅ | Art.17 削除権の Kafka イベントフロー定義済み |
| 越境データ移転 | ⚠️ | Azure OpenAI リージョン選定は Bicep (`location`) に依存。明示的な記述なし（エスカレーション #1 参照） |
| プロファイリング同意 | ✅ | §12.8 ① にオプトアウト機能の設計あり |
| データ匿名化 | ✅ | §9.2 の `AnonymizeUserPreferences` で個人データを AI に送信しない設計 |

**指摘一覧**: なし（前回の全指摘が解消済み）

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### トレーサビリティ・監査適合性

| 観点 | 判定 | 詳細 |
|------|------|------|
| 構造化ログ | ✅ | ILogger<T> + メッセージテンプレート使用 |
| Correlation ID | ✅ | §12b で設計済み。4 通信先への伝搬ルールあり |
| OpenTelemetry | ✅ | §14.2 にコード例追加済み |
| メトリクス | ✅ | §14.1 に 10 カスタムメトリクス定義 |
| 監査カラム | ✅ | `model_trainings.created_by` 追加済み。他テーブル省略理由明記 |
| RFC 9457 | ⚠️ | 例外ハンドラーのマッピングが未定義（High #6） |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| AU-1 | Medium | グローバル例外ハンドラーの RFC 9457 マッピング欠如（→ 統合指摘 High #6 に統合・格上げ） |

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### テスト戦略の評価

| 観点 | 判定 | 詳細 |
|------|------|------|
| テスト種別定義 | ✅ | Unit / Integration / DB / AI 品質テスト定義済み |
| カバレッジ目標 | ✅ | §15.2 に 80% 目標を明記、レイヤー別目標あり |
| 命名規約 | ✅ | `Should_*_When_*` パターン準拠 |
| AI 品質基準 | ✅ | ゴールデンテスト 80%、インジェクション防御 100%、Grounding 90%、P95 5秒 |
| Service 必須テストケース | ✅ | 正常系・異常系一覧あり |

**指摘一覧**: なし（前回 QA-1 の指摘が解消済み）

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### パフォーマンス設計の評価

| 観点 | 判定 | 詳細 |
|------|------|------|
| キャッシュ戦略 | ✅ | Redis レコメンデーションキャッシュ（TTL 1h）+ フォールバック戦略あり |
| スライディングウィンドウ | ✅ | チャット履歴を直近 20 件に制限 |
| スケーリング | ✅ | Azure Container Apps のスケーリングルール定義あり |
| フォールバック | ✅ | §12a.2 に 4 障害パターンのフォールバック定義（新規追加） |
| SLA/SLO | ⚠️ | AI 応答のアラート閾値はあるがユーザー向け SLO は未定義 |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| PF-1 | Low | `EstimateTokenCount` の `text.Length / 4` 概算の不正確さ（→ 統合指摘 Low #5） |

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### インフラ・運用設計の評価

| 観点 | 判定 | 詳細 |
|------|------|------|
| Dockerfile | ✅ | マルチステージビルド ✅ / 非 root ✅ / バージョン固定 ✅ / HEALTHCHECK ✅ |
| ヘルスチェック | ✅ | `/health` + `/health/ready` + Azure OpenAI / AI Search チェック |
| Bicep | ⚠️ | API キーパターンと Managed Identity の矛盾（High #2）、`latest` タグ使用（Medium #8） |
| CI/CD | ✅ | GitHub Actions 定義あり。テスト → ビルド → デプロイのフロー |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| IO-1 | High | Bicep の API キー ↔ Managed Identity 矛盾（→ 統合指摘 High #2） |
| IO-2 | Medium | Bicep の `latest` タグ使用禁止違反（→ 統合指摘 Medium #8） |

</details>

<details>
<summary>release-manager レビューレポート</summary>

### リリース戦略の評価

| 観点 | 判定 | 詳細 |
|------|------|------|
| CI/CD パイプライン | ✅ | GitHub Actions 定義あり。テスト + カバレッジ + デプロイ |
| ロールバック計画 | ⚠️ | AI モデル更新時のリリース / ロールバック分離が未設計 |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| RM-1 | Low | AI モデルバージョン更新時のリリース / ロールバック手順がデプロイメント分離されていない（→ 統合指摘 Low #2） |

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### NuGet 依存関係の評価

| 観点 | 判定 | 詳細 |
|------|------|------|
| 必須パッケージ | ✅ | AGENTS.md §8.1 の必須パッケージを含む。`Azure.Identity` / `Microsoft.Extensions.Http.Resilience` が新規追加 |
| 禁止パッケージ | ✅ | 禁止パッケージの使用なし |
| プレリリース版 | ✅ | プレリリース版の使用なし |
| ライセンス | ✅ | 全パッケージ MIT/Apache-2.0 互換 |
| `Newtonsoft.Json` | ✅ | 使用なし（`System.Text.Json` を使用） |

**指摘一覧**: なし

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### UX / アクセシビリティの評価

| 観点 | 判定 | 詳細 |
|------|------|------|
| チャット UX | ⚠️ | ストリーミング応答が未設計（Low #1） |
| エラーメッセージ | ✅ | RFC 9457 Problem Details 形式を採用 |
| フォールバック UX | ✅ | §12a.2 でAI 障害時のユーザー向けメッセージを定義 |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| UX-1 | Low | ストリーミング応答（SSE）が未検討（→ 統合指摘 Low #1） |

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 技術標準の横断適合性

| 観点 | 判定 | 詳細 |
|------|------|------|
| AGENTS.md 準拠 | ⚠️ | Outbox パターン（ADR-0005）未対応。他は概ね準拠 |
| Instructions 準拠 | ✅ | コーディング規約・セキュリティ規約に概ね準拠 |
| DDD 原則 | ⚠️ | Aggregate Root 未定義 |
| 実装実現可能性 | ⚠️ | Semantic Kernel のプラグイン DI とレジリエンス統合に設計ギャップ |
| 設計一貫性 | ⚠️ | §20 制約と §7.1 コードの矛盾（API キー vs Managed Identity） |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| TL-1 | High | §20 制約 #3 と §7.1 の Managed Identity アプローチの矛盾（→ 統合指摘 High #7） |
| TL-2 | Medium | `DataRetentionCleanupService` コード例が ChatSessions のみ（→ 統合指摘 Medium #7） |
| TL-3 | Low | spec.md Qdrant → Azure AI Search の変更が ADR 未記録（→ 統合指摘 Low #3） |

</details>

---

## 修正優先度ガイド

### 早期対応（High — 実装開始前に解決すべき）

1. **Semantic Kernel ↔ レジリエンスパイプラインの統合**（High #1）— HttpClient 注入パターンの確定
2. **Bicep テンプレートの Managed Identity 対応**（High #2）— API キー排除 + RBAC 設定追加
3. **`outbox_events` テーブルの追加**（High #3）— ADR-0005 準拠
4. **Kernel プラグイン DI の修正**（High #4）— `AddFromType` → `AddFromObject` パターン
5. **SSRF ブロックリスト IPv6 補完**（High #5）— コードとテーブルの一致
6. **グローバル例外ハンドラーの RFC 9457 マッピング追加**（High #6）— ADR-0007 準拠
7. **§20 制約の Managed Identity 反映**（High #7）— 設計一貫性

### 中期対応（Medium — 実装と並行して対応可能）

8. Medium #1〜#9 は実装フェーズで段階的に対応可能

---

## 前回比較サマリ

| 指標 | check-report-1 | check-report-2 | 変化 |
|------|---------------|---------------|------|
| **判定** | ❌ Rejected | ⚠️ Conditional Approval | 改善 |
| **Critical** | 4 | 0 | **-4（全件解消）** |
| **High** | 21 | 7 | **-14（17 件解消、7 件新規）** |
| **Medium** | 23 | 9 | **-14** |
| **Low** | 5 | 5 | ±0 |
| **合計指摘数** | 53 | 21 | **-32（60% 削減）** |
| **ドキュメント行数** | 1562 | 2250 | +688 |

**新規 High 7 件の内訳**:
- 修正に起因する新規指摘: 3 件（High #1 レジリエンス統合ギャップ、High #2 Bicep 未更新、High #7 §20 制約未更新）
- 前回見落とし: 2 件（High #3 Outbox 欠如、High #4 プラグイン DI）
- 修正の深堀りで判明: 2 件（High #5 IPv6 欠落、High #6 例外ハンドラー詳細）
