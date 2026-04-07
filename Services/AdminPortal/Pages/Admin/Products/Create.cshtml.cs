using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminPortal.Pages.Admin.Products;

[Authorize(Roles = "ADMIN,MANAGER")]
public class CreateModel(
    IHttpClientFactory httpClientFactory,
    ILogger<CreateModel> logger) : PageModel
{
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
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync(CancellationToken ct = default)
    {
        await LoadCategoriesAsync(ct);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            ErrorMessage = "入力内容にエラーがあります";
            await LoadCategoriesAsync(ct);
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

            var response = await client.PostAsJsonAsync("/api/products", request, ct);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("商品作成成功: Name={ProductName}", Name);
                return RedirectToPage("/Admin/Products/Index");
            }

            logger.LogWarning("商品作成失敗: StatusCode={StatusCode}", response.StatusCode);
            ErrorMessage = "商品の作成に失敗しました";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogError(ex, "商品作成API接続エラー: {Message}", ex.Message);
            ErrorMessage = "サーバーに接続できません";
        }

        await LoadCategoriesAsync(ct);
        return Page();
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
        }
    }

    public sealed record CategoryItem(string Id, string Name);
}
