using System.Security.Claims;
using FluentValidation;
using InventoryManagementService.DTOs.Responses;
using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagementService.Endpoints;

/// <summary>
/// レビュー（Review）エンドポイント定義クラス。
/// レビューの閲覧・作成、管理者返信、「参考になった」投票、ステータス更新を提供する。
/// </summary>
public static class ReviewEndpoints
{
    /// <summary>
    /// レビュー関連の Minimal API エンドポイントを登録する。
    /// </summary>
    /// <param name="app">エンドポイントルートビルダー</param>
    /// <remarks>
    /// ルートグループ: /api/reviews（タグ: Reviews、レート制限: default）。
    /// 閲覧系は AllowAnonymous、投稿・投票は認証必須、返信・ステータス更新は AdminOnly。
    /// </remarks>
    public static void MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reviews")
            .WithTags("Reviews")
            .RequireRateLimiting("default");

        group.MapGet("/product/{productId}", GetByProductId)
            .WithName("GetReviewsByProductId").AllowAnonymous()
            .Produces<PaginatedResult<ReviewDto>>(200);
        group.MapGet("/{id}", GetById)
            .WithName("GetReviewById").AllowAnonymous()
            .Produces<ReviewDto>(200).Produces(404);
        group.MapPost("/", CreateReview)
            .RequireAuthorization().WithName("CreateReview")
            .Produces<ReviewDto>(201).ProducesValidationProblem();
        group.MapPost("/{id}/response", CreateResponse)
            .RequireAuthorization("AdminOnly").WithName("CreateReviewResponse")
            .Produces<ReviewDto>(200).ProducesValidationProblem().Produces(404);
        group.MapPost("/{id}/helpful", MarkHelpful)
            .RequireAuthorization().WithName("MarkReviewHelpful")
            .Produces<ReviewDto>(200).Produces(404);
        // H-10: PUT → PATCH に変更
        group.MapPatch("/{id}/status", UpdateStatus)
            .RequireAuthorization("AdminOnly").WithName("UpdateReviewStatus")
            .Produces<ReviewDto>(200).ProducesValidationProblem().Produces(404);
    }

    /// <summary>
    /// GET /api/reviews/product/{productId} — 商品IDに紐づくレビュー一覧を取得する（認証不要）。
    /// </summary>
    /// <param name="productId">商品ID</param>
    /// <param name="query">ページネーションパラメータ</param>
    /// <param name="validator">ページネーションバリデーター</param>
    /// <param name="service">レビューサービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>レビュー一覧</returns>
    private static async Task<IResult> GetByProductId(
        string productId,
        [AsParameters] PaginationParams query,
        IValidator<PaginationParams> validator,
        IReviewService service,
        CancellationToken ct)
    {
        // H-7: PaginationParams バリデーション追加
        var validationResult = await validator.ValidateAsync(query, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        return Results.Ok(await service.GetByProductIdAsync(productId, query.Page, query.Size, ct));
    }

    /// <summary>
    /// GET /api/reviews/{id} — レビューIDでレビュー詳細を取得する（認証不要）。
    /// </summary>
    /// <param name="id">レビューID</param>
    /// <param name="service">レビューサービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>レビューが存在すれば 200 OK、存在しなければ 404 NotFound</returns>
    private static async Task<IResult> GetById(
        string id,
        IReviewService service,
        CancellationToken ct)
        => await service.GetByIdAsync(id, ct) is { } review
            ? Results.Ok(review)
            : Results.NotFound();

    /// <summary>
    /// POST /api/reviews — 新規レビューを投稿する（認証必須）。
    /// </summary>
    /// <param name="request">レビュー作成リクエスト</param>
    /// <param name="validator">バリデーター</param>
    /// <param name="user">認証済みユーザーの ClaimsPrincipal</param>
    /// <param name="service">レビューサービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>作成成功時は 201 Created、バリデーション失敗時は 400</returns>
    private static async Task<IResult> CreateReview(
        [FromBody] ReviewCreateRequest request,
        IValidator<ReviewCreateRequest> validator,
        ClaimsPrincipal user,
        IReviewService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException();
        var review = await service.CreateAsync(request, userId, ct);
        return Results.Created($"/api/reviews/{review.Id}", review);
    }

    /// <summary>
    /// POST /api/reviews/{id}/response — レビューに管理者返信を追加する（AdminOnly 認可必須）。
    /// </summary>
    /// <param name="id">対象レビューID</param>
    /// <param name="request">返信作成リクエスト</param>
    /// <param name="validator">バリデーター</param>
    /// <param name="user">認証済み管理者の ClaimsPrincipal</param>
    /// <param name="service">レビューサービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>返信追加後のレビュー情報</returns>
    private static async Task<IResult> CreateResponse(
        string id,
        [FromBody] ReviewResponseCreateRequest request,
        IValidator<ReviewResponseCreateRequest> validator,
        ClaimsPrincipal user,
        IReviewService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var responderId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException();
        var review = await service.AddResponseAsync(id, request, responderId, ct);
        return Results.Ok(review);
    }

    /// <summary>
    /// POST /api/reviews/{id}/helpful — レビューに「参考になった」投票を行う（認証必須）。
    /// </summary>
    /// <param name="id">対象レビューID</param>
    /// <param name="user">認証済みユーザーの ClaimsPrincipal</param>
    /// <param name="service">レビューサービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>投票後のレビュー情報</returns>
    private static async Task<IResult> MarkHelpful(
        string id,
        ClaimsPrincipal user,
        IReviewService service,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException();
        var review = await service.MarkHelpfulAsync(id, userId, ct);
        return Results.Ok(review);
    }

    /// <summary>
    /// PATCH /api/reviews/{id}/status — レビューのステータスを部分更新する（AdminOnly 認可必須）。
    /// </summary>
    /// <param name="id">対象レビューID</param>
    /// <param name="request">ステータス更新リクエスト</param>
    /// <param name="validator">バリデーター</param>
    /// <param name="service">レビューサービス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>ステータス更新後のレビュー情報</returns>
    private static async Task<IResult> UpdateStatus(
        string id,
        [FromBody] ReviewStatusUpdateRequest request,
        IValidator<ReviewStatusUpdateRequest> validator,
        IReviewService service,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var review = await service.UpdateStatusAsync(id, request.Status, ct);
        return Results.Ok(review);
    }
}
