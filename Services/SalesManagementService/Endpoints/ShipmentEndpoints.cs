using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SalesManagementService.DTOs.Requests;
using SalesManagementService.Services.Interfaces;

namespace SalesManagementService.Endpoints;

public static class ShipmentEndpoints
{
    public static void MapShipmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/shipments")
            .WithTags("Shipments")
            .RequireAuthorization("AdminOnly")
            ;

        group.MapGet("/", GetShipmentsByStatus).WithName("GetShipmentsByStatus");
        group.MapGet("/{id}", GetShipmentById).WithName("GetShipmentById");
        group.MapGet("/order/{orderId}", GetShipmentByOrderId).WithName("GetShipmentByOrderId");
        group.MapPost("/", CreateShipment).RequireRateLimiting("admin-api").WithName("CreateShipment");
        group.MapPut("/{id}/status", UpdateShipmentStatus).RequireRateLimiting("admin-api").WithName("UpdateShipmentStatus");
        group.MapPut("/{id}/tracking", UpdateTracking).RequireRateLimiting("admin-api").WithName("UpdateTracking");
    }

    private static async Task<IResult> GetShipmentsByStatus(
        string? status,
        [AsParameters] PaginationParams pagination,
        IShipmentService shipmentService,
        CancellationToken ct)
    {
        var result = await shipmentService.GetByStatusAsync(
            status ?? "PREPARING", pagination.Page, pagination.PageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetShipmentById(
        string id,
        IShipmentService shipmentService,
        CancellationToken ct)
    {
        return await shipmentService.GetByIdAsync(id, ct) is { } shipment
            ? Results.Ok(shipment)
            : Results.NotFound();
    }

    private static async Task<IResult> GetShipmentByOrderId(
        string orderId,
        IShipmentService shipmentService,
        CancellationToken ct)
    {
        return await shipmentService.GetByOrderIdAsync(orderId, ct) is { } shipment
            ? Results.Ok(shipment)
            : Results.NotFound();
    }

    private static async Task<IResult> CreateShipment(
        [FromBody] ShipmentCreateRequest request,
        IValidator<ShipmentCreateRequest> validator,
        IShipmentService shipmentService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var shipment = await shipmentService.CreateShipmentAsync(request, ct);
        return Results.Created($"/api/v1/shipments/{shipment.Id}", shipment);
    }

    private static async Task<IResult> UpdateShipmentStatus(
        string id,
        [FromBody] ShipmentUpdateRequest request,
        IValidator<ShipmentUpdateRequest> validator,
        IShipmentService shipmentService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        await shipmentService.UpdateShipmentAsync(id, request, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> UpdateTracking(
        string id,
        [FromBody] ShipmentUpdateRequest request,
        IValidator<ShipmentUpdateRequest> validator,
        IShipmentService shipmentService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        await shipmentService.UpdateShipmentAsync(id, request, ct);
        return Results.NoContent();
    }
}
