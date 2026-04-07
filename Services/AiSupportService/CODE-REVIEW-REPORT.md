# AiSupportService 包括的コードレビューレポート

**レビュー実施日**: 2025年  
**レビュー対象**: `Services/AiSupportService/` — 83 .cs ファイル（約 6,368 行）+ `AiSupportService.Tests/`  
**レビュー方式**: 14 専門エージェントによる並列包括レビュー + Tech Lead 最終裁定

---

## 総合判定

| 項目 | 値 |
|------|-----|
| **判定** | ⚠️ **Warning** |
| **総合スコア** | **20 / 25** |
| **指摘件数（統合後）** | Critical: 1 / High: 8 / Medium: 10 / Low: 3 |

---

## エージェント別スコアサマリー

| # | エージェント | スコア | 判定 | 主要指摘 |
|---|-----------|--------|------|---------|
| 1 | architecture | 22/25 | ⚠️ | AsNoTracking データ損失バグ、Repository バイパス |
| 2 | ddd-domain | 19/30 | ⚠️ | Aggregate 境界違反、Anemic Model、匿名ドメインイベント |
| 3 | csharp-standards | 24/25 | ✅ | FaqPlugin/FaqRepository 不一致、TimeProvider 未伝搬 |
| 4 | config-di | 22/25 | ⚠️ | JWT 設定タイミング、IOptions 未使用 |
| 5 | api-endpoint | 20/25 | ⚠️ | パスパラメータ検証欠落、WithOpenApi 未設定 |
| 6 | data-access | 21/30 | ❌ | AsNoTracking データ損失、Migrations 空、ページネーション未使用 |
| 7 | async-concurrency | 25/25 | ✅ | CancellationToken 完全伝搬、禁止パターンゼロ |
| 8 | error-logging | 23/25 | ⚠️ | EF Core OTEL 未設定、HealthCheck コスト |
| 9 | security | —/25 | ⚠️ | IPv6 SSRF バイパス、IDOR 未検証、JWT アルゴリズム |
| 10 | performance | 16/25 | ⚠️ | ページネーション未実装×6、Select プロジェクション未使用 |
| 11 | resilience | 19/25 | ⚠️ | Polly パラメータ未設定、HealthCheck 対象不一致 |
| 12 | dependency | 22/25 | ⚠️ | パッケージバージョン不整合、TreatWarningsAsErrors 未設定 |
| 13 | test-quality | 17/25 | ⚠️ | 統合テスト不在、カバレッジ推定30-40% |
| 14 | tech-lead | 20/25 | ⚠️ | 最終裁定（本レポート） |

---

## 禁止事項横断チェック（全項目クリア）

| # | パターン | 検出数 | 判定 |
|---|---------|--------|------|
| 1 | `Console.WriteLine` / `Console.Error.WriteLine` | 0 | ✅ |
| 2 | `catch (Exception) { }` 例外の握りつぶし | 0 | ✅ |
| 3 | 秘密情報のハードコード | 0 | ✅ |
| 4 | `FromSqlRaw` 文字列結合 | 0 | ✅ |
| 5 | `new Service()` 直接インスタンス化 | 0 | ✅ |
| 6 | `.Result` / `.Wait()` | 0 | ✅ |
| 7 | `Thread.Sleep()` | 0 | ✅ |
| 8 | プロパティインジェクション `[Inject]` | 0 | ✅ |
| 9 | `DateTime.Now`（非UTC） | 0 | ✅ |
| 10 | `new HttpClient()` | 0 | ✅ |

---

## Top 10 優先修正事項

### 1. [Critical] 購買履歴データ損失バグ — T-01

| 項目 | 詳細 |
|------|------|
| **対象ファイル** | [UserProfileRepository.cs](Repositories/UserProfileRepository.cs#L14-L16), [OrderCreatedConsumer.cs](Infrastructure/Kafka/OrderCreatedConsumer.cs#L54-L66) |
| **検出エージェント** | architecture, data-access, performance |
| **概要** | `FindByUserIdAsync` が `AsNoTracking()` で UserProfile を返すが、`OrderCreatedConsumer` がこの結果に `AddPurchaseHistory()` で変更を加えても、EF Core Change Tracker に追跡されないため `SaveChangesAsync()` で DB に変更が反映されない |
| **影響** | ユーザーの購買履歴が永続化されない。AI レコメンデーション精度に直接影響 |
| **修正方針** | `IUserProfileRepository` に `FindByUserIdForUpdateAsync`（AsNoTracking なし）を追加し、`OrderCreatedConsumer` でそれを使用する |

```csharp
// IUserProfileRepository に追加
Task<UserProfile?> FindByUserIdForUpdateAsync(string userId, CancellationToken ct = default);

// UserProfileRepository に実装
public async Task<UserProfile?> FindByUserIdForUpdateAsync(string userId, CancellationToken ct = default)
    => await context.UserProfiles
        .FirstOrDefaultAsync(p => p.UserId == userId, ct);  // AsNoTracking なし

// OrderCreatedConsumer.cs — FindByUserIdForUpdateAsync に変更
var profile = await userProfileRepository.FindByUserIdForUpdateAsync(@event.UserId, stoppingToken);
```

---

### 2. [High] SSRF IPv6 プライベートアドレスバイパス — T-04

| 項目 | 詳細 |
|------|------|
| **対象ファイル** | [SsrfPreventionHandler.cs](Infrastructure/SemanticKernel/SsrfPreventionHandler.cs#L107-L122) |
| **検出エージェント** | security |
| **概要** | `IsPrivateOrLoopback` が IPv4 レンジのみチェック。`fc00::/7`（Unique Local）や `fe80::/10`（Link-Local）の IPv6 アドレスで SSRF バイパスが可能 |
| **修正方針** | IPv6 プライベートアドレスチェックを追加 |

```csharp
private static bool IsPrivateOrLoopback(IPAddress address)
{
    if (IPAddress.IsLoopback(address)) return true;

    if (address.AddressFamily == AddressFamily.InterNetworkV6)
    {
        var bytes = address.GetAddressBytes();
        if ((bytes[0] & 0xFE) == 0xFC) return true;   // fc00::/7
        if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80) return true;  // fe80::/10
        return false;
    }

    if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
    // ... existing IPv4 checks ...
}
```

---

### 3. [High] レコメンデーションフィードバック IDOR — T-08

| 項目 | 詳細 |
|------|------|
| **対象ファイル** | [RecommendationService.cs](Services/RecommendationService.cs#L293-L296) |
| **検出エージェント** | security |
| **概要** | `RecordFeedbackAsync` でレコメンデーション ID の所有権チェックがない。任意の認証済みユーザーが他ユーザーのレコメンデーションにフィードバック可能 |
| **修正方針** | `recommendation.UserId != userId` の検証を追加 |

```csharp
var recommendation = await recommendationRepository.FindByIdAsync(request.RecommendationId, ct)
    ?? throw new NotFoundException("レコメンデーションが見つかりません");

if (recommendation.UserId != "system" && recommendation.UserId != userId)
    throw new ForbiddenException("このレコメンデーションに対するフィードバック権限がありません");
```

---

### 4. [High] FaqPlugin / FaqRepository データ不一致 + 死コード — T-02

| 項目 | 詳細 |
|------|------|
| **対象ファイル** | [FaqPlugin.cs](Infrastructure/SemanticKernel/Plugins/FaqPlugin.cs#L18-L26), [FaqRepository.cs](Repositories/FaqRepository.cs) |
| **検出エージェント** | architecture, csharp-standards, data-access, performance |
| **概要** | `FaqPlugin` と `FaqRepository` が異なる FAQ データを保持（返品期限: 14日 vs 30日、送料無料ライン: ¥10,000 vs ¥5,000）。`FaqRepository` は DI 未登録で実質死コード |
| **修正方針** | ① ビジネス部門に正しい値を確認 ② 単一データソースに統一 ③ 死コード（`FaqRepository` + `IFaqRepository`）を削除するか DI 登録して統一 |
| **要人間判断** | 返品期限・送料無料ラインのどちらが正しいビジネス要件か確認が必要 |

---

### 5. [High] EF Core Migrations 未生成 — T-06

| 項目 | 詳細 |
|------|------|
| **対象** | `Services/AiSupportService/Migrations/`（空ディレクトリ） |
| **検出エージェント** | data-access |
| **概要** | マイグレーションファイルが一切存在しない。デプロイ時に DB スキーマが自動適用されない |
| **修正方針** | `dotnet ef migrations add InitialCreate` を実行 |

---

### 6. [High] テストカバレッジ不足（推定 30-40%） — T-07

| 項目 | 詳細 |
|------|------|
| **対象** | `Services/AiSupportService.Tests/` |
| **検出エージェント** | test-quality |
| **概要** | Unit Test 47 件のみ存在。統合テスト(Endpoints)・DB テスト(Repository)・セキュリティテストが全て不在。推定カバレッジ 30-40% で AGENTS.md 80% 基準に大幅未達 |
| **不足テスト** | Endpoints 統合テスト ~30件、Repository DB テスト ~20件、Service 追加テスト ~15件、セキュリティテスト ~10件 = 推定 75件以上 |
| **良い点** | 既存 47 テストは命名規約 100% 準拠、AAA パターン完全適用、Shouldly 統一 |

---

### 7. [High] ページネーション未結線 — T-05

| 項目 | 詳細 |
|------|------|
| **対象ファイル** | ChatService.cs, SearchService.cs, RecommendationService.cs, ForecastService.cs, ModelTrainingService.cs |
| **検出エージェント** | api-endpoint, performance |
| **概要** | Repository に `*PagedAsync` メソッドが実装済みだが、Service/Endpoint で**一切使用されていない**。全件取得によりメモリ圧迫・レスポンス遅延リスク |
| **影響を受けるエンドポイント** | `GET /sessions`, `GET /messages`, `GET /search`, `GET /recommendations/personalized`, `GET /forecast`, `GET /models` |

---

### 8. [High] IChatMessageRepository Aggregate 境界違反 — T-03

| 項目 | 詳細 |
|------|------|
| **対象ファイル** | [IChatMessageRepository.cs](Repositories/Interfaces/IChatMessageRepository.cs) |
| **検出エージェント** | ddd-domain |
| **概要** | `IChatMessageRepository` に `AddAsync` / `SaveChangesAsync` が独立定義。ChatSession Aggregate Root の境界を破り、ChatSession を介さずにメッセージを直接追加可能 |
| **修正方針** | 書込メソッドを削除し、ChatSession 経由の `AddMessage()` → `IChatSessionRepository.SaveChangesAsync()` に統一 |

---

### 9. [Medium] DataRetention finally ブロックの CancellationToken — T-14

| 項目 | 詳細 |
|------|------|
| **対象ファイル** | [DataRetentionCleanupService.cs](Infrastructure/Kafka/DataRetentionCleanupService.cs#L95-L97) |
| **検出エージェント** | async-concurrency |
| **概要** | `finally` ブロック内のアドバイザリロック解放で `stoppingToken` を使用。シャットダウン時にキャンセル済みの場合、ロック解放が失敗する |
| **修正** | `stoppingToken` → `CancellationToken.None` に変更 |

---

### 10. [Medium] InputSanitizer / ResponseFilter 正規表現の事前コンパイル — T-10

| 項目 | 詳細 |
|------|------|
| **対象ファイル** | [InputSanitizer.cs](Infrastructure/SemanticKernel/InputSanitizer.cs#L47-L51), [ResponseFilter.cs](Infrastructure/SemanticKernel/ResponseFilter.cs#L28-L33) |
| **検出エージェント** | csharp-standards, performance |
| **概要** | ループ内で `Regex.IsMatch()` / `Regex.Replace()` に毎回パターン文字列を渡している。事前コンパイル済み `Regex[]` 配列にすべき |

---

## その他の Medium 指摘事項

| ID | 概要 | 対象ファイル |
|----|------|------------|
| T-11 | OpenTelemetry に `AddEntityFrameworkCoreInstrumentation()` 未設定 | Program.cs |
| T-12 | 全エンドポイントグループに `.WithOpenApi()` 未設定 | Endpoints/ 全 6 ファイル |
| T-13 | AzureOpenAIHealthCheck が毎回 ChatCompletion API を呼び出し（コスト・DoS リスク） | AzureOpenAIHealthCheck.cs |
| T-15 | Kafka BootstrapServers を `Configuration["..."]` で直接取得（IOptions 未使用） | Program.cs |
| T-16 | JWT 設定の `GetSection().Get<>()` と IOptions の二重バインド | Program.cs |
| T-17 | ConcurrencyRetryHelper のデリゲートが CancellationToken を受け取らない | ConcurrencyRetryHelper.cs |
| T-18 | Analytics エンドポイントの from/to 日付範囲バリデーションなし | AnalyticsEndpoints.cs |
| T-19 | ResponseFilter で Regex ループ内再コンパイル | ResponseFilter.cs |

---

## 競合裁定

| # | 競合内容 | 裁定結果 |
|---|---------|----------|
| 1 | `IChatMessageRepository` の重要度（ddd-domain: Critical vs architecture: 認容） | **High に降格**。読取系は CQRS 的分離として許容。書込系メソッドのみ Aggregate 違反 |
| 2 | `Kernel` の Scoped 登録コスト（performance: High vs config-di: 必要） | **現状維持**。プラグインが Scoped 依存を持つため Singleton 化不可。性能問題は計測後に対策検討 |
| 3 | `OutboxPublisher` の Repository バイパス（architecture: High vs 実装慣例: 許容） | **Medium に降格**。BackgroundService 内での直接 DbContext 使用は .NET エコシステムの標準慣例 |
| 4 | HealthCheck の ChatCompletion 呼び出し（security: Medium vs architecture: 標準） | **Medium 維持**。キャッシュ付き応答への変更を推奨 |

---

## 特筆すべき優秀な実装

| # | 観点 | 詳細 |
|---|------|------|
| 1 | **CancellationToken 伝搬** | 全 async メソッドに `ct = default` 完備。Endpoint → Service → Repository → EF Core の全チェーンで 100% 伝搬 |
| 2 | **禁止パターンゼロ** | 全 10 禁止パターン（Console.WriteLine, .Result, Thread.Sleep 等）が一切検出されず |
| 3 | **構造化ログ** | 全ファイルでメッセージテンプレート形式。文字列補間ゼロ。PII ログゼロ |
| 4 | **ミドルウェア順序** | ExceptionHandler→HSTS→CorrelationId→Serilog→CORS→Auth→RateLimiter→Endpoints の順序が AGENTS.md §11.3 に完全準拠 |
| 5 | **BackgroundService 品質** | 6 サービス全てが stoppingToken 完全伝搬 + IServiceScopeFactory + 例外分離 + バックオフ |
| 6 | **テスト命名規約** | 既存 47 テスト全てが `Should_X_When_Y` パターン 100% 準拠、AAA パターン完全適用 |
| 7 | **プロンプトインジェクション防止** | InputSanitizer + SystemPrompt 分離 + ResponseFilter (PII マスキング) の多層防御 |
| 8 | **SSRF 防止** | SsrfPreventionHandler で DNS 解決後の IP 検証 + 許可サービスエンドポイントのホワイトリスト |
| 9 | **Outbox パターン** | 動的バックオフ（100ms〜5s 指数増加）+ PENDING→PUBLISHED 状態管理 + ProduceException 分離 |
| 10 | **DI 設計** | primary constructor 統一、全サービスが interface 経由、Scoped ライフタイム適切 |

---

## エスカレーション事項（要人間判断）

| # | 事項 | 判断ポイント |
|---|------|------------|
| 1 | **FAQ データの正 (SSOT)** | FaqPlugin（14日/¥10,000）と FaqRepository（30日/¥5,000）のどちらが正しい返品期限・送料無料ラインか、ビジネス部門に確認が必要 |
| 2 | **Azure OpenAI HealthCheck 方針** | ChatCompletion API 呼び出しによる課金コスト vs Readiness 精度のトレードオフ |
| 3 | **テストカバレッジ投資** | 80% 目標達成には推定 75 件以上のテスト追加が必要。リリースまでのスコープ判断 |
| 4 | **購買履歴データ復旧** | T-01 修正後、Kafka `order.created` トピックのリプレイまたは SalesManagementService からのデータ再取込によるデータ復旧の検討 |

---

## カテゴリ別スコア

| カテゴリ | スコア (1-5) | 備考 |
|---------|-------------|------|
| 禁止事項遵守 | **5/5** | 全 10 禁止パターン検出ゼロ |
| 設計書との整合性 | **3/5** | FAQ 二重管理、ページネーション未結線、IDOR 違反、Migrations 未生成 |
| コード一貫性 | **4/5** | primary constructor・record DTO・例外階層・ログ形式が統一 |
| 命名規則遵守 | **5/5** | PascalCase/camelCase/_camelCase の使い分け完全適合 |
| 総合コード品質 | **3/5** | AsNoTracking データ損失(C)、SSRF バイパス(H)、テスト不足(H) |
| **総合スコア** | **20/25** | |
