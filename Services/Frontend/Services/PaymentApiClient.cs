using Frontend.DTOs;
using Frontend.Services.Interfaces;

namespace Frontend.Services;

public class PaymentApiClient(IApiGatewayClient apiClient, ILogger<PaymentApiClient> logger) : IPaymentApiClient
{
    public async Task<GuestCheckoutResponse?> GuestCheckoutAsync(GuestCheckoutRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("ゲストチェックアウト実行");
        return await apiClient.PostAsync<GuestCheckoutRequest, GuestCheckoutResponse>(
            "/api/v1/checkout/guest", request, ct);
    }

    public async Task<PaymentDto?> GetPaymentAsync(string paymentId, CancellationToken ct = default)
    {
        return await apiClient.GetAsync<PaymentDto>($"/api/v1/payments/{paymentId}", ct);
    }

    public async Task<PaymentDto?> GetPaymentByOrderAsync(string orderId, CancellationToken ct = default)
    {
        return await apiClient.GetAsync<PaymentDto>($"/api/v1/payments/order/{orderId}", ct);
    }
}

