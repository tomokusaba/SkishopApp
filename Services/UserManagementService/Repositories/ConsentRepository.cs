using Microsoft.EntityFrameworkCore;
using UserManagementService.Infrastructure.Persistence;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;

namespace UserManagementService.Repositories;

/// <summary>
/// 同意管理の EF Core Repository 実装。ConsentType 昇順でソートして返却する。
/// </summary>
public class ConsentRepository(AppDbContext context) : IConsentRepository
{
    public async Task<List<Consent>> FindByUserIdAsync(string userId, CancellationToken ct = default)
        => await context.Consents
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.ConsentType)
            .ToListAsync(ct);

    public async Task<Consent?> FindByUserIdAndTypeAsync(string userId, string consentType, CancellationToken ct = default)
        => await context.Consents
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ConsentType == consentType, ct);

    public async Task AddAsync(Consent consent, CancellationToken ct = default)
        => await context.Consents.AddAsync(consent, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
