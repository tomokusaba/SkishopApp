using System.ComponentModel.DataAnnotations;

namespace PaymentCartService.DTOs.Requests;

public record UpdateCartItemRequest(
    [Required]
    [Range(1, 10, ErrorMessage = "数量は 1〜10 の範囲で指定してください")]
    int Quantity);
