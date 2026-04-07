# fix-report-2: api-gateway-design.md

## 修正サマリー

| 指摘 ID | 重要度 | 内容 | 対応状況 |
|---------|--------|------|---------|
| H-NEW-1 | High | 4 YARP routes 欠落 + user-cluster 未定義 | ✅ 修正完了 |
| H-NEW-2 | High | coupons-route の AuthorizationPolicy 分割不足 | ✅ 修正完了 |

## 修正詳細

### H-NEW-1: YARP routes + user-cluster 追加
- §13 Routes に `users-route`（default, user-cluster）、`reports-route`（AdminOrManager, sales-cluster）、`ai-search-route`（anonymous, ai-cluster）、`ai-analytics-route`（AdminOrManager, ai-cluster）の 4 ルートを追加
- §13 Clusters に `user-cluster`（http://user-management-service:5002、HealthCheck 付き）を追加

### H-NEW-2: coupons-route 分割
- 既存の `coupons-route` を 3 ルートに分割:
  - `coupons-public-route`: GET /api/coupons → anonymous（公開クーポン検索）
  - `coupons-apply-route`: POST /api/coupons/apply → default（認証必須）
  - `coupons-route`: /api/coupons/{**catch-all} → default（その他認証必須）

## 結果: Critical 0 / High 0（2 件修正完了）
