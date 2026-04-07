using Microsoft.EntityFrameworkCore.Storage;
using PointService.Models;

namespace PointService.Repositories.Interfaces;

public interface IPointAccountRepository
{
    Task<PointAccount?> FindByUserIdAsync(string userId, CancellationToken ct = default);
    Task<PointAccount?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<List<PointAccount>> FindByTotalEarnedRangeAsync(int min, int max, CancellationToken ct = default);
    Task<int> CountByTotalEarnedRangeAsync(int min, int max, CancellationToken ct = default);
    Task<int> CountAllAsync(CancellationToken ct = default);
    Task AddAsync(PointAccount account, CancellationToken ct = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
    Task AnonymizeAccountAsync(string userId, string anonymizedId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
