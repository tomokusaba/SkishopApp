using System.ComponentModel.DataAnnotations;

namespace PaymentCartService.DTOs.Requests;

public record AddCartItemRequest(
    [Required(ErrorMessage = "商品 ID は必須です")]
    [StringLength(100)]
    string ProductId,

    [Required(ErrorMessage = "商品名は必須です")]
    [StringLength(200)]
    string ProductName,

    [Required(ErrorMessage = "SKU は必須です")]
    [StringLength(100)]
    string Sku,

    [Required]
    [Range(0.01, 99999999.99, ErrorMessage = "単価は 0.01〜99999999.99 の範囲で指定してください")]
    decimal UnitPrice,

    [Required]
    [Range(1, 10, ErrorMessage = "数量は 1〜10 の範囲で指定してください")]
    int Quantity);
