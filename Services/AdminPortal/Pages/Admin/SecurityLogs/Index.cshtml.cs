using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminPortal.Pages.Admin.SecurityLogs;

[Authorize(Policy = "AdminOnly")]
public class IndexModel(
    IHttpClientFactory httpClientFactory,
    ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? EventTypeFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? UserFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    public int TotalPages { get; set; }
    public int PageSize { get; set; } = 50;
    public List<SecurityLogItem> Logs { get; set; } = [];

    public static readonly string[] EventTypes =
    [
        "LOGIN_SUCCESS",
        "LOGIN_FAILURE",
        "LOGOUT",
        "PASSWORD_CHANGE",
        "ROLE_CHANGE",
        "ACCOUNT_LOCKED",
        "ACCOUNT_UNLOCKED",
        "TOKEN_REFRESH",
        "ACCESS_DENIED",
        "DATA_EXPORT"
    ];

    public async Task OnGetAsync(CancellationToken ct = default)
    {
        logger.LogInformation("セキュリティログページにアクセス: Operator={Operator}", User.Identity?.Name ?? "unknown");

        StartDate ??= DateTime.UtcNow.AddDays(-7);
        EndDate ??= DateTime.UtcNow;

        await LoadLogsAsync(ct);
    }

    private async Task LoadLogsAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("ApiGateway");
        var query = $"/api/v1/auth/security-logs?page={CurrentPage}&pageSize={PageSize}" +
                    $"&startDate={StartDate:yyyy-MM-dd}&endDate={EndDate:yyyy-MM-dd}";

        if (!string.IsNullOrWhiteSpace(EventTypeFilter))
        {
            query += $"&eventType={Uri.EscapeDataString(EventTypeFilter)}";
        }
        if (!string.IsNullOrWhiteSpace(UserFilter))
        {
            query += $"&user={Uri.EscapeDataString(UserFilter)}";
        }

        var response = await client.GetAsync(query, ct);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<SecurityLogResponse>(cancellationToken: ct);
            if (result is not null)
            {
                Logs = result.Items;
                TotalPages = result.TotalPages;
            }
        }
        else
        {
            logger.LogWarning("セキュリティログ取得失敗: StatusCode={StatusCode}", response.StatusCode);
        }
    }

    public record SecurityLogItem(
        string Id,
        string EventType,
        string UserId,
        string UserEmail,
        string IpAddress,
        string? Details,
        DateTime OccurredAt);

    public record SecurityLogResponse(
        List<SecurityLogItem> Items,
        int TotalCount,
        int TotalPages,
        int CurrentPage);
}
