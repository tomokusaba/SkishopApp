using System.ComponentModel.DataAnnotations;

namespace PaymentCartService.DTOs.Requests;

public record RefundRequest(
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "返金金額は 0 より大きい値を指定してください")]
    decimal Amount,

    [Required(ErrorMessage = "返金理由は必須です")]
    [StringLength(500)]
    string Reason);
