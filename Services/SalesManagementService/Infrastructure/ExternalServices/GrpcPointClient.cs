using Grpc.Core;
using PointService.Protos;

namespace SalesManagementService.Infrastructure.ExternalServices;

/// <summary>
/// gRPC 経由のポイントサービスクライアント
/// </summary>
public class GrpcPointClient(
    PointGrpc.PointGrpcClient grpcClient,
    ILogger<GrpcPointClient> logger) : IPointClient
{
    public async Task<ReservePointsResponse> ReservePointsAsync(
        string userId, int points, int deadlineMs, CancellationToken ct = default)
    {
        logger.LogInformation("gRPC ReservePoints: UserId={UserId}, Points={Points}",
            userId, points);

        var request = new PointService.Protos.ReservePointsRequest
        {
            UserId = userId,
            OrderId = Guid.NewGuid().ToString(),
            Points = points,
            IdempotencyKey = Guid.NewGuid().ToString()
        };

        var deadline = DateTime.UtcNow.AddMilliseconds(deadlineMs);
        var response = await grpcClient.ReservePointsAsync(
            request, deadline: deadline, cancellationToken: ct);

        if (!response.Success)
            throw new InvalidOperationException($"ポイント予約に失敗しました: {response.ErrorMessage}");

        return new ReservePointsResponse(response.RemainingBalance.ToString());
    }

    public async Task ReleasePointsAsync(
        string reservationId, int deadlineMs, CancellationToken ct = default)
    {
        logger.LogInformation("gRPC ReleasePoints: ReservationId={ReservationId}", reservationId);

        var request = new PointService.Protos.ReleasePointsRequest
        {
            UserId = string.Empty,
            OrderId = reservationId,
            IdempotencyKey = Guid.NewGuid().ToString()
        };

        var deadline = DateTime.UtcNow.AddMilliseconds(deadlineMs);
        await grpcClient.ReleasePointsAsync(
            request, deadline: deadline, cancellationToken: ct);
    }

    public async Task AwardPointsAsync(
        string userId, decimal orderAmount, int deadlineMs, CancellationToken ct = default)
    {
        logger.LogInformation("gRPC AwardPoints: UserId={UserId}, Amount={Amount}",
            userId, orderAmount);

        var request = new AwardPointsRequest
        {
            UserId = userId,
            OrderId = Guid.NewGuid().ToString(),
            OrderAmount = (long)(orderAmount * 100),
            IdempotencyKey = Guid.NewGuid().ToString()
        };

        var deadline = DateTime.UtcNow.AddMilliseconds(deadlineMs);
        var response = await grpcClient.AwardPointsAsync(
            request, deadline: deadline, cancellationToken: ct);

        if (!response.Success)
            throw new InvalidOperationException($"ポイント付与に失敗しました: {response.ErrorMessage}");
    }

    public async Task AdjustPointsForReturnAsync(
        string userId, decimal refundAmount, int usedPoints, int deadlineMs,
        CancellationToken ct = default)
    {
        // AdjustPointsForReturn は proto に未定義 — ログのみ出力
        logger.LogWarning(
            "AdjustPointsForReturn は gRPC proto 未定義のため未対応です。UserId={UserId}, RefundAmount={RefundAmount}",
            userId, refundAmount);
        await Task.CompletedTask;
    }
}
