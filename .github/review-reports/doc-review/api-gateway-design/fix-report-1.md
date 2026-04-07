# 修正レポート: api-gateway-design.md

## 修正サマリー

| カテゴリ | 検出数 | 修正数 | 残存数 |
|---------|-------|-------|-------|
| **Critical** | 1 | 1 | 0 |
| **High** | 18 | 18 | 0 |
| **Medium** | 21 | 6 | 15 |
| **Low** | 3 | 0 | 3 |

- **修正日時**: 2026-04-03
- **対象ファイル**: `design-docs/api-gateway-design.md`（393 行 → 918 行）
- **判定変更**: ❌ Rejected → 再レビュー待ち（Critical 0 件、High 0 件）

---

## Critical 修正詳細

| # | 指摘 ID | 修正内容 | 修正箇所 |
|---|--------|---------|---------|
| 1 | C-1 | エラーレスポンス形式を RFC 9457 Problem Details に全面書き換え。`TypedResults.Problem()` ベースの JSON 形式（`type`, `title`, `status`, `detail`, `instance`, `extensions`）に変更。グローバル例外ハンドラーの C# コード例を追加（ADR-0007 準拠） | §11 グローバルエラーレスポンス形式 |

## High 修正詳細

| # | 指摘 ID | 修正内容 | 修正箇所 |
|---|--------|---------|---------|
| 1 | H-1 | セキュリティレスポンスヘッダー 7 種（`X-Content-Type-Options`, `X-Frame-Options`, `Content-Security-Policy`, `Strict-Transport-Security`, `X-XSS-Protection`, `Referrer-Policy`, `Permissions-Policy`）の具体値テーブルと C# 実装例を追加 | §6 セキュリティレスポンスヘッダー |
| 2 | H-2 | CORS ポリシー設定セクションを追加。`allowedOrigins`, `allowedMethods`, `allowedHeaders`, `maxAge` の具体値を spec.md と整合。ワイルドカード CORS 禁止を明記 | §6 CORS ポリシー設定 |
| 3 | H-3 | Correlation ID ヘッダー名を `X-Request-ID` → `X-Correlation-Id` に統一（spec.md / AGENTS.md §11.2 準拠） | §5 ミドルウェアパイプライン |
| 4 | H-4 | レート制限の値・単位・アルゴリズムを spec.md §レート制限設計と完全整合。Token Bucket / Fixed Window のアルゴリズム使い分け、req/min 単位、キーリゾルバ（IP / userId）を明記。`Retry-After` ヘッダー付与を記載 | §8 レート制限設定 |
| 5 | H-5 | YARP `ReverseProxy` の appsettings.json 設定例を追加（Routes / Clusters / Transforms / HealthCheck / LoadBalancingPolicy）。全バックエンドサービスのクラスタ定義を含む | §13 YARP リバースプロキシ設定（新設） |
| 6 | H-6 | MailSendService を「内部専用サービス（Kafka イベント駆動のみ、API Gateway 経由のルーティングなし）」として明示注記。Mermaid 図の「監視サービス」を MailSendService（破線接続）に変更 | §4 ルートテーブル注記、§3 Mermaid 図 |
| 7 | H-7 | ミドルウェアパイプラインを spec.md 準拠の番号付きリスト（8 段階順序）に変更。禁止パターンを注記 | §5 ミドルウェアパイプライン登録順序 |
| 8 | H-8 | .NET Aspire `AppHost/Program.cs` の ApiGateway 登録例を追加。全バックエンドサービスへの `WithReference` 接続を明示 | §15 .NET Aspire オーケストレーション統合（新設） |
| 9 | H-9 | p95（< 50ms）/p99（< 100ms）パーセンタイルレイテンシ目標を追加。spec.md の Saga レイテンシバジェット（API Gateway 通過 30ms）との整合性を注記 | §12 パフォーマンスメトリクス |
| 10 | H-10 | 認証キャッシュ無効化戦略を追加。Kafka イベント `user.permission_changed` での即時無効化、ログアウト時のトークンハッシュ無効化リスト、TTL を 10 分 → 5 分に短縮 | §12 キャッシュ戦略 |
| 11 | H-11 | Docker バージョンを `Latest` → `25.x` に修正。`latest` タグ禁止を明記 | 技術スタックテーブル |
| 12 | H-12 | `AZURE_CLIENT_SECRET` を環境変数から削除。Managed Identity（`DefaultAzureCredential`）前提の設計を明確化。開発環境用 `az login` / `dotnet user-secrets` 方式を注記 | §10 環境変数 |
| 13 | H-13 | テスト戦略セクションを新設。Unit Test / Integration Test / サーキットブレーカーテスト / レート制限テスト / セキュリティテスト / パフォーマンステストの種別別方針を記載。テストメソッド命名規則を含む | §16 テスト戦略（新設） |
| 14 | H-14 | Correlation ID ミドルウェアの詳細設計を追加。生成ロジック（ヘッダー取得 or 新規 UUID）、レスポンスヘッダー付与、`LogContext.PushProperty` 連携、バックエンド転送方法を明記。C# コード例を追加 | §5 Correlation ID ミドルウェア詳細設計 |
| 15 | H-15 | ログのセンシティブデータマスキング設計を追加。Authorization ヘッダー、Cookie、ログインパスワード、メールアドレス、IP アドレスのマスキング方針テーブルを記載 | §9 センシティブデータマスキング |
| 16 | H-16 | サーキットブレーカー Open 時のフォールバック戦略テーブルを追加。サービス別のフォールバック（503 即時返却、キャッシュ応答、デフォルト値、静的リスト）を定義 | §7 フォールバック戦略 |
| 17 | H-17 | `Program.cs` の DI 登録・ミドルウェア構成の全体設計を追加。Serilog、YARP、認証・認可、CORS、レート制限、ヘルスチェック、OpenTelemetry の DI 登録例と、§5 準拠のミドルウェアパイプライン順序のコード例を記載 | §14 Program.cs 構成設計（新設） |
| 18 | H-18 | `/api/cart/**` の認証設定を見直し。`POST /api/cart/items`（カート追加）は AllowAnonymous（ゲスト購入対応: Cookie ベース CartId）、`POST /api/cart/checkout` は認証必須のように、エンドポイント単位で認可ポリシーを定義 | §4 ルートテーブル |

## Medium 修正（付随対応）

| # | 指摘 ID | 修正内容 |
|---|--------|---------|
| 1 | M-1 | Dockerfile HEALTHCHECK `--timeout=3s` → `--timeout=10s` に変更 |
| 2 | M-2 | Dockerfile HEALTHCHECK `--start-period=60s` → `--start-period=30s` に変更 |
| 3 | M-3 | 必須 NuGet パッケージ（Polly 8.*, FluentValidation 11.*, Serilog.Sinks.Console 6.*, AspNetCore.HealthChecks.Redis 9.*）を技術スタックテーブルに追加 |
| 4 | M-4 | Kafka バージョンを `7.4.0` → `3.x (Confluent Platform 7.4.0)` に明確化 |
| 5 | M-5 | Mermaid 図の「監視サービス（MONITOR）」を削除し、MailSendService（内部専用、破線接続）に変更 |
| 6 | M-10 | 環境変数セクションのサービス URL に「本番環境では Azure Container Apps の内部 DNS で自動解決。以下は Aspire 非使用環境でのフォールバック設定例」と注記追加 |

## 未修正の Medium/Low（残存）

| # | 指摘 ID | 理由 |
|---|--------|------|
| M-6 | レスポンス変換設計 | セキュリティヘッダーのミドルウェア追加で一部対応済み。詳細なレスポンス変換は実装フェーズで検討 |
| M-7 | IP フィルタリング実装レイヤー | §6 セキュリティポリシーで「Azure Front Door / WAF（第一層）+ ASP.NET Core ミドルウェア（第二層）」と方針を追記済み |
| M-8 | リクエストサイズ 5MB の根拠 | §5 に「画像アップロードパスは別途設定」と注記追加済み |
| M-9 | スケーリング設定 | Azure Container Apps 固有の設計は Infrastructure 設計書の範囲 |
| M-11 | C# コード例 | §5, §6, §11, §14 に C# コード例を追加済み（Correlation ID、セキュリティヘッダー、例外ハンドラー、Program.cs） |
| M-12 | カオスエンジニアリングテスト | §16 テスト戦略でサーキットブレーカーテスト方針を記載済み |
| M-13 | 改訂履歴テーブル | 設計書テンプレートの統一的な対応が必要なため保留 |
| M-14 | Redis ハッシュタグ設計 | インフラ設計の詳細として保留 |
| M-15 | クーポンエンドポイント粒度 | `GET /api/coupons`（AllowAnonymous）と `POST /api/coupons/apply`（認証必須）を§4で明記済み |
| M-16 | 技術スタック重複 | セクション再構成は Low 対応の範囲（非破壊的変更優先のため保留） |
| M-17 | TLS 終端配置 | §6 セキュリティポリシーに「Azure Application Gateway で TLS 終端」と追記済み |
| M-18 | エラーメッセージ i18n | §11 に `Accept-Language` ヘッダーに基づくメッセージ切替方針を追記済み |
| L-1 | セクション番号整理 | 新設セクション追加により §13〜§18 に再番号付け |
| L-2 | appsettings.json 設計例 | §13 YARP 設定で appsettings.json 例を追加済み |
| L-3 | .dockerignore | 低優先度として保留 |

## エスカレーション事項

| # | 内容 | 対応状況 |
|---|------|---------|
| E-1 | 認証キャッシュ TTL のトレードオフ | TTL を 5 分に短縮 + Kafka イベント無効化で設計対応済み。最終確認はセキュリティリード + テックリード判断 |
| E-2 | `/api/cart/**` のゲスト購入フロー | エンドポイント単位の認可ポリシー（`POST /api/cart/items` は AllowAnonymous）で設計対応済み。PO 最終確認推奨 |
| E-3 | レート制限値の確定 | spec.md の値（Token Bucket 60/120 req/min、Fixed Window 5/10 req/min）に統一済み |

## 新設セクション一覧

| セクション番号 | タイトル | 対応指摘 |
|------------|-------|---------|
| §13 | YARP リバースプロキシ設定 | H-5 |
| §14 | Program.cs 構成設計 | H-17 |
| §15 | .NET Aspire オーケストレーション統合 | H-8 |
| §16 | テスト戦略 | H-13 |
