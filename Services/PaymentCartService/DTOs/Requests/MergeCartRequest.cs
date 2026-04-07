using System.ComponentModel.DataAnnotations;

namespace PaymentCartService.DTOs.Requests;

public record MergeCartRequest(
    [Required(ErrorMessage = "ゲストカート ID は必須です")]
    [StringLength(36)]
    string GuestCartId);
