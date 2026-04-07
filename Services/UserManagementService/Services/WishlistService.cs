using UserManagementService.DTOs.Requests;
using UserManagementService.DTOs.Responses;
using UserManagementService.Exceptions;
using UserManagementService.Models;
using UserManagementService.Repositories.Interfaces;
using UserManagementService.Services.Interfaces;

namespace UserManagementService.Services;

/// <summary>
/// ウィッシュリスト管理のビジネスロジック。リスト上限 10 件・アイテム上限 100 件の制限と、
/// 在庫復活通知処理を担当する。
/// </summary>
public class WishlistService(
    IWishlistRepository wishlistRepository,
    ILogger<WishlistService> logger) : IWishlistService
{
    /// <summary>ユーザーあたりのウィッシュリスト上限。</summary>
    private const int MaxWishlistsPerUser = 10;

    public async Task<List<WishlistDto>> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        var wishlists = await wishlistRepository.FindByUserIdAsync(userId, ct);
        return wishlists.Select(MapToDto).ToList();
    }

    public async Task<WishlistDto> CreateAsync(
        string userId, CreateWishlistRequest request, CancellationToken ct = default)
    {
        var count = await wishlistRepository.CountByUserIdAsync(userId, ct);
        if (count >= MaxWishlistsPerUser)
            throw new BusinessException($"ウィッシュリストは最大 {MaxWishlistsPerUser} 件までです");

        var wishlist = new Wishlist
        {
            UserId = userId,
            Name = request.Name,
            IsDefault = request.IsDefault || count == 0
        };

        await wishlistRepository.AddAsync(wishlist, ct);
        await wishlistRepository.SaveChangesAsync(ct);
        logger.LogInformation("ウィッシュリストが作成されました: {WishlistId}, {UserId}", wishlist.Id, userId);
        return MapToDto(wishlist);
    }

    public async Task<WishlistDto> UpdateAsync(
        string userId, string wishlistId, UpdateWishlistRequest request, CancellationToken ct = default)
    {
        var wishlist = await wishlistRepository.FindByIdWithItemsAsync(wishlistId, ct)
            ?? throw new NotFoundException($"ウィッシュリストが見つかりません (ID: {wishlistId})");

        if (wishlist.UserId != userId)
            throw new ForbiddenException("このウィッシュリストにアクセスする権限がありません");

        if (request.Name is not null)
            wishlist.Rename(request.Name);
        if (request.IsDefault is true)
            wishlist.MarkAsDefault();

        await wishlistRepository.SaveChangesAsync(ct);
        return MapToDto(wishlist);
    }

    public async Task DeleteAsync(string userId, string wishlistId, CancellationToken ct = default)
    {
        var wishlist = await wishlistRepository.FindByIdAsync(wishlistId, ct)
            ?? throw new NotFoundException($"ウィッシュリストが見つかりません (ID: {wishlistId})");

        if (wishlist.UserId != userId)
            throw new ForbiddenException("このウィッシュリストにアクセスする権限がありません");

        wishlistRepository.Remove(wishlist);
        await wishlistRepository.SaveChangesAsync(ct);
        logger.LogInformation("ウィッシュリストが削除されました: {WishlistId}", wishlistId);
    }

    public async Task<WishlistItemDto> AddItemAsync(
        string userId, string wishlistId, AddWishlistItemRequest request, CancellationToken ct = default)
    {
        var wishlist = await wishlistRepository.FindByIdWithItemsAsync(wishlistId, ct)
            ?? throw new NotFoundException($"ウィッシュリストが見つかりません (ID: {wishlistId})");

        if (wishlist.UserId != userId)
            throw new ForbiddenException("このウィッシュリストにアクセスする権限がありません");

        var item = wishlist.AddItem(request.ProductId, request.ShouldNotifyOnRestock);
        await wishlistRepository.SaveChangesAsync(ct);
        logger.LogInformation("ウィッシュリストアイテムが追加されました: {ItemId}, {WishlistId}", item.Id, wishlistId);
        return MapItemToDto(item);
    }

    public async Task RemoveItemAsync(
        string userId, string wishlistId, string itemId, CancellationToken ct = default)
    {
        var wishlist = await wishlistRepository.FindByIdWithItemsAsync(wishlistId, ct)
            ?? throw new NotFoundException($"ウィッシュリストが見つかりません (ID: {wishlistId})");

        if (wishlist.UserId != userId)
            throw new ForbiddenException("このウィッシュリストにアクセスする権限がありません");

        wishlist.RemoveItem(itemId);
        await wishlistRepository.SaveChangesAsync(ct);
    }

    /// <summary>
    /// ウィッシュリストからアイテムを取り出し、カートへの移動をトリガーする。
    /// 現在は Remove のみ実行し、カート追加は PaymentCartService が担当する。
    /// </summary>
    public async Task MoveItemToCartAsync(
        string userId, string wishlistId, string itemId, CancellationToken ct = default)
    {
        var wishlist = await wishlistRepository.FindByIdWithItemsAsync(wishlistId, ct)
            ?? throw new NotFoundException($"ウィッシュリストが見つかりません (ID: {wishlistId})");

        if (wishlist.UserId != userId)
            throw new ForbiddenException("このウィッシュリストにアクセスする権限がありません");

        var item = wishlist.RemoveItem(itemId);
        await wishlistRepository.SaveChangesAsync(ct);
        logger.LogInformation("アイテムをカートに移動しました: {ItemId}, ProductId={ProductId}", itemId, item.ProductId);
    }

    /// <summary>
    /// 在庫復活イベント受信時に、該当商品をウォッチ中のウィッシュリストを検索しログ出力する。
    /// 実際の通知送信は MailSendService が担当する。
    /// </summary>
    public async Task ProcessRestockNotificationAsync(string productId, CancellationToken ct = default)
    {
        var wishlists = await wishlistRepository.FindByProductIdWithRestockNotifyAsync(productId, 100, ct);
        var itemCount = wishlists.SelectMany(w => w.Items).Count();
        logger.LogInformation("在庫復活通知対象: ProductId={ProductId}, 件数={Count}", productId, itemCount);
    }

    private static WishlistDto MapToDto(Wishlist w) =>
        new(w.Id, w.UserId, w.Name, w.IsDefault,
            w.Items.Select(MapItemToDto).ToList(), w.CreatedAt);

    private static WishlistItemDto MapItemToDto(WishlistItem i) =>
        new(i.Id, i.ProductId, i.AddedAt, i.ShouldNotifyOnRestock, i.NotifiedAt);
}
