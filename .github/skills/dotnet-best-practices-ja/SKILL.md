---
name: dotnet-best-practices-ja
description: ".NET / C# 実装をこのリポジトリのベストプラクティスに沿ってレビュー・改善する日本語 Skill。Use when: C# コードレビュー、.NET 実装改善、DI/非同期/設定/テスト/セキュリティ観点の見直し"
argument-hint: "対象ファイル、ディレクトリ、またはソリューション名を指定"
---

# dotnet-best-practices-ja — .NET / C# ベストプラクティス Skill

## 目的

指定された .NET / C# コードを、このリポジトリで採用している **C# 14 / .NET 10 / ASP.NET Core 10 / EF Core 10 / Aspire / Semantic Kernel** の実装規約に沿ってレビューし、必要に応じて改善する。

元になっている `awesome-copilot` の `dotnet-best-practices` Skill の観点を踏まえつつ、以下のように**このプロジェクト向けにローカライズ**して扱う:

- テストは **xUnit + Shouldly + NSubstitute** を標準とする
- DI は **primary constructor** を優先する
- 非同期処理では **CancellationToken 伝搬** を必須とする
- 設定は **IOptions<T> + ValidateOnStart()** を優先する
- ロギングは **ILogger<T> の構造化ログ** を前提とする
- セキュリティは **OWASP Top 10** と既存 instruction 群を基準にする

## 使用場面

- C# / .NET コードの実装品質をまとめて見直したいとき
- 新規サービスやエンドポイントが既存規約に沿っているか確認したいとき
- リファクタリング時に DI、非同期、設定、例外処理、テスト方針を揃えたいとき
- Semantic Kernel や AI 関連コードを安全に組み込みたいとき

## 前提条件

1. 対象コードにアクセスできること
2. 以下の規約ファイルを参照可能であること
   - `AGENTS.md`
   - `.github/instructions/dotnet-coding-standards.instructions.md`
   - `.github/instructions/security-coding.instructions.md`
   - `.github/instructions/api-design.instructions.md`
   - `.github/instructions/dotnet-config.instructions.md`
   - `.github/instructions/nuget-dependency.instructions.md`
   - `.github/instructions/test-standards.instructions.md`
3. 必要に応じて `dotnet build` / `dotnet test` を実行できること

## レビュー / 改善観点

詳細チェックリストは [best-practices-checklist](./references/best-practices-checklist.md) を参照。

### 1. ドキュメントと構造

- 既存の名前空間・ディレクトリ構成に従っているか
- 公開 API や再利用コンポーネントに必要な XML コメントがあるか
- DTO / Entity / Service / Repository / Endpoint の責務分離が明確か

### 2. 設計とアーキテクチャ

- `Endpoints -> Services -> Repositories` の依存方向を守っているか
- Aggregate Root / Value Object / Domain Event の境界を壊していないか
- 複雑な生成処理や分岐を Factory / 専用 Service に分離できているか

### 3. DI とサービス設計

- primary constructor による DI を使っているか
- インターフェース命名が `I` プレフィックスで統一されているか
- ライフタイムが適切か（Scoped / Singleton / Transient）
- `new HttpClient()` や `new Service()` のような DI 回避がないか

### 4. 非同期と並行性

- すべての `async` メソッドに `CancellationToken ct = default` があるか
- 下位呼び出しまで `ct` を伝搬しているか
- `.Result` / `.Wait()` / `Thread.Sleep()` を使っていないか
- I/O は `async/await` で処理されているか
- ライブラリコード以外で不要な `ConfigureAwait(false)` を乱用していないか

### 5. リソース管理とローカライズ

- ユーザー向けメッセージがハードコードされすぎていないか
- 多言語化が必要な箇所は Resource / 設定 / 専用メッセージ定義に切り出せるか
- `IAsyncDisposable` は `await using` で破棄しているか
- 時刻依存ロジックで `TimeProvider` を使える箇所がないか

### 6. テスト基準

- テストフレームワークは **xUnit** を使用しているか
- アサーションは **Shouldly** を優先しているか
- モックは **NSubstitute** を使用しているか
- テスト名が `Should_Expected_When_Condition` を満たしているか
- 正常系だけでなく異常系、境界値、キャンセルをカバーしているか

### 7. 設定とオプション

- 秘密情報を `appsettings*.json` に直接書いていないか
- `IOptions<T>` / `AddOptions<T>()` / `ValidateOnStart()` を使っているか
- `IConfiguration["Some:Key"]` のマジックストリング依存が過剰でないか
- `Program.cs` のミドルウェア順序が既存規約に沿っているか

### 8. Semantic Kernel / AI 統合

- Semantic Kernel の登録と設定が DI ベースで行われているか
- AI モデル設定がオプション化されているか
- プロンプトやツール呼び出しが過剰権限になっていないか
- 構造化出力や検証可能なレスポンス形式を採用できているか

### 9. エラーハンドリングとロギング

- `ILogger<T>` による構造化ログを使っているか
- `Console.WriteLine` を使っていないか
- 例外の握りつぶしがないか
- 期待される失敗は具体的な例外型で表現しているか
- API は Problem Details ベースでエラーを返しているか

### 10. 性能とセキュリティ

- 入力検証があるか
- SQL は EF Core LINQ / `FromSqlInterpolated` を使っているか
- 読み取り専用クエリで `AsNoTracking()` を使えるか
- 外部 HTTP 呼び出しが `IHttpClientFactory` + resilience handler 経由か
- 認証 / 認可 / レート制限 / セキュリティヘッダーが必要箇所にあるか

### 11. コード品質

- SOLID 原則に反する責務過多クラスがないか
- 重複実装を基底クラス・共通ヘルパー・共通 Service に寄せられるか
- 命名がドメイン用語に一致しているか
- メソッドが過度に長く、凝集度が下がっていないか

## 実施手順

1. **対象範囲の特定**
   - 引数で指定されたファイル / ディレクトリ / ソリューションを確認する
   - 関連する `Program.cs`、`*.csproj`、`appsettings*.json`、テストファイルも視野に入れる

2. **規約の読み込み**
   - `AGENTS.md` と `.github/instructions/*.instructions.md` のうち対象に関係するものを読む

3. **実装の棚卸し**
   - 命名、依存方向、DI、非同期、設定、例外処理、ログ、セキュリティ、テストの観点で現状を確認する

4. **重要度順に改善**
   - **Critical / High** を先に直す
   - ルール違反の修正と、それに密接に結びつく周辺修正をまとめて行う

5. **プロジェクト標準に合わせる**
   - テストライブラリは `MSTest + Moq + FluentAssertions` ではなく、**このリポジトリ標準の `xUnit + NSubstitute + Shouldly`** を採用する
   - C# 機能は `record`、pattern matching、primary constructor、`TimeProvider` 等を優先する

6. **検証**
   - 既存の `dotnet build`、`dotnet test`、必要なフィルタ付きテストを実行する
   - 変更内容に応じて対象プロジェクト単位またはソリューション単位で確認する

## 出力フォーマット

```markdown
# .NET ベストプラクティス適用結果

## 対象
- {対象ファイル / 対象ディレクトリ}

## 対応サマリー
- {何を改善したか}

## 主な改善点
| 観点 | 内容 |
|------|------|
| DI | ... |
| Async | ... |
| Security | ... |
| Test | ... |

## 残課題
| 重要度 | 内容 | 理由 |
|--------|------|------|
| High / Medium / Low | ... | ... |
```

## 注意事項

- 既存規約と競合する場合は、**このリポジトリ内の instruction / AGENTS.md を優先**する
- 既存パターンに合わせることを優先し、外部 Skill の内容を機械的に押し付けない
- ユーザー向けの日本語文言は、プロジェクト内の既存トーンに揃える

## 参照ドキュメント

- [best-practices-checklist](./references/best-practices-checklist.md)
- `AGENTS.md`
- `.github/instructions/dotnet-coding-standards.instructions.md`
- `.github/instructions/security-coding.instructions.md`
- `.github/instructions/api-design.instructions.md`
- `.github/instructions/dotnet-config.instructions.md`
- `.github/instructions/nuget-dependency.instructions.md`
- `.github/instructions/test-standards.instructions.md`
