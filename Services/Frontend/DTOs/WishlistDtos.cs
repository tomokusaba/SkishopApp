namespace Frontend.DTOs;

public record WishlistDto(
    string Id,
    string Name,
    string? Description,
    List<WishlistItemDto> Items,
    DateTime CreatedAt);

public record WishlistItemDto(
    string Id,
    string ProductId,
    string ProductName,
    decimal Price,
    string? ImageUrl,
    bool InStock,
    DateTime AddedAt);

public record CreateWishlistRequest(string Name, string? Description);

public record UpdateWishlistRequest(string Name, string? Description);

public record AddWishlistItemRequest(string ProductId);
