# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/front-end-need.md`（1138 行、修正後イテレーション 2）
- **判定**: ⚠️ **Conditional Approval** — High 指摘あり、人間の判断を介在
- **レビュー日時**: 2026-04-03 17:30
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)
- **参照ドキュメント**: `spec.md`（§8 フロントエンド / §チェックアウト・ステップインジケーター設計 / §特定商取引法対応 / §ゲスト購入フロー）, `AGENTS.md`, `copilot-instructions.md`, ADR-0008 (PCI DSS), ADR-0009 (Saga), `.github/instructions/*.instructions.md`
- **前回レポート**: `check-report-1.md`（0 Critical / 18 High）→ `fix-report-1.md`（18 High 全修正済）

---

## イテレーション 2 サマリー

| 区分 | check-report-1 | fix-report-1 後 | check-report-2（本レポート） |
|------|---------------|-----------------|---------------------------|
| Critical | 0 | 0 | **0** |
| High | 18 | 0（全修正） | **3**（全て新規） |
| Medium | 21 | 21（未修正） | **25**（carryover 20 + new 5） |
| Low | 4 | 4（未修正） | **6**（carryover 4 + M-12 降格 + new 1） |

### 前回 High 18 件の是正検証

| # | 前回指摘 | 是正状況 | 検証結果 |
|---|---------|---------|---------|
| H-01 | Next.js バージョン不一致（15→14） | §1.2 を `Next.js 14 (App Router)` に修正、spec.md 準拠注記追加 | ✅ 解決 |
| H-02 | 状態管理ライブラリ矛盾 | `Redux Toolkit / Zustand` に統一、spec.md 準拠 | ✅ 解決 |
| H-03 | 管理画面技術スタック矛盾 | §3 冒頭に Razor Pages 注記追加 | ✅ 解決 |
| H-04 | Core Web Vitals FID → INP | §8.1 を `INP ≤ 200ms` に修正 | ✅ 解決 |
| H-05 | ゲスト購入フロー未記載 | `/checkout/guest` セクション新設 | ✅ 解決 |
| H-06 | 越境データ移転同意 UI 未記載 | ゲスト購入フロー内に実装 | ✅ 解決 |
| H-07 | 特定商取引法表示ページ未記載 | §2.1 に `/legal/tokushoho` 等追加 | ✅ 解決 |
| H-08 | チェックアウトフロー 5 ステップ未分割 | 5 ステップに分割、URL パス定義済 | ✅ 解決（但し API フロー整合に新規 High あり） |
| H-09 | Stripe Hosted Payment Page 未反映 | Stripe Checkout Session + リダイレクトフロー追加 | ✅ 解決（但し Mermaid 図に新規 High あり） |
| H-10 | Wishlist API パス不一致 | `/wishlists` に統一、§9.2a 新設 | ✅ 解決 |
| H-11 | JWT httpOnly Cookie vs getAccessToken() 矛盾 | `withCredentials: true` に修正、getAccessToken() 削除 | ✅ 解決（但し §5.1/§5.2 図に新規 High あり） |
| H-12 | TTFB 目標値の位置づけ | 独自目標と注記追加 | ✅ 解決 |
| H-13 | テスト戦略セクション未定義 | §8.5 新設 | ✅ 解決 |
| H-14 | API クライアントライブラリ不一致 | `TanStack Query v5 + axios` に明確化 | ✅ 解決 |
| H-15 | WCAG 2.1 AA 要件簡略化 | §8.2 大幅拡充 | ✅ 解決 |
| H-16 | フロントエンド Correlation ID 生成 | バックエンド委譲に修正 | ✅ 解決 |
| H-17 | Saga パターン非同期処理 UI 未定義 | ポーリング・タイムアウト UX 追加 | ✅ 解決 |
| H-18 | i18n ライブラリ未指定 | §7.0 新設（next-intl + 翻訳キー規則） | ✅ 解決 |

> **結論**: 前回 High 18 件は全て適切に是正されていることを確認した。

---

## 技術スタック検証結果

| カテゴリ | front-end-need.md 記載 | spec.md 定義 | AGENTS.md 定義 | 整合性 |
|---------|----------------------|-------------|---------------|--------|
| フレームワーク | Next.js 14 (App Router) | Next.js（React） | — | ✅ |
| 言語 | TypeScript 5.x | TypeScript 5 | — | ✅ |
| 状態管理 | Redux Toolkit / Zustand | Redux/Zustand | — | ✅ |
| API 通信 | TanStack Query v5 + axios | Axios, React Query | — | ✅ |
| フォーム管理 | React Hook Form + Zod | — | — | ✅（spec.md 未定義） |
| UI コンポーネント | Tailwind CSS + shadcn/ui | TailwindCSS, Headless UI | — | ⚠️ shadcn/ui vs Headless UI（M-01 carryover） |
| テスト | Vitest + Testing Library + Playwright | Jest, Cypress | — | ⚠️ エスカレーション E-05 済 |
| i18n | next-intl | next-intl | — | ✅ |
| 管理画面 | Razor Pages（注記済） | ASP.NET Core + Razor Pages | — | ✅ |
| パフォーマンス | INP ≤ 200ms | INP ≤ 200ms | — | ✅ |
| JWT 保存 | httpOnly Cookie (SameSite=Strict, Secure) | httpOnly Cookie | — | ✅（但し §5.1/§5.2 図に矛盾 → H2-01） |
| Stripe | Hosted Payment Page (ADR-0008) | Hosted Payment Page | — | ✅ |

---

## 指摘サマリー

| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | Pass | 0 | 0 | 2 | 1 |
| architect | ⚠️ Conditional | 0 | 2 | 2 | 0 |
| programing-reviewer | Pass | 0 | 0 | 2 | 1 |
| dba-reviewer | Pass | 0 | 0 | 1 | 0 |
| security-reviewer | ⚠️ Conditional | 0 | 1 | 2 | 0 |
| compliance-reviewer | Pass | 0 | 0 | 2 | 0 |
| audit-reviewer | Pass | 0 | 0 | 2 | 0 |
| qa-manager | Pass | 0 | 0 | 1 | 0 |
| performance-reviewer | Pass | 0 | 0 | 2 | 1 |
| infra-ops-reviewer | Pass | 0 | 0 | 1 | 1 |
| release-manager | Pass | 0 | 0 | 1 | 0 |
| oss-reviewer | Pass | 0 | 0 | 1 | 0 |
| ux-accessibility-reviewer | Pass | 0 | 0 | 2 | 1 |
| tech-lead | ⚠️ Conditional | 0 | 0 | 2 | 1 |
| **合計** | | **0** | **3** | **25** | **6** |

---

## 判定根拠
- **Critical 指摘**: 0 件 → 自動 Rejected にはならない
- **High 指摘**: 3 件（全て新規 — 修正時の副作用）→ **Conditional Approval**
- **前回 High 18 件**: 全て是正済 ✅
- **最も重大な指摘**: §5.1/§5.2 認証フロー図と httpOnly Cookie 方針の矛盾、チェックアウト API フロー/Mermaid 図のステップ番号誤りによる WCAG SC 3.3.4 準拠リスク

---

## Critical/High 指摘一覧（修正必須）

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|---------------|----------|----------|
| H2-01 | **High** | security-reviewer, architect | セキュリティ整合性 | §5.1 / §5.2 JWT 認証フロー | **認証フロー図が httpOnly Cookie 方針と矛盾**: §4.4 API 通信設定と §5.3 JWT トークン仕様では `httpOnly Cookie (SameSite=Strict, Secure)` によるトークン管理が明記されているが、§5.1 の ASCII シーケンス図には `Authorization: Bearer {JWT}` がリクエストヘッダーに付与される記述が残存している（3 箇所）。§5.2 トークンリフレッシュフロー図でも `Authorization: Bearer {期限切れ}` および `{ refreshToken }` をリクエストボディで送信する記述が残っている。httpOnly Cookie は JavaScript からアクセス不可能であり、`Authorization` ヘッダーに手動設定する設計は成立しない。前回 H-11 で §4.4 のコード例は是正されたが、§5.1/§5.2 の図は更新されておらず**実装時の混乱を招く** | §5.1 の認証済みリクエスト部分を「Cookie: access_token={JWT}（自動送信）」に変更。§5.2 のリフレッシュリクエスト部分を「POST /auth/refresh（Cookie でリフレッシュトークン自動送信）」に変更。`Authorization: Bearer` の記述を全て削除する |
| H2-02 | **High** | architect, ux-accessibility-reviewer | WCAG 整合性 | §2.2 画面 5 API フロー | **チェックアウト API フローのステップ番号誤り — WCAG SC 3.3.4 違反リスク**: API 呼び出しフローが `ステップ 3（注文確定 → Stripe リダイレクト）: POST /orders` と記述されているが、5 ステップ定義（同セクション上部テーブル）ではステップ 3 は「お支払い（決済方法の選択）」、ステップ 4 が「注文確認（WCAG SC 3.3.4 準拠）」である。WCAG SC 3.3.4 は金融取引の確定**前**に確認・修正の機会を要求するため、`POST /orders`（注文確定）+ Stripe リダイレクトはステップ 4（確認ボタン押下後）で実行されるべき。API フローが「ステップ 3」に誤ラベリングされていると、実装者がステップ 3 で注文を確定する実装を行い、ステップ 4 の確認画面が形骸化する危険がある | API フローのラベルを修正: `ステップ 3` → UI 操作のみ（API 呼び出しなし、支払い方法選択）。`ステップ 4（注文確定 → Stripe リダイレクト）:` に `POST /orders` + Stripe リダイレクトを移動。spec.md §チェックアウト・ステップインジケーター設計 との整合を担保する |
| H2-03 | **High** | architect, ux-accessibility-reviewer | WCAG 整合性 | §10 画面遷移図 | **Mermaid チェックアウト遷移図が確認ステップを迂回**: §10 の Mermaid 図では `CHECKOUT_PAY --> STRIPE[Stripe Hosted Payment Page]` と `CHECKOUT_PAY --> CHECKOUT_CONFIRM` が並列に描画されており、Payment から**直接** Stripe に遷移可能な経路が存在する。また `CHECKOUT_CONFIRM --> CHECKOUT_PAY` で確認画面から支払い画面に戻るループが描画されているが、Stripe への遷移経路が含まれていない。正しいフローは `CHECKOUT_PAY --> CHECKOUT_CONFIRM --> STRIPE --> CHECKOUT_COMPLETE` であり、Confirm を必ず経由する設計にすべき（WCAG SC 3.3.4 準拠）| Mermaid 図を以下に修正: `CHECKOUT_PAY --> CHECKOUT_CONFIRM[注文確認 /checkout/confirm]`、`CHECKOUT_CONFIRM --> STRIPE[Stripe Hosted Payment Page]`、`STRIPE --> CHECKOUT_COMPLETE[注文完了 /checkout/complete]`。Payment → Stripe の直接経路を削除する |

---

## Medium 指摘一覧（改善推奨）

### 新規 Medium（イテレーション 2 で検出）

| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|---------------|----------|----------|
| M2-01 | Medium | security-reviewer, tech-lead | 整合性 | §13 リスクと対策 #2 | **リスク記述が是正済み方針と矛盾**: §13 リスク #2 に「JWT のセキュアな保存 — `httpOnly Cookie` への移行を検討」と記載されているが、§4.4 / §5.3 で既に httpOnly Cookie は**決定事項**として実装されている。「移行を検討」は不正確であり、リスク対策の記述として適切でない | リスク #2 を「httpOnly Cookie 方式を採用済み。CSP ヘッダーの厳格化でさらなる XSS 防御を実施」等に更新 |
| M2-02 | Medium | programing-reviewer | 技術正確性 | §8.4 SEO | **`next/head` は Next.js 14 App Router では非推奨**: §8.4 に「`next/head` でメタタグ（title, description, OGP）を動的設定」と記載されているが、Next.js 14 App Router では `next/head` ではなく `export const metadata` / `generateMetadata()` API を使用する。Pages Router と App Router で API が異なり、§1.2 で App Router を採用と明記しているため技術的に不正確 | `next/head` を `Metadata API（generateMetadata）` に修正 |
| M2-03 | Medium | business-analyst, tech-lead | 整合性 | §7.0 / §2.1 | **i18n URL パス方式が画面一覧に未反映**: §7.0 で URL パス方式（`/ja/products`, `/en/products`）を定義しているが、§2.1 画面一覧マトリクスのパスは `/products` のみで locale プレフィックスが含まれていない。Next.js 14 の internationalized routing 設定（`next.config.js` の `i18n` または middleware）でパス方式を透過的に処理する旨の説明が不足 | §7.0 または §2.1 に「画面一覧のパスはロケール部分を省略。Next.js middleware で `/ja/`, `/en/` プレフィックスを自動処理する」旨の注記を追加 |
| M2-04 | Medium | architect | 整合性 | §2.1 #6 | **`/orders/{id}/confirm` と `/orders/{id}` の役割重複**: §2.1 #6「注文確認 `/orders/{id}/confirm`」と #8「注文詳細 `/orders/{id}`」の機能差が不明。§9.5 注文 API には `/orders/{id}/confirm` に対応するエンドポイントが存在せず、両画面とも `GET /orders/{id}` を呼び出す。#6 がメール内リンク用の確認画面であれば、その旨を明記すべき | #6 の用途を明確化（例: 注文完了後のメールリンク先画面）するか、#8 に統合して不要なエントリを削除 |
| M2-05 | Medium | ux-accessibility-reviewer | UX | §2.2 画面 5 API フロー | **Stripe キャンセル URL の UX 課題**: Stripe キャンセル URL が `/checkout/payment?canceled=true` に設定されているが、ユーザーは Stripe 画面で入力した支払い情報が破棄された状態で戻される。キャンセル時にステップ 3（お支払い）のどの状態に復帰するか（フォーム再入力が必要か、選択は保持されるか）の UX 仕様が未定義 | キャンセル復帰時のフォーム状態保持ルール（カート内容・配送先を維持、支払い方法選択をリセット）を定義し、ユーザーへのトースト通知（「決済がキャンセルされました。再度お支払い方法を選択してください」）を追加 |

### Carryover Medium（check-report-1 からの継続 — 未是正）

以下は check-report-1 の Medium 指摘のうち、fix-report-1 で「修正対象外」とされ本イテレーションでも未是正の項目である。詳細は `check-report-1.md` を参照。

| # | 出典 Agent | 対象 | 概要 | 状態 |
|---|-----------|------|------|------|
| M-01 | architect | §1.2 | shadcn/ui vs Headless UI 不一致 | 未修正 |
| M-02 | architect | §1.2 | Vitest vs Jest テストフレームワーク不一致（E-05 エスカレーション済） | 未修正 |
| M-03 | programing-reviewer | §4.1 | `console.error()` — 本番環境での構造化ロギング使用の注記は追加済だがコード例未修正 | 注記対応済 |
| M-04 | programing-reviewer | §6.3 | 楽観的更新コード例の `Cart` 型定義不足 | 未修正 |
| M-05 | dba-reviewer | §6.2 | カート staleTime=0 の過剰取得（Redis キャッシュ効果低減） | 未修正 |
| M-06 | security-reviewer | §2.2 #9 | アカウントロック後の回復フロー UI 未定義 | 未修正 |
| M-07 | security-reviewer | §2.2 #10 | 漏洩パスワードリスト照合（HIBP）未記載 | 未修正 |
| M-08 | audit-reviewer | §4.1 | ユーザー操作テレメトリ（Application Insights）未定義 | 未修正 |
| M-09 | audit-reviewer | 文書全体 | 改訂履歴セクション未追加 | 未修正 |
| M-10 | performance-reviewer | §8.1 | App Router RSC によるバンドル削減効果の考慮不足 | 未修正 |
| M-11 | business-analyst | §2.2 | ページネーション方式未確定（無限スクロール vs ページ番号） | 未修正 |
| M-14 | compliance-reviewer | 文書全体 | DSR（データ主体の権利行使）フォーム画面未記載 | 未修正 |
| M-15 | infra-ops-reviewer | §4.3 | SalesManagementService 障害時のフォールバック未記載 | 未修正 |
| M-16 | release-manager | §11 | SEO 基本実装の Must Have 昇格未検討 | 未修正 |
| M-17 | ux-accessibility-reviewer | §2.2 #1 | ヘッダーに検索バー（オートコンプリート付き）配置要件未記載 | 未修正 |

> **注**: check-report-1 の M-12（パスワードリセット画面）は §2.1 に #23/#24 として追加されたため **一部是正**。詳細画面仕様は未定義のため Low に降格。M-13（ステップインジケーター aria）は H-08 是正の一部として **完全是正済** ✅。

---

## Low 指摘一覧

| # | 出典 Agent | 対象 | 概要 |
|---|-----------|------|------|
| L-01 | programing-reviewer | §4.4 | `NEXT_PUBLIC_` プレフィックス — Server Component での API 呼び出しパターン補足不足（carryover） |
| L-02 | ux-accessibility-reviewer | §2.2 #3 | 在庫表示の色非依存テキストラベル要件が画面レベルで未記載（carryover） |
| L-03 | business-analyst | §2.1 | NPS アンケートページ未記載（Phase 2 スコープ、carryover） |
| L-04 | infra-ops-reviewer | §12 | API Gateway 統合 Swagger の設定方法未記載（carryover） |
| L-05 | business-analyst | §2.1 #23/#24 | パスワードリセット画面の詳細仕様（フォーム構成・API フロー・トークン有効期限）未定義（M-12 降格） |
| L-06 | performance-reviewer | §8.5 | Lighthouse CI のしきい値（Score ≥ 90 等）が未定義。CI 統合ルールにパフォーマンスしきい値を追加すべき（new） |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 | 前回からの変化 |
|---|--------|-----------|------|-----------|-------------|
| E-04 | 高優先 | security-reviewer | **JWT 保存・送信方式の図表統一**: httpOnly Cookie 方式は §4.4/§5.3 で確定済みだが §5.1/§5.2 の図が未更新。H2-01 修正後にエスカレーション解消見込み | セキュリティリード | 新規（H-11 是正の副作用） |
| E-05 | 通常 | qa-manager | **テストフレームワーク確定**: Vitest/Playwright vs Jest/Cypress（§8.5 でエスカレーション済と注記）| フロントエンドリード | 継続 |
| E-06 | 通常 | release-manager | **SEO 基本実装の Phase 1 昇格判断** | PO | 継続 |

> check-report-1 の E-01（Next.js バージョン）、E-02（管理画面技術スタック）、E-03（ゲスト購入 UI）は H-01/H-03/H-05/H-06 の是正により**解消済**。

---

## 競合解決記録

本イテレーションでは Agent 間の競合は検出されなかった。

---

## ドキュメント横断分析

### サービス間 API 整合性（check-report-1 からの改善）

| front-end-need.md のパス | spec.md / 設計書のパス | 整合性 | 前回 |
|-------------------------|---------------------|--------|------|
| `/wishlists/{id}/items` | `/wishlists/{id}/items` | ✅ | ❌ → ✅ 修正 |
| `/checkout/cart` → `/checkout/complete`（5 ステップ） | `/checkout/cart` → `/checkout/complete` | ✅ | ❌ → ✅ 修正 |
| Stripe Checkout Session → リダイレクト | Stripe Hosted Payment Page (ADR-0008) | ✅ | ❌ → ✅ 修正 |
| `POST /orders` → ポーリング（Saga 対応） | Saga オーケストレーション (ADR-0009) | ✅ | ❌ → ✅ 修正 |
| `/checkout/guest` | `/checkout/guest` | ✅ | ❌ → ✅ 追加 |
| `/legal/tokushoho` | `/legal/tokushoho` | ✅ | ❌ → ✅ 追加 |
| `/coupons/validate` | `/coupons/validate` | ✅ | ✅ 継続 |
| `/points/balance` | `/points/balance` | ✅ | ✅ 継続 |
| `/auth/login` | `/auth/login` | ✅ | ✅ 継続 |
| `/cart/items` | `/cart/items` | ✅ | ✅ 継続 |
| `/products`, `/products/{id}` | `/products`, `/products/{id}` | ✅ | ✅ 継続 |

### 記載カバレッジ分析

| 要件領域 | check-report-1 | check-report-2 | 改善 |
|---------|---------------|----------------|------|
| チェックアウト 5 ステップ | ❌ 未分割 | ✅ 5 ステップ定義済 | ✅ |
| ゲスト購入フロー | ❌ 未記載 | ✅ 専用セクション追加 | ✅ |
| 越境データ移転同意 UI | ❌ 未記載 | ✅ ゲスト購入内に実装 | ✅ |
| Stripe Hosted Payment Page | ❌ 未反映 | ✅ Checkout Session フロー定義 | ✅ |
| WCAG 2.1 AA 詳細 | ❌ 簡略化 | ✅ §8.2 大幅拡充 | ✅ |
| i18n ライブラリ・翻訳規則 | ❌ 未記載 | ✅ §7.0 新設 | ✅ |
| テスト戦略 | ❌ 未定義 | ✅ §8.5 新設 | ✅ |
| 法的ページ | ❌ 未記載 | ✅ 3 ページ追加 | ✅ |
| httpOnly Cookie 認証 | ❌ 矛盾 | ⚠️ §4.4 修正済 / §5.1-5.2 図未更新 | △ |
| Saga 非同期処理 UI | ❌ 未定義 | ✅ ポーリング・タイムアウト UX 追加 | ✅ |
| DSR フォーム画面 | ❌ 未記載 | ❌ 未記載（M-14 継続） | — |
| 検索バー（オートコンプリート）| ❌ 未記載 | ❌ 未記載（M-17 継続） | — |

### 未定義・曖昧な領域（残存）

| # | 未定義事項 | 影響度 | ブロッカー可能性 | 前回からの変化 |
|---|----------|--------|---------------|-------------|
| 1 | §5.1/§5.2 認証フロー図の httpOnly Cookie 未反映 | 高 | ⚠️ 実装混乱リスク | 新規（H2-01） |
| 2 | チェックアウト API フロー/Mermaid 図のステップ番号誤り | 高 | ⚠️ WCAG SC 3.3.4 実装漏れリスク | 新規（H2-02/H2-03） |
| 3 | DSR フォーム画面 | 中 | 法規制コンプライアンスリスク | 継続 |
| 4 | 検索サジェスト UI 配置 | 中 | UX 設計遅延リスク | 継続 |
| 5 | ページネーション方式確定 | 低 | 設計判断で解決可能 | 継続 |

---

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### 評価: Pass — Medium 2 件, Low 1 件

**レビュー観点**: ビジネス要件の完全性、ユーザーストーリー整合性、EC サイト機能要件カバレッジ

**肯定的評価（前回からの改善）**:
- ゲスト購入フローが新設され、会員登録なしの購入パスが明確に定義された（H-05 是正）
- チェックアウトが 5 ステップに分割され、spec.md のステップインジケーター設計と整合（H-08 是正）
- i18n ライブラリ・翻訳キー規則が §7.0 で定義され、next-intl + `{domain}.{context}.{key}` 形式が明記（H-18 是正）
- 法的ページ（特定商取引法、プライバシーポリシー、利用規約）が画面一覧に追加（H-07 是正）
- パスワードリセット画面が §2.1 #23/#24 として追加（M-12 一部是正）

**指摘**:
- M2-03: i18n URL パス方式が §2.1 に未反映
- M-11: ページネーション方式未確定（carryover）
- L-03: NPS アンケートページ未記載（carryover）
</details>

<details>
<summary>architect レビューレポート</summary>

### 評価: ⚠️ Conditional — High 2 件, Medium 2 件

**レビュー観点**: アーキテクチャ整合性、サービス間通信、画面遷移フロー

**肯定的評価（前回からの改善）**:
- Wishlist API が `/wishlists` に統一され spec.md と整合（H-10 是正）
- Stripe Hosted Payment Page フローが定義され ADR-0008 と整合（H-09 是正）
- Saga パターンの非同期処理 UX（ポーリング・タイムアウト）が追加（H-17 是正）
- 管理画面が Razor Pages として注記され spec.md と整合（H-03 是正）

**指摘**:
- H2-02: API フローのステップ番号誤り（POST /orders がステップ 3 に割当 — ステップ 4 であるべき）
- H2-03: Mermaid 図で Payment → Stripe 直接経路が存在し Confirm を迂回可能
- M2-04: `/orders/{id}/confirm` と `/orders/{id}` の役割重複
- M-01: shadcn/ui vs Headless UI 不一致（carryover）
</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 評価: Pass — Medium 2 件, Low 1 件

**レビュー観点**: コード例の正確性、TypeScript / Next.js 14 パターンの適切性

**肯定的評価（前回からの改善）**:
- API クライアント設定が `withCredentials: true` に修正され httpOnly Cookie と整合（H-11 是正）
- TanStack Query + axios の併用方針が明確化（H-14 是正）
- Correlation ID がバックエンド生成に移行（H-16 是正）
- テスト戦略セクション（§8.5）が追加され CI 統合ルールまで定義（H-13 是正）

**指摘**:
- M2-02: §8.4 の `next/head` は App Router 環境で非推奨 → `Metadata API` に修正すべき
- M-03: §4.1 に `console.error()` 残存（注記対応済、carryover）
- L-01: Server Component での API 呼び出しパターン補足不足（carryover）
</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 評価: Pass — Medium 1 件

**レビュー観点**: データアクセスパターン、フロントエンド ↔ DB 設計整合性

**肯定的評価**:
- ゲスト購入フローの API 設計が spec.md のエンティティ設計（`is_guest`, `guest_email`）と整合
- ページネーション仕様がバックエンド設計（page=0 始まり、size パラメータ）と整合

**指摘**:
- M-05: カート staleTime=0 の過剰取得（carryover）
</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 評価: ⚠️ Conditional — High 1 件, Medium 2 件

**レビュー観点**: OWASP Top 10、認証/認可設計、JWT セキュリティ

**肯定的評価（前回からの改善）**:
- httpOnly Cookie 方式が §4.4 で正しく実装（`withCredentials: true`、Authorization ヘッダー削除）（H-11 是正）
- Correlation ID 生成がバックエンドに移行し、ログ汚染リスク排除（H-16 是正）
- ゲスト購入の越境データ移転同意がオプトイン方式で実装（H-06 是正）
- CSRF 対策（SameSite Cookie + CSRF トークン）が §2.2 #9 に記載

**指摘**:
- H2-01: §5.1/§5.2 認証図が httpOnly Cookie と矛盾（`Authorization: Bearer` 残存）
- M2-01: §13 リスク #2 が「移行を検討」と記述（決定済み事項と矛盾）
- M-06: アカウントロック後の回復フロー UI 未定義（carryover）
</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 評価: Pass — Medium 2 件

**レビュー観点**: GDPR、個人情報保護法、PCI DSS、特定商取引法

**肯定的評価（前回からの改善）**:
- 特定商取引法表示ページが §2.1 + 法的ページセクションに追加（H-07 是正）
- プライバシーポリシー・利用規約ページが画面一覧に追加
- ゲスト購入時の越境データ移転同意チェックボックスがオプトイン方式で実装（H-06 是正）
- 個人情報利用目的の通知テキスト、プライバシーポリシーリンクが定義

**指摘**:
- M-14: DSR フォーム画面（`/dsr/guest`）未記載（carryover）
- M-07: パスワード漏洩リスト照合（HIBP）未記載（carryover）
</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 評価: Pass — Medium 2 件

**レビュー観点**: トレーサビリティ、ドキュメント管理

**肯定的評価**:
- Correlation ID がバックエンド生成に統一され、レスポンスヘッダーの `X-Correlation-Id` をログ表示用に使用する設計が明確
- RFC 9457 エラーレスポンスの `traceId` フィールド活用が定義済

**指摘**:
- M-08: ユーザー操作テレメトリ（Application Insights SDK）が未定義（carryover）
- M-09: ドキュメント改訂履歴セクションが未追加（carryover）
</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 評価: Pass — Medium 1 件

**レビュー観点**: テスト戦略、カバレッジ目標、受入基準の検証可能性

**肯定的評価（前回からの改善）**:
- §8.5 テスト戦略セクションが新設（Vitest / Testing Library / Playwright / axe-core / Lighthouse CI）
- カバレッジ目標（分岐 80%）、テスト種別、CI 統合ルールが明確に定義（H-13 是正）
- アクセシビリティ自動テスト（axe-core）が CI に組み込まれる要件として記載

**指摘**:
- M-02: Vitest vs Jest のテストフレームワーク不一致は未解決（E-05 エスカレーション済、carryover）
</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 評価: Pass — Medium 2 件, Low 1 件

**レビュー観点**: Core Web Vitals、パフォーマンス SLA、キャッシュ戦略

**肯定的評価（前回からの改善）**:
- Core Web Vitals が INP ≤ 200ms に更新され spec.md と整合（H-04 是正）
- TTFB が独自目標として位置づけ記載（H-12 是正）
- Saga 処理中のポーリング間隔（3 秒）とタイムアウト（30 秒）が定義（H-17 是正）

**指摘**:
- M-10: App Router RSC によるバンドルサイズ削減効果の考慮不足（carryover）
- M-05: カート staleTime=0 の過剰取得（carryover、dba-reviewer と重複）
- L-06: Lighthouse CI のスコアしきい値未定義（new）
</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 評価: Pass — Medium 1 件, Low 1 件

**レビュー観点**: コンテナ設計、可観測性、グレースフルデグラデーション

**肯定的評価**:
- §4.3 グレースフルデグラデーション戦略が主要サービスをカバー
- §4.4 API クライアント設定がプロダクション対応（タイムアウト、リトライ）

**指摘**:
- M-15: SalesManagementService 障害時のフォールバック未記載（carryover）
- L-04: API Gateway 統合 Swagger の設定方法不明（carryover）
</details>

<details>
<summary>release-manager レビューレポート</summary>

### 評価: Pass — Medium 1 件

**レビュー観点**: リリース戦略、MoSCoW 分類

**肯定的評価**:
- §11 MoSCoW 分類が明確（Must/Should/Could/Won't）
- テスト CI 統合ルール（§8.5）がリリース品質ゲートとして機能

**指摘**:
- M-16: SEO 基本実装の Must Have 昇格未検討（carryover）
</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 評価: Pass — Medium 1 件

**レビュー観点**: 依存ライブラリの適切性、NuGet パッケージ整合性

**肯定的評価（前回からの改善）**:
- TanStack Query v5 + axios の併用方針が明確化（H-14 是正）
- next-intl がライブラリとして明記（H-18 是正）

**指摘**:
- M-01: shadcn/ui（Radix Primitives ベース）vs Headless UI（Tailwind Labs ベース）の不一致。設計思想が異なるためコンポーネント移行コストが発生する可能性（carryover）
</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 評価: Pass — Medium 2 件, Low 1 件

**レビュー観点**: WCAG 2.1 AA 準拠、UX 設計品質

**肯定的評価（前回からの改善）**:
- §8.2 が大幅拡充され、WCAG SC 4.1.3（ステータスメッセージ）、SC 2.4.1（スキップリンク）、SC 2.3.1（アニメーション制御）、SC 1.4.13（ホバー/フォーカスコンテンツ）、色非依存情報伝達、フォーカストラップが追加（H-15 是正）
- チェックアウト 5 ステップのステップインジケーターに `aria-current="step"`、`aria-label` が定義（H-08 是正）
- 注文確認画面（ステップ 4）が WCAG SC 3.3.4 準拠で設計され、「変更」リンク・金額表示付き確定ボタンが定義
- 注文完了画面に `role="alert" aria-live="assertive"` が適用

**指摘**:
- M2-05: Stripe キャンセル復帰時の UX 仕様未定義（new）
- M-17: ヘッダーに検索バー（オートコンプリート付き）配置要件未記載（carryover）
- L-02: 在庫表示の色非依存テキストラベルが画面レベルで未記載（carryover）
</details>

<details>
<summary>tech-lead レビューレポート</summary>

### 評価: ⚠️ Conditional — Medium 2 件, Low 1 件

**レビュー観点**: 技術標準の横断適合性、実装実現可能性、規約遵守

**肯定的評価（前回からの大幅改善）**:
- 前回 High 18 件が全て適切に是正されている。特に以下の改善が顕著:
  - httpOnly Cookie 方式への統一（H-11）: コード例が正確に修正され、AGENTS.md §10.2 との整合性が確保された
  - Stripe Hosted Payment Page 統合（H-09）: ADR-0008 の非保持化方針が正しく反映された
  - Saga パターン対応（H-17）: ADR-0009 の非同期処理がフロントエンド UX として具体化された
  - WCAG 2.1 AA 拡充（H-15）: spec.md のアクセシビリティ要件が網羅的に反映された
  - i18n 設計（H-18）: next-intl + 翻訳キー規則が spec.md と整合

- ドキュメント全体の品質が前回から大幅に向上。18 件の High 是正による完成度は高い

**残存リスク**:
- §5.1/§5.2 認証フロー図の未更新は「修正の取りこぼし」であり、3 件の新規 High は全て修正の副作用（本質的な設計不備ではない）
- 全て局所的な修正で解決可能であり、30 分程度の作業量と見積もる

**指摘**:
- M2-01: §13 リスク記述の「移行を検討」が決定済み事項と矛盾（new）
- M2-03: i18n パス方式が画面一覧に未反映（new）
- L-05: パスワードリセット画面の詳細仕様不足

**技術スタック横断整合性**: AGENTS.md / copilot-instructions.md / spec.md / ADR-0008 / ADR-0009 との主要な技術整合性は確保されている。新規 High 3 件はいずれも「修正時に更新が漏れた既存セクション」であり、ドキュメントの設計方針自体には問題がない。
</details>

---

## 総合評価

### 改善度

前回（check-report-1）からの改善は顕著であり、High 18 件の是正品質は高い。ドキュメント全体の完成度は大幅に向上した。

| 指標 | check-report-1 | check-report-2 | 改善率 |
|------|---------------|----------------|-------|
| High 件数 | 18 | 3 | -83% |
| API 整合性（✅ 件数） | 5/9 | 11/11 | +67% |
| WCAG 2.1 AA カバレッジ | 簡略化 | 網羅的 | 大幅改善 |
| spec.md との技術スタック整合 | 6/12 不一致 | 1/12 不一致 | 大幅改善 |

### 新規 High 3 件の性質

新規 High 3 件は全て「既存セクションの更新漏れ」であり、ドキュメントの設計方針の問題ではない:

1. **H2-01**: §4.4/§5.3 の httpOnly Cookie 修正時に §5.1/§5.2 の ASCII 図が更新されなかった
2. **H2-02**: 5 ステップ分割（H-08）時に API フローのステップ番号ラベルが未修正
3. **H2-03**: Stripe 統合（H-09）時に §10 Mermaid 図の遷移経路が未修正

いずれも局所的な修正で解決可能であり、次回イテレーションでの解決が見込まれる。
