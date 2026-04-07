using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.Infrastructure.Persistence;
using InventoryManagementService.Models;
using InventoryManagementService.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagementService.Repositories;

/// <summary>
/// 商品リポジトリの EF Core 実装クラス。
/// </summary>
/// <remarks>
/// <para>Eager Loading: FindByIdAsync では Category、Images（SortOrder 順）、アクティブ Prices、Inventories を Include する。</para>
/// <para>検索: キーワード（Name/Description）、カテゴリ、ブランドによる複合条件検索をサポートする。</para>
/// <para>読み取り最適化: 検索・一覧系クエリは AsNoTracking で変更追跡オーバーヘッドを排除する。</para>
/// </remarks>
public class ProductRepository(AppDbContext context) : IProductRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// Eager Loading 戦略: Category、SortOrder 順の Images、アクティブな Prices（IsActive フィルタ）、
    /// Inventories を Include する。変更追跡を有効にして返すため、更新操作に使用可能。
    /// </remarks>
    public async Task<Product?> FindByIdAsync(string id, CancellationToken ct = default)
        => await context.Products
            .Include(p => p.Category)
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .Include(p => p.Prices.Where(pr => pr.IsActive))
            .Include(p => p.Inventories)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    /// <inheritdoc />
    public async Task<Product?> FindBySkuAsync(string sku, CancellationToken ct = default)
        => await context.Products.AsNoTracking()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Sku == sku, ct);

    /// <inheritdoc />
    /// <remarks>
    /// BuildSearchQuery で動的にフィルタ条件を構築し、作成日降順でページネーションを適用する。
    /// Category を Include し、AsNoTracking で読み取り最適化を行う。
    /// </remarks>
    public async Task<List<Product>> SearchAsync(
        ProductSearchCriteria criteria, int page, int size, CancellationToken ct = default)
    {
        var query = BuildSearchQuery(criteria);
        return await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip(page * size).Take(size)
            .Include(p => p.Category)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<long> CountAsync(ProductSearchCriteria criteria, CancellationToken ct = default)
        => await BuildSearchQuery(criteria).LongCountAsync(ct);

    /// <inheritdoc />
    public async Task<List<Product>> FindByCategoryIdAsync(
        string categoryId, int page, int size, CancellationToken ct = default)
        => await context.Products.AsNoTracking()
            .Where(p => p.CategoryId == categoryId && p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .Skip(page * size).Take(size)
            .Include(p => p.Category)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<long> CountByCategoryIdAsync(string categoryId, CancellationToken ct = default)
        => await context.Products.LongCountAsync(p => p.CategoryId == categoryId && p.IsActive, ct);

    /// <inheritdoc />
    public async Task<List<Product>> FindByIdsAsync(List<string> ids, CancellationToken ct = default)
        => await context.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.IsActive)
            .Include(p => p.Category)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task AddAsync(Product product, CancellationToken ct = default)
        => await context.Products.AddAsync(product, ct);

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);

    /// <summary>
    /// 検索条件からクエリを構築する。アクティブな商品のみを対象とし、キーワード・カテゴリ・ブランドでフィルタリングする。
    /// </summary>
    /// <param name="criteria">検索条件（Keyword, CategoryId, Brand, CategoryName）</param>
    /// <returns>フィルタ適用済みの <see cref="IQueryable{Product}"/></returns>
    /// <remarks>
    /// <para>キーワード: Name または Description に対する部分一致（LIKE）検索を行う。</para>
    /// <para>カテゴリ: CategoryId での完全一致フィルタ。CategoryId 未指定時は CategoryName での完全一致。</para>
    /// <para>ブランド: Brand での完全一致フィルタ。</para>
    /// <para>全条件は AND 結合で適用される。</para>
    /// </remarks>
    private IQueryable<Product> BuildSearchQuery(ProductSearchCriteria criteria)
    {
        var query = context.Products.AsNoTracking().Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
            query = query.Where(p => p.Name.Contains(criteria.Keyword)
                || (p.Description != null && p.Description.Contains(criteria.Keyword))
                || (p.Category != null && p.Category.Name.Contains(criteria.Keyword)));

        if (!string.IsNullOrWhiteSpace(criteria.CategoryId))
            query = query.Where(p => p.CategoryId == criteria.CategoryId);
        else if (!string.IsNullOrWhiteSpace(criteria.CategoryName))
            query = query.Where(p => p.Category != null && p.Category.Name == criteria.CategoryName);

        if (!string.IsNullOrWhiteSpace(criteria.Brand))
            query = query.Where(p => p.Brand == criteria.Brand);

        return query;
    }
}
