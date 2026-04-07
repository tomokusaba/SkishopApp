using System.ComponentModel.DataAnnotations;

namespace SalesManagementService.DTOs.Requests;

public record ReturnCreateRequest(
    [Required, StringLength(36)] string OrderId,
    [Required, StringLength(36)] string OrderItemId,
    [Required, StringLength(30)] string Reason,
    [StringLength(2000)] string? ReasonDetail = null,
    [Range(1, 99)] int Quantity = 1);
