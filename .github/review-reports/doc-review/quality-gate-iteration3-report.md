# ドキュメント品質ゲート結果 — Iteration 3

## 判定
- **対象**: 全 11 マイクロサービス設計書
- **判定**: ⚠️ 修正完了・再レビュー推奨
- **実施日時**: 2026-04-03 16:14
- **イテレーション**: 3 / 5

## イテレーション 3 修正前レビュー結果

| # | 設計書 | Critical | High | Medium | Low | 判定 |
|---|--------|----------|------|--------|-----|------|
| 1 | api-gateway-design.md | 0 | 0 | 13 | 11 | ✅ Approved |
| 2 | payment-cart-service-design.md | 0 | 8 | 16 | 8 | ⚠️ |
| 3 | point-service-design.md | 0 | 9 | 13 | 8 | ⚠️ |
| 4 | sales-management-design.md | 1 | 7 | - | - | ⚠️ |
| 5 | ai-support-service-design.md | 0 | 11 | - | - | ⚠️ |
| 6 | authentication-service-design.md | 0 | 16 | 20 | 9 | ⚠️ |
| 7 | coupon-service-design.md | 0 | 10 | 19 | 6 | ⚠️ |
| 8 | front-end-need.md | 0 | 5 | 13 | 9 | ⚠️ |
| 9 | inventory-management-design.md | 0 | 9 | 17 | 9 | ⚠️ |
| 10 | user-management-design.md | 0 | 10 | - | - | ⚠️ |
| 11 | mailsend-service-design.md | 0 | 12 | 19 | 6 | ⚠️ |
| | **合計** | **1** | **97** | - | - | |

## イテレーション 3 修正サマリー

| 設計書 | 修正 Critical | 修正 High | 合計 | 修正後行数 |
|--------|-------------|----------|------|-----------|
| sales-management-design.md | 1 | 7 | 8 | 4,035 |
| authentication-service-design.md | 0 | 16 | 16 | 3,733 |
| payment-cart-service-design.md | 0 | 8 | 8 | 3,057 |
| point-service-design.md | 0 | 9 | 9 | 4,173 |
| ai-support-service-design.md | 0 | 11 | 11 | 4,424 |
| coupon-service-design.md | 0 | 10 | 10 | 3,561 |
| inventory-management-design.md | 0 | 9 | 9 | 4,581 |
| user-management-design.md | 0 | 10 | 10 | 3,516 |
| mailsend-service-design.md | 0 | 12 | 12 | 2,821 |
| front-end-need.md | 0 | 5 | 5 | 2,348 |
| **合計** | **1** | **97** | **98** | **36,205** |

## 修正詳細（サービス別）

### 1. sales-management-design.md（Critical 1 + High 7）

| # | 指摘ID | 重要度 | 修正内容 |
|---|--------|--------|---------|
| 1 | C-01 | Critical | `spec.md 更新提案` セクション追加。11 ステータスの必要性根拠を ADR-0009 参照付きで明記 |
| 2 | H-01 | High | ADR-0009 Saga ステップ数不整合の警告注記追加 |
| 3 | H-02 | High | ShipmentItem 省略の設計判断根拠と Phase 2 移行パス明記 |
| 4 | H-03 | High | ClosedXML → EPPlus 7.* に方針変更（プレリリース版禁止） |
| 5 | H-04 | High | ゲスト購入カラム（is_guest, guest_email）を 3 箇所に追加 |
| 6 | H-05 | High | ORDER_CANCEL/ORDER_RETURN Saga の詳細設計追加 |
| 7 | H-06 | High | spec.md エンティティ属性差異の注記追加 |
| 8 | H-07 | High | GDPR 匿名化対象フィールド一覧テーブル追加 |

### 2. authentication-service-design.md（High 16）

| # | 指摘ID | 重要度 | 修正内容 |
|---|--------|--------|---------|
| 1 | H-01 | High | spec.md エンティティ名対応表追加（AuthUser↔User 等） |
| 2 | H-02 | High | RefreshToken に family_id/previous_token_id 追加、Replay Detection 実装 |
| 3 | H-03 | High | リフレッシュトークン有効期限を 7日→14日に統一（spec.md 準拠） |
| 4 | H-04 | High | ロールモデル対応表追加（Customer↔USER 等） |
| 5 | H-05 | High | password_histories テーブル・エンティティ・Repository 追加 |
| 6 | H-06 | High | メール検証エンドポイント追加 |
| 7 | H-07 | High | oauth_accounts に access_token/refresh_token カラム追加 |
| 8 | H-08 | High | user_roles に assigned_at/assigned_by カラム追加 |
| 9 | H-09 | High | DSR イベントハンドラー（UserDeleted）追加 |
| 10 | H-10 | High | security_logs PII 保持期間ポリシー追加 |
| 11 | H-11 | High | Client Credentials エンドポイント追加 |
| 12 | H-12 | High | Npgsql DateTime 設定方針明記 |
| 13 | H-13 | High | EmailAddress Value Object Phase 2 計画明記 |
| 14 | H-14 | High | ユーザー登録の主体を AuthService と確定 |
| 15 | H-15 | High | AuditLogInterceptor 実装設計追加 |
| 16 | H-16 | High | エンティティ対応表で全概念を網羅的に定義 |

### 3. payment-cart-service-design.md（High 8）

| # | 指摘ID | 重要度 | 修正内容 |
|---|--------|--------|---------|
| 1 | H1 | High | ADR-0009 との SSOT 注記追加 |
| 2 | H2 | High | cart.proto に ClearCart RPC 追加 |
| 3 | H3 | High | Webhook IP ホワイトリスト多層防御設計追加 |
| 4 | H4 | High | webhook レート制限ポリシー追加 |
| 5 | H5 | High | Payment エンティティに RowVersion（楽観的ロック）追加 |
| 6 | H6 | High | PII 匿名化をソルト付き SHA-256 に修正 |
| 7 | H7 | High | transactions に監査カラム追加 |
| 8 | H8 | High | ゲスト購入 Saga ステップスキップロジック追加 |

### 4. point-service-design.md（High 9）

| # | 指摘ID | 重要度 | 修正内容 |
|---|--------|--------|---------|
| 1 | H-01 | High | FIFO ポイント消費ロジック追記 |
| 2 | H-02 | High | Saga ステップ用語を spec.md 準拠で統一 |
| 3 | H-03 | High | gRPC サービス名統一（PointService） |
| 4 | H-04 | High | point_expiries.status CHECK 制約に CONSUMED 追加 |
| 5 | H-05 | High | gRPC に InternalServiceOnly 認証ポリシー追加 |
| 6 | H-06 | High | gRPC/Outbox/バッチの統合テスト設計追記 |
| 7 | H-07 | High | N+1 クエリを Eager Loading に修正 |
| 8 | H-08 | High | データ保持期間・DSR 対応方針追記 |
| 9 | H-09 | High | point_audit_logs テーブル・エンティティ追記 |

### 5. ai-support-service-design.md（High 11）

| # | 指摘ID | 重要度 | 修正内容 |
|---|--------|--------|---------|
| 1 | H-01 | High | spec.md エンティティ対応表追加 |
| 2 | H-02 | High | spec.md インデックス定義の不一致注記追加 |
| 3 | H-03 | High | ProductIndexSyncConsumer を IServiceScopeFactory 経由に修正 |
| 4 | H-04 | High | ToolCallBehavior → FunctionChoiceBehavior.Auto() に更新 |
| 5 | H-05 | High | OrderPlugin IDOR 防止（userId プリセット）追加 |
| 6 | H-06 | High | ResponseFilter 呼出し箇所明記、漏洩対策強化 |
| 7 | H-07 | High | ForecastService に FunctionChoiceBehavior.None() 追加 |
| 8 | H-08 | High | Mermaid 図から未実装 ChatPlugin を削除 |
| 9 | H-09 | High | DataRetentionCleanupService に browsing_history 除去追加 |
| 10 | H-10 | High | JSONB プロパティに TypeName/HasColumnType 追加 |
| 11 | H-11 | High | Advisory Lock の boolean 戻り値取得を修正 |

### 6. coupon-service-design.md（High 10）

| # | 指摘ID | 重要度 | 修正内容 |
|---|--------|--------|---------|
| 1 | H-01 | High | InternalServiceOnly 認証ポリシー + Handler 追加 |
| 2 | H-02 | High | OutboxPublisher に Advisory Lock（pg_try_advisory_lock）追加 |
| 3 | H-03 | High | gRPC Proto パッケージを skishop.coupon.v1 に統一 |
| 4 | H-04 | High | CouponCodeGenerator インターフェース・実装追加 |
| 5 | H-05 | High | DateRange Value Object 追加 |
| 6 | H-06 | High | gRPC に Authorize("InternalServiceOnly") 追加 |
| 7 | H-07 | High | OutboxPublisher ステータス遷移を 3 段階に修正 |
| 8 | H-08 | High | gRPC 統合テスト 4 件追加 |
| 9 | H-09 | High | order.cancelled べき等性保証パターン追記 |
| 10 | H-10 | High | DTO 日時型を DateTimeOffset に統一 |

### 7. inventory-management-design.md（High 9）

| # | 指摘ID | 重要度 | 修正内容 |
|---|--------|--------|---------|
| 1 | H-01 | High | SKU/バリエーション管理 Phase 1 方針 + Phase 2 移行計画追記 |
| 2 | H-02 | High | PUT/DELETE/ImageUpload エンドポイント追加 |
| 3 | H-03 | High | Saga ステップ番号 SSOT 注記追加 |
| 4 | H-04 | High | inventory.product_id UNIQUE 制約の設計根拠と移行パス明記 |
| 5 | H-05 | High | 画像アップロード API + ファイルバリデーション追加 |
| 6 | H-06 | High | gRPC InternalServiceOnly + mTLS 設計追記 |
| 7 | H-07 | High | 低在庫アラート E2E フロー（Mermaid シーケンス図付き）追記 |
| 8 | H-08 | High | 並行在庫引当コンカレンシーテスト設計追記 |
| 9 | H-09 | High | reviews.user_id DSR 匿名化ポリシー + UserDeletedConsumer 追記 |

### 8. user-management-design.md（High 10）

| # | 指摘ID | 重要度 | 修正内容 |
|---|--------|--------|---------|
| 1 | H-01 | High | password.changed Kafka 購読イベント追加 |
| 2 | H-02 | High | Redis キャッシュ戦略定義 + NuGet + AppHost 追加 |
| 3 | H-03 | High | GDPR Art.20 エクスポート JSON スキーマ追加 |
| 4 | H-04 | High | 管理者操作監査ログ要件追加 |
| 5 | H-05 | High | ウィッシュリスト→カート移動 API 追加 |
| 6 | H-06 | High | Advisory Lock boolean 戻り値取得修正 |
| 7 | H-07 | High | Polly リトライ/サーキットブレーカー設計追加 |
| 8 | H-08 | High | DataExportService テスト計画追加 |
| 9 | H-09 | High | DSR 削除完了通知フロー追加 |
| 10 | H-10 | High | AuthService 責務分担注記追加 |

### 9. mailsend-service-design.md（High 12）

| # | 指摘ID | 重要度 | 修正内容 |
|---|--------|--------|---------|
| 1 | H-01 | High | mail_logs.status CHECK 制約を拡張（BOUNCED/SKIPPED 追加） |
| 2 | H-02 | High | template_id FK 追加 |
| 3 | H-03 | High | recipient_user_id カラム追加（DSR 用） |
| 4 | H-04 | High | consent.revoked イベント処理設計追加 |
| 5 | H-05 | High | user.deleted PII 仮名化を Phase 1 に前倒し |
| 6 | H-06 | High | user.processing-restricted イベント処理追加 |
| 7 | H-07 | High | テンプレート CRUD API 追記 |
| 8 | H-08 | High | イベント名差分の設計判断ノート追加 |
| 9 | H-09 | High | セキュリティヘッダー 2 種追加 |
| 10 | H-10 | High | DDD Aggregate Root 定義追加 |
| 11 | H-11 | High | PII 保持期間を 90 日→1 年に修正（spec.md 準拠） |
| 12 | H-12 | High | インデックス定義を spec.md と統一 |

### 10. front-end-need.md（High 5）

| # | 指摘ID | 重要度 | 修正内容 |
|---|--------|--------|---------|
| 1 | H-01 | High | TypeScript SPA → Razor Pages + C# に全面書換え |
| 2 | H-02 | High | API パスプレフィックス /api/v1/ 統一方針注記追加 |
| 3 | H-03 | High | CSP unsafe-inline の nonce 移行計画追記 |
| 4 | H-04 | High | テストフレームワークを Vitest + Playwright に確定 |
| 5 | H-05 | High | 同意管理 API パラメータ名を {consentType} に統一 |

## エスカレーション事項（テックリード判断が必要）

| # | サービス | 内容 |
|---|---------|------|
| 1 | sales-management | spec.md の `orders.status` CHECK 制約を 8→11 ステータスに更新する提案（C-01 対応として設計書側に根拠を追記済み） |
| 2 | authentication | spec.md のエンティティ名（AuthUser/OAuthClient）と設計書（User/OAuthAccount）の統一方針決定 |
| 3 | ai-support | spec.md の Aggregate Root（UserInteraction）と設計書（ChatSession）の統一方針決定 |
| 4 | inventory | SKU/バリエーション管理の Phase 2 移行方針承認 |
| 5 | user-management | spec.md の `passwordHash` 属性削除提案（AuthService 責務） |

## 次のステップ

1. **再レビュー（Iteration 4）**: 修正後の 10 ドキュメントに対して orchest-doc-review を再実行し、Critical/High が 0 件になったことを確認
2. **spec.md 更新**: エスカレーション事項のテックリード承認後、spec.md 側の更新を実施
3. **ADR-0009 更新**: Saga ステップ数を 6→9 に改訂
