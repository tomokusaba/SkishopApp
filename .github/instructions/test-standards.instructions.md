---
applyTo:
  - "**/*Tests.cs"
  - "**/*Test.cs"
---

# テスト規約 Instructions

本 Instructions は `**/*Tests.cs` および `**/*Test.cs` に自動適用される。テストコードの作成・編集時に以下の規約を遵守すること。

---

## 1. テスト命名

### 命名パターン
- **`Should_期待結果_When_条件`** の命名パターンを推奨する
- テストメソッド名から「何を」「どの条件で」「どうなることを期待するか」が明確に読み取れること

```csharp
// ✅ 良い例: 命名パターンに準拠
[Fact]
public async Task Should_ReturnUser_When_ValidIdProvided() { /* ... */ }

[Fact]
public async Task Should_ThrowNotFoundException_When_UserDoesNotExist() { /* ... */ }

[Fact]
public async Task Should_FailValidation_When_NameIsNull() { /* ... */ }
```

```csharp
// ❌ 悪い例: 何をテストしているか不明
[Fact]
public void Test1() { /* ... */ }

[Fact]
public void TestUser() { /* ... */ }

[Fact]
public void FindById() { /* ... */ }  // テスト対象メソッド名をそのまま使用
```

---

## 2. AAA パターン（Arrange-Act-Assert）

- テストメソッドは **3 つのセクションに明確に分離**する
- セクション間は空行で区切り、コメント（`// Arrange`, `// Act`, `// Assert`）を付ける

| セクション | 役割 | 内容 |
|-----------|------|------|
| **Arrange** | 準備 | テストデータの作成、モックのセットアップ |
| **Act** | 実行 | テスト対象メソッドの呼び出し（**1 メソッド呼び出しのみ**） |
| **Assert** | 検証 | 結果の検証、例外の検証、モック呼び出しの検証 |

```csharp
// ✅ 良い例: AAA パターン
[Fact]
public async Task Should_ReturnUser_When_EmailExists()
{
    // Arrange
    var expectedUser = new User { Id = "1", Email = "test@example.com" };
    _userRepository.FindByEmailAsync("test@example.com", default)
        .Returns(expectedUser);

    // Act
    var result = await _authService.FindByEmailAsync("test@example.com");

    // Assert
    result.ShouldNotBeNull();
    result.Email.ShouldBe("test@example.com");
}
```

```csharp
// ❌ 悪い例: セクション混在
[Fact]
public async Task TestFindUser()
{
    _userRepository.FindByEmailAsync("test@example.com", default)
        .Returns(new User { Email = "test@example.com" });
    var result = await _authService.FindByEmailAsync("test@example.com");
    result.ShouldNotBeNull();
    _userRepository.FindByEmailAsync("other@example.com", default)
        .Returns((User?)null);  // Arrange が混在
    var result2 = await _authService.FindByEmailAsync("other@example.com");
    result2.ShouldBeNull();  // 複数の Act-Assert が混在
}
```

---

## 3. カバレッジ基準

### 目標値
- **分岐カバレッジ 80% 以上**を必須とする
- **全パブリックメソッド**に対して単体テストを作成する

| カバレッジ率 | 判定 |
|---|---|
| 80% 以上 | ✅ 基準達成 |
| 60-79% | ⚠️ 基準未達（テスト追加が必要） |
| 60% 未満 | ❌ 深刻な不足 |

### 優先的にカバーすべきコード
- Service 層のビジネスロジック
- バリデーションロジック
- エラーハンドリング・例外処理パス
- 条件分岐（if / switch）の全分岐

---

## 4. 異常系テストの必須化

- **異常系テストは正常系と同等以上のテストケース数**を作成する
- 以下の異常パターンを網羅すること

### 入力バリデーション

```csharp
// ✅ 必須: null / 空文字 / 境界値のテスト
[Fact]
public async Task Should_ThrowException_When_NameIsNull() { /* ... */ }

[Fact]
public async Task Should_ThrowException_When_NameIsEmpty() { /* ... */ }

[Fact]
public async Task Should_ThrowException_When_NameExceedsMaxLength() { /* ... */ }

[Fact]
public async Task Should_Succeed_When_NameIsExactlyMaxLength() { /* ... */ }  // 境界値
```

### 外部サービス障害

```csharp
// ✅ 必須: 外部依存の障害テスト
[Fact]
public async Task Should_ThrowBusinessException_When_RepositoryThrowsException()
{
    // Arrange
    _userRepository.FindByIdAsync(Arg.Any<string>(), default)
        .ThrowsAsync(new InvalidOperationException("DB connection failed"));

    // Act & Assert
    var act = async () => await _userService.FindByIdAsync("1");
    var ex = await Should.ThrowAsync<BusinessException>(act);
    ex.Message.ShouldContain("ユーザー取得に失敗しました");
}
```

### 境界値テスト

| データ型 | テストすべき値 |
|---------|-------------|
| **数値** | 0, 1, -1, `int.MaxValue`, `int.MinValue`, 上限値, 上限値+1 |
| **文字列** | `null`, `""`, `" "`（空白のみ）, 最大長, 最大長+1, 特殊文字 |
| **コレクション** | `null`, 空リスト, 1 件, 大量件数 |
| **日時** | `null`, 過去日, 未来日, 閏年 2/29, 月末 |

---

## 5. アサーション品質

### Shouldly の使用を推奨
- xUnit の `Assert.Equal` / `Assert.True` よりも **Shouldly の `ShouldBe()` / `ShouldNotBeNull()`** を優先する（エラーメッセージの可読性）

```csharp
// ❌ 非推奨: xUnit の基本アサーション
Assert.NotNull(result);
Assert.Equal("test", result.Name);
Assert.Equal(3, result.Items.Count);

// ✅ 推奨: Shouldly の流暢なアサーション
result.ShouldNotBeNull();
result.Name.ShouldBe("test");
result.Items.Count.ShouldBe(3);
```

### 具体的なアサーション

```csharp
// ❌ 悪い例: 曖昧なアサーション
result.ShouldNotBeNull();  // 値が null でないことしか検証しない

// ✅ 良い例: 具体的な値の検証
result.Name.ShouldBe("田中太郎");
result.Email.ShouldBe("tanaka@example.com");
result.Status.ShouldBe(UserStatus.Active);
```

### 例外テスト
- 例外の型だけでなく、**メッセージ内容も検証**する

```csharp
// ✅ 良い例: 例外の型 + メッセージの検証
var act = async () => await _userService.FindByIdAsync("999");
var ex = await Should.ThrowAsync<NotFoundException>(act);
ex.Message.ShouldContain("User 999 not found");
```

### コレクションのアサーション

```csharp
// ✅ 良い例: コレクションの内容検証
users.Count.ShouldBe(2);
users.ShouldAllBe(u => u.IsActive);
users.ShouldBe(users.OrderByDescending(u => u.CreatedAt));

users.Select(u => u.Name).ToList().ShouldBe(new[] { "Alice", "Bob" });
```

---

## 6. テストの独立性と信頼性

### テストの独立性
- 各テストは**単独で実行可能**であること。他のテストの実行結果に依存しない
- テスト間で**共有状態を持たない**。テストごとにコンストラクタでセットアップする
- テストの実行順序に依存しない

### フレイキーテスト（不安定なテスト）の禁止
- 実行のたびに結果が変わるテストは**テストなしと同等に危険**

| 問題パターン | 原因 | 対策 |
|---|---|---|
| 時刻依存 | `DateTime.UtcNow` の直接使用 | `TimeProvider` をインジェクションし、テストで固定 |
| 乱数依存 | `Random` の使用 | シード値を固定、または入力をパラメータ化 |
| 非同期タイミング | `Thread.Sleep()` でのタイミング調整 | `TaskCompletionSource` / 直接テスト |
| 外部サービス依存 | 実際の DB / API への接続 | モック / Testcontainers を使用 |
| 実行順序依存 | 共有状態の汚染 | コンストラクタでの初期化 |

```csharp
// ❌ 悪い例: 時刻依存テスト
[Fact]
public void Should_BeExpired_When_CreatedOneHourAgo()
{
    var token = new Token(DateTime.UtcNow.AddHours(-1));  // 実行時刻に依存
    token.IsExpired.ShouldBeTrue();
}

// ✅ 良い例: TimeProvider による時刻制御
[Fact]
public void Should_BeExpired_When_CreatedOneHourAgo()
{
    var fakeTime = new FakeTimeProvider(DateTimeOffset.Parse("2026-03-18T12:00:00Z"));
    var token = new Token(fakeTime.GetUtcNow().AddHours(-1).DateTime, fakeTime);
    token.IsExpired.ShouldBeTrue();
}
```

### 常に Pass するテストの禁止
- アサーションなしのテスト、`true.ShouldBeTrue()` のテストは **Critical 違反**

```csharp
// ❌ Critical 違反: アサーションなし（何も検証していない）
[Fact]
public async Task TestCreateUser()
{
    await _userService.CreateAsync(new CreateUserRequest("test", "test@example.com"));
    // アサーションなし
}
```

---

## 7. モック / スタブの適切な使用

### 外部依存のモック化
- データベース、外部 API、メッセージキュー等の**外部依存はモック / スタブで分離**する
- NSubstitute を使用する

```csharp
// ✅ 良い例: モックによる外部依存の分離
public class UserServiceTests
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UserService> _logger;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _logger = Substitute.For<ILogger<UserService>>();
        _userService = new UserService(_userRepository, _logger);
    }

    [Fact]
    public async Task Should_ReturnUser_When_Exists()
    {
        // Arrange
        _userRepository.FindByIdAsync("1", default)
            .Returns(new User { Id = "1", Email = "test@example.com" });

        // Act
        var result = await _userService.FindByIdAsync("1");

        // Assert
        result.ShouldNotBeNull();
        await _userRepository.Received(1).FindByIdAsync("1", default);
    }
}
```

### NSubstitute の Received パターン
- `Received()` で**メソッドが期待通りに呼び出されたことを検証**する
- 呼び出し回数を細かく指定することで、意図しない副作用を検出できる

```csharp
// ✅ 呼び出し回数の検証パターン
await _userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
await _userRepository.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
await _eventPublisher.Received(1).PublishAsync(Arg.Any<OrderCreatedEvent>());
```

### モックの過剰使用の禁止
- テスト対象のクラス自体をモックしない（モックするのは**依存先**のみ）
- モックが 5 つ以上必要な場合、テスト対象クラスの**責務過多**（SRP 違反）を疑う

---

## 8. テスト種別と対応アプローチ

| テスト種別 | フレームワーク | 対象 |
|---------|-------------|------|
| Unit Test | xUnit + NSubstitute + Shouldly | Service, Utility クラス |
| Integration Test（API） | `WebApplicationFactory<Program>` | Minimal API エンドポイント |
| DB スライステスト | Testcontainers.PostgreSql | Repository（実 PostgreSQL） |
| セキュリティテスト | `WebApplicationFactory` + カスタム `AuthenticationHandler` | 認証/認可 |

### テストカテゴリの分類（`[Trait]`）
- 各テストに `[Trait("Category", "...")]` を付与し、CI での選択的実行を可能にする
- `dotnet test --filter "Category=Unit"` でユニットテストのみ実行可能

```csharp
// ✅ テストカテゴリの付与
[Fact]
[Trait("Category", "Unit")]
public async Task Should_ReturnUser_When_Exists() { /* ... */ }

[Fact]
[Trait("Category", "Integration")]
public async Task Should_Return200_When_GetProducts() { /* ... */ }

[Fact]
[Trait("Category", "Security")]
public async Task Should_Return401_When_NoToken() { /* ... */ }
```

### 統合テスト（WebApplicationFactory）

```csharp
// ✅ 良い例: WebApplicationFactory を使用した統合テスト
public class ProductEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ProductEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // テスト用 DB / モック設定
            });
        }).CreateClient();
    }

    [Fact]
    public async Task Should_Return200_When_GetProducts()
    {
        // Act
        var response = await _client.GetAsync("/products");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
```

### DB スライステスト（Testcontainers）

```csharp
// ✅ 良い例: Testcontainers で実 PostgreSQL を使用
public class UserRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        // DbContext 設定・Migration 適用
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Should_FindUser_When_EmailExists()
    {
        // Arrange: テストデータの投入
        // Act: Repository メソッドの呼び出し
        // Assert: 結果の検証
    }
}
```

---

## 9. テストデータ管理

### テストデータの原則
- テストデータは**テストメソッド内で完結**させる
- **本番データ（実在するメールアドレス、電話番号等）をテストコードに含めない**
- パラメタライズドテストで複数パターンを効率的にテストする

```csharp
// ✅ 良い例: パラメタライズドテスト
[Theory]
[InlineData("")]
[InlineData(" ")]
[InlineData("   ")]
public async Task Should_FailValidation_When_NameIsBlank(string invalidName)
{
    var request = new CreateUserRequest(invalidName, "test@example.com");
    var validator = new CreateUserRequestValidator();
    var result = await validator.ValidateAsync(request);
    result.IsValid.ShouldBeFalse();
}

[Theory]
[InlineData("abc", true)]
[InlineData("ab", false)]
[InlineData("", false)]
public async Task Should_ValidateName_Correctly(string name, bool expectedValid)
{
    // ...
}
```

---

## 10. テストの構造

### テストクラスとプロダクションコードの対応
- テストクラスは対象クラスと**同一名前空間構成**に配置する
- テストクラス名は `対象クラス名 + Tests`（例: `UserService` → `UserServiceTests`）

### `[Skip]` テストの管理
- スキップされたテストには**理由と対応予定**を記載する
- 理由なき `[Skip]` は Medium 指摘

```csharp
// ❌ 悪い例: 理由なし
[Fact(Skip = "")]
public async Task Should_SendEmail() { /* ... */ }

// ✅ 良い例: 理由と対応予定あり
[Fact(Skip = "メールサーバーのテスト環境構築待ち。2026-04 対応予定。Issue #42")]
public async Task Should_SendEmail() { /* ... */ }
```

---

## 11. CancellationToken のテスト

- 全ての非同期テストで `CancellationToken` を適切に伝搬する
- キャンセル時の動作もテストする

```csharp
// ✅ キャンセル時の動作テスト
[Fact]
public async Task Should_ThrowOperationCanceled_When_TokenIsCanceled()
{
    // Arrange
    var cts = new CancellationTokenSource();
    cts.Cancel();

    // Act & Assert
    var act = async () => await _userService.FindByIdAsync("1", cts.Token);
    await Should.ThrowAsync<OperationCanceledException>(act);
}
```

---

## 12. 禁止事項チェックリスト

| # | 禁止事項 | 重要度 | 理由 |
|---|---------|--------|------|
| 1 | アサーションなしのテスト | Critical | 何も検証していない（テストとして無意味） |
| 2 | `true.ShouldBeTrue()` | Critical | 常に Pass する偽テスト |
| 3 | テスト内の本番個人情報 | Critical | セキュリティ・コンプライアンス違反 |
| 4 | `Thread.Sleep()` によるタイミング調整 | High | フレイキーテストの原因 |
| 5 | 外部 DB / API への直接接続（モック不使用） | High | テストの再現性・独立性の破壊 |
| 6 | テスト間の共有状態（static 変数等） | High | テストの実行順序依存 |
| 7 | 理由なきの `[Skip]` | Medium | テストカバレッジのサイレントな低下 |
| 8 | 例外テストで型のみ検証（メッセージ未検証） | Medium | 間違った例外の検出漏れ |
| 9 | 1 テストメソッド内の複数 Act-Assert | Medium | テスト失敗時の原因特定が困難 |
| 10 | モック 5 つ以上のテスト | Medium | テスト対象クラスの SRP 違反の疑い |
