using CouponService.DTOs.Requests;
using CouponService.DTOs.Responses;
using CouponService.Exceptions;
using CouponService.Models;
using CouponService.Repositories.Interfaces;
using CouponService.Services.Interfaces;

namespace CouponService.Services;

public class CampaignAppService(
    ICampaignRepository campaignRepository,
    TimeProvider timeProvider,
    ILogger<CampaignAppService> logger) : ICampaignService
{
    public async Task<CampaignResponse> CreateCampaignAsync(
        CreateCampaignRequest request, CancellationToken ct = default)
    {
        var campaign = new Campaign
        {
            Name = request.Name,
            Description = request.Description,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            MaxCoupons = request.MaxCoupons
        };

        await campaignRepository.AddAsync(campaign, ct);
        await campaignRepository.SaveChangesAsync(ct);

        logger.LogInformation("キャンペーン作成: {CampaignName}, Id={CampaignId}", campaign.Name, campaign.Id);
        return MapToResponse(campaign);
    }

    public async Task<CampaignResponse?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var campaign = await campaignRepository.FindByIdAsync(id, ct);
        return campaign is null ? null : MapToResponse(campaign);
    }

    public async Task<PagedResponse<CampaignResponse>> GetAllAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var campaigns = await campaignRepository.GetAllAsync(page, pageSize, ct);
        var totalCount = await campaignRepository.CountAllAsync(ct);

        var items = campaigns.Select(MapToResponse).ToList();
        return new PagedResponse<CampaignResponse>(
            items, totalCount, page, pageSize,
            (int)Math.Ceiling((double)totalCount / pageSize));
    }

    public async Task<CampaignResponse> UpdateCampaignAsync(
        string id, UpdateCampaignRequest request, CancellationToken ct = default)
    {
        var campaign = await campaignRepository.FindByIdAsync(id, ct)
            ?? throw new CouponNotFoundException($"キャンペーン {id} が見つかりません");

        if (request.Name is not null) campaign.Name = request.Name;
        if (request.Description is not null) campaign.Description = request.Description;
        if (request.StartDate.HasValue) campaign.StartDate = request.StartDate.Value;
        if (request.EndDate.HasValue) campaign.EndDate = request.EndDate.Value;
        if (request.MaxCoupons.HasValue) campaign.MaxCoupons = request.MaxCoupons.Value;

        await campaignRepository.SaveChangesAsync(ct);

        logger.LogInformation("キャンペーン更新: {CampaignName}", campaign.Name);
        return MapToResponse(campaign);
    }

    public async Task ActivateCampaignAsync(string id, CancellationToken ct = default)
    {
        var campaign = await campaignRepository.FindByIdAsync(id, ct)
            ?? throw new CouponNotFoundException($"キャンペーン {id} が見つかりません");

        if (campaign.Status is not (CampaignStatus.Draft or CampaignStatus.Paused))
            throw new BusinessException($"キャンペーンのステータス {campaign.Status} から Active への遷移はできません");

        campaign.Status = CampaignStatus.Active;
        await campaignRepository.SaveChangesAsync(ct);

        logger.LogInformation("キャンペーン有効化: {CampaignName}", campaign.Name);
    }

    public async Task PauseCampaignAsync(string id, CancellationToken ct = default)
    {
        var campaign = await campaignRepository.FindByIdAsync(id, ct)
            ?? throw new CouponNotFoundException($"キャンペーン {id} が見つかりません");

        if (campaign.Status != CampaignStatus.Active)
            throw new BusinessException($"キャンペーンのステータス {campaign.Status} から Paused への遷移はできません");

        campaign.Status = CampaignStatus.Paused;
        await campaignRepository.SaveChangesAsync(ct);

        logger.LogInformation("キャンペーン一時停止: {CampaignName}", campaign.Name);
    }

    public async Task<int> CompleteExpiredCampaignsAsync(CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var completed = await campaignRepository.CompleteExpiredCampaignsAsync(now, ct);
        if (completed > 0)
            logger.LogInformation("終了キャンペーンを {Count} 件更新しました", completed);
        return completed;
    }

    private static CampaignResponse MapToResponse(Campaign c) => new(
        c.Id, c.Name, c.Description, (int)c.Status,
        c.StartDate, c.EndDate, c.MaxCoupons,
        c.IssuedCount, c.CreatedAt);
}
