using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using PointService.DTOs.Requests;
using PointService.DTOs.Responses;
using PointService.Services.Interfaces;

namespace PointService.Endpoints;

public static class TierEndpoints
{
    public static void MapTierEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/tiers")
            .WithTags("Tiers")
            .RequireAuthorization("AdminOnly");

        group.MapGet("/", GetTiers).WithName("GetTiers");
        group.MapPut("/{id}", UpdateTier).WithName("UpdateTier");
    }

    private static async Task<IResult> GetTiers(
        ITierService tierService,
        CancellationToken ct)
        => Results.Ok(await tierService.GetAllTiersAsync(ct));

    private static async Task<IResult> UpdateTier(
        string id,
        [FromBody] UpdateTierRequest request,
        IValidator<UpdateTierRequest> validator,
        ITierService tierService,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var result = await tierService.UpdateTierAsync(id, request, ct);
        return Results.Ok(result);
    }
}
