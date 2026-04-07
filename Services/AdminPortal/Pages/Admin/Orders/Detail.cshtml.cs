using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminPortal.Pages.Admin.Orders;

[Authorize(Roles = "ADMIN,MANAGER,STAFF")]
public class DetailModel(
    IHttpClientFactory httpClientFactory,
    ILogger<DetailModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = string.Empty;

    public OrderDetail? Order { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "ステータスは必須です")]
    [StringLength(50)]
    public string NewStatus { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public bool IsManagerOrAbove { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            return RedirectToPage("/Admin/Orders/Index");
        }

        IsManagerOrAbove = User.IsInRole("Admin") || User.IsInRole("Manager");
        await LoadOrderAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateStatusAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(NewStatus))
        {
            ErrorMessage = "注文IDとステータスは必須です";
            await LoadOrderAsync(ct);
            return Page();
        }

        // H-28: BFF でのロール制限は UX 向上のための UI ガード。
        // 本来のビジネスルール（Staff が特定ステータスのみ変更可能）は
        // バックエンドの SalesManagementService で強制される。
        // BFF 側での制限は二重チェック（Defense in Depth）として維持する。
        var isStaff = !User.IsInRole("Admin") && !User.IsInRole("Manager");
        var allowedForStaff = new[] { "Processing", "Shipped" };
        if (isStaff && !allowedForStaff.Contains(NewStatus))
        {
            ErrorMessage = "このステータスに変更する権限がありません";
            IsManagerOrAbove = false;
            await LoadOrderAsync(ct);
            return Page();
        }

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var request = new { Status = NewStatus };
            // H-24: ステータス変更は PUT を使用（REST 規約）
            var response = await client.PutAsJsonAsync($"/api/v1/orders/{Id}/status", request, ct);

            if (response.IsSuccessStatusCode)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
                logger.LogInformation("注文ステータス更新: OrderId={OrderId}, NewStatus={NewStatus}, UpdatedBy={UserId}",
                    Id, NewStatus, userId);
                SuccessMessage = $"ステータスを「{NewStatus}」に更新しました";
            }
            else
            {
                logger.LogWarning("注文ステータス更新失敗: OrderId={OrderId}, StatusCode={StatusCode}",
                    Id, response.StatusCode);
                ErrorMessage = "ステータスの更新に失敗しました";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogError(ex, "注文ステータス更新API接続エラー: {Message}", ex.Message);
            ErrorMessage = "サーバーに接続できません";
        }

        IsManagerOrAbove = User.IsInRole("Admin") || User.IsInRole("Manager");
        await LoadOrderAsync(ct);
        return Page();
    }

    private async Task LoadOrderAsync(CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var response = await client.GetAsync($"/api/v1/orders/{Id}", ct);
            if (response.IsSuccessStatusCode)
            {
                Order = await response.Content.ReadFromJsonAsync<OrderDetail>(ct);
            }
            else
            {
                ErrorMessage = "注文が見つかりません";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogWarning(ex, "注文詳細取得エラー: OrderId={OrderId}, {Message}", Id, ex.Message);
            ErrorMessage = "注文データの取得に失敗しました";
        }
    }

    public sealed record OrderDetail(
        string Id,
        string CustomerName,
        string CustomerEmail,
        string Status,
        decimal TotalAmount,
        DateTime OrderDate,
        string? ShippingAddress,
        List<OrderItemDetail> Items);

    public sealed record OrderItemDetail(
        string ProductName,
        int Quantity,
        decimal UnitPrice,
        decimal SubTotal);
}
