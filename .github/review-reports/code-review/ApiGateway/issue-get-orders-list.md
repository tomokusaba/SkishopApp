# Issue: GET /api/v1/orders — 注文一覧取得エンドポイント未実装

## 1. 問題の概要

| 項目 | 内容 |
|------|------|
| **発生箇所** | API Gateway 経由: `GET http://localhost:8080/api/v1/orders` |
| **期待動作** | 認証済みユーザーの注文一覧をページネーション付きで返却（HTTP 200） |
| **実際の動作** | HTTP 405（API Gateway の StatusCodeMiddleware が `GW-4005` として Problem Details 形式で返却） |
| **根本原因** | SalesManagementService に `GET /api/v1/orders`（ルートコレクション）のハンドラが存在しない |
| **影響** | 一般ユーザーが「自分の注文一覧」を取得する標準的な REST エンドポイントが利用不可 |
| **重要度** | High |

---

## 2. 詳細調査結果

### 2.1 リクエストフロー分析

```
クライアント
  │
  │  GET /api/v1/orders
  │  Authorization: Bearer <JWT>
  ▼
API Gateway (YARP)
  │  ルート: orders-route
  │  パターン: /api/v1/orders/{**catch-all}  (catch-all = "")
  │  クラスタ: sales-cluster（タイムアウト: 4 秒）
  │  転送先: http://sales-management-service:5004/api/v1/orders
  ▼
SalesManagementService
  │  MapGroup: /api/v1/orders
  │  GET / → ❌ ハンドラ未定義（POST / のみ存在）
  │  ASP.NET Core → HTTP 405 Method Not Allowed
  ▼
API Gateway
  │  バックエンドから 405 受信
  │  StatusCodeMiddleware が 405 を検知
  │  → エラーコード GW-4005 で Problem Details 形式にラップ
  │  → HTTP 405 をそのままクライアントに返却
  ▼
クライアント: HTTP 405 + Problem Details JSON
```

> **ステータスコードの詳細**: API Gateway の `StatusCodeMiddleware`（`Infrastructure/Middleware/StatusCodeMiddleware.cs` L37）は  
> 405 を `GW-4005`（"指定された HTTP メソッドは許可されていません"）として Problem Details 形式で返却する。  
> ステータスコード自体は 405 のまま保持され、500 への変換は行われない。  
> なお、`CircuitBreakerMiddleware` は 405 を「成功」として扱い、サーキットブレーカーの状態には影響しない。

### 2.2 SalesManagementService のエンドポイント構成（現状）

`Services/SalesManagementService/Endpoints/OrderEndpoints.cs`（L12-30）:

```csharp
var group = app.MapGroup("/api/v1/orders")
    .WithTags("Orders")
    .RequireAuthorization();

group.MapGet("/{orderId}", GetOrderById);              // GET /api/v1/orders/{orderId}
group.MapGet("/number/{orderNumber}", GetOrderByNumber); // GET /api/v1/orders/number/{orderNumber}
group.MapGet("/customer/{customerId}", GetCustomerOrders); // GET /api/v1/orders/customer/{customerId}
group.MapGet("/search", SearchOrders);                  // GET /api/v1/orders/search（AdminOnly）
group.MapPost("/", CreateOrder);                        // POST /api/v1/orders
group.MapPut("/{orderId}/status", UpdateOrderStatus);   // PUT /api/v1/orders/{orderId}/status（AdminOnly）
group.MapPost("/{orderId}/cancel", CancelOrder);        // POST /api/v1/orders/{orderId}/cancel
```

**注目点**: `group.MapGet("/", ...)` が**存在しない**。`POST /` は定義されているが、`GET /` は未定義。

### 2.3 設計ドキュメントとの整合性確認

`design-docs/sales-management-design.md`（L585-595）の API 仕様テーブル:

| メソッド | パス | 説明 |
|---------|-----|------|
| GET | /api/v1/orders/{orderId} | 注文詳細取得 |
| GET | /api/v1/orders/number/{orderNumber} | 注文番号による取得 |
| GET | /api/v1/orders/customer/{customerId} | 顧客注文履歴取得 |
| POST | /api/v1/orders | 注文作成 |
| PUT | /api/v1/orders/{orderId}/status | 注文ステータス更新 |
| POST | /api/v1/orders/{orderId}/cancel | 注文キャンセル |
| GET | /api/v1/orders/search | 注文検索（Admin） |

**結論**: 設計ドキュメントにも `GET /api/v1/orders`（ルートコレクション）は定義されていない。

### 2.4 既存の代替エンドポイント

| エンドポイント | 用途 | 認可 | 問題点 |
|-------------|------|------|--------|
| `GET /customer/{customerId}` | 特定顧客の注文一覧 | 認証済み + IDOR 保護 | ユーザーが自分の `customerId` を知っている必要がある |
| `GET /search` | 注文検索 | AdminOnly | 一般ユーザーは利用不可 |

### 2.5 IOrderService の既存メソッド

`Services/SalesManagementService/Services/Interfaces/IOrderService.cs`:

```csharp
Task<PaginatedResult<OrderDto>> GetByCustomerIdAsync(
    string customerId, int page, int pageSize, CancellationToken ct = default);

Task<PaginatedResult<OrderDto>> SearchAsync(
    string? customerId, string? status, string? paymentStatus,
    int page, int pageSize, CancellationToken ct = default);
```

**重要**: `GetByCustomerIdAsync` は既に実装済み。JWT の `ClaimTypes.NameIdentifier` から `customerId` を取得し、このメソッドを呼び出すだけで「自分の注文一覧」を返却できる。

---

## 3. 原因分析

### 3.1 直接原因

`OrderEndpoints.cs` の `MapOrderEndpoints()` メソッドに `group.MapGet("/", ...)` が定義されていないため、`GET /api/v1/orders` に対して ASP.NET Core が HTTP 405 Method Not Allowed を返却する。

### 3.2 根本原因

設計段階で「認証済みユーザーが自分の注文一覧を取得する」ための標準的な REST エンドポイント（`GET /api/v1/orders`）が考慮されていなかった。注文一覧取得は `GET /customer/{customerId}` に限定されており、フロントエンドがユーザー ID を別途取得してからアクセスする必要がある設計になっている。

### 3.3 API 設計上の課題

一般的な REST API 設計では、認証済みユーザーが `GET /orders` を呼び出すと自分の注文が返却されるのが標準的なパターン:

```
GET /api/v1/orders           → 自分の注文一覧（JWT から userId を抽出）
GET /api/v1/orders/{id}      → 特定の注文詳細
GET /api/v1/orders/search    → 管理者用検索
```

現在の設計では、一般ユーザーが自分の注文を取得するには:
1. 先に `GET /api/v1/users/me` で自分の ID を取得
2. その ID で `GET /api/v1/orders/customer/{myId}` を呼ぶ

この 2 段階アクセスはフロントエンドに不必要な負荷をかけ、API の使い勝手を損なう。

---

## 4. 修正方法

### 4.1 修正対象ファイル

| ファイル | 修正内容 |
|---------|---------|
| `Services/SalesManagementService/Endpoints/OrderEndpoints.cs` | `GET /` エンドポイント追加 |

### 4.2 修正内容の詳細

#### OrderEndpoints.cs — `GET /` ハンドラの追加

**修正箇所**: `MapOrderEndpoints()` メソッド内（L19 の前に追加）

```csharp
// 追加する行（L18-19 の間に挿入）
group.MapGet("/", GetMyOrders).WithName("GetMyOrders");
```

**追加するハンドラメソッド**:

```csharp
/// <summary>
/// GET /api/v1/orders — 認証済みユーザーの注文一覧を取得
/// JWT の ClaimTypes.NameIdentifier から customerId を自動抽出し、
/// ページネーション付きで注文一覧を返却する。
/// Admin ロールの場合は全注文を検索可能。
/// </summary>
private static async Task<IResult> GetMyOrders(
    [AsParameters] PaginationParams pagination,
    ClaimsPrincipal user,
    IOrderService orderService,
    CancellationToken ct)
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedException();

    // Admin ユーザーの場合はフィルタなし検索（全注文）
    if (user.IsInRole("Admin"))
    {
        var allOrders = await orderService.SearchAsync(
            null, null, null,
            pagination.Page, pagination.PageSize, ct);
        return Results.Ok(allOrders);
    }

    // 一般ユーザーは自分の注文のみ
    var result = await orderService.GetByCustomerIdAsync(
        userId, pagination.Page, pagination.PageSize, ct);
    return Results.Ok(result);
}
```

> **ロール名について**: SalesManagementService 内のロールチェックは全て `"Admin"`（PascalCase）で統一されている。  
> `Program.cs` L152: `options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));`  
> `OrderEndpoints.cs` L55, L69: `user.IsInRole("Admin")`  
> 新規コードも既存の規約に合わせ `"Admin"` のみで判定する。

### 4.3 修正の設計根拠

| 設計判断 | 理由 |
|---------|------|
| JWT から自動的に userId を抽出 | フロントエンドが別途 userId を取得する必要がなくなり、API の使い勝手が向上 |
| Admin は全注文を返却 | 管理者が `GET /orders` で全体概要を確認できる。詳細フィルタは既存の `/search` を使用 |
| 一般ユーザーは自分の注文のみ | IDOR 防止。他ユーザーの注文にアクセスさせない |
| `PaginationParams` を使用 | 既存の DTO を再利用。`Page`（デフォルト 1）、`PageSize`（デフォルト 20、最大 100） |
| 新規 Service メソッド不要 | 既存の `GetByCustomerIdAsync` と `SearchAsync` を組み合わせて実現 |

### 4.4 修正後のエンドポイント一覧

```
GET  /api/v1/orders                     ← 新規追加（マイオーダー / Admin: 全注文）
GET  /api/v1/orders/{orderId}           （既存・変更なし）
GET  /api/v1/orders/number/{orderNumber} （既存・変更なし）
GET  /api/v1/orders/customer/{customerId}（既存・変更なし）
GET  /api/v1/orders/search              （既存・変更なし、AdminOnly）
POST /api/v1/orders                     （既存・変更なし）
PUT  /api/v1/orders/{orderId}/status    （既存・変更なし、AdminOnly）
POST /api/v1/orders/{orderId}/cancel    （既存・変更なし）
```

### 4.5 修正後のコード（完全版）

`OrderEndpoints.cs` の修正後の `MapOrderEndpoints()`:

```csharp
public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
{
    var group = app.MapGroup("/api/v1/orders")
        .WithTags("Orders")
        .RequireAuthorization()
        ;

    group.MapGet("/", GetMyOrders).WithName("GetMyOrders");  // ← 新規追加
    group.MapGet("/{orderId}", GetOrderById).WithName("GetOrderById");
    group.MapGet("/number/{orderNumber}", GetOrderByNumber).WithName("GetOrderByNumber");
    group.MapGet("/customer/{customerId}", GetCustomerOrders).WithName("GetCustomerOrders");
    group.MapGet("/search", SearchOrders)
        .RequireAuthorization("AdminOnly")
        .WithName("SearchOrders");
    group.MapPost("/", CreateOrder).RequireRateLimiting("order-create").WithName("CreateOrder");
    group.MapPut("/{orderId}/status", UpdateOrderStatus)
        .RequireAuthorization("AdminOnly")
        .WithName("UpdateOrderStatus");
    group.MapPost("/{orderId}/cancel", CancelOrder).WithName("CancelOrder");
}
```

新規ハンドラ `GetMyOrders`:

```csharp
private static async Task<IResult> GetMyOrders(
    [AsParameters] PaginationParams pagination,
    ClaimsPrincipal user,
    IOrderService orderService,
    CancellationToken ct)
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedException();

    if (user.IsInRole("Admin"))
    {
        var allOrders = await orderService.SearchAsync(
            null, null, null,
            pagination.Page, pagination.PageSize, ct);
        return Results.Ok(allOrders);
    }

    var result = await orderService.GetByCustomerIdAsync(
        userId, pagination.Page, pagination.PageSize, ct);
    return Results.Ok(result);
}
```

---

## 5. 検証方法

### 5.1 ビルド確認

```bash
cd Services/SalesManagementService
dotnet build
```

### 5.2 Docker イメージの再構築

```bash
docker compose build sales-management-service
docker compose up -d sales-management-service
```

### 5.3 動作テスト

#### テスト 1: 未認証アクセス（401 期待）

```bash
curl -s -w "\nHTTP: %{http_code}" http://localhost:8080/api/v1/orders
# 期待: HTTP 401
```

#### テスト 2: 一般ユーザーでの注文一覧取得（200 期待）

```bash
# ログイン
TOKEN=$(curl -s -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"testuser@skishop.example.com","password":"SkiShop2026!"}' \
  | jq -r '.accessToken')

# 注文一覧取得
curl -s -w "\nHTTP: %{http_code}" http://localhost:8080/api/v1/orders \
  -H "Authorization: Bearer $TOKEN"
# 期待: HTTP 200 + PaginatedResult<OrderDto>
```

#### テスト 3: ページネーション付き

```bash
curl -s -w "\nHTTP: %{http_code}" \
  "http://localhost:8080/api/v1/orders?page=1&pageSize=10" \
  -H "Authorization: Bearer $TOKEN"
# 期待: HTTP 200 + PaginatedResult<OrderDto>（page=1, pageSize=10）
```

#### テスト 4: 管理者ユーザーでの全注文取得（200 期待）

```bash
# 管理者ログイン
ADMIN_TOKEN=$(curl -s -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@skishop.example.com","password":"AdminSkiShop2026!"}' \
  | jq -r '.accessToken')

# 全注文一覧取得
curl -s -w "\nHTTP: %{http_code}" http://localhost:8080/api/v1/orders \
  -H "Authorization: Bearer $ADMIN_TOKEN"
# 期待: HTTP 200 + PaginatedResult<OrderDto>（全ユーザーの注文）
```

### 5.4 期待レスポンス形式

```json
{
  "items": [],
  "totalCount": 0,
  "page": 1,
  "pageSize": 20,
  "totalPages": 0,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

---

## 6. リスク評価

| リスク | 影響度 | 対策 |
|--------|--------|------|
| 既存エンドポイントとの競合 | 低 | `GET /` は新規追加であり、既存の `GET /{orderId}` 等と URL パターンが競合しない。ASP.NET Core のルーティングではリテラルセグメント `"/"` がパラメータセグメント `"/{orderId}"` より優先されるため、`GET /api/v1/orders` が `orderId=""` として誤マッチすることはない |
| `/search` と `/{orderId}` の誤マッチ | なし | ASP.NET Core のルーティングではリテラルセグメント（`/search`）がパラメータセグメント（`/{orderId}`）より高い優先度でマッチするため、`GET /api/v1/orders/search` が `orderId="search"` として解釈されることはない（既存実装で既に動作確認済み） |
| IDOR（他ユーザーの注文漏洩） | 低 | JWT から userId を抽出し、`GetByCustomerIdAsync` が customerId でフィルタするため安全 |
| パフォーマンス（大量注文） | 低 | `PaginationParams` でページネーション済み（デフォルト 20 件、最大 100 件）。`@Range(1, 100)` バリデーション付き |
| Admin の全注文取得による負荷 | 低 | 同じく `SearchAsync` がページネーション対応済み |
| 注文データ未存在時 | なし | `GetByCustomerIdAsync` / `SearchAsync` は注文が 0 件でも `PaginatedResult` を返却する（`Items: []`, `TotalCount: 0`）。null や例外は発生しない |
| using 不足によるコンパイルエラー | なし | `OrderEndpoints.cs` は既に `System.Security.Claims`、`SalesManagementService.DTOs.Requests`（PaginationParams）、`SalesManagementService.Infrastructure.Exceptions`（UnauthorizedException）を import 済み。追加 import は不要 |

---

## 7. 影響範囲

| コンポーネント | 影響 |
|-------------|------|
| `OrderEndpoints.cs` | エンドポイント追加（1 メソッド追加 + 1 行のルート登録）。既存の using で全て対応可能（追加 import 不要） |
| `IOrderService.cs` | **変更不要**（既存メソッド `GetByCustomerIdAsync`, `SearchAsync` を再利用） |
| `OrderService.cs` | **変更不要**（既存実装を再利用。空リスト時も `PaginatedResult` を正常返却） |
| `IOrderRepository.cs` | **変更不要** |
| `OrderRepository.cs` | **変更不要** |
| `API Gateway appsettings.json` | **変更不要**（`/api/v1/orders/{**catch-all}` が空の catch-all も受け付ける） |
| テスト | `OrderEndpoints` の統合テストに 1 テストケース追加が必要 |

### 7.1 既存 using の確認（OrderEndpoints.cs L1-6）

```csharp
using System.Security.Claims;                          // ✅ ClaimsPrincipal, ClaimTypes
using FluentValidation;                                // ✅ IValidator
using Microsoft.AspNetCore.Mvc;                        // ✅ FromBody, FromHeader
using SalesManagementService.DTOs.Requests;            // ✅ PaginationParams, OrderSearchParams
using SalesManagementService.Infrastructure.Exceptions; // ✅ UnauthorizedException, ForbiddenException
using SalesManagementService.Services.Interfaces;       // ✅ IOrderService
```

> 新規ハンドラ `GetMyOrders` で使用する全ての型が既存の using に含まれているため、**追加の using は不要**。
