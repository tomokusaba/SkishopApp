# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/point-service-design.md`（PointService 詳細設計書）
- **判定**: ❌ **Rejected** — 重大な不備あり（Critical 指摘 6 件検出）
- **レビュー日時**: 2026-04-03
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

## 技術スタック検証結果
| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| ランタイム | .NET 10 (C# 14) | .NET 10 (C# 14) | ✅ |
| フレームワーク | ASP.NET Core 10 Minimal API | ASP.NET Core 10 Minimal API | ✅ |
| ORM | EF Core 10 (Npgsql 10.*) | EF Core 10 | ✅ |
| DB | PostgreSQL (skishopdb) | PostgreSQL (**pointdb** — ADR-0006) | ❌ DB 名不一致 |
| メッセージング | Confluent.Kafka 2.* | Confluent.Kafka 2.* | ✅ |
| キャッシュ | StackExchange.Redis 2.* | StackExchange.Redis 2.* | ✅ |
| バリデーション | FluentValidation 11.* | FluentValidation 11.* | ✅ |
| ログ | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| 可観測性 | OpenTelemetry 1.* | OpenTelemetry 1.* | ✅ |
| 耐障害性 | Polly 8.* | Polly 8.* + Microsoft.Extensions.Http.Resilience 9.* | ⚠️ Http.Resilience 未記載 |
| Saga 通信 | **REST のみ** | **gRPC** (point.proto) | ❌ gRPC 未記載 |

## 指摘サマリー
| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ❌ Fail | 1 | 2 | 1 | 0 |
| architect | ❌ Fail | 2 | 3 | 2 | 0 |
| programing-reviewer | ⚠️ Warn | 0 | 2 | 2 | 1 |
| dba-reviewer | ❌ Fail | 2 | 3 | 2 | 0 |
| security-reviewer | ⚠️ Warn | 0 | 1 | 2 | 0 |
| compliance-reviewer | ✅ Pass | 0 | 0 | 1 | 0 |
| audit-reviewer | ⚠️ Warn | 0 | 1 | 1 | 0 |
| qa-manager | ⚠️ Warn | 0 | 1 | 1 | 0 |
| performance-reviewer | ⚠️ Warn | 0 | 1 | 1 | 0 |
| infra-ops-reviewer | ⚠️ Warn | 0 | 0 | 2 | 1 |
| release-manager | ✅ Pass | 0 | 0 | 0 | 1 |
| oss-reviewer | ✅ Pass | 0 | 0 | 1 | 0 |
| ux-accessibility-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| tech-lead | ❌ Fail | 1 | 1 | 0 | 0 |
| **合計** | | **6** | **15** | **16** | **3** |

## 判定根拠
- 判定ルール適用結果: Critical 指摘が 6 件存在 → 自動 **❌ Rejected**
- 最も重大な指摘: Aggregate Root 名・エンティティ構造が spec.md と根本的に不一致（PointAccount vs PointBalance）。Saga 通信プロトコルが gRPC ではなく REST で設計されており spec.md と矛盾。会員ランク制度の基準（年間購入金額 vs 累計ポイント）が spec.md と完全に乖離。

---

## Critical/High 指摘一覧（修正必須）

### Critical 指摘

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|---------------|----------|----------|
| C-01 | **Critical** | architect, dba-reviewer | エンティティ設計 | §5 データモデル | **Aggregate Root 名が spec.md と不一致**。spec.md §DDD 戦術パターンでは `PointAccount` を Aggregate Root、`PointTransaction` を子エンティティとして定義（L348）。設計書は `PointBalance` を使用しており、spec.md のエンティティ一覧（`PointAccount`, `PointTransaction`, `PointRule`, `PointExpiry`, `PointCampaign`）とも一致しない。FK 制約定義（spec.md）は `point_transactions.account_id → point_accounts` を参照するが、設計書は `user_id` ベースの異なるスキーマ。 | `PointBalance` を `PointAccount` にリネームし、spec.md のエンティティ定義（`id`, `userId`, `balance`, `lifetimePoints`, `lastUpdatedAt`）に合わせてスキーマを再設計する。`point_transactions` に `account_id` FK カラムを追加する。 |
| C-02 | **Critical** | architect, tech-lead | Saga 通信 | §6.3, §18.1 | **Saga 内部通信が REST で設計されているが、spec.md は gRPC を必須指定**。spec.md（L1020-1041）は Saga ステップ 4（ポイント仮消費）・ステップ 7（ポイント確定付与）を **gRPC** で通信と明記し、`SkiShop.Contracts/Protos/point.proto` に `ReservePoints`, `ReleasePoints`, `AwardPoints` の gRPC サービス定義を掲載。設計書 §6.3 は REST エンドポイント（`/api/v1/internal/points/*`）のみ定義しており、gRPC サービス定義・`.proto` ファイル設計が完全に欠落。 | gRPC サービス定義セクションを追加し、`point.proto` の `ReservePoints`, `ReleasePoints`, `AwardPoints` を設計書に反映する。REST 内部 API は外部参照用として残しつつ、Saga 連携は gRPC を使用する旨を明記する。 |
| C-03 | **Critical** | business-analyst, architect | ビジネスルール | §8 ティアシステム | **会員ランク（ティア）の昇格基準が spec.md と根本的に矛盾**。spec.md §会員ランク制度（L2619-2637）は**年間購入金額（税込）**を基準に定義（Bronze: 0〜/1%, Silver: 50,000〜/3%, Gold: 100,000〜/5%, Platinum: 300,000〜/7%）。設計書は**累計獲得ポイント**を基準に定義（Bronze: 0/1.0x, Silver: 5,000/1.5x, Gold: 20,000/2.0x, Platinum: 50,000/3.0x）。値・単位・ロジックの全てが異なる。 | spec.md の会員ランク制度に合わせ、ティアシステムの基準を「年間購入金額（税込）」に変更する。ポイント還元率も spec.md の定義（1%/3%/5%/7%）に統一する。なお、spec.md では MemberRank は UserManagementService の管轄であり、PointService との責務分担を明確化する必要がある（下記 C-04 参照）。 |
| C-04 | **Critical** | architect | 責務分担 | §8, spec.md | **ティア（MemberRank）の所属サービスが spec.md と矛盾**。spec.md §マイクロサービスデータモデル詳細では `MemberRank` エンティティを **UserManagementService** に配置し、FK 制約定義でも `member_ranks.user_id → users`（UserManagementService 内部）としている。設計書は `UserTier`/`TierDefinition` エンティティを PointService 内に配置しており、Bounded Context の境界が spec.md と異なる。 | spec.md の設計に従い、MemberRank 管理を UserManagementService に移管するか、あるいは spec.md 側の責務分担を見直す ADR を起票する。PointService 内に残す場合はポイント還元率の取得元として UserManagementService から MemberRankUpdated イベントを購読するアプローチを設計する。 |
| C-05 | **Critical** | dba-reviewer | データモデル | §5.2 | **spec.md 定義の 3 エンティティが完全に欠落**。spec.md §ポイント管理サービスエンティティ一覧に記載された `PointRule`（ポイント付与ルール: conversionRate, minimumAmount, applicableProducts, isActive）、`PointCampaign`（ポイントキャンペーン: multiplier, startDate, endDate, targetProducts, isActive）、`PointConversionRate`（ポイント換算レート: fromCurrency, toCurrency, rate, effectiveDate）が設計書に存在しない。spec.md §コンポーネント構成図でもルールサービス・キャンペーンサービスが独立コンポーネントとして描かれている。 | `PointRule`, `PointCampaign`, `PointConversionRate` エンティティのテーブル定義・EF Core マッピング・Repository・Service を設計書に追加する。`PointCalculator` のポイント計算ロジックにルール適用・キャンペーン倍率の反映を組み込む。 |
| C-06 | **Critical** | architect | イベント設計 | §9, ADR-0005 | **Outbox パターンが設計に含まれていない**。ADR-0005 は全マイクロサービスに Outbox パターンによるイベント発行保証を必須としている。設計書 §9 のイベント発行は Outbox テーブルを経由せず直接 Kafka に発行する設計になっている。`outbox_events` テーブル定義、`OutboxPublisher` BackgroundService、DB トランザクション内での Outbox 書き込みが全て欠落。 | `outbox_events` テーブルを §5.2 に追加し、§9 のイベント発行を Outbox パターン（DB トランザクション内で outbox_events INSERT → OutboxPublisher BackgroundService が定期的にポーリングして Kafka に発行）に変更する。BackgroundServices セクションに OutboxPublisher を追加する。 |

### High 指摘

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|---------------|----------|----------|
| H-01 | **High** | dba-reviewer | データモデル | §5.2 | **ポイント取引タイプ（type カラム）が spec.md CHECK 制約と不一致**。spec.md CHECK 制約は `type IN ('EARN','REDEEM','EXPIRE','ADJUST')` と定義。設計書は `type IN ('EARN', 'SPEND', 'RESERVE', 'RELEASE', 'EXPIRE', 'REFUND')` を使用。`REDEEM` → `SPEND`、`ADJUST` が欠落（管理者調整機能があるにも関わらず）、`RESERVE`/`RELEASE`/`REFUND` が spec.md に未定義。 | spec.md 側を Saga 対応で拡張するか、設計書を spec.md に合わせるかを決定する。Saga の RESERVE/RELEASE は必須機能なので、**spec.md の CHECK 制約を拡張**して `('EARN','REDEEM','EXPIRE','ADJUST','RESERVE','RELEASE','REFUND')` とする ADR を起票するのが妥当。 |
| H-02 | **High** | dba-reviewer | データモデル | §5.2 | **データ型不一致**。spec.md は PointAccount.balance を `DECIMAL(12,2)`、PointTransaction.amount を `DECIMAL(12,2)` と定義。設計書は全て `INTEGER` を使用。整数か小数かの選択はビジネス要件の根幹であり、実装者に混乱を生じさせる。 | spec.md と設計書で統一する。設計書 §19 に「ポイントは整数値で管理」と明記されているため、**spec.md 側を INTEGER に修正**するか、小数ポイントの必要性を再評価する。 |
| H-03 | **High** | dba-reviewer | データモデル | §5.2 | **TIMESTAMP 型が `WITH TIME ZONE` なし**。`sql-schema-review.instructions.md` §2 データ型選択で日時カラムは `TIMESTAMP WITH TIME ZONE` を必須としている。設計書の全テーブル定義（tier_definitions, user_tiers, point_balances, point_transactions, point_expiries）で `TIMESTAMP` のみ記載されており、タイムゾーン情報が欠落。 | 全テーブル定義の `TIMESTAMP` カラムを `TIMESTAMP WITH TIME ZONE` に変更する。 |
| H-04 | **High** | business-analyst | ビジネスルール | §8.2 | **ティア降格の年次評価日・猶予期間が spec.md と矛盾**。spec.md: 4 月 1 日評価、降格猶予は閾値の 83%（プラチナの場合 250,000 円以上で維持）、1 ランクのみ降格。設計書: 1 月 1 日評価、3 ヶ月間の猶予期間。評価日・猶予ロジックの両方が異なる。 | spec.md の会員ランク制度ルールに合わせて、降格評価日を 4 月 1 日、猶予条件を閾値 83% に修正する。 |
| H-05 | **High** | architect | イベント設計 | §9.2 | **spec.md で定義された Kafka 購読イベントが欠落**。spec.md Kafka トピック一覧（L6043 付近）で PointService が購読すべきイベント: `user.deleted`（ポイントアカウント削除/無効化）、`payment.refunded`（返金時のポイント返却処理）。設計書 §9.2 には `OrderCreated`, `OrderCancelled`, `USER_REGISTERED` のみ記載。 | §9.2 に `user.deleted`（ポイントアカウント処理）、`payment.refunded`（ポイント返却）の購読を追加する。 |
| H-06 | **High** | architect | イベント設計 | §9.1 | **Kafka トピック名の命名不一致**。spec.md のトピック命名は `point.earned`, `point.redeemed`, `point.expired`（小文字ドット区切り）。設計書のイベント名は `PointsAwarded`, `PointsSpent`, `PointsExpired`（PascalCase record 名）。トピック名と record 名の対応関係が不明確で、`point.redeemed` に対応するイベント（`PointsSpent`）の名前が異なる。また `PointsReserved`, `PointsReleased`, `TierUpgraded`, `TierDowngraded` に対応する Kafka トピックが spec.md に未定義。 | 設計書にトピック名とイベント record の対応表を追加する。不足トピックは spec.md 側に追加を提案する。 |
| H-07 | **High** | business-analyst | ビジネスルール | §8.3 | **ポイント計算ロジックが spec.md の還元率と不一致**。spec.md は還元率（1%/3%/5%/7%）で定義。設計書は「100 円 = 1 ポイント」の基本レート × ティア倍率（1.0/1.5/2.0/3.0）で計算。例: 10,000 円の注文で Bronze の場合、spec.md では 100 ポイント（1%）、設計書でも 100 ポイント（100 × 1.0）で結果は一致するが、Silver の場合 spec.md では 300 ポイント（3%）、設計書では 150 ポイント（100 × 1.5）と乖離。 | spec.md の還元率定義に合わせてポイント計算ロジックを修正する（C-03 の修正と連動）。 |
| H-08 | **High** | security-reviewer | セキュリティ | §6.3 | **内部 API のサービス間認証が未設計**。§6.3 の内部 API（`/api/v1/internal/points/*`）は PaymentCartService から呼び出されるが、認証方式が「サービス間認証」とのみ記載され具体的な設計がない。spec.md では gRPC + Client Credentials による mTLS/JWT 認証が前提。IDOR/不正アクセスのリスクあり。 | サービス間認証の具体的方式（Client Credentials Grant + JWT、mTLS 等）を設計書に追記する。内部 API へのアクセスをサービス間認証ポリシーで制限する設計を明記する。 |
| H-09 | **High** | architect | DB 設計 | §3 | **データベース名が ADR-0006 と不一致**。ADR-0006（サービス別独立 DB）では各サービスに専用の論理 DB を割り当て（`pointdb`）。設計書 §3 は `skishopdb` と記載。ADR-0006 の `AddDatabase` 例でもサービスごとに分離されている。 | DB 名を `pointdb` に修正し、ADR-0006 の Database per Service パターンに準拠する。 |
| H-10 | **High** | programing-reviewer | コード品質 | §7.3, §13 | **BackgroundService の単一インスタンスロックが未設計**。spec.md（L1000-1010）では BackgroundService の複数インスタンス実行防止に `pg_try_advisory_lock` を必須としており、`PointExpiryService` に `hashtext('point_expiry')` のロック ID を定義。設計書の `PointExpirationChecker` はロック取得なしで実装。 | `PointExpirationChecker` の `ExecuteAsync` 冒頭に `pg_try_advisory_lock(hashtext('point_expiry'))` によるロック取得を追加する（spec.md の BackgroundService Lock 設計に準拠）。 |
| H-11 | **High** | programing-reviewer | コード品質 | §5.2 | **point_expiries の状態管理カラムが spec.md と不一致**。spec.md CHECK 制約は `point_expiries.status IN ('ACTIVE','EXPIRED')` として VARCHAR status カラムを定義。設計書は `is_expired BOOLEAN` を使用。spec.md と異なるカラム名・データ型。 | spec.md に合わせて `is_expired BOOLEAN` を `status VARCHAR(20)` に変更し、CHECK 制約 `CHECK (status IN ('ACTIVE','EXPIRED'))` を追加する。 |
| H-12 | **High** | audit-reviewer | トレーサビリティ | §9.3 | **イベントペイロードに correlationId が欠落**。AGENTS.md §11.2 は全リクエストに相関 ID を付与しサービス間で伝搬することを必須としている。設計書のイベントレコード定義（`PointsAwardedEvent`, `TierUpgradedEvent` 等）に `CorrelationId` プロパティが含まれていない。 | 全イベント record に `string CorrelationId` プロパティを追加する。 |
| H-13 | **High** | qa-manager | テスト | §15 | **テストカバレッジの対象が不十分**。設計書のテスト戦略では単体テスト 2 件、統合テスト 2 件のみ例示。ポイント付与計算（ルール適用・キャンペーン倍率）、ティア昇格/降格、有効期限失効バッチ、Saga 補償（仮消費解放）、楽観的ロック競合、Kafka イベント処理のテストケースが欠落。AGENTS.md は分岐カバレッジ 80% 以上を要求。 | PointCalculator、TierService（昇格/降格各パターン）、ExpiryService（バッチ処理、部分失効）、Saga 統合（Reserve→Confirm/Release）、ConcurrencyException ハンドリングのテストケースを追加する。 |
| H-14 | **High** | performance-reviewer | パフォーマンス | §7.3, §10 | **有効期限バッチ処理のスケーラビリティ設計が不十分**。`PointExpirationChecker` は全失効対象を 1 トランザクションで処理しており、大量レコード（数万件）時にトランザクション長時間化・ロック競合が発生する。バッチサイズ制限（`ExpiryBatchSize: 1000`）は appsettings に定義されているが、コード例では使用されていない。 | バッチサイズでの分割処理（`LIMIT {batchSize}`）を実装し、各バッチを独立トランザクションで処理する設計に変更する。処理件数が多い場合の動的バックオフも検討する。 |
| H-15 | **High** | architect | Saga 設計 | §18.1 | **Saga 補償トランザクションの冪等性設計が欠落**。ADR-0009 は各 Saga ステップの冪等性を要求。設計書 §18.1 は Checkout フローとの連携を概要レベルで記載するが、Reserve/Confirm/Release の冪等性保証（同一 orderId での重複リクエスト拒否 or 安全な再実行）の具体的設計がない。 | Reserve/Confirm/Release の冪等性設計を追加する。orderId + type の組み合わせによる重複チェック、または idempotency_key テーブルの活用を設計する。 |

---

## Medium/Low 指摘サマリー

### Medium 指摘（16 件）

| # | 出典 Agent | カテゴリ | 概要 |
|---|-----------|---------|------|
| M-01 | dba-reviewer | スキーマ | `point_balances` テーブルに `created_at` カラムが欠落。spec.md §監査カラム必須化ルールで全テーブルに `created_at`/`updated_at` 必須。 |
| M-02 | dba-reviewer | スキーマ | spec.md 定義の CHECK 制約が設計書に未記載（`point_accounts.balance >= 0`, `point_transactions.amount != 0`, `point_expiries.amount > 0` 等）。 |
| M-03 | architect | 構造 | Repository が Aggregate Root 単位で設計されていない。`PointTransactionRepository`, `PointBalanceRepository`, `UserTierRepository` が独立しており、PointAccount（PointBalance）Aggregate Root 経由のアクセスパターンに反する（AGENTS.md §3.4）。 |
| M-04 | architect | コンポーネント | spec.md のコンポーネント構成図では「ルールサービス」「キャンペーンサービス」「計算エンジン」が独立コンポーネントとして描かれているが、設計書 §4.1 のコンポーネント図には「ルールサービス」「キャンペーンサービス」が存在しない。 |
| M-05 | security-reviewer | 入力検証 | §6.4 の DTO に Data Annotations は記載あるが、FluentValidation の `AbstractValidator<T>` 実装が設計書に含まれていない（security-coding.instructions.md で推奨）。 |
| M-06 | security-reviewer | 認可 | §12.2 IDOR 防止のコード例で `user.FindFirstValue` の null チェック後に `throw new UnauthorizedException()` しているが、RFC 9457 準拠の Problem Details レスポンスへの変換ロジックが §12 に記載されていない。 |
| M-07 | programing-reviewer | コード品質 | `PointExpirationChecker` の `Task.Delay(TimeSpan.FromHours(24))` は起動時刻からの相対実行であり、「毎日 0:00 UTC」の定時実行にならない。`TimeProvider` DI の活用が推奨される（AGENTS.md §10.4）。 |
| M-08 | programing-reviewer | コード品質 | `PointCalculator` が static メソッドで実装されており、DI コンテナ経由でのテスト容易性・拡張性が低い。ルール適用やキャンペーン倍率を組み込む際にリファクタリングが必要になる。 |
| M-09 | compliance-reviewer | PII | §14 メトリクスで `points.balance.average`（平均ポイント残高）を計測するが、ユーザー単位のポイント残高情報は個人情報に該当しうるため、集計レベルの設計が必要。 |
| M-10 | infra-ops-reviewer | Dockerfile | Dockerfile で `EXPOSE 5007` を指定しているが、.NET 8+ のコンテナデフォルトポートは 8080。`ASPNETCORE_URLS` 環境変数または Kestrel 設定でポートを明示的に設定する必要がある。 |
| M-11 | infra-ops-reviewer | 可観測性 | spec.md §可観測性要件の Correlation ID ミドルウェア設計が §16 の設定ファイル・Program.cs 設計に含まれていない。 |
| M-12 | oss-reviewer | 依存関係 | `Microsoft.Extensions.Http.Resilience` パッケージが §2 の主要ライブラリ一覧に含まれていない（AGENTS.md §8.1 で必須）。 |
| M-13 | business-analyst | 機能 | spec.md のポイント付与シーケンス図で「UserManagementService の MemberRankUpdated イベントで事前同期済み」と記載されているが、設計書にこのイベント購読の設計がない。 |
| M-14 | qa-manager | テスト | テスト命名が一部 AGENTS.md §9.2 の推奨パターン（`Should_期待結果_When_条件`）に準拠しているが、`PointEndpointsIntegrationTest` のテストメソッドでは Testcontainers.PostgreSql の使用が記載されていない（AGENTS.md で DB スライステストに推奨）。 |
| M-15 | audit-reviewer | ADR | 既存 ADR からの逸脱（C-03 ティア基準、C-06 Outbox 欠落）に対する ADR 参照・理由付けが設計書に記載されていない。設計判断の根拠が不透明。 |
| M-16 | performance-reviewer | キャッシュ | Redis キャッシュキー `points:balance:{userId}` の TTL が 5 分だが、ポイント消費操作後のキャッシュ無効化タイミングとの整合性（stale read リスク）が未分析。 |

### Low 指摘（3 件）

| # | 出典 Agent | 概要 |
|---|-----------|------|
| L-01 | programing-reviewer | `TierService.TierChangeResult` が Service クラス内のネストされた record で定義されているが、DTOs/ ディレクトリに配置する方が一貫性がある。 |
| L-02 | release-manager | 設計書にバージョン情報（初版作成日、改定履歴）が記載されていない。 |
| L-03 | infra-ops-reviewer | Dockerfile の HEALTHCHECK で `curl` を使用しているが、aspnet ランタイムイメージには curl が含まれない場合がある。`wget` または .NET HealthCheck ツールの使用を推奨。 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-01 | **最優先** | architect, business-analyst | ティア（MemberRank）の所属サービス（PointService vs UserManagementService）の決定。spec.md は UserManagementService に配置しているが、設計書は PointService 内に配置。責務分担の決定が必要。 | プロダクトオーナー + テックリード |
| E-02 | **最優先** | dba-reviewer | ポイント値のデータ型（INTEGER vs DECIMAL(12,2)）の統一。spec.md と設計書で異なる。ビジネス要件（小数ポイントの必要性）を確認の上、spec.md を修正するか設計書を修正するか決定。 | プロダクトオーナー |
| E-03 | **高優先** | architect | spec.md の PointService エンティティ一覧に含まれる `PointRule`, `PointCampaign`, `PointConversionRate` のスコープ確認。Phase 1 で必要か、Phase 2 以降か。 | プロダクトオーナー + テックリード |
| E-04 | **高優先** | architect | Saga ステップの取引タイプ（RESERVE/RELEASE/REFUND）を spec.md の CHECK 制約に追加する ADR の起票要否。 | テックリード |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| — | — | — | 競合なし | — | — |

---

## ドキュメント横断分析

### サービス間整合性

| 整合性チェック項目 | 結果 | 詳細 |
|------------------|------|------|
| Kafka トピック名（spec.md ↔ 設計書） | ❌ 不一致 | `point.redeemed` vs `PointsSpent` 等、命名不整合（H-06） |
| 購読イベント（spec.md ↔ 設計書） | ❌ 不一致 | `user.deleted`, `payment.refunded` の購読が欠落（H-05） |
| エンティティ名（spec.md ↔ 設計書） | ❌ 不一致 | `PointAccount` vs `PointBalance`（C-01） |
| FK 制約設計（spec.md ↔ 設計書） | ❌ 不一致 | `point_transactions.account_id` vs `user_id`（C-01） |
| Saga ステップ定義（spec.md ↔ 設計書） | ❌ 不一致 | gRPC vs REST（C-02） |
| DB 名（ADR-0006 ↔ 設計書） | ❌ 不一致 | `pointdb` vs `skishopdb`（H-09） |
| ポイント還元率（spec.md ↔ 設計書） | ❌ 不一致 | 1%/3%/5%/7% vs 1.0x/1.5x/2.0x/3.0x（C-03, H-07） |
| USerRegistered イベント名 | ⚠️ 表記揺れ | spec.md: `user.registered`（小文字）、設計書: `USER_REGISTERED`（大文字） |

### 未定義・曖昧な領域

| 領域 | 影響 | ブロッカーリスク |
|------|------|-------------|
| PointRule による動的ポイント計算ロジック | ポイント計算の柔軟性 | 中（ハードコード計算で Phase 1 は対応可能） |
| PointCampaign によるキャンペーンポイント | マーケティング機能 | 低（Phase 2 以降で対応可能） |
| サービス間認証（gRPC/REST 内部 API） | セキュリティ | 高（Saga 統合時にブロッカー） |
| ポイント返金処理の詳細（payment.refunded 受信時） | 注文キャンセル/返品フロー | 高（返金時のポイント返却ルールが未定義） |
| MemberRankUpdated イベントの購読・ローカルキャッシュ同期 | ティア倍率の参照 | 中（Redis キャッシュで暫定対応可能） |

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### 観点: ビジネス要件の完全性

**Critical (1件)**:
- C-03: 会員ランク（ティア）の昇格基準が spec.md と根本的に矛盾。年間購入金額ベースと累計ポイントベースで完全に異なるビジネスロジック。

**High (2件)**:
- H-04: ティア降格の年次評価日・猶予期間が spec.md と矛盾（1/1 vs 4/1、3ヶ月猶予 vs 83%閾値）
- H-07: ポイント計算結果が Silver 以上のランクで spec.md と乖離

**Medium (1件)**:
- M-13: MemberRankUpdated イベント購読の設計欠落

</details>

<details>
<summary>architect レビューレポート</summary>

### 観点: マイクロサービス設計・DDD・Saga 統合

**Critical (2件)**:
- C-02: Saga 内部通信が REST で設計（spec.md は gRPC 必須）
- C-04: ティア管理の Bounded Context が spec.md と矛盾（UserManagementService vs PointService）
- C-06: Outbox パターン未採用（ADR-0005 違反）

**High (3件)**:
- H-05: `user.deleted`, `payment.refunded` 購読イベント欠落
- H-06: Kafka トピック名の命名不一致
- H-15: Saga 補償トランザクションの冪等性設計欠落

**Medium (2件)**:
- M-03: Repository が Aggregate Root 単位になっていない
- M-04: コンポーネント構成図にルールサービス・キャンペーンサービスが欠落

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 観点: C# 14 / .NET 10 コード品質・規約準拠

**High (2件)**:
- H-10: BackgroundService の pg_try_advisory_lock が未実装
- H-11: point_expiries の状態管理カラムが spec.md と不一致（BOOLEAN vs VARCHAR status）

**Medium (2件)**:
- M-07: PointExpirationChecker の定時実行ロジックが相対時間ベース
- M-08: PointCalculator が static で DI 非対応

**Low (1件)**:
- L-01: TierChangeResult のネスト配置

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 観点: DB スキーマ設計・EF Core マッピング

**Critical (2件)**:
- C-01: Aggregate Root 名・スキーマ構造が spec.md と不一致（PointAccount vs PointBalance）
- C-05: PointRule, PointCampaign, PointConversionRate エンティティの完全欠落

**High (3件)**:
- H-01: ポイント取引タイプの CHECK 制約値が spec.md と不一致
- H-02: データ型不一致（DECIMAL vs INTEGER）
- H-03: TIMESTAMP WITH TIME ZONE の欠落

**Medium (2件)**:
- M-01: point_balances テーブルの created_at カラム欠落
- M-02: spec.md 定義の CHECK 制約が設計書に未記載

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 観点: OWASP Top 10・認証認可・秘密情報管理

**High (1件)**:
- H-08: 内部 API のサービス間認証が未設計

**Medium (2件)**:
- M-05: FluentValidation AbstractValidator 実装の欠落
- M-06: RFC 9457 Problem Details レスポンス変換の未記載

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 観点: GDPR・個人情報保護

**Medium (1件)**:
- M-09: ポイント残高メトリクスの PII 配慮

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 観点: トレーサビリティ・ADR 整合性

**High (1件)**:
- H-12: イベントペイロードに correlationId が欠落

**Medium (1件)**:
- M-15: ADR 逸脱に対する理由付けが不透明

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 観点: テスト戦略・カバレッジ

**High (1件)**:
- H-13: テストカバレッジの対象が不十分（主要ビジネスロジックのテストケース欠落）

**Medium (1件)**:
- M-14: Testcontainers.PostgreSql の使用が統合テスト例に含まれていない

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 観点: パフォーマンス・スケーラビリティ

**High (1件)**:
- H-14: 有効期限バッチ処理のスケーラビリティ設計不十分（バッチサイズ分割未実装）

**Medium (1件)**:
- M-16: Redis キャッシュ TTL と書き込み後の stale read リスク未分析

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 観点: コンテナ設計・可観測性・運用性

**Medium (2件)**:
- M-10: Dockerfile EXPOSE ポートと .NET 8+ デフォルトの不整合
- M-11: Correlation ID ミドルウェアの Program.cs 設計欠落

**Low (1件)**:
- L-03: HEALTHCHECK の curl 使用

</details>

<details>
<summary>release-manager レビューレポート</summary>

### 観点: リリース戦略・バージョニング

**Low (1件)**:
- L-02: 設計書にバージョン情報・改定履歴が未記載

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 観点: NuGet ライセンス・依存関係

**Medium (1件)**:
- M-12: Microsoft.Extensions.Http.Resilience パッケージの記載漏れ

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 観点: UX・WCAG 2.1

指摘なし。PointService はバックエンドサービスであり、UX/アクセシビリティの直接的な影響はない。

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 観点: 技術標準の横断適合性・実装実現可能性

**Critical (1件)**:
- C-06: Outbox パターンの不採用（ADR-0005 違反）。全マイクロサービスに必須であり、PointService でのイベント発行が DB トランザクションとの原子性を保証できない。

**High (1件)**:
- H-09: Database per Service（ADR-0006）に反する DB 名（skishopdb）。

**総評**: 設計書は API 設計、キャッシュ戦略、エラーコード体系、テスト構造など個別セクションの品質は一定水準にあるが、**spec.md との整合性**に重大な問題がある。特にエンティティモデル（PointAccount vs PointBalance）、ティアシステムの設計基準、Saga 通信プロトコル（gRPC vs REST）、Outbox パターンの 4 点は実装着手前に必ず解決すべき。

</details>
