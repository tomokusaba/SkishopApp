using CouponService.Infrastructure.Persistence;
using CouponService.Models;
using CouponService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CouponService.Repositories;

public class CampaignRepository(AppDbContext context) : ICampaignRepository
{
    public async Task<Campaign?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.Campaigns.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<List<Campaign>> GetAllAsync(int page, int pageSize, CancellationToken ct = default)
        => await context.Campaigns
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    public async Task<int> CountAllAsync(CancellationToken ct = default)
        => await context.Campaigns.AsNoTracking().CountAsync(ct);

    public async Task AddAsync(Campaign campaign, CancellationToken ct = default)
        => await context.Campaigns.AddAsync(campaign, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);

    public async Task<List<Campaign>> GetActiveCampaignsAsync(CancellationToken ct = default)
        => await context.Campaigns
            .AsNoTracking()
            .Where(c => c.Status == CampaignStatus.Active)
            .ToListAsync(ct);

    public async Task<List<Campaign>> GetCampaignsToEndAsync(DateTimeOffset now, CancellationToken ct = default)
        => await context.Campaigns
            .Where(c => c.Status == CampaignStatus.Active && c.EndDate <= now)
            .ToListAsync(ct);

    public async Task<List<Campaign>> GetCampaignsToStartAsync(DateTimeOffset now, CancellationToken ct = default)
        => await context.Campaigns
            .Where(c => c.Status == CampaignStatus.Draft && c.StartDate <= now)
            .ToListAsync(ct);

    public async Task<int> CompleteExpiredCampaignsAsync(DateTimeOffset now, CancellationToken ct = default)
        => await context.Campaigns
            .Where(c => c.Status == CampaignStatus.Active && c.EndDate <= now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Status, CampaignStatus.Ended)
                .SetProperty(c => c.UpdatedAt, now), ct);

    public async Task<List<Campaign>> GetByStatusAsync(
        CampaignStatus status, int page = 1, int pageSize = 50, CancellationToken ct = default)
        => await context.Campaigns
            .AsNoTracking()
            .Where(c => c.Status == status)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
}
