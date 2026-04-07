using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SalesManagementService.DTOs.Requests;
using SalesManagementService.Infrastructure.Exceptions;
using SalesManagementService.Services.Interfaces;

namespace SalesManagementService.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/orders")
            .WithTags("Orders")
            .RequireAuthorization()
            ;

        group.MapGet("/", GetMyOrders).WithName("GetMyOrders");
        group.MapGet("/{orderId}", GetOrderById).WithName("GetOrderById");
        group.MapGet("/number/{orderNumber}", GetOrderByNumber).WithName("GetOrderByNumber");
        group.MapGet("/customer/{customerId}", GetCustomerOrders).WithName("GetCustomerOrders");
        group.MapGet("/search", SearchOrders)
            .RequireAuthorization("AdminOnly")
            .WithName("SearchOrders");
        group.MapPost("/", CreateOrder).RequireRateLimiting("order-create").WithName("CreateOrder");
        group.MapPut("/{orderId}/status", UpdateOrderStatus)
            .RequireAuthorization("AdminOnly")
            .WithName("UpdateOrderStatus");
        group.MapPost("/{orderId}/cancel", CancelOrder).WithName("CancelOrder");
    }

    private static async Task<IResult> GetMyOrders(
        [AsParameters] PaginationParams pagination,
        ClaimsPrincipal user,
        IOrderService orderService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();

        if (user.IsInRole("Admin"))
        {
            var allOrders = await orderService.SearchAsync(
                null, null, null,
                pagination.Page, pagination.PageSize, ct);
            return Results.Ok(allOrders);
        }

        var result = await orderService.GetByCustomerIdAsync(
            userId, pagination.Page, pagination.PageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetOrderById(
        string orderId,
        ClaimsPrincipal user,
        IOrderService orderService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        return await orderService.GetByIdAndUserIdAsync(orderId, userId, ct) is { } order
            ? Results.Ok(order)
            : Results.NotFound();
    }

    private static async Task<IResult> GetOrderByNumber(
        string orderNumber,
        ClaimsPrincipal user,
        IOrderService orderService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        var order = await orderService.GetByOrderNumberAsync(orderNumber, ct);
        if (order is null) return Results.NotFound();
        if (order.CustomerId != userId && !user.IsInRole("Admin"))
            throw new ForbiddenException();
        return Results.Ok(order);
    }

    private static async Task<IResult> GetCustomerOrders(
        string customerId,
        [AsParameters] PaginationParams pagination,
        ClaimsPrincipal user,
        IOrderService orderService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        if (customerId != userId && !user.IsInRole("Admin"))
            throw new ForbiddenException();
        var result = await orderService.GetByCustomerIdAsync(
            customerId, pagination.Page, pagination.PageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> SearchOrders(
        [AsParameters] OrderSearchParams searchParams,
        IOrderService orderService,
        CancellationToken ct)
    {
        var result = await orderService.SearchAsync(
            searchParams.CustomerId, searchParams.Status, searchParams.PaymentStatus,
            searchParams.Page, searchParams.PageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateOrder(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] OrderCreateRequest request,
        IValidator<OrderCreateRequest> validator,
        ClaimsPrincipal user,
        IOrderCheckoutService checkoutService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Results.BadRequest(new { Error = "Idempotency-Key ヘッダーは必須です" });

        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();

        var securedRequest = request with { CustomerId = userId };
        var order = await checkoutService.ExecuteCheckoutAsync(
            securedRequest, userId, idempotencyKey, ct);
        return Results.Created($"/api/v1/orders/{order.Id}", order);
    }

    private static async Task<IResult> UpdateOrderStatus(
        string orderId,
        [FromBody] OrderStatusUpdateRequest request,
        IValidator<OrderStatusUpdateRequest> validator,
        IOrderService orderService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        await orderService.UpdateStatusAsync(orderId, request.Status, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> CancelOrder(
        string orderId,
        [FromBody] OrderCancelRequest request,
        IValidator<OrderCancelRequest> validator,
        ClaimsPrincipal user,
        IOrderCancellationService orderCancellationService,
        IOrderService orderService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        _ = await orderService.GetByIdAndUserIdAsync(orderId, userId, ct)
            ?? throw new NotFoundException($"注文 {orderId} が見つかりません");
        await orderCancellationService.CancelOrderAsync(orderId, request.Reason, ct);
        return Results.NoContent();
    }
}
