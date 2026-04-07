# fix-report-2: front-end-need.md

## 修正サマリー

| 指摘 ID | 重要度 | 内容 | 対応状況 |
|---------|--------|------|---------|
| H2-01 | High | §5.1/§5.2 認証フロー図が Bearer JWT のまま（httpOnly Cookie 方針と矛盾） | ✅ 修正完了 |
| H2-02 | High | チェックアウト API フローのステップ番号誤り（ステップ 3 → ステップ 4） | ✅ 修正完了 |
| H2-03 | High | Mermaid 図で CHECKOUT_PAY → STRIPE の直接遷移（Confirm 迂回） | ✅ 修正完了 |

## 修正詳細

### H2-01: 認証フロー図の httpOnly Cookie 対応
- §5.1: `Authorization: Bearer {JWT}` → `Cookie: access_token={JWT}（自動送信）` に変更
- §5.2: `Authorization: Bearer {期限切れ}` → `Cookie: access_token={期限切れJWT}` に変更  
- §5.2: `{ refreshToken }` → `Cookie: refresh_token={RT}（自動送信）` に変更

### H2-02: ステップ番号修正
- API フロー: `ステップ 3（注文確定 → Stripe リダイレクト）` → `ステップ 3: UI 操作のみ（支払い方法選択 — API 呼び出しなし）` + `ステップ 4（注文確定 → Stripe リダイレクト）` に分割

### H2-03: Mermaid 図の遷移修正
- `CHECKOUT_PAY --> STRIPE` を削除
- `CHECKOUT_PAY --> CHECKOUT_CONFIRM --> STRIPE --> CHECKOUT_COMPLETE` の正しい順序に修正
- Confirm を必ず経由する遷移に変更（WCAG SC 3.3.4 準拠）

## 結果: Critical 0 / High 0（3 件修正完了）
