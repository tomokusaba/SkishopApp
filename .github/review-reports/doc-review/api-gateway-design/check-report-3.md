# ドキュメントレビュー統合レポート — api-gateway-design.md

## 判定結果
- **対象**: `design-docs/api-gateway-design.md`
- **判定**: ✅ **Approved with Notes** — 推奨改善事項あり
- **レビュー日時**: 2026-04-03
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

## 技術スタック検証結果
| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| ランタイム | .NET 10 (C# 14) | .NET 10 (C# 14) | ✅ |
| フレームワーク | ASP.NET Core 10 + YARP | ASP.NET Core 10 (Minimal API) | ✅ |
| オーケストレーション | .NET Aspire (§15) | .NET Aspire 13.1 | ✅ |
| キャッシュ | Redis (StackExchange.Redis 2.*) | Redis (StackExchange.Redis 2.*) | ✅ |
| 認証 | JWT Bearer 10.* | JWT Bearer 10.* | ✅ |
| 耐障害性 | Polly 8.* / Http.Resilience 9.* | Polly 8.* / Http.Resilience 9.* | ✅ |
| ログ | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| 可観測性 | OpenTelemetry 1.* | OpenTelemetry 1.* | ✅ |
| コンテナ | Docker 25.x (非root, マルチステージ) | Docker 25.x | ✅ |
| メッセージング | Confluent.Kafka 2.* | Confluent.Kafka 2.* | ✅ |

## 指摘サマリー
| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ✅ Pass | 0 | 0 | 1 | 1 |
| architect | ✅ Pass | 0 | 0 | 2 | 1 |
| tech-lead | ✅ Pass | 0 | 0 | 1 | 0 |
| programing-reviewer | ✅ Pass | 0 | 0 | 2 | 1 |
| security-reviewer | ✅ Pass | 0 | 0 | 2 | 1 |
| dba-reviewer | ✅ Pass | 0 | 0 | 0 | 1 |
| qa-manager | ✅ Pass | 0 | 0 | 1 | 1 |
| performance-reviewer | ✅ Pass | 0 | 0 | 1 | 1 |
| compliance-reviewer | ✅ Pass | 0 | 0 | 1 | 0 |
| oss-reviewer | ✅ Pass | 0 | 0 | 1 | 0 |
| release-manager | ✅ Pass | 0 | 0 | 0 | 1 |
| infra-ops-reviewer | ✅ Pass | 0 | 0 | 1 | 1 |
| audit-reviewer | ✅ Pass | 0 | 0 | 0 | 1 |
| ux-accessibility-reviewer | ✅ Pass | 0 | 0 | 0 | 1 |
| **合計** | | **0** | **0** | **13** | **11** |

## 判定根拠
- **判定ルール適用結果**: Critical 0 件 / High 0 件 → 全て Medium 以下であるため **Approved with Notes** を適用
- **最も重大な指摘**: Medium レベルの複数指摘。いずれも実装フェーズで対応可能な改善事項であり、実装をブロックするものではない
- **前回レビュー（check-report-1, 2）からの改善**: 過去レビューで指摘された主要課題（Correlation ID の実装詳細不足、セキュリティヘッダーの不足、テストコードの欠如等）が §19〜§24 の追記セクションで大幅に補完されている

## Medium 指摘一覧（推奨改善事項）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| M-01 | Medium | architect | ルーティング | §4 ルートテーブルに `admin/products`, `admin/orders`, `admin/reviews`, `admin/mail/templates` 等の管理者エンドポイントルートが未定義。spec.md では `AdminOnly` 認可の管理者 API が多数定義されているが、YARP ルート設定（§13）にも対応する admin ルートが欠落 | `admin-route` を追加し、各サービスの `/admin/**` パスを `AdminOnly` ポリシーで YARP ルート定義に含める。InventoryManagementService (`/api/admin/products`, `/api/admin/reviews`)、SalesManagementService (`/api/admin/orders`)、MailSendService (`/api/admin/mail/**`)、CouponService (`/api/admin/coupons`) 等の admin パスをカバーする |
| M-02 | Medium | architect | Aspire 統合 | §15 の AppHost/Program.cs で MailSendService の `AddProject` 登録が含まれていない。AGENTS.md ではサービス一覧に MailSendService が含まれており、Kafka 経由で他サービスからイベントを受信するため Aspire オーケストレーションへの登録が必要 | `builder.AddProject<Projects.MailSendService>("mailsend-service").WithReference(postgres).WithReference(kafka);` を追加。API Gateway からの `WithReference` は不要だが、AppHost 全体への登録は必要 |
| M-03 | Medium | programing-reviewer | コード例 | §11 のグローバル例外ハンドラーで `UnauthorizedException`, `ForbiddenException`, `ConcurrencyException` の分岐が欠落。AGENTS.md §4.7 では 5 つの例外クラス（NotFoundException, BusinessException, UnauthorizedException, ForbiddenException, ConcurrencyException）の完全な switch 式が規定されている | §11 の例外ハンドラーコード例を AGENTS.md §4.7 に完全準拠するよう拡張（401/403/409 分岐の追加） |
| M-04 | Medium | programing-reviewer | コード例 | §24 最終 Program.cs の例外ハンドラーでも同様に分岐が不完全。`error is not (NotFoundException or BusinessException)` のみで分岐しており、Handled 例外の Warning ログ出力パターンが省略されている | AGENTS.md §4.7 のパターンに合わせ、Handled/Unhandled の 2 段階ログ出力と 5 例外分岐を反映 |
| M-05 | Medium | security-reviewer | 認証 | §6 JWT 認証設定で `ValidIssuer`, `ValidAudience`, `IssuerSigningKey` の具体的な設定ソース（`IConfiguration` からの読取り方法）が未記載。`TokenValidationParameters` の各プロパティを `true` にしているが、検証対象値（Issuer URL, Audience 値, 署名鍵の取得方法）が不明 | `ValidIssuer = builder.Configuration["Jwt:Issuer"]`, `ValidAudience = builder.Configuration["Jwt:Audience"]` 等の設定バインディングコード例を追記。署名鍵については JWKS エンドポイント自動取得（`MetadataAddress` 設定）か Azure Key Vault からの読取りかを明示する |
| M-06 | Medium | security-reviewer | セキュリティ | §6 CORS ポリシーで `AllowCredentials()` の有無が未記載。JWT Bearer トークンは `Authorization` ヘッダーで送信されるがCookie ベースの CartId（ゲスト購入対応）も使用するため、`AllowCredentials()` の要否と `SameSite` 設定との関係を明確にすべき | ゲスト購入時の Cookie 送信を考慮し、`AllowCredentials()` の適用可否と `SameSite=Strict` との組み合わせについて設計判断を追記 |
| M-07 | Medium | qa-manager | テスト | §16/§23 のテスト戦略で E2E テスト（フロントエンド → API Gateway → バックエンド）の記載が欠落。spec.md ではフロントエンドから API Gateway 経由の統合フローが多数記述されており、Gateway 固有のパス変換（PathRemovePrefix）や認証フローの E2E 検証が必要 | E2E テストの対象シナリオ（ログイン → 商品閲覧 → カート追加 → チェックアウト）と使用ツール（Playwright 等）の記載を追加 |
| M-08 | Medium | performance-reviewer | パフォーマンス | §12 パフォーマンスメトリクスの「現状」列に具体的な数値が記載されているが（30ms, 1200 req/s, 0.05%, 0.5%, 0.2%）、これが負荷テスト結果なのか推定値なのか明記されていない。設計段階で「現状」と称する数値の根拠が不明 | 「現状」列を「推定値」または「目標値」に名称変更するか、負荷テスト実施予定時期を追記 |
| M-09 | Medium | compliance-reviewer | PII | §9 のセンシティブデータマスキングで「IP アドレス」をレート制限ログにのみ記録としているが、レート制限の Redis キー（`rate:{ip}:{endpoint}`）にも IP アドレスが含まれる。Redis キー内の IP アドレスの保持期間と GDPR 上の扱い（個人データに該当する可能性）が未記載 | Redis TTL（1 分）と IP アドレスの法的位置付け（GDPR 下では個人データに該当し得る）について注記を追加。保持期間が短い（1 分 TTL で自動削除）ことの正当性を記載 |
| M-10 | Medium | oss-reviewer | 依存関係 | §2 主要ライブラリに `Azure.Identity 1.*` と `Azure.Security.KeyVault.Secrets 4.*` が記載されているが、AGENTS.md §8 の必須パッケージ一覧にこれらが含まれていない。設計書独自の追加パッケージとして問題はないが、AGENTS.md との差分を明示すべき | Azure 関連パッケージが AGENTS.md の基本パッケージに加えて API Gateway 固有の追加パッケージであることを注釈する |
| M-11 | Medium | infra-ops-reviewer | Dockerfile | §10 Dockerfile で `HEALTHCHECK` が `curl` を使用しているが、spec.md §Dockerfile 設計ルールでは `aspnet` ランタイムイメージに `curl` が含まれないため `dotnet HealthCheck.dll` の使用が規定されている。設計書内で矛盾がある | Dockerfile の `HEALTHCHECK` 行を `CMD ["dotnet", "HealthCheck.dll"]` に変更するか、Azure Container Apps の `httpGet` プローブへの委任を明記し、`curl` ベースのヘルスチェックを削除 |
| M-12 | Medium | business-analyst | 機能カバレッジ | §3 マイクロサービス関係図のリンクパターン（MailSendService への破線「内部専用」）が表現されているが、spec.md に記載のある管理者向けメールテンプレート管理 API（`/admin/mail/templates`）へのルーティングが含まれていない。MailSendService は完全な内部サービスではなく、管理者 API を外部公開する必要がある | §3 の注記「MailSendService は内部専用（Kafka イベント駆動のみ）」を修正し、管理者 API ルートのみ API Gateway 経由で公開する設計に変更。§4 ルートテーブルと §13 YARP 設定に `admin/mail` ルートを追加 |
| M-13 | Medium | tech-lead | 規約整合性 | §5 ミドルウェアパイプライン順序でセキュリティヘッダーミドルウェア（§20 `UseSecurityHeaders`）の位置が §14 と §24 で微妙に異なる。§14 では記載なし、§24 では順序 2.5 として挿入。ミドルウェア順序は動作に直結するため、全セクションで統一する必要がある | §5 のミドルウェアパイプライン順序テーブルに `UseSecurityHeaders` を順序 2.5 として正式に追加し、§14 と §24 の記載を統一 |

## Low 指摘一覧（改善提案）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| L-01 | Low | business-analyst | ドキュメント | §1 概要と §17 まとめの内容が大部分重複。設計書の冗長性を低減すべき | §17 はまとめとして機能しているが、§1 との差分が不明確。片方を参照にするか、§17 を箇条書きのみに簡素化 |
| L-02 | Low | architect | 図表 | §3 コンポーネントアーキテクチャ図に FluentValidation コンポーネントが含まれているが、§22 で明確に「FluentValidation は不要」と記載されている。図の整合性が取れていない | §3 のコンポーネント図を更新し、FluentValidation の参照を削除。代わりに「外形的バリデーション（サイズ制限・Content-Type チェック）」を含める |
| L-03 | Low | programing-reviewer | コード例 | §19 CorrelationIdMiddleware の `InvokeAsync` で `context.Response.OnStarting()` を使用してレスポンスヘッダーに付与しているが、§5 のインラインミドルウェア例では `context.Response.Headers.Append()` を使用。同一機能に 2 つの異なる実装パターンが混在 | §5 のインラインミドルウェア例に注記「§19 の専用クラス実装が正式版」を追記し、コード重複の混乱を防止 |
| L-04 | Low | security-reviewer | セキュリティ | §20 SecurityHeadersMiddleware で `Cache-Control: no-store` と `Pragma: no-cache` を全レスポンスに付与しているが、商品画像やカテゴリ一覧など静的コンテンツのキャッシュが無効化される。パフォーマンスへの影響を考慮し、エンドポイント別のキャッシュ戦略が望ましい | 認証関連エンドポイントには `no-store`、公開商品一覧には `max-age=300` 等、パス別のキャッシュヘッダー戦略を検討する旨の注記を追加 |
| L-05 | Low | dba-reviewer | データ | API Gateway は DB を持たないため DBA 観点の指摘は最小。§12 の認証キャッシュに TTL 5 分を設定しているが、Redis の eviction policy（`allkeys-lru` 等）の明示がない | Redis の eviction policy を `appsettings.json` の設定例に含めるか、インフラ設計書（spec.md）への参照を追加 |
| L-06 | Low | qa-manager | テスト | §23 テストコードで `IClassFixture<GatewayWebApplicationFactory>` を primary constructor 形式で使用しているが、xUnit の `IClassFixture` は primary constructor と組み合わせた場合のライフサイクル管理に注意が必要。xUnit 3.x 以降の互換性を確認すべき | テストフィクスチャの DI パターンがプロジェクトの xUnit バージョンと互換であることを確認する注記を追加 |
| L-07 | Low | performance-reviewer | パフォーマンス | §7 サーキットブレーカー設定の AiSupportService で失敗率しきい値 70% が他サービス（30-60%）に比べて高い。AI サービスの外部 API 依存（LLM 呼出し）を考慮すると妥当だが、その設計判断理由が記載されていない | 各サービスのしきい値設定理由（ビジネスクリティカリティ、外部 API 依存度等）を簡潔に注記 |
| L-08 | Low | release-manager | 運用 | §10 デプロイ設定に Blue/Green デプロイやカナリアリリースの設計が欠落。spec.md では Azure Container Apps の `activeRevisionsMode: Multiple`（カナリアリリース対応）が設定されているが、API Gateway での活用方法が未記載 | API Gateway の Blue/Green デプロイ戦略について、Azure Container Apps のトラフィック分割（`trafficWeight`）の活用を注記 |
| L-09 | Low | infra-ops-reviewer | 可用性 | §7 サーキットブレーカー Open 時のフォールバック戦略で CouponService と PointService に「デフォルト値」を返すとあるが、ユーザーへのフィードバック（UI 上の表示メッセージ）が未定義 | フォールバック時のレスポンスボディにフォールバック状態であることを示すフラグ（例: `"degraded": true`）の付与を検討 |
| L-10 | Low | audit-reviewer | トレーサビリティ | §9 ログ設定で `correlationId` を記録しているが、`userId`（JWT 内の sub クレーム）の自動付与が記載されていない。認証済みリクエストでは userId をログに含めることで、問い合わせ時のトレーサビリティが向上する | Serilog の `Enrich` に JWT の `sub` クレームから `UserId` を抽出するカスタムエンリッチャーを追加する設計を検討 |
| L-11 | Low | ux-accessibility-reviewer | UX | §11 エラーレスポンスの `detail` フィールドが日本語のみ。設計書内の CORS 設定では `Accept-Language` ヘッダーを許可しているが、エラーメッセージの多言語対応（日英）の実装パターンが未記載 | エラーメッセージ i18n 対応方針（リソースファイル or Accept-Language ヘッダーによる切替）の概要を注記 |

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-01 | 通常 | architect | MailSendService の管理者 API ルーティングの扱い: 完全内部サービスとするか、管理者 API のみ Gateway 経由で公開するか。spec.md では管理者向けテンプレート管理 API が定義されている | テックリード / PO |
| E-02 | 通常 | performance-reviewer | §12 パフォーマンスメトリクス「現状」列の数値根拠: 負荷テスト結果か推定値か。設計段階で「現状」と称する数値を記載すべきかの判断 | テックリード |

## 競合解決記録

競合なし。全 Agent 間で矛盾する指摘は検出されなかった。

## ドキュメント横断分析

### マイクロサービス設計書カバレッジ（API Gateway ルーティング観点）

| サービス | ポート | YARP ルート定義 | ヘルスチェック | サーキットブレーカー | フォールバック |
|---------|--------|---------------|-------------|-------------------|-------------|
| AuthService | 5001 | ✅ `/api/auth/**` | ✅ | ✅ | ✅ (503) |
| UserManagementService | 5002 | ✅ `/api/users/**` | ✅ | ✅ | ✅ (503) |
| InventoryManagementService | 5003 | ✅ `/api/products/**`, `/api/inventory/**` | ✅ | ✅ | ✅ (Redis) |
| SalesManagementService | 5004 | ✅ `/api/orders/**`, `/api/reports/**` | ✅ | ✅ | ✅ (503) |
| PaymentCartService | 5005 | ✅ `/api/cart/**`, `/api/payments/**` | ✅ | ✅ | ✅ (503) |
| CouponService | 5006 | ✅ `/api/coupons/**` | ⚠️ 注1 | ✅ | ✅ (デフォルト) |
| PointService | 5007 | ✅ `/api/points/**` | ⚠️ 注1 | ✅ | ✅ (デフォルト) |
| MailSendService | 5008 | ❌ 未定義 (注2) | N/A | N/A | N/A |
| AiSupportService | 5009 | ✅ `/api/recommendations/**`, `/api/chat/**`, `/api/search/**`, `/api/analytics/**` | ✅ | ✅ | ✅ (静的リスト) |

**注1**: §13 YARP Clusters 設定で CouponService と PointService のヘルスチェック Active 設定が欠落（`HealthCheck` ブロックなし）。他サービスには `"Active": { "Enabled": true, "Interval": "00:00:30", "Path": "/health" }` が設定されている。

**注2**: MailSendService は §3 の注記で「内部専用（Kafka イベント駆動のみ）」としているが、spec.md では管理者向け API（`/admin/mail/templates` 等）が定義されている（M-12 参照）。

### spec.md との整合性サマリ

| 項目 | spec.md 定義 | api-gateway-design.md 記載 | 整合性 |
|------|------------|-------------------------|--------|
| ミドルウェア順序 | §ミドルウェアパイプライン順序設計 参照 | §5 で完全準拠 | ✅ |
| レート制限ポリシー | 5 カテゴリ (IP/User/Login/Checkout/Products) | §8 で全5カテゴリをカバー | ✅ |
| レート制限カウンター | Redis 共有 | §8, §12 で Redis 保存を明記 | ✅ |
| サーキットブレーカー | §1 API Gateway コンポーネント図 | §7 で全サービス別設定あり | ✅ |
| CORS 設定 | Azure Container Apps YAML 準拠 | §6 で整合した設定 | ✅ |
| ヘルスチェック | `/health` (Liveness) + `/health/ready` (Readiness) | §9, §21 で完全実装 | ✅ |
| エラーレスポンス | RFC 9457 (ADR-0007) | §11 で準拠 | ✅ |
| Saga レイテンシバジェット | API Gateway 通過 30ms | §12 で 30ms 明記 | ✅ |
| MailSendService ルーティング | 管理者 API あり | ルーティング未定義 | ⚠️ (M-12) |
| 管理者エンドポイント | 多数の `/admin/**` API | ルーティング未定義 | ⚠️ (M-01) |
| Dockerfile ヘルスチェック方式 | `dotnet HealthCheck.dll` | `curl` 使用 | ⚠️ (M-11) |

### 記載カバレッジ分析

| 設計領域 | カバレッジ | 評価 |
|---------|----------|------|
| YARP ルーティング | 90% | 管理者 admin ルートが未定義 |
| 認証・認可 | 95% | JWT 設定値のバインディング詳細が不足 |
| レート制限 | 100% | spec.md と完全に一致 |
| サーキットブレーカー | 100% | 全サービス別設定 + フォールバック定義済み |
| CORS | 95% | `AllowCredentials` の明示が不足 |
| セキュリティヘッダー | 100% | spec.md 準拠 + 追加ヘッダー（Cache-Control, Permissions-Policy, Referrer-Policy）あり |
| ミドルウェアパイプライン | 100% | AGENTS.md §11.3 完全準拠 |
| Correlation ID | 100% | §19 で完全実装（クラス + YARP Transform + テスト） |
| ヘルスチェック | 100% | Liveness/Readiness + バックエンドサービス検証 |
| 可観測性 (OpenTelemetry) | 95% | メトリクス・トレーシング定義済み。カスタムメトリクスの Meter 定義が概要のみ |
| ログ (Serilog) | 95% | PII マスキング定義済み。userId エンリッチャー未定義 |
| Docker/コンテナ | 90% | マルチステージ + 非root。HealthCheck 方式に spec.md との矛盾 |
| .NET Aspire 統合 | 90% | AppHost 設定あり。MailSendService 登録欠落 |
| テスト | 90% | Unit/Integration/Security テスト詳細あり。E2E テスト欠落 |
| エラーハンドリング | 90% | RFC 9457 準拠。例外分岐が AGENTS.md §4.7 より不完全 |
| Program.cs 完全設計 | 95% | §24 で統合ビュー提供。例外ハンドラーの分岐不足 |

### 未定義・曖昧な領域

| # | 領域 | 影響度 | 対応タイミング |
|---|------|--------|-------------|
| 1 | 管理者 API ルーティング（`/admin/**`） | Medium | 実装前 |
| 2 | JWT 署名鍵・Issuer・Audience の設定バインディング詳細 | Medium | 実装時 |
| 3 | MailSendService の管理者 API 公開方針 | Medium | 設計決定時 |
| 4 | CORS `AllowCredentials()` の要否 | Medium | 実装前 |
| 5 | パフォーマンスメトリクス「現状」数値の根拠 | Low | 負荷テスト時 |

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### 観点: ビジネス要件の完全性
- **判定**: ✅ Pass (Medium 1, Low 1)

#### 評価
API Gateway の設計書は、EC サイトのビジネス要件（商品閲覧、カート操作、チェックアウト、ユーザー管理、AI レコメンデーション）に対して、YARP ルーティングが適切に設計されている。ゲスト購入対応（`/api/cart/items` が AllowAnonymous）やクーポンの部分的匿名アクセスなど、ユーザーフロー別の認証要否が適切に分類されている。

#### Medium 指摘
- **M-12**: MailSendService の管理者 API（テンプレート管理、送信ログ参照）へのルーティングが欠落。管理者ペルソナ（佐藤次郎）のユーザーストーリー「メールテンプレートの管理」に必要。

#### Low 指摘
- **L-01**: §1 と §17 の記載重複。

</details>

<details>
<summary>architect レビューレポート</summary>

### 観点: マイクロサービスアーキテクチャ設計
- **判定**: ✅ Pass (Medium 2, Low 1)

#### 評価
YARP ベースの API Gateway 設計は、マイクロサービスアーキテクチャの原則に準拠。サービスディスカバリ（.NET Aspire WithReference）、サーキットブレーカー（サービス別設定）、Correlation ID（マイクロサービス間伝搬）が適切に設計されている。レイヤードアーキテクチャの依存方向もゲートウェイの責務範囲内で遵守。

#### Medium 指摘
- **M-01**: 管理者 admin ルートの欠落。spec.md で定義されている `AdminOnly` エンドポイント群のゲートウェイルートが未定義。
- **M-02**: AppHost での MailSendService 登録欠落。

#### Low 指摘
- **L-02**: コンポーネント図に FluentValidation が含まれているが §22 で不要と明記。図表の整合性不足。

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 観点: 技術標準の横断適合性・規約遵守
- **判定**: ✅ Pass (Medium 1)

#### 評価
設計書全体が AGENTS.md および `.github/instructions/` の規約に高い水準で準拠している。C# 14 の primary constructor、record 型、sealed クラスの活用例が適切。CancellationToken の伝搬、ILogger<T> によるログ出力、DI パターンも規約遵守。§19-§24 の追記セクションにより、実装コード生成に十分な詳細度を持つ。

#### Medium 指摘
- **M-13**: ミドルウェア順序の §5/§14/§24 間の微妙な不一致（SecurityHeaders の位置）。

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 観点: コード例の正確性・C# 14 機能活用
- **判定**: ✅ Pass (Medium 2, Low 1)

#### 評価
§19-§24 のコード例は C# 14 / .NET 10 の機能を適切に活用。primary constructor によるミドルウェア DI、sealed クラス、expression-bodied メンバー、`is { }` パターンマッチングが正しく使用されている。テストコードも `Should_期待結果_When_条件` パターンに準拠。`Console.WriteLine` や `.Result` / `.Wait()` の禁止パターンは全コード例で回避されている。

#### Medium 指摘
- **M-03**: §11 の例外ハンドラーで UnauthorizedException/ForbiddenException/ConcurrencyException 分岐が不完全。
- **M-04**: §24 の最終版 Program.cs でも同様の不完全な例外分岐。

#### Low 指摘
- **L-03**: §5 のインラインミドルウェア例と §19 の専用クラス実装で Correlation ID の実装パターンが 2 重化。

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 観点: OWASP Top 10・認証/認可設計・秘密情報管理
- **判定**: ✅ Pass (Medium 2, Low 1)

#### 評価
セキュリティ設計は高水準。OWASP Top 10 の主要項目がカバーされている:
- **A01 アクセス制御**: FallbackPolicy による認証必須化 + AllowAnonymous の明示的除外
- **A02 暗号化**: TLS 1.3 + HSTS
- **A03 インジェクション**: API Gateway はパススルーのため下流サービス委譲（適切）
- **A05 セキュリティ設定ミス**: セキュリティヘッダー 7 種の包括的付与
- **A07 認証失敗**: レート制限（ログイン 5 req/min/IP）でブルートフォース防止
- **A09 ログ・監視**: PII マスキング + Correlation ID + OpenTelemetry

秘密情報のハードコードなし（環境変数参照パターン）。Azure Key Vault 連携も設計済み。

#### Medium 指摘
- **M-05**: JWT 検証パラメータの設定バインディング詳細不足。
- **M-06**: CORS `AllowCredentials()` の要否未記載。

#### Low 指摘
- **L-04**: 全レスポンスへの `Cache-Control: no-store` は過剰。パス別キャッシュ戦略が望ましい。

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 観点: DB スキーマ設計・EF Core マッピング
- **判定**: ✅ Pass (Low 1)

#### 評価
API Gateway は DB を持たないサービスであるため、DBA 観点の指摘は最小限。Redis のキー設計（レート制限、認証キャッシュ、ルートキャッシュ）は合理的。Redis TTL の設定が明記されており、データ肥大化のリスクは低い。

#### Low 指摘
- **L-05**: Redis eviction policy の明示がない。

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 観点: テスト戦略・カバレッジ目標
- **判定**: ✅ Pass (Medium 1, Low 1)

#### 評価
§16 と §23 で包括的なテスト戦略が定義されている。Unit Test（ミドルウェア）、Integration Test（YARP ルーティング、JWT 認証、CORS）、レート制限テスト（Redis Testcontainer）、セキュリティテスト、パフォーマンステストが網羅的にカバー。テストメソッド命名は規約準拠。AAA パターンの適用が全テストコードで確認できる。`WebApplicationFactory` と `TestAuthHandler` による認証モックパターンは適切。

#### Medium 指摘
- **M-07**: E2E テスト（フロントエンド統合テスト）の記載欠落。

#### Low 指摘
- **L-06**: xUnit の `IClassFixture` と primary constructor の互換性確認。

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 観点: パフォーマンス SLA・スケーラビリティ
- **判定**: ✅ Pass (Medium 1, Low 1)

#### 評価
パフォーマンス設計は spec.md の SLO 要件と整合:
- API Gateway 通過レイテンシ: 30ms（spec.md Saga バジェットと一致）
- p95 レイテンシ目標: 50ms
- スループット目標: 1000+ req/s
- Redis キャッシュ戦略（レート制限 1 分、認証 5 分、ルート 60 分）が適切に階層化
- サーキットブレーカー設定がサービス特性に応じて差別化（PaymentCartService は低しきい値 30%、AiSupportService は高しきい値 70%）

#### Medium 指摘
- **M-08**: パフォーマンスメトリクスの「現状」列の数値根拠が不明。

#### Low 指摘
- **L-07**: サーキットブレーカーしきい値の設計判断理由が未記載。

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 観点: GDPR・個人情報保護法・PCI DSS
- **判定**: ✅ Pass (Medium 1)

#### 評価
PII 保護設計が §9 で明示されている。Authorization ヘッダーの JWT マスキング、Cookie ヘッダーの除外、パスワードフィールドのマスク、メールアドレスの部分マスクが定義済み。IP アドレスはレート制限ログにのみ記録する方針。PCI DSS については API Gateway はカード情報を一切扱わない（ADR-0008 準拠）ため対象外。

#### Medium 指摘
- **M-09**: Redis キー内の IP アドレスの GDPR 上の扱いと保持期間の明示不足。

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 観点: NuGet ライセンス適合性・依存関係
- **判定**: ✅ Pass (Medium 1)

#### 評価
§2 の主要ライブラリは全て AGENTS.md §8 の許可パッケージに含まれるか、正当な追加パッケージ。禁止パッケージ（`System.Web`, `log4net`, `Newtonsoft.Json`, `WebClient` 等）の使用なし。プレリリース版の使用なし。`YARP.ReverseProxy 2.*` は MIT ライセンスで商用利用可。

#### Medium 指摘
- **M-10**: Azure 固有パッケージ（`Azure.Identity`, `Azure.Security.KeyVault.Secrets`）が AGENTS.md の基本パッケージと異なることの注釈不足。

</details>

<details>
<summary>release-manager レビューレポート</summary>

### 観点: リリース戦略・ロールバック計画
- **判定**: ✅ Pass (Low 1)

#### 評価
§10 のデプロイ設定はマルチステージビルド、非 root 実行、ヘルスチェック、環境変数による設定注入が定義済み。.NET Aspire 統合（§15）によるローカル開発環境の構築も適切。

#### Low 指摘
- **L-08**: Blue/Green デプロイ・カナリアリリースの設計欠落。

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 観点: コンテナ設計・可観測性・ヘルスチェック
- **判定**: ✅ Pass (Medium 1, Low 1)

#### 評価
Dockerfile がマルチステージビルド + 非 root ユーザーで AGENTS.md §Dockerfile 規約に準拠。ヘルスチェックは Liveness（`/health`）+ Readiness（`/health/ready`）の 2 エンドポイント構成で、Redis と全バックエンドサービスの到達性を検証する BackendServicesHealthCheck が実装されている。Critical/Optional サービスの分離（Critical 障害 = Unhealthy、Optional 障害 = Degraded）は運用上の優れた設計。OpenTelemetry によるトレーシング・メトリクスも定義済み。

#### Medium 指摘
- **M-11**: Dockerfile の HEALTHCHECK が `curl` を使用しているが spec.md では `dotnet HealthCheck.dll` を規定。矛盾。

#### Low 指摘
- **L-09**: フォールバック時のレスポンスに degraded フラグがない。

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 観点: トレーサビリティ・ドキュメント整合性・ADR
- **判定**: ✅ Pass (Low 1)

#### 評価
トレーサビリティ設計は高水準:
- Correlation ID: §19 で完全な実装（生成 → レスポンスヘッダー → Serilog コンテキスト → YARP 転送）
- 構造化ログ: Serilog CompactJsonFormatter + traceId/spanId/correlationId
- PII マスキング: §9 で対象フィールドとマスク方法を定義
- ADR 参照: ADR-0007 (RFC 9457) への準拠が明示

#### Low 指摘
- **L-10**: 認証済みリクエストでの userId 自動ログ付与が未定義。

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 観点: UX 設計品質
- **判定**: ✅ Pass (Low 1)

#### 評価
API Gateway は主にバックエンド基盤であるため UX 直接影響は限定的。レート制限時の `429 + Retry-After` ヘッダーによるクライアント側リトライ制御は適切。エラーレスポンスが RFC 9457 準拠であり、フロントエンドでの統一的なエラーハンドリングが可能。

#### Low 指摘
- **L-11**: エラーメッセージの多言語対応パターンが未記載。

</details>

---

## 総評

`api-gateway-design.md` は、過去レビュー（check-report-1, 2）で指摘された主要課題を §19〜§24 の追記セクションで大幅に補完した結果、**エンタープライズ品質の API Gateway 設計書として十分な完成度** に達している。

**特に優れている点**:
1. ミドルウェアパイプライン順序の厳密な定義（AGENTS.md §11.3 完全準拠）
2. 全バックエンドサービスに対するサーキットブレーカー + フォールバック戦略の個別定義
3. §19-§24 の完全実装コード（CorrelationIdMiddleware, SecurityHeadersMiddleware, BackendServicesHealthCheck, テストコード, Program.cs 統合ビュー）
4. PII マスキングの包括的定義
5. YARP ルート設定の詳細な JSON 定義

**実装前に対応推奨の項目**:
1. 管理者 admin ルートの追加（M-01, M-12）
2. Dockerfile のヘルスチェック方式の spec.md 統一（M-11）
3. 例外ハンドラーの AGENTS.md §4.7 完全準拠（M-03, M-04）
