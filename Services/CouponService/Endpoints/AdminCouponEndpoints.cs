using CouponService.DTOs.Requests;
using CouponService.DTOs.Responses;
using CouponService.Services.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace CouponService.Endpoints;

public static class AdminCouponEndpoints
{
    public static void MapAdminCouponEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/coupons")
            .WithTags("Admin Coupons")
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("coupon-api");


        group.MapGet("/", GetAllCoupons)
            .WithName("AdminGetAllCoupons")
            .Produces<PagedResponse<CouponResponse>>();
        group.MapPost("/", CreateCoupon)
            .WithName("AdminCreateCoupon")
            .Produces<CouponResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();
        group.MapGet("/{id}", GetCouponById)
            .WithName("AdminGetCouponById")
            .Produces<CouponResponse>()
            .ProducesProblem(404);
        group.MapPut("/{id}", UpdateCoupon)
            .WithName("AdminUpdateCoupon")
            .Produces<CouponResponse>()
            .ProducesValidationProblem();
        group.MapDelete("/{id}", DeactivateCoupon)
            .WithName("AdminDeactivateCoupon")
            .Produces(StatusCodes.Status204NoContent);
        group.MapGet("/{id}/usages", GetCouponUsages)
            .WithName("AdminGetCouponUsages")
            .Produces<PagedResponse<CouponUsageResponse>>();
        group.MapGet("/analytics", GetAnalytics)
            .WithName("AdminGetCouponAnalytics")
            .Produces<CouponAnalyticsResponse>();
    }

    private static async Task<IResult> GetAllCoupons(
        [AsParameters] CouponQueryParams query,
        ICouponService couponService, CancellationToken ct)
        => Results.Ok(await couponService.GetAllCouponsAsync(query, ct));

    private static async Task<IResult> CreateCoupon(
        [FromBody] CreateCouponRequest request,
        IValidator<CreateCouponRequest> validator,
        ICouponService couponService, CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var coupon = await couponService.CreateCouponAsync(request, ct);
        return Results.Created($"/api/v1/admin/coupons/{coupon.Id}", coupon);
    }

    private static async Task<IResult> GetCouponById(
        string id, ICouponService couponService, CancellationToken ct)
        => await couponService.GetByIdAsync(id, ct) is { } coupon
            ? Results.Ok(coupon)
            : Results.Problem(detail: $"クーポン {id} が見つかりません", statusCode: 404);

    private static async Task<IResult> UpdateCoupon(
        string id, [FromBody] UpdateCouponRequest request,
        IValidator<UpdateCouponRequest> validator,
        ICouponService couponService, CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var coupon = await couponService.UpdateCouponAsync(id, request, ct);
        return Results.Ok(coupon);
    }

    private static async Task<IResult> DeactivateCoupon(
        string id, ICouponService couponService, CancellationToken ct)
    {
        await couponService.DeactivateCouponAsync(id, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> GetCouponUsages(
        string id,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        ICouponService couponService, CancellationToken ct)
        => Results.Ok(await couponService.GetCouponUsagesAsync(
            id, page > 0 ? page : 1, pageSize > 0 ? Math.Min(pageSize, 100) : 20, ct));

    private static async Task<IResult> GetAnalytics(
        [FromQuery] string? couponId,
        ICouponAnalyticsService analyticsService, CancellationToken ct)
        => couponId is not null
            ? Results.Ok(await analyticsService.GetAnalyticsAsync(couponId, ct))
            : Results.Ok(await analyticsService.GetOverallAnalyticsAsync(ct));
}
