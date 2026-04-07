using Microsoft.EntityFrameworkCore;
using PointService.Infrastructure.Persistence;
using PointService.Models;
using PointService.Repositories.Interfaces;

namespace PointService.Repositories;

public class PointRuleRepository(AppDbContext context) : IPointRuleRepository
{
    public async Task<List<PointRule>> FindActiveRulesAsync(CancellationToken ct = default)
        => await context.PointRules
            .AsNoTracking()
            .Where(r => r.IsActive)
            .ToListAsync(ct);

    public async Task<PointRule?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.PointRules.FindAsync([id], ct);

    public async Task AddAsync(PointRule rule, CancellationToken ct = default)
        => await context.PointRules.AddAsync(rule, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
