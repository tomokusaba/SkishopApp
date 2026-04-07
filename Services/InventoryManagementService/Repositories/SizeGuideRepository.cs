using InventoryManagementService.Infrastructure.Persistence;
using InventoryManagementService.Models;
using InventoryManagementService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagementService.Repositories;

/// <summary>
/// サイズガイドリポジトリの EF Core 実装クラス。
/// </summary>
/// <remarks>
/// カテゴリ別のサイズチャートデータの CRUD を提供する。
/// カテゴリ別参照は AsNoTracking で最適化する。
/// </remarks>
public class SizeGuideRepository(AppDbContext context) : ISizeGuideRepository
{
    /// <inheritdoc />
    public async Task<SizeGuide?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.SizeGuides
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    /// <inheritdoc />
    public async Task<SizeGuide?> FindByCategoryIdAsync(string categoryId, CancellationToken ct = default)
        => await context.SizeGuides.AsNoTracking()
            .FirstOrDefaultAsync(s => s.CategoryId == categoryId, ct);

    /// <inheritdoc />
    public async Task AddAsync(SizeGuide sizeGuide, CancellationToken ct = default)
        => await context.SizeGuides.AddAsync(sizeGuide, ct);

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
