using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace InventoryManagementService.Infrastructure.OpenApi;

/// <summary>
/// AsParameters で使用される record 型のスキーマ変換を行うトランスフォーマー。
/// .NET 10 の OpenAPI 2.0 では IOpenApiSchema インターフェースを使用する。
/// </summary>
public sealed class ParameterSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken ct)
    {
        var typeName = context.JsonTypeInfo.Type.Name;

        // PaginationParams の場合にスキーマの説明を追加
        if (typeName == "PaginationParams")
        {
            schema.Type = JsonSchemaType.Object;
            schema.Description = "ページネーションパラメータ（page: 0始まり、size: 1〜100）";
        }

        // ProductSearchParams の場合
        if (typeName == "ProductSearchParams")
        {
            schema.Type = JsonSchemaType.Object;
            schema.Description = "商品検索パラメータ（keyword: 検索キーワード、categoryId: カテゴリID、brand: ブランド名、page: ページ番号、size: 件数、sortBy: ソート項目、descending: 降順フラグ）";
        }

        return Task.CompletedTask;
    }
}
