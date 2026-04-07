using System.Text.Json;
using Microsoft.Extensions.Options;
using PaymentCartService.Configurations;
using PaymentCartService.DTOs.Requests;
using PaymentCartService.DTOs.Responses;
using PaymentCartService.Exceptions;
using PaymentCartService.Infrastructure.Persistence;
using PaymentCartService.Models;
using PaymentCartService.Models.Enums;
using PaymentCartService.Repositories.Interfaces;
using PaymentCartService.Services.Interfaces;
using Stripe;
using Stripe.Checkout;

namespace PaymentCartService.Services;

public class PaymentService(
    IPaymentRepository paymentRepository,
    ICartRepository cartRepository,
    IOutboxEventRepository outboxEventRepository,
    IStripeGateway stripeGateway,
    AppDbContext dbContext,
    IOptions<StripeSettings> stripeOptions,
    IOptions<PaymentSettings> paymentOptions,
    TimeProvider timeProvider,
    ILogger<PaymentService> logger) : IPaymentService
{
    private readonly StripeSettings _stripeSettings = stripeOptions.Value;
    private readonly PaymentSettings _paymentSettings = paymentOptions.Value;

    public async Task<PaymentResponse> CheckoutAsync(
        CheckoutRequest request, string userId, CancellationToken ct = default)
    {
        var cart = await cartRepository.FindByIdWithItemsAsync(request.CartId, ct)
            ?? throw new NotFoundException("カートが見つかりません");

        if (cart.Status != CartStatus.Active || !cart.Items.Any())
            throw new BusinessException("カートが空または無効です", "PAY-4001");

        var totalAmount = cart.CalculateTotal();

        Payment? payment = null;  // catch ブロックからアクセス可能にするため外で定義

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            payment = new Payment
            {
                OrderId = Guid.NewGuid().ToString(),
                CustomerId = userId,
                Amount = totalAmount,
                CurrencyCode = _paymentSettings.DefaultCurrency,
                Status = PaymentStatus.Pending,
                PaymentMethod = request.PaymentMethod,
                CreatedBy = userId
            };

            await paymentRepository.AddAsync(payment, ct);

            var sessionOptions = new SessionCreateOptions
            {
                PaymentMethodTypes = ["card"],
                LineItems = cart.Items.Select(item => new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = _paymentSettings.DefaultCurrency.ToLowerInvariant(),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.ProductName,
                        },
                        UnitAmount = (long)(item.UnitPrice * 100),
                    },
                    Quantity = item.Quantity,
                }).ToList(),
                Mode = "payment",
                SuccessUrl = $"{_stripeSettings.SuccessUrl}?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{_stripeSettings.CancelUrl}?session_id={{CHECKOUT_SESSION_ID}}",
                ClientReferenceId = payment.Id,
            };

            var session = await stripeGateway.CreateCheckoutSessionAsync(sessionOptions, ct);

            payment.StripeCheckoutSessionId = session.Id;
            await paymentRepository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            logger.LogInformation(
                "チェックアウトセッション作成: PaymentId={PaymentId}, StripeSessionId={StripeSessionId}",
                payment.Id, session.Id);

            return new PaymentResponse(
                Id: payment.Id,
                OrderId: payment.OrderId,
                CustomerId: payment.CustomerId,
                Amount: payment.Amount,
                CurrencyCode: payment.CurrencyCode,
                Status: payment.Status.ToString().ToUpperInvariant(),
                PaymentMethod: payment.PaymentMethod,
                CheckoutUrl: session.Url,
                CreatedAt: payment.CreatedAt,
                UpdatedAt: payment.UpdatedAt);
        }
        catch (StripeException ex)
        {
            logger.LogWarning("トランザクションロールバック実行: PaymentId={PaymentId}", payment?.Id);
            await transaction.RollbackAsync(ct);
            logger.LogError(ex, "Stripe API エラー: {Message}", ex.Message);
            throw new BusinessException("決済処理に失敗しました", ex, "PAY-4223");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "トランザクションロールバック実行（予期しないエラー）: PaymentId={PaymentId}", payment?.Id);
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<PaymentDetailResponse?> GetByIdAsync(
        string paymentId, string userId, CancellationToken ct = default)
    {
        var payment = await paymentRepository.FindByIdAsync(paymentId, ct);
        if (payment is null) return null;

        if (payment.CustomerId != userId)
            throw new BusinessException("アクセス権限がありません", "PAY-4041");

        return MapToDetailResponse(payment);
    }

    public async Task<PaymentDetailResponse?> GetByOrderIdAsync(
        string orderId, string userId, CancellationToken ct = default)
    {
        var payment = await paymentRepository.FindByOrderIdAsync(orderId, ct);
        if (payment is null) return null;

        if (payment.CustomerId != userId)
            throw new BusinessException("アクセス権限がありません", "PAY-4041");

        return MapToDetailResponse(payment);
    }

    public async Task<PaginatedResponse<PaymentResponse>> GetByCustomerIdAsync(
        string customerId, string userId, int page, int pageSize, CancellationToken ct = default)
    {
        if (customerId != userId)
            throw new BusinessException("アクセス権限がありません", "PAY-4041");

        var (items, totalCount) = await paymentRepository.FindByCustomerIdAsync(
            customerId, page, pageSize, ct);

        var responses = items.Select(p => new PaymentResponse(
            Id: p.Id, OrderId: p.OrderId, CustomerId: p.CustomerId,
            Amount: p.Amount, CurrencyCode: p.CurrencyCode,
            Status: p.Status.ToString().ToUpperInvariant(),
            PaymentMethod: p.PaymentMethod, CheckoutUrl: null,
            CreatedAt: p.CreatedAt, UpdatedAt: p.UpdatedAt
        )).ToList();

        return new PaginatedResponse<PaymentResponse>(
            Items: responses, Page: page, PageSize: pageSize,
            TotalCount: totalCount, TotalPages: (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task HandleWebhookAsync(string json, string signature, CancellationToken ct = default)
    {
        // tolerance は long 型で秒数を直接渡す（_paymentSettings.WebhookToleranceSeconds = 300）
        var stripeEvent = EventUtility.ConstructEvent(
            json,
            signature,
            _stripeSettings.WebhookSecret,
            tolerance: _paymentSettings.WebhookToleranceSeconds,
            throwOnApiVersionMismatch: false);

        logger.LogInformation("Stripe Webhook 受信: EventType={EventType}, EventId={EventId}, Tolerance={ToleranceSeconds}s",
            stripeEvent.Type, stripeEvent.Id, _paymentSettings.WebhookToleranceSeconds);

        switch (stripeEvent.Type)
        {
            case EventTypes.CheckoutSessionCompleted:
                await HandleCheckoutSessionCompletedAsync(stripeEvent, ct);
                break;
            case EventTypes.PaymentIntentPaymentFailed:
                await HandlePaymentFailedAsync(stripeEvent, ct);
                break;
            default:
                logger.LogInformation("未処理の Webhook イベント: {EventType}", stripeEvent.Type);
                break;
        }
    }

    public async Task CompensatePaymentAsync(string paymentId, CancellationToken ct = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

        var payment = await paymentRepository.FindByIdAsync(paymentId, timeoutCts.Token)
            ?? throw new NotFoundException($"決済が見つかりません: {paymentId}");

        if (payment.Status is PaymentStatus.Refunded or PaymentStatus.Cancelled)
        {
            logger.LogInformation("Saga 補償スキップ（既に返金済み）: PaymentId={PaymentId}", paymentId);
            return;
        }

        if (payment.StripePaymentIntentId is not null)
        {
            await stripeGateway.CreateRefundAsync(new RefundCreateOptions
            {
                PaymentIntent = payment.StripePaymentIntentId,
                Reason = "requested_by_customer",
            }, $"saga-refund-{paymentId}", timeoutCts.Token);
        }

        payment.MarkAsRefunded();
        await paymentRepository.SaveChangesAsync(timeoutCts.Token);

        logger.LogInformation("Saga 補償完了（返金）: PaymentId={PaymentId}", paymentId);
    }

    public async Task<PaymentResponse> GuestCheckoutAsync(
        string cartId, GuestCheckoutRequest request, CancellationToken ct = default)
    {
        var cart = await cartRepository.FindByIdWithItemsAsync(cartId, ct)
            ?? throw new NotFoundException("カートが見つかりません");

        if (cart.Status != CartStatus.Active || !cart.Items.Any())
            throw new BusinessException("カートが空または無効です", "PAY-4001");

        var totalAmount = cart.CalculateTotal();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            var payment = new Payment
            {
                OrderId = Guid.NewGuid().ToString(),
                CustomerId = "guest",
                Amount = totalAmount,
                CurrencyCode = _paymentSettings.DefaultCurrency,
                Status = PaymentStatus.Pending,
                PaymentMethod = request.PaymentMethod,
                CreatedBy = "guest"
            };

            await paymentRepository.AddAsync(payment, ct);

            var sessionOptions = new SessionCreateOptions
            {
                PaymentMethodTypes = ["card"],
                LineItems = cart.Items.Select(item => new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = _paymentSettings.DefaultCurrency.ToLowerInvariant(),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.ProductName,
                        },
                        UnitAmount = (long)(item.UnitPrice * 100),
                    },
                    Quantity = item.Quantity,
                }).ToList(),
                Mode = "payment",
                CustomerEmail = request.Email,
                SuccessUrl = $"{_stripeSettings.SuccessUrl}?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{_stripeSettings.CancelUrl}?session_id={{CHECKOUT_SESSION_ID}}",
                ClientReferenceId = payment.Id,
            };

            var session = await stripeGateway.CreateCheckoutSessionAsync(sessionOptions, ct);

            payment.StripeCheckoutSessionId = session.Id;
            await paymentRepository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            logger.LogInformation(
                "ゲストチェックアウトセッション作成: PaymentId={PaymentId}, StripeSessionId={StripeSessionId}",
                payment.Id, session.Id);

            return new PaymentResponse(
                Id: payment.Id,
                OrderId: payment.OrderId,
                CustomerId: payment.CustomerId,
                Amount: payment.Amount,
                CurrencyCode: payment.CurrencyCode,
                Status: payment.Status.ToString().ToUpperInvariant(),
                PaymentMethod: payment.PaymentMethod,
                CheckoutUrl: session.Url,
                CreatedAt: payment.CreatedAt,
                UpdatedAt: payment.UpdatedAt);
        }
        catch (StripeException ex)
        {
            await transaction.RollbackAsync(ct);
            logger.LogError(ex, "Stripe API エラー（ゲスト）: {Message}", ex.Message);
            throw new BusinessException("決済処理に失敗しました", ex, "PAY-4223");
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private async Task HandleCheckoutSessionCompletedAsync(Event stripeEvent, CancellationToken ct)
    {
        var session = stripeEvent.Data.Object as Session
            ?? throw new BusinessException("Stripe Session の解析に失敗しました");

        var payment = await paymentRepository.FindByStripeCheckoutSessionIdAsync(session.Id, ct)
            ?? throw new NotFoundException($"決済が見つかりません: StripeSessionId={session.Id}");

        var now = timeProvider.GetUtcNow().UtcDateTime;

        await using var dbTransaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            payment.MarkAsCompleted(session.PaymentIntentId, null, now);

            payment.AddTransaction(
                TransactionType.Charge,
                payment.Amount,
                "COMPLETED",
                JsonSerializer.Serialize(new { session.Id, session.PaymentIntentId }));

            await outboxEventRepository.AddAsync(new OutboxEvent
            {
                EventType = "payment.completed",
                AggregateId = payment.Id,
                Payload = JsonSerializer.Serialize(new
                {
                    PaymentId = payment.Id,
                    OrderId = payment.OrderId,
                    CustomerId = payment.CustomerId,
                    Amount = payment.Amount,
                    CurrencyCode = payment.CurrencyCode,
                    PaidAt = now
                }),
                CreatedAt = now
            }, ct);

            await paymentRepository.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);

            logger.LogInformation("決済完了: PaymentId={PaymentId}, OrderId={OrderId}",
                payment.Id, payment.OrderId);
        }
        catch
        {
            await dbTransaction.RollbackAsync(ct);
            throw;
        }
    }

    private async Task HandlePaymentFailedAsync(Event stripeEvent, CancellationToken ct)
    {
        var paymentIntent = stripeEvent.Data.Object as PaymentIntent
            ?? throw new BusinessException("Stripe PaymentIntent の解析に失敗しました");

        var payment = await paymentRepository.FindByStripePaymentIntentIdAsync(paymentIntent.Id, ct);
        if (payment is null)
        {
            logger.LogWarning("対応する決済が見つかりません: StripePaymentIntentId={IntentId}", paymentIntent.Id);
            return;
        }

        payment.MarkAsFailed(
            paymentIntent.LastPaymentError?.Code ?? "UNKNOWN",
            paymentIntent.LastPaymentError?.Message ?? "決済に失敗しました");

        await outboxEventRepository.AddAsync(new OutboxEvent
        {
            EventType = "payment.failed",
            AggregateId = payment.Id,
            Payload = JsonSerializer.Serialize(new
            {
                PaymentId = payment.Id,
                OrderId = payment.OrderId,
                FailureCode = payment.FailureCode,
                FailureMessage = payment.FailureMessage
            })
        }, ct);

        await paymentRepository.SaveChangesAsync(ct);

        logger.LogWarning("決済失敗: PaymentId={PaymentId}, FailureCode={FailureCode}",
            payment.Id, payment.FailureCode);
    }

    private static PaymentDetailResponse MapToDetailResponse(Payment payment) => new(
        Id: payment.Id, OrderId: payment.OrderId, CustomerId: payment.CustomerId,
        Amount: payment.Amount, CurrencyCode: payment.CurrencyCode,
        Status: payment.Status.ToString().ToUpperInvariant(),
        PaymentMethod: payment.PaymentMethod,
        StripeCheckoutSessionId: payment.StripeCheckoutSessionId,
        StripePaymentIntentId: payment.StripePaymentIntentId,
        PaidAt: payment.PaidAt, CreatedAt: payment.CreatedAt, UpdatedAt: payment.UpdatedAt);
}
