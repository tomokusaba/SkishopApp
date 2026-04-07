---
applyTo:
  - "**/Dockerfile"
  - "**/docker-compose*.yml"
---

# Dockerfile / インフラ設定 Instructions

本 Instructions は `**/Dockerfile` および `**/docker-compose*.yml` に自動適用される。コンテナ設定の作成・編集時に以下のチェック観点を遵守すること。

---

## 1. マルチステージビルド

- ビルドステージとランタイムステージを**必ず分離**する
- ビルドツール（SDK, ソースコード）をランタイムイメージに含めない
- 最終イメージのサイズを最小化する

```dockerfile
# ✅ 良い例: マルチステージビルド
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ServiceName/ServiceName.csproj", "ServiceName/"]
RUN dotnet restore "ServiceName/ServiceName.csproj"
COPY . .
WORKDIR "/src/ServiceName"
RUN dotnet publish "ServiceName.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ServiceName.dll"]
```

```dockerfile
# ❌ 悪い例: 単一ステージ（SDK + ソースコードが残る）
FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /app
COPY . .
RUN dotnet publish -c Release -o /out
ENTRYPOINT ["dotnet", "/out/ServiceName.dll"]
```

### レイヤーキャッシュの最適化
- `.csproj` ファイルと `dotnet restore` を**ソースコードのコピーより先に配置**する（依存関係が変わらない限りキャッシュが効く）
- 頻繁に変更されるファイル（ソースコード）はできるだけ最後のレイヤーに配置する
- 不要なファイルは `.dockerignore` で除外する

```
# ✅ .dockerignore の設定例
**/bin/
**/obj/
.git/
.github/
*.md
*.sln.DotSettings
.idea/
.vs/
```

---

## 2. ベースイメージ

- **公式の Microsoft Container Registry（MCR）イメージ**を使用する
- タグは **`latest` を使用禁止**。特定バージョンに固定する
- 可能であれば**ダイジェスト（SHA256）でイメージを固定**する（サプライチェーン攻撃の防止）
- ランタイムには **ASP.NET Core ランタイムイメージ**（`aspnet`）を使用する。SDK は不要

```dockerfile
# ✅ 良い例: バージョン固定 + ASP.NET Core ランタイム
FROM mcr.microsoft.com/dotnet/aspnet:10.0

# ⚠️ 許容: Alpine ベース（軽量）
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine

# ❌ 悪い例: latest タグ
FROM mcr.microsoft.com/dotnet/aspnet:latest

# ❌ 悪い例: SDK をランタイムに使用
FROM mcr.microsoft.com/dotnet/sdk:10.0
```

| 選択基準 | 推奨イメージ | 備考 |
|---------|-----------|------|
| 標準環境 | `mcr.microsoft.com/dotnet/aspnet:10.0` | Debian ベース、安定性重視 |
| イメージサイズ重視 | `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` | Alpine ベースで軽量。musl libc 依存に注意 |
| Chiseled（最小構成） | `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled` | Ubuntu Chiseled、シェルなし、最小攻撃面 |

---

## 3. 非 root 実行

- **`USER` 命令で非 root ユーザーを指定する**（必須）
- アプリケーションを root 権限で実行しない
- アプリケーションユーザーは最小限の権限を持つ

```dockerfile
# ✅ 良い例: 非 root ユーザーの作成と使用
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

RUN groupadd -r skishop && useradd -r -g skishop -d /app -s /sbin/nologin skishop
COPY --from=build --chown=skishop:skishop /app/publish .

USER skishop

ENTRYPOINT ["dotnet", "ServiceName.dll"]
```

```dockerfile
# ❌ 悪い例: USER 命令なし（root で実行される）
FROM mcr.microsoft.com/dotnet/aspnet:10.0
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ServiceName.dll"]
```

---

## 4. ヘルスチェック

- **`HEALTHCHECK` 命令を必ず定義する**
- ASP.NET Core の `/health` エンドポイントを使用する
- 適切な間隔・タイムアウト・リトライを設定する

```dockerfile
# ✅ 良い例: ヘルスチェック設定
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1
```

| パラメータ | 推奨値 | 説明 |
|-----------|-------|------|
| `--interval` | 30s | チェック間隔 |
| `--timeout` | 10s | タイムアウト |
| `--start-period` | 30s | 初期起動猶予時間（.NET の起動は高速） |
| `--retries` | 3 | 失敗とみなすまでのリトライ回数 |

- **curl が含まれないイメージの場合**（Chiseled 等）は `wget` または .NET ベースのヘルスチェックを使用する

```dockerfile
# ✅ curl なしイメージ向け: wget を使用
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
  CMD wget -qO- http://localhost:8080/health || exit 1
```

---

## 5. .NET ランタイム設定

- コンテナのメモリ制限と .NET ランタイム設定を**整合させる**
- .NET はコンテナのメモリ制限を自動検出するが、必要に応じて環境変数で調整する
- 本番環境では診断設定を適切に構成する

```dockerfile
# ✅ 良い例: .NET ランタイム設定
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_gcServer=1
ENV DOTNET_GCHeapHardLimit=0x40000000

ENTRYPOINT ["dotnet", "ServiceName.dll"]
```

| 設定 | 推奨値 | 説明 |
|------|-------|------|
| `DOTNET_RUNNING_IN_CONTAINER` | `true` | コンテナ環境の自動検出を有効化 |
| `DOTNET_gcServer` | `1` | サーバー GC を有効化（マルチコア環境で推奨） |
| `ASPNETCORE_ENVIRONMENT` | `Production` | 本番環境の設定を使用 |
| `COMPlus_EnableDiagnostics` | `0` | Chiseled イメージで診断を無効化（任意） |

---

## 6. セキュリティ

- **不要なパッケージをインストールしない**。`apt-get install` は最小限に
- インストール後に**パッケージマネージャーのキャッシュを削除**する
- **秘密情報（API キー、パスワード等）を Dockerfile に含めない**。ビルド引数（`ARG`）にも含めない
- ポートは**必要最小限のみ公開**する

```dockerfile
# ✅ 良い例: 最小限のポート公開
EXPOSE 8080

# ❌ 悪い例: 管理用ポートも公開
EXPOSE 8080 8081 5005 9090
```

```dockerfile
# ❌ 悪い例: 秘密情報をビルド引数で渡す
ARG DB_PASSWORD
ENV DATABASE_PASSWORD=$DB_PASSWORD
```

---

## 7. docker-compose 設定

### リソース制限
- **CPU / メモリの制限を設定する**（OOM によるホスト影響の防止）

```yaml
# ✅ 良い例: リソース制限
services:
  app:
    image: myapp:latest
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 1024M
        reservations:
          cpus: '0.5'
          memory: 512M
```

### 環境変数管理
- 秘密情報は `environment` に直接書かず、`env_file` または Docker Secrets を使用する
- `env_file` は `.gitignore` に含める

```yaml
# ✅ 良い例: env_file を使用
services:
  app:
    image: myapp:latest
    env_file:
      - .env

# ❌ 悪い例: 秘密情報を直接記述
services:
  app:
    image: myapp:latest
    environment:
      - DB_PASSWORD=mysecretpassword
```

### ヘルスチェック
- docker-compose でもヘルスチェックを定義する

```yaml
# ✅ 良い例: ヘルスチェック
services:
  app:
    image: myapp:latest
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 30s
```

### 依存関係管理
- `depends_on` に `condition: service_healthy` を使用してサービス起動順序を制御する

```yaml
# ✅ 良い例: ヘルスチェックベースの依存関係
services:
  app:
    depends_on:
      db:
        condition: service_healthy
  db:
    image: postgres:17
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5
```

### ネットワーク分離
- フロントエンド / バックエンド / DB 層のネットワークを分離する

```yaml
# ✅ 良い例: ネットワーク分離
services:
  app:
    networks:
      - frontend
      - backend
  db:
    networks:
      - backend

networks:
  frontend:
  backend:
    internal: true  # 外部からのアクセスを遮断
```

---

## 8. 禁止事項チェックリスト

| # | 禁止事項 | 理由 |
|---|---------|------|
| 1 | `FROM ... :latest` の使用 | ビルドの再現性が失われる |
| 2 | `USER` 命令なし（root 実行） | コンテナエスケープ時のリスク増大 |
| 3 | `HEALTHCHECK` なし | 障害検知の遅延 |
| 4 | 秘密情報の Dockerfile / docker-compose 内記述 | イメージ / リポジトリへの秘密情報漏洩 |
| 5 | 単一ステージビルド（SDK + ソースコード残存） | イメージサイズ増大、ソースコード漏洩 |
| 6 | 不必要なポートの公開 | 攻撃面の拡大 |
| 7 | `.dockerignore` なし | イメージへの不要ファイル混入 |
| 8 | リソース制限なし（docker-compose） | OOM によるホスト影響 |
