using SalesManagementService.Infrastructure.Exceptions;
using SalesManagementService.Infrastructure.ExternalServices;
using SalesManagementService.Models;
using SalesManagementService.Repositories.Interfaces;
using SalesManagementService.Services.Interfaces;

namespace SalesManagementService.Infrastructure.Saga;

public record ReturnProcessedEvent(
    string ReturnId, string OrderId, string UserId,
    decimal RefundAmount, string FinalStatus, DateTimeOffset OccurredAt);

public class ReturnSagaCoordinator(
    ISagaLogRepository sagaLogRepository,
    IOrderRepository orderRepository,
    IReturnRepository returnRepository,
    IPaymentClient paymentClient,
    IInventoryClient inventoryClient,
    IPointClient pointClient,
    IOutboxWriter outboxWriter,
    TimeProvider timeProvider,
    ILogger<ReturnSagaCoordinator> logger)
{
    private const int OverallTimeoutSeconds = 60;
    private const int MaxRetries = 3;

    public async Task ExecuteReturnSagaAsync(
        string returnId, string orderId, CancellationToken ct = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(OverallTimeoutSeconds));
        var sagaCt = timeoutCts.Token;

        var returnEntity = await returnRepository.FindTrackedByIdAsync(returnId, ct)
            ?? throw new NotFoundException($"返品が見つかりません: {returnId}");

        var order = await orderRepository.FindByIdWithDetailsAsync(orderId, ct)
            ?? throw new NotFoundException($"注文が見つかりません: {orderId}");

        // Step 1: 返品ステータス検証
        if (returnEntity.Status != "RECEIVED")
            throw new InvalidOrderStateException(
                $"返品ステータス '{returnEntity.Status}' では返品処理を実行できません。RECEIVED である必要があります。");

        var sagaLog = new SagaLog
        {
            SagaType = "ORDER_RETURN",
            UserId = order.CustomerId,
            OrderId = orderId,
            Status = "PROCESSING",
            CurrentStep = 1,
            StartedAt = timeProvider.GetUtcNow(),
            TimeoutAt = timeProvider.GetUtcNow().AddSeconds(OverallTimeoutSeconds)
        };

        await sagaLogRepository.AddAsync(sagaLog, ct);
        await sagaLogRepository.SaveChangesAsync(ct);

        logger.LogInformation("Return Saga 開始: SagaId={SagaId}, ReturnId={ReturnId}, OrderId={OrderId}",
            sagaLog.Id, returnId, orderId);

        try
        {
            // Step 2: 返金処理
            sagaLog.CurrentStep = 2;
            await sagaLogRepository.SaveChangesAsync(ct);

            var refundAmount = returnEntity.RefundAmount;
            var paymentStatus = "REFUNDED";

            // 注文の checkout saga から paymentId を取得
            var checkoutSaga = await sagaLogRepository.FindByOrderIdAsync(orderId, ct);
            var paymentId = checkoutSaga?.StepResults is not null
                ? TryExtractValue(checkoutSaga.StepResults, "paymentId")
                : null;

            try
            {
                if (paymentId is not null)
                {
                    await paymentClient.RefundPaymentAsync(paymentId, refundAmount, sagaCt);
                    logger.LogInformation(
                        "Return Step 2 完了: 返金処理 PaymentId={PaymentId}, Amount={Amount}",
                        paymentId, refundAmount);
                }
                else
                {
                    logger.LogWarning("Return Step 2: PaymentId が見つかりません。手動返金処理が必要です OrderId={OrderId}",
                        orderId);
                    paymentStatus = "REFUND_PENDING";
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Return Step 2 失敗: 返金処理 OrderId={OrderId}", orderId);
                paymentStatus = "REFUND_PENDING";
            }

            // Step 3: 在庫復元（リトライあり）
            sagaLog.CurrentStep = 3;
            await sagaLogRepository.SaveChangesAsync(ct);

            try
            {
                var orderItem = order.Items.FirstOrDefault(i => i.Id == returnEntity.OrderItemId);
                if (orderItem is not null)
                {
                    await ExecuteWithRetryAsync(
                        () => inventoryClient.RestoreInventoryAsync(
                            orderItem.ProductId, returnEntity.Quantity, 5000, sagaCt),
                        "在庫復元", sagaLog, ct);
                    logger.LogInformation(
                        "Return Step 3 完了: 在庫復元 ProductId={ProductId}, Quantity={Quantity}",
                        orderItem.ProductId, returnEntity.Quantity);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Return Step 3 失敗: 在庫復元 ReturnId={ReturnId}", returnId);
            }

            // Step 4: ポイント調整（リトライあり）
            sagaLog.CurrentStep = 4;
            await sagaLogRepository.SaveChangesAsync(ct);

            try
            {
                await ExecuteWithRetryAsync(
                    () => pointClient.AdjustPointsForReturnAsync(
                        order.CustomerId, refundAmount, order.UsedPoints, 5000, sagaCt),
                    "ポイント調整", sagaLog, ct);
                logger.LogInformation("Return Step 4 完了: ポイント調整 UserId={UserId}", order.CustomerId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Return Step 4 失敗: ポイント調整 UserId={UserId}", order.CustomerId);
            }

            // Step 5: 返品/注文ステータス更新 + Outbox 書き込み
            sagaLog.CurrentStep = 5;
            await sagaLogRepository.SaveChangesAsync(ct);

            returnEntity.Status = "REFUNDED";
            returnEntity.RefundedAt = timeProvider.GetUtcNow();
            await returnRepository.SaveChangesAsync(ct);

            var returnEvent = new ReturnProcessedEvent(
                returnId, orderId, order.CustomerId,
                refundAmount, paymentStatus, timeProvider.GetUtcNow());
            await outboxWriter.WriteAsync("order.return.processed", orderId, returnEvent, ct);

            sagaLog.Status = "COMPLETED";
            sagaLog.CompletedAt = timeProvider.GetUtcNow();
            await sagaLogRepository.SaveChangesAsync(ct);

            logger.LogInformation(
                "Return Saga 完了: SagaId={SagaId}, ReturnId={ReturnId}, PaymentStatus={PaymentStatus}",
                sagaLog.Id, returnId, paymentStatus);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            sagaLog.Status = "FAILED";
            sagaLog.LastError = "Return Saga タイムアウト";
            sagaLog.CompletedAt = timeProvider.GetUtcNow();
            await sagaLogRepository.SaveChangesAsync(ct);

            logger.LogError("Return Saga タイムアウト: SagaId={SagaId}, ReturnId={ReturnId}",
                sagaLog.Id, returnId);
            throw new BusinessException("返品処理がタイムアウトしました。しばらく後に再度お試しください。");
        }
        catch (Exception ex) when (ex is not OperationCanceledException
            and not BusinessException and not InvalidOrderStateException and not NotFoundException)
        {
            sagaLog.Status = "FAILED";
            sagaLog.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            sagaLog.CompletedAt = timeProvider.GetUtcNow();
            await sagaLogRepository.SaveChangesAsync(ct);

            logger.LogError(ex, "Return Saga 失敗: SagaId={SagaId}, ReturnId={ReturnId}",
                sagaLog.Id, returnId);
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
