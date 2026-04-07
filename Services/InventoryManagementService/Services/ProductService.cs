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
/// 商品管理サービスの実装クラス。
/// 商品の CRUD 操作、検索、画像アップロードを管理する。
/// </summary>
/// <remarks>
/// <para>Redis キャッシュ: 商品 ID / SKU 別にキャッシュし、変更操作時にキャッシュを無効化する。</para>
/// <para>Azure Blob Storage: 商品画像は IImageRepository 経由で Blob Storage にアップロードする。</para>
/// <para>イベント発行: 商品の作成・更新・削除時に Outbox パターンで ProductCreated/Updated/Deleted イベントを発行する。</para>
/// <para>論理削除: 商品の Delete は IsActive を false に設定する論理削除で実装する。</para>
/// </remarks>
public class ProductService(
    IProductRepository productRepository,
    ICategoryRepository categoryRepository,
    IImageRepository imageRepository,
    IEventPublisherService eventPublisher,
    IDistributedCache cache,
    IOptions<CacheConfig> cacheOptions,
    ILogger<ProductService> logger) : IProductService
{
    private readonly CacheConfig _cacheConfig = cacheOptions.Value;

    /// <inheritdoc />
    public async Task<ProductDto> CreateProductAsync(
        ProductCreateRequest request, CancellationToken ct = default)
    {
        var existing = await productRepository.FindBySkuAsync(request.Sku, ct);
        if (existing is not null)
            throw new DuplicateResourceException("Product", "sku", request.Sku);

        if (!await categoryRepository.ExistsByIdAsync(request.CategoryId, ct))
            throw new ResourceNotFoundException("Category", request.CategoryId);

        var product = new Product
        {
            Sku = request.Sku,
            Name = request.Name,
            Description = request.Description,
            Brand = request.Brand,
            CategoryId = request.CategoryId,
            Attributes = request.Attributes,
            Tags = request.Tags,
            Weight = request.Weight
        };

        await productRepository.AddAsync(product, ct);

        await eventPublisher.PublishProductEventAsync("ProductCreated", product.Id,
            new ProductCreatedEvent(product.Id, product.Sku, product.Name, product.Brand,
                product.CategoryId, DateTimeOffset.UtcNow), ct);

        await productRepository.SaveChangesAsync(ct);

        await InvalidateProductCacheAsync(ct);

        logger.LogInformation("商品作成完了: ProductId={ProductId}, SKU={Sku}", product.Id, product.Sku);
        return MapToDto(product);
    }

    /// <inheritdoc />
    public async Task<ProductDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (_cacheConfig.Enabled)
        {
            var cacheKey = $"product:{id}";
            var cached = await GetCacheSafeAsync(cacheKey, ct);
            if (cached is not null)
                return JsonSerializer.Deserialize<ProductDto>(cached);

            var dto = await GetByIdFromDbAsync(id, ct);
            if (dto is not null)
                await SetCacheSafeAsync(cacheKey, dto, _cacheConfig.ProductTtlSeconds, ct);
            return dto;
        }

        return await GetByIdFromDbAsync(id, ct);
    }

    /// <inheritdoc />
    public async Task<ProductDto?> GetBySkuAsync(string sku, CancellationToken ct = default)
    {
        if (_cacheConfig.Enabled)
        {
            var cacheKey = $"product:sku:{sku}";
            var cached = await GetCacheSafeAsync(cacheKey, ct);
            if (cached is not null)
                return JsonSerializer.Deserialize<ProductDto>(cached);

            var product = await productRepository.FindBySkuAsync(sku, ct);
            if (product is null) return null;

            var dto = MapToDto(product);
            await SetCacheSafeAsync(cacheKey, dto, _cacheConfig.ProductTtlSeconds, ct);
            return dto;
        }

        var p = await productRepository.FindBySkuAsync(sku, ct);
        return p is null ? null : MapToDto(p);
    }

    /// <inheritdoc />
    public async Task<PaginatedResult<ProductDto>> SearchAsync(
        ProductSearchCriteria criteria, int page, int size, CancellationToken ct = default)
    {
        var products = await productRepository.SearchAsync(criteria, page, size, ct);
        var total = await productRepository.CountAsync(criteria, ct);
        return new PaginatedResult<ProductDto>(products.Select(MapToDto).ToList(), total, page, size);
    }

    /// <inheritdoc />
    public async Task<PaginatedResult<ProductDto>> GetByCategoryAsync(
        string categoryId, int page, int size, CancellationToken ct = default)
    {
        var products = await productRepository.FindByCategoryIdAsync(categoryId, page, size, ct);
        var total = await productRepository.CountByCategoryIdAsync(categoryId, ct);
        return new PaginatedResult<ProductDto>(products.Select(MapToDto).ToList(), total, page, size);
    }

    /// <inheritdoc />
    public async Task<List<ProductDto>> GetByIdsAsync(List<string> ids, CancellationToken ct = default)
    {
        var products = await productRepository.FindByIdsAsync(ids, ct);
        return products.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>ドメインメソッド Update() で名前・説明・ブランド・重量・属性・タグを一括更新する。
    /// Activate() / Deactivate() で IsActive フラグを変更し、ビジネスルールをエンティティ側で保証する。</para>
    /// <para>カテゴリ変更時は存在確認を行い、存在しない場合は ResourceNotFoundException を送出する。</para>
    /// </remarks>
    public async Task<ProductDto> UpdateAsync(
        string id, ProductUpdateRequest request, CancellationToken ct = default)
    {
        var product = await productRepository.FindByIdAsync(id, ct)
            ?? throw new ResourceNotFoundException("Product", id);

        if (request.CategoryId is not null)
        {
            if (!await categoryRepository.ExistsByIdAsync(request.CategoryId, ct))
                throw new ResourceNotFoundException("Category", request.CategoryId);
            product.CategoryId = request.CategoryId;
        }

        product.Update(
            request.Name ?? product.Name,
            request.Description ?? product.Description,
            request.Brand ?? product.Brand,
            request.Weight ?? product.Weight,
            request.Attributes ?? product.Attributes,
            request.Tags ?? product.Tags);

        if (request.IsActive.HasValue)
        {
            if (request.IsActive.Value)
                product.Activate();
            else
                product.Deactivate();
        }

        await eventPublisher.PublishProductEventAsync("ProductUpdated", product.Id,
            new ProductUpdatedEvent(product.Id, product.Sku, product.Name, product.Brand,
                product.CategoryId, product.IsActive, DateTimeOffset.UtcNow), ct);

        await productRepository.SaveChangesAsync(ct);

        await InvalidateProductByIdCacheAsync(id, product.Sku, ct);

        logger.LogInformation("商品更新完了: ProductId={ProductId}", product.Id);
        return MapToDto(product);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 物理削除ではなく、ドメインメソッド Deactivate() による論理削除を実行する。
    /// ProductDeleted イベントを Outbox に登録し、キャッシュを無効化する。
    /// </remarks>
    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var product = await productRepository.FindByIdAsync(id, ct)
            ?? throw new ResourceNotFoundException("Product", id);

        product.Deactivate();

        await eventPublisher.PublishProductEventAsync("ProductDeleted", product.Id,
            new ProductDeletedEvent(product.Id, product.Sku, DateTimeOffset.UtcNow), ct);

        await productRepository.SaveChangesAsync(ct);

        await InvalidateProductByIdCacheAsync(id, product.Sku, ct);

        logger.LogInformation("商品論理削除完了: ProductId={ProductId}, SKU={Sku}", product.Id, product.Sku);
    }

    /// <inheritdoc />
    public async Task<ProductImageDto> UploadImageAsync(
        string productId, IFormFile file, CancellationToken ct = default)
    {
        var product = await productRepository.FindByIdAsync(productId, ct)
            ?? throw new ResourceNotFoundException("Product", productId);

        await using var stream = file.OpenReadStream();
        var imageUrl = await imageRepository.UploadAsync(stream, file.FileName, file.ContentType, ct);

        var image = new ProductImage
        {
            ProductId = productId,
            Url = imageUrl,
            Type = "MAIN",
            SortOrder = product.Images.Count
        };
        product.Images.Add(image);
        await productRepository.SaveChangesAsync(ct);

        await InvalidateProductByIdCacheAsync(productId, product.Sku, ct);

        logger.LogInformation("商品画像アップロード完了: ProductId={ProductId}, ImageId={ImageId}",
            productId, image.Id);
        return new ProductImageDto(image.Id, image.Url, image.ThumbnailUrl, image.Type,
            image.SortOrder, image.AltText);
    }

    /// <summary>
    /// DB から商品を取得して DTO に変換する。
    /// </summary>
    private async Task<ProductDto?> GetByIdFromDbAsync(string id, CancellationToken ct)
    {
        var product = await productRepository.FindByIdAsync(id, ct);
        return product is null ? null : MapToDto(product);
    }

    /// <summary>
    /// カテゴリ全件キャッシュを無効化する。
    /// </summary>
    private async Task InvalidateProductCacheAsync(CancellationToken ct)
    {
        await RemoveCacheSafeAsync("category:all", ct);
    }

    /// <summary>
    /// 商品 ID・SKU 別キャッシュおよびカテゴリ全件キャッシュを無効化する。
    /// </summary>
    private async Task InvalidateProductByIdCacheAsync(
        string id, string sku, CancellationToken ct)
    {
        await RemoveCacheSafeAsync($"product:{id}", ct);
        await RemoveCacheSafeAsync($"product:sku:{sku}", ct);
        await RemoveCacheSafeAsync("category:all", ct);
    }

    /// <summary>
    /// Redis からキャッシュ値を安全に取得する。Redis 障害時は null を返す。
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
    /// Redis にキャッシュ値を安全に書き込む。Redis 障害時はエラーを無視する。
    /// </summary>
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

    /// <summary>
    /// Redis のキャッシュエントリを安全に削除する。Redis 障害時はエラーを無視する。
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
    /// Product エンティティを ProductDto に変換する。
    /// </summary>
    private static ProductDto MapToDto(Product p) => new(
        p.Id, p.Sku, p.Name, p.Description, p.Brand,
        p.Category?.Name, p.Weight, p.IsActive, p.CreatedAt);
}
