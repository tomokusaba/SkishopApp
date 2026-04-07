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
/// 価格管理サービスの実装クラス。
/// 商品価格の設定・更新、価格履歴の記録と取得を管理する。
/// </summary>
/// <remarks>
/// <para>価格履歴: 価格変更時に PriceHistory テーブルへ変更者・変更理由を含む監査情報を記録する。</para>
/// <para>一括無効化: 新規価格設定時は既存のアクティブ価格を ExecuteUpdateAsync で一括無効化する。</para>
/// <para>Redis キャッシュ: 価格参照にキャッシュを使用し、変更時に無効化する。Redis 障害時は DB フォールバック。</para>
/// </remarks>
public class PriceService(
    IPriceRepository priceRepository,
    IProductRepository productRepository,
    IEventPublisherService eventPublisher,
    IDistributedCache cache,
    IOptions<CacheConfig> cacheOptions,
    TimeProvider timeProvider,
    ILogger<PriceService> logger) : IPriceService
{
    private readonly CacheConfig _cacheConfig = cacheOptions.Value;

    /// <inheritdoc />
    /// <remarks>
    /// <para>トランザクション戦略: 明示的トランザクションを開始し、以下を単一トランザクションで実行する。</para>
    /// <list type="number">
    ///   <item><description>ExecuteUpdateAsync による既存アクティブ価格の一括無効化</description></item>
    ///   <item><description>新規 Price エンティティの追加</description></item>
    ///   <item><description>PriceHistory への監査レコード追加</description></item>
    ///   <item><description>Outbox テーブルへの PriceUpdated イベント登録</description></item>
    /// </list>
    /// <para>コミット後に Redis キャッシュを無効化し、次回参照時に最新価格を取得させる。</para>
    /// </remarks>
    public async Task<PriceDto> CreateAsync(PriceCreateRequest request, string? changedBy = null, CancellationToken ct = default)
    {
        _ = await productRepository.FindByIdAsync(request.ProductId, ct)
            ?? throw new ResourceNotFoundException("Product", request.ProductId);

        var priceDto = await priceRepository.ExecuteInTransactionAsync(async (transaction, ct) =>
        {
            await priceRepository.DeactivateByProductIdAsync(request.ProductId, ct);

            var price = new Price
            {
                ProductId = request.ProductId,
                RegularPrice = request.RegularPrice,
                SalePrice = request.SalePrice,
                SaleStartDate = request.SaleStartDate,
                SaleEndDate = request.SaleEndDate,
                CurrencyCode = request.CurrencyCode,
                IsActive = true
            };

            await priceRepository.AddAsync(price, ct);

            await priceRepository.AddHistoryAsync(new PriceHistory
            {
                ProductId = request.ProductId,
                Price = request.RegularPrice,
                PriceType = "REGULAR",
                EffectiveDate = DateTimeOffset.UtcNow,
                CurrencyCode = request.CurrencyCode,
                Reason = "新規価格設定",
                ChangedBy = changedBy
            }, ct);

            await eventPublisher.PublishPriceEventAsync("PriceUpdated", request.ProductId,
                new PriceUpdatedEvent(request.ProductId, 0, request.RegularPrice,
                    null, request.SalePrice, request.CurrencyCode, DateTimeOffset.UtcNow), ct);

            await priceRepository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return MapToDto(price);
        }, ct);

        await RemoveCacheSafeAsync($"price:{request.ProductId}", ct);

        logger.LogInformation("価格設定完了: ProductId={ProductId}, RegularPrice={RegularPrice}",
            request.ProductId, request.RegularPrice);
        return priceDto;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>部分更新: null でないフィールドのみ更新する。通常価格・セール価格・有効フラグの各変更パターンに対応する。</para>
    /// <para>セール適用: SalePrice・SaleStartDate・SaleEndDate が全て指定された場合はドメインメソッド ApplySale で日付整合性を検証する。</para>
    /// <para>価格履歴: 通常価格が変更された場合のみ PriceHistory に監査レコードを追加し、PriceUpdated イベントを発行する。</para>
    /// </remarks>
    public async Task<PriceDto> UpdateAsync(
        string id, PriceUpdateRequest request, string? changedBy = null, CancellationToken ct = default)
    {
        var price = await priceRepository.FindByIdAsync(id, ct)
            ?? throw new ResourceNotFoundException("Price", id);

        var oldRegularPrice = price.RegularPrice;
        var oldSalePrice = price.SalePrice;

        if (request.RegularPrice.HasValue) price.UpdateRegularPrice(request.RegularPrice.Value);
        if (request.SalePrice is not null
            && request.SaleStartDate.HasValue
            && request.SaleEndDate.HasValue)
        {
            price.ApplySale(request.SalePrice.Value, request.SaleStartDate.Value, request.SaleEndDate.Value);
        }
        else
        {
            // 部分更新用ドメインメソッドを使用
            if (request.SalePrice is not null) price.UpdateSalePrice(request.SalePrice);
            if (request.SaleStartDate.HasValue) price.UpdateSaleStartDate(request.SaleStartDate);
            if (request.SaleEndDate.HasValue) price.UpdateSaleEndDate(request.SaleEndDate);
        }
        if (request.IsActive.HasValue)
        {
            if (!request.IsActive.Value)
                price.Deactivate();
            else
                price.Activate();  // ドメインメソッドを使用
        }

        if (request.RegularPrice.HasValue)
        {
            await priceRepository.AddHistoryAsync(new PriceHistory
            {
                ProductId = price.ProductId,
                Price = request.RegularPrice.Value,
                PriceType = "REGULAR",
                EffectiveDate = DateTimeOffset.UtcNow,
                CurrencyCode = price.CurrencyCode,
                Reason = "価格更新",
                ChangedBy = changedBy
            }, ct);

            await eventPublisher.PublishPriceEventAsync("PriceUpdated", price.ProductId,
                new PriceUpdatedEvent(price.ProductId, oldRegularPrice, request.RegularPrice.Value,
                    oldSalePrice, price.SalePrice, price.CurrencyCode,
                    DateTimeOffset.UtcNow), ct);
        }

        await priceRepository.SaveChangesAsync(ct);

        await RemoveCacheSafeAsync($"price:{price.ProductId}", ct);

        logger.LogInformation("価格更新完了: PriceId={PriceId}, ProductId={ProductId}",
            id, price.ProductId);
        return MapToDto(price);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 商品 ID からアクティブ価格を取得し、その価格 ID で UpdateAsync に委譲する。
    /// エンドポイントが商品 ID を受け取る場合に使用する（B-2 修正）。
    /// </remarks>
    public async Task<PriceDto> UpdateByProductIdAsync(
        string productId, PriceUpdateRequest request, string? changedBy = null, CancellationToken ct = default)
    {
        var price = await priceRepository.FindActiveByProductIdAsync(productId, ct)
            ?? throw new ResourceNotFoundException("Price for Product", productId);

        return await UpdateAsync(price.Id, request, changedBy, ct);
    }

    /// <inheritdoc />
    public async Task<PriceDto?> GetByProductIdAsync(string productId, CancellationToken ct = default)
    {
        if (_cacheConfig.Enabled)
        {
            var cacheKey = $"price:{productId}";
            var cached = await GetCacheSafeAsync(cacheKey, ct);
            if (cached is not null)
                return JsonSerializer.Deserialize<PriceDto>(cached);

            var price = await priceRepository.FindActiveByProductIdAsync(productId, ct);
            if (price is null) return null;

            var dto = MapToDto(price);
            await SetCacheSafeAsync(cacheKey, dto, _cacheConfig.PriceTtlSeconds, ct);
            return dto;
        }

        var p = await priceRepository.FindActiveByProductIdAsync(productId, ct);
        return p is null ? null : MapToDto(p);
    }

    /// <inheritdoc />
    public async Task<List<PriceDto>> GetByProductIdsAsync(List<string> productIds, CancellationToken ct = default)
    {
        var prices = await priceRepository.FindActiveByProductIdsAsync(productIds, ct);
        return prices.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<PaginatedResult<PriceHistoryDto>> GetHistoryAsync(
        string productId, int page, int size, CancellationToken ct = default)
    {
        var history = await priceRepository.FindHistoryByProductIdAsync(productId, page, size, ct);
        var total = await priceRepository.CountHistoryByProductIdAsync(productId, ct);
        return new PaginatedResult<PriceHistoryDto>(
            history.Select(MapHistoryToDto).ToList(), total, page, size);
    }

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

    private async Task SetCacheSafeAsync<T>(string key, T value, int ttlSeconds, CancellationToken ct)
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
    /// Price エンティティを PriceDto に変換する。セール期間の有効判定も行う。
    /// </summary>
    // H-13: TimeProvider 経由で現在時刻を取得（テスタビリティ向上）
    private PriceDto MapToDto(Price p)
    {
        var now = timeProvider.GetUtcNow();
        var onSale = p.SalePrice.HasValue
            && (!p.SaleStartDate.HasValue || p.SaleStartDate <= now)
            && (!p.SaleEndDate.HasValue || p.SaleEndDate >= now);
        return new PriceDto(p.Id, p.ProductId, p.RegularPrice, p.SalePrice,
            p.SaleStartDate, p.SaleEndDate, p.CurrencyCode, p.IsActive, onSale);
    }

    /// <summary>
    /// PriceHistory エンティティを PriceHistoryDto に変換する。
    /// </summary>
    private static PriceHistoryDto MapHistoryToDto(PriceHistory h) => new(
        h.Id, h.ProductId, h.Price, h.PriceType, h.EffectiveDate,
        h.Reason, h.CurrencyCode, h.ChangedBy, h.CreatedAt);
}
