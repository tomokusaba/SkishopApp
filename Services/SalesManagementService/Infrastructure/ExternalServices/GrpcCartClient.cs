using Grpc.Core;
using SkiShop.Contracts.Cart.V1;

namespace SalesManagementService.Infrastructure.ExternalServices;

/// <summary>
/// gRPC 経由のカートサービスクライアント
/// </summary>
public class GrpcCartClient(
    CartGrpcService.CartGrpcServiceClient grpcClient,
    ILogger<GrpcCartClient> logger) : ICartClient
{
    public async Task<GetCartResponse> GetCartAsync(
        string userId, int deadlineMs, CancellationToken ct = default)
    {
        logger.LogInformation("gRPC GetCart: UserId={UserId}", userId);

        var request = new GetCartSnapshotRequest { CartId = userId };
        var deadline = DateTime.UtcNow.AddMilliseconds(deadlineMs);
        var response = await grpcClient.GetCartSnapshotAsync(
            request, deadline: deadline, cancellationToken: ct);

        var items = response.Items.Select(i => new CartItemDto(
            i.ProductId,
            i.ProductName,
            (decimal)i.UnitPriceMinorUnits / 100m,
            i.Quantity
        )).ToList();

        return new GetCartResponse(items);
    }

    public async Task ClearCartAsync(
        string userId, int deadlineMs, CancellationToken ct = default)
    {
        logger.LogInformation("gRPC ClearCart: UserId={UserId}", userId);

        var request = new ClearCartRequest { CartId = userId };
        var deadline = DateTime.UtcNow.AddMilliseconds(deadlineMs);
        await grpcClient.ClearCartAsync(
            request, deadline: deadline, cancellationToken: ct);
    }
}
