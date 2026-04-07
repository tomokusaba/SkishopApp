using Frontend.DTOs;
using Frontend.Services.Interfaces;

namespace Frontend.Services;

public class OrderApiClient(IApiGatewayClient apiClient, ILogger<OrderApiClient> logger) : IOrderApiClient
{
    public async Task<CreateOrderResponse?> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("注文作成");
        return await apiClient.PostAsync<CreateOrderRequest, CreateOrderResponse>("/api/v1/orders", request, ct);
    }

    public async Task<OrderDto?> GetOrderAsync(string orderId, CancellationToken ct = default)
    {
        return await apiClient.GetAsync<OrderDto>($"/api/v1/orders/{orderId}", ct);
    }

    public async Task<OrderDto?> GetOrderByNumberAsync(string orderNumber, CancellationToken ct = default)
    {
        return await apiClient.GetAsync<OrderDto>($"/api/v1/orders/number/{orderNumber}", ct);
    }

    public async Task<List<OrderDto>> GetCustomerOrdersAsync(string customerId, int page = 0, int size = 10, CancellationToken ct = default)
    {
        var result = await apiClient.GetAsync<Models.PaginatedResult<OrderDto>>(
            $"/api/v1/orders/customer/{customerId}?page={page}&size={size}&sort=createdAt,desc", ct);
        return result?.Items ?? [];
    }

    public async Task CancelOrderAsync(string orderId, CancellationToken ct = default)
    {
        logger.LogInformation("注文キャンセル: OrderId={OrderId}", orderId);
        await apiClient.PostAsync<object, object>($"/api/v1/orders/{orderId}/cancel", new { }, ct);
    }
}

