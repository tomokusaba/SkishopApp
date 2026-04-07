# Fix Report — ai-support-service-design.md（Iteration 2）

## 概要

| 項目 | 値 |
|------|------|
| 対象ファイル | `design-docs/ai-support-service-design.md` |
| チェックレポート | `check-report-2.md` |
| 修正日 | 2026-04-03 |
| 修正対象 | High 7 件 |
| 修正完了 | **7 / 7 件（100%）** |
| 残存 | 0 件 |

## 修正詳細

### H-1: Semantic Kernel 内部 HTTP クライアントがレジリエンスパイプラインを迂回（§7.1）✅

**問題**: `kernelBuilder.AddAzureOpenAIChatCompletion()` は Semantic Kernel 内部の HttpClient を使用するため、§12a.1 の Named HttpClient（`"AzureOpenAI"`）に設定した `AddStandardResilienceHandler` のリトライ・サーキットブレーカー・タイムアウトが適用されなかった。

**修正内容**:
- `IHttpClientFactory.CreateClient("AzureOpenAI")` で取得したレジリエンス設定済み HttpClient を `httpClient:` パラメータで Semantic Kernel コネクタに注入
- `AddAzureOpenAIChatCompletion` / `AddAzureOpenAITextEmbeddingGeneration` 両方に適用
- 設計意図を説明する注記ブロック（`> **Semantic Kernel と HttpClient レジリエンスの統合**`）を追加

### H-2: Bicep が `listKeys().key1` を使用し DefaultAzureCredential と矛盾（§18.3）✅

**問題**: Bicep テンプレートが `openAi.listKeys().key1` / `searchService.listAdminKeys().primaryKey` で API キーを secrets に設定し、環境変数 `AzureOpenAI__ApiKey` / `AzureSearch__ApiKey` で渡していたが、コード側は `DefaultAzureCredential`（Managed Identity）を使用。

**修正内容**:
- `secrets` セクションから API キー関連を全削除
- Container App に `identity: { type: 'SystemAssigned' }` を追加
- 環境変数 `AzureOpenAI__ApiKey` / `AzureSearch__ApiKey` を削除、代わりに `AzureOpenAI__ChatDeployment` / `AzureOpenAI__EmbeddingDeployment` を追加
- `Cognitive Services OpenAI User` ロール（`a97b65f3-...`）の RBAC 割当リソースを追加
- `Search Index Data Reader` ロール（`1407120a-...`）の RBAC 割当リソースを追加
- **付随修正（M-8 解消）**: `image` タグを `latest` → `${containerImageTag}` に変更

### H-3: `outbox_events` テーブル未定義（ADR-0005 準拠）（§5.2）✅

**問題**: §11.2 で `RecommendationGenerated`、`ChatSessionEscalated`、`ForecastGenerated` の 3 イベントを Kafka 発行すると定義しているが、ADR-0005（Outbox パターン）に必要な `outbox_events` テーブルが §5.2 に存在しなかった。

**修正内容**:
- §5.2 の `model_trainings` テーブル後に `outbox_events` テーブル DDL を追加（AGENTS.md §10.4 準拠）
  - カラム: `id`, `aggregate_type`, `aggregate_id`, `event_type`, `topic`, `payload`, `status`（PENDING/PUBLISHED/FAILED）, `retry_count`, `error_message`, `created_at`, `published_at`
  - インデックス: `idx_outbox_events_status_created`, `idx_outbox_events_aggregate`
- `OutboxPublisher` BackgroundService の設計注記を追加（動的バックオフ 100ms〜5s、最大 5 回リトライ）
- §12d に `OutboxEvent` EF Core エンティティ定義を追加（`[Table]`/`[Column]`/`[Key]`/`[MaxLength]` 属性付き）

### H-4: `AddFromType<ProductPlugin>()` が Kernel DI からホスト側サービスを解決できない（§7.1）✅

**問題**: `AddFromType<T>()` は Kernel 内部の `ServiceProvider` から依存を解決するが、`ProductPlugin` が依存する `IProductClient` はホスト `ApplicationServices` にのみ登録されており、Kernel の `ServiceCollection` には未登録。プラグイン解決時に `InvalidOperationException` が発生する可能性。

**修正内容**:
- `AddFromType<ProductPlugin>()` → `AddFromObject(new ProductPlugin(sp.GetRequiredService<IProductClient>()))` に変更
- `OrderPlugin`、`FaqPlugin` も同様に `AddFromObject()` パターンに変更
- 設計意図を説明する注記ブロック（`> **プラグイン DI 解決**`）を追加

### H-5: SSRF ハンドラー `BlockedNetworks` に IPv6 アドレス範囲が欠落（§12.6）✅

**問題**: §12.6 のテーブルには `::1/128`、`fc00::/7`、`0.0.0.0/8` が記載されていたが、C# コードの `BlockedNetworks` 配列には IPv4 の 5 エントリのみ。IPv6 経由の SSRF バイパスリスク。

**修正内容**:
- `BlockedNetworks` 配列に以下を追加:
  - `IPNetwork.Parse("0.0.0.0/8")` — 現在のネットワーク
  - `IPNetwork.Parse("::1/128")` — IPv6 ループバック
  - `IPNetwork.Parse("fc00::/7")` — IPv6 ユニークローカルアドレス
  - `IPNetwork.Parse("fe80::/10")` — IPv6 リンクローカルアドレス

### H-6: グローバル例外ハンドラーに RFC 9457 Problem Details マッピングが未定義（§12c）✅

**問題**: `app.UseExceptionHandler()` のみ記載。AGENTS.md §4.7 の例外クラス → HTTP ステータスコードマッピングと RFC 9457 (ADR-0007) 準拠の Problem Details レスポンス生成が欠如。

**修正内容**:
- `app.UseExceptionHandler()` を `UseExceptionHandler(exceptionHandlerApp => { ... })` のラムダ付きコードに展開
- AGENTS.md §4.7 準拠の `switch` 式マッピングを追加:
  - `NotFoundException` → 404、`BusinessException` → 422、`UnauthorizedException` → 401、`ForbiddenException` → 403、`ConcurrencyException` → 409、その他 → 500
- `ILogger<Program>` による適切なログレベル出力（500 系は `Error`、その他は `Warning`）

### H-7: §20 制約 #3 が「API キーは環境変数で管理」のまま Managed Identity と矛盾 ✅

**問題**: §20 制約 #3 に「Azure OpenAI の API キーは環境変数で管理し、appsettings.json に記述しない」と古い記述が残存。C-3 修正で `DefaultAzureCredential` に変更済みのため矛盾。

**修正内容**:
- 制約 #3 を「本番環境では `DefaultAzureCredential`（Managed Identity）で Azure OpenAI / Azure AI Search に認証するため、API キーは不要。開発環境で API キーが必要な場合は `dotnet user-secrets` で管理し、appsettings.json にも環境変数にも直接記述しない」に更新

## 付随修正

| # | 重要度 | 修正内容 |
|---|--------|---------|
| M-8 | Medium | Bicep の `image` タグを `latest` → `${containerImageTag}` に変更（H-2 修正に含む） |

## 行数変化

| 修正前 | 修正後 | 差分 |
|--------|--------|------|
| 2250 行 | ~2400 行 | +~150 行 |
