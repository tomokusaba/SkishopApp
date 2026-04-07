using System.ComponentModel.DataAnnotations;

namespace SalesManagementService.DTOs.Requests;

public record OrderStatusUpdateRequest([Required, StringLength(20)] string Status);
