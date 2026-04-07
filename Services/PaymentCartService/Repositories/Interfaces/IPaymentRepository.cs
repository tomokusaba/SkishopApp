using PaymentCartService.Models;

namespace PaymentCartService.Repositories.Interfaces;

public interface IPaymentRepository
{
    Task<Payment?> FindByIdAsync(string id, CancellationToken ct = default);
    Task<Payment?> FindByIdWithTransactionsAsync(string id, CancellationToken ct = default);
    Task<Payment?> FindByOrderIdAsync(string orderId, CancellationToken ct = default);
    Task<Payment?> FindByOrderIdWithTransactionsAsync(string orderId, CancellationToken ct = default);
    Task<Payment?> FindByStripeCheckoutSessionIdAsync(string sessionId, CancellationToken ct = default);
    Task<Payment?> FindByStripePaymentIntentIdAsync(string intentId, CancellationToken ct = default);
    Task<(List<Payment> Items, int TotalCount)> FindByCustomerIdAsync(
        string customerId, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Payment payment, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
