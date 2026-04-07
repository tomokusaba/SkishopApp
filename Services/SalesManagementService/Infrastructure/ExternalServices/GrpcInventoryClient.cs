using Grpc.Core;
using InventoryManagementService.Protos;

namespace SalesManagementService.Infrastructure.ExternalServices;

/// <summary>
/// gRPC 経由の在庫管理サービスクライアント
/// </summary>
public class GrpcInventoryClient(
    InventoryService.InventoryServiceClient grpcClient,
    ILogger<GrpcInventoryClient> logger) : IInventoryClient
{
    public async Task<ReserveInventoryResponse> ReserveInventoryAsync(
        IReadOnlyList<CartItemDto> items, int deadlineMs, CancellationToken ct = default)
    {
        logger.LogInformation("gRPC ReserveInventory: ItemCount={Count}", items.Count);

        var request = new ReserveInventoryRequest
        {
            OrderId = Guid.NewGuid().ToString()
        };
        foreach (var item in items)
        {
            request.Items.Add(new ReserveItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity
            });
        }

        var deadline = DateTime.UtcNow.AddMilliseconds(deadlineMs);
        var response = await grpcClient.ReserveInventoryAsync(
            request, deadline: deadline, cancellationToken: ct);

        if (!response.Success)
            throw new InvalidOperationException($"在庫予約に失敗しました: {response.ErrorMessage}");

        return new ReserveInventoryResponse(response.ReservationId);
    }

    public async Task ReleaseReservationAsync(
        string reservationId, int deadlineMs, CancellationToken ct = default)
    {
        logger.LogInformation("gRPC ReleaseReservation: ReservationId={ReservationId}", reservationId);

        var request = new ReleaseReservationRequest
        {
            ReservationId = reservationId,
            OrderId = string.Empty
        };

        var deadline = DateTime.UtcNow.AddMilliseconds(deadlineMs);
        var response = await grpcClient.ReleaseReservationAsync(
            request, deadline: deadline, cancellationToken: ct);

        if (!response.Success)
            logger.LogWarning("在庫解放の応答エラー: {ErrorMessage}", response.ErrorMessage);
    }

    public async Task RestoreInventoryAsync(
        string productId, int quantity, int deadlineMs, CancellationToken ct = default)
    {
        // RestoreInventory は proto に未定義 — ReleaseReservation で代替
        logger.LogWarning(
            "RestoreInventory は gRPC proto 未定義のため未対応です。ProductId={ProductId}, Quantity={Quantity}",
            productId, quantity);
        await Task.CompletedTask;
    }
}
