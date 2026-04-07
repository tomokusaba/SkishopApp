using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminPortal.Pages.Admin.Users;

[Authorize(Policy = "AdminOnly")]
public class IndexModel(
    IHttpClientFactory httpClientFactory,
    ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    public int TotalPages { get; set; }
    public int PageSize { get; set; } = 20;
    public List<UserListItem> Users { get; set; } = [];
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(CancellationToken ct = default)
    {
        logger.LogInformation("ユーザー管理ページにアクセス: Operator={Operator}", User.Identity?.Name ?? "unknown");
        await LoadUsersAsync(ct);
    }

    public async Task<IActionResult> OnPostUpdateRoleAsync(string userId, string newRole, CancellationToken ct = default)
    {
        logger.LogInformation("ロール変更リクエスト: TargetUserId={TargetUserId}, NewRole={NewRole}, Operator={Operator}",
            userId, newRole, User.Identity?.Name ?? "unknown");

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var response = await client.PutAsJsonAsync(
                $"/api/v1/admin/users/{userId}/role",
                new { Role = newRole },
                ct);

            StatusMessage = response.IsSuccessStatusCode
                ? "ロールを更新しました。"
                : "ロールの更新に失敗しました。";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ロール変更中にエラー: TargetUserId={TargetUserId}", userId);
            StatusMessage = "ロール変更中にエラーが発生しました。";
        }

        await LoadUsersAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostLockAsync(string userId, CancellationToken ct = default)
    {
        logger.LogInformation("ユーザーロックリクエスト: TargetUserId={TargetUserId}, Operator={Operator}",
            userId, User.Identity?.Name ?? "unknown");

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var response = await client.PutAsJsonAsync(
                $"/api/v1/admin/users/{userId}/status",
                new { IsLocked = true },
                ct);

            StatusMessage = response.IsSuccessStatusCode
                ? "ユーザーをロックしました。"
                : "ユーザーのロックに失敗しました。";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ユーザーロック中にエラー: TargetUserId={TargetUserId}", userId);
            StatusMessage = "ユーザーロック中にエラーが発生しました。";
        }

        await LoadUsersAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostUnlockAsync(string userId, CancellationToken ct = default)
    {
        logger.LogInformation("ユーザーアンロックリクエスト: TargetUserId={TargetUserId}, Operator={Operator}",
            userId, User.Identity?.Name ?? "unknown");

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var response = await client.PutAsJsonAsync(
                $"/api/v1/admin/users/{userId}/status",
                new { IsLocked = false },
                ct);

            StatusMessage = response.IsSuccessStatusCode
                ? "ユーザーのロックを解除しました。"
                : "ユーザーのロック解除に失敗しました。";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ユーザーアンロック中にエラー: TargetUserId={TargetUserId}", userId);
            StatusMessage = "ユーザーのロック解除中にエラーが発生しました。";
        }

        await LoadUsersAsync(ct);
        return Page();
    }

    private async Task LoadUsersAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("ApiGateway");
        var query = $"/api/v1/admin/users?page={CurrentPage}&pageSize={PageSize}";
        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            query += $"&search={Uri.EscapeDataString(SearchTerm)}";
        }

        var response = await client.GetAsync(query, ct);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<UserListResponse>(cancellationToken: ct);
            if (result is not null)
            {
                Users = result.Items;
                TotalPages = result.TotalPages;
            }
        }
        else
        {
            logger.LogWarning("ユーザー一覧取得失敗: StatusCode={StatusCode}", response.StatusCode);
        }
    }

    public record UserListItem(
        string Id,
        string Email,
        string UserName,
        string Role,
        bool IsLocked,
        DateTime CreatedAt,
        DateTime? LastLoginAt);

    public record UserListResponse(
        List<UserListItem> Items,
        int TotalCount,
        int TotalPages,
        int CurrentPage);
}
