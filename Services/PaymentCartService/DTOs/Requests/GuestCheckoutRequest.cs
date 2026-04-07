using System.ComponentModel.DataAnnotations;
using PaymentCartService.Models.ValueObjects;

namespace PaymentCartService.DTOs.Requests;

public record GuestCheckoutRequest(
    [StringLength(36)]
    string? CartId,

    [Required(ErrorMessage = "決済方法は必須です")]
    [StringLength(50)]
    string PaymentMethod,

    [Required(ErrorMessage = "配送先は必須です")]
    ShippingAddress ShippingAddress,

    [Required(ErrorMessage = "メールアドレスは必須です")]
    [EmailAddress(ErrorMessage = "有効なメールアドレスを入力してください")]
    [StringLength(255)]
    string Email);
