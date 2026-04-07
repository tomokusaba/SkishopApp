using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminPortal.Pages.Admin.Points;

[Authorize(Policy = "AdminOnly")]
public class IndexModel(
    IHttpClientFactory httpClientFactory,
    ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string ActiveTab { get; set; } = "overview";

    public PointOverviewData? Overview { get; set; }
    public PointAnalyticsData? Analytics { get; set; }
    public string? StatusMessage { get; set; }

    [BindProperty]
    public PointAdjustmentInput Adjustment { get; set; } = new();

    public async Task OnGetAsync(CancellationToken ct = default)
    {
        logger.LogInformation("ポイント管理ページにアクセス: Operator={Operator}, Tab={Tab}",
            User.Identity?.Name ?? "unknown", ActiveTab);

        await LoadOverviewAsync(ct);

        if (ActiveTab == "analytics")
        {
            await LoadAnalyticsAsync(ct);
        }
    }

    public async Task<IActionResult> OnPostAdjustAsync(CancellationToken ct = default)
    {
        logger.LogInformation("ポイント調整リクエスト: TargetUserId={TargetUserId}, Amount={Amount}, Reason={Reason}, Operator={Operator}",
            Adjustment.UserId, Adjustment.Amount, Adjustment.Reason, User.Identity?.Name ?? "unknown");

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var response = await client.PostAsJsonAsync($"/api/v1/admin/points/users/{Adjustment.UserId}/adjust", Adjustment, ct);

            StatusMessage = response.IsSuccessStatusCode
                ? "ポイントを調整しました。"
                : "ポイントの調整に失敗しました。";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ポイント調整中にエラー: TargetUserId={TargetUserId}", Adjustment.UserId);
            StatusMessage = "ポイント調整中にエラーが発生しました。";
        }

        await LoadOverviewAsync(ct);
        return Page();
    }

    private async Task LoadOverviewAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("ApiGateway");
        var response = await client.GetAsync("/api/v1/admin/points/analytics", ct);
        if (response.IsSuccessStatusCode)
        {
            Overview = await response.Content.ReadFromJsonAsync<PointOverviewData>(cancellationToken: ct);
        }
        else
        {
            logger.LogWarning("ポイント概要取得失敗: StatusCode={StatusCode}", response.StatusCode);
        }
    }

    private async Task LoadAnalyticsAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("ApiGateway");
        var response = await client.GetAsync("/api/v1/admin/points/analytics", ct);
        if (response.IsSuccessStatusCode)
        {
            Analytics = await response.Content.ReadFromJsonAsync<PointAnalyticsData>(cancellationToken: ct);
        }
        else
        {
            logger.LogWarning("ポイント分析データ取得失敗: StatusCode={StatusCode}", response.StatusCode);
        }
    }

    public record PointOverviewData(
        long TotalIssued,
        long TotalConsumed,
        long TotalExpired,
        long CurrentBalance,
        int ActiveUsers);

    public record PointAnalyticsData(
        List<MonthlyKpi> MonthlyKpis,
        List<TierDistribution> TierDistributions);

    public record MonthlyKpi(string Month, long Issued, long Consumed, long Expired);
    public record TierDistribution(string Tier, int UserCount, decimal Percentage);

    public class PointAdjustmentInput
    {
        [Required(ErrorMessage = "ユーザーIDは必須です")]
        public string UserId { get; set; } = string.Empty;

        [StringLength(255)]
        public string UserSearchTerm { get; set; } = string.Empty;

        [Range(-1000000, 1000000)]
        public int Amount { get; set; }

        [Required(ErrorMessage = "理由は必須です")]
        [StringLength(500)]
        public string Reason { get; set; } = string.Empty;
    }
}
