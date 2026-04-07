using System.Text.Json;
using Microsoft.Extensions.Options;
using SalesManagementService.Configurations;
using SalesManagementService.DTOs.Requests;
using SalesManagementService.DTOs.Responses;
using SalesManagementService.Infrastructure.Exceptions;
using SalesManagementService.Infrastructure.ExternalServices;
using SalesManagementService.Models;
using SalesManagementService.Repositories.Interfaces;
using SalesManagementService.Services.Interfaces;

namespace SalesManagementService.Infrastructure.Saga;

public record OrderCreatedEvent(
    string OrderId, string OrderNumber, string UserId,
    decimal TotalAmount, string PaymentStatus, DateTimeOffset OccurredAt);

public class SagaCoordinator(
    ICartClient cartClient,
    IInventoryClient inventoryClient,
    ICouponClient couponClient,
    IPointClient pointClient,
    IPaymentClient paymentClient,
    IOrderService orderService,
    IOutboxWriter outboxWriter,
    ISagaLogRepository sagaLogRepository,
    IIdempotencyKeyRepository idempotencyKeyRepository,
    TimeProvider timeProvider,
    IOptions<SagaSettings> sagaOptions,
    ILogger<SagaCoordinator> logger) : ISagaCoordinator, IOrderCheckoutService
{
    private readonly SagaSettings _settings = sagaOptions.Value;

    public async Task<OrderDetailDto> ExecuteCheckoutSagaAsync(
        OrderCreateRequest request, string userId, string idempotencyKey, CancellationToken ct = default)
        => await ExecuteCheckoutAsync(request, userId, idempotencyKey, ct);

    public async Task<OrderDetailDto> ExecuteCheckoutAsync(
        OrderCreateRequest request, string userId, string idempotencyKey, CancellationToken ct = default)
    {
        var existingRequest = await idempotencyKeyRepository.FindByKeyAndUserIdAsync(idempotencyKey, userId, ct);
        if (existingRequest is not null)
        {
            if (existingRequest.RequestStatus == "COMPLETED" && existingRequest.ResponseBody is not null)
            {
                var cachedResponse = JsonSerializer.Deserialize<OrderDetailDto>(existingRequest.ResponseBody);
                if (cachedResponse is not null)
                {
                    logger.LogInformation(
                        "Idempotent request replay detected: UserId={UserId}, Key={IdempotencyKey}",
                        userId, idempotencyKey);
                    return cachedResponse;
                }
            }

            throw new BusinessException("同じ Idempotency-Key のリクエストが既に処理中です。");
        }

        var idempotencyRequest = new IdempotencyKey
        {
            Key = idempotencyKey,
            UserId = userId,
            RequestStatus = "PROCESSING",
            CreatedAt = timeProvider.GetUtcNow(),
            ExpiresAt = timeProvider.GetUtcNow().AddHours(24)
        };
        await idempotencyKeyRepository.AddAsync(idempotencyRequest, ct);
        await idempotencyKeyRepository.SaveChangesAsync(ct);

        var sagaLog = new SagaLog
        {
            SagaType = "ORDER_CHECKOUT",
            UserId = userId,
            OrderId = string.Empty,
            Status = "PROCESSING",
            CurrentStep = 0,
            StartedAt = timeProvider.GetUtcNow(),
            TimeoutAt = timeProvider.GetUtcNow().AddMilliseconds(_settings.SloDeadlineMs)
        };

        await sagaLogRepository.AddAsync(sagaLog, ct);
        await sagaLogRepository.SaveChangesAsync(ct);

        var sagaContext = new SagaContext { UserId = userId, IdempotencyKey = idempotencyKey };

        using var sloLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        sloLinkedCts.CancelAfter(_settings.SloDeadlineMs);
        var sloCt = sloLinkedCts.Token;

        try
        {
            // Step 1: カート取得
            sagaLog.CurrentStep = 1;
            await sagaLogRepository.SaveChangesAsync(ct);

            var cartResponse = await cartClient.GetCartAsync(userId, 200, sloCt);
            sagaContext.CartItems = cartResponse.Items;
            logger.LogInformation("Saga Step 1 完了: カート取得 UserId={UserId}, ItemCount={ItemCount}",
                userId, cartResponse.Items.Count);

            if (cartResponse.Items.Count == 0)
                throw new BusinessException("カートが空です");

            // Step 2: 在庫予約
            sagaLog.CurrentStep = 2;
            await sagaLogRepository.SaveChangesAsync(ct);

            var reserveResponse = await inventoryClient.ReserveInventoryAsync(sagaContext.CartItems, 500, sloCt);
            sagaContext.ReservationId = reserveResponse.ReservationId;
            await PersistStepResultsAsync(sagaLog, sagaContext, ct);
            logger.LogInformation("Saga Step 2 完了: 在庫予約 ReservationId={ReservationId}",
                reserveResponse.ReservationId);

            // Step 3: クーポン検証（指定がある場合のみ）
            sagaLog.CurrentStep = 3;
            await sagaLogRepository.SaveChangesAsync(ct);

            if (!string.IsNullOrWhiteSpace(request.CouponCode))
            {
                var couponResponse = await couponClient.ValidateCouponAsync(
                    request.CouponCode, userId, 300, sloCt);
                sagaContext.CouponDiscount = couponResponse.DiscountAmount;
                sagaContext.CouponId = couponResponse.CouponId;
                await PersistStepResultsAsync(sagaLog, sagaContext, ct);
                logger.LogInformation("Saga Step 3 完了: クーポン検証 CouponId={CouponId}, Discount={Discount}",
                    couponResponse.CouponId, couponResponse.DiscountAmount);
            }

            // Step 4: ポイント予約（使用する場合のみ）
            sagaLog.CurrentStep = 4;
            await sagaLogRepository.SaveChangesAsync(ct);

            if (request.UsedPoints > 0)
            {
                var pointResponse = await pointClient.ReservePointsAsync(
                    userId, request.UsedPoints, 300, sloCt);
                sagaContext.PointReservationId = pointResponse.ReservationId;
                await PersistStepResultsAsync(sagaLog, sagaContext, ct);
                logger.LogInformation("Saga Step 4 完了: ポイント予約 ReservationId={ReservationId}",
                    pointResponse.ReservationId);
            }

            // Step 5: 注文作成（ローカル TX）
            sagaLog.CurrentStep = 5;
            await sagaLogRepository.SaveChangesAsync(ct);

            var orderDetail = await orderService.CreateOrderAsync(request, sagaContext, sloCt);
            sagaContext.OrderId = orderDetail.Id;
            sagaLog.OrderId = orderDetail.Id;
            await PersistStepResultsAsync(sagaLog, sagaContext, ct);

            logger.LogInformation("Saga Step 5 完了: 注文作成 OrderId={OrderId}", orderDetail.Id);

            // Step 6: 決済処理
            sagaLog.CurrentStep = 6;
            await sagaLogRepository.SaveChangesAsync(ct);

            try
            {
                var paymentResult = await paymentClient.ProcessPaymentAsync(
                    orderDetail.TotalAmount, request.PaymentMethod, sloCt);
                sagaContext.PaymentId = paymentResult.PaymentId;
                await PersistStepResultsAsync(sagaLog, sagaContext, ct);
                logger.LogInformation("Saga Step 6 完了: 決済処理 PaymentId={PaymentId}, Status={Status}",
                    paymentResult.PaymentId, paymentResult.Status);
            }
            catch (OperationCanceledException) when (sloCt.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                sagaLog.Status = "PENDING_PAYMENT";
                sagaLog.LastError = "決済処理タイムアウト — 非同期で確認待ち";
                await sagaLogRepository.SaveChangesAsync(ct);

                await orderService.UpdateStatusAsync(orderDetail.Id, "PENDING_PAYMENT", ct);
                logger.LogWarning("Saga Step 6 タイムアウト: 決済 PENDING_PAYMENT OrderId={OrderId}", orderDetail.Id);

                throw new PaymentPendingException(
                    $"決済処理がタイムアウトしました。注文 {orderDetail.OrderNumber} は決済確認待ちです。");
            }

            // ── 後処理ステップ（補償対象外） ──

            // Step 7: ポイント付与
            sagaLog.CurrentStep = 7;
            await sagaLogRepository.SaveChangesAsync(ct);

            try
            {
                await pointClient.AwardPointsAsync(userId, orderDetail.TotalAmount, 300, ct);
                logger.LogInformation("Saga Step 7 完了: ポイント付与 UserId={UserId}", userId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Saga Step 7 失敗（後処理）: ポイント付与 UserId={UserId}", userId);
            }

            // Step 8: カートクリア
            sagaLog.CurrentStep = 8;
            await sagaLogRepository.SaveChangesAsync(ct);

            try
            {
                await cartClient.ClearCartAsync(userId, 200, ct);
                logger.LogInformation("Saga Step 8 完了: カートクリア UserId={UserId}", userId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Saga Step 8 失敗（後処理）: カートクリア UserId={UserId}", userId);
            }

            // Step 9: Outbox 書き込み
            sagaLog.CurrentStep = 9;
            await sagaLogRepository.SaveChangesAsync(ct);

            try
            {
                var orderCreatedEvent = new OrderCreatedEvent(
                    orderDetail.Id, orderDetail.OrderNumber, userId,
                    orderDetail.TotalAmount, orderDetail.PaymentStatus, timeProvider.GetUtcNow());
                await outboxWriter.WriteAsync("order.created", orderDetail.Id, orderCreatedEvent, ct);
                idempotencyRequest.RequestStatus = "COMPLETED";
                idempotencyRequest.ResponseStatus = StatusCodes.Status201Created;
                idempotencyRequest.ResponseBody = JsonSerializer.Serialize(orderDetail);
                await idempotencyKeyRepository.SaveChangesAsync(ct);
                logger.LogInformation("Saga Step 9 完了: Outbox 書き込み OrderId={OrderId}", orderDetail.Id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Saga Step 9 失敗（後処理）: Outbox 書き込み OrderId={OrderId}", sagaContext.OrderId);
            }

            // Saga 完了
            sagaLog.Status = "COMPLETED";
            sagaLog.CompletedAt = timeProvider.GetUtcNow();
            await sagaLogRepository.SaveChangesAsync(ct);

            logger.LogInformation("Checkout Saga 完了: SagaId={SagaId}, OrderId={OrderId}",
                sagaLog.Id, orderDetail.Id);

            return orderDetail;
        }
        catch (PaymentPendingException)
        {
            idempotencyRequest.RequestStatus = "COMPLETED";
            idempotencyRequest.ResponseStatus = StatusCodes.Status202Accepted;
            idempotencyRequest.ResponseBody = JsonSerializer.Serialize(new
            {
                Message = "お支払い処理中です。確定次第メールでお知らせします。"
            });
            await idempotencyKeyRepository.SaveChangesAsync(ct);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Checkout Saga 失敗 Step={Step}: {Message}",
                sagaLog.CurrentStep, ex.Message);

            sagaLog.Status = "COMPENSATING";
            sagaLog.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            await sagaLogRepository.SaveChangesAsync(ct);

            await CompensateAsync(sagaLog, sagaContext, ct);

            idempotencyRequest.RequestStatus = "PENDING";
            idempotencyRequest.ResponseStatus = null;
            idempotencyRequest.ResponseBody = null;
            await idempotencyKeyRepository.SaveChangesAsync(ct);

            throw;
        }
    }

    public async Task CompensateAsync(SagaLog sagaLog, CancellationToken ct = default)
    {
        await CompensateAsync(sagaLog, RestoreSagaContext(sagaLog), ct);
    }

    private async Task PersistStepResultsAsync(SagaLog sagaLog, SagaContext sagaContext, CancellationToken ct)
    {
        var results = new Dictionary<string, string?>();
        if (sagaContext.ReservationId is not null) results["reservationId"] = sagaContext.ReservationId;
        if (sagaContext.CouponId is not null) results["couponId"] = sagaContext.CouponId;
        if (sagaContext.PointReservationId is not null) results["pointReservationId"] = sagaContext.PointReservationId;
        if (sagaContext.OrderId is not null) results["orderId"] = sagaContext.OrderId;
        if (sagaContext.PaymentId is not null) results["paymentId"] = sagaContext.PaymentId;

        sagaLog.StepResults = JsonSerializer.Serialize(results);
        await sagaLogRepository.SaveChangesAsync(ct);
    }

    private static SagaContext RestoreSagaContext(SagaLog sagaLog)
    {
        var context = new SagaContext { UserId = sagaLog.UserId, OrderId = sagaLog.OrderId };
        if (string.IsNullOrWhiteSpace(sagaLog.StepResults))
            return context;

        var values = JsonSerializer.Deserialize<Dictionary<string, string?>>(sagaLog.StepResults);
        if (values is null)
            return context;

        if (values.TryGetValue("reservationId", out var reservationId))
            context.ReservationId = reservationId;
        if (values.TryGetValue("couponId", out var couponId))
            context.CouponId = couponId;
        if (values.TryGetValue("pointReservationId", out var pointReservationId))
            context.PointReservationId = pointReservationId;
        if (values.TryGetValue("orderId", out var orderId) && !string.IsNullOrWhiteSpace(orderId))
            context.OrderId = orderId;
        if (values.TryGetValue("paymentId", out var paymentId))
            context.PaymentId = paymentId;

        return context;
    }

    private async Task CompensateAsync(SagaLog sagaLog, SagaContext sagaContext, CancellationToken ct)
    {
        using var compensationCts = new CancellationTokenSource(TimeSpan.FromSeconds(_settings.CompensationTimeoutSeconds));
        var compCt = compensationCts.Token;

        var completedStep = sagaLog.CurrentStep;
        logger.LogInformation("補償トランザクション開始: SagaId={SagaId}, FromStep={Step}",
            sagaLog.Id, completedStep);

        // Step 5 補償: 注文キャンセル
        if (completedStep >= 5 && sagaContext.OrderId is not null)
        {
            try
            {
                await orderService.CancelOrderAsync(sagaContext.OrderId, "Saga 補償によるキャンセル", compCt);
                logger.LogInformation("補償 Step 5: 注文キャンセル完了 OrderId={OrderId}", sagaContext.OrderId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "補償 Step 5 失敗: 注文キャンセル OrderId={OrderId}", sagaContext.OrderId);
            }
        }

        // Step 4 補償: ポイント解放
        if (completedStep >= 4 && sagaContext.PointReservationId is not null)
        {
            try
            {
                await pointClient.ReleasePointsAsync(sagaContext.PointReservationId, 5000, compCt);
                logger.LogInformation("補償 Step 4: ポイント解放完了 ReservationId={ReservationId}",
                    sagaContext.PointReservationId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "補償 Step 4 失敗: ポイント解放 ReservationId={ReservationId}",
                    sagaContext.PointReservationId);
            }
        }

        // Step 3 補償: クーポン解放
        if (completedStep >= 3 && sagaContext.CouponId is not null)
        {
            try
            {
                await couponClient.ReleaseCouponAsync(sagaContext.OrderId, 5000, compCt);
                logger.LogInformation("補償 Step 3: クーポン解放完了 CouponId={CouponId}", sagaContext.CouponId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "補償 Step 3 失敗: クーポン解放 CouponId={CouponId}", sagaContext.CouponId);
            }
        }

        // Step 2 補償: 在庫解放
        if (completedStep >= 2 && sagaContext.ReservationId is not null)
        {
            try
            {
                await inventoryClient.ReleaseReservationAsync(sagaContext.ReservationId, 5000, compCt);
                logger.LogInformation("補償 Step 2: 在庫解放完了 ReservationId={ReservationId}",
                    sagaContext.ReservationId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "補償 Step 2 失敗: 在庫解放 ReservationId={ReservationId}",
                    sagaContext.ReservationId);
            }
        }

        sagaLog.Status = "COMPENSATED";
        sagaLog.CompletedAt = timeProvider.GetUtcNow();
        await sagaLogRepository.SaveChangesAsync(ct);

        logger.LogInformation("補償トランザクション完了: SagaId={SagaId}", sagaLog.Id);
    }
}
