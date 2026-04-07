using Polly;
using Polly.Registry;
using Stripe;
using Stripe.Checkout;
using PaymentCartService.Services.Interfaces;

namespace PaymentCartService.Services;

public class StripeGateway(
    IStripeClient stripeClient,
    ResiliencePipelineProvider<string> resilienceProvider,
    ILogger<StripeGateway> logger) : IStripeGateway
{
    public async Task<Session> CreateCheckoutSessionAsync(
        SessionCreateOptions options, CancellationToken ct = default)
    {
        logger.LogDebug("Stripe Checkout セッション作成開始");
        var pipeline = resilienceProvider.GetPipeline("stripe");
        return await pipeline.ExecuteAsync(async token =>
        {
            var service = new SessionService(stripeClient);
            return await service.CreateAsync(options, cancellationToken: token);
        }, ct);
    }

    public async Task<Stripe.Refund> CreateRefundAsync(
        RefundCreateOptions options, string? idempotencyKey = null, CancellationToken ct = default)
    {
        logger.LogDebug("Stripe 返金処理開始");
        var pipeline = resilienceProvider.GetPipeline("stripe");
        return await pipeline.ExecuteAsync(async token =>
        {
            // Stripe.RefundService を完全修飾名で指定（PaymentCartService.Services.RefundService との競合回避）
            var stripeRefundService = new Stripe.RefundService(stripeClient);
            var requestOptions = idempotencyKey is not null
                ? new RequestOptions { IdempotencyKey = idempotencyKey }
                : null;
            return await stripeRefundService.CreateAsync(options, requestOptions, token);
        }, ct);
    }
}
