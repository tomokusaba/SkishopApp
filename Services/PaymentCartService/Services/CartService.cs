using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentCartService.Configurations;
using PaymentCartService.DTOs.Requests;
using PaymentCartService.DTOs.Responses;
using PaymentCartService.Exceptions;
using PaymentCartService.Models;
using PaymentCartService.Models.Enums;
using PaymentCartService.Repositories.Interfaces;
using PaymentCartService.Services.Interfaces;

namespace PaymentCartService.Services;

public class CartService(
    ICartRepository cartRepository,
    ICartCacheService cacheService,
    IOptions<CartSettings> cartOptions,
    TimeProvider timeProvider,
    ILogger<CartService> logger) : ICartService
{
    private readonly CartSettings _settings = cartOptions.Value;

    public async Task<CartResponse> GetCartAsync(string cartId, CancellationToken ct = default)
    {
        var cached = await cacheService.GetCartAsync(cartId, ct);
        if (cached is not null && cached.Status == CartStatus.Active)
            return MapToResponse(cached);

        var cart = await cartRepository.FindByIdWithItemsAsync(cartId, ct)
            ?? throw new NotFoundException($"カートが見つかりません: {cartId}");

        if (cart.Status != CartStatus.Active)
            throw new BusinessException("カートは有効ではありません", "CART-4091");

        return MapToResponse(cart);
    }

    public async Task<CartResponse> GetOrCreateCartAsync(
        string? cartId, string sessionId, CancellationToken ct = default)
    {
        if (cartId is not null)
        {
            var existing = await cartRepository.FindByIdWithItemsAsync(cartId, ct);
            if (existing is not null && existing.Status == CartStatus.Active)
                return MapToResponse(existing);
        }

        var sessionCart = await cartRepository.FindBySessionIdAsync(sessionId, ct);
        if (sessionCart is not null)
            return MapToResponse(sessionCart);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var newCart = new Cart
        {
            SessionId = sessionId,
            ExpiresAt = now.AddDays(_settings.ExpiryDays),
            CreatedAt = now,
            UpdatedAt = now
        };

        await cartRepository.AddAsync(newCart, ct);
        await cartRepository.SaveChangesAsync(ct);

        logger.LogInformation("カート作成: CartId={CartId}, SessionId={SessionId}", newCart.Id, sessionId);
        return MapToResponse(newCart);
    }

    public async Task<CartResponse> AddItemAsync(
        string cartId, AddCartItemRequest request, CancellationToken ct = default)
    {
        var cart = await cartRepository.FindByIdWithItemsAsync(cartId, ct)
            ?? throw new NotFoundException($"カートが見つかりません: {cartId}");

        if (cart.Status != CartStatus.Active)
            throw new BusinessException("カートは有効ではありません", "CART-4091");

        if (cart.Items.Count >= _settings.MaxItemsPerCart)
            throw new BusinessException(
                $"カートのアイテム数上限（{_settings.MaxItemsPerCart}）に達しています", "CART-4002");

        cart.AddItem(request.ProductId, request.ProductName, request.Sku, request.UnitPrice, request.Quantity);

        try
        {
            await cartRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "楽観的ロック競合: CartId={CartId}", cartId);
            throw new ConcurrencyException("カートが他のユーザーによって更新されました。再度お試しください。");
        }

        logger.LogInformation("カートアイテム追加: CartId={CartId}, ProductId={ProductId}, Quantity={Quantity}",
            cartId, request.ProductId, request.Quantity);

        await cacheService.SetCartAsync(cart, ct);
        return MapToResponse(cart);
    }

    public async Task<CartResponse> UpdateItemQuantityAsync(
        string cartId, string itemId, UpdateCartItemRequest request, CancellationToken ct = default)
    {
        var cart = await cartRepository.FindByIdWithItemsAsync(cartId, ct)
            ?? throw new NotFoundException($"カートが見つかりません: {cartId}");

        if (cart.Status != CartStatus.Active)
            throw new BusinessException("カートは有効ではありません", "CART-4091");

        cart.UpdateItemQuantity(itemId, request.Quantity);

        try
        {
            await cartRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "楽観的ロック競合: CartId={CartId}", cartId);
            throw new ConcurrencyException("カートが他のユーザーによって更新されました。再度お試しください。");
        }

        logger.LogInformation("カートアイテム更新: CartId={CartId}, ItemId={ItemId}, Quantity={Quantity}",
            cartId, itemId, request.Quantity);

        await cacheService.SetCartAsync(cart, ct);
        return MapToResponse(cart);
    }

    public async Task<CartResponse> RemoveItemAsync(
        string cartId, string itemId, CancellationToken ct = default)
    {
        var cart = await cartRepository.FindByIdWithItemsAsync(cartId, ct)
            ?? throw new NotFoundException($"カートが見つかりません: {cartId}");

        cart.RemoveItem(itemId);
        await cartRepository.SaveChangesAsync(ct);

        logger.LogInformation("カートアイテム削除: CartId={CartId}, ItemId={ItemId}", cartId, itemId);
        await cacheService.SetCartAsync(cart, ct);
        return MapToResponse(cart);
    }

    public async Task ClearCartAsync(string cartId, CancellationToken ct = default)
    {
        var cart = await cartRepository.FindByIdWithItemsAsync(cartId, ct)
            ?? throw new NotFoundException($"カートが見つかりません: {cartId}");

        cart.ClearItems();
        await cartRepository.SaveChangesAsync(ct);

        logger.LogInformation("カート全削除: CartId={CartId}", cartId);
        await cacheService.RemoveCartAsync(cartId, ct);
    }

    public async Task<CartResponse> MergeCartAsync(
        string guestCartId, string userId, CancellationToken ct = default)
    {
        var guestCart = await cartRepository.FindByIdWithItemsAsync(guestCartId, ct)
            ?? throw new NotFoundException($"ゲストカートが見つかりません: {guestCartId}");

        var userCart = await cartRepository.FindActiveByCustomerIdAsync(userId, ct);

        if (userCart is null)
        {
            guestCart.CustomerId = userId;
            await cartRepository.SaveChangesAsync(ct);
            logger.LogInformation("ゲストカートをユーザーに紐付け: CartId={CartId}, UserId={UserId}",
                guestCartId, userId);
            return MapToResponse(guestCart);
        }

        foreach (var guestItem in guestCart.Items)
        {
            var existingItem = userCart.Items.FirstOrDefault(i => i.ProductId == guestItem.ProductId);
            if (existingItem is not null)
            {
                existingItem.UpdateQuantity(existingItem.Quantity + guestItem.Quantity);
            }
            else
            {
                userCart.AddItem(
                    guestItem.ProductId, guestItem.ProductName,
                    guestItem.Sku, guestItem.UnitPrice, guestItem.Quantity);
            }
        }

        guestCart.MarkAsAbandoned();
        await cartRepository.SaveChangesAsync(ct);

        await cacheService.SetCartAsync(userCart, ct);
        await cacheService.RemoveCartAsync(guestCartId, ct);

        logger.LogInformation("カートマージ完了: GuestCartId={GuestCartId}, UserCartId={UserCartId}, UserId={UserId}",
            guestCartId, userCart.Id, userId);

        return MapToResponse(userCart);
    }

    private static CartResponse MapToResponse(Cart cart) => new(
        Id: cart.Id,
        CustomerId: cart.CustomerId,
        SessionId: cart.SessionId,
        Status: cart.Status.ToString().ToUpperInvariant(),
        Items: cart.Items.Select(i => new CartItemResponse(
            Id: i.Id,
            ProductId: i.ProductId,
            ProductName: i.ProductName,
            Sku: i.Sku,
            UnitPrice: i.UnitPrice,
            Quantity: i.Quantity,
            Subtotal: i.Subtotal,
            CreatedAt: i.CreatedAt
        )).ToList(),
        TotalItems: cart.Items.Sum(i => i.Quantity),
        TotalAmount: cart.CalculateTotal(),
        ExpiresAt: cart.ExpiresAt,
        CreatedAt: cart.CreatedAt,
        UpdatedAt: cart.UpdatedAt
    );
}
