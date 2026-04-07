using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminPortal.Pages.Admin.Ai.Analytics;

[Authorize(Roles = "ADMIN,MANAGER,STAFF")]
public class IndexModel(
    IHttpClientFactory httpClientFactory,
    ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string ActiveTab { get; set; } = "search";

    [BindProperty(SupportsGet = true)]
    public string PeriodFilter { get; set; } = "last30days";

    public SearchAnalyticsData? SearchAnalytics { get; set; }
    public RecommendationAnalyticsData? RecommendationAnalytics { get; set; }
    public ChatAnalyticsData? ChatAnalytics { get; set; }

    public async Task OnGetAsync(CancellationToken ct = default)
    {
        logger.LogInformation("AI分析ダッシュボードにアクセス: Operator={Operator}, Tab={Tab}",
            User.Identity?.Name ?? "unknown", ActiveTab);

        switch (ActiveTab)
        {
            case "search":
                await LoadSearchAnalyticsAsync(ct);
                break;
            case "recommendation":
                await LoadRecommendationAnalyticsAsync(ct);
                break;
            case "chat":
                await LoadChatAnalyticsAsync(ct);
                break;
        }
    }

    private async Task LoadSearchAnalyticsAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("ApiGateway");
        var response = await client.GetAsync($"/api/v1/admin/ai/analytics/search?period={PeriodFilter}", ct);
        if (response.IsSuccessStatusCode)
        {
            SearchAnalytics = await response.Content.ReadFromJsonAsync<SearchAnalyticsData>(cancellationToken: ct);
        }
        else
        {
            logger.LogWarning("検索分析データ取得失敗: StatusCode={StatusCode}", response.StatusCode);
        }
    }

    private async Task LoadRecommendationAnalyticsAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("ApiGateway");
        var response = await client.GetAsync($"/api/v1/admin/ai/analytics/recommendations?period={PeriodFilter}", ct);
        if (response.IsSuccessStatusCode)
        {
            RecommendationAnalytics = await response.Content.ReadFromJsonAsync<RecommendationAnalyticsData>(cancellationToken: ct);
        }
        else
        {
            logger.LogWarning("レコメンド分析データ取得失敗: StatusCode={StatusCode}", response.StatusCode);
        }
    }

    private async Task LoadChatAnalyticsAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("ApiGateway");
        var response = await client.GetAsync($"/api/v1/admin/ai/analytics/chat?period={PeriodFilter}", ct);
        if (response.IsSuccessStatusCode)
        {
            ChatAnalytics = await response.Content.ReadFromJsonAsync<ChatAnalyticsData>(cancellationToken: ct);
        }
        else
        {
            logger.LogWarning("チャット分析データ取得失敗: StatusCode={StatusCode}", response.StatusCode);
        }
    }

    public record SearchAnalyticsData(
        int TotalSearches,
        int UniqueUsers,
        decimal AverageResultCount,
        List<TopSearchTerm> TopSearchTerms,
        List<DailySearchStat> DailyStats);

    public record TopSearchTerm(string Term, int Count, decimal ConversionRate);
    public record DailySearchStat(DateTime Date, int SearchCount, int ResultClicks);

    public record RecommendationAnalyticsData(
        int TotalRecommendations,
        decimal ClickThroughRate,
        decimal ConversionRate,
        List<RecommendationTypeStats> TypeStats,
        List<DailyRecommendationStat> DailyStats);

    public record RecommendationTypeStats(string Type, int Count, decimal Ctr);
    public record DailyRecommendationStat(DateTime Date, int Shown, int Clicked, int Converted);

    public record ChatAnalyticsData(
        int TotalSessions,
        decimal AverageSessionDuration,
        decimal SatisfactionRate,
        List<ChatTopicStats> TopTopics,
        List<DailyChatStat> DailyStats);

    public record ChatTopicStats(string Topic, int Count, decimal ResolutionRate);
    public record DailyChatStat(DateTime Date, int Sessions, decimal AvgDuration);
}
