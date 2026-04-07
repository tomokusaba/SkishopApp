# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/mailsend-service-design.md`
- **判定**: ✅ **Approved with Notes** — 推奨改善事項あり（Critical/High 指摘ゼロ）
- **レビュー日時**: 2026-04-03
- **イテレーション**: 2（前回: check-report-1 — 0 Critical / 21 High → 全件修正済）
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

## 前回指摘の修正検証結果

**全 21 件の High 指摘が修正済み ✅**

| # | 前回指摘 | 出典 Agent | 修正状態 | 検証結果 |
|---|---------|-----------|---------|---------|
| 1 | `TIMESTAMP WITH TIME ZONE` 未使用 | dba-reviewer | ✅ 修正済 | 全日時カラムが `TIMESTAMP WITH TIME ZONE` に変更 |
| 2 | `mail_templates` テーブル定義欠落 | dba-reviewer | ✅ 修正済 | §4.2 に完全な DDL + ハイブリッド方式の設計判断ノート追加 |
| 3 | `status` CHECK 制約欠如 | dba-reviewer | ✅ 修正済 | `CHECK (status IN ('PENDING', 'SENDING', 'SENT', 'FAILED', 'SKIPPED'))` 追加 |
| 4 | `retry_count` CHECK 制約欠如 | dba-reviewer | ✅ 修正済 | `CHECK (retry_count >= 0)` 追加 |
| 5 | Kafka イベント名不整合 | architect | ✅ 修正済 | `dot.separated.lowercase` に統一、§6.1 に命名規約ノート追加 |
| 6 | spec.md 未カバーイベント | architect | ✅ 修正済 | §6.1 に差分テーブル追加（Phase 4 対応の 6 イベントを明示） |
| 7 | Outbox パターン未記述 | architect | ✅ 修正済 | §12.4 に ADR-0005 準拠の Outbox 適用方針を明記 |
| 8 | PII 平文保存 | security-reviewer | ✅ 修正済 | PII 保護方針セクション追加（90 日保持、匿名化、暗号化） |
| 9 | API パス不整合 | security-reviewer | ✅ 修正済 | `/admin/mail/...` に統一（§7.1, §15） |
| 10 | XSS 防止策未記述 | security-reviewer | ✅ 修正済 | §13.3 に Razor XSS 防止規約追加（`Html.Raw()` 禁止含む） |
| 11 | MailSuppression 欠落 | business-analyst | ✅ 修正済 | §4.2 に `mail_suppressions` テーブル DDL 追加 |
| 12 | レート制限（ユーザー単位）欠落 | business-analyst | ✅ 修正済 | §9.5 に配信頻度制限設計 + コード例追加 |
| 13 | PII 保持期間未定義 | compliance-reviewer | ✅ 修正済 | 90 日保持 + `PiiCleanupBackgroundService` + `user.deleted` 対応 |
| 14 | データ最小化原則 | compliance-reviewer | ✅ 修正済 | 保存の正当性を監査要件として明文化 |
| 15 | `IOptions<T>` パターン未使用 | programing-reviewer | ✅ 修正済 | §8.1 に `KafkaSettings` record + `IOptions<KafkaSettings>` 追加 |
| 16 | UserInfoResolver エラーハンドリング不足 | programing-reviewer | ✅ 修正済 | §12.3 に 404/5xx 区別、`ILogger` 出力パターン追加 |
| 17 | 異常系テストケース不足 | qa-manager | ✅ 修正済 | §17.3 に 10 件の異常系テストケース + 期待動作を追加 |
| 18 | 統合テスト設計不足 | qa-manager | ✅ 修正済 | §17.4 に `WebApplicationFactory` + カスタム `AuthenticationHandler` 追加 |
| 19 | mail_queue 設計判断不明確 | tech-lead | ✅ 修正済 | §4.2 に設計判断ノート追加（PENDING ステータスで代用） |
| 20 | MailAttachment 監査カラム欠落 | tech-lead | ✅ 修正済 | `mail_attachments` に `created_at` / `updated_at` 追加 |
| 21 | Docker Compose パスワードインライン | infra-ops-reviewer | ✅ 修正済 | §16 に `.env` 管理方針 + `.env.example` 追加 |

## 前回エスカレーション事項の解決状況

| # | 前回エスカレーション | 解決状況 |
|---|-------------------|---------|
| 1 | MailKit vs Azure CS の位置づけ | ⚠️ 部分解決: 設計書は Azure CS を採用、開発環境では MailHog / SMTP4Dev を使用。spec.md の MailKit 言及との差分は明確になったが spec.md 側の更新は未実施 |
| 2 | テンプレート管理方式の選択 | ✅ 解決: §4.2 の mail_templates テーブル設計ノートでハイブリッド方式（DB メタデータ + Razor レンダリング）を明確化 |
| 3 | PII 保持期間 | ✅ 解決: 90 日保持 + 匿名化ポリシーを策定 |
| 4 | DB 名の ADR-0006 不整合 | ✅ 解決: §5 で `mailsenddb` に変更、Docker Compose も整合 |

## 技術スタック検証結果
| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| 言語 | C# 14 (.NET 10) | C# 14 (.NET 10) | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| ORM | EF Core 10 | EF Core 10 | ✅ |
| DB | PostgreSQL (mailsenddb) | PostgreSQL（ADR-0006 独立 DB） | ✅ |
| メッセージング | Confluent.Kafka 2.* | Confluent.Kafka 2.* | ✅ |
| メールプロバイダ | Azure Communication Services Email | spec.md: MailKit/Azure CS | ⚠️ 設計判断により Azure CS 選択（spec.md 更新推奨） |
| ロギング | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| テスト | xUnit, NSubstitute, Shouldly | xUnit, NSubstitute, Shouldly | ✅ |
| バリデーション | FluentValidation 11.* | FluentValidation 11.* | ✅ |
| 可観測性 | OpenTelemetry | OpenTelemetry | ✅ |
| エラーレスポンス | RFC 9457 Problem Details | ADR-0007 | ✅ |
| Outbox パターン | 適用（§12.4） | ADR-0005 | ✅ |

## 指摘サマリー
| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ✅ Pass | 0 | 0 | 0 | 0 |
| architect | ✅ Pass | 0 | 0 | 2 | 0 |
| tech-lead | ✅ Pass | 0 | 0 | 2 | 0 |
| programing-reviewer | ✅ Pass | 0 | 0 | 1 | 1 |
| security-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| dba-reviewer | ✅ Pass | 0 | 0 | 2 | 1 |
| qa-manager | ✅ Pass | 0 | 0 | 0 | 1 |
| performance-reviewer | ✅ Pass | 0 | 0 | 0 | 1 |
| compliance-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| oss-reviewer | ✅ Pass | 0 | 0 | 0 | 0 |
| release-manager | ✅ Pass | 0 | 0 | 0 | 0 |
| infra-ops-reviewer | ✅ Pass | 0 | 0 | 0 | 1 |
| audit-reviewer | ✅ Pass | 0 | 0 | 1 | 0 |
| ux-accessibility-reviewer | ✅ Pass | 0 | 0 | 0 | 1 |
| **合計** | | **0** | **0** | **8** | **6** |

## 判定根拠
- 判定ルール適用結果: Critical 0 件、High 0 件、Medium 8 件、Low 6 件 → **✅ Approved with Notes**
- 前回の 21 件の High 指摘が全て適切に修正されていることを確認
- 新規の Critical / High 指摘なし
- 修正により導入された新規問題: なし（全ての Medium/Low は既存の改善機会）
- 最も優先度の高い改善事項: EF Core エンティティ定義の補完（MailTemplate / MailSuppression）と CHECK 制約の命名規約準拠

---

## Medium 指摘一覧（推奨改善）

| # | 重要度 | 出典 Agent | カテゴリ | 対象箇所 | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|---------|----------|----------|
| 1 | **Medium** | dba-reviewer | DB スキーマ | §4.2 全テーブル | **CHECK 制約の命名規約**: `mail_logs`, `mail_templates`, `mail_suppressions` の CHECK 制約がインライン無名形式（`CHECK (status IN (...))`）で定義されている。sql-schema-review.instructions.md §2 では `CONSTRAINT ck_<table>_<column> CHECK (...)` の名前付き形式が規約とされている | 例: `CONSTRAINT ck_mail_logs_status CHECK (status IN ('PENDING', 'SENDING', 'SENT', 'FAILED', 'SKIPPED'))` の形式に変更。`ck_mail_logs_retry_count`, `ck_mail_templates_template_type`, `ck_mail_suppressions_reason` も同様 |
| 2 | **Medium** | dba-reviewer | EF Core | §4.2 | **MailTemplate / MailSuppression の C# EF Core エンティティ定義が未記載**: `MailLog` と `MailAttachment` には `[Table]` / `[Column]` 属性付きの完全な C# クラス定義があるが、新規追加された `MailTemplate` と `MailSuppression` には DDL のみで C# エンティティコードがない。AGENTS.md §10.3 では全エンティティに明示的マッピング属性が必須 | `MailTemplate` / `MailSuppression` の C# エンティティクラスを追加。`[Table("mail_templates")]`, `[Table("mail_suppressions")]` + 各カラムの `[Column("snake_case")]` 属性、コレクションナビゲーションの `= []` 初期化を含める |
| 3 | **Medium** | architect | Kafka 設計 | §8.1 | **Kafka トピック構成の設計判断が不明確**: `KafkaSettings` が単一の `Topic` フィールド（`"domain-events"`）を持つが、MailSendService は 7 種類以上のイベントタイプを購読する。spec.md ではイベント名（`user.registered`, `order.created` 等）が個別トピック名として使用されている。単一トピック方式と個別トピック方式のどちらを採用するかが明記されていない | ① 単一トピック方式の場合: eventType による消費ルーティングの設計判断を明記 ② 個別トピック方式の場合: `KafkaSettings.Topic` を `Topics`（`string[]`）に変更し、Consumer のマルチトピック Subscribe を記述 |
| 4 | **Medium** | architect | Outbox | §12.4 | **`outbox_events` テーブル DDL の参照不足**: Outbox パターンの適用方針は明記されているが、`outbox_events` テーブルのスキーマ定義がこの設計書内に含まれておらず、他の設計書（spec.md / 共通設計）への相互参照もない | ① `outbox_events` テーブルの DDL をこの設計書内に含めるか、② spec.md / ADR-0005 の該当セクションへの明示的な相互参照（`→ spec.md §X.X outbox_events テーブル定義を参照`）を追加 |
| 5 | **Medium** | tech-lead | プロジェクト構成 | §10 | **Models/ ディレクトリのファイル一覧が不完全**: §4.2 で `mail_templates` と `mail_suppressions` テーブルが追加されたが、§10 のプロジェクト構成の `Models/` には `MailLog.cs` と `MailAttachment.cs` のみ記載。`MailTemplate.cs` と `MailSuppression.cs` が欠落している | `Models/` 配下に `MailTemplate.cs` と `MailSuppression.cs` を追加記載 |
| 6 | **Medium** | tech-lead | ミドルウェア | §10-11 | **Program.cs のミドルウェアパイプライン順序が未記載**: AGENTS.md §11.3 で `UseExceptionHandler` → `UseHsts` → `UseAuthentication` → `UseAuthorization` 等の厳密な順序が規定されているが、設計書に MailSendService 固有のミドルウェア構成が記載されていない | MailSendService の Program.cs におけるミドルウェア登録順序を明記（少なくとも ExceptionHandler → SecurityHeaders → SerilogRequestLogging → Authentication → Authorization → RateLimiter → Endpoints の順序） |
| 7 | **Medium** | programing-reviewer | コーディング規約 | §8 | **MailEventConsumer BackgroundService の `IServiceScopeFactory` パターンが未記載**: AGENTS.md §10.6 では BackgroundService で Scoped サービスを使用する場合に `IServiceScopeFactory` でスコープを生成するパターンが必須。§8.1 で Kafka Consumer の DI 登録はあるが、Consumer 本体の `ExecuteAsync` 実装例が記載されていない | AGENTS.md §10.6 の `OrderCreatedConsumer` パターンに準じた `MailEventConsumer` の `BackgroundService` 実装例（`IServiceScopeFactory` + `stoppingToken` 伝搬 + エラーハンドリング）を追加 |
| 8 | **Medium** | audit-reviewer | トレーサビリティ | §8 | **Correlation ID の Kafka ヘッダーからの抽出フローが未記載**: `mail_logs` テーブルに `correlation_id` カラムが定義されているが、Kafka メッセージヘッダーから Correlation ID を抽出して MailLog に記録する具体的な実装フローが記載されていない。AGENTS.md §11.2 ではマイクロサービス間の Correlation ID 伝搬が必須 | Kafka Consumer 内で `ConsumeResult.Message.Headers` から `X-Correlation-Id` を抽出し、`MailLog.CorrelationId` に設定するフローを記述 |

## Low 指摘一覧（改善提案）

| # | 重要度 | 出典 Agent | カテゴリ | 対象箇所 | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|---------|----------|----------|
| 1 | **Low** | dba-reviewer | インデックス | §4.2 mail_logs | `idx_mail_logs_status` は低カーディナリティ（5 値）のため効果が限定的。部分インデックス `WHERE status IN ('PENDING', 'FAILED')` の方が PENDING/FAILED レコードの検索に効率的 | 部分インデックスの検討: `CREATE INDEX idx_mail_logs_status_pending ON mail_logs (status) WHERE status IN ('PENDING', 'FAILED')` |
| 2 | **Low** | programing-reviewer | コード品質 | §19.1, §19.2 | §19 の他サービス変更コード例で `DateTime.UtcNow` を使用しているが、§4.2 の EF Core エンティティでは `DateTimeOffset.UtcNow` を使用。設計書内での日時型の一貫性が低い。AGENTS.md §10.3 では両方許容されるが統一が望ましい | §19 のコード例を `DateTimeOffset.UtcNow` に統一（EF Core エンティティと整合） |
| 3 | **Low** | qa-manager | テスト | §17.3 #5 | Kafka メッセージのデシリアライズ失敗時の対応が「DLT への転送を検討」のまま。DLT（Dead Letter Topic）の具体的な設計（トピック名、リトライポリシー、DLT からの再処理手順）が未確定 | DLT のトピック命名規則（例: `mail.events.dlq`）と再処理方針を確定 |
| 4 | **Low** | performance-reviewer | 設定 | §9.5 | レート制限のしきい値 `5` がコード例にハードコード。`MailSettings` 等の設定クラスで外部化すべき | `MailSettings` に `RateLimitPerHour` プロパティを追加し、`IOptions<MailSettings>` 経由で参照 |
| 5 | **Low** | infra-ops-reviewer | Dockerfile | §16 | Docker Compose は定義されているが、MailSendService の **Dockerfile** が設計書に含まれていない。AGENTS.md §12.5 / dockerfile-infra.instructions.md ではマルチステージビルド、非 root ユーザー、HEALTHCHECK を含む Dockerfile 規約が定義されている | 実装フェーズ（Phase 9）で対応可。必要に応じて設計書に Dockerfile テンプレートを追加 |
| 6 | **Low** | ux-accessibility-reviewer | 多言語 | §6.2 | メールテンプレートの**多言語対応**（日本語 / 英語）方針が未記載。spec.md ではバックエンドの多言語化（`.resx` + `IStringLocalizer`）が規定されており、メール本文テンプレートも `.resx` 管理対象とされている。ユーザーの言語設定に基づくテンプレート選択ロジックの検討が推奨 | テンプレートの言語選択ロジック（`Accept-Language` ヘッダーまたはユーザープロファイルの言語設定参照）を将来課題として記録 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| 1 | 通常 | architect | spec.md のメールプロバイダ記載（MailKit / Azure CS）と設計書の選択（Azure CS のみ）に差分がある。設計書側の判断は妥当だが、spec.md の更新が未実施。spec.md 側の該当記述を更新し整合性を確保すべきか | テックリード |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|----------|----------|
| — | — | — | 競合なし | — | — |

---

## ドキュメント横断分析

### spec.md との整合性チェック

| 観点 | spec.md 定義 | 設計書記載 | 整合性 | 前回比較 |
|------|------------|----------|--------|---------|
| ポート番号 | 5008 | 5008 | ✅ | 変更なし |
| DB 名 | ADR-0006（独立 DB） | mailsenddb | ✅ | ❌→✅ 修正済 |
| メールプロバイダ | MailKit (SMTP) / Azure CS | Azure CS（開発: MailHog/SMTP4Dev） | ⚠️ 設計判断で Azure CS 選択 | 変更なし |
| テンプレート管理 | DB 管理（MailTemplate CRUD API） | ハイブリッド方式（DB メタデータ + Razor レンダリング） | ✅ 設計判断を明記 | ❌→✅ 修正済 |
| MailSuppression | 定義あり | `mail_suppressions` テーブル定義あり | ✅ | ❌→✅ 修正済 |
| 購読イベント | 9 種類 | 8 種類 + Phase 4 差分明示 | ✅ 差分管理済 | ⚠️→✅ 修正済 |
| 管理 API パス | `/admin/mail/...` | `/admin/mail/...` | ✅ | ❌→✅ 修正済 |
| レート制限 | 1 時間 5 通/ユーザー | §9.5 で設計済（セキュリティメール除外） | ✅ | ❌→✅ 修正済 |
| マーケティング配信 | In Scope（spec.md §11） | Out of Scope（§1.2） | ⚠️ スコープ差分（前回同様） | 変更なし |
| Outbox パターン | ADR-0005 必須 | §12.4 で適用方針明記 | ✅ | ❌→✅ 修正済 |
| RFC 9457 | ADR-0007 必須 | §7 で準拠 | ✅ | 変更なし |
| PII 保持期間 | GDPR 対応必須 | 90 日保持 + 匿名化 | ✅ | ❌→✅ 修正済 |
| Kafka イベント命名 | `dot.separated.lowercase` | §6.1 で統一 | ✅ | ❌→✅ 修正済 |
| XSS 防止 | AGENTS.md §5 / security-coding.instructions.md | §13.3 で規約化 | ✅ | ❌→✅ 修正済 |

### 改善推移サマリ

| 指標 | check-report-1 | check-report-2 | 推移 |
|------|---------------|---------------|------|
| Critical | 0 | 0 | — |
| High | 21 | 0 | ⬇️ -21 |
| Medium | 25 | 8 | ⬇️ -17 |
| Low | 7 | 6 | ⬇️ -1 |
| 判定 | ⚠️ Conditional Approval | ✅ Approved with Notes | ⬆️ 改善 |
| spec.md 整合性 ❌ | 6 箇所 | 0 箇所 | ✅ 全解消 |

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### 観点: ビジネス要件の完全性

前回指摘の 2 件の High（MailSuppression 欠落、レート制限欠落）が適切に修正されていることを確認。

- **MailSuppression**: §4.2 に `mail_suppressions` テーブル定義が追加され、ERD にも反映。CHECK 制約（reason IN ('UNSUBSCRIBE', 'BOUNCE', 'COMPLAINT')）も定義済み
- **レート制限**: §9.5 に同一受信者への配信頻度制限（1 時間 5 通）が設計され、セキュリティメール（パスワードリセット、メール認証）の除外ルールも適切
- **spec.md 差分**: §6.1 に未対応イベント 6 件を Phase 4 として明示的に記録

新規指摘なし。

</details>

<details>
<summary>architect レビューレポート</summary>

### 観点: アーキテクチャ設計の整合性

前回指摘の 3 件の High が全て修正済み。

#### Medium（新規）
1. **Kafka トピック構成の設計判断が不明確**: `KafkaSettings` が単一 `Topic` フィールドを持つが、7+ のイベントタイプを購読。単一トピック vs 個別トピックの設計判断が未記載
2. **`outbox_events` テーブル DDL の参照不足**: Outbox パターン適用方針は明記されたが、テーブルスキーマの相互参照がない

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 観点: 技術標準の横断適合性

前回指摘の 2 件の High が適切に修正済み。
- **mail_queue 設計判断**: §4.2 の設計判断ノートで意図的に PENDING ステータスで代用する理由を明確化
- **MailAttachment 監査カラム**: `created_at` / `updated_at` が追加され、EF Core エンティティにも反映

#### Medium（新規）
1. **Models/ ディレクトリ不完全**: §10 のプロジェクト構成に MailTemplate.cs / MailSuppression.cs が未記載
2. **Program.cs ミドルウェアパイプライン順序**: AGENTS.md §11.3 準拠のミドルウェア順序が設計書に未記載

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 観点: コード例の正確性・規約準拠

前回指摘の 2 件の High が修正済み。
- **IOptions<T>**: §8.1 で `KafkaSettings` record + `IOptions<KafkaSettings>` パターン適用
- **UserInfoResolver**: §12.3 で 404（null 返却）/ 5xx（例外→Polly リトライ）の分岐を詳細に記載

#### Medium（新規）
1. **MailEventConsumer BackgroundService 実装例なし**: `IServiceScopeFactory` パターン（AGENTS.md §10.6）に準じた Consumer 本体コードが未記載

#### Low（新規）
2. **§19 コード例の日時型不統一**: `DateTime.UtcNow` vs `DateTimeOffset.UtcNow`

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 観点: OWASP Top 10、認証/認可、秘密情報管理

前回指摘の 3 件の High が全て修正済み。
- **PII 保護**: 90 日保持 + 匿名化ポリシー + TDE 暗号化 + `user.deleted` 対応
- **API パス統一**: `/admin/mail/...` に統一済み
- **XSS 防止**: §13.3 に Razor デフォルトエスケープ、`Html.Raw()` 禁止、`Uri.EscapeDataString()` の規約を明記

新規指摘なし。セキュリティ設計は適切な水準に到達。

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 観点: DB スキーマ設計・EF Core マッピング

前回指摘の 4 件の High が全て修正済み。
- **TIMESTAMP WITH TIME ZONE**: 全テーブルの全日時カラムに適用
- **mail_templates テーブル**: 完全な DDL + ハイブリッド方式の設計ノート
- **CHECK 制約**: status / retry_count / template_type / reason に CHECK 制約定義

#### Medium（新規）
1. **CHECK 制約の命名規約**: インライン無名形式。`CONSTRAINT ck_<table>_<column>` の名前付き形式が sql-schema-review.instructions.md の規約
2. **MailTemplate / MailSuppression の C# EF Core エンティティ定義が未記載**: DDL はあるが C# クラスがない

#### Low（継続）
3. **部分インデックスの検討**: `idx_mail_logs_status` の低カーディナリティ改善（`WHERE status IN ('PENDING', 'FAILED')` の部分インデックス）

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 観点: テスト戦略・カバレッジ

前回指摘の 2 件の High が修正済み。
- **異常系テスト**: §17.3 に 10 件の具体的テストケース + 期待動作を追加
- **統合テスト**: §17.4 に `WebApplicationFactory` + カスタム `AuthenticationHandler` による管理 API テスト方針を追加
- テストメソッド命名規約（`Should_期待結果_When_条件`）も §17.2 で明記

#### Low（新規）
1. **DLT 設計が「検討」段階**: Kafka デシリアライズ失敗時の DLT への転送が未確定

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 観点: パフォーマンス SLA・スケーラビリティ

前回の Medium 指摘（バッチスループット、Consumer 並列処理、ACS レート制限値）は引き続き記録レベル。新規 High/Critical なし。

#### Low（新規）
1. **レート制限しきい値のハードコード**: §9.5 のコード例で `recentCount >= 5` がハードコード。設定クラスへの外部化が望ましい

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 観点: GDPR・個人情報保護法

前回指摘の 2 件の High が適切に修正済み。
- **PII 保持期間**: 90 日保持 + `PiiCleanupBackgroundService`（毎日 UTC 03:00）
- **忘れられる権利**: `user.deleted` イベント受信時の即時匿名化フロー
- **データ最小化**: `recipient_email` の保存が監査上必要である旨を明文化

新規指摘なし。GDPR 対応設計は十分な水準。

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 観点: NuGet ライセンス・依存関係

前回の Medium/Low 指摘（Azure.Communication.Email / Azure.Identity ライセンス確認）のみ。新規指摘なし。

- Azure.Communication.Email: MIT License ✅
- Azure.Identity: MIT License ✅
- その他の依存関係は AGENTS.md §8 の必須パッケージ一覧と整合

</details>

<details>
<summary>release-manager レビューレポート</summary>

### 観点: リリース戦略・ロールバック計画

前回の Medium 指摘（Phase 依存管理、ロールバック計画）は引き続き改善提案レベル。
- §18 の Phase 分け（Phase 1: MVP → Phase 3: 全メール完備）は明確
- §19 の他サービス変更と Phase の対応関係も明記
- §20.1 のイベント発行状況サマリテーブルが優れた可視性を提供

新規指摘なし。

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 観点: コンテナ設計・可観測性・DR

前回指摘の 1 件の High が修正済み。
- **Docker Compose パスワード管理**: `.env` ファイル管理方針 + `.env.example` + `.gitignore` 対応が明記

#### Low（継続）
1. **Dockerfile が設計書に未記載**: マルチステージビルド + 非 root ユーザー + HEALTHCHECK の Dockerfile 設計。Phase 9（最終）で対応可

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 観点: トレーサビリティ・ドキュメント整合性

#### Medium（継続）
1. **Correlation ID の Kafka ヘッダー抽出フロー**: `mail_logs.correlation_id` カラムは定義されているが、Kafka Consumer 内での抽出・記録の具体的な実装フローが未記載

前回の ADR 参照不足は §12.4（ADR-0005）および §5（ADR-0006）で改善。

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 観点: UX 設計・メールアクセシビリティ

#### Low（継続）
1. **多言語メールテンプレート**: ユーザーの言語設定に基づくテンプレート選択ロジックが未設計。将来課題として記録推奨

</details>
