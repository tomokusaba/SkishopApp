# プロジェクト共通ルール

## 技術スタック

- **言語**: C# 14 (.NET 10)
- **フレームワーク**: ASP.NET Core 10 (Minimal API), .NET Aspire 13.1, Semantic Kernel 1.x
- **ORM**: Entity Framework Core 10
- **ビルドツール**: dotnet CLI / MSBuild
- **パッケージ管理**: NuGet
- **プロジェクト構成**: `<プロジェクト名>/` 配下に `Endpoints`（または `Controllers`）、`Services`、`Repositories`、`Models`、`DTOs`、`Configurations` を配置

## DDD（ドメイン駆動設計）原則

- **Aggregate Root**: 各マイクロサービスは 1 つ以上の Aggregate Root を持ち、トランザクション整合性の境界とする
- **Value Object**: 不変値（金額、住所、メールアドレス等）は `record` / `readonly record struct` で Value Object として定義
- **Domain Event**: Aggregate 間の結合は Domain Event（Kafka イベント）で疎結合化。直接 DB 参照は禁止
- **Ubiquitous Language**: ビジネスドメインの用語をそのままクラス名・メソッド名に使用（例: `PlaceOrder`, `ReserveStock`）
- **Repository パターン**: Repository は Aggregate Root 単位で定義。異なる Aggregate のクエリを 1 つの Repository に混在させない

## コーディング規約

- **命名規則**: クラス名・メソッド名・プロパティは PascalCase、ローカル変数・パラメータは camelCase、プライベートフィールドは `_camelCase`、定数は PascalCase
- **プロジェクト構成**: レイヤードアーキテクチャに従い、Endpoints（Controllers）→ Services → Repositories の依存方向を厳守
- **C# 14 機能の活用**: record 型、パターンマッチング、`field` キーワード、null 条件代入 `?.=`、primary constructor、extension blocks 等を適切に利用
- **CancellationToken の必須化**: 全ての `async` メソッドのシグネチャに `CancellationToken ct = default` を含め、下位呼び出しに伝搬する
- **構造化ログ**: `ILogger<T>` を使用し、`_logger.LogInformation("Order created: {OrderId}", orderId)` のようにメッセージテンプレート形式で出力

## セキュリティ最低基準

- **OWASP Top 10** を常に意識し、特に以下を徹底:
  - 入力検証: 全ての外部入力に対してバリデーションを実施（Data Annotations: `[Required]`, `[StringLength]`, `[EmailAddress]` 等、または FluentValidation）
  - SQLインジェクション防止: EF Core の LINQ / `FromSqlInterpolated` を必須とし、`FromSqlRaw` での文字列結合を禁止
  - XSS 防止: Razor / Blazor のデフォルトエスケープを活用し、`Html.Raw()` / `MarkupString` の無検証使用を禁止
  - 認証・認可: `[Authorize]` / `RequireAuthorization()` / `[Authorize(Roles = "Admin")]` を適切に設定
- **秘密情報の管理**: API キー、パスワード、トークン等のハードコードを禁止。`appsettings.json` への直接記述不可。`dotnet user-secrets`（開発時）、環境変数、または Azure Key Vault を使用

## 耐障害性・レジリエンス

- **Polly**: 全ての外部 HTTP 通信に `IHttpClientFactory` + Polly リトライ / サーキットブレーカーポリシーを適用
- **タイムアウト**: 外部サービス呼び出しには必ず `CancellationToken` ベースのタイムアウトを設定
- **Bulkhead**: 高負荷サービスには同時実行制限（Bulkhead パターン）を適用
- **フォールバック**: 外部サービス障害時のフォールバック戦略を各サービスで定義（キャッシュ応答、デフォルト値等）

## 可観測性（Observability）

- **OpenTelemetry**: 全サービスに分散トレーシング（Traces）、メトリクス（Metrics）、ログ（Logs）を統合
- **ヘルスチェック**: 全サービスに `/health`（Liveness）と `/health/ready`（Readiness）エンドポイントを実装。DB・Redis・Kafka の疎通確認を含む
- **構造化ログ**: Serilog + `ILogger<T>` でメッセージテンプレート形式のログを出力。JSON 形式で集約基盤に送信
- **Correlation ID**: 全リクエストに相関 ID を付与し、マイクロサービス間で伝搬・ログ出力

## マイクロサービス間通信

- **同期通信**: `IHttpClientFactory` + Polly で HTTP/gRPC 呼び出し。直接 `new HttpClient()` は禁止
- **非同期通信**: Apache Kafka によるイベント駆動。Outbox パターンで DB 書き込みとイベント発行の整合性を保証
- **サービスディスカバリ**: .NET Aspire のサービス参照（`WithReference`）を使用。ハードコード URL は禁止

## テストカバレッジ目標

- **分岐カバレッジ**: 80% 以上
- **全パブリックメソッド**: 単体テスト必須
- **異常系テスト**: 正常系と同等以上のテストケースを作成

## コミットメッセージ規約

- **形式**: `<type>(<scope>): <summary>` (Conventional Commits 準拠)
- **type**: `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`, `perf`, `ci`
- **summary**: 日本語も可、50 文字以内
- **例**: `feat(auth): ログイン機能を追加`, `fix(api): NullReferenceException を修正`

## 禁止事項

- ハードコードされた秘密情報（API キー、パスワード、トークン、接続文字列）
- 未検証の外部入力をそのまま処理に使用すること
- `catch (Exception) { }` のような例外の握りつぶし
- `Console.WriteLine` によるログ出力（`ILogger<T>` / Serilog を使用）
- プレリリース版（`-preview`, `-beta`, `-rc`）の NuGet パッケージを本番ブランチに含めること
- テストなしでのコードマージ
