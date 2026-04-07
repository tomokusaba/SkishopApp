# AiSupportService API 検証レポート

**実施日時**: 2026-04-06  
**検証環境**: Docker Compose (localhost:5009)  
**検証対象**: AiSupportService v1.0

---

## 目次

1. [概要](#1-概要)
2. [テスト結果サマリー](#2-テスト結果サマリー)
3. [Phase 0: ヘルスチェック](#3-phase-0-ヘルスチェック)
4. [Phase 1: Search Endpoints](#4-phase-1-search-endpoints)
5. [Phase 2: Recommendations Endpoints](#5-phase-2-recommendations-endpoints)
6. [Phase 3: Chat Endpoints](#6-phase-3-chat-endpoints)
7. [Phase 4: Admin Forecast Endpoints](#7-phase-4-admin-forecast-endpoints)
8. [Phase 5: Admin Analytics Endpoints](#8-phase-5-admin-analytics-endpoints)
9. [Phase 6: Admin Model Endpoints](#9-phase-6-admin-model-endpoints)
10. [Appendix: 修正事項と問題点](#appendix-修正事項と問題点)

---

## 1. 概要

本レポートは AiSupportService で提供される全 API エンドポイントの動作検証結果を記録したものです。各エンドポイントに対して実際に curl コマンドでリクエストを送信し、レスポンスを確認しました。

### 検証環境の構成

```
- AiSupportService: localhost:5009
- AuthService: localhost:5001 (トークン取得用)
- PostgreSQL: skishop-postgres (aisupportdb)
- Redis: skishop-redis
- Kafka: skishop-kafka
- Azure OpenAI: gpt-5 モデル
```

### 前提条件

検証開始前に以下の修正を適用:

1. **データベース修正**: `user_profiles`, `chat_sessions`, `model_trainings` テーブルの `row_version` カラムにデフォルト値を設定
   ```sql
   ALTER TABLE user_profiles ALTER COLUMN row_version SET DEFAULT '\x00000000'::bytea;
   ALTER TABLE chat_sessions ALTER COLUMN row_version SET DEFAULT '\x00000000'::bytea;
   ALTER TABLE model_trainings ALTER COLUMN row_version SET DEFAULT '\x00000000'::bytea;
   ```

2. **AdminOnly ポリシーの修正**: `Program.cs` で `RequireRole("Admin")` を `RequireRole("Admin", "ADMIN")` に変更

3. **Semantic Kernel パラメータ修正**: 
   - `max_tokens` → `max_completion_tokens` に変更（gpt-5/o1 系モデル対応）
   - `temperature` パラメータを削除（gpt-5/o1 系モデルでは 1.0 固定のため）

4. **匿名ユーザープロファイルの追加**: trending/seasonal/similar/frequently-bought エンドポイント用  
   ※ 当初 `"system"` というユーザー ID を使用していたが、テックリードの判断により `"anonymous"` に変更。
   理由: `system` は管理者・特権操作を連想させるが、実際は匿名（未ログイン）ユーザー向けの公開機能であり、
   用途を正確に表す `anonymous` を使用することで可読性・保守性が向上する。
   
   **EF Core マイグレーション** (`Migrations/20260406130800_AddAnonymousUserProfile.cs`) を追加:
   ```sql
   INSERT INTO user_profiles (id, user_id, preferences_json, ...)
   VALUES (
       '00000000-0000-0000-0000-000000000001', 
       'anonymous', 
       '{"type": "anonymous", "description": "匿名ユーザー向けレコメンデーション用プロファイル"}',
       ...
   );
   ```

### テストトークン取得方法

```bash
# 一般ユーザートークン取得
LOGIN_RESPONSE=$(curl -s -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email": "verifytest1@example.com", "password": "Password123!"}')
USER_TOKEN=$(echo "$LOGIN_RESPONSE" | python3 -c "import sys, json; print(json.load(sys.stdin)['accessToken'])")

# 管理者トークン取得
ADMIN_RESPONSE=$(curl -s -X POST http://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email": "admin@example.com", "password": "AdminPass123!"}')
ADMIN_TOKEN=$(echo "$ADMIN_RESPONSE" | python3 -c "import sys, json; print(json.load(sys.stdin)['accessToken'])")
```

---

## 2. テスト結果サマリー

| Phase | テスト項目 | 結果 | 備考 |
|-------|-----------|------|------|
| 0 | GET /health | ✅ PASS | Liveness チェック |
| 0 | GET /health/ready | ✅ PASS | Readiness チェック |
| 1 | GET /api/v1/ai/search | ✅ PASS | 商品検索 |
| 1 | GET /api/v1/ai/search/suggest | ✅ PASS | 検索サジェスト |
| 1 | POST /api/v1/ai/search/feedback (認証なし) | ✅ PASS | 401 Unauthorized |
| 1 | POST /api/v1/ai/search/feedback (認証あり) | ✅ PASS | 204 No Content |
| 2 | GET /recommendations/trending | ✅ PASS | トレンド商品（修正後） |
| 2 | GET /recommendations/seasonal | ✅ PASS | 季節のおすすめ（修正後） |
| 2 | GET /recommendations/similar/{id} | ✅ PASS | 類似商品 |
| 2 | GET /recommendations/frequently-bought/{id} | ✅ PASS | よく一緒に購入される商品 |
| 2 | GET /recommendations/personalized (認証なし) | ✅ PASS | 401 Unauthorized |
| 2 | GET /recommendations/personalized (認証あり) | ✅ PASS | 空リスト返却 |
| 2 | POST /recommendations/feedback (認証なし) | ✅ PASS | 401 Unauthorized |
| 2 | POST /recommendations/feedback (認証あり) | ✅ PASS | 404（データなし） |
| 3 | POST /chat/sessions (認証なし) | ✅ PASS | 401 Unauthorized |
| 3 | POST /chat/sessions (認証あり) | ✅ PASS | 201 Created |
| 3 | GET /chat/sessions | ✅ PASS | セッション一覧 |
| 3 | GET /chat/sessions/{id} | ✅ PASS | セッション詳細 |
| 3 | GET /chat/sessions/{id}/messages | ✅ PASS | メッセージ一覧 |
| 3 | POST /chat/sessions/{id}/messages | ✅ PASS | AI応答生成 |
| 3 | POST /chat/sessions/{id}/close | ✅ PASS | 204 No Content |
| 3 | POST /chat/sessions/{id}/escalate | ✅ PASS | 有人エスカレーション |
| 4 | GET /admin/ai/forecast (認証なし) | ✅ PASS | 401 Unauthorized |
| 4 | GET /admin/ai/forecast (一般ユーザー) | ✅ PASS | 403 Forbidden |
| 4 | GET /admin/ai/forecast (Admin) | ✅ PASS | 空リスト返却 |
| 4 | GET /admin/ai/forecast/{productId} | ✅ PASS | 商品別予測 |
| 4 | POST /admin/ai/forecast/generate | ✅ PASS | 201 Created |
| 5 | GET /admin/ai/analytics/search | ✅ PASS | 検索分析 |
| 5 | GET /admin/ai/analytics/recommendations | ✅ PASS | 推薦分析 |
| 5 | GET /admin/ai/analytics/chat | ✅ PASS | チャット分析 |
| 6 | GET /admin/ai/models | ✅ PASS | モデル一覧 |
| 6 | POST /admin/ai/models/{name}/train | ✅ PASS | 201 Created |
| 6 | GET /admin/ai/models (一般ユーザー) | ✅ PASS | 403 Forbidden |

**全体結果**: 33/33 テスト PASS（100%）

---

## 3. Phase 0: ヘルスチェック

### Test 0-1: GET /health (Liveness)

**リクエスト**:
```bash
curl -s http://localhost:5009/health
```

**レスポンス**:
```
Healthy
```

**HTTP Status**: 200 OK  
**結果**: ✅ PASS

---

### Test 0-2: GET /health/ready (Readiness)

**リクエスト**:
```bash
curl -s http://localhost:5009/health/ready
```

**レスポンス**:
```
Healthy
```

**HTTP Status**: 200 OK  
**結果**: ✅ PASS

---

## 4. Phase 1: Search Endpoints

### Test 1-1: GET /api/v1/ai/search (Anonymous)

**リクエスト**:
```bash
curl -s "http://localhost:5009/api/v1/ai/search?Query=ski&Page=1&PageSize=10"
```

**レスポンス**:
```json
{"query":"ski","totalCount":0,"items":[]}
```

**HTTP Status**: 200 OK  
**結果**: ✅ PASS（データなしは想定通り）

---

### Test 1-2: GET /api/v1/ai/search/suggest (Anonymous)

**リクエスト**:
```bash
curl -s "http://localhost:5009/api/v1/ai/search/suggest?query=スキー"
```

**レスポンス**:
```json
[]
```

**HTTP Status**: 200 OK  
**結果**: ✅ PASS

---

### Test 1-3: POST /api/v1/ai/search/feedback (認証なし)

**リクエスト**:
```bash
curl -s -X POST "http://localhost:5009/api/v1/ai/search/feedback" \
  -H "Content-Type: application/json" \
  -d '{"searchId":"search-123","productId":"prod-1"}'
```

**HTTP Status**: 401 Unauthorized  
**結果**: ✅ PASS（認証なしで 401 は想定通り）

---

### Test 1-4: POST /api/v1/ai/search/feedback (認証あり)

**リクエスト**:
```bash
curl -s -X POST "http://localhost:5009/api/v1/ai/search/feedback" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer ${USER_TOKEN}" \
  -d '{"searchId":"search-123","productId":"prod-1"}'
```

**HTTP Status**: 204 No Content  
**結果**: ✅ PASS

---

## 5. Phase 2: Recommendations Endpoints

### Test 2-1: GET /api/v1/ai/recommendations/trending (Anonymous)

**リクエスト**:
```bash
curl -s "http://localhost:5009/api/v1/ai/recommendations/trending"
```

**レスポンス**:
```json
[
    {
        "id": "feb2096d-8d84-4aa4-8647-c6f47b37d623",
        "type": "TRENDING",
        "productIds": [],
        "score": 0.9,
        "reason": "現在人気のトレンド商品",
        "createdAt": "2026-04-06T13:18:34.2413548Z"
    }
]
```

**HTTP Status**: 200 OK  
**結果**: ✅ PASS

**備考**: 
- 匿名ユーザープロファイル (`user_id = 'anonymous'`) を使用してレコメンデーションを生成・保存
- `productIds` が空配列なのは、商品マスタ（InventoryManagementService）が未稼働のため
- レスポンスには有効期限 6 時間のトレンドレコメンデーションが返却される

---

### Test 2-2: GET /api/v1/ai/recommendations/seasonal (Anonymous)

**リクエスト**:
```bash
curl -s "http://localhost:5009/api/v1/ai/recommendations/seasonal"
```

**レスポンス**:
```json
[
    {
        "id": "811998fd-65a8-4129-9a9b-52f35683ea44",
        "type": "SEASONAL",
        "productIds": [],
        "score": 0.8,
        "reason": "今シーズンのおすすめ商品",
        "createdAt": "2026-04-06T13:18:34.2846208Z"
    }
]
```

**HTTP Status**: 200 OK  
**結果**: ✅ PASS

**備考**: 
- 匿名ユーザープロファイル (`user_id = 'anonymous'`) を使用
- 季節判定ロジック: 4月は春期間（4月〜6月）→「春 新シーズン」で検索
- 有効期限 30 日の季節レコメンデーションが返却される

---

### Test 2-3: GET /api/v1/ai/recommendations/similar/{productId} (Anonymous)

**リクエスト**:
```bash
curl -s "http://localhost:5009/api/v1/ai/recommendations/similar/prod-001"
```

**レスポンス**:
```json
[]
```

**HTTP Status**: 200 OK  
**結果**: ✅ PASS

**備考**: 
- `productId = "prod-001"` の類似商品を検索
- 空配列は商品マスタ（InventoryManagementService）が未稼働のため、基準商品が見つからない正常な挙動
- 商品が存在する場合は最大 5 件の類似商品が返却される

---

### Test 2-4: GET /api/v1/ai/recommendations/frequently-bought/{productId} (Anonymous)

**リクエスト**:
```bash
curl -s "http://localhost:5009/api/v1/ai/recommendations/frequently-bought/prod-001"
```

**レスポンス**:
```json
[]
```

**HTTP Status**: 200 OK  
**結果**: ✅ PASS

**備考**: 
- `productId = "prod-001"` とよく一緒に購入される商品を検索
- 空配列は商品マスタ（InventoryManagementService）が未稼働のため正常な挙動
- 商品が存在する場合は最大 3 件の併売商品が返却される

---

### Test 2-5: GET /api/v1/ai/recommendations/personalized (認証なし)

**リクエスト**:
```bash
curl -s "http://localhost:5009/api/v1/ai/recommendations/personalized?page=1&pageSize=10"
```

**HTTP Status**: 401 Unauthorized  
**結果**: ✅ PASS

---

### Test 2-6: GET /api/v1/ai/recommendations/personalized (認証あり)

**リクエスト**:
```bash
curl -s "http://localhost:5009/api/v1/ai/recommendations/personalized?page=1&pageSize=10" \
  -H "Authorization: Bearer ${USER_TOKEN}"
```

**レスポンス**:
```json
{"items":[],"totalCount":0,"page":1,"pageSize":10,"totalPages":0}
```

**HTTP Status**: 200 OK  
**結果**: ✅ PASS（データなしは想定通り）

---

### Test 2-7: POST /api/v1/ai/recommendations/feedback (認証あり)

**リクエスト**:
```bash
curl -s -X POST "http://localhost:5009/api/v1/ai/recommendations/feedback" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer ${USER_TOKEN}" \
  -d '{"recommendationId":"rec-123","feedbackType":"CLICK","productId":"prod-1"}'
```

**レスポンス**:
```json
{"title":"Not Found","status":404,"detail":"レコメンデーション rec-123 が見つかりません"}
```

**HTTP Status**: 404  
**結果**: ✅ PASS（存在しないレコメンデーション ID に対する正しいエラー）

---

## 6. Phase 3: Chat Endpoints

### Test 3-1: POST /api/v1/ai/chat/sessions (認証なし)

**リクエスト**:
```bash
curl -s -X POST "http://localhost:5009/api/v1/ai/chat/sessions" \
  -H "Content-Type: application/json" \
  -d '{"topic":"製品問い合わせ"}'
```

**HTTP Status**: 401 Unauthorized  
**結果**: ✅ PASS

---

### Test 3-2: POST /api/v1/ai/chat/sessions (認証あり)

**リクエスト**:
```bash
curl -s -X POST "http://localhost:5009/api/v1/ai/chat/sessions" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer ${USER_TOKEN}" \
  -d '{"topic":"スキー用品相談"}'
```

**レスポンス**:
```json
{
  "id": "e39d04f7-df83-41a3-82d0-3e6480e0ea1e",
  "userId": "3b8d4cfc-631f-42d7-b273-e670f20faea4",
  "title": "新しいチャット",
  "status": "ACTIVE",
  "createdAt": "2026-04-06T12:42:42.331625Z",
  "updatedAt": "2026-04-06T12:42:42.331625Z",
  "closedAt": null
}
```

**HTTP Status**: 201 Created  
**結果**: ✅ PASS

---

### Test 3-3: GET /api/v1/ai/chat/sessions (認証あり)

**リクエスト**:
```bash
curl -s "http://localhost:5009/api/v1/ai/chat/sessions?page=1&pageSize=10" \
  -H "Authorization: Bearer ${USER_TOKEN}"
```

**レスポンス**:
```json
{
  "items": [...],
  "totalCount": 1,
  "page": 1,
  "pageSize": 10,
  "totalPages": 1
}
```

**HTTP Status**: 200 OK  
**結果**: ✅ PASS

---

### Test 3-4: GET /api/v1/ai/chat/sessions/{sessionId} (認証あり)

**リクエスト**:
```bash
curl -s "http://localhost:5009/api/v1/ai/chat/sessions/${SESSION_ID}" \
  -H "Authorization: Bearer ${USER_TOKEN}"
```

**HTTP Status**: 200 OK  
**結果**: ✅ PASS

---

### Test 3-5: POST /api/v1/ai/chat/sessions/{sessionId}/messages (認証あり)

**リクエスト**:
```bash
curl -s -X POST "http://localhost:5009/api/v1/ai/chat/sessions/${SESSION_ID}/messages" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer ${USER_TOKEN}" \
  -d '{"message":"こんにちは、初心者向けのスキー板を探しています。"}'
```

**レスポンス**:
```json
{
  "id": "6ab5feac-ff80-4ebf-9813-8aa57296b2e0",
  "sessionId": "e39d04f7-df83-41a3-82d0-3e6480e0ea1e",
  "role": "assistant",
  "content": "ご相談ありがとうございます。初心者の方が扱いやすいスキー板の選び方と、まず押さえておきたいポイントをご案内します。...",
  "tokenCount": null,
  "createdAt": "2026-04-06T12:43:26.2575335Z"
}
```

**HTTP Status**: 201 Created  
**結果**: ✅ PASS

**備考**: Azure OpenAI (gpt-5) による AI 応答が正常に生成されました。応答時間は約 44 秒。

---

### Test 3-6: GET /api/v1/ai/chat/sessions/{sessionId}/messages (認証あり)

**リクエスト**:
```bash
curl -s "http://localhost:5009/api/v1/ai/chat/sessions/${SESSION_ID}/messages?page=1&pageSize=10" \
  -H "Authorization: Bearer ${USER_TOKEN}"
```

**レスポンス**:
```json
{
  "items": [
    {"role": "user", "content": "こんにちは、初心者向けのスキー板を探しています。"},
    {"role": "assistant", "content": "ご相談ありがとうございます..."}
  ],
  "totalCount": 2,
  "page": 1,
  "pageSize": 10,
  "totalPages": 1
}
```

**HTTP Status**: 200 OK  
**結果**: ✅ PASS

---

### Test 3-7: POST /api/v1/ai/chat/sessions/{sessionId}/close (認証あり)

**リクエスト**:
```bash
curl -s -X POST "http://localhost:5009/api/v1/ai/chat/sessions/${SESSION_ID}/close" \
  -H "Authorization: Bearer ${USER_TOKEN}"
```

**HTTP Status**: 204 No Content  
**結果**: ✅ PASS

---

### Test 3-8: POST /api/v1/ai/chat/sessions/{sessionId}/escalate (認証あり)

**リクエスト**:
```bash
curl -s -X POST "http://localhost:5009/api/v1/ai/chat/sessions/${SESSION_ID}/escalate" \
  -H "Authorization: Bearer ${USER_TOKEN}"
```

**レスポンス**:
```json
{"message":"有人サポートにエスカレーションしました"}
```

**HTTP Status**: 200 OK  
**結果**: ✅ PASS

---

## 7. Phase 4: Admin Forecast Endpoints

### Test 4-1: GET /api/v1/admin/ai/forecast (認証なし)

**HTTP Status**: 401 Unauthorized  
**結果**: ✅ PASS

---

### Test 4-2: GET /api/v1/admin/ai/forecast (一般ユーザー)

**HTTP Status**: 403 Forbidden  
**結果**: ✅ PASS（一般ユーザーには Admin 権限がない）

---

### Test 4-3: GET /api/v1/admin/ai/forecast (Admin)

**リクエスト**:
```bash
curl -s "http://localhost:5009/api/v1/admin/ai/forecast?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**レスポンス**:
```json
{"items":[],"totalCount":0,"page":1,"pageSize":10,"totalPages":0}
```

**HTTP Status**: 200 OK  
**結果**: ✅ PASS

---

### Test 4-4: GET /api/v1/admin/ai/forecast/{productId} (Admin)

**リクエスト**:
```bash
curl -s "http://localhost:5009/api/v1/admin/ai/forecast/prod-123?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**HTTP Status**: 200 OK  
**結果**: ✅ PASS

---

### Test 4-5: POST /api/v1/admin/ai/forecast/generate (Admin)

**リクエスト**:
```bash
curl -s -X POST "http://localhost:5009/api/v1/admin/ai/forecast/generate" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -d '{"productId":"prod-123","forecastPeriod":"MONTHLY"}'
```

**レスポンス**:
```json
{
  "id": "6a83bc21-0ca8-42ab-abc0-f06c056f84fa",
  "productId": "prod-123",
  "sku": null,
  "forecastDate": "2026-04-06T12:38:49.6128485Z",
  "forecastPeriod": "MONTHLY",
  "predictedDemand": 200,
  "confidenceScore": 0.75,
  "modelVersion": "moving-average-v1",
  "createdAt": "2026-04-06T12:38:49.7043834Z"
}
```

**HTTP Status**: 201 Created  
**結果**: ✅ PASS

---

## 8. Phase 5: Admin Analytics Endpoints

### Test 5-1: GET /api/v1/admin/ai/analytics/search (Admin)

**リクエスト**:
```bash
curl -s "http://localhost:5009/api/v1/admin/ai/analytics/search" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**レスポンス**:
```json
{
  "totalSearches": 4,
  "uniqueUsers": 1,
  "avgResponseTimeMs": 9.25,
  "topQueries": [
    {"query": "スキー", "count": 2},
    {"query": "feedback:search-123", "count": 1},
    {"query": "ski", "count": 1}
  ],
  "searchTypeDistribution": {"KEYWORD": 4}
}
```

**HTTP Status**: 200 OK  
**結果**: ✅ PASS

---

### Test 5-2: GET /api/v1/admin/ai/analytics/recommendations (Admin)

**リクエスト**:
```bash
curl -s "http://localhost:5009/api/v1/admin/ai/analytics/recommendations" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**レスポンス**:
```json
{
  "totalRecommendations": 0,
  "viewedCount": 0,
  "viewRate": 0,
  "typeDistribution": {}
}
```

**HTTP Status**: 200 OK  
**結果**: ✅ PASS

---

### Test 5-3: GET /api/v1/admin/ai/analytics/chat (Admin)

**リクエスト**:
```bash
curl -s "http://localhost:5009/api/v1/admin/ai/analytics/chat" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**レスポンス**:
```json
{
  "totalSessions": 2,
  "totalMessages": 2,
  "avgMessagesPerSession": 1,
  "escalatedSessions": 1,
  "statusDistribution": {"CLOSED": 1, "ESCALATED": 1}
}
```

**HTTP Status**: 200 OK  
**結果**: ✅ PASS

---

## 9. Phase 6: Admin Model Endpoints

### Test 6-1: GET /api/v1/admin/ai/models (Admin)

**リクエスト**:
```bash
curl -s "http://localhost:5009/api/v1/admin/ai/models?page=1&pageSize=10" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**レスポンス**:
```json
{"items":[],"totalCount":0,"page":1,"pageSize":10,"totalPages":0}
```

**HTTP Status**: 200 OK  
**結果**: ✅ PASS

---

### Test 6-2: POST /api/v1/admin/ai/models/{modelName}/train (Admin)

**リクエスト**:
```bash
curl -s -X POST "http://localhost:5009/api/v1/admin/ai/models/recommendation-model/train" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}"
```

**レスポンス**:
```json
{
  "id": "3f089f2f-d491-41ea-92b0-e8a2af00c447",
  "modelName": "recommendation-model",
  "modelVersion": "v20260406123900",
  "status": "PENDING",
  "startedAt": null,
  "completedAt": null,
  "createdAt": "2026-04-06T12:39:00.2133645Z"
}
```

**HTTP Status**: 201 Created  
**結果**: ✅ PASS

---

### Test 6-3: GET /api/v1/admin/ai/models (一般ユーザー)

**HTTP Status**: 403 Forbidden  
**結果**: ✅ PASS（一般ユーザーには Admin 権限がない）

---

## Appendix: 修正事項と問題点

### 適用した修正

| 修正項目 | 修正内容 | 影響範囲 |
|---------|---------|---------|
| row_version デフォルト値 | `user_profiles`, `chat_sessions`, `model_trainings` テーブルにデフォルト値設定 | DB スキーマ |
| AdminOnly ポリシー | `RequireRole("Admin", "ADMIN")` に変更 | Program.cs |
| max_tokens → max_completion_tokens | Semantic Kernel API パラメータ修正 | ChatService.cs |
| temperature 削除 | gpt-5/o1 系モデルでは temperature=1.0 固定のため削除 | ChatService.cs |

### 既知の問題

| 問題 | 原因 | 影響 | 対処方針 |
|------|------|------|---------|
| trending/seasonal が 500 エラー | `recommendations` テーブルへの INSERT 時に `user_profiles` 外部キー制約違反 | 2/33 テスト失敗 | テストデータ（シードデータ）を投入するか、匿名ユーザー向けの処理を実装する |

### 推奨アクション

1. **シードデータの投入**: 開発・テスト環境用のユーザープロファイルサンプルデータを用意する
2. **マイグレーションスクリプトの更新**: `row_version` のデフォルト値を EF Core マイグレーションに含める
3. **エラーハンドリングの改善**: 外部キー制約違反時により適切なエラーメッセージを返す

---

## 認証・認可の動作確認

| 認証レベル | 期待動作 | 実際の動作 | 判定 |
|-----------|---------|-----------|------|
| Anonymous (AllowAnonymous) | 200 OK | 200 OK | ✅ |
| Authenticated (RequireAuthorization) | 401 Unauthorized（未認証時） | 401 Unauthorized | ✅ |
| Authenticated (RequireAuthorization) | 200 OK（認証時） | 200 OK | ✅ |
| Admin Only (認証なし) | 401 Unauthorized | 401 Unauthorized | ✅ |
| Admin Only (一般ユーザー) | 403 Forbidden | 403 Forbidden | ✅ |
| Admin Only (Admin ユーザー) | 200 OK | 200 OK | ✅ |

---

## 結論

AiSupportService の全 33 エンドポイントのうち、**31 エンドポイント (93.9%)** が正常に動作しています。

残り 2 エンドポイント（trending/seasonal）の 500 エラーは、テストデータ不足に起因するもので、サービスの実装上の重大な問題ではありません。

**特記事項**:
- AI チャット機能（Semantic Kernel + Azure OpenAI gpt-5）が正常に動作することを確認
- gpt-5/o1 系モデルの API 仕様変更（max_completion_tokens, temperature=1.0 固定）に対応する修正を適用済み
- 認証・認可（JWT Bearer + AdminOnly ポリシー）が正しく機能していることを確認

---

## 付録: エンドポイント設計一覧

| メソッド | パス | 認証 | 説明 |
|---------|------|------|------|
| GET | /health | 不要 | Liveness チェック |
| GET | /health/ready | 不要 | Readiness チェック |
| GET | /api/v1/ai/search | 不要 | AI 商品検索 |
| GET | /api/v1/ai/search/suggest | 不要 | 検索サジェスト |
| POST | /api/v1/ai/search/feedback | 必須 | 検索フィードバック |
| GET | /api/v1/ai/recommendations/trending | 不要 | トレンド商品 |
| GET | /api/v1/ai/recommendations/seasonal | 不要 | 季節商品 |
| GET | /api/v1/ai/recommendations/similar/{id} | 不要 | 類似商品 |
| GET | /api/v1/ai/recommendations/frequently-bought/{id} | 不要 | よく一緒に購入される商品 |
| GET | /api/v1/ai/recommendations/personalized | 必須 | パーソナライズ推薦 |
| POST | /api/v1/ai/recommendations/feedback | 必須 | 推薦フィードバック |
| POST | /api/v1/ai/chat/sessions | 必須 | セッション作成 |
| GET | /api/v1/ai/chat/sessions | 必須 | セッション一覧 |
| GET | /api/v1/ai/chat/sessions/{id} | 必須 | セッション詳細 |
| GET | /api/v1/ai/chat/sessions/{id}/messages | 必須 | メッセージ一覧 |
| POST | /api/v1/ai/chat/sessions/{id}/messages | 必須 | メッセージ送信（AI応答生成） |
| POST | /api/v1/ai/chat/sessions/{id}/close | 必須 | セッション終了 |
| POST | /api/v1/ai/chat/sessions/{id}/escalate | 必須 | 有人エスカレーション |
| GET | /api/v1/admin/ai/forecast | Admin | 予測一覧 |
| GET | /api/v1/admin/ai/forecast/{productId} | Admin | 商品別予測 |
| POST | /api/v1/admin/ai/forecast/generate | Admin | 予測生成 |
| GET | /api/v1/admin/ai/analytics/search | Admin | 検索分析 |
| GET | /api/v1/admin/ai/analytics/recommendations | Admin | 推薦分析 |
| GET | /api/v1/admin/ai/analytics/chat | Admin | チャット分析 |
| GET | /api/v1/admin/ai/models | Admin | モデル一覧 |
| POST | /api/v1/admin/ai/models/{name}/train | Admin | モデル学習開始 |

---

## Appendix B: trending/seasonal 500 エラーの詳細分析と解決方法（解決済み）

> **ステータス: ✅ 解決済み**  
> 当初 `"system"` というユーザー ID を使用していたが、テックリードの判断により `"anonymous"` に変更。
> EF Core マイグレーションおよびソースコードの修正を完了。

### 1. 問題の概要

#### 発生したエンドポイント
- `GET /api/v1/ai/recommendations/trending`
- `GET /api/v1/ai/recommendations/seasonal`
- `GET /api/v1/ai/recommendations/similar/{id}`
- `GET /api/v1/ai/recommendations/frequently-bought/{id}`

#### 根本原因
`recommendations` テーブルの `user_id` カラムは `user_profiles.user_id` への外部キー制約を持つ。
匿名ユーザー向けレコメンデーション生成時に使用されるユーザー ID が `user_profiles` テーブルに存在しなかったため、INSERT 時に外部キー制約違反が発生していた。

---

### 2. 解決策: `system` → `anonymous` への変更

#### 変更理由（テックリード判断）
- `"system"` は一般的にシステム管理者、バックグラウンドプロセス、特権操作を連想させる
- 実際は**匿名（未ログイン）ユーザー向けの公開機能**であり、用途と名前が一致しない
- `"anonymous"` は用途を正確に表し、コードの可読性・保守性が向上する
- セキュリティ監査時に `system` ユーザーの操作は注目されやすく、誤解を招く可能性がある

#### 変更内容

**1. ソースコード修正** (`Services/RecommendationService.cs`)

```csharp
// 定数を追加（ファイル先頭）
/// <summary>
/// 匿名ユーザー（未ログインユーザー）向けレコメンデーションに使用されるユーザー ID。
/// </summary>
private const string AnonymousUserId = "anonymous";

// 各メソッドで使用
var recommendation = Recommendation.CreateTrending(AnonymousUserId, productIds, 0.9m, "現在人気のトレンド商品");
var recommendation = Recommendation.CreateSeasonal(AnonymousUserId, productIds, 0.8m, "今シーズンのおすすめ商品");
// ... similar, frequently-bought も同様に修正
```

**2. EF Core マイグレーション追加** (`Migrations/20260406130800_AddAnonymousUserProfile.cs`)

```csharp
public partial class AddAnonymousUserProfile : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO user_profiles (
                id, user_id, preferences_json, browsing_history_json,
                purchase_history_json, last_activity_at, created_at, updated_at, row_version
            ) VALUES (
                '00000000-0000-0000-0000-000000000001',
                'anonymous',
                '{"type": "anonymous", "description": "匿名ユーザー向けレコメンデーション用プロファイル"}',
                '[]', '[]', NULL, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, '\x00000000'::bytea
            )
            ON CONFLICT (user_id) DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM user_profiles WHERE user_id = 'anonymous';");
    }
}
```

**3. インターフェースドキュメント更新** (`Services/Interfaces/IRecommendationService.cs`)

```csharp
/// <para>
/// 匿名ユーザー向けレコメンデーション（userId = "anonymous"）へのフィードバックは許可されます。
/// </para>
```

---

### 3. 動作確認結果

```bash
# 匿名ユーザープロファイルの確認
docker exec skishop-postgres psql -U skishop -d aisupportdb -c \
  "SELECT id, user_id FROM user_profiles WHERE user_id = 'anonymous';"

# 結果:
#                   id                  | user_id  
# --------------------------------------+----------
#  00000000-0000-0000-0000-000000000001 | anonymous

# レコメンデーションの確認
docker exec skishop-postgres psql -U skishop -d aisupportdb -c \
  "SELECT id, user_id, type, reason FROM recommendations ORDER BY created_at DESC LIMIT 2;"

# 結果:
#                   id                  |  user_id  |   type   |          reason          
# --------------------------------------+-----------+----------+--------------------------
#  5325a421-4907-46a2-9f0e-ffb8d42790b2 | anonymous | SEASONAL | 今シーズンのおすすめ商品
#  4ebb8e19-5586-4ea4-9a38-56f6e24ec2b7 | anonymous | TRENDING | 現在人気のトレンド商品
```

---

### 4. 本番環境への適用手順

1. **ソースコードの変更をコミット**
   - `Services/RecommendationService.cs`
   - `Services/Interfaces/IRecommendationService.cs`
   - `Migrations/20260406130800_AddAnonymousUserProfile.cs`

2. **Docker イメージを再ビルド**
   ```bash
   docker compose build ai-support-service
   ```

3. **マイグレーションを適用**（本番 DB）
   ```bash
   dotnet ef database update
   ```
   または手動 SQL:
   ```sql
   INSERT INTO user_profiles (id, user_id, preferences_json, ...)
   VALUES ('00000000-0000-0000-0000-000000000001', 'anonymous', ...)
   ON CONFLICT (user_id) DO NOTHING;
   ```

4. **コンテナを再起動**
   ```bash
   docker compose restart ai-support-service
   ```

---

### 5. まとめ

| 項目 | 内容 |
|------|------|
| **原因** | `recommendations.user_id` → `user_profiles.user_id` の FK 制約。匿名レコメンデーション用のユーザープロファイルが存在しなかった |
| **影響範囲** | trending, seasonal, similar, frequently-bought の 4 エンドポイント |
| **解決策** | `"system"` → `"anonymous"` に変更し、EF Core マイグレーションでシードデータを投入 |
| **変更理由** | `system` は特権操作を連想させるが、実際は匿名ユーザー向け機能。可読性・保守性向上のため `anonymous` を採用 |
| **ステータス** | ✅ 解決済み（検証完了） |
