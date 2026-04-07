# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/mailsend-service-design.md`
- **判定**: ⚠️ **Conditional Approval** — High 指摘あり、人間の判断を介在
- **レビュー日時**: 2026-04-03
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

## 技術スタック検証結果
| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| 言語 | C# 14 (.NET 10) | C# 14 (.NET 10) | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| ORM | EF Core 10 | EF Core 10 | ✅ |
| DB | PostgreSQL | PostgreSQL | ✅ |
| メッセージング | Confluent.Kafka 2.* | Confluent.Kafka 2.* | ✅ |
| メールプロバイダ | Azure Communication Services Email | — (spec.md: MailKit/Azure CS) | ⚠️ 後述 |
| ロギング | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| テスト | xUnit, NSubstitute, Shouldly | xUnit, NSubstitute, Shouldly | ✅ |
| バリデーション | FluentValidation 11.* | FluentValidation 11.* | ✅ |
| 可観測性 | OpenTelemetry | OpenTelemetry | ✅ |

## 指摘サマリー
| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ⚠️ Conditional | 0 | 2 | 2 | 1 |
| architect | ⚠️ Conditional | 0 | 3 | 2 | 0 |
| tech-lead | ⚠️ Conditional | 0 | 2 | 3 | 0 |
| programing-reviewer | ⚠️ Conditional | 0 | 2 | 2 | 1 |
| security-reviewer | ⚠️ Conditional | 0 | 3 | 1 | 0 |
| dba-reviewer | ⚠️ Conditional | 0 | 4 | 3 | 1 |
| qa-manager | ⚠️ Conditional | 0 | 2 | 1 | 0 |
| performance-reviewer | Pass | 0 | 0 | 3 | 1 |
| compliance-reviewer | ⚠️ Conditional | 0 | 2 | 1 | 0 |
| oss-reviewer | Pass | 0 | 0 | 1 | 1 |
| release-manager | Pass | 0 | 0 | 2 | 0 |
| infra-ops-reviewer | ⚠️ Conditional | 0 | 1 | 2 | 0 |
| audit-reviewer | Pass | 0 | 0 | 2 | 1 |
| ux-accessibility-reviewer | Pass | 0 | 0 | 0 | 1 |
| **合計** | | **0** | **21** | **25** | **7** |

## 判定根拠
- 判定ルール適用結果: Critical 指摘 0 件、High 指摘 21 件 → **⚠️ Conditional Approval**
- 最も重大な指摘: DB スキーマにおける `TIMESTAMP WITH TIME ZONE` 未使用、`mail_templates` テーブル定義の欠落、spec.md との Kafka イベント名不整合、PII ログ保護の具体策不足

---

## Critical/High 指摘一覧（修正必須）

| # | 重要度 | 出典 Agent | カテゴリ | 対象箇所 | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|---------|----------|----------|
| 1 | **High** | dba-reviewer | DB スキーマ | §4.2 mail_logs テーブル | `sent_at`, `created_at`, `updated_at` が `TIMESTAMP` と記載されているが、sql-schema-review.instructions.md の必須規約では **`TIMESTAMP WITH TIME ZONE`** を要求している。マイクロサービス環境でタイムゾーン非対応の `TIMESTAMP` はデータ不整合の原因となる | 全日時カラムを `TIMESTAMP WITH TIME ZONE` に修正。`DEFAULT CURRENT_TIMESTAMP` も併記する |
| 2 | **High** | dba-reviewer | DB スキーマ | §4 データモデル | **`mail_templates` テーブル定義が欠落**している。spec.md §11（メール送信サービス）では MailTemplate エンティティ（id, name, subject, htmlBody, textBody, templateType, variables, isActive, createdAt, updatedAt）が定義されているが、設計書には ERD にも DDL にもテーブル定義が存在しない。§6.2 でテンプレートを Razor ファイルとして管理する方針だが、spec.md のテンプレート管理 CRUD API と矛盾する | ① spec.md との整合方針を明確化（Razor ファイル方式 or DB 管理方式）② DB 管理方式を採用する場合は `mail_templates` テーブルの DDL を追加 ③ Razor 方式のみの場合は spec.md 側の管理 API 仕様を更新 |
| 3 | **High** | dba-reviewer | DB スキーマ | §4.2 mail_logs テーブル | `status` カラムに **CHECK 制約が未定義**。sql-schema-review.instructions.md ではステータスカラムに `CHECK (status IN (...))` が必須。設計書には `PENDING, SENDING, SENT, FAILED, SKIPPED` の 5 値が記載されているが DB 制約として定義されていない | `CONSTRAINT ck_mail_logs_status CHECK (status IN ('PENDING', 'SENDING', 'SENT', 'FAILED', 'SKIPPED'))` を追加 |
| 4 | **High** | dba-reviewer | DB スキーマ | §4.2 mail_logs テーブル | `retry_count` カラムに **CHECK 制約（`retry_count >= 0`）** が未定義。sql-schema-review.instructions.md では値域制約の DB 層での保証が必須 | `CONSTRAINT ck_mail_logs_retry_count CHECK (retry_count >= 0)` を追加 |
| 5 | **High** | architect | サービス間整合性 | §6.1, §12 | **Kafka イベント名の不整合**が多数存在。spec.md では `user.registered`, `order.created`, `order.shipped` だが、設計書では `USER_REGISTERED`, `OrderCreated`, `ShipmentStatusUpdated` と混在。同一イベントに複数の命名形式（snake_case、PascalCase、UPPER_SNAKE_CASE）が使われており統一されていない | Kafka イベント名の命名規約を統一し、spec.md と設計書で同一名を使用する。推奨: `dot.separated.lowercase`（`user.registered`, `order.created` 等）|
| 6 | **High** | architect | サービス間整合性 | §6.1 vs spec.md §11 | spec.md が定義する Kafka イベントのうち、**設計書でカバーされていないイベントが複数**存在: `payment.completed`（決済完了メール）、`payment.refunded`（返金通知メール）、`point.earned`（ポイント獲得通知）、`coupon.expired`（クーポン期限通知）、`inventory.low-stock`（在庫アラート）、`user.deleted`（アカウント削除完了）。spec.md との差分が明示されていない | ① 各イベントの対応可否を明示的に記載 ② Phase 分けで対応する場合は Phase 4 以降として記録 ③ Out of Scope とする場合はその理由を明記 |
| 7 | **High** | architect | アーキテクチャ | §8, §12 | **Outbox パターン**（ADR-0005）との関係が未記述。MailSendService 自身がイベントを発行する（§12.4: `MailSent`, `MailSendFailed`）が、これらのイベント発行で Outbox パターンを適用するか否かが不明。ADR-0005 は「全てのイベント発行に Outbox パターンを適用」と規定しているため整合性を確認する必要がある | Outbox パターンの適用方針を明記。MailSendService が発行するイベント（`MailSent`, `MailSendFailed`）にも Outbox パターンを適用するか、または MailSendService はイベント消費が主であるためスキップする場合はその理由を ADR-0005 の例外として記述 |
| 8 | **High** | security-reviewer | セキュリティ | §4.2 mail_logs テーブル | `recipient_email` を平文で DB に保存している。AGENTS.md §5.7 および security-coding.instructions.md では **PII（個人情報）のログ保護**が必須。メールアドレスは PII であり、mail_logs テーブルに平文保存すると GDPR/個人情報保護法上のリスクがある | ① PII の保存方針を明確化（暗号化 at rest、マスキング、保持期間と削除ポリシー）② メールアドレスの保存が業務上必須かを検討 ③ 必要な場合は暗号化フィールドとし、検索はハッシュインデックスで対応 |
| 9 | **High** | security-reviewer | セキュリティ | §7.1 管理 API | API パスが **`/api/v1/mail/...`** だが、AGENTS.md §6.1 および spec.md の管理 API 設計では `/admin/mail/...` パターンが使用されている。設計書内でも §7.1 と §15（API Gateway ルーティング）で異なるパスが混在する可能性がある | spec.md の管理 API パターン（`/admin/mail/...`）に統一するか、API バージョニングの方針を明記 |
| 10 | **High** | security-reviewer | セキュリティ | §13 セキュリティ設計 | **Razor テンプレートへの XSS 防止策**が未記述。メールテンプレートでユーザー入力（氏名、注文番号等）をレンダリングする際の HTML エスケープ方針がない。Razor のデフォルトエスケープに依存する旨の明示、または `Html.Raw()` の禁止ルールが必要 | ① テンプレート内のユーザー入力は Razor のデフォルトエスケープを利用する旨を明記 ② `Html.Raw()` / `MarkupString` の使用禁止を明記（AGENTS.md §5 準拠）|
| 11 | **High** | business-analyst | ビジネス要件 | §6.1 | spec.md §11 には **`MailSuppression`（配信停止リスト）** エンティティ（id, email, reason, suppressedAt）と `consent.revoked` イベントによる配信停止機能が定義されているが、設計書では完全に欠落している | ① `mail_suppressions` テーブル定義を追加 ② `consent.revoked` イベント購読と配信停止ロジックの設計を追加 ③ Phase 分けの対象とする場合はその旨を明記 |
| 12 | **High** | business-analyst | ビジネス要件 | §9.4 | **メール送信のレート制限**設計が Azure ACS 側のスロットリング対応のみ。spec.md §11 では「同一ユーザーへの配信頻度制限（1 時間に 5 通まで）」が要件として定義されているが、設計書に対応する実装方針がない | ① 同一受信者へのレート制限ロジック（1 時間 5 通制限）の設計を追加 ② `mail_logs` の `recipient_email` + `created_at` を活用した重複防止ロジックを記述 |
| 13 | **High** | compliance-reviewer | GDPR/個人情報 | §4, §13 | **メール送信履歴の保持期間・削除ポリシー**が未定義。mail_logs に PII（メールアドレス、氏名）を含むデータが蓄積され続ける。GDPR では保持期間の明示とデータ主体の削除要求（忘れられる権利）への対応が必須 | ① mail_logs の保持期間を定義（例: 90 日 or 1 年）② 古いレコードのアーカイブ/匿名化/削除バッチの設計を追加 ③ `user.deleted` イベント受信時の PII 削除/匿名化フローを記述 |
| 14 | **High** | compliance-reviewer | GDPR/個人情報 | §12 | メールの **HTML 本文**がどこにも保存されていない点は良いが、`recipient_email` と `recipient_name` が `mail_logs` に保存される。**データ最小化原則**への適合性が不明確 | PII の保存が監査要件上必要であることを明文化するか、保存せずに user_id のみを保持し必要時に UserManagementService から取得する方針に変更 |
| 15 | **High** | programing-reviewer | コーディング規約 | §8.1 | Kafka Consumer の DI 登録が **`AddSingleton<IConsumer<string, string>>`** で `ConsumerConfig` を直接構築している。AGENTS.md では接続文字列やブートストラップサーバーは環境変数管理が必須だが、`builder.Configuration["Kafka:BootstrapServers"]` で appsettings.json から取得する方式のみ記載。**`IOptions<T>` パターン**（AGENTS.md §10.1）の利用が推奨 | ① `KafkaSettings` 設定クラスを定義し `IOptions<KafkaSettings>` で注入 ② `ConsumerConfig` の構築を設定クラスベースに変更 |
| 16 | **High** | programing-reviewer | コーディング規約 | §12.3 | `UserInfoResolver` の `ResolveAsync` メソッドに **`CancellationToken ct = default`** が記載されているが、外部 HTTP 呼び出し失敗時のエラーハンドリングが不足。`GetFromJsonAsync` が `null` を返すケース（404）と例外を投げるケース（5xx）の区別がない | ① 404 時は `null` 返却（呼び出し元で `FAILED` 化）、5xx 時は例外スロー＋リトライのフローを明記 ② `ILogger<T>` でエラーログ出力パターンを記載 |
| 17 | **High** | qa-manager | テスト戦略 | §17 | テスト戦略のカバレッジ目標は記載があるが、**異常系テスト**の具体的なケースが不足。test-standards.instructions.md では「異常系テストは正常系と同等以上のテストケース数」が必須。以下のテストケースが欠落: ① Azure ACS が 429 を返す場合 ② メールアドレスが RFC 5322 非準拠の場合 ③ UserManagementService API が不達の場合 ④ Kafka メッセージのデシリアライズ失敗 | 異常系テストケース一覧を追加し、各ケースの期待動作（FAILED/SKIPPED/リトライ等）を明記 |
| 18 | **High** | qa-manager | テスト戦略 | §17 | **統合テスト**の記載が不十分。test-standards.instructions.md ではエンドポイントの統合テストに `WebApplicationFactory<Program>` の使用を推奨。管理 API エンドポイント 5 本の統合テスト方針（認証チェック含む）が未記載 | `WebApplicationFactory` + カスタム `AuthenticationHandler` での統合テスト設計を追加 |
| 19 | **High** | tech-lead | 技術標準 | §4 データモデル | **`mail_queue` テーブル**がユーザーリクエストで言及されているが設計書に定義が存在しない。現在の設計は `mail_logs.status = PENDING` でキューを代用しているが、この方針が意図的かどうかが不明確 | ① 専用の `mail_queue` テーブルが不要な理由（mail_logs の PENDING ステータスで代用）を設計判断として明記 ② または mail_queue テーブルを追加してキュー管理とログ管理を分離 |
| 20 | **High** | tech-lead | 規約適合 | §4.2 | ERD の `MailAttachment` エンティティに `created_at` / `updated_at` 監査カラムが欠落。spec.md の「監査カラム必須化ルール」により全エンティティに `created_at` / `updated_at` (`TIMESTAMP WITH TIME ZONE`) が必須 | `mail_attachments` テーブルに `created_at` と `updated_at` カラムを追加 |
| 21 | **High** | infra-ops-reviewer | コンテナ設計 | §16 | Docker Compose の環境変数に **`ConnectionStrings__DefaultConnection` に `Password=${DB_PASSWORD}` がインライン記述**されている。AGENTS.md では接続文字列にパスワードをインライン記述することは禁止であり、環境変数参照のみが許可される | パスワード部分を `${DB_PASSWORD}` 環境変数に分離するか（現在もそうなっているが `Host=postgres;...;Password=${DB_PASSWORD}` の形式が `.env` 参照前提かを明確化）、`.env.example` のサンプルを追記 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| 1 | 高優先 | architect | spec.md と設計書でメールプロバイダが異なる（spec.md: MailKit/Azure CS、設計書: Azure CS のみ）。開発環境での SMTP テスト方針（MailHog / SMTP4Dev）は設計書に記載があるが、MailKit の位置づけが曖昧 | テックリード |
| 2 | 高優先 | architect, business-analyst | テンプレート管理方式の選択: Razor ファイル（設計書 §6.2）vs DB 管理（spec.md §11 MailTemplate エンティティ）。両方式にはトレードオフがあり、運用要件に基づく判断が必要 | プロダクトオーナー + テックリード |
| 3 | 最優先 | compliance-reviewer | メール送信履歴（mail_logs）の PII 保持期間。法務チームによる GDPR / 個人情報保護法の要件確認が必要 | 法務チーム |
| 4 | 通常 | architect | ADR-0006（サービス別独立 DB）に基づき、MailSendService の DB 名は `maildb` であるべきだが、設計書 §5 および Docker Compose §16 では `skishopdb`（共有 DB）を参照している | テックリード |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|----------|----------|
| — | — | — | 競合なし | — | — |

---

## ドキュメント横断分析

### spec.md との整合性チェック

| 観点 | spec.md 定義 | 設計書記載 | 整合性 |
|------|------------|----------|--------|
| ポート番号 | 5008 | 5008 | ✅ |
| DB 名 | 言及なし（ADR-0006 では独立 DB） | skishopdb（共有） | ⚠️ ADR-0006 不整合 |
| メールプロバイダ | MailKit (SMTP) / Azure CS | Azure CS のみ | ⚠️ 部分一致 |
| テンプレート管理 | DB 管理（MailTemplate エンティティ、CRUD API） | Razor ファイル管理 | ❌ 不整合 |
| MailSuppression | 定義あり（エンティティ + consent.revoked） | 欠落 | ❌ 不整合 |
| 購読イベント（spec.md定義） | 9 種類 | 8 種類 | ⚠️ 差分あり |
| 管理 API パス | `/admin/mail/...` | `/api/v1/mail/...` | ❌ 不整合 |
| レート制限 | 1 時間に 5 通/ユーザー | Azure ACS スロットリングのみ | ❌ 不整合 |
| マーケティング配信 | In Scope（spec.md §11） | Out of Scope（設計書 §1.2） | ⚠️ スコープ差分（要確認） |

### 未定義・曖昧な領域

| # | 領域 | 影響度 | 内容 |
|---|------|--------|------|
| 1 | `mail_templates` テーブル | 高 | テンプレート管理方式が spec.md と設計書で矛盾 |
| 2 | `mail_queue` テーブル | 中 | キュー管理の設計判断が不明確 |
| 3 | `mail_suppressions` テーブル | 高 | 配信停止リスト機能が設計書に欠落 |
| 4 | PII 保持期間 | 高 | mail_logs の削除ポリシーが未定義 |
| 5 | Dead Letter Topic | 中 | Kafka Consumer で DLT 送付が「検討」のみ（設計未確定） |
| 6 | EF Core エンティティ定義 | 中 | `MailLog` の C# エンティティクラスが記載されていない（`[Table]`, `[Column]` 属性での snake_case マッピング例がない） |
| 7 | Program.cs ミドルウェア順序 | 中 | AGENTS.md §11.3 のミドルウェアパイプライン順序の記載がない |
| 8 | 独立 DB 名 | 中 | ADR-0006 に基づく `maildb` の設計が反映されていない |

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### 観点: ビジネス要件の完全性

#### High
1. **MailSuppression（配信停止リスト）が欠落**: spec.md §11 で定義された `MailSuppression` エンティティと `consent.revoked` イベント購読による配信停止機能が設計書に存在しない。GDPR ではユーザーの配信停止（オプトアウト）権利の保証が必須
2. **レート制限（ユーザー単位）が欠落**: spec.md で「同一ユーザーへの配信頻度制限（1 時間に 5 通まで）」が要件として定義されているが、設計書では Azure ACS 側のスロットリング対応のみ

#### Medium
3. **マーケティングメールのスコープ差分**: spec.md §11 ではマーケティングメールが In Scope だが、設計書 §1.2 で Out of Scope と明示。この差分が意図的かどうか不明
4. **spec.md から欠落しているイベント対応**: `payment.completed`, `payment.refunded`, `point.earned`, `coupon.expired`, `inventory.low-stock`, `user.deleted` の 6 イベントが設計書に記載なし

#### Low
5. テンプレートのプレビュー機能（テストメール送信 §7.1 はあるが、管理画面でのプレビュー表示は言及なし）

</details>

<details>
<summary>architect レビューレポート</summary>

### 観点: アーキテクチャ設計の整合性

#### High
1. **Kafka イベント名の命名不統一**: `USER_REGISTERED`（UPPER_SNAKE_CASE）、`OrderCreated`（PascalCase）、`user.verified`（dot.lowercase）が混在。イベント名は Producer 側で定義されるため、MailSendService の設計書として消費するイベント名を正確に記載する必要がある
2. **spec.md 定義イベントの未カバー**: 6 つのイベント（payment.completed 等）が設計書で言及されていない
3. **Outbox パターンの適用方針未記述**: ADR-0005 でプロジェクト全体に Outbox パターンが必須とされているが、MailSendService が発行するイベント（MailSent, MailSendFailed）での適用有無が不明

#### Medium
4. **DB 独立性**: ADR-0006 では `Database per Service` が承認済みだが、設計書は `skishopdb` を参照。MailSendService 用の `maildb` とすべきか確認が必要
5. **サービス間通信の耐障害性**: UserManagementService への HTTP 呼び出し（§12.3）で `AddStandardResilienceHandler()` を使用しているが、具体的なリトライパラメータ（最大リトライ回数、バックオフ戦略）が未記載

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 観点: 技術標準の横断適合性

#### High
1. **`mail_queue` テーブルの設計判断が不明確**: 現在の設計は `mail_logs.status = PENDING` でキューを代用しているが、この方針が意図的かどうかが記載されていない。大量メール送信時のパフォーマンスへの影響も未評価
2. **`MailAttachment` に監査カラム欠落**: spec.md の全エンティティ監査カラム必須化ルールに違反

#### Medium
3. **EF Core エンティティの C# クラス定義が未記載**: AGENTS.md §10.3 では `[Table("snake_case")]`, `[Column("snake_case")]` 属性での明示的マッピング、コレクションナビゲーションの `= []` 初期化が必須。設計書に MailLog, MailAttachment の C# エンティティ定義がない
4. **Program.cs のミドルウェアパイプライン順序が未記載**: AGENTS.md §11.3 で厳密な順序が規定されているが、設計書に反映されていない
5. **BackgroundService（MailEventConsumer）の IServiceScopeFactory パターン**: AGENTS.md §10.6 で BackgroundService は `IServiceScopeFactory` で Scoped サービスを取得するパターンが必須とされているが、設計書の Consumer 実装例ではこのパターンが明示されていない

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 観点: コード例の正確性・規約準拠

#### High
1. **IOptions<T> パターン未使用**: Kafka Consumer の DI 登録で `builder.Configuration["Kafka:BootstrapServers"]` を直接参照。AGENTS.md §10.1 では `IOptions<T>` パターンが推奨
2. **UserInfoResolver のエラーハンドリング不足**: HTTP 呼び出しの 404/5xx 区別がない

#### Medium
3. **MailLogResponse DTO の日時型**: `DateTime` が使用されているが、AGENTS.md §10.3 では `DateTime.UtcNow` または `DateTimeOffset.UtcNow` の使用が推奨。DTO で `DateTimeOffset` を使用するとタイムゾーン情報が保持される
4. **TestMailRequest の Variables プロパティ**: `Dictionary<string, object>?` はデシリアライズ時に型安全性が低い。`JsonElement` または具体的な型を検討

#### Low
5. MailLogResponse の `Status` が `string` 型。enum 型を使用するか、record 内でのバリデーション方針が不明

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 観点: OWASP Top 10、認証/認可、秘密情報管理

#### High
1. **PII（メールアドレス）の平文保存**: mail_logs テーブルに `recipient_email` が平文で保存。暗号化 at rest やマスキング方針が未定義
2. **管理 API パスの不整合**: spec.md の `/admin/mail/...` に対して設計書は `/api/v1/mail/...` を使用。API Gateway のルーティング設定（§15）との整合性も不明確
3. **XSS 防止策が未記述**: Razor テンプレートでのユーザー入力エスケープ方針がない

#### Medium
4. **テストメール送信 API のアビューズ防止**: `/api/v1/mail/test`（POST）は ADMIN 限定だが、レート制限が未設定。悪意ある管理者による大量テストメール送信のリスク

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 観点: DB スキーマ設計・EF Core マッピング

#### High
1. **`TIMESTAMP WITH TIME ZONE` 未使用**: 全日時カラムが `TIMESTAMP` と記載。sql-schema-review.instructions.md では `TIMESTAMP WITH TIME ZONE` が必須
2. **`mail_templates` テーブル定義が欠落**: spec.md で定義された MailTemplate エンティティの DDL がない
3. **status カラムの CHECK 制約欠如**: `PENDING, SENDING, SENT, FAILED, SKIPPED` の値域制約が DB 層で未保証
4. **retry_count の CHECK 制約欠如**: 非負制約 `retry_count >= 0` が未定義

#### Medium
5. **`mail_attachments` テーブルの DDL が欠落**: ERD には MailAttachment が存在するが、テーブル定義（カラム型、制約、インデックス）が記載されていない
6. **主キー型の選択**: `VARCHAR(36)` を PK に使用。sql-schema-review.instructions.md では `UUID` 型（PostgreSQL ネイティブ）/ `CHAR(36)` が推奨。`VARCHAR(36)` は可変長のため不必要なオーバーヘッドがある
7. **外部キー制約の ON DELETE 動作が未記載**: `mail_attachments.mail_log_id` の FK に `ON DELETE CASCADE` を適用すべきか（mail_log 削除時に添付ファイル情報も連鎖削除）が不明

#### Low
8. インデックス `idx_mail_logs_status` は低カーディナリティ（5 値）のため効果が限定的。部分インデックス `WHERE status IN ('PENDING', 'FAILED')` の方が効率的

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 観点: テスト戦略・カバレッジ

#### High
1. **異常系テストケースの不足**: Azure ACS 429、RFC 5322 非準拠メールアドレス、UserManagement API 不達、Kafka デシリアライズ失敗、Razor テンプレートレンダリングエラー等の異常系が列挙されていない
2. **統合テスト設計の不足**: `WebApplicationFactory<Program>` + カスタム `AuthenticationHandler` での管理 API 統合テスト方針がない

#### Medium
3. **テストメソッド命名規約への言及なし**: test-standards.instructions.md の `Should_期待結果_When_条件` パターンへの準拠が未確認

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 観点: パフォーマンス SLA・スケーラビリティ

#### Medium
1. **バッチメール送信時のスループット設計がない**: 大量注文（セール時等）で同時に多数の `OrderCreated` イベントが発生した場合の処理キャパシティが未定義
2. **Kafka Consumer の並列処理設計が不足**: Consumer のパーティション数・Consumer Group のスケーリング方針が記載されていない
3. **Azure ACS の送信レート制限値が未記載**: Sandbox/本番環境での具体的な制限値（通/秒、通/日）と、それに基づくキャパシティプランニングがない

#### Low
4. テンプレートレンダリングのキャッシュ方針（コンパイル済み Razor テンプレートのメモリキャッシュ等）が未記載

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 観点: GDPR・個人情報保護法・PCI DSS

#### High
1. **PII 保持期間と削除ポリシーの未定義**: mail_logs に蓄積される `recipient_email`, `recipient_name` の保持期間が明示されていない。GDPR Article 5(1)(e) のストレージ制限原則に違反する可能性
2. **データ主体のアクセス権・削除権への対応不足**: `user.deleted` イベント受信時の PII 削除/匿名化フローが設計されていない

#### Medium
3. **マーケティングメールの同意管理**: Out of Scope としているが、将来の拡張を見据えた同意管理（`consent_type`, `consent_status`）のデータモデルの検討が推奨

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 観点: NuGet ライセンス・依存関係

#### Medium
1. **Azure.Communication.Email パッケージ**: NuGet パッケージ一覧に `Azure.Communication.Email 1.*` が記載されているが、AGENTS.md の必須パッケージ一覧に含まれていない。プロジェクト固有の依存として妥当だが、ライセンス（MIT）の確認記録がない

#### Low
2. **Azure.Identity パッケージ**: バージョン `1.*` が記載。Microsoft.Identity.Web 3.* との依存関係の整合性を確認すべき

</details>

<details>
<summary>release-manager レビューレポート</summary>

### 観点: リリース戦略・ロールバック計画

#### Medium
1. **Phase 分けと他サービスの変更依存**: §18（実装優先度）と §19（既存サービスへの変更）の関係が明確だが、AuthService への変更（§19.1 VerificationToken 追加）と MailSendService Phase 1 の同時リリースの調整方針が未記載
2. **ロールバック計画**: MailSendService のデプロイ失敗時、Kafka イベントが消費されない場合のメール送信バックログの処理方針が未記載

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 観点: コンテナ設計・可観測性・DR

#### High
1. **Docker Compose の接続文字列パスワード**: `Password=${DB_PASSWORD}` の参照元（`.env` ファイル）の管理方針が不明確

#### Medium
2. **Dockerfile が設計書に記載されていない**: AGENTS.md §12.5 の Dockerfile 規約（マルチステージビルド、非 root ユーザー、HEALTHCHECK）に準拠した Dockerfile 設計が欠落
3. **ヘルスチェックの Azure ACS 接続確認**: §14.2 で Azure Communication Services のヘルスチェックが記載されているが、具体的な実装方針（ACS の ping エンドポイント or アクセストークン取得テスト）が未記載

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 観点: トレーサビリティ・ドキュメント整合性

#### Medium
1. **Correlation ID の伝搬設計**: §4.2 に `correlation_id` カラムがあるが、Kafka イベントヘッダーから Correlation ID を抽出して MailLog に記録するフローが未記載。AGENTS.md §11.2 では全リクエストに Correlation ID を付与し伝搬することが必須
2. **ADR の参照不足**: 設計書内で ADR-0004（Kafka）、ADR-0005（Outbox）、ADR-0006（独立 DB）への明示的な参照がない。設計判断の根拠が不明確

#### Low
3. `mail_logs` テーブルに `created_by` / `updated_by` 監査カラムがない。イベント駆動のため「作成者」はシステムとなるが、手動リトライ（§7.1 POST /retry）の場合は操作者の記録が望ましい

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 観点: UX 設計・WCAG 2.1・メールアクセシビリティ

#### Low
1. **メールの多言語対応**: spec.md ではバックエンドの多言語化（`.resx` + `IStringLocalizer`）が規定されているが、メールテンプレートの多言語対応（日本語/英語）の方針が設計書に記載されていない。ユーザーの言語設定に基づくテンプレート選択ロジックの検討が推奨

</details>
