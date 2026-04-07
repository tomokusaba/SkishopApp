using PointService.DTOs.Requests;
using PointService.DTOs.Responses;
using PointService.Repositories.Interfaces;
using PointService.Services.Interfaces;

namespace PointService.Services;

public class TierService(
    ITierDefinitionRepository tierRepository,
    IPointAccountRepository accountRepository,
    IPointCacheService cacheService,
    ILogger<TierService> logger) : ITierService
{
    private const decimal DefaultPointRate = 0.01m;

    public async Task<TierInfoResponse> GetTierInfoAsync(
        string userId, CancellationToken ct = default)
    {
        var account = await accountRepository.FindByUserIdAsync(userId, ct);
        var tiers = await tierRepository.FindAllAsync(ct);

        var currentTierName = await cacheService.GetUserTierAsync(userId, ct) ?? "BRONZE";
        var currentTier = tiers.FirstOrDefault(t => t.Name == currentTierName);
        var earnRate = currentTier?.PointRate ?? DefaultPointRate;

        var nextTier = tiers
            .Where(t => t.SortOrder > (currentTier?.SortOrder ?? 0))
            .OrderBy(t => t.SortOrder)
            .FirstOrDefault();

        var totalEarned = account?.TotalEarned ?? 0;
        var pointsToNextTier = nextTier is not null
            ? Math.Max(0, nextTier.MinAnnualPoints - totalEarned)
            : (int?)null;

        var benefits = currentTier?.Benefits?.Split(',', StringSplitOptions.TrimEntries).ToList()
            ?? [];

        return new TierInfoResponse(
            currentTierName, earnRate, totalEarned, totalEarned,
            nextTier?.Name, pointsToNextTier, benefits);
    }

    public async Task<decimal> GetPointRateForUserAsync(
        string userId, CancellationToken ct = default)
    {
        var tierName = await cacheService.GetUserTierAsync(userId, ct);
        if (tierName is null)
        {
            logger.LogDebug("ティアキャッシュ未設定、デフォルトレート使用: UserId={UserId}", userId);
            return DefaultPointRate;
        }

        var tier = await tierRepository.FindByNameAsync(tierName, ct);
        return tier?.PointRate ?? DefaultPointRate;
    }

    public async Task<List<TierDefinitionResponse>> GetAllTiersAsync(CancellationToken ct = default)
    {
        var tiers = await tierRepository.FindAllAsync(ct);
        return tiers.Select(t => new TierDefinitionResponse(
            t.Id, t.Name, t.PointRate, t.MinAnnualPoints, t.Benefits, t.SortOrder)).ToList();
    }

    public async Task<TierDefinitionResponse> UpdateTierAsync(
        string id, UpdateTierRequest request, CancellationToken ct = default)
    {
        var tier = await tierRepository.FindByIdAsync(id, ct)
            ?? throw new Exceptions.NotFoundException("指定されたティア定義が見つかりません");

        tier.PointRate = request.PointRate;
        tier.MinAnnualPoints = request.MinAnnualPoints;
        tier.Benefits = request.Benefits;
        await tierRepository.SaveChangesAsync(ct);

        return new TierDefinitionResponse(
            tier.Id, tier.Name, tier.PointRate, tier.MinAnnualPoints, tier.Benefits, tier.SortOrder);
    }
}
