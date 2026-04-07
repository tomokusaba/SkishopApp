using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using PointService.DTOs.Requests;
using PointService.DTOs.Responses;
using PointService.Exceptions;
using PointService.Services.Interfaces;

namespace PointService.Endpoints;

public static class PointEndpoints
{
    public static void MapPointEndpoints(this IEndpointRouteBuilder app)
    {
        // ── 一般ユーザー向け ──
        var userGroup = app.MapGroup("/api/v1/points")
            .WithTags("Points")
            .RequireAuthorization();

        userGroup.MapGet("/balance", GetBalance).WithName("GetPointBalance");
        userGroup.MapGet("/history", GetHistory).WithName("GetPointHistory");
        userGroup.MapGet("/tier", GetTierInfo).WithName("GetTierInfo");
        userGroup.MapGet("/expiring", GetExpiringPoints).WithName("GetExpiringPoints");

        // ── 管理者向け ──
        var adminGroup = app.MapGroup("/api/v1/admin/points")
            .WithTags("PointsAdmin")
            .RequireAuthorization("AdminOnly");

        adminGroup.MapGet("/users/{userId}/balance", GetUserBalance)
            .WithName("AdminGetUserBalance");
        adminGroup.MapPost("/users/{userId}/adjust", AdjustPoints)
            .WithName("AdminAdjustPoints");
        adminGroup.MapGet("/analytics", GetAnalytics)
            .WithName("GetPointAnalytics");

        // ── 内部 API ──
        var internalGroup = app.MapGroup("/api/v1/internal/points")
            .WithTags("PointsInternal")
            .RequireAuthorization("InternalServiceOnly");

        internalGroup.MapGet("/users/{userId}/balance", GetInternalBalance)
            .WithName("InternalGetBalance");
        internalGroup.MapPost("/reserve", ReservePoints)
            .WithName("InternalReservePoints");
        internalGroup.MapPost("/confirm", ConfirmPoints)
            .WithName("InternalConfirmPoints");
        internalGroup.MapPost("/release", ReleasePoints)
            .WithName("InternalReleasePoints");
        internalGroup.MapPost("/award", AwardPoints)
            .WithName("InternalAwardPoints");
        internalGroup.MapPost("/accounts", CreateAccount)
            .WithName("InternalCreateAccount");
    }

    private static async Task<IResult> GetBalance(
        ClaimsPrincipal user,
        IPointService pointService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        return Results.Ok(await pointService.GetBalanceAsync(userId, ct));
    }

    private static async Task<IResult> GetHistory(
        ClaimsPrincipal user,
        [AsParameters] PaginationQuery query,
        IValidator<PaginationQuery> paginationValidator,
        IPointService pointService,
        CancellationToken ct)
    {
        var paginationResult = await paginationValidator.ValidateAsync(query, ct);
        if (!paginationResult.IsValid)
            return Results.ValidationProblem(paginationResult.ToDictionary());

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        return Results.Ok(await pointService.GetTransactionHistoryAsync(
            userId, query.Page, query.PageSize, ct));
    }

    private static async Task<IResult> GetTierInfo(
        ClaimsPrincipal user,
        ITierService tierService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        return Results.Ok(await tierService.GetTierInfoAsync(userId, ct));
    }

    private static async Task<IResult> GetExpiringPoints(
        ClaimsPrincipal user,
        IPointService pointService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        return Results.Ok(await pointService.GetExpiringPointsAsync(userId, ct));
    }

    private static async Task<IResult> GetUserBalance(
        string userId,
        IPointService pointService,
        CancellationToken ct)
        => Results.Ok(await pointService.GetBalanceAsync(userId, ct));

    private static async Task<IResult> AdjustPoints(
        string userId,
        [FromBody] AdjustPointsRequest request,
        IValidator<AdjustPointsRequest> validator,
        ClaimsPrincipal adminUser,
        HttpContext httpContext,
        IPointService pointService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var adminUserId = adminUser.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        await pointService.AdjustPointsAsync(
            userId, request, adminUserId, ipAddress, userAgent, ct);
        return Results.Ok();
    }

    private static async Task<IResult> GetAnalytics(
        IPointAnalyticsService analyticsService,
        CancellationToken ct)
        => Results.Ok(await analyticsService.GetAnalyticsAsync(ct));

    private static async Task<IResult> GetInternalBalance(
        string userId,
        IPointService pointService,
        CancellationToken ct)
        => Results.Ok(await pointService.GetBalanceAsync(userId, ct));

    private static async Task<IResult> ReservePoints(
        [FromBody] ReservePointsRequest request,
        IValidator<ReservePointsRequest> validator,
        IPointService pointService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var result = await pointService.ReservePointsAsync(request, ct);
        return result.Success ? Results.Ok(result) : Results.UnprocessableEntity(result);
    }

    private static async Task<IResult> ConfirmPoints(
        [FromBody] ConfirmPointsRequest request,
        IValidator<ConfirmPointsRequest> validator,
        IPointService pointService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var confirmed = await pointService.ConfirmPointsAsync(
            request.UserId, request.OrderId, ct);
        return Results.Ok(new ConfirmPointsResponse(confirmed));
    }

    private static async Task<IResult> ReleasePoints(
        [FromBody] ReleasePointsRequest request,
        IValidator<ReleasePointsRequest> validator,
        IPointService pointService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var released = await pointService.ReleasePointsAsync(
            request.UserId, request.OrderId, ct);
        return Results.Ok(new ReleasePointsResponse(released));
    }

    private static async Task<IResult> AwardPoints(
        [FromBody] AwardPointsRequest request,
        IValidator<AwardPointsRequest> validator,
        IPointService pointService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var result = await pointService.EarnPointsAsync(
            request.UserId, request.OrderId, request.OrderAmount, ct);
        return Results.Ok(new AwardPointsResponse(result.EarnedPoints));
    }

    private static async Task<IResult> CreateAccount(
        [FromBody] CreateAccountRequest request,
        IValidator<CreateAccountRequest> validator,
        IPointService pointService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        await pointService.CreateAccountAsync(request.UserId, ct);
        return Results.Created($"/api/v1/internal/points/users/{request.UserId}/balance", 
            new { userId = request.UserId });
    }
}
