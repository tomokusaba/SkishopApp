using FluentValidation;
using InventoryManagementService.DTOs.Responses;
using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagementService.Endpoints;

/// <summary>
/// 商品（Product）エンドポイント定義クラス。
/// CRUD 操作、検索、画像アップロード、バッチ取得を提供する。
/// </summary>
public static class ProductEndpoints
{
    /// <summary>
    /// 商品関連の Minimal API エンドポイントを登録する。
    /// </summary>
    /// <param name="app">エンドポイントルートビルダー</param>
    /// <remarks>
    /// ルートグループ: /api/products（タグ: Products、レート制限: default）。
    /// 参照系は AllowAnonymous、作成・更新・削除・画像アップロードは AdminOnly 認可が必要。
    /// H-10: PUT → PATCH に変更（部分更新セマンティクス）。
    /// </remarks>
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products")
            .WithTags("Products")
            .RequireRateLimiting("default");

        group.MapGet("/", GetAllProducts).WithName("GetProducts").AllowAnonymous()
            .Produces<PaginatedResult<ProductDto>>(200);
        group.MapGet("/{id}", GetProductById).WithName("GetProductById").AllowAnonymous()
            .Produces<ProductDto>(200).Produces(404);
        group.MapGet("/sku/{sku}", GetProductBySku).WithName("GetProductBySku").AllowAnonymous()
            .Produces<ProductDto>(200).Produces(404);
        group.MapGet("/search", SearchProducts).WithName("SearchProducts")
            .RequireRateLimiting("search").AllowAnonymous()
            .Produces<PaginatedResult<ProductDto>>(200);
        group.MapGet("/category/{categoryId}", GetProductsByCategory)
            .WithName("GetProductsByCategory").AllowAnonymous()
            .Produces<PaginatedResult<ProductDto>>(200);
        group.MapPost("/batch", GetProductsBatch).WithName("GetProductsBatch").AllowAnonymous()
            .Produces<List<ProductDto>>(200).ProducesValidationProblem();

        group.MapPost("/", CreateProduct)
            .RequireAuthorization("AdminOnly")
            .WithName("CreateProduct")
            .Produces<ProductDto>(201).ProducesValidationProblem();
        // H-10: PUT → PATCH に変更
        group.MapPatch("/{id}", UpdateProduct)
            .RequireAuthorization("AdminOnly")
            .WithName("UpdateProduct")
            .Produces<ProductDto>(200).ProducesValidationProblem().Produces(404);
        group.MapDelete("/{id}", DeleteProduct)
            .RequireAuthorization("AdminOnly")
            .WithName("DeleteProduct")
            .Produces(204).Produces(404);
        group.MapPost("/{id}/images", UploadProductImage)
            .RequireAuthorization("AdminOnly")
            .WithName("UploadProductImage")
            .DisableAntiforgery()
            .Produces<ProductImageDto>(201).ProducesValidationProblem().Produces(404);
    }

    /// <summary>
    /// GET /api/products — 全商品をページネーション付きで取得する（認証不要）。
    /// </summary>
    /// <param name="search">検索・ページネーションパラメータ（keyword, categoryId, brand, page, size）</param>
    /// <param name="validator">検索パラメータバリデーター</param>
    /// <param name="service">商品サービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>ページネーションされた商品一覧</returns>
    private static async Task<IResult> GetAllProducts(
        [AsParameters] ProductSearchParams search,
        IValidator<ProductSearchParams> validator,
        IProductService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(search, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var criteria = new ProductSearchCriteria(search.Keyword, search.CategoryId, search.Brand, search.Category);
        return Results.Ok(await service.SearchAsync(criteria, search.Page, search.Size, ct));
    }

    /// <summary>
    /// GET /api/products/{id} — 商品IDで商品詳細を取得する（認証不要）。
    /// </summary>
    /// <param name="id">商品ID</param>
    /// <param name="service">商品サービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>商品が存在すれば 200 OK、存在しなければ 404 NotFound</returns>
    private static async Task<IResult> GetProductById(
        string id,
        IProductService service,
        CancellationToken ct)
        => await service.GetByIdAsync(id, ct) is { } product
            ? Results.Ok(product)
            : Results.NotFound();

    /// <summary>
    /// GET /api/products/sku/{sku} — SKU コードで商品を取得する（認証不要）。
    /// </summary>
    /// <param name="sku">SKU コード</param>
    /// <param name="service">商品サービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>商品が存在すれば 200 OK、存在しなければ 404 NotFound</returns>
    private static async Task<IResult> GetProductBySku(
        string sku,
        IProductService service,
        CancellationToken ct)
        => await service.GetBySkuAsync(sku, ct) is { } product
            ? Results.Ok(product)
            : Results.NotFound();

    /// <summary>
    /// GET /api/products/search — キーワード・カテゴリ・ブランドで商品を検索する（認証不要、レート制限: search）。
    /// </summary>
    /// <param name="search">検索パラメータ（keyword, categoryId, brand, page, size）</param>
    /// <param name="validator">検索パラメータバリデーター</param>
    /// <param name="service">商品サービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>検索条件に一致する商品一覧</returns>
    private static async Task<IResult> SearchProducts(
        [AsParameters] ProductSearchParams search,
        IValidator<ProductSearchParams> validator,
        IProductService service,
        CancellationToken ct)
    {
        // H-7: ProductSearchParams バリデーション追加
        var validationResult = await validator.ValidateAsync(search, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var criteria = new ProductSearchCriteria(search.Keyword, search.CategoryId, search.Brand, search.Category);
        return Results.Ok(await service.SearchAsync(criteria, search.Page, search.Size, ct));
    }

    /// <summary>
    /// GET /api/products/category/{categoryId} — カテゴリIDに属する商品一覧を取得する（認証不要）。
    /// </summary>
    /// <param name="categoryId">カテゴリID</param>
    /// <param name="query">ページネーションパラメータ</param>
    /// <param name="validator">ページネーションバリデーター</param>
    /// <param name="service">商品サービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>指定カテゴリの商品一覧</returns>
    private static async Task<IResult> GetProductsByCategory(
        string categoryId,
        [AsParameters] PaginationParams query,
        IValidator<PaginationParams> validator,
        IProductService service,
        CancellationToken ct)
    {
        // H-7: PaginationParams バリデーション追加
        var validationResult = await validator.ValidateAsync(query, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        return Results.Ok(await service.GetByCategoryAsync(categoryId, query.Page, query.Size, ct));
    }

    /// <summary>
    /// POST /api/products/batch — 複数の商品IDで一括取得する（認証不要）。
    /// </summary>
    /// <param name="request">商品IDリストのリクエスト</param>
    /// <param name="validator">バリデーター</param>
    /// <param name="service">商品サービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>バリデーション成功時は該当商品リスト、失敗時は 400 ValidationProblem</returns>
    private static async Task<IResult> GetProductsBatch(
        [FromBody] BatchIdsRequest request,
        IValidator<BatchIdsRequest> validator,
        IProductService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());
        return Results.Ok(await service.GetByIdsAsync(request.Ids, ct));
    }

    /// <summary>
    /// POST /api/products — 新規商品を作成する（AdminOnly 認可必須）。
    /// </summary>
    /// <param name="request">商品作成リクエスト</param>
    /// <param name="validator">バリデーター</param>
    /// <param name="service">商品サービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>作成成功時は 201 Created + Location ヘッダー、バリデーション失敗時は 400</returns>
    private static async Task<IResult> CreateProduct(
        [FromBody] ProductCreateRequest request,
        IValidator<ProductCreateRequest> validator,
        IProductService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var product = await service.CreateProductAsync(request, ct);
        return Results.Created($"/api/products/{product.Id}", product);
    }

    /// <summary>
    /// PATCH /api/products/{id} — 既存商品を部分更新する（AdminOnly 認可必須）。
    /// </summary>
    /// <param name="id">更新対象の商品ID</param>
    /// <param name="request">商品更新リクエスト</param>
    /// <param name="validator">バリデーター</param>
    /// <param name="service">商品サービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>更新成功時は 200 OK、バリデーション失敗時は 400</returns>
    private static async Task<IResult> UpdateProduct(
        string id,
        [FromBody] ProductUpdateRequest request,
        IValidator<ProductUpdateRequest> validator,
        IProductService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var product = await service.UpdateAsync(id, request, ct);
        return Results.Ok(product);
    }

    /// <summary>
    /// DELETE /api/products/{id} — 商品を削除する（AdminOnly 認可必須）。
    /// </summary>
    /// <param name="id">削除対象の商品ID</param>
    /// <param name="service">商品サービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>削除成功時は 204 NoContent</returns>
    private static async Task<IResult> DeleteProduct(
        string id,
        IProductService service,
        CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return Results.NoContent();
    }

    /// <summary>
    /// POST /api/products/{id}/images — 商品画像をアップロードする（AdminOnly 認可必須、Antiforgery 無効）。
    /// </summary>
    /// <param name="id">対象商品ID</param>
    /// <param name="file">アップロードする画像ファイル</param>
    /// <param name="validator">ファイルバリデーター</param>
    /// <param name="service">商品サービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>アップロード成功時は 201 Created + Location ヘッダー</returns>
    private static async Task<IResult> UploadProductImage(
        string id,
        IFormFile file,
        IValidator<IFormFile> validator,
        IProductService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(file, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var image = await service.UploadImageAsync(id, file, ct);
        return Results.Created($"/api/products/{id}/images/{image.Id}", image);
    }
}
