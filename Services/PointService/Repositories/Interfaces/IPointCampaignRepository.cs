using PointService.Models;

namespace PointService.Repositories.Interfaces;

public interface IPointCampaignRepository
{
    Task<List<PointCampaign>> FindActiveCampaignsAsync(DateTime asOf, CancellationToken ct = default);
    Task<PointCampaign?> FindByIdAsync(string id, CancellationToken ct = default);
    Task AddAsync(PointCampaign campaign, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
