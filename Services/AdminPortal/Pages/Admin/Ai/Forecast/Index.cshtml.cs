using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminPortal.Pages.Admin.Ai.Forecast;

[Authorize(Roles = "ADMIN,MANAGER,STAFF")]
public class IndexModel(
    IHttpClientFactory httpClientFactory,
    ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? ProductFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? CategoryFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string PeriodFilter { get; set; } = "monthly";

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    public int TotalPages { get; set; }
    public int PageSize { get; set; } = 20;
    public List<ForecastItem> Forecasts { get; set; } = [];
    public string? StatusMessage { get; set; }
    public bool IsStaffRole => User.IsInRole("Staff") && !User.IsInRole("Admin") && !User.IsInRole("Manager");

    public async Task OnGetAsync(CancellationToken ct = default)
    {
        logger.LogInformation("AI需要予測ページにアクセス: Operator={Operator}", User.Identity?.Name ?? "unknown");
        await LoadForecastsAsync(ct);
    }

    public async Task<IActionResult> OnPostGenerateAsync(CancellationToken ct = default)
    {
        logger.LogInformation("需要予測生成リクエスト: Operator={Operator}", User.Identity?.Name ?? "unknown");

        if (IsStaffRole)
        {
            StatusMessage = "予測生成の権限がありません。";
            await LoadForecastsAsync(ct);
            return Page();
        }

        var client = httpClientFactory.CreateClient("ApiGateway");
        var payload = new
        {
            Category = CategoryFilter,
            Period = PeriodFilter
        };
        var response = await client.PostAsJsonAsync("/api/v1/admin/ai/forecast/generate", payload, ct);

        StatusMessage = response.IsSuccessStatusCode
            ? "需要予測を生成しました。"
            : "需要予測の生成に失敗しました。";

        await LoadForecastsAsync(ct);
        return Page();
    }

    private async Task LoadForecastsAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("ApiGateway");
        var query = $"/api/v1/admin/ai/forecast?page={CurrentPage}&pageSize={PageSize}&period={PeriodFilter}";
        if (!string.IsNullOrWhiteSpace(ProductFilter))
        {
            query += $"&product={Uri.EscapeDataString(ProductFilter)}";
        }
        if (!string.IsNullOrWhiteSpace(CategoryFilter))
        {
            query += $"&category={Uri.EscapeDataString(CategoryFilter)}";
        }

        var response = await client.GetAsync(query, ct);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<ForecastListResponse>(cancellationToken: ct);
            if (result is not null)
            {
                Forecasts = result.Items;
                TotalPages = result.TotalPages;
            }
        }
        else
        {
            logger.LogWarning("需要予測データ取得失敗: StatusCode={StatusCode}", response.StatusCode);
        }
    }

    public record ForecastItem(
        string ProductId,
        string ProductName,
        string Category,
        string Period,
        int ForecastQuantity,
        decimal Confidence,
        DateTime GeneratedAt);

    public record ForecastListResponse(
        List<ForecastItem> Items,
        int TotalCount,
        int TotalPages,
        int CurrentPage);
}
