using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using UserManagementService.DTOs.Requests;
using UserManagementService.Exceptions;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.Endpoints;

/// <summary>
/// 管理者専用エンドポイント。AdminOnly ポリシーで保護される。
/// ユーザー一覧取得・ステータス変更・処理制限設定を担当する。
/// </summary>
public static class AdminUserEndpoints
{
    /// <summary>ページサイズの上限値。DoS 防止のため大量取得を制限する。</summary>
    private const int MaxPageSize = 100;

    /// <summary>管理者用エンドポイントをルートグループ <c>/api/v1/admin/users</c> に登録する。</summary>
    public static void MapAdminUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/users")
            .WithTags("管理者向け")
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("api");

        group.MapGet("/", GetAllUsers).WithName("AdminGetAllUsers");
        group.MapPut("/{id}/status", UpdateUserStatus).WithName("AdminUpdateUserStatus");
        group.MapPut("/{id}/processing-restriction", UpdateProcessingRestriction).WithName("AdminUpdateProcessingRestriction");
    }

    private static async Task<IResult> GetAllUsers(
        int page, int pageSize, string? status,
        IUserService userService, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > MaxPageSize)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["page"] = ["page は 1 以上で指定してください"],
                ["pageSize"] = [$"pageSize は 1 以上 {MaxPageSize} 以下で指定してください"]
            });

        var (items, totalCount) = await userService.GetAllAsync(page, pageSize, status, ct);
        return Results.Ok(new { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize });
    }

    private static async Task<IResult> UpdateUserStatus(
        string id, [FromBody] UpdateUserStatusRequest request,
        ClaimsPrincipal user, IValidator<UpdateUserStatusRequest> validator,
        IUserService userService, CancellationToken ct)
    {
        var adminId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();

        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        await userService.UpdateStatusByAdminAsync(id, request.Status, adminId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> UpdateProcessingRestriction(
        string id, [FromBody] UpdateProcessingRestrictionRequest request,
        ClaimsPrincipal user, IValidator<UpdateProcessingRestrictionRequest> validator,
        IUserService userService, CancellationToken ct)
    {
        var adminId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException();

        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        await userService.UpdateProcessingRestrictionByAdminAsync(
            id,
            request.IsProcessingRestricted,
            request.RestrictionReason,
            adminId,
            ct);
        return Results.NoContent();
    }
}
