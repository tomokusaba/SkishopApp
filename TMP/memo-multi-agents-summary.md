# SkiShop マルチエージェント構成 — 全体サマリー

本ドキュメントは、SkiShop プロジェクトで提供する **2 つのマルチエージェントシステム** の全体像をまとめたものである。

---

## 1. 全体像

本プロジェクトでは、品質保証の 2 つの局面に対応するマルチエージェントを提供する。

```
┌──────────────────────────────────────────────────────────────────┐
│                    SkiShop 品質保証エージェント群                    │
│                                                                    │
│  ┌─────────────────────────┐  ┌──────────────────────────────┐   │
│  │  ドキュメントレビュー      │  │  ソースコードレビュー           │   │
│  │  (orchest-doc-review)     │  │  (orchest-code-review)        │   │
│  │                           │  │                                │   │
│  │  対象: design-docs/*.md   │  │  対象: **/*.cs, *.csproj,     │   │
│  │        仕様書・設計書       │  │        appsettings*.json       │   │
│  │                           │  │        ソースコード全般          │   │
│  │  1 Orchestrator           │  │  1 Orchestrator                │   │
│  │  + 14 Sub-Agents          │  │  + 14 Sub-Agents               │   │
│  └─────────────────────────┘  └──────────────────────────────┘   │
│                                                                    │
│  開発フロー:  設計書作成 → Doc Review → 実装 → Code Review          │
└──────────────────────────────────────────────────────────────────┘
```

---

## 2. ドキュメントレビュー・マルチエージェント（既存）

**目的**: 設計書・仕様書が「エンタープライズ品質で実装に着手できる水準」であることを保証する

### 2.1 構成一覧

| # | Agent 名 | ファイル名 | 役割概要 |
|---|---------|-----------|---------|
| 0 | **Orchestrator** | `orchest-doc-review.agent.md` | 14 Agent の呼び出し・レポート集約・競合解決・品質判定 |
| 1 | business-analyst | `orchest-doc-review-business-analyst.agent.md` | ビジネス要件の完全性・ユーザー価値・受入基準の検証 |
| 2 | architect | `orchest-doc-review-architect.agent.md` | アーキテクチャ設計の妥当性・DDD 原則・障害モード分析 |
| 3 | qa-manager | `orchest-doc-review-qa-manager.agent.md` | テスト戦略・カバレッジ計画・受入基準の検証可能性 |
| 4 | oss-reviewer | `orchest-doc-review-oss-reviewer.agent.md` | NuGet パッケージのライセンス・脆弱性リスク評価 |
| 5 | release-manager | `orchest-doc-review-release-manager.agent.md` | リリース計画・変更管理・ロールバック計画の検証 |
| 6 | **tech-lead** | `orchest-doc-review-tech-lead.agent.md` | 技術標準適合性・実装可能性・**Agent 間競合の最終裁定** |
| 7 | programing-reviewer | `orchest-doc-review-programing-reviewer.agent.md` | 設計書内コード例の正確性・C# 14 活用・DI 設計 |
| 8 | dba-reviewer | `orchest-doc-review-dba-reviewer.agent.md` | DB スキーマ設計・EF Core マッピング・マイグレーション戦略 |
| 9 | performance-reviewer | `orchest-doc-review-performance-reviewer.agent.md` | 性能要件・スケーラビリティ・キャッシュ戦略の設計品質 |
| 10 | security-reviewer | `orchest-doc-review-security-reviewer.agent.md` | OWASP Top 10 対策・認証認可設計・脅威モデリング |
| 11 | infra-ops-reviewer | `orchest-doc-review-infra-ops-reviewer.agent.md` | インフラ構成・コンテナ設計・監視/ログ設計・DR/BCP |
| 12 | audit-reviewer | `orchest-doc-review-audit-reviewer.agent.md` | 開発プロセス準拠・エビデンス・トレーサビリティの検証 |
| 13 | compliance-reviewer | `orchest-doc-review-compliance-reviewer.agent.md` | GDPR/個人情報保護・データ保持期間・同意管理設計 |
| 14 | ux-accessibility-reviewer | `orchest-doc-review-ux-accessibility-reviewer.agent.md` | UX 設計品質・WCAG 2.1 準拠・レスポンシブ対応・i18n |

---

## 3. ソースコードレビュー・マルチエージェント（新規作成対象）

**目的**: 実装コードが「設計書の意図通りに、プロジェクト規約に準拠して、安全かつ高品質に実装されている」ことを保証する

### 3.1 構成一覧

```
                    orchest-code-review (Orchestrator)
                                │
        ┌───────────────────────┼───────────────────────────┐
        │                       │                             │
   ┌────┴────┐           ┌─────┴─────┐              ┌───────┴───────┐
   │ Tier 1  │           │  Tier 2   │              │    Tier 3     │
   │ 統括層   │           │ 構造設計層  │              │  コード品質層   │
   └────┬────┘           └─────┬─────┘              └───────┬───────┘
        │                      │                             │
   tech-lead          architecture-reviewer         csharp-standards-reviewer
                      ddd-domain-reviewer           async-concurrency-reviewer
                      api-endpoint-reviewer         error-logging-reviewer

   ┌─────────┐        ┌──────────────┐             ┌────────────────┐
   │ Tier 4  │        │   Tier 5     │             │    Tier 6      │
   │データ/設定│        │ セキュリティ層 │             │   品質保証層    │
   └────┬────┘        └──────┬───────┘             └───────┬────────┘
        │                    │                              │
   data-access-reviewer  security-reviewer          test-quality-reviewer
   config-di-reviewer    dependency-reviewer         performance-reviewer
                                                     resilience-reviewer
```

### 3.2 各 Agent の詳細

#### Tier 1: 統括層

| # | Agent 名 | ファイル名 | 役割 |
|---|---------|-----------|------|
| 1 | **tech-lead** | `orchest-code-review-tech-lead.agent.md` | 全 Agent のレビュー結果を踏まえた技術標準の統括適合性確認。Agent 間で競合が発生した場合の最終裁定者（「ビジネス成果最大化」を基準に判断） |

**主な検証観点**:
- プロジェクト全体の技術標準への適合性
- 禁止事項チェックリスト（AGENTS.md §4.2）の遵守
- Agent 間判断の矛盾解決

---

#### Tier 2: 構造・設計適合層

| # | Agent 名 | ファイル名 | 役割 |
|---|---------|-----------|------|
| 2 | **architecture-reviewer** | `orchest-code-review-architecture.agent.md` | レイヤー依存方向の厳守を検証。Endpoints → Services → Repositories の方向が逆転していないか、プロジェクト構成が規約通りか |
| 3 | **ddd-domain-reviewer** | `orchest-code-review-ddd-domain.agent.md` | DDD 戦術パターンの実装品質を検証。Aggregate Root 境界、Value Object の不変性、Domain Event 設計、Repository の 1 Aggregate 原則 |
| 4 | **api-endpoint-reviewer** | `orchest-code-review-api-endpoint.agent.md` | Minimal API の実装パターンを検証。MapGroup/拡張メソッド分離、REST 規約、入力バリデーション、Problem Details レスポンス |

**代表的な検出パターン**:
```
❌ Endpoint が Repository を直接参照している             → architecture-reviewer が検出
❌ OrderItem を直接 Repository で操作している            → ddd-domain-reviewer が検出
❌ Endpoint に複雑なビジネスロジックが書かれている         → api-endpoint-reviewer が検出
```

---

#### Tier 3: コード品質層

| # | Agent 名 | ファイル名 | 役割 |
|---|---------|-----------|------|
| 5 | **csharp-standards-reviewer** | `orchest-code-review-csharp-standards.agent.md` | C# 14 / .NET 10 のコーディング規約適合性。命名規則、record/primary constructor 活用、禁止パターン（`Console.WriteLine`、`new HttpClient()` 等） |
| 6 | **async-concurrency-reviewer** | `orchest-code-review-async-concurrency.agent.md` | 非同期処理の正確性。`CancellationToken` 全伝搬、`.Result`/`.Wait()` 禁止、`Thread.Sleep()` → `Task.Delay` 置換、BackgroundService 実装品質 |
| 7 | **error-logging-reviewer** | `orchest-code-review-error-logging.agent.md` | 例外処理・ログの品質。例外クラス階層（NotFoundException→404 等）、構造化ログ（メッセージテンプレート形式）、PII ログ禁止、Correlation ID |

**代表的な検出パターン**:
```
❌ var password = "secret123";                         → csharp-standards-reviewer が検出
❌ var result = someTask.Result;                       → async-concurrency-reviewer が検出
❌ _logger.LogInformation($"User: {email}");           → error-logging-reviewer が検出
```

---

#### Tier 4: データ・設定層

| # | Agent 名 | ファイル名 | 役割 |
|---|---------|-----------|------|
| 8 | **data-access-reviewer** | `orchest-code-review-data-access.agent.md` | EF Core 実装品質。エンティティ設計（`[Table]`/`[Column]` snake_case）、`AsNoTracking`、N+1 クエリ、楽観的ロック、Include/Eager Loading、トランザクション管理 |
| 9 | **config-di-reviewer** | `orchest-code-review-config-di.agent.md` | DI 登録品質（Scoped/Singleton/Transient 使い分け）、ミドルウェアパイプライン順序（UseAuthentication→UseAuthorization 等）、appsettings.json 秘密情報チェック、IOptions<T> パターン |

**代表的な検出パターン**:
```
❌ DateTime.Now を使用（DateTime.UtcNow が必須）          → data-access-reviewer が検出
❌ UseAuthentication() が UseAuthorization() の後にある  → config-di-reviewer が検出
❌ appsettings.json に Password= が直書きされている        → config-di-reviewer が検出
```

---

#### Tier 5: セキュリティ層

| # | Agent 名 | ファイル名 | 役割 |
|---|---------|-----------|------|
| 10 | **security-reviewer** | `orchest-code-review-security.agent.md` | OWASP Top 10 の実装検証。SQL インジェクション（`FromSqlRaw` 文字列結合禁止）、認証認可（FallbackPolicy、`RequireAuthorization`）、IDOR 防止、セキュリティヘッダー、XSS、秘密情報ハードコード |
| 11 | **dependency-reviewer** | `orchest-code-review-dependency.agent.md` | NuGet パッケージの安全性。禁止パッケージ検出（`System.Web` / `Newtonsoft.Json` / EF6）、preview/beta/rc パッケージ禁止、既知 CVE チェック、ライセンス適合性 |

**代表的な検出パターン**:
```
❌ FromSqlRaw($"SELECT * FROM users WHERE email = '{email}'")  → security-reviewer が検出
❌ PackageReference Include="EntityFramework" Version="6.*"     → dependency-reviewer が検出
```

---

#### Tier 6: 品質保証層

| # | Agent 名 | ファイル名 | 役割 |
|---|---------|-----------|------|
| 12 | **test-quality-reviewer** | `orchest-code-review-test-quality.agent.md` | テストコードの品質。命名規約（`Should_○○_When_○○`）、AAA パターン、分岐カバレッジ 80% 基準、Testcontainers 使用（InMemory 禁止）、異常系テスト充足度 |
| 13 | **performance-reviewer** | `orchest-code-review-performance.agent.md` | 実装レベルの性能問題検出。N+1 クエリ、不要な `Include()`、LINQ 最適化、ページネーション、`AsSplitQuery()` 活用、メモリアロケーション |
| 14 | **resilience-reviewer** | `orchest-code-review-resilience.agent.md` | 耐障害性パターンの実装検証。`IHttpClientFactory` 必須、Polly / `AddStandardResilienceHandler`、サーキットブレーカー、リトライ（指数バックオフ）、ヘルスチェック（`/health`, `/health/ready`） |

**代表的な検出パターン**:
```
❌ var client = new HttpClient();                       → resilience-reviewer が検出
❌ テストメソッド名が Test1, Test2 になっている            → test-quality-reviewer が検出
❌ .ToListAsync() 後にメモリ上でフィルタリングしている      → performance-reviewer が検出
```

---

## 4. ドキュメントレビュー ↔ コードレビューの対応関係

2 つのマルチエージェントは「設計の検証」と「実装の検証」の関係にある。

```
設計フェーズ                              実装フェーズ
──────────                              ──────────

  [Doc] business-analyst                   (該当なし — ビジネス要件はコードでは検証しない)
  [Doc] architect              ───→       [Code] architecture-reviewer
  [Doc] qa-manager             ───→       [Code] test-quality-reviewer
  [Doc] oss-reviewer           ───→       [Code] dependency-reviewer
  [Doc] release-manager                    (該当なし — リリース判断はコードレビューのスコープ外)
  [Doc] tech-lead              ───→       [Code] tech-lead
  [Doc] programing-reviewer    ───→       [Code] csharp-standards-reviewer
  [Doc] dba-reviewer           ───→       [Code] data-access-reviewer
  [Doc] performance-reviewer   ───→       [Code] performance-reviewer
  [Doc] security-reviewer      ───→       [Code] security-reviewer
  [Doc] infra-ops-reviewer     ───→       [Code] resilience-reviewer
  [Doc] audit-reviewer         ─┐
                                 ├───→    [Code] error-logging-reviewer (ログ/監査証跡に統合)
  [Doc] compliance-reviewer    ─┘
                                 ┌───→    [Code] security-reviewer (PII/秘密情報に統合)
  [Doc] ux-accessibility-reviewer          (該当なし — バックエンド API のみ)

  (新規)                        ───→      [Code] async-concurrency-reviewer
  (新規)                        ───→      [Code] config-di-reviewer
  (新規)                        ───→      [Code] ddd-domain-reviewer
  (新規)                        ───→      [Code] api-endpoint-reviewer
```

### 新規 Agent の追加理由

| 新規 Agent | 追加理由 |
|-----------|---------|
| **async-concurrency-reviewer** | .NET の非同期処理（`async`/`await`, `CancellationToken`）はバグ・デッドロックの温床であり、専門知識が必要。ドキュメントレビューでは「設計方針の記載」を確認するが、コードレビューでは「全メソッドへの実装漏れ」を機械的に検出する必要がある |
| **config-di-reviewer** | ASP.NET Core の DI 登録・ミドルウェア順序・設定ファイルは、誤ると全サービスに影響する横断的関心事。ドキュメントでは「方針」を、コードでは「Program.cs / appsettings.json の実装」を検証する |
| **ddd-domain-reviewer** | DDD の Aggregate Root 境界・Value Object の不変性は、architecture（構造）とも programming（コード品質）とも異なる専門領域。コードに落とし込む際に境界が崩れやすいため独立 Agent が必要 |
| **api-endpoint-reviewer** | Minimal API の実装パターン（MapGroup / IEndpointRouteBuilder 拡張）は ASP.NET Core 固有の知識が求められ、REST 規約 + フレームワーク規約の両方を検証する必要がある |

### ドキュメントレビューから統合・削除した Agent の理由

| 統合/削除した Agent | 理由 |
|---|---|
| **business-analyst** | コードレビューではビジネス要件の妥当性ではなく「DDD で正しくモデリングされているか」を検証する。ddd-domain-reviewer がこの役割を担う |
| **release-manager** | リリース判断はコードレビューのスコープ外。オーケストレータの品質判定（Approved / Rejected）がリリース判断の入力になる |
| **audit-reviewer** | コードレベルでの監査証跡は「ログが適切に出力されているか」に帰着する。error-logging-reviewer に統合 |
| **compliance-reviewer** | コードレベルでのコンプライアンスは「PII がログに出力されていないか」「秘密情報がハードコードされていないか」に帰着する。security-reviewer に統合 |
| **ux-accessibility-reviewer** | 本プロジェクトのコードレビュー対象はバックエンド API（C# / ASP.NET Core）のみであり、フロントエンド UI は対象外 |

---

## 5. 数値サマリー

| 項目 | ドキュメントレビュー | ソースコードレビュー |
|------|-------------------|-------------------|
| オーケストレータ | 1 | 1 |
| サブ Agent 数 | 14 | 14 |
| 合計 Agent 数 | **15** | **15** |
| プロジェクト合計 | | **30 Agent** |
| レビュー対象 | `design-docs/*.md` | `**/*.cs`, `**/*.csproj`, `**/appsettings*.json` |
| レポート出力先 | `.github/review-reports/doc-review/` | `.github/review-reports/code-review/` |
| 最終裁定者 | tech-lead | tech-lead |
| 品質判定 | Approved / Rejected / Conditional | Approved / Rejected / Conditional |

---

## 6. Agent 間の競合解決（コードレビュー）

全ての競合は最終的に **tech-lead** が裁定する。裁定基準は「ビジネスで最大の成果が得られる方法」。

| 競合パターン | 初期解決ルール |
|---|---|
| architecture-reviewer（抽象化推奨）vs csharp-standards-reviewer（KISS 原則） | 設計書の設計意図を参照し architecture-reviewer を優先 |
| security-reviewer（制限追加）vs performance-reviewer（制限緩和） | security-reviewer を原則優先（安全性 > 性能） |
| data-access-reviewer（正規化/制約追加）vs performance-reviewer（非正規化） | 明確な根拠なき非正規化は data-access-reviewer を優先 |
| csharp-standards-reviewer（C#14 機能推奨）vs test-quality-reviewer（テスタビリティ優先） | テスタビリティを損なわない範囲で C#14 を推奨 |
| resilience-reviewer（リトライ追加）vs performance-reviewer（レイテンシ低減） | べき等性が保証される操作に限りリトライを許可 |

---

## 7. 開発ワークフローにおける位置づけ

```
 ① 設計書作成
      │
      ▼
 ② orchest-doc-review（ドキュメントレビュー）
      │  ✅ Approved → 次へ
      │  ❌ Rejected → ① に戻り修正
      ▼
 ③ ソースコード実装
      │
      ▼
 ④ orchest-code-review（ソースコードレビュー）
      │  ✅ Approved → 次へ
      │  ❌ Rejected → ③ に戻り修正
      ▼
 ⑤ CI/CD パイプライン → デプロイ
```
