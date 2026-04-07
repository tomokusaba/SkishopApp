# AiSupportService

SkiShop の AI サポート機能を提供するマイクロサービス。Semantic Kernel を使用した AI チャット、商品レコメンデーション、検索、需要予測を担当する。

## 技術スタック

- C# 14 / .NET 10
- ASP.NET Core 10 (Minimal API)
- Entity Framework Core 10 + PostgreSQL
- Semantic Kernel 1.x + Azure OpenAI
- Apache Kafka（イベント駆動）
- Redis（キャッシュ）

## ポート

| 環境 | ポート |
|------|--------|
| ローカル開発 | 5009 |
| Docker Compose | 5009 |

## 設定方法

### 設定クラスと appsettings の対応表

| 設定セクション | 設定クラス | 必須キー |
|---|---|---|
| `AzureOpenAI` | `AzureOpenAISettings` | `Endpoint`, `DeploymentName`, `EmbeddingDeploymentName`, `ApiKey`（省略時は DefaultAzureCredential） |
| `AzureAISearch` | `AzureAISearchSettings` | `Endpoint`, `IndexName`, `ApiKey`（省略時は DefaultAzureCredential） |
| `AiChat` | `AiChatSettings` | 全てデフォルト値あり（任意変更） |
| `Kafka` | `KafkaSettings` | `BootstrapServers` |
| `Services` | `ServiceEndpointSettings` | `InventoryManagementService`, `SalesManagementService` |
| `Jwt` | `JwtSettings` | `Issuer`, `Audience`, `SecretKey` |

> **注意**: 開発環境（`ASPNETCORE_ENVIRONMENT=Development`）では `AzureOpenAI` と `AzureAISearch` の `ValidateOnStart` がスキップされるため、Azure リソースがなくても起動可能です。

### 1. ローカル開発（`dotnet run`）— `dotnet user-secrets` を使用

秘密情報は `dotnet user-secrets` で管理し、appsettings.json やソースコードにハードコードしないこと。

```bash
cd Services/AiSupportService

# Azure OpenAI
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-4o"
dotnet user-secrets set "AzureOpenAI:EmbeddingDeploymentName" "text-embedding-3-small"

# Azure AI Search
dotnet user-secrets set "AzureAISearch:Endpoint" "https://your-search.search.windows.net"
dotnet user-secrets set "AzureAISearch:ApiKey" "your-search-api-key"

# DB 接続
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=aisupportdb;Username=skishop;Password=skishop_dev_password"

# JWT
dotnet user-secrets set "Jwt:SecretKey" "dev-signing-key-minimum-32-characters-long"
```

秘密情報は `~/.microsoft/usersecrets/` に保存され、Git にコミットされません。

### 2. Docker Compose — `docker-compose.yml` の `environment` セクション

`docker-compose.yml` の `ai-support-service` セクションで環境変数として設定する。キー名の `__`（ダブルアンダースコア）は JSON の `:` に対応する。

```yaml
ai-support-service:
  environment:
    # インフラ（設定済み）
    - ConnectionStrings__DefaultConnection=Host=postgres;Database=aisupportdb;Username=skishop;Password=skishop_dev_password
    - ConnectionStrings__Redis=redis:6379
    - Kafka__BootstrapServers=kafka:29092
    - Services__InventoryManagementService=http://inventory-management-service:5003
    - Services__SalesManagementService=http://sales-management-service:5004
    - Jwt__Issuer=https://skishop.local
    - Jwt__Audience=skishop-api
    - Jwt__SecretKey=dev-signing-key-minimum-32-characters-long

    # Azure OpenAI（コメントを外して設定）
    - AzureOpenAI__Endpoint=https://your-resource.openai.azure.com/
    - AzureOpenAI__ApiKey=your-api-key
    - AzureOpenAI__DeploymentName=gpt-4o
    - AzureOpenAI__EmbeddingDeploymentName=text-embedding-3-small

    # Azure AI Search
    - AzureAISearch__Endpoint=https://your-search.search.windows.net
    - AzureAISearch__ApiKey=your-search-api-key
```

### 3. 本番環境 — 環境変数 or Azure Key Vault

環境変数名は Docker Compose と同じ形式（`AzureOpenAI__Endpoint` 等）。Azure Key Vault を使用する場合は `Azure.Extensions.AspNetCore.Configuration.Secrets` パッケージを追加し、`Program.cs` で `builder.Configuration.AddAzureKeyVault(...)` を設定する。

## DB マイグレーション

起動時に未適用のマイグレーションがあれば自動的に適用されます。手動で実行する場合:

```bash
cd Services/AiSupportService

# マイグレーション追加
dotnet ef migrations add <MigrationName>

# マイグレーション適用
dotnet ef database update
```

## ビルド・テスト

```bash
# ビルド
cd Services/AiSupportService
dotnet build

# テスト
cd Services/AiSupportService.Tests
dotnet test

# Docker ビルド
cd Services
docker build -f AiSupportService/Dockerfile -t skishop-ai-support-service .
```

## ヘルスチェック

| エンドポイント | 用途 |
|---|---|
| `GET /health` | Liveness（常に 200） |
| `GET /health/ready` | Readiness（PostgreSQL・Redis の疎通確認） |
