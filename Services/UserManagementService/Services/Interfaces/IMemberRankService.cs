using UserManagementService.DTOs.Responses;

namespace UserManagementService.Services.Interfaces;

/// <summary>
/// 会員ランクサービス。ランク初期化・購入額加算・年次評価を提供する。
/// </summary>
public interface IMemberRankService
{
    Task<MemberRankDto?> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task InitializeAsync(string userId, CancellationToken ct = default);
    Task AddPurchaseAmountAsync(string userId, string orderId, decimal amount, CancellationToken ct = default);
    Task EvaluateAllRanksAsync(CancellationToken ct = default);
    Task TryExecuteEvaluationWithLockAsync(CancellationToken ct = default);
}
