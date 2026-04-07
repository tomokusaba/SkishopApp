using Frontend.DTOs;

namespace Frontend.Services.Interfaces;

/// <summary>
/// ポイント API クライアントインターフェース
/// </summary>
public interface IPointApiClient
{
    Task<PointBalanceDto?> GetBalanceAsync(CancellationToken ct = default);
    Task<PointHistoryResult?> GetHistoryAsync(int page = 0, int size = 10, CancellationToken ct = default);
    Task<TierInfoDto?> GetTierInfoAsync(CancellationToken ct = default);
    Task<ExpiringPointsDto?> GetExpiringPointsAsync(CancellationToken ct = default);
}
