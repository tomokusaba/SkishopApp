using Frontend.DTOs;

namespace Frontend.Services.Interfaces;

/// <summary>
/// 決済 API クライアントインターフェース
/// </summary>
public interface IPaymentApiClient
{
    Task<GuestCheckoutResponse?> GuestCheckoutAsync(GuestCheckoutRequest request, CancellationToken ct = default);
    Task<PaymentDto?> GetPaymentAsync(string paymentId, CancellationToken ct = default);
    Task<PaymentDto?> GetPaymentByOrderAsync(string orderId, CancellationToken ct = default);
}
