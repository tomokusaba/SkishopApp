using Grpc.Core;
using PointService.Protos;
using PointService.Services.Interfaces;

namespace PointService.GrpcServices;

public class PointGrpcService(
    IServiceScopeFactory scopeFactory,
    ILogger<PointGrpcService> logger) : PointGrpc.PointGrpcBase
{
    public override async Task<ReservePointsResponse> ReservePoints(
        ReservePointsRequest request, ServerCallContext context)
    {
        logger.LogInformation(
            "gRPC ReservePoints: UserId={UserId}, OrderId={OrderId}, Points={Points}",
            request.UserId, request.OrderId, request.Points);

        using var scope = scopeFactory.CreateScope();
        var pointService = scope.ServiceProvider.GetRequiredService<IPointService>();

        try
        {
            var reserveRequest = new DTOs.Requests.ReservePointsRequest(
                request.UserId, request.OrderId, request.Points);
            var result = await pointService.ReservePointsAsync(reserveRequest, context.CancellationToken);

            return new ReservePointsResponse
            {
                Success = result.Success,
                RemainingBalance = result.RemainingBalance,
                ErrorMessage = result.ErrorMessage ?? string.Empty
            };
        }
        catch (Exception ex) when (ex is not RpcException)
        {
            logger.LogError(ex, "gRPC ReservePoints エラー: UserId={UserId}", request.UserId);
            return new ReservePointsResponse
            {
                Success = false,
                ErrorMessage = "ポイント仮消費に失敗しました"
            };
        }
    }

    public override async Task<ReleasePointsResponse> ReleasePoints(
        ReleasePointsRequest request, ServerCallContext context)
    {
        logger.LogInformation(
            "gRPC ReleasePoints: UserId={UserId}, OrderId={OrderId}",
            request.UserId, request.OrderId);

        using var scope = scopeFactory.CreateScope();
        var pointService = scope.ServiceProvider.GetRequiredService<IPointService>();

        try
        {
            var released = await pointService.ReleasePointsAsync(
                request.UserId, request.OrderId, context.CancellationToken);

            return new ReleasePointsResponse
            {
                Success = true,
                ReleasedPoints = released
            };
        }
        catch (Exception ex) when (ex is not RpcException)
        {
            logger.LogError(ex, "gRPC ReleasePoints エラー: UserId={UserId}", request.UserId);
            return new ReleasePointsResponse
            {
                Success = false,
                ErrorMessage = "ポイント解放に失敗しました"
            };
        }
    }

    public override async Task<AwardPointsResponse> AwardPoints(
        AwardPointsRequest request, ServerCallContext context)
    {
        logger.LogInformation(
            "gRPC AwardPoints: UserId={UserId}, OrderId={OrderId}, Amount={Amount}",
            request.UserId, request.OrderId, request.OrderAmount);

        using var scope = scopeFactory.CreateScope();
        var pointService = scope.ServiceProvider.GetRequiredService<IPointService>();

        try
        {
            var result = await pointService.EarnPointsAsync(
                request.UserId, request.OrderId, request.OrderAmount,
                context.CancellationToken);

            return new AwardPointsResponse
            {
                Success = true,
                AwardedPoints = result.EarnedPoints,
                NewBalance = result.NewBalance
            };
        }
        catch (Exception ex) when (ex is not RpcException)
        {
            logger.LogError(ex, "gRPC AwardPoints エラー: UserId={UserId}", request.UserId);
            return new AwardPointsResponse
            {
                Success = false,
                ErrorMessage = "ポイント付与に失敗しました"
            };
        }
    }

    public override async Task<ConfirmPointsResponse> ConfirmPoints(
        ConfirmPointsRequest request, ServerCallContext context)
    {
        logger.LogInformation(
            "gRPC ConfirmPoints: UserId={UserId}, OrderId={OrderId}",
            request.UserId, request.OrderId);

        using var scope = scopeFactory.CreateScope();
        var pointService = scope.ServiceProvider.GetRequiredService<IPointService>();

        try
        {
            var confirmed = await pointService.ConfirmPointsAsync(
                request.UserId, request.OrderId, context.CancellationToken);

            var balance = await pointService.GetBalanceAsync(
                request.UserId, context.CancellationToken);

            return new ConfirmPointsResponse
            {
                Success = true,
                ConfirmedPoints = confirmed,
                NewBalance = balance.AvailablePoints
            };
        }
        catch (Exception ex) when (ex is not RpcException)
        {
            logger.LogError(ex, "gRPC ConfirmPoints エラー: UserId={UserId}", request.UserId);
            return new ConfirmPointsResponse
            {
                Success = false,
                ErrorMessage = "ポイント確定に失敗しました"
            };
        }
    }

    public override async Task<GetBalanceResponse> GetBalance(
        GetBalanceRequest request, ServerCallContext context)
    {
        logger.LogInformation("gRPC GetBalance: UserId={UserId}", request.UserId);

        using var scope = scopeFactory.CreateScope();
        var pointService = scope.ServiceProvider.GetRequiredService<IPointService>();

        try
        {
            var balance = await pointService.GetBalanceAsync(
                request.UserId, context.CancellationToken);

            return new GetBalanceResponse
            {
                AvailablePoints = balance.AvailablePoints,
                PendingPoints = balance.PendingPoints,
                TotalEarned = balance.TotalEarned,
                TotalSpent = balance.TotalSpent
            };
        }
        catch (PointService.Exceptions.PointAccountNotFoundException ex)
        {
            logger.LogWarning("gRPC GetBalance: ポイントアカウント未登録 UserId={UserId}", request.UserId);
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (Exception ex) when (ex is not RpcException)
        {
            logger.LogError(ex, "gRPC GetBalance エラー: UserId={UserId}", request.UserId);
            throw new RpcException(new Status(StatusCode.Internal, "残高取得に失敗しました"));
        }
    }
}
