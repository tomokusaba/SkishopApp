using System.Text.Json;
using MailSendService.DTOs.Responses;
using MailSendService.Models;
using MailSendService.Repositories.Interfaces;
using MailSendService.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace MailSendService.Services;

/// <summary>
/// メール送信統計情報を提供するサービス実装。
/// </summary>
/// <remarks>
/// <para>P2-2: MailService からの責務分割。</para>
/// <para>P2-3: Redis キャッシュによる統計クエリ最適化を実装。</para>
/// </remarks>
public class MailStatsService(
    IMailLogRepository mailLogRepository,
    IDistributedCache cache,
    ILogger<MailStatsService> logger) : IMailStatsService
{
    /// <summary>統計情報キャッシュのキー。</summary>
    private const string StatsCacheKey = "mail:stats";

    /// <summary>統計情報キャッシュの有効期限（5 分）。</summary>
    private static readonly TimeSpan StatsCacheTtl = TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    /// <remarks>
    /// P1-5: EF Core DbContext はスレッドセーフではないため、順次実行する。
    /// P2-3: Redis キャッシュを使用して DB 負荷を軽減する。
    /// </remarks>
    public async Task<MailStatsResponse> GetStatsAsync(CancellationToken ct = default)
    {
        // キャッシュからの取得を試行
        var cached = await cache.GetStringAsync(StatsCacheKey, ct);
        if (cached is not null)
        {
            logger.LogDebug("Statistics loaded from cache");
            return JsonSerializer.Deserialize<MailStatsResponse>(cached)!;
        }

        // P1-5: DbContext はスレッドセーフではないため、順次実行
        var sent = await mailLogRepository.CountByStatusAsync(MailLogStatus.Sent, ct);
        var failed = await mailLogRepository.CountByStatusAsync(MailLogStatus.Failed, ct);
        var pending = await mailLogRepository.CountByStatusAsync(MailLogStatus.Pending, ct);
        var sentByTemplate = await mailLogRepository.CountByTemplateAsync(ct);

        var total = sent + failed + pending;
        var successRate = total > 0 ? (double)sent / total * 100 : 0;
        var result = new MailStatsResponse(sent, failed, pending, successRate, sentByTemplate);

        // キャッシュに保存
        await cache.SetStringAsync(
            StatsCacheKey,
            JsonSerializer.Serialize(result),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = StatsCacheTtl },
            ct);

        logger.LogDebug("Statistics calculated and cached: Sent={Sent}, Failed={Failed}, Pending={Pending}",
            sent, failed, pending);

        return result;
    }

    /// <inheritdoc />
    public async Task InvalidateStatsCacheAsync(CancellationToken ct = default)
    {
        await cache.RemoveAsync(StatsCacheKey, ct);
        logger.LogDebug("Statistics cache invalidated");
    }
}
