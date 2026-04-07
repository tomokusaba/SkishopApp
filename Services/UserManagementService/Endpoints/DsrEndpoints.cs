using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using UserManagementService.DTOs.Requests;
using UserManagementService.Exceptions;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.Endpoints;

/// <summary>
/// GDPR データ主体権利（DSR）の Minimal API エンドポイント。
/// 削除リクエスト作成・照会・キャンセル、データエクスポート要求を提供する。
/// </summary>
public static class DsrEndpoints
{
    public static void MapDsrEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users/{userId}")
            .WithTags("GDPR/DSR")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapPost("/deletion-request", CreateDeletionRequest).WithName("CreateDeletionRequest");
        group.MapGet("/deletion-request", GetDeletionRequest).WithName("GetDeletionRequest");
        group.MapPost("/deletion-request/cancel", CancelDeletionRequest).WithName("CancelDeletionRequest");
        group.MapPost("/data-export", RequestDataExport).WithName("RequestDataExport");
    }

    private static async Task<IResult> CreateDeletionRequest(
        string userId, [FromBody] CreateDeletionRequest request,
        ClaimsPrincipal user, IValidator<CreateDeletionRequest> validator,
        IDsrService dsrService, CancellationToken ct)
    {
        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        var isAdmin = user.IsInRole("Admin") || user.IsInRole("ADMIN");
        if (!isAdmin && authenticatedUserId != userId)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");

        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var result = await dsrService.CreateDeletionRequestAsync(userId, authenticatedUserId, request, ct);
        return Results.Created($"/api/v1/users/{userId}/deletion-request", result);
    }

    private static async Task<IResult> GetDeletionRequest(
        string userId, ClaimsPrincipal user,
        IDsrService dsrService, CancellationToken ct)
    {
        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        var isAdmin = user.IsInRole("Admin") || user.IsInRole("ADMIN");
        if (!isAdmin && authenticatedUserId != userId)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");

        return await dsrService.GetDeletionRequestAsync(userId, ct) is { } request
            ? Results.Ok(request)
            : Results.NotFound();
    }

    private static async Task<IResult> CancelDeletionRequest(
        string userId, ClaimsPrincipal user,
        IDsrService dsrService, CancellationToken ct)
    {
        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        if (authenticatedUserId != userId)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");

        await dsrService.CancelDeletionRequestAsync(userId, ct);
        return Results.Ok();
    }

    private static async Task<IResult> RequestDataExport(
        string userId, ClaimsPrincipal user,
        IUserService userService, CancellationToken ct)
    {
        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        if (authenticatedUserId != userId)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");

        await userService.RequestDataExportAsync(userId, ct);
        return Results.Accepted();
    }
}
