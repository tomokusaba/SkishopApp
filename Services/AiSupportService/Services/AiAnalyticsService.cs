using AiSupportService.DTOs.Responses;
using AiSupportService.Repositories.Interfaces;
using AiSupportService.Services.Interfaces;

namespace AiSupportService.Services;

/// <summary>
/// <see cref="IAiAnalyticsService"/> の実装。検索・レコメンデーション・チャットの利用統計を集計する。
/// </summary>
/// <remarks>
/// <para>
/// このサービスは、AI サポート機能の利用状況を分析するためのメトリクスを提供します。
/// 各種リポジトリから集計データを取得し、分析レスポンスに変換します。
/// </para>
/// <para>
/// 依存サービス:
/// <list type="bullet">
///   <item><description><see cref="ISearchAnalyticsRepository"/>: 検索分析データへのアクセス</description></item>
///   <item><description><see cref="IRecommendationRepository"/>: レコメンデーションデータへのアクセス</description></item>
///   <item><description><see cref="IChatSessionRepository"/>: チャットセッションデータへのアクセス</description></item>
///   <item><description><see cref="IChatMessageRepository"/>: チャットメッセージデータへのアクセス</description></item>
/// </list>
/// </para>
/// </remarks>
/// <param name="searchAnalyticsRepository">検索分析リポジトリ。</param>
/// <param name="recommendationRepository">レコメンデーションリポジトリ。</param>
/// <param name="chatSessionRepository">チャットセッションリポジトリ。</param>
/// <param name="chatMessageRepository">チャットメッセージリポジトリ。</param>
/// <param name="logger">ロガー。</param>
public class AiAnalyticsService(
    ISearchAnalyticsRepository searchAnalyticsRepository,
    IRecommendationRepository recommendationRepository,
    IChatSessionRepository chatSessionRepository,
    IChatMessageRepository chatMessageRepository,
    ILogger<AiAnalyticsService> logger) : IAiAnalyticsService
{
    /// <inheritdoc />
    /// <remarks>
    /// 検索分析データには以下の情報が含まれます:
    /// <list type="bullet">
    ///   <item><description>総検索数</description></item>
    ///   <item><description>ユニークユーザー数</description></item>
    ///   <item><description>平均レスポンス時間（ミリ秒）</description></item>
    ///   <item><description>人気クエリ上位 10 件とその件数</description></item>
    ///   <item><description>検索タイプ（KEYWORD、AI_ASSISTED 等）の分布</description></item>
    /// </list>
    /// </remarks>
    public async Task<SearchAnalyticsResponse> GetSearchAnalyticsAsync(
        DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        var totalSearches = await searchAnalyticsRepository.CountByDateRangeAsync(fromDate, toDate, ct);
        var uniqueUsers = await searchAnalyticsRepository.CountDistinctUsersByDateRangeAsync(fromDate, toDate, ct);
        var avgResponseTime = await searchAnalyticsRepository.AverageResponseTimeByDateRangeAsync(fromDate, toDate, ct);
        var topQueryTuples = await searchAnalyticsRepository.GetTopQueriesByDateRangeAsync(fromDate, toDate, 10, ct);
        var typeDistribution = await searchAnalyticsRepository.GetSearchTypeDistributionByDateRangeAsync(fromDate, toDate, ct);

        var topQueries = topQueryTuples.Select(t => new TopSearchQuery(t.Query, t.Count)).ToList();

        logger.LogInformation("検索分析取得: From={From}, To={To}, Total={Total}", fromDate, toDate, totalSearches);

        return new SearchAnalyticsResponse(totalSearches, uniqueUsers, avgResponseTime, topQueries, typeDistribution);
    }

    /// <inheritdoc />
    /// <remarks>
    /// レコメンデーション分析データには以下の情報が含まれます:
    /// <list type="bullet">
    ///   <item><description>生成総数</description></item>
    ///   <item><description>閲覧数（ユーザーがクリックまたは閲覧したレコメンデーション数）</description></item>
    ///   <item><description>閲覧率（閲覧数 / 生成総数）</description></item>
    ///   <item><description>レコメンデーションタイプ（PERSONALIZED、SIMILAR 等）の分布</description></item>
    /// </list>
    /// </remarks>
    public async Task<RecommendationAnalyticsResponse> GetRecommendationAnalyticsAsync(
        DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        var total = await recommendationRepository.CountByDateRangeAsync(fromDate, toDate, ct);
        var viewed = await recommendationRepository.CountViewedByDateRangeAsync(fromDate, toDate, ct);
        var viewRate = total > 0 ? (double)viewed / total : 0;
        var typeDistribution = await recommendationRepository.GetTypeDistributionByDateRangeAsync(fromDate, toDate, ct);

        logger.LogInformation("レコメンデーション分析取得: From={From}, To={To}, Total={Total}", fromDate, toDate, total);

        return new RecommendationAnalyticsResponse(total, viewed, viewRate, typeDistribution);
    }

    /// <inheritdoc />
    /// <remarks>
    /// チャット分析データには以下の情報が含まれます:
    /// <list type="bullet">
    ///   <item><description>セッション総数</description></item>
    ///   <item><description>メッセージ総数</description></item>
    ///   <item><description>セッションあたりの平均メッセージ数</description></item>
    ///   <item><description>エスカレーション数（有人対応に切り替えられたセッション数）</description></item>
    ///   <item><description>セッションステータス（ACTIVE、CLOSED、ESCALATED）の分布</description></item>
    /// </list>
    /// </remarks>
    public async Task<ChatAnalyticsResponse> GetChatAnalyticsAsync(
        DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        var totalSessions = await chatSessionRepository.CountByDateRangeAsync(fromDate, toDate, ct);
        var totalMessages = await chatMessageRepository.CountByDateRangeAsync(fromDate, toDate, ct);
        var avgMessages = totalSessions > 0 ? (double)totalMessages / totalSessions : 0;
        var escalated = await chatSessionRepository.CountByDateRangeAndStatusAsync(fromDate, toDate, "ESCALATED", ct);
        var statusDistribution = await chatSessionRepository.GetStatusDistributionByDateRangeAsync(fromDate, toDate, ct);

        logger.LogInformation("チャット分析取得: From={From}, To={To}, Sessions={Sessions}", fromDate, toDate, totalSessions);

        return new ChatAnalyticsResponse(totalSessions, totalMessages, avgMessages, escalated, statusDistribution);
    }
}
