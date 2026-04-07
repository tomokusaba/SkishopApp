using System.Security.Claims;
using UserManagementService.Exceptions;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.Endpoints;

/// <summary>
/// アクティビティ履歴の Minimal API エンドポイント。
/// Admin は他ユーザーの履歴も参照可能、一般ユーザーは自分のみ。
/// </summary>
public static class ActivityEndpoints
{
    private const int MaxPageSize = 100;

    public static void MapActivityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users")
            .WithTags("ユーザーアクティビティ")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapGet("/{id}/activities", GetActivities).WithName("GetActivities");
        group.MapGet("/me/activities", GetMyActivities).WithName("GetMyActivities");
    }

    private static async Task<IResult> GetActivities(
        string id, int page, int pageSize,
        ClaimsPrincipal user, IActivityService activityService, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > MaxPageSize)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["page"] = ["page は 1 以上で指定してください"],
                ["pageSize"] = [$"pageSize は 1 以上 {MaxPageSize} 以下で指定してください"]
            });

        var authenticatedUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();
        var isAdmin = user.IsInRole("Admin") || user.IsInRole("ADMIN");
        if (!isAdmin && authenticatedUserId != id)
            return TypedResults.Problem(statusCode: 403, title: "Forbidden");

        var (items, totalCount) = await activityService.GetByUserIdAsync(id, page, pageSize, ct);
        return Results.Ok(new { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize });
    }

    private static async Task<IResult> GetMyActivities(
        int page, int pageSize,
        ClaimsPrincipal user, IActivityService activityService, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > MaxPageSize)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["page"] = ["page は 1 以上で指定してください"],
                ["pageSize"] = [$"pageSize は 1 以上 {MaxPageSize} 以下で指定してください"]
            });

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();

        var (items, totalCount) = await activityService.GetByUserIdAsync(userId, page, pageSize, ct);
        return Results.Ok(new { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize });
    }
}
