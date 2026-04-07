---
name: automatic-implementation-with-phase-finished-verification
description: "実装計画プランに基づくマイクロサービスの段階的自動実装を実行。各フェーズ実装後に完了チェックリストで網羅的に検証し、全項目合格の場合のみ次フェーズに自動進行する。Use when: マイクロサービスの自動実装、フェーズ実装と検証の自動化、段階的な安全な実装実行"
argument-hint: "サービス名を指定。例: authentication-service, inventory-management, payment-cart-service"
---

# automatic-implementation-with-phase-finished-verification — マイクロサービス段階的自動実装 Skill

## 目的

`impl-plan/` 配下の実装計画プランに基づき、指定されたマイクロサービスの **全フェーズを段階的かつ自動的に実装** し、各フェーズ完了後に **完了チェックリストによる網羅的検証** を実施する。検証に合格した場合のみ次フェーズに進行し、不合格の場合は是正→再検証のサイクルを実行する。

### コアコンセプト

```
┌─────────────────────────────────────────────────────────────┐
│  Phase N 開始（Phase 1 〜 Phase N を順に繰り返す）              │
│  ├── Step 1: 実装計画の読み取り                               │
│  ├── Step 2: 設計書の該当セクション特定                        │
│  ├── Step 3: フェーズ内全タスクの実装                          │
│  ├── Step 3.3: 自己検証【必須 — 各フェーズで実行】              │
│  │   └── 8 項目の grep チェックをターミナルで実行（全 0 件必須）  │
│  │       ⛔ 1件でも検出 → 修正して再実行。Step 4 に進めない     │
│  ├── Step 4: ビルド確認（dotnet build）                       │
│  ├── Step 5: 完了チェックリスト検証                            │
│  │   ├── ✅ 全項目 PASS → Phase N+1 へ進行                   │
│  │   └── ❌ FAIL あり → 是正 → 再検証（最大3回）              │
│  └── Step 6: 検証レポート生成・次フェーズへ進行                  │
├─────────────────────────────────────────────────────────────┤
│  全フェーズ完了後【1 回だけ実行】                                │
│  ├── Final Step A: 最終統合チェック（dotnet build + dotnet test）│
│  └── Final Step B: 最終コードレビュー【必須】                    │
│      └── runSubagent で orchest-code-review Agent を 1 回呼出  │
│          ⛔ Critical + High = 0 でなければリリース不可           │
└─────────────────────────────────────────────────────────────┘
```

**Fail-Safe 原則**: 判定に迷う場合は「不合格（要修正）」に倒す。品質を妥協しない。

> **⛔ 最重要ルール**: Step 3.3（自己検証 grep チェック）は **各フェーズ完了時に必ず実行する**。スキップして Step 4 のビルドに進むことは品質ゲートの無効化と同義であり禁止する。`orchest-code-review` エージェントによる包括的コードレビューは **全フェーズ完了後に 1 回だけ** 実行する（各フェーズごとの実行は不要）。

---

## 前提条件

1. 指定されたサービス名に対応する以下のファイルが存在すること:
   - `impl-plan/{service-name}-impl-plan.md` — 実装計画プラン
   - `design-docs/{service-name}-design.md` — 詳細設計書
   - `design-docs/spec.md` — システム全体設計書
2. `.github/instructions/` 配下のコーディング規約ファイルが存在すること
3. `AGENTS.md` が存在し、プロジェクト共通ルールが記載されていること
4. .NET 10 SDK がインストールされていること（`dotnet --version` で確認）

---

## サービス名 → ファイル マッピング

| 引数として指定するサービス名 | 実装計画ファイル | 設計書ファイル | プロジェクトディレクトリ |
|---------------------------|----------------|-------------|---------------------|
| `authentication-service` | `authentication-service-impl-plan.md` | `authentication-service-design.md` | `AuthService/` |
| `user-management` | `user-management-impl-plan.md` | `user-management-design.md` | `UserManagementService/` |
| `inventory-management` | `inventory-management-impl-plan.md` | `inventory-management-design.md` | `InventoryManagementService/` |
| `sales-management` | `sales-management-impl-plan.md` | `sales-management-design.md` | `SalesManagementService/` |
| `payment-cart-service` | `payment-cart-service-impl-plan.md` | `payment-cart-service-design.md` | `PaymentCartService/` |
| `coupon-service` | `coupon-service-impl-plan.md` | `coupon-service-design.md` | `CouponService/` |
| `point-service` | `point-service-impl-plan.md` | `point-service-design.md` | `PointService/` |
| `mailsend-service` | `mailsend-service-impl-plan.md` | `mailsend-service-design.md` | `MailSendService/` |
| `ai-support-service` | `ai-support-service-impl-plan.md` | `ai-support-service-design.md` | `AiSupportService/` |
| `api-gateway` | `api-gateway-impl-plan.md` | `api-gateway-design.md` | `ApiGateway/` |
| `front-end` | `front-end-impl-plan.md` | `front-end-need.md` | `frontend/` |

---

## 実行手順（メインフロー）

### **Step 0: 初期化・前提確認**

1. 指定されたサービス名から、上記マッピングテーブルに基づき対応ファイルを特定する
2. 以下のファイルの存在を確認:
   - `impl-plan/{service}-impl-plan.md`
   - `design-docs/{service}-design.md`（front-end の場合は `front-end-need.md`）
   - `design-docs/spec.md`
3. 実装計画プランを読み取り、**全フェーズ一覧**（Phase 1〜Phase N）を抽出する
4. 各フェーズの以下の情報を一覧化:
   - フェーズ番号と名称
   - 前提条件（依存フェーズ）
   - 実装タスク数
   - 完了チェックリスト項目数
5. SQL データベースに全フェーズを TODO として登録（進捗追跡用）:

```sql
INSERT INTO todos (id, title, description, status) VALUES
  ('{service}-phase-1', 'Phase 1: {phase_name}', '{phase_description}', 'pending'),
  ('{service}-phase-2', 'Phase 2: {phase_name}', '{phase_description}', 'pending'),
  ...;
```

6. .NET SDK バージョンを確認: `dotnet --version`
7. 既存プロジェクトの状態を確認: `dotnet build`（既存コードがある場合）

### **Step 1: フェーズ実装計画の読み取り**

対象フェーズの実装計画プランから以下を抽出:

1. **フェーズの目的・概要**
2. **前提条件**（依存する前フェーズの完了状態）
3. **実装タスク一覧**（各タスクの詳細な実装内容）
   - 作成するファイル一覧
   - 実装するクラス・メソッド一覧
   - コード例・テンプレート（記載がある場合）
4. **完了チェックリスト**（全チェック項目と基準）
5. **設計書参照セクション**（§x.x の参照先）

### **Step 2: 設計書の該当セクション読み取り**

1. 実装計画で参照されている設計書セクション（§x.x）を全て読み取る
2. 設計書に記載された以下の要件を抽出:
   - **エンティティ定義**（テーブル名、カラム名、型、制約）
   - **API エンドポイント定義**（HTTP メソッド、パス、リクエスト/レスポンス）
   - **ビジネスロジック**（バリデーション、計算式、状態遷移）
   - **イベント定義**（Kafka トピック、イベントペイロード）
   - **セキュリティ要件**（認証、認可、入力検証）
   - **エラーハンドリング**（エラーコード、例外クラス）

### **Step 3: フェーズ内全タスクの実装**

実装計画のタスクを **上から順に** 実装する。各タスクで以下のルールを遵守:

#### 3.1 コーディング規約の遵守（AGENTS.md + .github/instructions/ 準拠）

| カテゴリ | 必須ルール |
|---------|----------|
| **命名** | PascalCase（クラス/メソッド）、camelCase（変数）、_camelCase（プライベートフィールド） |
| **DI** | primary constructor によるコンストラクタインジェクション。`[Inject]` プロパティインジェクション禁止 |
| **async** | 全非同期メソッドに `CancellationToken ct = default`。`.Result` / `.Wait()` 禁止 |
| **ログ** | `ILogger<T>` + メッセージテンプレート。`Console.WriteLine` 禁止。文字列補間ログ禁止 |
| **例外** | `catch (Exception) { }` 禁止。必ずログ出力または再スロー |
| **Null** | `ArgumentNullException.ThrowIfNull()`。コレクションで null 返却禁止（`[]` を返す） |
| **EF Core** | `[Table("snake_case")]` / `[Column("snake_case")]`。`AsNoTracking()` 読み取り専用。`DateTime.UtcNow` 使用 |
| **セキュリティ** | `FromSqlRaw` 文字列結合禁止。秘密情報ハードコード禁止。入力バリデーション必須 |
| **レイヤー** | Endpoints → Services → Repositories の依存方向厳守 |

#### 3.2 ファイル作成順序

```
1. Models/（EF Core エンティティ）
2. DTOs/Requests/ と DTOs/Responses/（リクエスト/レスポンス DTO）
3. Infrastructure/Persistence/AppDbContext.cs（DbContext）
4. Repositories/Interfaces/ → Repositories/（インターフェース → 実装）
5. Services/Interfaces/ → Services/（インターフェース → 実装）
6. Endpoints/（Minimal API エンドポイント）
7. Configurations/（設定クラス）
8. Program.cs（DI 登録、ミドルウェア）
9. appsettings*.json（設定ファイル）
```

#### 3.3 実装中の自己検証【必須 — スキップ厳禁】

> **⛔ MANDATORY GATE**: Step 3 の全タスク実装完了後、**次の全コマンドをターミナルで実行し、出力結果を確認すること**。1 つでもスキップした場合、以降のステップは無効とする。

**全コマンドを順にターミナルで実行すること（推測で PASS としない）**:

```bash
# [3.3-CHECK-1] TODO/FIXME/HACK/NotImplementedException 残存チェック
grep -rn "TODO\|FIXME\|HACK\|UNDONE\|NotImplementedException" --include="*.cs" {ServiceDirectory}/ || echo "CHECK-1: PASS (0 hits)"

# [3.3-CHECK-2] Console.WriteLine 残存チェック
grep -rn "Console\.Write" --include="*.cs" {ServiceDirectory}/ || echo "CHECK-2: PASS (0 hits)"

# [3.3-CHECK-3] .Result / .Wait() デッドロックパターンチェック
grep -rn "\.Result\b\|\.Wait()" --include="*.cs" {ServiceDirectory}/ || echo "CHECK-3: PASS (0 hits)"

# [3.3-CHECK-4] 秘密情報ハードコードチェック
grep -rn 'Password\s*=\s*"\|"sk-\|connectionString.*Password' --include="*.cs" --include="*.json" {ServiceDirectory}/ || echo "CHECK-4: PASS (0 hits)"

# [3.3-CHECK-5] DateTime.Now チェック（DateTime.UtcNow を使用すべき）
grep -rn "DateTime\.Now[^U]" --include="*.cs" {ServiceDirectory}/ || echo "CHECK-5: PASS (0 hits)"

# [3.3-CHECK-6] プロパティインジェクション [Inject] チェック
grep -rn "\[Inject\]" --include="*.cs" {ServiceDirectory}/ || echo "CHECK-6: PASS (0 hits)"

# [3.3-CHECK-7] FromSqlRaw 文字列結合チェック
grep -rn "FromSqlRaw.*+" --include="*.cs" {ServiceDirectory}/ || echo "CHECK-7: PASS (0 hits)"

# [3.3-CHECK-8] ダミー値チェック（テストプロジェクト除外）
grep -rn '"dummy"\|"placeholder"\|"xxx"' --include="*.cs" {ServiceDirectory}/ | grep -v ".Tests/" || echo "CHECK-8: PASS (0 hits)"
```

**判定ルール**:
- 全 8 チェックが PASS (0 hits) → Step 4（ビルド確認）に進行
- 1 件でも検出あり → **即座に該当箇所を修正し、修正後に再度全チェックを実行**
- **検出がある状態で Step 4 に進んではならない**

---

### **Step 4: ビルド確認**

> **前提**: Step 3.3 の全 8 チェックが PASS (0 hits) であること。

```bash
cd {ServiceDirectory}
dotnet build --no-restore 2>&1
```

- ビルドエラーが 0 件であること
- 警告（`TreatWarningsAsErrors` が有効な場合）が 0 件であること
- ビルド失敗時は即座にエラーを修正し、再ビルド（最大 5 回）

### **Step 5: 完了チェックリスト検証**

実装計画の「Phase N 完了チェックリスト」に記載された **全項目** を検証する。

#### 5.1 チェック項目の分類と検証方法

| チェック種別 | 検証方法 |
|------------|---------|
| **ファイル存在確認** | `ls` / `find` でファイルの存在を確認 |
| **コード内容確認** | ファイルを読み取り、記載内容が基準を満たすか確認 |
| **ビルド確認** | `dotnet build` の成功を確認 |
| **テスト確認** | `dotnet test` の成功を確認（テストフェーズの場合） |
| **マイグレーション確認** | `dotnet ef migrations list` で確認（エンティティフェーズの場合） |
| **grep チェック** | 禁止パターンの残存を grep で検出 |

#### 5.2 共通必須チェック（全フェーズで実行）

以下の [品質チェックパターン](./references/verification-patterns.md) を全フェーズで実行:

```bash
# 1. TODO/FIXME/HACK コメント残存チェック
grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" {ServiceDirectory}/

# 2. NotImplementedException 残存チェック
grep -rn "NotImplementedException" --include="*.cs" {ServiceDirectory}/

# 3. Console.WriteLine 残存チェック
grep -rn "Console\.Write" --include="*.cs" {ServiceDirectory}/

# 4. 秘密情報ハードコードチェック
grep -rn "Password\s*=\s*\"" --include="*.cs" {ServiceDirectory}/

# 5. .Result / .Wait() チェック
grep -rn "\.\(Result\|Wait\)()" --include="*.cs" {ServiceDirectory}/

# 6. プロパティインジェクションチェック
grep -rn "\[Inject\]" --include="*.cs" {ServiceDirectory}/

# 7. FromSqlRaw 文字列結合チェック
grep -rn "FromSqlRaw.*+" --include="*.cs" {ServiceDirectory}/

# 8. 空メソッドボディチェック
grep -rn "{ }" --include="*.cs" {ServiceDirectory}/

# 9. ダミー値チェック
grep -rn "\"dummy\"\|\"test\"\|\"xxx\"\|\"placeholder\"" --include="*.cs" {ServiceDirectory}/
# （テストプロジェクト配下は除外）

# 10. DateTime.Now チェック（DateTime.UtcNow を使用すべき）
grep -rn "DateTime\.Now[^U]" --include="*.cs" {ServiceDirectory}/
```

#### 5.3 検証結果の記録

各チェック項目を以下の判定で記録:

| 判定 | 意味 | 次フェーズへの影響 |
|------|------|----------------|
| ✅ **PASS** | 基準を完全に満たす | 影響なし |
| ⚠️ **PARTIAL** | 一部不足だが重大ではない | 3 件以上で不合格 |
| ❌ **FAIL** | 基準を満たさない | 即座に不合格 |

#### 5.4 判定基準

| 総合判定 | 条件 |
|---------|------|
| ✅ **合格** | 全チェック項目 PASS、共通必須チェック全 0 件 |
| ⚠️ **条件付き合格** | PARTIAL が 1〜2 件、かつ低重要度の指摘のみ（即座に是正して再検証） |
| ❌ **不合格** | FAIL が 1 件以上、または共通必須チェックで検出あり |

### **Step 5.5: 不合格時の是正サイクル**

```
不合格 → 是正 → 再検証 → 合格？
  ├── YES → Step 6 へ
  └── NO → 是正 → 再検証（最大 3 回まで）
       └── 3 回連続不合格 → ユーザーに報告して中断
```

1. **不合格項目の特定**: FAIL / PARTIAL の項目を一覧化
2. **是正の実施**: 各不合格項目を修正
3. **再ビルド**: `dotnet build` で修正がビルドを壊していないか確認
4. **再検証**: Step 5 を再実行
5. **3 回連続不合格の場合**: ユーザーに状況を報告し、手動介入を求める

### **Step 6: 検証レポート生成・次フェーズへの進行**

#### 6.1 検証レポートの生成

```markdown
# {ServiceName} Phase {N} 完了検証レポート

## 実施日時
{YYYY-MM-DD HH:MM (UTC)}

## 総合判定
{✅ 合格 / ⚠️ 条件付き合格 / ❌ 不合格}

## 1. 完了チェックリスト結果
| # | チェック項目 | 結果 | 備考 |
|---|------------|------|------|
| 1 | {item} | ✅/⚠️/❌ | {notes} |
...

## 2. 共通品質チェック結果
| # | チェック項目 | 検出数 | 結果 |
|---|------------|-------|------|
| 1 | TODO/FIXME/HACK 残存 | 0 | ✅ |
| 2 | NotImplementedException 残存 | 0 | ✅ |
| 3 | Console.WriteLine 残存 | 0 | ✅ |
...

## 3. 是正履歴（該当する場合）
| 試行 | 不合格項目 | 是正内容 | 結果 |
|------|----------|---------|------|

## 4. 次フェーズへの申し送り事項
```

#### 6.2 SQL ステータス更新

```sql
UPDATE todos SET status = 'done' WHERE id = '{service}-phase-{N}';
UPDATE todos SET status = 'in_progress' WHERE id = '{service}-phase-{N+1}';
```

#### 6.3 次フェーズへの進行

- 合格の場合: **自動的に Step 1 に戻り、次フェーズ（Phase N+1）を開始**
- 最終フェーズの場合: **全フェーズ完了レポートを生成**

---

## 全フェーズ完了時の最終検証

全フェーズが完了した場合、以下の最終検証を実施:

### Final Step A: 最終統合チェック

```bash
# 1. 全体ビルド
dotnet build

# 2. 全テスト実行（テストプロジェクトがある場合）
dotnet test --no-build

# 3. 全体 grep チェック（全禁止パターン）
grep -rn "TODO\|FIXME\|HACK\|UNDONE\|XXX" --include="*.cs" {ServiceDirectory}/
grep -rn "NotImplementedException" --include="*.cs" {ServiceDirectory}/
grep -rn "Console\.Write" --include="*.cs" {ServiceDirectory}/
grep -rn "Password\s*=\s*\"" --include="*.cs" {ServiceDirectory}/
grep -rn "\.\(Result\|Wait\)()" --include="*.cs" {ServiceDirectory}/
grep -rn "\[Inject\]" --include="*.cs" {ServiceDirectory}/
grep -rn "FromSqlRaw.*+" --include="*.cs" {ServiceDirectory}/
grep -rn "DateTime\.Now[^U]" --include="*.cs" {ServiceDirectory}/
```

### Final Step B: 最終コードレビュー【必須 — スキップ厳禁】

> **⛔ MANDATORY GATE**: 全フェーズ完了後、**必ず `orchest-code-review` エージェントを `runSubagent` ツールで 1 回呼び出すこと**。各フェーズごとの実行は不要だが、全フェーズ完了後のこの最終レビューは **絶対にスキップしてはならない**。レビュー結果（Critical/High/Medium/Low の件数）が得られない限り、最終レポートの生成に進めない。

`orchest-code-review` エージェント（14 の専門 Agent）による包括的コードレビューを実施する。
全フェーズの実装が完了した状態のコードに対して **アーキテクチャ・DDD・セキュリティ・パフォーマンス・テスト品質** 等の多角的な最終検証を行う。

#### B.1 レビュー実行【ツール呼び出し必須】

**以下の `runSubagent` 呼び出しを必ず実行すること**（省略・スキップ禁止）:

```
runSubagent を使用して orchest-code-review エージェントを呼び出す。

agentName: "orchest-code-review"
prompt: "{ServiceDirectory} の全フェーズ実装が完了しました。サービス全体の最終コードレビューを実施してください。対象: {ServiceDirectory}"
description: "Final comprehensive code review"
```

> **重要**: `runSubagent` の `agentName` パラメータに `"orchest-code-review"` を指定すること。

14 の専門 Agent が並列でレビューを実施:

| グループ | Agent | 検証観点 |
|---------|-------|---------|
| **基盤品質** | `architecture` | レイヤー依存方向、プロジェクト構造 |
| | `ddd-domain` | Aggregate Root 境界、Value Object 不変性、Repository パターン |
| | `csharp-standards` | C# 14 / .NET 10 コーディング規約、命名規則 |
| | `config-di` | DI 登録、ミドルウェア順序、IOptions<T> パターン |
| **API・データ** | `api-endpoint` | REST 規約、入力バリデーション、レスポンス設計 |
| | `data-access` | EF Core エンティティ設計、クエリ品質、マイグレーション |
| | `async-concurrency` | CancellationToken 伝搬、async/await 正確性 |
| | `error-logging` | 例外クラス階層、構造化ログ、Correlation ID |
| **非機能要件** | `security` | OWASP Top 10、認証認可、秘密情報管理、IDOR 防止 |
| | `performance` | N+1 クエリ、LINQ 最適化、メモリ効率 |
| | `resilience` | Polly リトライ/サーキットブレーカー、ヘルスチェック |
| | `dependency` | NuGet バージョン、禁止パッケージ、ライセンス |
| **品質保証** | `test-quality` | テスト命名、AAA パターン、カバレッジ基準 |
| | `tech-lead` | 技術標準の統括、Agent 間競合の最終裁定 |

#### B.2 レビュー結果の判定

| レビュー総合判定 | 条件 | 対応 |
|---------------|------|------|
| ✅ **PASS**（A/B 判定） | Critical: 0 件、High: 0 件 | 最終レポート生成に進行 |
| ⚠️ **要修正**（C 判定） | Critical: 0 件、High: 1 件以上 | High 指摘を修正後、再レビュー |
| ❌ **不合格**（D/E 判定） | Critical: 1 件以上 | Critical 指摘を即座に修正後、再レビュー |

#### B.3 レビュー指摘の修正サイクル

```
レビュー結果 → 指摘あり？
  ├── Critical/High なし → 最終レポート生成へ
  └── Critical/High あり → 修正 → 再レビュー（最大 2 回）
       └── 2 回修正しても解消しない → ユーザーに報告して判断を仰ぐ
```

1. **Critical 指摘の修正**: セキュリティ脆弱性、データ損失リスク等は最優先で修正
2. **High 指摘の修正**: アーキテクチャ違反、規約違反等を修正
3. **Medium/Low 指摘**: 最終レポートに記録（改善推奨事項として）
4. **修正後の再レビュー**: 修正箇所を中心に再度 `orchest-code-review` を実行
5. **レビュー結果の保存**: 最終レポートの「コードレビュー結果」セクションに記録

### 最終レポート

```markdown
# {ServiceName} 全フェーズ完了レポート

## サービス概要
- サービス名: {ServiceName}
- 総フェーズ数: {N}
- 実装ファイル数: {count}

## フェーズ別結果サマリー
| Phase | 名称 | 初回判定 | 是正回数 | 最終判定 |
|-------|------|---------|---------|---------|

## 最終統合チェック結果
| # | チェック項目 | 結果 |
|---|------------|------|

## 最終コードレビュー結果（orchest-code-review）
| # | Agent | 指摘数 (Critical/High/Medium/Low) | 判定 |
|---|-------|----------------------------------|------|
| 1 | architecture | 0/0/0/0 | ✅ |
| 2 | ddd-domain | 0/0/0/0 | ✅ |
| 3 | csharp-standards | 0/0/0/0 | ✅ |
| 4 | config-di | 0/0/0/0 | ✅ |
| 5 | api-endpoint | 0/0/0/0 | ✅ |
| 6 | data-access | 0/0/0/0 | ✅ |
| 7 | async-concurrency | 0/0/0/0 | ✅ |
| 8 | error-logging | 0/0/0/0 | ✅ |
| 9 | security | 0/0/0/0 | ✅ |
| 10 | performance | 0/0/0/0 | ✅ |
| 11 | resilience | 0/0/0/0 | ✅ |
| 12 | dependency | 0/0/0/0 | ✅ |
| 13 | test-quality | 0/0/0/0 | ✅ |
| 14 | tech-lead | 0/0/0/0 | ✅ |
| レビュー総合判定 | {A〜E} | 修正回数: {N} |

## 品質メトリクス
- ビルドエラー: 0
- ビルド警告: {count}
- テスト結果: {passed}/{total}
- 禁止パターン検出: 0
```

---

## フェーズ種別ごとの特別な検証ポイント

各フェーズは実装計画の記載に従うが、フェーズの種別に応じて以下の追加検証を実施する。
詳細は [実装ワークフローガイド](./references/implementation-workflow.md) を参照。

| フェーズ種別 | 追加検証 |
|------------|---------|
| **基盤構築**（Phase 1） | `.csproj` の PropertyGroup、NuGet パッケージ、ディレクトリ構成 |
| **エンティティ**（Phase 2） | `[Table]`/`[Column]` 属性、snake_case、監査カラム、制約 |
| **Repository**（Phase 3） | インターフェース定義、`AsNoTracking()`、`CancellationToken` 伝搬 |
| **Service**（Phase 4） | ビジネスロジック完全性、例外処理、ログ出力 |
| **Endpoints**（Phase 5） | REST 規約、バリデーション、認可設定、OpenAPI |
| **Kafka/Background**（Phase 6） | イベント定義、Consumer 実装、Outbox パターン |
| **Redis/Cache**（Phase 7） | キャッシュ戦略、TTL 設定、キー命名 |
| **Security**（Phase 8） | 認証/認可、セキュリティヘッダー、レート制限 |
| **Test**（Phase 9/10） | テスト命名、AAA パターン、カバレッジ |
| **Observability**（Phase 10） | OpenTelemetry、ヘルスチェック、構造化ログ |
| **Deploy/Final**（Phase 11） | Program.cs 統合、Dockerfile、最終ビルド |

---

## エラーハンドリング・中断条件

| 状況 | 対応 |
|------|------|
| ビルドが 5 回連続失敗 | ユーザーに報告して中断 |
| 是正サイクルが 3 回連続不合格 | ユーザーに報告して中断 |
| 設計書と実装計画に矛盾 | 設計書を正として実装。矛盾をレポートに記録 |
| 前フェーズの成果物が壊れている | 前フェーズの該当箇所を修正してから継続 |
| 外部依存（Kafka, Redis, PostgreSQL）が利用不可 | 当該チェック項目を SKIP とし、レポートに記録 |

---

## 注意事項

1. **全項目を必ず検証すること**: ファイルを実際に読んで確認する。推測で PASS としない
2. **設計書を正として判断する**: 実装コードと設計書に矛盾がある場合、設計書を正とする
3. **手抜きパターンを厳格にチェック**: スタブ実装・仮実装は FAIL とする
4. **前フェーズのリグレッション検出**: 各フェーズ開始時に `dotnet build` で前フェーズの成果が壊れていないか確認
5. **コミットメッセージ規約**: `feat({service}): Phase {N} - {phase_name}` の形式で Conventional Commits 準拠
6. **並列実行禁止**: フェーズは必ず順番に実行する（依存関係があるため）

---

## 参照ドキュメント

- [実装ワークフローガイド](./references/implementation-workflow.md) — フェーズ種別ごとの詳細な実装手順
- [検証パターン集](./references/verification-patterns.md) — grep パターン、品質チェック項目の詳細
- [サービスマッピング](./references/service-mapping.md) — サービス名と設計書・実装計画・ディレクトリの対応表
- `.github/agents/orchest-code-review.agent.md` — 包括的ソースコードレビュー・オーケストレータ（Final Step B で使用）
- `AGENTS.md` — プロジェクト共通ルール
- `.github/instructions/dotnet-coding-standards.instructions.md` — C# コーディング規約
- `.github/instructions/security-coding.instructions.md` — セキュリティ規約
- `.github/instructions/api-design.instructions.md` — API 設計規約
- `.github/instructions/test-standards.instructions.md` — テスト規約
