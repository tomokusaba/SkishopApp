using Microsoft.EntityFrameworkCore;
using UserManagementService.Infrastructure.Persistence;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;

namespace UserManagementService.Repositories;

/// <summary>
/// GDPR 削除リクエストの EF Core Repository 実装。猶予期間切れ・タイムアウト検出用のクエリを提供する。
/// </summary>
public class DeletionRequestRepository(AppDbContext context, TimeProvider timeProvider) : IDeletionRequestRepository
{
    public async Task<DeletionRequest?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.DeletionRequests.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<DeletionRequest?> FindPendingByUserIdAsync(string userId, CancellationToken ct = default)
        => await context.DeletionRequests
            .FirstOrDefaultAsync(d => d.UserId == userId
                && d.Status == DeletionRequestStatus.Pending, ct);

    public async Task<List<DeletionRequest>> FindExpiredGracePeriodAsync(CancellationToken ct = default)
        => await context.DeletionRequests
            .Where(d => d.Status == DeletionRequestStatus.Pending
                && d.GracePeriodEndsAt <= timeProvider.GetUtcNow())
            .Take(100)
            .ToListAsync(ct);

    public async Task<List<DeletionRequest>> FindTimedOutProcessingAsync(TimeSpan timeout, CancellationToken ct = default)
        => await context.DeletionRequests
            .Where(d => d.Status == DeletionRequestStatus.Processing
                && d.RequestedAt.Add(timeout) <= timeProvider.GetUtcNow())
            .Take(100)
            .ToListAsync(ct);

    public async Task<List<DeletionRequest>> FindByStatusAsync(string status, CancellationToken ct = default)
        => await context.DeletionRequests
            .Where(d => d.Status == status)
            .Take(100)
            .ToListAsync(ct);

    public async Task AddAsync(DeletionRequest request, CancellationToken ct = default)
        => await context.DeletionRequests.AddAsync(request, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
