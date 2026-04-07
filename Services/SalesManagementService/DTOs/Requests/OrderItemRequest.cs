using System.ComponentModel.DataAnnotations;

namespace SalesManagementService.DTOs.Requests;

public record OrderItemRequest(
    [Required, StringLength(100)] string ProductId,
    [Required, StringLength(200)] string ProductName,
    [Required, StringLength(100)] string Sku,
    [Range(0.01, double.MaxValue)] decimal UnitPrice,
    [Range(1, 99)] int Quantity);
