using FluentValidation;
using InventoryManagementService.DTOs.Responses;
using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagementService.Endpoints;

/// <summary>
/// カテゴリ（Category）エンドポイント定義クラス。
/// カテゴリの CRUD 操作とカテゴリ別商品一覧取得を提供する。
/// </summary>
public static class CategoryEndpoints
{
    /// <summary>
    /// カテゴリ関連の Minimal API エンドポイントを登録する。
    /// </summary>
    /// <param name="app">エンドポイントルートビルダー</param>
    /// <remarks>
    /// ルートグループ: /api/categories（タグ: Categories、レート制限: default）。
    /// 参照系は AllowAnonymous、作成・更新・削除は AdminOnly 認可が必要。
    /// </remarks>
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/categories")
            .WithTags("Categories")
            .RequireRateLimiting("default");

        group.MapGet("/", GetAllCategories).WithName("GetCategories").AllowAnonymous()
            .Produces<List<CategoryDto>>(200);
        group.MapGet("/{id}", GetCategoryById).WithName("GetCategoryById").AllowAnonymous()
            .Produces<CategoryDto>(200).Produces(404);
        group.MapGet("/{id}/products", GetCategoryProducts)
            .WithName("GetCategoryProducts").AllowAnonymous()
            .Produces<PaginatedResult<ProductDto>>(200);
        group.MapPost("/", CreateCategory)
            .RequireAuthorization("AdminOnly").WithName("CreateCategory")
            .Produces<CategoryDto>(201).ProducesValidationProblem();
        // H-10: PUT → PATCH に変更
        group.MapPatch("/{id}", UpdateCategory)
            .RequireAuthorization("AdminOnly").WithName("UpdateCategory")
            .Produces<CategoryDto>(200).ProducesValidationProblem().Produces(404);
        group.MapDelete("/{id}", DeleteCategory)
            .RequireAuthorization("AdminOnly").WithName("DeleteCategory")
            .Produces(204).Produces(404);
    }

    /// <summary>
    /// GET /api/categories — 全カテゴリ一覧を取得する（認証不要、最大100件）。
    /// </summary>
    /// <param name="service">カテゴリサービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>カテゴリ一覧（最大100件）</returns>
    private static async Task<IResult> GetAllCategories(
        ICategoryService service,
        CancellationToken ct)
    {
        var categories = await service.GetAllAsync(ct);
        return Results.Ok(categories.Take(100).ToList());
    }

    /// <summary>
    /// GET /api/categories/{id} — カテゴリIDでカテゴリ詳細を取得する（認証不要）。
    /// </summary>
    /// <param name="id">カテゴリID</param>
    /// <param name="service">カテゴリサービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>カテゴリが存在すれば 200 OK、存在しなければ 404 NotFound</returns>
    private static async Task<IResult> GetCategoryById(
        string id,
        ICategoryService service,
        CancellationToken ct)
        => await service.GetByIdAsync(id, ct) is { } category
            ? Results.Ok(category)
            : Results.NotFound();

    /// <summary>
    /// GET /api/categories/{id}/products — カテゴリIDに属する商品一覧を取得する（認証不要）。
    /// </summary>
    /// <param name="id">カテゴリID</param>
    /// <param name="query">ページネーションパラメータ</param>
    /// <param name="validator">ページネーションバリデーター</param>
    /// <param name="productService">商品サービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>指定カテゴリの商品一覧</returns>
    private static async Task<IResult> GetCategoryProducts(
        string id,
        [AsParameters] PaginationParams query,
        IValidator<PaginationParams> validator,
        IProductService productService,
        CancellationToken ct)
    {
        // H-7: PaginationParams バリデーション追加
        var validationResult = await validator.ValidateAsync(query, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        return Results.Ok(await productService.GetByCategoryAsync(id, query.Page, query.Size, ct));
    }

    /// <summary>
    /// POST /api/categories — 新規カテゴリを作成する（AdminOnly 認可必須）。
    /// </summary>
    /// <param name="request">カテゴリ作成リクエスト</param>
    /// <param name="validator">バリデーター</param>
    /// <param name="service">カテゴリサービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>作成成功時は 201 Created + Location ヘッダー</returns>
    private static async Task<IResult> CreateCategory(
        [FromBody] CategoryCreateRequest request,
        IValidator<CategoryCreateRequest> validator,
        ICategoryService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var category = await service.CreateAsync(request, ct);
        return Results.Created($"/api/categories/{category.Id}", category);
    }

    /// <summary>
    /// PATCH /api/categories/{id} — 既存カテゴリを部分更新する（AdminOnly 認可必須）。
    /// </summary>
    /// <param name="id">更新対象のカテゴリID</param>
    /// <param name="request">カテゴリ更新リクエスト</param>
    /// <param name="validator">バリデーター</param>
    /// <param name="service">カテゴリサービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>更新後のカテゴリ情報</returns>
    private static async Task<IResult> UpdateCategory(
        string id,
        [FromBody] CategoryUpdateRequest request,
        IValidator<CategoryUpdateRequest> validator,
        ICategoryService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var category = await service.UpdateAsync(id, request, ct);
        return Results.Ok(category);
    }

    /// <summary>
    /// DELETE /api/categories/{id} — カテゴリを削除する（AdminOnly 認可必須）。
    /// </summary>
    /// <param name="id">削除対象のカテゴリID</param>
    /// <param name="service">カテゴリサービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>削除成功時は 204 NoContent</returns>
    private static async Task<IResult> DeleteCategory(
        string id,
        ICategoryService service,
        CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return Results.NoContent();
    }
}
