using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminPortal.Pages.Admin.Orders;

[Authorize(Roles = "ADMIN,MANAGER,STAFF")]
public class IndexModel(
    IHttpClientFactory httpClientFactory,
    ILogger<IndexModel> logger) : PageModel
{
    public List<OrderListItem> Orders { get; set; } = [];
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public string? StatusFilter { get; set; }
    public string? DateFrom { get; set; }
    public string? DateTo { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(
        [FromQuery] int page = 1,
        [FromQuery] string? status = null,
        [FromQuery] string? dateFrom = null,
        [FromQuery] string? dateTo = null,
        CancellationToken ct = default)
    {
        CurrentPage = page < 1 ? 1 : page;
        StatusFilter = status;
        DateFrom = dateFrom;
        DateTo = dateTo;

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var url = $"/api/v1/orders/search?page={CurrentPage}&pageSize=20";
            if (!string.IsNullOrWhiteSpace(status))
                url += $"&status={Uri.EscapeDataString(status)}";
            if (!string.IsNullOrWhiteSpace(dateFrom))
                url += $"&dateFrom={Uri.EscapeDataString(dateFrom)}";
            if (!string.IsNullOrWhiteSpace(dateTo))
                url += $"&dateTo={Uri.EscapeDataString(dateTo)}";

            var response = await client.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<OrderListResponse>(ct);
                if (result is not null)
                {
                    Orders = result.Items;
                    TotalPages = result.TotalPages;
                }
            }
            else
            {
                logger.LogWarning("注文一覧取得失敗: StatusCode={StatusCode}", response.StatusCode);
                ErrorMessage = "注文一覧の取得に失敗しました";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogError(ex, "注文一覧API接続エラー: {Message}", ex.Message);
            ErrorMessage = "注文データの取得に失敗しました";
        }
    }

    public sealed record OrderListItem(
        string Id,
        string CustomerName,
        string Status,
        decimal TotalAmount,
        int ItemCount,
        DateTime OrderDate);

    public sealed record OrderListResponse(
        List<OrderListItem> Items,
        int TotalPages,
        int TotalCount);
}
