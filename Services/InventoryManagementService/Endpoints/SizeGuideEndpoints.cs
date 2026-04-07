using FluentValidation;
using InventoryManagementService.DTOs.Responses;
using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagementService.Endpoints;

/// <summary>
/// サイズガイド（SizeGuide）エンドポイント定義クラス。
/// カテゴリ別サイズガイドの照会・作成・更新を提供する。
/// </summary>
public static class SizeGuideEndpoints
{
    /// <summary>
    /// サイズガイド関連の Minimal API エンドポイントを登録する。
    /// </summary>
    /// <param name="app">エンドポイントルートビルダー</param>
    /// <remarks>
    /// ルートグループ: /api/size-guides（タグ: SizeGuides、レート制限: default）。
    /// カテゴリ別取得は AllowAnonymous、作成・更新は AdminOnly 認可が必要。
    /// H-10: PUT → PATCH に変更（部分更新セマンティクス）。
    /// </remarks>
    public static void MapSizeGuideEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/size-guides")
            .WithTags("SizeGuides")
            .RequireRateLimiting("default");

        group.MapGet("/{categoryId}", GetByCategoryId)
            .WithName("GetSizeGuideByCategory").AllowAnonymous()
            .Produces<SizeGuideDto>(200).Produces(404);
        group.MapPost("/", CreateSizeGuide)
            .RequireAuthorization("AdminOnly").WithName("CreateSizeGuide")
            .Produces<SizeGuideDto>(201).ProducesValidationProblem();
        // H-10: PUT → PATCH に変更
        group.MapPatch("/{id}", UpdateSizeGuide)
            .RequireAuthorization("AdminOnly").WithName("UpdateSizeGuide")
            .Produces<SizeGuideDto>(200).ProducesValidationProblem().Produces(404);
    }

    /// <summary>
    /// GET /api/size-guides/{categoryId} — カテゴリIDでサイズガイドを取得する（認証不要）。
    /// </summary>
    /// <param name="categoryId">カテゴリID</param>
    /// <param name="service">サイズガイドサービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>サイズガイドが存在すれば 200 OK、存在しなければ 404 NotFound</returns>
    private static async Task<IResult> GetByCategoryId(
        string categoryId,
        ISizeGuideService service,
        CancellationToken ct)
        => await service.GetByCategoryIdAsync(categoryId, ct) is { } guide
            ? Results.Ok(guide)
            : Results.NotFound();

    /// <summary>
    /// POST /api/size-guides — 新規サイズガイドを作成する（AdminOnly 認可必須）。
    /// </summary>
    /// <param name="request">サイズガイド作成リクエスト</param>
    /// <param name="validator">バリデーター</param>
    /// <param name="service">サイズガイドサービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>作成成功時は 201 Created + Location ヘッダー</returns>
    private static async Task<IResult> CreateSizeGuide(
        [FromBody] SizeGuideCreateRequest request,
        IValidator<SizeGuideCreateRequest> validator,
        ISizeGuideService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var guide = await service.CreateAsync(request, ct);
        // H-9: Location ヘッダーを guide.Id → guide.CategoryId に修正
        return Results.Created($"/api/size-guides/{guide.CategoryId}", guide);
    }

    /// <summary>
    /// PATCH /api/size-guides/{id} — 既存サイズガイドを部分更新する（AdminOnly 認可必須）。
    /// </summary>
    /// <param name="id">更新対象のサイズガイドID</param>
    /// <param name="request">サイズガイド更新リクエスト</param>
    /// <param name="validator">バリデーター</param>
    /// <param name="service">サイズガイドサービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>更新後のサイズガイド情報</returns>
    private static async Task<IResult> UpdateSizeGuide(
        string id,
        [FromBody] SizeGuideUpdateRequest request,
        IValidator<SizeGuideUpdateRequest> validator,
        ISizeGuideService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var guide = await service.UpdateAsync(id, request, ct);
        return Results.Ok(guide);
    }
}
