namespace SalesManagementService.Infrastructure.ExternalServices;

public record ReservePointsResponse(string ReservationId);

public interface IPointClient
{
    Task<ReservePointsResponse> ReservePointsAsync(
        string userId, int points, int deadlineMs, CancellationToken ct = default);

    Task ReleasePointsAsync(string reservationId, int deadlineMs, CancellationToken ct = default);

    Task AwardPointsAsync(string userId, decimal orderAmount, int deadlineMs, CancellationToken ct = default);

    Task AdjustPointsForReturnAsync(
        string userId, decimal refundAmount, int usedPoints, int deadlineMs, CancellationToken ct = default);
}
