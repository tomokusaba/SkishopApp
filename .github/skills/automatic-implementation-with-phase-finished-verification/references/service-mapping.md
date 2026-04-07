# サービスマッピング

サービス名と対応するファイル・ディレクトリのマッピングを定義する。
スキル実行時にサービス名から必要なファイルパスを自動解決するために使用する。

---

## サービス一覧

### 1. AuthService（認証サービス）

| 項目 | パス |
|------|------|
| 引数名 | `authentication-service` |
| 実装計画 | `impl-plan/authentication-service-impl-plan.md` |
| 設計書 | `design-docs/authentication-service-design.md` |
| プロジェクトディレクトリ | `AuthService/` |
| テストディレクトリ | `AuthService.Tests/` |
| .csproj | `AuthService/AuthService.csproj` |
| ポート | 5001 |

### 2. UserManagementService（ユーザー管理サービス）

| 項目 | パス |
|------|------|
| 引数名 | `user-management` |
| 実装計画 | `impl-plan/user-management-impl-plan.md` |
| 設計書 | `design-docs/user-management-design.md` |
| プロジェクトディレクトリ | `UserManagementService/` |
| テストディレクトリ | `UserManagementService.Tests/` |
| .csproj | `UserManagementService/UserManagementService.csproj` |
| ポート | 5002 |

### 3. InventoryManagementService（在庫管理サービス）

| 項目 | パス |
|------|------|
| 引数名 | `inventory-management` |
| 実装計画 | `impl-plan/inventory-management-impl-plan.md` |
| 設計書 | `design-docs/inventory-management-design.md` |
| プロジェクトディレクトリ | `InventoryManagementService/` |
| テストディレクトリ | `InventoryManagementService.Tests/` |
| .csproj | `InventoryManagementService/InventoryManagementService.csproj` |
| ポート | 5003 |

### 4. SalesManagementService（販売管理サービス）

| 項目 | パス |
|------|------|
| 引数名 | `sales-management` |
| 実装計画 | `impl-plan/sales-management-impl-plan.md` |
| 設計書 | `design-docs/sales-management-design.md` |
| プロジェクトディレクトリ | `SalesManagementService/` |
| テストディレクトリ | `SalesManagementService.Tests/` |
| .csproj | `SalesManagementService/SalesManagementService.csproj` |
| ポート | 5004 |

### 5. PaymentCartService（決済・カートサービス）

| 項目 | パス |
|------|------|
| 引数名 | `payment-cart-service` |
| 実装計画 | `impl-plan/payment-cart-service-impl-plan.md` |
| 設計書 | `design-docs/payment-cart-service-design.md` |
| プロジェクトディレクトリ | `PaymentCartService/` |
| テストディレクトリ | `PaymentCartService.Tests/` |
| .csproj | `PaymentCartService/PaymentCartService.csproj` |
| ポート | 5005 |

### 6. CouponService（クーポンサービス）

| 項目 | パス |
|------|------|
| 引数名 | `coupon-service` |
| 実装計画 | `impl-plan/coupon-service-impl-plan.md` |
| 設計書 | `design-docs/coupon-service-design.md` |
| プロジェクトディレクトリ | `CouponService/` |
| テストディレクトリ | `CouponService.Tests/` |
| .csproj | `CouponService/CouponService.csproj` |
| ポート | 5006 |

### 7. PointService（ポイントサービス）

| 項目 | パス |
|------|------|
| 引数名 | `point-service` |
| 実装計画 | `impl-plan/point-service-impl-plan.md` |
| 設計書 | `design-docs/point-service-design.md` |
| プロジェクトディレクトリ | `PointService/` |
| テストディレクトリ | `PointService.Tests/` |
| .csproj | `PointService/PointService.csproj` |
| ポート | 5007 |

### 8. MailSendService（メール送信サービス）

| 項目 | パス |
|------|------|
| 引数名 | `mailsend-service` |
| 実装計画 | `impl-plan/mailsend-service-impl-plan.md` |
| 設計書 | `design-docs/mailsend-service-design.md` |
| プロジェクトディレクトリ | `MailSendService/` |
| テストディレクトリ | `MailSendService.Tests/` |
| .csproj | `MailSendService/MailSendService.csproj` |
| ポート | 5008 |

### 9. AiSupportService（AI サポートサービス）

| 項目 | パス |
|------|------|
| 引数名 | `ai-support-service` |
| 実装計画 | `impl-plan/ai-support-service-impl-plan.md` |
| 設計書 | `design-docs/ai-support-service-design.md` |
| プロジェクトディレクトリ | `AiSupportService/` |
| テストディレクトリ | `AiSupportService.Tests/` |
| .csproj | `AiSupportService/AiSupportService.csproj` |
| ポート | 5009 |

### 10. ApiGateway（API ゲートウェイ）

| 項目 | パス |
|------|------|
| 引数名 | `api-gateway` |
| 実装計画 | `impl-plan/api-gateway-impl-plan.md` |
| 設計書 | `design-docs/api-gateway-design.md` |
| プロジェクトディレクトリ | `ApiGateway/` |
| テストディレクトリ | `ApiGateway.Tests/` |
| .csproj | `ApiGateway/ApiGateway.csproj` |
| ポート | 8080 |

### 11. Frontend（フロントエンド）

| 項目 | パス |
|------|------|
| 引数名 | `front-end` |
| 実装計画 | `impl-plan/front-end-impl-plan.md` |
| 設計書 | `design-docs/front-end-need.md` |
| プロジェクトディレクトリ | `frontend/` |
| テストディレクトリ | `frontend/` (同一ディレクトリ内) |
| パッケージ定義 | `frontend/package.json` |
| ポート | 3000 |

> **注意**: Frontend は .NET プロジェクトではないため、ビルド・テストコマンドが異なる:
> - ビルド: `npm run build`（`frontend/` ディレクトリで実行）
> - テスト: `npm run test`
> - Lint: `npm run lint`

---

## 共通参照ファイル

| ファイル | 用途 |
|---------|------|
| `design-docs/spec.md` | システム全体設計（全サービス共通） |
| `AGENTS.md` | プロジェクト共通ルール・コーディング規約 |
| `.github/instructions/dotnet-coding-standards.instructions.md` | C# コーディング規約 |
| `.github/instructions/security-coding.instructions.md` | セキュリティ規約 |
| `.github/instructions/api-design.instructions.md` | API 設計規約 |
| `.github/instructions/dotnet-config.instructions.md` | 設定ファイル規約 |
| `.github/instructions/nuget-dependency.instructions.md` | NuGet 依存関係規約 |
| `.github/instructions/test-standards.instructions.md` | テスト規約 |
| `.github/instructions/dockerfile-infra.instructions.md` | Dockerfile 規約 |
| `.github/instructions/sql-schema-review.instructions.md` | SQL スキーマ規約 |

---

## AppHost（.NET Aspire オーケストレーション）

| 項目 | パス |
|------|------|
| プロジェクトディレクトリ | `AppHost/` |
| .csproj | `AppHost/AppHost.csproj` |
| Program.cs | `AppHost/Program.cs` |

> **注意**: AppHost は個別のマイクロサービスではなく、全サービスをオーケストレーションするプロジェクト。
> 各サービスの実装後に `AppHost/Program.cs` にサービス参照を追加する必要がある。
