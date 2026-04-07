using PointService.Models;

namespace PointService.Repositories.Interfaces;

public interface IPointAuditLogRepository
{
    Task AddAsync(PointAuditLog log, CancellationToken ct = default);
    Task<List<PointAuditLog>> FindByTargetUserIdAsync(
        string targetUserId, int page, int pageSize, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
