namespace SalesManagementService.DTOs.Responses;

public record OrderItemDto(
    string Id, string ProductId, string ProductName, string Sku,
    decimal UnitPrice, int Quantity, decimal Subtotal);
