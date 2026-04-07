using Microsoft.EntityFrameworkCore;
using SalesManagementService.Infrastructure.Persistence;
using SalesManagementService.Models;
using SalesManagementService.Repositories.Interfaces;

namespace SalesManagementService.Repositories;

public class IdempotencyKeyRepository(SalesDbContext context) : IIdempotencyKeyRepository
{
    public async Task<IdempotencyKey?> FindByKeyAndUserIdAsync(
        string key, string userId, CancellationToken ct = default)
        => await context.IdempotencyKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Key == key && k.UserId == userId, ct);

    public async Task AddAsync(IdempotencyKey idempotencyKey, CancellationToken ct = default)
        => await context.IdempotencyKeys.AddAsync(idempotencyKey, ct);

    public async Task DeleteExpiredAsync(DateTimeOffset now, CancellationToken ct = default)
        => await context.IdempotencyKeys
            .Where(k => k.ExpiresAt < now)
            .ExecuteDeleteAsync(ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
