using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminPortal.Pages.Admin.Reports;

[Authorize(Roles = "ADMIN,MANAGER")]
public class IndexModel(
    IHttpClientFactory httpClientFactory,
    ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string ActiveTab { get; set; } = "sales";

    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Period { get; set; } = "daily";

    public SalesReportData? SalesReport { get; set; }
    public InventoryReportData? InventoryReport { get; set; }

    public async Task OnGetAsync(CancellationToken ct = default)
    {
        logger.LogInformation("レポートページにアクセス: Operator={Operator}, Tab={Tab}",
            User.Identity?.Name ?? "unknown", ActiveTab);

        StartDate ??= DateTime.UtcNow.AddDays(-30);
        EndDate ??= DateTime.UtcNow;

        if (ActiveTab == "sales")
        {
            await LoadSalesReportAsync(ct);
        }
        else if (ActiveTab == "inventory")
        {
            await LoadInventoryReportAsync(ct);
        }
    }

    private async Task LoadSalesReportAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("ApiGateway");
        var query = $"/api/v1/reports/sales?startDate={StartDate:yyyy-MM-dd}&endDate={EndDate:yyyy-MM-dd}&period={Period}";
        var response = await client.GetAsync(query, ct);
        if (response.IsSuccessStatusCode)
        {
            SalesReport = await response.Content.ReadFromJsonAsync<SalesReportData>(cancellationToken: ct);
        }
        else
        {
            logger.LogWarning("売上レポート取得失敗: StatusCode={StatusCode}", response.StatusCode);
        }
    }

    private async Task LoadInventoryReportAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("ApiGateway");
        var response = await client.GetAsync("/api/v1/reports/inventory", ct);
        if (response.IsSuccessStatusCode)
        {
            InventoryReport = await response.Content.ReadFromJsonAsync<InventoryReportData>(cancellationToken: ct);
        }
        else
        {
            logger.LogWarning("在庫レポート取得失敗: StatusCode={StatusCode}", response.StatusCode);
        }
    }

    public record SalesReportData(
        decimal TotalRevenue,
        int TotalOrders,
        decimal AverageOrderValue,
        List<SalesSummaryItem> Summary);

    public record SalesSummaryItem(
        string PeriodLabel,
        decimal Revenue,
        int OrderCount,
        decimal AverageValue);

    public record InventoryReportData(
        int TotalProducts,
        int LowStockCount,
        int OutOfStockCount,
        List<StockLevelItem> StockLevels,
        List<LowStockAlert> LowStockAlerts);

    public record StockLevelItem(
        string ProductId,
        string ProductName,
        string Category,
        int CurrentStock,
        int ReorderLevel);

    public record LowStockAlert(
        string ProductId,
        string ProductName,
        int CurrentStock,
        int ReorderLevel,
        string Severity);
}
