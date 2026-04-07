using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminPortal.Pages.Admin.Campaigns;

[Authorize(Roles = "ADMIN,MANAGER")]
public class IndexModel(
    IHttpClientFactory httpClientFactory,
    ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string StatusFilter { get; set; } = "all";

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    public int TotalPages { get; set; }
    public int PageSize { get; set; } = 20;
    public List<CampaignListItem> Campaigns { get; set; } = [];
    public string? StatusMessage { get; set; }

    [BindProperty]
    public CampaignFormInput CampaignForm { get; set; } = new();

    public async Task OnGetAsync(CancellationToken ct = default)
    {
        logger.LogInformation("キャンペーン管理ページにアクセス: Operator={Operator}", User.Identity?.Name ?? "unknown");
        await LoadCampaignsAsync(ct);
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct = default)
    {
        logger.LogInformation("キャンペーン作成リクエスト: Name={Name}, Operator={Operator}",
            CampaignForm.Name, User.Identity?.Name ?? "unknown");

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var response = await client.PostAsJsonAsync("/api/v1/admin/campaigns", CampaignForm, ct);

            StatusMessage = response.IsSuccessStatusCode
                ? "キャンペーンを作成しました。"
                : "キャンペーンの作成に失敗しました。";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "キャンペーン作成中にエラー: Name={Name}", CampaignForm.Name);
            StatusMessage = "キャンペーン作成中にエラーが発生しました。";
        }

        await LoadCampaignsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAsync(string campaignId, CancellationToken ct = default)
    {
        logger.LogInformation("キャンペーン更新リクエスト: CampaignId={CampaignId}, Operator={Operator}",
            campaignId, User.Identity?.Name ?? "unknown");

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var response = await client.PutAsJsonAsync($"/api/v1/admin/campaigns/{campaignId}", CampaignForm, ct);

            StatusMessage = response.IsSuccessStatusCode
                ? "キャンペーンを更新しました。"
                : "キャンペーンの更新に失敗しました。";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "キャンペーン更新中にエラー: CampaignId={CampaignId}", campaignId);
            StatusMessage = "キャンペーン更新中にエラーが発生しました。";
        }

        await LoadCampaignsAsync(ct);
        return Page();
    }

    private async Task LoadCampaignsAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("ApiGateway");
        var query = $"/api/v1/admin/campaigns?page={CurrentPage}&pageSize={PageSize}";
        if (StatusFilter != "all")
        {
            query += $"&status={Uri.EscapeDataString(StatusFilter)}";
        }

        var response = await client.GetAsync(query, ct);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<CampaignListResponse>(cancellationToken: ct);
            if (result is not null)
            {
                Campaigns = result.Items;
                TotalPages = result.TotalPages;
            }
        }
        else
        {
            logger.LogWarning("キャンペーン一覧取得失敗: StatusCode={StatusCode}", response.StatusCode);
        }
    }

    public record CampaignListItem(
        string Id,
        string Name,
        string Description,
        string Status,
        DateTime StartDate,
        DateTime EndDate,
        decimal? DiscountRate,
        string? TargetCategory);

    public record CampaignListResponse(
        List<CampaignListItem> Items,
        int TotalCount,
        int TotalPages,
        int CurrentPage);

    public class CampaignFormInput
    {
        [Required(ErrorMessage = "キャンペーン名は必須です")]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime EndDate { get; set; } = DateTime.UtcNow.AddDays(30);

        [Range(0, 100)]
        public decimal? DiscountRate { get; set; }

        [StringLength(100)]
        public string? TargetCategory { get; set; }
    }
}
