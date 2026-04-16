---
name: dotnet-design-pattern-review
description: ".NET / C# コードのデザインパターン実装をレビューし、改善提案を日本語で返す。Use when: .NET 設計レビュー、デザインパターン確認、SOLID 原則の点検"
argument-hint: "レビュー対象のファイル、ディレクトリ、または選択範囲。例: Services/Auth, Program.cs, solution 全体"
---

# dotnet-design-pattern-review - .NET デザインパターンレビュー Skill

## 目的

`${selection}` で指定された C# / .NET コードを対象に、**デザインパターンの適用状況・設計の妥当性・改善余地**を日本語でレビューする。

- **コード変更は行わない**
- **レビュー結果は必ず日本語で返す**
- **抽象論ではなく、対象コードに根ざした具体的な指摘を返す**

## 使用場面

- .NET プロジェクトの設計レビューをしたいとき
- Command / Factory / Repository などの実装が妥当か確認したいとき
- SOLID 原則や保守性の観点で改善点を洗い出したいとき
- リファクタリング前に設計上の問題を把握したいとき

## 前提条件

- レビュー対象の C# / .NET コードにアクセスできること
- `${selection}` が指定されている場合は、その範囲を最優先でレビューすること
- 対象がソリューション全体の場合は、主要な `.sln`、`.csproj`、`Program.cs`、`Services/`、`Repositories/`、`Endpoints/`、`Models/`、`DTOs/` を優先確認すること

## 必須レビュー観点

詳細な観点は [レビュー観点一覧](./references/review-checklist.md) を参照。

### 1. 必須デザインパターン

- **Command Pattern**
  - `CommandHandler<TOptions>` のような共通基底
  - `ICommandHandler<TOptions>` などの抽象化
  - オプション型の継承設計
  - `SetupCommand(IHost host)` など起動統合の一貫性
- **Factory Pattern**
  - 複雑な生成処理の切り出し
  - DI / ServiceProvider との整合
  - 生成責務の分離
- **Dependency Injection**
  - primary constructor の活用
  - 依存の抽象化
  - 適切なライフタイム
  - null 安全性
- **Repository Pattern**
  - 非同期データアクセス
  - Aggregate 単位の責務分離
  - 永続化詳細の隠蔽
- **Provider Pattern**
  - 外部サービスとの境界分離
  - 明確な契約
  - 設定と例外処理の整理
- **Resource Pattern**
  - `ResourceManager` / `.resx` による文言管理
  - エラーメッセージ、ログメッセージ、表示文言の分離

### 2. アーキテクチャ / 設計品質

- 名前空間、レイヤー、責務の分離が明確か
- Endpoints -> Services -> Repositories の依存方向が守られているか
- ビジネスロジックが Endpoint に漏れていないか
- ドメイン用語とコード上の命名が一致しているか

### 3. .NET ベストプラクティス

- `async` / `await` と `CancellationToken` の伝搬
- 構造化ログ (`ILogger<T>`)
- `IOptions<T>` による型安全な設定
- `ProblemDetails` ベースのエラー設計
- `TimeProvider`、`ResourceManager`、`IHttpClientFactory` などの活用余地

### 4. GoF / SOLID / 保守性

- Command / Factory / Template Method / Strategy の使い分け
- Single Responsibility / Open-Closed / Dependency Inversion の逸脱
- テスト容易性、責務過多、重複、分岐の肥大化

### 5. セキュリティ / 性能

- 外部入力バリデーション
- 例外処理の安全性
- 秘密情報の扱い
- 非同期処理、リソース解放、不要な同期ブロック

## 実施手順

1. **レビュー対象の把握**
   - `${selection}` の範囲を特定する
   - 対象範囲に関連する主要ファイル・依存先・構成ファイルを洗い出す
2. **実装パターンの抽出**
   - 実際に使われているパターンを列挙する
   - パターン名だけでなく、どのコードがその役割を担っているかを確認する
3. **必須観点でのレビュー**
   - Command / Factory / DI / Repository / Provider / Resource の各観点で妥当性を判定する
4. **横断観点でのレビュー**
   - SOLID、保守性、テスト容易性、セキュリティ、性能を確認する
5. **改善提案の整理**
   - 重要度順に整理し、対象ファイルや根拠を添えて日本語で報告する

## レビュー時のルール

- **変更提案は具体的に書く**  
  例: 「Factory を使うべき」ではなく、「`FooService` の生成条件分岐を `IFooFactory` に分離すると `Program.cs` の分岐とテスト負荷を減らせる」のように書く
- **根拠を明示する**  
  対象ファイル、型名、メソッド名、責務の衝突箇所を示す
- **問題がない点も要点だけ触れる**  
  全て欠点として扱わず、良い設計も短く示す
- **推測で断定しない**  
  情報不足の指摘は「要確認」として扱う
- **コード変更はしない**

## 出力フォーマット

```markdown
# .NET デザインパターンレビュー結果

## 総評
- 全体評価: 良好 / 要改善 / 要大幅見直し
- 主要な論点: 2〜4 点

## 良い点
- ...

## 指摘事項
| 重要度 | 観点 | 対象 | 指摘内容 | 改善提案 |
|--------|------|------|----------|----------|
| High | Repository Pattern | `...` | ... | ... |
| Medium | SOLID | `...` | ... | ... |

## パターン別所見
### Command Pattern
- 採用状況:
- 問題点:
- 改善案:

### Factory Pattern
- ...

### Dependency Injection
- ...

### Repository Pattern
- ...

### Provider Pattern
- ...

### Resource Pattern
- ...

## 追加で検討すると良いこと
- ...
```

## 期待するレビュー品質

- **実コードに紐づく具体性**
- **日本語で読みやすい要約**
- **優先順位が分かる整理**
- **設計意図を尊重した現実的な改善提案**

## 参照ドキュメント

- [レビュー観点一覧](./references/review-checklist.md)
