using PointService.DTOs.Responses;
using PointService.Models;
using PointService.Repositories.Interfaces;
using PointService.Services.Interfaces;

namespace PointService.Services;

public class PointAnalyticsService(
    IPointTransactionRepository transactionRepository,
    IPointAccountRepository accountRepository,
    ITierDefinitionRepository tierDefinitionRepository,
    ILogger<PointAnalyticsService> logger) : IPointAnalyticsService
{
    public async Task<PointAnalyticsResponse> GetAnalyticsAsync(CancellationToken ct = default)
    {
        var totalIssued = await transactionRepository.SumPointsByTypeAsync(TransactionTypes.Earn, ct);
        var totalRedeemed = await transactionRepository.SumPointsByTypeAsync(TransactionTypes.Redeem, ct);
        var totalExpired = await transactionRepository.SumPointsByTypeAsync(TransactionTypes.Expire, ct);

        var redemptionRate = totalIssued > 0
            ? (double)totalRedeemed / totalIssued * 100
            : 0.0;

        var pointsByTier = await ComputePointsByTierAsync(ct);
        var tierDistribution = await ComputeTierDistributionAsync(ct);

        logger.LogInformation(
            "ポイント分析: TotalIssued={TotalIssued}, TotalRedeemed={TotalRedeemed}, " +
            "TotalExpired={TotalExpired}, RedemptionRate={RedemptionRate:F2}%",
            totalIssued, totalRedeemed, totalExpired, redemptionRate);

        return new PointAnalyticsResponse(
            totalIssued, totalRedeemed, totalExpired,
            redemptionRate,
            pointsByTier,
            tierDistribution);
    }

    private async Task<Dictionary<string, long>> ComputePointsByTierAsync(CancellationToken ct)
    {
        var tiers = await tierDefinitionRepository.FindAllAsync(ct);
        var sortedTiers = tiers.OrderBy(t => t.MinAnnualPoints).ToList();
        var result = new Dictionary<string, long>();

        for (var i = 0; i < sortedTiers.Count; i++)
        {
            var tier = sortedTiers[i];
            var maxPoints = i + 1 < sortedTiers.Count
                ? sortedTiers[i + 1].MinAnnualPoints
                : int.MaxValue;
            var count = await accountRepository.CountByTotalEarnedRangeAsync(
                tier.MinAnnualPoints, maxPoints, ct);
            result[tier.Name] = count;
        }

        return result;
    }

    private async Task<Dictionary<string, long>> ComputeTierDistributionAsync(CancellationToken ct)
    {
        var tiers = await tierDefinitionRepository.FindAllAsync(ct);
        var sortedTiers = tiers.OrderBy(t => t.MinAnnualPoints).ToList();
        var totalAccounts = await accountRepository.CountAllAsync(ct);

        if (totalAccounts == 0)
            return sortedTiers.ToDictionary(t => t.Name, _ => 0L);

        var result = new Dictionary<string, long>();
        for (var i = 0; i < sortedTiers.Count; i++)
        {
            var tier = sortedTiers[i];
            var maxPoints = i + 1 < sortedTiers.Count
                ? sortedTiers[i + 1].MinAnnualPoints
                : int.MaxValue;
            var count = await accountRepository.CountByTotalEarnedRangeAsync(
                tier.MinAnnualPoints, maxPoints, ct);
            var percentage = (long)Math.Round((double)count / totalAccounts * 100);
            result[tier.Name] = percentage;
        }

        return result;
    }
}
