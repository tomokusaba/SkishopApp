using PointService.Models;
using PointService.Repositories.Interfaces;
using PointService.Services.Interfaces;

namespace PointService.Services;

public class PointRuleService(
    IPointRuleRepository ruleRepository,
    ILogger<PointRuleService> logger) : IPointRuleService
{
    public async Task<List<PointRule>> GetActiveRulesAsync(CancellationToken ct = default)
        => await ruleRepository.FindActiveRulesAsync(ct);

    public async Task<PointRule?> GetByIdAsync(string id, CancellationToken ct = default)
        => await ruleRepository.FindByIdAsync(id, ct);

    public async Task<decimal> CalculateAdditionalRateAsync(
        decimal orderAmount, string? productCategory = null,
        CancellationToken ct = default)
    {
        var rules = await ruleRepository.FindActiveRulesAsync(ct);
        var applicableRule = rules
            .Where(r => orderAmount >= r.MinimumAmount)
            .OrderByDescending(r => r.MinimumAmount)
            .FirstOrDefault();

        if (applicableRule is null)
        {
            logger.LogDebug("適用可能なポイントルールなし: OrderAmount={OrderAmount}", orderAmount);
            return 0m;
        }

        logger.LogDebug(
            "ポイントルール適用: RuleId={RuleId}, Rate={Rate}, OrderAmount={OrderAmount}",
            applicableRule.Id, applicableRule.PointRate, orderAmount);

        return applicableRule.PointRate;
    }
}
