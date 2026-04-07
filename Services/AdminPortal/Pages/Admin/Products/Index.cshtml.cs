using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminPortal.Pages.Admin.Products;

[Authorize(Roles = "ADMIN,MANAGER")]
public class IndexModel(
    IHttpClientFactory httpClientFactory,
    ILogger<IndexModel> logger) : PageModel
{
    public List<ProductListItem> Products { get; set; } = [];
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public string? SearchKeyword { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(
        [FromQuery] int page = 1,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        CurrentPage = page < 1 ? 1 : page;
        SearchKeyword = search;

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var url = $"/api/products?page={CurrentPage}&size=20";
            if (!string.IsNullOrWhiteSpace(search))
            {
                url += $"&search={Uri.EscapeDataString(search)}";
            }

            var response = await client.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ProductListResponse>(ct);
                if (result is not null)
                {
                    Products = result.Items;
                    TotalPages = result.TotalPages;
                }
            }
            else
            {
                logger.LogWarning("商品一覧取得失敗: StatusCode={StatusCode}", response.StatusCode);
                ErrorMessage = "商品一覧の取得に失敗しました";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogError(ex, "商品一覧API接続エラー: {Message}", ex.Message);
            ErrorMessage = "商品データの取得に失敗しました";
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        [FromForm] string productId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(productId))
        {
            return RedirectToPage();
        }

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var response = await client.DeleteAsync($"/api/products/{productId}", ct);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("商品削除成功: ProductId={ProductId}", productId);
            }
            else
            {
                logger.LogWarning("商品削除失敗: ProductId={ProductId}, StatusCode={StatusCode}",
                    productId, response.StatusCode);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogError(ex, "商品削除API接続エラー: {Message}", ex.Message);
        }

        return RedirectToPage();
    }

    public sealed record ProductListItem(
        string Id,
        string Name,
        string Category,
        decimal Price,
        int StockQuantity,
        bool IsActive);

    public sealed record ProductListResponse(
        List<ProductListItem> Items,
        int TotalPages,
        int TotalCount);
}
