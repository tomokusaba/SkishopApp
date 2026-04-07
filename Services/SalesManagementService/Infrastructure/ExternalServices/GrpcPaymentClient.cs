using Grpc.Core;
using SkiShop.Contracts.Payment.V1;

namespace SalesManagementService.Infrastructure.ExternalServices;

/// <summary>
/// gRPC 経由の決済サービスクライアント
/// </summary>
public class GrpcPaymentClient(
    PaymentGrpcService.PaymentGrpcServiceClient grpcClient,
    ILogger<GrpcPaymentClient> logger) : IPaymentClient
{
    public async Task<PaymentResult> ProcessPaymentAsync(
        decimal amount, string paymentMethod, CancellationToken ct = default)
    {
        logger.LogInformation("gRPC ProcessPayment: Amount={Amount}, Method={Method}",
            amount, paymentMethod);

        var request = new ProcessPaymentRequest
        {
            OrderId = Guid.NewGuid().ToString(),
            CustomerId = "system",
            PaymentMethod = paymentMethod,
            AmountMinorUnits = (long)(amount * 100),
            CurrencyCode = "JPY",
            IdempotencyKey = Guid.NewGuid().ToString()
        };

        var response = await grpcClient.ProcessPaymentAsync(request, cancellationToken: ct);

        return new PaymentResult(response.PaymentId, response.Status.ToString());
    }

    public async Task RefundPaymentAsync(
        string paymentId, decimal amount, CancellationToken ct = default)
    {
        logger.LogInformation("gRPC RefundPayment: PaymentId={PaymentId}, Amount={Amount}",
            paymentId, amount);

        var request = new RefundPaymentRequest
        {
            PaymentId = paymentId,
            Reason = "注文キャンセルによる返金",
            IdempotencyKey = Guid.NewGuid().ToString()
        };

        await grpcClient.RefundPaymentAsync(request, cancellationToken: ct);
    }
}
