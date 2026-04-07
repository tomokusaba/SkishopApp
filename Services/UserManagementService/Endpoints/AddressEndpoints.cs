using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using UserManagementService.DTOs.Requests;
using UserManagementService.Exceptions;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.Endpoints;

/// <summary>
/// 住所管理の Minimal API エンドポイント。IDOR 防止を全エンドポイントで実施する。
/// </summary>
public static class AddressEndpoints
{
    /// <summary>住所エンドポイントをルートグループ <c>/api/v1/users/{userId}/addresses</c> に登録する。</summary>
    public static void MapAddressEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users/{userId}/addresses")
            .WithTags("住所管理")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapGet("/", GetAddresses).WithName("GetAddresses");
        group.MapPost("/", CreateAddress).WithName("CreateAddress");
        group.MapPut("/{id}", UpdateAddress).WithName("UpdateAddress");
        group.MapDelete("/{id}", DeleteAddress).WithName("DeleteAddress");
    }

    private static async Task<IResult> GetAddresses(
        string userId, ClaimsPrincipal user,
        IAddressService addressService, CancellationToken ct)
    {
        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        var isAdmin = user.IsInRole("Admin") || user.IsInRole("ADMIN");
        if (!isAdmin && authenticatedUserId != userId)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");
        return Results.Ok(await addressService.GetByUserIdAsync(userId, ct));
    }

    private static async Task<IResult> CreateAddress(
        string userId, [FromBody] CreateAddressRequest request,
        ClaimsPrincipal user, IValidator<CreateAddressRequest> validator,
        IAddressService addressService, CancellationToken ct)
    {
        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        if (authenticatedUserId != userId)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");

        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var created = await addressService.CreateAsync(userId, request, ct);
        return Results.Created($"/api/v1/users/{userId}/addresses/{created.Id}", created);
    }

    private static async Task<IResult> UpdateAddress(
        string userId, string id, [FromBody] UpdateAddressRequest request,
        ClaimsPrincipal user, IValidator<UpdateAddressRequest> validator,
        IAddressService addressService, CancellationToken ct)
    {
        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        if (authenticatedUserId != userId)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());
        return Results.Ok(await addressService.UpdateAsync(userId, id, request, ct));
    }

    private static async Task<IResult> DeleteAddress(
        string userId, string id, ClaimsPrincipal user,
        IAddressService addressService, CancellationToken ct)
    {
        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        if (authenticatedUserId != userId)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");
        await addressService.DeleteAsync(userId, id, ct);
        return Results.NoContent();
    }
}
