using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminPortal.Pages.Admin.Ai.Models;

[Authorize(Roles = "ADMIN,MANAGER")]
public class IndexModel(
    IHttpClientFactory httpClientFactory,
    ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    public int TotalPages { get; set; }
    public int PageSize { get; set; } = 20;
    public List<AiModelItem> Models { get; set; } = [];
    public string? StatusMessage { get; set; }
    public bool IsAdmin => User.IsInRole("Admin");

    public async Task OnGetAsync(CancellationToken ct = default)
    {
        logger.LogInformation("AIモデル管理ページにアクセス: Operator={Operator}", User.Identity?.Name ?? "unknown");
        await LoadModelsAsync(ct);
    }

    public async Task<IActionResult> OnPostTrainAsync(string modelId, CancellationToken ct = default)
    {
        if (!IsAdmin)
        {
            StatusMessage = "モデルのトレーニング権限がありません。";
            await LoadModelsAsync(ct);
            return Page();
        }

        logger.LogInformation("モデルトレーニングリクエスト: ModelId={ModelId}, Operator={Operator}",
            modelId, User.Identity?.Name ?? "unknown");

        var client = httpClientFactory.CreateClient("ApiGateway");
        var response = await client.PostAsync($"/api/v1/admin/ai/models/{modelId}/train", null, ct);

        StatusMessage = response.IsSuccessStatusCode
            ? "モデルのトレーニングを開始しました。"
            : "モデルのトレーニング開始に失敗しました。";

        await LoadModelsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string modelId, CancellationToken ct = default)
    {
        if (!IsAdmin)
        {
            StatusMessage = "モデルの削除権限がありません。";
            await LoadModelsAsync(ct);
            return Page();
        }

        logger.LogInformation("モデル削除リクエスト: ModelId={ModelId}, Operator={Operator}",
            modelId, User.Identity?.Name ?? "unknown");

        var client = httpClientFactory.CreateClient("ApiGateway");
        var response = await client.DeleteAsync($"/api/v1/admin/ai/models/{modelId}", ct);

        StatusMessage = response.IsSuccessStatusCode
            ? "モデルを削除しました。"
            : "モデルの削除に失敗しました。";

        await LoadModelsAsync(ct);
        return Page();
    }

    private async Task LoadModelsAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("ApiGateway");
        var response = await client.GetAsync($"/api/v1/admin/ai/models?page={CurrentPage}&pageSize={PageSize}", ct);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<AiModelListResponse>(cancellationToken: ct);
            if (result is not null)
            {
                Models = result.Items;
                TotalPages = result.TotalPages;
            }
        }
        else
        {
            logger.LogWarning("AIモデル一覧取得失敗: StatusCode={StatusCode}", response.StatusCode);
        }
    }

    public record AiModelItem(
        string Id,
        string Name,
        string Type,
        string Version,
        string Status,
        decimal? Accuracy,
        DateTime CreatedAt,
        DateTime? LastTrainedAt);

    public record AiModelListResponse(
        List<AiModelItem> Items,
        int TotalCount,
        int TotalPages,
        int CurrentPage);
}
