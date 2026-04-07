using PointService.Models;

namespace PointService.Repositories.Interfaces;

public interface IPointRuleRepository
{
    Task<List<PointRule>> FindActiveRulesAsync(CancellationToken ct = default);
    Task<PointRule?> FindByIdAsync(string id, CancellationToken ct = default);
    Task AddAsync(PointRule rule, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
