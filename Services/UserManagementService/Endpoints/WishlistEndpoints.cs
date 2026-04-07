using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using UserManagementService.DTOs.Requests;
using UserManagementService.Exceptions;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.Endpoints;

/// <summary>
/// ウィッシュリストの Minimal API エンドポイント。CRUD ・アイテム追加・カート移動を提供する。
/// 全操作でオーナーシップ検証（IDOR 防止）を実施する。
/// </summary>
public static class WishlistEndpoints
{
    public static void MapWishlistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users/{userId}/wishlists")
            .WithTags("ウィッシュリスト")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapGet("/", GetWishlists).WithName("GetWishlists");
        group.MapPost("/", CreateWishlist).WithName("CreateWishlist");
        group.MapPut("/{id}", UpdateWishlist).WithName("UpdateWishlist");
        group.MapDelete("/{id}", DeleteWishlist).WithName("DeleteWishlist");
        group.MapPost("/{id}/items", AddItem).WithName("AddWishlistItem");
        group.MapPost("/{id}/items/{itemId}/cart", MoveItemToCart).WithName("MoveWishlistItemToCart");
        group.MapDelete("/{id}/items/{itemId}", RemoveItem).WithName("RemoveWishlistItem");
    }

    private static async Task<IResult> GetWishlists(
        string userId, ClaimsPrincipal user,
        IWishlistService wishlistService, CancellationToken ct)
    {
        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        if (authenticatedUserId != userId)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");
        return Results.Ok(await wishlistService.GetByUserIdAsync(userId, ct));
    }

    private static async Task<IResult> CreateWishlist(
        string userId, [FromBody] CreateWishlistRequest request,
        ClaimsPrincipal user, IValidator<CreateWishlistRequest> validator,
        IWishlistService wishlistService, CancellationToken ct)
    {
        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        if (authenticatedUserId != userId)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");

        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var created = await wishlistService.CreateAsync(userId, request, ct);
        return Results.Created($"/api/v1/users/{userId}/wishlists/{created.Id}", created);
    }

    private static async Task<IResult> UpdateWishlist(
        string userId, string id, [FromBody] UpdateWishlistRequest request,
        ClaimsPrincipal user, IValidator<UpdateWishlistRequest> validator,
        IWishlistService wishlistService, CancellationToken ct)
    {
        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        if (authenticatedUserId != userId)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());
        return Results.Ok(await wishlistService.UpdateAsync(userId, id, request, ct));
    }

    private static async Task<IResult> DeleteWishlist(
        string userId, string id, ClaimsPrincipal user,
        IWishlistService wishlistService, CancellationToken ct)
    {
        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        if (authenticatedUserId != userId)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");
        await wishlistService.DeleteAsync(userId, id, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> AddItem(
        string userId, string id, [FromBody] AddWishlistItemRequest request,
        ClaimsPrincipal user, IValidator<AddWishlistItemRequest> validator,
        IWishlistService wishlistService, CancellationToken ct)
    {
        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        if (authenticatedUserId != userId)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());
        var item = await wishlistService.AddItemAsync(userId, id, request, ct);
        return Results.Created($"/api/v1/users/{userId}/wishlists/{id}/items/{item.Id}", item);
    }

    private static async Task<IResult> MoveItemToCart(
        string userId, string id, string itemId, ClaimsPrincipal user,
        IWishlistService wishlistService, CancellationToken ct)
    {
        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        if (authenticatedUserId != userId)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");
        await wishlistService.MoveItemToCartAsync(userId, id, itemId, ct);
        return Results.Ok();
    }

    private static async Task<IResult> RemoveItem(
        string userId, string id, string itemId, ClaimsPrincipal user,
        IWishlistService wishlistService, CancellationToken ct)
    {
        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        if (authenticatedUserId != userId)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");
        await wishlistService.RemoveItemAsync(userId, id, itemId, ct);
        return Results.NoContent();
    }
}
