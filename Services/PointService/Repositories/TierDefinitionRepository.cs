using Microsoft.EntityFrameworkCore;
using PointService.Infrastructure.Persistence;
using PointService.Models;
using PointService.Repositories.Interfaces;

namespace PointService.Repositories;

public class TierDefinitionRepository(AppDbContext context) : ITierDefinitionRepository
{
    public async Task<List<TierDefinition>> FindAllAsync(CancellationToken ct = default)
        => await context.TierDefinitions
            .AsNoTracking()
            .OrderBy(t => t.SortOrder)
            .ToListAsync(ct);

    public async Task<TierDefinition?> FindByIdAsync(
        string id, CancellationToken ct = default)
        => await context.TierDefinitions
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<TierDefinition?> FindByNameAsync(
        string name, CancellationToken ct = default)
        => await context.TierDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Name == name, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
