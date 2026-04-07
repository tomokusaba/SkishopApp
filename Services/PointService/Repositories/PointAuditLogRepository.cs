using Microsoft.EntityFrameworkCore;
using PointService.Infrastructure.Persistence;
using PointService.Models;
using PointService.Repositories.Interfaces;

namespace PointService.Repositories;

public class PointAuditLogRepository(AppDbContext context) : IPointAuditLogRepository
{
    public async Task AddAsync(PointAuditLog log, CancellationToken ct = default)
        => await context.PointAuditLogs.AddAsync(log, ct);

    public async Task<List<PointAuditLog>> FindByTargetUserIdAsync(
        string targetUserId, int page, int pageSize, CancellationToken ct = default)
        => await context.PointAuditLogs
            .AsNoTracking()
            .Where(a => a.TargetUserId == targetUserId)
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
