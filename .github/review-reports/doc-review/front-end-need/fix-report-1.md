# Fix Report: front-end-need.md (Phase 4)

## 概要
- **対象**: `design-docs/front-end-need.md`
- **修正日時**: 2026-04-03
- **基準レポート**: `check-report-1.md`
- **修正対象**: Critical 0件 / High 18件

## 修正結果サマリー

| 重要度 | 検出数 | 修正済 | 残存 | エスカレーション |
|--------|--------|--------|------|----------------|
| Critical | 0 | 0 | 0 | 0 |
| High | 18 | 18 | 0 | 0 |
| Medium | 21 | 0 | 21 | — |
| Low | 4 | 0 | 4 | — |

## High 指摘修正一覧

| # | 指摘 | 修正内容 | 対象行（修正後） |
|---|------|---------|---------------|
| H-01 | Next.js バージョン不一致（15→14） | `Next.js 15 (App Router)` → `Next.js 14 (App Router)`、spec.md §技術スタック準拠の補足追加 | L13 |
| H-02 | 状態管理ライブラリ矛盾（Zustand/Jotai → Redux Toolkit/Zustand） | `Zustand / Jotai` → `Redux Toolkit / Zustand`、spec.md §技術スタック準拠を明記 | L15 |
| H-03 | 管理画面技術スタック矛盾（Next.js SPA → Razor Pages） | §3 冒頭に注記追加: 管理画面は spec.md に基づき ASP.NET Core + Razor Pages で実装。front-end-need.md は機能要件のみ定義 | §3 冒頭 |
| H-04 | Core Web Vitals FID → INP | `FID < 100ms` → `INP ≤ 200ms`（spec.md 準拠）。TTFB は本ドキュメント独自目標と明記 | §8.1 |
| H-05 | ゲスト購入フロー UI 未記載 | ゲスト購入フローセクション新設（`/checkout/guest`）。メールアドレス2回入力検証、越境データ移転同意、プライバシーポリシー同意を含む | 画面10の後 |
| H-06 | 越境データ移転同意 UI 未記載 | ゲスト購入フロー内に SendGrid 越境移転同意チェックボックス（オプトイン方式）、プライバシーポリシーリンク、利用目的通知テキストを追加 | ゲスト購入フロー |
| H-07 | 特定商取引法表示ページ未記載 | §2.1 に `/legal/tokushoho`, `/legal/privacy`, `/legal/terms` を追加。法的ページセクション新設、フッターリンク要件を記載 | §2.1 + 法的ページ |
| H-08 | チェックアウトフロー 1画面→5ステップ | 単一 `/checkout` → 5ステップ（`/checkout/cart`, `/checkout/shipping`, `/checkout/payment`, `/checkout/confirm`, `/checkout/complete`）に分割。ステップインジケーターのアクセシビリティ要件を追加 | 画面5全体 |
| H-09 | Stripe Hosted Payment Page 未反映 | `POST /payments` 直接呼出し → Stripe Checkout Session 作成→リダイレクト→コールバック URL フローに変更。ADR-0008 PCI DSS 準拠の注記追加 | 画面5 ステップ3 |
| H-10 | Wishlist API パス不一致（`/users/me/favorites` → `/wishlists`） | API パスを `/wishlists` に統一。§9.2a セクション新設で spec.md §ウィッシュリスト機能準拠の全エンドポイントを記載。`notifyOnRestock` トグル対応を追加 | §2.1 + §9.2a |
| H-11 | JWT httpOnly Cookie vs `getAccessToken()` 矛盾 | `getAccessToken()` + `Authorization: Bearer` を削除。`withCredentials: true` 方式に変更。リフレッシュトークンも httpOnly Cookie 自動送信に統一 | §4.4 |
| H-12 | TTFB 目標値の位置づけ不明 | TTFB 行に「spec.md 未定義、本ドキュメント独自目標」の注記を追加 | §8.1 |
| H-13 | テスト戦略セクション未定義 | §8.5「フロントエンドテスト戦略」セクション新設。Vitest/Testing Library/Playwright/axe-core/Lighthouse CI、カバレッジ目標、CI 統合ルールを定義 | §8.5 |
| H-14 | API クライアントライブラリ不一致 | §1.2 の API 通信行を `TanStack Query v5 + axios` に明確化。axios をHTTP クライアントとして TanStack Query の `queryFn` 内で使用する方針を記載 | L17 |
| H-15 | WCAG 2.1 AA 要件の簡略化 | §8.2 を大幅拡充。SC 4.1.3 ステータスメッセージ、SC 2.4.1 スキップリンク、SC 2.3.1 アニメーション制御、SC 1.4.13 ホバー/フォーカスコンテンツ、色非依存情報伝達、フォーカストラップを追加。spec.md クロスリファレンスを設定 | §8.2 |
| H-16 | フロントエンド Correlation ID 生成のセキュリティリスク | `crypto.randomUUID()` による `X-Correlation-Id` 送信を削除。「Correlation ID はバックエンド（API Gateway）が生成。レスポンスの X-Correlation-Id をログ表示用に使用」と明記 | §4.4 |
| H-17 | Saga パターン非同期処理 UI 未定義 | 注文完了画面に Saga 処理中 UX を追加: PENDING 時ローディング表示、3秒ポーリング、30秒タイムアウト時メッセージ、FAILED/CANCELLED 時エラー表示 | 画面5 注記 |
| H-18 | i18n ライブラリ未指定 | §7.0 セクション新設。next-intl ライブラリ、翻訳キー命名規則（`{domain}.{context}.{key}`）、フォールバック戦略（`ja` → `en`）、エラーメッセージ i18n マッピングを追加 | §7.0 |

## 追加修正（関連改善）

| 修正 | 内容 |
|------|------|
| 画面遷移図更新 | Mermaid 図に5ステップチェックアウト、ゲスト購入、Stripe リダイレクト、法的ページを反映 |
| §4.1 エラーハンドラー | `console.error` に本番環境での構造化ロギング使用の注記を追加 |
| §2.1 画面一覧 | パスワードリセット（#23, #24）を追加。チェックアウト行を5ステップに分割 |

## エスカレーション事項（check-report-1 から継続）

以下は人間の判断が必要な事項であり、本修正フェーズではドキュメント側で注記を追加するにとどめた:

| # | 内容 | 本修正での対応 |
|---|------|-------------|
| E-01 | Next.js バージョン確定（14 vs 15） | spec.md に合わせて 14 に統一。15 採用の場合は spec.md 側の更新が必要 |
| E-02 | 管理画面技術スタック確定 | spec.md に合わせて Razor Pages を注記。front-end-need.md は機能要件のみ定義 |
| E-05 | テストフレームワーク確定（Vitest vs Jest） | §8.5 で Vitest/Playwright を推奨として記載し、注記でエスカレーション済みと明記 |

## 未修正（Medium/Low — 改善推奨）

Medium 21件、Low 4件は修正対象外。次回レビューサイクルで対応を検討。
主要な Medium 指摘: M-01 (shadcn/ui vs Headless UI), M-02 (Vitest vs Jest), M-05 (カート staleTime), M-12 (パスワードリセット画面詳細), M-14 (DSR フォーム画面)。
