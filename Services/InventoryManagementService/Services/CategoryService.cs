using System.Text.Json;
using InventoryManagementService.Configurations;
using InventoryManagementService.DTOs.Requests;
using InventoryManagementService.DTOs.Responses;
using InventoryManagementService.Exceptions;
using InventoryManagementService.Models;
using InventoryManagementService.Repositories.Interfaces;
using InventoryManagementService.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace InventoryManagementService.Services;

/// <summary>
/// カテゴリ管理サービスの実装クラス。
/// 階層構造を持つカテゴリの CRUD 操作を管理する。
/// </summary>
/// <remarks>
/// <para>階層管理: 親カテゴリ指定時は Level と Path を自動計算し、階層構造を維持する。</para>
/// <para>削除防止: 子カテゴリまたは商品が紐づくカテゴリの削除を禁止する。</para>
/// <para>Redis キャッシュ: カテゴリ全件と個別カテゴリのキャッシュを管理。変更時に無効化する。</para>
/// </remarks>
public class CategoryService(
    ICategoryRepository categoryRepository,
    IDistributedCache cache,
    IOptions<CacheConfig> cacheOptions,
    ILogger<CategoryService> logger) : ICategoryService
{
    private readonly CacheConfig _cacheConfig = cacheOptions.Value;

    /// <inheritdoc />
    public async Task<CategoryDto> CreateAsync(
        CategoryCreateRequest request, CancellationToken ct = default)
    {
        if (request.ParentId is not null)
        {
            var parent = await categoryRepository.FindByIdAsync(request.ParentId, ct)
                ?? throw new ResourceNotFoundException("Category", request.ParentId);

            var category = new Category
            {
                Name = request.Name,
                Description = request.Description,
                ParentId = request.ParentId,
                Level = parent.Level + 1,
                Path = string.IsNullOrEmpty(parent.Path)
                    ? parent.Id
                    : $"{parent.Path}/{parent.Id}"
            };
            await categoryRepository.AddAsync(category, ct);
            await categoryRepository.SaveChangesAsync(ct);

            await InvalidateCategoryCacheAsync(ct);

            logger.LogInformation("カテゴリ作成完了: CategoryId={CategoryId}, Name={Name}",
                category.Id, category.Name);
            return MapToDto(category);
        }
        else
        {
            var category = new Category
            {
                Name = request.Name,
                Description = request.Description,
                Level = 0
            };
            await categoryRepository.AddAsync(category, ct);
            await categoryRepository.SaveChangesAsync(ct);

            await InvalidateCategoryCacheAsync(ct);

            logger.LogInformation("ルートカテゴリ作成完了: CategoryId={CategoryId}, Name={Name}",
                category.Id, category.Name);
            return MapToDto(category);
        }
    }

    /// <inheritdoc />
    public async Task<CategoryDto> UpdateAsync(
        string id, CategoryUpdateRequest request, CancellationToken ct = default)
    {
        var category = await categoryRepository.FindByIdAsync(id, ct)
            ?? throw new ResourceNotFoundException("Category", id);

        if (request.Name is not null) category.Name = request.Name;
        if (request.Description is not null) category.Description = request.Description;
        // ADD-3: ドメインメソッド Activate()/Deactivate() を使用
        if (request.IsActive.HasValue)
        {
            if (request.IsActive.Value)
                category.Activate();
            else
                category.Deactivate();
        }

        await categoryRepository.SaveChangesAsync(ct);

        await InvalidateCategoryCacheAsync(ct);
        await RemoveCacheSafeAsync($"category:{id}", ct);

        logger.LogInformation("カテゴリ更新完了: CategoryId={CategoryId}", id);
        return MapToDto(category);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var category = await categoryRepository.FindByIdAsync(id, ct)
            ?? throw new ResourceNotFoundException("Category", id);

        if (await categoryRepository.HasChildrenAsync(id, ct))
            throw new InventoryException("CAT_001", "子カテゴリが存在するため削除できません");

        if (await categoryRepository.HasProductsAsync(id, ct))
            throw new InventoryException("CAT_002", "カテゴリに紐づく商品が存在するため削除できません");

        // ADD-3: ドメインメソッド Deactivate() を使用
        category.Deactivate();
        await categoryRepository.SaveChangesAsync(ct);

        await InvalidateCategoryCacheAsync(ct);
        await RemoveCacheSafeAsync($"category:{id}", ct);

        logger.LogInformation("カテゴリ論理削除完了: CategoryId={CategoryId}", id);
    }

    /// <inheritdoc />
    public async Task<CategoryDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (_cacheConfig.Enabled)
        {
            var cacheKey = $"category:{id}";
            var cached = await GetCacheSafeAsync(cacheKey, ct);
            if (cached is not null)
                return JsonSerializer.Deserialize<CategoryDto>(cached);

            var category = await categoryRepository.FindByIdAsync(id, ct);
            if (category is null) return null;

            var dto = MapToDto(category);
            await SetCacheSafeAsync(cacheKey, dto, _cacheConfig.CategoryTtlSeconds, ct);
            return dto;
        }

        var c = await categoryRepository.FindByIdAsync(id, ct);
        return c is null ? null : MapToDto(c);
    }

    /// <inheritdoc />
    public async Task<List<CategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        if (_cacheConfig.Enabled)
        {
            const string cacheKey = "category:all";
            var cached = await GetCacheSafeAsync(cacheKey, ct);
            if (cached is not null)
                return JsonSerializer.Deserialize<List<CategoryDto>>(cached) ?? [];

            var categories = await categoryRepository.GetAllAsync(ct);
            var dtos = categories.Select(MapToDto).ToList();
            await SetCacheSafeAsync(cacheKey, dtos, _cacheConfig.CategoryTtlSeconds, ct);
            return dtos;
        }

        var cats = await categoryRepository.GetAllAsync(ct);
        return cats.Select(MapToDto).ToList();
    }

    // カテゴリ全件キャッシュ（"category:all"）を無効化する
    private async Task InvalidateCategoryCacheAsync(CancellationToken ct)
    {
        await RemoveCacheSafeAsync("category:all", ct);
    }

    // Redis 障害時は null を返しグレースフルデグラデーションする
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

    // Redis 障害時はエラーを無視して処理を継続する
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

    // Redis 障害時はエラーを無視して処理を継続する
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
    /// Category エンティティを CategoryDto に変換する。
    /// </summary>
    private static CategoryDto MapToDto(Category c) => new(
        c.Id, c.Name, c.Description, c.ParentId, c.Level, c.Path, c.IsActive);
}
