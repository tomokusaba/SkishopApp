using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using PaymentCartService.Infrastructure.Persistence;
using PaymentCartService.Models;
using PaymentCartService.Models.Enums;
using PaymentCartService.Repositories.Interfaces;
using SkiShop.Contracts.Payment.V1;
using GrpcPaymentStatus = SkiShop.Contracts.Payment.V1.PaymentStatus;
using DomainPaymentStatus = PaymentCartService.Models.Enums.PaymentStatus;

namespace PaymentCartService.GrpcServices;

public class PaymentGrpcServiceImpl(
    IPaymentRepository paymentRepository,
    IOutboxEventRepository outboxEventRepository,
    AppDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<PaymentGrpcServiceImpl> logger) : SkiShop.Contracts.Payment.V1.PaymentGrpcService.PaymentGrpcServiceBase
{
    public override async Task<ProcessPaymentResponse> ProcessPayment(
        ProcessPaymentRequest request,
        ServerCallContext callContext)
    {
        var ct = callContext.CancellationToken;

        // 入力バリデーション
        if (string.IsNullOrWhiteSpace(request.OrderId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "order_id は必須です"));
        if (string.IsNullOrWhiteSpace(request.CustomerId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "customer_id は必須です"));
        if (request.AmountMinorUnits <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "amount_minor_units は 1 以上を指定してください"));
        if (string.IsNullOrWhiteSpace(request.CurrencyCode))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "currency_code は必須です"));
        if (string.IsNullOrWhiteSpace(request.PaymentMethod))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "payment_method は必須です"));

        logger.LogInformation(
            "gRPC ProcessPayment: OrderId={OrderId}, CustomerId={CustomerId}, IdempotencyKey={IdempotencyKey}",
            request.OrderId, request.CustomerId, request.IdempotencyKey);

        var existingPayment = await dbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.OrderId == request.OrderId, ct);

        if (existingPayment is not null)
        {
            logger.LogInformation(
                "重複決済検出（べき等応答）: PaymentId={PaymentId}, OrderId={OrderId}",
                existingPayment.Id, request.OrderId);

            return new ProcessPaymentResponse
            {
                PaymentId = existingPayment.Id,
                StripeCheckoutSessionId = existingPayment.StripeCheckoutSessionId ?? string.Empty,
                CheckoutUrl = string.Empty,
                Status = MapToGrpcStatus(existingPayment.Status)
            };
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var amountDecimal = request.AmountMinorUnits / 100m;

        var payment = new Payment
        {
            OrderId = request.OrderId,
            CustomerId = request.CustomerId,
            Amount = amountDecimal,
            CurrencyCode = request.CurrencyCode,
            PaymentMethod = request.PaymentMethod,
            Status = DomainPaymentStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            await paymentRepository.AddAsync(payment, ct);
            await paymentRepository.SaveChangesAsync(ct);

            var outboxEvent = new OutboxEvent
            {
                EventType = "payment.processing",
                Payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    payment.Id,
                    payment.OrderId,
                    payment.CustomerId,
                    payment.Amount,
                    payment.CurrencyCode
                }),
                CreatedAt = now,
                UpdatedAt = now
            };

            await outboxEventRepository.AddAsync(outboxEvent, ct);
            await outboxEventRepository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            logger.LogError(ex, "gRPC ProcessPayment 失敗: OrderId={OrderId}", request.OrderId);
            throw new RpcException(new Status(StatusCode.Internal, "決済処理に失敗しました"));
        }

        return new ProcessPaymentResponse
        {
            PaymentId = payment.Id,
            StripeCheckoutSessionId = string.Empty,
            CheckoutUrl = string.Empty,
            Status = MapToGrpcStatus(DomainPaymentStatus.Pending)
        };
    }

    public override async Task<RefundPaymentResponse> RefundPayment(
        RefundPaymentRequest request,
        ServerCallContext callContext)
    {
        var ct = callContext.CancellationToken;

        // 入力バリデーション
        if (string.IsNullOrWhiteSpace(request.PaymentId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "payment_id は必須です"));
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "reason は必須です"));

        logger.LogInformation(
            "gRPC RefundPayment: PaymentId={PaymentId}, IdempotencyKey={IdempotencyKey}",
            request.PaymentId, request.IdempotencyKey);

        var payment = await paymentRepository.FindByIdAsync(request.PaymentId, ct);
        if (payment is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "決済が見つかりません"));
        }

        if (payment.Status == DomainPaymentStatus.Refunded)
        {
            logger.LogInformation(
                "既に返金済み（べき等応答）: PaymentId={PaymentId}", request.PaymentId);

            return new RefundPaymentResponse
            {
                RefundId = payment.Id,
                Status = MapToGrpcStatus(DomainPaymentStatus.Refunded),
                Message = "既に返金済みです"
            };
        }

        if (payment.Status != DomainPaymentStatus.Completed)
        {
            throw new RpcException(new Status(
                StatusCode.FailedPrecondition,
                $"決済ステータスが Completed ではありません: {payment.Status}"));
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            payment.MarkAsRefunded();

            payment.AddTransaction(TransactionType.Refund, payment.Amount, "COMPLETED");

            var outboxEvent = new OutboxEvent
            {
                EventType = "payment.refunded",
                Payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    payment.Id,
                    payment.OrderId,
                    payment.Amount,
                    payment.CurrencyCode,
                    Reason = request.Reason
                }),
                CreatedAt = now,
                UpdatedAt = now
            };

            await outboxEventRepository.AddAsync(outboxEvent, ct);
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            logger.LogError(ex, "gRPC RefundPayment 失敗: PaymentId={PaymentId}", request.PaymentId);
            throw new RpcException(new Status(StatusCode.Internal, "返金処理に失敗しました"));
        }

        return new RefundPaymentResponse
        {
            RefundId = payment.Id,
            Status = MapToGrpcStatus(DomainPaymentStatus.Refunded),
            Message = "返金が完了しました"
        };
    }

    private static GrpcPaymentStatus MapToGrpcStatus(DomainPaymentStatus status)
        => status switch
        {
            DomainPaymentStatus.Pending => GrpcPaymentStatus.Pending,
            DomainPaymentStatus.Processing => GrpcPaymentStatus.Processing,
            DomainPaymentStatus.Completed => GrpcPaymentStatus.Completed,
            DomainPaymentStatus.Failed => GrpcPaymentStatus.Failed,
            DomainPaymentStatus.Refunded => GrpcPaymentStatus.Refunded,
            DomainPaymentStatus.Cancelled => GrpcPaymentStatus.Cancelled,
            _ => GrpcPaymentStatus.Unspecified
        };
}
