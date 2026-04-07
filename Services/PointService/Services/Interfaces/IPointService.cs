using PointService.DTOs.Requests;
using PointService.DTOs.Responses;

namespace PointService.Services.Interfaces;

public interface IPointService
{
    Task<EarnPointsResult> EarnPointsAsync(
        string userId, string orderId, decimal orderAmount,
        CancellationToken ct = default);
    Task<ReservePointsResult> ReservePointsAsync(
        ReservePointsRequest request, CancellationToken ct = default);
    Task<int> ConfirmPointsAsync(
        string userId, string orderId, CancellationToken ct = default);
    Task<int> ReleasePointsAsync(
        string userId, string orderId, CancellationToken ct = default);
    Task<PointBalanceResponse> GetBalanceAsync(
        string userId, CancellationToken ct = default);
    Task<PagedResult<PointTransactionResponse>> GetTransactionHistoryAsync(
        string userId, int page, int pageSize, CancellationToken ct = default);
    Task AdjustPointsAsync(
        string userId, AdjustPointsRequest request,
        string adminUserId, string? ipAddress, string? userAgent,
        CancellationToken ct = default);
    Task<ExpiringPointsResponse> GetExpiringPointsAsync(
        string userId, CancellationToken ct = default);
    Task CreateAccountAsync(
        string userId, CancellationToken ct = default);
    Task AnonymizeUserDataAsync(
        string userId, CancellationToken ct = default);
}
