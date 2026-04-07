using Frontend.DTOs;

namespace Frontend.Services.Interfaces;

/// <summary>
/// 注文 API クライアントインターフェース
/// </summary>
public interface IOrderApiClient
{
    Task<CreateOrderResponse?> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default);
    Task<OrderDto?> GetOrderAsync(string orderId, CancellationToken ct = default);
    Task<OrderDto?> GetOrderByNumberAsync(string orderNumber, CancellationToken ct = default);
    Task<List<OrderDto>> GetCustomerOrdersAsync(string customerId, int page = 0, int size = 10, CancellationToken ct = default);
    Task CancelOrderAsync(string orderId, CancellationToken ct = default);
}
