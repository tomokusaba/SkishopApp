using FluentValidation;
using InventoryManagementService.DTOs.Responses;
using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagementService.Endpoints;

/// <summary>
/// 在庫（Inventory）エンドポイント定義クラス。
/// 在庫照会、入出庫操作、低在庫アラート、在庫ステータス確認を提供する。
/// </summary>
public static class InventoryEndpoints
{
    /// <summary>
    /// 在庫関連の Minimal API エンドポイントを登録する。
    /// </summary>
    /// <param name="app">エンドポイントルートビルダー</param>
    /// <remarks>
    /// ルートグループ: /api/inventory（タグ: Inventory、レート制限: default）。
    /// 在庫照会は AllowAnonymous、入出庫・低在庫は AdminOnly、ステータス確認は認証必須。
    /// </remarks>
    public static void MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory")
            .WithTags("Inventory")
            .RequireRateLimiting("default");

        group.MapGet("/{productId}", GetByProductId)
            .WithName("GetInventoryByProductId").AllowAnonymous()
            .Produces<InventoryDto>(200).Produces(404);
        group.MapPost("/batch", GetBatch)
            .WithName("GetInventoryBatch").AllowAnonymous()
            .Produces<List<InventoryDto>>(200).ProducesValidationProblem();
        group.MapGet("/low-stock", GetLowStock)
            .WithName("GetLowStock").RequireAuthorization("AdminOnly")
            .Produces<PaginatedResult<InventoryDto>>(200);
        group.MapPost("/stock-in", StockIn)
            .RequireAuthorization("AdminOnly")
            .WithName("StockIn")
            .Produces<InventoryDto>(200).ProducesValidationProblem();
        group.MapPost("/stock-out", StockOut)
            .RequireAuthorization("AdminOnly")
            .WithName("StockOut")
            .Produces<InventoryDto>(200).ProducesValidationProblem();
        group.MapGet("/status/{productId}", GetInventoryStatus)
            .WithName("GetInventoryStatus").RequireAuthorization()
            .Produces<InventoryStatusDto>(200).Produces(404);
    }

    /// <summary>
    /// GET /api/inventory/{productId} — 商品IDで在庫情報を取得する（認証不要）。
    /// </summary>
    /// <param name="productId">商品ID</param>
    /// <param name="service">在庫サービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>在庫が存在すれば 200 OK、存在しなければ 404 NotFound</returns>
    private static async Task<IResult> GetByProductId(
        string productId,
        IInventoryService service,
        CancellationToken ct)
        => await service.GetByProductIdAsync(productId, ct) is { } inventory
            ? Results.Ok(inventory)
            : Results.NotFound();

    /// <summary>
    /// POST /api/inventory/batch — 複数商品IDで在庫情報を一括取得する（認証不要）。
    /// </summary>
    /// <param name="request">商品IDリストのリクエスト</param>
    /// <param name="validator">バリデーター</param>
    /// <param name="service">在庫サービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>バリデーション成功時は該当在庫リスト、失敗時は 400 ValidationProblem</returns>
    private static async Task<IResult> GetBatch(
        [FromBody] BatchIdsRequest request,
        IValidator<BatchIdsRequest> validator,
        IInventoryService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());
        return Results.Ok(await service.GetByProductIdsAsync(request.Ids, ct));
    }

    /// <summary>
    /// GET /api/inventory/low-stock — 低在庫（閾値10以下）の商品一覧を取得する（AdminOnly 認可必須）。
    /// </summary>
    /// <param name="query">ページネーションパラメータ</param>
    /// <param name="validator">ページネーションバリデーター</param>
    /// <param name="service">在庫サービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>低在庫の商品一覧</returns>
    private static async Task<IResult> GetLowStock(
        [AsParameters] PaginationParams query,
        IValidator<PaginationParams> validator,
        IInventoryService service,
        CancellationToken ct)
    {
        // H-7: PaginationParams バリデーション追加
        var validationResult = await validator.ValidateAsync(query, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        return Results.Ok(await service.GetLowStockAsync(10, query.Page, query.Size, ct));
    }

    /// <summary>
    /// POST /api/inventory/stock-in — 入庫処理を実行する（AdminOnly 認可必須）。
    /// </summary>
    /// <param name="request">入庫リクエスト（商品ID、数量、ロケーション等）</param>
    /// <param name="validator">バリデーター</param>
    /// <param name="service">在庫サービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>更新後の在庫情報</returns>
    private static async Task<IResult> StockIn(
        [FromBody] StockInRequest request,
        IValidator<StockInRequest> validator,
        IInventoryService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var inventory = await service.StockInAsync(request, ct);
        return Results.Ok(inventory);
    }

    /// <summary>
    /// POST /api/inventory/stock-out — 出庫処理を実行する（AdminOnly 認可必須）。
    /// </summary>
    /// <param name="request">出庫リクエスト（商品ID、数量等）</param>
    /// <param name="validator">バリデーター</param>
    /// <param name="service">在庫サービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>更新後の在庫情報</returns>
    private static async Task<IResult> StockOut(
        [FromBody] StockOutRequest request,
        IValidator<StockOutRequest> validator,
        IInventoryService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var inventory = await service.StockOutAsync(request, ct);
        return Results.Ok(inventory);
    }

    /// <summary>
    /// GET /api/inventory/status/{productId} — 商品の在庫ステータスを取得する（認証必須）。
    /// </summary>
    /// <param name="productId">商品ID</param>
    /// <param name="service">在庫サービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>ステータスが存在すれば 200 OK、存在しなければ 404 NotFound</returns>
    private static async Task<IResult> GetInventoryStatus(
        string productId,
        IInventoryService service,
        CancellationToken ct)
        => await service.GetStatusAsync(productId, ct) is { } status
            ? Results.Ok(status)
            : Results.NotFound();
}
