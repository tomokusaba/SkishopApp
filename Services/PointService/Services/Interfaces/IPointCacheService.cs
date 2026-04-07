using PointService.DTOs.Responses;

namespace PointService.Services.Interfaces;

public interface IPointCacheService
{
    Task<PointBalanceResponse?> GetBalanceCacheAsync(string userId, CancellationToken ct = default);
    Task SetBalanceCacheAsync(string userId, PointBalanceResponse balance, CancellationToken ct = default);
    Task InvalidateBalanceCacheAsync(string userId, CancellationToken ct = default);
    Task<string?> GetUserTierAsync(string userId, CancellationToken ct = default);
    Task SetUserTierAsync(string userId, string tier, decimal pointRate, CancellationToken ct = default);
}
