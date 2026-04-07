using Microsoft.EntityFrameworkCore;
using UserManagementService.Infrastructure.Persistence;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;

namespace UserManagementService.Repositories;

/// <summary>
/// 処理済みイベントの EF Core Repository 実装。Kafka イベントのべき等性保証に使用する。
/// </summary>
public class ProcessedEventRepository(AppDbContext context) : IProcessedEventRepository
{
    public async Task<bool> ExistsAsync(string eventId, string eventType, CancellationToken ct = default)
        => await context.ProcessedEvents
            .AnyAsync(e => e.EventId == eventId && e.EventType == eventType, ct);

    public async Task AddAsync(ProcessedEvent processedEvent, CancellationToken ct = default)
        => await context.ProcessedEvents.AddAsync(processedEvent, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
