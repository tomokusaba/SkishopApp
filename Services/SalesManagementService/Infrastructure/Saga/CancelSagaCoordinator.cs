using SalesManagementService.Infrastructure.Exceptions;
using SalesManagementService.Infrastructure.ExternalServices;
using SalesManagementService.Models;
using SalesManagementService.Repositories.Interfaces;
using SalesManagementService.Services;
using SalesManagementService.Services.Interfaces;

namespace SalesManagementService.Infrastructure.Saga;

public record OrderCancelledEvent(
    string OrderId, string UserId, string FinalStatus,
    string? PaymentStatus, DateTimeOffset OccurredAt);

public class CancelSagaCoordinator(
    ISagaLogRepository sagaLogRepository,
    IOrderService orderService,
    IOrderRepository orderRepository,
    IInventoryClient inventoryClient,
    ICouponClient couponClient,
    IPointClient pointClient,
    IPaymentClient paymentClient,
    IOutboxWriter outboxWriter,
    TimeProvider timeProvider,
    ILogger<CancelSagaCoordinator> logger) : IOrderCancellationService
{
    private const int OverallTimeoutSeconds = 30;
    private const int MaxRetries = 3;

    public async Task ExecuteCancelSagaAsync(string orderId, string reason, CancellationToken ct = default)
        => await CancelOrderAsync(orderId, reason, ct);

    public async Task CancelOrderAsync(string orderId, string reason, CancellationToken ct = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(OverallTimeoutSeconds));
        var sagaCt = timeoutCts.Token;

        var order = await orderRepository.FindByIdWithDetailsAsync(orderId, ct)
            ?? throw new NotFoundException($"注文が見つかりません: {orderId}");

        // Step 1: 注文ステータス検証
        var cancellableStatuses = new HashSet<string> { "PENDING", "CONFIRMED", "PROCESSING" };
        if (!cancellableStatuses.Contains(order.Status))
            throw new InvalidOrderStateException(
                $"注文ステータス '{order.Status}' はキャンセルできません");

        var sagaLog = new SagaLog
        {
            SagaType = "ORDER_CANCEL",
            UserId = order.CustomerId,
            OrderId = orderId,
            Status = "PROCESSING",
            CurrentStep = 1,
            StartedAt = timeProvider.GetUtcNow(),
            TimeoutAt = timeProvider.GetUtcNow().AddSeconds(OverallTimeoutSeconds)
        };

        await sagaLogRepository.AddAsync(sagaLog, ct);
        await sagaLogRepository.SaveChangesAsync(ct);

        logger.LogInformation("Cancel Saga 開始: SagaId={SagaId}, OrderId={OrderId}, CurrentStatus={Status}",
            sagaLog.Id, orderId, order.Status);

        var existingCheckoutSaga = await sagaLogRepository.FindByOrderIdAsync(orderId, ct);
        var reservationId = existingCheckoutSaga?.StepResults is not null
            ? TryExtractValue(existingCheckoutSaga.StepResults, "reservationId")
            : null;

        try
        {
            // Step 2: 在庫解放（リトライあり）
            sagaLog.CurrentStep = 2;
            await sagaLogRepository.SaveChangesAsync(ct);

            if (reservationId is not null)
            {
                await ExecuteWithRetryAsync(
                    () => inventoryClient.ReleaseReservationAsync(reservationId, 5000, sagaCt),
                    "在庫解放", sagaLog, ct);
                logger.LogInformation("Cancel Step 2 完了: 在庫解放 ReservationId={ReservationId}", reservationId);
            }

            // Step 3: クーポン解放（リトライあり）
            sagaLog.CurrentStep = 3;
            await sagaLogRepository.SaveChangesAsync(ct);

            if (!string.IsNullOrWhiteSpace(order.CouponCode))
            {
                await ExecuteWithRetryAsync(
                    () => couponClient.ReleaseCouponAsync(orderId, 5000, sagaCt),
                    "クーポン解放", sagaLog, ct);
                logger.LogInformation("Cancel Step 3 完了: クーポン解放 OrderId={OrderId}", orderId);
            }

            // Step 4: ポイント解放（リトライあり）
            sagaLog.CurrentStep = 4;
            await sagaLogRepository.SaveChangesAsync(ct);

            if (order.UsedPoints > 0)
            {
                var pointReservationId = existingCheckoutSaga?.StepResults is not null
                    ? TryExtractValue(existingCheckoutSaga.StepResults, "pointReservationId")
                    : null;

                if (pointReservationId is not null)
                {
                    await ExecuteWithRetryAsync(
                        () => pointClient.ReleasePointsAsync(pointReservationId, 5000, sagaCt),
                        "ポイント解放", sagaLog, ct);
                    logger.LogInformation("Cancel Step 4 完了: ポイント解放 ReservationId={ReservationId}",
                        pointReservationId);
                }
            }

            // Step 5: 決済返金/キャンセル
            sagaLog.CurrentStep = 5;
            await sagaLogRepository.SaveChangesAsync(ct);

            var finalPaymentStatus = order.PaymentStatus;
            try
            {
                if (order.PaymentStatus == "CAPTURED")
                {
                    var paymentId = existingCheckoutSaga?.StepResults is not null
                        ? TryExtractValue(existingCheckoutSaga.StepResults, "paymentId")
                        : null;

                    if (paymentId is not null)
                    {
                        await paymentClient.RefundPaymentAsync(paymentId, order.TotalAmount, sagaCt);
                        finalPaymentStatus = "REFUNDED";
                        logger.LogInformation("Cancel Step 5 完了: 返金処理 PaymentId={PaymentId}", paymentId);
                    }
                }
                else if (order.PaymentStatus == "AUTHORIZED")
                {
                    finalPaymentStatus = "FAILED";
                    logger.LogInformation("Cancel Step 5 完了: 決済キャンセル（AUTHORIZED → FAILED）OrderId={OrderId}",
                        orderId);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Cancel Step 5 失敗: 返金処理 OrderId={OrderId}", orderId);
                finalPaymentStatus = "REFUND_PENDING";
            }

            // Step 6: 注文ステータス更新 + Outbox 書き込み
            sagaLog.CurrentStep = 6;
            await sagaLogRepository.SaveChangesAsync(ct);

            var finalStatus = finalPaymentStatus == "REFUNDED" ? "REFUNDED" : "CANCELLED";
            await orderService.CancelOrderAsync(orderId, reason, ct);

            if (finalStatus == "REFUNDED")
            {
                await orderService.UpdateStatusAsync(orderId, "REFUNDED", ct);
            }

            var cancelEvent = new OrderCancelledEvent(
                orderId, order.CustomerId, finalStatus, finalPaymentStatus, timeProvider.GetUtcNow());
            await outboxWriter.WriteAsync("order.cancelled", orderId, cancelEvent, ct);

            sagaLog.Status = "COMPLETED";
            sagaLog.CompletedAt = timeProvider.GetUtcNow();
            await sagaLogRepository.SaveChangesAsync(ct);

            logger.LogInformation("Cancel Saga 完了: SagaId={SagaId}, OrderId={OrderId}, FinalStatus={Status}",
                sagaLog.Id, orderId, finalStatus);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            sagaLog.Status = "FAILED";
            sagaLog.LastError = "Cancel Saga タイムアウト";
            sagaLog.CompletedAt = timeProvider.GetUtcNow();
            await sagaLogRepository.SaveChangesAsync(ct);

            logger.LogError("Cancel Saga タイムアウト: SagaId={SagaId}, OrderId={OrderId}", sagaLog.Id, orderId);
            throw new BusinessException("キャンセル処理がタイムアウトしました。しばらく後に再度お試しください。");
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not BusinessException
            and not InvalidOrderStateException and not NotFoundException)
        {
            sagaLog.Status = "FAILED";
            sagaLog.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            sagaLog.CompletedAt = timeProvider.GetUtcNow();
            await sagaLogRepository.SaveChangesAsync(ct);

            logger.LogError(ex, "Cancel Saga 失敗: SagaId={SagaId}, OrderId={OrderId}", sagaLog.Id, orderId);
            throw;
        }
    }

    private async Task ExecuteWithRetryAsync(
        Func<Task> action, string stepName, SagaLog sagaLog, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                sagaLog.RetryCount++;
                logger.LogWarning(ex, "{StepName} リトライ {Attempt}/{MaxRetries}: {Message}",
                    stepName, attempt, MaxRetries, ex.Message);

                if (attempt == MaxRetries)
                    throw;

                var backoffMs = (int)Math.Pow(2, attempt) * 100;
                await Task.Delay(backoffMs, ct);
            }
        }
    }

    private string? TryExtractValue(string json, string key)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(key, out var value)
                ? value.GetString()
                : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to extract value for key: {Key}", key);
            return null;
        }
    }
}
