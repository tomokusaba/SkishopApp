using PaymentCartService.DTOs.Requests;
using PaymentCartService.DTOs.Responses;

namespace PaymentCartService.Services.Interfaces;

public interface IPaymentService
{
    Task<PaymentResponse> CheckoutAsync(
        CheckoutRequest request, string userId, CancellationToken ct = default);
    Task<PaymentDetailResponse?> GetByIdAsync(
        string paymentId, string userId, CancellationToken ct = default);
    Task<PaymentDetailResponse?> GetByOrderIdAsync(
        string orderId, string userId, CancellationToken ct = default);
    Task<PaginatedResponse<PaymentResponse>> GetByCustomerIdAsync(
        string customerId, string userId, int page, int pageSize, CancellationToken ct = default);
    Task HandleWebhookAsync(string json, string signature, CancellationToken ct = default);
    Task CompensatePaymentAsync(string paymentId, CancellationToken ct = default);
    Task<PaymentResponse> GuestCheckoutAsync(string cartId, GuestCheckoutRequest request, CancellationToken ct = default);
}
