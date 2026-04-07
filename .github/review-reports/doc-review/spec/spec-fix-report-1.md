# spec.md 修正レポート（Iteration 1）

## 概要
- **対象ファイル**: `design-docs/spec.md`
- **レビューレポート**: `.github/review-reports/doc-review/spec/check-report-1.md`
- **修正日**: 2025-07-17
- **修正前**: Critical=0, High=22, Medium=34, Low=15
- **修正対象**: High 22 件（全件対応）

## 修正サマリー

| # | 重要度 | 出典 Agent | 指摘内容 | 修正内容 | 修正方法 |
|---|--------|-----------|---------|---------|---------|
| H1 | High | security-reviewer | Phase 1 サービス間認証が未定義 | Client Credentials フロー（OAuth 2.0 RFC 6749 §4.4）を Phase 1 認証方式として追加。JWT 伝搬パターン、スコープ検証、フェーズ別ロードマップテーブルを記載 | 追記 |
| H2 | High | security-reviewer | レート制限アルゴリズム・閾値が未定義 | Token Bucket アルゴリズム選定、エンドポイント別閾値テーブル（8 パターン）、Retry-After ヘッダー設計を追加 | 追記 |
| H3 | High | security-reviewer | CORS allowedHeaders: ["*"] がリスク | `["Content-Type", "Authorization", "X-Correlation-Id", "Accept-Language"]` に限定 | 修正 |
| H4 | High | architect | Saga ステップ別タイムアウト配分が未定義 | gRPC Deadline per Saga ステップのタイムアウト配分テーブルを追加 | 追記 |
| H5 | High | architect | 本番サービスディスカバリが未定義 | Azure Container Apps 内部 DNS パターン（`<app-name>.internal.<env>.azurecontainerapps.dev`）を本番環境セクションに追加 | 追記 |
| H6 | High | architect | SkiShop.Contracts 共有プロジェクト構成が未定義 | プロジェクトディレクトリ構成、参照方式（ProjectReference）、バージョニング戦略（package ディレクティブ）、後方互換性（buf breaking）、責務分担テーブルを追加 | 追記 |
| H7 | High | business-analyst | ゲスト購入メールバリデーション不足 | メールアドレス 2 回入力一致検証を必須とするフロー記述を追加 | 追記 |
| H8 | High | business-analyst | 在庫引当タイムアウト解放が未定義 | 3 層防御（Saga Deadline 1s → SagaRecovery 5min → InventoryReservationCleanup 15min）を追加。inventory-management-design.md への参照を追記 | 追記 |
| H9 | High | dba-reviewer | Advisory Lock ID 干渉リスク | Advisory Lock ID 命名規約テーブル（4 サービス、hashtext() 使用）を追加 | 追記 |
| H10 | High | dba-reviewer | saga_logs/orders のインデックス不足 | `idx_orders_user_id_status`、`idx_security_logs_event_type_created`、`idx_saga_logs_status_started` の 3 インデックスを追加 | 追記 |
| H11 | High | tech-lead | Redis Cluster 制約への設計対応不足 | CROSSSLOT エラー回避（ハッシュタグ `{user:{id}}`）、KEYS→SCAN 移行方針を追加 | 追記 |
| H12 | High | tech-lead | エラーバジェット枯渇時の対応ポリシー未定義 | バジェット残量別アクションテーブル（4 段階: 通常/注意/警告/凍結）と回復計画テンプレートを追加 | 追記 |
| H13 | High | performance-reviewer | PgBouncer フェイルオーバー未定義 | PgBouncer ヘルスチェック、コンテナ再起動ポリシー、直接接続フォールバック戦略を追加 | 追記 |
| H14 | High | performance-reviewer | Kafka パーティション拡張手順未定義 | 拡張判断基準（Consumer Lag、TPS）と 4 ステップ拡張手順を追加 | 追記 |
| H15 | High | compliance-reviewer | DPO 任命判断の先送りリスク | 判断期限を Phase 1 開始前に設定。Art.37(1)(b) 該当可能性の強調と暫定プライバシー責任者任命を追記 | 強化 |
| H16 | High | compliance-reviewer | 電子帳簿保存法対応の欠如 | 電子帳簿保存法対応セクション（真実性の確保: 訂正削除履歴方式、可視性の確保: 検索要件テーブル）を追加 | 追記 |
| H17 | High | qa-manager | Chaos Engineering テスト計画が未定義 | Azure Chaos Studio による 6 シナリオ（サービスクラッシュ、ネットワークパーティション、DB フェイルオーバー、Kafka ブローカーダウン、Redis ノード障害、高レイテンシ注入）、実行頻度（月次）、ガードレールを追加 | 追記 |
| H18 | High | release-manager | ホットフィックス承認が決済サービスに不十分 | 決済関連サービスは最低 2 名承認を維持するルールを追加。1 名承認の場合の 24 時間以内事後承認プロセスを追記 | 強化 |
| H19 | High | infra-ops-reviewer | DR フェイルオーバー Runbook 不足 | 7 ステップのチェックリスト形式 Runbook（障害検知→エスカレーション→判断→DB 切替→アプリ切替→検証→通知）を追加。RTO 1 時間以内の目標時間配分を記載 | 追記 |
| H20 | High | audit-reviewer | ADR 実体化期限が不明確 | Phase 1 基盤構築の完了条件として ADR ファイルの存在・内容充足を必須化。ADR-0001〜0010 の範囲を明確化 | 強化 |
| H21 | High | ux-accessibility-reviewer | エラーメッセージ i18n 戦略が未定義 | RFC 9457 Problem Details の `type` フィールドから翻訳キーへのマッピングルール、`detail` フィールドの利用方針（開発者のみ、ユーザー非表示）を追加 | 追記 |
| H22 | High | architect | Outbox 動的バックオフ設計が未記載 | 指数バックオフアルゴリズム詳細（初期 500ms、イベント検出時 100ms リセット、未検出時 2 倍増加、上限 5s）をコメント形式で追記 | 追記 |

## 修正方針
- **全体仕様書の範囲にとどめる**: 個別マイクロサービスの詳細実装は各設計書に委任し、spec.md では全体方針・設計判断・テーブル定義レベルで記載
- **既存構成を維持**: セクション構造を崩さず、適切な位置に追記・強化
- **AGENTS.md / Instructions との整合性**: 修正内容が AGENTS.md および各 instructions ファイルの規約に準拠していることを確認
