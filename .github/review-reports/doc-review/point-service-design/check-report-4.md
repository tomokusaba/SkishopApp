# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/point-service-design.md`
- **判定**: ✅ **Approved with Notes** — Critical/High 指摘なし。Medium/Low の推奨改善事項あり
- **レビュー日時**: 2026-04-03 18:30 (イテレーション 4)
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

## 段階的実行モード
- イテレーション: 4 回目
- 実行 Agent: 全 14 Agent（イテレーション 3 で全 Agent が Medium 以上の指摘を保有しており、Stable 該当なし）
- スキップ Agent（Stable）: なし
- 実行理由: 全 Agent Active（3 回連続ゼロの Agent が存在しない）

## 技術スタック検証結果
| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| 言語 | C# 14 (.NET 10) | C# 14 (.NET 10) | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| ORM | EF Core 10 | EF Core 10 | ✅ |
| DB | PostgreSQL (pointdb) | PostgreSQL | ✅ |
| メッセージング | Apache Kafka (Confluent.Kafka 2.*) | Apache Kafka (Confluent.Kafka 2.*) | ✅ |
| キャッシュ | Redis (StackExchange.Redis) | Redis (StackExchange.Redis 2.*) | ✅ |
| 認証 | JWT Bearer + InternalServiceOnly | ASP.NET Core Identity + Microsoft.Identity.Web | ✅ |
| バリデーション | FluentValidation 11.* | FluentValidation 11.* | ✅ |
| ロギング | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| 可観測性 | OpenTelemetry | OpenTelemetry | ✅ |
| 耐障害性 | Polly 8.* + Microsoft.Extensions.Http.Resilience 9.* | 同上 | ✅ |
| テスト | xUnit + NSubstitute + Shouldly + Testcontainers | 同上 | ✅ |
| コンテナ | Docker (aspnet:10.0) | Docker 25.x | ✅ |

## Iteration 3 → 4 の High 修正検証結果

| # | 修正項目 | 修正箇所 | 検証結果 |
|---|---------|---------|----------|
| H-01 | FIFO ポイント消費ロジック | §7.2.1 新設（フロー説明 + `ConsumePointsFifoAsync` 実装コード + 適用タイミング表） | ✅ **修正完了** |
| H-02 | Saga ステップ用語統一 | §18.1 に用語統一セクション追加（ステップ 4=ReservePoints / ステップ 7=AwardPoints / ConfirmPoints=ステップ 4 後処理） | ✅ **修正完了** |
| H-03 | gRPC サービス名統一 | §I proto: `service PointService` に統一。`option csharp_namespace` で C# 名前空間衝突を回避。ConfirmPoints/GetBalance を「spec.md 拡張」と明記 | ✅ **修正完了** |
| H-04 | point_expiries.status CHECK 制約 CONSUMED 追加 | §5.2 テーブル定義: `CHECK (status IN ('ACTIVE','EXPIRED','CONSUMED'))` に更新。§B AppDbContext: 同一 3 値。§A エンティティ: 同一 3 値 | ✅ **修正完了** |
| H-05 | gRPC InternalServiceOnly 認証 | §6.5 + §G Program.cs: `app.MapGrpcService<PointGrpcService>().RequireAuthorization("InternalServiceOnly")` 追加 | ✅ **修正完了** |
| H-06 | gRPC/Outbox/バッチ統合テスト | §15.3 新設（4 サブセクション: gRPC Saga 連携 4 件、Outbox 3 件、バッチ処理 3 件、MemberRank 2 件 = 計 12 件追加） | ✅ **修正完了** |
| H-07 | N+1 クエリ Eager Loading 修正 | §7.3: `.Include(e => e.Account)` を追加。コメントで Eager Loading による N+1 防止を明記 | ✅ **修正完了** |
| H-08 | データ保持期間・DSR 対応 | §19.1 新設（5 テーブルの保持期間表 + DSR 対応方針表 + `AnonymizeUserDataAsync` 実装コード） | ✅ **修正完了** |
| H-09 | point_audit_logs テーブル | §19.2 新設（テーブル定義 + インデックス + EF Core エンティティ + AppDbContext 追加 + `AdjustPointsAsync` 監査ログ記録コード） | ✅ **修正完了** |

**結論: 9 件の High 指摘全てが適切に修正されている。**

## 指摘サマリー
| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ✅ | 0 | 0 | 2 | 1 |
| architect | ✅ | 0 | 0 | 1 | 0 |
| tech-lead | ✅ | 0 | 0 | 2 | 1 |
| programing-reviewer | ✅ | 0 | 0 | 2 | 1 |
| security-reviewer | ✅ | 0 | 0 | 1 | 0 |
| dba-reviewer | ✅ | 0 | 0 | 3 | 1 |
| qa-manager | ✅ | 0 | 0 | 1 | 0 |
| performance-reviewer | ✅ | 0 | 0 | 1 | 0 |
| compliance-reviewer | ✅ | 0 | 0 | 1 | 1 |
| oss-reviewer | ✅ | 0 | 0 | 0 | 1 |
| release-manager | ✅ | 0 | 0 | 1 | 0 |
| infra-ops-reviewer | ✅ | 0 | 0 | 1 | 1 |
| audit-reviewer | ✅ | 0 | 0 | 1 | 0 |
| ux-accessibility-reviewer | ✅ | 0 | 0 | 0 | 1 |
| **合計** | | **0** | **0** | **17** | **8** |

## 判定根拠
- 判定ルール適用結果: Critical 0 件、High 0 件、Medium 17 件、Low 8 件 → ✅ **Approved with Notes**
- Iteration 3 の 9 件の High 指摘が全て適切に修正されたことを確認
- 残存する Medium/Low は実装フェーズで対応可能な推奨改善事項

## Critical/High 指摘一覧（修正必須）
| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| — | — | — | — | **Critical/High 指摘なし** | — |

## エスカレーション事項（要人間判断）
| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-01 | 高優先 | architect | （前回より継続）ConfirmPoints RPC の Saga ステップ内での位置付け。設計書では「ステップ 4 の後処理」として位置付けたが、spec.md への正式追加が必要かの判断 | テックリード |
| E-02 | 通常 | business-analyst | （前回より継続）ポイント消費の上限（`MaxRedeemPoints = 500,000`）の妥当性。spec.md に定義なし | PO / ビジネス担当 |

## 競合解決記録
| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| — | — | — | 今回の指摘では Agent 間の直接的な競合は検出されなかった | — | — |

## Medium/Low 指摘一覧（推奨改善事項）

### 継続 Medium（Iteration 3 から未修正）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| M-01 | Medium | business-analyst | ビジネスロジック | （継続）ポイント返却（`order.cancelled` / `payment.refunded` イベント購読時）の返却ポイント数算出ルール（付与時のレート適用か、現在のレートか）が未定義 | §7 に「ポイント返却フロー」セクションを追加し、返却計算ルール（付与時のレートで計算）を明記 |
| M-02 | Medium | business-analyst | ビジネスロジック | （継続）プラチナ会員の「誕生月 2 倍ポイント」特典の実装設計が未記載。`PointCalculator` で誕生月判定を行うには UserManagementService から誕生日情報が必要だが連携方法が未定義 | §8 ティアシステムに誕生月ポイント倍率の実装設計を追加 |
| M-03 | Medium | architect | アーキテクチャ | （継続）`PointConversionRate` エンティティが §5.1 ER 図・§5.2 テーブル定義に存在するが使用箇所がない。Phase 1 スコープ外であれば明記が必要 | `PointConversionRate` の用途を明確化するか、Phase 1 スコープ外として注記を追加 |
| M-05 | Medium | programing-reviewer | コード品質 | （継続）§9.4 OutboxPublisher および §7.3 PointExpirationChecker の `SqlQueryRaw` 使用箇所に安全性コメントがない。AGENTS.md の `FromSqlRaw` 禁止ルールとの関係を明記すべき | `SqlQueryRaw` 使用箇所に `// 固定文字列クエリ（パラメータなし）のため SQL インジェクションリスクなし` コメントを追加 |
| M-06 | Medium | dba-reviewer | DB 設計 | （継続）`point_transactions` テーブルに冪等性チェック用の `(reference_id, type)` DB レベルユニーク制約がない。§18.2 のアプリケーション層チェックのみではレースコンディションで重複レコードが挿入される可能性がある | `UNIQUE (reference_id, type) WHERE reference_id IS NOT NULL` 部分ユニーク制約を §5.2 および §B に追加 |
| M-07 | Medium | dba-reviewer | DB 設計 | （継続）`tier_definitions` テーブルが §5.2 に定義されているが、§B AppDbContext に `DbSet<TierDefinition>` が存在しない | AppDbContext に `DbSet<TierDefinition>` を追加し、OnModelCreating に設定を追記 |
| M-08 | Medium | security-reviewer | セキュリティ | （継続）§6.4 の `AdjustPointsRequest` DTO の `Points` が `[Range(1, int.MaxValue)]` だが、FluentValidation (§C) では `MaxAdjustmentPoints = 100_000` の上限。上限が不一致 | DTO の Data Annotations を `[Range(1, 100000)]` に修正するか、Data Annotations を削除して FluentValidation に統一 |
| M-09 | Medium | qa-manager | テスト | （継続）§15.1 の `Should_ApplyCampaignMultiplier_When_ActiveCampaignExists` および `Should_FloorPoints_When_FractionalResult` のテストボディが空（コメントのみ）。§K で別テストクラスが充実したが、§15.1 の空テストは未修正 | §15.1 の空テストケースにアサーション例を追加するか、§K での充実版に統合してリンクを付記 |
| M-10 | Medium | performance-reviewer | パフォーマンス | （継続）§18.2 ReservePointsAsync の処理完了後に `InvalidateBalanceCacheAsync` が呼び出されていない。キャッシュ残高と DB 残高が最大 5 分間不整合になる | §18.2 の `SaveChangesAsync` 後に `await _cacheService.InvalidateBalanceCacheAsync(request.UserId, ct)` を追記 |
| M-11 | Medium | release-manager | リリース | （継続）§17 Dockerfile の `EXPOSE 5007` は開発用ポート。AGENTS.md 規約では `EXPOSE 8080` + `ENV ASPNETCORE_URLS=http://+:8080` が推奨 | `EXPOSE 8080` に統一し、`ENV ASPNETCORE_URLS=http://+:8080` を追加 |
| M-12 | Medium | infra-ops-reviewer | 運用 | （継続）§7.3 の `PointExpirationChecker` が `Task.Delay(TimeSpan.FromHours(24))` で日次実行を制御。§J で `PointExpiryBackgroundService` が設定値ベースのインターバルを実装済みだが、§7.3 の元実装は未更新 | §7.3 の元実装を §J のリファクタリング版に統一するか、§7.3 → §J へのリファレンスを明記 |

### 新規 Medium

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| M-13 | Medium | tech-lead | 規約整合性 | **`point_transactions.type` CHECK 制約が §5.2 と §B AppDbContext で値が不一致**。§5.2 は 7 値（`EARN,REDEEM,EXPIRE,ADJUST,RESERVE,RELEASE,REFUND`）、§B は 8 値（上記 + `CANCEL`）。さらに §7.2 フローチャートで `type=SPEND`（消費確定）を使用しているが、`SPEND` はどちらの CHECK にも含まれていない | (1) §5.2 と §B の CHECK を統一する（CANCEL を追加するか削除するか決定）。(2) §7.2 の `type=SPEND` を CHECK 制約内の値に変更する（例: `REDEEM` またはCHECK に `SPEND` を追加） |
| M-14 | Medium | tech-lead | 実装整合性 | **§19.2 `AdjustPointsAsync` のシグネチャが §E `IPointService` インターフェースと不一致**。§19.2 の実装メソッドは `HttpContext httpContext` パラメータを含み IP アドレスを取得するが、§E のインターフェースには `HttpContext` がなく `string adminUserId` のみ。Endpoint (§F) のハンドラーも `httpContext` を渡していない | (1) 監査ログの IP アドレスは Endpoint 層で取得し、Service メソッドに `string ipAddress` パラメータとして渡す設計に変更。(2) §E インターフェース・§F Endpoint・§19.2 実装を整合させる |
| M-15 | Medium | programing-reviewer | コード品質 | **`DateTime.UtcNow` の直接使用が複数箇所に残存**（M-04 未修正の再指摘）。§7.3 PointExpirationChecker の PointsExpiredEvent 生成（L784）、§9.4 OutboxPublisher の `ProcessedAt` 設定（L1075）、§J TierRecalculationNotifier（L3788, L3816）で `DateTime.UtcNow` を直接使用。`TimeProvider` が DI で注入済みのため不整合 | ビジネスロジック・BackgroundService 内の `DateTime.UtcNow` を全て `timeProvider.GetUtcNow().UtcDateTime` に置換。エンティティの初期値（`= DateTime.UtcNow`）は SaveChangesAsync オーバーライドで制御されるため対象外 |
| M-16 | Medium | compliance-reviewer | データ保護 | **§19.1 `AnonymizeUserDataAsync` で `point_expiries` テーブルの `user_id` 匿名化が漏れている**。`point_transactions.user_id` は匿名化しているが、`point_expiries.user_id` も個人データであり同様に匿名化が必要 | `AnonymizeUserDataAsync` に `point_expiries` の `user_id` 匿名化処理を追加 |
| M-17 | Medium | audit-reviewer | 監査証跡 | **§19.2 の `PointAuditLog` DbSet が §B AppDbContext の本文に未追加**。§19.2 で `// AppDbContext に追加` として別途記載されているが、§B の DbSet 一覧・OnModelCreating には含まれていない。実装時に見落とされるリスクがある | §B AppDbContext の DbSet 一覧に `PointAuditLogs` を追加し、OnModelCreating にも §19.2 の設定を統合する |

### 継続 Low

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| L-01 | Low | business-analyst | ドキュメント | （継続）§1.2 スコープの「ポイント分析レポート」の詳細項目未記載 | §14 に分析項目一覧を追記 |
| L-02 | Low | tech-lead | ドキュメント | （継続）§6 のサブセクション順序が不統一（6.3→6.5→6.6→6.4） | 論理順に並べ替え |
| L-03 | Low | programing-reviewer | コード品質 | （継続）§B の `SaveChangesAsync` の `if/else if` 型チェック連鎖。`IHasTimestamps` インターフェースで簡素化可能 | 実装時に検討 |
| L-04 | Low | dba-reviewer | DB 設計 | （継続）`point_conversion_rates` の `(from_currency, to_currency, effective_date)` 複合ユニーク制約なし | 制約の追加を検討 |
| L-05 | Low | oss-reviewer | 依存関係 | （継続）`Microsoft.AspNetCore.Authentication.JwtBearer` が §2 ライブラリ一覧に未記載 | ライブラリ一覧に追加 |
| L-06 | Low | compliance-reviewer | データ保護 | （継続）§16 `appsettings.json` に Kafka `BootstrapServers: localhost:9092` がハードコード | `appsettings.Development.json` に移動 |
| L-07 | Low | infra-ops-reviewer | 運用 | （継続）§17 Dockerfile の `curl` HEALTHCHECK。`aspnet` イメージに `curl` が含まれない場合がある | `wget` または .NET 組み込みヘルスチェックを使用 |
| L-08 | Low | ux-accessibility-reviewer | UX | （継続）ポイント残高表示の単位・フォーマット（「1,000 ポイント」等）が未定義 | レスポンス DTO にフォーマット仕様を付記 |

## ドキュメント横断分析

### サービス間整合性
- **spec.md Saga ステップとの整合性**: ✅ Saga ステップ 4（ReservePoints）/ ステップ 7（AwardPoints）が spec.md L1020-1023 と一致。ConfirmPoints は「ステップ 4 の後処理」として明確に位置付けられた（E-01 として継続管理）
- **gRPC proto 定義**: ✅ サービス名 `PointService` が spec.md L1076 と一致。`csharp_namespace` で C# 名前空間衝突を回避
- **UserManagementService 連携**: ✅ `member_rank.updated` Kafka イベントによるランク同期設計が spec.md の MemberRank 設計と整合
- **Kafka イベント整合性**: ✅ `point.earned`, `point.redeemed`, `point.expired` は spec.md と整合。`point.reserved`, `point.released` は spec.md 拡張として適切に注記
- **ADR-0005 Outbox パターン**: ✅ Outbox テーブル定義・BackgroundService 実装が ADR-0005 に準拠
- **ADR-0006 Database per Service**: ✅ `pointdb` として独立 DB を使用
- **ADR-0009 Saga パターン**: ✅ gRPC インターフェース定義・冪等性設計が ADR-0009 準拠。FIFO 消費ロジック追加により設計の完全性が向上

### 記載カバレッジ分析
| セクション | カバレッジ | 前回→今回 |
|-----------|----------|-----------|
| 概要・スコープ | 完全 | ✅ → ✅ |
| 技術スタック | 完全 | ✅ → ✅ |
| コンポーネントアーキテクチャ | 完全 | ✅ → ✅ |
| データモデル（ER 図・テーブル定義） | 高い | ✅ → ✅（type CHECK 不整合あり M-13） |
| API 設計（REST + gRPC） | 完全 | ✅ → ✅ gRPC サービス名統一済み |
| ポイントライフサイクル | 完全 | ⚠️ → ✅ FIFO 消費ロジック追加（H-01 修正） |
| ティア連携 | 完全 | ✅ → ✅ |
| イベント設計 | 完全 | ✅ → ✅ |
| キャッシュ戦略 | 完全 | ✅ → ✅ |
| セキュリティ | 完全 | ⚠️ → ✅ gRPC 認証設定追加（H-05 修正） |
| テスト戦略 | 高い | ⚠️ → ✅ 統合テスト 12 件追加（H-06 修正） |
| Dockerfile | 高い | ✅ → ✅（EXPOSE ポート問題は M-11） |
| Saga 冪等性 | 完全 | ✅ → ✅ 用語統一済み |
| 楽観的ロック | 完全 | ✅ → ✅ |
| EF Core エンティティ定義 | 完全 | ✅ → ✅ |
| FluentValidation | 完全 | ✅ → ✅ |
| Repository インターフェース | 完全 | ✅ → ✅ |
| Service インターフェース | 高い | ✅ → ✅（AdjustPointsAsync シグネチャ M-14） |
| Endpoint 実装 | 完全 | ✅ → ✅ |
| Program.cs | 完全 | ✅ → ✅ |
| 例外クラス | 完全 | ✅ → ✅ |
| 監視・メトリクス | 高い | ✅ → ✅ |
| データ保持ポリシー | 完全 | ❌ → ✅ §19.1 追加（H-08 修正） |
| 監査ログ構造 | 高い | ❌ → ✅ §19.2 追加（H-09 修正。§B 未統合 M-17） |

### Iteration 3 → 4 の改善サマリ

| 指標 | Iteration 3 | Iteration 4 | 差分 |
|------|------------|------------|------|
| Critical | 0 | 0 | — |
| High | **9** | **0** | **▼9 (全件修正)** |
| Medium | 13 | 17 | ▲4（12 件継続 + 5 件新規） |
| Low | 8 | 8 | — |
| 判定 | ⚠️ Conditional Approval | ✅ Approved with Notes | **昇格** |

### 未定義・曖昧な領域（実装フェーズで解決可能）
1. **ConfirmPoints の Saga ステップ位置付け** — spec.md への追加可否（E-01）
2. **ポイント消費上限のビジネスルール承認** — MaxRedeemPoints の妥当性（E-02）
3. **PointConversionRate の用途** — Phase 1 スコープ外の可能性（M-03）
4. **point_transactions.type CHECK 統一** — SPEND / CANCEL の位置付け（M-13）

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### business-analyst — ビジネス要件・ユーザーストーリー検証

**対象**: `design-docs/point-service-design.md`（Iteration 4）

#### 評価結果: ✅ Approved with Notes

**H-01 修正検証**: ✅ FIFO ポイント消費ロジックが §7.2.1 に詳細に追加された。フロー説明、`ConsumePointsFifoAsync` 実装コード、適用タイミング表の 3 点が揃っており、spec.md §H8-24 の「期限延長なし（FIFO 消費）」要件を満たす。

**良好な点**:
- FIFO 消費の適用/非適用が操作ごとに明確に定義された
- 部分消費ロジック（ポイント減算 + ACTIVE 維持）が実装されている
- ティア定義が spec.md と完全に一致（継続）

**残存指摘**:
1. **[Medium] M-01**: ポイント返却計算ルール未定義（継続）
2. **[Medium] M-02**: 誕生月 2 倍ポイントの実装設計未記載（継続）
3. **[Low] L-01**: ポイント分析レポートの詳細項目未記載（継続）

</details>

<details>
<summary>architect レビューレポート</summary>

### architect — アーキテクチャ・DDD パターン検証

**対象**: `design-docs/point-service-design.md`（Iteration 4）

#### 評価結果: ✅ Approved with Notes

**H-02 修正検証**: ✅ §18.1 の用語統一セクションが追加され、Saga ステップ 4=ReservePoints / ステップ 7=AwardPoints / ConfirmPoints=ステップ 4 後処理として明確化された。spec.md L1020-1023 との整合性が確保された。

**H-03 修正検証**: ✅ §I の proto 定義で `service PointService` に統一され、spec.md L1076 と一致。`option csharp_namespace` による C# 名前空間衝突回避も適切。ConfirmPoints/GetBalance が「spec.md 拡張」と明記された。

**良好な点**:
- Aggregate Root（PointAccount）の設計が DDD 原則に準拠（継続）
- FIFO 消費ロジックの追加により、ポイント有効期限管理の設計が完全化
- gRPC + REST 二重公開方針が spec.md と整合

**残存指摘**:
1. **[Medium] M-03**: PointConversionRate の使用箇所不明（継続）

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### tech-lead — 技術標準・規約遵守の横断検証

**対象**: `design-docs/point-service-design.md`（Iteration 4）

#### 評価結果: ✅ Approved with Notes

**H-04 修正検証**: ✅ point_expiries の status CHECK 制約が §5.2 テーブル定義・§A エンティティ・§B AppDbContext の 3 箇所で `('ACTIVE','EXPIRED','CONSUMED')` に統一された。

**良好な点**:
- AGENTS.md コーディング規約（primary constructor、CancellationToken、ILogger メッセージテンプレート）が全コード例で遵守されている（継続）
- ミドルウェアパイプライン順序が AGENTS.md §11.3 に準拠（継続）
- H-01〜H-09 の修正が設計書全体の整合性を向上させた

**新規指摘**:
1. **[Medium] M-13**: `point_transactions.type` CHECK 制約が §5.2（7 値）と §B AppDbContext（8 値: +CANCEL）で不一致。さらに §7.2 フローチャートの `type=SPEND` がどちらの CHECK にも含まれていない
2. **[Medium] M-14**: §19.2 `AdjustPointsAsync` メソッドのシグネチャ（`HttpContext httpContext` パラメータ含む）が §E `IPointService` インターフェースと不一致
3. **[Low] L-02**: セクション順序の不統一（継続）

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### programing-reviewer — C# 14 / .NET 10 コード品質検証

**対象**: `design-docs/point-service-design.md`（Iteration 4）

#### 評価結果: ✅ Approved with Notes

**良好な点**:
- primary constructor が全 Service/Repository で使用されている（継続）
- CancellationToken が全 async メソッドに含まれている（継続）
- `AsNoTracking()` が読み取り専用クエリに適用（継続）
- FIFO 消費ロジック（§7.2.1）のコード例が C# 14 規約に準拠
- gRPC サービス実装（§I）が適切な CancellationToken 伝搬を実装

**残存・新規指摘**:
1. **[Medium] M-05**: SqlQueryRaw の安全性コメント不足（継続）
2. **[Medium] M-15**: `DateTime.UtcNow` の直接使用が §7.3（L784）、§9.4（L1075）、§J（L3788, L3816）に残存。`TimeProvider` が DI 済みなのに未使用
3. **[Low] L-03**: SaveChangesAsync の型チェック連鎖の簡素化提案（継続）

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### security-reviewer — OWASP Top 10・認証認可・秘密情報管理

**対象**: `design-docs/point-service-design.md`（Iteration 4）

#### 評価結果: ✅ Approved with Notes

**H-05 修正検証**: ✅ §6.5 および §G Program.cs に `app.MapGrpcService<PointGrpcService>().RequireAuthorization("InternalServiceOnly")` が追加された。gRPC エンドポイントへのサービス間認証が適切に設定された。

**良好な点**:
- IDOR 防止が §12.2 で適切に設計されている（継続）
- FallbackPolicy による全エンドポイント認証必須（継続）
- セキュリティヘッダー適用（継続）
- gRPC 認証ポリシーが REST 内部 API と同等レベルで適用された

**残存指摘**:
1. **[Medium] M-08**: Data Annotations と FluentValidation の上限不一致（継続）

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### dba-reviewer — DB スキーマ・EF Core・マイグレーション

**対象**: `design-docs/point-service-design.md`（Iteration 4）

#### 評価結果: ✅ Approved with Notes

**H-04 修正検証**: ✅ point_expiries の CHECK 制約が全箇所で 3 値に統一された。

**H-09 修正検証**: ✅ `point_audit_logs` テーブルが §19.2 に追加され、適切な CHECK 制約・インデックス・EF Core OnModelCreating 設定が含まれている。

**良好な点**:
- Eager Loading（`.Include(e => e.Account)`）による N+1 対策（H-07 修正確認）
- インデックス設計（部分インデックス含む）が充実（継続）
- 監査ログテーブルの設計が監査要件を満たす

**残存指摘**:
1. **[Medium] M-06**: 冪等性チェック用の DB レベルユニーク制約の欠落（継続）
2. **[Medium] M-07**: TierDefinition の DbSet が §B AppDbContext に未定義（継続）
3. **[Medium] M-17（新規）**: PointAuditLog の DbSet・OnModelCreating が §19.2 にのみ記載され §B 本体に未統合
4. **[Low] L-04**: point_conversion_rates の複合ユニーク制約の欠落（継続）

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### qa-manager — テスト戦略・カバレッジ検証

**対象**: `design-docs/point-service-design.md`（Iteration 4）

#### 評価結果: ✅ Approved with Notes

**H-06 修正検証**: ✅ §15.3 に 4 カテゴリ・計 12 件の統合テストが追加された。gRPC Saga 連携（4 件）、Outbox Publisher（3 件）、PointExpirationChecker バッチ処理（3 件）、MemberRankEventConsumer（2 件）がカバーされ、AGENTS.md §9.4 のカバレッジ 80% 目標に向けた設計が充実した。

**良好な点**:
- テストメソッド命名が `Should_*_When_*` パターンに準拠（継続）
- §K の PointServiceTests が AAA パターンで充実した実装例を提供
- FluentValidation テスト（AdjustPointsRequestValidatorTests, ReservePointsRequestValidatorTests）が追加
- Testcontainers.PostgreSql 使用の統合テスト設計が適切

**残存指摘**:
1. **[Medium] M-09**: §15.1 の空テストケース（`Should_ApplyCampaignMultiplier_When_ActiveCampaignExists` 等）が未修正（継続）

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### performance-reviewer — パフォーマンス・スケーラビリティ検証

**対象**: `design-docs/point-service-design.md`（Iteration 4）

#### 評価結果: ✅ Approved with Notes

**H-07 修正検証**: ✅ §7.3 PointExpirationChecker で `.Include(e => e.Account)` により N+1 クエリが解消された。コメントで Eager Loading の意図が明記されている。

**良好な点**:
- Redis キャッシュ戦略が適切（継続）
- Outbox Publisher の動的バックオフが実装（継続）
- Advisory Lock によるバッチ処理の排他制御（継続）
- バッチサイズ分割処理（ExpiryBatchSize）でメモリ使用量を制御（継続）

**残存指摘**:
1. **[Medium] M-10**: §18.2 ReservePointsAsync 処理完了後のキャッシュ無効化呼び出し欠落（継続）

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### compliance-reviewer — GDPR・個人情報保護・PCI DSS

**対象**: `design-docs/point-service-design.md`（Iteration 4）

#### 評価結果: ✅ Approved with Notes

**H-08 修正検証**: ✅ §19.1 にデータ保持期間・DSR 対応が追加された。5 テーブルの保持期間・アーカイブ戦略、DSR 3 種別の対応方針、`AnonymizeUserDataAsync` 実装コードが含まれる。

**良好な点**:
- `point_transactions` 7 年間保持（電子帳簿保存法準拠）が適切
- DSR 削除要求時の匿名化方針（SHA256 + salt）が技術的に適切
- `outbox_events` 90 日間保持の短期ポリシーが運用に即している

**新規指摘**:
1. **[Medium] M-16**: §19.1 `AnonymizeUserDataAsync` で `point_expiries.user_id` の匿名化が漏れている。`point_transactions.user_id` は処理しているが `point_expiries` が未対応
2. **[Low] L-06**: appsettings.json の Kafka BootstrapServers ハードコード（継続）

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### oss-reviewer — NuGet パッケージ・ライセンス・脆弱性

**対象**: `design-docs/point-service-design.md`（Iteration 4）

#### 評価結果: ✅ Approved with Notes

**良好な点**:
- 全パッケージが AGENTS.md §8.1 の必須パッケージリストに含まれている（継続）
- 禁止パッケージが使用されていない（継続）
- プレリリース版パッケージが使用されていない（継続）
- `IHttpClientFactory` 経由の HTTP クライアント使用（継続）

**残存指摘**:
1. **[Low] L-05**: JwtBearer パッケージのライブラリ一覧への追記（継続）

</details>

<details>
<summary>release-manager レビューレポート</summary>

### release-manager — リリース戦略・バージョニング

**対象**: `design-docs/point-service-design.md`（Iteration 4）

#### 評価結果: ✅ Approved with Notes

**良好な点**:
- Dockerfile がマルチステージビルド・非 root ユーザーの規約に準拠（継続）
- HEALTHCHECK 指定あり（継続）

**残存指摘**:
1. **[Medium] M-11**: Dockerfile の EXPOSE ポート統一（5007 → 8080）（継続）

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### infra-ops-reviewer — コンテナ・可観測性・DR

**対象**: `design-docs/point-service-design.md`（Iteration 4）

#### 評価結果: ✅ Approved with Notes

**良好な点**:
- OpenTelemetry トレーシング・メトリクスが設定済み（継続）
- カスタムメトリクスが定義されている（継続）
- ヘルスチェック（Liveness + Readiness）が実装（継続）
- Correlation ID ミドルウェアが実装（継続）
- §J に PointExpiryBackgroundService のリファクタリング版が追加（改善）

**残存指摘**:
1. **[Medium] M-12**: §7.3 の元実装と §J のリファクタリング版の二重定義（継続）
2. **[Low] L-07**: Dockerfile HEALTHCHECK の curl 依存リスク（継続）

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### audit-reviewer — トレーサビリティ・監査証跡・ADR 整合性

**対象**: `design-docs/point-service-design.md`（Iteration 4）

#### 評価結果: ✅ Approved with Notes

**H-09 修正検証**: ✅ §19.2 に `point_audit_logs` テーブル定義が追加された。admin_user_id、target_user_id、action（CHECK 制約付き）、points_before/after、reason（必須）、ip_address、user_agent、created_at の構成で監査要件を満たす。EF Core エンティティ定義と `AdjustPointsAsync` の実装パターンも提供されている。

**良好な点**:
- 監査ログの action が CHECK 制約で制限されている（ADJUST_ADD, ADJUST_SUBTRACT, MANUAL_EXPIRE, MANUAL_RESTORE）
- IP アドレスが IPv6 対応（VARCHAR(45)）
- インデックスが admin_user_id、target_user_id、created_at に設定

**新規指摘**:
1. **[Medium] M-17**: PointAuditLog の DbSet・OnModelCreating 設定が §B AppDbContext 本体に未統合。§19.2 に分離記載されており、実装時に見落とされるリスクがある

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### ux-accessibility-reviewer — UX・WCAG 2.1 準拠

**対象**: `design-docs/point-service-design.md`（Iteration 4）

#### 評価結果: ✅ Approved with Notes

**良好な点**:
- API レスポンスが構造化された DTO で返却されている（継続）
- ティア進捗情報（NextTierName, PointsToNextTier）が UX を支援（継続）
- 失効予定ポイント一覧 API が提供されている（継続）
- FIFO 消費ロジックの追加により、ポイントの消費順序がユーザーに説明可能になった（改善）

**残存指摘**:
1. **[Low] L-08**: ポイント表示フォーマットの未定義（継続）

</details>
