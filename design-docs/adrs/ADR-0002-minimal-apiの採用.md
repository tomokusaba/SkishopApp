# ADR-0002: Minimal API の採用（Controller ベース不使用）

## ステータス

承認済み

## コンテキスト

ASP.NET Core には、API エンドポイントを定義する 2 つの主要な方法がある: Controller ベース（MVC パターン）と Minimal API。SkiShop プロジェクトでは全 11 サービスにわたり統一的なエンドポイント実装パターンを定める必要がある。

## 決定

全マイクロサービスで **ASP.NET Core Minimal API** を採用する。Controller ベースの API は使用しない。エンドポイントは `IEndpointRouteBuilder` の拡張メソッドとして `Endpoints/` ディレクトリに配置し、`MapGroup` でグループ化する。

## 理由

### 検討した代替案

| 代替案 | メリット | デメリット |
|-------|---------|----------|
| **Controller ベース MVC** | 既存の ASP.NET 開発者に馴染みがある、フィルターパイプラインが充実 | ボイラープレートが多い、起動時のリフレクションコスト、不要な MVC 依存 |
| **Minimal API（採用）** | 軽量・高パフォーマンス、ボイラープレート最小、AOT コンパイル対応 | フィルターの柔軟性で Controller に劣る（ただし EndpointFilter で補完可能） |

### 選択理由

1. **パフォーマンス**: Minimal API は Controller ベースに比べてオーバーヘッドが少なく、高スループットが求められる EC サイトに適合
2. **ボイラープレート削減**: 各エンドポイントが簡潔に定義でき、コードの可読性と保守性が向上
3. **ASP.NET Core 10 の推奨**: Microsoft が Minimal API を主要な API 実装方式として推進
4. **AGENTS.md との整合**: プロジェクトの技術標準で Minimal API を既定

## 結果

### ポジティブ

- エンドポイント定義が簡潔になり、開発速度が向上
- 起動時間が短縮され、コンテナ環境でのスケーリングに有利
- `MapGroup` + `WithTags` + `WithOpenApi` で一貫した API ドキュメント生成が可能

### ネガティブ

- 大規模なエンドポイント群では `Endpoints/` ディレクトリの構造化が重要（規約で対応）
- Controller の `[ApiController]` が提供する自動モデルバリデーションを手動で実装する必要がある（FluentValidation で対応）
