using System.Text.Json;
using InventoryManagementService.Configurations;
using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.DTOs.Responses;
using InventoryManagementService.Events;
using InventoryManagementService.Exceptions;
using InventoryManagementService.Models;
using InventoryManagementService.Repositories.Interfaces;
using InventoryManagementService.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace InventoryManagementService.Services;

/// <summary>
/// 在庫管理サービスの実装クラス。
/// 在庫の引当・解放・確定、入出庫、ステータス照会を管理する。
/// </summary>
/// <remarks>
/// <para>デッドロック防止: 複数商品への在庫操作時は商品 ID 順にソートしてロックを取得する。</para>
/// <para>悲観的ロック: PostgreSQL の SELECT FOR UPDATE を使用して在庫の排他制御を行う。</para>
/// <para>Outbox パターン: 在庫変更イベントは DB トランザクション内で Outbox テーブルに書き込み、整合性を保証する。</para>
/// <para>Redis キャッシュ: 読み取り操作ではキャッシュを優先参照し、Redis 障害時はグレースフルデグラデーションで DB にフォールバックする。</para>
/// </remarks>
public class InventoryService(
    IInventoryRepository inventoryRepository,
    IReviewService reviewService,
    IEventPublisherService eventPublisher,
    IDistributedCache cache,
    IOptions<CacheConfig> cacheOptions,
    ILogger<InventoryService> logger) : IInventoryService
{
    private readonly CacheConfig _cacheConfig = cacheOptions.Value;

    /// <inheritdoc />
    /// <remarks>
    /// <para>デッドロック防止: 商品 ID 昇順にソートしてからロックを取得する（全サービス共通規約 H-06）。</para>
    /// <para>悲観的ロック: FindByProductIdsForUpdateAsync で PostgreSQL の SELECT FOR UPDATE を発行し、
    /// 並行トランザクションとの在庫競合を防止する。</para>
    /// <para>ドメインメソッド Reserve() でビジネスルール（在庫不足チェック）をエンティティ側で保証する。</para>
    /// </remarks>
    public async Task<string> ReserveAsync(
        string orderId, List<ReserveItemDto> items, CancellationToken ct = default)
    {
        // Sort by ProductId to prevent deadlocks (H-06)
        var sortedItems = items.OrderBy(i => i.ProductId).ToList();
        var cacheKeysToInvalidate = new List<string>();

        var reservationId = await inventoryRepository.ExecuteInTransactionAsync(async (transaction, ct) =>
        {
            var resId = Guid.NewGuid().ToString();

            // Batch query with FOR UPDATE, sorted to prevent deadlocks (C-11)
            var productIds = sortedItems.Select(i => i.ProductId).ToList();
            var inventories = await inventoryRepository.FindByProductIdsForUpdateAsync(productIds, ct);
            var inventoryMap = inventories.ToDictionary(i => i.ProductId);

            foreach (var item in sortedItems)
            {
                if (!inventoryMap.TryGetValue(item.ProductId, out var inventory))
                    throw new ResourceNotFoundException("Inventory", item.ProductId);

                // Use entity domain method (C-14)
                inventory.Reserve(item.Quantity);

                await eventPublisher.PublishInventoryEventAsync("InventoryReserved", item.ProductId,
                    new InventoryReservedEvent(orderId, item.ProductId, item.Quantity,
                        resId, DateTimeOffset.UtcNow), ct);

                cacheKeysToInvalidate.Add($"inventory:{item.ProductId}");
            }

            await inventoryRepository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return resId;
        }, ct);

        // Invalidate caches after transaction commits successfully
        foreach (var cacheKey in cacheKeysToInvalidate)
            await RemoveCacheSafeAsync(cacheKey, ct);

        logger.LogInformation("在庫引当完了: OrderId={OrderId}, Items={ItemCount}",
            orderId, items.Count);
        return reservationId;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>デッドロック防止: 引当時と同様に商品 ID 昇順にソートしてロックを取得する。</para>
    /// <para>部分解放: 在庫レコードが見つからない商品はログ出力して処理をスキップし、残りの解放を継続する。</para>
    /// <para>ドメインメソッド Release() で引当数量のデクリメントと有効在庫の復元を行う。</para>
    /// </remarks>
    public async Task ReleaseAsync(
        string orderId, string reservationId, List<ReserveItemDto> items,
        CancellationToken ct = default)
    {
        // Sort by ProductId to prevent deadlocks (H-06)
        var sortedItems = items.OrderBy(i => i.ProductId).ToList();
        var cacheKeysToInvalidate = new List<string>();

        await inventoryRepository.ExecuteInTransactionAsync(async (transaction, ct) =>
        {
            var productIds = sortedItems.Select(i => i.ProductId).ToList();
            var inventories = await inventoryRepository.FindByProductIdsForUpdateAsync(productIds, ct);
            var inventoryMap = inventories.ToDictionary(i => i.ProductId);

            foreach (var item in sortedItems)
            {
                if (!inventoryMap.TryGetValue(item.ProductId, out var inventory))
                {
                    logger.LogWarning("在庫解放時にレコード未検出: ProductId={ProductId}", item.ProductId);
                    continue;
                }

                // Use entity domain method (C-14) - release only the specific quantity
                inventory.Release(item.Quantity);

                await eventPublisher.PublishInventoryEventAsync("InventoryReleased", inventory.ProductId,
                    new InventoryReleasedEvent(orderId, inventory.ProductId, item.Quantity,
                        reservationId, "ORDER_CANCELLED", DateTimeOffset.UtcNow), ct);

                cacheKeysToInvalidate.Add($"inventory:{inventory.ProductId}");
            }

            await inventoryRepository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }, ct);

        // Invalidate caches after transaction commits successfully
        foreach (var cacheKey in cacheKeysToInvalidate)
            await RemoveCacheSafeAsync(cacheKey, ct);

        logger.LogInformation("在庫解放完了: OrderId={OrderId}, ReservationId={ReservationId}",
            orderId, reservationId);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 注文完了時に呼び出される。引当済み数量を Release() で解放し、実在庫から差し引かれた状態を確定する。
    /// 在庫レコードが見つからない商品はログ出力してスキップする（べき等性の保証）。
    /// </remarks>
    public async Task ConfirmReservationAsync(
        string orderId, List<ReserveItemDto> items, CancellationToken ct = default)
    {
        var sortedItems = items.OrderBy(i => i.ProductId).ToList();

        await inventoryRepository.ExecuteInTransactionAsync(async (transaction, ct) =>
        {
            var productIds = sortedItems.Select(i => i.ProductId).ToList();
            var inventories = await inventoryRepository.FindByProductIdsForUpdateAsync(productIds, ct);
            var inventoryMap = inventories.ToDictionary(i => i.ProductId);

            foreach (var item in sortedItems)
            {
                if (!inventoryMap.TryGetValue(item.ProductId, out var inventory))
                {
                    logger.LogWarning(
                        "在庫レコード未検出: ProductId={ProductId}, OrderId={OrderId}",
                        item.ProductId, orderId);
                    continue;
                }

                // Release reserved quantity (order completed)
                inventory.Release(item.Quantity);
            }

            await inventoryRepository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }, ct);

        logger.LogInformation(
            "注文完了による在庫確定処理完了: OrderId={OrderId}, Items={ItemCount}",
            orderId, items.Count);
    }

    /// <inheritdoc />
    /// <remarks>
    /// PostgreSQL の SELECT FOR UPDATE による悲観的ロック（C-05）で排他制御を行い、
    /// ドメインメソッド StockIn() で数量加算のビジネスルールをエンティティ側で保証する。
    /// トランザクションコミット後に Redis キャッシュを無効化する。
    /// </remarks>
    public async Task<InventoryDto> StockInAsync(StockInRequest request, CancellationToken ct = default)
    {
        var result = await inventoryRepository.ExecuteInTransactionAsync(async (transaction, ct) =>
        {
            var inventory = await inventoryRepository.FindByProductIdForUpdateAsync(request.ProductId, ct)
                ?? throw new ResourceNotFoundException("Inventory", request.ProductId);

            var previousQuantity = inventory.Quantity;
            inventory.StockIn(request.Quantity);

            await eventPublisher.PublishInventoryEventAsync("InventoryUpdated", request.ProductId,
                new InventoryUpdatedEvent(request.ProductId, string.Empty, previousQuantity,
                    inventory.Quantity, "STOCK_IN", inventory.LocationCode,
                    DateTimeOffset.UtcNow, request.ReferenceId), ct);

            await inventoryRepository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return (dto: MapToDto(inventory), previousQuantity, newQuantity: inventory.Quantity);
        }, ct);

        await RemoveCacheSafeAsync($"inventory:{request.ProductId}", ct);

        logger.LogInformation("入庫処理完了: ProductId={ProductId}, PreviousQty={PreviousQty}, NewQty={NewQty}",
            request.ProductId, result.previousQuantity, result.newQuantity);
        return result.dto;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>悲観的ロック + ドメインメソッド StockOut() で在庫不足チェックと数量減算を行う。</para>
    /// <para>在庫枯渇（数量 0）時は StockDepleted イベント、
    /// 低在庫（数量 ≤ ReorderPoint）時は LowStockAlert イベントを追加発行する。</para>
    /// </remarks>
    public async Task<InventoryDto> StockOutAsync(StockOutRequest request, CancellationToken ct = default)
    {
        var result = await inventoryRepository.ExecuteInTransactionAsync(async (transaction, ct) =>
        {
            var inventory = await inventoryRepository.FindByProductIdForUpdateAsync(request.ProductId, ct)
                ?? throw new ResourceNotFoundException("Inventory", request.ProductId);

            var previousQuantity = inventory.Quantity;
            inventory.StockOut(request.Quantity);

            await eventPublisher.PublishInventoryEventAsync("InventoryUpdated", request.ProductId,
                new InventoryUpdatedEvent(request.ProductId, string.Empty, previousQuantity,
                    inventory.Quantity, request.Reason, inventory.LocationCode,
                    DateTimeOffset.UtcNow), ct);

            if (inventory.Quantity == 0)
            {
                await eventPublisher.PublishInventoryEventAsync("StockDepleted", request.ProductId,
                    new StockDepletedEvent(request.ProductId, string.Empty,
                        inventory.LocationCode, DateTimeOffset.UtcNow), ct);
            }
            else if (inventory.Quantity <= inventory.ReorderPoint)
            {
                await eventPublisher.PublishInventoryEventAsync("LowStockAlert", request.ProductId,
                    new LowStockAlertEvent(request.ProductId, string.Empty, inventory.Quantity,
                        inventory.ReorderPoint, inventory.LocationCode, DateTimeOffset.UtcNow), ct);
            }

            await inventoryRepository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return (dto: MapToDto(inventory), previousQuantity, newQuantity: inventory.Quantity);
        }, ct);

        await RemoveCacheSafeAsync($"inventory:{request.ProductId}", ct);

        logger.LogInformation("出庫処理完了: ProductId={ProductId}, PreviousQty={PreviousQty}, NewQty={NewQty}",
            request.ProductId, result.previousQuantity, result.newQuantity);
        return result.dto;
    }

    /// <inheritdoc />
    public async Task<InventoryDto?> GetByProductIdAsync(string productId, CancellationToken ct = default)
    {
        if (_cacheConfig.Enabled)
        {
            var cacheKey = $"inventory:{productId}";
            var cached = await GetCacheSafeAsync(cacheKey, ct);
            if (cached is not null)
                return JsonSerializer.Deserialize<InventoryDto>(cached);

            var inventory = await inventoryRepository.FindByProductIdAsync(productId, ct);
            if (inventory is null) return null;

            var dto = MapToDto(inventory);
            await SetCacheSafeAsync(cacheKey, dto, _cacheConfig.InventoryTtlSeconds, ct);
            return dto;
        }

        var inv = await inventoryRepository.FindByProductIdAsync(productId, ct);
        return inv is null ? null : MapToDto(inv);
    }

    /// <inheritdoc />
    public async Task<List<InventoryDto>> GetByProductIdsAsync(
        List<string> productIds, CancellationToken ct = default)
    {
        var inventories = await inventoryRepository.FindByProductIdsAsync(productIds, ct);
        return inventories.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<InventoryStatusDto?> GetStatusAsync(string productId, CancellationToken ct = default)
    {
        var inventory = await inventoryRepository.FindByProductIdAsync(productId, ct);
        if (inventory is null) return null;

        return new InventoryStatusDto(
            inventory.ProductId,
            inventory.Quantity,
            inventory.ReservedQuantity,
            inventory.Quantity - inventory.ReservedQuantity,
            inventory.DetermineStatus());
    }

    /// <inheritdoc />
    public async Task<PaginatedResult<InventoryDto>> GetLowStockAsync(
        int threshold, int page, int size, CancellationToken ct = default)
    {
        var inventories = await inventoryRepository.FindLowStockAsync(threshold, page, size, ct);
        var total = await inventoryRepository.CountLowStockAsync(threshold, ct);
        return new PaginatedResult<InventoryDto>(inventories.Select(MapToDto).ToList(), total, page, size);
    }

    /// <inheritdoc />
    public async Task AnonymizeUserReviewsAsync(string userId, CancellationToken ct = default)
    {
        await reviewService.AnonymizeUserReviewsAsync(userId, ct);
        logger.LogInformation("GDPR DSR 処理委譲完了: UserId={UserId}", userId);
    }

    /// <summary>
    /// Redis からキャッシュ値を安全に取得する。Redis 障害時は null を返しグレースフルデグラデーションする。
    /// </summary>
    private async Task<string?> GetCacheSafeAsync(string key, CancellationToken ct)
    {
        try
        {
            return await cache.GetStringAsync(key, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis キャッシュ読み取りエラー（フォールバック）: Key={Key}", key);
            return null;
        }
    }

    /// <summary>
    /// Redis にキャッシュ値を安全に書き込む。Redis 障害時はエラーを無視して処理を継続する。
    /// </summary>
    private async Task SetCacheSafeAsync<T>(
        string key, T value, int ttlSeconds, CancellationToken ct)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            await cache.SetStringAsync(key, json,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttlSeconds)
                }, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis キャッシュ書き込みエラー（無視）: Key={Key}", key);
        }
    }

    /// <summary>
    /// Redis のキャッシュエントリを安全に削除する。Redis 障害時はエラーを無視して処理を継続する。
    /// </summary>
    private async Task RemoveCacheSafeAsync(string key, CancellationToken ct)
    {
        try
        {
            await cache.RemoveAsync(key, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis キャッシュ削除エラー（無視）: Key={Key}", key);
        }
    }

    /// <summary>
    /// Inventory エンティティを InventoryDto に変換する。有効数量（= 総数量 - 引当数量）を計算する。
    /// </summary>
    private static InventoryDto MapToDto(Inventory i) => new(
        i.Id, i.ProductId, i.Quantity, i.ReservedQuantity,
        i.Quantity - i.ReservedQuantity, i.LocationCode, i.Status, i.ReorderPoint);
}
