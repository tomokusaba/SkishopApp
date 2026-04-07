using PointService.DTOs.Requests;
using PointService.DTOs.Responses;

namespace PointService.Services.Interfaces;

public interface ITierService
{
    Task<TierInfoResponse> GetTierInfoAsync(string userId, CancellationToken ct = default);
    Task<decimal> GetPointRateForUserAsync(string userId, CancellationToken ct = default);
    Task<List<TierDefinitionResponse>> GetAllTiersAsync(CancellationToken ct = default);
    Task<TierDefinitionResponse> UpdateTierAsync(string id, UpdateTierRequest request, CancellationToken ct = default);
}
