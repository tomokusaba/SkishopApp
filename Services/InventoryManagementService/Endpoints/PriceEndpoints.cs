using FluentValidation;
using InventoryManagementService.DTOs.Responses;
using System.Security.Claims;
using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagementService.Endpoints;

/// <summary>
/// 価格（Price）エンドポイント定義クラス。
/// 商品価格の照会・履歴確認・作成・更新を提供する。
/// </summary>
public static class PriceEndpoints
{
    /// <summary>
    /// 価格関連の Minimal API エンドポイントを登録する。
    /// </summary>
    /// <param name="app">エンドポイントルートビルダー</param>
    /// <remarks>
    /// ルートグループ: /api/prices（タグ: Prices、レート制限: default）。
    /// 現在価格の取得は AllowAnonymous、履歴・作成・更新は AdminOnly 認可が必要。
    /// H-10: PUT → PATCH に変更（部分更新セマンティクス）。
    /// </remarks>
    public static void MapPriceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/prices")
            .WithTags("Prices")
            .RequireRateLimiting("default");

        group.MapGet("/{productId}", GetByProductId)
            .WithName("GetPriceByProductId").AllowAnonymous()
            .Produces<PriceDto>(200).Produces(404);
        group.MapGet("/history/{productId}", GetPriceHistory)
            .WithName("GetPriceHistory").RequireAuthorization("AdminOnly")
            .Produces<PaginatedResult<PriceHistoryDto>>(200);
        group.MapPost("/", CreatePrice)
            .RequireAuthorization("AdminOnly").WithName("CreatePrice")
            .Produces<PriceDto>(201).ProducesValidationProblem();
        // H-10: PUT → PATCH に変更
        group.MapPatch("/{productId}", UpdatePrice)
            .RequireAuthorization("AdminOnly").WithName("UpdatePrice")
            .Produces<PriceDto>(200).ProducesValidationProblem().Produces(404);
    }

    /// <summary>
    /// GET /api/prices/{productId} — 商品IDで現在の価格情報を取得する（認証不要）。
    /// </summary>
    /// <param name="productId">商品ID</param>
    /// <param name="service">価格サービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>価格が存在すれば 200 OK、存在しなければ 404 NotFound</returns>
    private static async Task<IResult> GetByProductId(
        string productId,
        IPriceService service,
        CancellationToken ct)
        => await service.GetByProductIdAsync(productId, ct) is { } price
            ? Results.Ok(price)
            : Results.NotFound();

    /// <summary>
    /// GET /api/prices/history/{productId} — 商品の価格変更履歴を取得する（AdminOnly 認可必須）。
    /// </summary>
    /// <param name="productId">商品ID</param>
    /// <param name="query">ページネーションパラメータ</param>
    /// <param name="validator">ページネーションバリデーター</param>
    /// <param name="service">価格サービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>価格変更履歴一覧</returns>
    private static async Task<IResult> GetPriceHistory(
        string productId,
        [AsParameters] PaginationParams query,
        IValidator<PaginationParams> validator,
        IPriceService service,
        CancellationToken ct)
    {
        // H-7: PaginationParams バリデーション追加
        var validationResult = await validator.ValidateAsync(query, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        return Results.Ok(await service.GetHistoryAsync(productId, query.Page, query.Size, ct));
    }

    /// <summary>
    /// POST /api/prices — 新規価格を作成する（AdminOnly 認可必須）。
    /// </summary>
    /// <param name="request">価格作成リクエスト</param>
    /// <param name="validator">バリデーター</param>
    /// <param name="service">価格サービス</param>
    /// <param name="user">認証済み管理者の ClaimsPrincipal（変更者 ID の取得に使用）</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>作成成功時は 201 Created + Location ヘッダー</returns>
    private static async Task<IResult> CreatePrice(
        [FromBody] PriceCreateRequest request,
        IValidator<PriceCreateRequest> validator,
        IPriceService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        // H-17: changedBy の null チェック追加
        var changedBy = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("認証情報からユーザー ID を取得できません");
        var price = await service.CreateAsync(request, changedBy, ct);
        // H-8: Location ヘッダーを price.Id → price.ProductId に修正
        return Results.Created($"/api/prices/{price.ProductId}", price);
    }

    /// <summary>
    /// PATCH /api/prices/{productId} — 商品ID でアクティブ価格を部分更新する（AdminOnly 認可必須）。
    /// </summary>
    /// <param name="productId">更新対象の商品ID</param>
    /// <param name="request">価格更新リクエスト</param>
    /// <param name="validator">バリデーター</param>
    /// <param name="service">価格サービス</param>
    /// <param name="user">認証済み管理者の ClaimsPrincipal（変更者 ID の取得に使用）</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>更新後の価格情報</returns>
    private static async Task<IResult> UpdatePrice(
        string productId,
        [FromBody] PriceUpdateRequest request,
        IValidator<PriceUpdateRequest> validator,
        IPriceService service,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        // H-17: changedBy の null チェック追加
        var changedBy = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("認証情報からユーザー ID を取得できません");
        // B-2 修正: UpdateAsync → UpdateByProductIdAsync に変更（商品 ID からアクティブ価格を取得して更新）
        var price = await service.UpdateByProductIdAsync(productId, request, changedBy, ct);
        return Results.Ok(price);
    }
}
