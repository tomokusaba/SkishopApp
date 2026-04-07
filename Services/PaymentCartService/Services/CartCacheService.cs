using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using PaymentCartService.Configurations;
using PaymentCartService.Models;
using PaymentCartService.Services.Interfaces;

namespace PaymentCartService.Services;

public class CartCacheService(
    IDistributedCache cache,
    IOptions<CartSettings> cartOptions,
    ILogger<CartCacheService> logger) : ICartCacheService
{
    private readonly CartSettings _settings = cartOptions.Value;

    private DistributedCacheEntryOptions CreateCacheOptions()
        => new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(_settings.ExpiryDays) };

    public async Task<Cart?> GetCartAsync(string cartId, CancellationToken ct = default)
    {
        try
        {
            var cached = await cache.GetStringAsync($"cart:{cartId}", ct);
            if (cached is null) return null;

            return JsonSerializer.Deserialize<Cart>(cached);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis キャッシュ読み取りエラー: CartId={CartId}", cartId);
            return null;
        }
    }

    public async Task SetCartAsync(Cart cart, CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(cart);
            var options = CreateCacheOptions();
            await cache.SetStringAsync($"cart:{cart.Id}", json, options, ct);

            if (cart.CustomerId is not null)
                await cache.SetStringAsync(
                    $"cart:user:{cart.CustomerId}", cart.Id, options, ct);

            await cache.SetStringAsync(
                $"cart:session:{cart.SessionId}", cart.Id, options, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis キャッシュ書き込みエラー: CartId={CartId}", cart.Id);
        }
    }

    public async Task RemoveCartAsync(string cartId, CancellationToken ct = default)
    {
        try
        {
            await cache.RemoveAsync($"cart:{cartId}", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis キャッシュ削除エラー: CartId={CartId}", cartId);
        }
    }

    public async Task<string?> GetCartIdBySessionAsync(string sessionId, CancellationToken ct = default)
    {
        try
        {
            return await cache.GetStringAsync($"cart:session:{sessionId}", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis セッションキャッシュ読み取りエラー: SessionId={SessionId}", sessionId);
            return null;
        }
    }

    public async Task SetCartIdBySessionAsync(
        string sessionId, string cartId, CancellationToken ct = default)
    {
        try
        {
            var options = CreateCacheOptions();
            await cache.SetStringAsync($"cart:session:{sessionId}", cartId, options, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis セッションキャッシュ書き込みエラー: SessionId={SessionId}", sessionId);
        }
    }

    public async Task<string?> GetCartIdByUserAsync(string customerId, CancellationToken ct = default)
    {
        try
        {
            return await cache.GetStringAsync($"cart:user:{customerId}", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis ユーザーキャッシュ読み取りエラー: CustomerId={CustomerId}", customerId);
            return null;
        }
    }

    public async Task SetCartIdByUserAsync(
        string customerId, string cartId, CancellationToken ct = default)
    {
        try
        {
            var options = CreateCacheOptions();
            await cache.SetStringAsync($"cart:user:{customerId}", cartId, options, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis ユーザーキャッシュ書き込みエラー: CustomerId={CustomerId}", customerId);
        }
    }
}
