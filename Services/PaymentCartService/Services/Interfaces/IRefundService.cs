using PaymentCartService.DTOs.Requests;
using PaymentCartService.DTOs.Responses;

namespace PaymentCartService.Services.Interfaces;

public interface IRefundService
{
    Task<RefundResponse> RefundAsync(
        string paymentId, RefundRequest request, string adminUserId, CancellationToken ct = default);
}
