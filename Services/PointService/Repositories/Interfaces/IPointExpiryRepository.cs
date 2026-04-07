using PointService.Models;

namespace PointService.Repositories.Interfaces;

public interface IPointExpiryRepository
{
    Task<List<PointExpiry>> FindExpiredAsync(DateTime asOf, int batchSize, CancellationToken ct = default);
    Task<List<PointExpiry>> FindActiveByAccountIdAsync(string accountId, CancellationToken ct = default);
    Task<List<PointExpiry>> FindActiveByUserIdAsync(string userId, CancellationToken ct = default);
    Task<List<PointExpiry>> FindConsumedByAccountIdAsync(string accountId, CancellationToken ct = default);
    Task<List<PointExpiry>> FindAllByUserIdAsync(string userId, CancellationToken ct = default);
    Task<List<PointExpiry>> FindExpiringWithinAsync(string userId, int days, CancellationToken ct = default);
    Task AnonymizeExpiriesAsync(string userId, string anonymizedId, CancellationToken ct = default);
    Task AddAsync(PointExpiry expiry, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
