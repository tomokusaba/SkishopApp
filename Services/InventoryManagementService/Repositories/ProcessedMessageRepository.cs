using InventoryManagementService.Infrastructure.Persistence;
using InventoryManagementService.Models;
using InventoryManagementService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagementService.Repositories;

/// <summary>
/// ProcessedMessage エンティティのリポジトリ実装クラス。
/// </summary>
public class ProcessedMessageRepository(AppDbContext context) : IProcessedMessageRepository
{
    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string messageId, CancellationToken ct = default)
        => await context.ProcessedMessages.AnyAsync(m => m.MessageId == messageId, ct);

    /// <inheritdoc />
    public Task AddAsync(ProcessedMessage message, CancellationToken ct = default)
    {
        context.ProcessedMessages.Add(message);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
