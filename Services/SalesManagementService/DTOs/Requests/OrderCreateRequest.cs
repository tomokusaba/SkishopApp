using System.ComponentModel.DataAnnotations;

namespace SalesManagementService.DTOs.Requests;

public record OrderCreateRequest(
    [Required] string CustomerId,
    [Required, MinLength(1)] IReadOnlyList<OrderItemRequest> Items,
    [Required] ShippingAddressRequest ShippingAddress,
    [Required, StringLength(50)] string PaymentMethod,
    [StringLength(50)] string? CouponCode = null,
    [Range(0, int.MaxValue)] int UsedPoints = 0,
    [StringLength(500)] string? Notes = null);
