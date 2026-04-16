# SkiShop Docker Compose 環境

このドキュメントでは、SkiShop マイクロサービス EC プラットフォームを Docker Compose でローカル起動する手順を説明します。

## 📋 必要条件

- **Docker Desktop** 4.20+ (または Docker Engine 24+)
- **Docker Compose** v2.20+
- **.NET SDK** 10.0.x
- **メモリ**: 最低 8GB（推奨 16GB）
- **ディスク**: 20GB 以上の空き容量

### Apple Silicon (M1/M2/M3) Mac の注意

Apple Silicon Mac では、一部のサービスで AMD64 エミュレーションが必要です。`docker-compose.yml` には `platform: linux/amd64` が設定されています。

## 🏗️ アーキテクチャ概要

```
┌────────────────────────────────────────────────────────────────────────────────┐
│                            Tier 3: フロントエンド                                │
│  ┌──────────────────┐    ┌───────────────────────────────────────────────────┐ │
│  │  Admin Panel     │    │           Frontend (.NET)                         │ │
│  │  :8081           │    │           :3000                                   │ │
│  └──────────────────┘    └───────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────────────────────┘
                                         │
                                         ▼
┌────────────────────────────────────────────────────────────────────────────────┐
│                            Tier 2: API Gateway                                  │
│  ┌───────────────────────────────────────────────────────────────────────────┐ │
│  │                     ApiGateway (YARP) :8080                               │ │
│  └───────────────────────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────────────────────┘
                                         │
                                         ▼
┌────────────────────────────────────────────────────────────────────────────────┐
│                            Tier 1: マイクロサービス                              │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐          │
│  │ AuthService  │ │ UserMgmt     │ │ Inventory    │ │ Sales        │          │
│  │ :5001        │ │ :5002        │ │ :5003        │ │ :5004        │          │
│  └──────────────┘ └──────────────┘ └──────────────┘ └──────────────┘          │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐          │
│  │ PaymentCart  │ │ Coupon       │ │ Point        │ │ MailSend     │          │
│  │ :5005        │ │ :5006        │ │ :5007        │ │ :5008        │          │
│  └──────────────┘ └──────────────┘ └──────────────┘ └──────────────┘          │
│  ┌──────────────┐                                                              │
│  │ AiSupport    │                                                              │
│  │ :5009        │                                                              │
│  └──────────────┘                                                              │
└────────────────────────────────────────────────────────────────────────────────┘
                                         │
                                         ▼
┌────────────────────────────────────────────────────────────────────────────────┐
│                            Tier 0: インフラストラクチャ                          │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐          │
│  │ PostgreSQL   │ │ Redis        │ │ Kafka        │ │ MailHog      │          │
│  │ :5432        │ │ :6379        │ │ :9092        │ │ :8025        │          │
│  └──────────────┘ └──────────────┘ └──────────────┘ └──────────────┘          │
└────────────────────────────────────────────────────────────────────────────────┘
```

## 🚀 起動手順

### AppHost（Aspire）での起動

Aspire 導入後の推奨起動方法です。AppHost が .NET サービス群を `AddProject` で起動し、PostgreSQL / Redis / Kafka / MailHog / Kafka UI / MCP Server もまとめてオーケストレーションします。

```bash
# ランタイム用 solution を復元
dotnet restore SkiShop.slnx

# AppHost を起動
dotnet run --project AppHost\AppHost.csproj
```

- Aspire Dashboard: `http://localhost:17100`
- Frontend: `http://localhost:3000`
- Admin Panel: `http://localhost:8081`
- API Gateway: `http://localhost:8080`
- MCP Server: `http://localhost:5010/mcp`
- Kafka UI: `http://localhost:8090`
- MailHog: `http://localhost:8025`

> `SkiShop.slnx` は AppHost と実行対象プロジェクトをまとめた solution です。テストプロジェクトは含めていません。
>
> MCP Server は `InventoryManagementService` の公開 read-only API を使って、商品検索（keyword/category/brand）と商品取得（id / sku）の tool を提供します。
>
> MCP Server へ接続するクライアントは `X-Api-Key` ヘッダーが必要です。固定キーで運用したい場合は AppHost の user-secrets に `Parameters:McpServerApiKey` を設定してください。

```bash
# 固定の MCP API key を設定（推奨）
dotnet user-secrets --project AppHost\AppHost.csproj set "Parameters:McpServerApiKey" "<32文字以上のランダム文字列>"
```

```text
MCP endpoint: http://localhost:5010/mcp
Required header: X-Api-Key: <Parameters:McpServerApiKey の値>
```

### Docker Compose での起動（従来手順）

### 1. インフラストラクチャの起動

まず、データベース、キャッシュ、メッセージキューなどの外部サービスを起動します：

```bash
# インフラストラクチャサービスのみ起動
docker compose up -d postgres redis kafka mailhog

# 起動状態を確認（全て healthy になるまで待つ）
docker compose ps
```

### 2. マイクロサービスのビルド

初回起動時は全サービスのビルドが必要です：

```bash
# 全サービスをビルド（時間がかかります）
docker compose build

# または特定のサービスのみビルド
docker compose build auth-service user-management-service
```

### 3. 全サービスの起動

```bash
# 全サービスを起動
docker compose up -d

# 起動状態を確認
docker compose ps

# ログを確認
docker compose logs -f
```

### 4. 特定のサービスのみ起動

開発時には特定のサービスのみを起動することもできます：

```bash
# 例: 認証サービスとユーザー管理サービスのみ
docker compose up -d postgres redis kafka auth-service user-management-service
```

## 🔧 サービス詳細

### アクセスポイント

| サービス | URL | 用途 |
|----------|-----|------|
| Frontend | http://localhost:3000 | ユーザー向け EC サイト |
| Admin Panel | http://localhost:8081 | 管理画面 |
| API Gateway | http://localhost:8080 | API エントリーポイント |
| MCP Server | http://localhost:5010/mcp | MCP client / Copilot 接続先 |
| Kafka UI | http://localhost:8090 | Kafka 管理画面 |
| MailHog | http://localhost:8025 | メール確認（開発用） |
| PostgreSQL | localhost:5432 | データベース |
| Redis | localhost:6379 | キャッシュ |

### データベース

PostgreSQL には以下のデータベースが自動作成されます：

- `authdb` - 認証サービス
- `userdb` - ユーザー管理
- `inventorydb` - 在庫管理
- `salesdb` - 販売管理
- `cartdb` - カート・決済
- `coupondb` - クーポン
- `pointdb` - ポイント
- `mailsenddb` - メール送信
- `aisupportdb` - AI サポート

接続情報：
- ユーザー名: `skishop`
- パスワード: `skishop_dev_password`

### Kafka

- ブローカー（コンテナ内から）: `kafka:29092`
- ブローカー（ホストから）: `localhost:9092`

## 🧪 検証用テストユーザー

Docker Compose で全サービスを起動した後、以下の手順で検証用ユーザーを作成できます。

### テストユーザー一覧

| 項目 | 一般ユーザー | 管理者ユーザー |
|------|------------|--------------|
| メールアドレス | `testuser@skishop.example.com` | `admin@skishop.example.com` |
| パスワード | `SkiShop2026!` | `AdminSkiShop2026!` |
| ユーザー名 | `testuser` | `admin` |
| 氏名 | 山田 太郎 | 管理 花子 |
| ロール | USER | ADMIN |
| メール検証 | 済み | 済み |

### 作成手順

#### Step 1: API Gateway 経由でユーザー登録

```bash
# 一般ユーザー登録
curl -s -X POST http://localhost:8080/api/v1/auth/users \
  -H "Content-Type: application/json" \
  -d '{
    "email": "testuser@skishop.example.com",
    "password": "SkiShop2026!",
    "firstName": "太郎",
    "lastName": "山田",
    "username": "testuser"
  }'

# 管理者ユーザー登録
curl -s -X POST http://localhost:8080/api/v1/auth/users \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@skishop.example.com",
    "password": "AdminSkiShop2026!",
    "firstName": "花子",
    "lastName": "管理",
    "username": "admin"
  }'
```

#### Step 2: AuthDB でユーザーをアクティベート

```bash
docker exec -i skishop-postgres psql -U skishop -d authdb <<'SQL'
-- 一般ユーザーをアクティベート（メール検証済みに設定）
UPDATE users SET status='ACTIVE', email_verified=true
WHERE email='testuser@skishop.example.com';

-- 管理者ユーザーをアクティベート + ADMIN ロール付与
UPDATE users SET status='ACTIVE', email_verified=true, role='ADMIN'
WHERE email='admin@skishop.example.com';
SQL
```

#### Step 3: UserManagementDB にユーザーを同期

AuthDB と UserManagementDB は独立した DB のため、手動で同期します。  
`<user-id>` と `<admin-id>` は Step 1 のレスポンスに含まれる `id` に置き換えてください。

```bash
docker exec -i skishop-postgres psql -U skishop -d userdb <<SQL
-- 一般ユーザー
INSERT INTO users (id, email, first_name, last_name, status,
  processing_restricted, data_export_requested,
  created_at, updated_at, row_version)
VALUES (
  '<user-id>',
  'testuser@skishop.example.com',
  '太郎', '山田', 'ACTIVE', false, false,
  CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, '\x'
);

-- 管理者ユーザー
INSERT INTO users (id, email, first_name, last_name, status,
  processing_restricted, data_export_requested,
  created_at, updated_at, row_version)
VALUES (
  '<admin-id>',
  'admin@skishop.example.com',
  '花子', '管理', 'ACTIVE', false, false,
  CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, '\x'
);
SQL
```

> **ヒント**: ユーザー ID は以下のコマンドで確認できます:
> ```bash
> docker exec -i skishop-postgres psql -U skishop -d authdb \
>   -c "SELECT id, email FROM users WHERE email LIKE '%skishop.example.com';"
> ```

#### Step 4: ログインしてトークンを取得

```bash
# 一般ユーザーでログイン
curl -s -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"testuser@skishop.example.com","password":"SkiShop2026!"}' \
  | jq '{accessToken: .accessToken[0:50], role: .user.role}'

# 管理者ユーザーでログイン
curl -s -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@skishop.example.com","password":"AdminSkiShop2026!"}' \
  | jq '{accessToken: .accessToken[0:50], role: .user.role}'
```

#### Step 5: 動作確認

```bash
# トークンを変数に格納
USER_TOKEN=$(curl -s -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"testuser@skishop.example.com","password":"SkiShop2026!"}' | jq -r '.accessToken')

# ユーザープロフィール取得（UserManagementService 経由）
curl -s http://localhost:8080/api/v1/users/me \
  -H "Authorization: Bearer $USER_TOKEN" | jq .

# 管理者エンドポイントへのアクセス確認（USER → 403 拒否）
curl -s -o /dev/null -w "HTTP: %{http_code}\n" \
  http://localhost:8080/admin/mail/templates \
  -H "Authorization: Bearer $USER_TOKEN"
# → HTTP: 403（正常: USER ロールでは管理者エンドポイントにアクセス不可）
```

### 認可マトリクス

| エンドポイント | 未認証 | USER | ADMIN |
|--------------|--------|------|-------|
| GET /api/products | ✅ 200 | ✅ 200 | ✅ 200 |
| POST /api/v1/auth/login | ✅ 200 | ✅ 200 | ✅ 200 |
| GET /api/v1/auth/me | ❌ 401 | ✅ 200 | ✅ 200 |
| GET /api/v1/users/me | ❌ 401 | ✅ 200 | ✅ 200 |
| GET /api/v1/cart | ❌ 401 | ✅ 200 | ✅ 200 |
| GET /admin/mail/templates | ❌ 401 | ❌ 403 | ✅ 200 |
| GET /api/v1/admin/users | ❌ 401 | ❌ 403 | ✅ 200 |

---

## 🛑 停止・クリーンアップ

```bash
# 全サービスを停止
docker compose down

# ボリュームも含めて完全削除（データ消失注意！）
docker compose down -v

# 未使用イメージの削除
docker image prune -f
```

## 🔍 トラブルシューティング

### サービスが起動しない場合

```bash
# サービスのログを確認
docker compose logs <サービス名>

# 例: auth-service のログ
docker compose logs auth-service

# リアルタイムでログを確認
docker compose logs -f auth-service
```

### よくある問題

#### 1. Exit code 139（セグメンテーション違反）

Apple Silicon Mac で発生することがあります。`docker-compose.yml` に `platform: linux/amd64` が設定されていることを確認してください。

#### 2. Kafka に接続できない

- サービスの環境変数で `Kafka__BootstrapServers=kafka:29092` が設定されているか確認
- Kafka コンテナが healthy になっているか確認: `docker compose ps kafka`

#### 3. データベース接続エラー

- PostgreSQL コンテナが healthy になっているか確認
- 接続文字列が正しいか確認: `Host=postgres;Database=<dbname>;Username=skishop;Password=skishop_dev_password`

#### 4. ポートが使用中

```bash
# 使用中のポートを確認
lsof -i :<ポート番号>

# 例: ポート 5432 を使用しているプロセス
lsof -i :5432
```

#### 5. ヘルスチェックが失敗する

```bash
# ヘルスチェック状態を確認
docker inspect --format='{{json .State.Health}}' <コンテナ名>

# 手動でヘルスチェック
curl -f http://localhost:5001/health
```

### マイグレーションの実行

各サービスのデータベースマイグレーションは、サービス起動時に自動実行されるか、手動で実行する必要があります：

```bash
# サービスコンテナ内でマイグレーション実行
docker compose exec auth-service dotnet ef database update
```

## 📁 ファイル構成

```
.
├── docker-compose.yml           # メインの構成ファイル（全サービス）
├── docker-compose.infra.yml     # インフラのみの構成ファイル（バックアップ）
├── docker-compose-make-plan.md  # 設計計画ドキュメント
├── AppHost/                     # .NET Aspire AppHost
├── Services/
│   ├── .dockerignore            # Docker ビルド除外設定
│   ├── AuthService/
│   ├── UserManagementService/
│   ├── InventoryManagementService/
│   ├── SalesManagementService/
│   ├── PaymentCartService/
│   ├── CouponService/
│   ├── PointService/
│   ├── MailSendService/
│   ├── AiSupportService/
│   ├── McpServer/
│   ├── ApiGateway/
│   ├── AdminPanel/
│   └── frontend/
└── infra/
    └── postgres/
        └── init-databases.sql   # DB 初期化スクリプト
```

## ⚠️ 既知の問題

### 1. アプリケーションコードの問題

現在、いくつかのサービスでアプリケーションコードに問題があり、正常に起動しません：

1. **JwtSettings クラス** - パラメータなしコンストラクタが必要
2. **Kafka 設定** - 一部サービスで `localhost:9092` がハードコードされている
3. **DB マイグレーション** - 一部サービスでスキーマが未作成

これらは `docker-compose.yml` の問題ではなく、各サービスのソースコードを修正する必要があります。

### 2. AI Support Service

Azure OpenAI の認証情報が必要です。以下の環境変数を設定してください：

- `AzureOpenAI__Endpoint`
- `AzureOpenAI__DeploymentName`
- `AzureOpenAI__ApiKey`（または Azure Identity を使用）

## 📝 開発時のヒント

### ホットリロード開発

サービスのソースコードを変更しながら開発する場合：

```bash
# ローカルで .NET サービスを起動（docker-compose のインフラのみ使用）
docker compose up -d postgres redis kafka mailhog
cd Services/AuthService
dotnet watch run
```

### インフラのみ起動

バックエンドサービスをローカルで開発する場合：

```bash
# docker-compose.infra.yml を使用
docker compose -f docker-compose.infra.yml up -d
```

---

## 📞 サポート

問題が解決しない場合は、以下を確認してください：

1. `docker-compose-make-plan.md` - 詳細な設計計画
2. `design-docs/` - 各サービスの設計ドキュメント
3. 各サービスの `appsettings.json` - 設定項目の確認
