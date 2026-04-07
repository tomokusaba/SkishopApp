# ドキュメントレビュー統合レポート

## 判定結果
- **対象**: `design-docs/front-end-need.md`
- **判定**: ⚠️ **Conditional Approval** — High 指摘あり、人間の判断を介在
- **レビュー日時**: 2026-04-03 (イテレーション 3)
- **プロジェクト**: SkiShop (.NET 10 / C# 14 / ASP.NET Core 10 Minimal API / EF Core 10 / .NET Aspire 13.1)

## 技術スタック検証結果
| カテゴリ | 設計書記載 | AGENTS.md / spec.md 定義 | 整合性 |
|---------|-----------|-------------------------|--------|
| フロントエンド FW | Next.js 14 (App Router) | Next.js 14 (React) | ✅ |
| 言語 | TypeScript 5.x | TypeScript 5 | ✅ |
| 状態管理 | Redux Toolkit / Zustand | Redux/Zustand | ✅ |
| API 通信 | TanStack Query v5 + axios | Axios, React Query | ✅ |
| UI | Tailwind CSS + shadcn/ui | TailwindCSS, Headless UI | ⚠️ shadcn/ui vs Headless UI の差異 |
| テスト | Vitest + Testing Library + Playwright | Jest, React Testing Library, Cypress | ❌ 不一致（E-05 エスカレーション済み） |
| i18n | next-intl | next-intl | ✅ |
| 管理画面 | TypeScript ロール定義 + Razor Pages 注記 | ASP.NET Core + Razor Pages + Bootstrap 5 + htmx + Alpine.js | ⚠️ 混在あり |
| バックエンド FW | ASP.NET Core 10 (Minimal API) | ASP.NET Core 10 (Minimal API) | ✅ |
| ORM | — (API 経由) | EF Core 10 | ✅ N/A |
| API Gateway | YARP (port 8080) | YARP リバースプロキシ | ✅ |
| 認証 | JWT (httpOnly Cookie) + OAuth2/OIDC | ASP.NET Core Identity + JWT | ✅ |
| 決済 | Stripe Hosted Payment Page | Stripe (ADR-0008 PCI DSS 非保持化) | ✅ |

## 指摘サマリー
| Agent | 判定 | Critical | High | Medium | Low |
|-------|------|----------|------|--------|-----|
| business-analyst | ⚠️ | 0 | 0 | 2 | 1 |
| architect | ⚠️ | 0 | 2 | 1 | 0 |
| programing-reviewer | ✅ | 0 | 0 | 2 | 1 |
| dba-reviewer | ✅ | 0 | 0 | 0 | 1 |
| security-reviewer | ⚠️ | 0 | 1 | 1 | 0 |
| compliance-reviewer | ✅ | 0 | 0 | 1 | 1 |
| audit-reviewer | ✅ | 0 | 0 | 0 | 1 |
| qa-manager | ⚠️ | 0 | 1 | 0 | 0 |
| performance-reviewer | ✅ | 0 | 0 | 1 | 1 |
| infra-ops-reviewer | ✅ | 0 | 0 | 1 | 0 |
| release-manager | ✅ | 0 | 0 | 0 | 1 |
| oss-reviewer | ✅ | 0 | 0 | 1 | 0 |
| ux-accessibility-reviewer | ⚠️ | 0 | 0 | 2 | 2 |
| tech-lead | ⚠️ | 0 | 1 | 1 | 0 |
| **合計** | | **0** | **5** | **13** | **9** |

## 判定根拠
- 判定ルール適用結果: Critical 0 件だが High 5 件が存在するため **⚠️ Conditional Approval**
- 最も重大な指摘: 管理画面技術スタックの矛盾（H-01）、API パスプレフィックスの不統一（H-02）、テストフレームワーク未確定（H-04）

## Critical/High 指摘一覧（修正必須）
| # | 重要度 | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|--------|-----------|---------|--------------|----------|----------|
| H-01 | High | architect, tech-lead | 技術スタック整合性 | §3 管理画面, §16.5 ロール別権限 | spec.md §技術スタック では管理画面は **Razor Pages + Bootstrap 5 + htmx + Alpine.js** と明記されているが、§16.5 では TypeScript ベースのルートガード定義（`RoutePermission[]`, `adminRoutePermissions` 等）を含んでおり、SPA（Next.js）で管理画面を実装する前提のコードが混在している。§3 冒頭に「Razor Pages で実装する」注記がある一方、§16.5 は TypeScript コードで管理画面の権限制御を定義。**どちらの技術で管理画面を実装するのか不明確** | §16.5 の TypeScript ルートガードを Razor Pages の `@attribute [Authorize(Policy = "AdminOnly")]` / `@attribute [Authorize(Roles = "Admin,Manager")]` に書き換えるか、「Razor Pages 管理画面に対応する認可ポリシーのマッピング表」に変換する。TypeScript コード例は EC 画面（Next.js）のみに限定する |
| H-02 | High | architect, security-reviewer | API 設計 | §9.11 DSR API, §15.10 | DSR 関連 API（`POST /api/v1/users/{userId}/data-export`, `POST /api/v1/users/{userId}/dsr/deletion`）のみ `/api/v1/` プレフィックスを使用しており、他の全 API（`/auth/*`, `/products/*`, `/orders/*` 等）と**パスプレフィックスが不統一**。spec.md 側の同意管理 API は `/users/{userId}/consents` で `/api/v1/` プレフィックスなし。同一ドキュメント内でも §15.2 のデータエクスポート画面は `/api/v1/` 付き、§15.10 の同意管理 API は `/consents/anonymous`（プレフィックスなし）と混在 | 全 API を `/api/v1/` 付きに統一するか、全て `/` 始まり（プレフィックスなし）に統一する。API Gateway のルーティングルールとも整合させること |
| H-03 | High | security-reviewer | セキュリティ | §16.9 CSP ヘッダー | CSP で `style-src 'unsafe-inline'` を許可している。これにより CSS インジェクション攻撃のリスクが残る。Tailwind CSS JIT モードを理由としているが、Tailwind はビルド時に CSS を生成するためインラインスタイルの必要性は限定的。将来的な nonce 方式移行の「検討」のみで、具体的な移行計画やタイムラインが示されていない | Phase 1 リリースまでに nonce ベースの CSP に移行するか、`style-src 'unsafe-inline'` を許容する場合はリスク受容判断として明記する。CSP reporting endpoint（`report-uri` / `report-to`）の設定も追加すること |
| H-04 | High | qa-manager | テスト戦略 | §8.5 テスト戦略 | spec.md では `Jest + Cypress` を記載、front-end-need.md では `Vitest + Playwright` を推奨。E-05 としてエスカレーション済みだが**未解決のまま**。テストフレームワーク未確定では CI パイプラインの構築が不可能 | テストフレームワークの最終選定を早期に確定し、spec.md と front-end-need.md を同期する。Next.js 14 App Router との親和性を考慮すると Vitest + Playwright が合理的だが、spec.md の SSOT 原則に基づき spec.md 側も更新する |
| H-05 | High | architect | 同意管理 API | §15.1, §15.10 vs spec.md | spec.md の同意管理 API は `PUT /users/{userId}/consents/{consentType}`（個別同意付与）だが、front-end-need.md §15.10 では `PUT /users/{userId}/consents/{type}`（`consentType` vs `type` のパスパラメータ名差異）。また front-end-need.md は `PUT /users/{userId}/consents/bulk`（一括更新）を定義しているが、spec.md にもこのエンドポイントは存在する。一方、未認証ユーザー向け API（`/consents/anonymous`）は front-end-need.md で詳細に定義されているが、パラメータ名の不一致が混乱を招く可能性がある | パスパラメータ名を `{consentType}` に統一し、spec.md と完全一致させる |

## エスカレーション事項（要人間判断）
| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-01 | 高優先 | architect | UI コンポーネントライブラリの最終選定: spec.md は `Headless UI`、front-end-need.md は `shadcn/ui (Radix Primitives ベース)`。アクセシビリティ対応の基盤に影響するため早期決定が必要 | フロントエンドリード |
| E-02 | 高優先 | qa-manager | テストフレームワーク最終選定（Jest/Cypress vs Vitest/Playwright）— §14 E-05 に対応 | テックリード |
| E-03 | 通常 | infra-ops-reviewer | リアルタイム通知方式（SSE vs WebSocket）の最終確定 — §14 E-03, E-05 に対応 | バックエンドリード + フロントエンドリード |
| E-04 | 通常 | business-analyst | 再注文 API の一括追加仕様: §15.3 では「商品ごとにリクエスト」だが、一括追加 API（`POST /cart/items/bulk`）を新設すべきか | アーキテクトチーム |
| E-05 | 通常 | ux-accessibility-reviewer | ダークモード対応の Phase 1 スコープ inclusion/exclusion の明確化 | PO + デザインリード |

## 競合解決記録
| # | Agent A | Agent B | 競合内容 | Tech-Lead 裁定結果 | 裁定根拠 |
|---|---------|---------|---------|-------------------|----------|
| 1 | architect (Razor Pages 推奨) | programing-reviewer (TypeScript RBAC コード品質) | 管理画面 §16.5 の TypeScript ルートガードコードが Razor Pages 前提と矛盾 | architect を支持：spec.md の SSOT 原則に基づき、管理画面は Razor Pages で実装。§16.5 の TypeScript コードは EC 画面のナビゲーション表示制御用に限定するか、Razor Pages 用の認可ポリシー対応表に置き換える | 管理画面技術スタックは spec.md で確定済み。front-end-need.md で独自の技術選定をすることは SSOT 違反 |

## Medium 指摘一覧
| # | 出典 Agent | カテゴリ | 対象セクション | 指摘内容 | 推奨対応 |
|---|-----------|---------|--------------|----------|----------|
| M-01 | business-analyst | 機能要件 | §2.2 各画面詳細 | **SSR/CSR レンダリング戦略がページ単位で未定義**。§8.4 で「商品一覧・詳細は SSR/SSG」と記載があるが、各画面ごとの レンダリング方式（SSR/SSG/ISR/CSR）が体系的に整理されていない | 画面一覧マトリクス（§2.1）にレンダリング方式列を追加し、ページ単位で SSR/SSG/ISR/CSR を明示する |
| M-02 | business-analyst | 機能要件 | §2.2 画面 19: AI チャット | AI チャット（フローティング）の画面遷移・表示制御が不十分。「フローティング」とのみ記載され、表示条件（全ページ? 特定ページのみ?）、最小化/最大化動作、レスポンシブ時の挙動が未定義 | AI チャットウィジェットの表示条件、配置（画面右下等）、モバイル時の全画面モード切替、非表示条件（チェックアウトフロー中は非表示等）を明確に定義する |
| M-03 | architect | アーキテクチャ | §6 状態管理 | 状態管理ライブラリが「Redux Toolkit / Zustand」と選択肢の併記のまま。どちらを採用するか未確定では実装パターンが定まらない。§6.1 では Zustand を前提とした `useAuthStore` が使用されているが、§1.2 では Redux Toolkit も列挙 | Zustand を正式採用し、spec.md と同期する。Redux Toolkit の記載を削除または「将来の大規模化時の代替案」として注記に移す |
| M-04 | programing-reviewer | コード品質 | §16.2, §16.3 | TypeScript コード例で `console.error` が使用されている（§16.2 ログアウト周辺、§4.1 エラーハンドリング）。本番環境では構造化ログ（Application Insights SDK 等）を使用すべき | `console.error` を `logger.error()` に置き換えるか、「本番環境では Application Insights SDK 等を使用」する旨の注記をコード例に追加する（§4.1 の 500 系エラーハンドリングには部分的に注記があるが不完全） |
| M-05 | programing-reviewer | コード品質 | §16.1 API エラーハンドリング | `ValidationError.toFormErrors()` の PascalCase → camelCase 変換が `charAt(0).toLowerCase() + slice(1)` の簡易実装。`FirstName` → `firstName` は動作するが、`HTMLParser` → `hTMLParser` のようなケースで問題が発生する可能性 | lodash の `camelCase` または専用のケース変換ユーティリティの使用を推奨する旨を注記する |
| M-06 | security-reviewer | セキュリティ | §16.9 XSS 対策 | `dangerouslySetInnerHTML` 使用禁止の ESLint ルール（`react/no-danger`）が記載されているが、`.eslintrc` への具体的なルール設定が示されていない。また、DOMPurify の許可タグ定義（`ALLOWED_TAGS`）に `a` タグが含まれるが、`target` と `rel` 属性の値制限（`target="_blank"` 時の `rel="noopener noreferrer"` 強制）が未定義 | DOMPurify 設定に `ADD_ATTR: ['target']` + カスタムフック で `a[target="_blank"]` に `rel="noopener noreferrer"` を強制付与する処理を追加する |
| M-07 | compliance-reviewer | 法規制 | §15.1 Cookie 同意バナー | Cookie 同意バナーの「必要最低限のみ」ボタンと `Escape` キーの動作が同等と記載されているが、「`Escape` で閉じる」が同意撤回の意図を持つか、単に UI を閉じるだけかが不明確。ePrivacy 指令上、明示的な拒否の意思表示として扱うべきかの法務判断が必要 | `Escape` キーの動作を「バナーを閉じるが同意状態は変更しない（未回答のまま）」に変更するか、法務確認の上で「拒否と同等」の判断を明記する |
| M-08 | performance-reviewer | パフォーマンス | §16.8 画像最適化 | 商品画像サイズ上限（1200×1200px）が定義されているが、Azure Blob Storage からの配信時の CDN キャッシュ戦略（Cache-Control ヘッダー、CDN パージ方針）が未定義 | 商品画像の CDN キャッシュ戦略（`Cache-Control: public, max-age=86400, immutable` 等）を定義し、画像更新時の CDN パージフローを追加する |
| M-09 | infra-ops-reviewer | 運用 | §16.3 SSE | SSE 接続のエラーハンドリングで「5 回連続失敗時は SSE を停止しポーリングにフォールバック」と記載されているが、フォールバック後の SSE 復帰条件が未定義。ネットワーク復帰後も永続的にポーリングになる | フォールバック後、一定時間（例: 30 秒）経過後に SSE 再接続を試行するリカバリロジックを追加する |
| M-10 | oss-reviewer | ライセンス | §1.2, §16.9 | shadcn/ui は MIT ライセンスで問題ないが、spec.md では `Headless UI`（MIT）を記載。両者の選定が未確定な状況で、依存ライブラリのライセンス整合性の最終確認ができない | コンポーネントライブラリの最終選定後にライセンス確認を実施する（E-01 のエスカレーション解決後） |
| M-11 | ux-accessibility-reviewer | アクセシビリティ | §8.2, §16.6 | `prefers-reduced-motion` メディアクエリへの対応が §8.2 で言及されている（WCAG SC 2.3.1）が、具体的にどのアニメーション（カルーセル、ページ遷移、トースト出現等）を制御するかの一覧が未定義 | 対象アニメーション一覧を定義し、各コンポーネントに `@media (prefers-reduced-motion: reduce)` でアニメーション無効化/簡略化を適用する仕様を追加する |
| M-12 | ux-accessibility-reviewer | アクセシビリティ | 全体 | ダークモード（`prefers-color-scheme: dark`）への対応が設計書全体で未言及。現代の EC サイトとしてダークモード対応は Phase 1 で必須ではないが、カラートークン設計の段階でダークモード対応を考慮しておく方が後の改修コストが低い | Phase 1 のスコープ判断を明記（Won't Have または Could Have）し、カラーパレット定義にダークモード用の CSS custom properties を予約する |
| M-13 | tech-lead | 総合 | §14 エスカレーション | エスカレーション項目が 8 件あるが、うち 5 件が「要確認」「要検討」のまま。特に E-01（リフレッシュトークン仕様）、E-07（ページネーション統一仕様）はフロントエンド実装のブロッカーとなり得る | エスカレーション項目の優先度を付け、Phase 1 ブロッカー（E-01, E-02, E-07, E-08）を早急に解決する |

## Low 指摘一覧
| # | 出典 Agent | カテゴリ | 指摘内容 |
|---|-----------|---------|----------|
| L-01 | business-analyst | 文書構成 | §15（追記セクション）が 10 サブセクションに分かれ長大。本来 §2〜§8 の各セクションに統合すべき内容が追記として分離されているため、重複と参照の複雑さが増している |
| L-02 | dba-reviewer | データ整合性 | §4.2 ページネーションレスポンスで `first`, `last`, `numberOfElements` フィールドが `hasNext`, `hasPrevious` と重複情報。バックエンド API が両方返すならフロントエンドの型定義も合わせるべきだが、不要なフィールドであればドキュメントから削除し型定義を簡素化する |
| L-03 | programing-reviewer | コード品質 | §16.2 でトークンリフレッシュの並行リクエストキュー管理のコード例が示されているが、`failedQueue` がモジュールスコープの可変変数。React の Strict Mode（開発時 2 回レンダリング）での意図しない副作用に注意が必要 |
| L-04 | audit-reviewer | ドキュメント管理 | 改訂履歴テーブルが存在しない。spec.md には改訂履歴テーブルがあるが、front-end-need.md には版管理の記録がない |
| L-05 | performance-reviewer | パフォーマンス | §16.8 で FID（First Input Delay）と INP（Interaction to Next Paint）の両方を計測すると記載。Google は既に FID から INP に完全移行しているため、FID の記載は削除または「レガシー参考値」として扱う |
| L-06 | release-manager | リリース | MoSCoW 分類の Could Have に「AI チャットボット」「レビュー投稿」が含まれるが、これらの Must/Should への昇格条件が未定義 |
| L-07 | ux-accessibility-reviewer | UX | §2.2 の在庫表示ルールで絵文字（⏳, ✓, 📦, 🚚, ✅ 等）が使用されているが、スクリーンリーダーでの読み上げ挙動がブラウザ・OS により異なる。`aria-hidden="true"` でアイコン絵文字を隠し、テキストラベルで代替する方が安定 |
| L-08 | ux-accessibility-reviewer | UX | §15.7 タッチターゲットサイズが 44×44px（WCAG 2.5.5 AAA）を AA 基準として採用と記載しているが、WCAG 2.5.8（2.2 追加）の AA 基準は 24×24px。44px はより厳格な基準であり問題ないが、根拠の記述を正確にすべき |
| L-09 | compliance-reviewer | 法規制 | §15.1 の `ConsentId` Cookie TTL が「1 年」と記載。ePrivacy 指令上は問題ないが、GDPR のデータ最小化原則に基づき TTL の妥当性説明（「同意状態の継続管理に必要な最小期間として 1 年」等）を追記する |

## ドキュメント横断分析

### マイクロサービス設計書カバレッジ（front-end-need.md からの言及度）
| サービス | 設計書 | API 定義 | 画面対応 | イベント連携 | 認証/認可 |
|---------|--------|---------|---------|------------|----------|
| ApiGateway | api-gateway-design.md | ✅ ベース URL 定義 | — | — | ✅ JWT 検証 |
| AuthService | authentication-service-design.md | ✅ §9.1 | ✅ 画面 9,10,23,24 | — | ✅ §5, §16.2 |
| UserManagementService | user-management-design.md | ✅ §9.2, §9.2a | ✅ 画面 11-15,25-27 | — | ✅ |
| InventoryManagementService | inventory-management-design.md | ✅ §9.3 | ✅ 画面 1-3 | ✅ 在庫 SSE | ✅ |
| SalesManagementService | sales-management-design.md | ✅ §9.5 | ✅ 画面 5-8,28 | ✅ 注文ステータス SSE | ✅ |
| PaymentCartService | payment-cart-service-design.md | ✅ §9.4, §9.9 | ✅ 画面 4-5 | — | ✅ |
| CouponService | coupon-service-design.md | ✅ §9.6 | ✅ 画面 17 | — | ✅ |
| PointService | point-service-design.md | ✅ §9.7 | ✅ 画面 18 | — | ✅ |
| MailSendService | mailsend-service-design.md | ✅ (内部) | — | — | — |
| AiSupportService | ai-support-service-design.md | ✅ §9.8 | ✅ 画面 19 | ✅ AI チャット SSE | ✅ |

### サービス間整合性
- **API エンドポイント整合性**: §9 の API 一覧は spec.md のサービス責務と概ね一致。ただし DSR API のパスプレフィックス不一致（H-02）あり
- **認証フロー整合性**: §5 の JWT 認証フロー（httpOnly Cookie）は spec.md の AuthService 設計と整合。OAuth フロー（§16.2）は authentication-service-design.md の Authorization Code Flow with PKCE と整合
- **Saga パターン UX 整合性**: §2.2 ステップ 5 のポーリング→SSE フォールバック戦略は spec.md ADR-0009 のフロントエンド要件と整合
- **ゲスト購入フロー整合性**: §2.2, §15.3 のゲスト購入フローは spec.md のゲスト購入設計と完全に整合（越境データ移転同意、メールアドレス 2 回入力一致検証を含む）

### Kafka イベント定義整合性
| イベント | 発行元 (spec.md) | フロントエンド対応 (front-end-need.md) | 整合性 |
|---------|-----------------|--------------------------------------|--------|
| `consent.revoked` | UserManagementService | §15.1 でフロントエンドの Cookie/タグ制御に反映 | ✅ |
| `inventory.stock_updated` | InventoryManagementService | §16.3 SSE でリアルタイム在庫通知に変換 | ✅ |
| `order.status_changed` | SalesManagementService | §16.3 SSE で注文ステータス通知に変換 | ✅ |
| `cart.abandoned` | PaymentCartService | spec.md に定義あり、front-end-need.md でのフロントエンド対応は不要（バックエンド処理） | ✅ N/A |

### 記載カバレッジ分析
| 項目 | カバレッジ | 備考 |
|------|----------|------|
| EC 画面（一般ユーザー） | ✅ 高 | 28 画面を網羅、各画面の構成要素・API 呼出し・アクセシビリティ要件を詳細に定義 |
| 管理画面 | ⚠️ 中 | 画面一覧は定義済みだが、各画面の詳細仕様（構成要素、バリデーション等）が不足 |
| 認証フロー | ✅ 高 | JWT、OAuth、トークンリフレッシュ、ログアウトの全フローを詳細に定義 |
| エラーハンドリング | ✅ 高 | RFC 9457 準拠、ステータスコード別 UI 表示、グレースフルデグラデーション |
| アクセシビリティ | ✅ 高 | WCAG 2.1 AA 準拠要件を広範にカバー |
| セキュリティ | ✅ 高 | CSP、XSS、CSRF、SRI、httpOnly Cookie |
| パフォーマンス | ✅ 高 | Core Web Vitals、コード分割、画像最適化、キャッシュ戦略 |
| 国際化 | ✅ 中-高 | i18n ライブラリ、翻訳キー構造、日付・通貨フォーマット定義済み |
| テスト | ⚠️ 中 | 戦略定義済みだが、フレームワーク未確定（H-04） |
| レンダリング戦略 | ⚠️ 低 | ページ単位の SSR/SSG/ISR/CSR が未整理（M-01） |

### 未定義・曖昧な領域
| # | 領域 | ブロッカーリスク | 備考 |
|---|------|----------------|------|
| 1 | テストフレームワーク最終選定 | **高** | CI パイプライン構築不可（H-04） |
| 2 | 管理画面の画面詳細仕様 | 中 | 画面一覧のみ。各画面のモックアップ・構成要素は未定義 |
| 3 | SSR/CSR レンダリング方式のページ別定義 | 中 | ビルド設定・パフォーマンスに直結（M-01） |
| 4 | リアルタイム通知方式の最終確定 | 中 | SSE 推奨だがバックエンド対応が未確認（E-03） |
| 5 | コンポーネントライブラリ選定 | 中 | shadcn/ui vs Headless UI（E-01） |
| 6 | Error Boundary コンポーネント設計 | 低 | React Error Boundary の設計が未定義 |
| 7 | Service Worker / PWA 対応 | 低 | オフライン時の動作が未定義 |

## 各 Agent 詳細レポート

<details>
<summary>business-analyst レビューレポート</summary>

### 評価: ⚠️ Medium 2 件 / Low 1 件

**良好な点**:
- 画面一覧マトリクス（§2.1）が 28 画面を網羅し、MoSCoW 優先度が明確
- ユーザーペルソナとの紐付け（spec.md 準拠）が適切
- ゲスト購入フロー（§2.2, §15.5）がカート離脱率 30% 以下の KPI 達成に直結する設計
- チェックアウト 5 ステップフローが spec.md §チェックアウト・ステップインジケーター設計と整合
- 再注文機能（§15.3）の受入基準が spec.md ペルソナ 1 ストーリーと合致

**指摘事項**:
- **M-01**: ページ別レンダリング戦略（SSR/SSG/ISR/CSR）の体系的整理不足
- **M-02**: AI チャットウィジェットの表示制御・レスポンシブ挙動の詳細不足
- **L-01**: §15 追記セクションの肥大化による構造的複雑さ

</details>

<details>
<summary>architect レビューレポート</summary>

### 評価: ⚠️ High 2 件 / Medium 1 件

**良好な点**:
- バックエンドサービス一覧（§1.3）とポート番号が spec.md と完全一致
- API Gateway (YARP) 経由のパスマッピングが適切に定義
- Saga パターンの UX 対応（ポーリング + SSE フォールバック）が Saga 設計との整合性を保っている
- Stripe Hosted Payment Page による PCI DSS 非保持化（ADR-0008 準拠）が正確に反映
- Outbox パターンの存在をフロントエンドが意識せず、ステータスポーリングで抽象化している点は良設計

**指摘事項**:
- **H-01**: 管理画面技術スタックの矛盾（Razor Pages vs TypeScript SPA コード混在）
- **H-05**: 同意管理 API のパスパラメータ名不一致（`{consentType}` vs `{type}`）
- **M-03**: 状態管理ライブラリ（Redux Toolkit / Zustand）の未確定

</details>

<details>
<summary>programing-reviewer レビューレポート</summary>

### 評価: ✅ Medium 2 件 / Low 1 件

**良好な点**:
- TypeScript の型定義が充実（`ProblemDetails`, `PaginatedResult<T>`, `StockChangeEvent` 等）
- TanStack Query の使用パターン（楽観的更新 §6.3、ページネーション §16.4）が実践的
- `apiClient` の設定（§4.4）が httpOnly Cookie 方式に対応し、`withCredentials: true` が正しく設定
- トークンリフレッシュのキューイング機構（§16.2）が並行リクエストの競合を適切に処理

**指摘事項**:
- **M-04**: `console.error` の使用（本番環境では不適切）
- **M-05**: PascalCase → camelCase 変換の簡易実装のエッジケース
- **L-03**: `failedQueue` のモジュールスコープ可変変数の React Strict Mode 互換性

</details>

<details>
<summary>dba-reviewer レビューレポート</summary>

### 評価: ✅ Low 1 件

**良好な点**:
- ページネーションパラメータ（page: 0-indexed, size）がバックエンド ASP.NET Core 準拠
- レスポンス型（`PaginatedResult<T>`）がバックエンドの DTO と対応
- フロントエンド側で税額計算を行わず、バックエンド API レスポンス値をそのまま表示する設計方針が正しい

**指摘事項**:
- **L-02**: ページネーションレスポンスの冗長フィールド（`first`/`last` と `hasNext`/`hasPrevious` の重複）

</details>

<details>
<summary>security-reviewer レビューレポート</summary>

### 評価: ⚠️ High 1 件 / Medium 1 件

**良好な点**:
- JWT を httpOnly Cookie に保存し、`localStorage` 使用禁止を明記（H9 是正(C9-02) 準拠）
- CSRF 対策として SameSite=Strict + CSRF トークンの双方を採用
- CSP ヘッダーが明確に定義（Stripe ドメインのホワイトリスト含む）
- SRI（Subresource Integrity）による外部スクリプト改ざん防止
- DOMPurify によるリッチテキスト出力のサニタイズ
- `dangerouslySetInnerHTML` 使用禁止の ESLint ルール化
- OAuth コールバック処理がバックエンドに集約され、フロントエンドでトークン交換を行わない安全な設計
- ゲスト注文追跡 API のレート制限（5 req/分/IP）とエラーメッセージの情報漏洩防止

**指摘事項**:
- **H-03**: CSP `style-src 'unsafe-inline'` の許容と nonce 移行計画の欠如
- **M-06**: DOMPurify の `a` タグに対する `rel="noopener noreferrer"` 強制ロジック未定義
- **H-02**: API パスプレフィックス不統一（セキュリティルーティングの一貫性に影響）

</details>

<details>
<summary>compliance-reviewer レビューレポート</summary>

### 評価: ✅ Medium 1 件 / Low 1 件

**良好な点**:
- Cookie 同意バナー（§15.1）が GDPR 第 7 条 + ePrivacy 指令に準拠した 4 カテゴリ個別同意を実装
- 未認証ユーザーの同意管理（`/consents/anonymous`）が適切に設計
- ゲスト購入時の越境データ移転同意（SendGrid — 米国）がオプトイン方式で実装
- DSR 画面（§15.2）が GDPR 第 17 条（削除権）、第 20 条（ポータビリティ権）に対応
- 同意撤回時のデータ処理停止フローが Kafka イベント連携で設計済み
- 「プライバシーポリシーに同意する」チェックボックスがゲスト購入フォームに含まれている
- 個人情報の利用目的要約テキストがゲスト購入フォームに配置

**指摘事項**:
- **M-07**: Cookie 同意バナーの `Escape` キー動作の法的解釈の曖昧さ
- **L-09**: `ConsentId` Cookie TTL の妥当性説明不足

</details>

<details>
<summary>audit-reviewer レビューレポート</summary>

### 評価: ✅ Low 1 件

**良好な点**:
- Correlation ID のリクエスト/レスポンス連携（§16.3）が適切に設計
- エラー画面の「サポートに連絡」リンクに Correlation ID を含める設計がトレーサビリティを確保
- 同意バージョン管理（§15.1）と再同意バナー表示のフローが spec.md と整合
- DSR リクエストの 30 日処理期限（GDPR 第 12 条 3 項）が §15.2 に反映

**指摘事項**:
- **L-04**: 改訂履歴テーブルが文書に存在しない

</details>

<details>
<summary>qa-manager レビューレポート</summary>

### 評価: ⚠️ High 1 件

**良好な点**:
- テスト戦略（§8.5）が単体/統合/E2E/アクセシビリティ/パフォーマンスの 5 層を定義
- axe-core による WCAG 2.1 AA 自動テスト、Lighthouse CI による Core Web Vitals 監視
- CI 統合ルール（PR ごとの自動テスト、main マージ前の E2E）が定義済み
- カバレッジ目標（分岐 80% 以上）が spec.md のバックエンド目標と統一

**指摘事項**:
- **H-04**: テストフレームワーク未確定（Jest/Cypress vs Vitest/Playwright）が CI パイプラインのブロッカー

</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

### 評価: ✅ Medium 1 件 / Low 1 件

**良好な点**:
- Core Web Vitals 目標値（LCP ≤ 2.5s, INP ≤ 200ms, CLS ≤ 0.1）が明確で計測可能
- コード分割戦略（§16.8）が AI チャット、Stripe 決済等の重いコンポーネントを遅延読み込み
- TanStack Query のキャッシュ戦略（§16.8）が `staleTime`/`gcTime` をデータ種別ごとに設定
- BlurHash プレースホルダーによる CLS 防止策
- WebP/AVIF 画像最適化が明示
- Next.js の `keepPreviousData` によるページネーション切替時のチラつき防止

**指摘事項**:
- **M-08**: CDN キャッシュ戦略（商品画像の Cache-Control、CDN パージ方針）が未定義
- **L-05**: FID の記載が Google の INP 移行後として冗長

</details>

<details>
<summary>infra-ops-reviewer レビューレポート</summary>

### 評価: ✅ Medium 1 件

**良好な点**:
- グレースフルデグラデーション戦略（§4.3）が全サービス障害時のフォールバック動作を定義
- SSE（Server-Sent Events）によるリアルタイム通知のアーキテクチャが適切
- API ベース URL が環境変数（`NEXT_PUBLIC_API_BASE_URL`）で設定可能

**指摘事項**:
- **M-09**: SSE フォールバック後の復帰条件が未定義

</details>

<details>
<summary>release-manager レビューレポート</summary>

### 評価: ✅ Low 1 件

**良好な点**:
- MoSCoW 優先度分類（§11）が Phase 1 の Must Have を明確に定義
- Won't Have（モバイルアプリ、ライブチャット、サブスクリプション等）が明確にスコープ外
- Phase 1 / Phase 2 のスコープ判断が法人機能、NPS 測定等で適切に分離

**指摘事項**:
- **L-06**: Could Have 項目の昇格条件が未定義

</details>

<details>
<summary>oss-reviewer レビューレポート</summary>

### 評価: ✅ Medium 1 件

**良好な点**:
- 使用フロントエンド OSS はすべて MIT / Apache 2.0 等の permissive license
- DOMPurify（Apache 2.0 or MIT dual license）の使用が適切
- Stripe.js の SRI 対応が明記

**指摘事項**:
- **M-10**: コンポーネントライブラリ未確定（shadcn/ui vs Headless UI）によるライセンス最終確認の保留

</details>

<details>
<summary>ux-accessibility-reviewer レビューレポート</summary>

### 評価: ⚠️ Medium 2 件 / Low 2 件

**良好な点**:
- WCAG 2.1 AA 準拠要件が非常に詳細（§8.2, §16.6）。知覚可能・操作可能・理解可能・堅牢の 4 原則をカバー
- チェックアウトフローのステップインジケーターに `aria-current="step"` を適用
- フォーカストラップ（モーダル、Cookie 同意バナー）が適切に仕様化
- ステータスメッセージ（WCAG SC 4.1.3）の `aria-live` 使い分けが明確
- スキップリンクの配置仕様が WCAG SC 2.4.1 に準拠
- 注文確認画面（WCAG SC 3.3.4）の「確認→修正→確定」フローが適切に設計
- 色非依存の情報伝達（テキストラベル・アイコン併用）が明記
- 44×44px タッチターゲットサイズは十分に大きい
- キーボードナビゲーション要件テーブル（§16.6）が主要コンポーネントをカバー
- ランドマーク構造（`<header>`, `<nav>`, `<main>`, `<aside>`, `<footer>`）が明確

**指摘事項**:
- **M-11**: `prefers-reduced-motion` の対象アニメーション一覧が未定義
- **M-12**: ダークモード（`prefers-color-scheme`）対応の Phase 1 スコープが未判断
- **L-07**: 注文ステータス表示の絵文字がスクリーンリーダーで一貫した読み上げにならない可能性
- **L-08**: タッチターゲットサイズの WCAG 根拠記述の不正確さ（2.5.5 AAA vs 2.5.8 AA）

</details>

<details>
<summary>tech-lead レビューレポート（初期レビュー）</summary>

### 評価: ⚠️ High 1 件 / Medium 1 件

**総合評価**:
front-end-need.md は **約 2,400 行** に及ぶ詳細な設計書であり、EC 画面 28 画面のフルスペック、認証フロー、エラーハンドリング、アクセシビリティ、セキュリティ、パフォーマンス、国際化、GDPR 対応を包括的にカバーしている。spec.md との整合性は概ね良好で、ゲスト購入フロー・Saga パターン UX・Cookie 同意管理等の複雑な機能が正確に反映されている。

**最大の課題は以下の 2 点**:
1. 管理画面技術スタックの矛盾（H-01）: spec.md は Razor Pages を明記しているが、TypeScript ベースのコード例が混在
2. エスカレーション項目の未解決（M-13）: 8 件中 5 件が「要確認」のままでフロントエンド実装のブロッカーとなり得る

**AGENTS.md / Instructions 準拠状況**:
- コーディング規約（TypeScript 版）: 型安全性を徹底。`any` 型の使用なし ✅
- セキュリティ規約: OWASP Top 10 対応（XSS, CSRF, CSP）が詳細 ✅
- API 設計規約: RFC 9457 準拠のエラーハンドリングが適切 ✅
- DDD 原則: フロントエンドはバックエンド Aggregate Root の直接参照を避け、API 経由でアクセスしている ✅

**指摘事項**:
- **H-01**: 管理画面技術スタック矛盾（architect と共同指摘）
- **M-13**: エスカレーション項目の未解決が Phase 1 実装のブロッカーリスク

</details>

---

## 総合所見

front-end-need.md は SkiShop プロジェクトのフロントエンド要件を**非常に網羅的**に定義した高品質な設計書である。spec.md との整合性は大部分で確保されており、特に以下の領域は優れた設計を示している:

1. **GDPR 対応**: Cookie 同意バナー、DSR 画面、越境データ移転同意がフロントエンド観点で詳細に設計
2. **アクセシビリティ**: WCAG 2.1 AA 準拠が実装レベルの具体性で定義（ARIA 属性、キーボード操作、フォーカス管理）
3. **エラーハンドリング**: RFC 9457 準拠のバックエンド連携が TypeScript 型定義まで落とし込まれている
4. **Saga パターン UX**: 複雑な注文確定フローのフロントエンド表現（ポーリング + SSE フォールバック）が適切

**Phase 1 実装着手にあたり、以下の High 指摘 5 件の解決を推奨する。**
