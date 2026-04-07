using InventoryManagementService.Infrastructure.Persistence;
using InventoryManagementService.Models;
using InventoryManagementService.Repositories.Interfaces;

namespace InventoryManagementService.Repositories;

/// <summary>
/// OutboxEvent エンティティのリポジトリ実装クラス。
/// </summary>
public class OutboxRepository(AppDbContext context) : IOutboxRepository
{
    /// <inheritdoc />
    public async Task AddAsync(OutboxEvent outboxEvent, CancellationToken ct = default)
        => await context.OutboxEvents.AddAsync(outboxEvent, ct);

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
