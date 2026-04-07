using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminPortal.Pages.Admin.Inventory;

[Authorize(Roles = "ADMIN,MANAGER")]
public class IndexModel(
    IHttpClientFactory httpClientFactory,
    ILogger<IndexModel> logger) : PageModel
{
    public List<InventoryItem> Items { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "商品IDは必須です")]
    [StringLength(36)]
    public string UpdateProductId { get; set; } = string.Empty;

    [BindProperty]
    [Range(0, int.MaxValue, ErrorMessage = "数量は0以上を指定してください")]
    public int UpdateQuantity { get; set; }

    public async Task OnGetAsync(CancellationToken ct = default)
    {
        await LoadInventoryAsync(ct);
    }

    public async Task<IActionResult> OnPostUpdateStockAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(UpdateProductId))
        {
            ErrorMessage = "商品IDは必須です";
            await LoadInventoryAsync(ct);
            return Page();
        }

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var request = new { ProductId = UpdateProductId, Quantity = UpdateQuantity, Reason = "管理画面からの在庫更新" };
            var response = await client.PostAsJsonAsync("/api/inventory/stock-in", request, ct);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("在庫更新成功: ProductId={ProductId}, Quantity={Quantity}",
                    UpdateProductId, UpdateQuantity);
                SuccessMessage = "在庫を更新しました";
            }
            else
            {
                logger.LogWarning("在庫更新失敗: ProductId={ProductId}, StatusCode={StatusCode}",
                    UpdateProductId, response.StatusCode);
                ErrorMessage = "在庫の更新に失敗しました";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogError(ex, "在庫更新API接続エラー: {Message}", ex.Message);
            ErrorMessage = "サーバーに接続できません";
        }

        await LoadInventoryAsync(ct);
        return Page();
    }

    private async Task LoadInventoryAsync(CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var response = await client.GetAsync("/api/inventory/low-stock", ct);
            if (response.IsSuccessStatusCode)
            {
                Items = await response.Content.ReadFromJsonAsync<List<InventoryItem>>(ct) ?? [];
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogWarning(ex, "在庫一覧取得エラー: {Message}", ex.Message);
            ErrorMessage = "在庫データの取得に失敗しました";
        }
    }

    public sealed record InventoryItem(
        string ProductId,
        string ProductName,
        int CurrentStock,
        int ThresholdQuantity,
        bool IsLowStock);
}
