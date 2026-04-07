using InventoryManagementService.Models;

namespace InventoryManagementService.Repositories.Interfaces;

/// <summary>
/// カテゴリリポジトリのインターフェース。Category Aggregate Root のデータアクセスを提供する。
/// 階層構造（親子関係）の検証クエリを含む。
/// </summary>
public interface ICategoryRepository
{
    /// <summary>
    /// カテゴリ ID でカテゴリを取得する。子カテゴリを Include する。
    /// </summary>
    Task<Category?> FindByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// アクティブな全カテゴリを階層レベル順・名前順で取得する（読み取り専用）。
    /// </summary>
    Task<List<Category>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// アクティブなカテゴリの総件数を取得する。
    /// </summary>
    Task<long> CountAsync(CancellationToken ct = default);

    /// <summary>
    /// 指定 ID のアクティブなカテゴリが存在するかを確認する。
    /// </summary>
    Task<bool> ExistsByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// 指定カテゴリにアクティブな子カテゴリが存在するかを確認する。削除前の検証用。
    /// </summary>
    Task<bool> HasChildrenAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// 指定カテゴリにアクティブな商品が紐づいているかを確認する。削除前の検証用。
    /// </summary>
    Task<bool> HasProductsAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// カテゴリエンティティを追加する。
    /// </summary>
    Task AddAsync(Category category, CancellationToken ct = default);

    /// <summary>
    /// カテゴリエンティティを物理削除する。
    /// </summary>
    Task RemoveAsync(Category category, CancellationToken ct = default);

    /// <summary>
    /// 変更をデータベースに永続化する。
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
