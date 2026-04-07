using CouponService.DTOs.Requests;
using CouponService.DTOs.Responses;
using CouponService.Services.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace CouponService.Endpoints;

public static class CampaignEndpoints
{
    public static void MapCampaignEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/campaigns")
            .WithTags("Admin Campaigns")
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("coupon-api");


        group.MapGet("/", GetAllCampaigns)
            .WithName("AdminGetAllCampaigns")
            .Produces<PagedResponse<CampaignResponse>>();
        group.MapPost("/", CreateCampaign)
            .WithName("AdminCreateCampaign")
            .Produces<CampaignResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();
        group.MapGet("/{id}", GetCampaignById)
            .WithName("AdminGetCampaignById")
            .Produces<CampaignResponse>()
            .ProducesProblem(404);
        group.MapPut("/{id}", UpdateCampaign)
            .WithName("AdminUpdateCampaign")
            .Produces<CampaignResponse>()
            .ProducesValidationProblem();
        group.MapPost("/{id}/activate", ActivateCampaign)
            .WithName("AdminActivateCampaign")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(404)
            .ProducesProblem(422);
        group.MapPost("/{id}/pause", PauseCampaign)
            .WithName("AdminPauseCampaign")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(404)
            .ProducesProblem(422);
    }

    private static async Task<IResult> GetAllCampaigns(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        ICampaignService campaignService, CancellationToken ct)
        => Results.Ok(await campaignService.GetAllAsync(
            page > 0 ? page : 1, pageSize > 0 ? Math.Min(pageSize, 100) : 20, ct));

    private static async Task<IResult> CreateCampaign(
        [FromBody] CreateCampaignRequest request,
        IValidator<CreateCampaignRequest> validator,
        ICampaignService campaignService, CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var campaign = await campaignService.CreateCampaignAsync(request, ct);
        return Results.Created($"/api/v1/admin/campaigns/{campaign.Id}", campaign);
    }

    private static async Task<IResult> GetCampaignById(
        string id, ICampaignService campaignService, CancellationToken ct)
        => await campaignService.GetByIdAsync(id, ct) is { } campaign
            ? Results.Ok(campaign)
            : Results.NotFound();

    private static async Task<IResult> UpdateCampaign(
        string id, [FromBody] UpdateCampaignRequest request,
        IValidator<UpdateCampaignRequest> validator,
        ICampaignService campaignService, CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var campaign = await campaignService.UpdateCampaignAsync(id, request, ct);
        return Results.Ok(campaign);
    }

    private static async Task<IResult> ActivateCampaign(
        string id, ICampaignService campaignService, CancellationToken ct)
    {
        await campaignService.ActivateCampaignAsync(id, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> PauseCampaign(
        string id, ICampaignService campaignService, CancellationToken ct)
    {
        await campaignService.PauseCampaignAsync(id, ct);
        return Results.NoContent();
    }
}
