using System.Security.Claims;
using UserManagementService.Exceptions;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.Endpoints;

/// <summary>
/// 会員ランク参照の Minimal API エンドポイント。Admin も他ユーザー参照可。
/// </summary>
public static class MemberRankEndpoints
{
    public static void MapMemberRankEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users/{userId}/member-rank")
            .WithTags("会員ランク")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapGet("/", GetMemberRank).WithName("GetMemberRank");
    }

    private static async Task<IResult> GetMemberRank(
        string userId, ClaimsPrincipal user,
        IMemberRankService memberRankService, CancellationToken ct)
    {
        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        var isAdmin = user.IsInRole("Admin") || user.IsInRole("ADMIN");
        if (!isAdmin && authenticatedUserId != userId)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");

        return await memberRankService.GetByUserIdAsync(userId, ct) is { } rank
            ? Results.Ok(rank)
            : Results.NotFound();
    }
}
