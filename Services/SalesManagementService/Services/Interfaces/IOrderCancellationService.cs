namespace SalesManagementService.Services.Interfaces;

public interface IOrderCancellationService
{
    Task CancelOrderAsync(string orderId, string reason, CancellationToken ct = default);
}
