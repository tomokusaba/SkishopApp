namespace SalesManagementService.DTOs.Responses;

public record OrderDto(
    string Id, string OrderNumber, string CustomerId,
    DateTimeOffset OrderDate, string Status, string PaymentStatus,
    decimal SubtotalAmount, decimal TaxAmount, decimal ShippingFee,
    decimal DiscountAmount, decimal TotalAmount,
    string CurrencyCode, DateTimeOffset CreatedAt);
