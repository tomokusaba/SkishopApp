using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminPortal.Pages.Admin.Shipments;

[Authorize(Roles = "ADMIN,MANAGER,STAFF")]
public class IndexModel(
    IHttpClientFactory httpClientFactory,
    ILogger<IndexModel> logger) : PageModel
{
    public List<ShipmentItem> Shipments { get; set; } = [];
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public string? StatusFilter { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "注文IDは必須です")]
    [StringLength(36)]
    public string CreateOrderId { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "配送業者は必須です")]
    [StringLength(100)]
    public string CreateCarrier { get; set; } = string.Empty;

    [BindProperty]
    [StringLength(100)]
    public string? CreateTrackingNumber { get; set; }

    public async Task OnGetAsync(
        [FromQuery] int page = 1,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        CurrentPage = page < 1 ? 1 : page;
        StatusFilter = status;
        await LoadShipmentsAsync(ct);
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(CreateOrderId) || string.IsNullOrWhiteSpace(CreateCarrier))
        {
            ErrorMessage = "注文IDと配送業者は必須です";
            await LoadShipmentsAsync(ct);
            return Page();
        }

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var request = new
            {
                OrderId = CreateOrderId,
                Carrier = CreateCarrier,
                TrackingNumber = CreateTrackingNumber
            };
            var response = await client.PostAsJsonAsync("/api/v1/shipments", request, ct);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("配送作成成功: OrderId={OrderId}", CreateOrderId);
                SuccessMessage = "配送を作成しました";
            }
            else
            {
                logger.LogWarning("配送作成失敗: StatusCode={StatusCode}", response.StatusCode);
                ErrorMessage = "配送の作成に失敗しました";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogError(ex, "配送作成API接続エラー: {Message}", ex.Message);
            ErrorMessage = "サーバーに接続できません";
        }

        await LoadShipmentsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateStatusAsync(
        [FromForm] string shipmentId,
        [FromForm] string newStatus,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(shipmentId) || string.IsNullOrWhiteSpace(newStatus))
        {
            return RedirectToPage();
        }

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var request = new { Status = newStatus };
            // H-24: ステータス変更は PUT を使用（REST 規約）
            var response = await client.PutAsJsonAsync($"/api/v1/shipments/{shipmentId}/status", request, ct);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("配送ステータス更新: ShipmentId={ShipmentId}, Status={Status}",
                    shipmentId, newStatus);
            }
            else
            {
                logger.LogWarning("配送ステータス更新失敗: ShipmentId={ShipmentId}, StatusCode={StatusCode}",
                    shipmentId, response.StatusCode);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogError(ex, "配送ステータス更新エラー: {Message}", ex.Message);
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateTrackingAsync(
        [FromForm] string shipmentId,
        [FromForm] string trackingNumber,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(shipmentId) || string.IsNullOrWhiteSpace(trackingNumber))
        {
            return RedirectToPage();
        }

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var request = new { TrackingNumber = trackingNumber };
            var response = await client.PutAsJsonAsync($"/api/v1/shipments/{shipmentId}/tracking", request, ct);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("追跡番号更新: ShipmentId={ShipmentId}", shipmentId);
            }
            else
            {
                logger.LogWarning("追跡番号更新失敗: ShipmentId={ShipmentId}", shipmentId);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogError(ex, "追跡番号更新エラー: {Message}", ex.Message);
        }

        return RedirectToPage();
    }

    private async Task LoadShipmentsAsync(CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var url = $"/api/v1/shipments?page={CurrentPage}&size=20";
            if (!string.IsNullOrWhiteSpace(StatusFilter))
                url += $"&status={Uri.EscapeDataString(StatusFilter)}";

            var response = await client.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ShipmentListResponse>(ct);
                if (result is not null)
                {
                    Shipments = result.Items;
                    TotalPages = result.TotalPages;
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogWarning(ex, "配送一覧取得エラー: {Message}", ex.Message);
            ErrorMessage = "配送データの取得に失敗しました";
        }
    }

    public sealed record ShipmentItem(
        string Id,
        string OrderId,
        string Carrier,
        string? TrackingNumber,
        string Status,
        DateTime CreatedAt,
        DateTime? ShippedAt,
        DateTime? DeliveredAt);

    public sealed record ShipmentListResponse(
        List<ShipmentItem> Items,
        int TotalPages,
        int TotalCount);
}
