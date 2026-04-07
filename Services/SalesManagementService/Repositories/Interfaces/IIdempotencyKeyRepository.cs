using SalesManagementService.Models;

namespace SalesManagementService.Repositories.Interfaces;

public interface IIdempotencyKeyRepository
{
    Task<IdempotencyKey?> FindByKeyAndUserIdAsync(string key, string userId, CancellationToken ct = default);
    Task AddAsync(IdempotencyKey idempotencyKey, CancellationToken ct = default);
    Task DeleteExpiredAsync(DateTimeOffset now, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
