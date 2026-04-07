using PointService.Models;
using PointService.Repositories.Interfaces;
using PointService.Services.Interfaces;

namespace PointService.Services;

public class PointCampaignService(
    IPointCampaignRepository campaignRepository,
    TimeProvider timeProvider,
    ILogger<PointCampaignService> logger) : IPointCampaignService
{
    public async Task<List<PointCampaign>> GetActiveCampaignsAsync(CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return await campaignRepository.FindActiveCampaignsAsync(now, ct);
    }

    public async Task<PointCampaign?> GetByIdAsync(string id, CancellationToken ct = default)
        => await campaignRepository.FindByIdAsync(id, ct);

    public async Task<decimal> GetMaxMultiplierAsync(
        string? productCategory = null, CancellationToken ct = default)
    {
        var activeCampaigns = await GetActiveCampaignsAsync(ct);

        if (activeCampaigns.Count == 0)
        {
            logger.LogDebug("アクティブなキャンペーンなし。デフォルト倍率 1.0 を使用");
            return 1.0m;
        }

        var maxMultiplier = activeCampaigns.Max(c => c.Multiplier);

        logger.LogDebug(
            "キャンペーン最大倍率: Multiplier={Multiplier}, ActiveCount={Count}",
            maxMultiplier, activeCampaigns.Count);

        return maxMultiplier;
    }
}
