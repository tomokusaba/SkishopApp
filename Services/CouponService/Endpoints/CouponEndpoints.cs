using System.Security.Claims;
using CouponService.DTOs.Requests;
using CouponService.DTOs.Responses;
using CouponService.Exceptions;
using CouponService.Services.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace CouponService.Endpoints;

public static class CouponEndpoints
{
    public static void MapCouponEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/coupons")
            .WithTags("Coupons")
            .RequireRateLimiting("coupon-api");


        // GET /api/v1/coupons — 利用可能クーポン一覧（ルートパス）
        group.MapGet("/", GetCoupons)
            .WithName("GetCoupons")
            .Produces<PagedResponse<CouponSummaryResponse>>()
            .ProducesProblem(401)
            .RequireAuthorization("UserOrAdmin");

        group.MapGet("/available", GetAvailableCoupons)
            .WithName("GetAvailableCoupons")
            .Produces<PagedResponse<CouponSummaryResponse>>()
            .ProducesProblem(401)
            .RequireAuthorization("UserOrAdmin");

        group.MapGet("/mine", GetMyCoupons)
            .WithName("GetMyCoupons")
            .Produces<List<UserCouponResponse>>()
            .ProducesProblem(401)
            .RequireAuthorization();

        group.MapPost("/{code}/acquire", AcquireCoupon)
            .WithName("AcquireCoupon")
            .Produces<UserCouponResponse>(StatusCodes.Status201Created)
            .ProducesProblem(401)
            .ProducesProblem(404)
            .RequireAuthorization();

        group.MapPost("/validate", ValidateCoupon)
            .WithName("ValidateCoupon")
            .Produces<ValidationResult>()
            .ProducesValidationProblem()
            .ProducesProblem(401)
            .RequireAuthorization();
    }

    private static async Task<IResult> GetCoupons(
        ICouponService couponService,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Results.Ok(await couponService.GetAvailableCouponsAsync(
            page > 0 ? page : 1, pageSize > 0 ? Math.Min(pageSize, 100) : 20, ct));

    private static async Task<IResult> GetAvailableCoupons(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        ICouponService couponService, CancellationToken ct)
        => Results.Ok(await couponService.GetAvailableCouponsAsync(
            page > 0 ? page : 1, pageSize > 0 ? Math.Min(pageSize, 100) : 20, ct));

    private static async Task<IResult> GetMyCoupons(
        ClaimsPrincipal user, ICouponService couponService, CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException("認証が必要です");
        return Results.Ok(await couponService.GetUserCouponsAsync(userId, ct));
    }

    private static async Task<IResult> AcquireCoupon(
        string code, ClaimsPrincipal user,
        ICouponService couponService, CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException("認証が必要です");
        var result = await couponService.AcquireCouponAsync(code, userId, ct);
        return Results.Created($"/api/v1/coupons/mine", result);
    }

    private static async Task<IResult> ValidateCoupon(
        [FromBody] ValidateCouponRequest request,
        IValidator<ValidateCouponRequest> validator,
        ClaimsPrincipal user,
        ICouponService couponService, CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException("認証が必要です");

        var result = await couponService.ValidateAndApplyAsync(
            request.Code, userId, request.OrderAmount, request.CategoryId, ct);
        return Results.Ok(result);
    }
}
