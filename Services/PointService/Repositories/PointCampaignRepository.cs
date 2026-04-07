using Microsoft.EntityFrameworkCore;
using PointService.Infrastructure.Persistence;
using PointService.Models;
using PointService.Repositories.Interfaces;

namespace PointService.Repositories;

public class PointCampaignRepository(AppDbContext context) : IPointCampaignRepository
{
    public async Task<List<PointCampaign>> FindActiveCampaignsAsync(
        DateTime asOf, CancellationToken ct = default)
        => await context.PointCampaigns
            .AsNoTracking()
            .Where(c => c.IsActive && c.StartDate <= asOf && c.EndDate >= asOf)
            .ToListAsync(ct);

    public async Task<PointCampaign?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.PointCampaigns.FindAsync([id], ct);

    public async Task AddAsync(PointCampaign campaign, CancellationToken ct = default)
        => await context.PointCampaigns.AddAsync(campaign, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
