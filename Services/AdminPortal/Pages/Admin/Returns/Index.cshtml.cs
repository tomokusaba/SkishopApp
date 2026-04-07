using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminPortal.Pages.Admin.Returns;

[Authorize(Roles = "ADMIN,MANAGER,STAFF")]
public class IndexModel(
    IHttpClientFactory httpClientFactory,
    ILogger<IndexModel> logger) : PageModel
{
    public List<ReturnItem> Returns { get; set; } = [];
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public string? StatusFilter { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync(
        [FromQuery] int page = 1,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        CurrentPage = page < 1 ? 1 : page;
        StatusFilter = status;
        await LoadReturnsAsync(ct);
    }

    public async Task<IActionResult> OnPostApproveAsync(
        [FromForm] string returnId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(returnId))
        {
            return RedirectToPage();
        }

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var request = new { Status = "Approved" };
            // H-24: ステータス変更は PUT を使用（REST 規約）
            var response = await client.PutAsJsonAsync($"/api/v1/returns/{returnId}/status", request, ct);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("返品承認: ReturnId={ReturnId}", returnId);
                SuccessMessage = "返品を承認しました";
            }
            else
            {
                logger.LogWarning("返品承認失敗: ReturnId={ReturnId}, StatusCode={StatusCode}",
                    returnId, response.StatusCode);
                ErrorMessage = "返品の承認に失敗しました";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogError(ex, "返品承認API接続エラー: {Message}", ex.Message);
            ErrorMessage = "サーバーに接続できません";
        }

        await LoadReturnsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostRejectAsync(
        [FromForm] string returnId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(returnId))
        {
            return RedirectToPage();
        }

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var request = new { Status = "Rejected" };
            // H-24: ステータス変更は PUT を使用（REST 規約）
            var response = await client.PutAsJsonAsync($"/api/v1/returns/{returnId}/status", request, ct);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("返品却下: ReturnId={ReturnId}", returnId);
                SuccessMessage = "返品を却下しました";
            }
            else
            {
                ErrorMessage = "返品の却下に失敗しました";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogError(ex, "返品却下API接続エラー: {Message}", ex.Message);
            ErrorMessage = "サーバーに接続できません";
        }

        await LoadReturnsAsync(ct);
        return Page();
    }

    private async Task LoadReturnsAsync(CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var url = $"/api/v1/returns?page={CurrentPage}&size=20";
            if (!string.IsNullOrWhiteSpace(StatusFilter))
                url += $"&status={Uri.EscapeDataString(StatusFilter)}";

            var response = await client.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ReturnListResponse>(ct);
                if (result is not null)
                {
                    Returns = result.Items;
                    TotalPages = result.TotalPages;
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            logger.LogWarning(ex, "返品一覧取得エラー: {Message}", ex.Message);
            ErrorMessage = "返品データの取得に失敗しました";
        }
    }

    public sealed record ReturnItem(
        string Id,
        string OrderId,
        string CustomerName,
        string Reason,
        string Status,
        decimal RefundAmount,
        DateTime RequestedAt);

    public sealed record ReturnListResponse(
        List<ReturnItem> Items,
        int TotalPages,
        int TotalCount);
}
