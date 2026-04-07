using PointService.Models;

namespace PointService.Repositories.Interfaces;

public interface IPointTransactionRepository
{
    Task<PointTransaction?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<List<PointTransaction>> FindByAccountIdAsync(string accountId, CancellationToken ct = default);
    Task<List<PointTransaction>> FindByUserIdAsync(string userId, CancellationToken ct = default);
    Task<(List<PointTransaction> Items, int TotalCount)> GetPagedAsync(
        string userId, int page, int pageSize, CancellationToken ct = default);
    Task<PointTransaction?> FindByReferenceAsync(
        string referenceId, string type, CancellationToken ct = default);
    Task<long> SumPointsByTypeAsync(string type, CancellationToken ct = default);
    Task AnonymizeTransactionsAsync(string userId, string anonymizedId, CancellationToken ct = default);
    Task AddAsync(PointTransaction transaction, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
