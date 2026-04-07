# 検証パターン集

フェーズ完了検証で使用する品質チェックパターンの詳細を定義する。

---

## 1. コード品質 — grep 検索パターン

### 1.1 未完了作業・仮実装の検出

| # | パターン | 対象 | 重要度 | 判定 |
|---|---------|------|--------|------|
| 1 | `TODO` | `*.cs`（テスト除外） | Critical | ❌ 全て解消済みであること |
| 2 | `FIXME` | 同上 | Critical | ❌ 同上 |
| 3 | `HACK` | 同上 | Critical | ❌ 同上 |
| 4 | `UNDONE` | 同上 | Critical | ❌ 同上 |
| 5 | `XXX` | 同上 | High | ⚠️ コメント内容を確認 |
| 6 | `NotImplementedException` | `*.cs`（テスト除外） | Critical | ❌ 全メソッドを完全実装すること |
| 7 | `throw new NotImplementedException` | 同上 | Critical | ❌ 同上 |
| 8 | `{ }` (空メソッドボディ) | 同上 | High | ❌ 空メソッドは許可しない |

### 1.2 禁止されたコーディングパターン

| # | パターン | 対象 | 重要度 | 判定 |
|---|---------|------|--------|------|
| 1 | `Console.WriteLine` | `*.cs` | Critical | ❌ `ILogger<T>` を使用 |
| 2 | `Console.Write(` | 同上 | Critical | ❌ 同上 |
| 3 | `Console.Error` | 同上 | Critical | ❌ 同上 |
| 4 | `.Result` | 同上 | High | ❌ `await` を使用（デッドロック防止） |
| 5 | `.Wait()` | 同上 | High | ❌ 同上 |
| 6 | `Thread.Sleep` | 同上 | High | ❌ `await Task.Delay()` を使用 |
| 7 | `[Inject]` | 同上 | High | ❌ コンストラクタインジェクション必須 |
| 8 | `new HttpClient()` | 同上 | High | ❌ `IHttpClientFactory` を使用 |
| 9 | `DateTime.Now` | 同上 | High | ❌ `DateTime.UtcNow` / `TimeProvider` を使用 |

### 1.3 セキュリティ違反パターン

| # | パターン | 対象 | 重要度 | 判定 |
|---|---------|------|--------|------|
| 1 | `FromSqlRaw` + 文字列結合 | `*.cs` | Critical | ❌ SQL インジェクション。EF Core LINQ / `FromSqlInterpolated` 必須 |
| 2 | `Password\s*=\s*"` | `*.cs`, `*.json` | Critical | ❌ 秘密情報ハードコード |
| 3 | `"sk-` (API キー) | `*.cs`, `*.json` | Critical | ❌ 同上 |
| 4 | `connectionString\s*=\s*".*Password` | 同上 | Critical | ❌ 同上 |
| 5 | `Html.Raw(` | `*.cshtml`, `*.razor` | High | ❌ XSS リスク |
| 6 | `MarkupString` | `*.razor` | High | ⚠️ 無検証使用は ❌ |

### 1.4 ダミー値・テスト値の本番コード混入

| # | パターン | 対象 | 重要度 | 判定 |
|---|---------|------|--------|------|
| 1 | `"dummy"` | `*.cs`（テスト除外） | High | ❌ 本番コードにダミー値禁止 |
| 2 | `"test"` (文字列リテラル) | 同上 | High | ⚠️ 文脈確認（テストコード混入でないか） |
| 3 | `"xxx"` | 同上 | High | ❌ |
| 4 | `"placeholder"` | 同上 | High | ❌ |
| 5 | `"lorem"` / `"ipsum"` | 同上 | Medium | ❌ |
| 6 | `"sample"` | 同上 | Medium | ⚠️ 文脈確認 |

---

## 2. アーキテクチャ準拠チェック

### 2.1 レイヤー依存方向チェック

**ルール**: `Endpoints → Services → Repositories` の一方向依存を厳守

```bash
# Endpoints が Repository を直接参照していないか
grep -rn "IRepository\|Repository" --include="*.cs" {ServiceDir}/Endpoints/

# Repository が Service/Endpoints に依存していないか
grep -rn "IService\|Service\|Endpoint" --include="*.cs" {ServiceDir}/Repositories/
# （ただし ServiceScopeFactory 等の .NET 標準は除外）
```

### 2.2 DI 登録チェック

```bash
# Program.cs に全 Service/Repository の DI 登録があるか
grep -n "AddScoped\|AddTransient\|AddSingleton" {ServiceDir}/Program.cs
```

確認項目:
- 全 `IXxxService` → `XxxService` のマッピングが登録されていること
- 全 `IXxxRepository` → `XxxRepository` のマッピングが登録されていること
- `DbContext` が登録されていること
- FluentValidation バリデーターが登録されていること
- BackgroundService が登録されていること（該当する場合）

### 2.3 CancellationToken 伝搬チェック

```bash
# async メソッドで CancellationToken が省略されていないか
grep -n "async Task" --include="*.cs" {ServiceDir}/Services/ | grep -v "CancellationToken"
grep -n "async Task" --include="*.cs" {ServiceDir}/Repositories/ | grep -v "CancellationToken"
```

### 2.4 ログ形式チェック

```bash
# 文字列補間ログ（禁止パターン）
grep -rn 'Log.*\$"' --include="*.cs" {ServiceDir}/
grep -rn 'Log.*\$@"' --include="*.cs" {ServiceDir}/

# メッセージテンプレート（正しいパターン）— これが使用されていること
grep -rn 'Log.*"{.*}"' --include="*.cs" {ServiceDir}/
```

---

## 3. EF Core エンティティチェック

### 3.1 命名規則チェック

```bash
# [Table] 属性が snake_case であるか
grep -n '\[Table(' --include="*.cs" {ServiceDir}/Models/

# [Column] 属性が snake_case であるか
grep -n '\[Column(' --include="*.cs" {ServiceDir}/Models/
```

検証ルール:
- テーブル名: `snake_case` 複数形（例: `users`, `order_items`）
- カラム名: `snake_case`（例: `created_at`, `user_id`）
- PascalCase のテーブル名/カラム名は ❌

### 3.2 監査カラムチェック

全エンティティに以下が含まれること:
- `CreatedAt` (`[Column("created_at")]`)
- `UpdatedAt` (`[Column("updated_at")]`)

### 3.3 コレクション初期化チェック

```bash
# コレクションプロパティが = [] で初期化されているか
grep -n "ICollection\|IList\|List<" --include="*.cs" {ServiceDir}/Models/
```

---

## 4. テストコード品質チェック

### 4.1 テスト命名チェック

```bash
# Should_xxx_When_xxx パターンに従っているか
grep -n "\[Fact\]\|public async Task\|public void" --include="*.cs" {TestDir}/
```

### 4.2 AAA パターンチェック

テストファイルを読み取り、以下が含まれること:
- `// Arrange` コメント
- `// Act` コメント
- `// Assert` コメント

### 4.3 アサーション品質チェック

```bash
# Shouldly を使用しているか（Assert.Equal 等は非推奨）
grep -c "ShouldBe\|ShouldNotBeNull\|ShouldThrow" --include="*.cs" {TestDir}/
grep -c "Assert\." --include="*.cs" {TestDir}/
```

### 4.4 アサーションなしテストチェック

テストメソッドにアサーション（`ShouldBe`, `Assert`, `Received`）が含まれていないものを検出。

---

## 5. セキュリティチェック

### 5.1 認可設定チェック

```bash
# RequireAuthorization / AllowAnonymous が全エンドポイントにあるか
grep -n "MapGet\|MapPost\|MapPut\|MapDelete\|MapPatch" --include="*.cs" {ServiceDir}/Endpoints/
grep -n "RequireAuthorization\|AllowAnonymous" --include="*.cs" {ServiceDir}/Endpoints/
```

### 5.2 バリデーションチェック

```bash
# [FromBody] で受け取るエンドポイントにバリデーションがあるか
grep -n "FromBody" --include="*.cs" {ServiceDir}/Endpoints/
grep -n "IValidator\|ValidateAsync\|ValidationProblem" --include="*.cs" {ServiceDir}/Endpoints/
```

### 5.3 Mass Assignment チェック

```bash
# エンティティを直接 [FromBody] で受け取っていないか
grep -rn "\[FromBody\].*[^D][^T][^O]" --include="*.cs" {ServiceDir}/Endpoints/
```

---

## 6. 設定ファイルチェック

### 6.1 appsettings.json

```bash
# 秘密情報が含まれていないか
grep -n "Password\|Secret\|ApiKey\|Token\|ConnectionString.*=" {ServiceDir}/appsettings*.json
```

### 6.2 Program.cs ミドルウェア順序

以下の順序が守られていること（目視確認）:
1. `UseExceptionHandler()`
2. `UseHsts()` / `UseHttpsRedirection()`
3. `UseSerilogRequestLogging()`
4. `UseCors()`
5. `UseAuthentication()`
6. `UseAuthorization()`
7. `UseRateLimiter()`
8. `Map*Endpoints()` / `MapHealthChecks()`
