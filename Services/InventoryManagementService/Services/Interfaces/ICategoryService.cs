using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.DTOs.Responses;

namespace InventoryManagementService.Services.Interfaces;

/// <summary>
/// カテゴリ管理サービスのインターフェース。
/// 階層構造（親子関係）を持つカテゴリの CRUD 操作を提供する。
/// </summary>
public interface ICategoryService
{
    /// <summary>
    /// 新規カテゴリを作成する。親カテゴリ指定時は階層レベルとパスを自動計算する。
    /// </summary>
    /// <param name="request">カテゴリ作成リクエスト</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>作成されたカテゴリ DTO</returns>
    /// <exception cref="Exceptions.ResourceNotFoundException">指定された親カテゴリが存在しない場合</exception>
    Task<CategoryDto> CreateAsync(CategoryCreateRequest request, CancellationToken ct = default);

    /// <summary>
    /// 既存カテゴリの情報を部分更新する。
    /// </summary>
    /// <param name="id">カテゴリ ID</param>
    /// <param name="request">更新リクエスト（null フィールドは更新対象外）</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>更新後のカテゴリ DTO</returns>
    /// <exception cref="Exceptions.ResourceNotFoundException">カテゴリが存在しない場合</exception>
    Task<CategoryDto> UpdateAsync(string id, CategoryUpdateRequest request, CancellationToken ct = default);

    /// <summary>
    /// カテゴリを論理削除する。子カテゴリまたは紐づく商品が存在する場合は削除を拒否する。
    /// </summary>
    /// <param name="id">カテゴリ ID</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <exception cref="Exceptions.ResourceNotFoundException">カテゴリが存在しない場合</exception>
    /// <exception cref="Exceptions.InventoryException">子カテゴリまたは商品が存在する場合</exception>
    Task DeleteAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// カテゴリ ID でカテゴリを取得する。Redis キャッシュを優先的に参照する。
    /// </summary>
    /// <param name="id">カテゴリ ID</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>カテゴリ DTO。存在しない場合は null</returns>
    Task<CategoryDto?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// アクティブな全カテゴリを階層レベル順・名前順で取得する。Redis キャッシュを優先的に参照する。
    /// </summary>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>カテゴリ DTO のリスト</returns>
    Task<List<CategoryDto>> GetAllAsync(CancellationToken ct = default);
}
