# .NET / C# ベストプラクティス確認チェックリスト

`dotnet-best-practices-ja` Skill から参照する、実務向けの確認リスト。

## 1. ドキュメント / 構造

- [ ] ディレクトリ構成が `Endpoints`, `Services`, `Repositories`, `Models`, `DTOs`, `Configurations` の責務に沿っている
- [ ] 公開 API や再利用部品に必要な XML コメントがある
- [ ] DTO / Entity / Service / Repository が混在していない

## 2. 設計 / アーキテクチャ

- [ ] `Endpoints -> Services -> Repositories` の依存方向を守っている
- [ ] Endpoint にビジネスロジックが入り込みすぎていない
- [ ] Aggregate Root 単位で Repository が分離されている
- [ ] Value Object が `record` / `readonly record struct` で表現されている

## 3. DI / サービス登録

- [ ] DI は primary constructor ベース
- [ ] インターフェースと実装が整合している
- [ ] Scoped / Singleton / Transient の選択理由が妥当
- [ ] `new HttpClient()` や DI を回避する直接 `new` がない

## 4. Async / Cancellation

- [ ] すべての `async` メソッドに `CancellationToken ct = default` がある
- [ ] EF Core、HTTP、BackgroundService、外部 API に `ct` を渡している
- [ ] `.Result` / `.Wait()` / `Thread.Sleep()` を使用していない
- [ ] 非同期メソッド名が `Async` サフィックスを持つ

## 5. 設定 / appsettings

- [ ] 秘密情報が `appsettings*.json` に含まれていない
- [ ] `IOptions<T>` と `ValidateOnStart()` を使っている
- [ ] `Program.cs` のミドルウェア順序が規約どおり
- [ ] OpenAPI は `AddOpenApi()` + `MapOpenApi()` を使っている

## 6. ログ / 例外

- [ ] `ILogger<T>` の構造化ログを使っている
- [ ] `Console.WriteLine` を使っていない
- [ ] `catch (Exception) { }` がない
- [ ] 具体的な例外型を使い、Problem Details にマッピングできる

## 7. データアクセス

- [ ] SQL 文字列連結がない
- [ ] `FromSqlRaw` の危険な利用がない
- [ ] 読み取り専用クエリで `AsNoTracking()` を使える
- [ ] 楽観的ロックが必要な箇所で `[Timestamp]` 等を検討している

## 8. セキュリティ

- [ ] 入力検証がある
- [ ] 認証 / 認可が明示されている
- [ ] PII や秘密情報をログ出力していない
- [ ] 外部 URL / AI 入力 / ファイル入力の検証がある

## 9. テスト

- [ ] テストは xUnit
- [ ] アサーションは Shouldly
- [ ] モックは NSubstitute
- [ ] テスト名が `Should_Expected_When_Condition`
- [ ] 正常系 / 異常系 / 境界値 / キャンセルをカバーしている

## 10. AI / Semantic Kernel

- [ ] Semantic Kernel 設定が DI とオプションで管理されている
- [ ] AI 応答の出力形式が曖昧でない
- [ ] プロンプトインジェクションや過剰権限への対策を考慮している
- [ ] モデル設定や API エンドポイントがハードコードされていない
