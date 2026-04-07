using PointService.DTOs.Responses;

namespace PointService.Services.Interfaces;

public interface IPointAnalyticsService
{
    Task<PointAnalyticsResponse> GetAnalyticsAsync(CancellationToken ct = default);
}
