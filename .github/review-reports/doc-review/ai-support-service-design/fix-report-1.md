# 修正レポート: ai-support-service-design.md

## 修正概要

| 区分 | 検出数 | 修正数 | 残数 |
|------|--------|--------|------|
| **Critical** | 4 | 4 | 0 |
| **High** | 17 | 17 | 0 |
| **合計** | 21 | 21 | 0 |

修正日時: 2026-04-03
対象ファイル: `design-docs/ai-support-service-design.md`
行数変化: 1562 行 → 2250 行（+688 行）

---

## Critical 修正詳細

### C-1: SSRF 防止設計の完全欠如 → **修正済**
- **対応**: §12.6 を新設。spec.md §SSRF 防止設計を転記・統合
- **内容**: URL ホワイトリスト（5 ドメイン）、プライベート IP 拒否テーブル（8 CIDR）、DNS リバインディング対策、リクエストサイズ制限、`SsrfPreventionHandler` の C# 実装コード、Program.cs での HttpClient 登録パターン

### C-2: OData フィルタインジェクション脆弱性 → **修正済**
- **対応**: §8.1 `SearchService` のフィルタ構築をホワイトリスト検証に変更
- **内容**: `AllowedCategories` HashSet による許可カテゴリのホワイトリスト検証を追加。不正カテゴリ値は `BusinessException` で拒否。`SearchRequest` DTO にも `[RegularExpression]` バリデーションを追加

### C-3: API キー直接取得パターン → **修正済**
- **対応**: §7.1 Kernel セットアップを `DefaultAzureCredential` パターンに差し替え
- **内容**: `apiKey` パラメータを `credentials: new DefaultAzureCredential()` に変更。`!`（null 強制許容演算子）を `?? throw new InvalidOperationException()` に変更。`AddSingleton` → `AddScoped` に変更（High #14 と同時修正）。`Azure.Identity` パッケージを技術スタック表に追加。§16 の注記も更新

### C-4: ユーザー購入履歴の AI プロンプト直接埋め込み → **修正済**
- **対応**: §9.2 のプロンプト構築を匿名化サマリ方式に変更
- **内容**: `AnonymizeUserPreferences` メソッドを追加。購入履歴の生データではなく、カテゴリ傾向・価格帯・スキルレベルの匿名化サマリのみを AI に送信する設計に変更

---

## High 修正詳細

### H-5: プロンプトインジェクション対策がブラックリスト方式 → **修正済**
- §12.3 を多層防御（5 層）に全面改訂: ① システムプロンプトガードレール ② Azure Content Safety ③ 入力長制限 ④ 応答後処理フィルタ ⑤ 補助的ブラックリスト

### H-6: Polly レジリエンスポリシー未設計 → **修正済**
- §12a を新設。Azure OpenAI / Azure AI Search への `AddStandardResilienceHandler` 設定（リトライ・サーキットブレーカー・タイムアウト）を追加。`Microsoft.Extensions.Http.Resilience` を技術スタック表に追加

### H-7: Program.cs ミドルウェアパイプライン欠如 → **修正済**
- §12c を新設。AGENTS.md §11.3 準拠のミドルウェア登録順序（ExceptionHandler → HSTS → セキュリティヘッダー → Correlation ID → Serilog → CORS → Authentication → Authorization → RateLimiter → Endpoints → HealthChecks）を記載

### H-8: レート制限未設計 → **修正済**
- §12.5 を新設。`AddRateLimiter` による 4 種類のレート制限を定義:
  - チャット API: TokenBucket 10 req/min/user
  - 検索 API: FixedWindow 60 req/min/IP
  - レコメンデーション API: 30 req/min/user
  - 管理者 API: 20 req/min/admin

### H-9: GDPR データ保持期間・削除ポリシー未定義 → **修正済**
- §12.7 を新設。6 テーブルの保持期間定義、GDPR 削除権（Art.17）カスケード削除フロー、`DataRetentionCleanupService` BackgroundService のコード例を追加

### H-10: DPIA 計画未反映 → **修正済**
- §12.8 を新設。spec.md §DPIA 計画を参照し、4 つの軽減措置（オプトアウト、透明性表示、人間の介入、バイアスチェック）の実装設計を追加

### H-11: Correlation ID 欠如 → **修正済**
- §12b を新設。`X-Correlation-Id` ヘッダーの受け取り・生成・伝搬設計、`LogContext.PushProperty` でのログコンテキスト注入、4 つの通信先への伝搬ルールを記載

### H-12: EF Core エンティティの [Table]/[Column] 属性欠如 → **修正済**
- §12d を新設。`ChatSession`, `ChatMessage`, `UserProfile`, `Recommendation` の 4 エンティティに `[Table]`, `[Column]`, `[Key]`, `[MaxLength]`, `[Required]` 属性を付与したコード例を追加

### H-13: created_by/updated_by 監査カラム欠如 → **修正済**
- §5.2 `model_trainings` テーブルに `created_by` カラムを追加。他テーブルについては省略理由を注記で明記（`recommendations`/`search_analytics`/`demand_forecasts` はシステム自動生成、`user_profiles`/`chat_sessions` は `user_id` が作成者を示す）

### H-14: Kernel の Singleton 登録 → **修正済**
- §7.1 で `AddSingleton` → `AddScoped` に変更（Critical #3 と同時修正）

### H-15: 会話履歴の全件取得スケーラビリティ問題 → **修正済**
- §7.3 で `FindBySessionIdAsync` → `FindRecentBySessionIdAsync(sessionId, maxMessages: 20, ct)` に変更。スライディングウィンドウ方式のコメントを追加

### H-16: ProductPlugin が従来コンストラクタ → **修正済**
- §7.2 で `public class ProductPlugin(IProductClient productClient)` の primary constructor パターンに変更

### H-17: テストカバレッジ目標不足 → **修正済**
- §15.2 を新設（元 §15.2 は §15.3 に繰り下げ）。分岐カバレッジ 80% 目標の明記、AI 品質テストの合否判定基準（4 項目）、Service 層の必須テストケース一覧（正常系/異常系）を追加

---

## その他の関連修正

| 修正内容 | 箇所 |
|---------|------|
| DB名 `skishopdb` → `aisupportdb` | §3, §4.1 Mermaid 図, §19 CI/CD |
| 全テーブルの `TIMESTAMP` → `TIMESTAMP WITH TIME ZONE` | §5.2 全 7 テーブル |
| CHECK 制約値を UPPER_CASE + SQL 制約として定義 | §5.2 `chat_sessions.status`, `chat_messages.role`, `recommendations.type`, `search_analytics.search_type`, `demand_forecasts.forecast_period`, `model_trainings.status` |
| FK/クエリ用インデックス追加 | `user_profiles(user_id, last_activity_at)`, `recommendations(user_id, expires_at)`, `search_analytics(user_id, created_at, query)`, `demand_forecasts(product_id+forecast_date, product_id)` |
| OpenTelemetry 設定コード例追加 | §14.2 新設 |
| フォールバック戦略追加 | §12a.2 新設（4 障害パターン） |
| `Azure.Identity`, `Microsoft.Extensions.Http.Resilience` パッケージ追加 | §2 技術スタック表 |
| `SearchRequest` DTO に `[RegularExpression]`, `[Range]` 追加 | §6.6 |
| `DEFAULT CURRENT_TIMESTAMP` を全 `created_at`/`updated_at` に追加 | §5.2 全テーブル |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 内容 | 判断者 |
|---|--------|------|--------|
| 1 | 最優先 | DPIA が開発フェーズ開始前に完了済みか確認が必要 | プライバシー責任者 / DPO |
| 2 | 最優先 | Azure OpenAI へのデータ送信と越境データ移転の整合性 | 法務チーム |
| 3 | 高優先 | ベクトル DB の技術選定変更（spec.md の Qdrant → Azure AI Search）を ADR として記録すべきか | テックリード |
| 4 | 通常 | Azure OpenAI の TPM 制限とピーク時スロットリング対策 | SRE / インフラ |

---

## 修正後のカバレッジ分析

| セクション | 修正前 | 修正後 |
|-----------|--------|--------|
| セキュリティ（SSRF） | ❌ 欠如 | ✅ §12.6 |
| セキュリティ（レート制限） | ❌ 欠如 | ✅ §12.5 |
| セキュリティ（プロンプトインジェクション） | ⚠️ ブラックリストのみ | ✅ §12.3 多層防御 |
| GDPR / データ保持 | ❌ 欠如 | ✅ §12.7 |
| GDPR / DPIA | ❌ 欠如 | ✅ §12.8 |
| ミドルウェアパイプライン | ❌ 欠如 | ✅ §12c |
| 耐障害性 | ❌ 欠如 | ✅ §12a |
| Correlation ID | ❌ 欠如 | ✅ §12b |
| EF Core エンティティ属性 | ❌ 欠如 | ✅ §12d |
| テストカバレッジ目標 | ❌ 不足 | ✅ §15.2 |
| DB スキーマ制約 | ⚠️ 部分的 | ✅ CHECK + インデックス + TIMESTAMP WITH TIME ZONE |
