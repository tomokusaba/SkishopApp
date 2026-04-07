using CouponService.DTOs.Requests;
using CouponService.DTOs.Responses;
using CouponService.Services.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace CouponService.Endpoints;

public static class InternalCouponEndpoints
{
    public static void MapInternalCouponEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/internal/coupons")
            .WithTags("Internal Coupons")
            .RequireAuthorization("InternalServiceOnly")
            .RequireRateLimiting("redeem-api");


        group.MapPost("/calculate", CalculateDiscount)
            .WithName("InternalCalculateDiscount")
            .Produces<CalculateDiscountResponse>()
            .ProducesValidationProblem();
        group.MapPost("/redeem", RedeemCoupon)
            .WithName("InternalRedeemCoupon")
            .Produces<CouponUsageResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();
        group.MapPost("/release", ReleaseCoupon)
            .WithName("InternalReleaseCoupon")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();
    }

    private static async Task<IResult> CalculateDiscount(
        [FromBody] CalculateDiscountRequest request,
        IValidator<CalculateDiscountRequest> validator,
        ICouponService couponService, CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var result = await couponService.CalculateDiscountAsync(request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> RedeemCoupon(
        [FromBody] RedeemCouponRequest request,
        IValidator<RedeemCouponRequest> validator,
        ICouponService couponService, CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        // リクエストボディの UserId を使用（internal-service の sub ではなく実ユーザーID）
        var result = await couponService.RedeemCouponAsync(request.UserId, request, ct);
        return Results.Created($"/api/v1/internal/coupons/usages/{result.Id}", result);
    }

    private static async Task<IResult> ReleaseCoupon(
        [FromBody] ReleaseCouponRequest request,
        IValidator<ReleaseCouponRequest> validator,
        ICouponService couponService, CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        await couponService.ReleaseCouponAsync(request, ct);
        return Results.NoContent();
    }
}
