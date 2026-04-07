using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.Models;

namespace InventoryManagementService.Repositories.Interfaces;

/// <summary>
/// 商品リポジトリのインターフェース。Product Aggregate Root のデータアクセスを提供する。
/// </summary>
public interface IProductRepository
{
    /// <summary>
    /// 商品 ID で商品を取得する。Category、Images、アクティブな Prices、Inventories を Include する。
    /// </summary>
    /// <param name="id">商品 ID</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>商品エンティティ。存在しない場合は null</returns>
    Task<Product?> FindByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// SKU コードで商品を取得する（読み取り専用、AsNoTracking）。
    /// </summary>
    /// <param name="sku">SKU コード</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>商品エンティティ。存在しない場合は null</returns>
    Task<Product?> FindBySkuAsync(string sku, CancellationToken ct = default);

    /// <summary>
    /// 複数条件（キーワード、カテゴリ、ブランド）で商品を検索する。ページネーション・作成日降順。
    /// </summary>
    /// <param name="criteria">検索条件</param>
    /// <param name="page">ページ番号（0 始まり）</param>
    /// <param name="size">ページサイズ</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>条件に一致する商品リスト</returns>
    Task<List<Product>> SearchAsync(ProductSearchCriteria criteria, int page, int size, CancellationToken ct = default);

    /// <summary>
    /// 検索条件に一致する商品の総件数を取得する。
    /// </summary>
    /// <param name="criteria">検索条件</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>総件数</returns>
    Task<long> CountAsync(ProductSearchCriteria criteria, CancellationToken ct = default);

    /// <summary>
    /// カテゴリ ID でアクティブな商品を取得する。ページネーション・作成日降順。
    /// </summary>
    /// <param name="categoryId">カテゴリ ID</param>
    /// <param name="page">ページ番号（0 始まり）</param>
    /// <param name="size">ページサイズ</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>商品リスト</returns>
    Task<List<Product>> FindByCategoryIdAsync(string categoryId, int page, int size, CancellationToken ct = default);

    /// <summary>
    /// カテゴリ ID に属するアクティブな商品の総件数を取得する。
    /// </summary>
    /// <param name="categoryId">カテゴリ ID</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>総件数</returns>
    Task<long> CountByCategoryIdAsync(string categoryId, CancellationToken ct = default);

    /// <summary>
    /// 複数の商品 ID でアクティブな商品を一括取得する。
    /// </summary>
    /// <param name="ids">商品 ID のリスト</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>商品リスト</returns>
    Task<List<Product>> FindByIdsAsync(List<string> ids, CancellationToken ct = default);

    /// <summary>
    /// 商品エンティティを追加する。
    /// </summary>
    /// <param name="product">追加する商品</param>
    /// <param name="ct">キャンセルトークン</param>
    Task AddAsync(Product product, CancellationToken ct = default);

    /// <summary>
    /// 変更をデータベースに永続化する。
    /// </summary>
    /// <param name="ct">キャンセルトークン</param>
    Task SaveChangesAsync(CancellationToken ct = default);
}
