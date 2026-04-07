# ドキュメントレビュー統合レポート — mailsend-service-design.md

## 判定結果
- **対象**: `design-docs/mailsend-service-design.md`
- **判定**: ✅ **Approved with Notes** — Critical / High 指摘なし。前回の High 12 件は全て修正済み。Medium / Low の推奨改善事項あり
- **レビュー日時**: 2026-04-03 16:45
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)
- **イテレーション**: 4 回目（check-report-4）

## 段階的実行モード
- イテレーション: 4 回目
- 実行 Agent: 全 14 Agent（修正が 3 つ以上の Agent 管轄領域に同時影響する大規模修正のためリセット）
- スキップ Agent（Stable）: なし
- 実行理由: H-01〜H-12 の修正がデータモデル / GDPR / セキュリティ / DDD / API 設計と 5 領域以上に跨るため全量実行

## 技術スタック検証結果

| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| ランタイム | C# 14 / .NET 10 | C# 14 / .NET 10 | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| ORM | EF Core 10 | EF Core 10 | ✅ |
| DB | PostgreSQL (mailsenddb) | PostgreSQL (ADR-0006) | ✅ |
| メッセージング | Confluent.Kafka 2.* | Confluent.Kafka 2.* | ✅ |
| メールプロバイダ | Azure Communication Services Email | spec.md §11 記載: Azure CS | ✅ |
| テンプレートエンジン | Razor 10.* | spec.md §技術スタック: Razor | ✅ |
| バリデーション | FluentValidation 11.* | FluentValidation 11.* | ✅ |
| ログ | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| 可観測性 | OpenTelemetry 1.* | OpenTelemetry 1.* | ✅ |
| テスト | xUnit, NSubstitute, Shouldly, Testcontainers | xUnit, NSubstitute, Shouldly, Testcontainers | ✅ |

## 前回 High 指摘の修正確認

| # | 前回指摘 | 修正状況 | 確認箇所 |
|---|---------|---------|---------|
| H-01 | `mail_logs.status` CHECK 制約が spec.md と不一致 | ✅ 修正済み | §4.2: `BOUNCED` 追加。設計判断ノートで 6 値 (`PENDING`/`SENDING`/`SENT`/`FAILED`/`BOUNCED`/`SKIPPED`) の各差分理由を明記 |
| H-02 | `template_id` FK 欠落 | ✅ 修正済み | §4.2: `template_id VARCHAR(36) FK (mail_templates.id), ON DELETE RESTRICT` 追加。§21.1 Fluent API で FK 設定 |
| H-03 | `recipient_user_id` カラム欠落 | ✅ 修正済み | §4.2: `recipient_user_id VARCHAR(36)` 追加。EF Core エンティティ・インデックスも整備 |
| H-04 | `consent.revoked` イベント未対応 | ✅ 修正済み | §30.1: 処理フロー 5 ステップ + IMailService メソッド定義。§8.2 フローチャートにも反映。§28.1 SupportedEvents に追加 |
| H-05 | `user.deleted` PII 仮名化が Phase 4 先送り | ✅ 修正済み | §30.2: Phase 1 に前倒し。処理フロー 7 ステップ + `user.deletion.completed` イベント発行。§18 Phase 1 に GDPR イベント処理追加 |
| H-06 | `user.processing-restricted` イベント未対応 | ✅ 修正済み | §30.3: 処理制限 + 解除の双方向フロー定義。トランザクションメールは制限対象外の GDPR 第 18 条準拠明記 |
| H-07 | テンプレート CRUD API が §7.1 に未記載 | ✅ 修正済み | §7.1: GET/POST/PUT/DELETE `/admin/mail/templates` を追加。§26 の実装と整合 |
| H-08 | イベント名差分（`order.shipped` vs `shipment.status.updated`）未説明 | ✅ 修正済み | §6.1: 設計判断ノートで 3 つの理由を明記。Phase 4 先送りイベントのビジネスインパクト説明も追加 |
| H-09 | セキュリティヘッダー `Referrer-Policy` / `Permissions-Policy` 欠落 | ✅ 修正済み | §27: 5 ヘッダー明示 + `UseHsts()` で計 6 ヘッダー完備 |
| H-10 | DDD Aggregate Root 定義不足 | ✅ 修正済み | §31: MailLog / MailTemplate / MailSuppression の 3 Aggregate Root を明確に定義。Repository 対応・Aggregate 間ルールも記載 |
| H-11 | PII 保持期間不一致（設計書 90 日 vs spec.md 1 年） | ✅ 修正済み | §4.2: 1 年保持に統一。90 日経過後のアクセス制限強化も追記。§28.3: `-365` 日に修正 |
| H-12 | spec.md インデックス差分 | ✅ 修正済み | §4.2: `idx_mail_logs_template_id`, `idx_mail_logs_recipient_user_id` 追加。§21.1 Fluent API に反映 |

## 指摘サマリー

| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ✅ Pass | 0 | 0 | 1 | 0 |
| architect | ✅ Pass | 0 | 0 | 2 | 0 |
| programing-reviewer | ✅ Pass | 0 | 0 | 2 | 1 |
| dba-reviewer | ✅ Pass | 0 | 0 | 2 | 0 |
| security-reviewer | ✅ Pass | 0 | 0 | 2 | 0 |
| compliance-reviewer | ✅ Pass | 0 | 0 | 2 | 0 |
| audit-reviewer | ✅ Pass | 0 | 0 | 2 | 0 |
| qa-manager | ✅ Pass | 0 | 0 | 2 | 1 |
| performance-reviewer | ✅ Pass | 0 | 0 | 2 | 1 |
| infra-ops-reviewer | ✅ Pass | 0 | 0 | 1 | 1 |
| release-manager | ✅ Pass | 0 | 0 | 1 | 0 |
| oss-reviewer | ✅ Pass | 0 | 0 | 0 | 1 |
| ux-accessibility-reviewer | ✅ Pass | 0 | 0 | 1 | 1 |
| tech-lead | ✅ Pass | 0 | 0 | 1 | 0 |
| **合計** | | **0** | **0** | **21** | **6** |

## 判定根拠
- **Critical 指摘**: 0 件
- **High 指摘**: 0 件（前回 12 件から全件修正済み）
- **Medium 指摘**: 21 件（うち新規 2 件、前回からの残存 19 件）
- 判定ルール適用: Medium/Low のみ → ✅ **Approved with Notes**
- 最も重大な指摘: `mail_suppressions.reason` の競合リスク（`consent.revoked` と `user.processing-restricted` が同一 reason = 'UNSUBSCRIBE' を使用し、unrestrict 時に consent revocation が消失する可能性）

---

## Medium 指摘一覧（推奨改善事項）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 | 新規/残存 |
|---|--------|-----------|---------|----------|----------|----------|
| M-01 | Medium | compliance-reviewer, tech-lead | **GDPR ロジック競合** | §30.1 `consent.revoked` と §30.3 `user.processing-restricted` が共に `mail_suppressions` に `reason = 'UNSUBSCRIBE'` で登録する。UNIQUE 制約 `(email, reason)` により同一メールアドレスに対して 1 エントリしか持てないため、`user.processing-unrestricted` でエントリ削除時に consent revocation も消失する | `mail_suppressions.reason` の CHECK 制約に `'PROCESSING_RESTRICTED'` を追加し、処理制限には専用 reason を使用する。または、テーブルに `source_event` カラムを追加して区別する | **新規** |
| M-02 | Medium | dba-reviewer | インデックス差分 | spec.md の `mail_templates` インデックスは `(template_key, is_active) UNIQUE` だが、設計書は `name UNIQUE`（単独）。spec.md の定義では同一 `template_key` でも `is_active` が異なれば複数レコード可能だが、設計書では `name` の重複を一切許可しない。論理削除（`is_active = false`）を使用する設計（§7.1 DELETE）では、無効化後に同名テンプレートの再作成が不可能になる | spec.md の `(name, is_active)` 部分 UNIQUE（`WHERE is_active = true`）に変更するか、設計書の論理削除方針と `name UNIQUE` 制約の整合性を設計判断ノートで明記する | **新規** |
| M-03 | Medium | architect | サービス間通信 | §12.3 UserInfoResolver が Kafka イベント処理中に UserManagementService へ同期 HTTP 呼出しを行う。UserManagementService 障害時のメール送信可用性に影響するが、フォールバック戦略（Redis キャッシュ等）が未設計 | フォールバック戦略を明記する（例: Redis に直前のユーザー情報をキャッシュし、API 障害時に使用） | 残存 |
| M-04 | Medium | architect | 型安全性 | §28.1 `EventEnvelope.PayloadJson` が `string` 型で、ペイロードの二重デシリアライズが必要。`JsonElement` 型で受信し型別ディスパッチを行う方式が型安全 | `PayloadJson` を `JsonElement` 型に変更するか、設計判断としてstring 型を維持する理由を記載 | 残存 |
| M-05 | Medium | programing-reviewer | コード品質 | §28.1 `EventEnvelope.PayloadJson` の `string` 型受信は §12 の各イベントペイロード record への変換時に二重デシリアライズが発生する（M-04 と関連） | M-04 と同じ対応 | 残存 |
| M-06 | Medium | programing-reviewer | PII 保護 | §28.3 `PiiCleanupService.HashEmail()` がソルトなし SHA-256 を使用。レインボーテーブル攻撃で元のメールアドレスが推測可能。spec.md は「SHA-256 ハッシュ化」と記載しソルトを明示していないが、セキュリティベストプラクティスではソルト付きが推奨 | サービス固有の固定ソルト（環境変数管理）を追加するか、ソルトなしで許容する設計判断を明記 | 残存 |
| M-07 | Medium | security-reviewer | レート制限除外 | §9.5 のレート制限除外対象が「パスワードリセット、メール認証」のみ。`user.email_changed`（メールアドレス変更確認）もセキュリティ上重要であり除外対象に含めるべきか検討が必要 | `user.email_changed` を除外対象に追加するか、含めない理由を記載 | 残存 |
| M-08 | Medium | security-reviewer | レート制限実装 | §7.1 テストメール API の「1 時間 5 通」レート制限の実装方法が不明確。Endpoint レベル `RateLimiter` または Service 層での制御か | 実装方式を明記（ASP.NET Core `RateLimiter` ミドルウェア or Service 層の手動チェック） | 残存 |
| M-09 | Medium | compliance-reviewer | 外部プロセッサー DSR | spec.md §外部プロセッサー DSR 設計で SendGrid への DSR 伝搬が MailSendService の責務と定義されているが、設計書は Azure Communication Services を使用。ACS のデータ削除・DSR 対応方法が未記載 | Azure Communication Services の DSR 対応方針（API によるデータ削除 or Microsoft Azure DPA に基づく自動対応）を明記 | 残存 |
| M-10 | Medium | compliance-reviewer | GDPR 設計整合性 | §30.1 / §30.3 で UserManagementService への API 呼出しが必要（userId → email 取得）。しかし `user.deleted` イベント処理（§30.2）では userId のみで `mail_logs.recipient_user_id` を検索し email 取得不要。`consent.revoked` / `processing-restricted` で API が利用不可の場合の代替手段を検討すべき | consent.revoked / processing-restricted のペイロードに email を含めるか、UserManagementService 障害時のフォールバックを定義 | 残存 |
| M-11 | Medium | audit-reviewer | 監査ログ | 管理者操作（テンプレート CRUD、手動リトライ、テストメール送信）の監査ログ出力が未設計。管理 API の操作証跡（誰が・いつ・何を）の記録方式を定義すべき | `ILogger` によるメッセージテンプレート形式の監査ログ出力か、AuditLog テーブルでの永続化を追加 | 残存 |
| M-12 | Medium | audit-reviewer | メトリクスタグ | §14.1 `mail.event.consumed.total` のタグに `producer` の追加を推奨。どのサービスからのイベントかを区別可能にする | `producer` タグを追加 | 残存 |
| M-13 | Medium | qa-manager | テスト不足 | §17.1 の単体テスト計画に `PiiCleanupService` のテストが含まれていない。GDPR 要件に直結する匿名化ロジックはテスト必須 | `PiiCleanupService` のテストケースを §17.1 に追加（365 日経過レコードの匿名化、HashEmail 出力形式の検証） | 残存 |
| M-14 | Medium | qa-manager | テストケース不足 | §17.3 異常系テストに §30 の GDPR イベント処理関連テストケースが未追加（consent.revoked 処理、user.deleted PII 仮名化、processing-restricted マーケティング停止） | §30 の各イベント処理の正常系 + 異常系テストケースを §17.3 に追加 | 残存 |
| M-15 | Medium | performance-reviewer | DB 負荷 | §9.5 のレート制限チェックが毎回 `mail_logs` テーブルに COUNT クエリを発行。高頻度イベント処理時に DB 負荷が懸念される | Redis カウンター（TTL 1 時間）による最適化を検討 | 残存 |
| M-16 | Medium | performance-reviewer | 二重リトライ | §28.2 `MailRetryService` に PostgreSQL Advisory Lock が未適用。spec.md §BackgroundService リーダー選出パターンで推奨。複数レプリカ稼働時の二重リトライリスク | Advisory Lock によるリーダー選出パターンを追加 | 残存 |
| M-17 | Medium | infra-ops-reviewer | ヘルスチェック | §14.2 に「Azure Communication Services エンドポイント接続チェック」が記載されているが、§27 Program.cs の HealthChecks 登録に ACS チェックが含まれていない | `AddUrlGroup()` 等による ACS エンドポイント到達性チェックを §27 に追加 | 残存 |
| M-18 | Medium | release-manager | リリースゲート | §18 の Phase 分割は明確だが、各 Phase のリリース条件（Gate 基準）が未定義。Phase 1 リリース時の最小限の受入基準を明記推奨 | Phase 1 Gate 基準（必須テスト通過、GDPR イベント処理動作確認、ACS 接続確認等）を追加 | 残存 |
| M-19 | Medium | ux-accessibility-reviewer | アクセシビリティ | §6.2 のメールテンプレートに WCAG 2.1 Level A 準拠要件が未定義（画像 alt 属性、セマンティック HTML、テーブルレイアウトの aria 属性、color contrast ratio） | テンプレートガイドラインに WCAG 2.1 Level A 準拠項目を追加 | 残存 |
| M-20 | Medium | dba-reviewer | DRY 原則 | §21.1 `SaveChangesAsync` オーバーライド内のエンティティ型チェックが `if-else if` チェーン（4 型）。共通インターフェース `IAuditableEntity`（`CreatedAt`, `UpdatedAt` プロパティ）の導入で DRY 化可能 | `IAuditableEntity` インターフェースを追加し、`SaveChangesAsync` を簡潔化 | 残存 |
| M-21 | Medium | business-analyst | spec.md API 差分 | §7.1 の設計書 API が spec.md MailSendService API エンドポイント概要より 4 エンドポイント多い（POST templates, DELETE templates, POST retry, GET stats）。spec.md 側の更新が必要 | spec.md の MailSendService API 一覧を設計書と同期するか、設計書の「spec.md 側更新推奨」ノートを追加 | 残存 |

## Low 指摘一覧

| # | 出典 Agent | 指摘内容 |
|---|-----------|----------|
| L-01 | programing-reviewer | §22.1 `RateLimitExceededException` / `SuppressedRecipientException` のコンストラクタが `recipientEmail` を直接受け取り、メッセージに含めている。PII ログ出力リスク。ハッシュ値のみ受け取る設計を推奨 |
| L-02 | qa-manager | §17.2「Kafka イベント消費はインメモリテスト用プロバイダで検証」の具体的なテスト用プロバイダ実装方針が不明確 |
| L-03 | performance-reviewer | §28.3 `PiiCleanupService` のバッチサイズ（500 件）処理後の残存レコードへのページング処理が不明確 |
| L-04 | infra-ops-reviewer | §16 Docker Compose `profiles: [app]` のデフォルトプロファイルでの起動方法が不明確 |
| L-05 | oss-reviewer | §2 `Azure.Communication.Email` のバージョンが `1.*` と記載。具体的な安定版バージョンの確認・CVE チェックを推奨 |
| L-06 | ux-accessibility-reviewer | §6.2 テンプレートに多言語対応（日英）の設計が含まれていない。spec.md §ビジネス要件「多言語対応（日本語・英語）」への対応方針（ロケール別テンプレート or 動的切替）を記載推奨 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-01 | 通常 | business-analyst | カート放棄メール（`cart.abandoned` イベント）が spec.md §カート放棄メール設計に記載されているが、設計書では未対応。Phase 1 スコープに含めるか判断が必要 | PO |
| E-02 | 通常 | performance-reviewer | `mail_logs` テーブルでレート制限チェックを毎回実行しているが、高頻度送信時のクエリ負荷が懸念。Redis キャッシュによる最適化要否の判断 | テックリード |
| E-03 | 通常 | compliance-reviewer | spec.md §外部プロセッサー DSR 設計で SendGrid への DSR 伝搬が定義されているが、設計書は Azure Communication Services を使用。外部プロセッサー DSR 対応の方針確認が必要 | テックリード + 法務チーム |

---

## 競合解決記録

前回の競合（dba-reviewer vs architect の FK 方式、compliance-reviewer vs security-reviewer の PII 保持期間）は Iteration 3 で解決済み。Iteration 4 では新たな競合は検出されなかった。

---

## ドキュメント横断分析

### spec.md 定義項目との整合性チェック

| spec.md 定義項目 | 設計書対応 | 整合性 | 備考 |
|----------------|-----------|--------|------|
| MailTemplate エンティティ定義 | §4 / §21.2 | ✅ 一致 | 属性・制約とも合致。`variables` JSONB 定義あり |
| MailLog エンティティ定義 | §4 / §21.1 | ✅ 一致 | `templateId` FK 追加済み、`recipientUserId` 追加済み、`status` 差分は設計判断ノートで明記 |
| MailSuppression エンティティ定義 | §4 / §21.2 | ✅ 一致 | 属性・制約とも合致。`(email, reason)` UNIQUE |
| MailLog → MailTemplate FK | §4 / §21.1 | ✅ 一致 | ON DELETE RESTRICT, ON UPDATE CASCADE |
| テンプレート管理 API | §7.1 / §26 | ✅ 一致 | GET/POST/PUT/DELETE all covered |
| 送信ログ API | §7.1 / §26 | ✅ 一致 | GET/POST(retry)/GET(stats) |
| テストメール API | §7.1 / §26 | ✅ 一致 | `POST /admin/mail/test` |
| リトライ制御（指数バックオフ最大 3 回） | §8.4 | ✅ 一致 | 30s → 60s → 120s |
| 配信頻度制限（1 時間 5 通） | §9.5 | ✅ 一致 | セキュリティメール除外あり |
| Kafka Consumer (BackgroundService) | §8 / §28.1 | ✅ 一致 | IServiceScopeFactory 使用、stoppingToken 伝搬 |
| Outbox パターン | §12.4 | ✅ 一致 | ADR-0005 準拠 |
| PII 保持期間（1 年） | §4.2 / §28.3 | ✅ 一致 | spec.md「メール送信ログ: 1 年」に準拠 |
| `consent.revoked` イベント処理 | §30.1 | ✅ 一致 | マーケティング配信停止 + PENDING メールのキャンセル |
| `user.deleted` イベント処理 | §30.2 | ✅ 一致 | PII 仮名化（SHA-256）+ `user.deletion.completed` 発行 |
| `user.processing-restricted` イベント処理 | §30.3 | ✅ 一致 | マーケティング配信停止、トランザクションメールは制限対象外 |
| Aggregate Root（MailTemplate） | §31 | ✅ 一致 | spec.md 定義 + MailLog / MailSuppression も Aggregate Root として追加定義 |
| インデックス設計 | §4.2 / §21.1 | ⚠️ 部分一致 | `recipient_user_id`, `(status, created_at)`, `template_id` は一致。`mail_templates` のインデックスが `(template_key, is_active) UNIQUE` vs `name UNIQUE` で差分あり (M-02) |

### 記載カバレッジ分析

| 設計領域 | カバレッジ | 評価 |
|---------|----------|------|
| 概要・スコープ | §1 | ✅ 十分 |
| 技術スタック | §2 | ✅ 十分 |
| アーキテクチャ図 | §3 | ✅ 十分（Mermaid 図複数あり） |
| データモデル | §4 / §21 | ✅ 十分（spec.md 整合性修正済み） |
| API 設計 | §7 / §26 | ✅ 十分（テンプレート CRUD 追加済み） |
| イベント消費設計 | §8 / §12 / §28.1 | ✅ 十分（GDPR イベント追加済み） |
| Azure ACS 連携 | §9 | ✅ 十分 |
| プロジェクト構成 | §10 | ✅ 十分 |
| 設定ファイル | §11 | ✅ 十分 |
| イベントペイロード | §12 | ✅ 詳細に記載 |
| セキュリティ | §13 / §27 | ✅ 十分（ヘッダー修正済み） |
| 監視・運用 | §14 | ✅ 十分 |
| テスト戦略 | §17 | ⚠️ GDPR テストケース追加推奨 (M-13, M-14) |
| 既存サービス変更 | §19 | ✅ 詳細に記載 |
| エラーハンドリング | §22 | ✅ 十分 |
| Repository / Service IF | §23 / §24 | ✅ 十分（GDPR メソッド追加済み） |
| FluentValidation | §25 | ✅ 十分 |
| Endpoint 実装 | §26 | ✅ 十分 |
| Program.cs | §27 | ✅ 十分（セキュリティヘッダー修正済み） |
| BackgroundService | §28 | ✅ 十分 |
| ミドルウェア順序 | §29 | ✅ AGENTS.md 準拠 |
| GDPR イベント対応 | §30 | ✅ **新規追加**（Phase 1 必須明確化） |
| DDD Aggregate Root | §31 | ✅ **新規追加** |

### Iteration 3 → 4 品質改善サマリ

| 指標 | Iteration 3 | Iteration 4 | 改善 |
|------|------------|------------|------|
| Critical | 0 | 0 | — |
| High | **12** | **0** | ✅ -12 件 |
| Medium | 19 | 21 | +2 件（新規発見） |
| Low | 6 | 6 | ±0 |
| 判定 | ⚠️ Conditional Approval | ✅ **Approved with Notes** | ✅ 昇格 |
| spec.md 整合性 | ❌ 複数箇所不一致 | ✅ 全主要項目一致 | ✅ 改善 |
| GDPR 対応 | ❌ 3 イベント未対応 | ✅ 全 3 イベント設計完了 | ✅ 改善 |
| DDD 整合性 | ❌ Aggregate Root 未定義 | ✅ 3 Root 定義済み | ✅ 改善 |

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### 観点: ビジネス要件の完全性・ユーザーストーリー・受入基準

#### 前回 High の修正確認
- **H-07**: §7.1 にテンプレート管理 CRUD API（GET/POST/PUT/DELETE）が追加済み。§26 の実装と整合。✅
- **H-08**: §6.1 にイベント名差分の設計判断ノートが追加。Phase 4 先送りイベントのビジネスインパクト説明も追加。✅

#### Medium 指摘
1. **M-21**: 設計書の API が spec.md より 4 エンドポイント多い（POST templates, DELETE templates, POST retry, GET stats）。spec.md 側の更新が必要

#### 良好な点
- メール種別の網羅性は Phase 1-3 の段階的スコープで明確に定義
- §18 Phase 1 に GDPR イベント処理が含まれ、法規制リスクが解消
- §20.1 のイベント発行状況サマリが充実（11 イベントのステータス管理）
- テンプレート管理 API が §7.1 と §26 で完全に整合

</details>

<details>
<summary>architect レビューレポート</summary>

### 観点: マイクロサービス分割・Bounded Context・DDD パターン

#### 前回 High の修正確認
- **H-10**: §31 で MailLog / MailTemplate / MailSuppression の 3 Aggregate Root を明確に定義。Repository 対応関係・Aggregate 間ルールも記載。✅

#### Medium 指摘
1. **M-03**: UserInfoResolver の同期 HTTP 呼出し + フォールバック戦略未設計（前回から残存）
2. **M-04**: `EventEnvelope.PayloadJson` の `string` 型による型安全性の低さ（前回から残存）

#### 良好な点
- §31 の DDD Aggregate Root 定義が spec.md と整合（spec.md の MailTemplate + 設計独自の MailLog, MailSuppression）
- §30 の GDPR イベントハンドリングが疎結合設計（Kafka イベント駆動）を維持
- Outbox パターンの適用（§12.4）が ADR-0005 に準拠
- レイヤードアーキテクチャの依存方向が全セクションで正確

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 観点: コード例の正確性・C# 14 / .NET 10 機能活用・禁止パターン

#### Medium 指摘
1. **M-05**: `EventEnvelope.PayloadJson` の `string` 型受信（M-04 と同根。`JsonElement` 型推奨）
2. **M-06**: `HashEmail()` のソルトなし SHA-256 使用。レインボーテーブル攻撃リスク

#### Low 指摘
1. **L-01**: `RateLimitExceededException` / `SuppressedRecipientException` のコンストラクタが `recipientEmail` を直接受け取る設計（PII ログ出力リスク）

#### 良好な点
- §30 の新規 IMailService メソッドに `CancellationToken ct = default` が全て付与 ✅
- §28.1 SupportedEvents に GDPR イベントが正しく追加（`consent.revoked`, `user.deleted`, `user.processing-restricted`, `user.processing-unrestricted`）
- primary constructor の統一的な活用（§28.1-28.3）
- record 型の DTO / 設定クラスの活用（§7.2, §8.1, §24.4）
- `IServiceScopeFactory` による Scoped サービスの適切な取得
- コード例に `Console.WriteLine` / `.Result` / `.Wait()` の禁止パターンなし ✅

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 観点: DB スキーマ設計・EF Core マッピング・マイグレーション安全性

#### 前回 High の修正確認
- **H-01**: `mail_logs.status` CHECK 制約が 6 値に拡張。設計判断ノートで spec.md との差分理由を明記。✅
- **H-02**: `template_id` FK 追加（ON DELETE RESTRICT, ON UPDATE CASCADE）。§21.1 Fluent API で設定。✅
- **H-03**: `recipient_user_id` カラム追加。EF Core エンティティ + インデックス整備。✅
- **H-12**: `idx_mail_logs_template_id`, `idx_mail_logs_recipient_user_id` 追加。spec.md 準拠。✅

#### Medium 指摘
1. **M-02**: `mail_templates` インデックスが spec.md `(template_key, is_active) UNIQUE` と異なる（設計書は `name UNIQUE` 単独）。論理削除との整合性に影響
2. **M-20**: `SaveChangesAsync` の `if-else if` チェーン（4 型）。`IAuditableEntity` インターフェースで DRY 化推奨

#### 良好な点
- snake_case カラム名の徹底（全エンティティ + §21.1 Fluent API 一致）
- CHECK 制約の包括的な定義（status, retry_count, template_type, reason）
- UNIQUE インデックスによる冪等性保証（`event_id`）
- FK 制約が spec.md と整合（MailLog → MailTemplate: RESTRICT）
- JSONB 型 `variables` カラムの適切な定義
- `recipient_user_id` インデックスが DSR 処理の高速化に寄与

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 観点: OWASP Top 10・認証/認可設計・秘密情報管理

#### 前回 High の修正確認
- **H-09**: §27 のセキュリティヘッダーに `Referrer-Policy` と `Permissions-Policy` を追加。計 5 ヘッダー + `UseHsts()` = 6 種完備。✅

#### Medium 指摘
1. **M-07**: `user.email_changed` のレート制限除外検討（前回から残存）
2. **M-08**: テストメール API のレート制限実装方式が不明確（前回から残存）

#### 良好な点
- セキュリティヘッダー 6 種完備（X-Content-Type-Options, X-Frame-Options, CSP, Referrer-Policy, Permissions-Policy, HSTS）
- Managed Identity による Azure ACS 認証（ハードコード禁止の原則遵守）
- `FallbackPolicy` による全エンドポイント認証必須化
- JWT ClockSkew = 5 分設定
- XSS 防止策（Html.Raw() 禁止）明記
- RFC 5322 メールバリデーション
- §30.2 の user.deleted PII 仮名化でスタックトレースを含めずにログ出力する設計

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 観点: GDPR・個人情報保護法・PCI DSS・データガバナンス

#### 前回 High の修正確認
- **H-04**: `consent.revoked` イベント処理を §30.1 で設計。5 ステップの処理フロー + IMailService メソッド定義。✅
- **H-05**: `user.deleted` イベント処理を §30.2 で Phase 1 に前倒し。7 ステップの処理フロー + `user.deletion.completed` イベント発行。GDPR 30 日期限との整合性を §30.4 で明確化。✅
- **H-06**: `user.processing-restricted` / `unrestricted` イベント処理を §30.3 で設計。トランザクションメールは制限対象外（GDPR 第 18 条準拠）を明確化。✅

#### Medium 指摘
1. **M-01**: `mail_suppressions.reason` の `consent.revoked` / `processing-restricted` 競合リスク（**新規**）
2. **M-09**: Azure Communication Services の DSR 対応方針が未記載（前回から残存）

#### 良好な点
- GDPR 3 イベントの処理フローが詳細に設計され、Phase 1 必須が明確化（§30.4 のコンプライアンス根拠テーブル）
- PII 保持期間が spec.md（1 年）と統一（§4.2）
- 90 日経過後のアクセス制限強化（管理者のみ閲覧可能）が追加
- `user.deletion.completed` イベントによる DSR 進捗管理
- TDE（透過的データ暗号化）の方針維持

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 観点: トレーサビリティ・ドキュメント整合性・ADR 参照

#### Medium 指摘
1. **M-11**: 管理 API 操作の監査ログ出力が未設計（前回から残存）
2. **M-12**: `mail.event.consumed.total` メトリクスへの `producer` タグ追加推奨（前回から残存）

#### 良好な点
- §30 の GDPR イベント処理で PII をログ出力しない方針が明記
- §30.2 の `user.deletion.completed` イベント発行が DSR 進捗管理のトレーサビリティを確保
- Correlation ID の伝搬（§27 ミドルウェア + §4 `correlation_id` カラム）
- `event_id` による冪等性保証と重複検知ログ
- ADR-0004/0005/0006 への明示的な参照が維持

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 観点: テスト戦略・カバレッジ目標・検証可能性

#### Medium 指摘
1. **M-13**: `PiiCleanupService` のテスト計画が §17.1 に未記載（前回から残存）
2. **M-14**: §30 の GDPR イベント処理テストケースが §17.3 に未追加。以下のケースが必要: consent.revoked → mail_suppressions 登録確認、user.deleted → PII 仮名化確認、processing-restricted → マーケティングメール停止確認

#### Low 指摘
1. **L-02**: Kafka テスト用プロバイダの具体的な実装方針が不明確（前回から残存）

#### 良好な点
- テストメソッド命名規約（`Should_期待結果_When_条件`）の準拠維持
- 異常系テストケース 10 件の維持
- 統合テスト計画（WebApplicationFactory + カスタム AuthenticationHandler）
- Testcontainers.PostgreSql の使用方針

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 観点: パフォーマンス SLA・スケーラビリティ・キャッシュ戦略

#### Medium 指摘
1. **M-15**: レート制限チェックの DB 負荷（Redis キャッシュ推奨）（前回から残存）
2. **M-16**: `MailRetryService` / `PiiCleanupService` の Advisory Lock 未適用（前回から残存）

#### Low 指摘
1. **L-03**: `PiiCleanupService` のバッチ処理ページング（前回から残存）

#### 良好な点
- 非同期 Kafka Consumer によるイベント駆動設計
- `MailRetryService` の動的バックオフ（100ms〜5s）
- §28.3 のバッチサイズ 500 件は妥当
- `AsNoTracking()` の読み取り専用クエリでの暗黙的活用（Repository 設計）

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 観点: コンテナ設計・可観測性・ヘルスチェック・DR

#### Medium 指摘
1. **M-17**: ACS ヘルスチェックが §27 の HealthChecks 登録に未反映（前回から残存）

#### Low 指摘
1. **L-04**: Docker Compose `profiles: [app]` のデフォルト起動方法が不明確（前回から残存）

#### 良好な点
- PostgreSQL + Kafka のヘルスチェック登録（§27）
- Liveness / Readiness エンドポイントの分離
- Docker Compose の環境変数管理（`.env.example` 提供）
- OpenTelemetry のトレーシング + メトリクス統合

</details>

<details>
<summary>release-manager レビューレポート</summary>

### 観点: リリース戦略・ロールバック計画・バージョニング

#### Medium 指摘
1. **M-18**: Phase Gate 基準が未定義（前回から残存）

#### 良好な点
- §18 の Phase 分割に GDPR イベント処理が Phase 1 に含まれ、法規制リスクが解消
- §19 の既存サービス変更の影響分析が詳細
- §20 の制約・前提条件が明確
- §20.1 のイベント発行状況サマリで各イベントの Phase 対応が一覧化

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 観点: NuGet ライセンス適合性・依存関係脆弱性・禁止パッケージ

#### Low 指摘
1. **L-05**: `Azure.Communication.Email` の具体的バージョン確認・CVE チェック推奨（前回から残存）

#### 良好な点
- 全パッケージが AGENTS.md §8 の許可リストに準拠
- 禁止パッケージの使用なし（`Newtonsoft.Json`, `log4net`, `-preview` 版）
- `System.Text.Json` の使用（§28.1）

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 観点: UX 設計品質・WCAG 2.1 準拠・レスポンシブ設計

#### Medium 指摘
1. **M-19**: メールテンプレートの WCAG 2.1 Level A 準拠要件が未定義（前回から残存）

#### Low 指摘
1. **L-06**: テンプレートの多言語対応（日英）設計が未記載（前回から残存）

#### 良好な点
- フッターへのプライバシーポリシーリンク記載（§6.2）
- プレーンテキストフォールバックの自動生成方針
- レスポンシブ HTML メール対応の方針記載

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 観点: 技術標準の横断適合性・実装実現可能性・規約遵守

#### 前回 High の修正確認
- **H-10**: §31 の DDD Aggregate Root 定義が追加。3 Root の定義・Repository 対応・Aggregate 間ルールが明確。✅
- **H-11**: PII 保持期間が spec.md の 1 年に統一。§4.2 と §28.3 の両方で修正確認。✅

#### Medium 指摘
1. **M-01**: `mail_suppressions.reason` の `consent.revoked` / `processing-restricted` 競合リスク（compliance-reviewer と共同指摘、**新規**）

#### 総合評価
設計書は Iteration 3 の 12 件の High 指摘を全て適切に修正し、**実装可能なレベルに達している**。特に以下の改善が顕著:

1. **spec.md 整合性**: データモデル（`template_id` FK, `recipient_user_id`, status CHECK）が統一され、設計判断ノートで差分理由が明記された
2. **GDPR コンプライアンス**: §30 で `consent.revoked` / `user.deleted` / `user.processing-restricted` の 3 イベントの処理フローが詳細に設計され、Phase 1 必須が明確化された。GDPR 条項との対応表（§30.4）が法的根拠を提供
3. **DDD 整合性**: §31 で Aggregate Root が明確に定義され、Repository パターンとの整合性が確保された
4. **セキュリティ**: セキュリティヘッダー 6 種完備。ミドルウェアパイプライン順序が AGENTS.md 準拠

残存 Medium 21 件は実装と並行して対応可能な改善提案であり、ブロッカーではない。唯一注意すべきは M-01（mail_suppressions reason 競合）で、GDPR 処理の正確性に影響するため実装前に解決を推奨する。

</details>
