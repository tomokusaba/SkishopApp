using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.DTOs.Responses;
using InventoryManagementService.Exceptions;
using InventoryManagementService.Models;
using InventoryManagementService.Repositories.Interfaces;
using InventoryManagementService.Services.Interfaces;

namespace InventoryManagementService.Services;

/// <summary>
/// サイズガイド管理サービスの実装クラス。
/// カテゴリ別のサイズチャート（サイズ表）の作成・更新・取得を管理する。
/// </summary>
/// <remarks>
/// サイズガイドはカテゴリに 1:1 で紐づき、スキー用品のサイズ選択を支援する。
/// カテゴリの存在確認を行った上でサイズガイドの作成を許可する。
/// </remarks>
public class SizeGuideService(
    ISizeGuideRepository sizeGuideRepository,
    ICategoryRepository categoryRepository,
    ILogger<SizeGuideService> logger) : ISizeGuideService
{
    /// <inheritdoc />
    public async Task<SizeGuideDto?> GetByCategoryIdAsync(
        string categoryId, CancellationToken ct = default)
    {
        var guide = await sizeGuideRepository.FindByCategoryIdAsync(categoryId, ct);
        return guide is null ? null : MapToDto(guide);
    }

    /// <inheritdoc />
    public async Task<SizeGuideDto> CreateAsync(
        SizeGuideCreateRequest request, CancellationToken ct = default)
    {
        if (!await categoryRepository.ExistsByIdAsync(request.CategoryId, ct))
            throw new ResourceNotFoundException("Category", request.CategoryId);

        var guide = new SizeGuide
        {
            CategoryId = request.CategoryId,
            SizeChart = request.SizeChart,
            GuideType = request.GuideType
        };

        await sizeGuideRepository.AddAsync(guide, ct);
        await sizeGuideRepository.SaveChangesAsync(ct);

        logger.LogInformation("サイズガイド作成完了: SizeGuideId={SizeGuideId}, CategoryId={CategoryId}",
            guide.Id, request.CategoryId);
        return MapToDto(guide);
    }

    /// <inheritdoc />
    public async Task<SizeGuideDto> UpdateAsync(
        string id, SizeGuideUpdateRequest request, CancellationToken ct = default)
    {
        var guide = await sizeGuideRepository.FindByIdAsync(id, ct)
            ?? throw new ResourceNotFoundException("SizeGuide", id);

        if (request.SizeChart is not null) guide.SizeChart = request.SizeChart;
        if (request.GuideType is not null) guide.GuideType = request.GuideType;

        await sizeGuideRepository.SaveChangesAsync(ct);

        logger.LogInformation("サイズガイド更新完了: SizeGuideId={SizeGuideId}", id);
        return MapToDto(guide);
    }

    /// <summary>
    /// SizeGuide エンティティを SizeGuideDto に変換する。
    /// </summary>
    private static SizeGuideDto MapToDto(SizeGuide s) => new(
        s.Id, s.CategoryId, s.SizeChart, s.GuideType, s.CreatedAt, s.UpdatedAt);
}
