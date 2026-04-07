using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using UserManagementService.DTOs.Requests;
using UserManagementService.Exceptions;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.Endpoints;

/// <summary>
/// ユーザー設定の Minimal API エンドポイント。Admin は他ユーザーの設定参照可、更新は本人のみ。
/// </summary>
public static class PreferenceEndpoints
{
    public static void MapPreferenceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users/{userId}/preferences")
            .WithTags("ユーザー設定")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapGet("/", GetPreferences).WithName("GetPreferences");
        group.MapPut("/", UpdatePreferences).WithName("UpdatePreferences");
    }

    private static async Task<IResult> GetPreferences(
        string userId, ClaimsPrincipal user,
        IPreferenceService preferenceService, CancellationToken ct)
    {
        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        var isAdmin = user.IsInRole("Admin") || user.IsInRole("ADMIN");
        if (!isAdmin && authenticatedUserId != userId)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");

        return await preferenceService.GetByUserIdAsync(userId, ct) is { } pref
            ? Results.Ok(pref)
            : Results.NotFound();
    }

    private static async Task<IResult> UpdatePreferences(
        string userId, [FromBody] UpdatePreferenceRequest request,
        ClaimsPrincipal user, IValidator<UpdatePreferenceRequest> validator,
        IPreferenceService preferenceService, CancellationToken ct)
    {
        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        if (authenticatedUserId != userId)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");

        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        return Results.Ok(await preferenceService.UpdateAsync(userId, request, ct));
    }
}
