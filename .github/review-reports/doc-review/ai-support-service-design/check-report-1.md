# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/ai-support-service-design.md`（AiSupportService 詳細設計書）
- **判定**: ❌ **Rejected** — 重大な不備あり
- **レビュー日時**: 2026-04-03
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

## 🚨 CRITICAL 指摘検出

Critical 指摘が **4 件** 検出されたため、自動的に ❌ Rejected 判定となります。

## 技術スタック検証結果
| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| ランタイム | .NET 10 (LTS) | .NET 10 (LTS) | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| AI フレームワーク | Semantic Kernel 1.x | Semantic Kernel 1.x | ✅ |
| ORM | EF Core 10 | EF Core 10 | ✅ |
| DB | PostgreSQL | PostgreSQL | ✅ |
| メッセージング | Confluent.Kafka 2.* | Confluent.Kafka 2.* | ✅ |
| キャッシュ | StackExchange.Redis 2.* | StackExchange.Redis 2.* | ✅ |
| 耐障害性 | Polly 8.* | Polly 8.* | ✅ |
| ログ | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| テスト | xUnit / NSubstitute / Shouldly | xUnit / NSubstitute / Shouldly | ✅ |
| ベクトル DB | Azure AI Search | spec.md: Qdrant 等 | ⚠️ 差異あり（下記 Medium #4） |

## 指摘サマリー
| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ⚠️ Conditional | 0 | 1 | 2 | 1 |
| architect | ⚠️ Conditional | 0 | 3 | 2 | 0 |
| programing-reviewer | ❌ Rejected | 2 | 2 | 3 | 1 |
| dba-reviewer | ⚠️ Conditional | 0 | 2 | 3 | 0 |
| security-reviewer | ❌ Rejected | 2 | 3 | 1 | 0 |
| compliance-reviewer | ⚠️ Conditional | 0 | 2 | 1 | 0 |
| audit-reviewer | ⚠️ Conditional | 0 | 2 | 1 | 0 |
| qa-manager | ⚠️ Conditional | 0 | 1 | 2 | 0 |
| performance-reviewer | ⚠️ Conditional | 0 | 1 | 2 | 0 |
| infra-ops-reviewer | ⚠️ Conditional | 0 | 1 | 2 | 1 |
| release-manager | ✅ Approved with Notes | 0 | 0 | 1 | 1 |
| oss-reviewer | ✅ Approved with Notes | 0 | 0 | 1 | 0 |
| ux-accessibility-reviewer | ✅ Approved with Notes | 0 | 0 | 0 | 1 |
| tech-lead | ❌ Rejected | 0 | 3 | 2 | 0 |
| **合計** | | **4** | **21** | **23** | **5** |

## 判定根拠
- **判定ルール適用結果**: Critical 指摘 4 件 → 自動 Rejected
- **最も重要な指摘**: SSRF 防止設計の完全欠如（spec.md に詳細な SSRF 防止設計があるにもかかわらず、設計書に一切記載なし）、OData フィルタインジェクション脆弱性

---

## Critical/High 指摘一覧（修正必須）

| # | 重要度 | 出典 Agent | カテゴリ | 対象箇所 | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|---------|----------|----------|
| 1 | **Critical** | `security-reviewer`, `tech-lead` | セキュリティ / SSRF | §12 セキュリティ設計 全体 | **SSRF 防止設計が完全に欠如している**。spec.md §SSRF 防止設計（L1281-L1370）に URL ホワイトリスト、プライベート IP 拒否、DNS リバインディング対策、リクエストサイズ制限、`SsrfPreventionHandler` の C# 実装まで詳細に記載されているが、設計書に一切反映されていない。AiSupportService は Azure OpenAI・Azure AI Search への外部 HTTP リクエストを発行するため、SSRF リスクが最も高いサービスである | spec.md §SSRF 防止設計の内容を丸ごと設計書 §12 に転記・統合する。`SsrfPreventionHandler` の登録、許可ドメイン一覧、プライベート IP 拒否リスト、DNS リバインディング対策、リクエストサイズ制限を含めること |
| 2 | **Critical** | `security-reviewer`, `programing-reviewer` | セキュリティ / インジェクション | §8.1 `SearchService` L721-729 | **OData フィルタインジェクション脆弱性**。`filters.Add($"category eq '{request.Category}'")`（L723）でユーザー入力を直接 OData フィルタ文字列に埋め込んでいる。`request.Category` に `' or 1 eq 1 or category eq '` 等の値を注入することで、全データの取得やフィルタバイパスが可能 | OData クエリパラメータは `SearchFilter.Create()` を使用するか、入力値をホワイトリスト（許可カテゴリ一覧）で検証した上でフィルタに組み込む。`MinPrice`/`MaxPrice` は数値型なので問題ないが、文字列型パラメータは全てサニタイズすること |
| 3 | **Critical** | `programing-reviewer` | セキュリティ / API Key | §7.1 Kernel セットアップ L504-507 | **API キーを `builder.Configuration["AzureOpenAI:ApiKey"]!` で直接取得するコード例が示されている**。§16 の注意書きで Managed Identity を推奨しているが、コード例自体が API キーベース認証パターンを示しており、実装者がそのままコピーするリスクが高い。また `!`（null 許容演算子の強制）は null safety 規約に違反する | コード例を `DefaultAzureCredential` ベースの Managed Identity パターンに差し替える。API キーベースのコード例は削除するか、開発環境限定であることを明示し、`user-secrets` 経由での取得パターンを示すこと |
| 4 | **Critical** | `programing-reviewer`, `security-reviewer` | セキュリティ / データ漏洩 | §9.2 `RecommendationService` L918-921 | **ユーザーの購入履歴・嗜好データを Azure OpenAI プロンプトに直接埋め込んでいる**。`$"購入履歴: {profile?.PurchaseHistoryJson}"` で個人データを外部 AI プロバイダーに送信しているが、§12.2 で「AI に個人情報を含むプロンプトを送信しない」と定めており矛盾する。購入履歴は個人情報に該当する可能性があり、GDPR の越境データ移転にも抵触しうる | 購入履歴をカテゴリ・ブランド傾向等の匿名化された形式に変換してからプロンプトに含める。または、Semantic Kernel の Function Calling で商品データのみを渡し、ユーザー個人データはプロンプトに含めない設計に変更する |
| 5 | High | `security-reviewer`, `programing-reviewer` | セキュリティ / 入力検証 | §12.3 `InputSanitizer` L1026-1046 | **プロンプトインジェクション対策がブラックリスト方式**。`BlockedPatterns` による文字列マッチは、Unicode 文字の混入、URL エンコード、大文字小文字の混合（`ToLowerInvariant` で部分対策済み）、類義語による回避などで容易にバイパスされる。`security-coding.instructions.md` §1 でブラックリスト方式は明確に禁止されており、ホワイトリスト方式を要求している | ① システムプロンプトのガードレール強化（メタプロンプトで制約を再強調） ② Azure OpenAI の Content Safety フィルターを有効化 ③ ユーザー入力の最大長制限（§12.4 の `MaxTokensPerMessage` を Endpoint レベルで強制） ④ AI 応答の後処理フィルタリング（個人情報・システム情報の漏洩検知）。ブラックリストは補助的な位置づけに格下げし、多層防御の一層として記述する |
| 6 | High | `architect`, `tech-lead` | アーキテクチャ / 耐障害性 | 設計書全体 | **Azure OpenAI への Polly レジリエンスポリシーが未設計**。Polly 8.* が依存関係に含まれているが、Azure OpenAI / Azure AI Search への HTTP 通信に対するリトライ・サーキットブレーカー・タイムアウト・フォールバック戦略が一切記述されていない。AGENTS.md §11.1 で全外部 HTTP 通信に `AddStandardResilienceHandler` の適用が必須 | `IHttpClientFactory` + `AddStandardResilienceHandler` を使用した Azure OpenAI / Azure AI Search クライアントの登録パターンを設計書に追加。リトライ回数、バックオフ、サーキットブレーカーのパラメータ、フォールバック戦略（キャッシュレスポンス返却 / 汎用応答返却等）を定義すること |
| 7 | High | `architect`, `tech-lead` | アーキテクチャ / ミドルウェア | §13-§16 | **Program.cs のミドルウェアパイプライン設計が完全に欠如**。AGENTS.md §11.3 で定義されたミドルウェア登録順序（ExceptionHandler → HSTS → Correlation ID → Serilog → CORS → Authentication → Authorization → RateLimiter → Endpoints）が設計書に記載されていない。ミドルウェア順序の誤りは認証バイパスやログ欠落の原因となる | Program.cs のミドルウェアパイプライン定義セクションを追加し、AGENTS.md §11.3 準拠の登録順序を明示すること |
| 8 | High | `security-reviewer`, `architect` | セキュリティ / レート制限 | §12 セキュリティ設計 | **API エンドポイントレベルのレート制限が未設計**。`AiLimits` でトークン上限・セッション数上限は定義されているが、ASP.NET Core `AddRateLimiter` によるエンドポイントレベルのレート制限が設計されていない。AI エンドポイントは高コストであり、DoS / 不正利用のリスクが高い | `builder.Services.AddRateLimiter()` の設定を追加。チャット API（`/api/v1/ai/chat/**`）は IP / ユーザーあたりの制限を厳しく設定。検索 API・レコメンデーション API にも適切な制限を設定すること |
| 9 | High | `compliance-reviewer`, `audit-reviewer` | GDPR / データ保持 | §5 データモデル | **チャットデータの保持期間・削除ポリシーが未定義**。`chat_sessions` / `chat_messages` テーブルに TTL やクリーンアップ戦略が記載されていない。チャット内容はユーザーの個人データを含む可能性があり、GDPR に基づくデータ最小化原則（Art.5(1)(c)）・保存期間制限原則（Art.5(1)(e)）に抵触する。また、データ主体の削除権（Art.17）への対応方法も未定義 | ① `chat_sessions` / `chat_messages` の保持期間を定義（例: 90 日後に自動削除） ② `BackgroundService` によるクリーンアップジョブを設計 ③ GDPR 削除権行使時のカスケード削除フローを定義 ④ `user_profiles` の `browsing_history_json` / `purchase_history_json` の保持期間も定義すること |
| 10 | High | `compliance-reviewer`, `security-reviewer` | GDPR / DPIA | 設計書全体 | **spec.md の DPIA 計画が設計書に反映されていない**。spec.md L5049-L5070 に AiSupportService の商品レコメンデーション機能に関する詳細な DPIA（GDPR 第35条該当性、軽減措置、レビュー頻度）が記載されているが、設計書に一切言及がない。特に ① オプトアウト機能 ② 推薦理由の透明性表示 ③ 定期的なバイアスチェックの実装設計が欠如 | spec.md の DPIA 計画を設計書に参照記載し、各軽減措置の実装設計を追加する。レコメンデーション API レスポンスに `reason` フィールドは存在するが、表示の仕組みや `PERSONALIZATION` 同意撤回時の処理フローが不足 |
| 11 | High | `audit-reviewer`, `architect` | 可観測性 / Correlation ID | 設計書全体 | **Correlation ID の設計が欠如**。AGENTS.md §11.2 で全リクエストに相関 ID を付与しマイクロサービス間で伝搬することが必須だが、設計書に記載がない。AI エンドポイントは複数の外部サービス（Azure OpenAI、Azure AI Search、Kafka）を呼び出すため、分散トレーシングにおける Correlation ID は特に重要 | Correlation ID ミドルウェアの設計を追加。`X-Correlation-Id` ヘッダーの受け取り・生成・伝搬、ログコンテキストへの注入（`LogContext.PushProperty`）を設計すること |
| 12 | High | `dba-reviewer` | DB 設計 / エンティティ | §5.2 テーブル定義 / §7.3 コード例 | **EF Core エンティティに `[Table]` / `[Column]` 属性が未定義**。AGENTS.md §10.3 でテーブル名・カラム名の snake_case マッピングを `[Table("snake_case")]` / `[Column("snake_case")]` で明示することが必須だが、コード例のエンティティクラス（`ChatSession`, `ChatMessage`, `Recommendation` 等）にこれらの属性が含まれていない | 全エンティティクラスのコード例に `[Table("table_name")]`, `[Column("column_name")]`, `[Key]`, `[MaxLength]` 等のデータアノテーションを追加すること |
| 13 | High | `dba-reviewer` | DB 設計 / 監査カラム | §5.2 テーブル定義 | **`created_by` / `updated_by` 監査カラムが欠如**。`sql-schema-review.instructions.md` §2 で全テーブルに `created_by` / `updated_by` 監査カラムを含めることが推奨されている。特に `recommendations` / `model_trainings` テーブルではシステム / 管理者による操作の追跡が必要 | 必要に応じて `created_by` / `updated_by` カラムを追加するか、省略理由を設計書に明記すること |
| 14 | High | `architect` | アーキテクチャ / DI | §7.1 Kernel セットアップ L500 | **Kernel が `AddSingleton` で登録されているが、プラグイン内で Scoped サービスを使用する可能性がある**。`ProductPlugin` が `IProductClient`（通常 Scoped の `IHttpClientFactory` ベース）を DI で受け取るが、Singleton の Kernel からプラグインが解決されると Scoped サービスの解決に失敗するか Captive Dependency 問題が発生する | Kernel を `AddScoped` に変更するか、プラグイン内で `IServiceScopeFactory` を使用してスコープを生成する設計に変更すること |
| 15 | High | `performance-reviewer` | パフォーマンス | §7.3 `ChatService` L614-625 | **会話履歴の全件取得がスケーラビリティ上の問題**。`FindBySessionIdAsync` で全メッセージを取得し `ChatHistory` に追加しているが、長いチャットセッションではトークン上限（10,000）を超過する前にメモリ消費とレイテンシが問題となる。また Azure OpenAI のコンテキストウィンドウ上限にも抵触する | 直近 N 件のメッセージのみ取得するスライディングウィンドウ方式を導入するか、トークン数ベースで過去メッセージを切り捨てる設計にすること |
| 16 | High | `tech-lead` | 規約整合性 | §7.2 `ProductPlugin` L537-542 | **ProductPlugin が従来のコンストラクタ方式を使用**。AGENTS.md §4.3 / `dotnet-coding-standards.instructions.md` §3 で Service / Repository クラスは primary constructor を使用することが推奨されている | `public class ProductPlugin(IProductClient productClient)` の primary constructor パターンに変更すること |
| 17 | High | `qa-manager` | テスト戦略 | §15 テスト戦略 | **テストカバレッジの具体的な計測方法と目標値が不十分**。AGENTS.md §9.4 で分岐カバレッジ 80% 以上が必須だが、設計書のテスト戦略では AI 固有のテスト（ゴールデンテスト等）の品質基準（合格閾値等）が未定義。また Service 層の正常系・異常系テストケースの網羅性についての記述が不足 | テスト戦略に ① 分岐カバレッジ目標 80% の明記 ② AI 品質テストの合否判定基準 ③ Service 層の必須テストケース一覧（異常系含む）を追加すること |

## エスカレーション事項（要人間判断）
| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| 1 | 最優先 | `compliance-reviewer` | spec.md の DPIA 計画で「開発フェーズ開始前（設計段階で完了必須）」とされている DPIA が未実施の可能性がある。AiSupportService の実装開始前に DPIA の完了を確認すべき | プライバシー責任者 / DPO |
| 2 | 最優先 | `security-reviewer` | ユーザーの購入履歴・嗜好データを Azure OpenAI に送信する設計について、越境データ移転（Azure OpenAI のデータ処理場所）とプライバシーポリシーの整合性を確認すべき | 法務チーム / プライバシー責任者 |
| 3 | 高優先 | `architect` | spec.md ではベクトル DB として「Qdrant 等」を記載しているが、設計書では Azure AI Search を採用している。この設計変更を ADR として記録すべきか判断が必要 | テックリード / アーキテクト |
| 4 | 通常 | `performance-reviewer` | Azure OpenAI の GPT-4o モデルの TPM（Tokens Per Minute）制限と日次トークン予算（1,000,000）の関係。ピーク時のスロットリング対策と用量計画が必要 | SRE / インフラチーム |

## 競合解決記録

競合は検出されませんでした。

---

## ドキュメント横断分析

### サービス間整合性

| 検証項目 | 結果 | 詳細 |
|---------|------|------|
| Kafka イベント定義 | ⚠️ 一部不整合 | 購読イベント `USER_REGISTERED` の発行元が `AuthService` とされているが、spec.md の AuthService セクションでこのイベントの発行が明示されているか要確認 |
| API 契約 | ✅ 整合 | ApiGateway からの `/api/v1/ai/**` ルーティングは api-gateway-design.md と整合 |
| DB 分離 | ✅ 整合 | ADR-0006（サービス別独立 DB）に準拠。AiSupportService は独自の PostgreSQL スキーマを使用 |
| InventoryManagementService 連携 | ⚠️ 部分的 | `IProductClient` による同期 HTTP 呼び出しと Kafka イベント購読の二重連携が設計されているが、HTTP クライアントの DI 登録・Polly 設定が未定義 |

### 記載カバレッジ分析

| セクション | カバレッジ | 備考 |
|-----------|----------|------|
| 概要・スコープ | ✅ 十分 | |
| 技術スタック | ✅ 十分 | |
| アーキテクチャ | ✅ 十分 | Mermaid 図あり |
| データモデル | ⚠️ 部分的 | テーブル定義あり、EF Core 属性なし |
| API 設計 | ✅ 十分 | Endpoint 一覧・DTO 定義あり |
| Semantic Kernel 統合 | ✅ 十分 | プラグイン・チャット処理フローあり |
| 検索・レコメンデーション | ✅ 十分 | |
| セキュリティ | ❌ 重大な不足 | SSRF 防止欠如、レート制限欠如 |
| GDPR / プライバシー | ❌ 不足 | DPIA 未参照、データ保持期間未定義 |
| テスト戦略 | ⚠️ 部分的 | AI 固有テストの合否基準不足 |
| 監視・メトリクス | ✅ 十分 | |
| ミドルウェアパイプライン | ❌ 欠如 | |
| 耐障害性 | ❌ 欠如 | Polly 設計なし |
| Correlation ID | ❌ 欠如 | |

### 未定義・曖昧な領域

| # | 領域 | 影響度 | 詳細 |
|---|------|--------|------|
| 1 | SSRF 防止 | Critical | 実装ブロッカー。spec.md の設計が反映されていない |
| 2 | データ保持・GDPR 削除権 | High | チャットデータ・ユーザープロファイルの保持期間とクリーンアップ未定義 |
| 3 | Azure OpenAI フォールバック | High | AI サービス障害時のユーザー体験（エラーメッセージ？キャッシュ応答？） |
| 4 | トークンカウントの正確性 | Medium | `text.Length / 4` の概算は不正確。tiktoken 等の正規トークナイザーの使用を検討 |
| 5 | Kernel のスコープ管理 | High | Singleton Kernel + Scoped プラグイン依存の解決方法 |

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### ビジネス要件の完全性

| 観点 | 判定 | 詳細 |
|------|------|------|
| ユーザーストーリー | ⚠️ | ユーザーストーリーが明示的に定義されていない。各 API の用途は記述されているが、ビジネス目標との紐付けが弱い |
| 受入基準 | ⚠️ | AI 応答の品質基準（顧客満足度、エスカレーション率の閾値等）が定量的に定義されていない |
| スコープ | ✅ | In Scope / Out of Scope が明確に定義されている |
| ビジネス KPI | ⚠️ | §14.3 アラート条件にエスカレーション率 30% 等の閾値はあるが、ビジネス KPI（CVR 向上率、AOV 向上率等）との紐付けがない |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| BA-1 | High | レコメンデーション機能のビジネス目標（CVR/AOV への寄与目標）と成功指標が未定義。「ビジネス価値の測定」セクションを追加すべき |
| BA-2 | Medium | チャットボットの「有人サポートへのエスカレーション」のビジネスフロー（有人サポート側のシステム連携、SLA）が未定義 |
| BA-3 | Medium | 需要予測の精度目標と、予測結果の活用フロー（InventoryManagementService への自動発注連携等）が未定義 |
| BA-4 | Low | 多言語対応が Phase 2 以降とされているが、Phase 2 の計画がない |

</details>

<details>
<summary>architect レビューレポート</summary>

### アーキテクチャ設計の評価

| 観点 | 判定 | 詳細 |
|------|------|------|
| Bounded Context | ✅ | AI 機能が適切に分離されている |
| DDD パターン | ⚠️ | Aggregate Root が不明確。UserProfile / ChatSession はどちらが Aggregate Root か |
| サービス間通信 | ⚠️ | HTTP 同期呼び出し（IProductClient）の DI 登録・Polly 設定が未定義 |
| イベント設計 | ✅ | 購読・発行イベントが明確 |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| AR-1 | High | Azure OpenAI / Azure AI Search へのレジリエンスポリシー（リトライ・サーキットブレーカー・フォールバック）が未設計。（→ 統合指摘 #6） |
| AR-2 | High | Middleware パイプライン定義の欠如。（→ 統合指摘 #7） |
| AR-3 | High | Kernel の Singleton 登録と Scoped プラグイン依存の矛盾。（→ 統合指摘 #14） |
| AR-4 | Medium | spec.md のベクトル DB（Qdrant）との技術選定の差異が ADR として記録されていない |
| AR-5 | Medium | `ProductIndexSyncConsumer` の Kafka consumer が `ConsumeException` のみハンドリングしており、デシリアライゼーション失敗時の Dead Letter Queue 設計が不足 |

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### コード例の規約適合性

| 観点 | 判定 | 詳細 |
|------|------|------|
| Primary constructor | ⚠️ | ChatService は primary constructor 使用 ✅ / ProductPlugin は従来コンストラクタ ❌ |
| CancellationToken | ✅ | 概ね全 async メソッドに `ct` が伝搬されている |
| record 型 DTO | ✅ | リクエスト/レスポンス DTO は record 型で定義 |
| ILogger メッセージテンプレート | ✅ | 構造化ログ形式で記述されている |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| PR-1 | Critical | OData フィルタインジェクション。（→ 統合指摘 #2） |
| PR-2 | Critical | API キー取得パターンの問題。（→ 統合指摘 #3） |
| PR-3 | High | ProductPlugin が従来コンストラクタを使用。（→ 統合指摘 #16） |
| PR-4 | High | `EstimateTokenCount` が `text.Length / 4` で概算。tiktoken 等の正規トークナイザーを使用すべき |
| PR-5 | Medium | `ChatService.SendMessageAsync` で `userMessage` と `assistantMessage` の保存が個別の `AddAsync` 呼び出しになっており、`SaveChangesAsync` が最後に 1 回のみ。`userMessage` 保存後にAI 呼び出しが失敗した場合のデータ整合性が不明確 |
| PR-6 | Medium | `SystemPrompt` が `static readonly string` で定義されているが、設定ファイル（`IOptions<T>`）から読み込むべき。システムプロンプトの変更にデプロイが必要になる |
| PR-7 | Medium | `ChatService.ToResponse` メソッドが `private static` であるが、マッピングロジックが複数箇所に分散する可能性がある。拡張メソッドまたはマッピングクラスへの集約を検討 |
| PR-8 | Low | `SearchService` 内で `System.Diagnostics.Stopwatch` を直接使用。OpenTelemetry のメトリクス（`Histogram`）で計測すべき |

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### DB スキーマ設計

| 観点 | 判定 | 詳細 |
|------|------|------|
| 命名規則（snake_case） | ✅ | テーブル・カラム名は snake_case |
| 正規化 | ⚠️ | `user_profiles` の JSONB カラム（`browsing_history_json` 等）は検索性に課題 |
| インデックス | ⚠️ | 一部テーブルのインデックスが未定義 |
| 制約 | ⚠️ | CHECK 制約が定義されていない |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| DB-1 | High | EF Core エンティティの `[Table]` / `[Column]` 属性欠如。（→ 統合指摘 #12） |
| DB-2 | High | `created_by` / `updated_by` 監査カラムの欠如。（→ 統合指摘 #13） |
| DB-3 | Medium | `recommendations` テーブルに `user_id` のインデックスが未定義。ユーザー別レコメンデーション取得のパフォーマンスに影響する |
| DB-4 | Medium | `search_analytics` テーブルに `created_at` のインデックスが未定義。分析レポート生成時のクエリパフォーマンスに影響する |
| DB-5 | Medium | `demand_forecasts` テーブルに `(product_id, forecast_date)` の複合インデックスが必要。商品別予測の時系列取得に使用される |

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### セキュリティ設計の評価

| 観点 | 判定 | 詳細 |
|------|------|------|
| SSRF 防止 | ❌ | **完全欠如**。spec.md の詳細設計が未反映 |
| 認証・認可 | ✅ | ロールベースアクセス制御が定義されている |
| プロンプトインジェクション | ⚠️ | ブラックリスト方式。バイパスリスクあり |
| データ漏洩 | ❌ | ユーザー個人データが AI プロンプトに含まれる |
| レート制限 | ❌ | エンドポイントレベルのレート制限なし |
| 入力バリデーション | ⚠️ | DTO に Data Annotations はあるが FluentValidation の Validator 定義なし |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| SEC-1 | Critical | SSRF 防止設計の欠如。（→ 統合指摘 #1） |
| SEC-2 | Critical | OData フィルタインジェクション。（→ 統合指摘 #2） |
| SEC-3 | High | プロンプトインジェクション対策がブラックリスト方式。（→ 統合指摘 #5） |
| SEC-4 | High | レート制限の欠如。（→ 統合指摘 #8） |
| SEC-5 | High | ユーザーデータの AI プロンプト送信。（→ 統合指摘 #4 を参照） |
| SEC-6 | Medium | `SearchRequest` の `Category` パラメータに `[RegularExpression]` 等のホワイトリスト検証がない |

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### GDPR / 法規制適合性

| 観点 | 判定 | 詳細 |
|------|------|------|
| DPIA 要件 | ❌ | spec.md の DPIA 計画が設計書に未反映 |
| データ保持期間 | ❌ | チャットデータ・ユーザープロファイルの保持期間未定義 |
| データ主体の権利 | ❌ | 削除権（Art.17）の実装設計がない |
| 越境データ移転 | ⚠️ | Azure OpenAI のデータ処理場所に関する記述がない |
| プロファイリング同意 | ⚠️ | `PERSONALIZATION` 同意撤回時の処理フローが未定義 |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| CO-1 | High | DPIA 計画の未反映。（→ 統合指摘 #10） |
| CO-2 | High | データ保持期間の未定義。（→ 統合指摘 #9） |
| CO-3 | Medium | AI プロファイリングのオプトアウト実装設計が不足。spec.md で `PERSONALIZATION` 同意カテゴリが定義されているが、同意撤回時にレコメンデーション停止・データ削除を行うフローが設計書にない |

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### トレーサビリティ・監査適合性

| 観点 | 判定 | 詳細 |
|------|------|------|
| 構造化ログ | ✅ | ILogger<T> + メッセージテンプレート使用 |
| Correlation ID | ❌ | 設計に含まれていない |
| OpenTelemetry | ⚠️ | 依存関係にあるが設定コード例がない |
| メトリクス | ✅ | カスタムメトリクスが定義されている |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| AU-1 | High | Correlation ID の欠如。（→ 統合指摘 #11） |
| AU-2 | High | OpenTelemetry のトレーシング設定（`AddSource("SkiShop.*")`、`AddAspNetCoreInstrumentation` 等）のコード例がない。AGENTS.md §11.2 で必須とされている |
| AU-3 | Medium | AI モデルの変更・再トレーニングの監査証跡設計が不足。`model_trainings` テーブルはあるが、誰が・いつ・なぜトレーニングを実行したかの記録が `created_by` 欠如により不完全 |

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### テスト戦略の評価

| 観点 | 判定 | 詳細 |
|------|------|------|
| テスト種別定義 | ✅ | Unit / Integration / DB / AI 品質テストが定義 |
| カバレッジ目標 | ❌ | 80% 目標の明記なし |
| 命名規約 | ⚠️ | テストコード例は `Should_*_When_*` パターンに概ね準拠 |
| 異常系テスト | ⚠️ | プロンプトインジェクションのテストはあるが、Azure OpenAI 障害時のテストケースが不足 |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| QA-1 | High | テストカバレッジ目標・AI 品質基準の不足。（→ 統合指摘 #17） |
| QA-2 | Medium | Azure OpenAI のモック戦略が不明確。Semantic Kernel の `IChatCompletionService` をどのように NSubstitute でモックするかのガイドがない |
| QA-3 | Medium | `SearchService` の統合テスト戦略が不足。Azure AI Search の Testcontainers / エミュレーターの使用方針が未定義 |

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### パフォーマンス設計の評価

| 観点 | 判定 | 詳細 |
|------|------|------|
| キャッシュ戦略 | ✅ | Redis によるレコメンデーションキャッシュ（TTL 1h） |
| SLA/SLO | ⚠️ | Azure OpenAI の応答時間アラート（10 秒）はあるが、ユーザー向け SLO が未定義 |
| スケーリング | ✅ | Azure Container Apps のスケーリングルール定義あり |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| PF-1 | High | 会話履歴の全件取得のスケーラビリティ問題。（→ 統合指摘 #15） |
| PF-2 | Medium | 検索 API の `AsNoTracking()` が SearchAnalytics 保存と混在。読み取り専用の検索結果取得と書き込み（分析データ保存）が同一メソッド内にあり、パフォーマンス最適化が困難 |
| PF-3 | Medium | Azure OpenAI のレスポンスタイムの P95/P99 SLO が未定義。チャット API・検索 API のユーザー向けレスポンスタイム目標を設定すべき |

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### インフラ・運用設計の評価

| 観点 | 判定 | 詳細 |
|------|------|------|
| Dockerfile | ⚠️ | マルチステージビルド ✅ / 非 root ✅ / EXPOSE ポートが 5009（8080 推奨） |
| ヘルスチェック | ✅ | `/health` / `/health/ready` + Azure OpenAI / AI Search チェック |
| DR 計画 | ❌ | Azure OpenAI 障害時の DR / フォールバック計画がない |
| Bicep | ✅ | Azure リソース定義あり |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| IO-1 | High | Azure OpenAI 障害時のフォールバック戦略が未設計。チャット・レコメンデーション・検索すべてが Azure OpenAI に依存するため、障害時にサービス全体が停止する |
| IO-2 | Medium | Dockerfile の `EXPOSE 5009` はサービスのローカルポートだが、Azure Container Apps では `targetPort: 5009` で設定済み。AGENTS.md の Dockerfile 規約では `EXPOSE 8080` が推奨されている |
| IO-3 | Medium | Bicep テンプレートの Container Apps 定義で `image: '${containerRegistry.properties.loginServer}/ai-support-service:latest'` を使用しており、`latest` タグは禁止されている |
| IO-4 | Low | `.dockerignore` ファイルの定義がない。AGENTS.md §12.5 で `bin/`, `obj/`, `.git/`, `*.md` の除外が必須 |

</details>

<details>
<summary>release-manager レビューレポート</summary>

### リリース戦略の評価

| 観点 | 判定 | 詳細 |
|------|------|------|
| CI/CD パイプライン | ✅ | GitHub Actions 定義あり |
| ロールバック計画 | ⚠️ | `az containerapp update` での更新は記述されているが、ロールバック手順がない |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| RM-1 | Medium | AI モデル更新（GPT-4o のバージョン変更等）時のリリース / ロールバック手順がない。Model deployment の変更と Application deployment の分離が必要 |
| RM-2 | Low | CI/CD パイプラインで `dotnet-version: '10.0.x'` が記述されているが、具体的なパッチバージョン固定の方針がない |

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### NuGet 依存関係の評価

| 観点 | 判定 | 詳細 |
|------|------|------|
| 必須パッケージ | ✅ | AGENTS.md §8.1 の必須パッケージを概ね含む |
| 禁止パッケージ | ✅ | 禁止パッケージの使用なし |
| プレリリース版 | ✅ | プレリリース版の使用なし |
| ライセンス | ✅ | Semantic Kernel は MIT ライセンス |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| OSS-1 | Medium | `Azure.Search.Documents` のバージョンが `11.*` と記載されているが、`nuget-dependency.instructions.md` の必須パッケージ一覧に含まれていない。AI サービス固有の依存として許容されるが、ライセンス（MIT）と CVE の確認を実装時に行うこと |

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### UX / アクセシビリティの評価

| 観点 | 判定 | 詳細 |
|------|------|------|
| チャット UX | ⚠️ | チャットボットの UI 要件は本設計書のスコープ外（front-end-need.md で定義） |
| エラーメッセージ | ✅ | RFC 9457 Problem Details 形式を採用 |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| UX-1 | Low | チャットボットの応答遅延時（Azure OpenAI の応答待ち）のユーザー体験（ローディング表示、タイムアウトメッセージ等）がバックエンド設計書で考慮されていない。ストリーミング応答（Server-Sent Events）の検討をフロントエンド要件と合わせて行うべき |

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 技術標準の横断適合性

| 観点 | 判定 | 詳細 |
|------|------|------|
| AGENTS.md 準拠 | ❌ | SSRF 防止、ミドルウェアパイプライン、Correlation ID、レジリエンス設計が欠如 |
| Instructions 準拠 | ⚠️ | コーディング規約は概ね準拠、セキュリティ規約に重大な違反あり |
| DDD 原則 | ⚠️ | Aggregate Root が明確に定義されていない |
| 実装実現可能性 | ✅ | 技術選定は妥当。Semantic Kernel の使い方も適切 |

**指摘一覧**:

| # | 重要度 | 指摘 |
|---|--------|------|
| TL-1 | High | SSRF 防止 × レジリエンス × ミドルウェアの 3 つの「欠如」は相互に関連しており、`Program.cs` の設計セクション全体を追加する必要がある。AGENTS.md §10、§11 を参照して一括対応すべき |
| TL-2 | High | spec.md との整合性が不十分。SSRF 防止設計（spec.md L1281-L1370）と DPIA 計画（spec.md L5049-L5070）が本設計書に反映されていない |
| TL-3 | High | ProductPlugin の DI パターン（従来コンストラクタ）が dotnet-coding-standards.instructions.md に違反 |
| TL-4 | Medium | 設計書に ADR への参照がない。特にベクトル DB の技術選定変更（Qdrant → Azure AI Search）を ADR-00XX として記録すべき |
| TL-5 | Medium | `ProductIndexSyncConsumer` の Kafka consumer で `IServiceScopeFactory` を使用する設計（AGENTS.md §10.6 準拠）だが、実際のコードではスコープ生成なしで直接 `SearchClient` を使用している。Scoped サービスの安全な利用パターンに修正が必要 |

</details>

---

## 修正優先度ガイド

### 即時対応（Critical — 設計フェーズ進行のブロッカー）
1. **SSRF 防止設計の追加**（統合指摘 #1）— spec.md §SSRF 防止設計を転記・統合
2. **OData フィルタインジェクション修正**（統合指摘 #2）— `SearchFilter.Create()` またはホワイトリスト検証
3. **API キーパターンの修正**（統合指摘 #3）— DefaultAzureCredential パターンに変更
4. **ユーザーデータの AI プロンプト送信修正**（統合指摘 #4）— 匿名化またはアーキテクチャ変更

### 早期対応（High — 実装開始前に解決すべき）
5. Program.cs ミドルウェアパイプライン定義の追加
6. Azure OpenAI / AI Search の Polly レジリエンスポリシー設計
7. ASP.NET Core レート制限設計の追加
8. GDPR データ保持期間・削除ポリシーの定義
9. DPIA 計画の参照と軽減措置の実装設計
10. EF Core エンティティの属性マッピング追加
11. Correlation ID 設計の追加
