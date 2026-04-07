# Fix Report: mailsend-service-design.md (Iteration 1)

## 修正サマリー
- **対象**: `design-docs/mailsend-service-design.md`
- **修正日時**: 2026-04-03
- **元レポート**: `check-report-1.md` (0 Critical, 21 High)

## 修正結果
| 重要度 | 検出数 | 修正数 | 残存数 |
|--------|--------|--------|--------|
| **Critical** | 0 | 0 | 0 |
| **High** | 21 | 21 | 0 |
| Medium | 25 | 0 | 25 |
| Low | 7 | 0 | 7 |

## High 指摘 修正詳細

| # | 出典 Agent | 指摘内容 | 修正内容 | 状態 |
|---|-----------|---------|----------|------|
| H1 | dba-reviewer | `TIMESTAMP` → `TIMESTAMP WITH TIME ZONE` 未使用 | mail_logs テーブルの `sent_at`, `created_at`, `updated_at` を全て `TIMESTAMP WITH TIME ZONE` + `DEFAULT CURRENT_TIMESTAMP` に修正。verification_tokens テーブル（§19.1）も同様に修正 | ✅ 修正済み |
| H2 | dba-reviewer | `mail_templates` テーブル定義が欠落 | §4.2 に mail_templates テーブル DDL を追加。spec.md §11 の MailTemplate エンティティと整合するカラム定義、CHECK 制約（template_type）、インデックスを含む。Razor ファイル + DB 管理のハイブリッド方式を設計判断として明記 | ✅ 修正済み |
| H3 | dba-reviewer | status カラムの CHECK 制約欠如 | `CHECK (status IN ('PENDING', 'SENDING', 'SENT', 'FAILED', 'SKIPPED'))` を追加 | ✅ 修正済み |
| H4 | dba-reviewer | retry_count の CHECK 制約欠如 | `CHECK (retry_count >= 0)` を追加 | ✅ 修正済み |
| H5 | architect | Kafka イベント名の不整合 | 全イベント名を `dot.separated.lowercase` 形式に統一（spec.md 準拠）。§3.1/3.2 mermaid、§6.1 テーブル、§8.2 flowchart、§12.1 ペイロード JSON、§12.3 テーブル、§12.4 テーブル、§19 コード例、§20.1 テーブルを全て修正。命名規約の注記を §6.1 冒頭に追加 | ✅ 修正済み |
| H6 | architect | spec.md 定義イベントの未カバー | §6.1 に「spec.md 定義イベントとの差分」セクションを追加。`payment.completed`, `payment.refunded`, `point.earned`, `coupon.expired`, `inventory.low-stock`, `user.deleted` の 6 イベントを Phase 4 対応として明記 | ✅ 修正済み |
| H7 | architect | Outbox パターン（ADR-0005）との関係未記述 | §12.4 に Outbox パターン適用方針を追加。`mail.sent`, `mail.send.failed` イベント発行に Outbox パターンを適用し、`outbox_events` テーブル経由で Kafka に発行する設計を明記 | ✅ 修正済み |
| H8 | security-reviewer | PII（recipient_email）の平文保存 | §4 に「PII 保護方針・データ保持ポリシー」セクションを追加。保持期間 90 日、匿名化バッチ、user.deleted 対応、暗号化（TDE）、データ最小化原則を明記 | ✅ 修正済み |
| H9 | security-reviewer | API パスの不整合（/api/v1/mail vs /admin/mail） | §7.1、§13.1、§15 の全 API パスを `/admin/mail/...` に統一（spec.md 準拠） | ✅ 修正済み |
| H10 | security-reviewer | Razor テンプレートへの XSS 防止策未記述 | §13.3 に「XSS 防止策（メールテンプレート）」セクションを追加。Razor デフォルトエスケープ活用、`Html.Raw()` / `MarkupString` 禁止、URL エスケープ方針を明記 | ✅ 修正済み |
| H11 | business-analyst | MailSuppression（配信停止リスト）が欠落 | §4.1 ERD に MailSuppression エンティティ追加、§4.2 に mail_suppressions テーブル DDL（UNIQUE 制約、CHECK 制約、インデックス）を追加。`consent.revoked` イベント購読と配信停止ロジックの設計を記述 | ✅ 修正済み |
| H12 | business-analyst | レート制限（ユーザー単位）が欠落 | §9.5 に「同一受信者への配信頻度制限」セクションを追加。1 時間に 5 通制限、mail_logs ベースのカウントロジック、セキュリティメール除外、実装例コードを記載 | ✅ 修正済み |
| H13 | compliance-reviewer | PII 保持期間・削除ポリシー未定義 | §4（PII 保護方針セクション）に保持期間 90 日、`PiiCleanupBackgroundService` による自動匿名化、`user.deleted` イベント対応を明記 | ✅ 修正済み |
| H14 | compliance-reviewer | データ最小化原則への適合性不明確 | §4 PII セクションに「recipient_email の保存は監査要件上必要」であることを明文化し、不要な場合は user_id のみ保持する代替案も記載 | ✅ 修正済み |
| H15 | programing-reviewer | IOptions\<T\> パターン未使用 | §8.1 の Kafka Consumer 設定を `KafkaSettings` record + `IOptions<KafkaSettings>` パターンに修正（AGENTS.md §10.1 準拠） | ✅ 修正済み |
| H16 | programing-reviewer | UserInfoResolver のエラーハンドリング不足 | §12.3 の `ResolveAsync` メソッドを拡張。404 時は `null` 返却（FAILED 化）、5xx 時は `EnsureSuccessStatusCode()` で例外スロー（Polly リトライ対象）、`ILogger<T>` でエラーログ出力パターンを記載 | ✅ 修正済み |
| H17 | qa-manager | 異常系テストケースの不足 | §17.3 に異常系テストケース 10 件を追加（ACS 429、RFC 5322 非準拠、UserManagement 不達/404、Kafka デシリアライズ失敗、テンプレートエラー、冪等性、配信停止チェック、レート制限、ヘルスチェック） | ✅ 修正済み |
| H18 | qa-manager | 統合テスト設計の不足 | §17.4 に `WebApplicationFactory<Program>` + カスタム `AuthenticationHandler` での統合テスト設計を追加。全管理 API エンドポイントの認証チェック含む | ✅ 修正済み |
| H19 | tech-lead | mail_queue テーブルの設計判断不明確 | §4.2 のインデックスセクション後に設計判断ノートを追加。「mail_logs.status = 'PENDING' でキュー管理を代用する理由」を 3 点で明記 | ✅ 修正済み |
| H20 | tech-lead | MailAttachment に監査カラム欠落 | §4.1 ERD の MailAttachment に `created_at`, `updated_at` を追加。§4.2 に mail_attachments テーブル DDL（FK 制約 ON DELETE CASCADE、インデックス含む）を追加 | ✅ 修正済み |
| H21 | infra-ops-reviewer | Docker Compose パスワード管理の不明確さ | §16 に `.env` ファイル管理方針の注記を追加。`.env.example` のサンプルを記載し、`.gitignore` に `.env` を含める方針を明記。DB 名を `mailsenddb` に修正（ADR-0006 準拠） | ✅ 修正済み |

## 追加修正（High 対応に伴う付随修正）

| # | 修正内容 |
|---|---------|
| 1 | §4.1 ERD に MailTemplate エンティティ追加（MailTemplate → MailLog リレーション） |
| 2 | §4.2 に EF Core エンティティ定義（MailLog, MailAttachment の C# クラス）を追加（AGENTS.md §10.3 準拠：`[Table]`, `[Column]` 属性、`= []` 初期化） |
| 3 | §5 DB 名を `mailsenddb` に変更（ADR-0006: サービス別独立 DB） |
| 4 | §3.1 mermaid の DB 名を `mailsenddb` に変更 |
| 5 | §6.1 に spec.md 定義イベントとの差分テーブルを追加（Phase 4 対応方針明記） |
| 6 | §7.1 テストメール送信 API にレート制限注記を追加 |
| 7 | §7.2 DTO の `DateTime` → `DateTimeOffset` に修正 |
| 8 | §17.2 にテストメソッド命名規約（`Should_期待結果_When_条件` パターン）を追記 |
| 9 | §19.1 verification_tokens テーブルの `TIMESTAMP` → `TIMESTAMP WITH TIME ZONE` に修正 |

## エスカレーション事項（修正対象外・要人間判断）

| # | 優先度 | 内容 | 推奨判断者 |
|---|--------|------|-----------|
| 1 | 高優先 | spec.md と設計書でメールプロバイダが異なる（spec.md: MailKit/Azure CS、設計書: Azure CS のみ）。開発環境 SMTP テスト方針は設計書に記載があるが MailKit の位置づけが曖昧 | テックリード |
| 2 | 高優先 | テンプレート管理方式: ハイブリッド方式（Razor + DB）を採用したが、spec.md 側の更新が必要か検討 | プロダクトオーナー + テックリード |
| 3 | 最優先 | PII 保持期間 90 日は暫定値。法務チームによる GDPR / 個人情報保護法の要件確認が必要 | 法務チーム |

## 修正未実施（Medium/Low — ブロッキングではない）

- Medium 25 件、Low 7 件は本イテレーションの対象外
- パフォーマンス設計（バッチスループット、Kafka パーティション設計）は Medium
- Dockerfile 設計、多言語対応、Correlation ID 伝搬詳細は Medium/Low
