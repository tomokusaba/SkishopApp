using System.Text.Json;
using PaymentCartService.DTOs.Requests;
using PaymentCartService.DTOs.Responses;
using PaymentCartService.Exceptions;
using PaymentCartService.Infrastructure.Persistence;
using PaymentCartService.Models;
using PaymentCartService.Models.Enums;
using PaymentCartService.Repositories.Interfaces;
using PaymentCartService.Services.Interfaces;
using Stripe;

namespace PaymentCartService.Services;

public class RefundService(
    IPaymentRepository paymentRepository,
    IOutboxEventRepository outboxEventRepository,
    IStripeGateway stripeGateway,
    AppDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<RefundService> logger) : IRefundService
{
    public async Task<RefundResponse> RefundAsync(
        string paymentId, RefundRequest request, string adminUserId, CancellationToken ct = default)
    {
        var payment = await paymentRepository.FindByIdAsync(paymentId, ct)
            ?? throw new NotFoundException($"決済が見つかりません: {paymentId}");

        if (payment.Status != PaymentStatus.Completed)
            throw new BusinessException("完了済みの決済のみ返金可能です", "PAY-4222");

        if (request.Amount > payment.Amount)
            throw new BusinessException("返金金額が決済金額を超過しています", "PAY-4222");

        var now = timeProvider.GetUtcNow().UtcDateTime;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            Stripe.Refund? stripeRefund = null;
            if (payment.StripePaymentIntentId is not null)
            {
                stripeRefund = await stripeGateway.CreateRefundAsync(new RefundCreateOptions
                {
                    PaymentIntent = payment.StripePaymentIntentId,
                    Amount = (long)(request.Amount * 100),
                    Reason = "requested_by_customer",
                }, $"refund-{paymentId}-{now.Ticks}", ct);
            }

            payment.MarkAsRefunded();
            payment.UpdatedBy = adminUserId;

            var gatewayResponse = stripeRefund is not null
                ? JsonSerializer.Serialize(new { stripeRefund.Id, stripeRefund.Status })
                : null;

            payment.AddTransaction(TransactionType.Refund, request.Amount, "COMPLETED", gatewayResponse);

            await outboxEventRepository.AddAsync(new OutboxEvent
            {
                EventType = "payment.refunded",
                AggregateId = payment.Id,
                Payload = JsonSerializer.Serialize(new
                {
                    PaymentId = payment.Id,
                    OrderId = payment.OrderId,
                    RefundAmount = request.Amount,
                    Reason = request.Reason,
                    RefundedAt = now
                }),
                CreatedAt = now
            }, ct);

            await paymentRepository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            logger.LogInformation(
                "返金完了: PaymentId={PaymentId}, RefundAmount={RefundAmount}, AdminUserId={AdminUserId}",
                paymentId, request.Amount, adminUserId);

            var lastTransaction = payment.Transactions.Last();

            return new RefundResponse(
                Id: lastTransaction.Id,
                PaymentId: payment.Id,
                RefundAmount: request.Amount,
                Status: "COMPLETED",
                CreatedAt: now);
        }
        catch (StripeException ex)
        {
            await transaction.RollbackAsync(ct);
            logger.LogError(ex, "Stripe 返金エラー: PaymentId={PaymentId}", paymentId);
            throw new BusinessException("返金処理に失敗しました", ex, "PAY-4222");
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
