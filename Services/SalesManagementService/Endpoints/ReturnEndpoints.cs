using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SalesManagementService.DTOs.Requests;
using SalesManagementService.Infrastructure.Exceptions;
using SalesManagementService.Services.Interfaces;

namespace SalesManagementService.Endpoints;

public static class ReturnEndpoints
{
    public static void MapReturnEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/returns")
            .WithTags("Returns")
            .RequireAuthorization()
            ;

        group.MapGet("/", GetReturns)
            .RequireAuthorization("AdminOnly")
            .WithName("GetReturns");
        group.MapGet("/{id}", GetReturnById).WithName("GetReturnById");
        group.MapGet("/order/{orderId}", GetReturnsByOrderId).WithName("GetReturnsByOrderId");
        group.MapPost("/", CreateReturn).WithName("CreateReturn");
        group.MapPut("/{id}/status", ProcessReturn)
            .RequireAuthorization("AdminOnly")
            .WithName("ProcessReturn");
    }

    private static async Task<IResult> GetReturns(
        string? status,
        [AsParameters] PaginationParams pagination,
        IReturnService returnService,
        CancellationToken ct)
    {
        var result = await returnService.GetByStatusAsync(
            status ?? string.Empty, pagination.Page, pagination.PageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetReturnById(
        string id,
        ClaimsPrincipal user,
        IReturnService returnService,
        CancellationToken ct)
    {
        var returnDto = await returnService.GetByIdAsync(id, ct);
        if (returnDto is null) return Results.NotFound();

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        if (returnDto.CustomerId != userId && !user.IsInRole("Admin"))
            throw new ForbiddenException();

        return Results.Ok(returnDto);
    }

    private static async Task<IResult> GetReturnsByOrderId(
        string orderId,
        [AsParameters] PaginationParams pagination,
        ClaimsPrincipal user,
        IOrderService orderService,
        IReturnService returnService,
        CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        _ = await orderService.GetByIdAndUserIdAsync(orderId, userId, ct)
            ?? throw new NotFoundException($"注文 {orderId} が見つかりません");
        var result = await returnService.GetByOrderIdAsync(
            orderId, pagination.Page, pagination.PageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateReturn(
        [FromBody] ReturnCreateRequest request,
        IValidator<ReturnCreateRequest> validator,
        ClaimsPrincipal user,
        IReturnService returnService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        var returnDto = await returnService.CreateReturnAsync(request, userId, ct);
        return Results.Created($"/api/v1/returns/{returnDto.Id}", returnDto);
    }

    private static async Task<IResult> ProcessReturn(
        string id,
        [FromBody] ReturnProcessRequest request,
        IValidator<ReturnProcessRequest> validator,
        IReturnService returnService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        await returnService.ProcessReturnAsync(id, request, ct);
        return Results.NoContent();
    }
}
