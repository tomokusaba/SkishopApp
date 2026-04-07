using Frontend.DTOs;
using Frontend.Services.Interfaces;

namespace Frontend.Services;

/// <summary>
/// 配送・返品 API クライアント
/// BFF パターン: サーバーサイドから API Gateway 経由で販売管理サービスに通信
/// </summary>
public class ShipmentReturnApiClient(
    IApiGatewayClient apiClient,
    ILogger<ShipmentReturnApiClient> logger) : IShipmentReturnApiClient
{
    public async Task<ShipmentDto?> GetShipmentByOrderAsync(string orderId, CancellationToken ct = default)
    {
        return await apiClient.GetAsync<ShipmentDto>($"/api/v1/shipments/order/{orderId}", ct);
    }

    public async Task<ReturnDto?> CreateReturnRequestAsync(CreateReturnRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("返品リクエスト作成: OrderId={OrderId}", request.OrderId);
        return await apiClient.PostAsync<CreateReturnRequest, ReturnDto>("/api/v1/returns", request, ct);
    }

    public async Task<List<ReturnDto>> GetReturnsByOrderAsync(string orderId, CancellationToken ct = default)
    {
        var result = await apiClient.GetAsync<List<ReturnDto>>($"/api/v1/returns/order/{orderId}", ct);
        return result ?? [];
    }

    public async Task<OrderDto?> GetOrderByNumberAndEmailAsync(string orderNumber, string email, CancellationToken ct = default)
    {
        return await apiClient.GetAsync<OrderDto>(
            $"/api/v1/orders/track?orderNumber={Uri.EscapeDataString(orderNumber)}&email={Uri.EscapeDataString(email)}", ct);
    }
}

