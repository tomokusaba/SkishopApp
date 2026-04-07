# Issue: GET /api/v1/coupons — HTTP 500 エラー調査報告

**起票日**: 2026-04-07  
**重要度**: High  
**影響範囲**: CouponService（バックエンド側）+ API Gateway StatusCodeMiddleware（副次的）  
**ステータス**: 調査完了・修正方法確定

---

## 1. 問題概要

API Gateway 経由で `GET /api/v1/coupons`（認証済みユーザー）にアクセスすると HTTP 500 エラーが返却される。

```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:8080/api/v1/coupons \
  -H "Authorization: Bearer $TOKEN"
# 結果: 500
```

レスポンスボディ:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "内部エラーが発生しました",
  "code": "GW-5004"
}
```

---

## 2. リクエストフロー分析

### 2.1 正常時の期待フロー

```
Client → API Gateway → CouponService → 200 OK + クーポン一覧 → Client
```

### 2.2 実際のフロー（障害チェーン）

```
Client
  ↓ GET /api/v1/coupons (Authorization: Bearer <token>)
API Gateway
  ↓ YARP ルートマッチ: coupons-public-route (Order: 1, AuthorizationPolicy: Anonymous)
  ↓ プロキシ転送: http://coupon-service:5006/api/v1/coupons
CouponService
  ↓ JWT 認証成功（トークン有効）
  ↓ ルートマッチング: GET /api/v1/coupons にマッチするハンドラなし
  ↓ ★ HTTP 404 返却（Content-Length: 0, Content-Type: null）
API Gateway（YARP 受信）
  ↓ YARP が 404 レスポンスヘッダー（Content-Length: 0）をクライアント側レスポンスにコピー
  ↓ StatusCodeMiddleware が status=404 を検出
  ↓ ★ StatusCodeMiddleware が Problem Details JSON (268 bytes) を書き込み試行
  ↓ ★ Kestrel が Content-Length: 0 と 268 bytes の不整合を検出
  ↓ ★ InvalidOperationException: Response Content-Length mismatch: too many bytes written (268 of 0)
  ↓ グローバル例外ハンドラーがキャッチ → HTTP 500 (GW-5004) を返却
Client ← HTTP 500
```

### 2.3 ログ証跡

#### CouponService ログ（バックエンド）

```json
{
  "@t": "2026-04-07T07:14:20.854",
  "@mt": "HTTP {RequestMethod} {RequestPath} responded {StatusCode}",
  "RequestMethod": "GET",
  "RequestPath": "/api/v1/coupons",
  "StatusCode": 404
}
{
  "@mt": "Request reached the end of the middleware pipeline without being handled by application code",
  "Path": "/api/v1/coupons",
  "StatusCode": 404
}
```

#### API Gateway ログ（ゲートウェイ）

```json
// Step 1: YARP 転送
{"@mt": "Proxying to {targetUrl}", "targetUrl": "http://coupon-service:5006/api/v1/coupons"}

// Step 2: バックエンドから 404 受信
{"@mt": "Received HTTP/{version} response {statusCode}.", "statusCode": 404}

// Step 3: StatusCodeMiddleware が 404 を検出
{"@mt": "ステータスコード応答: Code={GwCode}, Status={StatusCode}, Path={Path}",
 "GwCode": "GW-4004", "StatusCode": 404, "Path": "/api/v1/coupons"}

// Step 4: Content-Length 不整合で例外発生
{"@x": "System.InvalidOperationException: Response Content-Length mismatch: too many bytes written (268 of 0).
   at Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.HttpProtocol.VerifyAndUpdateWrite(Int32 count)
   at System.Text.Json.Utf8JsonWriter.Flush()"}

// Step 5: 最終ステータス 500
{"StatusCode": 500}
```

---

## 3. 根本原因分析

### 3.1 主原因（Critical）: CouponService に `GET /api/v1/coupons` ハンドラが存在しない

**CouponEndpoints.cs** の現在のルート定義:

```csharp
var group = app.MapGroup("/api/v1/coupons")
    .WithTags("Coupons")
    .RequireRateLimiting("coupon-api");

group.MapGet("/available", GetAvailableCoupons);  // GET /api/v1/coupons/available
group.MapGet("/mine", GetMyCoupons);              // GET /api/v1/coupons/mine
group.MapPost("/{code}/acquire", AcquireCoupon);  // POST /api/v1/coupons/{code}/acquire
group.MapPost("/validate", ValidateCoupon);       // POST /api/v1/coupons/validate
```

**`GET /api/v1/coupons`（ルートパス `/`）にはハンドラが登録されていない。**

一方、API Gateway は `GET /api/v1/coupons` を CouponService に転送するルートを持つ:

```json
"coupons-public-route": {
  "ClusterId": "coupons-cluster",
  "AuthorizationPolicy": "Anonymous",
  "RateLimiterPolicy": "anonymous-api",
  "Match": {
    "Path": "/api/v1/coupons",
    "Methods": ["GET"]
  },
  "Order": 1
}
```

→ Gateway はルーティング成功するが、バックエンドにハンドラが存在しないため 404 となる。

### 3.2 副次原因（Medium）: StatusCodeMiddleware の Content-Length 不整合

StatusCodeMiddleware がバックエンドから受信した `Content-Length: 0` のレスポンスに対して Problem Details JSON（268 bytes）を書き込もうとする。YARP がすでに `Content-Length: 0` ヘッダーをコピー済みのため、Kestrel が不整合を検出して `InvalidOperationException` を送出する。

この副次バグは、主原因を修正すれば発生しなくなるが、他のバックエンドエラー（404 応答）でも同様の問題が起きる可能性がある。

---

## 4. CouponService エンドポイント設計の確認

### 4.1 既存エンドポイント一覧（CouponService-verification-report.md 確認済み）

| グループ | メソッド | パス | 用途 | 認可 |
|---------|---------|------|------|------|
| **ユーザー向け** | GET | `/api/v1/coupons/available` | 利用可能クーポン一覧 | UserOrAdmin |
| | GET | `/api/v1/coupons/mine` | 自分のクーポン一覧 | 認証必須 |
| | POST | `/api/v1/coupons/{code}/acquire` | クーポン取得 | 認証必須 |
| | POST | `/api/v1/coupons/validate` | クーポン検証 | 認証必須 |
| **管理者向け** | GET | `/api/v1/admin/coupons/` | 全クーポン一覧 | AdminOnly |
| | POST | `/api/v1/admin/coupons/` | クーポン作成 | AdminOnly |
| | GET | `/api/v1/admin/coupons/{id}` | クーポン詳細 | AdminOnly |
| | PUT | `/api/v1/admin/coupons/{id}` | クーポン更新 | AdminOnly |
| | DELETE | `/api/v1/admin/coupons/{id}` | クーポン無効化 | AdminOnly |
| | GET | `/api/v1/admin/coupons/{id}/usages` | 利用履歴 | AdminOnly |
| | GET | `/api/v1/admin/coupons/analytics` | 分析データ | AdminOnly |
| **内部サービス** | POST | `/api/v1/internal/coupons/calculate` | 割引計算 | InternalServiceOnly |
| | POST | `/api/v1/internal/coupons/redeem` | クーポン利用 | InternalServiceOnly |
| | POST | `/api/v1/internal/coupons/release` | クーポンリリース | InternalServiceOnly |

### 4.2 欠落エンドポイント

| メソッド | パス | 状態 |
|---------|------|------|
| **GET** | **`/api/v1/coupons`** | **❌ 未定義（今回の問題）** |

### 4.3 API Gateway ルーティングとの不整合

| Gateway ルート | Gateway パス | Backend ハンドラ | 状態 |
|---------------|-------------|-----------------|------|
| `coupons-public-route` | `GET /api/v1/coupons` | **なし** | ❌ **不整合** |
| `coupons-apply-route` | `POST /api/v1/coupons/apply` | **なし** ※1 | ❌ **不整合** |
| `coupons-validate-route` | `POST /api/v1/coupons/validate` | `ValidateCoupon` | ✅ 一致 |
| `coupons-route` | `/api/v1/coupons/{**catch-all}` | 各サブパス | ✅ 一致 |

※1: `coupons-apply-route` は `POST /api/v1/coupons/apply` にルーティングするが、CouponService には `/apply` エンドポイントが存在しない。クーポン取得は `POST /api/v1/coupons/{code}/acquire` で実装されている。**`coupons-apply-route`（Order: 2）は catch-all `coupons-route`（Order: 10）より優先されるため、catch-all ではカバーされない。** `POST /api/v1/coupons/apply` へのリクエストは CouponService で 404 となり、StatusCodeMiddleware の Content-Length 不整合バグ（Bug 2）と同じ経路で 500 エラーとなる。ただし、設計書（`coupon-service-design.md`）にも `/apply` エンドポイントは定義されておらず、Gateway ルートの誤設定と判断する。この問題は Bug 3 として本レポートのスコープに含める。

### 4.4 設計書との整合確認

`design-docs/coupon-service-design.md` を確認した結果、設計書に定義されているユーザー向けエンドポイントは以下の通り:

| メソッド | パス | ロール | 用途 |
|---------|------|--------|------|
| GET | `/api/v1/coupons/available` | USER | 利用可能なクーポン一覧 |
| GET | `/api/v1/coupons/mine` | USER | 自分のクーポン一覧 |
| POST | `/api/v1/coupons/{code}/acquire` | USER | クーポン取得 |
| POST | `/api/v1/coupons/validate` | USER | クーポン検証 |

**`GET /api/v1/coupons`（ルートパス）は設計書に元々存在しない。** API Gateway の `coupons-public-route` は設計書にない独自ルートであり、対応するバックエンドハンドラが作成されなかった設計時の不整合。同様に、`POST /api/v1/coupons/apply` も設計書には存在しない。

---

## 5. 修正方法

### 5.1 Bug 1（主原因）: CouponService に `GET /` ハンドラを追加

**対象ファイル**: `Services/CouponService/Endpoints/CouponEndpoints.cs`

**修正内容**: `GET /api/v1/coupons` ルートパスへのハンドラを追加し、利用可能なクーポン一覧を返す。既存の `GetAvailableCoupons` と同じロジックを委譲する。

**設計判断**:

| 案 | 内容 | 評価 |
|----|------|------|
| **A: ルートパスに新ハンドラ追加（推奨）** | `GET /` を追加し、`GetAvailableCoupons` と同じ Service メソッドを呼び出す | ✅ REST 設計に自然、Gateway ルートと完全一致 |
| B: Gateway のパス変換 | Gateway に YARP Transform を追加して `/api/v1/coupons` → `/api/v1/coupons/available` に書き換え | △ Gateway に CouponService 固有の知識を持たせることになる |
| C: Gateway ルートを削除 | `coupons-public-route` を削除し、catch-all でカバー | × 未認証の場合に catch-all ルート（default 認可）で 401 になる |

**→ 案 A を採用**

#### 修正コード

```csharp
// CouponEndpoints.cs — MapCouponEndpoints メソッド内に追加

// GET /api/v1/coupons — 利用可能クーポン一覧
group.MapGet("/", GetCoupons)
    .WithName("GetCoupons")
    .Produces<PagedResponse<CouponSummaryResponse>>()
    .RequireAuthorization("UserOrAdmin");
```

ハンドラメソッド:

```csharp
private static async Task<IResult> GetCoupons(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    ICouponService couponService, CancellationToken ct)
    => Results.Ok(await couponService.GetAvailableCouponsAsync(
        page > 0 ? page : 1, pageSize > 0 ? Math.Min(pageSize, 100) : 20, ct));
```

> **⚠️ 重要: `[FromQuery] int page = 1` のデフォルト値は必須。** ASP.NET Core Minimal API では、デフォルト値のない `[FromQuery] int page` パラメータはクエリパラメータが省略された場合に**ハンドラに到達する前に 400 Bad Request を返す**（`Required parameter "int page" was not provided from query string`）。ハンドラ内の `page > 0 ? page : 1` というデフォルトロジックは、パラメータが 0 や負数で渡された場合のフォールバックであり、パラメータ省略時には機能しない。既存の `GetAvailableCoupons` ハンドラにも同様のバグがある（CouponService-verification-report.md Test 3-1 注記参照）。

**注意事項**:

1. **ルーティング安全性**: ASP.NET Core はリテラルセグメント（`/available`, `/mine`）をパラメータセグメント（`/{code}`）より優先する。`GET /` は最も基本的なルートであり、`/available` や `/mine` とは競合しない。

2. **認可ポリシー**: 
   - API Gateway の `coupons-public-route` は `AuthorizationPolicy: "Anonymous"` だが、これは**Gateway レベルでの認可チェックをスキップする**という意味。
   - CouponService 側では `RequireAuthorization("UserOrAdmin")` を設定する。Gateway が JWT トークンをそのまま転送するため、CouponService 側でユーザー認証が行われる。
   - 「公開」の意味: Gateway が認可チェック不要（トークンはオプショナル）で転送するが、CouponService 側は認証を要求する。
   - CouponService の `UserOrAdmin` ポリシーは `"User", "Admin", "USER", "ADMIN"` の 4 つのロール名を受け付ける（Program.cs L88）。

3. **ページネーションのデフォルト値**: 
   - **パラメータ省略時**: `page = 1`, `pageSize = 20` がデフォルト値として適用される（ASP.NET Core の Minimal API パラメータバインディング）。
   - **パラメータが 0 や負数の場合**: ハンドラ内のフォールバック（`page > 0 ? page : 1`）で補正される。
   - **参考**: AdminCouponEndpoints.cs の `GetAllCoupons` は `[AsParameters] CouponQueryParams` パターンでデフォルト値を record 定義側に持たせている。新規ハンドラでは既存の `GetAvailableCoupons` と同一パターン（パラメータ直接指定）を維持し、デフォルト値を追加する方針とする。

4. **既存の using 文**: `CouponEndpoints.cs` には `ICouponService`, `CouponSummaryResponse`, `PagedResponse`, `FromQuery`, `CancellationToken` 等の必要な import がすべて存在するため、追加の using 文は不要。

### 5.2 Bug 2（副次的）: StatusCodeMiddleware の Content-Length 不整合修正

**対象ファイル**: `Services/ApiGateway/Infrastructure/Middleware/StatusCodeMiddleware.cs`

**修正内容**: Problem Details を書き込む前に、YARP がコピーした `Content-Length` ヘッダーをクリアする。

**修正コード**:

```csharp
if (gwCode is null) return;

logger.LogWarning(
    "ステータスコード応答: Code={GwCode}, Status={StatusCode}, Path={Path}",
    gwCode, statusCode, context.Request.Path);

// YARP がバックエンドの Content-Length をコピーしている場合、
// Problem Details 書き込み前にクリアする（Content-Length 不整合防止）
context.Response.Headers.Remove("Content-Length");
context.Response.ContentType = "application/problem+json";
await context.Response.WriteAsJsonAsync(new { /* ... */ }, context.RequestAborted);
```

**修正箇所の詳細**:

```csharp
// 既存コード（L44 付近）
if (gwCode is null) return;

logger.LogWarning(
    "ステータスコード応答: Code={GwCode}, Status={StatusCode}, Path={Path}",
    gwCode, statusCode, context.Request.Path);

context.Response.ContentType = "application/problem+json";  // ← この前に Content-Length クリアを追加

// 修正後
context.Response.Headers.Remove("Content-Length");  // ★ 追加: YARP がコピーした Content-Length をクリア
context.Response.ContentType = "application/problem+json";
```

### 5.3 Bug 3（関連問題）: 孤立した `coupons-apply-route` の修正

**対象ファイル**: `Services/ApiGateway/appsettings.json`

**問題**: Gateway に `POST /api/v1/coupons/apply`（Order: 2）のルートが定義されているが、CouponService には `/apply` エンドポイントが存在しない。設計書（`coupon-service-design.md`）にも `/apply` エンドポイントは定義されていない。

**影響**: `POST /api/v1/coupons/apply` にリクエストが送信されると、CouponService が 404 を返し、Bug 2 と同じ経路で StatusCodeMiddleware の Content-Length 不整合が発生する。Bug 2 の修正（Content-Length クリア）により 500 は防止されるが、GW-4004 エラーとなる。

**修正方針**:

| 案 | 内容 | 評価 |
|----|------|------|
| **A: 孤立ルートを削除（推奨）** | `coupons-apply-route` を `appsettings.json` から削除 | ✅ 設計書に存在しないルートを除去。`POST /api/v1/coupons/apply` は catch-all `coupons-route` でカバーされる（ただし CouponService 側で 404 となるが、本来の動作） |
| B: エンドポイント名を修正 | パスを `/api/v1/coupons/{code}/acquire` に変更 | △ Gateway 側で CouponService の実装詳細を知る必要がある。catch-all で既にカバーされているため不要 |

**→ 案 A を採用（`coupons-apply-route` を削除）**

**修正コード**:

```json
// appsettings.json から以下のルート定義を削除
"coupons-apply-route": {
  "ClusterId": "coupons-cluster",
  "AuthorizationPolicy": "default",
  "RateLimiterPolicy": "user-based",
  "Match": {
    "Path": "/api/v1/coupons/apply",
    "Methods": ["POST"]
  },
  "Order": 2
}
```

**理由**: `POST /api/v1/coupons/{code}/acquire` は catch-all `coupons-route`（`/api/v1/coupons/{**catch-all}`, Order: 10, AuthorizationPolicy: default）で正しくルーティングされる。専用ルートは不要であり、存在しないエンドポイントへのルートは混乱を招く。

---

## 6. 検証計画

### 6.1 修正後の検証テスト

| # | テスト内容 | 期待結果 |
|---|----------|---------|
| 1 | `GET /api/v1/coupons` （未認証） | HTTP 401（CouponService の FallbackPolicy） |
| 2 | `GET /api/v1/coupons` （一般ユーザー、ページネーションなし） | HTTP 200 + `PagedResponse<CouponSummaryResponse>` （デフォルト page=1, pageSize=20） |
| 3 | `GET /api/v1/coupons?page=1&pageSize=10` （一般ユーザー） | HTTP 200 + `pageSize: 10` |
| 4 | `GET /api/v1/coupons` （Admin ユーザー） | HTTP 200 + `PagedResponse<CouponSummaryResponse>` |
| 5 | 既存 `GET /api/v1/coupons/available` が引き続き動作すること | HTTP 200（回帰テスト） |
| 6 | 既存 `GET /api/v1/coupons/mine` が引き続き動作すること | HTTP 200（回帰テスト） |

### 6.2 StatusCodeMiddleware の検証

| # | テスト内容 | 期待結果 |
|---|----------|---------|
| 7 | バックエンドが Content-Length: 0 の 404 を返すケース | HTTP 404 + GW-4004 Problem Details（Content-Length 不整合なし） |

---

## 7. リスク評価

| # | リスク | 影響度 | 対策 |
|---|--------|--------|------|
| 1 | `GET /` と `GET /available` の共存 | 低 | 両者は異なるリテラルパスのため競合しない。`/` はルートパス、`/available` は名前付きサブパス |
| 2 | `GET /` と `GET /{code}/acquire` の競合 | なし | `GET /` と `POST /{code}/acquire` は HTTP メソッドが異なる（GET vs POST） |
| 3 | ページネーションパラメータ省略時の挙動 | **なし** | **`[FromQuery] int page = 1` のデフォルト値により、パラメータ省略時は ASP.NET Core が自動で 1 を使用する**。さらにハンドラ内のフォールバック（`page > 0 ? page : 1`）が 0 や負数をガードする |
| 4 | StatusCodeMiddleware 修正の副作用 | 低 | `Content-Length` ヘッダーの除去は `HasStarted == FALSE` の場合のみ実行される（L31 のガード済み）。`WriteAsJsonAsync` 後に ASP.NET Core が自動で正しい Content-Length を再設定する |
| 5 | 認可ポリシーの不整合 | 低 | Gateway は Anonymous（スルー）、CouponService は UserOrAdmin。Gateway 経由でトークンが転送されるため正常に動作する |
| 6 | `coupons-apply-route` 削除の影響 | 低 | catch-all `coupons-route`（Order: 10）が `POST /api/v1/coupons/*` を包括的にカバーする。AuthorizationPolicy: default（認証必須）は同一のため、認可レベルは変わらない |

---

## 8. 影響範囲

| 対象 | 影響 |
|------|------|
| `Services/CouponService/Endpoints/CouponEndpoints.cs` | `GET /` ハンドラ + メソッド追加 |
| `Services/ApiGateway/Infrastructure/Middleware/StatusCodeMiddleware.cs` | `Content-Length` クリア行を追加 |
| `Services/ApiGateway/appsettings.json` | `coupons-apply-route` の削除 |
| API Gateway 検証レポート | F-1 の結果を更新（✅ PASS） |

---

## 9. 修正手順チェックリスト

- [ ] `CouponEndpoints.cs` に `GET /` ハンドラを追加（`[FromQuery] int page = 1` デフォルト値必須）
- [ ] `StatusCodeMiddleware.cs` に `Content-Length` クリア処理を追加
- [ ] `appsettings.json` から `coupons-apply-route` を削除
- [ ] `dotnet build` でビルド成功を確認（CouponService + ApiGateway）
- [ ] Docker イメージを再ビルド（CouponService + ApiGateway）
- [ ] コンテナを再起動
- [ ] 検証テスト 1〜7 を全件実行
- [ ] 回帰テスト（既存エンドポイントの動作確認）
- [ ] `ApiGateway-verification-report.md` を更新（45/45 PASS）
- [ ] 変更をコミット
