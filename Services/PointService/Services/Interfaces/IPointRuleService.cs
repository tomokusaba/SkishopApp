namespace PointService.Services.Interfaces;

/// <summary>ポイント付与ルール管理サービス。</summary>
public interface IPointRuleService
{
    Task<List<Models.PointRule>> GetActiveRulesAsync(CancellationToken ct = default);
    Task<Models.PointRule?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<decimal> CalculateAdditionalRateAsync(
        decimal orderAmount, string? productCategory = null,
        CancellationToken ct = default);
}
