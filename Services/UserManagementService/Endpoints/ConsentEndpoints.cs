using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using UserManagementService.DTOs.Requests;
using UserManagementService.Exceptions;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.Endpoints;

/// <summary>
/// GDPR 同意管理の Minimal API エンドポイント。
/// 匿名同意（Cookie バナー等）は AllowAnonymous で提供し、専用のレート制限を適用する。
/// </summary>
public static class ConsentEndpoints
{
    public static void MapConsentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users/{userId}/consents")
            .WithTags("同意管理")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapGet("/", GetConsents).WithName("GetConsents");
        group.MapPut("/", UpdateConsent).WithName("UpdateConsent");

        app.MapPost("/api/v1/anonymous-consents", CreateAnonymousConsent)
            .WithTags("同意管理")
            .AllowAnonymous()
            .RequireRateLimiting("anonymous-consent")
            .WithName("CreateAnonymousConsent");
    }

    private static async Task<IResult> GetConsents(
        string userId, ClaimsPrincipal user,
        IConsentService consentService, CancellationToken ct)
    {
        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        if (authenticatedUserId != userId)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");
        return Results.Ok(await consentService.GetByUserIdAsync(userId, ct));
    }

    private static async Task<IResult> UpdateConsent(
        string userId, [FromBody] ConsentUpdateRequest request,
        ClaimsPrincipal user, HttpContext httpContext,
        IValidator<ConsentUpdateRequest> validator,
        IConsentService consentService, CancellationToken ct)
    {
        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        if (authenticatedUserId != userId)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");

        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        return Results.Ok(await consentService.UpdateAsync(userId, request, ipAddress, userAgent, ct));
    }

    private static async Task<IResult> CreateAnonymousConsent(
        [FromBody] ConsentUpdateRequest request, HttpContext httpContext,
        IValidator<ConsentUpdateRequest> validator,
        IConsentService consentService, CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        var result = await consentService.CreateAnonymousConsentAsync(request, ipAddress, userAgent, ct);
        return Results.Created("/api/v1/anonymous-consents", result);
    }
}
