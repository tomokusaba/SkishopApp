# ソースコードレビュー統合レポート

## 判定結果
- **対象**: `Services/UserManagementService/` 全ソースコード（87 .cs ファイル、.csproj、appsettings*.json、Dockerfile）
- **判定**: ❌ **Rejected** — Critical 指摘 4 件検出（自動判定）
- **レビュー日時**: 2025-07-25 (イテレーション 1)
- **プロジェクト**: SkiShop (.NET 10 マイクロサービス EC サイト)

---

## 🚨 CRITICAL 指摘検出

Critical 指摘が 4 件検出されたため、判定は自動的に **❌ Rejected** となります。Critical 指摘の修正完了後に再レビューを実施してください。

---

## 指摘サマリー

| Agent | 判定 | Critical | High | Medium | Low | スコア |
|-------|------|:--------:|:----:|:------:|:---:|--------|
| tech-lead | ⚠️ Warning | 0 | 4 | 5 | 2 | 23/25 |
| architecture-reviewer | ⚠️ Warning | 0 | 3 | 4 | 2 | 17/20 |
| ddd-domain-reviewer | ⚠️ Warning | 2 | 7 | 5 | 2 | 16/30 |
| api-endpoint-reviewer | ⚠️ Warning | 0 | 3 | 6 | 2 | 21/25 |
| csharp-standards-reviewer | ⚠️ Warning | 0 | 4 | 5 | 2 | 24/25 |
| async-concurrency-reviewer | ⚠️ Warning | 0 | 2 | 2 | 2 | 22/25 |
| error-logging-reviewer | ⚠️ Warning | 1 | 2 | 3 | 1 | 21/25 |
| data-access-reviewer | ⚠️ Warning | 0 | 5 | 5 | 2 | 19/25 |
| config-di-reviewer | ⚠️ Warning | 0 | 2 | 3 | 1 | 23/25 |
| security-reviewer | ⚠️ Warning | 1 | 6 | 6 | 3 | 41/55 |
| dependency-reviewer | ⚠️ Warning | 0 | 3 | 3 | 1 | 19/25 |
| test-quality-reviewer | ⚠️ Warning | 0 | 8 | 5 | 1 | 15/25 |
| performance-reviewer | ⚠️ Warning | 0 | 7 | 6 | 3 | 19/25 |
| resilience-reviewer | ⚠️ Warning | 0 | 4 | 4 | 2 | 18/25 |
| **合計（重複込み）** | | **4** | **60** | **62** | **26** | |
| **合計（重複排除後）** | | **4** | **~48** | **~52** | **~22** | |

> **注記**: 複数 Agent が同一問題を異なる観点で指摘しているケースがあり、重複排除後の合計は概算値です。重複排除の詳細は「重複統合記録」セクションを参照してください。

---

## 判定根拠
- **判定ルール適用結果**: Critical 指摘 4 件 → ❌ Rejected（自動判定）
- **最も重大な指摘**: AES-CBC（認証なし暗号）によるデータエクスポートの暗号化 — パディングオラクル攻撃により全ユーザー PII が復号されるリスク
- **総合評価**: 禁止事項（Console.WriteLine、catch握り潰し、秘密情報ハードコード等）は全項目クリアし、レイヤー依存方向・ミドルウェア順序・DI 登録パターンは模範的。ただし DDD 境界の漏洩、暗号化方式の脆弱性、テストカバレッジ不足（推定 25-30%）が品質基準を下回る

---

## Critical 指摘一覧（修正必須）

| # | 出典 Agent | カテゴリ | 対象ファイル | 行番号 | 指摘内容 | 修正コード例 |
|---|-----------|---------|------------|--------|----------|------------|
| C-1 | security | A02 / Tampering | `Services/UserManagementService/BackgroundServices/DataExportService.cs` | L68-80 | **AES-CBC without HMAC**: データエクスポートの暗号化が AES-256-CBC + IV 結合のみで認証タグなし。攻撃者が暗号文を入手した場合、パディングオラクル攻撃により全ユーザーの PII を復号可能。→ **AES-GCM（認証付き暗号）に変更必須** | 下記 §修正例 C-1 参照 |
| C-2 | ddd-domain | Aggregate 境界違反 | `Services/UserManagementService/Repositories/WishlistRepository.cs` | `FindItemsByProductIdWithRestockNotifyAsync` | **WishlistItem を Aggregate Root（Wishlist）を経由せず直接クエリ**: `WishlistRepository` が `WishlistItem` を ProductId で直接検索しており、Wishlist Aggregate の境界を破壊。DDD の「Aggregate Root 経由でのみ子エンティティを操作」原則に違反 | Wishlist 経由のフィルタリングに変更、または RestockNotification を別 Aggregate に分離 |
| C-3 | ddd-domain | Aggregate 境界違反 | `Services/UserManagementService/Services/DsrService.cs` | 全体 | **DeletionRequest の状態遷移ロジックが Service に漏洩**: `DsrService` が `DeletionRequest` の `Status`・`ApprovedAt`・`CompletedAt` を直接プロパティ代入で変更。状態遷移のビジネスルール（例: Pending→Approved→Processing→Completed の遷移制約）がドメインモデルに集約されていない | `DeletionRequest.Approve()`, `Complete()`, `Cancel()` 等のドメインメソッド追加 |
| C-4 | error-logging, security | 例外ハンドリング | `Services/UserManagementService/Infrastructure/Persistence/AppDbContext.cs` | `SaveChangesAsync` override | **`DbUpdateConcurrencyException` のログ出力なし**: `SaveChangesAsync` オーバーライドで `DbUpdateConcurrencyException` を `ConcurrencyException` に変換しているが、catch ブロック内でログ出力がない。楽観的ロック競合がサイレントに発生し、運用監視で検知不可 | `_logger.LogWarning(ex, "楽観的ロック競合: {EntityType}", ...)` を catch ブロックに追加 |

### 修正例 C-1: AES-GCM への変更

```csharp
private byte[] EncryptData(byte[] data)
{
    var key = Convert.FromBase64String(exportOptions.Value.EncryptionKeyBase64);
    var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
    RandomNumberGenerator.Fill(nonce);
    var tag = new byte[AesGcm.TagByteSizes.MaxSize];     // 16 bytes
    var ciphertext = new byte[data.Length];

    using var aesGcm = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
    aesGcm.Encrypt(nonce, data, ciphertext, tag);

    // Nonce(12) + Tag(16) + CipherText を連結
    var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
    Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
    Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
    Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);
    return result;
}
```

---

## High 指摘一覧（修正推奨）

### セキュリティ関連

| # | 出典 Agent | 対象ファイル | 指摘内容 |
|---|-----------|------------|----------|
| H-1 | security | `BackgroundServices/OrderConfirmedConsumer.cs` L26-33 | **Kafka `order.confirmed` リプレイ攻撃**: 冪等性チェックなし。同一注文イベント再送で年間購入額を不正加算 → 不正ランク昇格（Platinum）が可能。処理済み OrderId の記録テーブル導入が必要 |
| H-2 | security | `Models/MemberRank.cs` L58-69 | **負の購入金額によるランク操作**: `AddPurchaseAmount` に金額下限チェックなし。Kafka 経由で `TotalAmount: -999999` を送信し `AnnualPurchaseAmount` をリセット可能。`ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount)` を追加 |
| H-3 | security, config-di, resilience, dependency, tech-lead | `Program.cs` L109-115 | **レート制限が定義済みだが未適用**: `"api"` 固定ウィンドウリミッター（300/min）を登録しているが、どのエンドポイントグループにも `.RequireRateLimiting("api")` が設定されていない。DoS・列挙攻撃のリスク |
| H-4 | security | `Services/EventPublisherService.cs` L75-87 | **Email 平文が Kafka Outbox に格納**: `PublishDeletionNotificationAsync` で `email` を平文シリアライズし Kafka に発行。Kafka ACL 突破や DLT 経由で PII 漏洩。→ UserId のみ伝搬に変更推奨 |
| H-5 | security, tech-lead | `DTOs/Responses/UserResponses.cs` L55-61 | **ActivityDto に IP アドレスが含有**: 一般ユーザー向け API レスポンスに `IpAddress` が露出。管理者のみ閲覧可能に変更、またはマスキング処理を追加 |
| H-6 | security, performance | `Services/WishlistService.cs` L74-87 | **ウィッシュリストアイテム数の上限なし**: ウィッシュリスト数は上限 10 だがアイテム数制限なし。攻撃者が大量 AddItem リクエストで DB 圧迫可能。`MaxItemsPerWishlist` 制限の追加が必要 |

### アーキテクチャ・DDD 関連

| # | 出典 Agent | 対象ファイル | 指摘内容 |
|---|-----------|------------|----------|
| H-7 | architecture | `BackgroundServices/OutboxPublisher.cs`, `DsrTimeoutMonitorService.cs`, `MemberRankEvaluationService.cs` | **BackgroundService が Repository を直接参照**: Endpoints→Services→Repositories の依存方向に対し、BackgroundService が Service 層をバイパスして Repository を直接操作。ビジネスロジックの散逸・テスタビリティ低下のリスク |
| H-8 | ddd-domain | `Models/Address.cs` | **Anemic Model（Address）**: ドメインメソッドなし、全プロパティが public setter。住所変更のバリデーション（例: `SetDefault()` で他住所の isDefault 解除）が Service に漏洩 |
| H-9 | ddd-domain | `Models/Consent.cs` | **Anemic Model（Consent）**: 同意更新ロジック（`Grant()` / `Revoke()` + IP/UserAgent 記録）が Service に漏洩。ドメインモデルに集約すべき |
| H-10 | ddd-domain | 全 Models | **Value Object 未定義**: Email, PhoneNumber, PostalCode 等が string 型のまま。不変性・バリデーションが保証されない。`record EmailAddress`, `record PhoneNumber` 等の導入を推奨 |
| H-11 | ddd-domain | 各 Model | **Aggregate ナビゲーションプロパティの未カプセル化**: `User.Addresses`, `User.Activities` 等のコレクションが `ICollection<T>` で public setter。外部から直接操作可能 |

### OpenAPI・設定・DI 関連

| # | 出典 Agent | 対象ファイル | 指摘内容 |
|---|-----------|------------|----------|
| H-12 | api-endpoint, tech-lead | `Program.cs` L276 | **`app.MapOpenApi()` 未呼出**: `AddOpenApi()` は登録済みだが `MapOpenApi()` がない。AGENTS.md §6.0 に違反。`/openapi/v1.json` エンドポイントが公開されない |
| H-13 | config-di, tech-lead | `Configurations/KafkaSettings.cs` L6 / `appsettings.json` L19 | **KafkaSettings.GroupId と appsettings の ConsumerGroupId 名不一致**: `Bind()` によるマッピングが失敗し常にデフォルト値が使用される。設定変更時のバグ原因 |
| H-14 | config-di | `Program.cs` | **JwtSettings に ValidateOnStart 未設定**: JWT 設定の不備（空文字の Secret 等）がアプリ起動時に検出されず、初回リクエスト時に 500 エラーとなるリスク |
| H-15 | resilience, config-di, dependency, tech-lead | `Program.cs` L222 | **Redis ヘルスチェック未登録**: `.csproj` に `AspNetCore.HealthChecks.Redis` が未追加。Readiness チェックで Redis 障害を検知不可 |

### データアクセス・パフォーマンス関連

| # | 出典 Agent | 対象ファイル | 指摘内容 |
|---|-----------|------------|----------|
| H-16 | data-access, performance | `Repositories/UserRepository.cs` L10-18 | **`FindByIdAsync` / `FindByIdWithDetailsAsync` に AsNoTracking 未適用**: 読み取り専用と更新で共用されており、読み取り時に不要な Change Tracker 負荷。読み取り専用メソッドの分離を推奨 |
| H-17 | data-access, performance | `Repositories/MemberRankRepository.cs` L20-22 | **`FindByUserIdAsync` に AsNoTracking 未適用**: 同上の問題 |
| H-18 | data-access, performance | `Repositories/DeletionRequestRepository.cs` L18-34 | **3 メソッドが Take() なしの全件取得**: `FindExpiredGracePeriodAsync`、`FindTimedOutProcessingAsync`、`FindByStatusAsync` が無制限クエリ。大量蓄積時のメモリ圧迫リスク |
| H-19 | performance | `Repositories/UserRepository.cs` L24-39 | **FindAllAsync で Select プロジェクション未使用**: PasswordHash 等の不要カラムを含む全カラム取得。DB レベルのプロジェクション推奨 |
| H-20 | performance | `Repositories/ActivityRepository.cs` L10-24 | **FindByUserIdAsync で Select プロジェクション未使用**: 全カラム取得後に Service 層で DTO 変換。DB プロジェクションで転送量削減可能 |
| H-21 | performance | `Endpoints/AddressEndpoints.cs` L27 | **Address API にページネーションなし**: MaxAddressesPerUser=10 でガードされているため実害は限定的だが API 設計の一貫性に欠ける |

### 非同期・並行処理関連

| # | 出典 Agent | 対象ファイル | 指摘内容 |
|---|-----------|------------|----------|
| H-22 | async-concurrency | `Repositories/OutboxEventRepository.cs`, `MemberRankRepository.cs` | **PostgreSQL Advisory Lock のセッション管理**: EF Core の接続プーリングにより `pg_advisory_lock` のセッションスコープが不安定。接続リサイクル時にロックが意図せず解放されるリスク |

### エラー処理・ログ関連

| # | 出典 Agent | 対象ファイル | 指摘内容 |
|---|-----------|------------|----------|
| H-23 | error-logging | `Infrastructure/Persistence/AppDbContext.cs` | **AppDbContext の catch ブロックにログ出力なし**: `DbUpdateConcurrencyException` を `ConcurrencyException` に変換する際、ログ出力がない（Critical C-4 と関連） |
| H-24 | error-logging | 全 BackgroundServices（Kafka Consumer） | **Kafka Consumer で Correlation ID が伝搬されていない**: Kafka メッセージヘッダーから `X-Correlation-Id` を取得・`LogContext.PushProperty` する処理がない |

### 耐障害性関連

| # | 出典 Agent | 対象ファイル | 指摘内容 |
|---|-----------|------------|----------|
| H-25 | resilience | 全 Kafka Consumer | **ConsumeException 発生時のバックオフなし**: catch ブロックで即座にループ再開。Kafka ブローカー障害時にタイトループで CPU を消費。5 秒等のバックオフ追加を推奨 |
| H-26 | resilience | 全 Kafka Consumer | **Dead Letter Topic (DLT) 未実装**: デシリアライズ失敗・ビジネスロジック例外で処理不能なメッセージの退避先がない。障害分析が困難 |
| H-27 | resilience | `Services/CacheService.cs` | **catch (RedisConnectionException) が限定的すぎる**: `RedisTimeoutException`, `RedisServerException` 等の Redis 例外がキャッチされない |

### C# 規約関連

| # | 出典 Agent | 対象ファイル | 指摘内容 |
|---|-----------|------------|----------|
| H-28 | csharp-standards | `Models/User.cs`, `Models/WishlistItem.cs` | **bool プロパティの命名規約違反**: `ProcessingRestricted` → `IsProcessingRestricted`, `DataExportRequested` → `IsDataExportRequested`, `NotifyOnRestock` → `ShouldNotifyOnRestock`。AGENTS.md §4.1「bool プロパティは Is/Has/Can/Should プレフィックス」に違反 |

### NuGet 依存関係

| # | 出典 Agent | 対象ファイル | 指摘内容 |
|---|-----------|------------|----------|
| H-29 | dependency | `UserManagementService.csproj` | **StackExchange.Redis のバージョン固定 (2.12.14)**: AGENTS.md §8.1 ではワイルドカード `Version="2.*"` を推奨。パッチアップデートが自動適用されない |
| H-30 | dependency | `UserManagementService.csproj` | **OpenTelemetry パッケージのバージョン固定 (1.11.2, 1.11.1)**: 同上 |

### 機能完全性

| # | 出典 Agent | 対象ファイル | 指摘内容 |
|---|-----------|------------|----------|
| H-31 | tech-lead | `BackgroundServices/DataExportService.cs` L32-47 | **DataExportService が暗号化後にデータを破棄**: 暗号化済みデータを Blob Storage 等に保存する処理が未実装。ユーザーがエクスポートデータを取得する手段がない |
| H-32 | tech-lead | `Infrastructure/Persistence/AppDbContext.cs` L163 | **outbox_events テーブルの FAILED 部分インデックス未実装**: 設計書で要求されているが `HasFilter("status = 'FAILED'")` が未定義。失敗イベントのリトライ検索性能に影響 |

### テスト品質

| # | 出典 Agent | 対象 | 指摘内容 |
|---|-----------|------|----------|
| H-33 | test-quality | (欠損) | **WishlistService (8 メソッド) のテストなし** |
| H-34 | test-quality | (欠損) | **PreferenceService (3 メソッド) のテストなし** |
| H-35 | test-quality | (欠損) | **ActivityService (2 メソッド) のテストなし** |
| H-36 | test-quality | (欠損) | **Validator 6 クラスのテストなし**: UpdateAddress, UpdateWishlist, AddWishlistItem, UpdateUserStatus, UpdateProcessingRestriction, CreateDeletionRequest 等 |
| H-37 | test-quality | (欠損) | **統合テストが `/health` のみ**: 9 エンドポイントクラスの統合テストが欠損。FakeAuthHandler は用意済みだが未活用 |
| H-38 | test-quality | (欠損) | **Repository テスト 0/10**: Testcontainers.PostgreSql は .csproj に含まれるが一切使用されていない |
| H-39 | test-quality | (欠損) | **BackgroundService テスト 0/9**: OutboxPublisher, DeletionRequestProcessor, 5 Kafka Consumer, DataExportService, MemberRankEvaluationService が全て未テスト |
| H-40 | test-quality | `Fixtures/CustomWebApplicationFactory.cs` L36-37 | **統合テストで InMemory DB 使用**: PostgreSQL 方言非対応。Testcontainers.PostgreSql への置換を推奨 |

### API 設計

| # | 出典 Agent | 対象ファイル | 指摘内容 |
|---|-----------|------------|----------|
| H-41 | api-endpoint | `Endpoints/UserEndpoints.cs` | **ページネーションバリデーションが両方のエラーを常に返す**: pageSize と page の両方が無効な場合、2 つのバリデーションエラーが返されるが、最初のエラーのみ返すべき場合がある |
| H-42 | api-endpoint | `Endpoints/PreferenceEndpoints.cs` | **パスパラメータ `{id}` vs `{userId}` の不整合**: 他のエンドポイントは `{userId}` だが PreferenceEndpoints のみ `{id}` を使用 |

---

## エスカレーション事項（要人間判断）

| # | 優先度 | 出典 Agent | 内容 | 推奨判断者 |
|---|--------|-----------|------|-----------|
| E-1 | 最優先 | security | **AES-GCM への暗号方式変更**: 既存の CBC 暗号化データが存在する場合、マイグレーション戦略（旧データの再暗号化 or 並行復号サポート）の設計判断が必要 | セキュリティアーキテクト |
| E-2 | 高 | tech-lead | **DataExportService のストレージ方針**: 暗号化済みデータの保存先（Blob Storage / DB / ファイルシステム）とユーザーへの配信手段（ダウンロード URL / メール添付）の設計判断が必要 | プロダクトオーナー + インフラチーム |
| E-3 | 高 | performance | **FindByIdAsync の読み取り/更新共用問題**: 多くの Repository で同一メソッドが読み取り・更新の両方に使用されている。AsNoTracking 版の分離は全 Repository インターフェース変更を伴う | テックリード |
| E-4 | 高 | ddd-domain | **RestockNotification の Aggregate 設計**: WishlistItem を ProductId で横断検索する要件と Wishlist Aggregate 境界の整合性。RestockNotification を別 Aggregate に分離するか、CQRS リードモデルで対応するかの設計判断が必要 | ドメインエキスパート + テックリード |
| E-5 | 通常 | test-quality | **テストカバレッジ 25-30% → 80% への工数見積**: Service 5/11、Validator 3/9、Endpoint 0/9、Repository 0/10、BackgroundService 0/9 の大幅な不足。既存テストの品質は高い（命名・AAA・Shouldly 全て模範的）ため、テンプレートを流用した効率的な追加が可能 | テックリード + QA |
| E-6 | 通常 | resilience, tech-lead | **Redis ヘルスチェック追加の優先度**: Redis 障害時に CacheService は graceful degradation（DB フォールバック）を実装済み。ビジネス影響度を判断した上で対応優先度を決定 | SRE |

---

## 競合解決記録

Phase 3.3 の競合検出において、Agent 間で矛盾する推奨が検出されなかったため、Phase 4（競合解決）は実行されませんでした。

以下の軽微なテンション（競合未満の設計トレードオフ）を記録します:

| # | Agent A | Agent B | 内容 | 判断 |
|---|---------|---------|------|------|
| 1 | ddd-domain（Aggregate 境界厳守） | performance（クロス Aggregate クエリの効率性） | `FindItemsByProductIdWithRestockNotifyAsync` は DDD 境界を破るが、性能上必要な横断クエリ | エスカレーション E-4 に委譲。CQRS リードモデルまたは別 Aggregate 分離で両立可能 |
| 2 | security（ActivityDto から IP 除去: High） | tech-lead（同指摘: Low） | 重要度にギャップがあるが方向性は同一 | 最も高い重要度（High）を採用 |

---

## 重複統合記録

以下の指摘は複数 Agent が異なる観点から同一問題を検出しており、統合しました:

| 統合後の指摘 | 出典 Agent（重複元） | 採用重要度 |
|------------|-------------------|----------|
| H-3: レート制限未適用 | security (H) + config-di (M) + resilience (H) | High |
| H-5: ActivityDto に IP アドレス | security (H) + tech-lead (L) | High |
| H-6: Wishlist アイテム上限なし | security (H) + performance (H) | High |
| H-12: MapOpenApi() 未呼出 | api-endpoint (H) + tech-lead (H) | High |
| H-13: KafkaSettings 名不一致 | config-di (H) + tech-lead (M) | High |
| H-15: Redis ヘルスチェック | resilience (H) + config-di (M) + dependency (H) + tech-lead (H) | High |
| H-16: AsNoTracking 未適用 | data-access (H) + performance (H) | High |
| H-18: DeletionRequest 無制限クエリ | data-access (H) + performance (H) | High |
| C-4: ConcurrencyException ログなし | error-logging (C) + security (M) | Critical |

---

## 設計書との照合結果

### 設計書との適合（合格項目）

| 設計書セクション | 適合状況 |
|----------------|---------|
| `user-management-design.md` §4 API 設計 | ✅ 全 API パス・HTTP メソッド・認証要件が完全一致 |
| 同上 §5 イベント設計 | ✅ 発行 5 種 + 購読 5 種が全て実装 |
| 同上 §3 クラス構成 | ✅ 全レイヤーが規約通りに配置 |
| `AGENTS.md` §2.2 ディレクトリ構成 | ✅ 準拠 |
| `AGENTS.md` §11.3 ミドルウェア順序 | ✅ 完全一致 |
| `AGENTS.md` §4.1 命名規則 | ⚠️ bool プロパティ以外は完全準拠 |
| `AGENTS.md` §4.2 禁止事項チェックリスト | ✅ 全 10 項目クリア |

### 設計書からの逸脱

| 項目 | 設計書の要件 | 実装の状態 | 関連指摘 |
|------|-----------|----------|---------|
| OpenAPI エンドポイント | `AddOpenApi()` + `MapOpenApi()` 両方必要 (AGENTS.md §6.0) | `AddOpenApi()` のみ。`MapOpenApi()` 未呼出 | H-12 |
| outbox_events FAILED インデックス | `status = 'FAILED'` 部分インデックス定義 | PENDING インデックスのみ定義。FAILED 未実装 | H-32 |
| Redis ヘルスチェック | NuGet パッケージ一覧に HealthChecks.Redis 記載 | `.csproj` 未追加、HealthChecks 未登録 | H-15 |
| テストカバレッジ 80% | AGENTS.md §9.4 | 推定 25-30% | H-33〜H-40 |
| Value Object | AGENTS.md §3.2 / copilot-instructions.md | 未定義（Email, PhoneNumber 等が string 型） | H-10 |

### 未実装の設計要素

| 機能 | 設計書の記載 | 実装状況 |
|------|-----------|---------|
| データエクスポートのストレージ・配信 | 暗号化 + 保存 + ユーザー配信 | 暗号化のみ実装。保存・配信パスなし（H-31） |
| Dead Letter Topic | Kafka Consumer の障害メッセージ退避 | 未実装（H-26） |

---

## 模範的な実装（Good Practices）

以下の実装は特に品質が高く、他サービスへの展開を推奨します:

| 項目 | 内容 |
|------|------|
| **禁止事項ゼロ** | Console.WriteLine、catch 握り潰し、秘密情報ハードコード等、全 10 項目の禁止事項に違反なし |
| **Wishlist Aggregate Root** | `AddItem()` / `RemoveItem()` による子エンティティ操作の正確な集約 |
| **MemberRank ドメインロジック** | `AddPurchaseAmount()` / `EvaluateAnnual()` のビジネスルール集約が模範的 |
| **BackgroundService パターン統一** | 全 9 サービスで `IServiceScopeFactory` + `stoppingToken` 伝搬 + Advisory Lock 排他制御 |
| **Outbox パターン** | 動的バックオフ（100ms〜5s）実装、Aggregate Root 単位のイベント設計 |
| **TimeProvider DI** | テスタビリティのための `TimeProvider` 注入が全サービスで統一 |
| **セキュリティヘッダー完備** | X-Content-Type-Options, X-Frame-Options, CSP, Referrer-Policy, Permissions-Policy, HSTS 全て適切 |
| **テスト命名・AAA パターン** | 既存テストは全て `Should_X_When_Y` 準拠、Shouldly アサーション統一 |

---

## 各 Agent 詳細レポート

<details>
<summary>tech-lead レビューレポート</summary>

- **判定**: ⚠️ Warning
- **指摘件数**: Critical: 0 / High: 4 / Medium: 5 / Low: 2
- **総合スコア**: 23/25

**禁止事項チェック**: 全 10 項目クリア（Console.WriteLine, catch 握り潰し, 秘密情報, FromSqlRaw, new Service(), .Result/.Wait(), Thread.Sleep(), [Inject], DateTime.Now, new HttpClient()）

**主な指摘**:
1. (H) `app.MapOpenApi()` 未呼出 — AGENTS.md §6.0 違反
2. (H) `outbox_events` FAILED 部分インデックス未実装
3. (H) DataExportService — 暗号化後のデータ保存・配信パスなし
4. (H) Redis ヘルスチェック未登録
5. (M) KafkaSettings.GroupId と appsettings の ConsumerGroupId 名不一致
6. (M) CacheService — Redis 操作に CancellationToken 未伝搬
7. (M) InitializeProfileAsync — 未使用メソッド（デッドコード）
8. (M) PublishProfileUpdatedAsync — 固定フィールドリスト（実際の変更フィールドを動的に収集すべき）
9. (M) DsrTimeoutMonitorService — 冗長なタイムアウト再チェック

**総評**: 禁止事項ゼロ、レイヤー依存方向厳守、DDD パターンの正確な適用、TimeProvider DI 統一等、技術標準に忠実。
</details>

<details>
<summary>architecture-reviewer レビューレポート</summary>

- **判定**: ⚠️ Warning
- **指摘件数**: Critical: 0 / High: 3 / Medium: 4 / Low: 2
- **総合スコア**: 17/20

**主な指摘**:
1. (H) OutboxPublisher が IOutboxEventRepository を直接参照（Service 層バイパス）
2. (H) DsrTimeoutMonitorService が IDeletionRequestRepository を直接参照
3. (H) MemberRankEvaluationService が IMemberRankRepository を直接参照
4. (M) BackgroundService 群の共通パターンがベースクラスに抽出されていない
5. (M) Infrastructure/ 配下のクラス配置が設計規約と微妙に異なる

</details>

<details>
<summary>ddd-domain-reviewer レビューレポート</summary>

- **判定**: ⚠️ Warning
- **指摘件数**: Critical: 2 / High: 7 / Medium: 5 / Low: 2
- **総合スコア**: 16/30

**Critical 指摘**:
1. WishlistItem を Aggregate Root 経由でなく直接 Repository クエリ
2. DeletionRequest 状態遷移ロジックが Service に漏洩

**主な High 指摘**:
- Address, Consent モデルが Anemic（ドメインメソッドなし）
- Value Object 未定義（Email, PhoneNumber, PostalCode）
- Aggregate ナビゲーションプロパティの未カプセル化
- User Aggregate Root に AddAddress / RemoveAddress メソッドなし

**評価**: Wishlist・MemberRank は DDD パターンを正確に適用。一方で Address, Consent, DeletionRequest が Anemic Model であり、DDD 適合度にムラがある。
</details>

<details>
<summary>api-endpoint-reviewer レビューレポート</summary>

- **判定**: ⚠️ Warning
- **指摘件数**: Critical: 0 / High: 3 / Medium: 6 / Low: 2
- **総合スコア**: 21/25

**主な指摘**:
1. (H) `MapOpenApi()` 未呼出
2. (H) ページネーションバリデーションの不整合
3. (H) PreferenceEndpoints のパスパラメータ `{id}` vs `{userId}` 不整合
4. (M) 各エンドポイントに `.WithName()` が一部欠損
5. (M) Error レスポンスの Problem Details 記述の一貫性

**評価**: Minimal API パターン、IDOR 防止、FluentValidation 統合は模範的。
</details>

<details>
<summary>csharp-standards-reviewer レビューレポート</summary>

- **判定**: ⚠️ Warning
- **指摘件数**: Critical: 0 / High: 4 / Medium: 5 / Low: 2
- **総合スコア**: 24/25

**主な指摘**:
1-3. (H) bool プロパティ命名規約違反: ProcessingRestricted, DataExportRequested, NotifyOnRestock
4. (H) 追加の bool プロパティ命名違反
5-9. (M) 軽微なコーディングスタイル指摘

**評価**: primary constructor, record DTO, パターンマッチング, TimeProvider DI 等の C# 14 機能活用が模範的。禁止パターン検出ゼロ。
</details>

<details>
<summary>async-concurrency-reviewer レビューレポート</summary>

- **判定**: ⚠️ Warning
- **指摘件数**: Critical: 0 / High: 2 / Medium: 2 / Low: 2
- **総合スコア**: 22/25

**主な指摘**:
1. (H) PostgreSQL Advisory Lock — EF Core 接続プーリングとのセッション管理問題
2. (H) Advisory Lock 解放の保証（接続リサイクル時）
3. (M) CacheService — Redis に CancellationToken 未伝搬

**評価**: 全 async メソッドに CancellationToken 定義済み、`await` 統一、`.Result`/`.Wait()` ゼロ。BackgroundService の `stoppingToken` 伝搬も完全。
</details>

<details>
<summary>error-logging-reviewer レビューレポート</summary>

- **判定**: ⚠️ Warning
- **指摘件数**: Critical: 1 / High: 2 / Medium: 3 / Low: 1
- **総合スコア**: 21/25

**Critical 指摘**:
1. AppDbContext.SaveChangesAsync — ConcurrencyException 変換時のログ出力なし

**主な High 指摘**:
1. AppDbContext catch ブロックのログ出力なし
2. Kafka Consumer で Correlation ID が伝搬されていない

**評価**: `ILogger<T>` メッセージテンプレート形式、CompactJsonFormatter、ServiceName エンリッチメントは模範的。
</details>

<details>
<summary>data-access-reviewer レビューレポート</summary>

- **判定**: ⚠️ Warning
- **指摘件数**: Critical: 0 / High: 5 / Medium: 5 / Low: 2
- **総合スコア**: 19/25

**主な指摘**:
1. (H) FindByIdWithDetailsAsync — AsNoTracking 未適用
2. (H) バッチ処理時の ChangeTracker.Clear 未呼出
3-5. (H) DeletionRequest 系の無制限クエリ 3 件
6. (M) Consent, Preference, Wishlist に [Timestamp] 未設定
7. (M) フィルタ付きインデックスの不足

**評価**: EF Core の基本パターン（Include, snake_case Column 属性, DateTimeOffset）は正確。ChangeTracker 管理とクエリ最適化に改善余地。
</details>

<details>
<summary>config-di-reviewer レビューレポート</summary>

- **判定**: ⚠️ Warning
- **指摘件数**: Critical: 0 / High: 2 / Medium: 3 / Low: 1
- **総合スコア**: 23/25

**主な指摘**:
1. (H) JwtSettings — ValidateOnStart 未設定
2. (H) KafkaSettings.GroupId と appsettings.ConsumerGroupId 名不一致
3. (M) Kafka 設定の二重読み込み
4. (M) Redis ヘルスチェック未登録
5. (M) HealthCheck の二重登録を疑う

**評価**: DI 登録（全 Scoped、Singleton 適切）、ミドルウェアパイプライン順序、IOptions パターンは模範的。
</details>

<details>
<summary>security-reviewer レビューレポート</summary>

- **判定**: ⚠️ Warning
- **指摘件数**: Critical: 1 / High: 6 / Medium: 6 / Low: 3
- **総合スコア**: 41/55

**Critical 指摘**:
1. AES-CBC without HMAC — パディングオラクル攻撃リスク

**主な High 指摘**:
1. Kafka order.confirmed リプレイ攻撃（冪等性なし）
2. 負の購入金額によるランク操作
3. レート制限が定義済みだが未適用
4. Email 平文が Kafka Outbox に格納
5. ActivityDto に IP アドレスが一般ユーザーに露出
6. ウィッシュリストアイテム数の上限なし

**OWASP チェック**: A01 ✅, A02 ❌, A03 ✅, A05 ⚠️, A07 ✅, A08 ✅, A10 ✅
**STRIDE**: Spoofing ✅, Tampering ❌, Repudiation ✅, Info Disclosure ⚠️, DoS ⚠️, Elevation ✅

**評価**: 認証・認可（FallbackPolicy + AdminOnly + IDOR 全チェック）とセキュリティヘッダーは模範的。Kafka 経由の攻撃面と暗号化方式に脆弱性。
</details>

<details>
<summary>dependency-reviewer レビューレポート</summary>

- **判定**: ⚠️ Warning
- **指摘件数**: Critical: 0 / High: 3 / Medium: 3 / Low: 1
- **総合スコア**: 19/25

**主な指摘**:
1. (H) HealthChecks.Redis 未追加（.csproj + HealthChecks 登録なし）
2. (H) StackExchange.Redis バージョン固定 (2.12.14 → 2.*)
3. (H) OpenTelemetry パッケージバージョン固定
4. (M) その他のバージョン管理指摘

**評価**: .NET 10 対応パッケージ採用、禁止パッケージ（Newtonsoft.Json, log4net 等）ゼロ。
</details>

<details>
<summary>test-quality-reviewer レビューレポート</summary>

- **判定**: ⚠️ Warning
- **指摘件数**: Critical: 0 / High: 8 / Medium: 5 / Low: 1
- **総合スコア**: 15/25

**カバレッジ状況**:

| レイヤー | 目標 | 推定達成率 | 判定 |
|---------|------|----------|------|
| Service | 80% | ~50% | ❌ |
| Validators | 80% | ~33% | ❌ |
| Endpoints | 80% | ~5% | ❌ |
| Repository | 70% | 0% | ❌ |
| BackgroundService | — | 0% | ❌ |
| **全体** | **80%** | **~25-30%** | **❌** |

**評価**: 既存テストの品質は非常に高い（命名規約 5/5、AAA パターン 4/5、モック品質 4/5）が、テスト種別の網羅性（1/5）とカバレッジ（1/5）が致命的に不足。
</details>

<details>
<summary>performance-reviewer レビューレポート</summary>

- **判定**: ⚠️ Warning
- **指摘件数**: Critical: 0 / High: 7 / Medium: 6 / Low: 3
- **総合スコア**: 19/25

**主な指摘**:
1-2. (H) Wishlist / Address API にページネーションなし
3. (H) DeletionRequest 系の無制限クエリ
4-5. (H) Select プロジェクション未使用（UserRepository, ActivityRepository）
6-7. (H) AsNoTracking 未適用（UserRepository, MemberRankRepository）

**N+1 クエリ**: 検出されず（Include の使用が適切）
**キャッシュ戦略**: Redis フォールバック・TTL 設計は良好（一部無効化漏れあり）
</details>

<details>
<summary>resilience-reviewer レビューレポート</summary>

- **判定**: ⚠️ Warning
- **指摘件数**: Critical: 0 / High: 4 / Medium: 4 / Low: 2
- **総合スコア**: 18/25

**主な指摘**:
1. (H) Redis ヘルスチェック未登録
2. (H) Kafka Consumer の ConsumeException 後にバックオフなし
3. (H) Dead Letter Topic 未実装
4. (H) CacheService の catch (RedisConnectionException) が限定的
5. (M) Redis 接続の Retry/CircuitBreaker ポリシー未設定

**評価**: Outbox パターンの動的バックオフ、Advisory Lock による排他制御は模範的。Kafka Consumer の耐障害性に改善余地。
</details>

---

## 修正優先度ガイド

### 即時修正必須（Critical）
1. **C-1**: AES-CBC → AES-GCM 変更（セキュリティ脆弱性）
2. **C-4**: AppDbContext の ConcurrencyException ログ追加（運用監視）

### 設計検討後に修正（Critical — 設計判断を伴う）
3. **C-2**: WishlistItem Aggregate 境界の再設計（DDD）
4. **C-3**: DeletionRequest ドメインメソッド追加（DDD）

### 早期修正推奨（High — セキュリティ）
5. **H-1**: Kafka 冪等性キー導入
6. **H-2**: 購入金額バリデーション追加
7. **H-3**: レート制限のエンドポイント適用
8. **H-4**: Kafka Outbox から Email 平文除去

### 早期修正推奨（High — 機能・設定）
9. **H-12**: `app.MapOpenApi()` 追加
10. **H-13**: KafkaSettings プロパティ名統一
11. **H-15**: Redis ヘルスチェック追加

### 計画的修正（High — テスト）
12. **H-33〜H-40**: テストカバレッジの段階的拡充

---

*レポート生成: 全 14 Agent による包括的レビュー完了。競合なし。*
