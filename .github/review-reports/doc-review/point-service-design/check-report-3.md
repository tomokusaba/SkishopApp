# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/point-service-design.md`
- **判定**: ⚠️ **Conditional Approval** — High 指摘あり、人間の判断を介在
- **レビュー日時**: 2026-04-03 (イテレーション 3)
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

## 技術スタック検証結果
| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| 言語 | C# 14 (.NET 10) | C# 14 (.NET 10) | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| ORM | EF Core 10 | EF Core 10 | ✅ |
| DB | PostgreSQL (pointdb) | PostgreSQL | ✅ |
| メッセージング | Apache Kafka (Confluent.Kafka 2.*) | Apache Kafka (Confluent.Kafka 2.*) | ✅ |
| キャッシュ | Redis (StackExchange.Redis) | Redis (StackExchange.Redis 2.*) | ✅ |
| 認証 | JWT Bearer | ASP.NET Core Identity + Microsoft.Identity.Web | ✅ |
| バリデーション | FluentValidation 11.* | FluentValidation 11.* | ✅ |
| ロギング | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| 可観測性 | OpenTelemetry | OpenTelemetry | ✅ |
| 耐障害性 | Polly 8.* + Microsoft.Extensions.Http.Resilience 9.* | 同上 | ✅ |
| テスト | xUnit + NSubstitute + Shouldly + Testcontainers | 同上 | ✅ |
| コンテナ | Docker (aspnet:10.0) | Docker 25.x | ✅ |

## 指摘サマリー
| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ⚠️ | 0 | 1 | 2 | 1 |
| architect | ⚠️ | 0 | 2 | 1 | 0 |
| tech-lead | ⚠️ | 0 | 1 | 1 | 1 |
| programing-reviewer | ✅ | 0 | 0 | 2 | 1 |
| security-reviewer | ⚠️ | 0 | 1 | 1 | 0 |
| dba-reviewer | ✅ | 0 | 0 | 2 | 1 |
| qa-manager | ⚠️ | 0 | 1 | 1 | 0 |
| performance-reviewer | ⚠️ | 0 | 1 | 1 | 0 |
| compliance-reviewer | ⚠️ | 0 | 1 | 0 | 1 |
| oss-reviewer | ✅ | 0 | 0 | 0 | 1 |
| release-manager | ✅ | 0 | 0 | 1 | 0 |
| infra-ops-reviewer | ✅ | 0 | 0 | 1 | 1 |
| audit-reviewer | ⚠️ | 0 | 1 | 0 | 0 |
| ux-accessibility-reviewer | ✅ | 0 | 0 | 0 | 1 |
| **合計** | | **0** | **9** | **13** | **8** |

## 判定根拠
- 判定ルール適用結果: Critical 指摘 0 件、High 指摘 9 件 → ⚠️ Conditional Approval
- 最も重大な指摘: spec.md との Saga ステップ番号・proto 定義の不整合（architect H-01, H-02）、ポイント消費時の FIFO 有効期限制御の未記載（business-analyst H-01）

## Critical/High 指摘一覧（修正必須）
| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| H-01 | High | business-analyst | ビジネスロジック | **ポイント消費時の FIFO（先入先出）有効期限制御が設計書に記載されていない**。spec.md §H8-24 では「期限延長なし（FIFO 消費）」と明記されている。ポイント消費（REDEEM）時に、有効期限の早い `PointExpiry` レコードから順に消費する FIFO ロジックが §7.2 のフロー図・§18.2 の実装コードのいずれにも存在しない。消費時に `point_expiries` の `ACTIVE` レコードを `expires_at ASC` で取得し、順に `CONSUMED` に更新する処理が必要。 | §7.2 ポイント消費フローに FIFO 消費ロジックを追記。`PointExpiry` エンティティの Status に `CONSUMED` を追加（§A の PointExpiry クラスには既に記載あり、テーブル定義の CHECK 制約と一致させる） |
| H-02 | High | architect | Saga 整合性 | **Saga ステップ番号が spec.md と不一致**。設計書 §6.5 では「Saga ステップ 4: ポイント仮消費」「Saga ステップ 7: ポイント確定付与」と記載しているが、§18.1 では「Saga ステップ 4: ReservePoints」「ポイント消費確定: ConfirmPoints」と記載され、ステップ 7 の用語が「ポイント確定付与（AwardPoints）」と「ポイント消費確定（ConfirmPoints）」で混在している。spec.md L1020-1023 では「ステップ 4: ポイント仮消費」「ステップ 7: ポイント確定付与」と定義されている。 | §6.5 と §18.1 で Saga ステップの用語を統一する。spec.md の定義に合わせ: ステップ 4 = ReservePoints（仮消費）、ステップ 7 = AwardPoints（確定付与）。ConfirmPoints は「仮消費→確定消費」の変換操作として、ステップ番号を付与するか「後処理」として明確に位置付ける |
| H-03 | High | architect | gRPC 契約 | **gRPC proto 定義が spec.md と §I のローカル定義で乖離**。§6.5 の proto では `service PointService` と定義し、§I の proto 完全版では `service PointGrpcService` と定義している。また §I には `GetBalance` RPC が追加されているが §6.5 には存在しない。spec.md L1075-1079 の定義は `service PointService { ReservePoints, ReleasePoints, AwardPoints }` の 3 RPC のみ。 | §6.5 と §I の proto 定義を統一する。サービス名は `PointService`（spec.md 準拠）とし、追加 RPC（ConfirmPoints, GetBalance）は設計書内で明確に「spec.md 拡張」として位置付ける |
| H-04 | High | tech-lead | 規約整合性 | **`PointExpiry` の `status` CHECK 制約がテーブル定義と C# エンティティ・AppDbContext で不一致**。§5.2 テーブル定義では `CHECK (status IN ('ACTIVE','EXPIRED'))` の 2 値だが、§A の PointExpiry C# クラスのコメントでは `Status: ACTIVE / EXPIRED / CONSUMED` の 3 値、§B の AppDbContext では `CHECK ... IN ('ACTIVE','EXPIRED','CONSUMED')` の 3 値。FIFO 消費に `CONSUMED` が必要なら、テーブル定義 §5.2 にも反映が必要。 | §5.2 の `point_expiries` テーブル定義の CHECK 制約を `CHECK (status IN ('ACTIVE','EXPIRED','CONSUMED'))` に修正する |
| H-05 | High | security-reviewer | 認証・認可 | **gRPC エンドポイントへのサービス間認証設定が不完全**。§6.6 で `InternalServiceOnly` ポリシーを REST 内部 API に適用しているが、gRPC の `PointGrpcService` への認証ポリシー適用が §G の Program.cs に記載されていない。`app.MapGrpcService<PointGrpcService>()` に `.RequireAuthorization("InternalServiceOnly")` が未設定。未認証の gRPC 呼び出しが可能になるリスクがある。 | §G の Program.cs に `app.MapGrpcService<PointGrpcService>().RequireAuthorization("InternalServiceOnly");` を追記。または `PointGrpcService` クラスに `[Authorize(Policy = "InternalServiceOnly")]` 属性を付与する設計を追記 |
| H-06 | High | qa-manager | テスト | **統合テストのカバレッジが不十分**。§15.2 の統合テストは認証済みユーザーの残高取得と未認証テストの 2 件のみ。gRPC エンドポイント（ReservePoints/ReleasePoints/AwardPoints/ConfirmPoints）の統合テスト、Outbox パターンの E2E テスト（DB INSERT → Kafka 発行）、PointExpirationChecker の統合テストが欠落している。AGENTS.md §9.4 のカバレッジ 80% 目標を達成するにはテストケースが不足。 | §15 に以下を追加: (1) gRPC Saga 連携テスト（ReservePoints → ConfirmPoints / ReleasePoints のフロー）、(2) Outbox Publisher 統合テスト、(3) PointExpirationChecker バッチ処理テスト、(4) MemberRankEventConsumer の Redis 更新テスト |
| H-07 | High | performance-reviewer | パフォーマンス | **ポイント失効バッチ処理のパフォーマンスリスク**。§7.3 の `PointExpirationChecker` で、バッチ内の各 `PointExpiry` に対して個別に `PointAccount` を `FirstOrDefaultAsync` で取得しているため、N+1 クエリが発生する。大量の失効レコード（例: 数万件）がある場合、バッチ処理時間が §14.3 のアラート条件「失効バッチ処理時間 > 30 分」を超過するリスクがある。 | (1) `PointExpiry` 取得時に `.Include(e => e.Account)` で Eager Loading する、または (2) バッチ内の `UserId` リストで一括 `PointAccount` を取得し Dictionary でルックアップする設計に変更 |
| H-08 | High | compliance-reviewer | データ保護 | **ポイント取引履歴のデータ保持期間が未定義**。`point_transactions` テーブルにはデータ保持期間・アーカイブポリシーが設計書に記載されていない。spec.md §GDPR/個人情報保護設計では監査ログ 7 年、ユーザーデータは削除要求に対応が必要とされている。ポイント取引履歴は個人データに該当し、DSR（データ主体要求）への対応方針が未記載。 | §19 制約・前提条件、または新セクションに以下を追記: (1) `point_transactions` のデータ保持期間（例: 7 年、電子帳簿保存法準拠）、(2) DSR 削除要求時の対応方針（匿名化 or 論理削除）、(3) アーカイブ戦略（N 年経過後の Archive tier 移行） |
| H-09 | High | audit-reviewer | 監査証跡 | **管理者ポイント手動調整の監査ログ構造が未定義**。§12.3 で「管理者のポイント手動調整は監査ログに記録」と記載されているが、監査ログのテーブル構造・記録項目（操作者 ID、対象ユーザー ID、調整前後の残高、調整理由、IP アドレス、タイムスタンプ）が設計書に存在しない。`point_transactions` の `description` フィールドだけでは監査要件を満たさない。 | §12.3 に監査ログの設計を追加: (1) `point_audit_logs` テーブル定義（admin_user_id, target_user_id, action, points_before, points_after, reason, ip_address, created_at）、または (2) 既存の `point_transactions` に `performed_by` カラムを追加し、管理者操作を識別可能にする |

## エスカレーション事項（要人間判断）
| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-01 | 高優先 | architect | ConfirmPoints RPC の Saga ステップ内での位置付け。spec.md にはステップ 4（仮消費）→ ステップ 7（確定付与）の間に「消費確定」ステップの記載がないが、設計書では ConfirmPoints を追加している。spec.md の Saga 定義を更新するか、設計書から ConfirmPoints を削除するかの判断が必要 | テックリード |
| E-02 | 通常 | business-analyst | ポイント消費の上限（`MaxRedeemPoints = 500,000`）の妥当性。1 回の注文でのポイント充当上限が spec.md に定義されていない。ビジネスルールとして合意が必要 | PO / ビジネス担当 |

## 競合解決記録
| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| — | — | — | 今回の指摘では Agent 間の直接的な競合は検出されなかった | — | — |

## ドキュメント横断分析

### サービス間整合性
- **spec.md Saga ステップとの整合性**: Saga ステップ 4（ポイント仮消費）/ ステップ 7（ポイント確定付与）の gRPC 定義は概ね一致するが、ConfirmPoints RPC が spec.md に未定義であり追加承認が必要（E-01）
- **UserManagementService 連携**: `member_rank.updated` Kafka イベントによるランク同期設計は spec.md の MemberRank 設計と整合
- **Kafka イベント整合性**: `point.earned`, `point.redeemed`, `point.expired` は spec.md のイベント設計と整合。`point.reserved`, `point.released` は spec.md 未定義であり、追加提案として記載されている（適切）
- **ADR-0005 Outbox パターン**: Outbox テーブル定義・BackgroundService 実装が ADR-0005 に準拠
- **ADR-0006 Database per Service**: `pointdb` として独立 DB を使用、他サービスの DB への直接参照なし
- **ADR-0009 Saga パターン**: gRPC インターフェース定義、冪等性設計が ADR-0009 の要件を満たす

### 記載カバレッジ分析
| セクション | カバレッジ | 評価 |
|-----------|----------|------|
| 概要・スコープ | 完全 | ✅ |
| 技術スタック | 完全 | ✅ |
| コンポーネントアーキテクチャ | 完全 | ✅ Mermaid 図あり |
| データモデル（ER 図・テーブル定義） | 高い | ✅ CHECK 制約・FK 制約・インデックス定義あり |
| API 設計（REST + gRPC） | 高い | ✅ エンドポイント一覧・proto 定義・DTO 定義あり |
| ポイントライフサイクル | 高い | ⚠️ FIFO 消費ロジック未記載（H-01） |
| ティア連携 | 完全 | ✅ 責務分担が明確（C-04 対応済み） |
| イベント設計 | 完全 | ✅ Outbox パターン実装コード含む |
| キャッシュ戦略 | 完全 | ✅ Redis キー設計・TTL・無効化ルール |
| セキュリティ | 高い | ⚠️ gRPC 認証設定が不完全（H-05） |
| テスト戦略 | 中程度 | ⚠️ 統合テスト不足（H-06） |
| Dockerfile | 完全 | ✅ マルチステージ・非 root ユーザー |
| Saga 冪等性 | 高い | ✅ 冪等性パターンとコード例あり |
| 楽観的ロック | 完全 | ✅ `[Timestamp]` + DbUpdateConcurrencyException ハンドリング |
| EF Core エンティティ定義 | 完全 | ✅ 全エンティティの C# 定義・AppDbContext 完備 |
| FluentValidation | 完全 | ✅ 主要リクエスト DTO のバリデーター定義 |
| Repository インターフェース | 完全 | ✅ Aggregate Root 単位の設計 |
| Service インターフェース | 完全 | ✅ 全サービスのインターフェース定義 |
| Endpoint 実装 | 完全 | ✅ Minimal API の完全実装パターン |
| Program.cs | 完全 | ✅ DI 登録・ミドルウェア順序が AGENTS.md §11.3 準拠 |
| 例外クラス | 完全 | ✅ エラーコード対応の階層構造 |
| 監視・メトリクス | 高い | ✅ カスタムメトリクス・ヘルスチェック・アラート条件 |
| データ保持ポリシー | なし | ❌ H-08 で指摘 |
| 監査ログ構造 | なし | ❌ H-09 で指摘 |

### 未定義・曖昧な領域
1. **FIFO ポイント消費ロジック** — 消費時の有効期限順序制御が未定義（H-01）
2. **ConfirmPoints の Saga ステップ位置付け** — spec.md に未定義の RPC（E-01）
3. **データ保持期間・アーカイブ戦略** — ポイント取引履歴の保持ポリシー未記載（H-08）
4. **管理者操作の監査ログテーブル** — 記録構造が未定義（H-09）
5. **ポイント消費上限のビジネスルール承認** — MaxRedeemPoints の妥当性（E-02）

## Medium/Low 指摘一覧（推奨改善事項）
| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| M-01 | Medium | business-analyst | ビジネスロジック | ポイント返却（`order.cancelled` / `payment.refunded` イベント購読時）の計算ロジックが §9.2 のイベント一覧に記載されているが、返却ポイント数の算出ルール（付与時のレート適用か、現在のレートか）が未定義 | §7 に「ポイント返却フロー」セクションを追加し、返却計算ルール（付与時のレートで計算）を明記 |
| M-02 | Medium | business-analyst | ビジネスロジック | プラチナ会員の「誕生月 2 倍ポイント」特典の実装設計が未記載。`PointCalculator` で誕生月判定を行うには UserManagementService から誕生日情報が必要だが、連携方法が未定義 | §8 ティアシステムに誕生月ポイント倍率の実装設計を追加（UserManagementService からの情報取得方法含む） |
| M-03 | Medium | architect | アーキテクチャ | `PointConversionRate` エンティティ （§5.1 ER 図・§5.2 テーブル定義）が存在するが、使用箇所が設計書内に見当たらない。`IPointConversionRateRepository` も Repository 一覧に含まれていない。Phase 1 スコープ外であれば明記が必要 | `PointConversionRate` の用途を明確化するか、Phase 1 スコープ外として注記を追加 |
| M-04 | Medium | programing-reviewer | コード品質 | §7.3 の `PointExpirationChecker` でイベントペイロードの `DateTime.UtcNow` が直接使用されている（`new PointsExpiredEvent(... DateTime.UtcNow)`）。AGENTS.md では `DateTime.UtcNow` の代わりに `timeProvider.GetUtcNow()` の使用を推奨しており、同クラスは既に `TimeProvider` を DI で受け取っているため不整合 | `DateTime.UtcNow` → `timeProvider.GetUtcNow().UtcDateTime` に修正 |
| M-05 | Medium | programing-reviewer | コード品質 | §9.4 Outbox Publisher の `SqlQueryRaw<bool>` 使用が AGENTS.md の「`FromSqlRaw` での文字列結合禁止」ルールに抵触する可能性がある。Advisory Lock のクエリはパラメータ化不要な固定文字列だが、コメントで安全性の根拠を明記すべき | `SqlQueryRaw` 使用箇所にコメントを追加: `// 固定文字列クエリ（パラメータなし）のため SQL インジェクションリスクなし` |
| M-06 | Medium | dba-reviewer | DB 設計 | `point_transactions` テーブルに `(reference_id, type)` の複合ユニーク制約が存在しない。§18.2 の冪等性チェックは `FirstOrDefaultAsync(t => t.ReferenceId == orderId && t.Type == "RESERVE")` で行われるが、DB レベルの一意制約がないためレースコンディションで重複レコードが挿入される可能性がある | `point_transactions` に `UNIQUE (reference_id, type) WHERE reference_id IS NOT NULL` の部分ユニーク制約を追加 |
| M-07 | Medium | dba-reviewer | DB 設計 | `tier_definitions` テーブルが §5.2 に定義されているが、§B の AppDbContext に `DbSet<TierDefinition>` が存在しない。リポジトリ `ITierDefinitionRepository` は §13 プロジェクト構成に含まれているが実装が不整合 | AppDbContext に `DbSet<TierDefinition>` を追加し、OnModelCreating に設定を追記 |
| M-08 | Medium | security-reviewer | セキュリティ | §6.4 の `AdjustPointsRequest` DTO で `Points` フィールドが `[Range(1, int.MaxValue)]` と定義されているが、FluentValidation (§C) では `MaxAdjustmentPoints = 100_000` の上限を設定している。Data Annotations と FluentValidation で上限が不一致 | DTO の Data Annotations 定義を `[Range(1, 100000)]` に修正するか、Data Annotations を削除して FluentValidation に統一 |
| M-09 | Medium | qa-manager | テスト | §15.1 の単体テストで `Should_ReturnIdempotentResult_When_DuplicateReserve` と `Should_ApplyCampaignMultiplier_When_ActiveCampaignExists` のテストボディが空（コメントのみ）。テスト設計書としてはアサーションの期待値まで記載すべき | 空テストケースにアサーション例を追加 |
| M-10 | Medium | performance-reviewer | パフォーマンス | ポイント残高キャッシュ（`points:balance:{userId}`) の TTL が 5 分に設定されているが、ポイント変動（付与・消費）時のキャッシュ無効化処理が §10.2 に記載されているものの、§18.2 の冪等性実装コード内でキャッシュ無効化が呼び出されていない | §18.2 の ReservePointsAsync / AwardPoints 処理完了後に `InvalidateBalanceCacheAsync` 呼び出しを追記 |
| M-11 | Medium | release-manager | リリース | §17 Dockerfile の `EXPOSE 5007` は開発用ポート。AGENTS.md の Dockerfile 規約では `EXPOSE 8080` が推奨される（本番では `ASPNETCORE_URLS` で制御）。他サービスとのポート統一方針が不明確 | `EXPOSE 8080` に統一し、`ENV ASPNETCORE_URLS=http://+:8080` を追加。サービス情報テーブル（§3）のポート番号との整合性を確認 |
| M-12 | Medium | infra-ops-reviewer | 運用 | §7.3 の `PointExpirationChecker` が `Task.Delay(TimeSpan.FromHours(24))` で日次実行を制御しているが、定時実行（例: 毎日 AM 3:00 JST）の要件が spec.md のポイント有効期限設計に暗黙的に存在する。`Task.Delay(24h)` では起動時刻に依存し正確な定時実行にならない | `TimeProvider` ベースで次回実行時刻を計算する設計（例: 次の AM 3:00 JST まで待機）を追記するか、外部スケジューラ（Kubernetes CronJob, Azure Container Apps Job）との連携を明記 |
| L-01 | Low | business-analyst | ドキュメント | §1.2 スコープで「ポイント分析レポート」が In Scope に含まれているが、具体的な分析項目・レポート形式が §14 の `PointAnalyticsResponse` 以外に詳細化されていない | §14 またはアナリティクスの別セクションに分析項目一覧（ティア別 KPI、月次推移等）を追記 |
| L-02 | Low | tech-lead | ドキュメント | §6.4 の DTO 順序が API セクション内で不統一（§6.3 内部 API → §6.5 gRPC → §6.6 認証 → §6.4 DTO）。読みやすさのため論理順に並べ替えが望ましい | §6 のサブセクション順序を 6.1 → 6.2 → 6.3 → 6.4 → 6.5 → 6.6 の論理順に整理 |
| L-03 | Low | programing-reviewer | コード品質 | §B の `SaveChangesAsync` オーバーライドで型チェックの `if/else if` 連鎖が長い。`IHasTimestamps` インターフェースパターンの導入で簡素化可能 | 実装時に `IHasTimestamps` インターフェースの適用を検討（設計書への反映は任意） |
| L-04 | Low | dba-reviewer | DB 設計 | `point_conversion_rates` テーブルに `(from_currency, to_currency, effective_date)` の複合ユニーク制約がない。同一通貨ペア・同一有効開始日で複数レコードが挿入される可能性 | 複合ユニーク制約の追加を検討 |
| L-05 | Low | oss-reviewer | 依存関係 | `Microsoft.AspNetCore.Authentication.JwtBearer` パッケージが §2 の主要ライブラリ一覧に含まれていないが、§G の Program.cs で `AddJwtBearer` を使用している | ライブラリ一覧に `Microsoft.AspNetCore.Authentication.JwtBearer 10.*` を追加 |
| L-06 | Low | compliance-reviewer | データ保護 | §16 の `appsettings.json` に Kafka `BootstrapServers` がハードコードされている（`localhost:9092`）。開発用であっても接続情報は `appsettings.Development.json` に分離が AGENTS.md ベストプラクティス | BootstrapServers を `appsettings.Development.json` に移動し、本番は環境変数で参照 |
| L-07 | Low | infra-ops-reviewer | 運用 | §17 Dockerfile で `curl` コマンドによる HEALTHCHECK が定義されているが、`aspnet` ランタイムイメージには `curl` が含まれていない場合がある | `wget` または `/health` エンドポイントへの .NET 組み込みヘルスチェックを使用 |
| L-08 | Low | ux-accessibility-reviewer | UX | ポイント残高表示の単位・フォーマットが設計書に定義されていない（例: 「1,000 ポイント」「1,000 pt」「1000P」）。フロントエンド実装時に表記ゆれが発生するリスク | §6.4 レスポンス DTO にポイント表示フォーマット仕様を付記（例: カンマ区切り + 単位「ポイント」） |

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### business-analyst — ビジネス要件・ユーザーストーリー検証

**対象**: `design-docs/point-service-design.md`

#### 評価結果: ⚠️ Conditional

**良好な点**:
- ポイントライフサイクル（付与→消費→失効）のフロー図が Mermaid で可視化されている
- ティア定義（BRONZE/SILVER/GOLD/PLATINUM）が spec.md と完全に一致
- ポイント計算ロジックがコード例付きで具体的に設計されている
- 管理者向け機能（手動調整、分析レポート）が API として定義されている

**指摘事項**:
1. **[High] H-01**: FIFO ポイント消費ロジック未記載（上記参照）
2. **[Medium] M-01**: ポイント返却計算ルール未定義
3. **[Medium] M-02**: プラチナ誕生月 2 倍ポイントの実装設計未記載
4. **[Low] L-01**: ポイント分析レポートの詳細項目未記載

</details>

<details>
<summary>architect レビューレポート</summary>

### architect — アーキテクチャ・DDD パターン検証

**対象**: `design-docs/point-service-design.md`

#### 評価結果: ⚠️ Conditional

**良好な点**:
- Aggregate Root（PointAccount）が spec.md と一致し、子エンティティ（PointTransaction, PointExpiry）との関係が明確
- Repository パターンが Aggregate Root 単位で正しく定義されている
- Outbox パターンが ADR-0005 に準拠し、BackgroundService 実装まで詳細化されている
- サービス間の責務分担（特にティアシステムの UserManagementService 委任）が明確
- コンポーネントアーキテクチャ図が詳細かつ正確

**指摘事項**:
1. **[High] H-02**: Saga ステップ番号の不整合（上記参照）
2. **[High] H-03**: gRPC proto 定義の不整合（上記参照）
3. **[Medium] M-03**: PointConversionRate の使用箇所不明

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### tech-lead — 技術標準・規約遵守の横断検証

**対象**: `design-docs/point-service-design.md`

#### 評価結果: ⚠️ Conditional

**良好な点**:
- AGENTS.md のコーディング規約（primary constructor、CancellationToken、ILogger<T> メッセージテンプレート）が全コード例で遵守されている
- ミドルウェアパイプライン順序が AGENTS.md §11.3 に完全準拠
- DI 登録が Scoped で統一され、BackgroundService は IServiceScopeFactory パターンを使用
- 例外処理規約（例外クラス階層 → HTTP ステータスコードマッピング）が AGENTS.md §4.7 に準拠
- EF Core エンティティが `[Table]`、`[Column]` 属性で snake_case 命名を適用

**指摘事項**:
1. **[High] H-04**: PointExpiry の status CHECK 制約の不整合（上記参照）
2. **[Medium] M-04 (→ programing-reviewer に統合)**: 上記 M-04 と同一
3. **[Low] L-02**: セクション順序の不統一

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### programing-reviewer — C# 14 / .NET 10 コード品質検証

**対象**: `design-docs/point-service-design.md`

#### 評価結果: ✅ Approved with Notes

**良好な点**:
- primary constructor が全 Service/Repository で使用されている
- record 型が DTO・イベントペイロードに適切に使用されている
- CancellationToken が全 async メソッドシグネチャに含まれ、下位呼び出しに伝搬されている
- `AsNoTracking()` が読み取り専用クエリに適用されている（Repository インターフェース設計から推測）
- コレクションナビゲーションが `= []` で初期化（C# 12+）
- `ILogger<T>` メッセージテンプレート形式が全ログ出力で使用されている

**指摘事項**:
1. **[Medium] M-04**: DateTime.UtcNow の直接使用（上記参照）
2. **[Medium] M-05**: SqlQueryRaw の安全性コメント不足（上記参照）
3. **[Low] L-03**: SaveChangesAsync の型チェック連鎖の簡素化提案

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### security-reviewer — OWASP Top 10・認証認可・秘密情報管理

**対象**: `design-docs/point-service-design.md`

#### 評価結果: ⚠️ Conditional

**良好な点**:
- IDOR 防止が §12.2 で `ClaimsPrincipal` からの userId 照合として明確に設計されている
- FallbackPolicy で全エンドポイントが認証必須（AllowAnonymous 明示除外方式）
- サービス間認証が Client Credentials Grant + JWT で設計されている
- セキュリティヘッダー（X-Content-Type-Options, X-Frame-Options, CSP）が適用されている
- 入力バリデーションが FluentValidation で実装されている
- 楽観的ロックでポイント残高の競合更新を防止

**指摘事項**:
1. **[High] H-05**: gRPC 認証ポリシー未適用（上記参照）
2. **[Medium] M-08**: Data Annotations と FluentValidation の上限不一致

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### dba-reviewer — DB スキーマ・EF Core・マイグレーション

**対象**: `design-docs/point-service-design.md`

#### 評価結果: ✅ Approved with Notes

**良好な点**:
- 全テーブルに CHECK 制約が適切に定義されている
- インデックス設計（部分インデックス `WHERE status = 'ACTIVE'` 含む）が充実
- FK 制約に ON DELETE / ON UPDATE が明示されている（point_expiries）
- `row_version` による楽観的ロックが PointAccount に設定
- AppDbContext の OnModelCreating で CHECK 制約・インデックスが EF Core Fluent API で定義
- SaveChangesAsync オーバーライドで CreatedAt/UpdatedAt の自動管理

**指摘事項**:
1. **[Medium] M-06**: 冪等性チェック用の DB レベルユニーク制約の欠落
2. **[Medium] M-07**: TierDefinition の DbSet 未定義
3. **[Low] L-04**: point_conversion_rates の複合ユニーク制約の欠落

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### qa-manager — テスト戦略・カバレッジ検証

**対象**: `design-docs/point-service-design.md`

#### 評価結果: ⚠️ Conditional

**良好な点**:
- テストメソッド命名が `Should_*_When_*` パターンに準拠
- AAA パターン（Arrange-Act-Assert）が使用されている
- NSubstitute + Shouldly の組み合わせが AGENTS.md 準拠
- Testcontainers.PostgreSql が統合テストで使用されている
- PointCalculator の Theory テスト（ランク別ポイント計算）が充実
- 冪等性テスト・楽観的ロックテストのテストクラスが設計されている

**指摘事項**:
1. **[High] H-06**: 統合テストカバレッジ不足（上記参照）
2. **[Medium] M-09**: 空テストケースの存在

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### performance-reviewer — パフォーマンス・スケーラビリティ検証

**対象**: `design-docs/point-service-design.md`

#### 評価結果: ⚠️ Conditional

**良好な点**:
- Redis キャッシュ戦略（残高 5 分、ランク 24 時間、ティア定義 1 時間）が適切
- キャッシュ無効化タイミングが明確
- Outbox Publisher の動的バックオフ（100ms〜5s）が AGENTS.md §10.4 準拠
- Advisory Lock によるバッチ処理の単一インスタンス制御
- バッチサイズ分割処理（ExpiryBatchSize）でメモリ使用量を制御
- gRPC 通信で SLO 80ms（ステップ 4/7）を満たす設計

**指摘事項**:
1. **[High] H-07**: 失効バッチ処理の N+1 クエリリスク（上記参照）
2. **[Medium] M-10**: キャッシュ無効化の呼び出し欠落

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### compliance-reviewer — GDPR・個人情報保護・PCI DSS

**対象**: `design-docs/point-service-design.md`

#### 評価結果: ⚠️ Conditional

**良好な点**:
- ポイント取引履歴に個人を直接特定する情報（メールアドレス等）が含まれない設計
- ユーザー削除イベント（`user.deleted`）の購読と対応が §9.2 に記載
- ログ出力にメッセージテンプレートを使用し、PII の直接出力を回避

**指摘事項**:
1. **[High] H-08**: データ保持期間・アーカイブ戦略の未定義（上記参照）
2. **[Low] L-06**: appsettings.json の接続情報ハードコード

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### oss-reviewer — NuGet パッケージ・ライセンス・脆弱性

**対象**: `design-docs/point-service-design.md`

#### 評価結果: ✅ Approved with Notes

**良好な点**:
- 全パッケージが AGENTS.md §8.1 の必須パッケージリストに含まれている
- 禁止パッケージ（Newtonsoft.Json, log4net, EntityFramework 6 等）が使用されていない
- プレリリース版パッケージが使用されていない
- `IHttpClientFactory` 経由の HTTP クライアント使用がコードで確認

**指摘事項**:
1. **[Low] L-05**: JwtBearer パッケージのライブラリ一覧への追記

</details>

<details>
<summary>release-manager レビューレポート</summary>

### release-manager — リリース戦略・バージョニング

**対象**: `design-docs/point-service-design.md`

#### 評価結果: ✅ Approved with Notes

**良好な点**:
- Dockerfile がマルチステージビルド・非 root ユーザーの規約に準拠
- HEALTHCHECK 指定あり
- `.NET Aspire` のサービス参照による サービスディスカバリが前提

**指摘事項**:
1. **[Medium] M-11**: Dockerfile の EXPOSE ポート統一

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### infra-ops-reviewer — コンテナ・可観測性・DR

**対象**: `design-docs/point-service-design.md`

#### 評価結果: ✅ Approved with Notes

**良好な点**:
- OpenTelemetry のトレーシング・メトリクスが Program.cs で設定
- カスタムメトリクス（points.awarded.total 等）が定義
- ヘルスチェック（Liveness + Readiness）が PostgreSQL / Redis を含む
- Correlation ID ミドルウェアが実装
- Advisory Lock によるバッチ処理の排他制御

**指摘事項**:
1. **[Medium] M-12**: 失効バッチ処理の定時実行設計の不足
2. **[Low] L-07**: Dockerfile HEALTHCHECK の curl 依存リスク

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### audit-reviewer — トレーサビリティ・監査証跡・ADR 整合性

**対象**: `design-docs/point-service-design.md`

#### 評価結果: ⚠️ Conditional

**良好な点**:
- Correlation ID が全イベントペイロードに含まれている
- Outbox パターンによるイベント発行の監査証跡（outbox_events テーブル）
- PointTransaction テーブルが全取引の完全な履歴を保持
- ADR-0005, ADR-0006, ADR-0009 への準拠が明示的に記載

**指摘事項**:
1. **[High] H-09**: 管理者操作の監査ログ構造未定義（上記参照）

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### ux-accessibility-reviewer — UX・WCAG 2.1 準拠

**対象**: `design-docs/point-service-design.md`

#### 評価結果: ✅ Approved with Notes

**良好な点**:
- API レスポンスが構造化された DTO で返却され、フロントエンド実装がしやすい設計
- ティア進捗情報（NextTierName, PointsToNextTier）がレスポンスに含まれ、ゲーミフィケーション UX を支援
- 失効予定ポイント一覧 API が提供されており、ユーザーへの事前通知 UX が実現可能

**指摘事項**:
1. **[Low] L-08**: ポイント表示フォーマットの未定義

</details>
