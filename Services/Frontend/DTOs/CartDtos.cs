namespace Frontend.DTOs;

public record CartDto(
    string Id,
    List<CartItemDto> Items,
    decimal Subtotal,
    decimal Tax,
    decimal ShippingFee,
    decimal CouponDiscount,
    decimal PointDiscount,
    decimal Total,
    int EarnablePoints,
    string? AppliedCouponCode);

public record CartItemDto(
    string Id,
    string ProductId,
    string ProductName,
    string? ImageUrl,
    string? Size,
    string? Color,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal,
    int StockQuantity);

public record AddCartItemRequest(
    string ProductId,
    int Quantity,
    string? Size = null,
    string? Color = null);

public record UpdateCartItemRequest(int Quantity);
