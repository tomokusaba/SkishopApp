using System.Text.Json;
using SalesManagementService.DTOs.Responses;
using StackExchange.Redis;

namespace SalesManagementService.Infrastructure.Caching;

public class OrderCacheService(
    IConnectionMultiplexer redis,
    ILogger<OrderCacheService> logger)
{
    private static readonly TimeSpan OrderCacheTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan ReportCacheTtl = TimeSpan.FromDays(1);
    private static readonly TimeSpan TrackingCacheTtl = TimeSpan.FromMinutes(30);

    private IDatabase Db => redis.GetDatabase();

    public async Task<OrderDetailDto?> GetOrderAsync(string orderId, CancellationToken ct = default)
    {
        var cached = await Db.StringGetAsync($"order:{orderId}");
        if (cached.IsNullOrEmpty) return null;
        logger.LogDebug("キャッシュヒット: order:{OrderId}", orderId);
        return JsonSerializer.Deserialize<OrderDetailDto>(cached.ToString());
    }

    public async Task SetOrderAsync(string orderId, OrderDetailDto order, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(order);
        await Db.StringSetAsync($"order:{orderId}", json, OrderCacheTtl);
        logger.LogDebug("キャッシュ設定: order:{OrderId}, TTL={Ttl}", orderId, OrderCacheTtl);
    }

    public async Task InvalidateOrderAsync(string orderId, string userId, CancellationToken ct = default)
    {
        await Db.KeyDeleteAsync($"order:{orderId}");
        await Db.KeyDeleteAsync($"orders:user:{userId}");
        logger.LogDebug("キャッシュ無効化: order:{OrderId}, orders:user:{UserId}", orderId, userId);
    }

    public async Task<string?> GetDailyReportAsync(string date, CancellationToken ct = default)
    {
        var cached = await Db.StringGetAsync($"report:sales:daily:{date}");
        return cached.IsNullOrEmpty ? null : cached.ToString();
    }

    public async Task SetDailyReportAsync(string date, string reportJson, CancellationToken ct = default)
    {
        await Db.StringSetAsync($"report:sales:daily:{date}", reportJson, ReportCacheTtl);
    }

    public async Task InvalidateDailyReportAsync(string date, CancellationToken ct = default)
    {
        await Db.KeyDeleteAsync($"report:sales:daily:{date}");
    }

    public async Task<string?> GetTrackingAsync(string shipmentId, CancellationToken ct = default)
    {
        var cached = await Db.StringGetAsync($"shipping:tracking:{shipmentId}");
        return cached.IsNullOrEmpty ? null : cached.ToString();
    }

    public async Task SetTrackingAsync(string shipmentId, string trackingJson, CancellationToken ct = default)
    {
        await Db.StringSetAsync($"shipping:tracking:{shipmentId}", trackingJson, TrackingCacheTtl);
    }
}
