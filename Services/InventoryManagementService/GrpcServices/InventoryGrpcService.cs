using Grpc.Core;
using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.DTOs.Responses;
using InventoryManagementService.Exceptions;
using InventoryManagementService.Protos;
using InventoryManagementService.Services.Interfaces;

namespace InventoryManagementService.GrpcServices;

/// <summary>
/// 在庫管理 gRPC サービス実装。
/// Saga オーケストレーション（在庫予約・解放）と在庫照会・リアルタイム通知を提供する。
/// </summary>
public class InventoryGrpcService(
    IInventoryService inventoryService,
    IPriceService priceService,
    TimeProvider timeProvider,
    ILogger<InventoryGrpcService> logger)
    : Protos.InventoryService.InventoryServiceBase
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Saga ステップ 2: 注文に対して在庫を予約する。
    /// 在庫不足の場合は success=false とエラーメッセージを返す。
    /// </summary>
    /// <param name="request">在庫予約リクエスト（注文ID と予約対象の商品・数量リスト）</param>
    /// <param name="context">gRPC サーバーコールコンテキスト（CancellationToken を含む）</param>
    /// <returns>予約結果。成功時は ReservationId を、在庫不足時は success=false とエラーメッセージを返す</returns>
    public override async Task<ReserveInventoryResponse> ReserveInventory(
        ReserveInventoryRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;

        logger.LogInformation("在庫予約リクエスト受信: OrderId={OrderId}, ItemCount={ItemCount}",
            request.OrderId, request.Items.Count);

        var items = request.Items
            .Select(i => new ReserveItemDto(i.ProductId, i.Quantity))
            .ToList();

        try
        {
            var reservationId = await inventoryService.ReserveAsync(request.OrderId, items, ct);

            logger.LogInformation("在庫予約成功: OrderId={OrderId}, ReservationId={ReservationId}",
                request.OrderId, reservationId);

            return new ReserveInventoryResponse
            {
                Success = true,
                ReservationId = reservationId
            };
        }
        catch (InventoryException ex)
        {
            logger.LogWarning(ex, "在庫予約失敗（在庫不足）: OrderId={OrderId}, ErrorCode={ErrorCode}",
                request.OrderId, ex.ErrorCode);

            return new ReserveInventoryResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Saga 補償: 予約済みの在庫を解放する（注文キャンセル時）。
    /// </summary>
    /// <param name="request">在庫解放リクエスト（注文ID、予約ID、解放対象の商品・数量リスト）</param>
    /// <param name="context">gRPC サーバーコールコンテキスト（CancellationToken を含む）</param>
    /// <returns>解放結果。失敗時は success=false とエラーメッセージを返す</returns>
    public override async Task<ReleaseReservationResponse> ReleaseReservation(
        ReleaseReservationRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;

        logger.LogInformation("在庫解放リクエスト受信: OrderId={OrderId}, ReservationId={ReservationId}",
            request.OrderId, request.ReservationId);

        var items = request.Items
            .Select(i => new ReserveItemDto(i.ProductId, i.Quantity))
            .ToList();

        try
        {
            await inventoryService.ReleaseAsync(request.OrderId, request.ReservationId, items, ct);

            logger.LogInformation("在庫解放成功: OrderId={OrderId}, ReservationId={ReservationId}",
                request.OrderId, request.ReservationId);

            return new ReleaseReservationResponse { Success = true };
        }
        catch (InventoryException ex)
        {
            logger.LogWarning(ex, "在庫解放失敗: OrderId={OrderId}, ErrorCode={ErrorCode}",
                request.OrderId, ex.ErrorCode);

            return new ReleaseReservationResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// 単一商品の在庫状況を確認する。
    /// 商品が存在しない場合は gRPC NotFound ステータスをスローする。
    /// </summary>
    /// <param name="request">在庫確認リクエスト（商品ID と要求数量）</param>
    /// <param name="context">gRPC サーバーコールコンテキスト（CancellationToken を含む）</param>
    /// <returns>在庫状況（利用可能数量、在庫有無、要求数量の充足可否）</returns>
    public override async Task<StockResponse> CheckStock(
        CheckStockRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;

        // 入力バリデーション
        if (string.IsNullOrWhiteSpace(request.ProductId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "product_id は必須です"));
        if (request.RequiredQuantity <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument,
                "required_quantity は 1 以上を指定してください"));

        logger.LogDebug("在庫確認リクエスト: ProductId={ProductId}, RequiredQuantity={RequiredQuantity}",
            request.ProductId, request.RequiredQuantity);

        var inventory = await inventoryService.GetByProductIdAsync(request.ProductId, ct)
            ?? throw new RpcException(new Status(StatusCode.NotFound,
                $"在庫情報が見つかりません (ProductId: {request.ProductId})"));

        return BuildStockResponse(inventory, request.RequiredQuantity);
    }

    /// <summary>
    /// 複数商品の在庫状況を一括で確認する。
    /// 存在しない商品は結果から除外し、全商品の充足可否を判定する。
    /// </summary>
    /// <param name="request">一括在庫確認リクエスト（商品ID と要求数量のペアのリスト）</param>
    /// <param name="context">gRPC サーバーコールコンテキスト（CancellationToken を含む）</param>
    /// <returns>各商品の在庫状況リストと全商品の充足可否フラグ</returns>
    public override async Task<StockBatchResponse> CheckStockBatch(
        CheckStockBatchRequest request, ServerCallContext context)
    {
        var ct = context.CancellationToken;

        // 入力バリデーション
        if (request.Items.Count == 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "items は 1 件以上を指定してください"));
        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.ProductId))
                throw new RpcException(new Status(StatusCode.InvalidArgument,
                    "items 内の product_id は必須です"));
            if (item.RequiredQuantity <= 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument,
                    $"product_id={item.ProductId} の required_quantity は 1 以上を指定してください"));
        }

        logger.LogDebug("一括在庫確認リクエスト: ItemCount={ItemCount}", request.Items.Count);

        var productIds = request.Items.Select(i => i.ProductId).ToList();
        var inventories = await inventoryService.GetByProductIdsAsync(productIds, ct);
        var inventoryMap = inventories.ToDictionary(i => i.ProductId);

        var response = new StockBatchResponse();
        var allAvailable = true;

        foreach (var item in request.Items)
        {
            if (inventoryMap.TryGetValue(item.ProductId, out var inventory))
            {
                var stockResponse = BuildStockResponse(inventory, item.RequiredQuantity);
                response.Items.Add(stockResponse);

                if (!stockResponse.CanFulfillRequest)
                {
                    allAvailable = false;
                }
            }
            else
            {
                response.Items.Add(new StockResponse
                {
                    ProductId = item.ProductId,
                    AvailableQuantity = 0,
                    IsInStock = false,
                    CanFulfillRequest = false
                });
                allAvailable = false;
            }
        }

        response.AllItemsAvailable = allAvailable;
        return response;
    }

    /// <summary>
    /// 在庫変動のリアルタイム通知をサーバーストリーミングで提供する。
    /// 指定された商品 ID の在庫状況を定期的にポーリングし、変動があった場合にクライアントへ通知する。
    /// クライアント切断時に自動的にストリームを終了する。
    /// </summary>
    /// <param name="request">サブスクリプションリクエスト（監視対象の商品 ID リスト、価格更新通知の有効化フラグ）</param>
    /// <param name="responseStream">クライアントへの StockUpdate 書き込みストリーム</param>
    /// <param name="context">gRPC サーバーコールコンテキスト（CancellationToken を含む）</param>
    /// <remarks>
    /// ポーリング間隔は <see cref="PollingInterval"/>（5 秒）。
    /// 在庫数量の変動検出時に <see cref="DetermineUpdateType"/> で変動タイプを判定し通知する。
    /// IncludePriceUpdates=true の場合、N+1 防止のため全商品の価格をバッチ取得して比較する。
    /// </remarks>
    public override async Task SubscribeStockUpdates(
        StockSubscriptionRequest request,
        IServerStreamWriter<StockUpdate> responseStream,
        ServerCallContext context)
    {
        var ct = context.CancellationToken;
        var productIds = request.ProductIds.ToList();

        logger.LogInformation("在庫変動通知サブスクリプション開始: ProductIds={ProductIds}, IncludePriceUpdates={IncludePriceUpdates}",
            string.Join(",", productIds), request.IncludePriceUpdates);

        var previousQuantities = new Dictionary<string, int>();
        var previousPrices = new Dictionary<string, long>();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var inventories = await inventoryService.GetByProductIdsAsync(productIds, ct);

                // バッチ取得: 全商品の価格を事前にフェッチ（N+1 防止 — C-07）
                var priceMap = new Dictionary<string, PriceDto>();
                if (request.IncludePriceUpdates)
                {
                    var priceDtos = await priceService.GetByProductIdsAsync(
                        inventories.Select(i => i.ProductId).ToList(), ct);
                    foreach (var p in priceDtos)
                        priceMap[p.ProductId] = p;
                }

                foreach (var inventory in inventories)
                {
                    var availableQuantity = inventory.AvailableQuantity;
                    var hadPrevious = previousQuantities.TryGetValue(inventory.ProductId, out var prevQuantity);

                    if (!hadPrevious || prevQuantity != availableQuantity)
                    {
                        var updateType = DetermineUpdateType(availableQuantity, prevQuantity, hadPrevious);

                        var update = new StockUpdate
                        {
                            ProductId = inventory.ProductId,
                            AvailableQuantity = availableQuantity,
                            UpdateTimestamp = timeProvider.GetUtcNow().ToString("o"),
                            UpdateType = updateType
                        };

                        if (request.IncludePriceUpdates
                            && priceMap.TryGetValue(inventory.ProductId, out var priceDto))
                        {
                            var effectivePrice = priceDto.SalePrice ?? priceDto.RegularPrice;
                            var priceSen = (long)(effectivePrice * 100);
                            update.CurrentPriceSen = priceSen;
                            previousPrices[inventory.ProductId] = priceSen;
                        }

                        await responseStream.WriteAsync(update, ct);
                        previousQuantities[inventory.ProductId] = availableQuantity;
                    }
                    else if (request.IncludePriceUpdates
                             && priceMap.TryGetValue(inventory.ProductId, out var priceDto2))
                    {
                        var effectivePrice = priceDto2.SalePrice ?? priceDto2.RegularPrice;
                        var priceSen = (long)(effectivePrice * 100);
                        var hadPreviousPrice = previousPrices.TryGetValue(inventory.ProductId, out var prevPrice);

                        if (!hadPreviousPrice || prevPrice != priceSen)
                        {
                            var update = new StockUpdate
                            {
                                ProductId = inventory.ProductId,
                                AvailableQuantity = availableQuantity,
                                CurrentPriceSen = priceSen,
                                UpdateTimestamp = timeProvider.GetUtcNow().ToString("o"),
                                UpdateType = StockUpdateType.PriceChanged
                            };
                            await responseStream.WriteAsync(update, ct);
                            previousPrices[inventory.ProductId] = priceSen;
                        }
                    }
                }

                await Task.Delay(PollingInterval, ct);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("在庫変動通知サブスクリプション終了（クライアント切断）: ProductIds={ProductIds}",
                string.Join(",", productIds));
        }
    }

    /// <summary>
    /// 在庫 DTO から gRPC StockResponse を構築する。
    /// </summary>
    /// <param name="inventory">在庫 DTO（利用可能数量を含む）</param>
    /// <param name="requiredQuantity">要求数量（0 以下の場合は CanFulfillRequest が false）</param>
    /// <returns>在庫状況を表す gRPC StockResponse メッセージ</returns>
    private static StockResponse BuildStockResponse(DTOs.Responses.InventoryDto inventory, int requiredQuantity)
    {
        var availableQuantity = inventory.AvailableQuantity;
        var isInStock = availableQuantity > 0;

        return new StockResponse
        {
            ProductId = inventory.ProductId,
            AvailableQuantity = availableQuantity,
            IsInStock = isInStock,
            CanFulfillRequest = requiredQuantity > 0 && availableQuantity >= requiredQuantity
        };
    }

    /// <summary>
    /// 在庫数量の変動タイプを判定する。
    /// </summary>
    /// <param name="currentQuantity">現在の利用可能数量</param>
    /// <param name="previousQuantity">前回の利用可能数量（hadPrevious=false の場合は無効値）</param>
    /// <param name="hadPrevious">前回値が存在するかどうか</param>
    /// <returns>
    /// 変動タイプ（QuantityChanged / ProductAvailable / ProductUnavailable）。
    /// 初回観測で在庫 0 の場合は ProductUnavailable を返す。
    /// </returns>
    private static StockUpdateType DetermineUpdateType(int currentQuantity, int previousQuantity, bool hadPrevious)
    {
        if (!hadPrevious)
            return currentQuantity > 0 ? StockUpdateType.QuantityChanged : StockUpdateType.ProductUnavailable;

        return (previousQuantity, currentQuantity) switch
        {
            (> 0, 0) => StockUpdateType.ProductUnavailable,
            (0, > 0) => StockUpdateType.ProductAvailable,
            _ => StockUpdateType.QuantityChanged
        };
    }
}
