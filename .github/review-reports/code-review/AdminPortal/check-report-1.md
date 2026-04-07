# ソースコードレビュー統合レポート — AdminPortal 個別評価

## 判定結果

- **対象**: Services/AdminPortal/
- **判定**: ❌ **Rejected** — Critical 5 件 / High 12 件
- **レビュー日時**: 2026-04-07 21:13
- **プロジェクト**: SkiShop (.NET 10 マイクロサービス EC サイト) — 管理ポータル

---

## AdminPortal 固有指摘サマリー

| 重要度 | 件数 | 概要 |
|:------:|:----:|------|
| Critical | 5 | JWT 未転送、SRI 欠落、Data Annotations 全欠落、ログイン検証なし、API エラーボディ生ログ |
| High | 12 | CSP 欠落、Error.cshtml 不在、OpenTelemetry 未設定、POST try-catch 欠落、ProblemDetails 未対応 等 |
| Medium | 14 | IOptions 未使用、API パス不整合、htmx 未活用 等 |
| Low | 5 | record 化推奨、AntiForgeryToken 自動適用 等 |

---

## Critical 指摘（AdminPortal 固有）

| # | 出典 Agent | 対象ファイル | 指摘内容 |
|---|-----------|------------|----------|
| C-01 | security, arch, tech-lead | `Login.cshtml.cs` L34-38 | **JWT トークン未転送**: ログインで取得した JWT が保存されず、後続の API Gateway 呼出しに Bearer ヘッダーが付与されない |
| C-02 | arch, security | `_Layout.cshtml` L109 | **htmx SRI 欠落**: unpkg CDN からの読み込みに integrity 属性なし。Bootstrap Icons (L10-12) も同様 |
| C-03 | api-endpoint | 全 PageModel | **Data Annotations 全欠落**: `[BindProperty]` の全プロパティに `[Required]` / `[StringLength]` / `[Range]` 等なし |
| C-04 | api-endpoint | `Login.cshtml.cs` L29-31 | **ログインフォーム検証なし**: Email 形式・パスワード長の検証なしで不正入力送信可能 |
| C-05 | api-endpoint | `Products/Create.cshtml.cs` L88 | **API エラーレスポンスの生ボディ ログ出力**: SQL エラー/スタックトレース等の内部情報漏洩リスク |

## High 指摘（AdminPortal 固有）

| # | 出典 Agent | 対象ファイル | 指摘内容 |
|---|-----------|------------|----------|
| H-01 | config-di, security | `Program.cs` L79-86 | CSP ヘッダー未設定 |
| H-02 | arch, error-logging | `Program.cs` L72 | `UseExceptionHandler("/Admin/Error")` 参照先 Error.cshtml 不在 |
| H-03 | config-di, error-logging | `Program.cs` | OpenTelemetry パッケージ参照あるが初期化コードなし |
| H-04 | config-di | `Program.cs` | Correlation ID ミドルウェア未実装 |
| H-05 | config-di | `Program.cs` | `UseRateLimiter()` 未実装 |
| H-06 | resilience | `Program.cs` L97 | Readiness ヘルスチェック (`/health/ready`) 未実装 |
| H-07 | error-logging | 5 ページ | POST 系ハンドラー try-catch 未実装（Users, Coupons, Points, Campaigns, Mails） |
| H-08 | api-endpoint | 全ページ | ProblemDetails パース基盤なし |
| H-09 | arch | `_Layout.cshtml` | htmx ロード済みだが全ページで未使用。Alpine.js 未導入 |
| H-10 | dependency | `AdminPortal.csproj` | JwtBearer / FluentValidation / OpenTelemetry パッケージ参照あるが未使用 |
| H-11 | arch | 複数ページ | API パスプレフィックス `/admin/` と `/api/v1/` の混在 |
| H-12 | ddd | `Orders/Detail.cshtml.cs` L47-55 | Staff ロール制限ビジネスルールの BFF 漏洩 |

---

## Frontend との品質差分

AdminPortal は Frontend と比較して以下の品質基盤が不足:

| 品質要素 | Frontend | AdminPortal | Gap |
|---------|:--------:|:-----------:|:---:|
| API クライアント抽象化 | ✅ 16 ApiClient クラス | ❌ HttpClient 直接使用 | 高 |
| CSP ヘッダー | ✅ SecurityHeadersMiddleware | ❌ 未実装 | 高 |
| OpenTelemetry | ✅ メトリクス + トレース | ❌ パッケージのみ | 高 |
| ProblemDetails 対応 | ✅ ApiErrorHandler | ❌ なし | 高 |
| エラーハンドリング統一 | ⭐⭐⭐⭐ | ⭐⭐ | 中 |
| JWT トークン管理 | ✅ Cookie + DelegatingHandler | ❌ トークン未保存 | 致命的 |
| CancellationToken | ⭐⭐⭐ (コンポーネントで不足) | ⭐⭐⭐⭐⭐ (全 PageModel) | Frontend が劣る |
| C# 14 活用 | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | 同等 |

---

## 修正優先度（AdminPortal 固有ロードマップ）

### 即時（マージ前必須）
1. JWT トークン転送アーキテクチャ設計 + 実装
2. htmx / Bootstrap Icons に SRI 属性追加
3. 全 BindProperty に Data Annotations 追加
4. ログインフォーム入力検証追加
5. API エラーボディのサニタイズログ

### 短期（1-2 週間）
6. CSP ヘッダー追加（SecurityHeadersMiddleware 移植推奨）
7. Error.cshtml ページ作成
8. POST ハンドラー try-catch 追加（5 ページ）
9. OpenTelemetry 初期化コード追加
10. Correlation ID ミドルウェア導入

### 中期（次スプリント）
11. API クライアント抽象化層新設
12. ProblemDetails パース基盤導入
13. htmx 活用 or 除去の設計判断
14. UseRateLimiter 導入
15. Readiness ヘルスチェック追加
