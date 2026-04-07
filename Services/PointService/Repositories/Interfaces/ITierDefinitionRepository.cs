using PointService.Models;

namespace PointService.Repositories.Interfaces;

public interface ITierDefinitionRepository
{
    Task<List<TierDefinition>> FindAllAsync(CancellationToken ct = default);
    Task<TierDefinition?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<TierDefinition?> FindByNameAsync(string name, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
