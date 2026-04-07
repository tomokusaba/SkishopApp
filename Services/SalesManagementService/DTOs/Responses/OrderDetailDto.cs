namespace SalesManagementService.DTOs.Responses;

public record OrderDetailDto(
    string Id, string OrderNumber, string CustomerId,
    DateTimeOffset OrderDate, string Status, string PaymentStatus,
    string PaymentMethod, decimal SubtotalAmount, decimal TaxAmount,
    decimal ShippingFee, decimal DiscountAmount, decimal TotalAmount,
    string? CouponCode, int UsedPoints, decimal PointDiscountAmount,
    string? ShippingPostalCode, string? ShippingPrefecture,
    string? ShippingCity, string? ShippingAddressLine1,
    string? ShippingAddressLine2, string? ShippingRecipientName,
    string? ShippingPhoneNumber, string CurrencyCode,
    string? Notes, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    IReadOnlyList<OrderItemDto> Items);
