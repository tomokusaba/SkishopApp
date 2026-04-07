using InventoryManagementService.Infrastructure.Persistence;
using InventoryManagementService.Models;
using InventoryManagementService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagementService.Repositories;

/// <summary>
/// カテゴリリポジトリの EF Core 実装クラス。
/// </summary>
/// <remarks>
/// <para>階層検証: HasChildrenAsync / HasProductsAsync で削除前の依存関係を検証する。</para>
/// <para>Eager Loading: FindByIdAsync では子カテゴリ（Children）を Include する。</para>
/// </remarks>
public class CategoryRepository(AppDbContext context) : ICategoryRepository
{
    /// <inheritdoc />
    public async Task<Category?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.Categories
            .Include(c => c.Children)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <inheritdoc />
    public async Task<List<Category>> GetAllAsync(CancellationToken ct = default)
        => await context.Categories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Level)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<long> CountAsync(CancellationToken ct = default)
        => await context.Categories.LongCountAsync(c => c.IsActive, ct);

    /// <inheritdoc />
    public async Task<bool> ExistsByIdAsync(string id, CancellationToken ct = default)
        => await context.Categories.AnyAsync(c => c.Id == id && c.IsActive, ct);

    /// <inheritdoc />
    public async Task<bool> HasChildrenAsync(string id, CancellationToken ct = default)
        => await context.Categories.AnyAsync(c => c.ParentId == id && c.IsActive, ct);

    /// <inheritdoc />
    public async Task<bool> HasProductsAsync(string id, CancellationToken ct = default)
        => await context.Products.AnyAsync(p => p.CategoryId == id && p.IsActive, ct);

    /// <inheritdoc />
    public async Task AddAsync(Category category, CancellationToken ct = default)
        => await context.Categories.AddAsync(category, ct);

    /// <inheritdoc />
    public Task RemoveAsync(Category category, CancellationToken ct = default)
    {
        context.Categories.Remove(category);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
