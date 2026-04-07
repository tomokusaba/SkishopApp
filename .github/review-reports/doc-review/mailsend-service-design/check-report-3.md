# ドキュメントレビュー統合レポート — mailsend-service-design.md

## 判定結果
- **対象**: `design-docs/mailsend-service-design.md`
- **判定**: ⚠️ **Conditional Approval** — Critical 指摘なし、High 指摘あり。人間の判断を介在させて是正/受容を決定
- **レビュー日時**: 2026-04-03
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)
- **イテレーション**: 3 回目（check-report-3）

## 技術スタック検証結果

| カテゴリ | 設計書記載 | AGENTS.md 定義 | 整合性 |
|---------|-----------|---------------|--------|
| ランタイム | C# 14 / .NET 10 | C# 14 / .NET 10 | ✅ |
| フレームワーク | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| ORM | EF Core 10 | EF Core 10 | ✅ |
| DB | PostgreSQL (mailsenddb) | PostgreSQL (ADR-0006) | ✅ |
| メッセージング | Confluent.Kafka 2.* | Confluent.Kafka 2.* | ✅ |
| メールプロバイダ | Azure Communication Services Email | spec.md §11 記載: MailKit / Azure CS | ✅ |
| テンプレートエンジン | Razor 10.* | spec.md §技術スタック: Razor | ✅ |
| バリデーション | FluentValidation 11.* | FluentValidation 11.* | ✅ |
| ログ | Serilog.AspNetCore 8.* | Serilog.AspNetCore 8.* | ✅ |
| 可観測性 | OpenTelemetry 1.* | OpenTelemetry 1.* | ✅ |
| テスト | xUnit, NSubstitute, Shouldly, Testcontainers | xUnit, NSubstitute, Shouldly, Testcontainers | ✅ |

## 指摘サマリー

| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ⚠️ Conditional | 0 | 2 | 1 | 0 |
| architect | ⚠️ Conditional | 0 | 1 | 2 | 0 |
| programing-reviewer | ✅ Pass | 0 | 0 | 2 | 1 |
| dba-reviewer | ⚠️ Conditional | 0 | 3 | 1 | 0 |
| security-reviewer | ⚠️ Conditional | 0 | 1 | 2 | 0 |
| compliance-reviewer | ⚠️ Conditional | 0 | 3 | 1 | 0 |
| audit-reviewer | ✅ Pass | 0 | 0 | 2 | 0 |
| qa-manager | ✅ Pass | 0 | 0 | 2 | 1 |
| performance-reviewer | ✅ Pass | 0 | 0 | 2 | 1 |
| infra-ops-reviewer | ✅ Pass | 0 | 0 | 1 | 1 |
| release-manager | ✅ Pass | 0 | 0 | 1 | 0 |
| oss-reviewer | ✅ Pass | 0 | 0 | 0 | 1 |
| ux-accessibility-reviewer | ✅ Pass | 0 | 0 | 1 | 1 |
| tech-lead | ⚠️ Conditional | 0 | 2 | 1 | 0 |
| **合計** | | **0** | **12** | **19** | **6** |

## 判定根拠
- **Critical 指摘**: 0 件（自動 Rejected の条件に該当しない）
- **High 指摘**: 12 件（spec.md との整合性不備、GDPR イベントハンドリング欠落、データモデル差分）
- 判定ルール適用: High 指摘のみ（Critical なし）→ ⚠️ Conditional Approval
- 最も重大な指摘: spec.md との `mail_logs` スキーマ差分（status enum、templateId FK、recipientUserId カラム欠落）および GDPR 関連イベント（`consent.revoked`, `user.deleted`, `user.processing-restricted`）の未対応

---

## Critical/High 指摘一覧（修正必須）

| # | 重要度 | 出典 Agent | カテゴリ | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|----------|----------|
| H-01 | **High** | dba-reviewer, tech-lead | データモデル整合性 | `mail_logs.status` の CHECK 制約が spec.md と不一致。設計書: `('PENDING','SENDING','SENT','FAILED','SKIPPED')`、spec.md: `('QUEUED','SENT','FAILED','BOUNCED')`。`PENDING` vs `QUEUED`、`SENDING` 追加、`BOUNCED` → `SKIPPED` の差分が未説明 | spec.md が SSOT。設計書の状態値が spec.md と異なる理由を明示的に記載し、spec.md 側を更新するか設計書を修正する。`BOUNCED` ステータスの扱い（バウンスを `mail_suppressions` で管理するため不要としたか）を設計判断として記録する |
| H-02 | **High** | dba-reviewer, tech-lead | データモデル整合性 | spec.md の `MailLog` エンティティには `templateId`（FK → MailTemplate）が定義されているが、設計書の `mail_logs` テーブルには `templateId` カラムが存在しない。代わりに `template_name`（VARCHAR）を使用しており、FK 制約が欠落 | spec.md の FK 制約設計（`MailLog.templateId → MailTemplate, ON DELETE RESTRICT`）に合わせ、`template_id` FK カラムを追加するか、`template_name` による参照を設計判断として spec.md に反映する |
| H-03 | **High** | dba-reviewer | データモデル整合性 | spec.md の `MailLog` には `recipientUserId` カラムが定義されているが、設計書の `mail_logs` にはこのカラムが存在しない。DSR 処理（`user.deleted` 時の `recipientUserId → NULL 化`）に必要 | `recipient_user_id VARCHAR(36)` カラムを `mail_logs` テーブルに追加する。GDPR DSR 処理で `userId` ベースの匿名化に必須 |
| H-04 | **High** | compliance-reviewer | GDPR イベント未対応 | spec.md §同意撤回処理フローで定義されている `consent.revoked` イベント（type: MARKETING）の購読が設計書に未記載。MailSendService は「該当ユーザーをマーケティング配信リストから除外。配信予約済みメールをキャンセル」する責務がある | §6.1 の Kafka イベント購読一覧と §8.2 のイベントハンドリングフローに `consent.revoked` イベントの処理を追加する。`mail_suppressions` テーブルへの登録またはユーザー別のマーケティング配信停止フラグの管理を設計する |
| H-05 | **High** | compliance-reviewer | GDPR イベント未対応 | spec.md §DSR リクエスト処理フローで定義されている `user.deleted` イベントの購読が設計書に未記載（Phase 4 として先送り）。しかし spec.md では MailSendService が `mail_logs` の `recipientEmail`/`recipientUserId` を仮名化する責務が明確に定義されている | `user.deleted` イベントの処理を Phase 1 に前倒しするか、Phase 4 で対応する場合は GDPR 30 日期限との整合性を明確にする。少なくともイベントハンドラーの設計（仮名化ロジック、mail_suppressions へのハッシュ登録）を本設計書に含める |
| H-06 | **High** | compliance-reviewer | GDPR イベント未対応 | spec.md §処理制限権で定義されている `user.processing-restricted` / `user.processing-unrestricted` イベントの購読が設計書に未記載。処理制限中のユーザーへのマーケティングメール送信停止が必要 | `user.processing-restricted` イベント受信時にマーケティングメール送信を停止するロジックを設計に追加する |
| H-07 | **High** | business-analyst | 機能網羅性 | spec.md §11 で定義されているテンプレート管理 CRUD API（`GET/POST/PUT/DELETE /admin/mail/templates`）が spec.md のエンドポイント一覧に含まれている。設計書 §7.1 にはテンプレート管理 API が含まれていないが、§26 の Endpoint 実装で追加されている。しかし §7.1 の管理用 REST API 一覧と §26 の実装に不一致がある（§7.1 に templates CRUD が未記載） | §7.1 の管理用 REST API 一覧にテンプレート管理エンドポイント（GET/POST/PUT/DELETE `/admin/mail/templates`）を追加し、§26 の実装と整合させる |
| H-08 | **High** | business-analyst | Kafka イベント整合性 | spec.md §11 では `order.shipped` イベントが定義されているが、設計書では `shipment.status.updated` (status=SHIPPED) を使用。イベント名の差分が未説明。また spec.md で `payment.completed`, `payment.refunded`, `point.earned`, `coupon.expired`, `inventory.low-stock` が定義されているが、Phase 4 先送りの判断根拠が不明確 | イベント名の差分（`order.shipped` vs `shipment.status.updated`）を設計判断として明記する。Phase 4 先送りイベントについて、ビジネスインパクトと Phase 1 MVP スコープとの関係を記載する |
| H-09 | **High** | security-reviewer | セキュリティヘッダー | §27 の Program.cs でセキュリティヘッダーに `Referrer-Policy` と `Permissions-Policy` が欠落。AGENTS.md §5（`.github/instructions/security-coding.instructions.md`）では 6 種のセキュリティヘッダーが必須 | `context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin")` および `context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()")` を追加する |
| H-10 | **High** | architect | Aggregate Root 定義 | spec.md の Aggregate Root 一覧でメール送信サービスの Aggregate Root は `MailTemplate` と定義されているが、設計書では `MailLog` が事実上の中心エンティティとして機能している。`MailLog` を Aggregate Root として定義するか、Repository 設計と Aggregate Root の関係を明確にすべき | `MailLog` と `MailTemplate` の両方を Aggregate Root として定義し、それぞれの Repository が Aggregate Root 単位であることを §4 または新セクションで明記する。現状 Repository は適切に分離されている（§23）ためコード変更は不要だが、設計書上の DDD 整合性を確保する |
| H-11 | **High** | tech-lead | PII 保持期間不一致 | 設計書 §4.2 の PII 保護方針では「90 日間保持」後に匿名化と記載されているが、spec.md §データ保持ポリシーでは「メール送信ログ（MailLog）: 1 年」と定義されている。SSOT である spec.md と矛盾 | spec.md の「1 年」に合わせて §4.2 の保持期間を 1 年に修正するか、spec.md を更新する。§28.3 の PiiCleanupService の `-90` 日を `-365` 日に変更する |
| H-12 | **High** | dba-reviewer | spec.md インデックス差分 | spec.md のメール送信サービスインデックス設計では `mail_logs.template_id` (B-Tree)、`mail_logs.recipient_user_id` (B-Tree)、`mail_suppressions.(email, suppression_type)` (UNIQUE) が定義されているが、設計書のインデックスとカラム名が異なる | spec.md のインデックス設計に合わせてカラム名とインデックスを統一する。特に `recipient_user_id` と `template_id` の追加に伴うインデックス定義を追加する |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-01 | 高優先 | compliance-reviewer | `user.deleted` イベント処理を Phase 4 に先送りしている。GDPR 第 17 条（削除の権利）は Phase 1 リリース時点で必須。先送りの場合、GDPR 違反リスクの受容判断が必要 | PO + 法務チーム |
| E-02 | 高優先 | dba-reviewer, tech-lead | `mail_logs` テーブルのスキーマが spec.md と複数箇所で異なる。SSOT（spec.md）を更新するか設計書を修正するかの方針決定が必要 | テックリード |
| E-03 | 通常 | business-analyst | カート放棄メール（`cart.abandoned` イベント）が spec.md §カート放棄メール設計に記載されているが、設計書では対応していない。Phase 1 スコープに含めるか判断が必要 | PO |
| E-04 | 通常 | performance-reviewer | `mail_logs` テーブルでレート制限チェックを毎回実行しているが、高頻度送信時にクエリ負荷が懸念される。Redis キャッシュによる最適化要否の判断 | テックリード |

---

## 競合解決記録

| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| 1 | dba-reviewer（spec.md 準拠: templateId FK 必須） | architect（template_name 文字列参照で疎結合化） | `mail_logs` テーブルの MailTemplate 参照方法 | **dba-reviewer を優先**: spec.md が SSOT であり `templateId` FK を追加すべき。ただし `template_name` も保持して可読性を維持する併用方式を推奨 | spec.md の FK 制約設計が明確に定義されており、SSOT の原則に従う |
| 2 | compliance-reviewer（PII 保持 1 年: spec.md 準拠） | security-reviewer（PII 短期保持: 90 日で十分） | `mail_logs` の PII 保持期間 | **compliance-reviewer を優先**: spec.md §データ保持ポリシーが SSOT であり 1 年間保持を遵守。ただし 90 日経過後の PII にはアクセス制限強化（管理者のみ閲覧可能）を追加推奨 | 法規制・監査要件が技術的判断に優先する原則 |

---

## ドキュメント横断分析

### spec.md 定義項目との整合性チェック

| spec.md 定義項目 | 設計書対応 | 整合性 | 備考 |
|----------------|-----------|--------|------|
| MailTemplate エンティティ定義 | §4 / §21.2 | ⚠️ 部分一致 | spec.md の `htmlBody` → 設計書 `html_body` (snake_case) は OK。`variables` が JSONB 定義あり ✅ |
| MailLog エンティティ定義 | §4 / §21.1 | ❌ 不一致 | `templateId` FK 欠落、`recipientUserId` 欠落、`status` enum 差分 |
| MailSuppression エンティティ | §4 | ✅ 一致 | 属性・制約とも合致 |
| テンプレート管理 API | §26 | ✅ 一致 | GET/POST/PUT/DELETE all covered |
| 送信ログ API | §7.1 / §26 | ✅ 一致 | GET/POST(retry)/GET(stats) |
| テストメール API | §7.1 / §26 | ✅ 一致 | `POST /admin/mail/test` |
| リトライ制御（指数バックオフ最大 3 回） | §8.4 | ✅ 一致 | 30s → 60s → 120s |
| 配信頻度制限（1 時間 5 通） | §9.5 | ✅ 一致 | セキュリティメール除外あり |
| Kafka Consumer (BackgroundService) | §8 / §28.1 | ✅ 一致 | IServiceScopeFactory 使用、stoppingToken 伝搬 |
| Outbox パターン | §12.4 | ✅ 一致 | ADR-0005 準拠 |
| 配信停止対応 | §4 (mail_suppressions) | ⚠️ 部分 | テーブルはあるが `consent.revoked` イベント処理が未定義 |

### 記載カバレッジ分析

| 設計領域 | カバレッジ | 評価 |
|---------|----------|------|
| 概要・スコープ | §1 | ✅ 十分 |
| 技術スタック | §2 | ✅ 十分 |
| アーキテクチャ図 | §3 | ✅ 十分（Mermaid 図複数あり） |
| データモデル | §4 / §21 | ⚠️ spec.md との差分あり |
| API 設計 | §7 / §26 | ⚠️ §7.1 にテンプレート CRUD 未記載 |
| イベント消費設計 | §8 / §12 / §28.1 | ✅ 十分 |
| Azure ACS 連携 | §9 | ✅ 十分 |
| プロジェクト構成 | §10 | ✅ 十分 |
| 設定ファイル | §11 | ✅ 十分 |
| イベントペイロード | §12 | ✅ 詳細に記載 |
| セキュリティ | §13 | ⚠️ ヘッダー不足 |
| 監視・運用 | §14 | ✅ 十分 |
| テスト戦略 | §17 | ✅ 十分 |
| 既存サービス変更 | §19 | ✅ 詳細に記載 |
| エラーハンドリング | §22 | ✅ 十分 |
| Repository / Service IF | §23 / §24 | ✅ 十分 |
| FluentValidation | §25 | ✅ 十分 |
| Endpoint 実装 | §26 | ✅ 十分 |
| Program.cs | §27 | ⚠️ セキュリティヘッダー不足 |
| BackgroundService | §28 | ✅ 十分 |
| ミドルウェア順序 | §29 | ✅ AGENTS.md 準拠 |
| GDPR イベント対応 | 未記載 | ❌ `consent.revoked`, `user.deleted`, `user.processing-restricted` 未対応 |

### 未定義・曖昧な領域

| # | 領域 | 影響度 | 備考 |
|---|------|--------|------|
| 1 | `user.deleted` イベント処理のフルフローが未定義 | High | GDPR DSR 対応に必須 |
| 2 | `consent.revoked` イベント処理が未定義 | High | マーケティングメール停止ロジック |
| 3 | SendGrid DSR 伝搬（spec.md §外部プロセッサー DSR 設計） | Medium | spec.md では SendGrid API 呼出し責務が MailSendService に割り当てられている |
| 4 | 在庫復活通知メール（spec.md §ウィッシュリスト） | Medium | spec.md で「MailSendService 経由で在庫復活通知メールを送信」と記載あり |
| 5 | データエクスポート完了通知メール（spec.md §データポータビリティ権） | Medium | 設計書のメール種別に未掲載 |
| 6 | ゲスト購入者向け注文確認メール（spec.md §ゲスト購入フロー） | Medium | `guestEmail` 宛への送信フローが未設計 |
| 7 | BackgroundService リーダー選出（spec.md §BackgroundService リーダー選出パターン） | Low | MailRetryService / PiiCleanupService の Advisory Lock 管理が未記載 |

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### 観点: ビジネス要件の完全性・ユーザーストーリー・受入基準

#### High 指摘
1. **H-07**: §7.1 の管理用 REST API 一覧にテンプレート管理 CRUD が含まれていない（§26 の実装には存在）。API 仕様の一覧性が損なわれている
2. **H-08**: spec.md §11 で定義されている Kafka イベント名（`order.shipped`）と設計書のイベント名（`shipment.status.updated`）の差分が未説明

#### Medium 指摘
1. spec.md §カート放棄メール設計で `cart.abandoned` イベントによるカート放棄リマインドメールが定義されているが、設計書のイベント購読一覧（§6.1 / §8.2）に含まれていない。Phase 区分も不明確

#### 良好な点
- メール種別の網羅性は Phase 1-3 の段階的スコープで明確に定義されている
- テンプレート設計（Razor ファイルベース + DB メタデータ管理のハイブリッド方式）は実用的
- 各イベントペイロードの詳細定義（§12）と既存サービスへの変更影響（§19）が丁寧に記載されている

</details>

<details>
<summary>architect レビューレポート</summary>

### 観点: マイクロサービス分割・Bounded Context・DDD パターン

#### High 指摘
1. **H-10**: spec.md の Aggregate Root 一覧では MailTemplate のみが Aggregate Root と定義されているが、設計書では MailLog が中心的なエンティティとして機能している。Repository 設計（§23）は適切に Aggregate Root 単位で分離されているが、DDD の明示的な定義が不足

#### Medium 指摘
1. UserInfoResolver（§12.3）が Kafka イベント処理中に同期 HTTP 呼出しを行っている。メール送信トリガーは非同期（Kafka イベント駆動）であるため問題は少ないが、UserManagementService 障害時の可用性影響が懸念される。フォールバック戦略（Redis キャッシュ等）の検討を推奨
2. `EventEnvelope` の設計（§28.1）でペイロードを `string PayloadJson` として保持しているが、型安全性が低い。`JsonElement` の使用または型別デシリアライズのディスパッチャーパターンを検討

#### 良好な点
- コンポーネントアーキテクチャ図（§3）が明確で、依存方向が正しい（Consumer → Service → Template/Sender/Repo）
- Outbox パターンの適用（§12.4）が ADR-0005 に準拠
- レイヤードアーキテクチャの依存方向が正しい（Endpoints → Services → Repositories）

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 観点: コード例の正確性・C# 14 / .NET 10 機能活用・禁止パターン

#### Medium 指摘
1. §28.1 `EventEnvelope` の `PayloadJson` プロパティに `[JsonPropertyName("payload")]` が付与されているが、`string` 型での受信はペイロードの二重デシリアライズが必要になる。`JsonElement` 型で受信し、必要時に型変換する方式が型安全
2. §28.3 `PiiCleanupService` の `HashEmail` メソッドがソルトなしの SHA-256 を使用しているが、spec.md §各サービスの削除対象データでは「ソルト付き SHA-256」が指定されている（ただし MailSendService は `recipientEmail → SHA-256 ハッシュ化` と記載されており、ソルトの明示的な指定はないため Low に近い）

#### Low 指摘
1. §22.1 の例外クラスで `RateLimitExceededException` と `SuppressedRecipientException` のコンストラクタに `recipientEmail` を受けているが、メッセージに「PII はログ出力しないこと」とコメントがある。コンストラクタで PII を受け取る設計自体がリスク。email のハッシュ値のみ受け取る設計を推奨

#### 良好な点
- primary constructor の活用（§28.1-28.3 全 BackgroundService）
- CancellationToken の全メソッドへの伝搬（§23-24 全インターフェース）
- record 型の DTO / 設定クラス活用（§7.2, §8.1, §24.4）
- `IServiceScopeFactory` による Scoped サービスの適切な取得（§28.1-28.3）

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 観点: DB スキーマ設計・EF Core マッピング・マイグレーション安全性

#### High 指摘
1. **H-01**: `mail_logs.status` CHECK 制約の値が spec.md と不一致
2. **H-02**: `mail_logs.template_id` FK カラム欠落（spec.md の FK 制約設計に違反）
3. **H-12**: spec.md のインデックス設計で定義されている `template_id` インデックスと `recipient_user_id` インデックスが設計書に存在しない

#### Medium 指摘
1. §21.1 の AppDbContext で `SaveChangesAsync` オーバーライド内のエンティティ型チェックが `if-else if` チェーンで実装されている。共通インターフェース（`IAuditableEntity`）の導入で DRY 化を推奨

#### 良好な点
- snake_case カラム名の徹底（`[Column("snake_case")]` 属性）
- CHECK 制約の Fluent API 定義（§21.1）
- UNIQUE インデックスによる冪等性保証（`event_id`）
- 部分インデックスの検討（`idx_mail_logs_status`）
- JSONB 型の `variables` カラム定義

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 観点: OWASP Top 10・認証/認可設計・秘密情報管理

#### High 指摘
1. **H-09**: §27 Program.cs のセキュリティヘッダーに `Referrer-Policy` と `Permissions-Policy` が欠落。`security-coding.instructions.md` で 6 種必須と定義

#### Medium 指摘
1. §9.5 のレート制限チェック（セキュリティメール除外）で、除外対象が「パスワードリセット、メール認証」と記載されているが、`user.email_changed`（メールアドレス変更確認）もセキュリティ上重要であり除外対象に含めるべきか検討が必要
2. §7.1 のテストメール API（`POST /admin/mail/test`）のレート制限（1 時間 5 通）の実装方法が不明確。Endpoint レベルでのレート制限デコレーターまたは Service 層での制御を明記すべき

#### 良好な点
- Managed Identity による Azure ACS 認証（§9.1）— ハードコード禁止の原則を遵守
- XSS 防止策の明確な規定（§13.3）— `Html.Raw()` 禁止
- メールアドレスの RFC 5322 バリデーション（§13.4）
- JWT 認証の `ClockSkew = 5 分` 設定（§27）
- `FallbackPolicy` による全エンドポイント認証必須化（§27）

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 観点: GDPR・個人情報保護法・PCI DSS・データガバナンス

#### High 指摘
1. **H-04**: `consent.revoked` イベント処理が未定義
2. **H-05**: `user.deleted` イベント処理が Phase 4 に先送りされており GDPR リスク
3. **H-06**: `user.processing-restricted` イベント処理が未定義

#### Medium 指摘
1. spec.md §外部プロセッサー DSR 設計で、SendGrid への DSR 伝搬が MailSendService の責務と定義されているが、設計書では Azure Communication Services を使用しており SendGrid は使用していない。外部プロセッサーへの DSR 伝搬設計（Azure Communication Services のデータ削除方法）を記載すべき

#### 良好な点
- PII 保護方針の明記（§4.2）— 保持期間・匿名化バッチ・user.deleted 対応（Phase 4）
- `mail_suppressions` テーブルによる配信停止管理
- TDE（透過的データ暗号化）の有効化方針

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 観点: トレーサビリティ・ドキュメント整合性・ADR 参照

#### Medium 指摘
1. 管理者操作（テンプレート CRUD、手動リトライ）の監査ログ出力が未設計。管理 API 操作の監査証跡（誰が・いつ・何を操作したか）を `ILogger` またはAuditLog テーブルで記録する設計を追加推奨
2. §14.1 のメトリクス定義で `mail.event.consumed.total` のタグに `eventType` が含まれているが、`producer` タグの追加を推奨（どのサービスからのイベントかを区別可能にする）

#### 良好な点
- Correlation ID の伝搬（§27 ミドルウェア + §4 mail_logs.correlation_id カラム）
- event_id による冪等性保証と重複検知ログ
- ADR-0004/0005/0006 への明示的な参照
- OpenTelemetry による分散トレーシング設定（§27）

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 観点: テスト戦略・カバレッジ目標・検証可能性

#### Medium 指摘
1. §17.1 の単体テスト計画に `PiiCleanupService` のテストが含まれていない。90 日経過レコードの匿名化ロジックは GDPR 要件に直結するため、テスト必須
2. §17.3 の異常系テストケースに「UserManagementService が処理制限中ユーザーの情報を返した場合」のケースが欠落（GDPR 処理制限対応時に必要）

#### Low 指摘
1. §17.2 で「Kafka イベント消費はインメモリテスト用プロバイダで検証」と記載されているが、具体的なテスト用プロバイダの実装方針が不明確

#### 良好な点
- テストメソッド命名規約（`Should_期待結果_When_条件`）の準拠（§17.2）
- 異常系テストケースの網羅性（10 ケース定義）
- 統合テスト計画（WebApplicationFactory + カスタム AuthenticationHandler）
- Testcontainers.PostgreSql の使用方針

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 観点: パフォーマンス SLA・スケーラビリティ・キャッシュ戦略

#### Medium 指摘
1. §9.5 のレート制限チェックで毎回 `mail_logs` テーブルに COUNT クエリを発行している。高頻度のイベント処理時に DB 負荷が懸念される。Redis カウンター（TTL 1 時間）による最適化を推奨
2. §28.2 `MailRetryService` のポーリング間隔が動的バックオフ（100ms〜5s）だが、spec.md §BackgroundService リーダー選出パターンで推奨されている PostgreSQL Advisory Lock が未適用。複数レプリカ稼働時の二重リトライリスク

#### Low 指摘
1. §28.3 `PiiCleanupService` のバッチサイズ（500 件）は妥当だが、大量レコード蓄積時のページング処理が不明確（500 件処理後の残存レコードへの対応）

#### 良好な点
- 非同期 Kafka Consumer によるイベント駆動（Polling ではない）
- `AsNoTracking()` の読み取り専用クエリでの活用（§23 の Repository 実装で暗黙的に推奨）
- Azure ACS の LRO ポーリング対応（§9.2）

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 観点: コンテナ設計・可観測性・ヘルスチェック・DR

#### Medium 指摘
1. §14.2 のヘルスチェックで「Azure Communication Services エンドポイント接続チェック」が記載されているが、§27 の Program.cs HealthChecks 登録に ACS のヘルスチェックが含まれていない。`AddUrlGroup()` 等による ACS エンドポイントの到達性チェックを追加推奨

#### Low 指摘
1. §16 Docker Compose で `profiles: [app]` が設定されているが、デフォルトプロファイルでの起動方法が不明確

#### 良好な点
- PostgreSQL + Kafka のヘルスチェック登録（§27）
- Liveness / Readiness エンドポイントの分離（§27）
- Docker Compose の環境変数管理（`.env.example` 提供）
- OpenTelemetry のトレーシング + メトリクス統合（§27）

</details>

<details>
<summary>release-manager レビューレポート</summary>

### 観点: リリース戦略・ロールバック計画・バージョニング

#### Medium 指摘
1. §18 の実装優先度（Phase 1-3）は明確だが、各 Phase のリリース条件（Gate 基準）が未定義。Phase 1 リリース時に必要な最小限の機能セット（メール認証 + パスワードリセット + 注文確認）の受入基準を明記推奨

#### 良好な点
- Phase 分割が明確（Phase 1: MVP, Phase 2: 運用品質, Phase 3: 全機能）
- §19 の既存サービス変更の詳細な影響分析
- §20 の制約・前提条件の明記

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 観点: NuGet ライセンス適合性・依存関係脆弱性・禁止パッケージ

#### Low 指摘
1. §2 の主要ライブラリ一覧に `Azure.Communication.Email` のバージョンが `1.*` と記載されているが、本パッケージは比較的新しいため、最新安定版の具体的なバージョン（例: `1.0.1`）を確認し、既知の CVE がないことを検証推奨

#### 良好な点
- 全パッケージが AGENTS.md §8 の許可リストに準拠
- 禁止パッケージ（`Newtonsoft.Json`, `log4net`, `-preview` 版）の使用なし
- `System.Text.Json` の使用（§28.1 `JsonSerializer`）

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 観点: UX 設計品質・WCAG 2.1 準拠・レスポンシブ設計

#### Medium 指摘
1. §6.2 のテンプレート共通要素に「レスポンシブ HTML メール（モバイル対応）」が記載されているが、メールのアクセシビリティ要件（画像の alt 属性、セマンティック HTML、テーブルレイアウトのアクセシビリティ、color contrast ratio）が未定義。メールテンプレートの WCAG 2.1 Level A 準拠ガイドラインを追加推奨

#### Low 指摘
1. §6.2 のテンプレートファイル構成に多言語対応（日英）の設計が含まれていない。spec.md §ビジネス要件で「多言語対応（日本語・英語）」が定義されているため、テンプレートの多言語化方針（ロケール別テンプレートファイル or 動的切替）を記載推奨

#### 良好な点
- フッターへのプライバシーポリシーリンク記載（§6.2）
- プレーンテキストフォールバックの自動生成方針（§6.2）

</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 観点: 技術標準の横断適合性・実装実現可能性・規約遵守

#### High 指摘
1. **H-10**: DDD の Aggregate Root 定義が spec.md と設計書で整合していない
2. **H-11**: PII 保持期間が spec.md（1 年）と設計書（90 日）で不一致

#### Medium 指摘
1. 設計書全体の品質は高く、実装に必要な情報（エンティティ定義、Repository/Service IF、Endpoint 実装、Program.cs、BackgroundService）が網羅されている。ただし GDPR 関連イベント（H-04〜H-06）の設計が不足しており、Phase 1 リリース時の法的リスクが残る

#### 総合評価
設計書は§1〜§29 + 追記セクション（§21〜§29）で実装に必要な情報を高い粒度で提供しており、自動実装可能なレベルに達している。主な改善点は (1) spec.md とのデータモデル整合性、(2) GDPR イベントハンドリングの設計追加、(3) PII 保持期間の統一 の 3 点に集約される。

</details>
