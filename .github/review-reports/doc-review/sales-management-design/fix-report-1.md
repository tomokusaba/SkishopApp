# ドキュメント修正ログ

- **対象**: design-docs/sales-management-design.md
- **イテレーション**: 1 / 5
- **修正日時**: 2026-04-03 12:15
- **対応レビュー**: check-report-1.md

## 修正サマリー
| 修正件数 | Critical | High | 合計 |
|---------|----------|------|------|
| 修正済み | 7 | 25 | 32 |
| 修正不可（エスカレーション） | 0 | 0 | 0 |

## 修正詳細
| # | 指摘ID | 重要度 | 修正内容の概要 |
|---|--------|--------|-------------|
| 1 | C-1 | Critical | Saga を 3 ステップ→9 ステップに全面書き換え（spec.md SSOT 準拠） |
| 2 | C-2 | Critical | Outbox パターン追加（outbox_events テーブル、OutboxPublisher BackgroundService、動的バックオフ） |
| 3 | C-3 | Critical | SagaRecoveryService 追加（SELECT FOR UPDATE SKIP LOCKED、30 秒ポーリング、5 分タイムアウト） |
| 4 | C-4 | Critical | saga_logs テーブル追加（14 カラム + CHECK 制約 + 部分インデックス） |
| 5 | C-5 | Critical | 注文ステータス状態遷移図 + OrderStateMachine 追加 |
| 6 | C-6 | Critical | Idempotency-Key 設計追加（テーブル定義 + ヘッダー仕様 + TTL 24 時間） |
| 7 | C-7 | Critical | gRPC proto 定義 + Saga ステップ別 Deadline 設計追加 |
| 8 | H-1~H-25 | High | Kafka トピック名修正、Invoice 追加、CHECK 制約、TIMESTAMP WITH TIME ZONE、row_version、補償設計、配送料 10,000 円、税計算、productSnapshot、レイテンシバジェット、PENDING_PAYMENT、IDOR 防止、PII マスキング、RTO 1 時間、/health/ready、POST cancel、SagaCoordinator コード例、ClosedXML 修正、監査カラム、user.deleted 購読、テスト戦略拡充、Advisory Lock |
