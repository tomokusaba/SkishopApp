using System.Diagnostics;
using AiSupportService.DTOs.Requests;
using AiSupportService.DTOs.Responses;
using AiSupportService.Infrastructure.Metrics;
using AiSupportService.Models;
using AiSupportService.Repositories.Interfaces;
using AiSupportService.Services.Interfaces;

namespace AiSupportService.Services;

/// <summary>
/// <see cref="ISearchService"/> の実装。商品検索とキャッシュ・分析記録を統合する。
/// </summary>
/// <remarks>
/// <para>
/// このサービスは、InventoryManagementService の商品検索 API をラップし、
/// 以下の機能を追加しています:
/// <list type="bullet">
///   <item><description>検索結果のキャッシング（5 分有効期限）</description></item>
///   <item><description>検索分析データの記録（クエリ、レスポンス時間、結果件数）</description></item>
///   <item><description>検索メトリクスの収集（Prometheus / OpenTelemetry 用）</description></item>
///   <item><description>関連度スコアの付与</description></item>
/// </list>
/// </para>
/// <para>
/// キャッシュキー形式:
/// <c>search:{query}:{category}:{minPrice}:{maxPrice}:{page}:{pageSize}</c>
/// </para>
/// </remarks>
/// <param name="productClient">商品 API クライアント。</param>
/// <param name="analyticsRepository">検索分析リポジトリ。</param>
/// <param name="metrics">AI サポートメトリクス。</param>
/// <param name="logger">ロガー。</param>
/// <param name="cacheService">キャッシュサービス。</param>
public class SearchService(
    IProductClient productClient,
    ISearchAnalyticsRepository analyticsRepository,
    AiSupportMetrics metrics,
    ILogger<SearchService> logger,
    ICacheService cacheService) : ISearchService
{
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// 処理フロー:
    /// <list type="number">
    ///   <item><description>キャッシュをチェック（ヒットした場合は即座に返却）</description></item>
    ///   <item><description>レスポンス時間計測を開始</description></item>
    ///   <item><description>商品検索 API を呼び出し</description></item>
    ///   <item><description>検索結果に関連度スコアを付与（上位ほど高いスコア）</description></item>
    ///   <item><description>検索分析データをデータベースに保存</description></item>
    ///   <item><description>検索メトリクスを記録</description></item>
    ///   <item><description>ページネーションを適用</description></item>
    ///   <item><description>結果をキャッシュ（5 分）</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 関連度スコア:
    /// 1.0 から始まり、結果の順位が下がるごとに 0.05 ずつ減少します。
    /// （例: 1位 = 1.0, 2位 = 0.95, 3位 = 0.90, ...）
    /// </para>
    /// </remarks>
    public async Task<SearchResultResponse> SearchAsync(
        SearchRequest request, string? userId = null, CancellationToken ct = default)
    {
        var cacheKey = $"search:{request.Query}:{request.Category}:{request.MinPrice}:{request.MaxPrice}:{request.Page}:{request.PageSize}";
        var cached = await cacheService.GetAsync<SearchResultResponse>(cacheKey, ct);
        if (cached is not null)
            return cached;

        var stopwatch = Stopwatch.StartNew();

        var products = await productClient.SearchProductsAsync(
            request.Query, request.Category, request.MinPrice, request.MaxPrice, ct);

        stopwatch.Stop();

        var items = products.Select((p, i) => new SearchResultItem(
            p.Id, p.Name, p.Price, p.Category, p.ImageUrl,
            1.0 - (i * 0.05))).ToList();

        var analytics = new SearchAnalytics
        {
            UserId = userId,
            Query = request.Query,
            SearchType = "KEYWORD",
            ResultsCount = items.Count,
            ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds
        };
        await analyticsRepository.AddAsync(analytics, ct);
        await analyticsRepository.SaveChangesAsync(ct);

        metrics.RecordSearchRequest();
        metrics.RecordSearchDuration(stopwatch.Elapsed.TotalSeconds);

        logger.LogInformation(
            "検索実行: Query={Query}, ResultsCount={ResultsCount}, ResponseTimeMs={ResponseTimeMs}",
            request.Query, items.Count, stopwatch.ElapsedMilliseconds);

        var result = new SearchResultResponse(
            request.Query, items.Count,
            items.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList());
        await cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), ct);
        return result;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// フィードバックは検索分析データとして記録されます。
    /// クエリは <c>feedback:{searchId}</c> 形式で保存され、
    /// クリックされた商品 ID は JSON 配列として記録されます。
    /// </para>
    /// <para>
    /// このデータは検索ランキングアルゴリズムの改善に使用されます。
    /// </para>
    /// </remarks>
    public async Task RecordFeedbackAsync(
        SearchFeedbackRequest request, string? userId = null, CancellationToken ct = default)
    {
        var analytics = new SearchAnalytics
        {
            UserId = userId,
            Query = $"feedback:{request.SearchId}",
            SearchType = "KEYWORD",
            ClickedProductIdsJson = System.Text.Json.JsonSerializer.Serialize(new[] { request.ProductId }),
            ResultsCount = 0,
            ResponseTimeMs = 0
        };
        await analyticsRepository.AddAsync(analytics, ct);
        await analyticsRepository.SaveChangesAsync(ct);

        logger.LogInformation(
            "検索フィードバック記録: SearchId={SearchId}, ProductId={ProductId}",
            request.SearchId, request.ProductId);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// サジェスト機能の実装:
    /// 入力クエリで商品検索を行い、上位 5 件の商品名を返却します。
    /// </para>
    /// <para>
    /// 将来的な拡張:
    /// <list type="bullet">
    ///   <item><description>人気検索クエリの提案</description></item>
    ///   <item><description>ユーザーの検索履歴に基づく提案</description></item>
    ///   <item><description>スペルミス補正</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public async Task<List<string>> GetSuggestionsAsync(string query, CancellationToken ct = default)
    {
        var products = await productClient.SearchProductsAsync(query, ct: ct);
        return products.Take(5).Select(p => p.Name).ToList();
    }
}
