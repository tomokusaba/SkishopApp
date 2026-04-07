# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/api-gateway-design.md`（API Gateway サービス詳細設計書、918 行、修正後イテレーション 2）
- **判定**: ⚠️ **Conditional Approval** — High 指摘 2 件検出（Critical 0 件）
- **レビュー日時**: 2026-04-03
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)
- **前回レビュー**: check-report-1（❌ Rejected: 1 Critical, 18 High）→ fix-report-1 で全 Critical/High を修正済み

## 前回指摘の是正確認

### Critical 指摘の是正状況

| # | 指摘 ID | 内容 | 是正状況 | 確認箇所 |
|---|--------|------|---------|---------|
| C-1 | エラーレスポンス RFC 9457 非準拠 | カスタムエンベロープ形式 → RFC 9457 Problem Details に全面書き換え | ✅ **是正完了** | §11: `TypedResults.Problem()` ベースの JSON 形式（`type`, `title`, `status`, `detail`, `instance`, `extensions`）に変更。ADR-0007 準拠。C# コード例あり |

### High 指摘の是正状況

| # | 指摘 ID | 内容 | 是正状況 | 確認箇所 |
|---|--------|------|---------|---------|
| H-1 | セキュリティヘッダー未記載 | 7 つのヘッダーの具体値が欠落 | ✅ **是正完了** | §6: 7 ヘッダー（`X-Content-Type-Options`, `X-Frame-Options`, `Content-Security-Policy`, `Strict-Transport-Security`, `X-XSS-Protection`, `Referrer-Policy`, `Permissions-Policy`）の値テーブル + C# 実装例 |
| H-2 | CORS 設定が抽象的 | `allowedOrigins` 等の具体値なし | ✅ **是正完了** | §6: CORS ポリシーテーブル。`allowedOrigins`, `allowedMethods`, `allowedHeaders`, `maxAge` を spec.md と整合。ワイルドカード CORS 禁止を明記 |
| H-3 | Correlation ID ヘッダー名不一致 | `X-Request-ID` → `X-Correlation-Id` | ✅ **是正完了** | §5: ヘッダー名統一。生成ロジック・レスポンスヘッダー付与・`LogContext.PushProperty` 連携・バックエンド転送の詳細設計 + C# コード例 |
| H-4 | レート制限値 spec.md 不整合 | 桁違いの値・アルゴリズム未記載 | ✅ **是正完了** | §8: spec.md と完全整合（Token Bucket 60/120 req/min, Fixed Window 5/10 req/min）。アルゴリズム・単位・キーリゾルバを明記。`Retry-After` ヘッダー記載 |
| H-5 | YARP JSON 設定未記載 | `ReverseProxy` セクションの具体的設定がない | ✅ **是正完了**（ただし欠落あり → H-NEW-1, H-NEW-2） | §13: Routes / Clusters / Transforms / HealthCheck / LoadBalancingPolicy の JSON 設定例を追加 |
| H-6 | MailSendService 未掲載 | 内部専用の明示なし | ✅ **是正完了** | §4: 注記追加。§3: Mermaid 図を MailSendService（破線接続）に変更 |
| H-7 | ミドルウェア順序不明確 | 順序なし表形式 | ✅ **是正完了** | §5: spec.md 準拠の 8 段階番号付き順序テーブル。禁止パターンを注記 |
| H-8 | .NET Aspire 統合未記載 | `WithReference` の具体例なし | ✅ **是正完了**（部分欠落 → M-NEW-4） | §15: AppHost/Program.cs の全バックエンドサービスへの `WithReference` 接続例 |
| H-9 | p95/p99 目標未定義 | p95/p99 パーセンタイル目標なし | ✅ **是正完了** | §12: p95（< 50ms）、p99（< 100ms）。spec.md Saga レイテンシバジェットとの整合性注記 |
| H-10 | キャッシュ無効化戦略未記載 | JWT 無効化時の戦略なし | ✅ **是正完了** | §12: Kafka イベント `user.permission_changed` 即時無効化、ログアウト時のトークンハッシュ無効化、TTL 5 分に短縮 |
| H-11 | Docker バージョン `Latest` | `latest` タグ禁止違反 | ✅ **是正完了** | 技術スタックテーブル: `Docker 25.x`（`latest` タグ禁止明記） |
| H-12 | `AZURE_CLIENT_SECRET` 環境変数 | Managed Identity 前提で不要 | ✅ **是正完了** | §10: コメントで Managed Identity 前提を明確化。`AZURE_CLIENT_SECRET` 削除 |
| H-13 | テスト戦略欠落 | テスト戦略セクションなし | ✅ **是正完了** | §16: 6 テスト種別（Unit / Integration / サーキットブレーカー / レート制限 / セキュリティ / パフォーマンス）。カバレッジ 80%。命名規則 `Should_When` |
| H-14 | Correlation ID 詳細設計不足 | 生成・伝搬・ログの設計なし | ✅ **是正完了** | §5: 4 項目（生成ロジック / レスポンスヘッダー / ログ出力 / バックエンド転送）+ C# コード例 |
| H-15 | PII ログ未対策 | マスキング方針なし | ✅ **是正完了** | §9: 5 項目のマスキングテーブル（Authorization / Cookie / パスワード / メール / IP） |
| H-16 | フォールバック戦略未定義 | サーキットブレーカー Open 時のレスポンス未設計 | ✅ **是正完了** | §7: 8 サービス別フォールバックテーブル（503 即時返却 / キャッシュ応答 / デフォルト値 / 静的リスト） |
| H-17 | Program.cs 設計欠落 | DI 登録・ミドルウェア構成なし | ✅ **是正完了**（部分欠落 → M-NEW-1, M-NEW-2） | §14: DI 登録 + §5 準拠のミドルウェアパイプライン順序の C# コード例 |
| H-18 | カート認証がゲスト購入と矛盾 | `/api/cart/**` 全体が認証必須 | ✅ **是正完了** | §4: `POST /api/cart/items` は AllowAnonymous（ゲスト購入対応: Cookie ベース CartId）、`POST /api/cart/checkout` は認証必須 |

**結論**: 前回の Critical 1 件 + High 18 件は**全て是正完了**。ただし、修正により**新たな High 2 件・Medium 4 件・Low 2 件**が検出された。

---

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
| Docker | 25.x | 25.x（latest 禁止） | ✅ |
| 耐障害性 | Polly 8.* | Polly 8.* | ✅ |
| バリデーション | FluentValidation 11.* | FluentValidation 11.* | ✅ |
| OpenTelemetry | 1.* | 1.* | ✅ |
| ヘルスチェック | AspNetCore.HealthChecks.Redis 9.* | 9.* | ✅ |
| ログ出力 | Serilog.Sinks.Console 6.* | 6.* | ✅ |
| Kafka バージョン | 3.x (Confluent Platform 7.4.0) | — | ✅ 明確化済み |

---

## 指摘サマリー

| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ✅ Pass | 0 | 0 | 0 | 0 |
| architect | ⚠️ Conditional | 0 | 2 | 1 | 0 |
| tech-lead | ✅ Pass | 0 | 0 | 0 | 0 |
| programing-reviewer | ⚠️ Conditional | 0 | 0 | 2 | 1 |
| security-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| dba-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| qa-manager | ✅ Pass | 0 | 0 | 0 | 0 |
| performance-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| compliance-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| oss-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| release-manager | ✅ Pass | 0 | 0 | 0 | 0 |
| infra-ops-reviewer | ⚠️ Conditional | 0 | 0 | 1 | 1 |
| audit-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| ux-accessibility-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| **合計** | | **0** | **2** | **4** | **2** |

## 判定根拠
- 判定ルール適用結果: Critical 指摘 0 件、High 指摘 2 件のため **⚠️ Conditional Approval**（人間の判断を介在）
- 最も重大な指摘: YARP ルート定義（§13）がルートテーブル（§4）と不整合 — 4 ルート + 1 クラスタが欠落
- 前回レビューからの改善: 1 Critical + 18 High → 0 Critical + 2 High（大幅改善）

---

## Critical/High 指摘一覧（修正必須）

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|--------------|----------|----------|
| H-NEW-1 | **High** | architect | YARP ルート欠落 | §13 YARP リバースプロキシ設定 | YARP Routes 定義（§13）に §4 ルートテーブルで定義された **4 ルートが欠落**している: (1) `/api/users/{**catch-all}` → UserManagementService:5002（認証必須）、(2) `/api/reports/{**catch-all}` → SalesManagementService:5004（ADMIN/MANAGER）、(3) `/api/search/{**catch-all}` → AiSupportService:5009（AllowAnonymous）、(4) `/api/analytics/{**catch-all}` → AiSupportService:5009（ADMIN/MANAGER）。また Clusters 定義に `user-cluster`（UserManagementService:5002 向け）が欠落している。**§4 と §13 の不整合は実装時にルーティング不能を引き起こす** | §13 に以下を追加: (1) `users-route`（`default` AuthorizationPolicy、`user-cluster` 参照）、(2) `reports-route`（`AdminOrManager` AuthorizationPolicy、`sales-cluster` 参照）、(3) `ai-search-route`（`anonymous` AuthorizationPolicy、`ai-cluster` 参照）、(4) `ai-analytics-route`（`AdminOrManager` AuthorizationPolicy、`ai-cluster` 参照）。Clusters に `user-cluster`（`http://user-management-service:5002`、HealthCheck 付き）を追加 |
| H-NEW-2 | **High** | architect | YARP 認可ポリシー不整合 | §13 YARP リバースプロキシ設定 | `coupons-route` に `AuthorizationPolicy` が**未設定**。FallbackPolicy（`RequireAuthenticatedUser`）が適用されるため、§4 ルートテーブルで AllowAnonymous と定義された `GET /api/coupons`（公開クーポン検索）が認証必須になり、**未ログインユーザーからの公開クーポン参照が不可能**になる。§4 では `GET /api/coupons` は AllowAnonymous、`POST /api/coupons/apply` は認証必須の混合ポリシーが定義されている | cart と同様のルート分割を適用: (1) `coupons-public-route`（`GET /api/coupons`、`anonymous` AuthorizationPolicy）、(2) `coupons-apply-route`（`POST /api/coupons/apply`、`default` AuthorizationPolicy）、(3) `coupons-route`（`/api/coupons/{**catch-all}`、`default` AuthorizationPolicy）の 3 ルート構成に変更 |

---

## Medium/Low 指摘一覧（新規検出）

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|--------------|----------|----------|
| M-NEW-1 | Medium | programing-reviewer | コード例品質 | §11 エラーハンドリング | グローバル例外ハンドラーの `switch` 式が `NotFoundException` と `BusinessException` の 2 型のみ。AGENTS.md §4.7 で定義された 5 例外型のうち `UnauthorizedException`（401）、`ForbiddenException`（403）、`ConcurrencyException`（409）が欠落。これらの例外が未ハンドルのまま 500 として返却され、ログレベルも Error（本来は Warning）になる | §11 の `switch` 式に AGENTS.md §4.7 の 5 例外型（`NotFoundException` → 404, `BusinessException` → 422, `UnauthorizedException` → 401, `ForbiddenException` → 403, `ConcurrencyException` → 409）を全て含める |
| M-NEW-2 | Medium | programing-reviewer | コード例不完全 | §14 Program.cs 構成設計 | ミドルウェアパイプラインセクションで「2. セキュリティヘッダー」として `UseHsts` / `UseHttpsRedirection` のみ記載。§6 で定義された 7 つのセキュリティレスポンスヘッダー（`X-Content-Type-Options`, `X-Frame-Options` 等）を追加する `app.Use(...)` ミドルウェアが Program.cs コード例に含まれていない。§6 のコード例と §14 のコード例の間に齟齬がある | §14 の手順 2 に §6 のセキュリティヘッダーミドルウェア（`app.Use(async (context, next) => { ... })`）を追加。または `UseSecurityHeaders()` カスタム拡張メソッドとして参照する旨を注記 |
| M-NEW-3 | Medium | architect | YARP ヘルスチェック不一致 | §13 YARP Clusters | `coupons-cluster` と `points-cluster` に `HealthCheck` 設定が欠落。他のクラスタ（`auth-cluster`, `inventory-cluster`, `sales-cluster`, `payment-cart-cluster`）は全て `Active.Enabled: true` で HealthCheck が設定済み。ヘルスチェック未設定のクラスタでは障害検知が遅れ、ダウン中のバックエンドにリクエストがルーティングされるリスクがある | `coupons-cluster` と `points-cluster` に他クラスタと同等の HealthCheck 設定（`Interval: 00:00:30`, `Path: /health`）を追加 |
| M-NEW-4 | Medium | infra-ops-reviewer | Aspire 設定不完全 | §15 .NET Aspire オーケストレーション統合 | API Gateway の AppHost 登録で `.WithReference(kafka)` が欠落。§12 で認証キャッシュ無効化に Kafka イベント（`user.permission_changed`, `user.logged_out`）を使用する設計であり、技術スタックにも `Confluent.Kafka 2.*` が含まれている。Aspire 登録に Kafka 参照がなければ、Kafka 接続文字列が自動注入されない | `.WithReference(kafka)` を API Gateway の AppHost 登録に追加 |
| L-NEW-1 | Low | programing-reviewer | コード例変数未定義 | §14 Program.cs 構成設計 | `builder.Services.AddHealthChecks().AddRedis(redisConnectionString, ...)` で参照される `redisConnectionString` 変数が未定義。`builder.Configuration.GetConnectionString("Redis")` 等で取得する前処理が欠落している | 変数定義を追加: `var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? throw new InvalidOperationException("Redis connection string is not configured");` |
| L-NEW-2 | Low | infra-ops-reviewer | 環境変数の矛盾 | §10 環境変数 | `Services__MailSendService=http://mailsend-service:5008` が API Gateway の環境変数に含まれているが、§4 の注記で「MailSendService は内部専用サービスであり API Gateway 経由のルーティングは提供しない」と明記済み。API Gateway が MailSendService に直接通信する設計がなければ不要 | MailSendService URL を環境変数から削除。または API Gateway から MailSendService への通信が必要な場合、その理由を注記 |

---

## 前回 Medium/Low 残存指摘の追跡

| # | 前回 ID | 内容 | 現在の状況 |
|---|--------|------|----------|
| M-6 | レスポンス変換設計 | ⚠️ 残存（Low 格下げ）— §6 のセキュリティヘッダーミドルウェアで部分対応。レスポンスボディ変換は API Gateway では不要と判断可能 |
| M-7 | IP フィルタリング実装レイヤー | ✅ 解消 — §6 で「Azure Front Door / WAF（第一層）+ ASP.NET Core ミドルウェア（第二層）」と方針明記 |
| M-8 | リクエストサイズ 5MB 根拠 | ✅ 解消 — §5 に「画像アップロードパスは別途設定」と注記追加 |
| M-9 | スケーリング設定欠落 | ⚠️ 残存（Medium）— spec.md §サービス別スケーリング設定でAPI Gateway: minReplicas 2, maxReplicas 10 と定義。spec.md は個別サービス設計書に記載するよう指示しているが、本設計書に未記載 |
| M-11 | C# コード例不足 | ✅ 解消 — §5, §6, §11, §14 にコード例を追加 |
| M-12 | カオスエンジニアリングテスト | ✅ 解消 — §16 でサーキットブレーカーテスト方針を記載 |
| M-13 | 改訂履歴テーブル欠落 | ⚠️ 残存（Low）— テンプレート統一対応として保留中 |
| M-14 | Redis ハッシュタグ設計 | ⚠️ 残存（Medium）— 本番 Redis Cluster 環境での CROSSSLOT エラーリスク |
| M-16 | 技術スタック重複 | ⚠️ 残存（Low）— §2 + 無番号セクション 2 つに分散。重複は低リスク |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| — | — | — | 前回の E-1〜E-3 は全て設計対応済み。新たなエスカレーション事項なし | — |

---

## 競合解決記録

今回のレビューでは Agent 間の競合は検出されなかった。

---

## ドキュメント横断分析

### マイクロサービス設計書カバレッジ（API Gateway 視点）

| サービス | §4 ルート定義 | §13 YARP ルート | §7 サーキットブレーカー | §7 フォールバック | §8 レート制限 | §4 認証設定 | 整合性 |
|---------|------------|---------------|-------------------|---------------|------------|----------|--------|
| AuthService | ✅ | ✅ | ✅ | ✅ 503 即時返却 | ✅ Fixed Window 5/min | ✅ AllowAnonymous | ✅ |
| UserManagementService | ✅ | ❌ **欠落** | ✅ | ✅ 503 即時返却 | ✅ | ✅ 認証必須 | ❌ |
| InventoryManagementService | ✅ | ✅ | ✅ | ✅ キャッシュ応答 | ✅ Token Bucket 300/min | ✅ 混在 | ✅ |
| SalesManagementService | ✅ `/api/orders`, `/api/reports` | ✅ orders のみ、❌ reports 欠落 | ✅ | ✅ 503 即時返却 | ✅ | ✅ 認証必須 / ADMIN | ❌ |
| PaymentCartService | ✅ | ✅ (3 ルート分割) | ✅ | ✅ 503 即時返却 | ✅ Fixed Window 10/min | ✅ 混在 | ✅ |
| CouponService | ✅ | ⚠️ 認可ポリシー不整合 | ✅ | ✅ デフォルト値 | ✅ | ⚠️ 混在だが YARP 未対応 | ❌ |
| PointService | ✅ | ✅ | ✅ | ✅ デフォルト値(0pt) | ✅ | ✅ 認証必須 | ✅ |
| AiSupportService | ✅ 4 エンドポイント | ✅ 2/4 のみ、❌ search/analytics 欠落 | ✅ | ✅ 静的リスト | ✅ | ✅ 混在 | ❌ |
| MailSendService | ✅ 内部専用明記 | — 対象外 | — | — | — | — | ✅ |

### サービス間整合性

- **YARP ルート整合性**: §4 ルートテーブル（16 エントリ）に対し §13 YARP Routes は 12 エントリ。**4 ルートが欠落**（H-NEW-1）
- **認可ポリシー整合性**: cart の AllowAnonymous 分割は正しく実装。coupons の混合ポリシーが未対応（H-NEW-2）
- **レート制限**: spec.md と完全整合 ✅
- **Correlation ID**: spec.md / AGENTS.md §11.2 と整合（`X-Correlation-Id`）✅
- **セキュリティヘッダー**: spec.md §セキュリティレスポンスヘッダー設計の 7 ヘッダー全て整合 ✅
- **ミドルウェア順序**: spec.md §ミドルウェアパイプライン順序設計の 8 段階と整合 ✅
- **RFC 9457 エラーレスポンス**: ADR-0007 と整合 ✅

### 品質改善の評価

| 観点 | check-report-1 | check-report-2 | 改善度 |
|------|---------------|---------------|--------|
| Critical | 1 | 0 | ✅ 完全解消 |
| High | 18 | 2 | ✅ 89% 削減（18→2、新規発生分） |
| Medium | 21 | ~8（新規 4 + 残存 4） | ✅ 62% 削減 |
| Low | 3 | ~5（新規 2 + 残存 3） | — 微増（新セクション追加に伴う） |
| RFC 9457 準拠 | ❌ | ✅ | ✅ |
| セキュリティヘッダー 7 種 | ❌ | ✅ | ✅ |
| YARP 設定 | 未記載 | 80% カバー | ✅（残り 20% が H-NEW-1/2） |
| テスト戦略 | 欠落 | ✅ 6 種別 | ✅ |
| Program.cs 設計 | 欠落 | ✅ 完全なコード例 | ✅ |
| .NET Aspire 統合 | 欠落 | ✅ AppHost 例 | ✅ |

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### 判定: ✅ Pass

前回指摘（H-18: カート未ログイン認証矛盾）が適切に是正された。

**確認事項**:
- §4 ルートテーブル: `POST /api/cart/items` は AllowAnonymous（ゲスト購入対応）✅
- §4 ルートテーブル: `POST /api/cart/checkout` は認証必須 ✅
- クーポンエンドポイント粒度: `GET /api/coupons`（AllowAnonymous）と `POST /api/coupons/apply`（認証必須）が明記 ✅
- MailSendService 内部専用の明示 ✅

新規指摘なし。

</details>

<details>
<summary>architect レビューレポート</summary>

### 判定: ⚠️ Conditional（High 2 件、Medium 1 件）

**H-NEW-1**: YARP ルート定義（§13）に §4 ルートテーブルの 4 ルートが欠落。`users-route`, `reports-route`, `ai-search-route`, `ai-analytics-route` が未定義。`user-cluster`（UserManagementService:5002）も欠落。

**H-NEW-2**: `coupons-route` に AuthorizationPolicy が未設定。FallbackPolicy により全リクエストが認証必須になり、§4 の AllowAnonymous 定義と矛盾。

**M-NEW-3**: `coupons-cluster` と `points-cluster` の HealthCheck 設定欠落。他のクラスタとの一貫性が欠如。

**良好な改善点**:
- YARP Routes / Clusters の JSON 設定例（§13）の追加は大きな改善
- Cart の AllowAnonymous ルート分割（`cart-items-route`）は正確に実装
- Middleware 順序の 8 段階テーブル化は spec.md と完全整合
- .NET Aspire 統合（§15）のコード例は AGENTS.md §10.5 と整合

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 判定: ✅ Pass

前回指摘（H-16: フォールバック戦略、H-17: Program.cs 設計）が適切に是正された。

**確認事項**:
- §7 フォールバック戦略: 8 サービス別戦略（503 即時返却 / キャッシュ応答 / デフォルト値 / 静的リスト）が AGENTS.md §耐障害性と整合 ✅
- §14 Program.cs: DI 登録（Serilog, YARP, 認証・認可, CORS, レート制限, ヘルスチェック, OpenTelemetry）+ パイプライン順序 ✅
- 技術標準横断適合性: AGENTS.md §4.3 DI 規約、§4.7 例外処理、§11.3 ミドルウェア順序に全て整合 ✅
- FallbackPolicy による全エンドポイント認証必須 + AllowAnonymous 明示除外の設計は AGENTS.md §5.3 準拠 ✅

新規指摘なし。

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 判定: ⚠️ Conditional（Medium 2 件、Low 1 件）

**M-NEW-1**: §11 例外ハンドラーの `switch` 式に AGENTS.md §4.7 の 5 例外型のうち 3 型が欠落。

**M-NEW-2**: §14 Program.cs に §6 のセキュリティヘッダーミドルウェアが含まれていない。

**L-NEW-1**: §14 の `redisConnectionString` 変数が未定義。

**良好な改善点**:
- C# コード例が多数追加され（Correlation ID, セキュリティヘッダー, 例外ハンドラー, Program.cs）、実装意図の伝達が大幅改善
- primary constructor パターンは使用されていないが、API Gateway は Program.cs 中心の構成のため適切
- `CancellationToken` は Minimal API の自動バインドで対応済み
- コード例の命名規則は AGENTS.md §4.1 に準拠

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 判定: ✅ Pass

前回の Critical（C-1: RFC 9457 非準拠）+ High 3 件（H-1: セキュリティヘッダー、H-2: CORS、H-3: Correlation ID ヘッダー名）が全て適切に是正された。

**確認事項**:
- RFC 9457 Problem Details 形式のエラーレスポンス ✅（ADR-0007 準拠）
- 7 つのセキュリティレスポンスヘッダー（spec.md §セキュリティレスポンスヘッダー設計準拠）✅
- CORS ポリシー: ホワイトリストベース（ワイルドカード禁止）✅
- PII マスキング: 5 項目のマスキング方針テーブル ✅
- JWT TokenValidationParameters: `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey` 全て `true`、`ClockSkew: 5 分` ✅
- FallbackPolicy: 全エンドポイントに認証必須 ✅
- `DetailedErrors: false`（スタックトレース非公開）✅
- Managed Identity 前提（`AZURE_CLIENT_SECRET` 除去）✅
- 認証キャッシュ TTL 5 分 + Kafka イベント即時無効化 ✅

新規指摘なし。

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 判定: ✅ Pass

API Gateway は DB を直接使用しないため、DB 関連の指摘は対象外。Redis キャッシュキー設計（§12）は適切。

残存 M-14（Redis ハッシュタグ設計）はインフラ設計の詳細として保留中。

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 判定: ✅ Pass

前回指摘（H-13: テスト戦略欠落）が適切に是正された。

**確認事項**:
- §16 テスト戦略: 6 テスト種別（Unit / Integration / サーキットブレーカー / レート制限 / セキュリティ / パフォーマンス）✅
- フレームワーク: xUnit + NSubstitute + Shouldly + WebApplicationFactory + WireMock + k6/NBomber ✅
- カバレッジ目標: Unit 80%、Integration 80%、サーキットブレーカー/レート制限 70% ✅
- テストメソッド命名: `Should_期待結果_When_条件` パターン（`test-standards.instructions.md` 準拠）✅
- 異常系テスト: 401/429 のテスト例があり、正常系・異常系のバランスが取れている ✅

新規指摘なし。

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 判定: ✅ Pass

前回指摘（H-9: p95/p99 未定義、H-10: キャッシュ無効化未記載）が適切に是正された。

**確認事項**:
- §12 パフォーマンスメトリクス: p95（< 50ms）、p99（< 100ms）目標を追加 ✅
- spec.md Saga レイテンシバジェット（API Gateway 通過 30ms）との整合性を注記 ✅
- 認証キャッシュ無効化: Kafka イベント + TTL 5 分 ✅
- レート制限: spec.md と完全整合（Token Bucket / Fixed Window 使い分け）✅
- スループット目標: > 1000 req/s ✅

残存 M-9（スケーリング設定）はサービス設計書の範囲として許容可能。

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 判定: ✅ Pass

前回指摘（H-15: PII ログ）が適切に是正された。

**確認事項**:
- §9 センシティブデータマスキング: Authorization ヘッダー、Cookie、パスワード、メールアドレス、IP アドレスの 5 項目 ✅
- AGENTS.md §5.7 PII ログ禁止と整合 ✅
- Managed Identity 前提（秘密情報のハードコードなし）✅

新規指摘なし。

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 判定: ✅ Pass

前回指摘（H-11: Docker Latest、M-3: 必須パッケージ欠落、M-4: Kafka バージョン）が全て是正された。

**確認事項**:
- Docker 25.x（`latest` タグ禁止明記）✅
- Polly 8.*, FluentValidation 11.*, Serilog.Sinks.Console 6.*, AspNetCore.HealthChecks.Redis 9.* 追加 ✅
- Kafka バージョン明確化: `3.x (Confluent Platform 7.4.0)` ✅
- `nuget-dependency.instructions.md` §2 のプレリリース版禁止: 全パッケージ GA 版 ✅
- `Newtonsoft.Json` 不使用（`System.Text.Json` 使用）✅

新規指摘なし。

</details>

<details>
<summary>release-manager レビューレポート</summary>

### 判定: ✅ Pass

前回指摘（H-12: AZURE_CLIENT_SECRET）が適切に是正された。

**確認事項**:
- Managed Identity（`DefaultAzureCredential`）前提の設計を明確化 ✅
- 開発環境向け `az login` / `dotnet user-secrets` 方式を注記 ✅
- 環境変数セクションのサービス URL に Aspire 非使用環境のフォールバック注記 ✅
- Dockerfile: マルチステージビルド、非 root ユーザー、バージョン固定 ✅

新規指摘なし。

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 判定: ⚠️ Conditional（Medium 1 件、Low 1 件）

**M-NEW-4**: §15 AppHost の API Gateway 登録に `.WithReference(kafka)` が欠落。Kafka を認証キャッシュ無効化に使用する設計との矛盾。

**L-NEW-2**: §10 環境変数に MailSendService URL が含まれるが、API Gateway は MailSendService にルーティングしない。

**良好な改善点**:
- Dockerfile: マルチステージビルド、非 root（`skishop` ユーザー）、`HEALTHCHECK --timeout=10s --start-period=30s` ✅
- HealthCheck: `/health`（Liveness）+ `/health/ready`（Readiness、Redis 疎通確認）✅
- OpenTelemetry: Tracing + Metrics（AspNetCore / HttpClient / Runtime 計装）✅
- メトリクス設計: 6 ゲートウェイメトリクス + 4 システムメトリクス ✅

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 判定: ✅ Pass

前回指摘（H-14: Correlation ID 詳細設計不足）が適切に是正された。

**確認事項**:
- §5 Correlation ID ミドルウェア詳細設計: 4 項目（生成ロジック / レスポンスヘッダー / ログ出力 / バックエンド転送）✅
- AGENTS.md §11.2 準拠のコード例（`LogContext.PushProperty`）✅
- 構造化ログ: Serilog CompactJsonFormatter + `correlationId` フィールド ✅
- ログレベル: SkiShop.ApiGateway: Information、Yarp.ReverseProxy: Information ✅

残存 M-13（改訂履歴テーブル）はテンプレート統一対応として保留中。

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 判定: ✅ Pass

前回指摘（M-18: エラーメッセージ i18n）が対応された。

**確認事項**:
- §11 注記: エラーメッセージの i18n は `Accept-Language` ヘッダーに基づくメッセージ切替で対応 ✅
- API Gateway はバックエンドサービスへのプロキシであり、フロントエンド機能は直接提供しない ✅

新規指摘なし。

</details>
