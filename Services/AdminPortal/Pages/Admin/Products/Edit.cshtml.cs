using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminPortal.Pages.Admin.Products;

[Authorize(Roles = "ADMIN,MANAGER")]
public class EditModel(
    IHttpClientFactory httpClientFactory,
    ILogger<EditModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "商品名は必須です")]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    [StringLength(4000)]
    public string Description { get; set; } = string.Empty;

    [BindProperty]
    [Range(0, 9999999.99, ErrorMessage = "価格は0以上を指定してください")]
    public decimal Price { get; set; }

    [BindProperty]
    [Range(0, 9999999.99)]
    public decimal? SalePrice { get; set; }

    [BindProperty]
    public DateTime? SaleStartDate { get; set; }

    [BindProperty]
    public DateTime? SaleEndDate { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "カテゴリは必須です")]
    public string CategoryId { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "SKUは必須です")]
    [StringLength(100)]
    public string Sku { get; set; } = string.Empty;

    [BindProperty]
    [StringLength(500)]
    public string? ImageUrl { get; set; }

    public List<CategoryItem> Categories { get; set; } = [];
    public List<PriceHistoryItem> PriceHistory { get; set; } = [];
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            return RedirectToPage("/Admin/Products/Index");
        }

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var response = await client.GetAsync($"/api/products/{Id}", ct);
            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = "商品が見つかりません";
                return Page();
            }

            var product = await response.Content.ReadFromJsonAsync<ProductDetail>(ct);
            if (product is not null)
            {
                Name = product.Name;
                Description = product.Description;
                Price = product.Price;
                SalePrice = product.SalePrice;
                SaleStartDate = product.SaleStartDate;
                SaleEndDate = product.SaleEndDate;
                CategoryId = product.CategoryId;
                Sku = product.Sku;
                ImageUrl = product.ImageUrl;
            }

            await LoadCategoriesAsync(client, ct);
            await LoadPriceHistoryAsync(client, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogError(ex, "商品詳細取得エラー: ProductId={ProductId}, {Message}", Id, ex.Message);
            ErrorMessage = "商品データの取得に失敗しました";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "商品名は必須です";
            var client2 = httpClientFactory.CreateClient("ApiGateway");
            await LoadCategoriesAsync(client2, ct);
            await LoadPriceHistoryAsync(client2, ct);
            return Page();
        }

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var request = new
            {
                Name,
                Description,
                Price,
                SalePrice,
                SaleStartDate,
                SaleEndDate,
                CategoryId,
                Sku,
                ImageUrl
            };

            var response = await client.PatchAsJsonAsync($"/api/products/{Id}", request, ct);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("商品更新成功: ProductId={ProductId}", Id);
                return RedirectToPage("/Admin/Products/Index");
            }

            logger.LogWarning("商品更新失敗: ProductId={ProductId}, StatusCode={StatusCode}",
                Id, response.StatusCode);
            ErrorMessage = "商品の更新に失敗しました";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogError(ex, "商品更新API接続エラー: {Message}", ex.Message);
            ErrorMessage = "サーバーに接続できません";
        }

        var reloadClient = httpClientFactory.CreateClient("ApiGateway");
        await LoadCategoriesAsync(reloadClient, ct);
        await LoadPriceHistoryAsync(reloadClient, ct);
        return Page();
    }

    private async Task LoadCategoriesAsync(HttpClient client, CancellationToken ct)
    {
        try
        {
            var response = await client.GetAsync("/api/categories", ct);
            if (response.IsSuccessStatusCode)
            {
                Categories = await response.Content.ReadFromJsonAsync<List<CategoryItem>>(ct) ?? [];
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogWarning(ex, "カテゴリ取得エラー: {Message}", ex.Message);
        }
    }

    private async Task LoadPriceHistoryAsync(HttpClient client, CancellationToken ct)
    {
        try
        {
            var response = await client.GetAsync($"/api/prices/history/{Id}", ct);
            if (response.IsSuccessStatusCode)
            {
                PriceHistory = await response.Content.ReadFromJsonAsync<List<PriceHistoryItem>>(ct) ?? [];
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogWarning(ex, "価格履歴取得エラー: {Message}", ex.Message);
        }
    }

    public sealed record ProductDetail(
        string Id,
        string Name,
        string Description,
        decimal Price,
        decimal? SalePrice,
        DateTime? SaleStartDate,
        DateTime? SaleEndDate,
        string CategoryId,
        string Sku,
        string? ImageUrl);

    public sealed record CategoryItem(string Id, string Name);

    public sealed record PriceHistoryItem(
        decimal Price,
        decimal? SalePrice,
        DateTime ChangedAt,
        string ChangedBy);
}
