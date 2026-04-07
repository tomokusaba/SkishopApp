using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminPortal.Pages.Admin;

[Authorize(Policy = "StaffOrAbove")]
public class IndexModel(
    IHttpClientFactory httpClientFactory,
    ILogger<IndexModel> logger) : PageModel
{
    public decimal TodaySales { get; private set; }
    public int TodayOrderCount { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken ct = default)
    {
        logger.LogInformation("管理ダッシュボードにアクセス: UserId={UserId}",
            User.Identity?.Name ?? "unknown");

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var response = await client.GetAsync($"/api/v1/reports/sales?startDate={today}&endDate={today}", ct);
            if (response.IsSuccessStatusCode)
            {
                var salesData = await response.Content.ReadFromJsonAsync<DailySalesSummary>(ct);
                if (salesData is not null)
                {
                    TodaySales = salesData.TotalAmount;
                    TodayOrderCount = salesData.OrderCount;
                }
            }
            else
            {
                logger.LogWarning("日次売上レポート取得失敗: StatusCode={StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogWarning(ex, "日次売上レポートAPI接続エラー: {Message}", ex.Message);
            ErrorMessage = "売上データの取得に失敗しました";
        }
    }

    public sealed record DailySalesSummary(decimal TotalAmount, int OrderCount);
}
