using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace InventoryManagementService.Infrastructure.OpenApi;

/// <summary>
/// OpenAPI ドキュメント情報を設定するトランスフォーマー。
/// .NET 10 推奨の IOpenApiDocumentTransformer パターンを使用。
/// </summary>
public sealed class DocumentInfoTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken ct)
    {
        document.Info = new OpenApiInfo
        {
            Title = "InventoryManagementService API",
            Version = "v1",
            Description = """
                スキーショップ在庫管理サービスの REST API。
                
                ## 機能概要
                - **商品管理**: 商品の登録・更新・検索・削除
                - **カテゴリ管理**: 商品カテゴリの階層管理
                - **在庫管理**: 入庫・出庫・在庫予約・バッチ処理
                - **価格管理**: 価格設定・セール価格・価格履歴
                - **レビュー管理**: 商品レビューの投稿・承認・検索
                - **サイズガイド**: カテゴリ別サイズ表の管理
                
                ## 認証
                Bearer JWT トークンによる認証が必要です。
                `Authorization: Bearer <token>` ヘッダーを付与してください。
                """,
            Contact = new OpenApiContact
            {
                Name = "SkiShop Support",
                Email = "support@skishop.example.com",
                Url = new Uri("https://skishop.example.com/support")
            },
            License = new OpenApiLicense
            {
                Name = "MIT License",
                Url = new Uri("https://opensource.org/licenses/MIT")
            }
        };

        // サーバー情報の追加
        document.Servers =
        [
            new OpenApiServer
            {
                Url = "http://localhost:5003",
                Description = "Development Server (Docker Compose)"
            },
            new OpenApiServer
            {
                Url = "http://localhost:8080",
                Description = "Production Server (via API Gateway)"
            }
        ];

        return Task.CompletedTask;
    }
}
