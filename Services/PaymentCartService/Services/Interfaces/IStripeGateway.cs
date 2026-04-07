using Stripe;
using Stripe.Checkout;

namespace PaymentCartService.Services.Interfaces;

public interface IStripeGateway
{
    Task<Session> CreateCheckoutSessionAsync(SessionCreateOptions options, CancellationToken ct = default);
    Task<Stripe.Refund> CreateRefundAsync(RefundCreateOptions options, string? idempotencyKey = null, CancellationToken ct = default);
}
