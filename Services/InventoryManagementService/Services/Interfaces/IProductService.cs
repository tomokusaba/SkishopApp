using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.DTOs.Responses;

namespace InventoryManagementService.Services.Interfaces;

/// <summary>
/// 商品管理サービスのインターフェース。
/// 商品の CRUD 操作、検索、画像アップロード、一括取得を提供する。
/// </summary>
public interface IProductService
{
    /// <summary>
    /// 新規商品を作成する。SKU の重複チェックおよびカテゴリ存在確認を行う。
    /// </summary>
    /// <param name="request">商品作成リクエスト</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>作成された商品の DTO</returns>
    /// <exception cref="Exceptions.DuplicateResourceException">同一 SKU の商品が既に存在する場合</exception>
    /// <exception cref="Exceptions.ResourceNotFoundException">指定されたカテゴリが存在しない場合</exception>
    Task<ProductDto> CreateProductAsync(ProductCreateRequest request, CancellationToken ct = default);

    /// <summary>
    /// 既存商品の情報を部分更新する。
    /// </summary>
    /// <param name="id">商品 ID</param>
    /// <param name="request">更新リクエスト（null フィールドは更新対象外）</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>更新後の商品 DTO</returns>
    /// <exception cref="Exceptions.ResourceNotFoundException">商品またはカテゴリが存在しない場合</exception>
    Task<ProductDto> UpdateAsync(string id, ProductUpdateRequest request, CancellationToken ct = default);

    /// <summary>
    /// 商品を論理削除する（IsActive を false に設定）。
    /// </summary>
    /// <param name="id">商品 ID</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <exception cref="Exceptions.ResourceNotFoundException">商品が存在しない場合</exception>
    Task DeleteAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// 商品 ID で商品を取得する。Redis キャッシュを優先的に参照する。
    /// </summary>
    /// <param name="id">商品 ID</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>商品 DTO。存在しない場合は null</returns>
    Task<ProductDto?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// SKU コードで商品を取得する。Redis キャッシュを優先的に参照する。
    /// </summary>
    /// <param name="sku">SKU コード</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>商品 DTO。存在しない場合は null</returns>
    Task<ProductDto?> GetBySkuAsync(string sku, CancellationToken ct = default);

    /// <summary>
    /// 複数条件（キーワード、カテゴリ、ブランド）で商品を検索する。ページネーション対応。
    /// </summary>
    /// <param name="criteria">検索条件</param>
    /// <param name="page">ページ番号（0 始まり）</param>
    /// <param name="size">ページサイズ</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>ページネーション付き商品一覧</returns>
    Task<PaginatedResult<ProductDto>> SearchAsync(
        ProductSearchCriteria criteria, int page, int size, CancellationToken ct = default);

    /// <summary>
    /// 指定カテゴリに属する商品一覧を取得する。ページネーション対応。
    /// </summary>
    /// <param name="categoryId">カテゴリ ID</param>
    /// <param name="page">ページ番号（0 始まり）</param>
    /// <param name="size">ページサイズ</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>ページネーション付き商品一覧</returns>
    Task<PaginatedResult<ProductDto>> GetByCategoryAsync(
        string categoryId, int page, int size, CancellationToken ct = default);

    /// <summary>
    /// 商品 ID のリストで商品を一括取得する。マイクロサービス間のバッチ参照用。
    /// </summary>
    /// <param name="ids">商品 ID のリスト</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>商品 DTO のリスト</returns>
    Task<List<ProductDto>> GetByIdsAsync(List<string> ids, CancellationToken ct = default);

    /// <summary>
    /// 商品画像を Azure Blob Storage にアップロードし、商品に紐付ける。
    /// </summary>
    /// <param name="productId">商品 ID</param>
    /// <param name="file">アップロードファイル</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>アップロードされた画像の DTO</returns>
    /// <exception cref="Exceptions.ResourceNotFoundException">商品が存在しない場合</exception>
    Task<ProductImageDto> UploadImageAsync(string productId, IFormFile file, CancellationToken ct = default);
}
