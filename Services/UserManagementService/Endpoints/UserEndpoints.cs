using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using UserManagementService.DTOs.Requests;
using UserManagementService.Exceptions;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.Endpoints;

/// <summary>
/// ユーザープロファイルの Minimal API エンドポイント。
/// 全エンドポイントで IDOR 防止（ログインユーザー ID とリクエスト先 userId の照合）を実施する。
/// Admin ロールは他ユーザーのプロフィール参照・更新が可能。
/// </summary>
public static class UserEndpoints
{
    /// <summary>ユーザープロフィールエンドポイントをルートグループ <c>/api/v1/users</c> に登録する。</summary>
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users")
            .WithTags("ユーザープロファイル")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapGet("/{id}", GetUserById).WithName("GetUserById");
        group.MapPut("/{id}", UpdateUser).WithName("UpdateUser");
        group.MapGet("/me", GetCurrentUser).WithName("GetCurrentUser");
    }

    private static async Task<IResult> GetUserById(
        string id,
        ClaimsPrincipal user,
        IUserService userService,
        CancellationToken ct)
    {
        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        var isAdmin = user.IsInRole("Admin") || user.IsInRole("ADMIN");

        if (!isAdmin && authenticatedUserId != id)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");

        return await userService.GetByIdAsync(id, ct) is { } profile
            ? Results.Ok(profile)
            : Results.NotFound();
    }

    private static async Task<IResult> UpdateUser(
        string id,
        [FromBody] UpdateUserRequest request,
        ClaimsPrincipal user,
        IValidator<UpdateUserRequest> validator,
        IUserService userService,
        CancellationToken ct)
    {
        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        var isAdmin = user.IsInRole("Admin") || user.IsInRole("ADMIN");

        if (!isAdmin && authenticatedUserId != id)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");

        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var updated = await userService.UpdateProfileAsync(id, request, ct);
        return Results.Ok(updated);
    }

    private static async Task<IResult> GetCurrentUser(
        ClaimsPrincipal user,
        IUserService userService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();

        return await userService.GetByIdAsync(userId, ct) is { } profile
            ? Results.Ok(profile)
            : Results.NotFound();
    }
}
