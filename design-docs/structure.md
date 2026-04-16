# スキー用品販売ショップ - マイクロサービスアーキテクチャ図

## システム概要

スキー用品に特化した EC プラットフォーム。マイクロサービスアーキテクチャで構築され、.NET Aspire 13.2.2 によるオーケストレーションを採用している。  
季節性の高い需要に対応する柔軟なスケーリング機能と、Semantic Kernel を活用した AI によるパーソナライズされたショッピング体験を提供する。

## アーキテクチャ図

```text
┌─────────────────────────────────────────────────────────────────────────┐
│                        クライアント層                                    │
├─────────────────────────────────────────────────────────────────────────┤
│  Web UI  │  モバイルアプリ  │  管理パネル  │  外部システム  │  API クライアント │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         API Gateway                                     │
│                       (Port: 8080)                                      │
│         • ルーティング  • 認証  • 負荷分散  • レート制限                     │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                    ┌───────────────┼───────────────┐
                    │               │               │
                    ▼               ▼               ▼
┌─────────────┬─────────────┬─────────────┬─────────────┬─────────────┐
│    認証      │  ユーザー    │    在庫      │    販売     │  AI サポート  │
│  サービス    │   管理       │    管理      │    管理     │  サービス     │
│             │  サービス     │  サービス    │  サービス    │              │
│(Port: 5001) │(Port: 5002) │(Port: 5003) │(Port: 5004) │(Port: 5009) │
└─────────────┴─────────────┴─────────────┴─────────────┴─────────────┘
                    │               │               │
                    ▼               ▼               ▼
┌─────────────┬─────────────┬─────────────┬─────────────┐
│ 決済・カート │   クーポン   │   ポイント   │  メール送信  │
│  サービス    │  サービス    │  サービス    │  サービス    │
│(Port: 5005) │(Port: 5006) │(Port: 5007) │(Port: 5008) │
└─────────────┴─────────────┴─────────────┴─────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    データ & インフラストラクチャ層                         │
├─────────────┬─────────────┬─────────────┬─────────────────────────────┤
│ PostgreSQL  │    Redis    │    Kafka    │      OpenTelemetry          │
│(Port: 5432) │(Port: 6379) │(Port: 9092) │  分散トレーシング・メトリクス   │
│  メイン DB   │  キャッシュ │ メッセージング│                             │
└─────────────┴─────────────┴─────────────┴─────────────────────────────┘
```

> 補助アクセス経路として `McpServer`（Port: 5010）を配置し、MCP クライアントから `InventoryManagementService` の公開 read-only 商品 API を安全に利用できるようにする。

## マイクロサービス詳細構成

### コアサービス

#### 1. API Gateway (Port: 8080)

- **役割**: 統一アクセスポイント、リクエストルーティング
- **機能**:
  - 統一された認証・認可制御
  - レート制限とセキュリティ
  - 負荷分散とサーキットブレーカー
- **技術**: YARP リバースプロキシ

#### 2. AuthService（認証サービス）(Port: 5001)

- **役割**: 認証・認可管理
- **機能**:
  - JWT / OAuth 2.0 / OpenID Connect
  - 多要素認証（MFA）
  - ソーシャルログイン連携
  - パスワードリセット
- **データベース**: PostgreSQL
- **技術**: ASP.NET Core Identity, Microsoft.Identity.Web

#### 3. UserManagementService（ユーザー管理サービス）(Port: 5002)

- **役割**: ユーザー情報管理
- **機能**:
  - ユーザープロファイル管理
  - 住所録・配送先管理
  - ユーザー設定・権限管理
- **データベース**: PostgreSQL
- **技術**: ASP.NET Core 10 (Minimal API), EF Core 10

### ビジネスサービス

#### 4. InventoryManagementService（在庫管理サービス）(Port: 5003)

- **役割**: 商品・在庫管理
- **機能**:
  - 商品カタログ管理
  - 在庫数量管理・予約
  - カテゴリ・タグ管理
  - 商品画像・メタデータ
- **データベース**: PostgreSQL
- **検索**: EF Core LINQ

#### 5. SalesManagementService（販売管理サービス）(Port: 5004)

- **役割**: 注文・販売管理
- **機能**:
  - 注文処理・履歴管理
  - 配送状況追跡
  - 返品・交換処理
  - 売上分析・レポート
- **データベース**: PostgreSQL
- **メッセージング**: Kafka

#### 6. PaymentCartService（決済・カートサービス）(Port: 5005)

- **役割**: 決済・カート処理
- **機能**:
  - ショッピングカート管理
  - 決済処理（クレジット/デビットカード）
  - 決済ゲートウェイ連携
  - 決済履歴管理
- **データベース**: PostgreSQL
- **キャッシュ**: Redis

#### 7. CouponService（クーポンサービス）(Port: 5006)

- **役割**: クーポン管理
- **機能**:
  - クーポン作成・発行
  - 利用条件・制限管理
  - クーポン適用処理
  - 有効期限・利用回数管理
- **データベース**: PostgreSQL

#### 8. PointService（ポイントサービス）(Port: 5007)

- **役割**: ポイント管理
- **機能**:
  - ポイント獲得・利用
  - ポイント履歴管理
  - ポイント有効期限管理
  - ポイント還元率設定
- **データベース**: PostgreSQL

#### 9. MailSendService（メール送信サービス）(Port: 5008)

- **役割**: メール送信処理
- **機能**:
  - 注文確認メール送信
  - パスワードリセットメール
  - プロモーション通知
  - メール送信履歴管理
- **データベース**: PostgreSQL

#### 10. AiSupportService（AI サポートサービス）(Port: 5009)

- **役割**: AI 推奨・検索・チャットボット
- **機能**:
  - 商品レコメンデーション
  - 高度な商品検索・フィルタリング
  - AI チャットボット
  - ユーザー行動分析
- **技術**: Semantic Kernel 1.x, OpenAI API

#### 11. McpServer（MCP サーバー）(Port: 5010)

- **役割**: MCP クライアント向けの read-only tool endpoint
- **機能**:
  - 商品検索（keyword/category/brand, ページング）
  - 商品取得（id / sku）
  - `InventoryManagementService` への安全なプロキシ
- **技術**: ASP.NET Core 10, ModelContextProtocol.AspNetCore, .NET Aspire 13.2.2

### インフラストラクチャサービス

#### データベース

- **PostgreSQL (Port: 5432)**: メインデータベース
- **Redis (Port: 6379)**: キャッシュ・セッション管理

#### メッセージング

- **Apache Kafka (Port: 9092)**: イベントストリーミング・非同期処理

#### 監視・観測

- **OpenTelemetry**: 分散トレーシング・メトリクス・ログ統合
- **.NET Aspire ダッシュボード**: サービス可視化

## 技術スタック

### 開発環境

```text
言語: C# 14 (.NET 10 LTS)
フレームワーク: ASP.NET Core 10 (Minimal API)
オーケストレーション: .NET Aspire 13.2.2
ORM: Entity Framework Core 10
ビルドツール: dotnet CLI / MSBuild
```

### データ・メッセージング

```text
データベース: PostgreSQL 15
キャッシュ: Redis 7
メッセージング: Apache Kafka (Confluent.Kafka)
```

### 監視・運用

```text
トレーシング・メトリクス: OpenTelemetry
ロギング: Serilog + ILogger<T>
ヘルスチェック: ASP.NET Core HealthChecks
テスト: xUnit, Testcontainers.PostgreSql
```

## プロジェクト構造

```text
DotNet-Skishop-App/
├── DotNet-Skishop-App.sln             # ソリューションファイル
├── README.md                          # プロジェクト概要
│
├── AppHost/                           # .NET Aspire オーケストレーション
│   ├── AppHost.csproj
│   └── Program.cs
│
├── ApiGateway/                        # API ゲートウェイ (YARP)
│   ├── ApiGateway.csproj
│   └── Program.cs
│
├── AuthService/                       # 認証サービス
│   ├── AuthService.csproj
│   ├── Program.cs
│   ├── Endpoints/
│   ├── Services/
│   ├── Repositories/
│   ├── Models/
│   └── DTOs/
│
├── UserManagementService/             # ユーザー管理サービス
│   ├── UserManagementService.csproj
│   └── ...
│
├── InventoryManagementService/        # 在庫管理サービス
│   ├── InventoryManagementService.csproj
│   └── ...
│
├── SalesManagementService/            # 販売管理サービス
│   ├── SalesManagementService.csproj
│   └── ...
│
├── PaymentCartService/                # 決済・カートサービス
│   ├── PaymentCartService.csproj
│   └── ...
│
├── CouponService/                     # クーポンサービス
│   ├── CouponService.csproj
│   └── ...
│
├── PointService/                      # ポイントサービス
│   ├── PointService.csproj
│   └── ...
│
├── MailSendService/                   # メール送信サービス
│   ├── MailSendService.csproj
│   └── ...
│
├── AiSupportService/                  # AI サポートサービス
│   ├── AiSupportService.csproj
│   └── ...
│
├── McpServer/                         # MCP サーバー
│   ├── McpServer.csproj
│   └── ...
│
├── design-docs/                       # 設計ドキュメント
│   ├── spec.md                        # 仕様書
│   ├── strategy.md                    # 戦略
│   ├── structure.md                   # アーキテクチャ構成
│   └── [service]-design.md            # 各サービス設計書
│
└── tests/                             # テストプロジェクト
    ├── AuthService.Tests/
    ├── InventoryManagementService.Tests/
    └── ...
```

## サービス間通信

### 同期通信

- **HTTP/REST API**: リアルタイム処理が必要な操作（`IHttpClientFactory` + Polly で耐障害性を確保）
- **サービス間認証**: JWT トークンベース認証

### 非同期通信

- **Apache Kafka**: イベント駆動アーキテクチャ（Outbox パターンで整合性保証）
- **イベント例**:
  - ユーザー登録完了 → ポイント付与
  - 注文確定 → 在庫減少、決済処理
  - 決済完了 → 配送手配、領収書発行

### データ連携

- **Redis**: セッション共有、カート状態キャッシュ

## セキュリティ構成

```text
┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│  クライアント │───▶│ API Gateway │───▶│   サービス   │
└─────────────┘    └─────────────┘    └─────────────┘
       │                    │                  │
       ▼                    ▼                  ▼
   HTTPS/TLS          JWT 検証           サービス間
                      レート制限          認証 (JWT)
```

### 認証フロー

1. クライアント → API Gateway (HTTPS)
2. API Gateway → AuthService (JWT 検証)
3. 認証済みリクエスト → 各マイクロサービス
4. サービス間通信（内部 JWT）

## スケーリング戦略

### 水平スケーリング

- **ステートレス設計**: 全サービスがステートレス
- **負荷分散**: API Gateway（YARP）経由
- **オーケストレーション**: .NET Aspire 13.2.2 によるサービス管理

### データベース戦略

- **サービス別 DB 分離**: マイクロサービスごとに専用 DB
- **読み書き分離**: 読み取り専用レプリカ
- **キャッシュ活用**: Redis での高頻度アクセスデータキャッシュ

## 運用・監視

### メトリクス・トレーシング

- **OpenTelemetry**: 分散トレーシング、メトリクス、ログの統合収集
- **ビジネスメトリクス**: カスタムメトリクス
- **インフラメトリクス**: システム監視

### ログ管理

- **構造化ログ**: Serilog + ILogger<T>（JSON 形式）
- **相関 ID**: リクエスト追跡（Correlation ID ミドルウェア）
- **ログレベル**: Error, Warning, Information, Debug

### ヘルスチェック

- **アプリケーションヘルスチェック**: ASP.NET Core HealthChecks（`/health`, `/health/ready`）
- **依存関係チェック**: DB, Redis, Kafka 接続確認
- **カスケード障害対策**: サーキットブレーカーパターン（Polly）

## 今後の拡張計画

- **マルチテナント対応**: 企業顧客向け機能
- **国際化対応**: 多言語・多通貨
- **モバイルアプリ**: React Native / Flutter
- **機械学習強化**: Semantic Kernel による高度なレコメンデーション
- **リアルタイム機能**: SignalR（WebSocket、Server-Sent Events）
