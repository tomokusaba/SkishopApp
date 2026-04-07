namespace SalesManagementService.Infrastructure.ExternalServices;

public record PaymentResult(string PaymentId, string Status);

public interface IPaymentClient
{
    Task<PaymentResult> ProcessPaymentAsync(
        decimal amount, string paymentMethod, CancellationToken ct = default);

    Task RefundPaymentAsync(string paymentId, decimal amount, CancellationToken ct = default);
}
