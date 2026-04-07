# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/api-gateway-design.md`（API Gateway サービス詳細設計書）
- **判定**: ❌ **Rejected** — Critical 指摘 1 件検出
- **レビュー日時**: 2026-04-03
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

## 🚨 CRITICAL 指摘検出

Critical 指摘が 1 件検出されたため、判定は自動的に **❌ Rejected** となる。Critical 指摘の是正完了後に再レビューを実施すること。

## 技術スタック検証結果

| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| ランタイム | .NET 10 (C# 14) | .NET 10 (C# 14) | ✅ |
| フレームワーク | ASP.NET Core 10 + YARP | ASP.NET Core 10 (Minimal API) | ✅ |
| リバースプロキシ | YARP 2.* | YARP | ✅ |
| 認証 | JwtBearer 10.* | JwtBearer 10.* | ✅ |
| キャッシュ | StackExchange.Redis 2.* | StackExchange.Redis 2.* | ✅ |
| メッセージング | Confluent.Kafka 2.* | Confluent.Kafka 2.* | ✅ |
| ログ | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| Docker | "Latest" ← **違反** | 25.x（latest 禁止） | ❌ |
| 耐障害性（Polly） | **未記載** | Polly 8.* 必須 | ❌ |
| バリデーション（FluentValidation） | **未記載** | FluentValidation 11.* 必須 | ❌ |
| Serilog.Sinks.Console | **未記載** | 6.* 必須 | ❌ |
| OpenTelemetry | 1.* | 1.* | ✅ |
| ヘルスチェック NuGet | **未記載** | AspNetCore.HealthChecks.Redis 9.* 必須 | ❌ |

## 指摘サマリー

| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ⚠️ Conditional | 0 | 1 | 1 | 0 |
| architect | ⚠️ Conditional | 0 | 3 | 3 | 1 |
| tech-lead | ⚠️ Conditional | 0 | 2 | 2 | 0 |
| programing-reviewer | ⚠️ Conditional | 0 | 1 | 2 | 1 |
| security-reviewer | ❌ Rejected | 1 | 3 | 2 | 0 |
| dba-reviewer | ✅ Pass | 0 | 0 | 1 | 0 |
| qa-manager | ⚠️ Conditional | 0 | 1 | 1 | 0 |
| performance-reviewer | ⚠️ Conditional | 0 | 2 | 2 | 0 |
| compliance-reviewer | ⚠️ Conditional | 0 | 1 | 0 | 0 |
| oss-reviewer | ⚠️ Conditional | 0 | 1 | 2 | 0 |
| release-manager | ⚠️ Conditional | 0 | 1 | 1 | 0 |
| infra-ops-reviewer | ⚠️ Conditional | 0 | 1 | 2 | 1 |
| audit-reviewer | ⚠️ Conditional | 0 | 1 | 1 | 0 |
| ux-accessibility-reviewer | ✅ Pass | 0 | 0 | 1 | 0 |
| **合計** | | **1** | **18** | **21** | **3** |

## 判定根拠
- 判定ルール適用結果: Critical 指摘が 1 件以上存在するため **❌ Rejected**（自動判定）
- 最も重大な指摘: エラーレスポンス形式が RFC 9457 Problem Details に非準拠（ADR-0007 違反）

---

## Critical/High 指摘一覧（修正必須）

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|--------------|----------|----------|
| C-1 | **Critical** | security-reviewer, architect, programing-reviewer | エラーレスポンス | §11 エラーハンドリング | エラーレスポンス JSON 形式が RFC 9457 Problem Details に**非準拠**。設計書のグローバルエラーレスポンスは `{"timestamp", "status", "error", "code", "message", "path", "traceId"}` というカスタムエンベロープ形式だが、ADR-0007 で RFC 9457（`type`, `title`, `status`, `detail`, `instance`）への統一が決定済み。**プロジェクト全体のエラーハンドリング標準との重大な矛盾** | RFC 9457 準拠の `TypedResults.Problem()` ベースの形式に全面書き換え。カスタムフィールド（`code`, `traceId`）は RFC 9457 の `extensions` で追加可能 |
| H-1 | **High** | security-reviewer | セキュリティヘッダー | §6 セキュリティ設定 | セキュリティレスポンスヘッダーが抽象的な「セキュリティヘッダー」としか記載されていない。spec.md §セキュリティレスポンスヘッダー設計で **7 つの具体的なヘッダー**（`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Content-Security-Policy: default-src 'self'`, `Strict-Transport-Security: max-age=31536000; includeSubDomains`, `X-XSS-Protection: 0`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy: camera=, microphone=, geolocation=`）の設定が義務付けられているが、設計書に**ヘッダー名・値の具体的な記載がない** | §6 にセキュリティヘッダー一覧テーブルを追加し、spec.md §セキュリティレスポンスヘッダー設計の 7 ヘッダーを全て明記 |
| H-2 | **High** | security-reviewer, architect | CORS | §5, §6 | CORS 設定が「configurable allowed origins and methods」と抽象的。spec.md の Azure Container Apps YAML では具体的な `allowedOrigins: ["https://www.skieshop.com"]`、`allowedHeaders: ["Content-Type", "Authorization", "X-Correlation-Id", "Accept-Language", "X-Request-Id"]`、`maxAge: 3600` が定義されているが、設計書に**ホワイトリスト値の記載がない**。ワイルドカード CORS の禁止が AGENTS.md §5.3 で規定されており、具体値の明記は必須 | CORS ポリシーセクションを追加し、`allowedOrigins`, `allowedMethods`, `allowedHeaders`, `maxAge` の具体値を spec.md と整合させて記載 |
| H-3 | **High** | security-reviewer | Correlation ID | §5 ミドルウェア | Correlation ID ヘッダー名が `X-Request-ID` と記載されているが、spec.md §ミドルウェアパイプライン順序設計および AGENTS.md §11.2 では **`X-Correlation-Id`** を使用。ヘッダー名の不一致はサービス間の分散トレーシング連携に支障をきたす | ヘッダー名を `X-Correlation-Id` に統一。`X-Request-Id` を使用する場合は spec.md 側と合意形成が必要 |
| H-4 | **High** | architect, performance-reviewer | レート制限 | §8 レート制限設定 | レート制限の**値と単位が spec.md と不整合**。spec.md では IP ベース: Token Bucket 60 req/min、ユーザーベース: Token Bucket 120 req/min、`POST /auth/login`: Fixed Window 5 req/min/IP、`POST /checkout/*`: Fixed Window 10 req/min/user。api-gateway-design.md では `/api/auth/**` 20 req/s (= 1,200 req/min)、`/api/products/**` 50 req/s (= 3,000 req/min) と**桁違いの値**。また spec.md は Token Bucket と Fixed Window のアルゴリズム使い分けを明記しているが、設計書ではアルゴリズム種別が未記載 | spec.md のレート制限テーブルと完全に整合させる。アルゴリズム（Token Bucket / Fixed Window）、単位（req/min）、キーリゾルバ（IP / userId）を明記 |
| H-5 | **High** | architect | YARP 設定 | §4, §14 | YARP リバースプロキシの**具体的な appsettings.json 設定（`ReverseProxy` セクション）が未記載**。YARP のルート定義・クラスタ定義は API Gateway の最も重要な設定であるにもかかわらず、実装メモ（§14）で「`appsettings.json` の `ReverseProxy` セクションで管理」と言及するのみで、具体的な JSON スキーマ・ルート定義例がない | Routes / Clusters の YARP JSON 設定例を追加。各バックエンドサービスのクラスタ定義（Destinations, HealthCheck, LoadBalancingPolicy 等）を含める |
| H-6 | **High** | architect | サービスルーティング | §3, §4 | ルートテーブル（§4）に **MailSendService（ポート 5008）が未掲載**。AGENTS.md のマイクロサービス一覧には 11 サービスが定義されているが、ルートテーブルは 8 バックエンドサービスのみ。MailSendService が完全に内部サービス（Kafka イベントのみ）で外部ルーティング不要である場合、その旨を明示的に記載すべき。また、Mermaid 関係図に存在する「監視サービス」は AGENTS.md マイクロサービス一覧に存在しない | MailSendService を「内部専用サービス（API Gateway 経由のルーティングなし）」として明示注記。Mermaid 図の「監視サービス」を削除または整合させる |
| H-7 | **High** | architect, infra-ops-reviewer | ミドルウェア順序 | §5 | ASP.NET Core ミドルウェアパイプラインの**登録順序が明示されていない**。spec.md §ミドルウェアパイプライン順序設計で厳密な 8 段階の登録順序（ExceptionHandler → HSTS/HTTPS → CorrelationId → SerilogRequestLogging → CORS → Authentication/Authorization → RateLimiter → Endpoints）が定義されているが、設計書では順序なしの表形式で列挙されているのみ | ミドルウェアパイプラインを spec.md に準拠した番号付きリストに変更し、順序が動作に直結する旨を注記 |
| H-8 | **High** | architect | .NET Aspire 統合 | §14 | .NET Aspire の `AppHost/Program.cs` での ApiGateway 登録例（`WithReference` によるサービスディスカバリ接続）が未記載。AGENTS.md §10.5 で `WithReference` の使用が明記されているが、`builder.AddProject<Projects.ApiGateway>("api-gateway").WithReference(authService).WithReference(inventoryService)...` 等の具体例がない。サービスディスカバリの実装方法が不明確 | AppHost 登録例を追加し、全バックエンドサービスへの `WithReference` 接続を明示 |
| H-9 | **High** | performance-reviewer | パフォーマンス目標 | §12 | パフォーマンスメトリクスのレイテンシ目標が「< 50ms（平均 30ms）」と記載されているが、**p95/p99 パーセンタイル目標が未定義**。spec.md は API p95 を 300ms 以内と定義。また、spec.md の Saga レイテンシバジェットでは API Gateway 通過を「30ms」（処理 20ms + オーバーヘッド 10ms）と定義しており、50ms 目標との関係が不明確 | p95/p99 レイテンシ目標を追加。spec.md の Saga レイテンシバジェット（API Gateway 通過 30ms）との整合性を明記 |
| H-10 | **High** | performance-reviewer | キャッシュ戦略 | §12 | 「認証検証結果（TTL: 10 分）」のキャッシュ記載があるが、**JWT トークンの無効化（ログアウト、権限変更）時のキャッシュ無効化戦略が未記載**。10 分間のキャッシュ有効期間中にユーザーが権限を失ったケースで、不正アクセスが継続するセキュリティリスクがある | キャッシュ無効化戦略（Kafka イベント `user.permission_changed` での即時無効化、または TTL 短縮）を追加 |
| H-11 | **High** | oss-reviewer | Docker バージョン | §技術スタック | Docker のバージョンが `Latest` と記載。`dockerfile-infra.instructions.md` §2 で **`latest` タグの使用は禁止**と明記。AGENTS.md §12.5 でも同様に禁止 | `Docker 25.x` に修正（AGENTS.md の定義と整合） |
| H-12 | **High** | release-manager | 環境変数の秘密情報 | §10 環境変数 | 環境変数セクションに `AZURE_CLIENT_SECRET=${AZURE_CLIENT_SECRET}` が含まれているが、Azure Managed Identity（`Azure.Identity`）を使用する場合、Client Secret は**不要**（かつセキュリティリスク）。技術スタックに `Azure.Identity 1.*` が含まれており、Managed Identity 前提の設計であるはず | Managed Identity（`DefaultAzureCredential`）前提の設計を明確化し、`AZURE_CLIENT_SECRET` を環境変数から削除。開発環境用の `az login` / `dotnet user-secrets` 方式を明記 |
| H-13 | **High** | qa-manager | テスト戦略 | — (欠落) | テスト戦略セクションが**完全に欠落**。`test-standards.instructions.md` では全パブリックメソッドの単体テスト必須、分岐カバレッジ 80% 以上が規定。API Gateway 固有のテスト（YARP ルーティングテスト、レート制限テスト、サーキットブレーカーテスト、認証フィルタテスト）の記載がない | テスト戦略セクションを追加。Unit Test（NSubstitute）、Integration Test（`WebApplicationFactory`）、Performance Test のカテゴリ別テスト方針を記載 |
| H-14 | **High** | audit-reviewer | Correlation ID | §5, §9 | Correlation ID の**生成・伝搬・ログ出力の詳細設計が不足**。AGENTS.md §11.2 では「全リクエストに相関 ID を付与し、マイクロサービス間で伝搬・ログ出力」と規定。設計書はミドルウェアテーブルに `X-Request-ID` とのみ記載し、(1) 相関 ID の生成ロジック（受信時ヘッダーから取得 or 新規生成）、(2) バックエンドへの転送方法、(3) Serilog の `LogContext.PushProperty` 連携が未記載 | Correlation ID ミドルウェアの設計詳細を追加。AGENTS.md §11.2 のコード例に準拠 |
| H-15 | **High** | compliance-reviewer | PII ログ | §9 ログ設定 | ログ設定でリクエスト/レスポンスログの PII フィルタリング方針が未記載。API Gateway は全リクエストを通過するため、Authorization ヘッダー（JWT トークン）、Cookie、リクエストボディ（ログイン時のパスワード等）がログに出力される可能性がある。AGENTS.md §5.7 で PII ログ禁止が規定 | ログのセンシティブデータマスキング設計を追加。Serilog の Destructuring Policy または `ILogEventEnricher` で Authorization ヘッダー/Cookie のマスキングを明記 |
| H-16 | **High** | tech-lead | フォールバック戦略 | §7 サーキットブレーカー | サーキットブレーカー Open 時の**フォールバック戦略が未定義**。AGENTS.md §耐障害性で「フォールバック: 外部サービス障害時のフォールバック戦略を各サービスで定義（キャッシュ応答、デフォルト値等）」と規定。設計書にはサーキットブレーカーのパラメータのみで、Open 時にクライアントに返すレスポンス・代替データの設計がない | サービス別フォールバック戦略テーブルを追加。例: 商品一覧 → キャッシュ応答、認証 → 503 即時返却、AI レコメンド → 人気商品の静的リスト返却 |
| H-17 | **High** | tech-lead | Program.cs 設計 | — (欠落) | `Program.cs` の**DI 登録・ミドルウェア構成の設計詳細が欠落**。AGENTS.md §4.3 で DI 規約（Scoped 登録基本）、§4.7 で例外ハンドラー、§11.3 でミドルウェア順序が規定されているが、API Gateway の `Program.cs` がどのようにこれらを実装するかの設計指針がない | `Program.cs` の構成概要（DI 登録セクション、ミドルウェアパイプラインセクション、エンドポイントマッピングセクション）を追加 |
| H-18 | **High** | business-analyst | カート未ログイン | §4 ルートテーブル | `/api/cart/**` が「認証必須: はい」と設定されているが、AGENTS.md §10.2 および spec.md のゲスト購入フローでは**未ログイン状態でのカート操作**（Cookie ベースの CartId）が許可されている。認証必須設定ではゲスト購入フローが実現不可能 | `/api/cart/**` の認証設定を見直し、`POST /api/cart/items`（カート追加）は `AllowAnonymous`、`POST /api/cart/checkout` は認証必須のように、エンドポイント単位で認可ポリシーを定義 |

---

## Medium/Low 指摘一覧

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|--------------|----------|----------|
| M-1 | Medium | infra-ops-reviewer | Dockerfile | §10 | Dockerfile の `HEALTHCHECK --timeout=3s` が `dockerfile-infra.instructions.md` §4 の推奨値 `--timeout=10s` と不一致。Readiness チェックで Redis 疎通確認含む場合、3s では不十分な可能性 | `--timeout=10s` に変更 |
| M-2 | Medium | infra-ops-reviewer | Dockerfile | §10 | Dockerfile に `--start-period=60s` が設定されているが、`dockerfile-infra.instructions.md` §4 の推奨値は `--start-period=30s`（`.NET の起動は高速` とのコメント付き）。60s の根拠が不明 | 30s に短縮、または 60s の根拠を明記 |
| M-3 | Medium | oss-reviewer | 技術スタック | §2 | 必須 NuGet パッケージの記載に不足あり。`nuget-dependency.instructions.md` §3 で必須とされる `Polly 8.*`, `FluentValidation 11.*`, `FluentValidation.DependencyInjectionExtensions 11.*`, `Serilog.Sinks.Console 6.*`, `AspNetCore.HealthChecks.Redis 9.*` が技術スタックテーブルに含まれていない | 必須パッケージを全て技術スタックテーブルに追加 |
| M-4 | Medium | oss-reviewer | 技術スタック | §技術スタック（2つ目のテーブル） | Kafka バージョンが `7.4.0` と記載。Apache Kafka のバージョンは 3.x 系であり、7.4.0 は Confluent Platform のバージョニング。混同が発生するため明確化が必要 | `Apache Kafka 3.x (Confluent Platform 7.4.0)` のように明記 |
| M-5 | Medium | architect | サービス関係図 | §3 | Mermaid マイクロサービス関係図に「監視サービス（MONITOR）」が含まれているが、AGENTS.md のマイクロサービス一覧に「監視サービス」は存在しない。不整合 | 「監視サービス」を削除するか、Azure Monitor / Application Insights への外部連携として正確に表現 |
| M-6 | Medium | architect | リクエスト変換 | §5 | YARP パス変換ルール `/api/{segment}/**` → `/{segment}/**` の記載はあるが、**レスポンス変換**（レスポンスヘッダーの追加・削除、レスポンスボディの変換）の設計が不足 | レスポンス変換の必要性の有無を明記。セキュリティヘッダーの追加はミドルウェアで実施する旨を記載 |
| M-7 | Medium | architect | IP フィルタリング | §6 | 「IP フィルタリング: 許可リスト/ブロックリスト」の記載あるが、具体的な実装方針（ASP.NET Core ミドルウェア、YARP ポリシー、Azure WAF のいずれで実装するか）が未定義 | 実装レイヤーを明確化。Azure Front Door / Application Gateway の WAF で第一層防御、ASP.NET Core ミドルウェアで第二層防御の多層設計を推奨 |
| M-8 | Medium | performance-reviewer | リクエストサイズ | §5 | `RequestSizeLimit: maxSize=5MB` と記載されているが、`dotnet-config.instructions.md` §5 のデフォルトが 10MB。API Gateway は画像アップロードを中継する可能性があり、5MB の根拠が不明 | 5MB の採用根拠を明記。画像アップロードパスは別途サイズ制限を設定するか、multipart/form-data は対象外とする旨を記載 |
| M-9 | Medium | performance-reviewer | スケーリング | — (欠落) | API Gateway 固有のスケーリング設定（`minReplicas`, `maxReplicas`, スケーリングトリガー）が未記載。spec.md §スケーラビリティ要件で高負荷サービスは `maxReplicas: 10` 以上と規定 | Azure Container Apps のスケーリング設定例を追加 |
| M-10 | Medium | programing-reviewer | コード例 | §10 | 環境変数セクションで `Services__AuthService=http://auth-service:5001` のようにハードコード URL が記載されているが、AGENTS.md §10.5 で「ハードコード URL は禁止」と規定。.NET Aspire の `WithReference` で自動解決される旨と矛盾 | 環境変数セクションの注記として「本番環境では Azure Container Apps の内部 DNS で自動解決。以下は Aspire 非使用環境でのフォールバック設定例」と明記 |
| M-11 | Medium | programing-reviewer | コード例 | §全体 | 設計書全体を通じて **C# コード例が皆無**。AGENTS.md §4 で推奨される primary constructor、record 型、CancellationToken パターンの適用例がなく、実装者への設計意図の伝達が不十分 | ミドルウェア登録、YARP 設定読み込み、Correlation ID ミドルウェア実装等の C# コード例を追加 |
| M-12 | Medium | qa-manager | サーキットブレーカーテスト | §7 | サーキットブレーカーのパラメータ（失敗率しきい値、タイムアウト）が定義されているが、これらの値が適切であることを検証するテスト方針が未記載 | カオスエンジニアリング / 障害注入テスト（バックエンド遅延シミュレーション、障害率シミュレーション）の方針を記載 |
| M-13 | Medium | audit-reviewer | 改訂履歴 | — (欠落) | spec.md に存在する改訂履歴テーブル・承認プロセスが設計書に欠落。設計変更のトレーサビリティが確保できない | 改訂履歴テーブル（版数、改訂日、改訂者、承認者、改訂内容）を冒頭に追加 |
| M-14 | Medium | dba-reviewer | Redis 設計 | §12 | Redis キャッシュキー設計で `rate:{userId/ip}:{endpoint}` と記載されているが、spec.md §Redis Cluster 固有の制約対応で言及されている**ハッシュタグ（`{user:123}:*`）の使用方針**が未記載。本番 Redis Cluster 環境での CROSSSLOT エラーリスクがある | Redis Cluster 環境でのハッシュタグ設計を追加 |
| M-15 | Medium | business-analyst | エンドポイント粒度 | §4 | ルートテーブルのエンドポイントパターンがワイルドカード（`/api/auth/**`）のみで、各バックエンドサービスの具体的な API エンドポイントとの対応関係が不明。「一部エンドポイントで認証必須」（クーポン）の「一部」が不明確 | クーポンサービスの認証必須/不要エンドポイントを具体的に列挙 |
| M-16 | Medium | tech-lead | 技術スタック重複 | §2, §サービス情報, §技術スタック | 技術スタック情報が 3 つのテーブル（§2 主要ライブラリ、§サービス情報、§技術スタック）に分散しており、一部のバージョン情報に差異がある。情報の正規化が必要 | 技術スタック情報を 1 つのテーブルに統合。重複セクションを削除 |
| M-17 | Medium | tech-lead | TLS 設定 | §6 | 「TLS 設定: TLS 1.3、強力な暗号スイート」との記載はあるが、Kestrel での TLS 設定（`ListenOptions.UseHttps`）、Azure Application Gateway での TLS 終端、暗号スイートのホワイトリスト等の**具体的な実装方針がない** | TLS 終端の配置（Azure Application Gateway / Kestrel 直接）を明記 |
| M-18 | Medium | ux-accessibility-reviewer | エラーメッセージ | §11 | エラーメッセージが日本語と英語で混在（`"message": "無効な認証トークンです"`）。spec.md のビジネス要件に多言語対応（日本語・英語）が含まれるが、ゲートウェイレベルのエラーメッセージの i18n 方針が未定義 | エラーメッセージの i18n 方針を追加（`Accept-Language` ヘッダーに基づくメッセージ切替、または英語統一 + フロントエンドでの翻訳） |
| L-1 | Low | architect | 設計書構成 | §全体 | セクション番号に欠番あり（§1〜§14 のうち整理されていないセクションがある）。一部の情報がセクション外に記載されている（サービス情報テーブル、技術スタックテーブルがセクション番号なし） | セクション番号を振り直し、ドキュメント構造を整理 |
| L-2 | Low | programing-reviewer | appsettings.json | — (欠落) | `appsettings.json` / `appsettings.Development.json` / `appsettings.Production.json` の設計例が未記載。`dotnet-config.instructions.md` §2 でプロファイル別ファイル構成が義務付けられている | appsettings の構成例を追加（共通設定、開発向け緩和設定、本番向け最小ログ設定） |
| L-3 | Low | infra-ops-reviewer | .dockerignore | §10 | `.dockerignore` の内容が未記載。`dockerfile-infra.instructions.md` §1 で `bin/`, `obj/`, `.git/`, `*.md` の除外が推奨 | `.dockerignore` 設定例を追加 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-1 | 最優先 | security-reviewer, performance-reviewer | 認証検証結果の Redis キャッシュ（TTL: 10 分）は、パフォーマンスとセキュリティのトレードオフ。TTL を短縮するとレイテンシ増加、長期化すると権限変更の反映遅延。Kafka イベントによるキャッシュ無効化を導入するかの判断が必要 | セキュリティリード + テックリード |
| E-2 | 高優先 | architect, business-analyst | `/api/cart/**` の認証ポリシーがゲスト購入フローと矛盾。API Gateway レベルで AllowAnonymous にすると、フロントエンド実装に影響。ゲスト購入 MVP スコープと合わせて判断が必要 | プロダクトオーナー + テックリード |
| E-3 | 通常 | performance-reviewer | レート制限値が spec.md と桁違いに乖離。設計書の値（20-50 req/s）は spec.md（1-5 req/min）より遥かに大きい。どちらが正しいかビジネス要件（想定トラフィック量）に基づいて確定が必要 | テックリード + インフラチーム |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| 1 | performance-reviewer（認証キャッシュ TTL 10 分で高速化） | security-reviewer（キャッシュにより権限変更の反映が遅延するリスク） | 認証キャッシュの TTL 設定 | **security-reviewer を優先**。TTL を 5 分に短縮し、`user.permission_changed` Kafka イベントによるキャッシュ即時無効化を併用する設計を推奨。エスカレーション E-1 で最終確認 | セキュリティリスク（不正アクセスの継続）はパフォーマンス劣化より影響が大きい。Kafka イベントでのキャッシュ無効化により両立可能 |

---

## ドキュメント横断分析

### マイクロサービス設計書カバレッジ（API Gateway 視点）

| サービス | ルート定義 | サーキットブレーカー | レート制限 | 認証設定 | 整合性 |
|---------|----------|-------------------|----------|---------|--------|
| AuthService | ✅ | ✅ | ✅ | ✅ AllowAnonymous | ✅ |
| UserManagementService | ✅ | ✅ | ✅ | ✅ 認証必須 | ✅ |
| InventoryManagementService | ✅ | ✅ | ✅ | ✅ 混在 | ✅ |
| SalesManagementService | ✅ | ✅ | ✅ | ✅ 認証必須 | ✅ |
| PaymentCartService | ✅ | ✅ | ✅ | ⚠️ ゲスト購入との矛盾 | ❌ |
| CouponService | ✅ | ✅ | ❌ 未定義 | ⚠️ 「一部」不明確 | ❌ |
| PointService | ✅ | ✅ | ❌ 未定義 | ✅ 認証必須 | ⚠️ |
| AiSupportService | ✅ | ✅ | ✅ | ✅ 混在 | ✅ |
| MailSendService | ❌ **欠落** | ❌ | ❌ | ❌ | ❌ |

### サービス間整合性

- **API 契約**: ルートテーブルのエンドポイントパターンがワイルドカードのみのため、各バックエンドサービスの具体的 API エンドポイントとの整合性検証が困難
- **レート制限**: spec.md との値・単位の不整合あり（前述 H-4）
- **Correlation ID**: ヘッダー名の不一致（前述 H-3）
- **Kafka イベント**: API Gateway が Kafka に直接接続する用途が不明確。技術スタックに Confluent.Kafka が含まれているが、ゲートウェイがイベントを発行/消費するシナリオの記載なし

### 未定義・曖昧な領域

1. **WebSocket / SignalR 対応**: リアルタイム在庫通知、チャット機能で WebSocket が必要だが、YARP での WebSocket プロキシ設定が未定義
2. **API バージョニング**: `/api/v1/` 等のバージョニングプレフィックスの設計方針が未定義。spec.md の gRPC バージョニング戦略（`v1`/`v2`）と REST URI バージョニングの整合性
3. **OpenAPI / Swagger 集約**: API ドキュメント集約（§1 で言及）の具体的な実装方針が未定義
4. **グレースフルシャットダウン**: Kubernetes / Azure Container Apps での Pod 停止時のインフライトリクエスト処理方針が未定義
5. **Blue/Green・カナリアデプロイ**: API Gateway のデプロイ戦略（全リクエストを通過するため、ゲートウェイ障害の影響範囲が最大）

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### business-analyst レビュー結果

**判定**: ⚠️ Conditional Approval

#### 指摘一覧

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| BA-H-1 | High | `/api/cart/**` の認証必須設定がゲスト購入フロー（spec.md で定義済み）と矛盾。ゲストユーザーはカートに商品を追加できない設計になっている。ビジネス要件（CVR 向上、カート離脱率 30% 以下の KPI）に直結する問題 |
| BA-M-1 | Medium | CouponService の「一部エンドポイントで認証必須」が不明確。公開クーポン検索（認証不要）と個人クーポン適用（認証必須）の区分が設計書から読み取れない。ビジネスロジックの曖昧さ |

</details>

<details>
<summary>architect レビューレポート</summary>

### architect レビュー結果

**判定**: ⚠️ Conditional Approval

#### 指摘一覧

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| AR-H-1 | High | YARP `ReverseProxy` appsettings.json 設定の具体例が欠落。Routes / Clusters / Transforms の定義がない |
| AR-H-2 | High | MailSendService のルーティング方針が不明。11 サービス中 1 サービスの設計が欠落 |
| AR-H-3 | High | .NET Aspire AppHost 統合の設計詳細がない。`WithReference` 接続の具体例がない |
| AR-M-1 | Medium | Mermaid 図の「監視サービス」が AGENTS.md サービス一覧に存在しない |
| AR-M-2 | Medium | リクエスト/レスポンス変換の詳細設計が不足。YARP Transforms の設計がパス変換のみ |
| AR-M-3 | Medium | IP フィルタリングの実装レイヤー（WAF / ミドルウェア / YARP ポリシー）が未決定 |
| AR-L-1 | Low | セクション構成の整理が必要。技術スタック情報が 3 箇所に分散 |

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### tech-lead レビュー結果

**判定**: ⚠️ Conditional Approval

#### 指摘一覧

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| TL-H-1 | High | サーキットブレーカー Open 時のフォールバック戦略が未定義。AGENTS.md §耐障害性の必須要件 |
| TL-H-2 | High | `Program.cs` の DI 登録・ミドルウェア構成の設計が欠落。API Gateway の中核設計が不在 |
| TL-M-1 | Medium | 技術スタックテーブルの重複（3 箇所）。情報の正規化が必要 |
| TL-M-2 | Medium | TLS 設定の実装方針（Kestrel 直接 vs Application Gateway TLS 終端）が未決定 |

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### programing-reviewer レビュー結果

**判定**: ⚠️ Conditional Approval

#### 指摘一覧

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| PR-H-1 | High | エラーレスポンス形式が RFC 9457 非準拠（C-1 と同一。`dotnet-coding-standards.instructions.md` §例外処理規約にも `TypedResults.Problem()` の使用が規定） |
| PR-M-1 | Medium | 環境変数にハードコード URL（`http://auth-service:5001`）が含まれ、AGENTS.md §10.5 のハードコード URL 禁止に抵触 |
| PR-M-2 | Medium | C# コード例が皆無。primary constructor、CancellationToken、record 型の適用例がなく実装指針が不明確 |
| PR-L-1 | Low | `appsettings.json` の設計例がない |

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### security-reviewer レビュー結果

**判定**: ❌ Rejected

#### 指摘一覧

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| SR-C-1 | **Critical** | エラーレスポンス形式が RFC 9457 非準拠。カスタム形式では `type` フィールド（エラータイプの URI）が欠落し、クライアントのプログラマティカルなエラー処理が不可能。ADR-0007 で全サービス統一が決定済み |
| SR-H-1 | High | セキュリティヘッダー 7 種の具体値が未記載 |
| SR-H-2 | High | CORS ホワイトリストの具体値が未記載 |
| SR-H-3 | High | Correlation ID ヘッダー名の不一致 |
| SR-M-1 | Medium | JWT トークン検証の詳細設計不足。`TokenValidationParameters`（ValidateIssuer, ValidateAudience, ValidateLifetime, ClockSkew）の具体値がない |
| SR-M-2 | Medium | レート制限超過時の `Retry-After` ヘッダー付与が spec.md で規定されているが、設計書に記載なし |

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### dba-reviewer レビュー結果

**判定**: ✅ Pass

#### 指摘一覧

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| DB-M-1 | Medium | Redis キャッシュキー設計で Redis Cluster 環境のハッシュタグ方針が未記載 |

#### 備考
API Gateway は独自の PostgreSQL データベースを持たないため、DBA 観点の指摘は限定的。Redis のキャッシュ設計のみが対象。

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### qa-manager レビュー結果

**判定**: ⚠️ Conditional Approval

#### 指摘一覧

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| QA-H-1 | High | テスト戦略セクションが完全に欠落。YARP ルーティングテスト、レート制限テスト、サーキットブレーカーテスト、JWT 認証フィルタテストの方針が必要 |
| QA-M-1 | Medium | サーキットブレーカーパラメータの検証方針（カオスエンジニアリング / 障害注入テスト）が未記載 |

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### performance-reviewer レビュー結果

**判定**: ⚠️ Conditional Approval

#### 指摘一覧

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| PF-H-1 | High | p95/p99 パーセンタイルレイテンシ目標が未定義。spec.md の p95 300ms、Saga バジェット 30ms との整合性不明 |
| PF-H-2 | High | 認証キャッシュ無効化戦略の欠如（セキュリティリスク + パフォーマンストレードオフ） |
| PF-M-1 | Medium | リクエストサイズ 5MB の根拠が不明 |
| PF-M-2 | Medium | スケーリング設定（minReplicas, maxReplicas）が未記載 |

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### compliance-reviewer レビュー結果

**判定**: ⚠️ Conditional Approval

#### 指摘一覧

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| CP-H-1 | High | ログの PII フィルタリング方針が未記載。API Gateway は全リクエストのハブであり、Authorization ヘッダー（JWT トークン）、Cookie がログに出力される可能性。GDPR / 個人情報保護法への抵触リスク |

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### oss-reviewer レビュー結果

**判定**: ⚠️ Conditional Approval

#### 指摘一覧

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| OS-H-1 | High | Docker バージョン `Latest` 記載が `dockerfile-infra.instructions.md` 違反 |
| OS-M-1 | Medium | 必須 NuGet パッケージ 5 種が技術スタックテーブルに欠落 |
| OS-M-2 | Medium | Kafka バージョン表記が Apache Kafka / Confluent Platform で混同 |

</details>

<details>
<summary>release-manager レビューレポート</summary>

### release-manager レビュー結果

**判定**: ⚠️ Conditional Approval

#### 指摘一覧

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| RM-H-1 | High | 環境変数に `AZURE_CLIENT_SECRET` が含まれ、Managed Identity 前提の設計と矛盾。秘密情報の扱い方針が不明確 |
| RM-M-1 | Medium | API Gateway のデプロイ戦略（Blue/Green, カナリア）が未記載。全リクエストを通過するため、デプロイ障害の影響範囲が最大 |

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### infra-ops-reviewer レビュー結果

**判定**: ⚠️ Conditional Approval

#### 指摘一覧

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| IO-H-1 | High | ミドルウェアパイプラインの登録順序が明示されていない。spec.md の 8 段階順序と整合していない |
| IO-M-1 | Medium | Dockerfile HEALTHCHECK の timeout/start-period が推奨値と不一致 |
| IO-M-2 | Medium | スケーリング設定（Azure Container Apps）が未記載 |
| IO-L-1 | Low | `.dockerignore` 設定が未記載 |

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### audit-reviewer レビュー結果

**判定**: ⚠️ Conditional Approval

#### 指摘一覧

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| AU-H-1 | High | Correlation ID の生成・伝搬・ログ出力の詳細設計が不足。分散トレーシングの監査証跡が不完全 |
| AU-M-1 | Medium | 改訂履歴テーブル・承認プロセスが欠落。設計変更のトレーサビリティが不在 |

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### ux-accessibility-reviewer レビュー結果

**判定**: ✅ Pass

#### 指摘一覧

| # | 重要度 | 指摘内容 |
|---|--------|----------|
| UX-M-1 | Medium | エラーメッセージの i18n 方針が未定義。日英混在のエラーメッセージはユーザー体験に影響 |

#### 備考
API Gateway は主にバックエンド基盤であり、UX/アクセシビリティの直接的な影響は限定的。エラーメッセージの多言語対応のみが対象。

</details>

---

## 是正アクション優先順位

| 優先度 | アクション | 対応指摘 |
|--------|----------|---------|
| 1（最優先） | エラーレスポンスを RFC 9457 Problem Details に全面書き換え | C-1 |
| 2 | セキュリティヘッダー 7 種の具体値を明記 | H-1 |
| 3 | CORS ホワイトリスト値を spec.md と整合 | H-2 |
| 4 | Correlation ID ヘッダー名を `X-Correlation-Id` に統一 | H-3, H-14 |
| 5 | レート制限の値・単位・アルゴリズムを spec.md と整合 | H-4 |
| 6 | YARP `ReverseProxy` JSON 設定例を追加 | H-5 |
| 7 | MailSendService の取扱方針を明記 | H-6 |
| 8 | ミドルウェアパイプライン順序を spec.md 準拠に変更 | H-7 |
| 9 | .NET Aspire AppHost 設定例を追加 | H-8 |
| 10 | テスト戦略セクションを追加 | H-13 |
