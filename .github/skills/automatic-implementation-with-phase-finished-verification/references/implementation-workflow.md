# 実装ワークフローガイド

各フェーズ種別ごとの詳細な実装手順と注意事項を定義する。

---

## 共通: 最終コードレビュー（全フェーズ完了後に 1 回のみ実行）

全フェーズの実装が完了した後、`orchest-code-review` エージェントによる包括的コードレビューを **1 回だけ** 実施する。各フェーズごとの実行は不要（過度な検証を避けるため）。

### レビュー実行手順

1. **orchest-code-review エージェントを呼び出す**: `runSubagent` で対象サービスディレクトリ全体をレビュー
2. **14 Agent の並列レビュー結果を受け取る**: 各 Agent の指摘を Critical / High / Medium / Low で分類
3. **Critical / High 指摘の修正**: 検出された場合は即座に修正し、再レビュー（最大 2 回）
4. **Medium / Low はレポートに記録**: 改善推奨事項として最終レポートに含める

### 各フェーズでの品質チェック（orchest-code-review 不要）

各フェーズ完了時には、orchest-code-review の代わりに **Step 3.3 の自己検証（8 項目の grep チェック）** をターミナルで実行する。これにより、禁止パターン・未完了実装・セキュリティ違反を軽量に検出する。

### フェーズ種別ごとの重点 Agent（最終レビュー時の参考）

| フェーズ | 重点 Agent | 理由 |
|---------|-----------|------|
| エンティティ | `data-access`, `ddd-domain` | テーブル設計・Aggregate 境界の妥当性 |
| Repository | `data-access`, `async-concurrency` | クエリ品質・CancellationToken 伝搬 |
| Service | `ddd-domain`, `error-logging` | ビジネスロジック・例外処理の正確性 |
| Endpoints | `api-endpoint`, `security` | REST 規約・認証認可・入力検証 |
| Kafka/Background | `async-concurrency`, `resilience` | 非同期処理・耐障害性 |
| Security | `security` | OWASP Top 10 の網羅的確認 |
| Test | `test-quality` | テスト命名・AAA・カバレッジ |

---

## 1. Phase 1: 基盤構築（プロジェクトスケルトン）

### 実装手順

1. **プロジェクト作成**（既存でない場合）
   ```bash
   dotnet new web -n {ServiceName} --framework net10.0
   ```

2. **.csproj の設定確認**
   ```xml
   <PropertyGroup>
     <TargetFramework>net10.0</TargetFramework>
     <Nullable>enable</Nullable>
     <ImplicitUsings>enable</ImplicitUsings>
     <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
   </PropertyGroup>
   ```

3. **NuGet パッケージ追加**: 実装計画に記載された全パッケージを追加
   ```bash
   dotnet add package {PackageName} --version {Version}
   ```

4. **ディレクトリ構成作成**: 実装計画に記載された全ディレクトリを作成
   ```
   {ServiceName}/
   ├── Endpoints/
   ├── Services/
   │   └── Interfaces/
   ├── Repositories/
   │   └── Interfaces/
   ├── Models/
   ├── DTOs/
   │   ├── Requests/
   │   └── Responses/
   ├── Configurations/
   ├── Infrastructure/
   │   └── Persistence/
   └── Exceptions/
   ```

5. **基本設定ファイル作成**: `appsettings.json`, `appsettings.Development.json`

### 検証ポイント

- `dotnet build` が成功すること
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` が設定されていること
- `<Nullable>enable</Nullable>` が設定されていること
- 全必須 NuGet パッケージがプレリリース版でないこと
- ディレクトリ構成が実装計画と一致すること

---

## 2. Phase 2: エンティティ・DbContext

### 実装手順

1. **エンティティクラス作成**（Models/ 配下）
   - `[Table("snake_case_plural")]` 属性を付与
   - 全プロパティに `[Column("snake_case")]` 属性を付与
   - `[Key]`, `[Required]`, `[MaxLength]` 等のバリデーション属性
   - `CreatedAt` / `UpdatedAt` 監査カラムを含む
   - コレクションナビゲーションは `= []` で初期化

2. **Value Object 作成**（該当する場合）
   - `record` / `readonly record struct` で定義
   - バリデーションロジックをコンストラクタに含む

3. **DTO 作成**（DTOs/ 配下）
   - リクエスト DTO: `record` + Data Annotations
   - レスポンス DTO: `record`（不変）

4. **AppDbContext 作成**
   - `DbSet<T>` プロパティ定義
   - `OnModelCreating` でインデックス・制約設定
   - `SaveChangesAsync` オーバーライドで `CreatedAt`/`UpdatedAt` 自動更新

5. **Migrations 生成**
   ```bash
   dotnet ef migrations add Initial --project {ServiceName}
   ```

### 検証ポイント

- 設計書の全テーブルに対応するエンティティが存在すること
- カラム名が snake_case であること（`[Column]` 属性）
- テーブル名が snake_case 複数形であること（`[Table]` 属性）
- `DateTime.Now` が使用されていないこと（`DateTime.UtcNow` を使用）
- Migration が正常に生成されること
- CHECK 制約・インデックスが設計書通りであること

---

## 3. Phase 3: Repository

### 実装手順

1. **インターフェース定義**（Repositories/Interfaces/）
   - Aggregate Root 単位で 1 インターフェース
   - 全メソッドに `CancellationToken ct = default` パラメータ

2. **実装クラス**（Repositories/）
   - primary constructor で `AppDbContext` を注入
   - 読み取り専用クエリに `AsNoTracking()` を使用
   - `CancellationToken` を全 EF Core 呼び出しに伝搬

### 検証ポイント

- 設計書の全 Repository インターフェースが定義されていること
- 全メソッドに `CancellationToken ct = default` があること
- 読み取り専用クエリで `AsNoTracking()` が使用されていること
- 異なる Aggregate のクエリが混在していないこと

---

## 4. Phase 4: Service（ビジネスロジック）

### 実装手順

1. **インターフェース定義**（Services/Interfaces/）
2. **実装クラス**（Services/）
   - primary constructor で依存性を注入
   - ビジネスロジックを完全に実装（スタブ禁止）
   - 適切な例外クラスをスロー
   - `ILogger<T>` でメッセージテンプレート形式のログ出力

3. **例外クラス定義**（Exceptions/）
   - `NotFoundException`, `BusinessException`, `ConcurrencyException` 等

4. **FluentValidation バリデーター**（該当する場合）

### 検証ポイント

- 設計書の全ビジネスルールが実装されていること
- 例外処理が適切であること（握りつぶし禁止）
- ログ出力がメッセージテンプレート形式であること
- `CancellationToken` が全メソッドに伝搬されていること
- Service が Repository のみに依存し、他の Service への循環依存がないこと

---

## 5. Phase 5: Endpoints（Minimal API）

### 実装手順

1. **エンドポイントクラス作成**（Endpoints/）
   - `IEndpointRouteBuilder` 拡張メソッドパターン
   - `MapGroup()` でグループ化
   - `.WithTags()`, `.WithOpenApi()`, `.WithName()` メタデータ
   - `.RequireAuthorization()` / `.AllowAnonymous()` 認可設定

2. **バリデーション統合**
   - `IValidator<T>` を注入
   - `Results.ValidationProblem()` でエラー返却

### 検証ポイント

- 設計書の全 API エンドポイントが実装されていること
- REST 規約に準拠していること（名詞 URI、適切な HTTP メソッド）
- 全エンドポイントに認可設定があること
- バリデーションが実施されていること
- エンドポイントにビジネスロジックが含まれていないこと（Service 委譲）

---

## 6. Phase 6: Kafka / BackgroundService

### 実装手順

1. **イベント record 定義**: 不変 record で Kafka イベントペイロードを定義
2. **Producer 実装**: `IProducer<string, string>` を使用
3. **Consumer 実装**: `BackgroundService` で `IServiceScopeFactory` を使用
4. **Outbox パターン**（該当する場合）: `OutboxEvent` エンティティ + `OutboxPublisher` BackgroundService

### 検証ポイント

- 設計書の全イベントが定義されていること
- Consumer が `BackgroundService` で実装されていること
- `IServiceScopeFactory` で Scoped サービスを取得していること
- `stoppingToken` が全下位呼び出しに伝搬されていること
- エラー時のバックオフ / Dead Letter 戦略が実装されていること

---

## 7. Phase 7-8: Redis / Security

### Redis 検証ポイント
- キャッシュキー命名規約が統一されていること
- TTL が設計書通りに設定されていること
- キャッシュ無効化ロジックが実装されていること

### Security 検証ポイント
- セキュリティヘッダーが全て設定されていること
- レート制限が認証エンドポイントに設定されていること
- IDOR 防止（オーナーシップチェック）が実装されていること
- PII がログに出力されていないこと

---

## 8. Phase 9-10: テスト / 可観測性

### テスト検証ポイント
- テスト命名が `Should_期待結果_When_条件` パターンであること
- AAA パターン（Arrange-Act-Assert）が守られていること
- NSubstitute でモック化されていること
- Shouldly でアサーションされていること
- 異常系テストが正常系と同等以上であること
- `[Trait("Category", "...")]` が付与されていること

### 可観測性検証ポイント
- OpenTelemetry が設定されていること
- ヘルスチェック（`/health`, `/health/ready`）が実装されていること
- Serilog + 構造化ログが設定されていること
- Correlation ID ミドルウェアが設定されていること

---

## 9. Phase 11: 最終統合・デプロイ準備

### 検証ポイント
- `Program.cs` の DI 登録が全サービス/リポジトリを含むこと
- ミドルウェアパイプラインの順序が正しいこと（AGENTS.md §11.3 準拠）
- `dotnet build` が成功すること
- `dotnet test` が全て通過すること（テストプロジェクトがある場合）
- Dockerfile が存在し、マルチステージビルド + 非 root 実行であること
- 全禁止パターンの grep チェックが 0 件であること

---

## 共通: リグレッション防止

各フェーズ開始時に以下を実行し、前フェーズの成果物が壊れていないことを確認:

```bash
# ビルド確認
dotnet build --no-restore

# 前フェーズのテストが通るか確認（テストプロジェクトがある場合）
dotnet test --no-build --filter "Category={PreviousPhaseCategory}"
```

壊れている場合は、現フェーズの実装を開始する前に修正する。
