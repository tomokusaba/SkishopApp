---
applyTo: "**/*.csproj"
---

# NuGet 依存関係管理 Instructions

本 Instructions は `**/*.csproj` に自動適用される。NuGet パッケージの追加・変更時に以下のチェック観点を遵守すること。

---

## 1. 依存関係の最小化

### 基本原則
- **不要な依存関係を追加しない**。1 つの機能のために巨大なライブラリを追加する前に、.NET SDK 標準機能で実現可能か検討する
- 使用していないパッケージが `.csproj` に残っていないか確認する
- 同一機能を提供する複数ライブラリの共存を避ける（例: 複数の JSON ライブラリ、複数のログ実装）

### PrivateAssets の適切な設定
- 開発時のみ必要なパッケージには `PrivateAssets="all"` を設定し、依存先プロジェクトに伝搬させない

```xml
<!-- ✅ 良い例: 開発用ツールは PrivateAssets -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.*" PrivateAssets="all" />

<!-- ❌ 悪い例: Design パッケージを本番に含める -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.*" />
```

### .NET SDK 標準機能の活用
- `System.Text.Json` は SDK 同梱のため、JSON 処理には追加パッケージ不要
- `ILogger<T>` / `IConfiguration` は `Microsoft.Extensions.*` で提供
- 新規プロジェクトで `Newtonsoft.Json` を採用しない（`System.Text.Json` を使用）

---

## 2. バージョン管理

### プレリリース版の禁止
- **本番ブランチ（main / release）に `-preview`, `-beta`, `-rc` パッケージを含めることは絶対禁止**
- プレリリース版を検出した場合は Critical 指摘とする

```xml
<!-- ❌ Critical 違反: プレリリース版 -->
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0-preview.3" />

<!-- ✅ 良い例: GA（正式リリース）バージョン -->
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.*" />
```

### バージョン指定のルール
- メジャーバージョンを固定し、マイナー / パッチはワイルドカード（`*`）で最新を取得する
- 特定のバグ / セキュリティ修正が必要な場合は固定バージョンを指定する

```xml
<!-- ✅ 良い例: メジャーバージョン固定 + マイナー自動更新 -->
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.*" />
<PackageReference Include="Serilog.AspNetCore" Version="8.*" />

<!-- ✅ 良い例: セキュリティ修正のため固定 -->
<PackageReference Include="StackExchange.Redis" Version="2.8.16" />
```

### Directory.Build.props の活用
- 複数プロジェクト間でバージョンを一元管理する場合は `Directory.Build.props` を使用する

```xml
<!-- ✅ Directory.Build.props でのバージョン一元管理 -->
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

---

## 3. 必須パッケージ（各サービスの .csproj）

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <!-- ORM -->
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.*" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.*" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.*" PrivateAssets="all" />

    <!-- 認証 -->
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.*" />
    <PackageReference Include="Microsoft.Identity.Web" Version="3.*" />

    <!-- バリデーション（FluentValidation.AspNetCore は 11.x で非推奨） -->
    <PackageReference Include="FluentValidation" Version="11.*" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.*" />

    <!-- メッセージング -->
    <PackageReference Include="Confluent.Kafka" Version="2.*" />

    <!-- キャッシュ -->
    <PackageReference Include="StackExchange.Redis" Version="2.*" />

    <!-- 耐障害性 -->
    <PackageReference Include="Polly" Version="8.*" />
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="9.*" />

    <!-- ロギング -->
    <PackageReference Include="Serilog.AspNetCore" Version="8.*" />
    <PackageReference Include="Serilog.Sinks.Console" Version="6.*" />
    <PackageReference Include="Serilog.Formatting.Compact" Version="3.*" />

    <!-- OpenTelemetry -->
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.*" />

    <!-- ヘルスチェック -->
    <PackageReference Include="AspNetCore.HealthChecks.NpgSql" Version="9.*" />
    <PackageReference Include="AspNetCore.HealthChecks.Redis" Version="9.*" />
  </ItemGroup>
</Project>
```

---

## 4. 既知脆弱性（CVE）

### 脆弱性チェック
- 依存パッケージに既知の脆弱性（CVE）がないか確認する
- `dotnet list package --vulnerable` で脆弱性を検出する
- `dotnet list package --outdated` で古いバージョンを確認する

### 脆弱性発見時の対応

| CVSS スコア | 対応 |
|---|---|
| 9.0-10.0（Critical） | **即座のバージョンアップが必須**。修正版がない場合はパッケージの除去を検討 |
| 7.0-8.9（High） | **リリース前のバージョンアップが必須** |
| 4.0-6.9（Medium） | 計画的にバージョンアップを実施 |
| 0.1-3.9（Low） | 次回アップデート時に対応 |

### 推移的依存関係の管理
- `dotnet list package --include-transitive` で推移的依存関係を確認する
- 推移的依存関係に脆弱性がある場合、親パッケージのアップグレードまたは直接参照で安全なバージョンを指定する

---

## 5. 禁止パッケージ・避けるべき慣行

| 禁止・非推奨 | 理由 | 代替 |
|------------|------|------|
| `System.Web` | .NET Framework 専用 | ASP.NET Core |
| `log4net` / `NLog`（直接使用） | `ILogger<T>` 抽象を破る | Serilog + `ILogger<T>` |
| `EntityFramework`（EF6） | 旧世代 ORM | Entity Framework Core 10 |
| `Newtonsoft.Json`（新規実装） | 旧世代 | `System.Text.Json`（SDK 同梱） |
| `WebClient` / `HttpWebRequest` | 非推奨 | `HttpClient` / `IHttpClientFactory` |
| `-preview` / `-beta` / `-rc` パッケージ | 不安定 | GA（正式リリース）を使用 |
| `FromSqlRaw` での文字列結合 | SQL インジェクションリスク | EF Core LINQ / `FromSqlInterpolated` |

---

## 6. ライセンス互換性

- **GPL 系ライセンス**のパッケージは商用プロジェクトで使用前に法務確認が必要
- **ライセンス不明**のパッケージは使用禁止
- 新規パッケージ追加時は OSS 審査（`oss-reviewer`）の対象となることを認識する

| ライセンス | 商用利用 | 注意事項 |
|-----------|---------|---------|
| MIT, Apache 2.0, BSD | ✅ 可 | 著作権表示・免責事項の記載 |
| LGPL | ⚠️ 条件付き | 動的リンクなら通常可 |
| GPL | ❌ 要法務確認 | プロジェクト全体への伝播リスク |
| SSPL, BSL | ❌ 要法務確認 | 商用利用に重大な制約 |

---

## 7. プロジェクト設定

### .csproj の必須 PropertyGroup

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
</PropertyGroup>
```

| 設定 | 値 | 理由 |
|------|-----|------|
| `TargetFramework` | `net10.0` | .NET 10 LTS |
| `Nullable` | `enable` | Nullable Reference Types の有効化 |
| `ImplicitUsings` | `enable` | 暗黙的 using の有効化 |
| `TreatWarningsAsErrors` | `true` | 警告をエラーとして扱い、コード品質を維持 |

### テストプロジェクトの追加パッケージ

```xml
<!-- テストプロジェクト（*Tests.csproj）のみ -->
<ItemGroup>
  <PackageReference Include="xunit" Version="2.*" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
  <PackageReference Include="NSubstitute" Version="5.*" />
  <PackageReference Include="Shouldly" Version="4.*" />
  <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.*" />
  <PackageReference Include="Testcontainers.PostgreSql" Version="4.*" />
  <PackageReference Include="coverlet.collector" Version="6.*" />
</ItemGroup>
```

---

## 8. 禁止事項チェックリスト

| # | 禁止事項 | 重要度 | 理由 |
|---|---------|--------|------|
| 1 | プレリリース版（`-preview`, `-beta`, `-rc`）の本番ブランチ混入 | Critical | ビルドの再現性・安定性が失われる |
| 2 | CVE（Critical/High）のある依存関係の放置 | Critical | セキュリティインシデントに直結 |
| 3 | ライセンス不明のパッケージの使用 | High | 法的リスク |
| 4 | `Newtonsoft.Json` の新規採用 | Medium | `System.Text.Json` を使用 |
| 5 | テスト用パッケージに `PrivateAssets` / `Condition` なし | Medium | 本番ランタイムに不要なライブラリが含まれる |
| 6 | `TreatWarningsAsErrors` が `false` | Medium | 警告の蓄積によるコード品質低下 |
| 7 | 未使用のパッケージの放置 | Medium | イメージサイズ増大、攻撃面の拡大 |
| 8 | `Nullable` が `disable` | Medium | Null Safety の恩恵を受けられない |
