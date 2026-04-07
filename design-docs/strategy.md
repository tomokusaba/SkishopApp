# エンタープライズ .NET アプリケーション開発ガイド（マイクロサービスアーキテクチャ）

## 目次

1. [アーキテクチャ設計](#アーキテクチャ設計)
2. [マイクロサービスアーキテクチャ](#マイクロサービスアーキテクチャ)
3. [開発方法論](#開発方法論)
4. [パフォーマンスとスケーラビリティ](#パフォーマンスとスケーラビリティ)
5. [セキュリティ](#セキュリティ)
6. [テスト戦略](#テスト戦略)
7. [デプロイメントと運用](#デプロイメントと運用)
8. [非機能要件](#非機能要件)
9. [ベストプラクティス](#ベストプラクティス)

## アーキテクチャ設計

### レイヤードアーキテクチャ

- **プレゼンテーション層**:  
  - ユーザーインターフェース、REST API、その他の外部インターフェース
  - API Gateway と BFF（Backend For Frontend）パターンの採用
  - 柔軟なデータ取得のための GraphQL の検討

- **ビジネスロジック層**:  
  - コアアプリケーションロジック
  - ドメイン駆動設計（DDD）の原則に基づく実装
  - CQRS（Command Query Responsibility Segregation）パターンの検討

- **データアクセス層**:  
  - データベースおよびその他の永続ストレージとの統合
  - Repository パターンの採用
  - ORM として EF Core 10 を使用

- **インフラストラクチャ層**:  
  - 技術的な共通機能（ロギング、認証、監視等）を提供
  - 横断的関心事の集約
  - 外部サービス統合の抽象化

### マイクロサービス vs. モノリス

- **マイクロサービス**:
  - 独立したサービスに分割され、それぞれが特定のビジネス機能に集中
  - サービスごとに異なる技術スタックを採用可能
  - スケーラビリティの向上（サービスレベルでのスケーリング）
  - チーム間の独立した開発が可能（コンウェイの法則）
  - サービス間の明確な境界と責任分離
  - 部分的なデプロイとロールバックが可能
  - 運用の複雑さの増大（分散システム管理の複雑さ）
  - サービス間通信の信頼性確保の必要性
  - データ整合性維持の課題（結果整合性の検討）

- **モノリス**:
  - シンプルな開発と運用
  - 単一デプロイ単位
  - トランザクション処理が容易（ACID 特性の維持）
  - メソッド呼び出しによるコンポーネント間の効率的な通信
  - スケーリングが困難な場合がある（垂直スケーリングの限界）
  - 部分的な機能更新が困難

マイクロサービスが必ずしも最適解とは限らない。マイクロサービスアーキテクチャの採用は、組織規模、プロジェクトの複雑さ、開発チームの成熟度、ビジネス要件等を総合的に考慮して判断すべきである。

## マイクロサービスアーキテクチャ

マイクロサービスベースの設計を採用する際の重要な考慮事項を詳述する。

### サービス分解の原則

- **ビジネス能力による分解**:
  - 組織的なビジネス機能に基づいてサービスを分割
  - 例: 注文管理、在庫管理、顧客管理、決済処理等

- **サブドメインによる分解（DDD）**:
  - ドメイン駆動設計の境界づけられたコンテキスト（Bounded Context）に基づく分割
  - 各サービスが明確なドメインモデルと責任範囲を持つ

- **分解の粒度**:
  - 適切なサービスサイズの決定（過度に細粒度な分割を避ける）
  - 「Two Pizza Team」の原則（各サービスは2枚のピザで食事できるチームで開発可能）
  - データの凝集度と独立性を考慮

### サービス間通信

- **同期通信**:
  - REST API（OpenAPI 仕様）
  - gRPC（Protocol Buffers）
  - GraphQL（クライアント最適化クエリ）

- **非同期通信**:
  - メッセージキュー（Apache Kafka）
  - イベント駆動アーキテクチャ（Event Sourcing）
  - CQRS（Command Query Responsibility Segregation）

- **API Gateway**:
  - クライアントとマイクロサービスの仲介
  - ルーティング、認証・認可、SSL 終端、負荷分散
  - レート制限、キャッシング、API 集約
  - YARP リバースプロキシの活用

### データ管理戦略

- **データベース選定**:
  - サービスごとの独立データベース（Database per Service）
  - PostgreSQL に統一（全サービス共通）
  - Redis によるキャッシュの補完

- **分散トランザクション**:
  - Saga パターンによる複数サービス間のトランザクション管理
  - 二相コミットの回避（パフォーマンス問題）
  - 結果整合性の受容
  - 補償トランザクションの実装

- **データレプリケーション**:
  - サービス間のデータ共有の最小化
  - 必要に応じた冗長データの維持と更新
  - CDC（Change Data Capture）を使用したデータ同期

### サービスディスカバリと負荷分散

- **サービスディスカバリ**:
  - .NET Aspire 13.1 によるサービス参照（`WithReference`）
  - 動的なサービス登録と発見
  - ハードコード URL の禁止

- **負荷分散**:
  - YARP リバースプロキシによる負荷分散
  - `IHttpClientFactory` + Polly によるサービス間通信のレジリエンス

### ドメイン駆動設計（DDD）

- **戦略的設計**:
  - ビジネスドメインに焦点を当てたモデリング
  - ユビキタス言語の確立と共通理解の促進
  - 境界づけられたコンテキスト（Bounded Context）の定義と明確化
  - コンテキストマップによるサービス間関係の可視化

- **戦術的設計**:
  - 集約（Aggregate）の適切な設計
  - エンティティ、値オブジェクト、リポジトリの明確な役割分離
  - ドメインサービスとアプリケーションサービスの区別
  - ドメインイベントによるサービス間連携

- **イベントストーミング**:
  - チーム全体でのドメインモデル理解の共有
  - 主要なビジネスプロセスとイベントフローの可視化
  - 境界づけられたコンテキストの発見と精緻化

### クラウドネイティブアーキテクチャ

- **12-Factor Application**:
  - コードベース: バージョン管理下の単一コードベース
  - 依存関係: 明示的に宣言され分離された依存関係
  - 設定: 環境変数による設定とコードの分離
  - バッキングサービス: 付帯リソースとして扱う
  - ビルド/リリース/実行: 厳密に分離されたステージ
  - プロセス: ステートレスで共有なしのプロセス
  - ポートバインディング: 自己完結型のサービス公開
  - 並行性: プロセスモデルによるスケールアウト
  - 廃棄容易性: 高速な起動とグレースフルシャットダウン
  - 開発/本番一致: 環境の最大限の類似性
  - ログ: イベントストリームとしてのログ処理
  - 管理プロセス: ワンオフプロセスの実行

## 開発方法論

### 依存性注入（DI）

- **コンポーネント間の疎結合**:
  - 高結合からの脱却
  - テスタビリティの向上
  - コンポーネントの再利用性の向上

- **DI コンテナ**:
  - ASP.NET Core 組み込み DI コンテナ（`Microsoft.Extensions.DependencyInjection`）
  - ライフタイム管理: `AddScoped`（リクエストスコープ）、`AddTransient`、`AddSingleton`

- **DI 実装パターン**:
  - コンストラクタインジェクション（推奨）
  - primary constructor（C# 12+ 推奨）
  - プロパティインジェクション（非推奨）

```csharp
// 依存性注入の例（ASP.NET Core - primary constructor によるコンストラクタインジェクション）
public class UserService(
    IUserRepository userRepository,
    IEmailService emailService,
    ILogger<UserService> logger) : IUserService
{
    public async Task<UserDto> RegisterUserAsync(UserRegistrationRequest request, CancellationToken ct = default)
    {
        // ユーザー登録ロジック
        var newUser = new User
        {
            Email = request.Email,
            UserName = request.UserName,
            PasswordHash = passwordHasher.HashPassword(null!, request.Password)
        };

        await userRepository.AddAsync(newUser, ct);
        await userRepository.SaveChangesAsync(ct);

        // 登録成功時にウェルカムメールを送信
        await emailService.SendWelcomeEmailAsync(newUser, ct);

        return new UserDto(newUser.Id, newUser.Email, newUser.UserName, newUser.CreatedAt);
    }
}

// Program.cs での DI 登録
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
```

### インターフェース指向設計

- **実装よりインターフェースを優先**:
  - 明確な契約定義
  - 実装詳細からの分離
  - モジュール間の疎結合の促進

- **ポリモーフィズムの活用**:
  - 実行時の実装置換
  - 拡張性と柔軟性の向上
  - Strategy パターン等の設計パターンとの互換性

- **テスト戦略への影響**:
  - モックオブジェクトによるテストの容易化
  - 依存コンポーネントの分離
  - ユニットテストカバレッジの拡大

```csharp
// インターフェース指向設計の例
public interface IPaymentProcessor
{
    Task<PaymentResult> ProcessPaymentAsync(PaymentRequest paymentRequest, CancellationToken ct = default);
    bool SupportsPaymentMethod(PaymentMethod method);
    Task<PaymentStatus> CheckStatusAsync(string transactionId, CancellationToken ct = default);
}

public class CreditCardPaymentProcessor(
    IPaymentGateway paymentGateway,
    ILogger<CreditCardPaymentProcessor> logger) : IPaymentProcessor
{
    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest paymentRequest, CancellationToken ct = default)
    {
        // クレジットカード決済の実装
        return await paymentGateway.ProcessCardPaymentAsync(paymentRequest, ct);
    }

    public bool SupportsPaymentMethod(PaymentMethod method)
        => method == PaymentMethod.CreditCard;

    public async Task<PaymentStatus> CheckStatusAsync(string transactionId, CancellationToken ct = default)
        => await paymentGateway.GetTransactionStatusAsync(transactionId, ct);
}

// 他の実装（PayPal、Apple Pay 等）も同じインターフェースを実装
```

### 例外処理戦略

- **例外の分類と適切な使用**:
  - NotFoundException: リソースが見つからない場合 → HTTP 404
  - BusinessException: ビジネスルール違反 → HTTP 422
  - UnauthorizedException: 認証エラー → HTTP 401
  - ForbiddenException: 認可エラー → HTTP 403
  - ConcurrencyException: 楽観的ロック競合 → HTTP 409

- **グローバル例外ハンドリング**:
  - 一貫したエラーレスポンス形式（RFC 9457 Problem Details）
  - 適切な HTTP ステータスコードマッピング
  - セキュリティを意識した例外情報の制限

- **例外の監視とロギング**:
  - 構造化エラーログ
  - 適切なログレベルの使い分け
  - 例外情報の集中監視と分析

- **クライアントへのエラー通知**:
  - ユーザーフレンドリーなエラーメッセージ
  - 開発環境のみの詳細デバッグ情報
  - 国際化（i18n）対応のエラーメッセージ

```csharp
// カスタム例外定義
public class ResourceNotFoundException : Exception
{
    public string ResourceType { get; }
    public string ResourceId { get; }

    public ResourceNotFoundException(string resourceType, string resourceId)
        : base($"{resourceType}（ID: {resourceId}）が見つかりません")
    {
        ResourceType = resourceType;
        ResourceId = resourceId;
    }
}

// グローバル例外ハンドラーの例（ASP.NET Core Minimal API）
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var error = feature?.Error;

        if (error is not (NotFoundException or BusinessException or UnauthorizedException or ForbiddenException))
            logger.LogError(error, "未処理の例外: {Message}", error?.Message);
        else
            logger.LogWarning("処理済み例外: {ExceptionType} - {Message}", error.GetType().Name, error.Message);

        var problem = error switch
        {
            NotFoundException e    => TypedResults.Problem(e.Message, statusCode: 404),
            BusinessException e    => TypedResults.Problem(e.Message, statusCode: 422),
            UnauthorizedException  => TypedResults.Problem(statusCode: 401),
            ForbiddenException     => TypedResults.Problem(statusCode: 403),
            ConcurrencyException e => TypedResults.Problem(e.Message, statusCode: 409),
            _                      => TypedResults.Problem("内部エラーが発生しました", statusCode: 500)
        };
        await problem.ExecuteAsync(context);
    });
});
```

## パフォーマンスとスケーラビリティ

### データベース最適化

- **インデックス戦略**:
  - クエリパターンに基づく適切なインデックス設計
  - 複合インデックスの効果的な使用
  - 過度なインデックス作成の回避（更新パフォーマンスへの影響）
  - EXPLAIN コマンドによるクエリプラン分析

- **コネクションプーリング**:
  - Npgsql コネクションプールの適切な設定
  - プールサイズの適切な設定（最大/最小/アイドル）
  - コネクションリーク検出と防止
  - コネクション取得待ち時間の監視

- **N+1 問題の解決**:
  - `Include()` / `ThenInclude()` による Eager Loading の適切な使用
  - `AsSplitQuery()` によるクエリ分割
  - `AsNoTracking()` による読み取り専用クエリの高速化
  - バッチ取得サイズの最適化

- **トランザクション管理**:
  - 適切なトランザクション境界の設定
  - トランザクション分離レベルの最適化
  - 長時間トランザクションの回避
  - 楽観的ロック（`[Timestamp]`）の活用

- **クエリ最適化**:
  - 不要なデータ取得の回避（`Select()` で必要なカラムのみ取得）
  - ページネーションの実装（オフセットベース / キーセット）
  - クエリキャッシュの検討
  - `AsNoTracking()` の積極的使用

### キャッシュ戦略

- **多層キャッシュ**:
  - HTTP レベル（ブラウザキャッシュ、CDN）
  - アプリケーションレベル（メソッドレベル、オブジェクトレベル）
  - データベースレベル（クエリキャッシュ、結果セットキャッシュ）

- **キャッシュ技術の選定**:
  - インメモリキャッシュ（`IMemoryCache`）
  - 分散キャッシュ（Redis: `IDistributedCache`, StackExchange.Redis）
  - ニアキャッシュとリモートキャッシュの併用

- **キャッシュ更新戦略**:
  - 時間ベースの無効化（TTL: Time To Live）
  - 明示的な無効化（CUD 操作時）
  - イベント駆動の無効化（pub/sub）
  - Write-Through / Write-Behind パターン

- **キャッシュの監視と管理**:
  - ヒット率/ミス率の監視
  - メモリ使用量の追跡
  - エビクションポリシーの最適化
  - キャッシュウォームアップ戦略

```csharp
// キャッシュ設定の例 — Redis 分散キャッシュと IMemoryCache の併用
// Program.cs
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "SkiShop:";
});
builder.Services.AddMemoryCache();

// Service でのキャッシュ利用
public class ProductService(
    IProductRepository productRepository,
    IDistributedCache distributedCache,
    IMemoryCache memoryCache,
    ILogger<ProductService> logger) : IProductService
{
    private static readonly TimeSpan ProductCacheDuration = TimeSpan.FromHours(1);
    private static readonly TimeSpan PriceCacheDuration = TimeSpan.FromMinutes(5);

    public async Task<ProductDto?> GetProductByIdAsync(string id, CancellationToken ct = default)
    {
        // まずメモリキャッシュを確認
        var cacheKey = $"product:{id}";
        if (memoryCache.TryGetValue(cacheKey, out ProductDto? cached))
            return cached;

        // 分散キャッシュ（Redis）を確認
        var redisValue = await distributedCache.GetStringAsync(cacheKey, ct);
        if (redisValue is not null)
        {
            var product = JsonSerializer.Deserialize<ProductDto>(redisValue);
            memoryCache.Set(cacheKey, product, TimeSpan.FromMinutes(5));
            return product;
        }

        // DB から取得
        var entity = await productRepository.FindByIdAsync(id, ct)
            ?? throw new NotFoundException($"商品（ID: {id}）が見つかりません");

        var dto = new ProductDto(entity.Id, entity.Name, entity.Price, entity.Category);

        // キャッシュに保存
        await distributedCache.SetStringAsync(cacheKey,
            JsonSerializer.Serialize(dto),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ProductCacheDuration },
            ct);
        memoryCache.Set(cacheKey, dto, TimeSpan.FromMinutes(5));

        return dto;
    }

    public async Task<ProductDto> UpdateProductAsync(string id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var product = await productRepository.FindByIdAsync(id, ct)
            ?? throw new NotFoundException($"商品（ID: {id}）が見つかりません");

        product.Name = request.Name;
        product.Price = request.Price;
        product.UpdatedAt = DateTime.UtcNow;

        await productRepository.SaveChangesAsync(ct);

        // キャッシュを無効化
        var cacheKey = $"product:{id}";
        await distributedCache.RemoveAsync(cacheKey, ct);
        memoryCache.Remove(cacheKey);

        return new ProductDto(product.Id, product.Name, product.Price, product.Category);
    }
}
```

### 非同期処理とバッチ処理

- **C# 非同期プログラミング**:
  - `async` / `await` パターンの効果的な活用
  - `Task.WhenAll` / `Task.WhenAny` による並列処理
  - `Channel<T>` によるプロデューサー/コンシューマーパターン
  - `CancellationToken` の適切な伝搬

- **バックグラウンド処理**:
  - `BackgroundService` / `IHostedService` による長時間処理
  - `IServiceScopeFactory` によるスコープ管理
  - グレースフルシャットダウンの実装

- **メッセージングシステム統合**:
  - Apache Kafka による非同期メッセージング（Confluent.Kafka）
  - 永続的メッセージ配信
  - デッドレターキューの実装
  - 冪等性の確保

```csharp
// 非同期処理の例（Task.WhenAll による並列実行）
public class OrderProcessingService(
    IInventoryService inventoryService,
    IPaymentService paymentService,
    IShippingService shippingService,
    INotificationService notificationService,
    ILogger<OrderProcessingService> logger) : IOrderProcessingService
{
    public async Task<OrderResult> ProcessOrderAsync(Order order, CancellationToken ct = default)
    {
        // 在庫確認と決済処理を並列実行
        var inventoryTask = inventoryService.CheckInventoryAsync(order.Items, ct);
        var paymentTask = paymentService.ProcessPaymentAsync(order, ct);

        await Task.WhenAll(inventoryTask, paymentTask);

        var inventoryResult = await inventoryTask;
        var paymentResult = await paymentTask;

        if (!inventoryResult.IsAvailable)
            throw new BusinessException("在庫が不足しています");

        if (!paymentResult.IsSuccessful)
            throw new BusinessException($"決済処理に失敗しました: {paymentResult.ErrorCode}");

        // 配送手配
        var shippingResult = await shippingService.ArrangeShippingAsync(order, ct);

        // 通知を非同期で送信（結果を待たない）
        _ = Task.Run(() => notificationService.SendOrderConfirmationAsync(order, shippingResult, ct), ct);

        return new OrderResult(order.Id, OrderStatus.Processing,
            shippingResult.TrackingNumber, shippingResult.EstimatedDelivery);
    }
}

// BackgroundService によるバッチ処理の例（Outbox Publisher）
public class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var pendingEvents = await context.OutboxEvents
                .Where(e => e.PublishedAt == null)
                .OrderBy(e => e.CreatedAt)
                .Take(100)
                .ToListAsync(stoppingToken);

            foreach (var evt in pendingEvents)
            {
                try
                {
                    await producer.ProduceAsync(evt.EventType, new Message<string, string>
                    {
                        Key = evt.Id,
                        Value = evt.Payload
                    }, stoppingToken);

                    evt.PublishedAt = DateTime.UtcNow;
                    logger.LogInformation("Outbox イベント発行成功: {EventType}, {EventId}", evt.EventType, evt.Id);
                }
                catch (ProduceException<string, string> ex)
                {
                    logger.LogError(ex, "Outbox イベント発行失敗: {EventType}, {EventId}", evt.EventType, evt.Id);
                }
            }

            await context.SaveChangesAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
```

### スケーラビリティ

- **水平スケーリング設計**:
  - ステートレスアーキテクチャの徹底
  - サービスインスタンスの動的スケーリング
  - アプリケーション起動時間の最適化
  - 負荷分散アルゴリズムの検討（ラウンドロビン、最小接続数、リソースベース）

- **分散セッション管理**:
  - Redis によるセッション共有（StackExchange.Redis）
  - JWT によるステートレス認証
  - セッションレプリケーション戦略

- **コンテナオーケストレーション**:
  - .NET Aspire 13.1 によるサービスオーケストレーション
  - 適切なリソースリクエストとリミットの設定
  - ローリングアップデート戦略

- **データベーススケーリング**:
  - リードレプリカの活用
  - シャーディング戦略（水平パーティショニング）
  - データベースコネクションプールの適切なサイジング

## テスト戦略

### ユニットテスト

- **テストフレームワークと補助ツール**:
  - xUnit の活用（`[Fact]`, `[Theory]`, `[InlineData]`）
  - NSubstitute による高度なモック機能
  - Shouldly による可読性の高いアサーション
  - テストデータビルダーパターンの採用

- **テスト設計手法**:
  - 境界値分析と同値分割
  - エラーケースと例外テスト
  - パラメータ化テストによる多様なシナリオ
  - テスト駆動開発（TDD）の選択的導入

- **テストカバレッジ**:
  - 行カバレッジだけでなく分岐カバレッジの計測
  - ミューテーションテストによるテスト品質評価
  - 重要なビジネスロジックへの集中的なテスト
  - coverlet と CI の統合

- **開発者体験**:
  - 高速なテスト実行（テスト分離）
  - テスト信頼性の向上（フレーキーテストの排除）
  - テスト文化の醸成（ペアプログラミング、コードレビュー）

```csharp
// xUnit と NSubstitute を使用した包括的なユニットテストの例
public class OrderServiceTest
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPaymentService _paymentService;
    private readonly IInventoryService _inventoryService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<OrderService> _logger;
    private readonly OrderService _orderService;

    public OrderServiceTest()
    {
        _orderRepository = Substitute.For<IOrderRepository>();
        _paymentService = Substitute.For<IPaymentService>();
        _inventoryService = Substitute.For<IInventoryService>();
        _notificationService = Substitute.For<INotificationService>();
        _logger = Substitute.For<ILogger<OrderService>>();

        _orderService = new OrderService(
            _orderRepository,
            _paymentService,
            _inventoryService,
            _notificationService,
            _logger);
    }

    public class CreateOrderTests : OrderServiceTest
    {
        [Fact]
        public async Task Should_CreateOrder_When_ValidRequestProvided()
        {
            // Arrange
            var request = new CreateOrderRequest(
                CustomerId: "customer-1",
                Items: [new OrderItemRequest("product-101", 2), new OrderItemRequest("product-102", 1)],
                PaymentMethod: PaymentMethod.CreditCard);

            _inventoryService.CheckAvailabilityAsync(Arg.Any<IEnumerable<OrderItemRequest>>(), default)
                .Returns(true);
            _orderRepository.AddAsync(Arg.Any<Order>(), default)
                .Returns(Task.CompletedTask);
            _orderRepository.SaveChangesAsync(default)
                .Returns(Task.CompletedTask);
            _paymentService.ProcessPaymentAsync(Arg.Any<PaymentRequest>(), default)
                .Returns(new PaymentResult(true, "PAYMENT-123", null));

            // Act
            var result = await _orderService.CreateOrderAsync(request);

            // Assert
            result.ShouldNotBeNull();
            result.IsSuccessful.ShouldBeTrue();

            // 依存サービスの呼び出し検証
            await _inventoryService.Received(1).CheckAvailabilityAsync(Arg.Any<IEnumerable<OrderItemRequest>>(), default);
            await _orderRepository.Received(1).AddAsync(Arg.Any<Order>(), default);
            await _paymentService.Received(1).ProcessPaymentAsync(Arg.Any<PaymentRequest>(), default);
        }

        [Fact]
        public async Task Should_ThrowBusinessException_When_InsufficientInventory()
        {
            // Arrange
            var request = new CreateOrderRequest(
                CustomerId: "customer-1",
                Items: [new OrderItemRequest("product-101", 100)],
                PaymentMethod: PaymentMethod.CreditCard);

            _inventoryService.CheckAvailabilityAsync(Arg.Any<IEnumerable<OrderItemRequest>>(), default)
                .Returns(false);

            // Act & Assert
            var ex = await Should.ThrowAsync<BusinessException>(
                () => _orderService.CreateOrderAsync(request));
            ex.Message.ShouldContain("在庫不足");

            // 在庫不足の場合は注文保存・決済処理が行われないことを検証
            await _orderRepository.DidNotReceive().AddAsync(Arg.Any<Order>(), default);
            await _paymentService.DidNotReceive().ProcessPaymentAsync(Arg.Any<PaymentRequest>(), default);
            await _notificationService.DidNotReceive().SendOrderConfirmationAsync(Arg.Any<Order>(), default);
        }

        [Theory]
        [InlineData(0, "1以上")]
        [InlineData(-1, "1以上")]
        public async Task Should_ThrowValidationException_When_InvalidQuantity(int quantity, string expectedMessage)
        {
            // Arrange
            var request = new CreateOrderRequest(
                CustomerId: "customer-1",
                Items: [new OrderItemRequest("product-101", quantity)],
                PaymentMethod: PaymentMethod.CreditCard);

            // Act & Assert
            var ex = await Should.ThrowAsync<BusinessException>(
                () => _orderService.CreateOrderAsync(request));
            ex.Message.ShouldContain(expectedMessage);
        }
    }
}
```

### 統合テスト

- **テスト環境の整備**:
  - `WebApplicationFactory<Program>` による統合テスト
  - Testcontainers.PostgreSql による一時的なインフラ提供（DB）
  - テストプロファイルと環境固有の設定
  - テストデータ準備とクリーンアップの自動化

- **統合テスト範囲**:
  - データアクセス層テスト（Repository テスト）
  - エンドポイントテスト（`HttpClient` による API テスト）
  - 外部サービス連携テスト
  - 非同期処理/イベント処理テスト

```csharp
// WebApplicationFactory と Testcontainers を使用した統合テスト例
public class OrderRepositoryIntegrationTest : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithDatabase("testdb")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private AppDbContext _context = null!;

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        // テストデータ準備
        var customer = new Customer { Name = "テスト顧客", Email = "customer@example.com" };
        _context.Customers.Add(customer);

        var product1 = new Product { Name = "商品 1", Price = 10.99m };
        var product2 = new Product { Name = "商品 2", Price = 20.50m };
        _context.Products.AddRange(product1, product2);

        await _context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _postgresContainer.DisposeAsync();
    }

    [Fact]
    public async Task Should_SaveAndFindOrder()
    {
        // Arrange
        var customer = await _context.Customers.FirstAsync();
        var products = await _context.Products.ToListAsync();

        var order = new Order
        {
            CustomerId = customer.Id,
            Status = OrderStatus.Created,
            OrderDate = DateTime.UtcNow,
            Items =
            [
                new OrderItem { ProductId = products[0].Id, Quantity = 2, Price = products[0].Price },
                new OrderItem { ProductId = products[1].Id, Quantity = 1, Price = products[1].Price }
            ]
        };

        // Act
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var foundOrder = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == order.Id);

        // Assert
        foundOrder.ShouldNotBeNull();
        foundOrder.CustomerId.ShouldBe(customer.Id);
        foundOrder.Status.ShouldBe(OrderStatus.Created);
        foundOrder.Items.Count.ShouldBe(2);

        // 注文金額の計算が正しいことを検証
        var expectedTotal = 10.99m * 2 + 20.50m; // 42.48
        foundOrder.TotalAmount.ShouldBe(expectedTotal);
    }

    [Fact]
    public async Task Should_FindOrdersByCustomerId()
    {
        // Arrange
        var customer = await _context.Customers.FirstAsync();

        var order1 = new Order
        {
            CustomerId = customer.Id,
            Status = OrderStatus.Delivered,
            OrderDate = DateTime.UtcNow.AddDays(-10)
        };

        var order2 = new Order
        {
            CustomerId = customer.Id,
            Status = OrderStatus.Processing,
            OrderDate = DateTime.UtcNow.AddDays(-5)
        };

        var otherCustomer = new Customer { Name = "別の顧客", Email = "other@example.com" };
        _context.Customers.Add(otherCustomer);
        await _context.SaveChangesAsync();

        var order3 = new Order
        {
            CustomerId = otherCustomer.Id,
            Status = OrderStatus.Created,
            OrderDate = DateTime.UtcNow
        };

        _context.Orders.AddRange(order1, order2, order3);
        await _context.SaveChangesAsync();

        // Act
        var customerOrders = await _context.Orders
            .Where(o => o.CustomerId == customer.Id)
            .ToListAsync();

        // Assert
        customerOrders.Count.ShouldBe(2);
        customerOrders.ShouldAllBe(o => o.CustomerId == customer.Id);
    }
}
```

### パフォーマンステスト

- **パフォーマンスの種類**:
  - 負荷テスト（Load Testing）: 一定量の負荷下での動作検証
  - ストレステスト（Stress Testing）: 限界点の発見
  - 耐久テスト（Endurance Testing）: 長時間実行時の安定性
  - スパイクテスト（Spike Testing）: 急激な負荷変動への対応

- **テストツール**:
  - k6 によるモダンな JavaScript ベースの負荷テスト
  - NBomber による .NET ネイティブの負荷テスト
  - 分散負荷テスト

- **パフォーマンス指標**:
  - レスポンスタイム（平均、90 パーセンタイル、99 パーセンタイル）
  - スループット（RPS/TPS）
  - エラー率
  - リソース使用率（CPU, メモリ, ディスク I/O, ネットワーク）

- **テスト環境と実施**:
  - 本番に近い環境での実施
  - CI/CD パイプラインへの組み込み
  - ベースラインとの比較分析
  - ボトルネック特定と改善サイクル

```javascript
// k6 を使用したパフォーマンステストの例
import http from 'k6/http';
import { check, sleep } from 'k6';

// 負荷プロファイル定義
export const options = {
  stages: [
    // 通常負荷テスト: 5分間かけて100ユーザーまでランプアップし、10分間維持
    { duration: '5m', target: 100 },
    { duration: '10m', target: 100 },
    { duration: '2m', target: 0 },
  ],
  thresholds: {
    // 成功率が99.9%以上
    http_req_failed: ['rate<0.001'],
    // 95%のリクエストが1秒以内に完了
    http_req_duration: ['p(95)<1000', 'p(99)<2000'],
  },
};

const BASE_URL = 'https://api.example.com';

export default function () {
  // 商品詳細取得
  const productRes = http.get(`${BASE_URL}/products/1`, {
    headers: { Authorization: `Bearer ${__ENV.TOKEN}` },
  });
  check(productRes, { '商品取得成功': (r) => r.status === 200 });

  sleep(Math.random() * 3 + 1);

  // カートに追加
  const cartRes = http.post(`${BASE_URL}/cart/items`,
    JSON.stringify({ productId: '1', quantity: 1 }),
    { headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${__ENV.TOKEN}` } }
  );
  check(cartRes, { 'カート追加成功': (r) => r.status === 200 || r.status === 201 });

  sleep(Math.random() * 5 + 2);

  // 注文作成
  const orderRes = http.post(`${BASE_URL}/orders`,
    JSON.stringify({
      paymentMethod: 'CREDIT_CARD',
      shippingAddress: {
        street: '123 テスト通り',
        city: 'テスト市',
        country: '日本',
        postalCode: '123-4567',
      },
    }),
    { headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${__ENV.TOKEN}` } }
  );
  check(orderRes, { '注文作成成功': (r) => r.status === 201 });

  sleep(Math.random() * 2 + 1);
}
```

### 継続的インテグレーション

- **CI 環境構築**:
  - GitHub Actions による CI/CD パイプラインの設計
  - マルチステージパイプライン（ビルド、テスト、静的解析、セキュリティスキャン）
  - パイプラインの並列化と最適化

- **コード品質ゲート**:
  - `dotnet format` によるコードフォーマット検証
  - Roslyn アナライザーによる静的解析
  - Dependabot / `dotnet list package --vulnerable` による依存関係の脆弱性チェック
  - ブランチ保護と PR の自動レビュー

- **テスト自動化**:
  - ユニットテストの並列実行
  - ピラミッド型テスト戦略（Unit > Integration > E2E）
  - フレーキーテストの特定と修正
  - テスト結果の可視化と通知

- **開発プラクティス**:
  - トランクベース開発の検討
  - フィーチャーフラグの活用
  - テスト駆動開発（TDD）の選択的導入
  - ビヘイビア駆動開発（BDD）による要件と実装の整合性確保

```yaml
# GitHub Actions のワークフロー例
name: .NET CI/CD Pipeline

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]

jobs:
  build-and-test:
    runs-on: ubuntu-latest

    services:
      postgres:
        image: postgres:15
        env:
          POSTGRES_USER: test
          POSTGRES_PASSWORD: test
          POSTGRES_DB: testdb
        ports:
          - 5432:5432
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

      redis:
        image: redis:7
        ports:
          - 6379:6379
        options: >-
          --health-cmd "redis-cli ping"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

    steps:
    - uses: actions/checkout@v4

    - name: .NET SDK セットアップ
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'

    - name: 依存関係の復元
      run: dotnet restore

    - name: ビルド
      run: dotnet build --no-restore --configuration Release

    - name: テスト実行
      run: dotnet test --no-build --configuration Release --collect:"XPlat Code Coverage"

    - name: カバレッジレポート生成
      uses: danielpalme/ReportGenerator-GitHub-Action@5
      with:
        reports: '**/coverage.cobertura.xml'
        targetdir: 'coveragereport'

    - name: テスト結果アップロード
      if: always()
      uses: actions/upload-artifact@v4
      with:
        name: test-results
        path: '**/TestResults/**'

    - name: カバレッジレポートアップロード
      uses: actions/upload-artifact@v4
      with:
        name: coverage-report
        path: coveragereport

  security-scan:
    runs-on: ubuntu-latest
    needs: build-and-test

    steps:
    - uses: actions/checkout@v4

    - name: .NET SDK セットアップ
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'

    - name: 脆弱性チェック
      run: dotnet list package --vulnerable --include-transitive

  build-docker:
    runs-on: ubuntu-latest
    needs: [build-and-test, security-scan]
    if: github.ref == 'refs/heads/main'

    steps:
    - uses: actions/checkout@v4

    - name: Docker Buildx セットアップ
      uses: docker/setup-buildx-action@v3

    - name: DockerHub ログイン
      uses: docker/login-action@v3
      with:
        username: ${{ secrets.DOCKERHUB_USERNAME }}
        password: ${{ secrets.DOCKERHUB_TOKEN }}

    - name: ビルドとプッシュ
      uses: docker/build-push-action@v5
      with:
        context: .
        push: true
        tags: myorg/myapp:latest,myorg/myapp:${{ github.sha }}
        cache-from: type=registry,ref=myorg/myapp:buildcache
        cache-to: type=registry,ref=myorg/myapp:buildcache,mode=max
```

## デプロイメントと運用

### コンテナ化戦略

- **Docker イメージ最適化**:
  - マルチステージビルドによるイメージサイズの最小化
  - ベースイメージの選択（SDK 版、Runtime 版）とトレードオフ
  - レイヤーキャッシュの効率的活用
  - セキュリティスキャン（Docker Scout, Trivy）の組み込み

- **オーケストレーション**:
  - .NET Aspire 13.1 によるサービス管理
  - ヘルスチェックとグレースフルシャットダウン
  - サービスディスカバリの自動化

- **イミュータブルインフラストラクチャ**:
  - 実行環境の一貫性確保
  - インフラストラクチャのバージョン管理（GitOps）
  - 自己修復性と水平スケーリングの自動化
  - コンテナライフサイクル管理

```dockerfile
# 最適化された Dockerfile の例
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ServiceName/ServiceName.csproj", "ServiceName/"]
RUN dotnet restore "ServiceName/ServiceName.csproj"
COPY . .
WORKDIR "/src/ServiceName"
RUN dotnet publish "ServiceName.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# セキュリティ強化: 非 root ユーザーで実行
RUN groupadd -r skishop && useradd -r -g skishop -d /app skishop
COPY --from=build --chown=skishop:skishop /app/publish .
USER skishop

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

# ヘルスチェックの定義
HEALTHCHECK --interval=30s --timeout=10s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "ServiceName.dll"]
```

### 可観測性（監視、ロギング、トレーシング）

- **構造化ロギング**:
  - Serilog + ILogger<T> による JSON 形式ログ
  - 相関 ID（Correlation ID）による分散トレース伝搬
  - ログレベルの適切な使い分け（Debug, Information, Warning, Error）
  - 機密情報のマスキングとコンプライアンス対応

- **メトリクス収集**:
  - OpenTelemetry によるアプリケーションメトリクス収集
  - ASP.NET Core HealthChecks によるヘルスチェックと運用データ公開
  - カスタムメトリクスの定義（ビジネス指標、トランザクション指標）
  - アラートルールとダッシュボード設計

- **分散トレーシング**:
  - OpenTelemetry による標準化されたトレース収集
  - サービス間通信の可視化
  - トレースコンテキスト伝播（HTTP ヘッダー、メッセージング）
  - パフォーマンスボトルネックの特定とトラブルシューティング

- **統合監視基盤**:
  - OpenTelemetry によるメトリクス・トレース・ログの統合
  - .NET Aspire ダッシュボードによるサービス可視化
  - アラート一元管理とインシデント対応プロセス

```csharp
// 構造化ロギングとメトリクス連携の例
public class OrderService(
    IOrderRepository orderRepository,
    IPaymentService paymentService,
    IInventoryService inventoryService,
    ILogger<OrderService> logger) : IOrderService
{
    private static readonly Counter<long> OrdersCreatedCounter =
        new Meter("SkiShop.Orders").CreateCounter<long>("orders.created.total", description: "注文作成数");
    private static readonly Histogram<double> OrderCreationDuration =
        new Meter("SkiShop.Orders").CreateHistogram<double>("orders.creation.duration", "ms", "注文作成所要時間");

    public async Task<OrderResult> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("注文作成開始: CustomerId={CustomerId}, ItemsCount={ItemsCount}",
            request.CustomerId, request.Items.Count);

        try
        {
            // 在庫確認
            var stockAvailable = await inventoryService.CheckAvailabilityAsync(request.Items, ct);
            if (!stockAvailable)
            {
                logger.LogWarning("注文作成失敗 - 在庫不足: CustomerId={CustomerId}", request.CustomerId);
                throw new BusinessException("商品の在庫が不足しています");
            }

            // 注文保存
            var order = MapToOrder(request);
            await orderRepository.AddAsync(order, ct);

            // 決済処理
            var paymentResult = await paymentService.ProcessPaymentAsync(
                new PaymentRequest(order.Id, order.TotalAmount), ct);

            if (!paymentResult.IsSuccessful)
            {
                logger.LogError("注文作成失敗 - 決済エラー: CustomerId={CustomerId}, ErrorCode={ErrorCode}",
                    request.CustomerId, paymentResult.ErrorCode);
                throw new BusinessException($"決済処理に失敗しました: {paymentResult.ErrorMessage}");
            }

            // 注文確定
            order.Status = OrderStatus.Confirmed;
            order.PaymentId = paymentResult.TransactionId;
            await orderRepository.SaveChangesAsync(ct);

            // メトリクス記録
            OrdersCreatedCounter.Add(1);

            stopwatch.Stop();
            OrderCreationDuration.Record(stopwatch.ElapsedMilliseconds);

            logger.LogInformation("注文作成完了: OrderId={OrderId}, CustomerId={CustomerId}, Status=Confirmed",
                order.Id, request.CustomerId);

            return new OrderResult(order.Id, OrderStatus.Confirmed, paymentResult.TransactionId);
        }
        catch (Exception ex) when (ex is not BusinessException)
        {
            stopwatch.Stop();
            OrderCreationDuration.Record(stopwatch.ElapsedMilliseconds);

            logger.LogError(ex, "注文作成中に予期しないエラーが発生: {Message}", ex.Message);
            throw;
        }
    }
}
```

### CI/CD パイプライン

- **パイプライン設計原則**:
  - 段階的検証（ビルド → テスト → 静的解析 → セキュリティスキャン → デプロイ）
  - 環境分離（開発 → テスト → ステージング → 本番）
  - イミュータブルアーティファクト（同一バイナリの昇格）
  - 品質ゲートによる自動昇格・却下

- **デプロイメント戦略**:
  - ブルー/グリーンデプロイメント（ダウンタイムゼロ）
  - カナリアリリース（制限された利用者へのリリース）
  - A/B テスト（機能の段階的展開）
  - フィーチャーフラグによる機能の選択的有効化

- **自動化とツール連携**:
  - GitHub Actions によるパイプライン自動化
  - Terraform / Bicep / ARM テンプレートによる IaC の自動適用
  - 障害検知と自動ロールバック機構

- **リリース管理**:
  - リリースノートの自動生成
  - 変更履歴の追跡と監査
  - バージョン管理（セマンティックバージョニング）
  - リリース承認ワークフロー

### 災害復旧（DR）と事業継続計画（BCP）

- **バックアップ戦略**:
  - データバックアップの種類と頻度（フル、増分、差分）
  - 自動化されたバックアップ検証（リストア演習）
  - バックアップの暗号化と保管（オフサイト保管）
  - 世代管理と保持ポリシー

- **災害復旧計画**:
  - 復旧時間目標（RTO）と復旧ポイント目標（RPO）の設定
  - フェイルオーバー手順とフォールバック計画
  - 障害シナリオ別の対応手順
  - 定期的な災害復旧訓練

- **高可用性設計**:
  - 地理的冗長性（マルチリージョン、マルチ AZ 配置）
  - アクティブ/アクティブまたはアクティブ/パッシブ構成
  - データレプリケーション戦略
  - グローバルロードバランシングとトラフィック分散

- **インシデント対応**:
  - エスカレーションプロセスとコミュニケーションプラン
  - 障害の検出と初動対応の自動化
  - 障害レポートと根本原因分析（RCA）
  - ポストモーテムと再発防止策

## 非機能要件

マイクロサービスアーキテクチャにおいて、非機能要件は機能要件と同等に重要である。以下にエンタープライズ .NET アプリケーションの主要な非機能要件を詳述する。

### パフォーマンス要件

- **レスポンスタイム**:
  - API エンドポイントの最大応答時間（例: 95 パーセンタイルで 500ms 以内）
  - バッチ処理の完了時間制約
  - ユーザーインターフェース操作の応答時間（例: ページロード 2 秒以内）
  - サービス間通信のタイムアウト設定（例: 同期呼び出し 1 秒、非同期処理 5 秒）

- **スループット**:
  - ピーク時の毎秒トランザクション数（TPS）
  - 同時ユーザー数（並行セッション）
  - バルク処理の処理速度（例: 1 時間あたり 100 万レコード）
  - メッセージング処理能力（例: 1 秒あたり 10,000 メッセージ）

- **リソース使用効率**:
  - CPU 使用率の上限（例: 通常時 70% 以下、ピーク時 90% 以下）
  - メモリ使用量の制限と最適化
  - ディスク I/O 最適化（特にデータベース操作）
  - ネットワーク帯域幅の効率的利用

```csharp
// パフォーマンス最適化の例: N+1 問題の解決
public class OrderRepository(AppDbContext context) : IOrderRepository
{
    // ❌ N+1 問題が発生する実装
    public async Task<List<Order>> FindAllOrdersInefficientAsync(CancellationToken ct = default)
    {
        var orders = await context.Orders.ToListAsync(ct);

        // 各注文ごとに個別クエリが発生（N+1 問題）
        foreach (var order in orders)
        {
            _ = order.Items.Count; // 遅延ロードによる追加クエリ
        }

        return orders;
    }

    // ✅ 最適化された実装: Include による N+1 問題の解決
    public async Task<List<Order>> FindAllOrdersOptimizedAsync(CancellationToken ct = default)
        => await context.Orders
            .Include(o => o.Items)
            .AsNoTracking()
            .ToListAsync(ct);

    // ✅ AsSplitQuery による別の解決法（大量データ時のメモリ効率向上）
    public async Task<List<Order>> FindAllOrdersWithSplitQueryAsync(CancellationToken ct = default)
        => await context.Orders
            .Include(o => o.Items)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(ct);
}
```

### スケーラビリティ要件

- **水平スケーラビリティ**:
  - 無停止でのインスタンス追加・削除
  - 自動スケーリングポリシー（CPU 使用率、メモリ使用率、リクエスト数に基づく）
  - ステートレス設計による拡張性確保
  - キャッシュの分散と一貫性確保

- **垂直スケーラビリティ**:
  - リソース増強時の動作保証
  - スケールアップ時の性能向上比率の目標
  - 限界値の明確化

- **負荷分散**:
  - .NET Aspire によるサービスディスカバリーと負荷分散
  - セッションアフィニティ要件
  - ジオロケーションベースのルーティング
  - グローバル分散時のレイテンシ要件

### 可用性要件

- **稼働率（SLA）**:
  - サービスレベル目標（例: 99.95% の稼働率、月間ダウンタイム 22 分以内）
  - 計画メンテナンス時間の取り扱い
  - 各コンポーネントの可用性要件と全体 SLA の関係

- **フォールトトレランス**:
  - 単一障害点（SPOF）の排除
  - サーキットブレーカーパターンの適用（Polly）
  - デグラデーションモード（限定機能での継続運用）
  - リトライポリシーとバックオフ戦略

- **障害検知と復旧**:
  - ヘルスチェックの種類と頻度
  - 自動復旧メカニズム
  - フェイルオーバー時間制約
  - ディザスタリカバリ（DR）要件

```csharp
// サーキットブレーカーパターンの実装例（Polly + IHttpClientFactory）
// Program.cs での HttpClient 登録
builder.Services.AddHttpClient<IPaymentClient, PaymentClient>(client =>
{
    client.BaseAddress = new Uri("https://payment-service");
})
.AddStandardResilienceHandler(options =>
{
    // リトライ: 指数バックオフ（最大 3 回）
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.Delay = TimeSpan.FromMilliseconds(500);

    // サーキットブレーカー: 30 秒の遮断期間
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.MinimumThroughput = 10;

    // タイムアウト: 10 秒
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
});

// 決済サービスクライアントの実装
public class PaymentClient(HttpClient httpClient, ILogger<PaymentClient> logger) : IPaymentClient
{
    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken ct = default)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("/api/payments", request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<PaymentResult>(ct)
                ?? throw new BusinessException("決済レスポンスの解析に失敗しました");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        {
            logger.LogWarning("決済サービスが利用不可。フォールバック処理を実行します");
            return new PaymentResult(false, null, "SERVICE_UNAVAILABLE",
                "現在決済サービスが利用できません。後ほど再試行します。");
        }
    }
}
```

### セキュリティ要件

- **認証・認可**:
  - 認証方式（OpenID Connect, JWT）
  - 多要素認証（MFA）要件
  - ロールベース/属性ベースのアクセス制御（RBAC/ABAC）
  - セッション管理ポリシー（タイムアウト、同時セッション数）

- **データ保護**:
  - 保存データの暗号化要件（アルゴリズム、鍵管理）
  - 転送中データの暗号化（TLS 1.3, mTLS）
  - 個人情報保護対策（匿名化、仮名化）
  - 機密データの取り扱いポリシー

- **監査とコンプライアンス**:
  - 監査ログの要件（何を、どのように、どれだけの期間）
  - 法規制対応（GDPR, PCI DSS, HIPAA 等）
  - 脆弱性管理プロセス
  - ペネトレーションテスト頻度と範囲

- **侵入検知と防御**:
  - 入力検証ポリシー
  - OWASP Top 10 対策
  - API セキュリティ（レート制限、トークン検証）
  - マルウェア/ウイルス対策

### 運用・保守性要件

- **監視・観測性**:
  - 必須モニタリング指標と閾値
  - アラート設定と通知ルート
  - ロギング要件（フォーマット、詳細度、保持期間）
  - 分散トレーシング要件

- **デプロイメント**:
  - デプロイ頻度と所要時間
  - ゼロダウンタイムデプロイ要件
  - ロールバック要件と復旧時間
  - カナリアリリース、フィーチャーフラグ要件

- **構成管理**:
  - 環境間での設定差異管理
  - シークレット管理（`dotnet user-secrets`、Azure Key Vault）
  - 構成変更の追跡と監査
  - A/B テスト機能

- **バックアップと復元**:
  - バックアップ種類と頻度
  - 復元時間目標（RTO）
  - 復元ポイント目標（RPO）
  - バックアップ検証プロセス

### データ管理要件

- **データ永続性**:
  - データ保持ポリシー
  - バックアップと復元の要件
  - アーカイブ戦略
  - データ冗長性レベル

- **データ整合性**:
  - トランザクション境界と分離レベル
  - 最終的整合性の許容範囲
  - イベント整合性の保証
  - 楽観的/悲観的ロックの要件

- **データボリューム**:
  - 想定データ量と成長率
  - クエリパフォーマンス要件
  - データパーティショニング戦略
  - コールドデータ/ホットデータの区分管理

- **データ品質**:
  - データ検証ルール
  - マスターデータ管理
  - データクレンジング要件
  - データ移行・変換要件

### ユーザビリティ要件

- **ユーザーインターフェース**:
  - レスポンシブデザイン要件
  - アクセシビリティ基準（WCAG 準拠レベル）
  - 多言語対応
  - 異なるデバイスでの動作保証

- **ユーザー体験**:
  - エラーメッセージの親切さ
  - 操作ステップ数の制限
  - ヘルプ・ガイダンス機能
  - 一貫性のある UI/UX

### ローカライゼーション要件

- **国際化対応**:
  - サポートする言語・地域
  - 文字セット・エンコーディング
  - タイムゾーン処理
  - 数値・日付・通貨のフォーマット

- **地域固有の要件**:
  - 法的規制対応
  - 文化的配慮
  - 地域別のビジネスルール
  - 地域別のデータ保持ポリシー

## ベストプラクティス

### コーディング規約

- C# コーディング規約（PascalCase / camelCase / `_camelCase`）の採用
- Roslyn アナライザー、`dotnet format` による静的解析の活用
- コードレビュープロセスの確立

### ドキュメンテーション

- **API ドキュメント**:
  - `WithOpenApi()` による REST API 仕様の自動生成
  - XML ドキュメントコメントによる API 文書化
  - API の使用例とサンプルコード
  - 利用者ガイドとチュートリアル

- **アーキテクチャドキュメント**:
  - C4 モデルによる多層的なアーキテクチャ文書（コンテキスト、コンテナ、コンポーネント、コード）
  - ADR（Architecture Decision Records）による意思決定の記録
  - システムコンテキスト図とデータフロー図
  - 非機能要件のマッピングと検証方法

- **運用ドキュメント**:
  - 環境構築手順書
  - デプロイメントプロセス
  - 監視設定とアラート対応
  - インシデント対応手順

- **開発プロセス文書**:
  - コーディング規約
  - レビュープロセス
  - ブランチ戦略とリリースフロー
  - テスト戦略と品質基準

```csharp
/// <summary>
/// 注文サービスのインターフェース。
/// <para>
/// このサービスは注文のライフサイクル全体を管理し、以下の機能を提供します：
/// <list type="bullet">
///   <item>注文の作成と検証</item>
///   <item>決済処理の統合</item>
///   <item>在庫確認と予約</item>
///   <item>注文状態の追跡</item>
/// </list>
/// </para>
/// </summary>
public interface IOrderService
{
    /// <summary>
    /// 新しい注文を作成します。
    /// <para>
    /// このメソッドは以下のステップを実行します：
    /// <list type="number">
    ///   <item>注文リクエストの検証</item>
    ///   <item>在庫の確認</item>
    ///   <item>決済処理</item>
    ///   <item>注文の永続化</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="request">注文作成リクエスト（必須）</param>
    /// <param name="ct">キャンセレーショントークン</param>
    /// <returns>作成された注文の結果</returns>
    /// <exception cref="BusinessException">注文リクエストが無効な場合</exception>
    /// <exception cref="BusinessException">在庫が不足している場合</exception>
    /// <exception cref="BusinessException">決済処理に失敗した場合</exception>
    Task<OrderResult> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default);

    /// <summary>
    /// 注文 ID に基づいて注文を取得します。
    /// </summary>
    /// <param name="orderId">取得する注文の ID</param>
    /// <param name="ct">キャンセレーショントークン</param>
    /// <returns>見つかった注文、存在しない場合は例外をスロー</returns>
    /// <exception cref="NotFoundException">指定された ID の注文が存在しない場合</exception>
    Task<Order> GetOrderByIdAsync(string orderId, CancellationToken ct = default);

    /// <summary>
    /// 顧客 ID に基づいて注文履歴を取得します。
    /// 結果はページング処理され、指定された条件でソートされます。
    /// </summary>
    /// <param name="customerId">顧客 ID</param>
    /// <param name="page">ページ番号</param>
    /// <param name="pageSize">ページサイズ</param>
    /// <param name="ct">キャンセレーショントークン</param>
    /// <returns>注文のページング済みリスト</returns>
    Task<PagedResult<Order>> GetOrdersByCustomerIdAsync(string customerId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// 注文をキャンセルします。
    /// <para>
    /// 注文のキャンセルは、注文が「処理中」または「確認済み」状態の場合のみ可能です。
    /// キャンセル処理には以下が含まれます：
    /// <list type="bullet">
    ///   <item>注文状態の更新</item>
    ///   <item>在庫の解放</item>
    ///   <item>該当する場合の払い戻し処理</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="orderId">キャンセルする注文の ID</param>
    /// <param name="reason">キャンセル理由（オプション）</param>
    /// <param name="ct">キャンセレーショントークン</param>
    /// <returns>キャンセル処理の結果</returns>
    /// <exception cref="NotFoundException">指定された ID の注文が存在しない場合</exception>
    /// <exception cref="BusinessException">注文が既に処理済みまたは発送済みの場合</exception>
    Task<CancellationResult> CancelOrderAsync(string orderId, string? reason, CancellationToken ct = default);
}
```

### バージョン管理

- **セマンティックバージョニング**:
  - メジャー.マイナー.パッチ形式の採用
  - 後方互換性のない API 変更時のメジャーバージョン更新
  - API の拡張時のマイナーバージョン更新
  - バグ修正時のパッチバージョン更新

- **ブランチ戦略**:
  - トランクベース開発または GitHub Flow の採用
  - フィーチャーブランチの短命化
  - プルリクエストのレビュープロセス
  - 継続的インテグレーションによる早期問題検出

- **変更履歴管理**:
  - CHANGELOG ファイルの維持
  - リリースノートの自動生成
  - API バージョン管理と互換性保証
  - 非推奨化プロセスと移行期間の設定

### チーム開発プラクティス

- **アジャイル開発手法**:
  - スクラム/カンバンプロセスの採用
  - 短いイテレーションサイクル
  - 定期的なレトロスペクティブと改善
  - 透明性の高いタスク管理

- **ペアプログラミングとモブプログラミング**:
  - 知識共有と品質向上
  - コードオーナーシップの分散
  - 新メンバーのオンボーディング加速
  - 複雑な問題への共同取り組み

- **コードレビュー文化**:
  - 建設的なフィードバックの促進
  - 自動化されたコードスタイルチェック（`dotnet format`）
  - レビュー基準の明確化
  - コードレビューの効率化（プレレビューツール等）

- **技術的負債の管理**:
  - 技術負債の可視化と計測
  - 定期的なリファクタリング時間の確保
  - 「ボーイスカウトルール」の適用
  - リファクタリングとビジネス価値のバランス

### クラウドネイティブプラクティス

- **インフラストラクチャ・アズ・コード（IaC）**:
  - Terraform, Bicep, ARM テンプレート等の活用
  - 環境のバージョン管理とリプロダクション
  - イミュータブルインフラストラクチャの実現
  - 複数環境の一貫性確保

- **コンテナオーケストレーション**:
  - .NET Aspire 13.1 によるサービス管理
  - ヘルスチェックとグレースフルシャットダウン
  - ステートフルサービスの適切な管理
  - クラウドリソースとの統合

- **サーバーレスアーキテクチャ**:
  - 適切なユースケースへの Azure Functions 活用
  - コールドスタート対策
  - コスト最適化
  - 監視と障害検知

- **クラウドサービスの効果的な活用**:
  - マネージドサービスの戦略的採用
  - クラウドネイティブパターンの適用
  - マルチクラウド/ハイブリッドクラウド戦略
  - クラウドコスト最適化

## まとめ

エンタープライズ .NET アプリケーションのマイクロサービスアーキテクチャ構築には、単なる技術選定を超えた包括的なアプローチが必要である。本ガイドでは、アーキテクチャ設計から開発方法論、パフォーマンス最適化、セキュリティ対策、テスト戦略、デプロイメント、運用、非機能要件まで、モダンなエンタープライズシステム構築の主要な考慮事項を網羅した。

適切に設計・実装されたマイクロサービスアーキテクチャは、ビジネスアジリティ、スケーラビリティ、レジリエンスを大幅に向上させることができる。しかし、その複雑さを過小評価すべきではない。分散システムの課題、サービス間通信の信頼性確保、データ整合性の維持、運用の複雑さなどに対応するため、本ガイドで紹介したプラクティスを自組織のコンテキストに適応させることが重要である。

マイクロサービスの成功には、技術的な側面だけでなく、組織構造、開発文化、運用プロセスの変革も必要である。コンウェイの法則が示すように、システムアーキテクチャは組織構造を反映する傾向があるため、チーム構成やコミュニケーションパターンがマイクロサービス導入の成否に大きく影響する。

最後に、本ガイドはベストプラクティスと考慮事項を提示しているが、全てのプロジェクトに一律に適用できる「銀の弾丸」は存在しない。各組織のビジネス要件、既存システム、チームのスキルセット、リソース制約を考慮した上で、適切なトレードオフを行いながら、持続可能で価値を創出するアーキテクチャを構築することが重要である。
