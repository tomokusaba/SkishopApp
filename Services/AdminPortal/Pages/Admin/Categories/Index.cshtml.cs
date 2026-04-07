using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminPortal.Pages.Admin.Categories;

[Authorize(Roles = "ADMIN,MANAGER")]
public class IndexModel(
    IHttpClientFactory httpClientFactory,
    ILogger<IndexModel> logger) : PageModel
{
    public List<CategoryItem> Categories { get; set; } = [];

    [BindProperty]
    [Required(ErrorMessage = "カテゴリ名は必須です")]
    [StringLength(100, MinimumLength = 1)]
    public string NewCategoryName { get; set; } = string.Empty;

    [BindProperty]
    [StringLength(500)]
    public string? NewCategoryDescription { get; set; }

    [BindProperty]
    public string? EditCategoryId { get; set; }

    [BindProperty]
    [StringLength(100)]
    public string? EditCategoryName { get; set; }

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync(CancellationToken ct = default)
    {
        await LoadCategoriesAsync(ct);
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(NewCategoryName))
        {
            ErrorMessage = "カテゴリ名は必須です";
            await LoadCategoriesAsync(ct);
            return Page();
        }

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var request = new { Name = NewCategoryName, Description = NewCategoryDescription };
            var response = await client.PostAsJsonAsync("/api/categories", request, ct);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("カテゴリ作成成功: Name={CategoryName}", NewCategoryName);
                SuccessMessage = "カテゴリを作成しました";
            }
            else
            {
                logger.LogWarning("カテゴリ作成失敗: StatusCode={StatusCode}", response.StatusCode);
                ErrorMessage = "カテゴリの作成に失敗しました";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogError(ex, "カテゴリ作成API接続エラー: {Message}", ex.Message);
            ErrorMessage = "サーバーに接続できません";
        }

        await LoadCategoriesAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostEditAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(EditCategoryId) || string.IsNullOrWhiteSpace(EditCategoryName))
        {
            ErrorMessage = "カテゴリIDと名前は必須です";
            await LoadCategoriesAsync(ct);
            return Page();
        }

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var request = new { Name = EditCategoryName };
            var response = await client.PatchAsJsonAsync($"/api/categories/{EditCategoryId}", request, ct);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("カテゴリ更新成功: CategoryId={CategoryId}", EditCategoryId);
                SuccessMessage = "カテゴリを更新しました";
            }
            else
            {
                ErrorMessage = "カテゴリの更新に失敗しました";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogError(ex, "カテゴリ更新エラー: {Message}", ex.Message);
            ErrorMessage = "サーバーに接続できません";
        }

        await LoadCategoriesAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        [FromForm] string categoryId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            return RedirectToPage();
        }

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var response = await client.DeleteAsync($"/api/categories/{categoryId}", ct);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("カテゴリ削除成功: CategoryId={CategoryId}", categoryId);
            }
            else
            {
                logger.LogWarning("カテゴリ削除失敗: CategoryId={CategoryId}, StatusCode={StatusCode}",
                    categoryId, response.StatusCode);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogError(ex, "カテゴリ削除エラー: {Message}", ex.Message);
        }

        return RedirectToPage();
    }

    private async Task LoadCategoriesAsync(CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var response = await client.GetAsync("/api/categories", ct);
            if (response.IsSuccessStatusCode)
            {
                Categories = await response.Content.ReadFromJsonAsync<List<CategoryItem>>(ct) ?? [];
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogWarning(ex, "カテゴリ一覧取得エラー: {Message}", ex.Message);
            ErrorMessage = "カテゴリデータの取得に失敗しました";
        }
    }

    public sealed record CategoryItem(string Id, string Name, string? Description, int ProductCount);
}
