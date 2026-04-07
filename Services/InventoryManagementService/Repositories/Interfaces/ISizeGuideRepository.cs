using InventoryManagementService.Models;

namespace InventoryManagementService.Repositories.Interfaces;

/// <summary>
/// サイズガイドリポジトリのインターフェース。SizeGuide エンティティのデータアクセスを提供する。
/// </summary>
public interface ISizeGuideRepository
{
    /// <summary>
    /// サイズガイド ID でサイズガイドを取得する。
    /// </summary>
    Task<SizeGuide?> FindByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// カテゴリ ID でサイズガイドを取得する（読み取り専用、AsNoTracking）。
    /// </summary>
    Task<SizeGuide?> FindByCategoryIdAsync(string categoryId, CancellationToken ct = default);

    /// <summary>
    /// サイズガイドエンティティを追加する。
    /// </summary>
    Task AddAsync(SizeGuide sizeGuide, CancellationToken ct = default);

    /// <summary>
    /// 変更をデータベースに永続化する。
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
