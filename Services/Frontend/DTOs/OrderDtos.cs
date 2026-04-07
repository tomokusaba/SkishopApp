namespace Frontend.DTOs;

public record CreateOrderRequest(
    string AddressId,
    string ShippingMethod,
    string? CouponCode,
    int PointsToUse,
    List<OrderItemRequest> Items);

public record OrderItemRequest(
    string ProductId,
    int Quantity,
    string? Size = null,
    string? Color = null);

public record CreateOrderResponse(
    string OrderId,
    string? CheckoutSessionUrl);

public record OrderDto(
    string Id,
    string OrderNumber,
    string Status,
    DateTime CreatedAt,
    decimal Subtotal,
    decimal Tax,
    decimal ShippingFee,
    decimal CouponDiscount,
    decimal PointDiscount,
    decimal Total,
    int EarnedPoints,
    string? CouponCode,
    string? ShippingMethod,
    ShippingAddressDto? ShippingAddress,
    List<OrderItemDto> Items);

public record OrderItemDto(
    string ProductId,
    string ProductName,
    string? ImageUrl,
    string? Size,
    string? Color,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal);

public record ShippingAddressDto(
    string Id,
    string FullName,
    string PostalCode,
    string Prefecture,
    string City,
    string AddressLine1,
    string? AddressLine2,
    string PhoneNumber);
