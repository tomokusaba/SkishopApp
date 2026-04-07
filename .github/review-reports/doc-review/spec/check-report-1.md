# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/spec.md`（SkiShop システム全体設計書）
- **判定**: ⚠️ **Conditional Approval** — High 指摘あり、人間の判断を介在
- **レビュー日時**: 2025-07-17 15:30
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

## 技術スタック検証結果
| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| 言語 | C# 14 | C# 14 | ✅ |
| ランタイム | .NET 10 (LTS) | .NET 10 (LTS) | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| オーケストレーション | .NET Aspire 13.1 | .NET Aspire 13.1 | ✅ |
| ORM | Entity Framework Core 10 | Entity Framework Core 10 | ✅ |
| DB | PostgreSQL 16 | PostgreSQL | ✅ |
| キャッシュ | Redis (StackExchange.Redis) | Redis (StackExchange.Redis) | ✅ |
| メッセージング | Apache Kafka (Confluent.Kafka) | Apache Kafka (Confluent.Kafka) | ✅ |
| 認証 | ASP.NET Core Identity + Microsoft.Identity.Web | ASP.NET Core Identity + Microsoft.Identity.Web | ✅ |
| AI | Semantic Kernel 1.x | Semantic Kernel 1.x | ✅ |
| API ゲートウェイ | YARP | YARP | ✅ |
| コンテナ化 | Docker | Docker | ✅ |

## 指摘サマリー
| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ⚠️ Conditional | 0 | 2 | 3 | 1 |
| architect | ⚠️ Conditional | 0 | 3 | 4 | 2 |
| tech-lead | ⚠️ Conditional | 0 | 2 | 3 | 1 |
| programing-reviewer | ✅ Pass | 0 | 1 | 3 | 2 |
| security-reviewer | ⚠️ Conditional | 0 | 3 | 2 | 1 |
| dba-reviewer | ⚠️ Conditional | 0 | 2 | 3 | 1 |
| qa-manager | ✅ Pass | 0 | 1 | 2 | 1 |
| performance-reviewer | ⚠️ Conditional | 0 | 2 | 2 | 1 |
| compliance-reviewer | ⚠️ Conditional | 0 | 2 | 3 | 0 |
| oss-reviewer | ✅ Pass | 0 | 0 | 2 | 1 |
| release-manager | ✅ Pass | 0 | 1 | 1 | 1 |
| infra-ops-reviewer | ✅ Pass | 0 | 1 | 2 | 1 |
| audit-reviewer | ✅ Pass | 0 | 1 | 2 | 1 |
| ux-accessibility-reviewer | ✅ Pass | 0 | 1 | 2 | 1 |
| **合計** | | **0** | **22** | **34** | **15** |

## 判定根拠
- 判定ルール適用結果: Critical 指摘 0 件、High 指摘 22 件 → **⚠️ Conditional Approval**（人間の判断を介在）
- 最も重大な指摘: mTLS の Phase 2 先送りによるサービス間通信の認証不在（security-reviewer）、Saga デッドラインタイマーの具体的実装設計の欠如（architect）、マルチテナント Advisory Lock の干渉リスク（dba-reviewer）

## Critical/High 指摘一覧（修正必須）
| # | 重要度 | 出典 Agent | カテゴリ | 対象箇所 | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|----------|
| H1 | High | security-reviewer | サービス間認証 | §4 アーキテクチャ設計 | サービス間通信（gRPC / HTTP）の認証が mTLS「Phase 2」と先送りされているが、Phase 1 でのサービス間認証メカニズム（JWT 伝搬、API キー等）が未定義。内部ネットワークへの侵入時に全サービスが無認証でアクセス可能になるリスク | Phase 1 で最低限サービス間 JWT トークン伝搬または共有 API キー認証を実装し、spec.md に明記する |
| H2 | High | security-reviewer | レート制限 | §4.3 API Gateway 設計 | レート制限のアルゴリズム（Fixed Window / Sliding Window / Token Bucket）と具体的な閾値（例: 100 req/min/user）が未定義。`AddRateLimiter` の記載のみで、DDoS 耐性の実効性が不明 | レート制限アルゴリズムの選定、エンドポイント別・ユーザー別の閾値テーブル、429 レスポンス時の `Retry-After` ヘッダー設計を追加する |
| H3 | High | security-reviewer | CORS 設定 | §インフラ YAML 設定 | CORS の `allowedHeaders: ["*"]` はセキュリティヘッダーインジェクションのリスクがある。許可ヘッダーをホワイトリストで明示すべき | `allowedHeaders` を `["Content-Type", "Authorization", "X-Correlation-Id", "Accept-Language"]` 等の必要なヘッダーに限定する |
| H4 | High | architect | Saga デッドライン | §4.3.2 Saga オーケストレーション | Saga 全体の SLO デッドライン 1,000ms が定義されているが、各ステップの個別タイムアウト配分が未定義。9 ステップ中 1 ステップの遅延がデッドライン超過を引き起こした場合のステップ別タイムアウトとエスカレーション設計がない | 各 Saga ステップの個別タイムアウト配分（例: 在庫引当 200ms、クーポン検証 100ms、決済認証 400ms 等）を定義し、ステップ別のフォールバック戦略を明記する |
| H5 | High | architect | サービスディスカバリ | §4 アーキテクチャ・§インフラ | ローカル環境は .NET Aspire の `WithReference` でサービスディスカバリが解決されるが、本番環境（Azure Container Apps）でのサービスディスカバリメカニズム（Azure Container Apps 内部 DNS、環境変数注入等）の具体的な設計が不足 | 本番 Azure Container Apps 環境でのサービス解決方法（内部 DNS `<app-name>.internal.<env>.azurecontainerapps.dev`、または Dapr サービスディスカバリ）を明記する |
| H6 | High | architect | gRPC Proto 管理 | §4.3.2 Saga gRPC 定義 | gRPC の `.proto` ファイル定義が spec.md 内にインラインで記載されているが、共有プロジェクト `SkiShop.Contracts/Protos/` の構成・バージョン管理戦略が未定義。`.proto` の後方互換性検証（`buf breaking`）は テスト規約セクションで言及されているが、Proto 管理の全体像がない | `SkiShop.Contracts` 共有プロジェクトの構成（ディレクトリ構造、NuGet パッケージ化の可否、バージョニング戦略）を spec.md に追加する |
| H7 | High | business-analyst | ゲスト購入フロー | §4 支払い・カートサービス | ゲスト購入時の注文確認メール送信先（`guest_email`）のバリデーションフローが曖昧。メールアドレスの検証（確認メール送信）なしに注文が確定されるリスクがある。特にフィッシング目的での第三者メールアドレス入力への対策が不足 | ゲスト購入時のメールアドレス確認フロー（確認コード送信、またはメールアドレス 2 回入力による一致検証）を設計に追加する |
| H8 | High | business-analyst | 在庫引当解放 | §4.3.2 Saga 補償 | Saga 補償トランザクションでの在庫引当解放のタイミングが「即時」と記載されているが、決済処理中にユーザーがブラウザを閉じた場合（Webhook 未着パターン）の在庫引当タイムアウト解放戦略が不足。引当されたまま放置される在庫のリスク | 在庫引当の有効期限（例: 15 分）を定義し、`InventoryReservationCleanupService` (BackgroundService) による期限切れ引当の自動解放設計を追加する |
| H9 | High | dba-reviewer | Advisory Lock 干渉 | §データモデル・Outbox | `pg_advisory_xact_lock` のロック ID 設計が不明確。複数の BackgroundService（OutboxPublisher、SagaRecoveryService、PointExpiryService 等）が同一 DB 内で Advisory Lock を使用する場合のロック ID 名前空間の衝突リスクがある | サービスごとの Advisory Lock ID の命名規約（例: OutboxPublisher = hash('outbox_publisher')、SagaRecovery = hash('saga_recovery')）を定義し、衝突回避を明記する |
| H10 | High | dba-reviewer | インデックス設計 | §データモデル | `outbox_events` テーブルの `status + created_at` 複合インデックスは定義されているが、`saga_logs` テーブルの `status + started_at` へのインデックス、及び `orders` テーブルの `user_id + status` 複合インデックスが未定義。頻繁なクエリパターンに対するインデックス不足 | `saga_logs(status, started_at)`、`orders(user_id, status)`、`security_logs(event_type, created_at)` 等の高頻度クエリ向けインデックスを追加定義する |
| H11 | High | tech-lead | Aspire ↔ 本番の環境差異 | §インフラストラクチャ設計 | ローカル（.NET Aspire: 単一 PostgreSQL / 単一 Redis / 単一 Kafka）と本番（Azure: 独立 DB / Redis Cluster / Azure Event Hubs）の環境差異が大きいが、この差異をテストレベルで吸収する戦略が不十分。特に Redis Cluster と単一インスタンスのコマンド互換性（`SCAN` vs `KEYS`、multi-key 操作の制限等）への対策が未記載 | ステージング環境を本番同等構成にする旨は記載があるが、Redis Cluster 固有の制約（ハッシュタグ `{}`、CROSSSLOT エラー）への設計対応を spec.md に追記する |
| H12 | High | tech-lead | エラーバジェット消費時の対応 | §非機能要件 SLO | エラーバジェットの計算とバーンレートアラートは詳細に定義されているが、エラーバジェットが枯渇した場合の具体的な対応ポリシー（例: 新機能リリースの凍結、信頼性改善のみにリソースを集中等）が未定義 | Google SRE プラクティスに従い、エラーバジェット枯渇時のポリシー（リリース凍結基準、バジェット回復計画テンプレート等）を追加する |
| H13 | High | performance-reviewer | PgBouncer フェイルオーバー | §コネクションプール設計 | PgBouncer を Sidecar として配置する設計だが、PgBouncer 自体の障害時のフォールバック（直接接続への切替）が未定義。Sidecar 障害 = サービス障害となる | PgBouncer のヘルスチェック失敗時のフォールバック戦略（直接 PostgreSQL 接続への一時切替、またはコンテナ再起動によるリカバリ）を定義する |
| H14 | High | performance-reviewer | Kafka パーティション拡張 | §Kafka トピック設計 | `order.created` のパーティション数 12 は初期設計として適切だが、パーティション追加時の Consumer リバランス影響（一時的な処理停止）と、パーティション数変更手順が未定義 | Kafka パーティション数の拡張手順（Consumer の一時停止→パーティション追加→Consumer 再起動）と、拡張判断基準（パーティションあたりの Consumer Lag が閾値を超えた場合）を明記する |
| H15 | High | compliance-reviewer | GDPR DPO 任命 | §GDPR DPO セクション | DPO 任命の要否判断が「法務チームの最終判断」に委ねられているが、AI レコメンデーション機能（行動分析・プロファイリング）を提供する EC サイトは Art.37(1)(b) の「大規模な定期的・体系的監視」に該当する可能性が高い。判断を先送りした場合、GDPR 違反のリスク | DPO 任命を Phase 1 の前提条件として設計書に明記するか、法務チームによる判断期限（Phase 1 開始前）を設定する |
| H16 | High | compliance-reviewer | 電子帳簿保存法 | §セキュリティ要件 | PCI DSS・GDPR・個人情報保護法・特定商取引法は言及されているが、**電子帳簿保存法**（令和 4 年改正、令和 6 年 1 月完全義務化）への対応が欠如。EC サイトの電子取引（注文確認メール、領収書等）は電子帳簿保存法の対象 | 電子取引データの保存要件（タイムスタンプ付与、検索要件、訂正・削除履歴の保持）を spec.md に追加し、対応する ADR を起票する |
| H17 | High | qa-manager | Chaos Engineering | §テスト戦略 | テスト戦略に単体テスト・統合テスト・E2E・負荷テスト・セキュリティテスト・コントラクトテストが網羅されているが、マイクロサービスアーキテクチャで重要な **Chaos Engineering**（障害注入テスト）の計画が未定義。サーキットブレーカーの動作検証やフォールバック戦略の実効性テストが不足 | Chaos Engineering の計画（Azure Chaos Studio の活用、障害注入シナリオ定義、実行頻度）をテスト戦略セクションに追加する |
| H18 | High | release-manager | ホットフィックス承認 | §CI/CD パイプライン | ホットフィックスデプロイの短縮パイプライン（統合テスト・負荷テストスキップ）が定義されているが、承認が「Tech Lead or SRE Lead 1 名」のみ。通常デプロイの「最低 2 名」と比較してガバナンスが弱い。特に決済サービスのホットフィックスに対して 1 名承認は不十分 | ホットフィックスでも決済関連サービス（PaymentCartService）は最低 2 名承認を維持するか、ホットフィックス後 24 時間以内の事後承認プロセスを追加する |
| H19 | High | infra-ops-reviewer | マルチリージョン DR 切替 | §ディザスタリカバリ | DR フェイルオーバーの発動条件テーブルは定義されているが、「SRE Lead（手動判断）」による切替の具体的な判断チェックリスト・通信手順が不足。深夜帯のインシデント発生時にオンコール担当者が 1 時間以内に RTO を達成するための Runbook が必要 | DR フェイルオーバー Runbook（チェックリスト形式）を spec.md に追加するか、別文書（`runbooks/dr-failover.md`）への参照を明記する |
| H20 | High | audit-reviewer | ADR 実体化 | §ADR セクション | ADR-0001〜ADR-0010 の概要は spec.md に記載されているが、「Phase 1 基盤構築後に ADR ファイルを実体化」とされている。ADR のテンプレート（MADR v3）は定義済みだが、ADR ファイルの作成期限が明確でない。設計判断の追跡不能期間が発生する | ADR ファイルの実体化期限を Phase 1 の完了条件（`dotnet build` 成功と同時に全 ADR ファイルが `design-docs/adrs/` に存在すること）として明記する。ただし既に `design-docs/adrs/` に 10 件の ADR ファイルが存在するため、内容の充足度を確認する |
| H21 | High | ux-accessibility-reviewer | エラーメッセージ i18n | §フロントエンド・アクセシビリティ | バックエンドの RFC 9457 Problem Details 形式のエラーレスポンスが定義されているが、フロントエンドでのエラーメッセージのローカライズ戦略（日英対応）が不明確。`detail` フィールドの日本語メッセージをそのまま表示するのか、翻訳キーに変換するのかが未定義 | バックエンドエラーレスポンスの `type` フィールドをフロントエンドの翻訳キーにマッピングする戦略を定義する。`detail` フィールドは開発者向け、ユーザー表示はフロントエンドで i18n 変換 |
| H22 | High | architect | Outbox Polling 間隔 | §10.4 CheckoutService / Outbox | AGENTS.md §10.4 で「Outbox Polling は動的バックオフ（100ms〜5s）。固定間隔 1 秒は禁止」と明記されているが、spec.md 内の OutboxPublisher 設計セクションにこの動的バックオフの具体的な実装設計（バックオフアルゴリズム、初期値、最大値、リセット条件）が記載されていない | spec.md に OutboxPublisher の動的バックオフ設計（指数バックオフ: 100ms → 200ms → 400ms → ... → 5s、新規イベント検出時にリセット）を追加する |

## エスカレーション事項（要人間判断）
| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E1 | 最優先 | compliance-reviewer | DPO 任命の要否判断（GDPR Art.37(1)(b) への該当性評価） | 法務チーム + DPO 候補者 |
| E2 | 最優先 | compliance-reviewer | 電子帳簿保存法への対応方針決定 | 法務チーム + 経理部門 |
| E3 | 高優先 | security-reviewer | Phase 1 でのサービス間認証方式の選定（JWT 伝搬 vs 共有 API キー vs mTLS 前倒し） | Tech Lead + Security Lead |
| E4 | 高優先 | architect | Azure Event Hubs for Apache Kafka vs Confluent Cloud の最終選定 | Tech Lead + インフラチーム |
| E5 | 高優先 | performance-reviewer | フラッシュセール時の在庫引当ロックフリー設計（Redis DECR 方式）の Phase 2 導入可否 | Tech Lead + SRE Lead |
| E6 | 通常 | business-analyst | Phase 1 クレジットカードのみでの CVR 2.5% 達成可否の市場データ検証 | プロダクトオーナー |
| E7 | 通常 | oss-reviewer | Confluent Schema Registry の Phase 2 導入工数・コスト評価 | インフラチーム |

## 競合解決記録
| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| 1 | performance-reviewer (キャッシュ TTL 延長推奨) | architect (データ整合性重視で短 TTL 推奨) | 在庫キャッシュ `product:{id}:stock` の TTL 1 分は短すぎる vs 在庫の正確性が EC の信頼に直結 | architect の判断を支持（TTL 1 分を維持） | 在庫データは金銭的影響に直結するため整合性を優先。TTL 延長はキャッシュスタンピード対策（PER パターン）で補完する設計が spec.md に記載済み |
| 2 | security-reviewer (mTLS Phase 1 必須) | release-manager (Phase 1 スコープ過大回避) | mTLS を Phase 1 に前倒すべき vs Phase 1 のリリーススケジュールに影響 | エスカレーション E3 として人間の判断に委ねる | mTLS の工数と Phase 1 スケジュールのバランスは、プロジェクト全体のリスク許容度に基づく経営判断が必要 |

## ドキュメント横断分析

### マイクロサービス設計書カバレッジ
| サービス | 設計書 | 存在 | API 定義 | DB 設計 | イベント定義 | セキュリティ | 非機能要件 |
|---------|--------|------|---------|--------|------------|------------|-----------|
| ApiGateway | api-gateway-design.md | ✅ | ✅ | N/A | ✅ | ✅ | ✅ |
| AuthService | authentication-service-design.md | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| UserManagementService | user-management-design.md | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| InventoryManagementService | inventory-management-design.md | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| SalesManagementService | sales-management-design.md | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| PaymentCartService | payment-cart-service-design.md | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| CouponService | coupon-service-design.md | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| PointService | point-service-design.md | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| MailSendService | mailsend-service-design.md | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| AiSupportService | ai-support-service-design.md | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| AppHost | spec.md (§インフラ) | ✅ | N/A | N/A | N/A | N/A | N/A |

### サービス間整合性
- Kafka トピック設計で `order.created` の購読先に MailSendService が含まれているが、PointService も購読先として記載。Saga ステップとイベント駆動の境界が一部曖昧（Saga ステップ 9 でポイント付与は同期 gRPC、イベント駆動の `order.created` 購読は別用途と推定されるが明示されていない）
- AppHost の `WithReference` 設定と Kafka トピックの発行元/購読先が整合している
- gRPC 通信先（SalesManagementService → InventoryManagementService / PaymentCartService / CouponService / PointService）が AppHost の `WithReference` と一致
- AuthService が `user.registered` を発行し UserManagementService が購読する構成は `propagateEvent-from-authservice-2-usermanageservice.md` と整合

### 記載カバレッジ分析
- **充実している領域**: GDPR/個人情報保護（非常に詳細）、Saga オーケストレーション、PCI DSS 非保持化、データモデル設計、テスト戦略、CI/CD パイプライン、可用性・DR 設計
- **不足している領域**: サービス間認証（Phase 1）、レート制限の具体的閾値、電子帳簿保存法対応、Chaos Engineering、Outbox 動的バックオフ設計

### 未定義・曖昧な領域
1. Phase 1 でのサービス間認証メカニズム（mTLS は Phase 2 先送り）
2. レート制限アルゴリズムとエンドポイント別閾値
3. Saga 各ステップの個別タイムアウト配分
4. 在庫引当の有効期限とタイムアウト解放戦略
5. Advisory Lock の ID 名前空間管理
6. Redis Cluster 固有の制約への設計対応
7. エラーバジェット枯渇時の組織的対応ポリシー
8. 電子帳簿保存法への対応方針
9. DR フェイルオーバーの具体的な Runbook
10. フロントエンドエラーメッセージの i18n 戦略

## Medium/Low 指摘一覧

### Medium 指摘
| # | 出典 Agent | カテゴリ | 対象箇所 | 指摘内容 | 推奨対応 |
|---|-----------|---------|----------|----------|----------|
| M1 | business-analyst | 返品・交換 | §ビジネスルール | 返品理由分類コードは定義されているが、返品率 KPI の計測・レポーティング手段（ダッシュボード、定期レポート等）が未定義 | 返品率 KPI の計測手段と責任者を定義する |
| M2 | business-analyst | 会員ランク | §ビジネスルール | 会員ランク降格の猶予（83% 閾値）は定義されているが、新規会員のランク判定タイミング（登録直後 / 初回購入後 / 月次バッチ）が不明確 | 新規会員の初期ランク（Bronze）と初回ランク評価タイミングを明記する |
| M3 | business-analyst | 商品比較 | §ビジネスルール H8-25 | 商品比較機能が Phase 2 に先送りだが、比較軸（価格、サイズ等）のデータが InventoryManagementService のエンティティ定義に含まれているか未確認。Phase 2 実装時にスキーマ変更が必要になる可能性 | 比較軸に必要なカラム（weight, ski_level, material 等）が product テーブルに存在するか確認し、不足があれば Phase 1 の Expand マイグレーションで追加する |
| M4 | architect | 共有ライブラリ | §アーキテクチャ全般 | `SkiShop.Contracts` と `SkiShop.SharedKernel` が CI の変更検知フィルタに含まれているが、これらの共有プロジェクトの責務分担が未定義 | `SkiShop.Contracts`（Kafka イベント record、gRPC Proto、共有 DTO）と `SkiShop.SharedKernel`（例外クラス、Value Object 基底型、共通ユーティリティ）の責務を明記する |
| M5 | architect | Bounded Context マップ | §DDD 設計 | 各マイクロサービスの Bounded Context は暗黙的に定義されているが、Context Map（上流/下流関係、Conformist / Anti-Corruption Layer / Shared Kernel 等のパターン区分）が明示されていない | Context Map を Mermaid 図で追加し、サービス間の関係パターン（例: SalesManagementService は InventoryManagementService に対して Conformist）を明示する |
| M6 | architect | カート expiry | §4.6 カート管理 | カートデータの Redis TTL は 72 時間、`CartCleanupService` は 30 日以上未更新のカートを削除と記載。Redis TTL（72 時間）と DB 保持（30 日）の整合性設計が曖昧 | Redis キャッシュ TTL 切れ後に DB から復元するフロー、または Redis TTL と DB クリーンアップの関係を明確化する |
| M7 | architect | テナント分離 | §データモデル | サービス別独立 DB（ADR-0006）は正しいが、同一 PostgreSQL インスタンス上の論理 DB 分離（ローカル開発）と、Azure Database for PostgreSQL の物理インスタンス分離（本番）の境界が不明確 | ローカル（論理 DB 分離）、ステージング（物理インスタンス分離 or 論理分離）、本番（物理インスタンス分離）の環境別 DB トポロジーを表形式で明記する |
| M8 | programing-reviewer | コード例の不整合 | §リスク管理 Saga 例 | Saga 補償トランザクション例で `orderService.CancelOrderAsync` を呼んでいるが、`CreateOrderAsync` 内で `orderService.FinalizeOrderAsync` が呼ばれる前の段階では Order が未確定のため、`CancelOrderAsync` の対象が不明確 | Saga コード例の補償ステップを正確な実行順序に修正する（Finalize 前の段階ではキャンセルではなく注文レコード削除が妥当） |
| M9 | programing-reviewer | メソッド呼び出し構文 | §複数コード例 | Serilog 設定例で `.ReadFrom.Configuration(context.Configuration)` の後が `.Enrich.FromLogContext` のように括弧 `()` が省略されている箇所がある（C# ではメソッドグループは呼び出しにならない） | コード例の構文を修正する（`.FromLogContext()` に括弧を追加） |
| M10 | programing-reviewer | FeatureManagement コード例 | §フィーチャーフラグ | `IsEnabledAsync` の呼び出しで `CancellationToken` を渡しているが、`Microsoft.FeatureManagement` の `IsEnabledAsync` は CancellationToken パラメータを受け付けない（v3 時点） | コード例を修正するか、CancellationToken 対応の有無を確認する |
| M11 | security-reviewer | JWT 署名鍵管理 | §認証設計 | JWT 署名アルゴリズムは RS256 と定義されているが、署名鍵（RSA 秘密鍵）のローテーション戦略（ローテーション周期、旧鍵での検証期間、Key Vault での管理方法）が未定義 | JWT 署名鍵のローテーション設計（90 日周期、JWKS エンドポイントでの公開鍵配信、旧鍵は TTL 期間中検証可能）を追加する |
| M12 | security-reviewer | ゲスト DSR 本人確認 | §GDPR ゲスト DSR | ゲスト DSR の本人確認が「注文番号 + メールアドレスの一致検証」のみ。注文番号は推測可能な連番の場合、メールアドレスと組み合わせたブルートフォース攻撃のリスクがある | 注文番号を UUID 形式にするか、DSR フォームにレート制限 + CAPTCHA を適用する |
| M13 | dba-reviewer | パーティショニング | §データモデル | `orders` テーブルは高成長（日間 5,000 件）が予測されるが、PostgreSQL テーブルパーティショニング戦略（RANGE パーティション by `created_at` 等）が未定義。年間 182 万件の蓄積でクエリ性能が劣化するリスク | `orders` テーブルの月次 RANGE パーティショニング設計と、コールドデータのアーカイブ戦略を追加する |
| M14 | dba-reviewer | 外部キー CASCADE | §データモデル | 一部の外部キー制約で `ON DELETE CASCADE` が設定されているが、マイクロサービス間（DB 別）のデータ削除整合性は Kafka イベントで実現するため、サービス内のテーブル間 CASCADE の影響範囲を明示すべき | 各テーブルの FK 制約における CASCADE / RESTRICT / SET NULL の選択基準と、サービス間の削除整合性（Kafka `user.deleted` イベント）との関係を明記する |
| M15 | dba-reviewer | JSONB カラム | §データモデル | `size_guides.size_chart` (JSONB)、`products.specifications` (JSONB) 等の JSONB カラムが定義されているが、JSONB 内のスキーマ定義（必須キー、型制約）と GIN インデックスの必要性が未検討 | JSONB カラムに対する CHECK 制約（`jsonb_typeof` による型検証）または アプリケーション層でのバリデーション要件を明記する |
| M16 | qa-manager | 負荷テスト DB サイズ | §負荷テスト計画 | 負荷テスト用シードデータ（商品 10,000 件、ユーザー 100,000 件、注文 500,000 件）は定義されているが、本番想定のデータ量（10 万 SKU、日間 5,000 件注文の 1 年分 = 182 万件）との乖離が大きい | 負荷テストのシードデータ量を本番想定の 50% 以上に引き上げるか、データ量の差異によるテスト結果への影響を注記する |
| M17 | qa-manager | モック vs 実環境 | §テスト戦略 | E2E テストシナリオ E1〜E7 で Stripe をテストモードで使用するが、Webhook の到達保証やタイムアウト動作が実環境と異なる可能性。テストモードの制約事項が未記載 | Stripe テストモードの既知の制約（Webhook 発火タイミングの差異等）をテスト計画に注記し、対策を明記する |
| M18 | performance-reviewer | Redis ホットキー | §キャッシュ設計 | 人気商品のキャッシュキー（`product:{id}` や `product:{id}:stock`）がホットキーとなり、Redis Cluster の特定シャードに負荷が集中するリスクがある | ホットキー対策（Redis Cluster のハッシュタグ制御、またはアプリケーション層でのキーサフィックスによるシャード分散）を記載する |
| M19 | performance-reviewer | CDN キャッシュ戦略 | §インフラ・非機能 | Azure Front Door + CDN の利用は記載されているが、静的コンテンツ（商品画像、CSS/JS）のキャッシュ TTL・パージ戦略・ Cache-Control ヘッダー設計が未定義 | 静的コンテンツのキャッシュ戦略（画像: 1 年 + ハッシュ付きファイル名、HTML: no-cache 等）を定義する |
| M20 | compliance-reviewer | データ最小化 | §GDPR Art.25 DPbD | 「注文サービスがメールアドレスを保持しない」と記載されているが、`orders` テーブルに `guest_email` カラムが存在する。ゲスト購入時にはメールアドレスを保持する必要があり、DPbD 原則との整合性を整理すべき | ゲスト購入時の `guest_email` はデータ最小化の例外であることを明記し、保持期間（注文完了後 N 日で NULL 化）を定義する |
| M21 | compliance-reviewer | 同意管理 UI | §Consent Management | 同意バナー・プライバシー設定画面の設計は記載されているが、同意取得の「粒度」（サービス単位 vs 処理目的単位）の UI 設計が不明確。GDPR はデータ主体に処理目的ごとの個別選択権を要求 | 同意管理 UI のワイヤーフレームまたは画面設計（処理目的ごとの ON/OFF トグル）をフロントエンド要件に追加する |
| M22 | compliance-reviewer | 子供のデータ保護 | §GDPR Art.8 | Phase 1 では自己申告（チェックボックス）のみの年齢確認だが、年齢詐称のリスクへの対策が不足。GDPR Art.8 は「合理的な努力」による年齢確認を要求 | Phase 1 の年齢確認方法の限界を acknowledge し、Phase 2 での改善計画（技術的年齢推定、親権者確認メールフロー等）のロードマップを明記する |
| M23 | oss-reviewer | NuGet 禁止パッケージ | §全般 | spec.md のコード例で `Newtonsoft.Json` は使用されていないが、`System.Text.Json` の `JsonUnmappedMemberHandling` 等の .NET 8+ 機能の活用可否について言及がない | Kafka イベントのデシリアライゼーションで使用する `System.Text.Json` の設定（`JsonUnmappedMemberHandling.Skip` 等）を明記する |
| M24 | oss-reviewer | Azure SDK バージョン | §インフラ | Azure Container Apps Deploy Action (`azure/container-apps-deploy-action@v2`) 等の GitHub Actions のバージョンは固定されているが、Azure CLI (`azure/login@v2`) のバージョンピンニング方針が未定義 | GitHub Actions のアクションバージョン管理方針（メジャーバージョン固定 vs SHA 固定）を CI/CD セクションに追加する |
| M25 | release-manager | リリースノート | §運用プロセス | 「リリースノート自動生成」の記載があるが、ツール（`release-please`、GitHub Releases の自動生成）と生成対象（Conventional Commits のパース範囲、Changelog 形式）が未定義 | `release-please` の設定方針（`release-type: simple`、`changelog-sections` の定義）を追加する |
| M26 | infra-ops-reviewer | ログインジェスト量 | §Log Analytics 設計 | 日次 ~9.1 GB のインジェスト量見積もりは提供されているが、ピーク時（ハイシーズン 12-1 月）のインジェスト量予測が未考慮。トラフィック 5 倍で ~45 GB/日となり、コストに大幅な影響 | 季節性トラフィックに応じたログインジェスト量の変動予測と、ピーク時のコスト見積もりを追加する |
| M27 | infra-ops-reviewer | Terraform / IaC | §インフラ全般 | Azure インフラの構成が YAML/CLI ベースで記載されているが、Infrastructure as Code（Terraform / Bicep）の管理戦略が未定義 | IaC ツールの選定（Azure Bicep 推奨 + Terraform オプション）と管理方針を追加する |
| M28 | audit-reviewer | 変更管理プロセス | §運用プロセス | 変更管理プロセスの記載が「リスク評価、変更承認プロセス、変更適用とモニタリング」と概要レベル。変更カテゴリ（Standard / Normal / Emergency）の分類と各カテゴリの承認フローが未定義 | ITIL ベースの変更カテゴリ定義と、カテゴリ別の承認フロー・リードタイムを追加する |
| M29 | audit-reviewer | ポストモーテム | §運用プロセス | ポストモーテムの実施は言及されているが、テンプレート（タイムライン、影響範囲、根本原因、是正措置、再発防止策）と公開ポリシー（社内のみ / 全社公開 / 顧客向け公開）が未定義 | ポストモーテムテンプレートと公開ポリシーを定義する |
| M30 | ux-accessibility-reviewer | a11y テスト自動化 | §テスト戦略 | E2E テストで Playwright を使用するが、アクセシビリティ自動テスト（axe-core / Lighthouse CI）の統合が未記載 | Playwright テストに `@axe-core/playwright` を統合し、WCAG 2.1 Level AA の自動検証を CI に追加する |
| M31 | ux-accessibility-reviewer | カラーコントラスト | §フロントエンド要件 | WCAG 2.1 への準拠は言及されているが、カラーコントラスト比（Level AA: 4.5:1 以上）の具体的なデザイントークン・カラーパレット定義が不足 | デザインシステムのカラーパレット定義と、コントラスト比の検証手段（Lighthouse CI / Storybook a11y addon）を追加する |

### Low 指摘
| # | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|-----------|---------|----------|----------|
| L1 | business-analyst | 用語統一 | 「チェックアウト」と「注文確定」が文脈により混在。Ubiquitous Language として統一すべき | 用語集（Glossary）を spec.md 冒頭に追加する |
| L2 | architect | 図の凡例 | Mermaid 図の接続線の意味（実線=同期、破線=非同期）が図ごとに暗黙的。凡例があるとよい | 主要な Mermaid 図に凡例（Legend）を追加する |
| L3 | architect | ADR 番号の連続性 | ADR-0011 が「子供の個人データ保護方針」として spec.md 内で言及されているが、同時に「OpenTelemetry Exporter として Azure Monitor を選定」も ADR-0011 として提案されており、番号が衝突 | ADR 番号の重複を解消する（子供のデータ保護は ADR-0014 等に振り替え） |
| L4 | programing-reviewer | コード例の一貫性 | AppHost コード例で `.WithDataVolume` `.WithPgAdmin` `.WithRedisInsight` 等のメソッド呼び出しに括弧 `()` が省略されている箇所がある | コード例の構文統一（括弧の付与） |
| L5 | programing-reviewer | 例外クラス定義箇所 | `NotFoundException`, `BusinessException` 等のカスタム例外クラスが参照されているが、これらの定義箇所（`SkiShop.SharedKernel` 等）が明示されていない | 共通例外クラスの定義先プロジェクトを明記する |
| L6 | security-reviewer | TLS バージョン | TLS 1.3 の使用が記載されているが、TLS 1.2 のサポート有無（後方互換性）が不明確 | TLS 最低バージョン（TLS 1.2 以上）の方針を明記する |
| L7 | dba-reviewer | テーブル名・カラム名 | 大部分のテーブル名・カラム名は snake_case で統一されているが、一部のコード例で PascalCase のプロパティ名からの自動マッピングが暗黙的に行われている | EF Core の命名規約（`[Table]`, `[Column]` 属性による明示的 snake_case 指定）の必須化を再確認する |
| L8 | qa-manager | テスト命名 | spec.md 内のテスト例は `Should_X_When_Y` パターンに準拠しているが、BackgroundService テスト戦略テーブルのテストメソッド名例が未記載 | BackgroundService テストにも命名規約を適用した例を記載する |
| L9 | performance-reviewer | Azure Functions | §インフラ設計で「Azure Functions: イベント駆動型処理、バッチ処理、スケジュールタスク」と記載されているが、Azure Functions の具体的な用途が他セクションで定義されていない | Azure Functions の具体的な利用計画を追加するか、記載を削除する |
| L10 | oss-reviewer | ライセンス一覧 | 使用パッケージのライセンス互換性マトリクスが未記載 | 主要 NuGet パッケージのライセンス一覧（MIT, Apache 2.0 等）を追加する |
| L11 | release-manager | 通知チャンネル | デプロイ通知の Slack チャンネル名が `#sre-alerts`, `#dev-alerts`, `#security-incidents` と定義されているが、デプロイ成功/失敗の通知先チャンネルが未定義 | デプロイ通知チャンネル（例: `#deployments`）を定義する |
| L12 | infra-ops-reviewer | ステージング環境コスト | ステージング環境は「本番同等構成（小規模）」と記載されているが、具体的な SKU・レプリカ数の記載がない | ステージング環境の具体的な SKU・レプリカ数を定義する |
| L13 | audit-reviewer | 承認記録 | ADR の承認プロセス（PR レビュー 最低 2 名）は定義されているが、承認者の記録方法（PR の Approve ログで代替 vs ADR ファイル内に記載）が不明確 | ADR テンプレートに承認者セクションを追加する |
| L14 | ux-accessibility-reviewer | フォーカス管理 | チェックアウトフローのステップ間遷移時のフォーカス管理方針（ステップ遷移後にステップタイトルにフォーカス移動等）が未記載 | SPA のルート遷移時のフォーカス管理ポリシーを追加する |
| L15 | ux-accessibility-reviewer | 画像 alt テキスト | 商品画像の代替テキスト（alt 属性）の生成方針（商品名 + カテゴリの自動生成 vs 手動入力）が未定義 | 商品画像の alt テキスト生成ルールを定義する |

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### business-analyst レビュー
**観点**: ビジネス要件の完全性、ユーザーストーリー、受入基準

**評価**: ⚠️ Conditional — EC サイトとしての基本的なビジネスフローは網羅的に設計されているが、エッジケース（ゲスト購入のメールバリデーション、在庫引当タイムアウト）への対応が不足。

**指摘件数**: Critical: 0 / High: 2 / Medium: 3 / Low: 1

**詳細**:
- ビジネスフロー（商品閲覧→カート→チェックアウト→決済→注文確認）は明確に定義されている
- クーポン・ポイントの適用順序（H8-23）は詳細に定義されており、制約ルールも明確
- 会員ランク制度（Bronze/Silver/Gold/Platinum）の設計は充実しているが、新規会員の初期評価タイミングが不明確（M2）
- 返品・交換フローは Phase 1/Phase 2 で段階的に対応する方針が示されているが、KPI 計測手段が未定義（M1）
- ゲスト購入フローにおけるメールアドレス検証の不足は、フィッシング・なりすましリスクに直結する（H7）
- 特定商取引法の法定表示事項は網羅的に定義されている
- 決済手段ロードマップ（Phase 1: クレジットカードのみ → Phase 2: コンビニ・銀行振込）は明確だが、CVR への影響は E6 としてエスカレーション

</details>

<details>
<summary>architect レビューレポート</summary>

### architect レビュー
**観点**: マイクロサービス分割、Bounded Context、DDD パターン、.NET Aspire 構成

**評価**: ⚠️ Conditional — マイクロサービスアーキテクチャの全体設計は成熟しているが、Saga の実行時詳細設計と共有ライブラリの責務定義に不足。

**指摘件数**: Critical: 0 / High: 3 / Medium: 4 / Low: 2

**詳細**:
- 11 サービスの Bounded Context 分割は DDD 原則に適合している
- Saga オーケストレーション（9 ステップ）の設計は ADR-0009 と整合しているが、各ステップの個別タイムアウト配分が欠如（H4）
- Outbox パターンの設計は ADR-0005 と整合しているが、AGENTS.md §10.4 の「動的バックオフ」要件が spec.md に反映されていない（H22）
- AppHost の `WithReference` 設定は Kafka トピック設計と整合している
- 本番環境でのサービスディスカバリメカニズムが不明確（H5）
- gRPC Proto の共有プロジェクト管理が未定義（H6）
- Context Map（上流/下流関係）の明示的な図が不足（M5）

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### tech-lead レビュー
**観点**: 技術標準の横断適合性、実装実現可能性、AGENTS.md / Instructions との整合性

**評価**: ⚠️ Conditional — AGENTS.md および Instructions ファイルとの整合性は概ね良好だが、ローカル↔本番の環境差異への対策と SLO 運用ポリシーに不足。

**指摘件数**: Critical: 0 / High: 2 / Medium: 3 / Low: 1

**詳細**:
- spec.md のコード例は AGENTS.md の禁止事項（Console.WriteLine、FromSqlRaw 文字列結合、プロパティインジェクション等）に抵触していない
- primary constructor、record 型、CancellationToken の必須化は AGENTS.md §4 と整合
- EF Core エンティティの設計（`[Table]`, `[Column]`, `DateTime.UtcNow`, コレクション `= []` 初期化）は AGENTS.md §10.3 と整合
- Serilog のメッセージテンプレート形式ログは AGENTS.md §11.2 と整合
- ミドルウェアパイプライン順序は AGENTS.md §11.3 と整合
- ただし、Redis Cluster 固有の制約（CROSSSLOT エラー、multi-key 操作の制限）への設計対応が不足（H11）
- エラーバジェット枯渇時の組織的対応ポリシーが未定義（H12）

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### programing-reviewer レビュー
**観点**: コード例の正確性、C# 14 / .NET 10 機能活用、禁止パターンの検出

**評価**: ✅ Pass — コード例は概ね正確で、C# 14 の機能を適切に活用している。軽微な構文エラーが散見される。

**指摘件数**: Critical: 0 / High: 1 / Medium: 3 / Low: 2

**詳細**:
- `dotnet-coding-standards.instructions.md` の命名規則（PascalCase/camelCase/_camelCase）に準拠
- DI はコンストラクタインジェクション（primary constructor）で統一されており、プロパティインジェクション禁止ルールに適合
- `async` メソッドに CancellationToken が含まれている（AGENTS.md §4.5 準拠）
- `api-design.instructions.md` の Minimal API パターンに準拠したエンドポイント設計
- 一部のコード例でメソッド呼び出しの括弧 `()` が省略されている（L4, M9, M10）
- Saga 補償のコード例に論理的な不整合がある（M8）

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### security-reviewer レビュー
**観点**: OWASP Top 10、認証/認可設計、秘密情報管理、脅威モデリング

**評価**: ⚠️ Conditional — GDPR/PCI DSS 対応は詳細だが、Phase 1 でのサービス間認証とレート制限の具体的設計が不足。

**指摘件数**: Critical: 0 / High: 3 / Medium: 2 / Low: 1

**詳細**:
- `security-coding.instructions.md` の入力バリデーション要件は spec.md のエンドポイント設計に反映されている
- STRIDE 脅威分析が AuthService に対して実施されている
- PCI DSS 非保持化（SAQ A）の設計は適切。Webhook 署名検証設計も詳細
- IDOR 防止策（ログインユーザー ID とリソースの所有者チェック）が設計に含まれている
- JWT の RS256 署名、ClockSkew 設定は `security-coding.instructions.md` に準拠
- ただし Phase 1 のサービス間認証が未定義（H1）
- レート制限の具体的なアルゴリズム・閾値が不足（H2）
- CORS の `allowedHeaders: ["*"]` はセキュリティリスク（H3）

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### dba-reviewer レビュー
**観点**: DB スキーマ設計、EF Core マッピング、マイグレーション安全性、PostgreSQL 固有の最適化

**評価**: ⚠️ Conditional — データモデル設計は充実しているが、運用面（Advisory Lock 管理、大テーブルのパーティショニング、JSONB バリデーション）に不足。

**指摘件数**: Critical: 0 / High: 2 / Medium: 3 / Low: 1

**詳細**:
- `sql-schema-review.instructions.md` のテーブル命名規則（snake_case 複数形）に準拠
- 監査カラム（`created_at`, `updated_at`）が全テーブルに定義されている
- `DECIMAL` 型の金額カラム、`TIMESTAMP WITH TIME ZONE` の日時カラムは `sql-schema-review.instructions.md` に準拠
- CHECK 制約（例: `price > 0`, `stock_quantity >= 0`）が適切に定義されている
- 楽観的ロック（`[Timestamp]` / `row_version`）が必要なテーブルに定義されている
- コネクションプール設計（per-replica、PgBouncer 必須化）は詳細
- ただし Advisory Lock の ID 管理が不明確（H9）
- 高成長テーブルのパーティショニング戦略が未定義（M13）

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### qa-manager レビュー
**観点**: テスト戦略、カバレッジ目標、テスト分類、受入基準の検証可能性

**評価**: ✅ Pass — テスト戦略は `test-standards.instructions.md` と整合しており、テストピラミッド・カバレッジ目標・命名規約が明確。Chaos Engineering の不足が唯一の High 指摘。

**指摘件数**: Critical: 0 / High: 1 / Medium: 2 / Low: 1

**詳細**:
- テストピラミッド（70% Unit / 20% Integration / 10% E2E）は `test-standards.instructions.md` の推奨に準拠
- カバレッジ目標（分岐カバレッジ 80%）は AGENTS.md §9.4 と整合
- テスト命名規約（`Should_X_When_Y`）は `test-standards.instructions.md` に準拠
- AAA パターンの例示は `test-standards.instructions.md` に準拠
- BackgroundService テスト戦略が詳細に定義されている（OutboxPublisher、SagaRecoveryService 等）
- Saga 補償トランザクションテスト戦略が障害注入ポイントごとに定義されている
- コントラクトテスト（Pact.NET）と Kafka イベントスキーマ契約テストが定義されている
- CI カバレッジ品質ゲートが GitHub Actions で自動化されている
- ただし Chaos Engineering の計画が欠如（H17）

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### performance-reviewer レビュー
**観点**: パフォーマンス SLA、スケーラビリティ、キャッシュ戦略、負荷テスト計画

**評価**: ⚠️ Conditional — パフォーマンス要件と負荷テスト計画は詳細だが、PgBouncer のフォールバックと Kafka パーティション拡張手順に不足。

**指摘件数**: Critical: 0 / High: 2 / Medium: 2 / Low: 1

**詳細**:
- p95/p99 レスポンスタイム SLO が API カテゴリ別に定義されている
- キャッシュスタンピード対策（SET NX EX ロック、Stale-While-Revalidate、PER）が詳細
- DbContextPool の設計と制約事項が明記されている
- コネクションプール設計は per-replica で定義され、PgBouncer の必須化が明記
- 負荷テスト計画（k6/NBomber）は 7 シナリオが定義されている
- Redis メモリ容量見積もりは妥当（~500 MB〜1 GB、Premium P1 6 GB で十分なマージン）
- 季節性トラフィック対応のスケジュールスケーリングが定義されている

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### compliance-reviewer レビュー
**観点**: GDPR、個人情報保護法、PCI DSS、特定商取引法、データガバナンス

**評価**: ⚠️ Conditional — GDPR 対応は非常に詳細で模範的だが、DPO 任命の先送りと電子帳簿保存法の欠如が High 指摘。

**指摘件数**: Critical: 0 / High: 2 / Medium: 3 / Low: 0

**詳細**:
- GDPR Art.15/16/17/18/20/21/25/28/33/34/35/37 への対応が個別に設計されている（極めて詳細）
- DPIA が AiSupportService（Art.35 プロファイリング）と会員ランク自動評価（Art.22）の 2 件で実施されている
- 越境データ移転影響評価（TIA）が Azure / Stripe / SendGrid に対して実施されている
- Art.28 外部プロセッサー管理テーブルが完備
- PCI DSS 非保持化（SAQ A）の設計は適切
- 特定商取引法の法定表示事項が網羅的
- ただし DPO 任命の判断が先送りされている（H15）
- 電子帳簿保存法への対応が欠如（H16）

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### oss-reviewer レビュー
**観点**: NuGet パッケージのライセンス互換性、脆弱性、禁止パッケージの検出

**評価**: ✅ Pass — NuGet パッケージのバージョン管理方針は `nuget-dependency.instructions.md` と整合。禁止パッケージの使用は検出されず。

**指摘件数**: Critical: 0 / High: 0 / Medium: 2 / Low: 1

**詳細**:
- `nuget-dependency.instructions.md` の必須パッケージリストと spec.md の記載が整合
- 禁止パッケージ（`System.Web`, `log4net`, `EntityFramework` (EF6), `WebClient`, `Newtonsoft.Json`）の使用は検出されず
- `-preview` / `-beta` / `-rc` パッケージの使用はなし
- `System.Text.Json` の使用方針（Kafka イベントのシリアライゼーション）は適切
- ライセンス一覧の明示的な管理が不足（L10）

</details>

<details>
<summary>release-manager レビューレポート</summary>

### release-manager レビュー
**観点**: リリース戦略、バージョニング、CI/CD パイプライン、ロールバック計画

**評価**: ✅ Pass — CI/CD パイプラインとデプロイ戦略は詳細。カナリア 4 段階プロセスとロールバック 3 層戦略は成熟。

**指摘件数**: Critical: 0 / High: 1 / Medium: 1 / Low: 1

**詳細**:
- SemVer 2.0.0 に準拠したバージョニング
- カナリアデプロイの 4 段階プロセスは詳細に設定されている
- 3 層ロールバック戦略（アプリケーション / DB / Kafka）が定義されている
- Expand-Contract パターンによるゼロダウンタイムマイグレーションが設計されている
- デプロイ順序（Phase 1〜5）が依存関係に基づいて定義されている
- ただし、ホットフィックスの承認プロセスが決済サービスに対して不十分（H18）

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### infra-ops-reviewer レビュー
**観点**: コンテナ設計（Dockerfile）、Azure Container Apps、可観測性、DR 計画

**評価**: ✅ Pass — `dockerfile-infra.instructions.md` との整合性は良好。DR 戦略の具体的な Runbook が不足。

**指摘件数**: Critical: 0 / High: 1 / Medium: 2 / Low: 1

**詳細**:
- Dockerfile テンプレートは `dockerfile-infra.instructions.md` に完全準拠（マルチステージ、非 root、HEALTHCHECK、バージョン固定）
- HealthCheck.dll のコンソールアプリ設計は、`aspnet` ランタイムに `curl` が含まれない問題を解決している
- Azure Container Apps のスケーリング設定（サービス別 minReplicas/maxReplicas）が定義されている
- ヘルスチェック設計（Liveness / Readiness）が全サービスに定義されている
- グレースフルシャットダウン設計が詳細（ShutdownTimeout 30 秒、Kafka Consumer Close）
- OpenTelemetry + Serilog + Correlation ID の可観測性設計は AGENTS.md §11 と整合
- ただし DR フェイルオーバーの具体的な Runbook が不足（H19）

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### audit-reviewer レビュー
**観点**: トレーサビリティ、ADR 管理、承認プロセス、変更管理

**評価**: ✅ Pass — ADR 管理プロセスと監査ログ設計は定義されているが、ADR の実体化期限と変更管理の詳細が不足。

**指摘件数**: Critical: 0 / High: 1 / Medium: 2 / Low: 1

**詳細**:
- ADR テンプレート（MADR v3）が定義されている
- ADR 運用ルール（レビュー 最低 2 名、番号管理、置換プロセス）が明確
- 監査ログ（AuditLog エンティティ）が定義されている
- SecurityLog の PII ハッシュ化（90 日後）と物理削除（1 年後）が定義されている
- Correlation ID による分散トレーシングが設計されている
- ただし ADR ファイルの実体化期限が不明確（H20）
- 変更管理の詳細カテゴリ分類が不足（M28）

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### ux-accessibility-reviewer レビュー
**観点**: UX 設計品質、WCAG 2.1 準拠、レスポンシブ設計、i18n

**評価**: ✅ Pass — アクセシビリティ設計は充実（スキップリンク、aria 属性、フォーカス管理、スクリーンリーダー対応）。エラーメッセージの i18n 戦略が不足。

**指摘件数**: Critical: 0 / High: 1 / Medium: 2 / Low: 1

**詳細**:
- チェックアウトのステップインジケーター（5 ステップ）のアクセシビリティ設計が詳細（`aria-current="step"` 等）
- 決済ページリダイレクトの UX 設計（確認モーダル、ローディング状態、状態復元フロー）が詳細
- スクリーンリーダー対応（`aria-live` 告知、モーダルの `role="dialog"`）が定義されている
- Core Web Vitals 目標値（LCP ≤ 2.5s, INP ≤ 200ms, CLS ≤ 0.1）が定義されている
- 検索サジェスト UX の `role="combobox"` + `aria-autocomplete="list"` が定義されている
- ただし、バックエンドエラーの i18n 変換戦略が不明確（H21）

</details>
