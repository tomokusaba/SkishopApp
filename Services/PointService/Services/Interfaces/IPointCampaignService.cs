namespace PointService.Services.Interfaces;

/// <summary>ポイントキャンペーン管理サービス。</summary>
public interface IPointCampaignService
{
    Task<List<Models.PointCampaign>> GetActiveCampaignsAsync(CancellationToken ct = default);
    Task<Models.PointCampaign?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<decimal> GetMaxMultiplierAsync(
        string? productCategory = null, CancellationToken ct = default);
}
