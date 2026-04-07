using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminPortal.Pages.Admin.Coupons;

[Authorize(Roles = "ADMIN,MANAGER")]
public class IndexModel(
    IHttpClientFactory httpClientFactory,
    ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string StatusFilter { get; set; } = "all";

    [BindProperty(SupportsGet = true)]
    public string ActiveTab { get; set; } = "list";

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    public int TotalPages { get; set; }
    public int PageSize { get; set; } = 20;
    public List<CouponListItem> Coupons { get; set; } = [];
    public CouponAnalyticsData? Analytics { get; set; }
    public string? StatusMessage { get; set; }

    [BindProperty]
    public CreateCouponInput NewCoupon { get; set; } = new();

    public async Task OnGetAsync(CancellationToken ct = default)
    {
        logger.LogInformation("クーポン管理ページにアクセス: Operator={Operator}, Tab={Tab}",
            User.Identity?.Name ?? "unknown", ActiveTab);

        await LoadCouponsAsync(ct);

        if (ActiveTab == "analytics")
        {
            await LoadAnalyticsAsync(ct);
        }
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct = default)
    {
        logger.LogInformation("クーポン作成リクエスト: Code={Code}, Operator={Operator}",
            NewCoupon.Code, User.Identity?.Name ?? "unknown");

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var response = await client.PostAsJsonAsync("/api/v1/admin/coupons", NewCoupon, ct);

            StatusMessage = response.IsSuccessStatusCode
                ? "クーポンを作成しました。"
                : "クーポンの作成に失敗しました。";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "クーポン作成中にエラー: Code={Code}", NewCoupon.Code);
            StatusMessage = "クーポン作成中にエラーが発生しました。";
        }

        await LoadCouponsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string couponId, CancellationToken ct = default)
    {
        logger.LogInformation("クーポン削除リクエスト: CouponId={CouponId}, Operator={Operator}",
            couponId, User.Identity?.Name ?? "unknown");

        try
        {
            var client = httpClientFactory.CreateClient("ApiGateway");
            var response = await client.DeleteAsync($"/api/v1/admin/coupons/{couponId}", ct);

            StatusMessage = response.IsSuccessStatusCode
                ? "クーポンを削除しました。"
                : "クーポンの削除に失敗しました。";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "クーポン削除中にエラー: CouponId={CouponId}", couponId);
            StatusMessage = "クーポン削除中にエラーが発生しました。";
        }

        await LoadCouponsAsync(ct);
        return Page();
    }

    private async Task LoadCouponsAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("ApiGateway");
        var query = $"/api/v1/admin/coupons?page={CurrentPage}&pageSize={PageSize}";
        if (StatusFilter != "all")
        {
            query += $"&status={Uri.EscapeDataString(StatusFilter)}";
        }

        var response = await client.GetAsync(query, ct);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<CouponListResponse>(cancellationToken: ct);
            if (result is not null)
            {
                Coupons = result.Items;
                TotalPages = result.TotalPages;
            }
        }
        else
        {
            logger.LogWarning("クーポン一覧取得失敗: StatusCode={StatusCode}", response.StatusCode);
        }
    }

    private async Task LoadAnalyticsAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("ApiGateway");
        var response = await client.GetAsync("/api/v1/admin/coupons/analytics", ct);
        if (response.IsSuccessStatusCode)
        {
            Analytics = await response.Content.ReadFromJsonAsync<CouponAnalyticsData>(cancellationToken: ct);
        }
        else
        {
            logger.LogWarning("クーポン分析データ取得失敗: StatusCode={StatusCode}", response.StatusCode);
        }
    }

    public record CouponListItem(
        string Id,
        string Code,
        string Description,
        string DiscountType,
        decimal DiscountValue,
        DateTime StartDate,
        DateTime EndDate,
        int UsageCount,
        int? MaxUsage,
        string Status);

    public record CouponListResponse(
        List<CouponListItem> Items,
        int TotalCount,
        int TotalPages,
        int CurrentPage);

    public record CouponAnalyticsData(
        decimal UsageRate,
        decimal TotalDiscountAmount,
        int TotalCouponsIssued,
        int TotalCouponsUsed,
        List<CouponUsageItem> PerCouponUsage,
        List<UsageHistoryItem> UsageHistory);

    public record CouponUsageItem(string Code, int UsageCount, decimal TotalDiscount);
    public record UsageHistoryItem(DateTime Date, int Count, decimal Amount);

    public class CreateCouponInput
    {
        [Required(ErrorMessage = "クーポンコードは必須です")]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public string DiscountType { get; set; } = "Percentage";

        [Range(0, 100)]
        public decimal DiscountValue { get; set; }
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime EndDate { get; set; } = DateTime.UtcNow.AddDays(30);

        [Range(1, int.MaxValue)]
        public int? MaxUsage { get; set; }
    }
}
