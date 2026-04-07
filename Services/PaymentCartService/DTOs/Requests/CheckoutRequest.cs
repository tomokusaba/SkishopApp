using System.ComponentModel.DataAnnotations;
using PaymentCartService.Models.ValueObjects;

namespace PaymentCartService.DTOs.Requests;

public record CheckoutRequest(
    [Required(ErrorMessage = "カート ID は必須です")]
    [StringLength(36)]
    string CartId,

    [Required(ErrorMessage = "決済方法は必須です")]
    [StringLength(50)]
    string PaymentMethod,

    ShippingAddress? ShippingAddress,

    [StringLength(50)]
    string? CouponCode,

    [Range(0, int.MaxValue)]
    int? UsedPoints);
