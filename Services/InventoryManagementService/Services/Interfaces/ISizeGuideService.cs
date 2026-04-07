using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.DTOs.Responses;

namespace InventoryManagementService.Services.Interfaces;

/// <summary>
/// サイズガイド管理サービスのインターフェース。
/// カテゴリ別のサイズチャート（サイズ表）の CRUD 操作を提供する。
/// </summary>
public interface ISizeGuideService
{
    /// <summary>
    /// カテゴリ ID でサイズガイドを取得する。
    /// </summary>
    /// <param name="categoryId">カテゴリ ID</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>サイズガイド DTO。存在しない場合は null</returns>
    Task<SizeGuideDto?> GetByCategoryIdAsync(string categoryId, CancellationToken ct = default);

    /// <summary>
    /// 新規サイズガイドを作成する。カテゴリの存在確認を行う。
    /// </summary>
    /// <param name="request">サイズガイド作成リクエスト</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>作成されたサイズガイド DTO</returns>
    /// <exception cref="Exceptions.ResourceNotFoundException">カテゴリが存在しない場合</exception>
    Task<SizeGuideDto> CreateAsync(SizeGuideCreateRequest request, CancellationToken ct = default);

    /// <summary>
    /// 既存のサイズガイドを部分更新する。
    /// </summary>
    /// <param name="id">サイズガイド ID</param>
    /// <param name="request">更新リクエスト（null フィールドは更新対象外）</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>更新後のサイズガイド DTO</returns>
    /// <exception cref="Exceptions.ResourceNotFoundException">サイズガイドが存在しない場合</exception>
    Task<SizeGuideDto> UpdateAsync(string id, SizeGuideUpdateRequest request, CancellationToken ct = default);
}
