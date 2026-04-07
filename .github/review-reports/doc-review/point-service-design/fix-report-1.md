# 修正レポート — point-service-design.md

## 修正サマリー

| カテゴリ | 検出数 | 修正済 | 残存 |
|---------|-------|-------|------|
| **Critical** | 6 | 6 | 0 |
| **High** | 15 | 15 | 0 |
| **合計** | 21 | 21 | 0 |

## Critical 修正一覧

| # | 指摘 ID | 修正内容 | 対象セクション |
|---|---------|---------|---------------|
| 1 | C-01 | `PointBalance` → `PointAccount` にリネーム。Aggregate Root 名を spec.md に統一。`point_transactions` に `account_id` FK カラム追加。ER 図・テーブル定義・コード例・プロジェクト構成を全て更新。 | §5.1, §5.2, §4.1, §10, §13, §15 |
| 2 | C-02 | gRPC サービス定義セクション（§6.5）を追加。`point.proto` に `ReservePoints`, `ReleasePoints`, `AwardPoints` を定義。Program.cs での gRPC 登録例を追加。§4.1/§4.2 のダイアグラムに gRPC を反映。§18.1 を gRPC 経由に変更。 | §6.5, §4.1, §4.2, §18.1 |
| 3 | C-03 | ティア昇格基準を**年間購入金額（税込）**に変更（spec.md §会員ランク制度準拠）。還元率を 1%/3%/5%/7% に統一。tier_definitions テーブルを `min_points`/`earn_rate_multiplier` → `min_annual_purchase`/`point_rate` に変更。 | §5.2, §8 |
| 4 | C-04 | ティア（MemberRank）管理を UserManagementService に移管。`user_tiers` テーブルを削除（移管注記を追加）。`MemberRankEventConsumer` を追加し、`member_rank.updated` Kafka イベントで Redis キャッシュに同期する設計に変更。 | §5.2, §8.1, §9.2, §10, §13 |
| 5 | C-05 | `PointRule`, `PointCampaign`, `PointConversionRate` の 3 エンティティを追加（テーブル定義・インデックス・CHECK 制約含む）。PointCalculator を DI 対応のインスタンスメソッドに変更し、ルール適用・キャンペーン倍率を組み込み。プロジェクト構成に Repository/Service を追加。 | §5.1, §5.2, §8.3, §13 |
| 6 | C-06 | `outbox_events` テーブルを追加。`OutboxPublisher` BackgroundService（動的バックオフ 100ms〜5s）を §9.4 に追加。全イベント発行を Outbox パターン経由に変更。PointExpirationChecker にも Outbox INSERT を組み込み。 | §5.1, §5.2, §9, §9.4, §7.3, §13 |

## High 修正一覧

| # | 指摘 ID | 修正内容 | 対象セクション |
|---|---------|---------|---------------|
| 1 | H-01 | `point_transactions.type` の CHECK 制約を `('EARN','REDEEM','EXPIRE','ADJUST','RESERVE','RELEASE','REFUND')` に変更。spec.md の 4 値 + Saga 必須の 3 値を統合。 | §5.2 |
| 2 | H-02 | **エスカレーション対応**: 設計書 §19 に「ポイントは整数値で管理」と明記されており、INTEGER を維持。spec.md 側の DECIMAL(12,2) との差異は注記として記録（spec.md 修正を提案）。 | §19（変更なし、注記対応） |
| 3 | H-03 | 全テーブル定義の `TIMESTAMP` を `TIMESTAMP WITH TIME ZONE` に変更（6 テーブル、計 18 カラム）。 | §5.2 全テーブル |
| 4 | H-04 | ティア降格評価日を **4 月 1 日**、猶予条件を**閾値の 83%**（プラチナ: 250,000 円以上で維持）に修正。1 ランクのみ降格ルールも追記。 | §8.2 |
| 5 | H-05 | `user.deleted`（ポイントアカウント無効化）、`payment.refunded`（返金時ポイント返却）、`member_rank.updated`（ランク同期）の購読を追加。対応 Consumer クラスをプロジェクト構成に追加。 | §9.2, §13 |
| 6 | H-06 | Kafka トピック名（小文字ドット区切り）とイベント record 名の対応表を §9.1 に追加。`point.earned`, `point.redeemed`, `point.reserved`, `point.released`, `point.expired` を明示。 | §9.1 |
| 7 | H-07 | ポイント計算を還元率ベース（`注文金額 × 還元率 × キャンペーン倍率`）に変更。計算例を spec.md 準拠の値に更新（Silver ¥10,000 → 300pt、Gold → 500pt、Platinum → 700pt）。 | §8.3 |
| 8 | H-08 | サービス間認証セクション（§6.6）を追加。Client Credentials Grant + JWT によるサービス間認証ポリシー、gRPC Interceptor、mTLS の設計を記載。 | §6.6 |
| 9 | H-09 | データベース名を `skishopdb` → `pointdb` に変更（3 箇所: §3, §4.1 ダイアグラム）。ADR-0006 準拠の注記を追加。 | §3, §4.1 |
| 10 | H-10 | `PointExpirationChecker` に `pg_try_advisory_lock(hashtext('point_expiry'))` によるインスタンス排他制御を追加。ロック取得失敗時はスキップ。finally でロック解放。 | §7.3 |
| 11 | H-11 | `point_expiries.is_expired BOOLEAN` → `status VARCHAR(20)` に変更。CHECK 制約 `CHECK (status IN ('ACTIVE','EXPIRED'))` を追加。インデックス条件も更新。 | §5.2, §5.1 |
| 12 | H-12 | 全イベント record に `string CorrelationId` プロパティを追加（5 件: PointsEarnedEvent, PointsRedeemedEvent, PointsReservedEvent, PointsReleasedEvent, PointsExpiredEvent）。 | §9.3 |
| 13 | H-13 | テスト戦略を大幅拡充。PointCalculator（ランク別計算、キャンペーン倍率、端数処理）、ExpiryService（バッチ処理、部分失効）、Saga 冪等性（Reserve/Release/Award 重複）、楽観的ロック競合のテストケースを追加。 | §15.1, §15.2 |
| 14 | H-14 | 有効期限バッチ処理にバッチサイズ分割（`config.ExpiryBatchSize` 件ずつ独立トランザクション）を導入。`do-while` ループで残りがある限り継続。 | §7.3 |
| 15 | H-15 | Saga 補償トランザクションの冪等性設計を §18.2 に追加。`orderId + type` の組み合わせによる重複チェック。ReservePoints の冪等性実装コード例と 3 操作の冪等性パターン表を追加。 | §18.2 |

## エスカレーション事項

| # | 優先度 | 内容 | 推奨判断者 |
|---|--------|------|-----------|
| E-01 | **対応済** | ティア（MemberRank）を UserManagementService に移管済み（spec.md 準拠）。PointService は Kafka イベントで同期する設計に変更。 | — |
| E-02 | **要確認** | ポイント値のデータ型（INTEGER vs DECIMAL(12,2)）。設計書は INTEGER（§19「ポイントは整数値で管理」）を維持。**spec.md 側の修正を提案**: PointAccount.balance / PointTransaction.amount を `INTEGER` に変更する ADR 起票を推奨。 | プロダクトオーナー |
| E-03 | **対応済** | `PointRule`, `PointCampaign`, `PointConversionRate` を Phase 1 で追加済み（テーブル定義・基本 Service/Repository）。詳細なビジネスロジックは Phase 2 で拡充予定。 | — |
| E-04 | **要確認** | Saga ステップの取引タイプ（RESERVE/RELEASE/REFUND）を spec.md の CHECK 制約に追加する ADR の起票要否。設計書側は拡張済み。 | テックリード |

## 追加修正（Medium 一部対応）

| 指摘 ID | 修正内容 |
|---------|---------|
| M-01 | `point_accounts` テーブルに `created_at` カラム追加 |
| M-02 | CHECK 制約追加: `available_points >= 0`, `points != 0`, `amount > 0`, `status IN (...)` |
| M-12 | `Microsoft.Extensions.Http.Resilience 9.*` を §2 主要ライブラリに追加 |
| M-13 | `member_rank.updated` イベント購読を §9.2 に追加 |
| M-14 | 統合テストに Testcontainers.PostgreSql の使用例を追加 |
