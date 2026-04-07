using Frontend.DTOs;
using Frontend.Services.Interfaces;

namespace Frontend.Services;

public class PointApiClient(IApiGatewayClient apiClient, ILogger<PointApiClient> logger) : IPointApiClient
{
    public async Task<PointBalanceDto?> GetBalanceAsync(CancellationToken ct = default)
    {
        logger.LogInformation("ポイント残高取得");
        return await apiClient.GetAsync<PointBalanceDto>("/api/v1/points/balance", ct);
    }

    public async Task<PointHistoryResult?> GetHistoryAsync(int page = 0, int size = 10, CancellationToken ct = default)
    {
        logger.LogInformation("ポイント履歴取得: Page={Page}, Size={Size}", page, size);
        return await apiClient.GetAsync<PointHistoryResult>(
            $"/api/v1/points/history?page={page}&size={size}", ct);
    }

    public async Task<TierInfoDto?> GetTierInfoAsync(CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("ティア情報取得");
            return await apiClient.GetAsync<TierInfoDto>("/api/v1/points/tier", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ティア情報の取得に失敗しました（グレースフルデグラデーション）");
            return null;
        }
    }

    public async Task<ExpiringPointsDto?> GetExpiringPointsAsync(CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation("失効予定ポイント取得");
            return await apiClient.GetAsync<ExpiringPointsDto>("/api/v1/points/expiring", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "失効予定ポイントの取得に失敗しました");
            return null;
        }
    }
}

