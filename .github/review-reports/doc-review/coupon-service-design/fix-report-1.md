# Fix Report: coupon-service-design.md

## 概要
- **対象**: `design-docs/coupon-service-design.md`
- **基準レポート**: `check-report-1.md`
- **修正日時**: 2026-04-03
- **修正者**: Auto-Fix Agent (Phase 4)

---

## Critical 修正結果: 2/2 完了

| # | 指摘 ID | 内容 | 対応内容 | 状態 |
|---|---------|------|----------|------|
| 1 | C1 | 全テーブルの日時カラムが `TIMESTAMP` で `TIMESTAMP WITH TIME ZONE` が未使用 | campaigns / coupons / user_coupons / coupon_usages の全日時カラム（start_date, end_date, valid_from, valid_until, acquired_at, used_at, created_at, updated_at）を `TIMESTAMP WITH TIME ZONE` に修正。新規追加テーブル（coupon_types, coupon_restrictions, promotions, outbox_events）も全て `TIMESTAMP WITH TIME ZONE` で定義 | ✅ 修正済 |
| 2 | C2 | spec.md の `CouponType`, `CouponRestriction`, `Promotion` エンティティが欠落 | 3 エンティティのテーブル定義を §5.2 に追加。ER 図（§5.1）にリレーション追加。FK 制約（CouponRestriction → Coupon: CASCADE）、CHECK 制約、インデックスを定義。プロジェクト構成（§15）に Models / Repository を追加 | ✅ 修正済 |

---

## High 修正結果: 8/8 完了

| # | 指摘 ID | 内容 | 対応内容 | 状態 |
|---|---------|------|----------|------|
| 1 | H1 | gRPC サービス定義（coupon.proto）の欠落 | §10「gRPC サービス定義（Saga ステップ 3）」を新設。coupon.proto（ValidateCoupon / ReleaseCoupon RPC）、メッセージ型定義、gRPC サーバー実装パターン、Deadline 設計（300ms）を追加。Program.cs での `MapGrpcService<T>` 登録例を記載 | ✅ 修正済 |
| 2 | H2 | Outbox パターン（ADR-0005）の欠落 | §11「Outbox パターン（ADR-0005 準拠）」を新設。outbox_events テーブル定義（§5.2）、OutboxPublisher BackgroundService 実装（動的バックオフ 100ms〜5s）、Outbox イベント書き込みパターン（トランザクション内）を追加。プロジェクト構成に BackgroundServices/ ディレクトリ追加 | ✅ 修正済 |
| 3 | H3 | coupons テーブルの CHECK 制約欠落 | `ck_coupons_discount_value`, `ck_coupons_min_order_amount`, `ck_coupons_max_usage_count`, `ck_coupons_discount_type` の 4 CHECK 制約を追加。制約名は `ck_` プレフィックスで命名 | ✅ 修正済 |
| 4 | H4 | `FREE_SHIPPING` 割引タイプの欠落 | (1) coupons テーブルの discount_type 説明に `FREE_SHIPPING` 追加 (2) CHECK 制約に `FREE_SHIPPING` 追加 (3) CouponRuleEngine.CalculateDiscount の switch 式に `FREE_SHIPPING` ケース追加 (4) FluentValidation に `FREE_SHIPPING` 追加 | ✅ 修正済 |
| 5 | H5 | `DateTime.UtcNow` 直接使用（TimeProvider DI 未使用） | CouponRuleEngine / FraudDetectionService の primary constructor に `TimeProvider` を DI 注入。`DateTime.UtcNow` → `timeProvider.GetUtcNow()` に置換。テストコードも `TimeProvider.System` を使用するよう修正 | ✅ 修正済 |
| 6 | H6 | DB 名が `skishopdb`（ADR-0006 違反） | §3 サービス情報テーブル、§4.1 コンポーネントアーキテクチャ図の `skishopdb` → `coupondb` に修正 | ✅ 修正済 |
| 7 | H7 | Kafka トピック名の不整合（PascalCase vs ドット区切り） | §9.1 発行イベントを spec.md トピック一覧と整合: `coupon.applied`（CouponRedeemed を統合）、`coupon.expired` の 2 トピックに整理。§9.2 購読イベントに `user.deleted` トピック追加。イベントペイロード定義を `CouponAppliedEvent` / `CouponExpiredEvent` に修正。Outbox パターン経由の発行を明記 | ✅ 修正済 |
| 8 | H8 | クーポン・ポイント適用順序ルールの欠落 | §21.1「クーポン・ポイント適用順序（spec.md H8-23 準拠）」を追加。6 ステップの適用順序テーブル、制約ルール（送料無料判定基準、ポイント充当範囲、ゲスト購入スキップ、0 円決済スキップ）を記載 | ✅ 修正済 |

---

## 追加修正（Medium 同時対応）

| # | 指摘 ID | 内容 | 対応内容 | 状態 |
|---|---------|------|----------|------|
| 1 | M3 | coupon_usages テーブルに `updated_at` カラム欠落 | `created_at`, `updated_at` カラムを追加（TIMESTAMP WITH TIME ZONE） | ✅ 修正済 |
| 2 | M4 | campaigns.status の CHECK 制約なし | `ck_campaigns_status: CHECK (status IN ('DRAFT', 'ACTIVE', 'PAUSED', 'ENDED'))` 追加 | ✅ 修正済 |
| 3 | M5 | user_coupons.status の CHECK 制約なし | `ck_user_coupons_status: CHECK (status IN ('AVAILABLE', 'USED', 'EXPIRED'))` 追加 | ✅ 修正済 |
| 4 | — | user_coupons テーブルに `updated_at` カラム欠落 | 監査カラム必須ルール準拠で `updated_at` を追加 | ✅ 修正済 |

---

## セクション番号の変更

gRPC（§10）・Outbox（§11）セクション挿入に伴い、以下のセクション番号が変更:

| 旧番号 | 新番号 | セクション名 |
|--------|--------|------------|
| §10 | §12 | キャッシュ戦略 |
| §11 | §13 | セキュリティ設計 |
| §12 | §14 | エラーコード |
| §13 | §15 | プロジェクト構成 |
| §14 | §16 | 監視・メトリクス |
| §15 | §17 | テスト戦略 |
| §16 | §18 | 設定ファイル |
| §17 | §19 | Dockerfile |
| §18 | §20 | バッチ処理 |
| §19 | §21 | 制約・前提条件 |

---

## 未修正（Medium/Low — 次回対応推奨）

| # | 指摘 ID | 重要度 | 内容 | 理由 |
|---|---------|--------|------|------|
| 1 | M1 | Medium | 配布サービス（DistService）の追加 | Phase 判断が必要（エスカレーション E1 対象） |
| 2 | M2 | Medium | UserCoupon テーブルの位置づけ明確化 | spec.md との調整が必要 |
| 3 | M6 | Medium | DTO の DateTime → DateTimeOffset 変更 | ER 図で DateTimeOffset に更新済み。コード例は次回修正 |
| 4 | M7 | Medium | EF Core エンティティ完全定義（属性付き） | テーブル定義で補完済み。コード例は実装フェーズで対応 |
| 5 | M8-M9 | Medium | テストケース追加（FraudDetection, DB スライス） | テスト設計の拡充は次回対応 |
| 6 | M10 | Medium | Redis フォールバック実装例 | §21 制約 5 で方針記載済み。コード例は次回 |
| 7 | M11-M16 | Medium | PII ログ方針、Correlation ID、ミドルウェア順序等 | 横断的設計。全サービス共通で対応推奨 |
| 8 | L1-L3 | Low | UUID 型、default 値、curl → wget | 任意改善 |

---

## エスカレーション事項（据え置き）

| # | 内容 | 判断者 |
|---|------|--------|
| E1 | CouponType / CouponRestriction / Promotion の Phase 1 スコープ判断 — テーブル定義は追加済みだが、Service / Repository / Endpoints の実装範囲は要判断 | プロダクトオーナー / テックリード |
| E2 | gRPC + REST 二重公開方針 — §10 で gRPC 定義を追加済みだが、実装優先度は要判断 | テックリード |
| E3 | E2E テストシナリオ E6 のテスト範囲 | QA リード |

---

## サマリー

| カテゴリ | 修正前 | 修正後 | 残数 |
|---------|--------|--------|------|
| **Critical** | 2 | 0 | **0** |
| **High** | 8 | 0 | **0** |
| **Medium（対応済）** | — | 4 | 12 残 |
| **Low** | 3 | 0 | 3 残 |

**判定**: Critical 0 / High 0 により、**Conditional Approval → Approval** への格上げを推奨。
