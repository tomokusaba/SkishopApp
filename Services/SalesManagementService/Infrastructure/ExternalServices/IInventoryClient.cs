namespace SalesManagementService.Infrastructure.ExternalServices;

public record CartItemDto(string ProductId, string ProductName, decimal UnitPrice, int Quantity);
public record ReserveInventoryResponse(string ReservationId);

public interface IInventoryClient
{
    Task<ReserveInventoryResponse> ReserveInventoryAsync(
        IReadOnlyList<CartItemDto> items, int deadlineMs, CancellationToken ct = default);

    Task ReleaseReservationAsync(string reservationId, int deadlineMs, CancellationToken ct = default);

    Task RestoreInventoryAsync(string productId, int quantity, int deadlineMs, CancellationToken ct = default);
}
